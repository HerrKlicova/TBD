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
            if (!assetPath.StartsWith("Assets/TBD/Resources/Icons/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.isReadable = false;
            ti.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
