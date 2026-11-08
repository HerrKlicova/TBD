using System;
using System.Collections.Generic;
using TBD.Net;
using TBD.Tutorial;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>Motor del tutorial (D-N154, D-N163), intro variante C y aviso del C90. Lógica pura: señales, tiempos, salto, dificultad y memoria.</summary>
    static class TutorialTests
    {
        static void Run1(TutorialMotor m, float s) { for (int i = 0; i < (int)(s * 10) + 1; i++) m.Avanzar(0.1f); }

        /// <summary>Recorre el tutorial entero mandando, en cada paso, la señal que lo cierra (o dejando correr el reloj). Lo mismo que hará la -nightbot.</summary>
        public static int Recorrer(TutorialMotor m, Action<Paso> enCada = null)
        {
            int pasos = 0;
            for (int guard = 0; guard < 400 && m.Estado == EstadoTutorial.Activo; guard++)
            {
                var p = m.Actual;
                if (!m.Visible) { m.Senal(p.Espera); continue; }
                enCada?.Invoke(p); pasos++;
                if (p.Cierre == Cierre.Segundos) Run1(m, p.Segundos);
                else foreach (var s in p.Senales) { m.Senal(s); if (m.Actual != p) break; }
            }
            return pasos;
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[30] Tutorial (motor, capítulos 0 a 3, C90) e intro C");

            // --- textos y cabeceras
            var caps = TutorialGuion.Todos();
            check(caps.Length == 4 && caps[0].Pasos.Length == 1 && caps[1].Pasos.Length == 10 && caps[2].Pasos.Length == 7 && caps[3].Pasos.Length == 8, "capítulos 0 a 3 con 1, 10, 7 y 8 pasos, como en tutorial-textos.md");
            bool sinCifra = true; foreach (var p in caps[3].Pasos) foreach (char c in p.Texto) if (char.IsDigit(c) && p.Id != "3.3") sinCifra = false;   // «3 estaciones» es la única cifra
            check(sinCifra, "el capítulo 3 no escribe ninguna cifra de ETA en el texto");
            var ids = new HashSet<string>(); bool unicos = true; foreach (var c in caps) foreach (var p in c.Pasos) if (!ids.Add(p.Id)) unicos = false;
            check(unicos, "los ids de los pasos no se repiten");

            // --- dificultad y memoria
            var mem = new MemoriaEnRam();
            check(!TutorialGuion.Nuevo(mem).Arrancar(1000), "en difícil no hay tutorial");
            var m = TutorialGuion.Nuevo(mem);
            check(m.Arrancar(850) && m.Cabecera == "Capítulo 0 · Paso 1 de 1" && m.PidePausa, "en normal arranca en el capítulo 0, con el juego en pausa");
            check(Array.IndexOf(m.Actual.Botones, "Empezar") >= 0 && Array.IndexOf(m.Actual.Botones, TutorialMotor.BotonSaltar) >= 0, "la primera tarjeta tiene [Empezar] y [Saltar tutorial]");
            m.Senal(TutorialGuion.Senales.Empezar);
            check(m.Cap.Numero == 1 && m.Actual.Id == "1.1" && !m.PidePausa && m.PideHordaEnEspera, "Empezar pasa al 1.1, quita la pausa y la horda espera (capítulo con combate)");
            check(m.Cabecera == "Capítulo 1 · Paso 1 de 10", "cabecera «Capítulo 1 · Paso 1 de 10»");

            // 1.1 necesita las dos cosas (cámara y rueda)
            m.Senal(TutorialGuion.Senales.RuedaUsada);
            check(m.Actual.Id == "1.1", "1.1 no se cierra solo con la rueda");
            m.Senal(TutorialGuion.Senales.CamaraMovida);
            check(m.Actual.Id == "1.2", "1.1 se cierra con cámara movida y rueda");
            // 1.3 añade la línea de los vehículos si hay vehículos fuera
            m.Senal(TutorialGuion.Senales.VehiculosFuera); Run1(m, 4);
            check(m.Actual.Id == "1.3" && m.Texto.EndsWith("Selecciónalos igual que a la tropa."), "1.3 añade «Los vehículos también disparan…» con vehículos fuera");
            // desvío: repetir sin castigo
            Run1(m, 4); int r0 = m.Repeticiones;
            for (int i = 0; i < 260; i++) m.Avanzar(0.1f);
            check(m.Actual.Id == "1.4" && m.Repeticiones > r0, "sin avanzar 25 s, la tarjeta se repite y no pasa nada más");
            m.Senal("desvio"); check(m.Actual.Id == "1.4", "la señal de desvío repite la tarjeta y no la cierra");
            // una señal de otro paso no cierra el actual
            m.Senal(TutorialGuion.Senales.TrenEnviado); check(m.Actual.Id == "1.4", "una señal que no toca no cierra la tarjeta");

            // --- saltar: queda recordado, no vuelve a salir
            m.Saltar();
            check(m.Estado == EstadoTutorial.Saltado && m.Actual == null && !m.PideHordaEnEspera, "saltar quita la tarjeta y suelta la horda");
            check(!TutorialGuion.Nuevo(mem).Arrancar(850), "saltado una vez, no vuelve a salir");
            check(TutorialGuion.Nuevo(mem).Arrancar(850, forzar: true), "con forzar (-tutorial) sale otra vez");

            // saltar desde la primera tarjeta, con el botón
            var mem2 = new MemoriaEnRam(); var m2 = TutorialGuion.Nuevo(mem2); m2.Arrancar(725);
            m2.Senal("boton:" + TutorialMotor.BotonSaltar);
            check(m2.Estado == EstadoTutorial.Saltado && mem2.Leer(TutorialMotor.ClaveVisto), "el botón «Saltar tutorial» de la primera tarjeta lo salta y lo recuerda (fácil)");

            // --- recorrido completo
            var mem3 = new MemoriaEnRam(); var m3 = TutorialGuion.Nuevo(mem3); m3.Arrancar(850);
            var vistos = new List<string>();
            int n = Recorrer(m3, p => vistos.Add(p.Id));
            check(m3.Estado == EstadoTutorial.Terminado && n == 26 && vistos[25] == "3.8", $"el recorrido completo pasa por los 26 pasos y acaba en 3.8 ({n})");
            check(mem3.Leer(TutorialMotor.ClaveVisto), "al acabar queda como visto");

            // --- capítulo 1 entero (revisión de Nacho): la horda espera hasta el 1.8 y luego se suelta; el 1.9 tiene red de seguridad de 90 s
            {
                var mh = TutorialGuion.Nuevo(new MemoriaEnRam()); mh.Arrancar(850); mh.Senal(TutorialGuion.Senales.Empezar);
                bool esperaAntes = true;
                while (mh.Actual.Id != "1.8") { esperaAntes &= mh.PideHordaEnEspera; var p = mh.Actual; if (p.Cierre == Cierre.Segundos) Run1(mh, p.Segundos); else foreach (var s in p.Senales) mh.Senal(s); }
                check(esperaAntes && !mh.PideHordaEnEspera, "la horda espera del 1.1 al 1.7 y se suelta en el 1.8");
                mh.Senal(TutorialGuion.Senales.HordaAvanza);
                check(mh.Actual.Id == "1.9" && !mh.PideHordaEnEspera, "1.8 se cierra cuando la horda avanza; en el 1.9 la horda ya no espera");
                Run1(mh, 89f); check(mh.Actual.Id == "1.9", "el 1.9 sigue mientras dura el ataque");
                Run1(mh, 1.5f); check(mh.Actual.Id == "1.10", "si nunca llega «ataque rechazado», el 1.9 se cierra a los 90 s");
                Run1(mh, 4.2f); check(mh.Cap.Numero == 2, "y el capítulo 1 acaba y pasa al 2");
                var m18 = TutorialGuion.Nuevo(new MemoriaEnRam()); m18.Arrancar(850); m18.Senal(TutorialGuion.Senales.Empezar);
                while (m18.Actual.Id != "1.8") { var p = m18.Actual; if (p.Cierre == Cierre.Segundos) Run1(m18, p.Segundos); else foreach (var s in p.Senales) m18.Senal(s); }
                Run1(m18, 3.2f); check(m18.Actual.Id == "1.9", "sin señal, el 1.8 se cierra a los 3 s");
            }

            // --- estado que ya se cumple (revisión de Nacho): mapa ya abierto al salir la 3.2
            {
                var ms = TutorialGuion.Nuevo(new MemoriaEnRam()); ms.Arrancar(850); bool mapa = false;
                ms.EstadoActual = x => x == TutorialGuion.Senales.MapaAbierto && mapa;
                while (ms.Actual.Id != "3.2") { var p = ms.Actual; if (!ms.Visible) { ms.Senal(p.Espera); continue; } if (p.Cierre == Cierre.Segundos) Run1(ms, p.Segundos); else foreach (var s in p.Senales) ms.Senal(s); }
                ms.Avanzar(0.1f); check(ms.Actual.Id == "3.2", "3.2 espera mientras el mapa no está abierto");
                mapa = true; ms.Avanzar(0.1f); check(ms.Actual.Id == "3.3", "con el mapa ya abierto, la 3.2 se cierra sola aunque la señal llegara antes");
            }

            // --- capítulo 3 (D-N172 revisada): salta con la pila > 150 en la primera noche, la horda no espera, el tren llega a las puertas
            {
                Action<TutorialMotor, string> Hasta = (mm, id) => { while (mm.Actual != null && mm.Actual.Id != id) { var p = mm.Actual; if (!mm.Visible) { mm.Senal(p.Espera); continue; } if (p.Cierre == Cierre.Segundos) Run1(mm, p.Segundos); else foreach (var x in p.Senales) { mm.Senal(x); if (mm.Actual != p) break; } } };
                var m4 = TutorialGuion.Nuevo(new MemoriaEnRam()); m4.Arrancar(850); bool pila = false;
                m4.EstadoActual = x => (x == TutorialGuion.Senales.Cap3Dispara || x == TutorialGuion.Senales.Pila150) && pila;
                while (m4.Cap.Numero < 3) { var p = m4.Actual; if (!m4.Visible) { m4.Senal(p.Espera); continue; } if (p.Cierre == Cierre.Segundos) Run1(m4, p.Segundos); else foreach (var x in p.Senales) { m4.Senal(x); if (m4.Actual != p) break; } }
                Run1(m4, 30f);
                check(m4.Actual.Id == "3.1" && !m4.Visible && !m4.PideHordaEnEspera, "al acabar el capítulo 2, el 3 espera a la pila (y la horda no espera)");
                pila = true; m4.Avanzar(0.1f);
                check(m4.Visible && m4.Texto.StartsWith("¡Están trepando el muro!"), "con la pila por encima de 150 sale el 3.1 (aunque pasara antes: es un estado)");
                Hasta(m4, "3.6");
                m4.Relleno = t => t.Replace("{eta}", "0:09");
                check(m4.Texto.EndsWith("La ayuda llega en 0:09."), "3.6 escribe el ETA real");
                m4.Senal(TutorialGuion.Senales.VueltaZona);
                check(m4.Actual.Id == "3.7" && !m4.Visible, "3.7 espera al tren");
                m4.Senal(TutorialGuion.Senales.TrenEnSagunto);
                check(m4.Visible && m4.Texto == "Sagunto está sitiada: el tren espera en la puerta este. Baja la tropa.", "con el tren a las puertas sale el 3.7 (D-N175)");
                m4.Senal(TutorialGuion.Senales.TropaEnTierra); check(m4.Actual.Id == "3.8", "tropa en tierra: 3.8");
                m4.Senal(TutorialGuion.Senales.OrdenAtacar); check(m4.Estado == EstadoTutorial.Terminado, "3.8 acaba el tutorial con la orden de atacar (o de mover)");
                // si salta por los 60 s (sin tromba), sale el 3.1b
                var mt = TutorialGuion.Nuevo(new MemoriaEnRam()); mt.Arrancar(725); bool tiempo = false;
                mt.EstadoActual = x => (x == TutorialGuion.Senales.Cap3Dispara || x == TutorialGuion.Senales.Cap3PorTiempo) && tiempo;
                while (mt.Cap.Numero < 3) { var p = mt.Actual; if (!mt.Visible) { mt.Senal(p.Espera); continue; } if (p.Cierre == Cierre.Segundos) Run1(mt, p.Segundos); else foreach (var x in p.Senales) { mt.Senal(x); if (mt.Actual != p) break; } }
                check(!mt.Visible, "sin pila ni tiempo, el capítulo 3 espera");
                tiempo = true; mt.Avanzar(0.1f);
                check(mt.Visible && mt.Actual.Id == "3.1b" && mt.Texto.StartsWith("La horda está en el muro"), "a los 60 s del primer contacto sale el 3.1b");
                Run1(mt, 4.2f); check(mt.Actual.Id == "3.2", "y sigue con el 3.2");
                bool cifras = false; foreach (var p in caps[3].Pasos) foreach (char ch in p.Texto) if (char.IsDigit(ch)) cifras = true;
                check(!cifras, "el capítulo 3 no escribe cifras (el ETA lo pone el juego)");
            }

            // --- destacamento de Nules (D-N172) sobre la red: tren parado en Nules, embarque, viaje a Sagunto y descarga en casa
            {
                var w = NetScenario.Mvp(); w.DirectorEnabled = false;
                int sag = TrenPreparar.Nodo(w, "Sagunto"), nul = TrenPreparar.Nodo(w, "Nules");
                int n0 = w.Services.Count, sv = Destacamento.SembrarTren(w);
                check(sv == n0 && Destacamento.SembrarTren(w) == sv && w.Services[sv].Train.Name.EndsWith("Nules"), "se crea un tren parado en Nules (una vez)");
                check(TrenPreparar.TrenEn(w, nul) == sv, "TrenEn lo encuentra en Nules");
                const int Pel = 77, Veh = -1;     // sin Lince desde la revisión de la D-N172
                check(w.LoadBlockReason(sv, PasajeroTipo.Peloton, Pel, nul) != null, "un pelotón que no es del destacamento no sube en Nules (está en Sagunto)");
                Destacamento.Registrar(w, Pel, Veh); Destacamento.Registrar(w, Pel, Veh);
                check(w.Embarque.AtStation(nul, PasajeroTipo.Peloton).Count == 1 && w.Embarque.AtStation(nul, PasajeroTipo.Vehiculo).Count == 0, "el destacamento está en Nules (un pelotón, sin vehículo, sin duplicar)");
                check(w.LoadBlockReason(sv, PasajeroTipo.Peloton, Pel, nul) == null && w.LoadPassenger(sv, PasajeroTipo.Peloton, Pel, nul), "el pelotón sube al tren de Nules");
                var carga = new int[NetConst.ResCount]; carga[(int)Res.Materiales] = 20; w.Stock[nul, (int)Res.Materiales] = Math.Max(w.Stock[nul, (int)Res.Materiales], 20);
                check(w.SetSchedule(sv, TrenPreparar.Envio(w, sv, sag, carga)) == ScheduleResult.Ok, "Preparar tren desde Nules: horario a Sagunto válido");
                long eta = TrenPreparar.EtaDesdeParada(w, sv, sag);
                long t0 = w.Tick; bool salio = false; var s = w.Services[sv];
                while (w.Tick - t0 < NetConst.DayTicks / 2) { w.Step(); if (!s.AtStop) salio = true; if (salio && s.AtStop) break; }
                check(salio && s.AtStop && s.Stops[s.StopIndex].Node == sag && s.Cargo[(int)Res.Materiales] == 20, $"llega a Sagunto con los materiales ({TrenPreparar.HhMm(w.Tick - t0)}, ETA {TrenPreparar.HhMm(eta)})");
                check(w.SetSchedule(sv, TrenPreparar.Aparcar(w, sv)) == ScheduleResult.Ok, "se aparca en Sagunto");
                check(w.UnloadPassengers(sv, sag) == 1 && w.Embarque.Aboard(sv, PasajeroTipo.Peloton).Count == 0 && w.Embarque.Find(PasajeroTipo.Peloton, Pel) < 0, "en casa baja y sale de la lista (ya es tropa de Sagunto)");
            }

            // --- tarjeta suelta del C90: una vez, y solo con el tutorial en marcha
            var memc = new MemoriaEnRam(); var mc = TutorialGuion.Nuevo(memc); mc.Arrancar(850); mc.Senal(TutorialGuion.Senales.Empezar);
            mc.Senal(TutorialGuion.Senales.C90ATiro);
            check(mc.SueltaActual != null && mc.SueltaActual.Texto.StartsWith("Tus fusileros llevan C90") && mc.Actual.Id == "1.1", "la tarjeta del C90 sale aparte y no mueve el capítulo");
            mc.Senal(TutorialGuion.Senales.C90Lanzado); check(mc.SueltaActual == null, "se quita al lanzar el C90");
            mc.Senal(TutorialGuion.Senales.C90ATiro); check(mc.SueltaActual == null, "no vuelve a salir");
            var md = TutorialGuion.Nuevo(new MemoriaEnRam()); md.Arrancar(850); md.Senal(TutorialGuion.Senales.Empezar); md.Senal(TutorialGuion.Senales.C90ATiro); Run1(md, 10.5f);
            check(md.SueltaActual == null, "si no se lanza, se quita sola a los 10 s");
            var me = TutorialGuion.Nuevo(new MemoriaEnRam()); me.Arrancar(1000); me.Senal(TutorialGuion.Senales.C90ATiro);
            check(me.SueltaActual == null, "en difícil (sin tutorial) no sale");

            // --- aviso del C90 (regla de Lucía, 8-nov): 40 o más zombis dentro del recinto a menos de 30 m de un pelotón de fusileros con C90
            var uw = new UnitsWorld(5); var f = uw.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0);
            Func<int, int, bool> dentro = (x, y) => x < 25_000;                          // recinto de prueba: x < 25 m
            var zs = new List<Target>(); for (int i = 0; i < 40; i++) zs.Add(new Target { Id = i, X = 10_000 + (i % 8) * 1000, Y = (i / 8) * 1000 });
            check(f.C90Left > 0 && C90Aviso.Buscar(uw, zs, dentro) == f.Id, "40 zombis dentro del recinto a unos 15 m de los fusileros: avisa");
            check(C90Aviso.Buscar(uw, zs.GetRange(0, 39), dentro) == -1, "con 39 no avisa (el umbral es 40, el tope de bajas del C90)");
            var fuera = new List<Target>(); foreach (var z in zs) fuera.Add(new Target { Id = z.Id, X = z.X + 16_000, Y = z.Y });
            check(C90Aviso.Buscar(uw, fuera, dentro) == -1, "los mismos 40 fuera del recinto no cuentan");
            var lejos = new List<Target>(); foreach (var z in zs) lejos.Add(new Target { Id = z.Id, X = z.X - 60_000, Y = z.Y });
            check(C90Aviso.Buscar(uw, lejos, null) == -1, "a más de 30 m del pelotón no cuentan");
            f.C90Left = 0; check(C90Aviso.Buscar(uw, zs, dentro) == -1, "sin C90 que lanzar, no avisa");
            var ua = new UnitsWorld(5); ua.AddPlatoon(PlatoonKind.Ametralladores, 0, 0, 0);
            check(C90Aviso.Buscar(ua, zs, dentro) == -1, "un pelotón que no es de fusileros no avisa");

            // --- «Preparar tren»: Sagunto → Nules con carga, espera en Nules, descarga y vuelta (solo SetSchedule)
            {
                var w = NetScenario.Mvp(); w.DirectorEnabled = false;
                int sag = TrenPreparar.Nodo(w, "Sagunto"), nul = TrenPreparar.Nodo(w, "Nules");
                int sv = -1; for (int g = 0; g < 2 * NetConst.DayTicks && sv < 0; g++) { sv = TrenPreparar.TrenEn(w, sag); if (sv < 0) w.Step(); }
                check(sv >= 0, "hay un tren parado en Sagunto para preparar");
                long eta = TrenPreparar.EtaDesdeParada(w, sv, nul);
                check(eta > 0 && TrenPreparar.HhMm(eta).Contains(":"), $"ETA Sagunto–Nules: {TrenPreparar.HhMm(eta)}");
                var carga = new int[NetConst.ResCount]; carga[(int)Res.Municion] = 40; carga[(int)Res.Materiales] = 60;
                int mun0 = w.Stock[sag, (int)Res.Municion], nulMat0 = w.Stock[nul, (int)Res.Materiales];
                check(TrenPreparar.Envio(w, sv, sag, carga) == null, "no se puede enviar a la estación donde ya está");
                var r = w.SetSchedule(sv, TrenPreparar.Envio(w, sv, nul, carga));
                check(r == ScheduleResult.Ok, $"el envío es un horario válido ({r})");
                long t0 = w.Tick; bool salio = false;
                while (w.Tick - t0 < NetConst.DayTicks / 2) { w.Step(); if (!w.Services[sv].AtStop) salio = true; if (salio && w.Services[sv].AtStop) break; }
                var s = w.Services[sv];
                check(salio && s.AtStop && s.Stops[s.StopIndex].Node == nul, "sale enseguida y llega a Nules");
                check(s.Cargo[(int)Res.Municion] >= 40 && s.Cargo[(int)Res.Materiales] >= 60 && w.Stock[sag, (int)Res.Municion] <= mun0 - 40, "lleva la carga, que ha salido del almacén de Sagunto");
                long llegada = w.Tick - t0;
                check(Math.Abs(llegada - eta) <= eta / 10 + 30, $"la ETA cuadra con el viaje real ({TrenPreparar.HhMm(llegada)} frente a {TrenPreparar.HhMm(eta)})");
                check(w.SetSchedule(sv, TrenPreparar.Aparcar(w, sv)) == ScheduleResult.Ok, "al llegar se aparca en Nules (horario de una parada)");
                int sagMun1 = w.Stock[sag, (int)Res.Municion];
                for (long i = 0; i < 2L * NetConst.DayTicks; i++) w.Step();     // dos días: no se repite el viaje ni la carga (Nacho)
                check(s.AtStop && s.Stops[s.StopIndex].Node == nul, "dos días después sigue en Nules esperando a que lo descarguen");
                check(w.Stock[sag, (int)Res.Municion] >= sagMun1 && s.Cargo[(int)Res.Municion] >= 40 && s.Cargo[(int)Res.Municion] <= 40 + 0, "y no ha vuelto a cargar munición de Sagunto");
                check(TrenPreparar.TrenEn(w, nul) == sv, "TrenEn lo encuentra en Nules");
                r = w.SetSchedule(sv, TrenPreparar.Descarga(w, sv, sag));
                for (int i = 0; i < 5; i++) w.Step();
                check(r == ScheduleResult.Ok && s.CargoTotal() == 0 && w.Stock[nul, (int)Res.Materiales] >= nulMat0 + 60, "Descargar deja la carga en Nules");
                check(!s.AtStop && s.Stops[s.StopIndex].Node == sag, "y el tren vuelve a Sagunto");
                long t1 = w.Tick; while (!s.AtStop && w.Tick - t1 < NetConst.DayTicks) w.Step();
                check(s.AtStop && w.SetSchedule(sv, TrenPreparar.Aparcar(w, sv)) == ScheduleResult.Ok, "vuelve a Sagunto y se aparca");
                int sagMun2 = w.Stock[sag, (int)Res.Municion];
                for (long i = 0; i < 2L * NetConst.DayTicks; i++) w.Step();
                check(s.AtStop && s.Stops[s.StopIndex].Node == sag && s.CargoTotal() == 0 && w.Stock[sag, (int)Res.Municion] >= sagMun2, "aparcado en Sagunto no repite nada en dos días");
            }

            // --- red de estaciones: casos límite de TrenPreparar (envío, ETA en marcha, aparcar y descargar)
            {
                var w = NetScenario.Mvp(); w.DirectorEnabled = false;
                int sag = TrenPreparar.Nodo(w, "Sagunto"), nul = TrenPreparar.Nodo(w, "Nules"), cas = TrenPreparar.Nodo(w, "Castellón");
                int sv = -1; for (int g = 0; g < 2 * NetConst.DayTicks && sv < 0; g++) { sv = TrenPreparar.TrenEn(w, sag); if (sv < 0) w.Step(); }
                var raro = new int[NetConst.ResCount]; raro[0] = -50; raro[1] = 5000;
                var pe = TrenPreparar.Envio(w, sv, nul, raro);
                check(pe != null && pe[2] == 0 && pe[3] == NetWorld.MaxScheduleTransfer, "Envio recorta la carga: nada negativo y como mucho el tope de SetSchedule");
                check(TrenPreparar.Envio(w, sv, -1, null) == null && TrenPreparar.Envio(w, sv, w.Graph.Nodes.Count, null) == null && TrenPreparar.Envio(w, -1, nul, null) == null, "Envio rechaza destinos y trenes que no existen");
                check(TrenPreparar.Nodo(w, "Atlantis") == -1, "Nodo de un nombre que no existe: −1");
                check(TrenPreparar.HhMm(-1) == "—" && TrenPreparar.HhMm(0) == "0:00", "HhMm: «—» sin ETA y «0:00» al llegar");
                check(w.SetSchedule(sv, TrenPreparar.Envio(w, sv, cas, null)) == ScheduleResult.Ok, "a Castellón también hay horario (el panel lo bloquea mientras está infestada)");
                long eta0 = TrenPreparar.EtaDesdeParada(w, sv, cas), etaPrev = long.MaxValue; bool baja = true, enMarcha = false;
                for (int i = 0; i < 4000 && !(enMarcha && w.Services[sv].AtStop); i++)
                {
                    w.Step(); var s = w.Services[sv]; if (s.AtStop) continue; enMarcha = true;
                    long e = TrenPreparar.EtaEnMarcha(w, sv); if (e > etaPrev) baja = false; etaPrev = e;
                }
                check(enMarcha && baja && etaPrev <= eta0, "en marcha, el ETA solo baja y empieza por debajo del de la parada");
                check(TrenPreparar.EtaEnMarcha(w, sv) == -1, "parado: EtaEnMarcha da −1");
                var s2 = w.Services[sv];
                if (!s2.AtStop) { for (int i = 0; i < 2000 && !s2.AtStop; i++) w.Step(); }
                check(TrenPreparar.Aparcar(w, sv) != null && TrenPreparar.Descarga(w, sv, s2.Stops[s2.StopIndex].Node) == null, "Descarga hacia la misma estación donde está: no vale");
                check(TrenPreparar.Aparcar(w, -1) == null && TrenPreparar.Descarga(w, 99, sag) == null, "Aparcar y Descarga con trenes que no existen: null");
            }

            // --- aeródromo de Bétera (D-N173): datos de Álvaro, ETA real, salida, vuelta sola a rearmar, existencias propias; plantel por estación
            {
                string bj = BalanceSource.Read("estacion_betera.json");
                check(bj != null, "existe Data/estacion_betera.json (Álvaro, v0)");
                var ae = AerodromoEstado.FromJson(bj);
                check(ae.Aeronaves.Count == 2 && ae.Pelotones.Count == 2 && Math.Abs(ae.MapX - 48) < 0.01 && Math.Abs(ae.MapY - 75) < 0.01, "Bétera: 2 NH90 y 2 pelotones de embarque, en (48; 75) del mapa (red-inicio v1)");
                check(Math.Abs(ae.Km("Sagunto") - 18.1) < 0.01 && Math.Abs(ae.Km("Nules") - 37.4) < 0.01 && ae.Destinos["Sagunto"].RumboLlegada == 252, "distancias reales: 18,1 km a Sagunto (llega con rumbo 252°) y 37,4 km a Nules");
                float vS = AerodromoEstado.VueloS(18.1), vN = AerodromoEstado.VueloS(37.4);
                check(vS > 0 && vN > vS * 2 - 0.01f && vN < vS * 2.2f, $"el vuelo sale de la distancia real ({vS:0.0} s a Sagunto, {vN:0.0} s a Nules)");
                check(Math.Abs(ae.EtaIdaS("Sagunto") - (20f + vS)) < 0.01f, "ETA de ida = 20 s de preparar y despegar + vuelo");
                check(ae.Enviar(0, "Sagunto", 3, true) == null && ae.Aeronaves[0].Peloton == 0 && !ae.PelotonEnBase[0] && ae.CombustibleMilliU == 67_000, "sale con un pelotón a bordo y gasta 3 u de combustible");
                check(ae.Enviar(0, "Sagunto", 3, true) != null, "no puede salir otra vez si no está en la base");
                var evs = new List<AerodromoEstado.Suceso>(); float ta = 0;
                while (ae.Aeronaves[0].Estado != AeronaveEstado.EnZona && ta < 100) { evs.AddRange(ae.Avanzar(0.1f)); ta += 0.1f; }
                check(ae.Aeronaves[0].Estado == AeronaveEstado.EnZona && Math.Abs(ta - ae.EtaIdaS("Sagunto")) < 0.25f && evs.Exists(e => e.Nuevo == AeronaveEstado.EnZona && e.Destino == 3), $"entra en la zona de Sagunto a su ETA ({ta:0.0} s)");
                ae.Aeronaves[0].MunicionPm = 200; ae.PelotonBajado(0);
                check(ae.Volver(0) && ae.Aeronaves[0].Estado == AeronaveEstado.Volviendo, "al bajar del 30 % vuelve solo a Bétera");
                float eta = ae.Eta(0); check(Math.Abs(eta - (vS + 20f + 30f)) < 0.01f, "ETA de vuelta = vuelo + 20 s de tomar tierra + 30 s de rearme");
                ta = 0; while (ae.Aeronaves[0].Estado != AeronaveEstado.EnBase && ta < 200) { ae.Avanzar(0.1f); ta += 0.1f; }
                check(ae.Aeronaves[0].Estado == AeronaveEstado.EnBase && ae.Aeronaves[0].MunicionPm == 1000 && ae.MunicionMilliU == 50_000 - 1440, $"rearmado en la base: gasta 1,44 u de munición (80 % de 1,8 u) ({ta:0} s)");
                check(!ae.PelotonEnBase[0], "el pelotón que bajó en Sagunto ya no está en la base");
                ae.CombustibleMilliU = 2000; check(ae.PorQueNo(1, "Nules") == "No queda combustible", "sin combustible no despega (existencias propias, sin logística)");
                check(Plantel.De("Sagunto").NH90 == 0 && Plantel.De("betera").NH90 == 2 && Plantel.De("Castellón").Pelotones.Length == 0 && Plantel.De("Nules").Pelotones.Length == 0 && Plantel.De("Nules", true).Pelotones.Length == 1, "plantel por estación: Sagunto sin NH90, Bétera con 2, Nules solo con el tutorial");
                var ed = EstacionDato.FromJson(bj); check(ed.Tamano == EstacionTamano.Aerodromo, "EstacionDato lee Bétera como aeródromo");
            }

            // --- capítulo 4 (Bétera, textos aprobados por Lucía): su propia clave; saltar el principal deja marca
            {
                var mem4 = new MemoriaEnRam();
                var c4 = TutorialGuion.NuevoCap4(mem4);
                check(c4.Arrancar(850) && c4.Cabecera == "Capítulo 4 · Paso 1 de 8" && c4.Texto.StartsWith("Base aérea de Bétera") && !c4.PideHordaEnEspera, "capítulo 4: 8 pasos, empieza en «Base aérea de Bétera» y la horda no espera");
                bool malas = false; foreach (var p in TutorialGuion.Cap4().Pasos) if (p.Texto.Contains("helipuerto") || p.Texto.Contains("Segorbe") || p.Texto.Contains("*") || p.Texto.Contains("20 ×")) malas = true;
                check(!malas, "sin «helipuerto», «Segorbe», asteriscos ni la medida de 20 × 20 en los textos");
                int n4 = Recorrer(c4); check(c4.Estado == EstadoTutorial.Terminado && n4 == 8 && mem4.Leer(TutorialGuion.ClaveCap4) && !mem4.Leer(TutorialMotor.ClaveVisto), "se recorre entero y solo marca su clave (tutorial4.visto)");
                check(!TutorialGuion.NuevoCap4(mem4).Arrancar(850), "una sola vez");
                check(!TutorialGuion.NuevoCap4(new MemoriaEnRam()).Arrancar(1000), "en difícil nunca");
                var memS = new MemoriaEnRam(); var mp = TutorialGuion.Nuevo(memS); mp.Arrancar(850); mp.Saltar();
                check(memS.Leer(TutorialMotor.ClaveVisto + TutorialMotor.ClaveSaltado), "saltar el tutorial principal deja la marca «saltado» (la vista no saca el capítulo 4)");
                var memT = new MemoriaEnRam(); var mt2 = TutorialGuion.Nuevo(memT); mt2.Arrancar(850); Recorrer(mt2);
                check(!memT.Leer(TutorialMotor.ClaveVisto + TutorialMotor.ClaveSaltado), "acabarlo no deja la marca «saltado»");
            }

            // --- gestos del mapa (D-N170): doble clic y seguir un tren
            {
                var dc = new DobleClic();
                check(!dc.Clic("nodo:0", 1.0f, 100, 100) && dc.Clic("nodo:0", 1.3f, 104, 98), "dos clics en el mismo sitio en menos de 0,4 s: doble clic");
                check(!dc.Clic("nodo:0", 2.0f, 100, 100) && !dc.Clic("nodo:0", 2.5f, 100, 100), "con más de 0,4 s entre clics no hay doble clic");
                check(!dc.Clic("nodo:1", 3.0f, 100, 100) && !dc.Clic("nodo:2", 3.1f, 100, 100), "clics en objetivos distintos no hacen doble clic");
                check(!dc.Clic("tren:0", 4.0f, 100, 100) && !dc.Clic("tren:0", 4.1f, 140, 100), "si el ratón se ha movido más de 12 px, no cuenta");
                check(!dc.Clic("tren:0", 5.0f, 0, 0) && dc.Clic("tren:0", 5.1f, 0, 0) && !dc.Clic("tren:0", 5.2f, 0, 0), "un tercer clic no repite el doble clic");
                var sg = new SeguirTren();
                check(!sg.Activo && !sg.Paso(false, true, 10), "sin tren elegido no sigue nada");
                sg.Empezar(1);
                check(sg.Activo && sg.Paso(false, true, 10) && sg.Paso(false, true, 8), "sigue al tren mientras va de viaje y nadie toca la cámara");
                check(!sg.Paso(true, true, 8) && !sg.Activo && sg.PorQueSuelta.Contains("movió"), "al mover la cámara el jugador, se suelta");
                sg.Empezar(1); check(!sg.Paso(false, false, 0.5) && sg.PorQueSuelta.Contains("parado"), "si el tren para, se suelta");
                check(SeguirTren.Distancia(10) == SeguirTren.DistanciaKm && SeguirTren.Distancia(1.0) >= 2.5f, "de cerca (1,2 km), pero junto a una estación la cámara no baja de 2,5 km: no entra en su zona sin querer");
            }

            // --- salida visible del tren (D-N170; datos de Paula, clips de Nuria)
            {
                var st = new SalidaTren(TipoTren.Electrico); var son = new List<string>();
                st.Avanzar(0.1f, son);
                check(st.Fase == SalidaFase.AvisoPuertas && son.Count == 1 && son[0].StartsWith("sfx_tren_aviso_puertas_") && st.Puertas == 0f && st.Distancia == 0f, "empieza con el aviso de puertas (con variante _1 a _5), puertas abiertas y parado");
                son.Clear(); st.Avanzar(3.0f, son);
                check(st.Fase == SalidaFase.CierrePuertas && son.Exists(x => x.StartsWith("sfx_tren_cierre_puertas_")) && st.Puertas > 0f && st.Puertas < 1f && st.Distancia == 0f, "a los 3 s cierra las puertas (≥ 3 s de aviso, EN 14752) y sigue parado");
                son.Clear(); st.Avanzar(st.InicioArranque - st.T + 0.1f, son);
                check(son.Exists(x => x.StartsWith("sfx_tren_silbato_salida_")) && son.Exists(x => x.StartsWith("sfx_tren_arranque_electrico_")) && st.Fase == SalidaFase.Arranque && st.Puertas == 1f, "silbato de dos tonos y arranque, con las puertas cerradas");
                bool huecoA = true; for (uint sd = 1; sd < 60; sd++) { var x = new SalidaTren(TipoTren.Electrico, 60, sd); if (x.ComprobacionS < 2f || x.ComprobacionS > 5f) huecoA = false; }
                check(huecoA, "el arranque llega entre 2 y 5 s después del cierre (Nuria), según la semilla");
                var sp = new SalidaTren(TipoTren.Electrico, 60, TrenPreparar.SemillaSalida(1, 3, 2), puertas: 6, silbato: false); var ls = new List<SonidoSalida>(); sp.Avanzar(20f, ls);
                var cierres = ls.FindAll(x => x.Clip.StartsWith("sfx_tren_cierre_puertas_"));
                bool desf = true; for (int i = 0; i < 6; i++) if (sp.Desfase(i) < 0 || sp.Desfase(i) > 0.3f) desf = false;
                check(cierres.Count == 6 && cierres.TrueForAll(x => x.Puerta >= 0) && desf, "un cierre por puerta, con un desfase de 0 a 0,3 s");
                check(!ls.Exists(x => x.Clip.StartsWith("sfx_tren_silbato")) && ls.Exists(x => x.Cabeza && x.Clip.StartsWith("sfx_tren_arranque_electrico_")) && ls.Exists(x => x.Puerta == -1 && x.Clip.StartsWith("sfx_tren_aviso")), "silbato opcional; el arranque suena en la cabeza y el aviso en el andén");
                check(ls.Exists(x => x.Cabeza && x.Clip.StartsWith("sfx_tren_pitido_electrico_")) && sp.PitidoAntesS >= 0.5f && sp.PitidoAntesS <= 1f && sp.PitidoAntesS < sp.ComprobacionS, "pitido de salida en la cabeza, de 0,5 a 1 s antes del arranque y después del cierre (Nuria)");
                bool v = true; foreach (var x in son) { int k = x[x.Length - 1] - '0'; if (k < 1 || k > 5 || x[x.Length - 2] != '_') v = false; }
                check(v, "todas las variantes son _1 a _5");
                float t30 = SalidaTren.TiempoHasta(TipoTren.Electrico, 30), t60 = SalidaTren.TiempoHasta(TipoTren.Electrico, 60), t100 = SalidaTren.TiempoHasta(TipoTren.Electrico, 100);
                check(t30 >= 7.5f && t30 <= 10f && t60 >= 15f && t60 <= 21f && t100 >= 35f && t100 <= 47f, $"Cercanías: 30 km/h en {t30:0.0} s, 60 en {t60:0.0} s, 100 en {t100:0} s (Paula: ~9, ~18 y 37–45)");
                float d30 = SalidaTren.TiempoHasta(TipoTren.Diesel, 30), d60 = SalidaTren.TiempoHasta(TipoTren.Diesel, 60);
                check(d30 >= 20f && d30 <= 40f && d60 >= 60f && d60 <= 130f, $"mercancías diésel 333 con carga: 30 km/h en {d30:0} s, 60 en {d60:0} s (Paula: ~26 y ~90)");
                var lento = new SalidaTren(TipoTren.Electrico); lento.Avanzar(60f, (List<string>)null);
                check(Math.Abs(lento.Velocidad - 60f / 3.6f) < 0.01f, "no pasa de la velocidad de la red (60 km/h)");
                var a1 = new SalidaTren(TipoTren.Diesel, 60, 3); var a2 = new SalidaTren(TipoTren.Diesel, 60, 3); var l1 = new List<string>(); var l2 = new List<string>();
                a1.Avanzar(12f, l1); a2.Avanzar(12f, l2); check(string.Join(",", l1) == string.Join(",", l2) && l1.Exists(x => x.StartsWith("sfx_tren_arranque_diesel333_")), "misma semilla, mismos clips; el diésel usa sfx_tren_arranque_diesel333");
            }

            // --- objetivo de campaña «Lleva munición a Nules» (textos de Lucía): munición, escolta opcional, destino, viaje, descarga (5b si sitiada) y vuelta
            {
                var ov = new ObjetivoViaje();
                check(ov.Paso == PasoViaje.Inactivo && ov.Texto() == null && ObjetivoViaje.Titulo == "Lleva munición a Nules", "antes del amanecer no hay objetivo");
                check(ObjetivoViaje.HayObjetivo(725) && ObjetivoViaje.HayObjetivo(850) && !ObjetivoViaje.HayObjetivo(1000), "sale en fácil y normal (aunque se saltara el tutorial) y en difícil no");
                ov.Empezar(); check(ov.Paso == PasoViaje.Cargar && ov.Texto() == "Carga munición en el tren de Sagunto: abre Preparar tren.", "paso 1: cargar munición");
                ov.Enviado(1, "Sagunto", "Nules", 0); check(ov.Paso == PasoViaje.Cargar, "un tren a Nules sin munición no cuenta");
                ov.MunicionPuesta(); check(ov.Paso == PasoViaje.Escolta && ov.Texto().StartsWith("Sube tropa o un vehículo"), "con munición puesta: paso 2, escolta (opcional)");
                ov.DestinoNules(); check(ov.Paso == PasoViaje.Destino && ov.Texto() == "Destino: Nules. Mira el ETA y envíalo.", "poner destino sin escolta se salta el paso 2");
                var ov2 = new ObjetivoViaje(); ov2.Empezar(); ov2.MunicionPuesta(); ov2.EscoltaPuesta(); check(ov2.Paso == PasoViaje.Destino, "con escolta también pasa al destino");
                var ov3 = new ObjetivoViaje(); ov3.Empezar(); ov3.MunicionPuesta(); ov3.Enviado(4, "Sagunto", "Nules", 30); check(ov3.Paso == PasoViaje.EnCamino, "si sale sin escolta y sin pasar por el destino, el paso 2 se salta solo");
                ov.Enviado(1, "Sagunto", "Nules", 30); check(ov.Paso == PasoViaje.EnCamino && ov.Texto().Contains("{eta}"), "en camino, con el ETA");
                ov.Llegado(1, "Nules");
                check(ov.Paso == PasoViaje.Descargar && ov.Texto(false) == "Tren en Nules. Descarga la munición." && ov.Texto(true) == "Nules está sitiada: la carga se queda a bordo hasta que se despeje.", "llegada: descargar, o el 5b si Nules está sitiada");
                ov.Descargado(1, "Nules"); check(ov.Paso == PasoViaje.Volver && ov.Texto() == "Devuelve el tren a Sagunto.", "descargado: volver");
                ov.Llegado(1, "Sagunto"); check(ov.Paso == PasoViaje.Cumplido && ov.Texto() == "Cumplido. Nules tiene munición para aguantar.", "cumplido");
                // y en la red: CmdCargo baja la carga en Nules con el tren parado, que luego vuelve
                var w = NetScenario.Mvp(); w.DirectorEnabled = false;
                int sag = TrenPreparar.Nodo(w, "Sagunto"), nul = TrenPreparar.Nodo(w, "Nules");
                int sv = -1; for (int g = 0; g < 2 * NetConst.DayTicks && sv < 0; g++) { sv = TrenPreparar.TrenEn(w, sag); if (sv < 0) w.Step(); }
                var carga = new int[NetConst.ResCount]; carga[(int)Res.Municion] = 30;
                w.SetSchedule(sv, TrenPreparar.Envio(w, sv, nul, carga));
                var s = w.Services[sv]; bool salio = false; long t0 = w.Tick;
                while (w.Tick - t0 < NetConst.DayTicks / 2) { w.Step(); if (!s.AtStop) salio = true; if (salio && s.AtStop) break; }
                w.SetSchedule(sv, TrenPreparar.Aparcar(w, sv)); w.Step();
                int nulMun0 = w.Stock[nul, (int)Res.Municion];
                w.Push(NetWorld.CmdCargo(w.Tick, sv, nul, new[] { -s.Cargo[0], -s.Cargo[1], -s.Cargo[2], -s.Cargo[3] }));
                for (int i = 0; i < 3; i++) w.Step();
                check(s.CargoTotal() == 0 && w.Stock[nul, (int)Res.Municion] == nulMun0 + 30 && s.AtStop && s.Stops[s.StopIndex].Node == nul, "CmdCargo descarga en Nules y el tren se queda parado allí");
                check(w.SetSchedule(sv, TrenPreparar.Envio(w, sv, sag, null)) == ScheduleResult.Ok, "y desde Nules se le puede mandar de vuelta a Sagunto");
            }

            // --- Nules v0.1 (Álvaro): el tren para en el apartado del muelle y los puntos de bajada
            {
                string nj = BalanceSource.Read("estacion_nules.json");
                check(nj != null, "existe Data/estacion_nules.json (Nules v0.1)");
                var ed = EstacionDato.FromJson(nj); check(ed.Id == "nules" && ed.Tamano == EstacionTamano.Pequena && ed.Vias.Count == 3, "EstacionDato lee Nules: pequeña, con 3 vías (las dos generales y el apartado)");
                var ap = AndenPuntos.FromJson(nj);
                check(ap != null && ap.Via == "apartado" && ap.MorroX == 182000 && ap.MorroZ == 49000, "el tren para con el morro en (182000; 49000) mm, en el apartado");
                check(ap.TropaX == 130000 && ap.TropaZ == 44500 && ap.VehiculoX == 184000 && ap.VehiculoZ == 44400 && ap.SitiadaX == 50000 && ap.SitiadaZ == 44500, "bajadas: tropa y carga (130; 44,5), vehículo (184; 44,4), sitiada fuera (50; 44,5)");
                check(AndenPuntos.FromJson(BalanceSource.Read("estacion_betera.json")) == null, "Bétera no tiene tren: sin puntos de andén");
            }

            // --- escenario de inicio de la D-N156 (NetScenario.Inicio; red-inicio.md §5, escala-existencias.md de Hugo)
            {
                var w = NetScenario.Inicio(); w.DirectorEnabled = false;
                int Id(string n) { return TrenPreparar.Nodo(w, n); }
                int Sum(int node) { int t = 0; for (int r = 0; r < NetConst.ResCount; r++) t += w.Stock[node, r]; return t; }
                check(Sum(Id("Valencia")) == 0 && Sum(Id("Teruel")) == 0, "Valencia (caída) y Teruel (bloqueada) sin existencias");
                int sag = Id("Sagunto"), nul = Id("Nules");
                check(w.Stock[sag, 0] == 150 && w.Stock[sag, 1] == 140 && w.Stock[sag, 2] == 100 && w.Stock[sag, 3] == 80, "reserva de red de Sagunto en u: 150 / 140 / 100 / 80 personas (Hugo)");
                check(Sum(nul) == 0 && w.Stations.Founded[nul] && Array.IndexOf(w.Stations.P.Sites, "Nules") < 0, "Nules propia desde el principio, con todo a 0 (Lucía y Hugo), y ya no se funda");
                check(w.Services.Count == 2 && w.Services.TrueForAll(x => x.AtStop && x.Stops.Count == 1 && x.Stops[0].Node == sag), "los dos trenes esperan en Sagunto sin horario");
                for (long i = 0; i < 2L * NetConst.DayTicks; i++) w.Step();
                check(w.Services.TrueForAll(x => x.AtStop && x.Stops[x.StopIndex].Node == sag), "en dos días ningún tren sale solo (nada llega a Castellón infestada)");
                int sv = TrenPreparar.TrenEn(w, sag);
                var carga = new int[NetConst.ResCount]; carga[0] = 30;
                check(sv >= 0 && w.SetSchedule(sv, TrenPreparar.Envio(w, sv, nul, carga)) == ScheduleResult.Ok, "el jugador puede mandar munición a Nules (objetivo de campaña)");
            }

            // --- almacén de la zona (regla de Hugo): min(tope, reserva) al empezar la partida y cada ataque; reponer entre ataques; lo que sobra vuelve
            {
                var w = NetScenario.Inicio(); int sag = TrenPreparar.Nodo(w, "Sagunto"), nul = TrenPreparar.Nodo(w, "Nules");
                var az = new AlmacenZona(sag);
                int movido = az.EmpiezaPartida(w);
                check(az.Actual[0] == 47 && az.Actual[1] == 30 && az.Actual[2] == 40 && az.Actual[3] == 0 && w.Stock[sag, 0] == 103 && w.Stock[sag, 1] == 110 && w.Stock[sag, 2] == 60 && w.Stock[sag, 3] == 80 && movido == 117,
                    "al empezar: 47 / 30 / 40 al almacén de la zona, el resto en la reserva (las personas no se mueven)");
                az.EmpiezaAtaque(w); check(az.EnAtaque && az.Actual[0] == 47, "al empezar un ataque se llena hasta el tope (ya estaba lleno: no se mueve nada)");
                az.Gastar(0, 40); check(az.Actual[0] == 7 && az.Reponer(w, 0, 10) == 0, "en pleno ataque no se repone a mano");
                int dev = az.AcabaAtaque(w); check(!az.EnAtaque && az.Actual[0] == 0 && w.Stock[sag, 0] == 110 && dev == 7 + 30 + 40, "al acabar el ataque lo que sobra vuelve a la reserva");
                check(az.Reponer(w, 0, 100) == 47 && az.Actual[0] == 47 && w.Stock[sag, 0] == 63, "entre ataques se repone a mano hasta el tope");
                var an = new AlmacenZona(nul); an.EmpiezaAtaque(w);
                check(an.Actual[0] == 0, "Nules empieza con la reserva a 0: el almacén de su zona también (la guarnición lleva su dotación)");
                w.Stock[nul, 0] = 20; an.AcabaAtaque(w); an.EmpiezaAtaque(w); check(an.Actual[0] == 20 && w.Stock[nul, 0] == 0, "con 20 u traídas en tren, el siguiente ataque toma las 20 (menos que el tope)");
            }

            // --- zona mínima de Bétera: colocación de las piezas (betera.json v0.2, piezas de Teo r2) yy despegue yy toma del NH90
            {
                var nv = AerodromoNivel.FromJson(BalanceSource.Read("estacion_betera.json"));
                check(nv.ZonaX0 == 0 && nv.ZonaZ0 == -4 && nv.ZonaX1 == 256 && nv.ZonaZ1 == 124, "encuadre de reserva de 256 × 128 m (zona [0, −4, 256, 124])");
                var torretas = nv.Piezas.FindAll(c => c.Carpeta == "torreta_vigia");
                check(torretas.Count == 2 && torretas.Exists(c => c.X == 4 && c.Z == 121 && c.Yaw == 0) && torretas.Exists(c => c.X == 252 && c.Z == 121), "solo las 2 torretas del MVP, en (4; 121) y (252; 121) con yaw 0");
                check(nv.Piezas.FindAll(c => c.Carpeta == "hangar").Count == 2 && nv.Piezas.Exists(c => c.Carpeta == "polvorin") && nv.Piezas.Exists(c => c.Carpeta == "torre_control" && c.X == 224 && c.Z == 4), "2 hangares, polvorín y la torre de control en (224; 4)");
                var heli = nv.Piezas.Find(c => c.Carpeta == "helipuerto");
                check(heli.Variante == 4 && heli.X == 128 && heli.Z == 104 && heli.Malla == "SM_Helipuerto", "el helipuerto v4 es la losa P1 (128; 104)");
                check(AerodromoNivel.Malla("torre_control") == "SM_TorreControl" && AerodromoNivel.Malla("valla_base") == "SM_VallaBase" && AerodromoNivel.Malla("rearme_set") == "SM_RearmeSet", "nombres de malla de Teo: SM_<Carpeta en PascalCase>");
                var valla = nv.Piezas.FindAll(c => c.Carpeta == "valla_base");
                check(valla.Count >= 55 && valla.TrueForAll(c => c.Variante >= 0 && c.Variante < 5), $"valla de la base en tramos de 10 m ({valla.Count} tramos, 5 variantes)");
                check(nv.Piezas.Exists(c => c.Carpeta == "polvorin" && c.Faldon) && !nv.Piezas.Exists(c => c.Carpeta == "hangar" && c.Faldon), "el polvorín va de faldón y los hangares dentro de la zona");
                check(nv.Claros.Count == 6 && nv.Claros["P1"][2] == 30 && nv.Claros["P2"][2] == 25 && nv.Claros["C1"][0] == 100 && nv.Claros["C1"][1] == 160, "claros P1–P3 y C1–C3: P1 losa de 30 m, el resto de 25 m, C1 en (100; 160)");
                check(nv.Pista[0] == -37 && nv.Pista[2] == 238 && nv.CuartelX == 36 && nv.CuartelZ == -44, "pista de 275 m y la guarnición en su sitio (36; −44)");
                bool variaHangar = false; var h = nv.Piezas.FindAll(c => c.Carpeta == "valla_base"); for (int i = 1; i < h.Count; i++) if (h[i].Variante != h[0].Variante) variaHangar = true;
                check(variaHangar, "las variantes cambian de un tramo a otro (semilla por posición)");
                // despegue yy toma
                VueloLocal.Despegue(128, 104, 252, 5f, out float xx, out float zz, out float yy);
                check(xx == 128 && zz == 104 && yy == 0, "despegue: los primeros 12 s, rotor en el suelo");
                VueloLocal.Despegue(128, 104, 252, 16f, out xx, out zz, out yy); check(yy > 0 && yy < VueloLocal.AlturaM && xx == 128, "de 12 a 20 s sube en vertical");
                VueloLocal.Despegue(128, 104, 72, 30f, out xx, out zz, out yy); check(yy == VueloLocal.AlturaM && xx > 128 && zz > 104, "después sale con el rumbo del destino (72°: este-noreste, hacia Sagunto)");
                VueloLocal.Toma(128, 104, 252, 0f, out xx, out zz, out yy); check(xx == 128 && zz == 104 && yy == 0, "toma: acaba parado en la losa");
                VueloLocal.Toma(128, 104, 252, 40f, out xx, out zz, out yy); check(yy == VueloLocal.AlturaM && xx > 128, "antes de tomar viene en vuelo desde el lado del rumbo de llegada (252°: llega desde el este-noreste)");
            }

            // --- sonidos del NH90 y de Bétera (plan de Nuria): qué clip en cada fase, variantes, fundido día/noche y M3M
            {
                string a0 = SonidoNH90.EnDespegue(-0.1f, 0.05f, 7), a1 = SonidoNH90.EnDespegue(11.9f, 12.1f, 7), a2 = SonidoNH90.EnDespegue(5f, 5.1f, 7);
                check(a0 != null && a0.StartsWith("sfx_nh90_arranque_") && a1 != null && a1.StartsWith("sfx_nh90_despegue_") && a2 == null, "despegue: arranque al dar la orden y despegue al empezar a subir (a los 12 s)");
                string t0 = SonidoNH90.EnToma(20.1f, 19.9f, 3); check(t0 != null && t0.StartsWith("sfx_nh90_toma_") && SonidoNH90.EnToma(10f, 9.9f, 3) == null, "toma: el clip empieza al empezar la bajada");
                check(SonidoNH90.RalentiDespegue(3f) && !SonidoNH90.RalentiDespegue(15f), "ralentí en tierra mientras gira el rotor");
                bool v = true; for (uint sd = 1; sd < 50; sd++) { var x = SonidoNH90.Variante(SonidoNH90.Paso, sd); int k = x[x.Length - 1] - '0'; if (k < 1 || k > 5) v = false; }
                check(v, "variantes _1 a _5");
                check(SonidoNH90.PesoNoche(12) == 0f && SonidoNH90.PesoNoche(23) == 1f && Math.Abs(SonidoNH90.PesoNoche(21.5) - 0.5f) < 0.01f && Math.Abs(SonidoNH90.PesoNoche(5.5) - 0.5f) < 0.01f, "ambiente: día y noche se funden a las 21 y a las 6 (una hora)");
                check(!SonidoNH90.PuedeRafaga(1.1f, 1.0f) && SonidoNH90.PuedeRafaga(1.2f, 1.0f), "M3M: al menos 0,15 s entre ráfagas");
            }

            // --- kit de defensas de Teo: recinto de Nules (HESCO al este, sacos en el resto) y valla de Bétera por vida
            {
                var plan = RecintoPlan.Nules(BalanceSource.Read("estacion_nules.json"));
                var esq = plan.FindAll(p => p.Malla.Contains("Esquina")); var rem = plan.FindAll(p => p.Malla.StartsWith("SM_HescoRemate") || p.Malla.StartsWith("SM_SacosRemate"));
                check(esq.Count == 4, $"Nules: 4 esquinas ({esq.Count})");
                check(rem.Count == 10, $"Nules: 10 remates junto a las puertas y al edificio ({rem.Count}: {string.Join(" ", rem.ConvertAll(r => $"({r.X:0.#};{r.Z:0.#})"))})");
                check(plan.Exists(p => p.Malla.StartsWith("SM_MuroHesco_") && Math.Abs(p.X - 188) < 0.01f && Math.Abs(p.Yaw - 90) < 0.01f), "el lado este va en HESCO, con el frente al este (yaw 90)");
                check(plan.Exists(p => p.Malla.StartsWith("SM_MuroSacos_") && Math.Abs(p.Z - 40) < 0.01f && Math.Abs(p.Yaw - 180) < 0.01f) && plan.Exists(p => p.Malla.StartsWith("SM_MuroSacos_") && Math.Abs(p.X - 85) < 0.01f && Math.Abs(p.Yaw - 270) < 0.01f), "sur (yaw 180) y oeste (yaw 270) en sacos");
                check(!plan.Exists(p => p.Largo > 0 && p.Z > 96.5f), "no hay muro sobre el edificio de viajeros (hace de muro norte)");
                bool libre = true; foreach (var p in plan) if (p.Largo > 0 && Math.Abs(p.X - 85) < 0.01f && p.Z > 60.8f + 0.1f && p.Z < 68.2f - 0.1f) libre = false;
                check(libre, "la puerta de vía doble del oeste queda libre (Z 60,8–68,2)");
                var swc = esq.Find(p => Math.Abs(p.X - 85) < 0.01f && Math.Abs(p.Z - 40) < 0.01f); check(Math.Abs(swc.Yaw - 270) < 0.01f, "la esquina suroeste gira 270 (recinto al noreste)");
                float u = RecintoPlan.UltimoCosteLineal, up = RecintoPlan.CostePuertas(BalanceSource.Read("estacion_nules.json"));
                check(up == 42f, $"puertas de Nules: 2 de vía dobles (12), 1 de vía (8) y 1 de vehículos (10) = {up} u");
                check(u + up >= 110 && u + up <= 140, $"coste del recinto (Hugo: HESCO al este con alas de 15 m y sacos en el resto): muros {u:0.0} u + puertas {up} u = {u + up:0.0} u (entre 110 y 140)");
                check(plan.Exists(p => p.Malla.Contains("Hesco") && Math.Abs(p.Z - 40) < 0.01f && p.X > 173 && p.X < 188) && plan.Exists(p => p.Malla.StartsWith("SM_MuroSacos_") && Math.Abs(p.Z - 40) < 0.01f && p.X < 173), "alas de HESCO de 15 m en el sur junto al este; el resto del sur, sacos");
                check(RecintoPlan.Estado(1f) == 0 && RecintoPlan.Estado(0.5f) == 1 && RecintoPlan.Estado(0.1f) == 2 && RecintoPlan.Estado(0f) == 3, "estados: Sano ≥ 70 %, Dañado, Muy dañado y Brecha");
                check(RecintoPlan.Nules(BalanceSource.Read("estacion_nules.json"), 0.5f).TrueForAll(p => p.Malla.Contains("_e1_Danado")), "con la vida al 50 % todas las piezas salen dañadas");
                check(RecintoPlan.VallaBetera(5000, false) == "SM_VallaBetera_Malla_e0_Sana" && RecintoPlan.VallaBetera(4000, true) == "SM_VallaBetera_MuroBajo_e1_SanaGolpeada" && RecintoPlan.VallaBetera(2000, false) == "SM_VallaBetera_Malla_e2_Danada" && RecintoPlan.VallaBetera(1000, false) == "SM_VallaBetera_Malla_e3_MuyDanada" && RecintoPlan.VallaBetera(0, false) == "SM_VallaBetera_Malla_e4_Brecha",
                    "valla de Bétera por vida: ≥ 4.250 sana, 3.500–4.250 golpeada, 1.500–3.500 dañada, 1–1.500 muy dañada y 0 brecha");
            }

            // --- ids de datos de las estaciones (existencias_red.json de Hugo) contra RailNet
            {
                var w = NetScenario.Mvp();
                check(TrenPreparar.NodoPorId(w, "sagunto") == TrenPreparar.Nodo(w, "Sagunto") && TrenPreparar.NodoPorId(w, "nules") == TrenPreparar.Nodo(w, "Nules") && TrenPreparar.NodoPorId(w, "castellon") == TrenPreparar.Nodo(w, "Castellón"), "los id sagunto, nules y castellon de los JSON encuentran su nodo en RailNet (sin tildes)");
                check(TrenPreparar.NodoPorId(w, "betera") == -1 && TrenPreparar.IdDe("Puçol") == "pucol", "betera no está en RailNet (es el aeródromo, vive en AerodromoEstado); Puçol → pucol");
            }

            // --- intro C
            var it = new IntroSecuencia();
            check(it.Tarjetas.Length == 5 && it.TextoEntero == "Día 41 desde el brote.", "intro C: 5 tarjetas y empieza por «Día 41 desde el brote.»");
            bool sinAnio = true; foreach (var t in it.Tarjetas) for (int y = 1990; y <= 2040; y++) if (t.Contains(y.ToString())) sinAnio = false;
            check(sinAnio, "la variante C no dice el año");
            var snd = new List<string>();
            it.Avanzar(0.1f, snd);
            check(it.Texto.Length > 0 && it.Texto.Length < it.TextoEntero.Length && snd.Exists(x => x.StartsWith("sfx_ui_tecla_")), "la tarjeta se escribe letra a letra, con tecla");
            snd.Clear(); for (int i = 0; i < 40; i++) it.Avanzar(0.02f, snd);
            check(it.Texto == "Día 41 desde el brote." && snd.Contains(Maquina.Campanilla) && snd.Contains(Maquina.Retorno), "al acabar la línea: retorno y campanilla");
            bool repite = false, hueco = true; string prev = null; var m0 = new Maquina(IntroSecuencia.VarianteC[3], 7); var lista = new List<string>(); float tt = 0, ult = -1;
            for (int i = 0; i < 400; i++) { lista.Clear(); m0.Avanzar(0.01f, lista); tt += 0.01f; foreach (var x in lista) if (x.StartsWith("sfx_ui_tecla_") && x != Maquina.Retorno && x != Maquina.Campanilla) { if (x == prev) repite = true; if (ult >= 0 && tt - ult < Maquina.MinEntreTeclas - 1e-4f) hueco = false; prev = x; ult = tt; } }
            check(!repite && hueco, "las teclas no repiten la anterior y van separadas al menos 25 ms");
            check(it.Alfa == 1f, "entera, se queda a la vista");
            it.Siguiente(); check(it.Indice == 1 && it.Texto.Length == 0, "clic con la tarjeta entera: pasa a la siguiente");
            it.Avanzar(0.1f); it.Siguiente(); check(it.Indice == 1 && it.Texto == it.TextoEntero, "clic a medias: completa el texto, no pasa");
            it.Avanzar(10f); check(it.Indice == 2, "sola, pasa a la siguiente tras leerla");
            var it3 = new IntroSecuencia(); float dur = 0; while (!it3.Acabada && dur < 100) { it3.Avanzar(0.05f); dur += 0.05f; }
            check(it3.Acabada && dur < 40f, $"sin tocar nada, la intro entera dura menos de 40 s ({dur:0} s)");
            var it2 = new IntroSecuencia(); it2.Avanzar(1f); it2.Saltar(); check(it2.Acabada && it2.Alfa == 0f && it2.Texto == null, "Esc la salta entera en cualquier momento");
        }
    }
}
