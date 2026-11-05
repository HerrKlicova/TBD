using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace TBD.Sim
{
    /// <summary>
    /// Raíz de los sistemas de simulación. Se actualiza a mano a tick fijo (ver PrototipoBootstrap), no con el bucle de Unity.
    /// Los sistemas se añaden en un orden explícito (EnableSystemSorting = false) para que sea idéntico en todos los mundos.
    /// </summary>
    [DisableAutoCreation]
    public partial class SimRootGroup : ComponentSystemGroup { }

    // ================================================================== 1. órdenes
    [DisableAutoCreation]
    public partial struct CommandSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var root = SystemAPI.GetSingletonEntity<ZoneTag>();
            uint tick = em.GetComponentData<SimClock>(root).Tick;

            for (int guard = 0; guard < 64; guard++)
            {
                var cmds = em.GetBuffer<SimCommand>(root);
                int idx = -1;
                for (int i = 0; i < cmds.Length; i++)
                    if (cmds[i].Tick <= tick) { idx = i; break; }
                if (idx < 0) break;
                var cmd = cmds[idx];
                cmds.RemoveAt(idx);
                Execute(em, root, cmd, tick);
            }
        }

        static void Execute(EntityManager em, Entity root, SimCommand cmd, uint tick)
        {
            switch (cmd.Type)
            {
                case CmdType.ToggleGate:
                {
                    var info = em.GetComponentData<ZoneInfo>(root);
                    info.GateOpen = !info.GateOpen;
                    info.ForceFlow = true;
                    em.SetComponentData(root, info);
                    var solids = em.GetBuffer<Solid>(root);
                    for (int i = 0; i < solids.Length; i++)
                    {
                        if (solids[i].Id != Level.GateId) continue;
                        var s = solids[i];
                        s.Open = (byte)(info.GateOpen ? 1 : 0);
                        solids[i] = s;
                        var ground = em.GetBuffer<CellGround>(root);
                        Level.RasterSolid(solids, ground, s);       // solo se recalcula la región afectada
                    }
                    break;
                }
                case CmdType.ToggleTrain:
                {
                    var t = em.GetComponentData<TrainState>(root);
                    t.Speed = t.Speed > 0f ? 0f : SimConst.TrainSpeed;
                    em.SetComponentData(root, t);
                    break;
                }
                case CmdType.BuildWall:
                {
                    // Construcción libre: muro con posición y orientación libres; en la rejilla solo se rasteriza la región afectada
                    var solids = em.GetBuffer<Solid>(root);
                    var s = new Solid { A = cmd.P0, B = cmd.P1, HalfThick = Level.WallHalf * 0.5f, Height = Level.WallH, Kind = 0, Open = 0, Id = 0 };
                    solids.Add(s);
                    var ground = em.GetBuffer<CellGround>(root);
                    Level.RasterSolid(solids, ground, s);
                    SolidIndex.Rebuild(solids, em.GetBuffer<SolidBucketStart>(root), em.GetBuffer<SolidBucketRef>(root));
                    var info = em.GetComponentData<ZoneInfo>(root);
                    info.ForceFlow = true;
                    em.SetComponentData(root, info);
                    break;
                }
                case CmdType.SpawnRoof:
                    SpawnZombies(em, root, cmd.Arg, tick, roof: true);
                    break;
                case CmdType.SpawnWest:
                    SpawnZombies(em, root, cmd.Arg, tick, roof: false);
                    break;
            }
        }

        /// <summary>Aleatoriedad determinista: semilla derivada del tick y del tipo de orden (regla 4).</summary>
        public static void SpawnZombies(EntityManager em, Entity root, int count, uint tick, bool roof)
        {
            var clock = em.GetComponentData<SimClock>(root);
            var rng = new Unity.Mathematics.Random(math.hash(new uint2(tick, roof ? 101u : 202u)) | 1u);
            var arch = em.CreateArchetype(typeof(Zombie));
            var ents = em.CreateEntity(arch, count, Allocator.Temp);
            for (int i = 0; i < count; i++)
            {
                float2 p;
                float h;
                if (roof)
                {
                    p = new float2(rng.NextFloat(Level.RoofX0 + 0.5f, Level.RoofX1 - 0.5f),
                                   rng.NextFloat(Level.RoofY0 + 0.5f, Level.RoofY1 - 0.5f));
                    h = Level.RoofH;
                }
                else
                {
                    p = new float2(rng.NextFloat(2f, 14f), rng.NextFloat(8f, 120f));
                    h = 0f;
                }
                em.SetComponentData(ents[i], Zombie.Create(clock.NextSimId++, p, h));
            }
            ents.Dispose();
            em.SetComponentData(root, clock);
        }
    }

    // ================================================================== 2. campo de flujo
    [BurstCompile]
    [DisableAutoCreation]
    public partial struct FlowFieldSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<ZoneTag>();
            var clock = SystemAPI.GetSingleton<SimClock>();
            var info = SystemAPI.GetSingleton<ZoneInfo>();
            if (!info.ForceFlow && (clock.Tick % SimConst.FlowEveryTicks) != 0) return;

            var train = SystemAPI.GetSingleton<TrainState>();
            int2 gc = Level.CellOf(new float2(train.X, Level.TrackY));
            int goal = gc.y * SimConst.GridW + gc.x;
            info.GoalCell = goal;
            info.ForceFlow = false;
            SystemAPI.SetSingleton(info);

            var ground = SystemAPI.GetBuffer<CellGround>(root).Reinterpret<float>().AsNativeArray();
            var dist = SystemAPI.GetBuffer<CellFlowDist>(root).Reinterpret<int>().AsNativeArray();
            var vec = SystemAPI.GetBuffer<CellFlowVec>(root).Reinterpret<float2>().AsNativeArray();
            new FlowJob { Ground = ground, Dist = dist, Vec = vec, Goal = goal }.Run();
        }
    }

    // ================================================================== 3. zombis
    [BurstCompile]
    [DisableAutoCreation]
    public partial struct ZombieStepSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<ZoneTag>();
            var solids = SystemAPI.GetBuffer<Solid>(root).AsNativeArray();
            var bStart = SystemAPI.GetBuffer<SolidBucketStart>(root).Reinterpret<int>().AsNativeArray();
            var bRefs = SystemAPI.GetBuffer<SolidBucketRef>(root).Reinterpret<int>().AsNativeArray();
            var pile = SystemAPI.GetBuffer<CellPile>(root).Reinterpret<int>().AsNativeArray();
            var delta = SystemAPI.GetBuffer<CellPileDelta>(root).Reinterpret<int>().AsNativeArray();
            var flow = SystemAPI.GetBuffer<CellFlowVec>(root).Reinterpret<float2>().AsNativeArray();

            // Doble búfer (regla 5): cada zombi lee la pila del tick anterior y solo escribe deltas (sumas enteras conmutativas).
            var snapshot = new NativeArray<int>(pile.Length, Allocator.Temp);
            snapshot.CopyFrom(pile);

            new ZombieStepJob { Solids = solids, BucketStart = bStart, BucketRefs = bRefs, PileRead = snapshot, FlowVec = flow, PileDelta = delta }.Run();
            snapshot.Dispose();
        }
    }

    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public partial struct ZombieStepJob : IJobEntity
    {
        [ReadOnly] public NativeArray<Solid> Solids;
        [ReadOnly] public NativeArray<int> BucketStart;
        [ReadOnly] public NativeArray<int> BucketRefs;
        [ReadOnly] public NativeArray<int> PileRead;
        [ReadOnly] public NativeArray<float2> FlowVec;
        public NativeArray<int> PileDelta;

        void Execute(ref Zombie z)
        {
            ZombieLogic.Step(ref z, Solids, BucketStart, BucketRefs, PileRead, FlowVec, PileDelta);   // lógica pura, probada con dotnet (carpeta Dotnet/)
        }
    }

    [BurstCompile]
    [DisableAutoCreation]
    public partial struct PileApplySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<ZoneTag>();
            var pile = SystemAPI.GetBuffer<CellPile>(root).Reinterpret<int>().AsNativeArray();
            var delta = SystemAPI.GetBuffer<CellPileDelta>(root).Reinterpret<int>().AsNativeArray();
            new PileApplyJob { Pile = pile, Delta = delta }.Run();
        }
    }

    // ================================================================== 4. tren
    [BurstCompile]
    [DisableAutoCreation]
    public partial struct TrainSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var t = SystemAPI.GetSingleton<TrainState>();
            if (t.Speed > 0f)
            {
                t.X += t.Dir * t.Speed * SimConst.Dt;
                if (t.X >= Level.TrainMaxX) { t.X = Level.TrainMaxX; t.Dir = -1; }
                if (t.X <= Level.TrainMinX) { t.X = Level.TrainMinX; t.Dir = 1; }
                SystemAPI.SetSingleton(t);
            }
        }
    }

    // ================================================================== 5. hash del estado
    /// <summary>
    /// Hash del estado por tick (regla 7). Es una suma de hashes por elemento: no depende del orden de recorrido.
    /// Se usa para la prueba de desincronización entre dos mundos con las mismas órdenes.
    /// </summary>
    [BurstCompile]
    [DisableAutoCreation]
    public partial struct StateHashSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<ZoneTag>();
            var clock = SystemAPI.GetSingleton<SimClock>();
            ulong sum = 0;

            foreach (var zr in SystemAPI.Query<RefRO<Zombie>>())
                sum += HashMath.OfZombie(zr.ValueRO);

            var pile = SystemAPI.GetBuffer<CellPile>(root);
            for (int i = 0; i < pile.Length; i++)
                sum += HashMath.OfPileCell(i, pile[i].Value);

            var solids = SystemAPI.GetBuffer<Solid>(root);
            sum += HashMath.Mix((ulong)solids.Length + 0x7777UL);

            var train = SystemAPI.GetSingleton<TrainState>();
            sum += HashMath.Mix((ulong)(uint)(int)math.round(train.X * 256f) + 0x1234567UL);

            SystemAPI.SetSingleton(new SimHash { Value = sum + clock.Tick, Tick = clock.Tick });
        }
    }

    // ================================================================== 6. reloj
    [BurstCompile]
    [DisableAutoCreation]
    public partial struct TickSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var clock = SystemAPI.GetSingleton<SimClock>();
            clock.Tick += 1;
            SystemAPI.SetSingleton(clock);
        }
    }
}
