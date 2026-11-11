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
            foreach (var z in m.Zombis.Lista) if (z.Vivo) foreach (var c in m.Casas) if (z.X > c.R.X0 && z.X < c.R.X1 && z.Y > c.R.Y0 && z.Y < c.R.Y1) return false;
            return true;
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[50] Núcleo v2: Mundo, rejilla por trozos y zombis del caserío");

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
            check(m.Zombis.Lista.Count == 100 && m.Unidades.Platoons.Count == 1, $"caserío: 100 zombis y un pelotón ({m.Unidades.Platoons[0].Men.Count} soldados)");
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
                m2.Step(); if (!NadieEnCasa(m2)) fuera2 = false;
                int pe = 0; foreach (var z in m2.Zombis.Lista) if (z.Vivo && (z.Estado == EstadoZombi.Persigue || z.Estado == EstadoZombi.Ruido)) pe++; if (pe > maxPersiguen) maxPersiguen = pe;
                if (m2.Zombis.Vivos == 0) tickLimpio = m2.Tick;
            }
            var pl = m2.Unidades.Platoons[0];
            Console.WriteLine($"       caserío con el pelotón: abatidos {m2.Abatidos}, vivos {m2.Zombis.Vivos}, bajas {m2.BajasPropias}, munición gastada {pl.AmmoSpent} disparos, limpio en el tick {tickLimpio}, máx. persiguiendo {maxPersiguen}");
            check(maxPersiguen >= 10, $"los zombis reaccionan al pelotón (máximo a la vez persiguiendo u oyendo: {maxPersiguen})");
            check(fuera2, "con combate: ningún zombi dentro de una casa");
            check(m2.Abatidos > 50, $"el pelotón abate a la mayoría ({m2.Abatidos} de 100 en 10 min)");

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
    }
}
