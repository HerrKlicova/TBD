using System;
using System.IO;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Balance v1 de Hugo: los CSV se leen bien (todo entero) y las fórmulas dan EXACTAMENTE los casos P1-P6 de casos_prueba.csv.</summary>
    static class BalanceTests
    {
        public static string DataDirPublic() { return DataDir(); }

        static string DataDir()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Assets", "TBD", "Data"))) d = d.Parent;
            return d == null ? null : Path.Combine(d.FullName, "Assets", "TBD", "Data");
        }

        /// <summary>«dia=10 · huella_mil=5000» → diccionario de enteros (los campos que no son enteros se ignoran).</summary>
        static System.Collections.Generic.Dictionary<string, long> Fields(string s)
        {
            var d = new System.Collections.Generic.Dictionary<string, long>();
            foreach (var part in s.Split('·'))
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                try { d[kv[0].Trim()] = CsvTable.Int(kv[1]); } catch (FormatException) { }
            }
            return d;
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[11] Balance v1 (Hugo): CSV y casos de prueba P1-P6");
            string dir = DataDir();
            check(dir != null, "se encuentra Assets/TBD/Data");
            if (dir == null) return;

            // ---- todos los CSV de balance cargan y sus valores son enteros
            int files = 0, values = 0;
            foreach (var f in new[] { "balance_economia.csv", "balance_colmena.csv", "balance_pila.csv" })
            {
                var t = new CsvTable(File.ReadAllText(Path.Combine(dir, f)));
                int vc = t.Col("valor"), sc = t.Col("escala");
                foreach (var r in t.Rows) { CsvTable.Int(r[vc]); CsvTable.Int(r[sc]); values++; }
                files++;
            }
            var nodos = new CsvTable(File.ReadAllText(Path.Combine(dir, "balance_nodos.csv")));
            foreach (var r in nodos.Rows) for (int c = 1; c < r.Length - 1; c++) { CsvTable.Int(r[c]); values++; }
            var curva = new CsvTable(File.ReadAllText(Path.Combine(dir, "colmena_curva.csv")));
            var table = new long[curva.Rows.Count];
            foreach (var r in curva.Rows) table[CsvTable.Int(r[0])] = CsvTable.Int(r[1]);
            check(files == 3 && values > 100 && table.Length >= 401, $"los CSV de balance cargan y todo es entero ({values} valores, curva de {table.Length} días)");
            bool rejects = false;
            try { CsvTable.Int("1,5"); } catch (FormatException) { rejects = true; }
            check(rejects, "un valor con decimales («1,5») se rechaza");

            bool zeroCaught = false;
            try { BalanceMath.Combat(100, 1000, 0, true, zombiesPerU: 0); } catch (ArgumentOutOfRangeException) { zeroCaught = true; }
            bool zeroCaught2 = false;
            try { BalanceMath.CityFactorMil(10, 0); } catch (ArgumentOutOfRangeException) { zeroCaught2 = true; }
            check(zeroCaught && zeroCaught2, "un 0 en un divisor del balance da un error claro, no una división por cero (QA Q11)");
            var eco = new CsvTable(File.ReadAllText(Path.Combine(dir, "balance_economia.csv")));
            var col = new CsvTable(File.ReadAllText(Path.Combine(dir, "balance_colmena.csv")));
            var pila = new CsvTable(File.ReadAllText(Path.Combine(dir, "balance_pila.csv")));

            // ---- P1: combate S0
            long bonus = eco.Value("fort_bonus_por_nivel");
            var a = BalanceMath.Combat(2400, 4000, 2000, true, fortBonusMil: bonus);
            check(a.Eff == 560 && a.SpentMil == 4000 && a.Alive == 160 && a.FortDamageMil == 1600 && a.CasualtiesMil == 640,
                $"P1a: eff {a.Eff}, gasto {a.SpentMil}, vivos {a.Alive}, daño {a.FortDamageMil}, bajas {a.CasualtiesMil} (esperado 560 · 4000 · 160 · 1600 · 640)");
            var b = BalanceMath.Combat(2400, 9000, 2000, true, fortBonusMil: bonus);
            check(b.Eff == 560 && b.SpentMil == 4285 && b.Alive == 0, $"P1b: eff {b.Eff}, gasto {b.SpentMil}, vivos {b.Alive} (esperado 560 · 4285 · 0)");
            var c1 = BalanceMath.Combat(2400, 4000, 0, false, fortBonusMil: bonus);
            check(c1.Eff == 400 && c1.SpentMil == 4000 && c1.Alive == 800 && c1.CasualtiesMil == 9600, $"P1c: eff {c1.Eff}, vivos {c1.Alive}, bajas {c1.CasualtiesMil} (esperado 400 · 800 · 9600)");

            // ---- P1d-P1f: asalto a pie limitado por cadencia (D-N27). Esperados leídos de casos_prueba.csv
            {
                var casosA = new CsvTable(File.ReadAllText(Path.Combine(dir, "casos_prueba.csv")));
                long cap = eco.PositiveValue("combate_abatidos_tirador_contacto"), bxc = eco.Value("combate_bajas_por_z_contacto");
                int n = 0;
                foreach (var row in casosA.Rows)
                {
                    if (row[0] != "P1d" && row[0] != "P1e" && row[0] != "P1f") continue;
                    var inp = Fields(row[casosA.Col("entradas")]);
                    var exp = Fields(row[casosA.Col("esperado")]);
                    var r = BalanceMath.Assault(inp["z"], inp["tiradores"], cap, bxc);
                    check(r.Killed == exp["abatidos"] && r.Contact == exp["llegan_contacto"] && r.CasualtiesMil == exp["bajas_mil"],
                        $"{row[0]}: abatidos {r.Killed}, en contacto {r.Contact}, bajas {r.CasualtiesMil} (esperado {exp["abatidos"]} · {exp["llegan_contacto"]} · {exp["bajas_mil"]})");
                    n++;
                }
                check(n == 3, $"casos_prueba.csv trae P1d, P1e y P1f ({n})");
            }

            // ---- D-N27: desde el tren parado no hay tope de cadencia (es como un muro); a pie sí
            {
                long cap = eco.PositiveValue("combate_abatidos_tirador_contacto"), bxc = eco.Value("combate_bajas_por_z_contacto");
                var tren = BalanceMath.Engage(2000, 9000, 0, false, true, 21, 400, bonus, cap, bxc);
                var pie = BalanceMath.Engage(2000, 9000, 0, false, false, 21, 400, bonus, cap, bxc);
                check(tren.Killed == 2000 && tren.Alive == 0 && !tren.Capped && tren.FortDamageMil == 0,
                    $"desde el tren parado, 21 tiradores con 9 u limpian 2.000 sin tope ({tren.Killed} abatidos, gasto {tren.SpentMil})");
                check(pie.Killed == 1134 && pie.Alive == 866 && pie.Capped && pie.CasualtiesMil == 12124,
                    $"a pie, el mismo asalto topa en la cadencia ({pie.Killed} abatidos, {pie.Alive} llegan, bajas {pie.CasualtiesMil}) = P1d");
                var poca = BalanceMath.Engage(2000, 2000, 0, false, false, 30, 400, bonus, cap, bxc);
                check(poca.Killed == 800, $"a pie y con poca munición manda la munición (2 u = 800 abatidos: {poca.Killed})");
            }

            // ---- P2: presupuesto de la Colmena. Los esperados se LEEN de casos_prueba.csv: cuando Hugo recalibra (D-N25), basta
            // con copiar sus CSV; la prueba sigue comprobando que el código y su modelo dan lo mismo.
            long h0 = col.Value("H0_zombis_dia"), k = col.Value("k_huella");
            var casos = new CsvTable(File.ReadAllText(Path.Combine(dir, "casos_prueba.csv")));
            int p2 = 0;
            foreach (var row in casos.Rows)
            {
                if (!row[0].StartsWith("P2")) continue;
                var inp = Fields(row[casos.Col("entradas")]);
                var exp = Fields(row[casos.Col("esperado")]);
                long dia = inp["dia"], huella = inp["huella_mil"];
                long root;
                long h = BalanceMath.HiveBudget(h0, table[dia], k, huella, out root);
                check(table[dia] == exp["tabla"] && root == exp["raiz_mil"] && h == exp["H"],
                    $"{row[0]}: tabla {table[dia]}, raíz {root}, H {h} (esperado {exp["tabla"]} · {exp["raiz_mil"]} · {exp["H"]})");
                p2++;
            }
            check(p2 >= 3, $"casos_prueba.csv trae los casos P2 ({p2})");
            var dif = new CsvTable(File.ReadAllText(Path.Combine(dir, "balance_dificultad.csv")));
            check(dif.Rows.Exists(r => r[0] == "normal"), "balance_dificultad.csv tiene el nivel «normal»");

            // ---- P3: memoria de tráfico, jugador rutinario (sin derrame)
            long inc = col.Value("memoria_trafico_inc"), dec = col.Value("memoria_trafico_decae_dia");
            long warn = col.Value("memoria_umbral_aviso"), amb = col.Value("memoria_umbral_emboscada"), cut = col.Value("memoria_umbral_corte");
            long m = 0;
            var got = new System.Text.StringBuilder();
            var ev = new System.Text.StringBuilder();
            for (int day = 1; day <= 10; day++)
            {
                got.Append(day > 1 ? "," : "").Append(m);
                var e = BalanceMath.TrafficEvent(m, warn, amb, cut);
                ev.Append(day > 1 ? "," : "").Append(e == BalanceMath.Event.Nada ? "-" : e == BalanceMath.Event.Aviso ? "aviso" : e == BalanceMath.Event.Emboscada ? "emboscada" : "corte");
                m = BalanceMath.TrafficMemoryNextDay(m, inc, dec);
            }
            check(got.ToString() == "0,170,314,436,540,629,704,768,822,868", "P3 memoria al pasar: " + got);
            check(ev.ToString() == "-,-,-,aviso,aviso,emboscada,emboscada,emboscada,emboscada,corte", "P3 sucesos: " + ev);

            // ---- P4: pila en 60 m de sacos (1.200 mm)
            long mm = pila.Value("pila_mm_por_zombi_metro"), corpses = pila.Value("pila_cadaveres_permil"), drop = pila.Value("pila_baja_mm_hora"), sacos = pila.Value("muro_sacos_altura_mm");
            long h1 = BalanceMath.PileStep(0, 900, 2100, 60, mm, corpses, drop);
            long h2 = BalanceMath.PileStep(h1, 0, 0, 60, mm, corpses, drop);
            long alt = BalanceMath.PileStep(h1, 600, 1000, 60, mm, corpses, drop);
            check(h1 == 740 && h1 < sacos && h2 == 590 && alt == 1206 && alt >= sacos, $"P4: hora 1 {h1} mm, hora 2 sin presión {h2} mm, alternativa {alt} mm, brecha {(alt >= sacos ? 1 : 0)} (esperado 740 · 590 · 1206 · 1)");

            // ---- P5: viajes
            long vel = eco.PositiveValue("tren_vel_kmh"), carga = eco.Value("tren_horas_carga_viaje"), utiles = eco.Value("tren_horas_utiles_dia");
            long ter = BalanceMath.RoundTripTicks(130, vel, carga), zar = BalanceMath.RoundTripTicks(310, vel, carga);
            long viajes = utiles * 1500 * 1000 / ter;
            check(ter == 12500 && zar == 21500 && viajes == 1920, $"P5: Teruel {ter} ticks, Zaragoza {zar} ticks, {viajes} viajes/día ×1000 (esperado 12500 · 21500 · 1920)");

            // ---- P6: producción de Castellón según población
            long staff = eco.PositiveValue("plantilla_ciudad"), max = eco.Value("rend_ciudad_max"), slope = eco.Value("rend_ciudad_pendiente");
            long[] pops = { 15, 30, 60, 120 }, fac = { 500, 1000, 1500, 2000 }, prod = { 30, 60, 90, 120 };
            bool p6 = true; var p6s = new System.Text.StringBuilder();
            for (int i = 0; i < pops.Length; i++)
            {
                long f = BalanceMath.CityFactorMil(pops[i], staff, max, slope);
                long p = 60 * f / 1000;
                p6s.Append(pops[i]).Append("→").Append(f).Append("/").Append(p).Append(' ');
                p6 &= f == fac[i] && p == prod[i];
            }
            check(p6, "P6 factor/producción por población: " + p6s + "(esperado 500/30 1000/60 1500/90 2000/120)");
        }
    }
}
