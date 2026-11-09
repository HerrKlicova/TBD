using System;
using System.Collections.Generic;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>[40] Huecos del informe (Lucía, 9-nov): racha de la memoria de tráfico (tren y días seguidos), memoria de atención, vehículos por estación y pelotones de casa.</summary>
    public static class InformeRedTests
    {
        static int Id(NetWorld w, string n) { return w.Graph.Nodes.FindIndex(x => x.Name == n); }
        static void To(NetWorld w, long tick) { while (w.Tick < tick) { w.ApplyCommands(); w.Step(); } }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[40] Informe: racha de tráfico, atención, vehículos y pelotones de casa");
            long D = NetConst.DayTicks, H9 = NetConst.Hours(9);
            var m = new TrafficMemory(2, TrafficParams.Default);
            for (int d = 0; d < 4; d++) { if (d > 0) m.Decay(null); m.OnPass(0, d * D + H9 + 5, 7); }
            var l = m.Learned(3 * D + H9 + 10);
            check(l.Count == 1 && l[0].Edge == 0 && l[0].TrainId == 7 && l[0].Days == 4 && l[0].Hour == 9 && l[0].Alive, $"el tren 7 pasa 4 días seguidos a las 09:00: aprendido, racha de {(l.Count > 0 ? l[0].Days : -1)} días, hora {(l.Count > 0 ? l[0].Hour : -1)}");
            m.OnPass(0, 3 * D + H9 + 50, 8);
            check(m.Learned(3 * D + H9 + 60)[0].TrainId == 7, "otro tren el mismo día en la misma franja no rompe la racha");
            m.Decay(null); m.OnPass(0, 4 * D + H9 + 5, 8);
            var l2 = m.Learned(4 * D + H9 + 10);
            check(l2[0].TrainId == 8 && l2[0].Days == 1, "al día siguiente pasa el tren 8: la racha empieza de nuevo con él (1 día)");
            m.Decay(null); m.Decay(null);
            var l3 = m.Learned(6 * D + H9);
            check(l3.Count == 1 && !l3[0].Alive && l3[0].Days == 1, "dos días sin pasar: la franja sigue aprendida, pero la racha ya no sigue");

            var w = NetScenario.Inicio(); int sag = Id(w, "Sagunto"), nul = Id(w, "Nules");
            var f = w.IntelForcesAt(sag);
            check(f.Pelotones == 5 && f.Dragon && f.Vamtac == 2, $"Sagunto (casa) en Inicio: {f.Pelotones} pelotones, Dragón y {f.Vamtac} VAMTAC (plantel D-N173)");
            var fn = w.IntelForcesAt(nul);
            check(fn.Pelotones == 0 && !fn.Dragon && fn.Vamtac == 0, "Nules en Inicio: sin tropa de la red (la guarnición abstracta no cuenta)");
            w.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Peloton, Id = 3, Service = 0, Node = -1, Estado = PasajeroEstado.ABordo });
            w.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Vehiculo, Id = 0, Service = 0, Node = nul, Estado = PasajeroEstado.EnEstacion });
            w.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Peloton, Id = 4, Service = 0, Node = nul, Estado = PasajeroEstado.EnEstacion });
            f = w.IntelForcesAt(sag); fn = w.IntelForcesAt(nul);
            check(f.Pelotones == 3 && !f.Dragon && f.Vamtac == 2 && fn.Pelotones == 1 && fn.Dragon && fn.Vamtac == 0, $"un pelotón a bordo y otro y el Dragón bajados en Nules: Sagunto {f.Pelotones} pelotones y {f.Vamtac} VAMTAC; Nules {fn.Pelotones} pelotón y el Dragón");
            w.Embarque.Lista.Clear();

            int s0 = -1; for (int i = 0; i < w.Services.Count; i++) if (w.Services[i].Train != null) { s0 = i; break; }
            check(s0 >= 0, "Inicio tiene algún tren");
            if (s0 >= 0)
            {
                int tid = w.Services[s0].Train.Id;
                for (int d = 0; d < 3; d++) { if (d > 0) w.Traffic.Decay(null); w.Traffic.OnPass(0, d * D + H9 + 5, tid); }
                var it = w.IntelTraffic();
                check(it.Count == 1 && it[0].Tren == "Tren " + w.Services[s0].Train.Name && it[0].Dias == 3 && it[0].Hora == "09:00" && it[0].Tramo.Contains("–"), $"línea del informe: «{(it.Count > 0 ? it[0].Tramo + ": el " + it[0].Tren + " pasa a la misma hora " + it[0].Dias + " días seguidos" : "-")}» (hacia las {(it.Count > 0 ? it[0].Hora : "-")})");
                ulong h = w.Hash(); var b = NetSave.Save(w); var r = NetScenario.Inicio(); NetSave.Load(b, r);
                var ir = r.IntelTraffic();
                check(r.Hash() == h && ir.Count == 1 && ir[0].Dias == 3 && ir[0].Tren == it[0].Tren, "las rachas se guardan y se cargan (NetVersion 7) y van en el hash");
                var u = NetScenario.Inicio(); ulong hu = u.Hash(); u.Traffic.OnPass(0, H9, tid);
                var v = NetScenario.Inicio(); v.Traffic.OnPass(0, H9, tid); v.Traffic.StreakDays[0 * v.Traffic.P.Slots + v.Traffic.SlotOf(H9)] = 9;
                check(u.Hash() != v.Hash(), "una racha distinta cambia el hash");
            }
            check(NetSave.NetVersion >= 7, "la red va por la versión 7 o más (7: rachas de la memoria de tráfico)");

            var a = NetScenario.Inicio(); long d1 = (a.Tick / D + 1) * D; To(a, d1 + 1); a.ActiveZoneNode = sag; To(a, d1 + NetConst.Hours(7) + 1);   // el día empieza limpio a medianoche
            a.IntelAttention(out int foco, out var olv);
            check(foco == sag && !olv.Contains(sag), $"tras 7 h mirando Sagunto, el foco es Sagunto y no está entre los olvidados ({olv.Count} olvidados)");
        }
    }
}
