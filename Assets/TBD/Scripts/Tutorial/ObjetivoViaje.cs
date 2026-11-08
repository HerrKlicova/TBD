namespace TBD.Tutorial
{
    /// <summary>
    /// Objetivo de campaña de después de la primera noche (D-N172 ajustada; el viaje con carga de la D-N154): «Lleva munición a Nules». Textos de Lucía
    /// (tutorial-textos.md, «Objetivo después de la primera noche»). Sin tarjetas de tutorial: una línea de objetivo con el paso actual. Sale también si se
    /// saltó el tutorial; en difícil, no. C# puro: lo avanzan el panel del tren y las llegadas (TrenPanel).
    /// </summary>
    public enum PasoViaje : byte { Inactivo, Cargar, Escolta, Destino, EnCamino, Descargar, Volver, Cumplido }

    public sealed class ObjetivoViaje
    {
        public const string Titulo = "Lleva munición a Nules";
        public PasoViaje Paso { get; private set; } = PasoViaje.Inactivo;
        int _tren = -1;

        /// <summary>Texto del paso. «sitiada»: Nules está sitiada ahora (el paso de descargar pasa al 5b).</summary>
        public string Texto(bool nulesSitiada = false)
        {
            switch (Paso)
            {
                case PasoViaje.Cargar: return "Carga munición en el tren de Sagunto: abre Preparar tren.";
                case PasoViaje.Escolta: return "Sube tropa o un vehículo en plataforma: protegerán la carga.";
                case PasoViaje.Destino: return "Destino: Nules. Mira el ETA y envíalo.";
                case PasoViaje.EnCamino: return "El tren va hacia Nules. Llega en {eta}.";
                case PasoViaje.Descargar: return nulesSitiada ? "Nules está sitiada: la carga se queda a bordo hasta que se despeje." : "Tren en Nules. Descarga la munición.";
                case PasoViaje.Volver: return "Devuelve el tren a Sagunto.";
                case PasoViaje.Cumplido: return "Cumplido. Nules tiene munición para aguantar.";
                default: return null;
            }
        }

        public static bool HayObjetivo(int dificultadPermil) { return dificultadPermil < TutorialMotor.DificilDesde; }

        /// <summary>Al amanecer, después de la primera noche (fácil y normal).</summary>
        public void Empezar() { if (Paso == PasoViaje.Inactivo) Paso = PasoViaje.Cargar; }

        /// <summary>En «Preparar tren» de Sagunto se ha puesto munición (solo munición cierra el paso 1).</summary>
        public void MunicionPuesta() { if (Paso == PasoViaje.Cargar) Paso = PasoViaje.Escolta; }

        /// <summary>Tropa o un vehículo a bordo del tren que se prepara (paso 2, opcional).</summary>
        public void EscoltaPuesta() { if (Paso == PasoViaje.Escolta) Paso = PasoViaje.Destino; }

        /// <summary>Destino Nules puesto en el panel (paso 3). Si aún estaba en la escolta, se la salta (es opcional).</summary>
        public void DestinoNules() { if (Paso == PasoViaje.Escolta || Paso == PasoViaje.Destino) Paso = PasoViaje.Destino; }

        /// <summary>Sale un tren. Cuenta si va de Sagunto a Nules con munición; la escolta y el destino del panel se dan por hechos.</summary>
        public void Enviado(int tren, string origen, string destino, int municion)
        {
            if (Paso >= PasoViaje.Cargar && Paso <= PasoViaje.Destino && origen == "Sagunto" && destino == "Nules" && municion > 0) { _tren = tren; Paso = PasoViaje.EnCamino; }
        }

        public void Llegado(int tren, string estacion)
        {
            if (tren != _tren) return;
            if (Paso == PasoViaje.EnCamino && estacion == "Nules") Paso = PasoViaje.Descargar;
            else if (Paso == PasoViaje.Volver && estacion == "Sagunto") Paso = PasoViaje.Cumplido;
        }

        /// <summary>La munición baja en Nules (CmdCargo). Con Nules sitiada no se llama: la carga se queda a bordo (D-N166).</summary>
        public void Descargado(int tren, string estacion) { if (Paso == PasoViaje.Descargar && tren == _tren && estacion == "Nules") Paso = PasoViaje.Volver; }
    }

    public enum EstadoDefensa : byte { Inactivo, Activo, Cumplido, Caducado, Fallido }

    /// <summary>
    /// D-N179 (Lucía, 8-nov): al amanecer del día 2 sale «Defiende Nules antes de la noche: manda tropa y munición (≥20 u)». Se suma al aviso de las 19:00
    /// (la horda de zona de Nacho). Se cumple con Nules con 20 u de munición o más y tropa del jugador bajada allí (NetWorld.ZoneDefended); caduca a las 22:00,
    /// cuando empieza el asedio, y falla si Nules se pierde (el parte ZS-2 dice qué faltó). Solo lee: no toca la simulación.
    /// </summary>
    public sealed class ObjetivoDefensa
    {
        public const string Titulo = "Defiende Nules antes de la noche";
        public const int MunicionMin = 20;
        public EstadoDefensa Estado { get; private set; }
        public static long Desde { get { return TBD.Net.NetConst.DayTicks + TBD.Net.NetConst.Hours(6); } }     // amanecer del día 2
        public static long Hasta { get { return TBD.Net.NetConst.DayTicks + TBD.Net.NetConst.Hours(22); } }    // empieza el asedio de la 2.ª noche

        public void Actualizar(long tick, int municionNules, bool tropaNules, bool perdida)
        {
            if (Estado == EstadoDefensa.Inactivo && tick >= Desde && tick < Hasta) Estado = EstadoDefensa.Activo;
            if (Estado != EstadoDefensa.Activo) return;
            if (perdida) Estado = EstadoDefensa.Fallido;
            else if (municionNules >= MunicionMin && tropaNules) Estado = EstadoDefensa.Cumplido;
            else if (tick >= Hasta) Estado = EstadoDefensa.Caducado;
        }

        public static string Texto(int municionNules, bool tropaNules)
        {
            return "manda tropa y munición (≥" + MunicionMin + " u) · munición " + System.Math.Min(municionNules, 999) + "/" + MunicionMin + " · tropa " + (tropaNules ? "sí" : "no");
        }
    }
}
