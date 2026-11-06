using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace TBD.Sim
{
    /// <summary>
    /// Constantes de la simulación. Nada de aquí depende de Time.deltaTime (regla 1 del GDD §17.3).
    /// La rejilla interna es FINA (0,5 m) y solo guarda datos agregados: coste, dirección deseada y altura de pila.
    /// Los agentes y los muros NO viven en la rejilla: posición, velocidad y orientación son continuas (GDD §9c, v1.10).
    /// </summary>
    public static class SimConst
    {
        public const int TickRate = 30;                 // PROTOTIPO: 20-30 Hz
        public const float Dt = 1f / TickRate;
        public const float CellSize = 0.5f;             // metros por celda de la rejilla interna (ajustable)
        public const int GridW = 512;                   // zona de 256 x 128 m (D-N05); la vía va por el eje largo (X)
        public const int GridH = 256;
        public const float WorldW = GridW * CellSize;   // 256 m
        public const float WorldH = GridH * CellSize;   // 128 m

        public const float Radius = 0.25f;              // radio de la cápsula de un zombi
        public const float Thick = 0.35f;               // altura que añade un zombi apilado
        public const int MaxPile = 10;                  // tope de la pila por celda
        public const float StepUp = 0.6f;               // desnivel máximo que se sube andando
        public const float DropThreshold = 1.0f;        // desnivel a partir del cual se cae
        public const float Gravity = 20f;               // m/s^2
        public const float ClimbSpeed = 6f;             // velocidad vertical máxima al subir por una pila o un escalón (m/s)
        public const float SlideSpeed = 9f;             // velocidad vertical máxima al bajar deslizando por una pila (m/s)
        public const float TurnRate = 8f;               // rad/s al girar hacia la marcha
        public const float Accel = 0.25f;               // mezcla de velocidad por tick hacia la deseada
        public const int FlowEveryTicks = 15;           // el campo de flujo se recalcula cada 15 ticks
        public const float SepGain = 0.35f;              // separación por densidad: m/s por (zombi extra por celda / m)
        public const float SepMax = 1.0f;               // tope de la velocidad de separación (m/s)
        public const int InputDelayTicks = 2;           // retardo de entrada de las órdenes (lockstep)
        public const float TrainSpeed = 3f;             // m/s
    }

    public static class ZState
    {
        public const byte Walk = 0;
        public const byte Fall = 1;
        public const byte Settled = 2;                  // bloqueado contra un muro: forma parte de la pila
        public const byte Dead = 3;                     // abatido: se retira al final del tick (hito 3, combate)
    }

    // ------------------------------------------------------------------ tropas (hito 3, parte 4)
    public static class SoldierKind
    {
        public const byte Rifle = 0;
        public const byte MachineGun = 1;
    }

    public static class SoldierState
    {
        public const byte Embarked = 0;                 // en el tren
        public const byte Deployed = 1;                 // desplegado en la zona
        public const byte Dead = 2;                     // muerto: se levanta como zombi (D-N09 de Lucía, GDD 5.6)
    }

    /// <summary>Un soldado. Posición continua; dispara con cadencia en ticks; la munición es del tren (ZoneAmmo), no suya.</summary>
    public struct Soldier : IComponentData
    {
        public uint SimId;
        public float3 Pos;
        public float Heading;
        public int Hp;
        public int Cooldown;                             // ticks hasta el siguiente disparo
        public byte Kind;
        public byte State;
        public float2 Slot;                              // a bordo: posición relativa al tren (dx a lo largo de la vía, dz a un lado)
        public float3 AimPoint;                          // dónde apuntó el último disparo (lo usa la vista para trazadora e impacto)
        public byte LastHit;                             // 1 si el último disparo abatió
    }

    /// <summary>
    /// Munición de la zona en «disparos equivalentes» (Hugo, D-N10 y D-N21: 1 u = 2.000). Es la carga del tren: lo gastado vuelve a la
    /// red como consumo. Abatidos y disparos se cuentan para calibrar el combate abstracto S0 (P7).
    /// </summary>
    public struct ZoneAmmo : IComponentData
    {
        public long ShotsLeft;
        public long ShotsFired;
        public int Kills;
        public int SoldiersLost;
    }

    /// <summary>
    /// Parámetros del combate de la zona. Los valores salen de Assets/TBD/Data/balance_combate.csv (Hugo los ajusta sin tocar C#;
    /// decisión de Mak); aquí solo están los de por defecto, iguales a los del CSV. Es un struct sin referencias: entra en Burst.
    /// </summary>
    public struct CombatParams
    {
        public int ShotsPerUnit, RifleCooldown, MgCooldown, SoldierHp, ZombieDamagePerTick;
        public float RifleRange, MgRange, MeleeRange;
        public int HitPointBlankPermil, HitSlopePermilPerM, HitMinPermil, MgFactorPermil;

        public static CombatParams Default => new CombatParams
        {
            ShotsPerUnit = 2000, RifleCooldown = 30, MgCooldown = 4, SoldierHp = 100, ZombieDamagePerTick = 2,
            RifleRange = 60f, MgRange = 70f, MeleeRange = 0.9f,
            HitPointBlankPermil = 420, HitSlopePermilPerM = 6, HitMinPermil = 60, MgFactorPermil = 600,
        };

        /// <summary>Probabilidad de abatir (por mil) según la distancia: de cerca casi siempre, de lejos poco.</summary>
        public int HitPermil(byte kind, float dist)
        {
            int p = (int)(HitPointBlankPermil - dist * HitSlopePermilPerM);
            if (p < HitMinPermil) p = HitMinPermil;
            return kind == SoldierKind.MachineGun ? p * MgFactorPermil / 1000 : p;
        }
    }

    /// <summary>Parámetros de combate de esta zona (cargados del CSV al crearla). Componente de la raíz.</summary>
    public struct CombatConfig : IComponentData { public CombatParams P; }

    /// <summary>Compatibilidad: valores por defecto con los nombres de antes.</summary>
    public static class CombatConst
    {
        public const int ShotsPerUnit = 2000;
        public const int SoldierHp = 100;
    }

    // ------------------------------------------------------------------ raíz de la simulación
    public struct ZoneTag : IComponentData { }

    public struct SimClock : IComponentData
    {
        public uint Tick;
        public uint NextSimId;
        public float Alpha;                              // solo presentación: interpolación entre ticks (no entra en el hash)
    }

    /// <summary>Tipo de zona: estación (fuerte con puerta) o desmonte de emboscada (D-N38).</summary>
    public enum ZoneKind : byte { Station = 0, Cutting = 1 }

    public struct ZoneInfo : IComponentData
    {
        public ZoneKind Kind;
        public float TrainMin, TrainMax;                 // límites de la posición del tren dentro del mundo
        public bool TrainWallDirty;                      // el tren se ha parado solo (tope o tapón): la CommandSystem le devuelve el muro
        public bool GateOpen;
        public bool ForceFlow;
        public int GoalCell;
        public bool HasTrain;                            // hito 3: solo hay tren en la zona si hay un tren de la red en el andén
    }

    public struct TrainState : IComponentData
    {
        public float X;                                  // posición a lo largo de la vía (metros)
        public float Speed;                              // 0 = parado
        public int Dir;                                  // +1 / -1
    }

    public struct SimHash : IComponentData
    {
        public ulong Value;
        public uint Tick;
    }

    // Rejillas de la zona (índice = y * GridW + x), solo datos agregados
    public struct CellGround : IBufferElementData { public float Value; }     // altura rasterizada de los sólidos (solo para el coste del flujo)
    public struct CellPile : IBufferElementData { public int Value; }         // zombis apilados en la celda (estado del tick)
    public struct CellPileDelta : IBufferElementData { public int Value; }    // cambios de pila de este tick (doble búfer)
    public struct CellFlowDist : IBufferElementData { public int Value; }     // coste hasta la meta
    public struct CellFlowVec : IBufferElementData { public float2 Value; }   // dirección deseada (se interpola bilinealmente)

    // Índice espacial de los sólidos (cubetas uniformes de 4 m): cada zombi solo mira los sólidos de su cubeta
    public struct SolidBucketStart : IBufferElementData { public int Value; }  // inicio de la lista de cada cubeta (+ un final)
    public struct SolidBucketRef : IBufferElementData { public int Value; }    // índices de sólidos, en orden ascendente

    // ------------------------------------------------------------------ sólidos: muros con grosor y altura, edificios
    /// <summary>
    /// Sólido del mundo, con geometría continua. Kind 0 = segmento con grosor (A, B, HalfThick); Kind 1 = caja alineada (A = mínimo, B = máximo).
    /// La colisión es cápsula-contra-sólido, no por celda. Al construir, el sólido se rasteriza en la rejilla solo para el flujo.
    /// </summary>
    public struct Solid : IBufferElementData
    {
        public float2 A;
        public float2 B;
        public float HalfThick;
        public float Height;
        public byte Kind;
        public byte Open;                                // 1 = abierto (puerta): no cuenta como sólido
        public byte Id;                                  // 1 = puerta del fuerte
    }

    public static class SolidMath
    {
        static float2 ClosestOnSegment(float2 p, float2 a, float2 b)
        {
            float2 ab = b - a;
            float len2 = math.dot(ab, ab);
            float t = len2 > 1e-8f ? math.saturate(math.dot(p - a, ab) / len2) : 0f;
            return a + ab * t;
        }

        /// <summary>¿El punto está dentro de la huella del sólido (sin radio)? Da la altura sobre la que se anda.</summary>
        public static bool Contains(in Solid s, float2 p)
        {
            if (s.Open != 0) return false;
            if (s.Kind == 1) return p.x >= s.A.x && p.x <= s.B.x && p.y >= s.A.y && p.y <= s.B.y;
            return math.lengthsq(p - ClosestOnSegment(p, s.A, s.B)) <= s.HalfThick * s.HalfThick;
        }

        /// <summary>Empuje de un círculo (centro p, radio r) fuera del sólido. Devuelve false si no hay solape.</summary>
        public static bool Push(in Solid s, float2 p, float r, out float2 normal, out float depth)
        {
            normal = new float2(0f, 0f);
            depth = 0f;
            if (s.Open != 0) return false;

            if (s.Kind == 1)
            {
                float2 c = math.clamp(p, s.A, s.B);
                float2 d = p - c;
                float dist = math.length(d);
                if (dist > 1e-5f)
                {
                    if (dist >= r) return false;
                    normal = d / dist;
                    depth = r - dist;
                    return true;
                }
                // centro dentro de la caja: sacarlo por el lado más cercano
                float l = p.x - s.A.x, rr = s.B.x - p.x, b = p.y - s.A.y, t = s.B.y - p.y;
                float m = math.min(math.min(l, rr), math.min(b, t));
                if (m == l) normal = new float2(-1f, 0f);
                else if (m == rr) normal = new float2(1f, 0f);
                else if (m == b) normal = new float2(0f, -1f);
                else normal = new float2(0f, 1f);
                depth = m + r;
                return true;
            }

            float2 q = ClosestOnSegment(p, s.A, s.B);
            float2 dd = p - q;
            float dl = math.length(dd);
            float pen = s.HalfThick + r - dl;
            if (pen <= 0f) return false;
            if (dl > 1e-5f) normal = dd / dl;
            else
            {
                float2 ab = s.B - s.A;
                float al = math.length(ab);
                normal = al > 1e-5f ? new float2(-ab.y, ab.x) / al : new float2(1f, 0f);
            }
            depth = pen;
            return true;
        }
    }

    // ------------------------------------------------------------------ zombis
    public struct Zombie : IComponentData
    {
        public uint SimId;                               // id estable propio
        public float3 Pos;                               // x, z = plano; y = altura (continuas)
        public float3 PrevPos;
        public float3 Vel;
        public float Heading;                            // orientación en radianes alrededor de Y
        public float PrevHeading;
        public float Speed;
        public int SettledCell;
        public byte State;

        public static Zombie Create(uint id, float2 pos, float height)
        {
            // Repetición dorada en IL2CPP (6-nov): fuera de Burst, Mono puede calcular una cadena de operaciones en coma flotante
            // con más precisión que IL2CPP y el resultado difiere en 1 ulp. Por eso aquí todo se hace en enteros y se termina con UNA
            // sola división, que sale igual en todos los runtimes.
            float speed = (1200 + (int)((math.hash(new uint2(id, 7u)) & 1023u) * 800u / 1023u)) / 1000f;
            float heading = (int)((math.hash(new uint2(id, 13u)) & 1023u) * 6283u / 1023u) / 1000f;
            var p = new float3(pos.x, height, pos.y);
            return new Zombie
            {
                SimId = id, Pos = p, PrevPos = p, Vel = float3.zero, Heading = heading, PrevHeading = heading,
                Speed = speed, SettledCell = -1, State = ZState.Walk,
            };
        }
    }

    // ------------------------------------------------------------------ órdenes
    public enum CmdType : byte
    {
        SpawnRoof = 0,
        SpawnWest = 1,
        ToggleGate = 2,
        ToggleTrain = 3,
        BuildWall = 4,                                   // muro libre de P0 a P1 (polilíneas = varias órdenes)
        DeploySquad = 5,                                 // hito 3: desembarca un pelotón (20 fusileros + 1 ametralladora) junto al tren
        EmbarkSquad = 6,                                 // hito 3 (D-N27): pelotón a bordo; dispara desde los vagones con el tren parado
        Reembark = 7,                                    // hito 3: los soldados a pie a menos de 20 m del tren vuelven a bordo
        TrainDrive = 8,                                  // D-N38: Arg +1 adelante, -1 marcha atrás, 0 parar. Adelante lo frena el tapón de cuerpos
    }

    /// <summary>Todo lo que hace el jugador es una orden con número de tick (regla 2 del GDD §17.3).</summary>
    public struct SimCommand : IBufferElementData
    {
        public uint Tick;
        public CmdType Type;
        public int Arg;
        public float2 P0;
        public float2 P1;
    }

    // ------------------------------------------------------------------ desmonte de emboscada (D-N38, Lucía)
    /// <summary>
    /// Zona de emboscada «desmonte»: trinchera en la vía, de X = 60 a 200, 7 m de profundidad y 16 m de fondo. Los taludes (~40°) son
    /// cinco escalones de 1,4 m: el desnivel pasa de DropThreshold, así que los zombis bajan cayendo (caídas de §5.7). El tren queda con la
    /// locomotora en X = 170 y el tapón de cuerpos delante (X 175-185). La horda sale de los dos bordes altos y de la vía por delante.
    /// </summary>
    public static class CuttingLevel
    {
        public const float X0 = 60f, X1 = 200f;
        public const float Depth = 7f, HalfBottom = 8f, SlopeRun = 8.3f;
        public const int Steps = 5;
        public const float LocoX = 170f;                          // frente de la locomotora
        public const float TrainX = LocoX - Level.TrainFront;     // «X» del tren (el sólido va de X - 17,5 a X + 2,5)
        public const float PlugX0 = 175f, PlugX1 = 185f;
        public const float FreeBack = 40f;                        // los primeros 40 m por detrás siempre quedan libres
        public const float TrainMinX = Level.TrainRear, TrainMaxX = SimConst.WorldW - 8f;
        public const int PlugBlocksPile = 3;                      // pila (3 x 0,35 = 1,05 m) a partir de la cual el tapón frena al tren

        public static List<Solid> CreateSolids()
        {
            var list = new List<Solid>();
            float ya = Level.TrackY - HalfBottom, yb = Level.TrackY + HalfBottom;
            for (int k = 1; k <= Steps; k++)
            {
                float h = Depth * k / Steps;
                float north = yb + SlopeRun * (k - 1) / Steps, south = ya - SlopeRun * (k - 1) / Steps;
                list.Add(new Solid { A = new float2(X0, north), B = new float2(X1, SimConst.WorldH), Height = h, Kind = 1, Open = 0, Id = 0 });
                list.Add(new Solid { A = new float2(X0, 0f), B = new float2(X1, south), Height = h, Kind = 1, Open = 0, Id = 0 });
            }
            return list;
        }

        /// <summary>¿Frena el tapón al tren? Sí si, delante de la locomotora, alguna celda de la vía tiene una pila de cuerpos a la altura de las ruedas.</summary>
        public static bool PlugBlocks(Unity.Collections.NativeArray<int> pile, float trainX)
        {
            float front = trainX + Level.TrainFront;
            for (float x = front + 0.5f; x <= front + 3f; x += 0.5f)
                for (float y = Level.TrackY - 1f; y <= Level.TrackY + 1f; y += 0.5f)
                {
                    int2 c = Level.CellOf(new float2(x, y));
                    if (pile[c.y * SimConst.GridW + c.x] >= PlugBlocksPile) return true;
                }
            return false;
        }
    }

    // ------------------------------------------------------------------ nivel de prueba (metros)
    public static class Level
    {
        public const float TrackY = 64.5f;
        public const float WallH = 2.5f;
        public const float RoofH = 4f;        // el tejado de pruebas (almacén de naranjas): la horda trepa a 4 m (escena del hito 1); Lucía revirtió el 6,5 m el 6-nov
        public const float WallHalf = 1f;                 // muro de 2 m de grosor

        public const float FortX0 = 70f, FortX1 = 156f, FortY0 = 44f, FortY1 = 85f;     // fuerte de Sagunto alargado a 86 m hacia el este (tren a escala real, Lucía 6-nov)
        public const float GateY0 = 62f, GateY1 = 67f;
        public const float RoofX0 = 40f, RoofX1 = 51f, RoofY0 = 30f, RoofY1 = 51f;
        public const float TrainMinX = 135f, TrainMaxX = 151.5f;       // el tren cabe entero dentro del fuerte (muros en X = 70 y 101): cola en 73,5 y locomotora en 93,5 (parado en el andén)
        public const byte GateId = 1;
        public const byte TrainId = 2;                    // D-N27: el tren parado es un muro móvil
        public const float TrainRear = 61.5f, TrainFront = 2.5f, TrainHalf = 1.0f, TrainH = 2.2f;

        /// <summary>Sólido del tren parado en la posición X (de la cola a la locomotora).</summary>
        public static Solid TrainSolid(float x)
        {
            return new Solid { A = new float2(x - TrainRear, TrackY), B = new float2(x + TrainFront, TrackY), HalfThick = TrainHalf, Height = TrainH, Kind = 0, Open = 0, Id = TrainId };
        }

        /// <summary>Sólidos del nivel: cuatro lados del fuerte (el oeste partido por la puerta) y un edificio con tejado.</summary>
        public static List<Solid> CreateSolids()
        {
            float h = WallHalf;
            float xa = FortX0 + h, xb = FortX1 - h, ya = FortY0 + h, yb = FortY1 - h;
            Solid Seg(float2 a, float2 b, byte id = 0) => new Solid { A = a, B = b, HalfThick = h, Height = WallH, Kind = 0, Open = 0, Id = id };
            return new List<Solid>
            {
                Seg(new float2(xa, ya), new float2(xb, ya)),                  // sur
                Seg(new float2(xa, yb), new float2(xb, yb)),                  // norte
                Seg(new float2(xb, ya), new float2(xb, yb)),                  // este
                Seg(new float2(xa, ya), new float2(xa, GateY0)),              // oeste, tramo sur
                Seg(new float2(xa, GateY1), new float2(xa, yb)),              // oeste, tramo norte
                Seg(new float2(xa, GateY0), new float2(xa, GateY1), GateId),  // puerta
                new Solid { A = new float2(RoofX0, RoofY0), B = new float2(RoofX1, RoofY1), Height = RoofH, Kind = 1, Open = 0, Id = 0 },
            };
        }

        public static int2 CellOf(float2 p)
        {
            return math.clamp((int2)math.floor(p / SimConst.CellSize), new int2(0, 0), new int2(SimConst.GridW - 1, SimConst.GridH - 1));
        }

        /// <summary>Rasteriza en la rejilla los sólidos de una región (solo para el coste del flujo). Recalcula únicamente esa región.</summary>
        public static void RasterRegion(DynamicBuffer<Solid> solids, DynamicBuffer<CellGround> ground, int x0, int y0, int x1, int y1)
        {
            x0 = math.max(0, x0); y0 = math.max(0, y0);
            x1 = math.min(SimConst.GridW - 1, x1); y1 = math.min(SimConst.GridH - 1, y1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new float2((x + 0.5f) * SimConst.CellSize, (y + 0.5f) * SimConst.CellSize);
                    float hgt = 0f;
                    for (int i = 0; i < solids.Length; i++)
                    {
                        var s = solids[i];
                        if (SolidMath.Contains(s, p)) hgt = math.max(hgt, s.Height);
                    }
                    ground[y * SimConst.GridW + x] = new CellGround { Value = hgt };
                }
        }

        public static void RasterSolid(DynamicBuffer<Solid> solids, DynamicBuffer<CellGround> ground, in Solid s)
        {
            float2 mn, mx;
            if (s.Kind == 1) { mn = s.A; mx = s.B; }
            else { mn = math.min(s.A, s.B) - s.HalfThick; mx = math.max(s.A, s.B) + s.HalfThick; }
            int2 c0 = CellOf(mn) - 1, c1 = CellOf(mx) + 1;
            RasterRegion(solids, ground, c0.x, c0.y, c1.x, c1.y);
        }
    }
}
