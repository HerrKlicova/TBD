using System;
using System.Collections.Generic;

namespace TBD.Net
{
    // Hito 5 «Construir», carril 1 (diseno-hito5.md v0.3 §2b, D-N43/D-N44): las Obras por nodo de la red. Lógica pura en enteros, sin Unity.
    // - Todo lo construido en un nodo vive aquí, dentro del hash del lockstep (NetWorld.Systems, en «existencias»): la zona lo lee al
    //   materializarse y lo escribe al desmaterializarse, y el nivel de fortificación del S0 sale de esta lista y de nada más.
    // - Unidades: longitudes y coordenadas en mm (locales al nodo; el andén es el (0,0)); coste en «E4» = 1/10.000 de u (así
    //   el coste de un muro es EXACTO: precio por 10 m × longitud en mm); vida y nivel en ‰.
    // - Un muro se parte en módulos de ≤ 10 m. Cada módulo es una pieza con su coste y su vida: el daño es local y la regla del perímetro
    //   (≤ 60 m del andén) se aplica pieza a pieza, sin trucos con muros largos.

    public enum PieceKind : byte { Sacos = 0, Hesco = 1, Puerta = 2, Nido = 3, Foco = 4, Deposito = 5, Anden = 6, PuertaVia = 7 }

    public struct WorkPiece
    {
        public PieceKind Kind;
        public int X0, Y0, X1, Y1;       // mm locales al nodo (las piezas puntuales repiten el punto en X1,Y1)
        public int CostE4;               // lo pagado, en 1/10.000 de u
        public int LifeMil;              // 0..1000
        public bool Built;               // false = obra en marcha o fantasma pagado; solo lo construido cuenta para el nivel
        public int MaxWorkers;           // obreros que pueden trabajar a la vez en esta pieza (el valor del CSV; el puente del Jiloca lleva 8)
        public int Id;                   // número de la obra (una orden = una obra) dentro del nodo; cancelar actúa sobre una obra entera
        public int NeedE4;               // trabajo necesario, en 1/10.000 de obrero·tick (1 obrero·h = 1.500 obrero·ticks)
        public int WorkE4;               // trabajo hecho (mismas unidades)
        public bool IsWall => Kind == PieceKind.Sacos || Kind == PieceKind.Hesco;
        public bool IsGate => Kind == PieceKind.PuertaVia;
        // Puerta de vía (diseno-hito5.md §2a): su estado va en el registro, dentro del hash (solo las puertas de vía lo usan)
        public byte GateMode;            // 0 = automática, 1 = bloqueada abierta, 2 = bloqueada cerrada (orden del jugador)
        public int GateProg;             // 0 = cerrada … GateSwapTicks = abierta (ticks de zona; tarda 6 s en cada sentido)
        public int PaidU;                // u cobradas por la obra entera (solo la primera pieza de cada obra la guarda): cancelar sin empezar devuelve exactamente esto
        public bool Broken;              // rota: hueco con escombros por donde entra la horda, pero la vía sigue usable; no cuenta para el nivel
    }

    public struct ObrasParams
    {
        public int SacosPer10m, HescoPer10m, PuertaU, NidoU, FocoU, AndenU, DepositoU;   // precios en u (balance_construccion.csv)
        public int ModuleMm;             // longitud máxima de un módulo de muro
        public int MinWallMm;            // un muro más corto que esto (tras cortar por materiales) no se construye
        public int PerimeterMm;          // solo cuenta lo que está a esta distancia del andén (D-N43, Lucía)
        public int LevelDivU;            // u por nivel
        public int LevelMaxMil;          // nivel máximo en ‰
        public int MaxPieces;            // tope de piezas por nodo
        public int MaxVertices;          // tope de vértices de una polilínea
        public int MaxWorkersPerPiece;   // obreros que pueden trabajar a la vez en una misma pieza (obra_obreros_max_pieza; las obras grandes lo suben)
        public int GarrisonMin;          // personas que nunca trabajan: el retén (obras_guarnicion_minima de balance_construccion.csv, 6; NO la guarnicion_minima de 12 de la producción, D-N59)
        public int RepairU, RepairHours, WorksCrew;     // reparar un tramo: 5 u y 3 h con el tren de obras y 8 obreros a bordo (reparar_tramo_u, reparar_tramo_h, tren_obras_obreros)
        public int BridgeU, BridgeObreroH;              // el puente del Jiloca: 80 u (puente_jiloca_materiales, balance_economia.csv) y 192 obrero·h (puente_jiloca_obreros_h, balance_construccion.csv)
        public int WorkStartHour, WorkEndHour;          // todas las obras trabajan de 06:00 a 18:00 (obra_hora_inicio, obra_hora_fin: 12 h al día) y de noche se paran
        public int ViaGateU, HViaGate;   // puerta de vía: 8 u y 6 obrero·h (pieza_puerta_via_u, obra_puerta_via_h)
        public int SecureMm;     // terreno asegurado (diseno-hito5 §1): ningún zombi en pie a menos de esta distancia (mm) de la obra; obra_terreno_asegurado_mm
        public int TrackHalfMm;          // semiancho del hueco de la vía (3,2 m en total) respecto de y = 0; 0 = la red no conoce la vía (via_hueco_mm / 2)
        public int GateSwapTicks;        // abrir o cerrar la puerta de vía: 6 s a 30 ticks de zona por segundo = 180 ticks enteros
        public int GateNearMm;           // se abre sola con un tren propio a esta distancia o menos (150 m)
        public int OutsideWearMil;       // desgaste diario de los muros a más de 60 m del andén, sin mantenimiento posible (desgaste_fuera_perimetro_permil_dia, 10)
        public int WearDayMil;           // desgaste diario de lo construido (‰ de la vida, fort_desgaste_dia de balance_economia.csv, 10)
        public int CancelStartedMil;     // lo que se recupera al cancelar una obra ya empezada (‰); si no ha empezado, el 100 %
        public int HSacos10, HHesco10, HPuerta, HNido, HFoco, HDeposito, HAnden;   // obra en obrero·h (por 10 m en los muros)
        public int CoordLimitMm;         // |coordenada| máxima (evita desbordes)

