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
            var back = WorldWithZone(); back.Works.Restore(Node, zone);
            check(back.Works.Hash() == wl.Works.Hash() && back.Works.LevelMil(Node) == wl.Works.LevelMil(Node), "materializar y desmaterializar (copiar y devolver) no cambia el hash ni el nivel");
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
        }
    }
}
