using System;
using System.Collections.Generic;

namespace TBD.Net
{
    // Balance v1 de Hugo (Economía, D-N21) y combate abstracto S0 con pila por nodo (D-N16). Todo en enteros: unidades y milésimas
    // («_mil»), redondeo hacia abajo, como en docs/economia/economia-balance.md §3-§5. Los números viven en Assets/TBD/Data/*.csv
    // (separador «;», cabecera, todo entero): Hugo los cambia sin tocar el código. Lógica pura: se prueba con dotnet.

    /// <summary>Tabla CSV de balance: «clave;valor;escala;nota» o filas con cabecera. Sin dependencias y en cultura invariante.</summary>
    public sealed class CsvTable
    {
        public readonly string[] Header;
        public readonly List<string[]> Rows = new List<string[]>();

        public CsvTable(string text)
        {
            var lines = text.Replace("\r", "").Split('\n');
            int i = 0;
            while (i < lines.Length && lines[i].Trim().Length == 0) i++;
            if (i >= lines.Length) throw new FormatException("CSV vacío");
            Header = lines[i].TrimStart('﻿').Split(';');
            for (i++; i < lines.Length; i++)
            {
                if (lines[i].Trim().Length == 0) continue;
                Rows.Add(lines[i].Split(';'));
            }
        }

        public int Col(string name)
        {
            for (int c = 0; c < Header.Length; c++) if (Header[c].Trim() == name) return c;
            throw new KeyNotFoundException("Columna «" + name + "» no está en el CSV");
        }

        /// <summary>Entero estricto: sin decimales ni separadores de miles (la prueba falla si Hugo mete «1,5»).</summary>
        public static long Int(string s)
        {
            s = s.Trim();
            if (s.Length == 0) throw new FormatException("Valor vacío");
            long v = 0;
            int k = 0;
            bool neg = s[0] == '-';
            if (neg) k = 1;
            for (; k < s.Length; k++)
            {
                char ch = s[k];
                if (ch < '0' || ch > '9') throw new FormatException("«" + s + "» no es un entero");
                v = v * 10 + (ch - '0');
            }
            return neg ? -v : v;
        }

        /// <summary>Para las tablas «clave;valor;escala;nota»: valor de una clave (en su escala: si escala = 1000, son milésimas).</summary>
        /// <summary>Como Value, pero exige un valor mayor que 0 (QA Q11: los divisores del balance).</summary>
        public long PositiveValue(string key) { long v = Value(key); BalanceMath.Positive(v, key); return v; }

        /// <summary>Como Value, pero con un valor por defecto si la clave no está (para claves nuevas que aún no están en el CSV de Hugo).</summary>
        public long TryValue(string key, long fallback)
        {
            int kc = Col("clave"), vc = Col("valor");
            foreach (var r in Rows) if (r[kc].Trim() == key) return Int(r[vc]);
            return fallback;
        }

        public long Value(string key)
        {
            int kc = Col("clave"), vc = Col("valor");
            foreach (var r in Rows) if (r[kc].Trim() == key) return Int(r[vc]);
            throw new KeyNotFoundException("Clave «" + key + "» no está en el CSV");
        }
    }

    /// <summary>
    /// De dónde salen los CSV de balance. Unity pone el lector al arrancar (Editor: Assets/TBD/Data; compilación: StreamingAssets/Data);
    /// los tests de dotnet, la carpeta de datos del repositorio. Sin lector, o con un archivo que falta, se usan los valores por defecto del
    /// código (que una prueba vigila que coincidan con los CSV) y se avisa por «Warn».
    /// </summary>
    public static class BalanceSource
    {
        public static Func<string, string> Reader;      // nombre de archivo → texto, o null si no existe
        public static Action<string> Warn;
        public static readonly List<string> Missing = new List<string>();

