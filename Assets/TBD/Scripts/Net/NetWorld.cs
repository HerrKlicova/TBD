using System;
using System.Collections.Generic;

namespace TBD.Net
{
    // Hito 2 «Red» (GDD §6 S0, §7, §15): la simulación global. Lógica pura en enteros, sin Unity: se prueba con dotnet.
    // Reloj: 1 tick = 2,4 s de juego (un día de juego = 36.000 ticks = 20 min reales a x1 y 30 Hz, GDD §3).
    // Todo lo que hace el jugador entra como orden (NetCommand) con número de tick, incluidas la velocidad y la pausa (GDD §17.3, regla 9).

    public enum Res : byte { Municion = 0, Combustible = 1, Materiales = 2, Personas = 3, Viveres = 4 }   // los 5 recursos del MVP (GDD §9; D-N176: los víveres, al final para no mover índices)

    public static class NetConst
    {
        public const int TickRate = 30;
        /// <summary>D-N161 (reloj único): 24 h de juego = 14 min reales a 30 Hz (×102,86). 25.200 ticks por día y 1.050 por hora; 1 tick = 24/7 s de juego (3,43 s).
        /// No cabe en ms enteros: los ms de juego salen de GameMs (24.000/7 por tick), sin decimales.</summary>
        public const int DayTicks = 25_200;
        public const int HourTicks = DayTicks / 24;                         // 1.050
        public const long GameMsNum = 24_000, GameMsDen = 7;               // ms de juego por tick = 24.000 / 7
        public static long GameMs(long ticks) { return ticks * GameMsNum / GameMsDen; }
        public const int ResCount = 5;
        public const int FieldEvery = 15;                                   // la mancha avanza cada 15 ticks (medio segundo real a x1)
        public const long TrainMmPerTick = 57_143;                          // 60 km/h con 24/7 s por tick (57.142,9 mm)
        public const int TrainCapacity = 400;                               // toneladas (PROTOTIPO)
        public const int TrainHpMax = 120_000;                  // D-N105: vida del tren (100 zombis en contacto lo tiran)

        public static long Hours(double h) { return (long)Math.Round(h * HourTicks); }
        public static string Clock(long tick)
        {
            long t = tick % DayTicks;
            long secs = t * 86400 / DayTicks;
            return (secs / 3600).ToString("00") + ":" + (secs / 60 % 60).ToString("00");
        }
    }

    // ================================================================== órdenes
    public enum NetCmdType : byte { SetSpeed = 0, Pause = 1, Resume = 2, TrainGo = 3, TrainHold = 4, Materialize = 5, Dematerialize = 6, ConsumeAmmo = 7, ResolveAmbush = 8, PlaceWork = 9, AssignWorkers = 10, CancelWork = 11, FoundStation = 12, HaltAtSite = 13, RepairEdge = 14, SetGate = 15, OrderTrain = 16, CancelTrainOrder = 17, SetChispas = 18, SetSchedule = 19, TrainLoad = 20, TrainUnload = 21, SpendMaterials = 22, TrainCargo = 23, LoseNode = 24 }

    public struct NetCommand
    {
        public long Tick;          // tick en que se aplica
        public NetCmdType Type;
        public int Arg;            // velocidad (1, 2, 4), id de tren o id de nodo
        public int Arg2;           // Dematerialize: densidad que vuelve a la celda (por mil, 1 = 10 zombis vivos)
        public int Seq;            // orden de llegada (lo pone NetWorld.Push): desempata órdenes del mismo tick
        public int[] Payload;      // datos largos de la orden (null en casi todas). PlaceWork: puntos del muro en mm locales al nodo
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
        public bool ScheduleEdited;        // el jugador le ha puesto horario (SetSchedule): entra en el hash con sus paradas y cargas
        public bool WorksTrain;            // tren de obras (hito 5): con materiales y obreros a bordo repara tramos cortados por escombros
        public int HaltAt = -1;            // «detener en el emplazamiento» (hito 5): nodo donde el tren para al llegar (-1 = ninguno)
        public bool Ambushed;              // detenido por una emboscada hasta que se resuelva (zona o expiración)
        public long LastAmbushDay = -1;    // como mucho una emboscada por tren y día (D-N39)
        public long LastDepartDay = -1;    // último día en que salió de alguna parada (informativo)
        public readonly List<long> DepartedDay = new List<long>();   // por parada: día de la última salida (no sale dos veces el mismo día)
        public int GateNode = -1;          // D-N152: nodo a cuyas puertas espera (sitiado, o sin andén libre en su parada); -1 = ninguno
        public long GateSinceTick = -1;    // D-N152: tick en que llegó a las puertas (entran por orden de llegada)
        public int GateHorde;              // D-N177: zombis del asedio cuando llegó a las puertas (para el combate fuera de pantalla)
        public int Hp = NetConst.TrainHpMax; // D-N177 (Hugo): vida del tren, 120.000 (D-N105)
        public bool Destroyed;             // D-N177: cayó a las puertas; se pierde con la carga y la tropa a bordo (todo o nada)

        public void SaveState(SaveWriter w)
        {
            Train.SaveState(w);
            w.Int(Stops.Count);
            foreach (var s in Stops) { w.Int(s.Node); w.Long(s.DepartAt); w.Ints(s.Transfer); }
            w.Ints(Cargo);
            w.Int(StopIndex); w.Bool(AtStop); w.Bool(Held); w.Bool(ScheduleEdited); w.Bool(WorksTrain); w.Int(HaltAt); w.Bool(Ambushed); w.Long(LastAmbushDay); w.Long(LastDepartDay);
            w.Int(DepartedDay.Count); foreach (var d in DepartedDay) w.Long(d);
            w.Int(GateNode); w.Long(GateSinceTick); w.Int(GateHorde); w.Int(Hp); w.Bool(Destroyed);
        }

        public static TrainService LoadState(SaveReader r)
        {
            var s = new TrainService { Train = RailTrain.LoadState(r) };
            int n = r.Int();
            if (n < 0 || n > r.Remaining) throw new SaveException(SaveError.Corrupt, "paradas no válidas");
            for (int i = 0; i < n; i++) s.Stops.Add(new ScheduleStop { Node = r.Int(), DepartAt = r.Long(), Transfer = r.Ints() });
            var cargo = r.Ints();
            if (cargo == null || cargo.Length != s.Cargo.Length) throw new SaveException(SaveError.TopologyMismatch, "carga con otros recursos");
            Array.Copy(cargo, s.Cargo, cargo.Length);
            s.StopIndex = r.Int(); s.AtStop = r.Bool(); s.Held = r.Bool(); s.ScheduleEdited = r.Bool(); s.WorksTrain = r.Bool(); s.HaltAt = r.Int(); s.Ambushed = r.Bool(); s.LastAmbushDay = r.Long(); s.LastDepartDay = r.Long();
            n = r.Int();
            if (n < 0 || n > r.Remaining) throw new SaveException(SaveError.Corrupt, "días de salida no válidos");
            for (int i = 0; i < n; i++) s.DepartedDay.Add(r.Long());
            s.GateNode = r.Int(); s.GateSinceTick = r.Long(); s.GateHorde = r.Int(); s.Hp = r.Int(); s.Destroyed = r.Bool();
            return s;
        }

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

        public void SaveState(SaveWriter w)
        {
            w.Ints(D); w.Ints(S); w.Int(Frozen);
            w.Bools(_leash);                  // la correa se fijó al sembrar la horda inicial y no se puede deducir de lo que queda; «_block» sí sale del mapa y se rehace sola
        }

        public void LoadState(SaveReader r)
        {
            var d = r.Ints(); var s = r.Ints();
            if (d == null || s == null || d.Length != W * H || s.Length != W * H) throw new SaveException(SaveError.TopologyMismatch, "la mancha de horda tiene otro tamaño");
            D = d; S = s; Frozen = r.Int();
            var l = r.Bools();
            if (l != null && l.Length != W * H) throw new SaveException(SaveError.TopologyMismatch, "la correa de la horda tiene otro tamaño");
            _leash = l; _block = null;
            _next = new int[W * H]; _nextS = new int[W * H];       // búferes de trabajo: se vacían antes de usarse
        }

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
        /// <summary>Guarnición: un núcleo de «core» en la celda y «ring» en las cuatro vecinas (horda estática con correa).</summary>
        public void SeedStatic(int x, int y, int core, int ring)
        {
            S[y * W + x] += core;
            if (x > 0) S[y * W + x - 1] += ring;
            if (x < W - 1) S[y * W + x + 1] += ring;
            if (y > 0) S[(y - 1) * W + x] += ring;
            if (y < H - 1) S[(y + 1) * W + x] += ring;
            MarkLeash();
        }

        /// <summary>D-N183: quita la guarnición estática sembrada con SeedStatic en (x, y) (núcleo y las cuatro vecinas). Para una estación que pasa a ser del jugador.</summary>
        public void ClearStaticAt(int x, int y)
        {
            S[y * W + x] = 0;
            if (x > 0) S[y * W + x - 1] = 0;
            if (x < W - 1) S[y * W + x + 1] = 0;
            if (y > 0) S[(y - 1) * W + x] = 0;
            if (y < H - 1) S[(y + 1) * W + x] = 0;
        }

        public void BlobStatic(int cx, int cy, int r, int value)
        {
            Blob(cx, cy, r, value, S);
            MarkLeash();
        }

