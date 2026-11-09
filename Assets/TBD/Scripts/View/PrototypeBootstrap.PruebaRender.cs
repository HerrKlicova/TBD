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
    }
}
