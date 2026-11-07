using System;
using TBD.Net;

namespace TBD.Units
{
    // Armas y pelotones según balance_unidades.csv de Hugo (D-N72). Cada arma es una fila de datos: cadencia, alcance, puntería en función de la distancia,
    // coste en «disparos equivalentes» (1 u = 2.000), explosivo (radio, letalidad, tope de bajas), perforación, despliegue y retardo de vuelo.
    // Los valores por defecto del código son los de Hugo del 6-nov noche; el CSV manda (WeaponCatalog.Load / PlatoonCatalog.Load).

    public enum WeaponId : byte
    {
        Fusil = 0, FusilPrecision = 1, FusilAntimaterial = 2, MgLigera = 3, MgEmplazada = 4, MgPesada = 5, MgPuerta = 6,
        Lanzagranadas = 7, CargaZapador = 8, Canon30 = 9, Canon30Heli = 10, Cohetes70 = 11,
        C90 = 12,                       // D-N137 (Lucía): lanzacohetes de un solo uso de los fusileros, SOLO por orden del jugador (UnitsWorld.OrderC90), nunca automático
    }

    public struct Weapon
    {
        public string Key;                  // arma_<Key>_...
        public int CadenceTicks;
        public int AmmoClass;               // 1 ligera, 2 pesada, 3 especial (en el MVP, un solo stock)
        public int RangeMm, MinRangeMm;
        public int HitBlankPm;              // puntería a quemarropa, ‰
        public int HitSlopePmPerM;          // ‰ que se pierden por metro
        public int HitMinPm;                // suelo de la puntería
        public int CostShots;               // disparos equivalentes por disparo
        public int RadiusMm;                // 0 = tiro directo
        public int LethalityPm;             // ‰ de abatir a cada zombi dentro del radio
        public int CapKills;                // tope de bajas por disparo
        public int Pierce;                  // zombis extra detrás del primero (al 50 %, solo con horda densa)
        public int DeployTicks;             // parado y con la orden de emplazar este tiempo antes de disparar; si > 0, no dispara en marcha
        public int FlightTicks;             // retardo hasta el impacto
        public int HalfConeBam;             // 0 = sin límite; si no, semi-ángulo del arco de tiro (bam16) alrededor del rumbo del pelotón
        public int MinCluster;              // explosivos de área: no se disparan con menos de estos zombis en el radio (el granadero usa su fusil)
        public bool PrefersSpecials;        // francotirador: un zombi especial a tiro va antes que cualquier otro (solo código)
        public bool Explosive { get { return RadiusMm > 0; } }
    }

    public static class WeaponCatalog
    {
        public const int ShotsPerU = 2000;
        public static Weapon[] Table = Defaults();

        static Weapon W(string key, int cad, int cls, int range, int min, int blank, int slope, int floor, int cost, int radius, int leth, int cap, int pierce, int deploy, int flight, int cone = 0, int cluster = 1)
        {
            return new Weapon { Key = key, CadenceTicks = cad, AmmoClass = cls, RangeMm = range * 1000, MinRangeMm = min * 1000, HitBlankPm = blank, HitSlopePmPerM = slope, HitMinPm = floor, CostShots = cost,
                RadiusMm = radius, LethalityPm = leth, CapKills = cap, Pierce = pierce, DeployTicks = deploy, FlightTicks = flight, HalfConeBam = cone, MinCluster = cluster, PrefersSpecials = key == "fusil_precision" || key == "fusil_antimaterial" };
        }

