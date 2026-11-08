using System;

namespace TBD.Net
{
    /// <summary>
    /// Sonidos del NH90 y de la base de Bétera (D-N173; plan de Nuria, docs/sonido/nh90-betera/encargo.md). C# puro: qué clip toca en cada fase del vuelo
    /// local (VueloLocal) y el fundido día/noche del ambiente. Cada clip tiene variantes _1 a _5, elegidas con una semilla. Los OGG llegan después.
    /// - Una vez: arranque (turbinas y rotor al ralentí), despegue, paso (pasada cercana, Doppler horneado: dopplerLevel = 0, que ya pone Sfx), toma.
    /// - Bucles: ralentí en tierra; ambiente de día y de noche (LoopAt en el centro de la base, radio 120 m; se funden con el reloj).
    /// - M3M: ráfaga en la puerta, con al menos 0,15 s entre ráfagas. El vuelo sostenido sigue con sfx_nh90_rotor_1..6 (no es de este plan).
    /// </summary>
    public static class SonidoNH90
    {
        public const string Arranque = "sfx_nh90_arranque", Despegue = "sfx_nh90_despegue", Paso = "sfx_nh90_paso", Toma = "sfx_nh90_toma", Ralenti = "sfx_nh90_ralenti",
            M3M = "sfx_m3m_rafaga", AmbDia = "sfx_amb_betera_dia", AmbNoche = "sfx_amb_betera_noche";
        public const float DistanciaMax = 300f, RadioAmbiente = 120f, M3MMinS = 0.15f;

        public static string Variante(string clip, uint semilla) { uint h = semilla == 0 ? 1u : semilla; h ^= h << 13; h ^= h >> 17; h ^= h << 5; return clip + "_" + (1 + h % 5); }

        /// <summary>
        /// Clip de una vez que empieza en el instante t del despegue (VueloLocal), o null. t0 y t1: el fotograma (t0 &lt; instante ≤ t1).
        /// Arranque al dar la orden (t = 0); despegue al empezar a subir (t = RotorS).
        /// </summary>
        public static string EnDespegue(float t0, float t1, uint semilla)
        {
            if (t0 < 0f && t1 >= 0f) return Variante(Arranque, semilla);
            if (t0 < VueloLocal.RotorS && t1 >= VueloLocal.RotorS) return Variante(Despegue, semilla + 1);
            return null;
        }

        /// <summary>Clip de la toma: empieza cuando faltan 12 s de vuelo más los 8 s de bajada y los 12 s de rotor (lo que dura el clip, unos 12 s, cubre la llegada).</summary>
        public static string EnToma(float faltan0, float faltan1, uint semilla)
        {
            float at = VueloLocal.RotorS + VueloLocal.SubidaS;
            return faltan0 > at && faltan1 <= at ? Variante(Toma, semilla) : null;
        }

        /// <summary>¿Suena el bucle de ralentí? En tierra con el rotor girando: durante los 12 s de rotor del despegue o de la toma.</summary>
        public static bool RalentiDespegue(float t) { return t >= 0f && t < VueloLocal.RotorS; }

        /// <summary>Peso del ambiente de noche (0 = día, 1 = noche) según la hora del día en horas de juego: de noche de 21 a 6, con fundidos de una hora.</summary>
        public static float PesoNoche(double hora)
        {
            hora = ((hora % 24) + 24) % 24;
            if (hora >= 21 || hora < 6) return hora >= 21 && hora < 22 ? (float)(hora - 21) : hora >= 5 && hora < 6 ? (float)(6 - hora) : 1f;
            return 0f;
        }

        /// <summary>¿Puede sonar otra ráfaga de M3M? Al menos 0,15 s desde la anterior.</summary>
        public static bool PuedeRafaga(float ahora, float ultima) { return ahora - ultima >= M3MMinS; }
    }
}
