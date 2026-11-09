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
        public int PointUniqueMil, AttentionMil, RespiteMinH, RespiteMaxH;
        public int HuellaPersonMil, HuellaTrainMil, HuellaNodeMil, BigEveryDays, BigMultMil;
        public int H0, KHuellaMil, FirstSiegeDay, RetireDays;   // D-N36 (Hugo): presupuesto del día 0, huella, índice del primer asedio, días sin combatir antes de retirarse
        public int WorkProbeMil, WorkProbeBudgetMil, WorkWeightMil, WorkHuellaMil;   // hito 5 (D-N44, balance_construccion.csv): sondeo de obra por hora (‰), su tamaño (‰ del presupuesto del día), peso del nodo como objetivo (‰) y huella de una obra de muros (‰)
        // D-N160 (Hugo, aprobado por Lucía, 9-nov): tres estados de la Colmena (balance_colmena.csv, colmena_*). ColOn = 0: apagado
        public int ColOn, ColUp1Mil, ColUp2Mil, ColWarnMil, ColForgetDays, ColHuellaWeightMil, ColHuellaBaseMil, ColMult0, ColMult1, ColMult2, ColExtraSieges;
        public int Ramp0Mil, Ramp1Mil, Ramp2Mil, Ramp3Mil;   // huella (Hugo) y gran ataque anunciado (autoguardado, §14)   // D-N34 (Lucía, medido por Hugo)

        public static DirectorParams Default => new DirectorParams
        {
            SiegePerPerson = 4, AmbushPerMem = 2, CutPerMem = 3, SiegeDensity = 600, AmbushDensity = 250, SiegeCooldownH = 48, SiegeDurationH = 18,
            AmbushLeadH = 2, AttentionHours = 6, AttentionBonus = 400, HuntPerHour = 80, HuntHours = 3, HuntThreshold = 150,
            SiegeWarnH = 3, FirstSiegeFromH = 10, FirstSiegeToH = 16,
            BudgetCurveMil = 1360, SiegeShareMil = 600, SiegeDays = 2, WeightBase = 60, WeightInteriorMil = 1400, WeightNestMil = 2000, AmbushShareMil = 250, MaxSiegesStage1 = 1,
            PointUniqueMil = 1500, AttentionMil = 1500, RespiteMinH = 6, RespiteMaxH = 12,
            HuellaPersonMil = 10, HuellaTrainMil = 500, HuellaNodeMil = 400, BigEveryDays = 7, BigMultMil = 2000,
            H0 = 1550, KHuellaMil = 160, FirstSiegeDay = 1, RetireDays = 6, WorkProbeMil = 83, WorkProbeBudgetMil = 50, WorkWeightMil = 1500, WorkHuellaMil = 500, Ramp0Mil = 150, Ramp1Mil = 350, Ramp2Mil = 650, Ramp3Mil = 850,
            ColOn = 0, ColUp1Mil = 4000, ColUp2Mil = 7000, ColWarnMil = 6000, ColForgetDays = 3, ColHuellaWeightMil = 500, ColHuellaBaseMil = 8000, ColMult0 = 900, ColMult1 = 1000, ColMult2 = 1250, ColExtraSieges = 1,
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
            p.SiegeWarnH = (int)c.TryValue("asedio_aviso_h", p.SiegeWarnH);           // D-N37: ahora en balance_colmena.csv
            p.HuellaPersonMil = (int)c.Value("huella_por_persona"); p.HuellaTrainMil = (int)c.Value("huella_por_tren"); p.HuellaNodeMil = (int)c.Value("huella_por_nodo");
            p.BigEveryDays = (int)c.TryValue("asedio_grande_cada_dias", p.BigEveryDays); p.BigMultMil = (int)c.TryValue("asedio_grande_mult", p.BigMultMil);   // opcionales: Hugo aún no los puso en el CSV
            p.H0 = (int)c.PositiveValue("H0_zombis_dia"); p.KHuellaMil = (int)c.Value("k_huella"); p.FirstSiegeDay = (int)c.Value("dia_primer_asedio");
            p.Ramp0Mil = (int)c.Value("rampa_dia0"); p.Ramp1Mil = (int)c.Value("rampa_dia1"); p.Ramp2Mil = (int)c.Value("rampa_dia2"); p.Ramp3Mil = (int)c.Value("rampa_dia3");
            p.RetireDays = (int)c.TryValue("zombis_retirada_dias", 6);   // opcional: Hugo aún no lo puso en el CSV
            p.ColOn = (int)c.TryValue("colmena_estados", 0);           // D-N160: sin la fila, apagado
            p.ColUp1Mil = (int)c.TryValue("colmena_umbral_acecho_x1000", p.ColUp1Mil); p.ColUp2Mil = (int)c.TryValue("colmena_umbral_enjambre_x1000", p.ColUp2Mil);
            p.ColWarnMil = (int)c.TryValue("colmena_aviso_x1000", p.ColWarnMil); p.ColForgetDays = (int)c.TryValue("colmena_dias_olvido", p.ColForgetDays);
            p.ColHuellaWeightMil = (int)c.TryValue("colmena_peso_huella_x1000", p.ColHuellaWeightMil); p.ColHuellaBaseMil = (int)c.TryValue("colmena_huella_base_x1000", p.ColHuellaBaseMil);
            p.ColMult0 = (int)c.TryValue("colmena_mult_H_dispersa_permil", p.ColMult0); p.ColMult1 = (int)c.TryValue("colmena_mult_H_acecho_permil", p.ColMult1);
            p.ColMult2 = (int)c.TryValue("colmena_mult_H_enjambre_permil", p.ColMult2); p.ColExtraSieges = (int)c.TryValue("colmena_asedio_extra_enjambre", p.ColExtraSieges);
            return p;
        }

        /// <summary>Añade los valores de D-N44 (hito 5) que Hugo puso en balance_construccion.csv: el ruido de obra.</summary>
        public DirectorParams WithConstruccion(CsvTable c)
        {
            var p = this;
            p.WorkProbeMil = (int)c.Value("obra_sondeo_permil_hora"); p.WorkProbeBudgetMil = (int)c.Value("obra_sondeo_permil_presupuesto_dia");
            p.WorkWeightMil = (int)c.Value("obra_peso_asedio_permil"); p.WorkHuellaMil = (int)c.Value("obra_huella_muros_permil");
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

    public struct Injection { public int Cell, Amount; }

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
            public int Lost;           // asedio: masa que ha perdido en combate (bajas de la zona de su nodo)
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

        public void SaveState(SaveWriter w)
        {
            w.Int(Active.Count);
            foreach (var p in Active) { w.Byte((byte)p.Kind); w.Int(p.Edge); w.Int(p.Node); w.Int(p.Cell); w.Long(p.UntilTick); w.Int(p.Moved); w.Int(p.Train); w.Long(p.StartTick); w.Bool(p.Started); w.Int(p.Lost); }
            w.Long(BudgetUsedToday); w.Long(_budgetDay); w.Long(_lastSiegeTick); w.Long(_respiteUntil); w.Long(_ambushBank); w.Bool(_anySiege);
            w.Longs(LastSeenTick); w.Longs(AttentionTicks);
            w.Long(SiegeMovedTotal); w.Long(DriftMovedTotal); w.Long(AmbushMovedTotal); w.Long(BudgetTotal);
            w.Bool(IgnoreAttention); w.ULong(Seed);
            w.Long(_carryZombies); w.Longs(_injectedByDay);
            w.Long(WorkProbes_Count); w.Long(WorkProbes_Moved); w.Long(WorkProbes_Asked); w.Long(WorkProbes_Paid);
        }

        public void LoadState(SaveReader r)
        {
            int n = r.Int();
            if (n < 0 || n > r.Remaining) throw new SaveException(SaveError.Corrupt, "planes de la Colmena no válidos");
            Active.Clear();
            for (int i = 0; i < n; i++)
                Active.Add(new Plan { Kind = (PlanKind)r.Byte(), Edge = r.Int(), Node = r.Int(), Cell = r.Int(), UntilTick = r.Long(), Moved = r.Int(), Train = r.Int(), StartTick = r.Long(), Started = r.Bool(), Lost = r.Int() });
            BudgetUsedToday = r.Long(); _budgetDay = r.Long(); _lastSiegeTick = r.Long(); _respiteUntil = r.Long(); _ambushBank = r.Long(); _anySiege = r.Bool();
            ReadInto(r.Longs(), LastSeenTick, "atención"); ReadInto(r.Longs(), AttentionTicks, "ticks de atención");
            SiegeMovedTotal = r.Long(); DriftMovedTotal = r.Long(); AmbushMovedTotal = r.Long(); BudgetTotal = r.Long();
            IgnoreAttention = r.Bool(); Seed = r.ULong();
            _carryZombies = r.Long(); ReadInto(r.Longs(), _injectedByDay, "inyectado por día");
            WorkProbes_Count = r.Long(); WorkProbes_Moved = r.Long(); WorkProbes_Asked = r.Long(); WorkProbes_Paid = r.Long();
        }

        static void ReadInto(long[] src, long[] dst, string what)
        {
            if (src == null || src.Length != dst.Length) throw new SaveException(SaveError.TopologyMismatch, what + ": otra longitud");
            Array.Copy(src, dst, dst.Length);
        }

        static readonly string[] InteriorNodes = { "Segorbe", "Barracas", "Teruel", "Caminreal", "Calamocha", "Cariñena", "Zaragoza" };
        const string NestNode = "Valencia", UniqueNode = "Castellón";   // nido (D-N33) y única fuente de combustible (D-N34, OB-1)

        /// <summary>
        /// Peso de un nodo como objetivo de asedio (D-N33/D-N34): (base + población) × interior × nido × punto único × atención,
        /// los multiplicadores en «por mil». Entero.
        /// </summary>
        const int PersonsPerHugoUnit = 10;
        public long SiegeMovedTotal, DriftMovedTotal, AmbushMovedTotal, BudgetTotal;   // estadísticas para medir (no entran en el hash ni cambian la partida)
        public bool IgnoreAttention;                // solo pruebas: sin el peso de atención
        public ulong Seed;                          // semilla de la partida (0 por defecto)
        /// <summary>D-N156 (Lucía): un nodo protegido de asedio hasta un tick (Nules, hasta pasada la 1.ª noche). −1 = ninguno. Lo pone NetScenario.Inicio; no se guarda.</summary>
        public int SinAsedioNodo = -1; public long SinAsedioHasta;
        /// <summary>Lucía (8-nov 23:20): cada zona tiene su propia horda. Una horda de zona ataca su nodo cada noche desde el día FromDay (0 = 1.ª noche), de 22:00 a 06:00,
        /// con aviso a las 19:00 (SiegeWarnH). Al acabar, si la zona no está en pantalla, la resuelve NetWorld.ResolveZoneSiege (regla abstracta). Lo pone el escenario; no se guarda.</summary>
        public struct ZoneHorde { public int Node, FromDay, Zombies; }
        public readonly List<ZoneHorde> ZoneHordes = new List<ZoneHorde>();
        /// <summary>Marca de un plan de asedio que es horda de zona (en Plan.Edge, que en los asedios no se usa): Moved lleva la horda en ‰ de densidad y no recluta.</summary>
        public const int ZoneSiegeEdge = -2;
        public static bool IsZoneSiege(in Plan p) { return p.Kind == PlanKind.Asedio && p.Edge == ZoneSiegeEdge; }
        public long SiegeWeight(NetWorld w, int n)
        {
            var nd = w.Graph.Nodes[n];
            if (n == SinAsedioNodo && w.Tick + NetConst.Hours(P.SiegeWarnH) < SinAsedioHasta) return 0;   // el asedio empezaría (tras el aviso) antes de acabar la protección
            if (w.Stations.IsSite(n) && !w.Stations.Founded[n]) return 0;      // hito 5 (Mak, 6-nov): un emplazamiento sin fundar no cuenta (ni objetivo ni deriva)
            long wt = P.WeightBase + Math.Max(0, w.Stock[n, (int)Res.Personas]) / PersonsPerHugoUnit;   // el modelo de Hugo cuenta la población en unidades de 10 personas (Valencia 30 = 300)
            if (Array.IndexOf(InteriorNodes, nd.Name) >= 0) wt = wt * P.WeightInteriorMil / 1000;
            if (nd.Name == NestNode) wt = wt * P.WeightNestMil / 1000;
            if (nd.Name == UniqueNode) wt = wt * P.PointUniqueMil / 1000;
            if (!IgnoreAttention && n < LastSeenTick.Length && w.Tick - LastSeenTick[n] > NetConst.Hours(P.AttentionHours)) wt = wt * P.AttentionMil / 1000;
            return wt;
        }

        /// <summary>Peso del nodo como objetivo de asedio: el de siempre, ×1,5 mientras tenga una obra activa (el ruido de obra atrae a la Colmena, D-N44).</summary>
        public long SiegeTargetWeight(NetWorld w, int n)
        {
            long wt = SiegeWeight(w, n);
            return w.WorkInProgress(n) ? wt * P.WorkWeightMil / 1000 : wt;
        }

        /// <summary>Sorteo del objetivo con la semilla del tick, probabilidad proporcional al peso al cuadrado (D-N33). -1 si no hay.</summary>
        public int DrawSiegeTarget(NetWorld w)
        {
            long total = 0;
            var sq = new long[w.Graph.Nodes.Count];
            for (int n = 0; n < sq.Length; n++)
            {
                if (n == w.ActiveZoneNode || Active.Exists(p => p.Kind == PlanKind.Asedio && p.Node == n)) continue;
                long wt = SiegeTargetWeight(w, n);
                sq[n] = wt * wt; total += sq[n];
            }
            if (total <= 0) return -1;
            long r = (long)(NetWorld.Mix(0x51E6EUL ^ Seed ^ (ulong)w.Tick ^ ((ulong)(_lastSiegeTick + 1_000_000) << 20)) % (ulong)total);
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

        public long[] Season, Noise;                  // colmena_estacion.csv (30, por día mod 30) y colmena_ruido.csv (64 cuantiles ×1000)

        /// <summary>Factor de la rampa de los primeros días (D-N08/D-N36): 150, 350, 650, 850 ‰ y después 1000.</summary>
        public long RampMil(long day) { return day == 0 ? P.Ramp0Mil : day == 1 ? P.Ramp1Mil : day == 2 ? P.Ramp2Mil : day == 3 ? P.Ramp3Mil : 1000; }

        /// <summary>Cuantil de ruido del día: un índice de 0 a 63 sorteado con la semilla (sin coma flotante). 1000 si no hay tabla.</summary>
        public long NoiseMil(long day)
        {
            if (Noise == null || Noise.Length == 0) return 1000;
            return Noise[(int)(NetWorld.Mix(0xA70153UL ^ Seed ^ ((ulong)day << 24)) % (ulong)Noise.Length)];
        }

        /// <summary>
        /// Zombis que el director puede mover en un día (D-N36 de Hugo): H0 · curva · √(1 + k·huella) · estación · ruido · rampa, y desde
        /// el primer asedio × 1,36. Todo en enteros. La interdicción usa la curva sin × 1,36.
        /// </summary>
        public long DayZombies(long day, long huellaMil)
        {
            long table = Curve != null && day < Curve.Length ? Curve[day] : 1000 + day * 50;
            long root;
            long h = BalanceMath.HiveBudget(P.H0, table, P.KHuellaMil, huellaMil, out root);
            long season = Season != null && Season.Length > 0 ? Season[(int)(day % Season.Length)] : 1000;
            h = h * season / 1000 * NoiseMil(day) / 1000 * RampMil(day) / 1000;
            if (day >= P.FirstSiegeDay) h = h * P.BudgetCurveMil / 1000;
            return h;
        }

        // ------------------------------------------------ D-N160: los tres estados de la Colmena (Hugo, colmena-estados.md §5)
        public const int ColDispersa = 0, ColAcecho = 1, ColEnjambre = 2;
        /// <summary>Estado de la Colmena entera: 0 Dispersa, 1 Al acecho, 2 Enjambre. Se calcula una vez al día, al cambiar de día.</summary>
        public int ColState;
        /// <summary>Días seguidos con la señal por debajo del umbral del estado actual (baja uno tras ColForgetDays).</summary>
        public int ColDaysBelow;
        /// <summary>Señal del día en ‰: nodos ganados en 7 días × 1000 + 0,5 × (huella − 8). La vista la lee para el aviso ámbar (≥ ColWarnMil).</summary>
        public long ColSignalMil;
        /// <summary>Nodos con gente al empezar cada uno de los últimos 8 días (por día mod 8).</summary>
        public readonly int[] ColHeld = new int[8];
        /// <summary>¿Hay que dar el aviso ámbar «La Colmena se agita»? (señal ≥ colmena_aviso y aún no en Enjambre)</summary>
        public bool ColWarning { get { return P.ColOn != 0 && ColState < ColEnjambre && ColSignalMil >= P.ColWarnMil; } }
        /// <summary>Asedios simultáneos que permite la etapa; en Enjambre, ColExtraSieges más (D-N160).</summary>
        public int MaxSieges() { return P.MaxSiegesStage1 + (P.ColOn != 0 && ColState == ColEnjambre ? P.ColExtraSieges : 0); }
        public int ColMultMil() { return P.ColOn == 0 ? 1000 : ColState == ColEnjambre ? P.ColMult2 : ColState == ColAcecho ? P.ColMult1 : P.ColMult0; }
        /// <summary>Presupuesto del día con el multiplicador del estado (DayZombies sigue siendo la curva pura).</summary>
        public long DayBudget(long day, long huellaMil) { return DayZombies(day, huellaMil) * ColMultMil() / 1000; }

        /// <summary>CO-0 ya dado (o ya no toca). Va en el guardado (NetVersion 6), fuera del hash: al cargar una partida en la que ya salió, no se repite; una partida migrada de la v5 lo trae dado.</summary>
        public bool ColIntroDone;
        /// <summary>CO-0 (Lucía, 9-nov): la vista lo llama la primera vez que se abre el mapa de la red después del tutorial. Sale una sola vez por partida
        /// y solo si la Colmena está Dispersa (si no, ya salió el CO que tocaba). Devuelve true si ha añadido la entrada (para el sonido de Dispersa).</summary>
        public bool ColmenaIntro(NetWorld w)
        {
            if (ColIntroDone) return false;
            ColIntroDone = true;
            if (P.ColOn == 0 || ColState != ColDispersa) return false;
            w.Intel.Add(new IntelEntry { Tick = w.Tick, Id = "CO-0", Vars = new string[0] });
            return true;
        }

        static int HeldNodes(NetWorld w)
        {
            int held = 0;
            for (int n = 0; n < w.Graph.Nodes.Count; n++) if (Held(w, n)) held++;
            return held;
        }

        /// <summary>¿Es del jugador? (Fundada, si es un emplazamiento, y con gente.)</summary>
        internal static bool Held(NetWorld w, int n) { return !(w.Stations.IsSite(n) && !w.Stations.Founded[n]) && w.Stock[n, (int)Res.Personas] > 0; }

        /// <summary>
        /// Informe (memoria de atención, informe-textos.md §3): los nodos del jugador que llevan más de AttentionHours sin mirar (los que la Colmena prefiere, ×AttentionMil),
        /// por orden de nodo. Vacía si el peso de atención está apagado. Solo lectura.
        /// </summary>
        public List<int> Neglected(NetWorld w)
        {
            var l = new List<int>(); if (IgnoreAttention || P.AttentionMil == 1000) return l;
            long lim = NetConst.Hours(P.AttentionHours);
            for (int n = 0; n < w.Graph.Nodes.Count && n < LastSeenTick.Length; n++) if (Held(w, n) && w.Tick - LastSeenTick[n] > lim) l.Add(n);
            return l;
        }

        /// <summary>Una vez al día (sim_economia.py, colmena_estados): sube al instante, baja de uno en uno tras ColForgetDays días seguidos por debajo.
        /// Cada cambio deja su entrada en el informe (CO-1 a CO-4).</summary>
        internal void ColmenaDay(NetWorld w, long day)
        {
            if (P.ColOn == 0) return;
            int held = HeldNodes(w);
            ColHeld[(int)(day % ColHeld.Length)] = held;
            long refDay = Math.Max(0, day - 7);
            int gained = held - ColHeld[(int)(refDay % ColHeld.Length)];
            long hu = HuellaMil(w);
            ColSignalMil = gained * 1000L + (long)P.ColHuellaWeightMil * Math.Max(0, hu - P.ColHuellaBaseMil) / 1000;
            int up = ColSignalMil >= P.ColUp2Mil ? ColEnjambre : ColSignalMil >= P.ColUp1Mil ? ColAcecho : ColDispersa;
            int old = ColState;
            if (up > ColState) { ColState = up; ColDaysBelow = 0; }
            else if (up < ColState) { ColDaysBelow++; if (ColDaysBelow >= P.ColForgetDays) { ColState--; ColDaysBelow = 0; } }
            else ColDaysBelow = 0;
            if (ColState != old)
            {
                string id = ColState > old ? (ColState == ColEnjambre ? "CO-2" : "CO-1") : (ColState == ColAcecho ? "CO-3" : "CO-4");
                var vars = id == "CO-2" ? new[] { "n", gained.ToString(), "h", (hu / 1000).ToString() + "," + (hu % 1000 / 100).ToString() } : new string[0];   // CO-2 (Lucía): «{n} nodos en 7 días y huella {h}»
                w.Intel.Add(new IntelEntry { Tick = w.Tick, Id = id, Vars = vars });
            }
        }

        long _carryZombies;                           // resto de zombis que no llegó a un «por mil» entero (se arrastra hora a hora)

        /// <summary>Presupuesto de esta hora en «por mil» de densidad: DayZombies / 24 / 10, arrastrando el resto.</summary>
        long HourBudgetPermil(long day, long huellaMil)
        {
            long z = DayBudget(day, huellaMil) / 24 + _carryZombies;
            long permil = z / NetWorld.ZombiesPerPermil;
            _carryZombies = z - permil * NetWorld.ZombiesPerPermil;
            return permil;
        }

        /// <summary>
        /// Huella del jugador (modelo de Hugo): población (en unidades de 10) × 0,01 + trenes × 0,5 + nodos con gente × 0,4 (+ obras: 0,5 por nodo con obra de muros activa; el puente y las obras grandes, 1,5, llegan con el paso 4).
        /// Es lo que hace crecer la presión de la Colmena con el tamaño del jugador. En milésimas.
        /// </summary>
        public long HuellaMil(NetWorld w)
        {
            long pop = 0; int held = 0;
            for (int n = 0; n < w.Graph.Nodes.Count; n++) { if (w.Stations.IsSite(n) && !w.Stations.Founded[n]) continue; long p = Math.Max(0, w.Stock[n, (int)Res.Personas]); pop += p; if (p > 0) held++; }
            int building = 0;
            for (int n = 0; n < w.Graph.Nodes.Count; n++) if (w.WorkInProgress(n)) building++;       // hito 5: una huella por nodo con obra en curso (también de noche, Hugo D-N59 adenda 2) (partir una obra en varias no la multiplica)
            return pop / PersonsPerHugoUnit * P.HuellaPersonMil + (long)w.Services.Count * P.HuellaTrainMil + (long)held * P.HuellaNodeMil + (long)building * P.WorkHuellaMil;
        }

        /// <summary>
        /// D-N33 (Mak, A): el presupuesto de la hora entra en el campo como masa NUEVA, nunca sobre el objetivo: en el nido de Valencia
        /// (si no es el objetivo) y en la celda del borde del campo más cercana a cada nodo, repartido por peso. Después se mueve como siempre.
        /// Devuelve lo inyectado de verdad (una celda llena no admite más). La masa del campo cambia solo por esto y por las bajas (NetWorld.LostTotal).
        /// </summary>
        long Inject(NetWorld w, long amount)
        {
            if (amount <= 0) return 0;
            int nodes = w.Graph.Nodes.Count;
            long total = 0;
            var wt = new long[nodes];
            for (int n = 0; n < nodes; n++) { wt[n] = SiegeWeight(w, n); total += wt[n]; }
            if (total <= 0) return 0;
            long done = 0, given = 0;
            for (int n = 0; n < nodes; n++)
            {
                long part = n == nodes - 1 ? amount - given : amount * wt[n] / total;
                given += part;
                if (part <= 0) continue;
                var nd = w.Graph.Nodes[n];
                int cell = BorderCell(nd.XMeters, nd.YMeters);
                if (cell == w.Field.Frozen || NearPlayerNode(w, cell)) continue;      // ni en una zona en detalle ni a menos de ~10 km de un nodo del jugador (Lucía, D-N35)
                int add = (int)Math.Min(part, HordeField.Max - w.Field.D[cell]);
                if (add <= 0) continue;
                w.Field.D[cell] += add; done += add;
                w.Injections.Add(new Injection { Cell = cell, Amount = add });
            }
            w.InjectedTotal += done;
            _injectedByDay[(int)(w.Tick / NetConst.DayTicks % _injectedByDay.Length)] += done;
            return done;
        }

        readonly long[] _injectedByDay = new long[16];     // lo inyectado cada día (anillo por día mod 16), para saber qué toca retirar

        /// <summary>
        /// D-N36 (Mak, 4): los zombis inyectados que llevan RetireDays días sin combatir vuelven a los nidos y salen del campo. Aquí: el día d se
        /// retira lo inyectado el día d − RetireDays, de las celdas más alejadas de cualquier nodo, sin tocar las de un asedio en curso (8 celdas),
        /// las de un nodo del jugador ni la zona en detalle. Devuelve lo retirado de verdad.
        /// </summary>
        long Retire(NetWorld w, long amount)
        {
            if (amount <= 0) return 0;
            var f = w.Field; int nodes = w.Graph.Nodes.Count;
            var cand = new List<int>(); var dist = new Dictionary<int, int>();
            for (int c = 0; c < f.D.Length; c++)
            {
                if (f.D[c] <= 0 || c == f.Frozen) continue;
                int x = c % HordeField.W, y = c / HordeField.W;
                bool fight = false;
                foreach (var pl in Active)
                    if (pl.Kind == PlanKind.Asedio && pl.Started && Math.Max(Math.Abs(x - pl.Cell % HordeField.W), Math.Abs(y - pl.Cell / HordeField.W)) <= 8) { fight = true; break; }
                if (fight || NearPlayerNode(w, c)) continue;
                int dmin = int.MaxValue;
                for (int n = 0; n < nodes; n++) { var nd = w.Graph.Nodes[n]; int d = Math.Abs(nd.XMeters - x) + Math.Abs(nd.YMeters - y); if (d < dmin) dmin = d; }
                cand.Add(c); dist[c] = dmin;
            }
            cand.Sort((a, b) => dist[a] != dist[b] ? dist[b].CompareTo(dist[a]) : a.CompareTo(b));
            long done = 0;
            foreach (int c in cand)
            {
                if (done >= amount) break;
                int take = (int)Math.Min(f.D[c], amount - done);
                f.D[c] -= take; done += take;
                w.Retirements.Add(new Injection { Cell = c, Amount = take });
            }
            w.RetiredTotal += done; w.RetiredThisTick += done;
            return done;
        }

        const int KeepOutCells = 3;       // ~10 km: una celda del campo ronda los 3-4 km (aproximado: el mapa es de memoria)

        static bool NearPlayerNode(NetWorld w, int cell)
        {
            int cx = cell % HordeField.W, cy = cell / HordeField.W;
            for (int n = 0; n < w.Graph.Nodes.Count; n++)
            {
                if (w.Stock[n, (int)Res.Personas] <= 0) continue;
                var nd = w.Graph.Nodes[n];
                if (Math.Abs(nd.XMeters - cx) <= KeepOutCells && Math.Abs(nd.YMeters - cy) <= KeepOutCells) return true;
            }
            return false;
        }

        /// <summary>Celda del borde del campo más cercana a (x, y); empates: izquierda, derecha, arriba, abajo.</summary>
        static int BorderCell(int x, int y)
        {
            int dl = x, dr = HordeField.W - 1 - x, du = y, dd = HordeField.H - 1 - y;
            int m = Math.Min(Math.Min(dl, dr), Math.Min(du, dd));
            if (dl == m) return y * HordeField.W;
            if (dr == m) return y * HordeField.W + HordeField.W - 1;
            if (du == m) return x;
            return (HordeField.H - 1) * HordeField.W + x;
        }

        public long WorkProbes_Count, WorkProbes_Moved, WorkProbes_Asked, WorkProbes_Paid;   // estadísticas del sondeo de obra (no entran en el hash)

        /// <summary>
        /// Sondeo de obra (D-N44, Hugo): cada hora, cada nodo con una obra activa tiene un 83 ‰ de recibir un sondeo: el 5 % del presupuesto del
        /// día se concentra en su celda con Gather. Va DESCONTADO, no es masa extra (Hugo, aprobado por Mak): se paga con el 400 ‰ del presupuesto
        /// que no va a asedios, primero con la deriva de esa hora y después con lo que el canal de emboscada lleva ahorrado, así que el total de
        /// 20 días cuadra con la referencia y la masa del campo no cambia. Si no hay presupuesto suficiente el sondeo es más pequeño (o no hay).
        /// Sorteo por semilla, tick y nodo, sin estado. Si la celda del nodo es la de la zona en detalle (congelada), la horda se concentra en
        /// una celda vecina. Devuelve lo movido, en ‰ de densidad.
        /// </summary>
        long WorkProbes(NetWorld w, long day, ref long drift, ref long bank)
        {
            long ask = DayBudget(day, HuellaMil(w)) * P.WorkProbeBudgetMil / 1000 / NetWorld.ZombiesPerPermil;
            if (ask <= 0) return 0;
            long moved = 0;
            for (int n = 0; n < w.Graph.Nodes.Count; n++)
            {
                if (!w.WorkActive(n)) continue;
                if ((long)(NetWorld.Mix(0x0B5E0UL ^ Seed ^ (ulong)w.Tick ^ ((ulong)(uint)n << 40)) % 1000UL) >= P.WorkProbeMil) continue;
                long fromDrift = Math.Min(ask, Math.Max(0, drift));
                long fromBank = Math.Min(ask - fromDrift, Math.Max(0, bank));
                int pay = (int)(fromDrift + fromBank);
                WorkProbes_Count++; WorkProbes_Asked += ask;
                if (pay <= 0) continue;
                drift -= fromDrift; bank -= fromBank;
                var nd = w.Graph.Nodes[n];
                int cell = nd.YMeters * HordeField.W + nd.XMeters;
                if (cell == w.Field.Frozen)
                {
                    int cx = nd.XMeters, cy = nd.YMeters;
                    cell = cx > 0 ? cell - 1 : cx < HordeField.W - 1 ? cell + 1 : cy > 0 ? cell - HordeField.W : cell + HordeField.W;
                }
                int m = Gather(w, cell, pay);
                moved += m;
                WorkProbes_Paid += pay; WorkProbes_Moved += m;
                w.Intel.Add(new IntelEntry { Tick = w.Tick, Id = "OC-3", Vars = new[] { "nodo", nd.Name } });     // «el ruido de la obra nos ha delatado»: el sondeo llega de golpe, sin aviso previo (OC-2 necesitaría un plazo)
            }
            return moved;
        }

        public long[] Curve;                          // colmena_curva.csv (si no se carga, una aproximación lineal)

        /// <summary>Una hora de juego: caducan los planes, se elige la acción de mayor utilidad y se ejecuta.</summary>
        public void Hourly(NetWorld w)
        {
            long day = w.Tick / NetConst.DayTicks;
            if (day != _budgetDay)
            {
                _budgetDay = day; BudgetUsedToday = 0; Array.Clear(AttentionTicks, 0, AttentionTicks.Length);
                ColmenaDay(w, day);
                long old = day - P.RetireDays;
                if (old >= 0 && P.RetireDays < _injectedByDay.Length)
                {
                    int k = (int)(old % _injectedByDay.Length);
                    Retire(w, _injectedByDay[k]); _injectedByDay[k] = 0;
                }
            }
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
            foreach (var pl in Active)                                      // horda de zona: al amanecer se resuelve (fuera de pantalla)
                if (IsZoneSiege(pl) && pl.Started && w.Tick >= pl.UntilTick) w.ResolveZoneSiege(pl.Node, (long)pl.Moved * NetWorld.ZombiesPerPermil);
            foreach (var pl in Active)                                      // D-N34: un asedio que termina deja respiro de 6-12 h
                if (pl.Kind == PlanKind.Asedio && !IsZoneSiege(pl) && pl.Started && w.Tick >= pl.UntilTick)
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
                if (IsZoneSiege(pl)) { Active[i] = pl; var zn = w.Graph.Nodes[pl.Node]; w.Intel.Add(new IntelEntry { Tick = w.Tick, Id = "ES-2", Vars = new[] { "nodo", zn.Name, "n", ((long)pl.Moved * NetWorld.ZombiesPerPermil).ToString() } }); continue; }
                pl.Moved = 0;                                             // D-N33 (Mak, escala de Hugo): no hay reclutamiento inicial; el asedio recluta dentro de la curva, hora a hora
                pl.UntilTick = w.Tick + (long)P.SiegeDays * NetConst.DayTicks;
                Active[i] = pl;
                var nd = w.Graph.Nodes[pl.Node];
                w.Intel.Add(new IntelEntry { Tick = w.Tick, Id = "ES-2", Vars = new[] { "nodo", nd.Name, "n", (DayBudget(w.Tick / NetConst.DayTicks, HuellaMil(w)) * P.SiegeShareMil / 1000 * P.SiegeDays).ToString() } });   // «hasta n»: lo que el presupuesto le deja reclutar en sus días
                int focus = AttentionNode(w);
                if (focus >= 0 && focus != pl.Node)
                    w.Intel.Add(new IntelEntry { Tick = w.Tick, Id = "AT-1", Vars = new[] { "nodo_b", nd.Name, "nodo_a", w.Graph.Nodes[focus].Name } });
            }

            // presupuesto de la hora (D-N33): 600 ‰ a los asedios activos; del resto, 250 ‰ al canal de emboscada y corte y lo demás, deriva por peso
            long hb = HourBudgetPermil(day, HuellaMil(w));
            long siegeShare = hb * P.SiegeShareMil / 1000, rest = hb - siegeShare;
            bool big = day >= 2 && P.BigEveryDays > 0 && day % P.BigEveryDays == 0;     // gran ataque anunciado: el asedio golpea ×2 (se suma al presupuesto)
            long siegeFlow = big ? siegeShare * P.BigMultMil / 1000 : siegeShare;
            long ambushShare = rest * P.AmbushShareMil / 1000, drift = rest - ambushShare;
            int started = 0;
            foreach (var pl in Active) if (pl.Kind == PlanKind.Asedio && !IsZoneSiege(pl) && pl.Started) started++;
            if (started > 0)
            {
                for (int i = 0; i < Active.Count; i++)
                {
                    var pl = Active[i];
                    if (pl.Kind != PlanKind.Asedio || IsZoneSiege(pl) || !pl.Started) continue;
                    int m = Gather(w, pl.Cell, (int)(siegeFlow / started));
                    pl.Moved += m; BudgetUsedToday += m; SiegeMovedTotal += m; Active[i] = pl;
                }
            }
            else drift += siegeShare;                                       // D-N34: sin asedio, esa masa vuelve a la deriva y presiona por peso
            Inject(w, hb + (siegeFlow - siegeShare) * (started > 0 ? 1 : 0));        // D-N33 (Mak, A): masa nueva al campo, lejos del objetivo
            long bank = _ambushBank + ambushShare;                          // hito 5 (Hugo, aprobado por Mak): el sondeo de obra se paga con el 400 ‰ que no va a asedios
            long probeMoved = WorkProbes(w, day, ref drift, ref bank);      // (deriva de esta hora y, si no basta, lo ahorrado por el canal de emboscada); la masa se conserva
            _ambushBank = bank;
            BudgetUsedToday += probeMoved;
            long dm = Drift(w, drift); BudgetUsedToday += dm; DriftMovedTotal += dm; BudgetTotal += hb + (siegeFlow - siegeShare) * (started > 0 ? 1 : 0);

            // canal de asedio (D-N33): por calendario, sin competir por utilidad
            long startTod = (w.Tick + NetConst.Hours(P.SiegeWarnH)) % NetConst.DayTicks;
            bool inFirstWindow = startTod >= NetConst.Hours(P.FirstSiegeFromH) && startTod <= NetConst.Hours(P.FirstSiegeToH);
            int sieges = 0;
            foreach (var pl in Active) if (pl.Kind == PlanKind.Asedio && !IsZoneSiege(pl)) sieges++;
            foreach (var zh in ZoneHordes)                                  // hordas de zona: se anuncian a las 19:00 para las 22:00
            {
                if (zh.Node < 0 || zh.Node >= w.Graph.Nodes.Count || day < zh.FromDay || zh.Zombies <= 0 || w.NodeLost[zh.Node]) continue;
                long zst = day * NetConst.DayTicks + NetConst.Hours(22);
                if (w.Tick != zst - NetConst.Hours(P.SiegeWarnH)) continue;
                if (Active.Exists(p => p.Kind == PlanKind.Asedio && p.Node == zh.Node)) continue;
                var znd = w.Graph.Nodes[zh.Node];
                Active.Add(new Plan { Kind = PlanKind.Asedio, Edge = ZoneSiegeEdge, Node = zh.Node, Cell = znd.YMeters * HordeField.W + znd.XMeters, StartTick = zst, UntilTick = zst + NetConst.Hours(8), Moved = (zh.Zombies + NetWorld.ZombiesPerPermil - 1) / NetWorld.ZombiesPerPermil });
            }
            if (day >= P.FirstSiegeDay && (_anySiege || inFirstWindow) && sieges < MaxSieges() && w.Tick >= _respiteUntil)
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
            _ambushBank -= best.Moved; AmbushMovedTotal += best.Moved;
            if (best.Moved <= 0) return;
            Active.Add(best);


            switch (best.Kind)
            {
                case PlanKind.Corte:
                {
                    var e = w.Graph.Edges[best.Edge];
                    e.State = EdgeState.Cortada; e.Debris = true;       // escombros: hasta que el tren de obras los repare (hito 5), no se despeja sola
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

        /// <summary>
        /// Estado de manada «asedio» (Lucía §3): sale cuando pierde más del 50 % de su fuerza. La fuerza es lo que lleva reclutado (Moved);
        /// las bajas son las que deja la zona de su nodo al desmaterializar. Al terminar, esa masa vuelve a la deriva (respiro, D-N34).
        /// </summary>
        public void OnZoneLoss(NetWorld w, int node, int lost)
        {
            if (lost <= 0) return;
            for (int i = 0; i < Active.Count; i++)
            {
                var pl = Active[i];
                if (pl.Kind != PlanKind.Asedio || !pl.Started || pl.Node != node) continue;
                pl.Lost += lost;
                if ((long)pl.Lost * 2 > Math.Max(1, pl.Moved)) pl.UntilTick = w.Tick;        // se retira en la próxima hora
                Active[i] = pl;
            }
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
            int room = HordeField.Max - f.D[target];
            int moved = 0;
            int prev = 0;
            // la horda converge por anillos (8, 16, 32 y 64 celdas): primero lo cercano; si no basta, viene de más lejos (D-N35: la masa nueva entra por los bordes)
            foreach (int radius in new[] { 8, 16, 32, 64 })
            {
                if (moved >= want || moved >= room) break;
                var cand = new List<int>();
                for (int y = ty - radius; y <= ty + radius; y++)
                    for (int x = tx - radius; x <= tx + radius; x++)
                    {
                        if (x < 0 || y < 0 || x >= HordeField.W || y >= HordeField.H) continue;
                        if (Math.Max(Math.Abs(x - tx), Math.Abs(y - ty)) <= prev) continue;
                        int c = y * HordeField.W + x;
                        if (c == target || c == f.Frozen || f.D[c] <= 0) continue;
                        cand.Add(c);
                    }
                prev = radius;
                cand.Sort((p, q) => f.D[p] != f.D[q] ? f.D[q].CompareTo(f.D[p]) : p.CompareTo(q));
                foreach (int c in cand)
                {
                    if (moved >= want || moved >= room) break;
                    int take = Math.Min(f.D[c] / 2, Math.Min(want - moved, room - moved));
                    if (take <= 0) continue;
                    f.D[c] -= take;
                    f.D[target] += take;
                    moved += take;
                }
            }
            return moved;
        }

        public ulong Hash()
        {
            ulong h = NetWorld.Mix(0xD12EC7UL ^ (ulong)BudgetUsedToday ^ ((ulong)(uint)_budgetDay << 40)) + NetWorld.Mix((ulong)_respiteUntil ^ ((ulong)_ambushBank << 32)) + NetWorld.Mix((ulong)(_lastSiegeTick + 1_000_000) ^ (_anySiege ? 1UL << 62 : 0));
            foreach (var p in Active)
                h += NetWorld.Mix(((ulong)p.Kind << 56) ^ ((ulong)(uint)(p.Edge + 1) << 40) ^ ((ulong)(uint)(p.Node + 1) << 28) ^ ((ulong)(uint)(p.Cell + 1) << 8) ^ (ulong)p.UntilTick ^ ((ulong)(uint)p.Moved << 20) ^ ((ulong)(uint)(p.Train + 1) << 50)) + NetWorld.Mix((ulong)p.StartTick ^ (p.Started ? 1UL << 63 : 0)) + NetWorld.Mix((ulong)(uint)p.Lost ^ 0x10557UL);
            for (int k = 0; k < _injectedByDay.Length; k++) if (_injectedByDay[k] != 0) h += NetWorld.Mix(((ulong)(uint)k << 56) ^ (ulong)_injectedByDay[k] ^ 0x1A7ECUL);
            h += NetWorld.Mix((ulong)_carryZombies ^ 0xCA77UL);
            if (ColState != 0 || ColDaysBelow != 0 || ColSignalMil != 0) h += NetWorld.Mix(0xC01EUL ^ (ulong)(uint)ColState ^ ((ulong)(uint)ColDaysBelow << 8) ^ ((ulong)ColSignalMil << 20));   // D-N160: solo si no es el de partida
            for (int k = 0; k < ColHeld.Length; k++) if (ColHeld[k] != 0) h += NetWorld.Mix(((ulong)(uint)k << 56) ^ (ulong)(uint)ColHeld[k] ^ 0xC0E1DUL);
            for (int n = 0; n < LastSeenTick.Length; n++) if (LastSeenTick[n] != 0) h += NetWorld.Mix(((ulong)(uint)n << 52) ^ (ulong)LastSeenTick[n] ^ 0x5EE7UL);
            for (int n = 0; n < AttentionTicks.Length; n++) if (AttentionTicks[n] != 0) h += NetWorld.Mix(((ulong)(uint)n << 48) ^ (ulong)AttentionTicks[n]);
            return h;
        }
    }
}
