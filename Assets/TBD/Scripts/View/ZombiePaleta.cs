using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace TBD.View
{
    /// <summary>
    /// D-N80 · Tono por instancia para el shader TBD/Horda (Bruno, 8-nov). x camisa 0–7, y pantalón 0–5, z piel 0–3, w sangre 0–15.
    /// Solo presentación: la simulación no lo lee. Sustituye a ZombieBaseColor cuando el material es TBD/Horda.
    /// </summary>
    [MaterialProperty("_HordaPack")]
    public struct ZombiePaleta : IComponentData
    {
        public float4 Value;
    }

    /// <summary>
    /// Regla de mezcla de build_horda_color.py (elige), portada tal cual para que sea la misma en todos los clientes del coop.
    /// Camisa y pantalón por peso; piel uniforme; si camisa y pantalón están a menos de 4 de ΔE, el pantalón pasa al siguiente.
    /// Vectores de prueba (Python): 0→(4,0,1) 1→(0,4,2) 2→(3,0,1) 3→(1,0,2) 7→(5,4,1) 41→(3,0,3) 1000→(1,2,2) 65535→(7,0,0).
    /// </summary>
    public static class HordaPaleta
    {
        static readonly int[] PesoCamisa = { 3, 3, 2, 2, 1, 2, 1, 2 };        // suma 16
        static readonly int[] PesoPantalon = { 4, 3, 2, 2, 1, 1 };            // suma 13
        const int Pieles = 4;

        // Pares (camisa, pantalón) con ΔE < 4, calculados con lab() de build_horda_color.py. Bit p de Parecidos[c].
        static readonly int[] Parecidos =
        {
            (1 << 1) | (1 << 2),   // 0 negro desteñido: negro, gris
            (1 << 1) | (1 << 2),   // 1 gris marengo: negro, gris
            (1 << 0) | (1 << 4),   // 2 azul mahón: vaquero, chándal marino
            (1 << 3) | (1 << 5),   // 3 chocolate: marrón, pana parda
            (1 << 3) | (1 << 5),   // 4 tostado: marrón, pana parda
            0,                     // 5 gris azulado
            0,                     // 6 ciruela apagado
            (1 << 2) | (1 << 5),   // 7 pardo sucio: gris, pana parda
        };

        static float H(uint idx, uint k)
        {
            uint n = idx * 374761393u + k * 668265263u;
            n = (n ^ (n >> 13)) * 1274126177u;
            return ((n ^ (n >> 16)) & 0xFFFFu) / 65536f;
        }

        static int PorPeso(int[] pesos, int total, float u)
        {
            // Igual que Python (acumula p/tot en coma flotante); con estos pesos no hay empates en el borde.
            float acc = 0f;
            for (int i = 0; i < pesos.Length; i++) { acc += pesos[i] / (float)total; if (u < acc) return i; }
            return pesos.Length - 1;
        }

        public static int3 Elige(uint id)
        {
            int c = PorPeso(PesoCamisa, 16, H(id, 1));
            int p = PorPeso(PesoPantalon, 13, H(id, 2));
            if ((Parecidos[c] & (1 << p)) != 0) p = (p + 1) % PesoPantalon.Length;
            int s = (int)(H(id, 3) * Pieles);
            return new int3(c, p, s);
        }

        public static float4 Pack(uint id, int sangre = 0)
        {
            var e = Elige(id);
            return new float4(e.x, e.y, e.z, math.clamp(sangre, 0, 15));
        }
    }
}
