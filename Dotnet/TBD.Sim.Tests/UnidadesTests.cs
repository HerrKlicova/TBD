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
                int leader = p.Men[0].Id; bool died = w.DamageMan(p.Id, leader, 500);
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
                check(perWave[0] == 510 && perWave[1] == 1020 && perWave[2] == 1700 && perWave[3] == 2550 && perWave[4] == 3060 && perWave[5] == 4250 && all == 13090, $"las 6 oleadas suman exactamente 510, 1.020, 1.700, 2.550, 3.060 y 4.250 zombis al 850 ‰ ({all})");
                check(!early && firstSpawnTick == g.WaveStartTick(0) + 1 - 0 || firstSpawnTick == g.WaveStartTick(0) + 1 || firstSpawnTick > g.WaveStartTick(0), $"nadie aparece antes de su oleada (primera aparición en el tick {firstSpawnTick}, oleada 1 en {g.WaveStartTick(0)} = 22:20)");
                var expected = new[,] { { 255, 153, 102, 0 }, { 510, 306, 204, 0 }, { 850, 510, 340, 0 }, { 1275, 765, 510, 0 }, { 1224, 918, 612, 306 }, { 1275, 1275, 850, 850 } };
                bool exact = true; string got = ""; for (int a = 0; a < 6; a++) { got += $" [O{a + 1}:"; for (int b = 0; b < 4; b++) { if (perWE[a, b] != expected[a, b]) exact = false; got += (b > 0 ? "/" : "") + perWE[a, b]; } got += "]"; }
                check(exact, "reparto por oleada y entrada EXACTO al de Hugo (noche_sagunto.json, normal): O4 cerrada en 1–4, 306 en la 5 y 850 en la 6;" + got);
                check(!clockBad, "el reloj marca las 00:00 a las 2 h de juego (tick 7.715)");
                check(inRect, "los zombis aparecen dentro de los rectángulos de entrada de Álvaro");
                check(perEntry[0] == 4 * 0 + perEntry[0] && perEntry[3] == 306 + 850, $"el puente O4 se abre: 306 zombis en la oleada 5 y 850 en la 6 ({perEntry[3]} en total)");
                int wtot = g.WaveTotal(0); int fc = g.Cumulative(0, g.WaveDurationTicks(0) * 25 / 100);
                check(Math.Abs(fc - wtot * 55 / 100) <= 1 && fymax - fymin < 17000 / 3 + 2 && ymax - ymin > 17000 * 8 / 10, $"frente denso y cola dispersa: el 55 % entra en el primer 25 % del tiempo ({fc} de {wtot}) por el tercio central ({(fymax - fymin) / 1000.0:F1} m) y la cola se abre a {(ymax - ymin) / 1000.0:F1} m");
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
                check(g4.Report.Night == 1 && g4.Report.CloseTick == g4.TotalTicks && g4.Report.Spawned == 13090 && g4.Report.StationAlive && g4.Report.TrainAlive && g4.NightsSurvived == 1, "al amanecer sale el parte de la noche (noche 1, 13.090 zombis, edificio y tren en pie) y la noche queda cerrada");
                int tc = g4.Tick; g4.Step(true, true, sp);
                check(g4.State == NightState.Victoria && g4.Tick == tc + 1 && sp.Count == 0, "mundo continuo: tras ganar, el reloj sigue corriendo, no entra nadie y la partida NO termina");
                check(g4.BeginNextNight(NightParams.Default()) && g4.State == NightState.Jugando && g4.Tick == 0 && g4.NightIndex == 2 && g4.Hour == 22, "BeginNextNight abre la noche 2: reloj a las 22:00, oleadas a cero");
                var gd = new NightGame(NightParams.Default(), 1); gd.Step(false, true, sp);
                check(!gd.BeginNextNight(NightParams.Default()) && gd.State == NightState.Derrota, "la derrota sí acaba la partida: no se puede abrir otra noche");
                long cnt = 0; while (g4.State == NightState.Jugando) { g4.Step(true, true, sp); cnt += sp.Count; }
                check(cnt == 13090 && g4.NightsSurvived == 2, "la segunda noche también reparte las 6 oleadas completas (13.090) y se cierra");
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
                check(sim.Night.P.Waves.Length == 6 && sim.Night.WaveTotal(0) == 510 && sim.Night.WaveTotal(5) == 4250 && sim.Units.StockShots == 45 * 2000 && sim.MaterialsU == 40 && sim.FuelU == 30, "crear la noche desde noche_sagunto.json (normal): 6 oleadas de 510 a 4.250 zombis, 45 u de munición (90.000 disparos), 40 de materiales y 30 de combustible");
                var easy = NightSim.FromJson(json, "facil", 7); var hard = NightSim.FromJson(json, "dificil", 7);
                check(easy.Night.WaveTotal(0) == 440 && easy.Night.WaveTotal(5) == 3620 && hard.Night.WaveTotal(0) == 600 && hard.Night.WaveTotal(5) == 5000, "dificultad fácil (440…3.620) y difícil (600…5.000) desde el mismo JSON");
                bool bad = false; try { Mini.Parse("{\"a\": [1, 2"); } catch (FormatException) { bad = true; }
                check(bad && (long)((System.Collections.Generic.Dictionary<string, object>)Mini.Parse("{\"a\": -12}"))["a"] == -12, "el lector de JSON mínimo rechaza un texto truncado y lee enteros con signo");

                sim.AddDefaultForce();
                int ammo0 = 0; foreach (var p in sim.Units.Platoons) ammo0 += p.AmmoShots; ammo0 += sim.Units.StockShots;
                var swc = Stopwatch.StartNew();
                int resup = 0;
                while (sim.Night.State == NightState.Jugando)
                {
                    sim.Run(150);
                    foreach (var p in sim.Units.Platoons) if (p.AmmoShots * 4 < p.AmmoCap && sim.Resupply(p.Id) > 0) resup++;      // jugador automático mínimo: reabastece a un pelotón con < 25 %
                }
                swc.Stop();
                var snap = sim.Snapshot(); int spent = 0; foreach (var p in sim.Units.Platoons) spent += p.AmmoSpent;
                check((snap.State == NightState.Victoria ? snap.ZombiesSpawned == 13090 : snap.ZombiesSpawned > 0 && snap.ZombiesSpawned < 13090) && snap.Kills <= snap.ZombiesSpawned && snap.Kills + snap.ZombiesAlive <= snap.ZombiesSpawned, $"balance de zombis (la noche acabó en {snap.State}): entraron {snap.ZombiesSpawned} de 13.090, abatidos {snap.Kills}, vivos {snap.ZombiesAlive}");
                check(snap.PlatoonAmmoShots + snap.StockShots + spent == ammo0, $"la munición se conserva: inicial {ammo0} = restante {snap.PlatoonAmmoShots + snap.StockShots} + gastada {spent} disparos");
                int rs = 0, ks = 0, ds = 0; foreach (var w in sim.Waves) { rs += w.Resupplies; ks += w.Kills; ds += w.SoldierDeaths; }
                check(rs == snap.Resupplies && rs == resup && ks == snap.Kills && ds == snap.SoldierDeaths, $"las estadísticas por oleada suman los totales (recargas {rs}, bajas enemigas {ks}, bajas propias {ds})");
                check(sim.Waves[0].StartTick == 1285 && sim.Waves[0].HourAtStart == 22 && sim.Waves[0].MinuteAtStart == 20 && sim.Waves[2].HourAtStart == 0 && sim.Waves[2].MinuteAtStart == 40, "las oleadas empiezan a las 22:20, 23:20, 00:40… con su hora");
                var sb = new System.Text.StringBuilder();
                foreach (var w in sim.Waves) sb.Append($" [O{w.Wave} {w.HourAtStart:00}:{w.MinuteAtStart:00} entran {w.Spawned}, abatidos {w.Kills}, pila máx. {w.PeakPile}, bajas {w.SoldierDeaths}, recargas {w.Resupplies}, disparos {w.AmmoSpentShots}]");
                Console.WriteLine($"         noche con la fuerza por defecto y reabastecimiento mínimo: {snap.State} a las {snap.Hour:00}:{snap.Minute:00}, estrellas {sim.Stars()}, muro {snap.WallHp}, edificio {snap.StationHp}, bajas propias {snap.SoldierDeaths} de {snap.SoldierDeaths + snap.SoldiersAlive};{sb}");
                check(swc.Elapsed.TotalSeconds < 60, $"una noche entera (30.857 ticks, hasta 13.090 zombis) sin ventana en {swc.Elapsed.TotalSeconds:F1} s");
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
        }
    }
}
