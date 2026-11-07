using System;
using System.Collections.Generic;

namespace TBD.Net
{
    // Producción diaria de las estaciones y tope del almacén (Iker, 7-nov; especificación de Hugo en economia/castellon-bucle.md §3 y
    // red-estaciones-aviso.md §4). Lógica pura en enteros, sin Unity. Va dentro del hash (existencias) y del guardado (NetVersion 2).
    //
    // Reglas (de sim_economia.py, «2) PRODUCCIÓN»):
    //  - Produce cada nodo con gente (personas > 0) que no esté perdido, una vez al día a medianoche, lo de balance_nodos.csv (prod_*).
    //  - Rendimiento por plantilla: req = guarnicion_minima (12), ×2,5 en las ciudades. f = min(1, pob/req); en una ciudad con pob > req,
    //    f = min(rend_ciudad_max, 1 + rend_ciudad_pendiente·(pob/req − 1)). Todo en milésimas, con remanente exacto por nodo y recurso.
    //  - Tope del almacén POR RECURSO [propuesta de Iker, a confirmar con Hugo]: almacen_base_u (100) + almacen_deposito_extra_u (200) por
    //    depósito construido; el nudo (rama 0, Sagunto) tiene almacen_nudo_u (1000). La producción que no cabe se pierde («Castellón no puede
    //    tirar producción»); lo que llega en tren o ya estaba no se recorta.
    //  - Víveres: el MVP de Unity no tiene ese recurso (Res), así que prod_viveres no se usa. El chatarreo (3 u al día al azar) tampoco.

    public struct ProductionParams
    {
        public int GarrisonReq;          // guarnicion_minima: 12 personas para producir al 100 % en un nodo
        public int CityReqMil;           // ×2,5 en las ciudades (mult_req_ciudad de sim_economia; aún no está en el CSV)
        public int CityMaxMil;           // rend_ciudad_max: 2000 (×2)
        public int CitySlopeMil;         // rend_ciudad_pendiente: 500 (+0,5 por plantilla extra)
        public int StoreBaseU;           // almacen_base_u: 100 por estación
        public int StoreDepositU;        // almacen_deposito_extra_u: 200 por depósito
        public int StoreHubU;            // almacen_nudo_u: 1000 (Sagunto)
        public string[] Names;           // nodo de cada fila de balance_nodos.csv
        public int[] ProdU;              // por fila y recurso (Municion, Combustible, Materiales): u al día con plantilla plena
        public bool[] City, Hub;

        public static ProductionParams Default
        {
            get
            {
                // copia de balance_nodos.csv (una prueba vigila que coincidan)
                var names = new[] { "Sagunto", "Puçol", "Valencia", "Almenara", "Nules", "Castellón", "Segorbe", "Barracas", "Teruel", "Caminreal", "Calamocha", "Cariñena", "Zaragoza" };
                var prod = new int[names.Length * 3];
                prod[0 * 3 + 2] = 80; prod[5 * 3 + 1] = 60; prod[8 * 3 + 2] = 20; prod[12 * 3 + 0] = 40;
                var city = new bool[names.Length]; foreach (int i in new[] { 0, 2, 5, 8, 12 }) city[i] = true;
                var hub = new bool[names.Length]; hub[0] = true;
                return new ProductionParams
                {
                    GarrisonReq = 12, CityReqMil = 2500, CityMaxMil = 2000, CitySlopeMil = 500,
                    StoreBaseU = 100, StoreDepositU = 200, StoreHubU = 1000,
                    Names = names, ProdU = prod, City = city, Hub = hub
                };
            }
        }

