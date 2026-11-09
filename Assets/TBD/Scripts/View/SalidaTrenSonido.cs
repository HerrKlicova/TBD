using System;
using System.Collections.Generic;
using TBD.Net;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Toca los sonidos de la salida del tren (SalidaTren; clips de Nuria aprobados por Eugenio el 8-nov) en 3D con Sfx.PlayAt y a volumen 1, porque la mezcla
    /// ya va horneada. El aviso y el silbato suenan en el andén, el cierre en cada puerta y el pitido y el arranque en la cabeza del tren (el pitido con 400 m de distancia máxima: lo ajusta Sfx).
    /// Uso en la zona (Marcos): var s = new SalidaTren(tipo, 60, TrenPreparar.SemillaSalida(tren, nodo, día), puertas, silbato); cada fotograma
    /// s.Avanzar(dt, lista); SalidaTrenSonido.Tocar(lista, puerta => posición, andén, cabeza); y mover el modelo s.Distancia metros.
    /// </summary>
    public static class SalidaTrenSonido
    {
        public static void Tocar(List<SonidoSalida> sonidos, Func<int, Vector3> puerta, Vector3 anden, Vector3 cabeza)
        {
            foreach (var x in sonidos)
            {
                Vector3 p = x.Cabeza ? cabeza : x.Puerta >= 0 && puerta != null ? puerta(x.Puerta) : anden;
                Sfx.PlayClipAt(x.Clip, p, x.Clip.Contains("pitido") ? 0.72f : 1f);
                float vo = x.Clip.Contains("pitido") ? 0.72f : 1f; string sitio = x.Cabeza ? "cabeza" : x.Puerta >= 0 ? "puerta " + x.Puerta : "anden";
                Debug.Log("[TREN-SONIDO] " + x.Clip + " vol=" + vo.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " " + sitio + (Sfx.Existe(x.Clip) ? " ok" : " FALTA"));   // Nuria: comprobar orden y volumen en el log
            }
            sonidos.Clear();
        }
    }
}
