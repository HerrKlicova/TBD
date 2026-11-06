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
                check(wd.Field.Mass() + (wd.ActiveZoneNode >= 0 ? wd.ActiveZoneTaken : 0) == mass0 + wd.InjectedTotal - wd.LostTotal - wd.RetiredTotal, $"masa(t) = masa(0) + inyectada − bajas − retirados, exacto en enteros, en 6 días ({mass0} + {wd.InjectedTotal} − {wd.LostTotal} − {wd.RetiredTotal} = {wd.Field.Mass()})");
                check(wd.InjectedTotal > 0, "el director inyecta el presupuesto como masa nueva (D-N33, Mak)");
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
                Console.WriteLine($"         presupuesto del día (zombis, sin ruido de semilla): día 0 {NetScenario.Mvp().Director.DayZombies(0, 2000)}, día 1 {NetScenario.Mvp().Director.DayZombies(1, 2000)}, día 10 {NetScenario.Mvp().Director.DayZombies(10, 5000)}");
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
                check(wc.Field.Mass() == NetScenario.Mvp().Field.Mass() + wc.InjectedTotal - wc.LostTotal - wc.RetiredTotal, $"el reparto por peso (deriva) solo mueve masa: entra lo inyectado ({wc.InjectedTotal}) y sale lo retirado ({wc.RetiredTotal}) en 20 días");
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

            // ---- D-N35 (Lucía, Sara): lo inyectado solo cae en celdas del borde, nunca en el objetivo, en una zona en detalle ni a menos de ~10 km de un nodo del jugador
            {
                var wi = NetScenario.Mvp();
                bool edgeOnly = true, notTarget = true, awayFromPlayer = true; int count = 0; long sum = 0;
                for (long t = 0; t < 4L * NetConst.DayTicks; t++)
                {
                    wi.Step();
                    foreach (var inj in wi.Injections)
                    {
                        count++; sum += inj.Amount;
                        int x = inj.Cell % HordeField.W, y = inj.Cell / HordeField.W;
                        if (x != 0 && y != 0 && x != HordeField.W - 1 && y != HordeField.H - 1) edgeOnly = false;
                        foreach (var pl in wi.Director.Active) if (pl.Kind == HiveDirector.PlanKind.Asedio && pl.Cell == inj.Cell) notTarget = false;
                        for (int n = 0; n < wi.Graph.Nodes.Count; n++)
                            if (wi.Stock[n, (int)Res.Personas] > 0 && Math.Abs(wi.Graph.Nodes[n].XMeters - x) <= 3 && Math.Abs(wi.Graph.Nodes[n].YMeters - y) <= 3) awayFromPlayer = false;
                    }
                }
                check(count > 0 && edgeOnly && notTarget && awayFromPlayer, $"lo inyectado solo cae en celdas del borde, lejos del objetivo y de los nodos del jugador ({count} inyecciones, {sum} por mil en 4 días)");
                check(sum == wi.InjectedTotal, "el contador por tick de lo inyectado suma lo mismo que el total del mundo");
            }

            // ---- presupuesto del director sin jugador y sin ruido (curva pura) contra la tabla esperada de Hugo, días 0-9 (±5 %); estación indexada con el día desde 0
            {
                var wb = NetScenario.Mvp();
                wb.Director.Noise = null;
                long[] hugo = { 232, 821, 1685, 2421, 3108, 3366, 3617, 6171, 4081, 4285 };    // día 7 con el gran ataque ×2 (solo de asedio, 0,6 de la parte)
                bool okB = true; string txt = "";
                for (int day = 0; day < 10; day++)
                {
                    long z = wb.Director.DayZombies(day, 0);
                    if (day == 7) z += z * wb.Director.P.SiegeShareMil / 1000 * (wb.Director.P.BigMultMil - 1000) / 1000;   // gran ataque: el asedio golpea ×2
                    txt += z + "/" + hugo[day] + " ";
                    if (Math.Abs(z - hugo[day]) * 100 > hugo[day] * 5) okB = false;
                }
                Console.WriteLine("         presupuesto sin ruido ni huella (mío/Hugo): " + txt.Trim());
                check(okB, "el presupuesto diario con la curva pura coincide con la tabla de Hugo a ±5 % en los días 0 a 9");
            }

            // ---- D-N37b: la horda inicial deriva dentro de una correa de 2 celdas, nunca entra en un nodo y es finita
            {
                var wl2 = NetScenario.Mvp();
                var born = new System.Collections.Generic.List<int>();
                for (int i = 0; i < wl2.Field.S.Length; i++) if (wl2.Field.S[i] > 0) born.Add(i);
                long s0 = wl2.Field.StaticMass();
                long nearBirth = wl2.Field.LeashedMassNearTrack(wl2.Graph, 1);
                bool inLeash = true, inNode = false; int moved = 0;
                var before = (int[])wl2.Field.S.Clone();
                for (int q = 0; q < 600; q++) wl2.Field.Step(wl2.Graph, wl2.Attraction);
                for (int i = 0; i < wl2.Field.S.Length; i++)
                {
                    if (wl2.Field.S[i] != before[i]) moved++;
                    if (wl2.Field.S[i] <= 0) continue;
                    int cx = i % HordeField.W, cy = i / HordeField.W;
                    bool ok = false;
                    foreach (int b in born) if (Math.Abs(b % HordeField.W - cx) <= 2 && Math.Abs(b / HordeField.W - cy) <= 2) { ok = true; break; }
                    if (!ok) inLeash = false;
                    foreach (var nd in wl2.Graph.Nodes) if (Math.Abs(nd.XMeters - cx) <= 1 && Math.Abs(nd.YMeters - cy) <= 1 && before[i] == 0) inNode = true;
                }
                check(inLeash && moved > 100, $"la horda inicial deriva ({moved} celdas cambian) sin salir de su correa de 2 celdas");
                check(!inNode && wl2.Field.StaticMass() == s0, $"la horda inicial no entra en un nodo y es finita: {wl2.Field.StaticMass()} ‰ de {s0} ‰ tras 600 pasos");
                Console.WriteLine($"         masa inicial a tiro de la vía (1 celda): {wl2.Field.LeashedMassNearTrack(wl2.Graph, 1)} ‰ de {s0} ‰ (guarnición pagada por Hugo: ~1.250 ‰)");
                long nearT = wl2.Field.LeashedMassNearTrack(wl2.Graph, 1);
                check(nearBirth >= 700 && nearBirth <= 1500 && nearT <= nearBirth, $"a tiro de la vía hay una masa del orden de la guarnición pagada (700-1.500 ‰) y la deriva no la acumula: {nearBirth} ‰ al nacer, {nearT} ‰ tras 600 pasos; el resto es paisaje");
            }

            // ---- D-N39 (Sara): en 10 días, la horda inicial a tiro de la vía nunca pasa de 1.500 grupos
            {
                var w10 = NetScenario.Mvp();
                long worst = 0; long born10 = w10.Field.LeashedMassNearTrack(w10.Graph, 1);
                for (int day = 0; day < 10; day++)
                {
                    for (long t = 0; t < NetConst.DayTicks; t++) { w10.Step(); if (t % 1500 == 0) worst = Math.Max(worst, w10.Field.LeashedMassNearTrack(w10.Graph, 1)); }
                }
                check(worst <= 1500, $"en 10 días la horda inicial a tiro de la vía nunca pasa de 1.500 grupos ({worst} ‰, al nacer {born10} ‰)");
            }

            // ---- emboscada materializada: un tren cae en una emboscada preparada, se detiene con la horda del punto, y se resuelve por orden o por expiración
            {
                var we = NetScenario.Mvp();
                int ei = we.Graph.Edges.FindIndex(e => e.Ambush.Count > 0);
                var edge = we.Graph.Edges[ei];
                // preparamos a mano lo que haría el director: una emboscada en ese tramo con horda en su punto
                int cell = (we.Graph.Nodes[edge.A].YMeters + we.Graph.Nodes[edge.B].YMeters) / 2 * HordeField.W + (we.Graph.Nodes[edge.A].XMeters + we.Graph.Nodes[edge.B].XMeters) / 2;
                we.Field.D[cell] = 400;
                we.Director.Active.Add(new HiveDirector.Plan { Kind = HiveDirector.PlanKind.Emboscada, Edge = edge.Id, Node = -1, Cell = cell, UntilTick = we.Tick + 100000 });
                we.PendingAmbushes.Add(edge.Id);
                // un tren en ese tramo, con la horda sobre la vía
                var sv = we.Services[0];
                sv.AtStop = false; sv.Train.SetRoute(we.Graph.ShortestPath(edge.A, edge.B)); sv.Train.Go();
                edge.HordeAtMm = edge.LengthMm / 2;
                for (long t = 0; t < 60 && !we.Encounter.Active; t++) we.Step();
                bool started = we.Encounter.Active && we.Encounter.Taken > 300 && sv.Ambushed && we.Field.D[cell] == 0;
                int takenAmb = we.Encounter.Taken;
                long massBefore = we.Field.Mass();
                long cargoBefore = sv.CargoTotal();
                we.Push(new NetCommand { Tick = we.Tick, Type = NetCmdType.ResolveAmbush, Arg = 0, Arg2 = 150 });     // la zona devuelve 150 ‰ de 400
                we.Step();
                check(started, "un tren que cae en una emboscada preparada se detiene y la horda del punto sale del campo (encuentro)");
                check(!we.Encounter.Active && !sv.Ambushed && we.LostTotal == takenAmb - 150 && we.Field.D[cell] > 0, $"ResolveAmbush devuelve los supervivientes (150 ‰), pierde las bajas ({we.LostTotal} ‰) y libera el tren");
                // expiración
                var we2 = NetScenario.Mvp();
                var edge2 = we2.Graph.Edges[we2.Graph.Edges.FindIndex(e => e.Ambush.Count > 0)];
                int cell2 = (we2.Graph.Nodes[edge2.A].YMeters + we2.Graph.Nodes[edge2.B].YMeters) / 2 * HordeField.W + (we2.Graph.Nodes[edge2.A].XMeters + we2.Graph.Nodes[edge2.B].XMeters) / 2;
                we2.Field.D[cell2] = 400;
                we2.Director.Active.Add(new HiveDirector.Plan { Kind = HiveDirector.PlanKind.Emboscada, Edge = edge2.Id, Node = -1, Cell = cell2, UntilTick = we2.Tick + 100000 });
                we2.PendingAmbushes.Add(edge2.Id);
                var sv2 = we2.Services[0]; sv2.AtStop = false; sv2.Train.SetRoute(we2.Graph.ShortestPath(edge2.A, edge2.B)); sv2.Train.Go();
                for (int r = 0; r < NetConst.ResCount; r++) sv2.Cargo[r] = 100;
                edge2.HordeAtMm = edge2.LengthMm / 2;
                for (long t = 0; t < 60 && !we2.Encounter.Active; t++) we2.Step();
                long expAt = we2.Encounter.ExpireTick;
                while (we2.Tick <= expAt + 1) we2.Step();
                check(!we2.Encounter.Active && !sv2.Ambushed && sv2.Cargo[0] == 80 && we2.LostTotal == 0, $"si nadie responde en 30 min de juego la horda se dispersa sin bajas y el tren pierde el 20 % de la carga ({sv2.Cargo[0]} de 100)");
                // como mucho una emboscada por tren y día: una segunda el mismo día no se materializa
                we2.Field.D[cell2] = 400;
                we2.PendingAmbushes.Add(edge2.Id);
                sv2.AtStop = false; sv2.Train.SetRoute(we2.Graph.ShortestPath(edge2.A, edge2.B)); sv2.Train.Go();
                for (long t = 0; t < 120; t++) we2.Step();
                check(!we2.Encounter.Active, "como mucho una emboscada por tren y día (D-N39)");
            }

            // ---- estados de manada (Lucía §3): un asedio que pierde más del 50 % de su fuerza se retira y deja respiro
            {
                var wl = NetScenario.Mvp();
                for (long t = 0; t < 2L * NetConst.DayTicks && !wl.Director.Active.Exists(p => p.Kind == HiveDirector.PlanKind.Asedio && p.Started); t++) wl.Step();
                int idx = wl.Director.Active.FindIndex(p => p.Kind == HiveDirector.PlanKind.Asedio && p.Started);
                var spl = wl.Director.Active[idx]; spl.Moved = 400; wl.Director.Active[idx] = spl;
                int node = spl.Node; var nd = wl.Graph.Nodes[node];
                wl.Field.D[nd.YMeters * HordeField.W + nd.XMeters] = 600;
                wl.Push(new NetCommand { Tick = wl.Tick, Type = NetCmdType.Materialize, Arg = node });
                wl.Step();
                wl.Push(new NetCommand { Tick = wl.Tick, Type = NetCmdType.Dematerialize, Arg = node, Arg2 = 100 });    // de 600 vuelven 100: 500 bajas
                wl.Step();
                bool marked = wl.Director.Active.Exists(p => p.Kind == HiveDirector.PlanKind.Asedio && p.Node == node && p.Lost >= 400);
                long hourEnd = (wl.Tick / 1500 + 1) * 1500 - wl.Tick;
                for (long t = 0; t <= hourEnd; t++) wl.Step();
                bool gone = !wl.Director.Active.Exists(p => p.Kind == HiveDirector.PlanKind.Asedio && p.Node == node);
                                check(marked && gone, "un asedio que pierde más del 50 % de su fuerza se retira en la hora siguiente (estado de manada, Lucía §3)");
            }

            // ---- D-N36 (Mak, 4): los zombis sin combatir vuelven a los nidos: la masa sobre la inicial nunca supera lo inyectado en los últimos 6 días
            {
                var wr = NetScenario.Mvp();
                long baseMass = wr.Field.Mass(); long static0 = wr.Field.StaticMass();
                var perDay = new long[21];
                bool capOk = true; long worst = 0; long retiredSeen = 0; bool tickCounters = true;
                for (int day = 0; day < 20; day++)
                {
                    long before = wr.InjectedTotal;
                    for (long t = 0; t < NetConst.DayTicks; t++)
                    {
                        wr.Step();
                        retiredSeen += wr.RetiredThisTick;
                    }
                    perDay[day] = wr.InjectedTotal - before;
                    long last6 = 0; for (int k = Math.Max(0, day - 5); k <= day; k++) last6 += perDay[k];
                    long extra = wr.Field.Mass() + (wr.ActiveZoneNode >= 0 ? wr.ActiveZoneTaken : 0) - baseMass;
                    if (extra > last6) { capOk = false; worst = Math.Max(worst, extra - last6); }
                }
                check(capOk, $"la masa nueva en el campo no supera lo inyectado en los últimos 6 días (peor exceso: {worst} por mil)");
                check(wr.Field.StaticMass() == static0 && wr.InitialMassAlive == static0 && static0 > 0, $"el director no gasta la horda inicial: la guarnición y el paisaje siguen intactos tras 20 días ({static0} ‰ al empezar, {wr.InitialMassAlive} ‰ vivos)");
                check(wr.InjectedTotal * 100 >= wr.Director.BudgetTotal * 98 && wr.InjectedTotal <= wr.Director.BudgetTotal, $"lo inyectado es el presupuesto del director, sin pérdidas por el camino: {wr.InjectedZombies} zombis inyectados de {wr.Director.BudgetTotal * NetWorld.ZombiesPerPermil} de presupuesto en 20 días");
                check(retiredSeen == wr.RetiredTotal && wr.RetiredTotal > 0, $"el contador de retirados por tick suma el total ({wr.RetiredTotal} por mil retirados en 20 días)");
            }

            // ---- mediana de varias semillas contra la referencia de Hugo (solo con TBD_SEEDS=n; tarda: n × 21 días de simulación)
            // criterio D-N37: el total de los días 1-20 de golpes contra asedios y contra toda la red, a ±15 % de la referencia (fila total_1_20)
            {
                string ns = Environment.GetEnvironmentVariable("TBD_SEEDS");
                string dump2 = Environment.GetEnvironmentVariable("TBD_DUMP_DIR");
                if (!string.IsNullOrEmpty(ns))
                {
                    int seeds = int.Parse(ns);
                    var siegeTot = new System.Collections.Generic.List<long>(); var netTot = new System.Collections.Generic.List<long>();
                    var perDay = new System.Collections.Generic.List<long>[21];
                    for (int q = 0; q < 21; q++) perDay[q] = new System.Collections.Generic.List<long>();
                    for (int sd = 0; sd < seeds; sd++)
                    {
                        var wsd = NetScenario.Mvp();
                        wsd.Director.Seed = (ulong)(sd + 1) * 0x9E3779B97F4A7C15UL;
                        long prevS = 0, prevB = 0, sumS = 0, sumB = 0;
                        for (int q = 0; q < 21; q++)
                        {
                            for (long t = 0; t < NetConst.DayTicks; t++) wsd.Step();
                            long ds = (wsd.Director.SiegeMovedTotal - prevS) * NetWorld.ZombiesPerPermil, db = (wsd.Director.BudgetTotal - prevB) * NetWorld.ZombiesPerPermil;
                            perDay[q].Add(ds); prevS = wsd.Director.SiegeMovedTotal; prevB = wsd.Director.BudgetTotal;
                            if (q >= 1) { sumS += ds; sumB += db; }
                        }
                        siegeTot.Add(sumS); netTot.Add(sumB);
                    }
                    siegeTot.Sort(); netTot.Sort();
                    long medS = siegeTot[siegeTot.Count / 2], medN = netTot[netTot.Count / 2];
                    // D-N39 (Hugo): el total esperado depende de la huella fija del jugador (pob/100 + 0,5 trenes + 0,4 nodos + 1,5 obras);
                    // se compara con la fila de la huella de este escenario, redondeada a la unidad
                    var h0w = NetScenario.Mvp();
                    long huellaMil = h0w.Director.HuellaMil(h0w);
                    var refTab = new CsvTable(File.ReadAllText(Path.Combine(dump2 ?? ".", "referencia_total_por_huella.csv")));
                    string[] tr = null; long bestGap = long.MaxValue;
                    foreach (var rr in refTab.Rows) { long gap = Math.Abs(CsvTable.Int(rr[0]) - huellaMil); if (gap < bestGap) { bestGap = gap; tr = rr; } }
                    long refN = CsvTable.Int(tr[refTab.Col("total_red_1_20")]), refS = CsvTable.Int(tr[refTab.Col("total_asedios_1_20_esperado")]);
                    Console.WriteLine($"         huella del escenario {huellaMil / 1000.0:0.00} → fila {tr[0]}");
                    var csv = new System.Text.StringBuilder("dia;zombis_asedio_mediana;p10;p90\n");
                    for (int q = 0; q < 21; q++)
                    {
                        perDay[q].Sort();
                        csv.Append(q).Append(';').Append(perDay[q][perDay[q].Count / 2]).Append(';').Append(perDay[q][perDay[q].Count / 10]).Append(';').Append(perDay[q][Math.Min(perDay[q].Count - 1, perDay[q].Count * 9 / 10)]).Append('\n');
                    }
                    csv.Append("total_1_20;").Append(medS).Append(';').Append(siegeTot[siegeTot.Count / 10]).Append(';').Append(siegeTot[Math.Min(siegeTot.Count - 1, siegeTot.Count * 9 / 10)]).Append('\n');
                    if (!string.IsNullOrEmpty(dump2)) File.WriteAllText(Path.Combine(dump2, "director_mediana_semillas.csv"), csv.ToString());
                    Console.WriteLine($"         {seeds} semillas, días 1-20: asedios {medS} (Hugo {refS}), toda la red {medN} (Hugo {refN})");
                    check(Math.Abs(medS - refS) * 100 <= refS * 15, $"total de golpes contra asedios en los días 1-20: mediana {medS} frente a {refS} de Hugo (±15 %)");
                    check(Math.Abs(medN - refN) * 100 <= refN * 15, $"total contra toda la red en los días 1-20: mediana {medN} frente a {refN} de Hugo (±15 %)");
                }
            }

            // ---- medición para Hugo: zombis que mueve el director cada día (asedio, deriva, emboscada), días 1-20, a un CSV si TBD_DUMP_DIR existe
            {
                var wm2 = NetScenario.Mvp();
                var sb2 = new System.Text.StringBuilder("dia;zombis_asedio;presupuesto_zombis;deriva_zombis;emboscada_zombis;asedios_activos;objetivos\n");
                long pb = 0, ps = 0, pd = 0, pa = 0; long totalSiege = 0;
                int zpp = NetWorld.ZombiesPerPermil;
                for (int day = 1; day <= 20; day++)
                {
                    for (long t = 0; t < NetConst.DayTicks; t++) wm2.Step();
                    var tg = new System.Collections.Generic.List<string>(); int act = 0;
                    foreach (var pl in wm2.Director.Active) if (pl.Kind == HiveDirector.PlanKind.Asedio && pl.Started) { act++; tg.Add(wm2.Graph.Nodes[pl.Node].Name); }
                    var dr = wm2.Director;
                    sb2.Append(day - 1).Append(';').Append((dr.SiegeMovedTotal - ps) * zpp).Append(';').Append((dr.BudgetTotal - pb) * zpp).Append(';').Append((dr.DriftMovedTotal - pd) * zpp)
                       .Append(';').Append((dr.AmbushMovedTotal - pa) * zpp).Append(';').Append(act).Append(';').Append(string.Join("+", tg)).Append('\n');
                    totalSiege += dr.SiegeMovedTotal - ps;
                    pb = dr.BudgetTotal; ps = dr.SiegeMovedTotal; pd = dr.DriftMovedTotal; pa = dr.AmbushMovedTotal;
                }
                string dump = Environment.GetEnvironmentVariable("TBD_DUMP_DIR");
                if (!string.IsNullOrEmpty(dump)) File.WriteAllText(Path.Combine(dump, "director_golpes_por_dia.csv"), sb2.ToString());
                check(totalSiege > 0 && wm2.Director.BudgetTotal > 0, $"medición del director en 20 días: {wm2.Director.BudgetTotal * zpp} zombis de presupuesto, {wm2.Director.SiegeMovedTotal * zpp} a asedios, {wm2.Director.DriftMovedTotal * zpp} a deriva, {wm2.Director.AmbushMovedTotal * zpp} a emboscadas");
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
