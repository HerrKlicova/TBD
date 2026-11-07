using System.Collections.Generic;
using TBD.Net;
using UnityEngine;
using UnityEngine.Rendering;

namespace TBD.View
{
    /// <summary>
    /// Monta la zona de una estación a partir de su dato (EstacionDato, estaciones/&lt;id&gt;.json) con el kit de Sagunto y el kit modular de Teo,
    /// pieza a pieza y con la variante por hash de la posición (la misma regla que Sagunto). No depende de PrototypeBootstrap ni carga escenas:
    /// Marcos lo llama desde OperationsMap3D al entrar en un nodo que no sea Sagunto (acuerdo del 7-nov). Solo es aspecto: no toca la simulación.
    /// Lo que no tiene modelo todavía sale como una caja gris con su nombre, para que se vea qué falta.
    /// </summary>
    public static class EstacionVista
    {
        public const float ZoneW = 256f, ZoneD = 128f;
        const float RailGauge = 1.435f;
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static Material _ballast, _rail, _hesco, _ground, _platform, _missing;
        static readonly Dictionary<int, Material> DecalMats = new Dictionary<int, Material>();

        /// <summary>Lo que se ha montado: las piezas sin modelo y las cisternas (para enseñar el stock con el decal).</summary>
        public sealed class Result
        {
            public Transform Root;
            public readonly List<string> Missing = new List<string>();
            public readonly List<Transform> Tanks = new List<Transform>();
            public int Placed;
        }

        /// <summary>Carga el dato de una estación (Assets/TBD/Data/estacion_&lt;id&gt;.json en el Editor; en la compilación, donde lo copie BalanceBuildCopy).</summary>
        public static EstacionDato Load(string id)
        {
            var json = BalanceSource.Read("estacion_" + id + ".json");
            return json == null ? null : EstacionDato.FromJson(json);
        }

        public static Result Build(Transform root, EstacionDato dato)
        {
            var res = new Result { Root = root };
            if (dato == null) return res;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad); ground.name = "Suelo " + dato.Nombre;
            Kill(ground.GetComponent<Collider>());
            ground.transform.SetParent(root, false);
            ground.transform.localPosition = new Vector3(ZoneW * 0.5f, 0f, ZoneD * 0.5f); ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); ground.transform.localScale = new Vector3(ZoneW, ZoneD, 1f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = _ground ??= Flat("8a7a5c");

            var tracks = new GameObject("Vías").transform; tracks.SetParent(root, false);
            foreach (var v in dato.Vias) for (int i = 0; i + 3 < v.Length; i += 2) Track(tracks, new Vector2(v[i], v[i + 1]), new Vector2(v[i + 2], v[i + 3]));

            var pieces = new GameObject("Piezas").transform; pieces.SetParent(root, false);
            foreach (var p in dato.Piezas) PlacePiece(pieces, p, res);

            if (dato.Recinto != null) Enclosure(root, dato);
            return res;
        }

        public enum RadarEstado : byte { Ninguno = 0, RecienLlegado = 1, Obra = 2, Segura = 3, Fortificado = 4, Caido = 5 }