        public static ObrasParams Default
        {
            get
            {
                return new ObrasParams
                {
                    SacosPer10m = 2, HescoPer10m = 6, PuertaU = 6, NidoU = 8, FocoU = 2, AndenU = 6, DepositoU = 5,
                    ModuleMm = 10_000, MinWallMm = 1_000, PerimeterMm = 60_000, LevelDivU = 30, LevelMaxMil = 5_000,
                    MaxPieces = 2_000, MaxVertices = 32, CoordLimitMm = 2_000_000,
                    MaxWorkersPerPiece = 4, GarrisonMin = 6, ViaGateU = 8, HViaGate = 6, SecureMm = 30_000, TrackHalfMm = 1_600, GateSwapTicks = 180, GateNearMm = 150_000, RepairU = 5, RepairHours = 3, WorksCrew = 8, BridgeU = 80, BridgeObreroH = 192, WorkStartHour = 6, WorkEndHour = 18, OutsideWearMil = 10, WearDayMil = 10, CancelStartedMil = 500,
                    HSacos10 = 2, HHesco10 = 4, HPuerta = 4, HNido = 6, HFoco = 2, HDeposito = 8, HAnden = 10
                };
            }
        }

        /// <summary>De balance_construccion.csv. Las claves que aún no están en el CSV de Hugo conservan el valor por defecto (TryValue).</summary>
        public static ObrasParams FromCsv(CsvTable t)
        {
            var d = Default;
            d.SacosPer10m = (int)t.PositiveValue("pieza_sacos_u_por_10m");
            d.HescoPer10m = (int)t.PositiveValue("pieza_hesco_u_por_10m");
            d.PuertaU = (int)t.PositiveValue("pieza_puerta_u");
            d.NidoU = (int)t.PositiveValue("pieza_nido_u");
            d.FocoU = (int)t.PositiveValue("pieza_foco_u");
            d.AndenU = (int)t.PositiveValue("pieza_anden_u");
            d.DepositoU = (int)t.PositiveValue("pieza_deposito_u");
            d.ModuleMm = (int)t.TryValue("muro_modulo_mm", d.ModuleMm);
            d.MinWallMm = (int)t.TryValue("muro_minimo_mm", d.MinWallMm);
            d.PerimeterMm = (int)t.TryValue("perimetro_mm", d.PerimeterMm);
            d.GarrisonMin = (int)t.TryValue("obras_guarnicion_minima", d.GarrisonMin);
            d.BridgeObreroH = (int)t.TryValue("puente_jiloca_obreros_h", d.BridgeObreroH);
            d.WorkStartHour = (int)t.TryValue("obra_hora_inicio", d.WorkStartHour); d.WorkEndHour = (int)t.TryValue("obra_hora_fin", d.WorkEndHour);
            d.ViaGateU = (int)t.TryValue("pieza_puerta_via_u", d.ViaGateU); d.HViaGate = (int)t.TryValue("obra_puerta_via_h", d.HViaGate);
            d.SecureMm = (int)t.TryValue("obra_terreno_asegurado_mm", d.SecureMm); d.TrackHalfMm = (int)(t.TryValue("via_hueco_mm", 2L * d.TrackHalfMm) / 2); d.GateSwapTicks = (int)t.TryValue("puerta_via_ticks", d.GateSwapTicks); d.GateNearMm = (int)t.TryValue("puerta_via_cerca_mm", d.GateNearMm);
            d.RepairU = (int)t.TryValue("reparar_tramo_u", d.RepairU); d.RepairHours = (int)t.TryValue("reparar_tramo_h", d.RepairHours); d.WorksCrew = (int)t.TryValue("tren_obras_obreros", d.WorksCrew);
            d.OutsideWearMil = (int)t.TryValue("desgaste_fuera_perimetro_permil_dia", d.OutsideWearMil);
            d.CancelStartedMil = (int)t.TryValue("cancelar_obra_empezada_permil", d.CancelStartedMil);
            d.MaxWorkersPerPiece = (int)t.TryValue("obra_obreros_max_pieza", d.MaxWorkersPerPiece);
            d.HSacos10 = (int)t.TryValue("obra_sacos_h_por_10m", d.HSacos10); d.HHesco10 = (int)t.TryValue("obra_hesco_h_por_10m", d.HHesco10);
            d.HPuerta = (int)t.TryValue("obra_puerta_h", d.HPuerta); d.HNido = (int)t.TryValue("obra_nido_h", d.HNido);
            d.HFoco = (int)t.TryValue("obra_foco_h", d.HFoco); d.HDeposito = (int)t.TryValue("obra_deposito_h", d.HDeposito); d.HAnden = (int)t.TryValue("obra_anden_h", d.HAnden);
            BalanceMath.Positive(d.MaxWorkersPerPiece, "obra_obreros_max_pieza");
            BalanceMath.Positive(d.ModuleMm, "muro_modulo_mm"); if (d.GarrisonMin < 0) throw new ArgumentOutOfRangeException("obras_guarnicion_minima", d.GarrisonMin, "El retén de obras no puede ser negativo");
            return d;
        }

        /// <summary>Añade los u por nivel de fortificación de balance_economia.csv (fort_materiales_por_nivel, 30; D-N59: sustituye a nivel_u_por_nivel).</summary>
        public ObrasParams WithEconomia(CsvTable e) { var d = this; d.LevelDivU = (int)e.TryValue("fort_materiales_por_nivel", d.LevelDivU); d.WearDayMil = (int)e.TryValue("fort_desgaste_dia", d.WearDayMil); d.BridgeU = (int)e.TryValue("puente_jiloca_materiales", d.BridgeU); BalanceMath.Positive(d.LevelDivU, "fort_materiales_por_nivel"); return d; }

        /// <summary>Trabajo de reparar un tramo normal, en obrero·ticks (1 obrero·h = 1.500 ticks): 3 h con 8 obreros = 24 obrero·h.</summary>
        public long RepairNeedTicks { get { return (long)RepairHours * WorksCrew * 1500; } }

        /// <summary>Trabajo del puente del Jiloca: 192 obrero·h (8 obreros × 12 h × 2 días). Se trabaja solo de 06:00 a 18:00: con 8 obreros son 2 días.</summary>
        public long BridgeNeedTicks { get { return (long)BridgeObreroH * 1500; } }

