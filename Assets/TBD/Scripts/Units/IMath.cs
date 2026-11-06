using System;

namespace TBD.Units
{
    // Noche del 7-nov (Eugenio): infantería por pelotones, vehículos de tierra y helicóptero del jugador. Lógica pura en enteros, sin Unity.
    //
    // CONVENCIONES (para quien las integre en Unity):
    //   · Plano del suelo: X e Y en milímetros (int). La altura Z (solo helicóptero y relieve) también en mm. En Unity: (X, Y) → (x, z) y Z → y.
    //   · Ángulos en «bam16»: 65536 = una vuelta. 0 = eje +X; crece hacia +Y (sentido antihorario visto desde arriba con Y hacia delante).
    //     Mapeo a Unity y rumbo de los modelos de Teo (miran a +Z): ver Docs/unidades.md.
    //   · Velocidades en mm/s, aceleraciones en mm/s², tiempo en ticks a 30 Hz (TickRate). Controles en milésimas (−1000..1000).
    //   · Nada de float ni de Math.Sin: senos por tabla calculada con aritmética entera, así Mono e IL2CPP dan los mismos bits.

    public static class IMath
    {
        public const int TickRate = 30;
        public const int Turn = 65536;                  // una vuelta en bam16
        public const int One = 65536;                   // 1,0 en Q16

        static readonly int[] _sin = BuildSin();        // cuarto de onda: 1025 entradas, Q16, índice = bam16 / 16

        static int[] BuildSin()
        {
            var t = new int[1025];
            const long halfPi = 1686629713L;            // π/2 en Q30
            for (int i = 0; i <= 1024; i++)
            {
                long x = halfPi * i / 1024;
                long x2 = (x * x) >> 30, term = x, sum = x;
                for (int k = 1; k <= 5; k++)
                {
                    term = ((term * x2) >> 30) / ((2 * k) * (2 * k + 1));
                    sum += (k & 1) == 1 ? -term : term;
                }
                t[i] = (int)((sum + (1L << 13)) >> 14);   // Q30 → Q16 con redondeo
            }
            t[1024] = One;
            return t;
        }

        static int Quarter(int r)                        // r en 0..16384
        {
            int i = r >> 4, f = r & 15;
            return i >= 1024 ? _sin[1024] : _sin[i] + (((_sin[i + 1] - _sin[i]) * f) >> 4);
        }

        /// <summary>Seno de un ángulo bam16, en Q16 (65536 = 1,0).</summary>
        public static int Sin(int a)
        {
            a &= 0xFFFF;
            int q = a >> 14, r = a & 0x3FFF;
            switch (q)
            {
                case 0: return Quarter(r);
                case 1: return Quarter(16384 - r);
                case 2: return -Quarter(r);
                default: return -Quarter(16384 - r);
            }
        }

        public static int Cos(int a) { return Sin(a + 16384); }

        /// <summary>Raíz cuadrada entera exacta (suelo).</summary>
        public static long Isqrt(long n)
        {
            if (n <= 0) return 0;
            long x = (long)Math.Sqrt((double)n);         // semilla; la corrección de abajo hace el resultado exacto
            while (x * x > n) x--;
            while ((x + 1) * (x + 1) <= n) x++;
            return x;
        }

        public static int Dist(int dx, int dy) { return (int)Isqrt((long)dx * dx + (long)dy * dy); }

        /// <summary>Ángulo bam16 del vector (dx, dy). Error máximo ≈ 0,2°. (0, 0) devuelve 0.</summary>
        public static int Atan2(int dy, int dx)
        {
            if (dx == 0 && dy == 0) return 0;
            long ax = Math.Abs((long)dx), ay = Math.Abs((long)dy);
            bool swap = ay > ax;
            long lo = swap ? ax : ay, hi = swap ? ay : ax;
            long z = (lo << 16) / hi;                    // 0..1 en Q16
            // atan(z) ≈ z·π/4 + 0,273·z·(1 − z), en bam16 (π/4 = 8192)
            long a = (z * 8192 >> 16) + ((((z * (65536 - z)) >> 16) * 2848) >> 16);   // 0,273 rad = 2848 bam16
            int ang = (int)a;
            if (swap) ang = 16384 - ang;
            if (dx < 0) ang = 32768 - ang;
            if (dy < 0) ang = -ang;
            return ang & 0xFFFF;
        }

        /// <summary>Diferencia con signo entre dos ángulos, en −32768..32767 (el camino corto).</summary>
        public static int Diff(int to, int from) { return (short)(to - from); }

        public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }

        /// <summary>Acerca v a target como mucho step (en valor absoluto).</summary>
        public static int Approach(int v, int target, int step)
        {
            if (v < target) return v + step > target ? target : v + step;
            return v - step < target ? target : v - step;
        }

        /// <summary>División entera redondeando al más cercano (simétrica en los negativos).</summary>
        public static int DivR(long a, long b)
        {
            long q = a >= 0 ? (a + b / 2) / b : -((-a + b / 2) / b);
            return (int)q;
        }

        public static ulong Mix(ulong h)
        {
            h ^= h >> 33; h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33; h *= 0xc4ceb9fe1a85ec53UL;
            h ^= h >> 33;
            return h;
        }
    }

    /// <summary>Generador xorshift32 determinista (el estado entra en el hash y en el guardado).</summary>
    public struct IRng
    {
        public uint S;
        public IRng(uint seed) { S = seed == 0 ? 0x9E3779B9u : seed; }
        public uint Next() { uint x = S; x ^= x << 13; x ^= x >> 17; x ^= x << 5; S = x; return x; }
        public int Below(int n) { return (int)(Next() % (uint)n); }
    }
}
