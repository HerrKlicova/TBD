using System;
using System.Collections.Generic;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Hito 5, paso 5: el taller de Sagunto (cola de encargos, plazos, costes, máximo de trenes, Chispas, cancelar y hash).</summary>
    static class TallerTests
    {
        static NetWorld World()
        {
            var w = NetScenario.Mvp(); w.Services.Clear();            // sin trenes del escenario: no tocan las existencias de Sagunto
            return w;
        }
        static int Sag(NetWorld w) { return w.Taller.NodeIndex; }
        static int S(NetWorld w, Res r) { return w.Stock[Sag(w), (int)r]; }
        static void Steps(NetWorld w, long n) { for (long i = 0; i < n; i++) w.Step(); }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[24] El taller fabrica y rehabilita trenes (hito 5, paso 5)");
            var d = NetScenario.Mvp();
            check(d.Taller.NodeIndex >= 0 && d.Taller.P.Node == "Sagunto" && d.Taller.Hash() == 0, "el taller está en Sagunto y, sin encargos, su hash vale 0 (los goldens no se mueven)");
            var p = d.Taller.P;
            check(p.TrainU == 120 && p.TrainFuel == 30 && p.Tren3U == 60 && p.Tren3Fuel == 15 && p.ObrasU == 20 && p.ObrasFuel == 5 && p.ObrasCrew == 8 && p.TrainDays == 3 && p.RehabHours == 36 && p.MaxTrains == 4 && p.MaxTrainsStage2 == 6,
                "los costes y plazos salen del CSV de Hugo: tren 120 u + 30, Tren 3 60 + 15, tren de obras 20 + 5 con 8 obreros, 3 días, rehabilitar 36 h, máximo 4 (demo) y 6 (etapa 2)");

            // rehabilitar el Tren 3
            var w = World(); int m0 = S(w, Res.Materiales), f0 = S(w, Res.Combustible), p0 = S(w, Res.Personas);
            check(w.OrderTrain(TallerKind.RehabTren3) == TallerResult.Ok && S(w, Res.Materiales) == m0 - 60 && S(w, Res.Combustible) == f0 - 15 && S(w, Res.Personas) == p0 && w.Taller.Queue.Count == 1, "rehabilitar el Tren 3 cuesta 60 u y 15 de combustible, sin gente");
            check(w.OrderTrain(TallerKind.RehabTren3) == TallerResult.YaRehabilitado, "el Tren 3 solo se rehabilita una vez");
            long rehab = NetConst.Hours(36);
            Steps(w, rehab - 1); bool before = w.Services.Count == 0;
            w.Step();
            var t3 = w.Services.Count == 1 ? w.Services[0] : null;
            check(before && t3 != null && t3.Train.Id == 1 && !t3.WorksTrain && t3.Stops.Count == 1 && t3.Stops[0].Node == Sag(w) && t3.AtStop && w.Taller.Queue.Count == 0, $"a las 36 h (1,5 días, {rehab} ticks) sale el tren, en Sagunto y con una sola parada (sin horario no se mueve)");

            // tren de obras: sale con 8 obreros a bordo y es tren de obras
            var wo = World(); int po = S(wo, Res.Personas), mo = S(wo, Res.Materiales);
            check(wo.OrderTrain(TallerKind.RehabObras) == TallerResult.Ok && S(wo, Res.Materiales) == mo - 20 && S(wo, Res.Combustible) == 1400 - 5 && S(wo, Res.Personas) == po - 8, "rehabilitar el tren de obras cuesta 20 u, 5 de combustible y 8 obreros de la población");
            Steps(wo, rehab);
            var sw = wo.Services.Count == 1 ? wo.Services[0] : null;
            check(sw != null && sw.WorksTrain && sw.Cargo[(int)Res.Personas] == 8 && sw.Cargo[(int)Res.Materiales] == 0, "sale marcado como tren de obras y con 8 obreros a bordo (los materiales los carga el horario)");

            // Chispas: rehabilitar y reparar un 25 % más rápido, fabricar igual
            var wc = World(); wc.Push(new NetCommand { Tick = wc.Tick, Type = NetCmdType.SetChispas, Arg = 1 }); wc.ApplyCommands();
            check(wc.Taller.Chispas && wc.Taller.Faster(36_000) == 28_800 && wc.Taller.Faster(NetConst.Hours(36)) == 43_200, "con Chispas 36 h se quedan en 28,8 h (43.200 ticks)");
            wc.OrderTrain(TallerKind.RehabTren3); Steps(wc, 43_199); bool nb = wc.Services.Count == 0; wc.Step();
            check(nb && wc.Services.Count == 1, "con Chispas el Tren 3 sale a las 28,8 h, no antes");
            wc.OrderTrain(TallerKind.Carga); long day3 = 3L * NetConst.DayTicks;
            Steps(wc, day3 - 1); bool nb2 = wc.Services.Count == 1; wc.Step();
            check(nb2 && wc.Services.Count == 2 && wc.Services[1].Train.Id == 2 && !wc.Services[1].WorksTrain, $"fabricar un tren tarda 3 días ({day3} ticks) aunque esté Chispas: no acelera la fabricación");

            // cola: el taller hace uno a la vez
            var wq = World(); wq.OrderTrain(TallerKind.Carga); wq.OrderTrain(TallerKind.Combate);
            Steps(wq, 1000);
            check(wq.Taller.Queue.Count == 2 && wq.Taller.Queue[0].DoneTicks == 1000 && wq.Taller.Queue[1].DoneTicks == 0, "con dos encargos, trabaja en el primero y el segundo espera");
            Steps(wq, day3 - 1000);
            check(wq.Services.Count == 1 && wq.Taller.Queue.Count == 1 && wq.Taller.Queue[0].DoneTicks == 0, "al acabar el primero, pasa al segundo desde cero");
            Steps(wq, day3);
            check(wq.Services.Count == 2 && wq.Taller.Queue.Count == 0 && wq.Taller.Built == 2, "y a los 6 días salen los dos trenes");

            // máximo de trenes: 4 en la demo, 6 en la etapa 2 (cuentan los que ruedan y los encargados)
            var wm = NetScenario.Mvp();
            check(wm.Services.Count == 2 && wm.OrderTrain(TallerKind.RehabTren3) == TallerResult.Ok && wm.OrderTrain(TallerKind.RehabObras) == TallerResult.Ok, "con 2 trenes se pueden encargar el Tren 3 y el de obras");
            int mm = wm.Stock[Sag(wm), (int)Res.Materiales];
            check(wm.OrderTrain(TallerKind.Carga) == TallerResult.MaximoTrenes && wm.Stock[Sag(wm), (int)Res.Materiales] == mm, "el quinto tren (2 + 2 encargados = 4) se rechaza en la demo, sin cobrar");
            wm.Taller.P.Stage2 = true;
            check(wm.OrderTrain(TallerKind.Carga) == TallerResult.Ok && wm.OrderTrain(TallerKind.Combate) == TallerResult.Ok && wm.OrderTrain(TallerKind.Obras) == TallerResult.MaximoTrenes, "en la etapa 2 el máximo es 6");

            // sin existencias no se cobra nada
            var wf = World(); wf.Stock[Sag(wf), (int)Res.Materiales] = 119;
            check(wf.OrderTrain(TallerKind.Carga) == TallerResult.SinMateriales && S(wf, Res.Combustible) == 1400 && wf.Taller.Queue.Count == 0, "con 119 u de materiales no se encarga un tren nuevo (120)");
            wf.Stock[Sag(wf), (int)Res.Materiales] = 900; wf.Stock[Sag(wf), (int)Res.Combustible] = 29;
            check(wf.OrderTrain(TallerKind.Carga) == TallerResult.SinCombustible && S(wf, Res.Materiales) == 900, "con 29 de combustible tampoco (30)");
            wf.Stock[Sag(wf), (int)Res.Combustible] = 100; wf.Stock[Sag(wf), (int)Res.Personas] = 13;
            check(wf.OrderTrain(TallerKind.RehabObras) == TallerResult.SinObreros && S(wf, Res.Personas) == 13 && S(wf, Res.Materiales) == 900, "la dotación de 8 no deja Sagunto por debajo del retén de 6: con 13 personas no se encarga el tren de obras");
            check(wf.OrderTrain((TallerKind)9) == TallerResult.TipoInvalido, "un tipo que no existe se rechaza");

            // cancelar: sin empezar, exactamente lo cobrado; empezado, la parte que falta al 50 %
            var wx = World(); wx.OrderTrain(TallerKind.RehabObras); wx.OrderTrain(TallerKind.Carga);
            int pp = 170;
            check(wx.CancelTrainOrder(1) == TallerResult.Ok && S(wx, Res.Materiales) == 700 - 20 && S(wx, Res.Combustible) == 1400 - 5 && wx.Taller.Queue.Count == 1, "cancelar un encargo sin empezar devuelve exactamente lo cobrado");
            Steps(wx, 10_800);                                                                 // un 20 % de 54.000
            check(wx.CancelTrainOrder(0) == TallerResult.Ok, "se puede cancelar el que está en marcha");
            long left = 54_000 - 10_800; int expectU = (int)(20L * (left * 500 / 54_000) / 1000), expectF = (int)(5L * (left * 500 / 54_000) / 1000);
            check(S(wx, Res.Materiales) == 700 - 20 + expectU && S(wx, Res.Combustible) == 1400 - 5 + expectF && S(wx, Res.Personas) == pp, $"empezado, devuelve la mitad de lo que falta (+{expectU} u, +{expectF} de combustible) y toda la dotación");
            check(wx.OrderTrain(TallerKind.RehabObras) == TallerResult.Ok && wx.CancelTrainOrder(7) == TallerResult.EncargoInexistente, "un rehabilitado cancelado se puede volver a encargar; un encargo que no existe se rechaza");

            // órdenes: grabadas y repetidas igual; el hash cambia con el encargo y coincide entre mundos
            var ha = World(); var hb = World();
            check(ha.Hash() == hb.Hash(), "dos mundos iguales tienen el mismo hash");
            foreach (var q in new[] { ha, hb }) { q.Push(new NetCommand { Tick = q.Tick, Type = NetCmdType.OrderTrain, Arg = (int)TallerKind.RehabTren3 }); q.ApplyCommands(); Steps(q, 2000); }
            check(ha.Hash() == hb.Hash() && ha.Hash() != World().Hash() && ha.Taller.Queue.Count == 1, "la orden OrderTrain cambia el hash y es la misma en dos mundos");
            string gold = NetGolden.Record(new List<NetCommand>
            {
                new NetCommand { Tick = NetScenario.Mvp().Tick + 100, Type = NetCmdType.SetChispas, Arg = 1 },
                new NetCommand { Tick = NetScenario.Mvp().Tick + 200, Type = NetCmdType.OrderTrain, Arg = (int)TallerKind.RehabTren3 },
                new NetCommand { Tick = NetScenario.Mvp().Tick + 300, Type = NetCmdType.CancelTrainOrder, Arg = 0 },
                new NetCommand { Tick = NetScenario.Mvp().Tick + 400, Type = NetCmdType.OrderTrain, Arg = (int)TallerKind.Carga }
            }, 3000);
            var res = NetGolden.Verify(gold);
            check(res.Ok && gold.Contains(";16;") && gold.Contains(";17;") && gold.Contains(";18;"), $"las órdenes del taller se graban y se reproducen igual: {res}");

            // Chispas acelera también las reparaciones de vía (36.000 obrero·ticks → 28.800: 3.600 ticks con 8 obreros)
            NetWorld Rep(bool chispas, out RailEdge edge)
            {
                var r = NetScenario.Mvp(); r.Tick = NetConst.Hours(8); r.Taller.Chispas = chispas;
                var sv = r.Services[0]; sv.WorksTrain = true; sv.AtStop = true; sv.StopIndex = 0;
                sv.Cargo[(int)Res.Materiales] = 20; sv.Cargo[(int)Res.Personas] = 8;
                int node = sv.Stops[0].Node; edge = null;
                foreach (var e in r.Graph.Edges) if (e.A == node || e.B == node) { edge = e; break; }
                foreach (var n in r.Graph.Nodes) n.HordeDensity = 0;
                r.DestroyEdge(edge.Id);
                return r;
            }
            var rn = Rep(false, out var en); rn.RepairEdge(en.Id, 0); int tn = 0; while (en.Debris && tn < 20_000) { rn.Step(); tn++; }
            var rc = Rep(true, out var ec); rc.RepairEdge(ec.Id, 0); int tc = 0; while (ec.Debris && tc < 20_000) { rc.Step(); tc++; }
            check(tn == 4_500 && tc == 3_600, $"sin Chispas reparar un tramo tarda 3 h (4.500 ticks) y con Chispas 2,4 h (3.600): {tn} y {tc}");
        }
    }
}
