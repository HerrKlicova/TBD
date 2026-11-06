using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>
    /// Guardado y carga deterministas con versión (GDD §14 y §17.3, regla 8). La prueba de fondo es doble: (1) la comparación estructural por reflexión
    /// de TODOS los campos del mundo cargado contra el original (también los privados y los que no entran en el hash), y (2) seguir jugando los dos
    /// mundos con las mismas órdenes y ver que dan el mismo hash en cada tick.
    /// </summary>
    static class GuardadoTests
    {
        // Lo que NO se guarda a propósito: datos de QA «de este tick», el gancho de la zona y búferes de trabajo que se rehacen solos.
        static readonly HashSet<string> Skip = new HashSet<string> { "Injections", "Retirements", "LostThisTick", "RetiredThisTick", "ZoneDensity", "_next", "_pot", "_nextS", "_block" };

        static string Diff(object a, object b, string path, HashSet<(object, object)> seen)
        {
            if (a == null || b == null) return a == null && b == null ? null : path + ": " + (a == null ? "null" : "valor") + " ≠ " + (b == null ? "null" : "valor");
            var t = a.GetType();
            if (t != b.GetType()) return path + ": tipos distintos";
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)) return a.Equals(b) ? null : path + ": " + a + " ≠ " + b;
            if (typeof(Delegate).IsAssignableFrom(t)) return null;
            if (!t.IsValueType) { if (ReferenceEquals(a, b)) return null; if (!seen.Add((a, b))) return null; }
            if (a is Array aa)
            {
                var ab = (Array)b;
                if (aa.Rank != ab.Rank) return path + ": rango distinto";
                for (int d = 0; d < aa.Rank; d++) if (aa.GetLength(d) != ab.GetLength(d)) return path + ": longitud distinta " + aa.GetLength(d) + " ≠ " + ab.GetLength(d);
                var ea = aa.GetEnumerator(); var eb = ab.GetEnumerator(); int i = 0;
                while (ea.MoveNext() && eb.MoveNext()) { var r = Diff(ea.Current, eb.Current, path + "[" + i + "]", seen); if (r != null) return r; i++; }
                return null;
            }
            if (a is IEnumerable ie && t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                var la = new List<long>(); foreach (var x in ie) la.Add(Convert.ToInt64(x));
                var lb = new List<long>(); foreach (var x in (IEnumerable)b) lb.Add(Convert.ToInt64(x));
                la.Sort(); lb.Sort();
                if (la.Count != lb.Count) return path + ": conjunto de otro tamaño";
                for (int i = 0; i < la.Count; i++) if (la[i] != lb[i]) return path + ": conjunto distinto";
                return null;
            }
            if (a is IList l1)
            {
                var l2 = (IList)b;
                if (l1.Count != l2.Count) return path + ": lista de " + l1.Count + " ≠ " + l2.Count;
                for (int i = 0; i < l1.Count; i++) { var r = Diff(l1[i], l2[i], path + "[" + i + "]", seen); if (r != null) return r; }
                return null;
            }
            for (var tt = t; tt != null && tt != typeof(object); tt = tt.BaseType)
                foreach (var f in tt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (Skip.Contains(f.Name)) continue;
                    var r = Diff(f.GetValue(a), f.GetValue(b), path + "." + f.Name, seen);
                    if (r != null) return r;
                }
            return null;
        }

        public static string DeepDiff(NetWorld a, NetWorld b) { return Diff(a, b, "mundo", new HashSet<(object, object)>()); }

        static int Id(NetWorld w, string name) { for (int i = 0; i < w.Graph.Nodes.Count; i++) if (w.Graph.Nodes[i].Name == name) return i; return -1; }
        static int[] Stop(int node, long at, int mun = 0, int comb = 0, int mat = 0, int per = 0) { return new[] { node, (int)at, mun, comb, mat, per }; }

        /// <summary>Un guion con de todo: velocidad, taller, zona materializada, obras con obreros, horario nuevo, retención de trenes y consumo de munición.</summary>
        static List<NetCommand> RichScript(NetWorld w)
        {
            long T(double h) { return w.Tick + NetConst.Hours(h); }
            int sag = Id(w, "Sagunto"), ter = Id(w, "Teruel");
            return new List<NetCommand>
            {
                new NetCommand { Tick = T(0.2), Type = NetCmdType.SetSpeed, Arg = 4 },
                new NetCommand { Tick = T(0.3), Type = NetCmdType.SetSchedule, Arg = 1, Payload = new[] { sag, (int)NetConst.Hours(7.5), 0, 0, 5, 0, ter, (int)NetConst.Hours(15), 0, 0, 0, 0 } },
                new NetCommand { Tick = T(0.5), Type = NetCmdType.OrderTrain, Arg = (int)TallerKind.RehabObras },
                new NetCommand { Tick = T(0.6), Type = NetCmdType.OrderTrain, Arg = (int)TallerKind.RehabTren3 },
                new NetCommand { Tick = T(1.0), Type = NetCmdType.Materialize, Arg = sag },
                new NetCommand { Tick = T(1.2), Type = NetCmdType.PlaceWork, Arg = sag | ((int)PieceKind.Hesco << 16), Payload = new[] { -20_000, 10_000, 20_000, 10_000 } },
                new NetCommand { Tick = T(1.3), Type = NetCmdType.AssignWorkers, Arg = sag, Arg2 = 12 },
                new NetCommand { Tick = T(1.4), Type = NetCmdType.PlaceWork, Arg = sag | ((int)PieceKind.Foco << 16), Payload = new[] { 5_000, -12_000 } },
                new NetCommand { Tick = T(3.0), Type = NetCmdType.TrainHold, Arg = 0 },
                new NetCommand { Tick = T(4.0), Type = NetCmdType.TrainGo, Arg = 0 },
                new NetCommand { Tick = T(9.0), Type = NetCmdType.ConsumeAmmo, Arg = sag, Arg2 = 3 },
                new NetCommand { Tick = T(14.0), Type = NetCmdType.Dematerialize, Arg = sag, Arg2 = 0 },
                new NetCommand { Tick = T(40.0), Type = NetCmdType.SetSpeed, Arg = 1 },
            };
        }

        static void Run(NetWorld w, long ticks) { long end = w.Tick + ticks; int stalled = 0; while (w.Tick < end) { long b = w.Tick; w.Step(); if (w.Tick == b && ++stalled > 1000) break; } }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[28] Guardado y carga deterministas con versión (GDD §14 y §17.3)");
            var w = NetScenario.Mvp();
            foreach (var c in RichScript(w)) w.Push(c);

            // ---- puntos de control a lo largo de unos 3 días, con la zona materializada, obras en curso, taller, tren de obras, escombros…
            var marks = new[] { 0.0, 1.5, 6.0, 14.5, 20.0, 30.0, 52.0, 70.0 };      // horas desde el arranque
            long t0 = w.Tick;
            bool allDeep = true, allBytes = true, allLock = true; string firstBad = "";
            int nChecks = 0; long maxBytes = 0, lastSize = 0;
            byte[] lastSave = null; NetWorld lastLoaded = null;
            bool sawZone = false, sawWorkInProgress = false, sawQueue = false, sawIntel = false, sawDebris = false;
            foreach (double h in marks)
            {
                long target = t0 + NetConst.Hours(h);
                while (w.Tick < target) w.Step();
                if (h >= 20.0 && !sawDebris) { w.DestroyEdge(3); sawDebris = true; }              // un tramo con escombros: para que el estado de los tramos no esté vacío
                sawZone |= w.ActiveZoneNode >= 0; sawWorkInProgress |= w.WorkInProgress(Id(w, "Sagunto")); sawQueue |= w.Taller.Queue.Count > 0; sawIntel |= w.Intel.Count > 0;
                var bytes = NetSave.Save(w);
                var bytes2 = NetSave.Save(w);
                bool same = bytes.Length == bytes2.Length; for (int i = 0; same && i < bytes.Length; i++) same = bytes[i] == bytes2[i];
                var l = NetScenario.Mvp(false);
                var info = NetSave.Load(bytes, l);
                string d = DeepDiff(w, l);
                var re = NetSave.Save(l);
                bool ident = re.Length == bytes.Length; for (int i = 0; ident && i < re.Length; i++) ident = re[i] == bytes[i];
                nChecks++;
                if (!same) { allBytes = false; firstBad += $" [dos guardados distintos en {h} h]"; }
                if (!ident) { allBytes = false; firstBad += $" [guardar lo cargado no da el mismo archivo en {h} h]"; }
                if (d != null) { allDeep = false; firstBad += $" [{h} h: {d}]"; }
                if (info.Tick != w.Tick || info.Hash != w.Hash()) { allDeep = false; firstBad += " [cabecera]"; }
                // seguir jugando los dos con las mismas órdenes: mismo hash en cada tick
                var twin = l;
                for (int k = 0; k < 1500 && allLock; k++) { w.Step(); twin.Step(); if (w.Hash() != twin.Hash()) { allLock = false; firstBad += $" [hash distinto {k} ticks después de {h} h]"; } }
                // el original ha avanzado 1.500 ticks: el gemelo cargado también; se vuelve a guardar el original en el siguiente punto
                maxBytes = Math.Max(maxBytes, bytes.Length); lastSize = bytes.Length; lastSave = bytes; lastLoaded = twin;
            }
            check(allBytes, $"guardar dos veces da el mismo archivo, y guardar lo cargado da el mismo archivo, en {nChecks} puntos de la partida{firstBad}");
            check(allDeep, $"el mundo cargado es idéntico al original en TODOS sus campos (reflexión, también los privados): {nChecks} puntos{firstBad}");
            check(allLock, $"original y cargado siguen dando el mismo hash tick a tick durante 1.500 ticks desde cada punto");
            check(sawZone && sawWorkInProgress && sawQueue && sawIntel && sawDebris, $"los puntos incluyeron zona materializada ({sawZone}), obra en curso ({sawWorkInProgress}), cola del taller ({sawQueue}), informe ({sawIntel}) y escombros ({sawDebris})");

            // ---- una partida larga a partir de un guardado: 2 días más, hash a hash
            {
                var a = NetScenario.Mvp(false); NetSave.Load(NetSave.Save(w), a);
                var b = NetScenario.Mvp(false); NetSave.Load(NetSave.Save(w), b);
                bool eq = true; long sw = 0;
                var sh = Stopwatch.StartNew();
                for (int k = 0; k < 2 * NetConst.DayTicks && eq; k++) { a.Step(); w.Step(); b.Step(); if ((k & 15) == 0 || k < 600) { ulong ha = a.Hash(); if (ha != w.Hash() || ha != b.Hash()) { eq = false; sw = k; } } }
                sh.Stop();
                check(eq && DeepDiff(w, a) == null && DeepDiff(w, b) == null, $"2 días más (72.000 ticks) desde un guardado: hash idéntico en el original y en dos cargas, y mismo estado final{(eq ? "" : " (difiere en " + sw + ")")} ({sh.ElapsedMilliseconds} ms)");
            }

            // ---- estado artificial: emboscada materializada, planes de la Colmena y cola de órdenes pendientes
            {
                var x = NetScenario.Mvp(); Run(x, 3000);
                x.Encounter = new NetWorld.AmbushEncounter { Active = true, Service = 0, Edge = 1, Cell = 77, Taken = 12, ExpireTick = x.Tick + 700 };
                x.PendingAmbushes.Add(2); x.PendingAmbushes.Add(5);
                x.Director.Active.Add(new HiveDirector.Plan { Kind = HiveDirector.PlanKind.Asedio, Node = 1, Cell = 40, UntilTick = x.Tick + 9000, Moved = 33, Train = -1, StartTick = x.Tick + 4500, Started = false, Lost = 2 });
                x.Director.Seed = 0xDEADBEEF12345678UL; x.Director.IgnoreAttention = true;
                x.Push(new NetCommand { Tick = x.Tick + 500, Type = NetCmdType.SetSpeed, Arg = 2 });
                x.Push(new NetCommand { Tick = x.Tick + 900, Type = NetCmdType.PlaceWork, Arg = 0 | ((int)PieceKind.Sacos << 16), Payload = new[] { 0, 3000, 12_000, 3000 } });
                x.Paused = true; x.AutoSlow = false;
                var y = NetScenario.Mvp(false);
                SaveInfo inf = null; string err = null;
                try { inf = NetSave.Load(NetSave.Save(x), y); } catch (SaveException e) { err = e.Message; }
                check(err == null && DeepDiff(x, y) == null, $"un estado con emboscada, plan de asedio, cola de órdenes con carga útil, pausa y semilla se carga idéntico {err ?? DeepDiff(x, y)}");
            }

            // ---- errores: un archivo malo nunca se carga a medias sin avisar
            {
                var good = NetSave.Save(w);
                SaveError Try(byte[] b, Func<NetWorld> make = null) { try { NetSave.Load(b, make != null ? make() : NetScenario.Mvp(false)); return SaveError.None; } catch (SaveException e) { return e.Error; } }
                bool allTrunc = true; int cuts = 0;
                for (int n = 0; n < good.Length; n += Math.Max(1, good.Length / 300)) { cuts++; var e = Try(CopyOf(good, n)); if (e == SaveError.None) { allTrunc = false; break; } }
                check(allTrunc, $"un guardado recortado ({cuts} longitudes distintas) siempre da error y no carga");
                bool allFlip = true; int flips = 0; var rnd = new Random(7);
                for (int k = 0; k < 400; k++) { var b = (byte[])good.Clone(); int i = rnd.Next(b.Length); b[i] ^= (byte)(1 << rnd.Next(8)); flips++; if (Try(b) == SaveError.None) { allFlip = false; break; } }
                check(allFlip, $"un solo bit cambiado en cualquier parte ({flips} pruebas) se detecta (suma de comprobación)");
                var badMagic = (byte[])good.Clone(); badMagic[0] ^= 0xFF; Reseal(badMagic);
                check(Try(badMagic) == SaveError.BadMagic, "un archivo que no es de TBD da BadMagic");
                var newFmt = (byte[])good.Clone(); newFmt[4] = 9; Reseal(newFmt);
                check(Try(newFmt) == SaveError.TooNew, "un formato de contenedor más nuevo que el del juego da TooNew (actualiza el juego)");
                var info0 = NetSave.Peek(good);
                check(info0.FormatVersion == NetSave.FormatVersion && info0.NetVersion == NetSave.NetVersion && info0.Tick == w.Tick && info0.Hash == w.Hash() && NetSave.Peek(good).Find("NET\0") != null, $"Peek lee versión ({info0.FormatVersion}/{info0.NetVersion}), tick y hash sin cargar nada");

                // versiones de la sección de la red
                byte[] WithNetVersion(ushort v, byte[] prefix)
                {
                    var inf = NetSave.Peek(good); var net = inf.Find("NET\0");
                    var data = new byte[prefix.Length + net.Data.Length]; Array.Copy(prefix, data, prefix.Length); Array.Copy(net.Data, 0, data, prefix.Length, net.Data.Length);
                    var sw2 = new SaveWriter();
                    sw2.UInt(NetSave.Magic); sw2.UShort(NetSave.FormatVersion); sw2.UShort(0); sw2.ULong(inf.TopologyHash); sw2.ULong(inf.ParamsHash); sw2.ULong((ulong)inf.Tick); sw2.ULong(inf.Hash); sw2.UShort(1);
                    sw2.UInt(net.Id); sw2.UShort(v); sw2.UInt((uint)data.Length); sw2.Raw(data);
                    var body = sw2.ToArray(); var f = new SaveWriter(); f.Raw(body); f.ULong(NetSave.Checksum(body, body.Length)); return f.ToArray();
                }
                check(Try(WithNetVersion((ushort)(NetSave.NetVersion + 1), new byte[0])) == SaveError.TooNew, "una red guardada con una versión más nueva da TooNew");
                check(Try(WithNetVersion(0, new byte[0])) == SaveError.TooOld, "una versión antigua sin migración registrada da TooOld");
                NetSave.NetMigrations[0] = old => { var r = new byte[old.Length - 2]; Array.Copy(old, 2, r, 0, r.Length); return r; };      // migración de prueba: la versión 0 llevaba 2 bytes de prefijo
                try
                {
                    var l = NetScenario.Mvp(false); SaveInfo mi = null; SaveError e = SaveError.None;
                    try { mi = NetSave.Load(WithNetVersion(0, new byte[] { 0xAB, 0xCD }), l); } catch (SaveException ex) { e = ex.Error; }
                    check(e == SaveError.None && mi != null && mi.Migrated && mi.NetVersion == 0 && DeepDiff(w, l) == null, "con una migración registrada, una partida de la versión 0 se migra y carga idéntica (Migrated = true)");
                }
                finally { NetSave.NetMigrations.Remove(0); }

                // otro mapa
                Func<NetWorld> other = () => { var o = NetScenario.Mvp(false); o.Graph.AddNode("Utopía", 10, 10); return new NetWorld(o.Graph); };
                check(Try(good, other) == SaveError.TopologyMismatch, "cargar en un mapa distinto da TopologyMismatch");

                // balance distinto: carga igualmente y avisa
                var rb = NetScenario.Mvp(false); rb.Works.P.OutsideWearMil = 25;
                var ri = NetSave.Load(good, rb);
                check(ri.BalanceChanged && DeepDiff(w, rb) != null && rb.Hash() == w.Hash(), "si Hugo cambia el balance, el guardado carga con el balance nuevo y lo avisa (BalanceChanged)");
                var same2 = NetScenario.Mvp(false);
                check(!NetSave.Load(good, same2).BalanceChanged, "con el mismo balance no hay aviso");

                // secciones extra (la zona en detalle la añadirá quien la serialice)
                var zone = new SaveSection { Id = SaveSection.Tag("ZON\0"), Version = 3, Data = new byte[] { 1, 2, 3, 4, 5 } };
                var withZone = NetSave.Save(w, new List<SaveSection> { zone });
                var wzi = NetSave.Peek(withZone); var zs = wzi.Find("ZON\0");
                var lz = NetScenario.Mvp(false); NetSave.Load(withZone, lz);
                check(zs != null && zs.Version == 3 && zs.Data.Length == 5 && zs.Data[4] == 5 && DeepDiff(w, lz) == null, "una sección extra (ZON) viaja en el archivo, se recupera con Peek y la red carga igual");
            }

            // ---- tamaño y tiempo (GDD §14: «medir su tamaño y tiempo con una red grande y con la memoria de la Colmena acumulada»)
            {
                var big = NetScenario.Mvp(); foreach (var c in RichScript(big)) big.Push(c);
                var sw = Stopwatch.StartNew();
                Run(big, 20 * NetConst.DayTicks);                      // 20 días: memoria de tráfico, informe y planes acumulados
                sw.Stop();
                int best = int.MaxValue; byte[] data = null; double saveMs = double.MaxValue, loadMs = double.MaxValue;
                for (int rep = 0; rep < 5; rep++)
                {
                    var s1 = Stopwatch.StartNew(); data = NetSave.Save(big); s1.Stop(); saveMs = Math.Min(saveMs, s1.Elapsed.TotalMilliseconds);
                    var tgt = NetScenario.Mvp(false);
                    var s2 = Stopwatch.StartNew(); NetSave.Load(data, tgt); s2.Stop(); loadMs = Math.Min(loadMs, s2.Elapsed.TotalMilliseconds);
                }
                int gz; using (var ms = new MemoryStream()) { using (var g = new GZipStream(ms, CompressionLevel.Optimal, true)) g.Write(data, 0, data.Length); gz = (int)ms.Length; }
                Console.WriteLine($"         20 días de juego ({sw.ElapsedMilliseconds} ms): {data.Length / 1024.0:0.0} KB ({gz / 1024.0:0.0} KB con gzip), guardar {saveMs:0.00} ms, cargar {loadMs:0.00} ms; {big.Intel.Count} avisos de inteligencia, {big.Services.Count} trenes, {big.Works.Count(Id(big, "Sagunto"))} piezas en Sagunto");
                check(data.Length < 200 * 1024 && saveMs < 100 && loadMs < 200, $"con 20 días acumulados el guardado pesa {data.Length / 1024.0:0.0} KB (< 200) y se guarda en {saveMs:0.00} ms (< 100) y carga en {loadMs:0.00} ms (< 200)");
                var chk = NetScenario.Mvp(false); NetSave.Load(data, chk);
                check(DeepDiff(big, chk) == null, "tras 20 días, el mundo cargado también es idéntico campo a campo");
            }
        }

        static byte[] CopyOf(byte[] b, int n) { var r = new byte[n]; Array.Copy(b, r, n); return r; }

        /// <summary>Recalcula la suma de comprobación de un archivo modificado a mano (para probar los otros errores sin que salte siempre BadChecksum).</summary>
        static void Reseal(byte[] b)
        {
            ulong c = NetSave.Checksum(b, b.Length - 8);
            for (int i = 0; i < 8; i++) { b[b.Length - 8 + i] = (byte)c; c >>= 8; }
        }
    }
}
