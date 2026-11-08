using System;
using System.Collections.Generic;

namespace TBD.Tutorial
{
    /// <summary>
    /// Motor del tutorial (D-N154, D-N163; textos de Lucía en docs/tutorial-textos.md). C# puro: no sabe nada de Unity, de la zona ni del mapa.
    /// Quien juega le manda señales con nombre («mapa_abierto», «tren_enviado»…) y el motor decide qué tarjeta toca, qué se resalta y cuándo se cierra.
    /// Reglas comunes: una tarjeta cada vez, con «Capítulo N · Paso x de y»; se salta desde la primera tarjeta o desde la pausa; solo la primera vez y
    /// solo en fácil y normal; si el jugador se desvía, la tarjeta repite su texto, sin castigo. El reloj del motor es tiempo real (no el del juego).
    /// </summary>
    public enum Cierre { Senal, Segundos }

    public sealed class Paso
    {
        public string Id;                  // «3.4»
        public string Texto;
        public string[] Resalta = new string[0];   // claves de lo que late con el anillo (las registra la vista)
        public Cierre Cierre;
        public string[] Senales = new string[0];   // con Cierre.Senal: basta una de ellas, recibida DESPUÉS de que salga la tarjeta
        public bool Todas;                 // con Cierre.Senal: hacen falta todas (en cualquier orden), no una (1.1: cámara movida y rueda)
        public float Segundos;             // con Cierre.Segundos; con Cierre.Senal, si > 0, tope para cerrarse sola
        public string Espera;              // si no es null, la tarjeta no sale hasta que haya llegado esta señal (en cualquier momento del capítulo)
        public string ExtraSi, Extra;      // si ha llegado la señal ExtraSi en el capítulo, se añade Extra al texto (1.3: vehículos fuera)
        public bool Pausa;                 // con la tarjeta fuera, el juego se pausa (solo la del capítulo 0)
        public bool Linea;                 // una sola línea, sin cabecera (1.9, mientras dura el ataque)
        public string[] Botones = new string[0];   // botones de la tarjeta; cada uno manda la señal «boton:<texto>»
    }

    public sealed class Capitulo
    {
        public int Numero;
        public string Titulo;
        public bool Combate;               // la horda espera agrupada mientras dura (lo aplica la zona: HordaEnEspera)
        public Paso[] Pasos;
    }

    /// <summary>Tarjeta suelta: sale una vez, la primera vez que llega su señal (el C90). No es un paso de ningún capítulo.</summary>
    public sealed class Suelta
    {
        public string Id, Texto, Disparo;
        public string[] Cierra = new string[0];
        public float Segundos = 8f;
    }

    /// <summary>Lo que se recuerda entre partidas (en Unity, PlayerPrefs; en las pruebas, un diccionario).</summary>
    public interface IMemoria { bool Leer(string clave); void Escribir(string clave); }

    public sealed class MemoriaEnRam : IMemoria
    {
        public readonly HashSet<string> Claves = new HashSet<string>();
        public bool Leer(string c) { return Claves.Contains(c); }
        public void Escribir(string c) { Claves.Add(c); }
    }

    public enum EstadoTutorial { Apagado, Activo, Saltado, Terminado }

    public sealed class TutorialMotor
    {
        public const string ClaveVisto = "tutorial.visto";
        public const string BotonSaltar = "Saltar tutorial";
        public const float RepetirCada = 25f;      // sin avanzar en una tarjeta de señal, se repite su texto (desvío sin castigo)
        public const int DificilDesde = 951;       // palanca de dificultad (D-N74): 725 fácil, 850 normal, 1000 difícil; la difícil no tiene tutorial

        readonly Capitulo[] _caps;
        readonly Suelta[] _sueltas;
        readonly IMemoria _mem;
        readonly HashSet<string> _hechos = new HashSet<string>();     // señales recibidas en el capítulo actual
        readonly HashSet<string> _enPaso = new HashSet<string>();     // señales recibidas con la tarjeta actual fuera
        public EstadoTutorial Estado { get; private set; }
        public int CapIndex { get; private set; } = -1;
        public int PasoIndex { get; private set; } = -1;
        public float EnPaso { get; private set; }                     // segundos desde que salió la tarjeta actual (0 mientras espera)
        public int Repeticiones { get; private set; }                 // cuántas veces se ha repetido la tarjeta actual: la vista hace latir el texto
        float _sinAvance;
        public Suelta SueltaActual { get; private set; }
        float _enSuelta;
        public readonly List<string> Registro = new List<string>();   // para los registros de las corridas automáticas (Sara)

        public TutorialMotor(Capitulo[] caps, Suelta[] sueltas, IMemoria mem) { _caps = caps; _sueltas = sueltas ?? new Suelta[0]; _mem = mem; }

        public static bool HayTutorial(int dificultadPermil) { return dificultadPermil < DificilDesde; }

