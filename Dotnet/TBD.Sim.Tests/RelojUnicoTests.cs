using System;
using TBD.Net;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>[41] Reloj único (opción 1 de Sara, Lucía b4c2fe5; D-N186): la noche de la zona va con el reloj de la red. Empieza en el tick de las 22:00,
    /// solo la avanza NetWorld.Step (en orden de nodo, tras las órdenes) y entra en el hash de la red.</summary>
    static class RelojUnicoTests
    {
        static NightSim Sagunto(bool netClock)
        {
            var sim = NightSim.FromJson(BalanceSource.Read("noche_sagunto.json"), "normal", 3);
            var p = sim.Night.P; p.NetClock = netClock; sim.Night.P = p;
            return sim;
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[41] Reloj único: la noche de la zona con el reloj de la red");
            check(NightGame.NetHourTicks == NetConst.HourTicks, $"NightGame usa {NightGame.NetHourTicks} ticks por hora, los mismos que la red ({NetConst.HourTicks})");
            {
                var s = Sagunto(true); var g = s.Night;
                check(g.TotalTicks == 8 * NetConst.HourTicks, $"con el reloj de la red, de 22:00 a 06:00 son {g.TotalTicks} ticks (8 × {NetConst.HourTicks})");
                check(g.TicksForGameSeconds(10 * 60) == 175, $"10 min de juego son {g.TicksForGameSeconds(600)} ticks (conversión exacta: 175)");
                var o = Sagunto(false).Night;
                check(o.TotalTicks == 8L * 3600 * IMath.TickRate / o.P.ClockX, $"sin el reloj de la red no cambia nada: {o.TotalTicks} ticks con reloj_x {o.P.ClockX}");
            }
            {   // la red empieza y avanza la noche; dos mundos con la misma cola dan el mismo hash en cada tick
                var a = NetScenario.Inicio(); var b = NetScenario.Inicio();
                int sag = TrenPreparar.Nodo(a, "Sagunto");
                ulong h0 = a.Hash();
                var sa = Sagunto(true); var sb = Sagunto(true);
                a.NochesZona[sag] = sa; b.NochesZona[sag] = sb;
                long start = NetWorld.NightStartTick(1);
                bool idle = true, same = true; int steps = 0;
                while (a.Tick < start) { a.Step(); b.Step(); steps++; if (sa.Night.Tick != 0 || sa.Night.StartNetTick >= 0) idle = false; if (steps % 997 == 0 && a.Hash() != b.Hash()) same = false; }
                check(idle && a.Tick == start, $"antes de las 22:00 del día 1 la noche no da ningún paso (tick de red {a.Tick} = {NetConst.Clock(a.Tick)})");
                a.Step(); b.Step();
                check(sa.Night.StartNetTick == start && sa.Night.Tick == 1, $"en el tick de las 22:00 la noche empieza (StartNetTick {sa.Night.StartNetTick}) y da su primer paso (Tick {sa.Night.Tick})");
                bool lock1 = true;
                for (int k = 0; k < 1500; k++) { a.Step(); b.Step(); if (sa.Night.State == NightState.Jugando && sa.Night.Tick != a.Tick - sa.Night.StartNetTick) lock1 = false; if (a.Hash() != b.Hash()) same = false; }
                check(lock1, $"la noche va pegada a la red: Tick de la noche = tick de red − StartNetTick ({sa.Night.Tick} = {a.Tick} − {sa.Night.StartNetTick}); hora {sa.Night.Hour:00}:{sa.Night.Minute:00} y red {NetConst.Clock(a.Tick)}");
                check(same && sa.Hash() == sb.Hash(), $"dos mundos con la noche registrada dan el mismo hash en cada tick ({sa.ZombiesAlive} zombis vivos en la zona)");
                check(sa.Night.Hour * 60 + sa.Night.Minute == (int)(a.Tick % NetConst.DayTicks * 24 * 60 / NetConst.DayTicks) % (24 * 60) || Math.Abs((sa.Night.Hour * 60 + sa.Night.Minute) - (int)(a.Tick % NetConst.DayTicks * 1440 / NetConst.DayTicks)) <= 1,
                      $"la hora de la zona es la de la red ({sa.Night.Hour:00}:{sa.Night.Minute:00} y {NetConst.Clock(a.Tick)})");
                var c = NetScenario.Inicio(); check(c.Hash() == h0, "sin noches registradas el hash de la red no cambia (los goldens siguen valiendo)");
                // pausa: no avanza
                int nt = sa.Night.Tick; a.Push(new NetCommand { Tick = a.Tick, Type = NetCmdType.Pause }); for (int k = 0; k < 50; k++) a.Step();
                check(sa.Night.Tick == nt, $"con la red en pausa la noche no avanza (Tick {sa.Night.Tick})");
                // guardado de la noche
                var sw = new SaveWriter(); sa.Night.SaveState(sw); var gl = new NightGame(sa.Night.P, 0); gl.LoadState(new SaveReader(sw.ToArray()));
                check(gl.StartNetTick == sa.Night.StartNetTick && gl.Hash() == sa.Night.Hash(), $"guardar y cargar la noche conserva StartNetTick ({gl.StartNetTick}) y el hash");
            }
            {   // ×1 automático en el primer contacto (Lucía, D-N186)
                var w = NetScenario.Inicio(); int sag = TrenPreparar.Nodo(w, "Sagunto"); var s = Sagunto(true); w.NochesZona[sag] = s;
                while (w.Tick < NetWorld.NightStartTick(1) - 5) w.Step();
                w.Push(new NetCommand { Tick = w.Tick, Type = NetCmdType.SetSpeed, Arg = 4 });
                int guard = 0; while (w.ContactAtTick < 0 && guard++ < 8400) w.Step();
                check(w.ContactNode == sag && s.ContactTick > 0 && w.Speed == 1, $"primer contacto de la horda con Sagunto a las {NetConst.Clock(w.ContactAtTick)} (tick {s.ContactTick} de la noche): la red baja de ×4 a ×{w.Speed}");
                w.Push(new NetCommand { Tick = w.Tick, Type = NetCmdType.SetSpeed, Arg = 4 }); for (int k = 0; k < 300; k++) w.Step();
                check(w.Speed == 4, "solo el primer contacto de la noche baja a ×1: si el jugador vuelve a ×4, se queda");
            }
            {   // caso de Marcos (I-112): entrar en Nules a las 09:00 del día 2 no arranca la noche; es de día hasta las 22:00 y el tren y su carga no cambian
                var w = NetScenario.Inicio(); var r = NetScenario.Inicio(); int nul = TrenPreparar.Nodo(w, "Nules");
                long nine = NetConst.DayTicks + 9L * NetConst.HourTicks;
                while (w.Tick < nine) { w.Step(); r.Step(); }
                var ns = Sagunto(true); w.NochesZona[nul] = ns;          // la zona de Nules se registra al entrar (de día)
                long before22 = NetWorld.NightStartTick(2) - 1; bool day = true;
                while (w.Tick < before22) { w.Step(); r.Step(); if (ns.Night.StartNetTick >= 0 || ns.Night.Tick != 0 || ns.Spawned != 0 || ns.ZombiesAlive != 0) day = false; }
                bool cargo = w.Services.Count == r.Services.Count; for (int i = 0; cargo && i < w.Services.Count; i++) for (int k = 0; k < NetConst.ResCount; k++) if (w.Services[i].Cargo[k] != r.Services[i].Cargo[k]) cargo = false;
                bool trains = true; for (int i = 0; trains && i < w.Services.Count; i++) if (w.Services[i].Train.OffsetMm != r.Services[i].Train.OffsetMm || w.Services[i].Destroyed != r.Services[i].Destroyed) trains = false;
                check(day, $"entrar en Nules a las 09:00 del día 2: hasta las 21:59 la noche no empieza (StartNetTick {ns.Night.StartNetTick}, Tick {ns.Night.Tick}, {ns.Spawned} zombis nacidos)");
                check(cargo && trains, "y los trenes y su carga quedan igual que en una partida sin la zona registrada");
                w.Step(); w.Step();
                check(ns.Night.StartNetTick == NetWorld.NightStartTick(2), $"a las 22:00 del día 2 empieza la noche de Nules (Defiende Nules), tick {ns.Night.StartNetTick}");
            }
            {   // plan del escenario: Nules solo el día 2; numeración de noches por el plan
                var w = NetScenario.Inicio(); int sag = TrenPreparar.Nodo(w, "Sagunto"), nul = TrenPreparar.Nodo(w, "Nules");
                check(w.HasNight(sag, 1) && w.HasNight(sag, 5) && !w.HasNight(nul, 1) && w.HasNight(nul, 2) && !w.HasNight(nul, 3), "plan de Inicio: Sagunto todas las noches; Nules solo la del día 2 (Defiende Nules)");
                check(w.NightNumber(sag, 3) == 3 && w.NightNumber(nul, 2) == 1, $"numeración: el día 3 es la noche {w.NightNumber(sag, 3)} de Sagunto y el día 2 la {w.NightNumber(nul, 2)} de Nules");
                check(NetWorld.NightStartTick(2) == NetConst.DayTicks + 22L * NetConst.HourTicks, "las 22:00 del día 2 son DayTicks + 22 h");
            }
        }
    }
}
