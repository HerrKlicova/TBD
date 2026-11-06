using System;
using System.Collections.Generic;

namespace TBD.Net
{
    // Hito 2 «Red» (GDD §6 S0, §7, §15): la simulación global. Lógica pura en enteros, sin Unity: se prueba con dotnet.
    // Reloj: 1 tick = 2,4 s de juego (un día de juego = 36.000 ticks = 20 min reales a x1 y 30 Hz, GDD §3).
    // Todo lo que hace el jugador entra como orden (NetCommand) con número de tick, incluidas la velocidad y la pausa (GDD §17.3, regla 9).

    public enum Res : byte { Municion = 0, Combustible = 1, Materiales = 2, Personas = 3 }   // los 4 recursos del MVP (GDD §9)

    public static class NetConst
    {
        public const int TickRate = 30;
        public const int GameMsPerTick = 2400;
        public const int DayTicks = 24 * 3600 * 1000 / GameMsPerTick;     // 36.000
        public const int ResCount = 4;
        public const int FieldEvery = 15;                                   // la mancha avanza cada 15 ticks (medio segundo real a x1)
        public const long TrainMmPerTick = 40_000;                          // 60 km/h con 2,4 s por tick
        public const int TrainCapacity = 400;                               // toneladas (PROTOTIPO)

        public static long Hours(double h) { return (long)Math.Round(h * 3600.0 * 1000.0 / GameMsPerTick); }
        public static string Clock(long tick)
        {
            long t = tick % DayTicks;
            long secs = t * GameMsPerTick / 1000;
            return (secs / 3600).ToString("00") + ":" + (secs / 60 % 60).ToString("00");
        }
    }

    // ================================================================== órdenes
    public enum NetCmdType : byte { SetSpeed = 0, Pause = 1, Resume = 2, TrainGo = 3, TrainHold = 4, Materialize = 5, Dematerialize = 6, ConsumeAmmo = 7, ResolveAmbush = 8 }

    public struct NetCommand
    {
        public long Tick;          // tick en que se aplica
        public NetCmdType Type;
        public int Arg;            // velocidad (1, 2, 4), id de tren o id de nodo
        public int Arg2;           // Dematerialize: densidad que vuelve a la celda (por mil, 1 = 10 zombis vivos)
        public int Seq;            // orden de llegada (lo pone NetWorld.Push): desempata órdenes del mismo tick
    }

    // ================================================================== horarios y carga
    public struct ScheduleStop
    {
        public int Node;
        public long DepartAt;          // hora de salida dentro del día, en ticks (0..DayTicks-1)
        public int[] Transfer;         // por recurso: + carga en el tren, − descarga en la estación (toneladas). Se hace al salir.
    }

    public sealed class TrainService
    {
        public RailTrain Train;
        public readonly List<ScheduleStop> Stops = new List<ScheduleStop>();
        public readonly int[] Cargo = new int[NetConst.ResCount];
        public int StopIndex;              // parada en la que está o hacia la que va
        public bool AtStop = true;         // esperando en la parada StopIndex
        public bool Held;                  // retenido por el jugador
        public bool Ambushed;              // detenido por una emboscada hasta que se resuelva (zona o expiración)
        public long LastDepartDay = -1;    // último día en que salió de alguna parada (informativo)
        public readonly List<long> DepartedDay = new List<long>();   // por parada: día de la última salida (no sale dos veces el mismo día)

        public int CargoTotal() { int s = 0; for (int i = 0; i < Cargo.Length; i++) s += Cargo[i]; return s; }
    }

    // ================================================================== capa S0: mancha de horda
    /// <summary>
    /// Densidad de horda en una rejilla gruesa sobre el mapa de operaciones (1 celda = 1 unidad de mapa ≈ 3,5 km), en «por mil».
    /// Cada paso: difusión y deriva hacia lo que atrae a la Colmena (ciudades y ruido del tráfico). Todo en enteros y con un
    /// búfer de salida que solo recibe sumas: el resultado no depende del orden de recorrido. La masa total se conserva.
    /// </summary>
    public sealed class HordeField
    {
        public const int W = 96, H = 96;
        public const int Max = 1000;
        public const int DriftStop = 500;          // la deriva no entra en celdas ya cargadas: la mancha se extiende en vez de concentrarse
        public int[] D = new int[W * H];
        public int[] S = new int[W * H];           // horda INICIAL: guarnición estática y paisaje. No se mueve ni la recluta el director (D-N37, Hugo y Mak); solo la zona la toca
        int[] _next = new int[W * H];
        long[] _pot = new long[W * H];

        public int At(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return 0;
            return D[y * W + x] + S[y * W + x];
        }

        public long Mass() { long m = 0; for (int i = 0; i < D.Length; i++) m += D[i] + S[i]; return m; }

        /// <summary>
        /// Masa de la horda inicial que queda a tiro de la vía (a menos de «cells» celdas de algún tramo): la que cuenta para el combate;
        /// el resto es paisaje (D-N37b). En «por mil».
        /// </summary>
        public long LeashedMassNearTrack(RailGraph g, int cells)
        {
            var near = new bool[W * H];
            foreach (var e in g.Edges)
            {
                var a = g.Nodes[e.A]; var b = g.Nodes[e.B];
                int steps = Math.Max(Math.Abs(b.XMeters - a.XMeters), Math.Abs(b.YMeters - a.YMeters)) * 2 + 1;
                for (int k = 0; k <= steps; k++)
                {
                    int px = a.XMeters + (b.XMeters - a.XMeters) * k / steps, py = a.YMeters + (b.YMeters - a.YMeters) * k / steps;
                    for (int y = py - cells; y <= py + cells; y++)
                        for (int x = px - cells; x <= px + cells; x++)
                            if (x >= 0 && y >= 0 && x < W && y < H) near[y * W + x] = true;
                }
            }
            long m = 0;
            for (int i = 0; i < S.Length; i++) if (near[i]) m += S[i];
            return m;
        }