        /// <summary>¿Es hora de obra a esta hora del día (ticks desde las 00:00)? De WorkStartHour a WorkEndHour; fuera de ese horario todas las obras se paran sin perder progreso.</summary>
        public bool IsWorkTime(long tickOfDay) { return tickOfDay >= NetConst.Hours(WorkStartHour) && tickOfDay < NetConst.Hours(WorkEndHour); }

        public int PricePer10m(PieceKind k) { return k == PieceKind.Sacos ? SacosPer10m : HescoPer10m; }

        /// <summary>Trabajo de una pieza en 1/10.000 de obrero·tick: horas × 1.500 ticks por hora × 10.000. En muros, por la longitud en mm (÷ 10.000 mm × 10.000 = ×1).</summary>
        public int WorkE4(PieceKind k, long lengthMm)
        {
            const long TicksPerHour = 1500;
            switch (k)
            {
                case PieceKind.Sacos: return (int)(HSacos10 * TicksPerHour * lengthMm);
                case PieceKind.Hesco: return (int)(HHesco10 * TicksPerHour * lengthMm);
                case PieceKind.Puerta: return (int)(HPuerta * TicksPerHour * 10_000);
                case PieceKind.Nido: return (int)(HNido * TicksPerHour * 10_000);
                case PieceKind.Foco: return (int)(HFoco * TicksPerHour * 10_000);
                case PieceKind.Deposito: return (int)(HDeposito * TicksPerHour * 10_000);
                case PieceKind.Anden: return (int)(HAnden * TicksPerHour * 10_000);
                case PieceKind.PuertaVia: return (int)(HViaGate * TicksPerHour * 10_000);
                default: return 0;
            }
        }

        public int PointPriceU(PieceKind k)
        {
            switch (k)
            {
                case PieceKind.Puerta: return PuertaU;
                case PieceKind.Nido: return NidoU;
                case PieceKind.Foco: return FocoU;
                case PieceKind.Deposito: return DepositoU;
                case PieceKind.Anden: return AndenU;
                default: return 0;
            }
        }
    }

    /// <summary>Resultado de planificar una obra: lo mismo para la previsualización en directo que para la orden (una sola función).</summary>
    public sealed class WorkPlan
    {
        public bool Ok;                              // hay algo que construir
        public bool Cut;                             // se corta por falta de materiales o de espacio
        public int Gates;                            // puertas de vía que lleva el trazado (se ponen solas donde el muro cruza la vía)
        public long WantedMm, AllowedMm;             // longitud pedida y la que se puede pagar (muros)
        public int CostU;                            // u que se descuentan del almacén (redondeado hacia arriba)
        public long CostE4;                          // coste exacto
        public readonly List<WorkPiece> Pieces = new List<WorkPiece>();
        public int Id = -1;                          // número de obra asignado al registrarla (ObrasState.Add)
        public string Why = "";                      // motivo si no hay obra
    }

    /// <summary>
    /// Vista de la rejilla de densidad de la zona (zombis en pie por celda; los caídos y muertos no cuentan, SimCore.AddDensity) ya traducida a mm
    /// locales al nodo (andén = (0,0), vía = y 0): la celda (cx, cy) ocupa [OriginXMm + cx·CellMm, +CellMm) × [OriginYMm + cy·CellMm, +CellMm).
    /// La construye el lado de la zona (Marcos); la red solo la lee. Índice = cy · W + cx, como en la zona.
    /// </summary>
    public sealed class DensityView
    {
        public int[] D;
        public int W, H, CellMm, OriginXMm, OriginYMm;
    }

    /// <summary>«Terreno asegurado»: sin zombis en pie a menos de SecureMm del punto de la obra (diseno-hito5 §1). Enteros puros: determinista.</summary>
    public static class Terreno
    {
        /// <summary>¿Hay algún zombi en pie dentro del círculo de radio radiusMm alrededor de (x, y)? Una celda cuenta si su punto más cercano está a ≤ radio.</summary>
        public static bool IsSecure(DensityView g, long x, long y, int radiusMm)
        {
            if (g == null || g.D == null || g.CellMm <= 0) return true;
            long r = Math.Max(0, radiusMm), r2 = r * r;
            long fx = x - g.OriginXMm, fy = y - g.OriginYMm;                    // respecto de la esquina de la celda (0,0)
            long c0x = Math.Max(0, FloorDiv(fx - r, g.CellMm)), c1x = Math.Min(g.W - 1, FloorDiv(fx + r, g.CellMm));
            long c0y = Math.Max(0, FloorDiv(fy - r, g.CellMm)), c1y = Math.Min(g.H - 1, FloorDiv(fy + r, g.CellMm));
            for (long cy = c0y; cy <= c1y; cy++)
            {
                long ylo = cy * g.CellMm, yhi = ylo + g.CellMm - 1;
                long dy = fy < ylo ? ylo - fy : (fy > yhi ? fy - yhi : 0);
                for (long cx = c0x; cx <= c1x; cx++)
                {
                    if (g.D[cy * g.W + cx] <= 0) continue;
                    long xlo = cx * g.CellMm, xhi = xlo + g.CellMm - 1;
                    long dx = fx < xlo ? xlo - fx : (fx > xhi ? fx - xhi : 0);
                    if (dx * dx + dy * dy <= r2) return false;
                }
            }
            return true;
        }

        static long FloorDiv(long a, long b) { long q = a / b; return (a % b != 0 && ((a < 0) != (b < 0))) ? q - 1 : q; }

        /// <summary>
        /// ¿Está asegurada toda la obra del plan? Se comprueban los extremos de cada pieza y puntos intermedios cada SecureMm/2 como mucho,
        /// de modo que un muro largo no cruza una mancha de zombis sin que se note. Si no, devuelve el primer punto inseguro.
        /// </summary>
        public static bool PlanSecure(DensityView g, WorkPlan plan, int radiusMm, out long badX, out long badY)
        {
            badX = badY = 0;
            if (g == null || plan == null) return true;
            long step = Math.Max(1, radiusMm / 2);
            foreach (var w in plan.Pieces)
            {
                long dx = w.X1 - w.X0, dy = w.Y1 - w.Y0;
                long len = (long)Math.Sqrt((double)(dx * dx + dy * dy));       // solo decide cuántas muestras; los puntos salen con enteros
                long n = Math.Max(1, (len + step - 1) / step);
                for (long i = 0; i <= n; i++)
                {
                    long px = w.X0 + RoundDivL(dx * i, n), py = w.Y0 + RoundDivL(dy * i, n);
                    if (!IsSecure(g, px, py, radiusMm)) { badX = px; badY = py; return false; }
                }
            }
            return true;
        }

