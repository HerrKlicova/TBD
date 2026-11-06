using System;
using System.Collections.Generic;

namespace TBD.Net
{
    // Hito 4 «Colmena», parte 1: memoria de tráfico (diseno-colmena-h4.md §1, de Lucía; fórmulas validadas por Hugo, P3) e informe de
    // inteligencia (§5, banco de textos de Lucía en Assets/TBD/Data/textos_informe.csv). Todo en enteros y determinista.

    public struct TrafficParams
    {
        public int Inc, SpillMil, DecayMil, Slots, Warn, Ambush, Cut;
        public static TrafficParams Default => new TrafficParams { Inc = 200, SpillMil = 500, DecayMil = 850, Slots = 6, Warn = 400, Ambush = 600, Cut = 850 };

        public static TrafficParams FromCsv(CsvTable colmena)
        {
            return new TrafficParams
            {
                Inc = (int)colmena.Value("memoria_trafico_inc"), SpillMil = (int)colmena.Value("memoria_trafico_derrame"),
                DecayMil = (int)colmena.Value("memoria_trafico_decae_dia"), Slots = (int)colmena.PositiveValue("memoria_trafico_franjas"),
                Warn = (int)colmena.Value("memoria_umbral_aviso"), Ambush = (int)colmena.Value("memoria_umbral_emboscada"), Cut = (int)colmena.Value("memoria_umbral_corte"),
            };
        }
    }

    /// <summary>Entrada del informe de inteligencia: id del banco de textos, cuándo y sus variables (el texto se monta en la vista).</summary>
    public struct IntelEntry
    {
        public long Tick;
        public string Id;
        public string[] Vars;           // pares clave, valor: {"tren", "2 Montaña", "tramo", "Segorbe–Barracas", ...}
    }

    /// <summary>
    /// Memoria de tráfico: por tramo y franja de 4 h, valor entero (0-1000 en la práctica; puede pasar un poco tras un paso). Cada paso de un tren suma Inc en su franja y derrama un
    /// SpillMil/1000 a las dos franjas vecinas (así alternar 06:00 y 07:00 no engaña). Cada medianoche decae ×DecayMil/1000.
    /// Al cruzar el umbral de aviso (400) hacia arriba sale TR-1 en el informe; al bajar de él, TR-5 («ha olvidado»).
    /// </summary>
    public sealed class TrafficMemory
    {
        public readonly int[,] M;
        readonly bool[,] _known;        // por encima del aviso (para informar al cruzar en un sentido u otro)
        public readonly TrafficParams P;
        public int SlotTicks => NetConst.DayTicks / P.Slots;

        public TrafficMemory(int edges, TrafficParams p)
        {
            P = p;
            M = new int[edges, p.Slots];
            _known = new bool[edges, p.Slots];
        }

        public int SlotOf(long tick) { return (int)(tick % NetConst.DayTicks / SlotTicks); }

        /// <summary>Un tren pasa por el tramo «edge» en el tick dado. Devuelve true si la franja acaba de cruzar el aviso.</summary>
        public bool OnPass(int edge, long tick)
        {
            int s = SlotOf(tick);
            Add(edge, s, P.Inc);
            int spill = P.Inc * P.SpillMil / 1000;
            Add(edge, (s + 1) % P.Slots, spill);
            Add(edge, (s + P.Slots - 1) % P.Slots, spill);
            if (!_known[edge, s] && M[edge, s] >= P.Warn) { _known[edge, s] = true; return true; }
            return false;
        }

        // Sin tope en 1000: el modelo validado de Hugo (P3) llega a 868 tras pasar de 1022 antes del decaimiento. Con +200 diarios la
        // asíntota es 200 / (1 - 0,85) ≈ 1333 justo después de pasar. Tope de seguridad de 100.000 para que nunca desborde.
        void Add(int e, int s, int v) { M[e, s] = Math.Min(100000, M[e, s] + v); }

        /// <summary>Decaimiento de medianoche. Llama a «forgot(edge, slot)» por cada franja que baja del aviso.</summary>
        public void Decay(Action<int, int> forgot)
        {
            for (int e = 0; e < M.GetLength(0); e++)
                for (int s = 0; s < P.Slots; s++)
                {
                    M[e, s] = M[e, s] * P.DecayMil / 1000;
                    if (_known[e, s] && M[e, s] < P.Warn) { _known[e, s] = false; forgot?.Invoke(e, s); }
                }
        }

        public ulong Hash()
        {
            ulong h = 0x7EAF1CUL;
            for (int e = 0; e < M.GetLength(0); e++)
                for (int s = 0; s < P.Slots; s++)
                    if (M[e, s] != 0 || _known[e, s]) h += NetWorld.Mix(((ulong)(uint)e << 40) ^ ((ulong)(uint)s << 32) ^ (ulong)(uint)M[e, s] ^ (_known[e, s] ? 1UL << 20 : 0UL));
            return h;
        }
    }

    /// <summary>Banco de textos del informe (textos_informe.csv de Lucía): «id;fuente;es;en».</summary>
    public sealed class IntelTexts
    {
        readonly Dictionary<string, string[]> _rows = new Dictionary<string, string[]>();

        public IntelTexts(string csv)
        {
            var t = new CsvTable(csv);
            foreach (var r in t.Rows) if (r.Length >= 4) _rows[r[0].Trim()] = r;
        }

        public bool Has(string id) => _rows.ContainsKey(id);

        /// <summary>Texto de la entrada con sus variables sustituidas. lang = "es" o "en". Devuelve también la fuente.</summary>
        public string Format(IntelEntry e, string lang, out string source)
        {
            source = "";
            if (!_rows.TryGetValue(e.Id, out var r)) return e.Id;
            source = r[1];
            string text = lang == "en" ? r[3] : r[2];
            if (e.Vars != null)
                for (int i = 0; i + 1 < e.Vars.Length; i += 2)
                {
                    text = text.Replace("{" + e.Vars[i] + "}", e.Vars[i + 1]);
                    text = text.Replace("{" + EnglishKey(e.Vars[i]) + "}", e.Vars[i + 1]);
                }
            return text;
        }

        static string EnglishKey(string es)
        {
            switch (es)
            {
                case "tren": return "train";
                case "tramo": return "segment";
                case "hora": return "time";
                case "nodo": return "node";
                case "punto": return "point";
                case "dias": return "days";
                case "muertos": return "dead";
                case "poblacion": return "population";
                default: return es;
            }
        }
    }
}
