using System;
using System.Collections.Generic;

namespace TBD.Net
{
    public enum PasajeroTipo : byte { Peloton = 0, Vehiculo = 1 }
    public enum PasajeroEstado : byte { ABordo = 1, EnEstacion = 2 }

    /// <summary>Un pelotón o un vehículo que no está en casa (la zona de Sagunto): a bordo de un tren o bajado en otra estación.</summary>
    public struct Pasajero
    {
        public PasajeroTipo Tipo;
        public int Id;               // pelotón: Platoon.Id de UnitsWorld; vehículo: índice en la lista de vehículos de la zona
        public int Service;          // tren (índice en NetWorld.Services) en el que va o en el que llegó
        public int Node;             // EnEstacion: la estación donde está; ABordo: −1
        public PasajeroEstado Estado;
    }

    /// <summary>
    /// Embarque de tropa y vehículos en los trenes (D-N166, Lucía, 8-nov). Es el estado de la RED: quién va en qué tren y quién se quedó en qué estación.
    /// Las reglas:
    /// - Se sube con el tren parado en la estación (TrainAtNode) y sin emboscada. Lo que no está en la lista está en casa (HomeNode, Sagunto), y solo sube allí;
    ///   lo que se bajó en otra estación vuelve a subir en esa estación.
    /// - Cabe PelotonesPorTren pelotones (vagones de tropa) y VehiculosPorTren vehículos de tierra (plataformas) por tren. PROTOTIPO: sin medir.
    /// - Al bajar en una estación sitiada (WorkPaused: asediada, perdida o con la horda encima) solo baja la tropa; los vehículos se quedan a bordo hasta que acabe el asedio.
    ///   Bajar en casa saca de la lista. Bajar en otra estación los deja EnEstacion(nodo).
    /// - Los tiempos (10 s por pelotón, 20 s por vehículo, sin disparar) y la regla de noche (dentro del recinto y sin zombis a 3 m) son de la ZONA: la vista los cuenta
    ///   con EmbarqueZona (Units) y manda la orden de red al terminar. La red no sabe de segundos reales.
    /// Todo va en el hash («trenes»; 0 si la lista está vacía) y en el guardado (NetVersion 3).
    /// </summary>
    public sealed class EmbarqueState
    {
        public const int PelotonesPorTren = 1, VehiculosPorTren = 1;     // PROTOTIPO, sin medir (Hugo)
        public const int SubirPelotonS = 10, SubirVehiculoS = 20;          // D-N166: segundos reales para subir o bajar; los cuenta la zona
        public int HomeNode = -1;                                          // configuración (la pone el escenario): no se guarda
        public readonly List<Pasajero> Lista = new List<Pasajero>();

        public int Find(PasajeroTipo t, int id) { for (int i = 0; i < Lista.Count; i++) if (Lista[i].Tipo == t && Lista[i].Id == id) return i; return -1; }
        public int CountAboard(int service, PasajeroTipo t) { int n = 0; foreach (var p in Lista) if (p.Service == service && p.Tipo == t && p.Estado == PasajeroEstado.ABordo) n++; return n; }
        public List<int> Aboard(int service, PasajeroTipo t) { var l = new List<int>(); foreach (var p in Lista) if (p.Service == service && p.Tipo == t && p.Estado == PasajeroEstado.ABordo) l.Add(p.Id); return l; }
        public List<int> AtStation(int node, PasajeroTipo t) { var l = new List<int>(); foreach (var p in Lista) if (p.Node == node && p.Tipo == t && p.Estado == PasajeroEstado.EnEstacion) l.Add(p.Id); return l; }

        public ulong Hash()
        {
            ulong h = 0;
            for (int i = 0; i < Lista.Count; i++)
            {
                var p = Lista[i];
                h += NetWorld.Mix(0xE3BA0UL ^ ((ulong)p.Tipo << 60) ^ ((ulong)(uint)p.Id << 28) ^ ((ulong)(uint)(p.Service + 1) << 12) ^ ((ulong)(uint)(p.Node + 1) << 2) ^ (ulong)p.Estado);
            }
            return h;
        }

        public void SaveState(SaveWriter w)
        {
            w.Int(Lista.Count);
            foreach (var p in Lista) { w.Byte((byte)p.Tipo); w.Int(p.Id); w.Int(p.Service); w.Int(p.Node); w.Byte((byte)p.Estado); }
        }

        public void LoadState(SaveReader r)
        {
            int n = r.Int();
            if (n < 0 || n > r.Remaining) throw new SaveException(SaveError.Corrupt, "pasajeros no válidos");
            Lista.Clear();
            for (int i = 0; i < n; i++) Lista.Add(new Pasajero { Tipo = (PasajeroTipo)r.Byte(), Id = r.Int(), Service = r.Int(), Node = r.Int(), Estado = (PasajeroEstado)r.Byte() });
        }

