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
        public sealed class Tramo { public int Id, A, B, LongMm; public PointMm[] Puntos; public int[] Acum; public int RumboIni; public Pieza[] Piezas; public bool Borrado; public int Coste, Pago = -1; }      // Coste: metros de material pagados al construirlo; Pago: estación que los puso (se le devuelven al borrar)      // Borrado: se queda en la lista (los ids no cambian) pero ya no cuenta

        /// <summary>D-N209 (Lucía): la vía se compone de piezas del kit de Teo (entregas/teo-modelado/hito1-red). Rectas de 10 y 2,5 m; curvas de arco 15,71 m
        /// con R 60, 120 y 240 (15°, 7,5° y 3,75°), a izquierda o derecha. Izquierda = giro antihorario en el plano X-Y del mundo.</summary>
        public enum Pieza : byte { Recta10, Recta2_5, Izq60, Der60, Izq120, Der120, Izq240, Der240 }
        public const int ArcoMm = 15_710;
        public static int LargoMm(Pieza p) { return p == Pieza.Recta10 ? 10_000 : p == Pieza.Recta2_5 ? 2_500 : ArcoMm; }
        public static int RadioMm(Pieza p) { switch (p) { case Pieza.Izq60: case Pieza.Der60: return 60_000; case Pieza.Izq120: case Pieza.Der120: return 120_000; case Pieza.Izq240: case Pieza.Der240: return 240_000; default: return 0; } }
        /// <summary>Giro de la pieza en bam16 (+ izquierda): arco/radio en radianes × 65536/2π.</summary>
        public static int GiroBam(Pieza p) { int r = RadioMm(p); if (r == 0) return 0; int g = (int)((long)ArcoMm * IMath.Turn * 1000 / (6283L * r)); return (p == Pieza.Izq60 || p == Pieza.Izq120 || p == Pieza.Izq240) ? g : -g; }

        /// <summary>Puntos de una pieza desde la pose (x, y, rumbo): la recta, su extremo; la curva, 8 puntos del arco. Deja la pose final.</summary>
        static void PuntosPieza(Pieza p, ref long x, ref long y, ref int rumbo, List<PointMm> salida)
        {
            int g = GiroBam(p);
            if (g == 0) { int l = LargoMm(p); x += (long)IMath.Cos(rumbo) * l / IMath.One; y += (long)IMath.Sin(rumbo) * l / IMath.One; if (salida != null) salida.Add(new PointMm((int)x, (int)y)); return; }
            const int sub = 8; int r = RadioMm(p);
            for (int k = 1; k <= sub; k++)
            {
                int step = g * k / sub - g * (k - 1) / sub;
                int cuerda = (int)(2L * r * Math.Abs(IMath.Sin(Math.Abs(step) / 2)) / IMath.One);      // 2R·sen(paso/2)
                int dir = (rumbo + step / 2) & 0xFFFF;
                x += (long)IMath.Cos(dir) * cuerda / IMath.One; y += (long)IMath.Sin(dir) * cuerda / IMath.One;
                rumbo = (rumbo + step) & 0xFFFF;
                if (salida != null) salida.Add(new PointMm((int)x, (int)y));
            }
        }

        /// <summary>Pose final de una pieza desde (x, y, rumbo): la que usa la vista para colocar el FBX siguiente. La misma cuenta que la simulación.</summary>
        public static void Avanzar(Pieza p, ref long x, ref long y, ref int rumbo) { PuntosPieza(p, ref x, ref y, ref rumbo, null); }

        /// <summary>D-N209: ajusta en vivo una cadena de piezas al trazo libre del jugador (nunca elige piezas). Avance voraz: en cada paso, la pieza cuyo extremo queda
        /// más cerca del punto del trazo que toca (un arco por delante), con un pequeño premio a la recta. Para al llegar al final del trazo. Determinista.</summary>
        public static List<Pieza> AjustarTrazo(IList<PointMm> trazo, int x0, int y0, int rumboIni, out int restoMm)
        {
            var r = new List<Pieza>(); restoMm = int.MaxValue;
            if (trazo == null || trazo.Count < 2) return r;
            var acum = new long[trazo.Count]; for (int i = 1; i < trazo.Count; i++) acum[i] = acum[i - 1] + IMath.Dist(trazo[i].X - trazo[i - 1].X, trazo[i].Y - trazo[i - 1].Y);
            long total = acum[acum.Length - 1]; long x = x0, y = y0; int rumbo = rumboIni & 0xFFFF; long s = 0; var fin = trazo[trazo.Count - 1]; int atasco = 0;
            int mejorN = 0; long mejorDf = IMath.Dist((int)(fin.X - x), (int)(fin.Y - y));      // el prefijo que más se acerca al final (un final libre no debe «pasarse y dar vueltas»)
            for (int it = 0; it < 600; it++)
            {
                long df = IMath.Dist((int)(fin.X - x), (int)(fin.Y - y));
                if (df < mejorDf) { mejorDf = df; mejorN = r.Count; }
                if (df <= 6_000) break;
                if (mejorDf <= EncajeMm && df > mejorDf) break;      // ya estuvo a tiro del final y se aleja: se queda con el mejor
                // persecución pura: rumbo deseado hacia un punto del trazo 40 m por delante; la pieza que más corrige sin pasarse (la recta si el error es pequeño)
                long objetivo = Math.Min(total, s + 40_000); var tp = PuntoEn(trazo, acum, objetivo);
                int deseado = IMath.Atan2((int)(tp.Y - y), (int)(tp.X - x)); int err = IMath.Diff(deseado, rumbo);
                Pieza mejor = df <= 12_000 && Math.Abs(err) < GiroBam(Pieza.Izq240) / 2 ? Pieza.Recta2_5 : Pieza.Recta10;
                if (Math.Abs(err) >= GiroBam(Pieza.Izq240) / 2)
                {
                    bool izq = err > 0; int a = Math.Abs(err);
                    if (a >= GiroBam(Pieza.Izq60)) mejor = izq ? Pieza.Izq60 : Pieza.Der60;
                    else if (a >= GiroBam(Pieza.Izq120)) mejor = izq ? Pieza.Izq120 : Pieza.Der120;
                    else mejor = izq ? Pieza.Izq240 : Pieza.Der240;
                }
                long mx = x, my = y; int mr = rumbo; PuntosPieza(mejor, ref mx, ref my, ref mr, null);
                r.Add(mejor); x = mx; y = my; rumbo = mr;
                long s0 = s; s = Proyectar(trazo, acum, x, y, s);
                if (s == s0 && ++atasco > 8) break; else if (s != s0) atasco = 0;      // no avanza por el trazo (curva imposible): se deja de buscar
            }
            long dfin = IMath.Dist((int)(fin.X - x), (int)(fin.Y - y)); if (dfin < mejorDf) { mejorDf = dfin; mejorN = r.Count; }
            if (mejorN < r.Count) r.RemoveRange(mejorN, r.Count - mejorN);
            restoMm = (int)mejorDf;
            return r;
        }

        static PointMm PuntoEn(IList<PointMm> t, long[] acum, long s)
        {
            for (int i = 1; i < t.Count; i++) if (acum[i] >= s) { long seg = acum[i] - acum[i - 1]; if (seg <= 0) return t[i]; long f = s - acum[i - 1]; return new PointMm((int)(t[i - 1].X + (t[i].X - t[i - 1].X) * f / seg), (int)(t[i - 1].Y + (t[i].Y - t[i - 1].Y) * f / seg)); }
            return t[t.Count - 1];
        }

        /// <summary>Avance a lo largo del trazo del punto más cercano a (x, y), sin retroceder de «desde».</summary>
        static long Proyectar(IList<PointMm> t, long[] acum, long x, long y, long desde)
        {
            long best = desde, bd = long.MaxValue;
            for (int i = 1; i < t.Count; i++)
            {
                long ax = t[i - 1].X, ay = t[i - 1].Y, bx = t[i].X, by = t[i].Y, dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
                long f = l2 == 0 ? 0 : Math.Max(0, Math.Min(l2, (x - ax) * dx + (y - ay) * dy));
                long qx = l2 == 0 ? ax : ax + dx * f / l2, qy = l2 == 0 ? ay : ay + dy * f / l2;
                long d = (qx - x) * (qx - x) + (qy - y) * (qy - y);
                long sq = acum[i - 1] + (l2 == 0 ? 0 : (long)IMath.Dist((int)(qx - ax), (int)(qy - ay)));
                if (sq >= desde && d < bd) { bd = d; best = sq; }
            }
            return best;
        }

        /// <summary>Construye el tramo de una cadena de piezas desde el nodo «desde» con rumbo inicial. El extremo se encaja en un nodo a menos de EncajeMm o crea uno.</summary>
        public Resultado TrazarPiezas(int desde, int rumboIni, IList<Pieza> piezas)
        {
            if (desde < 0 || desde >= Nodos.Count || piezas == null || piezas.Count == 0) return new Resultado { Por = "vía vacía" };
            var pts = new List<PointMm> { new PointMm(Nodos[desde].X, Nodos[desde].Y) }; long x = Nodos[desde].X, y = Nodos[desde].Y; int ru = rumboIni & 0xFFFF;
            foreach (var pz in piezas) PuntosPieza(pz, ref x, ref y, ref ru, pts);
            int nb = NodoCerca((int)x, (int)y);
            if (nb == desde) return new Resultado { Por = "la vía empieza y acaba en el mismo sitio" };
            if (nb >= 0) pts[pts.Count - 1] = new PointMm(Nodos[nb].X, Nodos[nb].Y);      // el último palmo encaja en el nodo (menos de EncajeMm)
            var acum = new int[pts.Count]; for (int i = 1; i < pts.Count; i++) acum[i] = acum[i - 1] + IMath.Dist(pts[i].X - pts[i - 1].X, pts[i].Y - pts[i - 1].Y);
            if (acum[acum.Length - 1] < LongMinMm) return new Resultado { Por = "vía demasiado corta" };
            foreach (var q in pts) if (EnMar(q)) return new Resultado { Por = $"la vía no puede ir por el mar ({q.X / 1000}; {q.Y / 1000})" };
            var cruces = Cruces(pts, acum, out string sesgo); if (sesgo != null) return new Resultado { Por = sesgo };
            int coste = 0, pago = -1;
            if (Pagar != null) { coste = CosteMetros(piezas, cruces.Count); string no = Pagar(desde, coste, out pago); if (no != null) return new Resultado { Por = no }; if (pago < 0) coste = 0; }      // lo último antes de construir: si paga, se construye
            if (nb < 0) { nb = Nodos.Count; Nodos.Add(new Nodo { Id = nb, X = (int)x, Y = (int)y }); }
            foreach (var c in cruces) { var cc = c; cc.Tramo = Tramos.Count; PasosNivel.Add(cc); }
            var t = new Tramo { Id = Tramos.Count, A = desde, B = nb, Puntos = pts.ToArray(), Acum = acum, LongMm = acum[acum.Length - 1], RumboIni = rumboIni & 0xFFFF, Piezas = new List<Pieza>(piezas).ToArray(), Coste = coste, Pago = pago };
            Tramos.Add(t);
            int rmin = int.MaxValue; foreach (var pz in piezas) { int rr = RadioMm(pz); if (rr > 0 && rr < rmin) rmin = rr; }
            return new Resultado { Ok = true, Tramo = t.Id, RadioMinMm = rmin };
        }

        /// <summary>Cruces de la polilínea de vía con las carreteras: punto, distancia a lo largo de la vía y ángulo (0–90°, encajado a 90/60/45/30 para la pieza).
        /// Si alguno baja de CruceMinGrados, «sesgo» lleva la causa.</summary>
        List<PasoNivel> Cruces(List<PointMm> pts, int[] acum, out string sesgo)
        {
            sesgo = null; var r = new List<PasoNivel>();
            for (int c = 0; c < Carreteras.Count; c++)
            {
                var rd = Carreteras[c];
                for (int i = 1; i < pts.Count; i++) for (int k = 1; k < rd.Length; k++)
                {
                    if (!Corta(pts[i - 1], pts[i], rd[k - 1], rd[k], out long fNum, out long fDen)) continue;
                    int av = IMath.Atan2(pts[i].Y - pts[i - 1].Y, pts[i].X - pts[i - 1].X), ac = IMath.Atan2(rd[k].Y - rd[k - 1].Y, rd[k].X - rd[k - 1].X);
                    int d = Math.Abs(IMath.Diff(av, ac)); if (d > IMath.Turn / 4) d = IMath.Turn / 2 - d;      // 0..90° en bam16
                    int grados = (int)((long)d * 360 / IMath.Turn);
                    if (grados < CruceMinGrados) { sesgo = $"Cruce demasiado sesgado ({grados}°; mínimo {CruceMinGrados}°)"; return r; }
                    int pieza = grados >= 75 ? 90 : grados >= 52 ? 60 : grados >= 37 ? 45 : 30;
                    int seg = acum[i] - acum[i - 1]; int off = acum[i - 1] + (int)(seg * fNum / Math.Max(1, fDen));
                    var p = new PointMm(pts[i - 1].X + (int)((pts[i].X - pts[i - 1].X) * fNum / Math.Max(1, fDen)), pts[i - 1].Y + (int)((pts[i].Y - pts[i - 1].Y) * fNum / Math.Max(1, fDen)));
                    r.Add(new PasoNivel { OffMm = off, AnguloGrados = pieza, Carretera = c, P = p });
                }
            }
            return r;
        }

        /// <summary>¿Se cortan los segmentos p1p2 y q1q2? Devuelve la fracción a lo largo de p1p2 como fNum/fDen (enteros).</summary>
        static bool Corta(PointMm p1, PointMm p2, PointMm q1, PointMm q2, out long fNum, out long fDen)
        {
            long rx = p2.X - p1.X, ry = p2.Y - p1.Y, sx = q2.X - q1.X, sy = q2.Y - q1.Y;
            long den = rx * sy - ry * sx; fNum = 0; fDen = 1; if (den == 0) return false;
            long qpx = q1.X - p1.X, qpy = q1.Y - p1.Y;
            long t = qpx * sy - qpy * sx, u = qpx * ry - qpy * rx;
            if (den < 0) { den = -den; t = -t; u = -u; }
            if (t < 0 || t > den || u < 0 || u > den) return false;
            fNum = t; fDen = den; return true;
        }

        /// <summary>D-N209: el trazo libre del jugador → cadena de piezas → tramo. Empieza en el nodo más cercano al primer punto (a menos de EncajeMm), con el rumbo
        /// hacia el trazo (o el de la vía que ya llega a ese nodo, para seguirla sin quiebro). Se rechaza si la cadena no llega a 12 m del final del trazo.</summary>
        public Resultado TrazarLibre(IList<PointMm> trazo)
        {
            if (trazo == null || trazo.Count < 2) return new Resultado { Por = "hacen falta al menos dos puntos" };
            int na = NodoCerca(trazo[0].X, trazo[0].Y); if (na < 0) return new Resultado { Por = "la vía tiene que empezar en una estación o en otra vía" };
            // Marcos (Mundo21): soltar a ~20 m de B dejaba la vía suelta. Si el final cae a menos de 40 m de una estación o vía (y no a 12), se apunta a ella;
            // y si aun así la cadena no encaja, se rechaza diciendo a cuánto se queda, en vez de crear un extremo suelto junto a la estación.
            var fin = trazo[trazo.Count - 1]; int objetivo = NodoCerca(fin.X, fin.Y);
            if (objetivo < 0) { int nc = NodoCerca(fin.X, fin.Y, AyudaEncajeMm); if (nc >= 0 && nc != na) { objetivo = nc; var t2 = new List<PointMm>(trazo); t2.Add(new PointMm(Nodos[nc].X, Nodos[nc].Y)); trazo = t2; } }
            int ru = RumboSalida(na, trazo);
            var piezas = AjustarTrazo(trazo, Nodos[na].X, Nodos[na].Y, ru, out int resto);
            if (objetivo >= 0 && objetivo != na)
            {
                long x = Nodos[na].X, y = Nodos[na].Y; int r = ru; foreach (var pz in piezas) Avanzar(pz, ref x, ref y, ref r);
                int d = IMath.Dist((int)(Nodos[objetivo].X - x), (int)(Nodos[objetivo].Y - y));
                if (d > EncajeMm) return new Resultado { Por = $"no llega a {NombreNodo(objetivo)}: se queda a {d / 1000} m (tiene que acabar a menos de {EncajeMm / 1000} m)" };
            }
            if (resto > EncajeMm) return new Resultado { Por = $"curva demasiado cerrada: el radio mínimo es {RadioMinMm / 1000} m" };
            return TrazarPiezas(na, ru, piezas);
        }

        int RumboSalida(int nodo, IList<PointMm> trazo)
        {
            foreach (var t in Tramos)      // seguir sin quiebro la vía que ya acaba en este nodo
            {
                if (t.Borrado) continue;
                if (t.B == nodo && t.Puntos.Length >= 2) { var a = t.Puntos[t.Puntos.Length - 2]; var b = t.Puntos[t.Puntos.Length - 1]; return IMath.Atan2(b.Y - a.Y, b.X - a.X); }
                if (t.A == nodo && t.Puntos.Length >= 2) { var a = t.Puntos[1]; var b = t.Puntos[0]; return IMath.Atan2(b.Y - a.Y, b.X - a.X); }
            }
            var n = Nodos[nodo]; PointMm q = trazo[trazo.Count - 1];
            for (int i = 1; i < trazo.Count; i++) if (IMath.Dist(trazo[i].X - n.X, trazo[i].Y - n.Y) >= 20_000) { q = trazo[i]; break; }
            return IMath.Atan2(q.Y - n.Y, q.X - n.X);
        }
        public sealed class Estacion { public int Id, Nodo; public string Nombre; }

        /// <summary>Muestreo de la curva (mm). Radio mínimo de curva (mm): por debajo, el trazado se rechaza. Encaje (mm): un extremo a menos de esto se une al nodo existente.</summary>
        public const int PasoMm = 2_000, RadioMinMm = 60_000, EncajeMm = 12_000, LongMinMm = 10_000;

        public readonly List<Nodo> Nodos = new List<Nodo>();
        public readonly List<Tramo> Tramos = new List<Tramo>();
        public readonly List<Estacion> Estaciones = new List<Estacion>();
        /// <summary>Carreteras del terreno (Álvaro), como polilíneas en mm. La vía que las cruza lleva un paso a nivel (pieza de Teo, SM_PasoNivel_90/60/45/30).</summary>
        public readonly List<PointMm[]> Carreteras = new List<PointMm[]>();
        /// <summary>Carga carreteras_hito1.csv de Álvaro (carretera;orden;x_mm;z_mm;y_mm;ancho_mm;puente). Los tramos con puente=si no cortan la vía (pasa por debajo),
        /// así que cada carretera se parte en trozos sin puente. Z del terreno = Y de la red. Devuelve cuántas polilíneas añade.</summary>
        /// <summary>Mar (Hito 1): por cada metro de Z, la X en mm desde la que hacia el este es mar (agua_hito1.csv, de usos_2049.png de Álvaro). Vacío = sin mar.</summary>
        public int[] MarDesdeMm = new int[0];
        /// <summary>Hugo (cifras-hito1): coste de una vía en METROS de material (enteros; 1 palé = 100 m): la recta, su largo; la curva, 1,5 veces su arco; cada paso a nivel, 200 m.</summary>
        public static int CosteMetros(IList<Pieza> piezas, int pasos)
        {
            long mm = 0; foreach (var p in piezas) mm += RadioMm(p) > 0 ? (long)LargoMm(p) * 3 / 2 : LargoMm(p);
            return (int)((mm + 999) / 1000) + 200 * pasos;
        }
        /// <summary>Cobro del material (lo pone la Red): (nodo de salida, coste en metros) → null si paga (y quién paga), o el motivo del rechazo.</summary>
        public delegate string Cobro(int desde, int costeM, out int estacion);
        public Cobro Pagar;
        public bool EnMar(PointMm p) { if (MarDesdeMm == null || MarDesdeMm.Length == 0) return false; int z = p.Y / 1000; if (z < 0) z = 0; if (z >= MarDesdeMm.Length) z = MarDesdeMm.Length - 1; return p.X >= MarDesdeMm[z]; }
        public void CargarMarCsv(string csv)
        {
            var l = new List<int>();
            foreach (var raw in csv.Split('\n')) { var s = raw.Trim(); if (s.Length == 0 || s[0] == '#' || s.StartsWith("z_m")) continue; var c = s.Split(';'); if (c.Length >= 2 && int.TryParse(c[1], out int v)) l.Add(v); }
            MarDesdeMm = l.ToArray();
        }

        public int CargarCarreterasCsv(string csv)
        {
            var porNombre = new SortedDictionary<string, SortedDictionary<int, (int x, int y, bool puente)>>(System.StringComparer.Ordinal);
            foreach (var linea in csv.Split('\n'))
            {
                var c = linea.Trim().Split(';'); if (c.Length < 7 || c[0] == "carretera") continue;
                if (!int.TryParse(c[1], out int ord) || !int.TryParse(c[2], out int x) || !int.TryParse(c[3], out int z)) continue;
                if (!porNombre.TryGetValue(c[0], out var d)) porNombre[c[0]] = d = new SortedDictionary<int, (int, int, bool)>();
                d[ord] = (x, z, c[6].Trim() == "si");
            }
            int n = 0;
            foreach (var kv in porNombre)
            {
                var trozo = new List<PointMm>();
                foreach (var q in kv.Value.Values)
                {
                    if (q.puente) { if (trozo.Count >= 2) { Carreteras.Add(trozo.ToArray()); n++; } trozo.Clear(); continue; }
                    trozo.Add(new PointMm(q.x, q.y));
                }
                if (trozo.Count >= 2) { Carreteras.Add(trozo.ToArray()); n++; }
            }
            return n;
        }

        public struct PasoNivel { public int Tramo, OffMm, AnguloGrados, Carretera; public PointMm P; }
        public readonly List<PasoNivel> PasosNivel = new List<PasoNivel>();
        public const int CruceMinGrados = 30;      // Lucía (D-N209): menos de 30° es «Cruce demasiado sesgado»

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

        /// <summary>Sara (Mundo25): una vía que acaba suelta (su extremo B no es estación ni lo toca otro tramo) a menos de AvisoSueltaMm de una estación:
        /// «acaba suelta: no llega a la estación B (a 53 m)». No la rechaza (un extremo libre es válido); la vista previa lo avisa. null si no hay nada que avisar.</summary>
        public const int AvisoSueltaMm = 100_000;
        public string AvisoFinal(int tramo)
        {
            if (tramo < 0 || tramo >= Tramos.Count) return null; var t = Tramos[tramo]; int n = t.B;
            foreach (var e in Estaciones) if (e.Nodo == n) return null;
            foreach (var o in Tramos) if (o.Id != tramo && !o.Borrado && (o.A == n || o.B == n)) return null;
            string mejor = null; long bd = (long)AvisoSueltaMm * AvisoSueltaMm; int dm = 0;
            foreach (var e in Estaciones) { var q = Nodos[e.Nodo]; long dx = q.X - Nodos[n].X, dy = q.Y - Nodos[n].Y, d = dx * dx + dy * dy; if (d < bd) { bd = d; mejor = e.Nombre; dm = IMath.Dist((int)dx, (int)dy); } }
            return mejor == null ? null : $"Acaba suelta: no llega a {mejor} (a {dm / 1000} m)";      // D-N211 b
        }
        public const int AyudaEncajeMm = 40_000;      // a menos de esto del final, el trazo se lleva hasta la estación o vía
        string NombreNodo(int n) { foreach (var e in Estaciones) if (e.Nodo == n) return "la estación " + e.Nombre; return "la vía"; }
        public int NodoCerca(int x, int y) { return NodoCerca(x, y, EncajeMm); }
        public int NodoCerca(int x, int y, int radioMm)
        {
            int best = -1; long bd = (long)radioMm * radioMm;
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

        /// <summary>Borra un tramo construido: deja de existir para las rutas y se quitan sus pasos a nivel. El id se conserva (las rutas y los pasos guardan ids).</summary>
        public bool BorrarTramo(int id) { if (id < 0 || id >= Tramos.Count || Tramos[id].Borrado) return false; Tramos[id].Borrado = true; PasosNivel.RemoveAll(p => p.Tramo == id); return true; }

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
                    if (t.Borrado) continue;
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
            foreach (var t in Tramos) { h = IMath.Mix(h ^ (ulong)t.A ^ ((ulong)t.B << 20) ^ ((ulong)(uint)t.LongMm << 32)); h = IMath.Mix(h ^ (ulong)(uint)t.RumboIni); if (t.Piezas != null) foreach (var pz in t.Piezas) h = IMath.Mix(h ^ (ulong)pz); foreach (var p in t.Puntos) h = IMath.Mix(h ^ (ulong)(uint)p.X ^ ((ulong)(uint)p.Y << 32)); if (t.Borrado) h = IMath.Mix(h ^ 0xB0B0UL); if (t.Coste > 0) h = IMath.Mix(h ^ (ulong)(uint)t.Coste ^ ((ulong)(uint)(t.Pago + 1) << 32)); }
            foreach (var pn in PasosNivel) h = IMath.Mix(h ^ (ulong)pn.Tramo ^ ((ulong)(uint)pn.OffMm << 16) ^ ((ulong)pn.AnguloGrados << 48));
            return h;
        }

        public void Save(SaveWriter w)
        {
            w.Int(Nodos.Count); foreach (var n in Nodos) { w.Int(n.X); w.Int(n.Y); w.Int(n.Estacion); }
            w.Int(Estaciones.Count); foreach (var e in Estaciones) { w.Int(e.Nodo); w.Str(e.Nombre); }
            w.Int(Carreteras.Count); foreach (var rd in Carreteras) { w.Int(rd.Length); foreach (var q in rd) { w.Int(q.X); w.Int(q.Y); } }
            w.Ints(MarDesdeMm);
            w.Int(PasosNivel.Count); foreach (var pn in PasosNivel) { w.Int(pn.Tramo); w.Int(pn.OffMm); w.Int(pn.AnguloGrados); w.Int(pn.Carretera); w.Int(pn.P.X); w.Int(pn.P.Y); }
            w.Int(Tramos.Count); foreach (var t in Tramos) { w.Int(t.A); w.Int(t.B); w.Int(t.RumboIni); w.Int(t.Piezas != null ? t.Piezas.Length : 0); if (t.Piezas != null) foreach (var pz in t.Piezas) w.Byte((byte)pz); w.Int(t.Puntos.Length); foreach (var p in t.Puntos) { w.Int(p.X); w.Int(p.Y); } }
            int nb = 0; foreach (var t in Tramos) if (t.Borrado) nb++; w.Int(nb); foreach (var t in Tramos) if (t.Borrado) w.Int(t.Id);
            foreach (var t in Tramos) { w.Int(t.Coste); w.Int(t.Pago); }
        }

        public void Load(SaveReader r)
        {
            Nodos.Clear(); Estaciones.Clear(); Tramos.Clear(); Carreteras.Clear(); PasosNivel.Clear();
            int nn = r.Int(); for (int i = 0; i < nn; i++) Nodos.Add(new Nodo { Id = i, X = r.Int(), Y = r.Int(), Estacion = r.Int() });
            int ne = r.Int(); for (int i = 0; i < ne; i++) Estaciones.Add(new Estacion { Id = i, Nodo = r.Int(), Nombre = r.Str() });
            Carreteras.Clear(); int nc = r.Int(); for (int i = 0; i < nc; i++) { int nq = r.Int(); var rd = new PointMm[nq]; for (int k = 0; k < nq; k++) rd[k] = new PointMm(r.Int(), r.Int()); Carreteras.Add(rd); }
            MarDesdeMm = r.Ints() ?? new int[0];
            PasosNivel.Clear(); int npn = r.Int(); for (int i = 0; i < npn; i++) PasosNivel.Add(new PasoNivel { Tramo = r.Int(), OffMm = r.Int(), AnguloGrados = r.Int(), Carretera = r.Int(), P = new PointMm(r.Int(), r.Int()) });
            int nt = r.Int();
            for (int i = 0; i < nt; i++)
            {
                var t = new Tramo { Id = i, A = r.Int(), B = r.Int(), RumboIni = r.Int() }; int npz = r.Int(); t.Piezas = new Pieza[npz]; for (int k = 0; k < npz; k++) t.Piezas[k] = (Pieza)r.Byte(); int np = r.Int(); t.Puntos = new PointMm[np]; t.Acum = new int[np];
                for (int k = 0; k < np; k++) { t.Puntos[k] = new PointMm(r.Int(), r.Int()); if (k > 0) t.Acum[k] = t.Acum[k - 1] + IMath.Dist(t.Puntos[k].X - t.Puntos[k - 1].X, t.Puntos[k].Y - t.Puntos[k - 1].Y); }
                t.LongMm = np > 0 ? t.Acum[np - 1] : 0; Tramos.Add(t);
            }
            int nb = r.Int(); for (int i = 0; i < nb; i++) Tramos[r.Int()].Borrado = true;
            foreach (var t in Tramos) { t.Coste = r.Int(); t.Pago = r.Int(); }
        }
    }
}
