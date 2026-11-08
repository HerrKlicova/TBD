using System;
using System.Collections.Generic;

namespace TBD.Net
{
    // Lógica pura del hito 2 (red de vías, trenes y alertas), sin Unity ni ECS: se prueba con dotnet (Dotnet/TBD.Sim.Tests).
    // Todo en enteros (milímetros y ticks) para que sea determinista (GDD §17.3). Pensada para la capa S0 de la simulación global.

    public enum NodeState : byte { Segura = 0, Amenazada = 1, Asediada = 2, Perdida = 3 }   // GDD §7
    public enum EdgeState : byte { Ok = 0, Danada = 1, Cortada = 2 }                           // cortada = no se puede pasar

    public sealed class RailNode
    {
        public int Id;
        public string Name;
        public int XMeters, YMeters;          // para el mapa de operaciones
        public NodeState State;
        public int HordeDensity;              // por mil (0..1000), de la capa S0
    }

    public sealed class RailEdge
    {
        public int Id;
        public int A, B;                      // ids de nodo
        public long LengthMm;
        public long KmStartMm;                // punto kilométrico de A (para los avisos: «km 112»)
        public EdgeState State;
        public long HordeAtMm = -1;           // posición de la horda más cercana medida desde A; -1 = ninguna
        public readonly List<AmbushPoint> Ambush = new List<AmbushPoint>();   // puntos de emboscada del tramo (D-N16 de Lucía, para la Colmena del hito 4)
        public bool Debris;                   // escombros por una emboscada con corte (TR-4): la vía no existe hasta que el tren de obras la repara (hito 5)
        public int RepairSvc = -1;            // servicio del tren de obras que la está reparando (-1 = nadie)
        public long RepairDone;               // avance de la reparación, en obrero·ticks
        public long RepairNeed;               // trabajo total de la reparación en obrero·ticks (0 = el de un tramo normal)
        public int RepairU;                   // materiales de la reparación (0 = los de un tramo normal)
        public long HordeSpeedMmPerTick = 1905; // horda común a 2 km/h de juego con 24/7 s por tick (1.904,8 mm) (decisión D-N11 de Lucía); los rápidos, 6 km/h
    }

    public enum AmbushKind : byte { Desmonte = 0, Curva = 1, Puente = 2 }

    /// <summary>Punto de emboscada de un tramo: dónde le conviene a la Colmena atacar a un tren (km desde el nudo de Sagunto).</summary>
    public struct AmbushPoint
    {
        public long KmMm;
        public AmbushKind Kind;
        public string Name;
    }

    public sealed class RailGraph
    {
        public readonly List<RailNode> Nodes = new List<RailNode>();
        public readonly List<RailEdge> Edges = new List<RailEdge>();

        // ---- guardado (Guardado.cs): solo lo que cambia durante la partida; el mapa lo pone el escenario
        public void SaveState(SaveWriter w)
        {
            w.Int(Nodes.Count);
            foreach (var n in Nodes) { w.Byte((byte)n.State); w.Int(n.HordeDensity); }
            w.Int(Edges.Count);
            foreach (var e in Edges) { w.Byte((byte)e.State); w.Long(e.HordeAtMm); w.Bool(e.Debris); w.Int(e.RepairSvc); w.Long(e.RepairDone); w.Long(e.RepairNeed); w.Int(e.RepairU); }
        }

        public void LoadState(SaveReader r)
        {
            r.Expect(Nodes.Count, "nodos");
            foreach (var n in Nodes) { n.State = (NodeState)r.Byte(); n.HordeDensity = r.Int(); }
            r.Expect(Edges.Count, "tramos");
            foreach (var e in Edges) { e.State = (EdgeState)r.Byte(); e.HordeAtMm = r.Long(); e.Debris = r.Bool(); e.RepairSvc = r.Int(); e.RepairDone = r.Long(); e.RepairNeed = r.Long(); e.RepairU = r.Int(); }
        }

