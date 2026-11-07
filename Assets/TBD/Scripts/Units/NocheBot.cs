using System;
using System.Collections.Generic;

namespace TBD.Units
{
    // Bot de la noche (-nightbot, petición de Sara): reparte los pelotones por el muro hacia donde está la horda con ATAQUE-MOVIMIENTO y los reabastece al bajar del 30 % de su dotación.
    // Da las MISMAS órdenes que el jugador (UnitsWorld.AttackMove, DeployAt, Resupply), así que es determinista: depende solo del estado del mundo y de la lista de zombis
    // (mismo orden que la pasa la zona a Units.Step) y no usa azar ni coma flotante. La zona solo tiene que activarlo con la bandera y llamar a Step una vez por tick.
    // NightSim lo usa con NightSim.Bot = new NightBot(...). Ver Docs/nightbot.md.

    public sealed class NightBot
    {
        // ---- configuración (los valores por defecto son los medidos en NightSim)
        public readonly int X0, Y0, X1, Y1;            // recinto en mm (el rectángulo del muro; el interior es transitable por los pelotones)
        public readonly int StationX, StationY;        // edificio: la reserva (zapadores) espera allí
        public int DecideEveryTicks = 60;              // cada cuánto decide (2 s)
        public int ResupplyBelowPct = 30;              // reabastece a un pelotón al bajar de este % de su dotación
        public int ThreatBandMm = 40_000;              // los zombis fuera del muro a menos de esto cuentan como amenaza de ese lado
        public bool DeployMachineGuns = true;          // las ametralladoras se emplazan mirando al muro al llegar
        public bool Enabled = true;

        // ---- contadores (para el informe de Sara)
        public int Orders, Resupplies, Decisions, Reassignments;

        const int Sides = 4;                            // 0 = X− (oeste), 1 = X+ (este), 2 = Y−, 3 = Y+
        static readonly int[] OutwardHeading = { 32768, 0, 49152, 16384 };   // bam16: 0 = +X, crece hacia +Y
        static readonly string[] SideName = { "X−", "X+", "Y−", "Y+" };
        readonly int[] _threat = new int[Sides], _raw = new int[Sides], _along = new int[Sides], _count = new int[Sides];
        readonly long[] _alongSum = new long[Sides];
        readonly List<int> _id = new List<int>(), _side = new List<int>(), _lastX = new List<int>(), _lastY = new List<int>();   // por pelotón conocido

        public NightBot(int fortX0, int fortY0, int fortX1, int fortY1, int stationX, int stationY) { X0 = fortX0; Y0 = fortY0; X1 = fortX1; Y1 = fortY1; StationX = stationX; StationY = stationY; }
        public static NightBot FromParams(NightSimParams p) { return new NightBot(p.FortX0, p.FortY0, p.FortX1, p.FortY1, p.StationX, p.StationY); }

        public string Describe()
        {
            return $"NightBot: {Decisions} decisiones, {Orders} órdenes, {Reassignments} cambios de lado, {Resupplies} reabastecimientos; amenaza suavizada " +
                   $"{SideName[0]} {_threat[0]}, {SideName[1]} {_threat[1]}, {SideName[2]} {_threat[2]}, {SideName[3]} {_threat[3]}";
        }

        /// <summary>El lado del recinto al que se «pega» un zombi de fuera (el que más se ha pasado) o −1 si está dentro o lejos. «along» sale con la coordenada a lo largo de ese lado.</summary>
        int SideOf(int x, int y, out int along)
        {
            int ex0 = X0 - x, ex1 = x - X1, ey0 = Y0 - y, ey1 = y - Y1; along = 0;
            int best = Math.Max(Math.Max(ex0, ex1), Math.Max(ey0, ey1));
            if (best <= 0 || best > ThreatBandMm) return -1;
            if (best == ex0) { along = y; return 0; }
            if (best == ex1) { along = y; return 1; }
            if (best == ey0) { along = x; return 2; }
            along = x; return 3;
        }

        int Slot(int p) { for (int i = 0; i < _id.Count; i++) if (_id[i] == p) return i; _id.Add(p); _side.Add(-1); _lastX.Add(int.MinValue); _lastY.Add(int.MinValue); return _id.Count - 1; }

