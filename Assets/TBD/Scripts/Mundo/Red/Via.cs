using System;
using System.Collections.Generic;
using TBD.Net;
using TBD.Units;

namespace TBD.Mundo
{
    /// <summary>
    /// Núcleo v2, Mundo.Red.Via (Lucía, plan-nucleo-v2.md; Eugenio: coop, 11-nov 22:47): la vía la traza el jugador. Grafo de nodos y tramos en milímetros enteros
    /// (lockstep: el mismo resultado en las dos máquinas). Un tramo es una curva suave por los puntos de control (Catmull-Rom muestreada cada PasoMm),
    /// guardada como polilínea con su longitud acumulada. Es lógica pura: la vista solo la dibuja.
    /// </summary>
    public sealed class Via
    {
        public sealed class Nodo { public int Id, X, Y, Estacion = -1; }
        public sealed class Tramo { public int Id, A, B, LongMm; public PointMm[] Puntos; public int[] Acum; }
        public sealed class Estacion { public int Id, Nodo; public string Nombre; }

        /// <summary>Muestreo de la curva (mm). Radio mínimo de curva (mm): por debajo, el trazado se rechaza. Encaje (mm): un extremo a menos de esto se une al nodo existente.</summary>
        public const int PasoMm = 2_000, RadioMinMm = 60_000, EncajeMm = 12_000, LongMinMm = 10_000;

        public readonly List<Nodo> Nodos = new List<Nodo>();
        public readonly List<Tramo> Tramos = new List<Tramo>();
        public readonly List<Estacion> Estaciones = new List<Estacion>();

        public int AddEstacion(string nombre, int x, int y)
        {
            var n = new Nodo { Id = Nodos.Count, X = x, Y = y }; Nodos.Add(n);
            var e = new Estacion { Id = Estaciones.Count, Nombre = nombre, Nodo = n.Id }; Estaciones.Add(e); n.Estacion = e.Id;
            return e.Id;
        }

        public struct Resultado { public bool Ok; public string Por; public int Tramo, RadioMinMm; }

        /// <summary>Traza un tramo por los puntos de control (al menos 2, en mm). Los extremos se encajan en un nodo existente a menos de EncajeMm o crean uno nuevo.
        /// Se rechaza si una curva baja de RadioMinMm, si es más corto que LongMinMm o si une un nodo consigo mismo. No modifica nada si se rechaza.</summary>
        public Resultado Trazar(IList<PointMm> control)
        {
            if (control == null || control.Count < 2) return new Resultado { Por = "hacen falta al menos dos puntos" };
            int na = NodoCerca(control[0].X, control[0].Y), nb = NodoCerca(control[control.Count - 1].X, control[control.Count - 1].Y);
            if (na >= 0 && na == nb) return new Resultado { Por = "la vía empieza y acaba en el mismo sitio" };
            var cp = new List<PointMm>(control);
            if (na >= 0) cp[0] = new PointMm(Nodos[na].X, Nodos[na].Y);
            if (nb >= 0) cp[cp.Count - 1] = new PointMm(Nodos[nb].X, Nodos[nb].Y);
            var pts = Muestrear(cp);
            if (pts.Count < 2) return new Resultado { Por = "vía demasiado corta" };
            var acum = new int[pts.Count]; for (int i = 1; i < pts.Count; i++) acum[i] = acum[i - 1] + IMath.Dist(pts[i].X - pts[i - 1].X, pts[i].Y - pts[i - 1].Y);
            if (acum[acum.Length - 1] < LongMinMm) return new Resultado { Por = "vía demasiado corta" };
            int rmin = RadioMinimo(pts);
            if (rmin < RadioMinMm) return new Resultado { Por = $"curva demasiado cerrada ({rmin / 1000} m; mínimo {RadioMinMm / 1000} m)", RadioMinMm = rmin };
            if (na < 0) { na = Nodos.Count; Nodos.Add(new Nodo { Id = na, X = cp[0].X, Y = cp[0].Y }); }
            if (nb < 0) { nb = Nodos.Count; Nodos.Add(new Nodo { Id = nb, X = cp[cp.Count - 1].X, Y = cp[cp.Count - 1].Y }); }
            var t = new Tramo { Id = Tramos.Count, A = na, B = nb, Puntos = pts.ToArray(), Acum = acum, LongMm = acum[acum.Length - 1] };
            Tramos.Add(t);
            return new Resultado { Ok = true, Tramo = t.Id, RadioMinMm = rmin };
        }

