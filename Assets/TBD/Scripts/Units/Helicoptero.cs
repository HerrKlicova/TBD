using System;
using System.Collections.Generic;
using TBD.Net;

namespace TBD.Units
{
    // Helicóptero del jugador (noche del 7-nov). LOCAL: fuera del hash y del lockstep del prototipo (decisión de Mak); solo el daño a la
    // horda entra como orden. Física arcade creíble en enteros: inercia horizontal, ascenso con límite, inclinación visual según la aceleración,
    // arranque de rotor, despegue y aterrizaje (blando o duro con daño).

    public enum HeliKind : byte { Cougar = 0, Tigre = 1, Chinook = 2, Nh90 = 3 }
    public enum HeliState : byte { Apagado = 0, Arrancando = 1, EnTierra = 2, Volando = 3, Estrellado = 4 }

    public struct HeliStats
    {
        public string Name;
        public int MaxFwdMmS, MaxBackMmS, MaxStrafeMmS;
        public int AccelMmS2, DragMmS2;                // empuje horizontal y deriva pasiva (menor: el helicóptero resbala)
        public int MaxClimbMmS, MaxDescentMmS, VertAccelMmS2;
        public int YawRatePerTick;                      // bam16 por tick al máximo del pedal
        public int MaxTilt;                             // bam16 de inclinación visual máxima (cabeceo y alabeo)
        public int TiltRatePerTick;
        public int SpoolUpPerTick, SpoolDownPerTick;    // rotor en milésimas por tick
        public int CeilingMm;
        public int HardLandMmS;                         // por encima de esta velocidad de caída, el aterrizaje es duro y hace daño
        public int Hp;
        // Datos de Hugo (balance_unidades.csv, filas heli_<clave>_*; D-N76):
        public string Key;
        public int ArmorPm;                             // ‰ del daño de los zombis que se ignora
        public int CruiseAltMm, ContactAltMm;           // altura de crucero; por debajo de la de contacto los zombis pueden agarrarlo
        public int Seats;                               // plazas de infantería
        public int AmmoBoardMilliU;
        public int FuelFlyE4s, FuelGroundE4s, TankU;    // E4 de u por segundo real en vuelo y posado con rotores; depósito en u
        public WeaponId[] DoorGuns;                     // ametralladoras de puerta: [0] a la izquierda, [1] a la derecha (cada una solo dispara a su lado)
        public WeaponId[] NoseWeapons;                  // armas fijas al frente (cañón del Tigre)
    }

