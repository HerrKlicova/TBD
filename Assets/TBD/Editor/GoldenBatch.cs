using System;
using System.IO;
using System.Text;
using TBD.Net;
using TBD.Sim;
using UnityEditor;
using UnityEngine;

namespace TBD.EditorTools
{
    /// <summary>
    /// Repetición dorada sin ventana en el Editor (para Sara y para probar de un golpe):
    ///   Unity.exe -batchmode -nographics -projectPath C:\Dev\TBD -executeMethod TBD.EditorTools.GoldenBatch.Run -quit -logFile -
    /// Verifica G1 a G5 de Assets/TBD/Golden, escribe el resultado en Captures/golden_editor.txt y sale con código 0 si todo es idéntico y 1 si algo difiere.
    /// También sale por el menú TBD > Repetición dorada (todas).
    /// </summary>
    public static class GoldenBatch
    {
        [MenuItem("TBD/Repetición dorada (todas)")]
        public static void RunFromMenu() { Debug.Log("[TBD] " + RunAll(out _)); }

        public static void Run()
        {
            string text = RunAll(out bool ok);
            Debug.Log("[TBD] " + text);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static string RunAll(out bool allOk)
        {
            allOk = true;
            var sb = new StringBuilder();
            sb.Append("Repetición dorada en el Editor · ").Append(Application.unityVersion).Append('\n');
            string dir = Path.Combine(Application.dataPath, "TBD", "Golden");
            foreach (var name in new[] { "G3_red.golden", "G1_zona.golden", "G2_tropas.golden", "G4_bucle.golden", "G5_desmonte.golden" })
            {
                try
                {
                    var text = File.ReadAllText(Path.Combine(dir, name));
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    bool ok; string res;
                    if (text.StartsWith(NetGolden.Format)) { var r = NetGolden.Verify(text); res = r.ToString(); ok = r.Ok; }
                    else if (text.StartsWith(LoopGolden.Format)) { var r = LoopGolden.Verify(text); res = r.ToString(); ok = r.Ok; }
                    else { var r = ZoneGolden.Verify(text); res = r.ToString(); ok = r.Ok; }
                    allOk &= ok;
                    sb.Append(name).Append(": ").Append(res).Append(" · ").Append(sw.ElapsedMilliseconds).Append(" ms\n");
                }
                catch (Exception e) { allOk = false; sb.Append(name).Append(": ERROR ").Append(e.Message).Append('\n'); }
            }
            sb.Append(allOk ? "RESULTADO: IDÉNTICA\n" : "RESULTADO: DIFIERE\n");
            try
            {
                string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "golden_editor.txt"), sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[TBD] No se pudo escribir el resultado: " + e.Message); }
            return sb.ToString();
        }
    }
}
