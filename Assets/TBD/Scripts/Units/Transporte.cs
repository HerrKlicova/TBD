using System;
using System.Collections.Generic;

namespace TBD.Units
{
    // Orden de transporte (norma del mundo persistente, D-N96; registro conjunto con el tránsito para el mapa de frentes y los 3 s de «puesto bajo ataque»: D-N120): un pelotón a pie recibe «ir a X»; solo embarca en un vehículo LIBRE, que va por carreteras habilitadas
    // o por la caja de la vía (nunca campo a través); llegan, desembarca y el vehículo queda libre. La infantería a pie sin vehículo puede ir a campo través, más despacio.
    // El ETA sale calculado antes de decidir (Estimate) y se expone para el mapa de frentes (Bruno), que lee mapa_sagunto_carreteras.json.
    // Todo en enteros (mm, bam16, ticks); LOCAL como los vehículos (los pelotones sí están en la simulación: Embark/Disembark/Move son sus órdenes).

    public enum RoadKind : byte { Carretera = 0, Via = 1, Acceso = 2 }

    /// <summary>Por qué medio va una unidad en tránsito (la ficha del mapa de frentes): vía, carretera (también los accesos), aire, o campo a través (a pie, sin vehículo).</summary>
    public enum TransitRoute : byte { Via = 0, Carretera = 1, Aire = 2, Campo = 3 }
    /// <summary>Qué icono lleva la ficha: pelotón (a pie), vehículo (el pelotón va a bordo) o helicóptero.</summary>
    public enum TransitClass : byte { Peloton = 0, Vehiculo = 1, Helicoptero = 2 }

    /// <summary>Lo que consume el mapa de frentes para dibujar una unidad en tránsito (piezas de Bruno, LEEME de «bruno-mapa/final»). Todo en el MUNDO (mm, bam16): el giro del mapa (45°) y los píxeles son cosa de la vista.</summary>
    public struct TransitInfo
    {
        public bool Active;                 // false: no está en tránsito (sin orden, llegada, cancelada o fallida)
        public TransitRoute Route;          // tipo de ruta → ficha ui_mapa_unidad_<via|carretera|aire> y color de la barra
        public TransitClass Class;          // clase de unidad → icono dentro de la ficha
        public int X, Y;                    // dónde está ahora (mm del mundo)
        public int SegDx, SegDy;            // dirección del tramo actual (mm, de su inicio a su fin) → rumbo de la muesca, tras girar el mapa
        public int Heading;                 // la misma dirección en bam16 del mundo (0 = +X, creciendo hacia +Y)
        public int SecondsLeft;             // segundos REALES hasta la llegada, redondeados hacia arriba (0 solo al llegar)
        public int ProgressPm;              // avance de 0 a 1000 por DISTANCIA recorrida del largo total de la ruta (no salta si la unidad se para)
        public long TotalMm, DoneMm;        // largo de la ruta y lo recorrido
    }

    public sealed class RoadNode { public int X, Y; }
    public sealed class RoadEdge { public int A, B, LenMm; public RoadKind Kind; public string Road; public bool Enabled; }

    /// <summary>Un camino ya calculado: puntos en mm y el tipo de cada tramo (Kinds[i] es el tramo de i a i+1).</summary>
    public sealed class RoutePath
    {
        public bool Ok; public string Why;
        public readonly List<int> X = new List<int>(), Y = new List<int>();
        public readonly List<RoadKind> Kinds = new List<RoadKind>();
        public long LengthMm, RoadMm, ViaMm, AccessMm;
        public int Corners;                    // vértices con un giro de más de 45° (cada uno cuesta unos segundos al frenar y volver a acelerar)
        public int Count { get { return X.Count; } }
    }

    /// <summary>Red de carreteras habilitadas y vía (mapa_sagunto_carreteras.json). Coordenadas del mundo en metros en el JSON; aquí, mm. Cada tramo va a lo largo de X o de Z (Y en la simulación).</summary>
    public sealed class RoadMap
    {
        struct Seg { public int X0, Y0, X1, Y1; public RoadKind Kind; public string Id; public bool Enabled; public string Why; }
        struct Site { public string Name; public int X, Y; public RoadKind Snap; }
        struct Block { public int X, Y, R; }

        readonly List<Seg> _segs = new List<Seg>();
        readonly List<Site> _sites = new List<Site>();
        readonly List<string[]> _links = new List<string[]>();
        readonly List<Block> _blocks = new List<Block>();
        Graph _g;
        bool _hasRecinto; int _rx0, _ry0, _rx1, _ry1;       // el recinto de la estación: dentro no hay carretera, los vehículos salen por la puerta sur
        public bool InRecinto(int x, int y) { return _hasRecinto && x >= _rx0 && x <= _rx1 && y >= _ry0 && y <= _ry1; }

        public sealed class Graph { public readonly List<RoadNode> Nodes = new List<RoadNode>(); public readonly List<RoadEdge> Edges = new List<RoadEdge>(); public readonly Dictionary<string, int> SiteNode = new Dictionary<string, int>(); public readonly List<List<int>> Adj = new List<List<int>>(); }

        public Graph Net { get { if (_g == null) _g = Build(null); return _g; } }
        public IList<string> DisabledRoads() { var l = new List<string>(); foreach (var s in _segs) if (!s.Enabled && s.Kind == RoadKind.Carretera) l.Add(s.Id + ": " + s.Why); return l; }

        static int Mm(object o) { double d = o is long ? (long)o : (double)o; return (int)Math.Round(d * 1000.0); }

        public static RoadMap FromJson(string json)
        {
            var root = Mini.Parse(json) as Dictionary<string, object>; if (root == null) throw new FormatException("mapa_sagunto_carreteras.json no es un objeto");
            var m = new RoadMap();
            foreach (var o in (List<object>)root["carreteras"])
            {
                var d = (Dictionary<string, object>)o; var t = (List<object>)d["tramo"]; var a = (List<object>)t[0]; var b = (List<object>)t[1];
                bool en = d.ContainsKey("habilitada") && d["habilitada"] is bool && (bool)d["habilitada"];
                m.AddSegment(Mm(a[0]), Mm(a[1]), Mm(b[0]), Mm(b[1]), RoadKind.Carretera, (string)d["id"], en, d.ContainsKey("motivo") ? (string)d["motivo"] : null);
            }
            if (root.ContainsKey("recinto")) { var rc = (List<object>)root["recinto"]; int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue; foreach (var o in rc) { var q = (List<object>)o; int qx = Mm(q[0]), qy = Mm(q[1]); x0 = Math.Min(x0, qx); x1 = Math.Max(x1, qx); y0 = Math.Min(y0, qy); y1 = Math.Max(y1, qy); } m._hasRecinto = rc.Count >= 3; m._rx0 = x0; m._ry0 = y0; m._rx1 = x1; m._ry1 = y1; }
            var via = (List<object>)root["via"]; var va = (List<object>)via[0]; var vb = (List<object>)via[1];
            m.AddSegment(Mm(va[0]), Mm(va[1]), Mm(vb[0]), Mm(vb[1]), RoadKind.Via, "via", true, null);
            if (root.ContainsKey("sitios"))
            {
                var sitios = (Dictionary<string, object>)root["sitios"];
                foreach (var kv in sitios) { var p = (List<object>)kv.Value; m.AddSite(kv.Key, Mm(p[0]), Mm(p[1]), kv.Key == "tren" || kv.Key == "estacion" ? RoadKind.Via : RoadKind.Carretera); }
            }
            return m;
        }

