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
