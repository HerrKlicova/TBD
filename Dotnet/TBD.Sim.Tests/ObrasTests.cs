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

        static NetWorld WorldWithZone(int materials = 900)
        {
            var w = NetScenario.Mvp();
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
            var wNoZone = NetScenario.Mvp();
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
            var e0 = NetScenario.Mvp(); var e1 = NetScenario.Mvp();
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
            check(NetScenario.Mvp().Hash() != WorldWithZone().Hash(), "construir (y gastar materiales) mueve el hash del mundo");

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
            long badTick = NetScenario.Mvp().Tick + 2000;
            var res2 = NetGolden.Verify(gold, (world, t) => { if (t == badTick && world.Works.Count(Node) > 0) { var l = world.Works.At(Node); var x = l[0]; x.LifeMil -= 1; l[0] = x; } });
            check(!res2.Ok, $"un solo ‰ de vida cambiado a escondidas se detecta: {res2}");

            // ================================================================== paso 2: obreros, avance y cancelar
            Console.WriteLine("[16] Obras (hito 5, paso 2): asignar obreros, avance de la obra y cancelar");
            check(csv.CancelStartedMil == 500, "cancelar una obra empezada devuelve el 50 % (cancelar_obra_empezada_permil del CSV de Hugo)");

            // un nodo sin paradas en ningún horario: los trenes no cargan su población durante la prueba
            int quiet = -1;
            for (int n = 0; n < NetScenario.Mvp().Graph.Nodes.Count && quiet < 0; n++)
            {
                bool stops = false; foreach (var sv in NetScenario.Mvp().Services) foreach (var st in sv.Stops) if (st.Node == n) stops = true;
                if (!stops) quiet = n;
            }
            int StepsToBuild(int workers, int people, int[] pts, PieceKind k, int maxSteps, int node = Node)
            {
                var q = NetScenario.Mvp();
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
            check(NetScenario.Mvp().Works.P.GarrisonMin == 6 && NetScenario.Mvp().Works.P.MaxWorkersPerPiece == 4 && NetScenario.Mvp().Works.P.LevelDivU == 30, "el retén de obras (6) viene de obras_guarnicion_minima de balance_construccion.csv (NO el 12 de la producción), los u por nivel (30) de fort_materiales_por_nivel y el máximo de obreros por pieza es 4");
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
            var ph = NetScenario.Mvp(); ph.Works.Add(0, ph.Works.Preview(0, PieceKind.Sacos, ten, 900)); ph.Works.Workers[0] = 4;
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
            long bad2 = NetScenario.Mvp().Tick + 2500;
            var r4 = NetGolden.Verify(gold2, (world, t) => { if (t == bad2) world.Works.Workers[Node] += 1; });
            check(!r4.Ok, $"un obrero de más a escondidas se detecta: {r4}");

            // ================================================================== paso 3: ruido de obra en el director
            Console.WriteLine("[17] Obras (hito 5, paso 3): sondeo de obra, peso del nodo y huella");
            var dcsv = DirectorParams.Default.WithConstruccion(new CsvTable(File.ReadAllText(Path.Combine(BalanceTests.DataDirPublic(), "balance_construccion.csv"))));
            var dd0 = DirectorParams.Default;
            check(dcsv.WorkProbeMil == dd0.WorkProbeMil && dcsv.WorkProbeBudgetMil == dd0.WorkProbeBudgetMil && dcsv.WorkWeightMil == dd0.WorkWeightMil && dcsv.WorkHuellaMil == dd0.WorkHuellaMil && dd0.WorkProbeMil == 83,
                $"el ruido de obra por defecto coincide con balance_construccion.csv (sondeo {dcsv.WorkProbeMil} ‰/h, {dcsv.WorkProbeBudgetMil} ‰ del presupuesto, peso {dcsv.WorkWeightMil} ‰, huella {dcsv.WorkHuellaMil} ‰)");
            check(NetScenario.Mvp().Director.P.WorkProbeMil == 83, "el mundo carga el ruido de obra del CSV");

            NetWorld WithWork(int node, int workers = 4)
            {
                var q = NetScenario.Mvp();
                q.Works.Add(node, q.Works.Preview(node, PieceKind.Sacos, new[] { 0, 0, 50_000, 0 }, 900));
                q.Works.Workers[node] = workers;
                return q;
            }
            // huella: 0,5 por nodo con obra activa
            var ctl = NetScenario.Mvp(); long h0Huella = ctl.Director.HuellaMil(ctl);
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
            int hitsWork = 0, hitsCtl = 0; var qc = NetScenario.Mvp();
            for (int k = 0; k < 4000; k++) { qa.Tick = 40 * NetConst.DayTicks + k * 37; qc.Tick = qa.Tick; if (qa.Director.DrawSiegeTarget(qa) == 0) hitsWork++; if (qc.Director.DrawSiegeTarget(qc) == 0) hitsCtl++; }
            check(hitsWork > hitsCtl, $"con la obra el nodo sale más veces como objetivo: {hitsWork} contra {hitsCtl} de 4.000 sorteos");

            // sondeo: tasa de 83 ‰ por hora de obra, nada sin obra
            long Mass(NetWorld q) { return q.Field.Mass(); }
            var pw = WithWork(0); var pc = NetScenario.Mvp();
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
            var xa = WithWork(0); var xb = NetScenario.Mvp(); xa.Director.P.WorkHuellaMil = 0; xb.Director.P.WorkHuellaMil = 0; xa.Director.P.WorkProbeMil = 1000;
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
            check(!NetScenario.Mvp().Intel.Exists(e => e.Id.StartsWith("OC-")), "sin obras no hay avisos de obra");

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
        }
    }
}
