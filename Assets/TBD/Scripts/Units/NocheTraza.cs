using System;
using System.Collections.Generic;
using System.Text;

namespace TBD.Units
{
    // Traza de la noche: una fila cada N ticks con lo que importa para saber si la zona de Unity cumple el contrato de docs/noche-zona-contrato.md (pila, muro, abatidos, atacantes, munición).
    // La zona real y NightSim escriben el MISMO formato; NightTraceTools.Compare los compara. Sin dependencias de Unity: sirve en dotnet y dentro de Unity (EditMode o un MonoBehaviour).
    // Todo entero. «Tick» es el de NightGame (Night.Tick): el calendario de oleadas es el mismo en los dos, así que las filas se alinean por tick.

    public struct TraceRow
    {
        public int Tick;
        public int Spawned;          // zombis que han entrado desde el principio de la noche (NightGame los da por cuotas exactas: debe coincidir con NightSim al tick)
        public int Alive;            // zombis vivos ahora
        public int Pile;             // zombis vivos, fuera del recinto y a ≤ 1,5 m del muro, con el muro en pie (la «pila»)
        public int WallPpm;          // vida del muro en millonésimas de la de partida (1.000.000 = entero)
        public int Kills;            // zombis abatidos por el fuego desde el principio de la noche
        public int AmmoShots;        // munición restante en disparos equivalentes (almacén + pelotones); −1 si la zona no la da
        public int AtStation, AtTrain;   // zombis pegados al edificio y al tren (NightGame.CountAttackers)
    }

    public sealed class NightTrace
    {
        public const string Header = "tick;spawned;alive;pile;wall_ppm;kills;ammo_shots;at_station;at_train";
        public readonly List<TraceRow> Rows = new List<TraceRow>();
        public string Source = "";          // «NightSim», «zona»…, solo informativo

        public string ToCsv()
        {
            var sb = new StringBuilder(); sb.Append("# fuente;").Append(Source).Append('\n').Append(Header).Append('\n');
            foreach (var r in Rows) sb.Append(r.Tick).Append(';').Append(r.Spawned).Append(';').Append(r.Alive).Append(';').Append(r.Pile).Append(';').Append(r.WallPpm).Append(';').Append(r.Kills).Append(';').Append(r.AmmoShots).Append(';').Append(r.AtStation).Append(';').Append(r.AtTrain).Append('\n');
            return sb.ToString();
        }

        public static NightTrace FromCsv(string csv)
        {
            var t = new NightTrace(); if (csv == null) return t;
            foreach (var raw in csv.Split('\n'))
            {
                var line = raw.Trim(); if (line.Length == 0) continue;
                if (line.StartsWith("#")) { if (line.StartsWith("# fuente;")) t.Source = line.Substring(9); continue; }
                if (line.StartsWith("tick;")) continue;
                var f = line.Split(';'); if (f.Length < 9) throw new FormatException("fila de traza con menos de 9 columnas: " + line);
                t.Rows.Add(new TraceRow { Tick = int.Parse(f[0]), Spawned = int.Parse(f[1]), Alive = int.Parse(f[2]), Pile = int.Parse(f[3]), WallPpm = int.Parse(f[4]), Kills = int.Parse(f[5]), AmmoShots = int.Parse(f[6]), AtStation = int.Parse(f[7]), AtTrain = int.Parse(f[8]) });
            }
            return t;
        }
    }

    /// <summary>Lo que llama la zona de Unity: una vez por tick de simulación, DESPUÉS de haber aplicado la noche, la pila, el fuego y el contacto de ese tick. Guarda una fila cada «every» ticks dentro de la ventana.</summary>
    public sealed class NightTraceRecorder
    {
        public readonly NightTrace Trace = new NightTrace();
        public readonly int StartTick, EndTick, Every;
        public NightTraceRecorder(string source, int startTick, int windowTicks, int every = 30) { Trace.Source = source; StartTick = startTick; EndTick = startTick + windowTicks; Every = Math.Max(1, every); }
        public bool Done { get; private set; }

