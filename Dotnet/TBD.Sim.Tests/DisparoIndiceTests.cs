using System;
using System.Collections.Generic;
using System.Diagnostics;
using TBD.Sim;
using Unity.Collections;
using Unity.Mathematics;

namespace TBD.Sim.Tests
{
    /// <summary>
    /// Índice espacial de los disparos y del cuerpo a cuerpo (ZombieGrid). La prueba de verdad es la equivalencia: la versión
    /// exhaustiva de antes (copiada aquí tal cual) y la nueva dejan exactamente el mismo estado, tick a tick, en muchos escenarios
    /// aleatorios con empates de distancia, zombis abatidos a mitad de tick, tropas fuera de la horda y rejillas enormes.
    /// </summary>
    static class DisparoIndiceTests
    {
        // ---- la versión anterior, sin índice (referencia)
        static int FireRef(NativeArray<Soldier> soldiers, NativeArray<int> order, NativeArray<Zombie> zombies, ref ZoneAmmo ammo, uint tick, in CombatParams cp, NativeArray<int> pileDelta, bool trainStopped)
        {
            int kills = 0;
            for (int o = 0; o < order.Length; o++)
            {
                int si = order[o];
                var s = soldiers[si];
                bool canFire = s.State == SoldierState.Deployed || (s.State == SoldierState.Embarked && trainStopped);
                if (!canFire) continue;
                if (s.Cooldown > 0) { s.Cooldown--; soldiers[si] = s; continue; }
                float range = s.Kind == SoldierKind.MachineGun ? cp.MgRange : cp.RifleRange;
                int best = -1;
                float bestD2 = range * range;
                uint bestId = uint.MaxValue;
                for (int i = 0; i < zombies.Length; i++)
                {
                    var z = zombies[i];
                    if (z.State == ZState.Dead) continue;
                    float2 d = z.Pos.xz - s.Pos.xz;
                    float d2 = math.dot(d, d);
                    if (d2 < bestD2 || (d2 == bestD2 && z.SimId < bestId)) { bestD2 = d2; best = i; bestId = z.SimId; }
                }
                if (best < 0 || ammo.ShotsLeft <= 0) { soldiers[si] = s; continue; }
                ammo.ShotsLeft--;
                ammo.ShotsFired++;
                s.Cooldown = s.Kind == SoldierKind.MachineGun ? cp.MgCooldown : cp.RifleCooldown;
                var tz = zombies[best];
                float2 dv = tz.Pos.xz - s.Pos.xz;
                s.Heading = math.atan2(dv.x, dv.y);
                s.AimPoint = tz.Pos;
                s.LastHit = 0;
                uint roll = math.hash(new uint2(s.SimId * 0x9E3779B1u ^ 0x5EEDu, tick)) % 1000u;
                if (roll < (uint)cp.HitPermil(s.Kind, math.sqrt(bestD2)))
                {
                    if (tz.State == ZState.Settled && tz.SettledCell >= 0 && pileDelta.Length > 0) pileDelta[tz.SettledCell] -= 1;
                    tz.State = ZState.Dead;
                    s.LastHit = 1;
                    tz.SettledCell = -1;
                    tz.Vel = float3.zero;
                    zombies[best] = tz;
                    kills++;
                    ammo.Kills++;
                }
                soldiers[si] = s;
            }
            return kills;
        }

        static int MeleeRef(NativeArray<Soldier> soldiers, NativeArray<Zombie> zombies, ref ZoneAmmo ammo, in CombatParams cp)
        {
            int fallen = 0;
            float r2 = cp.MeleeRange * cp.MeleeRange;
            for (int si = 0; si < soldiers.Length; si++)
            {
                var s = soldiers[si];
                if (s.State != SoldierState.Deployed) continue;
                int touching = 0;
                for (int i = 0; i < zombies.Length; i++)
                {
                    var z = zombies[i];
                    if (z.State == ZState.Dead || z.State == ZState.Fall) continue;
                    float2 d = z.Pos.xz - s.Pos.xz;
                    if (math.dot(d, d) <= r2) touching++;
                }
                if (touching == 0) continue;
                s.Hp -= touching * cp.ZombieDamagePerTick;
                if (s.Hp <= 0) { s.Hp = 0; s.State = SoldierState.Dead; fallen++; ammo.SoldiersLost++; }
                soldiers[si] = s;
            }
            return fallen;
        }

