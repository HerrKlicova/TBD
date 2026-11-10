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
            {   // turbo: a ×4 la red da 4 pasos por fotograma, los mismos ticks: el vuelo dura los mismos ticks
                var w = NetScenario.Inicio(); Cmd(w, NetCmdType.SetSpeed, 4); w.Step(); long s = w.Tick; Cmd(w, NetCmdType.AirSend, 0, sag); int g = 0; while (w.Air.Craft[0].Phase != AirPhase.EnZona && g++ < 3000) w.Step();
                check(w.Tick - s == AirWing.PrepTicks + ft + 1, $"con ×4 la salida dura los mismos ticks ({w.Tick - s}): una cuarta parte del tiempo real");
                Cmd(w, NetCmdType.AirSend, 0, sag); w.Step();
                check(w.Air.LastRefusal == "No está en la base", "una salida de un aparato que no está en la base se rechaza y la red dice por qué");
            }
        }
    }
}
