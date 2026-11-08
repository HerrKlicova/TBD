using System;
using System.Collections.Generic;
using TBD.Net;

namespace TBD.Units
{
    // Bucle de la noche de Sagunto (Lucía y Hugo, D-N72..D-N76): reloj de 22:00 a 06:00 a ×28 (17,1 min reales), oleadas desde el borde oeste con un
    // frente denso y una cola dispersa, y victoria o derrota. Dentro de la simulación determinista: enteros, xorshift propio, hash y guardado.
    // REGLA DE FIN DE PARTIDA (Lucía): SIEMPRE GANA LA DERROTA. Si en el mismo tick cae el edificio de viajeros o la locomotora y dan las 06:00, es derrota.
    // La simulación NO crea zombis: devuelve los puntos de aparición de cada tick (SpawnPoint) y la zona los materializa.

    // D-N91 (Lucía): MUNDO CONTINUO. La noche no es una misión aparte: al amanecer sale un parte (NightReport) y el mundo sigue. Victoria = «noche cerrada»:
    // el reloj sigue corriendo (Step ya no genera zombis ni evalúa derrota) hasta que se llame a BeginNextNight. Solo la derrota acaba la partida.
    public enum NightState : byte { Jugando = 0, Victoria = 1, Derrota = 2 }
    /// <summary>Por qué se perdió (D-N133): sin estaciones principales, sin trenes o las dos cosas en el mismo tick.</summary>
    public enum DefeatCause : byte { Ninguna = 0, Estacion = 1, Tren = 2, Ambas = 3 }

    /// <summary>Parte del amanecer: lo que le enseña la interfaz al cerrarse la noche.</summary>
    public struct NightReport
    {
        public int Night, CloseTick, Spawned; public bool StationAlive, TrainAlive;
        /// <summary>Filas del parte (D-N104, Lucía; sin estrellas desde la D-N149): integridad = mín(vida del edificio, vida del tren) en ‰ del máximo; muro = vida que queda en las piezas de muro / suma de máximos, en ‰.</summary>
        public int IntegrityPermil, WallPermil;
    }

    public struct WaveSpec { public int StartMinute; public int Zombies; public int ArrivalSeconds; public int[] EntryCount; }   // EntryCount (opcional): reparto exacto por entrada
    public struct EntrySpec { public string Name; public int X0, Y0, X1, Y1; public int Weight; }   // rectángulo de aparición en mm y peso (‰ relativo)
    public struct RectMm { public int X0, Y0, X1, Y1; public RectMm(int x0, int y0, int x1, int y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; } }
    public struct SpawnPoint { public int Wave, Entry, X, Y; public bool Front; }

    public struct NightParams
    {
        public int ClockX;                    // 1 s real = ClockX s de juego
        public int StartHour, EndHour;
        public int ScalePermil;               // palanca única de dificultad (D-N74)
        public WaveSpec[] Waves;
        public EntrySpec[] Entries;
        public int FrontPct, FrontTimePct;    // el frente: FrontPct % de los zombis en el primer FrontTimePct % de la llegada
        // Vida del edificio de viajeros y del tren (puntos) y lo que quita cada zombi pegado por segundo. VALORES DE DISEÑO SIN MEDIR (Hugo: estación 60000
        // con 20 por zombi·s = 100 zombis dentro la derriban en 30 s; el tren no tiene cifra de Hugo todavía: provisional, el mismo desgaste).
        public int StationHp, TrainHp, StationDmgPerZombieS, TrainDmgPerZombieS;
        // Contacto zombi-soldado: igual que la zona (balance_combate: cuerpo_a_cuerpo_mm = 900, zombi_danio_tick = 2 por zombi y tick).
        public int ContactRadiusMm, ContactDmgPerTick;
        // Regla de «pegado» (UNA para la zona y para NightSim): un zombi vivo ataca al edificio o al tren si está a menos de AttackReachMm del rectángulo
        // (distancia del punto al rectángulo, 0 dentro). Coordenadas de la zona en mm: edificio de viajeros x 98-114, y 75-83 m (PrototypeBootstrap);
        // tren en la vía (y = 64,5 ± 1,6 m), de x 74 a 154 m (Hugo: el tren ocupa el andén). VALORES DE DISEÑO SIN MEDIR.
        public int AttackReachMm;
        public RectMm StationRect, TrainRect;
        /// <summary>D-N136 (Lucía): la noche se gana al acabar con la horda, sin esperar al amanecer (además de la victoria de siempre a las 06:00).</summary>
        public bool WinOnClear;

