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
        bool _autoPix = true; float _factorNext;           // pixelado automático: a más zoom, píxeles más gordos, para que el 3D de cerca también se vea pixelado (Eugenio, 6-nov)
        int _factor = 3;                              // factor de pixelado N (PROTOTIPO: 2-4)
        float _paletteMix = 0.65f;                     // mezcla con la paleta en el mundo 3D (Lucía, D-4 de dirección visual): el ocre y el ámbar no viran
        int _gradeIndex;
        int _lastW, _lastH;
        float _orthoSize = 18f;
        float _targetOrtho = 18f;
        float _yaw = 45f;                               // cámara libre: rotación, inclinación y zoom continuos
        float _pitch = 55f;                           // inclinación por defecto 55° (Lucía, D-N61); la cámara es ortográfica: el FOV no aplica
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
        // ---------------------------------------------------------------- la horda como mancha (D-N61, Lucía): LOD de masa y sombra bajo la horda
        float[] _massTmp; Texture2D _massTex; Color32[] _massPix; float[] _massCnt; GameObject _massQuad; float _massNext;
        const int MassRes = 2;                  // metros por texel de la sombra de masa

        /// <summary>LOD con histéresis según los píxeles por metro del búfer pixelado, y sombra de masa (suelo más oscuro bajo la densidad).</summary>
        // ---------------------------------------------------------------- prueba de rendimiento (D-N61): TBD.exe -stress10k
        // Entra solo en una emboscada, mete 10.000 zombis en la trinchera con el tren disparando, mide FPS 40 s y escribe Captures/stress_10k_fps.txt
        static readonly bool Stress = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-stress10k") >= 0;
        bool _stressEmbarked; float _stressT0 = -1f, _stressNext; int _stressFrames; readonly System.Collections.Generic.List<float> _stressFps = new System.Collections.Generic.List<float>();

        void StressTick()
        {
            if (_stressT0 < 0f) { _stressT0 = Time.realtimeSinceStartup; Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0; }   // sin tope: queremos saber si pasa de 60
            float t = Time.realtimeSinceStartup - _stressT0;
            if (t >= 1f && !_stressEmbarked) { _stressEmbarked = true; Push(CmdType.EmbarkSquad); }       // el pelotón a bordo dispara con el tren parado (D-N27)
            if (t < 3f) return;
            if (_stressFrames == 0 && _stressNext == 0f)
            {
                foreach (var w in new[] { _worldA, _worldB })
                {
                    var em = w.EntityManager;
                    using (var qc = em.CreateEntityQuery(typeof(SimClock)))
                    {
                        var clock = qc.GetSingleton<SimClock>();
                        var rng = new Unity.Mathematics.Random(777u);
                        var ents = em.CreateEntity(em.CreateArchetype(typeof(Zombie)), 10000, Allocator.Temp);
                        for (int i = 0; i < 10000; i++)
                            em.SetComponentData(ents[i], Zombie.Create(clock.NextSimId++, new Unity.Mathematics.float2(90f + rng.NextFloat() * 90f, Level.TrackY - 7f + rng.NextFloat() * 14f), 0f));
                        ents.Dispose(); qc.SetSingleton(clock);
                    }
                }
                _targetOrtho = 22f; _stressNext = Time.realtimeSinceStartup + 1f; _stressT0 = Time.realtimeSinceStartup; return;
            }
            _stressFrames++;
            if (Time.realtimeSinceStartup >= _stressNext)
            {
                _stressFps.Add(_stressFrames); _stressFrames = 0; _stressNext += 1f;
                if (_stressFps.Count >= 40)
                {
                    var arr = _stressFps.ToArray(); System.Array.Sort(arr); float sum = 0f; foreach (var f in arr) sum += f;
                    var net = _worldA.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Zombie>()).CalculateEntityCount();
                    var ammo = _worldA.EntityManager.CreateEntityQuery(typeof(ZoneAmmo)).GetSingleton<ZoneAmmo>();
                    Directory.CreateDirectory(CaptureRoot);
                    File.WriteAllText(Path.Combine(CaptureRoot, "stress_10k_fps.txt"), $"zombis al final {net}\nabatidos {ammo.Kills} disparos {ammo.ShotsFired}\nFPS medio {sum / arr.Length:0.0} mínimo {arr[0]:0} p5 {arr[2]:0} máximo {arr[arr.Length - 1]:0}\nresolución {Screen.width}x{Screen.height} factor 1/{_factor}\n");
                    Application.Quit();
                }
            }
        }

        void UpdateHordePresentation()
        {
            if (_worldCam == null || _worldA == null) return;
            if (Stress) StressTick();
            if (_autoPix && Time.unscaledTime > _factorNext)                      // ~12 px por metro como máximo: cerca, factor 4-6; lejos, 3
            {
                float ideal = Screen.height / (24f * Mathf.Max(0.01f, _worldCam.orthographicSize));
                int want = Mathf.Clamp(Mathf.RoundToInt(ideal), 3, 6);
                if (want != _factor && Mathf.Abs(ideal - _factor) > 0.6f) { _factor = want; RebuildRt(); _factorNext = Time.unscaledTime + 0.5f; }
            }
            float pxPerM = _worldCam.pixelHeight / (2f * Mathf.Max(0.01f, _worldCam.orthographicSize));
            float bodyPx = 0.8f * pxPerM;                                          // anchura de hombros del zombi en el búfer
            bool lod = RenderAssets.LodOn ? bodyPx < 10f : bodyPx < 8f;            // entra a menos de 8 px (Eugenio quiere figuras, no manchas: el sprite grande aguanta más), sale a más de 10
            if (lod != RenderAssets.LodOn) { RenderAssets.LodOn = lod; RenderAssets.LodVersion++; }
            if (Time.unscaledTime < _massNext) return;
            _massNext = Time.unscaledTime + 0.2f;
            int w = (int)(SimConst.WorldW / MassRes), h = (int)(SimConst.WorldH / MassRes);
            if (_massTex == null)
            {
                _massTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                _massPix = new Color32[w * h]; _massCnt = new float[w * h];
                _massQuad = GameObject.CreatePrimitive(PrimitiveType.Quad); _massQuad.name = "Sombra de masa"; Destroy(_massQuad.GetComponent<Collider>());
                _massQuad.transform.SetParent(transform, false);
                _massQuad.transform.position = new Vector3(SimConst.WorldW * 0.5f, 0.02f, SimConst.WorldH * 0.5f);
                _massQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                _massQuad.transform.localScale = new Vector3(SimConst.WorldW, SimConst.WorldH, 1f);
                var mm = new Material(Shader.Find("Sprites/Default")); mm.mainTexture = _massTex;
                _massQuad.GetComponent<MeshRenderer>().sharedMaterial = mm;
            }
            bool inZone = _zoneNode >= 0 || _ambush || _cutting;
            _massQuad.SetActive(inZone);
            if (!inZone) return;
            System.Array.Clear(_massCnt, 0, _massCnt.Length);
            var zs = _worldA.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Zombie>()).ToComponentDataArray<Zombie>(Allocator.Temp);
            for (int i = 0; i < zs.Length; i++)
            {
                int cx = (int)(zs[i].Pos.x / MassRes), cz = (int)(zs[i].Pos.z / MassRes);
                if (cx >= 0 && cx < w && cz >= 0 && cz < h) _massCnt[cz * w + cx] += 1f;
            }
            zs.Dispose();
            // kernel gaussiano de unos 6 m (sigma 3 m = 1,5 texels, radio 4 texels), separable; densidad en zombis/m²
            const int R = 4; const float sg = 1.5f;
            var k = new float[2 * R + 1]; float ks = 0f;
            for (int i = -R; i <= R; i++) { k[i + R] = Mathf.Exp(-i * i / (2f * sg * sg)); ks += k[i + R]; }
            for (int i = 0; i < k.Length; i++) k[i] /= ks;
            if (_massTmp == null || _massTmp.Length != _massCnt.Length) _massTmp = new float[_massCnt.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = 0f;
                    for (int i = -R; i <= R; i++) { int xx = x + i; if (xx >= 0 && xx < w) a += _massCnt[y * w + xx] * k[i + R]; }
                    _massTmp[y * w + x] = a;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = 0f;
                    for (int i = -R; i <= R; i++) { int yy = y + i; if (yy >= 0 && yy < h) a += _massTmp[yy * w + x] * k[i + R]; }
                    float dens = a / (MassRes * MassRes);                           // zombis por m²
                    float al = Mathf.Clamp01((dens - 0.2f) / 0.8f) * 0.55f;         // 0 bajo 0,2/m²; 0,55 a partir de 1/m²
                    _massPix[y * w + x] = new Color32(0, 0, 0, (byte)(al * 255f));
                }
            _massTex.SetPixels32(_massPix); _massTex.Apply(false);
        }

        void Update()
        {
            if (_worldA == null) return;                 // la zona aún espera a la orden Materialize
            UpdateHordePresentation();
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
                        WriteDesyncReport(ha, hb);
                    }
                }
                else if (ha.Tick % 300 == 0)
                {
                    Debug.Log($"[TBD] tick {ha.Tick}: hash idéntico en los dos mundos ({ha.Value:X16})");
                }
            }
        }

        /// <summary>Una orden entra por la cola única con un número de tick futuro, igual en los dos mundos.</summary>
        readonly System.Collections.Generic.List<string> _cmdLog = new System.Collections.Generic.List<string>();   // últimas órdenes de la zona (para el informe de desincronización)

        void Push(CmdType type, int arg = 0, Unity.Mathematics.float2 p0 = default, Unity.Mathematics.float2 p1 = default)
        {
            _cmdLog.Add($"tick {_worldA.EntityManager.GetComponentData<SimClock>(_rootA).Tick} {type} arg={arg}");
            if (_cmdLog.Count > 40) _cmdLog.RemoveAt(0);
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
            if (kb.leftBracketKey.wasPressedThisFrame) { _autoPix = false; _factor = Mathf.Max(2, _factor - 1); RebuildRt(); }
            if (kb.rightBracketKey.wasPressedThisFrame) { _autoPix = false; _factor = Mathf.Min(6, _factor + 1); RebuildRt(); }
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

            if (kb.bKey.wasPressedThisFrame) { _buildMode = !_buildMode; _hasWallStart = false; _wallPts.Clear(); }
            if (kb.escapeKey.wasPressedThisFrame) { _buildMode = false; _hasWallStart = false; _wallPts.Clear(); }
            if (_zoneNode >= 0 && !_ambush)
            {
                if (kb.hKey.wasPressedThisFrame) { _workKind = NextKind(_workKind); _wallPts.Clear(); _hasWallStart = false; }       // sacos, HESCO, puerta, nido, foco
                if (kb.nKey.wasPressedThisFrame)                                                                                       // asignar obreros / quitarlos
                    NetSession.Push(NetCmdType.AssignWorkers, _zoneNode, 0, NetSession.A.Works.Workers[_zoneNode] > 0 ? 0 : 8);
                if (kb.enterKey.wasPressedThisFrame && _buildMode) FinishWall();
                // taller de Sagunto (H5 paso 5): 1 tren de carga, 2 de combate, 3 de obras, 4 rehabilitar el Tren 3, 5 rehabilitar el de obras, 0 cancela el primer encargo, C mueve a Chispas
                var tl = NetSession.A != null ? NetSession.A.Taller : null;
                if (tl != null && tl.NodeIndex == _zoneNode)
                {
                    if (kb.digit1Key.wasPressedThisFrame) NetSession.Push(NetCmdType.OrderTrain, (int)TallerKind.Carga, 0);
                    if (kb.digit2Key.wasPressedThisFrame) NetSession.Push(NetCmdType.OrderTrain, (int)TallerKind.Combate, 0);
                    if (kb.digit3Key.wasPressedThisFrame) NetSession.Push(NetCmdType.OrderTrain, (int)TallerKind.Obras, 0);
                    if (kb.digit4Key.wasPressedThisFrame) NetSession.Push(NetCmdType.OrderTrain, (int)TallerKind.RehabTren3, 0);
                    if (kb.digit5Key.wasPressedThisFrame) NetSession.Push(NetCmdType.OrderTrain, (int)TallerKind.RehabObras, 0);
                    if (kb.digit0Key.wasPressedThisFrame && tl.Queue.Count > 0) NetSession.Push(NetCmdType.CancelTrainOrder, 0, 0);
                    if (kb.cKey.wasPressedThisFrame && tl.ChispasAt != -2) NetSession.Push(NetCmdType.SetChispas, tl.ChispasAt == -1 ? 1 : 0, 0);   // al tren 1 o de vuelta al taller
                }
            }

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
                    if (mouse.rightButton.wasPressedThisFrame) { if (_zoneNode >= 0 && !_ambush) FinishWall(); _hasWallStart = false; }
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
            SyncWorks();
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
        Light _sun;

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
                    SetLight(new Color(1f, 0.97f, 0.92f), 1.2f, new Color(0.30f, 0.36f, 0.52f), 40f);   // sol a 40° de día (Lucía, D-N61: 35-45°)
                    SetFloodlights(0f);
                    break;
                case 1:  // atardecer naranja
                    _grade.colorFilter.Override(new Color(1f, 0.8f, 0.62f));
                    _grade.postExposure.Override(0.1f);
                    _grade.saturation.Override(10f);
                    SetLight(new Color(1f, 0.97f, 0.92f), 1.2f, new Color(0.30f, 0.36f, 0.52f), 15f);   // al atardecer 15°: sombras largas (10-20°)
                    SetFloodlights(12f);
                    break;
                default: // noche: el azul viene de la luna y del ambiente (#283440 a #404460) sobre el albedo ocre de siempre; la gradación
                         // solo baja la saturación, sin teñir, para que el ámbar del foco siga siendo ámbar (no salmón)
                    _grade.colorFilter.Override(Color.white);
                    _grade.postExposure.Override(0.9f);
                    _grade.saturation.Override(-15f);
                    // luna y ambiente poco azules y bajos, y focos más fuertes: medido a resolución completa (Shift+F9 con máscara), el 84 % de los
                    // píxeles cálidos del charco queda entre 20° y 45° (con la gradación azul y el foco de antes, el 18 %: el borde del charco salía rosa)
                    SetLight(new Color(0.26f, 0.30f, 0.45f), 0.45f, new Color(0.06f, 0.07f, 0.10f), 50f);   // luna alta; ambiente al 6-10 %
                    SetFloodlights(90f);
                    break;
            }
        }

        /// <summary>Sol (o luna) y luz ambiente de cada gradación: el tono de la hora va en la luz, no en un tinte de la imagen.</summary>
        void SetLight(Color sunColor, float sunIntensity, Color ambient, float elevation = 40f)
        {
            if (_sun != null) { _sun.color = sunColor; _sun.intensity = sunIntensity; _sun.transform.rotation = Quaternion.Euler(elevation, -35f, 0f); }
            RenderSettings.ambientLight = ambient;
        }

        // ================================================================ interfaz nítida (capa 4)
        // ---------------------------------------------------------------- construcción (Lucía, D-N61): superposición a resolución completa, tras el pixelado
        Texture2D _px; GUIStyle _lblStyle;
        static readonly Color Bone = new Color32(0xd4, 0xd0, 0xcc, 255), Amber = new Color32(0xe0, 0x8c, 0x30, 255), Tint = new Color32(0x70, 0x78, 0x4c, 255);

        void FillRect(Rect r, Color c) { if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); } var old = GUI.color; GUI.color = c.linear; GUI.DrawTexture(r, _px); GUI.color = old; }   // el proyecto va en espacio lineal: sin .linear los colores salen lavados

        void DrawLine2(Vector2 a, Vector2 b, float w, Color c)
        {
            if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            var d = b - a; float len = d.magnitude; if (len < 0.5f) return;
            var m = GUI.matrix; var old = GUI.color; GUI.color = c.linear;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - w * 0.5f, len, w), _px);
            GUI.matrix = m; GUI.color = old;
        }

        Vector2 ToGui(Vector3 world) { var v = _worldCam.WorldToViewportPoint(world); return new Vector2(v.x * Screen.width, (1f - v.y) * Screen.height); }

        void BuildOverlay()
        {
            if (!_buildMode || _zoneNode < 0 || _ambush || _worldCam == null || NetSession.A == null || Mouse.current == null) return;
            var mp = Mouse.current.position.ReadValue();
            var ray = _worldCam.ViewportPointToRay(new Vector3(mp.x / Screen.width, mp.y / Screen.height, 0f));
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float tt)) return;
            var cur = ray.GetPoint(tt); cur.x = Mathf.Clamp(cur.x, 2f, SimConst.WorldW - 2f); cur.z = Mathf.Clamp(cur.z, 2f, SimConst.WorldH - 2f); cur.y = 0.1f;
            var net = NetSession.A; bool point = IsPoint(_workKind);
            var pts = new System.Collections.Generic.List<int>();
            Vector3 last = cur;
            if (!point)
            {
                for (int i = 0; i + 1 < _wallPts.Count; i += 2) pts.Add(_wallPts[i]);   // se rehace abajo con pares x,y
                pts.Clear();
                for (int i = 0; i + 1 < _wallPts.Count; i += 2) { pts.Add(_wallPts[i]); pts.Add(_wallPts[i + 1]); }
                if (_wallPts.Count >= 2) last = new Vector3(WorkOrigin.x + _wallPts[_wallPts.Count - 2] / 1000f, 0.1f, WorkOrigin.y + _wallPts[_wallPts.Count - 1] / 1000f);
                else if (_hasWallStart) last = new Vector3(_wallStart.x, 0.1f, _wallStart.z);
                if (_wallPts.Count == 0 && _hasWallStart) { pts.Add(ToLocalMm(_wallStart.x)); pts.Add(ToLocalMm(_wallStart.z, true)); }
            }
            pts.Add(ToLocalMm(cur.x)); pts.Add(ToLocalMm(cur.z, true));
            WorkPlan plan; bool have = true;
            if (!point && pts.Count < 4) { plan = default(WorkPlan); have = false; }
            else plan = PreviewZone(net, _workKind, pts.ToArray());
            bool ok = !have || plan.Ok;
            Color line = ok ? Bone : Amber, tint = ok ? Tint : Amber;
            if (point)
            {
                var c = ToGui(cur); float h = 14f;
                FillRect(new Rect(c.x - h, c.y - h, h * 2, h * 2), new Color(tint.r, tint.g, tint.b, 1f));
                FillRect(new Rect(c.x - h - 2, c.y - h - 2, h * 2 + 4, 2), line); FillRect(new Rect(c.x - h - 2, c.y + h, h * 2 + 4, 2), line);
                FillRect(new Rect(c.x - h - 2, c.y - h, 2, h * 2), line); FillRect(new Rect(c.x + h, c.y - h, 2, h * 2), line);
            }
            else
            {
                // trazado: tramos ya pedidos con tinte plano y contorno de 2 px; línea de 1 px entre el último clic y el cursor
                Vector3 prev = default(Vector3); bool first = true;
                for (int i = 0; i + 1 < pts.Count; i += 2)
                {
                    var wpt = new Vector3(WorkOrigin.x + pts[i] / 1000f, 0.1f, WorkOrigin.y + pts[i + 1] / 1000f);
                    if (!first)
                    {
                        Vector2 a = ToGui(prev), b2 = ToGui(wpt); var n = (b2 - a).normalized; var perp = new Vector2(-n.y, n.x) * 5f;
                        DrawLine2(a, b2, 8f, tint);
                        DrawLine2(a + perp, b2 + perp, 2f, line); DrawLine2(a - perp, b2 - perp, 2f, line);
                    }
                    prev = wpt; first = false;
                }
                DrawLine2(ToGui(last), ToGui(cur), 1f, Bone);
            }
            // etiqueta junto al cursor: caja #181c30 con borde #48403c
            if (_lblStyle == null) { _lblStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false }; _lblStyle.normal.textColor = Bone; }
            _lblStyle.fontSize = Mathf.Max(12, Screen.height / 66);
            string l1 = have ? (plan.Ok ? KindName(_workKind) + " · " + (plan.AllowedMm / 1000f).ToString("0.0") + " m · " + plan.CostU + " u" : KindName(_workKind)) : KindName(_workKind);
            string l2 = null;
            if (have && !plan.Ok) l2 = plan.Why != null && plan.Why.Contains("zombis") ? "Hay zombis cerca · 30 m" : plan.Why;
            float lw = Mathf.Max(220f, _lblStyle.fontSize * 16f), lh = _lblStyle.fontSize * (l2 != null ? 3.4f : 2.0f);
            var box = new Rect(mp.x + 24f, Screen.height - mp.y - lh - 8f, lw, lh);
            FillRect(new Rect(box.x - 2, box.y - 2, box.width + 4, box.height + 4), new Color32(0x48, 0x40, 0x3c, 255));
            FillRect(box, new Color32(0x18, 0x1c, 0x30, 255));
            GUI.Label(new Rect(box.x + 6, box.y + 2, box.width - 8, _lblStyle.fontSize * 1.6f), l1, _lblStyle);
            if (l2 != null) { var st2 = new GUIStyle(_lblStyle); st2.normal.textColor = Amber; GUI.Label(new Rect(box.x + 6, box.y + 2 + _lblStyle.fontSize * 1.5f, box.width - 8, _lblStyle.fontSize * 1.6f), l2, st2); }
        }

        void OnGUI()
        {
            BuildOverlay();
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
                string wh = WorkHud();
                if (wh.Length > 0) GUI.Label(new Rect(pad * 2f, h + pad * 1.6f + _hudStyle.lineHeight * 1.7f, w, _hudStyle.lineHeight * 7f), wh, _hudStyle);
            }
            NetSession.DrawFade(Time.realtimeSinceStartup - _fadeStart);
        }

        // ================================================================ capturas y GIF (solo hitos importantes)
        // F9 = captura PNG de lo que ve el jugador (con efectos, postproceso e interfaz, a mitad de tamaño).
        // F10 = empieza/para una grabación de fotogramas; el GIF se monta con scripts/capturas/hacer_gif.py de
        // mr-mak-workspace. Todo se guarda en <proyecto>/Captures/, fuera de Git.
        /// <summary>Las capturas de Shift+F9 salen a resolución completa del juego (la normal del F9 es a mitad de tamaño). Máscara: zombis #FF00FF, tren #00FFFF.</summary>
        static int MaskSuperSize = 1;        // captura normal con máscara a la misma resolución (el supersize cambia la exposición: Lucía, D-N61)
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
            var normal = ScreenCapture.CaptureScreenshotAsTexture(MaskSuperSize);
            File.WriteAllBytes(normalPath, normal.EncodeToPNG());
            Destroy(normal);

            var em = _worldA.EntityManager;
            var q = em.CreateEntityQuery(typeof(Zombie), typeof(ZombieBaseColor));
            var ents = q.ToEntityArray(Allocator.Persistent);                 // Persistent: cruza fotogramas (Temp se libera al acabar el fotograma)
            var allMats = RenderAssets.Materials; var savedTex = new Texture[allMats.Length]; var savedShader = new Shader[allMats.Length];
            for (int mi = 0; mi < allMats.Length; mi++)                         // sin textura y sin luz: la silueta del zombi sale magenta puro
            { savedTex[mi] = allMats[mi].GetTexture("_BaseMap"); savedShader[mi] = allMats[mi].shader; allMats[mi].SetTexture("_BaseMap", null); allMats[mi].shader = Shader.Find("Universal Render Pipeline/Unlit"); }
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
            // el tren en cian puro #00FFFF (todo el tren: cuerpo, techo, contorno y franja), para medir su contraste contra el fondo (D-N13)
            var cyan = new Material(Shader.Find("Universal Render Pipeline/Unlit")); cyan.SetColor("_BaseColor", new Color(0f, 1f, 1f, 1f));
            var trainRenderers = new System.Collections.Generic.List<MeshRenderer>(); var trainMats = new System.Collections.Generic.List<Material[]>();
            if (_trainParts != null)
                foreach (var part in _trainParts)
                    if (part != null)
                        foreach (var r in part.GetComponentsInChildren<MeshRenderer>())
                        {
                            trainRenderers.Add(r); trainMats.Add(r.sharedMaterials);
                            var arr = new Material[r.sharedMaterials.Length]; for (int k = 0; k < arr.Length; k++) arr[k] = cyan;
                            r.sharedMaterials = arr;
                        }
            yield return null;                                                // un fotograma para que Entities Graphics suba los colores
            yield return new WaitForEndOfFrame();
            var mask = ScreenCapture.CaptureScreenshotAsTexture(MaskSuperSize);
            File.WriteAllBytes(maskPath, mask.EncodeToPNG());
            Destroy(mask);

            for (int i = 0; i < ents.Length; i++)
                em.SetComponentData(ents[i], new ZombieBaseColor { Value = RenderAttachSystem.ColorFor(em.GetComponentData<Zombie>(ents[i]).SimId) });
            for (int i = 0; i < trainRenderers.Count; i++) trainRenderers[i].sharedMaterials = trainMats[i];
            Destroy(cyan);
            ents.Dispose();
            for (int mi = 0; mi < allMats.Length; mi++) { allMats[mi].shader = savedShader[mi]; allMats[mi].SetTexture("_BaseMap", savedTex[mi]); }
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

        /// <summary>Figura de zombi de pie, una sola malla (torso, cabeza, brazos adelantados y piernas): provisional, para que la horda se lea como figuras y proyecte sombra. Mira a +Z; el origen está en los pies.</summary>
        static Mesh BuildZombieFigure()
        {
            var parts = new System.Collections.Generic.List<CombineInstance>();
            void Add(PrimitiveType t, Vector3 pos, Vector3 scale, Quaternion? rot = null)
            {
                var g = GameObject.CreatePrimitive(t); var mf = g.GetComponent<MeshFilter>();
                var ci = new CombineInstance { mesh = mf.sharedMesh, transform = Matrix4x4.TRS(pos, rot ?? Quaternion.identity, scale) };
                parts.Add(ci); Destroy(g);
            }
            Add(PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.5f, 0.7f, 0.3f));                       // torso
            Add(PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0.02f), new Vector3(0.3f, 0.32f, 0.3f));                   // cabeza
            Add(PrimitiveType.Cube, new Vector3(-0.13f, 0.4f, 0f), new Vector3(0.18f, 0.8f, 0.2f));                     // piernas
            Add(PrimitiveType.Cube, new Vector3(0.13f, 0.4f, 0f), new Vector3(0.18f, 0.8f, 0.2f));
            Add(PrimitiveType.Cube, new Vector3(-0.32f, 1.3f, 0.32f), new Vector3(0.12f, 0.12f, 0.7f));                 // brazos adelantados
            Add(PrimitiveType.Cube, new Vector3(0.32f, 1.3f, 0.32f), new Vector3(0.12f, 0.12f, 0.7f));
            var m = new Mesh { name = "Zombi (figura provisional)" };
            m.CombineMeshes(parts.ToArray(), true, true);
            return m;
        }

        void SetupVisualAssets()
        {
            // Revisión de arte de Lucía (5-nov): suelo claro y cálido, tejado de terracota (antes era del color del suelo)
            _mGround = Mat("b0a060"); _mWall = Mat("88885c"); _mRoof = Mat("a86c50"); _mRail = Mat("94948c");
            _mSleeper = Mat("442c0c"); _mTrain = Mat("303424"); _mWagon = Mat("605c48"); _mGate = Mat("7c3c18");

            // Zombi: cápsula de Unity (escalada a 0,5 x 1,8 m en RenderAttachSystem)
            // Zombi: cuadrado de 1 m (primitiva Quad) con el atlas de andar de Bruno (6 variantes de ropa x 4 fotogramas); el recorte va por instancia
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Mesh capsule = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            var zm = Mat("ffffff");
            var zt = Resources.Load<Texture2D>("Icons/unidades/atlas_zombi_andar");
            if (zt != null) zm.SetTexture("_BaseMap", zt);
            zm.SetFloat("_Cull", 0f); zm.SetFloat("_AlphaClip", 1f); zm.SetFloat("_Cutoff", 0.5f); zm.EnableKeyword("_ALPHATEST_ON");
            var zl = new Material(zm) { shader = Shader.Find("Universal Render Pipeline/Unlit") };   // LOD de masa: atlas de 8 px (D-N61), unlit: los colores exactos de la paleta
            zl.SetFloat("_Cull", 0f); zl.SetFloat("_AlphaClip", 1f); zl.SetFloat("_Cutoff", 0.5f); zl.EnableKeyword("_ALPHATEST_ON"); zl.SetColor("_BaseColor", Color.white);
            var ztl = Resources.Load<Texture2D>("Icons/unidades/atlas_zombi_lod");
            if (ztl != null) zl.SetTexture("_BaseMap", ztl);
            if (RenderAssets.FigureMode)
            {
                // zombi de cerca de Teo (D-N61): tres camisas, LOD1 de 168-192 triángulos, paleta 32x16 con filtro de punto; si faltan, la figura procedural
                var teoMeshes = new System.Collections.Generic.List<Mesh>(); var teoMats = new System.Collections.Generic.List<Material>();
                foreach (var camisa in new[] { "camisa_303424", "camisa_283440", "camisa_442c0c" })
                {
                    var go = Resources.Load<GameObject>("Models/Zombi/" + camisa + "/SM_zombi_fino_LOD1");
                    var tex = Resources.Load<Texture2D>("Models/Zombi/" + camisa + "/T_Paleta32");
                    var mf = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
                    if (mf == null || tex == null) continue;
                    var mat = Mat("ffffff"); mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white);
                    teoMeshes.Add(mf.sharedMesh); teoMats.Add(mat);
                }
                if (teoMeshes.Count == 3) RenderAssets.SetVariants(teoMeshes.ToArray(), teoMats.ToArray());
                else { capsule = BuildZombieFigure(); zm = Mat("ffffff"); zl = zm; RenderAssets.Set(capsule, zm, zl); }
            }
            else RenderAssets.Set(capsule, zm, zl);              // el color por instancia es blanco (ZombieBaseColor); la máscara lo pone magenta

            // Luz direccional
            // Sol bajo (unos 32°), cálido, con sombras proyectadas; la luz ambiente fría tiñe las sombras de azul (revisión de Lucía)
            var sun = FindFirstObjectByType<Light>();
            if (sun == null) sun = new GameObject("Sol").AddComponent<Light>();
            _sun = sun;
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
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
                l.color = HexColor("ffc850");            // ámbar más claro: el rosa del borde del charco venía del azul de la luna y el ambiente
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
            foreach (var l in _cohesionLights) l.intensity = intensity > 0f ? 90f : 0f;
            foreach (var rm in _rimMats) { rm.EnableKeyword("_EMISSION"); rm.SetColor("_EmissionColor", intensity > 40f ? HexColor("404460") * 0.6f : Color.black); }
            foreach (var h in _halos) h.gameObject.SetActive(intensity > 0f);
        }

        void LateUpdate()
        {
            if (_fxCam == null) return;
            foreach (var h in _halos) if (h.gameObject.activeSelf) h.rotation = _fxCam.transform.rotation;   // el halo mira a la cámara
        }

        /// <summary>
        /// Q14 (Sara): cuando la zona se desincroniza, deja un informe en Captures/desync_zona_*.txt con la escena, el tipo de zona, Burst,
        /// el hash por sistema de los dos mundos y las últimas órdenes, para no tener que preguntar qué se hizo.
        /// </summary>
        void WriteDesyncReport(SimHash ha, SimHash hb)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("Desincronización de la zona · ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
                sb.Append("Unity ").Append(Application.unityVersion).Append(" · ").Append(Application.isEditor ? "Editor" : "compilación").Append(" · Burst ")
                  .Append(Unity.Burst.BurstCompiler.Options.EnableBurstCompilation ? "activado" : "DESACTIVADO").Append('\n');
                sb.Append("escena ").Append(SceneManager.GetActiveScene().name).Append(" · zona ").Append(_cutting ? "emboscada (desmonte)" : "estación").Append(" · nodo ")
                  .Append(_zoneNode).Append(" · zombis ").Append(_total).Append('\n');
                sb.Append("tick A ").Append(ha.Tick).Append(" B ").Append(hb.Tick).Append(" · hash A ").Append(ha.Value.ToString("X16")).Append(" B ").Append(hb.Value.ToString("X16")).Append('\n');
                var sa = ZoneGolden.Systems(_worldA.EntityManager, _rootA);
                var sbb = ZoneGolden.Systems(_worldB.EntityManager, _rootB);
                for (int i = 0; i < sa.Length && i < ZoneGolden.SystemNames.Length; i++)
                    sb.Append("  ").Append(ZoneGolden.SystemNames[i]).Append(": A ").Append(sa[i].ToString("X16")).Append(" B ").Append(sbb[i].ToString("X16")).Append(sa[i] == sbb[i] ? "" : "   <-- DIFIERE").Append('\n');
                sb.Append("últimas órdenes:\n");
                foreach (var c in _cmdLog) sb.Append("  ").Append(c).Append('\n');
                Directory.CreateDirectory(CaptureRoot);
                string path = Path.Combine(CaptureRoot, $"desync_zona_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(path, sb.ToString());
                Debug.Log("[TBD] Informe de desincronización: " + path);
            }
            catch (System.Exception e) { Debug.LogWarning("[TBD] No se pudo escribir el informe de desincronización: " + e.Message); }
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

        /// <summary>
        /// Terreno del desmonte para la vista: talud continuo (rampa que pasa por la mitad de los escalones de la simulación), un solo
        /// albedo oscurecido por la pendiente (lo hace la propia luz según la normal) y el fondo con el valor más oscuro de la escena.
        /// La simulación sigue usando los cinco escalones; esto es solo presentación.
        /// </summary>
        void BuildCuttingTerrain()
        {
            var bank = Mat("8a7650");                                // llano de arriba (L* 64-68 en captura normal)
            var slope = Mat("5a4c34");                               // taludes: L* 52-56 (medido: con el albedo del llano salían a 66)
            float hb = 0.7f, hp = CuttingLevel.Depth;                // altura de la base de la rampa y del llano de arriba
            float yb = Level.TrackY + CuttingLevel.HalfBottom;       // 72,5: pie del talud norte
            float yc = yb + (hp - hb) / 0.84f;                       // pendiente 0,84 m por metro: pasa por la mitad de los escalones
            float x0 = CuttingLevel.X0, x1 = CuttingLevel.X1;
            // norte
            AddQuad(slope, new Vector3(x0, hb, yb), new Vector3(x1, hb, yb), new Vector3(x0, hp, yc), new Vector3(x1, hp, yc));              // talud
            AddQuad(bank, new Vector3(x0, hp, yc), new Vector3(x1, hp, yc), new Vector3(x0, hp, SimConst.WorldH), new Vector3(x1, hp, SimConst.WorldH));  // llano de arriba
            // sur (simétrico respecto de la vía)
            float ys = Level.TrackY - CuttingLevel.HalfBottom, ysc = ys - (hp - hb) / 0.84f;
            AddQuad(slope, new Vector3(x1, hb, ys), new Vector3(x0, hb, ys), new Vector3(x1, hp, ysc), new Vector3(x0, hp, ysc));
            AddQuad(bank, new Vector3(x1, hp, ysc), new Vector3(x0, hp, ysc), new Vector3(x1, hp, 0f), new Vector3(x0, hp, 0f));
            // las cabeceras de la trinchera (paredes verticales en X0 y X1)
            AddQuad(bank, new Vector3(x0, 0f, yb), new Vector3(x0, 0f, SimConst.WorldH), new Vector3(x0, hp, yb), new Vector3(x0, hp, SimConst.WorldH));
            AddQuad(bank, new Vector3(x1, 0f, SimConst.WorldH), new Vector3(x1, 0f, yb), new Vector3(x1, hp, SimConst.WorldH), new Vector3(x1, hp, yb));
            AddQuad(bank, new Vector3(x0, 0f, 0f), new Vector3(x0, 0f, ys), new Vector3(x0, hp, 0f), new Vector3(x0, hp, ys));
            AddQuad(bank, new Vector3(x1, 0f, ys), new Vector3(x1, 0f, 0f), new Vector3(x1, hp, ys), new Vector3(x1, hp, 0f));
            // fondo: el valor más oscuro de la escena (balasto #48403c); el tren se lee contra él
            // fondo de la trinchera: L* 38-42 (#605c48, v2 del desmonte aprobada por Lucía); el balasto #48403c (L* 26-30) solo en la franja de ~4 m bajo la vía
            Box("Fondo de la trinchera", new Vector3((x0 + x1) * 0.5f, 0.02f, Level.TrackY), new Vector3(x1 - x0, 0.04f, CuttingLevel.HalfBottom * 2f), Mat("282419"));
            var floor = Box("Balasto", new Vector3((x0 + x1) * 0.5f, 0.03f, Level.TrackY), new Vector3(x1 - x0, 0.04f, 4f), Mat("48403c"));
            var tb = Resources.Load<Texture2D>("Icons/texturas/tex_balasto_64");        // balasto de Bruno, tileable (regla 13): una repetición cada 4 m
            if (tb != null)
            {
                var fm = floor.GetComponent<MeshRenderer>().material;
                fm.SetTexture("_BaseMap", tb); fm.SetColor("_BaseColor", new Color(0.9f, 0.9f, 0.9f));   // L* 28-32 en captura normal
                fm.SetTextureScale("_BaseMap", new Vector2((x1 - x0) / 4f, 1f));
            }
            // oclusión ambiental falsa: una banda más oscura al pie de cada talud (20-30 % según la profundidad)
            Box("Sombra del pie norte", new Vector3((x0 + x1) * 0.5f, 0.03f, yb + 1f), new Vector3(x1 - x0, 0.03f, 2f), Mat("342e2a"));
            Box("Sombra del pie sur", new Vector3((x0 + x1) * 0.5f, 0.03f, ys - 1f), new Vector3(x1 - x0, 0.03f, 2f), Mat("342e2a"));
        }

        /// <summary>Un cuadrilátero con normal hacia arriba o hacia fuera (a, b abajo; c, d arriba), sin colisión.</summary>
        void AddQuad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var go = new GameObject("Terreno");
            go.transform.SetParent(transform, false);
            var mesh = new Mesh { vertices = new[] { a, b, c, d }, triangles = new[] { 0, 2, 1, 1, 2, 3 } };
            mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            var dup = new GameObject("Terreno (cara de atrás)");                 // doble cara: no depende del sentido de los triángulos
            dup.transform.SetParent(transform, false);
            var back = new Mesh { vertices = mesh.vertices, triangles = new[] { 0, 1, 2, 1, 3, 2 } };
            back.RecalculateNormals();
            dup.AddComponent<MeshFilter>().sharedMesh = back;
            dup.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        // ---------------------------------------------------------------- captura de cohesión 3D (Iris y Lucía, 6-nov)
        /// <summary>
        /// Modelos PROVISIONALES con formas simples (albedo plano con colores de la paleta, sin texturas) para comprobar si el 3D bajo el
        /// pixelado casa con los iconos de Bruno: soldado, civil, zombi, blindado 8×8, locomotora, HESCO y un foco. Se llama desde el
        /// Editor (reflexión); escribe Captures/cohesion_pos.json con la posición en pantalla de cada modelo para medir L* contra el suelo.
        /// </summary>
        public void CohesionModels()
        {
            // sin la horda: la escena de cohesión muestra un solo zombi de referencia, y así se mide cada modelo contra el suelo
            foreach (var w2 in new[] { _worldA, _worldB })
                if (w2 != null && w2.IsCreated) w2.EntityManager.DestroyEntity(w2.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Zombie>()));
            var root = new GameObject("Cohesion_modelos").transform;
            root.SetParent(transform, false);
            float zc = Level.TrackY - 3f;                        // en el fondo de la trinchera, a la vista de la cámara por defecto
            float x0 = CuttingLevel.LocoX - 38f;
            Material M(string hex) { return Mat(hex); }
            GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
            {
                var g = GameObject.CreatePrimitive(t); g.name = name; Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root, false); g.transform.position = pos; g.transform.localScale = scale;
                if (rot.HasValue) g.transform.rotation = rot.Value;
                g.GetComponent<MeshRenderer>().sharedMaterial = m; return g;
            }
            var olive = M("50541c"); var bone = M("d4d0cc"); var ochre = M("b4a080"); var beige = M("d8c8a8"); var steel = M("6c7874"); var dark = M("2c2820");
            var oliveLight = M("88885c"); var beigeTop = M("b4a080"); var black = M("000000");
            // D-N57 (Lucía): valor partido. Costados en olive_dark, caras de arriba claras (beige en techos, olive_light en cascos y hombros) y
            // contorno negro de 1 px; solo las unidades propias llevan un rim frío (#404460) de noche. Los zombis, no.
            void Outline(GameObject g) { var o = Prim(PrimitiveType.Cube, g.name + " contorno", g.transform.position, g.transform.localScale + new Vector3(0.18f, -0.05f, 0.18f), black); o.GetComponent<MeshFilter>().sharedMesh = g.GetComponent<MeshFilter>().sharedMesh; o.transform.rotation = g.transform.rotation; }
            void Rim(GameObject g) { _rimMats.Add(g.GetComponent<MeshRenderer>().material); }
            var amber = M("e08c30"); var zomb = M("78805c");
            var pts = new System.Collections.Generic.List<(string name, Vector3 pos)>();
            // soldado: cuerpo oliva, casco y detalle de hueso
            Vector3 ps = new Vector3(x0, 0f, zc + 1f);
            var sold = Prim(PrimitiveType.Capsule, "Soldado", ps + Vector3.up * 0.9f, new Vector3(0.5f, 0.9f, 0.5f), olive); Outline(sold); Rim(sold);
            Prim(PrimitiveType.Cube, "Soldado hombros", ps + Vector3.up * 1.6f, new Vector3(0.9f, 0.16f, 0.7f), oliveLight);
            Prim(PrimitiveType.Cube, "Soldado casco", ps + Vector3.up * 1.9f, new Vector3(0.6f, 0.3f, 0.6f), oliveLight);
            Prim(PrimitiveType.Cube, "Soldado correaje", ps + Vector3.up * 1.0f + Vector3.forward * 0.26f, new Vector3(0.3f, 0.5f, 0.05f), bone);
            pts.Add(("soldado", ps + Vector3.up * 0.9f));
            // civil: ocre con detalle beige
            Vector3 pc = new Vector3(x0 + 3f, 0f, zc + 1f);
            var civ = Prim(PrimitiveType.Capsule, "Civil", pc + Vector3.up * 0.85f, new Vector3(0.5f, 0.85f, 0.5f), ochre); Outline(civ);
            Prim(PrimitiveType.Cube, "Civil hombros", pc + Vector3.up * 1.62f, new Vector3(0.9f, 0.16f, 0.7f), beigeTop); Prim(PrimitiveType.Sphere, "Civil cabeza", pc + Vector3.up * 1.85f, new Vector3(0.5f, 0.5f, 0.5f), bone);
            pts.Add(("civil", pc + Vector3.up * 0.85f));
            // zombi: el verde grisáceo de la horda
            Vector3 pz = new Vector3(x0 + 6f, 0f, zc + 1f);
            var zom = Prim(PrimitiveType.Capsule, "Zombi", pz + Vector3.up * 0.9f, new Vector3(0.5f, 0.9f, 0.5f), zomb); Outline(zom); Prim(PrimitiveType.Cube, "Zombi hombros", pz + Vector3.up * 1.65f, new Vector3(0.8f, 0.14f, 0.6f), M("98a078"));
            pts.Add(("zombi", pz + Vector3.up * 0.9f));
            // blindado 8×8: casco oliva, torreta y cuatro ruedas
            Vector3 pb = new Vector3(x0 + 11f, 0f, zc + 1f);
            var bl = Prim(PrimitiveType.Cube, "Blindado casco", pb + Vector3.up * 1.2f, new Vector3(6.2f, 1.6f, 2.6f), olive); Outline(bl); Rim(bl);
            Prim(PrimitiveType.Cube, "Blindado techo", pb + Vector3.up * 2.04f, new Vector3(6.0f, 0.08f, 2.4f), beigeTop);
            var tor = Prim(PrimitiveType.Cube, "Blindado torreta", pb + Vector3.up * 2.4f + Vector3.left * 0.4f, new Vector3(2.4f, 0.9f, 1.8f), olive); Outline(tor);
            Prim(PrimitiveType.Cube, "Blindado techo torreta", pb + Vector3.up * 2.88f + Vector3.left * 0.4f, new Vector3(2.2f, 0.07f, 1.6f), beigeTop);
            Prim(PrimitiveType.Cylinder, "Blindado cañón", pb + Vector3.up * 2.4f + Vector3.right * 1.9f, new Vector3(0.25f, 1.1f, 0.25f), dark, Quaternion.Euler(0f, 0f, 90f));
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, "Rueda", pb + new Vector3(-2.2f + i * 1.45f, 0.55f, i < 2 || true ? 1.4f : -1.4f), new Vector3(1.1f, 0.15f, 1.1f), dark, Quaternion.Euler(90f, 0f, 0f));
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, "Rueda", pb + new Vector3(-2.2f + i * 1.45f, 0.55f, -1.4f), new Vector3(1.1f, 0.15f, 1.1f), dark, Quaternion.Euler(90f, 0f, 0f));
            pts.Add(("blindado", pb + Vector3.up * 1.6f));
            // HESCO: tres módulos de gavión de 2,1 m
            Vector3 ph = new Vector3(x0 + 20f, 0f, zc + 1f);
            for (int i = 0; i < 3; i++) { var hs = Prim(PrimitiveType.Cube, "HESCO", ph + new Vector3(i * 1.25f, 1.05f, 0f), new Vector3(1.2f, 2.1f, 1.2f), olive); Outline(hs); Prim(PrimitiveType.Cube, "HESCO relleno", ph + new Vector3(i * 1.25f, 2.12f, 0f), new Vector3(1.1f, 0.06f, 1.1f), beigeTop); }
            pts.Add(("hesco", ph + Vector3.up * 1.05f));
            // foco: poste y charco de luz
            Vector3 pf = new Vector3(x0 + 28f, 0f, zc + 1f);
            Prim(PrimitiveType.Cube, "Foco poste", pf + Vector3.up * 2.4f, new Vector3(0.25f, 4.8f, 0.25f), steel);
            var lgo = new GameObject("Foco luz"); lgo.transform.SetParent(root, false); lgo.transform.position = pf + Vector3.up * 4.8f;
            lgo.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
            var l = lgo.AddComponent<Light>(); l.type = LightType.Spot; l.color = HexColor("ffc850"); l.range = 22f; l.spotAngle = 80f; l.innerSpotAngle = 40f;
            l.intensity = _gradeIndex == 2 ? 90f : 0f; l.shadows = LightShadows.None;
            _cohesionLights.Add(l);
            pts.Add(("charco", pf + Vector3.forward * 1.5f));
            _cohesionPts = pts;
            _focus = new Vector3(x0 + 14f, 0f, zc);
            _targetOrtho = 10f;
        }

        System.Collections.Generic.List<(string name, Vector3 pos)> _cohesionPts;

        /// <summary>Escribe Captures/cohesion_pos.json con la posición en pantalla (viewport 0-1) de cada modelo y de su suelo, para medir L*. Llamar con la cámara asentada.</summary>
        public void CohesionPositions()
        {
            if (_cohesionPts == null) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder("{");
            for (int i = 0; i < _cohesionPts.Count; i++)
            {
                var pos = _cohesionPts[i].pos;
                var vp = _worldCam.WorldToViewportPoint(pos);
                var vg = _worldCam.WorldToViewportPoint(new Vector3(pos.x + 1.4f, 0f, pos.z));
                sb.Append(i > 0 ? "," : "").Append("\"" + _cohesionPts[i].name + "\":{\"x\":" + vp.x.ToString(inv) + ",\"y\":" + vp.y.ToString(inv) + ",\"gx\":" + vg.x.ToString(inv) + ",\"gy\":" + vg.y.ToString(inv) + "}");
            }
            sb.Append('}');
            Directory.CreateDirectory(CaptureRoot);
            File.WriteAllText(Path.Combine(CaptureRoot, "cohesion_pos.json"), sb.ToString());
        }

        readonly System.Collections.Generic.List<Material> _rimMats = new System.Collections.Generic.List<Material>();

        readonly System.Collections.Generic.List<Light> _cohesionLights = new System.Collections.Generic.List<Light>();

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

            if (_cutting) BuildCuttingTerrain();
            foreach (var s in _cutting ? new System.Collections.Generic.List<Solid>() : Level.CreateSolids())
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

            // Tren: locomotora y tres vagones. Deuda de arte (Lucía): techos claros (#b8b8b8) sobre laterales verde oliva (#50541c) y contorno
            // oscuro de 1 px; la locomotora lleva una franja ámbar (#e08c30) en el frontal. El techo claro se lee sobre el fondo oscuro.
            var olive = Mat("50541c"); var roofLight = Mat("b8b8b8"); var amberMat = Mat("e08c30"); var outline = Mat("1c1812");
            _trainParts = new Transform[4];
            _trainParts[0] = Box("Locomotora", Vector3.zero, new Vector3(4f, 2.2f, 1.8f), olive).transform;
            for (int i = 1; i < 4; i++)
                _trainParts[i] = Box("Vagón " + i, Vector3.zero, new Vector3(4.6f, 2f, 1.8f), olive).transform;
            for (int i = 0; i < 4; i++)
            {
                var t = _trainParts[i];
                Vector3 sz = t.localScale;
                // techo claro: una losa fina sobre el cuerpo (en espacio local del cuerpo, que está escalado)
                var roof = Box("Techo", Vector3.zero, Vector3.one, roofLight, t); roof.transform.localPosition = new Vector3(0f, 0.5f, 0f); roof.transform.localScale = new Vector3(0.92f, 0.06f, 0.82f);
                // contorno: un cuerpo algo mayor y más bajo, oscuro, que asoma 1 px por los lados
                var line = Box("Contorno", Vector3.zero, Vector3.one, outline, t); line.transform.localPosition = new Vector3(0f, -0.02f, 0f); line.transform.localScale = new Vector3(1.06f, 0.96f, 1.12f);
                if (i == 0)
                {
                    var stripe = Box("Franja ámbar", Vector3.zero, Vector3.one, amberMat, t); stripe.transform.localPosition = new Vector3(0.5f, 0.05f, 0f); stripe.transform.localScale = new Vector3(0.04f, 0.5f, 1.02f);
                }
            }
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

        // ---------------------------------------------------------------- obras de la red en la zona (hito 5)
        static readonly Vector2 WorkOrigin = new Vector2(85f, Level.TrackY);        // el andén (0,0) de las obras del nodo, en metros de la zona
        readonly System.Collections.Generic.List<int> _wallPts = new System.Collections.Generic.List<int>();   // polilínea en curso, mm locales al nodo
        PieceKind _workKind = PieceKind.Sacos;
        readonly System.Collections.Generic.List<GameObject> _workVisuals = new System.Collections.Generic.List<GameObject>();   // una por pieza de la red
        readonly System.Collections.Generic.List<bool> _workBuilt = new System.Collections.Generic.List<bool>();               // la pieza ya es muro en la simulación
        float _workTimer;
        string _workMsg = "";

        static bool IsPoint(PieceKind k) { return k == PieceKind.Puerta || k == PieceKind.Nido || k == PieceKind.Foco; }

        static PieceKind NextKind(PieceKind k)
        {
            return k == PieceKind.Sacos ? PieceKind.Hesco : k == PieceKind.Hesco ? PieceKind.Puerta : k == PieceKind.Puerta ? PieceKind.Nido : k == PieceKind.Nido ? PieceKind.Foco : PieceKind.Sacos;
        }

        static string KindName(PieceKind k)
        {
            return k == PieceKind.Sacos ? "muro de sacos" : k == PieceKind.Hesco ? "muro HESCO" : k == PieceKind.Puerta ? "puerta" : k == PieceKind.Nido ? "nido de ametralladora" : k == PieceKind.Foco ? "foco" : k.ToString();
        }

        /// <summary>
        /// DensityView para el terreno asegurado (Nacho, PR #16): rejilla de celdas de 0,5 m con los zombis en pie, en mm locales al nodo
        /// (origen = esquina de la zona menos WorkOrigin; la vía es y = 0). Solo se enchufa mientras se previsualiza: las órdenes de la red
        /// no la usan, así que los dos mundos aceptan lo mismo y el hash no cambia.
        /// </summary>
        DensityView _densCache; float _densTime = -9f;

        DensityView BuildZoneDensity()
        {
            if (_densCache != null && Time.unscaledTime - _densTime < 0.3f) return _densCache;     // el fantasma lo pide en cada fotograma
            const int CellMm = 500;
            int w = (int)(SimConst.WorldW / 0.5f), h = (int)(SimConst.WorldH / 0.5f);
            var d = new int[w * h];
            var zs = _worldA.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Zombie>()).ToComponentDataArray<Zombie>(Allocator.Temp);
            for (int i = 0; i < zs.Length; i++)
            {
                if (zs[i].State == ZState.Fall || zs[i].State == ZState.Dead) continue;           // solo los que están en pie
                int cx = (int)(zs[i].Pos.x / 0.5f), cz = (int)(zs[i].Pos.z / 0.5f);
                if (cx >= 0 && cx < w && cz >= 0 && cz < h) d[cz * w + cx]++;
            }
            zs.Dispose();
            _densTime = Time.unscaledTime;
            return _densCache = new DensityView { D = d, W = w, H = h, CellMm = CellMm, OriginXMm = (int)Mathf.Round((0f - WorkOrigin.x) * 1000f), OriginYMm = (int)Mathf.Round((0f - WorkOrigin.y) * 1000f) };
        }

        WorkPlan PreviewZone(NetWorld net, PieceKind kind, int[] pts)
        {
            net.ZoneDensity = BuildZoneDensity;
            try { return net.PreviewWork(_zoneNode, kind, pts); }
            finally { net.ZoneDensity = null; }
        }

        static int ToLocalMm(float v, bool z = false) { return (int)Mathf.Round((v - (z ? WorkOrigin.y : WorkOrigin.x)) * 1000f); }

        void FinishWall()
        {
            if (_wallPts.Count < 4) { _wallPts.Clear(); return; }
            // la red (PR #12 de Nacho) conoce la vía: envía la polilínea entera y añade las puertas de vía donde el muro cruza
            var plan = PreviewZone(NetSession.A, _workKind, _wallPts.ToArray());
            if (!plan.Ok) { _workMsg = "No se puede construir: " + plan.Why; _wallPts.Clear(); return; }
            NetSession.PushPayload(NetCmdType.PlaceWork, _zoneNode | ((int)_workKind << 16), _wallPts.ToArray());
            _workMsg = $"Obra pedida: {plan.AllowedMm / 1000f:0.0} m de {(_workKind == PieceKind.Sacos ? "sacos" : "HESCO")}, {plan.CostU} u" + (plan.Cut ? " (se corta por falta de materiales)" : "");
            _wallPts.Clear();
        }

        /// <summary>Sincroniza lo construido en la red (Works) con la zona: las piezas terminadas pasan a ser muros de la simulación; las que están en obra, un fantasma.</summary>
        void SyncWorks()
        {
            if (_zoneNode < 0 || _ambush || NetSession.A == null) return;
            var pieces = NetSession.A.Works.At(_zoneNode);
            while (_workVisuals.Count < pieces.Count) { _workVisuals.Add(null); _workBuilt.Add(false); }
            for (int i = 0; i < pieces.Count; i++)
            {
                var w = pieces[i];
                if (!w.IsWall)
                {
                    if (!IsPoint(w.Kind) && !w.IsGate) continue;                      // depósito y andén: sin dibujo aún
                    if (w.Built && !_workBuilt[i])
                    {
                        if (_workVisuals[i] != null) Destroy(_workVisuals[i]);
                        var c = new Vector3(WorkOrigin.x + (w.IsGate ? (w.X0 + w.X1) * 0.5f : w.X0) / 1000f, 0f, WorkOrigin.y + (w.IsGate ? (w.Y0 + w.Y1) * 0.5f : w.Y0) / 1000f);
                        _workVisuals[i] = PieceSprite(w.Kind, c, 1f);          // la puerta de vía va abierta al tren: sin sólido que bloquee
                        if (w.Kind == PieceKind.Puerta || w.Kind == PieceKind.Nido)   // bloquean a la horda: un tramo corto de muro
                        {
                            float half = w.Kind == PieceKind.Puerta ? 1.2f : 0.8f;
                            Push(CmdType.BuildWall, Obras.HeightMm(w.Kind), new Unity.Mathematics.float2(c.x - half, c.z), new Unity.Mathematics.float2(c.x + half, c.z));
                        }
                        else if (w.Kind == PieceKind.Foco)                            // el foco ilumina de noche
                        {
                            var lg = new GameObject("Foco de obra"); lg.transform.SetParent(_workVisuals[i].transform, false); lg.transform.localPosition = new Vector3(0f, 3f, 0f);
                            lg.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                            var l = lg.AddComponent<Light>(); l.type = LightType.Spot; l.color = HexColor("ffc850"); l.range = 25f; l.spotAngle = 90f; l.innerSpotAngle = 50f;
                            l.intensity = _gradeIndex == 2 ? 90f : 0f; l.shadows = LightShadows.None; _cohesionLights.Add(l);
                        }
                        _workBuilt[i] = true;
                    }
                    else if (!w.Built && _workVisuals[i] == null)
                        _workVisuals[i] = PieceSprite(w.Kind, new Vector3(WorkOrigin.x + (w.IsGate ? (w.X0 + w.X1) * 0.5f : w.X0) / 1000f, 0f, WorkOrigin.y + (w.IsGate ? (w.Y0 + w.Y1) * 0.5f : w.Y0) / 1000f), 0.5f);   // fantasma: la mitad de tamaño
                    continue;
                }
                if (w.Built && !_workBuilt[i])
                {
                    if (_workVisuals[i] != null) Destroy(_workVisuals[i]);
                    var a = new Vector3(WorkOrigin.x + w.X0 / 1000f, 0f, WorkOrigin.y + w.Y0 / 1000f);
                    var b = new Vector3(WorkOrigin.x + w.X1 / 1000f, 0f, WorkOrigin.y + w.Y1 / 1000f);
                    Push(CmdType.BuildWall, Obras.HeightMm(w.Kind), new Unity.Mathematics.float2(a.x, a.z), new Unity.Mathematics.float2(b.x, b.z));
                    _workVisuals[i] = WallVisual(a, b, Level.WallHalf * 0.5f, Obras.HeightMm(w.Kind) / 1000f, w.Kind == PieceKind.Sacos ? _mWall : _mRoof);
                    _workBuilt[i] = true;
                }
                else if (!w.Built && _workVisuals[i] == null)
                {
                    var a = new Vector3(WorkOrigin.x + w.X0 / 1000f, 0.15f, WorkOrigin.y + w.Y0 / 1000f);
                    var b = new Vector3(WorkOrigin.x + w.X1 / 1000f, 0.15f, WorkOrigin.y + w.Y1 / 1000f);
                    var g = WallVisual(a, b, Level.WallHalf * 0.5f, 0.3f, _mSleeper);   // fantasma: obra en marcha
                    _workVisuals[i] = g;
                }
            }
        }

        /// <summary>Sprite de Bruno (puerta, nido, foco; 32 px, vista cenital, miran a +Y) tumbado sobre el suelo, con filtro de punto.</summary>
        GameObject PieceSprite(PieceKind k, Vector3 pos, float scale)
        {
            string n = k == PieceKind.Puerta || k == PieceKind.PuertaVia ? "puerta" : k == PieceKind.Nido ? "nido" : "foco";
            var tex = Resources.Load<Texture2D>("Icons/prototipo/vm_mundo_" + n + "_32");
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = "Pieza " + n; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.position = pos + Vector3.up * 0.06f;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);                // el arriba del sprite (+Y) apunta a +Z del mundo
            go.transform.localScale = new Vector3(3f * scale, 3f * scale, 1f);
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Surface", 0f); m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>Texto de la obra para la pantalla: coste en directo del muro que se traza, obreros y avance.</summary>
        string WorkHud()
        {
            if (_zoneNode < 0 || _ambush || NetSession.A == null) return "";
            var net = NetSession.A;
            var sb = new System.Text.StringBuilder();
            int stock = net.Stock[_zoneNode, (int)Res.Materiales];
            var tl2 = net.Taller;
            if (tl2 != null && tl2.NodeIndex == _zoneNode)
            {
                var p = tl2.P;
                string chispasTxt = tl2.ChispasAt == -2 ? "no está" : tl2.ChispasAt == -1 ? "en el taller" : "en el tren " + (tl2.ChispasAt + 1);
                sb.Append($"TALLER · 1 carga ({p.TrainU} u, {p.TrainFuel} comb.) · 2 combate · 3 obras · 4 rehab. Tren 3 ({p.Tren3U} u) · 5 rehab. obras ({p.ObrasU} u) · 0 cancela el primero · C mueve a Chispas ({chispasTxt})\n");
                if (tl2.Queue.Count == 0) sb.Append("   cola vacía\n");
                for (int qi = 0; qi < tl2.Queue.Count; qi++)
                {
                    var o = tl2.Queue[qi];
                    int pct = o.NeedTicks > 0 ? (int)System.Math.Min(100, o.DoneMilli / 10 / System.Math.Max(1, o.NeedTicks)) : 0;
                    string est = o.Waiting ? "esperando dotación" : pct + " %";
                    sb.Append("   " + (qi + 1) + ". " + o.Kind + " · " + est + "\n");
                }
            }
            sb.Append($"OBRAS · materiales {stock} u · obreros {net.Works.Workers[_zoneNode]} (N asigna/quita) · nivel {net.Works.LevelMil(_zoneNode) / 1000f:0.0} · H cambia de pieza (ahora: {KindName(_workKind)})");
            if (_buildMode)
            {
                sb.Append(IsPoint(_workKind) ? $"\n{KindName(_workKind).ToUpper()}: clic = colocarla en ese punto · H cambia de pieza · Esc = salir"
                    : $"\nMURO DE {(_workKind == PieceKind.Sacos ? "SACOS (2 u / 10 m)" : "HESCO (6 u / 10 m)")}: clic = punto · Intro o clic derecho = pedir la obra · Esc = cancelar");
                if (!IsPoint(_workKind) && _wallPts.Count >= 4)
                {
                    var plan = PreviewZone(net, _workKind, _wallPts.ToArray());
                    sb.Append(plan.Ok ? $"\n{plan.AllowedMm / 1000f:0.0} m · {plan.CostU} u" + (plan.Cut ? $" · faltan materiales: se construirá hasta aquí ({plan.AllowedMm / 1000f:0.0} de {plan.WantedMm / 1000f:0.0} m)" : "") : "\n" + plan.Why);
                }
            }
            var pieces = net.Works.At(_zoneNode);
            int building = 0; long need = 0, done = 0;
            foreach (var w in pieces) if (!w.Built) { building++; need += w.NeedE4; done += w.WorkE4; }
            if (building > 0) sb.Append($"\nobra en marcha: {building} piezas · {(need > 0 ? done * 100 / need : 0)} %");
            if (_workMsg.Length > 0) sb.Append("\n" + _workMsg);
            return sb.ToString();
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
            if (_zoneNode >= 0 && !_ambush && IsPoint(_workKind))
            {
                // pieza puntual (puerta, nido, foco): un clic = una obra en ese punto, en mm enteros locales al nodo
                var pp = PreviewZone(NetSession.A, _workKind, new[] { ToLocalMm(pt.x), ToLocalMm(pt.z, true) });
                if (!pp.Ok) { _workMsg = "No se puede construir: " + pp.Why; return; }
                NetSession.PushPayload(NetCmdType.PlaceWork, _zoneNode | ((int)_workKind << 16), new[] { ToLocalMm(pt.x), ToLocalMm(pt.z, true) });
                _workMsg = $"Obra pedida: {KindName(_workKind)}, {pp.CostU} u";
                return;
            }
            if (!_hasWallStart)
            {
                _wallStart = pt;
                _hasWallStart = true;
                return;
            }
            if ((pt - _wallStart).magnitude < 1f) return;
            if (_zoneNode >= 0 && !_ambush)
            {
                // H5: en una zona de la red, el muro es una obra: la polilínea se acumula en mm enteros y se paga y se registra en la red al cerrarla
                if (_wallPts.Count == 0) { _wallPts.Add(ToLocalMm(_wallStart.x)); _wallPts.Add(ToLocalMm(_wallStart.z, true)); }
                _wallPts.Add(ToLocalMm(pt.x)); _wallPts.Add(ToLocalMm(pt.z, true));
                _wallStart = pt;
                return;
            }
            Push(CmdType.BuildWall, 0, new Unity.Mathematics.float2(_wallStart.x, _wallStart.z), new Unity.Mathematics.float2(pt.x, pt.z));
            WallVisual(_wallStart, pt, Level.WallHalf * 0.5f, Level.WallH, _mWall);
            _wallStart = pt;
        }

        // ---------------------------------------------------------------- tropas (solo presentación)
        readonly System.Collections.Generic.Dictionary<uint, Transform> _soldierViews = new System.Collections.Generic.Dictionary<uint, Transform>();
        readonly System.Collections.Generic.HashSet<uint> _seenSoldiers = new System.Collections.Generic.HashSet<uint>();
        Material _mSoldier, _mGunner, _mSoldierSprite;
        readonly System.Collections.Generic.Dictionary<uint, Vector3> _soldierPrev = new System.Collections.Generic.Dictionary<uint, Vector3>();
        MaterialPropertyBlock _soldierBlockV; MaterialPropertyBlock _soldierBlock { get { return _soldierBlockV ?? (_soldierBlockV = new MaterialPropertyBlock()); } }

        Material SoldierSpriteMat()
        {
            if (_mSoldierSprite != null) return _mSoldierSprite;
            var m = Mat("ffffff");
            var t = Resources.Load<Texture2D>("Icons/unidades/atlas_fusilero");
            if (t != null) m.SetTexture("_BaseMap", t);
            m.SetFloat("_Cull", 0f); m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
            return _mSoldierSprite = m;
        }
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
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);       // fusilero de Bruno (32 px, cenital) tumbado sobre el suelo
                    go.name = "Soldado " + s.SimId;
                    Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(transform, false);
                    go.transform.localScale = new Vector3(2.0f, 2.0f, 1f);
                    go.GetComponent<Renderer>().sharedMaterial = SoldierSpriteMat();
                    tr = go.transform;
                    _soldierViews[s.SimId] = tr;
                }
                tr.position = new Vector3(s.Pos.x, s.Pos.y + 0.07f, s.Pos.z);
                tr.rotation = Quaternion.Euler(0f, s.Heading * Mathf.Rad2Deg, 0f) * Quaternion.Euler(90f, 0f, 0f);
                // andar mientras se mueve (la posición cambia), disparo el fotograma del tiro; todo solo presentación
                bool moving = _soldierPrev.TryGetValue(s.SimId, out var pp) && ((Vector3)(pp - (Vector3)s.Pos)).sqrMagnitude > 1e-6f;
                _soldierPrev[s.SimId] = (Vector3)s.Pos;
                int cdNow = s.Kind == SoldierKind.MachineGun ? _combat.MgCooldown : _combat.RifleCooldown;
                float col = moving ? Mathf.Floor(Time.time * 8f) % 4f : 0f;
                float row = 1f;                                                   // en la textura la fila 0 (andar) va arriba: v = 0,5
                var mpb = _soldierBlock; tr.GetComponent<Renderer>().GetPropertyBlock(mpb);
                bool firing = s.Cooldown > 0 && s.Cooldown >= cdNow - 6;
                mpb.SetVector("_BaseMap_ST", firing ? new Vector4(0.25f, 0.5f, 0.25f * (s.Cooldown % 3), 0f) : new Vector4(0.25f, 0.5f, 0.25f * col, 0.5f));
                tr.GetComponent<Renderer>().SetPropertyBlock(mpb);
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
