using System.Collections.Generic;

namespace TBD.View.UiNoche
{
    public enum Nivel { Info, Aviso, Ataque }
    public enum Gravedad { Baja = 1, Media = 2, Alta = 3 }

    public struct Aviso
    {
        public Nivel nivel;
        public string icono;      // p. ej. "ui_aviso_tren_atacado_16", "ui_estado_sin_municion_16"
        public string texto;      // «Tren atacado · vagón 2»
        public bool teclaT;       // dibuja ui_tecla_t_9 al final del texto
        public float desde;       // Time.unscaledTime en que apareció (el ámbar parpadea solo los 3 primeros s, D-N109)
    }

    public struct Frente
    {
        public string icono;      // ui_frente_{estacion,tren,base,paso_nivel}_16
        public string nombre;
        public Gravedad gravedad;
    }

    public struct Tarjeta
    {
        public string tipo;       // fusileros, ametralladora, granaderos, tiradores, dragon, vamtac, nh90...
        public int id;            // variante del retrato = id % 6
        public float efectivos, municion; // 0..1
        public bool seleccionada, sinMunicion;
        public float sinMunicionDesde;
    }

    public struct Parte
    {
        public bool victoria; public string titulo;   // titular de la derrota (D-N133: «El tren ha caído» / «Sagunto ha caído»)
        public int zombis, bajas, municionGastada, municionRestante;
        public float estacion, tren, muro; // 0..1
    }

    /// <summary>Lo que la interfaz de noche necesita del juego. Marcos lo implementa con NightRun y NetSession.</summary>
    public interface IUiNocheDatos
    {
        string Reloj { get; }                 // «01:12»
        float ProgresoNoche { get; }          // 0 a las 22:00, 1 al amanecer
        int Municion { get; }
        int Materiales { get; }
        int Combustible { get; }
        int Pila { get; }
        int Bajas { get; }
        float PrimeraOleadaEn { get; }        // s que faltan; < 0 si ya empezó
        IReadOnlyList<Aviso> Avisos { get; }
        IReadOnlyList<Frente> Frentes { get; }
        IReadOnlyList<Tarjeta> Tarjetas { get; }
        Parte Parte { get; }
        bool HayPartidaGuardada { get; }
    }

    /// <summary>Datos falsos para la prueba (-uidemo), con los números de las maquetas.</summary>
    public sealed class DatosFalsos : IUiNocheDatos
    {
        readonly List<Aviso> _avisos = new List<Aviso>();
        readonly List<Frente> _frentes = new List<Frente>();
        readonly List<Tarjeta> _tarjetas = new List<Tarjeta>();

        public DatosFalsos(float t0)
        {
            _avisos.Add(new Aviso { nivel = Nivel.Ataque, icono = "ui_aviso_tren_atacado_16", texto = "Tren atacado · vagón 2", desde = t0 });
            _avisos.Add(new Aviso { nivel = Nivel.Aviso, icono = "ui_estado_sin_municion_16", texto = "Sin munición · ", teclaT = true, desde = t0 });
            _avisos.Add(new Aviso { nivel = Nivel.Ataque, icono = "ui_aviso_muro_atacado_16", texto = "Muro bajo ataque", desde = t0 });
            _avisos.Add(new Aviso { nivel = Nivel.Info, icono = "ui_aviso_refuerzos_16", texto = "Refuerzos en camino", desde = t0 });
            _frentes.Add(new Frente { icono = "ui_frente_tren_16", nombre = "Tren", gravedad = Gravedad.Alta });
            _frentes.Add(new Frente { icono = "ui_frente_paso_nivel_16", nombre = "Paso a nivel", gravedad = Gravedad.Media });
            _frentes.Add(new Frente { icono = "ui_frente_estacion_16", nombre = "Estación", gravedad = Gravedad.Baja });
            string[] tipos = { "fusileros", "ametralladora", "granaderos", "tiradores", "dragon" };
            for (int i = 0; i < tipos.Length; i++)
                _tarjetas.Add(new Tarjeta { tipo = tipos[i], id = i + 1, efectivos = 1f - i * .15f, municion = i == 2 ? 0f : .8f - i * .1f,
                                            seleccionada = i == 1, sinMunicion = i == 2, sinMunicionDesde = t0 });
        }

        public string Reloj => "01:12";
        public float ProgresoNoche => .4f;
        public int Municion => 50;
        public int Materiales => 120;
        public int Combustible => 80;
        public int Pila => 340;
        public int Bajas => 3;
        public float PrimeraOleadaEn => 42f;
        public IReadOnlyList<Aviso> Avisos => _avisos;
        public IReadOnlyList<Frente> Frentes => _frentes;
        public IReadOnlyList<Tarjeta> Tarjetas => _tarjetas;
        public Parte Parte { get; set; } = new Parte { victoria = true, zombis = 1284, bajas = 3, municionGastada = 37, municionRestante = 13, estacion = .92f, tren = .64f, muro = .48f };
        public bool HayPartidaGuardada => false;
    }
}