        /// <param name="wallHp">vida actual del muro (la suma de las piezas de Obras o el WallHp que use la zona)</param>
        /// <param name="wallHpMax">la de partida; si es 0 se da 1.000.000 (muro entero)</param>
        public void Sample(int tick, int spawned, int alive, int pile, long wallHp, long wallHpMax, int kills, int ammoShots, int atStation, int atTrain)
        {
            if (tick < StartTick) return; if (tick > EndTick) { Done = true; return; }
            if ((tick - StartTick) % Every != 0) return;
            int ppm = wallHpMax <= 0 ? 1_000_000 : (int)(wallHp * 1_000_000 / wallHpMax);
            Trace.Rows.Add(new TraceRow { Tick = tick, Spawned = spawned, Alive = alive, Pile = pile, WallPpm = ppm, Kills = kills, AmmoShots = ammoShots, AtStation = atStation, AtTrain = atTrain });
        }
    }

    public sealed class TraceReport
    {
        public int Fails, Warns, Oks;
        public readonly List<string> Lines = new List<string>();
        public bool Ok { get { return Fails == 0; } }
        public string Text { get { return string.Join("\n", Lines); } }
        internal void Ok_(string m) { Oks++; Lines.Add("  OK     " + m); }
        internal void Warn(string m) { Warns++; Lines.Add("  AVISO  " + m); }
        internal void Fail(string m) { Fails++; Lines.Add("  FALLO  " + m); }
    }

    public static class NightTraceTools
    {
        /// <summary>Traza de referencia con NightSim: fuerza por defecto, bot de reabastecimiento, desde el principio de la noche; se anotan los ticks de [startTick, startTick + windowTicks].</summary>
        public static NightTrace Reference(string nocheJson, string difficulty, uint seed, int startTick, int windowTicks, int every = 30)
        {
            var sim = NightSim.FromJson(nocheJson, difficulty, seed); sim.AddDefaultForce();
            var rec = new NightTraceRecorder("NightSim", startTick, windowTicks, every); int end = startTick + windowTicks;
            while (sim.Night.State == NightState.Jugando && sim.Night.Tick <= end)
            {
                sim.Step();
                if ((sim.Night.Tick & 127) == 0) sim.ResupplyBot();
                var s = sim.Snapshot();
                rec.Sample(s.Tick, s.ZombiesSpawned, s.ZombiesAlive, s.PileAtWall, s.WallHp, sim.SP.WallHp, s.Kills, s.PlatoonAmmoShots + s.StockShots, 0, 0);
            }
            return rec.Trace;
        }

        public const string ReferenceFile = "traza_nightsim_o1.csv";

        /// <summary>La referencia que lleva el repositorio (normal, semilla 1, 9.000 ticks desde la O1, una fila por segundo): se lee de Assets/TBD/Data por BalanceSource; si falta, se calcula con NightSim.</summary>
        public static NightTrace ReferenceFromData()
        {
            var csv = TBD.Net.BalanceSource.Read(ReferenceFile);
            if (csv != null) return NightTrace.FromCsv(csv);
            var json = TBD.Net.BalanceSource.Read("noche_sagunto.json");
            return Reference(json, null, 1, FirstWaveTick(json, null, 1), 9000, 30);
        }

        /// <summary>Todo en una llamada para Unity: compara la traza grabada por la zona con la referencia y devuelve el informe completo (resumen, tabla lado a lado y las dos trazas en CSV si se pide).</summary>
        public static TraceReport CompareWithReference(NightTrace zone) { return Compare(ReferenceFromData(), zone); }

