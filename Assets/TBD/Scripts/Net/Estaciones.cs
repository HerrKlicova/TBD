using System;
using System.Collections.Generic;

namespace TBD.Net
{
    // Hito 5, «fundar una estación» (diseno-hito5.md §4, D-N18). Lógica pura en enteros, sin Unity.
    // Modelo del flag (Mak, 6-nov): NO se añaden nodos en caliente (los arrays por nodo son fijos). Los emplazamientos fundables existen desde
    // el principio sin fundar, y fundar pone su bandera a true. La bandera entra en el hash (solo cuando es true: sin fundar vale 0).
    // Hoy el único emplazamiento de la demo es Nules; la lista es configurable (StationParams.Sites). Jérica salió de la demo (Hugo, Lucía).

    public enum StationKind : byte { NoFundada = 0, Apeadero = 1, Estacion = 2 }

    public enum RepairResult : byte { Ok = 0, SinEscombros = 1, SinTrenDeObras = 2, TrenLejos = 3, SinCarga = 4, HordeEncima = 5, YaEnObra = 6 }
    public enum ScheduleResult : byte { Ok = 0, TrenInexistente = 1, NoEstaParado = 2, HorarioInvalido = 3, NoIncluyeParadaActual = 4 }
    public enum FoundResult : byte { Ok = 0, NoEsEmplazamiento = 1, YaFundada = 2, SinTren = 3, SinCarga = 4, HordeEncima = 5 }

    public struct StationParams
    {
        public int FoundMaterialsU;      // fundar_materiales (balance_economia.csv): 30 u
        public int FoundPeople;          // guarnicion_minima (balance_economia.csv): 12 personas
        public int StationPeople;        // población para subir de apeadero a estación: 40
        public int StopRadiusMm;         // un tren parado a esta distancia del nodo cuenta como «en el emplazamiento»
        public string[] Sites;           // nombres de los nodos fundables

        public static StationParams Default
        {
            get { return new StationParams { FoundMaterialsU = 30, FoundPeople = 12, StationPeople = 40, StopRadiusMm = 5_000, Sites = new[] { "Nules" } }; }
        }

        /// <summary>De balance_economia.csv. «estacion_personas» aún no está en el CSV de Hugo (valor por defecto, TryValue).</summary>
        public static StationParams FromCsv(CsvTable e)
        {
            var d = Default;
            d.FoundMaterialsU = (int)e.PositiveValue("fundar_materiales");
            d.FoundPeople = (int)e.PositiveValue("guarnicion_minima");
            d.StationPeople = (int)e.TryValue("estacion_personas", d.StationPeople);
            d.StopRadiusMm = (int)e.TryValue("fundar_radio_tren_mm", d.StopRadiusMm);
            return d;
        }
    }

    /// <summary>Qué nodos están fundados. Va dentro del hash de NetWorld (existencias).</summary>
    public sealed class StationState
    {
        public StationParams P;
        public readonly bool[] Founded;
        readonly string[] _names;

        public StationState(RailGraph g, StationParams p)
        {
            P = p; Founded = new bool[g.Nodes.Count]; _names = new string[g.Nodes.Count];
            for (int i = 0; i < _names.Length; i++) _names[i] = g.Nodes[i].Name;
        }

        public void SaveState(SaveWriter w) { w.Bools(Founded); }

        public void LoadState(SaveReader r)
        {
            var f = r.Bools();
            if (f == null || f.Length != Founded.Length) throw new SaveException(SaveError.TopologyMismatch, "estaciones fundadas: otro número de nodos");
            Array.Copy(f, Founded, f.Length);
        }

        public bool IsSite(int node)
        {
            if (node < 0 || node >= _names.Length || P.Sites == null) return false;
            return Array.IndexOf(P.Sites, _names[node]) >= 0;
        }

        public ulong Hash()
        {
            ulong h = 0;
            for (int n = 0; n < Founded.Length; n++) if (Founded[n]) h += NetWorld.Mix(0xF04DEDUL ^ ((ulong)(uint)n << 40));
            return h;
        }
    }
}
