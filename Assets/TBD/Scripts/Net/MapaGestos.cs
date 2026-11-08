using System;

namespace TBD.Net
{
    /// <summary>
    /// Gestos del mapa de la red (D-N170, deseo de Eugenio para después de la Noche58). C# puro, sin Unity: tiempos en segundos reales y posiciones en
    /// píxeles de pantalla. Doble clic: dos clics sobre el mismo objetivo en menos de Ventana s y a menos de Radio px. Seguimiento de un tren: la cámara va
    /// pegada a él hasta que el jugador mueva la cámara (teclas, rueda o arrastre), o hasta que el tren llegue y pare.
    /// </summary>
    public sealed class DobleClic
    {
        public const float Ventana = 0.4f, RadioPx = 12f;
        string _objetivo; float _t = -10f, _x, _y;

        /// <summary>Un clic sobre «objetivo» (p. ej. «nodo:3» o «tren:1») en el instante t. Devuelve true si es el segundo de un doble clic.</summary>
        public bool Clic(string objetivo, float t, float x, float y)
        {
            bool doble = objetivo != null && objetivo == _objetivo && t - _t <= Ventana && Math.Abs(x - _x) <= RadioPx && Math.Abs(y - _y) <= RadioPx;
            if (doble) { _objetivo = null; _t = -10f; return true; }      // un tercer clic empieza otro doble clic
            _objetivo = objetivo; _t = t; _x = x; _y = y;
            return false;
        }
    }

    public sealed class SeguirTren
    {
        public const float DistanciaKm = 1.2f;    // «la cámara lo sigue de cerca» (D-N170)
        // junto a una estación la cámara sube a SueltaKm: por debajo de 2 km sobre un nodo, el mapa entra en su zona (OperationsMap3D.EnterKm)
        public const float SueltaKm = 2.6f;
        public int Tren { get; private set; } = -1;
        public bool Activo { get { return Tren >= 0; } }
        public string PorQueSuelta { get; private set; }

        public void Empezar(int tren) { Tren = tren; PorQueSuelta = null; }

        /// <summary>
        /// Un fotograma: «movioCamara» = el jugador ha tocado la cámara; «enMarcha» = el tren sigue de viaje; «kmANodo» = distancia del tren a la estación más
        /// cercana. Devuelve true si la cámara debe seguir pegada al tren este fotograma.
        /// </summary>
        public bool Paso(bool movioCamara, bool enMarcha, double kmANodo)
        {
            if (!Activo) return false;
            if (movioCamara) return Soltar("el jugador movió la cámara");
            if (!enMarcha) return Soltar("el tren ha parado");
            return true;
        }

        /// <summary>Distancia de cámara al seguir: de cerca, pero sin bajar del umbral de entrada en una zona cuando el tren pasa junto a una estación.</summary>
        public static float Distancia(double kmANodo) { return kmANodo < SueltaKm ? SueltaKm : DistanciaKm; }

        bool Soltar(string porque) { Tren = -1; PorQueSuelta = porque; return false; }
    }
}
