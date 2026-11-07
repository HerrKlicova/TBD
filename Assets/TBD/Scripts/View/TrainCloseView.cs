using UnityEngine;
using UnityEngine.InputSystem;
using TBD.Net;

namespace TBD.View
{
    /// <summary>
    /// Vista cercana del tren en marcha (D-N71 P1 #10, D-N84/85): cámara lateral baja (yaw 0, pitch 32°, ortho 7) que sigue al tren, render a baja resolución (1/6, 320×180 a 1080p)
    /// con la paleta de 32 y el contorno, vía con traviesas que se desplazan, postes de catenaria cada 50 m (≈2,5 s a 20 m/s), y fondo en parallax (cielo 0, sierra 0,08, colinas 0,25).
    /// El mundo se desplaza, el tren está quieto en pantalla. Locomotora y coches de Teo. Todo local: no toca la simulación.
    /// </summary>
    public sealed class TrainCloseView : MonoBehaviour
    {
        static TrainCloseView _i;
        public static bool Active { get { return _i != null && _i._on; } }

        bool _on; Camera _cam; RenderTexture _rt; Material _pal; Transform _root; Light _sun; float _s, _speed, _ortho = 7f; int _factor = 6; RailTrain _train; string _name = "";
        Transform[] _posts; Material _sleepMat; Material[] _bgMats; float[] _bgFactor = { 0f, 0.08f, 0.25f }; GameObject[] _bg;
        bool _night; Vector3 _origin = new Vector3(30000f, 0f, 30000f);

        public static void Toggle(int serviceIndex)
        {
            if (_i == null) { var go = new GameObject("Vista cercana del tren"); _i = go.AddComponent<TrainCloseView>(); }
            if (_i._on) _i.Close(); else _i.Open(serviceIndex);
        }

