using UnityEditor;
using UnityEngine;

namespace TBD.EditorTools
{
    /// <summary>
    /// Los iconos de Assets/TBD/Resources/Icons son pixel art de 16 y 32 px (paleta de 32, alfa binaria): se importan con filtro de punto,
    /// sin compresión ni mipmaps, para que la interfaz los dibuje nítidos a cualquier escala entera.
    /// </summary>
    public class IconImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/TBD/Resources/Icons/") && !assetPath.StartsWith("Assets/TBD/Resources/Models/") && !assetPath.StartsWith("Assets/TBD/Resources/Suelo/")) return;     // los modelos de Teo llevan una paleta de 32x16 (punto, sin compresión)
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.isReadable = assetPath.Contains("/Resources/Suelo/");        // el suelo de Sagunto se compone en CPU a partir de estas texturas
            ti.wrapMode = assetPath.Contains("/texturas/") || assetPath.Contains("/Resources/Suelo/suelo_") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;   // las texturas de Bruno son tileables (Repeat)
        }
    }
}