        static long RoundDivL(long a, long b) { return a >= 0 ? (a + b / 2) / b : -((-a + b / 2) / b); }
    }

    public static class Obras
    {
        public static long ISqrt(long v)
        {
            if (v <= 0) return 0;
            long x = (long)Math.Sqrt((double)v);
            while (x * x > v) x--;
            while ((x + 1) * (x + 1) <= v) x++;
            return x;
        }

        public static long SegLenMm(long x0, long y0, long x1, long y1)
        {
            long dx = x1 - x0, dy = y1 - y0;
            return ISqrt(dx * dx + dy * dy);
        }

        /// <summary>Altura de la pieza en mm (para el sólido de la zona): sacos 1,2 m, HESCO y puerta 2,1 m, nido y andén 1,5 m.</summary>
        public static int HeightMm(PieceKind k)
        {
            switch (k)
            {
                case PieceKind.Sacos: return 1200;
                case PieceKind.Hesco: return 2100;
                case PieceKind.Puerta: return 2100;
                case PieceKind.PuertaVia: return 2100;
                case PieceKind.Nido: return 1500;
                case PieceKind.Anden: return 1500;
                default: return 0;
            }
        }

        static long CeilDiv(long a, long b) { return (a + b - 1) / b; }

        /// <summary>
        /// Traza un muro (polilínea [x0,y0,x1,y1,…] en mm) con los materiales que hay. Si no alcanzan, la SIMULACIÓN decide dónde se corta:
        /// el tramo más largo cuyo coste (precio por 10 m × longitud, en u redondeadas hacia arriba) cabe en las existencias. Sin azar y
        /// sin dependencia de la interfaz. Los módulos de cada tramo son iguales entre sí (reparto entero) y suman exactamente la longitud.
        /// </summary>
        struct Sub { public long Ax, Ay, Bx, By; public bool Gate; }
        static long RoundDiv(long a, long b)                                          // a ÷ b redondeado al más cercano (b != 0), exacto y determinista
        {
            if (b < 0) { a = -a; b = -b; }
            return a >= 0 ? (2 * a + b) / (2 * b) : -((-2 * a + b) / (2 * b));
        }
        const long TS = 1_000_000;                   // escala de t al recortar un segmento contra la franja de la vía

        /// <summary>
        /// Trocea la polilínea contra la franja de la vía (|y| &lt; TrackHalfMm; la vía es y = 0 en el nodo, 3,2 m de gálibo). Un tramo que cruza
        /// la franja de lado a lado deja una puerta de vía en el cruce (el hueco medido sobre el muro: 3,2 ÷ sen α); lo que queda dentro de la franja
        /// sin cruzarla (muro a lo largo de la vía o que acaba en ella) se descarta: la vía queda libre. Cruzar con menos de 45° rechaza el trazado.
        /// </summary>
        static bool SplitAtTrack(ObrasParams p, int[] pts, List<Sub> subs, out string why)
        {
            why = "";
            long g = p.TrackHalfMm;
            for (int s = 0; s + 3 < pts.Length; s += 2)
            {
                long x0 = pts[s], y0 = pts[s + 1], x1 = pts[s + 2], y1 = pts[s + 3], dx = x1 - x0, dy = y1 - y0;
                bool outside = g <= 0 || Math.Min(y0, y1) >= g || Math.Max(y0, y1) <= -g;
                if (outside) { subs.Add(new Sub { Ax = x0, Ay = y0, Bx = x1, By = y1 }); continue; }
                if (dy == 0) continue;                                              // a lo largo de la vía, dentro del gálibo: se descarta
                long ta = (-g - y0) * TS / dy, tb = (g - y0) * TS / dy;
                long tIn = Math.Max(0, Math.Min(ta, tb)), tOut = Math.Min(TS, Math.Max(ta, tb));
                if (tIn >= tOut) { subs.Add(new Sub { Ax = x0, Ay = y0, Bx = x1, By = y1 }); continue; }
                bool crossing = (y0 <= -g && y1 >= g) || (y0 >= g && y1 <= -g);
                if (crossing && 2 * dy * dy < dx * dx + dy * dy) { why = "El muro cruza la vía demasiado en diagonal"; return false; }   // sen α < sen 45°
                long yIn = dy > 0 ? -g : g, yOut = dy > 0 ? g : -g;                 // los puntos de entrada y salida caen exactamente en el borde del gálibo
                long ax = tIn == 0 ? x0 : x0 + RoundDiv(dx * (yIn - y0), dy), ay = tIn == 0 ? y0 : yIn;
                long bx = tOut == TS ? x1 : x0 + RoundDiv(dx * (yOut - y0), dy), by = tOut == TS ? y1 : yOut;
                if (tIn > 0) subs.Add(new Sub { Ax = x0, Ay = y0, Bx = ax, By = ay });
                if (crossing) subs.Add(new Sub { Ax = ax, Ay = ay, Bx = bx, By = by, Gate = true });
                if (tOut < TS) subs.Add(new Sub { Ax = bx, Ay = by, Bx = x1, By = y1 });
            }
            return true;
        }

