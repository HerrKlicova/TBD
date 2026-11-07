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
        public int GateGapMm;                           // hueco de la vía en el muro oeste (en la zona de Unity la puerta de vía no tiene sólido: 3.200). 0 = recinto cerrado (lo medido hasta ahora)

        public static NightSimParams Default()
        {
            return new NightSimParams
            {
                FortX0 = 70000, FortX1 = 156000, FortY0 = 44000, FortY1 = 85000, StationX = 113000, StationY = 64500,
                StationHp = 60000, WallHp = 1200000, ZombieSpeedMmS = 1500, WallDmgPerZombieS = 1, StationDmgPerZombieS = 20,
                ContactRadiusMm = 1200, ContactCasualtyPmPerS = 14, PileRadiusMm = 1500,
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
            p.ZombieSpeedMmS = g("ZombieSpeedMmS", p.ZombieSpeedMmS); p.ContactRadiusMm = g("ContactRadiusMm", p.ContactRadiusMm); p.ContactCasualtyPmPerS = g("ContactCasualtyPmPerS", p.ContactCasualtyPmPerS); p.PileRadiusMm = g("PileRadiusMm", p.PileRadiusMm); p.GateGapMm = g("GateGapMm", p.GateGapMm);
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
    }

    public sealed class WaveStats
    {
        public int Wave, StartTick, EndTick = -1;
        public int HourAtStart, MinuteAtStart;
        public int Spawned, Kills, SoldierDeaths, Resupplies;
        public int PeakPile, AmmoSpentShots;
        public int AliveAtEnd, AmmoLeftAtEnd, StockLeftAtEnd;
    }

    public sealed class NightSim
    {
        sealed class Zomb { public int X, Y; public bool Alive = true; }

        public NightSimParams SP;
        public readonly UnitsWorld Units;
        public readonly NightGame Night;
        public readonly List<WaveStats> Waves = new List<WaveStats>();
        public int WallHp, Kills, SoldierDeaths, Resupplies, PeakPile, Spawned;
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
            WallHp = sp.WallHp; Night.P.StationHp = sp.StationHp; Night.StationHp = sp.StationHp; Night.P.StationDmgPerZombieS = sp.StationDmgPerZombieS;
            // NightSim abstrae el edificio como el punto al que convergen los zombis (StationX/Y): su «edificio» es una caja de ±3,5 m alrededor; el tren es el real (andén)
            Night.P.StationRect = new RectMm(sp.StationX - 3500, sp.StationY - 3500, sp.StationX + 3500, sp.StationY + 3500);
            for (int w = 0; w < np.Waves.Length; w++) Waves.Add(new WaveStats { Wave = w + 1, StartTick = Night.WaveStartTick(w), HourAtStart = (np.StartHour + np.Waves[w].StartMinute / 60) % 24, MinuteAtStart = np.Waves[w].StartMinute % 60 });
        }

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
            return new NightSim(np, NightSimParams.FromJson(spj), (int)(long)rec["municion_u"], (int)(long)rec["materiales_u"], (int)(long)rec["combustible_u"], seed);
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

        bool InFort(int x, int y) { return x > SP.FortX0 && x < SP.FortX1 && y > SP.FortY0 && y < SP.FortY1; }

        public void Run(int ticks) { for (int i = 0; i < ticks && Night.State == NightState.Jugando; i++) Step(); }

        public void Step()
        {
            if (Night.State != NightState.Jugando) return;
            // 1) la noche: reloj, oleadas y fin de partida (gana la derrota)
            Night.WallPermil = SP.WallHp > 0 ? (int)((long)WallHp * 1000 / SP.WallHp) : 0;
            Night.Step(_spawns);
            var cur = Current();
            foreach (var s in _spawns) { _z.Add(new Zomb { X = s.X, Y = s.Y }); Spawned++; if (cur != null) cur.Spawned++; }
            // 2) los zombis: caminan hacia el edificio; el muro los detiene; se cuenta la pila
            int pile = 0, speed = SP.ZombieSpeedMmS / IMath.TickRate;
            for (int i = 0; i < _z.Count; i++)
            {
                var z = _z[i]; if (!z.Alive) continue;
                int tx = SP.StationX, ty = SP.StationY;
                bool gap = SP.GateGapMm > 0 && WallHp > 0;
                if (gap && z.X < SP.FortX0 - 500 && !InFort(z.X, z.Y)) { tx = SP.FortX0 - 1000; }      // con hueco en la vía, la horda rodea el muro hasta la puerta (como el flujo de la zona)
                int dx = tx - z.X, dy = ty - z.Y, d = IMath.Dist(dx, dy);
                if (d < 3500 && tx == SP.StationX) continue;
                if (d < 1) continue;
                int nx = z.X + IMath.DivR((long)dx * speed, d), ny = z.Y + IMath.DivR((long)dy * speed, d);
                if (WallHp > 0 && !InFort(z.X, z.Y) && InFort(nx, ny) && !(gap && z.X <= SP.FortX0 && Math.Abs(ny - SP.StationY) < SP.GateGapMm / 2))
                {
                    // choca con el muro: se queda pegado (si entra por X se corrige X; por Y, Y)
                    if (!InFort(nx, z.Y)) ny = z.Y; else if (!InFort(z.X, ny)) nx = z.X; else { nx = z.X; ny = z.Y; }
                    if (InFort(nx, ny)) { nx = z.X; ny = z.Y; }
                }
                z.X = nx; z.Y = ny;
                if (WallHp > 0 && !InFort(z.X, z.Y) && NearWall(z.X, z.Y) && !(gap && Math.Abs(z.Y - SP.StationY) < SP.GateGapMm / 2 + 500)) pile++;      // los que cruzan el hueco no son pila
            }
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
            int before = TotalAmmo();
            Units.Step(_targets, _res);
            foreach (var h in _res.Hits)
                if (h.TargetId >= 0 && h.TargetId < _z.Count && _z[h.TargetId].Alive) { _z[h.TargetId].Alive = false; Kills++; Units.NoteKill(h.Platoon); if (cur != null) cur.Kills++; }
            if (cur != null) cur.AmmoSpentShots += _res.AmmoSpent;
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

        /// <summary>Bot opcional (-nightbot): si no es null, da órdenes cada tick (ataque-movimiento al muro amenazado y reabastecimiento al 30 %). Ver NocheBot.cs.</summary>
        public NightBot Bot;
        public int LastPile;

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
                Resupplies = Resupplies, WallHp = WallHp, StationHp = StationHp };
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
