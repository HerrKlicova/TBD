using System;

namespace TBD.Net
{
    /// <summary>
    /// Regla de existencias de Hugo (docs/economia/escala-existencias.md, cerrada el 8-nov). Una sola unidad, la u. Hay dos almacenes por estación:
    /// - la reserva de red (NetWorld.Stock), que es lo que mueven los trenes;
    /// - el almacén de la zona, que es lo que se gasta en un ataque, con un tope por recurso (munición 47 u, combustible 30 u, materiales 40 u; las personas no
    ///   van al almacén de la zona).
    /// Al empezar la partida y al empezar cada ataque, de día o de noche, el almacén de la zona se llena con min(tope, reserva). Entre ataques, el jugador puede
    /// reponerlo a mano desde la reserva hasta el tope. Al acabar el ataque, lo que sobra vuelve a la reserva. De noche, a la reserva solo entra lo que llegue
    /// en tren. C# puro. Toca NetWorld.Stock: la vista lo aplica igual a los dos mundos (A y B) en el mismo fotograma, como el resto de cambios locales.
    /// La zona (Marcos) es la que gasta del almacén y llama a EmpiezaAtaque / AcabaAtaque.
    /// </summary>
    public sealed class AlmacenZona
    {
        public static readonly int[] Topes = { 47, 30, 40, 0 };           // u: munición, combustible, materiales, personas (Hugo)
        public readonly int Nodo;
        public readonly int[] Actual = new int[NetConst.ResCount];
        public bool EnAtaque { get; private set; }

        public AlmacenZona(int nodo) { Nodo = nodo; }

        /// <summary>Llena hasta el tope con lo que haya en la reserva. Devuelve las u movidas.</summary>
        public int Llenar(NetWorld w)
        {
            int n = 0;
            for (int r = 0; r < NetConst.ResCount; r++)
            {
                int quiere = Math.Max(0, Topes[r] - Actual[r]), m = Math.Min(quiere, Math.Max(0, w.Stock[Nodo, r]));
                w.Stock[Nodo, r] -= m; Actual[r] += m; n += m;
            }
            return n;
        }

        /// <summary>Al empezar la partida (una vez).</summary>
        public int EmpiezaPartida(NetWorld w) { return Llenar(w); }

        /// <summary>Al empezar un ataque, de día o de noche: se llena con min(tope, reserva).</summary>
        public int EmpiezaAtaque(NetWorld w) { EnAtaque = true; return Llenar(w); }

        /// <summary>Al acabar el ataque: lo que sobra vuelve a la reserva. Devuelve las u devueltas.</summary>
        public int AcabaAtaque(NetWorld w)
        {
            EnAtaque = false; int n = 0;
            for (int r = 0; r < NetConst.ResCount; r++) { w.Stock[Nodo, r] += Actual[r]; n += Actual[r]; Actual[r] = 0; }
            return n;
        }

        /// <summary>Entre ataques, el jugador repone a mano un recurso desde la reserva hasta el tope. Durante un ataque no se puede (devuelve 0).</summary>
        public int Reponer(NetWorld w, int recurso, int cuanto)
        {
            if (EnAtaque || recurso < 0 || recurso >= NetConst.ResCount || cuanto <= 0) return 0;
            int m = Math.Min(cuanto, Math.Min(Math.Max(0, Topes[recurso] - Actual[recurso]), Math.Max(0, w.Stock[Nodo, recurso])));
            w.Stock[Nodo, recurso] -= m; Actual[recurso] += m;
            return m;
        }

        /// <summary>La zona gasta del almacén (disparos, reparar…). Devuelve lo que pudo gastar.</summary>
        public int Gastar(int recurso, int cuanto)
        {
            if (recurso < 0 || recurso >= NetConst.ResCount || cuanto <= 0) return 0;
            int m = Math.Min(cuanto, Actual[recurso]); Actual[recurso] -= m; return m;
        }
    }
}
