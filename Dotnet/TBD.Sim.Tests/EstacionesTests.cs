using System;
using System.Collections.Generic;
using System.IO;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Hito 5, fundar una estación (modelo del flag): requisitos, efectos, nivel y hash.</summary>
    static class EstacionesTests
    {
        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[18] Fundar una estación (hito 5): emplazamientos, requisitos, nivel y hash");

            var w0 = NetScenario.Mvp(false);
            int nules = -1; foreach (var n in w0.Graph.Nodes) if (n.Name == "Nules") nules = n.Id;
            bool anyFounded = false; foreach (var f in w0.Stations.Founded) if (f) anyFounded = true;
            var sp = StationParams.FromCsv(new CsvTable(File.ReadAllText(Path.Combine(BalanceTests.DataDirPublic(), "balance_economia.csv"))));
            check(nules >= 0 && w0.Stations.IsSite(nules) && w0.Stations.P.Sites.Length == 2 && w0.Stations.P.Sites[1] == "Castellón" && !anyFounded && w0.StationKindOf(nules) == StationKind.NoFundada,
                "los emplazamientos de la demo son Nules y Castellón (infestada, se reclama igual: Mak, 7-nov) y empiezan sin fundar");
            check(sp.FoundMaterialsU == 30 && sp.FoundPeople == 12 && w0.Stations.P.FoundMaterialsU == 30 && w0.Stations.P.FoundPeople == 12 && w0.Stations.P.StationPeople == 40,
                $"fundar cuesta 30 u y 12 personas (fundar_materiales y guarnicion_minima del CSV de Hugo); para estación, 40 personas");
            check(w0.Stations.Hash() == 0, "sin fundar nada el hash de las estaciones es 0: G1–G5 no se mueven");

            // montaje: un tren parado en una parada de su horario, con la carga de fundar; el emplazamiento es ese nodo (la lista es configurable)
            NetWorld Setup(out TrainService sv, out int site, int mats = 30, int people = 12)
            {
                var w = NetScenario.Mvp(false);
                sv = w.Services[0];
                site = sv.Stops[0].Node;
                w.Stations.P.Sites = new[] { w.Graph.Nodes[site].Name };
                sv.AtStop = true; sv.StopIndex = 0;
                sv.Cargo[(int)Res.Materiales] = mats; sv.Cargo[(int)Res.Personas] = people;
                w.Graph.Nodes[site].HordeDensity = 0;
                return w;
            }

            // rechazos: no cambian nada
            var w1 = Setup(out var sv1, out int s1);
            ulong h1 = w1.Hash(); int pop1 = w1.Stock[s1, (int)Res.Personas];
            int other = -1; foreach (var n in w1.Graph.Nodes) if (n.Id != s1) { other = n.Id; break; }
            check(w1.FoundStation(other) == FoundResult.NoEsEmplazamiento, "un nodo que no es emplazamiento no se funda (ciudades y apeaderos existentes)");
            var w2 = Setup(out var sv2, out int s2, mats: 29); check(w2.FoundStation(s2) == FoundResult.SinCarga, "con 29 u de materiales no se funda");
            var w3 = Setup(out var sv3, out int s3, people: 11); check(w3.FoundStation(s3) == FoundResult.SinCarga, "con 11 personas a bordo no se funda");
            var w4 = Setup(out var sv4, out int s4); sv4.AtStop = false; sv4.Train.State = RailTrainState.EnMarcha;
            check(w4.FoundStation(s4) == FoundResult.SinTren, "sin un tren parado en el emplazamiento no se funda");
            var w5 = Setup(out var sv5, out int s5); w5.Graph.Nodes[s5].HordeDensity = NetWorld.SiegeAt;
            check(w5.FoundStation(s5) == FoundResult.HordeEncima && !w5.Stations.Founded[s5] && sv5.Cargo[(int)Res.Materiales] == 30, "con la horda encima (densidad de asedio) no se funda y no se gasta nada");
            check(w1.Hash() == h1 && w1.FoundStation(other) == FoundResult.NoEsEmplazamiento && w1.Stock[s1, (int)Res.Personas] == pop1, "un rechazo no cambia el hash");

            // éxito: la carga se descarga (30 u gastadas, 12 personas a la población del nodo)
            int popBefore = w1.Stock[s1, (int)Res.Personas];
            check(w1.FoundStation(s1) == FoundResult.Ok && w1.Stations.Founded[s1] && sv1.Cargo[(int)Res.Materiales] == 0 && sv1.Cargo[(int)Res.Personas] == 0 && w1.Stock[s1, (int)Res.Personas] == popBefore + 12,
                "fundar: 30 u gastadas, 12 personas a la población del nodo y el nodo queda fundado");
            check(w1.FoundStation(s1) == FoundResult.YaFundada && w1.StationKindOf(s1) == StationKind.Apeadero && w1.Hash() != h1, "una estación fundada no se funda otra vez; es un apeadero y el hash cambia");

            // un tren detenido cerca del nodo en su tramo también vale (dentro del radio)
            var wr = NetScenario.Mvp(false); var edge = wr.Graph.Edges[0]; int rsite = edge.B;
            wr.Stations.P.Sites = new[] { wr.Graph.Nodes[rsite].Name };
            var rs = wr.Services[0]; rs.AtStop = false; rs.Train.Route = new List<int> { edge.A, edge.B }; rs.Train.Leg = 0; rs.Train.State = RailTrainState.Parado;
            rs.Cargo[(int)Res.Materiales] = 30; rs.Cargo[(int)Res.Personas] = 12; wr.Graph.Nodes[rsite].HordeDensity = 0;
            rs.Train.OffsetMm = edge.LengthMm - 6_000; var far = wr.FoundStation(rsite);
            rs.Train.OffsetMm = edge.LengthMm - 3_000; var near = wr.FoundStation(rsite);
            check(far == FoundResult.SinTren && near == FoundResult.Ok, $"un tren parado a 6 m del nodo no vale y a 3 m sí (radio {wr.Stations.P.StopRadiusMm} mm)");

            // nivel: estación = fundada + un depósito construido + 40 personas
            var wl = Setup(out var svl, out int sl); wl.FoundStation(sl);
            wl.Stock[sl, (int)Res.Personas] = 100; wl.Stock[sl, (int)Res.Materiales] = 100;
            check(wl.StationKindOf(sl) == StationKind.Apeadero, "sin depósito, aunque haya gente, sigue siendo un apeadero");
            wl.Works.Add(sl, wl.Works.Preview(sl, PieceKind.Deposito, new[] { 0, 6000 }, 100));
            check(wl.StationKindOf(sl) == StationKind.Apeadero, "un depósito sin terminar tampoco sube de nivel");
            wl.Works.CompleteAll(sl);
            wl.Stock[sl, (int)Res.Personas] = 39; var k39 = wl.StationKindOf(sl);
            wl.Stock[sl, (int)Res.Personas] = 40; var k40 = wl.StationKindOf(sl);
            check(k39 == StationKind.Apeadero && k40 == StationKind.Estacion, "con el depósito construido, 39 personas son un apeadero y 40 una estación");

            // determinismo: la misma orden en dos mundos da el mismo hash 300 ticks después
            var da = Setup(out _, out int sa); var db = Setup(out _, out int sb);
            foreach (var q in new[] { da, db }) { q.Push(new NetCommand { Tick = q.Tick, Type = NetCmdType.FoundStation, Arg = q.Stations.P.Sites.Length > 0 ? Array.FindIndex(q.Graph.Nodes.ToArray(), n => n.Name == q.Stations.P.Sites[0]) : 0 }); q.ApplyCommands(); }
            for (int i = 0; i < 300; i++) { da.Step(); db.Step(); }
            check(da.Stations.Founded[sa] && da.Hash() == db.Hash(), "la orden FoundStation, aplicada en dos mundos, da el mismo hash 300 ticks después");

            // «detener en el emplazamiento» (Mak, 6-nov): el tren para justo en el nodo al llegar y, con la carga, se funda
            var wh = NetScenario.Mvp(false); int hs = -1;
            for (int i = 0; i < wh.Services.Count && hs < 0; i++)
                for (int k = 0; k < wh.Services[i].Stops.Count && hs < 0; k++)
                {
                    var rt = wh.Graph.ShortestPath(wh.Services[i].Stops[k].Node, wh.Services[i].Stops[(k + 1) % wh.Services[i].Stops.Count].Node);
                    if (rt.Contains(nules)) hs = i;
                }
            check(hs >= 0, "algún tren del horario pasa por Nules (si no, no se podría parar ahí)");
            var svh = wh.Services[hs];
            wh.Push(new NetCommand { Tick = wh.Tick, Type = NetCmdType.HaltAtSite, Arg = nules, Arg2 = hs }); wh.ApplyCommands();
            check(svh.HaltAt == nules, "la orden anota el emplazamiento donde debe parar el tren");
            int guard = 0; while (!svh.Held && guard++ < 3 * NetConst.DayTicks) wh.Step();
            check(svh.Held && svh.Train.State == RailTrainState.Parado && svh.Train.Route[svh.Train.Leg] == nules && svh.Train.OffsetMm == 0 && svh.HaltAt == -1 && wh.TrainAtNode(svh, nules),
                $"el tren para justo en Nules (en {guard} ticks, a {svh.Train.OffsetMm} mm del nodo) y queda retenido");
            svh.Cargo[(int)Res.Materiales] = 30; svh.Cargo[(int)Res.Personas] = 12; wh.Graph.Nodes[nules].HordeDensity = 0;
            check(wh.FoundStation(nules) == FoundResult.Ok && wh.StationKindOf(nules) == StationKind.Apeadero, "con la carga, desde el tren parado en Nules se funda la estación del día 3");
            wh.Push(new NetCommand { Tick = wh.Tick, Type = NetCmdType.TrainGo, Arg = hs }); wh.ApplyCommands();
            for (int i = 0; i < 200; i++) wh.Step();
            check(!svh.Held && svh.Train.State == RailTrainState.EnMarcha && (svh.Train.Leg > 0 || svh.Train.OffsetMm > 0), "TrainGo lo suelta y sigue su ruta");
            var wn = NetScenario.Mvp(false); ulong hBase = wn.Hash();
            wn.Push(new NetCommand { Tick = wn.Tick, Type = NetCmdType.HaltAtSite, Arg = wn.Graph.Nodes[0].Id, Arg2 = 0 }); wn.ApplyCommands();
            check(wn.Services[0].HaltAt == -1 && wn.Hash() == hBase, "detener en un nodo que no es emplazamiento se ignora");
            wn.Push(new NetCommand { Tick = wn.Tick, Type = NetCmdType.HaltAtSite, Arg = nules, Arg2 = 0 }); wn.ApplyCommands();
            bool pending = wn.Services[0].HaltAt == nules && wn.Hash() != hBase;
            wn.Push(new NetCommand { Tick = wn.Tick, Type = NetCmdType.HaltAtSite, Arg = -1, Arg2 = 0 }); wn.ApplyCommands();
            check(pending && wn.Services[0].HaltAt == -1 && wn.Hash() == hBase, "la parada pendiente entra en el hash mientras está, y la orden con nodo −1 la cancela");

            // un emplazamiento sin fundar no cuenta para el peso del director ni para la huella (Mak, 6-nov)
            var wp = NetScenario.Mvp(false);
            check(wp.Director.SiegeWeight(wp, nules) == 0 && wp.Director.SiegeTargetWeight(wp, nules) == 0, "sin fundar, Nules pesa 0 como objetivo y para la deriva");
            int picks = 0; for (int k = 0; k < 4000; k++) { wp.Tick = 40 * NetConst.DayTicks + k * 37; if (wp.Director.DrawSiegeTarget(wp) == nules) picks++; }
            check(picks == 0, "el director no sortea un emplazamiento sin fundar como objetivo de asedio (0 de 4.000)");
            long hu0 = wp.Director.HuellaMil(wp); wp.Stock[nules, (int)Res.Personas] = 50;
            check(wp.Director.HuellaMil(wp) == hu0, "la gente de un emplazamiento sin fundar no suma a la huella");
            wp.Stations.Founded[nules] = true;
            check(wp.Director.SiegeWeight(wp, nules) > 0 && wp.Director.HuellaMil(wp) > hu0, "una vez fundado, pesa y suma a la huella como cualquier nodo");

            // la orden con Nules sin tren se rechaza, y la repetición dorada con esa orden sigue idéntica
            string gold = NetGolden.Record(new List<NetCommand> { new NetCommand { Tick = NetScenario.Mvp(false).Tick + 100, Type = NetCmdType.FoundStation, Arg = nules } }, 1500);
            var res = NetGolden.Verify(gold);
            check(res.Ok && gold.Contains(";12;" + nules + ";0\n"), $"la orden FoundStation se graba y se reproduce igual: {res}");

            RunReparar(check);
        }

        static void RunReparar(Action<bool, string> check)
        {
            Console.WriteLine("[20] Reparar un tramo cortado por escombros con el tren de obras (hito 5, paso 4)");
            // montaje: el tren 0, marcado como tren de obras, parado en su primera parada; el tramo es uno que sale de ese nodo
            NetWorld Setup(out TrainService sv, out RailEdge edge, int mats = 20, int people = 8)
            {
                var w = NetScenario.Mvp(false); w.Tick = NetConst.Hours(8);                 // en horario de obra (06:00–18:00)
                sv = w.Services[0]; sv.WorksTrain = true; sv.AtStop = true; sv.StopIndex = 0;
                sv.Cargo[(int)Res.Materiales] = mats; sv.Cargo[(int)Res.Personas] = people;
                int node = sv.Stops[0].Node; edge = null;
                foreach (var e in w.Graph.Edges) if (e.A == node || e.B == node) { edge = e; break; }
                foreach (var n in w.Graph.Nodes) n.HordeDensity = 0;
                w.DestroyEdge(edge.Id);
                return w;
            }

            var w0 = Setup(out var sv0, out var e0);
            check(e0.Debris && e0.State == EdgeState.Cortada && w0.Works.P.RepairU == 5 && w0.Works.P.WorksCrew == 8 && w0.Works.P.RepairNeedTicks == 36_000, "un tramo en escombros está cortado; reparar cuesta 5 u y 3 h con 8 obreros (36.000 obrero·ticks)");
            for (int i = 0; i < 6000; i++) w0.Step();
            check(e0.Debris && e0.State == EdgeState.Cortada, "los escombros no se despejan solos (los cortes por densidad sí)");

            // rechazos: no cambian nada
            var w1 = Setup(out var sv1, out var e1);
            check(w1.RepairEdge(e1.Id + 100, 0) == RepairResult.SinEscombros && w1.RepairEdge(-1, 0) == RepairResult.SinEscombros, "un tramo que no existe no se repara");
            sv1.WorksTrain = false;
            check(w1.RepairEdge(e1.Id, 0) == RepairResult.SinTrenDeObras && w1.RepairEdge(e1.Id, 99) == RepairResult.SinTrenDeObras, "sin tren de obras (marcador) no se repara");
            sv1.WorksTrain = true; sv1.Cargo[(int)Res.Materiales] = 4;
            check(w1.RepairEdge(e1.Id, 0) == RepairResult.SinCarga, "con 4 u de materiales no se repara");
            sv1.Cargo[(int)Res.Materiales] = 20; sv1.Cargo[(int)Res.Personas] = 7;
            check(w1.RepairEdge(e1.Id, 0) == RepairResult.SinCarga, "con 7 obreros a bordo no se repara");
            sv1.Cargo[(int)Res.Personas] = 8; sv1.AtStop = false; sv1.Train.State = RailTrainState.EnMarcha;
            check(w1.RepairEdge(e1.Id, 0) == RepairResult.TrenLejos, "un tren en marcha o lejos del tramo no repara");
            sv1.AtStop = true; sv1.Train.State = RailTrainState.Parado; w1.Graph.Nodes[e1.A].HordeDensity = 600;
            check(w1.RepairEdge(e1.Id, 0) == RepairResult.HordeEncima, "con la horda encima no se repara");
            w1.Graph.Nodes[e1.A].HordeDensity = 0;
            check(sv1.Cargo[(int)Res.Materiales] == 20 && e1.RepairSvc == -1, "los rechazos no gastan materiales ni empiezan la obra");

            // la reparación: 5 u de la carga, el tren retenido, 3 h (4.500 ticks) con 8 obreros
            var w2 = Setup(out var sv2, out var e2); ulong hBefore = w2.Hash();
            check(w2.RepairEdge(e2.Id, 0) == RepairResult.Ok && sv2.Cargo[(int)Res.Materiales] == 15 && sv2.Held && e2.RepairSvc == 0 && w2.Hash() != hBefore, "la orden gasta 5 u de la carga, retiene el tren y cambia el hash");
            check(w2.RepairEdge(e2.Id, 0) == RepairResult.YaEnObra, "no se puede empezar dos veces");
            int t2 = 0; while (e2.Debris && t2 < 20_000) { w2.Step(); t2++; }
            check(!e2.Debris && e2.State == EdgeState.Ok && t2 == 4_500 && !sv2.Held && sv2.Cargo[(int)Res.Personas] == 8, $"8 obreros reparan el tramo en 3 h (4.500 ticks): {t2}; el tramo vuelve a existir y el tren se libera");

            // menos obreros, más lenta
            var w3 = Setup(out var sv3, out var e3, 20, 16);
            w3.RepairEdge(e3.Id, 0); int t3 = 0; while (e3.Debris && t3 < 20_000) { w3.Step(); t3++; }
            check(t3 == 4_500, $"el máximo son 8 obreros: con 16 a bordo tarda lo mismo ({t3})");
            var w4 = Setup(out var sv4, out var e4, 20, 8);
            w4.RepairEdge(e4.Id, 0); for (int i = 0; i < 2250; i++) w4.Step();
            sv4.Cargo[(int)Res.Personas] = 4;                                           // la mitad de los obreros muere
            int t4 = 2250; while (e4.Debris && t4 < 20_000) { w4.Step(); t4++; }
            check(t4 == 2250 + 4500, $"si mueren obreros va más lenta: 2.250 ticks con 8 y el resto con 4 = {t4} (6.750)");

            // si el tren se va, la obra espera
            var w5 = Setup(out var sv5, out var e5);
            w5.RepairEdge(e5.Id, 0); for (int i = 0; i < 100; i++) w5.Step();
            long done5 = e5.RepairDone; sv5.AtStop = false; sv5.Train.State = RailTrainState.EnMarcha;
            sv5.Held = true; sv5.Train.Route = new List<int> { e5.A, e5.B }; sv5.Train.Leg = 0; sv5.Train.OffsetMm = e5.LengthMm / 2;
            for (int i = 0; i < 50; i++) w5.Step();
            check(done5 == 800 && e5.RepairDone == done5 && e5.Debris, $"si el tren sale de posición la obra se para y espera ({e5.RepairDone} obrero·ticks)");

            // el puente: 80 u y 192 obrero·h = 36.000 ticks con 8 obreros
            var w6 = Setup(out var sv6, out var e6, 79, 8); w6.DestroyEdge(e6.Id, true);
            check(w6.RepairEdge(e6.Id, 0) == RepairResult.SinCarga, "el puente pide 80 u (puente_jiloca_materiales): con 79 no se empieza");
            sv6.Cargo[(int)Res.Materiales] = 80;
            check(w6.RepairEdge(e6.Id, 0) == RepairResult.Ok && sv6.Cargo[(int)Res.Materiales] == 0, "con 80 u se empieza y se pagan todas");
            int t6 = 0; while (e6.Debris && t6 < 100_000) { w6.Step(); t6++; }
            check(w6.Works.P.BridgeU == 80 && w6.Works.P.BridgeObreroH == 192 && t6 == 72_000 && !e6.Debris, $"el puente son 192 obrero·h: con 8 obreros trabajando de 06:00 a 18:00, 2 días (72.000 ticks): {t6}");

            // determinismo: dos mundos con la misma reparación dan el mismo hash en cada punto
            var wa = Setup(out var sva, out var ea); var wb = Setup(out var svb, out var eb);
            wa.RepairEdge(ea.Id, 0); wb.RepairEdge(eb.Id, 0);
            bool same = true; for (int i = 0; i < 3000; i++) { wa.Step(); wb.Step(); if (wa.Hash() != wb.Hash()) same = false; }
            check(same, "dos mundos con la misma reparación dan el mismo hash en 3.000 ticks");

            // la orden por la cola de órdenes se graba y se reproduce igual; los cortes de densidad (sin escombros) no cambian nada
            string gold = NetGolden.Record(new List<NetCommand> { new NetCommand { Tick = NetScenario.Mvp(false).Tick + 100, Type = NetCmdType.RepairEdge, Arg = 0, Arg2 = 0 } }, 1500);
            var res = NetGolden.Verify(gold);
            check(res.Ok && gold.Contains(";14;0;0"), $"la orden RepairEdge se graba y se reproduce igual (sin escombros no hace nada): {res}");
            var wn = NetScenario.Mvp(false); ulong hn = wn.Hash(); wn.Graph.Edges[0].State = EdgeState.Cortada;
            for (int i = 0; i < 4000; i++) wn.Step();
            check(!wn.Graph.Edges[0].Debris && wn.Graph.Edges[0].State == EdgeState.Ok, "un corte de densidad sin escombros sigue despejándose solo");

            // el director marca escombros en sus cortes (TR-4)
            var wd = NetScenario.Mvp(false); wd.Tick = 5 * NetConst.DayTicks;
            for (int e = 0; e < wd.Graph.Edges.Count; e++) for (int sl = 0; sl < wd.Traffic.P.Slots; sl++) wd.Traffic.M[e, sl] = wd.Traffic.P.Cut + 100;
            for (int hr = 0; hr < 48; hr++) { wd.Tick = 5 * NetConst.DayTicks + hr * 1500; wd.Director.Hourly(wd); }
            int debris = 0; RailEdge de = null; foreach (var e in wd.Graph.Edges) if (e.Debris) { debris++; de = e; }
            check(debris > 0 && de.State == EdgeState.Cortada, $"un corte del director deja escombros (tramos con escombros: {debris})");
            if (de != null) { for (int i = 0; i < 6000; i++) wd.Step(); check(de.Debris && de.State == EdgeState.Cortada, "y siguen ahí tras 6.000 ticks aunque el plan del director caduque"); }
        }
    }
}