        public static string Read(string file)
        {
            string text = null;
            try { text = Reader != null ? Reader(file) : null; }
            catch (Exception e) { Warn?.Invoke("[TBD] " + file + " no se pudo leer: " + e.Message); }
            if (text == null) { if (!Missing.Contains(file)) { Missing.Add(file); Warn?.Invoke("[TBD] falta " + file + ": se usan los valores por defecto del código"); } }
            return text;
        }

        public static TrafficParams Traffic()
        {
            var t = Read("balance_colmena.csv");
            return t == null ? TrafficParams.Default : TrafficParams.FromCsv(new CsvTable(t));
        }

        public static DirectorParams Director()
        {
            var d = Read("balance_director.csv"); var c = Read("balance_colmena.csv");
            var p = d == null ? DirectorParams.Default : DirectorParams.FromCsv(new CsvTable(d));
            if (c != null) p = p.WithColmena(new CsvTable(c));
            var k = Read("balance_construccion.csv");           // hito 5: ruido de obra
            return k == null ? p : p.WithConstruccion(new CsvTable(k));
        }

        /// <summary>colmena_curva.csv: factor ×1000 por día. Null si no hay archivo (el director usa su aproximación lineal).</summary>
        public static long[] Curve()
        {
            var t = Read("colmena_curva.csv");
            if (t == null) return null;
            var table = new CsvTable(t);
            var curve = new long[table.Rows.Count];
            foreach (var r in table.Rows) { int d = (int)CsvTable.Int(r[0]); if (d >= 0 && d < curve.Length) curve[d] = CsvTable.Int(r[1]); }
            return curve;
        }

        /// <summary>Tabla «índice;valor» (colmena_estacion.csv: 30 filas por día mod 30; colmena_ruido.csv: 64 cuantiles ×1000). Null si no hay archivo.</summary>
        public static long[] Table(string file)
        {
            var t = Read(file);
            if (t == null) return null;
            var table = new CsvTable(t);
            var v = new long[table.Rows.Count];
            foreach (var r in table.Rows) { int i = (int)CsvTable.Int(r[0]); if (i >= 0 && i < v.Length) v[i] = CsvTable.Int(r[1]); }
            return v;
        }

        public static ObrasParams Obras()
        {
            var t = Read("balance_construccion.csv");
            var p = t == null ? ObrasParams.Default : ObrasParams.FromCsv(new CsvTable(t));
            var e = Read("balance_economia.csv");
            return e == null ? p : p.WithEconomia(new CsvTable(e));
        }

        public static StationParams Stations()
        {
            var e = Read("balance_economia.csv");
            return e == null ? StationParams.Default : StationParams.FromCsv(new CsvTable(e));
        }

        public static TBD.Sim.CombatParams Combat()
        {
            var t = Read("balance_combate.csv");
            return t == null ? TBD.Sim.CombatParams.Default : CombatParamsCsv.Load(t);
        }
    }

    /// <summary>Carga los parámetros del combate de la zona desde balance_combate.csv (decisión de Mak: Hugo los ajusta sin tocar C#).</summary>
    public static class CombatParamsCsv
    {
        public static TBD.Sim.CombatParams Load(string csvText)
        {
            var t = new CsvTable(csvText);
            var cp = TBD.Sim.CombatParams.Default;
            cp.ShotsPerUnit = (int)t.PositiveValue("disparos_por_u");
            cp.RifleCooldown = (int)t.PositiveValue("fusil_cadencia_ticks");
            cp.MgCooldown = (int)t.PositiveValue("ametralladora_cadencia_ticks");
            cp.RifleRange = t.PositiveValue("fusil_alcance_m");
            cp.MgRange = t.PositiveValue("ametralladora_alcance_m");
            cp.SoldierHp = (int)t.PositiveValue("soldado_vida");
            cp.ZombieDamagePerTick = (int)t.Value("zombi_danio_tick");
            cp.MeleeRange = t.PositiveValue("cuerpo_a_cuerpo_mm") / 1000f;
            cp.HitPointBlankPermil = (int)t.Value("acierto_bocajarro_permil");
            cp.HitSlopePermilPerM = (int)t.Value("acierto_pendiente_permil_m");
            cp.HitMinPermil = (int)t.Value("acierto_minimo_permil");
            cp.MgFactorPermil = (int)t.Value("ametralladora_factor_permil");
            return cp;
        }
    }

