using System;
using System.Collections.Generic;
using TBD.Net;

namespace TBD.Units
{
    // Infantería por pelotones (noche del 7-nov), sobre el modelo de armas de Hugo (balance_unidades.csv, D-N72). DENTRO de la simulación determinista:
    // enteros, xorshift propio, orden fijo de recorrido, estado en el hash y en el guardado.
    // Zona: la simulación de la zona (DOTS) da los objetivos; este módulo devuelve disparos e impactos y el que llama aplica las bajas.
    // Un impacto = un zombi abatido (TargetHit.Damage = KillDamage); las armas de Hugo abaten, no restan vida.

    public enum InfKind : byte { Fusilero = 0, Ametrallador = 1, Granadero = 2, Tirador = 3, Zapador = 4, Servidor = 5 }
    public enum Formation : byte { Linea = 0, Columna = 1, Cuna = 2, Circulo = 3 }

    /// <summary>Objetivo que le da la zona al pelotón: identificador estable y posición en mm.</summary>
    public struct Target { public int Id; public int X, Y; }

    public struct FireEvent
    {
        public int Platoon, Man; public InfKind Kind; public WeaponId Weapon; public int TargetId; public int AimX, AimY;
        public bool Hit; public bool Splash; public int FlightTicks;
    }
    public struct TargetHit { public int TargetId; public int Damage; public int Platoon; public WeaponId Weapon; }

    /// <summary>Explosión que cae este tick (para el efecto visual en el punto de impacto).</summary>
    public struct Blast { public int X, Y, RadiusMm; public WeaponId Weapon; }

    public sealed class StepResult
    {
        public readonly List<FireEvent> Shots = new List<FireEvent>();
        public readonly List<TargetHit> Hits = new List<TargetHit>();
        public readonly List<Blast> Blasts = new List<Blast>();
        public int AmmoSpent;                                  // disparos equivalentes gastados este tick
        public void Clear() { Shots.Clear(); Hits.Clear(); Blasts.Clear(); AmmoSpent = 0; }
    }

    public sealed class Infantryman
    {
        public int Id;
        public InfKind Kind;
        public int X, Y, Heading;
        public int Hp;
        public int W0 = -1, W1 = -1;        // arma principal y secundaria (WeaponId o −1)
        public int Cd0, Cd1;                // ticks hasta el siguiente disparo de cada arma
        public int DeployedFor;             // ticks emplazado (parado y con la orden)
        public bool Alive { get { return Hp > 0; } }
        public int SlotX, SlotY;
        public bool Moving, Firing;         // para la animación y el fogonazo
    }

    public sealed class Platoon
    {
        public int Id;
        public PlatoonKind Kind;
        public readonly List<Infantryman> Men = new List<Infantryman>();
        public int AX, AY, Heading;
        public Formation Form = Formation.Cuna;
        public bool Moving, AttackMove; public int TX, TY; public int FinalHeading = -1;
        public bool Deploy;                  // orden de emplazar: parado, y los emplazables empiezan a prepararse
        public bool HoldFire;
        public int SpacingMm = 2200;
        public int SpeedMmS;
        public int AmmoShots, AmmoCap;       // munición propia en disparos equivalentes
        public int AmmoSpent, Kills;
        public int LastFireTick = -1000;
        public int AliveCount() { int n = 0; for (int i = 0; i < Men.Count; i++) if (Men[i].Alive) n++; return n; }
    }

    struct Pending { public int DueTick, X, Y, Platoon; public WeaponId Weapon; }

    public sealed class UnitsWorld
    {
        public const int KillDamage = 100;
        public readonly List<Platoon> Platoons = new List<Platoon>();
        public IRng Rng;
        public int Tick;
        public int StockShots;                           // almacén común (disparos equivalentes): de él se reabastecen los pelotones
        int _nextPlatoon = 1, _nextMan = 1;
        readonly List<Pending> _pending = new List<Pending>();
        readonly List<int> _scratch = new List<int>();
        public const int TurnPerTick = 2500;
        public const int PlatoonTurnPerTick = 1200;
        public const int EngageHoldTicks = 45;           // ataque en marcha: se para mientras alguien haya disparado hace menos de 1,5 s
        public const int DenseNeighbours = 3, DenseRadiusMm = 1500;