        public void AddSegment(int x0, int y0, int x1, int y1, RoadKind kind, string id, bool enabled, string why = null)
        {
            if (x0 != x1 && y0 != y1) throw new ArgumentException("los tramos van a lo largo de X o de Z");
            _segs.Add(new Seg { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, Kind = kind, Id = id, Enabled = enabled, Why = why }); _g = null;
        }
        /// <summary>Un sitio (estación, helipuerto, parque…): se une con un acceso al punto más cercano de una carretera (o de la vía) HABILITADA.</summary>
        public void AddSite(string name, int x, int y, RoadKind snap = RoadKind.Carretera) { _sites.Add(new Site { Name = name, X = x, Y = y, Snap = snap }); _g = null; }
        /// <summary>Acceso directo entre dos sitios (p. ej. el parque y la puerta sur: dentro del recinto no hay carretera).</summary>
        public void Link(string a, string b) { _links.Add(new[] { a, b }); _g = null; }
        /// <summary>Corta (o reabre) todo tramo cuyo punto medio quede a ≤ radio de (x, y): puerta de vía bloqueada, escombros… Se aplica a los tramos, no a los sitios.</summary>
        public void BlockNear(int x, int y, int radiusMm, bool blocked)
        {
            for (int i = 0; i < _blocks.Count; i++) if (_blocks[i].X == x && _blocks[i].Y == y && _blocks[i].R == radiusMm) { _blocks.RemoveAt(i); break; }
            if (blocked) _blocks.Add(new Block { X = x, Y = y, R = radiusMm });
            _g = null;
        }
        public void SetRoadEnabled(string id, bool enabled) { for (int i = 0; i < _segs.Count; i++) if (_segs[i].Id == id) { var s = _segs[i]; s.Enabled = enabled; _segs[i] = s; } _g = null; }
        public bool HasSite(string name) { foreach (var s in _sites) if (s.Name == name) return true; return false; }
        public bool SitePoint(string name, out int x, out int y) { foreach (var s in _sites) if (s.Name == name) { x = s.X; y = s.Y; return true; } x = y = 0; return false; }

        static long Dist2(int ax, int ay, int bx, int by) { long dx = ax - bx, dy = ay - by; return dx * dx + dy * dy; }

        /// <summary>Punto más cercano de un tramo (alineado con un eje) a (x, y).</summary>
        static void Project(Seg s, int x, int y, out int qx, out int qy)
        {
            if (s.Y0 == s.Y1) { qy = s.Y0; qx = Math.Max(Math.Min(s.X0, s.X1), Math.Min(Math.Max(s.X0, s.X1), x)); }
            else { qx = s.X0; qy = Math.Max(Math.Min(s.Y0, s.Y1), Math.Min(Math.Max(s.Y0, s.Y1), y)); }
        }
        static bool OnSeg(Seg s, int x, int y) { return x >= Math.Min(s.X0, s.X1) && x <= Math.Max(s.X0, s.X1) && y >= Math.Min(s.Y0, s.Y1) && y <= Math.Max(s.Y0, s.Y1) && (s.Y0 == s.Y1 ? y == s.Y0 : x == s.X0); }

        Graph Build(List<Site> extra)
        {
            var g = new Graph(); var idx = new Dictionary<long, int>();
            Func<int, int, int> node = (x, y) =>
            {
                long key = ((long)x << 32) ^ (uint)y; int n;
                if (!idx.TryGetValue(key, out n)) { n = g.Nodes.Count; idx[key] = n; g.Nodes.Add(new RoadNode { X = x, Y = y }); g.Adj.Add(new List<int>()); }
                return n;
            };
            var pts = new List<List<int[]>>(); for (int i = 0; i < _segs.Count; i++) pts.Add(new List<int[]> { new[] { _segs[i].X0, _segs[i].Y0 }, new[] { _segs[i].X1, _segs[i].Y1 } });
            // cruces entre tramos perpendiculares
            for (int i = 0; i < _segs.Count; i++)
                for (int j = i + 1; j < _segs.Count; j++)
                {
                    var a = _segs[i]; var b = _segs[j];
                    bool ah = a.Y0 == a.Y1, bh = b.Y0 == b.Y1; if (ah == bh) continue;
                    var h = ah ? a : b; var v = ah ? b : a;
                    int cx = v.X0, cy = h.Y0;
                    if (OnSeg(h, cx, cy) && OnSeg(v, cx, cy)) { pts[i].Add(new[] { cx, cy }); pts[j].Add(new[] { cx, cy }); }
                }
            // sitios: nodo en el sitio y acceso al punto más cercano del tipo pedido (solo tramos habilitados)
            var sites = new List<Site>(_sites); if (extra != null) sites.AddRange(extra);
            var access = new List<int[]>();       // pares de nodos unidos por un acceso (los nodos se crean luego, en orden)
            var accessPts = new List<int[]>();    // x1,y1,x2,y2
            var gateAccess = new List<int[]>();   // desde un punto del recinto hasta la puerta sur
            int gx, gy; bool hasGate = SitePoint("puerta_sur", out gx, out gy);
            foreach (var st in sites)
            {
                if (hasGate && st.Name != "puerta_sur" && InRecinto(st.X, st.Y)) { gateAccess.Add(new[] { st.X, st.Y, gx, gy }); continue; }       // dentro del recinto se sale por la puerta, no por la vía
                long best = long.MaxValue; int bi = -1, bx = 0, by = 0;
                for (int i = 0; i < _segs.Count; i++)
                {
                    var s = _segs[i]; if (!s.Enabled || s.Kind != st.Snap) continue;
                    int qx, qy; Project(s, st.X, st.Y, out qx, out qy); long d2 = Dist2(st.X, st.Y, qx, qy);
                    if (d2 < best) { best = d2; bi = i; bx = qx; by = qy; }
                }
                if (bi < 0) continue;
                pts[bi].Add(new[] { bx, by });
                accessPts.Add(new[] { st.X, st.Y, bx, by });
            }
            // nodos y tramos
            for (int i = 0; i < _segs.Count; i++)
            {
                var s = _segs[i]; var l = pts[i];
                l.Sort((p, q) => s.Y0 == s.Y1 ? p[0].CompareTo(q[0]) : p[1].CompareTo(q[1]));
                for (int k = 0; k + 1 < l.Count; k++)
                {
                    if (l[k][0] == l[k + 1][0] && l[k][1] == l[k + 1][1]) continue;
                    int a = node(l[k][0], l[k][1]), b = node(l[k + 1][0], l[k + 1][1]);
                    bool en = s.Enabled; int mx = (l[k][0] + l[k + 1][0]) / 2, my = (l[k][1] + l[k + 1][1]) / 2;
                    foreach (var bl in _blocks) if (Dist2(mx, my, bl.X, bl.Y) <= (long)bl.R * bl.R) en = false;
                    g.Edges.Add(new RoadEdge { A = a, B = b, LenMm = (int)Math.Sqrt(Dist2(l[k][0], l[k][1], l[k + 1][0], l[k + 1][1])), Kind = s.Kind, Road = s.Id, Enabled = en });
                }
            }
            foreach (var ap in accessPts)
            {
                int a = node(ap[0], ap[1]), b = node(ap[2], ap[3]);
                if (a != b) g.Edges.Add(new RoadEdge { A = a, B = b, LenMm = (int)Math.Sqrt(Dist2(ap[0], ap[1], ap[2], ap[3])), Kind = RoadKind.Acceso, Road = "acceso", Enabled = true });
            }
            foreach (var ga in gateAccess)
            {
                int a = node(ga[0], ga[1]), b = node(ga[2], ga[3]);
                if (a != b) g.Edges.Add(new RoadEdge { A = a, B = b, LenMm = (int)Math.Sqrt(Dist2(ga[0], ga[1], ga[2], ga[3])), Kind = RoadKind.Acceso, Road = "recinto", Enabled = true });
            }
            for (int i = 0; i < sites.Count; i++) g.SiteNode[sites[i].Name] = node(sites[i].X, sites[i].Y);
            foreach (var lk in _links)
            {
                int a, b; if (!g.SiteNode.TryGetValue(lk[0], out a) || !g.SiteNode.TryGetValue(lk[1], out b) || a == b) continue;
                g.Edges.Add(new RoadEdge { A = a, B = b, LenMm = (int)Math.Sqrt(Dist2(g.Nodes[a].X, g.Nodes[a].Y, g.Nodes[b].X, g.Nodes[b].Y)), Kind = RoadKind.Acceso, Road = "recinto", Enabled = true });
            }
            for (int e = 0; e < g.Edges.Count; e++) { g.Adj[g.Edges[e].A].Add(e); g.Adj[g.Edges[e].B].Add(e); }
            return g;
        }

