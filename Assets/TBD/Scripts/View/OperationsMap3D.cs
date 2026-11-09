using System.Collections.Generic;
using System.IO;
using TBD.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// Hito 3 «Unión», parte 1: mapa de operaciones en 3D sobre el terreno compartido (TBD.Net.IElevationSource), con la red, la
    /// mancha de horda, los trenes y el estado de los nodos encima. Cámara libre (girar, inclinar, zoom continuo de cientos de km
    /// a unos cientos de metros). Va a resolución completa, sin pixelar (GDD §7). Escena: Assets/Scenes/Union.unity.
    /// Unidades de Unity del mapa: 1 = 1 km. Relieve con exageración vertical.
    /// </summary>
    public sealed partial class OperationsMap3D : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // AfterSceneLoad solo se ejecuta al darle a Play; al cambiar de escena (mapa ↔ zona, hito 3) lo hace sceneLoaded.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GoldenRunner.Active) return;            // modo repetición dorada: sin juego
            if (scene.name != "Union") return;
            if (FindFirstObjectByType<OperationsMap3D>() != null) return;
            new GameObject("TBD_OperationsMap3D").AddComponent<OperationsMap3D>();
        }

        public const float MinX = 14f, MaxX = 80f, MinY = 6f, MaxY = 86f;   // extensión del mapa en unidades de mapa
        public const float Exag = 6f;                                        // exageración vertical (PROTOTIPO)
        const int GridX = 199, GridY = 241;                                  // vértices del terreno grueso (≈ 1,2 km por punto)
        const int TexW = 396, TexH = 480;                                    // textura de color del terreno (y la mancha encima)

        NetWorld _a, _b;
        IElevationSource _relief;
        float _acc;
        bool _check = true, _desync;
        Camera _cam;
        Light _sun;
        Mesh _mesh;
        Material _terrainMat, _lineMat;
        Texture2D _tex;
        Color32[] _baseColors, _px;
        readonly List<LineRenderer> _rails = new List<LineRenderer>();
        readonly List<Transform> _nodes = new List<Transform>();
        readonly List<Transform> _trains = new List<Transform>();
        long _lastFieldTick = -1;

        // detalle del terreno alrededor del foco (misma función de elevación, más fina): enlaza el mapa con la zona
        const int DetailN = 161;                 // vértices por lado
        const float DetailSizeKm = 12f;          // 12 x 12 km, unos 75 m entre puntos
        GameObject _detail;
        Mesh _detailMesh;
        Texture2D _detailTex;
        Vector3 _detailCenter = new Vector3(-9999, 0, -9999);
        Vector3[] _coarseVerts, _coarseWork;
        Color32[] _detailBase, _detailPx;               // colores de relieve del parche de detalle y con la mancha encima
        GUIStyle _label, _small, _title;
        Texture2D _panelTex;
        GUIStyle _panel;

        // cámara libre
        Vector3 _focus;
        // D-N170 (Iker): la cámara la mueven también los gestos del mapa (doble clic en una localización o en un tren, TrenPanel). Solo estas cuatro piezas.
        public Vector3 Foco { get { return _focus; } set { _focus = new Vector3(value.x, 0, value.z); } }
        public float DistanciaObjetivo { get { return _targetDist; } set { _targetDist = value; } }
        public int TrenesVistos { get { return _trains.Count; } }
        public Vector3 TrenPos(int i) { return _trains[i].position; }
        float _dist = 230f, _targetDist = 230f, _yaw = 0f, _pitch = 50f;

        // ---------------------------------------------------------------- conversión mapa ↔ mundo
        public static Vector3 MapToWorldFlat(double mx, double my)
        {
            return new Vector3((float)((mx - MinX) * MapGeo.KmPerUnit), 0f, (float)((MaxY - my) * MapGeo.KmPerUnit));
        }

        public Vector3 MapToWorld(double mx, double my, float lift = 0f)
        {
            var p = MapToWorldFlat(mx, my);
            double h = _relief.HeightM(mx, my);
            p.y = (float)(System.Math.Max(0, h) / 1000.0 * Exag) + lift;
            return p;
        }

        float _mapT0;
        void Update_NightReturn()
        {
            var s = NightRun.Session; if (s == null || !s.Away || _entering) return;
            var cl = System.Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < cl.Length; i++) if (cl[i] == "-returnafter" && float.TryParse(cl[i + 1], out var rt) && Time.realtimeSinceStartup - _mapT0 >= rt) { Debug.Log("[NOCHE] prueba: -returnafter, vuelvo a Sagunto"); EnterZone(0); }
        }
        void Start()
        {
            _mapT0 = Time.realtimeSinceStartup;
            if (NightRun.Active && NightRun.Session == null && !PruebaDeMapa) { EnterZone(ZonaArg()); return; }                            // «Jugar»: directo a la noche de Sagunto (D-N71); QA: -zona <nombre> entra en otra (D-N173, Sara)
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-stress10k-sagunto") >= 0) { EnterZone(0); return; }       // prueba de rendimiento en la zona de Sagunto (con la muestra del suelo)
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-stress10k") >= 0 && NetSession.DebugForceAmbush()) { NetSession.PendingAmbush = true; UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene"); return; }   // prueba de rendimiento (D-N61)
            Application.targetFrameRate = 60;
            NetSession.Ensure();                       // la red vive en la sesión y sigue corriendo dentro de las zonas (hito 3)
            _a = NetSession.A;
            ColmenaEstadoFuente = () => NetSession.A.Director.ColState; ColmenaSenalFuente = () => (int)NetSession.A.Director.ColSignalMil;   // D-N160: la Colmena de la red (Nacho) en la capa APP-6 y el informe (Bruno)
            if (!TutorialHud.Activo && _a.Director.ColmenaIntro(_a)) { Sfx.Play("sfx_colmena_dispersa_2", 1f); Sfx.Duck(3f); _intelVisto = _a.Intel.Count; }   // CO-0 (Nacho; Lucía: una vez por partida, también tras cargar)
            _b = NetSession.B;
            _relief = new PlaceholderRelief(_a.Graph);
            SetupCamera();
            BuildTerrain();
            BuildNetwork();
            var sag = _a.Graph.Nodes.Find(n => n.Name == "Sagunto");
            _focus = MapToWorldFlat(sag.XMeters - 6, sag.YMeters - 14);
            _fadeStart = Time.realtimeSinceStartup;
            if (NetSession.ReturnFromNode >= 0)
            {
                // vuelta de una zona: la cámara aparece sobre ese nodo a 2,5 km (por encima del umbral de entrada: histéresis)
                var n = _a.Graph.Nodes[NetSession.ReturnFromNode];
                _focus = MapToWorldFlat(n.XMeters, n.YMeters);
                _dist = _targetDist = 2.5f;
                _pitch = 50f;
                NetSession.ReturnFromNode = -1;
            }
        }

        /// <summary>QA: -trenpanel-auto y -refuerzos-auto se juegan en el mapa (TrenPanel solo vive en Union); con ellas no se entra directo en la noche.</summary>
        static bool PruebaDeMapa { get { var a = System.Environment.GetCommandLineArgs(); return System.Array.IndexOf(a, "-trenpanel-auto") >= 0 || System.Array.IndexOf(a, "-refuerzos-auto") >= 0 || System.Array.IndexOf(a, "-dia2-auto") >= 0 || System.Array.IndexOf(a, "-dia2-auto-sinescolta") >= 0 || System.Array.IndexOf(a, "-dia2-auto-tren-aparcado") >= 0; } }

        /// <summary>QA (Sara, D-N173): -zona &lt;nombre&gt; (Nules, Castellón...) elige la zona de la noche; sin ella o si no existe, Sagunto (nodo 0).</summary>
        static int ZonaArg()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-zona"); if (i < 0 || i + 1 >= a.Length) return 0;
            NetSession.Ensure(); string id = TrenPreparar.IdDe(a[i + 1]); int n = TrenPreparar.NodoPorId(NetSession.A, id);
            if (n < 0) { Debug.LogWarning("[NOCHE] -zona " + a[i + 1] + ": no es un nodo de la red (Bétera es el aeródromo, va aparte); entro en Sagunto"); return 0; }
            Debug.Log("[NOCHE] -zona " + a[i + 1] + " → nodo " + n); return n;
        }

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                _cam = new GameObject("Camera").AddComponent<Camera>();
                _cam.tag = "MainCamera";
            }
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 3000f;
            _cam.fieldOfView = 40f;
            _sun = FindFirstObjectByType<Light>();
            if (_sun == null) _sun = new GameObject("Sol").AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.transform.rotation = Quaternion.Euler(42f, 140f, 0f);
            _sun.intensity = 1.25f;
            _sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.38f);
        }

        // ---------------------------------------------------------------- terreno
        static Color32 Hypso(double h)
        {
            if (h < 0) return Color32.Lerp(new Color32(30, 58, 92, 255), new Color32(14, 30, 58, 255), (float)System.Math.Min(1, -h / 400));
            // llano en el ocre del suelo de la zona (#b4a080), para que el fundido mapa ↔ zona no salte de color (Lucía, 6-nov)
            Color32[] stops = { new Color32(180, 160, 128, 255), new Color32(168, 142, 106, 255), new Color32(152, 124, 92, 255), new Color32(132, 106, 82, 255), new Color32(118, 100, 90, 255), new Color32(176, 170, 162, 255) };
            double[] at = { 0, 200, 500, 900, 1300, 1700 };
            for (int i = 1; i < at.Length; i++)
                if (h < at[i]) return Color32.Lerp(stops[i - 1], stops[i], (float)((h - at[i - 1]) / (at[i] - at[i - 1])));
            return stops[stops.Length - 1];
        }

        void BuildTerrain()
        {
            var verts = new Vector3[GridX * GridY];
            var uvs = new Vector2[GridX * GridY];
            for (int j = 0; j < GridY; j++)
                for (int i = 0; i < GridX; i++)
                {
                    double mx = MinX + (MaxX - MinX) * i / (GridX - 1.0);
                    double my = MinY + (MaxY - MinY) * j / (GridY - 1.0);
                    var p = MapToWorldFlat(mx, my);
                    double h = _relief.HeightM(mx, my);
                    p.y = (float)(h < 0 ? h / 1000.0 * 2.0 : h / 1000.0 * Exag);
                    verts[j * GridX + i] = p;
                    uvs[j * GridX + i] = new Vector2(i / (GridX - 1f), 1f - j / (GridY - 1f));
                }
            var tris = new int[(GridX - 1) * (GridY - 1) * 6];
            int k = 0;
            for (int j = 0; j < GridY - 1; j++)
                for (int i = 0; i < GridX - 1; i++)
                {
                    int v = j * GridX + i;
                    tris[k++] = v; tris[k++] = v + 1; tris[k++] = v + GridX;
                    tris[k++] = v + 1; tris[k++] = v + GridX + 1; tris[k++] = v + GridX;
                }
            _coarseVerts = verts;
            _coarseWork = (Vector3[])verts.Clone();
            _mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = _coarseWork, uv = uvs, triangles = tris };
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            // plano táctico oscuro de Bruno (D-N113): paleta de 32, sombreado cocido, en Point y sin mipmaps
            _tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            _px = new Color32[TexW * TexH];
            _baseColors = MapaTactico.Pintar(_relief, _a.Graph, MinX, (MaxX - MinX) / (TexW - 1.0), MaxY, -(MaxY - MinY) / (TexH - 1.0), TexW, TexH, false);

            var go = new GameObject("Terreno (relieve de sustitución)");
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            _terrainMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));     // el sombreado va cocido en la textura (Bruno)
            _terrainMat.SetTexture("_BaseMap", _tex);
            mr.sharedMaterial = _terrainMat;
            UpdateTexture();
        }

        void UpdateTexture()
        {
            var f = _a.Field;
            var atac = TrenesAtacados();
            for (int y = 0; y < TexH; y++)
                for (int x = 0; x < TexW; x++)
                {
                    float mx = MinX + (MaxX - MinX) * x / (TexW - 1f);
                    float my = MaxY - (MaxY - MinY) * y / (TexH - 1f);
                    // densidad interpolada para que la mancha tenga bordes suaves a resolución completa
                    float u = mx - 0.5f, v = my - 0.5f;
                    int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
                    float fx = u - x0, fy = v - y0;
                    float d = Mathf.Lerp(Mathf.Lerp(f.At(x0, y0), f.At(x0 + 1, y0), fx), Mathf.Lerp(f.At(x0, y0 + 1), f.At(x0 + 1, y0 + 1), fx), fy) / 1000f;
                    var c = _baseColors[y * TexW + x];
                    _px[y * TexW + x] = MapaTactico.Mancha(c, d, x, y, false, d > 0.08f && MapaTactico.EnAtaque(_a.Graph, mx, my, atac));
                }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        /// <summary>Trenes emboscados o en encuentro, en unidades de mapa (para el rojo de la mancha solo donde ataca; Bruno, D-N113).</summary>
        // sfx_ui_aviso_ambar (Nuria): en 2D a 0,6, al aparecer el aviso de una estación, y como mucho una vez cada 10 s por estación
        readonly Dictionary<int, float> _avisoSonado = new Dictionary<int, float>(); readonly HashSet<int> _avisoVisto = new HashSet<int>();
        void SonarAvisos(List<NetWorld.ZoneWarning> l)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            var ahora = new HashSet<int>();
            foreach (var zw in l)
            {
                ahora.Add(zw.Node);
                if (_avisoVisto.Contains(zw.Node)) continue;
                if (_avisoSonado.TryGetValue(zw.Node, out var t0) && Time.unscaledTime - t0 < 10f) continue;
                _avisoSonado[zw.Node] = Time.unscaledTime; Sfx.Play("ui_aviso_ambar", Vector3.zero, 0.6f, 0.05f); Sfx.Duck(1.5f);
            }
            _avisoVisto.Clear(); foreach (var n in ahora) _avisoVisto.Add(n);
        }

        // sonidos de la Colmena y del informe (Nuria, elegidos por Lucía): una variante fija, en 2D a 1,0
        int _intelVisto = -1; bool _avisoColAntes, _informeAntes;
        void SonidosColmena()
        {
            if (_a == null) return;
            if (_intelVisto < 0) _intelVisto = _a.Intel.Count;
            for (int i = _intelVisto; i < _a.Intel.Count; i++)
            {
                switch (_a.Intel[i].Id)
                {
                    case "CO-1": Sfx.Play("sfx_colmena_acecho_1", 1f); Sfx.Duck(3f); break;
                    case "CO-2": Sfx.Play("sfx_colmena_enjambre_2", 1f); Sfx.Duck(3f); break;
                    case "CO-3": Sfx.Play("sfx_colmena_vuelta_acecho_1", 1f); Sfx.Duck(3f); break;
                    case "CO-4": Sfx.Play("sfx_colmena_dispersa_2", 1f); Sfx.Duck(3f); break;      // el CO-0 suena en ColmenaIntro (al abrir el mapa)
                }
            }
            _intelVisto = _a.Intel.Count;
            bool aviso = _a.Director.ColWarning;
            if (aviso && !_avisoColAntes) { Sfx.Play("sfx_ui_aviso_senal6_1", 1f); Sfx.Duck(2f); }      // la señal llega a 6: «La Colmena se agita…»
            _avisoColAntes = aviso;
            if (_informeOn && !_informeAntes) Sfx.Play("sfx_ui_informe_abrir_1", 1f);
            _informeAntes = _informeOn;
        }

        List<Vector2> TrenesAtacados()
        {
            var l = new List<Vector2>(); var g = _a.Graph;
            for (int i = 0; i < _a.Services.Count; i++)
            {
                var s = _a.Services[i]; if (!(s.Ambushed || (_a.Encounter.Active && _a.Encounter.Service == i))) continue;
                var t = s.Train; var e = s.AtStop ? null : t.CurrentEdge(g);
                if (e == null) { var n = g.Nodes[s.AtStop || t.Route.Count == 0 ? s.Stops[s.StopIndex].Node : t.Route[t.Route.Count - 1]]; l.Add(new Vector2((float)n.XMeters, (float)n.YMeters)); }
                else { var fr = g.Nodes[t.Route[t.Leg]]; var to = g.Nodes[t.Route[t.Leg + 1]]; float kt = e.LengthMm > 0 ? (float)t.OffsetMm / e.LengthMm : 0f; l.Add(Vector2.Lerp(new Vector2((float)fr.XMeters, (float)fr.YMeters), new Vector2((float)to.XMeters, (float)to.YMeters), kt)); }
            }
            return l;
        }

        // ---------------------------------------------------------------- red, nodos y trenes
        void BuildNetwork()
        {
            _lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var g = _a.Graph;
            foreach (var e in g.Edges)
            {
                var a = g.Nodes[e.A]; var b = g.Nodes[e.B];
                var lr = new GameObject("Vía " + a.Name + "–" + b.Name).AddComponent<LineRenderer>();
                int n = Mathf.Max(8, Mathf.CeilToInt(Vector2.Distance(new Vector2(a.XMeters, a.YMeters), new Vector2(b.XMeters, b.YMeters)) * 6));
                lr.positionCount = n + 1;
                for (int i = 0; i <= n; i++)
                {
                    double t = i / (double)n;
                    lr.SetPosition(i, MapToWorld(MapGeo.Lerp(a.XMeters, b.XMeters, t), MapGeo.Lerp(a.YMeters, b.YMeters, t), 0.02f));
                }
                lr.widthMultiplier = 0.6f;
                lr.useWorldSpace = false;                       // se elevan moviendo el objeto según la distancia de la cámara
                lr.sharedMaterial = new Material(_lineMat);          // un material por vía: el Unlit de URP no usa el color de vértice
                lr.numCapVertices = 2;
                _rails.Add(lr);
            }
            foreach (var n in g.Nodes)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = n.Name;
                Destroy(go.GetComponent<Collider>());
                go.transform.position = MapToWorld(n.XMeters, n.YMeters, 0.3f);
                go.transform.localScale = new Vector3(1.6f, 0.3f, 1.6f);
                go.GetComponent<Renderer>().sharedMaterial = new Material(_lineMat);
                _nodes.Add(go.transform);
            }
            foreach (var s in _a.Services)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Tren " + s.Train.Name;
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = new Vector3(0.9f, 0.5f, 2.4f);
                var m = new Material(_lineMat);
                m.SetColor("_BaseColor", new Color(1f, 0.93f, 0.6f));
                go.GetComponent<Renderer>().sharedMaterial = m;
                _trains.Add(go.transform);
            }
        }

        static Color StateColor(NodeState s)
        {
            switch (s)
            {
                case NodeState.Segura: return new Color(0.38f, 0.75f, 0.38f);
                case NodeState.Amenazada: return new Color(0.9f, 0.78f, 0.25f);
                case NodeState.Asediada: return new Color(0.92f, 0.45f, 0.16f);
                default: return new Color(0.45f, 0.08f, 0.1f);
            }
        }

        void UpdateNetwork()
        {
            var g = _a.Graph;
            // tamaño constante en pantalla: los marcadores y las vías se escalan con la distancia de la cámara
            float k = Mathf.Clamp(_dist / 230f, 0.0015f, 2f);
            foreach (var r in _rails) { r.widthMultiplier = 0.6f * k; r.transform.position = new Vector3(0f, 0.05f * k + 0.01f, 0f); }
            for (int i = 0; i < _nodes.Count; i++) _nodes[i].localScale = new Vector3(1.6f, 0.3f, 1.6f) * k;
            foreach (var t in _trains) t.localScale = new Vector3(0.9f, 0.5f, 2.4f) * Mathf.Max(k, 0.004f);
            for (int i = 0; i < g.Edges.Count; i++)
            {
                var e = g.Edges[i];
                Color c = e.State == EdgeState.Cortada ? new Color(0.85f, 0.15f, 0.12f) : e.State == EdgeState.Danada ? new Color(0.9f, 0.55f, 0.15f) : new Color(0.93f, 0.9f, 0.82f);
                _rails[i].sharedMaterial.SetColor("_BaseColor", c);
            }
            for (int i = 0; i < g.Nodes.Count; i++) _nodes[i].GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", StateColor(g.Nodes[i].State));
            for (int i = 0; i < _a.Services.Count; i++)
            {
                var s = _a.Services[i];
                var t = s.Train;
                var e = s.AtStop ? null : t.CurrentEdge(g);
                Vector3 p, dir = Vector3.forward;
                if (e == null)
                {
                    var n = g.Nodes[s.AtStop || t.Route.Count == 0 ? s.Stops[s.StopIndex].Node : t.Route[t.Route.Count - 1]];
                    float kk = Mathf.Clamp(_dist / 230f, 0.004f, 2f);
                    p = MapToWorld(n.XMeters, n.YMeters, 0.6f * kk) + (_dist > 20f ? new Vector3(1.2f * (i - 1) * kk, 0, 0) : Vector3.zero);
                }
                else
                {
                    var from = g.Nodes[t.Route[t.Leg]];
                    var to = g.Nodes[t.Route[t.Leg + 1]];
                    double kt = e.LengthMm > 0 ? (double)t.OffsetMm / e.LengthMm : 0;
                    double mx = MapGeo.Lerp(from.XMeters, to.XMeters, kt), my = MapGeo.Lerp(from.YMeters, to.YMeters, kt);
                    p = MapToWorld(mx, my, 0.35f * Mathf.Clamp(_dist / 230f, 0.004f, 2f));
                    dir = MapToWorldFlat(to.XMeters, to.YMeters) - MapToWorldFlat(from.XMeters, from.YMeters);
                }
                _trains[i].position = p;
                if (dir.sqrMagnitude > 1e-4f) _trains[i].rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
            }
        }

        // ---------------------------------------------------------------- bucle
        void Push(NetCmdType type, int arg, long delay) { NetSession.Push(type, arg, delay); }

        float _tvT0 = -1f; int _tvStep;
        /// <summary>-trainview (QA): entra y sale de la vista cercana tres veces y hace capturas del tren y del mapa al volver.</summary>
        void TrainViewDemo()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-trainview") < 0) return;
            if (_tvT0 < 0f) _tvT0 = Time.realtimeSinceStartup; float tt = Time.realtimeSinceStartup - _tvT0;
            float[] at = { 4f, 8f, 10f, 14f, 16f, 20f, 23f }; // abre, captura, cierra, abre, cierra, abre, captura+cierra
            if (_tvStep == 0 && tt > 4f) { TrainCloseView.Toggle(0); _tvStep = 1; Debug.Log("[VISTA] entra 1"); }
            else if (_tvStep == 1 && tt > 8f) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "..", "Captures", "tren_vista_1.png")); _tvStep = 2; }
            else if (_tvStep == 2 && tt > 9f) { TrainCloseView.Toggle(0); _tvStep = 3; Debug.Log("[VISTA] sale 1"); }
            else if (_tvStep == 3 && tt > 11f) { TrainCloseView.Toggle(0); _tvStep = 4; Debug.Log("[VISTA] entra 2"); }
            else if (_tvStep == 4 && tt > 14f) { TrainCloseView.Toggle(0); _tvStep = 5; Debug.Log("[VISTA] sale 2"); }
            else if (_tvStep == 5 && tt > 16f) { TrainCloseView.Toggle(0); _tvStep = 6; Debug.Log("[VISTA] entra 3"); }
            else if (_tvStep == 6 && tt > 20f) { TrainCloseView.Toggle(0); _tvStep = 7; Debug.Log("[VISTA] sale 3"); }
            else if (_tvStep == 7 && tt > 23f) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "..", "Captures", "mapa_tras_vista.png")); _tvStep = 8; Debug.Log("[VISTA] mapa tras salir 3 veces"); }
        }

        void Update()
        {
            SonidosColmena();
            Update_NightReturn();
            if (_a == null) return;                      // entrada directa a la zona (noche o prueba): el mapa no se ha construido
            HandleInput();
            { var kbv = UnityEngine.InputSystem.Keyboard.current; if (kbv != null && kbv.vKey.wasPressedThisFrame && !TrainCloseView.Active)
                { int idx = 0; for (int si = 0; si < _a.Services.Count; si++) if (_a.Services[si].Train.State == RailTrainState.EnMarcha) { idx = si; break; } TrainCloseView.Toggle(idx); } }
            TrainViewDemo();
            if (TrainCloseView.Active) return;           // mientras dura la vista cercana, el mapa no atiende la entrada
            long fieldTick = _a.Tick / NetConst.FieldEvery;
            if (fieldTick != _lastFieldTick) { _lastFieldTick = fieldTick; UpdateTexture(); UpdateDetailTexture(); }
            UpdateNetwork();
            UpdateMasasEnMarcha(); UpdateCerco(); AvisosEnLog();                       // D-N182: la masa del asedio anunciado llega andando por su rumbo (solo vista)
            UpdateCamera();
            UpdateDetail();
            CheckZoneEntry();
        }

        // ---------------------------------------------------------------- entrar en una zona (hito 3, D-N06)
        float _fadeStart;
        bool _entering;
        bool _armed;                              // histéresis: hay que haber estado por encima de 2,5 km antes de volver a entrar

        public const float EnterKm = 2f, ExitKm = 2.5f;

        void CheckZoneEntry()
        {
            if (_dist > ExitKm) _armed = true;
            if (_entering || !_armed || _dist >= EnterKm || _a.ActiveZoneNode >= 0) return;
            // nodo más cercano al foco, a menos de 1,5 km
            int best = -1;
            float bd = 1.5f;
            for (int i = 0; i < _a.Graph.Nodes.Count; i++)
            {
                var n = _a.Graph.Nodes[i];
                float d = Vector3.Distance(new Vector3(_focus.x, 0, _focus.z), MapToWorldFlat(n.XMeters, n.YMeters));
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0) return;
            EnterZone(best);
        }

        /// <summary>Entra en la zona de la emboscada del encuentro activo (el tren está parado con la horda delante).</summary>
        public void EnterAmbush()
        {
            if (_entering || !_a.Encounter.Active) return;
            _entering = true;
            NetSession.PendingAmbush = true;
            StartCoroutine(EnterRoutine());
        }

        public void EnterZone(int node)
        {
            if (_entering) return;
            _entering = true;
            NetSession.PendingZoneNode = node;
            NetSession.Push(NetCmdType.Materialize, node, 0);
            StartCoroutine(EnterRoutine());
        }

        System.Collections.IEnumerator EnterRoutine()
        {
            yield return NetSession.CaptureTransition();
            SceneManager.LoadScene("SampleScene");
        }

        // ---------------------------------------------------------------- detalle de cerca
        void UpdateDetail()
        {
            bool want = _dist < 14f;
            if (!want)
            {
                if (_detail != null && _detail.activeSelf) { _detail.SetActive(false); RestoreCoarse(); }
                return;
            }
            var c = new Vector3(_focus.x, 0, _focus.z);
            // el parche se calcula en segundo plano (pintar 512² téxeles tarda ~0,6–1 s): mientras, se queda el anterior (o el terreno grueso)
            if (_detailJob != null)
            {
                if (!_detailJob.IsCompleted) return;
                var job = _detailJob; _detailJob = null;
                if (job.Status == System.Threading.Tasks.TaskStatus.RanToCompletion) AplicarDetalle(job.Result);
                else Debug.LogWarning("[MAPA] el parche de detalle falló: " + job.Exception?.GetBaseException().Message);
                return;
            }
            if (_detail != null && _detail.activeSelf && (c - _detailCenter).magnitude < DetailSizeKm * 0.25f) return;
            _detailJob = System.Threading.Tasks.Task.Run(() => CalcularDetalle(c));
        }

        System.Threading.Tasks.Task<Detalle> _detailJob;
        sealed class Detalle { public Vector3 Centro; public Vector3[] Verts; public Vector2[] Uvs; public Color32[] Cols; }

        /// <summary>La mancha de horda también de cerca (Lucía: al volver de una zona a 2,5 km tiene que verse alrededor de la estación).</summary>
        void UpdateDetailTexture()
        {
            if (_detail == null || !_detail.activeSelf || _detailBase == null) return;
            const int TW = DetailTW;
            var atac = TrenesAtacados();
            float half = DetailSizeKm * 0.5f;
            var f = _a.Field;
            for (int y = 0; y < TW; y++)
                for (int x = 0; x < TW; x++)
                {
                    float wx = _detailCenter.x - half + DetailSizeKm * x / (TW - 1f);
                    float wz = _detailCenter.z - half + DetailSizeKm * y / (TW - 1f);
                    float mx = MinX + wx / (float)MapGeo.KmPerUnit, my = MaxY - wz / (float)MapGeo.KmPerUnit;
                    float u = mx - 0.5f, v = my - 0.5f;
                    int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
                    float fx = u - x0, fy = v - y0;
                    float d = Mathf.Lerp(Mathf.Lerp(f.At(x0, y0), f.At(x0 + 1, y0), fx), Mathf.Lerp(f.At(x0, y0 + 1), f.At(x0 + 1, y0 + 1), fx), fy) / 1000f;
                    var c = _detailBase[y * TW + x];
                    // de cerca, la mancha se ve como un moteado (puntos de horda), no como una tinta lisa
                    uint hsh = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                    bool dot = (hsh % 1000) < d * 1000f;
                    _detailPx[y * TW + x] = MapaTactico.Mancha(c, d, x, y, dot && d > 0.08f, d > 0.08f && MapaTactico.EnAtaque(_a.Graph, mx, my, atac));
                }
            _detailTex.SetPixels32(_detailPx);
            _detailTex.Apply(false);
        }

        /// <summary>Altura que tiene el terreno grueso (interpolado en su triángulo) en un punto del mundo.</summary>
        float CoarseHeightAt(float wx, float wz)
        {
            float fi = wx / ((MaxX - MinX) * (float)MapGeo.KmPerUnit) * (GridX - 1);
            float fj = (1f - wz / ((MaxY - MinY) * (float)MapGeo.KmPerUnit)) * (GridY - 1);
            int i0 = Mathf.Clamp(Mathf.FloorToInt(fi), 0, GridX - 2), j0 = Mathf.Clamp(Mathf.FloorToInt(fj), 0, GridY - 2);
            float u = Mathf.Clamp01(fi - i0), v = Mathf.Clamp01(fj - j0);
            float a = _coarseVerts[j0 * GridX + i0].y, b = _coarseVerts[j0 * GridX + i0 + 1].y;
            float c = _coarseVerts[(j0 + 1) * GridX + i0].y, d = _coarseVerts[(j0 + 1) * GridX + i0 + 1].y;
            // misma diagonal que los triángulos del terreno grueso (v, v+1, v+GridX) y (v+1, v+GridX+1, v+GridX)
            return u + v <= 1f ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1f - u) + (b - d) * (1f - v);
        }

        void RestoreCoarse()
        {
            System.Array.Copy(_coarseVerts, _coarseWork, _coarseVerts.Length);
            _mesh.vertices = _coarseWork;
            _detailCenter = new Vector3(-9999, 0, -9999);
        }

        /// <summary>Téxeles del parche de detalle (Bruno: 512 = 23 m por téxel; si se nota el tirón al rehacerlo, 384). UltimoParcheMs mide lo que tarda.</summary>
        const int DetailTW = 512;
        public static double UltimoParcheMs;

        /// <summary>Síncrono (capturas por lotes): calcula y aplica en el acto.</summary>
        void BuildDetail(Vector3 center) { AplicarDetalle(CalcularDetalle(center)); }

        /// <summary>Solo datos, sin API de Unity: se puede llamar fuera del hilo principal (relieve, terreno grueso y grafo son de solo lectura).</summary>
        Detalle CalcularDetalle(Vector3 center)
        {
            float half = DetailSizeKm * 0.5f;
            var verts = new Vector3[DetailN * DetailN];
            var uvs = new Vector2[DetailN * DetailN];
            const int TW = DetailTW;
            Color32[] cols;
            for (int j = 0; j < DetailN; j++)
                for (int i = 0; i < DetailN; i++)
                {
                    float wx = center.x - half + DetailSizeKm * i / (DetailN - 1f);
                    float wz = center.z - half + DetailSizeKm * j / (DetailN - 1f);
                    double mx = MinX + wx / MapGeo.KmPerUnit, my = MaxY - wz / MapGeo.KmPerUnit;
                    double h = _relief.HeightM(mx, my);
                    float fine = (float)(h < 0 ? h / 1000.0 * 2.0 : h / 1000.0 * Exag);
                    // en el 20 % exterior del parche se funde con la altura del terreno grueso: sin escalón en el borde
                    float edge = Mathf.Min(Mathf.Min(i, DetailN - 1 - i), Mathf.Min(j, DetailN - 1 - j)) / (float)(DetailN - 1);
                    float w = Mathf.SmoothStep(0f, 1f, edge / 0.2f);
                    verts[j * DetailN + i] = new Vector3(wx, Mathf.Lerp(CoarseHeightAt(wx, wz), fine, w) + 0.004f, wz);
                    uvs[j * DetailN + i] = new Vector2(i / (DetailN - 1f), j / (DetailN - 1f));
                }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            double dmx = DetailSizeKm / MapGeo.KmPerUnit / (TW - 1);
            cols = MapaTactico.Pintar(_relief, _a.Graph, MinX + (center.x - half) / MapGeo.KmPerUnit, dmx, MaxY - (center.z - half) / MapGeo.KmPerUnit, -dmx, TW, TW, true);
            UltimoParcheMs = sw.Elapsed.TotalMilliseconds;
            return new Detalle { Centro = center, Verts = verts, Uvs = uvs, Cols = cols };
        }

        void AplicarDetalle(Detalle dt)
        {
            const int TW = DetailTW;
            var center = dt.Centro; var verts = dt.Verts; var uvs = dt.Uvs; var cols = dt.Cols;
            _detailCenter = center;
            float half = DetailSizeKm * 0.5f;
            if (_detail == null)
            {
                var tris = new int[(DetailN - 1) * (DetailN - 1) * 6];
                int k = 0;
                for (int j = 0; j < DetailN - 1; j++)
                    for (int i = 0; i < DetailN - 1; i++)
                    {
                        int v = j * DetailN + i;
                        tris[k++] = v; tris[k++] = v + DetailN; tris[k++] = v + 1;
                        tris[k++] = v + 1; tris[k++] = v + DetailN; tris[k++] = v + DetailN + 1;
                    }
                _detailMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                _detailMesh.vertices = verts; _detailMesh.uv = uvs; _detailMesh.triangles = tris;
                _detail = new GameObject("Terreno de detalle");
                _detail.AddComponent<MeshFilter>().sharedMesh = _detailMesh;
                _detailTex = new Texture2D(TW, TW, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
                var m = new Material(_terrainMat);
                m.SetTexture("_BaseMap", _detailTex);
                _detail.AddComponent<MeshRenderer>().sharedMaterial = m;
            }
            else
            {
                _detailMesh.vertices = verts;
                _detailMesh.uv = uvs;
            }
            _detailMesh.RecalculateNormals();
            _detailMesh.RecalculateBounds();
            _detailBase = cols;
            _detailPx = new Color32[cols.Length];
            _detail.SetActive(true);
            UpdateDetailTexture();

            // el terreno grueso se hunde bajo el parche de detalle (con un margen) para que no asome por encima
            System.Array.Copy(_coarseVerts, _coarseWork, _coarseVerts.Length);
            float inner = half - 2.5f;
            for (int v = 0; v < _coarseWork.Length; v++)
            {
                var p = _coarseWork[v];
                if (Mathf.Abs(p.x - center.x) < inner && Mathf.Abs(p.z - center.z) < inner) _coarseWork[v].y -= 0.5f;
            }
            _mesh.vertices = _coarseWork;
        }

        void HandleInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;
            HandleInputInforme(kb);                                  // D-N160: pantalla del informe V1 (Tab abre y cierra, Esc cierra)
            HandleInputApp6(kb);                                     // D-N160: capa APP-6 del informe (tecla I)
            if (kb.spaceKey.wasPressedThisFrame) Push(_a.Paused ? NetCmdType.Resume : NetCmdType.Pause, 0, 0);
            if (kb.digit1Key.wasPressedThisFrame) Push(NetCmdType.SetSpeed, 1, 0);
            if (kb.digit2Key.wasPressedThisFrame) Push(NetCmdType.SetSpeed, 2, 0);
            if (kb.digit3Key.wasPressedThisFrame) Push(NetCmdType.SetSpeed, 4, 0);
            if (kb.jKey.wasPressedThisFrame) JumpToAlert();
            if (kb.enterKey.wasPressedThisFrame && _a.Encounter.Active && !_entering) EnterAmbush();     // D-N38: entrar en la emboscada
            if (kb.f3Key.wasPressedThisFrame) { NetSession.Check = false; NetSession.Desync = false; }
            if (kb.f8Key.wasPressedThisFrame) NetSession.DebugForceAmbush();      // solo para probar: monta una emboscada de prueba (luego Intro entra)
            if (kb.f9Key.wasPressedThisFrame) Capture();

            float dt = Time.unscaledDeltaTime;
            float pan = _dist * 0.9f * dt;
            Vector3 fwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward, right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) _focus += fwd * pan;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) _focus -= fwd * pan;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) _focus += right * pan;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) _focus -= right * pan;
            if (kb.qKey.isPressed) _yaw += 70f * dt;
            if (kb.eKey.isPressed) _yaw -= 70f * dt;
            if (kb.rKey.isPressed) _pitch = Mathf.Clamp(_pitch + 40f * dt, 15f, 89f);
            if (kb.fKey.isPressed) _pitch = Mathf.Clamp(_pitch - 40f * dt, 15f, 89f);
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) _targetDist = Mathf.Clamp(_targetDist * Mathf.Pow(0.85f, Mathf.Sign(wheel)), 0.3f, 600f);
                if (mouse.rightButton.isPressed)
                {
                    var d = mouse.delta.ReadValue();
                    _yaw += d.x * 0.25f;
                    _pitch = Mathf.Clamp(_pitch - d.y * 0.2f, 15f, 89f);
                }
            }
        }

        /// <summary>Una tecla te lleva a la alerta más urgente (GDD §7).</summary>
        public void JumpToAlert()
        {
            if (_a.Alerts.Count == 0) return;
            var al = _a.Alerts[0];
            var g = _a.Graph;
            Vector3 target;
            if (al.Kind == AlertKind.NodoAsediado) target = MapToWorldFlat(g.Nodes[al.Subject].XMeters, g.Nodes[al.Subject].YMeters);
            else if (al.Kind == AlertKind.ViaCortada) { var e = g.Edges[al.Subject]; target = (MapToWorldFlat(g.Nodes[e.A].XMeters, g.Nodes[e.A].YMeters) + MapToWorldFlat(g.Nodes[e.B].XMeters, g.Nodes[e.B].YMeters)) * 0.5f; }
            else
            {
                int idx = _a.Services.FindIndex(s => s.Train.Id == al.Subject);
                target = idx >= 0 ? _trains[idx].position : _focus;
            }
            _focus = new Vector3(target.x, 0, target.z);
            _targetDist = Mathf.Min(_targetDist, 40f);
        }

        void UpdateCamera()
        {
            _dist = Mathf.Lerp(_dist, _targetDist, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            _focus.x = Mathf.Clamp(_focus.x, 0, (MaxX - MinX) * (float)MapGeo.KmPerUnit);
            _focus.z = Mathf.Clamp(_focus.z, 0, (MaxY - MinY) * (float)MapGeo.KmPerUnit);
            double fmx = MinX + _focus.x / MapGeo.KmPerUnit, fmy = MaxY - _focus.z / MapGeo.KmPerUnit;
            float ground = (float)(System.Math.Max(0, _relief.HeightM(fmx, fmy)) / 1000.0 * Exag);
            var target = new Vector3(_focus.x, ground, _focus.z);
            var rot = Quaternion.Euler(_pitch, _yaw, 0);
            _cam.transform.position = target - rot * Vector3.forward * _dist;
            _cam.transform.rotation = rot;
            _cam.nearClipPlane = Mathf.Max(0.01f, _dist * 0.01f);
            _cam.farClipPlane = Mathf.Max(50f, _dist * 12f);
        }

        // ---------------------------------------------------------------- interfaz
        void EnsureStyles()
        {
            if (_label != null) return;
            _panelTex = new Texture2D(1, 1);
            _panelTex.SetPixel(0, 0, new Color(0.04f, 0.045f, 0.06f, 0.78f));
            _panelTex.Apply();
            _panel = new GUIStyle { normal = { background = _panelTex } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Screen.height / 52), richText = true };
            _label.normal.textColor = new Color(0.94f, 0.92f, 0.86f);
            _small = new GUIStyle(_label) { fontSize = Mathf.Max(10, Screen.height / 64) };
            _title = new GUIStyle(_label) { fontSize = Mathf.Max(14, Screen.height / 42), fontStyle = FontStyle.Bold };
            foreach (var st in new[] { _label, _small, _title }) { st.clipping = TextClipping.Overflow; st.wordWrap = false; st.padding = new RectOffset(2, 2, 2, 6); st.overflow = new RectOffset(0, 0, 0, 8); }   // la altura del rectángulo no recorta el texto (nombres cortados por abajo)
        }

        long _emSonado = -1; long _trenParadoAvisado = -1;
        /// <summary>Sonido y línea de log de los avisos del mapa en Update (OnGUI no corre con -nographics; Sara los comprueba en lote).</summary>
        void AvisosEnLog()
        {
            var em = _a.LastAmbush;
            if (em.Tick >= 0 && em.Node >= 0 && em.Node < _a.Graph.Nodes.Count && _emSonado != em.Tick)
            {
                _emSonado = em.Tick; string tren = em.Service >= 0 && em.Service < _a.Services.Count ? _a.Services[em.Service].Train.Name : "tren";
                Sfx.Play("aviso_horda", Vector3.zero, 0.7f, 0.05f); Sfx.Duck(1.5f);   // Nuria (mvp-em1-caida-repetir.md)
                Debug.Log($"[MAPA] EM-1 Emboscada en {_a.Graph.Nodes[em.Node].Name}: unos {em.Zombies} zombis atacan el {tren} fuera de la puerta este.");
            }
            long hd = _a.Tick % NetConst.DayTicks; if (hd < NetConst.Hours(19) || hd >= NetConst.Hours(22)) return;
            foreach (var sv in _a.Services)
            {
                if (!sv.AtStop || sv.Stops.Count == 0) continue; int nn = sv.GateNode >= 0 ? sv.GateNode : sv.Stops[sv.StopIndex].Node;
                if (nn < 0 || nn == _a.Embarque.HomeNode || _a.NodeLost[nn]) continue;
                long k = _a.Tick / NetConst.DayTicks * 1000 + nn; if (_trenParadoAvisado == k) continue; _trenParadoAvisado = k;
                Debug.Log($"[MAPA] Hay un tren parado en {_a.Graph.Nodes[nn].Name}. Sácalo antes de la noche o puede perderse.");
            }
        }
        void OnGUI()
        {
            if (_a == null) return;
            EnsureStyles();
            var g = _a.Graph;
            foreach (var n in g.Nodes)
            {
                var sp = _cam.WorldToScreenPoint(MapToWorld(n.XMeters, n.YMeters, 0.8f * Mathf.Clamp(_dist / 230f, 0.0015f, 2f)));
                if (sp.z <= 0) continue;
                int px = IconPx(32);
                DrawIcon(new Rect(sp.x - px * 0.5f, Screen.height - sp.y - px * 1.1f, px, px), "estaciones/vm_estacion_" + StateName(n.State) + "_32");
                GUI.Label(new Rect(sp.x + px * 0.5f + 4, Screen.height - sp.y - 10, 220, _small.fontSize * 2.2f), n.Name, _small);
                if (n.Id == _a.Director.CerradaNodo)       // D-N184: al pasar el ratón por Castellón
                {
                    var mp = Event.current.mousePosition; if ((mp - new Vector2(sp.x, Screen.height - sp.y - px * 0.6f)).magnitude < px * 1.2f)
                    { var tr = new Rect(sp.x + px * 0.5f + 4, Screen.height - sp.y + _small.fontSize * 1.2f, 360, _small.fontSize * 2.2f); GUI.DrawTexture(tr, Texture2D.blackTexture); GUI.Label(tr, " Castellón · cercada. Aguanta, pero está aislada.", _small); }
                }
            }
            DrawApp6();                                              // D-N160: símbolos APP-6 de cada estación (OperationsMap3D.InformeApp6.cs)
            for (int i = 0; i < _trains.Count; i++)
            {
                var sp = _cam.WorldToScreenPoint(_trains[i].position + Vector3.up * 0.8f * Mathf.Clamp(_dist / 230f, 0.0015f, 2f));
                if (sp.z <= 0) continue;
                int tp = IconPx(32);
                var sv = _a.Services[i];
                string tn = _a.Encounter.Active && _a.Encounter.Service == i ? "bajo_ataque" : sv.Ambushed ? "bajo_ataque" : sv.Train.State == RailTrainState.EnMarcha ? "en_marcha" : sv.Train.State == RailTrainState.Detenido ? "detenido" : "detenido";
                DrawIcon(new Rect(sp.x - tp * 0.5f, Screen.height - sp.y - tp * 1.6f, tp, tp), "trenes/vm_tren_" + tn + "_32");
                GUI.Label(new Rect(sp.x - 30, Screen.height - sp.y - 26 - _small.fontSize * 0.8f, 160, _small.fontSize * 2.2f), "<b><color=#FFF0A0>" + _a.Services[i].Train.Name + "</color></b>", _small);
                string se = tn == "bajo_ataque" ? null : Puertas.SinEscolta(_a, i);       // D-N177: ámbar en el icono; el rojo es solo el de bajo ataque
                if (se != null) GUI.Label(new Rect(sp.x - 30, Screen.height - sp.y - 26 + _small.fontSize * 1.2f, 260, _small.fontSize * 2.2f), "<color=#E08C30>" + se + "</color>", _small);
            }

            if (_informeOn)                                          // D-N160: pantalla del informe V1 (OperationsMap3D.InformeV1.cs); tapa el panel de avisos, la carga y el informe pequeño
            {
                SonarAvisos(_a.UndefendedWarnings());                // los avisos siguen sonando con el informe abierto
                DrawInformeV1();
                NetSession.DrawFade(Time.realtimeSinceStartup - _fadeStart);
                return;
            }
            float pw = Mathf.Min(460, Screen.width * 0.38f);
            float h = _small.fontSize * 14f;
            GUI.Box(new Rect(8, 8, pw, h), GUIContent.none, _panel);
            float y = 14;
            string speed = _a.Paused ? "<color=#E8C040>PAUSA</color>" : "x" + _a.Speed;
            GUI.Label(new Rect(16, y, pw, 40), $"Día {_a.Tick / NetConst.DayTicks + 1} · {NetConst.Clock(_a.Tick)} · {speed}", _title); y += _title.fontSize * 1.6f;
            string sync = NetSession.Check ? (NetSession.Desync ? "<color=#E04030>DESINCRONIZACIÓN</color>" : "<color=#70C070>Desincronización: correcta</color>") : "<color=#999999>prueba apagada</color>";
            GUI.Label(new Rect(16, y, pw, 30), sync + "  ·  <color=#B0A890>relieve de sustitución (no son datos reales)</color>", _small); y += _small.fontSize * 1.7f;
            string scaleTxt = _dist >= 10 ? $"{_dist:0} km" : $"{_dist * 1000:0} m";
            GUI.Label(new Rect(16, y, pw, 30), $"Distancia de cámara: {scaleTxt}", _small); y += _small.fontSize * 1.7f;
            int shown = 0;
            var zws = _a.UndefendedWarnings();
            SonarAvisos(zws);
            if (Puertas.EnApartadoConAsedio(_a).Count > 0)      // D-N177: el tren en el apartado de Nules con asedio anunciado
            {
                int zp = IconPx(16); shown++;
                DrawIcon(new Rect(20, y - 2, zp, zp), "alertas/vm_alertas_" + AlertIcon(AlertKind.NodoAsediado) + "_16");
                GUI.Label(new Rect(20 + zp + 6, y, pw - 20 - zp, 30), "<color=#E08C30>" + Puertas.TextoApartado + "</color>", _small); y += Mathf.Max(_small.fontSize * 1.5f, zp + 4);
            }
            {   // Lucía (3b2ee1d): de 19:00 a 22:00, aviso en e08c30 por cada tren parado fuera de casa; doble clic va a esa zona
                long hd = _a.Tick % NetConst.DayTicks;
                if (hd >= NetConst.Hours(19) && hd < NetConst.Hours(22))
                    foreach (var sv in _a.Services)
                    {
                        if (!sv.AtStop || sv.Stops.Count == 0) continue; int nn = sv.GateNode >= 0 ? sv.GateNode : sv.Stops[sv.StopIndex].Node;
                        if (nn < 0 || nn == _a.Embarque.HomeNode || _a.NodeLost[nn]) continue;
                        int zp = IconPx(16); shown++;
                        string txt = $"Hay un tren parado en {_a.Graph.Nodes[nn].Name}. Sácalo antes de la noche o puede perderse.";
                        var rr = new Rect(20, y - 2, pw - 20, Mathf.Max(_small.fontSize * 1.5f, zp + 4));
                        DrawIcon(new Rect(20, y - 2, zp, zp), "alertas/vm_alertas_" + AlertIcon(AlertKind.NodoAsediado) + "_16");
                        GUI.Label(new Rect(20 + zp + 6, y, pw - 20 - zp, 30), "<color=#E08C30>" + txt + "</color>", _small); y += rr.height;
                        var ev = Event.current; if (ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2 && rr.Contains(ev.mousePosition)) { ev.Use(); EnterZone(nn); }
                    }
            }
            {   // EM-1 (Lucía, a8b49fd): emboscada de la red en 982420; doble clic lleva a la zona del nodo. Visible 2 h de juego
                var em = _a.LastAmbush;
                if (em.Tick >= 0 && em.Node >= 0 && em.Node < _a.Graph.Nodes.Count && _a.Tick - em.Tick < NetConst.Hours(2))
                {
                    int zp = IconPx(16); shown++;
                    string tren = em.Service >= 0 && em.Service < _a.Services.Count ? _a.Services[em.Service].Train.Name : "tren";
                    string txt = $"Emboscada en {_a.Graph.Nodes[em.Node].Name}: unos {em.Zombies} zombis atacan el {tren} fuera de la puerta este.";
                    var rr = new Rect(20, y - 2, pw - 20, Mathf.Max(_small.fontSize * 1.5f, zp + 4));
                    DrawIcon(new Rect(20, y - 2, zp, zp), "alertas/vm_alertas_" + AlertIcon(AlertKind.NodoAsediado) + "_16");
                    GUI.Label(new Rect(20 + zp + 6, y, pw - 20 - zp, 30), "<color=#982420><b>" + txt + "</b></color>", _small); y += rr.height;
                    if (em.InApartado) { GUI.Label(new Rect(20 + zp + 6, y, pw - 20 - zp, 30), "<color=#E08C30>" + Puertas.TextoApartado + "</color>", _small); y += _small.fontSize * 1.5f; }
                    var ev = Event.current; if (ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2 && rr.Contains(ev.mousePosition)) { ev.Use(); EnterZone(em.Node); }
                }
            }
            y = DrawColmenaAviso(pw, y, ref shown);                  // D-N160: «La Colmena se agita» (ámbar), antes del Enjambre
            foreach (var zw in zws)       // aviso ámbar: la horda va a una zona sin tropa (Lucía, 8-nov; regla de Nacho)
            {
                if (shown++ >= 4) break;
                int zp = IconPx(16);
                DrawIcon(new Rect(20, y - 2, zp, zp), "alertas/vm_alertas_" + AlertIcon(AlertKind.NodoAsediado) + "_16");
                GUI.Label(new Rect(20 + zp + 6, y, pw - 20 - zp, 30), "<color=#E08C30>" + zw.Text + "</color>", _small); y += Mathf.Max(_small.fontSize * 1.5f, zp + 4);
            }
            foreach (var al in _a.Alerts)
            {
                if (shown++ >= 4) break;
                string col = al.Priority == 0 ? "#F05040" : al.Priority == 1 ? "#F0A040" : al.Priority == 2 ? "#E0D070" : "#A0A0A0";
                int ap = IconPx(16);
                DrawIcon(new Rect(20, y - 2, ap, ap), "alertas/vm_alertas_" + AlertIcon(al.Kind) + "_16");
                GUI.Label(new Rect(20 + ap + 6, y, pw - 20 - ap, 30), $"<color={col}>{al.Text}</color>", _small); y += Mathf.Max(_small.fontSize * 1.5f, ap + 4);
            }
            if (_a.Alerts.Count == 0) { GUI.Label(new Rect(20, y, pw, 30), "<color=#808080>sin alertas</color>", _small); }
            DrawCargo(pw);
            DrawIntel();
            GUI.Label(new Rect(16, Screen.height - _small.fontSize * 2.4f, Screen.width, 30),
                "WASD mover · Q E girar · R F inclinar · rueda zoom (a menos de 2 km de una estación se entra en la zona) · J ir a la alerta · Tab informe · I símbolos APP-6 · Espacio pausa · 1/2/3 velocidad · F8 emboscada de prueba · Intro entrar en la emboscada · F9 captura", _small);
            NetSession.DrawFade(Time.realtimeSinceStartup - _fadeStart);
        }

        // ---------------------------------------------------------------- iconos de Bruno (pixel art por código, paleta de 32): Assets/TBD/Resources/Icons
        static readonly System.Collections.Generic.Dictionary<string, Texture2D> _iconCache = new System.Collections.Generic.Dictionary<string, Texture2D>();

        static int IconPx(int baseSize) { return baseSize * Mathf.Max(1, Mathf.RoundToInt(Screen.height / 540f)); }     // múltiplos enteros: el pixel art no se emborrona

        static void DrawIcon(Rect r, string path)
        {
            Texture2D t;
            if (!_iconCache.TryGetValue(path, out t)) { t = Resources.Load<Texture2D>("Icons/" + path); _iconCache[path] = t; }
            if (t != null) GUI.DrawTexture(r, t, ScaleMode.ScaleToFit, true);
        }

        static string StateName(NodeState s) { return s == NodeState.Amenazada ? "amenazada" : s == NodeState.Asediada ? "asediada" : s == NodeState.Perdida ? "perdida" : "segura"; }

        static string AlertIcon(AlertKind k) { return k == AlertKind.ViaCortada ? "via_cortada" : k == AlertKind.TrenDetenido ? "tren_detenido" : k == AlertKind.NodoAsediado ? "asedio" : "ataque"; }

        /// <summary>Carga de cada tren con los iconos de los cinco recursos (munición, combustible, materiales, víveres, población).</summary>
        void DrawCargo(float pw)
        {
            string[] ic = { "municion", "combustible", "materiales", "poblacion" };   // los cuatro recursos de la S0 (Res): la población ocupa el último
            int p = IconPx(16);
            float h = (p + 6) * (_a.Services.Count + 0.8f);
            float x0 = 8, y0 = Screen.height * 0.12f + 6 * _small.fontSize;
            GUI.Box(new Rect(x0, y0, pw, h), GUIContent.none, _panel);
            float y = y0 + 4;
            for (int i = 0; i < _a.Services.Count; i++)
            {
                var sv = _a.Services[i];
                GUI.Label(new Rect(x0 + 8, y, 130, p + 4), "<b>" + sv.Train.Name + "</b>", _small);
                float x = x0 + 136;
                for (int r = 0; r < NetConst.ResCount && r < ic.Length; r++)
                {
                    DrawIcon(new Rect(x, y, p, p), "cargas/vm_cargas_" + ic[r] + "_16");
                    GUI.Label(new Rect(x + p + 2, y, 60, p + 4), sv.Cargo[r].ToString(), _small);
                    x += p + 2 + _small.fontSize * 2.6f;
                }
                y += p + 6;
            }
        }

        // ---------------------------------------------------------------- informe de inteligencia (hito 4)
        IntelTexts _texts;

        void DrawIntel()
        {
            if (_texts == null)
            {
                var tx = BalanceSource.Read("textos_informe.csv");
                if (tx == null) return;
                try { _texts = new IntelTexts(tx); } catch { return; }
            }
            int n = Mathf.Min(4, _a.Intel.Count);
            float w = Mathf.Min(560, Screen.width * 0.45f);
            float h = _small.fontSize * (2.2f + n * 1.6f + (_app6On ? 1.6f : 0f));
            float x = Screen.width - w - 8, y = 8;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _panel);
            if (GUI.Button(new Rect(x, y, w, h), GUIContent.none, GUIStyle.none)) AbrirInforme(true);     // un clic abre el informe completo (V1)
            GUI.Label(new Rect(x + 10, y + 4, w, 30), "<b>INFORME DE INTELIGENCIA</b> <color=#808080>(Tab)</color>" + (n == 0 ? "  <color=#808080>sin novedades</color>" : ""), _small);
            y += _small.fontSize * 1.8f;
            for (int i = _a.Intel.Count - n; i < _a.Intel.Count; i++)
            {
                var e = _a.Intel[i];
                string text = _texts.Format(e, "es", out var src);
                GUI.Label(new Rect(x + 12, y, w - 20, 40), $"<color=#C8B890>{NetConst.Clock(e.Tick)} · {src}</color> · {text}", _small);
                y += _small.fontSize * 1.6f;
            }
            DrawApp6Leyenda(x + 12, y, w - 20);
        }

        void Capture()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"union_{System.DateTime.Now:yyyyMMdd_HHmmss}.png"));
        }
    }
}
