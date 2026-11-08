using System;
using System.Collections.Generic;

namespace TBD.Net
{
    /// <summary>
    /// «Preparar tren» (Bloque 2 del 8-nov; capítulo 3 del tutorial, D-N154): mandar un tren parado en una estación a otra con la carga elegida,
    /// y descargarlo al llegar. Solo usa lo que la red ya tiene: la orden SetSchedule (horario de dos paradas con sus cargas). Nada nuevo en NetWorld,
    /// así que no cambia el hash ni el G3.
    ///   Enviar:    [origen, ahora + 1 tick, +carga]  →  [destino, 23:59, 0]. Sale enseguida y en el destino espera (no se mueve hasta las 23:59).
    ///   Descargar: [destino, ahora + 1 tick, −todo lo que lleva]  →  [origen, 23:59, 0]. Descarga en el andén al salir y vuelve.
    ///   Aparcar:   [aquí] (una sola parada). La vista lo manda al llegar al destino y al volver: SetSchedule es cíclico y, si no, el tren
    ///              repetiría el viaje (y la carga) cada día.
    /// Tropa y vehículos a bordo: PROTOTIPO a la espera de Nacho. Un pelotón a bordo usa el campo que ya existe, Platoon.Aboard (no se mueve, no
    /// dispara, no lo tocan), con el id ATren(tren); al bajarlo en una estación que no es Sagunto pasa a EnEstacion(nodo). Los vehículos solo van
    /// en el manifiesto de la vista (NightVehicle no tiene «a bordo»).
    /// </summary>
    public static class TrenPreparar
    {
        public const int AbordoBase = 2000, EstacionBase = 3000;
        public static int ATren(int trainId) { return AbordoBase + trainId; }
        public static int EnEstacion(int node) { return EstacionBase + node; }

        public static int Nodo(NetWorld w, string nombre) { return w.Graph.Nodes.FindIndex(n => n.Name == nombre); }

        /// <summary>Servicio parado en ese nodo y libre (no emboscado). −1 si no hay ninguno.</summary>
        public static int TrenEn(NetWorld w, int node)
        {
            for (int i = 0; i < w.Services.Count; i++)
            {
                var s = w.Services[i];
                if (s.AtStop && !s.Ambushed && s.Stops.Count > 0 && s.StopIndex < s.Stops.Count && s.Stops[s.StopIndex].Node == node) return i;
            }
            return -1;
        }

        static long Tod(NetWorld w) { return w.Tick % NetConst.DayTicks; }
        static int Ahora(NetWorld w) { return (int)Math.Min(NetConst.DayTicks - 2, Tod(w) + 1); }
        const int Espera = NetConst.DayTicks - 1;

        /// <summary>Lo que cabe: la capacidad del tren menos lo que ya lleva.</summary>
        public static int Hueco(NetWorld w, int service) { return NetConst.TrainCapacity - w.Services[service].CargoTotal(); }

        /// <summary>Payload de SetSchedule para enviar el tren desde donde está hasta el destino con esa carga (por recurso, ≥ 0). null si no vale.</summary>
        public static int[] Envio(NetWorld w, int service, int destino, int[] carga)
        {
            if (service < 0 || service >= w.Services.Count || destino < 0 || destino >= w.Graph.Nodes.Count) return null;
            var s = w.Services[service]; if (!s.AtStop || s.Ambushed) return null;
            int origen = s.Stops[s.StopIndex].Node; if (origen == destino) return null;
            if (w.Graph.ShortestPath(origen, destino).Count < 2) return null;
            int k = 2 + NetConst.ResCount; var p = new int[2 * k];
            p[0] = origen; p[1] = Ahora(w);
            for (int r = 0; r < NetConst.ResCount; r++) p[2 + r] = Math.Max(0, Math.Min(NetWorld.MaxScheduleTransfer, carga != null && r < carga.Length ? carga[r] : 0));
            p[k] = destino; p[k + 1] = Espera;
            return p;
        }

