using System;
using System.Diagnostics;

namespace TBD.Sim.Tests
{
    /// <summary>
    /// Vigilancia del coste del C# sin Burst (nota de Sara, QA, 6-nov): un límite absoluto en milisegundos depende de la máquina y una media suelta de una ventana corta
    /// depende de un tropiezo. Aquí se mide, en la misma ejecución, (1) una tarea de calibración fija que no usa el código vigilado, (2) el MÍNIMO de 5 ventanas de
    /// ticks (el ruido del sistema solo suma tiempo; una regresión real sube todas las ventanas) y se exige que ms_tick / ms_calibración quede por debajo de un valor
    /// fijado con la medida de referencia más un margen de 1,5×. Hasta 3 intentos: falla solo si los 3 pasan del límite. Los 33 ms de un fotograma a 30 Hz son un AVISO
    /// (no un fallo) cuando la calibración dice que la máquina es normal: el presupuesto real es el de Burst, medido en el Editor.
    /// TBD_PERF_INJECT=3 repite el tick 3 veces por tick medido (una regresión de 3× inyectada, solo para comprobar que la prueba detecta).
    /// </summary>
    static class PerfGate
    {
        public const int Windows = 5, Attempts = 3;
        public const double BudgetMs = 33.0;
        // Referencias medidas en el contenedor (4 CPU) con esta misma tarea, con y sin carga de CPU (4 procesos «yes»), y el margen de 1,5× ya aplicado.
        // Release: cociente 1,0–1,4 → límite 1,7. Debug (el «dotnet run» por defecto, 2,5–3,5× más lento: ahí salían los 31–34 ms): 307 sólidos 2,0–2,15 → 3,2; 7 sólidos 1,5–1,6 → 2,4.
        // Una regresión de 3× da ≥ 3,3 en Release y ≥ 6 en Debug: por encima de los dos límites.
#if DEBUG
        public const string Config = "Debug";
        public const double CalibRefMs = 16.2;
        public const double RatioIndexed = 3.2;           // 10.000 zombis y 307 sólidos con índice
        public const double RatioSimple = 2.4;            // 10.000 zombis y 7 sólidos
#else
        public const string Config = "Release";
        public const double CalibRefMs = 8.6;             // calibración de una máquina «normal»; con el doble o más es «lenta» y no se avisa de los 33 ms
        public const double RatioIndexed = 1.7;
        public const double RatioSimple = 1.7;
#endif
        static double _calBest = double.MaxValue;         // la calibración más baja vista en el proceso: el ruido solo suma tiempo, así que es la mejor estimación de la velocidad de la máquina

        static readonly float[] Table = BuildTable();
        static float[] BuildTable() { var t = new float[1 << 18]; uint x = 12345u; for (int i = 0; i < t.Length; i++) { x ^= x << 13; x ^= x >> 17; x ^= x << 5; t[i] = (x & 0xFFFF) / 65536f; } return t; }
        static float _sink;

        static int Inject
        {
            get { int k; return int.TryParse(Environment.GetEnvironmentVariable("TBD_PERF_INJECT"), out k) && k > 1 ? k : 1; }
        }

        /// <summary>Coste fijo de enteros, flotantes y accesos a memoria que no pasa por el código vigilado. Devuelve el mínimo de 5 repeticiones, en ms.</summary>
        public static double Calibrate()
        {
            double best = double.MaxValue;
            for (int rep = 0; rep < 5; rep++)
            {
                var sw = Stopwatch.StartNew();
                uint x = 2463534242u; float acc = 0f; int mask = Table.Length - 1;
                for (int i = 0; i < 4_000_000; i++)
                {
                    x ^= x << 13; x ^= x >> 17; x ^= x << 5;
                    acc += Table[x & (uint)mask] * 0.5f + MathF.Sqrt(i & 1023);
                }
                sw.Stop(); _sink += acc;
                best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
            }
            return best;
        }

        public static void Verdict(string what, Action tick, int ticksPerWindow, double ratioLimit)
        {
            for (int t = 0; t < 5; t++) tick();                                            // calentamiento
            int inject = Inject;
            double lastMin = 0, lastRatio = 0;
            for (int attempt = 1; attempt <= Attempts; attempt++)
            {
                _calBest = Math.Min(_calBest, Calibrate());
                double cal = _calBest;
                var ms = new double[Windows];
                for (int w = 0; w < Windows; w++)
                {
                    var sw = Stopwatch.StartNew();
                    for (int t = 0; t < ticksPerWindow; t++) for (int k = 0; k < inject; k++) tick();
                    sw.Stop(); ms[w] = sw.Elapsed.TotalMilliseconds / ticksPerWindow;
                }
                Array.Sort(ms);
                double min = ms[0], median = ms[Windows / 2], ratio = min / cal;
                Console.WriteLine($"         {what}: mínimo {min:0.00} ms, mediana {median:0.00} ms por tick; calibración {cal:0.0} ms; cociente {ratio:0.000} (límite {ratioLimit:0.000}); intento {attempt}/{Attempts}");
                lastMin = min; lastRatio = ratio;
                if (ratio <= ratioLimit)
                {
                    if (Config == "Debug") Console.WriteLine("         (compilación Debug: el coste no es representativo, 2,5–3,5 veces el de Release; para medir de verdad: dotnet run -c Release)");
                    else if (min > BudgetMs && cal <= 2.0 * CalibRefMs)
                        Console.WriteLine($"         AVISO: {min:0.00} ms supera los 33 ms de un fotograma a 30 Hz y la máquina es normal (calibración {cal:0.0} ms): el presupuesto real es el de Burst, medido en el Editor");
                    Program.Check(true, $"{what} cabe en el coste esperado: {min:0.00} ms por tick (cociente {ratio:0.000} ≤ {ratioLimit:0.000} con la calibración de esta máquina; {Config}, sin Burst, solo orientativo)");
                    return;
                }
            }
            Program.Check(false, $"{what}: regresión de coste, mínimo {lastMin:0.00} ms por tick y cociente {lastRatio:0.000} por encima de {ratioLimit:0.000} en los {Attempts} intentos");
        }
    }
}