        public static ProductionParams FromCsv(CsvTable nodos, CsvTable economia, CsvTable construccion)
        {
            var d = Default;
            if (nodos != null)
            {
                int cn = nodos.Col("nodo"), cr = nodos.Col("rama"), cc = nodos.Col("ciudad");
                int cm = nodos.Col("prod_municion"), cf = nodos.Col("prod_combustible"), ct = nodos.Col("prod_materiales");
                int n = nodos.Rows.Count;
                d.Names = new string[n]; d.ProdU = new int[n * 3]; d.City = new bool[n]; d.Hub = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    var r = nodos.Rows[i];
                    d.Names[i] = r[cn].Trim();
                    d.Hub[i] = CsvTable.Int(r[cr]) == 0;
                    d.City[i] = CsvTable.Int(r[cc]) != 0;
                    d.ProdU[i * 3 + 0] = (int)CsvTable.Int(r[cm]);
                    d.ProdU[i * 3 + 1] = (int)CsvTable.Int(r[cf]);
                    d.ProdU[i * 3 + 2] = (int)CsvTable.Int(r[ct]);
                }
            }
            if (economia != null)
            {
                d.GarrisonReq = (int)economia.PositiveValue("guarnicion_minima");
                d.CityReqMil = (int)economia.TryValue("mult_req_ciudad", d.CityReqMil);
                d.CityMaxMil = (int)economia.TryValue("rend_ciudad_max", d.CityMaxMil);
                d.CitySlopeMil = (int)economia.TryValue("rend_ciudad_pendiente", d.CitySlopeMil);
            }
            if (construccion != null)
            {
                d.StoreBaseU = (int)construccion.TryValue("almacen_base_u", d.StoreBaseU);
                d.StoreDepositU = (int)construccion.TryValue("almacen_deposito_extra_u", d.StoreDepositU);
                d.StoreHubU = (int)construccion.TryValue("almacen_nudo_u", d.StoreHubU);
            }
            return d;
        }
    }

    /// <summary>Remanentes de la producción (milésimas de u) y lo que se ha perdido por almacén lleno. Va dentro del hash de NetWorld (existencias).</summary>
    public sealed class ProductionState
    {
        public const int ProdRes = 3;                 // Municion, Combustible, Materiales (las personas no se producen)
        public ProductionParams P;
        public readonly int[] Row;                    // fila de balance_nodos.csv de cada nodo del grafo (-1 = no está: no produce)
        public readonly int[] RemMil;                 // por nodo y recurso: milésimas de u acumuladas que aún no llegan a 1 u
        public readonly long[] WastedU;               // por nodo y recurso: u perdidas porque el almacén estaba lleno (para QA y el panel)

        public ProductionState(RailGraph g, ProductionParams p)
        {
            P = p;
            int n = g.Nodes.Count;
            Row = new int[n]; RemMil = new int[n * ProdRes]; WastedU = new long[n * ProdRes];
            for (int i = 0; i < n; i++) Row[i] = p.Names == null ? -1 : Array.IndexOf(p.Names, g.Nodes[i].Name);
        }

        /// <summary>Rendimiento en milésimas con «pob» personas en el nodo (lineal a trozos, como sim_economia).</summary>
        public int FactorMil(int node, int pob)
        {
            int row = Row[node];
            if (row < 0 || pob <= 0) return 0;
            bool city = P.City[row];
            long req = (long)P.GarrisonReq * (city ? P.CityReqMil : 1000);     // en milésimas de persona
            long ratio = (long)pob * 1_000_000 / req;                          // pob/req en milésimas
            if (!city || ratio <= 1000) return (int)Math.Min(1000, ratio);
            return (int)Math.Min(P.CityMaxMil, 1000 + P.CitySlopeMil * (ratio - 1000) / 1000);
        }

        /// <summary>Producción con plantilla plena del nodo, en u al día (0 si no está en el CSV).</summary>
        public int BaseU(int node, int res) { int row = Row[node]; return row < 0 ? 0 : P.ProdU[row * ProdRes + res]; }

        public bool IsHub(int node) { int row = Row[node]; return row >= 0 && P.Hub[row]; }

        public void SaveState(SaveWriter w) { w.Ints(RemMil); w.Longs(WastedU); }

        public void LoadState(SaveReader r)
        {
            var a = r.Ints(); var b = r.Longs();
            if (a == null || a.Length != RemMil.Length || b == null || b.Length != WastedU.Length)
                throw new SaveException(SaveError.TopologyMismatch, "producción: otro número de nodos");
            Array.Copy(a, RemMil, a.Length); Array.Copy(b, WastedU, b.Length);
        }

        /// <summary>Lo que añade la versión 2 al final de la sección NET de una partida de la versión 1: remanentes y pérdidas a 0.</summary>
        public static void WriteEmpty(SaveWriter w, int nodes) { w.Ints(new int[nodes * ProdRes]); w.Longs(new long[nodes * ProdRes]); }

        public ulong Hash()
        {
            ulong h = 0;
            for (int i = 0; i < RemMil.Length; i++)
            {
                if (RemMil[i] != 0) h += NetWorld.Mix(0x960D0CUL ^ ((ulong)(uint)i << 32) ^ (ulong)(uint)RemMil[i]);
                if (WastedU[i] != 0) h += NetWorld.Mix(0x7A57EDUL ^ ((ulong)(uint)i << 40) ^ (ulong)WastedU[i]);
            }
            return h;
        }
    }

    public sealed partial class NetWorld
    {
        /// <summary>Solo para pruebas que aíslan otro sistema (mantenimiento, conservación de la carga): apaga la producción. No se guarda; el juego siempre la lleva encendida.</summary>
        public bool ProductionEnabled = true;

        /// <summary>Tope del almacén de un nodo para cada recurso producido: el nudo, 1000; el resto, 100 más 200 por depósito construido y sano.</summary>
        public int StoreCapU(int node)
        {
            if (Production.IsHub(node)) return Production.P.StoreHubU;
            int deposits = 0;
            foreach (var p in Works.Snapshot(node)) if (p.Kind == PieceKind.Deposito && p.Built && !p.Broken) deposits++;
            return Production.P.StoreBaseU + Production.P.StoreDepositU * deposits;
        }

        /// <summary>Producción de hoy en un nodo, en u (sin el remanente ni el tope): para el panel.</summary>
        public int ProductionTodayU(int node, int res)
        {
            if (Graph.Nodes[node].State == NodeState.Perdida) return 0;
            return (int)((long)Production.BaseU(node, res) * Production.FactorMil(node, Stock[node, (int)Res.Personas]) / 1000);
        }

        /// <summary>A medianoche: cada nodo con gente produce lo suyo según su plantilla, hasta el tope del almacén. Lo que no cabe se pierde.</summary>
        void ProduceDay()
        {
            if (!ProductionEnabled) return;
            for (int n = 0; n < Graph.Nodes.Count; n++)
            {
                if (Graph.Nodes[n].State == NodeState.Perdida) continue;
                int f = Production.FactorMil(n, Stock[n, (int)Res.Personas]);
                if (f <= 0) continue;
                int cap = -1;
                for (int r = 0; r < ProductionState.ProdRes; r++)
                {
                    int baseU = Production.BaseU(n, r);
                    if (baseU <= 0) continue;
                    int k = n * ProductionState.ProdRes + r;
                    long total = Production.RemMil[k] + (long)baseU * f;            // u × milésimas = milésimas de u
                    int give = (int)(total / 1000);
                    Production.RemMil[k] = (int)(total % 1000);
                    if (cap < 0) cap = StoreCapU(n);
                    int room = Math.Max(0, cap - Stock[n, r]);
                    int add = Math.Min(give, room);
                    Stock[n, r] += add;
                    if (give > add) Production.WastedU[k] += give - add;
                }
            }
        }
    }
}
