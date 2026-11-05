using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TBD.Sim
{
    // Lógica pura de la simulación, sin ECS: se compila también fuera de Unity con dotnet (carpeta Dotnet/) para probarla con tests
    // usando mínimos shims de Unity.Mathematics y Unity.Collections. Los sistemas de SimSystems.cs solo la llaman.

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

    /// <summary>Lógica de un zombi en un tick. Lee solo del estado del tick anterior (doble búfer) y escribe deltas enteros de pila.</summary>
    public static class ZombieLogic
    {
        const float TwoPi = 6.2831853f;

        public static float GroundAt(NativeArray<Solid> solids, float2 p)
        {
            float h = 0f;
            for (int i = 0; i < solids.Length; i++)
                if (SolidMath.Contains(solids[i], p)) h = math.max(h, solids[i].Height);
            return h;
        }

        static float PileCell(NativeArray<int> pile, int2 c)
        {
            c = math.clamp(c, new int2(0, 0), new int2(SimConst.GridW - 1, SimConst.GridH - 1));
            return math.min(pile[c.y * SimConst.GridW + c.x], SimConst.MaxPile) * SimConst.Thick;
        }

        /// <summary>Altura de pila interpolada entre celdas (GDD §9c): sin escalones de rejilla.</summary>
        public static float PileAt(NativeArray<int> pile, float2 p)
        {
            float2 u = p / SimConst.CellSize - 0.5f;
            int2 i0 = (int2)math.floor(u);
            float2 f = u - (float2)i0;
            float a = math.lerp(PileCell(pile, i0), PileCell(pile, i0 + new int2(1, 0)), f.x);
            float b = math.lerp(PileCell(pile, i0 + new int2(0, 1)), PileCell(pile, i0 + new int2(1, 1)), f.x);
            return math.lerp(a, b, f.y);
        }

        static float2 FlowCell(NativeArray<float2> flow, int2 c)
        {
            c = math.clamp(c, new int2(0, 0), new int2(SimConst.GridW - 1, SimConst.GridH - 1));
            return flow[c.y * SimConst.GridW + c.x];
        }

        /// <summary>Dirección deseada interpolada bilinealmente entre celdas: nada de ir de centro de celda a centro de celda.</summary>
        public static float2 FlowAt(NativeArray<float2> flow, float2 p)
        {
            float2 u = p / SimConst.CellSize - 0.5f;
            int2 i0 = (int2)math.floor(u);
            float2 f = u - (float2)i0;
            float2 a = math.lerp(FlowCell(flow, i0), FlowCell(flow, i0 + new int2(1, 0)), f.x);
            float2 b = math.lerp(FlowCell(flow, i0 + new int2(0, 1)), FlowCell(flow, i0 + new int2(1, 1)), f.x);
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

        public static void Step(ref Zombie z, NativeArray<Solid> solids, NativeArray<int> pileRead,
                                NativeArray<float2> flowVec, NativeArray<int> pileDelta)
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
                float surface = GroundAt(solids, q) + PileAt(pileRead, q);
                if (np3.y <= surface)
                {
                    z.Pos = new float3(q.x, surface, q.y);
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
            float2 desired = FlowAt(flowVec, p) * z.Speed;
            float2 vel2 = math.lerp(z.Vel.xz, desired, SimConst.Accel);
            float2 intended = vel2 * SimConst.Dt;
            float2 np = p + intended;
            float standing = GroundAt(solids, p) + PileAt(pileRead, p);   // altura de la superficie bajo el zombi
            // Un zombi apilado solo puede salir por encima del muro si está en las capas altas de la pila (su propia altura);
            // si todos usaran la altura de la pila, saldrían todos a la vez y la pila oscilaría entre llena y vacía cada tick.
            float reach = z.State == ZState.Settled ? z.Pos.y : standing;

            // ---- colisión cápsula contra sólidos (muros con grosor y altura), no por celda
            bool hit = false;
            float2 hitN = new float2(0f, 0f);
            for (int i = 0; i < solids.Length; i++)
            {
                var s = solids[i];
                if (s.Open != 0) continue;
                if (s.Height <= reach + SimConst.StepUp) continue;         // se puede subir o pasar por encima
                float2 n;
                float depth;
                if (SolidMath.Push(s, np, SimConst.Radius, out n, out depth))
                {
                    np += n * depth;
                    hit = true;
                    hitN += n;
                    float vn = math.dot(vel2, n);
                    if (vn < 0f) vel2 -= n * vn;                          // desliza a lo largo del muro
                }
            }
            np = ClampWorld(np);

            float wanted = math.length(intended);
            float moved = math.length(np - p);
            // Bloqueado: no avanza (menos del 30 % de lo previsto) o la horda lo empuja de frente contra el muro (el flujo apunta
            // al muro, aunque deslice un poco). Así la presión de la masa forma la pila en cada punto del muro, no solo en la puerta.
            float dl = math.length(desired);
            float hl = math.length(hitN);
            bool pressing = hit && dl > 1e-4f && hl > 1e-4f && math.dot(desired / dl, hitN / hl) < -0.6f;
            bool blocked = hit && wanted > 1e-4f && (moved < 0.3f * wanted || pressing);

            // ---- bloqueado: se apila (GDD §5.7)
            if (blocked)
            {
                int2 cc = math.clamp((int2)math.floor(p / SimConst.CellSize), new int2(0, 0), new int2(SimConst.GridW - 1, SimConst.GridH - 1));
                int ci = cc.y * SimConst.GridW + cc.x;
                if (z.State != ZState.Settled)
                {
                    z.State = ZState.Settled;
                    z.SettledCell = ci;
                    pileDelta[ci] += 1;
                }
                z.Vel = float3.zero;
                uint layers = (uint)math.max(1, math.min(pileRead[ci], SimConst.MaxPile));
                float h = GroundAt(solids, np) + (math.hash(new uint2(z.SimId, 99u)) % layers) * SimConst.Thick;
                z.Pos = new float3(np.x, h, np.y);
                return;
            }

            if (z.State == ZState.Settled)
            {
                pileDelta[z.SettledCell] -= 1;
                z.SettledCell = -1;
                z.State = ZState.Walk;
            }

            z.Vel = new float3(vel2.x, 0f, vel2.y);
            // La caída se decide por el desnivel ESTÁTICO del terreno (borde de tejado o de muro), no por la pila: la pila es
            // una masa de zombis y se baja deslizando. La altura sigue la superficie con una velocidad vertical limitada.
            float g0 = GroundAt(solids, p);
            float g1 = GroundAt(solids, np);
            float s2 = g1 + PileAt(pileRead, np);
            if (g0 - g1 > SimConst.DropThreshold)
            {
                z.Pos = new float3(np.x, z.Pos.y, np.y);               // sale al vacío: cae con gravedad
                z.State = ZState.Fall;
            }
            else
            {
                float dy = math.clamp(s2 - z.Pos.y, -SimConst.SlideSpeed * SimConst.Dt, SimConst.ClimbSpeed * SimConst.Dt);
                z.Pos = new float3(np.x, z.Pos.y + dy, np.y);
            }

            // ---- orientación continua: gira suavemente hacia donde avanza
            if (math.lengthsq(vel2) > 0.0025f)
                z.Heading = ApproachAngle(z.Heading, math.atan2(vel2.x, vel2.y), SimConst.TurnRate * SimConst.Dt);
        }
    }

    /// <summary>Hash del estado por tick (regla 7). Suma de hashes por elemento: no depende del orden de recorrido.</summary>
    public static class HashMath
    {
        public static ulong Mix(ulong h)
        {
            h ^= h >> 33; h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33; h *= 0xc4ceb9fe1a85ec53UL;
            h ^= h >> 33;
            return h;
        }

        public static ulong OfZombie(in Zombie z)
        {
            ulong v = Mix(z.SimId + 0x9E3779B97F4A7C15UL);
            v = Mix(v ^ (uint)(int)math.round(z.Pos.x * 256f));
            v = Mix(v ^ ((ulong)(uint)(int)math.round(z.Pos.y * 256f) << 1));
            v = Mix(v ^ ((ulong)(uint)(int)math.round(z.Pos.z * 256f) << 2));
            v = Mix(v ^ ((ulong)(uint)(int)math.round(z.Heading * 256f) << 3));
            v = Mix(v ^ ((ulong)z.State << 4));
            return v;
        }

        public static ulong OfPileCell(int i, int value)
        {
            return Mix(((ulong)i * 31UL) + (ulong)value + 0xABCDEFUL);
        }
    }
}
