using System;
using System.Collections.Generic;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>[33] Línea de visión (propuesta de Nacho, 8-nov): un sólido opaco tapa el disparo de tierra; sin sólidos, todo igual que antes.</summary>
    public static class LosTests
    {
        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[33] Línea de visión");
            var r = new RectMm(10_000, 10_000, 20_000, 20_000);
            check(UnitsWorld.SegHitsRect(0, 15_000, 30_000, 15_000, r) && UnitsWorld.SegHitsRect(0, 0, 30_000, 30_000, r), "una línea que atraviesa el sólido queda tapada (recta y en diagonal)");
            check(!UnitsWorld.SegHitsRect(0, 0, 30_000, 5_000, r) && !UnitsWorld.SegHitsRect(0, 21_000, 30_000, 25_000, r) && !UnitsWorld.SegHitsRect(0, 15_000, 9_000, 15_000, r), "pasar por fuera o quedarse antes no tapa");
            check(!UnitsWorld.SegHitsRect(15_000, 15_000, 40_000, 15_000, r), "un tirador dentro del sólido (en el tejado) no se tapa a sí mismo");
            check(UnitsWorld.SegHitsRect(0, 10_000, 30_000, 10_000, r), "rozar el borde cuenta como tapado");
            var w = new UnitsWorld(5); var p = w.AddPlatoon(PlatoonKind.Fusileros, 0, 15_000, 0);
            var wpn = WeaponCatalog.Table[(int)WeaponId.Fusil];
            var tg = new List<Target> { new Target { Id = 1, X = 25_000, Y = 15_000 }, new Target { Id = 2, X = 0, Y = 42_000 } };
            int a = w.PickAt(wpn, 0, 15_000, 0, 0, tg, 0);
            w.LosBlockers.Add(new UnitsWorld.LosBlocker { R = r, HeightMm = 8000 }); int b = w.PickAt(wpn, 0, 15_000, 0, 0, tg, 0); int c = w.PickAt(wpn, 0, 15_000, 0, 0, tg, 5000);
            check(a == 0 && b == 1 && c == 0, $"con el sólido delante, el fusil elige el blanco que ve ({a} → {b}); desde el aire (extraZ) sigue viendo el primero");
            w.LosBlockers.Clear(); w.LosBlockers.Add(new UnitsWorld.LosBlocker { R = r, HeightMm = 1200 });
            // 2,5D (Álvaro): sacos de 1,2 m entre x 10 y 20 m; boca a 1,6 m y blanco a 1,0 m. El rayo baja 0,6 m en toda su longitud: pasa por encima mientras el sacos quede en el primer 2/3
            var sac = new RectMm(10_000, 0, 11_000, 30_000); w.LosBlockers.Clear(); w.LosBlockers.Add(new UnitsWorld.LosBlocker { R = sac, HeightMm = 1200 });
            check(w.LineOfSight(0, 15_000, 25_000, 15_000) && !w.LineOfSight(0, 15_000, 15_000, 15_000), "2,5D: los sacos de 1,2 m no tapan un blanco lejano (a 25 m) pero sí uno pegado detrás (a 15 m)");
            check(w.LineOfSight(0, 15_000, 15_000, 15_000, 2_400), "con la boca más alta (VAMTAC, 2,4 m) se ve también el pegado");
            var wall = new RectMm(2_000, 0, 3_000, 30_000); w.LosBlockers.Clear(); w.LosBlockers.Add(new UnitsWorld.LosBlocker { R = wall, HeightMm = 2100, OwnWall = true });
            check(w.LineOfSight(0, 15_000, 25_000, 15_000) && !w.LineOfSight(-1_000, 15_000, 25_000, 15_000), "D-N174 (2): el muro propio no tapa a quien está a 2 m o menos de él; a 3 m, sí");
            var bl = w.LosBlockers[0]; bl.Off = true; w.LosBlockers[0] = bl; check(w.LineOfSight(-5_000, 15_000, 25_000, 15_000), "un tramo caído (brecha) ya no tapa");

            // D-N173 (Lucía): sin helipuerto; el NH90 sale del aeródromo y vuelve allí a rearmar
            var sim = NightSim.FromJson(TBD.Net.BalanceSource.Read("noche_sagunto.json"), "normal", 3); sim.AddDefaultForce(); sim.AddDefaultVehicles(true); sim.Bot = NightBot.FromParams(sim.SP);
            sim.Nh90StartAtAirfield(); var nh = sim.Vehicles.Find(v => v.Kind == NightVehKind.Nh90); int trip = sim.AirfieldTripTicks();
            check(nh.AtAirfield && !nh.Airborne && trip == (int)((40000L * 3600 / 1000 / 260) * 30 / sim.Night.P.ClockX), $"el NH90 empieza en el aeródromo; el viaje de 40 km (SUPUESTO) a 260 km/h son {trip} ticks a reloj ×{sim.Night.P.ClockX}");
            int launched = -1, arrived = -1;
            for (int t = 0; t < 30 * 600 && arrived < 0; t++) { sim.Run(1); if (launched < 0 && nh.AwayUntil >= 0) launched = sim.Night.Tick; if (nh.Airborne) arrived = sim.Night.Tick; }
            check(launched > 0 && arrived - launched == trip + 20 * 30, $"despega del aeródromo con horda: 20 s de preparar más el viaje (sale {launched}, llega {arrived})");
            nh.Shots = nh.MaxShots / 5; int t0 = sim.Night.Tick; for (int t = 0; t < 600 && nh.Airborne; t++) sim.Run(1);
            check(nh.AtAirfield && nh.Parked && nh.AutoReturns == 1 && nh.ResupplyUntil >= t0 + trip + 50 * 30, $"D-N167 enmendada: por debajo del 30 % vuelve al aeródromo, se queda allí y rearma al llegar (listo en el tick {nh.ResupplyUntil})");
            check(sim.TakeOffOrder(sim.Vehicles.IndexOf(nh)) && nh.AwayUntil == sim.Night.Tick + 20 * 30 + trip && !nh.Parked, "con una orden sale de nuevo y tarda el viaje");
        }
    }
}
