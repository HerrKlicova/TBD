using System.Collections.Generic;
using UnityEngine;
using TBD.Net;

namespace TBD.View
{
    /// <summary>
    /// «Que el juego se sienta vivo» (Nuria, 9-nov; docs/sonido/sonido-vivo.md en mr-mak-workspace). SOLO VISTA: no toca la simulación ni el hash.
    /// Todo usa clips que ya están en Resources/Audio, salvo el barrido de zoom (sfx_zoom_*, entregas/sonido-vivo).
    /// - La horda que se acerca desde lejos: lecho 3D en el centro de la horda (lejos/media, muy bajo) y un aviso «se acerca» una vez por noche.
    /// - NH90 en la zona: arranque, despegue, pasada cercana y toma según el estado del helicóptero (SonidoNH90 ya elegía las variantes).
    /// - Atropello: golpe sordo y caída en el morro del vehículo.
    /// - Barrido de zoom: al salir de la zona al mapa (aquí) y al entrar desde el mapa (OperationsMap3D.EnterZone).
    /// </summary>
    public partial class PrototypeBootstrap
    {
        bool _hordaAvisoSonado; float _hordaAvisoT = -100f;
        readonly Dictionary<string, int> _heliEstadoAntes = new Dictionary<string, int>();
        readonly Dictionary<string, float> _heliAltAntes = new Dictionary<string, float>(), _heliPasoT = new Dictionary<string, float>();

        /// <summary>Lo llama NightAudio al final, cada fotograma de la noche.</summary>
        void SonidoVivo()
        {
            HordaLejana();
            HelicopterosZona();
        }

        // ---- la horda que se acerca: se oye venir de su lado, muy bajo, y calla cuando llega (la cerca ya la lleva NightAudio)
        void HordaLejana()
        {
            float vLejos = 0f, vMedia = 0f; Vector3 c3 = Vector3.zero;
            if (_ng != null && _ng.State == TBD.Units.NightState.Jugando && _zomPos.Count > 0)
            {
                Vector2 c = Vector2.zero; for (int i = 0; i < _zomPos.Count; i++) c += _zomPos[i]; c /= _zomPos.Count;
                var sr = _ng.P.StationRect; var est = new Vector2((sr.X0 + sr.X1) * 0.0005f, (sr.Y0 + sr.Y1) * 0.0005f);
                float d = Vector2.Distance(c, est), hv = Mathf.Min(1f, _zomPos.Count / 150f);
                c3 = new Vector3(c.x, 0f, c.y);
                vLejos = 0.06f * hv * Mathf.Clamp01((d - 50f) / 30f);                          // de 80 m en adelante, entera
                vMedia = 0.07f * hv * Mathf.Clamp01((d - 20f) / 15f) * Mathf.Clamp01((90f - d) / 20f);   // de 35 a 70 m
                if (!_hordaAvisoSonado && d < 120f && d > 40f && Time.unscaledTime - _hordaAvisoT > 60f)
                { _hordaAvisoSonado = true; _hordaAvisoT = Time.unscaledTime; Sfx.PlayAt("horda_acercandose", c3, 0.7f, 30f); }
            }
            else if (_ng != null && _ng.State != TBD.Units.NightState.Jugando) _hordaAvisoSonado = false;   // la noche siguiente vuelve a avisar
            Sfx.LoopAt("horda_lejos_3d", "sfx_horda_lejos_1", c3, vLejos, 400f);
            Sfx.LoopAt("horda_media_3d", "sfx_horda_media_1", c3, vMedia, 250f);
        }

        // ---- NH90 en la zona: estado 0 en tierra, 1 despegando, 2 en vuelo, 3 aterrizando (Veh.State)
        void HelicopterosZona()
        {
            foreach (var v in _vehs)
            {
                if (v.Go == null || !v.Heli) continue;
                var p3 = new Vector3(v.Pos.x, v.Alt, v.Pos.y); uint semilla = (uint)Mathf.Abs(v.Name.GetHashCode());
                int antes = _heliEstadoAntes.TryGetValue(v.Name, out var e0) ? e0 : v.State;
                float altAntes = _heliAltAntes.TryGetValue(v.Name, out var a0) ? a0 : v.Alt;
                if (antes == 0 && v.State == 1) Sfx.PlayClipAt(SonidoNH90.Variante(SonidoNH90.Arranque, semilla + (uint)Time.frameCount), p3, 0.8f);
                if (altAntes < 1.5f && v.Alt >= 1.5f) Sfx.PlayClipAt(SonidoNH90.Variante(SonidoNH90.Despegue, semilla + (uint)Time.frameCount), p3, 0.9f);
                if (antes != 3 && v.State == 3) Sfx.PlayClipAt(SonidoNH90.Variante(SonidoNH90.Toma, semilla + (uint)Time.frameCount), p3, 0.8f);
                // pasada cercana: en vuelo, rápido y a menos de 25 m del foco; como mucho una cada 20 s por aparato (Doppler horneado)
                float tp = _heliPasoT.TryGetValue(v.Name, out var t0) ? t0 : -100f;
                if (v.State == 2 && Mathf.Abs(v.Speed) > 8f && Vector2.Distance(v.Pos, new Vector2(_focus.x, _focus.z)) < 25f && Time.unscaledTime - tp > 20f)
                { _heliPasoT[v.Name] = Time.unscaledTime; Sfx.PlayClipAt(SonidoNH90.Variante(SonidoNH90.Paso, semilla + (uint)Time.frameCount), p3, 0.8f); }
                // ralentí de turbinas en tierra con el rotor girando (se suma al rotor de NightAudio)
                Sfx.LoopAt("ralenti_" + v.Name, "sfx_nh90_ralenti_1", p3, (v.State == 0 || v.Alt < 1.5f) && v.Rpm > 0.05f ? 0.15f * v.Rpm : 0f, 200f);
                _heliEstadoAntes[v.Name] = v.State; _heliAltAntes[v.Name] = v.Alt;
            }
        }

        // ---- atropello: lo llama RunOver con los zombis que acaba de pisar (n > 0)
        void AtropelloSonido(Vector2 morro, int n)
        {
            var p = new Vector3(morro.x, 0f, morro.y);
            Sfx.PlayAt("impacto_carne", p, 0.55f, 0.12f);
            if (n >= 3) Sfx.PlayAt("zombi_caida", p, 0.4f, 0.25f);
        }

        // ---- tren perdido en la zona (Emboscada): golpe metálico y horda en el tren, en 3D
        void TrenPerdidoSonido()
        {
            var tr = _ng.P.TrainRect;
            Sfx.PlayAt("tren_caido_puertas", new Vector3((tr.X0 + tr.X1) * 0.0005f, 0f, (tr.Y0 + tr.Y1) * 0.0005f), 1f, 5f);
        }
    }
}
