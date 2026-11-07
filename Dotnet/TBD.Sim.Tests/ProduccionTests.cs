using System;
using System.IO;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Producción diaria de las estaciones y tope del almacén (Produccion.cs): parámetros, rendimiento, tope, depósito, nudo, guardado y hash.</summary>
    static class ProduccionTests
    {
        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[18b] Producción de las estaciones y tope del almacén");
            string dir = BalanceTests.DataDirPublic();
            CsvTable T(string f) => new CsvTable(File.ReadAllText(Path.Combine(dir, f)));

            // ---- parámetros: el código y los CSV dicen lo mismo
            var csv = ProductionParams.FromCsv(T("balance_nodos.csv"), T("balance_economia.csv"), T("balance_construccion.csv"));
            var def = ProductionParams.Default;
            bool same = csv.Names.Length == def.Names.Length && csv.GarrisonReq == def.GarrisonReq && csv.CityMaxMil == def.CityMaxMil && csv.CitySlopeMil == def.CitySlopeMil
                && csv.StoreBaseU == def.StoreBaseU && csv.StoreDepositU == def.StoreDepositU && csv.StoreHubU == def.StoreHubU;
            for (int i = 0; same && i < csv.Names.Length; i++)
                same = csv.Names[i] == def.Names[i] && csv.City[i] == def.City[i] && csv.Hub[i] == def.Hub[i]
                    && csv.ProdU[i * 3] == def.ProdU[i * 3] && csv.ProdU[i * 3 + 1] == def.ProdU[i * 3 + 1] && csv.ProdU[i * 3 + 2] == def.ProdU[i * 3 + 2];
            check(same, "los valores por defecto coinciden con balance_nodos.csv, balance_economia.csv y balance_construccion.csv");

            var w = NetScenario.Mvp(false);
            int Id(string n) { foreach (var x in w.Graph.Nodes) if (x.Name == n) return x.Id; return -1; }
            int cas = Id("Castellón"), sag = Id("Sagunto"), nul = Id("Nules"), ter = Id("Teruel");
            const int Comb = (int)Res.Combustible, Mat = (int)Res.Materiales, Per = (int)Res.Personas;

            // ---- rendimiento por plantilla (lineal a trozos, en milésimas)
            check(w.Production.FactorMil(cas, 30) == 1000 && w.Production.FactorMil(cas, 15) == 500 && w.Production.FactorMil(cas, 60) == 1500 && w.Production.FactorMil(cas, 90) == 2000 && w.Production.FactorMil(cas, 250) == 2000,
                "ciudad (Castellón): plantilla 30 = ×1; 15 = ×0,5; 60 = ×1,5; 90 = ×2 (tope); 250 = ×2");
            check(w.Production.FactorMil(nul, 12) == 1000 && w.Production.FactorMil(nul, 6) == 500 && w.Production.FactorMil(nul, 40) == 1000 && w.Production.FactorMil(nul, 0) == 0,
                "nodo (Nules): 12 = ×1; 6 = ×0,5; más de 12 no rinde más; sin gente no produce");
            check(w.StoreCapU(cas) == 100 && w.StoreCapU(sag) == 1000, "tope del almacén: 100 en una estación y 1000 en el nudo (Sagunto)");

            // ---- un día: Castellón con 80 personas produce 60 × 1,833 = 109,98 u de combustible y solo caben 100
            NetWorld Midnight(Action<NetWorld> setup)
            {
                var x = NetScenario.Mvp(false);
                x.DirectorEnabled = false;
                setup?.Invoke(x);
                x.Tick = NetConst.DayTicks - 1;
                x.Step();
                return x;
            }
            var w1 = Midnight(x => { x.Stock[cas, Comb] = 0; });
            int k = cas * ProductionState.ProdRes + Comb;
            check(w1.Stock[cas, Comb] == 100 && w1.Production.WastedU[k] == 9 && w1.Production.RemMil[k] == 980,
                $"Castellón (80 personas, ×1,833): 109 u del día, 100 al almacén, 9 perdidas y 0,98 u de remanente (almacén {w1.Stock[cas, Comb]}, perdidas {w1.Production.WastedU[k]}, remanente {w1.Production.RemMil[k]} ‰)");

            var w2 = Midnight(x => { x.Stock[cas, Comb] = 50; x.Stock[cas, Per] = 30; });
            check(w2.Stock[cas, Comb] == 100 && w2.Production.WastedU[k] == 10, "con el almacén a 50 y la plantilla justa (60 u): 50 entran y 10 se pierden");

            var w3 = Midnight(x => { x.Stock[cas, Comb] = 0; x.Stock[cas, Per] = 30; x.Works.Add(cas, x.Works.Preview(cas, PieceKind.Deposito, new[] { 0, 6000 }, 100)); x.Works.CompleteAll(cas); });
            check(w3.StoreCapU(cas) == 300 && w3.Stock[cas, Comb] == 60 && w3.Production.WastedU[k] == 0, "con un depósito construido el tope sube a 300 y no se pierde nada");

            var w4 = Midnight(x => { x.Stock[cas, Comb] = 0; x.Graph.Nodes[cas].State = NodeState.Perdida; });
            check(w4.Stock[cas, Comb] == 0, "una estación perdida no produce");

            var w5 = Midnight(x => { x.Stock[cas, Comb] = 250; x.Stock[cas, Per] = 30; });
            check(w5.Stock[cas, Comb] == 250 && w5.Production.WastedU[k] == 60, "lo que ya estaba por encima del tope no se recorta: solo se pierde lo producido");

            var w6 = Midnight(x => { x.Stock[sag, Mat] = 0; x.Stock[ter, Mat] = 0; });
            check(w6.Stock[sag, Mat] == 160 && w6.Stock[ter, Mat] == 23,
                $"Sagunto (170 personas, ×2) produce 160 u de materiales y Teruel (40, ×1,17) 23 u → {w6.Stock[sag, Mat]} y {w6.Stock[ter, Mat]}");

            // ---- guardado: el remanente y las pérdidas viajan en la partida, y la versión 1 se migra con ellos a 0
            var bytes = NetSave.Save(w1);
            var wl = NetScenario.Mvp(false); NetSave.Load(bytes, wl);
            check(wl.Hash() == w1.Hash() && wl.Production.RemMil[k] == 980 && wl.Production.WastedU[k] == 9, "guardar y cargar conserva el remanente y las pérdidas (mismo hash)");
            var fresh = NetScenario.Mvp(false);
            var sw = new SaveWriter(); fresh.SaveState(sw); var v2 = sw.ToArray();
            int tail = ProductionTailLength(fresh.Graph.Nodes.Count);
            var v1 = new byte[v2.Length - tail]; Array.Copy(v2, v1, v1.Length);
            var mig = NetSave.NetMigrations[1](v1);
            check(NetSave.NetVersion == 2 && mig.Length == v2.Length && BytesEqual(mig, v2), "la migración 1 → 2 añade la producción a 0 y deja la sección igual que una partida nueva");
            check(fresh.Production.Hash() == 0, "sin producir nada el hash de la producción es 0");
        }

        static int ProductionTailLength(int nodes) { var w = new SaveWriter(); ProductionState.WriteEmpty(w, nodes); return w.Length; }
        static bool BytesEqual(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    }
}
