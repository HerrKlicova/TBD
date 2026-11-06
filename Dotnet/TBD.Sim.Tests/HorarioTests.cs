using System;
using System.Collections.Generic;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Hito 5: poner horario a un tren (SetSchedule): validación, parada actual, salida y carga, hash y repetición.</summary>
    static class HorarioTests
    {
        static int Id(NetWorld w, string name) { for (int i = 0; i < w.Graph.Nodes.Count; i++) if (w.Graph.Nodes[i].Name == name) return i; return -1; }
        static int[] Stop(int node, long at, int mun = 0, int comb = 0, int mat = 0, int per = 0) { return new[] { node, (int)at, mun, comb, mat, per }; }
        static int[] Cat(params int[][] s) { var l = new List<int>(); foreach (var x in s) l.AddRange(x); return l.ToArray(); }

        /// <summary>Un tren recién salido del taller: parado en Sagunto con una sola parada y sin horario.</summary>
        static NetWorld WithParked(out int svIndex)
        {
            var w = NetScenario.Mvp(false);
            int sag = Id(w, "Sagunto");
            var s = new TrainService { Train = new RailTrain { Id = 3, Name = "3 Nuevo", SpeedMmPerTick = NetConst.TrainMmPerTick }, StopIndex = 0, AtStop = true };
            s.Stops.Add(new ScheduleStop { Node = sag, DepartAt = 0, Transfer = new int[NetConst.ResCount] });
            w.Services.Add(s); svIndex = w.Services.Count - 1;
            return w;
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[25] Poner horario a un tren (SetSchedule, hito 5)");
            var w = WithParked(out int si); int sag = Id(w, "Sagunto"), cas = Id(w, "Castellón"), val = Id(w, "Valencia");
            ulong h0 = w.Hash();
            check(w.Services[si].Stops.Count == 1 && !w.Services[si].ScheduleEdited, "un tren sin horario tiene una sola parada: no se mueve");

            // validación: nada cambia si no vale
            check(w.SetSchedule(99, Cat(Stop(sag, 100), Stop(cas, 200))) == ScheduleResult.TrenInexistente && w.SetSchedule(-1, Cat(Stop(sag, 100))) == ScheduleResult.TrenInexistente, "un tren que no existe se rechaza");
            check(w.SetSchedule(si, null) == ScheduleResult.HorarioInvalido && w.SetSchedule(si, new int[0]) == ScheduleResult.HorarioInvalido && w.SetSchedule(si, new[] { sag, 0, 0 }) == ScheduleResult.HorarioInvalido, "sin datos o con una parada a medias se rechaza");
            check(w.SetSchedule(si, Cat(Stop(sag, 100), Stop(999, 200))) == ScheduleResult.HorarioInvalido && w.SetSchedule(si, Cat(Stop(sag, 100), Stop(-1, 200))) == ScheduleResult.HorarioInvalido, "un nodo que no existe se rechaza");
            check(w.SetSchedule(si, Cat(Stop(sag, -1), Stop(cas, 200))) == ScheduleResult.HorarioInvalido && w.SetSchedule(si, Cat(Stop(sag, NetConst.DayTicks), Stop(cas, 200))) == ScheduleResult.HorarioInvalido, "la hora de salida tiene que estar dentro del día");
            check(w.SetSchedule(si, Cat(Stop(sag, 100), Stop(sag, 200))) == ScheduleResult.HorarioInvalido && w.SetSchedule(si, Cat(Stop(sag, 100), Stop(cas, 200), Stop(sag, 300))) == ScheduleResult.HorarioInvalido, "dos paradas seguidas en el mismo nodo (también el cierre) se rechazan");
            check(w.SetSchedule(si, Cat(Stop(sag, 100, mat: 1001), Stop(cas, 200))) == ScheduleResult.HorarioInvalido && w.SetSchedule(si, Cat(Stop(sag, 100, mat: -1001), Stop(cas, 200))) == ScheduleResult.HorarioInvalido, "una carga de más de 1.000 se rechaza");
            var big = new List<int[]>(); for (int i = 0; i < 13; i++) big.Add(Stop(i % 2 == 0 ? sag : cas, 100 + i));
            check(w.SetSchedule(si, Cat(big.ToArray())) == ScheduleResult.HorarioInvalido, "más de 12 paradas se rechaza");
            check(w.SetSchedule(si, Cat(Stop(cas, 100), Stop(val, 200))) == ScheduleResult.NoIncluyeParadaActual, "el horario tiene que incluir el nodo donde está el tren");
            check(w.Hash() == h0 && w.Services[si].Stops.Count == 1 && !w.Services[si].ScheduleEdited, "nada de lo rechazado cambia el estado ni el hash");

            // un tren en marcha no admite horario
            var moving = NetScenario.Mvp(false); moving.Services[0].AtStop = false;
            check(moving.SetSchedule(0, Cat(Stop(moving.Services[0].Stops[0].Node, 100), Stop(sag == moving.Services[0].Stops[0].Node ? cas : sag, 200))) == ScheduleResult.NoEstaParado, "un tren en marcha no admite horario (hay que esperar a que pare)");

            // horario válido: sale a su hora y carga
            int mat0 = w.Stock[sag, (int)Res.Materiales];
            long tod = w.Tick % NetConst.DayTicks, at = tod + 600;
            var data = Cat(Stop(sag, at, mat: 20, per: 8), Stop(cas, at + 5000, mat: -20));
            check(w.SetSchedule(si, data) == ScheduleResult.Ok && w.Services[si].Stops.Count == 2 && w.Services[si].StopIndex == 0 && w.Services[si].ScheduleEdited && w.Hash() != h0, "un horario válido se aplica y cambia el hash");
            var sv = w.Services[si];
            for (int i = 0; i < 599; i++) w.Step();
            check(sv.AtStop && sv.Cargo[(int)Res.Materiales] == 0, "no sale antes de su hora");
            for (int i = 0; i < 3; i++) w.Step();
            check(!sv.AtStop && sv.Stops[sv.StopIndex].Node == cas && sv.Cargo[(int)Res.Materiales] >= 1, "a su hora carga en Sagunto y sale hacia Castellón");

            // el horario puede empezar por otra parada: el tren se queda en la suya
            var w2 = WithParked(out int s2);
            check(w2.SetSchedule(s2, Cat(Stop(cas, 100), Stop(sag, 200), Stop(val, 300))) == ScheduleResult.Ok && w2.Services[s2].StopIndex == 1 && w2.Services[s2].Stops[1].Node == sag, "si Sagunto es la segunda parada, el tren queda en ella (StopIndex 1)");
            check(w2.SetSchedule(s2, Cat(Stop(sag, 100))) == ScheduleResult.Ok && w2.Services[s2].Stops.Count == 1, "un horario de una sola parada es válido (el tren se queda aparcado)");

            // sin horario puesto, los goldens no se mueven: el hash de un mundo sin SetSchedule no incluye nada
            var plain = NetScenario.Mvp(false); var plain2 = NetScenario.Mvp(false);
            check(plain.Hash() == plain2.Hash(), "sin SetSchedule el hash es el de siempre");

            // dos mundos con la misma orden: mismo hash; con otra distinta, otro
            var a = WithParked(out int ia); var b = WithParked(out int ib); var c = WithParked(out int ic);
            long t0 = a.Tick % NetConst.DayTicks + 600;
            a.SetSchedule(ia, Cat(Stop(sag, t0, mat: 20), Stop(cas, t0 + 5000))); b.SetSchedule(ib, Cat(Stop(sag, t0, mat: 20), Stop(cas, t0 + 5000))); c.SetSchedule(ic, Cat(Stop(sag, t0, mat: 21), Stop(cas, t0 + 5000)));
            for (int i = 0; i < 1000; i++) { a.Step(); b.Step(); c.Step(); }
            check(a.Hash() == b.Hash() && a.Hash() != c.Hash(), "mismo horario, mismo hash; una carga distinta (20 contra 21), otro");

            // grabación y repetición de la orden (el tren 0 del escenario cambia de horario)
            var scn = NetScenario.Mvp(false); int t0n = scn.Services[0].Stops[0].Node; int other = t0n == cas ? sag : cas;
            string gold = NetGolden.Record(new List<NetCommand>
            {
                new NetCommand { Tick = scn.Tick + 10, Type = NetCmdType.SetSchedule, Arg = 0, Payload = Cat(Stop(t0n, NetConst.Hours(6.5), mat: 10), Stop(other, NetConst.Hours(12))) }
            }, 6000);
            var res = NetGolden.Verify(gold);
            check(res.Ok, $"la orden SetSchedule se graba y se reproduce igual (con su Payload): {res}");
        }
    }
}
