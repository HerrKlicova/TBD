using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TBD.Net;
using Unity.Collections;
using Unity.Entities;

namespace TBD.Sim
{
    /// <summary>
    /// Repetición dorada G4 (criterio de Sara): el bucle mapa ↔ zona dos veces, sin vista. La red y la zona avanzan a la par
    /// (1 tick de red por tick de zona, como a x1), con las mismas reglas que la partida: Materializar, zona con la horda de la celda,
    /// pelotón a bordo si hay tren en el andén, munición de la carga y la estación, y al salir ConsumeAmmo y Desmaterializar.
    /// Se graba el hash de la red y el de la zona en cada tick, y los sistemas de ambas cada 300 ticks de red.
    /// </summary>
    public static class LoopGolden
    {
        public const string Format = "TBD-GOLDEN-BUCLE v1";
        const int ZoneTicks = 900;                       // 30 s de zona en cada visita
        static readonly double[] EnterHours = { 2.8, 6.0 };   // horas de juego (desde el arranque) en que se entra
        static readonly string[] EnterNodes = { "Sagunto", "Castellón" };

        public struct Result
        {
            public bool Ok; public long FirstBadTick; public string Where; public long TicksChecked;
            public override string ToString() => Ok ? $"idéntica ({TicksChecked} ticks)" : $"DIFIERE en el tick {FirstBadTick} ({Where})";
        }

        public static string Record(long netTicks = 15000)
        {
            var sb = new StringBuilder();
            sb.Append(Format).Append('\n').Append("escenario=G4 bucle mapa-zona dos veces (Sagunto y Castellón)\n").Append("version=0.1.0\n")
              .Append("ticks=").Append(netTicks).Append('\n').Append("HASHES\n");
            Play(netTicks, (t, net, zone, sys) =>
            {
                sb.Append(t).Append(';').Append(net.ToString("X16")).Append(';').Append(zone.ToString("X16"));
                if (sys != null) foreach (var x in sys) sb.Append(';').Append(x.ToString("X16"));
                sb.Append('\n');
                return true;
            });
            sb.Append("FIN\n");
            return sb.ToString();
        }

        public static Result Verify(string golden)
        {
            var lines = golden.Replace("\r", "").Split('\n');
            if (lines.Length < 5 || lines[0] != Format) throw new FormatException("No es un archivo dorado del bucle (" + Format + ")");
            long ticks = 0;
            int i = 1;
            for (; i < lines.Length && lines[i] != "HASHES"; i++) if (lines[i].StartsWith("ticks=")) ticks = long.Parse(lines[i].Substring(6), CultureInfo.InvariantCulture);
            var exp = new Dictionary<long, string[]>();
            for (i++; i < lines.Length && lines[i] != "FIN"; i++) { if (lines[i].Length == 0) continue; var f = lines[i].Split(';'); exp[long.Parse(f[0], CultureInfo.InvariantCulture)] = f; }
            var r = new Result { Ok = true, FirstBadTick = -1, Where = "" };
            Play(ticks, (t, net, zone, sys) =>
            {
                r.TicksChecked++;
                if (!exp.TryGetValue(t, out var f)) { r.Ok = false; r.FirstBadTick = t; r.Where = "falta en el archivo"; return false; }
                bool netOk = f[1] == net.ToString("X16"), zoneOk = f[2] == zone.ToString("X16");
                if (netOk && zoneOk) return true;
                r.Ok = false; r.FirstBadTick = t; r.Where = !netOk && !zoneOk ? "red y zona" : (!netOk ? "red" : "zona");
                return false;
            });
            return r;
        }

        static void Play(long netTicks, Func<long, ulong, ulong, ulong[], bool> onTick)
        {
            var net = NetScenario.Mvp();
            long start = net.Tick;
            int visit = 0;
            World zw = null;
            SimRootGroup zg = null;
            Entity zroot = default;
            int zoneLeft = 0, zoneNode = -1;
            try
            {
                while (net.Tick < start + netTicks)
                {
                    // ¿toca entrar en una zona?
                    if (zw == null && zoneNode < 0 && visit < EnterHours.Length && net.Tick >= start + NetConst.Hours(EnterHours[visit]))
                    {
                        zoneNode = net.Graph.Nodes.FindIndex(n => n.Name == EnterNodes[visit]);
                        net.Push(new NetCommand { Tick = net.Tick, Type = NetCmdType.Materialize, Arg = zoneNode });
                        visit++;
                    }
                    net.Step();
                    // la orden se aplicó: se crea la zona como en la partida
                    if (zw == null && zoneNode >= 0 && net.ActiveZoneNode == zoneNode)
                    {
                        bool hasTrain = false;
                        foreach (var sv in net.Services) if (sv.AtStop && sv.Stops.Count > 0 && sv.Stops[sv.StopIndex].Node == zoneNode) hasTrain = true;
                        zw = new World("TBD_GoldenBucle");
                        zg = SimWorldSetup.CreateSystems(zw);
                        uint seed = (uint)(zoneNode * 7919 + net.ActiveZoneSinceTick * 31 + 1);
                        zroot = SimWorldSetup.Populate(zw.EntityManager, net.ActiveZoneTaken * NetWorld.ZombiesPerPermil, net.ActiveZoneTaken >= NetWorld.SiegeAt,
                                                       seed, CombatParams.Default, net.AmmoAvailableAt(zoneNode) * CombatParams.Default.ShotsPerUnit, hasTrain);
                        zw.EntityManager.GetBuffer<SimCommand>(zroot).Add(new SimCommand { Tick = 5, Type = CmdType.EmbarkSquad });
                        zoneLeft = ZoneTicks;
                    }
                    ulong zh = 0;
                    if (zw != null)
                    {
                        zg.Update();
                        zh = zw.EntityManager.GetComponentData<SimHash>(zroot).Value;
                        if (--zoneLeft <= 0)
                        {
                            // salir: munición gastada (redondeo hacia arriba) y vivos devueltos a la celda
                            var em = zw.EntityManager;
                            var ammo = em.GetComponentData<ZoneAmmo>(zroot);
                            int used = (int)((ammo.ShotsFired + CombatParams.Default.ShotsPerUnit - 1) / CombatParams.Default.ShotsPerUnit);
                            int alive = em.CreateEntityQuery(ComponentType.ReadOnly<Zombie>()).CalculateEntityCount();
                            if (used > 0) net.Push(new NetCommand { Tick = net.Tick, Type = NetCmdType.ConsumeAmmo, Arg = zoneNode, Arg2 = used });
                            net.Push(new NetCommand { Tick = net.Tick, Type = NetCmdType.Dematerialize, Arg = zoneNode, Arg2 = alive / NetWorld.ZombiesPerPermil });
                            zw.Dispose(); zw = null; zg = null; zoneNode = -1;
                        }
                    }
                    ulong[] sys = null;
                    if (net.Tick % 300 == 0)
                    {
                        var s = net.Systems();
                        sys = new[] { s.Control, s.Field, s.Trains, s.Stock, s.Edges };
                    }
                    if (!onTick(net.Tick, net.Hash(), zh, sys)) return;
                }
            }
            finally { if (zw != null && zw.IsCreated) zw.Dispose(); }
        }
    }
}
