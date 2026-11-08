using System.IO;
using TBD.Net;
using TBD.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TBD.EditorTools
{
    /// <summary>
    /// Escena de la zona mínima de Bétera y capturas por cámara a una textura (sin ventana). En batch:
    ///   Unity.exe -batchmode -projectPath C:\Dev\TBD-2 -executeMethod TBD.EditorTools.AerodromoCaptura.Construir -quit -logFile Logs\betera.log
    /// Deja la escena en Assets/Scenes/Betera.unity y las capturas en Captures/betera/ (carpeta ignorada).
    /// </summary>
    public static class AerodromoCaptura
    {
        const string ScenePath = "Assets/Scenes/Betera.unity";

        [MenuItem("TBD/Aeródromo/Bétera: escena y capturas")]
        public static void Construir()
        {
            string data = Path.Combine(Application.dataPath, "TBD", "Data");
            BalanceSource.Reader = f => { var p = Path.Combine(data, f); return File.Exists(p) ? File.ReadAllText(p) : null; };
            var nivel = AerodromoNivel.FromJson(BalanceSource.Read("estacion_betera.json"));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sol").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            var root = new GameObject("Bétera · Base «Jaime I»").transform;
            var res = AerodromoZona.Build(root, nivel);
            Debug.Log($"[AERO] Bétera: {res.Placed} piezas ({res.Faldon} en el faldón), {res.Claros.Count} claros, sin modelo: {(res.Missing.Count == 0 ? "ninguna" : string.Join("; ", res.Missing))}");
            var cam = new GameObject("Cámara").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.68f, 0.72f); cam.farClipPlane = 3000f;
            string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Captures", "betera"); Directory.CreateDirectory(outDir);
            Shot(cam, new Vector3(128f, 230f, -120f), new Vector3(128f, 0f, 60f), 50f, Path.Combine(outDir, "betera_general.png"));
            Shot(cam, new Vector3(125f, 40f, -10f), new Vector3(125f, 4f, 45f), 55f, Path.Combine(outDir, "betera_hangares.png"));
            Shot(cam, new Vector3(128f, 30f, 60f), new Vector3(128f, 0f, 104f), 55f, Path.Combine(outDir, "betera_losa_p1.png"));
            Object.DestroyImmediate(cam.gameObject);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[AERO] capturas en " + outDir + " y escena en " + ScenePath);
        }

        static void Shot(Camera cam, Vector3 from, Vector3 at, float fov, string path)
        {
            cam.transform.position = from; cam.transform.LookAt(at); cam.fieldOfView = fov;
            var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null; RenderTexture.active = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }
    }
}
