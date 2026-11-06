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

            var w0 = NetScenario.Mvp();
            int nules = -1; foreach (var n in w0.Graph.Nodes) if (n.Name == "Nules") nules = n.Id;
            bool anyFounded = false; foreach (var f in w0.Stations.Founded) if (f) anyFounded = true;
            var sp = StationParams.FromCsv(new CsvTable(File.ReadAllText(Path.Combine(BalanceTests.DataDirPublic(), "balance_economia.csv"))));
            check(nules >= 0 && w0.Stations.IsSite(nules) && w0.Stations.P.Sites.Length == 1 && !anyFounded && w0.StationKindOf(nules) == StationKind.NoFundada,
                "el único emplazamiento de la demo es Nules y empieza sin fundar (Jérica salió de la demo)");
            check(sp.FoundMaterialsU == 30 && sp.FoundPeople == 12 && w0.Stations.P.FoundMaterialsU == 30 && w0.Stations.P.FoundPeople == 12 && w0.Stations.P.StationPeople == 40,
                $"fundar cuesta 30 u y 12 personas (fundar_materiales y guarnicion_minima del CSV de Hugo); para estación, 40 personas");
            check(w0.Stations.Hash() == 0, "sin fundar nada el hash de las estaciones es 0: G1–G5 no se mueven");

            // montaje: un tren parado en una parada de su horario, con la carga de fundar; el emplazamiento es ese nodo (la lista es configurable)
            NetWorld Setup(out TrainService sv, out int site, int mats = 30, int people = 12)
            {
                var w = NetScenario.Mvp();
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
            var wr = NetScenario.Mvp(); var edge = wr.Graph.Edges[0]; int rsite = edge.B;
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
            wl.Works.Add(sl, wl.Works.Preview(sl, PieceKind.Deposito, new[] { 0, 0 }, 100));
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

            // la orden con Nules sin tren se rechaza, y la repetición dorada con esa orden sigue idéntica
            string gold = NetGolden.Record(new List<NetCommand> { new NetCommand { Tick = NetScenario.Mvp().Tick + 100, Type = NetCmdType.FoundStation, Arg = nules } }, 1500);
            var res = NetGolden.Verify(gold);
            check(res.Ok && gold.Contains(";12;" + nules + ";0\n"), $"la orden FoundStation se graba y se reproduce igual: {res}");
        }
    }
}