        /// <summary>Camino más rápido para un vehículo de tierra. «viaPm»: velocidad por la vía en ‰ de la de carretera (Hugo: vía_kmh / carretera_kmh); «accessPm» igual para accesos.
        /// Los extremos pueden estar fuera de la red: se unen con un acceso al punto habilitado más cercano si queda a ≤ maxAccessMm. No hay campo a través.</summary>
        public RoutePath Route(int fx, int fy, int tx, int ty, int viaPm, int accessPm = 600, int maxAccessMm = 60_000)
        {
            if (InRecinto(fx, fy) && InRecinto(tx, ty))       // los dos dentro del recinto: en línea recta (no hay carretera dentro)
            {
                var q = new RoutePath { Ok = true }; q.X.Add(fx); q.Y.Add(fy); q.X.Add(tx); q.Y.Add(ty); q.Kinds.Add(RoadKind.Acceso); q.LengthMm = q.AccessMm = IMath.Dist(tx - fx, ty - fy); return q;
            }
            var extra = new List<Site> { new Site { Name = "\u0001a", X = fx, Y = fy, Snap = RoadKind.Carretera }, new Site { Name = "\u0001b", X = tx, Y = ty, Snap = RoadKind.Carretera } };
            // el extremo puede unirse a la vía o a una carretera: se prueban los dos tipos y gana el más cercano (se añaden ambos sitios)
            extra.Add(new Site { Name = "\u0001a", X = fx, Y = fy, Snap = RoadKind.Via }); extra.Add(new Site { Name = "\u0001b", X = tx, Y = ty, Snap = RoadKind.Via });
            var g = Build(extra);
            int s0, s1;
            if (!g.SiteNode.TryGetValue("\u0001a", out s0) || !g.SiteNode.TryGetValue("\u0001b", out s1)) return Fail("no hay red");
            // un acceso muy largo no vale: se descartan los accesos de los extremos que pasen de maxAccessMm
            int n = g.Nodes.Count; var dist = new long[n]; var prev = new int[n]; var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = long.MaxValue; prev[i] = -1; }
            dist[s0] = 0;
            while (true)
            {
                int u = -1; long best = long.MaxValue; for (int i = 0; i < n; i++) if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
                if (u < 0 || u == s1) break; done[u] = true;
                foreach (int ei in g.Adj[u])
                {
                    var e = g.Edges[ei]; if (!e.Enabled) continue;
                    if (e.Kind == RoadKind.Acceso && e.Road != "recinto" && e.LenMm > maxAccessMm) continue;
                    int v = e.A == u ? e.B : e.A; int pm = e.Kind == RoadKind.Carretera ? 1000 : e.Kind == RoadKind.Via ? viaPm : accessPm; if (pm <= 0) continue;
                    long cost = (long)e.LenMm * 1000 / pm;
                    if (dist[u] + cost < dist[v]) { dist[v] = dist[u] + cost; prev[v] = ei; }
                }
            }
            if (dist[s1] == long.MaxValue) return Fail("sin ruta por carreteras habilitadas ni por la vía");
            var rev = new List<int>(); int cur = s1;
            while (cur != s0) { int ei = prev[cur]; rev.Add(ei); cur = g.Edges[ei].A == cur ? g.Edges[ei].B : g.Edges[ei].A; }
            rev.Reverse();
            var p = new RoutePath { Ok = true }; cur = s0; p.X.Add(g.Nodes[cur].X); p.Y.Add(g.Nodes[cur].Y);
            foreach (int ei in rev)
            {
                var e = g.Edges[ei]; cur = e.A == cur ? e.B : e.A;
                p.X.Add(g.Nodes[cur].X); p.Y.Add(g.Nodes[cur].Y); p.Kinds.Add(e.Kind); p.LengthMm += e.LenMm;
                if (e.Kind == RoadKind.Carretera) p.RoadMm += e.LenMm; else if (e.Kind == RoadKind.Via) p.ViaMm += e.LenMm; else p.AccessMm += e.LenMm;
            }
            Simplify(p);
            return p;
        }

        static RoutePath Fail(string why) { return new RoutePath { Ok = false, Why = why }; }

