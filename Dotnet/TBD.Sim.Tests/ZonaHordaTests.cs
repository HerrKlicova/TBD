using System;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>[37] Hordas de zona (Lucía, 8-nov 23:20): Nules recibe su propia horda (2200 desde el 9-nov) desde la 2.ª noche; fuera de pantalla la resuelve la regla abstracta S0, con 300 zombis por u en Nules y el tope de fuego de 110 por defensor (Hugo, 9-nov).</summary>
    public static class ZonaHordaTests
    {
        static int Id(NetWorld w, string n) { return w.Graph.Nodes.FindIndex(x => x.Name == n); }
        static void To(NetWorld w, long tick) { while (w.Tick < tick) { w.ApplyCommands(); w.Step(); } }
        static void Land(NetWorld w, int n, int id) { w.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Peloton, Id = id, Service = 0, Node = n, Estado = PasajeroEstado.EnEstacion }); }   // un pelotón bajado allí (D-N179: el que manda el jugador)
        static bool Planned(NetWorld w, int n) { foreach (var p in w.Director.Active) if (HiveDirector.IsZoneSiege(p) && p.Node == n) return true; return false; }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[37] Hordas de zona: Nules desde la 2.ª noche, regla abstracta fuera de pantalla");
            var w = NetScenario.Inicio(); int nul = Id(w, "Nules"), sag = Id(w, "Sagunto");
            check(w.Director.ZoneHordes.Count == 1 && w.Director.ZoneHordes[0].Node == nul && w.Director.ZoneHordes[0].Zombies == 2200 && w.Director.ZoneHordes[0].FromDay == 1, "Inicio: horda propia de Nules, 2200, desde el día 1 (2.ª noche)");
            check(w.ZoneZombiesPerUNode[nul] == 300 && w.ZoneZombiesPerUNode[sag] == 400 && w.ZoneFireCapPerDefender == 110, $"Nules abate 300 por u (fila propia), Sagunto 400 (la general), tope de fuego 110 por defensor");
            long D = NetConst.DayTicks;
            To(w, NetConst.Hours(23)); bool n1 = Planned(w, nul) || w.Besieged(nul);
            check(!n1, "la 1.ª noche, Nules sin asedio (D-N156)");
            w.Stock[nul, (int)Res.Municion] = 20;
            To(w, D + NetConst.Hours(18) + 1);
            check(!Planned(w, nul), "el día 1 a las 18:00 aún no hay aviso");
            To(w, D + NetConst.Hours(19) + 1);
            var uw = w.UndefendedWarnings();
            check(Planned(w, nul) && uw.Exists(x => x.Node == nul && x.Text == "La horda va hacia Nules (llega hacia las 22:00) y no hay tropa"), "a las 19:00 se anuncia y sale el aviso ámbar de zona sin tropa");
            Land(w, nul, 50);
            To(w, D + NetConst.Hours(22) + NetConst.FieldEvery + 1);
            check(w.Besieged(nul) && w.SiegeZombies(nul) == 2200 && w.ZoneDefenders(nul) == 20, $"a las 22:00 Nules está sitiada, con 2200 ({w.SiegeZombies(nul)}), y la defienden 12 + 8 = {w.ZoneDefenders(nul)}");
            ulong hNight = w.Hash();
            var b = NetSave.Save(w); var l = NetScenario.Inicio(); NetSave.Load(b, l);
            check(l.Hash() == hNight, "se guarda y se carga en mitad del asedio");
            To(w, 2 * D + NetConst.Hours(6) + 1);
            var r = w.LastZoneSiege;
            check(r.Node == nul && !r.Lost && !r.OnScreen && r.Killed == 2200 && r.SpentU == 8 && r.Casualties == 0 && w.GarrisonMil[nul] == 12000 && w.Stock[nul, (int)Res.Municion] == 20 - r.SpentU && !w.NodeLost[nul] && !w.Besieged(nul),
                $"D-N179 cumplido (guarnición + 1 pelotón y 20 u): Nules aguanta, abate las 2200 y gasta {r.SpentU} u (300 por u)");
            var o = NetScenario.Inicio(); To(o, D + NetConst.Hours(19) + 1); o.Stock[nul, (int)Res.Municion] = 20; To(o, 2 * D + NetConst.Hours(6) + 1);
            check(o.LastZoneSiege.Lost && o.NodeLost[nul] && o.LastZoneSiege.Killed == 1320 && o.LastZoneSiege.SpentU == 5, $"solo la guarnición (12) con 20 u: 2200 pasa del tope (12 × 110 = 1320) y Nules cae aunque sobre munición; abate {o.LastZoneSiege.Killed} y gasta {o.LastZoneSiege.SpentU} u");
            check(w.Intel.Exists(e => e.Id == "ZS-1"), "parte «Nules aguanta la noche»");

            var z = NetScenario.Inicio(); To(z, D + NetConst.Hours(19) + 1); z.Stock[nul, (int)Res.Municion] = 0; To(z, 2 * D + NetConst.Hours(6) + 1);
            check(z.LastZoneSiege.Lost && z.NodeLost[nul] && z.GarrisonMil[nul] == 0 && z.WallU(nul) == 0 && z.Stock[nul, (int)Res.Viveres] == 0 && z.Intel.Exists(e => e.Id == "ZS-2"), $"con 0 u, Nules se pierde (2400 en pie: el muro baja {z.LastZoneSiege.DamageU} u y la guarnición a 2,4): sin gente, sin muro, sin existencias y parte «Hemos perdido Nules»");
            To(z, 2 * D + NetConst.Hours(8) + NetConst.FieldEvery + 1);
            check(z.Graph.Nodes[nul].State == NodeState.Perdida && z.Besieged(nul), "y sigue perdido (el campo no lo deshace): los trenes esperan a sus puertas");
            To(z, 2 * D + NetConst.Hours(20));
            check(!Planned(z, nul), "a un nodo perdido no vuelve su horda");
            var bz = NetSave.Save(z); var lz = NetScenario.Inicio(); NetSave.Load(bz, lz);
            check(lz.Hash() == z.Hash() && lz.NodeLost[nul], "se guarda perdido (NetVersion 5)");

            var s3 = NetScenario.Inicio(); s3.ActiveZoneNode = nul; To(s3, D + NetConst.Hours(19) + 1); s3.Stock[nul, (int)Res.Municion] = 0; To(s3, 2 * D + NetConst.Hours(6) + 1);
            check(s3.LastZoneSiege.OnScreen && !s3.NodeLost[nul] && s3.Stock[nul, (int)Res.Municion] == 0 && !s3.Intel.Exists(e => e.Id == "ZS-2"), "con la zona de Nules en pantalla no se aplica: manda NightSim");
            check(NetSave.NetVersion >= 5, "la versión de la red es 5 o más (5: hordas de zona)");
            check(sag >= 0 && !w.NodeLost[sag], "");
            var e = NetScenario.Inicio(); long wu = e.WallU(nul);
            check(wu == 124, $"el recinto de Nules en Inicio: {wu} u (Lucía: 124,3)");
            var f3 = NetScenario.Inicio(); To(f3, D + NetConst.Hours(19) + 1); Land(f3, nul, 50); f3.Stock[nul, (int)Res.Municion] = 5; To(f3, 2 * D + NetConst.Hours(6) + 1);
            check(!f3.LastZoneSiege.Lost && f3.LastZoneSiege.Killed == 5 * 300 && f3.GarrisonMil[nul] == 12000 - (2200 - 1500) * 4, $"con el pelotón y 5 u: abate 1500 (300 por u), quedan 700 en pie, el muro baja {f3.LastZoneSiege.DamageU} u y la guarnición pierde 2,8: aguanta con {f3.GarrisonMil[nul] / 1000.0:0.00}");
            var f2 = NetScenario.Inicio(); To(f2, D + NetConst.Hours(19) + 1); Land(f2, nul, 50); f2.Stock[nul, (int)Res.Municion] = 2; To(f2, 2 * D + NetConst.Hours(6) + 1);
            check(f2.LastZoneSiege.Lost && f2.LastZoneSiege.Killed == 600, $"con el pelotón y 2 u ya no aguanta: abate 600, quedan 1600 y la guarnición baja de la mitad");
        }
    }
}
