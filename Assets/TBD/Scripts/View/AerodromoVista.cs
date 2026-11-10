using System;
using System.Collections.Generic;
using TBD.Net;
using TBD.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// Bétera en el mapa de operaciones (D-N173). Un objeto que sobrevive a los cambios de escena: lleva el reloj de los NH90 (AerodromoEstado) en el mapa y en
    /// la zona, y en el mapa dibuja el nodo del aeródromo y su panel: existencias propias, guarnición, los NH90 con su estado y su ETA, «con pelotón», destino
    /// con la distancia real y «Enviar». Cuando un NH90 llega a una estación, queda en Llegadas para que la zona (Marcos) lo haga entrar por el borde con
    /// RumboLlegada; cuando la zona ve que baja del 30 %, llama a VolverARearmar. Abrir el panel por primera vez dispara el capítulo 4 del tutorial.
    /// PROTOTIPO: el estado vive aquí (sesión), no en la red. Sin ventana en las pruebas: se comprueba por dotnet y compilando en batch.
    /// </summary>
    public sealed class AerodromoVista : MonoBehaviour
    {
        public struct Llegada { public int Aero; public string Nombre; public int Nodo; public int RumboLlegada; public bool ConPeloton; }

        static AerodromoVista _i;
        static AerodromoEstado _a;
        GUIStyle _sLinea;      // D-N185: rótulo de Bétera en una línea
        public static AerodromoEstado Estado { get { if (_a == null) Cargar(); return _a; } }
        public static readonly Queue<Llegada> Llegadas = new Queue<Llegada>();   // la zona las saca al entrar o mientras está en ella
        static string _aviso; static float _avisoHasta;
        readonly DobleClic _dobleClic = new DobleClic();
        bool _abierto; int _sel; bool _conPeloton = true; string _destino;
        GUIStyle _t, _s, _b, _bOff; Texture2D _panel, _btn, _btnHot, _btnOff; int _sh;
        static readonly Color Bone = new Color32(0xd4, 0xd0, 0xcc, 255), Ink = new Color32(0x1c, 0x20, 0x0c, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GoldenRunner.Active || _i != null) return;
            var go = new GameObject("TBD_Aerodromo"); DontDestroyOnLoad(go); _i = go.AddComponent<AerodromoVista>();
        }

        static void Cargar()
        {
            string j = BalanceSource.Read("estacion_betera.json");
            try { _a = j != null ? AerodromoEstado.FromJson(j) : null; } catch (Exception e) { Debug.LogWarning("[AERO] estacion_betera.json no vale: " + e.Message); _a = null; }
            if (_a != null && !_a.AplicarExistencias(BalanceSource.Read("existencias_red.json"))) Debug.Log("[AERO] sin existencias_red.json: 50 u y 70 u por defecto");
            if (_a == null) Debug.LogWarning("[AERO] sin datos de Bétera");
        }

        /// <summary>Nueva partida: Bétera vuelve a su estado inicial.</summary>
        /// <summary>F0: pelotones de embarque libres en Bétera (un NH90 sale con pelotón si hay alguno).</summary>
        public static int PelotonesLibres() { var a = Estado; if (a == null) return 0; int n = 0; foreach (var b in a.PelotonEnBase) if (b) n++; return n; }
        public static void Reiniciar() { _a = null; Llegadas.Clear(); }

        /// <summary>La zona: el NH90 ha bajado del 30 % de munición (D-N173). Vuelve solo a Bétera con su ETA.</summary>
        public static bool VolverARearmar(int aero) { bool ok = Estado != null && Estado.Volver(aero); if (ok) Debug.Log($"[AERO] {Estado.Aeronaves[aero].Nombre} vuelve a Bétera a rearmar (ETA {AerodromoEstado.Mmss(Estado.Eta(aero))})"); return ok; }
        /// <summary>La zona: el pelotón del NH90 ya está en tierra.</summary>
        public static void PelotonEnTierra(int aero) { Estado?.PelotonBajado(aero); TutorialHud.Senal(TutorialGuion.Senales.NH90Descargado); }

        /// <summary>«0:12» al primer NH90 en camino (para el «{eta}» del tutorial).</summary>
        public static string EtaTexto()
        {
            var a = Estado; if (a == null) return "—";
            foreach (var x in a.Aeronaves) if (x.Estado == AeronaveEstado.Preparando || x.Estado == AeronaveEstado.Ida) return AerodromoEstado.Mmss(a.Eta(x.Id));
            return "—";
        }

        void Update()
        {
            var a = Estado; var w = NetSession.A; if (a == null) return;
            foreach (var ev in a.Avanzar(Time.unscaledDeltaTime))
            {
                var x = a.Aeronaves[ev.Aero];
                if (ev.Nuevo == AeronaveEstado.EnZona && w != null && ev.Destino >= 0)
                {
                    string dest = w.Graph.Nodes[ev.Destino].Name; a.Destinos.TryGetValue(dest, out var d);
                    Llegadas.Enqueue(new Llegada { Aero = x.Id, Nombre = x.Nombre, Nodo = ev.Destino, RumboLlegada = d != null ? d.RumboLlegada : 0, ConPeloton = x.Peloton >= 0 });
                    Aviso($"{x.Nombre} llega a {dest}"); TutorialHud.Senal(TutorialGuion.Senales.NH90Llega);
                    if (x.CargaMilliU > 0) { int u = x.CargaMilliU / 1000; x.CargaMilliU = 0; PrototypeBootstrap.EntregaAerea(ev.Destino, u); Aviso($"{x.Nombre} deja {u} u de munición en {dest}"); }      // D-N190
                    Debug.Log($"[AERO] {x.Nombre} entra en la zona de {dest} (rumbo de llegada {(d != null ? d.RumboLlegada : 0)}°, {(x.Peloton >= 0 ? "con" : "sin")} pelotón)");
                }
                if (ev.Nuevo == AeronaveEstado.EnBase) { Aviso($"{x.Nombre} listo en Bétera"); Debug.Log($"[AERO] {x.Nombre} rearmado en Bétera (munición {a.MunicionMilliU / 1000f:0.0} u, combustible {a.CombustibleMilliU / 1000f:0.0} u)"); }
            }
        }

        static void Aviso(string s) { _aviso = s; _avisoHasta = Time.unscaledTime + 5f; }

        // ---------------------------------------------------------------- dibujo (solo en el mapa)
        static Texture2D Tex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }
        void Styles()
        {
            if (_t != null && _sh == Screen.height) return; _sh = Screen.height;
            _panel = Tex(new Color(0.07f, 0.08f, 0.05f, 0.94f)); _btn = Tex(new Color(0.16f, 0.18f, 0.10f, 1f)); _btnHot = Tex(Bone); _btnOff = Tex(new Color(0.12f, 0.12f, 0.10f, 1f));
            int u = Mathf.Max(10, Screen.height / 60);
            _t = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 1.5f), fontStyle = FontStyle.Bold, richText = true }; _t.normal.textColor = Bone;
            _s = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 1.15f), richText = true, wordWrap = true }; _s.normal.textColor = Bone;
            _b = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u * 1.15f), fontStyle = FontStyle.Bold, richText = true };
            _b.normal.background = _btn; _b.hover.background = _btnHot; _b.active.background = _btnHot; _b.focused.background = _btn;
            _b.normal.textColor = Bone; _b.hover.textColor = Ink; _b.active.textColor = Ink; _b.focused.textColor = Bone;
            _bOff = new GUIStyle(_b); _bOff.normal.background = _btnOff; _bOff.hover.background = _btnOff; _bOff.active.background = _btnOff; _bOff.normal.textColor = _bOff.hover.textColor = _bOff.active.textColor = new Color(0.5f, 0.5f, 0.46f);
        }

        bool Boton(Rect r, string txt, bool on = true, string resalte = null)
        {
            if (resalte != null) TutorialHud.Objetivo(resalte, r);
            return GUI.Button(r, txt, on ? _b : _bOff) && on;
        }

        static string EstadoTexto(AerodromoEstado a, Aeronave x)
        {
            switch (x.Estado)
            {
                case AeronaveEstado.EnBase: return "en la base";
                case AeronaveEstado.Preparando: return "preparando · llega en " + AerodromoEstado.Mmss(a.Eta(x.Id));
                case AeronaveEstado.Ida: return "en vuelo · llega en " + AerodromoEstado.Mmss(a.Eta(x.Id));
                case AeronaveEstado.EnZona: return "en la zona";
                case AeronaveEstado.Volviendo: return "vuelve a rearmar · lista en " + AerodromoEstado.Mmss(a.Eta(x.Id));
                case AeronaveEstado.TomandoTierra: return "tomando tierra · lista en " + AerodromoEstado.Mmss(a.Eta(x.Id));
                default: return "rearmando · lista en " + AerodromoEstado.Mmss(a.Eta(x.Id));
            }
        }

        void OnGUI()
        {
            var a = Estado; var w = NetSession.A;
            if (a == null || w == null || IntroVista.Mostrando || SceneManager.GetActiveScene().name != "Union") return;
            var op = FindFirstObjectByType<OperationsMap3D>(); var cam = Camera.main; if (op == null || cam == null) return;
            GUI.depth = -50; Styles();
            float u = _s.fontSize;
            // el nodo: icono y nombre, con clic
            var sp = cam.WorldToScreenPoint(op.MapToWorld(a.MapX, a.MapY, 0f));
            if (sp.z > 0)
            {
                int px = 32 * Mathf.Max(1, Mathf.RoundToInt(Screen.height / 540f));
                var r = new Rect(sp.x - px * 0.6f, Screen.height - sp.y - px * 1.2f, px * 1.2f, px * 1.3f);
                if (r.xMax > 0 && r.yMax > 0 && r.x < Screen.width && r.y < Screen.height)
                {
                    TutorialHud.Objetivo(TutorialGuion.Resaltes.NodoBetera, r);
                    GUI.DrawTexture(new Rect(r.x + px * 0.25f, r.y + px * 0.3f, px * 0.7f, px * 0.7f), _btnHot);
                    GUI.Label(new Rect(r.x + px * 0.36f, r.y + px * 0.28f, px, px), "<b><color=#1c200c>H</color></b>", _s);
                    if (Ui3.Activa && OperationsMap3D.RotuloBeteraOk)      // D-N185: con -ui3, en el sitio que reserva el mapa y sobre fondo (no lo pisan los trenes)
                    {
                        var t = new GUIContent("Bétera (base aérea)"); var tam = _s.CalcSize(t); OperationsMap3D.TamRotuloBetera = new Vector2(tam.x, tam.y);
                        var lr = OperationsMap3D.RotuloBetera; var o = GUI.color; GUI.color = OperationsMap3D.FondoRotulo; GUI.DrawTexture(lr, Texture2D.whiteTexture); GUI.color = o;
                        GUI.Label(new Rect(lr.x + 5f, lr.y, tam.x + 4f, lr.height), t, _sLinea ?? (_sLinea = new GUIStyle(_s) { wordWrap = false }));
                    }
                    else GUI.Label(new Rect(r.xMax + 4, r.y + px * 0.3f, u * 16, u * 2.2f), "Bétera (base aérea)", _s);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    {
                        var ev = Event.current;
                        if (_dobleClic.Clic("betera", Time.unscaledTime, ev.mousePosition.x, ev.mousePosition.y)) { op.Foco = OperationsMap3D.MapToWorldFlat(a.MapX, a.MapY); op.DistanciaObjetivo = 6f; _abierto = true; Debug.Log("[MAPA] doble clic en Bétera: la cámara va a la base (sin zona en el MVP)"); }   // D-N170
                        else { _abierto = !_abierto; if (_abierto) TutorialHud.AbrirAerodromo(); }
                    }
                }
            }
            // NH90 en camino: «NH90 1 · llega a Sagunto en 0:xx»
            int fila = 0;
            foreach (var x in a.Aeronaves)
            {
                if (x.Estado != AeronaveEstado.Preparando && x.Estado != AeronaveEstado.Ida) continue;
                var r = new Rect(Screen.width * 0.5f - u * 14, u * (11f + fila++ * 2.6f), u * 28, u * 2.2f);
                GUI.DrawTexture(r, _panel); GUI.Label(new Rect(r.x + u, r.y + u * 0.4f, r.width, r.height), $"{x.Nombre} · llega a {w.Graph.Nodes[x.Destino].Name} en {AerodromoEstado.Mmss(a.Eta(x.Id))}", _s);
                TutorialHud.Objetivo(TutorialGuion.Resaltes.Eta, r);
            }
            if (Time.unscaledTime < _avisoHasta && _aviso != null)
            {
                var r = new Rect(Screen.width * 0.5f - u * 9, u * 8.2f + fila * u * 2.6f, u * 18, u * 2.2f);
                GUI.DrawTexture(r, _panel); GUI.Label(new Rect(r.x + u, r.y + u * 0.4f, r.width, r.height), "<b>" + _aviso + "</b>", _s);
            }
            if (_abierto) Panel(a, w, u);
        }

        void Panel(AerodromoEstado a, NetWorld w, float u)
        {
            float lh = u * 2.1f, wd = Mathf.Min(u * 34, Screen.width * 0.42f), x = 12, y = Screen.height * 0.40f;
            int libres = 0; foreach (var b in a.PelotonEnBase) if (b) libres++;
            float h = u * 3f + lh * (2 + a.Aeronaves.Count * 1.1f + 1.4f + a.Destinos.Count * 1.1f + 2.6f);
            var r = new Rect(x, y, wd, h); GUI.DrawTexture(r, _panel);
            float yy = y + u * 0.5f, x0 = x + u * 0.8f, ww = wd - u * 1.6f;
            GUI.Label(new Rect(x0, yy, ww, u * 2.4f), a.Nombre, _t);
            if (GUI.Button(new Rect(x + wd - u * 2.6f, yy, u * 2, u * 2), "×", _b)) { _abierto = false; return; }
            yy += u * 2.6f;
            GUI.Label(new Rect(x0, yy, ww, lh), $"Munición <b>{a.MunicionMilliU / 1000f:0.#} u</b> · Combustible <b>{a.CombustibleMilliU / 1000f:0.#} u</b> · {a.Guarnicion}", _s); yy += lh;
            GUI.Label(new Rect(x0, yy, ww, lh), $"<b>Helicópteros</b>  · pelotones en la base: {libres} de {a.Pelotones.Count}", _s); yy += lh;
            var nhR = new Rect(x0, yy, ww, lh * 1.1f * a.Aeronaves.Count); TutorialHud.Objetivo(TutorialGuion.Resaltes.TarjetasNH90, nhR);
            foreach (var n in a.Aeronaves)
            {
                bool sel = _sel == n.Id;
                if (Boton(new Rect(x0, yy, ww, lh), (sel ? "▶ " : "") + $"{n.Nombre} · {EstadoTexto(a, n)} · munición {n.MunicionPm / 10}%" + (n.Peloton >= 0 ? " · con pelotón" : ""))) _sel = n.Id;
                yy += lh * 1.1f;
            }
            var s = a.Aeronaves[Mathf.Clamp(_sel, 0, a.Aeronaves.Count - 1)];
            if (s.Estado == AeronaveEstado.EnZona) { if (Boton(new Rect(x0, yy, ww, lh), "Volver a Bétera a rearmar")) VolverARearmar(s.Id); yy += lh * 1.4f; }
            else
            {
                var cr = new Rect(x0, yy, ww, lh);
                if (Boton(cr, (_conPeloton && libres > 0 ? "✓ " : "") + "Con pelotón a bordo", libres > 0, TutorialGuion.Resaltes.ConPeloton)) { _conPeloton = !_conPeloton; if (_conPeloton) TutorialHud.Senal(TutorialGuion.Senales.NH90ConPeloton); }
                yy += lh * 1.4f;
            }
            if (!TutorialHud.Activo && s.Estado == AeronaveEstado.EnBase) { if (OrdenesMapa.Tipo != OrdenesMapa.Sel.NH90 || OrdenesMapa.Id != s.Id) OrdenesMapa.Seleccionar(OrdenesMapa.Sel.NH90, s.Id, "panel de Bétera"); GUI.Label(new Rect(x0, yy, ww, lh * 2f), $"<b>{s.Nombre} seleccionado.</b> Clic derecho en la estación de destino del mapa.", _s); return; }   // F0: sin destino ni Enviar en el panel
            foreach (var kv in a.Destinos)
            {
                int node = TrenPreparar.Nodo(w, kv.Key); if (node < 0) continue;
                string res = kv.Key == "Sagunto" ? TutorialGuion.Resaltes.NodoSagunto : kv.Key == "Nules" ? TutorialGuion.Resaltes.NodoNules : null;
                if (Boton(new Rect(x0, yy, ww, lh), (_destino == kv.Key ? "✓ " : "") + $"{kv.Key} · {kv.Value.Km:0.0} km · ETA {AerodromoEstado.Mmss(a.EtaIdaS(kv.Key))}", s.Estado == AeronaveEstado.EnBase, res)) _destino = kv.Key;
                yy += lh * 1.1f;
            }
            if (_destino != null) TutorialHud.Objetivo(TutorialGuion.Resaltes.Eta, new Rect(x0, yy - lh * 1.1f * a.Destinos.Count, ww, lh * 1.1f * a.Destinos.Count));
            string porque = _destino == null ? "Elige destino" : a.PorQueNo(s.Id, _destino);
            if (Boton(new Rect(x0, yy + u * 0.3f, ww, lh * 1.1f), porque == null ? "Enviar" : porque, porque == null, TutorialGuion.Resaltes.BotonEnviar))
            {
                int node = TrenPreparar.Nodo(w, _destino); bool con = _conPeloton && libres > 0;
                if (a.Enviar(s.Id, _destino, node, con) == null)
                {
                    Debug.Log($"[AERO] {s.Nombre} sale hacia {_destino} {(con ? "con" : "sin")} pelotón (ETA {AerodromoEstado.Mmss(a.Eta(s.Id))}; combustible {a.CombustibleMilliU / 1000f:0.0} u)");
                    if (con) TutorialHud.Senal(TutorialGuion.Senales.NH90ConPeloton);
                    TutorialHud.Senal(TutorialGuion.Senales.NH90Enviado);
                }
            }
        }
    }
}
