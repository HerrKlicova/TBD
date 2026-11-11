using TBD.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// D-N203 (Lucía): cuando una estación cae fuera de pantalla, la red lo apunta en el mismo tick (NetWorld.UltimaCaidaNodo / UltimaCaidaTick, de Nacho)
    /// y aquí, esté donde esté el jugador (mapa o zona), sale el cartel «{Estación} ha caído» con Seguir, Repetir la noche (si hay guardado del atardecer)
    /// y Menú. La cámara no salta. Solo vista: no toca la simulación. La partida solo acaba si no queda ninguna estación principal (D-N133), y eso lo decide la red.
    /// </summary>
    public sealed class CaidaCartel : MonoBehaviour
    {
        static CaidaCartel _i;
        long _visto = -1; int _nodo = -1; string _nombre;
        GUIStyle _titulo, _texto; Texture2D _px;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_i != null) return;
            var go = new GameObject("TBD_CaidaCartel"); DontDestroyOnLoad(go); _i = go.AddComponent<CaidaCartel>();
        }

        void Update()
        {
            var w = NetSession.A; if (w == null) { _visto = -1; _nodo = -1; return; }
            if (w.UltimaCaidaTick < 0 || w.UltimaCaidaTick == _visto) return;
            _visto = w.UltimaCaidaTick; int n = w.UltimaCaidaNodo;
            if (n < 0 || n >= w.Graph.Nodes.Count) return;
            _nodo = n; _nombre = w.Graph.Nodes[n].Name;
            Debug.Log($"[CAIDA] {_nombre} ha caído fuera de pantalla (tick {w.UltimaCaidaTick}): cartel con Seguir, Repetir la noche y Menú (D-N203)");
            try { Sfx.Play("sfx_ui_alerta"); } catch (System.Exception) { }
        }

        void OnGUI()
        {
            if (_nodo < 0) return;
            GUI.depth = -100;
            float k = Mathf.Max(0.6f, Screen.height / 1080f);
            if (_titulo == null)
            {
                _titulo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontStyle = FontStyle.Bold };
                _texto = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
                _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply();
            }
            _titulo.fontSize = Mathf.RoundToInt(40 * k); _texto.fontSize = Mathf.RoundToInt(22 * k);
            float w = Mathf.Min(Screen.width - 32f, 760 * k), h = 300 * k; var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.42f, w, h);
            var old = GUI.color; GUI.color = new Color(0.06f, 0.07f, 0.1f, 0.94f); GUI.DrawTexture(r, _px); GUI.color = old;
            GUI.Label(new Rect(r.x, r.y + 24 * k, r.width, 60 * k), $"<color=#982420>{_nombre} ha caído.</color>", _titulo);
            GUI.Label(new Rect(r.x + 30 * k, r.y + 96 * k, r.width - 60 * k, 70 * k), "<color=#d4d0cc>Se pierden su almacén, su guarnición y los trenes de su andén.</color>", _texto);
            bool atardecer = NetSession.HayAtardecer; int n = atardecer ? 3 : 2;
            float bw = 210 * k, gap = 18 * k, bx = r.center.x - (n * bw + (n - 1) * gap) * 0.5f, by = r.yMax - 92 * k;
            if (GUI.Button(new Rect(bx, by, bw, 60 * k), "<b>Seguir</b>") || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)) { Debug.Log("[CAIDA] Seguir"); _nodo = -1; return; }
            bx += bw + gap;
            if (atardecer) { if (GUI.Button(new Rect(bx, by, bw, 60 * k), "Repetir la noche")) { Debug.Log("[CAIDA] Repetir la noche"); _nodo = -1; NetSession.CargarAtardecer(); NightRun.Active = true; SceneManager.LoadScene("Union"); return; } bx += bw + gap; }
            if (GUI.Button(new Rect(bx, by, bw, 60 * k), "Menú principal")) { Debug.Log("[CAIDA] Menú"); _nodo = -1; NetSession.Reset(); NightRun.Active = false; SceneManager.LoadScene("Menu"); }
        }
    }
}
