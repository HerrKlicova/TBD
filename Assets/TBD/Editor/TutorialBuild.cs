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
    }
}
