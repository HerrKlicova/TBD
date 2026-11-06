using System;
using System.Collections.Generic;
using TBD.Net;

namespace TBD.Units
{
    // Bucle de la noche de Sagunto (Lucía y Hugo, D-N72..D-N76): reloj de 22:00 a 06:00 a ×28 (17,1 min reales), oleadas desde el borde oeste con un
    // frente denso y una cola dispersa, y victoria o derrota. Dentro de la simulación determinista: enteros, xorshift propio, hash y guardado.
    // REGLA DE FIN DE PARTIDA (Lucía): SIEMPRE GANA LA DERROTA. Si en el mismo tick cae el edificio de viajeros o la locomotora y dan las 06:00, es derrota.
    // La simulación NO crea zombis: devuelve los puntos de aparición de cada tick (SpawnPoint) y la zona los materializa.

    public enum NightState : byte { Jugando = 0, Victoria = 1, Derrota = 2 }

    public struct WaveSpec { public int StartMinute; public int Zombies; public int ArrivalSeconds; }
    public struct EntrySpec { public string Name; public int X0, Y0, X1, Y1; public int Weight; }   // rectángulo de aparición en mm y peso (‰ relativo)
    public struct SpawnPoint { public int Wave, Entry, X, Y; public bool Front; }

    public struct NightParams
    {
        public int ClockX;                    // 1 s real = ClockX s de juego
        public int StartHour, EndHour;
        public int ScalePermil;               // palanca única de dificultad (D-N74)
        public WaveSpec[] Waves;
        public EntrySpec[] Entries;
        public int FrontPct, FrontTimePct;    // el frente: FrontPct % de los zombis en el primer FrontTimePct % de la llegada

        public static NightParams Default()
        {
            return new NightParams
            {
                ClockX = 28, StartHour = 22, EndHour = 6, ScalePermil = 850, FrontPct = 55, FrontTimePct = 25,
                Waves = new[]
                {
                    new WaveSpec { StartMinute = 20, Zombies = 600, ArrivalSeconds = 40 }, new WaveSpec { StartMinute = 80, Zombies = 1200, ArrivalSeconds = 50 },
                    new WaveSpec { StartMinute = 160, Zombies = 2000, ArrivalSeconds = 60 }, new WaveSpec { StartMinute = 240, Zombies = 3000, ArrivalSeconds = 70 },
                    new WaveSpec { StartMinute = 320, Zombies = 3600, ArrivalSeconds = 90 }, new WaveSpec { StartMinute = 400, Zombies = 5000, ArrivalSeconds = 100 },
                },
                // Álvaro (sagunto-nivel.md §9.5), zona de 256 × 128 m: (X, Z) en metros → (X, Y) en mm
                Entries = new[]
                {
                    new EntrySpec { Name = "O1 vía", X0 = 0, X1 = 8000, Y0 = 56000, Y1 = 73000, Weight = 50 },
                    new EntrySpec { Name = "O2 carretera", X0 = 0, X1 = 8000, Y0 = 14000, Y1 = 31000, Weight = 30 },
                    new EntrySpec { Name = "O3 pueblo", X0 = 0, X1 = 8000, Y0 = 92000, Y1 = 124000, Weight = 20 },
                    new EntrySpec { Name = "O4 puente", X0 = 248000, X1 = 256000, Y0 = 56000, Y1 = 73000, Weight = 0 },
                },
            };
        }

        public static NightParams FromCsv(CsvTable t)
        {
            var d = Default();
            d.ClockX = (int)t.TryValue("noche_reloj_x", d.ClockX);
            d.StartHour = (int)t.TryValue("noche_hora_inicio", d.StartHour);
            d.EndHour = (int)t.TryValue("noche_hora_fin", d.EndHour);
            d.ScalePermil = (int)t.TryValue("noche_escala_oleadas_permil", d.ScalePermil);
            int n = (int)t.TryValue("noche_oleadas", d.Waves.Length);
            var w = new WaveSpec[n];
            for (int i = 0; i < n; i++)
            {
                var def = i < d.Waves.Length ? d.Waves[i] : default(WaveSpec);
                string k = "noche_oleada" + (i + 1) + "_";
                w[i] = new WaveSpec { StartMinute = (int)t.TryValue(k + "minuto", def.StartMinute), Zombies = (int)t.TryValue(k + "zombis", def.Zombies), ArrivalSeconds = (int)t.TryValue(k + "llegada_s", def.ArrivalSeconds) };
            }
            d.Waves = w;
            return d;
        }
    }

    public sealed class NightGame
    {
        public NightParams P;
        public int Tick;
        public NightState State = NightState.Jugando;
        public IRng Rng;
        public int DefeatTick = -1;
        public bool EndedByStation, EndedByTrain;
        public readonly int[] Spawned;                      // zombis ya generados por oleada

        public NightGame(NightParams p, uint seed) { P = p; Rng = new IRng(seed); Spawned = new int[p.Waves.Length]; }