        void Open(int serviceIndex)
        {
            var net = NetSession.A; if (net == null || net.Services.Count == 0) return;
            serviceIndex = Mathf.Clamp(serviceIndex, 0, net.Services.Count - 1);
            var sv = net.Services[serviceIndex]; _train = sv.Train; _name = sv.Train.Name; _s = 0f; _speed = 0f; _on = true;
            _night = IsNight(net);
            _root = new GameObject("Vista cercana").transform; _root.position = _origin;
            // luz propia (el mapa tiene la suya; esta solo importa dentro de la vista)
            var lg = new GameObject("Sol"); lg.transform.SetParent(_root, false); _sun = lg.AddComponent<Light>(); _sun.type = LightType.Directional;
            Apply(_night);
            // cámara
            var cg = new GameObject("Cámara del tren"); cg.transform.SetParent(_root, false); _cam = cg.AddComponent<Camera>(); _cam.orthographic = true; _cam.orthographicSize = _ortho; _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color32(0x60, 0x54, 0x6c, 255); _cam.depth = -5;
            cg.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
            var ad = cg.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); if (ad == null) ad = cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); ad.renderPostProcessing = false;
            RebuildRt();
            BuildWorld();
            var sh = Shader.Find("Hidden/TBD/PaletteQuantize"); if (sh != null) { _pal = new Material(sh); FillPalette(_pal); }
        }

        void Close()
        {
            _on = false; if (_root != null) Destroy(_root.gameObject); if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
        }

        static bool IsNight(NetWorld net) { string c = NetConst.Clock(net.Tick); int h; return int.TryParse(c.Substring(0, 2), out h) && (h < 6 || h >= 20); }

        void Apply(bool night)
        {
            if (_sun == null) return;
            _sun.color = night ? new Color(0.42f, 0.48f, 0.62f) : new Color(1f, 0.97f, 0.92f); _sun.intensity = night ? 0.9f : 1.2f; _sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        void RebuildRt()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            int h = Mathf.Max(90, Screen.height / _factor), w = Mathf.Max(160, Screen.width / _factor);
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, useMipMap = false }; _rt.Create(); if (_cam != null) _cam.targetTexture = _rt;
        }

        static void FillPalette(Material m)
        {
            var pal = new Vector4[32]; for (int i = 0; i < 32; i++) { var c = Hex(PrototypeBootstrapPalette(i)).linear; pal[i] = new Vector4(c.r, c.g, c.b, 1f); }
            m.SetVectorArray("_Palette", pal); m.SetFloat("_PaletteMix", 0.65f); m.SetFloat("_OutlineStrength", 1f); m.SetColor("_OutlineColor", ((Color)new Color32(0x1c, 0x20, 0x0c, 255)).linear); m.SetFloat("_OutlineThreshold", 0.26f);
        }

        static string PrototypeBootstrapPalette(int i) { return PrototypeBootstrap.PaletteHexPublic[i]; }
        static Color Hex(string h) { h = h.TrimStart('#'); return new Color32(System.Convert.ToByte(h.Substring(0, 2), 16), System.Convert.ToByte(h.Substring(2, 2), 16), System.Convert.ToByte(h.Substring(4, 2), 16), 255); }

        // ---------------------------------------------------------------- mundo
        Material Unlit(Color c) { var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_BaseColor", c); return m; }

        GameObject Prim(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Material m, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(t); Destroy(g.GetComponent<Collider>()); g.name = n; g.transform.SetParent(parent != null ? parent : _root, false); g.transform.localPosition = pos; g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = m; g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g;
        }

        GameObject Model(string folder, string file, Vector3 pos, float yaw)
        {
            var prefab = Resources.Load<GameObject>("Models/Vehiculos/tren/" + folder + "/" + file); if (prefab == null) return null;
            var tex = Resources.Load<Texture2D>("Models/Vehiculos/tren/" + folder + "/T_Paleta32");
            var go = Instantiate(prefab, _root); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); if (tex != null) m.SetTexture("_BaseMap", tex); m.SetFloat("_Smoothness", 0f);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
            return go;
        }

        void BuildWorld()
        {
            // suelo (franja: arriba queda el fondo en parallax), balasto y raíles
            Prim(PrimitiveType.Quad, "Suelo", new Vector3(0f, 0f, -3.5f), new Vector3(700f, 16f, 1f), Unlit(new Color32(0x88, 0x88, 0x5c, 255))).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Quad, "Balasto", new Vector3(0f, 0.04f, 0f), new Vector3(700f, 3.6f, 1f), Unlit(new Color32(0x60, 0x5c, 0x48, 255))).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // traviesas: textura que se desplaza (1,6 m por traviesa)
            var tex = new Texture2D(8, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            for (int x = 0; x < 8; x++) tex.SetPixel(x, 0, x < 2 ? new Color32(0x54, 0x38, 0x20, 255) : new Color32(0x60, 0x5c, 0x48, 255)); tex.Apply();
            _sleepMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); _sleepMat.SetTexture("_BaseMap", tex); _sleepMat.SetTextureScale("_BaseMap", new Vector2(700f / 1.6f, 1f));
            Prim(PrimitiveType.Quad, "Traviesas", new Vector3(0f, 0.08f, 0f), new Vector3(700f, 2.6f, 1f), _sleepMat).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var railMat = Unlit(new Color32(0x94, 0x94, 0x8c, 255));
            Prim(PrimitiveType.Cube, "Raíl A", new Vector3(0f, 0.2f, -0.72f), new Vector3(700f, 0.14f, 0.12f), railMat); Prim(PrimitiveType.Cube, "Raíl B", new Vector3(0f, 0.2f, 0.72f), new Vector3(700f, 0.14f, 0.12f), railMat);
            // tren: locomotora y coches (todo mira a +Z en los modelos: giro 90° para avanzar hacia +X)
            string[] cars = { "vagon_v0", "vagon_v0", "vagon_v1", "vagon_v2" }; float x0 = 6f;
            var lo = Model("locomotora", "SM_Locomotora_Cercana_LOD1", new Vector3(x0, 0f, 0f), 90f);
            float cx = x0 - 12f - 11.75f;
            for (int i = 0; i < cars.Length; i++) { Model(cars[i], "SM_Vagon_Cercano_" + cars[i].Substring(7) + "_LOD1", new Vector3(cx, 0f, 0f), 90f); cx -= 23.5f; }
            // postes de catenaria cada 50 m en el lado lejano
            _posts = new Transform[14]; var pp = Resources.Load<GameObject>("Models/Sagunto/poste_catenaria/SM_poste_catenaria_LOD1"); var pt = Resources.Load<Texture2D>("Models/Sagunto/poste_catenaria/T_Paleta32");
            var pm = new Material(Shader.Find("Universal Render Pipeline/Lit")); if (pt != null) pm.SetTexture("_BaseMap", pt); pm.SetFloat("_Smoothness", 0f);
            for (int i = 0; i < _posts.Length; i++)
            {
                if (pp == null) break; var p = Instantiate(pp, _root); p.transform.localRotation = Quaternion.Euler(0f, 270f, 0f); foreach (var r in p.GetComponentsInChildren<Renderer>()) r.sharedMaterial = pm; _posts[i] = p.transform;
            }
            // fondo en parallax: tres capas pegadas a la cámara
            string suf = _night ? "noche" : "dia"; string[] names = { "cielo", "sierra", "colinas" }; _bgMats = new Material[3]; _bg = new GameObject[3];
            float u = 2f * _ortho / 180f; float[] top = { 0f, 8f, 86f }; float[] hpx = { 120f, 56f, 44f };
            for (int i = 0; i < 3; i++)
            {
                var t = Resources.Load<Texture2D>("Icons/tren/bg_tren_" + names[i] + "_" + suf); if (t == null) continue; t.wrapMode = TextureWrapMode.Repeat; t.filterMode = FilterMode.Point;
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetTexture("_BaseMap", t); m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); _bgMats[i] = m;
                var g = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(g.GetComponent<Collider>()); g.name = "Fondo " + names[i]; g.transform.SetParent(_cam.transform, false);
                g.transform.localPosition = new Vector3(0f, _ortho - (top[i] + hpx[i] * 0.5f) * u, 150f - i * 10f); g.transform.localScale = new Vector3(320f * u, hpx[i] * u, 1f);   // 320 px visibles de los 640 (el resto es el desplazamiento)
                m.SetTextureScale("_BaseMap", new Vector2(0.5f, 1f)); g.GetComponent<MeshRenderer>().sharedMaterial = m; g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; _bg[i] = g;
            }
        }

        // ---------------------------------------------------------------- bucle
        void Update()
        {
            if (!_on || _train == null) return;
            var kb = Keyboard.current; float dt = Time.unscaledDeltaTime;
            if (kb != null && (kb.vKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) { Close(); return; }
            if (kb != null) { if (kb.zKey.isPressed) _ortho = Mathf.Max(4f, _ortho - 4f * dt); if (kb.xKey.isPressed) _ortho = Mathf.Min(12f, _ortho + 4f * dt); }
            var mouse = Mouse.current; if (mouse != null) { float w = mouse.scroll.ReadValue().y; if (w != 0f) _ortho = Mathf.Clamp(_ortho * Mathf.Pow(0.85f, w / (Mathf.Abs(w) >= 10f ? 120f : 1f)), 4f, 12f); }
            float target = _train.State == RailTrainState.EnMarcha ? 20f : 0f; _speed = Mathf.MoveTowards(_speed, target, 3f * dt); _s += _speed * dt;
            _cam.orthographicSize = _ortho;
            _cam.transform.localPosition = new Vector3(0f, 1.5f, 6.5f) - _cam.transform.forward * 60f;
            // el pixelado sigue al zoom: ~13 px por metro como en la zona
            int want = Mathf.Clamp(Mathf.RoundToInt(Screen.height / (24f * _ortho)), 3, 6); if (want != _factor) { _factor = want; RebuildRt(); }
            // traviesas y postes se desplazan hacia -X
            if (_sleepMat != null) _sleepMat.SetTextureOffset("_BaseMap", new Vector2(Mathf.Repeat(_s / 1.6f, 1f), 0f));
            for (int i = 0; i < _posts.Length; i++) if (_posts[i] != null) { float x = Mathf.Repeat(i * 50f - _s + 350f, 700f) - 350f; _posts[i].localPosition = new Vector3(x, 0f, 3.4f); }
            // parallax: desplazamiento horizontal en px por factor de la velocidad del suelo
            float pxPerM = 180f / (2f * _ortho);
            for (int i = 0; i < 3; i++) if (_bgMats != null && _bgMats[i] != null) _bgMats[i].SetTextureOffset("_BaseMap", new Vector2(Mathf.Repeat(_s * _bgFactor[i] * pxPerM / 640f, 1f), 0f));
        }

        void OnGUI()
        {
            if (!_on || _rt == null) return;
            GUI.depth = -1000;
            if (Event.current.type == EventType.Repaint)
            {
                if (_pal != null) Graphics.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _rt, _pal); else GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _rt);
            }
            float k = Screen.height / 1080f; var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * k), richText = true }; st.normal.textColor = new Color32(0xd4, 0xd0, 0xcc, 255);
            var o = GUI.color; GUI.color = new Color(0.07f, 0.08f, 0.04f, 0.85f); GUI.DrawTexture(new Rect(0, 0, Screen.width, 52 * k), Texture2D.whiteTexture); GUI.color = o;
            GUI.Label(new Rect(20 * k, 8 * k, Screen.width, 40 * k), $"<b>Vista cercana</b> · {_name} · {_speed * 3.6f:0} km/h · V o Esc: volver al mapa · Z X / rueda: zoom", st);
        }
    }
}
