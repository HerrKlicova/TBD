using System;
using System.Collections.Generic;
using TBD.Net;

namespace TBD.Units
{
    // Infantería por pelotones (noche del 7-nov). DENTRO de la simulación determinista: enteros, xorshift propio, orden fijo de recorrido,
    // estado en el hash y en el guardado. Las ESTADÍSTICAS son provisionales y de Hugo (balance): se cambian con InfCatalog.Set o la tabla
    // InfCatalog.Table antes de crear el mundo. Armas de referencia: ejército de tierra español (G36E, MG4, lanzagranadas de 40 mm, fusil de precisión).

    public enum InfKind : byte { Jefe = 0, Fusilero = 1, Ametrallador = 2, Granadero = 3, Tirador = 4 }
    public enum Formation : byte { Linea = 0, Columna = 1, Cuna = 2, Circulo = 3 }

    public struct InfStats
    {
        public string Name;
        public int Hp;
        public int SpeedMmS;                // andando en formación
        public int RangeMm, MinRangeMm;
        public int CooldownTicks;           // entre disparos (a 30 Hz)
        public int Damage;                  // por impacto (un zombi normal tiene 100 de vida en el campo de objetivos)
        public int HitNearPm, HitFarPm;     // probabilidad de impacto en milésimas: a quemarropa y al alcance máximo
        public int SplashMm;                // > 0: explosivo, daño en área
        public int ScatterMm;               // dispersión del explosivo al alcance máximo
        public int AmmoPerShot;             // coste en «disparos equivalentes» (ZoneAmmo: 1 u = 2.000)
    }

    public static class InfCatalog
    {
        /// <summary>Indexada por InfKind. Valores de partida, a calibrar por Hugo.</summary>
        public static InfStats[] Table = Defaults();

        static InfStats[] Defaults()
        {
            return new[]
            {
                // Jefe de pelotón (fusil G36E, más prudente)
                new InfStats { Name = "Jefe de pelotón", Hp = 110, SpeedMmS = 2600, RangeMm = 32000, MinRangeMm = 0, CooldownTicks = 15, Damage = 100, HitNearPm = 850, HitFarPm = 400, AmmoPerShot = 1 },
                // Fusilero (G36E, 5,56 mm)
                new InfStats { Name = "Fusilero", Hp = 100, SpeedMmS = 2600, RangeMm = 40000, MinRangeMm = 0, CooldownTicks = 15, Damage = 100, HitNearPm = 850, HitFarPm = 450, AmmoPerShot = 1 },
                // Ametrallador (MG4, 5,56 mm): ráfagas cortas, poco preciso pero cae mucho plomo
                new InfStats { Name = "Ametrallador", Hp = 100, SpeedMmS = 2300, RangeMm = 50000, MinRangeMm = 0, CooldownTicks = 3, Damage = 55, HitNearPm = 450, HitFarPm = 200, AmmoPerShot = 1 },
                // Granadero (lanzagranadas de 40 mm): área, lento y con alcance mínimo
                new InfStats { Name = "Granadero", Hp = 100, SpeedMmS = 2400, RangeMm = 35000, MinRangeMm = 8000, CooldownTicks = 90, Damage = 130, HitNearPm = 1000, HitFarPm = 1000, SplashMm = 4000, ScatterMm = 3000, AmmoPerShot = 10 },
                // Tirador de precisión: alcance largo, un disparo cada segundo
                new InfStats { Name = "Tirador", Hp = 90, SpeedMmS = 2500, RangeMm = 70000, MinRangeMm = 5000, CooldownTicks = 36, Damage = 200, HitNearPm = 970, HitFarPm = 800, AmmoPerShot = 2 },
            };
        }

        public static InfStats Get(InfKind k) { return Table[(int)k]; }
        public static void Set(InfKind k, InfStats s) { Table[(int)k] = s; }
        public static void Reset() { Table = Defaults(); }

