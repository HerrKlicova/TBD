using System;

namespace TBD.Units
{
    // Helicóptero del jugador (noche del 7-nov). LOCAL: fuera del hash y del lockstep del prototipo (decisión de Mak); solo el daño a la
    // horda entra como orden. Física arcade creíble en enteros: inercia horizontal, ascenso con límite, inclinación visual según la aceleración,
    // arranque de rotor, despegue y aterrizaje (blando o duro con daño).

    public enum HeliKind : byte { Cougar = 0, Tigre = 1, Chinook = 2 }
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
    }

    public static class HeliCatalog
    {
        // Referencias: Eurocopter AS532 Cougar (transporte), Tigre HAD (ataque), CH-47 Chinook (carga pesada), todos en servicio en el ejército de tierra.
        public static HeliStats[] Table =
        {
            new HeliStats { Name = "Cougar", MaxFwdMmS = 60000, MaxBackMmS = 12000, MaxStrafeMmS = 18000, AccelMmS2 = 9000, DragMmS2 = 5000, MaxClimbMmS = 9000, MaxDescentMmS = 8000, VertAccelMmS2 = 6000, YawRatePerTick = 480, MaxTilt = 2900, TiltRatePerTick = 160, SpoolUpPerTick = 7, SpoolDownPerTick = 4, CeilingMm = 120000, HardLandMmS = 3500, Hp = 700 },
            new HeliStats { Name = "Tigre", MaxFwdMmS = 70000, MaxBackMmS = 15000, MaxStrafeMmS = 24000, AccelMmS2 = 13000, DragMmS2 = 6000, MaxClimbMmS = 12000, MaxDescentMmS = 10000, VertAccelMmS2 = 9000, YawRatePerTick = 650, MaxTilt = 3600, TiltRatePerTick = 220, SpoolUpPerTick = 9, SpoolDownPerTick = 5, CeilingMm = 150000, HardLandMmS = 3500, Hp = 550 },
            new HeliStats { Name = "Chinook", MaxFwdMmS = 55000, MaxBackMmS = 10000, MaxStrafeMmS = 14000, AccelMmS2 = 6500, DragMmS2 = 4000, MaxClimbMmS = 7000, MaxDescentMmS = 7000, VertAccelMmS2 = 4500, YawRatePerTick = 360, MaxTilt = 2400, TiltRatePerTick = 120, SpoolUpPerTick = 5, SpoolDownPerTick = 3, CeilingMm = 100000, HardLandMmS = 3000, Hp = 1100 },
        };
        public static HeliStats Get(HeliKind k) { return Table[(int)k]; }
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

        // mandos −1000..1000: colectivo (subir/bajar), cíclico adelante/atrás, cíclico lateral, pedales de guiñada
        public int Collective, CyclicFwd, CyclicSide, Pedals;

        public Helicopter(int id, HeliKind kind, int x, int y, int yaw, int groundZ = 0)
        { Id = id; Kind = kind; X = x; Y = y; Z = groundZ; Yaw = yaw & 0xFFFF; Hp = HeliCatalog.Get(kind).Hp; State = HeliState.EnTierra; }

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
            // rotor
            Rotor = EngineOn ? Math.Min(1000, Rotor + s.SpoolUpPerTick) : Math.Max(0, Rotor - s.SpoolDownPerTick);
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
            int vzTarget = Collective >= 0 ? (int)((long)Collective * s.MaxClimbMmS / 1000) : (int)((long)Collective * s.MaxDescentMmS / 1000);
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

        /// <summary>Daño por aplastar con el rotor o las ruedas: el helicóptero no atropella; el daño a la horda es de su artillería, no de esta clase.</summary>
        public bool IsAirborne { get { return State == HeliState.Volando; } }
    }
}
