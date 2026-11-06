using System;
using System.Collections.Generic;
using System.IO;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Hito 5, carril 1: Obras por nodo (coste exacto, corte por materiales, nivel en ‰, perímetro de 60 m, S0 = S2, hash y orden).</summary>
    static class ObrasTests
    {
        const int Node = 0;                       // Valencia en el escenario MVP: 900 u de materiales

        /// <summary>Mundo MVP sin vía para las pruebas de coste, nivel y obra, cuyos muros se trazan por el andén (y = 0): la franja de la vía se prueba en [21].</summary>
        static NetWorld Mvp0() { var w = NetScenario.Mvp(); w.Works.P.TrackHalfMm = 0; return w; }

        static NetWorld WorldWithZone(int materials = 900)
        {
            var w = Mvp0();
            w.Stock[Node, (int)Res.Materiales] = materials;
            w.Push(new NetCommand { Tick = w.Tick, Type = NetCmdType.Materialize, Arg = Node });
            w.ApplyCommands();
            return w;
        }

        static int[] Square(int half) { return new[] { -half, -half, half, -half, half, half, -half, half, -half, -half }; }   // perímetro cerrado de 8·half mm

        static NetCommand Place(long tick, PieceKind k, params int[] pts) { return new NetCommand { Tick = tick, Type = NetCmdType.PlaceWork, Arg = Node | ((int)k << 16), Payload = pts }; }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[15] Obras por nodo (hito 5): coste exacto, corte por materiales, nivel de fortificación, S0 = S2");

            // ---- los valores por defecto coinciden con el CSV de Hugo
            var csv = ObrasParams.FromCsv(new CsvTable(File.ReadAllText(Path.Combine(BalanceTests.DataDirPublic(), "balance_construccion.csv"))));
            var def = ObrasParams.Default;
            def.TrackHalfMm = 0; csv.TrackHalfMm = 0;      // sin vía: estas pruebas trazan muros por el andén
            check(csv.SacosPer10m == def.SacosPer10m && csv.HescoPer10m == def.HescoPer10m && csv.PuertaU == def.PuertaU && csv.NidoU == def.NidoU && csv.FocoU == def.FocoU && csv.AndenU == def.AndenU && csv.DepositoU == def.DepositoU,
                $"los precios por defecto coinciden con balance_construccion.csv (sacos {csv.SacosPer10m}, HESCO {csv.HescoPer10m}, puerta {csv.PuertaU}, nido {csv.NidoU}, foco {csv.FocoU}, andén {csv.AndenU}, depósito {csv.DepositoU} u)");
            var p = csv;

            // ---- coste exacto en mm enteros
            var s150 = Obras.PlanWall(p, PieceKind.Sacos, Square(18750), 1000, 2000);        // 4 × 37,5 m = 150 m
            var h150 = Obras.PlanWall(p, PieceKind.Hesco, Square(18750), 1000, 2000);
            check(s150.Ok && s150.AllowedMm == 150_000 && s150.CostU == 30 && s150.CostE4 == 300_000, $"150 m de sacos cuestan 30 u exactas (D-N44): {s150.AllowedMm} mm, {s150.CostU} u");
            check(h150.Ok && h150.CostU == 90, $"150 m de HESCO cuestan 90 u exactas: {h150.CostU} u");
            var s10 = Obras.PlanWall(p, PieceKind.Sacos, new[] { 0, 0, 10_000, 0 }, 100, 2000);
            check(s10.CostU == 2 && s10.Pieces.Count == 1, "10 m de sacos son 2 u y un módulo");
            var odd = Obras.PlanWall(p, PieceKind.Hesco, new[] { 0, 0, 3_000, 4_000 }, 100, 2000);          // 5 m en diagonal: 3-4-5
            check(odd.AllowedMm == 5_000 && odd.CostE4 == 6 * 5_000 && odd.CostU == 3, $"5 m de HESCO en diagonal 3-4-5: longitud entera {odd.AllowedMm} mm, coste {odd.CostE4} E4, se cobran {odd.CostU} u (hacia arriba)");
            long sumLen = 0; bool short10 = true;
            foreach (var m in s150.Pieces) { long l = Obras.SegLenMm(m.X0, m.Y0, m.X1, m.Y1); sumLen += l; if (l > p.ModuleMm) short10 = false; }
            check(sumLen == 150_000 && short10 && s150.Pieces.Count == 16, $"los módulos suman exactamente 150 m y ninguno pasa de 10 m ({s150.Pieces.Count} módulos)");
            var ladder = Obras.PlanWall(p, PieceKind.Sacos, new[] { 0, 0, 7_777, 1_234, 20_001, -999, 33_333, 12_345 }, 1000, 2000);
            long cost = 0; foreach (var m in ladder.Pieces) cost += m.CostE4;
            check(cost == ladder.CostE4 && cost == 2L * ladder.AllowedMm, "el coste de una polilínea irregular es precio × longitud, sin pérdidas por redondeo en los módulos");

            // ---- regla determinista de corte por falta de materiales
            var cut = Obras.PlanWall(p, PieceKind.Sacos, Square(18750), 25, 2000);
            check(cut.Ok && cut.Cut && cut.AllowedMm == 125_000 && cut.CostU == 25, $"con 25 u se corta a 125 m: {cut.AllowedMm} mm, {cut.CostU} u");
            var last = cut.Pieces[cut.Pieces.Count - 1];
            var cut2 = Obras.PlanWall(p, PieceKind.Sacos, Square(18750), 25, 2000);
            bool same = cut2.Pieces.Count == cut.Pieces.Count;
            for (int i = 0; same && i < cut.Pieces.Count; i++) same = cut.Pieces[i].X0 == cut2.Pieces[i].X0 && cut.Pieces[i].Y1 == cut2.Pieces[i].Y1 && cut.Pieces[i].CostE4 == cut2.Pieces[i].CostE4;
            check(same, $"el mismo muro con las mismas existencias se corta en el mismo punto (último módulo acaba en {last.X1},{last.Y1})");
            bool never = true, monotone = true; long prev = -1;
            for (int stock = 0; stock <= 120; stock++)
            {
                var pl = Obras.PlanWall(p, PieceKind.Hesco, new[] { 0, 0, 90_000, 0, 90_000, 77_000 }, stock, 2000);
                if (pl.Ok && pl.CostU > stock) never = false;
                if (pl.AllowedMm < prev) monotone = false;
                prev = pl.AllowedMm;
            }
            check(never && monotone, "el corte nunca cobra más de lo que hay y, con más materiales, nunca construye menos (0 a 120 u)");
            var none = Obras.PlanWall(p, PieceKind.Sacos, new[] { 0, 0, 50_000, 0 }, 0, 2000);
            check(!none.Ok && none.Pieces.Count == 0, "sin materiales no se construye nada");
            check(!Obras.PlanWall(p, PieceKind.Sacos, new[] { 0, 0, 1, 2, 3 }, 10, 2000).Ok && !Obras.PlanWall(p, PieceKind.Puerta, new[] { 0, 0, 5, 5 }, 10, 2000).Ok
                  && !Obras.PlanWall(p, PieceKind.Sacos, new[] { 0, 0, 3_000_000, 0 }, 10_000, 2000).Ok, "polilíneas impares, piezas que no son muro y coordenadas fuera de la zona se rechazan");
            check(!Obras.PlanPoint(p, PieceKind.Nido, 0, 0, 7, 2000).Ok && Obras.PlanPoint(p, PieceKind.Nido, 0, 0, 8, 2000).Ok, "un nido (8 u) no se corta: o hay 8 u o no se pone");

            // ---- la orden: el coste en directo es el de la orden, y solo con zona
            var wNoZone = Mvp0();
            wNoZone.Stock[Node, (int)Res.Materiales] = 900;
            wNoZone.Push(Place(wNoZone.Tick, PieceKind.Sacos, 0, 0, 20_000, 0)); wNoZone.ApplyCommands();
            check(wNoZone.Works.Count(Node) == 0 && wNoZone.Stock[Node, (int)Res.Materiales] == 900, "sin zona materializada en el nodo la orden no hace nada ni cobra");
            var w = WorldWithZone(25);
            var live = w.Works.Preview(Node, PieceKind.Sacos, Square(18750), w.Stock[Node, (int)Res.Materiales]);
            w.Push(Place(w.Tick, PieceKind.Sacos, Square(18750))); w.ApplyCommands();
            check(w.Works.Count(Node) == live.Pieces.Count && w.Stock[Node, (int)Res.Materiales] == 25 - live.CostU && live.CostU == 25, $"lo previsualizado es lo que se construye: {w.Works.Count(Node)} módulos, quedan {w.Stock[Node, (int)Res.Materiales]} u");

            // ---- nivel de fortificación: el ejemplo de Lucía (150 m de HESCO, puerta, 2 nidos y 3 focos = 118 u → nivel 3,9)
            var wl = WorldWithZone();
            wl.Push(Place(wl.Tick, PieceKind.Hesco, Square(18750)));
            wl.Push(Place(wl.Tick, PieceKind.Puerta, 0, -18750));
            wl.Push(Place(wl.Tick, PieceKind.Nido, 18750, 0)); wl.Push(Place(wl.Tick, PieceKind.Nido, -18750, 0));
            wl.Push(Place(wl.Tick, PieceKind.Foco, 5000, 5000)); wl.Push(Place(wl.Tick, PieceKind.Foco, -5000, 5000)); wl.Push(Place(wl.Tick, PieceKind.Foco, 0, -5000));
            wl.ApplyCommands();
            check(900 - wl.Stock[Node, (int)Res.Materiales] == 118, $"el perímetro del ejemplo cuesta 118 u ({900 - wl.Stock[Node, (int)Res.Materiales]})");
            check(wl.Works.LevelMil(Node) == 0, "una obra pagada pero sin terminar no sube el nivel");
            wl.Works.CompleteAll(Node);
            check(wl.Works.LevelMil(Node) == 3933, $"118 u dan nivel 3,933 (D-N44: «nivel 3,9»): {wl.Works.LevelMil(Node)} ‰");
            var snap = wl.Works.Snapshot(Node);
            for (int i = 0; i < snap.Count; i++) { var x = snap[i]; x.LifeMil = 500; snap[i] = x; }
            check(Obras.LevelMil(p, snap) == 1966, $"con la mitad de vida el nivel es la mitad: {Obras.LevelMil(p, snap)} ‰");

            // depósito, lejanía y tope
            var wd = WorldWithZone();
            wd.Push(Place(wd.Tick, PieceKind.Deposito, 1000, 1000)); wd.Push(Place(wd.Tick, PieceKind.Hesco, 70_000, 0, 150_000, 0)); wd.ApplyCommands(); wd.Works.CompleteAll(Node);
            check(wd.Works.Count(Node) == 1 + 8 && wd.Works.LevelMil(Node) == 0, "el depósito no cuenta y un muro a más de 60 m del andén tampoco (D-N43: «un muro suelto en una esquina no infla el nivel»)");
            var edge = new List<WorkPiece> { new WorkPiece { Kind = PieceKind.Hesco, X0 = 60_000, Y0 = 0, X1 = 60_000, Y1 = 0, CostE4 = 10_000, LifeMil = 1000, Built = true },
                                             new WorkPiece { Kind = PieceKind.Hesco, X0 = 60_001, Y0 = 0, X1 = 60_001, Y1 = 0, CostE4 = 10_000, LifeMil = 1000, Built = true } };
            check(Obras.LevelMil(p, edge) == 10_000 * 1000 / 300_000, "el borde del perímetro: a 60.000 mm cuenta, a 60.001 no");
            var big = new List<WorkPiece>(); for (int i = 0; i < 40; i++) big.Add(new WorkPiece { Kind = PieceKind.Hesco, CostE4 = 300_000, LifeMil = 1000, Built = true });
            check(Obras.LevelMil(p, big) == 5000, "el nivel tiene un tope de 5.000 ‰");

            // ---- S0 = S2: la zona recalcula lo construido desde la geometría y sale lo mismo que el nivel del S0
            var zone = wl.Works.Snapshot(Node);                         // lo que lee la zona al materializarse
            long uE4 = 0; int solids = 0;
            foreach (var z in zone)
            {
                long len = Obras.SegLenMm(z.X0, z.Y0, z.X1, z.Y1);      // geometría del sólido
                long price = z.IsWall ? p.PricePer10m(z.Kind) * len : p.PointPriceU(z.Kind) * 10_000L;
                if (Obras.CountsForLevel(p, z)) uE4 += price * z.LifeMil;
                if (Obras.HeightMm(z.Kind) > 0) solids++;
            }
            int levelZone = (int)Math.Min(uE4 / 300_000, p.LevelMaxMil);
            check(levelZone == wl.Works.LevelMil(Node), $"S0 = S2: el nivel recalculado desde los sólidos de la zona ({levelZone} ‰) es el del S0 ({wl.Works.LevelMil(Node)} ‰); {solids} sólidos");
            ulong hBefore = wl.Works.Hash(); int lBefore = wl.Works.LevelMil(Node);
            wl.Works.Restore(Node, new List<WorkPiece>()); check(wl.Works.Count(Node) == 0 && wl.Works.LevelMil(Node) == 0, "la zona recoge las piezas (Snapshot) y el nodo queda vacío mientras tanto");
            wl.Works.Restore(Node, zone);
            check(wl.Works.Hash() == hBefore && wl.Works.LevelMil(Node) == lBefore, "materializar y desmaterializar (copiar y devolver) no cambia el hash ni el nivel");
            check(Obras.HeightMm(PieceKind.Hesco) == 2100 && Obras.HeightMm(PieceKind.Sacos) == 1200, "alturas de la pieza: sacos 1,2 m, HESCO 2,1 m");

            // ---- hash
            var e0 = Mvp0(); var e1 = Mvp0();
            check(e0.Works.Hash() == 0 && e0.Hash() == e1.Hash(), "sin obras el hash de las obras es 0: los goldens G1–G5 no se mueven");
            ulong h0 = wl.Works.Hash();
            var m1 = wl.Works.At(Node); var piece = m1[3]; piece.LifeMil = 999; m1[3] = piece;
            check(wl.Works.Hash() != h0, "1 ‰ de vida de una pieza cambia el hash");
            piece.LifeMil = 1000; m1[3] = piece; piece.Built = false; m1[3] = piece;
            check(wl.Works.Hash() != h0, "que una pieza esté terminada o no cambia el hash");
            piece.Built = true; m1[3] = piece;
            check(wl.Works.Hash() == h0, "devolver el estado devuelve el hash");
            var sw = m1[2]; m1[2] = m1[5]; m1[5] = sw;
            check(wl.Works.Hash() != h0, "el orden de las piezas importa en el hash");
            m1[5] = m1[2]; m1[2] = sw;
            var wa = WorldWithZone(); var wb2 = WorldWithZone();
            foreach (var q in new[] { wa, wb2 }) { q.Push(Place(q.Tick, PieceKind.Sacos, 0, 0, 33_000, 17_000, 40_000, -9_000)); q.ApplyCommands(); for (int i = 0; i < 300; i++) q.Step(); }
            check(wa.Hash() == wb2.Hash() && wa.Works.Count(Node) > 0, "dos mundos con las mismas órdenes dan el mismo hash 300 ticks después");
            check(Mvp0().Hash() != WorldWithZone().Hash(), "construir (y gastar materiales) mueve el hash del mundo");

            // ---- repetición dorada con órdenes de obra (la carga útil de la orden se guarda y se reproduce)
            long T(double hours) { return NetConst.Hours(hours); }
            var script = new List<NetCommand>
            {
                new NetCommand { Tick = T(0.1), Type = NetCmdType.Materialize, Arg = Node },
                Place(T(0.2), PieceKind.Hesco, Square(18750)),
                Place(T(0.3), PieceKind.Nido, 18750, 0),
                Place(T(0.4), PieceKind.Hesco, 0, 0, 1_500_000, 0),          // 900 u de HESCO: con 802 u en el almacén se corta por materiales
                new NetCommand { Tick = T(0.9), Type = NetCmdType.Dematerialize, Arg = Node, Arg2 = 0 },
            };
            string gold = NetGolden.Record(script, 3000);
            var res = NetGolden.Verify(gold);
            check(res.Ok && gold.Contains(";9;" + (Node | ((int)PieceKind.Hesco << 16)) + ";0;"), $"grabar y reproducir órdenes de obra: {res}");
            long badTick = Mvp0().Tick + 2000;
            var res2 = NetGolden.Verify(gold, (world, t) => { if (t == badTick && world.Works.Count(Node) > 0) { var l = world.Works.At(Node); var x = l[0]; x.LifeMil -= 1; l[0] = x; } });
            check(!res2.Ok, $"un solo ‰ de vida cambiado a escondidas se detecta: {res2}");

            // ================================================================== paso 2: obreros, avance y cancelar
            Console.WriteLine("[16] Obras (hito 5, paso 2): asignar obreros, avance de la obra y cancelar");
            check(csv.CancelStartedMil == 500, "cancelar una obra empezada devuelve el 50 % (cancelar_obra_empezada_permil del CSV de Hugo)");

            // un nodo sin paradas en ningún horario: los trenes no cargan su población durante la prueba
            int quiet = -1;
            for (int n = 0; n < Mvp0().Graph.Nodes.Count && quiet < 0; n++)
            {
                bool stops = false; foreach (var sv in Mvp0().Services) foreach (var st in sv.Stops) if (st.Node == n) stops = true;
                if (!stops) quiet = n;
            }
            int StepsToBuild(int workers, int people, int[] pts, PieceKind k, int maxSteps, int node = Node)
            {
                var q = Mvp0();
                q.Stock[node, (int)Res.Materiales] = 900;
                q.Push(new NetCommand { Tick = q.Tick, Type = NetCmdType.Materialize, Arg = node }); q.ApplyCommands();
                q.Stock[node, (int)Res.Personas] = people;
                q.Push(new NetCommand { Tick = q.Tick, Type = NetCmdType.PlaceWork, Arg = node | ((int)k << 16), Payload = pts });
                q.Push(new NetCommand { Tick = q.Tick, Type = NetCmdType.AssignWorkers, Arg = node, Arg2 = workers });
                q.ApplyCommands();
                for (int i = 1; i <= maxSteps; i++)
                {
                    q.Step();
                    bool all = true; foreach (var x in q.Works.At(node)) if (!x.Built) all = false;
                    if (all) return i;
                }
                return -1;
            }
            int[] ten = { 0, 0, 10_000, 0 };
            check(StepsToBuild(2, 300, ten, PieceKind.Sacos, 5000) == 1500, "10 m de sacos son 2 obrero·h: con 2 obreros se terminan en 1 h de juego (1.500 ticks) exactos");
            check(StepsToBuild(4, 300, ten, PieceKind.Sacos, 5000) == 750, "con 4 obreros, en la mitad (750 ticks): con más obreros va más rápida");
            check(StepsToBuild(8, 300, ten, PieceKind.Sacos, 5000) == 750, "4 es el máximo por pieza: con 8 obreros en una sola pieza no va más rápida");
            check(quiet >= 0 && StepsToBuild(6, 7, ten, PieceKind.Sacos, 5000, quiet) == 3000, "los obreros se limitan a la población del nodo menos el retén (6, obras_guarnicion_minima): con 7 personas, 1 obrero, 3.000 ticks");
            check(quiet >= 0 && StepsToBuild(6, 6, ten, PieceKind.Sacos, 4000, quiet) == -1 && StepsToBuild(6, 10, ten, PieceKind.Sacos, 5000, quiet) == 750 && StepsToBuild(6, 12, ten, PieceKind.Sacos, 5000, quiet) == 750, "con 6 personas (el retén) nadie construye; con 10 trabajan 4 y con 12 (una estación recién fundada) también construye (D-N59: con 12 de retén no podría)");
            check(Mvp0().Works.P.GarrisonMin == 6 && Mvp0().Works.P.MaxWorkersPerPiece == 4 && Mvp0().Works.P.LevelDivU == 30, "el retén de obras (6) viene de obras_guarnicion_minima de balance_construccion.csv (NO el 12 de la producción), los u por nivel (30) de fort_materiales_por_nivel y el máximo de obreros por pieza es 4");
            check(StepsToBuild(0, 300, ten, PieceKind.Sacos, 4000) == -1, "sin obreros asignados la obra no avanza");
            check(StepsToBuild(2, 0, ten, PieceKind.Sacos, 4000) == -1, "sin población en el nodo no trabaja nadie");
            check(StepsToBuild(4, 300, new[] { 0, 0 }, PieceKind.Anden, 5000) == 3750 && StepsToBuild(4, 300, new[] { 0, 0 }, PieceKind.Foco, 5000) == 750, "andén (10 obrero·h) con 4 obreros: 3.750 ticks; foco (2 obrero·h): 750");
            int ring = StepsToBuild(8, 300, Square(18750), PieceKind.Sacos, 20000);
            check(ring >= 5625 && ring <= 5640, $"150 m de sacos (30 obrero·h) con 8 obreros: unas 3,75 h de juego, {ring} ticks");

            var wo = WorldWithZone(900);
            wo.Push(Place(wo.Tick, PieceKind.Hesco, Square(18750)));
            wo.Push(new NetCommand { Tick = wo.Tick, Type = NetCmdType.AssignWorkers, Arg = Node, Arg2 = 12 }); wo.ApplyCommands();
            check(wo.Works.IsActive(Node) && !wo.Works.IsActive(1), "una obra con obreros está activa (es lo que atrae a la Colmena); otro nodo no");
            int built0 = 0; for (int i = 0; i < 3000; i++) wo.Step();
            foreach (var x in wo.Works.At(Node)) if (x.Built) built0++;
            int lvl = wo.Works.LevelMil(Node);
            check(built0 > 0 && built0 < 16 && lvl > 0 && lvl < 3000, $"a mitad de obra solo cuenta lo terminado: {built0} de 16 módulos, nivel {lvl} ‰ de 3.000");
            while (wo.Works.IsActive(Node)) wo.Step();
            check(wo.Works.LevelMil(Node) == 3000 && !wo.Works.IsActive(Node), $"terminada la obra, el nivel de los 150 m de HESCO es el de D-N44 (nivel 3): {wo.Works.LevelMil(Node)} ‰");

            // cancelar: 100 % si no ha empezado, 50 % si ya hay trabajo hecho; lo terminado se queda
            var wc = WorldWithZone(900);
            wc.Push(Place(wc.Tick, PieceKind.Hesco, 0, 0, 100_000, 0)); wc.ApplyCommands();           // 60 u
            check(wc.Stock[Node, (int)Res.Materiales] == 840 && wc.Works.Count(Node) == 10, "100 m de HESCO: 60 u y 10 módulos");
            wc.Push(new NetCommand { Tick = wc.Tick, Type = NetCmdType.CancelWork, Arg = Node, Arg2 = 0 }); wc.ApplyCommands();
            check(wc.Stock[Node, (int)Res.Materiales] == 900 && wc.Works.Count(Node) == 0, "cancelar una obra sin empezar devuelve el 100 % (60 u)");
            wc.Push(Place(wc.Tick, PieceKind.Hesco, 0, 0, 100_000, 0)); wc.Push(new NetCommand { Tick = wc.Tick, Type = NetCmdType.AssignWorkers, Arg = Node, Arg2 = 4 }); wc.ApplyCommands();
            for (int i = 0; i < 10; i++) wc.Step();
            wc.Push(new NetCommand { Tick = wc.Tick, Type = NetCmdType.CancelWork, Arg = Node, Arg2 = 1 }); wc.ApplyCommands();
            check(wc.Stock[Node, (int)Res.Materiales] == 870 && wc.Works.Count(Node) == 0, $"cancelar una obra ya empezada devuelve el 50 % (30 de 60 u): quedan {wc.Stock[Node, (int)Res.Materiales]} u");
            wc.Push(Place(wc.Tick, PieceKind.Sacos, 0, 0, 20_000, 0)); wc.ApplyCommands();            // obra 2: dos módulos, 4 u
            while (wc.Works.At(Node)[0].Built == false) wc.Step();                                   // 4 obreros terminan el primero y empiezan el segundo
            int before = wc.Stock[Node, (int)Res.Materiales], pieces = wc.Works.Count(Node);
            wc.Push(new NetCommand { Tick = wc.Tick, Type = NetCmdType.CancelWork, Arg = Node, Arg2 = 2 }); wc.ApplyCommands();
            check(wc.Works.Count(Node) == 1 && wc.Works.At(Node)[0].Built && wc.Stock[Node, (int)Res.Materiales] == before + 1 && pieces == 2, $"al cancelar, lo ya terminado se queda y del resto vuelve el 50 % (1 de 2 u): quedan {wc.Works.Count(Node)} pieza y {wc.Stock[Node, (int)Res.Materiales]} u");
            wc.Push(new NetCommand { Tick = wc.Tick, Type = NetCmdType.CancelWork, Arg = Node, Arg2 = 77 }); wc.ApplyCommands();
            check(wc.Works.Count(Node) == 1, "cancelar una obra que no existe no hace nada");

            // el máximo de obreros por pieza sale del CSV (obra_obreros_max_pieza) y las obras grandes lo suben (el puente del Jiloca lleva 8)
            var p8 = ObrasParams.FromCsv(new CsvTable(File.ReadAllText(Path.Combine(BalanceTests.DataDirPublic(), "balance_construccion.csv")).Replace("obra_obreros_max_pieza;4;", "obra_obreros_max_pieza;8;")));
            p8.TrackHalfMm = 0;
            var st8 = new ObrasState(3, p8); st8.Add(0, st8.Preview(0, PieceKind.Sacos, ten, 900));
            int ticks8 = 0; while (!st8.At(0)[0].Built && ticks8 < 5000) { st8.Advance(0, 8); ticks8++; }
            check(p8.MaxWorkersPerPiece == 8 && st8.At(0)[0].MaxWorkers == 8 && ticks8 == 375, $"con el máximo en 8 (obra grande) 8 obreros terminan 10 m de sacos en {ticks8} ticks (375)");
            var stOld = new ObrasState(3, p); stOld.Add(0, stOld.Preview(0, PieceKind.Sacos, ten, 900)); int t4 = 0; while (!stOld.At(0)[0].Built && t4 < 5000) { stOld.Advance(0, 8); t4++; }
            check(t4 == 750, "con el máximo por defecto (4) las mismas 8 personas tardan 750 ticks");

            // obra en pausa: nodo asediado o con la horda encima (Marcos)
            var pz2 = WorldWithZone(900);
            pz2.Push(Place(pz2.Tick, PieceKind.Sacos, 0, 0, 10_000, 0)); pz2.Push(new NetCommand { Tick = pz2.Tick, Type = NetCmdType.AssignWorkers, Arg = Node, Arg2 = 4 }); pz2.ApplyCommands();
            for (int i = 0; i < 40; i++) pz2.Step();
            int w0 = pz2.Works.At(Node)[0].WorkE4;
            check(w0 > 0 && pz2.WorkActive(Node) && !pz2.WorkPaused(Node), "con el nodo en calma la obra avanza y está activa");
            pz2.Director.Active.Add(new HiveDirector.Plan { Kind = HiveDirector.PlanKind.Asedio, Node = Node, Edge = -1, Cell = 0, StartTick = pz2.Tick, UntilTick = pz2.Tick + 10 * NetConst.DayTicks, Started = true });
            for (int i = 0; i < 30; i++) pz2.Step();                                          // el estado del nodo se actualiza cada 15 ticks
            int wMid = pz2.Works.At(Node)[0].WorkE4;
            for (int i = 0; i < 300; i++) pz2.Step();
            check(pz2.Intel.FindAll(e => e.Id == "OC-4").Count == 1, "OC-4: al parar la horda la obra avisa una sola vez, no en cada tick");
            check(pz2.WorkPaused(Node) && !pz2.WorkActive(Node) && pz2.Works.At(Node)[0].WorkE4 == wMid, $"con el nodo asediado la obra se pausa y deja de estar activa (sin ruido, peso ni huella): {wMid} = {pz2.Works.At(Node)[0].WorkE4}");
            check(pz2.Director.SiegeTargetWeight(pz2, Node) == pz2.Director.SiegeWeight(pz2, Node), "una obra pausada no pesa ×1,5 como objetivo");
            pz2.Director.Active.Clear();
            for (int i = 0; i < 40; i++) pz2.Step();
            check(!pz2.WorkPaused(Node) && pz2.Works.At(Node)[0].WorkE4 > wMid, "cuando pasa el asedio la obra sigue donde se quedó");
            pz2.Director.Active.Add(new HiveDirector.Plan { Kind = HiveDirector.PlanKind.Asedio, Node = Node, Edge = -1, Cell = 0, StartTick = pz2.Tick, UntilTick = pz2.Tick + 10 * NetConst.DayTicks, Started = true });
            for (int i = 0; i < 30; i++) pz2.Step();
            check(pz2.Intel.FindAll(e => e.Id == "OC-4").Count == 2, "una segunda parada vuelve a avisar (OC-4)");
            var ph = Mvp0(); ph.Works.Add(0, ph.Works.Preview(0, PieceKind.Sacos, ten, 900)); ph.Works.Workers[0] = 4;
            ph.Graph.Nodes[0].HordeDensity = NetWorld.SiegeAt;
            check(ph.WorkPaused(0) && !ph.WorkActive(0), "con la horda encima (densidad de asedio) la obra se pausa");

            // hash y repetición con las órdenes nuevas
            var ha = WorldWithZone(); var hb = WorldWithZone();
            foreach (var q in new[] { ha, hb })
            {
                q.Push(Place(q.Tick, PieceKind.Hesco, Square(18750))); q.Push(new NetCommand { Tick = q.Tick, Type = NetCmdType.AssignWorkers, Arg = Node, Arg2 = 9 }); q.ApplyCommands();
                for (int i = 0; i < 1500; i++) q.Step();
            }
            check(ha.Hash() == hb.Hash() && ha.Works.Workers[Node] == 9, "dos mundos con las mismas órdenes y 1.500 ticks de obra dan el mismo hash");
            var fresh = WorldWithZone(); fresh.Push(Place(fresh.Tick, PieceKind.Hesco, Square(18750))); fresh.ApplyCommands();
            ulong hNo = fresh.Works.Hash();
            fresh.Push(new NetCommand { Tick = fresh.Tick, Type = NetCmdType.AssignWorkers, Arg = Node, Arg2 = 1 }); fresh.ApplyCommands();
            check(fresh.Works.Hash() != hNo, "asignar un solo obrero cambia el hash");
            for (int i = 0; i < 20; i++) fresh.Step();
            ulong hW = fresh.Works.Hash(); var fp = fresh.Works.At(Node); var f0 = fp[0]; f0.WorkE4 -= 1; fp[0] = f0;
            check(fresh.Works.Hash() != hW, "1/10.000 de obrero·tick de trabajo cambia el hash");

            var script2 = new List<NetCommand>
            {
                new NetCommand { Tick = T(0.1), Type = NetCmdType.Materialize, Arg = Node },
                Place(T(0.2), PieceKind.Hesco, Square(18750)),
                new NetCommand { Tick = T(0.3), Type = NetCmdType.AssignWorkers, Arg = Node, Arg2 = 10 },
                Place(T(0.4), PieceKind.Sacos, 0, 0, 30_000, 0),
                new NetCommand { Tick = T(0.8), Type = NetCmdType.CancelWork, Arg = Node, Arg2 = 1 },
                new NetCommand { Tick = T(1.2), Type = NetCmdType.AssignWorkers, Arg = Node, Arg2 = 0 },
            };
            string gold2 = NetGolden.Record(script2, 3000);
            var r3 = NetGolden.Verify(gold2);
            check(r3.Ok && gold2.Contains(";10;" + Node + ";10\n") && gold2.Contains(";11;" + Node + ";1\n"), $"grabar y reproducir asignar obreros y cancelar: {r3}");
            long bad2 = Mvp0().Tick + 2500;
            var r4 = NetGolden.Verify(gold2, (world, t) => { if (t == bad2) world.Works.Workers[Node] += 1; });
            check(!r4.Ok, $"un obrero de más a escondidas se detecta: {r4}");

            // ================================================================== paso 3: ruido de obra en el director
            Console.WriteLine("[17] Obras (hito 5, paso 3): sondeo de obra, peso del nodo y huella");
            var dcsv = DirectorParams.Default.WithConstruccion(new CsvTable(File.ReadAllText(Path.Combine(BalanceTests.DataDirPublic(), "balance_construccion.csv"))));
            var dd0 = DirectorParams.Default;
            check(dcsv.WorkProbeMil == dd0.WorkProbeMil && dcsv.WorkProbeBudgetMil == dd0.WorkProbeBudgetMil && dcsv.WorkWeightMil == dd0.WorkWeightMil && dcsv.WorkHuellaMil == dd0.WorkHuellaMil && dd0.WorkProbeMil == 83,
                $"el ruido de obra por defecto coincide con balance_construccion.csv (sondeo {dcsv.WorkProbeMil} ‰/h, {dcsv.WorkProbeBudgetMil} ‰ del presupuesto, peso {dcsv.WorkWeightMil} ‰, huella {dcsv.WorkHuellaMil} ‰)");
            check(Mvp0().Director.P.WorkProbeMil == 83, "el mundo carga el ruido de obra del CSV");

            NetWorld WithWork(int node, int workers = 4)
            {
                var q = Mvp0();
                q.Works.Add(node, q.Works.Preview(node, PieceKind.Sacos, new[] { 0, 0, 50_000, 0 }, 900));
                q.Works.Workers[node] = workers;
                return q;
            }
            // huella: 0,5 por nodo con obra activa
            var ctl = Mvp0(); long h0Huella = ctl.Director.HuellaMil(ctl);
            var q1 = WithWork(0); var q0 = WithWork(0, 0);
            check(q1.Director.HuellaMil(q1) == h0Huella + 500 && q0.Director.HuellaMil(q0) == h0Huella, "una obra activa suma 0,5 a la huella; sin obreros no cuenta");
            q1.Works.Add(0, q1.Works.Preview(0, PieceKind.Hesco, new[] { 0, 0, 30_000, 0 }, 900));
            check(q1.Director.HuellaMil(q1) == h0Huella + 500, "partir la obra en varias órdenes no multiplica la huella (una por nodo)");
            q1.Works.Add(2, q1.Works.Preview(2, PieceKind.Sacos, new[] { 0, 0, 10_000, 0 }, 900)); q1.Works.Workers[2] = 1;
            check(q1.Director.HuellaMil(q1) == h0Huella + 1000, "dos nodos con obra suman 1,0");
            // peso como objetivo de asedio: ×1,5
            var qa = WithWork(0);
            bool wOk = qa.Director.SiegeTargetWeight(qa, 0) == qa.Director.SiegeWeight(qa, 0) * 1500 / 1000;
            for (int n = 0; n < qa.Graph.Nodes.Count; n++) if (n != 0 && qa.Director.SiegeTargetWeight(qa, n) != qa.Director.SiegeWeight(qa, n)) wOk = false;
            check(wOk && qa.Director.SiegeWeight(qa, 0) > 0, $"el nodo con obra activa pesa ×1,5 como objetivo de asedio y los demás no ({qa.Director.SiegeWeight(qa, 0)} → {qa.Director.SiegeTargetWeight(qa, 0)})");
            int hitsWork = 0, hitsCtl = 0; var qc = Mvp0();
            for (int k = 0; k < 4000; k++) { qa.Tick = 40 * NetConst.DayTicks + k * 37; qc.Tick = qa.Tick; if (qa.Director.DrawSiegeTarget(qa) == 0) hitsWork++; if (qc.Director.DrawSiegeTarget(qc) == 0) hitsCtl++; }
            check(hitsWork > hitsCtl, $"con la obra el nodo sale más veces como objetivo: {hitsWork} contra {hitsCtl} de 4.000 sorteos");

            // sondeo: tasa de 83 ‰ por hora de obra, nada sin obra
            long Mass(NetWorld q) { return q.Field.Mass(); }
            var pw = WithWork(0); var pc = Mvp0();
            long m0w = Mass(pw), m0c = Mass(pc);
            for (int hr = 0; hr < 600; hr++)
            {
                pw.Tick = 3 * NetConst.DayTicks + hr * 1500; pc.Tick = pw.Tick;
                pw.Director.Hourly(pw); pc.Director.Hourly(pc);
            }
            long rate = pw.Director.WorkProbes_Count;
            check(pc.Director.WorkProbes_Count == 0 && rate >= 30 && rate <= 75, $"sin obra no hay sondeos; con una obra, unos 83 ‰ por hora: {rate} sondeos en 600 h (esperado ≈ 50)");
            check(Mass(pw) == m0w + pw.InjectedTotal - pw.LostTotal - pw.RetiredTotal && Mass(pc) == m0c + pc.InjectedTotal - pc.LostTotal - pc.RetiredTotal, "la masa del campo se conserva con sondeos: masa = inicial + inyectada − bajas − retirada");
            check(pw.Director.WorkProbes_Moved > 0 && pw.Director.WorkProbes_Moved <= pw.Director.WorkProbes_Asked, $"los sondeos mueven horda hacia el nodo: pidieron {pw.Director.WorkProbes_Asked} ‰ y movieron {pw.Director.WorkProbes_Moved} ‰");

            // tamaño: 5 % del presupuesto del día, descontado del 400 ‰ que no va a asedios (Hugo): no es masa extra
            var ps = WithWork(0); ps.Director.P.WorkProbeMil = 1000;
            ps.Tick = 9 * NetConst.DayTicks; ps.Director.Hourly(ps);
            long askZ = ps.Director.WorkProbes_Asked * NetWorld.ZombiesPerPermil;
            long expectZ = ps.Director.DayZombies(9, ps.Director.HuellaMil(ps)) * 50 / 1000 / NetWorld.ZombiesPerPermil * NetWorld.ZombiesPerPermil;
            check(ps.Director.WorkProbes_Count == 1 && askZ == expectZ && askZ >= 150 && askZ <= 450, $"un sondeo del día 9 pide el 5 % del presupuesto del día: {askZ} zombis (Hugo: 214–281 según la huella, con el ruido y la estación del día)");
            check(ps.Director.WorkProbes_Paid > 0 && ps.Director.WorkProbes_Paid <= ps.Director.WorkProbes_Asked, $"se paga con lo que hay del 400 ‰ de esa hora (deriva y ahorro de emboscada): {ps.Director.WorkProbes_Paid} de {ps.Director.WorkProbes_Asked} ‰");
            ps.Director.Season = null; ps.Director.Noise = null;
            double k0 = ps.Director.DayZombies(8, 0), k3 = ps.Director.DayZombies(8, 3000);
            check(k3 / k0 > 1.30 && k3 / k0 < 1.33, $"con k_huella = 240 del CSV, huella 3 da {k3 / k0:0.000} veces el presupuesto de huella 0 (Hugo: 268 / 204 = 1,314)");
            // descontado, no extra: con la misma huella, la obra no cambia lo inyectado en 5 días
            var xa = WithWork(0); var xb = Mvp0(); xa.Director.P.WorkHuellaMil = 0; xb.Director.P.WorkHuellaMil = 0; xa.Director.P.WorkProbeMil = 1000;
            for (int hr = 0; hr < 120; hr++) { xa.Tick = 2 * NetConst.DayTicks + hr * 1500; xb.Tick = xa.Tick; xa.Director.Hourly(xa); xb.Director.Hourly(xb); }
            check(xa.Director.WorkProbes_Paid > 0 && xa.Director.BudgetTotal == xb.Director.BudgetTotal, $"el sondeo no suma presupuesto: el presupuesto de 120 h es el mismo con y sin obra ({xa.Director.BudgetTotal} = {xb.Director.BudgetTotal} ‰); el sondeo movió {xa.Director.WorkProbes_Moved} ‰ de masa que ya estaba en el campo");

            // informe: OC-1 (primera obra, una sola vez), OC-3 (el sondeo llega), OC-4 (la horda para la obra)
            var tx = new IntelTexts(File.ReadAllText(Path.Combine(BalanceTests.DataDirPublic(), "textos_informe.csv")));
            bool allTexts = true; foreach (var id in new[] { "OC-1", "OC-2", "OC-3", "OC-4" }) if (!tx.Has(id)) allTexts = false;
            string oc1 = tx.Format(new IntelEntry { Id = "OC-1", Vars = new[] { "nodo", "Valencia" } }, "es", out var src1), oc3 = tx.Format(new IntelEntry { Id = "OC-3", Vars = new[] { "nodo", "Valencia" } }, "en", out var src3);
            check(allTexts && oc1 == "Una obra se oye desde lejos. Mientras dure, Valencia atrae a la Colmena." && src1 == "Llorens" && oc3 == "The construction gave us away. Contact at Valencia." && src3 == "tras el ataque", $"los textos OC-1 a OC-4 de Lucía están en textos_informe.csv: «{oc1}» / «{oc3}»");
            var ic = WorldWithZone(900);
            ic.Push(Place(ic.Tick, PieceKind.Sacos, 0, 0, 10_000, 0)); ic.Push(new NetCommand { Tick = ic.Tick, Type = NetCmdType.AssignWorkers, Arg = Node, Arg2 = 4 }); ic.ApplyCommands();
            check(!ic.Intel.Exists(e => e.Id == "OC-1"), "sin tiempo de obra todavía no hay aviso de obra");
            ic.Step();
            int oc1n = ic.Intel.FindAll(e => e.Id == "OC-1").Count;
            ic.Push(Place(ic.Tick, PieceKind.Sacos, 0, 0, 20_000, 0)); ic.ApplyCommands(); for (int i = 0; i < 2000; i++) ic.Step();
            check(oc1n == 1 && ic.Intel.FindAll(e => e.Id == "OC-1").Count == 1 && ic.Intel.Find(e => e.Id == "OC-1").Vars[1] == ic.Graph.Nodes[Node].Name, "OC-1: la primera obra activa de la partida avisa una sola vez");
            var hOc = ic.Hash(); ic.Works.FirstNoted = false; check(ic.Hash() != hOc, "haber avisado de la primera obra entra en el hash"); ic.Works.FirstNoted = true;
            check(ps.Intel.Exists(e => e.Id == "OC-3" && e.Vars[1] == ps.Graph.Nodes[0].Name), "OC-3: el sondeo que llega avisa con el nombre del nodo");
            check(!Mvp0().Intel.Exists(e => e.Id.StartsWith("OC-")), "sin obras no hay avisos de obra");

            // en la zona en detalle: la celda congelada no recibe masa
            var pz = WithWork(0); pz.Director.P.WorkProbeMil = 1000;
            pz.Push(new NetCommand { Tick = pz.Tick, Type = NetCmdType.Materialize, Arg = 0 }); pz.ApplyCommands();
            long mz = Mass(pz);
            for (int hr = 0; hr < 24; hr++) { pz.Tick = 5 * NetConst.DayTicks + hr * 1500; pz.Director.Hourly(pz); }
            check(pz.Director.WorkProbes_Count == 24 && pz.Field.D[pz.Field.Frozen] == 0 && Mass(pz) == mz + pz.InjectedTotal - pz.LostTotal - pz.RetiredTotal, "con la zona del nodo materializada el sondeo no toca la celda congelada y la masa se conserva");

            // determinismo
            var da = WithWork(0); var db = WithWork(0);
            foreach (var q in new[] { da, db }) for (int hr = 0; hr < 100; hr++) { q.Tick = 4 * NetConst.DayTicks + hr * 1500; q.Director.Hourly(q); }
            check(da.Hash() == db.Hash() && da.Hash() != WithWork(0).Hash(), "dos mundos con la misma obra dan el mismo hash tras 100 horas de director");

            RunDesgaste(check);
            RunPuertaVia(check);
        }

        static void RunPuertaVia(Action<bool, string> check)
        {
            Console.WriteLine("[21] Puerta de vía (hito 5, diseno-hito5.md §2a): la red de Obras conoce la vía");
            var p = ObrasParams.FromCsv(new CsvTable(File.ReadAllText(Path.Combine(BalanceTests.DataDirPublic(), "balance_construccion.csv"))));
            check(p.ViaGateU == 8 && p.HViaGate == 6 && p.TrackHalfMm == 1600 && p.GateSwapTicks == 180 && p.GateNearMm == 150_000,
                $"la puerta de vía cuesta 8 u y 6 obrero·h (pieza_puerta_via_u y obra_puerta_via_h del CSV de Hugo); hueco de 3,2 m, 6 s = 180 ticks de zona, abre a 150 m");

            // un muro que cruza la vía en perpendicular: puerta sola, 3,2 m, y su coste va en el trazado
            var perp = Obras.PlanWall(p, PieceKind.Hesco, new[] { 20_000, -10_000, 20_000, 10_000 }, 900, 2000);
            WorkPiece gate = default; int gates = 0; foreach (var x in perp.Pieces) if (x.IsGate) { gate = x; gates++; }
            check(perp.Ok && perp.Gates == 1 && gates == 1 && perp.Pieces.Count == 3 && perp.WantedMm == 16_800, $"un muro de 20 m que cruza la vía deja 16,8 m de muro y una puerta de vía: {perp.Pieces.Count} piezas, pedido {perp.WantedMm} mm");
            check(gate.X0 == 20_000 && gate.Y0 == -1600 && gate.X1 == 20_000 && gate.Y1 == 1600 && gate.CostE4 == 80_000 && gate.LifeMil == 1000 && !gate.Built, "la puerta ocupa 3,2 m perpendiculares a la vía, de y = −1.600 a y = 1.600, y cuesta 8 u");
            check(perp.CostE4 == 2 * 8400 * 6 + 80_000 && perp.CostU == 19, $"el coste en directo suma muro y puerta: {perp.CostE4} E4 = {perp.CostU} u (2 × 8,4 m de HESCO + 8 u)");
            check(Obras.HeightMm(PieceKind.PuertaVia) == 2100 && p.WorkE4(PieceKind.PuertaVia, 0) == 6 * 1500 * 10_000, "la puerta mide 2,1 m cerrada, como el HESCO, y son 6 obrero·h de obra");

            // en diagonal: el hueco medido sobre el muro es 3,2 ÷ sen α; por debajo de 45° no se acepta
            var d45 = Obras.PlanWall(p, PieceKind.Hesco, new[] { 0, -10_000, 20_000, 10_000 }, 900, 2000);
            WorkPiece g45 = default; foreach (var x in d45.Pieces) if (x.IsGate) g45 = x;
            long chord = Obras.SegLenMm(g45.X0, g45.Y0, g45.X1, g45.Y1);
            check(d45.Ok && d45.Gates == 1 && chord == 4525, $"a 45° el hueco es 3,2 ÷ sen 45° = 4,525 m: {chord} mm");
            var shallow = Obras.PlanWall(p, PieceKind.Hesco, new[] { 0, -10_000, 30_000, 10_000 }, 900, 2000);
            check(!shallow.Ok && shallow.Why == "El muro cruza la vía demasiado en diagonal" && shallow.Pieces.Count == 0, $"a menos de 45° se rechaza el trazado: «{shallow.Why}»");

            // un muro a lo largo de la vía o que acaba en la franja: la vía queda libre, sin puerta
            var along = Obras.PlanWall(p, PieceKind.Hesco, new[] { 0, 500, 30_000, 500 }, 900, 2000);
            check(!along.Ok && along.Why == "el muro cae sobre la vía", $"un muro dentro del gálibo no se construye: «{along.Why}»");
            var ends = Obras.PlanWall(p, PieceKind.Sacos, new[] { 5000, 5000, 5000, 500 }, 900, 2000);
            check(ends.Ok && ends.Gates == 0 && ends.AllowedMm == 3400 && ends.Pieces[ends.Pieces.Count - 1].Y1 == 1600, $"un muro que acaba en la vía se recorta en el borde del gálibo (y = 1.600): {ends.AllowedMm} mm, sin puerta");
            var touch = Obras.PlanWall(p, PieceKind.Sacos, new[] { 0, 1600, 20_000, 1600 }, 900, 2000);
            check(touch.Ok && touch.Gates == 0 && touch.AllowedMm == 20_000, "un muro justo en el borde del gálibo (y = 1.600) se queda entero");

            // corte por materiales: la puerta entra en la regla (orden del trazado)
            var c10 = Obras.PlanWall(p, PieceKind.Hesco, new[] { 20_000, -10_000, 20_000, 10_000 }, 10, 2000);
            check(c10.Ok && c10.Cut && c10.Gates == 0 && c10.AllowedMm == 8400 && c10.CostU == 6, $"con 10 u el muro se corta antes de la vía y no lleva puerta ({c10.AllowedMm} mm, {c10.CostU} u)");
            var c14 = Obras.PlanWall(p, PieceKind.Hesco, new[] { 20_000, -10_000, 20_000, 10_000 }, 14, 2000);
            check(c14.Ok && c14.Cut && c14.Gates == 1 && c14.AllowedMm == 10_000 && c14.CostE4 == 140_000 && c14.CostU == 14, $"con 14 u cabe el primer muro, la puerta y 1,6 m del segundo: {c14.AllowedMm} mm, {c14.CostU} u");
            check(Obras.PlanWall(p, PieceKind.Hesco, new[] { 20_000, -10_000, 20_000, 10_000 }, 900, 2).Why == "demasiadas piezas en el nodo", "el tope de piezas cuenta también la puerta");

            // las piezas puntuales no ocupan la vía
            check(Obras.PlanPoint(p, PieceKind.Nido, 5000, 800, 900, 2000).Why == "la vía queda libre" && Obras.PlanPoint(p, PieceKind.Puerta, 0, 0, 900, 2000).Why == "la vía queda libre", "un nido o una puerta suelta en el gálibo de la vía se rechaza");
            check(Obras.PlanPoint(p, PieceKind.Nido, 5000, 1600, 900, 2000).Ok && Obras.PlanPoint(p, PieceKind.Anden, 0, 1600, 900, 2000).Ok, "pero sí pegado al borde del gálibo (a 1,6 m de la vía)");
            check(Obras.PlanPoint(p, PieceKind.Anden, 0, 0, 900, 2000).Why == "la vía queda libre" && Obras.PlanPoint(p, PieceKind.Deposito, 0, -1599, 900, 2000).Why == "la vía queda libre" && Obras.PlanPoint(p, PieceKind.Foco, 0, 1599, 900, 2000).Why == "la vía queda libre", "el andén fortificado tampoco invade el gálibo: está junto a la vía, no encima (Lucía)");
            check(!Obras.PlanPoint(p, PieceKind.PuertaVia, 0, 5000, 900, 2000).Ok, "la puerta de vía no se coloca a mano");

            // en el mundo: la orden paga muro y puerta, el nivel cuenta la puerta, el hash es el mismo en dos mundos
            NetWorld World() { var w = WorldWithZone(900); w.Works.P.TrackHalfMm = 1600; w.Push(Place(w.Tick, PieceKind.Hesco, 20_000, -10_000, 20_000, 10_000)); w.ApplyCommands(); return w; }
            var wa = World(); var wb = World();
            check(wa.Stock[Node, (int)Res.Materiales] == 900 - 19 && wa.Works.Count(Node) == 3 && wa.Hash() == wb.Hash(), "la orden descuenta 19 u (muro y puerta) y registra tres piezas; dos mundos dan el mismo hash");
            wa.Works.CompleteAll(Node);
            check(wa.Works.LevelMil(Node) == (int)(180_800L * 1000 / 300_000), $"la puerta es defensiva y cuenta para el nivel: {wa.Works.LevelMil(Node)} ‰");
            check(wa.Works.MaintenanceU(Node) == 1, "y para el mantenimiento");

            // estado: abrir y cerrar solas con un tren a ≤ 150 m, 6 s = 180 ticks, y el bloqueo del jugador, todo en el hash
            int gi = -1; for (int i = 0; i < wa.Works.At(Node).Count; i++) if (wa.Works.At(Node)[i].IsGate) gi = i;
            var near = new List<int> { 20_000 + 100_000 }; var far = new List<int> { 20_000 + 400_000 };
            ulong h0 = wa.Hash();
            for (int t = 0; t < 179; t++) wa.Works.StepGates(Node, near);
            bool halfway = wa.Works.At(Node)[gi].GateProg == 179 && !ObrasState.GatePassable(wa.Works.P, wa.Works.At(Node)[gi]);
            wa.Works.StepGates(Node, near);
            check(halfway && ObrasState.GatePassable(wa.Works.P, wa.Works.At(Node)[gi]) && wa.Hash() != h0, "con un tren propio a 100 m la puerta tarda 180 ticks (6 s) en abrirse: a los 179 no deja pasar, a los 180 sí; el estado entra en el hash");
            for (int t = 0; t < 500; t++) wa.Works.StepGates(Node, near);
            check(wa.Works.At(Node)[gi].GateProg == 180, "se queda abierta mientras el tren esté cerca");
            for (int t = 0; t < 179; t++) wa.Works.StepGates(Node, far);
            bool still = !ObrasState.GatePassable(wa.Works.P, wa.Works.At(Node)[gi]) && wa.Works.At(Node)[gi].GateProg == 1;
            wa.Works.StepGates(Node, far);
            check(still && wa.Works.At(Node)[gi].GateProg == 0, "con el tren a más de 150 m se cierra, también en 180 ticks");
            wa.Push(new NetCommand { Tick = wa.Tick, Type = NetCmdType.SetGate, Arg = Node | (1 << 16), Payload = new[] { 20_000, 0 } }); wa.ApplyCommands();
            for (int t = 0; t < 200; t++) wa.Works.StepGates(Node, null);
            check(wa.Works.At(Node)[gi].GateMode == 1 && wa.Works.At(Node)[gi].GateProg == 180, "bloqueada abierta, se abre sin ningún tren");
            wa.Push(new NetCommand { Tick = wa.Tick, Type = NetCmdType.SetGate, Arg = Node | (2 << 16), Payload = new[] { 20_300, 100 } }); wa.ApplyCommands();
            for (int t = 0; t < 200; t++) wa.Works.StepGates(Node, near);
            check(wa.Works.At(Node)[gi].GateMode == 2 && wa.Works.At(Node)[gi].GateProg == 0, "bloqueada cerrada, no se abre aunque llegue un tren: se para antes (la zona lo detiene)");
            ulong hLock = wa.Hash();
            wa.Push(new NetCommand { Tick = wa.Tick, Type = NetCmdType.SetGate, Arg = Node | (0 << 16), Payload = new[] { 20_000, 0 } }); wa.ApplyCommands();
            check(wa.Works.At(Node)[gi].GateMode == 0 && wa.Hash() != hLock, "el modo automático la desbloquea (y cambia el hash)");
            var wz = World(); wz.Works.CompleteAll(Node); ulong hz = wz.Hash();
            wz.Push(new NetCommand { Tick = wz.Tick, Type = NetCmdType.SetGate, Arg = Node | (1 << 16), Payload = new[] { 90_000, 90_000 } }); wz.ApplyCommands();
            check(wz.Hash() == hz, "una orden sobre un punto sin puerta no hace nada");

            // rota: queda un hueco con escombros, la vía sigue usable, no cuenta para el nivel y se reconstruye encima
            var wr = World(); wr.Works.CompleteAll(Node);
            int lvlWhole = wr.Works.LevelMil(Node); int gr = -1; for (int i = 0; i < wr.Works.At(Node).Count; i++) if (wr.Works.At(Node)[i].IsGate) gr = i;
            bool broke = wr.Works.Damage(Node, gr, 1000);
            var bg = wr.Works.At(Node)[gr];
            check(broke && bg.Broken && bg.LifeMil == 0 && wr.Works.Count(Node) == 3 && ObrasState.GatePassable(wr.Works.P, bg), "una puerta rota se queda como hueco con escombros y la vía sigue siendo usable");
            check(wr.Works.LevelMil(Node) < lvlWhole && wr.Works.LevelMil(Node) == (int)(100_800L * 1000 / 300_000), $"la puerta rota no cuenta para el nivel: {lvlWhole} ‰ → {wr.Works.LevelMil(Node)} ‰");
            check(!wr.Works.Damage(Node, gr, 500) && wr.Works.MaintenanceU(Node) == 1, "una puerta rota no recibe más daño");
            wr.Works.StepGates(Node, near);
            check(wr.Works.At(Node)[gr].GateProg == 0 && wr.Works.At(Node)[gr].Broken, "una puerta rota no se mueve");
            wr.Push(Place(wr.Tick, PieceKind.Hesco, 20_000, -10_000, 20_000, 10_000)); wr.ApplyCommands();
            int nGates = 0, nBroken = 0; foreach (var x in wr.Works.At(Node)) { if (x.IsGate) nGates++; if (x.Broken) nBroken++; }
            WorkPiece reb = default; foreach (var x in wr.Works.At(Node)) if (x.IsGate) reb = x;
            check(reb.X0 == bg.X0 && reb.Y0 == bg.Y0 && reb.X1 == bg.X1 && reb.Y1 == bg.Y1 && reb.CostE4 == 80_000 && reb.LifeMil == 1000 && !reb.Broken, "la puerta nueva conserva la posición exacta de la rota y cuesta lo de una nueva (8 u)");
            check(nGates == 1 && nBroken == 0 && wr.Works.Count(Node) == 5, $"se reconstruye encima: la puerta rota desaparece y entra la nueva ({nGates} puerta, {nBroken} rotas, {wr.Works.Count(Node)} piezas)");

            // el desgaste también la rompe, y se cancela con su obra
            var wc = World(); wc.Works.CompleteAll(Node); wc.Services.Clear(); wc.Stock[Node, (int)Res.Materiales] = 0;
            for (int d = 1; d <= 400; d++) { wc.Tick = d * NetConst.DayTicks - 1; wc.Step(); }
            bool anyBroken = false; foreach (var x in wc.Works.At(Node)) if (x.IsGate && x.Broken) anyBroken = true;
            check(anyBroken && wc.Works.Count(Node) >= 1, "sin mantenimiento, el desgaste acaba rompiendo la puerta (queda como escombros, no desaparece)");
            var wk = World(); int before = wk.Stock[Node, (int)Res.Materiales];
            wk.Push(new NetCommand { Tick = wk.Tick, Type = NetCmdType.CancelWork, Arg = Node, Arg2 = 0 }); wk.ApplyCommands();
            check(wk.Works.Count(Node) == 0 && wk.Stock[Node, (int)Res.Materiales] == before + 18, "cancelar la obra de un muro con puerta quita la puerta y devuelve el coste exacto (180.800 E4 = 18 u: la devolución redondea hacia abajo y el cobro hacia arriba, como siempre)");
        }

        static void RunDesgaste(Action<bool, string> check)
        {
            Console.WriteLine("[19] Obras (hito 5, paso 4): mantenimiento y desgaste diario (1 %) y daño");
            var mvp = Mvp0();
            check(mvp.Works.P.WearDayMil == 10, "el desgaste diario viene de fort_desgaste_dia de balance_economia.csv (10 ‰)");

            NetWorld Built(int reserve = 0)
            {
                var w = WorldWithZone();
                w.Push(Place(w.Tick, PieceKind.Hesco, 0, 0, 30_000, 0)); w.Push(Place(w.Tick, PieceKind.Sacos, 0, 5_000, 10_000, 5_000));
                w.ApplyCommands(); w.Works.CompleteAll(Node);
                w.Push(Place(w.Tick, PieceKind.Sacos, 0, 9_000, 10_000, 9_000)); w.ApplyCommands();      // esta queda sin construir
                w.Stock[Node, (int)Res.Materiales] = reserve;            // por defecto, una estación sin reservas: el mantenimiento no se puede pagar
                return w;
            }
            var a = Built();
            int pieces = a.Works.Count(Node); int lvl0 = a.Works.LevelMil(Node);
            // empuja el reloj hasta justo antes del cambio de día
            a.Tick = 3 * NetConst.DayTicks - 1;
            a.Step();
            bool wore = true; int unbuilt = 0;
            foreach (var x in a.Works.At(Node)) { if (x.Built) wore &= x.LifeMil == 990; else { unbuilt++; wore &= x.LifeMil == 1000; } }
            check(wore && unbuilt == 1 && a.Works.Count(Node) == pieces, "al cambiar de día, las piezas construidas pierden el 1 % (1000 → 990 ‰) y las sin terminar no se tocan");
            int lvl1 = a.Works.LevelMil(Node);
            check(lvl1 < lvl0 && lvl1 == (int)(((long)lvl0 * 990 + 500) / 1000) || Math.Abs(lvl1 - lvl0 * 0.99) < 2, $"el nivel de fortificación baja con la vida: {lvl0} ‰ → {lvl1} ‰");
            a.Step();
            bool once = true; foreach (var x in a.Works.At(Node)) if (x.Built && x.LifeMil != 990) once = false;
            check(once, "el desgaste es una vez al día, no en cada tick");
            for (int d = 0; d < 3; d++) { a.Tick = (4 + d) * NetConst.DayTicks - 1; a.Step(); }
            int expect = 990; for (int d = 0; d < 3; d++) expect -= Math.Max(1, (expect * 10 + 500) / 1000);
            bool seq = true; foreach (var x in a.Works.At(Node)) if (x.Built && x.LifeMil != expect) seq = false;
            check(seq, $"tras 4 días la vida es {expect} ‰ (1 % de lo que queda cada día)");

            // mantenimiento (Hugo): una estación abastecida no se degrada y una vacía sí
            var m = Built(2); m.Services.Clear();                  // sin trenes: nadie carga ni descarga el almacén durante la prueba
            check(m.Works.MaintenanceU(Node) == 1, $"el mantenimiento de un día es ceil(1 % de Σ(coste × vida)) = 1 u para 20 u construidas: {m.Works.MaintenanceU(Node)}");
            m.Tick = 3 * NetConst.DayTicks - 1; m.Step();
            bool intact = true; foreach (var x in m.Works.At(Node)) if (x.Built && x.LifeMil != 1000) intact = false;
            check(intact && m.Stock[Node, (int)Res.Materiales] == 1, "con reservas se paga el mantenimiento (1 u) y la vida no baja");
            m.Tick = 4 * NetConst.DayTicks - 1; m.Step();
            check(m.Stock[Node, (int)Res.Materiales] == 0, "el segundo día se paga con la última u");
            m.Tick = 5 * NetConst.DayTicks - 1; m.Step();
            bool worn = true; foreach (var x in m.Works.At(Node)) if (x.Built && x.LifeMil != 990) worn = false;
            check(worn && m.Stock[Node, (int)Res.Materiales] == 0, "sin reservas no se puede pagar: se aplica la caída del 1 % (1000 → 990 ‰) y no se cobra nada");
            // el mantenimiento usa la misma Σ que el nivel: ni el depósito ni lo que está fuera del perímetro de 60 m cuestan nada
            var dp = Built(2); dp.Services.Clear();
            dp.Push(Place(dp.Tick, PieceKind.Deposito, 1000, 1000)); dp.Push(Place(dp.Tick, PieceKind.Hesco, 100_000, 0, 150_000, 0)); dp.ApplyCommands();
            dp.Works.CompleteAll(Node);
            check(dp.Works.MaintenanceU(Node) == 1, $"el depósito y un muro a más de 60 m del andén no suman al mantenimiento (siguen en 1 u): {dp.Works.MaintenanceU(Node)}");
            // el depósito no se desgasta; los muros a más de 60 m sí, aunque la estación pague el mantenimiento (Hugo)
            var fo = Built(50); fo.Services.Clear();
            fo.Push(Place(fo.Tick, PieceKind.Deposito, 1000, 1000)); fo.Push(Place(fo.Tick, PieceKind.Hesco, 100_000, 0, 110_000, 0)); fo.ApplyCommands(); fo.Works.CompleteAll(Node);
            fo.Stock[Node, (int)Res.Materiales] = 50;
            check(fo.Works.P.OutsideWearMil == 10, "el desgaste fuera del perímetro viene de desgaste_fuera_perimetro_permil_dia del CSV de Hugo (10 ‰)");
            for (int d = 1; d <= 20; d++) { fo.Tick = d * NetConst.DayTicks - 1; fo.Step(); }
            int inside = -1, outside = -1, dep = -1;
            foreach (var x in fo.Works.At(Node))
            {
                if (x.Kind == PieceKind.Deposito) dep = x.LifeMil;
                else if (Math.Abs((x.X0 + x.X1) / 2) > 60_000) outside = x.LifeMil;
                else if (x.Kind == PieceKind.Hesco && x.X0 == 0) inside = x.LifeMil;
            }
            check(inside == 1000 && dep == 1000 && outside >= 800 && outside <= 830, $"con el mantenimiento pagado 20 días: dentro 1000 ‰, depósito 1000 ‰ y el muro de fuera {outside} ‰ (~82 %)");
            var fu = Built(0); fu.Services.Clear(); fu.Stock[Node, (int)Res.Materiales] = 10;
            fu.Push(Place(fu.Tick, PieceKind.Deposito, 1000, 1000)); fu.ApplyCommands(); fu.Works.CompleteAll(Node); fu.Stock[Node, (int)Res.Materiales] = 0;
            fu.Tick = 3 * NetConst.DayTicks - 1; fu.Step();
            int dep2 = -1; foreach (var x in fu.Works.At(Node)) if (x.Kind == PieceKind.Deposito) dep2 = x.LifeMil;
            check(dep2 == 1000, "tampoco se desgasta el depósito cuando la estación no puede pagar el mantenimiento");
            var d20 = Built(1000); d20.Services.Clear();
            for (int d = 1; d <= 20; d++) { d20.Tick = d * NetConst.DayTicks - 1; d20.Step(); }
            check(d20.Stock[Node, (int)Res.Materiales] == 980 && d20.Works.At(Node)[0].LifeMil == 1000, "abastecida 20 días: paga 1 u al día (980 u quedan) y conserva el 100 % de vida");
            var sinObras = Mvp0(); sinObras.Services.Clear(); sinObras.Tick = 3 * NetConst.DayTicks - 1; int hs0 = sinObras.Stock[0, (int)Res.Materiales];
            sinObras.Step();
            check(sinObras.Stock[0, (int)Res.Materiales] == hs0, "sin obras el mantenimiento no cobra nada");

            // determinismo y hash
            var b = Built(); var c = Built();
            foreach (var q in new[] { b, c }) { q.Tick = 3 * NetConst.DayTicks - 1; q.Step(); }
            check(b.Hash() == c.Hash() && b.Hash() != Built().Hash(), "dos mundos con la misma obra dan el mismo hash tras el desgaste, distinto del de antes");

            // sin obras no cambia nada: los goldens no se mueven
            var e0 = Mvp0(); e0.Tick = 3 * NetConst.DayTicks - 1; ulong h = e0.Hash(); long t = e0.Tick;
            check(e0.Works.WearDay(0) == 0 && e0.Hash() == h, "sin obras el desgaste no cambia el hash");

            // destrucción: una pieza a 0 ‰ se quita y la obra sigue
            var z = Built(); int n0 = z.Works.Count(Node); int idx = -1;
            for (int i = 0; i < z.Works.At(Node).Count; i++) if (z.Works.At(Node)[i].Built) { idx = i; break; }
            bool half = !z.Works.Damage(Node, idx, 400) && z.Works.At(Node)[idx].LifeMil == 600;
            check(half, "un daño de 400 ‰ deja la pieza a 600 ‰");
            bool gone = z.Works.Damage(Node, idx, 600) && z.Works.Count(Node) == n0 - 1;
            check(gone, "un daño que agota la vida destruye la pieza (se quita; la interfaz pone los escombros)");
            int unb = -1; for (int i = 0; i < z.Works.At(Node).Count; i++) if (!z.Works.At(Node)[i].Built) unb = i;
            check(!z.Works.Damage(Node, unb, 1000) && z.Works.Count(Node) == n0 - 1, "una pieza sin terminar no recibe daño");

            // el desgaste a lo largo de 20 días deja la vida por encima del 80 % y el nivel acorde (Hugo: 1 %/día)
            var y = Built();
            for (int d = 1; d <= 20; d++) { y.Tick = d * NetConst.DayTicks - 1; y.Step(); }
            int life = 0; foreach (var x in y.Works.At(Node)) if (x.Built) life = x.LifeMil;
            check(life >= 800 && life <= 830, $"tras 20 días la pieza conserva ~82 % de vida: {life} ‰");
        }
    }
}
