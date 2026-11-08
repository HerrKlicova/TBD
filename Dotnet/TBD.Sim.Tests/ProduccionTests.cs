using System;
using System.IO;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Producción de las estaciones (pulso por hora) y tope del almacén (Produccion.cs): parámetros, rendimiento, tope, depósito, nudo, guardado y hash.</summary>
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

            var w = NetScenario.Mvp();
            int Id(string n) { foreach (var x in w.Graph.Nodes) if (x.Name == n) return x.Id; return -1; }
            int cas = Id("Castellón"), sag = Id("Sagunto"), nul = Id("Nules"), ter = Id("Teruel");
            const int Comb = (int)Res.Combustible, Mat = (int)Res.Materiales, Per = (int)Res.Personas;
            const int R = ProductionState.ProdRes, H = ProductionState.HourTicks;

            // ---- rendimiento por plantilla (lineal a trozos, en milésimas)
            check(w.Production.FactorMil(cas, 30) == 1000 && w.Production.FactorMil(cas, 15) == 500 && w.Production.FactorMil(cas, 60) == 1500 && w.Production.FactorMil(cas, 90) == 2000 && w.Production.FactorMil(cas, 250) == 2000,
                "ciudad (Castellón): plantilla 30 = ×1; 15 = ×0,5; 60 = ×1,5; 90 = ×2 (tope); 250 = ×2");
            check(w.Production.FactorMil(nul, 12) == 1000 && w.Production.FactorMil(nul, 6) == 500 && w.Production.FactorMil(nul, 40) == 1000 && w.Production.FactorMil(nul, 0) == 0,
                "nodo (Nules): 12 = ×1; 6 = ×0,5; más de 12 no rinde más; sin gente no produce");
            check(NetScenario.Mvp(false).StoreCapU(cas) == 100 && w.StoreCapU(cas) == 300 && w.StoreCapU(sag) == 1000 && w.StoreCapU(ter) == 100,
                $"tope del almacén: Castellón 300 (100 + el depósito heredado; 100 sin estructuras de partida), Sagunto 1000 (nudo) y Teruel 100 → {w.StoreCapU(cas)}, {w.StoreCapU(sag)}, {w.StoreCapU(ter)}");

            // ---- escenario (Mak, 7-nov): Sagunto con 80 personas y Castellón infestada, sin gente, que se reclama como una fundación
            check(w.Stock[sag, Per] == 80 && w.Stock[cas, Per] == 0 && w.Stock[cas, Comb] == 0 && w.Stations.IsSite(cas) && w.StationKindOf(cas) == StationKind.NoFundada,
                "Sagunto arranca con 80 personas; Castellón infestada, sin gente ni almacén, y se reclama con la orden de fundar");

            // pasa «hours» horas enteras desde medianoche (cada Step que cruza una hora produce 1/24 del día)
            NetWorld Run(Action<NetWorld> setup, int hours, bool start = true)
            {
                var x = NetScenario.Mvp(start);
                x.DirectorEnabled = false; x.Services.Clear();          // sin trenes ni Colmena: nadie más toca el almacén
                setup?.Invoke(x);
                for (int h = 1; h <= hours; h++) { x.Tick = (long)h * H - 1; x.Step(); }
                return x;
            }
            int Hours(int pob, int target)              // horas hasta que Castellón (sin nada al empezar) tiene «target» u de combustible
            {
                var x = NetScenario.Mvp(); x.DirectorEnabled = false; x.Services.Clear(); x.Stock[cas, Per] = pob;
                for (int h = 1; h <= 24 * 30; h++) { x.Tick = (long)h * H - 1; x.Step(); if (x.Stock[cas, Comb] >= target) return h; }
                return -1;
            }

            // ---- pulso por hora: 60 u al día con la plantilla justa son 2,5 u por hora
            var h1 = Run(x => x.Stock[cas, Per] = 30, 1); var h2 = Run(x => x.Stock[cas, Per] = 30, 2);
            int k = cas * R + Comb;
            check(h1.Stock[cas, Comb] == 2 && h1.Production.Rem[k] == 12_000 && h2.Stock[cas, Comb] == 5 && h2.Production.Rem[k] == 0,
                $"cada hora produce 1/24 del día: 2,5 u por hora → {h1.Stock[cas, Comb]} u y medio de remanente tras 1 h, {h2.Stock[cas, Comb]} u tras 2 h");

            // ---- las dos pruebas de Hugo (castellon-bucle.md §3): el depósito de 300 u se llena en 5 días con 30 personas y en 2,5 con 90
            int t30 = Hours(30, 300), t90 = Hours(90, 300);
            check(t30 == 120, $"30 personas (×1, 60 u al día): 300 u en 5 días → {t30} h");
            check(t90 == 60, $"90 personas (×2, 120 u al día): 300 u en 2,5 días → {t90} h");

            // ---- tope: lo producido que no cabe se pierde; lo que ya estaba no se recorta
            var w1 = Run(x => { x.Stock[cas, Comb] = 250; x.Stock[cas, Per] = 80; }, 24);
            check(w1.Stock[cas, Comb] == 300 && w1.Production.WastedU[k] == 59 && w1.Production.Rem[k] == 23_520,
                $"Castellón (80 personas, ×1,833): 109,98 u en el día, 50 caben, 59 se pierden y queda 0,98 u de remanente → {w1.Stock[cas, Comb]} u, {w1.Production.WastedU[k]} perdidas, {w1.Production.Rem[k]}/24.000");
            int kt = ter * R + Mat;
            var w2 = Run(x => { x.Stock[ter, Mat] = 290; x.Stock[ter, Per] = 30; }, 24);
            check(w2.Stock[ter, Mat] == 290 && w2.Production.WastedU[kt] == 20, "Teruel con 290 u y tope 100: no se recorta lo que había y se pierden las 20 u del día");
            var w3 = Run(x => { x.Stock[ter, Mat] = 290; x.Stock[ter, Per] = 30; x.Works.Add(ter, x.Works.Preview(ter, PieceKind.Deposito, new[] { 0, 6000 }, 100)); x.Works.CompleteAll(ter); }, 24);
            check(w3.StoreCapU(ter) == 300 && w3.Stock[ter, Mat] == 300 && w3.Production.WastedU[kt] == 10, "con un depósito el tope de Teruel sube a 300: entran 10 y se pierden 10");
            var w3b = Run(x => { x.Works.Add(sag, x.Works.Preview(sag, PieceKind.Deposito, new[] { 0, 6000 }, 100)); x.Works.CompleteAll(sag); }, 0);
            check(w3b.StoreCapU(sag) == 1200, $"en el nudo el depósito suma sobre los 1000 (Hugo) → {w3b.StoreCapU(sag)}");

            var w4 = Run(x => { x.Stock[cas, Per] = 30; x.Graph.Nodes[cas].State = NodeState.Perdida; }, 24);
            check(w4.Stock[cas, Comb] == 0, "una estación perdida no produce");

            var w6 = Run(x => { x.Stock[sag, Mat] = 0; x.Stock[ter, Mat] = 0; }, 24, false);     // sin el muro de partida: el mantenimiento no se come 1 u
            check(w6.Stock[sag, Mat] == 146 && w6.Stock[ter, Mat] == 23,
                $"Sagunto (80 personas, ×1,833) produce 146 u de materiales al día y Teruel (40, ×1,167) 23 u → {w6.Stock[sag, Mat]} y {w6.Stock[ter, Mat]}");

            // ---- reclamar Castellón: con la fundación tiene gente y empieza a producir
            var wf = NetScenario.Mvp(); wf.DirectorEnabled = false;
            var sv = wf.Services[0]; int stop = -1; for (int i = 0; i < sv.Stops.Count; i++) if (sv.Stops[i].Node == cas) stop = i;
            if (stop >= 0) { sv.AtStop = true; sv.StopIndex = stop; sv.Cargo[Mat] = 30; sv.Cargo[Per] = 12; }
            wf.Graph.Nodes[cas].HordeDensity = 0;
            var fr = stop >= 0 ? wf.FoundStation(cas) : FoundResult.SinTren;
            check(stop >= 0 && fr == FoundResult.Ok && wf.Stock[cas, Per] == 12 && wf.ProductionTodayU(cas, Comb) == 24,
                $"reclamar Castellón (30 u y 12 personas desde el tren 1) la deja con 12 personas, que producen 24 u de combustible al día → {fr}, {wf.Stock[cas, Per]}, {wf.ProductionTodayU(cas, Comb)}");

            // ---- guardado: el remanente y las pérdidas viajan en la partida, y la versión 1 se migra con ellos a 0
            var bytes = NetSave.Save(w1);
            var wl = NetScenario.Mvp(); wl.Services.Clear(); NetSave.Load(bytes, wl);
            check(wl.Hash() == w1.Hash() && wl.Production.Rem[k] == 23_520 && wl.Production.WastedU[k] == 59, "guardar y cargar conserva el remanente y las pérdidas (mismo hash)");
            var fresh = NetScenario.Mvp(false);
            var sw = new SaveWriter(); fresh.SaveState(sw); var v3 = sw.ToArray();
            var ew = new SaveWriter(); EmbarqueState.WriteEmpty(ew); int etail = ew.ToArray().Length;      // D-N166: los pasajeros (vacío) van al final, NetVersion 3
            var v2 = new byte[v3.Length - etail]; Array.Copy(v3, v2, v2.Length);
            int tail = ProductionTailLength(fresh.Graph.Nodes.Count);
            var v1 = new byte[v2.Length - tail]; Array.Copy(v2, v1, v1.Length);
            var mig = NetSave.NetMigrations[1](v1);
            check(BytesEqual(mig, v2), "la migración 1 → 2 añade la producción a 0 y deja la sección igual que una partida v2 nueva");
            check(NetSave.NetVersion == 3 && BytesEqual(NetSave.NetMigrations[2](v2), v3), "D-N166: la migración 2 → 3 añade los pasajeros (ninguno) y deja la sección igual que una partida nueva");
            check(fresh.Production.Hash() == 0, "sin producir nada el hash de la producción es 0");
        }

        static int ProductionTailLength(int nodes) { var w = new SaveWriter(); ProductionState.WriteEmpty(w, nodes); return w.Length; }
        static bool BytesEqual(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    }
}
