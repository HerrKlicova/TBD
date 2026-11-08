using System;
using System.Collections.Generic;

namespace TBD.Net
{
    /// <summary>
    /// Dónde para el tren y dónde baja cada cosa en una estación (bloque «tren_capitulo3» del JSON de Álvaro; Nules v0.1, nules-nivel.md §4): el tren entra
    /// por el apartado nuevo del muelle y para con el morro en Morro; tropa y carga bajan en Tropa, el vehículo en Vehiculo y, con la estación sitiada,
    /// la tropa baja fuera, en Sitiada. Milímetros de la zona (X a lo largo de la vía, Z hacia el pueblo). Sirve también para los refuerzos de la D-N172.
    /// </summary>
    public sealed class AndenPuntos
    {
        public string Via; public long MorroX, MorroZ, TropaX, TropaZ, VehiculoX, VehiculoZ, SitiadaX, SitiadaZ;

        public static AndenPuntos FromJson(string json)
        {
            var root = TBD.Units.Mini.Parse(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("tren_capitulo3", out var o) || !(o is Dictionary<string, object> t)) return null;
            long[] P(string k) { var l = t[k] as List<object>; return new[] { Convert.ToInt64(l[0], System.Globalization.CultureInfo.InvariantCulture), Convert.ToInt64(l[1], System.Globalization.CultureInfo.InvariantCulture) }; }
            var m = P("morro_mm"); var tr = P("punto_bajada_tropa_mm"); var ve = P("punto_bajada_vehiculo_mm"); var si = P("punto_bajada_sitiada_mm");
            return new AndenPuntos { Via = t.TryGetValue("via", out var v) ? v as string : null, MorroX = m[0], MorroZ = m[1], TropaX = tr[0], TropaZ = tr[1], VehiculoX = ve[0], VehiculoZ = ve[1], SitiadaX = si[0], SitiadaZ = si[1] };
        }
    }

    /// <summary>
    /// Capítulo 3 del tutorial (D-N172, idea de Eugenio): Nules tiene su propio tren parado en el andén y un destacamento de refuerzo (1 pelotón de
    /// fusileros; sin Lince desde la revisión) que el jugador manda a Sagunto antes de la primera noche. Solo existe con el tutorial activo (fácil y normal, la primera vez).
    /// C# puro, sobre la red. El tren se crea igual que los del taller (una sola parada: no se mueve hasta que le den horario). El destacamento son dos
    /// pasajeros EnEstacion(Nules) del embarque de Nacho (D-N166); sus ids son los de la zona: Platoon.Id en UnitsWorld y el índice en NightRun.Vehs,
    /// que crea la zona (Marcos) y registra con Registrar. Se aplica igual a los dos mundos (A y B) en el mismo tick: no rompe la comprobación.
    /// </summary>
    public static class Destacamento
    {
        public const string Estacion = "Nules";
        public static string NombreTren(int id) { return id + " Nules"; }

        /// <summary>Crea el tren parado en Nules (si no lo hay ya). Devuelve el índice del servicio, o −1 si no hay Nules.</summary>
        public static int SembrarTren(NetWorld w)
        {
            int nul = TrenPreparar.Nodo(w, Estacion); if (nul < 0) return -1;
            for (int i = 0; i < w.Services.Count; i++) if (w.Services[i].Train.Name.EndsWith(" Nules")) return i;
            int id = 1; foreach (var sv in w.Services) id = Math.Max(id, sv.Train.Id + 1);
            var s = new TrainService { Train = new RailTrain { Id = id, Name = NombreTren(id), SpeedMmPerTick = NetConst.TrainMmPerTick }, StopIndex = 0, AtStop = true };
            s.Stops.Add(new ScheduleStop { Node = nul, DepartAt = 0, Transfer = new int[NetConst.ResCount] });
            w.Services.Add(s);
            return w.Services.Count - 1;
        }

        /// <summary>Apunta en la red el pelotón y el vehículo de la zona como destacamento en Nules (EnEstacion). Si ya estaban, no hace nada.</summary>
        public static void Registrar(NetWorld w, int platoonId, int vehIndex)
        {
            int nul = TrenPreparar.Nodo(w, Estacion); if (nul < 0) return;
            if (platoonId >= 0 && w.Embarque.Find(PasajeroTipo.Peloton, platoonId) < 0)
                w.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Peloton, Id = platoonId, Service = -1, Node = nul, Estado = PasajeroEstado.EnEstacion });
            if (vehIndex >= 0 && w.Embarque.Find(PasajeroTipo.Vehiculo, vehIndex) < 0)
                w.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Vehiculo, Id = vehIndex, Service = -1, Node = nul, Estado = PasajeroEstado.EnEstacion });
        }
    }
}