        public static ulong Hash()
        {
            ulong h = 0x1F00;
            for (int i = 0; i < Table.Length; i++)
            {
                var s = Table[i];
                h = IMath.Mix(h ^ (ulong)(uint)s.Hp) + IMath.Mix((ulong)(uint)s.SpeedMmS + ((ulong)(uint)s.RangeMm << 20)) + IMath.Mix((ulong)(uint)s.CooldownTicks + ((ulong)(uint)s.Damage << 20) + ((ulong)(uint)s.HitNearPm << 40))
                    + IMath.Mix((ulong)(uint)s.HitFarPm + ((ulong)(uint)s.SplashMm << 20) + ((ulong)(uint)s.MinRangeMm << 40)) + IMath.Mix((ulong)(uint)s.AmmoPerShot + ((ulong)(uint)s.ScatterMm << 20));
            }
            return h;
        }
    }

    /// <summary>Objetivo que le da la simulación de la zona al pelotón: identificador estable y posición en mm. El que llama aplica el daño.</summary>
    public struct Target { public int Id; public int X, Y; }

    public struct FireEvent
    {
        public int Platoon, Man; public InfKind Kind; public int TargetId; public int AimX, AimY;
        public bool Hit; public int Damage; public bool Splash;
    }
    public struct TargetHit { public int TargetId; public int Damage; public int Platoon; }

    public sealed class StepResult
    {
        public readonly List<FireEvent> Shots = new List<FireEvent>();
        public readonly List<TargetHit> Hits = new List<TargetHit>();
        public int AmmoSpent;
        public void Clear() { Shots.Clear(); Hits.Clear(); AmmoSpent = 0; }
    }

    public sealed class Infantryman
    {
        public int Id;
        public InfKind Kind;
        public int X, Y, Heading;
        public int Hp;
        public int Cooldown;
        public bool Alive { get { return Hp > 0; } }
        public int SlotX, SlotY;            // posición que le toca (se recalcula cada tick; solo lectura para la vista)
        public bool Moving;                  // se movió este tick (para la animación)
        public bool Firing;                  // disparó este tick (para el fogonazo)
    }

    public sealed class Platoon
    {
        public int Id;
        public readonly List<Infantryman> Men = new List<Infantryman>();
        public int AX, AY, Heading;          // centro del pelotón (el del jefe) y rumbo
        public Formation Form = Formation.Cuna;
        public bool Moving; public int TX, TY; public int FinalHeading = -1;
        public bool HoldFire;
        public int SpacingMm = 2200;
        public int AmmoSpent;
        public int Kills;                    // impactos que han abatido: lo cuenta quien aplica el daño con NoteKill
        public int AliveCount() { int n = 0; for (int i = 0; i < Men.Count; i++) if (Men[i].Alive) n++; return n; }
    }

    public static class Presets
    {
        public static readonly InfKind[] Fusileros = { InfKind.Jefe, InfKind.Fusilero, InfKind.Fusilero, InfKind.Fusilero, InfKind.Fusilero, InfKind.Fusilero, InfKind.Fusilero, InfKind.Ametrallador, InfKind.Granadero, InfKind.Tirador };
        public static readonly InfKind[] Ametralladores = { InfKind.Jefe, InfKind.Ametrallador, InfKind.Ametrallador, InfKind.Ametrallador, InfKind.Fusilero, InfKind.Fusilero, InfKind.Fusilero, InfKind.Fusilero };
        public static readonly InfKind[] Apoyo = { InfKind.Jefe, InfKind.Granadero, InfKind.Granadero, InfKind.Tirador, InfKind.Tirador, InfKind.Fusilero, InfKind.Fusilero, InfKind.Fusilero };
    }

    /// <summary>Todos los pelotones. Se pasa por Step con los objetivos del tick; devuelve disparos e impactos para que la zona aplique el daño.</summary>
    public sealed class UnitsWorld
    {
        public readonly List<Platoon> Platoons = new List<Platoon>();
        public IRng Rng;
        public int Tick;
        int _nextPlatoon = 1, _nextMan = 1;
        public const int TurnPerTick = 2500;             // bam16 por tick que gira un soldado (≈ 220°/s)
        public const int PlatoonTurnPerTick = 1200;      // la formación gira más despacio
        public const int MovingHitPm = 700;              // disparar en marcha: 70 % de la puntería