        public UnitsWorld(uint seed) { Rng = new IRng(seed); }

        // ------------------------------------------------------------ alta
        public Platoon AddPlatoon(PlatoonKind kind, int x, int y, int heading, Formation form = Formation.Cuna)
        {
            var spec = PlatoonCatalog.Get(kind);
            var p = new Platoon { Id = _nextPlatoon++, Kind = kind, AX = x, AY = y, Heading = heading & 0xFFFF, Form = form, SpeedMmS = spec.SpeedMmS };
            p.AmmoCap = (int)((long)spec.DotationPm * WeaponCatalog.ShotsPerU / 1000); p.AmmoShots = p.AmmoCap;
            var list = new List<int>();
            for (int w = 0; w < spec.WeaponCount.Length; w++) for (int n = 0; n < spec.WeaponCount[w]; n++) list.Add(w);
            for (int i = 0; i < spec.Persons; i++)
            {
                int prim = i < list.Count ? list[i] : (int)WeaponId.Fusil;
                var m = new Infantryman { Id = _nextMan++, X = x, Y = y, Heading = p.Heading, Hp = spec.Hp, W0 = prim };
                if (WeaponCatalog.Table[prim].MinCluster > 1) m.W1 = (int)WeaponId.Fusil;     // el granadero y el zapador tienen fusil de reserva
                m.Kind = KindOf(prim);
                p.Men.Add(m);
            }
            for (int i = 0; i < spec.Servers; i++) p.Men.Add(new Infantryman { Id = _nextMan++, X = x, Y = y, Heading = p.Heading, Hp = spec.Hp, Kind = InfKind.Servidor });
            Platoons.Add(p);
            ComputeSlots(p);
            for (int i = 0; i < p.Men.Count; i++) { p.Men[i].X = p.Men[i].SlotX; p.Men[i].Y = p.Men[i].SlotY; }
            return p;
        }

        static InfKind KindOf(int w)
        {
            switch ((WeaponId)w)
            {
                case WeaponId.FusilPrecision: case WeaponId.FusilAntimaterial: return InfKind.Tirador;
                case WeaponId.MgLigera: case WeaponId.MgEmplazada: case WeaponId.MgPesada: case WeaponId.MgPuerta: return InfKind.Ametrallador;
                case WeaponId.Lanzagranadas: return InfKind.Granadero;
                case WeaponId.CargaZapador: return InfKind.Zapador;
                default: return InfKind.Fusilero;
            }
        }

        public Platoon Find(int id) { for (int i = 0; i < Platoons.Count; i++) if (Platoons[i].Id == id) return Platoons[i]; return null; }

        // ------------------------------------------------------------ órdenes (cada una es una orden de la simulación)
        /// <summary>Mover: va al punto sin pararse; dispara por el camino con las armas que se pueden usar en marcha.</summary>
        public bool Move(int platoon, int x, int y, int finalHeading = -1) { return GoTo(platoon, x, y, finalHeading, false); }
        /// <summary>Atacar moviéndose: va al punto, pero se detiene mientras haya enemigos a tiro y reanuda al terminar.</summary>
        public bool AttackMove(int platoon, int x, int y, int finalHeading = -1) { return GoTo(platoon, x, y, finalHeading, true); }

        bool GoTo(int platoon, int x, int y, int finalHeading, bool attack)
        {
            var p = Find(platoon); if (p == null || p.AliveCount() == 0) return false;
            p.Moving = true; p.AttackMove = attack; p.Deploy = false; p.TX = x; p.TY = y; p.FinalHeading = finalHeading < 0 ? -1 : finalHeading & 0xFFFF; return true;
        }

