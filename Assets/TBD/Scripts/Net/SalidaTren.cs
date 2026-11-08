using System;
using System.Collections.Generic;

namespace TBD.Net
{
    /// <summary>
    /// Salida visible del tren (D-N170, deseo de Eugenio; datos de Paula en salida-tren-referencias.md). C# puro: fases, sonidos y cuánto se ha movido el
    /// tren en cada instante, en segundos reales de la zona (es la vista: la red sigue con su marcha; la zona usa este perfil para el modelo del tren).
    /// Secuencia: aviso de puertas ~3 s (EN 14752: ≥ 3 s antes de moverse), cierre ~3 s, comprobación y frenos ~2 s, señal de salida con silbato de mano
    /// de dos tonos (RCF art. 2.1.6.2; el pitido de la locomotora no es obligatorio: licencia artística, no se pone) y arranque progresivo.
    /// Arranque: eléctrico de Cercanías ~1,0 m/s² hasta 40 km/h y luego baja (30 km/h en ~9 s, 60 en ~18 s); mercancías diésel 333 con carga, 0,15 m/s²
    /// al principio (30 km/h en ~26 s con 800 t). Las cifras son ESTIMACIONES de Paula salvo el aviso de 3 s.
    /// </summary>
    public enum SalidaFase : byte { AvisoPuertas, CierrePuertas, Comprobacion, Arranque }

    /// <summary>Un sonido de la salida y dónde suena: Puerta ≥ 0 en esa puerta; Puerta = −1 en el andén (aviso y silbato); Cabeza en la cabeza del tren (arranque).
    /// Todos en 3D (Sfx.PlayAt) y a volumen 1: la mezcla ya va horneada (Nuria).</summary>
    public struct SonidoSalida { public string Clip; public int Puerta; public bool Cabeza; }
    public enum TipoTren : byte { Electrico, Diesel }

    public sealed class SalidaTren
    {
        public const float AvisoS = 3f, CierreS = 3f, DesfasePuertaMaxS = 0.3f, ArranqueMinS = 2f, ArranqueMaxS = 5f;   // Nuria: arranque de 2 a 5 s tras el cierre
        public const string SonAviso = "sfx_tren_aviso_puertas", SonCierre = "sfx_tren_cierre_puertas", SonSilbato = "sfx_tren_silbato_salida",
            SonArranqueElectrico = "sfx_tren_arranque_electrico", SonArranqueDiesel = "sfx_tren_arranque_diesel333",
            SonPitidoElectrico = "sfx_tren_pitido_electrico", SonPitidoDiesel = "sfx_tren_pitido_diesel";      // pitido de salida (Nuria, D-N170): en la cabeza, de 0,5 a 1 s antes del arranque   // nombres de Nuria (docs/sonido/salida-tren/encargo.md), cada uno con variantes _1 a _5

        public readonly TipoTren Tipo;
        public readonly float VmaxMs;                 // velocidad de crucero que alcanza (m/s): la de la red, 60 km/h
        public float T { get; private set; }          // segundos desde que empieza el aviso
        public float Velocidad { get; private set; }  // m/s
        public float Distancia { get; private set; }  // m recorridos desde la parada
        float _paso = 0.02f;

        public readonly int NumPuertas; public readonly bool ConSilbato;
        public readonly float ComprobacionS;          // 2 a 5 s, con la semilla
        public readonly float PitidoAntesS;           // 0,5 a 1 s antes del arranque (Nuria), con la semilla
        public const float PitidoDistanciaMax = 400f;
        readonly float[] _desfase;                    // 0 a 0,3 s por puerta

        /// <summary>«semilla»: según el tren y la parada (p. ej. TrenPreparar.Semilla(tren, nodo, día)), para que dos salidas no suenen igual.</summary>
        public SalidaTren(TipoTren tipo, float vmaxKmh = 60f, uint semilla = 1, int puertas = 1, bool silbato = true)
        {
            Tipo = tipo; VmaxMs = vmaxKmh / 3.6f; _rng = semilla == 0 ? 1u : semilla; NumPuertas = Math.Max(0, puertas); ConSilbato = silbato;
            ComprobacionS = ArranqueMinS + (ArranqueMaxS - ArranqueMinS) * (Next() % 1001) / 1000f;
            _desfase = new float[NumPuertas]; for (int i = 0; i < NumPuertas; i++) _desfase[i] = DesfasePuertaMaxS * (Next() % 1001) / 1000f;
            PitidoAntesS = 0.5f + 0.5f * (Next() % 1001) / 1000f;
        }
        uint _rng;
        uint Next() { _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5; return _rng; }
        /// <summary>Variante _1 a _5 del clip, con la semilla de la salida.</summary>
        string Variante(string clip) { return clip + "_" + (1 + Next() % 5); }
        public float Desfase(int puerta) { return _desfase[puerta]; }

