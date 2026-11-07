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
                    ArmorPm = 950, HordeSpeedMmS = 3000, BlockPile = 2, MaxYawPerTick = YawTick(5000), Seats = 4, AmmoBoardMilliU = 3000, FuelMarchE4s = 90, FuelIdleE4s = 25, TankU = 4, RunOverMinMmS = 3000, RunOverPm = 300,
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
        public bool OnJob;                          // lo tiene reservado una orden de transporte (TransportDirector)
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

    /// <summary>Punto en mm (camino de un vehículo).</summary>
    public struct PointMm { public int X, Y; public PointMm(int x, int y) { X = x; Y = y; } }

    /// <summary>Búsqueda de caminos de los vehículos (Eugenio, 7-nov: «se quedan pillados en los muros»). Rejilla de celdas (por defecto 1 m) sobre los mismos
    /// obstáculos de la D-N139 (edificios, muros, tren y vehículos aparcados), inflados con el radio del vehículo. A* de 8 vecinos sin cortar esquinas
    /// (coste 10 recto y 14 en diagonal, heurística octil) y camino alisado por línea de vista. Enteros y desempates por índice: determinista.
    /// No entra en ningún hash (los vehículos son locales en el prototipo). Una rejilla por radio: la zona guarda una por tipo de vehículo.</summary>
    public sealed class NavGrid
    {
        public readonly int X0, Y0, Cell, W, H;
        readonly byte[] _static;            // 1 = celda bloqueada por los obstáculos fijos (inflados)
        readonly int[] _dyn; int _stamp;     // obstáculos de cada búsqueda (otros vehículos): celda bloqueada si _dyn == _stamp
        public int InflateMm { get; private set; }
        public int MaxExpand = 250000;       // tope de nodos por búsqueda (si no hay camino en un mapa grande, no se cuelga)
        public int LastExpanded;             // nodos que abrió la última búsqueda (diagnóstico)

        public NavGrid(int x0, int y0, int x1, int y1, int cellMm = 1000)
        {
            X0 = x0; Y0 = y0; Cell = Math.Max(100, cellMm);
            W = Math.Max(1, (x1 - x0 + Cell - 1) / Cell); H = Math.Max(1, (y1 - y0 + Cell - 1) / Cell);
            _static = new byte[W * H]; _dyn = new int[W * H];
            _g = new int[W * H]; _from = new int[W * H]; _seen = new int[W * H]; _closed = new int[W * H];
        }

        /// <summary>Marca los obstáculos fijos inflados en «inflateMm» (radio del vehículo más margen). Se rehace cuando cambian (obra nueva, muro roto).</summary>
        public void Build(IList<RectMm> obstacles, int inflateMm)
        {
            InflateMm = Math.Max(0, inflateMm); Array.Clear(_static, 0, _static.Length);
            if (obstacles != null) for (int i = 0; i < obstacles.Count; i++) Stamp(_static, null, 1, obstacles[i], InflateMm);
        }

        void Stamp(byte[] b, int[] d, int v, RectMm r, int infl)
        {
            // una celda se bloquea si su centro cae dentro del rectángulo inflado
            int cx0 = CeilDiv(r.X0 - infl - X0 - Cell / 2, Cell), cx1 = FloorDiv(r.X1 + infl - X0 - Cell / 2, Cell);
            int cy0 = CeilDiv(r.Y0 - infl - Y0 - Cell / 2, Cell), cy1 = FloorDiv(r.Y1 + infl - Y0 - Cell / 2, Cell);
            cx0 = Math.Max(cx0, 0); cy0 = Math.Max(cy0, 0); cx1 = Math.Min(cx1, W - 1); cy1 = Math.Min(cy1, H - 1);
            for (int y = cy0; y <= cy1; y++) for (int x = cx0; x <= cx1; x++) { if (b != null) b[y * W + x] = (byte)v; else d[y * W + x] = v; }
        }
        static int FloorDiv(int a, int b) { return a >= 0 ? a / b : -((-a + b - 1) / b); }
        static int CeilDiv(int a, int b) { return -FloorDiv(-a, b); }

        public bool InGrid(int cx, int cy) { return cx >= 0 && cy >= 0 && cx < W && cy < H; }
        public int CellX(int xMm) { return IMath.Clamp(FloorDiv(xMm - X0, Cell), 0, W - 1); }
        public int CellY(int yMm) { return IMath.Clamp(FloorDiv(yMm - Y0, Cell), 0, H - 1); }
        public int CenterX(int cx) { return X0 + cx * Cell + Cell / 2; }
        public int CenterY(int cy) { return Y0 + cy * Cell + Cell / 2; }
        bool Free(int i) { return _static[i] == 0 && _dyn[i] != _stamp; }
        public bool FreeAt(int xMm, int yMm) { int cx = FloorDiv(xMm - X0, Cell), cy = FloorDiv(yMm - Y0, Cell); return InGrid(cx, cy) && Free(cy * W + cx); }

        // A*: montículo binario de índices con clave (f, h, índice) para que el orden sea siempre el mismo
        readonly int[] _g, _from, _seen, _closed; int _search;
        readonly List<long> _heap = new List<long>();
        void Push(int f, int h, int i) { long k = ((long)f << 42) | ((long)Math.Min(h, (1 << 20) - 1) << 22) | (uint)i; _heap.Add(k); int c = _heap.Count - 1; while (c > 0) { int p = (c - 1) / 2; if (_heap[p] <= _heap[c]) break; var t = _heap[p]; _heap[p] = _heap[c]; _heap[c] = t; c = p; } }
        long Pop() { long top = _heap[0]; int last = _heap.Count - 1; _heap[0] = _heap[last]; _heap.RemoveAt(last); int c = 0; while (true) { int l = 2 * c + 1, r = l + 1, m = c; if (l < _heap.Count && _heap[l] < _heap[m]) m = l; if (r < _heap.Count && _heap[r] < _heap[m]) m = r; if (m == c) break; var t = _heap[m]; _heap[m] = _heap[c]; _heap[c] = t; c = m; } return top; }
        static int Octile(int dx, int dy) { dx = Math.Abs(dx); dy = Math.Abs(dy); return 10 * Math.Max(dx, dy) + 4 * Math.Min(dx, dy); }

        /// <summary>La celda libre más cercana a (cx, cy) en anillos crecientes (radio máximo «maxR» celdas); −1 si no hay. Orden fijo: determinista.</summary>
        int NearestFree(int cx, int cy, int maxR)
        {
            cx = IMath.Clamp(cx, 0, W - 1); cy = IMath.Clamp(cy, 0, H - 1);
            if (Free(cy * W + cx)) return cy * W + cx;
            for (int r = 1; r <= maxR; r++)
            {
                int best = -1, bd = int.MaxValue;
                for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    int x = cx + dx, y = cy + dy; if (!InGrid(x, y) || !Free(y * W + x)) continue;
                    int d = dx * dx + dy * dy; if (d < bd) { bd = d; best = y * W + x; }
                }
                if (best >= 0) return best;
            }
            return -1;
        }

        /// <summary>Camino de (sx, sy) a (tx, ty) en mm, sin pasar por obstáculos fijos ni por «dynamic» (otros vehículos; inflados con el mismo radio).
        /// Rellena «path» con los puntos de paso ya alisados (sin el de salida; el último es el destino, o la celda libre más cercana si el destino está tapado).
        /// Si la salida está dentro de un obstáculo inflado (vehículo pegado al muro), sale por la celda libre más cercana. Devuelve false si no hay camino.</summary>
        public bool FindPath(int sx, int sy, int tx, int ty, List<PointMm> path, IList<RectMm> dynamic = null)
        {
            path.Clear(); _stamp++; if (_stamp == int.MaxValue) { Array.Clear(_dyn, 0, _dyn.Length); _stamp = 1; }
            if (dynamic != null) for (int i = 0; i < dynamic.Count; i++) Stamp(null, _dyn, _stamp, dynamic[i], InflateMm);
            int s = NearestFree(CellX(sx), CellY(sy), 8), t = NearestFree(CellX(tx), CellY(ty), 30);
            LastExpanded = 0;
            if (s < 0 || t < 0) return false;
            bool exactGoal = t == CellY(ty) * W + CellX(tx) && FreeAt(tx, ty);
            if (s == t) { path.Add(exactGoal ? new PointMm(tx, ty) : new PointMm(CenterX(t % W), CenterY(t / W))); return true; }
            _search++; if (_search == int.MaxValue) { Array.Clear(_seen, 0, _seen.Length); Array.Clear(_closed, 0, _closed.Length); _search = 1; }
            _heap.Clear();
            int tcx = t % W, tcy = t / W;
            _seen[s] = _search; _g[s] = 0; _from[s] = -1; Push(Octile(s % W - tcx, s / W - tcy), Octile(s % W - tcx, s / W - tcy), s);
            bool found = false;
            while (_heap.Count > 0)
            {
                long k = Pop(); int i = (int)(k & 0x3FFFFF);
                if (_closed[i] == _search) continue;
                _closed[i] = _search;
                if (i == t) { found = true; break; }
                if (++LastExpanded > MaxExpand) break;
                int cx = i % W, cy = i / W;
                for (int n = 0; n < 8; n++)
                {
                    int dx = n == 0 ? 1 : n == 1 ? -1 : n == 2 ? 0 : n == 3 ? 0 : n == 4 ? 1 : n == 5 ? 1 : n == 6 ? -1 : -1;
                    int dy = n == 0 ? 0 : n == 1 ? 0 : n == 2 ? 1 : n == 3 ? -1 : n == 4 ? 1 : n == 5 ? -1 : n == 6 ? 1 : -1;
                    int x = cx + dx, y = cy + dy; if (!InGrid(x, y)) continue;
                    int j = y * W + x; if (!Free(j) || _closed[j] == _search) continue;
                    if (dx != 0 && dy != 0 && (!Free(cy * W + x) || !Free(y * W + cx))) continue;     // sin cortar esquinas
                    int g = _g[i] + (dx != 0 && dy != 0 ? 14 : 10);
                    if (_seen[j] == _search && g >= _g[j]) continue;
                    _seen[j] = _search; _g[j] = g; _from[j] = i;
                    int h = Octile(x - tcx, y - tcy); Push(g + h, h, j);
                }
            }
            if (!found) return false;
            // celdas de la meta a la salida, y alisado por línea de vista (string pulling)
            var cells = _cells; cells.Clear(); for (int c = t; c >= 0; c = _from[c]) cells.Add(c); cells.Reverse();
            int a = 0; int ax = sx, ay = sy; if (!FreeAt(sx, sy)) { ax = CenterX(s % W); ay = CenterY(s / W); path.Add(new PointMm(ax, ay)); }
            while (a < cells.Count - 1)
            {
                int b = cells.Count - 1;
                while (b > a + 1 && !LineFree(ax, ay, CenterX(cells[b] % W), CenterY(cells[b] / W))) b--;
                a = b; ax = CenterX(cells[a] % W); ay = CenterY(cells[a] / W);
                path.Add(new PointMm(ax, ay));
            }
            if (exactGoal) { if (path.Count > 0 && LineFree(path.Count > 1 ? path[path.Count - 2].X : sx, path.Count > 1 ? path[path.Count - 2].Y : sy, tx, ty)) path[path.Count - 1] = new PointMm(tx, ty); else path.Add(new PointMm(tx, ty)); }
            return true;
        }
        readonly List<int> _cells = new List<int>();

        /// <summary>¿El tramo recto de a a b va todo por celdas libres? Muestra cada cuarto de celda (conservador).</summary>
        public bool LineFree(int ax, int ay, int bx, int by)
        {
            int d = IMath.Dist(bx - ax, by - ay), step = Math.Max(1, Cell / 4), n = d / step + 1;
            for (int k = 0; k <= n; k++) { int x = ax + (int)((long)(bx - ax) * k / n), y = ay + (int)((long)(by - ay) * k / n); if (!FreeAt(x, y)) return false; }
            return true;
        }

        /// <summary>Los muros del recinto (D-N139) como rectángulos de «thickMm»: oeste, norte y este macizos; sur con la puerta de vehículos E4 de gateX0 a gateX1.
        /// Coordenadas de la zona en mm (fuerte X 70-156, Y 44-85 m; E4 en X 95-103 m).</summary>
        public static void FortWalls(List<RectMm> into, int x0, int y0, int x1, int y1, int gateX0, int gateX1, int thickMm = 600)
        {
            int h = thickMm / 2;
            into.Add(new RectMm(x0 - h, y0 - h, x0 + h, y1 + h));     // oeste (puerta cerrada)
            into.Add(new RectMm(x1 - h, y0 - h, x1 + h, y1 + h));     // este
            into.Add(new RectMm(x0 - h, y1 - h, x1 + h, y1 + h));     // norte
            into.Add(new RectMm(x0 - h, y0 - h, gateX0, y0 + h));     // sur, a la izquierda de E4
            into.Add(new RectMm(gateX1, y0 - h, x1 + h, y0 + h));     // sur, a la derecha de E4
        }
    }

    /// <summary>Lo que sigue un vehículo por su camino (NavGrid): punto al que apuntar con avance anticipado (giro suave: cambia de tramo antes de llegar
    /// a la esquina) y detección de atasco: si quiere moverse y en «StuckTicks» (2 s) no avanza «StuckMm», recalcula la ruta desde donde está.</summary>
    public sealed class VehicleNav
    {
        public readonly List<PointMm> Path = new List<PointMm>();
        public int Index;                                   // punto de paso actual
        public int GoalX, GoalY; public bool HasGoal;
        public int AdvanceMm = 3500;                        // a esta distancia de un punto de paso ya se apunta al siguiente (radio de giro)
        public int ArriveMm = 1500;
        public int StuckTicks = 2 * IMath.TickRate, StuckMm = 400;
        public int Replans, Failures;                       // recálculos por atasco y búsquedas sin camino
        int _anchorX, _anchorY, _anchorTick, _tick;

        /// <summary>Nueva orden de ir a (tx, ty). Devuelve false si no hay camino (el vehículo se queda quieto).</summary>
        public bool SetGoal(NavGrid g, int x, int y, int tx, int ty, IList<RectMm> dynamic = null)
        {
            GoalX = tx; GoalY = ty; HasGoal = true; _anchorX = x; _anchorY = y; _anchorTick = _tick;
            return Plan(g, x, y, dynamic);
        }

        bool Plan(NavGrid g, int x, int y, IList<RectMm> dynamic)
        {
            Index = 0;
            if (!g.FindPath(x, y, GoalX, GoalY, Path, dynamic)) { Path.Clear(); HasGoal = false; Failures++; return false; }
            return true;
        }

        public void Clear() { Path.Clear(); HasGoal = false; Index = 0; }

        /// <summary>Una vez por tick: avanza el punto de paso y da a dónde apuntar. «moving»: el vehículo intenta moverse (acelerador puesto).
        /// Devuelve false si ya llegó o no tiene ruta (entonces que frene). Si lleva 2 s atascado, recalcula desde (x, y) con los obstáculos de ahora.</summary>
        public bool Steer(NavGrid g, int x, int y, out int aimX, out int aimY, IList<RectMm> dynamic = null)
        {
            _tick++;
            aimX = x; aimY = y;
            if (!HasGoal || Path.Count == 0) return false;
            var last = Path[Path.Count - 1];
            if (IMath.Dist(last.X - x, last.Y - y) <= ArriveMm) { Clear(); return false; }
            while (Index < Path.Count - 1 && IMath.Dist(Path[Index].X - x, Path[Index].Y - y) <= AdvanceMm) Index++;
            // atasco: no avanza StuckMm en StuckTicks
            if (IMath.Dist(x - _anchorX, y - _anchorY) >= StuckMm) { _anchorX = x; _anchorY = y; _anchorTick = _tick; }
            else if (_tick - _anchorTick >= StuckTicks)
            {
                Replans++; _anchorX = x; _anchorY = y; _anchorTick = _tick;
                if (!Plan(g, x, y, dynamic)) return false;
                HasGoal = true;
            }
            aimX = Path[Index].X; aimY = Path[Index].Y;
            return true;
        }
    }
}
