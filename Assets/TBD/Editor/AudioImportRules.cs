#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TBD.EditorTools
{
    /// <summary>
    /// Reglas de importación del audio (Nuria, 7-nov, arreglo de la Noche46). Va en Assets/TBD/Editor/.
    /// Se aplica sola a todo lo que entre en Resources/Audio. Para los 462 archivos que ya están: menú TBD > Audio > Reimportar con las reglas.
    /// - normalize DESACTIVADO: la mezcla por distancia ya viene en los archivos (D-N134). Con normalize todo sonaba al máximo.
    /// - preload ACTIVADO: el primer disparo no se queda esperando a que cargue el clip.
    /// - Efectos sueltos: mono (suenan en 3D), Decompress On Load, Vorbis 0,7.
    /// - Bucles, ambientes y música: Compressed In Memory, Vorbis 0,6, estéreo.
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        static readonly string[] LoopIds = {
            "peloton_", "horda_cerca", "horda_media", "horda_lejos", "batalla_mapa", "zombi_golpe_muro", "tren_ralenti",
            "vamtac_motor", "dragon_motor", "nh90_rotor", "rotor_nh90", "amb_", "fuego_crepitar", "mus_" };

        static bool IsLoop(string path) { string f = System.IO.Path.GetFileNameWithoutExtension(path); foreach (var id in LoopIds) if (f.Contains(id)) return true; return false; }

        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Audio/")) return;
            var imp = (AudioImporter)assetImporter; bool loop = IsLoop(assetPath);
            imp.forceToMono = !loop; imp.loadInBackground = loop;
            var s = imp.defaultSampleSettings;
            s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = loop ? 0.6f : 0.7f;
            s.preloadAudioData = true; s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            imp.defaultSampleSettings = s;
            var so = new SerializedObject(imp); var n = so.FindProperty("normalize") ?? so.FindProperty("m_Normalize"); if (n != null) { n.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        [MenuItem("TBD/Audio/Reimportar con las reglas")]
        static void ReimportAll()
        {
            foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/TBD/Resources/Audio" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
            Debug.Log("[Audio] Reimportado con las reglas de Nuria (sin normalize, mono en efectos, preload).");
        }
    }
}
#endif