        public RailNode AddNode(string name, int x, int y)
        {
            var n = new RailNode { Id = Nodes.Count, Name = name, XMeters = x, YMeters = y, State = NodeState.Segura };
            Nodes.Add(n);
            return n;
        }

        public RailEdge AddEdge(int a, int b, long lengthMm, long kmStartMm = 0)
        {
            var e = new RailEdge { Id = Edges.Count, A = a, B = b, LengthMm = lengthMm, KmStartMm = kmStartMm, State = EdgeState.Ok };
            Edges.Add(e);
            return e;
        }

        public RailEdge FindEdge(int a, int b)
        {
            for (int i = 0; i < Edges.Count; i++)
            {
                var e = Edges[i];
                if ((e.A == a && e.B == b) || (e.A == b && e.B == a)) return e;
            }
            return null;
        }

        /// <summary>
        /// Camino más corto entre dos nodos por Dijkstra con enteros, ignorando las vías cortadas. Los empates se resuelven por
        /// id de nodo (orden fijo), así que el resultado es el mismo en cualquier máquina. Devuelve los ids de nodo en orden,
        /// o una lista vacía si no hay camino.
        /// </summary>
        public List<int> ShortestPath(int from, int to)
        {
            int n = Nodes.Count;
            var dist = new long[n];
            var prev = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = long.MaxValue; prev[i] = -1; }
            dist[from] = 0;
            for (int iter = 0; iter < n; iter++)
            {
                int u = -1;
                for (int i = 0; i < n; i++)
                    if (!done[i] && dist[i] != long.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;   // empate: gana el id menor
                if (u < 0) break;
                done[u] = true;
                if (u == to) break;
                for (int k = 0; k < Edges.Count; k++)
                {
                    var e = Edges[k];
                    if (e.State == EdgeState.Cortada) continue;
                    int v = e.A == u ? e.B : (e.B == u ? e.A : -1);
                    if (v < 0 || done[v]) continue;
                    long cand = dist[u] + e.LengthMm + (e.State == EdgeState.Danada ? e.LengthMm / 2 : 0);   // dañada: va más lento
                    if (cand < dist[v] || (cand == dist[v] && u < prev[v])) { dist[v] = cand; prev[v] = u; }
                }
            }
            var path = new List<int>();
            if (dist[to] == long.MaxValue) return path;
            for (int at = to; at >= 0; at = prev[at]) path.Add(at);
            path.Reverse();
            return path;
        }
    }

    public enum RailTrainState : byte { Parado = 0, EnMarcha = 1, Detenido = 2, Llegado = 3 }   // detenido = vía cortada por delante

    public sealed class RailTrain
    {
        public int Id;
        public string Name;
        public List<int> Route = new List<int>();      // ids de nodo
        public int Leg;                                // tramo actual: de Route[Leg] a Route[Leg + 1]
        public long OffsetMm;                          // avance dentro del tramo
        public long SpeedMmPerTick;                    // 100 = 3 m/s a 30 Hz
        public RailTrainState State = RailTrainState.Parado;
        public int CargoTonnes;

        public void SaveState(SaveWriter w)
        {
            w.Int(Id); w.Str(Name); w.Int(Route == null ? -1 : Route.Count);
            if (Route != null) foreach (var n in Route) w.Int(n);
            w.Int(Leg); w.Long(OffsetMm); w.Long(SpeedMmPerTick); w.Byte((byte)State); w.Int(CargoTonnes);
        }

        public static RailTrain LoadState(SaveReader r)
        {
            var t = new RailTrain { Id = r.Int(), Name = r.Str() };
            int n = r.Int();
            if (n < 0) t.Route = null;
            else { if (n > r.Remaining) throw new SaveException(SaveError.Corrupt, "ruta más larga que el archivo"); t.Route = new List<int>(n); for (int i = 0; i < n; i++) t.Route.Add(r.Int()); }
            t.Leg = r.Int(); t.OffsetMm = r.Long(); t.SpeedMmPerTick = r.Long(); t.State = (RailTrainState)r.Byte(); t.CargoTonnes = r.Int();
            return t;
        }

