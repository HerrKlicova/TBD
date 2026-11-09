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
                ContactRadiusMm = 1200, ContactCasualtyPmPerS = 14, PileRadiusMm = 1500, ClimbPileZ = 150, ClimbPermilS = 240,
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
        public bool Parked;                 // D-N167: volvió solo por munición, rearmó y se queda posado hasta una orden del jugador (TakeOffOrder)
        public int AutoReturns;             // D-N167: veces que ha vuelto solo a rearmar
        public bool AtAirfield, CarriesPlatoon; public int AwayUntil = -1;   // D-N173: en el aeródromo (fuera de la zona) y tick en que llega a la zona tras el viaje (−1 = no viene)
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
        public ulong Hash() { return IMath.Mix((ulong)(uint)Shots + ((ulong)(uint)NextFireTick << 32) + (ulong)Kind) + IMath.Mix((ulong)(uint)ResupplyUntil + ((ulong)(uint)Kills << 32) + (Fire ? 7UL : 11UL) + (Airborne ? 13UL : 0UL) + (Parked ? 0x167UL << 40 : 0UL)) + (AtAirfield || AwayUntil >= 0 ? IMath.Mix(0x173UL ^ ((ulong)(uint)(AwayUntil + 1) << 8) ^ (AtAirfield ? 1UL : 0UL)) : 0UL); }
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
        /// <summary>Rodeo del edificio (Nacho y Marcos, 9-nov): un trepador DETRÁS del edificio de viajeros (y &gt; S.Y1 y x en [S.X0 − m, S.X1 + m]) cuyo objetivo
        /// queda al otro lado (ty &lt; S.Y0) no va en línea recta, porque se quedaría empujando contra la fachada trasera y contaría como atacante de la estación.
        /// Va primero, por la franja de detrás y sin cambiar de y, a la vertical de la esquina más cercana (S.X0 − m − 1 o S.X1 + m + 1, y). Sin estado: en cuanto sale de la sombra del edificio (por X o por Y),
        /// deja de aplicarse y vuelve a su objetivo. Enteros, sin azar. Devuelve true si ha cambiado (tx, ty).</summary>
        public static bool Rodeo(int x, int y, RectMm s, int m, ref int tx, ref int ty)
        {
            if (y <= s.Y1 || x < s.X0 - m || x > s.X1 + m || ty >= s.Y0) return false;
            tx = (x - (s.X0 - m)) <= ((s.X1 + m) - x) ? s.X0 - m - 1 : s.X1 + m + 1; ty = y;   // a lo largo de la franja de detrás (Marcos, 64ee102: en la zona hay un pasillo de < 1 m hasta el muro norte); 1 mm fuera de la franja para que, al llegar, ya no se aplique y no se quede parado en su propio punto
            return true;
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

    public sealed class NightSim : TBD.Net.INocheZona
    {
        sealed class Zomb { public int X, Y; public bool Alive = true; public int TX, TY; public byte Goal; public bool HitTrain, Aggro; }   // Goal (D-N148): 0 sin decidir, 1 infantería, 2 vehículo, 3 obrero, 4 tren

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
        int _wallRem; int[] _segPile;
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

        /// <summary>Crea la noche a partir del texto de noche_sagunto.json de Hugo (D-N74). «difficulty»: facil, normal o dificil (null = la del JSON). Sin estrellas (D-N149): resultados ya no se lee.</summary>
        public static NightSim FromJson(string json, string difficulty = null, uint seed = 1)
        {
            var root = Mini.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("noche_sagunto.json no es un objeto");
            if (difficulty == null) difficulty = (string)root["dificultad_por_defecto"];
            var np = NightParams.Default();
            np.ClockX = (int)(long)root["reloj_x"]; np.ScalePermil = 1000; np.NetClock = root.TryGetValue("reloj_red", out var rr) && rr is bool rb && rb;      // reloj único (D-N186): true = la noche va con el reloj de la red      // los totales del JSON ya llevan la escala de la dificultad
            var waves = (List<object>)((Dictionary<string, object>)root["oleadas"])[difficulty];
            np.Waves = new WaveSpec[waves.Count];
            for (int i = 0; i < waves.Count; i++)
            {
                var o = (Dictionary<string, object>)waves[i]; var pe = (Dictionary<string, object>)o["por_entrada"];
                np.Waves[i] = new WaveSpec { StartMinute = (int)(long)o["minuto_juego"], Zombies = (int)(long)o["total"], ArrivalSeconds = (int)(long)o["llegada_s_reales"],
                    EntryCount = new[] { (int)(long)pe["O1"], (int)(long)pe["O2"], (int)(long)pe["O3"], (int)(long)pe["O4"] } };
            }
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

        /// <summary>Comparación con la zona (8-nov): la zona solo da a los pelotones los zombis a menos de 60 m del ancla de algún pelotón (PrototypeBootstrap.Night.cs, «_tgt»).
        /// 0 (por defecto) = todos, como hasta ahora. No entra en el hash.</summary>
        public int TargetRadiusMm;
        /// <summary>D-N174: el edificio de viajeros (8 m, SUPUESTO), el tren parado (4,2 m) y, con tramos, cada tramo del muro con su altura (HeightMm; 0 = 2,1 m) como muro propio
        /// (rect de 1 m de grueso sobre la línea del muro). false = sin regla. Las alturas de los tirones: infantería 1,6 m; VAMTAC 2,3 m; Dragón 2,7 m (SUPUESTOS); el NH90 en vuelo, por encima de todo.</summary>
        public void EnableLineOfSight(bool on = true, IList<UnitsWorld.LosBlocker> scene = null)
        {
            Units.LosBlockers.Clear(); _losSeg0 = -1; if (!on) return;
            if (scene != null) Units.LosBlockers.AddRange(scene);       // D-N174: el inventario de la zona (Álvaro, sagunto_obstaculos_v1.csv), sin los muros, que van por tramos
            else
            {
                Units.LosBlockers.Add(new UnitsWorld.LosBlocker { R = Night.P.StationRect, HeightMm = LosStationMm });
                Units.LosBlockers.Add(new UnitsWorld.LosBlocker { R = Night.P.TrainRect, HeightMm = LosTrainMm });
            }
            if (Segments == null || !LosWalls) return;
            _losSeg0 = Units.LosBlockers.Count;
            foreach (var g in Segments)
            {
                int x0, y0, x1, y1; SegLine(g, out x0, out y0, out x1, out y1);
                var r = g.Side <= 1 ? new RectMm(x0 - 500, y0, x0 + 500, y1) : new RectMm(x0, y0 - 500, x1, y0 + 500);
                Units.LosBlockers.Add(new UnitsWorld.LosBlocker { R = r, HeightMm = g.HeightMm > 0 ? g.HeightMm : 2100, OwnWall = true, Off = g.Hp <= 0 });
            }
        }
        public int LosStationMm = 8000, LosTrainMm = 4200; public bool LosWalls = true; int _losSeg0 = -1;
        void LosSyncWalls() { if (_losSeg0 < 0) return; for (int i = 0; i < Segments.Count; i++) { var b = Units.LosBlockers[_losSeg0 + i]; bool off = Segments[i].Hp <= 0; if (b.Off != off) { b.Off = off; Units.LosBlockers[_losSeg0 + i] = b; } } }
        static int EyeMm(NightVehKind k) { return k == NightVehKind.Dragon ? 2900 : k == NightVehKind.Vamtac ? 2400 : 1600; }   // Álvaro (8-nov): Dragón ≈ 2,9 m (SIN VERIFICAR), VAMTAC ≈ 2,4 m
        bool NearPlatoon(int x, int y)
        {
            if (TargetRadiusMm <= 0) return true;
            long r2 = (long)TargetRadiusMm * TargetRadiusMm;
            foreach (var p in Units.Platoons) { long dx = x - p.AX, dy = y - p.AY; if (dx * dx + dy * dy < r2) return true; }
            return false;
        }

        /// <summary>La fuerza tal como la pone la zona al empezar la noche (PrototypeBootstrap.Night.cs, InitNight): los cinco junto al muro oeste, mirando al oeste.</summary>
        public void AddZoneForce()
        {
            int west = 32768;
            Units.AddPlatoon(PlatoonKind.Fusileros, 88000, 54000, west, Formation.Linea);
            Units.AddPlatoon(PlatoonKind.Ametralladores, 94000, 64000, west, Formation.Linea);
            Units.AddPlatoon(PlatoonKind.Granaderos, 98000, 57000, west, Formation.Cuna);
            Units.AddPlatoon(PlatoonKind.Tiradores, 98000, 72000, west, Formation.Cuna);
            Units.AddPlatoon(PlatoonKind.Zapadores, 88000, 76000, west, Formation.Cuna);
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
        /// <summary>D-N164: ya no se gana a las 06:00, así que una horda que no cae podría no acabar nunca. Las corridas en lote paran aquí
        /// (dos noches de reloj: a ×28 son unos 34 min reales) y cuentan la partida como «sin acabar», que NO es victoria.</summary>
        public int TickCap { get { return Math.Max(Night.TotalTicks * 2, 20 * 60 * IMath.TickRate); } }     // con el reloj ×103 la noche dura 279 s: mínimo 20 min reales
        /// <summary>Medidas para Hugo (D-N165): pico de zombis pegados al tren a la vez y zombis distintos que lo han tocado.</summary>
        public int TrainPeakAttackers, TrainTouched;
        public bool Unfinished { get { return Night.State == NightState.Jugando; } }

        // ---------------------------------------------------------------- reloj único (D-N186): la red avanza esta noche
        /// <summary>Parámetros de la noche siguiente (los pone quien monta la partida); null = repetir los de ahora.</summary>
        public NightParams? NextNightParams;
        /// <summary>INocheZona: la red llega a las 22:00. La primera noche empieza si aún no ha dado pasos; las siguientes, solo tras cerrar la anterior con victoria.
        /// Si la anterior sigue en marcha (D-N164: no se gana al amanecer, hay que acabar con la horda), sigue esa y no empieza otra.</summary>
        public void ComienzaNoche(int noche, long startNetTick)
        {
            if (Night.StartNetTick < 0 && Night.Tick == 0) { if (Night.Begin(startNetTick)) ContactTick = -1; return; }
            if (Night.State == NightState.Victoria && Night.BeginNextNight(NextNightParams ?? Night.P, startNetTick)) ContactTick = -1;
        }
        /// <summary>INocheZona: un paso por tick de red, solo con la noche empezada por la red.</summary>
        public void PasoNoche() { if (Night.StartNetTick >= 0) Step(); }
        /// <summary>Tick de la noche del primer contacto de la horda con la estación (pila en el muro o un zombi dentro del recinto); −1 si aún no. Se pone a −1 al empezar cada noche.
        /// Sale del estado de la simulación (determinista). No se guarda: tras cargar a mitad de noche puede volver a avisar una vez.</summary>
        public int ContactTick = -1;
        public bool PrimerContacto { get { return ContactTick >= 0 && ContactTick == Night.Tick && Night.StartNetTick >= 0; } }
        public ulong HashNoche() { return Hash(); }

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
            WallEvents.Clear();
            if (Segments != null) { if (_segPile == null || _segPile.Length != Segments.Count) _segPile = new int[Segments.Count]; Array.Clear(_segPile, 0, _segPile.Length); }
            LosSyncWalls();
            for (int i = 0; i < _z.Count; i++)
            {
                var z = _z[i]; if (!z.Alive) continue;
                int tx = SP.StationX, ty = SP.StationY;
                bool inside = InFort(z.X, z.Y);
                if (inside && ContactTick < 0) ContactTick = Night.Tick;                  // primer contacto de la noche (×1 automático, Lucía D-N186)
                if (inside)
                {
                    // D-N148 (Lucía): dentro del recinto ataca lo vivo más cercano (infantería, después vehículos parados, después obreros) y solo va al tren si no queda nadie en 30 m
                    if (z.Goal == 0 || ((Night.Tick + i) % ClimberRetargetTicks) == 0) RetargetClimber(z);
                    tx = z.TX; ty = z.TY;
                }
                else if (OutsideAggroMm > 0 && (z.Aggro || ((Night.Tick + i) % ClimberRetargetTicks) == 0) && (z.Aggro || !NearWall(z.X, z.Y))) AggroOutside(z);     // D-N175: la pila y los que ya están en el muro no cambian
                if (!inside && z.Aggro) tx = z.TX; ty = !inside && z.Aggro ? z.TY : ty;
                bool gap = SP.GateGapMm > 0 && WallHp > 0 && !(!inside && z.Aggro);
                bool toGate = false;
                if (gap && z.X < SP.FortX0 - 500 && !InFort(z.X, z.Y)) { tx = SP.FortX0 - 1000; toGate = true; }      // con hueco en la vía, la horda rodea el muro hasta la puerta (como el flujo de la zona)
                int dx = tx - z.X, dy = ty - z.Y, d = IMath.Dist(dx, dy);
                if (inside || z.Aggro ? (z.Goal != 4 && d < 500) : (d < 3500 && !toGate)) continue;
                if (d < 1) continue;
                int nx = z.X + IMath.DivR((long)dx * speed, d), ny = z.Y + IMath.DivR((long)dy * speed, d);
                if (!InFort(z.X, z.Y) && InFort(nx, ny) && SegIntact(z.X, z.Y) && !(gap && z.X <= SP.FortX0 && Math.Abs(ny - SP.StationY) < SP.GateGapMm / 2))
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
                if (WallHp > 0 && !InFort(z.X, z.Y) && NearWall(z.X, z.Y) && !(gap && Math.Abs(z.Y - SP.StationY) < SP.GateGapMm / 2 + 500))
                {
                    if (Segments == null) pile++;
                    else { int sg = SegmentAt(z.X, z.Y); if (sg >= 0 && Segments[sg].Hp > 0) { pile++; _segPile[sg]++; } }
                }      // los que cruzan el hueco no son pila
            }
            if (SP.ClimbPermilS > 0 && WallHp > 0 && pile > SP.ClimbPileZ) pile -= ClimbStep(pile - SP.ClimbPileZ);
            if (pile > 0 && ContactTick < 0) ContactTick = Night.Tick;
            if (pile > PeakPile) PeakPile = pile;
            if (cur != null && pile > cur.PeakPile) cur.PeakPile = pile;
            LastPile = pile;
            // 3) desgaste del muro por la pila y del edificio por los que llegan a él
            if (Segments == null) { if (WallHp > 0 && pile > 0) { _wallRem += pile * SP.WallDmgPerZombieS; int dmg = _wallRem / IMath.TickRate; _wallRem %= IMath.TickRate; WallHp = Math.Max(0, WallHp - dmg); } }
            else
            {
                DamageSegments(_segPile);
                SumSegments();
            }
            WallJobsStep();
            { _targets.Clear(); for (int i = 0; i < _z.Count; i++) if (_z[i].Alive) _targets.Add(new Target { Id = i, X = _z[i].X, Y = _z[i].Y }); int aS, aT; Night.CountAttackers(_targets, AimsStation, out aS, out aT); Night.Attack(aS, aT); SurroundedTrain(aT);
              if (aT > TrainPeakAttackers) TrainPeakAttackers = aT;
              if (aT > 0) for (int i = 0; i < _z.Count; i++) if (_z[i].Alive && !_z[i].HitTrain && Night.TouchesTrain(_z[i].X, _z[i].Y)) { _z[i].HitTrain = true; TrainTouched++; } }
            // 4) los pelotones disparan; los impactos abaten
            _targets.Clear(); _ids.Clear();
            for (int i = 0; i < _z.Count; i++) if (_z[i].Alive && NearPlatoon(_z[i].X, _z[i].Y)) { _targets.Add(new Target { Id = i, X = _z[i].X, Y = _z[i].Y }); }
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
            _targets.Clear(); for (int i = 0; i < _z.Count; i++) if (_z[i].Alive && NearPlatoon(_z[i].X, _z[i].Y)) _targets.Add(new Target { Id = i, X = _z[i].X, Y = _z[i].Y });
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
            if (v.Kind == NightVehKind.Nh90 && v.Airborne) return 0;          // D-N167: el NH90 solo rearma posado en su helipuerto (como la zona: en tierra y a menos de 12 m)
            int cost = v.RefillCostShots(); if (cost <= 0 || Units.StockShots < cost) return 0;
            Units.StockShots -= cost; v.ResupplyUntil = Night.Tick + v.ResupplyTicks; v.Resupplies++; v.ResupplyShotsSpent += cost; VehicleResupplies++;
            var w = Current(); if (w != null) w.VehicleResupplies++;
            return cost;
        }

        /// <summary>D-N167: umbral de vuelta sola del NH90 (% de su reserva).</summary>
        public int Nh90ReturnBelowPct = 30;
        /// <summary>Orden del jugador al NH90 (despegar o cualquier otra): anula el «posado» de la D-N167 y vuelve a despegar solo con horda. false si no es un NH90.</summary>
        public bool TakeOffOrder(int index)
        {
            if (index < 0 || index >= Vehicles.Count || Vehicles[index].Kind != NightVehKind.Nh90) return false; var v = Vehicles[index]; v.Parked = false;
            if (Nh90FromAirfield && v.AtAirfield && v.AwayUntil < 0) v.AwayUntil = Night.Tick + Nh90PrepS * IMath.TickRate + AirfieldTripTicks();      // D-N173: sale del aeródromo y tarda el viaje
            return true;
        }
        /// <summary>D-N173 (Lucía, 8-nov): no hay helipuerto en Sagunto. El NH90 vive en el aeródromo (un nodo del mapa): llega a la zona tras el viaje y, para rearmar, vuelve allí
        /// (ida y vuelta). Distancia SUPUESTA de 40 km hasta que Paula dé el sitio; crucero del NH90 260 km/h. El viaje va en tiempo de mapa: ticks reales = s de juego × 30 / reloj_x.</summary>
        public bool Nh90FromAirfield; public int AirfieldM = 40000, Nh90CruiseKmh = 260, Nh90PrepS = 20, Nh90LandS = 20, Nh90RearmS = 30;   // Lucía (8-nov): 20 s de preparar y despegar, 20 s de tomar tierra, 30 s de rearme
        /// <summary>D-N173: claro de 25 × 25 m (Lucía) donde se posa para desembarcar; centro SUPUESTO dentro del recinto, al este del edificio; crucero 260 km/h verificado por Paula y tiempo de bajar (D-N166).</summary>
        public int LandingX = 140000, LandingY = 76000, EmbarqueDropS = 10;
        public int AirfieldTripTicks() { long gameS = (long)AirfieldM * 3600 / 1000 / Math.Max(1, Nh90CruiseKmh); return (int)Math.Max(1, Night.TicksForGameSeconds(gameS)); }
        /// <summary>D-N173: el NH90 empieza en el aeródromo. «etaTick» ≥ 0: llega a la zona en ese tick (la ETA del mapa); −1: despega solo cuando hay horda y tarda el viaje.</summary>
        public void Nh90StartAtAirfield(int etaTick = -1) { Nh90FromAirfield = true; foreach (var v in Vehicles) if (v.Kind == NightVehKind.Nh90) { v.AtAirfield = true; v.Airborne = false; v.AwayUntil = etaTick; } }

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
                    if (Nh90FromAirfield)
                    {
                        if (v.AtAirfield)
                        {
                            if (v.AwayUntil < 0 && !v.Parked && v.Fire && _aliveForHeli > 30 && v.Shots * 20 > v.MaxShots && v.ResupplyUntil < 0) v.AwayUntil = tick + Nh90PrepS * IMath.TickRate + AirfieldTripTicks();
                            if (v.AwayUntil >= 0 && tick >= v.AwayUntil)
                            {
                                v.AtAirfield = false; v.AwayUntil = -1; v.X = 100000; v.Y = 64000; v.Airborne = true;     // entra por el borde y va sobre el andén
                                if (v.CarriesPlatoon)        // se posa en el claro y desembarca (10 s sin disparar, D-N166); luego vuelve a volar
                                { v.CarriesPlatoon = false; v.NextFireTick = tick + EmbarqueDropS * IMath.TickRate; Units.AddPlatoon(PlatoonKind.Fusileros, LandingX, LandingY, 0); if (Bot != null) { } }
                            }
                            if (v.AtAirfield) continue;
                        }
                        else if (v.Airborne && (_aliveForHeli == 0 || v.Shots * 100 < v.MaxShots * Nh90ReturnBelowPct))
                        {
                            bool rearm = _aliveForHeli > 0; v.Airborne = false; v.AtAirfield = true;      // vuelve al aeródromo; rearma al llegar (ida + rearme)
                            if (rearm) { v.Parked = true; v.AutoReturns++; v.ResupplyUntil = tick + AirfieldTripTicks() + (Nh90LandS + Nh90RearmS) * IMath.TickRate; v.Resupplies++; VehicleResupplies++; }   // Lucía: viaje + 20 s de tomar tierra + 30 s de rearme (el aeródromo pone la munición)
                            continue;
                        }
                        if (!v.Airborne) continue;
                    }
                    else if (!v.Airborne && !v.Parked && v.Fire && _aliveForHeli > 30 && v.Shots * 20 > v.MaxShots) { v.HomeX = v.X; v.HomeY = v.Y; v.X = 100000; v.Y = 64000; v.Airborne = true; }
                    else if (v.Airborne && _aliveForHeli == 0 && v.HomeX != 0) { v.X = v.HomeX; v.Y = v.HomeY; v.Airborne = false; }
                    else if (v.Airborne && v.HomeX != 0 && v.Shots * 100 < v.MaxShots * Nh90ReturnBelowPct)
                    {
                        // D-N167 (Lucía): por debajo del 30 % vuelve solo a su helipuerto, rearma y se queda posado (el pelotón que lleve sigue a bordo). Vuelo instantáneo en NightSim.
                        v.X = v.HomeX; v.Y = v.HomeY; v.Airborne = false; v.Parked = true; v.AutoReturns++; ResupplyVehicle(vi);
                    }
                    if (!v.Airborne) continue;
                }
                long best = (long)v.RangeMm * v.RangeMm; int bi = -1;
                bool los = !v.Airborne && Units.LosBlockers.Count > 0;
                for (int i = 0; i < _z.Count; i++) { var z = _z[i]; if (!z.Alive) continue; long dx = z.X - v.X, dy = z.Y - v.Y, d2 = dx * dx + dy * dy; if (d2 < best && (!los || Units.LineOfSight(v.X, v.Y, z.X, z.Y, EyeMm(v.Kind)))) { best = d2; bi = i; } }
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
        /// <summary>Rodeo del edificio (9-nov): margen = AttackReachMm + RodeoExtraMm, para que el que rodea no pase a la distancia de ataque de la fachada. Regla, fuera del hash.</summary>
        public bool RodeoOn = true; public int RodeoExtraMm = 500;
        /// <summary>D-N148: radio en el que un zombi dentro del recinto busca algo vivo antes de ir al tren.</summary>
        public const int ClimberSeekMm = 30000;
        readonly List<Target> _men = new List<Target>(), _vehT = new List<Target>(), _workers = new List<Target>();
        int _menTick = -1;

        /// <summary>D-N180 (Lucía, 9-nov): un tren sin escolta rodeado (horda en contacto y ningún soldado propio a SurroundReachMm o menos del tren) se pierde
        /// en SurroundLossGameMin minutos de juego como mucho: pierde al menos TrainHp / ese tiempo por tick (100 min de juego son 1.750 ticks con el reloj de la red). 0 = apagado.</summary>
        public int SurroundLossGameMin = 100, SurroundReachMm = 60000;      // Lucía (9-nov 22:28): 100 min de juego (~1 min real) para poder reaccionar; antes 10
        public int SurroundedTicks;
        void SurroundedTrain(int atTrain)
        {
            if (SurroundLossGameMin <= 0 || atTrain <= 0 || Night.TrainHp <= 0) return;
            var r = Night.P.TrainRect; long reach2 = (long)SurroundReachMm * SurroundReachMm;
            foreach (var p in Units.Platoons) { if (p.Aboard != 0) continue; foreach (var m in p.Men) { if (!m.Alive) continue; long dx = Math.Max(0, Math.Max(r.X0 - m.X, m.X - r.X1)), dy = Math.Max(0, Math.Max(r.Y0 - m.Y, m.Y - r.Y1)); if (dx * dx + dy * dy <= reach2) return; } }
            long lossTicks = Math.Max(1L, Night.TicksForGameSeconds((long)SurroundLossGameMin * 60));
            int floor = (int)((Night.P.TrainHp + lossTicks - 1) / lossTicks);
            SurroundedTicks++;
            Night.DamageTrain(floor - Night.LastDmgTrain);
        }

        /// <summary>D-N181: ¿va a por la estación el zombi de _targets[i]? Fuera del recinto, sí (en Nules el edificio es un tramo del muro norte: el que empuja desde
        /// fuera ataca el edificio). Dentro, solo con Goal 5 (D-N180). D181On = false vuelve a contar a todos (solo pruebas y medidas).</summary>
        bool AimsStation(int i) { if (!D181On) return true; var z = _z[_targets[i].Id]; return !InFort(z.X, z.Y) || z.Goal == 5; }
        public bool D181On = true;
        /// <summary>D-N180 (2): si el tren ya no está (destruido) o está fuera del recinto, el que ha trepado y no tiene a nadie cerca va al edificio de viajeros
        /// (Goal 5) en lugar de quedarse empujando contra el muro hacia el tren de fuera: la noche siempre acaba.</summary>
        public bool TrainReachableFromInside() { var r = Night.P.TrainRect; return Night.TrainHp > 0 && r.X1 > SP.FortX0 && r.X0 < SP.FortX1 && r.Y1 > SP.FortY0 && r.Y0 < SP.FortY1; }

        void RetargetClimber(Zomb z)
        {
            if (_menTick != Night.Tick)
            {
                _menTick = Night.Tick; _men.Clear(); _vehT.Clear();
                foreach (var p in Units.Platoons) { if (p.Aboard != 0) continue; foreach (var m in p.Men) if (m.Alive) _men.Add(new Target { Id = m.Id, X = m.X, Y = m.Y }); }
                for (int v = 0; v < Vehicles.Count; v++) if (!Vehicles[v].Airborne) _vehT.Add(new Target { Id = v, X = Vehicles[v].X, Y = Vehicles[v].Y });
            }
            int tx, ty; z.Goal = ClimberTargets.Pick(z.X, z.Y, _men, _vehT, _workers, ClimberSeekMm, Night.P.TrainRect, out tx, out ty); z.TX = tx; z.TY = ty;
            if (z.Goal == 4 && !TrainReachableFromInside()) { var s = Night.P.StationRect; z.Goal = 5; z.TX = Math.Max(s.X0, Math.Min(s.X1, z.X)); z.TY = Math.Max(s.Y0, Math.Min(s.Y1, z.Y)); }
            if (RodeoOn) ClimberTargets.Rodeo(z.X, z.Y, Night.P.StationRect, Night.P.AttackReachMm + RodeoExtraMm, ref z.TX, ref z.TY);   // rodeo del edificio (Marcos y Nacho, 9-nov), igual que la zona
        }

        /// <summary>D-N175 (Lucía, 8-nov, para medir; rescate de Nules D-N172): un zombi de FUERA con infantería de fuera a OutsideAggroMm o menos deja la puerta y va a por ella.
        /// Como mucho OutsideAggroCapPm ‰ de los vivos a la vez (el muro sigue siendo el frente). 0 = apagado (como siempre).</summary>
        public int OutsideAggroMm, OutsideAggroCapPm = 300; int _aggroN, _aggroTick = -1;
        void AggroOutside(Zomb z)
        {
            if (_aggroTick != Night.Tick) { _aggroTick = Night.Tick; _aggroN = 0; foreach (var q in _z) if (q.Alive && q.Aggro) _aggroN++; RetargetMenCache(); }
            long best = (long)OutsideAggroMm * OutsideAggroMm; int bx = 0, by = 0; bool found = false;
            foreach (var m in _men) { if (InFort(m.X, m.Y)) continue; long dx = m.X - z.X, dy = m.Y - z.Y, d2 = dx * dx + dy * dy; if (d2 <= best) { best = d2; bx = m.X; by = m.Y; found = true; } }
            foreach (var m in _vehT) { if (InFort(m.X, m.Y)) continue; long dx = m.X - z.X, dy = m.Y - z.Y, d2 = dx * dx + dy * dy; if (d2 <= best) { best = d2; bx = m.X; by = m.Y; found = true; } }     // D-N175: los vehículos cuentan como tropa
            if (!found) { if (z.Aggro) { z.Aggro = false; _aggroN--; } return; }
            if (!z.Aggro) { if ((long)_aggroN * 1000 >= (long)AliveZombies() * OutsideAggroCapPm) return; z.Aggro = true; _aggroN++; }
            z.Goal = 1; z.TX = bx; z.TY = by;
        }
        void RetargetMenCache()
        {
            if (_menTick == Night.Tick) return; _menTick = Night.Tick; _men.Clear(); _vehT.Clear();
            foreach (var p in Units.Platoons) { if (p.Aboard != 0) continue; foreach (var m in p.Men) if (m.Alive) _men.Add(new Target { Id = m.Id, X = m.X, Y = m.Y }); }
            for (int v = 0; v < Vehicles.Count; v++) if (!Vehicles[v].Airborne) _vehT.Add(new Target { Id = v, X = Vehicles[v].X, Y = Vehicles[v].Y });
        }

        /// <summary>Tope de trepadores por segundo (0 = sin tope). Va fuera del hash si es 0.</summary>
        public int ClimbMaxPerS = 55; long _capAcc;     // calibrado con la Noche58a (8-nov): 240 ‰/s sobre la pila − 150, como mucho 55 por segundo
        int ClimbStep(int excess)
        {
            long acc = _climbRem + (long)excess * SP.ClimbPermilS; int n = (int)(acc / (1000L * IMath.TickRate)); _climbRem = acc % (1000L * IMath.TickRate);
            if (ClimbMaxPerS > 0)      // tope de paso (calibración con la Noche58a, Hugo): como mucho ClimbMaxPerS por segundo, sin acumular más de 1 s
            {
                _capAcc = Math.Min(_capAcc + ClimbMaxPerS, (long)ClimbMaxPerS * IMath.TickRate); int capN = (int)(_capAcc / IMath.TickRate);
                if (n > capN) n = capN; _capAcc -= (long)n * IMath.TickRate;
            }
            int moved = 0;
            for (int i = _z.Count - 1; i >= 0 && moved < n; i--)
            {
                var z = _z[i]; if (!z.Alive || InFort(z.X, z.Y) || !NearWall(z.X, z.Y) || !SegIntact(z.X, z.Y)) continue;
                if (_segPile != null) { int sg = SegmentAt(z.X, z.Y); if (sg >= 0 && _segPile[sg] > 0) _segPile[sg]--; }
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

        public int AliveZombies() { int n = 0; for (int i = 0; i < _z.Count; i++) if (_z[i].Alive) n++; return n; }
        int PlatoonAmmo() { int n = 0; foreach (var p in Units.Platoons) n += p.AmmoShots; return n; }
        int TotalAmmo() { return PlatoonAmmo() + Units.StockShots; }

        public NightSnapshot Snapshot()
        {
            int alive = 0; foreach (var p in Units.Platoons) alive += p.AliveCount();
            return new NightSnapshot { Tick = Night.Tick, Hour = Night.Hour, Minute = Night.Minute, State = Night.State, ZombiesSpawned = Spawned, ZombiesAlive = AliveZombies(), Kills = Kills,
                PlatoonAmmoShots = PlatoonAmmo(), StockShots = Units.StockShots, PileAtWall = LastPile, PeakPile = PeakPile, SoldierDeaths = SoldierDeaths, SoldiersAlive = alive,
                Resupplies = Resupplies, WallHp = WallHp, StationHp = StationHp, VehicleKills = VehicleKills };
        }

        /// <summary>Munición restante en milésimas de u: almacén + dotación de los pelotones que siguen vivos (D-N77).</summary>
        public int AmmoMilliU() { long shots = Units.StockShots; foreach (var p in Units.Platoons) if (p.AliveCount() > 0) shots += p.AmmoShots; return (int)(shots * 1000 / WeaponCatalog.ShotsPerU); }
        public int VehiclesLost;

        // ================================================================ D-N163 (Lucía, construccion-reparar.md §8): el muro por tramos, Repair y Rebuild
        /// <summary>Un tramo del muro: un trozo de un lado del recinto (Side 0 oeste, 1 este, 2 sur, 3 norte; A0-A1 a lo largo, en mm). Hp 0 = brecha: ya no detiene.</summary>
        public sealed class WallSegment { public int Side, A0, A1, Hp, HpMax, Rem, PriceU, ObraMh, HeightMm; public int LenMm { get { return A1 - A0; } } }
        public enum WallJobKind : byte { Reparar = 0, Reconstruir = 1 }
        public sealed class WallJob { public WallJobKind Kind; public int Seg, Platoon, WorkTicks, TotalTicks; public bool Ordered, Working, PrevHoldFire; }   // TotalTicks: reconstruir, ticks totales con los zapadores de este tick (para la barra de avance: WorkTicks / TotalTicks); solo lectura, fuera del hash
        /// <summary>Tramos (null = el muro de un solo bloque de siempre, sin cambio de hash). Se activa con EnableSegments.</summary>
        public List<WallSegment> Segments;
        public readonly List<WallJob> WallJobs = new List<WallJob>();
        public int Repaired, Rebuilt, RepairHp, RepairMaterialsU;   // contadores para las medidas de Sara
        /// <summary>NightBot repara (AutoRepair): el tramo más bajo por debajo de este ‰ y reconstruye las brechas sin zombis cerca. Solo con Bot.</summary>
        public bool AutoRepair; public int AutoRepairBelowPm = 900;
        // cifras de Hugo (§13, sin medir): 1 u = 5.000 de vida, 1 u por zapador cada 20 s; sacos a 2 u cada 10 m; la brecha vuelve al 30 %
        public int RepairHpPerU = 5000, RepairSecPerU = 20, RebuildUPer10m = 2, RebuildPermil = 300, RebuildTicks = 0;
        public int RepairClearMm = 3000, RebuildClearMm = 15000, WorkReachMm = 10000;
        long _matAcc, _hpAcc;

        /// <summary>Parte el muro en tramos de «segLenMm» por lado (la vida total SP.WallHp se reparte en proporción a la longitud; el resto, a los primeros).</summary>
        public void EnableSegments(int segLenMm = 10000)
        {
            Segments = new List<WallSegment>();
            int[] len = { SP.FortY1 - SP.FortY0, SP.FortY1 - SP.FortY0, SP.FortX1 - SP.FortX0, SP.FortX1 - SP.FortX0 };
            int[] a0 = { SP.FortY0, SP.FortY0, SP.FortX0, SP.FortX0 };
            long per = 2L * (len[0] + len[2]);
            for (int s = 0; s < 4; s++)
            {
                int n = Math.Max(1, (len[s] + segLenMm - 1) / segLenMm);
                for (int k = 0; k < n; k++) Segments.Add(new WallSegment { Side = s, A0 = a0[s] + (int)((long)len[s] * k / n), A1 = a0[s] + (int)((long)len[s] * (k + 1) / n) });
            }
            long given = 0;
            foreach (var g in Segments) { g.HpMax = (int)((long)SP.WallHp * g.LenMm / per); given += g.HpMax; }
            for (int i = 0; given < SP.WallHp; i = (i + 1) % Segments.Count) { Segments[i].HpMax++; given++; }
            long cur = WallHp; foreach (var g in Segments) { g.Hp = (int)((long)g.HpMax * cur / Math.Max(1, SP.WallHp)); }
            SumSegments();
        }

        /// <summary>Tramos dados por la zona, una por pieza de Obras (Hugo, reparar-costes.md §1: vida_max = precio × TramoVidaPorU). Deben cubrir cada lado sin huecos,
        /// ordenados por lado y por A0. La vida que traen (Hp) se respeta; si Hp &lt; 0, empiezan al máximo.</summary>
        public void EnableSegments(IList<WallSegment> pieces)
        {
            Segments = new List<WallSegment>();
            foreach (var q in pieces) { var g = new WallSegment { Side = q.Side, A0 = q.A0, A1 = q.A1, PriceU = q.PriceU, ObraMh = q.ObraMh, HeightMm = q.HeightMm, HpMax = q.HpMax > 0 ? q.HpMax : q.PriceU * TramoVidaPorU }; g.Hp = q.Hp < 0 ? g.HpMax : Math.Min(q.Hp, g.HpMax); Segments.Add(g); }
            long mx = 0; foreach (var g in Segments) mx += g.HpMax; SP.WallHp = (int)mx;      // D-N165: el máximo del muro es la suma de sus piezas (el % del muro y el parte se miden contra esto)
            SumSegments();
        }
        public int TramoVidaPorU = 10000;
        /// <summary>D-N165 (b): ‰ del empuje de la pila que pasa a los tramos vecinos (0 = todo al tramo que toca, como en fa66281). Propuesta de Nacho: 500.</summary>
        public int PileSpreadPermil; long[] _segPush;
        void SumSegments() { long t = 0; foreach (var g in Segments) t += g.Hp; WallHp = (int)t; }
        /// <summary>Recalcula WallHp tras tocar a mano la vida de un tramo (pruebas y la zona al sincronizar).</summary>
        public void SumForTests() { if (Segments != null) SumSegments(); }

        /// <summary>El tramo que toca el punto (x, y) de fuera: el lado del que más se ha salido y la coordenada a lo largo. −1 si está dentro.</summary>
        public int SegmentAt(int x, int y)
        {
            if (Segments == null) return -1;
            int ex0 = SP.FortX0 - x, ex1 = x - SP.FortX1, ey0 = SP.FortY0 - y, ey1 = y - SP.FortY1;
            int best = Math.Max(Math.Max(ex0, ex1), Math.Max(ey0, ey1));
            int side = best == ex0 ? 0 : best == ex1 ? 1 : best == ey0 ? 2 : 3, along = side <= 1 ? y : x, last = -1;
            for (int i = 0; i < Segments.Count; i++) { var g = Segments[i]; if (g.Side != side) continue; last = i; if (along < g.A1) return i; }
            return last;
        }
        bool SegIntact(int x, int y) { if (Segments == null) return WallHp > 0; int s = SegmentAt(x, y); return s < 0 || Segments[s].Hp > 0; }

        void SegLine(WallSegment g, out int x0, out int y0, out int x1, out int y1)
        {
            if (g.Side <= 1) { x0 = x1 = g.Side == 0 ? SP.FortX0 : SP.FortX1; y0 = g.A0; y1 = g.A1; } else { y0 = y1 = g.Side == 2 ? SP.FortY0 : SP.FortY1; x0 = g.A0; x1 = g.A1; }
        }
        /// <summary>¿Hay algún zombi vivo a «r» mm o menos del tramo (de su línea)?</summary>
        public bool ZombiesNear(int seg, int r)
        {
            int x0, y0, x1, y1; SegLine(Segments[seg], out x0, out y0, out x1, out y1);
            if (Shadow)
            {
                if (_zoneZ == null) return false;
                for (int i = 0; i < _zoneZ.Count; i++)
                {
                    int zx = _zoneZ[i].X, zy = _zoneZ[i].Y, ddx = zx < x0 ? x0 - zx : zx > x1 ? zx - x1 : 0, ddy = zy < y0 ? y0 - zy : zy > y1 ? zy - y1 : 0;
                    if (ddx <= r && ddy <= r && (long)ddx * ddx + (long)ddy * ddy <= (long)r * r) return true;
                }
                return false;
            }
            for (int i = 0; i < _z.Count; i++)
            {
                if (!_z[i].Alive) continue;
                int dx = _z[i].X < x0 ? x0 - _z[i].X : _z[i].X > x1 ? _z[i].X - x1 : 0, dy = _z[i].Y < y0 ? y0 - _z[i].Y : _z[i].Y > y1 ? _z[i].Y - y1 : 0;
                if (dx <= r && dy <= r && (long)dx * dx + (long)dy * dy <= (long)r * r) return true;
            }
            return false;
        }
        Platoon Sappers() { foreach (var p in Units.Platoons) if (p.Kind == PlatoonKind.Zapadores && p.Aboard == 0 && p.AliveCount() > 0) return p; return null; }
        /// <summary>Tiempo de reconstruir (Hugo, reparar-costes.md §2): obra_h × 3600 / reloj_x / (2 × zapadores vivos) segundos reales. Sin dato de obra, sacos: 2 obrero·h cada 10 m.
        /// Con RebuildTicks &gt; 0 se usa ese tiempo fijo.</summary>
        public int RebuildTicksFor(int seg, int sappers)
        {
            if (RebuildTicks > 0) return RebuildTicks;
            var g = Segments[seg]; long mh = g.ObraMh > 0 ? g.ObraMh : 2000L * g.LenMm / 10000;
            long t = Night.P.NetClock ? Night.TicksForGameSeconds(mh * 3600L / 1000) / (2L * Math.Max(1, sappers)) : mh * 3600L * IMath.TickRate / 1000 / Math.Max(1, Night.P.ClockX) / (2L * Math.Max(1, sappers));
            return (int)Math.Max(1, t);
        }
        /// <summary>Reconstruir cuesta el precio de la pieza (Hugo, §1); sin precio, sacos a 2 u cada 10 m.</summary>
        int RebuildCostU(int seg) { var g = Segments[seg]; return g.PriceU > 0 ? g.PriceU : (int)(((long)RebuildUPer10m * g.LenMm + 9999) / 10000); }

        /// <summary>Por qué no se puede reparar el tramo (los textos del §4), o null si se puede.</summary>
        public string RepairBlockReason(int seg)
        {
            if (Segments == null || seg < 0 || seg >= Segments.Count) return "Sin tramo";
            if (Segments[seg].Hp <= 0) return "Tramo caído: hay que reconstruirlo";
            if (ZombiesNear(seg, RepairClearMm)) return "Hay zombis pegados al tramo";
            if (MaterialsU <= 0) return "Sin materiales";
            if (!Shadow && Sappers() == null) return "Sin zapadores";      // en la sombra los zapadores los cuenta la zona (TickWallJobs)
            return null;
        }
        /// <summary>Por qué no se puede reconstruir (§5), o null si se puede.</summary>
        public string RebuildBlockReason(int seg)
        {
            if (Segments == null || seg < 0 || seg >= Segments.Count) return "Sin tramo";
            if (Segments[seg].Hp > 0) return "El tramo no está caído";
            if (ZombiesNear(seg, RebuildClearMm)) return "Hay zombis a menos de 15 m";
            if (MaterialsU < RebuildCostU(seg)) return "Sin materiales";
            if (!Shadow && Sappers() == null) return "Sin zapadores";      // en la sombra los zapadores los cuenta la zona (TickWallJobs)
            return null;
        }
        /// <summary>Orden del jugador: reparar el tramo (a la cola). false si no se puede (motivo en RepairBlockReason) o ya está en la cola.</summary>
        public bool Repair(int seg) { if (RepairBlockReason(seg) != null || Queued(seg)) return false; WallJobs.Add(new WallJob { Kind = WallJobKind.Reparar, Seg = seg }); return true; }
        /// <summary>Orden del jugador: reconstruir la brecha (a la cola). false si no se puede.</summary>
        public bool Rebuild(int seg) { if (RebuildBlockReason(seg) != null || Queued(seg)) return false; WallJobs.Add(new WallJob { Kind = WallJobKind.Reconstruir, Seg = seg }); return true; }
        bool Queued(int seg) { foreach (var j in WallJobs) if (j.Seg == seg) return true; return false; }

        void EndJob(WallJob j, Platoon p) { if (p != null && j.Working) p.HoldFire = j.PrevHoldFire; if (Bot != null) Bot.Busy.Remove(j.Platoon); WallJobs.RemoveAt(0); }

        /// <summary>Daño de la pila a los tramos (D-N163, ×1; D-N165 (b) con PileSpreadPermil). «pile» = zombis pegados a cada tramo. Apunta las brechas en WallEvents.</summary>
        void DamageSegments(int[] pile)
        {
            // D-N163: el daño de la pila va al tramo que toca cada zombi (los que trepan ya no cuentan)
            if (PileSpreadPermil <= 0)
            for (int i = 0; i < Segments.Count; i++)
            {
                var g = Segments[i]; if (g.Hp <= 0 || pile[i] == 0) continue;
                g.Rem += pile[i] * SP.WallDmgPerZombieS; int dmg = g.Rem / IMath.TickRate; g.Rem %= IMath.TickRate; g.Hp = Math.Max(0, g.Hp - dmg); if (g.Hp == 0) { g.Rem = 0; Ev(WallEventKind.Brecha, i); }
            }
            else
            {
                // D-N165 (b): la pila empuja a los lados. PileSpreadPermil ‰ del empuje de cada tramo pasa a sus vecinos vivos del mismo lado (a medias si hay dos);
                // sin vecino vivo se queda en el tramo. Cuentas en milésimas de zombi; Rem en 1/(TickRate·1000) de vida, exacto.
                if (_segPush == null || _segPush.Length != Segments.Count) _segPush = new long[Segments.Count];
                Array.Clear(_segPush, 0, _segPush.Length);
                for (int i = 0; i < Segments.Count; i++)
                {
                    if (pile[i] == 0) continue; long all = pile[i] * 1000L, out_ = all * PileSpreadPermil / 1000;
                    bool l = i > 0 && Segments[i - 1].Side == Segments[i].Side && Segments[i - 1].Hp > 0, r = i + 1 < Segments.Count && Segments[i + 1].Side == Segments[i].Side && Segments[i + 1].Hp > 0;
                    if (!l && !r) out_ = 0;
                    _segPush[i] += all - out_;
                    if (l && r) { _segPush[i - 1] += out_ / 2; _segPush[i + 1] += out_ - out_ / 2; } else if (l) _segPush[i - 1] += out_; else if (r) _segPush[i + 1] += out_;
                }
                long q = (long)IMath.TickRate * 1000;
                for (int i = 0; i < Segments.Count; i++)
                {
                    var g = Segments[i]; if (g.Hp <= 0 || _segPush[i] == 0) continue;
                    long acc = g.Rem + _segPush[i] * SP.WallDmgPerZombieS; int dmg = (int)(acc / q); g.Rem = (int)(acc % q); g.Hp = Math.Max(0, g.Hp - dmg); if (g.Hp == 0) { g.Rem = 0; Ev(WallEventKind.Brecha, i); }
                }
            }
        }

        /// <summary>Una vez por tick: la obra de cabeza de la cola. Los zapadores van al tramo (por dentro, a 3 m del muro), y al estar a menos de 10 m paran de disparar y trabajan.
        /// Si deja de cumplirse la condición (zombis encima, sin materiales, sin zapadores), la obra se cancela.</summary>
        void WallJobsStep()
        {
            if (Segments == null) return;
            if (AutoRepair && Bot != null && WallJobs.Count == 0 && Night.Tick % 60 == 0)
            {
                int best = -1; long bp = long.MaxValue;
                for (int i = 0; i < Segments.Count; i++) if (Segments[i].Hp == 0 && RebuildBlockReason(i) == null) { best = i; bp = -1; break; }
                if (best >= 0) Rebuild(best);
                else
                {
                    for (int i = 0; i < Segments.Count; i++) { var g = Segments[i]; long pm = (long)g.Hp * 1000 / Math.Max(1, g.HpMax); if (g.Hp > 0 && pm < AutoRepairBelowPm && pm < bp && RepairBlockReason(i) == null) { bp = pm; best = i; } }
                    if (best >= 0) Repair(best);
                }
            }
            if (WallJobs.Count == 0) return;
            var j = WallJobs[0]; var p = Sappers();
            if (p == null || JobBlocked(j) != null) { EndJob(j, p); return; }
            j.Platoon = p.Id; if (Bot != null) Bot.Busy.Add(p.Id);
            int sx, sy; WorkSpot(j.Seg, out sx, out sy);
            if (IMath.Dist(p.AX - sx, p.AY - sy) > WorkReachMm) { if (!j.Ordered || !p.Moving) { Units.Move(p.Id, sx, sy); j.Ordered = true; } return; }
            WorkJob(j, p.AliveCount(), p);
        }

        /// <summary>Por qué se cancela la obra de cabeza (null si sigue): caído, zombis, materiales.</summary>
        string JobBlocked(WallJob j)
        {
            var seg = Segments[j.Seg];
            return j.Kind == WallJobKind.Reparar ? (seg.Hp <= 0 ? "caído" : ZombiesNear(j.Seg, RepairClearMm) ? "zombis" : (MaterialsU <= 0 ? "materiales" : null))
                                               : (seg.Hp > 0 && !j.Working ? "no caído" : ZombiesNear(j.Seg, RebuildClearMm) ? "zombis" : null);
        }

        /// <summary>Dónde trabajan los zapadores en el tramo: por dentro, a 3 m del muro, en el centro del tramo.</summary>
        public void WorkSpot(int seg, out int x, out int y)
        {
            var g = Segments[seg]; int x0, y0, x1, y1; SegLine(g, out x0, out y0, out x1, out y1);
            x = (x0 + x1) / 2 + (g.Side == 0 ? 3000 : g.Side == 1 ? -3000 : 0); y = (y0 + y1) / 2 + (g.Side == 2 ? 3000 : g.Side == 3 ? -3000 : 0);
        }

        /// <summary>Un tick de trabajo de «n» zapadores en la obra de cabeza (ya junto al tramo). «p» null en la sombra de la zona (la zona lleva el HoldFire).</summary>
        void WorkJob(WallJob j, int n, Platoon p)
        {
            var seg = Segments[j.Seg];
            if (!j.Working)
            {
                j.Working = true; if (p != null) { j.PrevHoldFire = p.HoldFire; p.HoldFire = true; }
                if (j.Kind == WallJobKind.Reconstruir) { int c = RebuildCostU(j.Seg); MaterialsU -= c; MaterialsSpentU += c; }
                Ev(WallEventKind.ObraEmpieza, j.Seg);
            }
            if (j.Kind == WallJobKind.Reparar)
            {
                long tps = (long)RepairSecPerU * IMath.TickRate;                     // ticks por u y zapador
                _matAcc += n; while (_matAcc >= tps && MaterialsU > 0) { _matAcc -= tps; MaterialsU--; RepairMaterialsU++; MaterialsSpentU++; }
                _hpAcc += (long)n * RepairHpPerU; int hp = (int)(_hpAcc / tps); _hpAcc %= tps;
                int add = Math.Min(hp, seg.HpMax - seg.Hp); seg.Hp += add; RepairHp += add; SumSegments();
                if (seg.Hp >= seg.HpMax) { Repaired++; Ev(WallEventKind.Reparado, j.Seg); EndJob(j, p); }
            }
            else if (++j.WorkTicks >= (j.TotalTicks = RebuildTicksFor(j.Seg, n))) { seg.Hp = (int)((long)seg.HpMax * RebuildPermil / 1000); seg.Rem = 0; SumSegments(); Rebuilt++; Ev(WallEventKind.Reconstruido, j.Seg); EndJob(j, p); }
        }

        // ================================================================ D-N163: la zona usa un NightSim «sombra» como dueño de los tramos (Marcos, 86bbd5d)
        public enum WallEventKind : byte { Brecha = 0, Reconstruido = 1, Reparado = 2, ObraEmpieza = 3, ObraCancelada = 4 }
        public struct WallEvent { public WallEventKind Kind; public int Seg; }
        /// <summary>Lo que ha pasado en los tramos en el último Step, ApplySegmentPile o TickWallJobs (se vacía al empezar cada uno).</summary>
        public readonly List<WallEvent> WallEvents = new List<WallEvent>();
        void Ev(WallEventKind k, int seg) { WallEvents.Add(new WallEvent { Kind = k, Seg = seg }); }
        /// <summary>Materiales gastados en total (reparar + reconstruir): la zona manda a la red lo nuevo con NetWorld.CmdSpendMaterials. No va en el hash (sale de MaterialsU).</summary>
        public int MaterialsSpentU;
        /// <summary>true en la sombra de la zona: los zombis los pone la zona (TickWallJobs) y los zapadores también.</summary>
        public bool Shadow; IList<Target> _zoneZ;

        /// <summary>Crea la sombra de la zona: solo tramos, materiales (los de la red: NetWorld.Stock[nodo, Materiales]) y la cola de obras. A la sombra nunca se le llama a Step.</summary>
        public static NightSim ShadowFor(NightParams np, NightSimParams sp, IList<WallSegment> pieces, int materialsU)
        {
            var s = new NightSim(np, sp, 0, materialsU, 0, 1); s.Shadow = true; s.EnableSegments(pieces); return s;
        }

        /// <summary>Un tick de zona: la pila de cada tramo (zombis pegados, sin los que trepan), contada por la zona. Devuelve las brechas de este tick.</summary>
        public List<WallEvent> ApplySegmentPile(int[] pilePerSegment)
        {
            WallEvents.Clear();
            if (Segments == null || pilePerSegment == null || pilePerSegment.Length != Segments.Count) return WallEvents;
            DamageSegments(pilePerSegment); SumSegments(); LosSyncWalls(); return WallEvents;
        }

        /// <summary>Un tick de zona de la obra de cabeza. «sappers» = zapadores vivos; «atWork» = ya están a WorkReachMm o menos de WorkSpot (los mueve la zona);
        /// «zombies» = los de la zona (reglas de 3 m y 15 m). Devuelve ObraEmpieza (dejan de disparar), Reparado, Reconstruido u ObraCancelada (vuelven a disparar).</summary>
        public List<WallEvent> TickWallJobs(int sappers, bool atWork, IList<Target> zombies)
        {
            WallEvents.Clear(); _zoneZ = zombies; LosSyncWalls();       // D-N174: un tramo que la zona ha tocado deja de tapar (o vuelve a tapar)
            if (Segments == null || WallJobs.Count == 0) return WallEvents;
            var j = WallJobs[0];
            if (sappers <= 0 || JobBlocked(j) != null) { Ev(WallEventKind.ObraCancelada, j.Seg); EndJob(j, null); return WallEvents; }
            if (atWork) WorkJob(j, sappers, null);
            LosSyncWalls(); return WallEvents;
        }

        /// <summary>Tramo de la obra de cabeza (para que la zona mueva a los zapadores a WorkSpot), o −1.</summary>
        public int CurrentJobSeg { get { return WallJobs.Count > 0 ? WallJobs[0].Seg : -1; } }

        // ================================================================ D-N174 / D-N175 en la sombra de la zona (Lucía, 8-nov): mismas reglas que NightSim, con las listas de la zona
        /// <summary>D-N174: altura de la boca del arma por tipo (Álvaro, 8-nov). Infantería 1,6 m; VAMTAC 2,4 m; Dragón 2,9 m; azotea del edificio de viajeros 12,6 m (11 m + 1,6 m).</summary>
        public const int EyeVamtacMm = 2400, EyeDragonMm = 2900, EyeAzoteaMm = 12600;
        /// <summary>D-N174 (Lucía): el AG36 (lanzagranadas de 40 mm) es tiro curvo y no necesita línea de visión; el resto (C90 incluido) es fuego directo.</summary>
        public static bool IsIndirectFire(string weaponKey) { return weaponKey == "lanzagranadas"; }
        /// <summary>D-N174: azotea del edificio de viajeros (Álvaro, sagunto-nivel.md §10.15): dos pelotones de tiradores al empezar la noche, libres (D-N168).</summary>
        public static readonly int[] AzoteaSpotsMm = { 97500, 76500, 97500, 82300 };

        /// <summary>D-N174: los sólidos de la zona a partir del inventario de Álvaro (docs/niveles/sagunto_obstaculos_v1.csv, «;», mm en coordenadas de la zona).
        /// Solo las filas con tapa_vision = si y altura &gt; 0, sin muros ni puertas (esos van por tramos, con sus brechas). Los nidos se corren «nestShiftMm» (−1,5 m: a X 72,0)
        /// y cuentan como muro propio (no tapan a quien está a 2 m). «seg» = segmento con medio grosor; «caja» = rectángulo.</summary>
        public static List<UnitsWorld.LosBlocker> LosSceneFromCsv(string csv, int nestShiftMm = -1500)
        {
            var l = new List<UnitsWorld.LosBlocker>(); if (csv == null) return l; bool head = true;
            foreach (var raw in csv.Split('\n'))
            {
                var line = raw.TrimEnd('\r'); if (line.Length == 0) continue;
                if (head) { head = false; continue; }
                var c = line.Split(';'); if (c.Length < 10) continue;
                string tipo = c[1]; if (tipo.StartsWith("muro") || tipo == "puerta" || c[9] != "si") continue;
                int x0, y0, x1, y1, hg, h;
                if (!int.TryParse(c[3], out x0) || !int.TryParse(c[4], out y0) || !int.TryParse(c[5], out x1) || !int.TryParse(c[6], out y1) || !int.TryParse(c[7], out hg) || !int.TryParse(c[8], out h) || h <= 0) continue;
                if (tipo == "nido") { x0 += nestShiftMm; x1 += nestShiftMm; }
                var r = c[2] == "seg" ? new RectMm(Math.Min(x0, x1) - hg, Math.Min(y0, y1) - hg, Math.Max(x0, x1) + hg, Math.Max(y0, y1) + hg) : new RectMm(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
                l.Add(new UnitsWorld.LosBlocker { R = r, HeightMm = h, OwnWall = tipo == "nido" });
            }
            return l;
        }

        /// <summary>D-N174 para la zona: ¿ve el tirador en (sx, sy), con la boca a eyeMm, al zombi en (tx, ty)? Mismas cuentas que NightSim (2,5D, blanco a 1,0 m).
        /// Antes, en la sombra: EnableLineOfSight(true, LosSceneFromCsv(...)) con los tramos ya puestos; las brechas dejan de tapar solas (ApplySegmentPile y TickWallJobs).</summary>
        public bool LineOfSight(int sx, int sy, int tx, int ty, int eyeMm = UnitsWorld.EyeInfantryMm)
        {
            if (Units.LosBlockers.Count == 0) return true;
            return Units.LineOfSight(sx, sy, tx, ty, eyeMm);
        }

        /// <summary>D-N175 para la zona: el zombi de fuera «ZombieId» deja el campo de flujo a la puerta y va a (TX, TY).</summary>
        public struct AggroOrder { public int ZombieId, TX, TY; }
        readonly SortedSet<int> _zoneAggro = new SortedSet<int>(); readonly List<AggroOrder> _aggroOut = new List<AggroOrder>();
        /// <summary>Zombis de la zona que persiguen a la tropa ahora mismo (D-N175). Va en el hash de la sombra.</summary>
        public int ZoneAggroCount { get { return _zoneAggro.Count; } }

        /// <summary>D-N175 (Lucía, 8-nov; 25 m y 30 %): un tick de decisión de la zona (recomendado: cada ClimberRetargetTicks). «outside» = los zombis VIVOS de fuera del recinto
        /// (los de la pila también: aquí se apartan los que están a PileRadiusMm del muro y no persiguen ya); «troops» = soldados y vehículos (no en vuelo) de la zona, se quitan los de
        /// dentro del recinto; «aliveTotal» = zombis vivos en toda la zona (para el tope). Devuelve TODOS los que persiguen tras este tick con su punto; los demás van a la puerta.
        /// Determinista: se recorren por Id; con el tope lleno, solo siguen los que ya perseguían y entran los de Id más bajo.</summary>
        public List<AggroOrder> TickOutsideAggro(IList<Target> outside, IList<Target> troops, int aliveTotal)
        {
            _aggroOut.Clear();
            if (OutsideAggroMm <= 0 || outside == null) { _zoneAggro.Clear(); return _aggroOut; }
            var tr = new List<Target>(); if (troops != null) foreach (var t in troops) if (!InFort(t.X, t.Y)) tr.Add(t);
            var zs = new List<Target>(outside); zs.Sort((a, b) => a.Id.CompareTo(b.Id));
            var alive = new HashSet<int>(); foreach (var z in zs) alive.Add(z.Id);
            _zoneAggro.RemoveWhere(id => !alive.Contains(id));                                   // los muertos o los que han entrado dejan de contar
            long r2 = (long)OutsideAggroMm * OutsideAggroMm; long cap = (long)Math.Max(0, aliveTotal) * OutsideAggroCapPm;
            // 1) los que ya perseguían: siguen si aún tienen tropa a 25 m; si no, vuelven a la puerta
            // 2) los nuevos (no pegados al muro), por Id, mientras quepan en el tope
            for (int pass = 0; pass < 2; pass++)
                foreach (var z in zs)
                {
                    bool was = _zoneAggro.Contains(z.Id); if ((pass == 0) != was) continue;
                    if (!was && NearWall(z.X, z.Y)) continue;                                          // la pila y los que ya están en el muro no cambian
                    long best = r2; int bx = 0, by = 0; bool found = false;
                    foreach (var m in tr) { long dx = m.X - z.X, dy = m.Y - z.Y, d2 = dx * dx + dy * dy; if (d2 <= best) { best = d2; bx = m.X; by = m.Y; found = true; } }
                    if (!found) { if (was) _zoneAggro.Remove(z.Id); continue; }
                    if (!was) { if ((long)_zoneAggro.Count * 1000 >= cap) continue; _zoneAggro.Add(z.Id); }
                    _aggroOut.Add(new AggroOrder { ZombieId = z.Id, TX = bx, TY = by });
                }
            _aggroOut.Sort((a, b) => a.ZombieId.CompareTo(b.ZombieId));
            return _aggroOut;
        }

        /// <summary>Tramos a partir de las piezas de Obras de una estación (Works.Snapshot): muros y puertas, en coordenadas de la zona (origin = centro del recinto).
        /// vida_max = CostE4 × vidaPorU / 10.000 (Hugo: 11.924 por u). Ordenados por lado (0 O, 1 E, 2 S, 3 N) y por A0, como pide EnableSegments.</summary>
        public static List<WallSegment> PiecesFromWorks(IEnumerable<WorkPiece> snapshot, int originX, int originY, int vidaPorU)
        {
            var list = new List<WallSegment>();
            foreach (var p in snapshot)
            {
                if (!(p.IsWall || p.IsGate)) continue;
                int x0 = p.X0 + originX, x1 = p.X1 + originX, y0 = p.Y0 + originY, y1 = p.Y1 + originY; int side, a0, a1;
                if (x0 == x1) { side = x0 < originX ? 0 : 1; a0 = Math.Min(y0, y1); a1 = Math.Max(y0, y1); } else { side = y0 < originY ? 2 : 3; a0 = Math.Min(x0, x1); a1 = Math.Max(x0, x1); }
                list.Add(new WallSegment { Side = side, A0 = a0, A1 = a1, HpMax = (int)((long)p.CostE4 * vidaPorU / 10000), Hp = -1, PriceU = (p.CostE4 + 9999) / 10000, HeightMm = TBD.Net.Obras.HeightMm(p.Kind) });   // D-N174: altura de la pieza para la línea de visión
            }
            list.Sort((a, b) => a.Side != b.Side ? a.Side.CompareTo(b.Side) : a.A0.CompareTo(b.A0));
            return list;
        }

        public ulong Hash()
        {
            ulong h = Units.Hash() ^ Night.Hash();
            h += IMath.Mix((ulong)(uint)WallHp + ((ulong)(uint)Night.StationHp << 32) + 3) + IMath.Mix((ulong)(uint)Kills + ((ulong)(uint)SoldierDeaths << 32) + 5) + IMath.Mix(_rng.S ^ 0x77);
            for (int i = 0; i < _z.Count; i++) if (_z[i].Alive) h += IMath.Mix((ulong)(uint)_z[i].X + ((ulong)(uint)_z[i].Y << 32) + (ulong)i);
            for (int i = 0; i < Vehicles.Count; i++) h += Vehicles[i].Hash() * (ulong)(2 * i + 3);
            foreach (var id in _zoneAggro) h += IMath.Mix((ulong)(uint)id + 0x175UL);                    // D-N175 en la sombra (vacío fuera de ella)     // sin vehículos, el hash es el de siempre
            if (Segments != null)                                                                       // sin tramos, el hash es el de siempre
            {
                for (int i = 0; i < Segments.Count; i++) h += IMath.Mix((ulong)(uint)Segments[i].Hp + ((ulong)(uint)Segments[i].Rem << 32) + (ulong)(i + 0xD163));
                for (int i = 0; i < WallJobs.Count; i++) h += IMath.Mix((ulong)WallJobs[i].Kind + ((ulong)(uint)WallJobs[i].Seg << 8) + ((ulong)(uint)WallJobs[i].WorkTicks << 24) + (WallJobs[i].Working ? 1UL << 60 : 0) + (ulong)(i + 0x30B));
                h += IMath.Mix((ulong)_matAcc + ((ulong)_hpAcc << 32) + ((ulong)(uint)MaterialsU << 20) + 0x2E9);
            }
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