    public static class HeliCatalog
    {
        // Referencias: Eurocopter AS532 Cougar (transporte), Tigre HAD (ataque), CH-47 Chinook (carga pesada), todos en servicio en el ejército de tierra.
        public static HeliStats[] Table =
        {
            new HeliStats { Key = "cougar", TankU = 6, FuelFlyE4s = 270, FuelGroundE4s = 90, Seats = 12, AmmoBoardMilliU = 1000, CruiseAltMm = 30000, ContactAltMm = 3000, ArmorPm = 900, DoorGuns = new[] { WeaponId.MgPuerta, WeaponId.MgPuerta }, NoseWeapons = new WeaponId[0], Name = "Cougar", MaxFwdMmS = 60000, MaxBackMmS = 12000, MaxStrafeMmS = 18000, AccelMmS2 = 9000, DragMmS2 = 5000, MaxClimbMmS = 9000, MaxDescentMmS = 8000, VertAccelMmS2 = 6000, YawRatePerTick = 480, MaxTilt = 2900, TiltRatePerTick = 160, SpoolUpPerTick = 7, SpoolDownPerTick = 4, CeilingMm = 120000, HardLandMmS = 3500, Hp = 700 },
            new HeliStats { Key = "tigre", TankU = 6, FuelFlyE4s = 270, FuelGroundE4s = 90, Seats = 0, AmmoBoardMilliU = 3000, CruiseAltMm = 30000, ContactAltMm = 3000, ArmorPm = 900, DoorGuns = new WeaponId[0], NoseWeapons = new[] { WeaponId.Canon30Heli }, Name = "Tigre", MaxFwdMmS = 50000, MaxBackMmS = 15000, MaxStrafeMmS = 24000, AccelMmS2 = 6000, DragMmS2 = 8000, MaxClimbMmS = 7000, MaxDescentMmS = 5000, VertAccelMmS2 = 9000, YawRatePerTick = 424, MaxTilt = 3277, TiltRatePerTick = 220, SpoolUpPerTick = 9, SpoolDownPerTick = 5, CeilingMm = 150000, HardLandMmS = 3500, Hp = 2500 },
            new HeliStats { Key = "chinook", TankU = 6, FuelFlyE4s = 270, FuelGroundE4s = 90, Seats = 24, AmmoBoardMilliU = 1000, CruiseAltMm = 30000, ContactAltMm = 3000, ArmorPm = 900, DoorGuns = new[] { WeaponId.MgPuerta, WeaponId.MgPuerta }, NoseWeapons = new WeaponId[0], Name = "Chinook", MaxFwdMmS = 55000, MaxBackMmS = 10000, MaxStrafeMmS = 14000, AccelMmS2 = 6500, DragMmS2 = 4000, MaxClimbMmS = 7000, MaxDescentMmS = 7000, VertAccelMmS2 = 4500, YawRatePerTick = 360, MaxTilt = 2400, TiltRatePerTick = 120, SpoolUpPerTick = 5, SpoolDownPerTick = 3, CeilingMm = 100000, HardLandMmS = 3000, Hp = 1100 },
            // NH90 de las FAMET (Paula A2): el helicóptero del MVP. Valores de Hugo en el CSV (una prueba los compara); lo demás (rotor, caída) es de jugabilidad.
            new HeliStats { Key = "nh90", Name = "NH90", MaxFwdMmS = 45000, MaxBackMmS = 9000, MaxStrafeMmS = 14000, AccelMmS2 = 5000, DragMmS2 = 7000, MaxClimbMmS = 6000, MaxDescentMmS = 4000, VertAccelMmS2 = 4000, YawRatePerTick = 364, MaxTilt = 2730, TiltRatePerTick = 180,
                SpoolUpPerTick = 8, SpoolDownPerTick = 4, CeilingMm = 60000, HardLandMmS = 4500, Hp = 3000, ArmorPm = 900, CruiseAltMm = 30000, ContactAltMm = 3000, Seats = 12, AmmoBoardMilliU = 3000, FuelFlyE4s = 270, FuelGroundE4s = 90, TankU = 6,
                DoorGuns = new[] { WeaponId.MgPuerta, WeaponId.MgPuerta }, NoseWeapons = new WeaponId[0] },
        };
        public static HeliStats Get(HeliKind k) { return Table[(int)k]; }

        static int CdegToBam(int cdeg) { return (int)((long)cdeg * 65536 / 36000); }

        /// <summary>Lee las filas heli_&lt;clave&gt;_* de balance_unidades.csv (NH90 y Tigre); lo que falte queda con el valor por defecto.</summary>
        public static void Load(CsvTable t)
        {
            for (int i = 0; i < Table.Length; i++)
            {
                var s = Table[i]; if (s.Key != "nh90" && s.Key != "tigre") continue; string k = "heli_" + s.Key + "_";
                s.Hp = (int)t.TryValue(k + "vida", s.Hp); s.ArmorPm = (int)t.TryValue(k + "armadura", s.ArmorPm);
                s.MaxFwdMmS = (int)t.TryValue(k + "vel_max_mm_s", s.MaxFwdMmS); s.AccelMmS2 = (int)t.TryValue(k + "acel_mm_s2", s.AccelMmS2); s.DragMmS2 = (int)t.TryValue(k + "fren_mm_s2", s.DragMmS2);
                s.YawRatePerTick = (int)((long)t.TryValue(k + "guinada_cdeg_s", s.YawRatePerTick * 36000L * IMath.TickRate / 65536) * 65536 / (36000L * IMath.TickRate));
                s.MaxClimbMmS = (int)t.TryValue(k + "subida_mm_s", s.MaxClimbMmS); s.MaxDescentMmS = (int)t.TryValue(k + "bajada_mm_s", s.MaxDescentMmS);
                s.CruiseAltMm = (int)t.TryValue(k + "altura_crucero_mm", s.CruiseAltMm); s.ContactAltMm = (int)t.TryValue(k + "altura_contacto_mm", s.ContactAltMm);
                s.MaxTilt = CdegToBam((int)t.TryValue(k + "inclinacion_max_cdeg", s.MaxTilt * 36000L / 65536));
                s.Seats = (int)t.TryValue(k + "plazas", s.Seats); s.AmmoBoardMilliU = (int)t.TryValue(k + "municion_bordo", s.AmmoBoardMilliU);
                s.FuelFlyE4s = (int)t.TryValue(k + "comb_vuelo_e4_s", s.FuelFlyE4s); s.FuelGroundE4s = (int)t.TryValue(k + "comb_suelo_e4_s", s.FuelGroundE4s); s.TankU = (int)t.TryValue(k + "deposito_u", s.TankU);
                Table[i] = s;
            }
        }
    }

