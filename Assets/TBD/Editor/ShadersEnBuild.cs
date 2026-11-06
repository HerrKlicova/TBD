using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace TBD.EditorTools
{
    /// <summary>
    /// El juego crea materiales con Shader.Find (URP Lit y Unlit, el aditivo de los efectos, el cuantizador de paleta...). En una compilación Unity
    /// descarta los shaders que ninguna escena referencia, y Shader.Find devuelve null (la primera compilación con ventana se cerraba al arrancar con
    /// «Parameter name: shader»). Antes de compilar, se añaden a «Always Included Shaders».
    /// </summary>
    public class ShadersEnBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10;

        static readonly string[] Nombres =
        {
            "Hidden/TBD/PaletteQuantize", "Sprites/Default", "TBD/FxAdditive", "Universal Render Pipeline/Lit", "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Unlit", "Unlit/Texture",
        };

        public void OnPreprocessBuild(BuildReport report) { Asegurar(); }

        [MenuItem("TBD/Incluir shaders en la compilación")]
        public static void Asegurar()
        {
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            int added = 0;
            foreach (var n in Nombres)
            {
                var sh = Shader.Find(n);
                if (sh == null) { Debug.LogWarning("[TBD] Shader no encontrado para la compilación: " + n); continue; }
                bool has = false;
                for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { has = true; break; }
                if (has) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                added++;
            }
            if (added > 0) { so.ApplyModifiedProperties(); AssetDatabase.SaveAssets(); Debug.Log("[TBD] " + added + " shaders añadidos a Always Included Shaders"); }
        }
    }
}
