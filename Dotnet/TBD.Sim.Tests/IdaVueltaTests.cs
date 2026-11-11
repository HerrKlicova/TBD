using System;
using System.Collections.Generic;
using TBD.Net;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>[44] Salir y volver de una zona sin desincronizar (Lucía, pulido-hasta-19, encargo 3 de Nacho; la corrida en Unity, con Iker, va después).
    /// Nules: la noche registrada en la red (A y B) sale a la zona, la zona la avanza, y al salir vuelve como dos copias por SaveState/LoadState (lo mismo que
    /// PrototypeBootstrap.CopiaNoche). Castellón: entrar y salir de la zona cercada (Materialize/Dematerialize) con el cerco de la D-N188.</summary>
    static class IdaVueltaTests
    {
        /// <summary>Copia de PrototypeBootstrap.CopiaNoche (vista de Unity), con la misma receta.</summary>
        public static NightSim Copia(NightSim s)
        {
            var w1 = new SaveWriter(); s.Night.SaveState(w1); var ng = new NightGame(s.Night.P, 0); ng.LoadState(new SaveReader(w1.ToArray()));
            var w2 = new SaveWriter(); s.Units.SaveState(w2); var uw = new UnitsWorld(0); uw.LoadState(new SaveReader(w2.ToArray()));
            foreach (var o in s.Units.Obstacles) uw.Obstacles.Add(o);
            foreach (var lb in s.Units.LosBlockers) uw.LosBlockers.Add(lb);
            uw.GrenadeDeconflict = s.Units.GrenadeDeconflict;
            for (int i = 0; i < uw.Platoons.Count && i < s.Units.Platoons.Count; i++) uw.Platoons[i].EyeMm = s.Units.Platoons[i].EyeMm;
            var c = new NightSim(ng, uw, s.SP, s.WallHp) { NextNightParams = s.NextNightParams };
            var z = new List<Target>(); s.ExportZombies(z); foreach (var t in z) c.ImportZombie(t.X, t.Y);
            foreach (var v in s.Vehicles) { var nv = NightVehicle.Make(v.Name, v.Kind, v.X, v.Y); foreach (var f in typeof(NightVehicle).GetFields()) if (!f.IsInitOnly && !f.IsLiteral) f.SetValue(nv, f.GetValue(v)); c.Vehicles.Add(nv); }
            c.VehicleParamsJson = s.VehicleParamsJson; c.Kills = s.Kills; c.SoldierDeaths = s.SoldierDeaths; c.Resupplies = s.Resupplies; c.PeakPile = s.PeakPile; c.Spawned = s.Spawned; c.Climbed = s.Climbed; c.MaterialsU = s.MaterialsU; c.FuelU = s.FuelU;
            if (s.Bot != null) { c.Bot = NightBot.FromParams(c.SP); foreach (var id in s.Bot.Busy) c.Bot.Busy.Add(id); }
            return c;
        }

        static NightSim Nules()
        {
            var s = NightSim.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(BalanceTests.DataDirPublic(), "..", "..", "StreamingAssets", "Noche", "nules.json")), "normal", 77);
            if (s.JsonForce != null) s.AddZoneForce(); else s.AddDefaultForce();
            s.Bot = NightBot.FromParams(s.SP);
            var p = s.Night.P; p.NetClock = true; s.Night.P = p;
            return Copia(s);      // como RegistrarNochesIniciales: lo que entra en la red es siempre una copia
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[44] Salir y volver de una zona: A y B siguen iguales (Nules y Castellón)");
            {
                var a = NetScenario.Inicio(); var b = NetScenario.Inicio(); int nul = TrenPreparar.Nodo(a, "Nules");
                var origen = Nules(); a.NochesZona[nul] = Copia(origen); b.NochesZona[nul] = Copia(origen);
                bool same = true; long firstBad = -1;
                void Paso(int n) { for (int k = 0; k < n; k++) { a.Step(); b.Step(); if (same && a.Hash() != b.Hash()) { same = false; firstBad = a.Tick; } } }
                Paso((int)(NetWorld.NightStartTick(2) + 1500 - a.Tick));
                var enRed = (NightSim)a.NochesZona[nul];
                check(same && enRed.Night.StartNetTick == NetWorld.NightStartTick(2), $"Nules: hasta las 22:00 del día 2 y 1500 ticks de noche, A y B iguales ({enRed.Spawned} zombis nacidos, {enRed.ZombiesAlive} vivos)");
                for (int vuelta = 1; vuelta <= 2; vuelta++)
                {
                    // entrar: la noche sale de la red (A y B) y la lleva la zona, un paso por tick de red
                    var zona = (NightSim)a.NochesZona[nul]; a.NochesZona.Remove(nul); b.NochesZona.Remove(nul);
                    for (int k = 0; k < 600; k++) { a.Step(); b.Step(); zona.Step(); if (same && a.Hash() != b.Hash()) { same = false; firstBad = a.Tick; } }
                    // salir: dos copias iguales vuelven a la red
                    var ca = Copia(zona); var cb = Copia(zona);
                    check(ca.Hash() == cb.Hash(), $"Nules, salida {vuelta}: las dos copias (A y B) tienen el mismo hash");
                    check(ca.Night.Hash() == zona.Night.Hash() && ca.ZombiesAlive == zona.ZombiesAlive && ca.Units.Platoons.Count == zona.Units.Platoons.Count,
                          $"Nules, salida {vuelta}: la copia conserva la noche (tick {ca.Night.Tick}, {ca.ZombiesAlive} zombis, {ca.Units.Platoons.Count} pelotones)");
                    a.NochesZona[nul] = ca; b.NochesZona[nul] = cb;
                    Paso(900);
                    check(same, $"Nules, vuelta {vuelta}: 900 ticks de red después de salir, A y B siguen iguales{(same ? "" : $" (se separan en el tick {firstBad})")}");
                }
            }
            {
                var a = NetScenario.Inicio(); var b = NetScenario.Inicio(); var c = NetScenario.Inicio(); int cas = TrenPreparar.Nodo(a, "Castellón");
                if (cas < 0) cas = TrenPreparar.Nodo(a, "Castellon");
                for (int k = 0; k < 2000; k++) { a.Step(); b.Step(); c.Step(); }
                int cerco0 = a.CercoZombies; bool cercada = a.CercoNodo == cas;
                foreach (var w in new[] { a, b }) w.Push(new NetCommand { Tick = w.Tick, Type = NetCmdType.Materialize, Arg = cas });
                a.Step(); b.Step(); c.Step();
                int toma = a.ZoneSpawnPermil;
                check(a.ActiveZoneNode == cas && toma == b.ZoneSpawnPermil && (!cercada || toma >= cerco0 / NetWorld.ZombiesPerPermil), $"Castellón: al entrar, la zona saca {toma} ‰ (cerco {(cercada ? cerco0 + " zombis" : "no")}), igual en A y B");
                bool same = true;
                for (int k = 0; k < 600; k++) { a.Step(); b.Step(); c.Step(); if (a.Hash() != b.Hash()) same = false; }
                foreach (var w in new[] { a, b }) w.Push(new NetCommand { Tick = w.Tick, Type = NetCmdType.Dematerialize, Arg = cas, Arg2 = toma });      // sale sin bajas: vuelve todo lo que sacó
                for (int k = 0; k < 600; k++) { a.Step(); b.Step(); c.Step(); if (a.Hash() != b.Hash()) same = false; }
                check(same && a.ActiveZoneNode < 0, "Castellón: entrar, 600 ticks y salir sin bajas: A y B iguales en cada tick y la zona queda libre");
                check(a.CercoZombies == c.CercoZombies && a.CercoNodo == c.CercoNodo, $"Castellón: sin bajas el cerco queda como en una partida sin entrar ({a.CercoZombies} y {c.CercoZombies} zombis)");
                foreach (var w in new[] { a, b }) w.Push(new NetCommand { Tick = w.Tick, Type = NetCmdType.Materialize, Arg = cas });
                a.Step(); b.Step();
                check(a.ZoneSpawnPermil == b.ZoneSpawnPermil && a.Hash() == b.Hash(), $"Castellón: al volver a entrar, A y B sacan lo mismo ({a.ZoneSpawnPermil} ‰)");
            }
            Console.WriteLine("\n[44b] NightBot: remate de los que quedan dentro del recinto (I-Noche174, fachada este de Nules)");
            {
                var bot = new NightBot(70_000, 44_000, 156_000, 85_000, 106_000, 79_000); var uw = new UnitsWorld(5);
                var oeste = uw.AddPlatoon(PlatoonKind.Fusileros, 73_000, 60_000, 0); var esc = uw.AddPlatoon(PlatoonKind.Fusileros, 150_000, 50_000, 0); bot.Busy.Add(esc.Id);
                var dentro = new List<Target> { new Target { Id = 1, X = 115_800, Y = 76_000 }, new Target { Id = 2, X = 115_800, Y = 78_000 }, new Target { Id = 3, X = 115_900, Y = 80_000 } };
                bot.Step(uw, dentro, 0);
                check(oeste.AttackMove && Math.Abs(oeste.TX - 115_800) < 7_000 && Math.Abs(oeste.TY - 78_000) < 7_000 && !esc.Moving && bot.InsideSweeps == 1,
                    $"con el muro tranquilo, el pelotón libre va a por los 3 de la fachada este (destino {oeste.TX / 1000};{oeste.TY / 1000}); la escolta ocupada no se mueve");
                var bot2 = new NightBot(70_000, 44_000, 156_000, 85_000, 106_000, 79_000); bot2.InsideStandOffMm = 0; var uw2 = new UnitsWorld(5); var o2 = uw2.AddPlatoon(PlatoonKind.Fusileros, 73_000, 60_000, 0);
                bot2.Step(uw2, dentro, 0); check(!o2.Moving, "con InsideStandOffMm = 0, como antes: sin amenaza de fuera nadie se mueve");
                var pila = new List<Target>(dentro); for (int i = 0; i < 40; i++) pila.Add(new Target { Id = 100 + i, X = 69_000, Y = 60_000 + i * 100 });
                var bot3 = new NightBot(70_000, 44_000, 156_000, 85_000, 106_000, 79_000); var uw3 = new UnitsWorld(5); var o3 = uw3.AddPlatoon(PlatoonKind.Fusileros, 73_000, 60_000, 0);
                bot3.Step(uw3, pila, 0); check(bot3.InsideSweeps == 0, "con pila en el muro (amenaza de fuera), el muro manda: no hay remate");
            }
        }
    }
}
