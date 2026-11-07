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
            Application.targetFrameRate = 60;
            _volume = PlayerPrefs.GetFloat("tbd.vol", 0.8f); AudioListener.volume = _volume;
            _music = MusicVolume = PlayerPrefs.GetFloat("tbd.mus", 0.8f); _sfx = SfxVolume = PlayerPrefs.GetFloat("tbd.sfx", 0.8f);
            _full = Screen.fullScreen;
            var cam = Camera.main; if (cam == null) { var go = new GameObject("Cámara del menú"); cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Dark;
            Sfx.StopAllLoops(); Sfx.Loop("mus_menu", 0.8f, true);
            var cla = System.Environment.GetCommandLineArgs(); if (System.Array.IndexOf(cla, "-stress10k") >= 0 || System.Array.IndexOf(cla, "-stress10k-sagunto") >= 0) { NightRun.Active = false; SceneManager.LoadScene("Union"); }   // pruebas de rendimiento: sin la noche
            else if (System.Array.IndexOf(cla, "-menu-auto") >= 0) StartGame();       // pruebas: arranca directo
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

        void StartGame() { Sfx.Play("sfx_ui_clic"); Sfx.StopLoop("mus_menu"); NetSession.Reset(); NightRun.Active = true; SceneManager.LoadScene("Union"); }
        void ContinueGame()
        {
            try { NetSession.LoadGame(); NightRun.Active = false; SceneManager.LoadScene("Union"); }
            catch (System.Exception e) { _msg = "No se pudo cargar la partida: " + e.Message; }
        }

        void OnGUI()
        {
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

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && _page != Page.Principal) _page = Page.Principal;
            if (kb != null && _page == Page.Principal && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) StartGame();
        }
    }
}
