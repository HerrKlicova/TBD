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
                check(wd.Intel.Exists(e => e.Id == "ES-1"), "un tren que sale de una estación amenazada lleva una manada detrás (caza, ES-1)");
            }

            // ---- D-N32: cada asedio se anuncia 3 h antes («Horda hacia X en 3 h») y el primero empieza entre las 10:00 y las 16:00 del día 2
            {
                var wf = NetScenario.Mvp();
                long warnTick = -1, startTick = -1;
                for (long t = 0; t < 3L * NetConst.DayTicks && startTick < 0; t++)
                {
                    wf.Step();
                    if (warnTick < 0 && wf.Alerts.Exists(a => a.Text.StartsWith("Horda hacia"))) warnTick = wf.Tick;
                    if (wf.Intel.Exists(e => e.Id == "ES-2")) startTick = wf.Tick;
                }
                long tod = startTick % NetConst.DayTicks;
                check(warnTick > 0 && startTick - warnTick >= NetConst.Hours(3) - NetConst.FieldEvery, $"el asedio se avisa con 3 h ({NetConst.Clock(warnTick)} → {NetConst.Clock(startTick)})");
                check(startTick / NetConst.DayTicks == 1 && tod >= NetConst.Hours(10) && tod <= NetConst.Hours(16), $"el primer asedio cae de día, el día 2 entre las 10:00 y las 16:00 (día {startTick / NetConst.DayTicks + 1}, {NetConst.Clock(startTick)})");
            }

            // ---- D-N33/D-N34: los valores de Hugo en balance_colmena.csv coinciden con los del director
            {
                var dc = DirectorParams.Default.WithColmena(new CsvTable(File.ReadAllText(Path.Combine(dir, "balance_colmena.csv"))));
                var dd = DirectorParams.Default;
                check(dc.BudgetCurveMil == dd.BudgetCurveMil && dc.SiegeShareMil == dd.SiegeShareMil && dc.SiegeDays == dd.SiegeDays && dc.WeightBase == dd.WeightBase
                    && dc.WeightInteriorMil == dd.WeightInteriorMil && dc.WeightNestMil == dd.WeightNestMil && dc.AmbushShareMil == dd.AmbushShareMil
                    && dc.AmbushPerMem == dd.AmbushPerMem && dc.CutPerMem == dd.CutPerMem && dc.MaxSiegesStage1 == dd.MaxSiegesStage1
                    && dc.PointUniqueMil == dd.PointUniqueMil && dc.AttentionMil == dd.AttentionMil && dc.RespiteMinH == dd.RespiteMinH && dc.RespiteMaxH == dd.RespiteMaxH,
                    $"balance_colmena.csv (D-N33/34) coincide con el director: curva ×{dc.BudgetCurveMil}‰, asedio {dc.SiegeShareMil}‰ y {dc.SiegeDays} días, respiro {dc.RespiteMinH}-{dc.RespiteMaxH} h");
                Console.WriteLine($"         presupuesto por hora (‰ de densidad): día 1 {HiveDirector.HourlyBudgetPermil(0, null, 1360)}, día 2 {HiveDirector.HourlyBudgetPermil(1, null, 1360)}, día 3 {HiveDirector.HourlyBudgetPermil(2, null, 1360)}, día 20 {HiveDirector.HourlyBudgetPermil(19, null, 1360)}");
            }

            // ---- D-N33/D-N34: asedio por calendario (siempre uno desde el primero, salvo el respiro de 6-12 h), objetivo sorteado por peso²
            {
                var wc = NetScenario.Mvp();
                var starts = new System.Collections.Generic.List<long>();
                var ends = new System.Collections.Generic.List<long>();
                var targets = new System.Collections.Generic.Dictionary<string, int>();
                bool prev = false; int maxAt = 0;
                for (long t = 0; t < 20L * NetConst.DayTicks; t++)
                {
                    wc.Step();
                    int n = 0; HiveDirector.Plan cur = default;
                    foreach (var pl in wc.Director.Active) if (pl.Kind == HiveDirector.PlanKind.Asedio && pl.Started) { n++; cur = pl; }
                    maxAt = Math.Max(maxAt, n);
                    if (n > 0 && !prev) { starts.Add(wc.Tick); string nm = wc.Graph.Nodes[cur.Node].Name; targets[nm] = targets.TryGetValue(nm, out int c) ? c + 1 : 1; }
                    if (n == 0 && prev) ends.Add(wc.Tick);
                    prev = n > 0;
                }
                bool gapsOk = true; string gaps = "";
                for (int i = 0; i < ends.Count && i + 1 < starts.Count; i++)
                {
                    long g = starts[i + 1] - ends[i];
                    gaps += (g / NetConst.Hours(1)) + "h ";
                    if (g < NetConst.Hours(6) || g > NetConst.Hours(12) + NetConst.Hours(3) + NetConst.Hours(1)) gapsOk = false;   // respiro + aviso de 3 h (+1 h de granularidad)
                }
                var tl = new System.Collections.Generic.List<string>(); foreach (var kv in targets) tl.Add(kv.Key + " " + kv.Value);
                check(maxAt == 1 && starts.Count >= 6, $"desde el día 2 hay asedio por calendario, uno a la vez en la etapa 1 ({starts.Count} asedios en 20 días)");
                check(gapsOk && ends.Count > 0, $"entre asedios, un respiro de 6-12 h más el aviso de 3 h (huecos: {gaps.Trim()})");
                check(targets.Count >= 2, $"el objetivo se sortea por peso² y no es siempre el mismo ({string.Join(", ", tl)})");
                check(wc.Field.Mass() == NetScenario.Mvp().Field.Mass(), "el reparto por peso (deriva) tampoco crea zombis en 20 días");
            }

            // ---- D-N33 (Mak): reparto de objetivos con 200 semillas, jugador quieto en Sagunto y sin atención, a ±8 puntos del modelo de Hugo
            {
                var refs = new System.Collections.Generic.Dictionary<string, int> { { "Valencia", 28 }, { "Castellón", 17 }, { "Teruel", 11 }, { "Segorbe", 7 }, { "Barracas", 7 } };
                var cnt = new System.Collections.Generic.Dictionary<string, int>();
                var ws = NetScenario.Mvp();
                int sagi = ws.Graph.Nodes.FindIndex(n => n.Name == "Sagunto");
                ws.Push(new NetCommand { Tick = ws.Tick, Type = NetCmdType.Materialize, Arg = sagi });
                ws.Step();
                // población de la mediana del día 10 del modelo de Hugo, en unidades de 10 personas (Mak/Hugo)
                foreach (var kv in new System.Collections.Generic.Dictionary<string, int> { { "Valencia", 34 }, { "Castellón", 35 }, { "Teruel", 26 }, { "Segorbe", 10 }, { "Barracas", 9 }, { "Sagunto", 38 } })
                    ws.Stock[ws.Graph.Nodes.FindIndex(n => n.Name == kv.Key), (int)Res.Personas] = kv.Value * 10;
                string wts = ""; for (int n = 0; n < ws.Graph.Nodes.Count; n++) wts += ws.Graph.Nodes[n].Name + "=" + ws.Director.SiegeWeight(ws, n) + " ";
                Console.WriteLine("         pesos con la población de Hugo: " + wts.Trim());
                ws.Director.IgnoreAttention = true;
                const int N = 200;
                for (int seed = 0; seed < N; seed++)
                {
                    ws.Director.Seed = (ulong)seed * 0x9E3779B97F4A7C15UL + 1;
                    int t = ws.Director.DrawSiegeTarget(ws);
                    string nm = t >= 0 ? ws.Graph.Nodes[t].Name : "-";
                    cnt[nm] = cnt.TryGetValue(nm, out int c) ? c + 1 : 1;
                }
                bool okDist = true; string rep = "";
                foreach (var kv in refs)
                {
                    int pct = (cnt.TryGetValue(kv.Key, out int c2) ? c2 : 0) * 100 / N;
                    rep += $"{kv.Key} {pct}% (Hugo {kv.Value}%) ";
                    if (Math.Abs(pct - kv.Value) > 8) okDist = false;
                }
                check(okDist, "el objetivo del asedio con 200 semillas sale a ±8 puntos del modelo de Hugo: " + rep.Trim());
            }

            // ---- los CSV mandan: un valor cambiado se nota en el mundo, y un archivo que falta avisa y usa los valores por defecto
            {
                var reader = TBD.Net.BalanceSource.Reader; var warnPrev = TBD.Net.BalanceSource.Warn;
                var warns = new System.Collections.Generic.List<string>();
                try
                {
                    TBD.Net.BalanceSource.Warn = warns.Add;
                    TBD.Net.BalanceSource.Reader = f => { var t = reader(f); return f == "balance_colmena.csv" ? t.Replace("asedio_dura_dias;2;", "asedio_dura_dias;3;").Replace("memoria_trafico_tope;1200;", "memoria_trafico_tope;900;") : t; };
                    var wc2 = NetScenario.Mvp();
                    check(wc2.Director.P.SiegeDays == 3 && wc2.Traffic.P.Cap == 900, $"cambiar un CSV (asedio_dura_dias, memoria_trafico_tope) cambia el mundo: {wc2.Director.P.SiegeDays} días de asedio, tope {wc2.Traffic.P.Cap}");
                    check(wc2.Director.Curve != null && wc2.Director.Curve.Length >= 401 && wc2.Director.Curve[1] == 1078, "el director usa la curva de colmena_curva.csv, no la aproximación lineal");
                    TBD.Net.BalanceSource.Missing.Clear(); warns.Clear();
                    TBD.Net.BalanceSource.Reader = f => f == "balance_director.csv" ? null : reader(f);
                    var wm = NetScenario.Mvp();
                    check(warns.Exists(x => x.Contains("balance_director.csv")) && wm.Director.P.SiegeWarnH == DirectorParams.Default.SiegeWarnH, "si falta un CSV se avisa y se usan los valores por defecto");
                }
                finally { TBD.Net.BalanceSource.Reader = reader; TBD.Net.BalanceSource.Warn = warnPrev; TBD.Net.BalanceSource.Missing.Clear(); }
            }

            // ---- parámetros del director en su CSV (Hugo los calibra sin tocar C#)
            {
                var dp = DirectorParams.FromCsv(new CsvTable(File.ReadAllText(Path.Combine(dir, "balance_director.csv"))));
                var dd = DirectorParams.Default;
                check(dp.SiegeWarnH == 3 && dp.FirstSiegeFromH == 10 && dp.FirstSiegeToH == 16 && dp.SiegePerPerson == dd.SiegePerPerson && dp.SiegeCooldownH == dd.SiegeCooldownH && dp.HuntPerHour == dd.HuntPerHour && dp.AttentionHours == dd.AttentionHours,
                    "balance_director.csv carga y coincide con los valores por defecto del director");
            }

            // ---- memoria de atención: más de 6 h en una zona hace que el director asedie OTRO nodo (AT-1)
            {
                var wa = NetScenario.Mvp();
                for (long t = 0; t < NetConst.DayTicks; t++) wa.Step();                 // día 1 entero (sin asedios)
                int sag = wa.Graph.Nodes.FindIndex(n => n.Name == "Sagunto");
                var cas = wa.Graph.Nodes.Find(n => n.Name == "Castellón");
                wa.Field.D[cas.YMeters * HordeField.W + cas.XMeters] = 300;             // Castellón amenazada (con gente): objetivo posible
                wa.Push(new NetCommand { Tick = wa.Tick, Type = NetCmdType.Materialize, Arg = sag });
                bool at1 = false;
                // el escenario empieza a las 05:30: el primer asedio (10:00) llega con 4,5 h de atención; el siguiente, ya con más de 6 h
                for (long t = 0; t < 4L * NetConst.DayTicks && !at1; t++) { wa.Step(); at1 = wa.Intel.Exists(e => e.Id == "AT-1"); }
                var ae = wa.Intel.Find(e => e.Id == "AT-1");
                check(at1, at1 ? $"tras más de 6 h en la zona de Sagunto, el director ataca otro nodo: AT-1 {ae.Vars[1]} mientras defendías {ae.Vars[3]}" : $"AT-1 no aparece en 4 días con el jugador en Sagunto (zona {wa.ActiveZoneNode}, atención {wa.Director.AttentionTicks[sag]}, ES-2 {wa.Intel.FindAll(e => e.Id == "ES-2").Count}, foco {wa.Director.AttentionNode(wa)})");
            }
        }
    }
}
