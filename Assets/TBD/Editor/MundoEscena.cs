using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TBD.EditorTools
{
    /// <summary>Núcleo v2, Hito 1 (Marcos): crea la escena «Mundo» con el prototipo de zum y la añade a la compilación. En batch:
    /// Unity.exe -batchmode -projectPath C:\Dev\TBD -executeMethod TBD.EditorTools.MundoEscena.CrearYCompilar -quit -logFile -</summary>
    public static class MundoEscena
    {
        public const string Ruta = "Assets/Scenes/Mundo.unity";

        [MenuItem("TBD/Mundo: crear escena")]
        public static void Crear()
        {
            var esc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Mundo").AddComponent<TBD.Mundo.MundoBootstrap>();
            EditorSceneManager.SaveScene(esc, Ruta);
            var lista = EditorBuildSettings.scenes.ToList();
            if (!lista.Any(s => s.path == Ruta)) { lista.Add(new EditorBuildSettingsScene(Ruta, true)); EditorBuildSettings.scenes = lista.ToArray(); }
            Debug.Log("[MUNDO] escena creada: " + Ruta);
        }

        public static void CrearYCompilar()
        {
            Crear();
            var escenas = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            string destino = System.Environment.GetEnvironmentVariable("TBD_MUNDO_BUILD") ?? "C:/Dev/TBD/Builds/Mundo1/TBD.exe";
            var r = BuildPipeline.BuildPlayer(escenas, destino, BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log("[MUNDO] compilación: " + r.summary.result + " · errores " + r.summary.totalErrors + " · " + destino);
            EditorApplication.Exit(r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
