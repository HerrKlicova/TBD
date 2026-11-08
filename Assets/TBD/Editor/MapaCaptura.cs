using System.IO;
using System.Reflection;
using TBD.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TBD.EditorTools
{
    /// <summary>
    /// Capturas del mapa de operaciones con el plano táctico de Bruno, sin abrir ventanas:
    /// Unity.exe -batchmode -projectPath C:\Dev\TBD-2 -executeMethod TBD.EditorTools.MapaCaptura.Capturar -quit
    /// Monta OperationsMap3D en una escena vacía (llama a Start por reflexión), pone la cámara a 2500 m sobre Nules y de lejos sobre la costa, y deja
    /// Captures/mapa/mapa_2500m_nules.png y mapa_lejos.png. También mide lo que tarda en rehacerse el parche de detalle (UltimoParcheMs).
    /// </summary>
    public static class MapaCaptura
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static void Capturar()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Mapa");
            var m = go.AddComponent<OperationsMap3D>();
            // lo que hace Start, sin NetSession (usa DontDestroyOnLoad, que no vale fuera del modo de juego): la red de partida en las dos copias
            var a = TBD.Net.NetScenario.Inicio(); var b = TBD.Net.NetScenario.Inicio();
            var t = typeof(OperationsMap3D);
            t.GetField("_a", F).SetValue(m, a); t.GetField("_b", F).SetValue(m, b);
            t.GetField("_relief", F).SetValue(m, new TBD.Net.PlaceholderRelief(a.Graph));
            Call(m, "SetupCamera"); Call(m, "BuildTerrain"); Call(m, "BuildNetwork");
            string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Captures", "mapa"); Directory.CreateDirectory(outDir);
            var g = a.Graph;
            var nules = g.Nodes.Find(n => n.Name == "Nules");
            Toma(m, OperationsMap3D.MapToWorldFlat(nules.XMeters, nules.YMeters), 2.5f, Path.Combine(outDir, "mapa_2500m_nules.png"));
            Debug.Log("[MAPA] parche de detalle: " + OperationsMap3D.UltimoParcheMs.ToString("0") + " ms");
            var sag = g.Nodes.Find(n => n.Name == "Sagunto");
            Toma(m, OperationsMap3D.MapToWorldFlat(sag.XMeters, sag.YMeters), 8f, Path.Combine(outDir, "mapa_8km_sagunto.png"));
            Debug.Log("[MAPA] parche de detalle (Sagunto): " + OperationsMap3D.UltimoParcheMs.ToString("0") + " ms");
            Toma(m, OperationsMap3D.MapToWorldFlat(sag.XMeters, sag.YMeters - 4), 230f, Path.Combine(outDir, "mapa_lejos.png"));
            Debug.Log("[MAPA] capturas en " + outDir);
        }

        static void Toma(OperationsMap3D m, Vector3 foco, float dist, string path)
        {
            m.Foco = foco; m.DistanciaObjetivo = dist;
            typeof(OperationsMap3D).GetField("_dist", F).SetValue(m, dist);
            Call(m, "UpdateNetwork"); Call(m, "UpdateCamera"); Call(m, "UpdateDetail");
            if (dist < 14f) typeof(OperationsMap3D).GetMethod("BuildDetail", F).Invoke(m, new object[] { new Vector3(foco.x, 0f, foco.z) });     // el parche, en el acto (en el juego va en segundo plano)
            Call(m, "UpdateCamera");
            var cam = (Camera)typeof(OperationsMap3D).GetField("_cam", F).GetValue(m);
            var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null; RenderTexture.active = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }

        static void Call(object o, string name)
        {
            var mi = o.GetType().GetMethod(name, F);
            if (mi == null) { Debug.LogError("[MAPA] no encuentro " + name); return; }
            mi.Invoke(o, null);
        }
    }
}