    /// <summary>Fórmulas del balance v1, en enteros y con redondeo hacia abajo.</summary>
    public static class BalanceMath
    {
        /// <summary>QA Q11: un 0 (o un negativo) en un divisor del CSV es un error de datos, no una división por cero.</summary>
        public static void Positive(long v, string name)
        {
            if (v <= 0) throw new ArgumentOutOfRangeException(name, v, "El balance necesita un valor mayor que 0 en «" + name + "»");
        }

        public static long ISqrt(long n)
        {
            if (n <= 0) return 0;
            long x = (long)Math.Sqrt(n);                    // aproximación y corrección exacta: el resultado es el mismo en cualquier máquina
            while (x * x > n) x--;
            while ((x + 1) * (x + 1) <= n) x++;
            return x;
        }

        public struct CombatResult { public long Eff, SpentMil, Alive, FortDamageMil, CasualtiesMil; }

        /// <summary>
        /// Combate abstracto S0 (§5): eficacia = zombis por u × (1 + bonus × nivel). Gasta la munición necesaria (o toda la que hay),
        /// los no abatidos dañan la fortificación y causan bajas (×3 sin muros).
        /// </summary>
        public static CombatResult Combat(long zombies, long ammoMil, long fortLevelMil, bool hasWalls,
                                          long zombiesPerU = 400, long fortBonusMil = 200, long fortDamagePerZombieMil = 10,
                                          long casualtiesPerZombieMil = 4, long noWallMult = 3)
        {
            Positive(zombiesPerU, nameof(zombiesPerU));
            var r = new CombatResult();
            r.Eff = zombiesPerU * (1000 + fortBonusMil * fortLevelMil / 1000) / 1000;
            long neededMil = zombies * 1000 / r.Eff;
            r.SpentMil = Math.Min(ammoMil, neededMil);
            long killed = ammoMil >= neededMil ? zombies : ammoMil * r.Eff / 1000;
            r.Alive = zombies - killed;
            r.FortDamageMil = hasWalls ? r.Alive * fortDamagePerZombieMil : 0;
            r.CasualtiesMil = r.Alive * casualtiesPerZombieMil * (hasWalls ? 1 : noWallMult);
            return r;
        }

        public struct AssaultResult { public long Killed, Contact, CasualtiesMil; }

        /// <summary>
        /// Asalto A PIE o en estación sin muros ni tren (D-N27 de Hugo y Lucía): los abatidos tienen tope por cadencia (tiradores ×
        /// abatidos por tirador y asalto); los que pasan llegan al cuerpo a cuerpo y causan bajas. Tras muro o desde el tren parado
        /// no hay tope: se usa Combat.
        /// </summary>
        public static AssaultResult Assault(long zombies, long shooters, long killsPerShooter, long casualtiesPerContactMil)
        {
            var r = new AssaultResult();
            r.Killed = Math.Min(zombies, shooters * killsPerShooter);
            r.Contact = zombies - r.Killed;
            r.CasualtiesMil = r.Contact * casualtiesPerContactMil;
            return r;
        }

        public struct EngagementResult { public long Killed, Alive, SpentMil, FortDamageMil, CasualtiesMil; public bool Capped; }

