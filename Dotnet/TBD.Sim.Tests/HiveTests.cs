using System;
using System.IO;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Hito 4, memoria de tráfico (diseño de Lucía, fórmulas de Hugo) e informe de inteligencia.</summary>
    static class HiveTests
    {
        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[14] Colmena (hito 4): memoria de tráfico e informe");
            string dir = BalanceTests.DataDirPublic();
            var p = TrafficParams.FromCsv(new CsvTable(File.ReadAllText(Path.Combine(dir, "balance_colmena.csv"))));
            var d = TrafficParams.Default;
            check(p.Inc == d.Inc && p.SpillMil == d.SpillMil && p.DecayMil == d.DecayMil && p.Slots == d.Slots && p.Warn == d.Warn && p.Ambush == d.Ambush && p.Cut == d.Cut,
                $"los parámetros por defecto coinciden con balance_colmena.csv (+{p.Inc}, derrame {p.SpillMil}, decae {p.DecayMil}, {p.Slots} franjas, {p.Warn}/{p.Ambush}/{p.Cut})");

            // P3 sin derrame, con la clase de la partida: mismos valores que el modelo de Hugo
            var np = p; np.SpillMil = 0;
            var tm = new TrafficMemory(1, np);
            var got = new System.Text.StringBuilder();
            long tick = NetConst.Hours(8);
            for (int day = 0; day < 10; day++)
            {
                got.Append(day > 0 ? "," : "").Append(tm.M[0, tm.SlotOf(tick)]);
                tm.OnPass(0, tick);
                tm.Decay(null);
                tick += NetConst.DayTicks;
            }
            check(got.ToString() == "0,170,314,436,540,629,704,768,822,868", "P3 con la memoria de la partida: " + got);

            // derrame: pasar a las 07:59 (franja 1, 04-08) también carga la franja de las 08-12
            var sp = new TrafficMemory(1, p);
            sp.OnPass(0, NetConst.Hours(7.9));
            check(sp.M[0, 1] == 200 && sp.M[0, 2] == 100 && sp.M[0, 0] == 100, $"el paso suma 200 en su franja y derrama 100 a las vecinas ({sp.M[0, 0]}/{sp.M[0, 1]}/{sp.M[0, 2]})");

            // la partida: el horario fijo del tren 2 (Sagunto 07:00) acaba en el informe, y al retenerlo unos días se olvida
            var w = NetScenario.Mvp();
            var texts = new IntelTexts(File.ReadAllText(Path.Combine(dir, "textos_informe.csv")));
            int learnedDay = -1;
            for (int day = 0; day < 8 && learnedDay < 0; day++)
            {
                for (long t = 0; t < NetConst.DayTicks; t++) w.Step();
                if (w.Intel.Exists(e => e.Id == "TR-1")) learnedDay = day + 1;
            }
            var first = w.Intel.Find(e => e.Id == "TR-1");
            string src;
            string text = first.Id != null ? texts.Format(first, "es", out src) : "(ninguna)";
            check(learnedDay > 0 && learnedDay <= 4, $"con el horario fijo, la Colmena lo aprende al día {learnedDay}: «{text}»");
            check(text.Contains("Tren") && !text.Contains("{"), "el texto del informe sale completo, sin llaves sin rellenar");
            for (int k = 0; k < w.Services.Count; k++) w.Push(new NetCommand { Tick = w.Tick, Type = NetCmdType.TrainHold, Arg = k });
            int forgotDay = -1;
            for (int day = 0; day < 10 && forgotDay < 0; day++)
            {
                for (long t = 0; t < NetConst.DayTicks; t++) w.Step();
                if (w.Intel.Exists(e => e.Id == "TR-5")) forgotDay = day + 1;
            }
            var fe = w.Intel.Find(e => e.Id == "TR-5");
            string ft = fe.Id != null ? texts.Format(fe, "es", out src) : "(ninguna)";
            check(forgotDay > 0, $"con los trenes retenidos, a los {forgotDay} días: «{ft}» (también se informa de lo que olvida)");
            var en = texts.Format(first, "en", out src);
            check(en.StartsWith("It has learned") && !en.Contains("{"), "el banco de textos también sale en inglés: «" + en + "»");
        }
    }
}
