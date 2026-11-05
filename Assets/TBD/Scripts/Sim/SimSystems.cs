using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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

    /// <summary>
    /// Campo de flujo por relajación en barridos (determinista, sin colas ni hashes). Los muros cuestan más, pero no son
    /// infranqueables: la horda intenta cruzarlos y se amontona (GDD §5.7). Solo produce una dirección deseada por celda
    /// (gradiente del coste); los agentes la interpolan bilinealmente y se mueven en continuo.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct FlowJob : IJob
    {
        [ReadOnly] public NativeArray<float> Ground;
        public NativeArray<int> Dist;
        public NativeArray<float2> Vec;
        public int Goal;

        const int Inf = 1 << 28;

        static int2 Off(int d)
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

            // dirección deseada = descenso del coste (suma de las direcciones a los vecinos más baratos, ponderada por la bajada)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int c = y * w + x;
                    if (c == Goal || Dist[c] >= Inf) { Vec[c] = new float2(0f, 0f); continue; }
                    float2 g = new float2(0f, 0f);
                    for (int d = 0; d < 8; d++)
                    {
                        int2 o = Off(d);
                        int nx = x + o.x, ny = y + o.y;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int dn = Dist[ny * w + nx];
                        if (dn >= Inf) continue;
                        float gain = (float)(Dist[c] - dn);
                        if (gain <= 0f) continue;
                        g += math.normalize(new float2(o.x, o.y)) * gain;
                    }
                    float l = math.length(g);
                    Vec[c] = l > 1e-4f ? g / l : new float2(0f, 0f);
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
            var solids = SystemAPI.GetBuffer<Solid>(root).AsNativeArray();
            var pile = SystemAPI.GetBuffer<CellPile>(root).Reinterpret<int>().AsNativeArray();
            var delta = SystemAPI.GetBuffer<CellPileDelta>(root).Reinterpret<int>().AsNativeArray();
            var flow = SystemAPI.GetBuffer<CellFlowVec>(root).Reinterpret<float2>().AsNativeArray();

            // Doble búfer (regla 5): cada zombi lee la pila del tick anterior y solo escribe deltas (sumas enteras conmutativas).
            var snapshot = new NativeArray<int>(pile.Length, Allocator.Temp);
            snapshot.CopyFrom(pile);

            new ZombieStepJob { Solids = solids, PileRead = snapshot, FlowVec = flow, PileDelta = delta }.Run();
            snapshot.Dispose();
        }
    }

    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public partial struct ZombieStepJob : IJobEntity
    {
        [ReadOnly] public NativeArray<Solid> Solids;
        [ReadOnly] public NativeArray<int> PileRead;
        [ReadOnly] public NativeArray<float2> FlowVec;
        public NativeArray<int> PileDelta;

        const float TwoPi = 6.2831853f;

        float GroundAt(float2 p)
        {
            float h = 0f;
            for (int i = 0; i < Solids.Length; i++)
                if (SolidMath.Contains(Solids[i], p)) h = math.max(h, Solids[i].Height);
            return h;
        }

        float PileCell(int2 c)
        {
            c = math.clamp(c, new int2(0, 0), new int2(SimConst.GridW - 1, SimConst.GridH - 1));
            return math.min(PileRead[c.y * SimConst.GridW + c.x], SimConst.MaxPile) * SimConst.Thick;
        }

        /// <summary>Altura de pila interpolada entre celdas (GDD §9c): sin escalones de rejilla.</summary>
        float PileAt(float2 p)
        {
            float2 u = p / SimConst.CellSize - 0.5f;
            int2 i0 = (int2)math.floor(u);
            float2 f = u - (float2)i0;
            float a = math.lerp(PileCell(i0), PileCell(i0 + new int2(1, 0)), f.x);
            float b = math.lerp(PileCell(i0 + new int2(0, 1)), PileCell(i0 + new int2(1, 1)), f.x);
            return math.lerp(a, b, f.y);
        }

        float2 FlowCell(int2 c)
        {
            c = math.clamp(c, new int2(0, 0), new int2(SimConst.GridW - 1, SimConst.GridH - 1));
            return FlowVec[c.y * SimConst.GridW + c.x];
        }

        /// <summary>Dirección deseada interpolada bilinealmente entre celdas: nada de ir de centro de celda a centro de celda.</summary>
        float2 FlowAt(float2 p)
        {
            float2 u = p / SimConst.CellSize - 0.5f;
            int2 i0 = (int2)math.floor(u);
            float2 f = u - (float2)i0;
            float2 a = math.lerp(FlowCell(i0), FlowCell(i0 + new int2(1, 0)), f.x);
            float2 b = math.lerp(FlowCell(i0 + new int2(0, 1)), FlowCell(i0 + new int2(1, 1)), f.x);
            return math.lerp(a, b, f.y);
        }

        static float ApproachAngle(float cur, float target, float maxStep)
        {
            float d = target - cur;
            d -= TwoPi * math.floor((d + 3.14159265f) / TwoPi);
            return cur + math.clamp(d, -maxStep, maxStep);
        }

        static float2 ClampWorld(float2 p)
        {
            return math.clamp(p, new float2(0.5f, 0.5f), new float2(SimConst.WorldSize - 0.5f, SimConst.WorldSize - 0.5f));
        }

        void Execute(ref Zombie z)
        {
            z.PrevPos = z.Pos;
            z.PrevHeading = z.Heading;
            float2 p = z.Pos.xz;

            // ---- caída libre
            if (z.State == ZState.Fall)
            {
                z.Vel.y -= SimConst.Gravity * SimConst.Dt;
                float3 np3 = z.Pos + z.Vel * SimConst.Dt;
                float2 q = ClampWorld(np3.xz);
                float s = GroundAt(q) + PileAt(q);
                if (np3.y <= s)
                {
                    z.Pos = new float3(q.x, s, q.y);
                    z.Vel = new float3(z.Vel.x * 0.3f, 0f, z.Vel.z * 0.3f);
                    z.State = ZState.Walk;
                }
                else
                {
                    z.Pos = new float3(q.x, np3.y, q.y);
                }
                return;
            }

            // ---- marcha en continuo: velocidad deseada = dirección del flujo interpolada
            float2 desired = FlowAt(p) * z.Speed;
            float2 vel2 = math.lerp(z.Vel.xz, desired, SimConst.Accel);
            float2 intended = vel2 * SimConst.Dt;
            float2 np = p + intended;
            float standing = GroundAt(p) + PileAt(p);            // altura de la superficie bajo el zombi

            // ---- colisión cápsula contra sólidos (muros con grosor y altura), no por celda
            bool hit = false;
            for (int i = 0; i < Solids.Length; i++)
            {
                var s = Solids[i];
                if (s.Open != 0) continue;
                if (s.Height <= standing + SimConst.StepUp) continue;      // se puede subir o pasar por encima
                float2 n;
                float depth;
                if (SolidMath.Push(s, np, SimConst.Radius, out n, out depth))
                {
                    np += n * depth;
                    hit = true;
                    float vn = math.dot(vel2, n);
                    if (vn < 0f) vel2 -= n * vn;                          // desliza a lo largo del muro
                }
            }
            np = ClampWorld(np);

            float wanted = math.length(intended);
            float moved = math.length(np - p);
            bool blocked = hit && wanted > 1e-4f && moved < 0.3f * wanted;

            // ---- bloqueado: se apila (GDD §5.7)
            if (blocked)
            {
                int2 cc = Level.CellOf(p);
                int ci = cc.y * SimConst.GridW + cc.x;
                if (z.State != ZState.Settled)
                {
                    z.State = ZState.Settled;
                    z.SettledCell = ci;
                    PileDelta[ci] += 1;
                }
                z.Vel = float3.zero;
                uint layers = (uint)math.max(1, math.min(PileRead[ci], SimConst.MaxPile));
                float h = GroundAt(np) + (math.hash(new uint2(z.SimId, 99u)) % layers) * SimConst.Thick;
                z.Pos = new float3(np.x, h, np.y);
                return;
            }

            if (z.State == ZState.Settled)
            {
                PileDelta[z.SettledCell] -= 1;
                z.SettledCell = -1;
                z.State = ZState.Walk;
            }

            z.Vel = new float3(vel2.x, 0f, vel2.y);
            float s2 = GroundAt(np) + PileAt(np);
            if (z.Pos.y - s2 > SimConst.DropThreshold)
            {
                z.Pos = new float3(np.x, z.Pos.y, np.y);               // sale al vacío: cae con gravedad
                z.State = ZState.Fall;
            }
            else
            {
                z.Pos = new float3(np.x, s2, np.y);
            }

            // ---- orientación continua: gira suavemente hacia donde avanza
            if (math.lengthsq(vel2) > 0.0025f)
                z.Heading = ApproachAngle(z.Heading, math.atan2(vel2.x, vel2.y), SimConst.TurnRate * SimConst.Dt);
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
                v = Mix(v ^ (uint)(int)math.round(z.Pos.x * 256f));
                v = Mix(v ^ ((ulong)(uint)(int)math.round(z.Pos.y * 256f) << 1));
                v = Mix(v ^ ((ulong)(uint)(int)math.round(z.Pos.z * 256f) << 2));
                v = Mix(v ^ ((ulong)(uint)(int)math.round(z.Heading * 256f) << 3));
                v = Mix(v ^ ((ulong)z.State << 4));
                sum += v;
            }

            var pile = SystemAPI.GetBuffer<CellPile>(root);
            for (int i = 0; i < pile.Length; i++)
                sum += Mix(((ulong)i * 31UL) + (ulong)pile[i].Value + 0xABCDEFUL);

            var solids = SystemAPI.GetBuffer<Solid>(root);
            sum += Mix((ulong)solids.Length + 0x7777UL);

            var train = SystemAPI.GetSingleton<TrainState>();
            sum += Mix((ulong)(uint)(int)math.round(train.X * 256f) + 0x1234567UL);

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
