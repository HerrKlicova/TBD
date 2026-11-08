using System;
using System.Collections.Generic;
using TBD.Net;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>[34] La zona usa la sombra de NightSim para la línea de visión (D-N174, con la azotea) y para la horda que persigue a la tropa (D-N175). Lucía, 8-nov.</summary>
    public static class SombraLosTests
    {
        // tres filas del inventario de Álvaro (docs/niveles/sagunto_obstaculos_v1.csv): un muro (se salta), el edificio de viajeros y un nido
        const string Csv = "id;tipo;forma;x0_mm;z0_mm;x1_mm;z1_mm;medio_grosor_mm;altura_mm;tapa_vision;tapa_paso;fuente;nota\r\n" +
                           "muro_oeste_n;muro_hesco;seg;70000;66100;70000;85000;500;2100;si;si;x;\r\n" +
                           "edificio_viajeros;edificio;caja;96500;75000;115500;83800;0;11000;si;si;x;\r\n" +
                           "nido_o1;nido;seg;73500;50000;73500;53000;400;1200;si;no;x;\r\n" +
                           "farola;farola;seg;80000;60000;80000;60000;100;6000;no;no;x;\r\n";

        static NightSim Shadow(out List<NightSim.WallSegment> pieces)
        {
            var w = NetScenario.Mvp(); int sag = -1; foreach (var nd in w.Graph.Nodes) if (nd.Name == "Sagunto") sag = nd.Id;
            pieces = NightSim.PiecesFromWorks(w.Works.Snapshot(sag), 113000, 64500, 11924);
            var src = NightSim.FromJson(BalanceSource.Read("noche_sagunto.json"), null, 1);
            return NightSim.ShadowFor(src.Night.P, src.SP, pieces, 40);
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[34] Sombra de la zona: línea de visión (D-N174) y horda que persigue a la tropa (D-N175)");
            List<NightSim.WallSegment> pieces; var sh = Shadow(out pieces);

            // --- el inventario
            var scene = NightSim.LosSceneFromCsv(Csv);
            check(scene.Count == 2 && scene[0].HeightMm == 11000 && !scene[0].OwnWall && scene[0].R.X0 == 96500 && scene[0].R.Y1 == 83800,
                $"LosSceneFromCsv: el edificio de viajeros a 11 m; sin el muro (va por tramos) ni lo que no tapa ({scene.Count} sólidos)");
            check(scene[1].OwnWall && scene[1].R.X0 == 73500 - 1500 - 400 && scene[1].R.X1 == 73500 - 1500 + 400, "el nido se corre 1,5 m hacia el muro y cuenta como muro propio");
            check(NightSim.LosSceneFromCsv(null).Count == 0 && NightSim.LosSceneFromCsv("cabecera\nmala;fila\n").Count == 0, "sin CSV o con filas rotas, escena vacía");
            check(NightSim.IsIndirectFire("lanzagranadas") && !NightSim.IsIndirectFire("c90") && !NightSim.IsIndirectFire("fusil"), "el AG36 es tiro curvo; el C90 y el fusil, directos");

            // --- tirar por encima del muro
            int y = 76500, x0 = sh.SP.FortX0, k = -1;
            for (int i = 0; i < sh.Segments.Count; i++) if (sh.Segments[i].Side == 0 && sh.Segments[i].A0 <= y && sh.Segments[i].A1 >= y) k = i;
            check(k >= 0, $"hay un tramo del muro oeste en Y {y / 1000} (tramo {k})");
            check(sh.LineOfSight(x0 + 5000, y, x0 - 20000, y), "sin escena (EnableLineOfSight apagado), todo se ve, como antes");
            ulong h0 = sh.Hash();
            sh.EnableLineOfSight(true, scene);
            check(!sh.LineOfSight(x0 + 5000, y, x0 - 20000, y), "un fusilero a 5 m del muro no ve a un zombi a 20 m fuera: el muro (2,1 m) le tapa");
            check(sh.LineOfSight(x0 + 1500, y, x0 - 20000, y), "en la banqueta (1,5 m del muro) sí lo ve: el muro propio no tapa a 2 m");
            check(!sh.LineOfSight(x0 + 1500, y, 120000, y), "el edificio de viajeros tapa lo que hay detrás");

            // --- la azotea: boca a 12,6 m. Álvaro calcula 2,7 m con el muro como una línea; con el HESCO de 1 m de grueso sale 3,4 m
            int ax = NightSim.AzoteaSpotsMm[0], ay = NightSim.AzoteaSpotsMm[1];
            check(ay == y && ax > 96500, "la azotea está dentro del edificio de viajeros");
            check(sh.LineOfSight(ax, ay, x0 - 4000, ay, NightSim.EyeAzoteaMm) && sh.LineOfSight(ax, ay, x0 - 30000, ay, NightSim.EyeAzoteaMm), "desde la azotea se ve a 4 m y a 30 m fuera del muro");
            check(!sh.LineOfSight(ax, ay, x0 - 3000, ay, NightSim.EyeAzoteaMm) && !sh.LineOfSight(ax, ay, x0 - 1000, ay, NightSim.EyeAzoteaMm), "a 1 y a 3 m del muro, no: el muro tapa al pie de la pila");
            check(!sh.LineOfSight(ax, ay, x0 - 4000, ay), "con la boca a 1,6 m, desde el mismo sitio, no (el edificio le tapa)");
            check(sh.Hash() == h0, "la línea de visión no cambia el hash (no guarda estado)");

            // --- con brecha, el tramo deja de tapar
            var pile = new int[sh.Segments.Count]; pile[k] = 2000; int t = 0;
            for (; t < 20000 && sh.Segments[k].Hp > 0; t++) sh.ApplySegmentPile(pile);
            check(sh.Segments[k].Hp == 0 && sh.LineOfSight(x0 + 5000, y, x0 - 20000, y), $"brecha en el tramo {k} (en {t} ticks): a 5 m del muro ya se ve por el hueco");
            sh.Segments[k].Hp = 5000; sh.SumForTests(); sh.TickWallJobs(0, false, new List<Target>());
            check(!sh.LineOfSight(x0 + 5000, y, x0 - 20000, y), "reconstruido, vuelve a tapar (se sincroniza en TickWallJobs)");

            // --- D-N175: la horda de fuera persigue a la tropa de fuera (25 m, tope 30 %)
            var a = Shadow(out pieces); a.OutsideAggroMm = 25000;
            int zx = x0 - 60000, zy = 64000;
            var troops = new List<Target> { new Target { Id = 900, X = zx + 10000, Y = zy } };
            var outside = new List<Target>();
            for (int i = 0; i < 10; i++) outside.Add(new Target { Id = 100 + i, X = zx, Y = zy + i * 200 });
            outside.Add(new Target { Id = 50, X = zx - 30000, Y = zy });                 // a 40 m de la tropa
            outside.Add(new Target { Id = 51, X = x0 - 1000, Y = zy });                  // en la pila
            ulong a0 = a.Hash();
            var o = a.TickOutsideAggro(outside, troops, 1000);
            check(o.Count == 10 && o[0].ZombieId == 100 && o[0].TX == zx + 10000 && o[0].TY == zy, $"los 10 zombis a 10 m de la tropa van a por ella ({o.Count})");
            bool far = true; foreach (var q in o) if (q.ZombieId == 50 || q.ZombieId == 51) far = false;
            check(far, "el de 40 m sigue a la puerta, y el de la pila no cambia");
            check(a.Hash() != a0 && a.ZoneAggroCount == 10, "los que persiguen van en el hash de la sombra");

            var b = Shadow(out pieces); b.OutsideAggroMm = 25000;
            var o2 = b.TickOutsideAggro(outside, troops, 20);
            check(o2.Count == 6 && o2[0].ZombieId == 100 && o2[5].ZombieId == 105, $"tope del 30 % de los vivos: con 20 vivos, 6 (los de Id más bajo) ({o2.Count})");
            var rev = new List<Target>(outside); rev.Reverse(); var c = Shadow(out pieces); c.OutsideAggroMm = 25000; c.TickOutsideAggro(rev, troops, 20);
            check(c.Hash() == b.Hash(), "el orden de la lista no importa: mismo resultado y mismo hash");
            check(b.TickOutsideAggro(outside, troops, 10).Count == 6, "los que ya persiguen siguen aunque el tope baje (no se les quita por el tope)");

            var moved = new List<Target>(outside); moved[0] = new Target { Id = 100, X = x0 - 1000, Y = zy }; var tr2 = new List<Target> { new Target { Id = 900, X = x0 - 3000, Y = zy } };
            var o3 = a.TickOutsideAggro(moved, tr2, 1000);
            check(o3.Count == 1 && o3[0].ZombieId == 100, "el que ya perseguía sigue aunque llegue junto al muro; los demás, sin tropa a 25 m, vuelven a la puerta");
            var inFort = new List<Target> { new Target { Id = 901, X = x0 + 5000, Y = zy } };
            check(a.TickOutsideAggro(outside, inFort, 1000).Count == 0 && a.ZoneAggroCount == 0, "la tropa de dentro del recinto no cuenta (D-N168: dentro, libre)");
            check(a.Hash() == a0, "sin nadie persiguiendo, el hash vuelve a ser el de antes");
            a.TickOutsideAggro(outside, troops, 1000); outside.RemoveAt(0);
            var o4 = a.TickOutsideAggro(outside, troops, 1000);
            check(o4.Count == 9 && o4[0].ZombieId == 101, "el que muere (ya no viene en la lista) deja de contar");
            var off = Shadow(out pieces);
            check(off.TickOutsideAggro(outside, troops, 1000).Count == 0, "con OutsideAggroMm = 0, apagado (como siempre)");
        }
    }
}