        /// <summary>
        /// Traza un muro (polilínea [x0,y0,x1,y1,…] en mm) con los materiales que hay. Si no alcanzan, la SIMULACIÓN decide dónde se corta:
        /// el tramo más largo cuyo coste (precio por 10 m × longitud, en u redondeadas hacia arriba) cabe en las existencias. Sin azar y
        /// sin dependencia de la interfaz. Los módulos de cada tramo son iguales entre sí (reparto entero) y suman exactamente la longitud.
        /// Donde el muro cruza la vía se añade sola una puerta de vía (8 u): entra en el coste, en el orden del trazado y en la regla de corte.
        /// </summary>
        public static WorkPlan PlanWall(ObrasParams p, PieceKind kind, int[] pts, int stockU, int pieceRoom)
        {
            var plan = new WorkPlan();
            if (kind != PieceKind.Sacos && kind != PieceKind.Hesco) { plan.Why = "no es un muro"; return plan; }
            if (pts == null || pts.Length < 4 || (pts.Length & 1) != 0 || pts.Length / 2 > p.MaxVertices) { plan.Why = "polilínea no válida"; return plan; }
            for (int i = 0; i < pts.Length; i++) if (pts[i] > p.CoordLimitMm || pts[i] < -p.CoordLimitMm) { plan.Why = "fuera de la zona"; return plan; }

            var subs = new List<Sub>();
            if (!SplitAtTrack(p, pts, subs, out string bad)) { plan.Why = bad; return plan; }
            long wanted = 0;
            var subLen = new long[subs.Count];
            for (int k = 0; k < subs.Count; k++) { subLen[k] = SegLenMm(subs[k].Ax, subs[k].Ay, subs[k].Bx, subs[k].By); if (!subs[k].Gate) wanted += subLen[k]; }
            plan.WantedMm = wanted;
            if (wanted == 0) { plan.Why = "el muro cae sobre la vía"; return plan; }

            long price = p.PricePer10m(kind);                        // u por cada 10.000 mm: coste E4 = precio × longitud en mm
            long rem = stockU <= 0 ? 0 : (long)stockU * 10_000;      // existencias en E4
            long gateE4 = (long)p.ViaGateU * 10_000;
            long allowedWall = 0;
            bool cut = false;
            for (int k = 0; k < subs.Count && !cut; k++)
            {
                var sb = subs[k];
                if (subLen[k] == 0) continue;
                if (sb.Gate)
                {
                    if (rem < gateE4) { cut = true; break; }
                    rem -= gateE4;
                    plan.Pieces.Add(new WorkPiece { Kind = PieceKind.PuertaVia, X0 = (int)sb.Ax, Y0 = (int)sb.Ay, X1 = (int)sb.Bx, Y1 = (int)sb.By, CostE4 = (int)gateE4, LifeMil = 1000, Built = false,
                        NeedE4 = p.WorkE4(PieceKind.PuertaVia, 0), MaxWorkers = p.MaxWorkersPerPiece });
                    plan.CostE4 += gateE4; plan.Gates++;
                    continue;
                }
                long len = Math.Min(subLen[k], rem / price);
                if (len < subLen[k]) cut = true;
                rem -= len * price;
                allowedWall += len;
                long dx = sb.Bx - sb.Ax, dy = sb.By - sb.Ay;
                int n = len <= 0 ? 0 : (int)CeilDiv(len, p.ModuleMm);
                long prev = 0;
                for (int j = 1; j <= n; j++)
                {
                    long t = len * j / n;                              // reparto entero: el último módulo termina exactamente en len
                    long mlen = t - prev;
                    var piece = new WorkPiece
                    {
                        Kind = kind,
                        X0 = (int)(sb.Ax + dx * prev / subLen[k]), Y0 = (int)(sb.Ay + dy * prev / subLen[k]),
                        X1 = (int)(sb.Ax + dx * t / subLen[k]), Y1 = (int)(sb.Ay + dy * t / subLen[k]),
                        CostE4 = (int)(price * mlen), LifeMil = 1000, Built = false, NeedE4 = p.WorkE4(kind, mlen), MaxWorkers = p.MaxWorkersPerPiece
                    };
                    plan.Pieces.Add(piece);
                    plan.CostE4 += piece.CostE4;
                    prev = t;
                }
            }
            plan.AllowedMm = allowedWall;
            plan.Cut = cut;
            if (allowedWall < p.MinWallMm) { plan.Pieces.Clear(); plan.CostE4 = 0; plan.Gates = 0; plan.AllowedMm = 0; plan.Why = cut ? "faltan materiales" : "muro demasiado corto"; return plan; }
            if (plan.Pieces.Count > pieceRoom) { plan.Pieces.Clear(); plan.CostE4 = 0; plan.Gates = 0; plan.Why = "demasiadas piezas en el nodo"; return plan; }
            plan.CostU = (int)CeilDiv(plan.CostE4, 10_000);
            plan.Ok = plan.Pieces.Count > 0;
            return plan;
        }

        /// <summary>Pieza puntual (puerta, nido, foco, depósito, andén): coste fijo; si no hay materiales, no se construye (no se corta).</summary>
        public static WorkPlan PlanPoint(ObrasParams p, PieceKind kind, int x, int y, int stockU, int pieceRoom)
        {
            var plan = new WorkPlan();
            int u = p.PointPriceU(kind);
            if (u <= 0) { plan.Why = "no es una pieza puntual"; return plan; }
            if (x > p.CoordLimitMm || x < -p.CoordLimitMm || y > p.CoordLimitMm || y < -p.CoordLimitMm) { plan.Why = "fuera de la zona"; return plan; }
            if (pieceRoom < 1) { plan.Why = "demasiadas piezas en el nodo"; return plan; }
            if (p.TrackHalfMm > 0 && Math.Abs((long)y) < p.TrackHalfMm) { plan.Why = "la vía queda libre"; return plan; }     // la puerta de vía se pone sola donde un muro cruza la vía
            if (stockU < u) { plan.Cut = true; plan.Why = "faltan materiales"; return plan; }
            plan.Pieces.Add(new WorkPiece { Kind = kind, X0 = x, Y0 = y, X1 = x, Y1 = y, CostE4 = u * 10_000, LifeMil = 1000, Built = false, NeedE4 = p.WorkE4(kind, 0), MaxWorkers = p.MaxWorkersPerPiece });
            plan.CostE4 = u * 10_000L;
            plan.CostU = u;
            plan.Ok = true;
            return plan;
        }

        /// <summary>Ver NetWorld.PlaceWork: Arg = nodo | (PieceKind &lt;&lt; 16), Payload = [x0,y0,x1,y1,…] o [x,y].</summary>
        public static WorkPlan Plan(ObrasParams p, PieceKind kind, int[] pts, int stockU, int pieceRoom)
        {
            if (kind == PieceKind.Sacos || kind == PieceKind.Hesco) return PlanWall(p, kind, pts, stockU, pieceRoom);
            if (pts == null || pts.Length != 2) { var bad = new WorkPlan(); bad.Why = "punto no válido"; return bad; }
            return PlanPoint(p, kind, pts[0], pts[1], stockU, pieceRoom);
        }

