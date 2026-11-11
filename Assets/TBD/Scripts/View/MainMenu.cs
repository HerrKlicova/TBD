using UnityEngine;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// Menú de inicio (D-N71, «La primera noche en Sagunto»): Nueva partida, Cómo se juega, Opciones y Salir. IMGUI sobre fondo liso con la paleta del juego
    /// (#303424 fondo, #d4d0cc texto, #e08c30 ámbar). La partida arranca cargando la escena Union (mapa de operaciones). «Continuar» no existe todavía:
    /// el guardado (NetSave) está hecho pero sin interfaz; se añade cuando haya un hueco, no se finge.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        enum Page { Principal, Controles, Opciones }
        Page _page = Page.Principal;
        GUIStyle _title, _sub, _btn, _txt, _small; Texture2D _bg, _panel, _btnTex, _btnHot;
        float _volume = 0.8f, _music = 0.8f, _sfx = 0.8f; bool _full;
        public static float MusicVolume = 0.8f, SfxVolume = 0.8f;      // los lee el sonido del juego
        float _t;

        static readonly Color Dark = new Color32(0x30, 0x34, 0x24, 255), Bone = new Color32(0xd4, 0xd0, 0xcc, 255), Amber = new Color32(0xe0, 0x8c, 0x30, 255), Ink = new Color32(0x1c, 0x20, 0x0c, 255);

        void Start()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-mundo") >= 0) { UnityEngine.SceneManagement.SceneManager.LoadScene("Mundo"); return; }   // núcleo v2, Hito 1: prototipo de zum (Marcos)
            Application.targetFrameRate = 60;
            _volume = PlayerPrefs.GetFloat("tbd.vol", 0.8f); AudioListener.volume = _volume;
            _music = MusicVolume = PlayerPrefs.GetFloat("tbd.mus", 0.8f); _sfx = SfxVolume = PlayerPrefs.GetFloat("tbd.sfx", 0.8f);
            _full = Screen.fullScreen;
            var cam = Camera.main; if (cam == null) { var go = new GameObject("Cámara del menú"); cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Dark;
            Sfx.StopAllLoops(); Sfx.Loop("mus_menu", 0.8f, true);
            var cla = System.Environment.GetCommandLineArgs(); if (System.Array.IndexOf(cla, "-stress10k") >= 0 || System.Array.IndexOf(cla, "-stress10k-sagunto") >= 0 || System.Array.IndexOf(cla, "-trainview") >= 0) { NightRun.Active = false; SceneManager.LoadScene("Union"); }   // pruebas de rendimiento: sin la noche
            else if (Auto) StartGame();       // pruebas: arranca directo (-menu-auto; -tutorial-auto lo implica, S3 de Marcos 9-nov)
        }

        static Texture2D Tex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

        void Styles()
        {
            if (_title != null) return;
            _bg = Tex(Dark); _panel = Tex(new Color(0.06f, 0.07f, 0.04f, 0.85f)); _btnTex = Tex(new Color(0.16f, 0.18f, 0.10f, 1f)); _btnHot = Tex(new Color(0.88f, 0.55f, 0.19f, 1f));
            int u = Mathf.Max(10, Screen.height / 54);
            _title = new GUIStyle(GUI.skin.label) { fontSize = u * 5, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; _title.normal.textColor = Bone;
            _sub = new GUIStyle(GUI.skin.label) { fontSize = u * 2, alignment = TextAnchor.MiddleCenter }; _sub.normal.textColor = Amber;
            _txt = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 1.4f), wordWrap = true, richText = true }; _txt.normal.textColor = Bone;
            _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 1.1f), alignment = TextAnchor.MiddleCenter }; _small.normal.textColor = new Color(0.6f, 0.62f, 0.5f);
            _btn = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u * 1.9f), fontStyle = FontStyle.Bold };
            _btn.normal.background = _btnTex; _btn.hover.background = _btnHot; _btn.active.background = _btnHot; _btn.focused.background = _btnTex;
            _btn.normal.textColor = Bone; _btn.hover.textColor = Ink; _btn.active.textColor = Ink; _btn.focused.textColor = Bone;
        }

        string _msg;
        float Slider(Rect r, int row, string label, float v, string key)
        {
            float u = Mathf.Max(10, Screen.height / 54f), yy = r.y + u * 1.0f + u * 3.6f * row;
            GUI.Label(new Rect(r.x + u, yy, u * 14f, u * 2f), label, _txt);
            float nv = GUI.HorizontalSlider(new Rect(r.x + u * 17f, yy + u * 0.5f, u * 16f, u * 2f), v, 0f, 1f);
            if (!Mathf.Approximately(nv, v)) { PlayerPrefs.SetFloat(key, nv); if (key == "tbd.mus") MusicVolume = nv; else if (key == "tbd.sfx") SfxVolume = nv; }
            return nv;
        }

        static bool Auto { get { var a = System.Environment.GetCommandLineArgs(); return System.Array.IndexOf(a, "-menu-auto") >= 0 || System.Array.IndexOf(a, "-tutorial-auto") >= 0 || System.Array.IndexOf(a, "-dia2-auto") >= 0 || System.Array.IndexOf(a, "-socorro-auto") >= 0 || System.Array.IndexOf(a, "-dia2-auto-sinescolta") >= 0 || System.Array.IndexOf(a, "-dia2-auto-tren-aparcado") >= 0 || System.Array.IndexOf(a, "-dia2-auto-enzona") >= 0; } }
        bool _confirmar;      // Lucía (5d21d64): con partida guardada, «Nueva partida» pide confirmar que se borra
        void StartGame() { if (!Auto && NetSession.HasSave && !_confirmar) { _confirmar = true; Sfx.Play("sfx_ui_clic"); return; } _confirmar = false; Sfx.Play("sfx_ui_clic"); Sfx.StopLoop("mus_menu"); NetSession.Reset(); NightRun.Active = true; if (Auto) SceneManager.LoadScene("Union"); else { NuevaPartidaDesdeCero(); IntroVista.Antes(() => SceneManager.LoadScene("Union")); } }
        /// <summary>Eugenio (9-nov): cada «Empezar» es una partida nueva con su intro y su tutorial (los dos se pueden saltar); no quedan apagados para siempre tras verlos o saltarlos una vez.</summary>
        void ConfirmarNueva()
        {
            Styles(); float W = Screen.width, H = Screen.height, u = Mathf.Max(10, H / 54f);
            GUI.DrawTexture(new Rect(0, 0, W, H), _bg); var r = new Rect(W * 0.5f - u * 18f, H * 0.5f - u * 6f, u * 36f, u * 12f); GUI.DrawTexture(r, _panel);
            GUI.Label(new Rect(r.x + u, r.y + u, r.width - 2 * u, u * 5f), "Hay una partida guardada. Si empiezas una nueva, se borrará.", _txt);
            if (GUI.Button(new Rect(r.x + u * 2f, r.yMax - u * 4.5f, u * 14f, u * 3.2f), "Empezar nueva", _btn)) { try { System.IO.File.Delete(NetSession.SavePath); } catch (System.Exception) { } Debug.Log("[MENÚ] partida guardada borrada: nueva partida"); StartGame(); }
            if (GUI.Button(new Rect(r.xMax - u * 16f, r.yMax - u * 4.5f, u * 14f, u * 3.2f), "Cancelar", _btn)) { _confirmar = false; Sfx.Play("sfx_ui_clic"); }
        }
        static void NuevaPartidaDesdeCero()
        {
            foreach (var k in new[] { "tbd.intro.vista", "tbd.tutorial.visto", "tbd.tutorial.visto.saltado", "tbd.tutorial4.visto", "tbd.tutorial4.visto.saltado", "tbd.suelta.c90" }) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save(); Debug.Log("[TUTORIAL] partida nueva: intro y tutorial otra vez");
        }
        void ContinueGame()
        {
            foreach (var k in new[] { "tbd.tutorial.visto", "tbd.tutorial.visto.saltado" }) PlayerPrefs.SetInt(k, 1); PlayerPrefs.Save();   // Lucía: «Continuar» sin intro ni tutorial
            try { NetSession.LoadGame(); NightRun.Active = false; SceneManager.LoadScene("Union"); }
            catch (System.Exception e) { _msg = "No se pudo cargar la partida: " + e.Message; }
        }

        sealed class DatosMenu : TBD.View.UiNoche.IUiNocheDatos
        {
            public string Reloj { get { return "22:00"; } } public float ProgresoNoche { get { return 0f; } } public int Municion { get { return 0; } } public int Materiales { get { return 0; } }
            public int Combustible { get { return 0; } } public int Pila { get { return 0; } } public int Bajas { get { return 0; } } public float PrimeraOleadaEn { get { return -1f; } }
            readonly System.Collections.Generic.List<TBD.View.UiNoche.Aviso> _a = new System.Collections.Generic.List<TBD.View.UiNoche.Aviso>();
            readonly System.Collections.Generic.List<TBD.View.UiNoche.Frente> _f = new System.Collections.Generic.List<TBD.View.UiNoche.Frente>();
            readonly System.Collections.Generic.List<TBD.View.UiNoche.Tarjeta> _t = new System.Collections.Generic.List<TBD.View.UiNoche.Tarjeta>();
            public System.Collections.Generic.IReadOnlyList<TBD.View.UiNoche.Aviso> Avisos { get { return _a; } }
            public System.Collections.Generic.IReadOnlyList<TBD.View.UiNoche.Frente> Frentes { get { return _f; } }
            public System.Collections.Generic.IReadOnlyList<TBD.View.UiNoche.Tarjeta> Tarjetas { get { return _t; } }
            public TBD.View.UiNoche.Parte Parte { get { return default(TBD.View.UiNoche.Parte); } }
            public bool HayPartidaGuardada { get { return NetSession.HasSave; } }
        }
        readonly DatosMenu _datosMenu = new DatosMenu(); bool _dragSlider; int _sliderIdx = -1;

        /// <summary>Menú con el arte de Bruno (kit UiNoche, base 640×360). El kit solo pinta: los clics van con botones transparentes sobre las piezas.</summary>
        bool KitMenu()
        {
            if (UiNoche.UiNocheKit.Tex("ui_menu_fondo_noche") == null) return false;
            Styles();
            UiNoche.UiNocheVistas.OpcVal[0] = _volume; UiNoche.UiNocheVistas.OpcVal[1] = _music; UiNoche.UiNocheVistas.OpcVal[2] = _sfx;
            UiNoche.UiNocheKit.Begin(); UiNoche.UiNocheVistas.Menu(_datosMenu, Time.unscaledTime, _page == Page.Opciones); UiNoche.UiNocheKit.End();
            int e = UiNoche.UiNocheKit.Escala; var o = UiNoche.UiNocheKit.Origen;
            Rect R(float bx, float by, float bw, float bh) { return new Rect(o.x + bx * e, o.y + by * e, bw * e, bh * e); }
            if (_page == Page.Principal)
            {
                bool has = NetSession.HasSave;
                if (GUI.Button(R(24, 120, 132, 20), GUIContent.none, GUIStyle.none)) StartGame();
                if (has && GUI.Button(R(24, 146, 132, 20), GUIContent.none, GUIStyle.none)) ContinueGame();
                if (GUI.Button(R(24, 172, 132, 20), GUIContent.none, GUIStyle.none)) _page = Page.Controles;
                if (GUI.Button(R(24, 198, 132, 20), GUIContent.none, GUIStyle.none)) _page = Page.Opciones;
                if (GUI.Button(R(24, 224, 132, 20), GUIContent.none, GUIStyle.none)) Quit();
                if (!string.IsNullOrEmpty(_msg)) GUI.Label(R(24, 250, 400, 14), _msg, _small);
            }
            else if (_page == Page.Opciones)
            {
                float x = 170, y = 92; var ev = Event.current; var mp = ev.mousePosition;
                for (int i = 0; i < 3; i++)
                {
                    var track = R(x + 130 - 4, y + 34 + i * 20 + 2, 128, 12);
                    if (ev.type == EventType.MouseDown && ev.button == 0 && track.Contains(mp)) { _dragSlider = true; _sliderIdx = i; }
                    if (_dragSlider && _sliderIdx == i && (ev.type == EventType.MouseDrag || ev.type == EventType.MouseDown))
                    {
                        float nv = Mathf.Clamp01((mp.x - R(x + 130, 0, 0, 0).x) / (120f * e));
                        if (i == 0) { _volume = nv; AudioListener.volume = nv; PlayerPrefs.SetFloat("tbd.vol", nv); } else if (i == 1) { _music = MusicVolume = nv; PlayerPrefs.SetFloat("tbd.mus", nv); } else { _sfx = SfxVolume = nv; PlayerPrefs.SetFloat("tbd.sfx", nv); }
                    }
                }
                if (ev.type == EventType.MouseUp) _dragSlider = false;
                if (GUI.Button(R(x + 130, y + 118, 18, 16), GUIContent.none, GUIStyle.none)) { _full = !_full; Screen.fullScreen = _full; }
                GUI.Label(R(x, y + 134, 260, 14), "×1 al primer ataque de cada noche: " + (NetSession.AutoX1 ? "sí" : "no") + "  (clic para cambiar)", _small);      // D-N186 (Lucía): se puede desactivar
                if (GUI.Button(R(x, y + 134, 260, 14), GUIContent.none, GUIStyle.none)) NetSession.AutoX1 = !NetSession.AutoX1;
                if (GUI.Button(R(x + 102, y + 148, 90, 18), GUIContent.none, GUIStyle.none)) { PlayerPrefs.Save(); _page = Page.Principal; }
                if (GUI.Button(R(x + 200, y + 148, 90, 18), GUIContent.none, GUIStyle.none)) _page = Page.Principal;
            }
            else
            {
                var r = R(150, 90, 340, 200); GUI.Box(r, GUIContent.none); GUI.Label(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20),
                    "<b>En la zona</b>\nclic o arrastre: elegir · clic derecho: mover · A+clic: atacar moviéndose · S: mantener · D: emplazar · G: alto el fuego · T: recargar\n" +
                    "cámara: flechas o bordes · Z acerca · X aleja · botón derecho arrastrando: girar · R F: inclinar · F2: depuración · F5: guardar\n" +
                    "<b>Mapa</b>: V vista cercana del tren.\n<b>Objetivo</b>: resiste hasta el amanecer.", _txt);
                if (GUI.Button(R(250, 296, 140, 20), "Volver")) _page = Page.Principal;
            }
            return true;
        }

        void OnGUI()
        {
            if (_confirmar) { ConfirmarNueva(); return; }
            if (KitMenu()) return;
            Styles(); _t += Time.unscaledDeltaTime;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bg);
            float W = Screen.width, H = Screen.height, u = Mathf.Max(10, H / 54f);
            // franja ámbar fina bajo el título (el contorno de la paleta, sin degradados)
            GUI.Label(new Rect(0, H * 0.10f, W, u * 6f), "VÍA MUERTA", _title);
            GUI.Label(new Rect(0, H * 0.10f + u * 6f, W, u * 3f), "La primera noche en Sagunto", _sub);
            GUI.DrawTexture(new Rect(W * 0.5f - u * 14f, H * 0.10f + u * 9.4f, u * 28f, Mathf.Max(2f, u * 0.25f)), _btnHot);

            float bw = u * 22f, bh = u * 3.6f, x = W * 0.5f - bw * 0.5f, y = H * 0.36f, gap = u * 0.9f;
            switch (_page)
            {
                case Page.Principal:
                    if (GUI.Button(new Rect(x, y, bw, bh), "JUGAR", _btn)) StartGame();
                    bool has = TBD.Net.NetSave.FormatVersion > 0 && NetSession.HasSave;
                    GUI.enabled = has;
                    if (GUI.Button(new Rect(x, y + (bh + gap), bw, bh), "CONTINUAR", _btn)) ContinueGame();
                    GUI.enabled = true;
                    if (GUI.Button(new Rect(x, y + (bh + gap) * 2f, bw, bh), "CÓMO SE JUEGA", _btn)) _page = Page.Controles;
                    if (GUI.Button(new Rect(x, y + (bh + gap) * 3f, bw, bh), "OPCIONES", _btn)) _page = Page.Opciones;
                    if (GUI.Button(new Rect(x, y + (bh + gap) * 4f, bw, bh), "SALIR", _btn)) Quit();
                    if (!string.IsNullOrEmpty(_msg)) GUI.Label(new Rect(0, y + (bh + gap) * 5f, W, u * 2f), _msg, _small);
                    break;
                case Page.Controles:
                {
                    var r = new Rect(W * 0.5f - u * 26f, y - u * 2f, u * 52f, u * 24f);
                    GUI.DrawTexture(r, _panel);
                    GUI.Label(new Rect(r.x + u, r.y + u * 0.6f, r.width - u * 2f, r.height - u),
                        "<b>Mapa de operaciones</b>\nClic en un nodo: ir allí. Rueda: zoom. WASD: mover. Clic en un tren: seleccionarlo.\n\n" +
                        "<b>En la zona</b>\nWASD / flechas: mover la cámara · botón dcho.: girar · R F: inclinar · rueda: zoom (cada muesca 15 %)\n" +
                        "Alejar del todo y una muesca más: aviso; otra seguida: vuelves al mapa (M también)\n" +
                        "7 / 8 / 9: hora (día, tarde, noche) · [ ]: pixelado · , .: paleta · F9: captura · F10: grabar\n" +
                        "N: asignar o quitar obreros · B: modo obra (clic para poner muros) · H: cambia de pieza\n\n" +
                        "<b>Objetivo</b>\nResiste la noche: la horda llega por el oeste. Protege el tren y la estación hasta el amanecer.", _txt);
                    if (GUI.Button(new Rect(x, r.yMax + u, bw, bh), "VOLVER", _btn)) _page = Page.Principal;
                    break;
                }
                case Page.Opciones:
                {
                    var r = new Rect(W * 0.5f - u * 18f, y - u * 1f, u * 36f, u * 15f);
                    GUI.DrawTexture(r, _panel);
                    _volume = Slider(r, 0, "Volumen general", _volume, "tbd.vol"); AudioListener.volume = _volume;
                    _music = Slider(r, 1, "Música", _music, "tbd.mus");
                    _sfx = Slider(r, 2, "Efectos", _sfx, "tbd.sfx");
                    GUI.Label(new Rect(r.x + u, r.y + u * 1.0f + u * 3.6f * 3f, u * 14f, u * 2f), "Pantalla completa", _txt);
                    bool nf = GUI.Toggle(new Rect(r.x + u * 17f, r.y + u * 1.0f + u * 3.6f * 3f, u * 16f, u * 2f), _full, "  sí");
                    if (nf != _full) { _full = nf; Screen.fullScreen = nf; }
                    bool nx = GUI.Toggle(new Rect(r.x + u, r.y + u * 1.0f + u * 3.6f * 3.6f, u * 32f, u * 2f), NetSession.AutoX1, "  ×1 al primer ataque de cada noche");      // D-N186
                    if (nx != NetSession.AutoX1) NetSession.AutoX1 = nx;
                    if (GUI.Button(new Rect(x, r.yMax + u, bw, bh), "VOLVER", _btn)) _page = Page.Principal;
                    break;
                }
            }
            GUI.Label(new Rect(0, H - u * 3f, W, u * 2f), "prototipo · pixel art 3D · Unity 6 + DOTS", _small);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        float _shotT = -1f;
        void Update()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-menu-shot") >= 0)       // QA: capturas del menu a los 6 s (principal) y 12 s (opciones)
            {
                if (_shotT < 0f) _shotT = Time.realtimeSinceStartup; float tt = Time.realtimeSinceStartup - _shotT;
                if (tt > 6f && _shotN == 0) { _shotN = 1; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "..", "Captures", "menu_principal.png")); }
                if (tt > 8f && _shotN == 1) { _shotN = 2; _page = Page.Opciones; }
                if (tt > 12f && _shotN == 2) { _shotN = 3; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "..", "Captures", "menu_opciones.png")); }
            }
            UpdateInput();
        }
        int _shotN;
        void UpdateInput()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && _page != Page.Principal) _page = Page.Principal;
            if (kb != null && _page == Page.Principal && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) StartGame();
        }
    }
}
