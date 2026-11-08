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
    /// Embarque de tropa y vehículos para el panel: une la red de Nacho (nacho/tren-embarque, D-N166: TrainLoad / TrainUnload en el hash y en el guardado)
    /// con la cuenta de la zona (EmbarqueZona: 10 s por pelotón y 20 s por vehículo, sin disparar). Ids: pelotón = Platoon.Id de la noche (a bordo,
    /// Platoon.Aboard = TrenPreparar.ATren(tren); en otra estación, EnEstacion(nodo)); vehículo = índice en la lista de vehículos de la noche.
    /// </summary>
    public interface IEmbarque
    {
        string PorQueNo(int service, PasajeroTipo t, int id, int node);   // null si se puede subir
        void Subir(int service, PasajeroTipo t, int id, int node);
        void BajarTodo(int service, int node);
        bool EnCola(PasajeroTipo t, int id);                                // subiendo (cuenta de la zona)
        bool Ocupado { get; }
        IList<int> ABordo(int service, PasajeroTipo t);
        void Step();
    }

    /// <summary>
    /// Embarque desde el mapa, con la noche en segundo plano (NightRun.Session): la cuenta de 10 s / 20 s corre aquí a la cadencia de la zona y, al acabar,
    /// manda la orden de red a los dos mundos (A y B), igual que el resto de órdenes. Regla de noche: en el mapa los pelotones están dentro de Sagunto,
    /// con noche fuera de pantalla se miran los zombis a 3 m con NightSim.ExportZombies (Nacho). Ids de vehículo: PROTOTIPO, el índice solo vale de noche y con todos vivos; Marcos expondrá su lista _vehs.
    /// </summary>
    public sealed class EmbarqueRed : IEmbarque
    {
        readonly EmbarqueZona _z = new EmbarqueZona();
        readonly Dictionary<EmbarqueZona.Trabajo, (int service, int node)> _dest = new Dictionary<EmbarqueZona.Trabajo, (int, int)>();
        float _acc;
        static NetWorld W { get { return NetSession.A; } }
        static UnitsWorld Uw { get { return NightRun.Session != null ? NightRun.Session.Uw : null; } }

        static void PushBoth(NetCommand c) { NetSession.A.Push(c); if (NetSession.B != null) NetSession.B.Push(c); }

        public string PorQueNo(int service, PasajeroTipo t, int id, int node)
        {
            if (W == null) return "Sin red";
            if (EnCola(t, id)) return t == PasajeroTipo.Peloton ? "Subiendo" : "Cargando";
            string r = W.LoadBlockReason(service, t, id, node); if (r != null) return r;
            int cap = t == PasajeroTipo.Peloton ? EmbarqueState.PelotonesPorTren : EmbarqueState.VehiculosPorTren, enCola = 0;      // cupo contando lo que ya está subiendo
            foreach (var kv in _dest) if (kv.Key.Vehiculo == (t == PasajeroTipo.Vehiculo) && !kv.Key.Bajar && kv.Value.service == service) enCola++;
            if (W.Embarque.CountAboard(service, t) + enCola >= cap) return t == PasajeroTipo.Peloton ? "No queda vagón de tropa libre" : "No queda plataforma libre";
            // regla de noche (Nacho): con noche fuera de pantalla se miran los zombis a 3 m del pelotón; sin sesión de noche (de día), no hay zombis que mirar.
            // En casa el andén queda dentro del muro; fuera de casa, dentro del recinto solo si la estación no está sitiada.
            var ses = NightRun.Session; bool noche = ses != null && ses.Sim != null;
            bool dentro = node == W.Embarque.HomeNode || !W.Besieged(node), cerca = false;
            if (noche && t == PasajeroTipo.Peloton)
            {
                var pl = ses.Sim.Units.Platoons.Find(q => q.Id == id);
                if (pl != null) { var zs = new List<Target>(); ses.Sim.ExportZombies(zs); cerca = EmbarqueZona.ZombisCerca(zs, pl.AX, pl.AY); }
            }
            r = EmbarqueZona.NocheBlockReason(noche, dentro, cerca, false); if (r != null) return r;
            if (t == PasajeroTipo.Peloton && Uw == null) return "Sin pelotones";
            return null;
        }

        public void Subir(int service, PasajeroTipo t, int id, int node)
        {
            if (PorQueNo(service, t, id, node) != null) return;
            int carrier = TrenPreparar.ATren(W.Services[service].Train.Id);
            bool ok = t == PasajeroTipo.Peloton ? _z.SubirPeloton(Uw, id, carrier) : _z.Vehiculo(id, carrier, false);
            if (ok) _dest[_z.Cola[_z.Cola.Count - 1]] = (service, node);
        }

        /// <summary>Descargar: orden de red ya (la red decide qué baja: con asedio, solo la tropa). Fuera de casa, los pelotones quedan EnEstacion(nodo).</summary>
        public void BajarTodo(int service, int node)
        {
            if (W == null) return;
            var tropa = W.Embarque.Aboard(service, PasajeroTipo.Peloton);
            PushBoth(NetWorld.CmdUnload(W.Tick, service, node));
            if (Uw != null && node != W.Embarque.HomeNode)
                foreach (var id in tropa) { var p = Uw.Platoons.Find(q => q.Id == id); if (p != null) p.Aboard = TrenPreparar.EnEstacion(node); }
        }

        public bool EnCola(PasajeroTipo t, int id) { foreach (var q in _z.Cola) if (q.Vehiculo == (t == PasajeroTipo.Vehiculo) && q.Id == id) return true; return false; }
        public bool Ocupado { get { return _z.Cola.Count > 0; } }
        public IList<int> ABordo(int service, PasajeroTipo t) { return W == null ? new List<int>() : W.Embarque.Aboard(service, t); }

        public void Step()
        {
            if (_z.Cola.Count == 0 || W == null) { _acc = 0; return; }
            var uw = Uw ?? new UnitsWorld(1);
            _acc += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            float step = 1f / IMath.TickRate;
            while (_acc >= step)
            {
                _acc -= step;
                foreach (var t in _z.Step(uw))
                {
                    if (!_dest.TryGetValue(t, out var d)) continue; _dest.Remove(t);
                    if (!t.Bajar) PushBoth(NetWorld.CmdLoad(W.Tick, d.service, t.Vehiculo ? PasajeroTipo.Vehiculo : PasajeroTipo.Peloton, t.Id, d.node));
                    Debug.Log($"[TREN] {(t.Vehiculo ? "vehículo" : "pelotón")} {t.Id} a bordo del tren {d.service}");
                }
                var vivos = new HashSet<EmbarqueZona.Trabajo>(_z.Cola); var fuera = new List<EmbarqueZona.Trabajo>();      // los que cayeron mientras subían
                foreach (var k in _dest.Keys) if (!vivos.Contains(k)) fuera.Add(k);
                foreach (var k in fuera) _dest.Remove(k);
            }
        }
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

        public static IEmbarque Embarque = new EmbarqueRed();
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
            Embarque.Step();
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
            int sv = TrenPreparar.TrenEn(W, Nodo("Sagunto")); if (sv < 0 || _destino < 0 || Embarque.Ocupado) return;
            var p = TrenPreparar.Envio(W, sv, _destino, _carga); if (p == null) return;
            NetSession.PushPayload(NetCmdType.SetSchedule, sv, p);
            _viaje = sv; _viajeDestino = _destino; _vuelta = Nodo("Sagunto"); _descargando = false;
            _vista = Vista.Nada;
            Debug.Log($"[TREN] enviado {W.Services[sv].Train.Name} a {W.Graph.Nodes[_destino].Name}: carga {string.Join("/", _carga)}, pelotones {Embarque.ABordo(sv, PasajeroTipo.Peloton).Count}, vehículos {Embarque.ABordo(sv, PasajeroTipo.Vehiculo).Count}, ETA {TrenPreparar.HhMm(TrenPreparar.EtaDesdeParada(W, sv, _destino))}");
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
                    GUI.Label(new Rect(r.x + u * 0.6f, r.y + u * 0.4f, r.width - u, u * 2.4f), $"<b>Nules · {sv.Train.Name}</b>: {sv.CargoTotal()} u, {Embarque.ABordo(_viaje, PasajeroTipo.Peloton).Count} pelotón, {Embarque.ABordo(_viaje, PasajeroTipo.Vehiculo).Count} vehículo", _s);
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
            var vehs = new List<NightVehicle>(); var vehIdx = new List<int>();   // id de vehículo = índice en la lista de la noche (Nacho)
            if (ses != null && ses.Sim != null) for (int k = 0; k < ses.Sim.Vehicles.Count; k++) if (ses.Sim.Vehicles[k].Kind != NightVehKind.Nh90) { vehs.Add(ses.Sim.Vehicles[k]); vehIdx.Add(k); }   // los aéreos no suben (Nacho)
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
            GUI.Label(new Rect(x0, yy, ww, lh), $"<b>Tropa</b>  (vagones de tropa: {EmbarqueState.PelotonesPorTren}; 10 s por pelotón)", _s); yy += lh;
            var tropaR = new Rect(x0, yy, ww, lh * filasP); TutorialHud.Objetivo(TutorialGuion.Resaltes.ListaUnidades, tropaR);
            if (nP == 0) { GUI.Label(new Rect(x0, yy + u * 0.3f, ww, lh), "<color=#a8a49c>Sin pelotones disponibles en Sagunto</color>", _s); yy += lh; }
            for (int i = 0; i < nP; i++)
            {
                var p = uw.Platoons[i]; string nombre = ses.PName != null && ses.PName.TryGetValue(p.Id, out var nm) ? nm : p.Kind.ToString();
                bool abordo = Embarque.ABordo(sv, PasajeroTipo.Peloton).Contains(p.Id), subiendo = Embarque.EnCola(PasajeroTipo.Peloton, p.Id); string porque = Embarque.PorQueNo(sv, PasajeroTipo.Peloton, p.Id, sag); bool puede = porque == null;
                var br = new Rect(x0 + (i % 2) * (ww * 0.5f + 2), yy + (i / 2) * lh, ww * 0.5f - 2, lh * 0.9f);
                if (Boton(br, (abordo ? "✓ " : subiendo ? "… " : "") + nombre + $" · {p.AliveCount()}", abordo || subiendo || puede))
                { if (!abordo && !subiendo) { Embarque.Subir(sv, PasajeroTipo.Peloton, p.Id, sag); TutorialHud.Senal(TutorialGuion.Senales.PelotonABordo); } }
            }
            if (nP > 0) { yy += lh * filasP; string lleno = Embarque.ABordo(sv, PasajeroTipo.Peloton).Count >= EmbarqueState.PelotonesPorTren ? "  · vagones llenos" : Embarque.Ocupado ? "  · subiendo…" : ""; if (lleno.Length > 0) GUI.Label(new Rect(x0 + ww * 0.45f, tropaR.y - lh, ww * 0.55f, lh), "<color=#a8a49c>" + lleno + "</color>", _s); }
            yy += lh * 0.2f;
            // apoyo terrestre
            GUI.Label(new Rect(x0, yy, ww, lh), $"<b>Apoyo terrestre</b>  (plataformas: {EmbarqueState.VehiculosPorTren}; 20 s)", _s); yy += lh;
            var vehR = new Rect(x0, yy, ww, lh * filasV); TutorialHud.Objetivo(TutorialGuion.Resaltes.ListaVehiculos, vehR);
            for (int i = 0; i <= vehs.Count; i++)
            {
                var br = new Rect(x0 + (i % 2) * (ww * 0.5f + 2), yy + (i / 2) * lh, ww * 0.5f - 2, lh * 0.9f);
                if (i == vehs.Count) { if (Boton(br, (_sinApoyo ? "✓ " : "") + "Sin apoyo", Embarque.ABordo(sv, PasajeroTipo.Vehiculo).Count == 0 && !Embarque.Ocupado)) { _sinApoyo = true; TutorialHud.Senal(TutorialGuion.Senales.SinApoyo); } break; }
                int vi = vehIdx[i];
                bool abordo = Embarque.ABordo(sv, PasajeroTipo.Vehiculo).Contains(vi), cargando = Embarque.EnCola(PasajeroTipo.Vehiculo, vi); bool puede = Embarque.PorQueNo(sv, PasajeroTipo.Vehiculo, vi, sag) == null;
                if (Boton(br, (abordo ? "✓ " : cargando ? "… " : "") + vehs[i].Name, abordo || cargando || puede))
                { if (!abordo && !cargando) { Embarque.Subir(sv, PasajeroTipo.Vehiculo, vi, sag); _sinApoyo = false; TutorialHud.Senal(TutorialGuion.Senales.VehiculoABordo); } }
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
            if (Boton(new Rect(x0, yy, ww, lh * 1.1f), Embarque.Ocupado ? "Subiendo a bordo…" : "Enviar", _destino >= 0 && !Embarque.Ocupado, TutorialGuion.Resaltes.BotonEnviar)) Enviar();
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
                case 2: if (t > 7f) { AbrirPreparar(); Cargar(0, 40); Cargar(2, 60); var ses = NightRun.Session; int sv = TrenPreparar.TrenEn(W, sag); if (ses != null && ses.Uw != null && ses.Uw.Platoons.Count > 0) Embarque.Subir(sv, PasajeroTipo.Peloton, ses.Uw.Platoons[0].Id, sag); _sinApoyo = true; Destino(nul); _autoPaso++; } break;
                case 3: if (t > 9f) { Cap("2_preparar_tren"); _autoPaso++; } break;
                case 4: if (t > 10f && !Embarque.Ocupado) { Cap("2b_a_bordo"); NetSession.Push(NetCmdType.SetSpeed, 4, 0); Enviar(); _autoPaso++; } break;
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