        /// <summary>Quita los puntos intermedios alineados con el mismo tipo de tramo (menos waypoints para el piloto automático).</summary>
        static void Simplify(RoutePath p)
        {
            try { SimplifyPoints(p); } finally { p.Corners = 0; for (int i = 1; i + 1 < p.Count; i++) { int a1 = IMath.Atan2(p.Y[i] - p.Y[i - 1], p.X[i] - p.X[i - 1]), a2 = IMath.Atan2(p.Y[i + 1] - p.Y[i], p.X[i + 1] - p.X[i]); if (Math.Abs(IMath.Diff(a2, a1)) > 8192) p.Corners++; } }
        }
        static void SimplifyPoints(RoutePath p)
        {
            for (int i = 1; i + 1 < p.Count;)
            {
                bool sameKind = p.Kinds[i - 1] == p.Kinds[i];
                long cross = (long)(p.X[i] - p.X[i - 1]) * (p.Y[i + 1] - p.Y[i]) - (long)(p.Y[i] - p.Y[i - 1]) * (p.X[i + 1] - p.X[i]);
                long dot = (long)(p.X[i] - p.X[i - 1]) * (p.X[i + 1] - p.X[i]) + (long)(p.Y[i] - p.Y[i - 1]) * (p.Y[i + 1] - p.Y[i]);
                if (sameKind && cross == 0 && dot > 0) { p.X.RemoveAt(i); p.Y.RemoveAt(i); p.Kinds.RemoveAt(i); } else i++;
            }
        }

