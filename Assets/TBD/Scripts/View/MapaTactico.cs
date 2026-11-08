using System;
using TBD.Net;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Plano táctico oscuro para el mapa de operaciones (Bruno, 8-nov; estilo de la D-N113, aprobada el 7-nov). Sustituye a Hypso, el
    /// degradado beige de altura que de cerca (2,5 km) se veía como «un plano beige con dos líneas» (Teo, captura 4_nules_descargar).
    /// Solo vista: no toca la simulación ni el hash. Pinta, por téxel y en la paleta de 32:
    ///   - el suelo por bandas de altura, oscuro: 303424 (llano), 48403c, 605c48 y 6c7874 (cumbres), con fundido tramado (Bayer 4x4);
    ///   - el relieve con sombreado de ladera cocido (luz del noroeste: la ladera al sol sube un escalón) y curvas de nivel
    ///     (de cerca, cada 50 m, y cada 250 m más marcada; de lejos, cada 200 m);
    ///   - el mar en navy 181c30 con la línea de costa en 94948c;
    ///   - los pueblos alrededor de cada nodo: manzanas en 605c48 y calles en 303424, con el borde irregular;
    ///   - la huerta del llano litoral (solo de cerca): parcelas con lindes en 50541c;
    ///   - las carreteras (PROTOTIPO, como el relieve: no son datos reales), paralelas a la vía, en 6c7874.
    /// El terreno no usa colores de la horda (1c200c, 442c0c ni la banda L* 9–13) ni ámbar: 1c200c es solo la mancha de horda,
    /// y el rojo 982420, solo donde la horda ataca un nodo o un tren (Lucía, 8-nov).
    /// Rendimiento: de cerca, las alturas se muestrean cada 2 téxeles y se interpolan; tramos y nodos se filtran por el parche.
    /// </summary>
    public static class MapaTactico
    {
        static readonly Color32 Navy = Hex(0x181c30), Sombra = Hex(0x303424), GrisPardo = Hex(0x48403c), OlivaGris = Hex(0x605c48),
            Hormigon = Hex(0x6c7874), HormigonClaro = Hex(0x94948c), OlivaOscuro = Hex(0x50541c), OlivaClaro = Hex(0x88885c),
            Rojo = Hex(0x982420), Vino = Hex(0x642030);
        // Relleno solo en tres valores oscuros (Iker, 8-nov: de lejos, 6c7874 y 94948c salían muy claros en la meseta). El 6c7874 queda
        // para las curvas maestras; el 94948c, para la costa.
        static readonly Color32[] Bandas = { Sombra, GrisPardo, OlivaGris };
        static readonly double[] Cortes = { 200, 800 };                               // m: límites entre bandas
        static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

        static Color32 Hex(int h) { return new Color32((byte)(h >> 16), (byte)(h >> 8), (byte)h, 255); }
        static float Hash(int x, int y, int s) { uint h = (uint)(x * 374761393 + y * 668265263 + s * 1274126177); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xffff) / 65535f; }

        /// <summary>Radio del casco urbano (m) por nombre de nodo. PROTOTIPO, a ojo (Valencia y Zaragoza grandes, apeaderos pequeños).</summary>
        static double RadioPueblo(string n)
        {
            switch (n)
            {
                case "Valencia": case "Zaragoza": return 2600;
                case "Castellón": return 1800;
                case "Sagunto": return 1400;
                case "Teruel": return 1100;
                case "Segorbe": case "Nules": case "Puçol": return 750;
                default: return 550;
            }
        }

        /// <summary>
        /// Pinta w x h téxeles. El téxel (x, y) está en el punto de mapa (mx0 + x·dmx, my0 + y·dmy), en unidades de mapa (3,5 km).
        /// detalle = true para el parche de cerca (curvas cada 50 m, huerta y manzanas); false para el terreno grueso.
        /// </summary>
        public static Color32[] Pintar(IElevationSource rel, RailGraph g, double mx0, double dmx, double my0, double dmy, int w, int h, bool detalle)
        {
            // Alturas: de cerca se muestrean cada 2 téxeles y se interpolan (el relieve es suave a esa escala): 4 veces menos HeightM.
            int s = detalle ? 2 : 1, gw = (w + 1) / s + 3, gh = (h + 1) / s + 3;
            var G = new double[gw * gh];
            for (int j = 0; j < gh; j++)
                for (int i = 0; i < gw; i++)
                    G[j * gw + i] = rel.HeightM(mx0 + (i * s - 1) * dmx, my0 + (j * s - 1) * dmy);
            var H = new double[(w + 2) * (h + 2)];
            for (int y = -1; y <= h; y++)
                for (int x = -1; x <= w; x++)
                {
                    int i0 = (x + 1) / s, j0 = (y + 1) / s; double fx = ((x + 1) - i0 * s) / (double)s, fy = ((y + 1) - j0 * s) / (double)s;
                    double a = G[j0 * gw + i0], b = G[j0 * gw + i0 + 1], c = G[(j0 + 1) * gw + i0], d = G[(j0 + 1) * gw + i0 + 1];
                    H[(y + 1) * (w + 2) + (x + 1)] = (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
                }
            // Solo los tramos y los nodos que tocan el parche (de cerca, casi siempre uno o dos de cada).
            double xMin = Math.Min(mx0, mx0 + (w - 1) * dmx), xMax = Math.Max(mx0, mx0 + (w - 1) * dmx);
            double yMin = Math.Min(my0, my0 + (h - 1) * dmy), yMax = Math.Max(my0, my0 + (h - 1) * dmy);
            var tramos = new System.Collections.Generic.List<RailEdge>(); var nodos = new System.Collections.Generic.List<RailNode>();
            foreach (var e in g.Edges)
            {
                var A = g.Nodes[e.A]; var B = g.Nodes[e.B]; double m = 0.5;                      // 0,5 u = 1,75 km de margen (desvío y curva)
                if (Math.Max(A.XMeters, B.XMeters) + m < xMin || Math.Min(A.XMeters, B.XMeters) - m > xMax || Math.Max(A.YMeters, B.YMeters) + m < yMin || Math.Min(A.YMeters, B.YMeters) - m > yMax) continue;
                tramos.Add(e);
            }
            foreach (var n in g.Nodes)
            {
                double m = RadioPueblo(n.Name) * 1.25 / (MapGeo.KmPerUnit * 1000);
                if (n.XMeters + m < xMin || n.XMeters - m > xMax || n.YMeters + m < yMin || n.YMeters - m > yMax) continue;
                nodos.Add(n);
            }
            double mTx = Math.Abs(dmx) * MapGeo.KmPerUnit * 1000.0, mTy = Math.Abs(dmy) * MapGeo.KmPerUnit * 1000.0;   // metros por téxel
            double paso = detalle ? 50 : 200, pasoFuerte = detalle ? 250 : 1000;
            var o = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double mx = mx0 + x * dmx, my = my0 + y * dmy;
                    int i = (y + 1) * (w + 2) + (x + 1);
                    double hc = H[i], hW = H[i - 1], hE = H[i + 1], hN = H[i - (w + 2)], hS = H[i + (w + 2)];
                    int b = Bayer[y & 3, x & 3];
                    if (hc < 0)
                    {
                        bool costa = hW >= 0 || hE >= 0 || hN >= 0 || hS >= 0;
                        o[y * w + x] = costa ? HormigonClaro : Navy;
                        continue;
                    }
                    // banda de altura con fundido tramado en el último 20 % de cada banda
                    int k = 0; while (k < Cortes.Length && hc >= Cortes[k]) k++;
                    if (k < Cortes.Length)
                    {
                        double lo = k == 0 ? 0 : Cortes[k - 1], f = (hc - lo) / (Cortes[k] - lo);
                        if (f > 0.8 && (f - 0.8) / 0.2 * 16 > b) k++;
                    }
                    // sombreado cocido: luz del noroeste (en el mapa, y crece hacia el sur). La ladera al sol sube un escalón, tramado.
                    double gx = (hE - hW) / (2 * mTx), gy = (hS - hN) / (2 * mTy);
                    double sol = 0.7 * (gx + gy);                         // normal·luz: > 0, la ladera mira al noroeste
                    double pend = Math.Sqrt(gx * gx + gy * gy);
                    if (sol > 0.04 && Math.Min(1.0, sol / 0.25) * 16 > b) k++;
                    var c = Bandas[Math.Min(k, Bandas.Length - 1)];
                    // curvas de nivel
                    if (Math.Floor(hc / paso) != Math.Floor(hE / paso) || Math.Floor(hc / paso) != Math.Floor(hS / paso))
                    {
                        bool fuerte = Math.Floor(hc / pasoFuerte) != Math.Floor(hE / pasoFuerte) || Math.Floor(hc / pasoFuerte) != Math.Floor(hS / pasoFuerte);
                        if (fuerte || pend < 0.5) c = fuerte ? Hormigon : Bandas[Math.Min(k + 1, Bandas.Length - 1)];
                    }
                    double pxm = mx * MapGeo.KmPerUnit * 1000, pym = my * MapGeo.KmPerUnit * 1000;    // metros
                    // huerta del llano litoral (de cerca): parcelas de 220–480 x 260 m con lindes oscuras
                    if (detalle && hc < 150 && MapGeo.CoastX(my) - mx < 8)
                    {
                        int fila = (int)Math.Floor(pym / 260.0);
                        double ancho = 220 + 260 * Hash(fila, 0, 71);
                        double u = pxm + fila * 37.0;
                        bool linde = (u % ancho + ancho) % ancho < mTx * 0.9 || (pym % 260.0 + 260.0) % 260.0 < mTy * 0.9;
                        if (linde && b < 5) c = OlivaOscuro;                                          // lindes tramadas al 30 %: textura, no rejilla
                        else if (!linde && Hash((int)Math.Floor(u / ancho), fila, 72) > 0.9) c = GrisPardo;   // alguna parcela en barbecho
                    }
                    // carreteras (PROTOTIPO): una por tramo de vía, a 600 m hacia el mar y con una curva suave
                    double dCar = double.MaxValue;
                    foreach (var e in tramos)
                    {
                        var A = g.Nodes[e.A]; var B = g.Nodes[e.B];
                        double ax = A.XMeters + 600 / 3500.0, bx = B.XMeters + 600 / 3500.0;
                        double t = Math.Max(0, Math.Min(1, ((mx - ax) * (bx - ax) + (my - A.YMeters) * (B.YMeters - A.YMeters)) / Math.Max(1e-9, (bx - ax) * (bx - ax) + (B.YMeters - A.YMeters) * (B.YMeters - A.YMeters))));
                        double L = Math.Sqrt(Math.Max(1e-9, (bx - ax) * (bx - ax) + (B.YMeters - A.YMeters) * (B.YMeters - A.YMeters)));
                        double nx = -(B.YMeters - A.YMeters) / L, ny = (bx - ax) / L;                       // normal del tramo
                        double along = ((mx - ax) * (bx - ax) + (my - A.YMeters) * (B.YMeters - A.YMeters)) / L;
                        if (along < -0.05 || along > L + 0.05) continue;
                        double sd = (mx - ax) * nx + (my - A.YMeters) * ny;                                    // distancia con signo a la recta
                        dCar = Math.Min(dCar, Math.Abs(sd - 0.12 * Math.Sin(t * Math.PI * 3 + e.Id)) * MapGeo.KmPerUnit * 1000);   // curva suave hacia los lados
                    }
                    if (dCar < Math.Max(mTx, 18) * 0.8) c = Hormigon;
                    // pueblos: casco con borde irregular, manzanas de 70–110 m y calles de un téxel
                    foreach (var n in nodos)
                    {
                        double dx = (mx - n.XMeters) * MapGeo.KmPerUnit * 1000, dy = (my - n.YMeters) * MapGeo.KmPerUnit * 1000;
                        double r = RadioPueblo(n.Name), d = Math.Sqrt(dx * dx + dy * dy);
                        if (d > r * 1.25) continue;
                        double ang = Math.Atan2(dy, dx);
                        double borde = r * (0.85 + 0.25 * Math.Sin(ang * 3 + n.Id) + 0.12 * Math.Sin(ang * 7 + n.Id * 2));
                        if (d > borde) continue;
                        if (!detalle) { c = OlivaGris; break; }
                        double man = 70 + 40 * Hash(n.Id, 1, 73);
                        bool calle = (dx % man + man) % man < mTx * 0.9 || (dy % man + man) % man < mTy * 0.9;
                        c = calle ? Sombra : (Hash((int)Math.Floor(dx / man), (int)Math.Floor(dy / man), 74 + n.Id) > 0.12 ? OlivaGris : GrisPardo);
                        if (d > borde - Math.Max(mTx, 1)) c = OlivaClaro;     // filo del casco, para que el pueblo se lea sobre la huerta
                        break;
                    }
                    o[y * w + x] = c;
                }
            return o;
        }

        static readonly Color32 Horda = Hex(0x1c200c);

        /// <summary>
        /// La mancha de horda encima (sustituye al Lerp hacia 150,22,26). Lucía (8-nov), como el minimapa de la D-N147: la mancha va
        /// OSCURA y tramada, en el valor de la horda 1c200c; solo pasa a rojo 982420 donde ataca un nodo o un tren (ataque = true).
        /// Si siempre fuera roja, el rojo dejaría de avisar. dens en 0..1; punto = de cerca, uno de los puntos de horda.
        /// </summary>
        public static Color32 Mancha(Color32 baseC, float dens, int x, int y, bool punto, bool ataque)
        {
            float a = Mathf.Clamp01((dens - 0.08f) * 1.6f) * 0.85f;
            if (a <= 0f) return baseC;
            if (punto) return ataque ? Rojo : Horda;
            return a * 16f > Bayer[y & 3, x & 3] ? (ataque ? Rojo : Horda) : baseC;
        }

        /// <summary>
        /// ¿Está este punto del mapa en un ataque? Sí, si está a menos del casco + 1,5 km de un nodo asediado, o a menos de 2 km de un
        /// tren atacado (trenesAtacados en unidades de mapa; lo rellena la vista, ver LEEME). Se llama por téxel: es barato.
        /// </summary>
        public static bool EnAtaque(RailGraph g, double mx, double my, System.Collections.Generic.List<Vector2> trenesAtacados)
        {
            foreach (var n in g.Nodes)
            {
                if (n.State != NodeState.Asediada) continue;
                double dx = (mx - n.XMeters) * MapGeo.KmPerUnit * 1000, dy = (my - n.YMeters) * MapGeo.KmPerUnit * 1000, r = RadioPueblo(n.Name) + 1500;
                if (dx * dx + dy * dy < r * r) return true;
            }
            if (trenesAtacados != null)
                foreach (var t in trenesAtacados)
                {
                    double dx = (mx - t.x) * MapGeo.KmPerUnit * 1000, dy = (my - t.y) * MapGeo.KmPerUnit * 1000;
                    if (dx * dx + dy * dy < 2000.0 * 2000.0) return true;
                }
            return false;
        }
    }
}
