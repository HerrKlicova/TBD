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
    public enum TipoTren : byte { Electrico, Diesel }

    public sealed class SalidaTren
    {
        public const float AvisoS = 3f, CierreS = 3f, ComprobacionS = 2f;
        public const string SonAviso = "sfx_tren_aviso_puertas", SonCierre = "sfx_tren_cierre_puertas", SonSilbato = "sfx_tren_silbato_salida",
            SonArranqueElectrico = "sfx_tren_arranque_electrico", SonArranqueDiesel = "sfx_tren_arranque_diesel333";   // nombres de Nuria (docs/sonido/salida-tren/encargo.md), cada uno con variantes _1 a _5

        public readonly TipoTren Tipo;
        public readonly float VmaxMs;                 // velocidad de crucero que alcanza (m/s): la de la red, 60 km/h
        public float T { get; private set; }          // segundos desde que empieza el aviso
        public float Velocidad { get; private set; }  // m/s
        public float Distancia { get; private set; }  // m recorridos desde la parada
        float _paso = 0.02f;

        public SalidaTren(TipoTren tipo, float vmaxKmh = 60f, uint semilla = 1) { Tipo = tipo; VmaxMs = vmaxKmh / 3.6f; _rng = semilla == 0 ? 1u : semilla; }
        uint _rng;
        /// <summary>Variante _1 a _5 del clip, al azar con la semilla de la salida (así dos trenes no suenan igual).</summary>
        string Variante(string clip) { _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5; return clip + "_" + (1 + _rng % 5); }

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

        /// <summary>Avanza dt segundos reales y añade los sonidos que tocan.</summary>
        public void Avanzar(float dt, List<string> sonidos)
        {
            float t0 = T, fin = T + Math.Max(0f, dt);
            Sonar(t0, fin, 0f, SonAviso, sonidos);
            Sonar(t0, fin, AvisoS, SonCierre, sonidos);
            Sonar(t0, fin, InicioArranque - 0.6f, SonSilbato, sonidos);                     // el silbato de dos tonos, justo antes de moverse
            Sonar(t0, fin, InicioArranque, Tipo == TipoTren.Electrico ? SonArranqueElectrico : SonArranqueDiesel, sonidos);
            while (T < fin)
            {
                float h = Math.Min(_paso, fin - T);
                if (T >= InicioArranque) { float a = Aceleracion(Velocidad); Velocidad = Math.Min(VmaxMs, Velocidad + a * h); Distancia += Velocidad * h; }
                T += h;
            }
        }

        void Sonar(float t0, float t1, float at, string s, List<string> l) { if (l != null && t0 <= at && at < t1 && !(t0 == at && t0 > 0)) l.Add(Variante(s)); }

        /// <summary>Segundos desde el principio del arranque hasta alcanzar esa velocidad (km/h); −1 si no llega en 600 s. Para las pruebas.</summary>
        public static float TiempoHasta(TipoTren tipo, float kmh)
        {
            var s = new SalidaTren(tipo, 120f); var l = new List<string>();
            s.Avanzar(s.InicioArranque, l);
            float t = 0; while (s.Velocidad * 3.6f < kmh && t < 600f) { s.Avanzar(0.05f, l); t += 0.05f; }
            return t < 600f ? t : -1f;
        }
    }
}