        public UnitsWorld(uint seed) { Rng = new IRng(seed); }

        // ------------------------------------------------------------ órdenes (cada una es una orden de la simulación)
        public Platoon AddPlatoon(InfKind[] composition, int x, int y, int heading, Formation form = Formation.Cuna)
        {
            var p = new Platoon { Id = _nextPlatoon++, AX = x, AY = y, Heading = heading & 0xFFFF, Form = form };
            for (int i = 0; i < composition.Length; i++)
                p.Men.Add(new Infantryman { Id = _nextMan++, Kind = composition[i], X = x, Y = y, Heading = p.Heading, Hp = InfCatalog.Get(composition[i]).Hp });
            Platoons.Add(p);
            ComputeSlots(p);
            for (int i = 0; i < p.Men.Count; i++) { p.Men[i].X = p.Men[i].SlotX; p.Men[i].Y = p.Men[i].SlotY; }
            return p;
        }

        public Platoon Find(int id) { for (int i = 0; i < Platoons.Count; i++) if (Platoons[i].Id == id) return Platoons[i]; return null; }

        public bool Move(int platoon, int x, int y, int finalHeading = -1)
        {
            var p = Find(platoon); if (p == null || p.AliveCount() == 0) return false;
            p.Moving = true; p.TX = x; p.TY = y; p.FinalHeading = finalHeading < 0 ? -1 : finalHeading & 0xFFFF; return true;
        }
        public bool Stop(int platoon) { var p = Find(platoon); if (p == null) return false; p.Moving = false; return true; }
        public bool SetFormation(int platoon, Formation f) { var p = Find(platoon); if (p == null) return false; p.Form = f; return true; }
        public bool SetHoldFire(int platoon, bool hold) { var p = Find(platoon); if (p == null) return false; p.HoldFire = hold; return true; }

        /// <summary>Daño que la zona le hace a un soldado (zombi en contacto). Devuelve true si muere.</summary>
        public bool DamageMan(int platoon, int manId, int damage)
        {
            var p = Find(platoon); if (p == null) return false;
            for (int i = 0; i < p.Men.Count; i++)
                if (p.Men[i].Id == manId && p.Men[i].Alive) { p.Men[i].Hp -= damage; return !p.Men[i].Alive; }
            return false;
        }

        // ------------------------------------------------------------ formación
        /// <summary>Posiciones de los puestos de la formación, para los soldados vivos en orden (el primero es el centro).</summary>
        public static void Slot(Formation f, int k, int n, int spacing, int heading, out int dx, out int dy)
        {
            int fwd = 0, right = 0;
            switch (f)
            {
                case Formation.Linea: { int o = (k + 1) / 2; right = (k & 1) == 1 ? o * spacing : -o * spacing; } break;
                case Formation.Columna: fwd = -k * spacing; break;
                case Formation.Cuna: { int row = (k + 1) / 2; int side = (k & 1) == 1 ? 1 : -1; if (k == 0) { row = 0; side = 0; } fwd = -row * spacing; right = side * row * spacing; } break;
                case Formation.Circulo:
                    if (k > 0)
                    {
                        int m = Math.Max(1, n - 1);
                        int r = Math.Max(2 * spacing, (int)((long)spacing * m * 1000 / 6283));
                        int a = (int)((long)(k - 1) * IMath.Turn / m);
                        fwd = IMath.DivR((long)r * IMath.Cos(a), IMath.One); right = IMath.DivR((long)r * IMath.Sin(a), IMath.One);
                    }
                    break;
            }
            int c = IMath.Cos(heading), s = IMath.Sin(heading);
            dx = IMath.DivR((long)fwd * c + (long)right * s, IMath.One);
            dy = IMath.DivR((long)fwd * s - (long)right * c, IMath.One);
        }

