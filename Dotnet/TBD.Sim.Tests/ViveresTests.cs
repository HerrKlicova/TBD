using System;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>[35] Víveres, quinto recurso de la red (D-N176, Lucía; reglas de Hugo, sim_economia.py 484-491), y reloj ×103 (D-N161).</summary>
    public static class ViveresTests
    {
        static int Id(NetWorld w, string n) { return w.Graph.Nodes.FindIndex(x => x.Name == n); }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[35] Víveres (D-N176) y reloj ×103 (D-N161)");
            if (Environment.GetEnvironmentVariable("TBD_VIV_SERIE") == "1")      // para Hugo: 30 días de NetScenario.Mvp() con el director, víveres y gente por día
            {
                var sw = NetScenario.Mvp(); Console.WriteLine($"# hash inicial {sw.Hash():X16} · tick {sw.Tick} ({NetConst.Clock(sw.Tick)})");
                int[] ns = { Id(sw, "Sagunto"), Id(sw, "Nules"), Id(sw, "Valencia"), Id(sw, "Castellón") };
                Console.WriteLine("dia;hash;" + string.Join(";", Array.ConvertAll(ns, n => sw.Graph.Nodes[n].Name + "_viv;" + sw.Graph.Nodes[n].Name + "_pob")) + ";hambre_total");
                for (int d = 1; d <= 30; d++)
                {
                    long end = (sw.Tick / NetConst.DayTicks + 1) * NetConst.DayTicks + 1; while (sw.Tick < end) { sw.ApplyCommands(); sw.Step(); }
                    Console.WriteLine($"{d};{sw.Hash():X16};" + string.Join(";", Array.ConvertAll(ns, n => sw.Stock[n, (int)Res.Viveres] + ";" + sw.Stock[n, (int)Res.Personas])) + $";{sw.StarvedTotal}");
                }
            }
            const int V = (int)Res.Viveres, P = (int)Res.Personas;

            // ---- reloj
            check(NetConst.DayTicks == 25_200 && NetConst.HourTicks == 1_050 && NetConst.Hours(1) == 1_050 && NetConst.Hours(24) == NetConst.DayTicks, "reloj ×103: 25.200 ticks por día y 1.050 por hora (14 min reales a 30 Hz)");
            check(NetConst.GameMs(7) == 24_000 && NetConst.GameMs(NetConst.DayTicks) == 86_400_000L && NetConst.Clock(NetConst.Hours(13.5)) == "13:30" && NetConst.Clock(NetConst.DayTicks - 1) == "23:59", "1 tick = 24/7 s de juego, sin decimales; el reloj marca 13:30 y 23:59");
            check(NetConst.TrainMmPerTick * NetConst.HourTicks / 1_000_000 == 60, $"el tren sigue a 60 km/h de juego ({NetConst.TrainMmPerTick} mm por tick)");

            // ---- km reales (Paula, PK del C-6; Álvaro, red-inicio.md v1)
            var g = RailNetSamples.Mvp();
            long Km(string a, string b) { int ia = g.Nodes.FindIndex(x => x.Name == a), ib = g.Nodes.FindIndex(x => x.Name == b); foreach (var e in g.Edges) if ((e.A == ia && e.B == ib) || (e.A == ib && e.B == ia)) return e.LengthMm; return -1; }
            check(Km("Sagunto", "Puçol") == 7_130_000 && Km("Puçol", "Valencia") == 26_500_000 && Km("Sagunto", "Almenara") == 9_590_000 && Km("Almenara", "Nules") == 11_760_000 && Km("Nules", "Castellón") == 18_620_000,
                "km reales: Puçol 7,13, Valencia 33,63, Almenara 9,59, Nules 21,35 y Castellón 39,97 desde Sagunto");
            bool mij = false; foreach (var e in g.Edges) foreach (var ap in e.Ambush) if (ap.Name == "puente del Mijares") mij = ap.KmMm == 34_500_000 && ap.KmMm > e.KmStartMm && ap.KmMm < e.KmStartMm + e.LengthMm;
            check(mij, "el puente del Mijares, en el km 34,5 (dentro del tramo Nules–Castellón)");

            // ---- víveres
            check(NetConst.ResCount == 5 && V == 4 && (int)Res.Municion == 0 && P == 3, "los víveres son el quinto recurso, al final (los otros no cambian de índice)");
            var w = NetScenario.Mvp(); int sag = Id(w, "Sagunto"), nul = Id(w, "Nules"), val = Id(w, "Valencia");
            check(w.Stock[sag, V] == 80, $"Sagunto empieza con 80 u de víveres ({w.Stock[sag, V]})");
            check(w.Production.BaseU(nul, V) == 6 && w.Production.BaseU(val, V) == 30 && w.Production.BaseU(sag, V) == 0 && w.Production.BaseU(nul, P) == 0, "producen Nules 6 u al día y Valencia 30 (balance_nodos.csv); las personas no se producen");

            // consumo: 0,10 u por persona y día, con remanente
            var c = NetScenario.Mvp(); c.DirectorEnabled = false; c.ProductionEnabled = false; c.Services.Clear();
            for (int n = 0; n < c.Graph.Nodes.Count; n++) { c.Stock[n, P] = 0; c.Stock[n, V] = 0; }
            c.Stock[sag, P] = 80; c.Stock[sag, V] = 80; c.Stock[nul, P] = 15; c.Stock[nul, V] = 100;
            long d0 = (c.Tick / NetConst.DayTicks + 1) * NetConst.DayTicks; c.Tick = d0 - 1; c.Step();
            check(c.Stock[sag, V] == 72 && c.Stock[sag, P] == 80, $"80 personas comen 8 u al día ({80 - c.Stock[sag, V]})");
            check(c.Stock[nul, V] == 99 && c.FoodRemMil[nul] == 500, $"15 personas, 1,5 u: se cobra 1 y el medio queda de remanente ({c.FoodRemMil[nul]} milésimas)");
            c.Tick = d0 + NetConst.DayTicks - 1; c.Step();
            check(c.Stock[nul, V] == 97 && c.FoodRemMil[nul] == 0, "al día siguiente, 1,5 + 0,5 = 2 u");

            // sin víveres: −5 % de la gente al día por la fracción que falta
            c.Stock[sag, V] = 0; c.Stock[sag, P] = 80; c.FoodRemMil[sag] = 0; c.Tick = d0 + 2 * NetConst.DayTicks - 1; c.Step();
            check(c.Stock[sag, P] == 76 && c.Stock[sag, V] == 0 && c.StarvedTotal == 4, $"sin víveres, 80 personas pierden el 5 % (4) en un día ({80 - c.Stock[sag, P]})");
            c.Stock[sag, V] = 4; c.Stock[sag, P] = 80; c.FoodRemMil[sag] = 0; c.Tick = d0 + 3 * NetConst.DayTicks - 1; c.Step();
            check(c.Stock[sag, P] == 78 && c.Stock[sag, V] == 0, $"con la mitad (4 de 8 u), pierden el 2,5 % (2) y se acaban los víveres ({80 - c.Stock[sag, P]})");

            // nodo pequeño con falta parcial: el hambre se acumula en milésimas (Hugo) y de media cuadra con el modelo
            int ala = Id(c, "Almenara"); c.Stock[ala, P] = 12; c.Stock[ala, V] = 0; c.FoodRemMil[ala] = 0; c.StarveRemMil[ala] = 0; int lostSmall = 0;
            for (int d = 4; d < 14; d++) { c.Stock[ala, V] = 0; int before = c.Stock[ala, P]; c.Tick = d0 + d * NetConst.DayTicks - 1; c.Step(); lostSmall += before - c.Stock[ala, P]; }
            check(lostSmall >= 5 && lostSmall <= 6, $"12 personas sin comida pierden 0,6 al día (5 %): {lostSmall} en 10 días; antes el floor daba 0");

            // producción por hora, como el resto: 6 u al día en Nules con la plantilla entera (12)
            var p = NetScenario.Mvp(); p.DirectorEnabled = false; p.Services.Clear(); p.Stock[nul, P] = 12; p.Stock[nul, V] = 0;
            long h0 = (p.Tick / NetConst.HourTicks + 1) * NetConst.HourTicks;
            for (int h = 0; h < 24; h++) { p.Tick = h0 + h * NetConst.HourTicks - 1; p.Step(); }
            check(p.Stock[nul, V] == 6 || p.Stock[nul, V] == 5, $"Nules con 12 personas produce 6 u de víveres al día, hora a hora ({p.Stock[nul, V]}, menos lo que comen al cambiar de día)");

            // carga en tren, hash y guardado
            var a = NetScenario.Mvp(); var b = NetScenario.Mvp(); ulong ha = a.Hash();
            b.Stock[sag, V] += 1;
            check(b.Hash() != ha, "las existencias de víveres van en el hash");
            b.FoodRemMil[sag] = 300; b.Stock[sag, V] -= 1;
            check(b.Hash() != ha, "el remanente de víveres también");
            var bytes = NetSave.Save(b); var l = NetScenario.Mvp(); NetSave.Load(bytes, l);
            check(l.Hash() == b.Hash() && l.FoodRemMil[sag] == 300 && l.Stock[sag, V] == 80, "se guardan y se cargan (NetVersion 4)");
            check(NetSave.NetVersion >= 4, "la versión de la red es la 4 o posterior");
        }
    }
}
