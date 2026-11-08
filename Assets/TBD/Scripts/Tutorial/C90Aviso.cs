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
}