        static void ComputeSlots(Platoon p)
        {
            int n = p.AliveCount(), k = 0;
            for (int i = 0; i < p.Men.Count; i++)
            {
                var m = p.Men[i]; if (!m.Alive) continue;
                int dx, dy; Slot(p.Form, k, n, p.SpacingMm, p.Heading, out dx, out dy);
                m.SlotX = p.AX + dx; m.SlotY = p.AY + dy; k++;
            }
        }

        static int PlatoonSpeed(Platoon p)
        {
            int v = int.MaxValue;
            for (int i = 0; i < p.Men.Count; i++) if (p.Men[i].Alive) v = Math.Min(v, InfCatalog.Get(p.Men[i].Kind).SpeedMmS);
            return v == int.MaxValue ? 0 : v;
        }

        // ------------------------------------------------------------ un tick
        public void Step(IList<Target> targets, StepResult res)
        {
            res.Clear();
            Tick++;
            for (int pi = 0; pi < Platoons.Count; pi++)
            {
                var p = Platoons[pi];
                if (p.AliveCount() == 0) continue;
                MovePlatoon(p);
                ComputeSlots(p);
                MoveMen(p);
                FireMen(p, targets, res);
            }
        }

        void MovePlatoon(Platoon p)
        {
            if (!p.Moving)
            {
                if (p.FinalHeading >= 0) { int d = IMath.Diff(p.FinalHeading, p.Heading); p.Heading = (p.Heading + IMath.Clamp(d, -PlatoonTurnPerTick, PlatoonTurnPerTick)) & 0xFFFF; if (d == 0) p.FinalHeading = -1; }
                return;
            }
            int dx = p.TX - p.AX, dy = p.TY - p.AY, dist = IMath.Dist(dx, dy);
            int speed = PlatoonSpeed(p);
            // cohesión: si alguien va muy rezagado, la formación espera a medias
            int lag = 0;
            for (int i = 0; i < p.Men.Count; i++) { var m = p.Men[i]; if (m.Alive) lag = Math.Max(lag, IMath.Dist(m.SlotX - m.X, m.SlotY - m.Y)); }
            if (lag > 4 * p.SpacingMm) speed /= 2;
            int step = speed / IMath.TickRate;
            if (dist > 600)
            {
                int want = IMath.Atan2(dy, dx), d = IMath.Diff(want, p.Heading);
                p.Heading = (p.Heading + IMath.Clamp(d, -PlatoonTurnPerTick, PlatoonTurnPerTick)) & 0xFFFF;
            }
            if (dist <= step) { p.AX = p.TX; p.AY = p.TY; p.Moving = false; }
            else { p.AX += IMath.DivR((long)dx * step, dist); p.AY += IMath.DivR((long)dy * step, dist); }
        }

        void MoveMen(Platoon p)
        {
            for (int i = 0; i < p.Men.Count; i++)
            {
                var m = p.Men[i]; m.Moving = false; m.Firing = false;
                if (!m.Alive) continue;
                int dx = m.SlotX - m.X, dy = m.SlotY - m.Y, dist = IMath.Dist(dx, dy);
                int maxStep = InfCatalog.Get(m.Kind).SpeedMmS * 5 / 4 / IMath.TickRate;
                if (dist > 0)
                {
                    m.Moving = dist > 80;
                    if (dist <= maxStep) { m.X = m.SlotX; m.Y = m.SlotY; }
                    else { m.X += IMath.DivR((long)dx * maxStep, dist); m.Y += IMath.DivR((long)dy * maxStep, dist); }
                    if (dist > 250)
                    {
                        int d = IMath.Diff(IMath.Atan2(dy, dx), m.Heading);
                        m.Heading = (m.Heading + IMath.Clamp(d, -TurnPerTick, TurnPerTick)) & 0xFFFF;
                    }
                }
                if (!m.Moving && dist <= 250)
                {
                    int d = IMath.Diff(p.Heading, m.Heading);
                    m.Heading = (m.Heading + IMath.Clamp(d, -TurnPerTick, TurnPerTick)) & 0xFFFF;
                }
            }
        }

