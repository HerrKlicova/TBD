using System;
using System.Collections.Generic;
using TBD.Tutorial;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Intro con el lore (D-N158), variante C «Día 41» (Mak, 8-nov), sobre el fondo A de Bruno, «La ciudad apagada» (Resources/Intro/fondo_intro, f608880).
    /// Petición de Eugenio (8-nov): el texto se escribe letra a letra como en una máquina de escribir, con el tecleo de Nuria (sfx_ui_tecla_*), en letra
    /// gruesa y grande, dentro del fondo: en la banda navy casi lisa que dejó Bruno para el texto (22–76 % de alto, 8–92 % de ancho; hueso sobre navy, 11:1).
    /// Clic o Espacio: completa la tarjeta o pasa a la siguiente. Esc: salta la intro entera.
    /// Uso: IntroVista.Antes(seguir) desde «Nueva partida»: si toca intro, la pone y al acabar llama a seguir; si no, llama a seguir enseguida.
    /// Sale solo la primera vez (PlayerPrefs «tbd.intro.vista»). Pruebas: -intro la fuerza, -sinintro la quita.
    /// </summary>
    public sealed class IntroVista : MonoBehaviour
    {
        static readonly Color Dark = new Color32(0x30, 0x34, 0x24, 255), Bone = new Color32(0xd4, 0xd0, 0xcc, 255);
        IntroSecuencia _seq; Action _seguir; GUIStyle _txt, _hint; Texture2D _bg; Texture2D[] _fondos; bool _hecho; int _sh = -1;
        // fondo de cada tarjeta (Lucía, con los de Bruno d8b1641): D faro que se acerca, A la ciudad apagada, B catenaria, C Sagunto desde fuera (4 y 5).
        // Si falta alguno se usa el A. El cambio cae entre tarjetas, con el texto en alfa 0.
        static readonly string[] FondoDe = { "Intro/fondo_intro_d", "Intro/fondo_intro", "Intro/fondo_intro_b", "Intro/fondo_intro_c", "Intro/fondo_intro_c" };
        readonly List<string> _snd = new List<string>();
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

        /// <summary>Toca los sonidos de la máquina (2D, bajos, Maquina.Volumen). Sin pausa mínima propia: la separación de 25 ms ya la pone Maquina.</summary>
        public static void Sonar(List<string> sonidos)
        {
            foreach (var s in sonidos)
            {
                if (s == Maquina.Retorno) Sfx.Play(s, Maquina.VolumenRetorno, 0f);
                else if (s == Maquina.Campanilla) Sfx.Play(s, Maquina.Volumen, 0f);
                else Sfx.Play(s, Maquina.Volumen, 0f, 1f + UnityEngine.Random.Range(-Maquina.Tono, Maquina.Tono));   // ±3 % de tono (Nuria)
            }
            sonidos.Clear();
        }

        bool _pregunta;      // Lucía (5d21d64): tras la intro, «¿Quieres hacer el tutorial?» Sí / No (foco en Sí)
        void Update()
        {
            if (_hecho) return;
            if (_pregunta)
            {
                var k2 = UnityEngine.InputSystem.Keyboard.current;
                if (TutorialHud.Auto) Responder(true);
                else if (k2 != null && (k2.enterKey.wasPressedThisFrame || k2.numpadEnterKey.wasPressedThisFrame || k2.sKey.wasPressedThisFrame)) Responder(true);
                else if (k2 != null && (k2.escapeKey.wasPressedThisFrame || k2.nKey.wasPressedThisFrame)) Responder(false);
                return;
            }
            var kb = UnityEngine.InputSystem.Keyboard.current; var ms = UnityEngine.InputSystem.Mouse.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { _seq.Saltar(); Debug.Log($"[TUTORIAL] intro saltada a los {_seq.T:0.0} s"); }
            else if ((kb != null && kb.spaceKey.wasPressedThisFrame) || (ms != null && ms.leftButton.wasPressedThisFrame)) _seq.Siguiente(_snd);
            int antes = _seq.Indice;
            _seq.Avanzar(Mathf.Min(Time.unscaledDeltaTime, 0.1f), _snd);   // un fotograma largo (carga de escena) no se come la tarjeta
            Sonar(_snd);
            if (TutorialHud.Auto && !_seq.Acabada)
            {
                if (_seq.Maquina.Visibles > 0 && !_seq.Maquina.Completa && _seq.Maquina.Visibles >= _seq.Maquina.Texto.Length / 2) TutorialHud.CapturaAuto($"intro_{_seq.Indice + 1}_escribiendo");
                if (_seq.Maquina.Completa && _seq.Alfa >= 1f) TutorialHud.CapturaAuto($"intro_{_seq.Indice + 1}");
            }
            if (_seq.Acabada) { _pregunta = true; Debug.Log("[TUTORIAL] intro acabada: pregunta del tutorial"); }
        }

        void Responder(bool si)
        {
            _pregunta = false; Sfx.Play("sfx_ui_clic");
            foreach (var k in new[] { "tbd.tutorial.visto", "tbd.tutorial.visto.saltado" }) { if (si) PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, 1); }
            PlayerPrefs.Save(); Debug.Log("[TUTORIAL] ¿tutorial?: " + (si ? "sí" : "no"));
            Acabar();
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
            if (_txt == null || _sh != Screen.height)
            {
                _sh = Screen.height;
                if (_bg == null)
                {
                    _bg = new Texture2D(1, 1); _bg.SetPixel(0, 0, Dark); _bg.Apply();
                    var a = Resources.Load<Texture2D>("Intro/fondo_intro");
                    _fondos = new Texture2D[FondoDe.Length]; for (int i = 0; i < FondoDe.Length; i++) _fondos[i] = Resources.Load<Texture2D>(FondoDe[i]) ?? a;
                }
                int u = Mathf.Max(10, Screen.height / 54);
                _txt = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 3.1f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, clipping = TextClipping.Overflow };
                _txt.normal.textColor = Bone;
                _hint = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 1.0f), alignment = TextAnchor.LowerRight };
                _hint.normal.textColor = new Color(Bone.r, Bone.g, Bone.b, 0.55f);
            }
            var full = new Rect(0, 0, Screen.width, Screen.height);
            GUI.DrawTexture(full, _bg);
            int fi = Mathf.Clamp(_seq.Acabada ? 0 : _seq.Indice, 0, _fondos.Length - 1);
            if (_fondos[fi] != null) GUI.DrawTexture(full, _fondos[fi], ScaleMode.ScaleAndCrop);
            // banda de texto de Bruno: 8–92 % de ancho, 22–76 % de alto. El texto se coloca con el tamaño del texto ENTERO, para que no salte al escribirse.
            var band = new Rect(Screen.width * 0.08f, Screen.height * 0.22f, Screen.width * 0.84f, Screen.height * 0.54f);
            float w = Mathf.Min(band.width, Screen.height * 1.25f);
            var r = new Rect(band.x + (band.width - w) * 0.5f, band.y, w, band.height);
            string entero = _seq.TextoEntero ?? "", parcial = _seq.Texto ?? "";
            var prev = GUI.color; GUI.color = new Color(1, 1, 1, _seq.Alfa);
            // las letras que faltan van transparentes: el bloque ya ocupa su sitio final y las líneas no se recolocan
            string conHueco = parcial + "<color=#00000000>" + entero.Substring(Math.Min(parcial.Length, entero.Length)) + "</color>";
            _txt.richText = true;
            GUI.Label(r, conHueco, _txt);
            GUI.color = prev;
            if (_pregunta)
            {
                float u = Mathf.Max(10, Screen.height / 54f), pw = u * 30f, ph = u * 11f; var pr = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
                var po = GUI.color; GUI.color = new Color(0.07f, 0.08f, 0.05f, 0.95f); GUI.DrawTexture(pr, Texture2D.whiteTexture); GUI.color = po;
                var qs = new GUIStyle(_txt) { fontSize = Mathf.RoundToInt(u * 2.0f) }; GUI.Label(new Rect(pr.x, pr.y + u, pw, u * 4f), "¿Quieres hacer el tutorial?", qs);
                var bs = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u * 1.8f), fontStyle = FontStyle.Bold };
                GUI.SetNextControlName("tut_si"); if (GUI.Button(new Rect(pr.x + u * 3f, pr.yMax - u * 5f, u * 10f, u * 3.2f), "Sí", bs)) Responder(true);
                if (GUI.Button(new Rect(pr.xMax - u * 13f, pr.yMax - u * 5f, u * 10f, u * 3.2f), "No", bs)) Responder(false);
                GUI.FocusControl("tut_si");
                return;
            }
            GUI.Label(new Rect(0, 0, Screen.width - 24, Screen.height - 18), "Clic o Espacio: seguir · Esc: saltar", _hint);
            { float u = Mathf.Max(10, Screen.height / 54f); var bs = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u * 1.2f) };
              if (GUI.Button(new Rect(Screen.width - u * 9f, u * 1.2f, u * 7.5f, u * 2.4f), "Saltar", bs)) { _seq.Saltar(); Debug.Log($"[TUTORIAL] intro saltada con el botón a los {_seq.T:0.0} s"); } }
        }
    }
}
