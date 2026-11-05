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
                for (int k = 0; k < 6; k++)
                {
                    var sp = new ParticleSystem.EmitParams
                    {
                        position = target + Vector3.up * 0.5f,
                        velocity = new Vector3(Random.Range(-6f, 6f), Random.Range(3f, 9f), Random.Range(-6f, 6f)),
                        startLifetime = Random.Range(0.3f, 0.7f),
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
