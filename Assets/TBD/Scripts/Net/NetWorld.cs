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
    public enum NetCmdType : byte { SetSpeed = 0, Pause = 1, Resume = 2, TrainGo = 3, TrainHold = 4, Materialize = 5, Dematerialize = 6, ConsumeAmmo = 7 }

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
        int[] _next = new int[W * H];
        long[] _pot = new long[W * H];

        public int At(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return 0;
            return D[y * W + x];
        }

        public long Mass() { long m = 0; for (int i = 0; i < D.Length; i++) m += D[i]; return m; }

        /// <summary>Siembra una mancha redonda (para el arranque y para las pruebas).</summary>
        public void Blob(int cx, int cy, int r, int value)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy > r * r) continue;
                    int i = y * W + x;
                    D[i] = Math.Min(Max, D[i] + value * (r * r - (dx * dx + dy * dy) / 2) / (r * r));
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

            Array.Clear(_next, 0, _next.Length);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    int d = D[i];
                    if (d == 0) continue;
                    int keep = d;
                    if (i == Frozen) { _next[i] += keep; continue; }
                    // difusión: 1/48 a cada vecino (nunca hacia la celda congelada)
                    int share = d / 48;
                    if (share > 0)
                    {
                        if (x > 0 && i - 1 != Frozen) { _next[i - 1] += share; keep -= share; }
                        if (x < W - 1 && i + 1 != Frozen) { _next[i + 1] += share; keep -= share; }
                        if (y > 0 && i - W != Frozen) { _next[i - W] += share; keep -= share; }
                        if (y < H - 1 && i + W != Frozen) { _next[i + W] += share; keep -= share; }
                    }
                    // deriva: 1/60 hacia el vecino de más potencial (orden fijo de vecinos en los empates), si no está ya cargado
                    int best = -1;
                    long bp = _pot[i];
                    if (x > 0 && _pot[i - 1] > bp) { bp = _pot[i - 1]; best = i - 1; }
                    if (x < W - 1 && _pot[i + 1] > bp) { bp = _pot[i + 1]; best = i + 1; }
                    if (y > 0 && _pot[i - W] > bp) { bp = _pot[i - W]; best = i - W; }
                    if (y < H - 1 && _pot[i + W] > bp) { bp = _pot[i + W]; best = i + W; }
                    if (best >= 0 && best != Frozen && D[best] < DriftStop)
                    {
                        int move = d / 60;
                        _next[best] += move;
                        keep -= move;
                    }
                    _next[i] += keep;
                }
            // tope por celda: lo que sobra se queda (no se pierde masa); se reparte al vecino libre en orden fijo
            for (int i = 0; i < _next.Length; i++)
            {
                if (_next[i] <= Max) continue;
                int over = _next[i] - Max;
                _next[i] = Max;
                int x = i % W, y = i / W;
                int[] nb = { x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < H - 1 ? i + W : -1 };
                foreach (int j in nb)
                {
                    if (j < 0 || j == Frozen || over == 0) continue;
                    int room = Max - _next[j];
                    if (room <= 0) continue;
                    int m = Math.Min(room, over);
                    _next[j] += m;
                    over -= m;
                }
                _next[i] += over;            // si no cabe en ningún vecino, se queda por encima del tope (raro)
            }
            var t = D; D = _next; _next = t;
        }

        public ulong Hash()
        {
            ulong h = 0;
            for (int i = 0; i < D.Length; i++) if (D[i] != 0) h += NetWorld.Mix(((ulong)i << 20) ^ (ulong)D[i]);
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
        public int ActiveZoneNode = -1;             // nodo con zona materializada (-1 = ninguna; el MVP admite una)
        public int ActiveZoneTaken;                 // densidad (por mil) que salió de la mancha al materializar
        public long ActiveZoneSinceTick = -1;
        public int ActiveZoneCell = -1;             // celda de la mancha de la que salió la horda (y a la que vuelve)
        public List<Alert> Alerts = new List<Alert>();
        readonly List<NetCommand> _queue = new List<NetCommand>();

        public const int SiegeAt = 500, ThreatAt = 150, CutAt = 850, NearAt = 250;

        public NetWorld(RailGraph g)
        {
            Graph = g;
            Attraction = new int[g.Nodes.Count];
            BaseAttraction = new int[g.Nodes.Count];
            Stock = new int[g.Nodes.Count, NetConst.ResCount];
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
                    if (Field.D[c] > best) { best = Field.D[c]; i = c; }
                }
            ActiveZoneCell = i;
            ActiveZoneNode = node;
            ActiveZoneTaken = Field.D[i];
            ActiveZoneSinceTick = Tick;
            Field.D[i] = 0;
            Field.Frozen = i;
        }

        /// <summary>Los zombis vivos de la zona vuelven a la celda del nodo (las bajas se pierden: la horda pierde masa, D-N05).</summary>
        void Dematerialize(int node, int returnPermil)
        {
            if (node != ActiveZoneNode) return;
            int i = ActiveZoneCell;
            Field.D[i] += Math.Max(0, returnPermil);
            ActiveZoneCell = -1;
            Field.Frozen = -1;
            ActiveZoneNode = -1;
            ActiveZoneTaken = 0;
            ActiveZoneSinceTick = -1;
        }

        // ---------------------------------------------------------------- un tick
        public void Step()
        {
            ApplyCommands();                 // en cada tick, no al ritmo de los fotogramas (QA Q5)
            if (Paused) return;
            for (int s = 0; s < Services.Count; s++) StepService(Services[s]);
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
            if (Tick % NetConst.FieldEvery == 0)
            {
                var prev = Alerts;
                Alerts = AlertEngine.Evaluate(Graph, Trains(), Tick);
                // D-N11: una alerta crítica nueva baja el tiempo a x1 (se puede desactivar). Sale del estado: es determinista.
                if (AutoSlow && Speed > 1)
                    foreach (var al in Alerts)
                        if (al.Priority == 0 && !prev.Exists(p => p.Id == al.Id)) { Speed = 1; break; }
            }
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
                s.AtStop = false;
                s.StopIndex = next;
                return;
            }

            if (s.Held) return;
            if (t.State == RailTrainState.Detenido)
            {
                var e = t.CurrentEdge(Graph);
                if (e != null && e.State != EdgeState.Cortada) t.Go();    // la vía ha quedado libre: sigue
            }
            int legBefore = t.Leg;
            t.Step(Graph);
            if (t.Leg != legBefore && t.Leg < t.Route.Count) MakeNoise(t.Route[t.Leg]);
            if (t.State == RailTrainState.Llegado)
            {
                MakeNoise(s.Stops[s.StopIndex].Node);
                s.AtStop = true;
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
                else if (e.State == EdgeState.Cortada) e.State = EdgeState.Ok;  // PROTOTIPO: se despeja sola (las obras llegan en el hito 5)
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
            public ulong Control, Field, Trains, Stock, Edges;
            public ulong Total => Control + Field + Trains + Stock + Edges;
        }

        public static readonly string[] SystemNames = { "control", "mancha", "trenes", "existencias", "vias" };

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
            w.Field.Blob(40, 66, 6, 550);
            w.Field.Blob(61, 55, 5, 600);           // al norte de Castellón, tierra adentro (no en el mar)
            w.Field.Blob(37, 46, 4, 500);
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