        public static NightParams Default()
        {
            return new NightParams
            {
                ClockX = 28, StartHour = 22, EndHour = 6, ScalePermil = 850, FrontPct = 55, FrontTimePct = 25,
                StationHp = 60000, TrainHp = 120000, AttackReachMm = 1500, StationRect = new RectMm(98000, 75000, 114000, 83000), TrainRect = new RectMm(81000, 62900, 145000, 66100), StationDmgPerZombieS = 20, TrainDmgPerZombieS = 20, ContactRadiusMm = 900, ContactDmgPerTick = 2, WinOnClear = true,
                // D-N136 (Lucía y Hugo, 7-nov 13:50): una sola horda de 4.700 (escala 1000) a las 22:00, que entra durante 180 s (D-N136b) por las cuatro entradas (30/30/20/20)
                Waves = new[] { new WaveSpec { StartMinute = 0, Zombies = 8600, ArrivalSeconds = 180, EntryCount = new[] { 50, 30, 20, 0 } } },
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
            d.StationHp = (int)t.TryValue("noche_estacion_vida", d.StationHp);
            d.TrainHp = (int)t.TryValue("noche_tren_vida", d.TrainHp);
            d.StationDmgPerZombieS = (int)t.TryValue("noche_estacion_danio_zombi_s", d.StationDmgPerZombieS);
            d.TrainDmgPerZombieS = (int)t.TryValue("noche_tren_danio_zombi_s", d.TrainDmgPerZombieS);
            d.ContactRadiusMm = (int)t.TryValue("noche_contacto_mm", d.ContactRadiusMm);
            d.ContactDmgPerTick = (int)t.TryValue("noche_contacto_danio_tick", d.ContactDmgPerTick);
            int n = (int)t.TryValue("noche_oleadas", d.Waves.Length);
            var w = new WaveSpec[n];
            for (int i = 0; i < n; i++)
            {
                var def = i < d.Waves.Length ? d.Waves[i] : default(WaveSpec);
                string k = "noche_oleada" + (i + 1) + "_";
                w[i] = new WaveSpec { StartMinute = (int)t.TryValue(k + "minuto", def.StartMinute), Zombies = (int)t.TryValue(k + "zombis", def.Zombies), ArrivalSeconds = (int)t.TryValue(k + "llegada_s", def.ArrivalSeconds), EntryCount = def.EntryCount };
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
        public int NightIndex = 1;                          // noches vividas (1 = la primera)
        public NightReport Report;                          // parte de la última noche cerrada (válido si State == Victoria)
        public int NightsSurvived;
        public bool EndedByStation, EndedByTrain;
        /// <summary>D-N136: zombis vivos según quien simula (la zona o NightSim), puesto ANTES de Step. −1 = no se sabe (no hay victoria por limpieza). No entra en el hash: es una entrada.</summary>
        public int ZombiesAlive = -1;
        /// <summary>true si la última victoria fue por acabar con la horda (D-N136) y no por llegar al amanecer.</summary>
        public bool WonByClear;
        int _lastSpawnTick = -1;
        /// <summary>¿Han entrado ya todos los zombis de todas las oleadas?</summary>
        public bool AllSpawned { get { for (int w = 0; w < Spawned.Length; w++) if (Spawned[w] < WaveTotal(w)) return false; return true; } }
        /// <summary>Recuento del último tick (D-N133). Hoy valen 1 o 0; con varios frentes, las estaciones principales vivas y los trenes vivos. No entran en el hash: son entradas del tick.</summary>
        public int StationsAlive = 1, TrainsAlive = 1;
        /// <summary>Causa de la derrota (Ninguna si no se ha perdido).</summary>
        public DefeatCause Cause => State != NightState.Derrota ? DefeatCause.Ninguna : (DefeatCause)((EndedByStation ? 1 : 0) | (EndedByTrain ? 2 : 0));
        /// <summary>Titular de la derrota: «El tren ha caído.», «Sagunto ha caído.» o los dos. Vacío si no se ha perdido.</summary>
        public string DefeatHeadline(string station = "Sagunto")
        {
            switch (Cause)
            {
                case DefeatCause.Estacion: return station + " ha caído.";
                case DefeatCause.Tren: return "El tren ha caído.";
                case DefeatCause.Ambas: return station + " y el tren han caído.";
                default: return "";
            }
        }
        public readonly int[] Spawned;                      // zombis ya generados por oleada
        readonly List<SpawnPoint> _scratch = new List<SpawnPoint>();
        public readonly int[][] SpawnedEntry;               // y por entrada (reparto exacto por cuotas)

        // Vida actual (entra en el hash y en el guardado). El reloj y los zombis no la reparan: solo RepairStation/RepairTrain.
        public int StationHp, TrainHp;
        /// <summary>‰ de vida que queda en el muro (piezas de muro, sacos y nidos; las destruidas cuentan 0). Lo calcula quien conoce las obras y lo anota aquí
        /// (Obras.WallPermil en la red); NightGame lo copia al parte al amanecer. 1000 por defecto.</summary>
        public int WallPermil = 1000;
        public int IntegrityPermil { get { return (int)Math.Min((long)StationHp * 1000 / Math.Max(1, P.StationHp), (long)TrainHp * 1000 / Math.Max(1, P.TrainHp)); } }
        int _stationRem, _trainRem;                         // desgaste fraccionario (puntos·ticks / 30) para que dañar a ×1 zombi sea exacto
        public int StationHpMax { get { return P.StationHp; } }
        public int TrainHpMax { get { return P.TrainHp; } }

        public NightGame(NightParams p, uint seed) { P = p; StationHp = p.StationHp; TrainHp = p.TrainHp; Rng = new IRng(seed); Spawned = new int[p.Waves.Length]; SpawnedEntry = new int[p.Waves.Length][]; for (int w = 0; w < p.Waves.Length; w++) SpawnedEntry[w] = new int[p.Entries.Length]; }

        /// <summary>Daño de este tick al edificio y al tren: «atStation» y «atTrain» son los zombis pegados a cada uno (los cuenta la zona; un zombi que
        /// toca los dos cuenta en los dos). Cada zombi quita StationDmgPerZombieS/TrainDmgPerZombieS por segundo (÷30 por tick, con resto exacto). La vida no baja
        /// de 0; en cuanto una llega a 0, el siguiente Step es DERROTA. Devuelve el daño aplicado ((estación, tren) en dos campos de LastDmgStation/LastDmgTrain).</summary>
        public void Attack(int atStation, int atTrain)
        {
            LastDmgStation = LastDmgTrain = 0;
            if (State == NightState.Derrota) return;
            if (atStation > 0 && StationHp > 0) { long acc = _stationRem + (long)atStation * P.StationDmgPerZombieS; int d = (int)Math.Min(acc / IMath.TickRate, StationHp); _stationRem = (int)(acc % IMath.TickRate); StationHp -= d; LastDmgStation = d; }
            if (atTrain > 0 && TrainHp > 0) { long acc = _trainRem + (long)atTrain * P.TrainDmgPerZombieS; int d = (int)Math.Min(acc / IMath.TickRate, TrainHp); _trainRem = (int)(acc % IMath.TickRate); TrainHp -= d; LastDmgTrain = d; }
        }
        public int LastDmgStation, LastDmgTrain;

        /// <summary>Cuenta los zombis pegados al edificio y al tren con la regla de NightParams (alcance al rectángulo). Un zombi que toca ambos cuenta en los dos.
        /// Úsalo con la lista de zombis vivos y pasa el resultado a Attack. O(zombis), sin orden ni azar.</summary>
        public void CountAttackers(IList<Target> zombies, out int atStation, out int atTrain)
        {
            atStation = atTrain = 0; if (zombies == null) return;
            long r2 = (long)P.AttackReachMm * P.AttackReachMm; var a = P.StationRect; var b = P.TrainRect;
            for (int i = 0; i < zombies.Count; i++)
            {
                var z = zombies[i];
                if (Dist2(z.X, z.Y, a) <= r2) atStation++;
                if (Dist2(z.X, z.Y, b) <= r2) atTrain++;
            }
        }
        static long Dist2(int x, int y, RectMm r)
        {
            long dx = x < r.X0 ? r.X0 - x : x > r.X1 ? x - r.X1 : 0, dy = y < r.Y0 ? r.Y0 - y : y > r.Y1 ? y - r.Y1 : 0;
            return dx * dx + dy * dy;
        }
        /// <summary>¿Está (x, y) pegado al tren (la misma regla que CountAttackers)?</summary>
        public bool TouchesTrain(int x, int y) { return Dist2(x, y, P.TrainRect) <= (long)P.AttackReachMm * P.AttackReachMm; }
        public bool StationAlive { get { return StationHp > 0; } }
        public bool TrainAlive { get { return TrainHp > 0; } }
        /// <summary>Reparación (la pagará la economía): suma sin pasar del máximo. Un edificio o un tren a 0 no se reparan: la partida ya acabó.</summary>
        public int RepairStation(int hp) { if (StationHp <= 0 || hp <= 0) return 0; int d = Math.Min(hp, P.StationHp - StationHp); StationHp += d; return d; }
        public int RepairTrain(int hp) { if (TrainHp <= 0 || hp <= 0) return 0; int d = Math.Min(hp, P.TrainHp - TrainHp); TrainHp += d; return d; }

        /// <summary>Un tick con la vida propia: equivale a Step(StationHp &gt; 0, TrainHp &gt; 0, spawns). Llama antes a Attack con los zombis pegados.</summary>
        public void Step(List<SpawnPoint> spawns) { Step(StationHp > 0, TrainHp > 0, spawns); }

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
        /// <summary>Un tick con recuentos (D-N133): se pierde con 0 estaciones principales vivas o 0 trenes vivos.</summary>
        public void Step(int stationsAlive, int trainsAlive, List<SpawnPoint> spawns)
        {
            Step(stationsAlive > 0, trainsAlive > 0, spawns);
            StationsAlive = stationsAlive < 0 ? 0 : stationsAlive; TrainsAlive = trainsAlive < 0 ? 0 : trainsAlive;
        }

        public void Step(bool stationAlive, bool trainAlive, List<SpawnPoint> spawns)
        {
            StationsAlive = stationAlive ? 1 : 0; TrainsAlive = trainAlive ? 1 : 0;
            if (spawns != null) spawns.Clear();
            if (State == NightState.Derrota) return;
            if (State == NightState.Victoria) { Tick++; return; }      // mundo continuo: la noche está cerrada, el reloj sigue y no entra nadie
            Tick++;
            if (spawns == null) spawns = _scratch;
            spawns.Clear();
            for (int w = 0; w < P.Waves.Length; w++)
            {
                int t = Tick - WaveStartTick(w);
                if (t < 0) continue;
                int due = Cumulative(w, t) - Spawned[w];
                bool front = t <= WaveDurationTicks(w) * P.FrontTimePct / 100;
                for (int k = 0; k < due; k++) spawns.Add(Place(w, front));
                if (due > 0) { Spawned[w] += due; _lastSpawnTick = Tick; }
            }
            // fin de partida: la derrota tiene prioridad sobre la victoria en el mismo tick
            if (!stationAlive || !trainAlive) { State = NightState.Derrota; DefeatTick = Tick; EndedByStation = !stationAlive; EndedByTrain = !trainAlive; }
            else if (P.WinOnClear && ZombiesAlive == 0 && Tick > _lastSpawnTick && AllSpawned)      // D-N164 (Lucía, 8-nov): solo se gana acabando con la horda; a las 06:00 la que queda sigue atacando
            {
                WonByClear = true; State = NightState.Victoria; NightsSurvived++;
                int sp = 0; for (int i = 0; i < Spawned.Length; i++) sp += Spawned[i];
                Report = new NightReport { Night = NightIndex, CloseTick = Tick, Spawned = sp, StationAlive = stationAlive, TrainAlive = trainAlive, IntegrityPermil = IntegrityPermil, WallPermil = WallPermil };
            }
        }

        /// <summary>Empieza la noche siguiente (mundo continuo): reloj a las 22:00, oleadas a cero y el mismo generador. Solo tras cerrar una noche.</summary>
        public bool BeginNextNight(NightParams next)
        {
            if (State != NightState.Victoria) return false;
            P = next; Tick = 0; State = NightState.Jugando; NightIndex++; DefeatTick = -1; EndedByStation = EndedByTrain = false; WonByClear = false; _lastSpawnTick = -1;
            if (Spawned.Length != next.Waves.Length) throw new ArgumentException("otro número de oleadas: crea un NightGame nuevo");
            for (int w = 0; w < Spawned.Length; w++) { Spawned[w] = 0; for (int e = 0; e < SpawnedEntry[w].Length; e++) SpawnedEntry[w][e] = 0; }
            return true;
        }

        SpawnPoint Place(int wave, bool front)
        {
            // reparto EXACTO por cuotas: cada zombi va a la entrada que más le falta respecto a su peso (empate: la de menor índice).
            // Con los pesos de la oleada (EntryCount, de noche_sagunto.json) el reparto final coincide con el de Hugo al zombi.
            var ec = P.Waves[wave].EntryCount; var se = SpawnedEntry[wave];
            long total = 0; for (int i = 0; i < P.Entries.Length; i++) total += ec != null && i < ec.Length ? ec[i] : P.Entries[i].Weight;
            long k1 = 1; for (int i = 0; i < se.Length; i++) k1 += se[i]; int e = 0; long best = long.MinValue;
            if (total > 0)
                for (int i = 0; i < P.Entries.Length; i++)
                {
                    long wi = ec != null && i < ec.Length ? ec[i] : P.Entries[i].Weight;
                    long deficit = wi * k1 - (long)se[i] * total;
                    if (wi > 0 && deficit > best) { best = deficit; e = i; }
                }
            se[e]++;
            var en = P.Entries[Math.Min(e, P.Entries.Length - 1)];
            int w = en.Y1 - en.Y0;
            int y = front ? en.Y0 + w / 3 + Rng.Below(Math.Max(1, w / 3)) : en.Y0 + Rng.Below(Math.Max(1, w));       // el frente va por el tercio central; la cola, a todo lo ancho
            int x = en.X0 + Rng.Below(Math.Max(1, en.X1 - en.X0));
            return new SpawnPoint { Wave = wave, Entry = e, X = x, Y = y, Front = front };
        }

        public ulong Hash()
        {
            ulong h = IMath.Mix(0x20E0 ^ (ulong)(uint)Tick) + IMath.Mix(0x20E1 ^ Rng.S) + IMath.Mix(0x20E2 ^ (ulong)State ^ ((ulong)(uint)(DefeatTick + 1) << 8)) + IMath.Mix(0x20E3 ^ (ulong)(uint)P.ScalePermil ^ ((ulong)(uint)P.ClockX << 20)) + IMath.Mix(0x20E4 ^ (ulong)(uint)NightIndex ^ ((ulong)(uint)NightsSurvived << 20) ^ ((ulong)(uint)Report.CloseTick << 40)) + IMath.Mix(0x20E5 ^ (ulong)(uint)StationHp ^ ((ulong)(uint)TrainHp << 24)) + IMath.Mix(0x20E7 ^ (ulong)(uint)WallPermil ^ ((ulong)(uint)Report.IntegrityPermil << 20) ^ ((ulong)(uint)Report.WallPermil << 40)) + (_stationRem != 0 || _trainRem != 0 ? IMath.Mix(0x20E6 ^ (ulong)(uint)_stationRem ^ ((ulong)(uint)_trainRem << 16)) : 0UL);
            for (int i = 0; i < Spawned.Length; i++) { h += IMath.Mix((ulong)(uint)Spawned[i] + ((ulong)i << 40) + 17); for (int e = 0; e < SpawnedEntry[i].Length; e++) h += IMath.Mix((ulong)(uint)SpawnedEntry[i][e] + ((ulong)i << 40) + ((ulong)e << 52) + 19); }
            return h;
        }

        public void SaveState(SaveWriter w)
        {
            w.Int(Tick); w.Byte((byte)State); w.UInt(Rng.S); w.Int(DefeatTick); w.Bool(EndedByStation); w.Bool(EndedByTrain);
            w.Int(NightIndex); w.Int(NightsSurvived); w.Int(Report.Night); w.Int(Report.CloseTick); w.Int(Report.Spawned); w.Bool(Report.StationAlive); w.Bool(Report.TrainAlive);
            w.Int(StationHp); w.Int(TrainHp); w.Int(_stationRem); w.Int(_trainRem); w.Int(WallPermil); w.Int(Report.IntegrityPermil); w.Int(Report.WallPermil);
            w.Int(Spawned.Length); for (int i = 0; i < Spawned.Length; i++) { w.Int(Spawned[i]); for (int e = 0; e < SpawnedEntry[i].Length; e++) w.Int(SpawnedEntry[i][e]); }
        }

        public void LoadState(SaveReader r)
        {
            Tick = r.Int(); State = (NightState)r.Byte(); Rng.S = r.UInt(); DefeatTick = r.Int(); EndedByStation = r.Bool(); EndedByTrain = r.Bool();
            NightIndex = r.Int(); NightsSurvived = r.Int(); Report = new NightReport { Night = r.Int(), CloseTick = r.Int(), Spawned = r.Int(), StationAlive = r.Bool(), TrainAlive = r.Bool() };
            StationHp = r.Int(); TrainHp = r.Int(); _stationRem = r.Int(); _trainRem = r.Int(); WallPermil = r.Int(); int ri = r.Int(), rw = r.Int(); Report.IntegrityPermil = ri; Report.WallPermil = rw;
            int n = r.Int(); if (n != Spawned.Length) throw new SaveException(SaveError.Corrupt, "otro número de oleadas");
            for (int i = 0; i < n; i++) { Spawned[i] = r.Int(); for (int e = 0; e < SpawnedEntry[i].Length; e++) SpawnedEntry[i][e] = r.Int(); }
        }
    }
}