        static Weapon[] Defaults()
        {
            return new[]
            {
                W("fusil", 15, 1, 60, 0, 210, 3, 30, 1, 0, 0, 1, 0, 0, 0),
                W("fusil_precision", 60, 1, 160, 0, 950, 1, 600, 3, 0, 0, 1, 1, 0, 0),
                W("fusil_antimaterial", 90, 1, 200, 0, 950, 1, 650, 6, 0, 0, 1, 2, 60, 0),
                W("ametralladora_ligera", 2, 1, 70, 0, 126, 2, 18, 1, 0, 0, 1, 0, 0, 0),
                W("ametralladora_emplazada", 2, 1, 80, 0, 300, 3, 40, 2, 0, 0, 1, 1, 60, 0, cone: 5461),     // cono de 60°: 30° a cada lado
                W("ametralladora_pesada", 3, 1, 100, 0, 300, 2, 40, 3, 0, 0, 1, 1, 0, 0),
                W("ametralladora_puerta", 3, 1, 120, 0, 180, 1, 30, 1, 0, 0, 1, 0, 0, 0, cone: 16384),         // sector de 180°: 90° a cada lado
                W("lanzagranadas", 180, 3, 140, 15, 0, 0, 0, 40, 4000, 500, 25, 0, 0, 20, cluster: 6),
                W("carga_zapador", 900, 3, 10, 0, 0, 0, 0, 200, 4000, 800, 40, 0, 90, 150, cluster: 6),
                W("canon_30", 10, 2, 220, 0, 500, 1, 200, 15, 2500, 500, 10, 0, 0, 0),
                W("canon_30_heli", 5, 2, 200, 0, 450, 1, 200, 15, 2500, 500, 10, 0, 0, 0),
                W("cohetes_70", 600, 3, 220, 30, 0, 0, 0, 200, 7000, 600, 60, 0, 0, 30, cluster: 3),
                W("c90", 300, 3, 120, 10, 0, 0, 0, 2000, 5000, 500, 40, 0, 0, 15),        // Hugo, dn137: 5 m, 500 ‰, tope 40, 1 u por tiro (se paga al reponerlo)
            };
        }

        public static Weapon Get(WeaponId id) { return Table[(int)id]; }

        /// <summary>Lee las filas arma_&lt;clave&gt;_* de balance_unidades.csv; lo que falte queda con el valor por defecto.</summary>
        public static void Load(CsvTable t)
        {
            for (int i = 0; i < Table.Length; i++)
            {
                var w = Table[i]; string k = "arma_" + w.Key + "_";
                w.CadenceTicks = (int)t.TryValue(k + "cadencia_ticks", w.CadenceTicks);
                w.AmmoClass = (int)t.TryValue(k + "clase_municion", w.AmmoClass);
                w.RangeMm = (int)t.TryValue(k + "alcance_m", w.RangeMm / 1000) * 1000;
                w.MinRangeMm = (int)t.TryValue(k + "alcance_min_m", w.MinRangeMm / 1000) * 1000;
                w.HitBlankPm = (int)t.TryValue(k + "acierto_bocajarro_permil", w.HitBlankPm);
                w.HitSlopePmPerM = (int)t.TryValue(k + "acierto_pendiente_permil_m", w.HitSlopePmPerM);
                w.HitMinPm = (int)t.TryValue(k + "acierto_minimo_permil", w.HitMinPm);
                w.CostShots = (int)t.TryValue(k + "coste_disparos", w.CostShots);
                w.RadiusMm = (int)t.TryValue(k + "radio_mm", w.RadiusMm);
                w.LethalityPm = (int)t.TryValue(k + "letalidad_permil", w.LethalityPm);
                w.CapKills = (int)t.TryValue(k + "tope_bajas", w.CapKills);
                w.Pierce = (int)t.TryValue(k + "perforacion", w.Pierce);
                w.DeployTicks = (int)t.TryValue(k + "despliegue_ticks", w.DeployTicks);
                w.FlightTicks = (int)t.TryValue(k + "vuelo_ticks", w.FlightTicks);
                Table[i] = w;
            }
        }

        public static void Reset() { Table = Defaults(); }

        public static ulong Hash()
        {
            ulong h = 0xA4;
            for (int i = 0; i < Table.Length; i++)
            {
                var w = Table[i];
                h += IMath.Mix((ulong)(uint)w.CadenceTicks + ((ulong)(uint)w.RangeMm << 20) + ((ulong)(uint)w.MinRangeMm << 42) ^ (ulong)i)
                   + IMath.Mix((ulong)(uint)w.HitBlankPm + ((ulong)(uint)w.HitSlopePmPerM << 16) + ((ulong)(uint)w.HitMinPm << 32) + ((ulong)(uint)w.CostShots << 48) + 1)
                   + IMath.Mix((ulong)(uint)w.RadiusMm + ((ulong)(uint)w.LethalityPm << 24) + ((ulong)(uint)w.CapKills << 40) + ((ulong)(uint)w.Pierce << 52) + 2)
                   + IMath.Mix((ulong)(uint)w.DeployTicks + ((ulong)(uint)w.FlightTicks << 24) + ((ulong)(uint)w.HalfConeBam << 44) + ((ulong)(uint)w.MinCluster << 60) + 3);
            }
            return h;
        }
    }

