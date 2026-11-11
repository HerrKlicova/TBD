using System.Collections.Generic;
using TBD.Net;
using TBD.Units;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Reloj único de la noche (D-N186, opción 1 de Sara; diseño de Nacho en nacho/reloj-unico). Las noches de las zonas viven en la red: NetWorld.NochesZona
    /// tiene una NightSim por nodo en el mundo A y otra, idéntica, en el B (si no, la prueba de desincronización salta). Solo NetWorld.Step las avanza
    /// (PasoNoche, y nada antes de las 22:00: ComienzaNoche). Ya no hay NightClock.
    ///  - Al empezar la partida se registran las noches de Sagunto y de Nules (con su plantel de la D-N183), así que entrar a las 23:00 encuentra la noche empezada.
    ///  - Al entrar en la zona, su NightSim sale de NochesZona en A y en B y la zona se monta desde ella (ResumeNightSession).
    ///  - Al salir, el estado de la zona se vuelca en dos NightSim nuevas e idénticas (copias por SaveState/LoadState) y vuelven a NochesZona.
    /// Pendiente (Nacho, condición 1): con la zona en pantalla, un paso de zona por tick de red; hoy la zona va a su ritmo (de día está parada, ver DiaEnLaRed).
    /// </summary>
    public partial class PrototypeBootstrap
    {
        public static readonly string[] EstacionesConNoche = { "Sagunto", "Nules" };

        /// <summary>¿Van las noches con el reloj de la red? En partida (NightRun.Active con red), no en el tutorial (su noche empieza al momento) ni con -reloj-propio (pruebas viejas).</summary>
        public static bool RelojUnicoActivo { get { return NightRun.Active && NetSession.A != null && !TutorialHud.Activo && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-reloj-propio") < 0 && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-zona") < 0; } }      // Marcos (qa52): las corridas de QA con -zona van con su reloj propio (la noche empieza al entrar)
        static NetWorld _registradoEn;
        static readonly Dictionary<int, NightParams> _base = new Dictionary<int, NightParams>(); static string _dif = "normal"; static int _diaAjustado = -1;
        /// <summary>D-N190: un NH90 deja «u» de munición en «nodo»: a la reserva de la red (orden AirSupply, A y B en el mismo tick) y, si su noche está en marcha,
        /// al almacén de esa noche (tope 47 u): en la zona abierta (la recoge NightTick) o en las dos copias de la red (fuera de Step: A y B iguales).</summary>
        public static void EntregaAerea(int nodo, int u)      // F1 (Nacho): la red ya lo ha dejado en la reserva y en la noche registrada (AirDeliver); aquí solo la zona abierta
        {
            if (u <= 0 || nodo < 0) return;
            if (NightRun.Session != null && NightRun.Session.Node == nodo && !NightRun.Session.Away) _entregaZona += u * WeaponCatalog.ShotsPerU;
            Debug.Log($"[AERO] D-N190: {u} u de munición en el nodo {nodo}");
        }
        static int _entregaZona;
        void RecogerEntregaAerea() { if (_entregaZona <= 0 || _uw == null) return; if (_ng != null && (_ng.StartNetTick >= 0 || !_ng.P.NetClock)) _uw.StockShots = System.Math.Min(47 * WeaponCatalog.ShotsPerU, _uw.StockShots + _entregaZona); _entregaZona = 0; }
        /// <summary>Parámetros de la noche «n» de una estación (n = 1 la primera). D-N191 (Lucía; Hugo con medida de Nacho): la noche 3 de Sagunto tiene 5250 / 6700 / 8150 zombis
        /// (fácil / normal / difícil) frente a 4480 / 5250 / 6550 de la primera; la 2.ª repite la primera.</summary>
        public static NightParams ParamsNoche(int nodo, string est, int n, NightParams actual)
        {
            if (!_base.TryGetValue(nodo, out var b)) b = actual;
            var p = b;
            if (est == "Sagunto" && n >= 3) { int obj = _dif == "facil" ? 5250 : _dif == "dificil" ? 8150 : 6700, ini = _dif == "facil" ? 4480 : _dif == "dificil" ? 6550 : 5250; p.ScalePermil = (int)((long)b.ScalePermil * obj / ini); }
            return p;
        }
        /// <summary>Una vez por día de red, antes de las 22:00: la noche siguiente de cada noche registrada (A y B, fuera de Step) y la de la zona abierta.</summary>
        public static void AjustarNochesSiguientes()
        {
            var a = NetSession.A; if (a == null || a.NochesZona.Count == 0 && NightRun.Session == null) return;
            int dia = NetWorld.DayOf(a.Tick); if (dia == _diaAjustado) return; _diaAjustado = dia;
            foreach (var w in new[] { a, NetSession.B })
            {
                if (w == null || (w == NetSession.B && w == a)) continue;
                foreach (var kv in w.NochesZona) if (kv.Value is NightSim ns) ns.NextNightParams = ParamsNoche(kv.Key, w.Graph.Nodes[kv.Key].Name, w.NightNumber(kv.Key, dia), ns.Night.P);
            }
            foreach (var ses in new List<NightSession>(NightRun.Aparcadas.Values) { NightRun.Session })
                if (ses != null && ses.Node >= 0 && ses.Ng != null) ses.Next = ParamsNoche(ses.Node, a.Graph.Nodes[ses.Node].Name, a.NightNumber(ses.Node, dia), ses.Ng.P);
        }
        /// <summary>Una vez por mundo de red (partida nueva o cargada): registra las noches iniciales. Si ya hay una noche con reloj propio en marcha (tutorial que sigue), no toca nada.</summary>
        public static void AsegurarNochesRed()
        {
            var a = NetSession.A; if (a == null || a == _registradoEn || !RelojUnicoActivo) return;
            _registradoEn = a;
            bool vieja = (NightRun.Session != null && NightRun.Session.Ng != null && !NightRun.Session.Ng.P.NetClock); foreach (var ps in NightRun.Aparcadas.Values) vieja |= ps != null && ps.Ng != null && !ps.Ng.P.NetClock;
            if (vieja) { Debug.Log("[RELOJ] hay una noche con reloj propio en marcha (tutorial): no se registran noches en la red"); return; }
            ClearNightSession(true);
            RegistrarNochesIniciales();
        }

        /// <summary>El tutorial (u otra noche con reloj propio) arranca después de registrar las noches: se quitan de la red (A y B) y de las sesiones, y no se vuelven a registrar en este mundo.
        /// La zona monta entonces su noche nueva con reloj propio, como antes del reloj único (Eugenio, I-Noche150: con el tutorial no venían zombis y a las 05:43 era de día).</summary>
        public static void AnularRelojUnico(string porQue)
        {
            var a = NetSession.A; if (a == null) return;
            int n = a.NochesZona.Count;
            ClearNightSession(true);
            foreach (var w in new[] { NetSession.A, NetSession.B }) if (w != null) w.NochesZona.Clear();
            _registradoEn = a;
            Debug.Log($"[RELOJ] {porQue}: fuera las {n} noche(s) registradas; la zona va con reloj propio");
        }

        // ---------------------------------------------------------------- la zona en pantalla
        long _tickRedZona = -1; bool _pausaRedPuesta;
        /// <summary>¿Va la zona al ritmo de la red? (noche con reloj de la red y red presente).</summary>
        bool ZonaConRelojRed { get { return NightRun.Active && _nightInit && _ng != null && _ng.P.NetClock && NetSession.A != null; } }
        /// <summary>Condición 1 de Nacho: un paso de zona por tick de red, siempre después de net.Step() (NetSession.Update corre antes; aquí se reproducen los ticks que ha dado).
        /// Devuelve los pasos dados. Con mucho retraso (zona muy lenta) se salta lo que sobra y se avisa: la noche de la zona queda detrás de la red.</summary>
        int PasosZonaRed()
        {
            var w = NetSession.A; if (_tickRedZona < 0 || _tickRedZona > w.Tick) _tickRedZona = w.Tick;
            if (TurboN > 1 && NetSession.Turbo < TurboN) NetSession.Turbo = TurboN;      // corridas por lotes (-turbo N): la red corre N veces y la zona la sigue
            int tope = 8 * System.Math.Max(1, TurboN) * System.Math.Max(1, NetSession.Turbo);
            int n = 0;
            while (_tickRedZona < w.Tick && n < tope) { _tickRedZona++; StepSimulation(); n++; }
            bool frena = w.Tick - _tickRedZona > tope;      // Nacho: sin saltar ticks; si la zona no da abasto, la red espera
            if (frena != NetSession.Frenada) { NetSession.Frenada = frena; if (frena) Debug.Log($"[RELOJ] la red espera a la zona ({w.Tick - _tickRedZona} ticks de retraso)"); }
            return n;
        }
        /// <summary>La pausa de la zona (Esc) pausa la red, para que la red no siga sin la zona.</summary>
        void PausaZonaEnRed()
        {
            var w = NetSession.A; if (w == null) return;
            if (_paused && !_pausaRedPuesta) { if (!w.Paused) NetSession.Push(NetCmdType.Pause, 0, 0); _pausaRedPuesta = true; }
            else if (!_paused && _pausaRedPuesta) { if (w.Paused) NetSession.Push(NetCmdType.Resume, 0, 0); _pausaRedPuesta = false; }
        }
        /// <summary>Zona abierta de día con el reloj único: la noche aún no ha empezado; la zona enseña la hora de la red y su luz, nunca la de su noche.</summary>
        bool DiaZonaRed { get { return _ng != null && _ng.P.NetClock && _ng.StartNetTick < 0 && NetSession.A != null; } }
        int _reservaVista = -1;
        /// <summary>De día, el almacén de la zona sigue a la reserva de la red (tope 47 u): lo que descarga un tren entra al momento; lo que se gasta en la emboscada no vuelve.</summary>
        void AlmacenDeDia()
        {
            var w = NetSession.A; if (w == null || _zoneNode < 0 || _uw == null) return;
            int res = System.Math.Max(0, w.Stock[_zoneNode, (int)Res.Municion]);
            if (_reservaVista < 0) { _uw.StockShots = System.Math.Min(47, res) * WeaponCatalog.ShotsPerU; _reservaVista = res; return; }
            if (res > _reservaVista) _uw.StockShots = System.Math.Min(47 * WeaponCatalog.ShotsPerU, _uw.StockShots + (res - _reservaVista) * WeaponCatalog.ShotsPerU);
            _reservaVista = res;
        }
        GUIStyle _avisoSt; Texture2D _avisoPx;
        /// <summary>El aviso de NetSession (primer contacto de la horda, ×1) en la zona, igual que en el mapa: arriba al centro, ámbar sobre oscuro.</summary>
        void AvisoNocheZona()
        {
            if (Time.unscaledTime >= NetSession.AvisoNocheHasta || string.IsNullOrEmpty(NetSession.AvisoNoche)) return;
            if (_avisoSt == null) { _avisoSt = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 42), richText = true, alignment = TextAnchor.MiddleCenter }; _avisoPx = new Texture2D(1, 1); _avisoPx.SetPixel(0, 0, Color.white); _avisoPx.Apply(); }
            var sz = _avisoSt.CalcSize(new GUIContent(NetSession.AvisoNoche)); var r = new Rect((Screen.width - sz.x) * 0.5f - 8f, Screen.height * 0.12f, sz.x + 16f, sz.y + 8f);
            var o = GUI.color; GUI.color = new Color(0.07f, 0.08f, 0.05f, 0.9f); GUI.DrawTexture(r, _avisoPx); GUI.color = o;
            GUI.Label(r, "<color=#e08c30>" + NetSession.AvisoNoche + "</color>", _avisoSt);
        }
        readonly HashSet<long> _asedioVisto = new HashSet<long>();
        /// <summary>Regla de Hugo y Nacho (aprobada por Lucía, decisiones-noche): al anunciarse el asedio de esta estación, los pelotones propios a menos de 40 m del tren
        /// descargado entran andando por la puerta (Move al puesto de dentro, sin teletransporte) y pasan a la guarnición (el bot ya puede usarlos). Los que el jugador haya
        /// llevado lejos se quedan fuera. Nules: con la escolta fuera se gana el 6 %; dentro, el 99,5 % (I-113).</summary>
        Platoon Cercano(int x, int y, int radioMm)
        {
            Platoon best = null; long bd = (long)radioMm * radioMm;
            foreach (var g in _uw.Platoons)
            {
                if (_escolta.IndexOf(g.Id) >= 0 || g.Aboard != 0 || g.EyeMm > UnitsWorld.EyeInfantryMm + 1000) continue;
                long dx = g.AX - x, dy = g.AY - y, d = dx * dx + dy * dy; if (d < bd) { bd = d; best = g; }
            }
            return best;
        }
        void EscoltaAlAsedio()
        {
            var w = NetSession.A; if (w == null || w.Director == null || _zoneNode < 0 || _uw == null || _ng == null) return;
            foreach (var pl in w.Director.Active)
            {
                if (pl.Kind != TBD.Net.HiveDirector.PlanKind.Asedio || pl.Node != _zoneNode) continue;
                long key = NetWorld.DayOf(w.Tick) * 1000L + pl.Node; if (!_asedioVisto.Add(key)) continue;      // una vez por día y estación (el director puede rehacer el plan)
                var tr = _ng.P.TrainRect; int dentroX = _puestoEnviado.HasValue ? _puestoEnviado.Value.x : (_ng.P.StationRect.X0 + _ng.P.StationRect.X1) / 2, dentroY = _puestoEnviado.HasValue ? _puestoEnviado.Value.y : _ng.P.StationRect.Y0 - 3000;
                var cerca = new List<Platoon>();
                foreach (var p in _uw.Platoons)
                {
                    if (p.AliveCount() == 0 || p.Aboard != 0 || _escolta.IndexOf(p.Id) < 0) continue;      // solo la tropa bajada del tren (la guarnición ya está dentro)
                    long dx = p.AX < tr.X0 ? tr.X0 - p.AX : p.AX > tr.X1 ? p.AX - tr.X1 : 0, dy = p.AY < tr.Y0 ? tr.Y0 - p.AY : p.AY > tr.Y1 ? p.AY - tr.Y1 : 0;
                    if (dx * dx + dy * dy <= 40000L * 40000L) cerca.Add(p);
                }
                if (cerca.Count == 0) continue;
                // D-N194 (Lucía 8a80d2a): orden de llenado explícito. Sur de la puerta oeste hasta 8, norte hasta 8, puesto este hasta 4 y el resto a la banqueta.
                // Cada puesto es el pelotón de la guarnición más cercano a su sitio (no «el de la Y más alta»: el puesto este ganaría y pasaría del muro).
                Platoon sur = Cercano(71500, 58500, 15000), norte = Cercano(71500, 70500, 15000), este = Cercano(118000, 80500, 15000);
                int pasan = 0;
                foreach (var (dest, tope) in new[] { (sur, 8), (norte, 8), (este, 4) })
                {
                    if (dest == null) continue;
                    foreach (var e in cerca)
                        for (int k = e.Men.Count - 1; k >= 0 && dest.AliveCount() < tope; k--)
                            if (e.Men[k].Alive) { dest.Men.Add(e.Men[k]); e.Men.RemoveAt(k); pasan++; }      // andan a su sitio en el pelotón (MoveMen): sin teletransporte
                }
                int n = 0;
                foreach (var e in cerca)
                {
                    if (_bot != null) _bot.Busy.Remove(e.Id);
                    if (e.AliveCount() == 0) continue;
                    _uw.Move(e.Id, 71500 + 3000 * n, 64500, 32768); n++;      // lo que sobra, a la banqueta (Lucía, D-N194: 71,5; 64,5)
                }
                Debug.Log($"[NOCHE] asedio anunciado en {_estacionZona}: {pasan} soldado(s) de la escolta completan la guarnición (sur {sur?.AliveCount()}, norte {norte?.AliveCount()}, este {este?.AliveCount()}); {n} pelotón(es) a la banqueta ({dentroX / 1000f:0.0}; {dentroY / 1000f:0.0})");
            }
        }
        int _cercoPend;
        /// <summary>D-N188 (Nacho): los zombis abatidos en la zona de una estación cercada bajan el cerco de la red (orden CercoBajas, en lotes de «minimo»; al salir, lo que quede).</summary>
        void EnviarCercoBajas(int minimo)
        {
            var w = NetSession.A; if (w == null || _zoneNode < 0 || !w.Cercada(_zoneNode)) { _cercoPend = 0; return; }
            if (_cercoPend < minimo || _cercoPend <= 0) return;
            NetSession.Push(NetCmdType.CercoBajas, _zoneNode, 0, _cercoPend);      // Push(tipo, arg, retardo, arg2)
            Debug.Log($"[CERCO] {_cercoPend} abatidos en {_estacionZona}: cerco {w.CercoZombies} → {System.Math.Max(0, w.CercoZombies - _cercoPend)} (se rompe por debajo de {w.CercoRompe})");
            _cercoPend = 0;
        }
        void SoltarPausaRed() { EnviarCercoBajas(1); NetSession.Frenada = false; if (_pausaRedPuesta && NetSession.A != null && NetSession.A.Paused) NetSession.Push(NetCmdType.Resume, 0, 0); _pausaRedPuesta = false; }
        /// <summary>Con la zona en pantalla, la zona pone el arranque de las 22:00 (Begin con el tick de red de las 22:00) si el plan de la red tiene noche ese día;
        /// tras una victoria, la noche siguiente (BeginNextNight). Lo mismo que hace NightSim.ComienzaNoche fuera de pantalla.</summary>
        void ArranqueRedZona()
        {
            var w = NetSession.A; if (w == null || _zoneNode < 0) return;
            long t = _tickRedZona >= 0 ? _tickRedZona : w.Tick; int day = NetWorld.DayOf(t); long st = NetWorld.NightStartTick(day);
            if (t < st || !w.HasNight(_zoneNode, day)) return;
            var ses = NightRun.Session;
            if (_ng.StartNetTick < 0 && _ng.Tick == 0)
            {
                if (!_ng.Begin(st)) return;
                Debug.Log($"[RELOJ] 22:00 del día {day} en la zona: empieza la noche {w.NightNumber(_zoneNode, day)} de {_estacionZona} (tick de red {st})");
                IniciaNocheZona();
            }
            else if (_ng.State == NightState.Victoria && _ng.StartNetTick < st)
            {
                var np = ses != null && ses.Next.HasValue ? ses.Next.Value : _ng.P;
                if (!_ng.BeginNextNight(np, st)) return;
                _reportClosed = false; _endDismissed = false; _lastWave = -1; _nightKills = 0; _nightShotsSpent = 0; _nightLost = 0; if (ses != null) ses.Reported = false;
                _bannerText = $"22:00. Noche {w.NightNumber(_zoneNode, day)} en {_estacionZona}."; _bannerUntil = Time.unscaledTime + 4f;
                Debug.Log($"[RELOJ] 22:00 del día {day} en la zona: noche siguiente de {_estacionZona} ({np.Waves.Length} oleada(s))");
                IniciaNocheZona();
            }
        }

        /// <summary>Copia exacta de una noche (NightGame y UnitsWorld por SaveState/LoadState, zombis, vehículos y bot nuevos). Dos copias del mismo origen son iguales bit a bit.</summary>
        public static NightSim CopiaNoche(NightSim s)
        {
            var w1 = new SaveWriter(); s.Night.SaveState(w1); var ng = new NightGame(s.Night.P, 0); ng.LoadState(new SaveReader(w1.ToArray()));
            var w2 = new SaveWriter(); s.Units.SaveState(w2); var uw = new UnitsWorld(0); uw.LoadState(new SaveReader(w2.ToArray()));
            foreach (var o in s.Units.Obstacles) uw.Obstacles.Add(o);
            foreach (var lb in s.Units.LosBlockers) uw.LosBlockers.Add(lb);      // la línea de visión (Álvaro) y la altura de la boca no van en SaveState
            uw.GrenadeDeconflict = s.Units.GrenadeDeconflict;
            for (int i = 0; i < uw.Platoons.Count && i < s.Units.Platoons.Count; i++) uw.Platoons[i].EyeMm = s.Units.Platoons[i].EyeMm;      // azoteas y banquetas (D-N174/D-N183)
            var c = new NightSim(ng, uw, s.SP, s.WallHp) { NextNightParams = s.NextNightParams };
            var z = new List<Target>(); s.ExportZombies(z); foreach (var t in z) c.ImportZombie(t.X, t.Y);
            foreach (var v in s.Vehicles) { var nv = NightVehicle.Make(v.Name, v.Kind, v.X, v.Y); foreach (var f in typeof(NightVehicle).GetFields()) if (!f.IsInitOnly && !f.IsLiteral) f.SetValue(nv, f.GetValue(v)); c.Vehicles.Add(nv); }      // todos los campos públicos (munición, recarga, NH90…)
            c.VehicleParamsJson = s.VehicleParamsJson; c.Kills = s.Kills; c.SoldierDeaths = s.SoldierDeaths; c.Resupplies = s.Resupplies; c.PeakPile = s.PeakPile; c.Spawned = s.Spawned; c.Climbed = s.Climbed; c.MaterialsU = s.MaterialsU; c.FuelU = s.FuelU;
            if (s.Bot != null) { c.Bot = NightBot.FromParams(c.SP); foreach (var id in s.Bot.Busy) c.Bot.Busy.Add(id); }
            return c;
        }

        /// <summary>Registra la noche de «nodo» en A (simA) y una copia en B. Para que A y B sean iguales, simA debe ser también una copia del mismo origen.</summary>
        public static void RegistrarNoche(int nodo, NightSim origen, NightSession s)
        {
            var a = NetSession.A; if (a == null || nodo < 0 || origen == null) return;
            var simA = CopiaNoche(origen); a.NochesZona[nodo] = simA;
            if (NetSession.B != null && NetSession.B != a) NetSession.B.NochesZona[nodo] = CopiaNoche(origen);
            if (s != null) { s.Sim = simA; s.Ng = simA.Night; s.Uw = simA.Units; s.Away = true; s.Node = nodo; }
        }
        /// <summary>La noche de «nodo» registrada en la red del mundo B (null si no está o si no hay B). La vista que cambie la noche fuera de pantalla (embarque) cambia también esta.</summary>
        public static NightSim NocheEnB(int nodo) { var b = NetSession.B; if (b == null || b == NetSession.A || nodo < 0) return null; return b.NochesZona.TryGetValue(nodo, out var z) ? z as NightSim : null; }
        /// <summary>La sesión de la noche de «nodo»: la de la zona abierta o la aparcada (fuera de pantalla).</summary>
        public static NightSession SesionDe(int nodo)
        {
            if (NightRun.Session != null && NightRun.Session.Node == nodo) return NightRun.Session;
            return NightRun.Aparcadas.TryGetValue(nodo, out var s) ? s : (NightRun.Session != null && NightRun.Session.Node < 0 ? NightRun.Session : null);
        }
        public static void QuitarNoche(int nodo)
        {
            if (NetSession.A != null) NetSession.A.NochesZona.Remove(nodo);
            if (NetSession.B != null) NetSession.B.NochesZona.Remove(nodo);
        }

        /// <summary>Partida nueva: las noches de Sagunto y Nules, montadas igual que InitNight (JSON de la estación + balance_noche.csv, plantel de la D-N183, vehículos),
        /// registradas en la red (A y B) y apuntadas como sesiones aparcadas. Empiezan a las 22:00 de su plan (Sagunto desde el día 1, Nules el día 2).</summary>
        public static void RegistrarNochesIniciales()
        {
            var a = NetSession.A; if (a == null || !NightRun.Active) return;
            NightRun.Session = null; NightRun.Aparcadas.Clear();
            var lectorAntes = BalanceSource.Reader;      // Marcos (qa52, 115): se devuelve al acabar; si no, lo que se lee después de Data/ falla (estacion_betera.json)
            try { RegistrarNochesIniciales(a); } finally { BalanceSource.Reader = lectorAntes; }
        }
        static void RegistrarNochesIniciales(NetWorld a)
        {
            BalanceSource.Reader = f => { var pth = System.IO.Path.Combine(Application.streamingAssetsPath, "Noche", f); return System.IO.File.Exists(pth) ? System.IO.File.ReadAllText(pth) : null; };
            string dir = System.IO.Path.Combine(Application.streamingAssetsPath, "Noche");
            CsvTable tn = null;
            try { var tu = new CsvTable(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "balance_unidades.csv"))); WeaponCatalog.Load(tu); PlatoonCatalog.Load(tu); tn = new CsvTable(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "balance_noche.csv"))); }
            catch (System.Exception e) { Debug.LogWarning("[RELOJ] balance_*.csv: " + e.Message); }
            string key = NightRun.Difficulty < 800 ? "facil" : NightRun.Difficulty <= 950 ? "normal" : "dificil";
            { var cd = System.Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < cd.Length; i++) if (cd[i] == "-dificultad" && (cd[i + 1] == "facil" || cd[i + 1] == "normal" || cd[i + 1] == "dificil")) key = cd[i + 1]; }
            bool bot = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nightbot") >= 0;
            string vjson = BalanceSource.Read("nightsim_params.json");
            foreach (var est in EstacionesConNoche)
            {
                int nodo = TrenPreparar.Nodo(a, est); if (nodo < 0) continue;
                string fich = est == "Nules" && System.IO.File.Exists(System.IO.Path.Combine(dir, "nules.json")) ? "nules.json" : "noche_sagunto.json";
                NightSim sj;
                try { sj = NightSim.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, fich)), key, 77u); }
                catch (System.Exception e) { Debug.LogWarning($"[RELOJ] {fich}: {e.Message}"); continue; }
                var jp = sj.Night.P; var np = jp;
                if (tn != null) { np = NightParams.FromCsv(tn); np.Waves = jp.Waves; np.Entries = jp.Entries; np.ClockX = jp.ClockX; np.ScalePermil = jp.ScalePermil; np.TrainNotDefeat = jp.TrainNotDefeat; if (sj.JsonForce != null) np.TrainRect = jp.TrainRect; }   // como InitNight (sin tren al empezar: el rectángulo del JSON)
                np.EndHour = (np.StartHour + 23) % 24; np.NetClock = true;                // D-N164; reloj de la red
                np.StationRect = new RectMm(96500, 75200, 115500, 83600);              // Nacho (1f2e7b07): el edificio = la caja sólida de la zona (si no, un zombi en la fachada este no cuenta como atacante); la zona reanuda esta noche, así que va aquí también
                var ng = new NightGame(np, 77u + SemillaQA); _base[nodo] = np; _dif = key; _diaAjustado = -1;
                int reservaU = a.Stock[nodo, (int)Res.Municion];
                var uw = new UnitsWorld(1234u + SemillaQA) { StockShots = System.Math.Min(47, System.Math.Max(0, reservaU)) * WeaponCatalog.ShotsPerU };   // Hugo: min(tope 47, reserva); a las 22:00 se vuelve a mirar
                var s = new NightSession { Node = nodo };
                foreach (var pp in Plantel.Pelotones(est, false)) { var pl = uw.AddPlatoon(pp.Tipo, pp.X, pp.Y, pp.Rumbo, pp.Formacion); if (pp.CotaMm > 0) pl.EyeMm = UnitsWorld.EyeInfantryMm + pp.CotaMm; if (pp.Hombres > 0) while (pl.Men.Count > pp.Hombres) pl.Men.RemoveAt(pl.Men.Count - 1); s.PName[pl.Id] = pp.Nombre; }
                if (est == "Sagunto" && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sinazotea") < 0)
                    for (int az = 0; az < 2; az++) { var pa = uw.AddPlatoon(PlatoonKind.Tiradores, 84500, 74300 + 4000 * az, 32768, Formation.Cuna); pa.EyeMm = 6350; s.PName[pa.Id] = "Tiradores " + (az == 0 ? "azotea 1" : "azotea 2"); }
                var sp = sj.SP;
                var origen = new NightSim(ng, uw, sp, sp.WallHp) { VehicleParamsJson = vjson };      // modo enganchado, como al salir de la zona
                foreach (var c in Plantel.Vehiculos(est, false))
                {
                    var nv = NightVehicle.Make(c.Nombre, c.Tipo == "dragon" ? NightVehKind.Dragon : NightVehKind.Vamtac, (int)(c.X * 1000f), (int)(c.Z * 1000f));
                    try { NightVehicle.ApplyParams(nv, vjson); } catch (System.Exception) { }
                    nv.Shots = nv.MaxShots; origen.Vehicles.Add(nv);
                }
                // I-Noche157: la noche aparcada también tiene línea de visión (como LosInit en la zona). El inventario de sólidos es el de Sagunto; Nules usa el suyo si existe y, si no, va sin regla
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sinlos") < 0)
                {
                    string losCsv = est == "Sagunto" ? "sagunto_obstaculos_v1.csv" : est.ToLowerInvariant() + "_obstaculos_v1.csv";
                    try { string csv = BalanceSource.Read(losCsv); if (!string.IsNullOrEmpty(csv)) origen.EnableLineOfSight(true, NightSim.LosSceneFromCsv(csv)); }
                    catch (System.Exception e) { Debug.LogWarning($"[RELOJ] línea de visión de {est}: {losCsv} ({e.Message})"); }
                    Debug.Log($"[RELOJ] línea de visión de {est}: {uw.LosBlockers.Count} sólidos ({losCsv})");
                }
                if (bot) { origen.Bot = NightBot.FromParams(sp); foreach (var kv in s.PName) if (kv.Value.StartsWith("Tiradores azotea")) origen.Bot.Busy.Add(kv.Key); }
                s.WallMax = Mathf.Max(1200000, NightSimParams.Default().WallHp); s.WallHp = s.WallMax;      // la vida del muro de la zona (NightTick); fuera, la de NightSim
                RegistrarNoche(nodo, origen, s); s.Bot = s.Sim.Bot;
                NightRun.Aparcadas[nodo] = s;
                Debug.Log($"[RELOJ] noche de {est} registrada en la red (A y B): {uw.Platoons.Count} pelotones, {origen.Vehicles.Count} vehículos, {uw.StockShots / WeaponCatalog.ShotsPerU} u, {fich} ({key}); noches del plan: día 1 {(a.HasNight(nodo, 1) ? "sí" : "no")}, día 2 {(a.HasNight(nodo, 2) ? "sí" : "no")}");
            }
        }

    }
}
