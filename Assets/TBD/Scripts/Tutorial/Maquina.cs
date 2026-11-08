using System;
using System.Collections.Generic;

namespace TBD.Tutorial
{
    /// <summary>
    /// Texto que sale letra a letra, como en una máquina de escribir (petición de Eugenio, 8-nov). C# puro: dice cuántas letras se ven y qué sonidos tocan.
    /// Sonidos (encargo de Nuria, docs/sonido/maquina-escribir/encargo.md): por cada letra visible, una tecla al azar de sfx_ui_tecla_1..8 a 0,35, sin repetir
    /// la anterior y con 25 ms como mínimo entre teclas (si salen más letras en ese hueco, se juntan en una); sfx_ui_tecla_retorno al final de cada línea y
    /// sfx_ui_tecla_campanilla al final del bloque, una vez. Completar() enseña todo de golpe (clic o Espacio) y ya no suena nada.
    /// </summary>
    public sealed class Maquina
    {
        public const float LetrasPorSegundo = 30f, MinEntreTeclas = 0.025f, Volumen = 0.35f, VolumenRetorno = 0.4f, Tono = 0.03f;
        public const string Retorno = "sfx_ui_tecla_retorno", Campanilla = "sfx_ui_tecla_campanilla";
        public static string Tecla(int i) { return "sfx_ui_tecla_" + i; }

        public readonly string Texto;
        public int Visibles { get; private set; }
        public bool Completa { get { return Visibles >= Texto.Length; } }
        public string Parcial { get { return Texto.Substring(0, Visibles); } }
        float _acc, _desdeTecla = 1f; int _ultima; uint _rng; bool _campanilla;

        public Maquina(string texto, uint semilla = 1) { Texto = texto ?? ""; _rng = semilla == 0 ? 1u : semilla; }

        uint Next() { _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5; return _rng; }

        /// <summary>Avanza dt segundos y añade a «sonidos» los clips que tocan en este fotograma.</summary>
        public void Avanzar(float dt, List<string> sonidos)
        {
            _desdeTecla += Math.Max(0f, dt);
            if (Completa) { Cerrar(sonidos); return; }
            _acc += Math.Max(0f, dt) * LetrasPorSegundo;
            bool letra = false;
            while (_acc >= 1f && !Completa)
            {
                _acc -= 1f;
                char c = Texto[Visibles++];
                if (c == '\n') { sonidos?.Add(Retorno); continue; }
                if (!char.IsWhiteSpace(c)) letra = true;
            }
            if (letra && _desdeTecla >= MinEntreTeclas)
            {
                int k; do { k = 1 + (int)(Next() % 8); } while (k == _ultima);
                _ultima = k; _desdeTecla = 0f; sonidos?.Add(Tecla(k));
            }
            if (Completa) Cerrar(sonidos);
        }

        void Cerrar(List<string> sonidos)
        {
            if (_campanilla) return;
            _campanilla = true;
            sonidos?.Add(Retorno); sonidos?.Add(Campanilla);
        }

        /// <summary>Clic o Espacio con el texto a medias: se ve entero.</summary>
        public void Completar(List<string> sonidos) { if (Completa) return; Visibles = Texto.Length; _campanilla = true; }   // si el jugador salta el texto, no suena nada más (Nuria)
    }
}
