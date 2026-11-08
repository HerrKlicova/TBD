using System;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>[36] D-N152: el tren espera a las puertas de una estación sitiada, y entra solo si queda andén (plazas). Parte de la red; la zona (tropa que baja fuera) es de Iker y Marcos.</summary>
    public static class TrenPuertasTests
    {
        static int Id(NetWorld w, string n) { return w.Graph.Nodes.FindIndex(x => x.Name == n); }

        static void Siege(NetWorld w, int node, bool on)
        {
            var nd = w.Graph.Nodes[node];
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int x = nd.XMeters + dx, y = nd.YMeters + dy; w.Field.S[y * HordeField.W + x] = 0; w.Field.D[y * HordeField.W + x] = 0; }
            if (on) w.Field.S[nd.YMeters * HordeField.W + nd.XMeters] = 600;              // asedio (≥ 500) sin cortar la vía (< 850)
        }

        static TrainService Svc(NetWorld w, int id, int a, double ha, int b, double hb)
        {
            var s = new TrainService { Train = new RailTrain { Id = id, Name = "Prueba " + id, SpeedMmPerTick = NetConst.TrainMmPerTick }, StopIndex = 0, AtStop = true };
            s.Stops.Add(new ScheduleStop { Node = a, DepartAt = NetConst.Hours(ha), Transfer = new int[NetConst.ResCount] });
            s.Stops.Add(new ScheduleStop { Node = b, DepartAt = NetConst.Hours(hb), Transfer = new int[NetConst.ResCount] });
            w.Services.Add(s); return s;
        }

        static NetWorld World()
        {
            var w = NetScenario.Mvp(); w.DirectorEnabled = false; w.Services.Clear();
            for (int n = 0; n < w.Graph.Nodes.Count; n++) Siege(w, n, false);
            w.Tick = (w.Tick / NetConst.DayTicks + 1) * NetConst.DayTicks;          // 00:00 de un día nuevo
            return w;
        }

        static void Run(NetWorld w, double hours) { long end = w.Tick + NetConst.Hours(hours); while (w.Tick < end) w.Step(); }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[36] D-N152: el tren espera a las puertas de una estación sitiada y entra si hay andén");

            // --- llega a su parada sitiada
            var w = World(); int sag = Id(w, "Sagunto"), nul = Id(w, "Nules"), cas = Id(w, "Castellón"), val = Id(w, "Valencia");
            var s = Svc(w, 901, nul, 6, sag, 8);
            Siege(w, sag, true); w.Step();
            check(w.Besieged(sag) && !w.Besieged(nul), "con la horda encima, Sagunto queda sitiada y Nules no");
            Run(w, 7);
            check(s.AtStop && w.AtGate(s, sag) && w.TrainAtNode(s, sag), "el tren de Nules llega a Sagunto y se queda a las puertas");
            check(w.GateReason(0) != null && w.GateReason(0).Contains("sitiada"), $"el panel dice por qué: «{w.GateReason(0)}»");
            check(w.MoveCargo(0, sag, new[] { 0, 0, -1, 0, 0 }) == -1, "a las puertas no carga ni descarga mercancía (no hay andén)");
            check(w.LoadBlockReason(0, PasajeroTipo.Vehiculo, 1, sag) != null && w.LoadBlockReason(0, PasajeroTipo.Vehiculo, 1, sag).Contains("puertas"), "los vehículos no suben a las puertas");
            Run(w, 3);
            check(w.AtGate(s, sag) && s.AtStop && s.StopIndex == 1, "pasada su hora de salida (08:00) sigue esperando: con el asedio, ni sale");
            var bytes = NetSave.Save(w); var l = NetScenario.Mvp(); NetSave.Load(bytes, l);
            check(l.Hash() == w.Hash() && l.Services[0].GateNode == sag, "se guarda y se carga esperando a las puertas");
            Siege(w, sag, false); Run(w, 0.5);
            check(!w.Besieged(sag) && !w.AtGate(s, sag) && s.StopIndex == 0 && s.DepartedDay[1] == w.Tick / NetConst.DayTicks, "acaba el asedio: entra, y como ya pasó su hora (08:00), carga y sale enseguida");

            // --- pasa por una estación sitiada
            var p = World(); var sp = Svc(p, 902, sag, 1, cas, 12);
            Siege(p, nul, true); p.Step();
            Run(p, 2);
            check(!sp.AtStop && p.AtGate(sp, nul) && sp.Train.Route[sp.Train.Leg] == nul && sp.Train.OffsetMm == 0, "de Sagunto a Castellón, se para a las puertas de Nules, sitiada");
            long off = sp.Train.OffsetMm; int leg = sp.Train.Leg; Run(p, 2);
            check(p.AtGate(sp, nul) && sp.Train.OffsetMm == off && sp.Train.Leg == leg, "y no se mueve mientras dure el asedio");
            Siege(p, nul, false); Run(p, 1);
            check(!p.AtGate(sp, nul) && sp.AtStop && sp.StopIndex == 1, "acabado el asedio, sigue y llega a Castellón");

            // --- plazas de andén: 2 en las principales y 1 en las demás
            check(p.TrainSlots(sag) == 2 && p.TrainSlots(val) == 2 && p.TrainSlots(cas) == 2 && p.TrainSlots(nul) == 1 && p.TrainSlots(Id(p, "Almenara")) == 1, "plazas: Sagunto, Valencia y Castellón 2; Nules y Almenara 1");
            var q = World(); var q1 = Svc(q, 903, sag, 1, nul, 5); var q2 = Svc(q, 904, sag, 2, nul, 6);
            Run(q, 3);
            check(q1.AtStop && !q.AtGate(q1, nul) && q2.AtStop && q.AtGate(q2, nul), "dos trenes a Nules: el primero entra y el segundo espera a las puertas");
            check(q.GateReason(1) != null && q.GateReason(1).Contains("andén"), $"«{q.GateReason(1)}»");
            Run(q, 2.5);
            check(!q.AtGate(q2, nul) && q2.AtStop && q.TrainAtNode(q2, nul) && q1.StopIndex == 0, "sale el primero (05:00) y entra el segundo");
            check(q.MoveCargo(1, nul, new[] { 0, 0, 0, 0, 0 }) == 0 && q.FreeSlots(nul) == 0, "dentro, ya puede cargar y descargar, y ocupa el andén");
            // --- por orden de llegada: con Nules sitiada llegan antes el 2 y luego el 1; al acabar entra el 2 (el 1 sigue a las puertas, 1 plaza)
            var o = World(); var o1 = Svc(o, 910, sag, 3, nul, 22); var o2 = Svc(o, 911, sag, 1, nul, 22);
            Siege(o, nul, true); Run(o, 4);
            check(o.AtGate(o1, nul) && o.AtGate(o2, nul) && o2.GateSinceTick < o1.GateSinceTick && o.TrainsAtGate(nul)[0] == 1, "dos trenes a las puertas de Nules; TrainsAtGate los da por orden de llegada");
            Siege(o, nul, false); Run(o, 0.5);
            check(!o.AtGate(o2, nul) && o.AtGate(o1, nul) && o.GateReason(0).Contains("andén"), "acaba el asedio: entra el que llegó antes aunque tenga índice mayor, y el otro espera andén");
            var r = World(); Svc(r, 905, nul, 1, sag, 5); Svc(r, 906, nul, 2, sag, 6); Run(r, 3);
            check(!r.AtGate(r.Services[0], sag) && !r.AtGate(r.Services[1], sag), "en Sagunto caben los dos");

            // --- hash y apagado
            var a = World(); var b = World(); Svc(a, 907, nul, 6, sag, 8); Svc(b, 907, nul, 6, sag, 8); b.GatesEnabled = false;
            Siege(a, sag, true); Siege(b, sag, true); Run(a, 7); Run(b, 7);
            check(a.AtGate(a.Services[0], sag) && !b.AtGate(b.Services[0], sag) && a.Hash() != b.Hash(), "con GatesEnabled = false entra aunque esté sitiada (como antes), y el hash lo distingue");
            var c = World(); var d = World(); Svc(c, 908, nul, 6, sag, 8); Svc(d, 908, nul, 6, sag, 8); Run(c, 7); Run(d, 7);
            check(c.Hash() == d.Hash() && c.Services[0].GateNode < 0, "sin asedio nadie espera y el hash no cambia (los goldens tampoco)");
        }
    }
}