        // ---------------------------------------------------------------- nivel de fortificación
        /// <summary>¿Cuenta esta pieza para el nivel? Construida, defensiva (no el depósito) y a ≤ 60 m del andén (punto medio de la pieza).</summary>
        public static bool CountsForLevel(ObrasParams p, in WorkPiece w)
        {
            return w.Built && !w.Broken && w.Kind != PieceKind.Deposito && InPerimeter(p, w);
        }

        /// <summary>El punto medio de la pieza está a ≤ 60 m del andén.</summary>
        public static bool InPerimeter(ObrasParams p, in WorkPiece w)
        {
            long mx = ((long)w.X0 + w.X1) / 2, my = ((long)w.Y0 + w.Y1) / 2;
            long r = p.PerimeterMm;
            return mx * mx + my * my <= r * r;
        }

        /// <summary>Nivel en ‰ = Σ(u invertidas × vida ‰) ÷ 30 u, con tope en 5.000 ‰ (D-N43). Redondeo hacia abajo.</summary>
        public static int LevelMil(ObrasParams p, List<WorkPiece> pieces)
        {
            long sum = 0;
            if (pieces != null)
                for (int i = 0; i < pieces.Count; i++)
                {
                    var w = pieces[i];
                    if (CountsForLevel(p, w)) sum += (long)w.CostE4 * w.LifeMil;
                }
            long lvl = sum / ((long)p.LevelDivU * 10_000);
            return (int)Math.Min(lvl, p.LevelMaxMil);
        }
    }

    /// <summary>Las obras de todos los nodos de la red. Va dentro del hash de NetWorld (existencias).</summary>
    public sealed class ObrasState
    {
        public ObrasParams P;
        readonly List<WorkPiece>[] _byNode;
        public readonly int[] Workers;           // obreros asignados por nodo (población; la orden «asignar obreros»)
        public readonly int[] NextId;            // siguiente número de obra por nodo
        public readonly bool[] WasPaused;        // por nodo: la obra estaba pausada en el tick anterior (para avisar OC-4 una sola vez por parada)
        public readonly long[] MaintRem;         // por nodo: remanente del mantenimiento, en E4·‰·‰ (ver MaintDen); entra en el hash solo si no es 0
        public bool FirstNoted;                  // ya se avisó de la primera obra de la partida (OC-1, una sola vez)

        public ObrasState(int nodes, ObrasParams p) { P = p; _byNode = new List<WorkPiece>[nodes]; Workers = new int[nodes]; NextId = new int[nodes]; WasPaused = new bool[nodes]; MaintRem = new long[nodes]; }

        public int Nodes => _byNode.Length;
        public List<WorkPiece> At(int node) { return _byNode[node] ?? (_byNode[node] = new List<WorkPiece>()); }
        public int Count(int node) { return _byNode[node] == null ? 0 : _byNode[node].Count; }
        public int LevelMil(int node) { return Obras.LevelMil(P, _byNode[node]); }

        /// <summary>Plan con las existencias dadas (la previsualización y la orden usan exactamente esto).</summary>
        public WorkPlan Preview(int node, PieceKind kind, int[] pts, int stockU)
        {
            return Obras.Plan(P, kind, pts, stockU, P.MaxPieces - Count(node));
        }

        /// <summary>Aplica el plan: añade las piezas (sin construir). Quien llama descuenta los materiales (plan.CostU).</summary>
        public int Add(int node, WorkPlan plan)
        {
            if (!plan.Ok) return -1;
            int id = NextId[node]++;
            var l = At(node);
            bool first = true;
            foreach (var w in plan.Pieces)
            {
                var x = w; x.Id = id; x.PaidU = first ? plan.CostU : 0; first = false;
                if (x.IsGate)                                                    // se reconstruye encima de una puerta rota (a ≤ 3,2 m): la rota desaparece y la nueva conserva su posición exacta (no mueve el hueco)
                    for (int i = l.Count - 1; i >= 0; i--)
                        if (l[i].IsGate && l[i].Broken && Math.Abs((long)(l[i].X0 + l[i].X1) / 2 - (x.X0 + x.X1) / 2) <= 2L * P.TrackHalfMm && Math.Abs((long)(l[i].Y0 + l[i].Y1) / 2 - (x.Y0 + x.Y1) / 2) <= 2L * P.TrackHalfMm)
                        { x.X0 = l[i].X0; x.Y0 = l[i].Y0; x.X1 = l[i].X1; x.Y1 = l[i].Y1; l.RemoveAt(i); }
                l.Add(x);
            }
            return id;
        }

        /// <summary>
        /// Desgaste de un día (Hugo): el depósito no se gasta; lo que está a ≤ 60 m del andén (la Σ del nivel) pierde WearDayMil ‰ de la vida que le queda
        /// solo si el mantenimiento NO se ha pagado (paid = false); los muros a más de 60 m pierden OutsideWearMil ‰ siempre, sin mantenimiento posible
        /// (así no hay anillos exteriores eternos). Mínimo 1 ‰, redondeo al más cercano. Las piezas sin terminar no se gastan. Una pieza a 0 ‰ queda
        /// destruida y se quita (la interfaz pone los escombros). Devuelve cuántas se destruyen.
        /// </summary>
        public int WearDay(int node, bool paid = false)
        {
            var l = _byNode[node];
            if (l == null) return 0;
            int destroyed = 0;
            for (int i = l.Count - 1; i >= 0; i--)
            {
                var w = l[i];
                if (!w.Built || w.Broken || w.Kind == PieceKind.Deposito) continue;
                int rate = Obras.InPerimeter(P, w) ? (paid ? 0 : P.WearDayMil) : P.OutsideWearMil;
                if (rate <= 0) continue;
                int loss = Math.Max(1, (int)(((long)w.LifeMil * rate + 500) / 1000));
                if (w.LifeMil <= loss) { if (w.IsGate) { w.LifeMil = 0; w.Broken = true; l[i] = w; } else l.RemoveAt(i); destroyed++; } else { w.LifeMil -= loss; l[i] = w; }
            }
            return destroyed;
        }

        /// <summary>Una u de materiales en las unidades del remanente: 10.000 E4 por u, y cada día se suma Σ(CostE4 × vida ‰) × WearDayMil ‰ (E4 × ‰ × ‰).</summary>
        public const long MaintDen = 10_000L * 1000L * 1000L;

