using System.Collections;
using System.IO;
using System.Text;
using TBD.Net;
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
            // AfterSceneLoad solo se ejecuta al darle a Play; al cambiar de escena (mapa ↔ zona, hito 3) lo hace sceneLoaded.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GoldenRunner.Active) return;            // modo repetición dorada: sin juego
            if (scene.name != "SampleScene") return;
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
        bool _checkStale;                // B quedó atrás (F3 se apagó): la prueba ya no se puede reactivar
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
        float _paletteMix = 0.65f;                     // mezcla con la paleta en el mundo 3D (Lucía, D-4 de dirección visual): el ocre y el ámbar no viran
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

        // ---- hito 3: zona materializada desde el mapa de operaciones
        int _zoneNode = -1;                 // nodo de la red del que viene esta zona (-1 = prototipo suelto, 10.000 zombis)
        float _fadeStart;
        bool _leaving;
        public double LastMaterializeMs;
        CombatParams _combat;
        bool _hasTrain = true;
        long _ammoUnitsAtStart;

        static CombatParams LoadCombatParams() { return BalanceSource.Combat(); }
        // lo que tardó en crearse la horda de la zona (para Lucía: ¿basta un vuelo de 0,8 s?)

        bool _cutting, _ambush;               // zona de emboscada en un desmonte (D-N38)
        int _ambushService = -1, _ambushEdge = -1;

        IEnumerator Start()
        {
            Application.targetFrameRate = -1;        // el mapa limita a 60; en la zona se mide sin tope
            _cutting = NetSession.PendingAmbush && NetSession.A != null && NetSession.A.Encounter.Active;
            NetSession.PendingAmbush = false;
            if (_cutting) _focus = new Vector3(CuttingLevel.LocoX - 10f, 0f, Level.TrackY);
            SetupVisualAssets();
            SetupLevelObjects();
            SetupCameras();
            SetupFloodlights();                      // después de las cámaras: el halo usa el material aditivo de la capa HD
            SetupPost();
            _fx = FxHd.Create(_fxMat);

            int zombies = SimWorldSetup.InitialZombies;
            bool siege = false;
            uint seed = SimWorldSetup.Seed;
            if (_cutting)
            {
                var enc = NetSession.A.Encounter;
                _ambush = true; _ambushService = enc.Service; _ambushEdge = enc.Edge;
                _zoneNode = NetSession.A.Graph.Edges[enc.Edge].A;                    // para colocar la cámara del mapa al volver
                zombies = enc.Taken * NetWorld.ZombiesPerPermil;
                seed = (uint)(enc.Cell * 7919 + enc.Edge * 131 + 1);
                _orthoSize = 90f; _targetOrtho = 26f;
            }
            else if (NetSession.PendingZoneNode >= 0)
            {
                // La orden Materialize se aplica en el siguiente tick de la red (también en pausa). Se espera a que esté; si en 10 s
                // no ha llegado, se vuelve al mapa en vez de crear una zona vacía que perdería masa (QA Q13).
                float t0 = Time.realtimeSinceStartup;
                while (NetSession.A.ActiveZoneNode != NetSession.PendingZoneNode && Time.realtimeSinceStartup - t0 < 10f) yield return null;
                if (NetSession.A.ActiveZoneNode != NetSession.PendingZoneNode)
                {
                    Debug.LogError("[TBD] La orden Materialize no llegó en 10 s: se vuelve al mapa sin crear la zona.");
                    NetSession.ReturnFromNode = NetSession.PendingZoneNode;
                    NetSession.PendingZoneNode = -1;
                    SceneManager.LoadScene("Union");
                    yield break;
                }
                var net = NetSession.A;
                _zoneNode = NetSession.PendingZoneNode;
                NetSession.PendingZoneNode = -1;
                zombies = net.ActiveZoneTaken * NetWorld.ZombiesPerPermil;
                siege = net.ActiveZoneTaken >= NetWorld.SiegeAt;
                seed = (uint)(_zoneNode * 7919 + net.ActiveZoneSinceTick * 31 + 1);    // semilla del nodo y del tick (D-N07)
                _orthoSize = 70f;                                                       // vuelo de cámara: de lejos a cerca
                _targetOrtho = 18f;
            }
            _fadeStart = Time.realtimeSinceStartup;

            // Combate: parámetros de balance_combate.csv (Hugo). Munición: la del tren en el andén y la estación (unidades × 2.000 disparos)
            // o 20 u en el prototipo suelto.
            _combat = LoadCombatParams();
            long ammoUnits = 20;
            if (_ambush) ammoUnits = NetSession.A.Services[_ambushService].Cargo[(int)Res.Municion];   // en la vía solo hay la munición del tren
            else if (_zoneNode >= 0) ammoUnits = NetSession.A.AmmoAvailableAt(_zoneNode);   // carga del tren en el andén + estación
            _ammoUnitsAtStart = ammoUnits;
            long shots = ammoUnits * _combat.ShotsPerUnit;
            bool hasTrain = true;
            if (_zoneNode >= 0 && !_ambush)
            {
                hasTrain = false;
                foreach (var sv in NetSession.A.Services)
                    if (sv.AtStop && sv.Stops.Count > 0 && sv.Stops[sv.StopIndex].Node == _zoneNode) hasTrain = true;
            }
            _hasTrain = hasTrain;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            _worldA = World.DefaultGameObjectInjectionWorld;
            _groupA = SimWorldSetup.CreateSystems(_worldA);
            var kind = _cutting ? ZoneKind.Cutting : ZoneKind.Station;
            _rootA = SimWorldSetup.Populate(_worldA.EntityManager, zombies, siege, seed, _combat, shots, hasTrain, kind);
            sw.Stop();
            LastMaterializeMs = sw.Elapsed.TotalMilliseconds;
            Debug.Log($"[TBD] Zona {(_zoneNode >= 0 ? NetSession.A.Graph.Nodes[_zoneNode].Name : "suelta")}: {zombies} zombis ({(siege ? "asedio" : "deriva")}) creados en {LastMaterializeMs:0.0} ms");
            _zombieQuery = _worldA.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Zombie>());

            _worldB = new World("TBD_SimB");
            _groupB = SimWorldSetup.CreateSystems(_worldB);
            _rootB = SimWorldSetup.Populate(_worldB.EntityManager, zombies, siege, seed, _combat, shots, hasTrain, kind);
        }

        void OnDestroy()
        {
            if (_lowRt != null) _lowRt.Release();
            if (_worldB != null && _worldB.IsCreated) _worldB.Dispose();
            // El mundo A es el de Entities por defecto, que sobrevive a la escena: se quitan los sistemas y las entidades de la zona
            // para que al volver a entrar no se dupliquen (hito 3: se entra y se sale de las zonas muchas veces).
            if (_worldA != null && _worldA.IsCreated && _groupA != null)
            {
                var em = _worldA.EntityManager;
                var handles = _groupA.GetAllSystems();
                for (int i = 0; i < handles.Length; i++) _worldA.DestroySystem(handles[i]);
                handles.Dispose();
                _worldA.DestroySystemManaged(_groupA);
                em.DestroyEntity(em.CreateEntityQuery(ComponentType.ReadOnly<Zombie>()));
                em.DestroyEntity(em.CreateEntityQuery(ComponentType.ReadOnly<ZoneTag>()));
            }
        }

        /// <summary>
        /// Volver al mapa (tecla M o alejar la cámara al máximo): los zombis vivos vuelven a la mancha de su celda (D-N05) con la
        /// orden Dematerialize, y se funde el último fotograma de la zona sobre el mapa.
        /// </summary>
        public void LeaveToMap()
        {
            if (_zoneNode < 0 || _leaving) return;
            _leaving = true;
            int alive = _zombieQuery.CalculateEntityCount();
            var ammo = _worldA.EntityManager.GetComponentData<ZoneAmmo>(_rootA);
            int usedUnits = (int)((ammo.ShotsFired + _combat.ShotsPerUnit - 1) / _combat.ShotsPerUnit);   // redondeo hacia arriba: una caja empezada se gasta
            if (_ambush)
            {
                // D-N38: la emboscada se resuelve en la red: vuelven los supervivientes, el tren sigue y la munición sale de su carga
                NetSession.Push(TBD.Net.NetCmdType.ResolveAmbush, _ambushService | (usedUnits << 8), 0, alive / NetWorld.ZombiesPerPermil);
                NetSession.ReturnFromNode = _zoneNode;
                StartCoroutine(LeaveRoutine());
                return;
            }
            if (usedUnits > 0) NetSession.Push(TBD.Net.NetCmdType.ConsumeAmmo, _zoneNode, 0, usedUnits);
            // Diseño del hito 3 (Lucía): si se fuerza la salida, la tropa a pie se queda atrás. Hoy se pierde; cuando la tropa exista en
            // la red (S0) quedará como guarnición del nodo.
            int leftBehind = 0;
            var sq = _worldA.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Soldier>());
            var sa = sq.ToComponentDataArray<Soldier>(Allocator.Temp);
            for (int i = 0; i < sa.Length; i++) if (sa[i].State == SoldierState.Deployed) leftBehind++;
            sa.Dispose();
            if (leftBehind > 0) Debug.LogWarning($"[TBD] Salida forzada: {leftBehind} soldados a pie se quedan atrás en {NetSession.A.Graph.Nodes[_zoneNode].Name}.");
            NetSession.Push(TBD.Net.NetCmdType.Dematerialize, _zoneNode, 0, alive / NetWorld.ZombiesPerPermil);
            NetSession.ReturnFromNode = _zoneNode;
            StartCoroutine(LeaveRoutine());
        }

        IEnumerator LeaveRoutine()
        {
            yield return NetSession.CaptureTransition();
            SceneManager.LoadScene("Union");
        }

        // ================================================================ bucle
        void Update()
        {
            if (_worldA == null) return;                 // la zona aún espera a la orden Materialize
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
            UpdateSoldierVisuals();
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
            if (_cutting)
            {
                if (kb.zKey.wasPressedThisFrame) Push(CmdType.TrainDrive, -1);      // marcha atrás: la salida de emergencia
                if (kb.xKey.wasPressedThisFrame) Push(CmdType.TrainDrive, 1);       // adelante: lo frena el tapón
                if (kb.cKey.wasPressedThisFrame) Push(CmdType.TrainDrive, 0);       // parar
            }
            if (kb.digit4Key.wasPressedThisFrame) Push(CmdType.SpawnWest, 3000);
            if (kb.digit5Key.wasPressedThisFrame) FireVolley();
            if (kb.digit6Key.wasPressedThisFrame) Push(CmdType.EmbarkSquad);      // D-N27: pelotón a bordo, dispara con el tren parado
            if (kb.digit0Key.wasPressedThisFrame) Push(CmdType.DeploySquad);      // desplegar a pie: arriesgado en campo abierto (P7)
            if (kb.kKey.wasPressedThisFrame) Push(CmdType.Reembark);              // reembarcar a los que estén a menos de 20 m del tren
            if (kb.digit7Key.wasPressedThisFrame) ApplyGrade(0);
            if (kb.digit8Key.wasPressedThisFrame) ApplyGrade(1);
            if (kb.digit9Key.wasPressedThisFrame) ApplyGrade(2);
            if (kb.leftBracketKey.wasPressedThisFrame) { _factor = Mathf.Max(2, _factor - 1); RebuildRt(); }
            if (kb.rightBracketKey.wasPressedThisFrame) { _factor = Mathf.Min(4, _factor + 1); RebuildRt(); }
            if (kb.commaKey.wasPressedThisFrame) _paletteMix = Mathf.Max(0f, _paletteMix - 0.25f);
            if (kb.periodKey.wasPressedThisFrame) _paletteMix = Mathf.Min(1f, _paletteMix + 0.25f);
            if (kb.pKey.wasPressedThisFrame) _paused = !_paused;
            if (kb.f3Key.wasPressedThisFrame) ToggleCheck();
            if (kb.mKey.wasPressedThisFrame) LeaveToMap();
            if (kb.f9Key.wasPressedThisFrame)
            {
                if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) SaveScreenshotWithMask(); else SaveScreenshot();
            }
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
                if (s != 0f)
                {
                    // hito 3: alejar más allá del máximo de la zona devuelve al mapa (histéresis: se volvió a entrar a < 2 km)
                    if (s < 0f && _targetOrtho >= 69.5f && _zoneNode >= 0) LeaveToMap();
                    _targetOrtho = Mathf.Clamp(_targetOrtho * Mathf.Pow(0.9f, s / 120f), 6f, 70f);
                }
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
        void ToggleCheck()
        {
            _checkEnabled = !_checkEnabled;
            _desync = false;
            // QA Q1: con F3 apagado el mundo B no avanza; al volver a encenderlo ya no es comparable con A (copiarlo exige el
            // guardado, hito 6). Se avisa y la prueba se queda apagada en vez de mostrar una desincronización falsa.
            if (_checkEnabled && _worldB.EntityManager.GetComponentData<SimClock>(_rootB).Tick != _worldA.EntityManager.GetComponentData<SimClock>(_rootA).Tick)
            {
                _checkEnabled = false;
                _checkStale = true;
                Debug.LogWarning("[TBD] El mundo B se quedó atrás mientras F3 estaba apagado: la prueba de desincronización solo vale desde el arranque (falta el guardado, hito 6).");
            }
        }

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
            var sq = _worldA.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Soldier>());
            var sa = sq.ToComponentDataArray<Soldier>(Allocator.Temp);
            _deployedSoldiers = 0;
            for (int i = 0; i < sa.Length; i++) if (sa[i].State == SoldierState.Deployed) _deployedSoldiers++;
            sa.Dispose();
        }

        int _deployedSoldiers;      // soldados a pie: si se sale de la zona con ellos fuera, se quedan atrás

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
            _bloom.threshold.Override(1.25f);              // solo brillan los efectos HDR; halo, no mancha (Lucía, 6-nov)
            _bloom.intensity.Override(0.8f);
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
                    _grade.colorFilter.Override(new Color(1f, 0.97f, 0.92f));   // día menos lavado: algo más de saturación cálida
                    _grade.postExposure.Override(0f);
                    _grade.saturation.Override(4f);
                    SetFloodlights(0f);
                    break;
                case 1:  // atardecer naranja
                    _grade.colorFilter.Override(new Color(1f, 0.8f, 0.62f));
                    _grade.postExposure.Override(0.1f);
                    _grade.saturation.Override(10f);
                    SetFloodlights(12f);
                    break;
                default: // noche casi monocroma
                    _grade.colorFilter.Override(new Color(0.8f, 0.84f, 1f));    // noche de luna; los focos cálidos sacan a la horda del fondo
                    _grade.postExposure.Override(-0.1f);
                    _grade.saturation.Override(-10f);
                    SetFloodlights(40f);
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
                var ammo = em.GetComponentData<ZoneAmmo>(_rootA);
                sb.AppendLine($"munición {ammo.ShotsLeft / (float)_combat.ShotsPerUnit:0.0} u ({ammo.ShotsFired} disparos)   ·   abatidos {ammo.Kills}   ·   soldados {_soldierViews.Count} (bajas {ammo.SoldiersLost})   ·   6 pelotón a bordo · 0 pelotón a pie");
            }
            string check = _checkStale ? "apagada: B se quedó atrás (se reactiva al reiniciar)" : !_checkEnabled ? "desactivada (F3)"
                : _desync ? $"¡DESINCRONIZADA en el tick {_desyncTick}!" : "correcta (dos mundos, mismo hash)";
            sb.AppendLine($"Desincronización: {check}");
            sb.AppendLine($"Pixelado 1/{_factor}   ·   paleta {_paletteMix * 100:0}%   ·   gradación {GradeNames[_gradeIndex]}" + (_recording ? "   <color=#ff5a5a>● GRABANDO</color>" : ""));
            sb.AppendLine("1 tejado · 2 puerta · 3 tren · 4 más zombis · 5 trazadoras · B muros libres" + (_buildMode ? " <color=#ffd25a>(MODO MURO: clics = puntos; clic derecho corta)</color>" : ""));
            sb.AppendLine("WASD mover · Q E girar · R F inclinar · rueda zoom · botón derecho girar · 7/8/9 hora · [ ] pixelado · , . paleta · F9 captura · F10 GIF");

            float pad = 10f, w = Screen.width * 0.7f, h = _hudStyle.lineHeight * 7.3f;
            GUI.Box(new Rect(pad, pad, w, h), GUIContent.none, _panelStyle);
            GUI.Label(new Rect(pad * 2f, pad * 1.4f, w - pad * 2f, h), sb.ToString(), _hudStyle);
            if (_zoneNode >= 0)
            {
                var net = NetSession.A;
                GUI.Label(new Rect(pad * 2f, h + pad * 1.6f, w, _hudStyle.lineHeight * 1.5f),
                    $"{ZoneTitle(net)} · día {net.Tick / TBD.Net.NetConst.DayTicks + 1} · {TBD.Net.NetConst.Clock(net.Tick)} · la red sigue corriendo · M o alejar del todo: volver al mapa" + (_deployedSoldiers > 0 ? $"   <color=#ffb050>¡{_deployedSoldiers} soldados a pie se quedarían atrás! (K: reembarcar)</color>" : ""), _hudStyle);
            }
            NetSession.DrawFade(Time.realtimeSinceStartup - _fadeStart);
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

        /// <summary>
        /// Shift+F9 (petición de Sara, QA): en pausa y en el mismo fotograma guarda la captura normal y una «máscara» con los zombis en
        /// magenta puro #FF00FF, sin paleta ni contorno, para medir el contraste horda/suelo con docs/qa/legibilidad_horda.py.
        /// </summary>
        public void SaveScreenshotWithMask()
        {
            if (_capturing) return;
            Directory.CreateDirectory(CaptureRoot);
            string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            StartCoroutine(MaskRoutine(Path.Combine(CaptureRoot, $"captura_{stamp}.png"), Path.Combine(CaptureRoot, $"captura_{stamp}_mascara.png")));
        }

        IEnumerator MaskRoutine(string normalPath, string maskPath)
        {
            _capturing = true;
            bool wasPaused = _paused;
            _paused = true;
            yield return new WaitForEndOfFrame();
            var normal = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(normalPath, normal.EncodeToPNG());
            Destroy(normal);

            var em = _worldA.EntityManager;
            var q = em.CreateEntityQuery(typeof(Zombie), typeof(ZombieBaseColor));
            var ents = q.ToEntityArray(Allocator.Persistent);                 // Persistent: cruza fotogramas (Temp se libera al acabar el fotograma)
            var mat = RenderAssets.ZombieMaterial;
            var lit = mat.shader;
            mat.shader = Shader.Find("Universal Render Pipeline/Unlit");
            var magenta = new Unity.Mathematics.float4(1f, 0f, 1f, 1f);       // #FF00FF también en espacio lineal
            for (int i = 0; i < ents.Length; i++) em.SetComponentData(ents[i], new ZombieBaseColor { Value = magenta });
            float mix = _paletteMix;
            float outline = _quadMat.GetFloat("_OutlineStrength");
            _paletteMix = 0f;
            _quadMat.SetFloat("_PaletteMix", 0f);
            _quadMat.SetFloat("_OutlineStrength", 0f);
            bool vol = _volume != null && _volume.enabled;
            if (_volume != null) _volume.enabled = false;                     // sin gradación ni bloom: el magenta tiene que salir puro
            var fxD = _fxCam.GetUniversalAdditionalCameraData();
            var prD = _presentCam.GetUniversalAdditionalCameraData();
            bool fxPost = fxD.renderPostProcessing, prPost = prD.renderPostProcessing;
            fxD.renderPostProcessing = false;                                   // tampoco el tonemapping de URP
            prD.renderPostProcessing = false;
            yield return null;                                                // un fotograma para que Entities Graphics suba los colores
            yield return new WaitForEndOfFrame();
            var mask = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(maskPath, mask.EncodeToPNG());
            Destroy(mask);

            for (int i = 0; i < ents.Length; i++)
                em.SetComponentData(ents[i], new ZombieBaseColor { Value = RenderAttachSystem.ColorFor(em.GetComponentData<Zombie>(ents[i]).SimId) });
            ents.Dispose();
            mat.shader = lit;
            _paletteMix = mix;
            _quadMat.SetFloat("_PaletteMix", mix);
            _quadMat.SetFloat("_OutlineStrength", outline);
            if (_volume != null) _volume.enabled = vol;
            fxD.renderPostProcessing = fxPost;
            prD.renderPostProcessing = prPost;
            _paused = wasPaused;
            _capturing = false;
            Debug.Log($"[TBD] Captura y máscara guardadas: {normalPath} · {maskPath}");
        }

        /// <summary>Captura normal y máscara de EFECTOS (partículas de VFX_HD en #FF00FF a cuadro entero, sin bloom), en pausa (Sara).</summary>
        public void SaveFxMask(string tag)
        {
            if (_capturing) return;
            Directory.CreateDirectory(CaptureRoot);
            StartCoroutine(FxMaskRoutine(Path.Combine(CaptureRoot, $"efectos_{tag}.png"), Path.Combine(CaptureRoot, $"efectos_{tag}_mascara.png")));
        }

        IEnumerator FxMaskRoutine(string normalPath, string maskPath)
        {
            _capturing = true;
            bool wasPaused = _paused;
            _paused = true;
            float ts = Time.timeScale;
            Time.timeScale = 0f;                                              // las partículas se quedan quietas
            yield return new WaitForEndOfFrame();
            var normal = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(normalPath, normal.EncodeToPNG());
            Destroy(normal);
            var mask = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mask.SetColor("_BaseColor", Color.magenta);
            _fx.SetMaskMaterial(mask);
            bool vol = _volume != null && _volume.enabled;
            if (_volume != null) _volume.enabled = false;
            var fxD = _fxCam.GetUniversalAdditionalCameraData();
            var prD = _presentCam.GetUniversalAdditionalCameraData();
            bool fxPost = fxD.renderPostProcessing, prPost = prD.renderPostProcessing;
            fxD.renderPostProcessing = false;
            prD.renderPostProcessing = false;
            yield return new WaitForEndOfFrame();
            var m = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(maskPath, m.EncodeToPNG());
            Destroy(m);
            _fx.SetMaskMaterial(null);
            if (_volume != null) _volume.enabled = vol;
            fxD.renderPostProcessing = fxPost;
            prD.renderPostProcessing = prPost;
            Time.timeScale = ts;
            _paused = wasPaused;
            _capturing = false;
            Debug.Log($"[TBD] Captura y máscara de efectos: {normalPath}");
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
            // Revisión de arte de Lucía (5-nov): suelo claro y cálido, tejado de terracota (antes era del color del suelo)
            _mGround = Mat("b4a080"); _mWall = Mat("88885c"); _mRoof = Mat("a86c50"); _mRail = Mat("94948c");
            _mSleeper = Mat("442c0c"); _mTrain = Mat("303424"); _mWagon = Mat("605c48"); _mGate = Mat("7c3c18");

            // Zombi: cápsula de Unity (escalada a 0,5 x 1,8 m en RenderAttachSystem)
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Mesh capsule = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            RenderAssets.Set(capsule, Mat("ffffff"));       // el color real va por instancia (ZombieBaseColor)

            // Luz direccional
            // Sol bajo (unos 32°), cálido, con sombras proyectadas; la luz ambiente fría tiñe las sombras de azul (revisión de Lucía)
            var sun = FindFirstObjectByType<Light>();
            if (sun == null) sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(32f, -35f, 0f);
            sun.color = new Color(1f, 0.97f, 0.92f);          // casi neutro: el suelo ocre ya no vira a amarillo limón
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.36f, 0.52f);
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
        // ---------------------------------------------------------------- focos de noche (revisión de arte de Lucía, punto 5a)
        readonly System.Collections.Generic.List<Light> _floodLights = new System.Collections.Generic.List<Light>();
        readonly System.Collections.Generic.List<Transform> _halos = new System.Collections.Generic.List<Transform>();

        /// <summary>
        /// Tres focos en el muro oeste del fuerte, hacia el campo por donde llega la horda. La luz es un foco de URP (Forward+) que
        /// se dibuja en la capa pixelada, así que los zombis dentro del cono salen claros sobre el fondo oscuro. La lámpara lleva un
        /// halo pequeño en la capa de efectos HD, que el bloom derrama. Se encienden de noche (y a media potencia al atardecer).
        /// </summary>
        void SetupFloodlights()
        {
            var amber = HexColor("e08c30");
            float[] ys = { Level.FortY0 + 6f, (Level.GateY0 + Level.GateY1) * 0.5f, Level.FortY1 - 6f };
            foreach (float y in ys)
            {
                var pos = new Vector3(Level.FortX0, Level.WallH + 1.6f, y);
                var lgo = new GameObject("Foco");
                lgo.transform.SetParent(transform, false);
                lgo.transform.position = pos;
                lgo.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, -0.55f, 0f));
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = amber;
                l.range = 45f;
                l.spotAngle = 78f;
                l.innerSpotAngle = 40f;
                l.intensity = 0f;
                l.shadows = LightShadows.None;                  // sin sombras: un foco con sombras cuesta un pase más por luz
                _floodLights.Add(l);
                Box("Poste del foco", pos + new Vector3(0.3f, -0.8f, 0f), new Vector3(0.25f, 1.6f, 0.25f), _mRail);

                var h = new GameObject("Halo del foco");
                h.layer = LayerFx;
                h.transform.SetParent(transform, false);
                h.transform.position = pos + new Vector3(-0.35f, 0f, 0f);
                var mesh = new Mesh
                {
                    vertices = new[] { new Vector3(-0.9f, -0.9f, 0), new Vector3(0.9f, -0.9f, 0), new Vector3(-0.9f, 0.9f, 0), new Vector3(0.9f, 0.9f, 0) },
                    uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) },
                    colors = new[] { amber, amber, amber, amber },
                    triangles = new[] { 0, 2, 1, 1, 2, 3 },
                };
                h.AddComponent<MeshFilter>().sharedMesh = mesh;
                var hm = new Material(_fxMat);
                hm.SetFloat("_Intensity", 5f);
                h.AddComponent<MeshRenderer>().sharedMaterial = hm;
                h.SetActive(false);
                _halos.Add(h.transform);
            }
        }

        void SetFloodlights(float intensity)
        {
            foreach (var l in _floodLights) l.intensity = intensity;
            foreach (var h in _halos) h.gameObject.SetActive(intensity > 0f);
        }

        void LateUpdate()
        {
            if (_fxCam == null) return;
            foreach (var h in _halos) if (h.gameObject.activeSelf) h.rotation = _fxCam.transform.rotation;   // el halo mira a la cámara
        }

        /// <summary>Título de la zona en pantalla: una emboscada lleva el nombre de su punto real (Lucía), no el del nodo más cercano.</summary>
        string ZoneTitle(NetWorld net)
        {
            if (_ambush && _ambushEdge >= 0)
            {
                var e = net.Graph.Edges[_ambushEdge];
                string punto = e.Ambush.Count > 0 ? e.Ambush[0].Name : net.Graph.Nodes[e.A].Name + "–" + net.Graph.Nodes[e.B].Name;
                return "EMBOSCADA · " + punto.ToUpper();
            }
            return "ZONA " + net.Graph.Nodes[_zoneNode].Name.ToUpper();
        }

        /// <summary>Color de un escalón del talud: más oscuro cuanto más alto (para leer el relieve del desmonte).</summary>
        static string BankColor(float h)
        {
            float k = Mathf.Clamp01(h / CuttingLevel.Depth);
            int r = (int)Mathf.Lerp(0xa0, 0x5a, k), g = (int)Mathf.Lerp(0x84, 0x48, k), bl = (int)Mathf.Lerp(0x58, 0x30, k);
            return r.ToString("x2") + g.ToString("x2") + bl.ToString("x2");
        }

        void SetupLevelObjects()
        {
            Box("Suelo", new Vector3(SimConst.WorldW * 0.5f, -0.25f, SimConst.WorldH * 0.5f), new Vector3(SimConst.WorldW, 0.5f, SimConst.WorldH), _mGround);

            foreach (var s in _cutting ? CuttingLevel.CreateSolids() : Level.CreateSolids())
            {
                if (s.Kind == 1)
                {
                    var c = (s.A + s.B) * 0.5f;
                    var size = s.B - s.A;
                    Box(_cutting ? "Talud" : "Edificio", new Vector3(c.x, s.Height * 0.5f, c.y), new Vector3(size.x, s.Height, size.y), _cutting ? Mat(BankColor(s.Height)) : _mRoof);
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
            float railX = _cutting ? SimConst.WorldW * 0.5f : 55f, railLen = _cutting ? SimConst.WorldW : 90f;
            Box("Raíl A", new Vector3(railX, 0.12f, ty - 0.7f), new Vector3(railLen, 0.1f, 0.12f), _mRail);
            Box("Raíl B", new Vector3(railX, 0.12f, ty + 0.7f), new Vector3(railLen, 0.1f, 0.12f), _mRail);
            for (int x = _cutting ? 2 : 12; x < (_cutting ? (int)SimConst.WorldW : 100); x += 2)
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
            pt.x = Mathf.Clamp(pt.x, 2f, SimConst.WorldW - 2f);
            pt.z = Mathf.Clamp(pt.z, 2f, SimConst.WorldH - 2f);
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

        // ---------------------------------------------------------------- tropas (solo presentación)
        readonly System.Collections.Generic.Dictionary<uint, Transform> _soldierViews = new System.Collections.Generic.Dictionary<uint, Transform>();
        readonly System.Collections.Generic.HashSet<uint> _seenSoldiers = new System.Collections.Generic.HashSet<uint>();
        Material _mSoldier, _mGunner;
        EntityQuery _soldierQuery;

        void UpdateSoldierVisuals()
        {
            if (_worldA == null) return;
            var em = _worldA.EntityManager;
            if (_soldierQuery == default) _soldierQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Soldier>());
            if (_mSoldier == null) { _mSoldier = Mat("d4d0cc"); _mGunner = Mat("e08c30"); }
            var ss = _soldierQuery.ToComponentDataArray<Soldier>(Allocator.Temp);
            _seenSoldiers.Clear();
            for (int i = 0; i < ss.Length; i++)
            {
                var s = ss[i];
                _seenSoldiers.Add(s.SimId);
                if (!_soldierViews.TryGetValue(s.SimId, out var tr))
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.name = "Soldado " + s.SimId;
                    Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(transform, false);
                    go.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
                    go.GetComponent<Renderer>().sharedMaterial = s.Kind == SoldierKind.MachineGun ? _mGunner : _mSoldier;
                    tr = go.transform;
                    _soldierViews[s.SimId] = tr;
                }
                tr.position = new Vector3(s.Pos.x, s.Pos.y + 0.9f, s.Pos.z);
                tr.rotation = Quaternion.Euler(0f, s.Heading * Mathf.Rad2Deg, 0f);
                // acaba de disparar: fogonazo y trazadora en la capa de efectos HD (solo vista)
                int cd = s.Kind == SoldierKind.MachineGun ? _combat.MgCooldown : _combat.RifleCooldown;
                if (s.Cooldown == cd && _fx != null)
                {
                    var dir = new Vector3(Mathf.Sin(s.Heading), 0f, Mathf.Cos(s.Heading));
                    var aim = new Vector3(s.AimPoint.x, s.AimPoint.y + 1f, s.AimPoint.z);
                    _fx.Shot(tr.position + new Vector3(0f, 0.6f, 0f) + dir * 0.5f, aim);
                    if (s.LastHit == 1) Impact(aim);
                }
            }
            ss.Dispose();
            if (_soldierViews.Count > _seenSoldiers.Count)
            {
                var gone = new System.Collections.Generic.List<uint>();
                foreach (var kv in _soldierViews) if (!_seenSoldiers.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (var id in gone) { Destroy(_soldierViews[id].gameObject); _soldierViews.Remove(id); }
            }
        }

        // impacto (pauta de Lucía): soplo de sangre o polvo de 2-3 px durante 0,2 s sobre el zombi alcanzado, EN LA CAPA PIXELADA
        ParticleSystem _impacts;

        void Impact(Vector3 at)
        {
            if (_impacts == null)
            {
                var go = new GameObject("Impactos (capa pixelada)");
                go.transform.SetParent(transform, false);
                _impacts = go.AddComponent<ParticleSystem>();
                var main = _impacts.main;
                main.playOnAwake = false; main.loop = false; main.maxParticles = 600;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = _impacts.emission; em.enabled = false;
                var sh = _impacts.shape; sh.enabled = false;
                var r = go.GetComponent<ParticleSystemRenderer>();
                var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                m.SetColor("_BaseColor", Color.white);
                r.sharedMaterial = m;
                _impacts.Play();
            }
            bool blood = Random.value < 0.7f;
            _impacts.Emit(new ParticleSystem.EmitParams
            {
                position = at, velocity = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.4f, 1.2f), Random.Range(-0.6f, 0.6f)),
                startLifetime = 0.2f, startSize = Random.Range(0.4f, 0.6f),
                startColor = blood ? (Color)HexColor("7c181c") : (Color)HexColor("b4a080"),
            }, 1);
        }

        void UpdateTrainVisual()
        {
            var em = _worldA.EntityManager;
            var train = em.GetComponentData<TrainState>(_rootA);
            var info = em.GetComponentData<ZoneInfo>(_rootA);
            float ty = Level.TrackY;
            foreach (var part in _trainParts) part.gameObject.SetActive(info.HasTrain);   // sin tren de la red en el andén, no hay tren
            // el tren avanza hacia +x; la locomotora va delante
            float x = train.X + 0.5f;
            _trainParts[0].position = new Vector3(x, 1.3f, ty);
            for (int i = 1; i < 4; i++)
                _trainParts[i].position = new Vector3(x - 2.3f - i * 5.0f + 2.5f, 1.2f, ty);
            if (_gate != null) _gate.SetActive(!info.GateOpen);
        }
    }
}