        /// <summary>Una vez por tick, ANTES de Units.Step, con la lista de zombis vivos. Solo decide cada DecideEveryTicks ticks.</summary>
        public void Step(UnitsWorld w, IList<Target> zombies, int tick)
        {
            if (!Enabled || w == null || tick % Math.Max(1, DecideEveryTicks) != 0) return;
            Decisions++;
            // 1) reabastecer: a cada pelotón con menos del umbral de su dotación (la misma orden del jugador)
            foreach (var p in w.Platoons) if (p.AmmoCap > 0 && p.AliveCount() > 0 && p.AmmoShots * 100 < p.AmmoCap * ResupplyBelowPct) if (w.Resupply(p.Id) > 0) Resupplies++;
            // 2) amenaza por lado, suavizada (media móvil entera) para que no salten de un lado a otro
            for (int s = 0; s < Sides; s++) { _raw[s] = 0; _alongSum[s] = 0; }
            if (zombies != null)
                for (int i = 0; i < zombies.Count; i++) { int al; int s = SideOf(zombies[i].X, zombies[i].Y, out al); if (s >= 0) { _raw[s]++; _alongSum[s] += al; } }
            long total = 0;
            for (int s = 0; s < Sides; s++)
            {
                _threat[s] = (_threat[s] * 3 + _raw[s] * 4 + 2) / 4;        // EMA: 3/4 lo anterior + 1/4 lo de ahora (escala ×4 para no perder los pocos)
                if (_raw[s] > 0) _along[s] = (int)(_alongSum[s] / _raw[s]);   // el centro de la horda a lo largo del lado
                total += _threat[s];
            }
            // 3) los pelotones de línea (todos menos zapadores) reparten el muro en proporción a la amenaza; sin amenaza se quedan donde están
            var line = new List<Platoon>(); Platoon reserve = null; var reserveList = new List<Platoon>();
            foreach (var p in w.Platoons) { if (p.AliveCount() == 0 || p.Aboard != 0) continue; if (p.Kind == PlatoonKind.Zapadores) reserveList.Add(p); else line.Add(p); Slot(p.Id); }
            foreach (var z in reserveList) { reserve = z; MoveIfNeeded(w, z, StationX, StationY + 6000 * (reserveList.IndexOf(z) + 1), 0, -1, false); }
            if (line.Count == 0) return;
            if (total >= 8)
            {
                var quota = new int[Sides]; int given = 0; var rem = new long[Sides];
                for (int s = 0; s < Sides; s++) { long share = _threat[s] * (long)line.Count; quota[s] = (int)(share / total); rem[s] = share % total; given += quota[s]; }
                while (given < line.Count) { int b = 0; for (int s = 1; s < Sides; s++) if (rem[s] > rem[b]) b = s; quota[b]++; rem[b] = -1; given++; }
                // quedarse donde ya están si el lado sigue teniendo cupo; el resto va a los lados con hueco, el más amenazado primero y el pelotón más cercano
                var assigned = new Dictionary<int, int>(); var free = new List<Platoon>(line); var have = new int[Sides];
                foreach (var p in line) { int cur = _side[Slot(p.Id)]; if (cur >= 0 && have[cur] < quota[cur]) { assigned[p.Id] = cur; have[cur]++; free.Remove(p); } }
                var order = new List<int>(); for (int s = 0; s < Sides; s++) order.Add(s);
                order.Sort((a, b) => _threat[b] != _threat[a] ? _threat[b].CompareTo(_threat[a]) : a.CompareTo(b));
                foreach (int s in order)
                    while (have[s] < quota[s] && free.Count > 0)
                    {
                        int tx, ty; Target(s, _along[s], 0, 1, out tx, out ty, PlatoonKind.Fusileros);
                        Platoon near = null; long bd = long.MaxValue; foreach (var p in free) { long d = IMath.Dist(p.AX - tx, p.AY - ty); if (d < bd) { bd = d; near = p; } }
                        assigned[near.Id] = s; have[s]++; free.Remove(near);
                    }
                foreach (var p in line) { int s = -1; if (assigned.TryGetValue(p.Id, out s)) { int slot = Slot(p.Id); if (_side[slot] != s) { _side[slot] = s; Reassignments++; } } }
            }
            // 4) las órdenes: cada pelotón a su sitio a lo largo de su lado (separados entre sí), con la distancia al muro que pide su arma
            for (int s = 0; s < Sides; s++)
            {
                var here = new List<Platoon>(); foreach (var p in line) if (_side[Slot(p.Id)] == s) here.Add(p);
                for (int k = 0; k < here.Count; k++)
                {
                    var p = here[k]; int tx, ty; Target(s, _along[s], k, here.Count, out tx, out ty, p.Kind);
                    MoveIfNeeded(w, p, tx, ty, OutwardHeading[s], p.Kind == PlatoonKind.Ametralladores && DeployMachineGuns ? s : -1, true);
                }
            }
        }

        /// <summary>Punto de espera de un pelotón a lo largo de un lado: centrado en la horda, repartidos cada 10 m, a la distancia del muro que su arma necesita.</summary>
        void Target(int side, int along, int k, int n, out int tx, out int ty, PlatoonKind kind)
        {
            int inset = kind == PlatoonKind.Granaderos ? 18_000 : kind == PlatoonKind.Tiradores ? 12_000 : kind == PlatoonKind.Ametralladores ? 4_000 : 3_000;   // lanzagranadas: mínimo de 15 m
            int off = (k * 2 - (n - 1)) * 5_000;                                      // −… 0 … + en pasos de 10 m
            if (side <= 1)
            {
                int y = IMath.Clamp(along + off, Y0 + 8_000, Y1 - 8_000), maxIn = (X1 - X0) / 2 - 2_000; inset = Math.Min(inset, maxIn);
                tx = side == 0 ? X0 + inset : X1 - inset; ty = y;
            }
            else
            {
                int x = IMath.Clamp(along + off, X0 + 8_000, X1 - 8_000), maxIn = (Y1 - Y0) / 2 - 2_000; inset = Math.Min(inset, maxIn);
                tx = x; ty = side == 2 ? Y0 + inset : Y1 - inset;
            }
        }

        void MoveIfNeeded(UnitsWorld w, Platoon p, int tx, int ty, int heading, int deploySide, bool attack)
        {
            int slot = Slot(p.Id); int dx = tx - _lastX[slot], dy = ty - _lastY[slot];
            bool targetMoved = _lastX[slot] == int.MinValue || IMath.Dist(dx, dy) > 4_000;
            int distNow = IMath.Dist(p.AX - tx, p.AY - ty);
            if (targetMoved || (!p.Moving && !p.Deploy && distNow > 3_000))
            {
                if (distNow <= 2_500 && !targetMoved) { }
                else { if (attack) w.AttackMove(p.Id, tx, ty, heading); else w.Move(p.Id, tx, ty, heading); _lastX[slot] = tx; _lastY[slot] = ty; Orders++; return; }
            }
            if (deploySide >= 0 && !p.Moving && !p.Deploy && distNow <= 3_000) { w.DeployAt(p.Id, OutwardHeading[deploySide]); Orders++; }
        }
    }
}
