using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TBD.EditorTools
{
    /// <summary>
    /// Compilación de prueba para la corrida automática del tutorial y de la intro (Iker, 8-nov). No sustituye a las builds de Marcos: sale en
    /// Builds/iker-tutorial/ (carpeta ignorada) con las escenas de EditorBuildSettings. En batch:
    ///   Unity.exe -batchmode -projectPath C:\Dev\TBD-2 -executeMethod TBD.EditorTools.TutorialBuild.Construir -quit -logFile Logs\tutorial_build.log
    /// Corrida: Builds\iker-tutorial\TBD.exe -menu-auto -tutorial-auto -intro -screen-width 1920 -screen-height 1080 -logFile tutorial_run.log
    /// (capturas en Builds/iker-tutorial/TBD_Data/../Captures/tutorial/).
    /// </summary>
    public static class TutorialBuild
    {
        public static void Construir()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var opts = new BuildPlayerOptions { scenes = scenes, locationPathName = "Builds/iker-tutorial/TBD.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None };
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[TUTORIAL] build: {r.summary.result}, {r.summary.totalErrors} errores, {r.summary.totalTime.TotalSeconds:0} s");
            if (r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>
        /// Build de integración desde la rama de Iker (encargo del coordinador, 8-nov): -noche N la saca en Builds/NocheN/TBD.exe de esta copia (C:\Dev\TBD-2),
        /// con las mismas escenas que las de Marcos. Unity.exe -batchmode -nographics -projectPath C:\Dev\TBD-2 -executeMethod TBD.EditorTools.TutorialBuild.ConstruirNoche -noche 69 -quit
        /// </summary>
        public static void ConstruirNoche()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-noche");
            string n = i >= 0 && i + 1 < a.Length ? a[i + 1] : "X";
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var opts = new BuildPlayerOptions { scenes = scenes, locationPathName = "Builds/Noche" + n + "/TBD.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None };
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[BUILD] Noche{n}: {r.summary.result}, {r.summary.totalErrors} errores, {r.summary.totalTime.TotalSeconds:0} s, {r.summary.totalSize / 1048576} MB");
            if (r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
