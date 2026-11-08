using System.Collections.Generic;
using TBD.Units;

namespace TBD.Tutorial
{
    /// <summary>
    /// Para la tarjeta suelta del C90 (D-N163): ¿hay algún pelotón de fusileros con C90 que tenga a tiro un grupo denso de zombis?
    /// «A tiro» es lo mismo que pide UnitsWorld.OrderC90 (alcance mínimo y máximo del C90 desde un soldado vivo que no sea servidor).
    /// «Denso»: al menos MinGrupo zombis en una celda de 2 × radio del C90 (5 m → celdas de 10 m). Es una cuenta por celdas, lineal en zombis.
    /// Solo lee: no toca la simulación. Devuelve el id del pelotón o -1.
    /// </summary>
    public static class C90Aviso
    {
        public const int MinGrupo = 12;   // PROTOTIPO: sin medir; Hugo puede cambiarlo

        public static int Buscar(UnitsWorld uw, IList<Target> zombis, int minGrupo = MinGrupo)
        {
            if (uw == null || zombis == null || zombis.Count < minGrupo) return -1;
            var w = WeaponCatalog.Table[(int)WeaponId.C90];
            int cell = System.Math.Max(1000, w.RadiusMm * 2);
            var cuenta = new Dictionary<long, int>();
            for (int i = 0; i < zombis.Count; i++)
            {
                long k = Clave(Div(zombis[i].X, cell), Div(zombis[i].Y, cell));
                cuenta.TryGetValue(k, out int n); cuenta[k] = n + 1;
            }
            long r2 = (long)w.RangeMm * w.RangeMm, min2 = (long)w.MinRangeMm * w.MinRangeMm;
            foreach (var p in uw.Platoons)
            {
                if (p.C90Left <= 0 || p.Aboard != 0 || p.AliveCount() == 0) continue;
                foreach (var kv in cuenta)
                {
                    if (kv.Value < minGrupo) continue;
                    long cx = (kv.Key >> 32) * cell + cell / 2, cy = (int)(kv.Key & 0xffffffff) * (long)cell + cell / 2;
                    foreach (var m in p.Men)
                    {
                        if (!m.Alive || m.Kind == InfKind.Servidor) continue;
                        long ex = cx - m.X, ey = cy - m.Y, d2 = ex * ex + ey * ey;
                        if (d2 <= r2 && d2 >= min2) return p.Id;
                    }
                }
            }
            return -1;
        }

        static int Div(int v, int c) { return v >= 0 ? v / c : (v - c + 1) / c; }
        static long Clave(int x, int y) { return ((long)x << 32) | (uint)y; }
    }
}