        /// <summary>Mantener: se queda donde está y dispara a lo que entre en alcance.</summary>
        public bool Hold(int platoon) { var p = Find(platoon); if (p == null) return false; p.Moving = false; p.AttackMove = false; return true; }
        public bool Stop(int platoon) { return Hold(platoon); }

        /// <summary>Emplazar: se para y orienta el pelotón al rumbo dado; las armas con despliegue (ametralladora emplazada) empiezan a prepararse.</summary>
        public bool DeployAt(int platoon, int heading)
        {
            var p = Find(platoon); if (p == null || p.AliveCount() == 0) return false;
            p.Moving = false; p.AttackMove = false; p.Deploy = true; p.FinalHeading = heading & 0xFFFF; return true;
        }

        public bool SetFormation(int platoon, Formation f) { var p = Find(platoon); if (p == null) return false; p.Form = f; return true; }
        public bool SetHoldFire(int platoon, bool hold) { var p = Find(platoon); if (p == null) return false; p.HoldFire = hold; return true; }

        /// <summary>Pasa munición del almacén común al pelotón hasta su dotación. Devuelve lo que pasó.</summary>
        public int Resupply(int platoon)
        {
            var p = Find(platoon); if (p == null) return 0;
            int n = Math.Min(StockShots, p.AmmoCap - p.AmmoShots); if (n <= 0) return 0;
            StockShots -= n; p.AmmoShots += n; return n;
        }

        public bool DamageMan(int platoon, int manId, int damage)
        {
            var p = Find(platoon); if (p == null) return false;
            for (int i = 0; i < p.Men.Count; i++)
                if (p.Men[i].Id == manId && p.Men[i].Alive) { p.Men[i].Hp -= damage; return !p.Men[i].Alive; }
            return false;
        }

        public void NoteKill(int platoon) { var p = Find(platoon); if (p != null) p.Kills++; }

        // ------------------------------------------------------------ formación
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

        // ------------------------------------------------------------ un tick
        public void Step(IList<Target> targets, StepResult res)
        {
            res.Clear();
            Tick++;
            ResolvePending(targets, res);
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
            if (!p.Moving || (p.AttackMove && Tick - p.LastFireTick < EngageHoldTicks))
            {
                if (p.FinalHeading >= 0) { int d = IMath.Diff(p.FinalHeading, p.Heading); p.Heading = (p.Heading + IMath.Clamp(d, -PlatoonTurnPerTick, PlatoonTurnPerTick)) & 0xFFFF; if (d == 0 && !p.Moving) p.FinalHeading = -1; }
                return;
            }
            int dx = p.TX - p.AX, dy = p.TY - p.AY, dist = IMath.Dist(dx, dy);
            int speed = p.SpeedMmS;
            int lag = 0;
            for (int i = 0; i < p.Men.Count; i++) { var m = p.Men[i]; if (m.Alive) lag = Math.Max(lag, IMath.Dist(m.SlotX - m.X, m.SlotY - m.Y)); }
            if (lag > 4 * p.SpacingMm) speed /= 2;
            int step = speed / IMath.TickRate;
            if (dist > 600)
            {
                int want = IMath.Atan2(dy, dx), d = IMath.Diff(want, p.Heading);
                p.Heading = (p.Heading + IMath.Clamp(d, -PlatoonTurnPerTick, PlatoonTurnPerTick)) & 0xFFFF;
            }
            if (dist <= step) { p.AX = p.TX; p.AY = p.TY; p.Moving = false; p.AttackMove = false; }
            else { p.AX += IMath.DivR((long)dx * step, dist); p.AY += IMath.DivR((long)dy * step, dist); }
        }

