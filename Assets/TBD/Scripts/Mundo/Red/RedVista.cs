using System.Collections.Generic;
using TBD.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TBD.Mundo
{
    /// <summary>
    /// Vista de la red del Hito 1 (Iker) sobre la escena del mundo de Marcos. Solo dibuja lo que dice Mundo.Red y le mete órdenes:
    /// - T: modo trazar. Arrastrar con el botón izquierdo dibuja la vía; la cadena de piezas del kit se ajusta en vivo (D-N209). Vista previa en hueso si vale,
    ///   en ámbar con un aspa y la causa si no (lámina 5 de Bruno). Al soltar, se traza. Esc cancela.
    /// - Con A y B unidas, la línea por defecto (4 cajas a B y 2 palés de vuelta, Hugo) se crea sola y el tren la recorre.
    /// La simulación va a 30 ticks/s (1:1, D-N208). Hasta que exista el Mundo común en la escena, RedVista lleva su propia Red (las mismas órdenes y el mismo código).
    /// </summary>
    public sealed class RedVista : MonoBehaviour
    {
        MundoBootstrap _mb; readonly Red _red = new Red();
        int _a = -1, _b = -1, _tren = -1, _linea = -1; int _tick; float _acc;
        readonly List<PointMm> _trazo = new List<PointMm>(); bool _modo, _arrastrando; LineRenderer _previa; string _causa; bool _valida; Vector3 _finPrevia;
        readonly List<GameObject> _tramosGo = new List<GameObject>(); int _tramosDibujados;
        Transform _trenGo; readonly List<Transform> _vagones = new List<Transform>(); Vector3[] _posAnt, _posAct; Quaternion[] _rotAnt, _rotAct;
        Material _mBalasto, _mCarril, _mTren, _mPrevia;
        static readonly Color Hueso = new Color32(0xe8, 0xdc, 0xc0, 0xff), Ambar = new Color32(0xe0, 0x8c, 0x30, 0xff);
        const float AlturaCarril = 0.72f;      // Teo: cabeza del carril a 0,72 m

        void Start()
        {
            _mb = GetComponent<MundoBootstrap>();
            _mBalasto = Mat(new Color32(0x60, 0x5c, 0x48, 255)); _mCarril = Mat(new Color32(0x38, 0x38, 0x3c, 255)); _mTren = Mat(new Color32(0x4a, 0x55, 0x3a, 255));
            _mPrevia = new Material(Shader.Find("Sprites/Default"));
            // estaciones de Álvaro (posiciones_hito1.csv): A (1060; 430) y B (1300; 1640), en mm (X, Z del terreno = X, Y de la red)
            _a = _red.AddEstacion("A", 1_060_000, 430_000); _b = _red.AddEstacion("B", 1_300_000, 1_640_000);
            _red.Almacen[_a][Red.Municion] = 20;
            try { var csv = System.IO.Path.Combine(Application.streamingAssetsPath, "Hito1", "carreteras_hito1.csv"); if (System.IO.File.Exists(csv)) Debug.Log($"[RED] carreteras de Álvaro: {_red.Via.CargarCarreterasCsv(System.IO.File.ReadAllText(csv))} trozos (sin el puente)"); }
            catch (System.Exception e) { Debug.LogWarning("[RED] carreteras_hito1.csv: " + e.Message); }
            _tren = _red.NuevoTren("Tren 1", _a);
            var gp = new GameObject("Vía · vista previa"); _previa = gp.AddComponent<LineRenderer>(); _previa.material = _mPrevia; _previa.positionCount = 0; _previa.numCapVertices = 2;
            MontarTren();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-redauto") >= 0) { var r = _red.TrazarVia(new List<PointMm> { new PointMm(1_060_000, 430_000), new PointMm(1_300_000, 1_640_000) }); Debug.Log("[RED] -redauto: vía A–B " + (r.Ok ? "trazada" : "rechazada: " + r.Por)); }
            Debug.Log("[RED] vista de la red lista: T para trazar vía (arrastrar con el botón izquierdo), Esc cancela");
        }

        static Material Mat(Color c) { var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")); m.color = c; return m; }

        void Update()
        {
            Entrada();
            // simulación a 30 ticks/s (1:1); -redx8 para probar
            float x = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-redx8") >= 0 ? 8f : 1f;
            _acc += Time.deltaTime * x; int pasos = 0;
            while (_acc >= 1f / 30f && pasos < 40) { _acc -= 1f / 30f; pasos++; GuardarAnterior(); _red.Step(_tick++); LeerTren(); }
            if (_linea < 0) LineaSiUnidas();
            DibujarTramos();
            InterpolarTren(_acc * 30f);
            Capturas();
        }

        // QA: -redcapturas 20,60,120 = capturas a esos segundos reales (Captures/red_<s>s.png); -redfoco x,z,altura = coloca la cámara (lo hace MundoBootstrap con -altura)
        readonly HashSet<float> _capHechas = new HashSet<float>();
        void Capturas()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-redcapturas"); if (i < 0 || i + 1 >= a.Length) return;
            foreach (var t in a[i + 1].Split(','))
                if (float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) && Time.realtimeSinceStartup >= s && _capHechas.Add(s))
                {
                    var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Captures")); System.IO.Directory.CreateDirectory(dir);
                    var f = System.IO.Path.Combine(dir, $"red_{s:0}s.png"); ScreenCapture.CaptureScreenshot(f);
                    var tr = _red.Trenes[_tren]; Debug.Log($"[RED] captura {f}: tren {tr.Estado}, parada {tr.Parada}, A {_red.Almacen[_a][0]} cajas, B {_red.Almacen[_b][0]} cajas, {_red.TextoLinea(_linea)}");
                }
        }

        // ---------------------------------------------------------------- trazado (D-N209: trazo libre, piezas en vivo)
        void Entrada()
        {
            var kb = Keyboard.current; var ms = Mouse.current; if (kb == null || ms == null) return;
            if (kb.tKey.wasPressedThisFrame) { _modo = !_modo; _trazo.Clear(); _previa.positionCount = 0; _causa = null; Debug.Log("[RED] modo trazar " + (_modo ? "activado" : "desactivado")); }
            if (kb.escapeKey.wasPressedThisFrame && _modo) { _modo = false; _arrastrando = false; _trazo.Clear(); _previa.positionCount = 0; }
            MundoBootstrap.TrazandoVia = _modo;
            if (!_modo) return;
            var pos = ms.position.ReadValue();
            if (ms.leftButton.wasPressedThisFrame && _mb.PuntoSuelo(pos, out var p0)) { _arrastrando = true; _trazo.Clear(); _trazo.Add(Mm(p0)); }
            if (_arrastrando && ms.leftButton.isPressed && _mb.PuntoSuelo(pos, out var p))
            {
                var q = Mm(p); var u = _trazo[_trazo.Count - 1];
                if (IMath.Dist(q.X - u.X, q.Y - u.Y) >= 5_000) { _trazo.Add(q); Previa(); }
            }
            if (_arrastrando && ms.leftButton.wasReleasedThisFrame)
            {
                _arrastrando = false;
                if (_trazo.Count >= 2)
                {
                    var r = _red.TrazarVia(_trazo);
                    Debug.Log(r.Ok ? $"[RED] vía trazada: tramo {r.Tramo}, {_red.Via.Tramos[r.Tramo].Piezas.Length} piezas, {_red.Via.Tramos[r.Tramo].LongMm / 1000} m, {_red.Via.PasosNivel.Count} pasos a nivel" : "[RED] vía rechazada: " + r.Por);
                    if (r.Ok) { _previa.positionCount = 0; _causa = null; } else _causa = r.Por;
                }
                _trazo.Clear();
            }
        }

        static PointMm Mm(Vector3 p) { return new PointMm(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.z * 1000f)); }
        Vector3 AMundo(PointMm p, float sobre = 0f) { float x = p.X / 1000f, z = p.Y / 1000f; return new Vector3(x, _mb.AlturaSuelo(x, z) + sobre, z); }

        /// <summary>Vista previa: la cadena de piezas sobre una copia de la vía (la simulación no se toca hasta soltar).</summary>
        void Previa()
        {
            var copia = new Red(); var w = new TBD.Net.SaveWriter(); _red.Save(w); copia.Load(new TBD.Net.SaveReader(w.ToArray()));
            var r = copia.Via.TrazarLibre(_trazo);
            _valida = r.Ok; _causa = r.Ok ? null : r.Por;
            List<PointMm> pts;
            if (r.Ok) pts = new List<PointMm>(copia.Via.Tramos[r.Tramo].Puntos);
            else { pts = new List<PointMm>(_trazo); }
            float ancho = Mathf.Clamp(_mb.AlturaCamara * 0.006f, 1.5f, 25f);
            _previa.startWidth = _previa.endWidth = ancho; _previa.startColor = _previa.endColor = _valida ? Hueso : Ambar;
            _previa.positionCount = pts.Count; for (int i = 0; i < pts.Count; i++) _previa.SetPosition(i, AMundo(pts[i], 1.5f));
            _finPrevia = AMundo(pts[pts.Count - 1], 1.5f);
        }

        void OnGUI()
        {
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(13, Screen.height / 60), richText = true };
            float y = Screen.height - 70f;
            if (_linea >= 0) GUI.Label(new Rect(16f, y - 26f, 900f, 28f), "<color=#e8dcc0>Línea A–B · " + _red.TextoLinea(_linea) + $" · tren: {_red.Trenes[_tren].Estado} · A {_red.Almacen[_a][0]} cajas · B {_red.Almacen[_b][0]} cajas</color>", st);
            GUI.Label(new Rect(16f, y, 900f, 28f), _modo ? "<color=#e8dcc0>Trazar vía: arrastra desde una estación o una vía con el botón izquierdo · Esc cancela</color>" : "<color=#8c8a86>T: trazar vía</color>", st);
            if (_modo && _causa != null && _mb.CamaraMundo != null)
            {
                var sp = _mb.CamaraMundo.WorldToViewportPoint(_finPrevia);
                if (sp.z > 0f) { float sx = sp.x * Screen.width, sy = (1f - sp.y) * Screen.height; GUI.Label(new Rect(sx - 10f, sy - 14f, 30f, 28f), "<color=#e08c30><b>✕</b></color>", st); GUI.Label(new Rect(sx + 18f, sy - 14f, 600f, 28f), "<color=#e08c30>" + _causa + "</color>", st); }
            }
        }

        // ---------------------------------------------------------------- línea por defecto en cuanto A y B están unidas
        void LineaSiUnidas()
        {
            if (_red.Via.Ruta(_red.Via.Estaciones[_a].Nodo, _red.Via.Estaciones[_b].Nodo) == null) return;
            var cA = new int[Red.Recursos]; cA[Red.Municion] = 4; var dA = new int[Red.Recursos]; dA[Red.Material] = -1;
            var cB = new int[Red.Recursos]; cB[Red.Material] = 2; var dB = new int[Red.Recursos]; dB[Red.Municion] = -1;
            _linea = _red.CrearLinea("A–B", new List<ParadaLinea> { new ParadaLinea { Estacion = _a, Cargar = cA, Descargar = dA }, new ParadaLinea { Estacion = _b, Cargar = cB, Descargar = dB } });
            _red.AsignarTren(_linea, _tren);
            Debug.Log("[RED] A y B unidas: línea por defecto (4 cajas a B, 2 palés de vuelta) y el tren sale");
        }

        // ---------------------------------------------------------------- vía: balasto y dos carriles por tramo, siguiendo el terreno
        void DibujarTramos()
        {
            while (_tramosDibujados < _red.Via.Tramos.Count)
            {
                var t = _red.Via.Tramos[_tramosDibujados++];
                var go = new GameObject($"Vía · tramo {t.Id} ({t.Piezas?.Length ?? 0} piezas)");
                Cinta(go.transform, t.Puntos, 0f, 3.4f, 0.25f, _mBalasto, "Balasto");
                Cinta(go.transform, t.Puntos, -0.75f, 0.12f, AlturaCarril, _mCarril, "Carril izquierdo");
                Cinta(go.transform, t.Puntos, 0.75f, 0.12f, AlturaCarril, _mCarril, "Carril derecho");
                _tramosGo.Add(go);
            }
        }

        void Cinta(Transform padre, PointMm[] pts, float desplaz, float ancho, float alto, Material m, string nombre)
        {
            var go = new GameObject(nombre); go.transform.SetParent(padre, false);
            var verts = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i < pts.Length; i++)
            {
                var p = AMundo(pts[i]); var a = AMundo(pts[Mathf.Max(0, i - 1)]); var b = AMundo(pts[Mathf.Min(pts.Length - 1, i + 1)]);
                var dir = (b - a); dir.y = 0f; dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward; var izq = new Vector3(-dir.z, 0f, dir.x);
                var c = p + izq * desplaz;
                verts.Add(c + izq * (ancho * 0.5f) + Vector3.up * alto); verts.Add(c - izq * (ancho * 0.5f) + Vector3.up * alto);
                if (i > 0) { int k = verts.Count - 4; tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        // ---------------------------------------------------------------- tren: locomotora y dos vagones (2 × 3 huecos, Hugo), interpolado entre ticks
        void MontarTren()
        {
            _trenGo = new GameObject("Tren 1").transform;
            for (int i = 0; i < 3; i++)
            {
                var v = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(v.GetComponent<Collider>());
                v.name = i == 0 ? "Locomotora" : "Vagón " + i; v.transform.SetParent(_trenGo, false); v.transform.localScale = new Vector3(3f, i == 0 ? 3.8f : 2.6f, i == 0 ? 16f : 13f);
                v.GetComponent<MeshRenderer>().sharedMaterial = _mTren; _vagones.Add(v.transform);
            }
            _posAnt = new Vector3[3]; _posAct = new Vector3[3]; _rotAnt = new Quaternion[3]; _rotAct = new Quaternion[3];
            LeerTren(); GuardarAnterior();
        }

        void GuardarAnterior() { for (int i = 0; i < 3; i++) { _posAnt[i] = _posAct[i]; _rotAnt[i] = _rotAct[i]; } }

        void LeerTren()
        {
            var t = _red.Trenes[_tren];
            for (int i = 0; i < 3; i++)
            {
                int atras = i == 0 ? 0 : 8_500 + (i - 1) * 14_000 + 6_500;      // centros de cada coche detrás de la cabeza
                var p = PosEnRuta(t, atras, out int rumbo);
                var dir = new Vector3(IMath.Cos(rumbo) / (float)IMath.One, 0f, IMath.Sin(rumbo) / (float)IMath.One);
                _posAct[i] = AMundo(p, AlturaCarril + (i == 0 ? 1.9f : 1.3f)); _rotAct[i] = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Vector3.forward);
            }
        }

        /// <summary>Punto de la ruta del tren «atras» mm por detrás de la cabeza (dentro del tramo actual; parado, en el nodo).</summary>
        PointMm PosEnRuta(Tren t, int atras, out int rumbo)
        {
            rumbo = 0;
            if (t.Estado != EstadoTren.EnMarcha || t.Ruta.Count == 0)
            {
                foreach (var tr in _red.Via.Tramos) if (tr.A == t.Nodo || tr.B == t.Nodo) { bool alA = tr.A == t.Nodo; var q = _red.Via.Posicion(tr.Id, alA ? Mathf.Min(tr.LongMm, atras) : Mathf.Max(0, tr.LongMm - atras), out rumbo); if (!alA) rumbo = (rumbo + IMath.Turn / 2) & 0xFFFF; return q; }
                var n = _red.Via.Nodos[Mathf.Max(0, t.Nodo)]; return new PointMm(n.X, n.Y - atras);
            }
            var ps = t.Ruta[t.PasoI]; var tram = _red.Via.Tramos[ps.Tramo]; int off = Mathf.Max(0, t.OffMm - atras);
            var p = _red.Via.Posicion(ps.Tramo, ps.Directo ? off : tram.LongMm - off, out rumbo); if (!ps.Directo) rumbo = (rumbo + IMath.Turn / 2) & 0xFFFF;
            return p;
        }

        void InterpolarTren(float f)
        {
            f = Mathf.Clamp01(f);
            for (int i = 0; i < 3; i++) { _vagones[i].position = Vector3.Lerp(_posAnt[i], _posAct[i], f); _vagones[i].rotation = Quaternion.Slerp(_rotAnt[i], _rotAct[i], f); }
        }
    }
}
