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

        public static NightSimParams Default()
        {
            return new NightSimParams
            {
                FortX0 = 70000, FortX1 = 156000, FortY0 = 44000, FortY1 = 85000, StationX = 113000, StationY = 64500,
                StationHp = 6000, WallHp = 120240, ZombieSpeedMmS = 1500, WallDmgPerZombieS = 6, StationDmgPerZombieS = 20,
                ContactRadiusMm = 1200, ContactCasualtyPmPerS = 14, PileRadiusMm = 1500,
            };
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

        public readonly NightSimParams SP;
        public readonly UnitsWorld Units;
        public readonly NightGame Night;
        public readonly List<WaveStats> Waves = new List<WaveStats>();
        public int WallHp, StationHp, Kills, SoldierDeaths, Resupplies, PeakPile, Spawned;
        public int MaterialsU, FuelU;
        readonly List<Zomb> _z = new List<Zomb>();
        readonly List<Target> _targets = new List<Target>();
        readonly List<int> _ids = new List<int>();
        readonly List<SpawnPoint> _spawns = new List<SpawnPoint>();
        readonly StepResult _res = new StepResult();
        int _wallRem, _stationRem, _contactSeed;
        IRng _rng;

        public NightSim(NightParams np, NightSimParams sp, int ammoU, int materialsU, int fuelU, uint seed)
        {
            SP = sp; Night = new NightGame(np, seed ^ 0x5A5A); Units = new UnitsWorld(seed); _rng = new IRng(seed ^ 0xC0FFEE);
            Units.StockShots = ammoU * WeaponCatalog.ShotsPerU; MaterialsU = materialsU; FuelU = fuelU;
            WallHp = sp.WallHp; StationHp = sp.StationHp;
            for (int w = 0; w < np.Waves.Length; w++) Waves.Add(new WaveStats { Wave = w + 1, StartTick = Night.WaveStartTick(w), HourAtStart = (np.StartHour + np.Waves[w].StartMinute / 60) % 24, MinuteAtStart = np.Waves[w].StartMinute % 60 });
        }

        /// <summary>Crea la noche a partir del texto de noche_sagunto.json de Hugo (D-N74). «difficulty»: facil, normal o dificil (null = la del JSON).</summary>
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
            var rec = (Dictionary<string, object>)root["recursos_iniciales"];
            return new NightSim(np, NightSimParams.Default(), (int)(long)rec["municion_u"], (int)(long)rec["materiales_u"], (int)(long)rec["combustible_u"], seed);
        }

        /// <summary>Los cinco pelotones de la noche, dentro del recinto junto al muro oeste.</summary>
        public void AddDefaultForce()
        {
            int x = SP.FortX0 + 9000, y0 = SP.FortY0 + 6000;
            Units.AddPlatoon(PlatoonKind.Fusileros, x, y0 + 0, 0);
            Units.AddPlatoon(PlatoonKind.Ametralladores, x, y0 + 10000, 0);
            Units.AddPlatoon(PlatoonKind.Granaderos, x + 6000, y0 + 20000, 0);
            Units.AddPlatoon(PlatoonKind.Tiradores, x + 12000, y0 + 5000, 0);
            Units.AddPlatoon(PlatoonKind.Zapadores, x + 12000, y0 + 25000, 0);
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
            Night.Step(StationHp > 0, true, _spawns);
            var cur = Current();
            foreach (var s in _spawns) { _z.Add(new Zomb { X = s.X, Y = s.Y }); Spawned++; if (cur != null) cur.Spawned++; }
            // 2) los zombis: caminan hacia el edificio; el muro los detiene; se cuenta la pila
            int pile = 0, speed = SP.ZombieSpeedMmS / IMath.TickRate, atStation = 0;
            for (int i = 0; i < _z.Count; i++)
            {
                var z = _z[i]; if (!z.Alive) continue;
                int dx = SP.StationX - z.X, dy = SP.StationY - z.Y, d = IMath.Dist(dx, dy);
                if (d < 3500) { atStation++; continue; }
                int nx = z.X + IMath.DivR((long)dx * speed, d), ny = z.Y + IMath.DivR((long)dy * speed, d);
                if (WallHp > 0 && !InFort(z.X, z.Y) && InFort(nx, ny))
                {
                    // choca con el muro: se queda pegado (si entra por X se corrige X; por Y, Y)
                    if (!InFort(nx, z.Y)) ny = z.Y; else if (!InFort(z.X, ny)) nx = z.X; else { nx = z.X; ny = z.Y; }
                    if (InFort(nx, ny)) { nx = z.X; ny = z.Y; }
                }
                z.X = nx; z.Y = ny;
                if (WallHp > 0 && !InFort(z.X, z.Y) && NearWall(z.X, z.Y)) pile++;
            }
            if (pile > PeakPile) PeakPile = pile;
            if (cur != null && pile > cur.PeakPile) cur.PeakPile = pile;
            LastPile = pile;
            // 3) desgaste del muro por la pila y del edificio por los que llegan a él
            if (WallHp > 0 && pile > 0) { _wallRem += pile * SP.WallDmgPerZombieS; int dmg = _wallRem / IMath.TickRate; _wallRem %= IMath.TickRate; WallHp = Math.Max(0, WallHp - dmg); }
            if (atStation > 0) { _stationRem += atStation * SP.StationDmgPerZombieS; int dmg = _stationRem / IMath.TickRate; _stationRem %= IMath.TickRate; StationHp = Math.Max(0, StationHp - dmg); }
            // 4) los pelotones disparan; los impactos abaten
            _targets.Clear(); _ids.Clear();
            for (int i = 0; i < _z.Count; i++) if (_z[i].Alive) { _targets.Add(new Target { Id = i, X = _z[i].X, Y = _z[i].Y }); }
            int before = TotalAmmo();
            Units.Step(_targets, _res);
            foreach (var h in _res.Hits)
                if (h.TargetId >= 0 && h.TargetId < _z.Count && _z[h.TargetId].Alive) { _z[h.TargetId].Alive = false; Kills++; Units.NoteKill(h.Platoon); if (cur != null) cur.Kills++; }
            if (cur != null) cur.AmmoSpentShots += _res.AmmoSpent;
            // 5) contacto con los soldados
            ContactCasualties(cur);
            // 6) limpieza cada 64 ticks para que la lista no crezca sin fin
            if ((Night.Tick & 63) == 0) _z.RemoveAll(z => !z.Alive);
            // 7) cierre de oleada: cuando empieza la siguiente (o acaba la noche) se congelan sus estadísticas
            for (int i = 0; i < Waves.Count; i++)
            {
                bool over = (i + 1 < Waves.Count && Night.Tick >= Waves[i + 1].StartTick) || Night.State != NightState.Jugando;
                if (over && Waves[i].EndTick < 0 && Night.Tick >= Waves[i].StartTick) { Waves[i].EndTick = Night.Tick; Waves[i].AliveAtEnd = AliveZombies(); Waves[i].AmmoLeftAtEnd = PlatoonAmmo(); Waves[i].StockLeftAtEnd = Units.StockShots; }
            }
        }

        public int LastPile;

        bool NearWall(int x, int y)
        {
            int r = SP.PileRadiusMm;
            bool inX = x > SP.FortX0 - r && x < SP.FortX1 + r, inY = y > SP.FortY0 - r && y < SP.FortY1 + r;
            return inX && inY;     // fuera del recinto y a menos de r de él
        }

        void ContactCasualties(WaveStats cur)
        {
            long r2 = (long)SP.ContactRadiusMm * SP.ContactRadiusMm;
            foreach (var p in Units.Platoons)
                foreach (var m in p.Men)
                {
                    if (!m.Alive) continue;
                    int contacts = 0;
                    for (int i = 0; i < _z.Count; i++) { var z = _z[i]; if (!z.Alive) continue; long ex = z.X - m.X, ey = z.Y - m.Y; if (ex * ex + ey * ey <= r2) contacts++; }
                    if (contacts == 0) continue;
                    // ‰ por zombi y segundo → por tick: p = contactos · pm / (30 · 1000)
                    if (_rng.Below(30000) < contacts * SP.ContactCasualtyPmPerS)
                        if (Units.DamageMan(p.Id, m.Id, 1000)) { SoldierDeaths++; if (cur != null) cur.SoldierDeaths++; }
                }
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
        public int Stars()
        {
            if (Night.State != NightState.Victoria) return 0;
            int s = 1; if (WallHp > 0) s++; if (SoldierDeaths == 0) s++; return s;
        }

        public ulong Hash()
        {
            ulong h = Units.Hash() ^ Night.Hash();
            h += IMath.Mix((ulong)(uint)WallHp + ((ulong)(uint)StationHp << 32) + 3) + IMath.Mix((ulong)(uint)Kills + ((ulong)(uint)SoldierDeaths << 32) + 5) + IMath.Mix(_rng.S ^ 0x77);
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