        /// <summary>Payload para descargarlo todo en la estación donde está parado y volver a «vuelta». null si no vale.</summary>
        public static int[] Descarga(NetWorld w, int service, int vuelta)
        {
            if (service < 0 || service >= w.Services.Count) return null;
            var s = w.Services[service]; if (!s.AtStop || s.Ambushed) return null;
            int aqui = s.Stops[s.StopIndex].Node; if (aqui == vuelta) return null;
            int k = 2 + NetConst.ResCount; var p = new int[2 * k];
            p[0] = aqui; p[1] = Ahora(w);
            for (int r = 0; r < NetConst.ResCount; r++) p[2 + r] = -Math.Min(NetWorld.MaxScheduleTransfer, s.Cargo[r]);
            p[k] = vuelta; p[k + 1] = Espera;
            return p;
        }

        /// <summary>
        /// Horario de UNA parada en el nodo donde está: el tren se queda ahí y no se mueve (StepService no sale con menos de 2 paradas). Hace falta porque
        /// SetSchedule es cíclico: sin esto, un tren enviado volvería a las 23:59 y al día siguiente cargaría otra vez lo mismo (Nacho, revisión).
        /// La vista lo manda en cuanto el tren llega a su destino o vuelve. null si no está parado.
        /// </summary>
        public static int[] Aparcar(NetWorld w, int service)
        {
            if (service < 0 || service >= w.Services.Count) return null;
            var s = w.Services[service]; if (!s.AtStop || s.Ambushed) return null;
            var p = new int[2 + NetConst.ResCount]; p[0] = s.Stops[s.StopIndex].Node; p[1] = Espera;
            return p;
        }

        /// <summary>Ticks de juego que tarda el tren desde su parada actual hasta el destino (por el camino más corto, a la velocidad del tren). −1 sin camino.</summary>
        public static long EtaDesdeParada(NetWorld w, int service, int destino)
        {
            var s = w.Services[service]; int origen = s.Stops[s.StopIndex].Node;
            var path = w.Graph.ShortestPath(origen, destino); if (path.Count < 2) return -1;
            long mm = 0;
            for (int i = 0; i + 1 < path.Count; i++) mm += Largo(w, path[i], path[i + 1]);
            return (mm + s.Train.SpeedMmPerTick - 1) / Math.Max(1, s.Train.SpeedMmPerTick);
        }

        /// <summary>Ticks que le quedan a un tren en marcha hasta el final de su ruta. −1 si no va en marcha.</summary>
        public static long EtaEnMarcha(NetWorld w, int service)
        {
            var s = w.Services[service]; var t = s.Train;
            if (s.AtStop || t.Route.Count < 2 || t.Leg >= t.Route.Count - 1) return -1;
            long mm = Largo(w, t.Route[t.Leg], t.Route[t.Leg + 1]) - t.OffsetMm;
            for (int i = t.Leg + 1; i + 1 < t.Route.Count; i++) mm += Largo(w, t.Route[i], t.Route[i + 1]);
            return Math.Max(0, mm) / Math.Max(1, t.SpeedMmPerTick);
        }

        static long Largo(NetWorld w, int a, int b)
        {
            foreach (var e in w.Graph.Edges) if ((e.A == a && e.B == b) || (e.A == b && e.B == a)) return e.LengthMm;
            return 0;
        }

        /// <summary>Semilla de la salida de un tren (variantes de sonido de SalidaTren): según el tren, la parada y el día.</summary>
        public static uint SemillaSalida(int trainId, int node, long day) { unchecked { return (uint)(trainId * 73856093 ^ node * 19349663 ^ (int)day * 83492791) | 1u; } }

        /// <summary>«0:35»: horas y minutos de juego.</summary>
        public static string HhMm(long ticks)
        {
            if (ticks < 0) return "—";
            long min = ticks * NetConst.GameMsPerTick / 60000;
            return $"{min / 60}:{min % 60:00}";
        }
    }
}
