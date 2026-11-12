using System;
using System.Collections.Generic;
using TBD.Mundo;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>[50] Núcleo v2, Hito 1 (Nacho): la simulación única (Mundo), la rejilla por trozos y los zombis del caserío.</summary>
    public static class MundoTests
    {
        static bool NadieEnCasa(TBD.Mundo.Mundo m)
        {
            foreach (var z in m.Zombis.Lista) if (z.Vivo) foreach (var c in m.Casas) if (c.TapaPaso && z.X > c.R.X0 && z.X < c.R.X1 && z.Y > c.R.Y0 && z.Y < c.R.Y1) return false;
            return true;
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[50] Núcleo v2: Mundo, rejilla por trozos y zombis del caserío");

            if (TBD.Mundo.Mundo.Lector == null) { string dir = System.IO.Path.Combine(BalanceTests.DataDirPublic(), "..", "..", "StreamingAssets", "Hito1"); TBD.Mundo.Mundo.Lector = f => { var pth = System.IO.Path.Combine(dir, f); return System.IO.File.Exists(pth) ? System.IO.File.ReadAllText(pth) : null; }; }
            // --- rejilla por trozos
            var rej = new RejillaTrozos(2000000, 2000000, 350);
            rej.PonerObstaculos(new List<RectMm> { new RectMm(1000000, 990000, 1002000, 1010000) });      // un muro de 2 × 20 m
            check(rej.TrozosMontados == 0, "la rejilla no monta ningún trozo hasta que se pregunta");
            check(rej.Libre(500000, 500000) && rej.TrozosMontados == 0, "un trozo sin obstáculos no se monta");
            check(!rej.Libre(1001000, 1000000) && rej.TrozosMontados == 1, "el trozo con el muro se monta al preguntar y la celda está bloqueada");
            var cam = new List<PointMm>();
            bool ok = rej.BuscarCamino(990000, 1000000, 1012000, 1000000, cam);
            bool limpio = ok && cam.Count >= 2; int px = 990000, py = 1000000; foreach (var q in cam) { if (!rej.LineaLibre(px, py, q.X, q.Y)) limpio = false; px = q.X; py = q.Y; }
            check(limpio && cam[cam.Count - 1].X == 1012000, $"A* rodea el muro ({cam.Count} puntos, {rej.UltimosExpandidos} celdas abiertas, {rej.TrozosMontados} trozos montados)");
            var cam2 = new List<PointMm>(); rej.BuscarCamino(990000, 1000000, 1012000, 1000000, cam2); bool igual = cam.Count == cam2.Count; for (int i = 0; igual && i < cam.Count; i++) igual = cam[i].X == cam2[i].X && cam[i].Y == cam2[i].Y;
            check(igual, "el mismo camino dos veces (determinista)");

            // --- el caserío quieto: nadie atraviesa casas y deambulan cerca de su casa
            var m = TBD.Mundo.Mundo.Caserio(7);
            check(m.Zombis.Lista.Count == 100 && m.Unidades.Platoons.Count == 1, $"caserío de Álvaro: 100 zombis y un pelotón ({m.Unidades.Platoons[0].Men.Count} soldados)");
            bool fuera = NadieEnCasa(m); int lejos = 0;
            for (int t = 0; t < 30 * 120; t++) { m.Step(); if (!NadieEnCasa(m)) fuera = false; }
            foreach (var z in m.Zombis.Lista) { long dx = z.X - z.CasaX, dy = z.Y - z.CasaY; if (dx * dx + dy * dy > 20000L * 20000) lejos++; }
            int quietosODeambulan = 0; foreach (var z in m.Zombis.Lista) if (z.Estado == EstadoZombi.Quieto || z.Estado == EstadoZombi.Deambula) quietosODeambulan++;
            check(fuera, "2 min sin pelotón cerca: ningún zombi dentro de una casa");
            check(lejos == 0 && quietosODeambulan == 100 && m.Abatidos == 0, $"sin pelotón cerca se quedan junto a su casa (a más de 20 m: {lejos}; quietos o deambulando: {quietosODeambulan})");

            // --- el pelotón entra en el caserío disparando: lo ven, lo persiguen, y lo limpia
            var m2 = TBD.Mundo.Mundo.Caserio(7); int pid = m2.Unidades.Platoons[0].Id;
            m2.Encolar(new Orden { Tick = 0, Jugador = 1, Tipo = TipoOrden.AtacarMoviendo, A = pid, B = TBD.Mundo.Mundo.CaserioX, C = TBD.Mundo.Mundo.CaserioY, D = -1 });
            bool fuera2 = true; int maxPersiguen = 0, tickLimpio = -1;
            for (int t = 0; t < 30 * 600 && tickLimpio < 0; t++)
            {
                m2.Unidades.Platoons[0].AmmoShots = m2.Unidades.Platoons[0].AmmoCap;      // munición sin límite: aquí se mira el combate, no la munición
                m2.Step(); if (!NadieEnCasa(m2)) fuera2 = false;
                int pe = 0; foreach (var z in m2.Zombis.Lista) if (z.Vivo && (z.Estado == EstadoZombi.Persigue || z.Estado == EstadoZombi.Ruido)) pe++; if (pe > maxPersiguen) maxPersiguen = pe;
                if (m2.Zombis.Vivos == 0) tickLimpio = m2.Tick;
            }
            var pl = m2.Unidades.Platoons[0];
            Console.WriteLine($"       caserío con el pelotón: abatidos {m2.Abatidos}, vivos {m2.Zombis.Vivos}, bajas {m2.BajasPropias}, munición gastada {pl.AmmoSpent} disparos, limpio en el tick {tickLimpio}, máx. persiguiendo {maxPersiguen}");
            check(maxPersiguen >= 10, $"los zombis reaccionan al pelotón (máximo a la vez persiguiendo u oyendo: {maxPersiguen})");
            check(fuera2, "con combate: ningún zombi dentro de una casa");
            check(tickLimpio > 0 && m2.BajasPropias <= 2, $"con munición sin límite y la puntería del Hito 1 limpia el caserío en {tickLimpio / 30} s con {m2.BajasPropias} bajas");
            // el premio de limpiar (Hugo y Lucía): el pelotón coge los 4 palés, va más despacio y sin disparar, y al llegar a 25 m de B los deja en el almacén
            {
                int bm = m2.Red.EstacionPorNombre("B"); var pbm = m2.Puntos["B"]; var pp = m2.Unidades.Platoons[0]; int antes = m2.Red.Existencias(bm, Red.Material);
                bool lleva = m2.LlevaMaterial(pid) && pp.HoldFire; int velLleva = pp.SpeedMmS;
                m2.Encolar(new Orden { Tick = m2.Tick, Jugador = 1, Tipo = TipoOrden.FuegoLibre, A = pid, B = 1 });      // el jugador intenta que dispare: no vale
                m2.Encolar(new Orden { Tick = m2.Tick, Jugador = 1, Tipo = TipoOrden.Mover, A = pid, B = pbm.X, C = pbm.Y, D = -1 });
                bool siempreAlto = true;
                for (int t = 0; t < 30 * 600 && !m2.MaterialEnB; t++) { m2.Step(); if (!m2.MaterialEnB && !pp.HoldFire) siempreAlto = false; }
                int despues = m2.Red.Existencias(bm, Red.Material);
                check(m2.CaserioLimpio && antes == 0 && lleva && siempreAlto && m2.MaterialEnB && despues == TBD.Mundo.Mundo.MaterialCaserioPales && !pp.HoldFire && pp.SpeedMmS > velLleva && m2.Portador < 0,
                    $"material del caserío: lo coge el pelotón ({velLleva} mm/s cargado, {pp.SpeedMmS} después; sin disparar: {siempreAlto}); {antes} palés en B al limpiar y {despues} al volver (tick {m2.Tick}); log «{string.Join(" | ", m2.Eventos.FindAll(e => e.StartsWith("[MATERIAL]")))}»");
            }
            // sin línea (35 cartuchos por soldado y nada en B): se queda seco a mitad, se retira solo a B y no muere entero (objetivo de Lucía y Hugo)
            var m3 = TBD.Mundo.Mundo.Caserio(7); var p3 = m3.Unidades.Platoons[0]; m3.Red.Sacar(m3.Red.EstacionPorNombre("B"), Red.Municion, 1000); bool seRetira = false;
            m3.Encolar(new Orden { Tick = 0, Jugador = 1, Tipo = TipoOrden.AtacarMoviendo, A = p3.Id, B = TBD.Mundo.Mundo.CaserioX, C = TBD.Mundo.Mundo.CaserioY, D = -1 });
            for (int t = 0; t < 30 * 900; t++) { m3.Step(); if (m3.SeRetira(p3.Id)) seRetira = true; }
            var bpt = m3.Puntos["B"]; int dB = IMath.Dist(p3.AX - bpt.X, p3.AY - bpt.Y);
            check(p3.AmmoCap == 35 * p3.Men.Count && seRetira && p3.AliveCount() >= 6 && m3.Abatidos >= 30 && m3.Abatidos <= 85 && dB <= 30000, $"sin línea: 35 por soldado, abate {m3.Abatidos}, se queda seco, se retira y acaba en B (a {dB / 1000} m) con {p3.AliveCount()} de {p3.Men.Count} vivos");


            // --- la unión de los dos géneros: en B se recarga de las cajas de la red; sin cajas (sin tren) no hay recarga, con las que deja el tren sí
            var r0 = TBD.Mundo.Mundo.Caserio(5); var r1 = TBD.Mundo.Mundo.Caserio(5); int b0 = r0.Red.EstacionPorNombre("B");
            check(b0 >= 0 && r0.Red.Existencias(b0, Red.Municion) == TBD.Mundo.Mundo.CajasInicialesB, $"B empieza con {r0.Red.Existencias(b0, Red.Municion)} caja de {TBD.Mundo.Mundo.CartuchosPorCaja} cartuchos");
            r0.Red.Sacar(b0, Red.Municion, 1000); r1.Red.Sacar(b0, Red.Municion, 1000); r1.Red.Meter(b0, Red.Municion, 4);      // r1: el tren ha dejado 4 cajas
            var q0 = r0.Unidades.Platoons[0]; var q1 = r1.Unidades.Platoons[0]; q0.AmmoShots = 10; q1.AmmoShots = 10;
            for (int t = 0; t < 90; t++) { r0.Step(); r1.Step(); }
            check(q0.AmmoShots == 10 && r0.CajasAbiertas == 0, $"sin cajas en B (sin tren) no hay recarga ({q0.AmmoShots} de {q0.AmmoCap})");
            check(q1.AmmoShots == q1.AmmoCap && r1.CajasAbiertas == 3 && r1.Red.Existencias(b0, Red.Municion) == 1 && r1.SueltosEnB == 3 * TBD.Mundo.Mundo.CartuchosPorCaja - (q1.AmmoCap - 10),
                $"con las 4 cajas del tren se llena ({q1.AmmoShots} de {q1.AmmoCap}): abre {r1.CajasAbiertas}, queda {r1.Red.Existencias(b0, Red.Municion)} caja y {r1.SueltosEnB} cartuchos sueltos");
            int sinCajas0 = r0.Eventos.FindAll(e => e.StartsWith("[RECARGA] sin cajas en B")).Count;
            check(sinCajas0 == 1 && r0.Eventos.Count == 1 && r1.Eventos.Count == 1 && r1.Eventos[0].StartsWith("[RECARGA] 3 cajas → " + (q1.AmmoCap - 10) + " cartuchos"),
                $"log de recarga: sin tren «{(r0.Eventos.Count > 0 ? r0.Eventos[0] : "-")}» una sola vez en 3 s ({r0.Eventos.Count}); con tren «{(r1.Eventos.Count > 0 ? r1.Eventos[0] : "-")}»");
            // --- Mundo8 de Marcos: el jugador manda al caserío un pelotón seco (AtacarMoviendo sin munición). Obedece, pero al pararse fuera de B vuelve solo a B.
            {
                var w = TBD.Mundo.Mundo.Caserio(5); var pw = w.Unidades.Platoons[0]; int bw = w.Red.EstacionPorNombre("B"); w.Red.Sacar(bw, Red.Municion, 1000); pw.AmmoShots = 0;
                w.Encolar(new Orden { Tick = 30, Jugador = 1, Tipo = TipoOrden.AtacarMoviendo, A = pw.Id, B = TBD.Mundo.Mundo.CaserioX, C = TBD.Mundo.Mundo.CaserioY, D = -1 });
                var pb = w.Puntos["B"]; long lejosW = 0; bool vuelto = false;
                for (int t = 0; t < 30 * 600 && !vuelto; t++)
                {
                    w.Step(); long dx = pw.AX - pb.X, dy = pw.AY - pb.Y, d2 = dx * dx + dy * dy; if (d2 > lejosW) lejosW = d2;
                    vuelto = lejosW > 100000L * 100000L && d2 <= (long)w.RecargaRadioMm * w.RecargaRadioMm;
                }
                check(vuelto, $"seco y mandado al caserío: va (hasta {Math.Sqrt(lejosW) / 1000:0} m de B), se para y vuelve solo a B ({pw.AliveCount()} de 8 vivos, tick {w.Tick}); el log dice «{(w.Eventos.Count > 0 ? w.Eventos[w.Eventos.Count - 1] : "-")}»");
            }
            // --- determinismo: dos instancias con las mismas órdenes dan el mismo hash cada tick durante 10 min
            var a = TBD.Mundo.Mundo.Caserio(11); var b = TBD.Mundo.Mundo.Caserio(11); int pa = a.Unidades.Platoons[0].Id;
            var ords = new[] {
                new Orden { Tick = 30, Jugador = 1, Tipo = TipoOrden.Mover, A = pa, B = TBD.Mundo.Mundo.CaserioX - 60000, C = TBD.Mundo.Mundo.CaserioY, D = -1 },
                new Orden { Tick = 1800, Jugador = 2, Tipo = TipoOrden.AtacarMoviendo, A = pa, B = TBD.Mundo.Mundo.CaserioX, C = TBD.Mundo.Mundo.CaserioY + 5000, D = -1 },
                new Orden { Tick = 9000, Jugador = 1, Tipo = TipoOrden.Formacion, A = pa, B = (int)Formation.Linea } };
            foreach (var o in ords) { a.Encolar(o); b.Encolar(o); }
            string desync = null;
            for (int t = 0; t < 30 * 600 && desync == null; t++) { a.Step(); b.Step(); desync = TBD.Mundo.Mundo.CompararLineas(a.LineaHash(), b.LineaHash()); }
            check(desync == null && a.Tick == 18000, $"dos instancias, 10 min de juego (18.000 ticks): el mismo hash en cada tick por sistema ({a.LineaHash()})");
            var c2 = TBD.Mundo.Mundo.Caserio(12); for (int t = 0; t < 300; t++) c2.Step(); var a2 = TBD.Mundo.Mundo.Caserio(11); for (int t = 0; t < 300; t++) a2.Step();
            string dif = TBD.Mundo.Mundo.CompararLineas(a2.LineaHash(), c2.LineaHash());
            check(dif != null && dif.StartsWith("desync=True@300") && dif.Contains("zombis"), $"con otra semilla salta la desincronización y dice qué sistema difiere ({dif})");

            // --- grabar y reproducir
            string grab = a.GrabacionTexto(TBD.Mundo.Mundo.EscenarioCaserio);
            var ordenes = TBD.Mundo.Mundo.OrdenesDe(grab, out uint sem, out string esc);
            var r = TBD.Mundo.Mundo.Caserio(sem); foreach (var o in ordenes) r.Encolar(o);
            for (int t = 0; t < 18000; t++) r.Step();
            check(esc == TBD.Mundo.Mundo.EscenarioCaserio && ordenes.Count == 3 && r.Hash() == a.Hash(), $"reproducir la grabación (semilla {sem}, {ordenes.Count} órdenes) da el mismo hash al final");

            // --- la red de Iker entra por los parciales: sin ella, su hash es 0 y sus órdenes no rompen nada
            var n = TBD.Mundo.Mundo.Caserio(3); int ns0 = n.HashPorSistema().Count; n.Encolar(new Orden { Tick = 0, Jugador = 1, Tipo = TipoOrden.RedPrimera }); n.Step();
            check(ns0 >= 3 && n.Grabacion.Count == 1, "las órdenes de la red (tipo ≥ 100) se aceptan y se graban (con la red ya dentro: " + ns0 + " sistemas en el hash)");
        }

        /// <summary>TBD_K_REPORT=1 (Hugo, cifras del Hito 1): el pelotón de B contra los 100 del caserío, N semillas (TBD_K_N, 200), con munición sin límite
        /// (TBD_K_MUNI vacío) o con TBD_K_MUNI disparos por soldado. TBD_K_HIT = lista de ‰ de puntería (UnitsWorld.HitScalePm) a probar, «1000» por defecto.
        /// k = disparos (cartuchos) gastados entre abatidos. Distancia media del abatido a su pelotón y minuto de cada baja.</summary>
        /// <summary>TBD_REPLAY=grabación (de -grabar): reproduce las órdenes y cuenta cada baja con su contexto (munición, si se retira, orden en curso, zombis a 10 m).
        /// TBD_REPLAY_FIN = último tick (18300 por defecto). Imprime el hash final para compararlo con el log de Unity.</summary>
        public static void Replay()
        {
            if (TBD.Mundo.Mundo.Lector == null) { string dir = System.IO.Path.Combine(BalanceTests.DataDirPublic(), "..", "..", "StreamingAssets", "Hito1"); TBD.Mundo.Mundo.Lector = f => { var pth = System.IO.Path.Combine(dir, f); return System.IO.File.Exists(pth) ? System.IO.File.ReadAllText(pth) : null; }; }
            var ords = TBD.Mundo.Mundo.OrdenesDe(System.IO.File.ReadAllText(Environment.GetEnvironmentVariable("TBD_REPLAY")), out uint sem, out string esc);
            int fin = int.TryParse(Environment.GetEnvironmentVariable("TBD_REPLAY_FIN"), out var ff) ? ff : 18300;
            var m = TBD.Mundo.Mundo.Caserio(sem); foreach (var o in ords) m.Encolar(o); m.Log = l => Console.WriteLine("  " + l);
            int bajas = 0, abat = 0; string ultOrden = "-";
            var b = m.Puntos["B"];
            while (m.Tick <= fin)
            {
                foreach (var o in ords) if (o.Tick == m.Tick && (int)o.Tipo < 100) { ultOrden = o.Tipo + "@" + o.Tick; Console.WriteLine($"t {m.Tick} ({m.Tick / 30} s) orden {o}"); }
                if (m.Tick == fin) break;
                m.Step();
                var p = m.Unidades.Platoons[0];
                if (m.Abatidos / 10 != abat / 10) Console.WriteLine($"t {m.Tick} ({m.Tick / 30} s) abatidos {m.Abatidos}, munición {p.AmmoShots}/{p.AmmoCap}");
                abat = m.Abatidos;
                if (m.BajasPropias != bajas)
                {
                    int cerca = 0, persig = 0; foreach (var z in m.Zombis.Lista) { if (!z.Vivo) continue; long dx = z.X - p.AX, dy = z.Y - p.AY; if (dx * dx + dy * dy < 100000L * 100000L) cerca++; if (z.Estado == EstadoZombi.Persigue) persig++; }
                    long bx = p.AX - b.X, by = p.AY - b.Y;
                    Console.WriteLine($"t {m.Tick} ({m.Tick / 30} s) BAJA {m.BajasPropias}: munición {p.AmmoShots}/{p.AmmoCap}, vivos {p.AliveCount()}, se retira {m.SeRetira(p.Id)}, moviendo {p.Moving} atacando {p.AttackMove} altoelfuego {p.HoldFire}, orden {ultOrden}, a {Math.Sqrt(bx * bx + by * by) / 1000:0} m de B, zombis a 100 m {cerca}, persiguiendo {persig}, abatidos {m.Abatidos}");
                    bajas = m.BajasPropias;
                }
            }
            Console.WriteLine($"fin: tick {m.Tick}, abatidos {m.Abatidos}, bajas {m.BajasPropias}, hash {m.Hash():x16}");
        }
        public static void KReport()
        {
            if (TBD.Mundo.Mundo.Lector == null) { string dir = System.IO.Path.Combine(BalanceTests.DataDirPublic(), "..", "..", "StreamingAssets", "Hito1"); TBD.Mundo.Mundo.Lector = f => { var pth = System.IO.Path.Combine(dir, f); return System.IO.File.Exists(pth) ? System.IO.File.ReadAllText(pth) : null; }; }
            int n = int.TryParse(Environment.GetEnvironmentVariable("TBD_K_N"), out var nn) ? nn : 200; int maxT = 30 * 60 * (int.TryParse(Environment.GetEnvironmentVariable("TBD_K_MIN"), out var mn) ? mn : 15);
            int muni = int.TryParse(Environment.GetEnvironmentVariable("TBD_K_MUNI"), out var mu) ? mu : -1;
            bool detalle = Environment.GetEnvironmentVariable("TBD_K_DET") == "1", linea = Environment.GetEnvironmentVariable("TBD_K_LINEA") == "1";
            int cajasB = int.TryParse(Environment.GetEnvironmentVariable("TBD_K_CAJAS"), out var cj) ? cj : 1;
            int cargCaja = int.TryParse(Environment.GetEnvironmentVariable("TBD_K_CARG_CAJA"), out var cc) ? cc : 3;      // Hugo 23:23: la caja es de 3 cargadores
            foreach (var hs in (Environment.GetEnvironmentVariable("TBD_K_HIT") ?? "def").Split(','))
            {
                int hit = hs == "def" ? TBD.Mundo.Mundo.PunteriaHito1Pm : int.Parse(hs);
                var ks = new List<double>(); var bajas = new List<int>(); var tiempos = new List<int>(); var dist = new List<int>(); var minBaja = new int[16];
                int limpios = 0, aniquilados = 0, retiradas = 0; long disT = 0, abT = 0, vivosT = 0;
                for (int s = 1; s <= n; s++)
                {
                    var m = TBD.Mundo.Mundo.Caserio((uint)s); var p = m.Unidades.Platoons[0]; m.Unidades.HitScalePm = hit;
                    int eB = m.Red.EstacionPorNombre("B");
                    if (muni >= 0) { p.AmmoCap = p.AmmoShots = muni * p.Men.Count; m.Red.Sacar(eB, Red.Municion, 1000); m.Red.Meter(eB, Red.Municion, cajasB); }
                    m.Encolar(new Orden { Tick = 0, Jugador = 1, Tipo = TipoOrden.AtacarMoviendo, A = p.Id, B = TBD.Mundo.Mundo.CaserioX, C = TBD.Mundo.Mundo.CaserioY, D = -1 });
                    int disparos = 0; bool seRetiro = false, retirado = false;
                    while (m.Tick < maxT && m.Zombis.Vivos > 0 && p.AliveCount() > 0)
                    {
                        if (muni < 0) p.AmmoShots = p.AmmoCap;
                        m.Step(); foreach (var ev in m.UltimoFuego.Shots) disparos += WeaponCatalog.Table[(int)ev.Weapon].CostShots;
                        if (m.SeRetira(p.Id)) { seRetiro = true; retirado = true; }
                        if (muni >= 0 && linea && m.Tick == 4 * 1800) m.Red.Meter(eB, Red.Municion, 4);      // el tren de la línea deja 4 cajas en B a los 4 min
                        if (!retirado && !m.SeRetira(p.Id) && !p.Moving && m.Tick % 900 == 0 && m.Zombis.Vivos > 0)      // como haría el jugador: parado y sin nadie a tiro, va a por el zombi vivo más cercano
                        {
                            int bz = -1; long bd = long.MaxValue; for (int zi = 0; zi < m.Zombis.Lista.Count; zi++) { var zz = m.Zombis.Lista[zi]; if (!zz.Vivo) continue; long dx = zz.X - p.AX, dy = zz.Y - p.AY, d2 = dx * dx + dy * dy; if (d2 < bd) { bd = d2; bz = zi; } }
                            if (bz >= 0 && bd > 40000L * 40000) m.Encolar(new Orden { Tick = m.Tick, Jugador = 1, Tipo = TipoOrden.AtacarMoviendo, A = p.Id, B = m.Zombis.Lista[bz].X, C = m.Zombis.Lista[bz].Y, D = -1 });
                        }
                        if (retirado && !m.SeRetira(p.Id) && (p.AmmoShots * 10 >= p.AmmoCap * 9 || (m.Red.Existencias(eB, Red.Municion) == 0 && m.SueltosEnB == 0 && p.AmmoShots * 2 >= p.AmmoCap))) { retirado = false; m.Encolar(new Orden { Tick = m.Tick, Jugador = 1, Tipo = TipoOrden.AtacarMoviendo, A = p.Id, B = TBD.Mundo.Mundo.CaserioX, C = TBD.Mundo.Mundo.CaserioY, D = -1 }); }      // recargado: vuelve al caserío
                        if (Environment.GetEnvironmentVariable("TBD_K_Z") is string zs && zs.Length > 0 && m.Tick > 20000 && m.Tick % 15 == 0 && m.Tick < 21000) { int zi = int.Parse(zs); var zz = m.Zombis.Lista[zi]; Console.WriteLine($"  t {m.Tick} z{zi} ({zz.X};{zz.Y}) {zz.Estado} meta ({zz.MetaX};{zz.MetaY}) {m.Zombis.Traza(zi)} and {zz.Andando}"); }
                    }
                    bool limpio = m.Zombis.Vivos == 0; if (limpio) limpios++; if (p.AliveCount() == 0) aniquilados++; if (seRetiro) retiradas++;
                    if (m.Abatidos > 0) ks.Add((double)disparos / m.Abatidos);
                    bajas.Add(m.BajasPropias); tiempos.Add(m.Tick); disT += disparos; abT += m.Abatidos; vivosT += m.Zombis.Vivos; dist.AddRange(m.DistAbatidoMm);
                    foreach (int tb in m.TickBaja) minBaja[Math.Min(15, tb / 1800)]++;
                    if (Environment.GetEnvironmentVariable("TBD_K_DBG") == "1") { Console.WriteLine($"  pelotón ({p.AX};{p.AY}) mov {p.Moving} atk {p.AttackMove} T ({p.TX};{p.TY}) vivos {p.AliveCount()} muni {p.AmmoShots}"); foreach (var z in m.Zombis.Lista) if (z.Vivo) Console.WriteLine($"   z{z.Id} ({z.X};{z.Y}) {z.Estado} meta ({z.MetaX};{z.MetaY}) d {IMath.Dist(z.X - p.AX, z.Y - p.AY) / 1000} m camino {m.Rejilla.BuscarCamino(z.X, z.Y, z.MetaX, z.MetaY, new List<PointMm>(), 200000)} libre {m.Rejilla.Libre(z.X, z.Y)} exp {m.Rejilla.UltimosExpandidos}"); }
                    if (detalle) Console.WriteLine($"{hit};{s};{m.Abatidos};{disparos};{(m.Abatidos > 0 ? (double)disparos / m.Abatidos : 0):0.00};{m.BajasPropias};{(limpio ? 1 : 0)};{(seRetiro ? 1 : 0)};{m.Tick}");
                }
                ks.Sort(); bajas.Sort(); tiempos.Sort(); dist.Sort();
                Func<List<double>, double, double> pd = (l, q) => l.Count == 0 ? double.NaN : l[Math.Min(l.Count - 1, (int)(q * l.Count))];
                Func<List<int>, double, int> pi = (l, q) => l.Count == 0 ? 0 : l[Math.Min(l.Count - 1, (int)(q * l.Count))];
                long dsum = 0; foreach (int d in dist) dsum += d;
                var sbm = new System.Text.StringBuilder(); for (int k = 0; k < 16; k++) if (minBaja[k] > 0) sbm.Append($" min{k}:{minBaja[k]}");
                Console.WriteLine($"# puntería {hit} ‰ · munición {(muni < 0 ? "sin límite" : muni + " por soldado, " + cajasB + " caja(s) de " + TBD.Mundo.Mundo.CartuchosPorCaja + " en B" + (linea ? ", línea con 4 cajas a los 4 min" : ", sin línea"))} · {n} semillas · tope {maxT / 1800} min");
                Console.WriteLine($"#   k (disparos por abatido): p10 {pd(ks, 0.1):0.0}, p50 {pd(ks, 0.5):0.0}, p90 {pd(ks, 0.9):0.0}; global {(double)disT / Math.Max(1, abT):0.0}");
                Console.WriteLine($"#   abatido a {dsum / Math.Max(1, dist.Count) / 1000.0:0.0} m de media (p10 {pi(dist, 0.1) / 1000.0:0}, p50 {pi(dist, 0.5) / 1000.0:0}, p90 {pi(dist, 0.9) / 1000.0:0} m)");
                Console.WriteLine($"#   bajas p10 {pi(bajas, 0.1)}, p50 {pi(bajas, 0.5)}, p90 {pi(bajas, 0.9)}; por minuto:{(sbm.Length > 0 ? sbm.ToString() : " ninguna")}");
                Console.WriteLine($"#   caserío limpio {limpios}/{n}; quedan vivos de media {(double)vivosT / n:0.0}; se retira {retiradas}/{n}; aniquilado {aniquilados}/{n}; tiempo p50 {pi(tiempos, 0.5) / 30} s");
            }
        }
    }
}
