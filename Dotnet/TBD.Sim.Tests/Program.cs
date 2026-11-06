using System;
using System.Collections.Generic;
using System.Diagnostics;
using TBD.Sim;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TBD.Sim.Tests
{
    /// <summary>Mundo mínimo para ejecutar la lógica pura de la simulación fuera de Unity.</summary>
    class MiniWorld
    {
        public readonly int N = SimConst.GridW * SimConst.GridH;
        public List<Solid> SolidList;
        public NativeArray<Solid> Solids;
        public NativeArray<int> BStart, BRefs;
        public NativeArray<float> Ground;
        public NativeArray<int> Dist, Pile, Delta, Snapshot, Density;
        public NativeArray<float2> Vec;
        public Zombie[] Z;
        public uint Tick;

        public MiniWorld(List<Solid> solids, float goalX, float goalY)
        {
            SolidList = solids;
            Ground = new NativeArray<float>(N, Allocator.Temp);
            Dist = new NativeArray<int>(N, Allocator.Temp);
            Pile = new NativeArray<int>(N, Allocator.Temp);
            Delta = new NativeArray<int>(N, Allocator.Temp);
            Snapshot = new NativeArray<int>(N, Allocator.Temp);
            Density = new NativeArray<int>(N, Allocator.Temp);
            Vec = new NativeArray<float2>(N, Allocator.Temp);
            Z = new Zombie[0];
            Rebuild(goalX, goalY);
        }

        public void Rebuild(float goalX, float goalY)
        {
            Solids = new NativeArray<Solid>(SolidList.ToArray());
            var solidsBuf = new DynamicBuffer<Solid>(SolidList);
            var startList = new List<SolidBucketStart>();
            var refList = new List<SolidBucketRef>();
            SolidIndex.Rebuild(solidsBuf, new DynamicBuffer<SolidBucketStart>(startList), new DynamicBuffer<SolidBucketRef>(refList));
            var st = new int[startList.Count]; for (int i = 0; i < st.Length; i++) st[i] = startList[i].Value;
            var rf = new int[refList.Count]; for (int i = 0; i < rf.Length; i++) rf[i] = refList[i].Value;
            BStart = new NativeArray<int>(st);
            BRefs = new NativeArray<int>(rf);
            var groundList = new List<CellGround>();
            var groundBuf = new DynamicBuffer<CellGround>(groundList);
            groundBuf.ResizeUninitialized(N);
            Level.RasterRegion(solidsBuf, groundBuf, 0, 0, SimConst.GridW - 1, SimConst.GridH - 1);
            for (int i = 0; i < N; i++) Ground[i] = groundList[i].Value;
            int2 gc = Level.CellOf(new float2(goalX, goalY));
            new FlowJob { Ground = Ground, Dist = Dist, Vec = Vec, Goal = gc.y * SimConst.GridW + gc.x }.Execute();
        }

        public void Spawn(int count, float x0, float x1, float z0, float z1, float height, uint seed)
        {
            var list = new List<Zombie>(Z);
            uint s = seed | 1u;
            float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / (float)0x1000000; }
            for (int i = 0; i < count; i++)
                list.Add(Zombie.Create((uint)(list.Count + 1), new float2(x0 + Next() * (x1 - x0), z0 + Next() * (z1 - z0)), height));
            Z = list.ToArray();
        }

        public void Tick1(int[] order = null)
        {
            Snapshot.CopyFrom(Pile);
            for (int i = 0; i < N; i++) Density[i] = 0;
            if (order == null)
            {
                for (int i = 0; i < Z.Length; i++) ZombieLogic.AddDensity(Z[i], Density);
                for (int i = 0; i < Z.Length; i++) ZombieLogic.Step(ref Z[i], Solids, BStart, BRefs, Snapshot, Vec, Delta, Density);
            }
            else
            {
                foreach (int i in order) ZombieLogic.AddDensity(Z[i], Density);
                foreach (int i in order) ZombieLogic.Step(ref Z[i], Solids, BStart, BRefs, Snapshot, Vec, Delta, Density);
            }
            new PileApplyJob { Pile = Pile, Delta = Delta }.Execute();
            Tick++;
        }

        public ulong Hash()
        {
            ulong sum = 0;
            for (int i = 0; i < Z.Length; i++) sum += HashMath.OfZombie(Z[i]);
            for (int i = 0; i < N; i++) sum += HashMath.OfPileCell(i, Pile[i]);
            return sum + Tick;
        }

        public int Count(byte state)
        {
            int c = 0;
            foreach (var z in Z) if (z.State == state) c++;
            return c;
        }

        public int MaxPile()
        {
            int m = 0;
            for (int i = 0; i < N; i++) m = Math.Max(m, Pile[i]);
            return m;
        }
    }

    static class Program
    {
        static int _pass, _fail;

        internal static void Check(bool ok, string msg)
        {
            if (ok) { _pass++; Console.WriteLine("  ok     " + msg); }
            else { _fail++; Console.WriteLine("  FALLO  " + msg); }
        }

        static int Main()
        {
            string dataDir = BalanceTests.DataDirPublic();           // el mismo lector que Unity, apuntando a la carpeta de datos del repositorio
            TBD.Net.BalanceSource.Reader = f => { var path = System.IO.Path.Combine(dataDir, f); return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null; };
            if (Environment.GetEnvironmentVariable("TBD_ONLY_PERF") == "1")       // solo las dos pruebas de coste, para repetirlas (criterio de Sara: 20 de 20 en verde con carga y 20 de 20 en rojo con TBD_PERF_INJECT=3)
            {
                TestPerfOnly();
                Console.WriteLine($"Resumen: {_pass} correctas, {_fail} fallos");
                return _fail == 0 ? 0 : 1;
            }
            Console.WriteLine("Pruebas de la lógica pura de la simulación (fuera de Unity, con shims mínimos)\n");
            TestSolidMath();
            TestFlow();
            TestOpenFieldWalk();
            TestRaster();
            TestRoofFall();
            TestSolidIndex();
            TestOneZombieAtWall();
            TestWallPileSpill();
            TestSeparation();
            TestDeterminism();
            TestPerf();
            RailTests.Run(Check);
            NetWorldTests.Run(Check);
            BalanceTests.Run(Check);
            CombatTests.Run(Check);
            HiveTests.Run(Check);
            ObrasTests.Run(Check);
            EstacionesTests.Run(Check);
            HorarioTests.Run(Check);
            TallerTests.Run(Check);
            GoldenTests.Run(Check);
            Console.WriteLine($"\nResumen: {_pass} correctas, {_fail} fallos");
            return _fail == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ sólidos
        static void TestSolidMath()
        {
            Console.WriteLine("[1] Sólidos y colisión cápsula");
            var wall = new Solid { A = new float2(10f, 10f), B = new float2(20f, 10f), HalfThick = 1f, Height = 2.5f, Kind = 0 };
            Check(SolidMath.Contains(wall, new float2(15f, 10.9f)), "un punto dentro del grosor está en la huella");
            Check(!SolidMath.Contains(wall, new float2(15f, 11.2f)), "un punto fuera del grosor no está en la huella");
            float2 n; float d;
            bool hit = SolidMath.Push(wall, new float2(15f, 11.1f), 0.25f, out n, out d);
            Check(hit && Math.Abs(n.y - 1f) < 1e-4f && Math.Abs(d - 0.15f) < 1e-3f, $"un círculo que roza el muro se empuja por la normal (n.y={n.y:0.00}, profundidad={d:0.000})");
            Check(!SolidMath.Push(wall, new float2(15f, 12f), 0.25f, out n, out d), "un círculo lejano no colisiona");
            var box = new Solid { A = new float2(40f, 30f), B = new float2(51f, 51f), Height = 4f, Kind = 1 };
            Check(SolidMath.Push(box, new float2(39.9f, 40f), 0.25f, out n, out d) && n.x < -0.9f, "una caja empuja hacia fuera por el lado más cercano");
            var open = wall; open.Open = 1;
            Check(!SolidMath.Contains(open, new float2(15f, 10f)) && !SolidMath.Push(open, new float2(15f, 10f), 0.25f, out n, out d), "una puerta abierta no es sólida");
        }

        // ------------------------------------------------------------------ campo de flujo
        static void TestFlow()
        {
            Console.WriteLine("[2] Campo de flujo");
            var w = new MiniWorld(new List<Solid>(), 100f, 64f);
            var v = w.Vec[Idx(50f, 64f)];
            Check(v.x > 0.95f && Math.Abs(v.y) < 0.1f, $"en campo abierto apunta hacia la meta (vec={v.x:0.00},{v.y:0.00})");
            var vg = w.Vec[Idx(100f, 64f)];
            Check(vg.x == 0f && vg.y == 0f, "en la celda meta la dirección es cero");
            var vd = w.Vec[Idx(80f, 90f)];
            Check(vd.x > 0.3f && vd.y < -0.3f, $"una celda en diagonal apunta en diagonal (vec={vd.x:0.00},{vd.y:0.00})");

            // coherencia: avanzar siguiendo el vector siempre reduce el coste
            bool monotone = true;
            for (int k = 0; k < 200 && monotone; k++)
            {
                float x = 5f + (k * 37 % 90), y = 5f + (k * 53 % 110);
                var c = Level.CellOf(new float2(x, y));
                int d0 = w.Dist[c.y * SimConst.GridW + c.x];
                var vec = w.Vec[c.y * SimConst.GridW + c.x];
                var c2 = Level.CellOf(new float2(x + vec.x * 1.1f, y + vec.y * 1.1f));
                int d1 = w.Dist[c2.y * SimConst.GridW + c2.x];
                if (d0 > 0 && d1 >= d0) monotone = false;
            }
            Check(monotone, "seguir la dirección siempre reduce el coste hasta la meta");

            // determinismo
            var w2 = new MiniWorld(new List<Solid>(), 100f, 64f);
            bool same = true;
            for (int i = 0; i < w.N; i++) if (w.Vec[i].x != w2.Vec[i].x || w.Vec[i].y != w2.Vec[i].y || w.Dist[i] != w2.Dist[i]) { same = false; break; }
            Check(same, "dos cálculos iguales dan exactamente el mismo campo");

            // un muro alto encarece el cruce
            var solids = new List<Solid> { new Solid { A = new float2(70f, 30f), B = new float2(70f, 100f), HalfThick = 1f, Height = 2.5f, Kind = 0 } };
            var ww = new MiniWorld(solids, 100f, 64f);
            int west = ww.Dist[Idx(66f, 64f) ], east = ww.Dist[Idx(74f, 64f)];
            Check(west - east > 80, $"cruzar un muro de 2 m de grosor cuesta mucho más (oeste {west}, este {east})");
        }

        static int Idx(float x, float y)
        {
            var c = Level.CellOf(new float2(x, y));
            return c.y * SimConst.GridW + c.x;
        }

        // ------------------------------------------------------------------ marcha en campo abierto
        static void TestOpenFieldWalk()
        {
            Console.WriteLine("[3] Marcha continua en campo abierto");
            var w = new MiniWorld(new List<Solid>(), 100f, 64f);
            w.Spawn(1, 40f, 40f, 64f, 64f, 0f, 7u);
            float x0 = w.Z[0].Pos.x;
            float maxStep = 0f;
            for (int t = 0; t < 600; t++)
            {
                var before = w.Z[0].Pos;
                w.Tick1();
                var after = w.Z[0].Pos;
                maxStep = Math.Max(maxStep, math.length(new float2(after.x - before.x, after.z - before.z)));
            }
            var z = w.Z[0];
            Check(z.Pos.x - x0 > 15f, $"el zombi avanza hacia la meta (de x={x0:0.0} a x={z.Pos.x:0.0})");
            Check(maxStep < 0.12f, $"el paso por tick es continuo y pequeño (máximo {maxStep:0.000} m; una celda mide {SimConst.CellSize} m)");
            // no se mueve solo por centros de celda: la posición no cae siempre en centros
            int onCenters = 0, samples = 0;
            var w2 = new MiniWorld(new List<Solid>(), 100f, 64f);
            w2.Spawn(1, 40f, 40f, 64.13f, 64.13f, 0f, 9u);
            for (int t = 0; t < 200; t++)
            {
                w2.Tick1();
                var p = w2.Z[0].Pos;
                float fx = p.x / SimConst.CellSize - (float)Math.Floor(p.x / SimConst.CellSize);
                float fz = p.z / SimConst.CellSize - (float)Math.Floor(p.z / SimConst.CellSize);
                samples++;
                if (Math.Abs(fx - 0.5f) < 0.02f && Math.Abs(fz - 0.5f) < 0.02f) onCenters++;
            }
            Check(onCenters < samples / 10, $"las posiciones no se quedan en los centros de celda ({onCenters} de {samples})");
        }

        // ------------------------------------------------------------------ rasterizado parcial
        static void TestRaster()
        {
            Console.WriteLine("[4] Rasterizado de sólidos y construcción");
            var solids = Level.CreateSolids();
            var w = new MiniWorld(solids, 76f, 64.5f);
            Check(w.Ground[Idx(71f, 50f)] == Level.WallH, "el muro oeste está en la rejilla con su altura");
            Check(w.Ground[Idx(71f, 64.5f)] == Level.WallH, "la puerta cerrada cuenta como muro");
            Check(w.Ground[Idx(45f, 40f)] == Level.RoofH, "el edificio está en la rejilla");
            Check(w.Ground[Idx(10f, 10f)] == 0f, "el campo abierto tiene altura cero");

            // construir un muro nuevo y recalcular solo su región
            var groundList = new List<CellGround>();
            var gb = new DynamicBuffer<CellGround>(groundList);
            gb.ResizeUninitialized(w.N);
            for (int i = 0; i < w.N; i++) groundList[i] = new CellGround { Value = w.Ground[i] };
            var sb = new DynamicBuffer<Solid>(w.SolidList);
            var nw = new Solid { A = new float2(20f, 20f), B = new float2(30f, 25f), HalfThick = 0.5f, Height = 2.5f, Kind = 0 };
            sb.Add(nw);
            Level.RasterSolid(sb, gb, nw);
            // comparar con un rasterizado completo
            var full = new List<CellGround>();
            var fb = new DynamicBuffer<CellGround>(full);
            fb.ResizeUninitialized(w.N);
            Level.RasterRegion(sb, fb, 0, 0, SimConst.GridW - 1, SimConst.GridH - 1);
            int diff = 0;
            for (int i = 0; i < w.N; i++) if (groundList[i].Value != full[i].Value) diff++;
            Check(diff == 0, $"rasterizar solo la región del muro nuevo da lo mismo que rasterizar todo ({diff} celdas distintas)");
            int touched = 0;
            for (int i = 0; i < w.N; i++) if (groundList[i].Value != w.Ground[i]) touched++;
            Check(touched > 0 && touched < 400, $"el muro nuevo solo cambia {touched} celdas de {w.N}");
        }

        // ------------------------------------------------------------------ caída desde el tejado
        static void TestRoofFall()
        {
            Console.WriteLine("[5] Caída desde el tejado");
            var w = new MiniWorld(Level.CreateSolids(), 76f, 64.5f);
            w.Spawn(40, 42f, 49f, 32f, 49f, Level.RoofH, 3u);
            bool sawFall = false;
            for (int t = 0; t < 900; t++)
            {
                w.Tick1();
                if (w.Count(ZState.Fall) > 0) sawFall = true;
            }
            int onRoof = 0, falling = w.Count(ZState.Fall);
            foreach (var z in w.Z) if (z.Pos.y > Level.RoofH - 0.5f && SolidMath.Contains(w.SolidList[6], new float2(z.Pos.x, z.Pos.z))) onRoof++;
            Check(sawFall, "los zombis del tejado entran en caída libre al salir del borde");
            Check(onRoof == 0, $"al cabo de 30 s ninguno sigue sobre el tejado (quedan {onRoof})");
            float maxY = 0f; foreach (var z in w.Z) maxY = Math.Max(maxY, z.Pos.y);
            Check(falling == 0 || maxY < 4.5f, $"los que han caído aterrizan (cayendo ahora: {falling}, altura máxima {maxY:0.0} m)");
        }

        // ------------------------------------------------------------------ diagnóstico: un zombi contra el muro
        static void TestOneZombieAtWall()
        {
            Console.WriteLine("[5b] Diagnóstico: un zombi contra el muro oeste (informativo)");
            var w = new MiniWorld(Level.CreateSolids(), 76f, 64.5f);
            w.Spawn(1, 66f, 66f, 60f, 60f, 0f, 5u);
            for (int t = 1; t <= 400; t++)
            {
                w.Tick1();
                if (t % 40 == 0)
                {
                    var z = w.Z[0];
                    var f = Unity.Mathematics.math.lerp(new float2(0, 0), w.Vec[Idx(z.Pos.x, z.Pos.z)], 1f);
                    Console.WriteLine($"         t={t,3} pos=({z.Pos.x:0.00},{z.Pos.y:0.00},{z.Pos.z:0.00}) vel=({z.Vel.x:0.00},{z.Vel.z:0.00}) estado={z.State} flujo=({f.x:0.00},{f.y:0.00}) pilaCelda={w.Pile[Idx(z.Pos.x, z.Pos.z)]}");
                }
            }
        }

        // ------------------------------------------------------------------ amontonamiento y desborde del muro
        static void TestWallPileSpill()
        {
            Console.WriteLine("[6] Amontonamiento contra el muro y desborde");
            var w = new MiniWorld(Level.CreateSolids(), 76f, 64.5f);
            w.Spawn(1500, 52f, 66f, 54f, 76f, 0f, 11u);
            int violations = 0;
            int settledAt300 = 0, maxPileAt900 = 0;
            for (int t = 1; t <= 2700; t++)
            {
                w.Tick1();
                if (t % 5 == 0)
                    foreach (var z in w.Z)
                    {
                        if (z.State == ZState.Fall) continue;
                        for (int i = 0; i < 5; i++)                         // los cinco muros del fuerte (sin puerta ni edificio)
                        {
                            var s = w.SolidList[i];
                            if (SolidMath.Contains(s, new float2(z.Pos.x, z.Pos.z)) && z.Pos.y < s.Height - SimConst.StepUp - 0.05f) violations++;   // subir un escalón de 0,6 m está permitido
                        }
                    }
                if (t % 300 == 0)
                {
                    int near = 0, nearWalk = 0, nearSet = 0; float zmin = 999f, zmax = -999f;
                    foreach (var z in w.Z)
                        if (z.Pos.x > 67f && z.Pos.x < 70.4f) { near++; if (z.State == ZState.Settled) nearSet++; else nearWalk++; zmin = Math.Min(zmin, z.Pos.z); zmax = Math.Max(zmax, z.Pos.z); }
                    Console.WriteLine($"         t={t,4}: junto al muro {near} (apilados {nearSet}, andando {nearWalk}) z[{zmin:0.0},{zmax:0.0}] pila máx {w.MaxPile()} ({Math.Min(w.MaxPile(), SimConst.MaxPile) * SimConst.Thick:0.0} m) apilados {w.Count(ZState.Settled)} cayendo {w.Count(ZState.Fall)}");
                }
                if (t == 300) settledAt300 = w.Count(ZState.Settled);
                if (t == 900) maxPileAt900 = w.MaxPile();
            }
            int inside = 0;
            foreach (var z in w.Z) if (z.Pos.x > 73f && z.Pos.x < 99f && z.Pos.z > 46f && z.Pos.z < 83f) inside++;
            Console.WriteLine($"         t=300: apilados {settledAt300} · t=900: pila máxima {maxPileAt900} · t=2700: dentro del fuerte {inside}, apilados {w.Count(ZState.Settled)}, cayendo {w.Count(ZState.Fall)}, pila máxima {w.MaxPile()}");
            int shown = 0;
            foreach (var z in w.Z)
                if (z.State == ZState.Fall && shown++ < 6)
                    Console.WriteLine($"         cayendo: x={z.Pos.x:0.0} y={z.Pos.y:0.00} z={z.Pos.z:0.0} vy={z.Vel.y:0.0} vx={z.Vel.x:0.0} vz={z.Vel.z:0.0}");
            Check(violations == 0, $"ningún zombi atraviesa el muro (violaciones: {violations})");
            Check(settledAt300 > 20, $"a los 10 s hay zombis apilados contra el muro ({settledAt300})");
            Check(maxPileAt900 >= 4, $"la pila crece contra el muro (máximo {maxPileAt900} por celda a los 30 s)");
            Check(inside > 0, $"la horda se derrama por encima del muro cuando la pila lo alcanza (dentro: {inside})");
            Check(w.Count(ZState.Fall) < w.Z.Length / 10, $"no hay zombis atascados alternando entre caer y andar (cayendo: {w.Count(ZState.Fall)} de {w.Z.Length})");
        }

        // ------------------------------------------------------------------ índice espacial de sólidos
        static void TestSolidIndex()
        {
            Console.WriteLine("[5c] Índice espacial de sólidos");
            uint seed = 99u;
            float Next() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / (float)0x1000000; }
            var solids = Level.CreateSolids();
            for (int i = 0; i < 300; i++)
            {
                var a = new float2(5f + Next() * 118f, 5f + Next() * 118f);
                var b = new float2(math.clamp(a.x + (Next() - 0.5f) * 14f, 2f, 126f), math.clamp(a.y + (Next() - 0.5f) * 14f, 2f, 126f));
                solids.Add(new Solid { A = a, B = b, HalfThick = 0.5f, Height = 2.5f, Kind = 0 });
            }
            var w = new MiniWorld(solids, 76f, 64.5f);
            int mismatchGround = 0, mismatchPush = 0, samples = 20000;
            for (int k = 0; k < samples; k++)
            {
                var p = new float2(Next() * 127f + 0.5f, Next() * 127f + 0.5f);
                float linear = 0f;
                foreach (var s in solids) if (SolidMath.Contains(s, p)) linear = Math.Max(linear, s.Height);
                if (ZombieLogic.GroundAt(w.Solids, w.BStart, w.BRefs, p) != linear) mismatchGround++;
                // conjunto de sólidos que empujan: lineal frente a índice
                int linearPush = 0;
                float2 n; float d;
                foreach (var s in solids) if (SolidMath.Push(s, p, SimConst.Radius, out n, out d)) linearPush++;
                int bucketPush = 0;
                int b = SolidIndex.BucketOf(p);
                for (int r = w.BStart[b]; r < w.BStart[b + 1]; r++) if (SolidMath.Push(solids[w.BRefs[r]], p, SimConst.Radius, out n, out d)) bucketPush++;
                if (linearPush != bucketPush) mismatchPush++;
            }
            Check(mismatchGround == 0, $"la altura del suelo con el índice coincide con el recorrido lineal ({mismatchGround} diferencias en {samples})");
            Check(mismatchPush == 0, $"los sólidos que empujan con el índice coinciden con el recorrido lineal ({mismatchPush} diferencias en {samples})");

            // mismo resultado de la simulación completa que sin índice (una sola cubeta con todos): se compara el hash
            var plain = new MiniWorld(solids, 76f, 64.5f);
            plain.Spawn(300, 52f, 66f, 54f, 76f, 0f, 41u);
            var indexed = new MiniWorld(solids, 76f, 64.5f);
            indexed.Spawn(300, 52f, 66f, 54f, 76f, 0f, 41u);
            // "sin índice": todas las cubetas apuntan a todos los sólidos
            int ns = solids.Count, bc = SolidIndex.BucketCount;
            var allStart = new int[bc + 1]; var allRefs = new int[bc * ns];
            for (int bk = 0; bk < bc; bk++) { allStart[bk] = bk * ns; for (int i = 0; i < ns; i++) allRefs[bk * ns + i] = i; }
            allStart[bc] = bc * ns;
            plain.BStart = new NativeArray<int>(allStart); plain.BRefs = new NativeArray<int>(allRefs);
            bool same = true;
            for (int t = 0; t < 300; t++) { plain.Tick1(); indexed.Tick1(); if (plain.Hash() != indexed.Hash()) { same = false; break; } }
            Check(same, "300 ticks con el índice dan el mismo hash que consultando todos los sólidos");

            PerfIndexed(solids);
        }

        /// <summary>Un tick de 10.000 zombis con 307 sólidos y el índice espacial (y, solo para informar, sin índice). Se vigila con PerfGate, no con una media suelta.</summary>
        static void PerfIndexed(List<Solid> solids)
        {
            int ns = solids.Count, bc = SolidIndex.BucketCount;
            var allStart = new int[bc + 1]; var allRefs = new int[bc * ns];
            for (int bk = 0; bk < bc; bk++) { allStart[bk] = bk * ns; for (int i = 0; i < ns; i++) allRefs[bk * ns + i] = i; }
            allStart[bc] = bc * ns;
            var big = new MiniWorld(solids, 76f, 64.5f);
            big.Spawn(10000, 4f, 120f, 4f, 120f, 0f, 51u);
            for (int t = 0; t < 5; t++) big.Tick1();
            var lin = new MiniWorld(solids, 76f, 64.5f);
            lin.BStart = new NativeArray<int>(allStart); lin.BRefs = new NativeArray<int>(allRefs);
            lin.Spawn(10000, 4f, 120f, 4f, 120f, 0f, 51u);
            for (int t = 0; t < 2; t++) lin.Tick1();
            var sw2 = Stopwatch.StartNew();
            for (int t = 0; t < 5; t++) lin.Tick1();
            sw2.Stop();
            Console.WriteLine($"         lo mismo consultando todos los sólidos: {sw2.Elapsed.TotalMilliseconds / 5:0.00} ms por tick");
            PerfGate.Verdict($"con {solids.Count} sólidos un tick de 10.000 zombis", () => big.Tick1(), 30, PerfGate.RatioIndexed);
        }

        // ------------------------------------------------------------------ separación entre agentes
        static void TestSeparation()
        {
            Console.WriteLine("[6d] Separación por densidad (la horda no se colapsa en filas)");
            var w = new MiniWorld(Level.CreateSolids(), 76f, 64.5f);
            w.Spawn(2000, 4f, 40f, 20f, 110f, 0f, 77u);
            for (int t = 0; t < 300; t++) w.Tick1();
            var occ = new Dictionary<int, int>();
            int walking = 0;
            foreach (var z in w.Z)
            {
                if (z.State != ZState.Walk || z.Pos.y > 0.01f) continue;      // solo los que andan por el suelo
                int c = (int)(z.Pos.z / SimConst.CellSize) * SimConst.GridW + (int)(z.Pos.x / SimConst.CellSize);
                occ[c] = occ.TryGetValue(c, out var n) ? n + 1 : 1;
                walking++;
            }
            int maxCell = 0; foreach (var v in occ.Values) maxCell = Math.Max(maxCell, v);
            float perCell = occ.Count > 0 ? walking / (float)occ.Count : 0f;
            // Sin separación (SepGain = 0) daba 1,38 por celda y un máximo de 9; con 0,35 da 1,22 y 4 (5-nov).
            Console.WriteLine($"         a los 10 s: {walking} andando en {occ.Count} celdas ({perCell:0.00} por celda ocupada, máximo {maxCell})");
            Check(perCell < 1.3f, $"los que andan se reparten (media {perCell:0.00} por celda ocupada de 0,5 m)");
            Check(maxCell <= 5, $"ninguna celda de suelo concentra una fila entera (máximo {maxCell})");
        }

        // ------------------------------------------------------------------ determinismo
        static void TestDeterminism()
        {
            Console.WriteLine("[7] Determinismo e independencia del orden");
            MiniWorld Make()
            {
                var w = new MiniWorld(Level.CreateSolids(), 76f, 64.5f);
                w.Spawn(400, 52f, 66f, 54f, 76f, 0f, 21u);
                w.Spawn(60, 42f, 49f, 32f, 49f, Level.RoofH, 22u);
                return w;
            }
            var a = Make(); var b = Make(); var c = Make();
            int n = a.Z.Length;
            var rev = new int[n]; for (int i = 0; i < n; i++) rev[i] = n - 1 - i;
            var rnd = new int[n]; for (int i = 0; i < n; i++) rnd[i] = i;
            uint s = 12345u; for (int i = n - 1; i > 0; i--) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; int j = (int)(s % (uint)(i + 1)); (rnd[i], rnd[j]) = (rnd[j], rnd[i]); }
            bool sameRuns = true, sameOrder = true;
            int firstDiff = -1;
            for (int t = 0; t < 900; t++)
            {
                a.Tick1(); b.Tick1(); c.Tick1(t % 2 == 0 ? rev : rnd);
                if (a.Hash() != b.Hash()) sameRuns = false;
                if (a.Hash() != c.Hash()) { sameOrder = false; if (firstDiff < 0) firstDiff = t; }
            }
            Check(sameRuns, "dos ejecuciones iguales dan el mismo hash en cada tick");
            Check(sameOrder, "procesar los zombis en otro orden da el mismo hash (doble búfer y deltas conmutativos)" + (firstDiff >= 0 ? $" — primera diferencia en el tick {firstDiff}" : ""));
        }

        // ------------------------------------------------------------------ coste
        static void TestPerfOnly()
        {
            uint seed = 99u;
            float Next() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / (float)0x1000000; }
            var solids = Level.CreateSolids();                                // los mismos 307 sólidos que en [5c]
            for (int i = 0; i < 300; i++)
            {
                var a = new float2(5f + Next() * 118f, 5f + Next() * 118f);
                var b = new float2(math.clamp(a.x + (Next() - 0.5f) * 14f, 2f, 126f), math.clamp(a.y + (Next() - 0.5f) * 14f, 2f, 126f));
                solids.Add(new Solid { A = a, B = b, HalfThick = 0.5f, Height = 2.5f, Kind = 0 });
            }
            PerfIndexed(solids);
            TestPerf();
        }

        static void TestPerf()
        {
            Console.WriteLine("[8] Coste (C# en Release, sin Burst: solo orientativo)");
            var w = new MiniWorld(Level.CreateSolids(), 76f, 64.5f);
            w.Spawn(10000, 4f, 60f, 8f, 120f, 0f, 31u);
            for (int t = 0; t < 10; t++) w.Tick1();
            var sw2 = Stopwatch.StartNew();
            w.Rebuild(76f, 64.5f);
            sw2.Stop();
            Console.WriteLine($"         recalcular el campo de flujo completo (65.536 celdas): {sw2.Elapsed.TotalMilliseconds:0.0} ms");
            PerfGate.Verdict("un tick de 10.000 zombis y 7 sólidos", () => w.Tick1(), 30, PerfGate.RatioSimple);
        }
    }
}
