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

        public long Value(string key)
        {
            int kc = Col("clave"), vc = Col("valor");
            foreach (var r in Rows) if (r[kc].Trim() == key) return Int(r[vc]);
            throw new KeyNotFoundException("Clave «" + key + "» no está en el CSV");
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
