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

            // ------------------------------------------------------------------ movimiento
            {
                var w = new UnitsWorld(7); var p = w.AddPlatoon(Presets.Fusileros, 0, 0, 0, Formation.Columna);
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
                int dist = IMath.Dist(60000, 20000), minSpeed = 2300;
                int ideal = dist * IMath.TickRate / minSpeed;
                check(arrived > 0 && arrived >= ideal * 9 / 10 && arrived <= ideal * 14 / 10, $"el pelotón llega a 63 m: {arrived} ticks (ideal con el más lento: {ideal})");
                int worstSlot = 0; for (int i = 0; i < p.Men.Count; i++) worstSlot = Math.Max(worstSlot, IMath.Dist(p.Men[i].X - p.Men[i].SlotX, p.Men[i].Y - p.Men[i].SlotY));
                check(worstSlot < 50 && IMath.Diff(p.Heading, 16384) == 0, $"todos en su puesto al parar (máx. {worstSlot} mm) y orientados al rumbo final pedido");
                check(maxStep <= 2600 * 5 / 4 / 30 + 2, $"nadie se teletransporta: paso máximo {maxStep} mm/tick (límite {2600 * 5 / 4 / 30 + 2})");
                w.SetFormation(p.Id, Formation.Linea); for (int t = 0; t < 120; t++) w.Step(none, res);
                var xs = new HashSet<int>(); foreach (var m in p.Men) xs.Add(m.X / 500);   // rumbo final 90°: la línea se abre a lo largo de X
                check(xs.Count >= 6, $"al cambiar a línea se abren a lo ancho ({xs.Count} bandas distintas)");
            }

            // ------------------------------------------------------------------ combate
            {
                var w = new UnitsWorld(11); var p = w.AddPlatoon(Presets.Fusileros, 0, 0, 0, Formation.Cuna);
                var z = new List<Zed>(); var rng = new IRng(5);
                for (int i = 0; i < 120; i++) z.Add(new Zed { Id = 100 + i, X = 60000 + rng.Below(30000), Y = rng.Below(40000) - 20000 });
                var res = new StepResult(); int kills = 0, shots = 0, ammo = 0; int maxRangeSeen = 0; bool rangeOk = true;
                var kinds = new Dictionary<InfKind, int>();
                for (int t = 0; t < 1800; t++)
                {
                    foreach (var a in z) if (a.Hp > 0) { int dx = -a.X, dy = -a.Y, d = Math.Max(1, IMath.Dist(dx, dy)); if (d > 1200) { a.X += dx * 1500 / 30 / d; a.Y += dy * 1500 / 30 / d; } }
                    w.Step(ToTargets(z), res);
                    foreach (var s in res.Shots)
                    {
                        shots++; kinds[s.Kind] = kinds.TryGetValue(s.Kind, out int c) ? c + 1 : 1;
                        var man = p.Men.Find(m => m.Id == s.Man);
                        int d = IMath.Dist(s.AimX - man.X, s.AimY - man.Y);
                        if (!s.Splash && d > InfCatalog.Get(s.Kind).RangeMm + 50) rangeOk = false; maxRangeSeen = Math.Max(maxRangeSeen, d);
                    }
                    ammo += res.AmmoSpent; kills += Apply(res, z, w);
                }
                int alive = 0; foreach (var a in z) if (a.Hp > 0) alive++;
                check(kills > 80 && kills == 120 - alive, $"un pelotón de 10 abate {kills} de 120 zombis que avanzan a 1,5 m/s en 60 s (quedan {alive})");
                check(rangeOk, $"nadie dispara fuera de su alcance (máx. visto {maxRangeSeen / 1000} m)");
                check(kinds.ContainsKey(InfKind.Fusilero) && kinds.ContainsKey(InfKind.Ametrallador) && kinds.ContainsKey(InfKind.Tirador),
                    "disparan fusileros, ametrallador y tirador (el granadero, al ir en la cola, no llega a tener horda a su alcance: se prueba aparte): " + string.Join(", ", kinds));
                check(kinds[InfKind.Ametrallador] > 2 * kinds[InfKind.Fusilero] / 6 && ammo == p.AmmoSpent && ammo > shots, $"la ametralladora dispara más del doble que cada fusilero ({kinds[InfKind.Ametrallador]} disparos contra {kinds[InfKind.Fusilero] / 6} por fusilero), y la munición gastada ({ammo}) cuadra con el pelotón");
            }
            {
                // lanzagranadas: área con caída, alcance mínimo y a mano alzada
                var w = new UnitsWorld(3); var p = w.AddPlatoon(new[] { InfKind.Granadero }, 0, 0, 0);
                var z = new List<Zed>(); for (int i = 0; i < 12; i++) z.Add(new Zed { Id = i + 1, X = 25000 + (i % 4) * 600, Y = (i / 4) * 600 });
                var res = new StepResult(); w.Step(ToTargets(z), res);
                int hit = res.Hits.Count; int maxD = 0, minD = int.MaxValue; foreach (var h in res.Hits) { maxD = Math.Max(maxD, h.Damage); minD = Math.Min(minD, h.Damage); }
                check(res.Shots.Count == 1 && res.Shots[0].Splash && hit >= 4 && maxD <= 130 && minD >= 130 * 40 / 100 - 1, $"una granada alcanza a {hit} zombis a la vez con daño entre {minD} y {maxD} (100 % centro, 40 % borde)");
                var near = new List<Zed> { new Zed { Id = 1, X = 5000, Y = 0 } }; var w2 = new UnitsWorld(3); w2.AddPlatoon(new[] { InfKind.Granadero }, 0, 0, 0);
                w2.Step(ToTargets(near), res);
                check(res.Shots.Count == 0, "el granadero no dispara a menos de 8 m (alcance mínimo)");
                var w3 = new UnitsWorld(3); var p3 = w3.AddPlatoon(Presets.Fusileros, 0, 0, 0); w3.SetHoldFire(p3.Id, true);
                int s3 = 0; for (int t = 0; t < 120; t++) { w3.Step(ToTargets(new List<Zed> { new Zed { Id = 1, X = 15000, Y = 0 } }), res); s3 += res.Shots.Count; }
                check(s3 == 0, "con la orden de alto el fuego no se dispara ni un tiro");
            }
            {
                var w = new UnitsWorld(1); var p = w.AddPlatoon(Presets.Fusileros, 0, 0, 0, Formation.Cuna);
                int leader = p.Men[0].Id; bool died = w.DamageMan(p.Id, leader, 500);
                var res = new StepResult(); w.Step(null, res);
                check(died && p.AliveCount() == 9, "muere el jefe y el pelotón sigue con 9");
                int c = 0; var pts = new HashSet<(int, int)>(); foreach (var m in p.Men) if (m.Alive) { c++; pts.Add((m.SlotX, m.SlotY)); }
                check(pts.Count == 9, "la formación se recompone con los vivos: 9 puestos distintos");
            }

            // ------------------------------------------------------------------ determinismo y guardado
            {
                Func<uint, List<ulong>> run = seed =>
                {
                    var w = new UnitsWorld(seed); var a = w.AddPlatoon(Presets.Fusileros, 0, 0, 0); var b = w.AddPlatoon(Presets.Ametralladores, 0, 12000, 0, Formation.Linea);
                    var z = new List<Zed>(); var rg = new IRng(99); for (int i = 0; i < 400; i++) z.Add(new Zed { Id = i + 1, X = 70000 + rg.Below(60000), Y = rg.Below(50000) - 25000 });
                    var res = new StepResult(); var hs = new List<ulong>();
                    for (int t = 0; t < 1800; t++)
                    {
                        if (t == 100) w.Move(a.Id, 30000, 5000, 0);
                        if (t == 400) w.SetFormation(b.Id, Formation.Circulo);
                        if (t == 700) w.Move(b.Id, 20000, -10000);
                        foreach (var q in z) if (q.Hp > 0) { int dx = 20000 - q.X, dy = -q.Y, d = Math.Max(1, IMath.Dist(dx, dy)); if (d > 1500) { q.X += dx * 2000 / 30 / d; q.Y += dy * 2000 / 30 / d; } }
                        w.Step(ToTargets(z), res); Apply(res, z, w);
                        if (t % 100 == 0) hs.Add(w.Hash());
                        if (t == 900) { var sw = new SaveWriter(); w.SaveState(sw); var w2 = new UnitsWorld(0); w2.LoadState(new SaveReader(sw.ToArray())); hs.Add(w2.Hash() == w.Hash() ? 1UL : 0UL); }
                    }
                    hs.Add(w.Hash()); return hs;
                };
                var h1 = run(42); var h2 = run(42); var h3 = run(43);
                bool same = h1.Count == h2.Count; for (int i = 0; same && i < h1.Count; i++) same = h1[i] == h2[i];
                check(same, $"dos partidas iguales con órdenes y combate dan el mismo hash en {h1.Count} puntos (1.800 ticks)");
                check(h1[h1.Count - 1] != h3[h3.Count - 1], "otra semilla da otro estado final");
                bool saveOk = false; for (int i = 0; i < h1.Count; i++) { } int idx = 10; saveOk = h1[idx] == 1UL;   // posición del control de guardado (tras el tick 900)
                check(saveOk, "guardar y cargar el estado de los pelotones da el mismo hash");
                // seguir jugando tras cargar: mismos hashes tick a tick
                var wa = new UnitsWorld(5); wa.AddPlatoon(Presets.Fusileros, 0, 0, 0); wa.Move(1, 40000, 0);
                var ra = new StepResult(); var tgs = new List<Target> { new Target { Id = 1, X = 30000, Y = 3000 }, new Target { Id = 2, X = 31000, Y = -2000 } };
                for (int t = 0; t < 300; t++) wa.Step(tgs, ra);
                var sw2 = new SaveWriter(); wa.SaveState(sw2); var wb = new UnitsWorld(0); wb.LoadState(new SaveReader(sw2.ToArray()));
                bool lock2 = true; var rb = new StepResult();
                for (int t = 0; t < 600 && lock2; t++) { wa.Step(tgs, ra); wb.Step(tgs, rb); lock2 = wa.Hash() == wb.Hash() && ra.Shots.Count == rb.Shots.Count; }
                check(lock2, "tras cargar, el pelotón sigue igual que el original (600 ticks, mismos disparos)");
                bool trunc = true; var full = sw2.ToArray();
                for (int cut = 0; cut < full.Length && trunc; cut += 7) { try { new UnitsWorld(0).LoadState(new SaveReader(full, 0, cut)); trunc = false; } catch (SaveException) { } }
                check(trunc, "un guardado de pelotones recortado da siempre error, nunca un mundo a medias en silencio");
            }

            // ------------------------------------------------------------------ rendimiento
            {
                var w = new UnitsWorld(2); for (int i = 0; i < 4; i++) w.AddPlatoon(Presets.Fusileros, i * 8000, 0, 0);
                var z = new List<Target>(); var rg = new IRng(3); for (int i = 0; i < 10000; i++) z.Add(new Target { Id = i, X = 30000 + rg.Below(150000), Y = rg.Below(120000) - 60000 });
                var res = new StepResult(); var sw = Stopwatch.StartNew(); for (int t = 0; t < 300; t++) w.Step(z, res); sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds / 300;
                check(ms < 3.0, $"40 soldados contra 10.000 objetivos: {ms:F2} ms por tick (< 3 ms; cada uno solo busca cuando puede disparar)");
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