        /// <summary>
        /// Pone (o quita) el radar de Teo en el sitio de inteligencia de la estación (Castellón: (206; 0; 82), yaw 180). Reparto de Lucía por estado:
        /// v3 plegado mientras se construye, v1 instalación cuando está Segura, v2 la mejora fortificada, v4 destruido si la estación cae y v0 recién
        /// llegado en el tren. Castellón empieza infestada, así que al principio no hay radar. La antena gira (una vuelta cada 4 s) solo si «encendido».
        /// </summary>
        public static GameObject SetRadar(Result r, EstacionDato dato, RadarEstado estado, bool encendido = true)
        {
            var old = r.Root.Find("Radar"); if (old != null) Kill(old.gameObject);
            if (estado == RadarEstado.Ninguno || dato == null || float.IsNaN(dato.InteligenciaX)) return null;
            string file;
            switch (estado)
            {
                case RadarEstado.RecienLlegado: file = "SM_Radar_v0_RecienLlegado_LOD1"; break;
                case RadarEstado.Obra: file = "SM_Radar_v3_Plegado_LOD1"; break;
                case RadarEstado.Fortificado: file = "SM_Radar_v2_Fortificado_LOD1"; break;
                case RadarEstado.Caido: file = "SM_Radar_v4_Destruido_LOD1"; break;
                default: file = "SM_Radar_v1_Instalacion_LOD1"; break;
            }
            var go = Model("Radar", file, r.Root) ?? Placeholder(r.Root, "radar");
            go.name = "Radar";
            go.transform.localPosition = new Vector3(dato.InteligenciaX, 0f, dato.InteligenciaZ);
            go.transform.localRotation = Quaternion.Euler(0f, dato.InteligenciaYaw, 0f);      // Castellón: 180, la lanza hacia el andén
            var antena = FindDeep(go.transform, "Antena");
            if (antena != null) { var spin = antena.gameObject.AddComponent<RadarAntena>(); spin.Encendido = encendido; }
            return go;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name || t.name.StartsWith(name + ".")) return t;
            for (int i = 0; i < t.childCount; i++) { var f = FindDeep(t.GetChild(i), name); if (f != null) return f; }
            return null;
        }

        /// <summary>Decal de carga de las cisternas (Bruno): N = round(4 · carga / capacidad), el mismo en todas las del depósito.</summary>
        public static void SetTankLoad(Result r, int cargaU, int capacidadU)
        {
            int n = capacidadU <= 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(4f * cargaU / capacidadU), 0, 4);
            var mat = DecalMat(n);
            foreach (var t in r.Tanks) foreach (var mr in t.GetComponentsInChildren<MeshRenderer>()) if (mr.gameObject.name.StartsWith("Decal")) mr.sharedMaterial = mat;
        }

        // ------------------------------------------------------------------ piezas
        static void PlacePiece(Transform parent, EstacionPieza p, Result res)
        {
            var pos = new Vector3(p.X, p.Y, p.Z);
            GameObject go = null;
            string name = p.Pieza;
            if (name.StartsWith("sagunto/"))
            {
                string piece = name.Substring(8);
                int k = EstacionDato.VariantOf(p.X, p.Z, 6);
                go = SaguntoPiece(piece, k, parent);
            }
            else if (name == "kit/anden_10m") go = Model("KitEstaciones", "SM_KitAnden_v" + PlatformVariant(p) + "_LOD1", parent);
            else if (name == "kit_paso") go = Model("KitEstaciones", "SM_KitPaso_v" + Mathf.Max(0, p.Variante) + "_LOD1", parent);   // paso inferior: el de Castellón por defecto (LEEME de Teo)
            else if (name == "kit_cuerpo" || name == "kit_remate")
            {
                // módulos del edificio: la variante la elige Álvaro en el JSON (es composición, no azar); fondo distinto de 9 m → escala en Z (LEEME de Teo)
                string mod = name == "kit_cuerpo" ? "Cuerpo" : "Remate";
                int k = p.Variante >= 0 ? p.Variante : EstacionDato.VariantOf(p.X, p.Z, name == "kit_cuerpo" ? 7 : 6);
                go = Model("KitEstaciones", "SM_Kit" + mod + "_v" + k + "_LOD1", parent);
                if (go != null && p.Fondo > 0f) go.transform.localScale = new Vector3(1f, 1f, p.Fondo / 9f);
            }
            else if (name == "kit/torreta_vigilancia")
            {
                int k = p.Variante >= 0 ? Mathf.Clamp(p.Variante, 0, 4) : 2;          // v2 improvisada: la heredada (Teo); el estado la cambia (v0 Segura, v3 Liberada, v4 Caída)
                go = Model("CastellonPiezas", "SM_Torreta_v" + k + "_" + TowerNames[k] + "_LOD1", parent);
            }
            else if (name == "castellon/talud_tierra_10m")
            {
                int k = p.Variante >= 0 ? Mathf.Clamp(p.Variante, 0, 4) : 0;          // v0 liso por defecto; v1 sacos (mejora), v2 erosionado, v4 reventado (ataque)
                go = Model("CastellonPiezas", "SM_TaludDeposito_v" + k + "_" + BermNames[k] + "_LOD1", parent);
            }
            else if (name == "nuevo/cuerpo_central_viajeros") go = CentralBody(parent, p);
            else if (name == "vagon_cisterna")
            {
                int k = EstacionDato.VariantOf(p.X, p.Z, 5);
                go = Model("Cisterna", "SM_VagonCisterna_v" + k + "_" + TankNames[k] + "_LOD1", parent);
                if (go != null) { AddTankDecals(go.transform); res.Tanks.Add(go.transform); }
            }
            else if (name.Contains("/SM_"))
            {
                // pieza de Teo nombrada por su carpeta y su malla (puesto_frente/SM_PuestoFrente, puerta_vehiculos/SM_PuertaVehiculos): 5 variantes vN
                int slash = name.IndexOf('/');
                string folder = name.Substring(0, slash), mesh = name.Substring(slash + 1);
                int k = p.Variante >= 0 ? p.Variante : EstacionDato.VariantOf(p.X, p.Z, 5);
                go = Model(folder, mesh + "_v" + k + "_LOD1", parent);
            }
            if (go == null) { go = Placeholder(parent, name); res.Missing.Add(name + " (" + p.X + ", " + p.Z + ")"); }
            float yaw = p.Yaw + (name == "vagon_cisterna" ? 90f : 0f);      // el JSON da el vagón a lo largo de la vía (X) y el modelo de Teo va a lo largo de Z
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
            res.Placed++;
        }

        /// <summary>
        /// Pieza de Sagunto con sus dos formatos de carpeta: base + v1..v5 en subcarpetas (edificio, bloque...) o ficheros planos SM_pieza_vK (muelle). Si la
        /// variante no existe (paso superior: una sola), sale la base.
        /// </summary>
        static GameObject SaguntoPiece(string piece, int k, Transform parent)
        {
            string f = "Sagunto/" + piece;
            return (k == 0 ? Model(f, "SM_" + piece + "_LOD1", parent) : Model(f + "/v" + k, "SM_" + piece + "_v" + k + "_LOD1", parent))
                ?? Model(f, "SM_" + piece + "_v" + k + "_LOD1", parent)
                ?? Model(f, "SM_" + piece + "_LOD1", parent)
                ?? Model(f, "SM_" + piece + "_v0_LOD1", parent);
        }

        static readonly string[] TankNames = { "Limpia", "Oxidada", "SinEscalera", "Abollada", "Quemada" };
        static readonly string[] TowerNames = { "Acero", "Hesco", "Improvisada", "Reparacion", "Caida" };
        static readonly string[] BermNames = { "Liso", "Sacos", "Erosionado", "Esquina", "Reventado" };

        /// <summary>Andén: de las 6 variantes de Teo, las de uso normal (0 lateral, 1 bajo, 4 bordillo, 5 isla); la 2 (roto) y la 3 (rampa) son de estado o de remate y no salen por azar.</summary>
        static int PlatformVariant(EstacionPieza p) { int[] ok = { 0, 1, 4, 5 }; return ok[EstacionDato.VariantOf(p.X, p.Z, ok.Length)]; }

        /// <summary>
        /// Cuerpo central del edificio de viajeros de Castellón (Álvaro: la única extrusión nueva). Mientras no haya pieza propia, son 4 módulos de
        /// edificio del kit de Teo (4 m) encadenados en X y escalados en Z de 9 a 13 m de fondo, como pide su LEEME. SIN VERIFICAR la anchura del hueco.
        /// </summary>
        static GameObject CentralBody(Transform parent, EstacionPieza p)
        {
            var g = new GameObject("Cuerpo central de viajeros"); g.transform.SetParent(parent, false);
            int[] mods = { 0, 6, 6, 0 };
            for (int i = 0; i < mods.Length; i++)
            {
                var m = Model("KitEstaciones", "SM_KitCuerpo_v" + mods[i] + "_LOD1", g.transform);
                if (m == null) { Kill(g); return null; }
                m.transform.localPosition = new Vector3((i - 1.5f) * 3.98f, 0f, 0f); m.transform.localScale = new Vector3(1f, 1f, 13f / 9f);
            }
            return g;
        }

        static GameObject Model(string folder, string file, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Models/" + folder + "/" + file);
            if (prefab == null) return null;
            var inst = Object.Instantiate(prefab, parent); inst.name = file;
            if (!Mats.TryGetValue(folder, out var mat) || mat == null)
            {
                var tex = Resources.Load<Texture2D>("Models/" + folder + "/T_Paleta32") ?? Resources.Load<Texture2D>("Models/" + RootFolder(folder) + "/T_Paleta32");
                mat = Flat("ffffff"); if (tex != null) mat.SetTexture("_BaseMap", tex); mat.enableInstancing = true; Mats[folder] = mat;
            }
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.On; }
            return inst;
        }

        static string RootFolder(string folder) { int i = folder.IndexOf('/'); int j = i < 0 ? -1 : folder.IndexOf('/', i + 1); return j < 0 ? folder : folder.Substring(0, j); }

        static GameObject Placeholder(Transform parent, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "FALTA " + name;
            Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localScale = new Vector3(4f, 3f, 4f);
            go.GetComponent<MeshRenderer>().sharedMaterial = _missing ??= Flat("9a9a9a");
            return go;
        }

        // Panel liso de Teo (2,6 × 1,1 m) a 3,0 m de altura y a 1,52 m del eje, en los dos costados; el vagón va a lo largo de Z.
        static void AddTankDecals(Transform tank)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Decal carga";
                Kill(q.GetComponent<Collider>());
                q.transform.SetParent(tank, false);
                q.transform.localPosition = new Vector3(1.53f * s, 3.0f, 0f);
                q.transform.localRotation = Quaternion.Euler(0f, s > 0 ? -90f : 90f, 0f);
                q.transform.localScale = new Vector3(2.6f, 1.1f, 1f);
                var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = DecalMat(0); mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static Material DecalMat(int n)
        {
            if (DecalMats.TryGetValue(n, out var m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var tex = Resources.Load<Texture2D>("Models/Cisterna/decal/decal_cisterna_carga_" + n + "_52x22");
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); }
            else m.SetColor("_BaseColor", Color.Lerp(new Color(0.38f, 0.36f, 0.28f), new Color(0.85f, 0.7f, 0.2f), n / 4f));
            DecalMats[n] = m; return m;
        }

        // ------------------------------------------------------------------ vías y recinto (primitivas hasta que haya pieza)
        static void Track(Transform parent, Vector2 a, Vector2 b)
        {
            var d = b - a; float len = d.magnitude; if (len < 0.01f) return;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            var mid = (a + b) * 0.5f;
            Bar(parent, "Balasto", new Vector3(mid.x, 0.1f, mid.y), yaw, new Vector3(3.2f, 0.2f, len), _ballast ??= Flat("6e6458"));
            var side = new Vector2(d.y, -d.x).normalized * (RailGauge * 0.5f);
            Bar(parent, "Carril", new Vector3(mid.x + side.x, 0.32f, mid.y + side.y), yaw, new Vector3(0.08f, 0.15f, len), _rail ??= Flat("4a4440"));
            Bar(parent, "Carril", new Vector3(mid.x - side.x, 0.32f, mid.y - side.y), yaw, new Vector3(0.08f, 0.15f, len), _rail);
        }

        /// <summary>Recinto de HESCO: tramos de 1,2 × 2,1 m a lo largo del polígono, con hueco de 8 m en cada puerta (las piezas de puerta van aparte).</summary>
        static void Enclosure(Transform root, EstacionDato dato)
        {
            var t = new GameObject("Recinto").transform; t.SetParent(root, false);
            var r = dato.Recinto; int n = r.Length / 2;
            for (int i = 0; i < n; i++)
            {
                var a = new Vector2(r[i * 2], r[i * 2 + 1]); var b = new Vector2(r[(i + 1) % n * 2], r[(i + 1) % n * 2 + 1]);
                var d = b - a; float len = d.magnitude; if (len < 0.01f) continue;
                var u = d / len;
                // cortes: las puertas que caen sobre este lado
                var cuts = new List<Vector2>();
                foreach (var g in dato.Puertas)
                {
                    var p = new Vector2(g.X, g.Z); float s = Vector2.Dot(p - a, u);
                    if (s < 0f || s > len) continue;
                    if ((a + u * s - p).sqrMagnitude > 1f) continue;
                    float half = g.Tipo == "via" ? 3f : 4f;
                    cuts.Add(new Vector2(s - half, s + half));
                }
                cuts.Sort((x, y) => x.x.CompareTo(y.x));
                float from = 0f;
                foreach (var c in cuts) { Wall(t, a, u, from, Mathf.Max(from, c.x)); from = Mathf.Max(from, c.y); }
                Wall(t, a, u, from, len);
            }
        }

        static void Wall(Transform parent, Vector2 a, Vector2 u, float s0, float s1)
        {
            if (s1 - s0 < 0.2f) return;
            var mid = a + u * ((s0 + s1) * 0.5f);
            Bar(parent, "HESCO", new Vector3(mid.x, 1.05f, mid.y), Mathf.Atan2(u.x, u.y) * Mathf.Rad2Deg, new Vector3(1.2f, 2.1f, s1 - s0), _hesco ??= Flat("a08a62"));
        }

        static void Bar(Transform parent, string name, Vector3 pos, float yaw, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        static void Kill(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }

        static Material Flat(string hex)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0f);
            return m;
        }
    }
}
