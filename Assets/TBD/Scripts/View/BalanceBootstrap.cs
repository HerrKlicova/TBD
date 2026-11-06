using System.IO;
using TBD.Net;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Pone el lector de los CSV de balance antes que nada (SubsystemRegistration va antes que BeforeSceneLoad, donde nacen los mundos).
    /// Editor: lee Assets/TBD/Data directamente, así que un CSV cambiado se nota en la siguiente partida. Compilación: lee
    /// StreamingAssets/Data, que copia BalanceBuildCopy antes de compilar.
    /// </summary>
    public static class BalanceBootstrap
    {
        public static string Folder => Application.isEditor
            ? Path.Combine(Application.dataPath, "TBD", "Data")
            : Path.Combine(Application.streamingAssetsPath, "Data");

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void InitInEditMode() { Init(); }      // también fuera del modo Play (comandos y pruebas del Editor)
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            BalanceSource.Missing.Clear();
            BalanceSource.Warn = Debug.LogWarning;
            BalanceSource.Reader = file => { var p = Path.Combine(Folder, file); return File.Exists(p) ? File.ReadAllText(p) : null; };
        }
    }
}