        /// <summary>
        /// Mantenimiento exacto de un día (Hugo, adenda de D-N63): suma al remanente del nodo el 1 % exacto de la Σ del nivel (la misma, con la vida) y
        /// devuelve las u que la estación tiene que poder cubrir hoy: ceil(remanente / MaintDen). Una estación con 0 u no puede cubrir ni una fracción,
        /// así que se desgasta cada día como antes; la que tiene reservas no se desgasta. Después se cierra con SettleMaintenance.
        /// </summary>
        public int AccrueMaintenance(int node)
        {
            var l = _byNode[node];
            if (l == null || P.WearDayMil <= 0) return 0;
            long value = 0;                                              // E4 × ‰
            foreach (var w in l) if (Obras.CountsForLevel(P, w)) value += (long)w.CostE4 * w.LifeMil;
            MaintRem[node] += value * P.WearDayMil;
            return (int)((MaintRem[node] + MaintDen - 1) / MaintDen);
        }

        /// <summary>
        /// Cierra el mantenimiento del día y devuelve las u que se cobran: si se pudo cubrir, floor(remanente / MaintDen) y el remanente baja en eso (queda
        /// la fracción, dentro del hash); si no, no se cobra nada, ese día se aplica el desgaste y solo se conserva la fracción (no hay deuda que arrastrar).
        /// Así un apeadero de 30 u paga 0,3 u al día de media y Sagunto 1,2 u, no un ceil por estación.
        /// </summary>
        public int SettleMaintenance(int node, bool paid)
        {
            long whole = MaintRem[node] / MaintDen;
            if (!paid) { MaintRem[node] -= whole * MaintDen; return 0; }
            MaintRem[node] -= whole * MaintDen;
            return (int)whole;
        }

        /// <summary>
        /// Techo del mantenimiento de un día (Hugo, #8): ceil(WearDayMil ‰ de Σ(CostE4 × vida)) con la misma Σ que el nivel de fortificación, en u de materiales del almacén.
        /// Si la estación puede pagarlo no hay desgaste; si no, se aplica WearDay (la caída geométrica).
        /// </summary>
        public int MaintenanceU(int node)
        {
            var l = _byNode[node];
            if (l == null || P.WearDayMil <= 0) return 0;
            long value = 0;                                              // E4 × ‰
            foreach (var w in l) if (Obras.CountsForLevel(P, w)) value += (long)w.CostE4 * w.LifeMil;     // la misma Σ que el nivel (Hugo): piezas defensivas en pie dentro del perímetro, sin el depósito
            long den = 1000L * 1000L * 10_000L;
            return (int)((value * P.WearDayMil + den - 1) / den);
        }

        /// <summary>Daño de combate a una pieza construida (la pila, los pesados; lo llamará la zona). Devuelve true si la pieza queda destruida (y se quita).</summary>
        public bool Damage(int node, int index, int lifeMil)
        {
            var l = _byNode[node];
            if (l == null || index < 0 || index >= l.Count || !l[index].Built || l[index].Broken || lifeMil <= 0) return false;
            var w = l[index];
            if (w.LifeMil <= lifeMil)
            {
                if (w.IsGate) { w.LifeMil = 0; w.Broken = true; l[index] = w; return true; }     // la puerta de vía rota deja un hueco con escombros, no desaparece
                l.RemoveAt(index); return true;
            }
            w.LifeMil -= lifeMil; l[index] = w;
            return false;
        }

        /// <summary>Hay una obra en marcha: obreros asignados y alguna pieza sin terminar. Es lo que atrae a la Colmena (sondeo de obra, D-N44).</summary>
        public bool IsActive(int node)
        {
            if (Workers[node] <= 0 || _byNode[node] == null) return false;
            foreach (var w in _byNode[node]) if (!w.Built) return true;
            return false;
        }

        /// <summary>
        /// Un tick de trabajo con «workers» obreros efectivos: se reparte por orden de obra (la más antigua primero), como mucho
        /// MaxWorkersPerPiece por pieza, y cada obrero aporta 1 obrero·tick. Devuelve cuántas piezas se terminan.
        /// </summary>
        public int Advance(int node, int workers)
        {
            var l = _byNode[node];
            if (l == null || workers <= 0) return 0;
            int done = 0;
            for (int i = 0; i < l.Count && workers > 0; i++)
            {
                var w = l[i];
                if (w.Built) continue;
                int take = Math.Min(workers, w.MaxWorkers > 0 ? w.MaxWorkers : P.MaxWorkersPerPiece);
                workers -= take;
                long total = w.WorkE4 + (long)take * 10_000;
                if (total >= w.NeedE4) { w.WorkE4 = w.NeedE4; w.Built = true; done++; } else w.WorkE4 = (int)total;
                l[i] = w;
            }
            return done;
        }

        /// <summary>
        /// Cancela una obra entera (lo que no está terminado): recupera el 100 % de lo pagado si nadie ha empezado y CancelStartedMil ‰ si ya
        /// hay trabajo hecho (D-N44). Lo terminado se queda. Devuelve las u que vuelven al almacén (hacia abajo).
        /// </summary>
        public int Cancel(int node, int id)
        {
            var l = _byNode[node];
            if (l == null) return 0;
            bool started = false, any = false;
            foreach (var w in l) if (w.Id == id && !w.Built) { any = true; if (w.WorkE4 > 0) started = true; }
            if (!any) return 0;
            foreach (var w in l) if (w.Id == id && w.Built) started = true;       // una obra que ya tiene piezas terminadas «está en marcha»
            // Hugo (D-N59): sin empezar se devuelve EXACTAMENTE lo cobrado (las u de la orden); empezada, floor(50 % de lo cobrado) por la parte sin terminar
            long pct = started ? P.CancelStartedMil : 1000, paid = 0, totalE4 = 0, unbuiltE4 = 0;
            foreach (var w in l) if (w.Id == id) { paid += w.PaidU; totalE4 += w.CostE4; if (!w.Built) unbuiltE4 += w.CostE4; }
            for (int i = l.Count - 1; i >= 0; i--)
                if (l[i].Id == id && !l[i].Built) l.RemoveAt(i);
            if (l.Count == 0) _byNode[node] = null;
            return totalE4 <= 0 ? 0 : (int)(paid * unbuiltE4 * pct / (totalE4 * 1000));
        }

