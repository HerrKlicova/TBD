using System.Collections.Generic;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Trazadoras y fogonazos de la noche (Lucia, encargo control-disparos §4; Eugenio, 7-nov: «no rayos laser»).
    /// Cada trazadora es un trazo corto que VIAJA del arma al blanco a una velocidad visual de unos 200 m/s, con una estela que se apaga hacia atras;
    /// nunca une arma y blanco de golpe. Cabeza crema f0dc9c, estela ambar e08c30. Pool de hasta 512. Solo vista: no toca la simulacion ni el hash.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        sealed class TracerFx { public LineRenderer Lr; public Vector3 A, B, Dir; public float T0, Speed, Dist, Tail; }
        readonly List<TracerFx> _tracerLive = new List<TracerFx>(); readonly Stack<TracerFx> _tracerFree = new Stack<TracerFx>();
        sealed class FlashFx { public GameObject Go; public float Until; }
        readonly List<FlashFx> _flashLive = new List<FlashFx>(); readonly Stack<FlashFx> _flashFree = new Stack<FlashFx>();
        Material _tracerMat, _flashMat; Gradient _tracerGrad; Mesh _flashMesh;

        void EnsureFxAssets()
        {
            if (_tracerMat != null) return;
            _tracerMat = new Material(Shader.Find("Sprites/Default")); _tracerMat.enableInstancing = false;
            _tracerGrad = new Gradient();
            _tracerGrad.SetKeys(new[] { new GradientColorKey(new Color32(0xe0, 0x8c, 0x30, 255), 0f), new GradientColorKey(new Color32(0xe0, 0x8c, 0x30, 255), 0.45f), new GradientColorKey(new Color32(0xf0, 0xdc, 0x9c, 255), 1f) },
                                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.55f), new GradientAlphaKey(1f, 1f) });
            _flashMat = new Material(Shader.Find("Sprites/Default")); _flashMat.color = new Color32(0xf0, 0xdc, 0x9c, 255);
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); _flashMesh = go.GetComponent<MeshFilter>().sharedMesh; Destroy(go);
        }

        /// <summary>Lanza una trazadora de a a b. speed en m/s visuales (200 fusil, 150 Dragon, 60 granada); width en metros; tail, largo de la estela en metros.</summary>
        void SpawnTracer(Vector3 a, Vector3 b, float speed, float width, float tail)
        {
            EnsureFxAssets();
            if (_tracerLive.Count >= 512) { var old = _tracerLive[0]; _tracerLive.RemoveAt(0); old.Lr.enabled = false; _tracerFree.Push(old); }
            TracerFx t;
            if (_tracerFree.Count > 0) t = _tracerFree.Pop();
            else
            {
                var go = new GameObject("Trazadora"); var lr = go.AddComponent<LineRenderer>(); lr.sharedMaterial = _tracerMat; lr.positionCount = 2; lr.colorGradient = _tracerGrad; lr.numCapVertices = 0;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; lr.alignment = LineAlignment.View;
                t = new TracerFx { Lr = lr };
            }
            t.A = a; t.B = b; t.Dist = Mathf.Max(0.01f, (b - a).magnitude); t.Dir = (b - a) / t.Dist; t.T0 = Time.time; t.Speed = speed; t.Tail = tail;
            t.Lr.startWidth = t.Lr.endWidth = width; t.Lr.enabled = true; _tracerLive.Add(t);
        }

        /// <summary>Fogonazo de 2 fotogramas (~0,05 s) en la boca del arma: una estrella pequena crema. size en metros.</summary>
        void SpawnFlash(Vector3 pos, float size)
        {
            EnsureFxAssets();
            FlashFx f;
            if (_flashFree.Count > 0) f = _flashFree.Pop();
            else
            {
                var go = new GameObject("Fogonazo"); go.AddComponent<MeshFilter>().sharedMesh = _flashMesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = _flashMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                f = new FlashFx { Go = go };
            }
            f.Go.transform.position = pos; f.Go.transform.localScale = Vector3.one * size;
            f.Go.transform.rotation = _worldCam != null ? _worldCam.transform.rotation : Quaternion.identity; f.Go.SetActive(true); f.Until = Time.time + 0.05f; _flashLive.Add(f);
        }

        void UpdateFx()
        {
            float now = Time.time;
            for (int i = _tracerLive.Count - 1; i >= 0; i--)
            {
                var t = _tracerLive[i]; float travelled = (now - t.T0) * t.Speed;
                if (travelled - t.Tail >= t.Dist) { t.Lr.enabled = false; _tracerFree.Push(t); _tracerLive.RemoveAt(i); continue; }
                float head = Mathf.Min(travelled, t.Dist), tail = Mathf.Max(0f, travelled - t.Tail);
                t.Lr.SetPosition(0, t.A + t.Dir * tail); t.Lr.SetPosition(1, t.A + t.Dir * head);
            }
            for (int i = _flashLive.Count - 1; i >= 0; i--)
                if (now >= _flashLive[i].Until) { _flashLive[i].Go.SetActive(false); _flashFree.Push(_flashLive[i]); _flashLive.RemoveAt(i); }
        }
    }
}
