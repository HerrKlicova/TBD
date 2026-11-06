using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TBD.Net
{
    /// <summary>
    /// Repetición dorada de la red (S0), criterio de Sara (docs/qa/criterio-repeticion-dorada.md). Un archivo de texto con:
    /// cabecera (formato, escenario, ticks), las órdenes (tick;tipo;arg;arg2) y el hash de cada tick, más el hash por sistema cada
    /// 300 ticks. El reproductor aplica las mismas órdenes y compara: se queda con el primer tick que difiere y con el sistema culpable.
    /// Sirve para comparar dotnet, el Editor y una compilación (Mono o IL2CPP), y dos sesiones distintas.
    /// </summary>
    public static class NetGolden
    {
        public const string Format = "TBD-GOLDEN-RED v1";
        public const string GameVersion = "0.1.0";      // versión del juego (bundleVersion); el commit va en el mensaje del commit que regenera
        public const int SystemEvery = 300;

        public struct Result
        {
            public bool Ok;
            public long FirstBadTick;       // -1 si todo coincide
            public string System;           // sistema culpable en el primer bloque de 300 ticks que difiere (o «?»)
            public long TicksChecked;
            public override string ToString() => Ok ? $"idéntica ({TicksChecked} ticks)" : $"DIFIERE en el tick {FirstBadTick} (sistema: {System})";
        }

        /// <summary>Juega «ticks» ticks del escenario MVP con «script» y devuelve el archivo dorado.</summary>
        public static string Record(List<NetCommand> script, long ticks, string scenario = "G3 red: un día del MVP")
        {
            var w = NetScenario.Mvp();
            var sb = new StringBuilder();
            sb.Append(Format).Append('\n');
            sb.Append("escenario=").Append(scenario).Append('\n');
            sb.Append("version=0.1.0").Append('\n');                                  // versión del juego (bundleVersion)
            sb.Append("semilla=fija (NetScenario.Mvp no usa azar)").Append('\n');
            sb.Append("inicio_tick=").Append(w.Tick).Append('\n');
            sb.Append("ticks=").Append(ticks).Append('\n');
            sb.Append("ORDENES\n");
            foreach (var c in script) sb.Append(c.Tick).Append(';').Append((int)c.Type).Append(';').Append(c.Arg).Append(';').Append(c.Arg2).Append('\n');
            sb.Append("HASHES\n");
            Play(w, script, ticks, (t, h, sys) =>
            {
                sb.Append(t).Append(';').Append(h.ToString("X16"));
                if (sys != null) foreach (var x in sys) sb.Append(';').Append(x.ToString("X16"));
                sb.Append('\n');
                return true;
            });
            sb.Append("FIN\n");
            return sb.ToString();
        }

        /// <summary>
        /// Reproduce un archivo dorado. «tamper», si no es null, se llama en cada tick y puede alterar el mundo a propósito (la prueba
        /// de que el reproductor sabe fallar).
        /// </summary>
        public static Result Verify(string golden, Action<NetWorld, long> tamper = null)
        {
            var lines = golden.Replace("\r", "").Split('\n');
            if (lines.Length < 6 || lines[0] != Format) throw new FormatException("No es un archivo dorado de la red (" + Format + ")");
            long ticks = 0;
            int i = 1;
            for (; i < lines.Length && lines[i] != "ORDENES"; i++)
                if (lines[i].StartsWith("ticks=")) ticks = long.Parse(lines[i].Substring(6), CultureInfo.InvariantCulture);
            var script = new List<NetCommand>();
            for (i++; i < lines.Length && lines[i] != "HASHES"; i++)
            {
                var f = lines[i].Split(';');
                script.Add(new NetCommand { Tick = long.Parse(f[0], CultureInfo.InvariantCulture), Type = (NetCmdType)int.Parse(f[1], CultureInfo.InvariantCulture), Arg = int.Parse(f[2], CultureInfo.InvariantCulture), Arg2 = int.Parse(f[3], CultureInfo.InvariantCulture) });
            }
            var expected = new Dictionary<long, string[]>();
            for (i++; i < lines.Length && lines[i] != "FIN"; i++)
            {
                if (lines[i].Length == 0) continue;
                var f = lines[i].Split(';');
                expected[long.Parse(f[0], CultureInfo.InvariantCulture)] = f;
            }

            var w = NetScenario.Mvp();
            var r = new Result { Ok = true, FirstBadTick = -1, System = "" };
            long lastSysTick = -1;
            string[] lastGoodSys = null;
            Play(w, script, ticks, (t, h, sys) =>
            {
                tamper?.Invoke(w, t);
                r.TicksChecked++;
                if (!expected.TryGetValue(t, out var f)) { r.Ok = false; r.FirstBadTick = t; r.System = "falta en el archivo"; return false; }
                if (sys != null) { lastSysTick = t; lastGoodSys = f; }
                if (f[1] == h.ToString("X16") && (sys == null || SameSystems(f, sys))) return true;
                r.Ok = false;
                r.FirstBadTick = t;
                r.System = sys != null ? Culprit(f, sys) : CulpritNow(w, t, expected);
                return false;
            });
            return r;
        }

        static bool SameSystems(string[] f, ulong[] sys)
        {
            if (f.Length < 2 + sys.Length) return false;
            for (int k = 0; k < sys.Length; k++) if (f[2 + k] != sys[k].ToString("X16")) return false;
            return true;
        }

        static string Culprit(string[] f, ulong[] sys)
        {
            var names = new List<string>();
            for (int k = 0; k < sys.Length && 2 + k < f.Length; k++) if (f[2 + k] != sys[k].ToString("X16")) names.Add(NetWorld.SystemNames[k]);
            return names.Count > 0 ? string.Join(", ", names) : "?";
        }

        // El tick que difiere no trae hash por sistema: se compara con el último bloque bueno no sirve (los sistemas cambian solos),
        // así que se calcula el estado de cada sistema ahora y se buscan, en el archivo, los sistemas del siguiente bloque: el
        // reproductor avanza una copia hasta el siguiente múltiplo de 300 para nombrar al culpable.
        static string CulpritNow(NetWorld w, long t, Dictionary<long, string[]> expected)
        {
            long next = (t / SystemEvery + 1) * SystemEvery;
            if (!expected.TryGetValue(next, out var f)) return "?";
            while (w.Tick < next) w.Step();
            var s = w.Systems();
            return Culprit(f, new[] { s.Control, s.Field, s.Trains, s.Stock, s.Edges });
        }

        /// <summary>Avanza «ticks» ticks aplicando el guion; en cada tick llama a «onTick» con el hash (y los sistemas cada 300 ticks).</summary>
        static void Play(NetWorld w, List<NetCommand> script, long ticks, Func<long, ulong, ulong[], bool> onTick)
        {
            foreach (var c in script) w.Push(c);
            long end = w.Tick + ticks;
            int stalled = 0;
            while (w.Tick < end)
            {
                long before = w.Tick;
                w.Step();
                if (w.Tick == before) { if (++stalled > 1000) throw new InvalidOperationException("La repetición se quedó en pausa: un guion con Pausa necesita su Reanudar en el mismo tick"); continue; }
                stalled = 0;
                ulong[] sys = null;
                if (w.Tick % SystemEvery == 0)
                {
                    var s = w.Systems();
                    sys = new[] { s.Control, s.Field, s.Trains, s.Stock, s.Edges };
                }
                if (!onTick(w.Tick, w.Hash(), sys)) return;
            }
        }

        /// <summary>Guion del escenario G3 (Sara): un día con x1 → x4, pausa y retención de trenes, a ticks fijos desde el arranque.</summary>
        public static List<NetCommand> ScriptG3(long startTick)
        {
            long T(double hours) => startTick + NetConst.Hours(hours);
            return new List<NetCommand>
            {
                new NetCommand { Tick = T(0.1), Type = NetCmdType.SetSpeed, Arg = 4 },
                new NetCommand { Tick = T(1.0), Type = NetCmdType.TrainHold, Arg = 1 },
                new NetCommand { Tick = T(2.5), Type = NetCmdType.TrainGo, Arg = 1 },
                new NetCommand { Tick = T(5.0), Type = NetCmdType.SetSpeed, Arg = 2 },
                new NetCommand { Tick = T(6.0), Type = NetCmdType.Pause },        // pausa y reanudar en el mismo tick (en una repetición el
                new NetCommand { Tick = T(6.0), Type = NetCmdType.Resume },       // tiempo no corre en pausa: sin Reanudar no avanzaría)
                new NetCommand { Tick = T(8.0), Type = NetCmdType.Materialize, Arg = 0 },
                new NetCommand { Tick = T(8.5), Type = NetCmdType.ConsumeAmmo, Arg = 0, Arg2 = 3 },
                new NetCommand { Tick = T(8.5), Type = NetCmdType.Dematerialize, Arg = 0, Arg2 = 0 },
                new NetCommand { Tick = T(12.0), Type = NetCmdType.SetSpeed, Arg = 1 },
                new NetCommand { Tick = T(20.0), Type = NetCmdType.TrainHold, Arg = 0 },
                new NetCommand { Tick = T(22.0), Type = NetCmdType.TrainGo, Arg = 0 },
            };
        }
    }
}
