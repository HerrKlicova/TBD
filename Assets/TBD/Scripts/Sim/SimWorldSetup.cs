using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TBD.Sim
{
    /// <summary>Crea la entidad raíz con el reloj, la zona, el tren y las rejillas, y registra los sistemas en un orden fijo.</summary>
    public static class SimWorldSetup
    {
        public const int InitialZombies = 10000;
        public const uint Seed = 12345u;

        public static Entity Populate(EntityManager em)
        {
            var root = em.CreateEntity(typeof(ZoneTag), typeof(SimClock), typeof(ZoneInfo), typeof(TrainState), typeof(SimHash));
            em.SetComponentData(root, new SimClock { Tick = 0, NextSimId = 1, Alpha = 1f });
            em.SetComponentData(root, new ZoneInfo { GateOpen = false, ForceFlow = true, GoalCell = 0 });
            em.SetComponentData(root, new TrainState { X = Level.TrainMinX, Speed = 0f, Dir = 1 });

            int n = SimConst.GridW * SimConst.GridH;
            var ground = em.AddBuffer<CellGround>(root);
            ground.ResizeUninitialized(n);
            Level.Build(ground, gateOpen: false);

            em.AddBuffer<CellPile>(root).Resize(n, NativeArrayOptions.ClearMemory);
            em.AddBuffer<CellPileDelta>(root).Resize(n, NativeArrayOptions.ClearMemory);
            em.AddBuffer<CellFlowDist>(root).Resize(n, NativeArrayOptions.ClearMemory);
            em.AddBuffer<CellFlowDir>(root).Resize(n, NativeArrayOptions.ClearMemory);
            em.AddBuffer<SimCommand>(root);

            SpawnInitialHorde(em, root);
            return root;
        }

        /// <summary>Horda inicial: posiciones deterministas (semilla fija) fuera del edificio del tejado.</summary>
        static void SpawnInitialHorde(EntityManager em, Entity root)
        {
            var clock = em.GetComponentData<SimClock>(root);
            var rng = new Unity.Mathematics.Random(Seed);
            var arch = em.CreateArchetype(typeof(Zombie));
            var ents = em.CreateEntity(arch, InitialZombies, Allocator.Temp);
            for (int i = 0; i < InitialZombies; i++)
            {
                float2 p;
                do
                {
                    p = new float2(rng.NextFloat(4f, 60f), rng.NextFloat(8f, 120f));
                }
                while (p.x > Level.RoofMinX - 1.5f && p.x < Level.RoofMaxX + 2.5f && p.y > Level.RoofMinY - 1.5f && p.y < Level.RoofMaxY + 2.5f);
                em.SetComponentData(ents[i], Zombie.Create(clock.NextSimId++, p, 0f));
            }
            ents.Dispose();
            em.SetComponentData(root, clock);
        }

        /// <summary>Crea el grupo raíz y añade los sistemas en el MISMO orden en cualquier mundo (regla de orden determinista).</summary>
        public static SimRootGroup CreateSystems(World world)
        {
            var group = world.CreateSystemManaged<SimRootGroup>();
            group.EnableSystemSorting = false;
            group.AddSystemToUpdateList(world.CreateSystem<CommandSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<FlowFieldSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<ZombieStepSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<PileApplySystem>());
            group.AddSystemToUpdateList(world.CreateSystem<TrainSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<StateHashSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<TickSystem>());
            return group;
        }
    }
}