        public void SetRoute(List<int> route)
        {
            Route = route;
            Leg = 0;
            OffsetMm = 0;
            State = route.Count >= 2 ? RailTrainState.Parado : RailTrainState.Llegado;
        }

        public void Go() { if (State == RailTrainState.Parado || State == RailTrainState.Detenido) State = RailTrainState.EnMarcha; }
        public void Stop() { if (State == RailTrainState.EnMarcha) State = RailTrainState.Parado; }

        public RailEdge CurrentEdge(RailGraph g)
        {
            return Leg + 1 < Route.Count ? g.FindEdge(Route[Leg], Route[Leg + 1]) : null;
        }

        /// <summary>Punto kilométrico en milímetros (según el sentido en que se recorre el tramo).</summary>
        public long KmMm(RailGraph g)
        {
            var e = CurrentEdge(g);
            if (e == null) return 0;
            bool forward = e.A == Route[Leg];
            return e.KmStartMm + (forward ? OffsetMm : e.LengthMm - OffsetMm);
        }

        /// <summary>Un tick de marcha. Si la vía de delante está cortada, el tren se detiene antes de entrar o donde está.</summary>
        public void Step(RailGraph g)
        {
            if (State != RailTrainState.EnMarcha) return;
            long remaining = SpeedMmPerTick;
            while (remaining > 0)
            {
                var e = CurrentEdge(g);
                if (e == null) { State = RailTrainState.Llegado; return; }
                if (e.State == EdgeState.Cortada) { State = RailTrainState.Detenido; return; }
                long left = e.LengthMm - OffsetMm;
                if (remaining >= left)
                {
                    remaining -= left;
                    Leg++;
                    OffsetMm = 0;
                    if (Leg + 1 >= Route.Count) { State = RailTrainState.Llegado; return; }
                }
                else
                {
                    OffsetMm += remaining;
                    remaining = 0;
                }
            }
        }
    }

    public enum AlertKind : byte { TrenDetenido = 0, HordaCercaDelTren = 1, NodoAsediado = 2, ViaCortada = 3 }

    public struct Alert
    {
        public int Id;               // estable: depende solo del tipo y del sujeto
        public AlertKind Kind;
        public int Priority;         // 0 = la más urgente
        public long EtaTicks;        // tiempo hasta el impacto (-1 si no aplica)
        public int Subject;          // id de tren, nodo o vía
        public string Text;
    }

    public static class AlertEngine
    {

        /// <summary>Minutos de JUEGO hasta el impacto (QA Q3: antes eran segundos reales a x1, que a x4 duraban la cuarta parte).</summary>
        public static long EtaMinutes(long etaTicks) { return NetConst.GameMs(etaTicks) / 60000; }

        public const long DetectMm = 3_000_000;    // detección de la horda a unos 3 km (D-N11): unos 75 s reales de aviso a x1

        static string ClockAt(long tick)
        {
            return NetConst.Clock(tick);
        }

        static string When(long eta, long nowTick)
        {
            string s = EtaMinutes(eta) + " min";
            return nowTick >= 0 ? s + " (" + ClockAt(nowTick + eta) + ")" : s;
        }

