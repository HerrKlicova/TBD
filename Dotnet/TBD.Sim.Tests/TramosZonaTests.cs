using System;
using System.Collections.Generic;
using TBD.Net;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>[32] La zona usa un NightSim «sombra» como dueño de los tramos (D-N163, Marcos 86bbd5d): ApplySegmentPile, TickWallJobs, eventos y materiales de la red.</summary>
    public static class TramosZonaTests
    {
        static int Count(List<NightSim.WallEvent> l, NightSim.WallEventKind k) { int n = 0; foreach (var e in l) if (e.Kind == k) n++; return n; }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[32] Tramos del muro desde la zona (NightSim sombra)");
            var w = NetScenario.Mvp(); int sag = -1; foreach (var nd in w.Graph.Nodes) if (nd.Name == "Sagunto") sag = nd.Id;
            var pieces = NightSim.PiecesFromWorks(w.Works.Snapshot(sag), 113000, 64500, 11924);
            var old = UnidadesTests.SaguntoPieces(11924); bool same = old.Count == pieces.Count;
            for (int i = 0; same && i < old.Count; i++) same = old[i].Side == pieces[i].Side && old[i].A0 == pieces[i].A0 && old[i].A1 == pieces[i].A1 && old[i].HpMax == pieces[i].HpMax && old[i].PriceU == pieces[i].PriceU;
            check(same && pieces.Count == 31, $"PiecesFromWorks da los mismos 31 tramos que las corridas de Hugo ({pieces.Count})");

            var src = NightSim.FromJson(BalanceSource.Read("noche_sagunto.json"), null, 1);
            var sh = NightSim.ShadowFor(src.Night.P, src.SP, pieces, 40);
            long tot = 0; foreach (var g in sh.Segments) tot += g.HpMax;
            check(sh.Shadow && sh.WallHp == tot && sh.SP.WallHp == tot && sh.MaterialsU == 40, $"la sombra empieza con los tramos llenos ({tot}) y los materiales de la red");
            ulong h0 = sh.Hash();

            // daño ×1: 30 zombis pegados 30 ticks (1 s) = 30 de vida
            int k = 3; var pile = new int[sh.Segments.Count]; pile[k] = 30; int hp0 = sh.Segments[k].Hp;
            for (int t = 0; t < 30; t++) sh.ApplySegmentPile(pile);
            check(sh.Segments[k].Hp == hp0 - 30 && sh.WallHp == tot - 30 && sh.Hash() != h0, $"ApplySegmentPile: 30 zombis durante 1 s quitan 30 de vida (×1) al tramo {k}");
            check(sh.ApplySegmentPile(new int[2]).Count == 0 && sh.Segments[k].Hp == hp0 - 30 - 0, "una pila de otra longitud no hace nada");
            sh.Segments[k].Hp = 2; sh.SumForTests(); int br = 0, ticks = 0;
            for (; ticks < 200 && sh.Segments[k].Hp > 0; ticks++) br += Count(sh.ApplySegmentPile(pile), NightSim.WallEventKind.Brecha);
            for (int t = 0; t < 30; t++) br += Count(sh.ApplySegmentPile(pile), NightSim.WallEventKind.Brecha);
            check(sh.Segments[k].Hp == 0 && br == 1, $"la brecha sale una sola vez como evento (en {ticks} ticks)");

            // reparar desde la zona
            int r = 0; for (int i = 1; i < sh.Segments.Count; i++) if (i != k && sh.Segments[i].HpMax > sh.Segments[r].HpMax) r = i; sh.Segments[r].Hp = sh.Segments[r].HpMax - 30000; sh.SumForTests(); int mat0 = sh.MaterialsU;
            check(sh.RepairBlockReason(r) == null && sh.Repair(r) && sh.CurrentJobSeg == r, "en la sombra se puede mandar reparar (los zapadores los cuenta la zona)");
            var none = new List<Target>();
            check(sh.TickWallJobs(4, false, none).Count == 0 && sh.Segments[r].Hp == sh.Segments[r].HpMax - 30000, "si los zapadores aún no han llegado, no se repara");
            var ev = sh.TickWallJobs(4, true, none);
            check(Count(ev, NightSim.WallEventKind.ObraEmpieza) == 1, "al llegar: ObraEmpieza (la zona les quita el fuego)");
            int rt = 1, rep = 0; for (; rt < 5000 && sh.CurrentJobSeg == r; rt++) rep += Count(sh.TickWallJobs(4, true, none), NightSim.WallEventKind.Reparado);
            // cifras de Hugo: cada zapador pone 1 u cada 20 s y 1 u = 5.000 de vida → 4 zapadores, 1.000 de vida por segundo: 30.000 en 30 s = 900 ticks y 4 × 30 / 20 = 6 u
            check(rep == 1 && sh.Segments[r].Hp == sh.Segments[r].HpMax && rt == 900, $"Reparado al llenarse: 30.000 de vida con 4 zapadores en {rt} ticks (900)");
            check(sh.MaterialsU == mat0 - 6 && sh.MaterialsSpentU == 6, $"reparar gasta 6 u ({mat0 - sh.MaterialsU}) y MaterialsSpentU lo cuenta para la red");

            // se cancela con zombis a 3 m o sin zapadores
            sh.Segments[r].Hp -= 1000; sh.SumForTests(); sh.Repair(r); int sx, sy; sh.WorkSpot(r, out sx, out sy);
            var g5 = sh.Segments[r]; int zx = g5.Side == 0 ? sh.SP.FortX0 - 2000 : g5.Side == 1 ? sh.SP.FortX1 + 2000 : (g5.A0 + g5.A1) / 2, zy = g5.Side == 2 ? sh.SP.FortY0 - 2000 : g5.Side == 3 ? sh.SP.FortY1 + 2000 : (g5.A0 + g5.A1) / 2;
            var near = new List<Target> { new Target { Id = 1, X = zx, Y = zy } };
            check(Count(sh.TickWallJobs(4, true, near), NightSim.WallEventKind.ObraCancelada) == 1 && sh.CurrentJobSeg == -1, "un zombi a 2 m del tramo cancela la obra (ObraCancelada)");
            sh.TickWallJobs(4, false, none); sh.Repair(r); check(Count(sh.TickWallJobs(0, true, none), NightSim.WallEventKind.ObraCancelada) == 1 && sh.CurrentJobSeg == -1, "sin zapadores vivos, también (los zombis de la regla son los del último tick de zona)");

            // reconstruir la brecha
            int mat1 = sh.MaterialsU, cost = sh.Segments[k].PriceU;
            check(sh.RebuildBlockReason(k) == null && sh.Rebuild(k), $"la brecha se puede reconstruir (precio {cost} u)");
            int bt = 0, reb = 0; for (; bt < 200000 && sh.CurrentJobSeg == k; bt++) reb += Count(sh.TickWallJobs(4, true, none), NightSim.WallEventKind.Reconstruido);
            check(reb == 1 && sh.Segments[k].Hp == (int)((long)sh.Segments[k].HpMax * sh.RebuildPermil / 1000) && sh.MaterialsU == mat1 - cost, $"Reconstruido al 30 % y gasta el precio de la pieza ({bt} ticks)");

            // determinismo: dos sombras con las mismas llamadas dan el mismo hash
            var a = NightSim.ShadowFor(src.Night.P, src.SP, pieces, 40); var b = NightSim.ShadowFor(src.Night.P, src.SP, pieces, 40);
            foreach (var s in new[] { a, b }) { for (int t = 0; t < 900; t++) s.ApplySegmentPile(pile); s.Segments[r].Hp -= 500; s.SumForTests(); s.Repair(r); for (int t = 0; t < 300; t++) s.TickWallJobs(2, t > 20, none); }
            check(a.Hash() == b.Hash() && a.WallHp == b.WallHp, "dos sombras con la misma secuencia dan el mismo hash");

            // los materiales viven en la red: CmdSpendMaterials
            int st0 = w.Stock[sag, (int)Res.Materiales]; ulong wh = w.Hash();
            w.Push(NetWorld.CmdSpendMaterials(w.Tick, sag, 3)); w.Step();
            check(w.Stock[sag, (int)Res.Materiales] == Math.Max(0, st0 - 3) && (st0 == 0 || w.Hash() != wh), $"CmdSpendMaterials resta los materiales de Sagunto ({st0} → {w.Stock[sag, (int)Res.Materiales]})");
            check(w.SpendMaterials(sag, 1 << 30) == Math.Max(0, w.Stock[sag, (int)Res.Materiales] + 0) || w.Stock[sag, (int)Res.Materiales] == 0, "nunca baja de 0");
            check(w.Stock[sag, (int)Res.Materiales] == 0 && w.SpendMaterials(sag, 5) == 0 && w.SpendMaterials(-1, 5) == 0, "sin existencias o sin nodo no gasta nada");
        }
    }
}