        public int NodoCerca(int x, int y)
        {
            int best = -1; long bd = (long)EncajeMm * EncajeMm;
            for (int i = 0; i < Nodos.Count; i++) { long dx = Nodos[i].X - x, dy = Nodos[i].Y - y, d = dx * dx + dy * dy; if (d <= bd) { bd = d; best = i; } }
            return best;
        }

        /// <summary>Catmull-Rom uniforme por los puntos de control, muestreada en enteros (N pasos por segmento según su longitud).</summary>
        static List<PointMm> Muestrear(List<PointMm> c)
        {
            var r = new List<PointMm> { c[0] };
            for (int i = 0; i + 1 < c.Count; i++)
            {
                var p0 = c[Math.Max(0, i - 1)]; var p1 = c[i]; var p2 = c[i + 1]; var p3 = c[Math.Min(c.Count - 1, i + 2)];
                int n = Math.Max(1, IMath.Dist(p2.X - p1.X, p2.Y - p1.Y) / PasoMm);
                for (int k = 1; k <= n; k++) r.Add(new PointMm(Cr(p0.X, p1.X, p2.X, p3.X, k, n), Cr(p0.Y, p1.Y, p2.Y, p3.Y, k, n)));
            }
            return r;
        }

        /// <summary>0,5·(2P1 + (−P0+P2)t + (2P0−5P1+4P2−P3)t² + (−P0+3P1−3P2+P3)t³) con t = k/n, todo en enteros (n³ en el denominador).</summary>
        static int Cr(long p0, long p1, long p2, long p3, long k, long n)
        {
            long n3 = n * n * n;
            long v = 2 * p1 * n3 + (-p0 + p2) * k * n * n + (2 * p0 - 5 * p1 + 4 * p2 - p3) * k * k * n + (-p0 + 3 * p1 - 3 * p2 + p3) * k * k * k;
            return IMath.DivR(v, 2 * n3);
        }

        /// <summary>Radio de curva mínimo de la polilínea (mm): en cada vértice, longitud media de los dos lados ÷ ángulo girado (radianes). Recta = int.MaxValue.</summary>
        static int RadioMinimo(List<PointMm> p)
        {
            long best = int.MaxValue;
            for (int i = 1; i + 1 < p.Count; i++)
            {
                int a0 = IMath.Atan2(p[i].Y - p[i - 1].Y, p[i].X - p[i - 1].X), a1 = IMath.Atan2(p[i + 1].Y - p[i].Y, p[i + 1].X - p[i].X);
                int d = Math.Abs(IMath.Diff(a1, a0)); if (d == 0) continue;
                long l = (IMath.Dist(p[i].X - p[i - 1].X, p[i].Y - p[i - 1].Y) + IMath.Dist(p[i + 1].X - p[i].X, p[i + 1].Y - p[i].Y)) / 2;
                long rad = l * IMath.Turn * 1000 / (6283L * d);      // ángulo en bam16 → radianes (2π = 6,283)
                if (rad < best) best = rad;
            }
            return (int)Math.Min(best, int.MaxValue);
        }

        /// <summary>Punto a «off» mm del extremo A del tramo (sentido A→B), con su rumbo en bam16.</summary>
        public PointMm Posicion(int tramo, int off, out int rumbo)
        {
            var t = Tramos[tramo]; off = IMath.Clamp(off, 0, t.LongMm);
            int lo = 0, hi = t.Acum.Length - 1;
            while (hi - lo > 1) { int m = (lo + hi) / 2; if (t.Acum[m] <= off) lo = m; else hi = m; }
            var a = t.Puntos[lo]; var b = t.Puntos[hi]; int seg = t.Acum[hi] - t.Acum[lo];
            rumbo = IMath.Atan2(b.Y - a.Y, b.X - a.X);
            if (seg <= 0) return a;
            return new PointMm(a.X + IMath.DivR((long)(b.X - a.X) * (off - t.Acum[lo]), seg), a.Y + IMath.DivR((long)(b.Y - a.Y) * (off - t.Acum[lo]), seg));
        }