    public sealed class Helicopter
    {
        public int Id;
        public HeliKind Kind;
        public HeliState State = HeliState.Apagado;
        public int X, Y, Z;                    // mm; Z = altura sobre el nivel del mar del mundo (suelo = Ground(X, Y))
        public int VX, VY, VZ;                 // mm/s
        public int Yaw;                        // bam16 (rumbo del morro)
        public int Pitch, Roll;                // bam16, solo visuales: Pitch > 0 = morro abajo, Roll > 0 = alabeo a la derecha
        public int Rotor;                      // 0..1000
        public int Hp;
        public bool EngineOn;
        public int LastImpactMmS;              // última velocidad de caída al tocar suelo (QA y sonido)
        public Func<int, int, int> Ground = (x, y) => 0;
        // combustible, armas y pasajeros (local, fuera del hash)
        public int FuelE4; public long FuelUsedE4; int _fuelRem, _dmgRem;
        public int AmmoShots; public int[] GunCd;
        public int CarrierId { get { return 1000 + Id; } }
        public bool Autorotating { get { return State == HeliState.Volando && FuelE4 <= 0; } }

        // mandos −1000..1000: colectivo (subir/bajar), cíclico adelante/atrás, cíclico lateral, pedales de guiñada
        public int Collective, CyclicFwd, CyclicSide, Pedals;

        public Helicopter(int id, HeliKind kind, int x, int y, int yaw, int groundZ = 0)
        {
            Id = id; Kind = kind; X = x; Y = y; Z = groundZ; Yaw = yaw & 0xFFFF; var st = HeliCatalog.Get(kind); Hp = st.Hp; State = HeliState.EnTierra;
            FuelE4 = st.TankU * 10_000; AmmoShots = (int)((long)st.AmmoBoardMilliU * WeaponCatalog.ShotsPerU / 1000);
            GunCd = new int[(st.DoorGuns == null ? 0 : st.DoorGuns.Length) + (st.NoseWeapons == null ? 0 : st.NoseWeapons.Length)];
        }

        /// <summary>Combustible bajo (menos del 20 %): aviso.</summary>
        public bool LowFuel { get { return FuelE4 * 5 < HeliCatalog.Get(Kind).TankU * 10_000; } }
        public int FuelPm { get { int tank = HeliCatalog.Get(Kind).TankU * 10_000; return tank == 0 ? 0 : (int)((long)FuelE4 * 1000 / tank); } }
        /// <summary>Repostar solo posado (en el helipuerto lo decide quien llama). Devuelve lo que ha entrado de verdad (E4 de u).</summary>
        public int Refuel(int e4)
        {
            if (State == HeliState.Volando || State == HeliState.Estrellado) return 0;
            int room = HeliCatalog.Get(Kind).TankU * 10_000 - FuelE4; int n = Math.Max(0, Math.Min(e4, room)); FuelE4 += n; return n;
        }
        /// <summary>Los zombis solo lo alcanzan por debajo de la altura de contacto (posado o embarcando): por encima es intocable.</summary>
        public bool Vulnerable { get { return State != HeliState.Estrellado && (State != HeliState.Volando || AltitudeMm < HeliCatalog.Get(Kind).ContactAltMm); } }
        /// <summary>Zombis agarrados: solo hacen daño si es vulnerable; la armadura resta el ‰ que corresponda. Resto exacto. Devuelve la vida que queda.</summary>
        public int TakeContact(int touching, int dmgPerTick)
        {
            if (touching <= 0 || Hp <= 0 || !Vulnerable) return Hp;
            long acc = _dmgRem + (long)touching * dmgPerTick * (1000 - HeliCatalog.Get(Kind).ArmorPm);
            Hp = (int)Math.Max(0, Hp - acc / 1000); _dmgRem = (int)(acc % 1000);
            if (Hp <= 0) { State = HeliState.Estrellado; VX = VY = VZ = 0; }
            return Hp;
        }

