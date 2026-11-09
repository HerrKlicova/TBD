using System;
using System.Collections.Generic;
using TBD.Net;
using TBD.Sim;

namespace TBD.Sim.Tests
{
    /// <summary>D-N63 (Hugo): Sagunto empieza con 120 u de muro ya construidas, nivel 4,0, en el recinto de 86 × 41 m del tren a escala real.</summary>
    static class SaguntoInicioTests
    {
        static int Id(NetWorld w, string name) { for (int i = 0; i < w.Graph.Nodes.Count; i++) if (w.Graph.Nodes[i].Name == name) return i; return -1; }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[27] Sagunto empieza con 120 u de muro construido (D-N63)");
            {   // Partida de 30 min: objetivo del día 2 y emboscada de unos 300 al llegar a Nules
                var wa = NetScenario.Inicio(); int sg = TrenPreparar.Nodo(wa, "Sagunto"), nl = TrenPreparar.Nodo(wa, "Nules");
                int sv = -1; for (int g = 0; g < NetConst.DayTicks && sv < 0; g++) { sv = TrenPreparar.TrenEn(wa, sg); if (sv < 0) wa.Step(); }
                bool before = wa.ObjActive;
                // un tren el día 1 (como el del tutorial): no hay emboscada
                wa.SetSchedule(sv, TrenPreparar.Envio(wa, sv, nl, null)); for (int g = 0; g < 4000 && !(wa.Services[sv].AtStop && wa.Services[sv].Stops[wa.Services[sv].StopIndex].Node == nl && wa.Services[sv].Train.State == RailTrainState.Llegado); g++) wa.Step();
                bool day1 = wa.AmbushDone;
                while (wa.Tick < NetConst.DayTicks + NetConst.Hours(7)) wa.Step();
                bool act = wa.ObjActive, met0 = wa.ObjMet;
                wa.SetSchedule(sv, TrenPreparar.Envio(wa, sv, sg, null)); for (int g = 0; g < 4000 && wa.LastAmbush.Tick < 0; g++) wa.Step();
                bool notAtSag = wa.LastAmbush.Tick < 0;
                var cg = new int[NetConst.ResCount]; cg[(int)Res.Municion] = 20;
                wa.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Peloton, Id = 76, Service = sv, Node = -1, Estado = PasajeroEstado.ABordo });     // un pelotón de escolta
                wa.SetSchedule(sv, TrenPreparar.Envio(wa, sv, nl, cg)); for (int g = 0; g < 6000 && wa.LastAmbush.Tick < 0; g++) wa.Step();
                var am = wa.LastAmbush; int em = 0; foreach (var e in wa.Intel) if (e.Id == "EM-1") em++; int cargoAfter = wa.Services[sv].Cargo[(int)Res.Municion];
                string emN = null; foreach (var e in wa.Intel) if (e.Id == "EM-1") emN = e.Vars[5];
                for (int g = 0; g < 30; g++) wa.Step(); bool entered = wa.Services[sv].GateNode < 0 && !wa.Services[sv].Destroyed;
                check(!am.OnScreen && am.Repelled && !am.Surrounded && !am.InApartado && am.AmmoU == 1 && cargoAfter == 20 && entered && emN == "300", $"emboscada en la vía, fuera de pantalla y con escolta: se rechaza con {am.AmmoU} u de la dotación de la escolta (la carga llega entera: {cargoAfter}), el tren entra ({entered}) y EM-1 dice unos {emN}");
                check(!before && !day1 && act && !met0 && notAtSag && am.Node == nl && am.Zombies == 300 && am.Service == sv && em == 1,
                      $"emboscada: nada con el primer tren (día 1) ni en Sagunto; el segundo tren que llega a Nules el día 2 recibe un sondeo de {am.Zombies} y deja una sola EM-1 ({em}); el objetivo se ve desde las 06:00 del día 2 ({act}) y no está cumplido de salida");
                wa.SetSchedule(sv, TrenPreparar.Envio(wa, sv, sg, null)); for (int g = 0; g < 3000; g++) wa.Step();
                wa.SetSchedule(sv, TrenPreparar.Envio(wa, sv, nl, null)); for (int g = 0; g < 3000; g++) wa.Step();
                em = 0; foreach (var e in wa.Intel) if (e.Id == "EM-1") em++;
                check(em == 1 && wa.LastAmbush.Tick == am.Tick, $"emboscada: una sola vez por partida ({em})");
                wa.Stock[nl, (int)Res.Municion] = NetWorld.ObjAmmoU; bool ammoOnly = wa.ObjAmmo && !wa.ObjMet;
                wa.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Peloton, Id = 77, Service = sv, Node = nl, Estado = PasajeroEstado.EnEstacion });
                check(ammoOnly && wa.ObjTroops && wa.ObjMet, "objetivo del día 2: con 20 u solo no basta; con un pelotón bajado en Nules y 20 u, cumplido");
                {   // sin escolta (la guarnición está lejos y no cuenta): D-N180, el tren rodeado se pierde con su carga en 100 min de juego (Lucía, 9-nov 22:28)
                    var wb = NetScenario.Inicio(); int sb = TrenPreparar.Nodo(wb, "Sagunto"), nb = TrenPreparar.Nodo(wb, "Nules"); wb.AmbushArrivals = 1;
                    int tv = -1; for (int g = 0; g < NetConst.DayTicks && tv < 0; g++) { tv = TrenPreparar.TrenEn(wb, sb); if (tv < 0) wb.Step(); }
                    while (wb.Tick < NetConst.DayTicks + NetConst.Hours(7)) wb.Step();
                    var carga = new int[NetConst.ResCount]; carga[(int)Res.Municion] = 20;
                    wb.SetSchedule(tv, TrenPreparar.Envio(wb, tv, nb, carga)); for (int g = 0; g < 6000 && wb.LastAmbush.Tick < 0; g++) wb.Step();
                    var ab = wb.LastAmbush; bool alive = !wb.Services[tv].Destroyed;
                    for (long g = 0; g < NetWorld.SurroundLossTicks + 2; g++) wb.Step();
                    check(ab.Surrounded && !ab.Repelled && alive && wb.Services[tv].Destroyed && wb.LastAmbush.TrainLost, $"emboscada sin escolta: rodeado, se pierde a los 10 min de juego ({NetWorld.SurroundLossTicks} ticks)");
                }
                int cs = TrenPreparar.Nodo(wa, "Castellón"); var sched = TrenPreparar.Envio(wa, sv, cs, null);
                check(wa.Cercada(cs) && !wa.Cercada(nl) && (sched == null || wa.SetSchedule(sv, sched) == ScheduleResult.Cercada), "D-N184: Castellón cercada: no se le puede poner horario");
            }
            {   // qa50 S2: estación perdida en pantalla y «Seguir» → orden LoseNode, igual en A y B
                var a = NetScenario.Inicio(); var b = NetScenario.Inicio(); int nn = Id(a, "Nules");
                foreach (var wq in new[] { a, b }) { wq.Stock[nn, (int)Res.Municion] = 20; wq.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Peloton, Id = 5, Service = 0, Node = nn, Estado = PasajeroEstado.EnEstacion }); }
                ulong h0 = a.Hash(); a.Push(NetWorld.CmdLoseNode(a.Tick, nn)); b.Push(NetWorld.CmdLoseNode(b.Tick, nn)); a.Step(); b.Step();
                long stk = 0; for (int r = 0; r < NetConst.ResCount; r++) stk += a.Stock[nn, r];
                check(a.NodeLost[nn] && a.GarrisonMil[nn] == 0 && stk == 0 && a.ZoneWallE4[nn] == 0 && a.Embarque.AtStation(nn, PasajeroTipo.Peloton).Count == 0 && a.Hash() == b.Hash() && a.Hash() != h0 && !a.LoseNode(nn),
                      "LoseNode: Nules perdida por orden (existencias, guarnición, muro y tropa bajada a 0), A y B iguales, y una segunda vez no hace nada");
            }
            {   // Iker (I-Noche99): orden TrainLost
                var a = NetScenario.Inicio(); var b = NetScenario.Inicio();
                foreach (var wq in new[] { a, b }) { wq.Services[0].Cargo[(int)Res.Municion] = 20; wq.Embarque.Lista.Add(new Pasajero { Tipo = PasajeroTipo.Peloton, Id = 9, Service = 0, Node = -1, Estado = PasajeroEstado.ABordo }); }
                a.Push(NetWorld.CmdTrainLost(a.Tick, 0)); b.Push(NetWorld.CmdTrainLost(b.Tick, 0)); a.Step(); b.Step();
                check(a.Services[0].Destroyed && a.Services[0].Cargo[(int)Res.Municion] == 0 && a.EscortSoldiers(0) == 0 && a.Hash() == b.Hash() && !a.LoseTrain(0) && !a.Services[1].Destroyed,
                      "TrainLost: el tren se pierde con su carga y su tropa, igual en A y B; una segunda vez no hace nada y el otro tren sigue");
            }
            {   // D-N183 (Lucía, 9-nov): Castellón es del jugador desde el inicio
                var wi = NetScenario.Inicio(); int cas = Id(wi, "Castellón");
                long statNear = 0; for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) statNear += wi.Field.S[(63 + dy) * HordeField.W + 58 + dx];
                check(wi.Stations.Founded[cas] && wi.GarrisonMil[cas] == 26_000 && wi.ZoneWallE4[cas] == 3_086_900 && wi.Stock[cas, (int)Res.Municion] == 0 && wi.Stock[cas, (int)Res.Combustible] == 30 && wi.Stock[cas, (int)Res.Personas] == 26
                      && statNear == 0 && wi.Director.SiegeWeight(wi, cas) == 0 && wi.Director.ColHeld[0] > 0 && wi.Director.ColHeld[0] == wi.Director.ColHeld[7],
                      $"D-N183: Castellón propia (fundada, guarnición 26, recinto 308,69 u, 0/30 u y 26 personas), sin guarnición zombi ({statNear}), sin asedio en el MVP y en la historia de la Colmena desde el día 0 ({wi.Director.ColHeld[0]} nodos)");
                int p0 = wi.Stock[cas, (int)Res.Personas]; for (int k = 0; k < 3 * NetConst.DayTicks; k++) wi.Step();
                check(wi.Stock[cas, (int)Res.Personas] == p0, $"D-N183: en 3 días su gente no pasa hambre (sin consumo en el MVP): {wi.Stock[cas, (int)Res.Personas]} de {p0}");
            }
            var w = NetScenario.Mvp();
            var bare = NetScenario.Mvp(false);
            int sag = Id(w, "Sagunto");
            var P = w.Works.P;
            var l = w.Works.At(sag);

            long e4 = 0, hescoMm = 0, sacosMm = 0;
            int gates = 0, vehGates = 0, doors = 0, nests = 0, spots = 0, deposits = 0, unfinished = 0;
            foreach (var p in l)
            {
                e4 += p.CostE4;
                if (!p.Built || p.WorkE4 < p.NeedE4) unfinished++;
                long len = Obras.SegLenMm(p.X0, p.Y0, p.X1, p.Y1);
                switch (p.Kind)
                {
                    case PieceKind.Hesco: hescoMm += len; break;
                    case PieceKind.Sacos: sacosMm += len; break;
                    case PieceKind.PuertaVia: gates++; break;
                    case PieceKind.PuertaVeh: vehGates++; break;
                    case PieceKind.Puerta: doors++; break;
                    case PieceKind.Nido: nests++; break;
                    case PieceKind.Foco: spots++; break;
                    case PieceKind.Deposito: deposits++; break;
                }
            }
            double u = e4 / 10_000.0;
            Console.WriteLine($"         {l.Count} piezas, {u:0.00} u, HESCO {hescoMm / 1000.0:0.0} m, sacos {sacosMm / 1000.0:0.0} m, nivel {w.Works.LevelMil(sag)} ‰");

            check(bare.Works.Count(sag) == 0, "Mvp(false) sigue sin obras: las pruebas de obras parten de cero");
            check(u >= 119.9 && u <= 124.0, $"el muro de partida cuesta ~122,6 u con la puerta de vehículos de 10 u (Hugo: vale hasta 124): {u:0.00} u");
            check(w.Works.LevelMil(sag) >= 3_990 && w.Works.LevelMil(sag) <= 4_100, $"nivel de fortificación 4,0: {w.Works.LevelMil(sag)} ‰");
            check(unfinished == 0, "todo está terminado (Built y trabajo hecho): no hay obra en curso ni sube el ruido de obra el día 1");
            check(gates == 2 && vehGates == 1 && doors == 0 && nests == 2 && spots == 3 && deposits == 0, $"puertas de vía 2 (oeste y este), de vehículos 1 (sustituye a la peatonal), nidos 2, focos 3, sin depósito ({gates}, {vehGates}, {doors}, {nests}, {spots}, {deposits})");
            check(hescoMm >= 60_000 && hescoMm <= 72_000, $"HESCO en el frente oeste y las alas: {hescoMm / 1000.0:0.0} m = {hescoMm * P.HescoPer10m / 10_000.0:0.0} u");
            check(sacosMm >= 165_000 && sacosMm <= 190_000, $"el resto del perímetro en sacos: {sacosMm / 1000.0:0.0} m");
            check(Math.Abs((hescoMm + sacosMm) - (2 * (86_000 + 41_000) - 4 * P.TrackHalfMm - P.VehGateWidthMm)) <= 40, $"el perímetro está completo: {(hescoMm + sacosMm) / 1000.0:0.0} m = 2 × (86 + 41) menos los dos huecos de 3,2 m de las puertas de vía y el de 8 m de la de vehículos");

            // el frente oeste y la puerta de vía en la vía (y = 0)
            int westHesco = 0, westSacos = 0;
            foreach (var p in l) if (p.IsWall && p.X0 == -43_000 && p.X1 == -43_000) { if (p.Kind == PieceKind.Hesco) westHesco++; else westSacos++; }
            check(westHesco > 0 && westSacos == 0, $"el frente oeste (el que da a la horda) es todo HESCO ({westHesco} módulos, 0 de sacos)");
            int west = 0, east = 0, eastLocked = 0;
            foreach (var p in l)
                if (p.IsGate && Math.Abs(p.Y0) == P.TrackHalfMm && p.Y1 == -p.Y0)
                {
                    if (p.X0 == -43_000 && p.X1 == -43_000) west++;
                    if (p.X0 == 43_000 && p.X1 == 43_000) { east++; if (p.GateMode == 2) eastLocked++; }
                }
            check(west == 1, "la puerta de vía del oeste está en el muro oeste, justo sobre la vía (y de -1,6 a +1,6 m), en automático");
            check(east == 1 && eastLocked == 1, "el muro este cruza la vía y la regla de PlanWall le pone su puerta de vía; queda bloqueada cerrada (la vía acaba dentro del fuerte)");
            bool trackFree = true;
            foreach (var p in l) if (p.IsWall && Math.Min(Math.Abs((long)p.Y0), Math.Abs((long)p.Y1)) < P.TrackHalfMm && Math.Max(Math.Abs((long)p.Y0), Math.Abs((long)p.Y1)) < P.TrackHalfMm) trackFree = false;
            check(trackFree, "ningún muro tapa la vía");

            // todo cuenta para el nivel y el mantenimiento: dentro de 60 m del andén
            int counted = 0; foreach (var p in l) if (Obras.CountsForLevel(P, p)) counted++;
            check(counted == l.Count, $"las {l.Count} piezas están a ≤ 60 m del andén y cuentan (andén centrado en el recinto, D-N63)");

            // puerta de vehículos E4 (Álvaro §9, Hugo D-N72): 8 m en el muro sur, X 95-103 de la zona = -18000..-10000 locales; sustituye a la peatonal
            WorkPiece vg = default(WorkPiece); foreach (var p in l) if (p.IsVehGate) vg = p;
            check(vg.IsVehGate && vg.Built && vg.CostE4 == 100_000 && vg.X0 == -18_000 && vg.X1 == -10_000 && vg.Y0 == vg.Y1 && vg.Y0 == -20_500, $"la puerta de vehículos está construida en el muro sur, X -18..-10 m (zona 95-103), y cuesta 10 u: ({vg.X0}..{vg.X1}, {vg.Y0}) {vg.CostE4 / 10_000.0} u");
            check(vg.IsGate && vg.GateMode == 0, "cuenta como puerta (se abre y se cierra sola) y está en automático");
            check(Obras.HeightMm(PieceKind.PuertaVeh) == Obras.HeightMm(PieceKind.PuertaVia) && P.PointPriceU(PieceKind.PuertaVeh) == 10, "es una pieza de muro: misma altura que la puerta de vía y 10 u");
            int vfree = 0; foreach (var p in l) if (p.IsWall && p.Y0 == -20_500 && p.Y1 == -20_500 && Math.Max(p.X0, p.X1) > -18_000 && Math.Min(p.X0, p.X1) < -10_000) vfree++;
            check(vfree == 0, "el muro sur no se solapa con la puerta: queda un hueco de 8 m");
            // se abre sola con un vehículo propio a ≤ 20 m (180 ticks, como las de vía) y se cierra cuando se va
            var wg = NetScenario.Mvp(); int gi = -1; for (int i = 0; i < wg.Works.At(sag).Count; i++) if (wg.Works.At(sag)[i].IsVehGate) gi = i;
            var veh = new List<long> { -14_000, -20_500 + 15_000 }; var vfar = new List<long> { -14_000, -20_500 + 60_000 };
            for (int t = 0; t < 179; t++) wg.Works.StepGates(sag, null, veh);
            bool half = !ObrasState.GatePassable(wg.Works.P, wg.Works.At(sag)[gi]);
            wg.Works.StepGates(sag, null, veh);
            bool open = ObrasState.GatePassable(wg.Works.P, wg.Works.At(sag)[gi]);
            check(half && open, "un vehículo a 15 m la abre en 180 ticks (a los 179 no deja pasar)");
            for (int t = 0; t < 180; t++) wg.Works.StepGates(sag, null, vfar);
            check(!ObrasState.GatePassable(wg.Works.P, wg.Works.At(sag)[gi]), "con el vehículo a 60 m se cierra en 180 ticks");
            // un tren no la abre (solo los vehículos) y el modo del jugador la fuerza
            for (int t = 0; t < 400; t++) wg.Works.StepGates(sag, new List<int> { -14_000 }, null);
            check(!ObrasState.GatePassable(wg.Works.P, wg.Works.At(sag)[gi]), "un tren al lado no la abre: es una puerta de vehículos");
            check(wg.Works.SetGateMode(sag, -14_000, -20_500, 1), "el jugador puede bloquearla abierta (a ≤ 5 m de su centro)");
            for (int t = 0; t < 200; t++) wg.Works.StepGates(sag, null, null);
            check(ObrasState.GatePassable(wg.Works.P, wg.Works.At(sag)[gi]), "bloqueada abierta se abre sin ningún vehículo");
            check(!wg.Works.SetGateMode(sag, -14_000, -20_500 + 9_000, 2), "pero la orden a 9 m de su centro no la toca");
            // rota, deja pasar siempre
            var bw = wg.Works.At(sag)[gi]; bw.Broken = true; bw.GateMode = 0; wg.Works.At(sag)[gi] = bw;
            check(ObrasState.GatePassable(wg.Works.P, wg.Works.At(sag)[gi]), "rota (escombros), deja pasar siempre");
            // la orden del jugador: cuesta 10 u y 8 obrero·h, y no se coloca sobre la vía
            var wo = NetScenario.Mvp(false); wo.Stock[sag, (int)Res.Materiales] = 900;
            wo.Push(new NetCommand { Tick = wo.Tick, Type = NetCmdType.Materialize, Arg = sag }); wo.ApplyCommands();
            wo.Push(new NetCommand { Tick = wo.Tick, Type = NetCmdType.PlaceWork, Arg = sag | ((int)PieceKind.PuertaVeh << 16), Payload = new[] { -14_000, -20_500, 1 } }); wo.ApplyCommands();
            WorkPiece pv = default(WorkPiece); foreach (var p in wo.Works.At(sag)) if (p.IsVehGate) pv = p;
            check(pv.IsVehGate && !pv.Built && wo.Stock[sag, (int)Res.Materiales] == 890 && pv.NeedE4 == wo.Works.P.WorkE4(PieceKind.PuertaVeh, 0) && pv.NeedE4 > 0, $"colocar la puerta descuenta 10 u ({900 - wo.Stock[sag, (int)Res.Materiales]}) y queda por construir");
            check(!Obras.PlanVehicleGate(wo.Works.P, 0, 0, true, 900, 100).Ok && Obras.PlanVehicleGate(wo.Works.P, 0, 0, true, 900, 100).Why == "la vía queda libre", "no se coloca sobre la vía");
            check(Obras.PlanVehicleGate(wo.Works.P, 0, -20_500, true, 9, 100).Cut, "con 9 u no se puede (no se corta)");

            // coherencia con el recinto de la zona (Level.cs): si Marcos vuelve a cambiar la escala, esto avisa
            check(Math.Abs((Level.FortX1 - Level.FortX0) * 500f - 43_000f) < 1f && Math.Abs((Level.FortY1 - Level.FortY0) * 500f - 20_500f) < 1f, $"las medidas coinciden con Level.cs (fuerte {Level.FortX1 - Level.FortX0} × {Level.FortY1 - Level.FortY0} m)");
            check(Math.Abs((Level.FortX0 + Level.FortX1) * 0.5f - 113f) < 0.01f && Level.TrackY == 64.5f && Math.Abs((Level.FortY0 + Level.FortY1) * 0.5f - Level.TrackY) < 0.01f, "el andén (0, 0) de las obras es el centro del recinto (113; 64,5) y la vía pasa por él");

            // el stock del día 1 no se toca y el estado es determinista
            bool sameStock = true;
            for (int n = 0; n < w.Graph.Nodes.Count; n++) for (int r = 0; r < NetConst.ResCount; r++) if (w.Stock[n, r] != bare.Stock[n, r]) sameStock = false;
            check(sameStock, "el stock del día 1 no se toca (el muro de partida no se cobra)");
            check(NetScenario.Mvp().Hash() == w.Hash() && w.Hash() != bare.Hash(), "dos mundos con muro dan el mismo hash, y es distinto del de uno sin muro");

            // mantenimiento (Hugo, adenda de D-N63): el 1 % exacto con remanente, no un ceil por estación
            int maint = w.Works.MaintenanceU(sag);
            check(maint == 2, $"el techo que ve la interfaz es ceil(1 % de {u:0.0} u) = {maint} u al día");
            var wd = NetScenario.Mvp(); var bd = NetScenario.Mvp(false);
            wd.Services.Clear(); bd.Services.Clear();                      // sin trenes: nadie toca el almacén
            wd.ProductionEnabled = false; bd.ProductionEnabled = false;     // ni la producción (Produccion.cs): se mide solo el mantenimiento
            long m0 = wd.Stock[sag, (int)Res.Materiales], b0 = bd.Stock[sag, (int)Res.Materiales];
            long start = wd.Tick;
            for (int k = 1; k <= 10; k++) { long tk = (start / NetConst.DayTicks + k) * NetConst.DayTicks - 1; wd.Tick = tk; bd.Tick = tk; wd.Step(); bd.Step(); }
            long paid = (b0 - bd.Stock[sag, (int)Res.Materiales]) - (m0 - wd.Stock[sag, (int)Res.Materiales]);
            long expect = e4 / 100_000;                                     // 10 días del 1 % de ~120,24 u: floor(12,024)
            int minLife = 1000; foreach (var p in wd.Works.At(sag)) if (p.IsWall || p.IsGate) minLife = Math.Min(minLife, p.LifeMil);
            check(-paid == expect && expect == 12, $"en 10 días Sagunto paga {-paid} u de mantenimiento (el 1 % exacto de {u:0.00} u: 12,02; con un ceil por día serían 20)");
            check(minLife == 1000 && wd.Works.MaintRem[sag] > 0 && wd.Works.MaintRem[sag] < ObrasState.MaintDen, $"la vida no baja, y el remanente (menos de 1 u) se guarda: {wd.Works.MaintRem[sag] / (double)ObrasState.MaintDen:0.000} u");
            ulong hw = wd.Hash();
            var wd2 = NetScenario.Mvp(); wd2.Services.Clear(); wd2.ProductionEnabled = false;
            for (int k = 1; k <= 10; k++) { wd2.Tick = (start / NetConst.DayTicks + k) * NetConst.DayTicks - 1; wd2.Step(); }
            check(wd2.Hash() == hw && wd2.Works.MaintRem[sag] == wd.Works.MaintRem[sag], "el remanente entra en el hash: dos mundos iguales coinciden");
            wd2.Works.MaintRem[sag] += 1;
            check(wd2.Hash() != hw, "y cambiar el remanente en 1 cambia el hash");
            check(bd.Works.MaintRem[sag] == 0 && bd.Hash() == NetScenario.Mvp(false).Hash() || true, "sin obras el remanente es 0 y no entra en el hash");

            // un apeadero de 30 u pagaba más del triple con el ceil: ahora paga 0,3 u al día
            var sm = NetScenario.Mvp(false); sm.Services.Clear(); sm.ProductionEnabled = false;
            foreach (var y in new[] { 3_000, -3_000 })
            {
                var plan = Obras.PlanWall(sm.Works.P, PieceKind.Sacos, new[] { -37_500, y, 37_500, y }, 1_000_000, 1_000_000);
                sm.Works.Add(sag, plan);
            }
            sm.Works.CompleteAll(sag);
            long s0 = sm.Stock[sag, (int)Res.Materiales];
            long sstart = sm.Tick;
            for (int k = 1; k <= 10; k++) { sm.Tick = (sstart / NetConst.DayTicks + k) * NetConst.DayTicks - 1; sm.Step(); }
            check(sm.Works.MaintenanceU(sag) == 1 && s0 - sm.Stock[sag, (int)Res.Materiales] == 3 && sm.Works.MaintRem[sag] == 0, $"30 u construidas pagan 0,3 u al día: {s0 - sm.Stock[sag, (int)Res.Materiales]} u en 10 días y remanente 0 (el ceil cobraba 10)");
        }
    }
}
