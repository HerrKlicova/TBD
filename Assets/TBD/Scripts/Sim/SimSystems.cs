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
                    SetTrainWall(em, root, t.Speed <= 0f ? t.X : float.NaN);     // D-N27: parado = muro; en marcha, sin muro
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
                        float x = mg ? Level.FortX0 + 2.5f : Level.FortX0 + 2.5f + (i % 3) * 1.3f;
                        float y = mg ? (Level.GateY0 + Level.GateY1) * 0.5f : Level.FortY0 + 4f + (i / 3) * ((Level.FortY1 - Level.FortY0 - 8f) / 6f);
                        em.SetComponentData(ents[i], new Soldier
                        {
                            SimId = clock.NextSimId++, Pos = new float3(x, 0f, y), Heading = -1.5708f, Hp = cfg.SoldierHp,
                            Kind = mg ? SoldierKind.MachineGun : SoldierKind.Rifle, State = SoldierState.Deployed,
                        });
                    }
                    ents.Dispose();
                    em.SetComponentData(root, clock);
                    break;
                }
                case CmdType.EmbarkSquad:
                {
                    // D-N27: 20 fusileros y la ametralladora a bordo, repartidos por los tres vagones (7 por vagón), a los dos lados
                    var clock = em.GetComponentData<SimClock>(root);
                    var cfg = em.GetComponentData<CombatConfig>(root).P;
                    var t = em.GetComponentData<TrainState>(root);
                    var arch = em.CreateArchetype(typeof(Soldier));
                    var ents = em.CreateEntity(arch, 21, Allocator.Temp);
                    for (int i = 0; i < 21; i++)
                    {
                        int wagon = i / 7, k = i % 7;
                        var slot = new float2(-4.3f - wagon * 5f + (k - 3) * 0.6f, (k % 2 == 0 ? -0.55f : 0.55f));
                        em.SetComponentData(ents[i], new Soldier
                        {
                            SimId = clock.NextSimId++, Slot = slot, Pos = new float3(t.X + slot.x, Level.TrainH, Level.TrackY + slot.y),
                            Heading = -1.5708f, Hp = cfg.SoldierHp, Kind = i == 10 ? SoldierKind.MachineGun : SoldierKind.Rifle,
                            State = SoldierState.Embarked,
                        });
                    }
                    ents.Dispose();
                    em.SetComponentData(root, clock);
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
            SoldierLogic.Fire(Soldiers, Order, Zombies, ref a, Tick, P, PileDelta, TrainStopped);
            SoldierLogic.Melee(Soldiers, Zombies, ref a, P);
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
            sum += HashMath.Mix((info.GateOpen ? 1UL : 2UL) ^ ((ulong)(uint)info.GoalCell << 8) ^ 0xFEEDUL);
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