        public int TotalTicks { get { return (int)((long)((P.EndHour - P.StartHour + 24) % 24) * 3600 * IMath.TickRate / P.ClockX); } }
        public int WaveStartTick(int w) { return (int)((long)P.Waves[w].StartMinute * 60 * IMath.TickRate / P.ClockX); }
        public int WaveDurationTicks(int w) { return P.Waves[w].ArrivalSeconds * IMath.TickRate; }
        public int WaveTotal(int w) { return (int)((long)P.Waves[w].Zombies * P.ScalePermil / 1000); }
        /// <summary>Segundos de juego desde las 22:00.</summary>
        public int GameSeconds { get { return (int)((long)Tick * P.ClockX / IMath.TickRate); } }
        public int Hour { get { return (P.StartHour + GameSeconds / 3600) % 24; } }
        public int Minute { get { return GameSeconds / 60 % 60; } }
        public int TicksLeft { get { return Math.Max(0, TotalTicks - Tick); } }

        /// <summary>Zombis acumulados de la oleada w en el tiempo local t (ticks desde su inicio): frente denso y cola dispersa.</summary>
        public int Cumulative(int w, int t)
        {
            long n = WaveTotal(w), d = WaveDurationTicks(w);
            if (t <= 0) return 0; if (t >= d) return (int)n;
            long df = Math.Max(1, d * P.FrontTimePct / 100), nf = n * P.FrontPct / 100;
            return t <= df ? (int)(nf * t / df) : (int)(nf + (n - nf) * (t - df) / Math.Max(1, d - df));
        }

        /// <summary>Un tick. Rellena «spawns» con los zombis que entran en este tick (la lista se vacía antes). Siempre gana la derrota.</summary>
        public void Step(bool stationAlive, bool trainAlive, List<SpawnPoint> spawns)
        {
            if (spawns != null) spawns.Clear();
            if (State != NightState.Jugando) return;
            Tick++;
            if (spawns != null)
            {
                for (int w = 0; w < P.Waves.Length; w++)
                {
                    int t = Tick - WaveStartTick(w);
                    if (t < 0) continue;
                    int due = Cumulative(w, t) - Spawned[w];
                    bool front = t <= WaveDurationTicks(w) * P.FrontTimePct / 100;
                    for (int k = 0; k < due; k++) spawns.Add(Place(w, front));
                    if (due > 0) Spawned[w] += due;
                }
            }
            else for (int w = 0; w < P.Waves.Length; w++) { int t = Tick - WaveStartTick(w); if (t >= 0) Spawned[w] = Cumulative(w, t); }
            // fin de partida: la derrota tiene prioridad sobre la victoria en el mismo tick
            if (!stationAlive || !trainAlive) { State = NightState.Derrota; DefeatTick = Tick; EndedByStation = !stationAlive; EndedByTrain = !trainAlive; }
            else if (Tick >= TotalTicks) State = NightState.Victoria;
        }

        SpawnPoint Place(int wave, bool front)
        {
            int total = 0; for (int i = 0; i < P.Entries.Length; i++) total += P.Entries[i].Weight;
            int e = 0;
            if (total > 0) { int r = Rng.Below(total), acc = 0; for (; e < P.Entries.Length; e++) { acc += P.Entries[e].Weight; if (r < acc) break; } }
            var en = P.Entries[Math.Min(e, P.Entries.Length - 1)];
            int w = en.Y1 - en.Y0;
            int y = front ? en.Y0 + w / 3 + Rng.Below(Math.Max(1, w / 3)) : en.Y0 + Rng.Below(Math.Max(1, w));       // el frente va por el tercio central; la cola, a todo lo ancho
            int x = en.X0 + Rng.Below(Math.Max(1, en.X1 - en.X0));
            return new SpawnPoint { Wave = wave, Entry = e, X = x, Y = y, Front = front };
        }

        public ulong Hash()
        {
            ulong h = IMath.Mix(0x20E0 ^ (ulong)(uint)Tick) + IMath.Mix(0x20E1 ^ Rng.S) + IMath.Mix(0x20E2 ^ (ulong)State ^ ((ulong)(uint)(DefeatTick + 1) << 8)) + IMath.Mix(0x20E3 ^ (ulong)(uint)P.ScalePermil ^ ((ulong)(uint)P.ClockX << 20));
            for (int i = 0; i < Spawned.Length; i++) h += IMath.Mix((ulong)(uint)Spawned[i] + ((ulong)i << 40) + 17);
            return h;
        }

        public void SaveState(SaveWriter w)
        {
            w.Int(Tick); w.Byte((byte)State); w.UInt(Rng.S); w.Int(DefeatTick); w.Bool(EndedByStation); w.Bool(EndedByTrain);
            w.Int(Spawned.Length); for (int i = 0; i < Spawned.Length; i++) w.Int(Spawned[i]);
        }

        public void LoadState(SaveReader r)
        {
            Tick = r.Int(); State = (NightState)r.Byte(); Rng.S = r.UInt(); DefeatTick = r.Int(); EndedByStation = r.Bool(); EndedByTrain = r.Bool();
            int n = r.Int(); if (n != Spawned.Length) throw new SaveException(SaveError.Corrupt, "otro número de oleadas");
            for (int i = 0; i < n; i++) Spawned[i] = r.Int();
        }
    }
}
