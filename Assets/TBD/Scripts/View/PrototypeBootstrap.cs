using System.IO;
using System.Text;
using TBD.Sim;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// Prototipo de zona (hito 1 del GDD), solo con primitivas y sin arte: un fuerte con puerta, una vía con su tren y una
    /// horda en DOTS que se amontona contra el muro y cae desde un tejado. Se arranca solo al dar a Play en SampleScene.
    ///
    /// Controles:  1 = zombis en el tejado (500) · 2 = abrir/cerrar la puerta · 3 = arrancar/parar el tren
    ///             4 = 3.000 zombis más por el oeste · WASD/flechas = mover la cámara · rueda = zoom
    ///             F3 = activar/desactivar la prueba de desincronización · P = pausa
    /// </summary>
    public class PrototypeBootstrap : MonoBehaviour
    {
        // Paleta de 32 colores aprobada (docs/arte/paleta_propuesta_32.hex del proyecto de diseño)
        static readonly string[] PaletteHex =
        {
            "000000", "340000", "1c200c", "181c30", "4c1008", "442c0c", "303424", "283440",
            "642030", "7c181c", "543820", "48403c", "404460", "982420", "7c3c18", "6c4038",
            "50541c", "60546c", "605c48", "8c5048", "845c28", "70784c", "6c7874", "847c34",
            "a86c50", "88885c", "94948c", "e08c30", "b4a080", "b8b8b8", "d4d0cc", "f0dc9c",
        };

        const int LowW = 480, LowH = 270;       // resolución interna de pixelado (PROTOTIPO)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SceneManager.GetActiveScene().name != "SampleScene") return;
            if (FindFirstObjectByType<PrototypeBootstrap>() != null) return;
            new GameObject("TBD_Prototype").AddComponent<PrototypeBootstrap>();
        }

        // ---------------------------------------------------------------- mundos de simulación
        World _worldA;                 // el que se ve
        World _worldB;                 // copia sin dibujo para la prueba de desincronización
        SimRootGroup _groupA, _groupB;
        Entity _rootA, _rootB;
        float _acc;
        bool _paused;
        bool _checkEnabled = true;
        bool _desync;
        uint _desyncTick;
        EntityQuery _zombieQuery;
        int _walking, _falling, _settled, _total;
        float _statsTimer;

        // ---------------------------------------------------------------- presentación
        Camera _cam;
        RenderTexture _lowRt;
        Material _paletteMat;
        float _orthoSize = 18f;
        Vector3 _focus = new Vector3(66f, 0f, 64f);
        Transform[] _trainParts;
        GameObject _gate;
        float _fpsAcc;
        int _fpsFrames;
        float _fps;

        void Start()
        {
            SetupVisualAssets();
            SetupLevelObjects();
            SetupCamera();

            _worldA = World.DefaultGameObjectInjectionWorld;
            _groupA = SimWorldSetup.CreateSystems(_worldA);
            _rootA = SimWorldSetup.Populate(_worldA.EntityManager);
            _zombieQuery = _worldA.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Zombie>());

            _worldB = new World("TBD_SimB");
            _groupB = SimWorldSetup.CreateSystems(_worldB);
            _rootB = SimWorldSetup.Populate(_worldB.EntityManager);
        }

        void OnDestroy()
        {
            if (_lowRt != null) _lowRt.Release();
            if (_worldB != null && _worldB.IsCreated) _worldB.Dispose();
        }

        // ================================================================ bucle
        void Update()
        {
            HandleInput();

            if (!_paused)
            {
                _acc += Time.unscaledDeltaTime;
                int steps = 0;
                while (_acc >= SimConst.Dt && steps < 4)
                {
                    _acc -= SimConst.Dt;
                    StepSimulation();
                    steps++;
                }
                if (_acc > SimConst.Dt * 4f) _acc = 0f;
            }

            // Interpolación entre ticks (solo presentación)
            var em = _worldA.EntityManager;
            var clock = em.GetComponentData<SimClock>(_rootA);
            clock.Alpha = _acc / SimConst.Dt;
            em.SetComponentData(_rootA, clock);

            UpdateTrainVisual();
            UpdateCamera();
            UpdateStats();
            UpdateCapture();

            _fpsAcc += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAcc >= 0.5f) { _fps = _fpsFrames / _fpsAcc; _fpsAcc = 0f; _fpsFrames = 0; }
        }

        void StepSimulation()
        {
            _groupA.Update();
            if (_checkEnabled)
            {
                _groupB.Update();
                var ha = _worldA.EntityManager.GetComponentData<SimHash>(_rootA);
                var hb = _worldB.EntityManager.GetComponentData<SimHash>(_rootB);
                if (ha.Value != hb.Value || ha.Tick != hb.Tick)
                {
                    if (!_desync)
                    {
                        _desync = true;
                        _desyncTick = ha.Tick;
                        Debug.LogError($"[TBD] DESINCRONIZACIÓN en el tick {ha.Tick}: hash A={ha.Value:X16} hash B={hb.Value:X16}");
                    }
                }
                else if (ha.Tick % 300 == 0)
                {
                    Debug.Log($"[TBD] tick {ha.Tick}: hash idéntico en los dos mundos ({ha.Value:X16})");
                }
            }
        }

        /// <summary>Una orden entra por la cola única con un número de tick futuro, igual en los dos mundos.</summary>
        void Push(CmdType type, int arg = 0)
        {
            PushTo(_worldA.EntityManager, _rootA, type, arg);
            PushTo(_worldB.EntityManager, _rootB, type, arg);
        }

        static void PushTo(EntityManager em, Entity root, CmdType type, int arg)
        {
            uint tick = em.GetComponentData<SimClock>(root).Tick + SimConst.InputDelayTicks;
            em.GetBuffer<SimCommand>(root).Add(new SimCommand { Tick = tick, Type = type, Arg = arg });
        }

        void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Push(CmdType.SpawnRoof, 500);
            if (kb.digit2Key.wasPressedThisFrame) Push(CmdType.ToggleGate);
            if (kb.digit3Key.wasPressedThisFrame) Push(CmdType.ToggleTrain);
            if (kb.digit4Key.wasPressedThisFrame) Push(CmdType.SpawnWest, 3000);
            if (kb.pKey.wasPressedThisFrame) _paused = !_paused;
            if (kb.f9Key.wasPressedThisFrame) SaveScreenshot();
            if (kb.f10Key.wasPressedThisFrame) ToggleRecording();
            if (kb.f3Key.wasPressedThisFrame) { _checkEnabled = !_checkEnabled; _desync = false; }

            Vector3 pan = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) pan += new Vector3(1, 0, 1);
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) pan += new Vector3(-1, 0, -1);
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) pan += new Vector3(-1, 0, 1);
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) pan += new Vector3(1, 0, -1);
            _focus += pan.normalized * (_orthoSize * 1.2f * Time.unscaledDeltaTime);

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float s = mouse.scroll.ReadValue().y;
                if (s > 0f) _orthoSize = Mathf.Max(6f, _orthoSize * 0.85f);
                if (s < 0f) _orthoSize = Mathf.Min(70f, _orthoSize / 0.85f);
            }
        }

        // ================================================================ capturas y GIF (solo hitos importantes)
        // F9 = captura PNG de la imagen pixelada · F10 = empieza/para una grabación de fotogramas (se monta el GIF con
        // scripts/capturas/hacer_gif.py de mr-mak-workspace). Todo se guarda en <proyecto>/Captures/, fuera de Git.
        RenderTexture _palRt;
        bool _recording;
        float _recTimer;
        int _recIndex;
        string _recDir;
        const float RecInterval = 1f / 15f;

        static string CaptureRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));

        Texture2D GrabPixelFrame()
        {
            if (_palRt == null)
                _palRt = new RenderTexture(LowW, LowH, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            if (_paletteMat != null) Graphics.Blit(_lowRt, _palRt, _paletteMat);
            else Graphics.Blit(_lowRt, _palRt);
            var prev = RenderTexture.active;
            RenderTexture.active = _palRt;
            var tex = new Texture2D(LowW, LowH, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, LowW, LowH), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        void SaveScreenshot()
        {
            Directory.CreateDirectory(CaptureRoot);
            var tex = GrabPixelFrame();
            string path = Path.Combine(CaptureRoot, $"captura_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log($"[TBD] Captura guardada: {path}");
        }

        void ToggleRecording()
        {
            if (!_recording)
            {
                _recDir = Path.Combine(CaptureRoot, $"gif_{System.DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(_recDir);
                _recIndex = 0;
                _recTimer = 0f;
                _recording = true;
                Debug.Log($"[TBD] Grabando fotogramas en {_recDir} (F10 para parar)");
            }
            else
            {
                _recording = false;
                Debug.Log($"[TBD] Grabación parada: {_recIndex} fotogramas en {_recDir}. Monta el GIF con scripts/capturas/hacer_gif.py");
            }
        }

        void UpdateCapture()
        {
            if (!_recording) return;
            _recTimer += Time.unscaledDeltaTime;
            if (_recTimer < RecInterval) return;
            _recTimer -= RecInterval;
            var tex = GrabPixelFrame();
            File.WriteAllBytes(Path.Combine(_recDir, $"f_{_recIndex:D4}.png"), tex.EncodeToPNG());
            Destroy(tex);
            _recIndex++;
            if (_recIndex >= 450) ToggleRecording();      // tope de 30 s a 15 fps
        }

        // ================================================================ estadísticas para la pantalla
        void UpdateStats()
        {
            _statsTimer -= Time.unscaledDeltaTime;
            if (_statsTimer > 0f) return;
            _statsTimer = 0.5f;
            var zs = _zombieQuery.ToComponentDataArray<Zombie>(Allocator.Temp);
            _total = zs.Length;
            _walking = _falling = _settled = 0;
            for (int i = 0; i < zs.Length; i++)
            {
                switch (zs[i].State)
                {
                    case ZState.Walk: _walking++; break;
                    case ZState.Fall: _falling++; break;
                    default: _settled++; break;
                }
            }
            zs.Dispose();
        }

        // ================================================================ cámara y pixelado
        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _cam = go.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = HexColor("181c30");
            _lowRt = new RenderTexture(LowW, LowH, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, antiAliasing = 1 };
            _lowRt.Create();
            _cam.targetTexture = _lowRt;

            var shader = Shader.Find("Hidden/TBD/PaletteQuantize");
            if (shader != null)
            {
                _paletteMat = new Material(shader);
                var pal = new Vector4[32];
                for (int i = 0; i < 32; i++)
                {
                    var c = HexColor(PaletteHex[i]).linear;       // el proyecto está en espacio lineal
                    pal[i] = new Vector4(c.r, c.g, c.b, 1f);
                }
                _paletteMat.SetVectorArray("_Palette", pal);
            }
            else
            {
                Debug.LogWarning("[TBD] No se encontró el shader Hidden/TBD/PaletteQuantize: se muestra sin reducir a la paleta.");
            }
        }

        void UpdateCamera()
        {
            _cam.orthographicSize = _orthoSize;
            Vector3 dir = new Vector3(1f, -1f, 1f).normalized;      // mirando en diagonal, como un RTS isométrico
            _cam.transform.position = _focus - dir * 120f;
            _cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 400f;
        }

        void OnGUI()
        {
            // Dibujo de la textura de baja resolución ampliada con filtro de punto y reducida a la paleta
            if (Event.current.type == EventType.Repaint && _lowRt != null)
            {
                float sw = Screen.width, sh = Screen.height;
                float scale = Mathf.Min(sw / LowW, sh / LowH);
                float w = LowW * scale, h = LowH * scale;
                var rect = new Rect((sw - w) * 0.5f, (sh - h) * 0.5f, w, h);
                if (_paletteMat != null) Graphics.DrawTexture(rect, _lowRt, _paletteMat);
                else GUI.DrawTexture(rect, _lowRt, ScaleMode.StretchToFill, false);
            }

            var sb = new StringBuilder();
            sb.AppendLine("PROTOTIPO DE ZONA · TBD");
            sb.AppendLine($"FPS {_fps:0}   zombis {_total}  (andando {_walking} · cayendo {_falling} · apilados {_settled})");
            if (_worldA != null && _worldA.IsCreated)
            {
                var em = _worldA.EntityManager;
                var clock = em.GetComponentData<SimClock>(_rootA);
                var hash = em.GetComponentData<SimHash>(_rootA);
                var info = em.GetComponentData<ZoneInfo>(_rootA);
                var train = em.GetComponentData<TrainState>(_rootA);
                sb.AppendLine($"tick {clock.Tick}   hash {hash.Value:X16}   puerta {(info.GateOpen ? "ABIERTA" : "cerrada")}   tren {(train.Speed > 0 ? "en marcha" : "parado")}");
            }
            string check = !_checkEnabled ? "desactivada (F3)"
                : _desync ? $"¡DESINCRONIZADA en el tick {_desyncTick}!" : "correcta (dos mundos, mismo hash)";
            sb.AppendLine($"Prueba de desincronización: {check}");
            sb.AppendLine("1 tejado · 2 puerta · 3 tren · 4 más zombis · WASD mover · rueda zoom · P pausa · F9 captura · F10 grabar GIF" + (_recording ? "  ● GRABANDO" : ""));
            GUI.Label(new Rect(10, 8, 1100, 120), sb.ToString());
        }

        // ================================================================ decorado con primitivas
        Material _mGround, _mWall, _mRoof, _mRail, _mSleeper, _mTrain, _mWagon, _mGate;

        static Color HexColor(string hex)
        {
            int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f);
        }

        static Material Mat(string hex)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", HexColor(hex));
            m.SetFloat("_Smoothness", 0f);
            return m;
        }

        void SetupVisualAssets()
        {
            _mGround = Mat("50541c"); _mWall = Mat("88885c"); _mRoof = Mat("6c7874"); _mRail = Mat("94948c");
            _mSleeper = Mat("442c0c"); _mTrain = Mat("303424"); _mWagon = Mat("605c48"); _mGate = Mat("7c3c18");

            // Zombi: cápsula de Unity (escalada a 0,5 x 1,8 m en RenderAttachSystem)
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Mesh capsule = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            RenderAssets.Set(capsule, Mat("70784c"));

            // Luz direccional
            if (FindFirstObjectByType<Light>() == null)
            {
                var l = new GameObject("Sol").AddComponent<Light>();
                l.type = LightType.Directional;
                l.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        GameObject Box(string name, Vector3 center, Vector3 size, Material m, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        void SetupLevelObjects()
        {
            int W = SimConst.GridW, H = SimConst.GridH;
            Box("Suelo", new Vector3(W * 0.5f, -0.25f, H * 0.5f), new Vector3(W, 0.5f, H), _mGround);

            // Edificio con tejado
            Box("Edificio", new Vector3((Level.RoofMinX + Level.RoofMaxX + 1) * 0.5f, Level.RoofH * 0.5f, (Level.RoofMinY + Level.RoofMaxY + 1) * 0.5f),
                new Vector3(Level.RoofMaxX - Level.RoofMinX + 1, Level.RoofH, Level.RoofMaxY - Level.RoofMinY + 1), _mRoof);

            // Muro del fuerte: cuatro lados de 2 m de grosor
            float wh = Level.WallH, t = Level.WallThickness;
            float x0 = Level.FortMinX, x1 = Level.FortMaxX + 1, y0 = Level.FortMinY, y1 = Level.FortMaxY + 1;
            Box("Muro sur", new Vector3((x0 + x1) * 0.5f, wh * 0.5f, y0 + t * 0.5f), new Vector3(x1 - x0, wh, t), _mWall);
            Box("Muro norte", new Vector3((x0 + x1) * 0.5f, wh * 0.5f, y1 - t * 0.5f), new Vector3(x1 - x0, wh, t), _mWall);
            Box("Muro este", new Vector3(x1 - t * 0.5f, wh * 0.5f, (y0 + y1) * 0.5f), new Vector3(t, wh, y1 - y0), _mWall);
            // Lado oeste en tres tramos, con la puerta en medio
            float gy0 = Level.GateMinY, gy1 = Level.GateMaxY + 1;
            Box("Muro oeste sur", new Vector3(x0 + t * 0.5f, wh * 0.5f, (y0 + gy0) * 0.5f), new Vector3(t, wh, gy0 - y0), _mWall);
            Box("Muro oeste norte", new Vector3(x0 + t * 0.5f, wh * 0.5f, (gy1 + y1) * 0.5f), new Vector3(t, wh, y1 - gy1), _mWall);
            _gate = Box("Puerta", new Vector3(x0 + t * 0.5f, wh * 0.5f, (gy0 + gy1) * 0.5f), new Vector3(t, wh, gy1 - gy0), _mGate);

            // Vía: dos raíles y traviesas, de oeste a este por el centro del fuerte
            float ty = Level.TrackY + 0.5f;
            Box("Raíl A", new Vector3(55f, 0.12f, ty - 0.7f), new Vector3(90f, 0.1f, 0.12f), _mRail);
            Box("Raíl B", new Vector3(55f, 0.12f, ty + 0.7f), new Vector3(90f, 0.1f, 0.12f), _mRail);
            for (int x = 12; x < 100; x += 2)
                Box("Traviesa", new Vector3(x, 0.05f, ty), new Vector3(0.4f, 0.08f, 2f), _mSleeper);

            // Tren: locomotora y tres vagones
            _trainParts = new Transform[4];
            _trainParts[0] = Box("Locomotora", Vector3.zero, new Vector3(4f, 2.2f, 1.8f), _mTrain).transform;
            for (int i = 1; i < 4; i++)
                _trainParts[i] = Box("Vagón " + i, Vector3.zero, new Vector3(4.6f, 2f, 1.8f), _mWagon).transform;
        }

        void UpdateTrainVisual()
        {
            var em = _worldA.EntityManager;
            var train = em.GetComponentData<TrainState>(_rootA);
            var info = em.GetComponentData<ZoneInfo>(_rootA);
            float ty = Level.TrackY + 0.5f;
            // el tren avanza hacia +x; la locomotora va delante
            float x = train.X + 0.5f;
            _trainParts[0].position = new Vector3(x, 1.3f, ty);
            for (int i = 1; i < 4; i++)
                _trainParts[i].position = new Vector3(x - 2.3f - i * 5.0f + 2.5f, 1.2f, ty);
            if (_gate != null) _gate.SetActive(!info.GateOpen);
        }
    }
}