        /// <summary>Masa de la horda inicial (guarnición y paisaje) que sigue viva, en «por mil». Solo la zona la toca; el director no (D-N37).</summary>
        public long StaticMass() { long m = 0; for (int i = 0; i < S.Length; i++) m += S[i]; return m; }

        /// <summary>Siembra una mancha redonda de horda ESTÁTICA (guarnición y paisaje; no la mueve el campo ni la recluta el director).</summary>
        public void BlobStatic(int cx, int cy, int r, int value)
        {
            Blob(cx, cy, r, value, S);
            if (_leash == null) _leash = new bool[W * H];
            for (int c = 0; c < S.Length; c++)                       // la correa: hasta LeashCells celdas de donde nació
            {
                if (S[c] <= 0) continue;
                int cx2 = c % W, cy2 = c / W;
                for (int y = cy2 - LeashCells; y <= cy2 + LeashCells; y++)
                    for (int x = cx2 - LeashCells; x <= cx2 + LeashCells; x++)
                        if (x >= 0 && y >= 0 && x < W && y < H) _leash[y * W + x] = true;
            }
        }

        /// <summary>Siembra una mancha redonda (para el arranque y para las pruebas).</summary>
        public void Blob(int cx, int cy, int r, int value) { Blob(cx, cy, r, value, D); }

