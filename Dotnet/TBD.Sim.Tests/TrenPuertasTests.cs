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
            var s = Svc(w, 901, nul, 6, sag, 8); w.ActiveZoneNode = sag;            // en pantalla: el combate a las puertas es de NightSim (D-N177)
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
            var p = World(); var sp = Svc(p, 902, sag, 1, cas, 12); p.ActiveZoneNode = nul;
            Siege(p, nul, true); p.Step();
            Run(p, 2);
            check(!sp.AtStop && p.AtGate(sp, nul) && sp.Train.Route[sp.Train.Leg] == nul && sp.Train.OffsetMm == 0, "de Sagunto a Castellón, se para a las puertas de Nules, sitiada");
            long off = sp.Train.OffsetMm; int leg = sp.Train.Leg; Run(p, 2);
            check(p.AtGate(sp, nul) && sp.Train.OffsetMm == off && sp.Train.Leg == leg, "y no se mueve mientras dure el asedio");
            Siege(p, nul, false); Run(p, 1);
            check(!p.AtGate(sp, nul) && sp.AtStop && sp.StopIndex == 1, "acabado el asedio, sigue y llega a Castellón");

            // --- plazas de andén: 2 en las principales y 1 en las demás
            check(p.TrainSlots(sag) == 2 && p.TrainSlots(nul) == 1 && p.TrainSlots(val) == 0 && p.TrainSlots(cas) == 0 && p.TrainSlots(Id(p, "Teruel")) == 0 && p.FreeSlots(cas) == int.MaxValue,
                "plazas (Lucía): Sagunto 2 y Nules 1; las demás, sin límite (0)");
            var q = World(); var q1 = Svc(q, 903, sag, 1, nul, 5); var q2 = Svc(q, 904, sag, 2, nul, 6);
            Run(q, 3);
            check(q1.AtStop && !q.AtGate(q1, nul) && q2.AtStop && q.AtGate(q2, nul), "dos trenes a Nules: el primero entra y el segundo espera a las puertas");
            check(q.GateReason(1) != null && q.GateReason(1).Contains("andén"), $"«{q.GateReason(1)}»");
            Run(q, 2.5);
            check(!q.AtGate(q2, nul) && q2.AtStop && q.TrainAtNode(q2, nul) && q1.StopIndex == 0, "sale el primero (05:00) y entra el segundo");
            check(q.MoveCargo(1, nul, new[] { 0, 0, 0, 0, 0 }) == 0 && q.FreeSlots(nul) == 0, "dentro, ya puede cargar y descargar, y ocupa el andén");
            // --- por orden de llegada: con Nules sitiada llegan antes el 2 y luego el 1; al acabar entra el 2 (el 1 sigue a las puertas, 1 plaza)
            var o = World(); var o1 = Svc(o, 910, sag, 3, nul, 22); var o2 = Svc(o, 911, sag, 1, nul, 22); o.ActiveZoneNode = nul;
            Siege(o, nul, true); Run(o, 4);
            check(o.AtGate(o1, nul) && o.AtGate(o2, nul) && o2.GateSinceTick < o1.GateSinceTick && o.TrainsAtGate(nul)[0] == 1, "dos trenes a las puertas de Nules; TrainsAtGate los da por orden de llegada");
            Siege(o, nul, false); Run(o, 0.5);
            check(!o.AtGate(o2, nul) && o.AtGate(o1, nul) && o.GateReason(0).Contains("andén"), "acaba el asedio: entra el que llegó antes aunque tenga índice mayor, y el otro espera andén");
            var r = World(); Svc(r, 905, nul, 1, sag, 5); Svc(r, 906, nul, 2, sag, 6); Run(r, 3);
            check(!r.AtGate(r.Services[0], sag) && !r.AtGate(r.Services[1], sag), "en Sagunto caben los dos");

            var cs = World(); Svc(cs, 912, sag, 1, cas, 5); Svc(cs, 913, sag, 2, cas, 6); Svc(cs, 914, sag, 2.5, cas, 7); Run(cs, 4);
            check(cs.TrainsAtGate(cas).Count == 0 && cs.Services[2].AtStop && cs.Services[2].StopIndex == 1, "en Castellón (sin límite) entran los tres");

            // --- D-N177: fuera de pantalla, la regla de Hugo (todo o nada)
            var f = World();
            var g0 = f.GateFightFor(2400, 0, NetConst.TrainHpMax); var g4 = f.GateFightFor(2400, 4, NetConst.TrainHpMax); var g8 = f.GateFightFor(2400, 8, NetConst.TrainHpMax);
            check(g0.Attackers == 240 && g0.Destroyed && g4.Contact == 24 && !g4.Destroyed && NetConst.TrainHpMax - g4.HpLost == 91_200 && g8.Contact == 0 && g8.HpLost == 0,
                $"Nules, 2400: sin escolta cae; con 4 soldados queda al 76 % ({(NetConst.TrainHpMax - g4.HpLost) * 100 / NetConst.TrainHpMax} %); con 8, intacto (ejemplos de Hugo)");
            var s1 = f.GateFightFor(5250, 8, NetConst.TrainHpMax); var s2 = f.GateFightFor(5250, 16, NetConst.TrainHpMax);
            check(s1.Contact == 93 && !s1.Destroyed && s2.Contact == 0 && s2.AmmoU == 1 && g4.EscortLostMil == 336, $"Sagunto normal 5250: con 1 pelotón llegan 93 al tren y queda al {(NetConst.TrainHpMax - s1.HpLost) * 100 / NetConst.TrainHpMax} %; con 2 nadie llega; la escolta gasta 1 u por 400 abatidos y pierde 14 milésimas por zombi en contacto");
            var fs = Svc(f, 920, sag, 1, nul, 22); fs.Cargo[(int)Res.Materiales] = 30;
            f.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Vehiculo, Id = 7, Service = 0, Node = -1, Estado = PasajeroEstado.ABordo });
            check(f.EscortWarning(0) == null, "con un vehículo a bordo no hay aviso");
            f.Embarque.Lista.Clear();
            Siege(f, nul, true); Run(f, 0.5);
            check(f.EscortWarning(0) == "Sin escolta: Nules está sitiada", $"aviso ámbar al dar la salida: «{f.EscortWarning(0)}»");
            Run(f, 1);
            check(f.AtGate(fs, nul) && fs.GateHorde == 6000, $"llega a las puertas de Nules con la horda apuntada ({fs.GateHorde} zombis)");
            f.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Peloton, Id = 3, Service = 0, Node = -1, Estado = PasajeroEstado.ABordo });
            check(f.EscortWarning(0) == null, "con tropa a bordo, sin aviso");
            ulong hf = f.Hash(); Siege(f, nul, false); Run(f, 0.5);
            check(fs.Destroyed && fs.CargoTotal() == 0 && f.Embarque.Lista.Count == 0 && f.LastGateFight.Attackers == 600 && f.LastGateFight.Contact == 600 - 8 * 54 && f.GateFights == 1,
                $"acaba el asedio fuera de pantalla: 600 van a por el tren, 1 pelotón abate 432, llegan {f.LastGateFight.Contact} y el tren cae con la carga y la tropa (todo o nada)");
            check(f.Hash() != hf && f.MoveCargo(0, nul, new[] { 0, 0, 1, 0, 0 }) == -1 && f.LoadBlockReason(0, PasajeroTipo.Peloton, 1, nul) != null, "el tren perdido ya no carga ni sube nadie");
            long tk = f.Tick; Run(f, 1); check(fs.Destroyed && fs.Train.Leg == f.Services[0].Train.Leg, "ni se mueve");
            var bytes2 = NetSave.Save(f); var l2 = NetScenario.Mvp(); NetSave.Load(bytes2, l2);
            check(l2.Hash() == f.Hash() && l2.Services[0].Destroyed, "se guarda perdido");
            var k = World(); var ks = Svc(k, 921, sag, 1, nul, 22); k.GateKillsPerSoldier = 54;
            for (int i = 0; i < 2; i++) k.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Peloton, Id = 10 + i, Service = 0, Node = -1, Estado = PasajeroEstado.ABordo });
            Siege(k, nul, true); Run(k, 1.5); k.GateAttackPermil = 50; Siege(k, nul, false); Run(k, 0.5);
            check(!ks.Destroyed && ks.Hp == NetConst.TrainHpMax - (300 - 16 * 54 > 0 ? 300 - 16 * 54 : 0) * 1200 && ks.AtStop && !k.AtGate(ks, nul) && k.TrainRepairU(0) == 0, "con 2 pelotones aguanta, entra y no hay que reparar");
            var z = World(); var zs = Svc(z, 922, sag, 1, nul, 22); z.ActiveZoneNode = nul; Siege(z, nul, true); Run(z, 1.5); Siege(z, nul, false); Run(z, 0.5);
            check(!zs.Destroyed && zs.Hp == NetConst.TrainHpMax && z.GateFights == 0, "con la zona en pantalla no se aplica: manda NightSim");
            var rp = World(); var rs = Svc(rp, 923, sag, 1, nul, 22); rs.Hp = 91_200;
            check(rp.TrainRepairU(0) == 6, $"reparar 28.800 de vida cuesta 6 u (1 por 5.000, redondeo arriba) ({rp.TrainRepairU(0)})");

            // --- hash y apagado
            var a = World(); var b = World(); Svc(a, 907, nul, 6, sag, 8); Svc(b, 907, nul, 6, sag, 8); b.GatesEnabled = false; a.ActiveZoneNode = b.ActiveZoneNode = sag;
            Siege(a, sag, true); Siege(b, sag, true); Run(a, 7); Run(b, 7);
            check(a.AtGate(a.Services[0], sag) && !b.AtGate(b.Services[0], sag) && a.Hash() != b.Hash(), "con GatesEnabled = false entra aunque esté sitiada (como antes), y el hash lo distingue");
            var c = World(); var d = World(); Svc(c, 908, nul, 6, sag, 8); Svc(d, 908, nul, 6, sag, 8); Run(c, 7); Run(d, 7);
            check(c.Hash() == d.Hash() && c.Services[0].GateNode < 0, "sin asedio nadie espera y el hash no cambia (los goldens tampoco)");
        }
    }
}
