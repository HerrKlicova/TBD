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
    public partial class SimRootGroup : ComponentSystemGroup
    {
        // El setter de EnableSystemSorting es protected: se fija desde la propia clase.
        protected override void OnCreate()
        {
            base.OnCreate();
            EnableSystemSorting = false;
        }
    }

    // ================================================================== 1. órdenes
    [DisableAutoCreation]
    public partial struct CommandSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var root = SystemAPI.GetSingletonEntity<ZoneTag>();
            uint tick = em.GetComponentData<SimClock>(root).Tick;
            var zi = em.GetComponentData<ZoneInfo>(root);
            if (zi.TrainWallDirty)                                           // el tren se paró solo (tope o tapón): vuelve a ser muro
            {
                zi.TrainWallDirty = false;
                em.SetComponentData(root, zi);
                SetTrainWall(em, root, em.GetComponentData<TrainState>(root).X);
            }

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

        /// <summary>
        /// Pone (x = posición del tren) o quita (NaN) el muro del tren: la colisión y la pila lo tratan como un segmento de muro (§9c).
        /// Solo se rasteriza la región afectada y se recalcula el flujo.
        /// </summary>
        public static void SetTrainWall(EntityManager em, Entity root, float x)
        {
            var solids = em.GetBuffer<Solid>(root);
            float2 mn = new float2(float.MaxValue), mx = new float2(float.MinValue);
            for (int i = solids.Length - 1; i >= 0; i--)
                if (solids[i].Id == Level.TrainId) { mn = math.min(mn, math.min(solids[i].A, solids[i].B)); mx = math.max(mx, math.max(solids[i].A, solids[i].B)); solids.RemoveAt(i); }
            if (!float.IsNaN(x))
            {
                var ts = Level.TrainSolid(x);
                solids.Add(ts);
                mn = math.min(mn, math.min(ts.A, ts.B)); mx = math.max(mx, math.max(ts.A, ts.B));
            }
            if (mn.x > mx.x) return;
            mn -= Level.TrainHalf + 1f; mx += Level.TrainHalf + 1f;
            var ground = em.GetBuffer<CellGround>(root);
            int2 c0 = Level.CellOf(mn), c1 = Level.CellOf(mx);
            Level.RasterRegion(solids, ground, c0.x, c0.y, c1.x, c1.y);
            SolidIndex.Rebuild(solids, em.GetBuffer<SolidBucketStart>(root), em.GetBuffer<SolidBucketRef>(root));
            var info = em.GetComponentData<ZoneInfo>(root);
            info.ForceFlow = true;
            em.SetComponentData(root, info);
        }

        /// <summary>Hueco k (0-20) de los vagones de tropa: 11 y 10, a los dos lados.</summary>
        public static float2 SlotOf(int k)
        {
            // tren a escala real: dos vagones R2 de tropa de 19,9 m detrás de la locomotora de 24 m (centros a -31,5 y -51,4 m de X): 11 huecos en el primero y 10 en el segundo
            int wagon = k / 11, j = k % 11;
            return new float2(Det.Mm(-31500 - wagon * 19900 + (j - 5) * 1200), Det.Mm(j % 2 == 0 ? -550 : 550));   // enteros (determinista en IL2CPP)
        }

        static int SlotIndex(float2 slot)
        {
            for (int k = 0; k < 21; k++) { var s = SlotOf(k); if (math.abs(s.x - slot.x) < 0.01f && math.abs(s.y - slot.y) < 0.01f) return k; }
            return -1;
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
                    if (!em.GetComponentData<ZoneInfo>(root).HasTrain) break;      // sin tren en el andén no hay nada que mover
                    var t = em.GetComponentData<TrainState>(root);
                    t.Speed = t.Speed > 0f ? 0f : SimConst.TrainSpeed;
                    em.SetComponentData(root, t);
                    SetTrainWall(em, root, t.Speed <= 0f ? t.X : float.NaN);     // D-N27: parado = muro; en marcha, sin muro
                    break;
                }
                case CmdType.TrainDrive:
                {
                    if (!em.GetComponentData<ZoneInfo>(root).HasTrain) break;
                    var t = em.GetComponentData<TrainState>(root);
                    t.Dir = cmd.Arg < 0 ? -1 : 1;
                    t.Speed = cmd.Arg == 0 ? 0f : SimConst.TrainSpeed;
                    em.SetComponentData(root, t);
                    SetTrainWall(em, root, t.Speed <= 0f ? t.X : float.NaN);
                    break;
                }
                case CmdType.BuildWall:
                {
                    // Construcción libre: muro con posición y orientación libres; en la rejilla solo se rasteriza la región afectada
                    var solids = em.GetBuffer<Solid>(root);
                    var s = new Solid { A = cmd.P0, B = cmd.P1, HalfThick = Level.WallHalf * 0.5f, Height = cmd.Arg > 0 ? Det.Mm(cmd.Arg) : Level.WallH, Kind = 0, Open = 0, Id = 0 };   // H5: Arg = altura en mm (sacos 1.200, HESCO 2.100)
                    solids.Add(s);
                    var ground = em.GetBuffer<CellGround>(root);
                    Level.RasterSolid(solids, ground, s);
                    SolidIndex.Rebuild(solids, em.GetBuffer<SolidBucketStart>(root), em.GetBuffer<SolidBucketRef>(root));
                    var info = em.GetComponentData<ZoneInfo>(root);
                    info.ForceFlow = true;
                    em.SetComponentData(root, info);
                    break;
                }
                case CmdType.BuildSeg:
                {
                    // D-N163: tramo de muro con Id propio (16..255) para poder abrirlo (brecha) y cerrarlo (reconstruir) por la cola unica
                    int segId = cmd.Arg / 10000, segH = cmd.Arg % 10000;
                    if (segId < 16 || segId > 255) break;                              // ids de tramo: 16..255 (el 1 es la puerta, el 2 el tren); fuera de rango no hace nada (Nacho)
                    var solids = em.GetBuffer<Solid>(root);
                    var s = new Solid { A = cmd.P0, B = cmd.P1, HalfThick = Level.WallHalf, Height = segH > 0 ? Det.Mm(segH) : Level.WallH, Kind = 0, Open = 0, Id = (byte)math.clamp(segId, 0, 255) };
                    solids.Add(s);
                    var ground = em.GetBuffer<CellGround>(root);
                    Level.RasterSolid(solids, ground, s);
                    SolidIndex.Rebuild(solids, em.GetBuffer<SolidBucketStart>(root), em.GetBuffer<SolidBucketRef>(root));
                    var info = em.GetComponentData<ZoneInfo>(root);
                    info.ForceFlow = true;
                    em.SetComponentData(root, info);
                    break;
                }
                case CmdType.SetSegment:
                {
                    // D-N163: abre (brecha) o cierra (reconstruido) el tramo con ese Id; solo se recalcula la region afectada
                    int segId = cmd.Arg / 10, open = cmd.Arg % 10;
                    if (segId < 16 || segId > 255) break;
                    var solids = em.GetBuffer<Solid>(root);
                    for (int i = 0; i < solids.Length; i++)
                    {
                        if (solids[i].Id != segId) continue;
                        var s = solids[i];
                        s.Open = (byte)(open != 0 ? 1 : 0);
                        solids[i] = s;
                        var ground = em.GetBuffer<CellGround>(root);
                        Level.RasterSolid(solids, ground, s);
                    }
                    var info = em.GetComponentData<ZoneInfo>(root);
                    info.ForceFlow = true;
                    em.SetComponentData(root, info);
                    break;
                }
                case CmdType.ClearWest:
                {
                    var solids = em.GetBuffer<Solid>(root);
                    float wx = Level.FortX0 + Level.WallHalf;
                    for (int i = 0; i < solids.Length; i++)
                    {
                        var s = solids[i];
                        if (s.Kind != 0 || s.Id != 0 || math.abs(s.A.x - wx) > 0.01f || math.abs(s.B.x - wx) > 0.01f) continue;
                        s.Open = 1; solids[i] = s;
                        var ground = em.GetBuffer<CellGround>(root);
                        Level.RasterSolid(solids, ground, s);
                    }
                    var info = em.GetComponentData<ZoneInfo>(root);
                    info.ForceFlow = true;
                    em.SetComponentData(root, info);
                    break;
                }
                case CmdType.DeploySquad:
                {
                    // 20 fusileros en tres filas tras el muro oeste y una ametralladora en la puerta (dentro del fuerte, junto al tren)
                    var clock = em.GetComponentData<SimClock>(root);
                    var cfg = em.GetComponentData<CombatConfig>(root).P;
                    var arch = em.CreateArchetype(typeof(Soldier));
                    var ents = em.CreateEntity(arch, 21, Allocator.Temp);
                    for (int i = 0; i < 21; i++)
                    {
                        bool mg = i == 20;
                        int fx0 = (int)math.round(Level.FortX0 * 1000f), fy0 = (int)math.round(Level.FortY0 * 1000f), fy1 = (int)math.round(Level.FortY1 * 1000f);
                        int gy = ((int)math.round(Level.GateY0 * 1000f) + (int)math.round(Level.GateY1 * 1000f)) / 2;
                        float x = Det.Mm(mg ? fx0 + 2500 : fx0 + 2500 + (i % 3) * 1300);                                        // enteros (IL2CPP)
                        float y = Det.Mm(mg ? gy : fy0 + 4000 + (i / 3) * ((fy1 - fy0 - 8000) / 6));
                        em.SetComponentData(ents[i], new Soldier
                        {
                            SimId = clock.NextSimId++, Pos = new float3(x, 0f, y), Heading = -1.5708f, Hp = cfg.SoldierHp,
                            Kind = mg ? SoldierKind.MachineGun : SoldierKind.Rifle, State = SoldierState.Deployed,
                            Cooldown = (int)((clock.NextSimId * 7u) % (uint)cfg.RifleCooldown),
                        });
                    }
                    ents.Dispose();
                    em.SetComponentData(root, clock);
                    break;
                }
                case CmdType.EmbarkSquad:
                {
                    if (!em.GetComponentData<ZoneInfo>(root).HasTrain) break;      // sin tren no se puede ir a bordo
                    // D-N27: 20 fusileros y la ametralladora a bordo, repartidos por los tres vagones (7 por vagón), a los dos lados
                    var clock = em.GetComponentData<SimClock>(root);
                    var cfg = em.GetComponentData<CombatConfig>(root).P;
                    var t = em.GetComponentData<TrainState>(root);
                    var arch = em.CreateArchetype(typeof(Soldier));
                    var ents = em.CreateEntity(arch, 21, Allocator.Temp);
                    for (int i = 0; i < 21; i++)
                    {
                        var slot = SlotOf(i);
                        em.SetComponentData(ents[i], new Soldier
                        {
                            SimId = clock.NextSimId++, Slot = slot, Pos = new float3(t.X + slot.x, Level.TrainH, Level.TrackY + slot.y),
                            Heading = -1.5708f, Hp = cfg.SoldierHp, Kind = i == 10 ? SoldierKind.MachineGun : SoldierKind.Rifle,
                            State = SoldierState.Embarked, Cooldown = (int)((clock.NextSimId * 7u) % (uint)cfg.RifleCooldown),   // desfase: no disparan a la vez
                        });
                    }
                    ents.Dispose();
                    em.SetComponentData(root, clock);
                    break;
                }
                case CmdType.Reembark:
                {
                    // Los soldados a pie que estén a menos de 20 m del tren suben a bordo, en orden de SimId y a los huecos libres
                    var t = em.GetComponentData<TrainState>(root);
                    var q = em.CreateEntityQuery(typeof(Soldier));
                    var ents = q.ToEntityArray(Allocator.Temp);
                    var arr = q.ToComponentDataArray<Soldier>(Allocator.Temp);
                    var order = new NativeArray<int>(arr.Length, Allocator.Temp);
                    SoldierLogic.SortById(arr, order);
                    var used = new NativeArray<bool>(21, Allocator.Temp);
                    for (int i = 0; i < arr.Length; i++)
                        if (arr[i].State == SoldierState.Embarked) { int sl = SlotIndex(arr[i].Slot); if (sl >= 0) used[sl] = true; }
                    float2 a = new float2(t.X - Level.TrainRear, Level.TrackY), b = new float2(t.X + Level.TrainFront, Level.TrackY);
                    for (int o = 0; o < order.Length; o++)
                    {
                        int i = order[o];
                        var so = arr[i];
                        if (so.State != SoldierState.Deployed) continue;
                        float2 p = so.Pos.xz, ab = b - a;
                        float tt = math.saturate(math.dot(p - a, ab) / math.dot(ab, ab));
                        if (math.distance(p, a + ab * tt) > 20f) continue;
                        int free = -1;
                        for (int k = 0; k < 21; k++) if (!used[k]) { free = k; break; }
                        if (free < 0) break;
                        used[free] = true;
                        so.Slot = SlotOf(free);
                        so.State = SoldierState.Embarked;
                        em.SetComponentData(ents[i], so);
                    }
                    ents.Dispose(); arr.Dispose(); order.Dispose(); used.Dispose();
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
                    p = new float2(Det.Range(ref rng, Level.RoofX0 + 0.5f, Level.RoofX1 - 0.5f),
                                   Det.Range(ref rng, Level.RoofY0 + 0.5f, Level.RoofY1 - 0.5f));
                    h = Level.RoofH;
                }
                else
                {
                    p = new float2(Det.Range(ref rng, 2f, 14f), Det.Range(ref rng, 8f, 120f));
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
            // TempJob y no Temp: IJobEntity.Run() pasa por el planificador y rechaza contenedores Temp (visto en Unity 6000.3).
            var snapshot = new NativeArray<int>(pile.Length, Allocator.TempJob);
            snapshot.CopyFrom(pile);

            // Densidad del tick anterior para la separación entre agentes (sumas enteras: no depende del orden).
            var density = new NativeArray<int>(pile.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            new DensityCountJob { Density = density }.Run();

            new ZombieStepJob { Solids = solids, BucketStart = bStart, BucketRefs = bRefs, PileRead = snapshot, FlowVec = flow, PileDelta = delta, Density = density }.Run();
            snapshot.Dispose();
            density.Dispose();
        }
    }

    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    public partial struct ZombieStepJob : IJobEntity
    {
        [ReadOnly] public NativeArray<Solid> Solids;
        [ReadOnly] public NativeArray<int> BucketStart;
        [ReadOnly] public NativeArray<int> BucketRefs;
        [ReadOnly] public NativeArray<int> PileRead;
        [ReadOnly] public NativeArray<float2> FlowVec;
        public NativeArray<int> PileDelta;
        [ReadOnly] public NativeArray<int> Density;

        void Execute(ref Zombie z)
        {
            ZombieLogic.Step(ref z, Solids, BucketStart, BucketRefs, PileRead, FlowVec, PileDelta, Density);   // lógica pura, probada con dotnet (carpeta Dotnet/)
        }
    }

    [BurstCompile]
    public partial struct DensityCountJob : IJobEntity
    {
        public NativeArray<int> Density;

        void Execute(in Zombie z)
        {
            ZombieLogic.AddDensity(z, Density);
        }
    }

    // ================================================================== 3b. tropas (hito 3, parte 4)
    /// <summary>
    /// Disparos y cuerpo a cuerpo de las tropas, con la lógica pura de SoldierLogic (probada en dotnet). Después retira los zombis
    /// abatidos y convierte a los soldados caídos en zombis (se levantan, GDD 5.6). Todo en orden determinista: soldados por SimId
    /// y las altas nuevas con NextSimId.
    /// </summary>
    [DisableAutoCreation]
    public partial struct SoldierSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var root = SystemAPI.GetSingletonEntity<ZoneTag>();
            var sq = SystemAPI.QueryBuilder().WithAll<Soldier>().Build();
            if (sq.IsEmptyIgnoreFilter) return;
            var zq = SystemAPI.QueryBuilder().WithAll<Zombie>().Build();
            var clock = em.GetComponentData<SimClock>(root);
            var cfg = em.GetComponentData<CombatConfig>(root).P;
            var ammo = em.GetComponentData<ZoneAmmo>(root);
            var delta = em.GetBuffer<CellPileDelta>(root).Reinterpret<int>().AsNativeArray();

            var soldiers = sq.ToComponentDataArray<Soldier>(Allocator.TempJob);
            var sEnts = sq.ToEntityArray(Allocator.TempJob);
            var train = em.GetComponentData<TrainState>(root);
            for (int i = 0; i < soldiers.Length; i++)                    // los de a bordo van con el tren
            {
                var so = soldiers[i];
                if (so.State != SoldierState.Embarked) continue;
                so.Pos = new float3(train.X + so.Slot.x, Level.TrainH, Level.TrackY + so.Slot.y);
                soldiers[i] = so;
            }
            var zombies = zq.ToComponentDataArray<Zombie>(Allocator.TempJob);
            var order = new NativeArray<int>(soldiers.Length, Allocator.TempJob);
            var ammoArr = new NativeArray<ZoneAmmo>(1, Allocator.TempJob);
            ammoArr[0] = ammo;
            new SoldierJob { Soldiers = soldiers, Order = order, Zombies = zombies, Ammo = ammoArr, Tick = clock.Tick, P = cfg, PileDelta = delta, TrainStopped = train.Speed <= 0f }.Run();
            ammo = ammoArr[0];
            zq.CopyFromComponentDataArray(zombies);
            sq.CopyFromComponentDataArray(soldiers);

            // retirar abatidos
            var zEnts = zq.ToEntityArray(Allocator.Temp);
            var dead = new NativeList<Entity>(Allocator.Temp);
            for (int i = 0; i < zombies.Length; i++) if (zombies[i].State == ZState.Dead) dead.Add(zEnts[i]);
            if (dead.Length > 0) em.DestroyEntity(dead.AsArray());

            // los soldados caídos se levantan como zombis (en orden de SimId)
            for (int o = 0; o < order.Length; o++)
            {
                int si = order[o];
                if (soldiers[si].State != SoldierState.Dead) continue;
                var ze = em.CreateEntity(typeof(Zombie));
                em.SetComponentData(ze, Zombie.Create(clock.NextSimId++, soldiers[si].Pos.xz, soldiers[si].Pos.y));
                em.DestroyEntity(sEnts[si]);
            }
            em.SetComponentData(root, clock);
            em.SetComponentData(root, ammo);
            dead.Dispose(); zEnts.Dispose();
            soldiers.Dispose(); sEnts.Dispose(); zombies.Dispose(); order.Dispose(); ammoArr.Dispose();
        }
    }

    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    public struct SoldierJob : IJob
    {
        public NativeArray<Soldier> Soldiers;
        public NativeArray<int> Order;
        public NativeArray<Zombie> Zombies;
        public NativeArray<ZoneAmmo> Ammo;
        public NativeArray<int> PileDelta;
        public uint Tick;
        public CombatParams P;
        public bool TrainStopped;

        public void Execute()
        {
            var a = Ammo[0];
            SoldierLogic.SortById(Soldiers, Order);
            var grid = new ZombieGrid();                          // índice espacial: una sola construcción por tick, compartida por Fire y Melee
            SoldierLogic.Fire(Soldiers, Order, Zombies, ref a, Tick, P, PileDelta, TrainStopped, ref grid);
            SoldierLogic.Melee(Soldiers, Zombies, ref a, P, ref grid);
            grid.Dispose();
            Ammo[0] = a;
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
                var info = SystemAPI.GetSingleton<ZoneInfo>();
                if (info.Kind == ZoneKind.Cutting)
                {
                    // desmonte: el tapón de cuerpos frena al tren hacia delante; en los topes se para (y vuelve a ser muro)
                    var root = SystemAPI.GetSingletonEntity<ZoneTag>();
                    var pile = SystemAPI.GetBuffer<CellPile>(root).Reinterpret<int>().AsNativeArray();
                    float nx = t.X + t.Dir * t.Speed * SimConst.Dt;
                    bool blocked = t.Dir > 0 && CuttingLevel.PlugBlocks(pile, t.X);
                    if (blocked || nx >= info.TrainMax || nx <= info.TrainMin)
                    {
                        t.Speed = 0f;
                        info.TrainWallDirty = true;
                        SystemAPI.SetSingleton(info);
                    }
                    else t.X = nx;
                    SystemAPI.SetSingleton(t);
                    return;
                }
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
            for (int i = 0; i < solids.Length; i++) sum += HashMath.OfSolid(i, solids[i]);   // QA Q2: contenido, no solo cuántos

            var train = SystemAPI.GetSingleton<TrainState>();
            sum += HashMath.OfFloat(train.X, 21) + HashMath.OfFloat(train.Speed, 22) + HashMath.Mix((ulong)(uint)train.Dir + 0x1234567UL);
            var info = SystemAPI.GetSingleton<ZoneInfo>();
            sum += HashMath.Mix((info.GateOpen ? 1UL : 2UL) ^ (info.HasTrain ? 4UL : 0UL) ^ ((ulong)(uint)info.GoalCell << 8) ^ 0xFEEDUL);
            sum += HashMath.Mix(((ulong)clock.NextSimId << 1) ^ 0xBEEF00UL);
            foreach (var sr in SystemAPI.Query<RefRO<Soldier>>()) sum += HashMath.OfSoldier(sr.ValueRO);
            sum += HashMath.OfAmmo(SystemAPI.GetSingleton<ZoneAmmo>());

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