        public void Controls(int collective, int cyclicFwd, int cyclicSide, int pedals)
        {
            Collective = IMath.Clamp(collective, -1000, 1000); CyclicFwd = IMath.Clamp(cyclicFwd, -1000, 1000);
            CyclicSide = IMath.Clamp(cyclicSide, -1000, 1000); Pedals = IMath.Clamp(pedals, -1000, 1000);
        }

        public void StartEngine() { if (State != HeliState.Estrellado) EngineOn = true; }
        public void StopEngine() { EngineOn = false; }
        public int GroundZ { get { return Ground(X, Y); } }
        public int AltitudeMm { get { return Z - GroundZ; } }

        public void Step()
        {
            var s = HeliCatalog.Get(Kind);
            if (State == HeliState.Estrellado) { Collective = CyclicFwd = CyclicSide = Pedals = 0; EngineOn = false; }
            // combustible: en vuelo o posado con los rotores girando, en E4 de u por segundo real con resto exacto; a cero el motor se para (en el aire: autorrotación)
            if (EngineOn && State != HeliState.Estrellado)
            {
                _fuelRem += State == HeliState.Volando ? s.FuelFlyE4s : s.FuelGroundE4s; int use = _fuelRem / IMath.TickRate; _fuelRem %= IMath.TickRate;
                if (use > 0) { use = Math.Min(use, FuelE4); FuelE4 -= use; FuelUsedE4 += use; }
                if (FuelE4 <= 0) { FuelE4 = 0; EngineOn = false; }
            }
            bool auto = State == HeliState.Volando && FuelE4 <= 0;      // autorrotación: el rotor sigue girando por la caída, sin control, a velocidad de descenso constante
            if (auto) { Collective = 0; CyclicFwd = 0; CyclicSide = 0; Pedals = 0; }
            // rotor
            Rotor = auto ? Math.Max(Rotor, 950) : EngineOn ? Math.Min(1000, Rotor + s.SpoolUpPerTick) : Math.Max(0, Rotor - s.SpoolDownPerTick);
            int gz = Ground(X, Y);
            bool airborne = State == HeliState.Volando;

            if (!airborne)
            {
                // en tierra: nada se mueve; despega con rotor casi a tope y colectivo hacia arriba
                VX = VY = VZ = 0;
                Z = gz;
                Pitch = IMath.Approach(Pitch, 0, s.TiltRatePerTick); Roll = IMath.Approach(Roll, 0, s.TiltRatePerTick);
                if (State != HeliState.Estrellado)
                    State = !EngineOn && Rotor == 0 ? HeliState.Apagado : Rotor < 1000 ? HeliState.Arrancando : HeliState.EnTierra;
                if (State != HeliState.Estrellado && Rotor >= 950 && Collective > 150) { State = HeliState.Volando; airborne = true; VZ = 500; }
                else return;
            }

            // vertical: el colectivo manda la velocidad de ascenso; sin rotor se cae (autorrotación torpe)
            int lift = Rotor;                                          // 0..1000
            int vzTarget = auto ? -s.MaxDescentMmS : Collective >= 0 ? (int)((long)Collective * s.MaxClimbMmS / 1000) : (int)((long)Collective * s.MaxDescentMmS / 1000);
            if (lift < 900) vzTarget = -s.MaxDescentMmS * (900 - lift) / 900 - 2500 * (900 - lift) / 900;   // el rotor se para: cae cada vez más rápido
            if (Z - gz >= s.CeilingMm && vzTarget > 0) vzTarget = 0;
            VZ = IMath.Approach(VZ, vzTarget, s.VertAccelMmS2 / IMath.TickRate);

            // horizontal en ejes del cuerpo: el cíclico pide una velocidad y el aparato la persigue con empuje limitado (inercia)
            int c = IMath.Cos(Yaw), sn = IMath.Sin(Yaw);
            int fwdWant = CyclicFwd >= 0 ? (int)((long)CyclicFwd * s.MaxFwdMmS / 1000) : (int)((long)CyclicFwd * s.MaxBackMmS / 1000);
            int sideWant = (int)((long)CyclicSide * s.MaxStrafeMmS / 1000);            // + = a la derecha
            if (lift < 900) { fwdWant = 0; sideWant = 0; }
            // velocidad actual en ejes del cuerpo
            long vf = ((long)VX * c + (long)VY * sn) >> 16, vr = ((long)VX * sn - (long)VY * c) >> 16;
            int step = s.AccelMmS2 / IMath.TickRate, drag = s.DragMmS2 / IMath.TickRate;
            int nf, nr;
            if (CyclicFwd == 0 && CyclicSide == 0) { nf = IMath.Approach((int)vf, 0, drag); nr = IMath.Approach((int)vr, 0, drag); }
            else
            {
                // el empuje total (paso) se reparte entre los dos ejes en proporción al error
                int ef = fwdWant - (int)vf, er = sideWant - (int)vr, ed = IMath.Dist(ef, er);
                if (ed <= step) { nf = fwdWant; nr = sideWant; }
                else { nf = (int)vf + IMath.DivR((long)ef * step, ed); nr = (int)vr + IMath.DivR((long)er * step, ed); }
            }
            int accF = nf - (int)vf, accR = nr - (int)vr;                              // aceleración de este tick, para inclinar
            VX = (int)((nf * (long)c + nr * (long)sn) >> 16); VY = (int)((nf * (long)sn - nr * (long)c) >> 16);

            // guiñada
            Yaw = (Yaw + (int)((long)Pedals * s.YawRatePerTick / 1000) * Math.Min(lift, 1000) / 1000) & 0xFFFF;

            // inclinación visual: sigue a la velocidad (morro abajo al avanzar) y se acentúa con la aceleración
            int pitchWant = IMath.Clamp((int)((long)nf * s.MaxTilt / (2 * s.MaxFwdMmS)) + accF * 12, -s.MaxTilt, s.MaxTilt);
            int rollWant = IMath.Clamp((int)((long)nr * s.MaxTilt / (2 * s.MaxStrafeMmS)) + accR * 12, -s.MaxTilt, s.MaxTilt);
            Pitch = IMath.Approach(Pitch, pitchWant, s.TiltRatePerTick); Roll = IMath.Approach(Roll, rollWant, s.TiltRatePerTick);

            // integrar
            X += IMath.DivR(VX, IMath.TickRate); Y += IMath.DivR(VY, IMath.TickRate); Z += IMath.DivR(VZ, IMath.TickRate);

            // suelo: aterrizaje blando o duro
            gz = Ground(X, Y);
            if (Z <= gz)
            {
                int impact = -VZ, horiz = IMath.Dist(VX, VY);
                LastImpactMmS = Math.Max(0, impact);
                int dmg = 0;
                if (impact > s.HardLandMmS) dmg += (impact - s.HardLandMmS) * s.Hp / 8000;      // 8 m/s sobra para destrozarlo
                if (horiz > 12000) dmg += (horiz - 12000) * s.Hp / 40000;
                Hp -= dmg;
                Z = gz; VX = VY = VZ = 0;
                State = Hp <= 0 ? HeliState.Estrellado : HeliState.EnTierra;
            }
        }

