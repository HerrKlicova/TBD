using System;
using System.Collections.Generic;
using TBD.Units;

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

        /// <summary>Id de datos de una estación («castellon», «nules»…: minúsculas y sin tildes, como en los JSON de Hugo y Álvaro) → nodo de RailNet. −1 si no está (Bétera vive fuera de RailNet).</summary>
        public static int NodoPorId(NetWorld w, string id) { return w.Graph.Nodes.FindIndex(n => IdDe(n.Name) == id); }
        public static string IdDe(string nombre)
        {
            var d = nombre.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD); var sb = new System.Text.StringBuilder();
            foreach (var c in d) if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c == ' ' ? '_' : c);
            return sb.ToString();
        }

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
            long min = NetConst.GameMs(ticks) / 60000;
            return $"{min / 60}:{min % 60:00}";
        }
    }

    /// <summary>
    /// Carga visible de Teo sobre las plataformas Rgs (carga-vagones, aprobada por Lucía): un módulo por recurso, hijo del vagón en (0; 1,25; 0) y sin giro.
    /// Lleno a partir de 2/3 de la capacidad, Mitad a partir de 1/3 y Vacío por debajo; Dañado y Destrozado según la vida del vagón (por debajo del 70 % y del 30 %,
    /// validados por Hugo). La tropa no lleva módulo (es el vagón R2). Capacidad por plataforma: 20 u (sim_economia de Hugo), así que la carga del objetivo de Nules llena justo una.
    /// </summary>
    public static class CargaVisible
    {
        public static readonly string[] Recurso = { "Municion", "Combustible", "Materiales", null, "Viveres" };
        public const int CapacidadRefU = 20, DanadoPm = 700, DestrozadoPm = 300;     // Hugo: 20 u por plataforma (100 u por tren); umbrales validados

        public static string Estado(int u, int capacidad, int vidaPm = 1000)
        {
            if (vidaPm < DestrozadoPm) return "Destrozado";
            if (vidaPm < DanadoPm) return "Danado";
            if (capacidad <= 0) capacidad = CapacidadRefU;
            return u * 3 >= capacidad * 2 ? "Lleno" : u * 3 >= capacidad ? "Mitad" : "Vacio";
        }

        /// <summary>Fichero del módulo (sin _LOD1); null para las personas.</summary>
        public static string Archivo(int recurso, string estado) { return recurso < 0 || recurso >= Recurso.Length || Recurso[recurso] == null ? null : "SM_Carga" + Recurso[recurso] + "_" + estado; }

        /// <summary>
        /// Qué lleva cada plataforma: una por recurso con carga, en el orden del panel (munición, combustible, materiales, víveres). Si hay más recursos que
        /// plataformas, los que sobran no se ven; las plataformas que sobran van sin módulo (recurso −1).
        /// </summary>
        public static int[] Reparto(int[] cargo, int plataformas)
        {
            var r = new int[Math.Max(0, plataformas)]; for (int i = 0; i < r.Length; i++) r[i] = -1;
            int k = 0;
            if (cargo != null) for (int res = 0; res < cargo.Length && k < r.Length; res++) if (Recurso.Length > res && Recurso[res] != null && cargo[res] > 0) r[k++] = res;
            return r;
        }
    }

    /// <summary>
    /// Acopio de víveres de Nules de Teo (acopio_viveres, aprobado por Lucía): 5 pilas (nules_instancias.csv, grupo «viveres») que siguen a los víveres de Nules
    /// con la regla de los vagones sobre la capacidad de referencia de Hugo (30 u: Lleno desde 20, Mitad desde 10). Saqueado si Nules cae y Quemado si arde.
    /// Las variantes v0–v4 SON los estados: todas comparten origen y solo cambia la malla.
    /// </summary>
    public static class AcopioViveres
    {
        public static readonly string[] Estados = { "Lleno", "Mitad", "Vacio", "Saqueado", "Quemado" };
        public static int CapacidadRefU = 30;

        public static int Estado(int viveres, bool caida = false, bool quemada = false)
        {
            if (quemada) return 4;
            if (caida) return 3;
            return viveres * 3 >= CapacidadRefU * 2 ? 0 : viveres * 3 >= CapacidadRefU ? 1 : 2;
        }

        public static string Archivo(int estado) { return "SM_AcopioViveres_v" + estado + "_" + Estados[estado]; }

        /// <summary>Lee la capacidad de referencia de Nules del bloque «viveres.capacidad_ref_u» de existencias_red.json.</summary>
        public static bool LeerCapacidad(string json, string id = "nules")
        {
            if (!(Mini.Parse(json) is Dictionary<string, object> root) || !root.TryGetValue("viveres", out var v) || !(v is Dictionary<string, object> vd)) return false;
            if (!vd.TryGetValue("capacidad_ref_u", out var c) || !(c is List<object> l)) return false;
            foreach (var o in l) if (o is Dictionary<string, object> d && d.TryGetValue("id", out var i) && i as string == id && d.TryGetValue("u", out var u)) { CapacidadRefU = (int)Convert.ToDouble(u, System.Globalization.CultureInfo.InvariantCulture); return true; }
            return false;
        }

        /// <summary>Las pilas del CSV de Álvaro (grupo «viveres»): x, y, z y yaw.</summary>
        public static List<float[]> Pilas(string csv)
        {
            var r = new List<float[]>();
            foreach (var raw in csv.Replace("\r", "").Split('\n'))
            {
                var c = raw.Split(';'); if (c.Length < 7 || c[0] != "viveres") continue;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                r.Add(new[] { float.Parse(c[3], ci), float.Parse(c[4], ci), float.Parse(c[5], ci), float.Parse(c[6], ci) });
            }
            return r;
        }
    }

    /// <summary>
    /// D-N152: la regla es la de la red de Nacho (nacho/tren-puertas: TrainService.GateNode, guardado en la v4; plazas en NetWorld.TrainSlots). Esto solo la
    /// lee para el panel, la zona y el tutorial, para que no haya dos reglas. D-N177 (Lucía, provisional): aviso en ámbar «Sin escolta: X está sitiada» al dar la
    /// orden de salida y en el icono del tren; el rojo, solo si el tren cae.
    /// </summary>
    public static class Puertas
    {
        public static int Plazas(NetWorld w, int node) { return w == null ? 0 : w.TrainSlots(node); }

        public static bool EnPuertas(NetWorld w, int service, int node) { return w != null && service >= 0 && service < w.Services.Count && w.AtGate(w.Services[service], node); }

        /// <summary>Texto para el panel y el aviso, o null si no espera.</summary>
        public static string Motivo(NetWorld w, int service) { return w == null ? null : w.GateReason(service); }

        /// <summary>D-N177: «Sin escolta: X está sitiada» si el tren va hacia una estación sitiada o espera a sus puertas por el asedio; null si no.</summary>
        public static string SinEscolta(NetWorld w, int service)
        {
            if (w == null || service < 0 || service >= w.Services.Count) return null;
            var sv = w.Services[service];
            int n = sv.GateNode >= 0 ? sv.GateNode : !sv.AtStop && sv.StopIndex >= 0 && sv.StopIndex < sv.Stops.Count ? sv.Stops[sv.StopIndex].Node : -1;
            return n >= 0 && w.Besieged(n) ? AvisoSitiada(w, n) : null;
        }

        public static string AvisoSitiada(NetWorld w, int node) { return "Sin escolta: " + w.Graph.Nodes[node].Name + " está sitiada"; }
    }
}
