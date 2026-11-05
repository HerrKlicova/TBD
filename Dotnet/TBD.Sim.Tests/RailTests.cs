using System;
using System.Collections.Generic;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Pruebas de la lógica pura del hito 2: red de vías, trenes y alertas.</summary>
    static class RailTests
    {
        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[9] Red de vías, trenes y alertas (hito 2, lógica pura)");

            // ---- caminos
            var g = RailNetSamples.Mvp();
            int Id(string name) { foreach (var n in g.Nodes) if (n.Name == name) return n.Id; return -1; }
            var p = g.ShortestPath(Id("Valencia"), Id("Zaragoza"));
            string Names(List<int> path) { var l = new List<string>(); foreach (var i in path) l.Add(g.Nodes[i].Name); return string.Join(" > ", l); }
            check(p.Count == 7 && Names(p) == "Valencia > Sagunto > Segorbe > Teruel > Caminreal > Calatayud > Zaragoza", "Valencia a Zaragoza pasa por Sagunto, Segorbe y Teruel: " + Names(p));
            check(g.ShortestPath(Id("Valencia"), Id("Vinaròs")).Count == 4, "Valencia a Vinaròs pasa por Sagunto y Castellón");

            var e = g.FindEdge(Id("Segorbe"), Id("Teruel"));
            e.State = EdgeState.Cortada;
            check(g.ShortestPath(Id("Valencia"), Id("Zaragoza")).Count == 0, "con la única línea al interior cortada no hay camino a Zaragoza");
            e.State = EdgeState.Danada;
            check(g.ShortestPath(Id("Valencia"), Id("Zaragoza")).Count == 7, "una vía dañada se puede usar (cuesta más)");
            e.State = EdgeState.Ok;

            // empates: dos caminos de igual longitud -> siempre el mismo
            var d = new RailGraph();
            for (int i = 0; i < 4; i++) d.AddNode("n" + i, i, 0);
            d.AddEdge(0, 1, 10); d.AddEdge(0, 2, 10); d.AddEdge(1, 3, 10); d.AddEdge(2, 3, 10);
            string a1 = string.Join(",", d.ShortestPath(0, 3)), a2 = string.Join(",", d.ShortestPath(0, 3));
            check(a1 == a2 && (a1 == "0,1,3" || a1 == "0,2,3"), "los empates se resuelven siempre igual (" + a1 + ")");

            // ---- trenes
            var t = new RailTrain { Id = 7, Name = "7", SpeedMmPerTick = 100 };
            t.SetRoute(g.ShortestPath(Id("Valencia"), Id("Sagunto")));
            t.Go();
            for (int i = 0; i < 1000; i++) t.Step(g);
            check(t.State == RailTrainState.EnMarcha && t.OffsetMm == 100_000, "tras 1000 ticks a 100 mm/tick lleva 100 m recorridos (" + t.OffsetMm + " mm)");
            for (int i = 0; i < 400_000; i++) t.Step(g);
            check(t.State == RailTrainState.Llegado, "el tren llega al final de la ruta");

            var t2 = new RailTrain { Id = 8, Name = "8", SpeedMmPerTick = 100 };
            t2.SetRoute(g.ShortestPath(Id("Valencia"), Id("Zaragoza")));
            t2.Go();
            for (int i = 0; i < 100_000; i++) t2.Step(g);     // unos 10 km
            long before = t2.OffsetMm;
            g.FindEdge(Id("Valencia"), Id("Sagunto")).State = EdgeState.Cortada;
            for (int i = 0; i < 50; i++) t2.Step(g);
            check(t2.State == RailTrainState.Detenido && t2.OffsetMm == before, "si cortan la vía bajo el tren, se detiene donde está");
            g.FindEdge(Id("Valencia"), Id("Sagunto")).State = EdgeState.Ok;
            t2.Go();
            for (int i = 0; i < 50; i++) t2.Step(g);
            check(t2.State == RailTrainState.EnMarcha && t2.OffsetMm > before, "al reparar la vía vuelve a marchar");

            // ---- alertas
            var trains = new List<RailTrain> { t2 };
            g.FindEdge(Id("Valencia"), Id("Sagunto")).HordeAtMm = 25_000_000L;      // horda en el km 25: a más de 3 km, no se detecta (D-N11)
            var far = AlertEngine.Evaluate(g, trains);
            check(far.Count == 0, "una horda a más de 3 km del tren no se detecta todavía (D-N11)");
            g.FindEdge(Id("Valencia"), Id("Sagunto")).HordeAtMm = t2.OffsetMm + 2_500_000L;   // a 2,5 km por delante
            var alerts = AlertEngine.Evaluate(g, trains, 9000);
            check(alerts.Count >= 1 && alerts[0].Kind == AlertKind.HordaCercaDelTren && alerts[0].EtaTicks > 0 && alerts[0].Text.Contains("min (") , "una horda por delante genera un aviso en tiempo de juego con hora: " + (alerts.Count > 0 ? alerts[0].Text : "ninguno"));
            // D-N11: con el tren parado y la horda común a 2 km/h, detectarla a 3 km da entre 60 y 120 s reales de aviso a x1
            var parado = new RailTrain { Id = 99, Name = "P", SpeedMmPerTick = 0 };
            parado.SetRoute(new List<int> { Id("Valencia"), Id("Sagunto") });
            g.FindEdge(Id("Valencia"), Id("Sagunto")).HordeAtMm = 3_000_000L;
            var ap = AlertEngine.Evaluate(g, new List<RailTrain> { parado });
            double realSecs = ap.Count > 0 ? ap[0].EtaTicks / 30.0 : -1;
            check(realSecs >= 60 && realSecs <= 120, $"aviso a x1 entre 60 y 120 s reales con la horda detectada a 3 km ({realSecs:0} s)");

            // tren detenido con horda: prioridad máxima y por encima de una vía cortada y de un nodo asediado
            g.FindEdge(Id("Segorbe"), Id("Teruel")).State = EdgeState.Cortada;
            g.Nodes[Id("Castellón")].State = NodeState.Asediada;
            g.FindEdge(Id("Valencia"), Id("Sagunto")).State = EdgeState.Cortada;
            t2.Step(g);                                                              // el tren nota la vía cortada y se detiene
            g.FindEdge(Id("Valencia"), Id("Sagunto")).HordeAtMm = t2.OffsetMm + 100_000L;   // horda a 100 m por delante
            alerts = AlertEngine.Evaluate(g, trains);
            check(alerts.Count >= 4 && alerts[0].Kind == AlertKind.TrenDetenido && alerts[0].Priority == 0, "el tren detenido con horda cerca es el primer aviso: " + alerts[0].Text);
            bool sorted = true;
            for (int i = 1; i < alerts.Count; i++) if (alerts[i - 1].Priority > alerts[i].Priority) sorted = false;
            check(sorted, "los avisos salen ordenados por prioridad");
            var again = AlertEngine.Evaluate(g, trains);
            bool same = again.Count == alerts.Count;
            for (int i = 0; same && i < alerts.Count; i++) same = again[i].Id == alerts[i].Id && again[i].Text == alerts[i].Text;
            check(same, "evaluar dos veces da exactamente los mismos avisos en el mismo orden");
            Console.WriteLine("         avisos: " + string.Join(" | ", alerts.ConvertAll(a => a.Text)));
        }
    }
}
