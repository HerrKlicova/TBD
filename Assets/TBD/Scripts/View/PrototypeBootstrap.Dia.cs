using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Ambiente de día de la zona (Nuria, D-N157, mr-mak f968ca0; docs/sonido/mezcla-mvp.md D3). Cada estación tiene su variante fija:
    /// Sagunto sfx_amb_dia_sagunto_1 y Nules sfx_amb_dia_nules_2. Va en 2D a 1,0 (nivel horneado a −53 LUFS, 3 dB por debajo de la noche).
    /// Si el día se come a la noche (el clip es mono y la noche estéreo), Nuria pide bajarlo a 0,7: SIN ESCUCHAR todavía.
    /// De noche se calla (lo lleva NightAudio). En pausa calla AudioListener.pause (regla de Iker en NetSession).
    /// El atardecer (sfx_amb_atardecer_1, 20 s antes de la noche, fundido de 3 s) queda pendiente: hoy la zona no pasa del día a la noche en pantalla.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        public const float DiaZonaVol = 1.0f;
        string _diaClip;

        void DayAudio()
        {
            if (_zoneNode < 0 || NetSession.A == null) return;
            if (_diaClip == null)
            {
                string est = _zoneNode < NetSession.A.Graph.Nodes.Count ? NetSession.A.Graph.Nodes[_zoneNode].Name : "";
                _diaClip = est == "Nules" ? "sfx_amb_dia_nules_2" : "sfx_amb_dia_sagunto_1";
            }
            Sfx.Loop(_diaClip, _nightOn ? 0f : DiaZonaVol, false);
        }
    }
}
