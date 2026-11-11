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
        public const TipoOrden CancelarVia = (TipoOrden)102;    // borra los puntos del jugador
        public const TipoOrden NuevaLinea = (TipoOrden)103;     // A = estación de la munición, B = estación del frente; C = cajas de munición por viaje (0 = 4, Hugo), D = palés de material de vuelta (0 = 2)
        public const TipoOrden AsignarTren = (TipoOrden)104;    // A = línea, B = tren
        public const TipoOrden NuevoTren = (TipoOrden)105;      // A = estación
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
                        int cajas = o.C > 0 ? o.C : 4, pales = o.D > 0 ? o.D : 2;      // Hugo (cifras-hito1): 4 cajas a B y vuelve con el material que haya (2 huecos)
                        var cA = new int[Red.Recursos]; cA[Red.Municion] = cajas; var dA = new int[Red.Recursos]; dA[Red.Material] = -1;
                        var cB = new int[Red.Recursos]; cB[Red.Material] = pales; var dB = new int[Red.Recursos]; dB[Red.Municion] = -1;
                        Red.CrearLinea("Línea " + (Red.Lineas.Count + 1), new List<ParadaLinea> { new ParadaLinea { Estacion = o.A, Cargar = cA, Descargar = dA }, new ParadaLinea { Estacion = o.B, Cargar = cB, Descargar = dB } });
                        break;
                    }
                case OrdenesRed.AsignarTren: Red.AsignarTren(o.A, o.B); break;
                case OrdenesRed.NuevoTren: if (o.A >= 0 && o.A < Red.Via.Estaciones.Count) Red.NuevoTren("Tren " + (Red.Trenes.Count + 1), o.A); break;
            }
        }

        /// <summary>La red del Hito 1 en el escenario del caserío: estaciones A y B de Mundo.Puntos, carreteras de Álvaro (Lector, carreteras_hito1.csv),
        /// «Tren 1» en A y 20 cajas de munición en A. Determinista: la llama el escenario (Mundo.Caserio) igual en los dos jugadores. Sin A o B, no hace nada.</summary>
        public void MontarRedHito1()
        {
            if (Red.Via.Estaciones.Count > 0 || !Puntos.TryGetValue("A", out var a) || !Puntos.TryGetValue("B", out var b)) return;
            int ea = Red.AddEstacion("A", a.X, a.Y); Red.AddEstacion("B", b.X, b.Y);
            var csv = Lector != null ? Lector("carreteras_hito1.csv") : null; if (csv != null) Red.Via.CargarCarreterasCsv(csv);
            Red.NuevoTren("Tren 1", ea); Red.Almacen[ea][Red.Municion] = 20;
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
