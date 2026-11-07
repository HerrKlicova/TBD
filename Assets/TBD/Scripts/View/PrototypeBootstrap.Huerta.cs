using TBD.Sim;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Prueba de FPS de la huerta (Teo, 7-nov; plan del día: «comprobar los FPS de la huerta con Marcos»). Solo se usa con
    /// TBD.exe -huertafps (lo conduce HuertaFpsProbe). Llena de naranjos la zona 5 del mapa de zonas completo, en hileras a lo
    /// largo de X (5,5 m entre árboles y 5 m entre hileras, al tresbolillo), mete una horda en la huerta del este y deja que
    /// HuertaFpsProbe mida. No toca nada del juego normal: sin -huertafps, nada de este archivo se llama.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        internal bool HuertaListo()
        {
            return _worldA != null && _worldB != null && _worldCam != null && transform.Find("Ambientación de Sagunto") != null;
        }

        /// <summary>Planta los naranjos de prueba en la zona 5 a menos de «radio» metros de la zona simulada (0 = solo dentro de ella).
        /// Devuelve null o el motivo del fallo.</summary>
        internal string HuertaPlantar(float radio, float dx, float dz, out int arboles, out long tris, out int renderers)
        {
            arboles = 0; tris = 0; renderers = 0;
            var amb = transform.Find("Ambientación de Sagunto");
            if (amb == null) return "no hay ambientación de Sagunto (¿se ha entrado en la zona de Sagunto?)";
            var mapTex = Resources.Load<Texture2D>("Suelo/sagunto_zonas_1m_completo");
            if (mapTex == null || mapTex.width != MapW || mapTex.height != MapH) return "falta el mapa de zonas completo (Resources/Suelo/sagunto_zonas_1m_completo, 756 x 680)";
            var map = mapTex.GetPixels32();
            var root = new GameObject("Huerta de prueba (FPS)").transform; root.SetParent(amb, false);
            float sx0 = -radio, sz0 = -radio, sx1 = SimConst.WorldW + radio, sz1 = SimConst.WorldH + radio;
            int fila = 0;
            for (float z = MapZ0 + dz * 0.5f; z < MapZ0 + MapH; z += dz, fila++)
            {
                float off = (fila & 1) == 1 ? dx * 0.5f : 0f;
                for (float x = MapX0 + dx * 0.5f + off; x < MapX0 + MapW; x += dx)
                {
                    if (x < sx0 || x > sx1 || z < sz0 || z > sz1) continue;
                    int xi = Mathf.FloorToInt(x - MapX0), zi = Mathf.FloorToInt(z - MapZ0);
                    if (xi < 0 || zi < 0 || xi >= MapW || zi >= MapH) continue;
                    if (map[zi * MapW + xi].r != 5 || VegMask(x, z) == 255) continue;
                    // las hileras que ya hay (BuildGroundSampleProps al sur y el pueblo al norte) no se duplican
                    if ((x > 22f && x < 96f && z > 3f && z < 20f) || (x > 77f && x < 150f && z > 128f && z < 138f)) continue;
                    var go = PlaceVariant("naranjo", new Vector3(x, 0f, z), math_hash((int)(x * 10f), (int)(z * 10f)) % 360u, root);
                    if (go == null) continue;
                    arboles++;
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    {
                        var m = mf.sharedMesh; if (m == null) continue;
                        for (int s = 0; s < m.subMeshCount; s++) tris += m.GetIndexCount(s) / 3;
                    }
                    renderers += go.GetComponentsInChildren<MeshRenderer>().Length;
                }
            }
            return null;
        }

        /// <summary>Naranjos que ya estaban en la escena antes de la prueba (modelos SM_naranjo* fuera de la huerta de prueba).</summary>
        internal int HuertaNaranjosPrevios()
        {
            var amb = transform.Find("Ambientación de Sagunto"); if (amb == null) return 0;
            int n = 0;
            foreach (var t in amb.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("SM_naranjo") && (t.parent == null || !t.parent.name.StartsWith("SM_naranjo")) && (t.parent == null || t.parent.name != "Huerta de prueba (FPS)")) n++;
            return n;
        }

        /// <summary>La misma alta de zombis que la prueba -stress10k (en los dos mundos, con la misma semilla), en otro rectángulo.</summary>
        internal void HuertaHorda(int n, float x0, float z0, float w, float h)
        {
            if (n <= 0) return;
            foreach (var wld in new[] { _worldA, _worldB })
            {
                var em = wld.EntityManager;
                using (var qc = em.CreateEntityQuery(typeof(SimClock)))
                {
                    var clock = qc.GetSingleton<SimClock>();
                    var rng = new Unity.Mathematics.Random(777u);
                    var ents = em.CreateEntity(em.CreateArchetype(typeof(Zombie)), n, Allocator.Temp);
                    for (int i = 0; i < n; i++)
                        em.SetComponentData(ents[i], Zombie.Create(clock.NextSimId++, new Unity.Mathematics.float2(x0 + rng.NextFloat() * w, z0 + rng.NextFloat() * h), 0f));
                    ents.Dispose(); qc.SetSingleton(clock);
                }
            }
        }

        internal int HuertaZombis()
        {
            using (var q = _worldA.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Zombie>())) return q.CalculateEntityCount();
        }

        internal void HuertaCamara(Vector3 foco, float ortho) { _focus = foco; _orthoSize = ortho; _targetOrtho = ortho; }

        internal void HuertaCaptura() { SaveScreenshot(); }

        internal int HuertaFactor() { return _factor; }

        internal static string HuertaCapturas => CaptureRoot;
    }
}
