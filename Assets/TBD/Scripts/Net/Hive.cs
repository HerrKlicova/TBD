using System;
using System.Collections.Generic;

namespace TBD.Net
{
    // Hito 4 «Colmena», parte 1: memoria de tráfico (diseno-colmena-h4.md §1, de Lucía; fórmulas validadas por Hugo, P3) e informe de
    // inteligencia (§5, banco de textos de Lucía en Assets/TBD/Data/textos_informe.csv). Todo en enteros y determinista.

    public struct TrafficParams
    {
        public int Inc, SpillMil, DecayMil, Slots, Warn, Ambush, Cut, Cap, Forget;
        public static TrafficParams Default => new TrafficParams { Inc = 200, SpillMil = 500, DecayMil = 850, Slots = 6, Warn = 400, Ambush = 600, Cut = 850, Cap = 1200, Forget = 300 };

        public static TrafficParams FromCsv(CsvTable colmena)
        {
            return new TrafficParams
            {
                Inc = (int)colmena.Value("memoria_trafico_inc"), SpillMil = (int)colmena.Value("memoria_trafico_derrame"),
                DecayMil = (int)colmena.Value("memoria_trafico_decae_dia"), Slots = (int)colmena.PositiveValue("memoria_trafico_franjas"),
                Warn = (int)colmena.Value("memoria_umbral_aviso"), Ambush = (int)colmena.Value("memoria_umbral_emboscada"), Cut = (int)colmena.Value("memoria_umbral_corte"),
                Cap = (int)colmena.PositiveValue("memoria_trafico_tope"), Forget = (int)colmena.Value("memoria_umbral_olvido"),
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
    /// Al cruzar el umbral de aviso (400) hacia arriba sale TR-1 en el informe; al bajar del de olvido (300), TR-5 («ha olvidado»).
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

        public void SaveState(SaveWriter w)
        {
            w.Ints2(M); w.Bools2(_known);
            var passed = new List<long>(_passedToday); passed.Sort();           // orden fijo: el recorrido de un HashSet no vale
            w.Int(passed.Count); foreach (var p in passed) w.Long(p);
            w.Long(_passDay);
        }

        public void LoadState(SaveReader r)
        {
            r.Ints2Into(M); r.Bools2Into(_known);
            int n = r.Int();
            if (n < 0 || n > r.Remaining) throw new SaveException(SaveError.Corrupt, "pasos de hoy no válidos");
            _passedToday.Clear(); for (int i = 0; i < n; i++) _passedToday.Add(r.Long());
            _passDay = r.Long();
        }

        public int SlotOf(long tick) { return (int)(tick % NetConst.DayTicks / SlotTicks); }

        // D-N31: un solo paso por tren, tramo, franja y día (dos trenes distintos sí suman). Se guarda (día, tren, tramo, franja).
        readonly HashSet<long> _passedToday = new HashSet<long>();
        long _passDay = -1;

        /// <summary>Un tren pasa por el tramo «edge» en el tick dado. Devuelve true si la franja acaba de cruzar el aviso.</summary>
        public bool OnPass(int edge, long tick, int trainId = -1)
        {
            int s = SlotOf(tick);
            long day = tick / NetConst.DayTicks;
            if (day != _passDay) { _passDay = day; _passedToday.Clear(); }
            if (trainId >= 0 && !_passedToday.Add(((long)(trainId & 0xFFFF) << 32) | ((long)(edge & 0xFFFF) << 8) | (long)s)) return false;
            Add(edge, s, P.Inc);
            int spill = P.Inc * P.SpillMil / 1000;
            Add(edge, (s + 1) % P.Slots, spill);
            Add(edge, (s + P.Slots - 1) % P.Slots, spill);
            if (!_known[edge, s] && M[edge, s] >= P.Warn) { _known[edge, s] = true; return true; }
            return false;
        }

        // D-N31 (Lucía y Hugo, aprobado por Mak): tope de 1.200 por franja. Sin tope, con 3 trenes la memoria llegaría a ~3.400 y
        // tardaría 14-15 días en olvidar; con 1.200 baja de 300 en 8 días como mucho. P3 no cambia (su máximo es 1.022).
        void Add(int e, int s, int v) { M[e, s] = Math.Min(P.Cap, M[e, s] + v); }

        /// <summary>Decaimiento de medianoche. Llama a «forgot(edge, slot)» por cada franja que baja del aviso.</summary>
        public void Decay(Action<int, int> forgot)
        {
            for (int e = 0; e < M.GetLength(0); e++)
                for (int s = 0; s < P.Slots; s++)
                {
                    M[e, s] = M[e, s] * P.DecayMil / 1000;
                    if (_known[e, s] && M[e, s] < P.Forget) { _known[e, s] = false; forgot?.Invoke(e, s); }   // histéresis D-N31: olvida por debajo de 300
                }
        }

        public ulong Hash()
        {
            ulong h = 0x7EAF1CUL ^ ((ulong)_passedToday.Count << 40) ^ (ulong)(_passDay + 1);
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
