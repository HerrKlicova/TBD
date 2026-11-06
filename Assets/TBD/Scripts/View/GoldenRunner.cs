using System;
using System.IO;
using System.Text;
using TBD.Net;
using TBD.Sim;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Modo sin ventana de la repetición dorada (D4 de Sara): si el ejecutable se lanza con «-tbd-golden», reproduce G1, G2 y G3
    /// desde StreamingAssets/Golden, escribe el resultado en «-tbd-golden-out ruta» (o en persistentDataPath/golden_result.txt) y
    /// sale con código 0 si todo es idéntico y 1 si algo difiere. Así se compara el Editor con una compilación Mono o IL2CPP.
    ///   TBD.exe -batchmode -nographics -tbd-golden -tbd-golden-out C:\ruta\resultado.txt
    /// </summary>
    public static class GoldenRunner
    {
        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            int di = Array.IndexOf(args, "-tbd-golden-diag");
            if (di >= 0 && di + 1 < args.Length)
            {
                Active = true;
                File.WriteAllText(args[di + 1], ScriptingBackend() + "\n" + Diag());
                if (!Application.isEditor) Application.Quit(0);
                return;
            }
            if (Array.IndexOf(args, "-tbd-golden") < 0) return;
            Active = true;
            string outPath = Path.Combine(Application.persistentDataPath, "golden_result.txt");
            int oi = Array.IndexOf(args, "-tbd-golden-out");
            if (oi >= 0 && oi + 1 < args.Length) outPath = args[oi + 1];

            var sb = new StringBuilder();
            bool allOk = true;
            sb.Append("Repetición dorada · ").Append(Application.isEditor ? "Editor" : "compilación").Append(" · ")
              .Append(Application.unityVersion).Append(" · backend ").Append(ScriptingBackend()).Append('\n');
            string dir = Path.Combine(Application.streamingAssetsPath, "Golden");
            foreach (var name in new[] { "G3_red.golden", "G1_zona.golden", "G2_tropas.golden" })
            {
                string path = Path.Combine(dir, name);
                try
                {
                    var text = File.ReadAllText(path);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    string res;
                    bool ok;
                    if (text.StartsWith(NetGolden.Format)) { var r = NetGolden.Verify(text); res = r.ToString(); ok = r.Ok; }
                    else { var r = ZoneGolden.Verify(text); res = r.ToString(); ok = r.Ok; }
                    allOk &= ok;
                    sb.Append(name).Append(": ").Append(res).Append(" · ").Append(sw.ElapsedMilliseconds).Append(" ms\n");
                }
                catch (Exception e) { allOk = false; sb.Append(name).Append(": ERROR ").Append(e.Message).Append('\n'); }
            }
            sb.Append(allOk ? "RESULTADO: IDÉNTICA\n" : "RESULTADO: DIFIERE\n");
            try { File.WriteAllText(outPath, sb.ToString()); } catch (Exception e) { Debug.LogError("[TBD] No se pudo escribir " + outPath + ": " + e.Message); }
            Debug.Log("[TBD] " + sb);
            if (!Application.isEditor) Application.Quit(allOk ? 0 : 1);
        }

        /// <summary>Diagnóstico de determinismo: bits de los primeros zombis de G1 recién creados y de cálculos de coma flotante sueltos.</summary>
        public static string Diag()
        {
            var sb = new StringBuilder();
            var rng = new Unity.Mathematics.Random(12345u);
            for (int k = 0; k < 3; k++) { float a = rng.NextFloat(4f, 60f); sb.Append("rng ").Append(Unity.Mathematics.math.asuint(a).ToString("X8")).Append('\n'); }
            float x = 1.2f, y = 0.37f, z = 0.8f;
            float fma = x * y + z;
            sb.Append("x*y+z ").Append(Unity.Mathematics.math.asuint(fma).ToString("X8")).Append('\n');
            var w = new Unity.Entities.World("TBD_Diag");
            try
            {
                SimWorldSetup.CreateSystems(w);
                var root = SimWorldSetup.Populate(w.EntityManager, 10000, false, 12345u, CombatParams.Default, 0, true);
                var q = w.EntityManager.CreateEntityQuery(typeof(Zombie));
                var zs = q.ToComponentDataArray<Zombie>(Unity.Collections.Allocator.Temp);
                for (int i = 0; i < 4 && i < zs.Length; i++)
                {
                    var zz = zs[i];
                    sb.Append("z").Append(zz.SimId).Append(" pos ").Append(Unity.Mathematics.math.asuint(zz.Pos.x).ToString("X8")).Append(' ')
                      .Append(Unity.Mathematics.math.asuint(zz.Pos.z).ToString("X8")).Append(" vel ").Append(Unity.Mathematics.math.asuint(zz.Speed).ToString("X8"))
                      .Append(" rumbo ").Append(Unity.Mathematics.math.asuint(zz.Heading).ToString("X8")).Append('\n');
                }
                ulong sum = 0; for (int i = 0; i < zs.Length; i++) sum += HashMath.OfZombie(zs[i]);
                sb.Append("hash zombis ").Append(sum.ToString("X16")).Append('\n');
                zs.Dispose();
            }
            finally { w.Dispose(); }
            return sb.ToString();
        }

        static string ScriptingBackend()
        {
#if ENABLE_IL2CPP
            return "IL2CPP";
#else
            return "Mono";
#endif
        }
    }
}
