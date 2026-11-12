using System.Collections.Generic;
using TBD.Units;

namespace TBD.Mundo
{
    /// <summary>Órdenes de la red (100 o más). Una vía se traza con varios RedPuntoVia (A = x, B = y, en mm) y un RedTrazarVia que la cierra; los puntos son de cada jugador
    /// (en coop cada uno traza la suya a la vez sin mezclarse).</summary>
    public static class OrdenesRed
    {
        public const TipoOrden PuntoVia = (TipoOrden)100;       // A = x, B = y
        public const TipoOrden TrazarVia = (TipoOrden)101;      // cierra los puntos del jugador en un tramo
        public const TipoOrden CancelarVia = (TipoOrden)102;    // borra los puntos pendientes del jugador (no toca vía construida: eso es BorrarVia, 106)
        public const TipoOrden NuevaLinea = (TipoOrden)103;     // A = estación de la munición, B = estación del frente; C = cajas de munición por viaje (0 = 4, Hugo), D = palés de material de vuelta (0 = 2)
        public const TipoOrden AsignarTren = (TipoOrden)104;    // A = línea, B = tren
        public const TipoOrden NuevoTren = (TipoOrden)105;      // A = estación
        public const TipoOrden BorrarVia = (TipoOrden)106;      // A = tramo. Con un tren encima se rechaza; los que lo iban a cruzar paran en el nodo siguiente y se reencaminan
        public const TipoOrden AnadirParada = (TipoOrden)107;   // A = línea, B = estación, C = posición (−1 = al final), D = cajas de munición a cargar (0 = depósito: descarga toda la munición)
    }

    /// <summary>La red dentro de LA simulación (contrato de Nacho): Mundo.Red, con sus órdenes, su paso y su hash por sistema.</summary>
    public sealed partial class Mundo
    {
        public readonly Red Red = new Red();
        readonly SortedDictionary<int, List<PointMm>> _trazos = new SortedDictionary<int, List<PointMm>>();      // puntos de vía pendientes, por jugador

        partial void AplicarRed(Orden o)
        {
            switch (o.Tipo)
            {
                case OrdenesRed.PuntoVia: { if (!_trazos.TryGetValue(o.Jugador, out var l)) _trazos[o.Jugador] = l = new List<PointMm>(); if (l.Count < 64) l.Add(new PointMm(o.A, o.B)); break; }
                case OrdenesRed.TrazarVia: { if (_trazos.TryGetValue(o.Jugador, out var l)) { Red.TrazarVia(l); _trazos.Remove(o.Jugador); } break; }
                case OrdenesRed.CancelarVia: _trazos.Remove(o.Jugador); break;
                case OrdenesRed.NuevaLinea:
                    {
                        if (o.A < 0 || o.A >= Red.Via.Estaciones.Count || o.B < 0 || o.B >= Red.Via.Estaciones.Count || o.A == o.B) { Red.UltimoRechazo = "línea no válida"; break; }
                        Red.CrearLineaPorDefecto(o.A, o.B, o.C > 0 ? o.C : 4, o.D > 0 ? o.D : 2);      // Hugo (cifras-hito1): 4 cajas a B y vuelve con el material que haya (2 huecos)
                        break;
                    }
                case OrdenesRed.AsignarTren: Red.AsignarTren(o.A, o.B); break;
                case OrdenesRed.BorrarVia: Red.BorrarVia(o.A); break;
                case OrdenesRed.AnadirParada:
                    {
                        var c = new int[Red.Recursos]; var d = new int[Red.Recursos];
                        if (o.D > 0) { c[Red.Municion] = o.D; d[Red.Material] = -1; } else d[Red.Municion] = -1;      // carga munición (y deja el material) o depósito del frente
                        Red.AnadirParada(o.A, o.C, new ParadaLinea { Estacion = o.B, Cargar = c, Descargar = d });
                        break;
                    }
                case OrdenesRed.NuevoTren: if (o.A >= 0 && o.A < Red.Via.Estaciones.Count) Red.NuevoTren("Tren " + (Red.Trenes.Count + 1), o.A); break;
            }
        }

        /// <summary>La red del Hito 1 en el escenario del caserío: estaciones A y B de Mundo.Puntos, carreteras de Álvaro (Lector, carreteras_hito1.csv),
        /// «Tren 1» en A, 20 cajas de munición y 19 palés de material en A (la vía cuesta material). Determinista: la llama el escenario (Mundo.Caserio) igual en los dos jugadores. Sin A o B, no hace nada.</summary>
        public void MontarRedHito1()
        {
            if (Red.Via.Estaciones.Count > 0 || !Puntos.TryGetValue("A", out var a) || !Puntos.TryGetValue("B", out var b)) return;
            int ea = Red.AddEstacion("A", a.X, a.Y); Red.AddEstacion("B", b.X, b.Y);
            var csv = Lector != null ? Lector("carreteras_hito1.csv") : null; if (csv != null) Red.Via.CargarCarreterasCsv(csv);
            var mar = Lector != null ? Lector("agua_hito1.csv") : null; if (mar != null) Red.Via.CargarMarCsv(mar);
            Red.NuevoTren("Tren 1", ea); Red.Almacen[ea][Red.Municion] = 20;
            Red.LineaAuto = true;
            Red.Almacen[ea][Red.Material] = 19; Red.CuestaMaterial = true;      // Hugo (12-nov): A = coste del guion A–P–B (17,04, RedTests) + 2, sin redondear hacia arriba → 19. Si cambia la vía del guion, sigue siendo su coste + 2      // la línea A–B por defecto la crea la simulación al unirse A y B (antes la pedía la vista, en un tick que dependía de los fotogramas)
        }

        partial void StepRed() { Red.Step(Tick); }

        partial void HashRedPorSistema(List<KeyValuePair<string, ulong>> l)
        {
            Red.HashPorSistema(l);
            ulong h = 0x54UL; foreach (var kv in _trazos) { h = IMath.Mix(h ^ (ulong)kv.Key); foreach (var p in kv.Value) h = IMath.Mix(h ^ (ulong)(uint)p.X ^ ((ulong)(uint)p.Y << 32)); }
            l.Add(new KeyValuePair<string, ulong>("red.trazos", h));
        }
    }
}