        // ------------------------------------------------------------ armas
        /// <summary>Ametralladoras de puerta (una a cada lado, cada una solo a su semiplano: izquierda = rumbo + 90°) y armas fijas al morro. Disparan con el alcance ya recortado por la altura.
        /// No disparan estrellado. Munición de a bordo compartida.</summary>
        public void StepWeapons(UnitsWorld world, IList<Target> zombies, StepResult res)
        {
            var s = HeliCatalog.Get(Kind);
            for (int i = 0; i < GunCd.Length; i++) if (GunCd[i] > 0) GunCd[i]--;
            if (State == HeliState.Estrellado || Hp <= 0 || zombies == null || zombies.Count == 0) return;
            int alt = Math.Max(0, AltitudeMm), n = 0;
            if (s.DoorGuns != null)
                for (int i = 0; i < s.DoorGuns.Length; i++, n++)
                {
                    if (GunCd[n] > 0) continue;
                    var w = WeaponCatalog.Get(s.DoorGuns[i]);
                    int center = (Yaw + (i == 0 ? 16384 : -16384)) & 0xFFFF;
                    int idx = world.PickAt(w, X, Y, center, w.HalfConeBam > 0 ? w.HalfConeBam : 16384, zombies, alt);
                    int cd = GunCd[n]; world.MountFire(-1001 - Id, X, Y, s.DoorGuns[i], ref cd, ref AmmoShots, zombies, idx, res); GunCd[n] = cd;
                }
            if (s.NoseWeapons != null)
                for (int i = 0; i < s.NoseWeapons.Length; i++, n++)
                {
                    if (GunCd[n] > 0) continue;
                    var w = WeaponCatalog.Get(s.NoseWeapons[i]);
                    int idx = world.PickAt(w, X, Y, Yaw, 1500, zombies, alt);       // fijo al morro: ±8°
                    int cd = GunCd[n]; world.MountFire(-1001 - Id, X, Y, s.NoseWeapons[i], ref cd, ref AmmoShots, zombies, idx, res); GunCd[n] = cd;
                }
        }

