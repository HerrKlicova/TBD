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
                int vuelta = tV2 - tV1; check(vuelta > 30 * 360 && vuelta < 30 * 480, $"una vuelta tarda {vuelta / 30} s (Hugo: ~405 s con 1250 m; esta vía mide 1502 m)");
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