        struct Scenario { public int Zombies, Soldiers, Ticks; public float W, H, OffX, OffZ; public bool Lattice; public uint Seed; public bool Stopped; }

        static uint Rnd(ref uint s) { s = s * 1664525u + 1013904223u; return s >> 8; }

        static void Build(Scenario sc, out Zombie[] zs, out Soldier[] ss)
        {
            uint r = sc.Seed;
            zs = new Zombie[sc.Zombies];
            for (int i = 0; i < zs.Length; i++)
            {
                float x, z;
                if (sc.Lattice) { x = sc.OffX + (Rnd(ref r) % 40) * 1.5f; z = sc.OffZ + (Rnd(ref r) % 40) * 1.5f; }   // posiciones repetidas: empates de distancia
                else { x = sc.OffX + (Rnd(ref r) % 100000) / 100000f * sc.W; z = sc.OffZ + (Rnd(ref r) % 100000) / 100000f * sc.H; }
                var zb = Zombie.Create((uint)(10 + i * 3), new float2(x, z), 0f);
                uint st = Rnd(ref r) % 10;
                zb.State = st == 0 ? ZState.Dead : st == 1 ? ZState.Fall : st == 2 ? ZState.Settled : ZState.Walk;
                zb.SettledCell = zb.State == ZState.Settled ? (int)(Rnd(ref r) % 200) : -1;
                zs[i] = zb;
            }
            // orden de array distinto al de SimId: el de SimId menor no es siempre el primero
            for (int i = zs.Length - 1; i > 0; i--) { int j = (int)(Rnd(ref r) % (uint)(i + 1)); var t = zs[i]; zs[i] = zs[j]; zs[j] = t; }
            ss = new Soldier[sc.Soldiers];
            for (int i = 0; i < ss.Length; i++)
            {
                float x = sc.Lattice ? sc.OffX + (Rnd(ref r) % 40) * 1.5f + 0.75f : sc.OffX + (Rnd(ref r) % 100000) / 100000f * sc.W * 1.3f - sc.W * 0.15f;
                float z = sc.Lattice ? sc.OffZ + (Rnd(ref r) % 40) * 1.5f + 0.75f : sc.OffZ + (Rnd(ref r) % 100000) / 100000f * sc.H * 1.3f - sc.H * 0.15f;
                ss[i] = new Soldier { SimId = 5000u + (uint)i * 7u, Pos = new float3(x, 0f, z), Hp = 100, Kind = i % 7 == 0 ? SoldierKind.MachineGun : SoldierKind.Rifle,
                    State = (i % 5 == 4) ? SoldierState.Embarked : SoldierState.Deployed, Cooldown = (int)(Rnd(ref r) % 6) };
            }
        }

        static bool Same(Zombie[] a, Zombie[] b, Soldier[] sa, Soldier[] sb, ZoneAmmo ma, ZoneAmmo mb, int[] pa, int[] pb)
        {
            if (ma.ShotsLeft != mb.ShotsLeft || ma.ShotsFired != mb.ShotsFired || ma.Kills != mb.Kills || ma.SoldiersLost != mb.SoldiersLost) return false;
            for (int i = 0; i < a.Length; i++) if (HashMath.OfZombie(a[i]) != HashMath.OfZombie(b[i]) || a[i].State != b[i].State) return false;
            for (int i = 0; i < sa.Length; i++) if (HashMath.OfSoldier(sa[i]) != HashMath.OfSoldier(sb[i]) || sa[i].Hp != sb[i].Hp || sa[i].Cooldown != sb[i].Cooldown || sa[i].State != sb[i].State) return false;
            for (int i = 0; i < pa.Length; i++) if (pa[i] != pb[i]) return false;
            return true;
        }

