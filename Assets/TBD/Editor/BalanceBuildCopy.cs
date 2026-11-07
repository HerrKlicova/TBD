using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TBD.EditorTools
{
    /// <summary>Antes de cada compilación copia Assets/TBD/Data/*.csv a StreamingAssets/Data, que es de donde los lee el jugador (QA: un CSV de Hugo debe notarse en la build).</summary>
    public class BalanceBuildCopy : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string src = Path.Combine(Application.dataPath, "TBD", "Data");
            string dst = Path.Combine(Application.streamingAssetsPath, "Data");
            Directory.CreateDirectory(dst);
            int n = 0;
            foreach (var f in Directory.GetFiles(src, "*.csv")) { File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true); n++; }
            foreach (var f in Directory.GetFiles(src, "estacion_*.json")) { File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true); n++; }   // dato por estación (EstacionVista.Load)
            AssetDatabase.Refresh();
            Debug.Log("[TBD] " + n + " CSV de balance copiados a StreamingAssets/Data");
        }
    }
}