        /// <summary>Tick de la primera oleada de la noche (para fijar la ventana: «los primeros minutos de la O1»).</summary>
        public static int FirstWaveTick(string nocheJson, string difficulty, uint seed) { var sim = NightSim.FromJson(nocheJson, difficulty, seed); return sim.Waves.Count > 0 ? sim.Waves[0].StartTick : 0; }

        static TraceRow? At(NightTrace t, int tick) { foreach (var r in t.Rows) if (r.Tick == tick) return r; return null; }
        static string Pct(long a, long b) { return b == 0 ? (a == 0 ? "=" : "∞") : (a * 100 / b) + " %"; }

        /// <summary>Compara la traza de la zona con la de NightSim. Los FALLO son rotura del contrato (pila siempre 0, muro que no baja, zombis que no cuadran); los AVISO son diferencias que pueden venir de la fuerza o de
        /// la geometría (abatidos, munición, hora de la primera pila). Con «pilaRate» en puntos de muro por zombi y segundo (1 en NightSim) se mide si el desgaste cuadra con la propia pila de la zona.</summary>
        public static TraceReport Compare(NightTrace reference, NightTrace zone, int wallHpMax = 0, int pilaRate = 1, int every = 30)
        {
            if (wallHpMax <= 0) wallHpMax = NightSimParams.FromJson(TBD.Net.BalanceSource.Read("nightsim_params.json")).WallHp;       // la vida de muro de partida del balance (nightsim_params.json)
            var rep = new TraceReport();
            rep.Lines.Add($"Comparación de trazas: referencia «{reference.Source}» ({reference.Rows.Count} filas) frente a «{zone.Source}» ({zone.Rows.Count} filas)");
            if (zone.Rows.Count < 2 || reference.Rows.Count < 2) { rep.Fail("faltan filas: la traza de la zona o la de referencia tiene menos de 2"); return rep; }
            int dt = zone.Rows[1].Tick - zone.Rows[0].Tick; if (dt <= 0) dt = every;
            // 1) alineación por tick: debe haber filas comunes
            int common = 0, spawnDiff = 0, firstSpawnDiff = -1; var pairs = new List<KeyValuePair<TraceRow, TraceRow>>();
            foreach (var z in zone.Rows) { var r = At(reference, z.Tick); if (r == null) continue; common++; pairs.Add(new KeyValuePair<TraceRow, TraceRow>(r.Value, z)); if (r.Value.Spawned != z.Spawned) { spawnDiff++; if (firstSpawnDiff < 0) firstSpawnDiff = z.Tick; } }
            if (common < 2) { rep.Fail("las dos trazas no tienen ticks en común: ventanas distintas (la zona debe empezar en el mismo Night.Tick y muestrear cada " + dt + " ticks)"); return rep; }
            // 2) entradas: NightGame las da exactas, así que deben ser idénticas
            if (spawnDiff == 0) rep.Ok_($"zombis que entran: idénticos en las {common} filas comunes (NightGame.Step da las mismas cuotas)");
            else rep.Fail($"zombis que entran: difieren en {spawnDiff} de {common} filas (la primera en el tick {firstSpawnDiff}); la zona no está usando NightGame.Step(spawns) tal cual");
            // 3) conservación
            var last = zone.Rows[zone.Rows.Count - 1]; long lost = last.Spawned - last.Alive - last.Kills;
            if (lost == 0) rep.Ok_($"conservación: entraron {last.Spawned} = vivos {last.Alive} + abatidos {last.Kills}");
            else if (Math.Abs(lost) * 100 <= Math.Max(1, last.Spawned)) rep.Warn($"conservación: faltan {lost} zombis de {last.Spawned} (≤ 1 %): ¿muertos por otra vía (atropello, torreta de vehículo)? Cuéntalos en «Kills»");
            else rep.Fail($"conservación rota: entraron {last.Spawned}, vivos {last.Alive}, abatidos {last.Kills}; faltan {lost}");
            // 4) la pila: ¿llega a haber? (el fallo de Noche15 era pila 0 con 6.845 vivos)
            int zPeak = 0, rPeak = 0, zFirst = -1, rFirst = -1;
            foreach (var z in zone.Rows) { if (z.Pile > zPeak) zPeak = z.Pile; if (z.Pile > 0 && zFirst < 0) zFirst = z.Tick; }
            foreach (var r in reference.Rows) { if (r.Pile > rPeak) rPeak = r.Pile; if (r.Pile > 0 && rFirst < 0) rFirst = r.Tick; }
            if (rPeak > 0 && zPeak == 0) rep.Fail($"la pila de la zona es 0 en toda la ventana y NightSim llega a {rPeak} (primera en el tick {rFirst}): la zona no cuenta los zombis pegados al muro (paso 3 del contrato)");
            else if (rPeak > 0)
            {
                rep.Ok_($"hay pila en la zona: pico {zPeak} frente a {rPeak} de NightSim ({Pct(zPeak, rPeak)})");
                int d = Math.Abs(zFirst - rFirst);
                if (d * 100 <= Math.Max(900, rFirst) * 25 && d <= 900) rep.Ok_($"la primera pila llega en el tick {zFirst} (NightSim {rFirst}): ±{d / 30.0:0.0} s");
                else rep.Warn($"la primera pila llega en el tick {zFirst} y en NightSim en el {rFirst}: {d / 30.0:0.0} s de diferencia (geometría de los caminos, velocidad del zombi 1,5 m/s)");
                if (zPeak * 4 < rPeak || zPeak > rPeak * 4) rep.Warn($"el pico de pila difiere más de ×4 ({zPeak} frente a {rPeak}): comprobar el radio de «pegado» (1,5 m) y que se cuenten solo los zombis fuera del recinto");
            }
            else rep.Warn("NightSim no llega a tener pila en esta ventana: alarga la ventana para juzgar la pila");
            // 5) el muro: ¿baja? ¿cuadra con SU pila?
            int rWall0 = reference.Rows[0].WallPpm, rWall1 = reference.Rows[reference.Rows.Count - 1].WallPpm, zWall0 = zone.Rows[0].WallPpm, zWall1 = last.WallPpm;
            long rDrop = rWall0 - rWall1, zDrop = zWall0 - zWall1;
            // pérdida esperada por la propia pila de la zona: Σ pila × tasa / 30 por tick, entre filas; en ppm de la vida de partida
            double expected = 0; for (int i = 0; i + 1 < zone.Rows.Count; i++) expected += (double)zone.Rows[i].Pile * pilaRate * (zone.Rows[i + 1].Tick - zone.Rows[i].Tick) / 30.0;
            double expectedPpm = expected * 1_000_000.0 / wallHpMax;
            if (rDrop > 0 && zDrop <= 0) rep.Fail($"el muro de la zona no baja (−{zDrop} ppm) y el de NightSim baja {rDrop} ppm: el daño de la pila no llega a las piezas de muro (paso 3 del contrato) ni a Night.WallPermil (paso 1)");
            else if (zDrop > 0)
            {
                rep.Ok_($"el muro de la zona baja {zDrop} ppm ({zDrop / 10000.0:0.00} %) frente a {rDrop} ppm de NightSim");
                if (expectedPpm > 100)
                {
                    double ratio = zDrop / expectedPpm;
                    if (ratio >= 0.8 && ratio <= 1.25) rep.Ok_($"el desgaste cuadra con la pila de la propia zona: medido / esperado = {ratio:0.00} ({pilaRate} punto por zombi y segundo, muro de partida {wallHpMax})");
                    else if (ratio < 0.2) rep.Fail($"el desgaste es {ratio:0.00} veces el esperado por su pila: casi no se aplica daño (¿solo a una pieza? ¿solo a veces?)");
                    else rep.Warn($"el desgaste es {ratio:0.00} veces el esperado por su pila ({pilaRate} punto por zombi y segundo sobre {wallHpMax}): ¿otra escala de vida del muro o de daño por zombi? Si es a propósito, pásame la escala");
                }
            }
            else rep.Warn("el muro no baja ni en la zona ni en NightSim en esta ventana: alarga la ventana");
            // 6) abatidos y munición: dependen de la fuerza desplegada, no del contrato
            var rl = reference.Rows[reference.Rows.Count - 1]; var rc = At(reference, last.Tick); if (rc != null) rl = rc.Value;
            if (rl.Kills > 0 && last.Kills == 0) rep.Warn($"abatidos: 0 en la zona y {rl.Kills} en NightSim a igual tick; si hay pelotones disparando, el fuego no se está aplicando (res.Hits → matar al zombi y Units.NoteKill)");
            else if (rl.Kills > 0) { string q = Pct(last.Kills, rl.Kills); if (last.Kills * 2 < rl.Kills || last.Kills > rl.Kills * 2) rep.Warn($"abatidos {last.Kills} frente a {rl.Kills} de NightSim ({q}): es normal si la zona despliega otra fuerza que AddDefaultForce"); else rep.Ok_($"abatidos {last.Kills} frente a {rl.Kills} de NightSim ({q})"); }
            if (zone.Rows[0].AmmoShots >= 0 && rl.AmmoShots >= 0)
            {
                long zs = zone.Rows[0].AmmoShots - last.AmmoShots, rs = reference.Rows[0].AmmoShots - rl.AmmoShots;
                if (rs > 0 && zs == 0 && last.Kills > 0) rep.Warn("munición: la zona abate pero no gasta munición (¿no descuenta AmmoSpent?)");
                else if (rs > 0) rep.Ok_($"munición gastada {zs} disparos frente a {rs} de NightSim ({Pct(zs, rs)})");
            }
            else rep.Warn("la zona no da la munición (ammo_shots = −1): no se puede comparar el gasto");
            // 7) atacantes del edificio y del tren
            int zAtt = 0, rAtt = 0; foreach (var z in zone.Rows) zAtt += z.AtStation + z.AtTrain; foreach (var r in reference.Rows) rAtt += r.AtStation + r.AtTrain;
            if (zAtt > 0) rep.Warn($"hay zombis pegados al edificio o al tren ya en esta ventana (suma {zAtt}): en NightSim con el muro en pie no llega ninguno antes de la O4; revisa por dónde entran (¿pasillo de la vía?)");
            else rep.Ok_("ningún zombi pegado al edificio ni al tren con el muro en pie (como NightSim)");
            rep.Lines.Add($"Resumen: {rep.Oks} correctas, {rep.Warns} avisos, {rep.Fails} fallos → {(rep.Ok ? "el contrato se cumple en lo esencial" : "el contrato NO se cumple")}");
            return rep;
        }

        /// <summary>Tabla tick a tick (cada «step» filas) de las dos trazas lado a lado, para pegar en un informe.</summary>
        public static string SideBySide(NightTrace reference, NightTrace zone, int step = 10)
        {
            var sb = new StringBuilder(); sb.Append("tick | entran ref/zona | vivos ref/zona | pila ref/zona | muro‰ ref/zona | abatidos ref/zona\n"); int n = 0;
            foreach (var z in zone.Rows)
            {
                if (n++ % step != 0) continue; var r = At(reference, z.Tick); if (r == null) continue; var q = r.Value;
                sb.Append(z.Tick).Append(" | ").Append(q.Spawned).Append('/').Append(z.Spawned).Append(" | ").Append(q.Alive).Append('/').Append(z.Alive).Append(" | ").Append(q.Pile).Append('/').Append(z.Pile).Append(" | ").Append(q.WallPpm / 1000).Append('/').Append(z.WallPpm / 1000).Append(" | ").Append(q.Kills).Append('/').Append(z.Kills).Append('\n');
            }
            return sb.ToString();
        }
    }
}
