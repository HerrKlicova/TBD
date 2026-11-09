using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace TBD.View
{
    /// <summary>
    /// Prueba de render (Lucía 09-11, después de qa48; Eugenio quiere decenas de miles de zombis en pantalla): -pruebarender N crea N zombis SOLO DE VISTA
    /// (entidades de Entities Graphics con la misma malla, materiales y paleta que la horda, sin componente Zombie: la simulación y su hash no los ven) en una
    /// rejilla de 1,2 m alrededor del foco, y registra cada 5 s una línea [RENDER] con fps medio, p1 del tiempo de fotograma, draw calls, batches y SetPass
    /// (los contadores de render solo existen en builds de desarrollo; si no, salen como -1). Úsese con -sinhorda para medir solo esto.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        static readonly int PruebaRenderN = LeerEntero("-pruebarender", 0);
        static int LeerEntero(string bandera, int porDefecto)
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, bandera);
            return i >= 0 && i + 1 < a.Length && int.TryParse(a[i + 1], out var v) ? v : porDefecto;
        }

        void PruebaRenderIniciar()
        {
            if (PruebaRenderN <= 0) return;
            StartCoroutine(PruebaRender());
        }

        System.Collections.IEnumerator PruebaRender()
        {
            while (!RenderAssets.Ready || _worldA == null || !_worldA.IsCreated) yield return null;
            var em = _worldA.EntityManager;
            var desc = new RenderMeshDescription(RenderAttachSystem.FigureMode ? ShadowCastingMode.On : ShadowCastingMode.Off, RenderAttachSystem.FigureMode);
            int lado = Mathf.CeilToInt(Mathf.Sqrt(PruebaRenderN)); const float paso = 1.2f;
            float x0 = _focus.x - lado * paso * 0.5f, z0 = _focus.z - lado * paso * 0.5f;
            var proto = em.CreateEntity();
            RenderMeshUtility.AddComponents(proto, em, desc, RenderAssets.Array, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            em.AddComponentData(proto, LocalTransform.Identity);
            em.AddComponentData(proto, new PostTransformMatrix { Value = float4x4.Scale(RenderAssets.FigureScale) });
            em.AddComponentData(proto, new ZombieBaseColor { Value = RenderAttachSystem.ColorFor(1) });
            em.AddComponentData(proto, new ZombieAtlasST { Value = RenderAttachSystem.AtlasST(1, 0.0) });
            if (RenderAssets.Horda) em.AddComponentData(proto, new ZombiePaleta { Value = HordaPaleta.Pack(1) });
            var ents = em.Instantiate(proto, PruebaRenderN, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                uint id = (uint)(1000000 + i); uint hv = math.hash(new uint2(id, 41u));
                float x = x0 + (i % lado) * paso + ((hv & 255u) / 255f - 0.5f) * 0.6f, z = z0 + (i / lado) * paso + (((hv >> 8) & 255u) / 255f - 0.5f) * 0.6f;
                em.SetComponentData(ents[i], LocalTransform.FromPositionRotation(new float3(x, 0.05f, z), quaternion.RotateY((hv % 360u) * math.PI / 180f)));
                em.SetComponentData(ents[i], MaterialMeshInfo.FromRenderMeshArrayIndices((int)(hv % (uint)RenderAssets.Variants), (int)((hv >> 8) % (uint)RenderAssets.MeshVariants)));
                em.SetComponentData(ents[i], new ZombieBaseColor { Value = RenderAttachSystem.ColorFor(id) });
                if (RenderAssets.Horda) em.SetComponentData(ents[i], new ZombiePaleta { Value = HordaPaleta.Pack(id) });
            }
            ents.Dispose(); em.DestroyEntity(proto);
            Debug.Log("[RENDER] prueba: " + PruebaRenderN + " zombis de vista en una rejilla de " + lado + " × " + lado + " (" + (lado * paso).ToString("0") + " m) alrededor de " + _focus.ToString("0"));

            var dc = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var bt = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            var sp = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            var tiempos = new System.Collections.Generic.List<float>();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            while (true)
            {
                tiempos.Clear(); float t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < 5f) { yield return null; tiempos.Add(Time.unscaledDeltaTime); }
                tiempos.Sort(); float suma = 0f; foreach (var t in tiempos) suma += t;
                float fps = tiempos.Count / Mathf.Max(0.001f, suma), peor = tiempos[Mathf.Min(tiempos.Count - 1, (int)(tiempos.Count * 0.99f))] * 1000f;
                Debug.Log("[RENDER] N=" + PruebaRenderN + " fps=" + fps.ToString("0.0", ic) + " p99ms=" + peor.ToString("0.0", ic)
                    + " drawcalls=" + (dc.Valid ? dc.LastValue : -1) + " batches=" + (bt.Valid ? bt.LastValue : -1) + " setpass=" + (sp.Valid ? sp.LastValue : -1));
            }
        }
        // ---- QA del suelo por trozos (Eugenio 09-11: «al mover la cámara se va cargando suelo»): -barrido V T mueve el foco de la cámara a V m/s
        // (por defecto 40) durante T s (por defecto 20) de ida y vuelta en X por el mapa, cuando ha terminado la precarga. Cada segundo escribe una línea
        // [BARRIDO] con el foco, los trozos cargados y pendientes y los «huecos»: trozos que tocan lo que se ve (aprox.: círculo de radio ortho×aspecto×1,2)
        // y aún no están. Al final, un resumen con el máximo de huecos y los segundos con algún hueco. Solo presentación: no toca la simulación.
        static readonly float BarridoV = LeerBarrido(0, 40f), BarridoT = LeerBarrido(1, 20f);
        static float LeerBarrido(int k, float porDefecto)
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-barrido");
            if (i < 0) return k == 0 ? 0f : porDefecto;
            return i + 1 + k < a.Length && float.TryParse(a[i + 1 + k], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0f ? v : porDefecto;
        }

        int HuecosSuelo()
        {
            if (_worldCam == null || _chunkRoot == null) return -1;
            float r = _worldCam.orthographicSize * Mathf.Max(1f, _worldCam.aspect) * 1.2f + ChunkM * 0.5f;
            int cols = (MapW + ChunkM - 1) / ChunkM, rows = (MapH + ChunkM - 1) / ChunkM, n = 0;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    float cx = MapX0 + i * ChunkM + ChunkM * 0.5f, cz = MapZ0 + j * ChunkM + ChunkM * 0.5f;
                    if ((cx - _focus.x) * (cx - _focus.x) + (cz - _focus.z) * (cz - _focus.z) < r * r && !_chunks.ContainsKey(ChunkKey(i, j))) n++;
                }
            return n;
        }

        void BarridoIniciar()
        {
            if (BarridoV <= 0f) return;
            StartCoroutine(Barrido());
        }

        float _msSubidaSuelo, _msSubidaSueloMax;                                 // ms de UpdateGroundStreaming subiendo trozos (los suma el streaming, los vacía el barrido)
        static readonly float BarridoCap = LeerFloat("-barridocap", 0f);       // -barridocap S: una captura cada S s durante el barrido (Captures/barrido_v<V>_t<t>.png)

        System.Collections.IEnumerator Barrido()
        {
            float t0 = Time.realtimeSinceStartup;
            while ((_chunkPrecarga || _chunkRoot == null) && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            yield return new WaitForSecondsRealtime(3f);
            // Limitado al mapa (Marcos, I-Noche92: a 80 m/s el foco acabó en Z −244): X de ida y vuelta y Z fija dentro del mapa en cada fotograma,
            // para que ni el desplazamiento por el borde de la pantalla ni el teclado lo saquen.
            float xMin = MapX0 + 60f, xMax = MapX0 + MapW - 60f, z = Mathf.Clamp(_focus.z, MapZ0 + 60f, MapZ0 + MapH - 60f), dir = 1f, t = 0f, tLog = 0f, tCap = 0f;
            int maxHuecos = 0, segConHuecos = 0, huecosSeg = 0; float peorMs = 0f, peorMsTotal = 0f, subidaMaxTotal = 0f;
            string capDir = null; if (BarridoCap > 0f) { capDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Captures")); System.IO.Directory.CreateDirectory(capDir); }
            _msSubidaSuelo = 0f; _msSubidaSueloMax = 0f;
            Debug.Log("[BARRIDO] empieza: " + BarridoV.ToString("0.#") + " m/s durante " + BarridoT.ToString("0.#") + " s desde " + _focus.ToString("0") + " (X " + xMin.ToString("0") + "…" + xMax.ToString("0") + ", Z fija " + z.ToString("0") + ")" + (capDir != null ? " · captura cada " + BarridoCap + " s (cuesta tiempo: no medir fps con capturas)" : ""));
            while (t < BarridoT)
            {
                float dt = Time.unscaledDeltaTime; t += dt; tLog += dt; tCap += dt;
                if (!_capturing && dt * 1000f > peorMs) peorMs = dt * 1000f;           // los fotogramas de captura no cuentan
                float x = Mathf.Clamp(_focus.x, xMin, xMax) + dir * BarridoV * dt;
                if (x > xMax) { x = xMax; dir = -1f; } else if (x < xMin) { x = xMin; dir = 1f; }
                _focus = new Vector3(x, 0f, z);
                int h = HuecosSuelo(); if (h > huecosSeg) huecosSeg = h;
                if (capDir != null && tCap >= BarridoCap && !_capturing) { tCap = 0f; StartCoroutine(CaptureRoutine(System.IO.Path.Combine(capDir, "barrido_v" + BarridoV.ToString("0") + "_t" + t.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture) + ".png"))); }
                if (tLog >= 1f)
                {
                    Debug.Log("[BARRIDO] t=" + t.ToString("0.0") + " foco=" + _focus.ToString("0") + " trozos=" + _chunks.Count + " pendientes=" + _chunkPending.Count + " huecos=" + huecosSeg + " fps=" + _fps.ToString("0")
                        + " peor=" + peorMs.ToString("0") + " ms subida=" + _msSubidaSuelo.ToString("0") + " ms (máx " + _msSubidaSueloMax.ToString("0.0") + " ms en un fotograma)");
                    if (huecosSeg > maxHuecos) maxHuecos = huecosSeg; if (huecosSeg > 0) segConHuecos++;
                    if (peorMs > peorMsTotal) peorMsTotal = peorMs; if (_msSubidaSueloMax > subidaMaxTotal) subidaMaxTotal = _msSubidaSueloMax;
                    huecosSeg = 0; tLog = 0f; peorMs = 0f; _msSubidaSuelo = 0f; _msSubidaSueloMax = 0f;
                }
                yield return null;
            }
            Debug.Log("[BARRIDO] fin: máximo de huecos a la vista " + maxHuecos + ", segundos con algún hueco " + segConHuecos + " de " + Mathf.RoundToInt(BarridoT) + ", trozos cargados " + _chunks.Count
                + ", peor fotograma " + peorMsTotal.ToString("0") + " ms, peor subida de trozos en un fotograma " + subidaMaxTotal.ToString("0.0") + " ms");
        }
    }
}
