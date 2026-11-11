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
                var cerrada = r.TrazarVia(new List<PointMm> { new PointMm(0, 0), new PointMm(40_000, 0), new PointMm(40_000, 40_000) });
                check(!cerrada.Ok && cerrada.Por.Contains("curva") && r.Via.Tramos.Count == 1, $"una curva de menos de 60 m se rechaza sin tocar nada ({cerrada.Por})");
                check(!r.TrazarVia(new List<PointMm> { new PointMm(200_000, 1_000_000), new PointMm(203_000, 1_002_000) }).Ok, "no se puede trazar de una estación a sí misma");
                check(r.Via.Ruta(r.Via.Estaciones[a].Nodo, r.Via.Estaciones[b].Nodo).Count == 1 && r.Via.Ruta(r.Via.Estaciones[b].Nodo, r.Via.Estaciones[a].Nodo)[0].Directo == false, "la ruta va y vuelve por el mismo tramo");
            }
            // tren solo: acelera, llega y para en el andén
            {
                var r = Hito1(out int a, out int b); r.TrazarVia(Curva());
                int tr = r.NuevoTren("1 Costa", a);
                int l = r.CrearLinea("Munición", new List<ParadaLinea> { new ParadaLinea { Estacion = a, Cargar = new[] { 10, 0, 0, 0 } }, new ParadaLinea { Estacion = b, Descargar = new[] { -1, 0, 0, 0 } } });
                r.Almacen[a][0] = 30;
                check(r.AsignarTren(l, tr), "el tren se asigna a la línea");
                int vmax = 0, tLlegada = -1, tVuelta = -1, vueltas = 0; bool enA = true;
                for (int tick = 1; tick <= 30 * 60 * 30 && vueltas < 3; tick++)
                {
                    r.Step(tick); var t = r.Trenes[tr];
                    if (t.VelMmT > vmax) vmax = t.VelMmT;
                    if (t.Estado == EstadoTren.EnParada && t.EsperaT == 1) { int est = r.Via.Nodos[t.Nodo].Estacion; if (est == b && enA) { if (tLlegada < 0) tLlegada = tick; enA = false; } else if (est == a && !enA) { if (tVuelta < 0) tVuelta = tick; enA = true; vueltas++; } }
                }
                check(vmax == r.Trenes[tr].VmaxMmT, $"el tren llega a su velocidad máxima ({vmax} mm/tick)");
                check(tLlegada > 0 && tLlegada < 30 * 300, $"llega a la segunda parada en {tLlegada / 30} s");
                check(vueltas == 3, $"la línea se repite sola: {vueltas} vueltas completas");
                check(r.Almacen[a][0] == 0 && r.Almacen[b][0] == 30 && r.Trenes[tr].Carga[0] == 0, $"lleva la munición: Sagunto {r.Almacen[a][0]} u, Caserío {r.Almacen[b][0]} u, a bordo {r.Trenes[tr].Carga[0]} u (10 u por viaje, 3 viajes)");
            }
            // determinismo: dos mundos iguales con las mismas órdenes; guardado a mitad de viaje
            {
                Red Montar() { var r = Hito1(out int a, out int b); r.TrazarVia(Curva()); int tr = r.NuevoTren("1 Costa", a); r.Almacen[a][0] = 50; int l = r.CrearLinea("M", new List<ParadaLinea> { new ParadaLinea { Estacion = a, Cargar = new[] { -1, 0, 0, 0 } }, new ParadaLinea { Estacion = b, Descargar = new[] { -1, 0, 0, 0 } } }); r.AsignarTren(l, tr); return r; }
                var r1 = Montar(); var r2 = Montar(); bool iguales = true;
                for (int tick = 1; tick <= 6000; tick++) { r1.Step(tick); r2.Step(tick); if (r1.Hash() != r2.Hash()) { iguales = false; break; } }
                check(iguales, "dos mundos con las mismas órdenes dan el mismo hash en cada tick (6000 ticks)");
                var w = new SaveWriter(); r1.Save(w); var r3 = new Red(); r3.Load(new SaveReader(w.ToArray()));
                check(r3.Hash() == r1.Hash(), "guardar y cargar a mitad de viaje da el mismo hash");
                for (int tick = 6001; tick <= 12000; tick++) { r1.Step(tick); r3.Step(tick); }
                check(r3.Hash() == r1.Hash() && r1.Almacen[1][0] == r3.Almacen[1][0], $"y sigue igual 6000 ticks después (Caserío: {r1.Almacen[1][0]} u en los dos)");
                check(r1.Trenes[0].Carga[0] <= r1.Trenes[0].Capacidad, "el tren no pasa de su capacidad (40 u)");
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
