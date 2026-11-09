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

            {   // D-N181: al edificio solo le cuenta quien va a por él
                var g = Make(true).Night; var sr = g.P.StationRect;
                var zs = new List<Target> { new Target { Id = 0, X = (sr.X0 + sr.X1) / 2, Y = sr.Y0 - 500 }, new Target { Id = 1, X = (sr.X0 + sr.X1) / 2, Y = sr.Y1 + 500 } };
                int s0, t0n, s1, t1n, s2, t2n; g.CountAttackers(zs, out s0, out t0n); g.CountAttackers(zs, i => i == 1, out s1, out t1n); g.CountAttackers(zs, null, out s2, out t2n);
                check(s0 == 2 && s1 == 1 && s2 == 2 && t0n == t1n, $"D-N181: dos zombis pegados a la fachada; cuentan para la estación {s0} sin filtro, {s1} si solo uno la tiene de objetivo, {s2} con filtro nulo");
            }
            {   // D-N182: la horda nace 150 m más lejos y antes; al muro llega igual (el azar de la colocación se reparte en otro orden: se compara el reparto, no zombi a zombi)
                var n0 = Make(false); var n1 = Make(false); n1.SpawnLeadMm = 150000;
                var l0 = new List<Target>(); var l1 = new List<Target>(); int minX = int.MaxValue; var sb = new System.Text.StringBuilder(); bool same = true;
                for (int k = 1; k <= 90 * IMath.TickRate; k++)
                {
                    n0.Step(); n1.Step(); if (k == 1) { n1.ExportZombies(l1); foreach (var z in l1) minX = Math.Min(minX, z.X); }
                    if (k % (15 * IMath.TickRate) != 0) continue;
                    n0.ExportZombies(l0); n1.ExportZombies(l1);
                    int c0 = 0, c1 = 0; long s0 = 0, s1 = 0; foreach (var z in l0) if (z.X >= 10000) { c0++; s0 += z.X; } foreach (var z in l1) if (z.X >= 10000) { c1++; s1 += z.X; }
                    long m0 = c0 > 0 ? s0 / c0 : 0, m1 = c1 > 0 ? s1 / c1 : 0;
                    same &= Math.Abs(c0 - c1) * 100 <= Math.Max(20, c0 * 3) && Math.Abs(m0 - m1) <= 2000;
                    sb.Append($" {k / IMath.TickRate} s: {c0}/{c1} a {m0 / 1000}/{m1 / 1000} m;");
                }
                check(minX < -100000 && same, $"D-N182: con 150 m de adelanto nacen fuera (x mín. {minX / 1000} m) y pasan los 10 m a la vez (sin/con adelanto, zombis y x media):{sb}");
            }
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
            var S = new RectMm(98000, 75000, 114000, 83000); int m = 2000;
            int tx = 110000, ty = 64000; bool r1 = ClimberTargets.Rodeo(104000, 86000, S, m, ref tx, ref ty);
            check(r1 && tx == 95999 && ty == 86000, $"rodeo: detrás del edificio, con el tren al otro lado, sale por la franja de detrás hacia la esquina más cercana ({tx}; {ty})");
            int ax = tx, ay = ty; int t2x = 110000, t2y = 64000; bool r4 = ClimberTargets.Rodeo(ax, ay, S, m, ref t2x, ref t2y);
            check(!r4 && t2x == 110000, "rodeo: al llegar a su punto de paso ya no se aplica y sigue hacia el tren (no se queda parado)");
            tx = 110000; ty = 64000; bool r2 = ClimberTargets.Rodeo(95000, 86000, S, m, ref tx, ref ty);
            bool r3 = ClimberTargets.Rodeo(104000, 74000, S, m, ref tx, ref ty);
            check(!r2 && !r3 && tx == 110000 && ty == 64000, "rodeo: fuera de la sombra del edificio (por X o por Y) vuelve a su objetivo; sin estado, no se queda en bucle");
        }
    }
}
