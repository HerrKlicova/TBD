using System;
using System.Collections.Generic;

namespace TBD.Tutorial
{
    /// <summary>
    /// Intro con el lore (D-N158), variante C «Sin fecha» elegida por Mak el 8-nov (docs/intro-variantes.md). C# puro: textos, tiempos y sonidos.
    /// Cada tarjeta se escribe letra a letra (Maquina, petición de Eugenio), se queda Pausa segundos entera y se funde. Clic o Espacio: si la tarjeta
    /// está a medias, se completa; si ya está entera, pasa a la siguiente (Siguiente). Esc salta la intro entera (Saltar). En total, menos de 40 s.
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
        public readonly float Pausa, Fundido;
        public int Indice { get; private set; }
        public Maquina Maquina { get; private set; }
        public float T { get; private set; }          // segundos desde el principio
        float _entera;                                  // segundos que lleva la tarjeta entera
        public bool Saltada { get; private set; }

        public IntroSecuencia(string[] tarjetas = null, float pausa = 2.6f, float fundido = 0.5f)
        {
            Tarjetas = tarjetas ?? VarianteC; Pausa = pausa; Fundido = fundido;
            Maquina = new Maquina(Tarjetas[0], 41);
        }

        public bool Acabada { get { return Saltada || Indice >= Tarjetas.Length; } }
        public string Texto { get { return Acabada ? null : Maquina.Parcial; } }
        public string TextoEntero { get { return Acabada ? null : Tarjetas[Indice]; } }
        /// <summary>Opacidad: entera mientras se escribe y se lee; se funde al final de cada tarjeta.</summary>
        public float Alfa { get { if (Acabada) return 0f; float f = _entera - Pausa; return f <= 0 ? 1f : Math.Max(0f, 1f - f / Fundido); } }

        public void Avanzar(float dt, List<string> sonidos = null)
        {
            if (Acabada) return;
            dt = Math.Max(0f, dt); T += dt;
            Maquina.Avanzar(dt, sonidos);
            if (Maquina.Completa) { _entera += dt; if (_entera >= Pausa + Fundido) Pasar(); }
        }

        /// <summary>Clic o Espacio.</summary>
        public void Siguiente(List<string> sonidos = null)
        {
            if (Acabada) return;
            if (!Maquina.Completa) Maquina.Completar(sonidos); else Pasar();
        }

        void Pasar()
        {
            Indice++; _entera = 0f;
            if (!Acabada) Maquina = new Maquina(Tarjetas[Indice], (uint)(41 + Indice));
        }

        public void Saltar() { Saltada = true; }
    }
}