        /// <summary>El mundo de Sagunto: sitios del JSON más el parque de vehículos (99; 53) y la puerta sur (99; 43), unidos por dentro del recinto y de la puerta a la carretera vieja.</summary>
        public static RoadMap Sagunto(string json)
        {
            var m = FromJson(json);
            m.AddSite("puerta_sur", 99_000, 43_000, RoadKind.Carretera);
            m.AddSite("parque", 99_000, 53_000, RoadKind.Carretera); m.Link("parque", "puerta_sur");
            return m;
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------
    public enum TransportMode : byte { Vehiculo = 0, APie = 1, VehiculoOPie = 2 }
    public enum TransportPhase : byte { VehiculoVaPorElPeloton = 0, PelotonVaAlVehiculo = 1, EnRuta = 2, Llegado = 3, APie = 4, Cancelado = 5, Fallido = 6 }

    /// <summary>ETA y coste de un envío, listo para el mapa: ruta (puntos en mm), kilómetros, tiempo real y de juego, y combustible.</summary>
    public sealed class TransportEstimate
    {
        public bool Ok; public string Why;
        public int VehicleId = -1; public bool OnFoot;
        public readonly List<int> PickupX = new List<int>(), PickupY = new List<int>();     // el vehículo hasta el pelotón (vacío)
        public readonly List<int> RouteX = new List<int>(), RouteY = new List<int>();       // del pelotón al destino
        public readonly List<TransitRoute> RouteLegs = new List<TransitRoute>();            // tipo de cada tramo de la ruta (RouteLegs[i] va de i a i+1), para colorearla en el mapa
        public TransitRoute RouteType;                                                      // el del primer tramo con vehículo; Campo si va a pie
        public long PickupMm, RouteMm, ViaMm;
        public int EtaPickupS, EtaRouteS, EtaTotalS;       // segundos REALES
        public int EtaGameMin;                             // minutos de juego (× reloj)
        public int FuelE4;                                 // combustible del viaje completo en E4 de u
        public int ClockX = 28;
    }

    public sealed class TransportJob
    {
        public int Id, Platoon, VehicleId = -1; public TransportPhase Phase;
        public int DestX, DestY; public bool OnFoot;
        public RoutePath Pickup, Route; public int Idx; public int PhaseTick, StartTick;
        public int SavedSpeedMmS;                   // la del pelotón antes de ir a campo través
        public int StartX, StartY;                  // dónde estaba el pelotón al dar la orden
        public long DoneMm;                         // lo recorrido de la ruta, que nunca baja (el vehículo puede salirse un poco de la línea al arrancar)
        public TransportEstimate Estimate;
    }

    /// <summary>Director de transportes: da órdenes «ir a X», elige el vehículo libre, lo conduce por la ruta (piloto automático) y desembarca. Llamar a Step() una vez por tick:
    /// mueve (Step) los vehículos que están en un trabajo; el resto los mueve quien los lleve (Vehicle.OnJob dice cuáles).</summary>
    public sealed class TransportDirector
    {
        public const int CornerS = 4;                   // por vértice con giro de más de 45°: frenar, girar y volver a acelerar (medido con el piloto automático, ver pruebas)
        public const int OverheadS = 5;                 // arranque, giros y parada: calibrado con la tabla de Hugo (eta_zona_s_reales: Dragón 20 m → 6 s … 130 m → 16 s)
        public const int ClockX = 28;                   // D-N72
        public static int FootPenaltyPm = 700;          // a campo través la infantería va al 70 % de su velocidad: dato de balance_unidades.csv (infanteria_campo_traves_permil, D-N119 de Hugo); 700 si falta
        /// <summary>Lee de balance_unidades.csv la fila infanteria_campo_traves_permil (escala 1000). Llamar al cargar el balance, junto a VehicleCatalog.Load.</summary>
        public static void LoadBalance(TBD.Net.CsvTable t) { FootPenaltyPm = IMath.Clamp((int)t.TryValue("infanteria_campo_traves_permil", 700), 100, 1000); }
        public const int PivotStep = 450;               // giro en el sitio: 2,5° por tick (≈ 74°/s)
        public const int BoardTicks = 90;               // 3 s
        public const int MaxAccessMm = 60_000;           // un acceso más largo sería campo a través (D-N96): el destino a más de 60 m de una carretera habilitada o de la vía no tiene ruta
        public readonly UnitsWorld World; public readonly RoadMap Map; public readonly List<Vehicle> Vehicles = new List<Vehicle>();
        public readonly List<TransportJob> Jobs = new List<TransportJob>(); int _nextJob = 1;
        public int ViaKmh(VehicleKind k) { return k == VehicleKind.Dragon ? 30 : k == VehicleKind.Vamtac ? 25 : k == VehicleKind.Pizarro ? 35 : 25; }
        public int RoadKmh(VehicleKind k) { return k == VehicleKind.Dragon ? 80 : k == VehicleKind.Vamtac ? 90 : k == VehicleKind.Pizarro ? 50 : 90; }

        public TransportDirector(UnitsWorld world, RoadMap map) { World = world; Map = map; }

        public TransportJob Find(int id) { foreach (var j in Jobs) if (j.Id == id) return j; return null; }
        public TransportJob JobOfPlatoon(int platoon) { foreach (var j in Jobs) if (j.Platoon == platoon && j.Phase <= TransportPhase.EnRuta || j.Platoon == platoon && j.Phase == TransportPhase.APie) return j; return null; }
        bool Busy(Vehicle v) { if (v.OnJob) return true; foreach (var j in Jobs) if (j.VehicleId == v.Id && (j.Phase <= TransportPhase.EnRuta)) return true; return false; }

        // ------------------------------------------------------------------------------------------------------------------ tiempos
        /// <summary>Segundos reales de un trayecto de la zona: OverheadS más cada tramo a la velocidad del vehículo (la vía, más lenta en la proporción de Hugo: vía_kmh / carretera_kmh).</summary>
        public int EtaZoneSeconds(VehicleKind kind, RoutePath p)
        {
            if (!p.Ok) return 0; var s = VehicleCatalog.Get(kind); int cruise = Math.Max(1, s.MaxFwdMmS);
            long viaV = cruise * ViaKmh(kind) / RoadKmh(kind), accV = cruise * 6 / 10;
            double sec = OverheadS + p.Corners * CornerS + (double)p.RoadMm / cruise + (double)p.ViaMm / Math.Max(1, viaV) + (double)p.AccessMm / Math.Max(1, accV);
            return (int)Math.Ceiling(sec - 0.0001);
        }
        /// <summary>Segundos reales de un trayecto del mapa: ETA = km ÷ km/h × 3600 ÷ 28 (D-N72). Para las rutas entre estaciones (Hugo: eta_red).</summary>
        public static int EtaMapSeconds(long distanceM, int kmh, int clockX = ClockX) { if (kmh <= 0 || clockX <= 0) return 0; long den = 1000L * kmh * clockX; return (int)((distanceM * 3600 * 2 + den) / (2 * den)); }      // redondeo al segundo más cercano, como la tabla de Hugo
        public static int GameMinutes(int realSeconds, int clockX = ClockX) { return (int)(((long)realSeconds * clockX + 30) / 60); }
        /// <summary>Combustible del trayecto en E4 de u (tasa de marcha por segundo real).</summary>
        public static int FuelE4For(VehicleKind kind, int realSeconds) { return realSeconds * VehicleCatalog.Get(kind).FuelMarchE4s; }

        /// <summary>Helicóptero (los aéreos van en línea recta entre bases): ETA con despegue, crucero y toma a velocidad de crucero ≈ 80 % de la máxima.</summary>
        public static int EtaAirSeconds(HeliKind kind, int fromX, int fromY, int toX, int toY)
        {
            var s = HeliCatalog.Get(kind); long d = IMath.Dist(toX - fromX, toY - fromY); int cruise = Math.Max(1, s.MaxFwdMmS * 8 / 10);
            double sec = (double)s.CruiseAltMm / Math.Max(1, s.MaxClimbMmS) + (double)s.CruiseAltMm / Math.Max(1, s.MaxDescentMmS) + (double)d / cruise + 2;
            return (int)Math.Ceiling(sec - 0.0001);
        }

        static int Dist(int ax, int ay, int bx, int by) { return IMath.Dist(ax - bx, ay - by); }

        // ------------------------------------------------------------------------------------------------------------------ estimación y orden
        /// <summary>Calcula, SIN dar la orden, el envío: vehículo elegido, ruta, ETA, kilómetros y combustible. Es lo que lee el mapa de frentes al elegir el destino.</summary>
        public TransportEstimate Estimate(int platoon, int destX, int destY, TransportMode mode)
        {
            var est = new TransportEstimate { ClockX = ClockX };
            var p = World.Find(platoon);
            if (p == null || p.AliveCount() == 0) { est.Why = "el pelotón no existe o no tiene soldados"; return est; }
            if (p.Aboard != 0) { est.Why = "el pelotón ya va a bordo de otro transporte"; return est; }
            if (JobOfPlatoon(platoon) != null) { est.Why = "el pelotón ya tiene una orden de transporte"; return est; }
            string why = null;
            if (mode != TransportMode.APie)
            {
                Vehicle best = null; RoutePath bestPick = null, bestRoute = null; int bestEta = int.MaxValue; int worstFuel = 0;
                foreach (var v in Vehicles)
                {
                    var s = VehicleCatalog.Get(v.Kind);
                    if (Busy(v) || v.Hp <= 0 || World.SeatsUsed(v.CarrierId) > 0) continue;       // libre: sin trabajo, sin pasajeros, entero
                    if (p.AliveCount() > s.Seats) { why = why ?? "el pelotón no cabe en ningún vehículo libre"; continue; }
                    int viaPm = ViaPm(v.Kind);
                    var route = Map.Route(p.AX, p.AY, destX, destY, viaPm, 600, MaxAccessMm);
                    if (!route.Ok) { why = route.Why; continue; }
                    RoutePath pick = Dist(v.X, v.Y, p.AX, p.AY) <= 25_000 ? null : Map.Route(v.X, v.Y, p.AX, p.AY, viaPm, 600, MaxAccessMm);
                    if (pick != null && !pick.Ok) { why = "el vehículo libre no puede llegar al pelotón: " + pick.Why; continue; }
                    int eta = EtaZoneSeconds(v.Kind, route) + (pick == null ? Dist(v.X, v.Y, p.AX, p.AY) / Math.Max(1, s.MaxFwdMmS) + 1 : EtaZoneSeconds(v.Kind, pick)) + BoardTicks / IMath.TickRate;
                    int fuel = FuelE4For(v.Kind, eta);
                    if (v.FuelE4 < fuel) { why = "no hay combustible para el viaje (" + (fuel / 10_000.0).ToString("0.0") + " u)"; continue; }
                    if (eta < bestEta) { best = v; bestPick = pick; bestRoute = route; bestEta = eta; worstFuel = fuel; }
                }
                if (best != null)
                {
                    est.Ok = true; est.VehicleId = best.Id; FillRoute(est, bestPick, bestRoute, best.Kind, bestEta, worstFuel);
                    if (bestPick == null) { est.PickupX.Clear(); est.PickupY.Clear(); est.PickupMm = Dist(best.X, best.Y, p.AX, p.AY); est.EtaPickupS = Math.Max(1, (int)(est.PickupMm / Math.Max(1, VehicleCatalog.Get(best.Kind).MaxFwdMmS)) + 1); }
                    return est;
                }
                if (mode == TransportMode.Vehiculo) { est.Why = why ?? "no hay ningún vehículo libre"; return est; }
            }
            // a pie, a campo través y más despacio
            long dist = Dist(p.AX, p.AY, destX, destY); int speed = Math.Max(1, (int)((long)p.SpeedMmS * FootPenaltyPm / 1000));
            est.Ok = true; est.OnFoot = true; est.RouteMm = dist; est.RouteX.Add(p.AX); est.RouteY.Add(p.AY); est.RouteX.Add(destX); est.RouteY.Add(destY);
            est.RouteType = TransitRoute.Campo; est.RouteLegs.Add(TransitRoute.Campo);
            est.EtaRouteS = (int)Math.Ceiling(dist / (double)speed); est.EtaTotalS = est.EtaRouteS; est.EtaGameMin = GameMinutes(est.EtaTotalS);
            return est;
        }

        static TransitRoute Leg(RoadKind k) { return k == RoadKind.Via ? TransitRoute.Via : TransitRoute.Carretera; }

        int ViaPm(VehicleKind k) { return ViaKmh(k) * 1000 / RoadKmh(k); }

        void FillRoute(TransportEstimate est, RoutePath pick, RoutePath route, VehicleKind kind, int totalS, int fuel)
        {
            if (pick != null) { for (int i = 0; i < pick.Count; i++) { est.PickupX.Add(pick.X[i]); est.PickupY.Add(pick.Y[i]); } est.PickupMm = pick.LengthMm; est.EtaPickupS = EtaZoneSeconds(kind, pick); }
            for (int i = 0; i < route.Count; i++) { est.RouteX.Add(route.X[i]); est.RouteY.Add(route.Y[i]); if (i < route.Kinds.Count) est.RouteLegs.Add(Leg(route.Kinds[i])); }
            est.RouteType = est.RouteLegs.Count > 0 ? est.RouteLegs[0] : TransitRoute.Carretera;
            est.RouteMm = route.LengthMm; est.ViaMm = route.ViaMm; est.EtaRouteS = EtaZoneSeconds(kind, route); est.EtaTotalS = totalS; est.EtaGameMin = GameMinutes(totalS); est.FuelE4 = fuel;
        }

        /// <summary>Da la orden «ir a (x, y)» al pelotón. Con TransportMode.Vehiculo (por defecto) solo embarca en un vehículo libre; si no hay, devuelve null y el motivo en «why».
        /// VehiculoOPie usa un vehículo si lo hay y, si no, va a pie a campo través; APie, siempre a pie.</summary>
        public TransportJob Order(int platoon, int destX, int destY, TransportMode mode, out string why)
        {
            var est = Estimate(platoon, destX, destY, mode); why = est.Why;
            if (!est.Ok) return null;
            var p = World.Find(platoon);
            var job = new TransportJob { Id = _nextJob++, Platoon = platoon, DestX = destX, DestY = destY, StartX = p.AX, StartY = p.AY, Estimate = est, StartTick = World.Tick, OnFoot = est.OnFoot };
            if (est.OnFoot)
            {
                job.Phase = TransportPhase.APie; job.SavedSpeedMmS = p.SpeedMmS; p.SpeedMmS = Math.Max(1, (int)((long)p.SpeedMmS * FootPenaltyPm / 1000));
                World.Move(platoon, destX, destY); Jobs.Add(job); return job;
            }
            var v = VehicleById(est.VehicleId); job.VehicleId = v.Id; v.OnJob = true;
            int viaPm = ViaPm(v.Kind);
            job.Route = Map.Route(p.AX, p.AY, destX, destY, viaPm, 600, MaxAccessMm);
            if (est.PickupX.Count > 0) { job.Pickup = Map.Route(v.X, v.Y, p.AX, p.AY, viaPm, 600, MaxAccessMm); job.Phase = TransportPhase.VehiculoVaPorElPeloton; }
            else { job.Phase = TransportPhase.PelotonVaAlVehiculo; World.Move(platoon, v.X, v.Y); }
            Jobs.Add(job); return job;
        }

        // ------------------------------------------------------------------------------------------------------------------ datos para el mapa de frentes
        /// <summary>Estado de tránsito de la orden para el mapa: tipo de ruta, clase de unidad, rumbo del tramo, segundos que faltan y avance por distancia. Se puede llamar cada fotograma (no cambia nada).
        /// Mientras el vehículo va a recoger al pelotón la unidad es el pelotón, quieto, con avance 0 y el ETA completo.</summary>
        public TransitInfo Transit(int jobId) { var j = Find(jobId); return j == null ? new TransitInfo() : Transit(j); }

        public TransitInfo Transit(TransportJob j)
        {
            var t = new TransitInfo(); var p = World.Find(j.Platoon);
            if (p == null || j.Phase > TransportPhase.EnRuta && j.Phase != TransportPhase.APie) return t;
            var est = j.Estimate; t.Active = true; t.X = p.AX; t.Y = p.AY;
            if (j.Phase == TransportPhase.APie)
            {
                t.Route = TransitRoute.Campo; t.Class = TransitClass.Peloton; t.TotalMm = Dist(j.StartX, j.StartY, j.DestX, j.DestY);
                long left = Dist(p.AX, p.AY, j.DestX, j.DestY); t.DoneMm = Math.Max(0, t.TotalMm - left);
                t.SegDx = j.DestX - j.StartX; t.SegDy = j.DestY - j.StartY; t.Heading = IMath.Atan2(j.DestY - p.AY, j.DestX - p.AX);
                int speed = Math.Max(1, p.SpeedMmS); t.SecondsLeft = Math.Max(1, (int)((left + speed - 1) / speed));
                t.ProgressPm = t.TotalMm <= 0 ? 1000 : (int)(t.DoneMm * 1000 / t.TotalMm); return t;
            }
            var v = VehicleById(j.VehicleId); t.Class = TransitClass.Vehiculo; t.TotalMm = est.RouteMm;
            var path = j.Route; int n = path == null ? 0 : path.Count;
            if (j.Phase != TransportPhase.EnRuta || v == null || n < 2)
            {
                // recogida: el pelotón (quieto o yendo al vehículo) con el recorrido completo por delante
                t.Class = TransitClass.Peloton; t.ProgressPm = 0; t.DoneMm = 0;
                if (n >= 2) { t.Route = Leg(path.Kinds[0]); t.SegDx = path.X[1] - path.X[0]; t.SegDy = path.Y[1] - path.Y[0]; t.Heading = IMath.Atan2(t.SegDy, t.SegDx); }
                else t.Route = TransitRoute.Carretera;
                int pickLeft = 0;
                if (j.Phase == TransportPhase.VehiculoVaPorElPeloton && v != null && j.Pickup != null && j.Pickup.Count >= 2)
                {
                    long tot = Math.Max(1, j.Pickup.LengthMm), done = DoneAlong(j.Pickup, Math.Max(1, j.Idx), v.X, v.Y); pickLeft = (int)Math.Ceiling(est.EtaPickupS * (double)(tot - Math.Min(tot, done)) / tot);
                }
                else if (j.Phase == TransportPhase.PelotonVaAlVehiculo) pickLeft = 0;
                t.SecondsLeft = Math.Max(1, pickLeft + est.EtaRouteS + Math.Max(0, (BoardTicks - j.PhaseTick) / IMath.TickRate)); return t;
            }
            int ix = Math.Max(1, Math.Min(j.Idx, n - 1));
            t.X = v.X; t.Y = v.Y; t.Route = Leg(path.Kinds[Math.Min(ix - 1, path.Kinds.Count - 1)]);
            t.SegDx = path.X[ix] - path.X[ix - 1]; t.SegDy = path.Y[ix] - path.Y[ix - 1]; t.Heading = IMath.Atan2(t.SegDy, t.SegDx);
            long total = Math.Max(1, path.LengthMm); long doneMm = Math.Min(total, Math.Max(j.DoneMm, DoneAlong(path, ix, v.X, v.Y)));
            t.TotalMm = total; t.DoneMm = doneMm; t.ProgressPm = (int)(doneMm * 1000 / total);
            t.SecondsLeft = Math.Max(1, (int)Math.Ceiling(est.EtaRouteS * (double)(total - doneMm) / total));
            return t;
        }

        /// <summary>Distancia recorrida a lo largo de la ruta cuando el vehículo está en el tramo que acaba en «idx» (proyección sobre ese tramo).</summary>
        static long DoneAlong(RoutePath path, int idx, int x, int y)
        {
            long done = 0; for (int k = 1; k < idx && k < path.Count; k++) done += IMath.Dist(path.X[k] - path.X[k - 1], path.Y[k] - path.Y[k - 1]);
            if (idx < path.Count)
            {
                int ax = path.X[idx - 1], ay = path.Y[idx - 1], bx = path.X[idx], by = path.Y[idx]; int len = IMath.Dist(bx - ax, by - ay);
                if (len > 0) done += IMath.Clamp((int)(((long)(x - ax) * (bx - ax) + (long)(y - ay) * (by - ay)) / len), 0, len);
            }
            return done;
        }

        /// <summary>Tránsito de un helicóptero que vuela en línea recta entre bases (D-N96: los aéreos no usan carreteras). «elapsedTicks» desde el despegue; el avance es lineal por distancia.</summary>
        public static TransitInfo AirTransit(HeliKind kind, int fromX, int fromY, int toX, int toY, int elapsedTicks)
        {
            int eta = EtaAirSeconds(kind, fromX, fromY, toX, toY); long total = Dist(fromX, fromY, toX, toY);
            int el = Math.Max(0, elapsedTicks) / IMath.TickRate; double f = Math.Min(1.0, el / (double)Math.Max(1, eta));
            var t = new TransitInfo { Active = el < eta, Route = TransitRoute.Aire, Class = TransitClass.Helicoptero, TotalMm = total, SegDx = toX - fromX, SegDy = toY - fromY, Heading = IMath.Atan2(toY - fromY, toX - fromX) };
            t.DoneMm = (long)(total * f); t.ProgressPm = (int)(f * 1000); t.X = (int)(fromX + (toX - fromX) * f); t.Y = (int)(fromY + (toY - fromY) * f); t.SecondsLeft = Math.Max(el < eta ? 1 : 0, eta - el);
            return t;
        }

        public Vehicle VehicleById(int id) { foreach (var v in Vehicles) if (v.Id == id) return v; return null; }

        /// <summary>Cancela la orden: el vehículo se detiene y queda libre; el pelotón a bordo baja donde está, el de a pie se para.</summary>
        public bool Cancel(int jobId)
        {
            var j = Find(jobId); if (j == null || j.Phase > TransportPhase.EnRuta && j.Phase != TransportPhase.APie) return false;
            Finish(j, TransportPhase.Cancelado); return true;
        }

        void Finish(TransportJob j, TransportPhase phase)
        {
            var p = World.Find(j.Platoon); var v = j.VehicleId >= 0 ? VehicleById(j.VehicleId) : null;
            if (v != null) { v.OnJob = false; v.Controls(0, 0, 1000); if (p != null && p.Aboard == v.CarrierId) v.Unload(World, p.Id); }
            if (p != null && j.OnFoot) { p.SpeedMmS = j.SavedSpeedMmS; if (phase == TransportPhase.Cancelado) World.Hold(p.Id); }
            j.Phase = phase;
        }

        // ------------------------------------------------------------------------------------------------------------------ un tick
        public void Step()
        {
            for (int i = 0; i < Jobs.Count; i++)
            {
                var j = Jobs[i]; var p = World.Find(j.Platoon); var v = j.VehicleId >= 0 ? VehicleById(j.VehicleId) : null;
                if (j.Phase > TransportPhase.EnRuta && j.Phase != TransportPhase.APie) continue;
                if (p == null || p.AliveCount() == 0) { Finish(j, TransportPhase.Fallido); continue; }
                if (v != null && (v.Hp <= 0 || v.OutOfFuel)) { Finish(j, TransportPhase.Fallido); continue; }
                j.PhaseTick++;
                switch (j.Phase)
                {
                    case TransportPhase.APie:
                        if (!p.Moving) { p.SpeedMmS = j.SavedSpeedMmS; j.Phase = TransportPhase.Llegado; }
                        break;
                    case TransportPhase.VehiculoVaPorElPeloton:
                        if (Drive(v, j.Pickup, ref j.Idx)) { j.Idx = 0; j.Phase = TransportPhase.PelotonVaAlVehiculo; j.PhaseTick = 0; World.Move(p.Id, v.X, v.Y); }
                        v.Step(); break;
                    case TransportPhase.PelotonVaAlVehiculo:
                        v.Controls(0, 0, 1000); v.Step();
                        if (v.CanBoard(World, p.Id) && j.PhaseTick >= 15 && IMath.Dist(p.AX - v.X, p.AY - v.Y) <= 8000 || j.PhaseTick > 30 * 20 && v.CanBoard(World, p.Id))
                        { if (v.Board(World, p.Id)) { j.Phase = TransportPhase.EnRuta; j.PhaseTick = 0; j.Idx = 0; World.Hold(p.Id); j.Route = Map.Route(v.X, v.Y, j.DestX, j.DestY, ViaPm(v.Kind), 600, MaxAccessMm); if (!j.Route.Ok) Finish(j, TransportPhase.Fallido); } }
                        else if (j.PhaseTick > 30 * 90) Finish(j, TransportPhase.Fallido);       // el pelotón no llega al vehículo en 90 s
                        else if (j.PhaseTick % 30 == 0 && p.Moving == false) World.Move(p.Id, v.X, v.Y);
                        break;
                    case TransportPhase.EnRuta:
                        if (j.Route != null && j.Route.Count >= 2) j.DoneMm = Math.Max(j.DoneMm, DoneAlong(j.Route, Math.Max(1, Math.Min(j.Idx, j.Route.Count - 1)), v.X, v.Y));
                        if (Drive(v, j.Route, ref j.Idx))
                        {
                            v.Controls(0, 0, 1000); v.Step(); v.CarryPassengers(World);
                            if (Math.Abs(v.SpeedMmS) < 200) { v.OnJob = false; v.Unload(World, p.Id); World.Move(p.Id, j.DestX, j.DestY); j.Phase = TransportPhase.Llegado; }
                            break;
                        }
                        v.Step(); v.CarryPassengers(World); break;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------------ piloto automático
        /// <summary>Conduce al vehículo por la ruta con persecución pura: apunta a un punto que va por delante sobre el tramo (así recupera la línea aunque salga torcido), frena antes de las curvas
        /// y en la llegada. «idx» es el índice del punto al que acaba el tramo actual. Devuelve true al llegar al último punto.</summary>
        bool Drive(Vehicle v, RoutePath path, ref int idx)
        {
            var s = VehicleCatalog.Get(v.Kind);
            if (path == null || path.Count == 0) return true;
            int n = path.Count; int fx = path.X[n - 1], fy = path.Y[n - 1];
            if (n == 1) return IMath.Dist(fx - v.X, fy - v.Y) <= 4000;
            if (idx < 1) idx = 1; if (idx > n - 1) idx = n - 1;
            int turnRadius = s.Tracked ? 3000 : (int)((long)s.WheelbaseMm * IMath.Cos(s.MaxSteer) / Math.Max(1, IMath.Sin(s.MaxSteer)));
            int arrive = Math.Max(4000, turnRadius * 2 / 5);              // un vehículo no puede acertar un punto dentro de su círculo de giro: se da por llegado a esta distancia (los soldados andan el resto)
            int reach = Math.Max(5000, turnRadius * 3 / 4);                 // las curvas se redondean: se cambia de tramo antes de llegar al vértice
            int ax = 0, ay = 0, bx = 0, by = 0, segLen = 0, along = 0;
            for (int guard = 0; guard < n; guard++)
            {
                ax = path.X[idx - 1]; ay = path.Y[idx - 1]; bx = path.X[idx]; by = path.Y[idx]; segLen = IMath.Dist(bx - ax, by - ay);
                if (segLen == 0) { if (idx < n - 1) { idx++; continue; } along = 0; break; }
                along = (int)(((long)(v.X - ax) * (bx - ax) + (long)(v.Y - ay) * (by - ay)) / segLen);
                if (idx < n - 1 && segLen - along <= reach) { idx++; continue; }
                break;
            }
            bool last = idx == n - 1; int distFinal = IMath.Dist(fx - v.X, fy - v.Y);
            if (last && distFinal <= arrive) return true;
            along = IMath.Clamp(along, 0, segLen);
            int look = IMath.Clamp(Math.Abs(v.SpeedMmS), 6000, 14000);
            // punto de mira: «look» más allá de la proyección, pasando al tramo siguiente si hace falta
            int tx, ty; int ahead = along + look;
            if (ahead <= segLen || segLen == 0) { int a2 = segLen == 0 ? 0 : ahead; tx = segLen == 0 ? bx : (int)(ax + (long)(bx - ax) * a2 / segLen); ty = segLen == 0 ? by : (int)(ay + (long)(by - ay) * a2 / segLen); }
            else if (idx < n - 1)
            {
                int cx = path.X[idx + 1], cy = path.Y[idx + 1]; int l2 = IMath.Dist(cx - bx, cy - by); int rem = Math.Min(ahead - segLen, l2);
                tx = l2 == 0 ? bx : (int)(bx + (long)(cx - bx) * rem / l2); ty = l2 == 0 ? by : (int)(by + (long)(cy - by) * rem / l2);
            }
            else { tx = bx; ty = by; }
            int dx = tx - v.X, dy = ty - v.Y, dist = IMath.Dist(dx, dy);
            int want = IMath.Atan2(dy, dx), diff = IMath.Diff(want, v.Heading);
            // maniobra simplificada de giro en el sitio (sustituye al cambio de sentido en varias vueltas): casi parado y mal orientado, el vehículo gira sobre sí mismo hasta mirar al punto
            int spd = Math.Abs(v.SpeedMmS); int pivotThr = spd < 300 ? 4000 : (spd < 2000 ? (last && distFinal < turnRadius * 3 ? 5000 : 12000) : int.MaxValue);
            if (Math.Abs(diff) > pivotThr) { v.Controls(0, 0, 1000); v.Heading = (v.Heading + IMath.Clamp(diff, -PivotStep, PivotStep)) & 0xFFFF; return false; }
            int steer = IMath.Clamp(diff * 1000 / 5000, -1000, 1000);
            int cruise = s.MaxFwdMmS * 9 / 10;
            // velocidad por la vía (balasto) y por los accesos: más lenta, en la proporción de Hugo
            RoadKind kind = idx - 1 < path.Kinds.Count ? path.Kinds[idx - 1] : RoadKind.Carretera;
            if (kind == RoadKind.Via) cruise = cruise * ViaKmh(v.Kind) / RoadKmh(v.Kind); else if (kind == RoadKind.Acceso) cruise = cruise * 6 / 10;
            int target = cruise; int ad = Math.Abs(diff);
            if (ad > 24000) target = Math.Min(target, 2500); else if (ad > 9000) target = Math.Min(target, 5000);
            if (!last)
            {
                // curva a la vista: baja la velocidad según lo que haya que girar en el vértice
                int nx = path.X[idx + 1] - bx, ny = path.Y[idx + 1] - by; int turn = Math.Abs(IMath.Diff(IMath.Atan2(ny, nx), IMath.Atan2(by - ay, bx - ax)));
                int corner = Math.Max(3500, cruise * (16384 - Math.Min(turn, 16384)) / 16384);
                int toCorner = Math.Max(0, segLen - along);
                int brakeDist = Math.Max(0, (int)(((long)v.SpeedMmS * v.SpeedMmS - (long)corner * corner) / (2L * s.BrakeMmS2 / 2 + 1)));
                if (toCorner < brakeDist + reach) target = Math.Min(target, corner);
            }
            else
            {
                int stopV = (int)Math.Sqrt(2.0 * (s.BrakeMmS2 / 2) * Math.Max(0, distFinal - 1500));          // llegar parado
                if (distFinal < turnRadius * 3 && ad > 4000) target = Math.Min(target, 1800);     // el punto queda dentro del círculo de giro: a poca velocidad el volante gira más y se puede acercar
                target = Math.Min(target, Math.Max(800, stopV));
            }
            int err = target - v.SpeedMmS;
            int throttle = IMath.Clamp(err * 1000 / 2000, -1000, 1000), brake = 0;
            if (err < -1500) { brake = IMath.Clamp(-err / 4, 0, 1000); throttle = 0; }
            v.Controls(throttle, steer, brake);
            return false;
        }
    }
}