        /// <summary>Alertas priorizadas («Tren 7 detenido en el km 112, horda a 25 min»), con tiempos en minutos de juego. Orden: prioridad, luego tiempo, luego id.</summary>
        public static List<Alert> Evaluate(RailGraph g, List<RailTrain> trains, long nowTick = -1)
        {
            var list = new List<Alert>();
            foreach (var t in trains)
            {
                var e = t.CurrentEdge(g);
                long eta = -1;
                if (e != null && e.HordeAtMm >= 0)
                {
                    bool forward = e.A == t.Route[t.Leg];
                    long posA = forward ? t.OffsetMm : e.LengthMm - t.OffsetMm;
                    long gap = Math.Abs(e.HordeAtMm - posA);
                    bool hordeAhead = forward ? e.HordeAtMm >= posA : e.HordeAtMm <= posA;
                    long closing = hordeAhead ? t.SpeedMmPerTick + e.HordeSpeedMmPerTick : e.HordeSpeedMmPerTick - t.SpeedMmPerTick;
                    if (t.State != RailTrainState.EnMarcha) closing = e.HordeSpeedMmPerTick;     // parado: solo se acerca la horda
                    if (closing > 0 && gap <= DetectMm) eta = gap / closing;           // fuera de los 3 km no se detecta
                }
                string km = (t.KmMm(g) / 1_000_000L).ToString();
                if (t.State == RailTrainState.Detenido)
                {
                    string extra = eta >= 0 ? ", horda en " + When(eta, nowTick) : "";
                    list.Add(new Alert { Id = Make(AlertKind.TrenDetenido, t.Id), Kind = AlertKind.TrenDetenido, Priority = (eta >= 0 && EtaMinutes(eta) <= 60) ? 0 : 1, EtaTicks = eta, Subject = t.Id,
                        Text = "Tren " + t.Name + " detenido en el km " + km + extra });
                }
                else if (eta >= 0)
                {
                    long mins = EtaMinutes(eta);
                    list.Add(new Alert { Id = Make(AlertKind.HordaCercaDelTren, t.Id), Kind = AlertKind.HordaCercaDelTren, Priority = mins <= 30 ? 0 : (mins <= 60 ? 1 : 2),
                        EtaTicks = eta, Subject = t.Id, Text = "Horda en " + When(eta, nowTick) + " junto al tren " + t.Name + " (km " + km + ")" });
                }
            }
            foreach (var n in g.Nodes)
                if (n.State == NodeState.Asediada)
                    list.Add(new Alert { Id = Make(AlertKind.NodoAsediado, n.Id), Kind = AlertKind.NodoAsediado, Priority = 1, EtaTicks = -1, Subject = n.Id, Text = n.Name + " asediada" });
            foreach (var e in g.Edges)
                if (e.State == EdgeState.Cortada)
                    list.Add(new Alert { Id = Make(AlertKind.ViaCortada, e.Id), Kind = AlertKind.ViaCortada, Priority = 3, EtaTicks = -1, Subject = e.Id,
                        Text = "Vía cortada entre " + g.Nodes[e.A].Name + " y " + g.Nodes[e.B].Name });
            list.Sort((a, b) =>
            {
                int c = a.Priority.CompareTo(b.Priority);
                if (c != 0) return c;
                long ea = a.EtaTicks < 0 ? long.MaxValue : a.EtaTicks, eb = b.EtaTicks < 0 ? long.MaxValue : b.EtaTicks;
                c = ea.CompareTo(eb);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            return list;
        }

        static int Make(AlertKind kind, int subject) { return ((int)kind << 24) ^ subject; }
    }

    public static class RailNetSamples
    {
        /// <summary>
        /// Red canónica del MVP (D-N14 de Lucía, aprobada por Mak): nudo de Sagunto (con el Puerto dentro del nodo), rama sur
        /// Puçol–Valencia, rama norte Almenara–Nules–Castellón y rama interior Segorbe–Barracas–Teruel–(puente del Jiloca)–Caminreal–
        /// Calamocha–Cariñena–Zaragoza. Calatayud y Vinaròs quedan fuera (ganchos del acceso anticipado).
        /// Kilómetros desde Sagunto: los reales (Paula, PK del C-6; 8-nov), con el puente del Mijares en el km 34,5 (Álvaro). El Carraixet sigue en el km 24 (SIN VERIFICAR). Posiciones del mapa: latitud y longitud APROXIMADAS (de memoria,
        /// sin verificar) con x = 50 + (lon + 0,38) · 24, y = 80 − (lat − 39,47) · 32; una unidad ≈ 3,5 km.
        /// Puntos de emboscada: propuestos por Marcos y revisados por Lucía (5-nov): seis valen, Paniza queda por verificar con la BTN
        /// y Lucía añade el puente del barranco del Carraixet.
        /// </summary>
        public static RailGraph Mvp()
        {
            var g = new RailGraph();
            int sagunto = g.AddNode("Sagunto", 53, 73).Id;
            int pucol = g.AddNode("Puçol", 52, 75).Id;
            int valencia = g.AddNode("Valencia", 50, 80).Id;
            int almenara = g.AddNode("Almenara", 54, 71).Id;
            int nules = g.AddNode("Nules", 55, 68).Id;
            int castellon = g.AddNode("Castellón", 58, 63).Id;
            int segorbe = g.AddNode("Segorbe", 47, 68).Id;
            int barracas = g.AddNode("Barracas", 42, 62).Id;
            int teruel = g.AddNode("Teruel", 33, 52).Id;
            int caminreal = g.AddNode("Caminreal", 27, 36).Id;
            int calamocha = g.AddNode("Calamocha", 28, 34).Id;
            int carinena = g.AddNode("Cariñena", 30, 20).Id;
            int zaragoza = g.AddNode("Zaragoza", 38, 10).Id;
            const long km = 1_000_000L;
            // rama sur
            g.AddEdge(sagunto, pucol, 7_130_000L, 0);                                    // km reales (Paula, PK del C-6): 7,13
            var pv = g.AddEdge(pucol, valencia, 26_500_000L, 7_130_000L);                 // 26,50 (Valencia Nord a 33,63 de Sagunto)
            pv.Ambush.Add(new AmbushPoint { KmMm = 24 * km, Kind = AmbushKind.Puente, Name = "puente del barranco del Carraixet" });   // junto al nido de Valencia (Lucía)
            // rama norte
            g.AddEdge(sagunto, almenara, 9_590_000L, 0);                                 // 9,59
            g.AddEdge(almenara, nules, 11_760_000L, 9_590_000L);                          // 11,76 (Nules a 21,35)
            var nc = g.AddEdge(nules, castellon, 18_620_000L, 21_350_000L);               // 18,62 (Castellón a 39,97)
            nc.Ambush.Add(new AmbushPoint { KmMm = 34_500_000L,     /* Álvaro (red-inicio.md v1): km 34,5, hasta que Paula lo mida */ Kind = AmbushKind.Puente, Name = "puente del Mijares" });
            // rama interior
            var ss = g.AddEdge(sagunto, segorbe, 35 * km, 0);
            ss.Ambush.Add(new AmbushPoint { KmMm = 21 * km, Kind = AmbushKind.Curva, Name = "curvas del Palancia" });
            var sb = g.AddEdge(segorbe, barracas, 30 * km, 35 * km);
            sb.Ambush.Add(new AmbushPoint { KmMm = 52 * km, Kind = AmbushKind.Desmonte, Name = "desmonte de Jérica" });
            var bt = g.AddEdge(barracas, teruel, 65 * km, 65 * km);
            bt.Ambush.Add(new AmbushPoint { KmMm = 78 * km, Kind = AmbushKind.Desmonte, Name = "puerto de Escandón" });
            bt.Ambush.Add(new AmbushPoint { KmMm = 104 * km, Kind = AmbushKind.Curva, Name = "curvas de Puebla de Valverde" });
            var tc = g.AddEdge(teruel, caminreal, 70 * km, 130 * km);
            tc.Ambush.Add(new AmbushPoint { KmMm = 192 * km, Kind = AmbushKind.Puente, Name = "puente del Jiloca" });
            g.AddEdge(caminreal, calamocha, 15 * km, 200 * km);
            var cc = g.AddEdge(calamocha, carinena, 45 * km, 215 * km);
            cc.Ambush.Add(new AmbushPoint { KmMm = 240 * km, Kind = AmbushKind.Desmonte, Name = "desmonte de Paniza (por verificar con la BTN)" });
            g.AddEdge(carinena, zaragoza, 50 * km, 260 * km);
            return g;
        }
    }
}
