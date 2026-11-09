using System.Collections.Generic;
using UnityEngine;
using TBD.Sim;
using TBD.Units;

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
            t.Lr.startWidth = t.Lr.endWidth = width; t.Lr.enabled = true; t.Lr.gameObject.layer = 0; _tracerLive.Add(t);
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

        /// <summary>Sonido de la noche (Nuria, docs/sonido/integracion-noche26.md): ambiente, lecho de la horda por distancia segun el zoom, motores, rotor, tren y alarma. Sin AudioMixer: volumenes directos.</summary>
        bool _alarmOn; float _alarmT = -100f;
        void NightAudio()
        {
            if (_ng == null) return;
            float o = _orthoSize; float far01 = Mathf.Clamp01((o - 14f) / 40f);
            Sfx.Ear(_focus, o, _yaw);                                                         // oyente RTS en el foco de la camara (Nuria): todo suena en 3D con atenuacion
            // ambiente: bajo y cada vez mas vacio al alejar la camara (Eugenio 8 y 20)
            Sfx.Loop("sfx_amb_estacion_pequena_1", 0.10f * (1f - 0.85f * far01), false);
            Sfx.Loop("sfx_amb_viento_1", 0.05f * far01, false);
            // horda: solo se oye si te acercas, y bajito (Eugenio 17)
            float hv = Mathf.Min(1f, _total / 150f);
            Sfx.Loop("sfx_horda_cerca_1", o < 12f ? 0.10f * hv : 0f, false); Sfx.Loop("sfx_horda_media_1", 0f, false); Sfx.Loop("sfx_horda_lejos_1", 0f, false);
            Sfx.LoopAt("tren_ralenti", "sfx_tren_ralenti_1", new Vector3(110f, 0f, 64.5f), 0.12f, 60f);
            // motores y rotor: uno por vehiculo, en su posicion (Eugenio 19 y 21)
            foreach (var v in _vehs)
            {
                if (v.Go == null) continue; var p3 = new Vector3(v.Pos.x, v.Alt, v.Pos.y);
                if (v.Heli) Sfx.LoopAt("rotor_" + v.Name, "sfx_nh90_rotor_" + (1 + (Mathf.Abs(v.Name.GetHashCode()) % 6)), p3, 0.22f * v.Rpm, 300f);   // el volumen sigue a las palas (Nuria)
                else Sfx.LoopAt("motor_" + v.Name, v.Name.StartsWith("Drag") ? "sfx_dragon_motor_1" : "sfx_vamtac_motor_1", p3, Mathf.Abs(v.Speed) > 0.5f ? 0.25f : 0f, 60f);
            }
            bool attack = _ng.State == NightState.Jugando && (_pileNearStation > 0 || _nearTrain > 0);
            if (attack && !_alarmOn && Time.unscaledTime - _alarmT > 20f) { _alarmT = Time.unscaledTime; Sfx.Play("alarma_ataque", Vector3.zero, 0.6f, 1f); }
            _alarmOn = attack;
        }

        // ---- QA29 (Bruno): barrido de 5 gradaciones en el MISMO fotograma (partida en pausa), una captura directa (_full) por variante, con frameCount, tiempo y media de la imagen en el log
        bool _sweepDone;
        void GradeSweepCheck()
        {
            if (_sweepDone || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-gradsweep") < 0 || Time.realtimeSinceStartup - _nightT0 < 40f || _ng == null) return;
            _sweepDone = true; StartCoroutine(GradeSweep());
        }
        System.Collections.IEnumerator GradeSweep()
        {
            _nightGradeLock = true; _paused = true;
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Captures")); System.IO.Directory.CreateDirectory(dir);
            // exposicion, saturacion, luna r, g, b
            float[][] v = { new[] { -0.45f, -30f, 0.75f, 0.80f, 1f }, new[] { -0.45f, -30f, 0.62f, 0.70f, 1f }, new[] { -0.20f, -30f, 0.62f, 0.70f, 1f }, new[] { -0.70f, -20f, 0.62f, 0.70f, 1f }, new[] { -0.45f, -20f, 0.55f, 0.65f, 1f } };
            for (int i = 0; i < v.Length; i++)
            {
                _grade.colorFilter.Override(Color.white); _grade.postExposure.Override(v[i][0]); _grade.saturation.Override(v[i][1]);
                SetLight(new Color(v[i][2], v[i][3], v[i][4]), 0.45f, new Color(0.03f, 0.045f, 0.10f), 50f); SetFloodlights(90f);
                yield return null; yield return null; yield return new WaitForEndOfFrame();
                var full = ScreenCapture.CaptureScreenshotAsTexture(); var px = full.GetPixels32(); long sr = 0, sg = 0, sb = 0; int step = 7, n = 0;
                for (int k = 0; k < px.Length; k += step) { sr += px[k].r; sg += px[k].g; sb += px[k].b; n++; }
                string path = System.IO.Path.Combine(dir, "grad_G" + (i + 1) + "_full.png"); System.IO.File.WriteAllBytes(path, full.EncodeToPNG()); Destroy(full);
                Debug.Log($"[NOCHE] barrido G{i + 1}: frame={Time.frameCount} t={Time.realtimeSinceStartup:0.000} fps={_fps:0} media=({sr / n};{sg / n};{sb / n}) exp={v[i][0]} sat={v[i][1]} luna=({v[i][2]};{v[i][3]};{v[i][4]})");
            }
            _paused = false;
        }

        // ---- QA (Lucia): lista los objetos grandes que se ven en pantalla con su posicion en el mundo y en la captura de 1920x1080 (-listobjs)
        bool _listDone;
        void ListObjsCheck()
        {
            if (_listDone || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-listobjs") < 0 || Time.realtimeSinceStartup - _nightT0 < 28f || _worldCam == null) return;
            _listDone = true; int n = 0;
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var b = r.bounds; if (b.size.x < 3f && b.size.z < 3f && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-listtodo") < 0) continue; var vp = _worldCam.WorldToViewportPoint(b.center); if (vp.z < 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;
                string path = r.gameObject.name; var tr = r.transform.parent; int d = 0; while (tr != null && d++ < 3) { path = tr.name + "/" + path; tr = tr.parent; }
                Debug.Log($"[OBJ] {path} mundo=({b.center.x:0.0};{b.center.y:0.0};{b.center.z:0.0}) tam=({b.size.x:0.0};{b.size.y:0.0};{b.size.z:0.0}) pantalla=({vp.x * 1920:0};{(1f - vp.y) * 1080:0}) color={(r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor") ? ColorUtility.ToHtmlStringRGB(r.sharedMaterial.GetColor("_BaseColor")) : "-")}"); n++;
            }
            Debug.Log("[OBJ] total " + n);
        }

        // ---- QA (-inputtest): simula WASD y clics con el Input System y escribe en el log si la camara se mueve y si la seleccion funciona varias veces seguidas
        bool _itStarted;
        void InputTestCheck()
        {
            if (_itStarted || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-inputtest") < 0 || Time.realtimeSinceStartup - _nightT0 < 15f || _ng == null) return;
            _itStarted = true; StartCoroutine(InputTest());
        }
        System.Collections.IEnumerator InputTest()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current; var ms = UnityEngine.InputSystem.Mouse.current;
            if (kb == null || ms == null) { Debug.Log("[TEST] sin teclado o raton"); yield break; }
            _paused = false;
            // 1) WASD: D mueve el foco a la derecha, W hacia delante
            foreach (var key in new[] { UnityEngine.InputSystem.Key.D, UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.A, UnityEngine.InputSystem.Key.S })
            {
                var f0 = _focus; UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
                yield return new WaitForSecondsRealtime(0.6f); var f1 = _focus; UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return new WaitForSecondsRealtime(0.2f);
                Debug.Log($"[TEST] tecla {key}: foco ({f0.x:0.00};{f0.z:0.00}) -> ({f1.x:0.00};{f1.z:0.00}) {(Vector3.Distance(f0, f1) > 0.2f ? "MUEVE" : "NO MUEVE")}");
            }
            _focus = new Vector3(96f, 0f, 64f); yield return new WaitForSecondsRealtime(0.5f);   // vuelve al encuadre inicial para que los pelotones esten a la vista
            // 2) seleccion varias veces seguidas por clic sobre cada pelotón
            int ok = 0, tot = 0;
            for (int rep = 0; rep < 2; rep++)
                for (int i = 0; i < _uw.Platoons.Count; i++)
                {
                    var pl = _uw.Platoons[i]; Infantryman man = null; foreach (var m in pl.Men) if (m.Alive) { man = m; break; } if (man == null) continue;
                    var sp = ScreenOf(new Vector3(man.X / 1000f, 1f, man.Y / 1000f)); if (sp.z < 0f || sp.y > Screen.height * 0.78f || sp.y < Screen.height * 0.1f) { Debug.Log($"[TEST] pelotón {pl.Id}: fuera de pantalla ({sp.x:0};{sp.y:0})"); continue; }
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(ms, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(sp.x, sp.y) }); yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(ms, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(sp.x, sp.y) }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true)); yield return null; yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(ms, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(sp.x, sp.y) }); yield return null; yield return null;
                    tot++; bool sel = _sel.Count == 1 && _sel.Contains(pl.Id) && _selVeh == null; if (sel) ok++;
                    Debug.Log($"[TEST] clic sobre pelotón {pl.Id} en ({sp.x:0};{sp.y:0}): seleccionados [{string.Join(",", _sel)}] {(sel ? "OK" : "FALLO")}");
                }
            Debug.Log($"[TEST] seleccion: {ok}/{tot} correctas");
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
