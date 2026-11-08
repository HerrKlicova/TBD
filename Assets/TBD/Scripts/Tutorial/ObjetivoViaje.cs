namespace TBD.Tutorial
{
    /// <summary>
    /// Objetivo de después de la primera noche (D-N172 ajustada; es el viaje con carga de la D-N154): «Viaje de carga a Nules». Sin tarjetas de tutorial:
    /// una línea de objetivo con el paso actual. C# puro: avanza con lo que pasa en la red (lo cuenta TrenPanel).
    /// Pasos: cargar en Sagunto y mandar el tren a Nules → llega → descargar (CmdCargo de Nacho) → volver a Sagunto → cumplido.
    /// TEXTOS EN BORRADOR, para que los revise Lucía.
    /// </summary>
    public enum PasoViaje : byte { Inactivo, Cargar, EnCamino, Descargar, Volver, Cumplido }

    public sealed class ObjetivoViaje
    {
        public const string Titulo = "Viaje de carga a Nules";
        public PasoViaje Paso { get; private set; } = PasoViaje.Inactivo;
        int _tren = -1;

        public string Texto
        {
            get
            {
                switch (Paso)
                {
                    case PasoViaje.Cargar: return "Carga el tren en Sagunto y mándalo a Nules.";
                    case PasoViaje.EnCamino: return "El tren va hacia Nules. Llega en {eta}.";
                    case PasoViaje.Descargar: return "El tren está en Nules. Descarga la carga.";
                    case PasoViaje.Volver: return "Devuelve el tren a Sagunto.";
                    case PasoViaje.Cumplido: return "Cumplido: Nules tiene con qué aguantar.";
                    default: return null;
                }
            }
        }

        /// <summary>Al amanecer después de la primera noche, con el tutorial acabado.</summary>
        public void Empezar() { if (Paso == PasoViaje.Inactivo) Paso = PasoViaje.Cargar; }

        /// <summary>Un tren sale de «origen» hacia «destino» con «carga» u (la suma de los 4 recursos).</summary>
        public void Enviado(int tren, string origen, string destino, int carga)
        {
            if (Paso == PasoViaje.Cargar && origen == "Sagunto" && destino == "Nules" && carga > 0) { _tren = tren; Paso = PasoViaje.EnCamino; }
            // en «Volver» se queda hasta que el tren llegue a Sagunto (Llegado)
        }

        /// <summary>El tren llega a una estación.</summary>
        public void Llegado(int tren, string estacion)
        {
            if (tren != _tren) return;
            if (Paso == PasoViaje.EnCamino && estacion == "Nules") Paso = PasoViaje.Descargar;
            else if (Paso == PasoViaje.Volver && estacion == "Sagunto") Paso = PasoViaje.Cumplido;
        }

        /// <summary>Se descarga la carga del tren en una estación (CmdCargo con cantidades negativas).</summary>
        public void Descargado(int tren, string estacion) { if (Paso == PasoViaje.Descargar && tren == _tren && estacion == "Nules") Paso = PasoViaje.Volver; }
    }
}
