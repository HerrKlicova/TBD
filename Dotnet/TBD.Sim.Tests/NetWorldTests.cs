using System;
using System.Collections.Generic;
using System.Diagnostics;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Pruebas del hito 2 «Red»: mancha de horda (S0), horarios y carga, órdenes de tiempo y determinismo.</summary>
    static class NetWorldTests
    {
        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[10] Mundo S0 del hito 2: mancha, horarios, carga, tiempo y determinismo");

            // ---- la mancha conserva la masa y se mueve
            var w = NetScenario.Mvp();
            long m0 = w.Field.Mass();
            int[] before = (int[])w.Field.D.Clone();
            for (int i = 0; i < 300; i++) w.Field.Step(w.Graph, w.Attraction);
            long m1 = w.Field.Mass();
            int changed = 0; for (int i = 0; i < before.Length; i++) if (before[i] != w.Field.D[i]) changed++;
            check(m0 == m1, $"la mancha conserva la masa en 300 pasos ({m0} → {m1})");
            check(changed > 500, $"la mancha se desplaza y se difunde ({changed} celdas cambian)");
            int maxCell = 0; foreach (var d in w.Field.D) maxCell = Math.Max(maxCell, d);
            check(maxCell <= HordeField.Max, $"ninguna celda pasa del tope ({maxCell})");

            // ---- horarios: el tren 1 sale a las 06:00 de Valencia y llega a Sagunto con la carga
            w = NetScenario.Mvp();
            var s1 = w.Services[0];
            long departTick = -1, arriveTick = -1;
            for (long t = 0; t < NetConst.DayTicks && arriveTick < 0; t++)
            {
                bool atBefore = s1.AtStop;
                w.ApplyCommands();
                w.Step();
                if (atBefore && !s1.AtStop && departTick < 0) departTick = w.Tick - 1;
                if (!atBefore && s1.AtStop) arriveTick = w.Tick;
            }
            check(departTick == NetConst.Hours(6), $"el tren 1 sale de Valencia a las {NetConst.Clock(departTick)} (horario 06:00)");
            check(arriveTick > departTick && s1.StopIndex == 1, $"llega a Sagunto a las {NetConst.Clock(arriveTick)} (30 km a 60 km/h ≈ 30 min)");
            int sagunto = s1.Stops[1].Node;
            check(s1.Cargo[0] == 80 && s1.Cargo[2] == 120 && s1.Cargo[3] == 40, $"carga en Valencia antes de salir: munición {s1.Cargo[0]}, materiales {s1.Cargo[2]}, personas {s1.Cargo[3]}");
            while (w.Tick < NetConst.Hours(9)) { w.ApplyCommands(); w.Step(); }
            int stockBefore = w.Stock[sagunto, 0];
            while (s1.AtStop && w.Tick < NetConst.DayTicks) { w.ApplyCommands(); w.Step(); }
            check(NetConst.Clock(w.Tick - 1) == "09:00", $"sale de Sagunto a las {NetConst.Clock(w.Tick - 1)} (horario 09:00)");
            check(s1.Cargo[0] == 40 && w.Stock[sagunto, 0] == stockBefore + 40, $"en Sagunto deja la mitad: munición a bordo {s1.Cargo[0]}, en la estación {stockBefore} → {w.Stock[sagunto, 0]}");
            check(s1.CargoTotal() <= NetConst.TrainCapacity, "nunca se pasa de la capacidad del tren");

            // ---- un día completo: cada tren hace su servicio y la carga se conserva (tren + estaciones)
            w = NetScenario.Mvp();
            long total0 = TotalGoods(w);
            for (long t = 0; t < 2 * NetConst.DayTicks; t++) { w.ApplyCommands(); w.Step(); }
            check(TotalGoods(w) == total0, $"en dos días no se crea ni se pierde carga ({total0} t)");
            int moved = 0; foreach (var s in w.Services) if (s.LastDepartDay >= 1) moved++;
            check(moved >= 2, $"los trenes vuelven a salir al día siguiente ({moved} de 3)");

            // ---- órdenes de tiempo: pausa, reanudar y velocidad
            w = NetScenario.Mvp();
            long t0 = w.Tick;
            for (int i = 0; i < 10; i++) { w.ApplyCommands(); w.Step(); }
            w.Push(new NetCommand { Tick = w.Tick, Type = NetCmdType.Pause });
            for (int i = 0; i < 10; i++) { w.ApplyCommands(); w.Step(); }
            long pausedAt = w.Tick - t0;
            check(w.Paused && pausedAt == 10, $"en pausa el reloj no avanza ({pausedAt} ticks desde el arranque)");
            w.Push(new NetCommand { Tick = w.Tick, Type = NetCmdType.Resume });
            w.Push(new NetCommand { Tick = w.Tick, Type = NetCmdType.SetSpeed, Arg = 4 });
            w.ApplyCommands(); w.Step();
            check(!w.Paused && w.Speed == 4 && w.Tick - t0 == 11, "reanudar y poner x4 se aplican aunque el reloj estuviera parado");

            // ---- retener un tren
            w = NetScenario.Mvp();
            w.Push(new NetCommand { Tick = 0, Type = NetCmdType.TrainHold, Arg = 0 });
            for (long t = 0; t < NetConst.Hours(7); t++) { w.ApplyCommands(); w.Step(); }
            check(w.Services[0].AtStop && w.Services[0].StopIndex == 0, "un tren retenido no sale aunque llegue su hora");
            w.Push(new NetCommand { Tick = w.Tick, Type = NetCmdType.TrainGo, Arg = 0 });
            for (int i = 0; i < 5; i++) { w.ApplyCommands(); w.Step(); }
            check(!w.Services[0].AtStop, "al soltarlo sale (ya es su hora)");

            // ---- horda sobre la vía: corta, el tren se detiene y hay aviso
            w = NetScenario.Mvp();
            var g = w.Graph;
            var e = g.FindEdge(w.Services[1].Stops[0].Node, IdOf(g, "Segorbe"));       // Sagunto–Segorbe, en la ruta del tren 2
            var a = g.Nodes[e.A]; var b = g.Nodes[e.B];
            int mx = (a.XMeters + b.XMeters) / 2, my = (a.YMeters + b.YMeters) / 2;
            bool sawStop = false, sawCut = false;
            for (long t = 0; t < NetConst.Hours(10); t++)
            {
                if (t == NetConst.Hours(7) + 60) w.Field.Blob(mx, my, 3, 1000);       // la horda cae sobre la vía con el tren ya en marcha
                w.ApplyCommands(); w.Step();
                if (e.State == EdgeState.Cortada) sawCut = true;
                foreach (var al in w.Alerts) if (al.Kind == AlertKind.TrenDetenido || al.Kind == AlertKind.HordaCercaDelTren) sawStop = true;
            }
            check(sawCut, "una mancha densa sobre la vía la corta");
            check(sawStop, "el tren 2 da aviso (detenido o con la horda cerca)");

            // ---- determinismo: dos mundos con las mismas órdenes, mismo hash en cada tick
            var w1 = NetScenario.Mvp();
            var w2 = NetScenario.Mvp();
            bool same = true;
            var sw = Stopwatch.StartNew();
            for (long t = 0; t < NetConst.DayTicks; t++)
            {
                if (t == 500) { var c = new NetCommand { Tick = 502, Type = NetCmdType.TrainHold, Arg = 2 }; w1.Push(c); w2.Push(c); }
                if (t == 9000) { var c = new NetCommand { Tick = 9002, Type = NetCmdType.TrainGo, Arg = 2 }; w1.Push(c); w2.Push(c); }
                w1.ApplyCommands(); w1.Step();
                w2.ApplyCommands(); w2.Step();
                if (w1.Hash() != w2.Hash()) { same = false; break; }
            }
            sw.Stop();
            check(same, "dos mundos S0 con las mismas órdenes dan el mismo hash durante un día entero");
            double perTick = sw.Elapsed.TotalMilliseconds / NetConst.DayTicks / 2;
            Console.WriteLine($"         coste: {perTick:0.000} ms por tick del mundo S0 (con hash; la mancha avanza 1 de cada {NetConst.FieldEvery})");
            check(perTick * 4 < 8.0, $"a x4 cabe en el presupuesto de un fotograma ({perTick * 4:0.00} ms)");
        }

        static int IdOf(RailGraph g, string name) { foreach (var n in g.Nodes) if (n.Name == name) return n.Id; return -1; }

        static long TotalGoods(NetWorld w)
        {
            long t = 0;
            for (int n = 0; n < w.Graph.Nodes.Count; n++) for (int r = 0; r < NetConst.ResCount; r++) t += w.Stock[n, r];
            foreach (var s in w.Services) t += s.CargoTotal();
            return t;
        }
    }
}