        void MoveMen(Platoon p)
        {
            for (int i = 0; i < p.Men.Count; i++)
            {
                var m = p.Men[i]; m.Moving = false; m.Firing = false;
                if (!m.Alive) continue;
                int dx = m.SlotX - m.X, dy = m.SlotY - m.Y, dist = IMath.Dist(dx, dy);
                int maxStep = p.SpeedMmS * 5 / 4 / IMath.TickRate;
                if (dist > 0)
                {
                    m.Moving = dist > 80;
                    if (dist <= maxStep) { m.X = m.SlotX; m.Y = m.SlotY; }
                    else { m.X += IMath.DivR((long)dx * maxStep, dist); m.Y += IMath.DivR((long)dy * maxStep, dist); }
                    if (dist > 250) { int d = IMath.Diff(IMath.Atan2(dy, dx), m.Heading); m.Heading = (m.Heading + IMath.Clamp(d, -TurnPerTick, TurnPerTick)) & 0xFFFF; }
                }
                if (!m.Moving && dist <= 250) { int d = IMath.Diff(p.Heading, m.Heading); m.Heading = (m.Heading + IMath.Clamp(d, -TurnPerTick, TurnPerTick)) & 0xFFFF; }
                // emplazar: parado y con la orden
                if (p.Deploy && !p.Moving && !m.Moving) m.DeployedFor++; else m.DeployedFor = 0;
            }
        }

        // ------------------------------------------------------------ disparo
        int Pick(Weapon w, Infantryman m, Platoon p, IList<Target> targets)
        {
            long best = long.MaxValue; int bi = -1;
            long r2 = (long)w.RangeMm * w.RangeMm, min2 = (long)w.MinRangeMm * w.MinRangeMm;
            for (int t = 0; t < targets.Count; t++)
            {
                long ex = targets[t].X - m.X, ey = targets[t].Y - m.Y, d2 = ex * ex + ey * ey;
                if (d2 > r2 || d2 < min2) continue;
                if (d2 > best || (d2 == best && targets[t].Id > targets[bi].Id)) continue;
                if (w.HalfConeBam > 0 && Math.Abs(IMath.Diff(IMath.Atan2((int)ey, (int)ex), p.Heading)) > w.HalfConeBam) continue;
                best = d2; bi = t;
            }
            return bi;
        }

        static int CountNear(IList<Target> targets, int x, int y, int radius, int stopAt)
        {
            long r2 = (long)radius * radius; int n = 0;
            for (int t = 0; t < targets.Count; t++)
            {
                long ex = targets[t].X - x, ey = targets[t].Y - y;
                if (ex * ex + ey * ey <= r2 && ++n >= stopAt) break;
            }
            return n;
        }

        void FireMen(Platoon p, IList<Target> targets, StepResult res)
        {
            for (int i = 0; i < p.Men.Count; i++)
            {
                var m = p.Men[i];
                if (!m.Alive) continue;
                if (m.Cd0 > 0) m.Cd0--; if (m.Cd1 > 0) m.Cd1--;
                if (p.HoldFire || targets == null || targets.Count == 0) continue;
                for (int slot = 0; slot < 2; slot++)
                {
                    int wid = slot == 0 ? m.W0 : m.W1; if (wid < 0) continue;
                    if ((slot == 0 ? m.Cd0 : m.Cd1) > 0) continue;
                    var w = WeaponCatalog.Table[wid];
                    if (w.DeployTicks > 0 && !(p.Deploy && m.DeployedFor >= w.DeployTicks)) continue;
                    if (p.AmmoShots < w.CostShots) continue;
                    int bi = Pick(w, m, p, targets);
                    if (bi < 0) continue;
                    var tg = targets[bi];
                    if (w.MinCluster > 1 && CountNear(targets, tg.X, tg.Y, w.RadiusMm, w.MinCluster) < w.MinCluster) continue;   // el granadero usa el fusil
                    Fire(p, m, slot, (WeaponId)wid, w, tg, targets, res);
                    break;
                }
            }
        }

