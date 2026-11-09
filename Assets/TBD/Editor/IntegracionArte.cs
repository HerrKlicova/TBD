using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TBD.EditorTools
{
    /// <summary>
    /// Pasos de Editor de las ramas de arte de Bruno para la Noche71 (Iker, 8-nov), en batch:
    ///   Unity.exe -batchmode -nographics -projectPath C:\Dev\TBD-2 -executeMethod TBD.EditorTools.IntegracionArte.Preparar -quit
    /// - Zombis D-N80 (Models/ZombiDN80) y pila (Models/PilaZombis): Mesh Compression Off, color de vértice importado sin tocar el alfa, sin materiales ni colliders.
    /// - Vías (Resources/Vias): Point, sin mipmaps, Repeat.
    /// - TBD/Horda, TBD/Fantasma y TBD/FantasmaContorno en Always Included Shaders.
    /// - Soldados armados: comprueba que Muzzle_0/Muzzle_1 siguen en el modelo importado (que el Avatar no los filtra) y que los vehículos traen Muzzle_0.
    /// </summary>
    public static class IntegracionArte
    {
        public static void Preparar()
        {
            int n = 0;
            foreach (var dir in new[] { "Assets/TBD/Resources/Models/ZombiDN80", "Assets/TBD/Resources/Models/PilaZombis" })
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { dir }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                    mi.meshCompression = ModelImporterMeshCompression.Off;
                    mi.materialImportMode = ModelImporterMaterialImportMode.None;
                    mi.addCollider = false;
                    mi.importAnimation = false;
                    mi.SaveAndReimport(); n++;
                }
            Debug.Log($"[ARTE] {n} FBX de zombis y pila: sin compresión, con color de vértice, sin materiales ni colliders");

            int t = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/TBD/Resources/Vias" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Repeat; ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport(); t++;
            }
            Debug.Log($"[ARTE] {t} texturas de vía: Point, sin mipmaps, Repeat");

            // Always Included Shaders
            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs); var arr = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var nombre in new[] { "TBD/Horda", "TBD/Fantasma", "TBD/FantasmaContorno" })
            {
                var sh = Shader.Find(nombre);
                if (sh == null) { Debug.LogWarning("[ARTE] no encuentro el shader " + nombre); continue; }
                bool esta = false; for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) esta = true;
                if (esta) { Debug.Log("[ARTE] " + nombre + " ya estaba en Always Included"); continue; }
                arr.InsertArrayElementAtIndex(arr.arraySize); arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                Debug.Log("[ARTE] " + nombre + " añadido a Always Included");
            }
            so.ApplyModifiedProperties(); AssetDatabase.SaveAssets();

            // bocas de arma (Teo 1e8c0d5)
            var faltan = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("SM_SoldadoArmado t:Model", new[] { "Assets/TBD/Resources/Models/Infanteria" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (go == null) continue;
                var nombres = go.GetComponentsInChildren<Transform>(true).Select(x => x.name).ToList();
                bool m0 = nombres.Contains("Muzzle_0"), m1 = nombres.Contains("Muzzle_1");
                bool ag36 = path.Contains("AG36");
                if (!m0 || (ag36 && !m1)) faltan.Add(System.IO.Path.GetFileName(path) + (m0 ? "" : " sin Muzzle_0") + (ag36 && !m1 ? " sin Muzzle_1" : ""));
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                Debug.Log($"[ARTE] {System.IO.Path.GetFileName(path)}: Muzzle_0 {(m0 ? "sí" : "NO")}, Muzzle_1 {(m1 ? "sí" : "no")}, rig {mi?.animationType}, optimizar jerarquía {mi?.optimizeGameObjects}");
            }
            foreach (var p in new[] { "Assets/TBD/Resources/Models/Vehiculos/dragon/SM_VCR_Dragon_Animable_LOD1.fbx", "Assets/TBD/Resources/Models/Vehiculos/vamtac/SM_Vamtac_Animable_LOD1.fbx", "Assets/TBD/Resources/Models/Vehiculos/nh90/SM_NH90_LOD1.fbx" })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p); if (go == null) { Debug.LogWarning("[ARTE] falta " + p); continue; }
                var nombres = go.GetComponentsInChildren<Transform>(true).Select(x => x.name).ToList();
                bool m0 = nombres.Contains("Muzzle_0"); if (!m0) faltan.Add(System.IO.Path.GetFileName(p) + " sin Muzzle_0");
                Debug.Log($"[ARTE] {System.IO.Path.GetFileName(p)}: Muzzle_0 {(m0 ? "sí" : "NO")}, Muzzle_1 {(nombres.Contains("Muzzle_1") ? "sí" : "no")}");
            }
            Debug.Log(faltan.Count == 0 ? "[ARTE] bocas de arma: todas en su sitio (el Avatar no las filtra)" : "[ARTE] bocas de arma que faltan: " + string.Join("; ", faltan));
        }
    }
}
