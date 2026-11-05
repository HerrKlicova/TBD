using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TBD.Sim
{
    /// <summary>Crea la entidad raíz con el reloj, la zona, el tren, los sólidos y las rejillas, y registra los sistemas en un orden fijo.</summary>
    public static class SimWorldSetup
    {
        public const int InitialZombies = 10000;
        public const uint Seed = 12345u;

        public static Entity Populate(EntityManager em) { return Populate(em, InitialZombies, false, Seed); }

        public static Entity Populate(EntityManager em, int zombies, bool siege, uint seed)
        {
            return Populate(em, zombies, siege, seed, CombatParams.Default, 20L * CombatParams.Default.ShotsPerUnit, true);
        }

        /// <summary>
        /// Crea la zona con «zombies» zombis (hito 3: los que vienen de la mancha S0 al materializar). Reparto (D-N07 de Lucía):
        /// en «asedio», concentrados junto al muro oeste del fuerte; en «deriva», dispersos por el oeste de la zona. La semilla sale
        /// del nodo y del tick, así que dos mundos con las mismas órdenes crean exactamente la misma horda.
        /// </summary>
        /// <param name="combat">Parámetros de combate (de balance_combate.csv).</param>
        /// <param name="ammoShots">Munición del tren para esta zona, en disparos equivalentes.</param>
        public static Entity Populate(EntityManager em, int zombies, bool siege, uint seed, CombatParams combat, long ammoShots, bool hasTrain)
        {
            var root = em.CreateEntity(typeof(ZoneTag), typeof(SimClock), typeof(ZoneInfo), typeof(TrainState), typeof(SimHash), typeof(ZoneAmmo), typeof(CombatConfig));
            em.SetComponentData(root, new ZoneAmmo { ShotsLeft = ammoShots });
            em.SetComponentData(root, new CombatConfig { P = combat });
            em.SetComponentData(root, new SimClock { Tick = 0, NextSimId = 1, Alpha = 1f });
            em.SetComponentData(root, new ZoneInfo { GateOpen = false, ForceFlow = true, GoalCell = 0, HasTrain = hasTrain });
            em.SetComponentData(root, new TrainState { X = Level.TrainMinX, Speed = 0f, Dir = 1 });

            int n = SimConst.GridW * SimConst.GridH;

            // Primero se añaden todos los buffers (cambios estructurales) y solo después se obtienen y rellenan:
            // un DynamicBuffer queda invalidado por cualquier cambio estructural de su entidad.
            em.AddBuffer<Solid>(root);
            em.AddBuffer<SolidBucketStart>(root);
            em.AddBuffer<SolidBucketRef>(root);
            em.AddBuffer<CellGround>(root);
            em.AddBuffer<CellPile>(root);
            em.AddBuffer<CellPileDelta>(root);
            em.AddBuffer<CellFlowDist>(root);
            em.AddBuffer<CellFlowVec>(root);
            em.AddBuffer<SimCommand>(root);

            var solids = em.GetBuffer<Solid>(root);
            foreach (var s in Level.CreateSolids()) solids.Add(s);
            if (hasTrain) solids.Add(Level.TrainSolid(Level.TrainMinX));   // D-N27: el tren empieza parado, así que es muro

            SolidIndex.Rebuild(solids, em.GetBuffer<SolidBucketStart>(root), em.GetBuffer<SolidBucketRef>(root));

            var ground = em.GetBuffer<CellGround>(root);
            ground.ResizeUninitialized(n);
            Level.RasterRegion(solids, ground, 0, 0, SimConst.GridW - 1, SimConst.GridH - 1);

            em.GetBuffer<CellPile>(root).Resize(n, NativeArrayOptions.ClearMemory);
            em.GetBuffer<CellPileDelta>(root).Resize(n, NativeArrayOptions.ClearMemory);
            em.GetBuffer<CellFlowDist>(root).Resize(n, NativeArrayOptions.ClearMemory);
            em.GetBuffer<CellFlowVec>(root).Resize(n, NativeArrayOptions.ClearMemory);

            SpawnInitialHorde(em, root, zombies, siege, seed);
            return root;
        }

        /// <summary>Horda inicial: posiciones deterministas (semilla fija) fuera del edificio del tejado.</summary>
        static void SpawnInitialHorde(EntityManager em, Entity root, int count, bool siege, uint seed)
        {
            var clock = em.GetComponentData<SimClock>(root);
            var rng = new Unity.Mathematics.Random(seed | 1u);
            var arch = em.CreateArchetype(typeof(Zombie));
            if (count <= 0) { em.SetComponentData(root, clock); return; }
            var ents = em.CreateEntity(arch, count, Allocator.Temp);
            for (int i = 0; i < count; i++)
            {
                float2 p;
                do
                {
                    p = siege
                        ? new float2(rng.NextFloat(Level.FortX0 - 16f, Level.FortX0 - 2.5f), rng.NextFloat(Level.FortY0 - 4f, Level.FortY1 + 4f))   // asedio: contra el muro oeste
                        : new float2(rng.NextFloat(4f, 60f), rng.NextFloat(8f, 120f));                                                            // deriva: dispersos
                }
                while (p.x > Level.RoofX0 - 1.5f && p.x < Level.RoofX1 + 1.5f && p.y > Level.RoofY0 - 1.5f && p.y < Level.RoofY1 + 1.5f);
                em.SetComponentData(ents[i], Zombie.Create(clock.NextSimId++, p, 0f));
            }
            ents.Dispose();
            em.SetComponentData(root, clock);
        }

        /// <summary>Crea el grupo raíz y añade los sistemas en el MISMO orden en cualquier mundo (regla de orden determinista).</summary>
        public static SimRootGroup CreateSystems(World world)
        {
            var group = world.CreateSystemManaged<SimRootGroup>();
            group.AddSystemToUpdateList(world.CreateSystem<CommandSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<FlowFieldSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<ZombieStepSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<SoldierSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<PileApplySystem>());
            group.AddSystemToUpdateList(world.CreateSystem<TrainSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<StateHashSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<TickSystem>());
            return group;
        }
    }
}
