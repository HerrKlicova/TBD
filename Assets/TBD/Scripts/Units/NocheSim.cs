using System;
using System.Collections.Generic;
using TBD.Net;

namespace TBD.Units
{
    // Noche de Sagunto SIN VENTANA (petición de Sara para el jugador automático): crea la noche desde noche_sagunto.json, deja dar órdenes a los
    // pelotones, avanza N ticks y da estadísticas por oleada. Solo C# puro, sin Unity, determinista (hash).
    // La «zona» es una abstracción mínima y declarada (NightSimParams, valores provisionales de Hugo): los zombis caminan hacia el edificio de viajeros,
    // se amontonan contra el muro (la pila), lo desgastan y, si cae, entran; los que están en contacto con un soldado lo abaten con una probabilidad.
    // No sustituye a la zona de Unity: sirve para medir munición, bajas y equilibrio sin ventana.

    public struct NightSimParams
    {
        public int FortX0, FortY0, FortX1, FortY1;      // recinto en mm (X 70–156 m, Y 44–85 m)
        public int StationX, StationY;                  // edificio de viajeros y tren, en el centro del andén
        public int StationHp, WallHp;
        public int ZombieSpeedMmS;
        public int WallDmgPerZombieS;                   // desgaste del muro por zombi pegado, por segundo
        public int StationDmgPerZombieS;
        public int ContactRadiusMm, ContactCasualtyPmPerS;  // soldado en contacto con un zombi: ‰ de caer por zombi y segundo
        public int PileRadiusMm;                        // «pegado al muro»
        /// <summary>QA-35 (Nacho, 7-nov): la pila trepa. En la zona los muros miden 2,5 m y cada zombi apilado suma 0,35 m (hasta 10 por celda de 0,5 m): con unos 6 por celda
        /// los de arriba saltan el muro sin romperlo. Aquí: con más de ClimbPileZ zombis pegados al muro, saltan dentro ClimbPermilS ‰ por segundo de los que sobran
        /// (los últimos en llegar). ClimbPermilS = 0: no trepan.</summary>
        public int ClimbPileZ, ClimbPermilS;
        public int GateGapMm;                           // hueco de la vía en el muro oeste (en la zona de Unity la puerta de vía no tiene sólido: 3.200). 0 = recinto cerrado (lo medido hasta ahora)

        public static NightSimParams Default()
        {
            return new NightSimParams
            {
                FortX0 = 70000, FortX1 = 156000, FortY0 = 44000, FortY1 = 85000, StationX = 113000, StationY = 64500,
                StationHp = 60000, WallHp = 1200000, ZombieSpeedMmS = 1500, WallDmgPerZombieS = 1, StationDmgPerZombieS = 20,
                ContactRadiusMm = 1200, ContactCasualtyPmPerS = 14, PileRadiusMm = 1500, ClimbPileZ = 150, ClimbPermilS = 20,
            };
        }

        /// <summary>Lee de nightsim_params.json (Hugo) el objeto «NightSimParams» sobre los valores por defecto del código; lo que no esté se queda como está.</summary>
        public static NightSimParams FromJson(string json)
        {
            var p = Default(); if (json == null) return p;
            var root = Mini.Parse(json) as Dictionary<string, object>; if (root == null || !root.ContainsKey("NightSimParams")) return p;
            var o = root["NightSimParams"] as Dictionary<string, object>; if (o == null) return p;
            Func<string, int, int> g = (k, d) => o.ContainsKey(k) && o[k] is long ? (int)(long)o[k] : d;
            p.FortX0 = g("FortX0", p.FortX0); p.FortX1 = g("FortX1", p.FortX1); p.FortY0 = g("FortY0", p.FortY0); p.FortY1 = g("FortY1", p.FortY1); p.StationX = g("StationX", p.StationX); p.StationY = g("StationY", p.StationY);
            p.WallHp = g("WallHp", p.WallHp); p.WallDmgPerZombieS = g("WallDmgPerZombieS", p.WallDmgPerZombieS); p.StationHp = g("StationHp", p.StationHp); p.StationDmgPerZombieS = g("StationDmgPerZombieS", p.StationDmgPerZombieS);
            p.ZombieSpeedMmS = g("ZombieSpeedMmS", p.ZombieSpeedMmS); p.ContactRadiusMm = g("ContactRadiusMm", p.ContactRadiusMm); p.ContactCasualtyPmPerS = g("ContactCasualtyPmPerS", p.ContactCasualtyPmPerS); p.PileRadiusMm = g("PileRadiusMm", p.PileRadiusMm); p.GateGapMm = g("GateGapMm", p.GateGapMm); p.ClimbPileZ = g("ClimbPileZ", p.ClimbPileZ); p.ClimbPermilS = g("ClimbPermilS", p.ClimbPermilS);
            return p;
        }
    }

    public struct NightSnapshot
    {
        public int Tick, Hour, Minute;
        public NightState State;
        public int ZombiesSpawned, ZombiesAlive, Kills;
        public int PlatoonAmmoShots, StockShots;        // munición restante (disparos equivalentes; 1 u = 2.000)
        public int PileAtWall, PeakPile;
        public int SoldierDeaths, SoldiersAlive;
        public int Resupplies;
        public int WallHp, StationHp;
        public int VehicleKills;
    }

    public sealed class WaveStats
    {
        public int Wave, StartTick, EndTick = -1;
        public int HourAtStart, MinuteAtStart;
        public int Spawned, Kills, SoldierDeaths, Resupplies;
        public int PeakPile, AmmoSpentShots;
        public int VehicleKills, VehicleResupplies;
        public int AliveAtEnd, AmmoLeftAtEnd, StockLeftAtEnd;
    }