        public struct Paso { public int Tramo; public bool Directo; }      // Directo: se recorre de A a B

        /// <summary>Camino más corto (Dijkstra por longitud, desempate por id) entre dos nodos. null si no hay. Lista vacía si son el mismo nodo.</summary>
        public List<Paso> Ruta(int desde, int hasta)
        {
            if (desde == hasta) return new List<Paso>();
            int n = Nodos.Count; var dist = new long[n]; var prevT = new int[n]; var prevN = new int[n]; var hecho = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = long.MaxValue; prevT[i] = -1; prevN[i] = -1; }
            dist[desde] = 0;
            for (int it = 0; it < n; it++)
            {
                int u = -1; for (int i = 0; i < n; i++) if (!hecho[i] && dist[i] != long.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || u == hasta) break; hecho[u] = true;
                foreach (var t in Tramos)
                {
                    int v = t.A == u ? t.B : t.B == u ? t.A : -1; if (v < 0 || hecho[v]) continue;
                    long nd = dist[u] + t.LongMm; if (nd < dist[v]) { dist[v] = nd; prevT[v] = t.Id; prevN[v] = u; }
                }
            }
            if (dist[hasta] == long.MaxValue) return null;
            var r = new List<Paso>();
            for (int v = hasta; v != desde; v = prevN[v]) { var t = Tramos[prevT[v]]; r.Add(new Paso { Tramo = t.Id, Directo = t.B == v }); }
            r.Reverse(); return r;
        }

        public ulong Hash()
        {
            ulong h = 0x9E37UL;
            foreach (var n in Nodos) h = IMath.Mix(h ^ (ulong)(uint)n.X ^ ((ulong)(uint)n.Y << 32) ^ (ulong)(n.Estacion + 1) * 31UL);
            foreach (var t in Tramos) { h = IMath.Mix(h ^ (ulong)t.A ^ ((ulong)t.B << 20) ^ ((ulong)(uint)t.LongMm << 32)); foreach (var p in t.Puntos) h = IMath.Mix(h ^ (ulong)(uint)p.X ^ ((ulong)(uint)p.Y << 32)); }
            return h;
        }

        public void Save(SaveWriter w)
        {
            w.Int(Nodos.Count); foreach (var n in Nodos) { w.Int(n.X); w.Int(n.Y); w.Int(n.Estacion); }
            w.Int(Estaciones.Count); foreach (var e in Estaciones) { w.Int(e.Nodo); w.Str(e.Nombre); }
            w.Int(Tramos.Count); foreach (var t in Tramos) { w.Int(t.A); w.Int(t.B); w.Int(t.Puntos.Length); foreach (var p in t.Puntos) { w.Int(p.X); w.Int(p.Y); } }
        }

        public void Load(SaveReader r)
        {
            Nodos.Clear(); Estaciones.Clear(); Tramos.Clear();
            int nn = r.Int(); for (int i = 0; i < nn; i++) Nodos.Add(new Nodo { Id = i, X = r.Int(), Y = r.Int(), Estacion = r.Int() });
            int ne = r.Int(); for (int i = 0; i < ne; i++) Estaciones.Add(new Estacion { Id = i, Nodo = r.Int(), Nombre = r.Str() });
            int nt = r.Int();
            for (int i = 0; i < nt; i++)
            {
                var t = new Tramo { Id = i, A = r.Int(), B = r.Int() }; int np = r.Int(); t.Puntos = new PointMm[np]; t.Acum = new int[np];
                for (int k = 0; k < np; k++) { t.Puntos[k] = new PointMm(r.Int(), r.Int()); if (k > 0) t.Acum[k] = t.Acum[k - 1] + IMath.Dist(t.Puntos[k].X - t.Puntos[k - 1].X, t.Puntos[k].Y - t.Puntos[k - 1].Y); }
                t.LongMm = np > 0 ? t.Acum[np - 1] : 0; Tramos.Add(t);
            }
        }
    }
}
