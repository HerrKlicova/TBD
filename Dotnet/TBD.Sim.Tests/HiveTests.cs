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
            check(p.Inc == d.Inc && p.SpillMil == d.SpillMil && p.DecayMil == d.DecayMil && p.Slots == d.Slots && p.Warn == d.Warn && p.Ambush == d.Ambush && p.Cut == d.Cut && p.Cap == d.Cap && p.Forget == d.Forget,
                $"los parámetros por defecto coinciden con balance_colmena.csv (+{p.Inc}, derrame {p.SpillMil}, decae {p.DecayMil}, {p.Slots} franjas, {p.Warn}/{p.Ambush}/{p.Cut}, tope {p.Cap}, olvida bajo {p.Forget})");

            // D-N31: un solo paso por tren, tramo, franja y día; dos trenes distintos sí suman; tope de 1.200
            var once = new TrafficMemory(1, p);
            once.OnPass(0, NetConst.Hours(9), 1); once.OnPass(0, NetConst.Hours(10), 1);
            check(once.M[0, 2] == 200, $"el mismo tren dos veces en la misma franja el mismo día suma una sola vez ({once.M[0, 2]})");
            once.OnPass(0, NetConst.Hours(10), 2);
            check(once.M[0, 2] == 400, $"un segundo tren sí suma ({once.M[0, 2]})");
            var cap = new TrafficMemory(1, p);
            for (int k = 0; k < 20; k++) cap.OnPass(0, NetConst.Hours(9), 100 + k);
            check(cap.M[0, 2] == 1200, $"la memoria no pasa del tope de 1.200 ({cap.M[0, 2]})");

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
            check(learnedDay >= 2 && learnedDay <= 4, $"con el horario fijo, la Colmena lo aprende al día {learnedDay}: «{text}»");
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

            // ---- director (hito 4, parte 2): conserva la masa, sin asedios el día 1, asedio desde el día 2, castigo al horario fijo
            {
                var wd = NetScenario.Mvp();
                long mass0 = wd.Field.Mass();
                bool siegeDay1 = false, siegeLater = false, ambushOrCut = false;
                var events = new System.Collections.Generic.List<string>();
                for (long t = 0; t < 6L * NetConst.DayTicks; t++)
                {
                    wd.Step();
                    long day = wd.Tick / NetConst.DayTicks;
                    foreach (var pl in wd.Director.Active)
                    {
                        if (pl.Kind == HiveDirector.PlanKind.Asedio) { if (day == 0) siegeDay1 = true; else siegeLater = true; }
                        else ambushOrCut = true;
                    }
                }
                foreach (var e in wd.Intel) if (e.Id != "TR-1" && e.Id != "TR-5") events.Add(NetConst.Clock(e.Tick) + " d" + (e.Tick / NetConst.DayTicks + 1) + " " + e.Id);
                check(wd.Field.Mass() == mass0, $"el director no crea zombis: la masa de la mancha se conserva en 6 días ({mass0} → {wd.Field.Mass()})");
                check(!siegeDay1, "el director no asedia el día 1 (D-N08)");
                check(siegeLater, "desde el día 2 el director lanza asedios (como mucho uno a la vez en la etapa 1)");
                check(ambushOrCut, "con el horario fijo, el director prepara emboscadas o cortes en los tramos que ha aprendido");
                Console.WriteLine("         informe del director en 6 días: " + string.Join(" | ", events.GetRange(0, Math.Min(8, events.Count))));
            }
        }
    }
}
