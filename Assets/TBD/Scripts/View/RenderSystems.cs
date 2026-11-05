using TBD.Sim;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace TBD.View
{
    /// <summary>Malla y material de los zombis (los fija PrototypeBootstrap antes de que haya entidades).</summary>
    public static class RenderAssets
    {
        public static Mesh ZombieMesh;
        public static Material ZombieMaterial;
        public static RenderMeshArray Array;
        public static bool Ready;

        public static void Set(Mesh mesh, Material material)
        {
            ZombieMesh = mesh;
            ZombieMaterial = material;
            Array = new RenderMeshArray(new[] { material }, new[] { mesh });
            Ready = true;
        }
    }

    /// <summary>
    /// Solo en el mundo por defecto (el de pantalla): a cada zombi nuevo le añade los componentes de dibujo de Entities Graphics.
    /// La simulación no depende de nada de esto, así que el mundo B de la prueba de desincronización no lo necesita.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class RenderAttachSystem : SystemBase
    {
        EntityQuery _query;

        protected override void OnCreate()
        {
            _query = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Zombie>() },
                None = new[] { ComponentType.ReadOnly<LocalTransform>() },
            });
        }

        protected override void OnUpdate()
        {
            if (!RenderAssets.Ready || _query.IsEmptyIgnoreFilter) return;

            var desc = new RenderMeshDescription(ShadowCastingMode.Off, false);
            var mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);
            var scale = new PostTransformMatrix { Value = float4x4.Scale(0.5f, 0.9f, 0.5f) };   // cápsula de 0,5 x 1,8 m
            var ents = _query.ToEntityArray(Allocator.Temp);
            var zs = _query.ToComponentDataArray<Zombie>(Allocator.Temp);
            var em = EntityManager;
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                RenderMeshUtility.AddComponents(e, em, desc, RenderAssets.Array, mmi);
                em.AddComponentData(e, LocalTransform.FromPosition(new float3(zs[i].Pos.x, zs[i].Z + 0.9f, zs[i].Pos.y)));
                em.AddComponentData(e, scale);
                em.AddComponentData(e, new URPMaterialPropertyBaseColor { Value = ColorFor(zs[i].SimId) });
            }
            ents.Dispose();
            zs.Dispose();
        }

        // Verdes y grises de la paleta de 32 colores
        static float4 ColorFor(uint id)
        {
            switch (math.hash(new uint2(id, 5u)) & 3u)
            {
                case 0: return Srgb(0x6c, 0x78, 0x74);
                case 1: return Srgb(0x70, 0x78, 0x4c);
                case 2: return Srgb(0x88, 0x88, 0x5c);
                default: return Srgb(0x60, 0x5c, 0x48);
            }
        }

        static float4 Srgb(int r, int g, int b)
        {
            var c = new Color(r / 255f, g / 255f, b / 255f).linear;
            return new float4(c.r, c.g, c.b, 1f);
        }
    }

    /// <summary>Copia la posición simulada a la transformada de dibujo, interpolando entre ticks.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(RenderAttachSystem))]
    public partial struct ZombieTransformSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float alpha = 1f;
            if (SystemAPI.TryGetSingleton<SimClock>(out var clock)) alpha = clock.Alpha;
            new ApplyJob { Alpha = math.saturate(alpha) }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ApplyJob : IJobEntity
        {
            public float Alpha;

            void Execute(ref LocalTransform lt, in Zombie z)
            {
                float2 p = math.lerp(z.PrevPos, z.Pos, Alpha);
                float h = math.lerp(z.PrevZ, z.Z, Alpha);
                lt.Position = new float3(p.x, h + 0.9f, p.y);
            }
        }
    }
}
