using System;
using System.Collections.Generic;
using TBD.Net;

namespace TBD.Units
{
    // Vehículos de tierra del jugador (noche del 7-nov). LOCALES: por decisión de Mak no entran en el hash ni en el lockstep del prototipo.
    // Solo el daño que hacen a la horda se aplica como orden dentro de la simulación (VehicleDamage). Aun así va en enteros, para poder
    // llevarlo a la simulación con coma fija en el hito 7 sin reescribirlo. Modelo cinemático: bicicleta para ruedas, giro sobre el sitio para orugas.

    public enum VehicleKind : byte { Lince = 0, Vamtac = 1, Pizarro = 2, Dragon = 3 }

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
        // Datos de Hugo (balance_unidades.csv, filas vehiculo_<clave>_*; D-N76, D-N96, D-N101):
        public string Key;
        public int ArmorPm;                            // ‰ del daño de los zombis que se ignora
        public int HordeSpeedMmS;                      // máxima a través de horda suelta (≥ 0,5 zombis/m²); 0 = sin límite
        public int BlockPile;                          // pila de zombis delante a partir de la cual se para (0 = no se para)
        public int MaxYawPerTick;                      // tope de giro del casco (bam16 por tick); 0 = sin tope
        public int Seats;                              // plazas de infantería
        public int AmmoBoardMilliU;                    // munición de a bordo, en milésimas de u
        public int FuelMarchE4s, FuelIdleE4s, TankU;   // combustible: E4 de u por segundo real en marcha y al ralentí; depósito en u
        public int RunOverMinMmS, RunOverPm;           // atropello: velocidad mínima y ‰ de abatir a cada zombi que pisa
        public WeaponId[] Mounts;                      // armas de la torreta, en orden de prioridad
        public int TurretRatePerTick;                  // giro de la torreta, bam16 por tick
    }

    public static class VehicleCatalog
    {
        // Referencias: IVECO LMV «Lince» (provisional, no está en el CSV), URO VAMTAC ST5, VCR 8×8 «Dragón» (ruedas) y VCI «Pizarro» (orugas). Los valores de VAMTAC, Dragón y Pizarro
        // son los del CSV de Hugo (una prueba comprueba que coinciden); lo demás (geometría, frenos, ruedas) son valores de jugabilidad míos.
        public static VehicleStats[] Table = Defaults();

        static int YawTick(int cdegS) { return (int)((long)cdegS * 65536 / (36000L * IMath.TickRate)); }

        static VehicleStats[] Defaults()
        {
            return new[]
            {
                new VehicleStats { Key = "lince", Name = "Lince (LMV)", Tracked = false, MaxFwdMmS = 22000, MaxRevMmS = 6000, AccelMmS2 = 4500, BrakeMmS2 = 9000, CoastMmS2 = 1200, WheelbaseMm = 3200, MaxSteer = 4400, SteerRatePerTick = 600, Hp = 400, RamDamage = 150, LengthMm = 5000, WidthMm = 2200,
                    TankU = 4, FuelMarchE4s = 90, FuelIdleE4s = 25, Seats = 4, Mounts = new WeaponId[0], TurretRatePerTick = 800 },
                new VehicleStats { Key = "vamtac", Name = "VAMTAC", Tracked = false, MaxFwdMmS = 19000, MaxRevMmS = 5500, AccelMmS2 = 3800, BrakeMmS2 = 8000, CoastMmS2 = 1200, WheelbaseMm = 3300, MaxSteer = 4000, SteerRatePerTick = 550, Hp = 2000, RamDamage = 130, LengthMm = 4900, WidthMm = 2200,
                    ArmorPm = 950, HordeSpeedMmS = 3000, BlockPile = 2, MaxYawPerTick = YawTick(5000), Seats = 4, AmmoBoardMilliU = 1000, FuelMarchE4s = 90, FuelIdleE4s = 25, TankU = 4, RunOverMinMmS = 3000, RunOverPm = 300,
                    Mounts = new[] { WeaponId.MgPesada }, TurretRatePerTick = 900 },
                new VehicleStats { Key = "pizarro", Name = "Pizarro", Tracked = true, MaxFwdMmS = 14000, MaxRevMmS = 4000, AccelMmS2 = 2500, BrakeMmS2 = 6000, CoastMmS2 = 2000, WheelbaseMm = 4000, MaxSteer = 0, PivotRatePerTick = 700, Hp = 5000, RamDamage = 300, LengthMm = 6800, WidthMm = 3200,
                    ArmorPm = 993, HordeSpeedMmS = 5000, BlockPile = 6, MaxYawPerTick = YawTick(4000), Seats = 8, AmmoBoardMilliU = 2500, FuelMarchE4s = 250, FuelIdleE4s = 80, TankU = 8, RunOverMinMmS = 2000, RunOverPm = 700,
                    Mounts = new[] { WeaponId.Canon30, WeaponId.MgLigera }, TurretRatePerTick = 600 },
                new VehicleStats { Key = "dragon", Name = "Dragón (VCR 8×8)", Tracked = false, MaxFwdMmS = 12000, MaxRevMmS = 5000, AccelMmS2 = 3000, BrakeMmS2 = 7000, CoastMmS2 = 1500, WheelbaseMm = 4800, MaxSteer = 3000, SteerRatePerTick = 450, Hp = 4000, RamDamage = 450, LengthMm = 8000, WidthMm = 3000,
                    ArmorPm = 990, HordeSpeedMmS = 4000, BlockPile = 3, MaxYawPerTick = YawTick(4500), Seats = 8, AmmoBoardMilliU = 2000, FuelMarchE4s = 200, FuelIdleE4s = 60, TankU = 8, RunOverMinMmS = 3000, RunOverPm = 500,
                    Mounts = new[] { WeaponId.Canon30, WeaponId.MgLigera }, TurretRatePerTick = 700 },
            };
        }

        public static VehicleStats Get(VehicleKind k) { return Table[(int)k]; }

        /// <summary>Lee las filas vehiculo_&lt;clave&gt;_* de balance_unidades.csv; lo que falte queda con el valor por defecto.</summary>
        public static void Load(CsvTable t)
        {
            for (int i = 0; i < Table.Length; i++)
            {
                var s = Table[i]; string k = "vehiculo_" + s.Key + "_";
                if (s.Key == "lince") continue;
                s.Hp = (int)t.TryValue(k + "vida", s.Hp); s.ArmorPm = (int)t.TryValue(k + "armadura", s.ArmorPm);
                s.MaxFwdMmS = (int)t.TryValue(k + "vel_mm_s", s.MaxFwdMmS); s.HordeSpeedMmS = (int)t.TryValue(k + "vel_horda_mm_s", s.HordeSpeedMmS);
                s.AccelMmS2 = (int)t.TryValue(k + "acel_mm_s2", s.AccelMmS2);
                s.MaxYawPerTick = YawTick((int)t.TryValue(k + "giro_cdeg_s", s.MaxYawPerTick * 36000L * IMath.TickRate / 65536));
                s.BlockPile = (int)t.TryValue(k + "bloqueo_pila", s.BlockPile); s.Seats = (int)t.TryValue(k + "plazas", s.Seats);
                s.AmmoBoardMilliU = (int)t.TryValue(k + "municion_bordo", s.AmmoBoardMilliU);
                s.FuelMarchE4s = (int)t.TryValue(k + "comb_marcha_e4_s", s.FuelMarchE4s); s.FuelIdleE4s = (int)t.TryValue(k + "comb_ralenti_e4_s", s.FuelIdleE4s);
                s.TankU = (int)t.TryValue(k + "deposito_u", s.TankU);
                s.RunOverMinMmS = (int)t.TryValue(k + "atropello_vel_min_mm_s", s.RunOverMinMmS); s.RunOverPm = (int)t.TryValue(k + "atropello_permil", s.RunOverPm);
                if (s.Tracked) s.PivotRatePerTick = s.MaxYawPerTick;
                Table[i] = s;
            }
        }

        public static void Reset() { Table = Defaults(); }
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
        // combustible, entorno, armas y pasajeros (todo local, fuera del hash)
        public int FuelE4;                          // E4 de u en el depósito
        public bool EngineOn = true;
        public long FuelUsedE4;                     // acumulado, para el informe
        public int PileAhead;                       // zombis apilados delante (lo pone la zona cada tick)
        public bool InHorde;                        // a través de horda suelta (≥ 0,5 zombis/m²)
        public int AmmoShots;                       // munición de a bordo en disparos equivalentes
        public int TurretYaw;                       // rumbo absoluto de la torreta (bam16)
        public int[] MountCd;
        int _fuelRem, _dmgRem;
        public int CarrierId { get { return Id; } }

        public Vehicle(int id, VehicleKind kind, int x, int y, int heading)
        {
            Id = id; Kind = kind; X = x; Y = y; Heading = heading & 0xFFFF; var s = VehicleCatalog.Get(kind); Hp = s.Hp;
            FuelE4 = s.TankU * 10_000; AmmoShots = (int)((long)s.AmmoBoardMilliU * WeaponCatalog.ShotsPerU / 1000); TurretYaw = Heading; MountCd = new int[s.Mounts == null ? 0 : s.Mounts.Length];
        }

        public bool Disabled { get { return Hp <= 0; } }
        public bool OutOfFuel { get { return FuelE4 <= 0; } }
        /// <summary>Aviso de combustible bajo (menos del 20 % del depósito).</summary>
        public bool LowFuel { get { return FuelE4 * 5 < VehicleCatalog.Get(Kind).TankU * 10_000; } }
        public int FuelPm { get { int tank = VehicleCatalog.Get(Kind).TankU * 10_000; return tank == 0 ? 0 : (int)((long)FuelE4 * 1000 / tank); } }
        /// <summary>Echa combustible (E4 de u) hasta llenar el depósito. Devuelve lo que ha entrado de verdad (la economía descuenta solo eso).</summary>
        public int Refuel(int e4) { int room = VehicleCatalog.Get(Kind).TankU * 10_000 - FuelE4; int n = Math.Max(0, Math.Min(e4, room)); FuelE4 += n; return n; }
        /// <summary>El motor se apaga a mano: sin consumo ni ralentí. Solo se puede encender si queda combustible.</summary>
        public void SetEngine(bool on) { EngineOn = on && FuelE4 > 0; }

        /// <summary>Contacto con la horda: «touching» zombis pegados quitan dmgPerTick cada uno, menos la armadura (en ‰). Resto exacto. Devuelve la vida que queda.</summary>
        public int TakeContact(int touching, int dmgPerTick)
        {
            if (touching <= 0 || Hp <= 0) return Hp;
            long acc = _dmgRem + (long)touching * dmgPerTick * (1000 - VehicleCatalog.Get(Kind).ArmorPm);
            Hp = (int)Math.Max(0, Hp - acc / 1000); _dmgRem = (int)(acc % 1000);
            return Hp;
        }


        public void Controls(int throttle, int steer, int brake)
        { Throttle = IMath.Clamp(throttle, -1000, 1000); SteerInput = IMath.Clamp(steer, -1000, 1000); Brake = IMath.Clamp(brake, 0, 1000); }

        /// <summary>Velocidad lateral aparente para la animación de ruedas y el balanceo: la rotación de la caja en bam16 por tick.</summary>
        public int YawPerTick;

        public void Step()
        {
            var s = VehicleCatalog.Get(Kind);
            // combustible: en marcha (acelerador o rodando) o al ralentí; se cobra en E4 de u por segundo real con resto exacto; apagado, nada
            if (EngineOn)
            {
                int rate = (Throttle != 0 || Math.Abs(SpeedMmS) > 200) ? s.FuelMarchE4s : s.FuelIdleE4s;
                _fuelRem += rate; int use = _fuelRem / IMath.TickRate; _fuelRem %= IMath.TickRate;
                if (use > 0) { use = Math.Min(use, FuelE4); FuelE4 -= use; FuelUsedE4 += use; }
                if (FuelE4 <= 0) { FuelE4 = 0; EngineOn = false; }
            }
            if (Hp <= 0 || !EngineOn) { Throttle = 0; Brake = 1000; }          // sin motor o destruido: se queda inmóvil (las armas siguen)
            // longitudinal
            int cap = InHorde && s.HordeSpeedMmS > 0 ? s.HordeSpeedMmS : s.MaxFwdMmS;
            int target = Throttle >= 0 ? (int)((long)Throttle * cap / 1000) : (int)((long)Throttle * s.MaxRevMmS / 1000);
            if (s.BlockPile > 0 && PileAhead >= s.BlockPile && target > 0) target = 0;       // encajado en la pila: no empuja
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
            if (s.MaxYawPerTick > 0) yaw = IMath.Clamp(yaw, -s.MaxYawPerTick, s.MaxYawPerTick);
            YawPerTick = yaw;
            Heading = (Heading + yaw) & 0xFFFF;
            // avance con el rumbo medio del tick
            int hm = (Heading - yaw / 2) & 0xFFFF;
            if (s.BlockPile > 0 && PileAhead >= s.BlockPile && SpeedMmS > 0) SpeedMmS = 0;       // la pila lo para en seco
            X += IMath.DivR((long)SpeedMmS * IMath.Cos(hm), (long)IMath.One * IMath.TickRate);
            Y += IMath.DivR((long)SpeedMmS * IMath.Sin(hm), (long)IMath.One * IMath.TickRate);
        }

        /// <summary>Atropello (Hugo): a ≥ RunOverMinMmS cada zombi que cae bajo el vehículo se abate con probabilidad RunOverPm ‰ (tirada por hash de vehículo, zombi y tick: sin estado).
        /// Rellena «killed» con los ids abatidos y devuelve cuántos; el que llama los aplica como orden. Por debajo de la velocidad mínima solo empuja.</summary>
        public int RunOver(IList<Target> zombies, int tick, List<int> killed)
        {
            killed.Clear();
            var s = VehicleCatalog.Get(Kind);
            if (Hp <= 0 || zombies == null || s.RunOverPm <= 0 || Math.Abs(SpeedMmS) < s.RunOverMinMmS) return 0;
            for (int i = 0; i < zombies.Count; i++)
            {
                var z = zombies[i];
                if (!InRam(this, z.X, z.Y)) continue;
                ulong h = IMath.Mix(((ulong)(uint)tick << 20) ^ ((ulong)(uint)z.Id << 4) ^ ((ulong)(uint)Id << 52) ^ 0xA7120A);
                if ((int)(h % 1000UL) < s.RunOverPm) killed.Add(z.Id);
            }
            return killed.Count;
        }

        /// <summary>Torreta automática: apunta al blanco más cercano de la primera arma que tenga uno, gira a su ritmo y dispara cuando está alineada (≤ 4,4°); las demás armas de la
        /// torreta disparan a su propio blanco si queda dentro de ±6,6° de donde apunta. Sigue disparando sin motor o sin combustible, no destruida.</summary>
        public void StepWeapons(UnitsWorld world, IList<Target> zombies, StepResult res)
        {
            var s = VehicleCatalog.Get(Kind);
            for (int i = 0; i < MountCd.Length; i++) if (MountCd[i] > 0) MountCd[i]--;
            if (Hp <= 0 || zombies == null || zombies.Count == 0 || s.Mounts == null) return;
            int aim = -1, aimMount = -1;
            for (int i = 0; i < s.Mounts.Length && aim < 0; i++)
            {
                var w = WeaponCatalog.Get(s.Mounts[i]);
                int idx = world.PickAt(w, X, Y, 0, 0, zombies, 0);
                if (idx >= 0 && w.MinCluster > 1) { long r2 = (long)w.RadiusMm * w.RadiusMm; int n = 0; for (int t = 0; t < zombies.Count && n < w.MinCluster; t++) { long ex = zombies[t].X - zombies[idx].X, ey = zombies[t].Y - zombies[idx].Y; if (ex * ex + ey * ey <= r2) n++; } if (n < w.MinCluster) idx = -1; }
                if (idx >= 0) { aim = idx; aimMount = i; }
            }
            if (aim < 0) return;
            int want = IMath.Atan2(zombies[aim].Y - Y, zombies[aim].X - X);
            TurretYaw = (TurretYaw + IMath.Clamp(IMath.Diff(want, TurretYaw), -s.TurretRatePerTick, s.TurretRatePerTick)) & 0xFFFF;
            for (int i = 0; i < s.Mounts.Length; i++)
            {
                int cd = MountCd[i]; if (cd > 0) continue;
                var w = WeaponCatalog.Get(s.Mounts[i]);
                int idx;
                if (i == aimMount) idx = Math.Abs(IMath.Diff(want, TurretYaw)) <= 800 ? aim : -1;
                else idx = world.PickAt(w, X, Y, TurretYaw, 1200, zombies, 0);
                if (idx < 0) continue;
                world.MountFire(-1 - Id, X, Y, s.Mounts[i], ref cd, ref AmmoShots, zombies, idx, res);
                MountCd[i] = cd;
            }
        }

        // ------------------------------------------------------------ pasajeros
        /// <summary>¿Puede subir el pelotón? Casi parado, el pelotón a ≤ 15 m, vivo el vehículo y plazas libres.</summary>
        public bool CanBoard(UnitsWorld world, int platoon)
        {
            var p = world.Find(platoon); var s = VehicleCatalog.Get(Kind);
            if (p == null || Hp <= 0 || Math.Abs(SpeedMmS) > 1500 || p.Aboard != 0) return false;
            if (IMath.Dist(p.AX - X, p.AY - Y) > 15000) return false;
            return p.AliveCount() <= s.Seats - world.SeatsUsed(CarrierId);
        }
        public bool Board(UnitsWorld world, int platoon) { return CanBoard(world, platoon) && world.Embark(platoon, CarrierId, VehicleCatalog.Get(Kind).Seats - world.SeatsUsed(CarrierId)); }
        /// <summary>Lleva a los pasajeros con el vehículo (llamar cada tick tras Step).</summary>
        public void CarryPassengers(UnitsWorld world) { world.CarryAboard(CarrierId, X, Y, Heading); }
        /// <summary>Baja a un pelotón detrás del vehículo (a 6 m del eje trasero, unos 2 m por encima de la mitad de su largo).</summary>
        public bool Unload(UnitsWorld world, int platoon)
        {
            var s = VehicleCatalog.Get(Kind); int back = s.LengthMm / 2 + 3000;
            int bx = X - IMath.DivR((long)back * IMath.Cos(Heading), IMath.One), by = Y - IMath.DivR((long)back * IMath.Sin(Heading), IMath.One);
            return world.Disembark(platoon, bx, by, Heading);
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
