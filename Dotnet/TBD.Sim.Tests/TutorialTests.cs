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
            check(caps.Length == 4 && caps[0].Pasos.Length == 1 && caps[1].Pasos.Length == 10 && caps[2].Pasos.Length == 7 && caps[3].Pasos.Length == 13, "capítulos 0 a 3 con 1, 10, 7 y 13 pasos, como en tutorial-textos.md");
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
            check(m3.Estado == EstadoTutorial.Terminado && n == 31 && vistos[30] == "3.13", $"el recorrido completo pasa por los 31 pasos y acaba en 3.13 ({n})");
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
                while (ms.Actual.Id != "3.2") { var p = ms.Actual; if (p.Cierre == Cierre.Segundos) Run1(ms, p.Segundos); else foreach (var s in p.Senales) ms.Senal(s); }
                ms.Avanzar(0.1f); check(ms.Actual.Id == "3.2", "3.2 espera mientras el mapa no está abierto");
                mapa = true; ms.Avanzar(0.1f); check(ms.Actual.Id == "3.3", "con el mapa ya abierto, la 3.2 se cierra sola aunque la señal llegara antes");
            }

            // --- capítulo 3 (D-N172): refuerzos de Nules. «{eta}» lo rellena la vista; el 3.12 espera al tren; si Sagunto está sitiada sale el 3.12b
            {
                Action<TutorialMotor, string> Hasta = (mm, id) => { while (mm.Actual != null && mm.Actual.Id != id) { var p = mm.Actual; if (!mm.Visible) { mm.Senal(p.Espera); continue; } if (p.Cierre == Cierre.Segundos) Run1(mm, p.Segundos); else foreach (var x in p.Senales) { mm.Senal(x); if (mm.Actual != p) break; } } };
                var m4 = TutorialGuion.Nuevo(new MemoriaEnRam()); m4.Arrancar(850);
                Hasta(m4, "3.1");
                check(m4.Texto.StartsWith("El radar ve lo que viene esta noche") && Array.IndexOf(m4.Resalta, TutorialGuion.Resaltes.AvisoRadar) >= 0, "3.1 abre el capítulo con el aviso de radar resaltado");
                Hasta(m4, "3.7");
                m4.Senal(TutorialGuion.Senales.SinCarga); check(m4.Actual.Id == "3.8", "3.7 se cierra con «Sin carga»");
                Hasta(m4, "3.10");
                check(m4.Cabecera == "Capítulo 3 · Paso 10 de 13" && !m4.PideHordaEnEspera, "3.10: «Capítulo 3 · Paso 10 de 13» y sin horda en espera");
                Run1(m4, 5.2f);
                m4.Relleno = t => t.Replace("{eta}", "0:12");
                check(m4.Actual.Id == "3.11" && m4.Texto.EndsWith("Los refuerzos llegan en 0:12."), "3.11 escribe el ETA real en lugar de «0:xx»");
                m4.Senal(TutorialGuion.Senales.VueltaZona);
                check(m4.Actual.Id == "3.12" && !m4.Visible, "3.12 no sale hasta que entra el tren");
                m4.Senal(TutorialGuion.Senales.TrenEnSagunto);
                check(m4.Visible && m4.Actual.Id == "3.12" && m4.Texto == "Ya están aquí. Descarga los refuerzos.", "con el tren en Sagunto sale el 3.12");
                m4.Senal(TutorialGuion.Senales.RefuerzosEnTierra); check(m4.Actual.Id == "3.13", "refuerzos en tierra: 3.13");
                m4.Senal(TutorialGuion.Senales.OrdenMover); check(m4.Estado == EstadoTutorial.Terminado, "3.13 acaba el tutorial con la orden de mover");
                // 3.12b: Sagunto sitiada cuando entra el tren
                var m5 = TutorialGuion.Nuevo(new MemoriaEnRam()); m5.Arrancar(850); bool sitiada = false;
                m5.EstadoActual = x => x == TutorialGuion.Senales.SaguntoSitiada && sitiada;
                Hasta(m5, "3.12"); check(!m5.Visible, "3.12 espera al tren (con asedio también)");
                sitiada = true; m5.Senal(TutorialGuion.Senales.TrenEnSagunto);
                check(m5.Actual.Id == "3.12b" && m5.Texto.StartsWith("Llegan tarde") && m5.Cabecera == "Capítulo 3 · Paso 12 de 13", "con Sagunto sitiada sale el 3.12b en lugar del 3.12");
                m5.Senal(TutorialGuion.Senales.RefuerzosEnTierra); check(m5.Actual.Id == "3.12b", "el 3.12b no se cierra con «refuerzos en tierra»");
                m5.Senal(TutorialGuion.Senales.TropaEnTierra); check(m5.Actual.Id == "3.13", "se cierra con la tropa en tierra (D-N166)");
            }

            // --- destacamento de Nules (D-N172) sobre la red: tren parado en Nules, embarque, viaje a Sagunto y descarga en casa
            {
                var w = NetScenario.Mvp(); w.DirectorEnabled = false;
                int sag = TrenPreparar.Nodo(w, "Sagunto"), nul = TrenPreparar.Nodo(w, "Nules");
                int n0 = w.Services.Count, sv = Destacamento.SembrarTren(w);
                check(sv == n0 && Destacamento.SembrarTren(w) == sv && w.Services[sv].Train.Name.EndsWith("Nules"), "se crea un tren parado en Nules (una vez)");
                check(TrenPreparar.TrenEn(w, nul) == sv, "TrenEn lo encuentra en Nules");
                const int Pel = 77, Veh = 4;
                check(w.LoadBlockReason(sv, PasajeroTipo.Peloton, Pel, nul) != null, "un pelotón que no es del destacamento no sube en Nules (está en Sagunto)");
                Destacamento.Registrar(w, Pel, Veh); Destacamento.Registrar(w, Pel, Veh);
                check(w.Embarque.AtStation(nul, PasajeroTipo.Peloton).Count == 1 && w.Embarque.AtStation(nul, PasajeroTipo.Vehiculo).Count == 1, "el destacamento está en Nules (un pelotón y un vehículo, sin duplicar)");
                check(w.LoadBlockReason(sv, PasajeroTipo.Peloton, Pel, nul) == null && w.LoadPassenger(sv, PasajeroTipo.Peloton, Pel, nul) && w.LoadPassenger(sv, PasajeroTipo.Vehiculo, Veh, nul), "el pelotón y el Lince suben al tren de Nules");
                var carga = new int[NetConst.ResCount]; carga[(int)Res.Materiales] = 20; w.Stock[nul, (int)Res.Materiales] = Math.Max(w.Stock[nul, (int)Res.Materiales], 20);
                check(w.SetSchedule(sv, TrenPreparar.Envio(w, sv, sag, carga)) == ScheduleResult.Ok, "Preparar tren desde Nules: horario a Sagunto válido");
                long eta = TrenPreparar.EtaDesdeParada(w, sv, sag);
                long t0 = w.Tick; bool salio = false; var s = w.Services[sv];
                while (w.Tick - t0 < NetConst.DayTicks / 2) { w.Step(); if (!s.AtStop) salio = true; if (salio && s.AtStop) break; }
                check(salio && s.AtStop && s.Stops[s.StopIndex].Node == sag && s.Cargo[(int)Res.Materiales] == 20, $"llega a Sagunto con los materiales ({TrenPreparar.HhMm(w.Tick - t0)}, ETA {TrenPreparar.HhMm(eta)})");
                check(w.SetSchedule(sv, TrenPreparar.Aparcar(w, sv)) == ScheduleResult.Ok, "se aparca en Sagunto");
                check(w.UnloadPassengers(sv, sag) == 2 && w.Embarque.Aboard(sv, PasajeroTipo.Peloton).Count == 0 && w.Embarque.Find(PasajeroTipo.Peloton, Pel) < 0, "en casa bajan los dos y salen de la lista (son tropa de Sagunto)");
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
