using System;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>[37] Hordas de zona (Lucía, 8-nov 23:20): Nules recibe su propia horda (2400) desde la 2.ª noche; fuera de pantalla la resuelve la regla abstracta S0.</summary>
    public static class ZonaHordaTests
    {
        static int Id(NetWorld w, string n) { return w.Graph.Nodes.FindIndex(x => x.Name == n); }
        static void To(NetWorld w, long tick) { while (w.Tick < tick) { w.ApplyCommands(); w.Step(); } }
        static bool Planned(NetWorld w, int n) { foreach (var p in w.Director.Active) if (HiveDirector.IsZoneSiege(p) && p.Node == n) return true; return false; }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[37] Hordas de zona: Nules desde la 2.ª noche, regla abstracta fuera de pantalla");
            var w = NetScenario.Inicio(); int nul = Id(w, "Nules"), sag = Id(w, "Sagunto");
            check(w.Director.ZoneHordes.Count == 1 && w.Director.ZoneHordes[0].Node == nul && w.Director.ZoneHordes[0].Zombies == 2400 && w.Director.ZoneHordes[0].FromDay == 1, "Inicio: horda propia de Nules, 2400, desde el día 1 (2.ª noche)");
            long D = NetConst.DayTicks;
            To(w, NetConst.Hours(23)); bool n1 = Planned(w, nul) || w.Besieged(nul);
            check(!n1, "la 1.ª noche, Nules sin asedio (D-N156)");
            w.Stock[nul, (int)Res.Municion] = 20;
            To(w, D + NetConst.Hours(18) + 1);
            check(!Planned(w, nul), "el día 1 a las 18:00 aún no hay aviso");
            To(w, D + NetConst.Hours(19) + 1);
            var uw = w.UndefendedWarnings();
            check(Planned(w, nul) && uw.Exists(x => x.Node == nul && x.Text == "La horda va hacia Nules (llega hacia las 22:00) y no hay tropa"), "a las 19:00 se anuncia y sale el aviso ámbar de zona sin tropa");
            To(w, D + NetConst.Hours(22) + NetConst.FieldEvery + 1);
            check(w.Besieged(nul) && w.SiegeZombies(nul) == 2400, $"a las 22:00 Nules está sitiada, con 2400 ({w.SiegeZombies(nul)})");
            ulong hNight = w.Hash();
            var b = NetSave.Save(w); var l = NetScenario.Inicio(); NetSave.Load(b, l);
            check(l.Hash() == hNight, "se guarda y se carga en mitad del asedio");
            To(w, 2 * D + NetConst.Hours(6) + 1);
            var r = w.LastZoneSiege;
            check(r.Node == nul && !r.Lost && !r.OnScreen && r.Killed == 2400 && r.SpentU >= 5 && r.SpentU <= 7 && w.Stock[nul, (int)Res.Municion] == 20 - r.SpentU && !w.NodeLost[nul] && !w.Besieged(nul),
                $"con 20 u, Nules aguanta: abate las 2400 y gasta {r.SpentU} u (unas 6, Hugo)");
            check(w.Intel.Exists(e => e.Id == "ZS-1"), "parte «Nules aguanta la noche»");

            var z = NetScenario.Inicio(); To(z, D + NetConst.Hours(19) + 1); z.Stock[nul, (int)Res.Municion] = 0; To(z, 2 * D + NetConst.Hours(6) + 1);
            check(z.LastZoneSiege.Lost && z.NodeLost[nul] && z.Stock[nul, (int)Res.Personas] == 0 && z.Intel.Exists(e => e.Id == "ZS-2"), "con 0 u, Nules se pierde: nodo perdido, sin gente y parte «Hemos perdido Nules»");
            To(z, 2 * D + NetConst.Hours(8) + NetConst.FieldEvery + 1);
            check(z.Graph.Nodes[nul].State == NodeState.Perdida && z.Besieged(nul), "y sigue perdido (el campo no lo deshace): los trenes esperan a sus puertas");
            To(z, 2 * D + NetConst.Hours(20));
            check(!Planned(z, nul), "a un nodo perdido no vuelve su horda");
            var bz = NetSave.Save(z); var lz = NetScenario.Inicio(); NetSave.Load(bz, lz);
            check(lz.Hash() == z.Hash() && lz.NodeLost[nul], "se guarda perdido (NetVersion 5)");

            var s3 = NetScenario.Inicio(); s3.ActiveZoneNode = nul; To(s3, D + NetConst.Hours(19) + 1); s3.Stock[nul, (int)Res.Municion] = 0; To(s3, 2 * D + NetConst.Hours(6) + 1);
            check(s3.LastZoneSiege.OnScreen && !s3.NodeLost[nul] && s3.Stock[nul, (int)Res.Municion] == 0 && !s3.Intel.Exists(e => e.Id == "ZS-2"), "con la zona de Nules en pantalla no se aplica: manda NightSim");
            check(NetSave.NetVersion == 5, "la versión de la red sube a 5");
            check(sag >= 0 && !w.NodeLost[sag], "");
        }
    }
}