        void FireMen(Platoon p, IList<Target> targets, StepResult res)
        {
            for (int i = 0; i < p.Men.Count; i++)
            {
                var m = p.Men[i];
                if (!m.Alive) continue;
                if (m.Cooldown > 0) { m.Cooldown--; continue; }
                if (p.HoldFire || targets == null || targets.Count == 0) continue;
                var st = InfCatalog.Get(m.Kind);
                long best = long.MaxValue; int bi = -1;
                long r2 = (long)st.RangeMm * st.RangeMm, min2 = (long)st.MinRangeMm * st.MinRangeMm;
                for (int t = 0; t < targets.Count; t++)
                {
                    long ex = targets[t].X - m.X, ey = targets[t].Y - m.Y, d2 = ex * ex + ey * ey;
                    if (d2 > r2 || d2 < min2) continue;
                    if (d2 < best || (d2 == best && targets[t].Id < targets[bi].Id)) { best = d2; bi = t; }
                }
                if (bi < 0) continue;
                var tg = targets[bi];
                int dist = (int)IMath.Isqrt(best);
                // se gira hacia el blanco (el fogonazo sale aunque aún no esté de cara: es un prototipo)
                m.Heading = IMath.Atan2(tg.Y - m.Y, tg.X - m.X);
                m.Cooldown = st.CooldownTicks;
                m.Firing = true;
                res.AmmoSpent += st.AmmoPerShot; p.AmmoSpent += st.AmmoPerShot;
                var ev = new FireEvent { Platoon = p.Id, Man = m.Id, Kind = m.Kind, TargetId = tg.Id, AimX = tg.X, AimY = tg.Y, Damage = st.Damage };
                if (st.SplashMm > 0)
                {
                    int sc = (int)((long)st.ScatterMm * dist / st.RangeMm);
                    int ox = sc == 0 ? 0 : Rng.Below(2 * sc + 1) - sc, oy = sc == 0 ? 0 : Rng.Below(2 * sc + 1) - sc;
                    int cx = tg.X + ox, cy = tg.Y + oy;
                    ev.AimX = cx; ev.AimY = cy; ev.Splash = true; ev.Hit = true;
                    long s2 = (long)st.SplashMm * st.SplashMm;
                    for (int t = 0; t < targets.Count; t++)
                    {
                        long ex = targets[t].X - cx, ey = targets[t].Y - cy, d2 = ex * ex + ey * ey;
                        if (d2 > s2) continue;
                        int de = (int)IMath.Isqrt(d2);
                        int dmg = st.Damage * (1000 - 600 * de / st.SplashMm) / 1000;       // 100 % en el centro, 40 % en el borde
                        res.Hits.Add(new TargetHit { TargetId = targets[t].Id, Damage = dmg, Platoon = p.Id });
                    }
                }
                else
                {
                    int pm = st.HitNearPm + (int)((long)(st.HitFarPm - st.HitNearPm) * dist / st.RangeMm);
                    if (p.Moving) pm = pm * MovingHitPm / 1000;
                    ev.Hit = Rng.Below(1000) < pm;
                    if (ev.Hit) res.Hits.Add(new TargetHit { TargetId = tg.Id, Damage = st.Damage, Platoon = p.Id });
                }
                res.Shots.Add(ev);
            }
        }

