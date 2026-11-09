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
            AsegurarVariantes();
        }

        /// <summary>
        /// Bruno, 09-11 (qa47, sombra del tren): los materiales del juego se crean en tiempo de ejecución y activan palabras clave (_ALPHATEST_ON, _EMISSION). Son
        /// «shader_feature»: Unity solo compila esas variantes si algún material de la compilación las usa, y el proyecto no tenía ningún .mat. Sin la variante, el recorte
        /// por alfa no existe en la build: los téxeles transparentes de la vía en el suelo por trozos (y los de los decals) salían NEGROS y opacos (090a10, solo el reflejo
        /// del cielo). Era la «sombra del tren» que no cambiaba con la luna, el ambiente ni la sonda (I-Noche80 a 86). Estos materiales en Resources fuerzan las variantes.
        /// </summary>
        static void AsegurarVariantes()
        {
            const string dir = "Assets/TBD/Resources/Variantes";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/TBD/Resources", "Variantes");
            int n = 0;
            n += Variante(dir + "/V_Lit_Recorte.mat", "Universal Render Pipeline/Lit", m => { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); });
            n += Variante(dir + "/V_Lit_Emision.mat", "Universal Render Pipeline/Lit", m => { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.02f, 0.02f, 0.02f)); });
            n += Variante(dir + "/V_Lit_Recorte_Emision.mat", "Universal Render Pipeline/Lit", m => { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.02f, 0.02f, 0.02f)); });
            n += Variante(dir + "/V_Unlit_Recorte.mat", "Universal Render Pipeline/Unlit", m => { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); });
            if (n > 0) { AssetDatabase.SaveAssets(); Debug.Log("[TBD] " + n + " materiales de variantes creados en " + dir + " (recorte por alfa y emisión en la build)"); }
        }

        static int Variante(string path, string shader, System.Action<Material> conf)
        {
            // I-Noche90: con _EmissionColor en negro, la validación de URP quita _EMISSION del .mat al importarlo y la variante de emisión no entraba en la build
            // (el ambiente extra del suelo no se veía). Ahora el color es gris oscuro y, si el .mat ya existe, se vuelve a configurar y se guarda.
            var ya = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (ya != null)
            {
                conf(ya); EditorUtility.SetDirty(ya);
                Debug.Log("[TBD] Variante " + path + ": " + string.Join(" ", ya.shaderKeywords));
                return 1;
            }
            var sh = Shader.Find(shader); if (sh == null) { Debug.LogWarning("[TBD] Shader no encontrado para la variante: " + shader); return 0; }
            var m = new Material(sh); conf(m);
            AssetDatabase.CreateAsset(m, path);
            return 1;
        }
    }
}
