using System;

namespace TBD.Net
{
    // Hito 3 «Unión» (GDD §7): un único terreno para el mapa de operaciones y para las zonas. El mapa lo muestrea grueso y la zona
    // fino, siempre con la MISMA función, para que el zoom continuo enlace sin saltos.
    //
    // RELIEVE DE SUSTITUCIÓN: no son datos reales. Los datos de elevación del IGN o de Copernicus quedan pendientes de que Lucía
    // revise sus licencias. Cuando estén, se escribe otra clase con la misma interfaz que lea el modelo digital de elevación y
    // no hay que tocar nada más.

    public interface IElevationSource
    {
        /// <summary>Altura en metros sobre el nivel del mar en un punto del mapa (unidades de mapa; 1 unidad ≈ 3,5 km). Negativa en el mar.</summary>
        double HeightM(double mx, double my);
    }

    public static class MapGeo
    {
        public const double KmPerUnit = 3.5;

        /// <summary>Costa aproximada (unidades de mapa, a ojo): a la derecha de esta x está el mar.</summary>
        public static double CoastX(double my)
        {
            if (my >= 84) return 53;
            if (my >= 74) return Lerp(56, 53, (my - 74) / 10);
            if (my >= 63) return Lerp(61, 56, (my - 63) / 11);
            if (my >= 48) return Lerp(72, 61, (my - 48) / 15);
            if (my >= 40) return Lerp(76, 72, (my - 40) / 8);
            return 80;
        }

        public static double Lerp(double a, double b, double t) { return a + (b - a) * t; }
        public static double Smooth(double e0, double e1, double x)
        {
            double t = Math.Max(0, Math.Min(1, (x - e0) / (e1 - e0)));
            return t * t * (3 - 2 * t);
        }

        /// <summary>Distancia (unidades de mapa) de un punto al segmento AB.</summary>
        public static double SegDist(double px, double py, double ax, double ay, double bx, double by)
        {
            double vx = bx - ax, vy = by - ay;
            double l2 = vx * vx + vy * vy;
            double t = l2 > 0 ? Math.Max(0, Math.Min(1, ((px - ax) * vx + (py - ay) * vy) / l2)) : 0;
            double dx = px - (ax + t * vx), dy = py - (ay + t * vy);
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>
    /// Relieve inventado pero plausible: costa llana, interior que sube hacia el oeste (Teruel alto), sierras con ruido fractal y
    /// valles excavados a lo largo de las vías (la red va por los valles, GDD §7). Determinista: ruido de valor con hash entero.
    /// </summary>
    public sealed class PlaceholderRelief : IElevationSource
    {
        readonly RailGraph _g;

        public PlaceholderRelief(RailGraph g) { _g = g; }

        static uint Hash(int x, int y)
        {
            uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ 0x9E3779B9u;
            h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
            return h;
        }

        static double Value(double x, double y)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            double fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            double a = Hash(x0, y0) / 4294967295.0, b = Hash(x0 + 1, y0) / 4294967295.0;
            double c = Hash(x0, y0 + 1) / 4294967295.0, d = Hash(x0 + 1, y0 + 1) / 4294967295.0;
            return MapGeo.Lerp(MapGeo.Lerp(a, b, fx), MapGeo.Lerp(c, d, fx), fy);
        }

        static double Fbm(double x, double y)
        {
            double s = 0, amp = 0.5, f = 1, norm = 0;
            for (int o = 0; o < 6; o++)          // 6 octavas: detalle hasta unos 50 m, suficiente para la zona
            {
                s += amp * Value(x * f + o * 17.3, y * f - o * 9.1);
                norm += amp;
                amp *= 0.5;
                f *= 2.03;
            }
            return s / norm;
        }

        /// <summary>Altura de la explanada de una estación: el relieve sin explanar en el propio nodo.</summary>
        public double StationHeight(RailNode n) { return RawHeight(n.XMeters, n.YMeters); }

        public double HeightM(double mx, double my) { return RawFlatten(mx, my); }

        // Explanada de las estaciones: llana en los 300 m alrededor del nodo y en suave transición hasta los 1.000 m, para que la
        // zona (llana, 128 m) encaje con el terreno del mapa al hacer zoom.
        double RawFlatten(double mx, double my)
        {
            double h = RawHeight(mx, my);
            foreach (var n in _g.Nodes)
            {
                double dx = (mx - n.XMeters) * MapGeo.KmPerUnit * 1000, dy = (my - n.YMeters) * MapGeo.KmPerUnit * 1000;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > 1000) continue;
                h = MapGeo.Lerp(StationHeight(n), h, MapGeo.Smooth(300, 1000, dist));
            }
            return h;
        }

        double RawHeight(double mx, double my)
        {
            double inland = MapGeo.CoastX(my) - mx;                  // > 0 en tierra
            if (inland < 0) return Math.Max(-400, inland * 60);      // plataforma marina

            double baseH = 60 + 900 * MapGeo.Smooth(2, 32, inland);  // llano litoral → meseta del interior
            double ridge = Fbm(mx / 7.0, my / 7.0);
            ridge = 1 - Math.Abs(2 * ridge - 1);                     // crestas
            double h = baseH + 700 * ridge * ridge * MapGeo.Smooth(3, 14, inland);

            // valle del Ebro alrededor de Zaragoza (más bajo)
            double dz = (mx - 38) * (mx - 38) + (my - 10) * (my - 10);
            h -= 650 * Math.Exp(-dz / 260.0);

            // valles a lo largo de las vías: la red va por donde el terreno lo permite
            double v = double.MaxValue;
            foreach (var e in _g.Edges)
            {
                var a = _g.Nodes[e.A]; var b = _g.Nodes[e.B];
                v = Math.Min(v, MapGeo.SegDist(mx, my, a.XMeters, a.YMeters, b.XMeters, b.YMeters));
            }
            h *= MapGeo.Lerp(0.45, 1.0, MapGeo.Smooth(0.3, 5, v));
            h += 25 * (Fbm(mx * 3, my * 3) - 0.5);                   // rugosidad fina (se nota de cerca)
            return Math.Max(2, h);
        }
    }
}
