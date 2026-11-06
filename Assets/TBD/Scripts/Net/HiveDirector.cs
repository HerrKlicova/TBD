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
    public struct DirectorParams
    {
        public int SiegePerPerson, AmbushPerMem, CutPerMem, SiegeDensity, AmbushDensity, SiegeCooldownH, SiegeDurationH, AmbushLeadH;
        public int AttentionHours, AttentionBonus, HuntPerHour, HuntHours, HuntThreshold, SiegeWarnH, FirstSiegeFromH, FirstSiegeToH;
        // D-N33 (Hugo, aprobado por Mak), de balance_colmena.csv
        public int BudgetCurveMil, SiegeShareMil, SiegeDays, WeightBase, WeightInteriorMil, WeightNestMil, AmbushShareMil, MaxSiegesStage1;
        public int PointUniqueMil, AttentionMil, RespiteMinH, RespiteMaxH;   // D-N34 (Lucía, medido por Hugo)

        public static DirectorParams Default => new DirectorParams
        {
            SiegePerPerson = 4, AmbushPerMem = 2, CutPerMem = 3, SiegeDensity = 600, AmbushDensity = 250, SiegeCooldownH = 48, SiegeDurationH = 18,
            AmbushLeadH = 2, AttentionHours = 6, AttentionBonus = 400, HuntPerHour = 80, HuntHours = 3, HuntThreshold = 150,
            SiegeWarnH = 3, FirstSiegeFromH = 10, FirstSiegeToH = 16,
            BudgetCurveMil = 1360, SiegeShareMil = 600, SiegeDays = 2, WeightBase = 60, WeightInteriorMil = 1400, WeightNestMil = 2000, AmbushShareMil = 250, MaxSiegesStage1 = 1,
            PointUniqueMil = 1500, AttentionMil = 1500, RespiteMinH = 6, RespiteMaxH = 12,
        };

        /// <summary>Añade los valores de D-N33 que Hugo puso en balance_colmena.csv.</summary>
        public DirectorParams WithColmena(CsvTable c)
        {
            var p = this;
            p.BudgetCurveMil = (int)c.Value("director_presupuesto_curva"); p.SiegeShareMil = (int)c.Value("asedio_fraccion_H");
            p.SiegeDays = (int)c.PositiveValue("asedio_dura_dias"); p.WeightBase = (int)c.Value("director_peso_nodo_base");
            p.WeightInteriorMil = (int)c.Value("director_peso_interior"); p.WeightNestMil = (int)c.Value("director_peso_nido_valencia");
            p.AmbushShareMil = (int)c.Value("emboscada_fraccion"); p.AmbushPerMem = (int)c.Value("emboscada_utilidad_x_memoria");
            p.CutPerMem = (int)c.Value("corte_utilidad_x_memoria"); p.MaxSiegesStage1 = (int)c.PositiveValue("asedios_max_etapa1");
            p.PointUniqueMil = (int)c.Value("director_peso_punto_unico"); p.AttentionMil = (int)c.Value("director_peso_atencion");
            p.RespiteMinH = (int)c.Value("asedio_respiro_min_h"); p.RespiteMaxH = (int)c.Value("asedio_respiro_max_h");
            return p;
        }

        public static DirectorParams FromCsv(CsvTable t)
        {
            return new DirectorParams
            {
                SiegePerPerson = (int)t.Value("utilidad_asedio_por_persona"), AmbushPerMem = (int)t.Value("utilidad_emboscada_por_memoria"),
                CutPerMem = (int)t.Value("utilidad_corte_por_memoria"), SiegeDensity = (int)t.Value("asedio_densidad"), AmbushDensity = (int)t.Value("emboscada_densidad"),
                SiegeCooldownH = (int)t.Value("asedio_enfriamiento_horas"), SiegeDurationH = (int)t.PositiveValue("asedio_duracion_horas"),
                AmbushLeadH = (int)t.Value("emboscada_antelacion_horas"), AttentionHours = (int)t.PositiveValue("atencion_horas_dia"),
                AttentionBonus = (int)t.Value("atencion_bonus"), HuntPerHour = (int)t.Value("caza_densidad_hora"), HuntHours = (int)t.PositiveValue("caza_duracion_horas"),
                HuntThreshold = (int)t.Value("caza_umbral_densidad"),
                SiegeWarnH = (int)t.Value("asedio_aviso_horas"), FirstSiegeFromH = (int)t.Value("primer_asedio_desde_h"), FirstSiegeToH = (int)t.Value("primer_asedio_hasta_h"),
            };
        }
    }

    public sealed class HiveDirector
    {
        public enum PlanKind : byte { Emboscada = 0, Corte = 1, Asedio = 2, Caza = 3 }
        public DirectorParams P = DirectorParams.Default;

        public struct Plan
        {
            public PlanKind Kind;
            public int Edge;           // emboscada y corte
            public int Node;           // asedio
            public int Cell;           // celda de la mancha donde se concentra
            public long UntilTick;
            public int Moved;          // densidad (por mil) que movió hasta aquí
            public int Train;          // caza: índice del servicio al que sigue
            public long StartTick;     // asedio: empieza tras el aviso de 3 h (Sara/Mak); hasta entonces solo es una amenaza anunciada
            public bool Started;
        }

        public readonly List<Plan> Active = new List<Plan>();
        public long BudgetUsedToday;                  // densidad movida hoy (por mil)
        long _budgetDay = -1;
        long _lastSiegeTick = -1_000_000;
        long _respiteUntil;                            // D-N34: tras un asedio, 6-12 h sin asedio nuevo en la rama
        long _ambushBank;                              // D-N33: lo que el canal de emboscada y corte lleva ahorrado (por mil)
        public readonly long[] LastSeenTick = new long[64];      // por nodo: último tick con el jugador en esa zona (peso de atención)
        bool _anySiege;                                // D-N32: el primer asedio de la partida, de día (ventana del CSV)
        long SiegeCooldownTicks => NetConst.Hours(P.SiegeCooldownH);

        int HoursAhead => P.AmbushLeadH;              // la emboscada se prepara 1-2 h antes (Lucía)
        public readonly long[] AttentionTicks = new long[64];   // por nodo: ticks de juego con zona en detalle hoy (memoria de atención)
        public const int SiegeMinDay = 1;             // día 2 en adelante (Tick / DayTicks >= 1)

        /// <summary>
        /// Presupuesto de una hora (D-N33 de Hugo), en «por mil» de densidad: H(día) de la curva × 1,36 desde el día 2 (los días 0 y 1,
        /// con la rampa de D-N08), / 24 horas, / 10 zombis por mil. Es lo que el director puede mover en esa hora.
        /// </summary>
        public static long HourlyBudgetPermil(long day, long[] curve, int curveMil, long h0 = 1600, long kHuellaMil = 160, long huellaMil = 2000)
        {
            long table = curve != null && day < curve.Length ? curve[day] : 1000 + day * 50;
            long root;
            long h = BalanceMath.HiveBudget(h0, table, kHuellaMil, huellaMil, out root);
            long mult = day == 0 ? 150 : day == 1 ? 500 : curveMil;                    // D-N08 (15 %, 50 %) y D-N33 (× 1,36 desde el día 2)
            return h * mult / 1000 / 24 / NetWorld.ZombiesPerPermil;
        }

        static readonly string[] InteriorNodes = { "Segorbe", "Barracas", "Teruel", "Caminreal", "Calamocha", "Cariñena", "Zaragoza" };
        const string NestNode = "Valencia", UniqueNode = "Castellón";   // nido (D-N33) y única fuente de combustible (D-N34, OB-1)

        /// <summary>
        /// Peso de un nodo como objetivo de asedio (D-N33/D-N34): (base + población) × interior × nido × punto único × atención,
        /// los multiplicadores en «por mil». Entero.
        /// </summary>
        public long SiegeWeight(NetWorld w, int n)
        {
            var nd = w.Graph.Nodes[n];
            long wt = P.WeightBase + Math.Max(0, w.Stock[n, (int)Res.Personas]);
            if (Array.IndexOf(InteriorNodes, nd.Name) >= 0) wt = wt * P.WeightInteriorMil / 1000;
            if (nd.Name == NestNode) wt = wt * P.WeightNestMil / 1000;
            if (nd.Name == UniqueNode) wt = wt * P.PointUniqueMil / 1000;
            if (n < LastSeenTick.Length && w.Tick - LastSeenTick[n] > NetConst.Hours(P.AttentionHours)) wt = wt * P.AttentionMil / 1000;
            return wt;
        }

        /// <summary>Sorteo del objetivo con la semilla del tick, probabilidad proporcional al peso al cuadrado (D-N33). -1 si no hay.</summary>
        public int DrawSiegeTarget(NetWorld w)
        {
            long total = 0;
            var sq = new long[w.Graph.Nodes.Count];
            for (int n = 0; n < sq.Length; n++)
            {
                if (n == w.ActiveZoneNode || Active.Exists(p => p.Kind == PlanKind.Asedio && p.Node == n)) continue;
                long wt = SiegeWeight(w, n);
                sq[n] = wt * wt; total += sq[n];
            }
            if (total <= 0) return -1;
            long r = (long)(NetWorld.Mix(0x51E6EUL ^ (ulong)w.Tick ^ ((ulong)(_lastSiegeTick + 1_000_000) << 20)) % (ulong)total);
            for (int n = 0; n < sq.Length; n++) { if (r < sq[n]) return n; r -= sq[n]; }
            return -1;
        }

        /// <summary>Reparte «amount» por mil de deriva hacia los nodos según su peso (lo que no va a asedios ni a emboscadas).</summary>
        long Drift(NetWorld w, long amount)
        {
            if (amount <= 0) return 0;
            long total = 0, moved = 0;
            for (int n = 0; n < w.Graph.Nodes.Count; n++) total += SiegeWeight(w, n);
            if (total <= 0) return 0;
            for (int n = 0; n < w.Graph.Nodes.Count; n++)
            {
                int want = (int)(amount * SiegeWeight(w, n) / total);
                if (want <= 0) continue;
                var nd = w.Graph.Nodes[n];
                moved += Gather(w, nd.YMeters * HordeField.W + nd.XMeters, want);
            }
            return moved;
        }

        public long[] Curve;                          // colmena_curva.csv (si no se carga, una aproximación lineal)

        /// <summary>Una hora de juego: caducan los planes, se elige la acción de mayor utilidad y se ejecuta.</summary>
        public void Hourly(NetWorld w)
        {
            long day = w.Tick / NetConst.DayTicks;
            if (day != _budgetDay) { _budgetDay = day; BudgetUsedToday = 0; Array.Clear(AttentionTicks, 0, AttentionTicks.Length); }
            // caza: cada manada que sigue a un tren mueve horda hacia donde está el tren
            for (int i = 0; i < Active.Count; i++)
            {
                var pl = Active[i];
                if (pl.Kind != PlanKind.Caza) continue;
                int c = TrainCell(w, w.Services[pl.Train]);
                if (c < 0) { pl.UntilTick = w.Tick; Active[i] = pl; continue; }     // llegó: pierde el rastro
                pl.Moved += Gather(w, c, P.HuntPerHour);
                pl.Cell = c;
                Active[i] = pl;
            }
            foreach (var pl in Active)                                      // D-N34: un asedio que termina deja respiro de 6-12 h
                if (pl.Kind == PlanKind.Asedio && pl.Started && w.Tick >= pl.UntilTick)
                {
                    int span = Math.Max(0, P.RespiteMaxH - P.RespiteMinH);
                    long extra = span == 0 ? 0 : (long)(NetWorld.Mix(0x8E5917UL ^ (ulong)pl.UntilTick ^ ((ulong)(uint)pl.Node << 40)) % (ulong)(span + 1));
                    _respiteUntil = Math.Max(_respiteUntil, w.Tick + NetConst.Hours(P.RespiteMinH + (int)extra));
                }
            Active.RemoveAll(p => w.Tick >= p.UntilTick);
            for (int i = 0; i < Active.Count; i++)                         // asedios anunciados que empiezan ahora
            {
                var pl = Active[i];
                if (pl.Kind != PlanKind.Asedio || pl.Started || w.Tick < pl.StartTick) continue;
                pl.Started = true;
                pl.Moved = Gather(w, pl.Cell, P.SiegeDensity);            // recluta inicial desde la deriva (la masa no cambia)
                BudgetUsedToday += pl.Moved;
                pl.UntilTick = w.Tick + (long)P.SiegeDays * NetConst.DayTicks;
                Active[i] = pl;
                var nd = w.Graph.Nodes[pl.Node];
                w.Intel.Add(new IntelEntry { Tick = w.Tick, Id = "ES-2", Vars = new[] { "nodo", nd.Name, "n", (pl.Moved * NetWorld.ZombiesPerPermil).ToString() } });
                int focus = AttentionNode(w);
                if (focus >= 0 && focus != pl.Node)
                    w.Intel.Add(new IntelEntry { Tick = w.Tick, Id = "AT-1", Vars = new[] { "nodo_b", nd.Name, "nodo_a", w.Graph.Nodes[focus].Name } });
            }

            // presupuesto de la hora (D-N33): 600 ‰ a los asedios activos; del resto, 250 ‰ al canal de emboscada y corte y lo demás, deriva por peso
            long hb = HourlyBudgetPermil(day, Curve, P.BudgetCurveMil);
            long siegeShare = hb * P.SiegeShareMil / 1000, rest = hb - siegeShare;
            long ambushShare = rest * P.AmbushShareMil / 1000, drift = rest - ambushShare;
            int started = 0;
            foreach (var pl in Active) if (pl.Kind == PlanKind.Asedio && pl.Started) started++;
            if (started > 0)
            {
                for (int i = 0; i < Active.Count; i++)
                {
                    var pl = Active[i];
                    if (pl.Kind != PlanKind.Asedio || !pl.Started) continue;
                    int m = Gather(w, pl.Cell, (int)(siegeShare / started));
                    pl.Moved += m; BudgetUsedToday += m; Active[i] = pl;
                }
            }
            else drift += siegeShare;                                       // D-N34: sin asedio, esa masa vuelve a la deriva y presiona por peso
            BudgetUsedToday += Drift(w, drift);
            _ambushBank += ambushShare;

            // canal de asedio (D-N33): por calendario, sin competir por utilidad
            long startTod = (w.Tick + NetConst.Hours(P.SiegeWarnH)) % NetConst.DayTicks;
            bool inFirstWindow = startTod >= NetConst.Hours(P.FirstSiegeFromH) && startTod <= NetConst.Hours(P.FirstSiegeToH);
            int sieges = 0;
            foreach (var pl in Active) if (pl.Kind == PlanKind.Asedio) sieges++;
            if (day >= SiegeMinDay && (_anySiege || inFirstWindow) && sieges < P.MaxSiegesStage1 && w.Tick >= _respiteUntil)
            {
                int n = DrawSiegeTarget(w);
                if (n >= 0)
                {
                    // aviso de 3 h (Mak, D-N32): se anuncia ahora y empieza en StartTick; la alerta «Horda en 3 h» la da la red
                    var nd = w.Graph.Nodes[n];
                    long st = w.Tick + NetConst.Hours(P.SiegeWarnH);
                    Active.Add(new Plan { Kind = PlanKind.Asedio, Edge = -1, Node = n, Cell = nd.YMeters * HordeField.W + nd.XMeters, StartTick = st, UntilTick = st + (long)P.SiegeDays * NetConst.DayTicks });
                    _lastSiegeTick = w.Tick;
                    _anySiege = true;
                }
            }

            // canal de emboscada y corte (D-N33): por memoria de tráfico, con lo ahorrado
            if (_ambushBank <= 0) return;
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
                long u = (long)m * (cut ? P.CutPerMem : P.AmbushPerMem);              // valor × oportunidad (PROTOTIPO, CSV)
                if (u > bestU || (u == bestU && found && e.Id < best.Edge))
                {
                    bestU = u; found = true;
                    best = new Plan { Kind = cut ? PlanKind.Corte : PlanKind.Emboscada, Edge = e.Id, Node = -1, Cell = cell, UntilTick = w.Tick + NetConst.Hours(HoursAhead + 2) };
                }
            }

            if (!found) return;

            int want = (int)Math.Min(_ambushBank, P.AmbushDensity);
            best.Moved = Gather(w, best.Cell, want);
            BudgetUsedToday += best.Moved;
            _ambushBank -= best.Moved;
            if (best.Moved <= 0) return;
            Active.Add(best);


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
            }
        }

        /// <summary>Nodo en el que el jugador lleva hoy más de AttentionHours con la zona en detalle (o -1).</summary>
        public int AttentionNode(NetWorld w)
        {
            long lim = NetConst.Hours(P.AttentionHours);
            for (int n = 0; n < w.Graph.Nodes.Count && n < AttentionTicks.Length; n++) if (AttentionTicks[n] >= lim) return n;
            return -1;
        }

        /// <summary>Un tren sale de un nodo con horda cerca: una manada lo sigue (estado «caza», Lucía §3), con su entrada ES-1.</summary>
        public void OnDeparture(NetWorld w, int service, int fromNode)
        {
            if (fromNode < 0 || w.Graph.Nodes[fromNode].HordeDensity < P.HuntThreshold) return;
            if (Active.Exists(p => p.Kind == PlanKind.Caza && p.Train == service)) return;
            Active.Add(new Plan { Kind = PlanKind.Caza, Edge = -1, Node = fromNode, Cell = -1, Train = service, UntilTick = w.Tick + NetConst.Hours(P.HuntHours) });
            w.Intel.Add(new IntelEntry { Tick = w.Tick, Id = "ES-1", Vars = new[] { "tren", "Tren " + w.Services[service].Train.Name, "nodo", w.Graph.Nodes[fromNode].Name } });
        }

        static int TrainCell(NetWorld w, TrainService s)
        {
            var t = s.Train;
            if (s.AtStop) return -1;
            var e = t.CurrentEdge(w.Graph);
            if (e == null) return -1;
            var from = w.Graph.Nodes[t.Route[t.Leg]]; var to = w.Graph.Nodes[t.Route[t.Leg + 1]];
            long k = e.LengthMm > 0 ? t.OffsetMm * 1000 / e.LengthMm : 0;
            int x = from.XMeters + (int)((to.XMeters - from.XMeters) * k / 1000), y = from.YMeters + (int)((to.YMeters - from.YMeters) * k / 1000);
            return y * HordeField.W + x;
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
            ulong h = NetWorld.Mix(0xD12EC7UL ^ (ulong)BudgetUsedToday ^ ((ulong)(uint)_budgetDay << 40)) + NetWorld.Mix((ulong)_respiteUntil ^ ((ulong)_ambushBank << 32)) + NetWorld.Mix((ulong)(_lastSiegeTick + 1_000_000) ^ (_anySiege ? 1UL << 62 : 0));
            foreach (var p in Active)
                h += NetWorld.Mix(((ulong)p.Kind << 56) ^ ((ulong)(uint)(p.Edge + 1) << 40) ^ ((ulong)(uint)(p.Node + 1) << 28) ^ ((ulong)(uint)(p.Cell + 1) << 8) ^ (ulong)p.UntilTick ^ ((ulong)(uint)p.Moved << 20) ^ ((ulong)(uint)(p.Train + 1) << 50)) + NetWorld.Mix((ulong)p.StartTick ^ (p.Started ? 1UL << 63 : 0));
            for (int n = 0; n < LastSeenTick.Length; n++) if (LastSeenTick[n] != 0) h += NetWorld.Mix(((ulong)(uint)n << 52) ^ (ulong)LastSeenTick[n] ^ 0x5EE7UL);
            for (int n = 0; n < AttentionTicks.Length; n++) if (AttentionTicks[n] != 0) h += NetWorld.Mix(((ulong)(uint)n << 48) ^ (ulong)AttentionTicks[n]);
            return h;
        }
    }
}