        /// <summary>Corre las dos versiones en paralelo, moviendo los zombis un poco cada tick, y devuelve si coinciden en todos los ticks.</summary>
        static bool Equivalent(Scenario sc, CombatParams cp, out long shots, out int kills)
        {
            Build(sc, out var zs, out var ss);
            var zA = (Zombie[])zs.Clone(); var zB = (Zombie[])zs.Clone();
            var sA = (Soldier[])ss.Clone(); var sB = (Soldier[])ss.Clone();
            var pA = new int[200]; var pB = new int[200];
            var mA = new ZoneAmmo { ShotsLeft = 1_000_000 }; var mB = mA;
            var nzA = new NativeArray<Zombie>(zA); var nzB = new NativeArray<Zombie>(zB);
            var nsA = new NativeArray<Soldier>(sA); var nsB = new NativeArray<Soldier>(sB);
            var npA = new NativeArray<int>(pA); var npB = new NativeArray<int>(pB);
            var orderA = new NativeArray<int>(ss.Length, Allocator.Temp); var orderB = new NativeArray<int>(ss.Length, Allocator.Temp);
            SoldierLogic.SortById(nsA, orderA); SoldierLogic.SortById(nsB, orderB);
            uint r = sc.Seed ^ 0xABCDu;
            for (int t = 0; t < sc.Ticks; t++)
            {
                for (int i = 0; i < zA.Length; i++)            // los zombis se mueven entre ticks (no dentro de uno)
                {
                    if (zA[i].State == ZState.Dead) continue;
                    float dx = ((int)(Rnd(ref r) % 21) - 10) * 0.05f, dz = ((int)(Rnd(ref r) % 21) - 10) * 0.05f;
                    var p = zA[i].Pos; p.x += dx; p.z += dz; zA[i].Pos = p; zB[i].Pos = p;
                }
                FireRef(nsA, orderA, nzA, ref mA, (uint)t, cp, npA, sc.Stopped);
                MeleeRef(nsA, nzA, ref mA, cp);
                var grid = new ZombieGrid();
                SoldierLogic.Fire(nsB, orderB, nzB, ref mB, (uint)t, cp, npB, sc.Stopped, ref grid);
                SoldierLogic.Melee(nsB, nzB, ref mB, cp, ref grid);
                grid.Dispose();
                if (!Same(zA, zB, sA, sB, mA, mB, pA, pB)) { shots = mA.ShotsFired; kills = mA.Kills; return false; }
            }
            shots = mA.ShotsFired; kills = mA.Kills;
            return true;
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[26] Índice espacial de disparos y cuerpo a cuerpo (ZombieGrid): igual que recorrer todos los zombis");
            var cp = CombatParams.Default;
            var scenarios = new List<(string, Scenario)>
            {
                ("campo abierto 60×60, 600 zombis", new Scenario { Zombies = 600, Soldiers = 21, Ticks = 400, W = 60, H = 60, OffX = 0, OffZ = 0, Seed = 1, Stopped = true }),
                ("rejilla con posiciones repetidas (empates de distancia)", new Scenario { Zombies = 800, Soldiers = 21, Ticks = 300, W = 60, H = 60, OffX = 5, OffZ = 5, Lattice = true, Seed = 2, Stopped = true }),
                ("tropa a pie, tren en marcha", new Scenario { Zombies = 500, Soldiers = 21, Ticks = 300, W = 40, H = 40, OffX = 100, OffZ = 40, Seed = 3, Stopped = false }),
                ("tropa fuera de la horda (a 100 m)", new Scenario { Zombies = 300, Soldiers = 21, Ticks = 100, W = 20, H = 20, OffX = 300, OffZ = 300, Seed = 4, Stopped = true }),
                ("un solo zombi", new Scenario { Zombies = 1, Soldiers = 21, Ticks = 100, W = 10, H = 10, OffX = 50, OffZ = 50, Seed = 5, Stopped = true }),
                ("sin zombis", new Scenario { Zombies = 0, Soldiers = 21, Ticks = 20, W = 10, H = 10, Seed = 6, Stopped = true }),
                ("rejilla enorme (celdas más grandes), 500 zombis en 4 km", new Scenario { Zombies = 500, Soldiers = 21, Ticks = 100, W = 4000, H = 4000, OffX = 0, OffZ = 0, Seed = 7, Stopped = true }),
                ("1.500 zombis y 80 soldados, 600 ticks", new Scenario { Zombies = 1500, Soldiers = 80, Ticks = 600, W = 50, H = 50, OffX = 10, OffZ = 10, Seed = 8, Stopped = true }),
            };
            foreach (var (name, sc) in scenarios)
            {
                bool ok = Equivalent(sc, cp, out long shots, out int kills);
                check(ok && (sc.Zombies == 0 || sc.Soldiers == 0 || shots >= 0), $"igual que la versión exhaustiva en todos los ticks: {name} ({shots} disparos, {kills} abatidos)");
            }
            {
                Equivalent(scenarios[0].Item2, cp, out long s0, out int k0);
                check(k0 > 20 && s0 > 100, $"el escenario base dispara y abate de verdad ({s0} disparos, {k0} abatidos)");
            }
            {   // distintas semillas
                bool all = true;
                for (uint seed = 100; seed < 130; seed++)
                    all &= Equivalent(new Scenario { Zombies = 200 + (int)seed, Soldiers = 21, Ticks = 120, W = 30 + seed % 20, H = 30 + seed % 13, OffX = 0, OffZ = 0, Seed = seed, Stopped = seed % 3 != 0, Lattice = seed % 4 == 0 }, cp, out _, out _);
                check(all, "30 semillas más (con y sin rejilla repetida, tren parado o en marcha): idénticas");
            }

            // coste: 10.000 zombis, 21 soldados (a pie y a bordo)
            Console.WriteLine("         Coste de Fire + Melee, 10.000 zombis y 21 soldados:");
            foreach (var deployed in new[] { false, true })
            {
                var sc = new Scenario { Zombies = 10000, Soldiers = 21, Ticks = 0, W = 140, H = 90, OffX = 40, OffZ = 10, Seed = 9, Stopped = true };
                Build(sc, out var zs, out var ss);
                for (int i = 0; i < ss.Length; i++) { ss[i].State = deployed ? SoldierState.Deployed : SoldierState.Embarked; ss[i].Pos = new float3(30f + i * 0.5f, 0f, 50f + (i % 3)); ss[i].Hp = 1000000; }
                for (int i = 0; i < zs.Length; i++) if (zs[i].State == ZState.Dead || zs[i].State == ZState.Fall) zs[i].State = ZState.Walk;
                double msRef = Bench(zs, ss, cp, false), msNew = Bench(zs, ss, cp, true);
                Console.WriteLine($"         · {(deployed ? "a pie" : "a bordo")}: antes {msRef:0.000} ms/tick, ahora {msNew:0.000} ms/tick ({msRef / Math.Max(msNew, 1e-9):0.0}×)");
                if (deployed) check(msNew * 1.5 <= msRef, $"con 10.000 zombis y la tropa a pie (cuerpo a cuerpo incluido), el índice es al menos 1,5× más rápido ({msNew:0.000} contra {msRef:0.000} ms/tick)");
                else check(msNew <= 0.1, $"con la tropa a bordo (pocos disparos por tick) el coste es despreciable en cualquier caso ({msNew:0.000} ms/tick, antes {msRef:0.000})");
            }
        }

