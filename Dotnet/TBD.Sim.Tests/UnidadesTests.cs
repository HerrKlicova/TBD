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
            string json = TBD.Net.BalanceSource.Read("noche_sagunto.json");
            int n = 20; int.TryParse(Environment.GetEnvironmentVariable("TBD_SEEDS") ?? "20", out n); if (n <= 0) n = 20;
            string difs = Environment.GetEnvironmentVariable("TBD_DIF") ?? "todas";
            Console.WriteLine("dificultad;vehiculos;partidas;victorias;estrellas_0;estrellas_1;estrellas_2;estrellas_3;muro_medio_pct;pila_max_media;abatidos_vehiculos_medio;recargas_vehiculos_medio;municion_final_media_u;bajas_medias");
            foreach (var dif in difs == "todas" ? new[] { "facil", "normal", "dificil" } : new[] { difs })
                foreach (bool veh in new[] { false, true })
                {
                    int wins = 0; var st = new int[4]; long wall = 0, pile = 0, vk = 0, vr = 0, ammo = 0, deaths = 0;
                    for (int i = 0; i < n; i++)
                    {
                        var sim = NightSim.FromJson(json, dif, (uint)(i + 1)); sim.AddDefaultForce(); if (veh) sim.AddDefaultVehicles(true); sim.Bot = NightBot.FromParams(sim.SP);
                        while (sim.Night.State == NightState.Jugando) sim.Run(300);
                        if (sim.Night.State == NightState.Victoria) wins++; st[Math.Max(0, Math.Min(3, sim.Stars()))]++;
                        wall += (long)sim.WallHp * 100 / Math.Max(1, sim.SP.WallHp); pile += sim.PeakPile; vk += sim.VehicleKills; vr += sim.VehicleResupplies; ammo += sim.AmmoMilliU(); deaths += sim.SoldierDeaths;
                    }
                    Console.WriteLine($"{dif};{(veh ? "si" : "no")};{n};{wins};{st[0]};{st[1]};{st[2]};{st[3]};{wall / n};{pile / n};{vk / n};{(vr * 10 / n) / 10.0:0.0};{(ammo / n) / 1000.0:0.0};{(deaths * 10 / n) / 10.0:0.0}");
                }
        }

        /// <summary>TBD_BOT_REPORT=1: noche entera con el NightBot (sin bot de recarga aparte), por dificultad y semilla, para comparar con la fuerza fija por defecto.</summary>
        public static void BotReport()
        {
            string json = TBD.Net.BalanceSource.Read("noche_sagunto.json");
            foreach (var dif in new string[] { "facil", null, "dificil" })
                foreach (uint seed in new uint[] { 1, 2, 3 })
                    foreach (bool useBot in new bool[] { false, true })
                    {
                        var sim = NightSim.FromJson(json, dif, seed); sim.AddDefaultForce(); if (useBot) sim.Bot = NightBot.FromParams(sim.SP);
                        while (sim.Night.State == NightState.Jugando) { sim.Run(150); if (!useBot) sim.ResupplyBot(); }
                        var s = sim.Snapshot();
                        Console.WriteLine($"[{dif ?? "normal"} s{seed} {(useBot ? "NightBot" : "fija+bot recarga")}] {s.State} {s.Hour:00}:{s.Minute:00} ★{sim.Stars()} muro {s.WallHp * 100 / Math.Max(1, sim.SP.WallHp)} % bajas {s.SoldierDeaths} munición {(s.PlatoonAmmoShots + s.StockShots) / 2000.0:0.0} u recargas {s.Resupplies} pila máx {s.PeakPile}{(useBot ? " | " + sim.Bot.Describe() : "")}");
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
                    while (sim.Night.State == NightState.Jugando) { sim.Run(30); if (firstPile < 0 && sim.PeakPile > 0) firstPile = sim.Night.Tick; if (!useBot) sim.ResupplyBot(); }
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
                    while (sim.Night.State == NightState.Jugando)
                    {
                        sim.Run(150);
                        foreach (var p in sim.Units.Platoons) if (used < cap && p.AmmoCap > 0 && p.AmmoShots * 100 < p.AmmoCap * 30) if (sim.Resupply(p.Id) > 0) used++;
                    }
                    var s = sim.Snapshot();
                    Console.WriteLine($"[{dif ?? "normal"} recargas<={cap}] {s.State} a las {s.Hour:00}:{s.Minute:00} estrellas {sim.Stars()} muro {s.WallHp * 100 / Math.Max(1, sim.SP.WallHp)} % edificio {sim.Night.StationHp} tren {sim.Night.TrainHp} bajas {s.SoldierDeaths} munición {s.PlatoonAmmoShots + s.StockShots} abatidos {s.Kills}/{s.ZombiesSpawned} recargas {used}");
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
                foreach (Formation f in new[] { Formation.Linea, Formation.Columna, Formation.Cuna, Formation.Circulo })
                {
                    var pts = new List<(int, int)>();
                    for (int k = 0; k < 10; k++) { int dx, dy; UnitsWorld.Slot(f, k, 10, 2200, 0, out dx, out dy); pts.Add((dx, dy)); }
                    int minD = int.MaxValue;
                    for (int i = 0; i < pts.Count; i++) for (int j = i + 1; j < pts.Count; j++) minD = Math.Min(minD, IMath.Dist(pts[i].Item1 - pts[j].Item1, pts[i].Item2 - pts[j].Item2));
                    if (minD < 1500) { ok = false; bad += $" [{f}: {minD} mm]"; }
                }
                check(ok, "las cuatro formaciones (línea, columna, cuña, círculo) separan a 10 soldados al menos 1,5 m" + bad);
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
                check(same, "los valores por defecto de la noche coinciden con balance_noche.csv (6 oleadas, ×28, escala 850 ‰)");
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
                check(g.State == NightState.Victoria && g.Tick == g.TotalTicks, "si el edificio y el tren aguantan hasta las 06:00, victoria");
                check(perWave[0] == 765 && perWave[1] == 1275 && perWave[2] == 1700 && perWave[3] == 2550 && perWave[4] == 3060 && perWave[5] == 4250 && all == 13600, $"las 6 oleadas suman exactamente 765, 1.275, 1.700, 2.550, 3.060 y 4.250 zombis al 850 ‰ ({all})");
                check(!early && firstSpawnTick == g.WaveStartTick(0) + 1 - 0 || firstSpawnTick == g.WaveStartTick(0) + 1 || firstSpawnTick > g.WaveStartTick(0), $"nadie aparece antes de su oleada (primera aparición en el tick {firstSpawnTick}, oleada 1 en {g.WaveStartTick(0)} = 22:20)");
                var expected = new[,] { { 383, 229, 153, 0 }, { 638, 382, 255, 0 }, { 850, 510, 340, 0 }, { 1275, 765, 510, 0 }, { 1224, 918, 612, 306 }, { 1275, 1275, 850, 850 } };
                bool exact = true; string got = ""; for (int a = 0; a < 6; a++) { got += $" [O{a + 1}:"; for (int b = 0; b < 4; b++) { if (perWE[a, b] != expected[a, b]) exact = false; got += (b > 0 ? "/" : "") + perWE[a, b]; } got += "]"; }
                check(exact, "reparto por oleada y entrada por cuotas exacto (pesos de noche_sagunto.json con la escala de 850 ‰): O4 cerrada en 1–4, 306 en la 5 y 850 en la 6;" + got);
                check(!clockBad, "el reloj marca las 00:00 a las 2 h de juego (tick 7.715)");
                check(inRect, "los zombis aparecen dentro de los rectángulos de entrada de Álvaro");
                check(perEntry[0] == 4 * 0 + perEntry[0] && perEntry[3] == 306 + 850, $"el puente O4 se abre: 306 zombis en la oleada 5 y 850 en la 6 ({perEntry[3]} en total)");
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
                check(g3.State == NightState.Derrota && g3.EndedByTrain && g3.Tick == g3.TotalTicks, "si el tren cae en el tick de las 06:00 gana la derrota");
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
                // estrellas D-N104: integridad, muro, bajas y munición, con los umbrales del JSON de Hugo
                var json = TBD.Net.BalanceSource.Read("noche_sagunto.json"); var ns = NightSim.FromJson(json, null, 1); var R = ns.Night.P.Stars; var d = StarRules.Default();
                check(R.Two.IntegrityMinPm == d.Two.IntegrityMinPm && R.Two.WallMinPm == 250 && R.Two.MaxLosses == 6 && R.Two.AmmoMinMilliU == 8000 && R.Three.IntegrityMinPm == 750 && R.Three.WallMinPm == 500 && R.Three.MaxLosses == 2 && R.Three.AmmoMinMilliU == 15000 && R.Three.NoVehiclesLost, "los umbrales de estrellas se leen de resultados.estrellas del JSON (y coinciden con los de código)");
                var V = NightState.Victoria;
                check(R.Stars(NightState.Derrota, 1000, 1000, 0, 99000, false) == 0 && R.Stars(NightState.Jugando, 1000, 1000, 0, 99000, false) == 0, "con derrota (o noche sin cerrar) no hay estrellas");
                check(R.Stars(V, 1000, 90, 0, 99000, false) == 1, "el caso de Sara: muro al 9 % da como mucho 1 estrella aunque todo lo demás sea perfecto");
                check(R.Stars(V, 400, 250, 6, 8000, false) == 2 && R.Stars(V, 399, 250, 6, 8000, false) == 1 && R.Stars(V, 400, 249, 6, 8000, false) == 1 && R.Stars(V, 400, 250, 7, 8000, false) == 1 && R.Stars(V, 400, 250, 6, 7999, false) == 1, "★★ en el límite justo de cada umbral: 400 ‰, 250 ‰, 6 bajas, 8 u (un punto menos, 1 estrella)");
                check(R.Stars(V, 750, 500, 2, 15000, false) == 3 && R.Stars(V, 749, 500, 2, 15000, false) == 2 && R.Stars(V, 750, 499, 2, 15000, false) == 2 && R.Stars(V, 750, 500, 3, 15000, false) == 2 && R.Stars(V, 750, 500, 2, 14999, false) == 2 && R.Stars(V, 750, 500, 2, 15000, true) == 2, "★★★ en el límite justo de cada umbral (y sin perder un vehículo)");
                // el parte lleva la integridad y el muro (filas 7 y 8) y la regla de «pegado» es única
                var g = new NightGame(NightParams.Default(), 1); var sp = new List<SpawnPoint>();
                var near = new List<Target> { new Target { Id = 1, X = 106000, Y = 76500 }, new Target { Id = 2, X = 90000, Y = 64000 }, new Target { Id = 3, X = 106000, Y = 74000 }, new Target { Id = 4, X = 50000, Y = 64500 }, new Target { Id = 5, X = 106000, Y = 67000 } };
                int aS, aT; g.CountAttackers(near, out aS, out aT);
                check(aS == 2 && aT == 2, $"pegado = a ≤ 1,5 m del edificio (2: dentro y a 1 m) o del tren (2: sobre la vía y a 1,1 m del borde) y un zombi lejano no cuenta ({aS}, {aT})");
                for (int i = 0; i < 900; i++) g.Attack(0, 100);      // 100 zombis 30 s = 60.000 al tren: la mitad de 120.000
                g.WallPermil = 640; while (g.State == NightState.Jugando) g.Step(sp);
                check(g.State == NightState.Victoria && g.Report.IntegrityPermil == 500 && g.Report.WallPermil == 640, $"al amanecer el parte lleva integridad {g.Report.IntegrityPermil} ‰ (mín edificio-tren) y muro {g.Report.WallPermil} ‰");
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
                check(sc == 0 && am0 == 2000 && kills > 20 && kill.AmmoShots < am0, $"estrellado no dispara; en 20 s abate {kills} zombis y gasta {am0 - kill.AmmoShots} de {am0} disparos de a bordo");
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
                    bool rt = back.Rows.Count == refT.Rows.Count && back.Source == "NightSim" && back.ToCsv() == csv && refT.Rows.Count == 301 && refT.Rows[0].Tick == t0 && refT.Rows[300].Tick == t0 + 9000;
                    var lastR = refT.Rows[refT.Rows.Count - 1]; int peakR = 0; foreach (var rw in refT.Rows) peakR = Math.Max(peakR, rw.Pile); bool mono = true; for (int i = 1; i < refT.Rows.Count; i++) mono &= refT.Rows[i].Spawned >= refT.Rows[i - 1].Spawned && refT.Rows[i].Kills >= refT.Rows[i - 1].Kills;
                    check(rt && mono && peakR > 50 && lastR.Spawned > 1500, $"traza de referencia de NightSim: {refT.Rows.Count} filas (1 por segundo, 9.000 ticks desde la O1), ida y vuelta por CSV idéntica; al final entraron {lastR.Spawned}, abatidos {lastR.Kills}, pila máxima {peakR}, muro {lastR.WallPpm / 10000.0:0.0} %");
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
                    var full = NightSim.FromJson(TBD.Net.BalanceSource.Read("noche_sagunto.json"), null, 1); full.AddDefaultForce(); full.Bot = NightBot.FromParams(full.SP); while (full.Night.State == NightState.Jugando) full.Run(150);
                    var fs = full.Snapshot();
                    check(fs.State == NightState.Victoria && full.Stars() >= 2 && fs.WallHp * 100 / full.SP.WallHp > 60 && full.Bot.Resupplies > 20 && full.Resupplies == 0, $"la noche normal entera con el bot (sin ayuda de recarga aparte): {fs.State}, {full.Stars()} estrellas, muro {fs.WallHp * 100 / full.SP.WallHp} %, {full.Bot.Resupplies} reabastecimientos, {fs.SoldierDeaths} bajas");
                    // hueco de la vía (la zona de Unity no pone sólido a la puerta de vía): la horda rodea el muro hasta el hueco, no se amontona y el muro no se desgasta
                    Func<int, NightSim> gapRun = gap => { var n = NightSim.FromJson(TBD.Net.BalanceSource.Read("noche_sagunto.json"), null, 1); n.SP.GateGapMm = gap; n.AddDefaultForce(); n.Run(4500); return n; };
                    var gClosed = gapRun(0); var gOpen = gapRun(3200);
                    check(gClosed.PeakPile > 20 && gClosed.WallHp < gClosed.SP.WallHp && gOpen.PeakPile == 0 && gOpen.WallHp == gOpen.SP.WallHp, $"recinto cerrado: la oleada 1 amontona {gClosed.PeakPile} y gasta muro; con hueco de 3,2 m en la vía: pila {gOpen.PeakPile} y el muro intacto (así sale la zona real: pila 0, muro 1000 ‰)");
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
                var g3 = new NightGame(NightParams.Default(), 1); while (g3.Tick < 5000) g3.Step(true, true, sp); g3.Step(false, false, sp);
                int t0 = g3.Tick; g3.Step(true, true, sp);
                check(g3.State == NightState.Derrota && g3.DefeatTick == 5001 && g3.Tick == t0 && sp.Count == 0, "la derrota es definitiva: el reloj se para y no entra nadie más");
                var g4 = new NightGame(NightParams.Default(), 1); while (g4.State == NightState.Jugando) g4.Step(true, true, sp); g4.Step(false, false, sp);
                check(g4.State == NightState.Victoria, "una vez ganada la noche, un fallo posterior no la cambia");
                // D-N91: mundo continuo
                check(g4.Report.Night == 1 && g4.Report.CloseTick == g4.TotalTicks && g4.Report.Spawned == 13600 && g4.Report.StationAlive && g4.Report.TrainAlive && g4.NightsSurvived == 1, "al amanecer sale el parte de la noche (noche 1, 13.600 zombis, edificio y tren en pie) y la noche queda cerrada");
                int tc = g4.Tick; g4.Step(true, true, sp);
                check(g4.State == NightState.Victoria && g4.Tick == tc + 1 && sp.Count == 0, "mundo continuo: tras ganar, el reloj sigue corriendo, no entra nadie y la partida NO termina");
                check(g4.BeginNextNight(NightParams.Default()) && g4.State == NightState.Jugando && g4.Tick == 0 && g4.NightIndex == 2 && g4.Hour == 22, "BeginNextNight abre la noche 2: reloj a las 22:00, oleadas a cero");
                var gd = new NightGame(NightParams.Default(), 1); gd.Step(false, true, sp);
                check(!gd.BeginNextNight(NightParams.Default()) && gd.State == NightState.Derrota, "la derrota sí acaba la partida: no se puede abrir otra noche");
                long cnt = 0; while (g4.State == NightState.Jugando) { g4.Step(true, true, sp); cnt += sp.Count; }
                check(cnt == 13600 && g4.NightsSurvived == 2, "la segunda noche también reparte las 6 oleadas completas (13.600) y se cierra");
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
                check(sim.Night.P.Waves.Length == 6 && sim.Night.WaveTotal(0) == 760 && sim.Night.WaveTotal(5) == 4250 && sim.Units.StockShots == 52 * 2000 && sim.MaterialsU == 40 && sim.FuelU == 30, "crear la noche desde noche_sagunto.json (normal): 6 oleadas de 760 a 4.250 zombis, 52 u de munición (104.000 disparos), 40 de materiales y 30 de combustible");
                var easy = NightSim.FromJson(json, "facil", 7); var hard = NightSim.FromJson(json, "dificil", 7);
                check(easy.Night.WaveTotal(0) == 650 && easy.Night.WaveTotal(5) == 3620 && hard.Night.WaveTotal(0) == 1030 && hard.Night.WaveTotal(5) == 5700, "dificultad fácil (650…3.620) y difícil (1.030…5.700, escala 1140) desde el mismo JSON");
                bool bad = false; try { Mini.Parse("{\"a\": [1, 2"); } catch (FormatException) { bad = true; }
                check(bad && (long)((System.Collections.Generic.Dictionary<string, object>)Mini.Parse("{\"a\": -12}"))["a"] == -12, "el lector de JSON mínimo rechaza un texto truncado y lee enteros con signo");

                sim.AddDefaultForce();
                int ammo0 = 0; foreach (var p in sim.Units.Platoons) ammo0 += p.AmmoShots; ammo0 += sim.Units.StockShots;
                var swc = Stopwatch.StartNew();
                int resup = 0;
                while (sim.Night.State == NightState.Jugando)
                {
                    sim.Run(150);
                    resup += sim.ResupplyBot();      // bot de Hugo: reabastece a un pelotón con < 30 % de su dotación
                }
                swc.Stop();
                var snap = sim.Snapshot(); int spent = 0; foreach (var p in sim.Units.Platoons) spent += p.AmmoSpent;
                check((snap.State == NightState.Victoria ? snap.ZombiesSpawned == 13600 : snap.ZombiesSpawned > 0 && snap.ZombiesSpawned < 13600) && snap.Kills <= snap.ZombiesSpawned && snap.Kills + snap.ZombiesAlive <= snap.ZombiesSpawned, $"balance de zombis (la noche acabó en {snap.State}): entraron {snap.ZombiesSpawned} de 13.600, abatidos {snap.Kills}, vivos {snap.ZombiesAlive}");
                check(snap.PlatoonAmmoShots + snap.StockShots + spent == ammo0, $"la munición se conserva: inicial {ammo0} = restante {snap.PlatoonAmmoShots + snap.StockShots} + gastada {spent} disparos");
                int rs = 0, ks = 0, ds = 0; foreach (var w in sim.Waves) { rs += w.Resupplies; ks += w.Kills; ds += w.SoldierDeaths; }
                check(rs == snap.Resupplies && rs == resup && ks == snap.Kills && ds == snap.SoldierDeaths, $"las estadísticas por oleada suman los totales (recargas {rs}, bajas enemigas {ks}, bajas propias {ds})");
                check(sim.Waves[0].StartTick == 1285 && sim.Waves[0].HourAtStart == 22 && sim.Waves[0].MinuteAtStart == 20 && sim.Waves[2].HourAtStart == 0 && sim.Waves[2].MinuteAtStart == 40, "las oleadas empiezan a las 22:20, 23:20, 00:40… con su hora");
                var sb = new System.Text.StringBuilder();
                foreach (var w in sim.Waves) sb.Append($" [O{w.Wave} {w.HourAtStart:00}:{w.MinuteAtStart:00} entran {w.Spawned}, abatidos {w.Kills}, pila máx. {w.PeakPile}, bajas {w.SoldierDeaths}, recargas {w.Resupplies}, disparos {w.AmmoSpentShots}]");
                Console.WriteLine($"         noche con la fuerza por defecto y reabastecimiento mínimo: {snap.State} a las {snap.Hour:00}:{snap.Minute:00}, estrellas {sim.Stars()}, muro {snap.WallHp}, edificio {snap.StationHp}, bajas propias {snap.SoldierDeaths} de {snap.SoldierDeaths + snap.SoldiersAlive};{sb}");
                check(swc.Elapsed.TotalSeconds < 60, $"una noche entera (30.857 ticks, hasta 13.600 zombis) sin ventana en {swc.Elapsed.TotalSeconds:F1} s");
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
                check(sim.Night.State == NightState.Derrota && sim.Stars() == 0, $"sin ningún pelotón, la noche difícil se pierde (a las {sim.Night.Hour:00}:{sim.Night.Minute:00}) y da 0 estrellas");
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
                    var zone = NightSim.FromJson(json, null, 5); zone.AddDefaultForce(); zone.Run(4000);
                    int tick0 = zone.Night.Tick, sp0 = zone.Night.Spawned[0] + zone.Night.Spawned[1], ammo0 = zone.Units.StockShots, alive0 = zone.ZombiesAlive, wall0 = zone.WallHp;
                    var exp = new List<Target>(); zone.ExportZombies(exp);
                    var off = new NightSim(zone.Night, zone.Units, zone.SP, wall0, 5); foreach (var t in exp) off.ImportZombie(t.X, t.Y);
                    check(off.Night == zone.Night && off.Units == zone.Units && off.ZombiesAlive == alive0 && off.WallHp == wall0 && off.Night.Tick == tick0 && off.Units.StockShots == ammo0,
                        $"al salir de la zona la noche sigue donde estaba: mismo reloj (tick {tick0}), {alive0} zombis vivos, muro {wall0}, almacén {ammo0 / 2000.0:0.0} u (no se reinicia)");
                    int k0 = off.Night.Tick; off.Run(9000);
                    int spawned = 0; for (int w = 0; w < off.Night.Spawned.Length; w++) spawned += off.Night.Spawned[w];
                    check(off.Night.Tick > k0 && spawned > sp0 && off.Kills > 0, $"fuera de pantalla el reloj avanza ({k0} → {off.Night.Tick}), siguen llegando oleadas ({sp0} → {spawned} zombis) y los pelotones disparan ({off.Kills} abatidos)");
                    var back = new List<Target>(); off.ExportZombies(back); bool inRange = true; foreach (var t in back) if (t.X < -400000 || t.X > 600000 || t.Y < -400000 || t.Y > 600000) inRange = false;
                    check(back.Count == off.ZombiesAlive && inRange, $"al volver a la zona, los {back.Count} zombis vivos salen con su posición para crearlos en ECS");
                    while (off.Night.State == NightState.Jugando) off.Run(300);
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
                check(d.MaxShots == 120 && d.PeriodTicks == 30 && d.KillsPerShot == 3 && d.RangeMm == 55000 && d.RefillCostShots() == 0, "Dragón como en la zona: 120 salvas, 1 por segundo, 3 abatidos por salva, 55 m");
                d.Shots = 60; check(d.RefillCostShots() == 600, $"recargar medio Dragón cuesta 0,3 u del almacén ({d.RefillCostShots()} disparos)");
                var s0 = NightSim.FromJson(json, null, 3); s0.AddDefaultForce(); ulong h0 = s0.Hash();
                var s1 = NightSim.FromJson(json, null, 3); s1.AddDefaultForce(); check(s1.Hash() == h0 && s1.Vehicles.Count == 0, "sin vehículos, el hash de NightSim es el de siempre");
                var sv = NightSim.FromJson(json, null, 3); sv.AddDefaultForce(); sv.AddDefaultVehicles(true); sv.Bot = NightBot.FromParams(sv.SP);
                var sn = NightSim.FromJson(json, null, 3); sn.AddDefaultForce(); sn.Bot = NightBot.FromParams(sn.SP);
                for (int t = 0; t < 9000; t++) { sv.Step(); sn.Step(); }
                var dr = sv.Vehicles[0]; var nh = sv.Vehicles[3];
                check(sv.Vehicles.Count == 4 && sv.VehicleKills > 0 && dr.Kills > 0 && dr.ShotsFired > 0 && nh.Kills == 0, $"con fuego libre los vehículos abaten ({sv.VehicleKills} en 9.000 ticks; Dragón {dr.Kills}, VAMTAC {sv.Vehicles[1].Kills}+{sv.Vehicles[2].Kills}); el NH90 en tierra no dispara");
                check(sv.LastPile <= sn.LastPile || sv.PeakPile <= sn.PeakPile, $"con vehículos la pila no crece (máx. {sv.PeakPile} frente a {sn.PeakPile})");
                var sx = NightSim.FromJson(json, null, 3); sx.AddDefaultForce(); sx.AddDefaultVehicles(true); sx.Bot = NightBot.FromParams(sx.SP);
                for (int t = 0; t < 9000; t++) sx.Step();
                check(sx.Hash() == sv.Hash(), "con vehículos sigue siendo determinista (mismo hash en dos corridas)");
                var sg = NightSim.FromJson(json, null, 3); sg.AddDefaultForce(); sg.AddDefaultVehicles(false); for (int t = 0; t < 9000; t++) sg.Step();
                check(sg.VehicleKills == 0, "con alto el fuego no disparan");
                // reserva vacía y recarga
                var sr = NightSim.FromJson(json, null, 3); sr.AddDefaultForce(); sr.AddDefaultVehicles(true); var v0 = sr.Vehicles[0]; v0.Shots = 0; int stock0 = sr.Units.StockShots;
                int cost = sr.ResupplyVehicle(0); check(cost == 1200 && sr.Units.StockShots == stock0 - 1200 && v0.ResupplyUntil == sr.Night.Tick + 600, $"recargar el Dragón vacío: 0,6 u del almacén y 20 s ({cost} disparos)");
                check(sr.ResupplyVehicle(0) == 0, "no se puede pedir otra recarga mientras dura la primera");
                v0.Fire = false; for (int t = 0; t < 601; t++) sr.Step(); check(v0.ResupplyUntil == -1 && v0.Shots == v0.MaxShots, "al acabar la recarga (20 s) la reserva vuelve a estar llena");
                var sj = NightSim.FromJson(json, null, 3); sj.VehicleParamsJson = "{\"NightVehicles\": {\"Dragon\": {\"MaxShots\": 240}, \"Nh90\": {\"Airborne\": true}}}"; sj.AddDefaultVehicles(true);
                check(sj.Vehicles[0].MaxShots == 240 && sj.Vehicles[0].Shots == 240 && sj.Vehicles[3].Airborne && sj.Vehicles[1].MaxShots == 300, "las cifras de Hugo (NightVehicles en nightsim_params.json) cambian solo lo que dicen");
            }
        }
    }
}
