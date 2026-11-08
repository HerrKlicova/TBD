using System;
using System.Collections.Generic;
using TBD.Units;

namespace TBD.Tutorial
{
    /// <summary>
    /// Para la tarjeta suelta del C90 (D-N163; regla de Lucía del 8-nov con la medida de Hugo y Nacho, docs/economia/entregas/c90/):
    /// salta la primera vez que hay MinGrupo zombis o más DENTRO del recinto a menos de Radio de un pelotón de fusileros que aún tenga C90.
    /// 40 = el tope de bajas de un tiro de C90; con 12–30 el aviso saltaba 11 veces por noche (con pila hay más de 200 en 5 m, sin pila menos de 20).
    /// Es la misma situación que la D-N148 (zombis dentro). «Dentro del recinto» lo responde la zona (dentroRecinto); null = cuenta todos.
    /// Solo lee: no toca la simulación. Devuelve el id del pelotón o −1. La tarjeta sale una sola vez en la partida y no sale si se saltó el tutorial (TutorialMotor).
    /// </summary>
    public static class C90Aviso
    {
        public const int MinGrupo = 40;           // Hugo (tope de bajas del C90)
        public const int RadioMm = 30_000;        // Lucía: a menos de 30 m del pelotón

        public static int Buscar(UnitsWorld uw, IList<Target> zombis, Func<int, int, bool> dentroRecinto = null, int minGrupo = MinGrupo)
        {
            if (uw == null || zombis == null || zombis.Count < minGrupo) return -1;
            long r2 = (long)RadioMm * RadioMm;
            foreach (var p in uw.Platoons)
            {
                if (p.Kind != PlatoonKind.Fusileros || p.C90Left <= 0 || p.Aboard != 0 || p.AliveCount() == 0) continue;
                int n = 0;
                for (int i = 0; i < zombis.Count; i++)
                {
                    long dx = zombis[i].X - p.AX, dy = zombis[i].Y - p.AY;
                    if (dx * dx + dy * dy > r2) continue;
                    if (dentroRecinto != null && !dentroRecinto(zombis[i].X, zombis[i].Y)) continue;
                    if (++n >= minGrupo) return p.Id;
                }
            }
            return -1;
        }
    }

    /// <summary>
    /// Las 4 señales de la zona para los capítulos 1 y 2 (riesgo del tablón; las pone Iker con el visto bueno de Lucía). Solo lee lo que la zona le pasa una vez por
    /// segundo y devuelve las señales que se cumplen ahora:
    /// - horda_avanza: la horda ya no está en espera y hay zombis vivos en la zona;
    /// - ataque_rechazado: después de eso, no queda ninguno (el ataque de día acabado);
    /// - vehiculos_fuera: hay algún vehículo en la zona, vivo y fuera del tren;
    /// - tramo_sobre_70: un tramo del muro que estaba por debajo del 70 % (el oeste al 45 % del día 1) vuelve a pasar del 70 %.
    /// </summary>
    public sealed class SenalesZona
    {
        public const int TramoPm = 700;
        bool _hordaVista;
        readonly HashSet<int> _bajos = new HashSet<int>();

        public List<string> Mirar(bool hordaEnEspera, int zombisVivos, int vehiculosFuera, IList<int> tramosPm)
        {
            var r = new List<string>();
            if (!hordaEnEspera && zombisVivos > 0) { _hordaVista = true; r.Add(TutorialGuion.Senales.HordaAvanza); }
            if (_hordaVista && zombisVivos == 0) r.Add(TutorialGuion.Senales.AtaqueRechazado);
            if (vehiculosFuera > 0) r.Add(TutorialGuion.Senales.VehiculosFuera);
            if (tramosPm != null)
                for (int i = 0; i < tramosPm.Count; i++)
                {
                    if (tramosPm[i] < TramoPm) _bajos.Add(i);
                    else if (_bajos.Contains(i)) { r.Add(TutorialGuion.Senales.TramoSobre70); break; }
                }
            return r;
        }

        public void Reiniciar() { _hordaVista = false; _bajos.Clear(); }
    }

    /// <summary>
    /// Recorrido del capítulo 3 en Sagunto (docs/niveles/tutorial-cap3-recorrido.md de Álvaro; Lucía, 8-nov). La infantería va en línea recta, y de la puerta este
    /// (192; 60) al flanco sur (100; 34) la recta cruza la esquina SE del fuerte (156; 44): por eso, una orden que sale del lado este y termina al sur del fuerte pasa
    /// antes por el paso fijo (160; 40) y luego sigue sola al destino. La retirada: del flanco sur a R2 (160; 38), que es la parada normal, con R1 (130; 36) como paso
    /// opcional y R3 (196; 51), el puesto F3 junto al tren, como refugio final. En mm de la zona.
    /// </summary>
    public sealed class RutaCap3
    {
        public const int PasoX = 160_000, PasoY = 40_000, EsquinaX = 156_000, EsquinaY = 44_000, LlegadaMm = 4_000;
        public static readonly int[] FlancoSur = { 100_000, 34_000 }, R1 = { 130_000, 36_000 }, R2 = { 160_000, 38_000 }, R3 = { 196_000, 51_000 };
        struct Pendiente { public int X, Y, Rumbo; public bool Ataque; }
        readonly Dictionary<int, Pendiente> _pend = new Dictionary<int, Pendiente>();

        /// <summary>¿Hay que rodear la esquina SE? (sale al este del fuerte y termina al oeste de la esquina y al sur del muro sur)</summary>
        public static bool Rodea(int sx, int sy, int tx, int ty) { return sx > EsquinaX && tx < EsquinaX && ty < EsquinaY; }

        /// <summary>Antes de dar la orden: si hay que rodear, cambia el destino por el paso fijo y guarda el de verdad. true si lo ha cambiado.</summary>
        public bool Ajustar(int peloton, int sx, int sy, ref int tx, ref int ty, int rumbo, bool ataque)
        {
            _pend.Remove(peloton);
            if (!Rodea(sx, sy, tx, ty)) return false;
            _pend[peloton] = new Pendiente { X = tx, Y = ty, Rumbo = rumbo, Ataque = ataque };
            tx = PasoX; ty = PasoY; return true;
        }

        /// <summary>Cada tick: si el pelotón ya está en el paso, devuelve la orden que le falta (al destino de verdad).</summary>
        public bool Seguir(int peloton, int px, int py, out int tx, out int ty, out int rumbo, out bool ataque)
        {
            tx = ty = rumbo = 0; ataque = false;
            if (!_pend.TryGetValue(peloton, out var p)) return false;
            long dx = px - PasoX, dy = py - PasoY; if (dx * dx + dy * dy > (long)LlegadaMm * LlegadaMm) return false;
            _pend.Remove(peloton); tx = p.X; ty = p.Y; rumbo = p.Rumbo; ataque = p.Ataque; return true;
        }

        public int Pendientes { get { return _pend.Count; } }
        public IEnumerable<int> Ids { get { return new List<int>(_pend.Keys); } }
    }
}
