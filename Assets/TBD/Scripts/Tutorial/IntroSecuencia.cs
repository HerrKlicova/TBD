using System;

namespace TBD.Tutorial
{
    /// <summary>
    /// Intro con el lore (D-N158), variante C «Sin fecha» elegida por Mak el 8-nov (docs/intro-variantes.md). C# puro: solo tiempos y textos.
    /// 5 tarjetas de unos 6 s, con fundido de entrada y de salida; se salta con un clic o con la barra espaciadora (Saltar). En total, 30 s.
    /// </summary>
    public sealed class IntroSecuencia
    {
        public static readonly string[] VarianteC =
        {
            "Día 41 desde el brote.",
            "Ya nadie dice el año. Las ciudades cayeron en dos semanas; las carreteras, en tres.",
            "Lo único que todavía se mueve es el tren.",
            "Desde Sagunto, el Mando Ferroviario de Emergencia reúne lo que queda: tropa, vagones y vía.",
            "Reparar la red es recuperar el país. Tramo a tramo.",
        };
        public const string ClaveVista = "intro.vista";

        public readonly string[] Tarjetas;
        public readonly float PorTarjeta, Fundido;
        public float T { get; private set; }
        public bool Saltada { get; private set; }

        public IntroSecuencia(string[] tarjetas = null, float porTarjeta = 6f, float fundido = 0.8f)
        {
            Tarjetas = tarjetas ?? VarianteC; PorTarjeta = porTarjeta; Fundido = Math.Min(fundido, porTarjeta * 0.5f);
        }

        public float Duracion { get { return Tarjetas.Length * PorTarjeta; } }
        public bool Acabada { get { return Saltada || T >= Duracion; } }
        public int Indice { get { return Acabada ? -1 : Math.Min(Tarjetas.Length - 1, (int)(T / PorTarjeta)); } }
        public string Texto { get { int i = Indice; return i < 0 ? null : Tarjetas[i]; } }
        /// <summary>Opacidad del texto (0..1): sube y baja en cada tarjeta.</summary>
        public float Alfa
        {
            get
            {
                if (Acabada) return 0f;
                float t = T - Indice * PorTarjeta;
                return Math.Max(0f, Math.Min(1f, Math.Min(t / Fundido, (PorTarjeta - t) / Fundido)));
            }
        }

        public void Avanzar(float dt) { if (!Acabada) T += Math.Max(0f, dt); }
        public void Saltar() { Saltada = true; }
    }
}