        public float InicioArranque { get { return AvisoS + CierreS + ComprobacionS; } }
        public SalidaFase Fase
        {
            get
            {
                if (T < AvisoS) return SalidaFase.AvisoPuertas;
                if (T < AvisoS + CierreS) return SalidaFase.CierrePuertas;
                if (T < InicioArranque) return SalidaFase.Comprobacion;
                return SalidaFase.Arranque;
            }
        }
        /// <summary>0 = puertas abiertas, 1 = cerradas (se cierran durante CierreS).</summary>
        public float Puertas { get { return Math.Max(0f, Math.Min(1f, (T - AvisoS) / CierreS)); } }

        /// <summary>Aceleración (m/s²) a esa velocidad, según el tipo de tren.</summary>
        public float Aceleracion(float v)
        {
            if (v >= VmaxMs) return 0f;
            if (Tipo == TipoTren.Electrico)
            {
                const float v40 = 40f / 3.6f;
                return v <= v40 ? 1.0f : Math.Max(0.25f, 1.0f * v40 / v);      // potencia casi constante por encima de 40 km/h
            }
            // diésel con carga: arranque suave (0,15 m/s²) los primeros segundos, después lo que da la potencia (~0,37 m/s² de máximo teórico con 800 t, cae con la velocidad)
            float tArr = T - InicioArranque;
            float suave = tArr < 6f ? 0.15f : 0.37f;
            return Math.Max(0.06f, suave * Math.Min(1f, 8.3f / Math.Max(8.3f, v)));
        }

        /// <summary>Avanza dt segundos reales y añade los sonidos que tocan (solo el nombre del clip).</summary>
        public void Avanzar(float dt, List<string> sonidos)
        {
            List<SonidoSalida> l = sonidos == null ? null : new List<SonidoSalida>();
            Avanzar(dt, l);
            if (l != null) foreach (var x in l) sonidos.Add(x.Clip);
        }

        /// <summary>Avanza dt segundos reales y añade los sonidos que tocan, con dónde suenan. El pitido de salida va en la cabeza, de 0,5 a 1 s antes del arranque (Nuria, D-N170; 400 m de distancia máxima).</summary>
        public void Avanzar(float dt, List<SonidoSalida> sonidos)
        {
            float t0 = T, fin = T + Math.Max(0f, dt);
            Sonar(t0, fin, 0f, SonAviso, -1, false, sonidos);
            for (int i = 0; i < NumPuertas; i++) Sonar(t0, fin, AvisoS + _desfase[i], SonCierre, i, false, sonidos);   // cada puerta con su desfase
            if (ConSilbato) Sonar(t0, fin, InicioArranque - 0.6f, SonSilbato, -1, false, sonidos);              // el silbato de dos tonos del jefe, justo antes de moverse
            Sonar(t0, fin, InicioArranque - PitidoAntesS, Tipo == TipoTren.Electrico ? SonPitidoElectrico : SonPitidoDiesel, -1, true, sonidos);   // después del cierre y antes del arranque
            Sonar(t0, fin, InicioArranque, Tipo == TipoTren.Electrico ? SonArranqueElectrico : SonArranqueDiesel, -1, true, sonidos);
            while (T < fin)
            {
                float h = Math.Min(_paso, fin - T);
                if (T >= InicioArranque) { float a = Aceleracion(Velocidad); Velocidad = Math.Min(VmaxMs, Velocidad + a * h); Distancia += Velocidad * h; }
                T += h;
            }
        }

        void Sonar(float t0, float t1, float at, string s, int puerta, bool cabeza, List<SonidoSalida> l) { if (l != null && t0 <= at && at < t1 && !(t0 == at && t0 > 0)) l.Add(new SonidoSalida { Clip = Variante(s), Puerta = puerta, Cabeza = cabeza }); }

        /// <summary>Segundos desde el principio del arranque hasta alcanzar esa velocidad (km/h); −1 si no llega en 600 s. Para las pruebas.</summary>
        public static float TiempoHasta(TipoTren tipo, float kmh)
        {
            var s = new SalidaTren(tipo, 120f); var l = new List<SonidoSalida>();
            s.Avanzar(s.InicioArranque, l);
            float t = 0; while (s.Velocidad * 3.6f < kmh && t < 600f) { s.Avanzar(0.05f, l); t += 0.05f; }
            return t < 600f ? t : -1f;
        }
    }
}
