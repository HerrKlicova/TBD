using System;
using TBD.Tutorial;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Intro con el lore (D-N158), variante C «Día 41» (Mak, 8-nov). Pantalla completa sobre el fondo de Bruno (Resources/Intro/fondo_intro; mientras no esté,
    /// fondo liso #303424). Texto en hueso d4d0cc, nítido y centrado. Se salta con un clic o con la barra espaciadora.
    /// Uso: IntroVista.Antes(seguir) desde «Nueva partida»: si toca intro, la pone y al acabar llama a seguir; si no, llama a seguir enseguida.
    /// Sale solo la primera vez (PlayerPrefs «tbd.intro.vista»). Pruebas: -intro la fuerza, -sinintro la quita.
    /// </summary>
    public sealed class IntroVista : MonoBehaviour
    {
        static readonly Color Dark = new Color32(0x30, 0x34, 0x24, 255), Bone = new Color32(0xd4, 0xd0, 0xcc, 255);
        IntroSecuencia _seq; Action _seguir; GUIStyle _txt, _hint; Texture2D _bg, _fondo; bool _hecho;
        public static bool Mostrando { get; private set; }

        static bool Arg(string a) { return Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0; }

        public static bool TocaIntro()
        {
            if (Arg("-sinintro")) return false;
            if (Arg("-intro")) return true;
            return PlayerPrefs.GetInt("tbd." + IntroSecuencia.ClaveVista, 0) == 0;
        }

        public static void Antes(Action seguir)
        {
            TutorialHud.NuevaPartida();                    // el tutorial arranca al entrar en Sagunto (capítulo 0)
            if (!TocaIntro()) { seguir?.Invoke(); return; }
            var go = new GameObject("TBD_Intro"); DontDestroyOnLoad(go);
            var v = go.AddComponent<IntroVista>(); v._seq = new IntroSecuencia(); v._seguir = seguir; Mostrando = true;
            Debug.Log("[TUTORIAL] intro C: empieza");
        }

        void Update()
        {
            if (_hecho) return;
            _seq.Avanzar(Time.unscaledDeltaTime);
            var kb = UnityEngine.InputSystem.Keyboard.current; var ms = UnityEngine.InputSystem.Mouse.current;
            if ((kb != null && kb.spaceKey.wasPressedThisFrame) || (ms != null && ms.leftButton.wasPressedThisFrame)) { _seq.Saltar(); Debug.Log($"[TUTORIAL] intro saltada a los {_seq.T:0.0} s"); }
            if (TutorialHud.Auto && _seq.T >= 3f && _seq.Indice == 0) TutorialHud.CapturaAuto("intro_1");
            if (TutorialHud.Auto && _seq.T >= 9f && _seq.Indice == 1) TutorialHud.CapturaAuto("intro_2");
            if (TutorialHud.Auto && _seq.T >= 27f && _seq.Indice == 4) TutorialHud.CapturaAuto("intro_5");
            if (_seq.Acabada) Acabar();
        }

        void Acabar()
        {
            _hecho = true; Mostrando = false;
            PlayerPrefs.SetInt("tbd." + IntroSecuencia.ClaveVista, 1); PlayerPrefs.Save();
            Debug.Log("[TUTORIAL] intro C: acabada");
            var s = _seguir; _seguir = null;
            Destroy(gameObject);
            s?.Invoke();
        }

        void OnGUI()
        {
            if (_hecho) return;
            GUI.depth = -200;
            if (_txt == null)
            {
                _bg = new Texture2D(1, 1); _bg.SetPixel(0, 0, Dark); _bg.Apply();
                _fondo = Resources.Load<Texture2D>("Intro/fondo_intro");
                int u = Mathf.Max(10, Screen.height / 54);
                _txt = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 2.3f), alignment = TextAnchor.MiddleCenter, wordWrap = true };
                _txt.normal.textColor = Bone;
                _hint = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 1.0f), alignment = TextAnchor.LowerRight };
                _hint.normal.textColor = new Color(Bone.r, Bone.g, Bone.b, 0.45f);
            }
            var full = new Rect(0, 0, Screen.width, Screen.height);
            GUI.DrawTexture(full, _bg);
            if (_fondo != null) GUI.DrawTexture(full, _fondo, ScaleMode.ScaleAndCrop);
            var prev = GUI.color;
            GUI.color = new Color(1, 1, 1, _seq.Alfa);
            float w = Mathf.Min(Screen.width * 0.7f, Screen.height * 1.4f);
            GUI.Label(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.3f, w, Screen.height * 0.4f), _seq.Texto, _txt);
            GUI.color = prev;
            GUI.Label(new Rect(0, 0, Screen.width - 24, Screen.height - 18), "Clic o Espacio para saltar", _hint);
        }
    }
}
