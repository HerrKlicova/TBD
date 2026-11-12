using System;
using System.Collections.Generic;
using TBD.Mundo;
using TBD.Net;
using TBD.Units;

namespace TBD.Sim.Tests
{
    /// <summary>[50] Núcleo v2, Mundo.Red (Iker, Hito 1): vía trazada por el jugador, tren con aceleración y frenada, línea automática de dos paradas,
    /// determinismo (dos mundos iguales) y guardado a mitad de viaje.</summary>
    static class RedTests
    {
        static Red Hito1(out int a, out int b)
        {
            var r = new Red();
            a = r.AddEstacion("Sagunto", 200_000, 1_000_000); b = r.AddEstacion("Caserío", 1_600_000, 1_300_000);
            return r;
        }
        static List<PointMm> Curva() { return new List<PointMm> { new PointMm(205_000, 1_003_000), new PointMm(700_000, 900_000), new PointMm(1_200_000, 1_150_000), new PointMm(1_595_000, 1_298_000) }; }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[50] Núcleo v2: red construida, tren y línea automática (Hito 1)");
            // almacén: lo que el pelotón saca de B (Nacho, recarga a 25 m de B)
            {
                var r = Hito1(out int a, out int b);
                check(r.EstacionPorNombre("Caserío") == b && r.EstacionPorNombre("Z") == -1, "la estación del frente se encuentra por su nombre");
                r.Meter(b, Red.Municion, 3);
                int s1 = r.Sacar(b, Red.Municion, 2), s2 = r.Sacar(b, Red.Municion, 5), s3 = r.Sacar(b, Red.Municion, 1);
                check(s1 == 2 && s2 == 1 && s3 == 0 && r.Existencias(b, Red.Municion) == 0, $"Sacar da solo lo que hay: {s1}, {s2}, {s3}");
                check(r.Sacar(99, Red.Municion, 1) == 0 && r.Existencias(-1, Red.Municion) == 0, "una estación que no existe no da nada");
            }
            // trazado
            {
                var r = Hito1(out int a, out int b);
                var res = r.TrazarVia(Curva());
                check(res.Ok && r.Via.Tramos.Count == 1, $"la vía se traza por cuatro puntos ({(res.Ok ? "ok" : res.Por)}; radio mínimo {res.RadioMinMm / 1000} m)");
                var t = r.Via.Tramos[0];
                check(t.A == r.Via.Estaciones[a].Nodo && t.B == r.Via.Estaciones[b].Nodo, "los extremos encajan en las estaciones (a menos de 12 m)");
                check(t.LongMm > 1_400_000 && t.LongMm < 1_700_000, $"longitud razonable: {t.LongMm / 1000} m (recta: {IMath.Dist(1_400_000, 300_000) / 1000} m)");
                var p0 = r.Via.Posicion(0, 0, out _); var p1 = r.Via.Posicion(0, t.LongMm, out _);
                check(p0.X == 200_000 && p0.Y == 1_000_000 && p1.X == 1_600_000 && p1.Y == 1_300_000, "la vía empieza y acaba en el centro de las estaciones");
                var cerrada = r.TrazarVia(new List<PointMm> { new PointMm(1_600_000, 1_300_000), new PointMm(1_640_000, 1_300_000), new PointMm(1_640_000, 1_340_000), new PointMm(1_600_000, 1_340_000) });      // horquilla de 40 m desde el caserío
                check(!cerrada.Ok && cerrada.Por.Contains("curva") && r.Via.Tramos.Count == 1, $"una curva de menos de 60 m se rechaza sin tocar nada ({cerrada.Por})");
                check(!r.TrazarVia(new List<PointMm> { new PointMm(200_000, 1_000_000), new PointMm(203_000, 1_002_000) }).Ok, "no se puede trazar de una estación a sí misma");
                bool soloKit = t.Piezas != null && t.Piezas.Length > 0; int curvas = 0; foreach (var pz in t.Piezas) { if (Via.RadioMm(pz) > 0) { curvas++; if (Via.RadioMm(pz) < 60_000) soloKit = false; } }
                check(soloKit && curvas > 0, $"D-N209: la vía son {t.Piezas.Length} piezas del kit de Teo ({curvas} curvas, ninguna de menos de R 60)");
                check(!r.TrazarVia(new List<PointMm> { new PointMm(900_000, 500_000), new PointMm(1_000_000, 500_000) }).Ok, "una vía que no empieza en una estación ni en otra vía se rechaza");
                check(r.Via.Ruta(r.Via.Estaciones[a].Nodo, r.Via.Estaciones[b].Nodo).Count == 1 && r.Via.Ruta(r.Via.Estaciones[b].Nodo, r.Via.Estaciones[a].Nodo)[0].Directo == false, "la ruta va y vuelve por el mismo tramo");
            }
            // paso a nivel (Teo y Lucía): la vía que cruza una carretera lleva su paso a nivel; por debajo de 30° se rechaza
            {
                var r = Hito1(out int a, out int b);
                r.Via.Carreteras.Add(new[] { new PointMm(900_000, 600_000), new PointMm(920_000, 1_600_000) });      // casi norte-sur: la vía la cruza casi en perpendicular
                var res = r.TrazarVia(Curva());
                check(res.Ok && r.Via.PasosNivel.Count == 1 && r.Via.PasosNivel[0].AnguloGrados >= 60, $"la vía cruza la carretera con un paso a nivel de {(r.Via.PasosNivel.Count > 0 ? r.Via.PasosNivel[0].AnguloGrados : 0)}° (pieza SM_PasoNivel)");
                var r2 = Hito1(out int a2, out int b2);
                r2.Via.Carreteras.Add(new[] { new PointMm(150_000, 960_000), new PointMm(1_650_000, 1_345_000) });      // casi paralela a la vía, la corta con poco ángulo
                var res2 = r2.TrazarVia(Curva());
                check(!res2.Ok && res2.Por.StartsWith("Cruce demasiado sesgado") && r2.Via.Tramos.Count == 0, $"un cruce de menos de 30° se rechaza: «{res2.Por}»");
                var w = new SaveWriter(); r.Save(w); var rc = new Red(); rc.Load(new SaveReader(w.ToArray()));
                check(rc.Hash() == r.Hash() && rc.Via.PasosNivel.Count == 1, "el paso a nivel y la carretera se guardan y se cargan");
            }
            // terreno v2 de Álvaro: la carretera vieja en S corta la recta A–B una vez, a 58°, en (1174; 1006); el puente no cuenta
            {
                string csv = null; foreach (var cand in new[] { "../../Assets/StreamingAssets/Hito1/carreteras_hito1.csv", "../../../../../Assets/StreamingAssets/Hito1/carreteras_hito1.csv" }) if (System.IO.File.Exists(cand)) { csv = System.IO.File.ReadAllText(cand); break; }
                if (csv == null) check(true, "(sin carreteras_hito1.csv en esta rama: prueba del terreno v2 omitida)");
                else
                {
                    var r = new Red(); int a = r.AddEstacion("A", 1_060_000, 430_000), b = r.AddEstacion("B", 1_300_000, 1_640_000);
                    int nc = r.Via.CargarCarreterasCsv(csv);
                    var res = r.TrazarVia(new List<PointMm> { new PointMm(1_060_000, 430_000), new PointMm(1_300_000, 1_640_000) });
                    var pn = r.Via.PasosNivel.Count > 0 ? r.Via.PasosNivel[0] : default;
                    check(nc >= 2 && res.Ok && r.Via.PasosNivel.Count == 1 && pn.AnguloGrados == 60 && IMath.Dist(pn.P.X - 1_174_000, pn.P.Y - 1_006_000) < 15_000, $"A–B corta la carretera vieja una vez: paso a nivel de {pn.AnguloGrados}° en ({pn.P.X / 1000}; {pn.P.Y / 1000}) ({nc} trozos de carretera sin el puente; vía de {(res.Ok ? r.Via.Tramos[0].LongMm / 1000 : 0)} m)");
                }
            }
            // el escenario del caserío de Nacho con la red montada (MontarRedHito1): A y B de Puntos, carreteras, Tren 1 y 20 cajas en A
            {
                var lectorAntes = TBD.Mundo.Mundo.Lector;
                string raiz = null; foreach (var c in new[] { "../../Assets/StreamingAssets/Hito1", "../../../../../Assets/StreamingAssets/Hito1" }) if (System.IO.Directory.Exists(c)) { raiz = c; break; }
                if (raiz == null) check(true, "(sin StreamingAssets/Hito1: prueba del caserío con red omitida)");
                else
                {
                    TBD.Mundo.Mundo.Lector = f => { var q = System.IO.Path.Combine(raiz, f); return System.IO.File.Exists(q) ? System.IO.File.ReadAllText(q) : null; };
                    var m = TBD.Mundo.Mundo.Caserio(5); m.MontarRedHito1();
                    check(m.Red.Via.Estaciones.Count == 2 && m.Red.Trenes.Count == 1 && m.Red.Almacen[0][Red.Municion] == 20 && m.Red.Via.Carreteras.Count >= 2, $"MontarRedHito1: estaciones A y B, Tren 1, 20 cajas en A y {m.Red.Via.Carreteras.Count} trozos de carretera");
                    var a0 = m.Puntos["A"]; var b0 = m.Puntos["B"];
                    m.Encolar(new Orden { Tick = 1, Jugador = 1, Tipo = OrdenesRed.PuntoVia, A = a0.X, B = a0.Y }); m.Encolar(new Orden { Tick = 1, Jugador = 1, Tipo = OrdenesRed.PuntoVia, A = b0.X, B = b0.Y }); m.Encolar(new Orden { Tick = 2, Jugador = 1, Tipo = OrdenesRed.TrazarVia });
                    for (int t = 0; t < 10; t++) m.Step();
                    check(m.Red.Via.Tramos.Count == 1 && m.Red.Via.PasosNivel.Count == 1 && m.Red.Via.PasosNivel[0].AnguloGrados == 60, $"en el caserío, la vía A–B se traza por órdenes y cruza la carretera con el paso a nivel de 60° ({m.Red.Via.Tramos.Count} tramo, {m.Red.Via.PasosNivel.Count} paso)");
                    check(m.Red.Lineas.Count == 0 && m.Red.LineaAuto, "aún sin línea: la crea la simulación en el siguiente segundo entero");
                    for (int t = 0; t < 40; t++) m.Step();
                    check(m.Red.Lineas.Count == 1 && m.Red.Trenes[0].Linea == 0 && m.Red.LineaAutoCreadaTick % 30 == 0 && !m.Red.LineaAuto, $"A y B unidas: la simulación crea la línea por defecto y asigna el Tren 1 (tick {m.Red.LineaAutoCreadaTick}), sin depender de la vista");
                    check(m.Red.Via.MarDesdeMm.Length == 2049, "MontarRedHito1 carga el mar (agua_hito1.csv)");
                    Console.WriteLine($"MEDIDA A–B recta: {Red.Pales(m.Red.Via.Tramos[0].Coste)} ({m.Red.Via.Tramos[0].Coste} m)");
                    check(m.Red.CuestaMaterial && m.Red.Via.Tramos[0].Coste > 0 && m.Red.MaterialM(0) >= 200, $"en el caserío la vía A–B recta cuesta {Red.Pales(m.Red.Via.Tramos[0].Coste)} y a A le quedan {Red.Pales(m.Red.MaterialM(0))} (Hugo: al menos 2 de margen; A empieza con 19)");
                    TBD.Mundo.Mundo.Lector = lectorAntes;
                }
            }
            // tren solo: acelera, llega y para en el andén
            {
                var r = Hito1(out int a, out int b); r.TrazarVia(Curva());
                int tr = r.NuevoTren("1 Costa", a);
                // la línea de Hugo (cifras-hito1): 4 cajas de A a B y vuelve con 2 palés de material
                int l = r.CrearLinea("Munición", new List<ParadaLinea> { new ParadaLinea { Estacion = a, Cargar = new[] { 4, 0, 0, 0 }, Descargar = new[] { 0, 0, -1, 0 } }, new ParadaLinea { Estacion = b, Cargar = new[] { 0, 0, 2, 0 }, Descargar = new[] { -1, 0, 0, 0 } } });
                r.Almacen[a][Red.Municion] = 12; r.Almacen[b][Red.Material] = 6;
                check(r.AsignarTren(l, tr), "el tren se asigna a la línea");
                int vmax = 0, tLlegada = -1, tVuelta = -1, vueltas = 0, tV1 = -1, tV2 = -1; bool enA = true;
                for (int tick = 1; tick <= 30 * 60 * 40 && vueltas < 3; tick++)
                {
                    r.Step(tick); var t = r.Trenes[tr];
                    if (t.VelMmT > vmax) vmax = t.VelMmT;
                    if (t.Estado == EstadoTren.EnParada && t.EsperaT == 1) { int est = r.Via.Nodos[t.Nodo].Estacion; if (est == b && enA) { if (tLlegada < 0) tLlegada = tick; enA = false; } else if (est == a && !enA) { if (tVuelta < 0) tVuelta = tick; enA = true; vueltas++; if (vueltas == 1) tV1 = tick; if (vueltas == 2) tV2 = tick; } }
                }
                check(vmax == r.Trenes[tr].VmaxMmT, $"el tren llega a su velocidad máxima ({vmax} mm/tick)");
                check(tLlegada > 0 && tLlegada < 30 * 300, $"llega a la segunda parada en {tLlegada / 30} s (40 km/h, 60 s de carga en A)");
                check(vueltas == 3, $"la línea se repite sola: {vueltas} vueltas completas");
                check(r.TextoLinea(l).StartsWith("4 cajas por viaje · un viaje cada ") && r.TextoLinea(l).EndsWith(" min"), "texto de la línea con la vuelta medida: «" + r.TextoLinea(l) + "»");
                int vuelta = tV2 - tV1; check(vuelta > 30 * 300 && vuelta < 30 * 400, $"una vuelta tarda {vuelta / 30} s (Hugo, 5 s por hueco: ~5,3 min con 1250 m; esta vía mide 1502 m)");
                check(r.Almacen[a][Red.Municion] == 0 && r.Almacen[b][Red.Municion] == 12 && r.Almacen[a][Red.Material] + r.Trenes[tr].Carga[Red.Material] == 6 && r.Almacen[b][Red.Material] == 0, $"lleva 4 cajas por viaje y vuelve con el material: cajas A {r.Almacen[a][0]} / B {r.Almacen[b][0]}, palés A {r.Almacen[a][2]} + {r.Trenes[tr].Carga[2]} a bordo (llega y aún no ha descargado) / B {r.Almacen[b][2]}");
            }
            // determinismo: dos mundos iguales con las mismas órdenes; guardado a mitad de viaje
            {
                Red Montar() { var r = Hito1(out int a, out int b); r.TrazarVia(Curva()); int tr = r.NuevoTren("1 Costa", a); r.Almacen[a][0] = 50; int l = r.CrearLinea("M", new List<ParadaLinea> { new ParadaLinea { Estacion = a, Cargar = new[] { -1, 0, 0, 0 } }, new ParadaLinea { Estacion = b, Descargar = new[] { -1, 0, 0, 0 } } }); r.AsignarTren(l, tr); return r; }
                var r1 = Montar(); var r2 = Montar(); bool iguales = true;
                for (int tick = 1; tick <= 6000; tick++) { r1.Step(tick); r2.Step(tick); if (r1.Hash() != r2.Hash()) { iguales = false; break; } }
                check(iguales, "dos mundos con las mismas órdenes dan el mismo hash en cada tick (6000 ticks)");
                var w = new SaveWriter(); r1.Save(w); var r3 = new Red(); r3.Load(new SaveReader(w.ToArray()));
                check(r3.Hash() == r1.Hash(), "guardar y cargar a mitad de viaje da el mismo hash");
                for (int tick = 6001; tick <= 24000; tick++) { r1.Step(tick); r3.Step(tick); }
                check(r3.Hash() == r1.Hash() && r1.Almacen[1][0] == r3.Almacen[1][0], $"y sigue igual 18000 ticks después (Caserío: {r1.Almacen[1][0]} u en los dos)");
                check(r1.Trenes[0].Carga[0] <= 6 && r1.Almacen[1][0] > 0, $"el tren no pasa de sus 6 huecos (lleva {r1.Trenes[0].Carga[0]}; el caserío ha recibido {r1.Almacen[1][0]} cajas)");
                var hs1 = new List<KeyValuePair<string, ulong>>(); r1.HashPorSistema(hs1); r1.Almacen[1][0]++; var hs2 = new List<KeyValuePair<string, ulong>>(); r1.HashPorSistema(hs2); r1.Almacen[1][0]--;
                var dif = new List<string>(); for (int i = 0; i < hs1.Count; i++) if (hs1[i].Value != hs2[i].Value) dif.Add(hs1[i].Key);
                check(hs1.Count == 4 && dif.Count == 1 && dif[0] == "red.almacen", $"el hash por sistema señala solo el sistema que cambia ({string.Join(", ", dif)})");
            }
            // dentro de Mundo (contrato de Nacho): la vía, la línea y el tren entran como órdenes; dos mundos con la misma grabación dan el mismo hash
            {
                TBD.Mundo.Mundo Montar()
                {
                    var m = new TBD.Mundo.Mundo(7u, 2_000_000, 2_000_000);
                    m.Red.AddEstacion("A", 200_000, 1_000_000); m.Red.AddEstacion("B", 1_600_000, 1_300_000); m.Red.Almacen[0][0] = 12; m.Red.Almacen[1][2] = 6;
                    foreach (var p in Curva()) m.Encolar(new Orden { Tick = 10, Jugador = 1, Tipo = OrdenesRed.PuntoVia, A = p.X, B = p.Y });
                    m.Encolar(new Orden { Tick = 11, Jugador = 1, Tipo = OrdenesRed.TrazarVia });
                    m.Encolar(new Orden { Tick = 12, Jugador = 2, Tipo = OrdenesRed.NuevoTren, A = 0 });
                    m.Encolar(new Orden { Tick = 13, Jugador = 2, Tipo = OrdenesRed.NuevaLinea, A = 0, B = 1 });      // por defecto: 4 cajas y 2 palés (Hugo)
                    m.Encolar(new Orden { Tick = 14, Jugador = 1, Tipo = OrdenesRed.AsignarTren, A = 0, B = 0 });
                    return m;
                }
                var m1 = Montar(); var m2 = Montar(); bool igual = true; string difiere = "";
                for (int t = 0; t < 30 * 60 * 25; t++)
                {
                    m1.Step(); m2.Step();
                    if (m1.Hash() != m2.Hash()) { igual = false; var a1 = m1.HashPorSistema(); var a2 = m2.HashPorSistema(); for (int i = 0; i < a1.Count; i++) if (a1[i].Value != a2[i].Value) difiere += a1[i].Key + " "; break; }
                }
                check(m1.Red.Via.Tramos.Count == 1 && m1.Red.Trenes.Count == 1 && m1.Red.Lineas.Count == 1, "las órdenes de red de dos jugadores trazan la vía, crean el tren y la línea");
                check(igual, $"dos Mundos con las mismas órdenes, mismo hash 25 minutos de juego{(igual ? "" : " (difiere: " + difiere + ")")}");
                check(m1.Red.Almacen[1][0] == 12 && m1.Red.Almacen[0][2] == 6, $"la línea por defecto lleva las 12 cajas a B y trae los 6 palés a A en 25 minutos (cajas B {m1.Red.Almacen[1][0]}, palés A {m1.Red.Almacen[0][2]})");
                var hs = m1.HashPorSistema(); var nombres = new List<string>(); foreach (var kv in hs) nombres.Add(kv.Key);
                check(nombres.Contains("red.via") && nombres.Contains("red.trenes") && nombres.Contains("red.trazos") && nombres.Contains("unidades"), "el hash por sistema de Mundo incluye la red (" + string.Join(", ", nombres) + ")");
            }
            // Marcos (Mundo11): A → P(1150; 900) → B en el terreno real, y A → mar rechazado
            {
                string Leer(string f) { foreach (var cand in new[] { "../../Assets/StreamingAssets/Hito1/" + f, "../../../../../Assets/StreamingAssets/Hito1/" + f }) if (System.IO.File.Exists(cand)) return System.IO.File.ReadAllText(cand); return null; }
                string csv = Leer("carreteras_hito1.csv"), mar = Leer("agua_hito1.csv");
                if (csv == null || mar == null) check(true, "(sin carreteras o mar del Hito 1 en esta rama: prueba omitida)");
                else
                {
                    var r = new Red(); r.AddEstacion("A", 1_060_000, 430_000); r.AddEstacion("B", 1_300_000, 1_640_000); r.Via.CargarCarreterasCsv(csv); r.Via.CargarMarCsv(mar);
                    var r1 = r.TrazarVia(new List<PointMm> { new PointMm(1_060_000, 430_000), new PointMm(1_110_000, 700_000), new PointMm(1_150_000, 900_000) });
                    check(r1.Ok, $"A → P(1150; 900) con final libre se traza ({(r1.Ok ? r.Via.Tramos[r1.Tramo].Piezas.Length + " piezas" : r1.Por)})");
                    var r2 = r.TrazarVia(new List<PointMm> { new PointMm(1_150_000, 900_000), new PointMm(1_390_000, 1_300_000), new PointMm(1_300_000, 1_640_000) });
                    if (r1.Ok && r2.Ok) { r.Via.Tramos[r1.Tramo].Coste = 0; int cm = Via.CosteMetros(r.Via.Tramos[r1.Tramo].Piezas, 0) + Via.CosteMetros(r.Via.Tramos[r2.Tramo].Piezas, r.Via.PasosNivel.Count); Console.WriteLine($"MEDIDA A–P–B (guion de Marcos): {Red.Pales(cm)} ({cm} m)"); }
                    check(r2.Ok && r.Via.Ruta(r.Via.Estaciones[0].Nodo, r.Via.Estaciones[1].Nodo)?.Count == 2, $"P → B por el punto guía de Marcos (1390; 1300) sigue desde el final de la anterior y une A con B ({(r2.Ok ? "ok, " + r.Via.PasosNivel.Count + " paso a nivel" : r2.Por)})");
                    check(r.Via.MarDesdeMm.Length == 2049 && r.Via.EnMar(new PointMm(1_950_000, 900_000)) && !r.Via.EnMar(new PointMm(1_300_000, 1_640_000)), "el mar de usos_2049 está al este (1950; 900 es mar; B no)");
                    {   // para la captura de Marcos: P → (1330; 1600) → (1338; 1640) llega a B de lado y cerrado: se rechaza «no llega a la estación B»
                        var w0 = new SaveWriter(); r.Save(w0); var rr = new Red(); rr.Load(new SaveReader(w0.ToArray())); rr.BorrarVia(r2.Tramo);
                        var q = rr.TrazarVia(new List<PointMm> { new PointMm(1_150_000, 900_000), new PointMm(1_330_000, 1_600_000), new PointMm(1_338_000, 1_640_000) });
                        check(!q.Ok && q.Por.StartsWith("no llega a la estación B"), $"P → (1330; 1600) → (1338; 1640): «{q.Por}»");
                    }
                    var r3 = r.TrazarVia(new List<PointMm> { new PointMm(1_060_000, 430_000), new PointMm(1_950_000, 900_000) });
                    check(!r3.Ok && r3.Por.Contains("mar") && r.Via.Tramos.Count == 2, $"A → mar (1950; 900) se rechaza ({r3.Por})");
                    var w = new SaveWriter(); r.Save(w); var rc = new Red(); rc.Load(new SaveReader(w.ToArray()));
                    check(rc.Hash() == r.Hash() && rc.Via.EnMar(new PointMm(1_950_000, 900_000)), "la copia de la vista previa (guardar y cargar) también sabe dónde está el mar");
                }
            }
            // D-N210 (Lucía): textos del panel de la línea y de la placa del almacén
            {
                var r = Hito1(out int a, out int b); r.TrazarVia(Curva()); int tr = r.NuevoTren("Tren 1", a);
                r.Almacen[a][Red.Municion] = 40; r.Almacen[b][Red.Material] = 20;
                int l = r.CrearLineaPorDefecto(a, b); r.AsignarTren(l, tr);
                check(r.TituloLinea(l) == "Línea 1 · Sagunto ⇄ Caserío" && r.CargaLinea(l) == "Lleva 4 cajas de munición por viaje · trae material de vuelta" && r.FrecuenciaLinea(l) == "Midiendo el primer viaje…",
                    $"panel: «{r.TituloLinea(l)}» · «{r.CargaLinea(l)}» · «{r.FrecuenciaLinea(l)}»");
                var vistos = new HashSet<string>(); int tick = 0;
                for (; tick < 30 * 60 * 30; tick++) { r.Step(tick + 1); var e = r.EstadoTrenTexto(tr, out _); vistos.Add(e.StartsWith("Hacia") ? "Hacia" : e); if (tick % 900 == 0 && r.Almacen[b][Red.Municion] > 0) r.Sacar(b, Red.Municion, 1); }      // B gasta 1 caja cada 30 s
                check(vistos.Contains("Cargando en Sagunto") && vistos.Contains("Hacia") && vistos.Contains("Descargando en Caserío") && vistos.Contains("Cargando en Caserío"), "estado en vivo: " + string.Join(" / ", vistos));
                var hacia = ""; for (int k = 0; k < 30 * 60 * 6 && !hacia.StartsWith("Hacia"); k++) { r.Step(++tick + 1); hacia = r.EstadoTrenTexto(tr, out _); }
                check(System.Text.RegularExpressions.Regex.IsMatch(hacia, @"^Hacia (Sagunto|Caserío) · llega en \d+:\d\d$"), $"«{hacia}»");
                string fr = r.FrecuenciaLinea(l); check(System.Text.RegularExpressions.Regex.IsMatch(fr, @"^Un viaje cada \d+,\d min$") && r.Trenes[tr].PenultimaVueltaT > 0, $"«{fr}» (media de las 2 últimas vueltas: {r.Trenes[tr].UltimaVueltaT / 30} s y {r.Trenes[tr].PenultimaVueltaT / 30} s)");
                string ex = r.ExistenciasAlmacen(b, 105); check(ex.StartsWith(r.Almacen[b][Red.Municion] + " ") && ex.Contains(" · " + r.Almacen[b][Red.Municion] * 105 + " cartuchos"), $"placa de B: «{ex}»");
                string tb = r.TendenciaAlmacen(b, out bool amb); check(tb == "Sube" || tb == "Se mantiene" || tb.StartsWith("Se agota en ~"), $"B (recibe 4 por viaje, gasta 1 cada 30 s): «{tb}»{(amb ? " (ámbar)" : "")}");
                string ta = r.TendenciaAlmacen(a, out _); check(ta.StartsWith("Se agota en ~"), $"A (solo salen cajas): «{ta}»");
                var w = new SaveWriter(); r.Save(w); var rc = new Red(); rc.Load(new SaveReader(w.ToArray()));
                check(rc.Hash() == r.Hash() && rc.TendenciaAlmacen(b, out _) == tb && rc.FrecuenciaLinea(l) == fr, "guardar y cargar conserva contadores, fotos y vueltas");
                var r2 = Hito1(out a, out b); int t2 = r2.NuevoTren("Tren 2", a); int l2 = r2.CrearLineaPorDefecto(a, b); r2.AsignarTren(l2, t2); for (int k = 1; k <= 200; k++) r2.Step(k);
                check(r2.EstadoTrenTexto(t2, out bool av) == "Sin vía entre Sagunto y Caserío" && av, $"sin vía: «{r2.EstadoTrenTexto(t2, out _)}» (ámbar)");
            }
            // Marcos (Mundo21): soltar a ~20 m de B ya no deja la vía suelta
            {
                var r = Hito1(out int a, out int b);
                var res = r.TrazarVia(new List<PointMm> { new PointMm(200_000, 1_000_000), new PointMm(900_000, 1_150_000), new PointMm(1_580_000, 1_292_000) });      // acaba a ~22 m de B
                check(res.Ok && r.Via.Tramos[res.Tramo].B == r.Via.Estaciones[b].Nodo && r.Via.Nodos.Count == 2, $"un trazo que acaba a 22 m de B se lleva hasta B y encaja ({(res.Ok ? "ok" : res.Por)}; nodos {r.Via.Nodos.Count})");
                var r2 = Hito1(out a, out b);
                var no = r2.TrazarVia(new List<PointMm> { new PointMm(200_000, 1_000_000), new PointMm(1_000_000, 1_000_000), new PointMm(1_570_000, 1_000_000), new PointMm(1_600_000, 1_270_000) });      // llega de lado, sin sitio para girar
                check(no.Ok ? r2.Via.Tramos[no.Tramo].B == r2.Via.Estaciones[b].Nodo && r2.Via.Nodos.Count == 2 : (no.Por.Contains("no llega a la estación") || no.Por.Contains("curva")) && r2.Via.Tramos.Count == 0,
                    $"llegando de lado a B: o encaja en B o se rechaza diciendo por qué; nunca queda un extremo suelto junto a la estación ({(no.Ok ? "encaja" : no.Por)})");
            }
            // D-N211 c: «B llena»: el tren descarga lo que cabe y vuelve con el resto; la placa dice «Lleno» en ámbar
            {
                var m = new TBD.Mundo.Mundo(7u, 2_000_000, 2_000_000);
                m.Red.AddEstacion("A", 200_000, 1_000_000); m.Red.AddEstacion("B", 1_600_000, 1_300_000); m.Red.Almacen[0][0] = 12;
                foreach (var p in Curva()) m.Encolar(new Orden { Tick = 2, Jugador = 1, Tipo = OrdenesRed.PuntoVia, A = p.X, B = p.Y });
                m.Encolar(new Orden { Tick = 3, Jugador = 1, Tipo = OrdenesRed.TrazarVia });
                m.Encolar(new Orden { Tick = 4, Jugador = 1, Tipo = OrdenesRed.PruebaAlmacen, A = 1, B = 8, C = 6 });      // B casi llena: 6 de 8
                m.Encolar(new Orden { Tick = 5, Jugador = 1, Tipo = OrdenesRed.NuevoTren, A = 0 });
                m.Encolar(new Orden { Tick = 6, Jugador = 1, Tipo = OrdenesRed.NuevaLinea, A = 0, B = 1 });
                m.Encolar(new Orden { Tick = 7, Jugador = 1, Tipo = OrdenesRed.AsignarTren, A = 0, B = 0 });
                var R = m.Red; string visto = null; bool ambarB = false; string placa = null;
                for (int k = 0; k < 30 * 60 * 8 && visto == null; k++) { m.Step(); var e = R.EstadoTrenTexto(0, out bool av); if (e.Contains("llena")) { visto = e; placa = R.TendenciaAlmacen(1, out ambarB); } }
                check(visto != null && visto.StartsWith("B llena: vuelven 2 cajas · Hacia A"), $"panel: «{visto}»");
                check(R.Almacen[1][Red.Municion] == 8 && R.Trenes[0].Carga[Red.Municion] == 2 && placa == "Lleno" && ambarB, $"B se queda en 8 (lleno), el tren lleva 2 de vuelta y la placa dice «{placa}» en ámbar");
                int cargaA = -1; for (int k = 0; k < 30 * 60 * 6; k++) { m.Step(); if (R.Trenes[0].Estado == EstadoTren.EnMarcha && R.Trenes[0].Parada == 1) { cargaA = R.Trenes[0].Carga[Red.Municion]; break; } }
                check(cargaA > 0 && cargaA <= 6, $"en A no pasa de sus 6 huecos y vuelve hacia B con {cargaA} cajas");
                var w = new SaveWriter(); R.Save(w); var rc = new Red(); rc.Load(new SaveReader(w.ToArray()));
                check(rc.Hash() == R.Hash() && rc.CapacidadCajas[1] == 8, "guardar y cargar conserva la capacidad y lo que vuelve");
            }
            // Sara (Mundo25): la vía que acaba suelta a 40–100 m de una estación se acepta, pero se avisa
            {
                var r = Hito1(out int a, out int b);
                var res = r.TrazarVia(new List<PointMm> { new PointMm(200_000, 1_000_000), new PointMm(900_000, 1_150_000), new PointMm(1_550_000, 1_290_000) });      // acaba a ~51 m de B
                string av = res.Ok ? r.Via.AvisoFinal(res.Tramo) : null;
                check(res.Ok && av != null && av.StartsWith("Acaba suelta: no llega a Caserío (a "), $"se acepta y avisa: «{av}»");
                var r2 = Hito1(out a, out b); var ok = r2.TrazarVia(Curva());
                check(ok.Ok && r2.Via.AvisoFinal(ok.Tramo) == null, "la que encaja en B no avisa");
                var r3 = Hito1(out a, out b); var lejos = r3.TrazarVia(new List<PointMm> { new PointMm(200_000, 1_000_000), new PointMm(700_000, 1_000_000) });
                check(lejos.Ok && r3.Via.AvisoFinal(lejos.Tramo) == null, "la que acaba lejos de toda estación (como A → P) no avisa");
            }
            // coste de material (Hugo, cifras-hito1): en metros enteros (1 palé = 100 m), curvas ×1,5, paso a nivel 200 m; borrar devuelve el 100 %
            {
                check(Via.CosteMetros(new[] { Via.Pieza.Recta10 }, 0) == 10 && Via.CosteMetros(new[] { Via.Pieza.Izq60 }, 0) == 24 && Via.CosteMetros(new[] { Via.Pieza.Recta2_5 }, 1) == 203, "coste: recta de 10 m = 10 m; curva de 15,71 m = 24 m (×1,5); recta de 2,5 m + paso a nivel = 203 m");
                check(Red.Pales(1490) == "14 palés y 90 m" && Red.Pales(1600) == "16 palés" && Red.Pales(130) == "1 palé y 30 m" && Red.Pales(30) == "30 m", "en pantalla, «N palés y M m» sin decimales ni redondeo (1490 m = «14 palés y 90 m»)");
                var r = Hito1(out int a, out int b); int m = r.AddEstacion("Medio", 900_000, 1_150_000); r.CuestaMaterial = true; r.Almacen[a][Red.Material] = 16;
                var t0 = r.TrazarVia(new List<PointMm> { new PointMm(200_000, 1_000_000), new PointMm(900_000, 1_150_000) });
                int c0 = t0.Ok ? r.Via.Tramos[t0.Tramo].Coste : -1;
                check(t0.Ok && c0 == Via.CosteMetros(r.Via.Tramos[0].Piezas, 0) && r.MaterialM(a) == 1600 - c0, $"A–Medio ({r.Via.Tramos[0].LongMm / 1000} m) cuesta {Red.Pales(c0)}, sin redondear; a A le quedan {Red.Pales(r.MaterialM(a))} ({r.Almacen[a][Red.Material]} palés y {r.MetrosSueltos[a]} m sueltos)");
                var t1 = r.TrazarVia(new List<PointMm> { new PointMm(900_000, 1_150_000), new PointMm(1_600_000, 1_300_000) });
                check(t1.Ok && r.Via.Tramos[t1.Tramo].Pago == a, $"Medio–B sale de Medio (sin material) y lo paga A, unida por vía (quedan {Red.Pales(r.MaterialM(a))})");
                int quedan = r.MaterialM(a);
                var t2 = r.TrazarVia(new List<PointMm> { new PointMm(1_600_000, 1_300_000), new PointMm(1_700_000, 1_700_000) });
                check(!t2.Ok && t2.Por.Contains("falta material") && t2.Por.Contains("faltan") && r.MaterialM(a) == quedan && r.Via.Tramos.Count == 2, $"sin material suficiente se rechaza entera y no gasta nada ({t2.Por})");
                check(r.BorrarVia(1) && r.MaterialM(a) == quedan + r.Via.Tramos[1].Coste, $"al borrar Medio–B, A recupera el 100 % ({Red.Pales(r.Via.Tramos[1].Coste)})");
                var w = new SaveWriter(); r.Save(w); var rc = new Red(); rc.Load(new SaveReader(w.ToArray()));
                check(rc.Hash() == r.Hash() && rc.CuestaMaterial && rc.Via.Tramos[0].Coste == c0 && rc.MaterialM(a) == r.MaterialM(a), "guardar y cargar conserva el coste, quién pagó y los metros sueltos");
                var rg = Hito1(out a, out b); check(rg.TrazarVia(Curva()).Ok && rg.Via.Tramos[0].Coste == 0, "sin CuestaMaterial (pruebas y goldens viejos) la vía sigue siendo gratis");
            }
            // borrar vía construida con el tren en marcha (orden 106) y añadir una parada a una línea existente (107)
            {
                Red Montar(out int a, out int b, out int m)
                {
                    var r = Hito1(out a, out b); m = r.AddEstacion("Medio", 900_000, 1_150_000); r.Almacen[a][Red.Municion] = 12;
                    r.TrazarVia(new List<PointMm> { new PointMm(200_000, 1_000_000), new PointMm(900_000, 1_150_000) });       // tramo 0: A–Medio
                    r.TrazarVia(new List<PointMm> { new PointMm(900_000, 1_150_000), new PointMm(1_600_000, 1_300_000) });     // tramo 1: Medio–B
                    int tr = r.NuevoTren("Tren 1", a);
                    var cA = new int[Red.Recursos]; cA[Red.Municion] = 4; var dB = new int[Red.Recursos]; dB[Red.Municion] = -1;
                    int l = r.CrearLinea("L", new List<ParadaLinea> { new ParadaLinea { Estacion = a, Cargar = cA, Descargar = new int[Red.Recursos] }, new ParadaLinea { Estacion = b, Cargar = new int[Red.Recursos], Descargar = dB } });
                    r.AsignarTren(l, tr); return r;
                }
                var r = Montar(out int a, out int b, out int m); int tick = 0; var t0 = r.Trenes[0];
                check(r.Via.Tramos.Count == 2 && r.Via.Ruta(r.Via.Estaciones[a].Nodo, r.Via.Estaciones[b].Nodo)?.Count == 2, "red de prueba: A–Medio–B en dos tramos");
                while (tick < 20_000 && !(t0.Estado == EstadoTren.EnMarcha && t0.PasoI == 0 && t0.OffMm > 100_000)) r.Step(++tick);
                check(t0.Estado == EstadoTren.EnMarcha && t0.Ruta.Count == 2, $"el tren va de A a B por el tramo 0 (tick {tick})");
                bool bajo = r.BorrarVia(0);
                check(!bajo && r.UltimoRechazo.Contains("hay un tren") && !r.Via.Tramos[0].Borrado, $"no se puede borrar el tramo que tiene un tren encima ({r.UltimoRechazo})");
                check(r.BorrarVia(1) && r.Via.Tramos[1].Borrado && t0.Ruta.Count == 1, "se borra el tramo siguiente con el tren en marcha: su ruta se corta en el nodo Medio");
                check(!r.BorrarVia(1), "borrar dos veces el mismo tramo se rechaza");
                while (tick < 40_000 && t0.Estado == EstadoTren.EnMarcha) r.Step(++tick);
                for (int k = 0; k < 60; k++) r.Step(++tick);
                check(t0.Estado == EstadoTren.Parado && t0.Nodo == r.Via.Estaciones[m].Nodo && t0.Carga[Red.Municion] == 4, $"el tren para en Medio con su carga y espera vía ({r.UltimoRechazo})");
                var res = r.TrazarVia(new List<PointMm> { new PointMm(900_000, 1_150_000), new PointMm(1_600_000, 1_300_000) });
                for (int k = 0; k < 6_000; k++) r.Step(++tick);
                check(res.Ok && r.Almacen[b][Red.Municion] >= 4, $"con la vía nueva (tramo {res.Tramo}) se reencamina solo y entrega en B: {r.Almacen[b][Red.Municion]} cajas");
                var w = new SaveWriter(); r.Save(w); var r2 = new Red(); r2.Load(new SaveReader(w.ToArray()));
                check(r2.Hash() == r.Hash() && r2.Via.Tramos[1].Borrado && r2.Via.Ruta(r2.Via.Estaciones[m].Nodo, r2.Via.Estaciones[b].Nodo)[0].Tramo == res.Tramo, "guardar y cargar conserva el tramo borrado y la ruta nueva");
                // añadir parada: Medio como depósito entre A y B
                var q = Montar(out a, out b, out m); var dM = new int[Red.Recursos]; dM[Red.Municion] = -1;
                check(q.AnadirParada(0, 1, new ParadaLinea { Estacion = m, Cargar = new int[Red.Recursos], Descargar = dM }) && q.Lineas[0].Paradas.Count == 3, "se añade una parada (depósito en Medio) a la línea existente");
                for (int k = 1; k <= 9_000; k++) q.Step(k);
                check(q.Almacen[m][Red.Municion] >= 4 && q.Almacen[b][Red.Municion] == 0, $"el tren descarga en el depósito nuevo (Medio {q.Almacen[m][Red.Municion]}, B {q.Almacen[b][Red.Municion]})");
                check(!q.AnadirParada(5, 0, new ParadaLinea { Estacion = m }) && !q.AnadirParada(0, 0, new ParadaLinea { Estacion = 9 }), "una línea o estación que no existe se rechaza");
            }
            // en Mundo: 106 con el tren en marcha, dos instancias, mismo hash
            {
                TBD.Mundo.Mundo Mm()
                {
                    var mu = new TBD.Mundo.Mundo(7u, 2_000_000, 2_000_000);
                    mu.Red.AddEstacion("A", 200_000, 1_000_000); mu.Red.AddEstacion("B", 1_600_000, 1_300_000); mu.Red.AddEstacion("M", 900_000, 1_150_000); mu.Red.Almacen[0][0] = 12;
                    void Via(int tk, int x0, int y0, int x1, int y1) { mu.Encolar(new Orden { Tick = tk, Jugador = 1, Tipo = OrdenesRed.PuntoVia, A = x0, B = y0 }); mu.Encolar(new Orden { Tick = tk, Jugador = 1, Tipo = OrdenesRed.PuntoVia, A = x1, B = y1 }); mu.Encolar(new Orden { Tick = tk, Jugador = 1, Tipo = OrdenesRed.TrazarVia }); }
                    Via(10, 200_000, 1_000_000, 900_000, 1_150_000); Via(11, 900_000, 1_150_000, 1_600_000, 1_300_000);
                    mu.Encolar(new Orden { Tick = 12, Jugador = 2, Tipo = OrdenesRed.NuevoTren, A = 0 });
                    mu.Encolar(new Orden { Tick = 13, Jugador = 2, Tipo = OrdenesRed.NuevaLinea, A = 0, B = 1 });
                    mu.Encolar(new Orden { Tick = 14, Jugador = 1, Tipo = OrdenesRed.AsignarTren, A = 0, B = 0 });
                    mu.Encolar(new Orden { Tick = 2_400, Jugador = 2, Tipo = OrdenesRed.BorrarVia, A = 1 });                                  // con el tren en marcha por el tramo 0
                    mu.Encolar(new Orden { Tick = 2_500, Jugador = 1, Tipo = OrdenesRed.AnadirParada, A = 0, B = 2, C = -1, D = 0 });           // depósito en M, al final
                    Via(5_000, 900_000, 1_150_000, 1_600_000, 1_300_000);
                    return mu;
                }
                var u1 = Mm(); var u2 = Mm(); bool ig = true; int tb = -1;
                for (int k = 0; k < 30 * 60 * 10; k++) { u1.Step(); u2.Step(); if (tb < 0 && u1.Red.Via.Tramos.Count > 1 && u1.Red.Via.Tramos[1].Borrado) tb = k; if (u1.Hash() != u2.Hash()) { ig = false; break; } }
                check(ig && tb > 0 && u1.Red.Lineas[0].Paradas.Count == 3 && u1.Red.Via.Tramos.Count == 3, $"órdenes 106 y 107 en dos Mundos: mismo hash 10 minutos (tramo 1 borrado en el tick {tb}; tren {u1.Red.Trenes[0].Estado})");
            }
            // sin vía: el tren con línea espera y no se mueve
            {
                var r = Hito1(out int a, out int b); int tr = r.NuevoTren("2 Montaña", a);
                int l = r.CrearLinea("M", new List<ParadaLinea> { new ParadaLinea { Estacion = a }, new ParadaLinea { Estacion = b } });
                r.AsignarTren(l, tr); for (int tick = 1; tick <= 120; tick++) r.Step(tick);      // atiende la primera parada (3 s) y luego no encuentra vía hasta la segunda
                check(r.Trenes[tr].Estado == EstadoTren.Parado && r.UltimoRechazo != null && r.UltimoRechazo.Contains("no hay vía"), $"sin vía, el tren espera parado ({r.UltimoRechazo})");
                r.TrazarVia(Curva()); for (int tick = 121; tick <= 160; tick++) r.Step(tick);
                check(r.Trenes[tr].Estado != EstadoTren.Parado, "en cuanto se traza la vía, sale solo");
            }
        }
    }
}
