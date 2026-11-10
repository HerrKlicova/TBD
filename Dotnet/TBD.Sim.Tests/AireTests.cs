using System;
using TBD.Net;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>[42] F1 paso 2 (plan del núcleo): los NH90 de Bétera dentro de NetWorld. Ticks de red, órdenes de la cola, hash y guardado.</summary>
    static class AireTests
    {
        static void Cmd(NetWorld w, NetCmdType t, int arg, int arg2 = 0, int[] pl = null) { w.Push(new NetCommand { Tick = w.Tick, Type = t, Arg = arg, Arg2 = arg2, Payload = pl }); }
        static void Both(NetWorld a, NetWorld b, NetCmdType t, int arg, int arg2 = 0, int[] pl = null) { Cmd(a, t, arg, arg2, pl); Cmd(b, t, arg, arg2, pl); }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[42] F1: los NH90 de Bétera en la red");
            var a = NetScenario.Inicio(); var b = NetScenario.Inicio();
            int sag = TrenPreparar.Nodo(a, "Sagunto");
            check(a.Air != null && a.Air.Craft.Count == 2 && a.Air.PlatoonAt.Length == 2, "Inicio pone Bétera en la red: 2 NH90 y 2 pelotones de embarque");
            check(a.Air.Hash() == 0, "sin usar el aire, el aire suma 0 al hash (los goldens siguen valiendo)");
            int ft = AirWing.TicksFor(18_100_000);
            check(ft == 74, $"Bétera–Sagunto, 18,1 km a 260 km/h: {ft} ticks de vuelo (2,5 s reales a ×1)");
            while (a.Tick < NetConst.Hours(8)) { a.Step(); b.Step(); }
            int stock0 = a.Stock[sag, (int)Res.Municion], ammo0 = a.Air.AmmoMilliU, fuel0 = a.Air.FuelMilliU;
            long t0 = a.Tick;
            Both(a, b, NetCmdType.AirSend, 0 | 1 << 8, sag);                                                  // NH90 1 a Sagunto con pelotón
            int fx = 52 * AirWing.MmPerCell, fy = 70 * AirWing.MmPerCell;
            Both(a, b, NetCmdType.AirSend, 1, -1, new[] { fx, fy });                                        // NH90 2 a un punto libre, sin pelotón
            a.Step(); b.Step();
            var c0 = a.Air.Craft[0]; var c1 = a.Air.Craft[1];
            check(c0.Phase == AirPhase.Preparando && c0.Platoon == 0 && a.Air.PlatoonAt[0] == AirWing.Aboard, "AirSend: el NH90 1 prepara la salida con el pelotón 1 a bordo");
            check(a.Air.FuelMilliU == fuel0 - 6000 && a.Air.AmmoMilliU == ammo0 - 6000, $"dos salidas gastan 6 u de combustible y cargan 6 u de munición (combustible {a.Air.FuelMilliU / 1000} u, munición {a.Air.AmmoMilliU / 1000} u)");
            bool same = true; long llegada = -1, mitad = -1; byte[] guardado = null; ulong hGuardado = 0;
            for (int k = 0; k < 2000; k++)
            {
                a.Step(); b.Step(); if (a.Hash() != b.Hash()) same = false;
                if (c0.Phase == AirPhase.EnZona && llegada < 0) llegada = a.Tick;
                if (c1.Phase == AirPhase.Ida && mitad < 0 && a.Tick - c1.PhaseStartTick == c1.FlightTicks / 2) { mitad = a.Tick; guardado = NetSave.Save(a); hGuardado = a.Hash(); }
            }
            check(llegada - t0 == AirWing.PrepTicks + ft + 1, $"el NH90 1 llega a Sagunto {llegada - t0} ticks después de la orden (600 de preparar + {ft} de vuelo + el de la orden)");
            check(a.Stock[sag, (int)Res.Municion] == stock0 + 3, $"D-N190: al llegar deja 3 u en la reserva de Sagunto ({stock0} → {a.Stock[sag, (int)Res.Municion]})");
            check(c1.Phase == AirPhase.EnZona && c1.DestNode == -1 && c1.CargoMilliU == 3000, "el NH90 2 está en estacionario en el punto libre y conserva su carga");
            check(same, "dos mundos con la misma cola dan el mismo hash en cada tick con los dos aparatos volando");
            {
                var r = NetScenario.Inicio(); NetSave.Load(guardado, r);
                check(r.Hash() == hGuardado && r.Air.Craft[1].Phase == AirPhase.Ida, $"guardar a mitad de vuelo y cargar da el mismo hash (tick {mitad})");
                var r2 = NetScenario.Inicio(); NetSave.Load(guardado, r2); bool s2 = true; for (int k = 0; k < 400; k++) { r.Step(); r2.Step(); if (r.Hash() != r2.Hash()) s2 = false; }
                check(s2 && r.Air.Craft[1].Phase == AirPhase.EnZona, "la partida cargada sigue igual que otra cargada del mismo punto y el NH90 2 llega");
            }
            Both(a, b, NetCmdType.AirDrop, 0); a.Step(); b.Step();
            check(a.Air.PlatoonAt[0] == sag && c0.Platoon == -1, "AirDrop: el pelotón 1 baja en Sagunto");
            Both(a, b, NetCmdType.AirPickup, 1, 0); a.Step(); b.Step();
            check(a.Air.PlatoonAt[0] == sag, "AirPickup desde otro sitio no recoge al pelotón (el NH90 2 no está en Sagunto)");
            Both(a, b, NetCmdType.AirAmmo, 0, 250); a.Step(); b.Step();
            check(c0.Phase == AirPhase.Volviendo, "con el 25 % de munición (< 30 %) la red manda volver al NH90 1 sin orden del jugador");
            int ammoBase = a.Air.AmmoMilliU; long vuelta = a.Tick;
            while (c0.Phase != AirPhase.EnBase && a.Tick - vuelta < 5000) { a.Step(); b.Step(); if (a.Hash() != b.Hash()) same = false; }
            check(c0.Phase == AirPhase.EnBase && a.Tick - vuelta == ft + AirWing.LandTicks + AirWing.RearmTicks, $"vuelve, toma tierra y rearma en {a.Tick - vuelta} ticks ({ft} + 600 + 900)");
            check(c0.AmmoPm == 1000 && a.Air.AmmoMilliU == ammoBase - 1350, $"el rearme del 75 % gasta 1,35 u de Bétera (quedan {a.Air.AmmoMilliU / 1000.0:0.00} u) y deja el aparato al 100 %");
            Both(a, b, NetCmdType.AirReturn, 1); a.Step(); b.Step();
            check(c1.Phase == AirPhase.Volviendo, "AirReturn: el NH90 2 vuelve desde el punto libre");
            while (c1.Phase != AirPhase.EnBase && a.Tick < vuelta + 10000) { a.Step(); b.Step(); if (a.Hash() != b.Hash()) same = false; }
            check(c1.Phase == AirPhase.EnBase && same, "el NH90 2 está en la base y A y B siguen iguales");
            {   // D-N188: el cerco de Castellón en la red
                var w = NetScenario.Inicio(); int cas = TrenPreparar.Nodo(w, "Castellón"); ulong h0 = w.Hash();
                check(w.Cercada(cas) && w.CercoZombies == 4400 && w.SiegeZombies(cas) == 4400, $"D-N188: Castellón empieza cercada por {w.CercoZombies} zombis");
                check(!w.CercoSocorroAbierto, "el socorro no se abre el día 1");
                Cmd(w, NetCmdType.CercoBajas, cas, 1000); w.Step();
                check(w.CercoZombies == 3400 && w.Hash() != h0, "la zona informa de 1000 bajas: quedan 3400 y el hash lo recoge");
                while (w.Tick % NetConst.DayTicks != 0) w.Step();
                check(w.CercoZombies == 3550, $"al cambiar de día se rehace 150 ({w.CercoZombies})");
                var bytes = NetSave.Save(w); var r = NetScenario.Inicio(); NetSave.Load(bytes, r);
                check(r.CercoZombies == 3550 && r.Hash() == w.Hash(), "guardar y cargar conserva el cerco");
                while (w.Tick < 2L * NetConst.DayTicks + 10) w.Step();
                check(w.CercoSocorroAbierto, $"el día 3 ({NetConst.Clock(w.Tick)}) el socorro ya se puede abrir");
                Cmd(w, NetCmdType.CercoBajas, cas, w.CercoZombies - 499); w.Step();
                check(!w.Cercada(cas) && w.CercoZombies == 0, "por debajo de 500 el cerco se rompe: Castellón deja de estar cercada (ya admite horario)");
                int z0 = w.CercoZombies; while (w.Tick % NetConst.DayTicks != 0) w.Step(); w.Step();
                check(w.CercoZombies == z0, "roto, ya no se rehace");
            }
            {   // D-N188: el socorro (preguntas de Iker, I-Noche135)
                var w = NetScenario.Inicio(); int cas = TrenPreparar.Nodo(w, "Castellón"), sg = TrenPreparar.Nodo(w, "Sagunto");
                int sv = -1; for (int k = 0; k < w.Services.Count; k++) if (w.Services[k].AtStop && w.Services[k].Stops.Count > 0 && w.Services[k].Stops[w.Services[k].StopIndex].Node == sg) { sv = k; break; }
                int stride = 2 + NetConst.ResCount; var data = new int[2 * stride]; data[0] = sg; data[1] = (int)NetConst.Hours(9); data[stride] = cas; data[stride + 1] = (int)NetConst.Hours(14);
                check(sv >= 0 && w.SetSchedule(sv, (int[])data.Clone()) == ScheduleResult.Cercada, "día 1: un horario con parada en Castellón se rechaza (Cercada)");
                while (w.Tick < 2L * NetConst.DayTicks + NetConst.Hours(6)) w.Step();
                sv = -1; for (int k = 0; k < w.Services.Count; k++) if (w.Services[k].AtStop && w.Services[k].Stops.Count > 0 && w.Services[k].Stops[w.Services[k].StopIndex].Node == sg && !w.Services[k].Ambushed) { sv = k; break; }
                var rr = sv >= 0 ? w.SetSchedule(sv, (int[])data.Clone()) : ScheduleResult.TrenInexistente;
                check(rr != ScheduleResult.Cercada, $"día 3: el socorro ya admite Castellón ({rr})");
                check(w.Besieged(cas), "el cerco sitia: el tren de socorro espera a las puertas y solo baja la tropa (D-N166)");
                Cmd(w, NetCmdType.Materialize, cas); w.Step();
                int fieldBefore = 0; foreach (var d in w.Field.D) fieldBefore += d; foreach (var d in w.Field.S) fieldBefore += d;
                check(w.ActiveZoneNode == cas && w.ZoneSpawnPermil == w.ActiveZoneTaken + 440, $"al entrar en Castellón la zona saca la celda más el cerco ({w.ZoneSpawnPermil} ‰, 440 del cerco)");
                Cmd(w, NetCmdType.CercoBajas, cas, 400); w.Step();
                Cmd(w, NetCmdType.Dematerialize, cas, w.ActiveZoneTaken + 400); w.Step();
                int fieldAfter = 0; foreach (var d in w.Field.D) fieldAfter += d; foreach (var d in w.Field.S) fieldAfter += d;
                check(w.CercoZombies == 4000 && Math.Abs(fieldAfter - fieldBefore - w.ActiveZoneTaken) <= 50, $"al salir, los 4000 del cerco siguen siendo cerco y no vuelven a la mancha (mancha {fieldBefore} → {fieldAfter})");
            }
            {   // turbo: a ×4 la red da 4 pasos por fotograma, los mismos ticks: el vuelo dura los mismos ticks
                var w = NetScenario.Inicio(); Cmd(w, NetCmdType.SetSpeed, 4); w.Step(); long s = w.Tick; Cmd(w, NetCmdType.AirSend, 0, sag); int g = 0; while (w.Air.Craft[0].Phase != AirPhase.EnZona && g++ < 3000) w.Step();
                check(w.Tick - s == AirWing.PrepTicks + ft + 1, $"con ×4 la salida dura los mismos ticks ({w.Tick - s}): una cuarta parte del tiempo real");
                Cmd(w, NetCmdType.AirSend, 0, sag); w.Step();
                check(w.Air.LastRefusal == "No está en la base", "una salida de un aparato que no está en la base se rechaza y la red dice por qué");
            }
        }
    }
}
