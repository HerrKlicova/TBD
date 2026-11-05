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
        public const int GridW = 256;
        public const int GridH = 256;                   // zona de 128 x 128 m
        public const float WorldSize = GridW * CellSize;

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
    }

    // ------------------------------------------------------------------ raíz de la simulación
    public struct ZoneTag : IComponentData { }

    public struct SimClock : IComponentData
    {
        public uint Tick;
        public uint NextSimId;
        public float Alpha;                              // solo presentación: interpolación entre ticks (no entra en el hash)
    }

    public struct ZoneInfo : IComponentData
    {
        public bool GateOpen;
        public bool ForceFlow;
        public int GoalCell;
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
            float speed = 1.2f + (math.hash(new uint2(id, 7u)) & 1023u) / 1023f * 0.8f;
            float heading = (math.hash(new uint2(id, 13u)) & 1023u) / 1023f * 6.2831853f;
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

    // ------------------------------------------------------------------ nivel de prueba (metros)
    public static class Level
    {
        public const float TrackY = 64.5f;
        public const float WallH = 2.5f;
        public const float RoofH = 4f;
        public const float WallHalf = 1f;                 // muro de 2 m de grosor

        public const float FortX0 = 70f, FortX1 = 101f, FortY0 = 44f, FortY1 = 85f;
        public const float GateY0 = 62f, GateY1 = 67f;
        public const float RoofX0 = 40f, RoofX1 = 51f, RoofY0 = 30f, RoofY1 = 51f;
        public const float TrainMinX = 74f, TrainMaxX = 98f;
        public const byte GateId = 1;

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
