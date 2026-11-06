using System;
using System.IO;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Repetición dorada de la red (criterio de Sara): idéntica al repetirla, igual que el archivo guardado y capaz de fallar.</summary>
    static class GoldenTests
    {
        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[13] Repetición dorada de la red (G3: un día del MVP)");
            var start = NetScenario.Mvp().Tick;
            var script = NetGolden.ScriptG3(start);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string a = NetGolden.Record(script, NetConst.DayTicks);
            sw.Stop();
            string b = NetGolden.Record(script, NetConst.DayTicks);
            check(a == b, $"grabar dos veces da el mismo archivo ({a.Length / 1024} KB, {sw.Elapsed.TotalMilliseconds:0} ms)");
            var r = NetGolden.Verify(a);
            check(r.Ok && r.TicksChecked == NetConst.DayTicks, "reproducir en el mismo proceso: " + r);

            // criterio 4 (sesiones distintas): el archivo guardado en el repositorio se reproduce igual en cualquier sesión
            string dir = Path.Combine(BalanceTests.DataDirPublic(), "..", "Golden");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "G3_red.golden");
            bool created = false;
            if (!File.Exists(file)) { File.WriteAllText(file, a); created = true; }
            var rf = NetGolden.Verify(File.ReadAllText(file));
            check(rf.Ok, $"el archivo guardado (Assets/TBD/Golden/G3_red.golden) se reproduce igual: {rf}" + (created ? " (recién creado)" : ""));

            // criterio 5: el reproductor sabe fallar. Un bit en la mancha en el tick 8.400 ...
            long badTick = start + 8400;                 // 8.400 ticks después del arranque
            var rb = NetGolden.Verify(a, (w, t) => { if (t == badTick) w.Field.D[50 * HordeField.W + 40] ^= 1; });
            // el bit se cambia al final del tick «badTick»: el primer estado que lo contiene es el del tick siguiente
            check(!rb.Ok && rb.FirstBadTick == badTick + 1 && rb.System.Contains("mancha"), "un bit cambiado en la mancha se detecta en el tick siguiente y con el sistema culpable: " + rb);
            // ... y una orden cambiada en el archivo
            string altered = a.Replace($"{script[1].Tick};{(int)NetCmdType.TrainHold};1;0", $"{script[1].Tick};{(int)NetCmdType.TrainHold};0;0");
            var ra = NetGolden.Verify(altered);
            check(altered != a && !ra.Ok && ra.FirstBadTick >= script[1].Tick, "una orden cambiada en el archivo se detecta: " + ra);
            check(a.Length < 5 * 1024 * 1024, $"el archivo pesa menos de 5 MB ({a.Length / 1024} KB sin comprimir)");
        }
    }
}
