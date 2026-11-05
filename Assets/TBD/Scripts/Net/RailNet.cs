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
        public long HordeSpeedMmPerTick = 40; // aproximación de la horda hacia el tren (≈ 1,2 m/s a 30 Hz)
    }

    public sealed class RailGraph
    {
        public readonly List<RailNode> Nodes = new List<RailNode>();
        public readonly List<RailEdge> Edges = new List<RailEdge>();

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
        public const int TicksPerSecond = 30;

        /// <summary>Alertas priorizadas («Tren 7 detenido en el km 112, horda a 90 s»). Orden: prioridad, luego tiempo, luego id.</summary>
        public static List<Alert> Evaluate(RailGraph g, List<RailTrain> trains)
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
                    if (closing > 0) eta = gap / closing;
                }
                string km = (t.KmMm(g) / 1_000_000L).ToString();
                if (t.State == RailTrainState.Detenido)
                {
                    string extra = eta >= 0 ? ", horda a " + (eta / TicksPerSecond) + " s" : "";
                    list.Add(new Alert { Id = Make(AlertKind.TrenDetenido, t.Id), Kind = AlertKind.TrenDetenido, Priority = (eta >= 0 && eta / TicksPerSecond <= 120) ? 0 : 1, EtaTicks = eta, Subject = t.Id,
                        Text = "Tren " + t.Name + " detenido en el km " + km + extra });
                }
                else if (eta >= 0)
                {
                    long secs = eta / TicksPerSecond;
                    list.Add(new Alert { Id = Make(AlertKind.HordaCercaDelTren, t.Id), Kind = AlertKind.HordaCercaDelTren, Priority = secs <= 30 ? 0 : (secs <= 120 ? 1 : 2),
                        EtaTicks = eta, Subject = t.Id, Text = "Horda a " + secs + " s del tren " + t.Name + " (km " + km + ")" });
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
        /// Red de ejemplo del MVP: nudo en Sagunto, línea Sagunto–Teruel–Zaragoza y costera Valencia–Sagunto–Castellón (GDD §15).
        /// Las LONGITUDES SON DE EJEMPLO (kilómetros redondeados, sin verificar con datos de la red real). Las posiciones del mapa salen
        /// de latitud y longitud aproximadas de cada ciudad (de memoria, sin verificar): x = 50 + (lon + 0,38) · 24, y = 80 − (lat − 39,47) · 32;
        /// una unidad de mapa ≈ 3,5 km.
        /// </summary>
        public static RailGraph Mvp()
        {
            var g = new RailGraph();
            int valencia = g.AddNode("Valencia", 50, 80).Id;
            int puerto = g.AddNode("Puerto de Sagunto", 54, 74).Id;
            int sagunto = g.AddNode("Sagunto", 53, 73).Id;
            int castellon = g.AddNode("Castellón", 58, 63).Id;
            int segorbe = g.AddNode("Segorbe", 47, 68).Id;
            int caminreal = g.AddNode("Caminreal", 28, 36).Id;
            int teruel = g.AddNode("Teruel", 33, 52).Id;
            int calatayud = g.AddNode("Calatayud", 20, 20).Id;
            int zaragoza = g.AddNode("Zaragoza", 38, 10).Id;
            int vinaros = g.AddNode("Vinaròs", 70, 48).Id;
            const long km = 1_000_000L;
            g.AddEdge(valencia, sagunto, 30 * km, 0);
            g.AddEdge(sagunto, puerto, 5 * km, 30 * km);
            g.AddEdge(sagunto, castellon, 60 * km, 30 * km);
            g.AddEdge(castellon, vinaros, 75 * km, 90 * km);
            g.AddEdge(sagunto, segorbe, 35 * km, 30 * km);
            g.AddEdge(segorbe, teruel, 90 * km, 65 * km);
            g.AddEdge(teruel, caminreal, 45 * km, 155 * km);
            g.AddEdge(caminreal, calatayud, 80 * km, 200 * km);
            g.AddEdge(calatayud, zaragoza, 90 * km, 280 * km);
            return g;
        }
    }
}