        /// <summary>Lo que escribe una partida sin pasajeros (migración 2 → 3).</summary>
        public static void WriteEmpty(SaveWriter w) { w.Int(0); }
    }

    public sealed partial class NetWorld
    {
        public readonly EmbarqueState Embarque = new EmbarqueState();

        /// <summary>¿Está sitiada la estación? (D-N166: entonces solo baja la tropa). Misma regla que pausa las obras.</summary>
        public bool Besieged(int node) { return node >= 0 && node < Graph.Nodes.Count && (WorkPaused(node) || node == CercoNodo); }      // D-N188: el cerco también sitia (el socorro espera a las puertas y solo baja la tropa)
        /// <summary>Zombis para el combate a las puertas (D-N177). El cerco (D-N188) no se resuelve con la fórmula: se combate en la zona, que informa con CercoBajas.</summary>
        int GateHordeFor(int node) { return node == CercoNodo ? 0 : SiegeZombies(node); }

        /// <summary>Por qué no puede subir (texto para el panel), o null si puede. «node» es la estación donde está el tren.</summary>
        public string LoadBlockReason(int service, PasajeroTipo t, int id, int node)
        {
            if (service < 0 || service >= Services.Count) return "No hay tren";
            if (node < 0 || node >= Graph.Nodes.Count) return "No hay estación";
            var sv = Services[service];
            if (sv.Ambushed) return "El tren está emboscado";
            if (sv.Destroyed) return "El tren se ha perdido";
            if (!TrainAtNode(sv, node)) return "El tren no está en la estación";
            if (sv.GateNode == node && t == PasajeroTipo.Vehiculo) return "El tren espera a las puertas: los vehículos suben en el andén";   // D-N152
            int i = Embarque.Find(t, id);
            if (i >= 0)
            {
                var p = Embarque.Lista[i];
                if (p.Estado == PasajeroEstado.ABordo) return "Ya va a bordo";
                if (p.Node != node) return "Está en otra estación";
            }
            else if (Embarque.HomeNode >= 0 && node != Embarque.HomeNode) return "Está en " + Graph.Nodes[Embarque.HomeNode].Name;
            int cap = t == PasajeroTipo.Peloton ? EmbarqueState.PelotonesPorTren : EmbarqueState.VehiculosPorTren;
            if (Embarque.CountAboard(service, t) >= cap) return t == PasajeroTipo.Peloton ? "No queda vagón de tropa libre" : "No queda plataforma libre";
            return null;
        }

        /// <summary>Orden TrainLoad (aplicada en su tick): sube el pelotón o el vehículo. false si no puede (LoadBlockReason).</summary>
        public bool LoadPassenger(int service, PasajeroTipo t, int id, int node)
        {
            if (LoadBlockReason(service, t, id, node) != null) return false;
            int i = Embarque.Find(t, id); var p = new Pasajero { Tipo = t, Id = id, Service = service, Node = -1, Estado = PasajeroEstado.ABordo };
            if (i >= 0) Embarque.Lista[i] = p; else Embarque.Lista.Add(p);
            return true;
        }

        /// <summary>Orden TrainUnload: baja todo lo que va a bordo del tren en la estación donde está. Con la estación sitiada solo baja la tropa (D-N166).
        /// En casa salen de la lista; en otra estación quedan EnEstacion. Devuelve cuántos han bajado (−1 si el tren no está en esa estación).</summary>
        public int UnloadPassengers(int service, int node)
        {
            if (service < 0 || service >= Services.Count || node < 0 || node >= Graph.Nodes.Count || !TrainAtNode(Services[service], node)) return -1;
            bool siege = Besieged(node); int n = 0;
            for (int i = Embarque.Lista.Count - 1; i >= 0; i--)
            {
                var p = Embarque.Lista[i];
                if (p.Service != service || p.Estado != PasajeroEstado.ABordo) continue;
                if ((siege || Services[service].GateNode == node) && p.Tipo == PasajeroTipo.Vehiculo) continue;   // a las puertas solo baja la tropa (D-N152, D-N166)
                n++;
                if (node == Embarque.HomeNode) { Embarque.Lista.RemoveAt(i); continue; }
                p.Estado = PasajeroEstado.EnEstacion; p.Node = node; Embarque.Lista[i] = p;
            }
            return n;
        }

        /// <summary>Órdenes para el panel (TrenPanel, Iker): Payload = [id, nodo].</summary>
        public static NetCommand CmdLoad(long tick, int service, PasajeroTipo t, int id, int node) { return new NetCommand { Tick = tick, Type = NetCmdType.TrainLoad, Arg = service, Arg2 = (int)t, Payload = new[] { id, node } }; }
        public static NetCommand CmdUnload(long tick, int service, int node) { return new NetCommand { Tick = tick, Type = NetCmdType.TrainUnload, Arg = service, Arg2 = node }; }
    }
}