        void Fire(Platoon p, Infantryman m, int slot, WeaponId wid, Weapon w, Target tg, IList<Target> targets, StepResult res)
        {
            int dist = IMath.Dist(tg.X - m.X, tg.Y - m.Y);
            if (slot == 0) m.Cd0 = w.CadenceTicks; else m.Cd1 = w.CadenceTicks;
            m.Heading = IMath.Atan2(tg.Y - m.Y, tg.X - m.X);
            m.Firing = true; p.LastFireTick = Tick;
            p.AmmoShots -= w.CostShots; p.AmmoSpent += w.CostShots; res.AmmoSpent += w.CostShots;
            var ev = new FireEvent { Platoon = p.Id, Man = m.Id, Kind = m.Kind, Weapon = wid, TargetId = tg.Id, AimX = tg.X, AimY = tg.Y, FlightTicks = w.FlightTicks };
            bool directHit = false;
            if (w.HitBlankPm > 0)
            {
                int pm = w.HitBlankPm - w.HitSlopePmPerM * (dist / 1000);
                if (pm < w.HitMinPm) pm = w.HitMinPm;
                if (pm > w.HitBlankPm) pm = w.HitBlankPm;
                directHit = Rng.Below(1000) < pm;
                ev.Hit = directHit;
                if (directHit)
                {
                    res.Hits.Add(new TargetHit { TargetId = tg.Id, Damage = KillDamage, Platoon = p.Id, Weapon = wid });
                    // perforación: con horda densa, un zombi extra detrás del primero al 50 %
                    if (w.Pierce > 0 && CountNear(targets, tg.X, tg.Y, DenseRadiusMm, DenseNeighbours + 1) > DenseNeighbours)
                        for (int k = 0; k < w.Pierce; k++)
                        {
                            int ex = PickBehind(m, tg, targets, res, wid, p.Id);
                            if (ex >= 0 && Rng.Below(2) == 0) res.Hits.Add(new TargetHit { TargetId = targets[ex].Id, Damage = KillDamage, Platoon = p.Id, Weapon = wid });
                        }
                }
            }
            if (w.Explosive)
            {
                ev.Splash = true; if (w.HitBlankPm == 0) ev.Hit = true;
                if (w.FlightTicks > 0) _pending.Add(new Pending { DueTick = Tick + w.FlightTicks, X = tg.X, Y = tg.Y, Platoon = p.Id, Weapon = wid });
                else Explode(tg.X, tg.Y, w, wid, p.Id, directHit ? tg.Id : -1, targets, res);
            }
            res.Shots.Add(ev);
        }

        int PickBehind(Infantryman m, Target tg, IList<Target> targets, StepResult res, WeaponId wid, int platoon)
        {
            long best = long.MaxValue; int bi = -1; long r2 = (long)DenseRadiusMm * DenseRadiusMm;
            int ux = tg.X - m.X, uy = tg.Y - m.Y;
            for (int t = 0; t < targets.Count; t++)
            {
                if (targets[t].Id == tg.Id) continue;
                long ex = targets[t].X - tg.X, ey = targets[t].Y - tg.Y, d2 = ex * ex + ey * ey;
                if (d2 > r2 || ex * ux + ey * uy <= 0) continue;
                if (d2 < best || (d2 == best && targets[t].Id < targets[bi].Id)) { best = d2; bi = t; }
            }
            return bi;
        }

        void Explode(int x, int y, Weapon w, WeaponId wid, int platoon, int exclude, IList<Target> targets, StepResult res)
        {
            res.Blasts.Add(new Blast { X = x, Y = y, RadiusMm = w.RadiusMm, Weapon = wid });
            long r2 = (long)w.RadiusMm * w.RadiusMm;
            _scratch.Clear();
            for (int t = 0; t < targets.Count; t++)
            {
                long ex = targets[t].X - x, ey = targets[t].Y - y;
                if (targets[t].Id != exclude && ex * ex + ey * ey <= r2) _scratch.Add(t);
            }
            // de más cerca a más lejos, con el identificador como desempate: el tope de bajas siempre alcanza a los mismos
            _scratch.Sort((a, b) =>
            {
                long ax = targets[a].X - x, ay = targets[a].Y - y, bx = targets[b].X - x, by = targets[b].Y - y;
                long da = ax * ax + ay * ay, db = bx * bx + by * by;
                return da != db ? da.CompareTo(db) : targets[a].Id.CompareTo(targets[b].Id);
            });
            int kills = exclude >= 0 ? 1 : 0;
            for (int k = 0; k < _scratch.Count && kills < w.CapKills; k++)
                if (Rng.Below(1000) < w.LethalityPm) { res.Hits.Add(new TargetHit { TargetId = targets[_scratch[k]].Id, Damage = KillDamage, Platoon = platoon, Weapon = wid }); kills++; }
        }