        /// <summary>
        /// PROVISIONAL hasta la orden «asignar obreros» (paso 2 del hito 5): da por terminadas todas las piezas pagadas del nodo.
        /// </summary>
        public void CompleteAll(int node)
        {
            var l = _byNode[node];
            if (l == null) return;
            for (int i = 0; i < l.Count; i++) { var w = l[i]; w.Built = true; l[i] = w; }
        }

        // ---------------------------------------------------------------- puerta de vía (diseno-hito5.md §2a)
        /// <summary>Un tick de zona de una puerta: va hacia abierta si el modo la bloquea abierta o, en automático, hay un tren propio cerca; si no, hacia cerrada. Tarda GateSwapTicks.</summary>
        public static void StepGate(in ObrasParams p, ref WorkPiece w, bool trainNear)
        {
            if (!w.IsGate || !w.Built || w.Broken) return;
            bool open = w.GateMode == 1 ? true : w.GateMode == 2 ? false : trainNear;
            if (open) { if (w.GateProg < p.GateSwapTicks) w.GateProg++; }
            else if (w.GateProg > 0) w.GateProg--;
        }

        /// <summary>El tren pasa por la puerta si está del todo abierta o si está rota (la vía sigue siendo usable). Cerrada, el tren se para antes de llegar.</summary>
        public static bool GatePassable(in ObrasParams p, in WorkPiece w) { return w.IsGate && w.Built && (w.Broken || w.GateProg >= p.GateSwapTicks); }

        /// <summary>Un tick de zona de todas las puertas del nodo. trainXMm = posición (x local, en mm, sobre la vía y = 0) de los trenes propios.</summary>
        public void StepGates(int node, IList<int> trainXMm)
        {
            var l = _byNode[node];
            if (l == null) return;
            for (int i = 0; i < l.Count; i++)
            {
                var w = l[i];
                if (!w.IsGate || !w.Built || w.Broken) continue;
                long mx = ((long)w.X0 + w.X1) / 2; bool near = false;
                if (trainXMm != null) for (int t = 0; t < trainXMm.Count; t++) if (Math.Abs(trainXMm[t] - mx) <= P.GateNearMm) { near = true; break; }
                int before = w.GateProg; StepGate(P, ref w, near);
                if (w.GateProg != before) l[i] = w;
            }
        }

        /// <summary>Orden del jugador sobre la puerta de vía más cercana a (x, y) (a ≤ 3,2 m): mode 0 = automática, 1 = bloqueada abierta, 2 = bloqueada cerrada.</summary>
        public bool SetGateMode(int node, int x, int y, int mode)
        {
            var l = _byNode[node];
            if (l == null || mode < 0 || mode > 2) return false;
            int best = -1; long bestD = long.MaxValue;
            for (int i = 0; i < l.Count; i++)
            {
                var w = l[i];
                if (!w.IsGate || !w.Built || w.Broken) continue;
                long dx = (long)(w.X0 + w.X1) / 2 - x, dy = (long)(w.Y0 + w.Y1) / 2 - y, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = i; }
            }
            long lim = 2L * P.TrackHalfMm;
            if (best < 0 || bestD > lim * lim) return false;
            var g = l[best]; g.GateMode = (byte)mode; l[best] = g;
            return true;
        }

        /// <summary>Copia de las piezas del nodo: es lo que lee la zona al materializarse (y lo que escribe al salir con Restore).</summary>
        public List<WorkPiece> Snapshot(int node) { return _byNode[node] == null ? new List<WorkPiece>() : new List<WorkPiece>(_byNode[node]); }

        public void Restore(int node, List<WorkPiece> pieces) { _byNode[node] = pieces == null || pieces.Count == 0 ? null : new List<WorkPiece>(pieces); }

        public ulong Hash()
        {
            ulong h = 0;
            for (int n = 0; n < _byNode.Length; n++)
            {
                var l = _byNode[n];
                if (l == null) continue;
                for (int i = 0; i < l.Count; i++)
                {
                    var w = l[i];
                    ulong a = ((ulong)(uint)w.X0 << 32) | (uint)w.Y0;
                    ulong b = ((ulong)(uint)w.X1 << 32) | (uint)w.Y1;
                    ulong c = (ulong)(uint)w.CostE4 | ((ulong)(uint)w.LifeMil << 32) | ((ulong)w.Kind << 52) | (w.Built ? 1UL << 60 : 0UL);
                    ulong d = (ulong)(uint)w.NeedE4 | ((ulong)(uint)w.WorkE4 << 32);
                    h += NetWorld.Mix(NetWorld.Mix(NetWorld.Mix(NetWorld.Mix(0x0B2A5UL ^ ((ulong)(uint)n << 40) ^ (ulong)(uint)i) ^ a) ^ b) ^ NetWorld.Mix(c) ^ NetWorld.Mix(d ^ ((ulong)(uint)w.Id << 20) ^ ((ulong)(uint)w.MaxWorkers << 44)));
                    if (w.PaidU != 0) h += NetWorld.Mix(0x9A1DUL ^ ((ulong)(uint)n << 48) ^ ((ulong)(uint)i << 32) ^ (ulong)(uint)w.PaidU);
                    if (w.IsGate) h += NetWorld.Mix(0x6A7E5UL ^ ((ulong)(uint)n << 48) ^ ((ulong)(uint)i << 32) ^ ((ulong)w.GateMode << 24) ^ ((ulong)(uint)w.GateProg << 4) ^ (w.Broken ? 1UL : 0UL));     // estado de la puerta de vía (solo ellas)
                }
            }
            if (FirstNoted) h += NetWorld.Mix(0x0C1F1UL);
            for (int n = 0; n < WasPaused.Length; n++) if (WasPaused[n]) h += NetWorld.Mix(0x0C4A5UL ^ ((ulong)(uint)n << 40));
            for (int n = 0; n < MaintRem.Length; n++) if (MaintRem[n] != 0) h += NetWorld.Mix(0x4D41494EUL ^ ((ulong)(uint)n << 40) ^ (ulong)MaintRem[n]);     // remanente del mantenimiento (solo si no es 0)
            for (int n = 0; n < Workers.Length; n++)
                if (Workers[n] != 0 || NextId[n] != 0) h += NetWorld.Mix(0x0B0BEUL ^ ((ulong)(uint)n << 40) ^ ((ulong)(uint)Workers[n] << 16) ^ (ulong)(uint)NextId[n]);
            return h;
        }
    }
}
