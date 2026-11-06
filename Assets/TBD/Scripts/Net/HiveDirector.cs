using System;
using System.Collections.Generic;

namespace TBD.Net
{
    /// <summary>
    /// Hito 4, parte 2: el director de la Colmena (diseno-colmena-h4.md §2-§4 de Lucía, presupuesto de Hugo). Una vez por hora de
    /// juego decide con utilidades enteras entre EMBOSCADA (memoria de tráfico ≥ 600 en la próxima franja), CORTE DE VÍA
    /// (memoria ≥ 850) y ASEDIO (nodo con gente, desde el día 2 con aviso; como mucho uno a la vez en la etapa 1, D-N08).
    /// Nunca crea zombis: mueve densidad de la mancha desde celdas cercanas (conservación). Todo determinista y en enteros.
    /// </summary>
    public sealed class HiveDirector
    {
        public enum PlanKind : byte { Emboscada = 0, Corte = 1, Asedio = 2 }

        public struct Plan
        {
            public PlanKind Kind;
            public int Edge;           // emboscada y corte
            public int Node;           // asedio
            public int Cell;           // celda de la mancha donde se concentra
            public long UntilTick;
            public int Moved;          // densidad (por mil) que movió hasta aquí
        }

        public readonly List<Plan> Active = new List<Plan>();
        public int MaxSieges = 1;                     // etapa 1 (D-N08)
        public long BudgetUsedToday;                  // densidad movida hoy (por mil)
        long _budgetDay = -1;
        long _lastSiegeTick = -1_000_000;
        public const long SiegeCooldownTicks = 2L * NetConst.DayTicks;   // un asedio nuevo cada 2 días como mucho (PROTOTIPO, para Hugo)

        public const int HoursAhead = 2;              // la emboscada se prepara 1-2 h antes (Lucía)
        public const int SiegeMinDay = 1;             // día 2 en adelante (Tick / DayTicks >= 1)

        /// <summary>Presupuesto del día en «por mil» de densidad movible: H(día) de Hugo / 10 zombis por mil, con la rampa de D-N08.</summary>
        public static long DailyBudgetPermil(long day, long[] curve, long h0 = 1600, long kHuellaMil = 160, long huellaMil = 2000)
        {
            long table = curve != null && day < curve.Length ? curve[day] : 1000 + day * 50;
            long root;
            long h = BalanceMath.HiveBudget(h0, table, kHuellaMil, huellaMil, out root);
            long ramp = day == 0 ? 150 : day == 1 ? 500 : day == 2 ? 800 : 1000;      // D-N08: 15 %, 50 %, 80 %
            return h * ramp / 1000 / NetWorld.ZombiesPerPermil;
        }

        public long[] Curve;                          // colmena_curva.csv (si no se carga, una aproximación lineal)

        /// <summary>Una hora de juego: caducan los planes, se elige la acción de mayor utilidad y se ejecuta.</summary>
        public void Hourly(NetWorld w)
        {
            long day = w.Tick / NetConst.DayTicks;
            if (day != _budgetDay) { _budgetDay = day; BudgetUsedToday = 0; }
            Active.RemoveAll(p => w.Tick >= p.UntilTick);
            long budget = DailyBudgetPermil(day, Curve) - BudgetUsedToday;
            if (budget <= 0) return;

            int nextSlot = w.Traffic.SlotOf(w.Tick + NetConst.Hours(HoursAhead));
            long bestU = 0;
            Plan best = default;
            bool found = false;

            // emboscadas y cortes: tramos con memoria alta en la franja que viene, en un punto de emboscada
            foreach (var e in w.Graph.Edges)
            {
                int m = w.Traffic.M[e.Id, nextSlot];
                if (m < w.Traffic.P.Ambush || e.Ambush.Count == 0 || e.State == EdgeState.Cortada) continue;
                if (Active.Exists(p => p.Edge == e.Id && p.Kind != PlanKind.Asedio)) continue;
                var ap = e.Ambush[0];
                int cell = CellAtKm(w, e, ap.KmMm);
                bool cut = m >= w.Traffic.P.Cut;
                long u = (long)m * (cut ? 3 : 2);                                     // valor × oportunidad (PROTOTIPO)
                if (u > bestU || (u == bestU && found && e.Id < best.Edge))
                {
                    bestU = u; found = true;
                    best = new Plan { Kind = cut ? PlanKind.Corte : PlanKind.Emboscada, Edge = e.Id, Node = -1, Cell = cell, UntilTick = w.Tick + NetConst.Hours(HoursAhead + 2) };
                }
            }

            // asedio: desde el día 2, sobre el nodo con más gente y defensas débiles, si no hay ya el máximo de asedios
            if (day >= SiegeMinDay && Active.FindAll(p => p.Kind == PlanKind.Asedio).Count < MaxSieges && w.Tick - _lastSiegeTick >= SiegeCooldownTicks)
            {
                for (int n = 0; n < w.Graph.Nodes.Count; n++)
                {
                    if (n == w.ActiveZoneNode) continue;
                    long pop = w.Stock[n, (int)Res.Personas];
                    if (pop <= 0 || w.Graph.Nodes[n].HordeDensity < NetWorld.ThreatAt) continue;   // solo donde ya hay horda cerca (amenazada)
                    long u = pop * 4 + w.Graph.Nodes[n].HordeDensity;                 // valor del nodo + horda que ya tiene cerca
                    if (u > bestU)
                    {
                        bestU = u; found = true;
                        var nd = w.Graph.Nodes[n];
                        best = new Plan { Kind = PlanKind.Asedio, Edge = -1, Node = n, Cell = nd.YMeters * HordeField.W + nd.XMeters, UntilTick = w.Tick + NetConst.Hours(18) };
                    }
                }
            }
            if (!found) return;

            int want = (int)Math.Min(budget, best.Kind == PlanKind.Asedio ? 600 : 250);
            best.Moved = Gather(w, best.Cell, want);
            BudgetUsedToday += best.Moved;
            if (best.Moved <= 0) return;
            Active.Add(best);
            if (best.Kind == PlanKind.Asedio) _lastSiegeTick = w.Tick;

            switch (best.Kind)
            {
                case PlanKind.Corte:
                {
                    var e = w.Graph.Edges[best.Edge];
                    e.State = EdgeState.Cortada;
                    w.Intel.Add(new IntelEntry { Tick = w.Tick, Id = "TR-4", Vars = new[] { "km", (e.Ambush[0].KmMm / 1_000_000L).ToString(), "tramo", Name(w, e) } });
                    break;
                }
                case PlanKind.Emboscada:
                {
                    var e = w.Graph.Edges[best.Edge];   // sin visión: el informe lo explicará después del ataque (TR-3, en la red)
                    w.PendingAmbushes.Add(best.Edge);
                    break;
                }
                case PlanKind.Asedio:
                {
                    var nd = w.Graph.Nodes[best.Node];
                    w.Intel.Add(new IntelEntry { Tick = w.Tick, Id = "ES-2", Vars = new[] { "nodo", nd.Name, "n", (best.Moved * NetWorld.ZombiesPerPermil).ToString() } });
                    break;
                }
            }
        }