        void ResolvePending(IList<Target> targets, StepResult res)
        {
            if (_pending.Count == 0) return;
            for (int i = 0; i < _pending.Count;)
            {
                var q = _pending[i];
                if (q.DueTick > Tick) { i++; continue; }
                _pending.RemoveAt(i);
                if (targets != null) Explode(q.X, q.Y, WeaponCatalog.Table[(int)q.Weapon], q.Weapon, q.Platoon, -1, targets, res);
            }
        }

        // ------------------------------------------------------------ hash y guardado
        public ulong Hash()
        {
            ulong h = IMath.Mix(0x1AF0 ^ (ulong)Tick) + IMath.Mix(0x1AF1 ^ Rng.S) + WeaponCatalog.Hash() + PlatoonCatalog.Hash() + IMath.Mix(0x1AF2 ^ (ulong)(uint)StockShots);
            for (int i = 0; i < _pending.Count; i++)
                h += IMath.Mix((ulong)(uint)_pending[i].DueTick + ((ulong)(uint)_pending[i].X << 20) + ((ulong)(uint)_pending[i].Y << 40) + ((ulong)_pending[i].Weapon << 60) ^ (ulong)(i + 1));
            for (int i = 0; i < Platoons.Count; i++)
            {
                var p = Platoons[i];
                ulong ph = IMath.Mix((ulong)(uint)p.Id + ((ulong)(uint)p.AX << 20) + ((ulong)(uint)p.AY << 40) + (ulong)p.Heading * 0x9E3779B1UL);
                ph ^= IMath.Mix((ulong)(uint)p.TX + ((ulong)(uint)p.TY << 32) + ((p.Moving ? 1UL : 0UL) << 63) + ((p.HoldFire ? 1UL : 0UL) << 62) + ((p.AttackMove ? 1UL : 0UL) << 61) + ((p.Deploy ? 1UL : 0UL) << 60) + ((ulong)p.Form << 56) + ((ulong)(uint)(p.FinalHeading + 1) << 20));
                ph += IMath.Mix((ulong)(uint)p.SpacingMm + ((ulong)(uint)p.AmmoSpent << 24) + ((ulong)(uint)p.Kills << 48)) + IMath.Mix((ulong)(uint)p.AmmoShots + ((ulong)(uint)p.AmmoCap << 32) + 11)
                    + IMath.Mix((ulong)(uint)(p.LastFireTick + 1000) + ((ulong)p.Kind << 40) + ((ulong)(uint)p.SpeedMmS << 20));
                for (int j = 0; j < p.Men.Count; j++)
                {
                    var m = p.Men[j];
                    ph += IMath.Mix((ulong)(uint)m.Id + ((ulong)(uint)m.X << 24) + ((ulong)(uint)m.Y << 44)) ^ IMath.Mix((ulong)(uint)m.Hp + ((ulong)m.Heading << 32) + ((ulong)(uint)m.Cd0 << 48) + ((ulong)m.Kind << 40))
                        ^ IMath.Mix((ulong)(uint)m.Cd1 + ((ulong)(uint)(m.W0 + 1) << 20) + ((ulong)(uint)(m.W1 + 1) << 28) + ((ulong)(uint)m.DeployedFor << 36) + 13);
                }
                h += IMath.Mix(ph ^ (ulong)(i + 1));
            }
            return h;
        }

