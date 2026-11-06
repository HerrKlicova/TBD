using System;

namespace TBD.Units
{
    // Vehículos de tierra del jugador (noche del 7-nov). LOCALES: por decisión de Mak no entran en el hash ni en el lockstep del prototipo.
    // Solo el daño que hacen a la horda se aplica como orden dentro de la simulación (VehicleDamage). Aun así va en enteros, para poder
    // llevarlo a la simulación con coma fija en el hito 7 sin reescribirlo. Modelo cinemático: bicicleta para ruedas, giro sobre el sitio para orugas.

    public enum VehicleKind : byte { Lince = 0, Vamtac = 1, Pizarro = 2 }

    public struct VehicleStats
    {
        public string Name;
        public bool Tracked;
        public int MaxFwdMmS, MaxRevMmS;
        public int AccelMmS2, BrakeMmS2, CoastMmS2;   // acelerar, frenar y rodar sin acelerador
        public int WheelbaseMm;                        // distancia entre ejes (ruedas)
        public int MaxSteer;                           // bam16, ángulo máximo de las ruedas delanteras
        public int SteerRatePerTick;                   // bam16 por tick que giran las ruedas
        public int PivotRatePerTick;                   // orugas: bam16 por tick de giro a velocidad cero
        public int Hp;
        public int RamDamage;                          // daño a la horda por embestida a velocidad máxima (se escala con la velocidad)
        public int LengthMm, WidthMm;
    }

    public static class VehicleCatalog
    {
        // Referencias: IVECO LMV «Lince» y URO VAMTAC (ruedas), VCI «Pizarro» (orugas). Valores provisionales de jugabilidad.
        public static VehicleStats[] Table =
        {
            new VehicleStats { Name = "Lince (LMV)", Tracked = false, MaxFwdMmS = 22000, MaxRevMmS = 6000, AccelMmS2 = 4500, BrakeMmS2 = 9000, CoastMmS2 = 1200, WheelbaseMm = 3200, MaxSteer = 4400, SteerRatePerTick = 600, Hp = 400, RamDamage = 150, LengthMm = 5000, WidthMm = 2200 },
            new VehicleStats { Name = "VAMTAC", Tracked = false, MaxFwdMmS = 19000, MaxRevMmS = 5500, AccelMmS2 = 3800, BrakeMmS2 = 8000, CoastMmS2 = 1200, WheelbaseMm = 3300, MaxSteer = 4000, SteerRatePerTick = 550, Hp = 350, RamDamage = 130, LengthMm = 4900, WidthMm = 2200 },
            new VehicleStats { Name = "Pizarro", Tracked = true, MaxFwdMmS = 14000, MaxRevMmS = 4000, AccelMmS2 = 2500, BrakeMmS2 = 6000, CoastMmS2 = 2000, WheelbaseMm = 4000, MaxSteer = 0, PivotRatePerTick = 700, Hp = 1200, RamDamage = 300, LengthMm = 6800, WidthMm = 3200 },
        };
        public static VehicleStats Get(VehicleKind k) { return Table[(int)k]; }
    }

    public sealed class Vehicle
    {
        public int Id;
        public VehicleKind Kind;
        public int X, Y, Heading;                  // mm, bam16
        public int SpeedMmS;                       // con signo, a lo largo del rumbo
        public int Steer;                          // ángulo actual de las ruedas (bam16, con signo; positivo = a la izquierda)
        public int Hp;
        // mandos, −1000..1000 (freno 0..1000)
        public int Throttle, SteerInput, Brake;

        public Vehicle(int id, VehicleKind kind, int x, int y, int heading)
        { Id = id; Kind = kind; X = x; Y = y; Heading = heading & 0xFFFF; Hp = VehicleCatalog.Get(kind).Hp; }

        public void Controls(int throttle, int steer, int brake)
        { Throttle = IMath.Clamp(throttle, -1000, 1000); SteerInput = IMath.Clamp(steer, -1000, 1000); Brake = IMath.Clamp(brake, 0, 1000); }

        /// <summary>Velocidad lateral aparente para la animación de ruedas y el balanceo: la rotación de la caja en bam16 por tick.</summary>
        public int YawPerTick;

