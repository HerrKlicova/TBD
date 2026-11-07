using System.Collections.Generic;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Sonido mínimo (D-N71 §7): efectos en 2D con limitador por nombre y bucles (horda, música). Los clips están en Resources/Audio (ElevenLabs, nombres fijos de Lucía).
    /// Volumen = general (AudioListener) × música o efectos (opciones del menú).
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        static Sfx _i; readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(); readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        readonly Dictionary<string, AudioSource> _loops = new Dictionary<string, AudioSource>(); AudioSource _one;

        static Sfx I
        {
            get { if (_i == null) { var go = new GameObject("Sfx"); DontDestroyOnLoad(go); _i = go.AddComponent<Sfx>(); _i._one = go.AddComponent<AudioSource>(); _i._one.playOnAwake = false; } return _i; }
        }

        AudioClip Clip(string n) { if (_clips.TryGetValue(n, out var c)) return c; c = Resources.Load<AudioClip>("Audio/" + n); _clips[n] = c; return c; }

        /// <summary>Un efecto, como mucho cada «minGap» segundos por nombre (las ráfagas de 10.000 disparos no se apilan).</summary>
        public static void Play(string name, float vol = 1f, float minGap = 0.08f, float pitch = 1f)
        {
            var s = I; float now = Time.unscaledTime; if (s._last.TryGetValue(name, out var t) && now - t < minGap) return; s._last[name] = now;
            var c = s.Clip(name); if (c == null) return;
            s._one.pitch = pitch; s._one.PlayOneShot(c, vol * MainMenu.SfxVolume);
        }

        /// <summary>
        /// Efecto con distancia ligada al zoom y variantes al azar (Eugenio, 7-nov). Convencion de nombres en Resources/Audio:
        /// sfx_&lt;evento&gt;_&lt;cerca|media|lejos&gt;_&lt;n&gt;  (n = 1..8). Si no existe esa variante prueba sfx_&lt;evento&gt;_&lt;distancia&gt; y por ultimo sfx_&lt;evento&gt; (el clip de siempre).
        /// «ortho» es el tamano ortografico de la camara: &lt; 14 cerca, &lt; 34 media, mas lejos «lejos». Volumen: cerca 1, media 0,7, lejos 0,45.
        /// </summary>
        public static void PlayDist(string evento, float ortho, float vol = 1f, float minGap = 0.08f, float pitchJitter = 0.04f)
        {
            string d = ortho < 14f ? "cerca" : ortho < 34f ? "media" : "lejos"; float dv = d == "cerca" ? 1f : d == "media" ? 0.7f : 0.45f;
            var s = I; float now = Time.unscaledTime; if (s._last.TryGetValue(evento, out var t0) && now - t0 < minGap) return; s._last[evento] = now;
            AudioClip c = null;
            for (int tries = 0; tries < 3 && c == null; tries++) { int n = s.Variants(evento, d); if (n <= 0) break; c = s.Clip("sfx_" + evento + "_" + d + "_" + Random.Range(1, n + 1)); }
            if (c == null) c = s.Clip("sfx_" + evento + "_" + d); if (c == null) c = s.Clip("sfx_" + evento); if (c == null) return;
            s._one.pitch = 1f + Random.Range(-pitchJitter, pitchJitter); s._one.PlayOneShot(c, vol * dv * MainMenu.SfxVolume);
        }
        /// <summary>Efecto sin distancia (impactos, zombis, muros, tren, motores, rotor, ambientes, interfaz, alertas; Nuria): carga sfx_&lt;evento&gt;_&lt;1..8&gt; al azar sin repetir la ultima y con el tono a +-5 %. Si no hay variantes, usa sfx_&lt;evento&gt;.</summary>
        public static void Play(string evento, Vector3 pos, float vol = 1f, float minGap = 0.08f)
        {
            var s = I; float now = Time.unscaledTime; if (s._last.TryGetValue(evento, out var t0) && now - t0 < minGap) return; s._last[evento] = now;
            int n = 0; while (n < 8 && s.Clip("sfx_" + evento + "_" + (n + 1)) != null) n++;
            AudioClip c = null;
            if (n > 0) { int k = Random.Range(1, n + 1); if (n > 1 && s._lastVar.TryGetValue(evento, out var lv) && lv == k) k = k % n + 1; s._lastVar[evento] = k; c = s.Clip("sfx_" + evento + "_" + k); }
            if (c == null) c = s.Clip("sfx_" + evento); if (c == null) return;
            s._one.pitch = 1f + Random.Range(-0.05f, 0.05f); s._one.PlayOneShot(c, vol * MainMenu.SfxVolume);
        }
        readonly Dictionary<string, int> _lastVar = new Dictionary<string, int>();
        readonly Dictionary<string, int> _nvar = new Dictionary<string, int>();
        int Variants(string evento, string d)
        {
            string k = evento + "|" + d; if (_nvar.TryGetValue(k, out var n)) return n;
            n = 0; while (n < 8 && Clip("sfx_" + evento + "_" + d + "_" + (n + 1)) != null) n++; _nvar[k] = n; return n;
        }

        /// <summary>Bucle con nombre (música o ambiente). volumen 0 lo pausa.</summary>
        public static void Loop(string name, float vol, bool music)
        {
            var s = I; if (!s._loops.TryGetValue(name, out var a))
            {
                var c = s.Clip(name); if (c == null) return;
                a = s.gameObject.AddComponent<AudioSource>(); a.clip = c; a.loop = true; a.playOnAwake = false; s._loops[name] = a;
            }
            float v = vol * (music ? MainMenu.MusicVolume : MainMenu.SfxVolume);
            a.volume = v; if (v > 0.001f && !a.isPlaying) a.Play(); else if (v <= 0.001f && a.isPlaying) a.Pause();
        }

        public static void StopLoop(string name) { var s = I; if (s._loops.TryGetValue(name, out var a)) a.Stop(); }
        public static void StopAllLoops() { var s = I; foreach (var a in s._loops.Values) a.Stop(); }
    }
}
