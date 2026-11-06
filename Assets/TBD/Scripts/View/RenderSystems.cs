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
    /// <summary>
    /// Color por instancia del zombi, enlazado a la propiedad _BaseColor del material de URP Lit (SRP Batcher / Entities Graphics).
    /// Se define aquí porque la documentación de Entities Graphics 1.5 no lista componentes URPMaterialProperty*.
    /// </summary>
    [MaterialProperty("_BaseColor")]
    public struct ZombieBaseColor : IComponentData
    {
        public float4 Value;
    }

    /// <summary>Recorte del atlas de Bruno por instancia (_BaseMap_ST: escala xy, desplazamiento zw): variante de ropa y fotograma del andar.</summary>
    [MaterialProperty("_BaseMap_ST")]
    public struct ZombieAtlasST : IComponentData
    {
        public float4 Value;
    }

    /// <summary>Malla y material de los zombis (los fija PrototypeBootstrap antes de que haya entidades).</summary>
    public static class RenderAssets
    {
        public static Mesh ZombieMesh;
        public static Material ZombieMaterial, ZombieLodMaterial;
        public static RenderMeshArray Array;
        public static bool Ready;
        /// <summary>D-N61: a poco zoom cada zombi se dibuja con el sprite de masa de 8 px (con histéresis, la decide PrototypeBootstrap).</summary>
        public static bool LodOn;
        public static bool LodDebug;                      // tiñe el LOD de magenta para comprobar que el cambio ocurre (reflexión desde el Editor)
        public static int LodVersion;

        public static void Set(Mesh mesh, Material material, Material lodMaterial)
        {
            ZombieMesh = mesh;
            ZombieMaterial = material;
            ZombieLodMaterial = lodMaterial;
            Array = new RenderMeshArray(new[] { material, lodMaterial }, new[] { mesh });
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
            var mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(RenderAssets.LodOn ? 1 : 0, 0);
            var scale = new PostTransformMatrix { Value = SpriteMatrix(RenderAssets.LodOn) };   // sprite de Bruno tumbado sobre el suelo
            var ents = _query.ToEntityArray(Allocator.Temp);
            var zs = _query.ToComponentDataArray<Zombie>(Allocator.Temp);
            var em = EntityManager;
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                RenderMeshUtility.AddComponents(e, em, desc, RenderAssets.Array, mmi);
                var lt = LocalTransform.FromPositionRotation(new float3(zs[i].Pos.x, zs[i].Pos.y + 0.05f, zs[i].Pos.z), quaternion.RotateY(zs[i].Heading));
                if (em.HasComponent<LocalTransform>(e)) em.SetComponentData(e, lt); else em.AddComponentData(e, lt);
                em.AddComponentData(e, scale);
                em.AddComponentData(e, new ZombieBaseColor { Value = ColorFor(zs[i].SimId) });
                em.AddComponentData(e, new ZombieAtlasST { Value = AtlasST(zs[i].SimId, 0.0) });
            }
            ents.Dispose();
            zs.Dispose();
        }

        public const float SpriteMeters = 2.6f, LodMeters = 1.1f;     // lado del cuadrado: sprite de 32 px y sprite de masa de 8 px

        public static float4x4 SpriteMatrix(bool lod)
        {
            float m = lod ? LodMeters : SpriteMeters;
            return float4x4.TRS(float3.zero, quaternion.RotateX(1.5707964f), new float3(m, m, 1f));
        }               // lado del cuadrado de 32 px del zombi (hombros de unos 0,8 m)
        public const int ZombieVariants = 6, ZombieFrames = 4;

        /// <summary>Recorte del atlas (4 columnas de fotogramas x variantes de ropa en filas, la fila 0 arriba): solo presentación, el id manda la variante y la fase.</summary>
        public static float4 AtlasST(uint id, double time, bool lod = false)
        {
            uint h = math.hash(new uint2(id, 11u));
            if (lod)                                         // atlas de masa: 5 columnas de 8 px (0-3 andar a 6 fps, 4 cadáver), una fila
                return new float4(0.2f, 1f, (((int)(time * 6.0) + (int)((h >> 8) & 3u)) & 3) * 0.2f, 0f);
            int row = (int)(h % ZombieVariants);
            int frame = ((int)(time * 6.0) + (int)((h >> 8) & 3u)) & (ZombieFrames - 1);
            float rows = ZombieVariants;
            return new float4(1f / ZombieFrames, 1f / rows, frame / (float)ZombieFrames, 1f - (row + 1) / rows);
        }

        // Los zombis llevan su color en el sprite (la textura): el color por instancia queda en blanco; solo la máscara lo cambia
        public static float4 ColorFor(uint id)
        {
            return new float4(1f, 1f, 1f, 1f);
        }

        // (paleta anterior de cápsulas, sin uso)
        static float4 ColorForOld(uint id)
        {
            // Revisión de arte de Lucía (5-nov): zombis OSCUROS sobre suelo claro, para que la horda se lea de un vistazo
            switch (math.hash(new uint2(id, 5u)) & 3u)
            {
                case 0: return Srgb(0x30, 0x34, 0x24);
                case 1: return Srgb(0x28, 0x34, 0x40);
                case 2: return Srgb(0x48, 0x40, 0x3c);
                default: return Srgb(0x44, 0x2c, 0x0c);
            }
        }

        static float4 Srgb(int r, int g, int b)
        {
            var c = new Color(r / 255f, g / 255f, b / 255f).linear;
            return new float4(c.r, c.g, c.b, 1f);
        }
    }

    /// <summary>Anima el andar de los zombis: fotograma por hash del id y reloj de pantalla (nunca el azar de la simulación).</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ZombieAnimSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            double t = SystemAPI.Time.ElapsedTime;
            bool lod = RenderAssets.LodOn;
            Entities.ForEach((ref ZombieAtlasST st, in Zombie z) => { st.Value = RenderAttachSystem.AtlasST(z.SimId, t, lod); }).ScheduleParallel();
        }
    }

    /// <summary>Cambia la malla/material y el tamaño de todos los zombis cuando cambia el LOD (solo al cruzar el umbral de zoom).</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class ZombieLodSystem : SystemBase
    {
        int _applied;

        protected override void OnUpdate()
        {
            int ver = RenderAssets.LodVersion + (RenderAssets.LodDebug ? 1000 : 0);
            if (!RenderAssets.Ready || _applied == ver) return;
            _applied = ver;
            var mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(RenderAssets.LodOn ? 1 : 0, 0);
            var post = new PostTransformMatrix { Value = RenderAttachSystem.SpriteMatrix(RenderAssets.LodOn) };
            var tint = RenderAssets.LodOn && RenderAssets.LodDebug ? new float4(1f, 0.2f, 1f, 1f) : new float4(1f, 1f, 1f, 1f);
            foreach (var (m, p, c) in SystemAPI.Query<RefRW<MaterialMeshInfo>, RefRW<PostTransformMatrix>, RefRW<ZombieBaseColor>>().WithAll<Zombie>())
            { m.ValueRW = mmi; p.ValueRW = post; c.ValueRW.Value = tint; }
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
                float3 p = math.lerp(z.PrevPos, z.Pos, Alpha);
                uint h = math.hash(new uint2(z.SimId, 29u));                  // rompe las filas: ±0,5 m y ±20° por id, nunca la posición simulada
                float jx = ((h & 255u) / 255f - 0.5f), jz = (((h >> 8) & 255u) / 255f - 0.5f), jr = (((h >> 16) & 255u) / 255f - 0.5f) * 0.6981317f;
                lt.Position = new float3(p.x + jx, p.y + 0.05f, p.z + jz);
                // orientación continua: se interpola por el camino corto
                float d = z.Heading - z.PrevHeading;
                d -= 6.2831853f * math.floor((d + 3.14159265f) / 6.2831853f);
                lt.Rotation = quaternion.RotateY(z.PrevHeading + d * Alpha + jr);
            }
        }
    }
}