        void MarkLeash()
        {
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
                foreach (var e in g.Edges)                              // a tiro de la vía (1 celda): la horda inicial puede irse pero no entrar (D-N39)
                {
                    var a = g.Nodes[e.A]; var b = g.Nodes[e.B];
                    int steps = Math.Max(Math.Abs(b.XMeters - a.XMeters), Math.Abs(b.YMeters - a.YMeters)) * 2 + 1;
                    for (int k = 0; k <= steps; k++)
                    {
                        int px = a.XMeters + (b.XMeters - a.XMeters) * k / steps, py = a.YMeters + (b.YMeters - a.YMeters) * k / steps;
                        for (int y = py - 1; y <= py + 1; y++)
                            for (int x = px - 1; x <= px + 1; x++)
                                if (x >= 0 && y >= 0 && x < W && y < H) _block[y * W + x] = true;
                    }
                }
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
    public sealed partial class NetWorld
    {
        public readonly RailGraph Graph;
        public readonly List<TrainService> Services = new List<TrainService>();
        public readonly HordeField Field = new HordeField();
        public readonly int[] Attraction;          // por nodo: base de la ciudad + ruido del tráfico
        public readonly int[] BaseAttraction;
        public readonly int[,] Stock;              // por nodo y recurso
        public readonly StationState Stations;     // hito 5: qué emplazamientos están fundados (flag dentro del hash, sin nodos en caliente)
        public readonly TallerState Taller;        // hito 5, paso 5: la cola del taller de Sagunto (dentro del hash, en «existencias»)
        public readonly ObrasState Works;          // hito 5: lo construido en cada nodo (dentro del hash, en «existencias»)
        public readonly ProductionState Production; // producción diaria y tope del almacén (Produccion.cs; dentro del hash, en «existencias»)
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
        public const long AmbushWaitTicks = NetConst.HourTicks / 2;     // 30 min de juego (525 ticks con el reloj ×103)
        public const int AmbushCargoLossMil = 200;         // carga que pierde el tren si no se resuelve (D-N39, Hugo: 20 %)
        public readonly List<int> PendingAmbushes = new List<int>();      // tramos con emboscada preparada (TR-3 cuando el tren cae en ella)
        readonly List<NetCommand> _queue = new List<NetCommand>();

        public const int SiegeAt = 500, ThreatAt = 150, CutAt = 850, NearAt = 250;

        // ---------------------------------------------------------------- guardado y carga (Guardado.cs, GDD §14 y §17.3)
        // Se guarda el estado completo entre dos ticks. NO se guardan: la configuración (mapa y CSV de balance, que pone el escenario), los datos de «este
        // tick» para QA (Injections, Retirements, LostThisTick, RetiredThisTick, se vacían al empezar cada tick), el gancho ZoneDensity (lo enchufa la zona)
        // ni los búferes de trabajo. Si cambias algo de aquí, sube NetSave.NetVersion y registra la migración (NetSave.NetMigrations).
        public void SaveState(SaveWriter w)
        {
            w.Long(Tick); w.Int(Speed); w.Bool(Paused); w.Bool(AutoSlow); w.Bool(DirectorEnabled);
            w.Long(InjectedTotal); w.Long(LostTotal); w.Long(RetiredTotal);
            w.Int(ActiveZoneTakenS); w.Int(ActiveZoneNode); w.Int(ActiveZoneTaken); w.Long(ActiveZoneSinceTick); w.Int(ActiveZoneCell);
            w.Bool(Encounter.Active); w.Int(Encounter.Service); w.Int(Encounter.Edge); w.Int(Encounter.Cell); w.Int(Encounter.Taken); w.Long(Encounter.ExpireTick);
            w.Int(PendingAmbushes.Count); foreach (var e in PendingAmbushes) w.Int(e);
            w.Int(_seq);
            w.Int(_queue.Count);
            foreach (var c in _queue) { w.Long(c.Tick); w.Byte((byte)c.Type); w.Int(c.Arg); w.Int(c.Arg2); w.Int(c.Seq); w.Ints(c.Payload); }
            w.Ints(Attraction); w.Ints(BaseAttraction); w.Ints2(Stock);
            Graph.SaveState(w);
            w.Int(Services.Count); foreach (var s in Services) s.SaveState(w);
            Field.SaveState(w);
            Stations.SaveState(w);
            Taller.SaveState(w);
            Works.SaveState(w);
            w.Bool(Traffic != null); if (Traffic != null) Traffic.SaveState(w);
            w.Int(Intel.Count); foreach (var e in Intel) { w.Long(e.Tick); w.Str(e.Id); w.Strs(e.Vars); }
            w.Int(Alerts == null ? -1 : Alerts.Count);
            if (Alerts != null) foreach (var a in Alerts) { w.Int(a.Id); w.Byte((byte)a.Kind); w.Int(a.Priority); w.Long(a.EtaTicks); w.Int(a.Subject); w.Str(a.Text); }
            Director.SaveState(w);
            Production.SaveState(w);                    // NetVersion 2
            Embarque.SaveState(w);                      // NetVersion 3 (D-N166)
            w.Ints(FoodRemMil); w.Ints(StarveRemMil); w.Long(StarvedTotal);   // NetVersion 4 (D-N176)
            var gf = LastGateFight; w.Int(GateFights); w.Ints(new[] { gf.Service, gf.Node, gf.Horde, gf.Attackers, gf.Soldiers, gf.Killed, gf.Contact, gf.HpLost, gf.EscortLostMil, gf.AmmoU, gf.Destroyed ? 1 : 0 }); w.Long(gf.Tick);   // D-N177
            var lost = new int[NodeLost.Length]; for (int q = 0; q < lost.Length; q++) lost[q] = NodeLost[q] ? 1 : 0; w.Ints(lost); w.Ints(GarrisonMil); w.Ints(ZoneWallE4);    // NetVersion 5: hordas de zona
            var zs = LastZoneSiege; w.Ints(new[] { zs.Node, zs.Lost ? 1 : 0, zs.OnScreen ? 1 : 0 }); w.Long(zs.Zombies); w.Long(zs.Killed); w.Long(zs.SpentU); w.Long(zs.DamageU); w.Long(zs.Casualties); w.Long(zs.Tick);
            w.Ints(new[] { Director.ColState, Director.ColDaysBelow, Director.ColIntroDone ? 1 : 0 }); w.Long(Director.ColSignalMil); w.Ints(Director.ColHeld);      // NetVersion 6: estados de la Colmena (D-N160)
            if (Traffic != null) Traffic.SaveStreaks(w); else for (int q = 0; q < 4; q++) w.Ints(new int[0]);      // NetVersion 7: rachas de la memoria de tráfico (informe)
        }

        public void LoadState(SaveReader r)
        {
            Tick = r.Long(); Speed = r.Int(); Paused = r.Bool(); AutoSlow = r.Bool(); DirectorEnabled = r.Bool();
            InjectedTotal = r.Long(); LostTotal = r.Long(); RetiredTotal = r.Long();
            ActiveZoneTakenS = r.Int(); ActiveZoneNode = r.Int(); ActiveZoneTaken = r.Int(); ActiveZoneSinceTick = r.Long(); ActiveZoneCell = r.Int();
            Encounter = new AmbushEncounter { Active = r.Bool(), Service = r.Int(), Edge = r.Int(), Cell = r.Int(), Taken = r.Int(), ExpireTick = r.Long() };
            int n = r.Int();
            if (n < 0 || n > r.Remaining) throw new SaveException(SaveError.Corrupt, "emboscadas pendientes no válidas");
            PendingAmbushes.Clear(); for (int i = 0; i < n; i++) PendingAmbushes.Add(r.Int());
            _seq = r.Int();
            n = r.Int();
            if (n < 0 || n > r.Remaining) throw new SaveException(SaveError.Corrupt, "cola de órdenes no válida");
            _queue.Clear();
            for (int i = 0; i < n; i++) _queue.Add(new NetCommand { Tick = r.Long(), Type = (NetCmdType)r.Byte(), Arg = r.Int(), Arg2 = r.Int(), Seq = r.Int(), Payload = r.Ints() });
            ReadInto(r.Ints(), Attraction, "atracción"); ReadInto(r.Ints(), BaseAttraction, "atracción base");
            r.Ints2Into(Stock);
            Graph.LoadState(r);
            n = r.Int();
            if (n < 0 || n > r.Remaining) throw new SaveException(SaveError.Corrupt, "trenes no válidos");
            Services.Clear(); for (int i = 0; i < n; i++) Services.Add(TrainService.LoadState(r));
            Field.LoadState(r);
            Stations.LoadState(r);
            Taller.LoadState(r);
            Works.LoadState(r);
            bool hasTraffic = r.Bool();
            if (hasTraffic != (Traffic != null)) throw new SaveException(SaveError.TopologyMismatch, "memoria de tráfico presente solo en uno de los dos");
            if (hasTraffic) Traffic.LoadState(r);
            n = r.Int();
            if (n < 0 || n > r.Remaining) throw new SaveException(SaveError.Corrupt, "informe no válido");
            Intel.Clear(); for (int i = 0; i < n; i++) Intel.Add(new IntelEntry { Tick = r.Long(), Id = r.Str(), Vars = r.Strs() });
            n = r.Int();
            if (n < -1 || n > r.Remaining) throw new SaveException(SaveError.Corrupt, "alertas no válidas");
            if (n < 0) Alerts = null;
            else { Alerts = new List<Alert>(n); for (int i = 0; i < n; i++) Alerts.Add(new Alert { Id = r.Int(), Kind = (AlertKind)r.Byte(), Priority = r.Int(), EtaTicks = r.Long(), Subject = r.Int(), Text = r.Str() }); }
            Director.LoadState(r);
            Production.LoadState(r);
            Embarque.LoadState(r);
            ReadInto(r.Ints(), FoodRemMil, "víveres"); ReadInto(r.Ints(), StarveRemMil, "hambre"); StarvedTotal = r.Long();
            GateFights = r.Int(); var gv = r.Ints(); if (gv == null || gv.Length != 11) throw new SaveException(SaveError.Corrupt, "combate a las puertas no válido");
            LastGateFight = new GateFight { Service = gv[0], Node = gv[1], Horde = gv[2], Attackers = gv[3], Soldiers = gv[4], Killed = gv[5], Contact = gv[6], HpLost = gv[7], EscortLostMil = gv[8], AmmoU = gv[9], Destroyed = gv[10] != 0, Tick = r.Long() };
            var lv = r.Ints(); if (lv == null || (lv.Length != 0 && lv.Length != NodeLost.Length)) throw new SaveException(SaveError.TopologyMismatch, "nodos perdidos");
            for (int q = 0; q < NodeLost.Length; q++) NodeLost[q] = lv.Length != 0 && lv[q] != 0;
            var gv5 = r.Ints(); if (gv5 == null || (gv5.Length != 0 && gv5.Length != GarrisonMil.Length)) throw new SaveException(SaveError.TopologyMismatch, "guarniciones");
            for (int q = 0; q < GarrisonMil.Length; q++) GarrisonMil[q] = gv5.Length != 0 ? gv5[q] : 0;
            var wv5 = r.Ints(); if (wv5 == null || (wv5.Length != 0 && wv5.Length != ZoneWallE4.Length)) throw new SaveException(SaveError.TopologyMismatch, "recintos abstractos");
            for (int q = 0; q < ZoneWallE4.Length; q++) ZoneWallE4[q] = wv5.Length != 0 ? wv5[q] : 0;
            var zv = r.Ints(); if (zv == null || zv.Length != 3) throw new SaveException(SaveError.Corrupt, "horda de zona no válida");
            LastZoneSiege = new ZoneSiegeResult { Node = zv[0], Lost = zv[1] != 0, OnScreen = zv[2] != 0, Zombies = r.Long(), Killed = r.Long(), SpentU = r.Long(), DamageU = r.Long(), Casualties = r.Long(), Tick = r.Long() };
            var cv = r.Ints(); if (cv == null || cv.Length != 3) throw new SaveException(SaveError.Corrupt, "estado de la Colmena no válido");
            Director.ColState = cv[0]; Director.ColDaysBelow = cv[1]; Director.ColIntroDone = cv[2] != 0; Director.ColSignalMil = r.Long();
            var ch = r.Ints(); if (ch == null || ch.Length != Director.ColHeld.Length) throw new SaveException(SaveError.Corrupt, "historia de la Colmena no válida");
            Array.Copy(ch, Director.ColHeld, ch.Length);
            if (Traffic != null) Traffic.LoadStreaks(r); else for (int q = 0; q < 4; q++) { var e7 = r.Ints(); if (e7 == null || e7.Length != 0) throw new SaveException(SaveError.TopologyMismatch, "rachas sin memoria de tráfico"); }
            Injections.Clear(); Retirements.Clear(); LostThisTick = 0; RetiredThisTick = 0;      // datos de QA del tick: empiezan limpios
        }

        static void ReadInto(int[] src, int[] dst, string what)
        {
            if (src == null || src.Length != dst.Length) throw new SaveException(SaveError.TopologyMismatch, what + ": otro número de nodos");
            Array.Copy(src, dst, dst.Length);
        }

        public NetWorld(RailGraph g)
        {
            Graph = g;
            Attraction = new int[g.Nodes.Count];
            BaseAttraction = new int[g.Nodes.Count];
            Stock = new int[g.Nodes.Count, NetConst.ResCount];
            FoodRemMil = new int[g.Nodes.Count]; StarveRemMil = new int[g.Nodes.Count]; NodeLost = new bool[g.Nodes.Count]; GarrisonMil = new int[g.Nodes.Count]; ZoneWallE4 = new int[g.Nodes.Count];
            Works = new ObrasState(g.Nodes.Count, BalanceSource.Obras());
            Stations = new StationState(g, BalanceSource.Stations());
            Taller = new TallerState(g, BalanceSource.Taller());
            Production = new ProductionState(g, BalanceSource.Production());
            Traffic = new TrafficMemory(g.Edges.Count, BalanceSource.Traffic());
            Director.P = BalanceSource.Director();
            Director.Curve = BalanceSource.Curve();
            Director.Season = BalanceSource.Table("colmena_estacion.csv");
            Director.Noise = BalanceSource.Table("colmena_ruido.csv");
            { var ec = BalanceSource.Read("balance_economia.csv"); if (ec != null) { var t = new CsvTable(ec); ZoneZombiesPerU = (int)t.TryValue("combate_z_por_u", ZoneZombiesPerU); ZoneWallBonusMil = (int)t.TryValue("fort_bonus_por_nivel", ZoneWallBonusMil);
                ZoneFireCapPerDefender = (int)t.TryValue("combate_tope_fuego_por_defensor", 0);
                ZoneZombiesPerUNode = new int[g.Nodes.Count]; for (int q = 0; q < g.Nodes.Count; q++) ZoneZombiesPerUNode[q] = (int)t.TryValue("combate_z_por_u_" + TrenPreparar.IdDe(g.Nodes[q].Name), ZoneZombiesPerU); } }   // Hugo, 9-nov: fila por estación (Nules 300) y tope de fuego   // opción A (Hugo y Lucía, 9-nov): 400 por u, sin bonus del muro
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
                    case NetCmdType.ResolveAmbush: ResolveAmbush(c.Arg & 0xFF, c.Arg2, false, c.Arg >> 8); break;       // Arg = servicio | (munición gastada en u) << 8
                    case NetCmdType.Dematerialize: Dematerialize(c.Arg, c.Arg2); break;
                    case NetCmdType.ConsumeAmmo: ConsumeAmmo(c.Arg, c.Arg2); break;
                    case NetCmdType.SpendMaterials: SpendMaterials(c.Arg, c.Arg2); break;      // Arg = nodo, Arg2 = u (D-N163: lo que gastan reparar y reconstruir en la zona)
                    case NetCmdType.AssignWorkers: AssignWorkers(c.Arg, c.Arg2); break;                    // Arg = nodo, Arg2 = obreros
                    case NetCmdType.HaltAtSite: HaltAtSite(c.Arg, c.Arg2); break;                           // Arg = emplazamiento (-1 cancela), Arg2 = servicio
                    case NetCmdType.SetSchedule: SetSchedule(c.Arg, c.Payload); break;
                    case NetCmdType.TrainCargo: MoveCargo(c.Arg, c.Payload); break;      // Arg = tren, Payload = [nodo, 4 cantidades] (Iker: descargar sin salir)
                    case NetCmdType.TrainLoad: if (c.Payload != null && c.Payload.Length == 2) LoadPassenger(c.Arg, (PasajeroTipo)(c.Arg2 & 1), c.Payload[0], c.Payload[1]); break;   // D-N166: Arg = tren, Arg2 = tipo, Payload = [id, nodo]
                    case NetCmdType.TrainUnload: UnloadPassengers(c.Arg, c.Arg2); break;                    // D-N166: Arg = tren, Arg2 = nodo       // Arg = tren, Payload = [nodo, hora de salida (ticks), 4 cargas] por parada
                    case NetCmdType.LoseNode: LoseNode(c.Arg); break;                                   // qa50 S2 (Lucía 7705049): estación perdida en pantalla y «Seguir»
                    case NetCmdType.OrderTrain: OrderTrain((TallerKind)c.Arg); break;
                    case NetCmdType.CancelTrainOrder: CancelTrainOrder(c.Arg); break;
                    case NetCmdType.SetChispas: MoveChispas(c.Arg); break;       // Arg = 0 al taller, k > 0 al tren de índice k − 1
                    case NetCmdType.SetGate: if (c.Payload != null && c.Payload.Length == 2 && (c.Arg & 0xFFFF) == ActiveZoneNode) Works.SetGateMode(c.Arg & 0xFFFF, c.Payload[0], c.Payload[1], c.Arg >> 16); break;     // Arg = nodo | (modo << 16), Payload = [x, y] de la puerta
                    case NetCmdType.RepairEdge: RepairEdge(c.Arg, c.Arg2); break;                           // Arg = tramo, Arg2 = servicio del tren de obras
                    case NetCmdType.FoundStation: FoundStation(c.Arg); break;                               // Arg = nodo
                    case NetCmdType.CancelWork: CancelWork(c.Arg, c.Arg2); break;                          // Arg = nodo, Arg2 = número de obra
                    case NetCmdType.PlaceWork: PlaceWork(c.Arg & 0xFFFF, (PieceKind)(c.Arg >> 16), c.Payload); break;       // Arg = nodo | (PieceKind << 16)
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

        /// <summary>D-N163: la zona gasta materiales de la estación (reparar y reconstruir tramos del muro). Sin bajar de 0. Devuelve lo gastado.</summary>
        public int SpendMaterials(int node, int units)
        {
            if (node < 0 || node >= Graph.Nodes.Count || units <= 0) return 0;
            int take = Math.Min(units, Math.Max(0, Stock[node, (int)Res.Materiales]));
            Stock[node, (int)Res.Materiales] -= take; return take;
        }
        public static NetCommand CmdSpendMaterials(long tick, int node, int units) { return new NetCommand { Tick = tick, Type = NetCmdType.SpendMaterials, Arg = node, Arg2 = units }; }

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

        // ---------------------------------------------------------------- horario de un tren (hito 5: un tren recién fabricado o rehabilitado no se mueve sin él)
        public const int MaxScheduleStops = 12;
        public const int MaxScheduleTransfer = 1000;

        /// <summary>
        /// «Poner horario»: sustituye las paradas de un tren que está parado en una parada. data = por cada parada seis enteros
        /// [nodo, hora de salida en ticks del día, carga de munición, de combustible, de materiales y de personas] (+ carga en el tren, − descarga en la estación).
        /// El horario tiene que incluir el nodo donde está el tren (de ahí sale: es su parada actual); dos paradas seguidas, contando el cierre, no pueden ser el mismo nodo.
        /// Si no vale, no cambia nada.
        /// </summary>
        /// <summary>D-N184 (Eugenio, 9-nov): estación propia pero cercada (Castellón en el MVP). El cerco es de vista y no ataca, pero no se le puede poner horario
        /// (SetSchedule devuelve Cercada) hasta romperlo, que es para después del MVP. La vista lo lee para pintar el cerco y bloquear el botón. −1 = ninguna.
        /// Lo pone NetScenario.Inicio; no se guarda.</summary>
        public int CercoNodo = -1;
        public bool Cercada(int node) { return node >= 0 && node == CercoNodo; }

        public ScheduleResult SetSchedule(int service, int[] data)
        {
            if (service < 0 || service >= Services.Count) return ScheduleResult.TrenInexistente;
            var s = Services[service];
            if (!s.AtStop || s.Ambushed || s.StopIndex < 0 || s.StopIndex >= s.Stops.Count) return ScheduleResult.NoEstaParado;
            int stride = 2 + NetConst.ResCount;
            if (data == null || data.Length == 0 || data.Length % stride != 0 || data.Length / stride > MaxScheduleStops) return ScheduleResult.HorarioInvalido;
            int n = data.Length / stride, cur = s.Stops[s.StopIndex].Node, newIndex = -1;
            for (int k = 0; k < n; k++)
            {
                int node = data[k * stride], at = data[k * stride + 1];
                if (node < 0 || node >= Graph.Nodes.Count || at < 0 || at >= NetConst.DayTicks) return ScheduleResult.HorarioInvalido;
                if (node == CercoNodo) return ScheduleResult.Cercada;
                if (n > 1 && node == data[((k + 1) % n) * stride]) return ScheduleResult.HorarioInvalido;
                for (int r = 0; r < NetConst.ResCount; r++) if (Math.Abs(data[k * stride + 2 + r]) > MaxScheduleTransfer) return ScheduleResult.HorarioInvalido;
                if (newIndex < 0 && node == cur) newIndex = k;
            }
            if (newIndex < 0) return ScheduleResult.NoIncluyeParadaActual;
            s.Stops.Clear();
            for (int k = 0; k < n; k++)
            {
                var tr = new int[NetConst.ResCount];
                for (int r = 0; r < NetConst.ResCount; r++) tr[r] = data[k * stride + 2 + r];
                s.Stops.Add(new ScheduleStop { Node = data[k * stride], DepartAt = data[k * stride + 1], Transfer = tr });
            }
            s.StopIndex = newIndex;
            s.DepartedDay.Clear();                                    // horario nuevo: ninguna parada cuenta como ya salida hoy
            s.ScheduleEdited = true;
            return ScheduleResult.Ok;
        }

        // ---------------------------------------------------------------- obras (hito 5)
        /// <summary>
        /// Paga y registra una obra en el nodo (solo con la zona del nodo materializada: se construye dentro de una zona en detalle).
        /// Los materiales salen de las existencias del nodo; si no alcanzan, el muro se corta donde lo decide Obras.PlanWall.
        /// Devuelve el plan aplicado (null si no hubo orden válida), para que la interfaz muestre el resultado real.
        /// </summary>
        public WorkPlan PlaceWork(int node, PieceKind kind, int[] pts)
        {
            if (node < 0 || node >= Graph.Nodes.Count || node != ActiveZoneNode) return null;
            var plan = PreviewWork(node, kind, pts);
            if (!plan.Ok) return plan;
            Stock[node, (int)Res.Materiales] -= plan.CostU;
            plan.Id = Works.Add(node, plan);
            return plan;
        }

        /// <summary>
        /// Lo mismo que PlaceWork pero sin pagar ni registrar: es lo que enseña el fantasma. Además del coste y el corte, exige terreno asegurado
        /// (sin zombis en pie a menos de SecureMm de la obra) cuando la zona ha enchufado su rejilla en ZoneDensity.
        /// </summary>
        public WorkPlan PreviewWork(int node, PieceKind kind, int[] pts)
        {
            var plan = Works.Preview(node, kind, pts, Stock[node, (int)Res.Materiales]);
            if (!plan.Ok || ZoneDensity == null) return plan;
            var view = ZoneDensity();
            if (view != null && !Terreno.PlanSecure(view, plan, Works.P.SecureMm, out _, out _))
            {
                plan.Ok = false; plan.Why = "Hay zombis cerca: el terreno no está asegurado";
            }
            return plan;
        }

        /// <summary>
        /// Enchufe de la zona (Marcos): devuelve la rejilla de densidad del tick en mm locales al nodo, o null si no hay zona. No entra en el hash:
        /// la zona es determinista y el resultado depende solo de su estado. Sin enchufar (pruebas de la red, goldens) todo terreno vale.
        /// </summary>
        public Func<DensityView> ZoneDensity;
        // ---------------------------------------------------------------- el taller fabrica trenes (hito 5, diseno-hito5.md §5)
        /// <summary>
        /// Encargar un tren al taller. Paga con las existencias del nodo del taller (materiales, combustible y, salvo el tren de obras, su tripulación de 10);
        /// no cobra nada si falla. El tren de obras (rehabilitado o nuevo) solo comprueba que haya sus 8 obreros libres por encima del retén: se descuentan al salir (Hugo y Lucía).
        /// </summary>
        public TallerResult OrderTrain(TallerKind kind)
        {
            if (Taller.NodeIndex < 0) return TallerResult.NoHayTaller;
            if (kind > TallerKind.RehabObras) return TallerResult.TipoInvalido;
            var p = Taller.P;
            bool rehab = kind == TallerKind.RehabTren3 || kind == TallerKind.RehabObras;
            bool works = kind == TallerKind.RehabObras || kind == TallerKind.Obras;
            if (kind == TallerKind.RehabTren3 && Taller.Tren3Done || kind == TallerKind.RehabObras && Taller.ObrasDone) return TallerResult.YaRehabilitado;
            if (Services.Count + Taller.Queue.Count >= p.MaxTrainsNow) return TallerResult.MaximoTrenes;
            int u = kind == TallerKind.RehabTren3 ? p.Tren3U : kind == TallerKind.RehabObras ? p.ObrasU : p.TrainU;
            int fuel = kind == TallerKind.RehabTren3 ? p.Tren3Fuel : kind == TallerKind.RehabObras ? p.ObrasFuel : p.TrainFuel;
            int crew = works ? 0 : p.TrainCrew;        // Hugo: todo tren de servicio (nuevo o el Tren 3 rehabilitado) toma 10; el de obras no: sus 8 obreros son su tripulación
            int n = Taller.NodeIndex;
            if (Stock[n, (int)Res.Materiales] < u) return TallerResult.SinMateriales;
            if (Stock[n, (int)Res.Combustible] < fuel) return TallerResult.SinCombustible;
            if (Stock[n, (int)Res.Personas] - Works.P.GarrisonMin < crew + (works ? p.ObrasCrew : 0)) return TallerResult.SinObreros;     // libres por encima del retén
            Stock[n, (int)Res.Materiales] -= u; Stock[n, (int)Res.Combustible] -= fuel; Stock[n, (int)Res.Personas] -= crew;
            if (kind == TallerKind.RehabTren3) Taller.Tren3Done = true;
            if (kind == TallerKind.RehabObras) Taller.ObrasDone = true;
            Taller.Queue.Add(new TallerOrder
            {
                Kind = kind, PaidU = u, PaidFuel = fuel, PaidCrew = crew,
                NeedTicks = rehab ? NetConst.Hours(p.RehabHours) : (long)p.TrainDays * NetConst.DayTicks
            });
            return TallerResult.Ok;
        }

        /// <summary>
        /// Cancelar un encargo de la cola (misma regla que las obras, D-N59, sobre lo pagado): sin empezar se devuelve el 100 %; empezado, floor(50 % de lo pagado).
        /// La tripulación de un tren nuevo vuelve entera a la población. Un rehabilitado cancelado se puede volver a encargar. Si se había empezado, avisa (TL-3).
        /// </summary>
        public TallerResult CancelTrainOrder(int index)
        {
            if (index < 0 || index >= Taller.Queue.Count) return TallerResult.EncargoInexistente;
            var o = Taller.Queue[index]; int n = Taller.NodeIndex;
            long mil = o.Started ? Taller.P.CancelStartedMil : 1000;
            int backU = (int)(o.PaidU * mil / 1000), backF = (int)(o.PaidFuel * mil / 1000);
            Stock[n, (int)Res.Materiales] += backU; Stock[n, (int)Res.Combustible] += backF; Stock[n, (int)Res.Personas] += o.PaidCrew;
            if (o.Kind == TallerKind.RehabTren3) Taller.Tren3Done = false;
            if (o.Kind == TallerKind.RehabObras) Taller.ObrasDone = false;
            Taller.Queue.RemoveAt(index);
            if (o.Started) Intel.Add(new IntelEntry { Tick = Tick, Id = "TL-3", Vars = new[] { "tren", TrainName(o.Kind, NextTrainId()), "u", backU.ToString() } });
            return TallerResult.Ok;
        }

        /// <summary>
        /// Mover a Chispas: arg 0 = al taller, k &gt; 0 = al tren de índice k − 1. No puede salir del taller hasta que ha rodado el tren de obras (D-N45), y solo
        /// se cambia de sitio con el tren de origen y el de destino parados en el taller.
        /// </summary>
        public TallerResult MoveChispas(int arg)
        {
            if (Taller.ChispasAt == -2) return TallerResult.ChispasNoEsta;
            int to = arg <= 0 ? -1 : arg - 1;
            if (to >= Services.Count) return TallerResult.TrenLejos;
            if (to == Taller.ChispasAt) return TallerResult.Ok;
            if (!Taller.ObrasOut) return TallerResult.ChispasBloqueado;
            if (Taller.ChispasAt >= 0 && !ParkedAtTaller(Taller.ChispasAt)) return TallerResult.TrenLejos;
            if (to >= 0 && !ParkedAtTaller(to)) return TallerResult.TrenLejos;
            Taller.ChispasAt = to;
            return TallerResult.Ok;
        }

        bool ParkedAtTaller(int service)
        {
            var s = Services[service];
            return s.AtStop && s.StopIndex >= 0 && s.StopIndex < s.Stops.Count && s.Stops[s.StopIndex].Node == Taller.NodeIndex;
        }

        int NextTrainId() { int id = 1; foreach (var sv in Services) id = Math.Max(id, sv.Train.Id + 1); return id; }
        static string TrainName(TallerKind k, int id) { return k == TallerKind.RehabTren3 ? id + " Reserva" : k == TallerKind.RehabObras || k == TallerKind.Obras ? id + " Obras" : k == TallerKind.Combate ? id + " Combate" : id + " Carga"; }

        /// <summary>
        /// El taller trabaja las 24 h en el primer encargo de la cola (Hugo: bajo techo, sin horario de obra). Chispas en el taller acelera solo las rehabilitaciones.
        /// Un tren de obras terminado espera en el taller hasta que haya sus 8 obreros libres (TL-2); al salir se descuentan.
        /// </summary>
        void AdvanceTaller()
        {
            if (Taller.Queue.Count == 0) return;
            var o = Taller.Queue[0]; int n = Taller.NodeIndex;
            if (!o.Waiting)
            {
                o.DoneMilli += Taller.RateMilli(o.Kind);
                if (o.DoneMilli < o.NeedTicks * 1000) { Taller.Queue[0] = o; return; }
                o.DoneMilli = o.NeedTicks * 1000;
            }
            bool works = o.Kind == TallerKind.RehabObras || o.Kind == TallerKind.Obras;
            int free = Stock[n, (int)Res.Personas] - Works.P.GarrisonMin;
            if (works && free < Taller.P.ObrasCrew)
            {
                if (!o.Waiting) Intel.Add(new IntelEntry { Tick = Tick, Id = "TL-2", Vars = new[] { "tren", TrainName(o.Kind, NextTrainId()), "n", (Taller.P.ObrasCrew - Math.Max(0, free)).ToString() } });
                o.Waiting = true; Taller.Queue[0] = o;
                return;
            }
            Taller.Queue.RemoveAt(0);
            int id = NextTrainId(); string name = TrainName(o.Kind, id);
            var s = new TrainService { Train = new RailTrain { Id = id, Name = name, SpeedMmPerTick = NetConst.TrainMmPerTick }, StopIndex = 0, AtStop = true };
            s.Stops.Add(new ScheduleStop { Node = n, DepartAt = 0, Transfer = new int[NetConst.ResCount] });
            if (works) { s.WorksTrain = true; Stock[n, (int)Res.Personas] -= Taller.P.ObrasCrew; s.Cargo[(int)Res.Personas] = Taller.P.ObrasCrew; Taller.ObrasOut = true; }
            Services.Add(s);
            Taller.Built++;
            Intel.Add(new IntelEntry { Tick = Tick, Id = "TL-1", Vars = new[] { "tren", name } });     // «El {tren} está listo. Cuando quiera, comandante.» (la escena de cámara es de la interfaz)
        }

        /// <summary>«Asignar obreros»: población del nodo que trabaja en sus obras. Se limita a la población que hay en cada tick; no se gasta.</summary>
        public void AssignWorkers(int node, int workers)
        {
            if (node < 0 || node >= Graph.Nodes.Count) return;
            Works.Workers[node] = Math.Max(0, Math.Min(workers, 10_000));
        }

        // ---------------------------------------------------------------- fundar una estación (hito 5, diseno-hito5.md §4)
        /// <summary>
        /// «Detener en el emplazamiento» (Mak, 6-nov): el tren «service» para en cuanto llegue a ese nodo (justo en el nodo, retenido: TrainGo lo suelta).
        /// Solo emplazamientos. node = -1 cancela. Si ya está parado en el nodo en una parada del horario, no hace falta nada.
        /// </summary>
        public void HaltAtSite(int node, int service)
        {
            if (service < 0 || service >= Services.Count) return;
            var s = Services[service];
            if (node < 0) { s.HaltAt = -1; return; }
            if (node >= Graph.Nodes.Count || !Stations.IsSite(node)) return;
            if (TrainAtNode(s, node)) return;
            s.HaltAt = node;
        }

        /// <summary>¿Está el tren parado en el nodo? En una parada del horario, o detenido a menos de StopRadiusMm del nodo en su tramo.</summary>
        public bool TrainAtNode(TrainService sv, int node)
        {
            if (sv.AtStop && sv.StopIndex >= 0 && sv.StopIndex < sv.Stops.Count && sv.Stops[sv.StopIndex].Node == node) return true;
            var t = sv.Train;
            if (t.State == RailTrainState.EnMarcha) return false;
            var e = t.CurrentEdge(Graph);
            if (e == null) return t.Route.Count > 0 && t.Route[t.Route.Count - 1] == node && t.State == RailTrainState.Llegado;
            if (t.Route[t.Leg] == node && t.OffsetMm <= Stations.P.StopRadiusMm) return true;
            return t.Route[t.Leg + 1] == node && e.LengthMm - t.OffsetMm <= Stations.P.StopRadiusMm;
        }

        /// <summary>
        /// Prueba y aplica «Fundar»: un tren parado en el emplazamiento con 30 u de materiales y 12 personas a bordo, sin horda encima (densidad
        /// menor que la de asedio). Descarga los materiales (se gastan en el apeadero) y las personas pasan a la población del nodo.
        /// Si hay varios trenes, el de índice menor que reúna la carga. Devuelve el resultado (la interfaz explica por qué no).
        /// </summary>
        public FoundResult FoundStation(int node)
        {
            if (node < 0 || node >= Graph.Nodes.Count || !Stations.IsSite(node)) return FoundResult.NoEsEmplazamiento;
            if (Stations.Founded[node]) return FoundResult.YaFundada;
            bool anyTrain = false; TrainService pick = null;
            foreach (var sv in Services)
            {
                if (!TrainAtNode(sv, node)) continue;
                anyTrain = true;
                if (sv.Cargo[(int)Res.Materiales] >= Stations.P.FoundMaterialsU && sv.Cargo[(int)Res.Personas] >= Stations.P.FoundPeople) { pick = sv; break; }
            }
            if (!anyTrain) return FoundResult.SinTren;
            if (pick == null) return FoundResult.SinCarga;
            if (Graph.Nodes[node].HordeDensity >= SiegeAt) return FoundResult.HordeEncima;
            pick.Cargo[(int)Res.Materiales] -= Stations.P.FoundMaterialsU;
            pick.Cargo[(int)Res.Personas] -= Stations.P.FoundPeople;
            Stock[node, (int)Res.Personas] += Stations.P.FoundPeople;
            Stations.Founded[node] = true;
            return FoundResult.Ok;
        }

        /// <summary>Sin fundar, apeadero o estación (un depósito construido y 40 personas). Los demás nodos (ciudades, apeaderos existentes) no se tratan aquí.</summary>
        public StationKind StationKindOf(int node)
        {
            if (node < 0 || node >= Graph.Nodes.Count || !Stations.Founded[node]) return StationKind.NoFundada;
            bool deposit = false;
            var l = Works.Snapshot(node);
            foreach (var p in l) if (p.Kind == PieceKind.Deposito && p.Built) { deposit = true; break; }
            return deposit && Stock[node, (int)Res.Personas] >= Stations.P.StationPeople ? StationKind.Estacion : StationKind.Apeadero;
        }

        /// <summary>Cancela una obra: recupera el 100 % si no ha empezado y el 50 % si sí (D-N44).</summary>
        public void CancelWork(int node, int id)
        {
            if (node < 0 || node >= Graph.Nodes.Count) return;
            Stock[node, (int)Res.Materiales] += Works.Cancel(node, id);
        }

        /// <summary>
        /// La obra se pausa si el nodo está asediado o perdido, o tiene la horda encima (densidad de asedio) (Marcos, 6-nov). Sale del estado
        /// del nodo, que sale del campo: no añade estado al hash.
        /// </summary>
        public bool WorkPaused(int node)
        {
            var nd = Graph.Nodes[node];
            return nd.State == NodeState.Asediada || nd.State == NodeState.Perdida || nd.HordeDensity >= SiegeAt;
        }

        /// <summary>
        /// Obreros que trabajan de verdad en el nodo: los asignados, sin dejar nunca la población por debajo de la guarnición mínima (12) y
        /// ninguno mientras la obra esté pausada.
        /// </summary>
        public int EffectiveWorkers(int node)
        {
            if (!Works.P.IsWorkTime(Tick % NetConst.DayTicks)) return 0;     // de noche se para toda la obra (Hugo, D-N59: 06:00 a 18:00)
            return AssignedWorkers(node);
        }

        /// <summary>Obreros que quedan asignados a la obra sin tener en cuenta el horario: sin bajar de la guarnición mínima y ninguno si la horda la tiene parada.</summary>
        int AssignedWorkers(int node)
        {
            if (WorkPaused(node)) return 0;
            return Math.Max(0, Math.Min(Works.Workers[node], Stock[node, (int)Res.Personas] - Works.P.GarrisonMin));
        }

        /// <summary>
        /// Obra EN CURSO = encargada y sin acabar, con gente asignada, también de noche (Hugo, D-N59 adenda 2). Es lo que pesa en el asedio (×1,5) y suma huella:
        /// los objetivos del asedio son persistentes, no cambian cada 12 h y encargar las obras al anochecer no borra la presión.
        /// </summary>
        public bool WorkInProgress(int node) { return Works.IsActive(node) && AssignedWorkers(node) > 0; }

        /// <summary>Obra TRABAJANDO = en curso y dentro del horario (06:00–18:00). Es lo que hace ruido (sondeo) y avanza.</summary>
        public bool WorkActive(int node) { return WorkInProgress(node) && EffectiveWorkers(node) > 0; }

        /// <summary>Un tick de obra en cada nodo con obreros efectivos.</summary>
        /// <summary>Cada día, cada estación con obras paga el mantenimiento (MaintenanceU) de su almacén; si no puede, lo construido se desgasta (Hugo).</summary>
        // ------------------------------------------------------------------ víveres (D-N176, Lucía; reglas de sim_economia.py, líneas 484-491)
        /// <summary>Consumo de víveres: 0,10 u por persona y día (milésimas: 100). Lo que no llega a 1 u se queda en FoodRemMil (por nodo, en el hash y en el guardado).</summary>
        public int FoodPerPersonMil = 100;
        /// <summary>Si falta, el nodo pierde FoodLossPermil ‰ de su gente por la fracción no cubierta (50 = el 5 % al día sin nada).</summary>
        public int FoodLossPermil = 50;
        public int[] FoodRemMil;
        /// <summary>Hambre acumulada que aún no llega a una persona (milésimas, por nodo; Hugo): así un nodo pequeño con falta parcial también pierde gente, de media lo del modelo.</summary>
        public int[] StarveRemMil;
        /// <summary>Personas perdidas por hambre (para QA y el informe). Va en el guardado, no en el hash (sale de las existencias).</summary>
        public long StarvedTotal;

        /// <summary>Una vez al día, antes del mantenimiento: cada nodo con gente come; si no tiene para el día, se queda en 0 y pierde gente (pob ×= 1 − 0,05 × falta).</summary>
        /// <summary>D-N183 (Hugo, existencias_red.json «no_consumen_en_mvp»): un nodo cuya gente no come en el MVP (Castellón). −1 = ninguno. Lo pone NetScenario.Inicio; no se guarda.</summary>
        public int FoodExemptNode = -1;
        void ConsumeFood()
        {
            for (int n = 0; n < Graph.Nodes.Count; n++)
            {
                int pob = Stock[n, (int)Res.Personas];
                if (pob <= 0 || n == FoodExemptNode) continue;
                long acc = FoodRemMil[n] + (long)pob * FoodPerPersonMil;
                int need = (int)(acc / 1000); FoodRemMil[n] = (int)(acc % 1000);
                if (need <= 0) continue;
                int have = Math.Max(0, Stock[n, (int)Res.Viveres]);
                if (have >= need) { Stock[n, (int)Res.Viveres] = have - need; continue; }
                Stock[n, (int)Res.Viveres] = 0;
                long hunger = StarveRemMil[n] + (long)pob * FoodLossPermil * (need - have) * 1000 / (1000L * need);     // falta = (need − have) / need; en milésimas de persona (Hugo)
                int lost = (int)Math.Min(pob, hunger / 1000); StarveRemMil[n] = (int)(hunger % 1000);
                if (lost > 0) { Stock[n, (int)Res.Personas] = pob - lost; StarvedTotal += lost; }
            }
        }

        void WearAndMaintain()
        {
            for (int n = 0; n < Works.Nodes; n++)
            {
                int need = Works.AccrueMaintenance(n);        // el 1 % exacto con remanente (adenda de D-N63): hay que poder cubrir ceil(remanente); se cobra la parte entera
                bool paid = Stock[n, (int)Res.Materiales] >= need;
                Stock[n, (int)Res.Materiales] -= Works.SettleMaintenance(n, paid);
                Works.WearDay(n, paid);          // el depósito no se gasta y lo que está fuera del perímetro sí, pague o no la estación (Hugo)
            }
        }

        // ------------------------------------------------------------------ reparar un tramo (hito 5, paso 4)
        /// <summary>Deja un tramo en escombros (un corte de emboscada, o el puente volado del Acto III con bridge = true: 80 u y 192 obrero·h).</summary>
        public void DestroyEdge(int edge, bool bridge = false)
        {
            var e = Graph.Edges[edge];
            e.Debris = true; e.State = EdgeState.Cortada; e.RepairSvc = -1; e.RepairDone = 0;
            e.RepairU = bridge ? Works.P.BridgeU : 0;
            e.RepairNeed = bridge ? Works.P.BridgeNeedTicks : 0;
        }

        int EdgeRepairU(RailEdge e) { return e.RepairU > 0 ? e.RepairU : Works.P.RepairU; }
        long EdgeRepairNeed(RailEdge e) { return e.RepairNeed > 0 ? e.RepairNeed : Works.P.RepairNeedTicks; }

        /// <summary>El tren de obras está donde puede trabajar: parado antes del corte, dentro del tramo o en uno de sus extremos.</summary>
        bool RepairInPosition(TrainService sv, RailEdge e)
        {
            if (sv.Train.State == RailTrainState.Detenido && sv.Train.CurrentEdge(Graph) == e) return true;
            return TrainAtNode(sv, e.A) || TrainAtNode(sv, e.B);
        }

        /// <summary>La obra se para si la horda está sobre el tramo (HordeAtMm ≥ 0) o encima de uno de sus nodos (densidad de asedio).</summary>
        bool RepairBlocked(RailEdge e)
        {
            return e.HordeAtMm >= 0 || Graph.Nodes[e.A].HordeDensity >= SiegeAt || Graph.Nodes[e.B].HordeDensity >= SiegeAt;
        }

        /// <summary>
        /// Orden «reparar»: un tren de obras en posición, con los materiales (5 u; el puente, 80) y los obreros (8) a bordo, sin horda encima.
        /// Gasta los materiales de la carga y retiene el tren mientras dura la obra.
        /// </summary>
        public RepairResult RepairEdge(int edge, int service)
        {
            if (edge < 0 || edge >= Graph.Edges.Count || !Graph.Edges[edge].Debris) return RepairResult.SinEscombros;
            var e = Graph.Edges[edge];
            if (e.RepairSvc >= 0) return RepairResult.YaEnObra;
            if (service < 0 || service >= Services.Count || !Services[service].WorksTrain) return RepairResult.SinTrenDeObras;
            var sv = Services[service];
            if (!RepairInPosition(sv, e)) return RepairResult.TrenLejos;
            if (sv.Cargo[(int)Res.Materiales] < EdgeRepairU(e) || sv.Cargo[(int)Res.Personas] < Works.P.WorksCrew) return RepairResult.SinCarga;
            if (RepairBlocked(e)) return RepairResult.HordeEncima;
            sv.Cargo[(int)Res.Materiales] -= EdgeRepairU(e);
            e.RepairU = EdgeRepairU(e); e.RepairNeed = Taller.ChispasAt == service ? Taller.Faster(EdgeRepairNeed(e)) : EdgeRepairNeed(e);       // lo pagado y lo pedido quedan fijos (Chispas: un 25 % más rápido)
            e.RepairSvc = service; e.RepairDone = 0;
            sv.Held = true;
            return RepairResult.Ok;
        }

        /// <summary>
        /// Cada tick, cada reparación en curso avanza con los obreros que quedan a bordo (como mucho 8) mientras el tren siga en posición y
        /// no haya horda encima. Al terminar, el tramo vuelve a existir y el tren se libera.
        /// </summary>
        void AdvanceRepairs()
        {
            foreach (var e in Graph.Edges)
            {
                if (!e.Debris || e.RepairSvc < 0) continue;
                var sv = Services[e.RepairSvc];
                if (!Works.P.IsWorkTime(Tick % NetConst.DayTicks) || !RepairInPosition(sv, e) || RepairBlocked(e)) continue;     // también de 06:00 a 18:00
                int crew = Math.Min(sv.Cargo[(int)Res.Personas], Works.P.WorksCrew);
                if (crew <= 0) continue;
                e.RepairDone += crew;
                if (e.RepairDone < e.RepairNeed) continue;
                e.Debris = false; e.State = EdgeState.Ok; e.RepairSvc = -1; e.RepairDone = 0; e.RepairNeed = 0; e.RepairU = 0;
                sv.Held = false;
            }
        }

        void AdvanceWorks()
        {
            for (int n = 0; n < Works.Workers.Length; n++)
            {
                int w = EffectiveWorkers(n);
                if (w > 0) Works.Advance(n, w);
                // avisos de obra (textos de Lucía, §7 de textos-informe-colmena.md)
                if (!Works.FirstNoted && w > 0) { Works.FirstNoted = true; Intel.Add(new IntelEntry { Tick = Tick, Id = "OC-1", Vars = new[] { "nodo", Graph.Nodes[n].Name } }); }
                bool stopped = Works.Workers[n] > 0 && Works.IsActive(n) && WorkPaused(n);          // la horda para la obra
                if (stopped && !Works.WasPaused[n]) Intel.Add(new IntelEntry { Tick = Tick, Id = "OC-4", Vars = new[] { "nodo", Graph.Nodes[n].Name } });
                Works.WasPaused[n] = stopped;
            }
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
            AdvanceWorks();
            AdvanceRepairs();
            AdvanceTaller();
            AmbushStep();                    // emboscada del segundo tren: D-N180 fuera de pantalla
            if (ActiveZoneNode >= 0 && ActiveZoneNode < Director.AttentionTicks.Length) { Director.AttentionTicks[ActiveZoneNode]++; Director.LastSeenTick[ActiveZoneNode] = Tick; }   // memoria de atención
            if (Encounter.Active && Tick >= Encounter.ExpireTick) ResolveAmbush(Encounter.Service, Encounter.Taken, true);   // nadie respondió: la horda se dispersa y el tren pierde carga
            if (DirectorEnabled && Tick % NetConst.HourTicks == 0) Director.Hourly(this);       // una vez por hora de juego
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
            if (Tick % ProductionState.HourTicks == 0) ProduceHour(); // producción de la hora (1/24 del día), hasta el tope del almacén (Produccion.cs)
            if (Tick % NetConst.DayTicks == 0) { ConsumeFood(); WearAndMaintain(); }     // D-N176: primero los víveres (Hugo), luego el mantenimiento     // mantenimiento diario de lo construido (Hugo, 1 %)
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
            if (sv.LastAmbushDay == Tick / NetConst.DayTicks) return;      // como mucho una emboscada por tren y día (D-N39)
            int cell = -1;
            foreach (var pl in Director.Active) if (pl.Kind == HiveDirector.PlanKind.Emboscada && pl.Edge == ce.Id) { cell = pl.Cell; break; }
            if (cell < 0) return;
            int taken = Field.D[cell];
            Field.D[cell] = 0;
            LostThisTick += 0;                                             // la masa no se pierde: vuelve al resolverse
            sv.LastAmbushDay = Tick / NetConst.DayTicks;
            Encounter = new AmbushEncounter { Active = true, Service = Services.IndexOf(sv), Edge = ce.Id, Cell = cell, Taken = taken, ExpireTick = Tick + AmbushWaitTicks };
            sv.Ambushed = true; sv.Train.Stop();
            Field.Frozen = Field.Frozen >= 0 ? Field.Frozen : cell;        // la celda del encuentro no recibe masa mientras dura
        }

        /// <summary>Resuelve la emboscada: los supervivientes vuelven al campo, las bajas se pierden y el tren sigue.</summary>
        void ResolveAmbush(int service, int survivorsPermil, bool expired, int ammoUsedUnits = 0)
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
            if (ammoUsedUnits > 0) sv.Cargo[(int)Res.Municion] = Math.Max(0, sv.Cargo[(int)Res.Municion] - ammoUsedUnits);   // la zona gastó munición del tren
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
            if (s.Destroyed) return;
            if (s.GateNode >= 0 && !TryEnterGate(s)) return;          // D-N152: espera a las puertas (ni sale ni sigue)
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
            if (s.HaltAt >= 0 && t.Leg != legBefore && t.State == RailTrainState.EnMarcha && t.Leg < t.Route.Count && t.Route[t.Leg] == s.HaltAt)
            {
                t.OffsetMm = 0; t.Stop(); s.Held = true; s.HaltAt = -1;        // para justo en el nodo (la marcha sobrante del tick se pierde); TrainGo lo suelta
            }
            else if (GatesEnabled && t.Leg != legBefore && t.State == RailTrainState.EnMarcha && t.Leg < t.Route.Count && Besieged(t.Route[t.Leg]))
            {
                t.OffsetMm = 0; t.Stop(); s.GateNode = t.Route[t.Leg]; s.GateSinceTick = Tick; s.GateHorde = SiegeZombies(s.GateNode);          // D-N152: estación de paso sitiada: se queda a sus puertas hasta que acabe el asedio
            }
            if (t.State == RailTrainState.Llegado)
            {
                int node = s.Stops[s.StopIndex].Node;
                if (s.HaltAt >= 0 && node == s.HaltAt) s.HaltAt = -1;     // llegó a su parada: ya está en el nodo
                MakeNoise(node);
                s.AtStop = true;
                if (GatesEnabled && (Besieged(node) || FreeSlots(node, s) <= 0)) { s.GateNode = node; s.GateSinceTick = Tick; s.GateHorde = Besieged(node) ? SiegeZombies(node) : 0; }   // D-N152: sitiada o sin andén libre: a las puertas
                TryAmbush(s, node);
            }
        }

        // ---------------------------------------------------------------- Partida de 30 min (Lucía, 9-nov 08:00): objetivo del día 2 y emboscada a la llegada a Nules
        /// <summary>D-N179: nodo del objetivo del día 2 («Defiende Nules antes de la noche: manda tropa y munición (≥ 20 u)»). −1 = sin objetivo. Lo pone NetScenario.Inicio; no se guarda.</summary>
        public int ObjNode = -1;
        public const int ObjAmmoU = 20;
        /// <summary>El objetivo se ve desde el amanecer del día 2 (06:00 tras la 1.ª noche) hasta que empieza la noche de ese día (22:00), si el nodo no se ha perdido.</summary>
        public bool ObjActive { get { return ObjNode >= 0 && Tick >= NetConst.DayTicks + NetConst.Hours(6) && Tick < NetConst.DayTicks + NetConst.Hours(22) && !NodeLost[ObjNode]; } }
        /// <summary>Tropa: al menos un pelotón bajado en el nodo (embarque, D-N166). La guarnición de partida no cuenta: el objetivo es mandar tropa.</summary>
        public bool ObjTroops { get { return ObjNode >= 0 && Embarque.AtStation(ObjNode, PasajeroTipo.Peloton).Count > 0; } }
        /// <summary>Munición: al menos ObjAmmoU u en el almacén del nodo.</summary>
        public bool ObjAmmo { get { return ObjNode >= 0 && Stock[ObjNode, (int)Res.Municion] >= ObjAmmoU; } }
        public bool ObjMet { get { return ObjTroops && ObjAmmo; } }

        /// <summary>Emboscada del segundo tren (plan de 30 min; Lucía, 9-nov 08:10 y e5f2f1c): cuando llega a AmbushNode el SEGUNDO tren de la partida (cuenta desde el principio)
        /// y es el día 2 (de 06:00 a 22:00), un sondeo de la Colmena de AmbushZombies (300) le cae encima EN LA VÍA, antes del recinto: el tren se queda esperando fuera de la
        /// puerta oeste (GateNode, el sitio de la D-N152; en la zona de Nules, X 20–84 m, con la cola en la franja oscura del borde) y los 300 salen de la franja (X 0–20 m)
        /// a por él. La guarnición queda lejos y no cuenta. Una sola vez por partida: queda la entrada EM-1 del informe (va en el guardado), así que al cargar no se repite.
        /// La red no mueve masa. En pantalla manda la zona (LastAmbush le dice que cree los 300; al acabar llama a AmbushEnd). Fuera de pantalla, ResolveAmbushOffscreen.
        /// −1 = sin emboscada. Lo pone NetScenario.Inicio; no se guarda (tampoco AmbushArrivals: al cargar a media partida se vuelve a contar desde 0).</summary>
        public int AmbushNode = -1, AmbushZombies = 300, AmbushArrivals;
        public struct AmbushEvent { public int Node, Service, Zombies, AmmoU; public long Tick, LossTick, HoldUntil; public bool OnScreen, InApartado, Repelled, Surrounded, TrainLost; }
        /// <summary>La última emboscada (Tick = −1: ninguna en esta sesión). El tren queda a las puertas hasta HoldUntil (o hasta AmbushEnd). Fuera de pantalla: Repelled (con escolta:
        /// se gastan AmmoU u de la carga) o Surrounded (sin escolta: D-N180, se pierde en LossTick). InApartado queda siempre en false (la emboscada es en la vía). No entra en el hash.</summary>
        public AmbushEvent LastAmbush = new AmbushEvent { Tick = -1, Node = -1, Service = -1, LossTick = -1, HoldUntil = -1 };
        public bool AmbushDone { get { foreach (var e in Intel) if (e.Id == "EM-1") return true; return false; } }
        /// <summary>D-N180 en la red: un tren rodeado sin escolta se pierde con su carga en 10 minutos de juego como mucho.</summary>
        public static readonly long SurroundLossTicks = NetConst.HourTicks / 6;
        /// <summary>Punto de la emboscada en la zona de Nules (mm de la zona; medida de Nacho en NightSim, 9-nov 08:50): tren con la cola en X 20 m y el morro en X 84 m, en la vía
        /// (Z 47,4–50,6), y los 300 saliendo de la franja X 0–20 m, Z 30–70 m, en 30 s.</summary>
        public const int AmbushTrainX0Mm = 20_000, AmbushTrainX1Mm = 84_000, AmbushSpawnX0Mm = 0, AmbushSpawnX1Mm = 20_000, AmbushSpawnZ0Mm = 30_000, AmbushSpawnZ1Mm = 70_000, AmbushArrivalS = 30;

        void TryAmbush(TrainService s, int node)
        {
            if (AmbushNode < 0 || node != AmbushNode || AmbushZombies <= 0) return;
            AmbushArrivals++;
            if (AmbushArrivals < 2 || Tick < NetConst.DayTicks + NetConst.Hours(6) || Tick >= NetConst.DayTicks + NetConst.Hours(22) || NodeLost[node] || AmbushDone) return;
            int si = Services.IndexOf(s);
            s.GateNode = node; s.GateSinceTick = Tick;                    // se queda en la vía, fuera de la puerta oeste
            LastAmbush = new AmbushEvent { Node = node, Service = si, Zombies = AmbushZombies, Tick = Tick, LossTick = -1, HoldUntil = Tick + SurroundLossTicks, OnScreen = node == ActiveZoneNode };
            int n100 = Math.Max(100, (AmbushZombies + 50) / 100 * 100);
            Intel.Add(new IntelEntry { Tick = Tick, Id = "EM-1", Vars = new[] { "nodo", Graph.Nodes[node].Name, "tren", "Tren " + s.Train.Name, "n", n100.ToString() } });   // «Emboscada en {nodo}: unos {n} zombis atacan el {tren}.»
            if (!LastAmbush.OnScreen) ResolveAmbushOffscreen(s, si, node);
        }

        /// <summary>¿Retiene la emboscada a este tren a las puertas?</summary>
        bool AmbushHolds(TrainService s) { return LastAmbush.Tick >= 0 && LastAmbush.HoldUntil > Tick && s.GateNode == LastAmbush.Node && LastAmbush.Service >= 0 && LastAmbush.Service < Services.Count && Services[LastAmbush.Service] == s; }

        /// <summary>La zona avisa de que la emboscada ha acabado (los 300 abatidos, o el tren perdido en la zona). Suelta el tren o lo da por perdido con su carga.</summary>
        public void AmbushEnd(bool trainLost)
        {
            if (LastAmbush.Tick < 0) return;
            LastAmbush.HoldUntil = Tick; LastAmbush.LossTick = -1;
            if (trainLost && LastAmbush.Service >= 0 && LastAmbush.Service < Services.Count) LoseAmbushedTrain(LastAmbush.Service);
        }

        /// <summary>Fuera de pantalla, sin reglas nuevas (Lucía, 08:10). Con escolta (un pelotón o un vehículo a bordo) se rechaza: se gasta techo(zombis ÷ eficacia de la zona) u de
        /// munición de la carga del tren, nadie tiene bajas y el tren entra. Sin escolta, el tren queda rodeado (la guarnición está lejos) y se pierde con su carga en 10 min de juego
        /// (D-N180) salvo que el jugador entre en la zona (entonces manda la zona). En NightSim, con 300: sin escolta se pierde el 87 %, con un pelotón el 0 %.</summary>
        void ResolveAmbushOffscreen(TrainService s, int si, int node)
        {
            if (EscortSoldiers(si) > 0)
            {
                int zpu = ZoneZombiesPerUNode != null && node < ZoneZombiesPerUNode.Length && ZoneZombiesPerUNode[node] > 0 ? ZoneZombiesPerUNode[node] : ZoneZombiesPerU;
                int need = (int)((AmbushZombies + zpu - 1) / Math.Max(1, zpu)), m = (int)Res.Municion;
                int got = Math.Min(need, Math.Max(0, s.Cargo[m])); s.Cargo[m] -= got;
                LastAmbush.Repelled = true; LastAmbush.AmmoU = got; LastAmbush.HoldUntil = Tick;
            }
            else { LastAmbush.Surrounded = true; LastAmbush.LossTick = Tick + SurroundLossTicks; LastAmbush.HoldUntil = LastAmbush.LossTick + 1; }      // retenido hasta que AmbushStep lo resuelva
        }

        void LoseAmbushedTrain(int si)
        {
            var s = Services[si]; if (s.Destroyed) return;
            s.Destroyed = true; s.Hp = 0; s.Held = true; s.GateNode = -1; s.GateSinceTick = -1;
            Array.Clear(s.Cargo, 0, s.Cargo.Length); s.Train.CargoTonnes = 0;
            for (int i = Embarque.Lista.Count - 1; i >= 0; i--) if (Embarque.Lista[i].Service == si && Embarque.Lista[i].Estado == PasajeroEstado.ABordo) Embarque.Lista.RemoveAt(i);
            LastAmbush.TrainLost = true;
        }

        void AmbushStep()
        {
            if (LastAmbush.LossTick < 0 || Tick < LastAmbush.LossTick) return;
            int si = LastAmbush.Service, node = LastAmbush.Node; LastAmbush.LossTick = -1;
            if (si < 0 || si >= Services.Count || node == ActiveZoneNode) return;          // el jugador ha entrado: manda la zona
            var s = Services[si];
            if (s.Destroyed || s.GateNode != node || EscortSoldiers(si) > 0) return;     // ya no está allí (o le han subido tropa)
            LoseAmbushedTrain(si);
        }

        // ---------------------------------------------------------------- D-N152: el tren espera a las puertas
        /// <summary>D-N152 (Eugenio; Lucía, 7-nov): si la estación está sitiada, el tren que llega o pasa por ella se queda a las puertas, fuera del muro, hasta
        /// que acabe el asedio. En su parada, además, entra solo si queda andén libre (plazas: 2 en las principales y 1 en las demás, propuesta de Lucía;
        /// ampliar la estación da más, D-N153). A las puertas no sale ni sigue, no carga ni descarga mercancía y solo baja la tropa (D-N166).
        /// false lo apaga del todo (como antes de la D-N152).</summary>
        public bool GatesEnabled = true;

        /// <summary>Plazas de tren (andenes) del MVP (Lucía, 8-nov): Sagunto 2 y Nules 1. Bétera no es nodo de la red. Las demás estaciones no tienen zona en el MVP
        /// y no limitan (0 = sin límite): un tren a Castellón o a Valencia entra siempre que no estén sitiadas.</summary>
        public static readonly string[] SlotNames = { "Sagunto", "Nules" };
        public static readonly int[] SlotCounts = { 2, 1 };

        /// <summary>Plazas de tren de la estación (0 = sin límite). Sale de la topología, no del estado.</summary>
        public int TrainSlots(int node)
        {
            if (node < 0 || node >= Graph.Nodes.Count) return 0;
            int i = Array.IndexOf(SlotNames, Graph.Nodes[node].Name);
            return i >= 0 ? SlotCounts[i] : 0;
        }

        /// <summary>Andenes libres en el nodo sin contar al tren «self»: plazas menos los trenes que están dentro (parados en el nodo y no a las puertas).</summary>
        public int FreeSlots(int node, TrainService self = null)
        {
            int inside = 0;
            int slots = TrainSlots(node);
            if (slots <= 0) return int.MaxValue;                          // sin límite
            foreach (var sv in Services) if (sv != self && !sv.Destroyed && sv.GateNode != node && TrainAtNode(sv, node)) inside++;
            return slots - inside;
        }

        /// <summary>¿Espera a las puertas de ese nodo? (Para el panel y la zona: la tropa de un tren a las puertas baja fuera del muro, D-N166.)</summary>
        public bool AtGate(TrainService sv, int node) { return sv != null && sv.GateNode >= 0 && sv.GateNode == node; }

        // ---------------------------------------------------------------- D-N177 (provisional): el tren a las puertas, fuera de pantalla
        /// <summary>D-N177 (Lucía, 8-nov; cifras de Hugo): al acabar el asedio con el tren a las puertas, y si la zona de ese nodo NO está en pantalla (ahí manda NightSim),
        /// se resuelve una vez: el GateAttackPermil (100 ‰, la única cifra inventada) de la horda va a por el tren; la escolta abate GateKillsPerSoldier (54) por soldado
        /// (un vehículo en plataforma cuenta como GateVehicleSoldiers, 8); cada zombi en contacto quita GateHpPerZombie (1.200) de vida al tren. Si cae, se pierde todo:
        /// la carga y lo que va a bordo. La escolta pierde 14 milésimas de persona por zombi en contacto (con remanente) y gasta 1 u por cada 400 abatidos: eso lo aplica
        /// quien lleve los pelotones (LastGateFight lo deja apuntado).</summary>
        public int GateAttackPermil = 100, GateKillsPerSoldier = 54, GateVehicleSoldiers = 8, GateHpPerZombie = 1_200, GateEscortLossMil = 14, GateKillsPerAmmoU = 400, GateSoldiersPerPlatoon = 8, TrainRepairHpPerU = 5_000;

        public struct GateFight { public int Service, Node, Horde, Attackers, Soldiers, Killed, Contact, HpLost, EscortLostMil, AmmoU; public bool Destroyed; public long Tick; }
        public GateFight LastGateFight;
        public int GateFights;                                            // cuántos combates a las puertas se han resuelto (informativo)

        /// <summary>Zombis del asedio en el nodo (para el combate a las puertas): la masa que el director ha llevado hasta allí, menos la que ha perdido, en zombis.</summary>
        public int SiegeZombies(int node)
        {
            long m = 0;
            foreach (var pl in Director.Active) if (pl.Kind == HiveDirector.PlanKind.Asedio && pl.Node == node) m += Math.Max(0, pl.Moved - pl.Lost);
            if (m == 0) m = Graph.Nodes[node].HordeDensity;              // sin plan (densidad de asedio): lo que haya encima
            return (int)Math.Min(int.MaxValue, m * ZombiesPerPermil);
        }

        /// <summary>Soldados de escolta a bordo: GateSoldiersPerPlatoon por pelotón y GateVehicleSoldiers por vehículo.</summary>
        public int EscortSoldiers(int service)
        {
            int n = 0;
            foreach (var p in Embarque.Lista) if (p.Service == service && p.Estado == PasajeroEstado.ABordo) n += p.Tipo == PasajeroTipo.Peloton ? GateSoldiersPerPlatoon : GateVehicleSoldiers;
            return n;
        }

        /// <summary>El cálculo, sin tocar nada (para el panel y las pruebas).</summary>
        public GateFight GateFightFor(int horde, int soldiers, int hp)
        {
            var f = new GateFight { Horde = horde, Soldiers = soldiers };
            f.Attackers = (int)((long)horde * GateAttackPermil / 1000);
            f.Killed = (int)Math.Min(f.Attackers, (long)soldiers * GateKillsPerSoldier);
            f.Contact = f.Attackers - f.Killed;
            f.HpLost = (int)Math.Min(hp, (long)f.Contact * GateHpPerZombie);
            f.Destroyed = (long)f.Contact * GateHpPerZombie >= hp;
            f.EscortLostMil = f.Contact * GateEscortLossMil;
            f.AmmoU = f.Killed / GateKillsPerAmmoU;
            return f;
        }

        void ResolveGateFight(TrainService s, int node)
        {
            int horde = s.GateHorde; s.GateHorde = 0;
            if (node == ActiveZoneNode) return;                          // en pantalla manda NightSim (D-N177)
            int idx = Services.IndexOf(s);
            var f = GateFightFor(horde, EscortSoldiers(idx), s.Hp);
            f.Service = idx; f.Node = node; f.Tick = Tick;
            s.Hp -= f.HpLost;
            if (f.Destroyed)
            {
                s.Destroyed = true; s.Hp = 0; s.Held = true; s.GateNode = -1; s.GateSinceTick = -1;
                Array.Clear(s.Cargo, 0, s.Cargo.Length); s.Train.CargoTonnes = 0;
                for (int i = Embarque.Lista.Count - 1; i >= 0; i--) if (Embarque.Lista[i].Service == idx && Embarque.Lista[i].Estado == PasajeroEstado.ABordo) Embarque.Lista.RemoveAt(i);   // todo o nada
            }
            LastGateFight = f; GateFights++;
        }

        /// <summary>Unidades de materiales para dejar el tren a tope (1 u por 5.000 de vida, Hugo).</summary>
        public int TrainRepairU(int service) { if (service < 0 || service >= Services.Count) return 0; int d = NetConst.TrainHpMax - Services[service].Hp; return d <= 0 ? 0 : (d + TrainRepairHpPerU - 1) / TrainRepairHpPerU; }

        /// <summary>D-N177: aviso ámbar «Sin escolta: &lt;estación&gt; está sitiada» (al dar la salida y en el icono del tren): sin tropa ni vehículos a bordo y con una estación sitiada
        /// en su camino hasta la próxima parada (incluida). null si no hay aviso.</summary>
        public string EscortWarning(int service)
        {
            if (service < 0 || service >= Services.Count) return null;
            var s = Services[service]; if (s.Destroyed || s.Stops.Count < 2 || EscortSoldiers(service) > 0) return null;
            List<int> route;
            if (s.AtStop) { int next = (s.StopIndex + 1) % s.Stops.Count; route = Graph.ShortestPath(s.Stops[s.StopIndex].Node, s.Stops[next].Node); }
            else route = s.Train.Route;
            if (route == null) return null;
            int from = s.AtStop ? 1 : Math.Max(1, s.Train.Leg);
            for (int i = from; i < route.Count; i++) if (Besieged(route[i])) return "Sin escolta: " + Graph.Nodes[route[i]].Name + " está sitiada";
            if (s.GateNode >= 0 && Besieged(s.GateNode)) return "Sin escolta: " + Graph.Nodes[s.GateNode].Name + " está sitiada";
            return null;
        }

        // ---------------------------------------------------------------- aviso ámbar: la horda va a una zona sin defensa (Lucía, 8-nov; con Iker, en Inicio)
        // ---------------------------------------------------------------- horda de zona, fuera de pantalla (Lucía, 8-nov 23:20; regla abstracta S0 de Hugo)
        /// <summary>Nodos perdidos: la horda de zona tomó la estación (persistente; el campo no lo deshace). Guardado v5.</summary>
        public bool[] NodeLost = new bool[0];
        /// <summary>Guarnición abstracta de cada nodo, en milésimas de persona (Hugo: Nules 12). No es población (no trabaja ni come); la pone el escenario. Guardado v5.</summary>
        public int[] GarrisonMil = new int[0];
        /// <summary>Recinto abstracto, en 1/10.000 de u, para un nodo sin piezas de obra (Nules en Inicio: 124,3 u). Si el nodo tiene piezas, mandan ellas. Guardado v5.</summary>
        public int[] ZoneWallE4 = new int[0];
        /// <summary>Regla del modelo económico (Hugo, sim_economia.py 549-561), la misma con la que está medida la economía.</summary>
        public int[] ZoneZombiesPerUNode;          // por nodo: combate_z_por_u_<id> si existe, si no la general (null = la general en todos)
        public int ZoneFireCapPerDefender;          // combate_tope_fuego_por_defensor (Hugo, 9-nov): 0 = sin tope; no se aplica a casa (Sagunto)
        public int ZoneZombiesPerU = 400, ZoneWallBonusMil = 0, ZoneWallStepU = 30, ZoneWallMaxSteps = 5, ZoneWallLossMilPerZombie = 10, ZoneGarrisonLossMilPerZombie = 4, ZoneNoWallMult = 3, ZoneFallAlivePermil = 500;
        /// <summary>qa50 S2 (Lucía, 7705049): la estación se ha perdido en pantalla y el jugador pulsa «Seguir». Lo mismo que la caída fuera de pantalla (ResolveZoneSiege, res.Lost):
        /// NodeLost, existencias y guarnición a 0, el muro destruido; además se pierden la tropa y los vehículos bajados en esa estación (embarque). La horda se queda encima
        /// (el campo no se toca). Va por orden (CmdLoseNode) para que A y B lo apliquen en el mismo tick. Devuelve false si el nodo no vale o ya estaba perdido.</summary>
        public bool LoseNode(int node)
        {
            if (node < 0 || node >= Graph.Nodes.Count || NodeLost[node]) return false;
            NodeLost[node] = true; GarrisonMil[node] = 0;
            for (int r = 0; r < NetConst.ResCount; r++) Stock[node, r] = 0;
            ZoneWallE4[node] = 0; var left = Works.Snapshot(node); for (int i = left.Count - 1; i >= 0; i--) if (left[i].Built && (left[i].IsWall || left[i].IsGate)) Works.Damage(node, i, 1000);
            for (int i = Embarque.Lista.Count - 1; i >= 0; i--) if (Embarque.Lista[i].Estado == PasajeroEstado.EnEstacion && Embarque.Lista[i].Node == node) Embarque.Lista.RemoveAt(i);
            return true;
        }
        public static NetCommand CmdLoseNode(long tick, int node) { return new NetCommand { Tick = tick, Type = NetCmdType.LoseNode, Arg = node }; }

        public struct ZoneSiegeResult { public int Node; public long Zombies, Killed, SpentU, DamageU, Casualties; public bool Lost, OnScreen; public long Tick; }
        public ZoneSiegeResult LastZoneSiege;

        /// <summary>u de muro construido del nodo (piezas de muro y puertas, por lo pagado).</summary>
        public long WallU(int node) { long e4 = 0; foreach (var p in Works.Snapshot(node)) if (p.Built && (p.IsWall || p.IsGate)) e4 += (long)p.CostE4 * p.LifeMil / 1000; if (e4 == 0 && node >= 0 && node < ZoneWallE4.Length) e4 = ZoneWallE4[node]; return e4 / 10_000; }

        /// <summary>Resuelve la noche de una horda de zona (Hugo, sim_economia.py 549-561). En pantalla manda NightSim y aquí no se toca nada. Fuera:
        /// eficacia = 400 × (1 + 0,2 × min(5, muro/30)) zombis por u; vivos = horda − munición × eficacia. Por cada vivo, el muro pierde 0,010 u y la guarnición 0,004 personas
        /// (×3 si el muro ha llegado a 0). Cae si la guarnición baja de la mitad de la inicial, o si el muro llega a 0 con más del 50 % de la horda en pie: se pierden la gente, el muro
        /// y todas las existencias (NodeLost).</summary>
        public void ResolveZoneSiege(int node, long zombies)
        {
            if (node < 0 || node >= Graph.Nodes.Count || zombies <= 0) return;
            var res = new ZoneSiegeResult { Node = node, Zombies = zombies, Tick = Tick, OnScreen = node == ActiveZoneNode };
            if (res.OnScreen) { LastZoneSiege = res; return; }
            var pieces = Works.Snapshot(node); long wallE4 = 0;
            foreach (var p in pieces) if (p.Built && (p.IsWall || p.IsGate)) wallE4 += (long)p.CostE4 * p.LifeMil / 1000;
            bool abstractWall = wallE4 == 0; if (abstractWall) wallE4 = ZoneWallE4[node];
            long stepsMil = Math.Min((long)ZoneWallMaxSteps * 1000, wallE4 * 1000 / ((long)ZoneWallStepU * 10_000));
            int zpu = ZoneZombiesPerUNode != null && node < ZoneZombiesPerUNode.Length ? ZoneZombiesPerUNode[node] : ZoneZombiesPerU;
            long eff = Math.Max(1, (long)zpu * (1000 + ZoneWallBonusMil * stepsMil / 1000) / 1000);      // opción A: 400 zombis por u con o sin muro (Nules 300, fila propia)
            long ammo = Math.Max(0, Stock[node, (int)Res.Municion]);
            // Tope de fuego (Hugo, 9-nov, como los trepadores de NightSim): con más horda que defensores × tope, la estación cae aunque sobre munición; abaten como mucho ese tope.
            bool capOn = ZoneFireCapPerDefender > 0 && node != Embarque.HomeNode;
            long fireCap = capOn ? (long)ZoneDefenders(node) * ZoneFireCapPerDefender : long.MaxValue;
            bool overrun = capOn && zombies > fireCap;
            long alive = Math.Max(0, zombies - Math.Min(ammo * eff, fireCap));
            res.Killed = zombies - alive;
            res.SpentU = Math.Min(ammo, (res.Killed + eff - 1) / eff); Stock[node, (int)Res.Municion] -= (int)res.SpentU;      // en u enteras, por arriba (Hugo: 3,3 u con 20)
            long lossE4 = alive * ZoneWallLossMilPerZombie * 10;                                            // 0,010 u por vivo
            res.DamageU = Math.Min(wallE4, lossE4) / 10_000;
            bool wallGone = wallE4 <= 0 || lossE4 >= wallE4;
            if (abstractWall) ZoneWallE4[node] = (int)Math.Max(0, wallE4 - lossE4);
            else if (wallE4 > 0 && lossE4 > 0)
            {
                int lifeMil = (int)Math.Min(1000, (lossE4 * 1000 + wallE4 - 1) / wallE4);
                for (int i = pieces.Count - 1; i >= 0; i--) if (pieces[i].Built && (pieces[i].IsWall || pieces[i].IsGate)) Works.Damage(node, i, lifeMil);
            }
            int g0 = GarrisonMil[node];
            long gl = alive * ZoneGarrisonLossMilPerZombie * (wallGone ? ZoneNoWallMult : 1);
            GarrisonMil[node] = (int)Math.Max(0, g0 - gl);
            res.Casualties = (g0 - GarrisonMil[node]) / 1000;
            res.Lost = overrun || (g0 > 0 && GarrisonMil[node] * 2 < g0) || (g0 <= 0 && alive > 0) || (wallGone && alive * 1000 > zombies * ZoneFallAlivePermil);
            if (res.Lost)
            {
                NodeLost[node] = true; res.Casualties = g0 / 1000 + Math.Max(0, Stock[node, (int)Res.Personas]); GarrisonMil[node] = 0;
                for (int r = 0; r < NetConst.ResCount; r++) Stock[node, r] = 0;
                ZoneWallE4[node] = 0; var left = Works.Snapshot(node); for (int i = left.Count - 1; i >= 0; i--) if (left[i].Built && (left[i].IsWall || left[i].IsGate)) Works.Damage(node, i, 1000);
            }
            LastZoneSiege = res;
            Intel.Add(new IntelEntry { Tick = Tick, Id = res.Lost ? "ZS-2" : "ZS-1", Vars = new[] { "nodo", Graph.Nodes[node].Name, "n", zombies.ToString(), "abatidos", res.Killed.ToString(), "u", res.SpentU.ToString(), "bajas", res.Casualties.ToString() } });
        }

        /// <summary>Defensores para el tope de fuego: la guarnición abstracta más 8 por pelotón o vehículo bajado allí (GateSoldiersPerPlatoon, GateVehicleSoldiers).</summary>
        public int ZoneDefenders(int node)
        {
            int d = GarrisonMil[node] / 1000;
            foreach (var p in Embarque.Lista) if (p.Estado == PasajeroEstado.EnEstacion && p.Node == node) d += p.Tipo == PasajeroTipo.Peloton ? GateSoldiersPerPlatoon : GateVehicleSoldiers;
            return d;
        }

        public struct ZoneWarning { public int Node; public long EtaTicks; public bool Started; public string Text; }

        /// <summary>¿Tiene la zona tropa del jugador? Casa (Embarque.HomeNode, Sagunto) siempre; en las demás, algún pelotón o vehículo bajado allí (EnEstacion).
        /// La guarnición abstracta (la regla del asedio contra el recinto) no cuenta como defensa.</summary>
        public bool ZoneDefended(int node)
        {
            if (node < 0 || node >= Graph.Nodes.Count) return false;
            if (node == Embarque.HomeNode) return true;
            foreach (var p in Embarque.Lista) if (p.Estado == PasajeroEstado.EnEstacion && p.Node == node) return true;
            return false;
        }

        /// <summary>Avisos en ámbar: un asedio anunciado (o ya empezado) contra una zona sin tropa. Por nodo, el más próximo, ordenados por ETA y luego por nodo.
        /// Texto: «La horda va hacia Nules (llega hacia las 22:00) y no hay tropa». Solo lectura: no toca el hash.</summary>
        public List<ZoneWarning> UndefendedWarnings()
        {
            var l = new List<ZoneWarning>();
            foreach (var pl in Director.Active)
            {
                if (pl.Kind != HiveDirector.PlanKind.Asedio || pl.Node < 0 || pl.Node >= Graph.Nodes.Count || ZoneDefended(pl.Node)) continue;
                long eta = pl.Started ? 0 : Math.Max(0, pl.StartTick - Tick);
                int k = l.FindIndex(x => x.Node == pl.Node);
                if (k >= 0) { if (l[k].EtaTicks <= eta) continue; l.RemoveAt(k); }
                string nm = Graph.Nodes[pl.Node].Name;
                string txt = pl.Started ? "La horda ataca " + nm + " y no hay tropa" : "La horda va hacia " + nm + " (llega hacia las " + NetConst.Clock((Tick + eta) / NetConst.HourTicks * NetConst.HourTicks) + ") y no hay tropa";
                l.Add(new ZoneWarning { Node = pl.Node, EtaTicks = eta, Started = pl.Started, Text = txt });
            }
            l.Sort((a, b) => a.EtaTicks != b.EtaTicks ? a.EtaTicks.CompareTo(b.EtaTicks) : a.Node.CompareTo(b.Node));
            return l;
        }

        /// <summary>Trenes que esperan a las puertas del nodo, por orden de llegada (para la zona: el tren queda fuera del muro y se le puede atacar, D-N148).</summary>
        public List<int> TrainsAtGate(int node)
        {
            var l = new List<int>();
            for (int i = 0; i < Services.Count; i++) if (Services[i].GateNode == node && node >= 0) l.Add(i);
            l.Sort((a, b) => Services[a].GateSinceTick != Services[b].GateSinceTick ? Services[a].GateSinceTick.CompareTo(Services[b].GateSinceTick) : a.CompareTo(b));
            return l;
        }

        /// <summary>Por qué espera (texto para el panel), o null si no espera.</summary>
        public string GateReason(int service)
        {
            if (service < 0 || service >= Services.Count || Services[service].GateNode < 0) return null;
            int n = Services[service].GateNode;
            return Besieged(n) ? "Espera a las puertas de " + Graph.Nodes[n].Name + ": la estación está sitiada" : "Espera a las puertas de " + Graph.Nodes[n].Name + ": no queda andén libre";
        }

        /// <summary>Un tren a las puertas entra (o sigue) cuando acaba el asedio y, si es su parada, queda andén libre. true si ha entrado.</summary>
        bool TryEnterGate(TrainService s)
        {
            int n = s.GateNode;
            if (!GatesEnabled) { s.GateNode = -1; s.GateSinceTick = -1; if (!s.AtStop && s.Train.State == RailTrainState.Detenido) s.Train.Go(); return true; }
            if (Besieged(n) || AmbushHolds(s)) return false;              // también mientras dura la emboscada del segundo tren
            if (s.GateHorde > 0) { ResolveGateFight(s, n); if (s.Destroyed) return false; }
            if (s.AtStop)
            {
                if (FreeSlots(n, s) <= 0) return false;
                int me = Services.IndexOf(s);                                     // por orden de llegada: antes entra el que lleva más esperando a sus puertas (empate: el de índice menor)
                for (int i = 0; i < Services.Count; i++)
                {
                    var o = Services[i];
                    if (o == s || o.GateNode != n || !o.AtStop) continue;
                    if (o.GateSinceTick < s.GateSinceTick || (o.GateSinceTick == s.GateSinceTick && i < me)) return false;
                }
                s.GateNode = -1; s.GateSinceTick = -1; return true;
            }
            s.GateNode = -1; s.GateSinceTick = -1;
            if (!s.Held && !s.Ambushed) s.Train.Go();
            return true;
        }

        string EdgeName(RailEdge e) { return Graph.Nodes[e.A].Name + "–" + Graph.Nodes[e.B].Name; }

        /// <summary>Una línea de memoria de tráfico para el informe (§3): «{tramo}: el tren {n} pasa a la misma hora {k} días seguidos. Lo ha aprendido.»</summary>
        public struct IntelTrafficLine
        {
            public int Edge, Slot, Mem;     // tramo, franja de la memoria y su valor (la de más memoria va primero)
            public string Tramo;            // «Sagunto–Nules», como en TR-1
            public string Tren;             // «Tren 2 Montaña», como en TR-1; null si no se sabe (partida migrada o el tren ya no existe)
            public string Hora;             // la hora en punto del primer paso de la racha, «08:00»; sin racha, el principio de la franja
            public int Dias;                // días seguidos que ese tren ha pasado en esa franja (0 si no se sabe)
            public bool Sigue;              // pasó hoy o ayer: la racha sigue viva
        }

        /// <summary>Franjas que la Colmena ha aprendido (por encima del aviso y sin olvidar), la de más memoria primero. Solo lectura: no toca el hash.</summary>
        public List<IntelTrafficLine> IntelTraffic()
        {
            var r = new List<IntelTrafficLine>(); if (Traffic == null) return r;
            foreach (var l in Traffic.Learned(Tick))
            {
                string tren = null;
                if (l.TrainId >= 0) foreach (var s in Services) if (s.Train != null && s.Train.Id == l.TrainId) { tren = "Tren " + s.Train.Name; break; }
                var e = Graph.Edges[l.Edge];
                r.Add(new IntelTrafficLine { Edge = l.Edge, Slot = l.Slot, Mem = l.Mem, Tramo = EdgeName(e), Tren = tren, Hora = NetConst.Clock((l.Hour >= 0 ? l.Hour : l.Slot * 24 / Traffic.P.Slots) * (long)NetConst.HourTicks), Dias = tren != null ? l.Days : 0, Sigue = l.Alive });
            }
            return r;
        }

        /// <summary>
        /// Memoria de atención para el informe (§3): el nodo que el jugador mira hoy (Director.AttentionNode, −1 si ninguno) y los nodos suyos que lleva más de 6 h sin mirar
        /// (Director.Neglected, el de más Colmena alrededor primero). {zona} = olvidados[0] (Lucía, 9-nov 01:21). Solo lectura.
        /// </summary>
        public void IntelAttention(out int foco, out List<int> olvidados) { foco = Director.AttentionNode(this); olvidados = Director.Neglected(this); }

        /// <summary>Fuerzas propias de una estación para el informe (§4): pelotones, Dragón y VAMTAC que hay allí según la red.</summary>
        public struct IntelForces { public int Pelotones, Vamtac; public bool Dragon; }

        /// <summary>
        /// Lo que la red sabe que hay en la estación (§4). En casa (Embarque.HomeNode): el plantel de la D-N173 (Plantel.De) menos lo que está fuera (a bordo o bajado en otra
        /// estación). En las demás: lo bajado allí (EnEstacion). La red no sabe de bajas en la zona: un pelotón muerto en la noche sigue contando hasta que la zona lo diga.
        /// Solo lectura.
        /// </summary>
        public IntelForces IntelForcesAt(int node)
        {
            var f = new IntelForces();
            if (node < 0 || node >= Graph.Nodes.Count) return f;
            var coches = Plantel.Vehiculos(Embarque.HomeNode >= 0 ? Graph.Nodes[Embarque.HomeNode].Name : "");
            var here = new List<int>();
            if (node == Embarque.HomeNode)
            {
                f.Pelotones = Plantel.De(Graph.Nodes[node].Name).Pelotones.Length;
                foreach (var p in Embarque.Lista) if (p.Tipo == PasajeroTipo.Peloton) f.Pelotones--;
                for (int i = 0; i < coches.Count; i++) if (Embarque.Find(PasajeroTipo.Vehiculo, i) < 0) here.Add(i);
                if (f.Pelotones < 0) f.Pelotones = 0;
            }
            else { f.Pelotones = Embarque.AtStation(node, PasajeroTipo.Peloton).Count; here = Embarque.AtStation(node, PasajeroTipo.Vehiculo); }
            foreach (int i in here) { if (i < 0 || i >= coches.Count) continue; if (coches[i].Tipo == "dragon") f.Dragon = true; else f.Vamtac++; }
            return f;
        }

        /// <summary>Un tren entra en un tramo: suma a la memoria de tráfico; si la franja cruza el aviso, entrada TR-1 en el informe.</summary>
        void PassEdge(TrainService s, RailEdge e)
        {
            if (e == null || Traffic == null) return;
            if (Traffic.OnPass(e.Id, Tick, s.Train.Id))
            {
                string hora = NetConst.Clock(Tick / NetConst.HourTicks * NetConst.HourTicks);             // la hora en punto: «hacia las 08:00»
                Intel.Add(new IntelEntry { Tick = Tick, Id = "TR-1", Vars = new[] { "tren", "Tren " + s.Train.Name, "tramo", EdgeName(e), "hora", hora } });
            }
        }

        void MakeNoise(int node) { Attraction[node] += 70; }       // el tráfico atrae a la Colmena (semilla de la memoria, GDD §5.2)

        /// <summary>Orden TrainCargo (Iker, 8-nov): carga o descarga con el tren PARADO en la estación, sin salir. delta[r] &gt; 0 sube de la estación al tren
        /// (hasta la capacidad y lo que haya), &lt; 0 baja del tren a la estación (lo que lleve). Misma regla que Transfer al salir. Devuelve las u movidas, o −1 si el tren no está
        /// parado en ese nodo o está emboscado.</summary>
        public int MoveCargo(int service, int node, int[] delta)
        {
            if (service < 0 || service >= Services.Count || node < 0 || node >= Graph.Nodes.Count || delta == null || delta.Length != NetConst.ResCount) return -1;
            var s = Services[service]; if (s.Ambushed || s.Destroyed || !TrainAtNode(s, node) || s.GateNode == node) return -1;   // a las puertas no hay andén (D-N152)
            int moved = 0;
            for (int r = 0; r < NetConst.ResCount; r++)
            {
                int q = Math.Max(-MaxScheduleTransfer, Math.Min(MaxScheduleTransfer, delta[r]));
                if (q > 0) { int m = Math.Min(q, Math.Min(NetConst.TrainCapacity - s.CargoTotal(), Stock[node, r])); if (m <= 0) continue; Stock[node, r] -= m; s.Cargo[r] += m; moved += m; }
                else if (q < 0) { int m = Math.Min(-q, s.Cargo[r]); if (m <= 0) continue; s.Cargo[r] -= m; Stock[node, r] += m; moved += m; }
            }
            s.Train.CargoTonnes = s.CargoTotal();
            return moved;
        }
        void MoveCargo(int service, int[] payload) { if (payload == null || payload.Length != 1 + NetConst.ResCount) return; var d = new int[NetConst.ResCount]; Array.Copy(payload, 1, d, 0, d.Length); MoveCargo(service, payload[0], d); }
        public static NetCommand CmdCargo(long tick, int service, int node, int[] delta)
        {
            var pl = new int[1 + NetConst.ResCount]; pl[0] = node; if (delta != null) Array.Copy(delta, 0, pl, 1, Math.Min(delta.Length, NetConst.ResCount));
            return new NetCommand { Tick = tick, Type = NetCmdType.TrainCargo, Arg = service, Payload = pl };
        }

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
                if (n.Id >= 0 && n.Id < NodeLost.Length && NodeLost[n.Id]) n.State = NodeState.Perdida;      // tomada por su horda de zona: no se recupera sola
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
                else if (e.State == EdgeState.Cortada && !e.Debris && !Director.Active.Exists(p => p.Kind == HiveDirector.PlanKind.Corte && p.Edge == e.Id))
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
            foreach (var sv in Services)                                          // horarios puestos por el jugador (los del escenario no cambian y no entran)
                if (sv.ScheduleEdited)
                    for (int k = 0; k < sv.Stops.Count; k++)
                    {
                        var st = sv.Stops[k];
                        h += Mix(0x5C4ED0UL ^ ((ulong)(uint)sv.Train.Id << 40) ^ ((ulong)(uint)k << 32) ^ ((ulong)(uint)st.Node << 16) ^ (ulong)st.DepartAt);
                        for (int r3 = 0; r3 < NetConst.ResCount; r3++) h += Mix(0x5C4ED1UL ^ ((ulong)(uint)sv.Train.Id << 40) ^ ((ulong)(uint)k << 32) ^ ((ulong)r3 << 24) ^ (ulong)(uint)(st.Transfer == null ? 0 : st.Transfer[r3]));
                    }
            foreach (var sv in Services) if (sv.WorksTrain) h += Mix(0x70B5UL ^ ((ulong)(uint)sv.Train.Id << 32));
            foreach (var sv in Services) if (sv.HaltAt >= 0) h += Mix(0x4A17UL ^ ((ulong)(uint)sv.Train.Id << 32) ^ (ulong)(uint)(sv.HaltAt + 1));
            foreach (var sv in Services) if (sv.Hp != NetConst.TrainHpMax || sv.Destroyed || sv.GateHorde != 0) h += Mix(0xD177UL ^ ((ulong)(uint)sv.Train.Id << 40) ^ ((ulong)(uint)sv.Hp << 2) ^ (sv.Destroyed ? 1UL : 0UL)) + Mix(0xD177AUL ^ ((ulong)(uint)sv.Train.Id << 40) ^ (ulong)(uint)sv.GateHorde);
            foreach (var sv in Services) if (sv.GateNode >= 0) h += Mix(0xD152UL ^ ((ulong)(uint)sv.Train.Id << 32) ^ (ulong)(uint)(sv.GateNode + 1)) + Mix(0xD152AUL ^ ((ulong)(uint)sv.Train.Id << 40) ^ (ulong)sv.GateSinceTick);   // D-N152: 0 si nadie espera
            if (!GatesEnabled) h += Mix(0xD152F0UL);
            h += Embarque.Hash();                                                 // D-N166: 0 sin pasajeros (no mueve los goldens)
            r.Trains = h;
            h = 0;
            for (int n = 0; n < Graph.Nodes.Count; n++)
            {
                h += Mix(((ulong)(uint)n << 32) ^ (ulong)(uint)Attraction[n]);
                for (int r2 = 0; r2 < NetConst.ResCount; r2++) h += Mix(((ulong)(uint)n << 40) ^ ((ulong)r2 << 32) ^ (ulong)(uint)Stock[n, r2]);
                if (FoodRemMil[n] != 0) h += Mix(0xD176UL ^ ((ulong)(uint)n << 40) ^ (ulong)(uint)FoodRemMil[n]);
                if (StarveRemMil[n] != 0) h += Mix(0x5D176UL ^ ((ulong)(uint)n << 40) ^ (ulong)(uint)StarveRemMil[n]);
                if (NodeLost[n]) h += Mix(0x205EUL ^ ((ulong)(uint)n << 40));
                if (ZoneWallE4[n] != 0) h += Mix(0x2A11UL ^ ((ulong)(uint)n << 40) ^ (ulong)(uint)ZoneWallE4[n]);
                if (GarrisonMil[n] != 0) h += Mix(0x6A225UL ^ ((ulong)(uint)n << 40) ^ (ulong)(uint)GarrisonMil[n]);
            }
            h += Works.Hash();
            h += Stations.Hash();
            h += Taller.Hash();
            h += Production.Hash();
            r.Stock = h;
            h = 0;
            foreach (var e in Graph.Edges)
            {
                h += Mix(((ulong)(uint)e.Id << 8) ^ (ulong)e.State);
                if (e.Debris) h += Mix(Mix(0xDEB215UL ^ ((ulong)(uint)e.Id << 40) ^ ((ulong)(uint)(e.RepairSvc + 1) << 32)) ^ (ulong)e.RepairDone) + Mix((ulong)e.RepairNeed ^ ((ulong)(uint)e.RepairU << 48));
            }
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
        public static NetWorld Mvp(bool saguntoWalls = true)
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
            void Stock(string n, int mun, int comb, int mat, int per, int viv = 0) { int i = Id(n); w.Stock[i, 0] = mun; w.Stock[i, 1] = comb; w.Stock[i, 2] = mat; w.Stock[i, 3] = per; w.Stock[i, 4] = viv; }
            Stock("Valencia", 600, 800, 900, 300);
            Stock("Sagunto", 200, 1400, 700, 80, 80);         // D-N176: 80 u de víveres               // incluye el Puerto (D-N14): combustible y acero. 80 personas (Mak, 7-nov, con la medida de Hugo: con 170 y Castellón propia solo se perdía el 1,8 %)
            // Castellón empieza infestada, sin gente ni almacén propio (D-N124, decisión de Mak del 7-nov): se reclama el primer día con la misma orden
            // que una fundación (FoundStation: 30 u y 12 personas desde un tren parado). Hereda el depósito de combustible (Hugo: 100 + 200 = 300 u).
            w.Stations.P.Sites = new[] { "Nules", "Castellón" };
            Stock("Teruel", 20, 30, 40, 40);

            int[] T(int mun, int comb, int mat, int per) { return new[] { mun, comb, mat, per, 0 }; }
            ScheduleStop Stop(string n, double hour, int[] tr) { return new ScheduleStop { Node = Id(n), DepartAt = NetConst.Hours(hour), Transfer = tr }; }

            // Dos trenes al empezar (balance de Hugo: trenes_inicio = 2; el Tren 3 está a medio montar en el taller de Sagunto)
            // Tren 1: lanzadera de la costa Valencia–Sagunto–Castellón
            Add(w, 1, "1 Costa", Stop("Valencia", 6, T(80, 0, 120, 40)), Stop("Sagunto", 9, T(-40, 150, -60, -20)),
                Stop("Castellón", 11, T(-30, -100, 0, 0)), Stop("Sagunto", 14, T(0, -50, 0, 0)));
            // Tren 2: Sagunto–Teruel, el de montaña (abastece a Teruel)
            Add(w, 2, "2 Montaña", Stop("Sagunto", 7, T(60, 40, 80, 20)), Stop("Teruel", 15, T(-60, -40, -80, -20)));

            // tres manchas: al oeste de Segorbe (interior), al norte de Castellón y al este de Teruel
            // D-N37b: paisaje (lejos de la vía, no cuenta para el combate) y guarnición (a tiro de la vía: 1.400 ‰ (D-N39: como mucho 1.500), lo que paga el balance de Hugo)
            w.Field.BlobStatic(30, 74, 6, 550);                // paisaje: monte al oeste de Segorbe, lejos de la vía
            w.Field.SeedStatic(58, 63, 300, 100);              // guarnición pegada a Castellón (700 ‰)
            w.Field.SeedStatic(42, 62, 300, 100);              // guarnición pegada a Barracas (700 ‰)
            w.Field.BlobStatic(61, 55, 5, 600);           // al norte de Castellón, tierra adentro (no en el mar)
            w.Field.BlobStatic(42, 44, 4, 500);                // paisaje: al este de Teruel, lejos de la vía
            for (int i = 0; i < w.Graph.Nodes.Count; i++) if (w.Graph.Nodes[i].Name == "Sagunto") w.Embarque.HomeNode = i;     // D-N166: la tropa y los vehículos están en casa en Sagunto
            if (saguntoWalls) SagunteStart(w);     // D-N63: Sagunto empieza con 120 u de muro ya construidas
            if (saguntoWalls)                      // con las estructuras de partida: Castellón hereda su depósito de combustible (Hugo: 100 + 200 = 300 u)
            {
                int cas = Id("Castellón");
                var dep = w.Works.Preview(cas, PieceKind.Deposito, new[] { 48_000, -44_000 }, 1000);
                if (dep.Ok) { w.Works.Add(cas, dep); w.Works.CompleteAll(cas); }
            }
            w.Tick = NetConst.Hours(5.5);          // el prototipo arranca a las 05:30, media hora antes del primer tren
            return w;
        }

        // ---- D-N63 (Hugo, aprobada por Mak): el recinto de Sagunto empieza ya construido, 120 u, nivel 4,0
        // Medidas del recinto a escala real del tren (Level.cs, 6903083): X 70 a 156 y Y 44 a 85, la vía en Y = 64,5 y el andén en el centro.
        // Aquí, en mm locales al nodo (el andén es el (0, 0) y la vía es y = 0): X de -43.000 a +43.000 e Y de -20.500 a +20.500.
        const int FortHalfX = 43_000, FortHalfY = 20_500, HescoWing = 14_500;

        /// <summary>
        /// Construye el muro de partida de Sagunto con las mismas funciones que las órdenes (Obras.PlanWall / PlanPoint), pero ya terminado y sin cobrar:
        /// - Frente oeste (el que da a la horda y por donde entra la vía) en HESCO, con la puerta de vía que se pone sola donde el muro cruza la vía, y
        ///   14,5 m de HESCO en cada ala (norte y sur) desde las esquinas del oeste. El lado oeste es el corto (41 m), no el largo de 90 m que supuso
        ///   Hugo, así que el HESCO no llega a los 54 u: son ~40 u, y el total se mantiene en ~120 u / nivel 4,0 acortando las alas.
        /// - El resto del perímetro, en sacos. El lado este también cruza la vía, así que lleva su propia puerta de vía (8 u, la regla de PlanWall),
        ///   que queda bloqueada cerrada (GateMode 2): en la zona la vía acaba dentro del fuerte, y en automático los dos extremos se abrirían con el tren dentro.
        /// - La puerta de vehículos de 8 m en el muro sur (X 95–103 de la zona; 10 u, automática: se abre sola con un vehículo propio cerca), que sustituye a la puerta
        ///   peatonal, 2 nidos y 3 focos.
        /// Coste total ≈ 122,6 u (el muro de sacos del sur pierde 8 m = 1,6 u; la puerta suma 10 u y la peatonal se quita, −6 u). No se descuenta del almacén: el stock del día 1 no se toca.
        /// </summary>
        public static void SagunteStart(NetWorld w)
        {
            int sag = -1; foreach (var n in w.Graph.Nodes) if (n.Name == "Sagunto") sag = n.Id;
            if (sag < 0) return;
            var P = w.Works.P;
            const int Plenty = 1_000_000;                                    // existencias: las que haga falta, el muro de partida no se paga
            void Wall(PieceKind k, params int[] pts)
            {
                var plan = Obras.PlanWall(P, k, pts, Plenty, Plenty);
                if (!plan.Ok || plan.Cut) throw new InvalidOperationException("muro de partida de Sagunto: " + plan.Why);
                w.Works.Add(sag, plan);
                Finish(w, sag, plan);
            }
            void Point(PieceKind k, int x, int y)
            {
                var plan = Obras.PlanPoint(P, k, x, y, Plenty, Plenty);
                if (!plan.Ok) throw new InvalidOperationException("pieza de partida de Sagunto: " + plan.Why);
                w.Works.Add(sag, plan);
                Finish(w, sag, plan);
            }
            void VehicleGate(int x, int y)
            {
                var plan = Obras.PlanVehicleGate(P, x, y, true, Plenty, Plenty);
                if (!plan.Ok) throw new InvalidOperationException("puerta de vehículos de Sagunto: " + plan.Why);
                w.Works.Add(sag, plan);
                Finish(w, sag, plan);
            }
            int X = FortHalfX, Y = FortHalfY, A = HescoWing;
            int VehGateX0 = -18_000, VehGateX1 = VehGateX0 + P.VehGateWidthMm;
            Wall(PieceKind.Hesco, -X, -Y, -X, Y);                            // frente oeste: de la esquina sur a la norte, la puerta de vía en el cruce con y = 0
            Wall(PieceKind.Hesco, -X + A, Y, -X, Y);                         // ala norte (hacia el oeste)
            Wall(PieceKind.Hesco, -X, -Y, -X + A, -Y);                       // ala sur
            Wall(PieceKind.Sacos, -X + A, Y, X, Y);                          // norte, el resto
            Wall(PieceKind.Sacos, X, Y, X, -Y);                              // este
            // sur: sacos con la PUERTA DE VEHÍCULOS (Álvaro §9, Hugo D-N72) de 8 m en X 95–103 de la zona = −18.000 a −10.000 mm locales; sustituye a la puerta peatonal
            Wall(PieceKind.Sacos, X, -Y, VehGateX1, -Y);
            VehicleGate(VehGateX0 + (VehGateX1 - VehGateX0) / 2, -Y);
            Wall(PieceKind.Sacos, VehGateX0, -Y, -X + A, -Y);
            Point(PieceKind.Nido, -X + 3_500, 7_000); Point(PieceKind.Nido, -X + 3_500, -7_000);          // dos nidos junto a la puerta de vía, por dentro
            Point(PieceKind.Foco, -X + 2_500, 15_000); Point(PieceKind.Foco, -X + 2_500, -15_000); Point(PieceKind.Foco, 0, Y - 2_000);
            var l = w.Works.At(sag);
            for (int i = 0; i < l.Count; i++) if (l[i].IsGate && l[i].X0 > 0) { var g = l[i]; g.GateMode = 2; l[i] = g; }     // la puerta del este, bloqueada cerrada
        }

        /// <summary>Da por terminadas las piezas de un plan recién añadido (trabajo hecho = trabajo necesario).</summary>
        static void Finish(NetWorld w, int node, WorkPlan plan)
        {
            var l = w.Works.At(node);
            for (int i = l.Count - plan.Pieces.Count; i < l.Count; i++) { var p = l[i]; p.Built = true; p.WorkE4 = p.NeedE4; l[i] = p; }
        }

        /// <summary>
        /// Arranque de la partida según la D-N156 (red-inicio.md §5 de Álvaro, con Lucía y Hugo, 8-nov). Parte de Mvp (que siguen usando las pruebas y los
        /// goldens hasta que Nacho los pase a este) y cambia solo lo que dicen las decisiones:
        /// - Valencia (caída) y Teruel (bloqueada), sin existencias.
        /// - Los dos trenes, sin los horarios viejos (Castellón infestada recibía uno): esperan en Sagunto con una sola parada hasta que el jugador los envíe.
        /// - Una sola unidad (escala-existencias.md de Hugo): la reserva de red de Sagunto es 150 u de munición, 140 de combustible, 100 de materiales y 80
        ///   personas. Los 80 u de víveres de Hugo NO caben: la red tiene 4 recursos (Res) y no hay víveres (pendiente de Nacho y Hugo).
        /// - Nules, propia desde el principio (ya no se funda), con todo a 0 (Lucía y Hugo, existencias_red.json: la guarnición lleva encima su dotación).
        /// Pendiente (no está aquí): Nules sin asedio la primera noche (director, Nacho), su recinto de 130 u (Obras), Bétera fuera de RailNet (vive en
        /// AerodromoEstado) y cargar el almacén de la zona (Hugo y Marcos).
        /// </summary>
        public static NetWorld Inicio(bool saguntoWalls = true)
        {
            var w = Mvp(saguntoWalls);
            int Id(string name) { foreach (var n in w.Graph.Nodes) if (n.Name == name) return n.Id; throw new ArgumentException(name); }
            void Stock(string n, int mun, int comb, int mat, int per, int viv) { int i = Id(n); w.Stock[i, 0] = mun; w.Stock[i, 1] = comb; w.Stock[i, 2] = mat; w.Stock[i, 3] = per; w.Stock[i, (int)Res.Viveres] = viv; }
            Stock("Valencia", 0, 0, 0, 0, 0); Stock("Teruel", 0, 0, 0, 0, 0);
            Stock("Sagunto", 150, 140, 100, 80, 80);     // víveres: bloque «viveres_u» de existencias_red.json (Hugo, D-N176)
            Stock("Nules", 0, 0, 0, 0, 0);       // Hugo (existencias_red.json): 0 en todo; la guarnición lleva su dotación
            w.Stations.P.Sites = new[] { "Castellón" };
            w.Stations.Founded[Id("Nules")] = true;
            // D-N183 (Lucía, 9-nov, con Eugenio): Castellón es del jugador desde el principio, con su guarnición de 3 pelotones y 1 VAMTAC (26 personas; la colocación es de la zona:
            // estacion_castellon.json). Sin la guarnición zombi pegada a la estación. Hugo (existencias_red.json): 0 u de munición, 30 de combustible, 26 personas y sin consumo de víveres.
            // Recinto de 308,69 u (Álvaro, con el que Hugo calcula H0 = 1400). La Colmena la tiene en su historia desde el día 0 (no cuenta como recién ganada).
            // En el MVP no sufre la gran horda («Castellón cerrada»): el director no la asedia (Director.CerradaNodo).
            { int cas = Id("Castellón"); w.Stations.Founded[cas] = true; w.GarrisonMil[cas] = 26_000; w.ZoneWallE4[cas] = 3_086_900; Stock("Castellón", 0, 30, 0, 26, 0); w.FoodExemptNode = cas; w.Field.ClearStaticAt(58, 63); w.Director.CerradaNodo = cas; w.Director.ColSeedHistory(w); }
            w.CercoNodo = Id("Castellón");      // D-N184: suya pero cercada; sin trenes hasta después del MVP
            w.ObjNode = Id("Nules"); w.AmbushNode = Id("Nules");      // partida de 30 min (Lucía, 9-nov 08:00): objetivo del día 2 y emboscada de unos 300 al llegar el tren
            w.Director.SinAsedioNodo = Id("Nules"); w.Director.SinAsedioHasta = NetConst.DayTicks + NetConst.Hours(8);     // D-N156: Nules sin asedio la 1.ª noche (hasta las 08:00 del día siguiente)
            w.Director.ZoneHordes.Add(new HiveDirector.ZoneHorde { Node = Id("Nules"), FromDay = 1, Zombies = 2200 }); w.GarrisonMil[Id("Nules")] = 12_000; w.ZoneWallE4[Id("Nules")] = 1_322_400;   // guarnición de 12 y recinto de 132,24 u (Álvaro, nules-nivel.md §14: HESCO en los cruces de andén del oeste; antes 124,3)   // horda propia de Nules desde la 2.ª noche: 2200 en normal (Lucía, 9-nov 00:56, medida en NightSim con la D-N180; difícil 2440 cuando la red tenga dificultad); Castellón, sin noche en el MVP
            w.Services.Clear();
            var cero = new int[NetConst.ResCount];
            Add(w, 1, "1 Costa", new ScheduleStop { Node = Id("Sagunto"), DepartAt = NetConst.Hours(9), Transfer = (int[])cero.Clone() });
            Add(w, 2, "2 Montaña", new ScheduleStop { Node = Id("Sagunto"), DepartAt = NetConst.Hours(7), Transfer = (int[])cero.Clone() });
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
