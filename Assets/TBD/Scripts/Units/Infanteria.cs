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
    /// <summary>Bloque (Lucía, 7-nov; Eugenio quiere «dibujar un cuadrado»): sustituye al Círculo en las órdenes del jugador. Circulo se queda solo para partidas guardadas.</summary>
    public enum Formation : byte { Linea = 0, Columna = 1, Cuna = 2, Circulo = 3, Bloque = 4 }

    /// <summary>Objetivo que le da la zona al pelotón: identificador estable y posición en mm.</summary>
    public struct Target { public int Id; public int X, Y; public bool Special; }      // Special: zombi especial (los tiradores lo prefieren)

    public struct FireEvent
    {
        public int Platoon, Man; public InfKind Kind; public WeaponId Weapon; public int TargetId; public int AimX, AimY;
        public bool Hit; public bool Splash; public int FlightTicks;
        public int FromX, FromY;               // dónde está el tirador
        public int ArcPeakMm;                  // altura del vértice de la parábola (granadas, cohetes con vuelo); 0 = tiro directo
    }
    public struct TargetHit { public int TargetId; public int Damage; public int Platoon; public WeaponId Weapon; }

    /// <summary>Explosión que cae este tick (para el efecto visual en el punto de impacto).</summary>
    public struct Blast { public int X, Y, RadiusMm; public WeaponId Weapon; public int Platoon, InRadius, Hits; }   // Platoon, InRadius y Hits: solo para medir (fuera del hash)

    public struct Casualty { public int Platoon, Man, X, Y; }
    /// <summary>Contacto de la horda con un pelotón en un tick: zombis pegados a sus soldados (suma de contactos) y vida quitada.</summary>
    public struct PlatoonHurt { public int Platoon, Contacts, Damage; }
    /// <summary>Resultado de UnitsWorld.ZombieContact: quién ha caído y cuánto contacto hubo este tick.</summary>
    public sealed class ContactResult
    {
        public readonly List<Casualty> Fallen = new List<Casualty>();
        public int Contacts, Touches, Damage;                 // contactos zombi-soldado, y vida quitada
        public readonly List<PlatoonHurt> Hurt = new List<PlatoonHurt>();   // por pelotón con contacto este tick (para «puesto bajo ataque» en el mapa); solo salida, no entra en el hash
        public void Clear() { Fallen.Clear(); Hurt.Clear(); Contacts = Touches = Damage = 0; }
    }

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
        public int AmmoSpent, Kills, Losses;      // Losses: soldados caídos del pelotón (por contacto de zombis)
        public int LastFireTick = -1000;
        public int EyeMm = UnitsWorld.EyeInfantryMm;   // D-N174: altura de la boca (atalaya 5,6 m; azotea 12,6 m)
        public int C90Left, C90Cap;           // D-N137: tiros de C90 que quedan y dotación (se reponen del almacén en Resupply, a arma_c90_coste_disparos cada uno)
        public bool C90Order; public int C90X, C90Y;   // orden de disparar el C90 a ese punto: se cumple en el siguiente Step
        internal List<PointMm> Route; internal int RouteI, RouteTX = int.MinValue, RouteTY, StuckTick, StuckX, StuckY, RouteX0, RouteY0;   // camino por la rejilla (S1 de Eugenio, I-Noche110): no se guarda; se rehace igual en los dos mundos
        public int Aboard;                   // 0 = a pie; si no, id del transporte (vehículo = su Id, helicóptero = 1000 + su Id): ni se mueve, ni dispara, ni lo tocan los zombis
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
        /// <summary>D-N139 (colisiones): obstáculos en mm (edificios, muros, el tren parado y los vehículos) que la infantería no atraviesa: los rodea deslizando por su borde.
        /// Vacía = como antes (sin cambio de hash). Los pone quien simula (la zona o NightSim) y se pueden cambiar cada tick (vehículos que se mueven).</summary>
        public readonly List<RectMm> Obstacles = new List<RectMm>();
        /// <summary>D-N174 (Lucía, 8-nov, a petición de Eugenio): sólidos que tapan el fuego directo (edificios, vagones y tren, muros, puertas, relieve), en 2,5D: cada uno con su altura.
        /// Un tirador con los ojos a «eyeMm» dispara por encima de lo que sea más bajo. El muro propio (OwnWall) no tapa a quien está a OwnWallReachMm (2 m) o menos de él.
        /// Los zombis y la pila no tapan. Vacía = sin regla, como siempre (no mueve los goldens). Off = tramo caído (brecha): ya no tapa.</summary>
        public struct LosBlocker { public RectMm R; public int HeightMm; public bool OwnWall, Off; }
        public readonly List<LosBlocker> LosBlockers = new List<LosBlocker>();
        public const int EyeInfantryMm = 1600, OwnWallReachMm = 2000;
        public long LosChecks;      // contador para medir el coste (no entra en el hash)

        public const int TargetHeightMm = 1000;   // Álvaro (8-nov): el blanco, a 1,0 m (zombi de pie, a la altura del pecho)
        /// <summary>¿Ve (sx, sy), con la boca del arma a eyeMm, a (tx, ty), con el blanco a TargetHeightMm? 2,5D: el rayo baja (o sube) en línea recta y un sólido tapa si
        /// el rayo pasa por debajo de su altura en algún punto de su huella. Liang–Barsky en enteros (fracciones con long): sin coma flotante, determinista.</summary>
        public bool LineOfSight(int sx, int sy, int tx, int ty, int eyeMm = EyeInfantryMm)
        {
            int bx0 = Math.Min(sx, tx), bx1 = Math.Max(sx, tx), by0 = Math.Min(sy, ty), by1 = Math.Max(sy, ty);
            for (int i = 0; i < LosBlockers.Count; i++)
            {
                var b = LosBlockers[i]; var r = b.R;
                if (b.Off || r.X0 > bx1 || r.X1 < bx0 || r.Y0 > by1 || r.Y1 < by0) continue;
                if (b.HeightMm <= Math.Min(eyeMm, TargetHeightMm)) continue;                       // más bajo que los dos extremos del rayo: no tapa nunca
                if (b.OwnWall) { long ddx = sx < r.X0 ? r.X0 - sx : sx > r.X1 ? sx - r.X1 : 0, ddy = sy < r.Y0 ? r.Y0 - sy : sy > r.Y1 ? sy - r.Y1 : 0; if (ddx * ddx + ddy * ddy <= (long)OwnWallReachMm * OwnWallReachMm) continue; }
                LosChecks++;
                long ln, ld, hn, hd; if (!SegSpan(sx, sy, tx, ty, r, out ln, out ld, out hn, out hd)) continue;
                // altura del rayo en t: eye + (tgt − eye)·t. Es lineal: el mínimo dentro de [tin, tout] está en un extremo
                long dh = TargetHeightMm - eyeMm, n = dh < 0 ? hn : ln, d = dh < 0 ? hd : ld;
                if ((long)eyeMm * d + dh * n < (long)b.HeightMm * d) return false;
            }
            return true;
        }

        public static bool SegHitsRect(int x0, int y0, int x1, int y1, RectMm r) { long a, b, c, d; return SegSpan(x0, y0, x1, y1, r, out a, out b, out c, out d); }
        /// <summary>Tramo [ln/ld, hn/hd] de t ∈ [0, 1] en que el segmento está dentro del rect. false si no lo toca o si un extremo está dentro (el tirador en el tejado no se tapa).</summary>
        public static bool SegSpan(int x0, int y0, int x1, int y1, RectMm r, out long ln, out long ld, out long hn, out long hd)
        {
            ln = 0; ld = 1; hn = 1; hd = 1;
            if (In(x0, y0, r) || In(x1, y1, r)) return false;
            long dx = (long)x1 - x0, dy = (long)y1 - y0;
            return Clip(-dx, (long)x0 - r.X0, ref ln, ref ld, ref hn, ref hd) && Clip(dx, (long)r.X1 - x0, ref ln, ref ld, ref hn, ref hd)
                && Clip(-dy, (long)y0 - r.Y0, ref ln, ref ld, ref hn, ref hd) && Clip(dy, (long)r.Y1 - y0, ref ln, ref ld, ref hn, ref hd);
        }
        static bool In(int x, int y, RectMm r) { return x > r.X0 && x < r.X1 && y > r.Y0 && y < r.Y1; }
        // p·t ≤ q: si p &lt; 0, t ≥ q/p (entrada); si p &gt; 0, t ≤ q/p (salida). [ln/ld, hn/hd] es el tramo de t que queda; denominadores &gt; 0.
        static bool Clip(long p, long q, ref long ln, ref long ld, ref long hn, ref long hd)
        {
            if (p == 0) return q >= 0;
            long n = q, d = p; if (d < 0) { n = -n; d = -d; }
            if (p < 0) { if (n * ld > ln * d) { ln = n; ld = d; } }
            else { if (n * hd < hn * d) { hn = n; hd = d; } }
            return ln * hd <= hn * ld;
        }
        public const int ManRadiusMm = 300;
        bool Blocked(int x, int y)
        {
            for (int i = 0; i < Obstacles.Count; i++) { var r = Obstacles[i]; if (x > r.X0 - ManRadiusMm && x < r.X1 + ManRadiusMm && y > r.Y0 - ManRadiusMm && y < r.Y1 + ManRadiusMm) return true; }
            return false;
        }
        /// <summary>D-N139: de (x, y) a (nx, ny) sin entrar en un obstáculo. Prueba el paso entero, por su eje principal y de lado (izquierda y derecha); si nada vale, se queda.
        /// El que ya está dentro (p. ej. un obstáculo nuevo encima) puede salir. Entero y determinista.</summary>
        void Slide(int x, int y, ref int nx, ref int ny)
        {
            if (Obstacles.Count == 0 || !Blocked(nx, ny) || Blocked(x, y)) return;
            int dx = nx - x, dy = ny - y;
            if (dx != 0 && !Blocked(nx, y)) { ny = y; return; }                // por el eje de avance principal (sin quedarse quieto)
            if (dy != 0 && Math.Abs(dy) >= Math.Abs(dx) && !Blocked(x, ny)) { nx = x; return; }
            // de lado, hacia el borde más cercano del obstáculo que estorba (siempre el mismo lado: no oscila)
            int bi = -1; for (int i = 0; i < Obstacles.Count && bi < 0; i++) { var r0 = Obstacles[i]; if (nx > r0.X0 - ManRadiusMm && nx < r0.X1 + ManRadiusMm && ny > r0.Y0 - ManRadiusMm && ny < r0.Y1 + ManRadiusMm) bi = i; }
            int st = Math.Max(Math.Abs(dx), Math.Abs(dy)); var r = Obstacles[Math.Max(0, bi)];
            int sx = x, sy = y;
            if (Math.Abs(dx) >= Math.Abs(dy)) sy = y + (2L * y >= (long)r.Y0 + r.Y1 ? st : -st); else sx = x + (2L * x >= (long)r.X0 + r.X1 ? st : -st);
            if (!Blocked(sx, sy)) { nx = sx; ny = sy; return; }
            nx = x; ny = y;
        }

        public UnitsWorld(uint seed) { Rng = new IRng(seed); }

        // ------------------------------------------------------------ alta
        public Platoon AddPlatoon(PlatoonKind kind, int x, int y, int heading, Formation form = Formation.Cuna)
        {
            var spec = PlatoonCatalog.Get(kind);
            var p = new Platoon { Id = _nextPlatoon++, Kind = kind, AX = x, AY = y, Heading = heading & 0xFFFF, Form = form, SpeedMmS = spec.SpeedMmS };
            p.AmmoCap = (int)((long)spec.DotationPm * WeaponCatalog.ShotsPerU / 1000); p.AmmoShots = p.AmmoCap;
            p.C90Cap = p.C90Left = Math.Max(0, spec.C90Shots);
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

        /// <summary>Reparto de objetivos (Lucía, 9-nov, decisiones-noche): un lanzagranadas no dispara a un punto que ya tiene una granada en vuelo a menos de su radio; ese tirador usa el fusil.
        /// Medido en NightSim: los granaderos pasan de 347 a 437 abatidos por u en normal y Sagunto difícil sube a ~6470. Es regla, no estado: fuera del hash.</summary>
        public bool GrenadeDeconflict = true;
        bool GrenadeInbound(int x, int y, int r) { long r2 = (long)r * r; foreach (var q in _pending) { long dx = q.X - x, dy = q.Y - y; if (dx * dx + dy * dy < r2) return true; } return false; }
        public long DxResupShots, DxResupN;      // medida: lo que sale del almacén en recargas de pelotón (fuera del hash)
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

        /// <summary>Pasa munición del almacén común al pelotón hasta su dotación y repone los C90 gastados (cada uno cuesta arma_c90_coste_disparos). Devuelve lo que pasó (en disparos equivalentes).</summary>
        public int Resupply(int platoon)
        {
            var p = Find(platoon); if (p == null) return 0;
            int n = Math.Max(0, Math.Min(StockShots, p.AmmoCap - p.AmmoShots));
            StockShots -= n; p.AmmoShots += n;
            int c = WeaponCatalog.Table[(int)WeaponId.C90].CostShots;
            while (p.C90Left < p.C90Cap && StockShots >= c && c >= 0) { StockShots -= c; p.C90Left++; n += c; if (c == 0) { p.C90Left = p.C90Cap; break; } }
            if (n > 0) { DxResupShots += n; DxResupN++; }
            return n;
        }

        /// <summary>D-N137 (Lucía): orden del jugador «C90 (C)»: el pelotón dispara un C90 al punto (x, y) en el siguiente Step. Nunca automático.
        /// Se rechaza (false) si el pelotón no existe, no tiene vivos, va a bordo, no le quedan C90 o el punto está fuera de alcance (mínimo y máximo) de todos sus soldados.
        /// Dispara el soldado vivo más cercano al punto que lo tenga a tiro. No gasta munición propia: el C90 se paga al reponerlo. El alto el fuego (G) no lo impide: es una orden expresa.</summary>
        public bool OrderC90(int platoon, int x, int y)
        {
            var p = Find(platoon); if (p == null || p.Aboard != 0 || p.C90Left <= 0 || p.AliveCount() == 0) return false;
            if (C90Shooter(p, x, y) < 0) return false;
            p.C90Order = true; p.C90X = x; p.C90Y = y; return true;
        }

        int C90Shooter(Platoon p, int x, int y)
        {
            var w = WeaponCatalog.Table[(int)WeaponId.C90];
            long r2 = (long)w.RangeMm * w.RangeMm, min2 = (long)w.MinRangeMm * w.MinRangeMm, best = long.MaxValue; int bi = -1;
            for (int i = 0; i < p.Men.Count; i++)
            {
                var m = p.Men[i]; if (!m.Alive || m.Kind == InfKind.Servidor) continue;
                long ex = x - m.X, ey = y - m.Y, d2 = ex * ex + ey * ey;
                if (d2 > r2 || d2 < min2) continue;
                if (LosBlockers.Count > 0 && !LineOfSight(m.X, m.Y, x, y, p.EyeMm)) continue;      // D-N174: el C90 es fuego directo
                if (d2 < best) { best = d2; bi = i; }
            }
            return bi;
        }

        void FireC90(Platoon p, IList<Target> targets, StepResult res)
        {
            if (!p.C90Order) return;
            p.C90Order = false;
            if (p.C90Left <= 0) return;
            int mi = C90Shooter(p, p.C90X, p.C90Y); if (mi < 0) return;      // se movió y ya no llega: la orden se pierde y no gasta el tiro
            var m = p.Men[mi]; var w = WeaponCatalog.Table[(int)WeaponId.C90];
            p.C90Left--;
            m.Heading = IMath.Atan2(p.C90Y - m.Y, p.C90X - m.X); m.Firing = true; p.LastFireTick = Tick;
            Shoot(p.Id, m.Id, m.Kind, WeaponId.C90, w, m.X, m.Y, new Target { Id = -1, X = p.C90X, Y = p.C90Y }, targets ?? new List<Target>(), res, false);
        }

        public bool DamageMan(int platoon, int manId, int damage)
        {
            var p = Find(platoon); if (p == null) return false;
            for (int i = 0; i < p.Men.Count; i++)
                if (p.Men[i].Id == manId && p.Men[i].Alive) { p.Men[i].Hp -= damage; return !p.Men[i].Alive; }
            return false;
        }

        // ------------------------------------------------------------ monturas (vehículos y helicópteros) y embarque
        /// <summary>Generador de las monturas: NO entra en el hash (los vehículos y el helicóptero son locales en el prototipo; solo sus bajas entran como orden).</summary>
        public IRng MountRng = new IRng(0xB07);

        /// <summary>Una montura dispara a targets[targetIdx]: cadencia y munición los lleva quien llama (ref). Devuelve true si disparó. La explosión cae ya (sin impactos pendientes).
        /// «ownerId» sale en los eventos como Platoon (convención: vehículo −1 − Id, helicóptero −1001 − Id).</summary>
        public bool MountFire(int ownerId, int sx, int sy, WeaponId wid, ref int cooldown, ref int ammoShots, IList<Target> targets, int targetIdx, StepResult res)
        {
            if (cooldown > 0 || targetIdx < 0 || targets == null || targetIdx >= targets.Count) return false;
            var w = WeaponCatalog.Table[(int)wid];
            if (ammoShots < w.CostShots) return false;
            cooldown = w.CadenceTicks; ammoShots -= w.CostShots; res.AmmoSpent += w.CostShots;
            var saved = Rng; Rng = MountRng;
            Shoot(ownerId, 0, InfKind.Servidor, wid, w, sx, sy, targets[targetIdx], targets, res, true);
            MountRng = Rng; Rng = saved;
            return true;
        }

        /// <summary>Sube un pelotón entero a un transporte con «seatsFree» plazas libres. No sube si no cabe, ya va a bordo o no existe. Para en seco al pelotón.</summary>
        public bool Embark(int platoon, int carrierId, int seatsFree)
        {
            var p = Find(platoon); if (p == null || p.Aboard != 0) return false;
            int n = p.AliveCount(); if (n == 0 || n > seatsFree || carrierId == 0) return false;
            p.Aboard = carrierId; p.Moving = false; p.AttackMove = false; p.Deploy = false;
            return true;
        }

        /// <summary>El transporte lleva a sus pasajeros: pelotón y soldados quedan en (x, y) con ese rumbo.</summary>
        public void CarryAboard(int carrierId, int x, int y, int heading)
        {
            for (int i = 0; i < Platoons.Count; i++)
            {
                var p = Platoons[i]; if (p.Aboard != carrierId) continue;
                p.AX = x; p.AY = y; p.TX = x; p.TY = y; p.Heading = heading & 0xFFFF;
                for (int k = 0; k < p.Men.Count; k++) { p.Men[k].X = x; p.Men[k].Y = y; p.Men[k].Heading = heading & 0xFFFF; }
            }
        }

        /// <summary>Plazas ocupadas en un transporte.</summary>
        public int SeatsUsed(int carrierId) { int n = 0; for (int i = 0; i < Platoons.Count; i++) if (Platoons[i].Aboard == carrierId) n += Platoons[i].AliveCount(); return n; }

        /// <summary>Baja un pelotón en (x, y): se coloca allí con su formación y se queda parado. Devuelve false si no iba a bordo.</summary>
        public bool Disembark(int platoon, int x, int y, int heading)
        {
            var p = Find(platoon); if (p == null || p.Aboard == 0) return false;
            p.Aboard = 0; p.AX = p.TX = x; p.AY = p.TY = y; p.Heading = heading & 0xFFFF; p.Moving = false;
            for (int k = 0; k < p.Men.Count; k++) { p.Men[k].X = x; p.Men[k].Y = y; }
            return true;
        }

        // ------------------------------------------------------------ daño de los zombis (cuerpo a cuerpo)
        /// <summary>Regla (la misma que la zona, balance_combate: zombi_danio_tick y cuerpo_a_cuerpo_mm): cada zombi vivo a menos de «radiusMm» de un soldado
        /// le quita «dmgPerTick» de vida ESTE tick. Es proximidad pura: sin azar, sin orden (se cuentan contactos por soldado). Un zombi toca a todos los
        /// soldados de su radio. Devuelve los soldados caídos; «res» (opcional) acumula quién. Coste O(zombis) con una rejilla sobre los soldados.</summary>
        public int ZombieContact(IList<Target> zombies, int radiusMm, int dmgPerTick, ContactResult res)
        {
            if (res != null) res.Clear();
            if (zombies == null || zombies.Count == 0 || dmgPerTick <= 0 || radiusMm <= 0) return 0;
            int n = 0; int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            _cx.Clear(); _cy.Clear(); _cp.Clear(); _cm.Clear();
            for (int a = 0; a < Platoons.Count; a++)
            {
                var p = Platoons[a]; if (p.Aboard != 0) continue;
                for (int b = 0; b < p.Men.Count; b++)
                {
                    var m = p.Men[b]; if (!m.Alive) continue;
                    _cx.Add(m.X); _cy.Add(m.Y); _cp.Add(a); _cm.Add(b); n++;
                    if (m.X < minX) minX = m.X; if (m.X > maxX) maxX = m.X; if (m.Y < minY) minY = m.Y; if (m.Y > maxY) maxY = m.Y;
                }
            }
            if (n == 0) return 0;
            int cell = Math.Max(1, radiusMm);
            int gw = (maxX - minX) / cell + 1, gh = (maxY - minY) / cell + 1;
            while ((long)gw * gh > 4096) { cell *= 2; gw = (maxX - minX) / cell + 1; gh = (maxY - minY) / cell + 1; }
            if (_head.Length < gw * gh) _head = new int[gw * gh];
            for (int i = 0; i < gw * gh; i++) _head[i] = -1;
            if (_next.Length < n) _next = new int[n];
            if (_touch.Length < n) _touch = new int[n];
            for (int i = 0; i < n; i++) { int c = ((_cy[i] - minY) / cell) * gw + (_cx[i] - minX) / cell; _next[i] = _head[c]; _head[c] = i; _touch[i] = 0; }
            long r2 = (long)radiusMm * radiusMm; int touches = 0;
            for (int zi = 0; zi < zombies.Count; zi++)
            {
                var z = zombies[zi];
                if (z.X < minX - radiusMm || z.X > maxX + radiusMm || z.Y < minY - radiusMm || z.Y > maxY + radiusMm) continue;
                int c0 = Math.Max(0, (z.X - radiusMm - minX) / cell), c1 = Math.Min(gw - 1, (z.X + radiusMm - minX) / cell);
                int r0 = Math.Max(0, (z.Y - radiusMm - minY) / cell), r1 = Math.Min(gh - 1, (z.Y + radiusMm - minY) / cell);
                if (z.X - radiusMm < minX) c0 = 0; if (z.Y - radiusMm < minY) r0 = 0;
                for (int ry = r0; ry <= r1; ry++)
                    for (int rx = c0; rx <= c1; rx++)
                        for (int i = _head[ry * gw + rx]; i >= 0; i = _next[i])
                        {
                            long ex = z.X - _cx[i], ey = z.Y - _cy[i];
                            if (ex * ex + ey * ey <= r2) { _touch[i]++; touches++; }
                        }
            }
            int fallen = 0;
            if (res != null) { if (_pc.Length < Platoons.Count) { _pc = new int[Platoons.Count]; _pd = new int[Platoons.Count]; } for (int a = 0; a < Platoons.Count; a++) { _pc[a] = 0; _pd[a] = 0; } }
            for (int i = 0; i < n; i++)
            {
                if (_touch[i] == 0) continue;
                var p = Platoons[_cp[i]]; var m = p.Men[_cm[i]];
                int dmg = (int)Math.Min((long)_touch[i] * dmgPerTick, int.MaxValue / 2);
                m.Hp -= dmg;
                if (res != null) { res.Contacts += _touch[i]; res.Damage += dmg; _pc[_cp[i]] += _touch[i]; _pd[_cp[i]] += dmg; }
                if (!m.Alive) { fallen++; p.Losses++; if (res != null) res.Fallen.Add(new Casualty { Platoon = p.Id, Man = m.Id, X = m.X, Y = m.Y }); }
            }
            if (res != null) { res.Touches = touches; for (int a = 0; a < Platoons.Count; a++) if (_pc[a] > 0) res.Hurt.Add(new PlatoonHurt { Platoon = Platoons[a].Id, Contacts = _pc[a], Damage = _pd[a] }); }
            return fallen;
        }
        readonly List<int> _cx = new List<int>(), _cy = new List<int>(), _cp = new List<int>(), _cm = new List<int>();
        int[] _head = new int[0], _next = new int[0], _touch = new int[0], _pc = new int[0], _pd = new int[0];

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
                case Formation.Bloque:
                    {
                        // cuadrado lo más compacto posible: cols = ⌈√n⌉, filas que hagan falta; el jefe (k = 0) en la esquina delantera izquierda; el bloque va centrado en el ancla
                        int nn = Math.Max(1, n), cols = 1; while (cols * cols < nn) cols++;
                        int rows = (nn + cols - 1) / cols, row = k / cols, col = k % cols;
                        right = (2 * col - (cols - 1)) * spacing / 2; fwd = -(2 * row - (rows - 1)) * spacing / 2;
                    }
                    break;
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
                if (p.Aboard != 0) { for (int i = 0; i < p.Men.Count; i++) { p.Men[i].Moving = false; p.Men[i].Firing = false; } continue; }      // a bordo: lo lleva el transporte
                MovePlatoon(p);
                ComputeSlots(p);
                MoveMen(p);
                FireMen(p, targets, res);
                FireC90(p, targets, res);
            }
        }

        void MovePlatoon(Platoon p)
        {
            if (!p.Moving || (p.AttackMove && Tick - p.LastFireTick < EngageHoldTicks))
            {
                if (p.FinalHeading >= 0) { int d = IMath.Diff(p.FinalHeading, p.Heading); p.Heading = (p.Heading + IMath.Clamp(d, -PlatoonTurnPerTick, PlatoonTurnPerTick)) & 0xFFFF; if (d == 0 && !p.Moving) p.FinalHeading = -1; }
                return;
            }
            int gx = p.TX, gy = p.TY; bool last = true;
            if (Obstacles.Count > 0) NextWaypoint(p, out gx, out gy, out last);      // rodea los muros por las aberturas (sin obstáculos, el camino recto de siempre)
            int dx = gx - p.AX, dy = gy - p.AY, dist = IMath.Dist(dx, dy);
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
            if (dist <= step && !last) { p.AX = gx; p.AY = gy; p.RouteI++; }
            else if (dist <= step) { p.AX = p.TX; p.AY = p.TY; p.Moving = false; p.AttackMove = false; p.Route = null; }
            else { int nax = p.AX + IMath.DivR((long)dx * step, dist), nay = p.AY + IMath.DivR((long)dy * step, dist); Slide(p.AX, p.AY, ref nax, ref nay); p.AX = nax; p.AY = nay; }
        }

        NavGrid _nav; readonly List<RectMm> _navObs = new List<RectMm>(); const int NavCellMm = 1000, NavInflateMm = ManRadiusMm + 350, NavPadMm = 40000;

        /// <summary>Si el camino recto a la orden corta un obstáculo, A* por una rejilla de 1 m sobre los obstáculos inflados (la NavGrid de los vehículos, entera y con desempates fijos):
        /// el pelotón va de punto de paso en punto de paso. Se calcula al cambiar la orden y si lleva 2 s sin avanzar. Determinista: mismo estado, mismo camino en A y B.</summary>
        void NextWaypoint(Platoon p, out int gx, out int gy, out bool last)
        {
            bool nueva = p.Route == null || p.RouteTX != p.TX || p.RouteTY != p.TY;
            if (!nueva && Tick - p.StuckTick >= 2 * IMath.TickRate)
            {
                if (IMath.Dist(p.AX - p.StuckX, p.AY - p.StuckY) < 500) nueva = true;     // atascado: rehace el camino con los obstáculos de ahora
                p.StuckTick = Tick; p.StuckX = p.AX; p.StuckY = p.AY;
            }
            if (nueva)
            {
                p.RouteTX = p.TX; p.RouteTY = p.TY; p.RouteI = 0; p.RouteX0 = p.AX; p.RouteY0 = p.AY; p.StuckTick = Tick; p.StuckX = p.AX; p.StuckY = p.AY;
                if (p.Route == null) p.Route = new List<PointMm>(); p.Route.Clear();
                if (!SegmentBlocked(p.AX, p.AY, p.TX, p.TY) || !NavFor(p.AX, p.AY, p.TX, p.TY).FindPath(p.AX, p.AY, p.TX, p.TY, p.Route) || p.Route.Count == 0)
                { p.Route.Clear(); p.Route.Add(new PointMm(p.TX, p.TY)); }
                else p.Route[p.Route.Count - 1] = new PointMm(p.TX, p.TY);            // el destino exacto de la orden (los soldados lo ajustan con Slide)
            }
            while (p.RouteI < p.Route.Count - 1 && IMath.Dist(p.Route[p.RouteI].X - p.AX, p.Route[p.RouteI].Y - p.AY) <= 1200) p.RouteI++;   // punto de paso alcanzado: al siguiente sin frenar
            int i = Math.Min(p.RouteI, p.Route.Count - 1);
            gx = p.Route[i].X; gy = p.Route[i].Y; last = i >= p.Route.Count - 1;
        }

        /// <summary>¿El segmento corta algún obstáculo inflado con el radio del soldado? Muestras cada 250 mm (enteros).</summary>
        bool SegmentBlocked(int ax, int ay, int bx, int by)
        {
            int d = IMath.Dist(bx - ax, by - ay), n = Math.Max(1, d / 250);
            for (int k = 1; k <= n; k++) { int x = ax + (int)((long)(bx - ax) * k / n), y = ay + (int)((long)(by - ay) * k / n); if (Blocked(x, y)) return true; }
            return false;
        }

        NavGrid NavFor(int ax, int ay, int bx, int by)
        {
            bool igual = _nav != null && _navObs.Count == Obstacles.Count;
            if (igual) for (int i = 0; i < Obstacles.Count; i++) { var a = _navObs[i]; var b = Obstacles[i]; if (a.X0 != b.X0 || a.Y0 != b.Y0 || a.X1 != b.X1 || a.Y1 != b.Y1) { igual = false; break; } }
            if (igual && Math.Min(ax, bx) >= _nav.X0 && Math.Min(ay, by) >= _nav.Y0 && Math.Max(ax, bx) < _nav.X0 + _nav.W * _nav.Cell && Math.Max(ay, by) < _nav.Y0 + _nav.H * _nav.Cell) return _nav;
            int x0 = Math.Min(ax, bx), y0 = Math.Min(ay, by), x1 = Math.Max(ax, bx), y1 = Math.Max(ay, by);
            for (int i = 0; i < Obstacles.Count; i++) { var r = Obstacles[i]; x0 = Math.Min(x0, r.X0); y0 = Math.Min(y0, r.Y0); x1 = Math.Max(x1, r.X1); y1 = Math.Max(y1, r.Y1); }
            x0 -= NavPadMm; y0 -= NavPadMm; x1 += NavPadMm; y1 += NavPadMm;
            if ((long)(x1 - x0) * (y1 - y0) > 600L * 400 * 1000000) { x0 = Math.Min(ax, bx) - 150000; x1 = Math.Max(ax, bx) + 150000; y0 = Math.Min(ay, by) - 100000; y1 = Math.Max(ay, by) + 100000; }   // tope de memoria (mapas grandes)
            _nav = new NavGrid(x0, y0, x1, y1, NavCellMm); _nav.Build(Obstacles, NavInflateMm);
            _navObs.Clear(); _navObs.AddRange(Obstacles);
            return _nav;
        }

        /// <summary>Punto de paso para un soldado que no ve su puesto en la formación (muro por medio): el punto del camino del pelotón más avanzado que ve,
        /// o el ancla, o el arranque del camino. Determinista (orden fijo).</summary>
        void ManWaypoint(Platoon p, Infantryman m, ref int gx, ref int gy)
        {
            if (!SegmentBlocked(m.X, m.Y, p.AX, p.AY)) { gx = p.AX; gy = p.AY; return; }
            if (p.Route != null) for (int k = Math.Min(p.RouteI, p.Route.Count - 1); k >= 0; k--) { var q = p.Route[k]; if (!SegmentBlocked(m.X, m.Y, q.X, q.Y)) { gx = q.X; gy = q.Y; return; } }
            if (p.Route != null && !SegmentBlocked(m.X, m.Y, p.RouteX0, p.RouteY0)) { gx = p.RouteX0; gy = p.RouteY0; return; }
            // sin nada a la vista: camino propio por la rejilla hasta el ancla
            var tmp = _manPath; tmp.Clear();
            if (NavFor(m.X, m.Y, p.AX, p.AY).FindPath(m.X, m.Y, p.AX, p.AY, tmp) && tmp.Count > 0) { gx = tmp[0].X; gy = tmp[0].Y; }
        }
        readonly List<PointMm> _manPath = new List<PointMm>();

        void MoveMen(Platoon p)
        {
            for (int i = 0; i < p.Men.Count; i++)
            {
                var m = p.Men[i]; m.Moving = false; m.Firing = false;
                if (!m.Alive) continue;
                int gx = m.SlotX, gy = m.SlotY;
                if (Obstacles.Count > 0 && IMath.Dist(gx - m.X, gy - m.Y) > 1500 && SegmentBlocked(m.X, m.Y, gx, gy)) ManWaypoint(p, m, ref gx, ref gy);   // I-Noche153: el soldado no se pega al muro: sigue el camino del pelotón
                int dx = gx - m.X, dy = gy - m.Y, dist = IMath.Dist(dx, dy);
                int maxStep = p.SpeedMmS * 5 / 4 / IMath.TickRate;
                if (dist > 0)
                {
                    m.Moving = dist > 80;
                    int nx, ny;
                    if (dist <= maxStep) { nx = gx; ny = gy; }
                    else { nx = m.X + IMath.DivR((long)dx * maxStep, dist); ny = m.Y + IMath.DivR((long)dy * maxStep, dist); }
                    Slide(m.X, m.Y, ref nx, ref ny); m.X = nx; m.Y = ny;
                    if (dist > 250) { int d = IMath.Diff(IMath.Atan2(dy, dx), m.Heading); m.Heading = (m.Heading + IMath.Clamp(d, -TurnPerTick, TurnPerTick)) & 0xFFFF; }
                }
                if (!m.Moving && dist <= 250) { int d = IMath.Diff(p.Heading, m.Heading); m.Heading = (m.Heading + IMath.Clamp(d, -TurnPerTick, TurnPerTick)) & 0xFFFF; }
                // emplazar: parado y con la orden
                if (p.Deploy && !p.Moving && !m.Moving) m.DeployedFor++; else m.DeployedFor = 0;
            }
        }

        // ------------------------------------------------------------ disparo
        int Pick(Weapon w, Infantryman m, Platoon p, IList<Target> targets) { return PickAt(w, m.X, m.Y, p.Heading, w.HalfConeBam, targets, 0, p.EyeMm); }

        /// <summary>Blanco de un arma desde (sx, sy): el más cercano dentro del alcance y del arco (halfCone 0 = 360°; empate: id menor). «extraZ» (mm de altura del tirador)
        /// se suma en vertical al alcance (helicóptero). Si el arma prefiere especiales (Weapon.PrefersSpecials), un especial a tiro gana a cualquier normal.</summary>
        public int PickAt(Weapon w, int sx, int sy, int center, int halfCone, IList<Target> targets, int extraZ, int eyeMm = EyeInfantryMm)
        {
            long best = long.MaxValue; int bi = -1; bool bestSpecial = false;
            long r2 = (long)w.RangeMm * w.RangeMm - (long)extraZ * extraZ, min2 = (long)w.MinRangeMm * w.MinRangeMm;
            if (r2 <= 0) return -1;
            bool indirect = LosBlockers.Count > 0 && w.Key == "lanzagranadas";      // D-N174 (Lucía): el AG36 de 40 mm es tiro curvo, sin línea de visión
            for (int t = 0; t < targets.Count; t++)
            {
                long ex = targets[t].X - sx, ey = targets[t].Y - sy, d2 = ex * ex + ey * ey;
                if (d2 > r2 || d2 < min2) continue;
                bool sp = w.PrefersSpecials && targets[t].Special;
                if (bi >= 0 && bestSpecial && !sp) continue;
                bool better = bi < 0 || (sp && !bestSpecial) || d2 < best || (d2 == best && targets[t].Id < targets[bi].Id);
                if (!better) continue;
                if (halfCone > 0 && Math.Abs(IMath.Diff(IMath.Atan2((int)ey, (int)ex), center)) > halfCone) continue;
                if (extraZ == 0 && !indirect && LosBlockers.Count > 0 && !LineOfSight(sx, sy, targets[t].X, targets[t].Y, eyeMm)) continue;
                best = d2; bi = t; bestSpecial = sp;
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
                    if (GrenadeDeconflict && w.Explosive && w.FlightTicks > 0 && GrenadeInbound(tg.X, tg.Y, w.RadiusMm)) continue;   // reparto (Lucía, 9-nov): ese grupo ya tiene una granada en vuelo; usa el fusil
                    Fire(p, m, slot, (WeaponId)wid, w, tg, targets, res);
                    break;
                }
            }
        }

        void Fire(Platoon p, Infantryman m, int slot, WeaponId wid, Weapon w, Target tg, IList<Target> targets, StepResult res)
        {
            if (slot == 0) m.Cd0 = w.CadenceTicks; else m.Cd1 = w.CadenceTicks;
            m.Heading = IMath.Atan2(tg.Y - m.Y, tg.X - m.X);
            m.Firing = true; p.LastFireTick = Tick;
            p.AmmoShots -= w.CostShots; p.AmmoSpent += w.CostShots; res.AmmoSpent += w.CostShots;
            Shoot(p.Id, m.Id, m.Kind, wid, w, m.X, m.Y, tg, targets, res, false);
        }

        /// <summary>Resuelve un disparo desde (sx, sy): puntería por distancia, perforación y explosión. La munición y la cadencia las lleva quien llama.
        /// «immediate»: la explosión cae ya (monturas de vehículos y helicópteros, que no usan la lista de impactos pendientes del hash).</summary>
        void Shoot(int platoonId, int manId, InfKind kind, WeaponId wid, Weapon w, int sx, int sy, Target tg, IList<Target> targets, StepResult res, bool immediate)
        {
            int dist = IMath.Dist(tg.X - sx, tg.Y - sy);
            var ev = new FireEvent { Platoon = platoonId, Man = manId, Kind = kind, Weapon = wid, TargetId = tg.Id, AimX = tg.X, AimY = tg.Y, FlightTicks = immediate ? 0 : w.FlightTicks, FromX = sx, FromY = sy, ArcPeakMm = immediate ? 0 : ArcPeak(w.FlightTicks) };
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
                    res.Hits.Add(new TargetHit { TargetId = tg.Id, Damage = KillDamage, Platoon = platoonId, Weapon = wid });
                    // perforación: con horda densa, un zombi extra detrás del primero al 50 %
                    if (w.Pierce > 0 && CountNear(targets, tg.X, tg.Y, DenseRadiusMm, DenseNeighbours + 1) > DenseNeighbours)
                        for (int k = 0; k < w.Pierce; k++)
                        {
                            int ex = PickBehind(sx, sy, tg, targets);
                            if (ex >= 0 && Rng.Below(2) == 0) res.Hits.Add(new TargetHit { TargetId = targets[ex].Id, Damage = KillDamage, Platoon = platoonId, Weapon = wid });
                        }
                }
            }
            if (w.Explosive)
            {
                ev.Splash = true; if (w.HitBlankPm == 0) ev.Hit = true;
                if (w.FlightTicks > 0 && !immediate) _pending.Add(new Pending { DueTick = Tick + w.FlightTicks, X = tg.X, Y = tg.Y, Platoon = platoonId, Weapon = wid });
                else Explode(tg.X, tg.Y, w, wid, platoonId, directHit ? tg.Id : -1, targets, res);
            }
            res.Shots.Add(ev);
        }

        /// <summary>Altura del vértice de la parábola de una granada con ese tiempo de vuelo (g = 9,81 m/s²): g·T²/8. Para que la vista dibuje el arco.</summary>
        static int ArcPeak(int flightTicks) { if (flightTicks <= 0) return 0; long t = flightTicks; return (int)(9810L * t * t / (8L * IMath.TickRate * IMath.TickRate)); }

        int PickBehind(int sx, int sy, Target tg, IList<Target> targets)
        {
            long best = long.MaxValue; int bi = -1; long r2 = (long)DenseRadiusMm * DenseRadiusMm;
            int ux = tg.X - sx, uy = tg.Y - sy;
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
            int bi0 = res.Blasts.Count; res.Blasts.Add(new Blast { X = x, Y = y, RadiusMm = w.RadiusMm, Weapon = wid, Platoon = platoon });
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
            { var bb = res.Blasts[bi0]; bb.InRadius = _scratch.Count + (exclude >= 0 ? 1 : 0); bb.Hits = kills; res.Blasts[bi0] = bb; }
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
                ph += IMath.Mix((ulong)(uint)p.SpacingMm + ((ulong)(uint)p.AmmoSpent << 24) + ((ulong)(uint)p.Kills << 48)) + IMath.Mix((ulong)(uint)p.Losses + ((ulong)(uint)p.Aboard << 32) + 0x105) + IMath.Mix((ulong)(uint)p.AmmoShots + ((ulong)(uint)p.AmmoCap << 32) + 11)
                    + IMath.Mix((ulong)(uint)(p.LastFireTick + 1000) + ((ulong)p.Kind << 40) + ((ulong)(uint)p.SpeedMmS << 20))
                    + IMath.Mix((ulong)(uint)p.C90Left + ((ulong)(uint)p.C90Cap << 8) + ((p.C90Order ? 1UL : 0UL) << 16) + ((ulong)(uint)p.C90X << 17) + ((ulong)(uint)p.C90Y << 40) + 0xC90);
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
                w.Bool(p.Deploy); w.Bool(p.HoldFire); w.Int(p.SpacingMm); w.Int(p.SpeedMmS); w.Int(p.AmmoShots); w.Int(p.AmmoCap); w.Int(p.AmmoSpent); w.Int(p.Kills); w.Int(p.Losses); w.Int(p.Aboard); w.Int(p.LastFireTick);
                w.Int(p.C90Left); w.Int(p.C90Cap); w.Bool(p.C90Order); w.Int(p.C90X); w.Int(p.C90Y);
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
                p.Deploy = r.Bool(); p.HoldFire = r.Bool(); p.SpacingMm = r.Int(); p.SpeedMmS = r.Int(); p.AmmoShots = r.Int(); p.AmmoCap = r.Int(); p.AmmoSpent = r.Int(); p.Kills = r.Int(); p.Losses = r.Int(); p.Aboard = r.Int(); p.LastFireTick = r.Int();
                p.C90Left = r.Int(); p.C90Cap = r.Int(); p.C90Order = r.Bool(); p.C90X = r.Int(); p.C90Y = r.Int();
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
