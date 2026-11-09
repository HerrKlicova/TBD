using System;
using System.Collections.Generic;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>[39] D-N180 (Lucía, 9-nov): el tren sin escolta rodeado se pierde en un tiempo acotado, y la horda sin objetivo vuelve al edificio: la noche siempre acaba.</summary>
    public static class TrenRodeadoTests
    {
        static NightSim Make(bool trainOutside)
        {
            var sim = NightSim.FromJson(TBD.Net.BalanceSource.Read("noche_sagunto.json"), "normal", 3);
            if (trainOutside) { var p = sim.Night.P; p.TrainRect = new RectMm(sim.SP.FortX0 - 70000, p.TrainRect.Y0, sim.SP.FortX0 - 6000, p.TrainRect.Y1); sim.Night.P = p; }
            return sim;
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[39] D-N180: tren rodeado sin tropa y horda sin objetivo");
            var a = Make(true); var r = a.Night.P.TrainRect;
            for (int k = 0; k < 4; k++) a.ImportZombie(r.X0 + 5000 + k * 10000, r.Y1 + 500);
            long lossTicks = (long)a.SurroundLossGameMin * 60 * IMath.TickRate / a.Night.P.ClockX;
            int t0 = a.Night.Tick; while (a.Night.TrainHp > 0 && a.Night.Tick < t0 + 4 * lossTicks) a.Step();
            int took = a.Night.Tick - t0;
            check(a.Night.TrainHp == 0 && took <= lossTicks + 2 && a.SurroundedTicks > 0, $"4 zombis en un tren sin tropa: se pierde en {took} ticks ({took * a.Night.P.ClockX / (double)IMath.TickRate / 60:0.0} min de juego; tope {a.SurroundLossGameMin} min = {lossTicks} ticks)");

            var b = Make(true); var rb = b.Night.P.TrainRect;
            for (int k = 0; k < 4; k++) b.ImportZombie(rb.X0 + 5000 + k * 10000, rb.Y1 + 500);
            var esc = b.Units.AddPlatoon(PlatoonKind.Fusileros, rb.X0 + 30000, rb.Y1 + 40000, 0); esc.HoldFire = true;      // a 40 m, sin disparar: solo cuenta que está a su alcance
            int tb = b.Night.Tick; while (b.Night.Tick < tb + lossTicks + 30) b.Step();
            check(b.Night.TrainHp > 0 && b.SurroundedTicks == 0, $"con un pelotón propio a 40 m no cuenta como rodeado: el tren sigue con {b.Night.TrainHp * 100 / b.Night.P.TrainHp} % de vida");

            var c = Make(true); var st = c.Night.P.StationRect;
            int zi = c.ImportZombie(c.SP.FortX0 + 3000, c.SP.FortY0 + 3000);
            var buf = new List<Target>(); Func<long> dist = () => { c.ExportZombies(buf); foreach (var z in buf) if (z.Id == zi) { long dx = Math.Max(0, Math.Max(st.X0 - z.X, z.X - st.X1)), dy = Math.Max(0, Math.Max(st.Y0 - z.Y, z.Y - st.Y1)); return (long)Math.Sqrt(dx * dx + dy * dy); } return -1; };
            long d0 = dist(); for (int k = 0; k < 10 * IMath.TickRate; k++) c.Step(); long d1 = dist();
            check(!c.TrainReachableFromInside() && d1 >= 0 && d1 < d0 - 5000, $"con el tren fuera del recinto, el que ha trepado sin nadie cerca va al edificio: de {d0 / 1000} m a {d1 / 1000} m en 10 s");

            var d = Make(false); int zd = d.ImportZombie(d.SP.FortX0 + 3000, d.SP.FortY0 + 3000);
            check(d.TrainReachableFromInside(), "con el tren dentro y entero, sigue yendo al tren (D-N148, sin cambios)");
        }
    }
}
