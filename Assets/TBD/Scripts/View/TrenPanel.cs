using System;
using System.Collections.Generic;
using System.IO;
using TBD.Net;
using TBD.Tutorial;
using TBD.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// Embarque de tropa y vehículos. Lo hará Nacho en la red (nacho/tren-embarque: órdenes TrainLoad / TrainUnload con tick, en el hash y en el guardado;
    /// D-N166 de Lucía). Mientras no exista, el panel habla con esta interfaz y la implementación es un stub que solo apunta el manifiesto en la vista.
    /// Ids acordados con Nacho: a bordo = TrenPreparar.ATren(tren), en otra estación = TrenPreparar.EnEstacion(nodo).
    /// </summary>
    public interface IEmbarque
    {
        bool PuedeSubirPeloton(int service, int platoonId, out string porque);
        bool PuedeSubirVehiculo(int service, string vehiculo, out string porque);
        void SubirPeloton(int service, int platoonId);
        void SubirVehiculo(int service, string vehiculo);
        void BajarTodo(int service, int node);
        IList<int> Pelotones(int service);
        IList<string> Vehiculos(int service);
    }

    /// <summary>STUB (PROTOTIPO) hasta nacho/tren-embarque: un pelotón y un vehículo por tren, sin tiempos ni condiciones de noche. No toca la simulación.</summary>
    public sealed class EmbarqueStub : IEmbarque
    {
        public const int PelotonesPorTren = 1, VehiculosPorTren = 1;     // vagones de tropa y plataformas libres (sin medir; los fija Hugo con Nacho)
        readonly Dictionary<int, List<int>> _p = new Dictionary<int, List<int>>();
        readonly Dictionary<int, List<string>> _v = new Dictionary<int, List<string>>();
        List<int> P(int s) { if (!_p.TryGetValue(s, out var l)) _p[s] = l = new List<int>(); return l; }
        List<string> V(int s) { if (!_v.TryGetValue(s, out var l)) _v[s] = l = new List<string>(); return l; }
        public bool PuedeSubirPeloton(int s, int id, out string porque) { porque = P(s).Count >= PelotonesPorTren ? "no queda vagón de tropa libre" : P(s).Contains(id) ? "ya va a bordo" : null; return porque == null; }
        public bool PuedeSubirVehiculo(int s, string v, out string porque) { porque = V(s).Count >= VehiculosPorTren ? "no queda plataforma libre" : V(s).Contains(v) ? "ya va a bordo" : null; return porque == null; }
        public void SubirPeloton(int s, int id) { if (PuedeSubirPeloton(s, id, out _)) P(s).Add(id); }
        public void SubirVehiculo(int s, string v) { if (PuedeSubirVehiculo(s, v, out _)) V(s).Add(v); }
        public void BajarTodo(int s, int node) { P(s).Clear(); V(s).Clear(); }
        public IList<int> Pelotones(int s) { return P(s); }
        public IList<string> Vehiculos(int s) { return V(s); }
    }

    /// <summary>
    /// Panel «Preparar tren» en el mapa de operaciones (Bloque 2 del 8-nov; capítulo 3 del tutorial). Clic en Sagunto → «Preparar tren»: carga (los 4 recursos),
    /// un pelotón, apoyo terrestre (un vehículo o «Sin apoyo»), destino con su ETA y «Enviar». Al llegar, el tren se aparca en el destino; clic en la estación
    /// → «Descargar» (la carga y la tropa) y vuelve. Todo con órdenes de red (SetSchedule de TrenPreparar), así que vale para la prueba de desincronización.
    /// Manda las señales del capítulo 3 (TutorialGuion.Senales) y registra los rectángulos de los anillos (TutorialGuion.Resaltes). Archivo de Iker:
    /// OperationsMap3D (Marcos) no se toca; el panel se engancha solo al cargar el mapa.
    /// Pruebas: -trenpanel-auto (en el mapa: prepara, envía, descarga y hace capturas en Captures/tren/; luego sale).
    /// </summary>
    public sealed class TrenPanel : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene;
            OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        static void OnScene(Scene s, LoadSceneMode m)
        {
            if (GoldenRunner.Active || s.name != "Union" || FindFirstObjectByType<TrenPanel>() != null) return;
            new GameObject("TBD_TrenPanel").AddComponent<TrenPanel>();
        }

        public static IEmbarque Embarque = new EmbarqueStub();
        enum Vista { Nada, MenuSagunto, Preparar, MenuNules }
        Vista _vista;
        readonly int[] _carga = new int[NetConst.ResCount];
        int _destino = -1, _vehSel = -1; bool _sinApoyo;
        // viajes en curso (sobreviven al cambio de escena: el tren sigue en la red)
        static int _viaje = -1, _viajeDestino = -1, _vuelta = -1; static bool _descargando;
        static float _avisoHasta, _aparcadoT = -10f; static string _aviso;
        GUIStyle _t, _s, _b, _bOff; Texture2D _panel, _btn, _btnHot, _btnOff; int _sh;
        static readonly Color Bone = new Color32(0xd4, 0xd0, 0xcc, 255), Ink = new Color32(0x1c, 0x20, 0x0c, 255);
        static readonly string[] ResNombre = { "Munición", "Combustible", "Materiales", "Personas" };

        NetWorld W { get { return NetSession.A; } }
        int Nodo(string n) { return W == null ? -1 : TrenPreparar.Nodo(W, n); }
        static bool Arg(string a) { return Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0; }

        void Start()
        {
            TutorialHud.EstadoExtra = s =>
                s == TutorialGuion.Senales.PanelTrenAbierto ? _vista == Vista.Preparar
              : s == TutorialGuion.Senales.TrenEnNules ? (W != null && _viaje >= 0 && TrenPreparar.TrenEn(W, Nodo("Nules")) == _viaje)
              : false;
        }

        void Update()
        {
            if (W == null) return;
            // llegada: aparcar (SetSchedule es cíclico; si no, repetiría el viaje cada día)
            if (_viaje >= 0 && _viaje < W.Services.Count)
            {
                var sv = W.Services[_viaje];
                int meta = _descargando ? _vuelta : _viajeDestino;
                if (sv.AtStop && sv.Stops.Count > 1 && sv.Stops[sv.StopIndex].Node == meta && sv.DepartedDay.Count > 0 && Time.unscaledTime > _aparcadoT + 1f)
                {
                    NetSession.PushPayload(NetCmdType.SetSchedule, _viaje, TrenPreparar.Aparcar(W, _viaje)); _aparcadoT = Time.unscaledTime;
                    if (!_descargando) { _aviso = "Tren en " + W.Graph.Nodes[meta].Name; _avisoHasta = Time.unscaledTime + 5f; TutorialHud.Senal(TutorialGuion.Senales.TrenEnNules); Debug.Log("[TREN] llega a " + W.Graph.Nodes[meta].Name + " y se aparca"); }
                    else { Debug.Log("[TREN] vuelve a " + W.Graph.Nodes[meta].Name + " y se aparca"); _viaje = -1; _descargando = false; }
                }
            }
            if (Arg("-trenpanel-auto")) Auto();
        }

        // ---------------------------------------------------------------- acciones (las usa el botón y la corrida automática)
        void AbrirPreparar()
        {
            _vista = Vista.Preparar; Array.Clear(_carga, 0, _carga.Length); _destino = -1; _vehSel = -1; _sinApoyo = false;
            TutorialHud.Senal(TutorialGuion.Senales.PanelTrenAbierto);
        }
        void Cargar(int r, int d)
        {
            int sv = TrenPreparar.TrenEn(W, Nodo("Sagunto")); if (sv < 0) return;
            int total = 0; foreach (var c in _carga) total += c;
            int hueco = TrenPreparar.Hueco(W, sv) - total;
            _carga[r] = Mathf.Clamp(_carga[r] + Mathf.Min(d, hueco), 0, Mathf.Min(NetWorld.MaxScheduleTransfer, W.Stock[Nodo("Sagunto"), r]));
            if (_carga[r] > 0) TutorialHud.Senal(TutorialGuion.Senales.CargaPuesta);
        }
        void Destino(int node) { _destino = node; if (node == Nodo("Nules")) TutorialHud.Senal(TutorialGuion.Senales.DestinoNules); }
        void Enviar()
        {
            int sv = TrenPreparar.TrenEn(W, Nodo("Sagunto")); if (sv < 0 || _destino < 0) return;
            var p = TrenPreparar.Envio(W, sv, _destino, _carga); if (p == null) return;
            NetSession.PushPayload(NetCmdType.SetSchedule, sv, p);
            _viaje = sv; _viajeDestino = _destino; _vuelta = Nodo("Sagunto"); _descargando = false;
            _vista = Vista.Nada;
            Debug.Log($"[TREN] enviado {W.Services[sv].Train.Name} a {W.Graph.Nodes[_destino].Name}: carga {string.Join("/", _carga)}, pelotones {Embarque.Pelotones(sv).Count}, vehículos {Embarque.Vehiculos(sv).Count}, ETA {TrenPreparar.HhMm(TrenPreparar.EtaDesdeParada(W, sv, _destino))}");
            TutorialHud.Senal(TutorialGuion.Senales.TrenEnviado);
        }
        void Descargar()
        {
            if (_viaje < 0) return; var p = TrenPreparar.Descarga(W, _viaje, _vuelta); if (p == null) return;
            NetSession.PushPayload(NetCmdType.SetSchedule, _viaje, p);
            Embarque.BajarTodo(_viaje, _viajeDestino); _descargando = true; _vista = Vista.Nada;
            Debug.Log("[TREN] descarga en " + W.Graph.Nodes[_viajeDestino].Name + " y vuelve");
            TutorialHud.Senal(TutorialGuion.Senales.Descargado);
        }

        // ---------------------------------------------------------------- dibujo
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

        Rect NodoRect(string name)
        {
            var op = FindFirstObjectByType<OperationsMap3D>(); var cam = Camera.main; int id = Nodo(name);
            if (op == null || cam == null || id < 0) return Rect.zero;
            var n = W.Graph.Nodes[id]; var sp = cam.WorldToScreenPoint(op.MapToWorld(n.XMeters, n.YMeters, 0f));
            if (sp.z <= 0) return Rect.zero;
            int px = 32 * Mathf.Max(1, Mathf.RoundToInt(Screen.height / 540f));
            var r = new Rect(sp.x - px * 0.6f, Screen.height - sp.y - px * 1.2f, px * 1.2f, px * 1.3f);
            return r.xMax < 0 || r.yMax < 0 || r.x > Screen.width || r.y > Screen.height ? Rect.zero : r;     // fuera de pantalla: como si no estuviera
        }

        bool Boton(Rect r, string txt, bool on = true, string resalte = null)
        {
            if (resalte != null) TutorialHud.Objetivo(resalte, r);
            return GUI.Button(r, txt, on ? _b : _bOff) && on;
        }

        void OnGUI()
        {
            if (W == null || IntroVista.Mostrando) return;
            GUI.depth = -50; Styles();
            int sag = Nodo("Sagunto"), nul = Nodo("Nules");
            // clic en los nodos (botón invisible sobre el icono)
            var rs = NodoRect("Sagunto"); var rn = NodoRect("Nules");
            if (rs.width > 0 && GUI.Button(rs, GUIContent.none, GUIStyle.none)) _vista = _vista == Vista.MenuSagunto ? Vista.Nada : Vista.MenuSagunto;
            if (rn.width > 0 && GUI.Button(rn, GUIContent.none, GUIStyle.none)) { _vista = Vista.MenuNules; if (_viaje >= 0 && TrenPreparar.TrenEn(W, nul) == _viaje) TutorialHud.Senal(TutorialGuion.Senales.PanelNulesAbierto); }
            float u = _s.fontSize;
            // tren en camino: «El tren llega a Nules en 0:xx»
            if (_viaje >= 0 && !_descargando && _viaje < W.Services.Count && !W.Services[_viaje].AtStop)
            {
                long eta = TrenPreparar.EtaEnMarcha(W, _viaje);
                var r = new Rect(Screen.width * 0.5f - u * 14, u * 5.5f, u * 28, u * 2.2f);
                GUI.DrawTexture(r, _panel); GUI.Label(new Rect(r.x + u, r.y + u * 0.4f, r.width, r.height), $"El tren llega a {W.Graph.Nodes[_viajeDestino].Name} en {TrenPreparar.HhMm(eta)}", _s);
                TutorialHud.Objetivo(TutorialGuion.Resaltes.Eta, r);
            }
            if (Time.unscaledTime < _avisoHasta && _aviso != null)
            {
                var r = new Rect(Screen.width * 0.5f - u * 8, u * 8.2f, u * 16, u * 2.2f);
                GUI.DrawTexture(r, _panel); GUI.Label(new Rect(r.x + u, r.y + u * 0.4f, r.width, r.height), "<b>" + _aviso + "</b>", _s);
            }
            if (rs.width <= 0) rs = new Rect(Screen.width * 0.45f, Screen.height * 0.45f, 0, u * 2);     // nodo fuera de pantalla: el menú sale en el centro
            if (rn.width <= 0) rn = new Rect(Screen.width * 0.45f, Screen.height * 0.45f, 0, u * 2);
            if (_vista == Vista.MenuSagunto)
            {
                int sv = TrenPreparar.TrenEn(W, sag);
                var r = new Rect(rs.xMax + 8, rs.y, u * 15, u * 3.2f); GUI.DrawTexture(r, _panel);
                if (Boton(new Rect(r.x + u * 0.5f, r.y + u * 0.5f, r.width - u, u * 2.2f), sv >= 0 ? "Preparar tren" : "Sin tren en el andén", sv >= 0, TutorialGuion.Resaltes.BotonPrepararTren)) AbrirPreparar();
            }
            else if (_vista == Vista.MenuNules)
            {
                bool aqui = _viaje >= 0 && !_descargando && TrenPreparar.TrenEn(W, nul) == _viaje;
                var r = new Rect(rn.xMax + 8, rn.y, u * 19, u * (aqui ? 6.6f : 3.4f)); GUI.DrawTexture(r, _panel);
                if (!aqui) GUI.Label(new Rect(r.x + u * 0.6f, r.y + u * 0.6f, r.width, u * 2), "Nules · sin tren en el andén", _s);
                else
                {
                    var sv = W.Services[_viaje];
                    GUI.Label(new Rect(r.x + u * 0.6f, r.y + u * 0.4f, r.width - u, u * 2.4f), $"<b>{sv.Train.Name}</b> · {sv.CargoTotal()} u, {Embarque.Pelotones(_viaje).Count} pelotón, {Embarque.Vehiculos(_viaje).Count} vehículo", _s);
                    if (Boton(new Rect(r.x + u * 0.5f, r.y + u * 3.6f, r.width - u, u * 2.4f), "Descargar", true, TutorialGuion.Resaltes.BotonDescargar)) Descargar();
                }
            }
            else if (_vista == Vista.Preparar) PanelPreparar(sag, nul);
        }

        void PanelPreparar(int sag, int nul)
        {
            int sv = TrenPreparar.TrenEn(W, sag);
            if (sv < 0) { _vista = Vista.Nada; return; }
            float u = _s.fontSize, w = Mathf.Min(u * 36, Screen.width * 0.44f), x = Screen.width - w - 12, y = Screen.height * 0.08f, lh = u * 1.95f;
            var ses = NightRun.Session; var uw = ses != null ? ses.Uw : null;
            var vehs = new List<NightVehicle>(); if (ses != null && ses.Sim != null) foreach (var v in ses.Sim.Vehicles) if (v.Kind != NightVehKind.Nh90) vehs.Add(v);   // los aéreos no suben (Nacho)
            int nP = uw != null ? Mathf.Min(5, uw.Platoons.Count) : 0;
            int filasP = Mathf.Max(1, (nP + 1) / 2), filasV = (vehs.Count + 2) / 2;
            float h = u * 3.4f + lh * (NetConst.ResCount + 1.4f) + lh * (filasP + 1.2f) + lh * (filasV + 1.2f) + lh * 2.1f + lh * 1.5f;
            var r = new Rect(x, y, w, h); GUI.DrawTexture(r, _panel);
            float yy = y + u * 0.6f, x0 = x + u * 0.8f, ww = w - u * 1.6f;
            GUI.Label(new Rect(x0, yy, ww, u * 2.4f), $"Preparar tren · {W.Services[sv].Train.Name}", _t);
            if (GUI.Button(new Rect(x + w - u * 2.6f, yy, u * 2, u * 2), "×", _b)) { _vista = Vista.Nada; return; }
            yy += u * 2.8f;
            // carga
            int total = 0; foreach (var c in _carga) total += c;
            GUI.Label(new Rect(x0, yy, ww, lh), $"<b>Carga</b>  ({total} de {TrenPreparar.Hueco(W, sv)} u libres)", _s); yy += lh;
            var cargaR = new Rect(x0, yy, ww, lh * NetConst.ResCount); TutorialHud.Objetivo(TutorialGuion.Resaltes.Cargas, cargaR);
            for (int res = 0; res < NetConst.ResCount; res++)
            {
                GUI.Label(new Rect(x0, yy + u * 0.3f, ww * 0.5f, lh), $"{ResNombre[res]} <color=#a8a49c>(hay {W.Stock[sag, res]})</color>", _s);
                if (Boton(new Rect(x0 + ww * 0.56f, yy, lh * 1.3f, lh * 0.9f), "−10")) Cargar(res, -10);
                GUI.Label(new Rect(x0 + ww * 0.56f + lh * 1.45f, yy + u * 0.3f, lh * 1.6f, lh), $"<b>{_carga[res]}</b>", _s);
                if (Boton(new Rect(x0 + ww * 0.56f + lh * 2.9f, yy, lh * 1.3f, lh * 0.9f), "+10")) Cargar(res, 10);
                yy += lh;
            }
            yy += lh * 0.4f;
            // tropa
            GUI.Label(new Rect(x0, yy, ww, lh), $"<b>Tropa</b>  (vagones de tropa: {EmbarqueStub.PelotonesPorTren})", _s); yy += lh;
            var tropaR = new Rect(x0, yy, ww, lh * filasP); TutorialHud.Objetivo(TutorialGuion.Resaltes.ListaUnidades, tropaR);
            if (nP == 0) { GUI.Label(new Rect(x0, yy + u * 0.3f, ww, lh), "<color=#a8a49c>Sin pelotones disponibles en Sagunto</color>", _s); yy += lh; }
            for (int i = 0; i < nP; i++)
            {
                var p = uw.Platoons[i]; string nombre = ses.PName != null && ses.PName.TryGetValue(p.Id, out var nm) ? nm : p.Kind.ToString();
                bool abordo = Embarque.Pelotones(sv).Contains(p.Id); bool puede = Embarque.PuedeSubirPeloton(sv, p.Id, out var porque);
                var br = new Rect(x0 + (i % 2) * (ww * 0.5f + 2), yy + (i / 2) * lh, ww * 0.5f - 2, lh * 0.9f);
                if (Boton(br, (abordo ? "✓ " : "") + nombre + $" · {p.AliveCount()}", abordo || puede))
                { if (!abordo) { Embarque.SubirPeloton(sv, p.Id); TutorialHud.Senal(TutorialGuion.Senales.PelotonABordo); } }
            }
            if (nP > 0) { yy += lh * filasP; string lleno = Embarque.Pelotones(sv).Count >= EmbarqueStub.PelotonesPorTren ? "  · vagones llenos" : ""; if (lleno.Length > 0) GUI.Label(new Rect(x0 + ww * 0.45f, tropaR.y - lh, ww * 0.55f, lh), "<color=#a8a49c>" + lleno + "</color>", _s); }
            yy += lh * 0.2f;
            // apoyo terrestre
            GUI.Label(new Rect(x0, yy, ww, lh), $"<b>Apoyo terrestre</b>  (plataformas: {EmbarqueStub.VehiculosPorTren})", _s); yy += lh;
            var vehR = new Rect(x0, yy, ww, lh * filasV); TutorialHud.Objetivo(TutorialGuion.Resaltes.ListaVehiculos, vehR);
            for (int i = 0; i <= vehs.Count; i++)
            {
                var br = new Rect(x0 + (i % 2) * (ww * 0.5f + 2), yy + (i / 2) * lh, ww * 0.5f - 2, lh * 0.9f);
                if (i == vehs.Count) { if (Boton(br, (_sinApoyo ? "✓ " : "") + "Sin apoyo", Embarque.Vehiculos(sv).Count == 0)) { _sinApoyo = true; TutorialHud.Senal(TutorialGuion.Senales.SinApoyo); } break; }
                bool abordo = Embarque.Vehiculos(sv).Contains(vehs[i].Name); bool puede = Embarque.PuedeSubirVehiculo(sv, vehs[i].Name, out var porque);
                if (Boton(br, (abordo ? "✓ " : "") + vehs[i].Name, abordo || puede))
                { if (!abordo) { Embarque.SubirVehiculo(sv, vehs[i].Name); _sinApoyo = false; TutorialHud.Senal(TutorialGuion.Senales.VehiculoABordo); } }
            }
            yy += lh * filasV + lh * 0.2f;
            // destino y ETA
            GUI.Label(new Rect(x0, yy, ww * 0.3f, lh), "<b>Destino</b>", _s);
            int cas = Nodo("Castellón");
            if (Boton(new Rect(x0 + ww * 0.28f, yy, ww * 0.3f, lh * 0.9f), (_destino == nul ? "✓ " : "") + "Nules", true, TutorialGuion.Resaltes.NodoNules)) Destino(nul);
            bool casOk = cas >= 0 && W.Stations.Founded[cas];
            if (Boton(new Rect(x0 + ww * 0.62f, yy, ww * 0.38f, lh * 0.9f), casOk ? (_destino == cas ? "✓ " : "") + "Castellón" : "Castellón (infestada)", casOk)) Destino(cas);
            yy += lh;
            string etaTxt = _destino >= 0 ? TrenPreparar.HhMm(TrenPreparar.EtaDesdeParada(W, sv, _destino)) : "—";
            var etaR = new Rect(x0, yy, ww, lh); GUI.Label(etaR, $"ETA: <b>{etaTxt}</b>" + (_destino >= 0 ? $"  · estado: {W.Graph.Nodes[_destino].State}" : ""), _s);
            TutorialHud.Objetivo(TutorialGuion.Resaltes.Eta, etaR);
            if (_destino >= 0) TutorialHud.Objetivo(TutorialGuion.Resaltes.EstadoNules, etaR);
            yy += lh * 1.1f;
            if (Boton(new Rect(x0, yy, ww, lh * 1.1f), "Enviar", _destino >= 0, TutorialGuion.Resaltes.BotonEnviar)) Enviar();
        }

        // ---------------------------------------------------------------- corrida automática (-trenpanel-auto)
        float _autoT0 = -1; int _autoPaso;
        void Auto()
        {
            if (_autoT0 < 0) _autoT0 = Time.unscaledTime; float t = Time.unscaledTime - _autoT0;
            int sag = Nodo("Sagunto"), nul = Nodo("Nules");
            switch (_autoPaso)
            {
                case 0: if (t > 4f && TrenPreparar.TrenEn(W, sag) >= 0) { _vista = Vista.MenuSagunto; _autoPaso++; } else if (t > 4f && W.Speed != 4) NetSession.Push(NetCmdType.SetSpeed, 4, 0); break;
                case 1: if (t > 6f) { Cap("1_menu_sagunto"); _autoPaso++; } break;
                case 2: if (t > 7f) { AbrirPreparar(); Cargar(0, 40); Cargar(2, 60); var ses = NightRun.Session; int sv = TrenPreparar.TrenEn(W, sag); if (ses != null && ses.Uw != null && ses.Uw.Platoons.Count > 0) Embarque.SubirPeloton(sv, ses.Uw.Platoons[0].Id); _sinApoyo = true; Destino(nul); _autoPaso++; } break;
                case 3: if (t > 9f) { Cap("2_preparar_tren"); _autoPaso++; } break;
                case 4: if (t > 10f) { NetSession.Push(NetCmdType.SetSpeed, 4, 0); Enviar(); _autoPaso++; } break;
                case 5: if (t > 13f) { Cap("3_en_camino"); _autoPaso++; } break;
                case 6: if (_viaje >= 0 && TrenPreparar.TrenEn(W, nul) == _viaje && W.Services[_viaje].Stops.Count == 1) { _vista = Vista.MenuNules; _autoT0 = Time.unscaledTime - 100f; _autoPaso++; } else if (t > 240f) { Debug.LogError("[TREN] auto: el tren no llegó a Nules"); Application.Quit(1); } break;
                case 7: if (t > 102f) { Cap("4_nules_descargar"); _autoPaso++; } break;
                case 8: if (t > 103f) { Descargar(); _autoPaso++; } break;
                case 9: if (_viaje < 0) { Debug.Log($"[TREN] auto: ciclo completo; Nules tiene {W.Stock[nul, 0]} de munición y {W.Stock[nul, 2]} de materiales"); Cap("5_de_vuelta"); _autoT0 = Time.unscaledTime - 200f; _autoPaso++; } else if (t > 340f) { Debug.LogError("[TREN] auto: el tren no volvió"); Application.Quit(1); } break;
                case 10: if (t > 203f) { Debug.Log("[TREN] auto: fin"); Application.Quit(0); _autoPaso++; } break;
            }
        }
        static void Cap(string n)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "tren")); Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, n + ".png")); Debug.Log("[TREN] captura " + n);
        }
    }
}
