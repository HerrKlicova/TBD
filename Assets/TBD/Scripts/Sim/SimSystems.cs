using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TBD.Sim
{
    /// <summary>
    /// Raíz de los sistemas de simulación. Se actualiza a mano a tick fijo (ver SimDriver), no con el bucle de Unity.
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
                    var ground = em.GetBuffer<CellGround>(root);
                    Level.SetGate(ground, info.GateOpen);
                    break;
                }
                case CmdType.ToggleTrain:
                {
                    var t = em.GetComponentData<TrainState>(root);
                    t.Speed = t.Speed > 0f ? 0f : SimConst.TrainSpeed;
                    em.SetComponentData(root, t);
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
                float z;
                if (roof)
                {
                    p = new float2(rng.NextFloat(Level.RoofMinX + 0.5f, Level.RoofMaxX + 0.5f),
                                   rng.NextFloat(Level.RoofMinY + 0.5f, Level.RoofMaxY + 0.5f));
                    z = Level.RoofH;
                }
                else
                {
                    p = new float2(rng.NextFloat(2f, 14f), rng.NextFloat(8f, 120f));
                    z = 0f;
                }
                em.SetComponentData(ents[i], Zombie.Create(clock.NextSimId++, p, z));
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
            int gx = (int)math.clamp(math.floor(train.X), 1f, SimConst.GridW - 2f);
            int goal = Level.TrackY * SimConst.GridW + gx;
            info.GoalCell = goal;
            info.ForceFlow = false;
            SystemAPI.SetSingleton(info);

            var ground = SystemAPI.GetBuffer<CellGround>(root).Reinterpret<float>().AsNativeArray();
            var dist = SystemAPI.GetBuffer<CellFlowDist>(root).Reinterpret<int>().AsNativeArray();
            var dir = SystemAPI.GetBuffer<CellFlowDir>(root).Reinterpret<byte>().AsNativeArray();
            new FlowJob { Ground = ground, Dist = dist, Dir = dir, Goal = goal }.Run();
        }
    }

    /// <summary>
    /// Campo de flujo por relajación en barridos (determinista, sin colas ni hashes). Los muros y tejados cuestan más,
    /// pero no son infranqueables: la horda intenta cruzarlos y se amontona (GDD §5.7).
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct FlowJob : IJob
    {
        [ReadOnly] public NativeArray<float> Ground;
        public NativeArray<int> Dist;
        public NativeArray<byte> Dir;
        public int Goal;

        const int Inf = 1 << 28;

        public static int2 Off(int d)
        {
            switch (d)
            {
                case 0: return new int2(1, 0);
                case 1: return new int2(1, 1);
                case 2: return new int2(0, 1);
                case 3: return new int2(-1, 1);
                case 4: return new int2(-1, 0);
                case 5: return new int2(-1, -1);
                case 6: return new int2(0, -1);
                default: return new int2(1, -1);
            }
        }

        int EnterCost(int cell, bool diagonal)
        {
            int c = diagonal ? 14 : 10;
            if (Ground[cell] > 1f) c += 40;              // muro o tejado: se puede cruzar, pero cuesta
            return c;
        }

        bool Relax(int x, int y, int dx, int dy)
        {
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= SimConst.GridW || ny >= SimConst.GridH) return false;
            int c = y * SimConst.GridW + x;
            int n = ny * SimConst.GridW + nx;
            int dn = Dist[n];
            if (dn >= Inf) return false;
            int cand = dn + EnterCost(n, dx != 0 && dy != 0);
            if (cand < Dist[c]) { Dist[c] = cand; return true; }
            return false;
        }

        public void Execute()
        {
            int w = SimConst.GridW, h = SimConst.GridH;
            for (int i = 0; i < Dist.Length; i++) Dist[i] = Inf;
            Dist[Goal] = 0;

            for (int iter = 0; iter < 256; iter++)
            {
                bool changed = false;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        changed |= Relax(x, y, -1, 0);
                        changed |= Relax(x, y, -1, -1);
                        changed |= Relax(x, y, 0, -1);
                        changed |= Relax(x, y, 1, -1);
                    }
                for (int y = h - 1; y >= 0; y--)
                    for (int x = w - 1; x >= 0; x--)
                    {
                        changed |= Relax(x, y, 1, 0);
                        changed |= Relax(x, y, 1, 1);
                        changed |= Relax(x, y, 0, 1);
                        changed |= Relax(x, y, -1, 1);
                    }
                if (!changed) break;
            }

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int c = y * w + x;
                    if (c == Goal) { Dir[c] = 8; continue; }
                    int best = Dist[c];
                    int bestDir = 8;
                    for (int d = 0; d < 8; d++)
                    {
                        int2 o = Off(d);
                        int nx = x + o.x, ny = y + o.y;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int dn = Dist[ny * w + nx];
                        if (dn < best) { best = dn; bestDir = d; }   // empate: gana el primero (orden fijo)
                    }
                    Dir[c] = (byte)bestDir;
                }
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
            var ground = SystemAPI.GetBuffer<CellGround>(root).Reinterpret<float>().AsNativeArray();
            var pile = SystemAPI.GetBuffer<CellPile>(root).Reinterpret<int>().AsNativeArray();
            var delta = SystemAPI.GetBuffer<CellPileDelta>(root).Reinterpret<int>().AsNativeArray();
            var flow = SystemAPI.GetBuffer<CellFlowDir>(root).Reinterpret<byte>().AsNativeArray();

            // Doble búfer (regla 5): cada zombi lee la pila del tick anterior y solo escribe deltas (sumas enteras conmutativas).
            var snapshot = new NativeArray<int>(pile.Length, Allocator.Temp);
            snapshot.CopyFrom(pile);

            new ZombieStepJob { Ground = ground, PileRead = snapshot, FlowDir = flow, PileDelta = delta }.Run();
            snapshot.Dispose();
        }
    }

    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public partial struct ZombieStepJob : IJobEntity
    {
        [ReadOnly] public NativeArray<float> Ground;
        [ReadOnly] public NativeArray<int> PileRead;
        [ReadOnly] public NativeArray<byte> FlowDir;
        public NativeArray<int> PileDelta;

        float Surface(int cell)
        {
            return Ground[cell] + math.min(PileRead[cell], SimConst.MaxPile) * SimConst.Thick;
        }

        static int2 CellOf(float2 p)
        {
            return math.clamp((int2)math.floor(p / SimConst.CellSize), new int2(0, 0), new int2(SimConst.GridW - 1, SimConst.GridH - 1));
        }

        static float2 ClampPos(float2 p)
        {
            return math.clamp(p, new float2(0.5f, 0.5f), new float2(SimConst.GridW - 0.5f, SimConst.GridH - 0.5f));
        }

        void Execute(ref Zombie z)
        {
            z.PrevPos = z.Pos;
            z.PrevZ = z.Z;

            int2 c = CellOf(z.Pos);
            int ci = c.y * SimConst.GridW + c.x;

            // ---- caída libre
            if (z.State == ZState.Fall)
            {
                z.Vz -= SimConst.Gravity * SimConst.Dt;
                z.Z += z.Vz * SimConst.Dt;
                z.Pos = ClampPos(z.Pos + z.Dir * (z.Speed * 0.6f * SimConst.Dt));
                int2 c2 = CellOf(z.Pos);
                float s2 = Surface(c2.y * SimConst.GridW + c2.x);
                if (z.Z <= s2) { z.Z = s2; z.Vz = 0f; z.State = ZState.Walk; }
                return;
            }

            float surf = Surface(ci);
            byte d = FlowDir[ci];
            if (d >= 8)
            {
                // en la meta: se queda
                if (z.State == ZState.Settled) { PileDelta[z.SettledCell] -= 1; z.SettledCell = -1; z.State = ZState.Walk; }
                z.Z = surf;
                return;
            }

            int2 off = FlowJob.Off(d);
            int2 n = math.clamp(c + off, new int2(0, 0), new int2(SimConst.GridW - 1, SimConst.GridH - 1));
            int ni = n.y * SimConst.GridW + n.x;
            float rise = Surface(ni) - surf;

            // ---- bloqueado: se apila (GDD §5.7)
            if (rise > SimConst.StepUp)
            {
                if (z.State != ZState.Settled)
                {
                    z.State = ZState.Settled;
                    z.SettledCell = ci;
                    PileDelta[ci] += 1;
                }
                // altura repartida dentro de la pila de forma determinista (por SimId), para que parezca una masa
                uint layers = (uint)math.max(1, math.min(PileRead[ci], SimConst.MaxPile));
                z.Z = Ground[ci] + (math.hash(new uint2(z.SimId, 99u)) % layers) * SimConst.Thick;
                return;
            }

            // ---- puede avanzar
            if (z.State == ZState.Settled)
            {
                PileDelta[z.SettledCell] -= 1;
                z.SettledCell = -1;
                z.State = ZState.Walk;
            }

            float2 dirv = math.normalize(new float2(off.x, off.y));
            // desviación lateral mínima por zombi para que no marchen en fila perfecta
            float side = ((math.hash(new uint2(z.SimId, 31u)) & 255u) / 255f - 0.5f) * 0.25f;
            float2 move = dirv + new float2(-dirv.y, dirv.x) * side;
            z.Dir = math.normalize(move);
            z.Pos = ClampPos(z.Pos + z.Dir * (z.Speed * SimConst.Dt));

            int2 c3 = CellOf(z.Pos);
            float s3 = Surface(c3.y * SimConst.GridW + c3.x);
            if (z.Z - s3 > SimConst.DropThreshold)
            {
                z.State = ZState.Fall;                   // sale al vacío: cae con gravedad
                z.Vz = 0f;
            }
            else
            {
                z.Z = s3;
            }
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

    [BurstCompile]
    public struct PileApplyJob : IJob
    {
        public NativeArray<int> Pile;
        public NativeArray<int> Delta;

        public void Execute()
        {
            for (int i = 0; i < Pile.Length; i++)
            {
                Pile[i] = math.max(0, Pile[i] + Delta[i]);
                Delta[i] = 0;
            }
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
        static ulong Mix(ulong h)
        {
            h ^= h >> 33; h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33; h *= 0xc4ceb9fe1a85ec53UL;
            h ^= h >> 33;
            return h;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<ZoneTag>();
            var clock = SystemAPI.GetSingleton<SimClock>();
            ulong sum = 0;

            foreach (var zr in SystemAPI.Query<RefRO<Zombie>>())
            {
                var z = zr.ValueRO;
                ulong v = Mix(z.SimId + 0x9E3779B97F4A7C15UL);
                v = Mix(v ^ (uint)(int)math.round(z.Pos.x * 64f));
                v = Mix(v ^ ((ulong)(uint)(int)math.round(z.Pos.y * 64f) << 1));
                v = Mix(v ^ ((ulong)(uint)(int)math.round(z.Z * 64f) << 2));
                v = Mix(v ^ ((ulong)z.State << 3));
                sum += v;
            }

            var pile = SystemAPI.GetBuffer<CellPile>(root);
            for (int i = 0; i < pile.Length; i++)
                sum += Mix(((ulong)i * 31UL) + (ulong)pile[i].Value + 0xABCDEFUL);

            var train = SystemAPI.GetSingleton<TrainState>();
            sum += Mix((ulong)(uint)(int)math.round(train.X * 64f) + 0x1234567UL);

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
