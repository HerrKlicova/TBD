using System;
using System.Collections.Generic;
using System.IO;
using TBD.Net;
using TBD.Sim;
using Unity.Collections;
using Unity.Mathematics;

namespace TBD.Sim.Tests
{
    /// <summary>
    /// Hito 3, parte 4: combate de tropas en la zona. P7 de Hugo: 20 fusileros y 1 ametralladora, sin muros, contra 2.000 comunes.
    /// El modelo S0 predice 5 u (2.000 / 400); aceptable entre 4 y 6 u; por encima de 6,7 u la economía se rompe.
    /// </summary>
    static class CombatTests
    {
        public struct P7Result { public long Shots; public int Kills, Lost, AliveZombies, Ticks; public double Units; }

        public static P7Result RunP7(CombatParams cp, int zombies = 2000, uint seed = 2024u)
        {
            var w = new MiniWorld(new List<Solid>(), 64f, 64f);                 // campo abierto, la horda va a por la tropa
            w.Spawn(zombies, 4f, 34f, 24f, 104f, 0f, seed);                       // entre 30 y 60 m al oeste
            var soldiers = new Soldier[21];
            uint id = 1_000_000;
            for (int i = 0; i < 20; i++)
                soldiers[i] = new Soldier { SimId = id++, Pos = new float3(62f + (i % 5) * 1.5f, 0f, 61f + (i / 5) * 1.5f), Hp = cp.SoldierHp, Kind = SoldierKind.Rifle, State = SoldierState.Deployed };
            soldiers[20] = new Soldier { SimId = id++, Pos = new float3(65f, 0f, 64f), Hp = cp.SoldierHp, Kind = SoldierKind.MachineGun, State = SoldierState.Deployed };
            var sArr = new NativeArray<Soldier>(soldiers);
            var order = new NativeArray<int>(soldiers.Length, Allocator.Temp);
            SoldierLogic.SortById(sArr, order);
            var ammo = new ZoneAmmo { ShotsLeft = 100L * cp.ShotsPerUnit };     // munición de sobra: se mide lo que se GASTA
            int t = 0;
            for (; t < 30 * 60 * 5; t++)                                          // hasta 5 minutos de combate
            {
                w.Tick1();
                var zArr = new NativeArray<Zombie>(w.Z);
                SoldierLogic.Fire(sArr, order, zArr, ref ammo, (uint)t, cp, w.Delta);
                SoldierLogic.Melee(sArr, zArr, ref ammo, cp);
                int alive = 0; foreach (var z in w.Z) if (z.State != ZState.Dead) alive++;
                int standing = 0; foreach (var s in soldiers) if (s.State == SoldierState.Deployed) standing++;
                if (alive == 0 || standing == 0) { t++; break; }
            }
            int left = 0; foreach (var z in w.Z) if (z.State != ZState.Dead) left++;
            return new P7Result { Shots = ammo.ShotsFired, Kills = ammo.Kills, Lost = ammo.SoldiersLost, AliveZombies = left, Ticks = t, Units = ammo.ShotsFired / (double)cp.ShotsPerUnit };
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[12] Tropas en la zona (hito 3, parte 4) y P7 de Hugo");
            var cp = CombatParamsCsv.Load(File.ReadAllText(Path.Combine(BalanceTests.DataDirPublic(), "balance_combate.csv")));
            check(cp.RifleCooldown > 0 && cp.MgCooldown > 0 && cp.ShotsPerUnit == 2000, $"balance_combate.csv carga (cadencias {cp.RifleCooldown} y {cp.MgCooldown} ticks, 2.000 disparos por u)");

            // determinismo: dos ejecuciones iguales, mismo resultado
            var a = RunP7(cp, 400, 7u);
            var b = RunP7(cp, 400, 7u);
            check(a.Shots == b.Shots && a.Kills == b.Kills && a.Lost == b.Lost && a.Ticks == b.Ticks, $"el combate es determinista ({a.Shots} disparos, {a.Kills} abatidos, {a.Lost} bajas, en ambas ejecuciones)");
            check(a.Kills > 0 && a.Shots >= a.Kills, "se dispara y se abate (sin objetivo no se dispara)");

            // P7
            var p = RunP7(cp);
            Console.WriteLine($"         P7: {p.Shots} disparos = {p.Units:0.00} u · abatidos {p.Kills} de 2000 · quedan {p.AliveZombies} · bajas propias {p.Lost} de 21 · {p.Ticks / 30.0:0} s de combate · {(p.Kills > 0 ? p.Shots / (double)p.Kills : 0):0.0} disparos por abatido");
            double spk = p.Kills > 0 ? p.Shots / (double)p.Kills : 0;
            check(p.Units <= 6.7, $"P7: la zona no gasta más de 6,7 u contra 2.000 comunes sin muros ({p.Units:0.00} u)");
            check(p.Kills >= 1000 && p.Kills <= 1300 && spk >= 4 && spk <= 6 && p.Units >= 2.5 && p.Units <= 3.9,
                $"P7 en el rango de Hugo (D-N28): abatidos {p.Kills} (1.000-1.300), {spk:0.0} disparos por abatido (4-6), {p.Units:0.00} u (2,5-3,9)");
        }
    }
}
