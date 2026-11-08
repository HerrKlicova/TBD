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

            // --- capítulo 3: el 3.11 espera a que el tren llegue a Nules
            var m4 = TutorialGuion.Nuevo(new MemoriaEnRam()); m4.Arrancar(850);
            while (m4.Actual != null && m4.Actual.Id != "3.10") { var p = m4.Actual; if (p.Cierre == Cierre.Segundos) Run1(m4, p.Segundos); else foreach (var s in p.Senales) m4.Senal(s); }
            check(m4.Cabecera == "Capítulo 3 · Paso 10 de 13" && !m4.PideHordaEnEspera, "3.10: «Capítulo 3 · Paso 10 de 13» y sin horda en espera (no hay combate)");
            Run1(m4, 5);
            check(m4.Actual.Id == "3.11" && !m4.Visible && m4.Texto == null, "3.11 no sale hasta que el tren llega");
            m4.Senal(TutorialGuion.Senales.PanelNulesAbierto);
            check(m4.Actual.Id == "3.11", "abrir Nules antes de que llegue el tren no cierra el 3.11");
            m4.Senal(TutorialGuion.Senales.TrenEnNules);
            check(m4.Visible && m4.Actual.Id == "3.11", "con el tren en Nules sale el 3.11");
            m4.Senal(TutorialGuion.Senales.PanelNulesAbierto);
            check(m4.Actual.Id == "3.12", "y se cierra al abrir el panel de Nules");
            // 3.7 se cierra también con «Sin apoyo»
            var m5 = TutorialGuion.Nuevo(new MemoriaEnRam()); m5.Arrancar(850);
            while (m5.Actual.Id != "3.7") { var p = m5.Actual; if (p.Cierre == Cierre.Segundos) Run1(m5, p.Segundos); else foreach (var s in p.Senales) m5.Senal(s); }
            m5.Senal(TutorialGuion.Senales.SinApoyo); check(m5.Actual.Id == "3.8", "3.7 se cierra con «Sin apoyo»");

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

            // --- aviso del C90 (lectura de UnitsWorld)
            var uw = new UnitsWorld(5); var f = uw.AddPlatoon(PlatoonKind.Fusileros, 0, 0, 0);
            var zs = new List<Target>(); for (int i = 0; i < 20; i++) zs.Add(new Target { Id = i, X = 60_000 + (i % 5) * 600, Y = (i / 5) * 600 });
            check(f.C90Left > 0 && C90Aviso.Buscar(uw, zs) == f.Id, "un grupo de 20 zombis a 60 m: los fusileros lo tienen a tiro");
            var lejos = new List<Target>(); foreach (var z in zs) lejos.Add(new Target { Id = z.Id, X = z.X + 200_000, Y = z.Y });
            check(C90Aviso.Buscar(uw, lejos) == -1, "a 260 m, fuera del alcance del C90, no avisa");
            var sueltos = new List<Target>(); for (int i = 0; i < 20; i++) sueltos.Add(new Target { Id = i, X = 30_000 + i * 15_000, Y = 0 });
            check(C90Aviso.Buscar(uw, sueltos) == -1, "zombis sueltos (cada 15 m) no son un grupo denso");
            f.C90Left = 0; check(C90Aviso.Buscar(uw, zs) == -1, "sin C90 que lanzar, no avisa");
            var ua = new UnitsWorld(5); ua.AddPlatoon(PlatoonKind.Ametralladores, 0, 0, 0);
            check(C90Aviso.Buscar(ua, zs) == -1, "un pelotón sin C90 (ametralladores) no avisa");

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
                for (int i = 0; i < 600; i++) w.Step();
                check(s.AtStop && s.Stops[s.StopIndex].Node == nul, "en Nules espera a que lo descarguen");
                check(TrenPreparar.TrenEn(w, nul) == sv, "TrenEn lo encuentra en Nules");
                r = w.SetSchedule(sv, TrenPreparar.Descarga(w, sv, sag));
                for (int i = 0; i < 5; i++) w.Step();
                check(r == ScheduleResult.Ok && s.CargoTotal() == 0 && w.Stock[nul, (int)Res.Materiales] >= nulMat0 + 60, "Descargar deja la carga en Nules");
                check(!s.AtStop && s.Stops[s.StopIndex].Node == sag, "y el tren vuelve a Sagunto");
            }

            // --- intro C
            var it = new IntroSecuencia();
            check(it.Tarjetas.Length == 5 && it.Texto == "Día 41 desde el brote." && it.Duracion <= 40f, "intro C: 5 tarjetas, empieza por «Día 41 desde el brote.» y dura menos de 40 s");
            bool sinAnio = true; foreach (var t in it.Tarjetas) for (int y = 1990; y <= 2040; y++) if (t.Contains(y.ToString())) sinAnio = false;
            check(sinAnio, "la variante C no dice el año");
            it.Avanzar(0.1f); float a0 = it.Alfa; it.Avanzar(2.9f); float a1 = it.Alfa;
            check(a0 < 0.3f && a1 > 0.99f, "cada tarjeta entra con fundido");
            it.Avanzar(3.2f); check(it.Indice == 1, "a los 6 s pasa a la segunda");
            it.Avanzar(100f); check(it.Acabada && it.Texto == null, "al acabar no queda texto");
            var it2 = new IntroSecuencia(); it2.Avanzar(1f); it2.Saltar(); check(it2.Acabada && it2.Alfa == 0f, "se salta en cualquier momento");
        }
    }
}