    public enum NightVehKind : byte { Dragon = 0, Vamtac = 1, Nh90 = 2 }

    /// <summary>Vehículo que dispara en NightSim (7-nov, Mak: fuego libre desde el principio). Copia la regla de la zona (PrototypeBootstrap.Vehicles.cs, VehicleFire):
    /// blanco = el zombi vivo más cercano dentro del alcance; un disparo cada «PeriodTicks»; abate hasta «KillsPerShot» zombis a menos de «SplashMm» del blanco;
    /// reserva propia en disparos (salvas) y recarga del almacén (coste en milésimas de u proporcional a lo gastado, «ResupplyTicks» sin moverse). Fijo en su sitio.
    /// Reservas y coste de recarga completa: D-N128 (Hugo, 7-nov): Dragón 400 salvas / 2 u, VAMTAC 750 / 3 u, NH90 920 / 3 u (antes 120/0,6, 300/1,2, 400/1,3).
    /// Entero y determinista. Va en NightSim.Hash solo si hay vehículos (sin vehículos el hash no cambia).</summary>
    public sealed class NightVehicle
    {
        public string Name; public NightVehKind Kind; public int X, Y;
        public int RangeMm, PeriodTicks, KillsPerShot, SplashMm, MaxShots, ResupplyMilliU, ResupplyTicks;
        public bool Fire = true;            // fuego libre (A) / alto el fuego (G)
        public bool Airborne;               // el helicóptero solo dispara en vuelo (en la zona: State == 2)
        public int Shots, NextFireTick, ResupplyUntil = -1;
        public int HomeX, HomeY;            // NH90: su plataforma, para volver al acabar
        public int Kills, ShotsFired, Resupplies, ResupplyShotsSpent;

        public static NightVehicle Make(string name, NightVehKind k, int x, int y)
        {
            var v = new NightVehicle { Name = name, Kind = k, X = x, Y = y };
            switch (k)
            {
                case NightVehKind.Dragon: v.RangeMm = 55000; v.PeriodTicks = 30; v.KillsPerShot = 3; v.SplashMm = 3000; v.MaxShots = 400; v.ResupplyMilliU = 2000; v.ResupplyTicks = 600; break;
                case NightVehKind.Vamtac: v.RangeMm = 40000; v.PeriodTicks = 9; v.KillsPerShot = 1; v.SplashMm = 1095; v.MaxShots = 750; v.ResupplyMilliU = 3000; v.ResupplyTicks = 600; break;
                default: v.RangeMm = 70000; v.PeriodTicks = 9; v.KillsPerShot = 1; v.SplashMm = 1095; v.MaxShots = 920; v.ResupplyMilliU = 3000; v.ResupplyTicks = 900; break;
            }
            v.Shots = v.MaxShots; return v;
        }
        /// <summary>Aplica a un vehículo su entrada de «NightVehicles» (nightsim_params.json de Hugo) y le llena la reserva (Shots = MaxShots). Sin JSON o sin entrada, no cambia nada.
        /// Lo que no esté en la entrada se queda como estaba. La zona lo usa al pasar sus vehículos a NightSim (fuera de pantalla).</summary>
        public static void ApplyParams(NightVehicle v, string json)
        {
            if (v == null || json == null) return;
            var root = Mini.Parse(json) as Dictionary<string, object>; if (root == null || !root.ContainsKey("NightVehicles")) return;
            var all = root["NightVehicles"] as Dictionary<string, object>; if (all == null) return;
            object o; if (!all.TryGetValue(v.Kind.ToString(), out o)) return; var d = o as Dictionary<string, object>; if (d == null) return;
            Func<string, int, int> g = (k, def) => d.ContainsKey(k) && d[k] is long ? (int)(long)d[k] : def;
            v.RangeMm = g("RangeMm", v.RangeMm); v.PeriodTicks = Math.Max(1, g("PeriodTicks", v.PeriodTicks)); v.KillsPerShot = g("KillsPerShot", v.KillsPerShot); v.SplashMm = g("SplashMm", v.SplashMm);
            v.MaxShots = g("MaxShots", v.MaxShots); v.ResupplyMilliU = g("ResupplyMilliU", v.ResupplyMilliU); v.ResupplyTicks = g("ResupplyTicks", v.ResupplyTicks);
            if (d.ContainsKey("Airborne") && d["Airborne"] is bool) v.Airborne = (bool)d["Airborne"];
            if (d.ContainsKey("Fire") && d["Fire"] is bool) v.Fire = (bool)d["Fire"];
            v.Shots = v.MaxShots;
        }
        /// <summary>Coste de llenar la reserva, en disparos del almacén (1 u = WeaponCatalog.ShotsPerU): ResupplyMilliU × lo que falta / MaxShots.</summary>
        public int RefillCostShots() { return MaxShots <= 0 ? 0 : (int)((long)ResupplyMilliU * WeaponCatalog.ShotsPerU / 1000 * (MaxShots - Shots) / MaxShots); }
        public ulong Hash() { return IMath.Mix((ulong)(uint)Shots + ((ulong)(uint)NextFireTick << 32) + (ulong)Kind) + IMath.Mix((ulong)(uint)ResupplyUntil + ((ulong)(uint)Kills << 32) + (Fire ? 7UL : 11UL) + (Airborne ? 13UL : 0UL)); }
    }