        // ------------------------------------------------------------ hash y guardado
        public ulong Hash()
        {
            ulong h = IMath.Mix(0x1AF0 ^ (ulong)Tick) + IMath.Mix(0x1AF1 ^ Rng.S) + InfCatalog.Hash();
            for (int i = 0; i < Platoons.Count; i++)
            {
                var p = Platoons[i];
                ulong ph = IMath.Mix((ulong)(uint)p.Id + ((ulong)(uint)p.AX << 20) + ((ulong)(uint)p.AY << 40) + (ulong)p.Heading * 0x9E3779B1UL);
                ph ^= IMath.Mix((ulong)(uint)p.TX + ((ulong)(uint)p.TY << 32) + ((p.Moving ? 1UL : 0UL) << 63) + ((p.HoldFire ? 1UL : 0UL) << 62) + ((ulong)p.Form << 56) + ((ulong)(uint)(p.FinalHeading + 1) << 20));
                ph += IMath.Mix((ulong)(uint)p.SpacingMm + ((ulong)(uint)p.AmmoSpent << 24) + ((ulong)(uint)p.Kills << 48));
                for (int j = 0; j < p.Men.Count; j++)
                {
                    var m = p.Men[j];
                    ph += IMath.Mix((ulong)(uint)m.Id + ((ulong)(uint)m.X << 24) + ((ulong)(uint)m.Y << 44)) ^ IMath.Mix((ulong)(uint)m.Hp + ((ulong)m.Heading << 32) + ((ulong)(uint)m.Cooldown << 48) + ((ulong)m.Kind << 40));
                }
                h += IMath.Mix(ph ^ (ulong)(i + 1));
            }
            return h;
        }

        public void NoteKill(int platoon) { var p = Find(platoon); if (p != null) p.Kills++; }

        public void SaveState(SaveWriter w)
        {
            w.Int(Tick); w.UInt(Rng.S); w.Int(_nextPlatoon); w.Int(_nextMan);
            w.Int(Platoons.Count);
            foreach (var p in Platoons)
            {
                w.Int(p.Id); w.Int(p.AX); w.Int(p.AY); w.Int(p.Heading); w.Byte((byte)p.Form); w.Bool(p.Moving); w.Int(p.TX); w.Int(p.TY); w.Int(p.FinalHeading);
                w.Bool(p.HoldFire); w.Int(p.SpacingMm); w.Int(p.AmmoSpent); w.Int(p.Kills);
                w.Int(p.Men.Count);
                foreach (var m in p.Men)
                {
                    w.Int(m.Id); w.Byte((byte)m.Kind); w.Int(m.X); w.Int(m.Y); w.Int(m.Heading); w.Int(m.Hp); w.Int(m.Cooldown);
                    w.Int(m.SlotX); w.Int(m.SlotY); w.Bool(m.Moving); w.Bool(m.Firing);
                }
            }
        }

        public void LoadState(SaveReader r)
        {
            Tick = r.Int(); Rng.S = r.UInt(); _nextPlatoon = r.Int(); _nextMan = r.Int();
            Platoons.Clear();
            int np = r.Int(); if (np < 0 || np > 10000) throw new SaveException(SaveError.Corrupt, "número de pelotones no válido");
            for (int i = 0; i < np; i++)
            {
                var p = new Platoon { Id = r.Int(), AX = r.Int(), AY = r.Int(), Heading = r.Int(), Form = (Formation)r.Byte(), Moving = r.Bool(), TX = r.Int(), TY = r.Int(), FinalHeading = r.Int() };
                p.HoldFire = r.Bool(); p.SpacingMm = r.Int(); p.AmmoSpent = r.Int(); p.Kills = r.Int();
                int nm = r.Int(); if (nm < 0 || nm > 1000) throw new SaveException(SaveError.Corrupt, "número de soldados no válido");
                for (int j = 0; j < nm; j++)
                {
                    var m = new Infantryman { Id = r.Int(), Kind = (InfKind)r.Byte() };
                    if ((int)m.Kind >= InfCatalog.Table.Length) throw new SaveException(SaveError.Corrupt, "tipo de soldado no válido");
                    m.X = r.Int(); m.Y = r.Int(); m.Heading = r.Int(); m.Hp = r.Int(); m.Cooldown = r.Int(); m.SlotX = r.Int(); m.SlotY = r.Int(); m.Moving = r.Bool(); m.Firing = r.Bool();
                    p.Men.Add(m);
                }
                Platoons.Add(p);
            }
        }
    }
}