        /// <summary>
        /// Regla de combate S0 completa (D-N27, Hugo y Lucía): quien dispara tras un muro de estación o desde un tren PARADO no tiene
        /// tope de cadencia (combate por munición, Combat; el tren cuenta como muro aunque la estación no tenga). A pie o en una
        /// estación sin muros ni tren hay tope por cadencia (Assault) y además manda la munición disponible.
        /// </summary>
        public static EngagementResult Engage(long zombies, long ammoMil, long fortLevelMil, bool stationWalls, bool trainStopped, long shooters,
                                              long zombiesPerU, long fortBonusMil, long killsPerShooter, long casualtiesPerContactMil)
        {
            var r = new EngagementResult();
            if (stationWalls || trainStopped)
            {
                var c = Combat(zombies, ammoMil, stationWalls ? fortLevelMil : 0, true, zombiesPerU, fortBonusMil);
                r.Killed = zombies - c.Alive; r.Alive = c.Alive; r.SpentMil = c.SpentMil;
                r.FortDamageMil = stationWalls ? c.FortDamageMil : 0;        // el tren no pierde materiales de fortificación
                r.CasualtiesMil = c.CasualtiesMil;
                return r;
            }
            var a = Assault(zombies, shooters, killsPerShooter, casualtiesPerContactMil);
            long ammoKills = ammoMil * zombiesPerU / 1000;                      // tampoco se abate más de lo que da la munición
            r.Killed = Math.Min(a.Killed, ammoKills);
            r.Capped = a.Killed < zombies;
            r.Alive = zombies - r.Killed;
            r.SpentMil = Math.Min(ammoMil, r.Killed * 1000 / zombiesPerU);
            r.CasualtiesMil = r.Alive * casualtiesPerContactMil;
            return r;
        }

        /// <summary>Presupuesto diario de la Colmena (§4), sin estación, rampa ni ruido: H0 · tabla/1000 · raíz(1 + k·huella).</summary>
        public static long HiveBudget(long h0, long tableX1000, long kHuellaMil, long huellaMil, out long rootMil)
        {
            rootMil = ISqrt((1000 + kHuellaMil * huellaMil / 1000) * 1000);
            return h0 * tableX1000 / 1000 * rootMil / 1000;
        }

        /// <summary>Memoria de tráfico de una franja tras un día: (m + inc si pasó un tren) · decae/1000.</summary>
        public static long TrafficMemoryNextDay(long memoryAtPass, long inc, long decayMil) { return (memoryAtPass + inc) * decayMil / 1000; }

        public enum Event { Nada, Aviso, Emboscada, Corte }
        public static Event TrafficEvent(long memory, long warn, long ambush, long cut)
        {
            return memory >= cut ? Event.Corte : memory >= ambush ? Event.Emboscada : memory >= warn ? Event.Aviso : Event.Nada;
        }

        /// <summary>Pila abstracta de un nodo en un pulso de 1 h (§5): sube (vivos + cadáveres) · mm / metros de muro; sin presión, baja.</summary>
        public static long PileStep(long pileMm, long alive, long killed, long wallMeters, long mmPerZombieMeter = 40, long corpsesPermil = 100, long dropMmHour = 150)
        {
            if (alive <= 0 && killed <= 0) return Math.Max(0, pileMm - dropMmHour);
            if (wallMeters <= 0) return pileMm;
            return pileMm + (alive + killed * corpsesPermil / 1000) * mmPerZombieMeter / wallMeters;
        }

        /// <summary>Ticks de un viaje de ida y vuelta (§3): a 60 km/h, 1 km = 25 ticks (1 tick = 2,4 s); más la carga y descarga.</summary>
        public static long RoundTripTicks(long km, long speedKmh, long loadHoursMil)
        {
            Positive(speedKmh, nameof(speedKmh));
            long ticksPerHour = 3600L * 1000 / NetConst.GameMsPerTick;          // 1.500
            return 2 * km * ticksPerHour / speedKmh + loadHoursMil * ticksPerHour / 1000;
        }

        /// <summary>Producción de una ciudad según su población (§2): f = pob/plantilla si falta gente; si sobra, min(máx, 1 + pendiente·(pob/req − 1)).</summary>
        public static long CityFactorMil(long pop, long staff, long maxMil = 2000, long slopeMil = 500)
        {
            Positive(staff, nameof(staff));
            if (pop <= staff) return pop * 1000 / staff;
            return Math.Min(maxMil, 1000 + slopeMil * (pop * 1000 / staff - 1000) / 1000);
        }
    }
}
