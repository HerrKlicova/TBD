using System.Collections.Generic;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Sonido v2 (Nuria, 7-nov, arreglo de la Noche46). Sustituye al Sfx 2D. Mantiene la API vieja para que compile igual.
    /// - OÍDO RTS: un único AudioListener en el suelo, en el foco de la cámara, a una altura que crece con el zoom.
    ///   Con zoom cercano el oído está a pie de calle; al alejar sube y todo se oye más lejos, más flojo y más vacío.
    /// - Todos los efectos con posición suenan en 3D (spatialBlend 1) con una curva de atenuación propia.
    /// - Un pool de fuentes: cada una con su tono (el tono ya no se comparte entre disparos) y topes de voces por grupo.
    /// - Bucles con fundido de entrada y de salida. No se pausan: bajan a 0 y paran tras 3 s; al volver arrancan en un punto al azar.
    /// - Sin «normalize»: la mezcla viene en los archivos (D-N134). Las reglas de importación están en Editor/AudioImportRules.cs.
    /// Llamadas nuevas: Sfx.Ear(foco, ortho, yaw) cada fotograma; Sfx.PlayAt / PlayDistAt con posición; Sfx.LoopAt para emisores 3D.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        // ----------------------------------------------------------------- ajustes de mezcla (los únicos números a tocar)
        const float MaxDist = 220f;          // a partir de aquí no se oye nada
        const int PoolSize = 40;             // fuentes para efectos sueltos
        const float FadeIn = 0.6f, FadeOut = 0.8f;
        static readonly Dictionary<string, int> Cap = new Dictionary<string, int>
        { { "arma", 12 }, { "impacto", 8 }, { "zombi", 4 }, { "muro", 3 }, { "veh", 4 }, { "ui", 2 }, { "alerta", 2 }, { "otro", 6 } };
        static readonly Dictionary<string, int> Prio = new Dictionary<string, int>      // menor = más importante
        { { "alerta", 0 }, { "ui", 1 }, { "arma", 2 }, { "impacto", 3 }, { "veh", 4 }, { "muro", 4 }, { "zombi", 5 }, { "otro", 5 } };

        static Sfx _i;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        readonly Dictionary<string, int> _lastVar = new Dictionary<string, int>();
        readonly Dictionary<string, int> _nvar = new Dictionary<string, int>();
        readonly List<Voice> _pool = new List<Voice>();
        readonly Dictionary<string, LoopV> _loops = new Dictionary<string, LoopV>();
        Transform _ear; AnimationCurve _curve; float _duckUntil;

        sealed class Voice { public AudioSource A; public string Group; public float Started; }
        sealed class LoopV { public AudioSource A; public float Target, Cur, SilentSince = -1f; public bool Music, Spatial; }

        static Sfx I
        {
            get
            {
                if (_i != null) return _i;
                var go = new GameObject("Sfx"); DontDestroyOnLoad(go); _i = go.AddComponent<Sfx>(); _i.Init(); return _i;
            }
        }

        void Init()
        {
            // atenuación: plana hasta 15 m, -5 dB a 44 m, -13 dB a 100 m, -22 dB a 150 m, silencio a 220 m
            _curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.07f, 1f), new Keyframe(0.2f, 0.55f),
                                        new Keyframe(0.45f, 0.22f), new Keyframe(0.7f, 0.08f), new Keyframe(1f, 0f));
            var e = new GameObject("Oido"); e.transform.SetParent(transform, false); _ear = e.transform;
            for (int k = 0; k < PoolSize; k++) _pool.Add(new Voice { A = NewSource(gameObject, true) });
            EnsureListener();
        }

        AudioSource NewSource(GameObject host, bool spatial)
        {
            var go = new GameObject("fuente"); go.transform.SetParent(host.transform, false);
            var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.dopplerLevel = 0f; a.spread = 40f;
            a.spatialBlend = spatial ? 1f : 0f; a.rolloffMode = AudioRolloffMode.Custom; a.maxDistance = MaxDist; a.minDistance = 1f;
            a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _curve);
            return a;
        }

        /// <summary>Un solo AudioListener: el del oído. Desactiva el de la cámara o cualquier otro.</summary>
        void EnsureListener()
        {
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l.transform != _ear) l.enabled = false;
            var mine = _ear.GetComponent<AudioListener>(); if (mine == null) mine = _ear.gameObject.AddComponent<AudioListener>(); mine.enabled = true;
        }

        /// <summary>LLAMAR CADA FOTOGRAMA desde la cámara de la partida: foco en el suelo, tamaño ortográfico y giro de la cámara.</summary>
        public static void Ear(Vector3 focus, float ortho, float yawDeg)
        {
            var s = I; float h = Mathf.Clamp(ortho * 1.6f, 12f, 140f);
            s._ear.position = new Vector3(focus.x, h, focus.z); s._ear.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            if (Time.frameCount % 60 == 0) s.EnsureListener();       // por si una escena trae su propio listener
        }

        static float EarDist(Vector3 p) => Vector3.Distance(I._ear.position, p);

        AudioClip Clip(string n) { if (_clips.TryGetValue(n, out var c)) return c; c = Resources.Load<AudioClip>("Audio/" + n); _clips[n] = c; return c; }

        int Variants(string baseName)
        {
            if (_nvar.TryGetValue(baseName, out var n)) return n;
            n = 0; while (n < 8 && Clip(baseName + "_" + (n + 1)) != null) n++; _nvar[baseName] = n; return n;
        }

        AudioClip Pick(string baseName)
        {
            int n = Variants(baseName); if (n == 0) return Clip(baseName);
            int k = Random.Range(1, n + 1); if (n > 1 && _lastVar.TryGetValue(baseName, out var lv) && lv == k) k = k % n + 1;
            _lastVar[baseName] = k; return Clip(baseName + "_" + k);
        }

        static string GroupOf(string e)
        {
            if (e.StartsWith("alarma") || e.StartsWith("aviso") || e.StartsWith("radar") || e.StartsWith("municion") || e.StartsWith("estacion")) return "alerta";
            if (e.StartsWith("ui_") || e.StartsWith("sfx_ui")) return "ui";
            if (e.StartsWith("impacto") || e.StartsWith("explosion")) return "impacto";
            if (e.StartsWith("zombi") || e.StartsWith("horda") || e.StartsWith("sfx_zombi")) return "zombi";
            if (e.StartsWith("muro")) return "muro";
            if (e.StartsWith("tren") || e.StartsWith("nh90") || e.Contains("motor")) return "veh";
            if (e.StartsWith("fusil") || e.StartsWith("ametr") || e.StartsWith("canon") || e.StartsWith("granada") || e.StartsWith("tirador") ||
                e.StartsWith("cohete") || e.StartsWith("g36e") || e.StartsWith("cola")) return "arma";
            return "otro";
        }

        Voice Grab(string group)
        {
            int cap = Cap[group], used = 0; Voice oldestSame = null, free = null, worst = null;
            foreach (var v in _pool)
            {
                if (!v.A.isPlaying) { if (free == null) free = v; continue; }
                if (v.Group == group) { used++; if (oldestSame == null || v.Started < oldestSame.Started) oldestSame = v; }
                if (worst == null || Prio[v.Group] > Prio[worst.Group] || (Prio[v.Group] == Prio[worst.Group] && v.Started < worst.Started)) worst = v;
            }
            if (used >= cap) return oldestSame;                       // el grupo está lleno: se reutiliza su voz más vieja
            if (free != null) return free;
            return worst != null && Prio[worst.Group] >= Prio[group] ? worst : null;   // sin huecos: se roba la menos importante
        }

        void Fire(AudioClip c, string group, Vector3? pos, float vol, float pitch)
        {
            if (c == null) return; var v = Grab(group); if (v == null) return;
            if (group == "alerta") _duckUntil = Time.unscaledTime + 1.5f;
            float duck = group != "alerta" && group != "ui" && Time.unscaledTime < _duckUntil ? 0.55f : 1f;
            v.A.Stop(); v.A.clip = c; v.A.pitch = pitch; v.A.volume = Mathf.Clamp01(vol * duck * MainMenu.SfxVolume);
            v.A.spatialBlend = pos.HasValue ? 1f : 0f; if (pos.HasValue) v.A.transform.position = pos.Value;
            v.Group = group; v.Started = Time.unscaledTime; v.A.Play();
        }

        bool Gate(string key, float minGap)
        {
            float now = Time.unscaledTime; if (_last.TryGetValue(key, out var t) && now - t < minGap) return false; _last[key] = now; return true;
        }

        // ----------------------------------------------------------------- API

        /// <summary>Efecto 2D con nombre de archivo exacto (interfaz). Compatibilidad con el Sfx viejo.</summary>
        public static void Play(string name, float vol = 1f, float minGap = 0.08f, float pitch = 1f)
        {
            var s = I; if (!s.Gate(name, minGap)) return; s.Fire(s.Clip(name), GroupOf(name.Replace("sfx_", "")), null, vol, pitch);
        }

        /// <summary>Efecto por evento con variantes. Con pos = Vector3.zero suena en 2D (interfaz y alertas); con posición, en 3D.</summary>
        public static void Play(string evento, Vector3 pos, float vol = 1f, float minGap = 0.08f) => I.PlayEv(evento, pos == Vector3.zero ? (Vector3?)null : pos, vol, minGap);

        /// <summary>Efecto por evento SIEMPRE en 3D en «pos» (impactos, zombis, muros...).</summary>
        public static void PlayAt(string evento, Vector3 pos, float vol = 1f, float minGap = 0.05f) => I.PlayEv(evento, pos, vol, minGap);

        void PlayEv(string evento, Vector3? pos, float vol, float minGap)
        {
            if (!Gate(evento, minGap)) return;
            Fire(Pick("sfx_" + evento), GroupOf(evento), pos, vol, 1f + Random.Range(-0.05f, 0.05f));
        }

        /// <summary>Arma con capa cerca/media/lejos elegida por la distancia REAL al oído, y sonando en 3D en su sitio.</summary>
        public static void PlayDistAt(string evento, Vector3 pos, float vol = 1f, float minGap = 0.08f, float pitchJitter = 0.04f)
        {
            var s = I; float d = EarDist(pos); string capa = d < 40f ? "cerca" : d < 110f ? "media" : "lejos";
            if (!s.Gate(evento + capa, minGap)) return;
            var c = s.Pick("sfx_" + evento + "_" + capa); if (c == null) c = s.Pick("sfx_" + evento);
            s.Fire(c, GroupOf(evento), pos, vol, 1f + Random.Range(-pitchJitter, pitchJitter));
        }

        /// <summary>COMPATIBILIDAD: llamadas viejas sin posición. Suenan en el foco (centro de pantalla). Pasar a PlayDistAt.</summary>
        public static void PlayDist(string evento, float ortho, float vol = 1f, float minGap = 0.08f, float pitchJitter = 0.04f)
        {
            var e = I._ear.position; PlayDistAt(evento, new Vector3(e.x, 0f, e.z), vol, minGap, pitchJitter);
        }

        /// <summary>Bucle 2D (ambiente, música). Volumen objetivo con fundido; nunca corta en seco.</summary>
        public static void Loop(string name, float vol, bool music) => I.SetLoop(name, name, vol, music, null, MaxDist);

        /// <summary>Bucle 3D pegado a un punto (rotor, motores, fuego, muro, tren, horda cercana). key único por emisor.</summary>
        public static void LoopAt(string key, string clipName, Vector3 pos, float vol, float maxDist = 160f) => I.SetLoop(key, clipName, vol, false, pos, maxDist);

        void SetLoop(string key, string clipName, float vol, bool music, Vector3? pos, float maxDist)
        {
            if (!_loops.TryGetValue(key, out var l))
            {
                var c = Clip(clipName); if (c == null) return;
                var a = NewSource(gameObject, pos.HasValue); a.clip = c; a.loop = true; a.maxDistance = maxDist; a.priority = music ? 0 : 64;
                l = new LoopV { A = a, Music = music, Spatial = pos.HasValue }; _loops[key] = l;
            }
            l.Target = Mathf.Clamp01(vol); if (pos.HasValue) l.A.transform.position = pos.Value;
        }

        public static void StopLoop(string name) { if (I._loops.TryGetValue(name, out var l)) l.Target = 0f; }
        public static void StopAllLoops() { foreach (var l in I._loops.Values) l.Target = 0f; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime; float duck = now < _duckUntil ? 0.55f : 1f;
            foreach (var l in _loops.Values)
            {
                float rate = l.Target > l.Cur ? dt / FadeIn : dt / FadeOut; l.Cur = Mathf.MoveTowards(l.Cur, l.Target, rate);
                float master = l.Music ? MainMenu.MusicVolume : MainMenu.SfxVolume;
                l.A.volume = l.Cur * master * (l.Music ? 1f : duck);
                if (l.Cur > 0.001f)
                {
                    l.SilentSince = -1f;
                    if (!l.A.isPlaying) { l.A.time = Random.Range(0f, Mathf.Max(0f, l.A.clip.length - 0.1f)); l.A.Play(); }
                }
                else if (l.A.isPlaying)
                {
                    if (l.SilentSince < 0f) l.SilentSince = now; else if (now - l.SilentSince > 3f) l.A.Stop();
                }
            }
        }
    }
}
