using Unity.Mathematics;

namespace TBD.Sim
{
    /// <summary>
    /// Cálculos deterministas fuera de Burst (Mono e IL2CPP dan lo mismo): enteros y una sola operación en coma flotante al final.
    /// </summary>
    public static class Det
    {
        /// <summary>Número aleatorio en [min, max) con resolución de 1 mm, sin cadenas de operaciones en coma flotante.</summary>
        public static float Range(ref Random rng, float min, float max)
        {
            int a = (int)math.round(min * 1000f), b = (int)math.round(max * 1000f);
            return (a + (int)rng.NextUInt((uint)math.max(1, b - a))) / 1000f;
        }

        public static float Mm(int mm) { return mm / 1000f; }
    }

}
