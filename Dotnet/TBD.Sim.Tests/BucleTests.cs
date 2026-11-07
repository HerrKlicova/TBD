using System;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Bucle Sagunto–Castellón (BucleCastellon): horario válido, dos viajes al día y saldo de combustible. Mide la cifra SIN VERIFICAR de Hugo.</summary>
    static class BucleTests
    {
        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[18d] Bucle de trenes Sagunto–Castellón");
            var w = NetScenario.Mvp(); w.DirectorEnabled = false;
            int Id(string n) { foreach (var x in w.Graph.Nodes) if (x.Name == n) return x.Id; return -1; }
            int sag = Id("Sagunto"), cas = Id("Castellón");
            const int Comb = (int)Res.Combustible, Mat = (int)Res.Materiales, Per = (int)Res.Personas;
            // Castellón ya reclamada con su plantilla justa (30 personas: ×1, 60 u de combustible al día)
            w.Stations.Founded[cas] = true; w.Stock[cas, Per] = 30;
            int svc = -1; for (int i = 0; i < w.Services.Count; i++) if (w.Services[i].AtStop && w.Services[i].Stops[w.Services[i].StopIndex].Node == sag) svc = i;
            var r = svc < 0 ? ScheduleResult.TrenInexistente : w.SetSchedule(svc, BucleCastellon.Horario(w));
            check(svc >= 0 && r == ScheduleResult.Ok, $"el horario del bucle es válido para el tren que está en Sagunto ({r})");

            long sagComb0 = w.Stock[sag, Comb]; int casMat0 = w.Stock[cas, Mat];
            var arrivals = new int[6]; long casMax = 0;
            for (int d = 0; d < 5; d++)
            {
                long before = w.Stock[sag, Comb];
                long end = (w.Tick / NetConst.DayTicks + 1) * NetConst.DayTicks;
                int visits = 0; bool atCas = false;
                while (w.Tick < end)
                {
                    w.ApplyCommands(); w.Step();
                    var s = w.Services[svc];
                    bool now = s.AtStop && s.Stops[s.StopIndex].Node == cas;
                    if (now && !atCas) visits++;
                    atCas = now; casMax = Math.Max(casMax, w.Stock[cas, Comb]);
                }
                arrivals[d] = visits;
                Console.WriteLine($"         día {d + 1}: llegadas a Castellón {visits}, combustible en Sagunto {before} → {w.Stock[sag, Comb]}, en Castellón {w.Stock[cas, Comb]}, materiales en Castellón {w.Stock[cas, Mat]}");
            }
            check(arrivals[1] == 2 && arrivals[2] == 2 && arrivals[3] == 2, $"dos viajes redondos al día con un solo tren (días 2–4: {arrivals[1]}, {arrivals[2]}, {arrivals[3]})");
            long gained = w.Stock[sag, Comb] - sagComb0;
            check(gained > 0 && w.Stock[cas, Mat] > casMat0, $"el bucle trae combustible a Sagunto (+{gained} u en 5 días) y lleva materiales a Castellón (+{w.Stock[cas, Mat] - casMat0} u)");
            check(casMax <= w.StoreCapU(cas), $"con el tren pasando, el depósito de Castellón no se llena (máximo {casMax} de {w.StoreCapU(cas)} u): el combustible sale al ritmo que se produce");
        }
    }
}