        // ------------------------------------------------------------ pasajeros
        /// <summary>Solo se embarca posado (EnTierra, también con rotores girando) con el pelotón a ≤ 20 m y plazas libres.</summary>
        public bool CanBoard(UnitsWorld world, int platoon)
        {
            var p = world.Find(platoon); var s = HeliCatalog.Get(Kind);
            if (p == null || State != HeliState.EnTierra || p.Aboard != 0 || s.Seats <= 0) return false;
            if (IMath.Dist(p.AX - X, p.AY - Y) > 20000) return false;
            if (OnePlatoon && PlatoonsAboard(world) > 0) return false;      // D-N143: en el MVP, un pelotón por helicóptero (la cifra real del NH90 está SIN VERIFICAR)
            return p.AliveCount() <= s.Seats - world.SeatsUsed(CarrierId);
        }
        /// <summary>D-N143 (Eugenio): capacidad de un pelotón, sea cual sea su tamaño (MVP). false = solo cuentan las plazas.</summary>
        public bool OnePlatoon = true;
        public int PlatoonsAboard(UnitsWorld world) { int n = 0; for (int i = 0; i < world.Platoons.Count; i++) if (world.Platoons[i].Aboard == CarrierId) n++; return n; }
        /// <summary>D-N143 (tecla U): baja a todos los que lleva, solo posado. Devuelve cuántos pelotones bajan.</summary>
        public int UnloadAll(UnitsWorld world)
        {
            if (State != HeliState.EnTierra) return 0; int n = 0;
            for (int i = 0; i < world.Platoons.Count; i++) if (world.Platoons[i].Aboard == CarrierId && Unload(world, world.Platoons[i].Id)) n++;
            return n;
        }
        /// <summary>D-N143: ¿se puede aterrizar en (x, y)? Hace falta un claro de 20 × 20 m (±halfMm) sin edificios, muros ni vehículos («blocked», rectángulos en mm)
        /// y sin zombis dentro. El helipuerto es un claro más. Entero y sin azar.</summary>
        public static bool LandingClear(int x, int y, IList<RectMm> blocked, IList<Target> zombies, int halfMm = 10000)
        {
            int x0 = x - halfMm, x1 = x + halfMm, y0 = y - halfMm, y1 = y + halfMm;
            if (blocked != null) for (int i = 0; i < blocked.Count; i++) { var r = blocked[i]; if (r.X0 < x1 && r.X1 > x0 && r.Y0 < y1 && r.Y1 > y0) return false; }
            if (zombies != null) for (int i = 0; i < zombies.Count; i++) { var z = zombies[i]; if (z.X >= x0 && z.X <= x1 && z.Y >= y0 && z.Y <= y1) return false; }
            return true;
        }
        public bool Board(UnitsWorld world, int platoon) { return CanBoard(world, platoon) && world.Embark(platoon, CarrierId, HeliCatalog.Get(Kind).Seats - world.SeatsUsed(CarrierId)); }
        public void CarryPassengers(UnitsWorld world) { world.CarryAboard(CarrierId, X, Y, Yaw); }
        /// <summary>Baja a un pelotón a 8 m a un costado, solo posado. Estrellado, los pasajeros mueren con él (los baja y los mata quien llama).</summary>
        public bool Unload(UnitsWorld world, int platoon)
        {
            if (State != HeliState.EnTierra) return false;
            int bx = X + IMath.DivR(8000L * IMath.Cos((Yaw - 16384) & 0xFFFF), IMath.One), by = Y + IMath.DivR(8000L * IMath.Sin((Yaw - 16384) & 0xFFFF), IMath.One);
            return world.Disembark(platoon, bx, by, Yaw);
        }

        /// <summary>Daño por aplastar con el rotor o las ruedas: el helicóptero no atropella; el daño a la horda es de su artillería, no de esta clase.</summary>
        public bool IsAirborne { get { return State == HeliState.Volando; } }
    }
}
