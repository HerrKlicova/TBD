using TBD.Net;
using TBD.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// F0 del plan del núcleo (Eugenio, 9-nov 22:00; doc «Núcleo del juego»): el gesto. Se selecciona un tren o los NH90 de Bétera y se da la orden con un clic
    /// derecho en el mapa, sin paneles. La selección vive en NetSession (sobrevive a los cambios de escena). Mientras la zona no tenga su selección (Marcos),
    /// en el mapa se selecciona con clic izquierdo en el icono del tren o en la H de Bétera. Clic derecho a ≤ 40 px de una estación: ese es el destino. El tren
    /// solo sale si puede llegar; si no, el motivo sale junto al cursor. Un punto libre del campo llega en F1 (solo NH90). Esc quita la selección.
    /// </summary>
    public sealed class OrdenesMapa : MonoBehaviour
    {
        public enum Sel : byte { Nada, Tren, NH90 }
        public static Sel Tipo; public static int Id = -1;     // tren: índice del servicio; NH90: id de la aeronave en AerodromoEstado
        public const float RadioEstacionPx = 40f, RadioIconoPx = 24f;

        static OrdenesMapa _i;
        string _msg; float _msgHasta; Vector2 _msgPos;
        Vector2 _derPos; float _derT = -1f; Vector2 _izqPos; float _izqT = -1f;
        GUIStyle _st; Texture2D _px;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear() { if (_i != null) return; var go = new GameObject("TBD_OrdenesMapa"); DontDestroyOnLoad(go); _i = go.AddComponent<OrdenesMapa>(); }

        static NetWorld W { get { return NetSession.A; } }
        static bool EnMapa { get { return SceneManager.GetActiveScene().name == "Union"; } }

        public static void Seleccionar(Sel t, int id, string porQue)
        {
            Tipo = t; Id = id; Debug.Log($"[ORDEN] seleccionado {t} {id} ({porQue})");
        }

        void Aviso(string m, Vector2 guiPos) { _msg = m; _msgHasta = Time.unscaledTime + 2.8f; _msgPos = guiPos; Debug.Log("[ORDEN] " + m); }

        void Update()
        {
            if (!EnMapa || W == null) return;
            if (!SeleccionGlobal.Vacia)      // F0: lo seleccionado en la zona (NH90 en Bétera, tren en el andén) sigue seleccionado en el mapa (Marcos, marcos/f0-zona)
            {
                if (SeleccionGlobal.Tren >= 0 && SeleccionGlobal.Tren < W.Services.Count) Seleccionar(Sel.Tren, SeleccionGlobal.Tren, "seleccionado en la zona");
                else if (SeleccionGlobal.NH90.Count > 0) Seleccionar(Sel.NH90, SeleccionGlobal.NH90[0], "seleccionado en Bétera");
                SeleccionGlobal.Limpiar();
            }
            var ms = UnityEngine.InputSystem.Mouse.current; var kb = UnityEngine.InputSystem.Keyboard.current; if (ms == null) return;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && Tipo != Sel.Nada) { Seleccionar(Sel.Nada, -1, "Esc"); }
            Vector2 p = ms.position.ReadValue();
            if (ms.leftButton.wasPressedThisFrame) { _izqPos = p; _izqT = Time.unscaledTime; }
            if (ms.leftButton.wasReleasedThisFrame && _izqT >= 0f && (p - _izqPos).magnitude < 6f && Time.unscaledTime - _izqT < 0.35f && !TrenPanel.PanelAbierto) ClicIzquierdo(p);
            if (ms.rightButton.wasPressedThisFrame) { _derPos = p; _derT = Time.unscaledTime; }
            if (ms.rightButton.wasReleasedThisFrame && _derT >= 0f && (p - _derPos).magnitude < 6f && Time.unscaledTime - _derT < 0.35f) ClicDerecho(p);
            if (Tipo == Sel.Tren && (Id < 0 || Id >= W.Services.Count)) Seleccionar(Sel.Nada, -1, "el tren ya no existe");
        }

        static OperationsMap3D Mapa() { return FindObjectOfType<OperationsMap3D>(); }
        static Vector2 Pantalla(OperationsMap3D op, double mx, double my) { var c = Camera.main; if (c == null || op == null) return new Vector2(-9999, -9999); var s = c.WorldToScreenPoint(op.MapToWorld(mx, my, 0.3f)); return s.z <= 0 ? new Vector2(-9999, -9999) : new Vector2(s.x, s.y); }

        void ClicIzquierdo(Vector2 p)
        {
            var op = Mapa(); if (op == null) return;
            int mejor = -1; float dm = RadioIconoPx;
            for (int i = 0; i < W.Services.Count; i++) { var c = Camera.main; if (c == null) break; var s = c.WorldToScreenPoint(op.PosTren(i)); if (s.z <= 0) continue; float d = (new Vector2(s.x, s.y) - p).magnitude; if (d < dm) { dm = d; mejor = i; } }
            if (mejor >= 0) { Seleccionar(Sel.Tren, mejor, "clic en el mapa"); Aviso($"{W.Services[mejor].Train.Name}: clic derecho en el destino", Gui(p)); return; }
            var a = AerodromoVista.Estado;
            if (a != null && (Pantalla(op, a.MapX, a.MapY) - p).magnitude < RadioIconoPx)
            {
                int id = -1; foreach (var x in a.Aeronaves) if (x.Estado == AeronaveEstado.EnBase) { id = x.Id; break; }
                if (id < 0) { Aviso("Ningún NH90 listo en Bétera", Gui(p)); return; }
                Seleccionar(Sel.NH90, id, "clic en Bétera"); Aviso($"{a.Aeronaves[id].Nombre}: clic derecho en el destino", Gui(p));
            }
        }

        static Vector2 Gui(Vector2 screen) { return new Vector2(screen.x, Screen.height - screen.y); }

        void ClicDerecho(Vector2 p)
        {
            if (Tipo == Sel.Nada) return;
            var op = Mapa(); if (op == null) return;
            int nodo = -1; float dm = RadioEstacionPx;
            foreach (var n in W.Graph.Nodes) { float d = (Pantalla(op, n.XMeters, n.YMeters) - p).magnitude; if (d < dm) { dm = d; nodo = n.Id; } }
            if (nodo < 0) { Aviso(Tipo == Sel.NH90 ? "Ir a un punto del campo llega más adelante (F1): elige una estación" : "El tren solo va a estaciones", Gui(p)); return; }
            string nom = W.Graph.Nodes[nodo].Name;
            if (Tipo == Sel.Tren)
            {
                string porque = TrenPanel.PorQueNoLlega(Id, nodo);
                if (porque != null) { Aviso(porque, Gui(p)); return; }
                if (TrenPanel.EnviarDesdeMapa(Id, nodo)) { Aviso($"{W.Services[Id].Train.Name} sale hacia {nom}", Gui(p)); Seleccionar(Sel.Nada, -1, "orden dada"); }
                else Aviso("El tren no puede salir ahora", Gui(p));
                return;
            }
            var a = AerodromoVista.Estado; if (a == null || Id < 0 || Id >= a.Aeronaves.Count) { Seleccionar(Sel.Nada, -1, "sin NH90"); return; }
            if (!a.Destinos.ContainsKey(nom)) { Aviso($"El NH90 no tiene ruta a {nom}", Gui(p)); return; }
            string no = a.PorQueNo(Id, nom); if (no != null) { Aviso(no, Gui(p)); return; }
            bool con = AerodromoVista.PelotonesLibres() > 0;
            if (a.Enviar(Id, nom, nodo, con) == null)
            {
                Debug.Log($"[AERO] {a.Aeronaves[Id].Nombre} sale hacia {nom} {(con ? "con" : "sin")} pelotón (orden en el mapa)");
                if (con) TutorialHud.Senal(TutorialGuion.Senales.NH90ConPeloton); TutorialHud.Senal(TutorialGuion.Senales.NH90Enviado);
                Aviso($"{a.Aeronaves[Id].Nombre} sale hacia {nom}", Gui(p)); Seleccionar(Sel.Nada, -1, "orden dada");
            }
        }

        void OnGUI()
        {
            if (!EnMapa || W == null) return;
            if (_st == null) { _st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Screen.height / 60), richText = true }; _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            var op = Mapa();
            if (op != null && Tipo != Sel.Nada && Camera.main != null)      // anillo de selección en hueso
            {
                Vector2 c = new Vector2(-9999, -9999);
                if (Tipo == Sel.Tren && Id >= 0 && Id < W.Services.Count) { var s = Camera.main.WorldToScreenPoint(op.PosTren(Id)); if (s.z > 0) c = new Vector2(s.x, s.y); }
                else if (Tipo == Sel.NH90 && AerodromoVista.Estado != null) c = Pantalla(op, AerodromoVista.Estado.MapX, AerodromoVista.Estado.MapY);
                if (c.x > -9000) { var g = Gui(c); var o = GUI.color; GUI.color = new Color32(0xd4, 0xd0, 0xcc, 0xff); float r = 18f; GUI.DrawTexture(new Rect(g.x - r, g.y - r, 2 * r, 2f), _px); GUI.DrawTexture(new Rect(g.x - r, g.y + r, 2 * r, 2f), _px); GUI.DrawTexture(new Rect(g.x - r, g.y - r, 2f, 2 * r), _px); GUI.DrawTexture(new Rect(g.x + r, g.y - r, 2f, 2 * r + 2f), _px); GUI.color = o; }
            }
            if (Time.unscaledTime < NetSession.AvisoNocheHasta && !string.IsNullOrEmpty(NetSession.AvisoNoche))
            { var sz = _st.CalcSize(new GUIContent(NetSession.AvisoNoche)); var r = new Rect((Screen.width - sz.x) * 0.5f - 8f, Screen.height * 0.12f, sz.x + 16f, sz.y + 8f); var o = GUI.color; GUI.color = new Color(0.07f, 0.08f, 0.05f, 0.9f); GUI.DrawTexture(r, _px); GUI.color = o; GUI.Label(new Rect(r.x + 8f, r.y + 4f, sz.x, sz.y), "<color=#e08c30>" + NetSession.AvisoNoche + "</color>", _st); }
            if (Time.unscaledTime < _msgHasta && !string.IsNullOrEmpty(_msg))
            {
                var sz = _st.CalcSize(new GUIContent(_msg)); var r = new Rect(_msgPos.x + 16f, _msgPos.y + 8f, sz.x + 12f, sz.y + 6f);
                var o = GUI.color; GUI.color = new Color(0.07f, 0.08f, 0.05f, 0.9f); GUI.DrawTexture(r, _px); GUI.color = o;
                GUI.Label(new Rect(r.x + 6f, r.y + 3f, sz.x, sz.y), "<color=#d4d0cc>" + _msg + "</color>", _st);
            }
        }
    }
}
