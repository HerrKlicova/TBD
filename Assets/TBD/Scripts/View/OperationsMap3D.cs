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
        /// <summary>F0: posición en el mundo del icono del tren «i» (para seleccionarlo en el mapa).</summary>
        public Vector3 PosTren(int i) { return i >= 0 && i < _trains.Count && _trains[i] != null ? _trains[i].position : new Vector3(0, -9999, 0); }
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

        float _mapT0, _diagMapaT;
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
            if (!TutorialHud.Activo && NetSession.B != null && NetSession.B != _a) NetSession.B.Director.ColmenaIntro(NetSession.B);   // qa52: la entrada CO-0 en los dos mundos (con solo A, desincronización de «memoria» en el tick 5776)
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
                _dist = _targetDist = 6f;      // S1 qa52 121-123 (Marcos): a 2,5 km la cámara quedaba pegada al relieve (pantalla negra); a 6 km se ve la estación y no se vuelve a entrar (umbral 2 km)
                _pitch = 50f;
                NetSession.ReturnFromNode = -1;
            }
            { var ck = System.Environment.GetCommandLineArgs(); int ik = System.Array.IndexOf(ck, "-mapa-km"); if (ik >= 0 && ik + 1 < ck.Length && float.TryParse(ck[ik + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var km) && km > 0f) { _dist = _targetDist = Mathf.Clamp(km, 0.3f, 600f); _armed = true; Debug.Log($"[MAPA] -mapa-km: cámara a {_dist:0.#} km"); } }   // QA (Marcos): capturas -ui3 a 150 y 300 km
        }

        /// <summary>QA: -trenpanel-auto y -refuerzos-auto se juegan en el mapa (TrenPanel solo vive en Union); con ellas no se entra directo en la noche.</summary>
        static bool PruebaDeMapa { get { var a = System.Environment.GetCommandLineArgs(); return System.Array.IndexOf(a, "-trenpanel-auto") >= 0 || System.Array.IndexOf(a, "-refuerzos-auto") >= 0 || System.Array.IndexOf(a, "-dia2-auto") >= 0 || System.Array.IndexOf(a, "-dia2-auto-sinescolta") >= 0 || System.Array.IndexOf(a, "-dia2-auto-tren-aparcado") >= 0 || System.Array.IndexOf(a, "-dia2-auto-enzona") >= 0; } }

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

        // avisos de la red en el mapa (Nuria, sonido-vivo.md). Solo vista. La alarma pasa por el filtro del mapa y suena lejana; el tren perdido, con su golpe y la horda.
        readonly HashSet<int> _asedioSonado = new HashSet<int>(); bool _trenPerdidoAntes, _redIni;
        void SonidosRed()
        {
            if (_a == null) return;
            var ahora = new HashSet<int>();
            foreach (var pl in _a.Director.Active)
                if (pl.Kind == TBD.Net.HiveDirector.PlanKind.Asedio && pl.Started && pl.Node >= 0) ahora.Add(pl.Node);
            if (!_redIni) { _redIni = true; foreach (var n in ahora) _asedioSonado.Add(n); _trenPerdidoAntes = _a.LastAmbush.TrainLost; return; }   // al abrir el mapa no suena lo que ya estaba
            foreach (var n in ahora) if (!_asedioSonado.Contains(n)) { Sfx.Play("alarma_ataque", Vector3.zero, 0.3f, 20f); Sfx.Duck(2f); break; }   // una por fotograma aunque empiecen dos
            _asedioSonado.Clear(); foreach (var n in ahora) _asedioSonado.Add(n);
            bool perdido = _a.LastAmbush.TrainLost;
            if (perdido && !_trenPerdidoAntes) { Sfx.Play("tren_caido_puertas", Vector3.zero, 0.6f, 5f); Sfx.Duck(2f); }
            _trenPerdidoAntes = perdido;
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
            if (Time.realtimeSinceStartup - _mapT0 < 12f && Time.realtimeSinceStartup >= _diagMapaT) { _diagMapaT = Time.realtimeSinceStartup + 2f; Debug.Log($"[MAPA-DIAG] t={Time.realtimeSinceStartup - _mapT0:0.0} dist={_dist:0.0} foco={_focus} zona_red={_a.ActiveZoneNode} pausa={_a.Paused} vel={_a.Speed} frenada={NetSession.Frenada} tick={_a.Tick} cam={(Camera.main != null ? Camera.main.name + " " + Camera.main.enabled + " " + Camera.main.transform.position : "ninguna")} entrando={_entering} fps={1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime):0}"); }   // S1 qa52 121 (Marcos): mapa negro tras M
            NetSession.Frenada = false; PrototypeBootstrap.AsegurarNochesRed();          // reloj único (en el mapa la red no espera a ninguna zona): las noches de Sagunto y Nules en la red (A y B), una vez por partida
            SonidosColmena();
            SonidosRed();                                 // Nuria (sonido-vivo.md): estación atacada y tren perdido fuera de pantalla
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
            var ae = AerodromoVista.Estado;      // F0: Bétera (no es nodo de la red) se entra acercándose a su H; la zona de la red debajo es la de casa (Marcos, marcos/f0-zona)
            if (ae != null && _a.Embarque.HomeNode >= 0)
            {
                float db = Vector3.Distance(new Vector3(_focus.x, 0, _focus.z), MapToWorldFlat(ae.MapX, ae.MapY));
                if (db < 1.5f && db < bd) { ZonaBetera.Activa = true; Debug.Log("[MAPA] entro en Bétera"); EnterZone(_a.Embarque.HomeNode); return; }
            }
            if (best < 0) return;
            ZonaBetera.Activa = false;
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
            Sfx.Play("zoom_entrar_zona", Vector3.zero, 0.8f, 0.5f);                     // Nuria: barrido de zoom hacia la zona (sonido-vivo.md)
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

        static readonly bool DepuracionMapa = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-depuracion") >= 0;
        // ---------------------------------------------------------------- Lucía (ed7f0ae): estaciones en el mapa (se quita el verde)
        static Texture2D _disco, _anillo;
        static readonly Color CFondoRotulo = new Color(0.059f, 0.071f, 0.047f, 0.88f);   // D-N185: fondo de los rótulos con -ui3 (0f120c al 88 %)
        static Texture2D Circulo(bool lleno) { const int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear }; float c = (n - 1) * 0.5f, r = n * 0.5f - 1f; for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)); float a = lleno ? Mathf.Clamp01(r - d + 0.5f) : Mathf.Clamp01(1f - Mathf.Abs(d - (r - 3f)) / 2.5f); t.SetPixel(x, y, new Color(1, 1, 1, a)); } t.Apply(); return t; }
        static readonly Color CHueso = new Color32(0xd4, 0xd0, 0xcc, 0xff), COscuro = new Color32(0x1c, 0x20, 0x0c, 0xff), CAmbar = new Color32(0xe0, 0x8c, 0x30, 0xff), CRojo = new Color32(0x98, 0x24, 0x20, 0xff), CGris = new Color32(0xb8, 0xb8, 0xb8, 0x80);
        static void Dibuja(Texture2D t, Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, t); GUI.color = o; }
        bool Propia(int n) { return !_a.NodeLost[n] && (_a.Stations.Founded[n] || n == _a.Embarque.HomeNode); }
        int PelotonesEn(RailNode nd) { int c = Plantel.De(nd.Name).Pelotones.Length + (nd.Name == "Sagunto" ? 2 : 0); return c + _a.Embarque.AtStation(nd.Id, PasajeroTipo.Peloton).Count; }
        bool Atacada(int n) { if (_a.Besieged(n)) return true; foreach (var p in _a.Director.Active) if (p.Kind == HiveDirector.PlanKind.Asedio && p.Started && p.Node == n) return true; return false; }
        void MarcaEstacion(RailNode nd, Vector3 sp, bool todosNombres = false, int trenes = 0)
        {
            if (_disco == null) { _disco = Circulo(true); _anillo = Circulo(false); }
            int n = nd.Id; bool perdida = _a.NodeLost[n], propia = Propia(n), cerco = n == _a.Director.CerradaNodo;
            bool importante = propia || perdida;
            float px = IconPx(32) * (importante ? 1f : 0.5f); var c = new Vector2(sp.x, Screen.height - sp.y - IconPx(32) * 0.6f);
            Rect R(float s) { return new Rect(c.x - s * 0.5f, c.y - s * 0.5f, s, s); }
            if (propia) { Dibuja(_disco, R(px + 2f), COscuro); Dibuja(_disco, R(px), CHueso); }          // relleno en hueso con contorno oscuro de 1 px
            else if (perdida) { Dibuja(_disco, R(px + 2f), COscuro); Dibuja(_disco, R(px), new Color(CRojo.r, CRojo.g, CRojo.b, 0.55f)); }   // perdida: rojo apagado
            else Dibuja(_anillo, R(px), CGris);                                                            // el resto: hueco, gris al 50 %, más pequeño
            if (cerco) Dibuja(_anillo, R(px * 1.7f), CAmbar);
            if (propia && Atacada(n) && Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f) Dibuja(_anillo, R(px * 2.1f), CRojo);   // atacada: anillo rojo que parpadea
            var mp = Event.current.mousePosition; bool encima = (mp - c).magnitude < Mathf.Max(px, 14f);
            string nombre = cerco ? nd.Name + " · cercada" : propia ? nd.Name + " · " + PelotonesEn(nd) : nd.Name;
            if (trenes > 0) nombre += trenes == 1 ? " · 1 tren" : " · " + trenes + " trenes";      // D-N185: lejos, los trenes parados van en el rótulo
            if (Ui3.Activa) _rotulos.Add(R(px));      // D-N185: los rótulos tampoco pisan el disco de la estación
            if (importante || encima || (Ui3.Activa && (cerco || todosNombres)))      // D-N185 (-ui3): la cercada siempre con nombre y, de cerca, todas
            {
                var lr = SinPisar(new Rect(c.x + px * 0.5f + 4f, c.y - _small.fontSize * 0.7f, _small.CalcSize(new GUIContent(nombre)).x + 4f, _small.fontSize * 1.4f));
                if (Ui3.Activa) Dibuja(Texture2D.whiteTexture, new Rect(lr.x - 3f, lr.y, lr.width + 6f, lr.height), CFondoRotulo);   // D-N185: rótulo sobre fondo, que se lea encima del terreno
                GUI.Label(lr, nombre, _small);
                if (perdida) Dibuja(Texture2D.whiteTexture, new Rect(lr.x, lr.y + lr.height * 0.5f, lr.width - 4f, 1.5f), CRojo);   // tachado
            }
            if (cerco && encima) { var tr = new Rect(c.x + px * 0.5f + 4f, c.y + _small.fontSize * 0.9f, 360, _small.fontSize * 2.2f); GUI.DrawTexture(tr, Texture2D.blackTexture); GUI.Label(tr, " Castellón · cercada. Aguanta, pero está aislada.", _small); }
        }
        // ---------------------------------------------------------------- D-N185: estaciones con -ui3
        /// <summary>Zoom del mapa en tres niveles (D-N185). Cerca: todos los nombres. Medio: nombres de las propias, las perdidas y la cercada,
        /// y el resto al pasar el ratón. Lejos: las estaciones que no son tuyas se ocultan (salvo al pasar el ratón).</summary>
        public const float ZoomCercaKm = 40f, ZoomLejosKm = 300f;
        public int NivelZoom => _dist < ZoomCercaKm ? 0 : _dist < ZoomLejosKm ? 1 : 2;
        readonly List<int> _estNodo = new List<int>(); readonly List<Vector3> _estSp = new List<Vector3>(); readonly List<int> _estPrio = new List<int>();
        readonly List<int> _grupo = new List<int>(); bool[] _enGrupo;
        GUIStyle _cuenta;
        int[] _trenesEst = new int[8]; bool[] _trenOculto = new bool[8];
        /// <summary>Dónde va el rótulo de Bétera este fotograma (lo dibuja AerodromoVista). Con -ui3 se reserva aquí, para que no lo pisen los trenes ni las estaciones.</summary>
        public static Rect RotuloBetera; public static bool RotuloBeteraOk; public static Vector2 TamRotuloBetera = new Vector2(180f, 26f);
        public static readonly Color FondoRotulo = CFondoRotulo;
        void RotuloBeteraReserva()
        {
            RotuloBeteraOk = false; var ae = AerodromoVista.Estado; if (ae == null) return;
            var sp = _cam.WorldToScreenPoint(MapToWorld(ae.MapX, ae.MapY, 0f)); if (sp.z <= 0) return;
            int px = IconPx(32); var r = new Rect(sp.x - px * 0.6f, Screen.height - sp.y - px * 1.2f, px * 1.2f, px * 1.3f);
            _rotulos.Add(r);
            RotuloBetera = SinPisar(new Rect(r.xMax + 4f, r.y + px * 0.3f, TamRotuloBetera.x + 10f, TamRotuloBetera.y)); RotuloBeteraOk = true;
        }

        /// <summary>Prioridad del rótulo: 0 atacada o cercada, 1 propia, 2 perdida, 3 el resto. Las de prioridad más alta reservan su sitio antes.</summary>
        int PrioridadEst(int n) { if (n == _a.Director.CerradaNodo || (Propia(n) && Atacada(n))) return 0; if (Propia(n)) return 1; if (_a.NodeLost[n]) return 2; return 3; }

        void EstacionesUi3()
        {
            var g = _a.Graph; int nivel = NivelZoom; var mp = Event.current.mousePosition;
            _estNodo.Clear(); _estSp.Clear(); _estPrio.Clear();
            for (int i = 0; i < g.Nodes.Count; i++)
            {
                var n = g.Nodes[i];
                var sp = _cam.WorldToScreenPoint(MapToWorld(n.XMeters, n.YMeters, 0.8f * Mathf.Clamp(_dist / 230f, 0.0015f, 2f)));
                if (sp.z <= 0) continue;
                int pr = PrioridadEst(n.Id);
                var c = new Vector2(sp.x, Screen.height - sp.y - IconPx(32) * 0.6f);
                if (nivel == 2 && pr == 3 && (mp - c).magnitude > 18f) continue;          // lejos: solo lo tuyo
                _estNodo.Add(i); _estSp.Add(sp); _estPrio.Add(pr);
            }
            // lejos: los trenes parados junto a una estación no se dibujan; se cuentan en su rótulo (captura de Marcos a 300 km, I-Noche115)
            if (_trenesEst.Length < _estNodo.Count) _trenesEst = new int[Mathf.Max(8, _estNodo.Count * 2)];
            for (int k = 0; k < _estNodo.Count; k++) _trenesEst[k] = 0;
            if (_trenOculto.Length < _trains.Count) _trenOculto = new bool[Mathf.Max(8, _trains.Count * 2)];
            for (int i = 0; i < _trains.Count; i++)
            {
                _trenOculto[i] = false;
                if (nivel < 2 || i >= _a.Services.Count) continue;
                var sv = _a.Services[i];
                if (sv.Train.State == RailTrainState.EnMarcha || sv.Ambushed || (_a.Encounter.Active && _a.Encounter.Service == i)) continue;
                var ts = _cam.WorldToScreenPoint(_trains[i].position); if (ts.z <= 0) continue;
                int mejor = -1; float md = IconPx(32) * 1.6f;
                for (int k = 0; k < _estNodo.Count; k++) { float d = ((Vector2)_estSp[k] - (Vector2)ts).magnitude; if (d < md) { md = d; mejor = k; } }
                if (mejor >= 0) { _trenOculto[i] = true; _trenesEst[mejor]++; }
            }
            // agrupación: propias tranquilas (ni atacadas ni cercadas) que en pantalla quedan a menos de 1,6 discos
            if (_enGrupo == null || _enGrupo.Length < _estNodo.Count) _enGrupo = new bool[Mathf.Max(8, _estNodo.Count * 2)];
            for (int k = 0; k < _estNodo.Count; k++) _enGrupo[k] = false;
            float radio = IconPx(32) * 1.6f;
            for (int k = 0; k < _estNodo.Count; k++)
            {
                if (_enGrupo[k] || _estPrio[k] != 1) continue;
                _grupo.Clear(); _grupo.Add(k);
                for (int j = k + 1; j < _estNodo.Count; j++)
                    if (!_enGrupo[j] && _estPrio[j] == 1 && ((Vector2)_estSp[j] - (Vector2)_estSp[k]).magnitude < radio) _grupo.Add(j);
                if (_grupo.Count < 2) continue;
                foreach (var j in _grupo) _enGrupo[j] = true;
                MarcaGrupo(_grupo);
            }
            // el resto, por prioridad (atacadas y cercada primero), para que sus rótulos reserven sitio antes
            for (int pr = 0; pr <= 3; pr++)
                for (int k = 0; k < _estNodo.Count; k++)
                    if (!_enGrupo[k] && _estPrio[k] == pr) MarcaEstacion(g.Nodes[_estNodo[k]], _estSp[k], nivel == 0, _trenesEst[k]);
            RotuloBeteraReserva();      // después de las estaciones, antes que los trenes
        }

        /// <summary>Varias estaciones propias juntas en pantalla: un disco hueso con el número y «Sagunto +2»; al pasar el ratón, la tarjeta con cada una.</summary>
        void MarcaGrupo(List<int> grupo)
        {
            if (_disco == null) { _disco = Circulo(true); _anillo = Circulo(false); }
            if (_cuenta == null) { _cuenta = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(_small.fontSize * 1.1f) }; _cuenta.normal.textColor = COscuro; _cuenta.padding = new RectOffset(0, 0, 0, 0); }
            Vector2 c = Vector2.zero; int cabeza = grupo[0];
            foreach (var k in grupo) { c += (Vector2)_estSp[k]; if (g_Nodo(k) == _a.Embarque.HomeNode) cabeza = k; }
            c /= grupo.Count; c = new Vector2(c.x, Screen.height - c.y - IconPx(32) * 0.6f);
            float px = IconPx(32) * 1.25f; var r = new Rect(c.x - px * 0.5f, c.y - px * 0.5f, px, px);
            Dibuja(_disco, new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), COscuro); Dibuja(_disco, r, CHueso);
            Dibuja(_anillo, new Rect(r.x - px * 0.18f, r.y - px * 0.18f, px * 1.36f, px * 1.36f), CHueso);      // anillo exterior: «son varias»
            GUI.Label(r, grupo.Count.ToString(), _cuenta);
            _rotulos.Add(r);
            string nombre = _a.Graph.Nodes[g_Nodo(cabeza)].Name + " +" + (grupo.Count - 1);
            int tg = 0; foreach (var k in grupo) tg += _trenesEst[k];
            if (tg > 0) nombre += tg == 1 ? " · 1 tren" : " · " + tg + " trenes";
            var lr = SinPisar(new Rect(r.xMax + 4f, c.y - _small.fontSize * 0.7f, _small.CalcSize(new GUIContent(nombre)).x + 4f, _small.fontSize * 1.4f));
            Dibuja(Texture2D.whiteTexture, new Rect(lr.x - 3f, lr.y, lr.width + 6f, lr.height), CFondoRotulo);
            GUI.Label(lr, nombre, _small);
            var mp = Event.current.mousePosition;
            if ((mp - c).magnitude < px || lr.Contains(mp))
            {
                float lh = _small.fontSize * 1.45f, w = 0f;
                foreach (var k in grupo) w = Mathf.Max(w, _small.CalcSize(new GUIContent(Linea(k))).x);
                var tr = new Rect(lr.x - 3f, lr.yMax + 2f, w + 18f, lh * grupo.Count + 10f);
                Dibuja(Texture2D.whiteTexture, tr, CFondoRotulo);
                Dibuja(Texture2D.whiteTexture, new Rect(tr.x, tr.y, 2f, tr.height), CHueso);
                for (int i = 0; i < grupo.Count; i++) GUI.Label(new Rect(tr.x + 10f, tr.y + 5f + lh * i, w + 6f, lh), Linea(grupo[i]), _small);
            }
        }
        int g_Nodo(int k) { return _a.Graph.Nodes[_estNodo[k]].Id; }
        string Linea(int k) { var nd = _a.Graph.Nodes[_estNodo[k]]; return nd.Name + " · " + PelotonesEn(nd); }

        float _nombreTrenW;
        long _emSonado = -1; long _trenParadoAvisado = -1; long _recordatorioLog = -1;
        /// <summary>Lucía (13365b8): el día 2, de 15:00 a 22:00, si aún no ha salido ningún tren hacia Nules.</summary>
        bool NulesSinCumplirA19() { int nul = -1; for (int i = 0; i < _a.Graph.Nodes.Count; i++) if (_a.Graph.Nodes[i].Name == "Nules") nul = i; long hd = _a.Tick % NetConst.DayTicks; return nul >= 0 && _a.Tick / NetConst.DayTicks == 1 && hd >= NetConst.Hours(19) && hd < NetConst.Hours(22) && !_a.NodeLost[nul] && !(_a.ObjTroops && _a.ObjAmmo); }
        bool RecordatorioNules(out int nul)
        {
            nul = -1; for (int i = 0; i < _a.Graph.Nodes.Count; i++) if (_a.Graph.Nodes[i].Name == "Nules") nul = i;
            long hd = _a.Tick % NetConst.DayTicks;
            return nul >= 0 && _a.Tick / NetConst.DayTicks == 1 && hd >= NetConst.Hours(15) && hd < NetConst.Hours(22) && !TrenPanel.TrenHaciaNulesDia2 && !_a.NodeLost[nul] && !_a.ObjMet;
        }
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
            if (RecordatorioNules(out int nulR)) { if (_recordatorioLog != _a.Tick / NetConst.DayTicks) { _recordatorioLog = _a.Tick / NetConst.DayTicks; Debug.Log("[MAPA] Nules sigue sin munición. Si no mandas el tren, caerá esta noche."); } }
            long hd = _a.Tick % NetConst.DayTicks; if (hd < NetConst.Hours(19) || hd >= NetConst.Hours(22)) return;
            foreach (var sv in _a.Services)
            {
                if (!sv.AtStop || sv.Stops.Count == 0) continue; int nn = sv.GateNode >= 0 ? sv.GateNode : sv.Stops[sv.StopIndex].Node;
                if (nn < 0 || nn == _a.Embarque.HomeNode || _a.NodeLost[nn]) continue;
                long k = _a.Tick / NetConst.DayTicks * 1000 + nn; if (_trenParadoAvisado == k) continue; _trenParadoAvisado = k;
                Debug.Log($"[MAPA] Hay un tren parado en {_a.Graph.Nodes[nn].Name}. Sácalo antes de la noche o puede perderse.");
            }
        }
        readonly List<Rect> _rotulos = new List<Rect>();
        /// <summary>Baja el rótulo hasta que no pisa a otro de este fotograma (S3 de Eugenio: «12Costaña», Caminreal y Calamocha).</summary>
        float _altoAvisos;      // D-N185: alto del panel de avisos del fotograma anterior (con -ui3)
        Rect SinPisar(Rect r) { for (int k = 0; k < 6; k++) { bool pisa = false; foreach (var o in _rotulos) if (o.Overlaps(r)) { pisa = true; break; } if (!pisa) break; r.y += r.height; } _rotulos.Add(r); return r; }
        void OnGUI()
        {
            if (_a == null) return;
            _rotulos.Clear();
            EnsureStyles();
            var g = _a.Graph;
            if (Ui3.Activa) EstacionesUi3();                         // D-N185: tres niveles de zoom, propias agrupadas al alejarse y rótulos por prioridad
            else foreach (var n in g.Nodes)
            {
                var sp = _cam.WorldToScreenPoint(MapToWorld(n.XMeters, n.YMeters, 0.8f * Mathf.Clamp(_dist / 230f, 0.0015f, 2f)));
                if (sp.z <= 0) continue;
                MarcaEstacion(n, sp);      // Lucía (ed7f0ae): propias, cercada, atacada, perdida y el resto
            }
            if (!Ui3.Activa) DrawApp6();                             // D-N160: símbolos APP-6 de cada estación; D-N185: con -ui3 el APP-6 solo va en el informe (Tab)
            for (int i = 0; i < _trains.Count; i++)
            {
                var sp = _cam.WorldToScreenPoint(_trains[i].position + Vector3.up * 0.8f * Mathf.Clamp(_dist / 230f, 0.0015f, 2f));
                if (sp.z <= 0) continue;
                if (Ui3.Activa && i < _trenOculto.Length && _trenOculto[i]) continue;     // D-N185: lejos, parado en una estación: va contado en su rótulo
                int tp = IconPx(32);
                var sv = _a.Services[i];
                string tn = _a.Encounter.Active && _a.Encounter.Service == i ? "bajo_ataque" : sv.Ambushed ? "bajo_ataque" : sv.Train.State == RailTrainState.EnMarcha ? "en_marcha" : sv.Train.State == RailTrainState.Detenido ? "detenido" : "detenido";
                if (Ui3.Activa && tn != "bajo_ataque")      // D-N185: siluetas de Bruno (IconosMapa); el tren bajo ataque sigue con su icono rojo
                    GUI.DrawTexture(new Rect(sp.x - tp * 0.5f, Screen.height - sp.y - tp * 1.6f, tp, tp), IconosMapa.Textura(tn == "en_marcha" ? IconosMapa.Icono.TrenMarcha : IconosMapa.Icono.TrenParado, tp >= 24 ? 32 : 16));
                else DrawIcon(new Rect(sp.x - tp * 0.5f, Screen.height - sp.y - tp * 1.6f, tp, tp), "trenes/vm_tren_" + tn + "_32");
                bool nombreTren = !Ui3.Activa || NivelZoom < 2 || new Rect(sp.x - tp * 0.5f, Screen.height - sp.y - tp * 1.6f, tp, tp).Contains(Event.current.mousePosition);
                if (Ui3.Activa && NivelZoom == 2) _rotulos.Add(new Rect(sp.x - tp * 0.5f, Screen.height - sp.y - tp * 1.6f, tp, tp));
                if (Ui3.Activa && !nombreTren) { }      // lejos: el nombre del tren en marcha, solo al pasar el ratón
                else if (Ui3.Activa)      // D-N185: nombre del tren a la derecha del icono, sobre fondo y en hueso; si choca con otro rótulo, baja (1 Costa y 2 Montaña en Sagunto)
                {
                    string tl = "<b>" + _a.Services[i].Train.Name + "</b>";
                    var ic = new Rect(sp.x - tp * 0.5f, Screen.height - sp.y - tp * 1.6f, tp, tp);
                    var trr = SinPisar(new Rect(ic.xMax + 4f, ic.center.y - _small.fontSize * 0.7f, _small.CalcSize(new GUIContent(tl)).x + 10f, _small.fontSize * 1.4f));
                    Dibuja(Texture2D.whiteTexture, trr, CFondoRotulo);
                    GUI.Label(new Rect(trr.x + 5f, trr.y, trr.width, trr.height), tl, _small);
                }
                else { string tl = "<b><color=#FFF0A0>" + _a.Services[i].Train.Name + "</color></b>"; GUI.Label(SinPisar(new Rect(sp.x - 30, Screen.height - sp.y - 26 - _small.fontSize * 0.8f, _small.CalcSize(new GUIContent(tl)).x + 4f, _small.fontSize * 1.4f)), tl, _small); }
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
            float h = Ui3.Activa ? _altoAvisos : _small.fontSize * 14f;   // D-N185: con -ui3 el panel mide lo que lleva dentro (alto del fotograma anterior) y sin avisos no sale
            float top = Ui3.Activa ? 64f : 8f;                       // D-N185: con -ui3 el día, la hora y la velocidad van en la franja de arriba (Ui3.cs)
            if (!Ui3.Activa || h > 1f) GUI.Box(new Rect(8, top, pw, h), GUIContent.none, _panel);
            float y = top + 6;
            string speed = _a.Paused ? "<color=#E8C040>PAUSA</color>" : "x" + _a.Speed;
            if (!Ui3.Activa) { GUI.Label(new Rect(16, y, pw, 40), $"Día {_a.Tick / NetConst.DayTicks + 1} · {NetConst.Clock(_a.Tick)} · {speed}", _title); y += _title.fontSize * 1.6f; }
            string sync = NetSession.Check ? (NetSession.Desync ? "<color=#E04030>DESINCRONIZACIÓN</color>" : "<color=#70C070>Desincronización: correcta</color>") : "<color=#999999>prueba apagada</color>";
            if (DepuracionMapa || NetSession.Desync) { GUI.Label(new Rect(16, y, pw, 30), sync + "  ·  <color=#B0A890>relieve de sustitución (no son datos reales)</color>", _small); y += _small.fontSize * 1.7f; }   // S3 (Eugenio): solo con -depuracion o si hay desincronización
            string scaleTxt = _dist >= 10 ? $"{_dist:0} km" : $"{_dist * 1000:0} m";
            if (DepuracionMapa) { GUI.Label(new Rect(16, y, pw, 30), $"Distancia de cámara: {scaleTxt}", _small); y += _small.fontSize * 1.7f; }
            int shown = 0;
            var zws = _a.UndefendedWarnings();
            SonarAvisos(zws);
            if (Puertas.EnApartadoConAsedio(_a).Count > 0)      // D-N177: el tren en el apartado de Nules con asedio anunciado
            {
                int zp = IconPx(16); shown++;
                DrawIcon(new Rect(20, y - 2, zp, zp), "alertas/vm_alertas_" + AlertIcon(AlertKind.NodoAsediado) + "_16");
                GUI.Label(new Rect(20 + zp + 6, y, pw - 20 - zp, 30), "<color=#E08C30>" + Puertas.TextoApartado + "</color>", _small); y += Mathf.Max(_small.fontSize * 1.5f, zp + 4);
            }
            if (NulesSinCumplirA19())      // Hugo y Lucía (d4d0cc): a las 19:00, si el objetivo no está cumplido, también se puede ir por aire
            { int zp = IconPx(16); shown++; GUI.Label(new Rect(20 + zp + 6, y, pw - 20 - zp, 30), "<color=#E08C30>Nules no está lista para la noche.</color>", _small); y += Mathf.Max(_small.fontSize * 1.5f, zp + 4);
              GUI.Label(new Rect(20 + zp + 6, y, pw - 20 - zp, 30), "<color=#E08C30>Puedes llevar munición en los NH90 desde Bétera.</color>", _small); y += Mathf.Max(_small.fontSize * 1.5f, zp + 4); }   // D-N190: vuelve el aviso
            if (RecordatorioNules(out _))      // Lucía (13365b8): recordatorio de las 15:00, doble clic va a Sagunto
            {
                int zp = IconPx(16); shown++; var rr = new Rect(20, y - 2, pw - 20, Mathf.Max(_small.fontSize * 1.5f, zp + 4));
                DrawIcon(new Rect(20, y - 2, zp, zp), "alertas/vm_alertas_" + AlertIcon(AlertKind.NodoAsediado) + "_16");
                GUI.Label(new Rect(20 + zp + 6, y, pw - 20 - zp, 30), "<color=#E08C30>Nules sigue sin munición. Si no mandas el tren, caerá esta noche.</color>", _small); y += rr.height;
                var ev = Event.current; if (ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2 && rr.Contains(ev.mousePosition)) { ev.Use(); EnterZone(_a.Embarque.HomeNode); }
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
            if (Ui3.Activa) _altoAvisos = y > top + 7f ? y - top + 6f : 0f;
            else if (_a.Alerts.Count == 0) { GUI.Label(new Rect(20, y, pw, 30), "<color=#808080>sin alertas</color>", _small); }
            DrawCargo(pw);
            if (!Ui3.Activa) DrawIntel();                             // D-N185: con -ui3 el informe se abre con Tab (franja de arriba)
            if (!Ui3.Activa) GUI.Label(new Rect(16, Screen.height - _small.fontSize * 2.4f, Screen.width, 30),
                "WASD mover · Q E girar · R F inclinar · rueda zoom (a menos de 2 km de una estación se entra en la zona) · J ir a la alerta · Tab informe · I símbolos APP-6 · Espacio pausa · 1/2/3 velocidad · Intro entrar en la emboscada", _small);
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
            _nombreTrenW = 0f; foreach (var s0 in _a.Services) _nombreTrenW = Mathf.Max(_nombreTrenW, _small.CalcSize(new GUIContent("<b>" + s0.Train.Name + "</b>")).x);
            for (int i = 0; i < _a.Services.Count; i++)
            {
                var sv = _a.Services[i];
                GUI.Label(new Rect(x0 + 8, y, Mathf.Max(130f, _small.CalcSize(new GUIContent("<b>" + sv.Train.Name + "</b>")).x + 8f), p + 4), "<b>" + sv.Train.Name + "</b>", _small);   // S3: «2 Montaña» entero
                float x = x0 + Mathf.Max(136f, _nombreTrenW + 14f);      // Eugenio: «2 Monta» cortado: los iconos van detrás del nombre más largo
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
