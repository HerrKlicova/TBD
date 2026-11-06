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

    public enum PieceKind : byte { Sacos = 0, Hesco = 1, Puerta = 2, Nido = 3, Foco = 4, Deposito = 5, Anden = 6 }

    public struct WorkPiece
    {
        public PieceKind Kind;
        public int X0, Y0, X1, Y1;       // mm locales al nodo (las piezas puntuales repiten el punto en X1,Y1)
        public int CostE4;               // lo pagado, en 1/10.000 de u
        public int LifeMil;              // 0..1000
        public bool Built;               // false = obra en marcha o fantasma pagado; solo lo construido cuenta para el nivel
        public int Id;                   // número de la obra (una orden = una obra) dentro del nodo; cancelar actúa sobre una obra entera
        public int NeedE4;               // trabajo necesario, en 1/10.000 de obrero·tick (1 obrero·h = 1.500 obrero·ticks)
        public int WorkE4;               // trabajo hecho (mismas unidades)
        public bool IsWall => Kind == PieceKind.Sacos || Kind == PieceKind.Hesco;
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
        public int MaxWorkersPerPiece;   // obreros que pueden trabajar a la vez en una misma pieza
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
                    MaxWorkersPerPiece = 4, CancelStartedMil = 500,
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
            d.LevelDivU = (int)t.TryValue("nivel_u_por_nivel", d.LevelDivU);
            d.CancelStartedMil = (int)t.TryValue("cancelar_obra_empezada_permil", d.CancelStartedMil);
            d.MaxWorkersPerPiece = (int)t.TryValue("obra_max_obreros_por_pieza", d.MaxWorkersPerPiece);
            d.HSacos10 = (int)t.TryValue("obra_sacos_h_por_10m", d.HSacos10); d.HHesco10 = (int)t.TryValue("obra_hesco_h_por_10m", d.HHesco10);
            d.HPuerta = (int)t.TryValue("obra_puerta_h", d.HPuerta); d.HNido = (int)t.TryValue("obra_nido_h", d.HNido);
            d.HFoco = (int)t.TryValue("obra_foco_h", d.HFoco); d.HDeposito = (int)t.TryValue("obra_deposito_h", d.HDeposito); d.HAnden = (int)t.TryValue("obra_anden_h", d.HAnden);
            BalanceMath.Positive(d.MaxWorkersPerPiece, "obra_max_obreros_por_pieza");
            BalanceMath.Positive(d.ModuleMm, "muro_modulo_mm"); BalanceMath.Positive(d.LevelDivU, "nivel_u_por_nivel");
            return d;
        }

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
        public long WantedMm, AllowedMm;             // longitud pedida y la que se puede pagar (muros)
        public int CostU;                            // u que se descuentan del almacén (redondeado hacia arriba)
        public long CostE4;                          // coste exacto
        public readonly List<WorkPiece> Pieces = new List<WorkPiece>();
        public int Id = -1;                          // número de obra asignado al registrarla (ObrasState.Add)
        public string Why = "";                      // motivo si no hay obra
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
        public static WorkPlan PlanWall(ObrasParams p, PieceKind kind, int[] pts, int stockU, int pieceRoom)
        {
            var plan = new WorkPlan();
            if (kind != PieceKind.Sacos && kind != PieceKind.Hesco) { plan.Why = "no es un muro"; return plan; }
            if (pts == null || pts.Length < 4 || (pts.Length & 1) != 0 || pts.Length / 2 > p.MaxVertices) { plan.Why = "polilínea no válida"; return plan; }
            for (int i = 0; i < pts.Length; i++) if (pts[i] > p.CoordLimitMm || pts[i] < -p.CoordLimitMm) { plan.Why = "fuera de la zona"; return plan; }

            int nSeg = pts.Length / 2 - 1;
            var segLen = new long[nSeg];
            long total = 0;
            for (int s = 0; s < nSeg; s++) { segLen[s] = SegLenMm(pts[2 * s], pts[2 * s + 1], pts[2 * s + 2], pts[2 * s + 3]); total += segLen[s]; }
            plan.WantedMm = total;

            long price = p.PricePer10m(kind);                        // u por cada 10.000 mm: coste E4 = precio × longitud en mm
            long maxByStock = stockU <= 0 ? 0 : (long)stockU * 10_000 / price;
            long allowed = Math.Min(total, maxByStock);
            plan.AllowedMm = allowed;
            plan.Cut = allowed < total;
            if (allowed < p.MinWallMm) { plan.Why = plan.Cut ? "faltan materiales" : "muro demasiado corto"; plan.AllowedMm = 0; return plan; }

            long left = allowed;
            for (int s = 0; s < nSeg && left > 0; s++)
            {
                if (segLen[s] == 0) continue;
                long len = Math.Min(segLen[s], left);
                left -= len;
                long ax = pts[2 * s], ay = pts[2 * s + 1], dx = pts[2 * s + 2] - ax, dy = pts[2 * s + 3] - ay;
                int n = (int)CeilDiv(len, p.ModuleMm);
                long prev = 0;
                for (int j = 1; j <= n; j++)
                {
                    long t = len * j / n;                              // reparto entero: el último módulo termina exactamente en len
                    long mlen = t - prev;
                    var piece = new WorkPiece
                    {
                        Kind = kind,
                        X0 = (int)(ax + dx * prev / segLen[s]), Y0 = (int)(ay + dy * prev / segLen[s]),
                        X1 = (int)(ax + dx * t / segLen[s]), Y1 = (int)(ay + dy * t / segLen[s]),
                        CostE4 = (int)(price * mlen), LifeMil = 1000, Built = false, NeedE4 = p.WorkE4(kind, mlen)
                    };
                    plan.Pieces.Add(piece);
                    plan.CostE4 += piece.CostE4;
                    prev = t;
                }
            }
            if (plan.Pieces.Count > pieceRoom) { plan.Pieces.Clear(); plan.CostE4 = 0; plan.Why = "demasiadas piezas en el nodo"; return plan; }
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
            if (stockU < u) { plan.Cut = true; plan.Why = "faltan materiales"; return plan; }
            plan.Pieces.Add(new WorkPiece { Kind = kind, X0 = x, Y0 = y, X1 = x, Y1 = y, CostE4 = u * 10_000, LifeMil = 1000, Built = false, NeedE4 = p.WorkE4(kind, 0) });
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
            if (!w.Built || w.Kind == PieceKind.Deposito) return false;
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

        public ObrasState(int nodes, ObrasParams p) { P = p; _byNode = new List<WorkPiece>[nodes]; Workers = new int[nodes]; NextId = new int[nodes]; }

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
            foreach (var w in plan.Pieces) { var x = w; x.Id = id; l.Add(x); }
            return id;
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
                int take = Math.Min(workers, P.MaxWorkersPerPiece);
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
            long pct = started ? P.CancelStartedMil : 1000, refund = 0;
            for (int i = l.Count - 1; i >= 0; i--)
                if (l[i].Id == id && !l[i].Built) { refund += l[i].CostE4 * pct / 1000; l.RemoveAt(i); }
            if (l.Count == 0) _byNode[node] = null;
            return (int)(refund / 10_000);
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
                    h += NetWorld.Mix(NetWorld.Mix(NetWorld.Mix(NetWorld.Mix(0x0B2A5UL ^ ((ulong)(uint)n << 40) ^ (ulong)(uint)i) ^ a) ^ b) ^ NetWorld.Mix(c) ^ NetWorld.Mix(d ^ ((ulong)(uint)w.Id << 20)));
                }
            }
            for (int n = 0; n < Workers.Length; n++)
                if (Workers[n] != 0 || NextId[n] != 0) h += NetWorld.Mix(0x0B0BEUL ^ ((ulong)(uint)n << 40) ^ ((ulong)(uint)Workers[n] << 16) ^ (ulong)(uint)NextId[n]);
            return h;
        }
    }
}