        public void SaveState(SaveWriter w)
        {
            w.Int(Tick); w.UInt(Rng.S); w.Int(_nextPlatoon); w.Int(_nextMan); w.Int(StockShots);
            w.Int(_pending.Count);
            foreach (var q in _pending) { w.Int(q.DueTick); w.Int(q.X); w.Int(q.Y); w.Int(q.Platoon); w.Byte((byte)q.Weapon); }
            w.Int(Platoons.Count);
            foreach (var p in Platoons)
            {
                w.Int(p.Id); w.Byte((byte)p.Kind); w.Int(p.AX); w.Int(p.AY); w.Int(p.Heading); w.Byte((byte)p.Form); w.Bool(p.Moving); w.Bool(p.AttackMove); w.Int(p.TX); w.Int(p.TY); w.Int(p.FinalHeading);
                w.Bool(p.Deploy); w.Bool(p.HoldFire); w.Int(p.SpacingMm); w.Int(p.SpeedMmS); w.Int(p.AmmoShots); w.Int(p.AmmoCap); w.Int(p.AmmoSpent); w.Int(p.Kills); w.Int(p.LastFireTick);
                w.Int(p.Men.Count);
                foreach (var m in p.Men)
                {
                    w.Int(m.Id); w.Byte((byte)m.Kind); w.Int(m.X); w.Int(m.Y); w.Int(m.Heading); w.Int(m.Hp); w.Int(m.W0); w.Int(m.W1); w.Int(m.Cd0); w.Int(m.Cd1); w.Int(m.DeployedFor);
                    w.Int(m.SlotX); w.Int(m.SlotY); w.Bool(m.Moving); w.Bool(m.Firing);
                }
            }
        }

        public void LoadState(SaveReader r)
        {
            Tick = r.Int(); Rng.S = r.UInt(); _nextPlatoon = r.Int(); _nextMan = r.Int(); StockShots = r.Int();
            _pending.Clear();
            int nq = r.Int(); if (nq < 0 || nq > 100000) throw new SaveException(SaveError.Corrupt, "disparos en vuelo no válidos");
            for (int i = 0; i < nq; i++) _pending.Add(new Pending { DueTick = r.Int(), X = r.Int(), Y = r.Int(), Platoon = r.Int(), Weapon = (WeaponId)r.Byte() });
            Platoons.Clear();
            int np = r.Int(); if (np < 0 || np > 10000) throw new SaveException(SaveError.Corrupt, "número de pelotones no válido");
            for (int i = 0; i < np; i++)
            {
                var p = new Platoon { Id = r.Int(), Kind = (PlatoonKind)r.Byte(), AX = r.Int(), AY = r.Int(), Heading = r.Int(), Form = (Formation)r.Byte(), Moving = r.Bool(), AttackMove = r.Bool(), TX = r.Int(), TY = r.Int(), FinalHeading = r.Int() };
                p.Deploy = r.Bool(); p.HoldFire = r.Bool(); p.SpacingMm = r.Int(); p.SpeedMmS = r.Int(); p.AmmoShots = r.Int(); p.AmmoCap = r.Int(); p.AmmoSpent = r.Int(); p.Kills = r.Int(); p.LastFireTick = r.Int();
                if ((int)p.Kind >= PlatoonCatalog.Table.Length) throw new SaveException(SaveError.Corrupt, "tipo de pelotón no válido");
                int nm = r.Int(); if (nm < 0 || nm > 1000) throw new SaveException(SaveError.Corrupt, "número de soldados no válido");
                for (int j = 0; j < nm; j++)
                {
                    var m = new Infantryman { Id = r.Int(), Kind = (InfKind)r.Byte() };
                    m.X = r.Int(); m.Y = r.Int(); m.Heading = r.Int(); m.Hp = r.Int(); m.W0 = r.Int(); m.W1 = r.Int(); m.Cd0 = r.Int(); m.Cd1 = r.Int(); m.DeployedFor = r.Int();
                    if (m.W0 >= WeaponCatalog.Table.Length || m.W1 >= WeaponCatalog.Table.Length) throw new SaveException(SaveError.Corrupt, "arma no válida");
                    m.SlotX = r.Int(); m.SlotY = r.Int(); m.Moving = r.Bool(); m.Firing = r.Bool();
                    p.Men.Add(m);
                }
                Platoons.Add(p);
            }
        }
    }
}