        public Capitulo Cap { get { return CapIndex >= 0 && CapIndex < _caps.Length ? _caps[CapIndex] : null; } }
        public Paso Actual { get { var c = Cap; return Estado == EstadoTutorial.Activo && c != null && PasoIndex >= 0 && PasoIndex < c.Pasos.Length ? c.Pasos[PasoIndex] : null; } }
        /// <summary>La tarjeta se ve: hay paso actual y no está esperando su señal previa.</summary>
        public bool Visible { get { var p = Actual; return p != null && (p.Espera == null || _hechos.Contains(p.Espera)); } }
        public bool PideHordaEnEspera { get { var c = Cap; return Estado == EstadoTutorial.Activo && c != null && c.Combate; } }
        public bool PidePausa { get { var p = Actual; return Visible && p.Pausa; } }
        public string Cabecera { get { var c = Cap; return Visible ? $"Capítulo {c.Numero} · Paso {PasoIndex + 1} de {c.Pasos.Length}" : null; } }
        public string Texto
        {
            get
            {
                var p = Actual; if (!Visible) return null;
                return p.ExtraSi != null && _hechos.Contains(p.ExtraSi) ? p.Texto + " " + p.Extra : p.Texto;
            }
        }
        public string[] Resalta { get { return Visible ? Actual.Resalta : new string[0]; } }
        public bool Hecho(string senal) { return _hechos.Contains(senal); }

        /// <summary>Arranca el tutorial desde el capítulo 0. Devuelve false (y no hace nada) en difícil o si ya se vio o se saltó antes.</summary>
        public bool Arrancar(int dificultadPermil, bool forzar = false)
        {
            if (!forzar && (!HayTutorial(dificultadPermil) || _mem.Leer(ClaveVisto))) { Log("no arranca: " + (!HayTutorial(dificultadPermil) ? "difícil" : "ya visto")); return false; }
            Estado = EstadoTutorial.Activo; EntrarCapitulo(0); return true;
        }

        /// <summary>«Saltar tutorial»: desde la primera tarjeta o desde el menú de pausa. Queda recordado: no vuelve a salir.</summary>
        public void Saltar()
        {
            if (Estado != EstadoTutorial.Activo) return;
            Log($"saltado en {Actual?.Id ?? "?"}");
            Estado = EstadoTutorial.Saltado; SueltaActual = null; _mem.Escribir(ClaveVisto);
        }

        public void Senal(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (s == "boton:" + BotonSaltar) { Saltar(); return; }
            // tarjetas sueltas: una vez en la vida, solo con el tutorial en marcha (si se salta, tampoco salen)
            if (Estado == EstadoTutorial.Activo)
            {
                if (SueltaActual != null && Array.IndexOf(SueltaActual.Cierra, s) >= 0) { Log($"suelta {SueltaActual.Id} cerrada por {s}"); SueltaActual = null; }
                foreach (var su in _sueltas)
                    if (su.Disparo == s && SueltaActual == null && !_mem.Leer("suelta." + su.Id)) { SueltaActual = su; _enSuelta = 0; _mem.Escribir("suelta." + su.Id); Log($"suelta {su.Id}"); }
            }
            if (Estado != EstadoTutorial.Activo) return;
            if (s == "desvio") { Repetir(); return; }
            bool eraVisible = Visible;
            _hechos.Add(s);
            var p = Actual;
            if (p == null) return;
            if (!eraVisible && Visible) { EnPaso = 0; _sinAvance = 0; _enPaso.Clear(); Log($"sale {p.Id} (esperaba {p.Espera})"); return; }   // la señal que esperaba no cierra la misma tarjeta
            if (!eraVisible || p.Cierre != Cierre.Senal || Array.IndexOf(p.Senales, s) < 0) return;
            _enPaso.Add(s);
            if (p.Todas) foreach (var x in p.Senales) if (!_enPaso.Contains(x)) return;
            Log($"{p.Id} cerrado por {s}"); Siguiente();
        }

        public void Repetir() { if (Visible) { Repeticiones++; _sinAvance = 0; Log($"{Actual.Id} se repite ({Repeticiones})"); } }

        /// <summary>Avanza los relojes (segundos reales). Llamar una vez por fotograma.</summary>
        public void Avanzar(float dt)
        {
            if (SueltaActual != null) { _enSuelta += dt; if (_enSuelta >= SueltaActual.Segundos) SueltaActual = null; }
            if (!Visible) return;
            var p = Actual;
            EnPaso += dt; _sinAvance += dt;
            if (p.Cierre == Cierre.Segundos) { if (EnPaso >= p.Segundos) { Log($"{p.Id} cerrado a los {p.Segundos:0} s"); Siguiente(); } return; }
            if (p.Segundos > 0 && EnPaso >= p.Segundos) { Log($"{p.Id} cerrado por tiempo"); Siguiente(); return; }
            if (_sinAvance >= RepetirCada && !p.Pausa && !p.Linea) Repetir();
        }

        void Siguiente()
        {
            var c = Cap; Repeticiones = 0; EnPaso = 0; _sinAvance = 0; _enPaso.Clear();
            if (PasoIndex + 1 < c.Pasos.Length) { PasoIndex++; Log($"paso {Actual.Id}"); return; }
            if (CapIndex + 1 < _caps.Length) { EntrarCapitulo(CapIndex + 1); return; }
            Estado = EstadoTutorial.Terminado; _mem.Escribir(ClaveVisto); Log("terminado");
        }

        void EntrarCapitulo(int i)
        {
            CapIndex = i; PasoIndex = 0; _hechos.Clear(); _enPaso.Clear(); EnPaso = 0; _sinAvance = 0; Repeticiones = 0;
            Log($"capítulo {Cap.Numero}: {Cap.Titulo}; paso {Actual.Id}");
        }

        void Log(string s) { Registro.Add(s); }
    }
}
