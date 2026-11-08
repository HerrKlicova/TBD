using System;
using System.Collections.Generic;
using System.Diagnostics;
using TBD.Net;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>[29] Noche del 7-nov: matemática entera, pelotones (dentro del hash), vehículo y helicóptero (locales).</summary>
    static class UnidadesTests
    {
        sealed class Zed { public int Id, X, Y, Hp = 100; }

        static List<Target> ToTargets(List<Zed> z) { var l = new List<Target>(z.Count); foreach (var a in z) if (a.Hp > 0) l.Add(new Target { Id = a.Id, X = a.X, Y = a.Y }); return l; }

        static int Apply(StepResult r, List<Zed> z, UnitsWorld w)
        {
            int kills = 0;
            foreach (var h in r.Hits)
                foreach (var a in z) if (a.Id == h.TargetId && a.Hp > 0) { a.Hp -= h.Damage; if (a.Hp <= 0) { kills++; w.NoteKill(h.Platoon); } break; }
            return kills;
        }

        /// <summary>Informe de calibración (no es una prueba): TBD_NIGHT_REPORT=1. Una noche con el bot de reabastecimiento limitado a N recargas en total (el jugador de la zona hizo 3).</summary>
        /// <summary>TBD_VEH_REPORT=1: muchas partidas con NightBot, con y sin el fuego de los vehículos (Mak, 7-nov). TBD_SEEDS=N (por defecto 20; Hugo: 1000), TBD_DIF=facil|normal|dificil|todas.</summary>
        public static void VehReport()
        {
            string jp = Environment.GetEnvironmentVariable("TBD_NOCHE_JSON");      // otra noche_sagunto.json (p. ej. la de 6 oleadas, para calibrar con la qa25)
            string json = jp != null ? System.IO.File.ReadAllText(jp) : TBD.Net.BalanceSource.Read("noche_sagunto.json");
            int n = 20; int.TryParse(Environment.GetEnvironmentVariable("TBD_SEEDS") ?? "20", out n); if (n <= 0) n = 20;
            string difs = Environment.GetEnvironmentVariable("TBD_DIF") ?? "todas";
            int climb = -1; int.TryParse(Environment.GetEnvironmentVariable("TBD_CLIMB") ?? "-1", out climb);   // ClimbPileZ (−1 = el del código o el JSON)
            int climbR = -1; int.TryParse(Environment.GetEnvironmentVariable("TBD_CLIMB_R") ?? "-1", out climbR);   // ClimbPermilS
            int climbMax = -1; int.TryParse(Environment.GetEnvironmentVariable("TBD_CLIMB_MAX") ?? "-1", out climbMax);   // tope de trepadores por segundo (Noche58a: 55)
            int trainX0 = 0; int.TryParse(Environment.GetEnvironmentVariable("TBD_TRAIN_X0") ?? "0", out trainX0);   // cola del tren en mm; el tren mide 64 m (74000 = como en la qa25)
            string vehs = Environment.GetEnvironmentVariable("TBD_VEH") ?? "ambos";
            // comparación con la zona (8-nov): TBD_TGT_M = radio de blancos alrededor de los pelotones (la zona usa 60), TBD_FUERZA=zona (posiciones de InitNight), TBD_BOT=no (sin órdenes, solo la recarga automática al 30 %)
            int seed0 = 0; int.TryParse(Environment.GetEnvironmentVariable("TBD_SEED0") ?? "0", out seed0);   // Sara: desplaza las semillas para partir una corrida en varios procesos
            bool tramos = Environment.GetEnvironmentVariable("TBD_TRAMOS") == "si" || Environment.GetEnvironmentVariable("TBD_TRAMOS") == "sinreparar", repairOn = Environment.GetEnvironmentVariable("TBD_TRAMOS") == "si";   // D-N163: muro por tramos con el NightBot reparando
            int tgtM = 0; int.TryParse(Environment.GetEnvironmentVariable("TBD_TGT_M") ?? "0", out tgtM);
            bool zoneForce = Environment.GetEnvironmentVariable("TBD_FUERZA") == "zona", noBot = Environment.GetEnvironmentVariable("TBD_BOT") == "no";
            // D-N165 (8-nov): TBD_PIEZAS=sagunto carga los tramos con las piezas reales de Works.Snapshot(Sagunto) de NetWorld.Mvp(); TBD_VIDA_U = vida por u (A 10000, B 13010)
            bool piezas = Environment.GetEnvironmentVariable("TBD_PIEZAS") == "sagunto"; int vidaU = 10000; int.TryParse(Environment.GetEnvironmentVariable("TBD_VIDA_U") ?? "10000", out vidaU);
            var sagPieces = piezas ? SaguntoPieces(vidaU) : null;
            string extra = Environment.GetEnvironmentVariable("TBD_EXTRA") ?? "";
            int atkX = 100000, atkY = 34000; { var a = Environment.GetEnvironmentVariable("TBD_ATAQUE"); if (a != null) { var pp = a.Split(','); atkX = int.Parse(pp[0]) * 1000; atkY = int.Parse(pp[1]) * 1000; } }   // D-N172: punto de ataque del rescate (m)
            int aggro = 0; int.TryParse(Environment.GetEnvironmentVariable("TBD_DESVIO_M") ?? "0", out aggro);   // propuesta: la horda de fuera se desvía hacia la tropa a ≤ N m
            bool atalayas = Environment.GetEnvironmentVariable("TBD_ATALAYAS") == "1";
            bool dual = Environment.GetEnvironmentVariable("TBD_DISPARO") == "doble";
            string nh90 = Environment.GetEnvironmentVariable("TBD_NH90") ?? "";   // D-N173 (Hugo): 0 | 1 | 1p | 2p — NH90 desde Bétera, despegan con la pila > 150; «p» = llevan pelotón   // D-N171 (Lucía): refuerzo de Nules en el tutorial: «fus» = un pelotón de fusileros más; «fus+lince» = y un Lince (modelado como VAMTAC, supuesto de Hugo)
            bool los = Environment.GetEnvironmentVariable("TBD_LOS") == "1";   // línea de visión: el edificio y el tren tapan (propuesta, 8-nov)
            var losScene = los ? LosScene(Environment.GetEnvironmentVariable("TBD_LOS_CSV")) : null;
            int guard = 0; int.TryParse(Environment.GetEnvironmentVariable("TBD_GUARDIA") ?? "0", out guard);   // D-N168: pelotones de combate que se quedan dentro, junto al tren (el NightBot no los mueve)
            int spread = 0; int.TryParse(Environment.GetEnvironmentVariable("TBD_EMPUJE") ?? "0", out spread);   // D-N165 (b): PileSpreadPermil
            if (piezas) { long tot = 0; foreach (var q in sagPieces) tot += q.HpMax; Console.WriteLine($"# tramos de Works.Snapshot(Sagunto): {sagPieces.Count}, vida total {tot} a {vidaU} por u"); tramos = true; }
            Console.WriteLine("dificultad;vehiculos;partidas;victorias;municion_p10_u;muro_medio_pct;pila_max_media;abatidos_vehiculos_medio;recargas_vehiculos_medio;municion_final_media_u;bajas_medias;derrotas_tren;derrotas_estacion;tren_final_medio_pct;estacion_final_media_pct;trepan_medio;muro_p50_pct;sin_acabar;con_brecha;brecha_min_p50;brecha_hora_p50;brecha_tramo_moda;brecha_tramo_moda_n;reparados_medio;reconstruidos_medio;vida_reparada_media;tren_cae_min_p50;pila_max_p50;pila_max_p90;al_tren_pico_medio;al_tren_distintos_p50;nh90_vueltas_medio");
            foreach (var dif in difs == "todas" ? new[] { "facil", "normal", "dificil" } : new[] { difs })
                foreach (bool veh in vehs == "si" ? new[] { true } : vehs == "no" ? new[] { false } : new[] { false, true })
                {
                    bool c90 = Environment.GetEnvironmentVariable("TBD_C90") == "1"; var c90Max = new List<int>(); var c90Hits = new long[C90Umbrales.Length]; var c90Buf = new List<Target>();   // Hugo (8-nov): grupo máximo a tiro de C90, cada segundo
                    long losChecks = 0, losTicks = 0; long reinfN = 0, reinfDead = 0, reinfMen = 0, reinfNever = 0, byPile = 0, byTime = 0, reinfWiped = 0; var nearPeaks = new List<int>();
                    int wins = 0; var ammos = new List<int>(); long wall = 0, pile = 0, vk = 0, vr = 0, ammo = 0, deaths = 0, trainF = 0, stF = 0, climbed = 0; int lostT = 0, lostS = 0; var walls = new List<int>(); int unfinished = 0; var brkTicks = new List<int>(); var brkSegs = new Dictionary<string, int>(); long repN = 0, rebN = 0, repHp = 0, nhRet = 0; var trainFall = new List<int>(); var piles = new List<int>(); var touched = new List<int>(); long tPeak = 0;
                    for (int i = 0; i < n; i++)
                    {
                        var sim = NightSim.FromJson(json, dif, (uint)(i + 1 + seed0)); _clk = sim.Night.P.ClockX; if (climb >= 0) sim.SP.ClimbPileZ = climb; if (climbR >= 0) sim.SP.ClimbPermilS = climbR; if (climbMax >= 0) sim.ClimbMaxPerS = climbMax; if (trainX0 > 0) { var tp = sim.Night.P; tp.TrainRect = new RectMm(trainX0, tp.TrainRect.Y0, trainX0 + 64000, tp.TrainRect.Y1); sim.Night.P = tp; }
                        sim.TargetRadiusMm = tgtM * 1000; if (zoneForce) sim.AddZoneForce(); else sim.AddDefaultForce(); if (veh) sim.AddDefaultVehicles(true); if (noBot) sim.AutoPlatoonResupply = true; else sim.Bot = NightBot.FromParams(sim.SP); if (guard > 0 && sim.Bot != null) GuardTrain(sim, guard); if (extra == "fus" || extra == "fus+lince") sim.Units.AddPlatoon(PlatoonKind.Fusileros, 86000, 73000, 0); if (extra.Contains("lince") && veh) { var lv = NightVehicle.Make("Lince", NightVehKind.Vamtac, 74000, 64500); lv.Fire = true; sim.Vehicles.Add(lv); NightVehicle.ApplyParams(lv, sim.VehicleParamsJson); } if (tramos) { if (sagPieces != null) sim.EnableSegments(sagPieces); else sim.EnableSegments(); sim.AutoRepair = repairOn; sim.PileSpreadPermil = spread; } if (nh90 != "") { sim.Vehicles.RemoveAll(q => q.Kind == NightVehKind.Nh90); int nN = nh90 == "0" ? 0 : nh90.StartsWith("2") ? 2 : 1;
                            for (int h = 0; h < nN; h++) { var hv = NightVehicle.Make("NH90 " + (h + 1), NightVehKind.Nh90, 180000, 44000); hv.Fire = true; NightVehicle.ApplyParams(hv, sim.VehicleParamsJson); hv.CarriesPlatoon = nh90.EndsWith("p"); sim.Vehicles.Add(hv); }
                            sim.AirfieldM = 18100; sim.Nh90StartAtAirfield(); foreach (var hv in sim.Vehicles) if (hv.Kind == NightVehKind.Nh90) hv.Parked = true; }      /* D-N173: Bétera a 18,1 km; esperan a la orden (pila > 150) */ sim.OutsideAggroMm = aggro * 1000; if (los) { sim.LosWalls = Environment.GetEnvironmentVariable("TBD_LOS_MURO") != "no"; sim.EnableLineOfSight(true, losScene); if (sim.Bot != null) sim.Bot.BanquetaMm = 1500;
                            if (atalayas) foreach (var at in new[] { (73500, 47500), (73500, 81500) }) { var tp = sim.Units.AddPlatoon(PlatoonKind.Tiradores, at.Item1, at.Item2, 0); tp.EyeMm = 5600; if (sim.Bot != null) sim.Bot.Busy.Add(tp.Id); } }   // Álvaro: atalayas de 4 m (boca a 5,6 m), 2 tiradores cada una
                        int brk = -1, brkSeg = -1;
                        bool heliGo = false; int firstContact = -1;
                        bool rescate = extra.StartsWith("rescate+"); int reinfAt = extra.StartsWith("nules+") || rescate ? -1 : -2, reinfDelayS = extra.StartsWith("nules+") ? int.Parse(extra.Substring(6)) : rescate ? int.Parse(extra.Substring(8)) : 0, reinfId = -1, nearPeak = 0;   // TBD_EXTRA=nules+<s>: retardo en s reales desde pila > 150
                        int c90Last = -100000; int[] c90Last4 = { -100000, -100000, -100000, -100000 }; int c90Sec = 0;
                        while (sim.Night.State == NightState.Jugando && sim.Night.Tick < sim.TickCap)
                        {
                            sim.Run(c90 || reinfAt != -2 || reinfId >= 0 || (nh90 != "" && !heliGo) || (tramos && brk < 0) ? 30 : 300);
                            if (nh90 != "" && !heliGo && sim.LastPile > 150) { heliGo = true; for (int h = 0; h < sim.Vehicles.Count; h++) if (sim.Vehicles[h].Kind == NightVehKind.Nh90) sim.TakeOffOrder(h); }
                            if (firstContact < 0 && sim.LastPile > 0) firstContact = sim.Night.Tick;
                            if (reinfAt == -1 && (sim.LastPile > 150 || (dual && firstContact >= 0 && sim.Night.Tick >= firstContact + 60 * IMath.TickRate))) { if (sim.LastPile > 150) byPile++; else byTime++; reinfAt = sim.Night.Tick + reinfDelayS * IMath.TickRate; }   // Lucía: pila > 150 o 60 s tras el primer contacto      // D-N172 (Hugo, capítulo 3): el tren de Nules llega en la primera tromba
                            if (reinfAt >= 0 && sim.Night.Tick >= reinfAt) { var tr = sim.Night.P.TrainRect; if (rescate) { var rp0 = sim.Units.AddPlatoon(PlatoonKind.Fusileros, 192000, 60000, 0); reinfId = rp0.Id; sim.Units.Move(rp0.Id, atkX, atkY); if (sim.Bot != null) sim.Bot.Busy.Add(rp0.Id); }   // D-N172: baja en la puerta este (192; 60) y ataca por el flanco sur desde (100; 34)
                                else reinfId = sim.Units.AddPlatoon(PlatoonKind.Fusileros, sim.SP.FortX0 - 6000, tr.Y1 + 3000, 0).Id; reinfAt = -2; }
                            if (reinfId >= 0) { var rq = sim.Units.Platoons.Find(q => q.Id == reinfId); if (rq.AliveCount() > 0) { sim.ExportZombies(c90Buf); int nz = 0; foreach (var z in c90Buf) { long ddx = z.X - rq.AX, ddy = z.Y - rq.AY; if (ddx * ddx + ddy * ddy <= 15000L * 15000L) nz++; } if (nz > nearPeak) nearPeak = nz; } }   // baja fuera, a la espalda de la horda, junto a la vía oeste
                            if (c90) { int m = C90Cluster(sim, c90Buf); if (m >= 0) { c90Max.Add(m); for (int q = 0; q < C90Umbrales.Length; q++) if (m >= C90Umbrales[q] && c90Sec - c90Last4[q] >= 10) { c90Last4[q] = c90Sec; c90Hits[q]++; } } c90Sec++; }
                            if (tramos && brk < 0) for (int k = 0; k < sim.Segments.Count; k++) if (sim.Segments[k].Hp == 0) { brk = sim.Night.Tick; brkSeg = k; break; }
                        }
                        losChecks += sim.Units.LosChecks; losTicks += sim.Night.Tick;
                        if (reinfId >= 0) { var rp = sim.Units.Platoons.Find(q => q.Id == reinfId); reinfN++; reinfDead += rp.Men.Count - rp.AliveCount(); reinfMen += rp.Men.Count; nearPeaks.Add(nearPeak); if (rp.AliveCount() == 0) reinfWiped++; } else if (extra.StartsWith("nules+") || extra.StartsWith("rescate+")) reinfNever++;
                        if (sim.Unfinished) unfinished++; repN += sim.Repaired; rebN += sim.Rebuilt; repHp += sim.RepairHp;
                        if (sim.Night.State == NightState.Derrota && sim.Night.EndedByTrain) trainFall.Add(sim.Night.DefeatTick); piles.Add(sim.PeakPile); touched.Add(sim.TrainTouched); tPeak += sim.TrainPeakAttackers; foreach (var vv in sim.Vehicles) nhRet += vv.AutoReturns;
                        if (brk >= 0) { brkTicks.Add(brk); string key = SegLabel(sim.Segments[brkSeg], brkSeg); int c; brkSegs.TryGetValue(key, out c); brkSegs[key] = c + 1; }
                        if (Environment.GetEnvironmentVariable("TBD_POR_PARTIDA") == "1") Console.Error.WriteLine($"P;{i + 1 + seed0};{sim.Night.State};{sim.Climbed};{sim.AmmoMilliU()};{sim.Night.Tick};{sim.Night.Hour:00}:{sim.Night.Minute:00};{sim.PeakPile}");   // una línea por partida (Hugo: contraste con la build)
                        if (sim.Night.State == NightState.Victoria) wins++; ammos.Add(sim.AmmoMilliU());
                        if (sim.Night.State == NightState.Derrota) { if (sim.Night.EndedByTrain) lostT++; if (sim.Night.EndedByStation) lostS++; }
                        trainF += (long)sim.Night.TrainHp * 100 / Math.Max(1, sim.Night.P.TrainHp); stF += (long)sim.Night.StationHp * 100 / Math.Max(1, sim.Night.P.StationHp); climbed += sim.Climbed; walls.Add((int)((long)sim.WallHp * 100 / Math.Max(1, sim.SP.WallHp)));
                        wall += (long)sim.WallHp * 100 / Math.Max(1, sim.SP.WallHp); pile += sim.PeakPile; vk += sim.VehicleKills; vr += sim.VehicleResupplies; ammo += sim.AmmoMilliU(); deaths += sim.SoldierDeaths;
                    }
                    if (c90 && c90Max.Count > 0) { var cs = new List<int>(c90Max); cs.Sort(); Console.WriteLine($"# C90 {dif}: {cs.Count} segundos con pelotón de C90; zombis en 5 m (centro a ≤30 m del pelotón) p50 {cs[cs.Count / 2]} p90 {cs[cs.Count * 9 / 10]} p99 {cs[cs.Count * 99 / 100]} máx {cs[cs.Count - 1]}"); for (int q = 0; q < C90Umbrales.Length; q++) Console.WriteLine($"# C90 umbral {C90Umbrales[q]}: {c90Hits[q] / (double)n:0.00} avisos por noche (10 s entre avisos)"); }
                    if (extra.StartsWith("nules+") || extra.StartsWith("rescate+")) Console.WriteLine($"# Nules {extra}: zombis a 15 m del pelotón (pico por partida) p50 {(nearPeaks.Count > 0 ? Median(nearPeaks) : 0)} p90 {(nearPeaks.Count > 0 ? P90(nearPeaks) : 0)}; pelotón aniquilado en {reinfWiped}; llega en {reinfN} partidas (nunca en {reinfNever}; salta por pila {byPile}, por tiempo {byTime}); bajas del pelotón de Nules {(reinfN > 0 ? reinfDead * 10 / reinfN / 10.0 : 0):0.0} de {(reinfN > 0 ? reinfMen / reinfN : 0)} de media");
                    if (los) Console.WriteLine($"# LOS {dif}: {losChecks / Math.Max(1, losTicks)} comprobaciones de sólido por tick de media");
                    Console.WriteLine($"{dif};{(veh ? "si" : "no")};{n};{wins};{P10(ammos) / 1000.0:0.0};{wall / n};{pile / n};{vk / n};{(vr * 10 / n) / 10.0:0.0};{(ammo / n) / 1000.0:0.0};{(deaths * 10 / n) / 10.0:0.0};{lostT};{lostS};{trainF / n};{stF / n};{climbed / n};{Median(walls)};{unfinished};{brkTicks.Count};{BreachMin(brkTicks)};{BreachHour(brkTicks)};{Mode(brkSegs)};{(repN * 10 / n) / 10.0:0.0};{(rebN * 10 / n) / 10.0:0.0};{repHp / n};{BreachMin(trainFall)};{Median(piles)};{P90(piles)};{tPeak / n};{Median(touched)};{(nhRet * 10 / n) / 10.0:0.0}");
                }
        }

        static string BreachMin(List<int> t) { if (t.Count == 0) return "-"; var c = new List<int>(t); return (Median(c) / 30.0 / 60).ToString("0.0"); }   // minutos reales desde las 22:00
        static string BreachHour(List<int> t) { if (t.Count == 0) return "-"; var c = new List<int>(t); long gs = (long)Median(c) * _clk / 30; return $"{(22 + gs / 3600) % 24:00}:{gs / 60 % 60:00}"; }   // al reloj de la noche
        static int _clk = 28;
        static string Mode(Dictionary<string, int> d) { string b = "-"; int bn = 0; foreach (var kv in d) if (kv.Value > bn || (kv.Value == bn && string.CompareOrdinal(kv.Key, b) < 0)) { b = kv.Key; bn = kv.Value; } return b + ";" + bn; }
        static string SegLabel(NightSim.WallSegment g, int i) { string[] side = { "oeste", "este", "sur", "norte" }; return $"{i}:{side[g.Side]}_{g.A0 / 1000.0:0.#}-{g.A1 / 1000.0:0.#}"; }

        /// <summary>D-N165: los tramos reales de Sagunto (NetWorld.Mvp(), D-N63) en mm de NightSim (mundo = local + (113.000; 64.500)). Vida = precio × vidaPorU.
        /// Lado 0 oeste, 1 este, 2 sur, 3 norte; ordenados por lado y por A0, sin huecos. Las esquinas son de los lados oeste y este (como SegmentAt).</summary>
        /// <summary>D-N174: sólidos que tapan la vista, del inventario de Álvaro (docs/niveles/sagunto_obstaculos_v1.csv), menos los muros y las puertas (van por tramos, con su brecha).
        /// Los nidos, a 1,5 m del muro (Lucía) y como muro propio. null si no hay archivo (entonces, el edificio y el tren de NightParams).</summary>
        static List<UnitsWorld.LosBlocker> LosScene(string path)
        {
            if (path == null || !System.IO.File.Exists(path)) return null;
            var l = new List<UnitsWorld.LosBlocker>(); bool head = true;
            foreach (var line in System.IO.File.ReadAllLines(path))
            {
                if (head) { head = false; continue; }
                var c = line.Split(';'); if (c.Length < 10) continue;
                string tipo = c[1]; if (tipo.StartsWith("muro") || tipo == "puerta" || c[9] != "si") continue;
                int x0 = int.Parse(c[3]), y0 = int.Parse(c[4]), x1 = int.Parse(c[5]), y1 = int.Parse(c[6]), hg = int.Parse(c[7]), h = int.Parse(c[8]); if (h <= 0) continue;
                if (tipo == "nido") { x0 -= 1500; x1 -= 1500; }
                var r = c[2] == "seg" ? new RectMm(Math.Min(x0, x1) - hg, Math.Min(y0, y1) - hg, Math.Max(x0, x1) + hg, Math.Max(y0, y1) + hg) : new RectMm(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
                l.Add(new UnitsWorld.LosBlocker { R = r, HeightMm = h, OwnWall = tipo == "nido" });
            }
            return l;
        }

        public static readonly int[] C90Umbrales = { 12, 20, 25, 30 };
        /// <summary>El grupo más grande que cabe en un círculo de 5 m (el radio del C90) con centro en un zombi a 30 m o menos de un pelotón con C90 (C90Left &gt; 0). −1 si no hay pelotón con C90.
        /// Centro en un zombi: aproximación por abajo del máximo continuo.</summary>
        static int C90Cluster(NightSim sim, List<Target> buf)
        {
            const long R = 5000, A = 30000; bool any = false; int best = 0;
            buf.Clear(); sim.ExportZombies(buf);
            foreach (var p in sim.Units.Platoons)
            {
                if (p.C90Left <= 0 || p.Aboard != 0 || p.AliveCount() == 0) continue; any = true;
                var near = new List<Target>();
                foreach (var z in buf) { long dx = z.X - p.AX, dy = z.Y - p.AY; if (dx * dx + dy * dy <= (A + R) * (A + R)) near.Add(z); }
                var grid = new Dictionary<long, List<Target>>();
                foreach (var z in near) { long key = ((long)Math.Floor(z.X / (double)R) << 32) ^ (uint)(int)Math.Floor(z.Y / (double)R); List<Target> l; if (!grid.TryGetValue(key, out l)) grid[key] = l = new List<Target>(); l.Add(z); }
                foreach (var c in near)
                {
                    long dx = c.X - p.AX, dy = c.Y - p.AY; if (dx * dx + dy * dy > A * A) continue;
                    int gx = (int)Math.Floor(c.X / (double)R), gy = (int)Math.Floor(c.Y / (double)R), cnt = 0;
                    for (int ix = gx - 1; ix <= gx + 1; ix++) for (int iy = gy - 1; iy <= gy + 1; iy++)
                    { List<Target> l; if (!grid.TryGetValue(((long)ix << 32) ^ (uint)iy, out l)) continue; foreach (var z in l) { long ex = z.X - c.X, ey = z.Y - c.Y; if (ex * ex + ey * ey <= R * R) cnt++; } }
                    if (cnt > best) best = cnt;
                }
            }
            return any ? best : -1;
        }

        public static List<NightSim.WallSegment> SaguntoPieces(int vidaPorU, System.Text.StringBuilder dump = null)
        {
            var w = NetScenario.Mvp(); int sag = -1; foreach (var nd in w.Graph.Nodes) if (nd.Name == "Sagunto") sag = nd.Id;
            var list = new List<NightSim.WallSegment>();
            foreach (var p in w.Works.Snapshot(sag))
            {
                if (!(p.IsWall || p.IsGate)) continue;
                int x0 = p.X0 + 113000, x1 = p.X1 + 113000, y0 = p.Y0 + 64500, y1 = p.Y1 + 64500; int side, a0, a1;
                if (x0 == x1) { side = x0 < 113000 ? 0 : 1; a0 = Math.Min(y0, y1); a1 = Math.Max(y0, y1); } else { side = y0 < 64500 ? 2 : 3; a0 = Math.Min(x0, x1); a1 = Math.Max(x0, x1); }
                long hp = (long)p.CostE4 * vidaPorU / 10000;
                list.Add(new NightSim.WallSegment { Side = side, A0 = a0, A1 = a1, HpMax = (int)hp, Hp = -1, PriceU = (p.CostE4 + 9999) / 10000, HeightMm = TBD.Net.Obras.HeightMm(p.Kind) });
                if (dump != null) dump.AppendLine($"{p.Kind};{side};{a0};{a1};{a1 - a0};{p.CostE4 / 10000.0:0.####};{hp}");
            }
            list.Sort((a, b) => a.Side != b.Side ? a.Side.CompareTo(b.Side) : a.A0.CompareTo(b.A0));
            return list;
        }

        /// <summary>TBD_CALIB=1 (Hugo, Noche58a): con la fuerza y los blancos de la zona, el NightBot y los vehículos, entradas acumuladas (Climbed) a las 22:35 y 22:55 y hora de caída del tren.
        /// TBD_CLIMB_R (‰/s), TBD_CLIMB_MAX (tope por s, 0 = sin tope), TBD_SEEDS. Objetivo: ~773 y ~2612, tren hacia las 22:55.</summary>
        public static void CalibReport()
        {
            string json = TBD.Net.BalanceSource.Read("noche_sagunto.json");
            int n = 30; int.TryParse(Environment.GetEnvironmentVariable("TBD_SEEDS") ?? "30", out n);
            int r = 20; int.TryParse(Environment.GetEnvironmentVariable("TBD_CLIMB_R") ?? "20", out r);
            int cap = 0; int.TryParse(Environment.GetEnvironmentVariable("TBD_CLIMB_MAX") ?? "0", out cap);
            var a35 = new List<int>(); var a55 = new List<int>(); var fall = new List<int>(); var piles = new List<int>(); int wins = 0;
            for (int i = 0; i < n; i++)
            {
                var sim = NightSim.FromJson(json, "normal", (uint)(i + 1)); sim.SP.ClimbPermilS = r; _clk = sim.Night.P.ClockX; sim.ClimbMaxPerS = cap; sim.TargetRadiusMm = 60000; sim.AddZoneForce(); sim.AddDefaultVehicles(true); sim.Bot = NightBot.FromParams(sim.SP);
                int cx = sim.Night.P.ClockX, t35 = 35 * 60 * 30 / cx, t55 = 55 * 60 * 30 / cx; int c35 = -1, c55 = -1;
                while (sim.Night.State == NightState.Jugando && sim.Night.Tick < sim.TickCap) { sim.Run(1); if (sim.Night.Tick == t35) c35 = sim.Climbed; if (sim.Night.Tick == t55) c55 = sim.Climbed; }
                if (c35 < 0) c35 = sim.Climbed; if (c55 < 0) c55 = sim.Climbed;
                a35.Add(c35); a55.Add(c55); piles.Add(sim.PeakPile); if (sim.Night.State == NightState.Victoria) wins++;
                if (sim.Night.State == NightState.Derrota && sim.Night.EndedByTrain) fall.Add(sim.Night.DefeatTick);
            }
            Console.WriteLine($"climb_r;tope;partidas;victorias;entran_2235_p50;entran_2255_p50;tren_cae_n;tren_cae_hora_p50;pila_max_p50");
            Console.WriteLine($"{r};{cap};{n};{wins};{Median(a35)};{Median(a55)};{fall.Count};{BreachHour(fall)};{Median(piles)}");
        }

        /// <summary>D-N168: los «k» primeros pelotones de combate (ametralladores, granaderos, tiradores, fusileros) van junto al tren, por el norte (y = tren + 2,5 m),
        /// repartidos a lo largo, y el NightBot no los mueve (Busy). Disparan y recargan como los demás.</summary>
        static void GuardTrain(NightSim sim, int k)
        {
            var order = new[] { PlatoonKind.Ametralladores, PlatoonKind.Granaderos, PlatoonKind.Tiradores, PlatoonKind.Fusileros };
            var tr = sim.Night.P.TrainRect; int n = 0;
            foreach (var kind in order)
            {
                if (n >= k) break; var p = sim.Units.Platoons.Find(q => q.Kind == kind); if (p == null) continue;
                int x = tr.X0 + (int)((long)(tr.X1 - tr.X0) * (2 * n + 1) / (2 * Math.Max(1, k))), y = tr.Y1 + 2500;
                sim.Units.Move(p.Id, x, y); sim.Bot.Busy.Add(p.Id); n++;
            }
        }

        static int P90(List<int> v) { if (v.Count == 0) return 0; v.Sort(); return v[Math.Min(v.Count - 1, v.Count * 9 / 10)]; }
        static int P10(List<int> v) { if (v.Count == 0) return 0; v.Sort(); return v[v.Count / 10]; }
        static int Median(List<int> v) { if (v.Count == 0) return 0; v.Sort(); return v[v.Count / 2]; }

        /// <summary>TBD_BOT_REPORT=1: noche entera con el NightBot (sin bot de recarga aparte), por dificultad y semilla, para comparar con la fuerza fija por defecto.</summary>
        public static void BotReport()
        {
            string json = TBD.Net.BalanceSource.Read("noche_sagunto.json");
            foreach (var dif in new string[] { "facil", null, "dificil" })
                foreach (uint seed in new uint[] { 1, 2, 3 })
                    foreach (bool useBot in new bool[] { false, true })
                    {
                        var sim = NightSim.FromJson(json, dif, seed); sim.AddDefaultForce(); if (useBot) sim.Bot = NightBot.FromParams(sim.SP);
                        while (sim.Night.State == NightState.Jugando && sim.Night.Tick < sim.TickCap) { sim.Run(150); if (!useBot) sim.ResupplyBot(); }
                        var s = sim.Snapshot();
                        Console.WriteLine($"[{dif ?? "normal"} s{seed} {(useBot ? "NightBot" : "fija+bot recarga")}] {s.State} {s.Hour:00}:{s.Minute:00} muro {s.WallHp * 100 / Math.Max(1, sim.SP.WallHp)} % bajas {s.SoldierDeaths} munición {(s.PlatoonAmmoShots + s.StockShots) / 2000.0:0.0} u recargas {s.Resupplies} pila máx {s.PeakPile}{(useBot ? " | " + sim.Bot.Describe() : "")}");
                    }
        }

        /// <summary>TBD_WAVE_REPORT=1: por oleada (normal, semilla 1, con NightBot o fuerza fija), ticks hasta el primer contacto con el muro, pila máxima, abatidos, recargas y munición. Variantes de velocidad del zombi para comparar con la zona.</summary>
        public static void WaveReport()
        {
            string json = TBD.Net.BalanceSource.Read("noche_sagunto.json");
            foreach (int gapMm in new int[] { 0, 3200 })
                foreach (bool useBot in new bool[] { true, false })
                {
                    var sim = NightSim.FromJson(json, null, 1); sim.SP.GateGapMm = gapMm; sim.AddDefaultForce(); if (useBot) sim.Bot = NightBot.FromParams(sim.SP);
                    int firstPile = -1;
                    while (sim.Night.State == NightState.Jugando && sim.Night.Tick < sim.TickCap) { sim.Run(30); if (firstPile < 0 && sim.PeakPile > 0) firstPile = sim.Night.Tick; if (!useBot) sim.ResupplyBot(); }
                    Console.WriteLine($"-- hueco de la vía {gapMm} mm, {(useBot ? "NightBot" : "fija")}: {sim.Night.State} a las {sim.Night.Hour:00}:{sim.Night.Minute:00}, edificio {sim.StationHp}, tren {sim.Night.TrainHp}, muro {sim.WallHp * 100 / Math.Max(1, sim.SP.WallHp)} %, primer contacto con el muro en el tick {firstPile}");
                    foreach (var w in sim.Waves)
                        Console.WriteLine($"   oleada {w.Wave} {w.HourAtStart:00}:{w.MinuteAtStart:00} zombis {w.Spawned} abatidos {w.Kills} pila máx {w.PeakPile} recargas {w.Resupplies} munición gastada {w.AmmoSpentShots / 2000.0:0.0} u bajas {w.SoldierDeaths} vivos al cierre {w.AliveAtEnd} duración {(w.EndTick < 0 ? -1 : (w.EndTick - w.StartTick) / 30)} s");
                }
        }

        /// <summary>TBD_KILL_TIMELINE=1: abatidos de la oleada 1 a las 22:25, 22:44 y 22:54 (los hitos que mide la zona), con el bot y con la fuerza fija, y velocidades de zombi.</summary>
        public static void KillTimeline()
        {
            string json = TBD.Net.BalanceSource.Read("noche_sagunto.json");
            foreach (int spd in new int[] { 1500, 1600 })
                foreach (bool useBot in new bool[] { true, false })
                {
                    var sim = NightSim.FromJson(json, null, 1); sim.SP.ZombieSpeedMmS = spd; sim.AddDefaultForce(); if (useBot) sim.Bot = NightBot.FromParams(sim.SP);
                    int t0 = sim.Night.WaveStartTick(0); var line = new System.Text.StringBuilder(); int firstK = -1;
                    foreach (int min in new int[] { 0, 5, 10, 24, 34, 44 })
                    {
                        int target = t0 + (int)(min * 60L * 30 / 28);
                        while (sim.Night.Tick < target && sim.Night.State == NightState.Jugando) { sim.Run(1); if (firstK < 0 && sim.Kills > 0) firstK = sim.Night.Tick - t0; if (useBot == false) sim.ResupplyBot(); }
                        line.Append($" 22:{20 + min:00}→{sim.Kills}/{sim.Spawned} (pila {sim.LastPile})");
                    }
                    Console.WriteLine($"{spd} mm/s {(useBot ? "NightBot" : "fija")}: primer abatido a los {firstK / 30.0:0.0} s;{line}");
                }
        }

        public static void NightReport()
        {
            string json = TBD.Net.BalanceSource.Read("noche_sagunto.json");
            foreach (var dif in new string[] { "facil", null, "dificil" })
                foreach (int cap in new int[] { 1000, 8, 5, 3 })
                {
                    var sim = NightSim.FromJson(json, dif, 3); sim.AddDefaultForce(); int used = 0;
                    while (sim.Night.State == NightState.Jugando && sim.Night.Tick < sim.TickCap)
                    {
                        sim.Run(150);
                        foreach (var p in sim.Units.Platoons) if (used < cap && p.AmmoCap > 0 && p.AmmoShots * 100 < p.AmmoCap * 30) if (sim.Resupply(p.Id) > 0) used++;
                    }
                    var s = sim.Snapshot();
                    Console.WriteLine($"[{dif ?? "normal"} recargas<={cap}] {s.State} a las {s.Hour:00}:{s.Minute:00} muro {s.WallHp * 100 / Math.Max(1, sim.SP.WallHp)} % edificio {sim.Night.StationHp} tren {sim.Night.TrainHp} bajas {s.SoldierDeaths} munición {s.PlatoonAmmoShots + s.StockShots} abatidos {s.Kills}/{s.ZombiesSpawned} recargas {used}");
                }
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[29] Infantería por pelotones, vehículo de tierra y helicóptero (noche del 7-nov)");

            // ------------------------------------------------------------------ matemática entera
            {
                double maxS = 0; for (int a = 0; a < 65536; a += 7) maxS = Math.Max(maxS, Math.Abs(IMath.Sin(a) / 65536.0 - Math.Sin(a * 2 * Math.PI / 65536)));
                check(maxS < 3e-4, $"seno por tabla entera: error máximo {maxS:E1} (< 3e-4)");
                double maxC = 0; for (int a = 0; a < 65536; a += 11) maxC = Math.Max(maxC, Math.Abs(IMath.Cos(a) / 65536.0 - Math.Cos(a * 2 * Math.PI / 65536)));
                check(maxC < 3e-4 && IMath.Sin(0) == 0 && IMath.Sin(16384) == 65536 && IMath.Sin(32768) == 0 && IMath.Sin(49152) == -65536, $"coseno por tabla entera ({maxC:E1}) y valores exactos en 0, 90, 180 y 270°");
                bool sq = true; for (long n = 0; n < 5000 && sq; n++) { long r = IMath.Isqrt(n); sq = r * r <= n && (r + 1) * (r + 1) > n; }
                foreach (long n in new long[] { long.MaxValue / 4, 4611686014132420609L, 999999999999L }) { long r = IMath.Isqrt(n); sq &= r * r <= n && (r + 1) * (r + 1) > n; }
                check(sq, "raíz cuadrada entera exacta (suelo) en 5.000 valores y en los extremos");
                int worst = 0;
                for (int a = 0; a < 65536; a += 13)
                {
                    int dx = IMath.Cos(a) * 3, dy = IMath.Sin(a) * 3;
                    int got = IMath.Atan2(dy, dx); worst = Math.Max(worst, Math.Abs(IMath.Diff(got, a)));
                }
                check(worst <= 70, $"atan2 entero: error máximo {worst} bam16 = {worst * 360.0 / 65536:F2}° (≤ 0,4°)");
            }

            // ------------------------------------------------------------------ formaciones
            {
                bool ok = true; string bad = "";
                foreach (Formation f in new[] { Formation.Linea, Formation.Columna, Formation.Cuna, Formation.Circulo, Formation.Bloque })
                {
                    var pts = new List<(int, int)>();
                    for (int k = 0; k < 10; k++) { int dx, dy; UnitsWorld.Slot(f, k, 10, 2200, 0, out dx, out dy); pts.Add((dx, dy)); }
                    int minD = int.MaxValue;
                    for (int i = 0; i < pts.Count; i++) for (int j = i + 1; j < pts.Count; j++) minD = Math.Min(minD, IMath.Dist(pts[i].Item1 - pts[j].Item1, pts[i].Item2 - pts[j].Item2));
                    if (minD < 1500) { ok = false; bad += $" [{f}: {minD} mm]"; }
                }
                check(ok, "las cinco formaciones (línea, columna, cuña, círculo y bloque) separan a 10 soldados al menos 1,5 m" + bad);
                {
                    int x0 = int.MaxValue, x1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue; long sx = 0, sy = 0;
                    for (int k = 0; k < 9; k++) { int dx, dy; UnitsWorld.Slot(Formation.Bloque, k, 9, 2000, 0, out dx, out dy); x0 = Math.Min(x0, dx); x1 = Math.Max(x1, dx); y0 = Math.Min(y0, dy); y1 = Math.Max(y1, dy); sx += dx; sy += dy; }
                    check(x1 - x0 == 4000 && y1 - y0 == 4000 && sx == 0 && sy == 0, $"el bloque de 9 es un cuadrado de 3 × 3 centrado en el ancla ({x1 - x0} × {y1 - y0} mm)");
                    var wb = new UnitsWorld(3); var pb = wb.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0); check(wb.SetFormation(pb.Id, Formation.Bloque) && pb.Form == Formation.Bloque, "un pelotón puede pasar a bloque");
                }
                int ax, ay, bx, by; UnitsWorld.Slot(Formation.Columna, 3, 10, 2000, 0, out ax, out ay); UnitsWorld.Slot(Formation.Columna, 3, 10, 2000, 16384, out bx, out by);
                check(ax == -6000 && Math.Abs(ay) <= 1 && Math.Abs(bx) <= 1 && by == -6000, "la columna va detrás del jefe según el rumbo (0° hacia +X, 90° hacia +Y)");
            }

            // ------------------------------------------------------------------ datos de Hugo: los valores por defecto coinciden con los CSV
            {
                var h0 = WeaponCatalog.Hash(); var ph0 = PlatoonCatalog.Hash(); var before = (Weapon[])WeaponCatalog.Table.Clone();
                WeaponCatalog.Load(new CsvTable(TBD.Net.BalanceSource.Read("balance_unidades.csv")));
                PlatoonCatalog.Load(new CsvTable(TBD.Net.BalanceSource.Read("balance_unidades.csv")));
                string wd = ""; for (int i = 0; i < before.Length; i++) foreach (var fi in typeof(Weapon).GetFields()) { var a0 = fi.GetValue(before[i]); var a1 = fi.GetValue(WeaponCatalog.Table[i]); if (!Equals(a0, a1)) wd += $" [{before[i].Key}.{fi.Name}: código {a0}, CSV {a1}]"; }
                check(WeaponCatalog.Hash() == h0, "los valores por defecto de las armas coinciden con balance_unidades.csv (si Hugo cambia el CSV, hay que actualizar Armas.cs)" + wd);
                check(PlatoonCatalog.Hash() == ph0, "los valores por defecto de los pelotones coinciden con balance_unidades.csv");
                var nd = NightParams.FromCsv(new CsvTable(TBD.Net.BalanceSource.Read("balance_noche.csv"))); var dd = NightParams.Default();
                bool same = nd.ClockX == dd.ClockX && nd.ScalePermil == dd.ScalePermil && nd.Waves.Length == dd.Waves.Length && nd.StartHour == dd.StartHour && nd.EndHour == dd.EndHour;
                for (int i = 0; same && i < nd.Waves.Length; i++) same = nd.Waves[i].StartMinute == dd.Waves[i].StartMinute && nd.Waves[i].Zombies == dd.Waves[i].Zombies && nd.Waves[i].ArrivalSeconds == dd.Waves[i].ArrivalSeconds;
                check(same, "los valores por defecto de la noche coinciden con balance_noche.csv (D-N136: una horda, ×28, escala 850 ‰)");
                var w = WeaponCatalog.Get(WeaponId.Fusil);
                check(w.RangeMm == 60000 && w.HitBlankPm == 210 && WeaponCatalog.Get(WeaponId.Lanzagranadas).RangeMm == 140000 && WeaponCatalog.Get(WeaponId.Canon30).RangeMm == 220000, "fusil 60 m y 210 ‰; lanzagranadas a 140 m y cañón de 30 mm a 220 m (cambios de Hugo de las 00:25)");
                var w2 = new UnitsWorld(1); var f = w2.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0); var a = w2.AddPlatoon(PlatoonKind.Ametralladores, 0, 0, 0); var g = w2.AddPlatoon(PlatoonKind.Granaderos, 0, 0, 0); var t = w2.AddPlatoon(PlatoonKind.Tiradores, 0, 0, 0);
                check(f.Men.Count == 8 && a.Men.Count == 6 && g.Men.Count == 6 && t.Men.Count == 2 && a.Men.FindAll(m => m.Kind == InfKind.Servidor).Count == 2 && f.AmmoCap == 3000 && a.AmmoCap == 4000, "los pelotones salen con las personas, servidores y dotación del CSV (8, 6 con 2 servidores, 6, 2; 1,5 u = 3.000 disparos)");
            }

            // ------------------------------------------------------------------ movimiento
            {
                var w = new UnitsWorld(7); var p = w.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0, Formation.Columna);
                w.Move(p.Id, 60000, 20000, 16384);
                var res = new StepResult(); var none = new List<Target>();
                int arrived = -1, maxStep = 0; var prev = new (int, int)[p.Men.Count];
                for (int i = 0; i < p.Men.Count; i++) prev[i] = (p.Men[i].X, p.Men[i].Y);
                for (int t = 0; t < 1500; t++)
                {
                    w.Step(none, res);
                    for (int i = 0; i < p.Men.Count; i++) { maxStep = Math.Max(maxStep, IMath.Dist(p.Men[i].X - prev[i].Item1, p.Men[i].Y - prev[i].Item2)); prev[i] = (p.Men[i].X, p.Men[i].Y); }
                    if (!p.Moving && arrived < 0) arrived = t;
                }
                int dist = IMath.Dist(60000, 20000), ideal = dist * IMath.TickRate / 2500;
                check(arrived > 0 && arrived >= ideal * 9 / 10 && arrived <= ideal * 14 / 10, $"el pelotón llega a 63 m: {arrived} ticks (ideal a 2,5 m/s: {ideal})");
                int worstSlot = 0; for (int i = 0; i < p.Men.Count; i++) worstSlot = Math.Max(worstSlot, IMath.Dist(p.Men[i].X - p.Men[i].SlotX, p.Men[i].Y - p.Men[i].SlotY));
                check(worstSlot < 50 && IMath.Diff(p.Heading, 16384) == 0, $"todos en su puesto al parar (máx. {worstSlot} mm) y orientados al rumbo final pedido");
                check(maxStep <= 2500 * 5 / 4 / 30 + 2, $"nadie se teletransporta: paso máximo {maxStep} mm/tick (límite {2500 * 5 / 4 / 30 + 2})");
                w.SetFormation(p.Id, Formation.Linea); for (int t = 0; t < 120; t++) w.Step(none, res);
                var xs = new HashSet<int>(); foreach (var m in p.Men) xs.Add(m.X / 500);
                check(xs.Count >= 6, $"al cambiar a línea se abren a lo ancho ({xs.Count} bandas distintas)");
            }
            {
                // mover frente a atacar en marcha frente a mantener
                Func<int, (int, int)> run = mode =>
                {
                    var w = new UnitsWorld(9); var p = w.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0, Formation.Cuna);
                    var tg = new List<Target> { new Target { Id = 1, X = 75000, Y = 0 } };   // un blanco que no muere: el pelotón lo ve siempre
                    if (mode == 0) w.Move(p.Id, 150000, 0); else if (mode == 1) w.AttackMove(p.Id, 150000, 0); else { w.Move(p.Id, 150000, 0); }
                    var res = new StepResult(); int shots = 0;
                    for (int t = 0; t < 900; t++) { if (mode == 2 && t == 120) w.Hold(p.Id); w.Step(tg, res); shots += res.Shots.Count; }
                    return (p.AX, shots);
                };
                var mv = run(0); var am = run(1); var hd = run(2);
                check(mv.Item1 > 70000 && mv.Item2 > 0, $"mover: avanza sin pararse ({mv.Item1 / 1000} m en 30 s) y dispara por el camino ({mv.Item2} disparos)");
                check(am.Item1 < 40000 && am.Item2 > 30, $"atacar en marcha: se detiene al tener el blanco a tiro (avanza solo {am.Item1 / 1000} m) y dispara ({am.Item2} disparos)");
                check(hd.Item1 < 12000, $"mantener: se queda donde está ({hd.Item1 / 1000} m)");
                // reanuda al desaparecer el blanco
                var w3 = new UnitsWorld(9); var p3 = w3.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0); w3.AttackMove(p3.Id, 60000, 0);
                var one = new List<Target> { new Target { Id = 1, X = 50000, Y = 500 } }; var r3 = new StepResult();
                for (int t = 0; t < 200; t++) w3.Step(one, r3);
                int stuck = p3.AX; for (int t = 0; t < 900; t++) w3.Step(new List<Target>(), r3);
                check(stuck < 20000 && !p3.Moving && IMath.Dist(p3.AX - 60000, p3.AY) < 200, $"al no quedar enemigos a tiro, el ataque en marcha reanuda y llega al punto (parado en {stuck / 1000} m, llegó a {p3.AX / 1000} m)");
            }

            // ------------------------------------------------------------------ armas
            {
                // puntería: bocajarro − pendiente·metros, medida con miles de disparos
                var w = new UnitsWorld(21); var p = w.AddPlatoon(PlatoonKind.Tiradores, 0, 0, 0, Formation.Cuna); p.AmmoShots = 100000000;
                var tg = new List<Target> { new Target { Id = 1, X = 20000, Y = 0 } }; var res = new StepResult();
                int fs = 0, fh = 0, ps = 0, phh = 0;
                for (int t = 0; t < 40000; t++) { w.Step(tg, res); foreach (var sh in res.Shots) { if (sh.Weapon == WeaponId.Fusil) { fs++; if (sh.Hit) fh++; } else if (sh.Weapon == WeaponId.FusilPrecision) { ps++; if (sh.Hit) phh++; } } }
                double rf = fh / (double)Math.Max(1, fs), rp = phh / (double)Math.Max(1, ps);
                check(fs > 2000 && ps > 500 && rf > 0.10 && rf < 0.21 && rp > 0.88 && rp < 0.97, $"puntería a 20 m (desde unos 20–25 m según el puesto): fusil {rf * 1000:F0} ‰ (≈ 140–150) en {fs} disparos y fusil de precisión {rp * 1000:F0} ‰ (≈ 930) en {ps}");
                var w2 = new UnitsWorld(4); var far = w2.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0); far.AmmoShots = 100000000;
                int farShots = 0, farHits = 0; var res2 = new StepResult(); var tgf = new List<Target> { new Target { Id = 1, X = 58000, Y = 0 } };
                for (int t = 0; t < 40000; t++) { w2.Step(tgf, res2); foreach (var s in res2.Shots) if (s.Weapon == WeaponId.Fusil) { farShots++; if (s.Hit) farHits++; } }
                double fr = farHits / (double)Math.Max(1, farShots);
                check(farShots > 1000 && fr > 0.02 && fr < 0.06, $"el fusil a 58 m casi no acierta ({fr * 1000:F0} ‰ frente a los 36 ‰ del modelo, {farShots} disparos)");
            }
            {
                // munición: sin disparos equivalentes no se dispara; el almacén reabastece
                var w = new UnitsWorld(2); var p = w.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0); p.AmmoShots = 5; w.StockShots = 100;
                var tg = new List<Target> { new Target { Id = 1, X = 20000, Y = 0 } }; var res = new StepResult(); int spent = 0, shots = 0;
                for (int t = 0; t < 300; t++) { w.Step(tg, res); spent += res.AmmoSpent; shots += res.Shots.Count; }
                check(spent <= 5 && p.AmmoShots >= 0 && shots > 0 && shots <= 5, $"con 5 disparos de munición, el pelotón dispara como mucho 5 veces ({shots}) y gasta {spent}");
                int got = w.Resupply(p.Id); int before = shots;
                for (int t = 0; t < 120; t++) { w.Step(tg, res); shots += res.Shots.Count; }
                check(got > 0 && w.StockShots == 100 - got && shots > before, $"reabastecer pasa {got} disparos del almacén al pelotón y vuelve a disparar");
                w.Resupply(p.Id); check(p.AmmoShots <= p.AmmoCap, "el pelotón nunca pasa de su dotación");
            }
            {
                // granadero: solo con ≥ 6 zombis en el radio; si no, fusil. Alcance mínimo de 15 m. Vuelo de 20 ticks. Tope de 10 bajas.
                Func<int, int, (int, int, int, int)> run = (n, dm) =>
                {
                    var w = new UnitsWorld(5); var p = w.AddPlatoon(PlatoonKind.Granaderos, 0, 0, 0, Formation.Circulo); p.AmmoShots = 100000000;
                    var tg = new List<Target>(); for (int i = 0; i < n; i++) tg.Add(new Target { Id = i + 1, X = dm * 1000 + (i % 4) * 500, Y = (i / 4) * 500 });
                    var res = new StepResult(); int grenades = 0, rifles = 0, blasts = 0, maxKills = 0; bool firstBlastLate = true;
                    for (int t = 0; t < 400; t++)
                    {
                        w.Step(tg, res);
                        foreach (var s in res.Shots) { if (s.Weapon == WeaponId.Lanzagranadas) { grenades++; if (grenades == 1 && s.FlightTicks != 20) firstBlastLate = false; } else rifles++; }
                        blasts += res.Blasts.Count; int k = 0; foreach (var h in res.Hits) if (h.Weapon == WeaponId.Lanzagranadas) k++; if (res.Blasts.Count > 0) maxKills = Math.Max(maxKills, (k + res.Blasts.Count - 1) / res.Blasts.Count);
                    }
                    return (grenades, rifles, blasts, firstBlastLate ? maxKills : -1);
                };
                var many = run(12, 40); var few = run(3, 40); var close = run(12, 7);
                check(many.Item1 > 10 && many.Item3 > 10 && many.Item4 >= 1 && many.Item4 <= 10, $"con 12 zombis juntos a 40 m el granadero lanza granadas ({many.Item1}), explotan ({many.Item3}) y abaten como mucho {many.Item4} de golpe (tope 10)");
                check(few.Item1 == 0 && few.Item2 > 0, $"con solo 3 zombis usa el fusil ({few.Item2} disparos) y no gasta granadas");
                check(close.Item1 == 0, "a menos de 15 m no lanza granadas (alcance mínimo)");
            }
            {
                // ametralladora emplazada: hay que dar la orden, 60 ticks, y solo dispara en su cono de 60°
                Func<int, int, int, (int, int)> run = (mode, tx, ty) =>
                {
                    var w = new UnitsWorld(6); var p = w.AddPlatoon(PlatoonKind.Ametralladores, 0, 0, 0); p.AmmoShots = 100000000;
                    var tg = new List<Target> { new Target { Id = 1, X = tx, Y = ty } }; var res = new StepResult(); int first = -1, shots = 0;
                    if (mode >= 1) w.DeployAt(p.Id, 0);
                    for (int t = 0; t < 400; t++)
                    {
                        if (mode == 2 && t == 200) w.Move(p.Id, 5000, 0);
                        w.Step(tg, res); int n = 0; foreach (var s in res.Shots) if (s.Weapon == WeaponId.MgEmplazada) n++;
                        if (n > 0 && first < 0) first = t; if (t >= 200 || mode != 2) shots += n;
                    }
                    return (first, shots);
                };
                var nodeploy = run(0, 40000, 0); var dep = run(1, 40000, 0); var side = run(1, 0, 40000); var cone = run(1, 40000, 15000); var moved = run(2, 40000, 0);
                check(nodeploy.Item2 == 0, "sin la orden de emplazar, la ametralladora emplazada no dispara");
                check(dep.Item1 >= 58 && dep.Item1 <= 75 && dep.Item2 > 100, $"emplazada: empieza a disparar a los {dep.Item1} ticks (despliegue de 60) y dispara {dep.Item2} veces");
                check(side.Item2 == 0 && cone.Item2 > 0, "dispara a un blanco a 20° del eje (dentro del cono de 60°) y no a uno a 90° (fuera)");
                check(moved.Item2 == 0, "si el pelotón se mueve, deja de estar emplazado y no dispara");
            }
            {
                // perforación: con horda densa abate uno más al 50 %; con un blanco aislado, no
                Func<bool, double> pierce = dense =>
                {
                    var w = new UnitsWorld(33); var p = w.AddPlatoon(PlatoonKind.Tiradores, 0, 0, 0); p.AmmoShots = 100000000;
                    var tg = new List<Target> { new Target { Id = 1, X = 40000, Y = 0 } };
                    if (dense) for (int i = 0; i < 6; i++) tg.Add(new Target { Id = 10 + i, X = 40600 + i * 120, Y = (i % 2) * 300 });
                    var res = new StepResult(); int direct = 0, entries = 0;
                    for (int t = 0; t < 30000; t++) { w.Step(tg, res); foreach (var s in res.Shots) if (s.Weapon == WeaponId.FusilPrecision && s.Hit) direct++; foreach (var h in res.Hits) if (h.Weapon == WeaponId.FusilPrecision) entries++; }
                    return entries / (double)Math.Max(1, direct);
                };
                double sparse = pierce(false), densed = pierce(true);
                check(sparse == 1.0 && densed > 1.35 && densed < 1.65, $"perforación del fusil de precisión: {sparse:F2} bajas por impacto con un blanco aislado y {densed:F2} con horda densa (50 % de uno extra)");
            }
            {
                var w = new UnitsWorld(11); var p = w.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0); var q = w.AddPlatoon(PlatoonKind.Ametralladores, 0, 8000, 0); w.DeployAt(q.Id, 0);
                var z = new List<Zed>(); var rng = new IRng(5);
                for (int i = 0; i < 300; i++) z.Add(new Zed { Id = 100 + i, X = 70000 + rng.Below(40000), Y = rng.Below(30000) - 10000 });
                var res = new StepResult(); int kills = 0, shots = 0;
                for (int t = 0; t < 2400; t++)
                {
                    foreach (var a in z) if (a.Hp > 0) { int dx = 0 - a.X, dy = 4000 - a.Y, d = Math.Max(1, IMath.Dist(dx, dy)); if (d > 1200) { a.X += dx * 1500 / 30 / d; a.Y += dy * 1500 / 30 / d; } }
                    w.Step(ToTargets(z), res); shots += res.Shots.Count; kills += Apply(res, z, w);
                }
                int alive = 0; foreach (var a in z) if (a.Hp > 0) alive++;
                check(kills == 300 - alive && kills > 100 && shots > 800, $"fusileros y ametralladores emplazadas contra 300 zombis a 1,5 m/s: {kills} abatidos, {shots} disparos, quedan {alive}");
            }
            {
                var w = new UnitsWorld(1); var p = w.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0, Formation.Cuna);
                int leader = p.Men[0].Id; bool died = w.DamageMan(p.Id, leader, 5000);
                var res = new StepResult(); w.Step(null, res);
                check(died && p.AliveCount() == 7, "muere un soldado y el pelotón sigue con 7");
                var pts = new HashSet<(int, int)>(); foreach (var m in p.Men) if (m.Alive) pts.Add((m.SlotX, m.SlotY));
                check(pts.Count == 7, "la formación se recompone con los vivos: 7 puestos distintos");
                var w4 = new UnitsWorld(1); var h4 = w4.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0); h4.AmmoShots = 100000000; w4.SetHoldFire(h4.Id, true); int s4 = 0;
                for (int t = 0; t < 120; t++) { w4.Step(new List<Target> { new Target { Id = 1, X = 15000, Y = 0 } }, res); s4 += res.Shots.Count; }
                check(s4 == 0, "con la orden de alto el fuego no se dispara ni un tiro");
            }

            // ------------------------------------------------------------------ bucle de la noche
            {
                var g = new NightGame(NightParams.Default(), 77);
                check(g.TotalTicks == 30857 && Math.Abs(g.TotalTicks / 30.0 / 60 - 17.1) < 0.05, $"de 22:00 a 06:00 a ×28: {g.TotalTicks} ticks = {g.TotalTicks / 30.0 / 60:F1} min reales");
                var sp = new List<SpawnPoint>();
                bool clockBad = false; int all = 0; var perWave = new int[6]; var perEntry = new int[4]; var perWE = new int[6, 4]; bool inRect = true; int firstSpawnTick = -1; bool early = false;
                int frontCount1 = 0, tail1 = 0; int ymin = int.MaxValue, ymax = int.MinValue, fymin = int.MaxValue, fymax = int.MinValue;
                { var gq = new NightGame(NightParams.Default(), 77); gq.ZombiesAlive = 1; while (gq.Tick < gq.TotalTicks + 600) gq.Step(true, true, sp);
                  check(gq.State == NightState.Jugando && gq.Hour == 6, $"D-N164: a las 06:00 con horda viva NO se gana: la noche sigue ({gq.State} a las {gq.Hour:00}:{gq.Minute:00})");
                  gq.ZombiesAlive = 0; gq.Step(true, true, sp); check(gq.State == NightState.Victoria && gq.WonByClear && gq.Tick > gq.TotalTicks, "D-N164: ya de día, al caer el último zombi, victoria"); }
                g.ZombiesAlive = 0;      // D-N164: la horda cae en cuanto entra (solo se gana acabando con ella)
                while (g.State == NightState.Jugando)
                {
                    g.Step(true, true, sp);
                    if (g.Tick == 7715 && (g.Hour != 0 || g.Minute != 0)) clockBad = true;
                    foreach (var s in sp)
                    {
                        if (firstSpawnTick < 0) firstSpawnTick = g.Tick; all++; perWave[s.Wave]++; perEntry[s.Entry]++; perWE[s.Wave, s.Entry]++;
                        var e = g.P.Entries[s.Entry]; if (s.X < e.X0 || s.X >= e.X1 || s.Y < e.Y0 || s.Y >= e.Y1) inRect = false;
                        if (g.Tick < g.WaveStartTick(s.Wave)) early = true;
                        if (s.Wave == 0 && s.Entry == 0) { if (s.Front) { frontCount1++; fymin = Math.Min(fymin, s.Y); fymax = Math.Max(fymax, s.Y); } else { tail1++; ymin = Math.Min(ymin, s.Y); ymax = Math.Max(ymax, s.Y); } }
                    }
                }
                check(g.State == NightState.Victoria && g.WonByClear && g.Tick < g.TotalTicks, $"D-N164: con el edificio y el tren en pie, victoria al acabar con la horda, antes de las 06:00 ({g.Hour:00}:{g.Minute:00})");
                check(perWave[0] == 7310 && all == 7310, $"D-N136: la horda única suma exactamente 7.310 zombis (8.600 al 850 ‰, horda final) ({all})");
                check(!early && firstSpawnTick == g.WaveStartTick(0) + 1 - 0 || firstSpawnTick == g.WaveStartTick(0) + 1 || firstSpawnTick > g.WaveStartTick(0), $"nadie aparece antes de su oleada (primera aparición en el tick {firstSpawnTick}, oleada 1 en {g.WaveStartTick(0)} = 22:20)");
                var expected = new[,] { { 3655, 2193, 1462, 0 } };
                bool exact = true; string got = ""; for (int a = 0; a < 1; a++) { got += $" [O{a + 1}:"; for (int b = 0; b < 4; b++) { if (perWE[a, b] != expected[a, b]) exact = false; got += (b > 0 ? "/" : "") + perWE[a, b]; } got += "]"; }
                check(exact, "reparto por oleada y entrada por cuotas exacto (pesos de noche_sagunto.json con la escala de 850 ‰; horda final: 50/30/20/0);" + got);
                check(!clockBad, "el reloj marca las 00:00 a las 2 h de juego (tick 7.715)");
                check(inRect, "los zombis aparecen dentro de los rectángulos de entrada de Álvaro");
                check(perEntry[3] == 0, $"horda final: por el puente O4 (este) no entra nadie ({perEntry[3]} en total)");
                int wtot = g.WaveTotal(0); int fc = g.Cumulative(0, g.WaveDurationTicks(0) * 25 / 100);
                check(Math.Abs(fc - wtot * 55 / 100) <= 1 && fymax - fymin < 17000 / 3 + 2 && ymax - ymin > 17000 * 8 / 10, $"frente denso y cola dispersa: el 55 % entra en el primer 25 % del tiempo ({fc} de {wtot}) por el tercio central ({(fymax - fymin) / 1000.0:F1} m) y la cola se abre a {(ymax - ymin) / 1000.0:F1} m");
            }
            {
                // daño de los zombis a los soldados: proximidad pura, determinista (misma regla que la zona: 900 mm, 2 por zombi y tick)
                Func<UnitsWorld> mk = () => { var u = new UnitsWorld(3); u.AddPlatoon(PlatoonKind.Fusileros, 50000, 50000, 0); return u; };
                var w = mk(); var pl = w.Platoons[0]; var man = pl.Men[0]; int total0 = pl.AliveCount();
                var zs = new List<Target>();
                zs.Add(new Target { Id = 1, X = man.X + 800, Y = man.Y });          // dentro
                zs.Add(new Target { Id = 2, X = man.X, Y = man.Y + 900 });          // justo en el radio: toca
                zs.Add(new Target { Id = 3, X = man.X + 901, Y = man.Y });          // fuera
                zs.Add(new Target { Id = 4, X = man.X - 20000, Y = man.Y });        // lejos
                var cr = new ContactResult(); int fell = w.ZombieContact(zs, 900, 2, cr);
                check(man.Hp == 4000 - 2 * 2 && fell == 0 && cr.Touches >= 2, $"dos zombis a ≤ 900 mm quitan 2 × 2 de vida en un tick y el de 901 mm no (vida {man.Hp})");
                // un zombi pegado mata a un soldado de 100 de vida en 50 ticks; tres, en 17
                var w1 = mk(); var m1 = w1.Platoons[0].Men[0]; int t1 = 0; var one = new List<Target> { new Target { Id = 1, X = m1.X, Y = m1.Y } };
                while (m1.Alive && t1 < 5000) { w1.ZombieContact(one, 900, 2, null); t1++; }
                var w3 = mk(); var m3 = w3.Platoons[0].Men[0]; int t3 = 0; var three = new List<Target> { new Target { Id = 1, X = m3.X, Y = m3.Y }, new Target { Id = 2, X = m3.X + 100, Y = m3.Y }, new Target { Id = 3, X = m3.X, Y = m3.Y - 100 } };
                var cr3 = new ContactResult(); int fell3 = 0; while (m3.Alive && t3 < 5000) { fell3 += w3.ZombieContact(three, 900, 2, cr3); t3++; }
                check(t1 == 2000 && t3 == 667, $"un zombi pegado abate a un soldado de 4000 de vida en {t1} ticks (67 s) y tres en {t3} (22 s)");
                check(fell3 == 1 && cr3.Fallen.Count == 1 && cr3.Fallen[0].Man == m3.Id && w3.Platoons[0].Losses == 1 && w3.Platoons[0].AliveCount() == total0 - 1, "el caído se informa (pelotón, soldado, posición) y cuenta como baja del pelotón");
                // el orden de la lista no importa
                var rz = new List<Target>(); var rng = new IRng(5); for (int i = 0; i < 400; i++) rz.Add(new Target { Id = i, X = 50000 + (int)(rng.Below(12000)) - 6000, Y = 50000 + (int)(rng.Below(12000)) - 6000 });
                var wa = new UnitsWorld(3); wa.AddPlatoon(PlatoonKind.Fusileros, 50000, 50000, 0); wa.AddPlatoon(PlatoonKind.Ametralladores, 52000, 50000, 0);
                var wb = new UnitsWorld(3); wb.AddPlatoon(PlatoonKind.Fusileros, 50000, 50000, 0); wb.AddPlatoon(PlatoonKind.Ametralladores, 52000, 50000, 0);
                var rev = new List<Target>(rz); rev.Reverse();
                wa.ZombieContact(rz, 1500, 2, null); wb.ZombieContact(rev, 1500, 2, null);
                check(wa.Hash() == wb.Hash(), "dar la lista de zombis del revés da el mismo estado (cuenta contactos, sin orden ni azar)");
                // y coincide con la fuerza bruta
                var wc = new UnitsWorld(3); wc.AddPlatoon(PlatoonKind.Fusileros, 50000, 50000, 0); wc.AddPlatoon(PlatoonKind.Ametralladores, 52000, 50000, 0);
                foreach (var pp in wc.Platoons) foreach (var mm in pp.Men) { int c = 0; foreach (var z in rz) { long ex = z.X - mm.X, ey = z.Y - mm.Y; if (ex * ex + ey * ey <= 1500L * 1500) c++; } mm.Hp -= c * 2; }
                bool same = true; for (int a = 0; a < wa.Platoons.Count; a++) for (int b = 0; b < wa.Platoons[a].Men.Count; b++) if (wa.Platoons[a].Men[b].Hp != wc.Platoons[a].Men[b].Hp) same = false;
                check(same, "el resultado de la rejilla coincide con contar a fuerza bruta para cada soldado");
                // guardado
                var sw = new SaveWriter(); wa.SaveState(sw); var wl = new UnitsWorld(0); wl.LoadState(new SaveReader(sw.ToArray()));
                check(wl.Hash() == wa.Hash(), "la vida y las bajas del pelotón viajan en el guardado");
                // coste con 10.000 zombis
                var big = new List<Target>(); var rg = new IRng(9); for (int i = 0; i < 10000; i++) big.Add(new Target { Id = i, X = 50000 + rg.Below(40000) - 20000, Y = 50000 + rg.Below(40000) - 20000 });
                var wp = new UnitsWorld(3); for (int k = 0; k < 5; k++) wp.AddPlatoon(PlatoonKind.Fusileros, 40000 + k * 4000, 50000, 0);
                var swp = Stopwatch.StartNew(); for (int i = 0; i < 100; i++) { foreach (var q in wp.Platoons) foreach (var mm in q.Men) mm.Hp = 4000; wp.ZombieContact(big, 900, 2, null); } swp.Stop();
                double ms = swp.Elapsed.TotalMilliseconds / 100;
                Console.WriteLine($"         contacto con 10.000 zombis y {wp.Platoons.Count * wp.Platoons[0].Men.Count} soldados: {ms:0.000} ms por tick");
                check(ms < 1.0, $"el contacto con 10.000 zombis cuesta {ms:0.000} ms por tick (< 1 ms)");
            }
            {
                // vida del edificio de viajeros y del tren
                var P = NightParams.Default(); var sp = new List<SpawnPoint>();
                var g = new NightGame(P, 1);
                check(g.StationHp == 60000 && g.TrainHp == 120000 && g.StationAlive && g.TrainAlive, "la noche empieza con el edificio (60.000) y el tren (120.000) enteros");
                int t = 0; while (g.StationHp > 0 && t < 5000) { g.Attack(100, 0); g.Step(sp); t++; }
                check(t == 900 && g.StationHp == 0 && g.TrainHp == 120000, $"100 zombis pegados al edificio lo derriban en {t} ticks (30 s: 100 × 20 por segundo)");
                g.Step(sp);
                check(g.State == NightState.Derrota && g.EndedByStation && !g.EndedByTrain, "con el edificio a 0 el siguiente tick es DERROTA por el edificio");
                var g1 = new NightGame(P, 1); for (int i = 0; i < 30; i++) g1.Attack(1, 0);
                check(g1.StationHp == 60000 - 20, $"un zombi pegado 30 ticks quita exactamente 20 (resto fraccionario exacto): {60000 - g1.StationHp}");
                var g2 = new NightGame(P, 1); for (int i = 0; i < 300; i++) g2.Attack(0, 7);
                check(g2.TrainHp == 120000 - 7 * 20 * 10 && g2.StationHp == 60000, "el tren se desgasta aparte: 7 zombis durante 10 s quitan 1.400");
                var g3 = new NightGame(P, 1); while (g3.Tick < g3.TotalTicks - 1) g3.Step(sp);
                g3.Attack(0, 100000); while (g3.TrainHp > 0) g3.Attack(0, 100000);
                g3.Step(sp);
                check(g3.State == NightState.Derrota && g3.EndedByTrain && g3.Tick == g3.TotalTicks, "si el tren cae en el tick de las 06:00, derrota");
                var g4 = new NightGame(P, 1); g4.Attack(50, 10); int rp = g4.RepairStation(1000000); int rt = g4.RepairTrain(5);
                check(g4.StationHp == 60000 && rp > 0 && rt == 5 && g4.TrainHp <= 120000, "reparar suma sin pasar del máximo");
                var g5 = new NightGame(P, 1); g5.Attack(10000, 10000); g5.Attack(10000, 10000); int rz = g5.RepairStation(100);
                check(g5.StationHp > 0 || rz == 0, "una vida a 0 no se repara");
                var h0 = new NightGame(P, 1); var h1 = new NightGame(P, 1); h1.Attack(1, 0);
                check(h0.Hash() != h1.Hash(), "la vida entra en el hash (también el desgaste fraccionario)");
                var sws = new SaveWriter(); g2.SaveState(sws); var gl = new NightGame(P, 0); gl.LoadState(new SaveReader(sws.ToArray()));
                check(gl.Hash() == g2.Hash() && gl.TrainHp == g2.TrainHp, "la vida de edificio y tren viaja en el guardado");
                var csv = NightParams.FromCsv(new CsvTable("clave;valor;escala;nota\nnoche_estacion_vida;1234;1;\nnoche_tren_vida;99;1;\nnoche_contacto_danio_tick;3;1;\n"));
                check(csv.StationHp == 1234 && csv.TrainHp == 99 && csv.ContactDmgPerTick == 3 && csv.ContactRadiusMm == 900, "los parámetros se leen del CSV de la noche");
            }
            {
                // D-N149 (Eugenio): sin estrellas; el JSON ya no trae resultados.nota ni resultados.estrellas y se lee igual
                var json = TBD.Net.BalanceSource.Read("noche_sagunto.json"); var ns = NightSim.FromJson(json, null, 1);
                check(!json.Contains("\"estrellas\"") && !json.Contains("victoria_1") && ns.Night.P.Waves.Length == 1, "D-N149: noche_sagunto.json sin estrellas ni nota, y la noche se carga igual");
                // el parte lleva la integridad y el muro (filas 7 y 8) y la regla de «pegado» es única
                var g = new NightGame(NightParams.Default(), 1); var sp = new List<SpawnPoint>();
                var near = new List<Target> { new Target { Id = 1, X = 106000, Y = 76500 }, new Target { Id = 2, X = 90000, Y = 64000 }, new Target { Id = 3, X = 106000, Y = 74000 }, new Target { Id = 4, X = 50000, Y = 64500 }, new Target { Id = 5, X = 106000, Y = 67000 } };
                int aS, aT; g.CountAttackers(near, out aS, out aT);
                check(aS == 2 && aT == 2, $"pegado = a ≤ 1,5 m del edificio (2: dentro y a 1 m) o del tren (2: sobre la vía y a 1,1 m del borde) y un zombi lejano no cuenta ({aS}, {aT})");
                for (int i = 0; i < 900; i++) g.Attack(0, 100);      // 100 zombis 30 s = 60.000 al tren: la mitad de 120.000
                g.WallPermil = 640; g.ZombiesAlive = 0; while (g.State == NightState.Jugando) g.Step(sp);
                check(g.State == NightState.Victoria && g.Report.IntegrityPermil == 500 && g.Report.WallPermil == 640, $"al acabar con la horda el parte lleva integridad {g.Report.IntegrityPermil} ‰ (mín edificio-tren) y muro {g.Report.WallPermil} ‰");
                var sw = new SaveWriter(); g.SaveState(sw); var gl = new NightGame(NightParams.Default(), 0); gl.LoadState(new SaveReader(sw.ToArray()));
                check(gl.Hash() == g.Hash() && gl.Report.IntegrityPermil == 500 && gl.WallPermil == 640, "las dos medidas viajan en el guardado y en el hash");
                // en NightSim el tren también recibe daño
                var nsim = NightSim.FromJson(json, "dificil", 3); nsim.AddDefaultForce(); nsim.Units.Platoons.Clear();
                nsim.Run(40000);
                check(nsim.Night.TrainHp < nsim.Night.P.TrainHp || nsim.Night.State == NightState.Derrota, $"sin defensa, los zombis que llegan al andén dañan el tren (vida {nsim.Night.TrainHp} de {nsim.Night.P.TrainHp})");
            }
            {
                // vehículos de Hugo (CSV), combustible, pila, atropello, torreta y pasajeros
                var before = (VehicleStats[])VehicleCatalog.Table.Clone();
                VehicleCatalog.Load(new CsvTable(TBD.Net.BalanceSource.Read("balance_unidades.csv")));
                bool same = true; for (int i = 1; i < before.Length; i++) { var a = before[i]; var b = VehicleCatalog.Table[i]; same &= a.Hp == b.Hp && a.ArmorPm == b.ArmorPm && a.MaxFwdMmS == b.MaxFwdMmS && a.HordeSpeedMmS == b.HordeSpeedMmS && a.AccelMmS2 == b.AccelMmS2 && Math.Abs(a.MaxYawPerTick - b.MaxYawPerTick) <= 1 && a.BlockPile == b.BlockPile && a.Seats == b.Seats && a.AmmoBoardMilliU == b.AmmoBoardMilliU && a.FuelMarchE4s == b.FuelMarchE4s && a.FuelIdleE4s == b.FuelIdleE4s && a.TankU == b.TankU && a.RunOverMinMmS == b.RunOverMinMmS && a.RunOverPm == b.RunOverPm; }
                VehicleCatalog.Reset();
                check(same, "los valores por defecto de VAMTAC, Dragón y Pizarro coinciden con balance_unidades.csv (vida, armadura, velocidades, pila, plazas, munición, combustible, atropello)");
                var dr = VehicleCatalog.Get(VehicleKind.Dragon);
                check(dr.Hp == 4000 && dr.ArmorPm == 990 && dr.MaxFwdMmS == 12000 && dr.Seats == 8 && dr.TankU == 8 && dr.Mounts.Length == 2 && dr.Mounts[0] == WeaponId.Canon30 && !dr.Tracked, "el Dragón: 8×8 de ruedas, 4000 de vida, armadura 99 %, 12 m/s, 8 plazas, depósito de 8 u, cañón de 30 mm y ametralladora ligera");
                // aceleración y combustible: 100 s a fondo = 1,2 u/min × 1,67 min = 2 u (20.000 E4)
                var v = new Vehicle(1, VehicleKind.Dragon, 0, 0, 0); v.Controls(1000, 0, 0);
                for (int t = 0; t < 3000; t++) v.Step();
                check(v.SpeedMmS == 12000 && v.FuelUsedE4 >= 19_900 && v.FuelUsedE4 <= 20_100 && v.FuelE4 == 80_000 - v.FuelUsedE4, $"el Dragón llega a 12 m/s y gasta 1,2 u/min en marcha: {v.FuelUsedE4 / 10_000.0:0.00} u en 100 s");
                var idle = new Vehicle(2, VehicleKind.Dragon, 0, 0, 0); for (int t = 0; t < 1800; t++) idle.Step();
                check(idle.FuelUsedE4 >= 3_550 && idle.FuelUsedE4 <= 3_650, $"parado con el motor encendido gasta 0,36 u/min: {idle.FuelUsedE4 / 10_000.0:0.000} u en 60 s");
                idle.SetEngine(false); long f0 = idle.FuelE4; for (int t = 0; t < 600; t++) idle.Step();
                check(idle.FuelE4 == f0, "con el motor apagado no gasta nada");
                // sin combustible: se queda inmóvil, y repostar lo devuelve a la vida
                var dry = new Vehicle(3, VehicleKind.Dragon, 0, 0, 0); dry.FuelE4 = 150; dry.Controls(1000, 0, 0);
                for (int t = 0; t < 600; t++) dry.Step();
                check(dry.OutOfFuel && !dry.EngineOn && dry.SpeedMmS == 0 && dry.LowFuel, $"a cero de combustible el motor se apaga y el Dragón se queda inmóvil (velocidad {dry.SpeedMmS})");
                int taken = dry.Refuel(1_000_000); dry.SetEngine(true); dry.Controls(1000, 0, 0); for (int t = 0; t < 120; t++) dry.Step();
                check(taken == 80_000 - 0 - 0 + (80_000 - taken) * 0 && dry.SpeedMmS > 3000 && !dry.LowFuel, $"repostar mete lo que cabe ({taken / 10_000.0:0.0} u) y vuelve a rodar");
                // pila delante y horda suelta
                var pv = new Vehicle(4, VehicleKind.Dragon, 0, 0, 0); pv.Controls(1000, 0, 0); pv.PileAhead = 2; for (int t = 0; t < 300; t++) pv.Step(); int moved2 = pv.X;
                var pb = new Vehicle(5, VehicleKind.Dragon, 0, 0, 0); pb.Controls(1000, 0, 0); pb.PileAhead = 3; for (int t = 0; t < 300; t++) pb.Step();
                var vm = new Vehicle(6, VehicleKind.Vamtac, 0, 0, 0); vm.Controls(1000, 0, 0); vm.PileAhead = 2; for (int t = 0; t < 300; t++) vm.Step();
                check(moved2 > 30_000 && pb.X == 0 && pb.SpeedMmS == 0 && vm.X == 0, $"con pila 2 el Dragón avanza ({moved2 / 1000} m), con 3 se para en seco y el VAMTAC ya se para con 2");
                var hv = new Vehicle(7, VehicleKind.Dragon, 0, 0, 0); hv.Controls(1000, 0, 0); hv.InHorde = true; for (int t = 0; t < 400; t++) hv.Step();
                check(hv.SpeedMmS == 4000, $"a través de horda suelta el Dragón no pasa de 4 m/s ({hv.SpeedMmS})");
                // daño de contacto con armadura: Hugo dice que un VAMTAC con 24 zombis pegados cae en unos 28 s
                var vt = new Vehicle(8, VehicleKind.Vamtac, 0, 0, 0); int ticks = 0; while (vt.Hp > 0 && ticks < 100000) { vt.TakeContact(24, 2); ticks++; }
                var dt = new Vehicle(9, VehicleKind.Dragon, 0, 0, 0); int dticks = 0; while (dt.Hp > 0 && dticks < 100000) { dt.TakeContact(24, 2); dticks++; }
                check(Math.Abs(ticks / 30.0 - 27.8) < 0.6 && dticks > 8000, $"con 24 zombis pegados el VAMTAC (armadura 95 %) cae en {ticks / 30.0:0.0} s y el Dragón (99 %) aguanta {dticks / 30.0:0.0} s");
                // atropello: a ≥ 3 m/s abate al 50 %; más despacio solo empuja
                var rv = new Vehicle(10, VehicleKind.Dragon, 0, 0, 0); rv.Controls(1000, 0, 0); for (int t = 0; t < 300; t++) rv.Step();
                var crowd = new List<Target>(); for (int i = 0; i < 1000; i++) crowd.Add(new Target { Id = i, X = rv.X + 1000 + (i % 40) * 50, Y = rv.Y - 1000 + (i / 40) * 80 });
                var kl = new List<int>(); int k1 = rv.RunOver(crowd, 500, kl); var kl2 = new List<int>(); int k2 = rv.RunOver(crowd, 500, kl2);
                var slow = new Vehicle(11, VehicleKind.Dragon, 0, 0, 0); slow.Controls(200, 0, 0); for (int t = 0; t < 60; t++) slow.Step();
                var kl3 = new List<int>();
                int inRam = 0; foreach (var z in crowd) if (Vehicle.InRam(rv, z.X, z.Y)) inRam++;
                check(k1 == k2 && kl.Count == kl2.Count && k1 > inRam * 40 / 100 && k1 < inRam * 60 / 100 && slow.RunOver(crowd, 500, kl3) == 0, $"a {rv.SpeedMmS / 1000} m/s el Dragón abate {k1} de los {inRam} zombis que pisa (50 %), siempre los mismos; a {slow.SpeedMmS / 1000.0:0.0} m/s no abate");
                // torreta
                var wv = new UnitsWorld(1); var tv = new Vehicle(12, VehicleKind.Dragon, 50_000, 50_000, 0); var res = new StepResult();
                var zs = new List<Target>(); for (int i = 0; i < 40; i++) zs.Add(new Target { Id = 100 + i, X = 50_000 + 40_000 + (i % 8) * 600, Y = 50_000 + 35_000 + (i / 8) * 600 });
                int ammo0 = tv.AmmoShots; int shotsFired = 0, killsT = 0; int yaw0 = tv.TurretYaw;
                for (int t = 0; t < 300; t++) { tv.StepWeapons(wv, zs, res); shotsFired += res.Shots.Count; foreach (var h in res.Hits) { zs.RemoveAll(q => q.Id == h.TargetId); killsT++; } res.Shots.Clear(); res.Hits.Clear(); res.Blasts.Clear(); }
                check(ammo0 == 4000 && tv.AmmoShots < ammo0 && killsT > 3 && Math.Abs(IMath.Diff(tv.TurretYaw, yaw0)) > 1000, $"la torreta automática gira hacia los zombis ({Math.Abs(IMath.Diff(tv.TurretYaw, yaw0)) * 360 / 65536}°), dispara {shotsFired} veces y abate {killsT}; munición {ammo0} → {tv.AmmoShots} disparos");
                var empty = new Vehicle(13, VehicleKind.Dragon, 50_000, 50_000, 0); empty.FuelE4 = 0; empty.EngineOn = false; int sf = 0; var e2 = new StepResult();
                var zs2 = new List<Target>(); for (int i = 0; i < 20; i++) zs2.Add(new Target { Id = 300 + i, X = 80_000, Y = 52_000 + i * 300 });
                for (int t = 0; t < 120; t++) { empty.StepWeapons(wv, zs2, e2); sf += e2.Shots.Count; e2.Clear(); }
                var wrecked = new Vehicle(14, VehicleKind.Dragon, 50_000, 50_000, 0); wrecked.Hp = 0; int sw0 = 0; for (int t = 0; t < 120; t++) { wrecked.StepWeapons(wv, zs2, e2); sw0 += e2.Shots.Count; e2.Clear(); }
                check(sf > 0 && sw0 == 0, $"sin combustible las armas siguen disparando ({sf} disparos) y destruido no");
                var hashBefore = wv.Hash(); var tt = new Vehicle(15, VehicleKind.Dragon, 50_000, 50_000, 0); for (int t = 0; t < 100; t++) { tt.StepWeapons(wv, zs2, e2); e2.Clear(); }
                check(wv.Hash() == hashBefore, "los disparos de los vehículos no tocan el hash de la simulación (son locales)");
                // pasajeros
                var pw = new UnitsWorld(2); var pl = pw.AddPlatoon(PlatoonKind.Fusileros, 50_000, 50_000, 0); var pl2 = pw.AddPlatoon(PlatoonKind.Fusileros, 52_000, 50_000, 0);
                var bus = new Vehicle(16, VehicleKind.Dragon, 51_000, 50_000, 0); int men = pl.AliveCount();
                var far = new UnitsWorld(2); var plf = far.AddPlatoon(PlatoonKind.Fusileros, 90_000, 50_000, 0);
                check(!bus.CanBoard(far, plf.Id), "un pelotón a más de 15 m no sube");
                bus.SpeedMmS = 5000; bool moving = bus.CanBoard(pw, pl.Id); bus.SpeedMmS = 0;
                check(!moving && bus.CanBoard(pw, pl.Id) && men <= 8, $"con el vehículo en marcha no sube; parado sí ({men} soldados, 8 plazas)");
                bool boarded = bus.Board(pw, pl.Id); bool second = bus.Board(pw, pl2.Id);
                check(boarded && pl.Aboard == bus.CarrierId && pw.SeatsUsed(bus.CarrierId) == men && (men == 8 ? !second : second), $"sube el primer pelotón ({men} plazas ocupadas) y el segundo {(second ? "también cabe" : "ya no cabe")}");
                bus.Controls(1000, 0, 0); for (int t = 0; t < 300; t++) { bus.Step(); bus.CarryPassengers(pw); }
                check(Math.Abs(pl.AX - bus.X) < 10 && Math.Abs(pl.Men[0].X - bus.X) < 10 && bus.X > 30_000, $"los pasajeros viajan con el vehículo ({bus.X / 1000} m)");
                var cr = new ContactResult(); int hp0 = pl.Men[0].Hp; var swarm = new List<Target>(); for (int i = 0; i < 50; i++) swarm.Add(new Target { Id = i, X = bus.X, Y = bus.Y });
                pw.ZombieContact(swarm, 900, 2, cr);
                check(pl.Men[0].Hp == hp0, "a bordo los zombis no tocan a los soldados");
                var stepRes = new StepResult(); int tx = pl.Men[0].X; pw.Step(swarm, stepRes);
                check(stepRes.Shots.Count == 0 || pl.Aboard != 0, "a bordo el pelotón no dispara ni se mueve por su cuenta");
                var sw5 = new SaveWriter(); pw.SaveState(sw5); var pwl = new UnitsWorld(0); pwl.LoadState(new SaveReader(sw5.ToArray()));
                check(pwl.Hash() == pw.Hash() && pwl.Find(pl.Id).Aboard == bus.CarrierId, "quién va a bordo entra en el hash y en el guardado");
                bus.Brake = 0; bus.Controls(0, 0, 1000); for (int t = 0; t < 200; t++) bus.Step();
                bool un = bus.Unload(pw, pl.Id);
                check(un && pl.Aboard == 0 && IMath.Dist(pl.AX - bus.X, pl.AY - bus.Y) > 6000, "al bajar, el pelotón queda detrás del vehículo y vuelve a ser suyo");
            }
            {
                // NH90 (Hugo): datos, combustible, autorrotación, vulnerabilidad, ametralladoras de puerta y transporte
                var hb = (HeliStats[])HeliCatalog.Table.Clone();
                HeliCatalog.Load(new CsvTable(TBD.Net.BalanceSource.Read("balance_unidades.csv")));
                bool same = true; for (int i = 0; i < hb.Length; i++) { if (hb[i].Key != "nh90" && hb[i].Key != "tigre") continue; var a = hb[i]; var b = HeliCatalog.Table[i]; same &= a.Hp == b.Hp && a.ArmorPm == b.ArmorPm && a.MaxFwdMmS == b.MaxFwdMmS && a.AccelMmS2 == b.AccelMmS2 && (a.Key == "tigre" || a.DragMmS2 == b.DragMmS2) && Math.Abs(a.YawRatePerTick - b.YawRatePerTick) <= 1 && a.MaxClimbMmS == b.MaxClimbMmS && a.MaxDescentMmS == b.MaxDescentMmS && a.CruiseAltMm == b.CruiseAltMm && a.ContactAltMm == b.ContactAltMm && Math.Abs(a.MaxTilt - b.MaxTilt) <= 1 && a.Seats == b.Seats && a.AmmoBoardMilliU == b.AmmoBoardMilliU && a.FuelFlyE4s == b.FuelFlyE4s && a.FuelGroundE4s == b.FuelGroundE4s && a.TankU == b.TankU; }
                HeliCatalog.Table = hb;
                check(same, "los valores por defecto del NH90 y el Tigre coinciden con balance_unidades.csv");
                var ns = HeliCatalog.Get(HeliKind.Nh90);
                check(ns.Hp == 3000 && ns.MaxFwdMmS == 45000 && ns.Seats == 12 && ns.CruiseAltMm == 30000 && ns.ContactAltMm == 3000 && ns.TankU == 6 && ns.DoorGuns.Length == 2, "NH90: 3000 de vida, 45 m/s, 12 plazas, crucero a 30 m, contacto por debajo de 3 m, depósito de 6 u y dos ametralladoras de puerta");
                // despegue y crucero a 30 m
                var h = new Helicopter(1, HeliKind.Nh90, 0, 0, 0); h.StartEngine(); h.Controls(1000, 0, 0, 0);
                int tk = 0; while (h.AltitudeMm < 30_000 && tk < 3000) { h.Step(); tk++; }
                h.Controls(0, 1000, 0, 0); for (int t = 0; t < 900; t++) h.Step(); int speed = IMath.Dist(h.VX, h.VY);
                check(h.IsAirborne && h.AltitudeMm >= 29_000 && speed > 40_000 && speed <= 45_000, $"el NH90 despega, sube a {h.AltitudeMm / 1000} m en {tk / 30.0:0.0} s y vuela a {speed / 1000.0:0.0} m/s (máx. 45)");
                long used0 = h.FuelUsedE4; for (int t = 0; t < 1800; t++) h.Step(); long used = h.FuelUsedE4 - used0;
                check(used >= 16_100 && used <= 16_300, $"en vuelo gasta 1,62 u/min: {used / 10_000.0:0.000} u en 60 s");
                // sin combustible en el aire: autorrotación a 4 m/s, aterrizaje blando, sin control
                var f = new Helicopter(2, HeliKind.Nh90, 0, 0, 0); f.StartEngine(); f.Controls(1000, 0, 0, 0); while (f.AltitudeMm < 20_000) f.Step();
                f.FuelE4 = 40; f.Controls(1000, 1000, 1000, 1000); int hp0 = f.Hp; int vzSeen = 0; int tt = 0;
                while (f.IsAirborne && tt < 3000) { f.Step(); if (f.Autorotating) vzSeen = f.VZ; tt++; }
                check(!f.EngineOn && f.State == HeliState.EnTierra && vzSeen == -4000 && f.Hp == hp0 && f.LastImpactMmS <= 4500, $"a cero de combustible baja en autorrotación a {-vzSeen / 1000.0:0.0} m/s, sin hacer caso a los mandos, y aterriza sin daño (impacto {f.LastImpactMmS / 1000.0:0.0} m/s)");
                check(f.LowFuel && f.Refuel(1_000_000) == 60_000 && !f.LowFuel, "tras posarse puede repostar (6 u) y el aviso de combustible bajo se apaga");
                var fl = new Helicopter(3, HeliKind.Nh90, 0, 0, 0); fl.StartEngine(); fl.Controls(1000, 0, 0, 0); while (fl.AltitudeMm < 5_000) fl.Step();
                check(fl.Refuel(1000) == 0, "en el aire no se reposta");
                // vulnerabilidad: solo por debajo de 3 m
                var hv = new Helicopter(4, HeliKind.Nh90, 0, 0, 0); hv.StartEngine(); hv.Controls(1000, 0, 0, 0); while (hv.AltitudeMm < 20_000) hv.Step();
                int hpA = hv.Hp; for (int t = 0; t < 100; t++) hv.TakeContact(24, 2);
                var gv = new Helicopter(5, HeliKind.Nh90, 0, 0, 0); for (int t = 0; t < 100; t++) gv.TakeContact(24, 2);
                var lv = new Helicopter(6, HeliKind.Nh90, 0, 0, 0); lv.StartEngine(); lv.Controls(1000, 0, 0, 0); while (lv.AltitudeMm < 2_500) lv.Step(); bool low = lv.Vulnerable; int hpL = lv.Hp; for (int t = 0; t < 100; t++) lv.TakeContact(24, 2);
                check(!hv.Vulnerable && hv.Hp == hpA && gv.Vulnerable && gv.Hp == 3000 - 480 && low && lv.Hp == hpL - 480, $"a 20 m los zombis no lo tocan; posado o a 2,5 m sí (armadura 90 %: 480 de vida en 100 ticks con 24 agarrados)");
                // ametralladoras de puerta: cada una solo a su lado; el alcance se recorta con la altura
                var wd = new UnitsWorld(1); var res = new StepResult();
                Func<Helicopter> air = () => { var a = new Helicopter(7, HeliKind.Nh90, 50_000, 50_000, 0); a.State = HeliState.Volando; a.Z = 30_000; a.EngineOn = true; return a; };
                Func<int, int, int> shots = (sideLeft, sideRight) =>
                {
                    var a = air(); var zl = new List<Target>(); for (int i = 0; i < sideLeft; i++) zl.Add(new Target { Id = i, X = 50_000 + (i % 5) * 400, Y = 50_000 + 60_000 + (i / 5) * 400 });
                    for (int i = 0; i < sideRight; i++) zl.Add(new Target { Id = 1000 + i, X = 50_000 + (i % 5) * 400, Y = 50_000 - 60_000 - (i / 5) * 400 });
                    int n = 0; for (int t = 0; t < 30; t++) { a.StepWeapons(wd, zl, res); n += res.Shots.Count; res.Clear(); } return n;
                };
                int onlyLeft = shots(20, 0), onlyRight = shots(0, 20), both = shots(20, 20);
                check(onlyLeft == 10 && onlyRight == 10 && both == 20, $"cada ametralladora solo dispara a su lado: izquierda sola {onlyLeft} disparos en 1 s, derecha sola {onlyRight}, las dos {both}");
                var ahead = air(); var za = new List<Target>(); for (int i = 0; i < 10; i++) za.Add(new Target { Id = i, X = 50_000 + 70_000, Y = 50_000 + 300 * i });
                int sa = 0; for (int t = 0; t < 30; t++) { ahead.StepWeapons(wd, za, res); sa += res.Shots.Count; res.Clear(); }
                var farz = new List<Target> { new Target { Id = 1, X = 50_000, Y = 50_000 + 119_000 } }; var near2 = new List<Target> { new Target { Id = 2, X = 50_000, Y = 50_000 + 100_000 } };
                int sFar = 0, sNear = 0; var ah = air(); for (int t = 0; t < 30; t++) { ah.StepWeapons(wd, farz, res); sFar += res.Shots.Count; res.Clear(); } var ah2 = air(); for (int t = 0; t < 30; t++) { ah2.StepWeapons(wd, near2, res); sNear += res.Shots.Count; res.Clear(); }
                check(sFar == 0 && sNear > 0, $"a 30 m de altura, un zombi a 119 m en horizontal queda fuera de alcance (120 m en 3D) y a 100 m dentro ({sFar}, {sNear})");
                var crash = air(); crash.State = HeliState.Estrellado; crash.Hp = 0; int sc = 0; var zc = new List<Target> { new Target { Id = 1, X = 50_000, Y = 90_000 } }; for (int t = 0; t < 30; t++) { crash.StepWeapons(wd, zc, res); sc += res.Shots.Count; res.Clear(); }
                var kill = air(); int kills = 0; var big = new List<Target>(); for (int i = 0; i < 400; i++) big.Add(new Target { Id = i, X = 50_000 - 20_000 + (i % 20) * 300, Y = 50_000 + 50_000 + (i / 20) * 300 });
                int am0 = kill.AmmoShots; for (int t = 0; t < 600; t++) { kill.StepWeapons(wd, big, res); foreach (var hh in res.Hits) { big.RemoveAll(q => q.Id == hh.TargetId); kills++; } res.Clear(); }
                check(sc == 0 && am0 == 6000 && kills > 20 && kill.AmmoShots < am0, $"estrellado no dispara; en 20 s abate {kills} zombis y gasta {am0 - kill.AmmoShots} de {am0} disparos de a bordo");
                // transporte de un pelotón
                var tw = new UnitsWorld(3); var p1 = tw.AddPlatoon(PlatoonKind.Fusileros, 50_000, 50_000, 0); var p2 = tw.AddPlatoon(PlatoonKind.Fusileros, 53_000, 50_000, 0);
                var nh = new Helicopter(8, HeliKind.Nh90, 51_000, 50_000, 0); int men = p1.AliveCount();
                bool b1 = nh.Board(tw, p1.Id), b2 = nh.Board(tw, p2.Id);
                check(b1 && !b2 && men <= 12 && tw.SeatsUsed(nh.CarrierId) == men, $"sube un pelotón ({men} de 12 plazas) y el segundo ya no cabe ({p2.AliveCount()} más)");
                nh.StartEngine(); nh.Controls(1000, 0, 0, 0); while (nh.AltitudeMm < 10_000) { nh.Step(); nh.CarryPassengers(tw); }
                check(Math.Abs(p1.AX - nh.X) < 10 && Math.Abs(p1.Men[0].Y - nh.Y) < 10 && !nh.Unload(tw, p1.Id) && p1.Aboard == nh.CarrierId, "en vuelo los pasajeros van con el helicóptero y no se pueden bajar");
                var other = new Helicopter(9, HeliKind.Nh90, 90_000, 90_000, 0); other.StartEngine(); other.Controls(1000, 0, 0, 0); while (other.AltitudeMm < 5_000) other.Step();
                var tw2 = new UnitsWorld(3); var pz = tw2.AddPlatoon(PlatoonKind.Fusileros, 90_000, 90_000, 0);
                check(!other.CanBoard(tw2, pz.Id), "en el aire no se embarca");
                nh.Controls(-1000, 0, 0, 0); int gt = 0; while (nh.IsAirborne && gt < 3000) { nh.Step(); nh.CarryPassengers(tw); gt++; }
                bool unl = nh.Unload(tw, p1.Id);
                check(unl && p1.Aboard == 0 && nh.State == HeliState.EnTierra && IMath.Dist(p1.AX - nh.X, p1.AY - nh.Y) >= 7_000, "posado, el pelotón baja a un costado y vuelve a ser suyo");
                // D-N143: un pelotón por helicóptero, descarga con U y aterrizaje en un claro de 20 × 20 m
                {
                    var tw3 = new UnitsWorld(4); var q1 = tw3.AddPlatoon(PlatoonKind.Tiradores, 50_000, 50_000, 0); var q2 = tw3.AddPlatoon(PlatoonKind.Tiradores, 52_000, 50_000, 0);
                    var nh3 = new Helicopter(10, HeliKind.Nh90, 51_000, 50_000, 0);
                    bool a1 = nh3.Board(tw3, q1.Id), a2 = nh3.Board(tw3, q2.Id);
                    check(a1 && !a2 && q1.AliveCount() + q2.AliveCount() <= 12, "D-N143: el NH90 lleva un pelotón; el segundo no sube aunque quepan las plazas");
                    check(nh3.UnloadAll(tw3) == 1 && q1.Aboard == 0, "D-N143: posado, U baja a todos los que lleva");
                    var blocks = new List<RectMm> { new RectMm(98000, 75000, 114000, 83000) };
                    check(Helicopter.LandingClear(140000, 30000, blocks, null) && !Helicopter.LandingClear(110000, 70000, blocks, null), "D-N143: aterriza en un claro de 20 × 20 m, no encima del edificio");
                    check(!Helicopter.LandingClear(140000, 30000, blocks, new List<Target> { new Target { X = 145000, Y = 35000 } }), "D-N143: con un zombi en el claro no aterriza");
                }
            }
            {
                // arco de las granadas para la vista, y francotiradores que prefieren a los especiales
                var gw = new UnitsWorld(4); var gp = gw.AddPlatoon(PlatoonKind.Granaderos, 50_000, 50_000, 0); var rs = new StepResult();
                var cl = new List<Target>(); for (int i = 0; i < 12; i++) cl.Add(new Target { Id = i, X = 50_000 + 60_000 + (i % 4) * 500, Y = 50_000 + (i / 4) * 500 });
                FireEvent gev = default(FireEvent); bool gotG = false, gotRifle = false; FireEvent rev = default(FireEvent);
                for (int t = 0; t < 400 && !(gotG && gotRifle); t++) { gw.Step(cl, rs); foreach (var e in rs.Shots) { if (e.Weapon == WeaponId.Lanzagranadas && !gotG) { gev = e; gotG = true; } if (e.Weapon == WeaponId.Fusil && !gotRifle) { rev = e; gotRifle = true; } } }
                check(gotG && gev.FlightTicks == 20 && gev.ArcPeakMm >= 500 && gev.ArcPeakMm <= 600 && gev.FromX > 49_000 && gev.FromX < 55_000 && (!gotRifle || rev.ArcPeakMm == 0), $"la granada sale con su parábola: vuelo {gev.FlightTicks} ticks, vértice a {gev.ArcPeakMm} mm (g·T²/8), desde ({gev.FromX / 1000}, {gev.FromY / 1000}) m; el fusil no tiene arco");
                var sw2 = new UnitsWorld(5); var sp2 = sw2.AddPlatoon(PlatoonKind.Tiradores, 50_000, 50_000, 0); var sr = new StepResult();
                var mix = new List<Target> { new Target { Id = 1, X = 50_000 + 30_000, Y = 50_000 }, new Target { Id = 2, X = 50_000 + 120_000, Y = 50_000, Special = true }, new Target { Id = 3, X = 50_000 + 20_000, Y = 50_000 + 500 } };
                int firstTarget = -1; for (int t = 0; t < 400 && firstTarget < 0; t++) { sw2.Step(mix, sr); foreach (var e in sr.Shots) if (e.Weapon == WeaponId.FusilPrecision && firstTarget < 0) firstTarget = e.TargetId; }
                var sw3 = new UnitsWorld(5); sw3.AddPlatoon(PlatoonKind.Tiradores, 50_000, 50_000, 0); int firstPlain = -1; var plain = new List<Target> { new Target { Id = 1, X = 80_000, Y = 50_000 }, new Target { Id = 2, X = 170_000, Y = 50_000 }, new Target { Id = 3, X = 70_000, Y = 50_500 } };
                for (int t = 0; t < 400 && firstPlain < 0; t++) { sw3.Step(plain, sr); foreach (var e in sr.Shots) if (e.Weapon == WeaponId.FusilPrecision && firstPlain < 0) firstPlain = e.TargetId; }
                var sw4 = new UnitsWorld(5); sw4.AddPlatoon(PlatoonKind.Fusileros, 50_000, 50_000, 0); int firstRifle = -1; var rm = new List<Target> { new Target { Id = 1, X = 50_000 + 30_000, Y = 50_000 }, new Target { Id = 2, X = 50_000 + 50_000, Y = 50_000, Special = true } };
                for (int t = 0; t < 400 && firstRifle < 0; t++) { sw4.Step(rm, sr); foreach (var e in sr.Shots) if (e.Weapon == WeaponId.Fusil && firstRifle < 0) firstRifle = e.TargetId; }
                check(firstTarget == 2 && firstPlain == 3 && firstRifle == 1, $"el francotirador dispara primero al especial aunque esté a 120 m ({firstTarget}); sin especiales, al más cercano ({firstPlain}); el fusilero no distingue ({firstRifle})");
            }
            {
                // ------------------------------------------------------------------ orden de transporte (D-N96): carreteras habilitadas y vía, vehículo libre, ETA para el mapa
                string rj = TBD.Net.BalanceSource.Read("mapa_sagunto_carreteras.json");
                var map = RoadMap.Sagunto(rj);
                var dis = map.DisabledRoads();
                check(dis.Count == 2 && dis[0].StartsWith("carretera_vieja_este") && dis[1].StartsWith("paso_superior"), $"el mapa de carreteras sale del JSON: {dis.Count} deshabilitadas con su motivo ({string.Join(" | ", dis)})");
                var rHeli = map.Route(99_000, 53_000, 180_000, 44_000, 375, 600, 60_000);
                check(rHeli.Ok && rHeli.LengthMm > 80_000 && rHeli.LengthMm < 140_000, $"ruta parque → helipuerto: {rHeli.LengthMm / 1000} m (carretera {rHeli.RoadMm / 1000}, vía {rHeli.ViaMm / 1000}, accesos {rHeli.AccessMm / 1000}); Hugo: 95 m");
                var rPaso = map.Route(99_000, 53_000, 59_000, 62_000, 375, 600, 60_000);
                check(rPaso.Ok && rPaso.LengthMm > 60_000, $"ruta parque → paso a nivel: {rPaso.LengthMm / 1000} m; Hugo: 110 m");
                // lo deshabilitado no se usa: ni el paso superior ni el tramo de la carretera vieja pasado el Palancia (x > 222 m)
                var rUp = map.Route(-20_000, -20_000, -20_000, 100_000, 375);
                bool usesUp = false; for (int i = 0; i + 1 < rUp.Count; i++) if (rUp.Kinds[i] == RoadKind.Carretera && rUp.X[i] == -20_000 && rUp.X[i + 1] == -20_000) usesUp = true;
                var rEast = map.Route(200_000, 23_000, 300_000, 23_000, 375); bool usesEast = false; for (int i = 0; i + 1 < rEast.Count; i++) if (rEast.Kinds[i] == RoadKind.Carretera && Math.Max(rEast.X[i], rEast.X[i + 1]) > 222_000) usesEast = true;
                check(rUp.Ok && !usesUp && rEast.Ok && !usesEast && rEast.ViaMm > 50_000, $"no pasa por el paso superior (coches abandonados) ni por la carretera pasado el Palancia (camión): el este se hace por la vía ({rEast.ViaMm / 1000} m de vía)");
                map.SetRoadEnabled("paso_superior", true); var rUp2 = map.Route(-20_000, -20_000, -20_000, 100_000, 375); bool usesUp2 = false; for (int i = 0; i + 1 < rUp2.Count; i++) if (rUp2.Kinds[i] == RoadKind.Carretera && rUp2.X[i] == -20_000 && rUp2.X[i + 1] == -20_000) usesUp2 = true;
                map.SetRoadEnabled("paso_superior", false);
                check(usesUp2 && rUp2.LengthMm < rUp.LengthMm, "al habilitar el paso superior, la ruta lo usa (el dato manda)");
                var rNone = map.Route(99_000, 53_000, 300_000, 300_000, 375);
                check(!rNone.Ok && rNone.Why.Contains("sin ruta"), $"un destino a campo abierto (más de 60 m de una carretera habilitada o de la vía) no tiene ruta: «{rNone.Why}»");
                map.BlockNear(-250_000, 64_500, 1_000, true); var rBlk = map.Route(-240_000, 64_500, 100_000, 64_500, 375); map.BlockNear(-250_000, 64_500, 1_000, false);
                check(map.Route(-240_000, 64_500, 100_000, 64_500, 375).Ok, "bloquear y desbloquear un punto de la red es reversible");
                // tiempos: ETA real = km ÷ km/h × 3600 ÷ 28 (D-N72), igual que la tabla de Hugo (eta_red)
                bool etaOk = TransportDirector.EtaMapSeconds(44_000, 80) == 71 && TransportDirector.EtaMapSeconds(40_000, 30) == 171 && TransportDirector.EtaMapSeconds(40_000, 25) == 206 && TransportDirector.EtaMapSeconds(44_000, 50) == 113 && TransportDirector.EtaMapSeconds(33_000, 90) == 47;
                check(etaOk && TransportDirector.GameMinutes(71) == 33 && TransportDirector.GameMinutes(171) == 80 && TransportDirector.FuelE4For(VehicleKind.Dragon, 71) == 14_200 && TransportDirector.FuelE4For(VehicleKind.Dragon, 171) == 34_200 && TransportDirector.FuelE4For(VehicleKind.Vamtac, 63) == 5_670 && TransportDirector.FuelE4For(VehicleKind.Pizarro, 113) == 28_250,
                    "ETA de ruta entre estaciones = tabla de Hugo (Dragón 44 km a 80 km/h: 71 s reales, 33 min de juego, 1,4 u; vía 171 s y 3,4 u; VAMTAC 206 s; Pizarro 113 s, 2,8 u)");
                var tw = new UnitsWorld(1); var td = new TransportDirector(tw, map);
                bool zoneEta = true; string zoneTxt = "";
                foreach (var row in new[] { new object[] { 20, VehicleKind.Dragon, 6 }, new object[] { 40, VehicleKind.Dragon, 8 }, new object[] { 95, VehicleKind.Dragon, 13 }, new object[] { 110, VehicleKind.Dragon, 14 }, new object[] { 130, VehicleKind.Dragon, 16 },
                                            new object[] { 95, VehicleKind.Vamtac, 11 }, new object[] { 130, VehicleKind.Vamtac, 13 }, new object[] { 95, VehicleKind.Pizarro, 13 }, new object[] { 130, VehicleKind.Pizarro, 15 } })
                {
                    var fake = new RoutePath { Ok = true, RoadMm = (int)row[0] * 1000L, LengthMm = (int)row[0] * 1000L }; int e = td.EtaZoneSeconds((VehicleKind)row[1], fake);
                    zoneEta &= Math.Abs(e - (int)row[2]) <= 1; zoneTxt += $" {row[1].ToString().Substring(0, 3)}{row[0]}m={e}s({row[2]})";
                }
                check(zoneEta, "ETA de la zona = 5 s + longitud ÷ velocidad, dentro de ±1 s de la tabla de Hugo (eta_zona_s_reales):" + zoneTxt);
                int vsl = td.ViaKmh(VehicleKind.Dragon) * 1000 / td.RoadKmh(VehicleKind.Dragon);
                var viaOnly = new RoutePath { Ok = true, ViaMm = 100_000, LengthMm = 100_000 }; var roadOnly = new RoutePath { Ok = true, RoadMm = 100_000, LengthMm = 100_000 };
                check(vsl == 375 && td.EtaZoneSeconds(VehicleKind.Dragon, viaOnly) > 2 * td.EtaZoneSeconds(VehicleKind.Dragon, roadOnly) - 10 && td.EtaZoneSeconds(VehicleKind.Dragon, viaOnly) < 3 * td.EtaZoneSeconds(VehicleKind.Dragon, roadOnly), $"por la vía el Dragón va a 30/80 de la carretera ({td.EtaZoneSeconds(VehicleKind.Dragon, viaOnly)} s frente a {td.EtaZoneSeconds(VehicleKind.Dragon, roadOnly)} s en 100 m)");
                check(TransportDirector.EtaAirSeconds(HeliKind.Tigre, 180_000, 44_000, 180_000, 44_000) > 0 && TransportDirector.EtaAirSeconds(HeliKind.Tigre, 0, 0, 3_000_000, 0) > TransportDirector.EtaAirSeconds(HeliKind.Tigre, 0, 0, 300_000, 0), "los helicópteros van en línea recta: el ETA crece con la distancia");

                {
                    var w = new UnitsWorld(71); var pl = w.AddPlatoon(PlatoonKind.Fusileros, 99_000, 53_000, 0); var d = new TransportDirector(w, RoadMap.Sagunto(rj)); d.Vehicles.Add(new Vehicle(1, VehicleKind.Dragon, 99_500, 53_000, 0));
                    var eR = d.Estimate(pl.Id, 180_000, 44_000, TransportMode.Vehiculo); var eV = d.Estimate(pl.Id, 300_000, 23_000, TransportMode.Vehiculo);
                    bool legs = eR.RouteLegs.Count == eR.RouteX.Count - 1 && eR.RouteType == TransitRoute.Carretera && eV.RouteLegs.Contains(TransitRoute.Via) && eV.ViaMm > 50_000;
                    var air = TransportDirector.AirTransit(HeliKind.Nh90, 180_000, 44_000, 280_000, 144_000, 30 * 10); var air0 = TransportDirector.AirTransit(HeliKind.Nh90, 180_000, 44_000, 280_000, 144_000, 0); var airEnd = TransportDirector.AirTransit(HeliKind.Nh90, 180_000, 44_000, 280_000, 144_000, 30 * 600);
                    check(legs && air.Active && air.Route == TransitRoute.Aire && air.Class == TransitClass.Helicoptero && air.ProgressPm > air0.ProgressPm && air.SecondsLeft < air0.SecondsLeft && air0.SecondsLeft == TransportDirector.EtaAirSeconds(HeliKind.Nh90, 180_000, 44_000, 280_000, 144_000) && !airEnd.Active && airEnd.ProgressPm == 1000 && airEnd.SecondsLeft == 0 && air.Heading == 8192,
                        $"tipo de ruta por tramo (carretera hasta el helipuerto; al este, por la vía) y tránsito aéreo en línea recta (a los 10 s: {air.ProgressPm / 10.0:0.0} %, faltan {air.SecondsLeft} de {air0.SecondsLeft} s, rumbo {air.Heading * 360 / 65536}°)");
                }
                {
                    // puesto bajo ataque (D-N117): el contacto de la horda sale por pelotón, para que el mapa lo mantenga encendido unos segundos
                    var w = new UnitsWorld(81); var pa = w.AddPlatoon(PlatoonKind.Fusileros, 50_000, 50_000, 0); var pb = w.AddPlatoon(PlatoonKind.Fusileros, 150_000, 50_000, 0);
                    var zs = new List<Target>(); for (int i = 0; i < 12; i++) zs.Add(new Target { Id = 1 + i, X = 50_000 + (i % 4) * 200, Y = 50_000 + (i / 4) * 200 });
                    var cr = new ContactResult(); int fallen = w.ZombieContact(zs, 900, 2, cr);
                    int sumC = 0, sumD = 0; foreach (var h in cr.Hurt) { sumC += h.Contacts; sumD += h.Damage; }
                    bool onlyA = cr.Hurt.Count == 1 && cr.Hurt[0].Platoon == pa.Id && cr.Hurt[0].Contacts > 0 && cr.Hurt[0].Damage > 0;
                    var cr2 = new ContactResult(); w.ZombieContact(new List<Target>(), 900, 2, cr2);
                    check(onlyA && sumC == cr.Contacts && sumD == cr.Damage && cr2.Hurt.Count == 0, $"el contacto de la horda sale por pelotón: solo el A ({cr.Hurt[0].Contacts} contactos, {cr.Hurt[0].Damage} de vida), el B lejos no; sin zombis, nada");
                }
                {
                    // ------------------------------------------------ traza de la noche: la herramienta que compara la zona de Unity con NightSim
                    string nj = TBD.Net.BalanceSource.Read("noche_sagunto.json"); int t0 = NightTraceTools.FirstWaveTick(nj, null, 1);
                    var refT = NightTraceTools.Reference(nj, null, 1, t0, 9000, 30);
                    var csv = refT.ToCsv(); var back = NightTrace.FromCsv(csv);
                    bool rt = back.Rows.Count == refT.Rows.Count && back.Source == "NightSim" && back.ToCsv() == csv && refT.Rows.Count >= 2 && refT.Rows.Count <= 301 && refT.Rows[0].Tick >= t0 && refT.Rows[0].Tick <= t0 + 30;   // D-N136: la traza acaba antes si se acaba con la horda
                    var lastR = refT.Rows[refT.Rows.Count - 1]; int peakR = 0; foreach (var rw in refT.Rows) peakR = Math.Max(peakR, rw.Pile); bool mono = true; for (int i = 1; i < refT.Rows.Count; i++) mono &= refT.Rows[i].Spawned >= refT.Rows[i - 1].Spawned && refT.Rows[i].Kills >= refT.Rows[i - 1].Kills;
                    check(rt && mono && peakR > 50 && lastR.Spawned > 1500, $"traza de referencia de NightSim: {refT.Rows.Count} filas (1 por segundo, hasta 9.000 ticks desde la horda), ida y vuelta por CSV idéntica; al final entraron {lastR.Spawned}, abatidos {lastR.Kills}, pila máxima {peakR}, muro {lastR.WallPpm / 10000.0:0.0} %");
                    // la misma traza contra sí misma: todo correcto
                    var same = NightTraceTools.Compare(refT, NightTrace.FromCsv(csv)); 
                    check(same.Ok && same.Warns == 0, $"una traza contra sí misma no da fallos ni avisos ({same.Oks} correctas)");
                    // la zona de Noche15: la pila siempre a 0, el muro al 100 % y 6.845 vivos → debe fallar y decir por qué
                    var broken = NightTrace.FromCsv(csv); broken.Source = "zona rota"; for (int i = 0; i < broken.Rows.Count; i++) { var r = broken.Rows[i]; r.Pile = 0; r.WallPpm = 1_000_000; broken.Rows[i] = r; }
                    var rb = NightTraceTools.Compare(refT, broken);
                    check(!rb.Ok && rb.Text.Contains("la pila de la zona es 0") && rb.Text.Contains("el muro de la zona no baja"), "la zona de Noche15 (pila 0 y muro al 100 %) da FALLO con las dos causas: pila no contada y muro que no baja");
                    // entradas distintas (la zona no usa NightGame.Step) y conservación rota
                    var badSp = NightTrace.FromCsv(csv); var rr = badSp.Rows[100]; rr.Spawned += 5; badSp.Rows[100] = rr; var lr = badSp.Rows[badSp.Rows.Count - 1]; lr.Alive += 400; badSp.Rows[badSp.Rows.Count - 1] = lr;
                    var rs2 = NightTraceTools.Compare(refT, badSp);
                    check(!rs2.Ok && rs2.Text.Contains("zombis que entran: difieren en 1 de") && rs2.Text.Contains("conservación rota"), "detecta que las entradas de la zona no coinciden con NightGame y que no se conservan los zombis");
                    // pila que existe pero con otra escala de muro: AVISO, no fallo
                    var scaled = NightTrace.FromCsv(csv); scaled.Source = "zona con muro 2x"; for (int i = 0; i < scaled.Rows.Count; i++) { var r = scaled.Rows[i]; r.WallPpm = 1_000_000 - (1_000_000 - r.WallPpm) / 2; scaled.Rows[i] = r; }
                    var rsc = NightTraceTools.Compare(refT, scaled);
                    check(rsc.Ok && rsc.Warns >= 1 && rsc.Text.Contains("veces el esperado"), $"un muro que se gasta a la mitad (otra escala de vida) da AVISO con la razón, no fallo ({rsc.Warns} avisos)");
                    // una zona con la pila que cuadra en tiempo pero sin fuego: avisa de los abatidos
                    var noFire = NightTrace.FromCsv(csv); for (int i = 0; i < noFire.Rows.Count; i++) { var r = noFire.Rows[i]; r.Kills = 0; r.Alive = r.Spawned; noFire.Rows[i] = r; }
                    var rn = NightTraceTools.Compare(refT, noFire);
                    check(rn.Text.Contains("abatidos: 0 en la zona"), "sin abatidos en la zona avisa de que el fuego no se aplica");
                    // el grabador: solo guarda dentro de la ventana y cada N ticks
                    var rec = new NightTraceRecorder("zona", 1000, 300, 30); for (int tk = 0; tk < 2000; tk++) rec.Sample(tk, tk, 1, 0, 960_000, 960_000, 0, 5, 0, 0);
                    check(rec.Trace.Rows.Count == 11 && rec.Trace.Rows[0].Tick == 1000 && rec.Trace.Rows[10].Tick == 1300 && rec.Trace.Rows[0].WallPpm == 1_000_000 && rec.Done, "el grabador guarda un tick de cada 30 dentro de la ventana (11 filas) y convierte la vida del muro a ppm");
                }
                {
                    // los valores por defecto de NightSimParams son los de nightsim_params.json (Hugo) y NightSim los lee de ahí
                    var dj = NightSimParams.Default(); var fj = NightSimParams.FromJson(TBD.Net.BalanceSource.Read("nightsim_params.json")); var sim0 = NightSim.FromJson(TBD.Net.BalanceSource.Read("noche_sagunto.json"), null, 1);
                    bool same = dj.FortX0 == fj.FortX0 && dj.FortX1 == fj.FortX1 && dj.FortY0 == fj.FortY0 && dj.FortY1 == fj.FortY1 && dj.StationX == fj.StationX && dj.StationY == fj.StationY && dj.WallHp == fj.WallHp && dj.WallDmgPerZombieS == fj.WallDmgPerZombieS
                        && dj.StationHp == fj.StationHp && dj.StationDmgPerZombieS == fj.StationDmgPerZombieS && dj.ZombieSpeedMmS == fj.ZombieSpeedMmS && dj.ContactRadiusMm == fj.ContactRadiusMm && dj.ContactCasualtyPmPerS == fj.ContactCasualtyPmPerS && dj.PileRadiusMm == fj.PileRadiusMm;
                    var cut = NightSimParams.FromJson("{\"NightSimParams\": {\"WallHp\": 777}}"); var none = NightSimParams.FromJson(null);
                    check(same && fj.WallHp == 1_200_000 && sim0.WallHp == 1_200_000 && sim0.SP.WallHp == fj.WallHp && cut.WallHp == 777 && cut.StationHp == dj.StationHp && none.WallHp == dj.WallHp, $"NightSimParams: el código coincide con nightsim_params.json (muro {fj.WallHp}), NightSim lo lee de ahí, un JSON parcial solo cambia lo que dice y sin JSON valen los del código");
                }
                {
                    // ------------------------------------------------ NightBot (-nightbot): mismas órdenes que el jugador, determinista
                    var bw = new UnitsWorld(91); var pf = bw.AddPlatoon(PlatoonKind.Fusileros, 100_000, 60_000, 0); var pg = bw.AddPlatoon(PlatoonKind.Granaderos, 110_000, 60_000, 0); var pz = bw.AddPlatoon(PlatoonKind.Zapadores, 120_000, 70_000, 0); var pm = bw.AddPlatoon(PlatoonKind.Ametralladores, 105_000, 66_000, 0);
                    var sp0 = NightSimParams.Default(); var bot = NightBot.FromParams(sp0); bw.StockShots = 20_000; pf.AmmoShots = pf.AmmoCap * 20 / 100; int stock0 = bw.StockShots;
                    // horda de 120 zombis pegada al lado X+ (este) del recinto, a unos 6 m fuera
                    var hz = new List<Target>(); for (int i = 0; i < 120; i++) hz.Add(new Target { Id = i, X = sp0.FortX1 + 6_000 + (i % 4) * 1_000, Y = 55_000 + (i / 4) * 500 });
                    bot.Step(bw, hz, 1); int ordersTick1 = bot.Orders; bot.Step(bw, hz, 0);
                    check(ordersTick1 == 0 && bot.Decisions == 1, "el bot solo decide cada 60 ticks (en el tick 1 no hace nada, en el 0 sí)");
                    check(pf.AmmoShots == pf.AmmoCap && bw.StockShots < stock0 && bot.Resupplies >= 1, $"reabastece con la orden del jugador al bajar del 30 % (el fusilero estaba al 20 %: ahora {pf.AmmoShots}/{pf.AmmoCap}, el almacén bajó {stock0 - bw.StockShots} disparos)");
                    bool toEast = pf.Moving && pf.AttackMove && pf.TX > (sp0.FortX0 + sp0.FortX1) / 2 && pg.Moving && pg.AttackMove && pg.TX > (sp0.FortX0 + sp0.FortX1) / 2 && pm.Moving && pm.TX > (sp0.FortX0 + sp0.FortX1) / 2;
                    int distF = sp0.FortX1 - pf.TX, distG = sp0.FortX1 - pg.TX;
                    check(toEast && distG >= 15_000 && distG > distF && distF >= 2_000, $"con la horda al este, los pelotones de línea van hacia el muro este con ataque-movimiento; los lanzagranadas se quedan a {distG / 1000} m del muro (mínimo 15 m) y los fusileros a {distF / 1000} m");
                    check(!pz.AttackMove && IMath.Dist(pz.TX - sp0.StationX, pz.TY - sp0.StationY) < 12_000 && pz.Moving, "los zapadores no van al muro: esperan junto al edificio");
                    int o1 = bot.Orders; bot.Step(bw, hz, 60); check(bot.Orders == o1, "con la misma amenaza no repite órdenes (nada de saturar con órdenes iguales)");
                    var off = NightBot.FromParams(sp0); off.Enabled = false; var w2 = new UnitsWorld(92); var q2 = w2.AddPlatoon(PlatoonKind.Fusileros, 100_000, 60_000, 0); w2.StockShots = 9000; q2.AmmoShots = 10; off.Step(w2, hz, 0);
                    check(q2.AmmoShots == 10 && !q2.Moving && off.Orders == 0 && off.Decisions == 0, "con Enabled = false (sin la bandera) el bot no toca nada");
                    // una noche entera con el bot: gana y es determinista
                    Func<uint, ulong> botNight = seed => { var n = NightSim.FromJson(TBD.Net.BalanceSource.Read("noche_sagunto.json"), null, seed); n.AddDefaultForce(); n.Bot = NightBot.FromParams(n.SP); n.Run(9000); return n.Hash() ^ ((ulong)n.Bot.Orders << 40) ^ ((ulong)n.Bot.Resupplies << 52); };
                    check(botNight(5) == botNight(5) && botNight(5) != botNight(6), "el bot es determinista: misma semilla, mismo hash y mismas órdenes; otra semilla, otro");
                    var full = NightSim.FromJson(TBD.Net.BalanceSource.Read("noche_sagunto.json"), null, 1); full.SP.ClimbPermilS = 20; full.ClimbMaxPerS = 0; full.AddDefaultForce();     // D-N128 con la trepada antigua; con la calibrada (Noche58a) 7310 pierde, como la build
 full.AddDefaultVehicles(true); full.Bot = NightBot.FromParams(full.SP); while (full.Night.State == NightState.Jugando && full.Night.Tick < full.TickCap) full.Run(150);
                    var fs = full.Snapshot();
                    check(fs.State == NightState.Victoria && fs.WallHp * 100 / full.SP.WallHp > 60 && full.Bot.Resupplies > 3 && full.Resupplies == 0, $"la noche normal entera con el bot y los vehículos en fuego libre, trepada antigua (D-N128; sin ayuda de recarga aparte): {fs.State}, muro {fs.WallHp * 100 / full.SP.WallHp} %, {full.Bot.Resupplies} reabastecimientos, {fs.SoldierDeaths} bajas");
                    // hueco de la vía (la zona de Unity no pone sólido a la puerta de vía): la horda rodea el muro hasta el hueco, no se amontona y el muro no se desgasta
                    Func<int, NightSim> gapRun = gap => { var n = NightSim.FromJson(TBD.Net.BalanceSource.Read("noche_sagunto.json"), null, 1); n.SP.GateGapMm = gap; n.AddDefaultForce(); n.Run(4500); return n; };
                    var gClosed = gapRun(0); var gOpen = gapRun(3200);
                    check(gClosed.PeakPile > 20 && gClosed.WallHp < gClosed.SP.WallHp && gOpen.PeakPile < gClosed.PeakPile && gOpen.WallHp > gClosed.WallHp, $"recinto cerrado: la horda amontona {gClosed.PeakPile} y gasta muro; con hueco de 3,2 m en la vía oeste, menos pila ({gOpen.PeakPile}) y menos desgaste (D-N136: el puente O4 sigue apilando en el este)");
                }
                // ---------------------------------------------------- elección del vehículo: solo uno LIBRE, entero, con plazas y combustible
                Func<UnitsWorld, TransportDirector> mk = w => new TransportDirector(w, RoadMap.Sagunto(rj));
                var sr1 = new StepResult(); var noT = new List<Target>();
                {
                    var w = new UnitsWorld(11); var pl = w.AddPlatoon(PlatoonKind.Fusileros, 95_000, 50_000, 0); var d = mk(w); int men = pl.AliveCount();
                    var e0 = d.Estimate(pl.Id, 180_000, 44_000, TransportMode.Vehiculo);
                    check(!e0.Ok && e0.Why.Contains("ningún vehículo libre"), $"sin vehículos no hay transporte: «{e0.Why}»");
                    var busy = new Vehicle(1, VehicleKind.Dragon, 99_000, 53_000, 0); busy.OnJob = true; var other = new UnitsWorld(1);
                    var pas = w.AddPlatoon(PlatoonKind.Fusileros, 99_500, 53_000, 0); var full = new Vehicle(2, VehicleKind.Dragon, 99_000, 54_000, 0); full.Board(w, pas.Id);
                    var wreck = new Vehicle(3, VehicleKind.Dragon, 99_000, 55_000, 0); wreck.Hp = 0; var dry = new Vehicle(4, VehicleKind.Dragon, 99_000, 56_000, 0); dry.FuelE4 = 1000;
                    d.Vehicles.AddRange(new[] { busy, full, wreck, dry });
                    var e1 = d.Estimate(pl.Id, 180_000, 44_000, TransportMode.Vehiculo);
                    check(!e1.Ok && e1.Why.Contains("combustible"), $"ocupado, con pasajeros, destruido o sin gasolina: ninguno vale ({(e1.Ok ? "elegido " + e1.VehicleId : e1.Why)})");
                    var vam = new Vehicle(5, VehicleKind.Vamtac, 99_000, 57_000, 0);
                    var small = new UnitsWorld(1); d.Vehicles.Clear(); d.Vehicles.Add(vam);
                    var e2 = d.Estimate(pl.Id, 180_000, 44_000, TransportMode.Vehiculo);
                    check(men <= 4 ? e2.Ok : (!e2.Ok && e2.Why.Contains("no cabe")), $"el pelotón de {men} no cabe en un VAMTAC de 4 plazas: {(e2.Ok ? "sí cabe" : e2.Why)}");
                    var near = new Vehicle(6, VehicleKind.Dragon, 99_000, 53_000, 0); var farV = new Vehicle(7, VehicleKind.Dragon, 120_000, 60_000, 0);
                    d.Vehicles.Clear(); d.Vehicles.AddRange(new[] { farV, near });
                    var e3 = d.Estimate(pl.Id, 180_000, 44_000, TransportMode.Vehiculo);
                    check(e3.Ok && e3.VehicleId == 6 && e3.RouteMm > 60_000 && e3.EtaTotalS > 8 && e3.EtaGameMin == TransportDirector.GameMinutes(e3.EtaTotalS) && e3.FuelE4 == e3.EtaTotalS * 200 && e3.RouteX.Count >= 2 && e3.ClockX == 28,
                        $"de dos libres elige el que antes llega (el {e3.VehicleId}); ETA {e3.EtaTotalS} s reales = {e3.EtaGameMin} min de juego, {e3.RouteMm / 1000} m, {e3.FuelE4 / 10_000.0:0.0} u de gasolina, {e3.RouteX.Count} puntos de ruta");
                    var e4 = d.Estimate(pl.Id, 300_000, 300_000, TransportMode.Vehiculo);
                    var e5 = d.Estimate(pl.Id, 300_000, 300_000, TransportMode.VehiculoOPie);
                    check(!e4.Ok && e4.Why.Contains("sin ruta") && e5.Ok && e5.OnFoot, $"destino sin carretera: con vehículo se niega («{e4.Why}»); VehiculoOPie va a pie ({e5.EtaTotalS} s)");
                    string why; var nj = d.Order(pl.Id, 300_000, 300_000, TransportMode.Vehiculo, out why);
                    check(nj == null && why != null && !near.OnJob && !farV.OnJob && d.Jobs.Count == 0, "una orden negada no reserva ningún vehículo ni crea trabajo");
                }

                // ---------------------------------------------------- una orden entera: el vehículo recoge, embarca, conduce, llega y desembarca
                Func<uint, Tuple<UnitsWorld, TransportDirector, Platoon, Vehicle>> scene = (seed) =>
                {
                    var w = new UnitsWorld(seed); var pl = w.AddPlatoon(PlatoonKind.Fusileros, 94_000, 52_000, 0); var d = mk(w);
                    var v = new Vehicle(1, VehicleKind.Dragon, 99_000, 53_000, 0); d.Vehicles.Add(v); return Tuple.Create(w, d, pl, v);
                };
                {
                    var sc = scene(21u); var w = sc.Item1; var d = sc.Item2; var pl = sc.Item3; var v = sc.Item4; string why;
                    var est = d.Estimate(pl.Id, 180_000, 44_000, TransportMode.Vehiculo);
                    var job = d.Order(pl.Id, 180_000, 44_000, TransportMode.Vehiculo, out why);
                    check(job != null && v.OnJob && job.Phase == TransportPhase.PelotonVaAlVehiculo && d.Estimate(pl.Id, 100_000, 44_000, TransportMode.Vehiculo).Why.Contains("ya tiene"), "la orden reserva el vehículo; el pelotón ya no admite otra orden mientras tanto");
                    var tr0 = d.Transit(job.Id);
                    bool seenBoard = false, seenRoute = false; int boardTick = -1, arriveTick = -1; ulong hBoard = 0; ulong hFree = w.Hash();
                    int lastPm = -1, lastLeft = 1_000_000; bool monoPm = true, monoLeft = true, headOk = true, classOk = true, routeKinds = true; int samples = 0; TransitInfo trMid = default(TransitInfo);
                    for (int t = 0; t < 30 * 120 && job.Phase != TransportPhase.Llegado && job.Phase != TransportPhase.Fallido; t++)
                    {
                        w.Step(noT, sr1); d.Step();
                        if (!seenBoard && pl.Aboard == v.CarrierId) { seenBoard = true; boardTick = t; hBoard = w.Hash(); }
                        if (job.Phase == TransportPhase.EnRuta) seenRoute = true;
                        var tr = d.Transit(job.Id);
                        if (tr.Active)
                        {
                            samples++; monoPm &= tr.ProgressPm >= lastPm; lastPm = tr.ProgressPm; monoLeft &= tr.SecondsLeft <= lastLeft + 1; lastLeft = Math.Min(lastLeft, tr.SecondsLeft);
                            headOk &= tr.Heading == IMath.Atan2(tr.SegDy, tr.SegDx) && (tr.SegDx != 0 || tr.SegDy != 0); classOk &= job.Phase == TransportPhase.EnRuta ? tr.Class == TransitClass.Vehiculo : tr.Class == TransitClass.Peloton;
                            routeKinds &= tr.Route == TransitRoute.Carretera || tr.Route == TransitRoute.Via; if (job.Phase == TransportPhase.EnRuta && t % 10 == 0 && tr.ProgressPm > 300 && trMid.TotalMm == 0) trMid = tr;
                        }
                        if (job.Phase == TransportPhase.Llegado) arriveTick = t;
                    }
                    for (int t = 0; t < 60; t++) { w.Step(noT, sr1); d.Step(); }
                    int dx = IMath.Dist(pl.AX - 180_000, pl.AY - 44_000); double realS = w.Tick / 30.0;
                    check(job.Phase == TransportPhase.Llegado && seenBoard && seenRoute, $"el pelotón embarca (tick {boardTick}), el vehículo conduce por la ruta y llega: fase final {job.Phase}");
                    check(pl.Aboard == 0 && !v.OnJob && w.SeatsUsed(v.CarrierId) == 0 && dx < 15_000, $"desembarca y el vehículo queda libre; el pelotón está a {dx / 1000.0:0.0} m del destino");
                    var trEnd = d.Transit(job.Id);
                    check(tr0.Active && tr0.Class == TransitClass.Peloton && tr0.Route == TransitRoute.Carretera && tr0.ProgressPm == 0 && tr0.SecondsLeft >= est.EtaTotalS - 1 && !trEnd.Active && trEnd.SecondsLeft == 0,
                        $"datos del mapa: al empezar, pelotón con avance 0 y ETA {tr0.SecondsLeft} s; al llegar, ya no está en tránsito");
                    check(samples > 100 && monoPm && monoLeft && headOk && classOk && routeKinds && lastPm > 850, $"datos del mapa durante el viaje ({samples} muestras): el avance por distancia solo sube (hasta {lastPm / 10.0:0.0} %), los segundos que faltan solo bajan, el rumbo es el del tramo, pelotón → vehículo al embarcar");
                    check(trMid.Active && trMid.Class == TransitClass.Vehiculo && trMid.TotalMm > 100_000 && trMid.DoneMm > 0 && trMid.DoneMm < trMid.TotalMm && trMid.SecondsLeft > 0 && trMid.SecondsLeft < est.EtaRouteS, $"a mitad de camino: {trMid.ProgressPm / 10.0:0.0} % ({trMid.DoneMm / 1000} de {trMid.TotalMm / 1000} m), faltan {trMid.SecondsLeft} s, rumbo {trMid.Heading * 360 / 65536}°");
                    check(hBoard != hFree && w.Hash() != hBoard, "el estar a bordo cuenta en el hash de la simulación (Aboard)");
                    check(est.EtaTotalS > 8 && realS - 2 < est.EtaTotalS * 1.4 && realS - 2 > est.EtaTotalS * 0.8, $"el ETA ({est.EtaTotalS} s) se parece al tiempo real del trayecto ({realS:0.0} s, incluyendo 2 s de espera extra): el mapa no miente");
                    var free = d.Estimate(pl.Id, 100_000, 44_000, TransportMode.Vehiculo);
                    check(free.Ok && free.VehicleId == 1, "tras llegar, el vehículo vuelve a estar libre para otra orden");
                }
                // un segundo recorrido idéntico da el mismo hash (determinismo del director y del piloto automático)
                {
                    ulong[] hs = new ulong[2]; int[] vx = new int[2];
                    for (int k = 0; k < 2; k++)
                    {
                        var sc = scene(21u); string why; sc.Item2.Order(sc.Item3.Id, 180_000, 44_000, TransportMode.Vehiculo, out why);
                        for (int t = 0; t < 30 * 40; t++) { sc.Item1.Step(noT, sr1); sc.Item2.Step(); }
                        hs[k] = sc.Item1.Hash(); vx[k] = sc.Item4.X ^ (sc.Item4.Y << 12) ^ sc.Item4.Heading;
                    }
                    check(hs[0] == hs[1] && vx[0] == vx[1], "el mismo envío dos veces: mismo hash de la simulación y mismo punto del vehículo");
                }
                // el vehículo está lejos del pelotón: va a buscarlo por carretera
                {
                    var w = new UnitsWorld(31); var pl = w.AddPlatoon(PlatoonKind.Fusileros, 59_000, 62_000, 0); var d = mk(w); var v = new Vehicle(1, VehicleKind.Dragon, 99_000, 53_000, 0); d.Vehicles.Add(v); string why;
                    var est = d.Estimate(pl.Id, 180_000, 44_000, TransportMode.Vehiculo);
                    var job = d.Order(pl.Id, 180_000, 44_000, TransportMode.Vehiculo, out why);
                    check(job != null && job.Phase == TransportPhase.VehiculoVaPorElPeloton && est.PickupMm > 50_000 && est.PickupX.Count >= 2 && est.EtaPickupS > 5, $"con el vehículo lejos primero va a recoger al pelotón: {est.PickupMm / 1000} m, {est.EtaPickupS} s");
                    bool picked = false, boarded = false; for (int t = 0; t < 30 * 240 && job.Phase != TransportPhase.Llegado && job.Phase != TransportPhase.Fallido; t++) { w.Step(noT, sr1); d.Step();if (job.Phase == TransportPhase.PelotonVaAlVehiculo) picked = true; if (pl.Aboard != 0) boarded = true; }
                    for (int t = 0; t < 60; t++) { w.Step(noT, sr1); d.Step(); }
                    int dx = IMath.Dist(pl.AX - 180_000, pl.AY - 44_000);
                    check(picked && boarded && job.Phase == TransportPhase.Llegado && dx < 15_000 && !v.OnJob && Math.Abs(w.Tick / 30.0 - est.EtaTotalS) < est.EtaTotalS * 0.35, $"recoge al pelotón, lo lleva al helipuerto ({dx / 1000.0:0.0} m del punto) y queda libre; total {w.Tick / 30.0:0.0} s reales (ETA {est.EtaTotalS} s)");
                }
                // cancelar a mitad de camino: el vehículo para, el pelotón baja donde está y queda libre
                {
                    var sc = scene(41u); var w = sc.Item1; var d = sc.Item2; var pl = sc.Item3; var v = sc.Item4; string why;
                    var job = d.Order(pl.Id, 180_000, 44_000, TransportMode.Vehiculo, out why);
                    for (int t = 0; t < 30 * 40 && job.Phase != TransportPhase.EnRuta; t++) { w.Step(noT, sr1); d.Step(); }
                    for (int t = 0; t < 30 * 6; t++) { w.Step(noT, sr1); d.Step(); }
                    bool wasAboard = pl.Aboard != 0 && v.SpeedMmS > 1000; int vxBefore = v.X;
                    bool canceled = d.Cancel(job.Id); for (int t = 0; t < 60; t++) { w.Step(noT, sr1); d.Step(); v.Controls(0, 0, 1000); v.Step(); }
                    check(wasAboard && canceled && job.Phase == TransportPhase.Cancelado && pl.Aboard == 0 && !v.OnJob && !d.Cancel(job.Id), "cancelar en ruta: el pelotón baja, el vehículo queda libre y no se puede cancelar dos veces");
                }
                // un vehículo destruido o sin gasolina por el camino: la orden falla limpiamente y suelta al pelotón
                {
                    var sc = scene(51u); var w = sc.Item1; var d = sc.Item2; var pl = sc.Item3; var v = sc.Item4; string why;
                    var job = d.Order(pl.Id, 180_000, 44_000, TransportMode.Vehiculo, out why);
                    for (int t = 0; t < 30 * 40 && job.Phase != TransportPhase.EnRuta; t++) { w.Step(noT, sr1); d.Step(); }
                    for (int t = 0; t < 30 * 3; t++) { w.Step(noT, sr1); d.Step(); }
                    v.FuelE4 = 0; w.Step(noT, sr1); d.Step();
                    check(job.Phase == TransportPhase.Fallido && pl.Aboard == 0 && !v.OnJob, "si se queda sin gasolina en ruta, la orden falla y el pelotón baja");
                }
                // a pie: a campo través y más despacio (D-N96)
                {
                    var w = new UnitsWorld(61); var pl = w.AddPlatoon(PlatoonKind.Fusileros, 95_000, 52_000, 0); var d = mk(w); string why; int speed0 = pl.SpeedMmS;
                    var est = d.Estimate(pl.Id, 140_000, 70_000, TransportMode.VehiculoOPie);
                    var job = d.Order(pl.Id, 140_000, 70_000, TransportMode.VehiculoOPie, out why);
                    int slow = pl.SpeedMmS; for (int t = 0; t < 30 * 200 && job.Phase == TransportPhase.APie; t++) { w.Step(noT, sr1); d.Step(); }
                    int dx = IMath.Dist(pl.AX - 140_000, pl.AY - 70_000);
                    long straight = IMath.Dist(140_000 - 95_000, 70_000 - 52_000);
                    check(est.Ok && est.OnFoot && job.Phase == TransportPhase.Llegado && slow == speed0 * 700 / 1000 && pl.SpeedMmS == speed0 && dx < 5_000 && Math.Abs(w.Tick / 30.0 - est.EtaTotalS) < 6, $"sin vehículo, el pelotón va a campo través al 70 % ({speed0 / 1000.0:0.0} → {slow / 1000.0:0.0} m/s), llega ({w.Tick / 30.0:0.0} s; ETA {est.EtaTotalS}) y recupera su velocidad");
                    var pl2 = w.AddPlatoon(PlatoonKind.Fusileros, 95_000, 52_000, 0); var job2 = d.Order(pl2.Id, 140_000, 70_000, TransportMode.APie, out why); for (int t = 0; t < 40; t++) { w.Step(noT, sr1); d.Step(); }
                    var trF = d.Transit(job2.Id); bool footOk = trF.Active && trF.Route == TransitRoute.Campo && trF.Class == TransitClass.Peloton && trF.ProgressPm >= 0 && trF.ProgressPm < 1000 && trF.SecondsLeft > 0;
                    var estF = d.Estimate(pl2.Id, 140_000, 70_000, TransportMode.APie); // ya tiene orden: no hay estimación
                    check(footOk && !estF.Ok, $"a pie: tipo de ruta «campo», clase pelotón, avance {trF.ProgressPm / 10.0:0.0} %, faltan {trF.SecondsLeft} s");
                    int fp0 = TransportDirector.FootPenaltyPm; TransportDirector.LoadBalance(new CsvTable(TBD.Net.BalanceSource.Read("balance_unidades.csv"))); int fpCsv = TransportDirector.FootPenaltyPm;
                    TransportDirector.LoadBalance(new CsvTable("clave;valor;escala;nota\ninfanteria_campo_traves_permil;500;1000;x")); int fp5 = TransportDirector.FootPenaltyPm; TransportDirector.LoadBalance(new CsvTable("clave;valor;escala;nota\notra;1;1;x")); int fpDef = TransportDirector.FootPenaltyPm; TransportDirector.FootPenaltyPm = fp0;
                    check(fpCsv == 700 && fp5 == 500 && fpDef == 700, $"la penalización a campo través sale de balance_unidades.csv (infanteria_campo_traves_permil = {fpCsv}); otro valor se respeta ({fp5}) y sin fila vale 700 ({fpDef})");
                    bool cx = d.Cancel(job2.Id); check(cx && job2.Phase == TransportPhase.Cancelado && pl2.SpeedMmS == speed0 && !pl2.Moving, "cancelar la marcha a pie: se para y recupera su velocidad");
                }
            }
            {
                // la derrota gana siempre
                var g = new NightGame(NightParams.Default(), 1); var sp = new List<SpawnPoint>();
                while (g.Tick < g.TotalTicks - 1) g.Step(true, true, sp);
                g.Step(true, false, sp);
                check(g.State == NightState.Derrota && g.Tick == g.TotalTicks && g.EndedByTrain && !g.EndedByStation, "en el mismo tick de las 06:00 cae la locomotora: DERROTA, no victoria");
                var g2 = new NightGame(NightParams.Default(), 1); while (g2.Tick < g2.TotalTicks - 1) g2.Step(true, true, sp); g2.Step(false, true, sp);
                check(g2.State == NightState.Derrota && g2.EndedByStation, "en el mismo tick de las 06:00 cae el edificio de viajeros: DERROTA");
                // QA-35: la pila trepa el muro oeste; de noche el tren espera en el centro del recinto (sólido de X 81 a 145)
                {
                    check(NightParams.Default().TrainRect.X0 == (int)((TBD.Sim.Level.TrainNightX - TBD.Sim.Level.TrainRear) * 1000f) && NightParams.Default().TrainRect.X1 == (int)((TBD.Sim.Level.TrainNightX + TBD.Sim.Level.TrainFront) * 1000f), "QA-35: el tren de NightGame es el sólido de la zona aparcado de noche (X 81 a 145 m, en el centro del recinto)");
                    Func<int, NightSim> pile = (x0) =>
                    {
                        var ng = new NightGame(NightParams.Default(), 1); if (x0 > 0) { var tp = ng.P; tp.TrainRect = new RectMm(x0, tp.TrainRect.Y0, tp.TrainRect.X1, tp.TrainRect.Y1); ng.P = tp; }
                        var ns = new NightSim(ng, new UnitsWorld(1), NightSimParams.Default(), 1200000);
                        for (int i = 0; i < 400; i++) ns.ImportZombie(68800, 60000 + (i % 10) * 900);
                        for (int i = 0; i < 600; i++) ns.Step();
                        return ns;
                    };
                    var a1 = pile(0); var a2 = pile(0); var old = pile(74000);
                    check(a1.Climbed > 0 && a1.WallHp > 1100000, $"QA-35: con 400 pegados al muro oeste la pila trepa sin romper el muro ({a1.Climbed} saltan en 20 s, muro {a1.WallHp * 100 / 1200000} %)");
                    // (D-N148: sin nadie dentro, los que trepan van al tren; lo prueba el bloque de la D-N148)
                    check(old.Night.TrainHp < old.Night.P.TrainHp, $"QA-35: con la cola a 74 m (como en la qa25) los que trepan caen sobre el tren ({(old.Night.P.TrainHp - old.Night.TrainHp)} de daño en 20 s)");
                    check(a1.Night.Hash() == a2.Night.Hash() && a1.Climbed == a2.Climbed, "QA-35: trepar es determinista");
                    var off = NightSimParams.Default(); off.ClimbPermilS = 0; var ng0 = new NightGame(NightParams.Default(), 1); var n0 = new NightSim(ng0, new UnitsWorld(1), off, 1200000);
                    for (int i = 0; i < 400; i++) n0.ImportZombie(68800, 60000 + (i % 10) * 900); for (int i = 0; i < 600; i++) n0.Step();
                    check(n0.Climbed == 0, "QA-35: con ClimbPermilS = 0 no trepa nadie (el modelo anterior)");
                }
                // D-N148: el que pasa el muro ataca lo vivo más cercano (infantería, vehículos parados, obreros) y solo va al tren si no hay nadie en 30 m
                {
                    var tr = NightParams.Default().TrainRect; int tx, ty;
                    var men = new List<Target> { new Target { X = 100000, Y = 70000 }, new Target { X = 80000, Y = 60000 } };
                    var veh = new List<Target> { new Target { X = 76000, Y = 60000 } }; var wk = new List<Target> { new Target { X = 75000, Y = 60000 } };
                    byte c1 = ClimberTargets.Pick(72000, 60000, men, veh, wk, 30000, tr, out tx, out ty);
                    check(c1 == 1 && tx == 80000 && ty == 60000, "D-N148: con infantería a menos de 30 m va al soldado más cercano, aunque haya un vehículo o un obrero más cerca");
                    byte c2 = ClimberTargets.Pick(72000, 60000, new List<Target>(), veh, wk, 30000, tr, out tx, out ty);
                    byte c3 = ClimberTargets.Pick(72000, 60000, new List<Target>(), new List<Target>(), wk, 30000, tr, out tx, out ty);
                    check(c2 == 2 && c3 == 3, "D-N148: sin infantería, el vehículo parado; sin vehículo, el obrero");
                    byte c4 = ClimberTargets.Pick(72000, 50000, new List<Target> { new Target { X = 140000, Y = 80000 } }, null, null, 30000, tr, out tx, out ty);
                    check(c4 == 4 && tx == tr.X0 && ty == tr.Y0, "D-N148: si no hay nadie en 30 m va al punto más cercano del tren");
                    Func<bool, NightSim> climb = withMen =>
                    {
                        var ng = new NightGame(NightParams.Default(), 1); var uw = new UnitsWorld(1);
                        if (withMen) { var pl = uw.AddPlatoon(PlatoonKind.Fusileros, 76000, 60000, 0); pl.HoldFire = true; }
                        var ns = new NightSim(ng, uw, NightSimParams.Default(), 1200000) { AutoPlatoonResupply = false, ClimbMaxPerS = 0 }; ns.SP.ClimbPermilS = 20;     // la regla de D-N148 se mide con la trepada antigua (20 ‰/s, sin tope)
                        for (int i = 0; i < 400; i++) ns.ImportZombie(68800, 58000 + (i % 10) * 400);
                        for (int i = 0; i < 600; i++) ns.Step();
                        return ns;
                    };
                    var cm = climb(true); var cn = climb(false);
                    check(cm.Climbed > 0 && cm.SoldierDeaths > 0 && cm.Night.TrainHp == cm.Night.P.TrainHp, $"D-N148: con un pelotón junto al muro, los que trepan van a por él ({cm.SoldierDeaths} bajas) y el tren queda intacto");
                    check(cn.Climbed > 0 && cn.Night.TrainHp < cn.Night.P.TrainHp, $"D-N148: sin nadie dentro, los que trepan van al tren ({cn.Night.P.TrainHp - cn.Night.TrainHp} de daño)");
                }
                // NH90 (Sara: «no despega ni dispara», 920 de 920): como en la Noche50, sale solo con más de 30 zombis y dispara en vuelo
                {
                    var hs = NightSim.FromJson(TBD.Net.BalanceSource.Read("noche_sagunto.json"), null, 4); hs.AddDefaultForce(); hs.AddDefaultVehicles(true);
                    var heli = hs.Vehicles.Find(v => v.Kind == NightVehKind.Nh90); bool up = false; for (int t = 0; t < 4000 && hs.Night.State == NightState.Jugando; t++) { hs.Step(); up |= heli.Airborne; }
                    check(up && heli.Kills > 0 && heli.ShotsFired > 0, $"NH90: despega solo con la horda y dispara en vuelo ({heli.ShotsFired} disparos, {heli.Kills} abatidos)");
                }
                // D-N167 (Lucía): por debajo del 30 % el NH90 vuelve solo al helipuerto, rearma y se queda posado hasta una orden del jugador
                {
                    var hs = NightSim.FromJson(TBD.Net.BalanceSource.Read("noche_sagunto.json"), null, 4); hs.AddDefaultForce(); hs.AddDefaultVehicles(true);
                    int hi = hs.Vehicles.FindIndex(v => v.Kind == NightVehKind.Nh90); var heli = hs.Vehicles[hi]; long stock0 = 0;
                    for (int t = 0; t < 6000 && hs.Night.State == NightState.Jugando && !heli.Airborne; t++) hs.Step();
                    check(heli.Airborne && hs.ResupplyVehicle(hi) == 0, "D-N167: en vuelo el NH90 no rearma (solo posado en su helipuerto)");
                    heli.Shots = heli.MaxShots * 29 / 100; stock0 = hs.Units.StockShots; hs.Step();
                    check(!heli.Airborne && heli.Parked && heli.AutoReturns == 1 && heli.X == heli.HomeX && heli.Y == heli.HomeY && heli.ResupplyUntil > 0 && hs.Units.StockShots < stock0, $"D-N167: al bajar del 30 % vuelve solo a su helipuerto y rearma del almacén ({stock0 - hs.Units.StockShots} disparos)");
                    for (int t = 0; t < 1200 && hs.Night.State == NightState.Jugando; t++) hs.Step();
                    check(!heli.Airborne && heli.Parked && heli.Shots == heli.MaxShots, "D-N167: rearmado, se queda posado aunque siga la horda");
                    check(hs.TakeOffOrder(hi) && !heli.Parked, "D-N167: una orden del jugador anula el posado");
                    hs.Step(); check(heli.Airborne || hs.AliveZombies() <= 30, "D-N167: tras la orden vuelve a despegar solo con horda");
                }
                // D-N139: la infantería no atraviesa edificios, muros, vehículos ni el tren: los rodea
                {
                    var wo = new UnitsWorld(6); var po = wo.AddPlatoon(PlatoonKind.Tiradores, 0, 0, 0); wo.Obstacles.Add(new RectMm(10000, -3000, 14000, 3000));
                    wo.Move(po.Id, 30000, 0); bool inside = false;
                    for (int t = 0; t < 2400; t++) { wo.Step(new List<Target>(), new StepResult()); foreach (var m in po.Men) if (m.X > 10000 - 200 && m.X < 14000 + 200 && m.Y > -3000 - 200 && m.Y < 3000 + 200) inside = true; }
                    check(!inside && po.AX > 25000, $"D-N139: el pelotón rodea un edificio de 4 × 6 m que tiene delante sin que nadie lo atraviese (ancla en X {po.AX / 1000} m)");
                    var w0 = new UnitsWorld(6); var p0 = w0.AddPlatoon(PlatoonKind.Tiradores, 0, 0, 0); var w1 = new UnitsWorld(6); var p1x = w1.AddPlatoon(PlatoonKind.Tiradores, 0, 0, 0);
                    w0.Move(p0.Id, 30000, 0); w1.Move(p1x.Id, 30000, 0); for (int t = 0; t < 300; t++) { w0.Step(new List<Target>(), new StepResult()); w1.Step(new List<Target>(), new StepResult()); }
                    check(w0.Hash() == w1.Hash(), "D-N139: sin obstáculos todo sigue igual (mismo hash)");
                }
                // D-N137 (Lucía): C90 de los fusileros, 2 por pelotón, solo por orden del jugador, de un solo uso y se repone del almacén
                {
                    var wc = new UnitsWorld(9); var pf = wc.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0); var pg = wc.AddPlatoon(PlatoonKind.Granaderos, 0, 20000, 0);
                    check(pf.C90Left == 2 && pf.C90Cap == 2 && pg.C90Cap == 0, $"D-N137: los fusileros salen con 2 C90 y los demás con ninguno ({pf.C90Left}/{pf.C90Cap}, granaderos {pg.C90Cap})");
                    var zs = new List<Target>(); int id = 1; for (int i = 0; i < 10; i++) for (int j = 0; j < 10; j++) zs.Add(new Target { Id = id++, X = 50000 + i * 800 - 3600, Y = j * 800 - 3600 });
                    var rs = new StepResult(); int c90Shots = 0;
                    for (int t = 0; t < 900; t++) { wc.Step(zs, rs); foreach (var e in rs.Shots) if (e.Weapon == WeaponId.C90) c90Shots++; }
                    check(c90Shots == 0 && pf.C90Left == 2, $"D-N137: sin orden nadie dispara el C90, ni con 100 zombis a tiro ({c90Shots} disparos)");
                    { var wb = new UnitsWorld(3); var pb = wb.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0, Formation.Bloque); int far = 0; foreach (var m in pb.Men) far = Math.Max(far, IMath.Dist(m.X - pb.AX, m.Y - pb.AY)); bool a1 = !wc.OrderC90(pf.Id, 200000, 0), a2 = far < 10000 && !wb.OrderC90(pb.Id, pb.AX, pb.AY), a3 = !wc.OrderC90(pg.Id, 50000, 0);
                    check(a1 && a2 && a3, $"D-N137: fuera de alcance (120 m), demasiado cerca (10 m) o sin C90, la orden se rechaza ({a1} {a2} {a3}, soldado más lejano a {far} mm)"); }
                    wc.SetHoldFire(pf.Id, true);
                    check(wc.OrderC90(pf.Id, 50000, 0), "D-N137: la orden C90 a 50 m se acepta (también con alto el fuego)");
                    var sw = new SaveWriter(); wc.SaveState(sw); var wl = new UnitsWorld(0); wl.LoadState(new SaveReader(sw.ToArray()));
                    check(wl.Hash() == wc.Hash(), "D-N137: la orden pendiente y los C90 se guardan y cargan (mismo hash)");
                    int fired = 0, blasts = 0, kills = 0, blastR = 0;
                    for (int t = 0; t < 30; t++) { wc.Step(zs, rs); foreach (var e in rs.Shots) if (e.Weapon == WeaponId.C90) fired++; foreach (var b in rs.Blasts) if (b.Weapon == WeaponId.C90) { blasts++; blastR = b.RadiusMm; } foreach (var h in rs.Hits) if (h.Weapon == WeaponId.C90) kills++; }
                    check(fired == 1 && blasts == 1 && blastR == 5000 && kills >= 10 && kills <= 40 && pf.C90Left == 1, $"D-N137: un solo disparo, explota con 5 m de radio y abate entre 10 y 40 (abatidos {kills}, quedan {pf.C90Left})");
                    check(wc.OrderC90(pf.Id, 50000, 0), "D-N137: el segundo C90 también se acepta");
                    for (int t = 0; t < 30; t++) wc.Step(zs, rs);
                    check(pf.C90Left == 0 && !wc.OrderC90(pf.Id, 50000, 0), "D-N137: gastados los dos, la orden se rechaza");
                    pf.AmmoShots = pf.AmmoCap; wc.StockShots = 3000; wc.Resupply(pf.Id);
                    check(pf.C90Left == 1 && wc.StockShots == 1000, $"D-N137: en la recarga se repone un C90 por cada 1 u del almacén (quedan {pf.C90Left}, almacén {wc.StockShots})");
                    wc.StockShots = 10000; wc.Resupply(pf.Id);
                    check(pf.C90Left == 2 && wc.StockShots == 8000, "D-N137: con almacén de sobra se repone hasta 2 y no más");
                }
                // Caminos de los vehículos (Eugenio, 7-nov: «se quedan pillados en los muros»): A* en rejilla sobre los obstáculos inflados, giro suave y recálculo si se atasca
                {
                    var walls = new List<RectMm>(); NavGrid.FortWalls(walls, 70000, 44000, 156000, 85000, 95000, 103000);
                    var station = new RectMm(98000, 75000, 114000, 83000); var train = new RectMm(81000, 62900, 145000, 66100);
                    var obs = new List<RectMm>(walls) { station, train };
                    var ng = new NavGrid(0, 0, 510000, 480000, 1000); ng.Build(obs, 1800);
                    // sigue el camino con un modelo simple: 300 mm por tick (9 m/s), giro máximo 6° por tick, y no entra en un obstáculo (si lo haría, se para)
                    Func<VehicleNav, int, int, IList<RectMm>, IList<RectMm>, (bool arrived, bool crossed, int x, int y, int ticks)> drive = (nav, x, y, solid, dyn) =>
                    {
                        int hd = 0; bool crossed = false; int t = 0;
                        for (; t < 3000; t++)
                        {
                            if (!nav.Steer(ng, x, y, out int ax, out int ay, dyn)) break;
                            int want = IMath.Atan2(ay - y, ax - x); hd = (hd + IMath.Clamp(IMath.Diff(want, hd), -1092, 1092)) & 0xFFFF;
                            int nx = x + (int)(((long)IMath.Cos(hd) * 300) >> 16), ny = y + (int)(((long)IMath.Sin(hd) * 300) >> 16);
                            bool hit = false; foreach (var r in solid) if (nx > r.X0 - 900 && nx < r.X1 + 900 && ny > r.Y0 - 900 && ny < r.Y1 + 900) hit = true;
                            if (hit) { crossed = true; continue; } x = nx; y = ny;
                        }
                        return (!nav.HasGoal, crossed, x, y, t);
                    };
                    var p1 = new List<PointMm>(); bool ok1 = ng.FindPath(120000, 55000, 99000, 25000, p1);
                    var n1 = new VehicleNav(); n1.SetGoal(ng, 120000, 55000, 99000, 25000); var r1 = drive(n1, 120000, 55000, obs, null);
                    check(ok1 && r1.arrived && !r1.crossed && Math.Abs(r1.y - 25000) < 2000, $"caminos: del patio sale por la puerta E4 al sur sin rozar muro, estación ni tren ({p1.Count} puntos, llega en {r1.ticks} ticks a ({r1.x / 1000};{r1.y / 1000}) m)");
                    var n2 = new VehicleNav(); bool ok2 = n2.SetGoal(ng, 120000, 55000, 175000, 64000); var r2 = drive(n2, 120000, 55000, obs, null);
                    check(ok2 && r2.arrived && !r2.crossed, $"caminos: para ir al este del recinto (muro macizo) da la vuelta por E4 ({r2.ticks} ticks)");
                    var p2 = new List<PointMm>(); var p2b = new List<PointMm>(); ng.FindPath(120000, 55000, 175000, 64000, p2); ng.FindPath(30000, 20000, 400000, 300000, p2b);
                    var ng2 = new NavGrid(0, 0, 510000, 480000, 1000); ng2.Build(obs, 1800); var p3 = new List<PointMm>(); ng2.FindPath(120000, 55000, 175000, 64000, p3);
                    bool same = p2.Count == p3.Count && p2.Count > 1; for (int i = 0; same && i < p2.Count; i++) same = p2[i].X == p3[i].X && p2[i].Y == p3[i].Y;
                    check(same, $"caminos: la misma búsqueda en otra rejilla (y después de otras búsquedas) da el mismo camino: determinista ({p2.Count} puntos)");
                    var free = new NavGrid(0, 0, 510000, 480000, 1000); free.Build(new List<RectMm>(), 1800); var pf2 = new List<PointMm>();
                    check(free.FindPath(20000, 20000, 300000, 200000, pf2) && pf2.Count == 1 && pf2[0].X == 300000 && pf2[0].Y == 200000, $"caminos: sin obstáculos va en línea recta al destino ({pf2.Count} punto)");
                    var boxed = new NavGrid(0, 0, 100000, 100000, 1000); boxed.Build(new List<RectMm> { new RectMm(40000, 40000, 60000, 41000), new RectMm(40000, 59000, 60000, 60000), new RectMm(40000, 40000, 41000, 60000), new RectMm(59000, 40000, 60000, 60000) }, 0);
                    check(!boxed.FindPath(10000, 10000, 50000, 50000, new List<PointMm>()), "caminos: a un sitio encerrado no hay camino (y no se cuelga)");
                    // un vehículo aparcado aparece en medio del camino: se atasca, a los 2 s recalcula con él y lo rodea
                    var n3 = new VehicleNav(); n3.SetGoal(ng, 30000, 20000, 200000, 20000);
                    var parked = new List<RectMm> { new RectMm(60000, 16000, 68000, 24000) }; var solid3 = new List<RectMm>(obs); solid3.AddRange(parked);
                    var r3 = drive(n3, 30000, 20000, solid3, parked);
                    check(n3.Replans >= 1 && r3.arrived && Math.Abs(r3.x - 200000) < 2000, $"caminos: atascado 2 s contra un vehículo aparcado, recalcula ({n3.Replans} veces) y llega ({r3.ticks} ticks)");
                    var big = new NavGrid(0, 0, 510000, 480000, 1000); var sw0 = Stopwatch.StartNew(); big.Build(obs, 1800); var pp = new List<PointMm>(); for (int i = 0; i < 20; i++) big.FindPath(5000 + i * 1000, 5000, 500000, 470000 - i * 1000, pp); sw0.Stop();
                    check(sw0.ElapsedMilliseconds < 4000, $"caminos: 20 búsquedas de punta a punta del mapa de 510 × 480 m en {sw0.ElapsedMilliseconds} ms");
                }
                // D-N163 (Lucía, construccion-reparar.md §8): el muro por tramos en NightSim, Repair y Rebuild con las condiciones de §4 y §5
                {
                    string jn = TBD.Net.BalanceSource.Read("noche_sagunto.json");
                    var ts = NightSim.FromJson(jn, null, 3); ts.AddDefaultForce(); ts.EnableSegments();
                    long sumMax = 0, sumHp = 0; foreach (var sgm in ts.Segments) { sumMax += sgm.HpMax; sumHp += sgm.Hp; }
                    check(ts.Segments.Count == 28 && sumMax == ts.SP.WallHp && sumHp == ts.WallHp, $"D-N163: el muro se parte en {ts.Segments.Count} tramos de ~10 m y la vida suma la de siempre ({sumMax})");
                    int east = -1, west = -1; for (int i = 0; i < ts.Segments.Count; i++) { var sgm = ts.Segments[i]; if (sgm.Side == 1 && east < 0 && sgm.A0 <= 64500 && sgm.A1 > 64500) east = i; if (sgm.Side == 0 && sgm.A0 <= 64500 && sgm.A1 > 64500) west = i; }
                    var eg = ts.Segments[east]; eg.Hp = eg.HpMax / 2; ts.Segments[east] = eg;
                    int mat0 = ts.MaterialsU; ts.MaterialsU = 0; string r0 = ts.RepairBlockReason(east); ts.MaterialsU = mat0;
                    int zi = ts.ImportZombie(158000, 64500); string r1 = ts.RepairBlockReason(east);
                    var tsx = NightSim.FromJson(jn, null, 3); tsx.EnableSegments(); var eg2 = tsx.Segments[east]; eg2.Hp = 1000; string r2 = tsx.RepairBlockReason(east);
                    var eg3 = tsx.Segments[west]; eg3.Hp = 0; string r3 = tsx.RepairBlockReason(west);
                    check(r0 == "Sin materiales" && r1 == "Hay zombis pegados al tramo" && r2 == "Sin zapadores" && r3 == "Tramo caído: hay que reconstruirlo" && !ts.Repair(east), $"D-N163 §4: sin materiales, con zombis a 3 m, sin zapadores o caído no se repara, con su motivo («{r0}», «{r1}», «{r2}», «{r3}»)");
                    var ts2 = NightSim.FromJson(jn, null, 3); ts2.AddDefaultForce(); ts2.EnableSegments(); var e2 = ts2.Segments[east]; e2.Hp = e2.HpMax / 2; int half = e2.HpMax - e2.Hp; ts2.SumForTests();
                    int m0 = ts2.MaterialsU; bool ok = ts2.Repair(east); int t2 = 0; bool heldFire = false;
                    for (; t2 < 3000 && ts2.WallJobs.Count > 0; t2++) { ts2.Step(); foreach (var p in ts2.Units.Platoons) if (p.Kind == PlatoonKind.Zapadores && ts2.WallJobs.Count > 0 && ts2.WallJobs[0].Working && p.HoldFire) heldFire = true; }
                    int spentU = m0 - ts2.MaterialsU;
                    check(ok && e2.Hp == e2.HpMax && ts2.Repaired == 1 && heldFire && spentU >= half / 5000 && spentU <= half / 5000 + 1, $"D-N163 §3: los zapadores van, reparan sin disparar y dejan el tramo al máximo: {half} de vida, {spentU} u de materiales, {t2 / 30} s");
                    bool over = false; foreach (var p in ts2.Units.Platoons) if (p.Kind == PlatoonKind.Zapadores && p.HoldFire) over = true;
                    check(!over, "D-N163: al acabar la obra los zapadores vuelven a disparar");
                    // brecha: por un tramo a 0 entran (sin trepar), por el muro entero no
                    Func<bool, int> inside = breach => { var b = NightSim.FromJson(jn, null, 5); b.SP.ClimbPermilS = 0; b.AddDefaultForce(); b.EnableSegments(); if (breach) { var bg = b.Segments[west]; bg.Hp = 0; b.SumForTests(); } int max = 0;
                        for (int t = 0; t < 3600; t++) { b.Step(); if (t % 300 == 0) { var lz = new List<Target>(); b.ExportZombies(lz); int n = 0; foreach (var q in lz) if (q.X > b.SP.FortX0 && q.X < b.SP.FortX1 && q.Y > b.SP.FortY0 && q.Y < b.SP.FortY1) n++; max = Math.Max(max, n); } } return max; };
                    int inB = inside(true), inN = inside(false);
                    check(inB > 20 && inN == 0, $"D-N163 §1: por una brecha entran ({inB} dentro a la vez) y con el muro entero y sin trepar, nadie ({inN})");
                    // reconstruir: solo sin zombis a 15 m; vuelve al 30 % y cuesta 2 u cada 10 m
                    var ts3 = NightSim.FromJson(jn, null, 3); ts3.AddDefaultForce(); ts3.EnableSegments(); var e3 = ts3.Segments[east]; e3.Hp = 0; ts3.SumForTests();
                    int zq = ts3.ImportZombie(165000, 64500); string rb = ts3.RebuildBlockReason(east);
                    var ts4 = NightSim.FromJson(jn, null, 3); ts4.AddDefaultForce(); ts4.EnableSegments(); var e4 = ts4.Segments[east]; e4.Hp = 0; ts4.SumForTests();
                    int m4 = ts4.MaterialsU; bool rok = ts4.Rebuild(east) && !ts4.Repair(east); int t4 = 0; for (; t4 < 3000 && ts4.WallJobs.Count > 0; t4++) ts4.Step();
                    check(rb == "Hay zombis a menos de 15 m" && rok && e4.Hp == e4.HpMax * 300 / 1000 && m4 - ts4.MaterialsU == 2 && ts4.Rebuilt == 1, $"D-N163 §5: con zombis a 15 m no se reconstruye («{rb}»); sin ellos vuelve al 30 % ({e4.Hp} de {e4.HpMax}) por {m4 - ts4.MaterialsU} u en {t4 / 30} s");
                    // NightBot repara por debajo del 90 % y todo es determinista
                    Func<(ulong h, int rep, int wall, NightState st)> botNight = () => { var b = NightSim.FromJson(jn, null, 11); b.AddDefaultForce(); b.AddDefaultVehicles(true); b.Bot = NightBot.FromParams(b.SP); b.EnableSegments(); b.AutoRepair = true; var dmg = b.Segments[east]; dmg.Hp = dmg.HpMax / 2; b.SumForTests(); while (b.Night.State == NightState.Jugando && b.Night.Tick < b.TickCap) b.Run(300); return (b.Hash(), b.Repaired + b.Rebuilt, (int)((long)b.WallHp * 100 / b.SP.WallHp), b.Night.State); };
                    var n1 = botNight(); var n2 = botNight();
                    check(n1.h == n2.h && n1.rep > 0, $"D-N163 §8: el NightBot repara solo un tramo del este al 50 % (obras {n1.rep}, muro {n1.wall} %, {n1.st}) y la noche con tramos es determinista");
                }
                // D-N133: causa y recuentos
                check(g.Cause == DefeatCause.Tren && g.DefeatHeadline() == "El tren ha caído." && g.TrainsAlive == 0 && g.StationsAlive == 1, "D-N133: sin tren la causa es el tren y el titular «El tren ha caído.»");
                check(g2.Cause == DefeatCause.Estacion && g2.DefeatHeadline() == "Sagunto ha caído." && g2.StationsAlive == 0, "D-N133: sin estación la causa es la estación y el titular «Sagunto ha caído.»");
                { var gc = new NightGame(NightParams.Default(), 1); gc.Step(2, 1, sp); check(gc.State == NightState.Jugando && gc.Cause == DefeatCause.Ninguna && gc.DefeatHeadline() == "" && gc.StationsAlive == 2, "D-N133: con estaciones y trenes vivos se sigue jugando, sin causa");
                  gc.Step(1, 0, sp); check(gc.State == NightState.Derrota && gc.Cause == DefeatCause.Tren && gc.TrainsAlive == 0, "D-N133: con 0 trenes vivos (y 1 estación) se pierde por el tren");
                  var gx = new NightGame(NightParams.Default(), 1); gx.Step(0, 0, sp); check(gx.Cause == DefeatCause.Ambas && gx.DefeatHeadline("Castellón") == "Castellón y el tren han caído.", "D-N133: si caen las dos a la vez, las dos causas");
                  var ge = new NightGame(NightParams.Default(), 1); var gf = new NightGame(NightParams.Default(), 1); for (int i = 0; i < 300; i++) { ge.Step(true, true, sp); gf.Step(3, 2, sp); } check(ge.Hash() == gf.Hash(), "D-N133: los recuentos no cambian el hash"); }
                var g3 = new NightGame(NightParams.Default(), 1); while (g3.Tick < 5000) g3.Step(true, true, sp); g3.Step(false, false, sp);
                int t0 = g3.Tick; g3.Step(true, true, sp);
                check(g3.State == NightState.Derrota && g3.DefeatTick == 5001 && g3.Tick == t0 && sp.Count == 0, "la derrota es definitiva: el reloj se para y no entra nadie más");
                var g4 = new NightGame(NightParams.Default(), 1); g4.ZombiesAlive = 0; while (g4.State == NightState.Jugando) g4.Step(true, true, sp); g4.Step(false, false, sp);
                check(g4.State == NightState.Victoria, "una vez ganada la noche, un fallo posterior no la cambia");
                // D-N91: mundo continuo
                check(g4.Report.Night == 1 && g4.Report.CloseTick < g4.TotalTicks && g4.Report.Spawned == 7310 && g4.Report.StationAlive && g4.Report.TrainAlive && g4.NightsSurvived == 1, "al acabar con la horda sale el parte de la noche (noche 1, 7.310 zombis, edificio y tren en pie) y la noche queda cerrada");
                int tc = g4.Tick; g4.Step(true, true, sp);
                check(g4.State == NightState.Victoria && g4.Tick == tc + 1 && sp.Count == 0, "mundo continuo: tras ganar, el reloj sigue corriendo, no entra nadie y la partida NO termina");
                check(g4.BeginNextNight(NightParams.Default()) && g4.State == NightState.Jugando && g4.Tick == 0 && g4.NightIndex == 2 && g4.Hour == 22, "BeginNextNight abre la noche 2: reloj a las 22:00, oleadas a cero");
                var gd = new NightGame(NightParams.Default(), 1); gd.Step(false, true, sp);
                check(!gd.BeginNextNight(NightParams.Default()) && gd.State == NightState.Derrota, "la derrota sí acaba la partida: no se puede abrir otra noche");
                long cnt = 0; while (g4.State == NightState.Jugando) { g4.Step(true, true, sp); cnt += sp.Count; }
                check(cnt == 7310 && g4.NightsSurvived == 2, "la segunda noche también reparte la horda completa (7.310) y se cierra");
            }
            {
                // determinismo y guardado de todo junto: pelotones + noche
                Func<uint, List<ulong>> run = seed =>
                {
                    var w = new UnitsWorld(seed); var f = w.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0); var a = w.AddPlatoon(PlatoonKind.Ametralladores, 0, 12000, 0, Formation.Linea); var gr = w.AddPlatoon(PlatoonKind.Granaderos, 0, -12000, 0);
                    var n = new NightGame(NightParams.Default(), seed + 1); var sp = new List<SpawnPoint>();
                    var z = new List<Zed>(); int id = 1000; var hs = new List<ulong>(); var res = new StepResult();
                    for (int t = 0; t < 4000; t++)
                    {
                        if (t == 1300) w.DeployAt(a.Id, 0);
                        if (t == 1500) w.AttackMove(f.Id, 30000, 5000, 0);
                        if (t == 2500) { w.SetFormation(gr.Id, Formation.Circulo); w.Resupply(gr.Id); }
                        w.StockShots = 20000;
                        n.Step(true, true, sp);
                        foreach (var s in sp) z.Add(new Zed { Id = id++, X = 60000 + s.X / 4, Y = s.Y - 40000 });
                        foreach (var q in z) if (q.Hp > 0) { int dx = 10000 - q.X, dy = -q.Y, d = Math.Max(1, IMath.Dist(dx, dy)); if (d > 1500) { q.X += dx * 3000 / 30 / d; q.Y += dy * 3000 / 30 / d; } }
                        w.Step(ToTargets(z), res); Apply(res, z, w);
                        if (t % 250 == 0) hs.Add(w.Hash() ^ n.Hash());
                        if (t == 1900) { var sw = new SaveWriter(); w.SaveState(sw); n.SaveState(sw); var sr = new SaveReader(sw.ToArray()); var w2 = new UnitsWorld(0); var n2 = new NightGame(NightParams.Default(), 0); w2.LoadState(sr); n2.LoadState(sr); hs.Add(w2.Hash() == w.Hash() && n2.Hash() == n.Hash() ? 1UL : 0UL); }
                    }
                    hs.Add(w.Hash()); return hs;
                };
                var h1 = run(42); var h2 = run(42); var h3 = run(43);
                bool same = h1.Count == h2.Count; for (int i = 0; same && i < h1.Count; i++) same = h1[i] == h2[i];
                check(same, $"dos partidas iguales (pelotones con órdenes y combate + la noche) dan el mismo hash en {h1.Count} puntos (4.000 ticks)");
                check(h1[h1.Count - 1] != h3[h3.Count - 1], "otra semilla da otro estado final");
                check(h1[8] == 1UL, "guardar y cargar pelotones y noche en pleno combate (con granadas en el aire) da el mismo hash");
                var wa = new UnitsWorld(5); wa.AddPlatoon(PlatoonKind.Granaderos, 0, 0, 0); wa.AddPlatoon(PlatoonKind.Fusileros, 0, 8000, 0); wa.Move(2, 40000, 0);
                var ra = new StepResult(); var tgs = new List<Target>(); for (int i = 0; i < 20; i++) tgs.Add(new Target { Id = i + 1, X = 50000 + (i % 5) * 500, Y = (i / 5) * 500 });
                for (int t = 0; t < 300; t++) wa.Step(tgs, ra);
                var sw2 = new SaveWriter(); wa.SaveState(sw2); var wb = new UnitsWorld(0); wb.LoadState(new SaveReader(sw2.ToArray()));
                bool lock2 = true; var rb = new StepResult();
                for (int t = 0; t < 600 && lock2; t++) { wa.Step(tgs, ra); wb.Step(tgs, rb); lock2 = wa.Hash() == wb.Hash() && ra.Shots.Count == rb.Shots.Count && ra.Hits.Count == rb.Hits.Count; }
                check(lock2, "tras cargar, los pelotones siguen igual que el original (600 ticks, mismos disparos e impactos)");
                bool trunc = true; var full = sw2.ToArray();
                for (int cut = 0; cut < full.Length && trunc; cut += 7) { try { new UnitsWorld(0).LoadState(new SaveReader(full, 0, cut)); trunc = false; } catch (SaveException) { } }
                check(trunc, "un guardado de pelotones recortado da siempre error, nunca un mundo a medias en silencio");
            }

            // ------------------------------------------------------------------ noche sin ventana (API para el jugador automático de Sara)
            {
                string json = TBD.Net.BalanceSource.Read("noche_sagunto.json");
                var sim = NightSim.FromJson(json, null, 7);
                check(sim.Night.P.Waves.Length == 1 && sim.Night.WaveTotal(0) == 7310 && sim.Units.StockShots == 47 * 2000 && sim.MaterialsU == 40 && sim.FuelU == 30, "crear la noche desde noche_sagunto.json (normal): una horda de 7.310 zombis (horda final de Hugo), 47 u de munición (94.000 disparos; D-N128), 40 de materiales y 30 de combustible");
                var easy = NightSim.FromJson(json, "facil", 7); var hard = NightSim.FromJson(json, "dificil", 7);
                check(easy.Night.WaveTotal(0) == 6240 && hard.Night.WaveTotal(0) == 9590, "dificultad fácil (6.240) y difícil (9.590, escala 1115) desde el mismo JSON (horda final)");
                bool bad = false; try { Mini.Parse("{\"a\": [1, 2"); } catch (FormatException) { bad = true; }
                check(bad && (long)((System.Collections.Generic.Dictionary<string, object>)Mini.Parse("{\"a\": -12}"))["a"] == -12, "el lector de JSON mínimo rechaza un texto truncado y lee enteros con signo");

                sim.AddDefaultForce();
                int ammo0 = 0; foreach (var p in sim.Units.Platoons) ammo0 += p.AmmoShots; ammo0 += sim.Units.StockShots;
                var swc = Stopwatch.StartNew();
                int resup = 0;
                while (sim.Night.State == NightState.Jugando && sim.Night.Tick < sim.TickCap)
                {
                    sim.Run(150);
                    resup += sim.ResupplyBot();      // bot de Hugo: reabastece a un pelotón con < 30 % de su dotación
                }
                swc.Stop();
                var snap = sim.Snapshot(); int spent = 0; foreach (var p in sim.Units.Platoons) spent += p.AmmoSpent;
                check((snap.State == NightState.Victoria ? snap.ZombiesSpawned == 7310 : snap.ZombiesSpawned > 0 && snap.ZombiesSpawned <= 7310) && snap.Kills <= snap.ZombiesSpawned && snap.Kills + snap.ZombiesAlive <= snap.ZombiesSpawned, $"balance de zombis (la noche acabó en {snap.State}): entraron {snap.ZombiesSpawned} de 7.310, abatidos {snap.Kills}, vivos {snap.ZombiesAlive}");
                check(snap.PlatoonAmmoShots + snap.StockShots + spent == ammo0, $"la munición se conserva: inicial {ammo0} = restante {snap.PlatoonAmmoShots + snap.StockShots} + gastada {spent} disparos");
                int rs = 0, ks = 0, ds = 0; foreach (var w in sim.Waves) { rs += w.Resupplies; ks += w.Kills; ds += w.SoldierDeaths; }
                check(rs == snap.Resupplies && rs == resup && ks == snap.Kills && ds == snap.SoldierDeaths, $"las estadísticas por oleada suman los totales (recargas {rs}, bajas enemigas {ks}, bajas propias {ds})");
                check(sim.Waves.Count == 1 && sim.Waves[0].StartTick == 0 && sim.Waves[0].HourAtStart == 22 && sim.Waves[0].MinuteAtStart == 0, "D-N136: la horda empieza a entrar a las 22:00");
                var sb = new System.Text.StringBuilder();
                foreach (var w in sim.Waves) sb.Append($" [O{w.Wave} {w.HourAtStart:00}:{w.MinuteAtStart:00} entran {w.Spawned}, abatidos {w.Kills}, pila máx. {w.PeakPile}, bajas {w.SoldierDeaths}, recargas {w.Resupplies}, disparos {w.AmmoSpentShots}]");
                Console.WriteLine($"         noche con la fuerza por defecto y reabastecimiento mínimo: {snap.State} a las {snap.Hour:00}:{snap.Minute:00}, muro {snap.WallHp}, edificio {snap.StationHp}, bajas propias {snap.SoldierDeaths} de {snap.SoldierDeaths + snap.SoldiersAlive};{sb}");
                check(swc.Elapsed.TotalSeconds < 60, $"una noche entera (30.857 ticks, hasta 4.000 zombis) sin ventana en {swc.Elapsed.TotalSeconds:F1} s");
                // determinismo de la noche entera
                Func<uint, ulong> night = seed =>
                {
                    var n = NightSim.FromJson(json, null, seed); n.AddDefaultForce(); n.Run(9000); n.Units.Move(1, 85000, 60000); n.Run(6000); return n.Hash();
                };
                check(night(5) == night(5) && night(5) != night(6), "la noche sin ventana es determinista: misma semilla, mismo hash; otra semilla, otro");
            }
            {
                // atajo de pruebas de Sara: un tren sin edificio ni pelotones pierde pronto
                var sim = NightSim.FromJson(TBD.Net.BalanceSource.Read("noche_sagunto.json"), "dificil", 3);
                sim.Run(sim.Night.TotalTicks);
                check(sim.Night.State == NightState.Derrota, $"sin ningún pelotón, la noche difícil se pierde (a las {sim.Night.Hour:00}:{sim.Night.Minute:00})");
            }

            // ------------------------------------------------------------------ rendimiento
            {
                var w = new UnitsWorld(2); for (int i = 0; i < 4; i++) w.AddPlatoon(PlatoonKind.Fusileros, i * 8000, 0, 0); for (int i = 0; i < 2; i++) w.AddPlatoon(PlatoonKind.Granaderos, i * 8000, 10000, 0);
                foreach (var p in w.Platoons) p.AmmoShots = 1000000000;
                var z = new List<Target>(); var rg = new IRng(3); for (int i = 0; i < 10000; i++) z.Add(new Target { Id = i, X = 30000 + rg.Below(150000), Y = rg.Below(120000) - 60000 });
                var res = new StepResult(); var sw = Stopwatch.StartNew(); for (int t = 0; t < 300; t++) w.Step(z, res); sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds / 300;
                check(ms < 4.0, $"6 pelotones (40 soldados) contra 10.000 objetivos: {ms:F2} ms por tick (< 4 ms)");
            }

            // ------------------------------------------------------------------ vehículo (local)
            {
                var v = new Vehicle(1, VehicleKind.Lince, 0, 0, 0); var s = VehicleCatalog.Get(VehicleKind.Lince);
                v.Controls(1000, 0, 0);
                int t5 = 0, tmax = -1;
                for (int t = 0; t < 600; t++) { v.Step(); if (t == 4) t5 = v.SpeedMmS; if (tmax < 0 && v.SpeedMmS >= s.MaxFwdMmS * 95 / 100) tmax = t; }
                check(t5 < s.MaxFwdMmS * 40 / 100 && tmax > 60 && tmax < 240 && v.SpeedMmS == s.MaxFwdMmS, $"el Lince acelera con inercia: a los 5 ticks va a {t5 / 1000.0:F1} m/s y llega al 95 % a los {tmax / 30.0:F1} s (máx. {s.MaxFwdMmS / 1000} m/s)");
                v.Controls(0, 0, 1000); int stopT = 0; while (v.SpeedMmS > 0 && stopT < 600) { v.Step(); stopT++; }
                check(stopT < 90 && stopT > 5, $"frena desde la máxima en {stopT / 30.0:F1} s");
                // círculo a baja velocidad: radio ≈ L / tan(δ)
                var c = new Vehicle(2, VehicleKind.Lince, 0, 0, 0); c.Controls(250, 1000, 0);
                for (int t = 0; t < 90; t++) c.Step();
                int sp0 = c.SpeedMmS; var pts = new List<(int, int)>(); int h0 = c.Heading, turned = 0, last = c.Heading;
                for (int t = 0; t < 900 && turned < 65536 - 2000; t++) { c.Step(); pts.Add((c.X, c.Y)); turned += (c.Heading - last) & 0xFFFF; last = c.Heading; }
                int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue; foreach (var q in pts) { minX = Math.Min(minX, q.Item1); maxX = Math.Max(maxX, q.Item1); minY = Math.Min(minY, q.Item2); maxY = Math.Max(maxY, q.Item2); }
                int diam = (maxX - minX + maxY - minY) / 2; int lim = s.MaxSteer * (1000 - (int)(550L * sp0 / s.MaxFwdMmS)) / 1000;
                int expect = (int)(s.WheelbaseMm * (long)IMath.Cos(lim) / IMath.Sin(lim));
                check(turned >= 60000 && Math.Abs(diam / 2 - expect) < expect / 6, $"giro a tope con el volante: radio {diam / 2000.0:F1} m frente a L/tan(δ) = {expect / 1000.0:F1} m");
                var r = new Vehicle(3, VehicleKind.Lince, 0, 0, 0); r.Controls(-1000, 0, 0); for (int t = 0; t < 400; t++) r.Step();
                check(r.SpeedMmS == -s.MaxRevMmS && r.X < 0, "marcha atrás limitada a su velocidad máxima de reversa");
                var tr = new Vehicle(4, VehicleKind.Pizarro, 0, 0, 0); tr.Controls(0, 1000, 0); for (int t = 0; t < 60; t++) tr.Step();
                check(Math.Abs(tr.X) < 50 && Math.Abs(tr.Y) < 50 && tr.Heading != 0, "el Pizarro de orugas gira sobre su eje sin avanzar");
                var rm = new Vehicle(5, VehicleKind.Lince, 0, 0, 0); rm.Controls(1000, 0, 0); for (int t = 0; t < 30; t++) rm.Step(); int slow = rm.RamDamage(); for (int t = 0; t < 400; t++) rm.Step();
                check(rm.RamDamage() > slow && rm.RamDamage() >= 140 && Vehicle.InRam(rm, rm.X + 2000, rm.Y + 300) && !Vehicle.InRam(rm, rm.X - 2000, rm.Y) && !Vehicle.InRam(rm, rm.X + 2000, rm.Y + 3000), $"la embestida hace daño proporcional a la velocidad ({rm.RamDamage()}) y solo delante del morro");
            }

            // ------------------------------------------------------------------ helicóptero (local)
            {
                var s = HeliCatalog.Get(HeliKind.Cougar);
                var h = new Helicopter(1, HeliKind.Cougar, 0, 0, 0);
                h.Controls(1000, 0, 0, 0); for (int t = 0; t < 30; t++) h.Step();
                check(h.State == HeliState.Apagado && h.Z == 0, "con el motor apagado no despega aunque se tire del colectivo");
                h.StartEngine(); int spool = -1; for (int t = 0; t < 400; t++) { h.Step(); if (spool < 0 && h.State == HeliState.Volando) spool = t; if (spool >= 0 && t >= spool + 60) break; }
                check(spool > 100 && spool < 250 && h.State == HeliState.Volando, $"arranca el rotor y despega a los {spool / 30.0:F1} s, no antes (el rotor tarda ~4,5 s en llegar al 95 %)");
                // altitud estable con colectivo a 0
                h.Controls(0, 0, 0, 0); for (int t = 0; t < 90; t++) h.Step();
                int z0 = h.Z; for (int t = 0; t < 120; t++) h.Step();
                check(Math.Abs(h.Z - z0) < 100 && h.Z > 5000 && h.IsAirborne, $"estacionario: con el colectivo a 0 mantiene la altura ({h.Z / 1000.0:F1} m)");
                // inercia horizontal
                h.Controls(0, 1000, 0, 0); int v3 = 0; for (int t = 0; t < 3; t++) h.Step(); v3 = IMath.Dist(h.VX, h.VY);
                int tfull = -1; for (int t = 3; t < 600; t++) { h.Step(); if (tfull < 0 && IMath.Dist(h.VX, h.VY) >= s.MaxFwdMmS * 95 / 100) tfull = t; }
                check(v3 < s.MaxFwdMmS * 15 / 100 && tfull > 90 && tfull < 400, $"inercia: a los 3 ticks va a {v3 / 1000.0:F1} m/s y alcanza el 95 % de {s.MaxFwdMmS / 1000} m/s a los {tfull / 30.0:F1} s");
                check(h.Pitch > s.MaxTilt / 4 && Math.Abs(h.Roll) < 200, $"al avanzar baja el morro (cabeceo {h.Pitch * 360.0 / 65536:F1}°), sin alabeo");
                h.Controls(0, 0, 0, 0); for (int t = 0; t < 10; t++) h.Step(); int still = IMath.Dist(h.VX, h.VY);
                for (int t = 0; t < 700; t++) h.Step();
                check(still > s.MaxFwdMmS / 2 && IMath.Dist(h.VX, h.VY) == 0 && Math.Abs(h.Pitch) < 100, $"al soltar el cíclico sigue deslizando (a 0,3 s aún {still / 1000.0:F1} m/s) y acaba parado y nivelado");
                // lateral y guiñada
                h.Controls(0, 0, 1000, 0); for (int t = 0; t < 120; t++) h.Step();
                check(h.Roll > s.MaxTilt / 4, $"desplazamiento lateral a la derecha: alabeo {h.Roll * 360.0 / 65536:F1}°");
                h.Controls(0, 0, 0, 1000); int y0 = h.Yaw; for (int t = 0; t < 30; t++) h.Step();
                check(IMath.Diff(h.Yaw, y0) > 5000, $"el pedal gira el morro {IMath.Diff(h.Yaw, y0) * 360.0 / 65536:F0}° en 1 s");
                // aterrizaje blando
                h.Controls(0, 0, 0, 0); for (int t = 0; t < 200; t++) h.Step();
                h.Controls(-250, 0, 0, 0); int guard = 0; while (h.IsAirborne && guard++ < 3000) h.Step();
                check(h.State == HeliState.EnTierra && h.LastImpactMmS < s.HardLandMmS && h.Hp == s.Hp && h.Z == 0, $"aterrizaje blando a {h.LastImpactMmS / 1000.0:F1} m/s: sin daño");
                // aterrizaje duro
                var d = new Helicopter(2, HeliKind.Cougar, 0, 0, 0); d.StartEngine(); d.Controls(1000, 0, 0, 0);
                for (int t = 0; t < 400; t++) d.Step(); for (int t = 0; t < 120; t++) d.Step();
                d.Controls(-1000, 0, 0, 0); guard = 0; while (d.IsAirborne && guard++ < 3000) d.Step();
                check(d.LastImpactMmS > s.HardLandMmS && d.Hp < s.Hp, $"aterrizaje duro a {d.LastImpactMmS / 1000.0:F1} m/s: daño {s.Hp - d.Hp} de {s.Hp}");
                // motor en el aire
                var e = new Helicopter(3, HeliKind.Tigre, 0, 0, 0); e.StartEngine(); e.Controls(1000, 0, 0, 0); for (int t = 0; t < 500; t++) e.Step();
                e.StopEngine(); e.Controls(0, 1000, 0, 0); guard = 0; while (e.IsAirborne && guard++ < 6000) e.Step();
                check(!e.IsAirborne && e.Hp < HeliCatalog.Get(HeliKind.Tigre).Hp, "sin motor en el aire el rotor se para, se pierde el control y se cae con daño");
                // relieve: el suelo sube y el aparato lo respeta
                var g = new Helicopter(4, HeliKind.Cougar, 0, 0, 0); g.Ground = (x, y) => x > 50000 ? 8000 : 0; g.StartEngine(); g.Controls(1000, 0, 0, 0);
                for (int t = 0; t < 330; t++) g.Step(); g.Controls(0, 1000, 0, 0); bool below = false;
                for (int t = 0; t < 300; t++) { g.Step(); if (g.Z < g.GroundZ) below = true; }
                check(!below, "el helicóptero nunca queda por debajo del relieve");
            }
            {
                // noche FUERA DE PANTALLA (Eugenio, 7-nov): salir de la zona no para ni reinicia la noche; NightSim se engancha al mismo NightGame y UnitsWorld
                string json = TBD.Net.BalanceSource.Read("noche_sagunto.json");
                Func<bool, ulong> run = leave =>
                {
                    var zone = NightSim.FromJson(json, null, 5); zone.AddDefaultForce(); zone.Bot = NightBot.FromParams(zone.SP);
                    zone.Run(4000);                                                   // 4.000 ticks «en pantalla»
                    if (!leave) { zone.Run(6000); return zone.Hash(); }
                    var exp = new List<Target>(); zone.ExportZombies(exp);
                    var off = new NightSim(zone.Night, zone.Units, zone.SP, zone.WallHp, 5); foreach (var t in exp) off.ImportZombie(t.X, t.Y);
                    off.Run(6000); return off.Hash();
                };
                {
                    var zone = NightSim.FromJson(json, null, 5); zone.AddDefaultForce(); zone.Run(1000);   // D-N136: a los 33 s la horda aún está entrando
                    int sp00 = 0; for (int w = 0; w < zone.Night.Spawned.Length; w++) sp00 += zone.Night.Spawned[w];
                    int tick0 = zone.Night.Tick, sp0 = sp00, ammo0 = zone.Units.StockShots, alive0 = zone.ZombiesAlive, wall0 = zone.WallHp;
                    var exp = new List<Target>(); zone.ExportZombies(exp);
                    var off = new NightSim(zone.Night, zone.Units, zone.SP, wall0, 5); foreach (var t in exp) off.ImportZombie(t.X, t.Y);
                    check(off.Night == zone.Night && off.Units == zone.Units && off.ZombiesAlive == alive0 && off.WallHp == wall0 && off.Night.Tick == tick0 && off.Units.StockShots == ammo0,
                        $"al salir de la zona la noche sigue donde estaba: mismo reloj (tick {tick0}), {alive0} zombis vivos, muro {wall0}, almacén {ammo0 / 2000.0:0.0} u (no se reinicia)");
                    int k0 = off.Night.Tick; off.Run(9000);
                    int spawned = 0; for (int w = 0; w < off.Night.Spawned.Length; w++) spawned += off.Night.Spawned[w];
                    check(off.Night.Tick > k0 && spawned > sp0 && off.Kills > 0, $"fuera de pantalla el reloj avanza ({k0} → {off.Night.Tick}), siguen llegando oleadas ({sp0} → {spawned} zombis) y los pelotones disparan ({off.Kills} abatidos)");
                    var back = new List<Target>(); off.ExportZombies(back); bool inRange = true; foreach (var t in back) if (t.X < -400000 || t.X > 600000 || t.Y < -400000 || t.Y > 600000) inRange = false;
                    check(back.Count == off.ZombiesAlive && inRange, $"al volver a la zona, los {back.Count} zombis vivos salen con su posición para crearlos en ECS");
                    while (off.Night.State == NightState.Jugando && off.Night.Tick < off.TickCap) off.Run(300);
                    check(off.Night.State == NightState.Victoria || off.Night.State == NightState.Derrota, $"la noche puede acabar fuera de pantalla ({off.Night.State} a las {off.Night.Hour:00}:{off.Night.Minute:00})");
                }
                check(run(true) == run(true), "fuera de pantalla es determinista: dos salidas iguales dan el mismo hash");
                {
                    var a = NightSim.FromJson(json, null, 9); a.AddDefaultForce(); var b = NightSim.FromJson(json, null, 9); b.AddDefaultForce();
                    var off = new NightSim(b.Night, b.Units, b.SP, b.WallHp, 9) { AutoPlatoonResupply = false };
                    for (int t = 0; t < 3000; t++) { a.Step(); off.Step(); }
                    check(a.Hash() == off.Hash() && a.Kills == off.Kills, $"engancharse sin zombis al principio es la misma noche que NightSim normal (3.000 ticks, {a.Kills} abatidos)");
                }
            }
            {
                // fuego de los vehículos en NightSim (Mak, 7-nov: fuego libre desde el principio; misma regla que la zona)
                string json = TBD.Net.BalanceSource.Read("noche_sagunto.json");
                var d = NightVehicle.Make("Dragón", NightVehKind.Dragon, 0, 0);
                check(d.MaxShots == 400 && d.ResupplyMilliU == 2000 && d.PeriodTicks == 30 && d.KillsPerShot == 3 && d.RangeMm == 55000 && d.RefillCostShots() == 0, "Dragón: 1 salva por segundo, 3 abatidos por salva y 55 m como en la zona; 400 salvas y recarga completa de 2 u (D-N128)");
                d.Shots = 200; check(d.RefillCostShots() == 2000, $"recargar medio Dragón cuesta 1 u del almacén: se cobra solo lo que se rellena ({d.RefillCostShots()} disparos)");
                var s0 = NightSim.FromJson(json, null, 3); s0.AddDefaultForce(); ulong h0 = s0.Hash();
                var s1 = NightSim.FromJson(json, null, 3); s1.AddDefaultForce(); check(s1.Hash() == h0 && s1.Vehicles.Count == 0, "sin vehículos, el hash de NightSim es el de siempre");
                var sv = NightSim.FromJson(json, null, 3); sv.AddDefaultForce(); sv.AddDefaultVehicles(true); sv.Bot = NightBot.FromParams(sv.SP);
                var sn = NightSim.FromJson(json, null, 3); sn.AddDefaultForce(); sn.Bot = NightBot.FromParams(sn.SP);
                for (int t = 0; t < 9000; t++) { sv.Step(); sn.Step(); }
                var dr = sv.Vehicles[0]; var nh = sv.Vehicles[3];
                check(sv.Vehicles.Count == 4 && sv.VehicleKills > 0 && dr.Kills > 0 && dr.ShotsFired > 0 && nh.Kills > 0, $"con fuego libre los vehículos abaten ({sv.VehicleKills} en 9.000 ticks; Dragón {dr.Kills}, VAMTAC {sv.Vehicles[1].Kills}+{sv.Vehicles[2].Kills}, NH90 {nh.Kills}); el NH90 despega solo con la horda y dispara en vuelo");
                check(sv.LastPile <= sn.LastPile || sv.PeakPile <= sn.PeakPile, $"con vehículos la pila no crece (máx. {sv.PeakPile} frente a {sn.PeakPile})");
                var sx = NightSim.FromJson(json, null, 3); sx.AddDefaultForce(); sx.AddDefaultVehicles(true); sx.Bot = NightBot.FromParams(sx.SP);
                for (int t = 0; t < 9000; t++) sx.Step();
                check(sx.Hash() == sv.Hash(), "con vehículos sigue siendo determinista (mismo hash en dos corridas)");
                var sg = NightSim.FromJson(json, null, 3); sg.AddDefaultForce(); sg.AddDefaultVehicles(false); for (int t = 0; t < 9000; t++) sg.Step();
                check(sg.VehicleKills == 0, "con alto el fuego no disparan");
                // reserva vacía y recarga
                var sr = NightSim.FromJson(json, null, 3); sr.AddDefaultForce(); sr.AddDefaultVehicles(true); var v0 = sr.Vehicles[0]; v0.Shots = 0; int stock0 = sr.Units.StockShots;
                int cost = sr.ResupplyVehicle(0); check(cost == 2400 && sr.Units.StockShots == stock0 - 2400 && v0.ResupplyUntil == sr.Night.Tick + 600, $"recargar el Dragón vacío: 1,2 u del almacén (D-N135, ×0,6) y 20 s ({cost} disparos)");
                check(sr.ResupplyVehicle(0) == 0, "no se puede pedir otra recarga mientras dura la primera");
                v0.Fire = false; for (int t = 0; t < 601; t++) sr.Step(); check(v0.ResupplyUntil == -1 && v0.Shots == v0.MaxShots, "al acabar la recarga (20 s) la reserva vuelve a estar llena");
                var sj = NightSim.FromJson(json, null, 3); sj.VehicleParamsJson = "{\"NightVehicles\": {\"Dragon\": {\"MaxShots\": 240}, \"Nh90\": {\"Airborne\": true}}}"; sj.AddDefaultVehicles(true);
                check(sj.Vehicles[0].MaxShots == 240 && sj.Vehicles[0].Shots == 240 && sj.Vehicles[3].Airborne && sj.Vehicles[1].MaxShots == 750, "las cifras de Hugo (NightVehicles en nightsim_params.json) cambian solo lo que dicen");
                { var sz = NightSim.FromJson(json, null, 3); var vz = NightVehicle.Make("VAMTAC 1", NightVehKind.Vamtac, 88000, 53000); sz.Vehicles.Add(vz); sz.VehicleParamsJson = "{\"NightVehicles\": {\"Vamtac\": {\"ResupplyMilliU\": 1800}}}"; sz.ApplyVehicleParams(); vz.Shots = 375;
                  check(vz.ResupplyMilliU == 1800 && vz.MaxShots == 750 && vz.RefillCostShots() == 1800, $"la zona puede aplicar el JSON a sus vehículos (ApplyVehicleParams) y luego poner la reserva real: medio VAMTAC con recarga ×0,6 cuesta 0,9 u ({vz.RefillCostShots()} disparos)"); }
            }
        }
    }
}