        static double Bench(Zombie[] zs0, Soldier[] ss0, CombatParams cp, bool indexed)
        {
            const int Ticks = 300;
            double best = double.MaxValue;
            for (int rep = 0; rep < 3; rep++)
            {
                var z = new NativeArray<Zombie>((Zombie[])zs0.Clone());
                var s = new NativeArray<Soldier>((Soldier[])ss0.Clone());
                var pile = new NativeArray<int>(200, Allocator.Temp);
                var order = new NativeArray<int>(ss0.Length, Allocator.Temp);
                SoldierLogic.SortById(s, order);
                var ammo = new ZoneAmmo { ShotsLeft = 10_000_000 };
                var sw = Stopwatch.StartNew();
                for (int t = 0; t < Ticks; t++)
                {
                    if (indexed)
                    {
                        var g = new ZombieGrid();
                        SoldierLogic.Fire(s, order, z, ref ammo, (uint)t, cp, pile, true, ref g);
                        SoldierLogic.Melee(s, z, ref ammo, cp, ref g);
                        g.Dispose();
                    }
                    else { FireRef(s, order, z, ref ammo, (uint)t, cp, pile, true); MeleeRef(s, z, ref ammo, cp); }
                }
                sw.Stop();
                best = Math.Min(best, sw.Elapsed.TotalMilliseconds / Ticks);
            }
            return best;
        }
    }
}
