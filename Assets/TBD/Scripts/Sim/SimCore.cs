using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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
    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
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

    /// <summary>
    /// Índice espacial uniforme de los sólidos. Cada sólido se registra en todas las cubetas que toca su caja (ampliada por el radio de
    /// un zombi y un margen), de modo que basta consultar la cubeta del zombi. Las listas van en orden ascendente de índice de sólido:
    /// la colisión se resuelve en el mismo orden que con un recorrido lineal, así que el resultado es idéntico.
    /// </summary>
    public static class SolidIndex
    {
        public const float BucketSize = 4f;
        public const int BucketsPerSide = (int)(SimConst.WorldSize / BucketSize);   // 32
        public const int BucketCount = BucketsPerSide * BucketsPerSide;
        const float Margin = SimConst.Radius + 0.25f;

        public static int BucketOf(float2 p)
        {
            int bx = (int)math.clamp(math.floor(p.x / BucketSize), 0f, BucketsPerSide - 1f);
            int by = (int)math.clamp(math.floor(p.y / BucketSize), 0f, BucketsPerSide - 1f);
            return by * BucketsPerSide + bx;
        }

        static void Bounds(Solid s, out int x0, out int y0, out int x1, out int y1)
        {
            float2 mn, mx;
            if (s.Kind == 1) { mn = s.A; mx = s.B; }
            else { mn = math.min(s.A, s.B) - s.HalfThick; mx = math.max(s.A, s.B) + s.HalfThick; }
            mn = mn - Margin;
            mx = mx + Margin;
            x0 = (int)math.clamp(math.floor(mn.x / BucketSize), 0f, BucketsPerSide - 1f);
            y0 = (int)math.clamp(math.floor(mn.y / BucketSize), 0f, BucketsPerSide - 1f);
            x1 = (int)math.clamp(math.floor(mx.x / BucketSize), 0f, BucketsPerSide - 1f);
            y1 = (int)math.clamp(math.floor(mx.y / BucketSize), 0f, BucketsPerSide - 1f);
        }

        /// <summary>Reconstruye el índice (se llama solo cuando cambian los sólidos: al construir).</summary>
        public static void Rebuild(DynamicBuffer<Solid> solids, DynamicBuffer<SolidBucketStart> start, DynamicBuffer<SolidBucketRef> refs)
        {
            var count = new int[BucketCount];
            for (int i = 0; i < solids.Length; i++)
            {
                int x0, y0, x1, y1;
                Bounds(solids[i], out x0, out y0, out x1, out y1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++) count[y * BucketsPerSide + x]++;
            }
            start.ResizeUninitialized(BucketCount + 1);
            int total = 0;
            for (int b = 0; b < BucketCount; b++)
            {
                start[b] = new SolidBucketStart { Value = total };
                total += count[b];
            }
            start[BucketCount] = new SolidBucketStart { Value = total };
            refs.ResizeUninitialized(total);
            var fill = new int[BucketCount];
            for (int i = 0; i < solids.Length; i++)
            {
                int x0, y0, x1, y1;
                Bounds(solids[i], out x0, out y0, out x1, out y1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int b = y * BucketsPerSide + x;
                        refs[start[b].Value + fill[b]] = new SolidBucketRef { Value = i };
                        fill[b]++;
                    }
            }
        }
    }

    /// <summary>Lógica de un zombi en un tick. Lee solo del estado del tick anterior (doble búfer) y escribe deltas enteros de pila.</summary>
    public static class ZombieLogic
    {
        const float TwoPi = 6.2831853f;

        public static float GroundAt(NativeArray<Solid> solids, NativeArray<int> bStart, NativeArray<int> bRefs, float2 p)
        {
            float h = 0f;
            int b = SolidIndex.BucketOf(p);
            for (int k = bStart[b]; k < bStart[b + 1]; k++)
            {
                var s = solids[bRefs[k]];
                if (SolidMath.Contains(s, p)) h = math.max(h, s.Height);
            }
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

        static int CellIndex(float2 p)
        {
            int2 c = math.clamp((int2)math.floor(p / SimConst.CellSize), new int2(0, 0), new int2(SimConst.GridW - 1, SimConst.GridH - 1));
            return c.y * SimConst.GridW + c.x;
        }

        /// <summary>Cuenta un zombi en la rejilla de densidad (sumas enteras: el resultado no depende del orden). Los que caen no cuentan.</summary>
        public static void AddDensity(in Zombie z, NativeArray<int> density)
        {
            if (z.State == ZState.Fall || z.State == ZState.Dead) return;
            density[CellIndex(z.Pos.xz)] += 1;
        }

        // Exceso de densidad de una celda: un zombi solo no se empuja a sí mismo.
        static float DensityCell(NativeArray<int> density, int2 c)
        {
            c = math.clamp(c, new int2(0, 0), new int2(SimConst.GridW - 1, SimConst.GridH - 1));
            return math.max(0, density[c.y * SimConst.GridW + c.x] - 1);
        }

        static float DensityAt(NativeArray<int> density, float2 p)
        {
            float2 u = p / SimConst.CellSize - 0.5f;
            int2 i0 = (int2)math.floor(u);
            float2 f = u - (float2)i0;
            float a = math.lerp(DensityCell(density, i0), DensityCell(density, i0 + new int2(1, 0)), f.x);
            float b = math.lerp(DensityCell(density, i0 + new int2(0, 1)), DensityCell(density, i0 + new int2(1, 1)), f.x);
            return math.lerp(a, b, f.y);
        }

        /// <summary>
        /// Separación entre agentes sin consultar vecinos: velocidad hacia donde baja la densidad del tick anterior (gradiente de la
        /// rejilla interpolada). Evita que la horda se colapse en filas sobre las líneas del campo de flujo. Determinista: solo lee.
        /// </summary>
        public static float2 SeparationAt(NativeArray<int> density, float2 p)
        {
            float h = SimConst.CellSize;
            float gx = DensityAt(density, p + new float2(h, 0f)) - DensityAt(density, p - new float2(h, 0f));
            float gy = DensityAt(density, p + new float2(0f, h)) - DensityAt(density, p - new float2(0f, h));
            float2 v = new float2(-gx, -gy) * (SimConst.SepGain / (2f * h));
            float l = math.length(v);
            return l > SimConst.SepMax ? v * (SimConst.SepMax / l) : v;
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

        public static void Step(ref Zombie z, NativeArray<Solid> solids, NativeArray<int> bStart, NativeArray<int> bRefs,
                                NativeArray<int> pileRead, NativeArray<float2> flowVec, NativeArray<int> pileDelta,
                                NativeArray<int> density)
        {
            if (z.State == ZState.Dead) return;          // abatido: no se mueve (se retira al final del tick)
            z.PrevPos = z.Pos;
            z.PrevHeading = z.Heading;
            float2 p = z.Pos.xz;

            // ---- caída libre
            if (z.State == ZState.Fall)
            {
                z.Vel.y -= SimConst.Gravity * SimConst.Dt;
                float3 np3 = z.Pos + z.Vel * SimConst.Dt;
                float2 q = ClampWorld(np3.xz);
                float surface = GroundAt(solids, bStart, bRefs, q) + PileAt(pileRead, q);
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
            float2 steer = desired + SeparationAt(density, p);      // la separación no cuenta como «empujar contra el muro»
            float2 vel2 = math.lerp(z.Vel.xz, steer, SimConst.Accel);
            float2 intended = vel2 * SimConst.Dt;
            float2 np = p + intended;
            float standing = GroundAt(solids, bStart, bRefs, p) + PileAt(pileRead, p);   // altura de la superficie bajo el zombi
            // Un zombi apilado solo puede salir por encima del muro si está en las capas altas de la pila (su propia altura);
            // si todos usaran la altura de la pila, saldrían todos a la vez y la pila oscilaría entre llena y vacía cada tick.
            float reach = z.State == ZState.Settled ? z.Pos.y : standing;

            // ---- colisión cápsula contra sólidos (muros con grosor y altura), no por celda
            bool hit = false;
            float2 hitN = new float2(0f, 0f);
            int bucket = SolidIndex.BucketOf(np);
            for (int k = bStart[bucket]; k < bStart[bucket + 1]; k++)
            {
                var s = solids[bRefs[k]];
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
                float h = GroundAt(solids, bStart, bRefs, np) + (math.hash(new uint2(z.SimId, 99u)) % layers) * SimConst.Thick;
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
            float g0 = GroundAt(solids, bStart, bRefs, p);
            float g1 = GroundAt(solids, bStart, bRefs, np);
            float s2 = g1 + PileAt(pileRead, np);
            if (g0 - g1 > SimConst.DropThreshold)
            {
                z.Pos = new float3(np.x, z.Pos.y, np.y);               // sale al vacío: cae con gravedad
                z.State = ZState.Fall;
            }
            else
            {
                float dy = math.clamp(s2 - z.Pos.y, -SimConst.SlideSpeed * SimConst.Dt, SimConst.ClimbSpeed * SimConst.Dt);
                // Nunca dentro de un sólido por debajo del escalón: si la pila le dejó pasar por encima del muro, ya está arriba
                // (la subida limitada se quedaba atrás un tick cuando la separación lo empujaba hacia el muro).
                float y = math.max(z.Pos.y + dy, g1 - SimConst.StepUp);
                z.Pos = new float3(np.x, y, np.y);
            }

            // ---- orientación continua: gira suavemente hacia donde avanza
            if (math.lengthsq(vel2) > 0.0025f)
                z.Heading = ApproachAngle(z.Heading, math.atan2(vel2.x, vel2.y), SimConst.TurnRate * SimConst.Dt);
        }
    }

    /// <summary>
    /// Combate de las tropas en la zona (hito 3, parte 4). Lógica pura y determinista: los soldados se procesan en orden de SimId,
    /// cada disparo busca el zombi vivo más cercano dentro del alcance (empate: el de SimId menor) y el acierto sale de un hash del
    /// soldado y del tick, no de un generador compartido. Lo prueba dotnet (P7 de Hugo).
    /// </summary>
    public static class SoldierLogic
    {
        /// <summary>Un tick de disparos. Devuelve cuántos zombis se abatieron. «order» = índices de soldados ordenados por SimId.</summary>
        public static int Fire(NativeArray<Soldier> soldiers, NativeArray<int> order, NativeArray<Zombie> zombies, ref ZoneAmmo ammo, uint tick, in CombatParams cp, NativeArray<int> pileDelta, bool trainStopped = false)
        {
            int kills = 0;
            for (int o = 0; o < order.Length; o++)
            {
                int si = order[o];
                var s = soldiers[si];
                bool canFire = s.State == SoldierState.Deployed || (s.State == SoldierState.Embarked && trainStopped);   // D-N27: a bordo, solo con el tren parado
                if (!canFire) continue;
                if (s.Cooldown > 0) { s.Cooldown--; soldiers[si] = s; continue; }
                float range = s.Kind == SoldierKind.MachineGun ? cp.MgRange : cp.RifleRange;
                int best = -1;
                float bestD2 = range * range;
                uint bestId = uint.MaxValue;
                for (int i = 0; i < zombies.Length; i++)
                {
                    var z = zombies[i];
                    if (z.State == ZState.Dead) continue;
                    float2 d = z.Pos.xz - s.Pos.xz;
                    float d2 = math.dot(d, d);
                    if (d2 < bestD2 || (d2 == bestD2 && z.SimId < bestId)) { bestD2 = d2; best = i; bestId = z.SimId; }
                }
                if (best < 0 || ammo.ShotsLeft <= 0) { soldiers[si] = s; continue; }     // sin objetivo no se dispara (Hugo)
                ammo.ShotsLeft--;
                ammo.ShotsFired++;
                s.Cooldown = s.Kind == SoldierKind.MachineGun ? cp.MgCooldown : cp.RifleCooldown;
                var tz = zombies[best];
                float2 dv = tz.Pos.xz - s.Pos.xz;
                s.Heading = math.atan2(dv.x, dv.y);
                s.AimPoint = tz.Pos;
                s.LastHit = 0;
                uint roll = math.hash(new uint2(s.SimId * 0x9E3779B1u ^ 0x5EEDu, tick)) % 1000u;
                if (roll < (uint)cp.HitPermil(s.Kind, math.sqrt(bestD2)))
                {
                    if (tz.State == ZState.Settled && tz.SettledCell >= 0 && pileDelta.Length > 0) pileDelta[tz.SettledCell] -= 1;   // deja la pila
                    tz.State = ZState.Dead;
                    s.LastHit = 1;
                    tz.SettledCell = -1;
                    tz.Vel = float3.zero;
                    zombies[best] = tz;
                    kills++;
                    ammo.Kills++;
                }
                soldiers[si] = s;
            }
            return kills;
        }

        /// <summary>Cuerpo a cuerpo: cada zombi vivo a menos de MeleeRange de un soldado le quita vida. Devuelve los soldados caídos este tick.</summary>
        public static int Melee(NativeArray<Soldier> soldiers, NativeArray<Zombie> zombies, ref ZoneAmmo ammo, in CombatParams cp)
        {
            int fallen = 0;
            float r2 = cp.MeleeRange * cp.MeleeRange;
            for (int si = 0; si < soldiers.Length; si++)
            {
                var s = soldiers[si];
                if (s.State != SoldierState.Deployed) continue;              // a bordo (D-N27): tras el muro móvil, sin cuerpo a cuerpo
                int touching = 0;
                for (int i = 0; i < zombies.Length; i++)
                {
                    var z = zombies[i];
                    if (z.State == ZState.Dead || z.State == ZState.Fall) continue;
                    float2 d = z.Pos.xz - s.Pos.xz;
                    if (math.dot(d, d) <= r2) touching++;
                }
                if (touching == 0) continue;
                s.Hp -= touching * cp.ZombieDamagePerTick;
                if (s.Hp <= 0) { s.Hp = 0; s.State = SoldierState.Dead; fallen++; ammo.SoldiersLost++; }
                soldiers[si] = s;
            }
            return fallen;
        }

        /// <summary>Orden por SimId (inserción: son pocos soldados).</summary>
        public static void SortById(NativeArray<Soldier> soldiers, NativeArray<int> order)
        {
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = 1; i < order.Length; i++)
            {
                int v = order[i], j = i - 1;
                while (j >= 0 && soldiers[order[j]].SimId > soldiers[v].SimId) { order[j + 1] = order[j]; j--; }
                order[j + 1] = v;
            }
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

        // QA Q2: el hash usa los BITS de cada campo (math.asuint), no posiciones redondeadas: ve una diferencia de 1 mm o de un
        // solo bit de coma flotante, que es justo lo que delata una desincronización entre máquinas.
        static ulong F(ulong h, float v, int salt) { return Mix(h ^ ((ulong)math.asuint(v) << (salt & 31)) ^ ((ulong)(uint)salt << 40)); }

        public static ulong OfZombie(in Zombie z)
        {
            ulong v = Mix(z.SimId + 0x9E3779B97F4A7C15UL);
            v = F(v, z.Pos.x, 1); v = F(v, z.Pos.y, 2); v = F(v, z.Pos.z, 3);
            v = F(v, z.Vel.x, 4); v = F(v, z.Vel.y, 5); v = F(v, z.Vel.z, 6);
            v = F(v, z.Heading, 7); v = F(v, z.Speed, 8);
            v = Mix(v ^ ((ulong)z.State << 48) ^ (ulong)(uint)z.SettledCell);
            return v;
        }

        public static ulong OfSolid(int i, in Solid s)
        {
            ulong v = Mix((ulong)(uint)i * 0x100000001B3UL + 0x5151UL);
            v = F(v, s.A.x, 11); v = F(v, s.A.y, 12); v = F(v, s.B.x, 13); v = F(v, s.B.y, 14);
            v = F(v, s.HalfThick, 15); v = F(v, s.Height, 16);
            return Mix(v ^ ((ulong)s.Kind << 8) ^ ((ulong)s.Open << 16) ^ ((ulong)s.Id << 24));
        }

        public static ulong OfSoldier(in Soldier s)
        {
            ulong v = Mix(s.SimId + 0x50D1E8UL);
            v = F(v, s.Pos.x, 31); v = F(v, s.Pos.y, 32); v = F(v, s.Pos.z, 33); v = F(v, s.Heading, 34);
            v = F(v, s.Slot.x, 35); v = F(v, s.Slot.y, 36);
            v = F(v, s.AimPoint.x, 37); v = F(v, s.AimPoint.z, 38); v = Mix(v ^ s.LastHit);
            return Mix(v ^ ((ulong)(uint)s.Hp << 8) ^ ((ulong)(uint)s.Cooldown << 24) ^ ((ulong)s.Kind << 40) ^ ((ulong)s.State << 48));
        }

        public static ulong OfAmmo(in ZoneAmmo a)
        {
            return Mix((ulong)a.ShotsLeft * 31UL + 0xA770UL) + Mix((ulong)a.ShotsFired + 0xF12EUL) + Mix(((ulong)(uint)a.Kills << 20) ^ (ulong)(uint)a.SoldiersLost);
        }

        public static ulong OfFloat(float value, int salt) { return F(Mix((ulong)(uint)salt), value, salt); }

        public static ulong OfPileCell(int i, int value)
        {
            return Mix(((ulong)i * 31UL) + (ulong)value + 0xABCDEFUL);
        }
    }
}
