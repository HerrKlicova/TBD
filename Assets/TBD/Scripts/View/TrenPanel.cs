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
    /// Platoon.Aboard = TrenPreparar.ATren(tren); en otra estación, EnEstacion(nodo)); vehículo = índice en NightRun.Vehs (Marcos).
    /// </summary>
    public interface IEmbarque
    {
        string PorQueNo(int service, PasajeroTipo t, int id, int node);   // null si se puede subir
        void Subir(int service, PasajeroTipo t, int id, int node);
        void BajarTodo(int service, int node);                              // fuera de casa: orden de red ya; los pelotones quedan EnEstacion(nodo)
        void BajarEnCasa(int service, int node, int x, int y, bool soloTropa, Action alAcabar);   // en la zona: 10 s / 20 s y luego la orden de red
        bool EnCola(PasajeroTipo t, int id);
        bool Ocupado { get; }
        IList<int> ABordo(int service, PasajeroTipo t);
        void Step();
    }

    /// <summary>
    /// Embarque con la noche en segundo plano (NightRun.Session): la cuenta de 10 s / 20 s corre aquí a la cadencia de la zona y, al acabar, manda la orden
    /// de red a los dos mundos (A y B). Regla de noche (Nacho): con noche fuera de pantalla se miran los zombis a 3 m con NightSim.ExportZombies; en casa
    /// el andén queda dentro del muro; fuera de casa, dentro del recinto solo si la estación no está sitiada. Vehículos: al subir, NightRun.Vehs[i].Embarcado
    /// = true y Marcos oculta el vehículo en la zona; al bajar en casa, false.
    /// </summary>
    public sealed class EmbarqueRed : IEmbarque
    {
        readonly EmbarqueZona _z = new EmbarqueZona();
        readonly Dictionary<EmbarqueZona.Trabajo, (int service, int node)> _dest = new Dictionary<EmbarqueZona.Trabajo, (int, int)>();
        int _bajadaService = -1, _bajadaNode = -1, _bajadaQuedan; bool _bajadaSolo; Action _bajadaFin;
        float _acc;
        static NetWorld W { get { return NetSession.A; } }
        static UnitsWorld Uw { get { return NightRun.Session != null ? NightRun.Session.Uw : null; } }

        static void PushBoth(NetCommand c) { NetSession.A.Push(c); if (NetSession.B != null) NetSession.B.Push(c); }
        static void Embarcado(int v, bool on) { if (v >= 0 && v < NightRun.Vehs.Count && NightRun.Vehs[v] != null) NightRun.Vehs[v].Embarcado = on; }

        public string PorQueNo(int service, PasajeroTipo t, int id, int node)
        {
            if (W == null) return "Sin red";
            if (EnCola(t, id)) return t == PasajeroTipo.Peloton ? "Subiendo" : "Cargando";
            string r = W.LoadBlockReason(service, t, id, node); if (r != null) return r;
            int cap = t == PasajeroTipo.Peloton ? EmbarqueState.PelotonesPorTren : EmbarqueState.VehiculosPorTren, enCola = 0;      // cupo contando lo que ya está subiendo
            foreach (var kv in _dest) if (kv.Key.Vehiculo == (t == PasajeroTipo.Vehiculo) && !kv.Key.Bajar && kv.Value.service == service) enCola++;
            if (W.Embarque.CountAboard(service, t) + enCola >= cap) return t == PasajeroTipo.Peloton ? "No queda vagón de tropa libre" : "No queda plataforma libre";
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
            var uw = Uw;
            if (t == PasajeroTipo.Peloton && uw != null) { var p = uw.Platoons.Find(q => q.Id == id); if (p != null && p.Aboard >= TrenPreparar.EstacionBase) p.Aboard = 0; }   // el destacamento de otra estación sale del andén
            bool ok = t == PasajeroTipo.Peloton ? _z.SubirPeloton(uw, id, carrier) : _z.Vehiculo(id, carrier, false);
            if (ok) _dest[_z.Cola[_z.Cola.Count - 1]] = (service, node);
        }

        public void BajarTodo(int service, int node)
        {
            if (W == null) return;
            var tropa = W.Embarque.Aboard(service, PasajeroTipo.Peloton);
            PushBoth(NetWorld.CmdUnload(W.Tick, service, node));
            if (Uw != null && node != W.Embarque.HomeNode)
                foreach (var id in tropa) { var p = Uw.Platoons.Find(q => q.Id == id); if (p != null) p.Aboard = TrenPreparar.EnEstacion(node); }
        }

        /// <summary>
        /// Bajada en casa, dentro de la zona (D-N166): cada pelotón tarda 10 s y aparece en (x, y); cada vehículo, 20 s. Con soloTropa (Sagunto sitiada) los
        /// vehículos se quedan a bordo. Al acabar todos, la orden de red (TrainUnload: la red vuelve a decidir qué baja) y alAcabar.
        /// </summary>
        public void BajarEnCasa(int service, int node, int x, int y, bool soloTropa, Action alAcabar)
        {
            if (W == null || _bajadaService >= 0) return;
            int carrier = TrenPreparar.ATren(W.Services[service].Train.Id), n = 0;
            var uw = Uw;
            if (uw != null) foreach (var id in W.Embarque.Aboard(service, PasajeroTipo.Peloton)) if (_z.BajarPeloton(uw, id, x + n * 1500, y)) { _dest[_z.Cola[_z.Cola.Count - 1]] = (service, node); n++; }
            if (!soloTropa) foreach (var v in W.Embarque.Aboard(service, PasajeroTipo.Vehiculo)) if (_z.Vehiculo(v, carrier, true)) { _dest[_z.Cola[_z.Cola.Count - 1]] = (service, node); n++; }
            _bajadaService = service; _bajadaNode = node; _bajadaQuedan = n; _bajadaSolo = soloTropa; _bajadaFin = alAcabar;
            if (n == 0) TerminarBajada();
        }

        void TerminarBajada()
        {
            PushBoth(NetWorld.CmdUnload(W.Tick, _bajadaService, _bajadaNode));
            if (!_bajadaSolo)      // la carga baja al andén (CmdCargo de Nacho); con asedio se queda a bordo hasta que acabe (D-N166)
            {
                var sv = W.Services[_bajadaService]; var delta = new int[NetConst.ResCount]; bool hay = false;
                for (int r = 0; r < NetConst.ResCount; r++) { delta[r] = -sv.Cargo[r]; hay |= sv.Cargo[r] > 0; }
                if (hay) PushBoth(NetWorld.CmdCargo(W.Tick, _bajadaService, _bajadaNode, delta));
            }
            Debug.Log($"[TREN] bajada en {W.Graph.Nodes[_bajadaNode].Name} acabada{(_bajadaSolo ? " (solo la tropa: sitiada)" : "")}");
            var fin = _bajadaFin; _bajadaService = -1; _bajadaFin = null; fin?.Invoke();
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
                    if (t.Vehiculo) Embarcado(t.Id, !t.Bajar);
                    Debug.Log($"[TREN] {(t.Vehiculo ? "vehículo" : "pelotón")} {t.Id} {(t.Bajar ? "en tierra" : "a bordo")} (tren {d.service})");
                    if (t.Bajar && _bajadaService >= 0 && --_bajadaQuedan <= 0) TerminarBajada();
                }
                var vivos = new HashSet<EmbarqueZona.Trabajo>(_z.Cola); var fuera = new List<EmbarqueZona.Trabajo>();      // los que cayeron mientras subían o bajaban
                foreach (var k in _dest.Keys) if (!vivos.Contains(k)) fuera.Add(k);
                foreach (var k in fuera) { _dest.Remove(k); if (k.Bajar && _bajadaService >= 0 && --_bajadaQuedan <= 0) TerminarBajada(); }
            }
        }
    }

    /// <summary>
    /// Panel de trenes en el mapa de operaciones (Bloque 2 del 8-nov; capítulo 3 del tutorial, D-N172). Clic en una estación → su menú: el destacamento que
    /// tenga, «Preparar tren» si hay tren en el andén y «Descargar» si ha llegado uno de fuera. Preparar tren: carga (los 4 recursos o «Sin carga»), tropa,
    /// apoyo terrestre (un vehículo o «Sin apoyo»), destino con su ETA y «Enviar». Al llegar, el tren se aparca (TrenPreparar.Aparcar). Doble clic en Sagunto:
    /// entra en la zona (D-N170). Todo con órdenes de red. Manda las señales del tutorial y registra los anillos. OperationsMap3D (Marcos) no se toca.
    /// Pruebas: -trenpanel-auto (Sagunto → Nules → vuelta) y -refuerzos-auto (capítulo 3: Nules → Sagunto con el destacamento); capturas en Captures/tren/.
    /// </summary>
    public sealed class TrenPanel : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene;
            OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
            TutorialHud.EstadoExtra = Estado;
        }
        static void OnScene(Scene s, LoadSceneMode m)
        {
            if (GoldenRunner.Active) return;
            if (s.name == "Union" && FindFirstObjectByType<TrenPanel>() == null) new GameObject("TBD_TrenPanel").AddComponent<TrenPanel>();
            if (s.name == "SampleScene" && FindFirstObjectByType<TrenAnden>() == null) new GameObject("TBD_TrenAnden").AddComponent<TrenAnden>();
        }

        public static IEmbarque Embarque = new EmbarqueRed();

        // ---------------------------------------------------------------- viaje en curso (sobrevive al cambio de escena: el tren sigue en la red)
        public static int Viaje = -1, Origen = -1, Destino = -1; static bool _descargando, _llegado; static int _munEnvio;
        static float _avisoHasta, _aparcadoT = -10f; static string _aviso; static bool _avisoAmbar;     // D-N177: el de «Sin escolta» va en ámbar
        static NetWorld W { get { return NetSession.A; } }
        static int Nodo(string n) { return W == null ? -1 : TrenPreparar.Nodo(W, n); }
        static int Casa { get { return W == null ? -1 : W.Embarque.HomeNode; } }
        static bool Arg(string a) { return Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0; }

        /// <summary>Estados que el tutorial puede preguntar en cualquier momento (TutorialMotor.EstadoActual).</summary>
        static bool Estado(string s)
        {
            if (W == null) return false;
            var p = FindFirstObjectByType<TrenPanel>();
            if (s == TutorialGuion.Senales.PanelTrenAbierto) return p != null && p._vista == Vista.Preparar;
            if (s == TutorialGuion.Senales.SaguntoSitiada) return W.Besieged(Casa);
            if (s == TutorialGuion.Senales.VueltaZona) return SceneManager.GetActiveScene().name == "SampleScene";
            if (s == TutorialGuion.Senales.TrenEnSagunto) return Viaje >= 0 && _llegado && Destino == Casa;
            if (s == TutorialGuion.Senales.TrenEnNules) return Viaje >= 0 && _llegado && Destino == Nodo("Nules");
            return false;
        }

        /// <summary>«0:12»: lo que le falta al tren en marcha (para el «{eta}» del tutorial).</summary>
        public static string EtaTexto() { return W != null && Viaje >= 0 && Viaje < W.Services.Count && !W.Services[Viaje].AtStop ? TrenPreparar.HhMm(TrenPreparar.EtaEnMarcha(W, Viaje)) : "—"; }

        /// <summary>Llegadas: aparcar y avisar. La llaman el mapa (TrenPanel) y la zona (TrenAnden), así que funciona en las dos vistas.</summary>
        public static void Vigilar()
        {
            if (W == null) return;
            Embarque.Step();
            if (Viaje < 0 || Viaje >= W.Services.Count) return;
            var sv = W.Services[Viaje];
            int meta = _descargando ? Origen : Destino;
            if (sv.AtStop && sv.Stops.Count > 1 && sv.Stops[sv.StopIndex].Node == meta && sv.DepartedDay.Count > 0 && Time.unscaledTime > _aparcadoT + 1f)
            {
                NetSession.PushPayload(NetCmdType.SetSchedule, Viaje, TrenPreparar.Aparcar(W, Viaje)); _aparcadoT = Time.unscaledTime;
                string nombre = W.Graph.Nodes[meta].Name;
                if (_descargando) { Debug.Log("[TREN] vuelve a " + nombre + " y se aparca"); Viaje = -1; _descargando = false; return; }
                _llegado = true; _avisoAmbar = false; _aviso = Puertas.EnPuertas(W, Viaje, meta) ? "Tren a las puertas de " + nombre : "Tren en " + nombre; _avisoHasta = Time.unscaledTime + 5f;     // D-N152: sitiada o sin andén (regla de Nacho)
                Debug.Log($"[TREN] llega a {nombre} y se aparca (carga {sv.CargoTotal()} u, {W.Embarque.CountAboard(Viaje, PasajeroTipo.Peloton)} pelotón, {W.Embarque.CountAboard(Viaje, PasajeroTipo.Vehiculo)} vehículo)");
                if (meta == Casa) TutorialHud.Senal(TutorialGuion.Senales.TrenEnSagunto);
                if (meta == Nodo("Nules")) TutorialHud.Senal(TutorialGuion.Senales.TrenEnNules);
                TutorialHud.Viaje.Llegado(Viaje, nombre);
                var emb = W.LastAmbush; bool cercado = emb.Service == Viaje && emb.Surrounded && !emb.Repelled && emb.HoldUntil > W.Tick;
                if (meta != Casa && !cercado && _yo != null) { _yo.DescargarFuera(); Debug.Log("[TREN] descarga automática al llegar a " + nombre); }   // Eugenio (I-Noche110): llevó tropa y munición y el objetivo seguía a 0: fuera de casa, el tren descarga solo al llegar
            }
        }

        static void Empezar(int sv, int origen, int destino, int mun) { Viaje = sv; Origen = origen; Destino = destino; _descargando = false; _llegado = false; _munEnvio = mun; _descargadoAqui = false; }
        static bool _descargadoAqui;     // la carga ya bajó en el destino (CmdCargo): el tren se queda y se puede preparar para volver

        // ---------------------------------------------------------------- vista del mapa
        enum Vista { Nada, Menu, Preparar }
        static TrenPanel _yo; public static bool PanelAbierto { get { return _yo != null && _yo._vista != Vista.Nada; } }      // el recuadro del objetivo no lo tapa (Eugenio)
        Vista _vista; int _nodoMenu = -1, _origenP = -1;
        readonly int[] _carga = new int[NetConst.ResCount];
        int _destinoP = -1; bool _sinApoyo, _sinCarga;
        readonly DobleClic _dobleClic = new DobleClic(); readonly SeguirTren _seguir = new SeguirTren();
        GUIStyle _t, _s, _b, _bOff; Texture2D _panel, _btn, _btnHot, _btnOff; int _sh;
        static readonly Color Bone = new Color32(0xd4, 0xd0, 0xcc, 255), Ink = new Color32(0x1c, 0x20, 0x0c, 255);
        static readonly string[] ResNombre = { "Munición", "Combustible", "Materiales", "Personas", "Víveres" };   // D-N176: los víveres son el 5.º recurso de la red (Res.Viveres = 4, nacho/sombra-reloj)

        void Update()
        {
            _yo = this;
            Vigilar();
            Seguir();
            if (Arg("-trenpanel-auto")) AutoIdaVuelta();
            if (Arg("-refuerzos-auto")) AutoRefuerzos();
            if (Arg("-dia2-auto") || Arg("-dia2-auto-sinescolta") || Arg("-dia2-auto-tren-aparcado")) AutoDia2();
        }

        /// <summary>Cámara pegada al tren seguido (D-N170). Se suelta si el jugador toca la cámara o si el tren para.</summary>
        void Seguir()
        {
            if (!_seguir.Activo || W == null) return;
            var op = FindFirstObjectByType<OperationsMap3D>(); int i = _seguir.Tren;
            if (op == null || i >= op.TrenesVistos || i >= W.Services.Count) { _seguir.Empezar(-1); return; }
            var kb = UnityEngine.InputSystem.Keyboard.current; var ms = UnityEngine.InputSystem.Mouse.current;
            bool movio = (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed || kb.qKey.isPressed || kb.eKey.isPressed || kb.rKey.isPressed || kb.fKey.isPressed
                         || kb.upArrowKey.isPressed || kb.downArrowKey.isPressed || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed || kb.jKey.wasPressedThisFrame))
                      || (ms != null && (Mathf.Abs(ms.scroll.ReadValue().y) > 0.01f || ms.rightButton.isPressed));
            var pos = op.TrenPos(i); double km = double.MaxValue;
            foreach (var n in W.Graph.Nodes) km = Math.Min(km, Vector3.Distance(new Vector3(pos.x, 0, pos.z), OperationsMap3D.MapToWorldFlat(n.XMeters, n.YMeters)));
            if (!_seguir.Paso(movio, !W.Services[i].AtStop, km)) { Debug.Log("[MAPA] deja de seguir el tren: " + _seguir.PorQueSuelta); return; }
            op.Foco = pos; op.DistanciaObjetivo = SeguirTren.Distancia(km);
        }

        void AbrirMenu(int node)
        {
            _vista = Vista.Menu; _nodoMenu = node;
            if (node == Nodo("Nules")) TutorialHud.Senal(TutorialGuion.Senales.PanelNulesAbierto);
        }
        void AbrirPreparar(int origen)
        {
            _vista = Vista.Preparar; _origenP = origen; Array.Clear(_carga, 0, _carga.Length); _destinoP = -1; _sinApoyo = false; _sinCarga = false;
            TutorialHud.Senal(TutorialGuion.Senales.PanelTrenAbierto);
        }
        void Cargar(int r, int d)
        {
            int sv = TrenPreparar.TrenEn(W, _origenP); if (sv < 0) return;
            int total = 0; foreach (var c in _carga) total += c;
            int hueco = TrenPreparar.Hueco(W, sv) - total;
            _carga[r] = Mathf.Clamp(_carga[r] + Mathf.Min(d, hueco), 0, Mathf.Min(NetWorld.MaxScheduleTransfer, W.Stock[_origenP, r]));
            if (_carga[r] > 0) { _sinCarga = false; TutorialHud.Senal(TutorialGuion.Senales.CargaPuesta); if (r == (int)Res.Materiales) TutorialHud.Senal(TutorialGuion.Senales.MaterialesPuestos); }
            if (r == (int)Res.Municion && _carga[r] > 0 && _origenP == Casa) TutorialHud.Viaje.MunicionPuesta();
        }
        public const string AvisoCerco = "Castellón está cercada. Ningún tren puede llegar hasta que se rompa el cerco.";   // D-N184 (Lucía)
        void PonerDestino(int node)
        {
            if (W != null && node >= 0 && node == W.Director.CerradaNodo) { _aviso = AvisoCerco; _avisoHasta = Time.unscaledTime + 5f; _avisoAmbar = true; return; }
            _destinoP = node;
            if (node == Nodo("Nules")) { TutorialHud.Senal(TutorialGuion.Senales.DestinoNules); if (_origenP == Casa) TutorialHud.Viaje.DestinoNules(); }
            if (node == Casa) TutorialHud.Senal(TutorialGuion.Senales.DestinoSagunto);
        }
        /// <summary>Ya ha salido un tren hacia Nules el día 2 (para el recordatorio de las 15:00 de Lucía).</summary>
        public static bool TrenHaciaNulesDia2;
        void Enviar()
        {
            if (W != null && _destinoP >= 0 && _destinoP == Nodo("Nules") && W.Tick / NetConst.DayTicks == 1) TrenHaciaNulesDia2 = true;
            int sv = TrenPreparar.TrenEn(W, _origenP); if (sv < 0 || _destinoP < 0 || Embarque.Ocupado) return;
            var p = TrenPreparar.Envio(W, sv, _destinoP, _carga); if (p == null) return;
            NetSession.PushPayload(NetCmdType.SetSchedule, sv, p);
            Empezar(sv, _origenP, _destinoP, _carga[(int)Res.Municion]);
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Union") Sfx.Play("tren_pitido_electrico", Vector3.zero, 0.36f, 10f);   // D-N170: en el mapa, en 2D a 0,36 (Nuria v2), como mucho una vez cada 10 s
            if (W.Besieged(_destinoP) && W.EscortSoldiers(sv) == 0 && Puertas.SePierde(W, sv, _destinoP)) {   // Lucía: solo si en su sitio de espera se pierde
                _aviso = Puertas.AvisoSitiada(W, _destinoP); _avisoHasta = Time.unscaledTime + 5f; _avisoAmbar = true; Debug.Log("[TREN] " + _aviso); }   // D-N177: en ámbar
            _vista = Vista.Nada;
            Debug.Log($"[TREN] enviado {W.Services[sv].Train.Name} de {W.Graph.Nodes[_origenP].Name} a {W.Graph.Nodes[_destinoP].Name}: carga {string.Join("/", _carga)}, ETA {TrenPreparar.HhMm(TrenPreparar.EtaDesdeParada(W, sv, _destinoP))}");
            TutorialHud.Senal(TutorialGuion.Senales.TrenEnviado);
            TutorialHud.Viaje.Enviado(sv, W.Graph.Nodes[_origenP].Name, W.Graph.Nodes[_destinoP].Name, _carga[(int)Res.Municion]);
        }
        /// <summary>Descargar fuera de casa (el flujo Sagunto → otra estación): la carga y la tropa se quedan allí y el tren vuelve a su origen.</summary>
        /// <summary>
        /// Descargar fuera de casa: la carga baja al andén con CmdCargo (orden de Nacho; con asedio, D-N166, la carga se queda a bordo) y la tropa se queda
        /// en la estación. El tren se queda aparcado: para volver, «Preparar tren» desde esa estación.
        /// </summary>
        void DescargarFuera()
        {
            if (Viaje < 0) return;
            var sv = W.Services[Viaje]; var delta = new int[NetConst.ResCount]; bool hay = false;
            for (int r = 0; r < NetConst.ResCount; r++) { delta[r] = -sv.Cargo[r]; hay |= sv.Cargo[r] > 0; }   // Lucía (decisiones 09-11, andén ocupado): a las puertas también baja la carga, como la tropa
            if (hay) { var c = NetWorld.CmdCargo(W.Tick, Viaje, Destino, delta); NetSession.A.Push(c); if (NetSession.B != null) NetSession.B.Push(c); }
            Embarque.BajarTodo(Viaje, Destino); _descargadoAqui = true; _vista = Vista.Nada;
            Debug.Log("[TREN] descarga en " + W.Graph.Nodes[Destino].Name + " (el tren se queda)");
            TutorialHud.Senal(TutorialGuion.Senales.Descargado);
            if (hay) TutorialHud.Viaje.Descargado(Viaje, W.Graph.Nodes[Destino].Name);      // con asedio la carga no baja (D-N166): el objetivo espera
        }

        static readonly string[] IconoDe = { "municion", "combustible", "materiales", "poblacion", "viveres" };
        static readonly Texture2D[] _iconos = new Texture2D[5];
        static Texture2D Icono(int r)
        {
            if (r < 0 || r >= IconoDe.Length) return null;
            if (_iconos[r] == null) _iconos[r] = Resources.Load<Texture2D>("Icons/cargas/vm_cargas_" + IconoDe[r] + (Screen.height >= 900 ? "_32" : "_16"));
            return _iconos[r];
        }

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

        static Rect NodoRect(int id)
        {
            var op = FindFirstObjectByType<OperationsMap3D>(); var cam = Camera.main;
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

        string NombrePeloton(int id)
        {
            var ses = NightRun.Session; var p = ses != null && ses.Uw != null ? ses.Uw.Platoons.Find(q => q.Id == id) : null;
            string n = ses != null && ses.PName != null && ses.PName.TryGetValue(id, out var nm) ? nm : p != null ? p.Kind.ToString() : "Pelotón " + id;
            return p != null ? n + " · " + p.AliveCount() : n;
        }
        static string NombreVeh(int i) { return i >= 0 && i < NightRun.Vehs.Count && NightRun.Vehs[i] != null ? NightRun.Vehs[i].Name : "Vehículo " + i; }

        void OnGUI()
        {
            if (W == null || IntroVista.Mostrando) return;
            GUI.depth = -50; Styles();
            float u = _s.fontSize; int sag = Casa, nul = Nodo("Nules");
            // clic en los nodos (botón invisible sobre el icono); doble clic en Sagunto: a la zona (D-N170)
            // D-N170: clic en una localización abre su menú (Sagunto y Nules); doble clic lleva directamente a ella (entra en su zona). Doble clic en un tren
            // en viaje: la cámara lo sigue de cerca hasta que el jugador la mueva (SeguirTren).
            var opG = FindFirstObjectByType<OperationsMap3D>();
            var ev = Event.current;
            for (int node = 0; node < W.Graph.Nodes.Count; node++)
            {
                var rn = NodoRect(node);
                if (rn.width <= 0 || !GUI.Button(rn, GUIContent.none, GUIStyle.none)) continue;
                if (_dobleClic.Clic("nodo:" + node, Time.unscaledTime, ev.mousePosition.x, ev.mousePosition.y))
                {
                    if (opG != null) { Debug.Log($"[MAPA] doble clic en {W.Graph.Nodes[node].Name}: a la zona"); _seguir.Empezar(-1); opG.EnterZone(node); }
                    continue;
                }
                if (node == sag || node == nul) { if (_vista == Vista.Menu && _nodoMenu == node) _vista = Vista.Nada; else AbrirMenu(node); }
            }
            if (opG != null && Camera.main != null)
                for (int i = 0; i < opG.TrenesVistos && i < W.Services.Count; i++)
                {
                    if (W.Services[i].AtStop) continue;                                    // solo trenes en viaje
                    var sp = Camera.main.WorldToScreenPoint(opG.TrenPos(i)); if (sp.z <= 0) continue;
                    int tp = 32 * Mathf.Max(1, Mathf.RoundToInt(Screen.height / 540f));
                    var rt = new Rect(sp.x - tp * 0.6f, Screen.height - sp.y - tp * 1.7f, tp * 1.2f, tp * 1.4f);
                    if (!GUI.Button(rt, GUIContent.none, GUIStyle.none)) continue;
                    if (_dobleClic.Clic("tren:" + i, Time.unscaledTime, ev.mousePosition.x, ev.mousePosition.y)) { _seguir.Empezar(i); Debug.Log($"[MAPA] doble clic en el tren {W.Services[i].Train.Name}: la cámara lo sigue"); }
                }
            // tren en camino: «Refuerzos de Nules · llegan en 0:xx» o «El tren llega a Nules en 0:xx»
            if (Viaje >= 0 && !_descargando && Viaje < W.Services.Count && !W.Services[Viaje].AtStop)
            {
                string txt = Destino == sag ? $"Refuerzos de {W.Graph.Nodes[Origen].Name} · llegan en {EtaTexto()}" : $"El tren llega a {W.Graph.Nodes[Destino].Name} en {EtaTexto()}";
                var r = new Rect(Screen.width * 0.5f - u * 14, u * 5.5f, u * 28, u * 2.2f);
                GUI.DrawTexture(r, _panel); GUI.Label(new Rect(r.x + u, r.y + u * 0.4f, r.width, r.height), txt, _s);
                TutorialHud.Objetivo(TutorialGuion.Resaltes.Eta, r);
            }
            if (Time.unscaledTime < _avisoHasta && _aviso != null)
            {
                var r = new Rect(Screen.width * 0.5f - u * 8, u * 8.2f, u * 16, u * 2.2f);
                GUI.DrawTexture(r, _panel); GUI.Label(new Rect(r.x + u, r.y + u * 0.4f, r.width, r.height), (_avisoAmbar ? "<b><color=#E08C30>" + _aviso + "</color></b>" : "<b>" + _aviso + "</b>"), _s);
            }
            if (_vista == Vista.Menu) Menu(_nodoMenu, u);
            else if (_vista == Vista.Preparar) PanelPreparar(u);
        }

        void Menu(int node, float u)
        {
            var rn = NodoRect(node); if (rn.width <= 0) rn = new Rect(Screen.width * 0.45f, Screen.height * 0.45f, 0, u * 2);   // nodo fuera de pantalla: en el centro
            var pel = W.Embarque.AtStation(node, PasajeroTipo.Peloton); var veh = W.Embarque.AtStation(node, PasajeroTipo.Vehiculo);
            int sv = TrenPreparar.TrenEn(W, node);
            bool llegoDeFuera = Viaje >= 0 && _llegado && !_descargando && !_descargadoAqui && Destino == node && node != Casa && sv == Viaje && Origen == Casa;
            float lh = u * 2.3f, w = u * 21, h = u * 2.6f + (pel.Count + veh.Count > 0 ? lh * (1 + (pel.Count + veh.Count + 1) / 2) : 0) + lh * 1.2f + (llegoDeFuera ? lh * 2.2f : 0);
            var r = new Rect(rn.xMax + 8, rn.y, w, h); if (r.xMax > Screen.width) r.x = rn.x - w - 8;
            GUI.DrawTexture(r, _panel);
            float x0 = r.x + u * 0.6f, ww = w - u * 1.2f, yy = r.y + u * 0.4f;
            GUI.Label(new Rect(x0, yy, ww, u * 2.2f), $"<b>{W.Graph.Nodes[node].Name}</b> · {W.Graph.Nodes[node].State}" + (sv >= 0 ? $" · tren {W.Services[sv].Train.Name} en el andén" : ""), _s); yy += u * 2.2f;
            if (pel.Count + veh.Count > 0)
            {
                GUI.Label(new Rect(x0, yy, ww, lh), "<b>Destacamento</b>", _s); yy += lh * 0.9f;
                var dr = new Rect(x0, yy, ww, lh * ((pel.Count + veh.Count + 1) / 2)); TutorialHud.Objetivo(TutorialGuion.Resaltes.TarjetasDestacamento, dr);
                int k = 0;
                foreach (var id in pel) { GUI.Box(new Rect(x0 + (k % 2) * (ww * 0.5f + 2), yy + (k / 2) * lh, ww * 0.5f - 2, lh * 0.9f), GUIContent.none); GUI.Label(new Rect(x0 + (k % 2) * (ww * 0.5f + 2) + u * 0.4f, yy + (k / 2) * lh + u * 0.3f, ww * 0.5f, lh), NombrePeloton(id), _s); k++; }
                foreach (var id in veh) { GUI.Box(new Rect(x0 + (k % 2) * (ww * 0.5f + 2), yy + (k / 2) * lh, ww * 0.5f - 2, lh * 0.9f), GUIContent.none); GUI.Label(new Rect(x0 + (k % 2) * (ww * 0.5f + 2) + u * 0.4f, yy + (k / 2) * lh + u * 0.3f, ww * 0.5f, lh), NombreVeh(id), _s); k++; }
                yy += lh * ((k + 1) / 2) + u * 0.2f;
            }
            if (llegoDeFuera)
            {
                var t = W.Services[Viaje];
                GUI.Label(new Rect(x0, yy, ww, lh), $"{t.Train.Name}: {t.CargoTotal()} u, {W.Embarque.CountAboard(Viaje, PasajeroTipo.Peloton)} pelotón, {W.Embarque.CountAboard(Viaje, PasajeroTipo.Vehiculo)} vehículo", _s); yy += lh;
                if (Boton(new Rect(x0, yy, ww, lh * 0.95f), "Descargar", true, TutorialGuion.Resaltes.BotonDescargar)) DescargarFuera();
                yy += lh * 1.2f;
            }
            if (Boton(new Rect(x0, yy, ww, lh * 0.95f), sv >= 0 && !llegoDeFuera ? "Preparar tren" : sv >= 0 ? "Preparar tren (descarga antes)" : "Sin tren en el andén", sv >= 0 && !llegoDeFuera, TutorialGuion.Resaltes.BotonPrepararTren)) AbrirPreparar(node);
        }

        void PanelPreparar(float u)
        {
            int org = _origenP, sv = TrenPreparar.TrenEn(W, org);
            if (sv < 0) { _vista = Vista.Nada; return; }
            float w = Mathf.Min(u * 36, Screen.width * 0.44f), x = Screen.width - w - 12, y = Screen.height * 0.08f, lh = u * 1.95f;
            var ses = NightRun.Session; var uw = ses != null ? ses.Uw : null; bool casa = org == Casa;
            // tropa: en casa, los pelotones de Sagunto que están en tierra (o subiendo/a bordo de este tren); fuera, el destacamento de esa estación
            var pels = new List<int>();
            var aBordoP = W.Embarque.Aboard(sv, PasajeroTipo.Peloton);
            if (casa) { if (uw != null) foreach (var p in uw.Platoons) if ((p.Aboard == 0 && W.Embarque.Find(PasajeroTipo.Peloton, p.Id) < 0) || aBordoP.Contains(p.Id) || Embarque.EnCola(PasajeroTipo.Peloton, p.Id)) pels.Add(p.Id); }
            else { pels.AddRange(W.Embarque.AtStation(org, PasajeroTipo.Peloton)); foreach (var id in aBordoP) if (!pels.Contains(id)) pels.Add(id); }
            if (pels.Count > 6) pels.RemoveRange(6, pels.Count - 6);
            // vehículos: en casa, los de Sagunto vivos, de tierra y sin embarcar; fuera, los del destacamento
            var vehs = new List<int>(); var aBordoV = W.Embarque.Aboard(sv, PasajeroTipo.Vehiculo);
            if (casa) { for (int k = 0; k < NightRun.Vehs.Count; k++) { var vi = NightRun.Vehs[k]; if (vi != null && vi.Vivo && vi.Tipo != "helicoptero" && (!vi.Embarcado || aBordoV.Contains(k) || Embarque.EnCola(PasajeroTipo.Vehiculo, k))) vehs.Add(k); } }
            else { vehs.AddRange(W.Embarque.AtStation(org, PasajeroTipo.Vehiculo)); foreach (var id in aBordoV) if (!vehs.Contains(id)) vehs.Add(id); }
            int filasP = Mathf.Max(1, (pels.Count + 1) / 2), filasV = (vehs.Count + 2) / 2;
            float h = u * 3.4f + lh * (NetConst.ResCount + 2.4f) + lh * (filasP + 1.2f) + lh * (filasV + 1.2f) + lh * 2.1f + lh * 1.5f;
            var r = new Rect(x, y, w, h); GUI.DrawTexture(r, _panel);
            float yy = y + u * 0.6f, x0 = x + u * 0.8f, ww = w - u * 1.6f;
            GUI.Label(new Rect(x0, yy, ww, u * 2.4f), $"Preparar tren · {W.Services[sv].Train.Name} · {W.Graph.Nodes[org].Name}", _t);
            if (GUI.Button(new Rect(x + w - u * 2.6f, yy, u * 2, u * 2), "×", _b)) { _vista = Vista.Nada; return; }
            yy += u * 2.8f;
            // carga
            int total = 0; foreach (var c in _carga) total += c;
            GUI.Label(new Rect(x0, yy, ww, lh), $"<b>Carga</b>  ({total} de {TrenPreparar.Hueco(W, sv)} u libres)", _s); yy += lh;
            var cargaR = new Rect(x0, yy, ww, lh * (NetConst.ResCount + 1)); TutorialHud.Objetivo(TutorialGuion.Resaltes.Cargas, cargaR);
            for (int res = 0; res < NetConst.ResCount; res++)
            {
                var ico = Icono(res); float ip = lh * 0.7f;                                     // iconos de carga de Bruno (pixel art, escala entera)
                if (ico != null) GUI.DrawTexture(new Rect(x0, yy + (lh - ip) * 0.35f, ip, ip), ico, ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(x0 + ip + u * 0.4f, yy + u * 0.3f, ww * 0.5f - ip, lh), $"{(res < ResNombre.Length ? ResNombre[res] : "Recurso " + res)} <color=#a8a49c>(hay {W.Stock[org, res]})</color>", _s);
                if (Boton(new Rect(x0 + ww * 0.56f, yy, lh * 1.3f, lh * 0.9f), "−10")) Cargar(res, -10);
                GUI.Label(new Rect(x0 + ww * 0.56f + lh * 1.45f, yy + u * 0.3f, lh * 1.6f, lh), $"<b>{_carga[res]}</b>", _s);
                if (Boton(new Rect(x0 + ww * 0.56f + lh * 2.9f, yy, lh * 1.3f, lh * 0.9f), "+10")) Cargar(res, 10);
                yy += lh;
            }
            if (Boton(new Rect(x0, yy, ww * 0.5f, lh * 0.9f), (_sinCarga ? "✓ " : "") + "Sin carga", total == 0)) { _sinCarga = true; TutorialHud.Senal(TutorialGuion.Senales.SinCarga); }
            yy += lh * 1.4f;
            // tropa
            GUI.Label(new Rect(x0, yy, ww, lh), $"<b>Tropa</b>  (vagones de tropa: {EmbarqueState.PelotonesPorTren}; 10 s por pelotón)" + (Embarque.Ocupado ? "  <color=#a8a49c>· subiendo…</color>" : ""), _s); yy += lh;
            var tropaR = new Rect(x0, yy, ww, lh * filasP); TutorialHud.Objetivo(TutorialGuion.Resaltes.ListaUnidades, tropaR);
            if (pels.Count == 0) GUI.Label(new Rect(x0, yy + u * 0.3f, ww, lh), $"<color=#a8a49c>Sin pelotones disponibles en {W.Graph.Nodes[org].Name}</color>", _s);
            for (int i = 0; i < pels.Count; i++)
            {
                int id = pels[i];
                bool abordo = aBordoP.Contains(id), subiendo = Embarque.EnCola(PasajeroTipo.Peloton, id); bool puede = Embarque.PorQueNo(sv, PasajeroTipo.Peloton, id, org) == null;
                var br = new Rect(x0 + (i % 2) * (ww * 0.5f + 2), yy + (i / 2) * lh, ww * 0.5f - 2, lh * 0.9f);
                if (Boton(br, (abordo ? "✓ " : subiendo ? "… " : "") + NombrePeloton(id), abordo || subiendo || puede))
                { if (!abordo && !subiendo) { Embarque.Subir(sv, PasajeroTipo.Peloton, id, org); TutorialHud.Senal(TutorialGuion.Senales.PelotonABordo); TutorialHud.Viaje.EscoltaPuesta(); } }
            }
            yy += lh * filasP + lh * 0.2f;
            // apoyo terrestre
            GUI.Label(new Rect(x0, yy, ww, lh), $"<b>Apoyo terrestre</b>  (plataformas: {EmbarqueState.VehiculosPorTren}; 20 s)", _s); yy += lh;
            var vehR = new Rect(x0, yy, ww, lh * filasV); TutorialHud.Objetivo(TutorialGuion.Resaltes.ListaVehiculos, vehR);
            for (int i = 0; i <= vehs.Count; i++)
            {
                var br = new Rect(x0 + (i % 2) * (ww * 0.5f + 2), yy + (i / 2) * lh, ww * 0.5f - 2, lh * 0.9f);
                if (i == vehs.Count) { if (Boton(br, (_sinApoyo ? "✓ " : "") + "Sin apoyo", aBordoV.Count == 0 && !Embarque.Ocupado)) { _sinApoyo = true; TutorialHud.Senal(TutorialGuion.Senales.SinApoyo); } break; }
                int vi = vehs[i];
                bool abordo = aBordoV.Contains(vi), cargando = Embarque.EnCola(PasajeroTipo.Vehiculo, vi); bool puede = Embarque.PorQueNo(sv, PasajeroTipo.Vehiculo, vi, org) == null;
                if (Boton(br, (abordo ? "✓ " : cargando ? "… " : "") + NombreVeh(vi), abordo || cargando || puede))
                {
                    if (!abordo && !cargando)
                    {
                        Embarque.Subir(sv, PasajeroTipo.Vehiculo, vi, org); _sinApoyo = false; TutorialHud.Senal(TutorialGuion.Senales.VehiculoABordo); TutorialHud.Viaje.EscoltaPuesta();
                        if (NombreVeh(vi).StartsWith("Lince")) TutorialHud.Senal(TutorialGuion.Senales.LinceABordo);
                    }
                }
            }
            yy += lh * filasV + lh * 0.2f;
            // destino y ETA
            GUI.Label(new Rect(x0, yy, ww * 0.3f, lh), "<b>Destino</b>", _s);
            int otro = casa ? Nodo("Nules") : Casa, cas = Nodo("Castellón");
            if (Boton(new Rect(x0 + ww * 0.28f, yy, ww * 0.3f, lh * 0.9f), (_destinoP == otro ? "✓ " : "") + W.Graph.Nodes[otro].Name, true, otro == Casa ? TutorialGuion.Resaltes.NodoSagunto : TutorialGuion.Resaltes.NodoNules)) PonerDestino(otro);
            bool casCerco = cas >= 0 && cas == W.Director.CerradaNodo, casOk = cas >= 0 && W.Stations.Founded[cas];
            if (Boton(new Rect(x0 + ww * 0.62f, yy, ww * 0.38f, lh * 0.9f), casCerco ? "Castellón (cercada)" : casOk ? (_destinoP == cas ? "✓ " : "") + "Castellón" : "Castellón (infestada)", casOk || casCerco))
            { if (casCerco) { _aviso = AvisoCerco; _avisoHasta = Time.unscaledTime + 5f; _avisoAmbar = true; Debug.Log("[TREN] " + _aviso); } else PonerDestino(cas); }   // D-N184
            yy += lh;
            string etaTxt = _destinoP >= 0 ? TrenPreparar.HhMm(TrenPreparar.EtaDesdeParada(W, sv, _destinoP)) : "—";
            var etaR = new Rect(x0, yy, ww, lh); GUI.Label(etaR, $"ETA: <b>{etaTxt}</b>  · ahora: {NetConst.Clock(W.Tick)}" + (_destinoP >= 0 ? $"  · {W.Graph.Nodes[_destinoP].Name}: {W.Graph.Nodes[_destinoP].State}" : ""), _s);
            TutorialHud.Objetivo(TutorialGuion.Resaltes.Eta, etaR);
            if (_destinoP >= 0) TutorialHud.Objetivo(_destinoP == Casa ? TutorialGuion.Resaltes.EstadoSagunto : TutorialGuion.Resaltes.EstadoNules, etaR);
            yy += lh * 1.1f;
            if (Boton(new Rect(x0, yy, ww, lh * 1.1f), Embarque.Ocupado ? "Subiendo a bordo…" : "Enviar", _destinoP >= 0 && !Embarque.Ocupado, TutorialGuion.Resaltes.BotonEnviar)) Enviar();
        }

        // ---------------------------------------------------------------- corridas automáticas
        float _autoT0 = -1; int _autoPaso;
        float AutoT() { if (_autoT0 < 0) _autoT0 = Time.unscaledTime; return Time.unscaledTime - _autoT0; }

        /// <summary>-trenpanel-auto: Sagunto → Nules con carga, un pelotón y un VAMTAC; descarga en Nules y vuelta.</summary>
        void AutoIdaVuelta()
        {
            float t = AutoT(); int sag = Casa, nul = Nodo("Nules");
            switch (_autoPaso)
            {
                case 0: if (t > 4f && TrenPreparar.TrenEn(W, sag) >= 0) { AbrirMenu(sag); _autoPaso++; } else if (t > 4f && W.Speed != 4) NetSession.Push(NetCmdType.SetSpeed, 4, 0); break;
                case 1: if (t > 6f) { Cap("1_menu_sagunto"); _autoPaso++; } break;
                case 2: if (t > 7f) { AbrirPreparar(sag); Cargar(0, 40); Cargar(2, 60); var ses = NightRun.Session; int sv = TrenPreparar.TrenEn(W, sag); if (ses != null && ses.Uw != null && ses.Uw.Platoons.Count > 0) Embarque.Subir(sv, PasajeroTipo.Peloton, ses.Uw.Platoons[0].Id, sag);
                        int vk = NightRun.Vehs.FindIndex(v => v != null && v.Vivo && v.Tipo == "vamtac"); if (vk >= 0) Embarque.Subir(sv, PasajeroTipo.Vehiculo, vk, sag); else _sinApoyo = true;
                        PonerDestino(nul); _autoPaso++; } break;
                case 3: if (t > 9f) { Cap("2_preparar_tren"); _autoPaso++; } break;
                case 4: if (t > 10f && !Embarque.Ocupado) { Cap("2b_a_bordo"); NetSession.Push(NetCmdType.SetSpeed, 4, 0); Enviar(); _autoPaso++; } break;
                case 5: if (t > 13f) { Cap("3_en_camino"); _autoPaso++; } break;
                case 6: if (Viaje >= 0 && _llegado) { AbrirMenu(nul); _autoT0 = Time.unscaledTime - 100f; _autoPaso++; } else if (t > 240f) { Debug.LogError("[TREN] auto: el tren no llegó a Nules"); Application.Quit(1); } break;
                case 7: if (t > 102f) { Cap("4_nules_descargar"); Debug.Log("[TREN] auto: vehículos embarcados: " + string.Join(", ", NightRun.Vehs.FindAll(v => v != null && v.Embarcado).ConvertAll(v => v.Name))); _autoPaso++; } break;
                case 8: if (t > 103f) { DescargarFuera(); _autoPaso++; } break;
                case 9: if (Viaje < 0) { Debug.Log($"[TREN] auto: ciclo completo; Nules tiene {W.Stock[nul, 0]} de munición y {W.Stock[nul, 2]} de materiales"); Cap("5_de_vuelta"); _autoT0 = Time.unscaledTime - 200f; _autoPaso++; } else if (t > 340f) { Debug.LogError("[TREN] auto: el tren no volvió"); Application.Quit(1); } break;
                case 10: if (t > 203f) { Debug.Log("[TREN] auto: fin"); Application.Quit(0); _autoPaso++; } break;
            }
        }

        /// <summary>
        /// -dia2-auto (Marcos, T8 y T9 de la qa50): avanza la red al día 2 (Turbo), manda el primer tren de Sagunto a Nules y luego el segundo con un pelotón y 20 u
        /// (el segundo que llega el día 2 dispara la emboscada de Nacho), espera a las 21:00 y entra en la zona de Nules para la noche 2. Con -nightbot la juega el bot
        /// y NightEnded deja resultado= y causa=. Registra [DIA2] en cada paso.
        /// </summary>
        /// <summary>-dia2-auto: la zona de Sagunto, al ver esto, vuelve al mapa en cuanto crea su sesión (así hay pelotón que embarcar). Se apaga al usarse.</summary>
        public static bool Dia2Volver; static bool _dia2ZonaHecha;
        void AutoDia2()
        {
            float t = AutoT(); int sag = Casa, nul = Nodo("Nules"); long dia2 = NetConst.DayTicks; bool escolta = !Arg("-dia2-auto-sinescolta");
            if (_autoPaso == 0 && escolta && !_dia2ZonaHecha) { if (t > 3f) { _dia2ZonaHecha = true; Dia2Volver = true; Debug.Log("[DIA2] paso por Sagunto para crear la sesión de la zona"); var mz = FindObjectOfType<OperationsMap3D>(); if (mz != null) mz.EnterZone(sag); } return; }
            switch (_autoPaso)
            {
                case 0: if (t > 3f) { NetSession.Turbo = 40; NetSession.Push(NetCmdType.SetSpeed, 4, 0); Debug.Log("[DIA2] avanzo la red al día 2"); _autoPaso++; } break;
                case 1: if (W.Tick >= dia2 + NetConst.Hours(6.5)) { NetSession.Turbo = 1; Debug.Log($"[DIA2] día 2, {NetConst.Clock(W.Tick)}"); _autoT0 = Time.unscaledTime; _autoPaso++; } break;
                case 2: if (t > 2f && TrenPreparar.TrenEn(W, sag) >= 0 && !Embarque.Ocupado) { AbrirPreparar(sag); PonerDestino(nul); Enviar(); Debug.Log("[DIA2] primer tren a Nules"); _autoT0 = Time.unscaledTime; _autoPaso++; } else if (t > 30f) { Debug.LogError("[DIA2] no hay tren en Sagunto"); _autoPaso = 99; } break;
                case 3: if (TrenPreparar.TrenEn(W, nul) >= 0 && !Embarque.Ocupado) { AbrirPreparar(nul); PonerDestino(sag); Enviar(); Debug.Log("[DIA2] el primer tren vuelve a Sagunto (deja libre el andén de Nules)"); _autoT0 = Time.unscaledTime; _autoPaso++; }
                        else if (t > 60f) { Debug.LogWarning("[DIA2] el primer tren no ha llegado a Nules"); _autoT0 = Time.unscaledTime; _autoPaso++; } break;
                case 4: if (t > 4f && TrenPreparar.TrenEn(W, sag) >= 0 && !Embarque.Ocupado)
                    {
                        AbrirPreparar(sag); Cargar((int)Res.Municion, 25); int sv = TrenPreparar.TrenEn(W, sag); var ses = NightRun.Session;   // 25: la emboscada rechazada gasta ~1 u de la carga y el objetivo pide 20 en el almacén
                        if (!escolta) Debug.Log("[DIA2] sin escolta (-dia2-auto-sinescolta)");
                        else if (ses != null && ses.Uw != null && ses.Uw.Platoons.Count > 0) { Embarque.Subir(sv, PasajeroTipo.Peloton, ses.Uw.Platoons[0].Id, sag); Debug.Log("[DIA2] sube el pelotón " + ses.Uw.Platoons[0].Kind); }
                        else Debug.LogWarning("[DIA2] sin pelotón que subir (no hay sesión de zona)");
                        _autoT0 = Time.unscaledTime; _autoPaso++;
                    }
                    else if (t > 30f) { Debug.LogError("[DIA2] no hay segundo tren en Sagunto"); _autoPaso = 99; } break;
                case 5: if (t > 2f && !Embarque.Ocupado) { PonerDestino(nul); Enviar(); Debug.Log($"[DIA2] segundo tren a Nules {(escolta ? "con pelotón" : "sin escolta")} y 25 u"); _autoPaso++; } break;
                case 6: if (W.LastAmbush.Tick >= 0) { var e = W.LastAmbush; Debug.Log($"[DIA2] emboscada: nodo {W.Graph.Nodes[e.Node].Name}, {e.Zombies} zombis, en pantalla {e.OnScreen}, rechazada {e.Repelled}, cercado {e.Surrounded}"); NetSession.Turbo = 20; _autoPaso++; }
                        else if (W.Tick >= dia2 + NetConst.Hours(20)) { Debug.LogWarning("[DIA2] sin emboscada a las 20:00"); NetSession.Turbo = 20; _autoPaso++; } break;
                case 7: if (W.LastAmbush.TrainLost || (W.LastAmbush.Surrounded && !W.LastAmbush.Repelled)) { Debug.Log("[DIA2] el tren está cercado o perdido en la emboscada: no hay descarga"); _autoPaso++; }
                        else if (Viaje >= 0 && W.Services[Viaje].AtStop && W.Services[Viaje].Stops[W.Services[Viaje].StopIndex].Node == nul && !Puertas.EnPuertas(W, Viaje, nul)) { NetSession.Turbo = 1; DescargarFuera(); Debug.Log($"[DIA2] el tren entra en Nules y descarga ({NetConst.Clock(W.Tick)})"); NetSession.Turbo = 20; _autoPaso++; }
                        else if (W.Tick >= dia2 + NetConst.Hours(20.5)) { Debug.Log("[DIA2] el tren no ha entrado en Nules (sigue a las puertas o perdido)"); _autoPaso++; } break;
                case 8: if (W.Tick >= dia2 + NetConst.Hours(19.5))      // como un jugador que hace caso del aviso de las 19:00: el tren vuelve a Sagunto (salvo -dia2-auto-tren-aparcado)
                    {
                        if (Arg("-dia2-auto-tren-aparcado")) Debug.Log("[DIA2] el tren se queda aparcado en Nules (-dia2-auto-tren-aparcado)");
                        else if (TrenPreparar.TrenEn(W, nul) >= 0 && !Embarque.Ocupado) { NetSession.Turbo = 1; AbrirPreparar(nul); PonerDestino(sag); Enviar(); Debug.Log($"[DIA2] {NetConst.Clock(W.Tick)}: el tren sale de Nules hacia Sagunto antes de la noche"); NetSession.Turbo = 20; }
                        else Debug.Log("[DIA2] no hay tren en Nules que sacar");
                        _autoPaso++;
                    } break;
                case 9: if (W.Tick >= dia2 + NetConst.Hours(21)) { NetSession.Turbo = 1; Debug.Log($"[DIA2] {NetConst.Clock(W.Tick)}: objetivo del día 2 {(W.ObjMet ? "cumplido" : "no cumplido")} (tropa {W.ObjTroops}, munición {W.Stock[nul, (int)Res.Municion]} u); entro en Nules para la noche 2"); var mapa = FindObjectOfType<OperationsMap3D>(); if (mapa != null) mapa.EnterZone(nul); else Debug.LogError("[DIA2] no hay mapa"); _autoPaso++; } break;
            }
        }

        /// <summary>-refuerzos-auto (con -tutorial): capítulo 3 en el mapa. Abre Nules, sube el destacamento, carga materiales y lo manda a Sagunto; la bajada la hace TrenAnden.</summary>
        void AutoRefuerzos()
        {
            float t = AutoT(); int sag = Casa, nul = Nodo("Nules");
            switch (_autoPaso)
            {
                case 0: if (t > 3f)
                    {
                        // PRUEBA: si la zona aún no ha creado el destacamento (enganche de Marcos), lo crea la corrida para poder recorrer el capítulo
                        var ses = NightRun.Session;
                        if (W.Embarque.AtStation(nul, PasajeroTipo.Peloton).Count == 0 && ses != null && ses.Uw != null)
                        {
                            var p = ses.Uw.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0);
                            TutorialHud.RegistrarDestacamento(ses.Uw, p.Id);
                            Debug.Log("[TREN] auto: destacamento de prueba creado (falta el enganche de la zona)");
                        }
                        if (TrenPreparar.TrenEn(W, nul) < 0) { Destacamento.SembrarTren(W); if (NetSession.B != null) Destacamento.SembrarTren(NetSession.B); }
                        AbrirMenu(nul); _autoPaso++;
                    }
                    break;
                case 1: if (t > 5f) { Cap("r1_menu_nules"); AbrirPreparar(nul); _autoPaso++; } break;
                case 2: if (t > 6f) { int sv = TrenPreparar.TrenEn(W, nul); if (sv < 0) { Debug.LogError("[TREN] auto: no hay tren en Nules"); Application.Quit(1); return; }
                        foreach (var id in W.Embarque.AtStation(nul, PasajeroTipo.Peloton)) Embarque.Subir(sv, PasajeroTipo.Peloton, id, nul);
                        foreach (var id in W.Embarque.AtStation(nul, PasajeroTipo.Vehiculo)) Embarque.Subir(sv, PasajeroTipo.Vehiculo, id, nul);
                        Cargar((int)Res.Materiales, 30); PonerDestino(sag); _autoPaso++; } break;
                case 3: if (t > 8f) { Cap("r2_preparar_nules"); _autoPaso++; } break;
                case 4: if (!Embarque.Ocupado && t > 9f) { Cap("r3_a_bordo"); Enviar(); NetSession.Push(NetCmdType.SetSpeed, 4, 0); _autoPaso++; } break;
                case 5: if (t > 14f) { Cap("r4_refuerzos_en_camino"); var op = FindFirstObjectByType<OperationsMap3D>(); if (op != null) { Debug.Log("[TREN] auto: vuelvo a Sagunto"); op.EnterZone(sag); } _autoPaso++; } break;
            }
        }

        public static void Cap(string n)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "tren")); Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, n + ".png")); Debug.Log("[TREN] captura " + n);
        }
    }

    /// <summary>
    /// En la zona de Sagunto: el tren que ha llegado con refuerzos, con su botón «Descargar» (capítulo 3, pasos 3.12 y 3.12b). Si Sagunto está sitiada
    /// (D-N166), el tren espera a las puertas y solo baja la tropa; el vehículo se queda a bordo hasta que acabe el asedio. La tropa aparece en PuntoBajada
    /// (junto al muro oeste; lo pone la zona, Marcos) y tarda 10 s por pelotón; el vehículo, 20 s.
    /// </summary>
    public sealed class TrenAnden : MonoBehaviour
    {
        public static Vector2Int? PuntoBajada;     // mm de la zona: el tren de Nules entra por el este y para fuera de la puerta este; la tropa baja en el costado sur (Álvaro, §10.10). Lo pone Marcos; sin él, junto al primer pelotón de Sagunto
        public static readonly Vector2Int ObjetivoAtaque = new Vector2Int(100_000, 34_000);   // Álvaro §10.10: flanco sur de la horda, (100; 34) m, para el anillo del paso 3.8
        GUIStyle _s, _b; Texture2D _panel, _btn, _btnHot; bool _bajando; int _sh;
        static readonly Color Bone = new Color32(0xd4, 0xd0, 0xcc, 255), Ink = new Color32(0x1c, 0x20, 0x0c, 255);
        static NetWorld W { get { return NetSession.A; } }

        readonly HashSet<int> _caidos = new HashSet<int>();
        // salida del tren a la vista (D-N170, sonidos de Nuria): nadie llamaba a SalidaTrenSonido en la zona, así que no sonaba; al salir un tren de la estación de la zona
        // se toca la secuencia (aviso, cierre de puertas, pitido en la cabeza y arranque) en el andén (143; 64,5), con la cabeza 20 m al este
        readonly Dictionary<int, bool> _antes = new Dictionary<int, bool>();
        static readonly bool _demo = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-salidademo") >= 0; bool _demoHecho;   // -salidademo: una salida a los 10 s de entrar en la zona
        readonly List<SalidaTren> _salidas = new List<SalidaTren>(); readonly List<SonidoSalida> _sonSal = new List<SonidoSalida>();
        static readonly Vector3 AndenZona = new Vector3(143f, 1f, 64.5f), CabezaZona = new Vector3(163f, 2f, 64.5f);
        void SalidasALaVista()
        {
            if (!_demoHecho && _demo && Time.timeSinceLevelLoad > 10f) { _demoHecho = true; _salidas.Add(new SalidaTren(TipoTren.Electrico, 60f, 7u, 4, false)); Debug.Log("[TREN] sale a la vista: -salidademo"); }   // prueba de oído (Nuria, Eugenio)
            if (W != null)
            {
            int casa = W.Embarque.HomeNode;
            for (int i = 0; i < W.Services.Count; i++)
            {
                var s = W.Services[i]; bool enCasa = W.TrainAtNode(s, casa) && s.GateNode < 0;
                if (_antes.TryGetValue(i, out var antes) && antes && !enCasa && !s.Destroyed)
                {
                    _salidas.Add(new SalidaTren(TipoTren.Electrico, 60f, TrenPreparar.SemillaSalida(s.Train.Id, casa, W.Tick / NetConst.DayTicks), 4, false));
                    Debug.Log("[TREN] sale a la vista: " + s.Train.Name);
                }
                _antes[i] = enCasa;
            }
            }
            for (int k = _salidas.Count - 1; k >= 0; k--)
            {
                var sal = _salidas[k]; _sonSal.Clear(); sal.Avanzar(Time.deltaTime, _sonSal);
                SalidaTrenSonido.Tocar(_sonSal, pu => AndenZona + new Vector3(-8f + 5f * pu, 0f, 0f), AndenZona, CabezaZona + new Vector3(sal.Distancia, 0f, 0f));
                if (sal.T > 20f) _salidas.RemoveAt(k);
            }
        }

        void Update()
        {
            TrenPanel.Vigilar();
            SalidasALaVista();
            if (W != null)       // D-N177: el tren cae a las puertas de casa → sfx_tren_caido_puertas en la puerta (Nuria), una vez por tren
                for (int i = 0; i < W.Services.Count; i++)
                {
                    var s = W.Services[i]; if (!s.Destroyed) { _caidos.Remove(i); continue; }
                    if (!_caidos.Add(i) || s.GateNode != W.Embarque.HomeNode) continue;
                    var pt = PuntoBajada ?? Defecto(); Sfx.PlayAt("tren_caido_puertas", new Vector3(pt.x / 1000f, 0f, pt.y / 1000f), 1f);
                    Debug.Log("[TREN] " + s.Train.Name + " cae a las puertas de " + W.Graph.Nodes[W.Embarque.HomeNode].Name);
                }
            if (System.Array.IndexOf(Environment.GetCommandLineArgs(), "-refuerzos-auto") >= 0 && !_bajando && Hay(out var sv, out _)) { _autoT += Time.unscaledDeltaTime; if (_autoT > 3f) { TrenPanel.Cap("r5_anden_sagunto"); Descargar(sv); } }
        }
        float _autoT;

        bool Hay(out int sv, out bool sitiada)
        {
            sv = TrenPanel.Viaje; sitiada = false;
            if (W == null || sv < 0 || sv >= W.Services.Count || TrenPanel.Destino != W.Embarque.HomeNode) return false;
            if (!W.TrainAtNode(W.Services[sv], W.Embarque.HomeNode)) return false;
            sitiada = W.Besieged(W.Embarque.HomeNode);
            int n = W.Embarque.CountAboard(sv, PasajeroTipo.Peloton) + (sitiada ? 0 : W.Embarque.CountAboard(sv, PasajeroTipo.Vehiculo));
            return n > 0 || _bajando;
        }

        void Descargar(int sv)
        {
            if (_bajando) return; _bajando = true;
            bool sitiada = W.Besieged(W.Embarque.HomeNode);
            Vector2Int pt = PuntoBajada ?? Defecto();
            Debug.Log($"[TREN] descarga en Sagunto{(sitiada ? " (sitiada: solo la tropa)" : "")} en ({pt.x}, {pt.y})");
            TrenPanel.Embarque.BajarEnCasa(sv, W.Embarque.HomeNode, pt.x, pt.y, sitiada, () =>
            {
                _bajando = false;
                TutorialHud.Senal(sitiada ? TutorialGuion.Senales.TropaEnTierra : TutorialGuion.Senales.RefuerzosEnTierra);
                if (System.Array.IndexOf(Environment.GetCommandLineArgs(), "-refuerzos-auto") >= 0) { TrenPanel.Cap("r6_refuerzos_en_tierra"); Debug.Log("[TREN] auto: refuerzos en tierra; fin"); Application.Quit(0); }
            });
        }

        static Vector2Int Defecto()
        {
            var uw = NightRun.Session != null ? NightRun.Session.Uw : null;
            if (uw != null) foreach (var p in uw.Platoons) if (p.Aboard == 0 && p.AliveCount() > 0) return new Vector2Int(p.AX - 6000, p.AY);
            return Vector2Int.zero;
        }

        static Texture2D Tex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

        void OnGUI()
        {
            if (!Hay(out int sv, out bool sitiada)) return;
            GUI.depth = -60;
            if (_s == null || _sh != Screen.height)
            {
                _sh = Screen.height; _panel = Tex(new Color(0.07f, 0.08f, 0.05f, 0.94f)); _btn = Tex(new Color(0.16f, 0.18f, 0.10f, 1f)); _btnHot = Tex(Bone);
                int u0 = Mathf.Max(10, Screen.height / 60);
                _s = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u0 * 1.15f), richText = true, wordWrap = true }; _s.normal.textColor = Bone;
                _b = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u0 * 1.2f), fontStyle = FontStyle.Bold }; _b.normal.background = _btn; _b.hover.background = _btnHot; _b.active.background = _btnHot; _b.normal.textColor = Bone; _b.hover.textColor = Ink; _b.active.textColor = Ink;
            }
            float u = _s.fontSize, w = u * 30, h = u * (sitiada ? 7.6f : 5.8f);
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.62f, w, h);
            GUI.DrawTexture(r, _panel); TutorialHud.Objetivo(TutorialGuion.Resaltes.TrenAnden, r);
            var t = W.Services[sv];
            string quien = $"{W.Embarque.CountAboard(sv, PasajeroTipo.Peloton)} pelotón, {W.Embarque.CountAboard(sv, PasajeroTipo.Vehiculo)} vehículo" + (t.CargoTotal() > 0 ? $", {t.CargoTotal()} u de carga" : "");
            GUI.Label(new Rect(r.x + u, r.y + u * 0.5f, w - 2 * u, u * 2.4f), $"<b>{t.Train.Name}</b> " + (sitiada ? "espera a las puertas" : "en el andén") + ": " + quien, _s);
            if (sitiada) GUI.Label(new Rect(r.x + u, r.y + u * 2.4f, w - 2 * u, u * 2.4f), "<color=#c8c0a8>Sagunto está sitiada: solo baja la tropa (D-N166).</color>", _s);
            var br = new Rect(r.x + u, r.yMax - u * 3.0f, w - 2 * u, u * 2.4f); TutorialHud.Objetivo(TutorialGuion.Resaltes.BotonDescargar, br);
            if (GUI.Button(br, _bajando ? "Bajando…" : sitiada ? "Bajar la tropa" : "Descargar", _b) && !_bajando) Descargar(sv);
        }
    }
}
