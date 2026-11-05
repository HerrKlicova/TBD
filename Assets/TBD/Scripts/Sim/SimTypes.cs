using Unity.Entities;
using Unity.Mathematics;

namespace TBD.Sim
{
    /// <summary>Constantes de la simulación. Nada de aquí depende de Time.deltaTime (regla 1 del GDD §17.3).</summary>
    public static class SimConst
    {
        public const int TickRate = 30;                 // PROTOTIPO: 20-30 Hz
        public const float Dt = 1f / TickRate;
        public const int GridW = 128;
        public const int GridH = 128;
        public const float CellSize = 1f;               // metros por celda
        public const float Thick = 0.35f;               // altura que añade un zombi apilado
        public const int MaxPile = 10;                  // tope visual/colisión de la pila por celda
        public const float StepUp = 0.6f;               // desnivel máximo que se sube andando
        public const float DropThreshold = 1.0f;        // desnivel a partir del cual se cae
        public const float Gravity = 20f;               // m/s^2
        public const int FlowEveryTicks = 8;            // el campo de flujo se recalcula cada 8 ticks
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
    // Una única entidad "raíz" por mundo lleva el reloj, la zona, el tren, el hash y las rejillas (buffers).

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

    // Rejillas de la zona (índice = y * GridW + x)
    public struct CellGround : IBufferElementData { public float Value; }     // altura estática: suelo, muro, tejado
    public struct CellPile : IBufferElementData { public int Value; }         // zombis apilados en la celda (estado del tick)
    public struct CellPileDelta : IBufferElementData { public int Value; }    // cambios de pila de este tick (doble búfer)
    public struct CellFlowDist : IBufferElementData { public int Value; }     // coste hasta la meta
    public struct CellFlowDir : IBufferElementData { public byte Value; }     // 0..7 vecino; 8 = meta

    public enum CmdType : byte
    {
        SpawnRoof = 0,
        SpawnWest = 1,
        ToggleGate = 2,
        ToggleTrain = 3,
    }

    /// <summary>Todo lo que hace el jugador es una orden con número de tick (regla 2 del GDD §17.3).</summary>
    public struct SimCommand : IBufferElementData
    {
        public uint Tick;
        public CmdType Type;
        public int Arg;
    }

    // ------------------------------------------------------------------ zombis
    public struct Zombie : IComponentData
    {
        public uint SimId;                               // id estable propio
        public float2 Pos;
        public float2 PrevPos;
        public float Z;
        public float PrevZ;
        public float Vz;
        public float2 Dir;                               // última dirección de marcha (se mantiene al caer)
        public float Speed;
        public int SettledCell;
        public byte State;

        public static Zombie Create(uint id, float2 pos, float z)
        {
            float speed = 1.2f + (math.hash(new uint2(id, 7u)) & 1023u) / 1023f * 0.8f;
            return new Zombie
            {
                SimId = id, Pos = pos, PrevPos = pos, Z = z, PrevZ = z, Vz = 0f,
                Dir = new float2(1f, 0f), Speed = speed, SettledCell = -1, State = ZState.Walk,
            };
        }
    }

    // ------------------------------------------------------------------ nivel de prueba
    /// <summary>Geometría del prototipo: un fuerte con muro y puerta, la vía con su tren dentro y un edificio con tejado.</summary>
    public static class Level
    {
        public const int TrackY = 64;
        public const float WallH = 2.5f;
        public const float RoofH = 4f;

        public const int FortMinX = 70, FortMaxX = 100, FortMinY = 44, FortMaxY = 84;   // contorno exterior del muro
        public const int WallThickness = 2;
        public const int GateMinX = 70, GateMaxX = 71, GateMinY = 62, GateMaxY = 66;    // hueco de la puerta (cerrada = muro)
        public const int RoofMinX = 40, RoofMaxX = 50, RoofMinY = 30, RoofMaxY = 50;    // edificio con tejado
        public const float TrainMinX = 74f, TrainMaxX = 98f;

        public static int CellIndex(int x, int y) => y * SimConst.GridW + x;

        public static void Build(Unity.Entities.DynamicBuffer<CellGround> g, bool gateOpen)
        {
            for (int y = 0; y < SimConst.GridH; y++)
                for (int x = 0; x < SimConst.GridW; x++)
                {
                    float h = 0f;
                    if (x >= RoofMinX && x <= RoofMaxX && y >= RoofMinY && y <= RoofMaxY) h = RoofH;
                    bool inFort = x >= FortMinX && x <= FortMaxX && y >= FortMinY && y <= FortMaxY;
                    bool inInterior = x >= FortMinX + WallThickness && x <= FortMaxX - WallThickness
                                      && y >= FortMinY + WallThickness && y <= FortMaxY - WallThickness;
                    if (inFort && !inInterior) h = WallH;
                    g[CellIndex(x, y)] = new CellGround { Value = h };
                }
            SetGate(g, gateOpen);
        }

        public static void SetGate(Unity.Entities.DynamicBuffer<CellGround> g, bool open)
        {
            for (int y = GateMinY; y <= GateMaxY; y++)
                for (int x = GateMinX; x <= GateMaxX; x++)
                    g[CellIndex(x, y)] = new CellGround { Value = open ? 0f : WallH };
        }
    }
}