        void Blob(int cx, int cy, int r, int value, int[] layer)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy > r * r) continue;
                    int i = y * W + x;
                    layer[i] = Math.Min(Max, layer[i] + value * (r * r - (dx * dx + dy * dy) / 2) / (r * r));
                }
        }

        /// <summary>
        /// Celda congelada (-1 = ninguna): la del nodo con una zona en detalle. No da ni recibe masa mientras la zona existe
        /// (decisión de Sara/Mak, QA H3): si no, la difusión la rellenaría y al volver se sumaría a lo devuelto (masa duplicada).
        /// </summary>
        public int Frozen = -1;

        public void Step(RailGraph g, int[] attraction)
        {
            // potencial: suma de atracciones de los nodos, que cae con la distancia (Manhattan, enteros)
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    long p = 0;
                    for (int n = 0; n < g.Nodes.Count; n++)
                    {
                        var node = g.Nodes[n];
                        int d = Math.Abs(node.XMeters - x) + Math.Abs(node.YMeters - y);
                        p += attraction[n] * 1000L / (4 + d);
                    }
                    _pot[y * W + x] = p;
                }

            if (_block == null)                                          // nunca entra en un nodo: las 3×3 de cada nodo quedan vedadas a la horda con correa
            {
                _block = new bool[W * H];
                foreach (var node in g.Nodes)
                    for (int y = node.YMeters - 1; y <= node.YMeters + 1; y++)
                        for (int x = node.XMeters - 1; x <= node.XMeters + 1; x++)
                            if (x >= 0 && y >= 0 && x < W && y < H) _block[y * W + x] = true;
            }
            var nd = StepLayer(D, _next, false);
            _next = D; D = nd;
            if (_leash != null)                                          // la horda inicial: deriva y reacciona al ruido, pero dentro de su correa (D-N37b)
            {
                var ns = StepLayer(S, _nextS, true);
                _nextS = S; S = ns;
            }
        }

        public const int LeashCells = 2;           // correa de la horda inicial: unos 7 km alrededor de donde nace (D-N37b)
        bool[] _leash, _block;
        int[] _nextS = new int[W * H];

        bool Ok(int j, bool leashed) { return j != Frozen && (!leashed || (_leash[j] && !_block[j])); }

        /// <summary>Un paso de difusión y deriva de una capa. La capa con correa solo se mueve a celdas de su correa y fuera de los nodos.</summary>
        int[] StepLayer(int[] src, int[] next, bool leashed)
        {
            Array.Clear(next, 0, next.Length);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    int d = src[i];
                    if (d == 0) continue;
                    int keep = d;
                    if (i == Frozen) { next[i] += keep; continue; }
                    // difusión: 1/48 a cada vecino (nunca hacia la celda congelada)
                    int share = d / 48;
                    if (share > 0)
                    {
                        if (x > 0 && Ok(i - 1, leashed)) { next[i - 1] += share; keep -= share; }
                        if (x < W - 1 && Ok(i + 1, leashed)) { next[i + 1] += share; keep -= share; }
                        if (y > 0 && Ok(i - W, leashed)) { next[i - W] += share; keep -= share; }
                        if (y < H - 1 && Ok(i + W, leashed)) { next[i + W] += share; keep -= share; }
                    }
                    // deriva: 1/60 hacia el vecino de más potencial (orden fijo de vecinos en los empates), si no está ya cargado
                    int best = -1;
                    long bp = _pot[i];
                    if (x > 0 && _pot[i - 1] > bp) { bp = _pot[i - 1]; best = i - 1; }
                    if (x < W - 1 && _pot[i + 1] > bp) { bp = _pot[i + 1]; best = i + 1; }
                    if (y > 0 && _pot[i - W] > bp) { bp = _pot[i - W]; best = i - W; }
                    if (y < H - 1 && _pot[i + W] > bp) { bp = _pot[i + W]; best = i + W; }
                    if (best >= 0 && Ok(best, leashed) && src[best] < DriftStop)
                    {
                        int move = d / 60;
                        next[best] += move;
                        keep -= move;
                    }
                    next[i] += keep;
                }
            // tope por celda: lo que sobra se queda (no se pierde masa); se reparte al vecino libre en orden fijo
            for (int i = 0; i < next.Length; i++)
            {
                if (next[i] <= Max) continue;
                int over = next[i] - Max;
                next[i] = Max;
                int x = i % W, y = i / W;
                int[] nb = { x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < H - 1 ? i + W : -1 };
                foreach (int j in nb)
                {
                    if (j < 0 || !Ok(j, leashed) || over == 0) continue;
                    int room = Max - next[j];
                    if (room <= 0) continue;
                    int m = Math.Min(room, over);
                    next[j] += m;
                    over -= m;
                }
                next[i] += over;            // si no cabe en ningún vecino, se queda por encima del tope (raro)
            }
            return next;
        }

        public ulong Hash()
        {
            ulong h = 0;
            for (int i = 0; i < D.Length; i++) { if (D[i] != 0) h += NetWorld.Mix(((ulong)i << 20) ^ (ulong)D[i]); if (S[i] != 0) h += NetWorld.Mix(((ulong)i << 20) ^ (ulong)S[i] ^ 0x57A71CUL); }
            return h;
        }
    }

    // ================================================================== mundo S0
    public sealed class NetWorld
    {
        public readonly RailGraph Graph;
        public readonly List<TrainService> Services = new List<TrainService>();
        public readonly HordeField Field = new HordeField();
        public readonly int[] Attraction;          // por nodo: base de la ciudad + ruido del tráfico
        public readonly int[] BaseAttraction;
        public readonly int[,] Stock;              // por nodo y recurso
        public long Tick;
        public int Speed = 1;                       // 1, 2 o 4 ticks por paso de 1/30 s
        public bool Paused;
        public bool AutoSlow = true;

        // ---- zona en detalle (hito 3, D-N05): 1 por mil de densidad = 10 zombis
        public const int ZombiesPerPermil = 10;
        public long InjectedTotal, LostTotal, RetiredTotal;      // en «por mil» de densidad: 1 ‰ = ZombiesPerPermil (10) zombis
        public long InjectedZombies => InjectedTotal * ZombiesPerPermil;        // todos los contadores de salida, en zombis individuales (decisión de Mak)
        public long LostZombies => LostTotal * ZombiesPerPermil;
        public long RetiredZombies => RetiredTotal * ZombiesPerPermil;
        public long InjectedZombiesThisTick { get { long s = 0; foreach (var i in Injections) s += i.Amount; return s * ZombiesPerPermil; } }
        public long RetiredZombiesThisTick => RetiredThisTick * ZombiesPerPermil;
        public long InitialMassAlive => Field.StaticMass();      // la horda inicial que sigue viva (Sara: el director solo gasta masa inyectada)
        public readonly List<Injection> Injections = new List<Injection>();   // lo inyectado en ESTE tick (celda y cantidad), para QA (Sara); no entra en el hash
        public long LostThisTick, RetiredThisTick;
        public readonly List<Injection> Retirements = new List<Injection>();   // lo retirado en ESTE tick (celda y cantidad), para QA       // masa(t) = masa(t-1) + inyectada − bajas, exacto en enteros (D-N33, Mak)
        public int ActiveZoneTakenS;                // de lo que sacó la zona, la parte que era guarnición
        public int ActiveZoneNode = -1;             // nodo con zona materializada (-1 = ninguna; el MVP admite una)
        public int ActiveZoneTaken;                 // densidad (por mil) que salió de la mancha al materializar
        public long ActiveZoneSinceTick = -1;
        public int ActiveZoneCell = -1;             // celda de la mancha de la que salió la horda (y a la que vuelve)
        public List<Alert> Alerts = new List<Alert>();
        public TrafficMemory Traffic;                    // hito 4: memoria de tráfico de la Colmena
        public readonly List<IntelEntry> Intel = new List<IntelEntry>();   // informe de inteligencia (entradas en orden de tick)
        public HiveDirector Director = new HiveDirector();               // hito 4: decide una vez por hora de juego
        public bool DirectorEnabled = true;
        /// <summary>
        /// Emboscada materializada (hito 4): un tren cae en una emboscada preparada. La horda que el director juntó en el punto sale del campo
        /// (queda en el encuentro) y el tren se detiene. Se resuelve con la orden ResolveAmbush (la zona devuelve los supervivientes) o,
        /// si nadie responde en AmbushWaitTicks, por expiración: la horda se dispersa y el tren pierde parte de la carga.
        /// </summary>
        public struct AmbushEncounter { public bool Active; public int Service, Edge, Cell, Taken; public long ExpireTick; }
        public AmbushEncounter Encounter;
        public const long AmbushWaitTicks = 750;           // 30 min de juego (a 2,4 s por tick: 1.800 s)
        public const int AmbushCargoLossMil = 300;         // carga que pierde el tren si no se resuelve (PROTOTIPO)
        public readonly List<int> PendingAmbushes = new List<int>();      // tramos con emboscada preparada (TR-3 cuando el tren cae en ella)
        readonly List<NetCommand> _queue = new List<NetCommand>();

        public const int SiegeAt = 500, ThreatAt = 150, CutAt = 850, NearAt = 250;

        public NetWorld(RailGraph g)
        {
            Graph = g;
            Attraction = new int[g.Nodes.Count];
            BaseAttraction = new int[g.Nodes.Count];
            Stock = new int[g.Nodes.Count, NetConst.ResCount];
            Traffic = new TrafficMemory(g.Edges.Count, BalanceSource.Traffic());
            Director.P = BalanceSource.Director();
            Director.Curve = BalanceSource.Curve();
            Director.Season = BalanceSource.Table("colmena_estacion.csv");
            Director.Noise = BalanceSource.Table("colmena_ruido.csv");
        }

        public static ulong Mix(ulong h)
        {
            h ^= h >> 33; h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33; h *= 0xc4ceb9fe1a85ec53UL;
            h ^= h >> 33;
            return h;
        }

        // ---------------------------------------------------------------- órdenes
        int _seq;
        public void Push(NetCommand c) { c.Seq = _seq++; _queue.Add(c); }

        /// <summary>
        /// Aplica las órdenes cuyo tick ya ha llegado, en orden de tick y luego de llegada. Se llama también en pausa: así la orden
        /// de reanudar (sellada con el tick actual) se aplica aunque el reloj esté parado.
        /// </summary>
        public void ApplyCommands()
        {
            _queue.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Seq.CompareTo(b.Seq));
            var keep = new List<NetCommand>();
            for (int i = 0; i < _queue.Count; i++)
            {
                var c = _queue[i];
                if (c.Tick > Tick) { keep.Add(c); continue; }
                switch (c.Type)
                {
                    case NetCmdType.SetSpeed: Speed = c.Arg == 4 ? 4 : (c.Arg == 2 ? 2 : 1); break;
                    case NetCmdType.Pause: Paused = true; break;
                    case NetCmdType.Resume: Paused = false; break;
                    case NetCmdType.TrainHold: if (c.Arg >= 0 && c.Arg < Services.Count) { Services[c.Arg].Held = true; Services[c.Arg].Train.Stop(); } break;
                    case NetCmdType.Materialize: Materialize(c.Arg); break;
                    case NetCmdType.ResolveAmbush: ResolveAmbush(c.Arg, c.Arg2, false); break;
                    case NetCmdType.Dematerialize: Dematerialize(c.Arg, c.Arg2); break;
                    case NetCmdType.ConsumeAmmo: ConsumeAmmo(c.Arg, c.Arg2); break;
                    case NetCmdType.TrainGo:
                        if (c.Arg >= 0 && c.Arg < Services.Count)
                        {
                            var sv = Services[c.Arg];
                            sv.Held = false;
                            if (!sv.AtStop) sv.Train.Go();
                        }
                        break;
                }
            }
            _queue.Clear();
            _queue.AddRange(keep);
        }

        /// <summary>
        /// Munición disponible en una zona (diseño del hito 3 de Lucía): la carga de los trenes parados en la estación (a menos de
        /// 150 m, en la práctica los que están en el andén) más las existencias de la estación.
        /// </summary>
        public long AmmoAvailableAt(int node)
        {
            if (node < 0 || node >= Graph.Nodes.Count) return 0;
            long u = Stock[node, (int)Res.Municion];
            foreach (var sv in Services)
                if (sv.AtStop && sv.Stops.Count > 0 && sv.Stops[sv.StopIndex].Node == node) u += sv.Cargo[(int)Res.Municion];
            return u;
        }

        /// <summary>Gasta munición en un nodo: primero la de los trenes del andén (en orden de id), luego la de la estación.</summary>
        void ConsumeAmmo(int node, int units)
        {
            if (node < 0 || node >= Graph.Nodes.Count || units <= 0) return;
            int left = units;
            foreach (var sv in Services)                        // Services va en orden de creación: determinista
            {
                if (left == 0) break;
                if (!(sv.AtStop && sv.Stops.Count > 0 && sv.Stops[sv.StopIndex].Node == node)) continue;
                int take = Math.Min(left, sv.Cargo[(int)Res.Municion]);
                sv.Cargo[(int)Res.Municion] -= take;
                sv.Train.CargoTonnes = sv.CargoTotal();
                left -= take;
            }
            Stock[node, (int)Res.Municion] = Math.Max(0, Stock[node, (int)Res.Municion] - left);
        }

        // ---------------------------------------------------------------- zonas en detalle
        /// <summary>
        /// La horda de la celda del nodo pasa a la zona: la densidad sale de la mancha (masa conservada: vuelve al desmaterializar).
        /// La zona la recibe como ActiveZoneTaken × 10 zombis. Si ya hay otra zona activa, se ignora (MVP: una sola zona).
        /// </summary>
        void Materialize(int node)
        {
            if (ActiveZoneNode >= 0 || node < 0 || node >= Graph.Nodes.Count) return;
            var n = Graph.Nodes[node];
            // la celda que se materializa es la más densa de las 3×3 alrededor del nodo (la misma que da el estado del nodo:
            // HordeDensity = máximo alrededor); empate: la de índice menor
            int i = -1, best = -1;
            for (int y = n.YMeters - 1; y <= n.YMeters + 1; y++)
                for (int x = n.XMeters - 1; x <= n.XMeters + 1; x++)
                {
                    if (x < 0 || y < 0 || x >= HordeField.W || y >= HordeField.H) continue;
                    int c = y * HordeField.W + x;
                    if (Field.D[c] + Field.S[c] > best) { best = Field.D[c] + Field.S[c]; i = c; }
                }
            ActiveZoneCell = i;
            ActiveZoneNode = node;
            ActiveZoneTaken = Field.D[i] + Field.S[i];
            ActiveZoneTakenS = Field.S[i];            // la parte de guarnición vuelve a su sitio como guarnición
            ActiveZoneSinceTick = Tick;
            Field.D[i] = 0; Field.S[i] = 0;
            Field.Frozen = i;
        }

        /// <summary>Los zombis vivos de la zona vuelven a la celda del nodo (las bajas se pierden: la horda pierde masa, D-N05).</summary>
        void Dematerialize(int node, int returnPermil)
        {
            if (node != ActiveZoneNode) return;
            int i = ActiveZoneCell;
            LostThisTick += Math.Max(0, ActiveZoneTaken - Math.Max(0, returnPermil));
            LostTotal += Math.Max(0, ActiveZoneTaken - Math.Max(0, returnPermil));
            Director.OnZoneLoss(this, node, Math.Max(0, ActiveZoneTaken - Math.Max(0, returnPermil)));      // estado de manada «asedio»: pierde más del 50 % y se retira   // las bajas de la zona salen de la masa; si vuelve más de lo que salió (refuerzos) cuenta como inyectado
            InjectedTotal += Math.Max(0, Math.Max(0, returnPermil) - ActiveZoneTaken);
            int back = Math.Max(0, returnPermil), backS = Math.Min(back, ActiveZoneTakenS);
            Field.S[i] += backS; Field.D[i] += back - backS;
            ActiveZoneCell = -1;
            Field.Frozen = -1;
            ActiveZoneNode = -1;
            ActiveZoneTaken = 0;
            ActiveZoneSinceTick = -1;
        }

        // ---------------------------------------------------------------- un tick
        public void Step()
        {
            Injections.Clear(); Retirements.Clear(); LostThisTick = 0; RetiredThisTick = 0;
            ApplyCommands();                 // en cada tick, no al ritmo de los fotogramas (QA Q5)
            if (Paused) return;
            for (int s = 0; s < Services.Count; s++) StepService(Services[s]);
            if (ActiveZoneNode >= 0 && ActiveZoneNode < Director.AttentionTicks.Length) { Director.AttentionTicks[ActiveZoneNode]++; Director.LastSeenTick[ActiveZoneNode] = Tick; }   // memoria de atención
            if (Encounter.Active && Tick >= Encounter.ExpireTick) ResolveAmbush(Encounter.Service, Encounter.Taken, true);   // nadie respondió: la horda se dispersa y el tren pierde carga
            if (DirectorEnabled && Tick % 1500 == 0) Director.Hourly(this);       // una vez por hora de juego
            if (Tick % NetConst.FieldEvery == 0)
            {
                for (int n = 0; n < Attraction.Length; n++)
                {
                    int noise = Attraction[n] - BaseAttraction[n];
                    Attraction[n] = BaseAttraction[n] + noise - noise / 50;    // el ruido del tráfico se olvida poco a poco
                }
                Field.Step(Graph, Attraction);
                UpdateFromField();
            }
            Tick++;
            if (Tick % NetConst.DayTicks == 0 && Traffic != null)
                Traffic.Decay((e, slot) => Intel.Add(new IntelEntry { Tick = Tick, Id = "TR-5", Vars = new[] { "tramo", EdgeName(Graph.Edges[e]) } }));
            if (Tick % NetConst.FieldEvery == 0)
            {
                var prev = Alerts;
                Alerts = AlertEngine.Evaluate(Graph, Trains(), Tick);
                foreach (var pl in Director.Active)                                  // aviso de asedio: «Horda en 3 h» (Mak, D-N32)
                {
                    if (pl.Kind != HiveDirector.PlanKind.Asedio || pl.Started) continue;
                    long eta = pl.StartTick - Tick;
                    Alerts.Add(new Alert { Id = (5 << 24) ^ pl.Node, Kind = AlertKind.HordaCercaDelTren, Priority = 1, EtaTicks = eta, Subject = pl.Node,
                        Text = "Horda hacia " + Graph.Nodes[pl.Node].Name + " en " + AlertEngine.EtaMinutes(eta) / 60 + " h " + AlertEngine.EtaMinutes(eta) % 60 + " min (" + NetConst.Clock(pl.StartTick) + ")" });
                }
                foreach (var sv in Services)
                {
                    var ce = sv.AtStop ? null : sv.Train.CurrentEdge(Graph);
                    if (ce == null || !PendingAmbushes.Contains(ce.Id) || ce.HordeAtMm < 0) continue;
                    PendingAmbushes.Remove(ce.Id);
                    StartEncounter(sv, ce);
                    string punto = ce.Ambush.Count > 0 ? ce.Ambush[0].Name : Graph.Nodes[ce.A].Name + "–" + Graph.Nodes[ce.B].Name;
                    Intel.Add(new IntelEntry { Tick = Tick, Id = "TR-3", Vars = new[] { "punto", punto } });
                }
                // D-N11: una alerta crítica nueva baja el tiempo a x1 (se puede desactivar). Sale del estado: es determinista.
                if (AutoSlow && Speed > 1)
                    foreach (var al in Alerts)
                        if (al.Priority == 0 && !prev.Exists(p => p.Id == al.Id)) { Speed = 1; break; }
            }
        }

        void StartEncounter(TrainService sv, RailEdge ce)
        {
            if (Encounter.Active) return;                                  // MVP: un encuentro a la vez
            int cell = -1;
            foreach (var pl in Director.Active) if (pl.Kind == HiveDirector.PlanKind.Emboscada && pl.Edge == ce.Id) { cell = pl.Cell; break; }
            if (cell < 0) return;
            int taken = Field.D[cell];
            Field.D[cell] = 0;
            LostThisTick += 0;                                             // la masa no se pierde: vuelve al resolverse
            Encounter = new AmbushEncounter { Active = true, Service = Services.IndexOf(sv), Edge = ce.Id, Cell = cell, Taken = taken, ExpireTick = Tick + AmbushWaitTicks };
            sv.Ambushed = true; sv.Train.Stop();
            Field.Frozen = Field.Frozen >= 0 ? Field.Frozen : cell;        // la celda del encuentro no recibe masa mientras dura
        }

        /// <summary>Resuelve la emboscada: los supervivientes vuelven al campo, las bajas se pierden y el tren sigue.</summary>
        void ResolveAmbush(int service, int survivorsPermil, bool expired)
        {
            if (!Encounter.Active || Encounter.Service != service) return;
            var enc = Encounter;
            int back = Math.Max(0, Math.Min(survivorsPermil, enc.Taken));
            Field.D[enc.Cell] += back;
            long lost = enc.Taken - back;
            LostThisTick += lost; LostTotal += lost;
            if (Field.Frozen == enc.Cell) Field.Frozen = -1;
            var sv = Services[enc.Service];
            sv.Ambushed = false;
            if (expired)                                                   // nadie respondió: la carga se resiente
                for (int r = 0; r < NetConst.ResCount; r++) sv.Cargo[r] -= sv.Cargo[r] * AmbushCargoLossMil / 1000;
            sv.Train.CargoTonnes = sv.CargoTotal();
            if (!sv.Held) sv.Train.Go();
            Encounter = default;
        }

        List<RailTrain> Trains()
        {
            var l = new List<RailTrain>(Services.Count);
            foreach (var s in Services) l.Add(s.Train);
            return l;
        }

        void StepService(TrainService s)
        {
            var t = s.Train;
            if (s.AtStop)
            {
                if (s.Held || s.Stops.Count < 2) return;
                var stop = s.Stops[s.StopIndex];
                long day = Tick / NetConst.DayTicks;
                long tod = Tick % NetConst.DayTicks;
                while (s.DepartedDay.Count < s.Stops.Count) s.DepartedDay.Add(-1);
                if (tod < stop.DepartAt || s.DepartedDay[s.StopIndex] == day) return;
                int next = (s.StopIndex + 1) % s.Stops.Count;
                var route = Graph.ShortestPath(stop.Node, s.Stops[next].Node);
                if (route.Count < 2) return;                          // sin camino (vía cortada): espera en la estación
                Transfer(s, stop);                                    // carga y descarga en el andén, justo antes de salir
                s.DepartedDay[s.StopIndex] = day;
                s.LastDepartDay = day;
                t.SetRoute(route);
                t.Go();
                PassEdge(s, t.CurrentEdge(Graph));                    // entra en el primer tramo: la Colmena lo apunta
                if (DirectorEnabled) Director.OnDeparture(this, Services.IndexOf(s), stop.Node);   // ¿una manada lo sigue? (caza)
                s.AtStop = false;
                s.StopIndex = next;
                return;
            }

            if (s.Held || s.Ambushed) return;
            if (t.State == RailTrainState.Detenido)
            {
                var e = t.CurrentEdge(Graph);
                if (e != null && e.State != EdgeState.Cortada) t.Go();    // la vía ha quedado libre: sigue
            }
            int legBefore = t.Leg;
            t.Step(Graph);
            if (t.Leg != legBefore && t.Leg < t.Route.Count) MakeNoise(t.Route[t.Leg]);
            if (t.Leg != legBefore && t.State != RailTrainState.Llegado) PassEdge(s, t.CurrentEdge(Graph));
            if (t.State == RailTrainState.Llegado)
            {
                MakeNoise(s.Stops[s.StopIndex].Node);
                s.AtStop = true;
            }
        }

        string EdgeName(RailEdge e) { return Graph.Nodes[e.A].Name + "–" + Graph.Nodes[e.B].Name; }

        /// <summary>Un tren entra en un tramo: suma a la memoria de tráfico; si la franja cruza el aviso, entrada TR-1 en el informe.</summary>
        void PassEdge(TrainService s, RailEdge e)
        {
            if (e == null || Traffic == null) return;
            if (Traffic.OnPass(e.Id, Tick, s.Train.Id))
            {
                string hora = NetConst.Clock(Tick / 1500 * 1500);             // la hora en punto: «hacia las 08:00»
                Intel.Add(new IntelEntry { Tick = Tick, Id = "TR-1", Vars = new[] { "tren", "Tren " + s.Train.Name, "tramo", EdgeName(e), "hora", hora } });
            }
        }

        void MakeNoise(int node) { Attraction[node] += 70; }       // el tráfico atrae a la Colmena (semilla de la memoria, GDD §5.2)

        void Transfer(TrainService s, ScheduleStop stop)
        {
            if (stop.Transfer == null) return;
            for (int r = 0; r < NetConst.ResCount; r++)
            {
                int q = stop.Transfer[r];
                if (q > 0)
                {
                    int room = NetConst.TrainCapacity - s.CargoTotal();
                    int m = Math.Min(q, Math.Min(room, Stock[stop.Node, r]));
                    if (m <= 0) continue;
                    Stock[stop.Node, r] -= m;
                    s.Cargo[r] += m;
                }
                else if (q < 0)
                {
                    int m = Math.Min(-q, s.Cargo[r]);
                    s.Cargo[r] -= m;
                    Stock[stop.Node, r] += m;
                }
            }
            s.Train.CargoTonnes = s.CargoTotal();
        }

        // ---------------------------------------------------------------- de la mancha al estado de nodos y vías
        void UpdateFromField()
        {
            foreach (var n in Graph.Nodes)
            {
                int d = MaxAround(n.XMeters, n.YMeters);
                n.HordeDensity = d;
                // Lucía (6-nov, D-N08): «asediada» solo con un asedio real, que lanzará el director de la Colmena (hito 4). Hasta entonces,
                // una densidad alta es «amenazada», por alta que sea.
                n.State = d >= ThreatAt ? NodeState.Amenazada : NodeState.Segura;
            }
            foreach (var pl in Director.Active)                                   // asedio real (director, desde el día 2)
                if (pl.Kind == HiveDirector.PlanKind.Asedio && pl.Started) Graph.Nodes[pl.Node].State = NodeState.Asediada;
            foreach (var e in Graph.Edges)
            {
                var a = Graph.Nodes[e.A];
                var b = Graph.Nodes[e.B];
                const int Samples = 32;
                int best = 0;
                long bestAt = -1;
                for (int k = 1; k < Samples; k++)        // sin los extremos: la densidad en la estación es del nodo
                {
                    int x = a.XMeters + (b.XMeters - a.XMeters) * k / Samples;
                    int y = a.YMeters + (b.YMeters - a.YMeters) * k / Samples;
                    int d = Field.At(x, y);
                    if (d > best) { best = d; bestAt = e.LengthMm * k / Samples; }
                }
                e.HordeAtMm = best >= NearAt ? bestAt : -1;
                if (best >= CutAt) e.State = EdgeState.Cortada;              // la horda está sobre la vía
                else if (e.State == EdgeState.Cortada && !Director.Active.Exists(p => p.Kind == HiveDirector.PlanKind.Corte && p.Edge == e.Id))
                    e.State = EdgeState.Ok;  // PROTOTIPO: se despeja sola si no es un corte del director (las obras llegan en el hito 5)
            }
        }

        int MaxAround(int cx, int cy)
        {
            int m = 0;
            for (int y = cy - 1; y <= cy + 1; y++)
                for (int x = cx - 1; x <= cx + 1; x++) m = Math.Max(m, Field.At(x, y));
            return m;
        }

        // ---------------------------------------------------------------- hash (prueba de desincronización)
        /// <summary>Hash por sistema (repetición dorada, D3 de Sara): control, mancha, trenes, existencias y vías.</summary>
        public struct SystemHashes
        {
            public ulong Control, Field, Trains, Stock, Edges, Memory;
            public ulong Total => Control + Field + Trains + Stock + Edges + Memory;
        }

        public static readonly string[] SystemNames = { "control", "mancha", "trenes", "existencias", "vias", "memoria" };

        public SystemHashes Systems()
        {
            var r = new SystemHashes();
            r.Control = Mix((ulong)Tick) + Mix(0xC0FFEEUL ^ ((ulong)(uint)Speed << 1) ^ (Paused ? 1UL : 0UL))
                      + Mix(0x2041EUL ^ ((ulong)(uint)(ActiveZoneCell + 1) << 48) ^ ((ulong)(uint)(ActiveZoneNode + 1) << 8) ^ ((ulong)(uint)ActiveZoneTaken << 24) ^ ((ulong)(ActiveZoneSinceTick + 1) << 40));
            r.Field = Field.Hash();
            ulong h = 0;
            foreach (var s in Services)
            {
                var t = s.Train;
                h += Mix(((ulong)(uint)t.Id << 40) ^ ((ulong)(uint)t.Leg << 32) ^ (ulong)t.OffsetMm);
                h += Mix(((ulong)t.State << 48) ^ ((ulong)(uint)s.StopIndex << 16) ^ (ulong)(uint)s.CargoTotal());
                for (int r2 = 0; r2 < NetConst.ResCount; r2++) h += Mix(((ulong)(uint)t.Id << 8) ^ (ulong)r2 ^ ((ulong)(uint)s.Cargo[r2] << 20));
                for (int k = 0; k < s.DepartedDay.Count; k++) h += Mix(((ulong)(uint)t.Id << 52) ^ ((ulong)(uint)k << 40) ^ (ulong)(s.DepartedDay[k] + 1));
                h += Mix(((ulong)(uint)t.Id << 4) ^ (s.AtStop ? 1UL : 0UL) ^ (s.Held ? 2UL : 0UL));
            }
            r.Trains = h;
            h = 0;
            for (int n = 0; n < Graph.Nodes.Count; n++)
            {
                h += Mix(((ulong)(uint)n << 32) ^ (ulong)(uint)Attraction[n]);
                for (int r2 = 0; r2 < NetConst.ResCount; r2++) h += Mix(((ulong)(uint)n << 40) ^ ((ulong)r2 << 32) ^ (ulong)(uint)Stock[n, r2]);
            }
            r.Stock = h;
            h = 0;
            foreach (var e in Graph.Edges) h += Mix(((ulong)(uint)e.Id << 8) ^ (ulong)e.State);
            r.Edges = h;
            r.Memory = (Traffic != null ? Traffic.Hash() : 0) + Mix(0x1E7E1UL ^ (ulong)(uint)Intel.Count) + Director.Hash();
            foreach (int pa in PendingAmbushes) r.Memory += Mix(0xA3B05UL ^ (ulong)(uint)pa);
            if (Encounter.Active) r.Memory += Mix(0xE9C0UL ^ ((ulong)(uint)Encounter.Cell << 32) ^ ((ulong)(uint)Encounter.Taken << 8) ^ (ulong)Encounter.ExpireTick ^ ((ulong)(uint)Encounter.Service << 56));
            return r;
        }

        public ulong Hash() { return Systems().Total; }
    }

    // ================================================================== escenario del MVP
    public static class NetScenario
    {
        /// <summary>
        /// Arranque del prototipo de red: la red del MVP, 3 trenes con horario diario y carga, existencias en las estaciones y tres
        /// manchas de horda. Todo fijo (sin azar): dos mundos creados así son idénticos.
        /// </summary>
        public static NetWorld Mvp()
        {
            var w = new NetWorld(RailNetSamples.Mvp());
            var g = w.Graph;
            int Id(string name) { foreach (var n in g.Nodes) if (n.Name == name) return n.Id; throw new ArgumentException(name); }

            // atracción base: tamaño de la ciudad (PROTOTIPO)
            void Base(string n, int a) { int i = Id(n); w.BaseAttraction[i] = a; w.Attraction[i] = a; }
            Base("Valencia", 60); Base("Sagunto", 40); Base("Puçol", 8); Base("Almenara", 6); Base("Nules", 8); Base("Castellón", 40);
            Base("Segorbe", 10); Base("Barracas", 4); Base("Teruel", 20); Base("Caminreal", 4); Base("Calamocha", 5); Base("Cariñena", 6);
            Base("Zaragoza", 50);

            // existencias iniciales (toneladas; personas = plazas)
            void Stock(string n, int mun, int comb, int mat, int per) { int i = Id(n); w.Stock[i, 0] = mun; w.Stock[i, 1] = comb; w.Stock[i, 2] = mat; w.Stock[i, 3] = per; }
            Stock("Valencia", 600, 800, 900, 300);
            Stock("Sagunto", 200, 1400, 700, 170);              // incluye el Puerto (D-N14): combustible y acero
            Stock("Castellón", 100, 100, 100, 80);
            Stock("Teruel", 20, 30, 40, 40);

            int[] T(int mun, int comb, int mat, int per) { return new[] { mun, comb, mat, per }; }
            ScheduleStop Stop(string n, double hour, int[] tr) { return new ScheduleStop { Node = Id(n), DepartAt = NetConst.Hours(hour), Transfer = tr }; }

            // Dos trenes al empezar (balance de Hugo: trenes_inicio = 2; el Tren 3 está a medio montar en el taller de Sagunto)
            // Tren 1: lanzadera de la costa Valencia–Sagunto–Castellón
            Add(w, 1, "1 Costa", Stop("Valencia", 6, T(80, 0, 120, 40)), Stop("Sagunto", 9, T(-40, 150, -60, -20)),
                Stop("Castellón", 11, T(-30, -100, 0, 0)), Stop("Sagunto", 14, T(0, -50, 0, 0)));
            // Tren 2: Sagunto–Teruel, el de montaña (abastece a Teruel)
            Add(w, 2, "2 Montaña", Stop("Sagunto", 7, T(60, 40, 80, 20)), Stop("Teruel", 15, T(-60, -40, -80, -20)));

            // tres manchas: al oeste de Segorbe (interior), al norte de Castellón y al este de Teruel
            // D-N37b: paisaje (lejos de la vía, no cuenta para el combate) y guarnición (a tiro de la vía: ~1.600 ‰, lo que paga el balance de Hugo)
            w.Field.BlobStatic(30, 74, 6, 550);                // paisaje: monte al oeste de Segorbe, lejos de la vía
            w.Field.BlobStatic(58, 63, 1, 160);                // guarnición pegada a Castellón
            w.Field.BlobStatic(42, 62, 1, 160);                // guarnición pegada a Barracas
            w.Field.BlobStatic(61, 55, 5, 600);           // al norte de Castellón, tierra adentro (no en el mar)
            w.Field.BlobStatic(42, 44, 4, 500);                // paisaje: al este de Teruel, lejos de la vía
            w.Tick = NetConst.Hours(5.5);          // el prototipo arranca a las 05:30, media hora antes del primer tren
            return w;
        }

        static void Add(NetWorld w, int id, string name, params ScheduleStop[] stops)
        {
            var s = new TrainService { Train = new RailTrain { Id = id, Name = name, SpeedMmPerTick = NetConst.TrainMmPerTick } };
            s.Stops.AddRange(stops);
            s.StopIndex = 0;
            w.Services.Add(s);
        }
    }
}