        static string Name(NetWorld w, RailEdge e) { return w.Graph.Nodes[e.A].Name + "–" + w.Graph.Nodes[e.B].Name; }

        static int CellAtKm(NetWorld w, RailEdge e, long kmMm)
        {
            var a = w.Graph.Nodes[e.A]; var b = w.Graph.Nodes[e.B];
            long along = Math.Max(0, Math.Min(e.LengthMm, kmMm - e.KmStartMm));
            int x = a.XMeters + (int)((b.XMeters - a.XMeters) * along / Math.Max(1, e.LengthMm));
            int y = a.YMeters + (int)((b.YMeters - a.YMeters) * along / Math.Max(1, e.LengthMm));
            return y * HordeField.W + x;
        }

        /// <summary>
        /// Concentra densidad en «target» moviéndola desde las celdas de un radio de 8 (de la más densa a la menos; empate, índice
        /// menor), sin pasar del tope por celda ni tocar la celda congelada. Devuelve lo movido. La masa total no cambia.
        /// </summary>
        static int Gather(NetWorld w, int target, int want)
        {
            var f = w.Field;
            int tx = target % HordeField.W, ty = target / HordeField.W;
            var cand = new List<int>();
            for (int y = ty - 8; y <= ty + 8; y++)
                for (int x = tx - 8; x <= tx + 8; x++)
                {
                    if (x < 0 || y < 0 || x >= HordeField.W || y >= HordeField.H) continue;
                    int c = y * HordeField.W + x;
                    if (c == target || c == f.Frozen || f.D[c] <= 0) continue;
                    cand.Add(c);
                }
            cand.Sort((p, q) => f.D[p] != f.D[q] ? f.D[q].CompareTo(f.D[p]) : p.CompareTo(q));
            int room = HordeField.Max - f.D[target];
            int moved = 0;
            foreach (int c in cand)
            {
                if (moved >= want || moved >= room) break;
                int take = Math.Min(f.D[c] / 2, Math.Min(want - moved, room - moved));
                if (take <= 0) continue;
                f.D[c] -= take;
                f.D[target] += take;
                moved += take;
            }
            return moved;
        }

        public ulong Hash()
        {
            ulong h = NetWorld.Mix(0xD12EC7UL ^ (ulong)BudgetUsedToday ^ ((ulong)(uint)_budgetDay << 40)) + NetWorld.Mix((ulong)(_lastSiegeTick + 1_000_000));
            foreach (var p in Active)
                h += NetWorld.Mix(((ulong)p.Kind << 56) ^ ((ulong)(uint)(p.Edge + 1) << 40) ^ ((ulong)(uint)(p.Node + 1) << 28) ^ ((ulong)(uint)p.Cell << 8) ^ (ulong)p.UntilTick ^ ((ulong)(uint)p.Moved << 20));
            return h;
        }
    }
}
