using System;
using System.Collections.Generic;
using TBD.Net;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>[31] Embarque de tropa y vehículos en los trenes (D-N166, Nacho): estado de red (Embarque.cs) y tiempos de zona (EmbarqueZona.cs).</summary>
    public static class EmbarqueTests
    {
        static int Node(NetWorld w, string name) { for (int i = 0; i < w.Graph.Nodes.Count; i++) if (w.Graph.Nodes[i].Name == name) return i; return -1; }
        static int TrainAt(NetWorld w, int node) { for (int i = 0; i < w.Services.Count; i++) if (!w.Services[i].Ambushed && w.TrainAtNode(w.Services[i], node)) return i; return -1; }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[31] Embarque en los trenes (D-N166)");
            var w = NetScenario.Mvp(); int sag = Node(w, "Sagunto"), nul = Node(w, "Nules"), val = Node(w, "Valencia");
            check(w.Embarque.HomeNode == sag && sag >= 0, "el escenario pone Sagunto como casa de la tropa y los vehículos");
            ulong h0 = w.Hash(); var sw0 = new SaveWriter(); w.SaveState(sw0);
            check(w.Embarque.Hash() == 0, "sin pasajeros el embarque no aporta nada al hash (no mueve los goldens)");
            int sv = TrainAt(w, sag); for (int t = 0; sv < 0 && t < 20000; t++) { w.Step(); sv = TrainAt(w, sag); }
            check(sv >= 0, $"hay un tren parado en Sagunto (tren {sv}, tick {w.Tick})");
            if (sv < 0) return;
            // reglas de subida
            check(w.LoadBlockReason(sv, PasajeroTipo.Peloton, 7, val) != null, $"no sube si el tren no está en esa estación: «{w.LoadBlockReason(sv, PasajeroTipo.Peloton, 7, val)}»");
            check(w.LoadBlockReason(sv, PasajeroTipo.Peloton, 7, sag) == null, "un pelotón de casa sube en Sagunto con el tren parado");
            w.Push(NetWorld.CmdLoad(w.Tick, sv, PasajeroTipo.Peloton, 7, sag)); w.Push(NetWorld.CmdLoad(w.Tick, sv, PasajeroTipo.Vehiculo, 2, sag));
            var held = w.Services[sv]; held.Held = true; w.Step();
            check(w.Embarque.CountAboard(sv, PasajeroTipo.Peloton) == 1 && w.Embarque.CountAboard(sv, PasajeroTipo.Vehiculo) == 1, "las órdenes TrainLoad (con tick) suben el pelotón y el vehículo");
            check(w.LoadBlockReason(sv, PasajeroTipo.Peloton, 7, sag) == "Ya va a bordo", "el mismo pelotón no sube dos veces");
            check(w.LoadBlockReason(sv, PasajeroTipo.Peloton, 8, sag) == "No queda vagón de tropa libre" && w.LoadBlockReason(sv, PasajeroTipo.Vehiculo, 3, sag) == "No queda plataforma libre",
                "con un vagón de tropa y una plataforma por tren (PROTOTIPO), el segundo no sube");
            check(w.Hash() != h0, "los pasajeros entran en el hash");
            // guardado
            var bytes = NetSave.Save(w); var wl = NetScenario.Mvp(); wl.Services.Clear(); NetSave.Load(bytes, wl);
            check(wl.Hash() == w.Hash() && wl.Embarque.Lista.Count == 2 && wl.Embarque.Lista[0].Id == 7, "los pasajeros viajan en el guardado (mismo hash)");
            // bajar en casa: salen de la lista
            int dn = w.UnloadPassengers(sv, sag);
            check(dn == 2 && w.Embarque.Lista.Count == 0, "bajar en Sagunto los devuelve a casa (salen de la lista)");
            check(w.UnloadPassengers(sv, val) == -1, "no se baja en una estación donde el tren no está");
            // bajar en otra estación (sin asedio): EnEstacion; y desde allí solo vuelve a subir allí
            w.LoadPassenger(sv, PasajeroTipo.Peloton, 7, sag); w.LoadPassenger(sv, PasajeroTipo.Vehiculo, 2, sag);
            var nd = w.Graph.Nodes[nul]; var st0 = nd.State; int dens0 = nd.HordeDensity;
            // el tren «está» en Nules: lo ponemos en una parada de Nules a mano (prueba de la regla, no del viaje)
            var svc = w.Services[sv]; var stops = new List<ScheduleStop>(svc.Stops); svc.Stops.Clear(); svc.Stops.Add(new ScheduleStop { Node = nul, DepartAt = 0, Transfer = new int[NetConst.ResCount] }); int si0 = svc.StopIndex; bool at0 = svc.AtStop; svc.StopIndex = 0; svc.AtStop = true;
            nd.State = NodeState.Asediada;
            check(w.Besieged(nul), "Nules sitiada (estado Asediada)");
            int b1 = w.UnloadPassengers(sv, nul);
            check(b1 == 1 && w.Embarque.AtStation(nul, PasajeroTipo.Peloton).Count == 1 && w.Embarque.CountAboard(sv, PasajeroTipo.Vehiculo) == 1,
                "D-N166: en una estación sitiada solo baja la tropa; el vehículo se queda a bordo");
            nd.State = st0; nd.HordeDensity = dens0;
            check(!w.Besieged(nul) && w.UnloadPassengers(sv, nul) == 1 && w.Embarque.AtStation(nul, PasajeroTipo.Vehiculo).Count == 1, "acabado el asedio, baja el vehículo");
            check(w.LoadBlockReason(sv, PasajeroTipo.Peloton, 7, nul) == null, "el pelotón que se quedó en Nules vuelve a subir en Nules");
            check(w.LoadBlockReason(sv, PasajeroTipo.Peloton, 9, nul) == "Está en Sagunto", "un pelotón de casa no sube en Nules");
            svc.Stops.Clear(); svc.Stops.AddRange(stops); svc.StopIndex = si0; svc.AtStop = at0;

            // zona: tiempos y regla de noche
            var uw = new UnitsWorld(3); var p = uw.AddPlatoon(PlatoonKind.Fusileros, 100_000, 60_000, 0); var ez = new EmbarqueZona(); int carrier = 2000 + sv;
            check(ez.SubirPeloton(uw, p.Id, carrier) && p.HoldFire && !ez.SubirPeloton(uw, p.Id, carrier), "empezar a subir para al pelotón y deja de disparar; no se encola dos veces");
            int ticks = 0; List<EmbarqueZona.Trabajo> done = null;
            while (ticks < 2000) { ticks++; done = ez.Step(uw); if (done.Count > 0) break; }
            check(ticks == 10 * 30 && p.Aboard == carrier && !p.HoldFire, $"D-N166: subir un pelotón tarda 10 s ({ticks} ticks) y luego va a bordo y recupera el fuego");
            check(ez.BajarPeloton(uw, p.Id, 120_000, 64_000), "empezar a bajar"); for (int t = 0; t < 299; t++) ez.Step(uw);
            check(p.Aboard == carrier, "a los 299 ticks aún no ha bajado");
            ez.Step(uw); check(p.Aboard == 0 && p.AX == 120_000, "a los 10 s baja donde se le dice");
            check(ez.Vehiculo(1, carrier, false), "empezar a subir un vehículo"); int vt = 0; while (vt < 2000) { vt++; if (ez.Step(uw).Count > 0) break; }
            check(vt == 20 * 30, $"subir un vehículo tarda 20 s ({vt} ticks)");
            check(ez.SubirPeloton(uw, p.Id, carrier) && ez.Cancelar(uw, false, p.Id) && !p.HoldFire && p.Aboard == 0, "cancelar devuelve el fuego y no sube");
            check(EmbarqueZona.NocheBlockReason(true, false, false, false) != null && EmbarqueZona.NocheBlockReason(true, true, true, false) != null && EmbarqueZona.NocheBlockReason(true, true, false, false) == null
                  && EmbarqueZona.NocheBlockReason(true, false, true, true) == null && EmbarqueZona.NocheBlockReason(false, false, true, false) == null,
                  "D-N166: de noche, solo dentro del recinto y sin zombis a 3 m, salvo la tropa que baja en una estación sitiada; de día, sin esa regla");
            var zs = new List<Target> { new Target { Id = 1, X = 102_900, Y = 60_000 } };
            check(EmbarqueZona.ZombisCerca(zs, 100_000, 60_000) && !EmbarqueZona.ZombisCerca(zs, 99_000, 60_000), "zombis a 3 m: 2,9 m sí, 3,9 m no");
        }
    }
}