    public enum PlatoonKind : byte { Fusileros = 0, Ametralladores = 1, Granaderos = 2, Tiradores = 3, Zapadores = 4 }

    public struct PlatoonSpec
    {
        public string Key;                      // peloton_<Key>_...
        public int Persons, Servers;            // servidores: ayudantes sin arma que acompañan a los emplazados
        public int[] WeaponCount;               // por WeaponId
        public int SpeedMmS;
        public int DotationPm;                  // munición propia en ‰ de u
        public int MaterialsU;
        public int Hp;
        public int C90Shots;                    // D-N137: tiros de C90 por pelotón (de un solo uso, solo por orden; no están en WeaponCount para que nadie los dispare solo)
    }

    public static class PlatoonCatalog
    {
        public static PlatoonSpec[] Table = Defaults();

        static PlatoonSpec P(string key, int persons, int servers, int speed, int dot, int mat, params (WeaponId, int)[] ws)
        {
            var c = new int[Enum.GetValues(typeof(WeaponId)).Length];
            foreach (var (w, n) in ws) c[(int)w] = n;
            return new PlatoonSpec { Key = key, Persons = persons, Servers = servers, WeaponCount = c, SpeedMmS = speed, DotationPm = dot, MaterialsU = mat, Hp = 4000, C90Shots = key == "fusileros" ? 2 : 0 };   // D-N107 (Hugo): 4000 con el contacto de 2 por zombi y tick
        }

        static PlatoonSpec[] Defaults()
        {
            return new[]
            {
                P("fusileros", 8, 0, 2500, 1500, 0, (WeaponId.Fusil, 4), (WeaponId.MgLigera, 2), (WeaponId.Lanzagranadas, 2)),
                P("ametralladores", 4, 2, 2200, 2000, 2, (WeaponId.MgEmplazada, 2)),
                P("granaderos", 6, 0, 2300, 2000, 3, (WeaponId.Lanzagranadas, 6)),
                P("tiradores", 2, 0, 2500, 300, 1, (WeaponId.FusilPrecision, 1), (WeaponId.Fusil, 1)),
                P("zapadores", 6, 0, 2300, 1500, 2, (WeaponId.Fusil, 6), (WeaponId.CargaZapador, 2)),
            };
        }

        public static PlatoonSpec Get(PlatoonKind k) { return Table[(int)k]; }

        public static void Load(CsvTable t)
        {
            for (int i = 0; i < Table.Length; i++)
            {
                var s = Table[i]; string k = "peloton_" + s.Key + "_";
                s.Persons = (int)t.TryValue(k + "personas", s.Persons);
                s.Servers = (int)t.TryValue(k + "servidores", s.Servers);
                s.SpeedMmS = (int)t.TryValue(k + "velocidad_mm_s", s.SpeedMmS);
                s.DotationPm = (int)t.TryValue(k + "dotacion_municion_permil_u", s.DotationPm);
                s.MaterialsU = (int)t.TryValue(k + "materiales_u", s.MaterialsU);
                s.Hp = (int)t.TryValue(k + "soldado_vida", s.Hp);
                s.C90Shots = (int)t.TryValue(k + "c90_tiros", s.C90Shots);
                bool any = false; var c = new int[s.WeaponCount.Length];
                for (int w = 0; w < c.Length; w++) { c[w] = (int)t.TryValue(k + "arma_" + WeaponCatalog.Table[w].Key, -1); if (c[w] >= 0) any = true; else c[w] = 0; }
                if (any) s.WeaponCount = c;
                Table[i] = s;
            }
        }

        public static void Reset() { Table = Defaults(); }

        public static ulong Hash()
        {
            ulong h = 0xB7;
            for (int i = 0; i < Table.Length; i++)
            {
                var s = Table[i];
                ulong x = IMath.Mix((ulong)(uint)s.Persons + ((ulong)(uint)s.Servers << 16) + ((ulong)(uint)s.SpeedMmS << 32) + ((ulong)(uint)s.DotationPm << 48) ^ (ulong)i)
                        + IMath.Mix((ulong)(uint)s.MaterialsU + ((ulong)(uint)s.Hp << 24) + ((ulong)(uint)s.C90Shots << 56) + 5);
                for (int w = 0; w < s.WeaponCount.Length; w++) x += IMath.Mix((ulong)(uint)s.WeaponCount[w] + ((ulong)w << 32) + ((ulong)i << 40) + 7);
                h += x;
            }
            return h;
        }
    }
}