        public void Step()
        {
            var s = VehicleCatalog.Get(Kind);
            if (Hp <= 0) { Throttle = 0; Brake = 1000; }
            // longitudinal
            int target = Throttle >= 0 ? (int)((long)Throttle * s.MaxFwdMmS / 1000) : (int)((long)Throttle * s.MaxRevMmS / 1000);
            int accel = s.AccelMmS2 / IMath.TickRate, brake = s.BrakeMmS2 / IMath.TickRate, coast = s.CoastMmS2 / IMath.TickRate;
            if (Brake > 0)
            {
                SpeedMmS = IMath.Approach(SpeedMmS, 0, Math.Max(1, brake * Brake / 1000));
            }
            else if (Throttle == 0) SpeedMmS = IMath.Approach(SpeedMmS, 0, coast);
            else if ((target > 0 && SpeedMmS < 0) || (target < 0 && SpeedMmS > 0)) SpeedMmS = IMath.Approach(SpeedMmS, 0, brake);   // cambiar de sentido: primero frena
            else SpeedMmS = IMath.Approach(SpeedMmS, target, Math.Abs(target) >= Math.Abs(SpeedMmS) ? accel : coast + accel / 2);

            int yaw;
            if (s.Tracked)
            {
                // giro de orugas: sobre el sitio parado, más lento cuanto más rápido va
                int scale = 1000 - (int)(600L * Math.Abs(SpeedMmS) / Math.Max(1, s.MaxFwdMmS));
                yaw = SteerInput * s.PivotRatePerTick / 1000 * scale / 1000;
                if (SpeedMmS < 0) yaw = -yaw;      // al dar marcha atrás el giro se invierte como en un coche
                Steer = 0;
            }
            else
            {
                int lim = s.MaxSteer * (1000 - (int)(550L * Math.Abs(SpeedMmS) / Math.Max(1, s.MaxFwdMmS))) / 1000;   // menos volante a más velocidad
                int wanted = SteerInput * lim / 1000;
                Steer = IMath.Approach(Steer, wanted, s.SteerRatePerTick);
                if (Steer > lim) Steer = lim; else if (Steer < -lim) Steer = -lim;
                int c = IMath.Cos(Steer);
                // velocidad de guiñada = v · tan(δ) / L  (rad/s) → bam16 por tick: · 65536 / (2π · 30) = · 347,679
                yaw = c == 0 ? 0 : IMath.DivR((long)SpeedMmS * IMath.Sin(Steer) * 347679, (long)s.WheelbaseMm * c * 1000);
            }
            YawPerTick = yaw;
            Heading = (Heading + yaw) & 0xFFFF;
            // avance con el rumbo medio del tick
            int hm = (Heading - yaw / 2) & 0xFFFF;
            X += IMath.DivR((long)SpeedMmS * IMath.Cos(hm), (long)IMath.One * IMath.TickRate);
            Y += IMath.DivR((long)SpeedMmS * IMath.Sin(hm), (long)IMath.One * IMath.TickRate);
        }

        /// <summary>Daño a la horda por embestida: crece con la velocidad (cuadrática suave) y es 0 por debajo de 4 m/s. Es lo único que entra como orden.</summary>
        public int RamDamage()
        {
            var s = VehicleCatalog.Get(Kind);
            int v = Math.Abs(SpeedMmS);
            if (v < 4000) return 0;
            return (int)((long)s.RamDamage * v / s.MaxFwdMmS);
        }

        /// <summary>Zombis (ids) atropellados este tick: dentro de un rectángulo delante del vehículo. El que llama aplica RamDamage como orden.</summary>
        public static bool InRam(Vehicle v, int px, int py)
        {
            var s = VehicleCatalog.Get(v.Kind);
            int dx = px - v.X, dy = py - v.Y, c = IMath.Cos(v.Heading), sn = IMath.Sin(v.Heading);
            long fwd = ((long)dx * c + (long)dy * sn) >> 16, side = ((long)dx * sn - (long)dy * c) >> 16;
            return fwd >= 0 && fwd <= s.LengthMm / 2 + 700 && Math.Abs(side) <= s.WidthMm / 2 + 150;
        }
    }
}
