using System.Collections;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Efectos de luz a resolución completa (capa VFX_HD, GDD v1.4): trazadoras, fogonazos y chispas como partículas de Unity
    /// con brillo HDR. Son solo presentación: no entran en la simulación ni en el hash.
    /// </summary>
    public class FxHd : MonoBehaviour
    {
        public const int Layer = 8;                       // capa VFX_HD (sin nombre en el proyecto; se usa por número)

        ParticleSystem _tracers, _sparks, _flash;

        public static FxHd Create(Material additive)
        {
            var go = new GameObject("FX_HD");
            var fx = go.AddComponent<FxHd>();
            fx._tracers = fx.Make("Trazadoras", additive, ParticleSystemRenderMode.Stretch, gravity: 0f);
            fx._sparks = fx.Make("Chispas", additive, ParticleSystemRenderMode.Billboard, gravity: 1.2f);
            fx._flash = fx.Make("Fogonazos", additive, ParticleSystemRenderMode.Billboard, gravity: 0f);
            var r = fx._tracers.GetComponent<ParticleSystemRenderer>();
            r.velocityScale = 0.06f;
            r.lengthScale = 2.5f;
            return fx;
        }

        ParticleSystem Make(string name, Material mat, ParticleSystemRenderMode mode, float gravity)
        {
            var go = new GameObject(name);
            go.layer = Layer;
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 4000;
            main.gravityModifier = gravity;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            ps.Play();
            return ps;
        }

        int _shotCount;
        public static bool LegacyShots;            // solo para la captura «antes» de Sara: el disparo con la descarga grande de antes

        /// <summary>Máscara de efectos (Sara, D-N28): todas las partículas en magenta opaco a cuadro entero (con su halo), o null para volver.</summary>
        public void SetMaskMaterial(Material m)
        {
            foreach (var ps in new[] { _tracers, _sparks, _flash })
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (m != null) { if (!_saved.ContainsKey(r)) _saved[r] = r.sharedMaterial; r.sharedMaterial = m; }
                else if (_saved.TryGetValue(r, out var orig)) r.sharedMaterial = orig;
            }
            if (m == null) _saved.Clear();
        }
        readonly System.Collections.Generic.Dictionary<ParticleSystemRenderer, Material> _saved = new System.Collections.Generic.Dictionary<ParticleSystemRenderer, Material>();

        /// <summary>
        /// Disparo de arma ligera (pauta de Lucía, 6-nov): fogonazo diminuto (2-4 px del mundo pixelado, 1-2 fotogramas) en la boca y,
        /// solo en 1 de cada 4 balas, una trazadora corta y fina (núcleo #f0dc9c), de 0,06 s. Con fuego sostenido no debe tapar más
        /// del 5 % de la pantalla.
        /// </summary>
        public void Shot(Vector3 muzzle, Vector3 dir)
        {
            if (LegacyShots) { Volley(muzzle, muzzle + dir * 25f, 1); return; }
            _flash.Emit(new ParticleSystem.EmitParams
            {
                position = muzzle, velocity = Vector3.zero, startLifetime = 0.04f, startSize = 0.6f,
                startColor = new Color(1f, 0.86f, 0.61f, 0.55f),
            }, 1);
            if ((_shotCount++ & 3) != 0) return;
            _tracers.Emit(new ParticleSystem.EmitParams
            {
                position = muzzle + dir * 1.5f, velocity = dir.normalized * 320f, startLifetime = 0.06f, startSize = 0.09f,
                startColor = new Color(0.94f, 0.86f, 0.61f, 0.6f),
            }, 1);
        }

        /// <summary>Descarga de trazadoras con fogonazo en el origen y chispas en el destino.</summary>
        public void Volley(Vector3 from, Vector3 to, int count = 12)
        {
            StartCoroutine(VolleyRoutine(from, to, count));
        }

        IEnumerator VolleyRoutine(Vector3 from, Vector3 to, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 target = to + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
                Vector3 d = target - from;
                float dist = d.magnitude;
                float speed = 140f;

                var tr = new ParticleSystem.EmitParams
                {
                    position = from,
                    velocity = d / dist * speed,
                    startLifetime = dist / speed,
                    startSize = 0.35f,
                    startColor = new Color(1f, 0.85f, 0.45f, 1f),
                };
                _tracers.Emit(tr, 1);

                var fl = new ParticleSystem.EmitParams
                {
                    position = from,
                    velocity = Vector3.zero,
                    startLifetime = 0.07f,
                    startSize = 2.2f,
                    startColor = new Color(1f, 0.8f, 0.4f, 1f),
                };
                _flash.Emit(fl, 1);

                yield return new WaitForSeconds(dist / speed);
                for (int k = 0; k < 3; k++)                                // chispas: la mitad y más cortas (Lucía)
                {
                    var sp = new ParticleSystem.EmitParams
                    {
                        position = target + Vector3.up * 0.5f,
                        velocity = new Vector3(Random.Range(-6f, 6f), Random.Range(3f, 9f), Random.Range(-6f, 6f)),
                        startLifetime = Random.Range(0.15f, 0.35f),
                        startSize = Random.Range(0.12f, 0.3f),
                        startColor = new Color(1f, 0.6f, 0.25f, 1f),
                    };
                    _sparks.Emit(sp, 1);
                }
                yield return new WaitForSeconds(0.05f);
            }
        }
    }
}
