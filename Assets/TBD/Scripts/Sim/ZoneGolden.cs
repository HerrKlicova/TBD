using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TBD.Sim
{
    /// <summary>
    /// Repetición dorada de la ZONA (G1 y G2 del criterio de Sara): mundo de Entities propio, sin dibujo, con los mismos sistemas y
    /// Burst que la partida. Graba las órdenes (con los float en bits exactos) y el hash de cada tick, más el hash por sistema cada
    /// 300 ticks: zombis, pila, sólidos, soldados, munición y tren/puerta. Se reproduce en el Editor y en una compilación.
    /// </summary>
    public static class ZoneGolden
    {
        public const string Format = "TBD-GOLDEN-ZONA v1";
        public const int SystemEvery = 300;
        public static readonly string[] SystemNames = { "zombis", "pila", "solidos", "soldados", "municion", "tren_y_puerta" };

        public struct Setup { public string Scenario; public int Zombies; public bool Siege; public uint Seed; public long AmmoShots; public bool HasTrain; public int Ticks; }

        public struct Result
        {
            public bool Ok; public long FirstBadTick; public string System; public long TicksChecked;
            public override string ToString() => Ok ? $"idéntica ({TicksChecked} ticks)" : $"DIFIERE en el tick {FirstBadTick} (sistema: {System})";
        }

        static string F(float v) => math.asuint(v).ToString("X8");
        static float Fp(string s) => math.asfloat(uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

        public static string Record(Setup s, List<SimCommand> script)
        {
            var sb = new StringBuilder();
            sb.Append(Format).Append('\n');
            sb.Append("escenario=").Append(s.Scenario).Append('\n');
            sb.Append("zombis=").Append(s.Zombies).Append('\n');
            sb.Append("asedio=").Append(s.Siege ? 1 : 0).Append('\n');
            sb.Append("semilla=").Append(s.Seed).Append('\n');
            sb.Append("municion=").Append(s.AmmoShots).Append('\n');
            sb.Append("tren=").Append(s.HasTrain ? 1 : 0).Append('\n');
            sb.Append("ticks=").Append(s.Ticks).Append('\n');
            sb.Append("ORDENES\n");
            foreach (var c in script)
                sb.Append(c.Tick).Append(';').Append((int)c.Type).Append(';').Append(c.Arg).Append(';')
                  .Append(F(c.P0.x)).Append(';').Append(F(c.P0.y)).Append(';').Append(F(c.P1.x)).Append(';').Append(F(c.P1.y)).Append('\n');
            sb.Append("HASHES\n");
            Play(s, script, (t, h, sys) =>
            {
                sb.Append(t).Append(';').Append(h.ToString("X16"));
                if (sys != null) foreach (var x in sys) sb.Append(';').Append(x.ToString("X16"));
                sb.Append('\n');
                return true;
            }, null);
            sb.Append("FIN\n");
            return sb.ToString();
        }

        public static Result Verify(string golden, Action<EntityManager, Entity, long> tamper = null)
        {
            var lines = golden.Replace("\r", "").Split('\n');
            if (lines.Length < 10 || lines[0] != Format) throw new FormatException("No es un archivo dorado de zona (" + Format + ")");
            var s = new Setup();
            int i = 1;
            for (; i < lines.Length && lines[i] != "ORDENES"; i++)
            {
                var kv = lines[i].Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;
                switch (kv[0])
                {
                    case "escenario": s.Scenario = kv[1]; break;
                    case "zombis": s.Zombies = int.Parse(kv[1], CultureInfo.InvariantCulture); break;
                    case "asedio": s.Siege = kv[1] == "1"; break;
                    case "semilla": s.Seed = uint.Parse(kv[1], CultureInfo.InvariantCulture); break;
                    case "municion": s.AmmoShots = long.Parse(kv[1], CultureInfo.InvariantCulture); break;
                    case "tren": s.HasTrain = kv[1] == "1"; break;
                    case "ticks": s.Ticks = int.Parse(kv[1], CultureInfo.InvariantCulture); break;
                }
            }
            var script = new List<SimCommand>();
            for (i++; i < lines.Length && lines[i] != "HASHES"; i++)
            {
                var f = lines[i].Split(';');
                script.Add(new SimCommand
                {
                    Tick = uint.Parse(f[0], CultureInfo.InvariantCulture), Type = (CmdType)int.Parse(f[1], CultureInfo.InvariantCulture),
                    Arg = int.Parse(f[2], CultureInfo.InvariantCulture), P0 = new float2(Fp(f[3]), Fp(f[4])), P1 = new float2(Fp(f[5]), Fp(f[6])),
                });
            }
            var expected = new Dictionary<long, string[]>();
            for (i++; i < lines.Length && lines[i] != "FIN"; i++)
            {
                if (lines[i].Length == 0) continue;
                var f = lines[i].Split(';');
                expected[long.Parse(f[0], CultureInfo.InvariantCulture)] = f;
            }
            var r = new Result { Ok = true, FirstBadTick = -1, System = "" };
            string[] pendingFrom = null;
            Play(s, script, (t, h, sys) =>
            {
                r.TicksChecked++;
                if (!expected.TryGetValue(t, out var f)) { r.Ok = false; r.FirstBadTick = t; r.System = "falta en el archivo"; return false; }
                if (r.Ok)
                {
                    if (f[1] != h.ToString("X16") || (sys != null && !Same(f, sys)))
                    {
                        r.Ok = false; r.FirstBadTick = t;
                        if (sys != null) { r.System = Culprit(f, sys); return false; }
                        r.System = "?";
                        return true;                                   // seguir hasta el siguiente bloque para nombrar al culpable
                    }
                    return true;
                }
                if (sys != null) { r.System = Culprit(f, sys); return false; }
                return true;
            }, tamper);
            return r;
        }

        static bool Same(string[] f, ulong[] sys)
        {
            if (f.Length < 2 + sys.Length) return false;
            for (int k = 0; k < sys.Length; k++) if (f[2 + k] != sys[k].ToString("X16")) return false;
            return true;
        }

        static string Culprit(string[] f, ulong[] sys)
        {
            var names = new List<string>();
            for (int k = 0; k < sys.Length && 2 + k < f.Length; k++) if (f[2 + k] != sys[k].ToString("X16")) names.Add(SystemNames[k]);
            return names.Count > 0 ? string.Join(", ", names) : "?";
        }

        /// <summary>Crea un mundo propio, mete el guion en la cola de órdenes y avanza tick a tick.</summary>
        static void Play(Setup s, List<SimCommand> script, Func<long, ulong, ulong[], bool> onTick, Action<EntityManager, Entity, long> tamper)
        {
            var world = new World("TBD_Golden");
            try
            {
                var group = SimWorldSetup.CreateSystems(world);
                var em = world.EntityManager;
                var root = SimWorldSetup.Populate(em, s.Zombies, s.Siege, s.Seed, CombatParams.Default, s.AmmoShots, s.HasTrain);
                var buf = em.GetBuffer<SimCommand>(root);
                foreach (var c in script) buf.Add(c);
                for (int k = 0; k < s.Ticks; k++)
                {
                    group.Update();
                    var hash = em.GetComponentData<SimHash>(root);
                    tamper?.Invoke(em, root, hash.Tick);
                    ulong[] sys = null;
                    if (hash.Tick % SystemEvery == 0) sys = Systems(em, root);
                    if (!onTick(hash.Tick, hash.Value, sys)) break;
                }
            }
            finally { world.Dispose(); }
        }

        /// <summary>Hash por sistema de la zona (D3 de Sara).</summary>
        public static ulong[] Systems(EntityManager em, Entity root)
        {
            var r = new ulong[6];
            var zq = em.CreateEntityQuery(ComponentType.ReadOnly<Zombie>());
            var zs = zq.ToComponentDataArray<Zombie>(Allocator.Temp);
            for (int i = 0; i < zs.Length; i++) r[0] += HashMath.OfZombie(zs[i]);
            zs.Dispose();
            var pile = em.GetBuffer<CellPile>(root);
            for (int i = 0; i < pile.Length; i++) r[1] += HashMath.OfPileCell(i, pile[i].Value);
            var solids = em.GetBuffer<Solid>(root);
            for (int i = 0; i < solids.Length; i++) r[2] += HashMath.OfSolid(i, solids[i]);
            var sq = em.CreateEntityQuery(ComponentType.ReadOnly<Soldier>());
            var ss = sq.ToComponentDataArray<Soldier>(Allocator.Temp);
            for (int i = 0; i < ss.Length; i++) r[3] += HashMath.OfSoldier(ss[i]);
            ss.Dispose();
            r[4] = HashMath.OfAmmo(em.GetComponentData<ZoneAmmo>(root));
            var t = em.GetComponentData<TrainState>(root);
            var info = em.GetComponentData<ZoneInfo>(root);
            r[5] = HashMath.OfFloat(t.X, 21) + HashMath.OfFloat(t.Speed, 22) + HashMath.Mix((ulong)(uint)t.Dir ^ (info.GateOpen ? 1UL << 40 : 0UL) ^ (info.HasTrain ? 1UL << 41 : 0UL));
            return r;
        }

        // ---------------------------------------------------------------- escenarios del criterio de Sara
        public static Setup G1() => new Setup { Scenario = "G1 zona del H1: 10.000 zombis, tejado, puerta y muro libre", Zombies = 10000, Siege = false, Seed = 12345u, AmmoShots = 0, HasTrain = true, Ticks = 3000 };

        public static List<SimCommand> ScriptG1() => new List<SimCommand>
        {
            new SimCommand { Tick = 30, Type = CmdType.SpawnRoof, Arg = 500 },
            new SimCommand { Tick = 600, Type = CmdType.ToggleGate },
            new SimCommand { Tick = 900, Type = CmdType.BuildWall, P0 = new float2(40.5f, 60.25f), P1 = new float2(52.75f, 71.5f) },
            new SimCommand { Tick = 1200, Type = CmdType.SpawnWest, Arg = 3000 },
            new SimCommand { Tick = 1800, Type = CmdType.ToggleGate },
            new SimCommand { Tick = 2100, Type = CmdType.ToggleTrain },
            new SimCommand { Tick = 2400, Type = CmdType.ToggleTrain },
        };

        public static Setup G2() => new Setup { Scenario = "G2 zona con tropas: tren parado, a bordo, a pie, reembarcar", Zombies = 3000, Siege = true, Seed = 777u, AmmoShots = 20L * 2000, HasTrain = true, Ticks = 3000 };

        public static List<SimCommand> ScriptG2() => new List<SimCommand>
        {
            new SimCommand { Tick = 20, Type = CmdType.EmbarkSquad },
            new SimCommand { Tick = 900, Type = CmdType.ToggleTrain },
            new SimCommand { Tick = 1000, Type = CmdType.ToggleTrain },
            new SimCommand { Tick = 1300, Type = CmdType.DeploySquad },
            new SimCommand { Tick = 2000, Type = CmdType.Reembark },
            new SimCommand { Tick = 2500, Type = CmdType.SpawnWest, Arg = 1500 },
        };
    }
}