    /// <summary>D-N148 (Lucía, 7-nov): a qué va un zombi que ha pasado el muro (trepando la pila o por una brecha). Lo vivo más cercano a menos de «seekMm», por este
    /// orden: infantería, después vehículos parados y después obreros; si no hay nadie, el punto del tren más cercano. Entero y determinista (empate: el primero de la lista).
    /// Lo usan NightSim y la zona (ECS), con las mismas listas. Devuelve 1 infantería, 2 vehículo, 3 obrero o 4 tren.</summary>
    public static class ClimberTargets
    {
        public static byte Pick(int x, int y, IList<Target> men, IList<Target> vehicles, IList<Target> workers, int seekMm, RectMm train, out int tx, out int ty)
        {
            long r2 = (long)seekMm * seekMm;
            if (Nearest(x, y, men, r2, out tx, out ty)) return 1;
            if (Nearest(x, y, vehicles, r2, out tx, out ty)) return 2;
            if (Nearest(x, y, workers, r2, out tx, out ty)) return 3;
            tx = Math.Max(train.X0, Math.Min(train.X1, x)); ty = Math.Max(train.Y0, Math.Min(train.Y1, y));
            return 4;
        }
        static bool Nearest(int x, int y, IList<Target> list, long r2, out int tx, out int ty)
        {
            tx = ty = 0; long best = long.MaxValue; bool found = false;
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                long dx = list[i].X - x, dy = list[i].Y - y, d = dx * dx + dy * dy;
                if (d <= r2 && d < best) { best = d; tx = list[i].X; ty = list[i].Y; found = true; }
            }
            return found;
        }
    }

    public sealed class NightSim
    {
        sealed class Zomb { public int X, Y; public bool Alive = true; public int TX, TY; public byte Goal; }   // Goal (D-N148): 0 sin decidir, 1 infantería, 2 vehículo, 3 obrero, 4 tren

        public NightSimParams SP;
        public readonly UnitsWorld Units;
        public readonly NightGame Night;
        public readonly List<WaveStats> Waves = new List<WaveStats>();
        public int WallHp, Kills, SoldierDeaths, Resupplies, PeakPile, Spawned;
        /// <summary>Zombis que han saltado el muro trepando por la pila (QA-35).</summary>
        public int Climbed;
        int _aliveForHeli;
        long _climbRem;
        public int StationHp { get { return Night.StationHp; } }      // la vida del edificio vive en NightGame (misma regla que el juego)
        public int MaterialsU, FuelU;
        readonly List<Zomb> _z = new List<Zomb>();
        readonly List<Target> _targets = new List<Target>();
        readonly List<int> _ids = new List<int>();
        readonly List<SpawnPoint> _spawns = new List<SpawnPoint>();
        readonly StepResult _res = new StepResult();
        readonly ContactResult _contact = new ContactResult();
        int _wallRem;
        IRng _rng;

        public NightSim(NightParams np, NightSimParams sp, int ammoU, int materialsU, int fuelU, uint seed)
        {
            SP = sp; Night = new NightGame(np, seed ^ 0x5A5A); Units = new UnitsWorld(seed); _rng = new IRng(seed ^ 0xC0FFEE);
            Units.StockShots = ammoU * WeaponCatalog.ShotsPerU; MaterialsU = materialsU; FuelU = fuelU;
            Units.Obstacles.Add(np.StationRect); Units.Obstacles.Add(np.TrainRect);     // D-N139: la infantería rodea el edificio y el tren parado
            WallHp = sp.WallHp; Night.P.StationHp = sp.StationHp; Night.StationHp = sp.StationHp; Night.P.StationDmgPerZombieS = sp.StationDmgPerZombieS;
            // QA-35: el edificio y el tren son los reales de NightParams (los mismos de la zona). Fuera, la horda converge hacia StationX/Y (la puerta de la vía, como el
            // flujo de la zona); dentro del recinto (tras trepar o con el muro roto) va al centro del edificio de viajeros.
            for (int w = 0; w < np.Waves.Length; w++) Waves.Add(new WaveStats { Wave = w + 1, StartTick = Night.WaveStartTick(w), HourAtStart = (np.StartHour + np.Waves[w].StartMinute / 60) % 24, MinuteAtStart = np.Waves[w].StartMinute % 60 });
        }

        /// <summary>Modo «fuera de pantalla» (Eugenio, 7-nov: el mundo es persistente y la noche no se para si el jugador sale al mapa o va a otra estación).
        /// Se engancha a la noche y a los pelotones QUE YA EXISTEN (los de la zona): mismo NightGame (reloj, oleadas, edificio, tren) y mismo UnitsWorld (pelotones, munición,
        /// almacén). No toca Night.P (el edificio sigue siendo el real). La vida del muro se pasa en «wallHp» (la de la zona). Los zombis vivos se pasan con ImportZombie.
        /// Al volver a la zona, ExportZombies da los vivos para crearlos en ECS y WallHp la vida del muro. Ver Docs/noche-fuera-de-pantalla.md.</summary>
        public NightSim(NightGame night, UnitsWorld units, NightSimParams sp, int wallHp, uint seed = 1)
        {
            SP = sp; Night = night; Units = units; _rng = new IRng(seed ^ 0xC0FFEE); WallHp = Math.Max(0, Math.Min(wallHp, sp.WallHp)); Attached = true; AutoPlatoonResupply = true; AutoVehicleResupply = true;      // Mak (7-nov): fuera de pantalla los vehículos también recargan solos al 30 %
            var np = night.P;
            for (int w = 0; w < np.Waves.Length; w++) Waves.Add(new WaveStats { Wave = w + 1, StartTick = night.WaveStartTick(w), HourAtStart = (np.StartHour + np.Waves[w].StartMinute / 60) % 24, MinuteAtStart = np.Waves[w].StartMinute % 60 });
        }
        /// <summary>true si se creó enganchado a una noche existente (modo fuera de pantalla).</summary>
        public readonly bool Attached;
        /// <summary>Recarga automática de los pelotones al 30 % (la regla de la zona, PrototypeBootstrap.Night.cs). Activa por defecto en el modo fuera de pantalla; con Bot no hace falta.</summary>
        public bool AutoPlatoonResupply;

        /// <summary>Añade un zombi vivo en (x, y) mm (al salir de la zona, uno por cada zombi de ECS). Devuelve su índice.</summary>
        public int ImportZombie(int x, int y) { _z.Add(new Zomb { X = x, Y = y }); return _z.Count - 1; }
        /// <summary>Los zombis vivos, en orden (al volver a la zona). La lista se vacía y se rellena.</summary>
        public void ExportZombies(List<Target> into) { into.Clear(); for (int i = 0; i < _z.Count; i++) if (_z[i].Alive) into.Add(new Target { Id = i, X = _z[i].X, Y = _z[i].Y }); }
        public int ZombiesAlive { get { return AliveZombies(); } }

        /// <summary>Crea la noche a partir del texto de noche_sagunto.json de Hugo (D-N74). «difficulty»: facil, normal o dificil (null = la del JSON).</summary>
        static StarTier Tier(Dictionary<string, object> d)
        {
            var t = new StarTier { IntegrityMinPm = (int)(long)d["integridad_min_permil"], WallMinPm = (int)(long)d["muro_min_permil"], MaxLosses = (int)(long)d["bajas_max"], AmmoMinMilliU = (int)(long)d["municion_min_permil_u"] };
            object nv; if (d.TryGetValue("sin_vehiculos_perdidos", out nv) && nv is bool) t.NoVehiclesLost = (bool)nv;
            return t;
        }

        public static NightSim FromJson(string json, string difficulty = null, uint seed = 1)
        {
            var root = Mini.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("noche_sagunto.json no es un objeto");
            if (difficulty == null) difficulty = (string)root["dificultad_por_defecto"];
            var np = NightParams.Default();
            np.ClockX = (int)(long)root["reloj_x"]; np.ScalePermil = 1000;      // los totales del JSON ya llevan la escala de la dificultad
            var waves = (List<object>)((Dictionary<string, object>)root["oleadas"])[difficulty];
            np.Waves = new WaveSpec[waves.Count];
            for (int i = 0; i < waves.Count; i++)
            {
                var o = (Dictionary<string, object>)waves[i]; var pe = (Dictionary<string, object>)o["por_entrada"];
                np.Waves[i] = new WaveSpec { StartMinute = (int)(long)o["minuto_juego"], Zombies = (int)(long)o["total"], ArrivalSeconds = (int)(long)o["llegada_s_reales"],
                    EntryCount = new[] { (int)(long)pe["O1"], (int)(long)pe["O2"], (int)(long)pe["O3"], (int)(long)pe["O4"] } };
            }
            var res0 = root.ContainsKey("resultados") ? root["resultados"] as Dictionary<string, object> : null;
            var est = res0 != null && res0.ContainsKey("estrellas") ? res0["estrellas"] as Dictionary<string, object> : null;
            if (est != null) np.Stars = new StarRules { Two = Tier(est["dos"] as Dictionary<string, object>), Three = Tier(est["tres"] as Dictionary<string, object>) };
            var rec = (Dictionary<string, object>)root["recursos_iniciales"];
            var spj = TBD.Net.BalanceSource.Read("nightsim_params.json");          // el balance de Hugo manda sobre el código; sin archivo, valen los de Default()
            var sim = new NightSim(np, NightSimParams.FromJson(spj), (int)(long)rec["municion_u"], (int)(long)rec["materiales_u"], (int)(long)rec["combustible_u"], seed);
            sim.VehicleParamsJson = spj;
            return sim;
        }

        /// <summary>Los cinco pelotones de la noche, dentro del recinto junto al muro oeste.</summary>
        /// <summary>Fuerza de salida repartida (calibración de Hugo, nightsim-calibracion.md): los del centro cubren los dos muros y los lanzagranadas no quedan a menos de su mínimo.</summary>
        public void AddDefaultForce()
        {
            Units.AddPlatoon(PlatoonKind.Fusileros, 86000, 56000, 0);
            Units.AddPlatoon(PlatoonKind.Granaderos, 113000, 64500, 0);
            Units.AddPlatoon(PlatoonKind.Ametralladores, 113000, 56000, 0);
            Units.AddPlatoon(PlatoonKind.Tiradores, 113000, 74000, 0);
            Units.AddPlatoon(PlatoonKind.Zapadores, 140000, 64500, 0);
        }

        /// <summary>Bot de reabastecimiento de Hugo: a cada pelotón que baja del 30 % de su dotación le pasa munición del almacén. Devuelve cuántos pelotones ha repuesto.</summary>
        public int ResupplyBot()
        {
            int n = 0;
            foreach (var p in Units.Platoons) if (p.AmmoCap > 0 && p.AmmoShots * 100 < p.AmmoCap * 30) if (Resupply(p.Id) > 0) n++;
            return n;
        }

        /// <summary>Reabastece a un pelotón del almacén y lo cuenta (para las estadísticas por oleada).</summary>
        public int Resupply(int platoon)
        {
            int n = Units.Resupply(platoon);
            if (n > 0) { Resupplies++; var w = Current(); if (w != null) w.Resupplies++; }
            return n;
        }

        WaveStats Current()
        {
            WaveStats cur = null;
            for (int i = 0; i < Waves.Count; i++) if (Night.Tick >= Waves[i].StartTick) cur = Waves[i];
            return cur;
        }

        static bool InRect(int x, int y, RectMm r) { return x >= r.X0 && x <= r.X1 && y >= r.Y0 && y <= r.Y1; }
        bool InFort(int x, int y) { return x > SP.FortX0 && x < SP.FortX1 && y > SP.FortY0 && y < SP.FortY1; }

        public void Run(int ticks) { for (int i = 0; i < ticks && Night.State == NightState.Jugando; i++) Step(); }

        public void Step()
        {
            if (Night.State != NightState.Jugando) return;
            // 1) la noche: reloj, oleadas y fin de partida (gana la derrota)
            Night.WallPermil = SP.WallHp > 0 ? (int)((long)WallHp * 1000 / SP.WallHp) : 0;
            Night.ZombiesAlive = AliveZombies();                               // D-N136: victoria al acabar con la horda
            Night.Step(_spawns);
            var cur = Current();
            foreach (var s in _spawns) { _z.Add(new Zomb { X = s.X, Y = s.Y }); Spawned++; if (cur != null) cur.Spawned++; }
            // 2) los zombis: caminan hacia el edificio; el muro los detiene; se cuenta la pila
            int pile = 0, speed = SP.ZombieSpeedMmS / IMath.TickRate;
            for (int i = 0; i < _z.Count; i++)
            {
                var z = _z[i]; if (!z.Alive) continue;
                int tx = SP.StationX, ty = SP.StationY;
                bool inside = InFort(z.X, z.Y);
                if (inside)
                {
                    // D-N148 (Lucía): dentro del recinto ataca lo vivo más cercano (infantería, después vehículos parados, después obreros) y solo va al tren si no queda nadie en 30 m
                    if (z.Goal == 0 || ((Night.Tick + i) % ClimberRetargetTicks) == 0) RetargetClimber(z);
                    tx = z.TX; ty = z.TY;
                }
                bool gap = SP.GateGapMm > 0 && WallHp > 0;
                bool toGate = false;
                if (gap && z.X < SP.FortX0 - 500 && !InFort(z.X, z.Y)) { tx = SP.FortX0 - 1000; toGate = true; }      // con hueco en la vía, la horda rodea el muro hasta la puerta (como el flujo de la zona)
                int dx = tx - z.X, dy = ty - z.Y, d = IMath.Dist(dx, dy);
                if (inside ? (z.Goal != 4 && d < 500) : (d < 3500 && !toGate)) continue;
                if (d < 1) continue;
                int nx = z.X + IMath.DivR((long)dx * speed, d), ny = z.Y + IMath.DivR((long)dy * speed, d);
                if (WallHp > 0 && !InFort(z.X, z.Y) && InFort(nx, ny) && !(gap && z.X <= SP.FortX0 && Math.Abs(ny - SP.StationY) < SP.GateGapMm / 2))
                {
                    // choca con el muro: se queda pegado (si entra por X se corrige X; por Y, Y)
                    if (!InFort(nx, z.Y)) ny = z.Y; else if (!InFort(z.X, ny)) nx = z.X; else { nx = z.X; ny = z.Y; }
                    if (InFort(nx, ny)) { nx = z.X; ny = z.Y; }
                }
                if (InFort(z.X, z.Y) && InRect(nx, ny, Night.P.TrainRect) && !InRect(z.X, z.Y, Night.P.TrainRect))
                {
                    // QA-35 y D-N139: el tren parado es un muro. Se rodea deslizando por su costado (primero en Y, luego en X); si no hay salida, se queda pegado
                    if (!InRect(z.X, ny, Night.P.TrainRect)) nx = z.X; else if (!InRect(nx, z.Y, Night.P.TrainRect)) ny = z.Y; else { nx = z.X; ny = z.Y; }
                }
                z.X = nx; z.Y = ny;
                if (WallHp > 0 && !InFort(z.X, z.Y) && NearWall(z.X, z.Y) && !(gap && Math.Abs(z.Y - SP.StationY) < SP.GateGapMm / 2 + 500)) pile++;      // los que cruzan el hueco no son pila
            }
            if (SP.ClimbPermilS > 0 && WallHp > 0 && pile > SP.ClimbPileZ) pile -= ClimbStep(pile - SP.ClimbPileZ);
            if (pile > PeakPile) PeakPile = pile;
            if (cur != null && pile > cur.PeakPile) cur.PeakPile = pile;
            LastPile = pile;
            // 3) desgaste del muro por la pila y del edificio por los que llegan a él
            if (WallHp > 0 && pile > 0) { _wallRem += pile * SP.WallDmgPerZombieS; int dmg = _wallRem / IMath.TickRate; _wallRem %= IMath.TickRate; WallHp = Math.Max(0, WallHp - dmg); }
            { _targets.Clear(); for (int i = 0; i < _z.Count; i++) if (_z[i].Alive) _targets.Add(new Target { Id = i, X = _z[i].X, Y = _z[i].Y }); int aS, aT; Night.CountAttackers(_targets, out aS, out aT); Night.Attack(aS, aT); }
            // 4) los pelotones disparan; los impactos abaten
            _targets.Clear(); _ids.Clear();
            for (int i = 0; i < _z.Count; i++) if (_z[i].Alive) { _targets.Add(new Target { Id = i, X = _z[i].X, Y = _z[i].Y }); }
            if (Bot != null) Bot.Step(Units, _targets, Night.Tick);          // el bot da sus órdenes antes del fuego, con la misma API que el jugador
            else if (AutoPlatoonResupply && Night.Tick % 15 == 0) ResupplyBot();   // fuera de pantalla: la misma recarga automática al 30 % que hace la zona cada 15 ticks
            int before = TotalAmmo();
            Units.Step(_targets, _res);
            foreach (var h in _res.Hits)
                if (h.TargetId >= 0 && h.TargetId < _z.Count && _z[h.TargetId].Alive) { _z[h.TargetId].Alive = false; Kills++; Units.NoteKill(h.Platoon); if (cur != null) cur.Kills++; }
            if (cur != null) cur.AmmoSpentShots += _res.AmmoSpent;
            // 4b) los vehículos (fuera de UnitsWorld; misma regla que la zona)
            if (Vehicles.Count > 0) VehiclesStep(cur);
            // 5) contacto con los soldados
            _targets.Clear(); for (int i = 0; i < _z.Count; i++) if (_z[i].Alive) _targets.Add(new Target { Id = i, X = _z[i].X, Y = _z[i].Y });
            int fell = Units.ZombieContact(_targets, Night.P.ContactRadiusMm, Night.P.ContactDmgPerTick, _contact);
            SoldierDeaths += fell; if (cur != null) cur.SoldierDeaths += fell;
            // 6) limpieza cada 64 ticks para que la lista no crezca sin fin
            if ((Night.Tick & 63) == 0) _z.RemoveAll(z => !z.Alive);
            // 7) cierre de oleada: cuando empieza la siguiente (o acaba la noche) se congelan sus estadísticas
            for (int i = 0; i < Waves.Count; i++)
            {
                bool over = (i + 1 < Waves.Count && Night.Tick >= Waves[i + 1].StartTick) || Night.State != NightState.Jugando;
                if (over && Waves[i].EndTick < 0 && Night.Tick >= Waves[i].StartTick) { Waves[i].EndTick = Night.Tick; Waves[i].AliveAtEnd = AliveZombies(); Waves[i].AmmoLeftAtEnd = PlatoonAmmo(); Waves[i].StockLeftAtEnd = Units.StockShots; }
            }
        }

        // ------------------------------------------------------------------------------------------------ vehículos
        public readonly List<NightVehicle> Vehicles = new List<NightVehicle>();
        public int VehicleKills, VehicleResupplies;
        /// <summary>Recarga automática de los vehículos al bajar de este % de su reserva (−1 = nunca; el jugador la pide con T). Por defecto la hace el bot si lo hay.</summary>
        public int VehicleResupplyBelowPct = 30;
        public bool AutoVehicleResupply;

        /// <summary>Los vehículos de la noche donde los pone la zona (InitVehicles): Dragón (99; 53), VAMTAC 1 (88; 53), VAMTAC 2 (80; 53), NH90 en el helipuerto (180; 44), en tierra.
        /// «fire»: fuego libre desde el principio (decisión de Mak, 7-nov).</summary>
        public void AddDefaultVehicles(bool fire = true)
        {
            Vehicles.Add(NightVehicle.Make("Dragón", NightVehKind.Dragon, 99000, 53000));
            Vehicles.Add(NightVehicle.Make("VAMTAC 1", NightVehKind.Vamtac, 88000, 53000));
            Vehicles.Add(NightVehicle.Make("VAMTAC 2", NightVehKind.Vamtac, 80000, 53000));
            Vehicles.Add(NightVehicle.Make("NH90", NightVehKind.Nh90, 180000, 44000));
            foreach (var v in Vehicles) v.Fire = fire;
            ApplyVehicleParams();
        }

        /// <summary>Cifras de Hugo: nightsim_params.json → «NightVehicles»: { "Dragon": { "MaxShots": 240, ... }, "Vamtac": {...}, "Nh90": {..., "Airborne": true} }. Lo que no esté se queda.</summary>
        public string VehicleParamsJson;
        /// <summary>Aplica NightVehicles (VehicleParamsJson) a todos los vehículos de la lista y les llena la reserva. Público para la zona (Marcos): Make + Add + ApplyVehicleParams()
        /// y después, si hace falta, la reserva real (Shots) de cada uno.</summary>
        public void ApplyVehicleParams() { foreach (var v in Vehicles) NightVehicle.ApplyParams(v, VehicleParamsJson); }

        /// <summary>Orden T del jugador: recarga del almacén si la reserva no está llena (&gt; 95 % no recarga, como la zona) y hay munición. Devuelve el coste en disparos (0 = no).</summary>
        public int ResupplyVehicle(int index)
        {
            if (index < 0 || index >= Vehicles.Count) return 0; var v = Vehicles[index];
            if (v.ResupplyUntil >= 0 || v.Shots * 100 > v.MaxShots * 95) return 0;
            int cost = v.RefillCostShots(); if (cost <= 0 || Units.StockShots < cost) return 0;
            Units.StockShots -= cost; v.ResupplyUntil = Night.Tick + v.ResupplyTicks; v.Resupplies++; v.ResupplyShotsSpent += cost; VehicleResupplies++;
            var w = Current(); if (w != null) w.VehicleResupplies++;
            return cost;
        }

        void VehiclesStep(WaveStats cur)
        {
            int tick = Night.Tick; _aliveForHeli = AliveZombies();
            for (int vi = 0; vi < Vehicles.Count; vi++)
            {
                var v = Vehicles[vi];
                if (v.ResupplyUntil >= 0 && tick >= v.ResupplyUntil) { v.Shots = v.MaxShots; v.ResupplyUntil = -1; }      // como la zona: al acabar, reserva llena
                if ((AutoVehicleResupply || Bot != null) && VehicleResupplyBelowPct >= 0 && v.ResupplyUntil < 0 && v.Shots * 100 < v.MaxShots * VehicleResupplyBelowPct && (tick % 60) == 0) ResupplyVehicle(vi);
                if (!v.Fire || v.Shots <= 0 || tick < v.NextFireTick) continue;
                if (v.Kind == NightVehKind.Nh90)            // como la zona (Noche50, Marcos): el NH90 despega solo con más de 30 zombis y va sobre el andén (100;64); sin zombis vuelve a su plataforma
                {
                    if (!v.Airborne && v.Fire && _aliveForHeli > 30 && v.Shots * 20 > v.MaxShots) { v.HomeX = v.X; v.HomeY = v.Y; v.X = 100000; v.Y = 64000; v.Airborne = true; }
                    else if (v.Airborne && _aliveForHeli == 0 && v.HomeX != 0) { v.X = v.HomeX; v.Y = v.HomeY; v.Airborne = false; }
                    if (!v.Airborne) continue;
                }
                long best = (long)v.RangeMm * v.RangeMm; int bi = -1;
                for (int i = 0; i < _z.Count; i++) { var z = _z[i]; if (!z.Alive) continue; long dx = z.X - v.X, dy = z.Y - v.Y, d2 = dx * dx + dy * dy; if (d2 < best) { best = d2; bi = i; } }
                if (bi < 0) continue;
                v.NextFireTick = tick + v.PeriodTicks; v.Shots--; v.ShotsFired++;
                int tx = _z[bi].X, ty = _z[bi].Y, left = v.KillsPerShot; long r2 = (long)v.SplashMm * v.SplashMm;
                for (int i = 0; i < _z.Count && left > 0; i++)
                {
                    var z = _z[i]; if (!z.Alive) continue; long dx = z.X - tx, dy = z.Y - ty;
                    if (dx * dx + dy * dy < r2) { z.Alive = false; left--; Kills++; VehicleKills++; v.Kills++; if (cur != null) { cur.Kills++; cur.VehicleKills++; } }
                }
            }
        }

        /// <summary>Bot opcional (-nightbot): si no es null, da órdenes cada tick (ataque-movimiento al muro amenazado y reabastecimiento al 30 %). Ver NocheBot.cs.</summary>
        public NightBot Bot;
        public int LastPile;

        /// <summary>QA-35: la pila trepa. De los «excess» que sobran por encima de ClimbPileZ saltan ClimbPermilS ‰ por segundo (resto entero exacto); saltan los últimos
        /// en llegar (índice más alto) y caen 1,5 m dentro del recinto, en su lado del muro. Entero y determinista. Devuelve cuántos han saltado.</summary>
        /// <summary>D-N148: cada cuántos ticks vuelve a elegir objetivo un zombi que ha pasado el muro (repartido por índice para no hacerlo todo en el mismo tick).</summary>
        public const int ClimberRetargetTicks = 15;
        /// <summary>D-N148: radio en el que un zombi dentro del recinto busca algo vivo antes de ir al tren.</summary>
        public const int ClimberSeekMm = 30000;
        readonly List<Target> _men = new List<Target>(), _vehT = new List<Target>(), _workers = new List<Target>();
        int _menTick = -1;

        void RetargetClimber(Zomb z)
        {
            if (_menTick != Night.Tick)
            {
                _menTick = Night.Tick; _men.Clear(); _vehT.Clear();
                foreach (var p in Units.Platoons) { if (p.Aboard != 0) continue; foreach (var m in p.Men) if (m.Alive) _men.Add(new Target { Id = m.Id, X = m.X, Y = m.Y }); }
                for (int v = 0; v < Vehicles.Count; v++) if (!Vehicles[v].Airborne) _vehT.Add(new Target { Id = v, X = Vehicles[v].X, Y = Vehicles[v].Y });
            }
            int tx, ty; z.Goal = ClimberTargets.Pick(z.X, z.Y, _men, _vehT, _workers, ClimberSeekMm, Night.P.TrainRect, out tx, out ty); z.TX = tx; z.TY = ty;
        }

        int ClimbStep(int excess)
        {
            long acc = _climbRem + (long)excess * SP.ClimbPermilS; int n = (int)(acc / (1000L * IMath.TickRate)); _climbRem = acc % (1000L * IMath.TickRate);
            int moved = 0;
            for (int i = _z.Count - 1; i >= 0 && moved < n; i--)
            {
                var z = _z[i]; if (!z.Alive || InFort(z.X, z.Y) || !NearWall(z.X, z.Y)) continue;
                if (z.X <= SP.FortX0) z.X = SP.FortX0 + 1500; else if (z.X >= SP.FortX1) z.X = SP.FortX1 - 1500;
                else if (z.Y <= SP.FortY0) z.Y = SP.FortY0 + 1500; else z.Y = SP.FortY1 - 1500;
                z.X = Math.Max(SP.FortX0 + 1500, Math.Min(SP.FortX1 - 1500, z.X)); z.Y = Math.Max(SP.FortY0 + 1500, Math.Min(SP.FortY1 - 1500, z.Y));
                moved++;
            }
            Climbed += moved; return moved;
        }

        bool NearWall(int x, int y)
        {
            int r = SP.PileRadiusMm;
            bool inX = x > SP.FortX0 - r && x < SP.FortX1 + r, inY = y > SP.FortY0 - r && y < SP.FortY1 + r;
            return inX && inY;     // fuera del recinto y a menos de r de él
        }

        int AliveZombies() { int n = 0; for (int i = 0; i < _z.Count; i++) if (_z[i].Alive) n++; return n; }
        int PlatoonAmmo() { int n = 0; foreach (var p in Units.Platoons) n += p.AmmoShots; return n; }
        int TotalAmmo() { return PlatoonAmmo() + Units.StockShots; }

        public NightSnapshot Snapshot()
        {
            int alive = 0; foreach (var p in Units.Platoons) alive += p.AliveCount();
            return new NightSnapshot { Tick = Night.Tick, Hour = Night.Hour, Minute = Night.Minute, State = Night.State, ZombiesSpawned = Spawned, ZombiesAlive = AliveZombies(), Kills = Kills,
                PlatoonAmmoShots = PlatoonAmmo(), StockShots = Units.StockShots, PileAtWall = LastPile, PeakPile = PeakPile, SoldierDeaths = SoldierDeaths, SoldiersAlive = alive,
                Resupplies = Resupplies, WallHp = WallHp, StationHp = StationHp, VehicleKills = VehicleKills };
        }

        /// <summary>Estrellas de la partida (D-N74 pendiente de Lucía): 0 si se pierde; 1 por ganar; +1 si el muro sigue en pie; +1 si no cae ningún soldado.</summary>
        /// <summary>Estrellas (D-N104, Lucía; umbrales de Hugo en noche_sagunto.json → resultados.estrellas): ganar = 1; 2 y 3 exigen integridad, muro, bajas y munición
        /// restante (y, en 3, ningún vehículo perdido: VehiclesLost, lo anota la zona). Se da la mayor que se cumpla; con derrota, 0.</summary>
        public int Stars()
        {
            int integrity = Night.State == NightState.Victoria ? Night.Report.IntegrityPermil : Night.IntegrityPermil;
            int wall = Night.State == NightState.Victoria ? Night.Report.WallPermil : Night.WallPermil;
            return Night.P.Stars.Stars(Night.State, integrity, wall, SoldierDeaths, AmmoMilliU(), VehiclesLost > 0);
        }
        /// <summary>Munición restante en milésimas de u: almacén + dotación de los pelotones que siguen vivos (D-N77).</summary>
        public int AmmoMilliU() { long shots = Units.StockShots; foreach (var p in Units.Platoons) if (p.AliveCount() > 0) shots += p.AmmoShots; return (int)(shots * 1000 / WeaponCatalog.ShotsPerU); }
        public int VehiclesLost;

        public ulong Hash()
        {
            ulong h = Units.Hash() ^ Night.Hash();
            h += IMath.Mix((ulong)(uint)WallHp + ((ulong)(uint)Night.StationHp << 32) + 3) + IMath.Mix((ulong)(uint)Kills + ((ulong)(uint)SoldierDeaths << 32) + 5) + IMath.Mix(_rng.S ^ 0x77);
            for (int i = 0; i < _z.Count; i++) if (_z[i].Alive) h += IMath.Mix((ulong)(uint)_z[i].X + ((ulong)(uint)_z[i].Y << 32) + (ulong)i);
            for (int i = 0; i < Vehicles.Count; i++) h += Vehicles[i].Hash() * (ulong)(2 * i + 3);     // sin vehículos, el hash es el de siempre
            return h;
        }
    }

    /// <summary>Lector mínimo de JSON (objetos, listas, textos, números —enteros como long, con decimales como double—, booleanos y null): sin dependencias, vale en Unity y en dotnet.</summary>
    public static class Mini
    {
        public static object Parse(string s)
        {
            try { int i = 0; var v = Value(s, ref i); Ws(s, ref i); if (i != s.Length) throw new FormatException("JSON con basura al final"); return v; }
            catch (IndexOutOfRangeException) { throw new FormatException("JSON truncado"); }
            catch (ArgumentOutOfRangeException) { throw new FormatException("JSON truncado"); }
        }
        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        static object Value(string s, ref int i)
        {
            Ws(s, ref i); if (i >= s.Length) throw new FormatException("JSON truncado");
            char c = s[i];
            if (c == '{')
            {
                i++; var d = new Dictionary<string, object>(); Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true) { Ws(s, ref i); var k = Str(s, ref i); Ws(s, ref i); Expect(s, ref i, ':'); d[k] = Value(s, ref i); Ws(s, ref i); if (s[i] == ',') { i++; continue; } Expect(s, ref i, '}'); return d; }
            }
            if (c == '[')
            {
                i++; var l = new List<object>(); Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true) { l.Add(Value(s, ref i)); Ws(s, ref i); if (s[i] == ',') { i++; continue; } Expect(s, ref i, ']'); return l; }
            }
            if (c == '"') return Str(s, ref i);
            if (c == 't') { i += 4; return true; }
            if (c == 'f') { i += 5; return false; }
            if (c == 'n') { i += 4; return null; }
            int st = i; if (c == '-') i++;
            while (i < s.Length && (char.IsDigit(s[i]))) i++;
            if (i < s.Length && (s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+'))
            {
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
                return double.Parse(s.Substring(st, i - st), System.Globalization.CultureInfo.InvariantCulture);       // no entra en la simulación: solo se lee
            }
            return long.Parse(s.Substring(st, i - st), System.Globalization.CultureInfo.InvariantCulture);
        }
        static void Expect(string s, ref int i, char c) { if (i >= s.Length || s[i] != c) throw new FormatException("JSON: se esperaba «" + c + "» en " + i); i++; }
        static string Str(string s, ref int i)
        {
            Expect(s, ref i, '"'); var sb = new System.Text.StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++; char e = s[i++];
                    switch (e) { case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; case 'r': sb.Append('\r'); break; case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break; default: sb.Append(e); break; }
                }
                else sb.Append(s[i++]);
            }
            i++; return sb.ToString();
        }
    }
}
