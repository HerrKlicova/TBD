using System.IO;
using TBD.Net;
using TBD.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TBD.EditorTools
{
    /// <summary>
    /// Escena de prueba de una estación montada con EstacionVista y capturas para la galería (Iker, 7-nov). Solo para el Editor: no forma parte
    /// del juego ni carga nada en la partida. En modo batch:
    ///   Unity.exe -batchmode -projectPath C:\Dev\TBD-2 -executeMethod TBD.EditorTools.EstacionCaptura.CapturarCastellon -quit
    /// deja las capturas en Captures/estaciones/ y la escena en Assets/Scenes/Castellon.unity.
    /// </summary>
    public static class EstacionCaptura
    {
        const string ScenePath = "Assets/Scenes/Castellon.unity";

        [MenuItem("TBD/Estaciones/Castellón: escena de prueba y capturas")]
        public static void CapturarCastellon() { Capturar("castellon"); }

        public static void Capturar(string id)
        {
            string data = Path.Combine(Application.dataPath, "TBD", "Data");
            BalanceSource.Reader = f => { var p = Path.Combine(data, f); return File.Exists(p) ? File.ReadAllText(p) : null; };
            var dato = EstacionVista.Load(id);
            if (dato == null) { Debug.LogError("[Estaciones] falta estacion_" + id + ".json"); return; }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sol").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            var root = new GameObject("Estación " + dato.Nombre).transform;
            var res = EstacionVista.Build(root, dato);
            EstacionVista.SetTankLoad(res, 150, 300);
            var radar = EstacionVista.SetRadar(res, dato, EstacionVista.RadarEstado.Segura);
            if (radar != null) { var b = new Bounds(radar.transform.position, Vector3.zero); foreach (var mr in radar.GetComponentsInChildren<Renderer>()) b.Encapsulate(mr.bounds); Debug.Log("[Estaciones] radar: raíz " + radar.transform.position + ", caja " + b.center + " ± " + b.extents + ", hijos " + radar.transform.childCount); }
            Debug.Log("[Estaciones] " + dato + ": " + res.Placed + " piezas, " + res.Tanks.Count + " cisternas, sin modelo: " + (res.Missing.Count == 0 ? "ninguna" : string.Join("; ", res.Missing)));

            var cam = new GameObject("Cámara").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.68f, 0.72f);
            cam.farClipPlane = 2000f;
            string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Captures", "estaciones"); Directory.CreateDirectory(outDir);
            Shot(cam, new Vector3(128f, 190f, -40f), new Vector3(128f, 0f, 60f), 45f, Path.Combine(outDir, id + "_general.png"));
            Shot(cam, new Vector3(60f, 45f, 20f), new Vector3(130f, 0f, 70f), 50f, Path.Combine(outDir, id + "_estacion.png"));
            Shot(cam, new Vector3(150f, 30f, -25f), new Vector3(180f, 0f, 22f), 50f, Path.Combine(outDir, id + "_deposito.png"));
            Shot(cam, new Vector3(225f, 14f, 105f), new Vector3(206f, 2f, 82f), 45f, Path.Combine(outDir, id + "_radar.png"));
            Object.DestroyImmediate(cam.gameObject);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ScenePath)));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Estaciones] capturas en " + outDir + " y escena en " + ScenePath);
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
