using System.Collections;
using System.IO;
using System.Text;
using TBD.Sim;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// Prototipo de zona (hito 1 del GDD), solo con primitivas y sin arte: un fuerte con puerta, una vía con su tren y una
    /// horda en DOTS que se amontona contra el muro y cae desde un tejado. Se arranca solo al dar a Play en SampleScene.
    ///
    /// Render en capas (docs/arte/direccion-visual.md, v1.4):
    ///   1. Mundo: la cámara pinta a una textura de 1/N de la pantalla (N = 2, 3 o 4), se amplía con vecino más cercano,
    ///      con contorno de 1 px y acercamiento a la paleta de 32 colores.
    ///   2. Efectos de luz (trazadoras, fogonazos, chispas): partículas a resolución completa en la capa VFX_HD.
    ///   3. Postproceso a resolución completa: bloom, viñeta y gradación por hora (día, atardecer, noche).
    ///   4. Interfaz: nítida y moderna (OnGUI), encima de todo.
    ///
    /// Controles:  1 zombis en el tejado (500) · 2 abrir/cerrar la puerta · 3 arrancar/parar el tren · 4 más zombis (3.000)
    ///             5 descarga de trazadoras (solo efecto visual) · 7/8/9 día/atardecer/noche · [ ] factor de pixelado (2-4)
    ///             , . mezcla con la paleta · WASD/flechas mover · rueda zoom · F3 prueba de desincronización · P pausa
    ///             F9 captura · F10 grabar fotogramas para un GIF
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

        const int LayerPresent = 9;                 // cuadrilátero de presentación
        const int LayerFx = FxHd.Layer;             // 8: VFX_HD

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

        // ---------------------------------------------------------------- presentación en capas
        Camera _worldCam, _presentCam, _fxCam;
        RenderTexture _lowRt;
        Material _quadMat, _fxMat;
        Transform _quad;
        FxHd _fx;
        Volume _volume;
        ColorAdjustments _grade;
        Bloom _bloom;
        int _factor = 3;                              // factor de pixelado N (PROTOTIPO: 2-4)
        float _paletteMix = 1f;
        int _gradeIndex;
        int _lastW, _lastH;
        float _orthoSize = 18f;
        float _targetOrtho = 18f;
        float _yaw = 45f;                               // cámara libre: rotación, inclinación y zoom continuos
        float _pitch = 50f;
        Vector3 _focus = new Vector3(66f, 0f, 64f);
        bool _buildMode;
        bool _hasWallStart;
        Vector3 _wallStart;
        Transform[] _trainParts;
        GameObject _gate;
        float _fpsAcc;
        int _fpsFrames;
        float _fps;
        GUIStyle _hudStyle, _panelStyle;
        Texture2D _panelTex;

        void Start()
        {
            SetupVisualAssets();
            SetupLevelObjects();
            SetupCameras();
            SetupPost();
            _fx = FxHd.Create(_fxMat);

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
        void Push(CmdType type, int arg = 0, Unity.Mathematics.float2 p0 = default, Unity.Mathematics.float2 p1 = default)
        {
            PushTo(_worldA.EntityManager, _rootA, type, arg, p0, p1);
            PushTo(_worldB.EntityManager, _rootB, type, arg, p0, p1);
        }

        static void PushTo(EntityManager em, Entity root, CmdType type, int arg, Unity.Mathematics.float2 p0, Unity.Mathematics.float2 p1)
        {
            uint tick = em.GetComponentData<SimClock>(root).Tick + SimConst.InputDelayTicks;
            em.GetBuffer<SimCommand>(root).Add(new SimCommand { Tick = tick, Type = type, Arg = arg, P0 = p0, P1 = p1 });
        }

        void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Push(CmdType.SpawnRoof, 500);
            if (kb.digit2Key.wasPressedThisFrame) Push(CmdType.ToggleGate);
            if (kb.digit3Key.wasPressedThisFrame) Push(CmdType.ToggleTrain);
            if (kb.digit4Key.wasPressedThisFrame) Push(CmdType.SpawnWest, 3000);
            if (kb.digit5Key.wasPressedThisFrame) FireVolley();
            if (kb.digit7Key.wasPressedThisFrame) ApplyGrade(0);
            if (kb.digit8Key.wasPressedThisFrame) ApplyGrade(1);
            if (kb.digit9Key.wasPressedThisFrame) ApplyGrade(2);
            if (kb.leftBracketKey.wasPressedThisFrame) { _factor = Mathf.Max(2, _factor - 1); RebuildRt(); }
            if (kb.rightBracketKey.wasPressedThisFrame) { _factor = Mathf.Min(4, _factor + 1); RebuildRt(); }
            if (kb.commaKey.wasPressedThisFrame) _paletteMix = Mathf.Max(0f, _paletteMix - 0.25f);
            if (kb.periodKey.wasPressedThisFrame) _paletteMix = Mathf.Min(1f, _paletteMix + 0.25f);
            if (kb.pKey.wasPressedThisFrame) _paused = !_paused;
            if (kb.f3Key.wasPressedThisFrame) { _checkEnabled = !_checkEnabled; _desync = false; }
            if (kb.f9Key.wasPressedThisFrame) SaveScreenshot();
            if (kb.f10Key.wasPressedThisFrame) ToggleRecording();

            if (kb.bKey.wasPressedThisFrame) { _buildMode = !_buildMode; _hasWallStart = false; }
            if (kb.escapeKey.wasPressedThisFrame) { _buildMode = false; _hasWallStart = false; }

            // ---- cámara libre: rotación, inclinación y zoom continuos (GDD §11, v1.10)
            float dt = Time.unscaledDeltaTime;
            if (kb.qKey.isPressed) _yaw -= 70f * dt;
            if (kb.eKey.isPressed) _yaw += 70f * dt;
            if (kb.rKey.isPressed) _pitch = Mathf.Min(80f, _pitch + 40f * dt);
            if (kb.fKey.isPressed) _pitch = Mathf.Max(20f, _pitch - 40f * dt);

            Vector3 fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            Vector3 pan = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) pan += fwd;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) pan -= fwd;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) pan -= right;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) pan += right;
            _focus += pan.normalized * (_orthoSize * 1.2f * dt);

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float s = mouse.scroll.ReadValue().y;
                if (s != 0f) _targetOrtho = Mathf.Clamp(_targetOrtho * Mathf.Pow(0.9f, s / 120f), 6f, 70f);
                if (mouse.rightButton.isPressed && !_buildMode)
                {
                    Vector2 dd = mouse.delta.ReadValue();
                    _yaw += dd.x * 0.2f;
                    _pitch = Mathf.Clamp(_pitch - dd.y * 0.2f, 20f, 80f);
                }
                if (_buildMode)
                {
                    if (mouse.leftButton.wasPressedThisFrame) PlaceWallPoint(mouse.position.ReadValue());
                    if (mouse.rightButton.wasPressedThisFrame) _hasWallStart = false;
                }
            }
            _orthoSize = Mathf.Lerp(_orthoSize, _targetOrtho, 1f - Mathf.Exp(-12f * dt));
        }

        /// <summary>Descarga visual de trazadoras desde el tren (efecto de presentación, sin efecto en la simulación).</summary>
        void FireVolley()
        {
            Vector3 from = _trainParts[0].position + new Vector3(0f, 1.6f, 0f);
            Vector3 to = new Vector3(Level.FortX0 - 10f, 0f, Level.TrackY);
            _fx.Volley(from, to, 14);
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

        // ================================================================ cámaras y render en capas
        void SetupCameras()
        {
            // --- cámara del mundo: pinta a la textura de baja resolución (todo menos VFX_HD y la presentación)
            _worldCam = Camera.main;
            if (_worldCam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _worldCam = go.AddComponent<Camera>();
            }
            _worldCam.orthographic = true;
            _worldCam.clearFlags = CameraClearFlags.SolidColor;
            _worldCam.backgroundColor = HexColor("181c30");
            _worldCam.cullingMask = ~((1 << LayerFx) | (1 << LayerPresent));
            _worldCam.depth = 0;
            var worldData = _worldCam.GetUniversalAdditionalCameraData();
            worldData.renderPostProcessing = false;           // el postproceso va una sola vez, a resolución completa
            worldData.antialiasing = AntialiasingMode.None;

            // --- cámara de presentación: ve solo el cuadrilátero con el mundo ampliado
            var pgo = new GameObject("Camara_Presentacion");
            pgo.transform.SetParent(transform, false);
            _presentCam = pgo.AddComponent<Camera>();
            _presentCam.orthographic = true;
            _presentCam.orthographicSize = 0.5f;
            _presentCam.nearClipPlane = 0.1f;
            _presentCam.farClipPlane = 20f;
            _presentCam.clearFlags = CameraClearFlags.SolidColor;
            _presentCam.backgroundColor = Color.black;
            _presentCam.cullingMask = 1 << LayerPresent;
            _presentCam.depth = 10;
            _presentCam.transform.position = Vector3.zero;
            _presentCam.transform.rotation = Quaternion.identity;

            // --- cámara de efectos de luz: superpuesta, a resolución completa, ve solo VFX_HD
            var fgo = new GameObject("Camara_VFX_HD");
            fgo.transform.SetParent(transform, false);
            _fxCam = fgo.AddComponent<Camera>();
            _fxCam.orthographic = true;
            _fxCam.cullingMask = 1 << LayerFx;
            _fxCam.allowHDR = true;
            var fxData = _fxCam.GetUniversalAdditionalCameraData();
            fxData.renderType = CameraRenderType.Overlay;
            fxData.renderPostProcessing = true;

            var presentData = _presentCam.GetUniversalAdditionalCameraData();
            presentData.renderPostProcessing = true;
            presentData.antialiasing = AntialiasingMode.None;
            presentData.cameraStack.Add(_fxCam);
            _presentCam.allowHDR = true;

            // --- cuadrilátero de presentación con el shader de contorno y paleta
            var shader = Shader.Find("Hidden/TBD/PaletteQuantize");
            _quadMat = new Material(shader != null ? shader : Shader.Find("Unlit/Texture"));
            if (shader != null)
            {
                var pal = new Vector4[32];
                for (int i = 0; i < 32; i++)
                {
                    var c = HexColor(PaletteHex[i]).linear;       // el proyecto está en espacio lineal
                    pal[i] = new Vector4(c.r, c.g, c.b, 1f);
                }
                _quadMat.SetVectorArray("_Palette", pal);
            }
            else
            {
                Debug.LogWarning("[TBD] No se encontró el shader Hidden/TBD/PaletteQuantize: el mundo se ve sin contorno ni paleta.");
            }

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Presentacion";
            Destroy(quad.GetComponent<Collider>());
            quad.layer = LayerPresent;
            quad.transform.SetParent(transform, false);
            quad.transform.position = new Vector3(0f, 0f, 5f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = _quadMat;
            _quad = quad.transform;

            // --- material aditivo de los efectos HD
            var fxShader = Shader.Find("TBD/FxAdditive");
            _fxMat = new Material(fxShader != null ? fxShader : Shader.Find("Sprites/Default"));

            RebuildRt();
        }

        /// <summary>Reconstruye la textura del mundo a 1/N de la pantalla (mismo aspecto que la pantalla: sin bandas).</summary>
        void RebuildRt()
        {
            _lastW = Screen.width;
            _lastH = Screen.height;
            int w = Mathf.Max(64, _lastW / _factor);
            int h = Mathf.Max(36, _lastH / _factor);
            if (_lowRt != null && _lowRt.width == w && _lowRt.height == h) return;
            if (_lowRt != null)
            {
                _worldCam.targetTexture = null;
                _lowRt.Release();
                Destroy(_lowRt);
            }
            _lowRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, antiAliasing = 1 };
            _lowRt.Create();
            _worldCam.targetTexture = _lowRt;
            _quadMat.SetTexture("_MainTex", _lowRt);
        }

        void UpdateCamera()
        {
            if (Screen.width != _lastW || Screen.height != _lastH) RebuildRt();

            _worldCam.orthographicSize = _orthoSize;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 camPos = _focus - rot * Vector3.forward * 120f;
            // snapping a la rejilla de píxeles: solo visual (la simulación no sabe nada de la cámara)
            float pixelSize = 2f * _orthoSize / Mathf.Max(1, _lowRt.height);
            Vector3 local = Quaternion.Inverse(rot) * camPos;
            local.x = Mathf.Round(local.x / pixelSize) * pixelSize;
            local.y = Mathf.Round(local.y / pixelSize) * pixelSize;
            camPos = rot * local;
            _worldCam.transform.SetPositionAndRotation(camPos, rot);
            _worldCam.nearClipPlane = 0.1f;
            _worldCam.farClipPlane = 400f;

            // la cámara de efectos ve el mundo exactamente igual que la del mundo
            _fxCam.transform.SetPositionAndRotation(_worldCam.transform.position, _worldCam.transform.rotation);
            _fxCam.orthographicSize = _orthoSize;
            _fxCam.nearClipPlane = 0.1f;
            _fxCam.farClipPlane = 400f;

            // el cuadrilátero llena la vista de la cámara de presentación (alto 1, ancho = aspecto)
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            _quad.localScale = new Vector3(aspect, 1f, 1f);
            _quadMat.SetFloat("_PaletteMix", _paletteMix);
        }

        // ================================================================ postproceso a resolución completa
        static readonly string[] GradeNames = { "día", "atardecer", "noche" };

        void SetupPost()
        {
            var go = new GameObject("Volumen_Post");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // sin tonemapping: la paleta ya está decidida y no debe moverse
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.None);

            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(1f);                 // solo brillan los efectos HDR (> 1)
            _bloom.intensity.Override(1.4f);
            _bloom.scatter.Override(0.75f);
            _bloom.highQualityFiltering.Override(true);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.28f);
            vig.smoothness.Override(0.6f);

            _grade = profile.Add<ColorAdjustments>(true);
            _volume.sharedProfile = profile;
            ApplyGrade(0);
        }

        void ApplyGrade(int index)
        {
            _gradeIndex = index;
            switch (index)
            {
                case 0:  // día cálido mediterráneo
                    _grade.colorFilter.Override(new Color(1f, 0.97f, 0.9f));
                    _grade.postExposure.Override(0f);
                    _grade.saturation.Override(0f);
                    break;
                case 1:  // atardecer naranja
                    _grade.colorFilter.Override(new Color(1f, 0.72f, 0.5f));
                    _grade.postExposure.Override(-0.2f);
                    _grade.saturation.Override(10f);
                    break;
                default: // noche casi monocroma
                    _grade.colorFilter.Override(new Color(0.55f, 0.64f, 0.98f));
                    _grade.postExposure.Override(-0.7f);
                    _grade.saturation.Override(-45f);
                    break;
            }
        }

        // ================================================================ interfaz nítida (capa 4)
        void OnGUI()
        {
            if (_hudStyle == null)
            {
                _panelTex = new Texture2D(1, 1);
                _panelTex.SetPixel(0, 0, new Color(0.06f, 0.07f, 0.09f, 0.84f));
                _panelTex.Apply();
                _panelStyle = new GUIStyle { normal = { background = _panelTex } };
                _hudStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false };
                _hudStyle.normal.textColor = new Color(0.9f, 0.92f, 0.95f);
            }
            _hudStyle.fontSize = Mathf.Max(13, Screen.height / 62);

            var sb = new StringBuilder();
            sb.AppendLine("<b>PROTOTIPO DE ZONA · TBD</b>");
            sb.AppendLine($"{_fps:0} FPS   ·   zombis {_total}   (andando {_walking} · cayendo {_falling} · apilados {_settled})");
            if (_worldA != null && _worldA.IsCreated)
            {
                var em = _worldA.EntityManager;
                var clock = em.GetComponentData<SimClock>(_rootA);
                var hash = em.GetComponentData<SimHash>(_rootA);
                var info = em.GetComponentData<ZoneInfo>(_rootA);
                var train = em.GetComponentData<TrainState>(_rootA);
                sb.AppendLine($"tick {clock.Tick}   ·   hash {hash.Value:X16}   ·   puerta {(info.GateOpen ? "ABIERTA" : "cerrada")}   ·   tren {(train.Speed > 0 ? "en marcha" : "parado")}");
            }
            string check = !_checkEnabled ? "desactivada (F3)"
                : _desync ? $"¡DESINCRONIZADA en el tick {_desyncTick}!" : "correcta (dos mundos, mismo hash)";
            sb.AppendLine($"Desincronización: {check}");
            sb.AppendLine($"Pixelado 1/{_factor}   ·   paleta {_paletteMix * 100:0}%   ·   gradación {GradeNames[_gradeIndex]}" + (_recording ? "   <color=#ff5a5a>● GRABANDO</color>" : ""));
            sb.AppendLine("1 tejado · 2 puerta · 3 tren · 4 más zombis · 5 trazadoras · B muros libres" + (_buildMode ? " <color=#ffd25a>(MODO MURO: clics = puntos; clic derecho corta)</color>" : ""));
            sb.AppendLine("WASD mover · Q E girar · R F inclinar · rueda zoom · botón derecho girar · 7/8/9 hora · [ ] pixelado · , . paleta · F9 captura · F10 GIF");

            float pad = 10f, w = Screen.width * 0.7f, h = _hudStyle.lineHeight * 7.3f;
            GUI.Box(new Rect(pad, pad, w, h), GUIContent.none, _panelStyle);
            GUI.Label(new Rect(pad * 2f, pad * 1.4f, w - pad * 2f, h), sb.ToString(), _hudStyle);
        }

        // ================================================================ capturas y GIF (solo hitos importantes)
        // F9 = captura PNG de lo que ve el jugador (con efectos, postproceso e interfaz, a mitad de tamaño).
        // F10 = empieza/para una grabación de fotogramas; el GIF se monta con scripts/capturas/hacer_gif.py de
        // mr-mak-workspace. Todo se guarda en <proyecto>/Captures/, fuera de Git.
        bool _recording;
        bool _capturing;
        float _recTimer;
        int _recIndex;
        string _recDir;
        const float RecInterval = 1f / 15f;

        static string CaptureRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));

        void SaveScreenshot()
        {
            Directory.CreateDirectory(CaptureRoot);
            string path = Path.Combine(CaptureRoot, $"captura_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            StartCoroutine(CaptureRoutine(path));
            Debug.Log($"[TBD] Captura guardada en {path}");
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
            if (!_recording || _capturing) return;
            _recTimer += Time.unscaledDeltaTime;
            if (_recTimer < RecInterval) return;
            _recTimer -= RecInterval;
            string path = Path.Combine(_recDir, $"f_{_recIndex:D4}.png");
            _recIndex++;
            StartCoroutine(CaptureRoutine(path));
            if (_recIndex >= 450) ToggleRecording();      // tope de 30 s a 15 fps
        }

        IEnumerator CaptureRoutine(string path)
        {
            _capturing = true;
            yield return new WaitForEndOfFrame();
            var full = ScreenCapture.CaptureScreenshotAsTexture();
            int w = Mathf.Max(2, full.width / 2), h = Mathf.Max(2, full.height / 2);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            Graphics.Blit(full, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var half = new Texture2D(w, h, TextureFormat.RGB24, false);
            half.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            half.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, half.EncodeToPNG());
            Destroy(full);
            Destroy(half);
            _capturing = false;
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

        /// <summary>Decorado generado desde los mismos sólidos que usa la simulación (geometría continua, no celdas).</summary>
        void SetupLevelObjects()
        {
            float world = SimConst.WorldSize;
            Box("Suelo", new Vector3(world * 0.5f, -0.25f, world * 0.5f), new Vector3(world, 0.5f, world), _mGround);

            foreach (var s in Level.CreateSolids())
            {
                if (s.Kind == 1)
                {
                    var c = (s.A + s.B) * 0.5f;
                    var size = s.B - s.A;
                    Box("Edificio", new Vector3(c.x, s.Height * 0.5f, c.y), new Vector3(size.x, s.Height, size.y), _mRoof);
                }
                else
                {
                    var go = WallVisual(new Vector3(s.A.x, 0f, s.A.y), new Vector3(s.B.x, 0f, s.B.y), s.HalfThick, s.Height,
                        s.Id == Level.GateId ? _mGate : _mWall);
                    if (s.Id == Level.GateId) _gate = go;
                }
            }

            // Vía: dos raíles y traviesas, de oeste a este por el centro del fuerte
            float ty = Level.TrackY;
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

        /// <summary>Muro como caja con posición, rotación, grosor y altura libres (de A a B).</summary>
        GameObject WallVisual(Vector3 a, Vector3 b, float halfThick, float height, Material m)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            var go = Box("Muro", (a + b) * 0.5f + Vector3.up * (height * 0.5f), new Vector3(len + 2f * halfThick, height, 2f * halfThick), m);
            go.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg, 0f);
            return go;
        }

        /// <summary>Construcción libre: clic para cada punto de la polilínea; cada tramo es una orden BuildWall.</summary>
        void PlaceWallPoint(Vector2 screenPos)
        {
            // Rayo por coordenadas de vista (la cámara del mundo pinta a una textura, no a la pantalla)
            Ray ray = _worldCam.ViewportPointToRay(new Vector3(screenPos.x / Screen.width, screenPos.y / Screen.height, 0f));
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float t)) return;
            Vector3 pt = ray.GetPoint(t);
            pt.x = Mathf.Clamp(pt.x, 2f, SimConst.WorldSize - 2f);
            pt.z = Mathf.Clamp(pt.z, 2f, SimConst.WorldSize - 2f);
            pt.y = 0f;
            if (!_hasWallStart)
            {
                _wallStart = pt;
                _hasWallStart = true;
                return;
            }
            if ((pt - _wallStart).magnitude < 1f) return;
            Push(CmdType.BuildWall, 0, new Unity.Mathematics.float2(_wallStart.x, _wallStart.z), new Unity.Mathematics.float2(pt.x, pt.z));
            WallVisual(_wallStart, pt, Level.WallHalf * 0.5f, Level.WallH, _mWall);
            _wallStart = pt;
        }

        void UpdateTrainVisual()
        {
            var em = _worldA.EntityManager;
            var train = em.GetComponentData<TrainState>(_rootA);
            var info = em.GetComponentData<ZoneInfo>(_rootA);
            float ty = Level.TrackY;
            // el tren avanza hacia +x; la locomotora va delante
            float x = train.X + 0.5f;
            _trainParts[0].position = new Vector3(x, 1.3f, ty);
            for (int i = 1; i < 4; i++)
                _trainParts[i].position = new Vector3(x - 2.3f - i * 5.0f + 2.5f, 1.2f, ty);
            if (_gate != null) _gate.SetActive(!info.GateOpen);
        }
    }
}
