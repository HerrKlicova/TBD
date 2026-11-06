using System;
using System.Collections.Generic;
using System.Linq;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Hito 5, paso 5: el taller de Sagunto (cola de encargos, plazos, costes, dotación, máximo de trenes, Chispas, cancelar, avisos y hash).</summary>
    static class TallerTests
    {
        static NetWorld World(bool chispas = false)
        {
            var w = NetScenario.Mvp(false); w.Services.Clear();            // sin trenes del escenario: no tocan las existencias de Sagunto
            if (!chispas) w.Taller.ChispasAt = -2;                    // sin Chispas: plazos del CSV tal cual
            return w;
        }
        static int Sag(NetWorld w) { return w.Taller.NodeIndex; }
        static int S(NetWorld w, Res r) { return w.Stock[Sag(w), (int)r]; }
        static void Steps(NetWorld w, long n) { for (long i = 0; i < n; i++) w.Step(); }
        static int Count(NetWorld w, string id) { return w.Intel.Count(e => e.Id == id); }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[24] El taller fabrica y rehabilita trenes (hito 5, paso 5)");
            var d = NetScenario.Mvp(false);
            check(d.Taller.NodeIndex >= 0 && d.Taller.P.Node == "Sagunto" && d.Taller.Hash() == 0, "el taller está en Sagunto y, sin encargos y con Chispas en el taller, su hash vale 0 (los goldens no se mueven)");
            var p = d.Taller.P;
            check(p.TrainU == 120 && p.TrainFuel == 30 && p.TrainCrew == 10 && p.Tren3U == 60 && p.Tren3Fuel == 15 && p.ObrasU == 20 && p.ObrasFuel == 5 && p.ObrasCrew == 8 && p.TrainDays == 3 && p.RehabHours == 36 && p.MaxTrains == 4 && p.MaxTrainsStage2 == 6,
                "los costes y plazos salen del CSV de Hugo: tren 120 u + 30 y 10 de tripulación, Tren 3 60 + 15, tren de obras 20 + 5 con 8 obreros, 3 días, rehabilitar 36 h, máximo 4 (demo) y 6 (etapa 2)");
            check(d.Taller.ChispasAt == -1 && !d.Taller.ObrasOut, "Chispas empieza en el taller y no puede salir hasta que rueda el tren de obras");

            // rehabilitar el Tren 3 (sin Chispas: 36 h)
            var w = World(); int m0 = S(w, Res.Materiales), f0 = S(w, Res.Combustible), p0 = S(w, Res.Personas);
            check(w.OrderTrain(TallerKind.RehabTren3) == TallerResult.Ok && S(w, Res.Materiales) == m0 - 60 && S(w, Res.Combustible) == f0 - 15 && S(w, Res.Personas) == p0 - 10 && w.Taller.Queue.Count == 1, "rehabilitar el Tren 3 cuesta 60 u, 15 de combustible y 10 de tripulación (es un tren de servicio como los nuevos)");
            check(w.OrderTrain(TallerKind.RehabTren3) == TallerResult.YaRehabilitado, "el Tren 3 solo se rehabilita una vez");
            long rehab = NetConst.Hours(36);
            Steps(w, rehab - 1); bool before = w.Services.Count == 0;
            w.Step();
            var t3 = w.Services.Count == 1 ? w.Services[0] : null;
            check(before && t3 != null && t3.Train.Id == 1 && !t3.WorksTrain && t3.Stops.Count == 1 && t3.Stops[0].Node == Sag(w) && t3.AtStop && w.Taller.Queue.Count == 0, $"a las 36 h (1,5 días, {rehab} ticks) sale el tren, en Sagunto y con una sola parada (sin horario no se mueve)");
            check(Count(w, "TL-1") == 1 && w.Intel.First(e => e.Id == "TL-1").Vars[1] == t3.Train.Name, "al salir avisa (TL-1: «El {tren} está listo»)");

            // tren de obras: los 8 obreros se descuentan al SALIR, no al encargar
            var wo = World(); int po = S(wo, Res.Personas), mo = S(wo, Res.Materiales);
            check(wo.OrderTrain(TallerKind.RehabObras) == TallerResult.Ok && S(wo, Res.Materiales) == mo - 20 && S(wo, Res.Combustible) == 1400 - 5 && S(wo, Res.Personas) == po, "rehabilitar el tren de obras cuesta 20 u y 5 de combustible; al encargar no se descuenta gente");
            Steps(wo, rehab - 1);
            check(S(wo, Res.Personas) == po && wo.Services.Count == 0, "hasta que sale el tren no se descuenta a nadie");
            wo.Step();
            var sw = wo.Services.Count == 1 ? wo.Services[0] : null;
            check(sw != null && sw.WorksTrain && sw.Cargo[(int)Res.Personas] == 8 && sw.Cargo[(int)Res.Materiales] == 0 && S(wo, Res.Personas) == po - 8 && wo.Taller.ObrasOut, "al salir se descuentan los 8 obreros de la población y viajan a bordo; el tren de obras queda marcado y Chispas ya puede salir");

            // sin dotación libre al salir: el tren espera en el taller (TL-2) y sale en cuanto los hay
            var wd = World(); wd.OrderTrain(TallerKind.RehabObras);
            Steps(wd, rehab - 5); wd.Stock[Sag(wd), (int)Res.Personas] = wd.Works.P.GarrisonMin + 3;       // solo 3 libres por encima del retén
            Steps(wd, 10);
            check(wd.Services.Count == 0 && wd.Taller.Queue.Count == 1 && wd.Taller.Queue[0].Waiting && Count(wd, "TL-2") == 1 && wd.Intel.First(e => e.Id == "TL-2").Vars[3] == "5", "con 3 libres el tren acabado espera en el taller y avisa una sola vez (TL-2: faltan 5)");
            Steps(wd, 500);
            check(Count(wd, "TL-2") == 1 && wd.Services.Count == 0, "esperando no repite el aviso");
            wd.Stock[Sag(wd), (int)Res.Personas] = 300; wd.Step();
            check(wd.Services.Count == 1 && wd.Services[0].WorksTrain && wd.Services[0].Cargo[(int)Res.Personas] == 8 && S(wd, Res.Personas) == 292 && wd.Taller.Queue.Count == 0 && Count(wd, "TL-1") == 1, "en cuanto hay 8 libres sale el tren con su dotación");

            // Chispas en el taller: rehabilitar un 25 % más rápido; fabricar igual
            var wc = World(true);
            wc.OrderTrain(TallerKind.RehabTren3); Steps(wc, 43_199); bool nb = wc.Services.Count == 0; wc.Step();
            check(nb && wc.Services.Count == 1, "con Chispas en el taller el Tren 3 sale a las 28,8 h (43.200 ticks), no antes");
            wc.OrderTrain(TallerKind.Carga); long day3 = 3L * NetConst.DayTicks;
            int peopleBefore = S(wc, Res.Personas);
            Steps(wc, day3 - 1); bool nb2 = wc.Services.Count == 1; wc.Step();
            check(nb2 && wc.Services.Count == 2 && wc.Services[1].Train.Id == 2 && !wc.Services[1].WorksTrain, $"fabricar un tren tarda 3 días ({day3} ticks) aunque Chispas esté en el taller: no acelera la fabricación");
            check(peopleBefore == 170 - 10 - 10, "un tren nuevo toma 10 de tripulación de la población al encargarse (el Tren 3 ya había tomado otros 10)");

            // Chispas fuera del taller: no acelera las rehabilitaciones
            var wl = World(true); wl.Taller.ChispasAt = 0;              // en un tren (índice 0)
            wl.Services.Add(new TrainService { Train = new RailTrain { Id = 9, Name = "9", SpeedMmPerTick = NetConst.TrainMmPerTick } });
            wl.OrderTrain(TallerKind.RehabTren3); Steps(wl, rehab - 1); bool nl = wl.Services.Count == 1; wl.Step();
            check(nl && wl.Services.Count == 2, "con Chispas en un tren (no en el taller) la rehabilitación tarda las 36 h completas");

            // mover a Chispas: bloqueado hasta que ha rodado el tren de obras; solo con los trenes parados en el taller
            var wm0 = World(true); wm0.Services.Add(new TrainService { Train = new RailTrain { Id = 1, Name = "1", SpeedMmPerTick = NetConst.TrainMmPerTick } });
            check(wm0.MoveChispas(1) == TallerResult.ChispasBloqueado && wm0.Taller.ChispasAt == -1, "antes de que ruede el tren de obras Chispas no puede salir del taller");
            check(wm0.MoveChispas(0) == TallerResult.Ok && wm0.MoveChispas(5) == TallerResult.TrenLejos, "quedarse en el taller no cambia nada; un tren que no existe se rechaza");
            var wx0 = World(true); wx0.OrderTrain(TallerKind.RehabObras); Steps(wx0, 43_200);
            var works = wx0.Services[0]; works.AtStop = false;           // en marcha
            check(wx0.Taller.ObrasOut && wx0.MoveChispas(1) == TallerResult.TrenLejos && wx0.Taller.ChispasAt == -1, "con el tren de obras en marcha Chispas no sube");
            works.AtStop = true;
            check(wx0.MoveChispas(1) == TallerResult.Ok && wx0.Taller.ChispasAt == 0, "con el tren de obras parado en el taller Chispas sube a él");
            works.AtStop = false;
            check(wx0.MoveChispas(0) == TallerResult.TrenLejos && wx0.Taller.ChispasAt == 0, "y solo baja al taller con ese tren parado en él");
            var wnone = World(false);
            check(wnone.MoveChispas(0) == TallerResult.ChispasNoEsta, "si Chispas no está en la partida, no se mueve");

            // cola: el taller hace uno a la vez
            var wq = World(); wq.OrderTrain(TallerKind.Carga); wq.OrderTrain(TallerKind.Combate);
            Steps(wq, 1000);
            check(wq.Taller.Queue.Count == 2 && wq.Taller.Queue[0].DoneTicks == 1000 && wq.Taller.Queue[1].DoneTicks == 0, "con dos encargos, trabaja en el primero y el segundo espera");
            Steps(wq, day3 - 1000);
            check(wq.Services.Count == 1 && wq.Taller.Queue.Count == 1 && wq.Taller.Queue[0].DoneTicks == 0, "al acabar el primero, pasa al segundo desde cero");
            Steps(wq, day3);
            check(wq.Services.Count == 2 && wq.Taller.Queue.Count == 0 && wq.Taller.Built == 2, "y a los 6 días salen los dos trenes");

            // máximo de trenes: 4 en la demo, 6 en la etapa 2 (cuentan los que ruedan y los encargados)
            var wm = NetScenario.Mvp(false); wm.Taller.ChispasAt = -2;
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
            wf.Stock[Sag(wf), (int)Res.Combustible] = 100; wf.Stock[Sag(wf), (int)Res.Personas] = 15;
            check(wf.OrderTrain(TallerKind.Carga) == TallerResult.SinObreros && S(wf, Res.Personas) == 15 && S(wf, Res.Materiales) == 900, "la tripulación de 10 no deja Sagunto por debajo del retén de 6: con 15 personas no se encarga un tren nuevo");
            wf.Stock[Sag(wf), (int)Res.Personas] = 13;
            check(wf.OrderTrain(TallerKind.RehabObras) == TallerResult.SinObreros && S(wf, Res.Personas) == 13, "el tren de obras comprueba que haya 8 libres por encima del retén (con 13, solo 7): no se encarga");
            wf.Stock[Sag(wf), (int)Res.Personas] = 14;
            check(wf.OrderTrain(TallerKind.RehabObras) == TallerResult.Ok && S(wf, Res.Personas) == 14, "con 14 sí, y no se descuenta nada al encargar");
            check(wf.OrderTrain((TallerKind)9) == TallerResult.TipoInvalido, "un tipo que no existe se rechaza");

            // cancelar: sin empezar, el 100 % de lo pagado; empezado, floor(50 % de lo pagado) (D-N59, igual que las obras)
            var wx = World(); wx.OrderTrain(TallerKind.RehabObras); wx.OrderTrain(TallerKind.Carga);
            check(wx.CancelTrainOrder(1) == TallerResult.Ok && S(wx, Res.Materiales) == 700 - 20 && S(wx, Res.Combustible) == 1400 - 5 && S(wx, Res.Personas) == 170 && wx.Taller.Queue.Count == 1, "cancelar un encargo sin empezar devuelve el 100 %, tripulación incluida");
            check(Count(wx, "TL-3") == 0, "sin empezar no hay aviso de recuperación");
            Steps(wx, 10_800);
            check(wx.CancelTrainOrder(0) == TallerResult.Ok, "se puede cancelar el que está en marcha");
            check(S(wx, Res.Materiales) == 700 - 20 + 10 && S(wx, Res.Combustible) == 1400 - 5 + 2 && S(wx, Res.Personas) == 170, "empezado, devuelve floor(50 % de lo pagado): +10 u de 20 y +2 de 5 de combustible (no la mitad de lo que falta)");
            check(Count(wx, "TL-3") == 1 && wx.Intel.First(e => e.Id == "TL-3").Vars[3] == "10", "al cancelar con la obra en marcha avisa (TL-3: recuperamos 10 u)");
            check(wx.OrderTrain(TallerKind.RehabObras) == TallerResult.Ok && wx.CancelTrainOrder(7) == TallerResult.EncargoInexistente, "un rehabilitado cancelado se puede volver a encargar; un encargo que no existe se rechaza");
            var wy = World(); wy.OrderTrain(TallerKind.Carga); Steps(wy, 1000); int pm = S(wy, Res.Materiales);
            wy.CancelTrainOrder(0);
            check(S(wy, Res.Materiales) == pm + 60 && S(wy, Res.Personas) == 170, "un tren nuevo empezado devuelve 60 de 120 u y la tripulación entera de 10");
            var wz = World(); wz.OrderTrain(TallerKind.RehabTren3); wz.OrderTrain(TallerKind.Obras);
            check(S(wz, Res.Personas) == 160, "el Tren 3 toma 10 de tripulación y un tren de obras (nuevo) no toma ninguna al encargarse: sus 8 obreros son su tripulación (Hugo)");
            wz.CancelTrainOrder(0);
            check(S(wz, Res.Personas) == 170, "cancelar el Tren 3 sin empezar devuelve sus 10 de tripulación");

            // órdenes: grabadas y repetidas igual; el hash cambia con el encargo y coincide entre mundos
            var ha = World(); var hb = World();
            check(ha.Hash() == hb.Hash(), "dos mundos iguales tienen el mismo hash");
            foreach (var q in new[] { ha, hb }) { q.Push(new NetCommand { Tick = q.Tick, Type = NetCmdType.OrderTrain, Arg = (int)TallerKind.RehabTren3 }); q.ApplyCommands(); Steps(q, 2000); }
            check(ha.Hash() == hb.Hash() && ha.Hash() != World().Hash() && ha.Taller.Queue.Count == 1, "la orden OrderTrain cambia el hash y es la misma en dos mundos");
            var hc = World(true); var hdd = World(true); hdd.Taller.ChispasAt = 3;
            check(hc.Hash() != hdd.Hash(), "dónde está Chispas entra en el hash");
            string gold = NetGolden.Record(new List<NetCommand>
            {
                new NetCommand { Tick = NetScenario.Mvp(false).Tick + 100, Type = NetCmdType.OrderTrain, Arg = (int)TallerKind.RehabTren3 },
                new NetCommand { Tick = NetScenario.Mvp(false).Tick + 200, Type = NetCmdType.SetChispas, Arg = 1 },
                new NetCommand { Tick = NetScenario.Mvp(false).Tick + 300, Type = NetCmdType.CancelTrainOrder, Arg = 0 },
                new NetCommand { Tick = NetScenario.Mvp(false).Tick + 400, Type = NetCmdType.OrderTrain, Arg = (int)TallerKind.Carga }
            }, 3000);
            var res = NetGolden.Verify(gold);
            check(res.Ok && gold.Contains(";16;") && gold.Contains(";17;") && gold.Contains(";18;"), $"las órdenes del taller se graban y se reproducen igual: {res}");

            // Chispas a bordo del tren de obras acelera las reparaciones de vía (36.000 obrero·ticks → 28.800: 3.600 ticks con 8 obreros)
            NetWorld Rep(bool onTrain, out RailEdge edge)
            {
                var r = NetScenario.Mvp(false); r.Tick = NetConst.Hours(8); r.Taller.ChispasAt = onTrain ? 0 : -1;
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
            check(tn == 4_500 && tc == 3_600, $"con Chispas en el taller reparar un tramo tarda 3 h (4.500 ticks) y con Chispas a bordo del tren de obras 2,4 h (3.600): {tn} y {tc}");
        }
    }
}
