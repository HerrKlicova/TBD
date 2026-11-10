using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using TBD.Sim;
using TBD.Units;
using TBD.Net;

using TBD.Tutorial;
namespace TBD.View
{
    /// <summary>Estado compartido entre el menú y la zona: «Jugar» activa la noche de Sagunto (D-N71).</summary>
    public static class NightRun
    {
        public static bool Active;
        public static bool LeftOnce;               // QA: -leaveat solo una vez
        public sealed class VehInfo { public string Name; public string Tipo; public bool Vivo; public bool Embarcado; }
        /// <summary>Registro estatico de los vehiculos de la noche (nombre, tipo, vivo), en orden de creacion: el id de vehiculo del embarque es el indice (Nacho). Lo lee el mapa siempre, tambien de dia.</summary>
        public static readonly System.Collections.Generic.List<VehInfo> Vehs = new System.Collections.Generic.List<VehInfo>();
        public static bool NocheGanada;           // ya se ganó una noche: el mapa anuncia el objetivo siguiente (Lucía b4c2fe5)
        public static NightSession Session;       // la noche persistente: sobrevive a la escena de la zona (D-N91); la de la última zona
        public static readonly System.Collections.Generic.Dictionary<int, NightSession> Aparcadas = new System.Collections.Generic.Dictionary<int, NightSession>();   // las de las otras zonas (siguen con su reloj)
        public static int Difficulty = 850;      // palanca única (D-N74): 725 fácil, 850 normal, 1000 difícil
    }

    /// <summary>
    /// «La primera noche en Sagunto» (D-N71): pelotones (UnitsWorld) y oleadas (NightGame) de Nacho sobre la zona de Sagunto. La lógica de Nacho es C# puro; aquí solo se
    /// conectan: aparición de zombis en los dos mundos, abatidos por impacto, vista de los soldados con los clips de Teo, selección y órdenes, HUD y pantalla final.
    /// Todo es local (un jugador): los zombis se crean y se retiran igual en A y B, así que el hash de comprobación sigue coincidiendo.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        // ---------------------------------------------------------------- estado
        bool _nightOn, _nightInit; NightGame _ng; UnitsWorld _uw; readonly List<SpawnPoint> _spawns = new List<SpawnPoint>(); StepResult _res = new StepResult();
        readonly List<Target> _tgt = new List<Target>(); readonly HashSet<uint> _killSet = new HashSet<uint>();
        int _nightKills, _nightLost, _nightShotsSpent; int _lastWave = -1; float _bannerUntil; string _bannerText = "";
        /// <summary>QA (Sara): las trazas no se sobrescriben entre corridas: «_normal_s3» con la dificultad y la semilla de -semilla (vacío sin -semilla).</summary>
        static string TrazaSufijo { get { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-semilla"), d = System.Array.IndexOf(a, "-dificultad"); if (i < 0 || i + 1 >= a.Length) return ""; return "_" + (d >= 0 && d + 1 < a.Length ? a[d + 1] : "normal") + "_s" + a[i + 1]; } }
        static uint SemillaQA { get { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-semilla"); uint s = 0; return i >= 0 && i + 1 < a.Length && uint.TryParse(a[i + 1], out s) ? s * 7919u : 0u; } }   // QA (Hugo): semilla distinta por corrida
        int _pilaMaxVista, _pilaMuroMax; NightBot _bot; NightTraceRecorder _rec; int _spawnedTotal; int _resupplies; float _fpsLogT; bool _debugHud; float _nightT0; int _shotsTaken; float _lastStep; string _endReason = "";   // _pilaMuroMax: la pila del muro (la columna pila de la traza), Sara qa46
        readonly List<int> _sel = new List<int>();
        bool _selDrag; Vector2 _selStart; bool _rmbDown; Vector2 _rmbStart; bool _attackNext;
        static readonly Vector2 StationC = new Vector2(113f, 64.5f), TrainC = new Vector2(143f, 64.5f);
        int _nightWaveTotal;

        // ---------------------------------------------------------------- inicio
        void InitNight()
        {
            _nightInit = true; _nightOn = true; _acc = 0f; { long tkI = _worldA.EntityManager.GetComponentData<SimClock>(_rootA).Tick; HashLogCmd("InitNight;tick_ecs;" + tkI); Debug.Log("[NOCHE] InitNight en el tick ECS " + tkI + " (frame " + Time.frameCount + ", Active=" + NightRun.Active + ")"); } _debugHud = false; _nightT0 = Time.realtimeSinceStartup;
            // oleadas, reloj, recursos y fuerza: noche_sagunto.json de Hugo (StreamingAssets/Noche), D-N74/D-N102. Sin el archivo, valores por defecto de Nacho.
            BalanceSource.Reader = f => { var pth = System.IO.Path.Combine(Application.streamingAssetsPath, "Noche", f); return System.IO.File.Exists(pth) ? System.IO.File.ReadAllText(pth) : null; };
            AsegurarNochesRed();                                                         // reloj único: las noches de Sagunto y Nules ya están en la red (si se entra antes de pasar por el mapa)
            NightSim sim = null; int stock = 50 * WeaponCatalog.ShotsPerU;
            try
            {
                // cada zona su noche (Hugo, 9-nov): Nules lee nules.json (hordas 1880/2200/2440 desde la 2.ª noche, guarnición de fusileros y ametralladores); el resto, la de Sagunto
                string estN = NetSession.A != null && _zoneNode >= 0 && _zoneNode < NetSession.A.Graph.Nodes.Count ? NetSession.A.Graph.Nodes[_zoneNode].Name : "Sagunto";
                string fN = estN == "Nules" && System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, "Noche", "nules.json")) ? "nules.json" : "noche_sagunto.json";
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Noche", fN);
                Debug.Log("[NOCHE] noche de " + estN + ": " + fN);
                string key = NightRun.Difficulty < 800 ? "facil" : NightRun.Difficulty <= 950 ? "normal" : "dificil";
                { var cd = System.Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < cd.Length; i++) if (cd[i] == "-dificultad" && (cd[i + 1] == "facil" || cd[i + 1] == "normal" || cd[i + 1] == "dificil")) key = cd[i + 1]; }   // QA: -dificultad facil|normal|dificil
                sim = NightSim.FromJson(System.IO.File.ReadAllText(path), key, 77u); _ng = sim.Night; stock = sim.Units.StockShots; Debug.Log($"[NOCHE] {fN} ({key}): el tren {(_ng.P.TrainNotDefeat ? "no es" : "es")} derrota");
            }
            catch (System.Exception e) { Debug.LogWarning("[TBD] noche_sagunto.json no se pudo leer (" + e.Message + "): valores por defecto"); _ng = new NightGame(NightParams.Default(), 77u); var pd = _ng.P; pd.ScalePermil = NightRun.Difficulty; _ng.P = pd; }
            { var p = _ng.P; var ca = System.Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < ca.Length; i++) if (ca[i] == "-nightclock" && int.TryParse(ca[i + 1], out var ck) && ck > 0) p.ClockX = ck; _ng.P = p; }   // pruebas: reloj mas rapido
            try
            {
                string dir = System.IO.Path.Combine(Application.streamingAssetsPath, "Noche");
                var tu = new CsvTable(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "balance_unidades.csv"))); WeaponCatalog.Load(tu); PlatoonCatalog.Load(tu);
                var tn = new CsvTable(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "balance_noche.csv")));
                var jp = _ng.P; var np = NightParams.FromCsv(tn); np.Waves = jp.Waves; np.Entries = jp.Entries; np.ClockX = jp.ClockX; np.ScalePermil = jp.ScalePermil; np.TrainNotDefeat = jp.TrainNotDefeat; if (sim != null && sim.JsonForce != null && TrenALasPuertas()) np.TrainRect = jp.TrainRect;   // Eugenio (I-Noche110): solo el tren que espera a las puertas va fuera de la puerta este; en el andén, dentro   // Nules (Lucía da2d050; Hugo/Nacho 10:38): el JSON trae el tren fuera de la puerta este y que no es derrota; el CSV no lo tiene
                np.EndHour = (np.StartHour + 23) % 24;                                  // D-N164: sin victoria por tiempo; solo se gana acabando con la horda
                if (_netZone)      // la caja la pone BuildSaguntoAmbience, que es la escena de todas las estaciones (también Nules)
                    np.StationRect = new RectMm(96500, 75200, 115500, 83600);           // Nacho (qa52 131-133): el edificio de la noche = la caja sólida de la zona (BuildBox 96,5–115,5 × 75,2–83,6). Con el de NightParams (98–114 × 75–83), un zombi parado contra la fachada este quedaba a 1,8 m (> alcance de 1,5 m): no atacaba y la noche no acababa
                np.NetClock = RelojUnicoActivo;                                          // reloj único (D-N186): en partida, la noche espera a las 22:00 de la red y va a su ritmo
                _ng = new NightGame(np, 77u + SemillaQA);
            }
            catch (System.Exception e) { Debug.LogWarning("[TBD] balance_*.csv no se pudo leer (" + e.Message + "): valores por defecto de Nacho"); }
            if (NetSession.A != null && _zoneNode >= 0 && _zoneNode < NetSession.A.Graph.Nodes.Count)      // Hugo (nules.json, noche_sagunto.json): el almacén de la zona = min(tope 47 u, reserva de la red); Nules empieza con 0 y el objetivo del día 2 lo llena
            { int reservaU = NetSession.A.Stock[_zoneNode, (int)Res.Municion]; int topeU = 47; stock = System.Math.Min(topeU, System.Math.Max(0, reservaU)) * WeaponCatalog.ShotsPerU; Debug.Log($"[NOCHE] almacén de munición de la zona: min(tope {topeU}, reserva {reservaU}) = {stock / WeaponCatalog.ShotsPerU} u"); }
            _uw = new UnitsWorld(1234u + SemillaQA) { StockShots = stock };
            int west = 32768;                                                        // rumbo −X (hacia la puerta oeste)
            _estacionZona = NetSession.A != null && _zoneNode >= 0 && _zoneNode < NetSession.A.Graph.Nodes.Count ? NetSession.A.Graph.Nodes[_zoneNode].Name : "Sagunto";
            // reloj único (D-N186): la noche de esta zona ya existe en la red (registrada al empezar la partida o al salir); sale de NochesZona (A y B) y la lleva la zona
            if (NightRun.Session != null && NightRun.Session.Node >= 0 && NightRun.Session.Node != _zoneNode)       // otra zona: su noche se aparca (sigue fuera de pantalla) y se toma la de esta, si la hay
            {
                NightRun.Aparcadas[NightRun.Session.Node] = NightRun.Session; NightRun.Session = null;
                Debug.Log($"[NOCHE] la noche de la zona anterior se aparca ({NightRun.Aparcadas.Count} aparcada(s))");
            }
            if (NightRun.Session == null && _zoneNode >= 0 && NightRun.Aparcadas.TryGetValue(_zoneNode, out var esta)) { NightRun.Session = esta; NightRun.Aparcadas.Remove(_zoneNode); }
            if (NightRun.Session != null && NightRun.Session.Node == _zoneNode && _zoneNode >= 0) QuitarNoche(_zoneNode);
            Debug.Log($"[NOCHE] noche de {_estacionZona}: {(NightRun.Session != null ? "la suya, reanudada" : "nueva")}");
            bool resume = NightRun.Session != null && NightRun.Session.Away && NightRun.Session.Ng != null;
            if (resume)                                                              // la noche ya existia: se reanuda con su NightGame y su UnitsWorld (sin sacar otra vez el plantel)
            {
                var S = NightRun.Session; _ng = S.Ng; _uw = S.Uw; _pname.Clear(); foreach (var kv in S.PName) _pname[kv.Key] = kv.Value;
                _wallMax = S.WallMax; _wallHp = S.WallHp; _lastWave = S.LastWave; _nightWaveTotal = S.WaveTotal; _fullTrace.Clear(); if (S.FullTrace != null) _fullTrace.Append(S.FullTrace.ToString());
            }
            else _pname.Clear();
            // D-N173 (Iker): cada estación saca su propio plantel (Plantel.Pelotones); Sagunto, los cinco de siempre en los mismos sitios
            if (!resume) foreach (var pp in TBD.Net.Plantel.Pelotones(_estacionZona, TutorialHud.Activo)) { var pl = _uw.AddPlatoon(pp.Tipo, pp.X, pp.Y, pp.Rumbo, pp.Formacion); if (pp.CotaMm > 0) pl.EyeMm = UnitsWorld.EyeInfantryMm + pp.CotaMm; if (pp.Hombres > 0) while (pl.Men.Count > pp.Hombres) pl.Men.RemoveAt(pl.Men.Count - 1); _pname[pl.Id] = pp.Nombre; }   // D-N183: puestos de cada estación (Álvaro v1.1); en alto (azotea, banqueta), la boca sube con el suelo
            Debug.Log($"[NOCHE] plantel de {_estacionZona}: {_pname.Count} pelotones"); if (_estacionZona == "Nules") TutorialHud.Senal(TutorialGuion.Senales.ZonaNules);   // 3.3 (Lucía 7e4b903)
            _escolta.Clear();
            {   // tropa bajada del tren en esta estación (Embarque.AtStation): sale junto al tren y el bot no la manda al muro (Hugo/Nacho, 9-nov)
                var wz = NetSession.A; var tr = _ng.P.TrainRect; int ex = (tr.X0 + tr.X1) / 2, ey = tr.Y0 - 2900;   // junto al tren por el sur: en Nules A (192; 60), la escolta de la emboscada de Hugo
                if (sim != null && sim.JsonForce != null && sim.JsonForce.Count > 1 && (wz == null || wz.LastAmbush.Tick < 0 || wz.LastAmbush.HoldUntil <= wz.Tick)) { ex = sim.JsonForce[1].Value[0]; ey = sim.JsonForce[1].Value[1]; }
                if (sim != null && sim.JsonForce != null && sim.JsonForce.Count > 1) _puestoEnviado = new Vector2Int(sim.JsonForce[1].Value[0], sim.JsonForce[1].Value[1]);   // de noche, el pelotón enviado va donde lo midió NightSim (71,5; 70,5)
                if (wz != null && _zoneNode >= 0) foreach (var pid in wz.Embarque.AtStation(_zoneNode, PasajeroTipo.Peloton)) { _embVistos.Add(pid); if (resume && !NightRun.Session.Emb.Add(pid)) continue; var pe = _uw.AddPlatoon(PlatoonKind.Fusileros, ex + 6000 * _escolta.Count, ey, west, Formation.Linea); _pname[pe.Id] = "Escolta " + (_escolta.Count + 1); _escolta.Add(pe.Id); }
                string trenAqui = "ninguno"; if (wz != null) foreach (var sv in wz.Services) if (sv.AtStop && sv.Stops.Count > 0 && sv.Stops[sv.StopIndex].Node == _zoneNode) trenAqui = sv.Train.Name + (sv.GateNode == _zoneNode ? " (a las puertas)" : " (en el andén)");
                if (DiaEnLaRed()) { _esperandoNoche = true; Debug.Log($"[NOCHE] {_estacionZona} de día ({NetConst.Clock(wz != null ? wz.Tick : 0)}): la noche empieza a las 22:00 de la red"); }
                else if (trenAqui == "ninguno" && wz != null && _ng.P.TrainNotDefeat) { _ng.TrainHp = 0; _trenPerdidoAvisado = true; _nocheSinTren = true; Debug.Log("[NOCHE] sin tren en la estación: la noche empieza sin tren (TrainHp = 0)"); }   // Hugo: si no, la noche mataba un tren que no estaba (traza de la 104); con el arreglo de Marcos (Level.TrainNightX) la horda ya no se va a X 900
                Debug.Log($"[NOCHE] tren en {_estacionZona}: {trenAqui}; rectángulo del tren en la zona X {tr.X0 / 1000f:0.0}–{tr.X1 / 1000f:0.0}, Z {tr.Y0 / 1000f:0.0}–{tr.Y1 / 1000f:0.0} m; edificio X {_ng.P.StationRect.X0 / 1000f:0.0}–{_ng.P.StationRect.X1 / 1000f:0.0}, Z {_ng.P.StationRect.Y0 / 1000f:0.0}–{_ng.P.StationRect.Y1 / 1000f:0.0}; escolta {_escolta.Count} pelotón(es) en ({ex / 1000f:0.0}; {ey / 1000f:0.0}); el tren {(_ng.P.TrainNotDefeat ? "no es" : "es")} derrota");
            }
            if (!resume && _estacionZona == "Sagunto" && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sinazotea") < 0)      // D-N174 (Lucia, Alvaro 10.15): 2 pelotones de tiradores libres en el ala oeste del edificio de viajeros, boca a 6,35 m
                for (int az = 0; az < 2; az++) { var pa = _uw.AddPlatoon(PlatoonKind.Tiradores, 84500, 74300 + 4000 * az, west, Formation.Cuna); pa.EyeMm = 6350; _pname[pa.Id] = "Tiradores " + (az == 0 ? "azotea 1" : "azotea 2"); }
            bool cercoAqui = NetSession.A != null && NetSession.A.Cercada(_zoneNode);      // D-N188: en la estación cercada los zombis iniciales SON el cerco (se quedan; sus abatidos van a CercoBajas)
            if (cercoAqui) Debug.Log($"[CERCO] {_estacionZona} cercada: {NetSession.A.CercoZombies} zombis del cerco en la zona");
            if (!cercoAqui) foreach (var wld in new[] { _worldA, _worldB })                          // sin los zombis iniciales de la zona: solo las oleadas
            {
                if (wld == null || !wld.IsCreated) continue;
                using (var q = wld.EntityManager.CreateEntityQuery(typeof(Zombie))) wld.EntityManager.DestroyEntity(q);
            }
            _targetOrtho = 26f; _focus = new Vector3(96f, 0f, 64f);
            { var cf = System.Environment.GetCommandLineArgs(); for (int fi = 0; fi + 1 < cf.Length; fi++) if (cf[fi] == "-focus") { var pr = cf[fi + 1].Split((char)44); if (pr.Length == 2 && float.TryParse(pr[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fx0) && float.TryParse(pr[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fz0)) _focus = new Vector3(fx0, 0f, fz0); else Debug.LogWarning("[NOCHE] -focus x,z"); } else if (cf[fi] == "-zoom0" && float.TryParse(cf[fi + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var zz0)) _targetOrtho = zz0; }   // QA: encuadre inicial
            ApplyGrade(2);
            _paletteMix = 0f;                                          // D-N129 (Lucia y Bruno): el mundo no pasa por el cuantizador; la paleta queda solo en los albedos y la interfaz
            { var ca2 = System.Environment.GetCommandLineArgs(); for (int i2 = 0; i2 + 1 < ca2.Length; i2++) if (ca2[i2] == "-luz") NightLightPass(ca2[i2 + 1]); }
            PruebaRenderIniciar(); BarridoIniciar();                                     // -pruebarender N (Lucía 09-11): zombis solo de vista para medir el render
            { string est = string.IsNullOrEmpty(_estacionZona) ? "Sagunto" : _estacionZona; int np = 0; foreach (var pl in _uw.Platoons) if (pl.AliveCount() > 0) np++;   // Lucía (7db76ef): título de entrada, 3 s, en hueso
              _bannerText = NetSession.A != null && _zoneNode >= 0 && _zoneNode == NetSession.A.Director.CerradaNodo ? est + " · cercada" : $"{est} · {np} pelotones"; _bannerUntil = Time.unscaledTime + 3f; Debug.Log("[NOCHE] título: " + _bannerText); }
            NearWall(Vector2.zero); Debug.Log($"[NOCHE] muros_obras={_wallSegs.Count}");
            Debug.Log($"[NOCHE] reloj_x={_ng.P.ClockX} municion_u={_uw.StockShots / (float)WeaponCatalog.ShotsPerU:0.#} materiales_u=40 combustible_u=30 almacen_combustible_u=16");
            if (_sel.Count == 0 && _uw.Platoons.Count > 0) _sel.Add(_uw.Platoons[0].Id);
            InitVehicles(); Debug.Log($"[NOCHE] vehículos de {_estacionZona}: {_vehs.Count} ({string.Join(", ", _vehs.ConvertAll(v => v.Name))})");
            NaBegin();                                                                   // [NOCHE-ARMAS]: u y dotación al empezar (solo vista)
            _rec = resume && NightRun.Session.Rec != null ? NightRun.Session.Rec : new NightTraceRecorder("zona", 1285, 9000, 30);
            if (resume && NightRun.Session.Bot != null) _bot = NightRun.Session.Bot;
            LosInit();   // D-N174: linea de vision (sagunto_obstaculos_v1.csv de Alvaro); -sinlos la apaga
            if (!resume && NightRun.Session == null) NightRun.Session = new NightSession { Ng = _ng, Uw = _uw, Node = _zoneNode };
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nightbot") >= 0) { try { _bot = NightBot.FromParams(NightSimParams.FromJson(BalanceSource.Read("nightsim_params.json"))); Debug.Log("[NOCHE] prueba: -nightbot (NightBot de Nacho)"); } catch (System.Exception e) { Debug.LogWarning("[TBD] NightBot: " + e.Message); } }
            if (_bot != null) foreach (var kvb in _pname) if (kvb.Value.StartsWith("Tiradores azotea")) _bot.Busy.Add(kvb.Key);
            if (_bot != null) foreach (var eid in _escolta) _bot.Busy.Add(eid);      // la escolta se queda junto al tren (Hugo/Nacho)      // el bot no mueve a los de la azotea (libres: solo el jugador)
            if (resume) ResumeNightSession();
        }

        // ---- QA (Bruno): doble codificacion sRGB en partida. -colorcasos recorre 5 casos, uno cada 6 s reales, con una captura en cada uno (F6 pasa al siguiente a mano)
        int _colorCase = -1; float _colorCaseT = -1f; bool _colorAuto = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-colorcasos") >= 0;
        static readonly string[] ColorCaseNames = { "base", "a_fx_sin_post", "b_fx_y_present_sin_post", "c_sin_hdr", "d_sin_fxcam_en_pila" };
        void ColorCases()
        {
            var kb = Keyboard.current; bool next = kb != null && kb.f6Key.wasPressedThisFrame;
            if (_colorAuto && _colorCaseT < 0f) _colorCaseT = Time.unscaledTime + 8f;
            if (_colorAuto && _colorCaseT > 0f && Time.unscaledTime >= _colorCaseT && _colorCase < ColorCaseNames.Length - 1) { next = true; }
            if (!next) return;
            if (_colorCase >= 0 && _colorCase < ColorCaseNames.Length) { }
            _colorCase++; if (_colorCase >= ColorCaseNames.Length) _colorCase = 0; _colorCaseT = Time.unscaledTime + 6f;
            var fxD = _fxCam.GetUniversalAdditionalCameraData(); var prD = _presentCam.GetUniversalAdditionalCameraData();
            fxD.renderPostProcessing = true; prD.renderPostProcessing = true; _fxCam.allowHDR = true; _presentCam.allowHDR = true;
            if (!prD.cameraStack.Contains(_fxCam)) prD.cameraStack.Add(_fxCam);
            switch (_colorCase)
            {
                case 1: fxD.renderPostProcessing = false; break;
                case 2: fxD.renderPostProcessing = false; prD.renderPostProcessing = false; break;
                case 3: _fxCam.allowHDR = false; _presentCam.allowHDR = false; break;
                case 4: prD.cameraStack.Remove(_fxCam); break;
            }
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Captures")); System.IO.Directory.CreateDirectory(dir);
            Debug.Log("[NOCHE] caso de color " + ColorCaseNames[_colorCase]);
            StartCoroutine(ColorCaseShot(System.IO.Path.Combine(dir, "color_" + _colorCase + "_" + ColorCaseNames[_colorCase] + ".png")));
        }
        System.Collections.IEnumerator ColorCaseShot(string path) { yield return new WaitForSeconds(2f); yield return CaptureRoutine(path); }

        /// <summary>QA24 (Bruno): una sola luz sobre la imagen sin gradacion ni bloom. luna = sol blanco 1,0 con sombras; ambiente = luz ambiente blanca 0,1; focos = solo focos a 90.</summary>
        void NightLightPass(string mode)
        {
            _nightGradeLock = true; _paletteMix = 0f;
            _grade.colorFilter.Override(Color.white); _grade.saturation.Override(0f); _grade.postExposure.Override(0f); if (_bloom != null) _bloom.intensity.Override(0f);
            if (mode == "luna") { SetLight(Color.white, 1.0f, Color.black, 50f); SetFloodlights(0f); }
            else if (mode == "ambiente") { SetLight(Color.black, 0f, new Color(0.1f, 0.1f, 0.1f), 50f); SetFloodlights(0f); }
            else if (mode == "focos") { SetLight(Color.black, 0f, Color.black, 50f); SetFloodlights(90f); }
            else if (mode == "actual") { }
            else if (mode == "dia") { ApplyGrade(0); _paletteMix = 0.65f; }   // 09-11 (Lucía, recorte por alfa): la misma noche con la luz y la gradación de día, para capturas de día (la simulación sigue siendo la de noche)
            Debug.Log("[NOCHE] pasada de luz: " + mode);
        }

        // ---------------------------------------------------------------- un tick (30 Hz), antes de los sistemas de la zona
        readonly List<int> _tramosPm = new List<int>();
        static readonly bool _sinHorda = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sinhorda") >= 0;
        readonly RutaCap3 _rutaCap3 = new RutaCap3();         // paso fijo (160; 40) para rodear la esquina SE del fuerte (capítulo 3, Iker)
        void NightTick()
        {
            if (!_nightOn || _ng == null) return;
            if ((_ng.Tick % 30) == 0) EnviarCercoBajas(25);
            RecogerEntregaAerea(); if ((_ng.Tick & 15) == 0) EscoltaAlAsedio();                                                     // D-N190: munición que deja un NH90 en esta zona
            bool playing = _ng.State == NightState.Jugando;
            if (_ng.Tick - _gateTk >= 30)    // las puertas de SagunteStart nacen construidas con la noche inactiva: SyncWorks no les hace el muro. Aqui, una sola vez por pieza (idempotente), por la cola como el resto de ordenes (Nacho)
            {
                _gateTk = _ng.Tick;
                var gp = NetSession.A != null && _zoneNode >= 0 ? NetSession.A.Works.At(_zoneNode) : null;
                if (gp != null) for (int gi = 0; gi < gp.Count; gi++)
                {
                    var w = gp[gi]; if (!w.IsGate || !w.Built || _gateWalled.Contains(gi)) continue;
                    _gateWalled.Add(gi);
                    Push(CmdType.BuildWall, 2500, new Unity.Mathematics.float2(WorkOrigin.x + w.X0 / 1000f, WorkOrigin.y + w.Y0 / 1000f), new Unity.Mathematics.float2(WorkOrigin.x + w.X1 / 1000f, WorkOrigin.y + w.Y1 / 1000f));
                    Debug.Log($"[NOCHE] puerta {w.Kind} cerrada con muro: ({WorkOrigin.x + w.X0 / 1000f:0.0};{WorkOrigin.y + w.Y0 / 1000f:0.0}) -> ({WorkOrigin.x + w.X1 / 1000f:0.0};{WorkOrigin.y + w.Y1 / 1000f:0.0})");
                }
            }
            if (_ng.State == NightState.Derrota && !_endDismissed) return;            // derrota: el mundo se para
            _spawns.Clear();
            bool diaRed;
            if (_ng.P.NetClock) { if (_ng.StartNetTick < 0 || _ng.State == NightState.Victoria) ArranqueRedZona(); diaRed = _ng.StartNetTick < 0; if (diaRed) AlmacenDeDia(); else _reservaVista = -1; }   // reloj único: la zona arranca su noche a las 22:00 de la red (Begin) y no avanza antes
            else { diaRed = DiaEnLaRed(); if (!diaRed && _esperandoNoche) IniciaNocheZona(); }      // S1 qa52 (Marcos): noche con reloj propio, parche de día
            if (!TutorialHud.HordaEnEspera && !diaRed) { var st0 = _ng.State; _ng.Attack(_pileNearStation, _nearTrain); SurroundedTrainZona(_nearTrain); _ng.ZombiesAlive = _zomCounted ? _zomPos.Count : -1; _ng.Step(_spawns); if (st0 == NightState.Jugando && _ng.State != NightState.Jugando) NightEnded(); if (_ng.State == NightState.Derrota) return; }   // D-N136: NightGame gana al acabar con la horda (WinOnClear, misma regla que NightSim)
            playing = _ng.State == NightState.Jugando;

            // oleadas: líneas de registro y rótulo
            int wv = CurrentWave();
            if (playing && wv != _lastWave && wv >= 0)
            {
                _lastWave = wv; _nightWaveTotal = _ng.WaveTotal(wv); _waveBannerT0 = Time.unscaledTime;
                string time = $"{_ng.P.StartHour + _ng.P.Waves[wv].StartMinute / 60:00}:{_ng.P.Waves[wv].StartMinute % 60:00}".Replace("24:", "00:");
                // D7 (Lucía, mezcla de Nuria): sin cartel «OLEADA n» ni sfx_alerta_oleada; el ataque lo anuncia la alarma (D-N136: no hay oleadas)
                Debug.Log($"[NOCHE] oleada={wv + 1} hora={TimeStr()} total={_nightWaveTotal} ammo_u={_uw.StockShots / (float)WeaponCatalog.ShotsPerU:0.0} recargas={_resupplies} pila={_pileWall} muro_permil={_ng.WallPermil} bajas={_nightLost}");
            }

            // zombis nuevos (en los dos mundos, en el mismo punto)
            EmboscadaEnZona(_spawns);                                                   // partida de 30 min: la emboscada de la red con el jugador en la zona (300 zombis)
            if (_sinHorda) _spawns.Clear();                                           // QA (Sara, qa48 «vacío»): -sinhorda no saca zombis
            if (_spawns.Count > 0) SpawnZombies(_spawns);

            // soldados: objetivos = zombis a menos de 60 m de algún soldado
            _tgt.Clear();
            var emA = _worldA.EntityManager;
            using (var q = emA.CreateEntityQuery(typeof(Zombie)))
            {
                var zs = q.ToComponentDataArray<Zombie>(Allocator.Temp);
                zs.Sort(new ZombieSimIdOrder());      // el orden de los chunks cambia con los cambios estructurales por fotograma: la noche siempre ve a los zombis por SimId (turbo determinista)
                int nearStation = 0, nearTrain = 0; _zomPos.Clear(); _zomId.Clear(); int wallPile = 0;
                for (int i = 0; i < zs.Length; i++)
                {
                    var z = zs[i]; var pz = new Vector2(z.Pos.x, z.Pos.z); _zomPos.Add(pz); _zomId.Add(z.SimId); if (NearWall(pz)) wallPile++;
                    if ((pz - StationC).sqrMagnitude < 64f) nearStation++;
                    if ((pz - TrainC).sqrMagnitude < 49f) nearTrain++;
                    bool near = false;
                    for (int pi = 0; pi < _uw.Platoons.Count && !near; pi++)
                    {
                        var pl = _uw.Platoons[pi]; float dx = z.Pos.x * 1000f - pl.AX, dy = z.Pos.z * 1000f - pl.AY;
                        if (dx * dx + dy * dy < 60000f * 60000f) near = true;
                    }
                    if (near) _tgt.Add(new Target { Id = (int)z.SimId, X = (int)(z.Pos.x * 1000f), Y = (int)(z.Pos.z * 1000f) });
                }
                zs.Dispose();
                _zomCounted = true;
                if ((_ng.Tick % NightSim.ClimberRetargetTicks) == 0) ClimberGoals();   // D-N148 (Nacho)   // D-N136 (Nacho): _zomPos ya es la cuenta de vivos de ECS; NightGame gana al llegar a 0 con toda la horda dentro
                { for (int ci = 0; ci < _zomPos.Count; ci++) { var zp = _zomPos[ci]; bool inside = zp.x > Level.FortX0 + 1f && zp.x < Level.FortX1 - 1f && zp.y > Level.FortY0 + 1f && zp.y < Level.FortY1 - 1f; uint zid = _zomId[ci]; if (inside) { if (_insideIds.Add(zid)) { _climbSec++; _entTot++; float dW = zp.x - Level.FortX0, dE = Level.FortX1 - zp.x, dS = zp.y - Level.FortY0, dN = Level.FortY1 - zp.y; float mn = Mathf.Min(Mathf.Min(dW, dE), Mathf.Min(dS, dN)); _entSide[mn == dW ? 0 : mn == dS ? 1 : mn == dE ? 2 : 3]++; } } else _insideIds.Remove(zid); }
                  { var tr = _ng.P.TrainRect; float r = _ng.P.AttackReachMm / 1000f; float tx0 = tr.X0 / 1000f - r, tx1 = tr.X1 / 1000f + r, ty0 = tr.Y0 / 1000f - r, ty1 = tr.Y1 / 1000f + r; int tin = 0, tout = 0; for (int ci = 0; ci < _zomPos.Count; ci++) { var zp = _zomPos[ci]; if (zp.x < tx0 || zp.x > tx1 || zp.y < ty0 || zp.y > ty1) continue; bool insideF = zp.x > Level.FortX0 + 1f && zp.x < Level.FortX1 - 1f && zp.y > Level.FortY0 + 1f && zp.y < Level.FortY1 - 1f; if (insideF) tin++; else tout++; } _trainIn = tin; _trainOut = tout; } }
                _allT.Clear(); for (int i = 0; i < _zomPos.Count; i++) _allT.Add(new Target { Id = (int)_zomId[i], X = (int)(_zomPos[i].x * 1000f), Y = (int)(_zomPos[i].y * 1000f) });
                _ng.CountAttackers(_allT, AimsStationZona, out _pileNearStation, out _nearTrain); { var trr = _ng.P.TrainRect; float tcx = (trr.X0 + trr.X1) / 2000f, tcy = (trr.Y0 + trr.Y1) / 2000f; bool trenFuera = tcx < Level.FortX0 || tcx > Level.FortX1 || tcy < Level.FortY0 || tcy > Level.FortY1; _nearTrain = trenFuera ? _trainOut : _trainIn + _trainOut; }   if (_pileNearStation > _pilaMaxVista) _pilaMaxVista = _pileNearStation; TutorialHud.ComprobarC90(_uw, _allT, (cx, cy) => cx > (int)(Level.FortX0 * 1000f) + 1000 && cx < (int)(Level.FortX1 * 1000f) - 1000 && cy > (int)(Level.FortY0 * 1000f) + 1000 && cy < (int)(Level.FortY1 * 1000f) - 1000);      // Nacho (S1 qa52 128): alrededor del TrainRect real, no del punto fijo del andén (con el tren aparcado fuera daba 0)
                if (_rutaCap3.Pendientes > 0)
                    foreach (var rid in _rutaCap3.Ids) { var rp = _uw.Find(rid); if (rp == null) continue; if (_rutaCap3.Seguir(rid, rp.AX, rp.AY, out var rtx, out var rty, out var rh, out var ra)) { if (ra) _uw.AttackMove(rid, rtx, rty, rh); else _uw.Move(rid, rtx, rty, rh); } }
                if (TutorialHud.Activo)
                {   // las 4 señales de la zona para los capítulos 1 y 2 (Iker): zombis vivos, vehículos fuera del tren y la vida de cada tramo del muro
                    int vf = 0; foreach (var v in _vehs) if (v.Go != null && v.Go.activeSelf) vf++;
                    _tramosPm.Clear(); foreach (var sg in _segs) if (!sg.IsGate && sg.Max > 0f) _tramosPm.Add(sg.Open ? 0 : (int)(sg.Hp * 1000f / sg.Max));
                    TutorialHud.Zona(_zomPos.Count, vf, _tramosPm);
                    TutorialHud.Estado(TutorialGuion.Senales.HayVehiculos, vf > 0);      // 1.6b/1.6c solo con vehículos
                    {   // 2.1 (Lucía): el capítulo 2 no empieza con horda a menos de 15 m del tramo más dañado del muro oeste
                        Seg peor = null; foreach (var sg in _segs) if (!sg.IsGate && sg.Max > 0f && (peor == null || sg.Hp / sg.Max < peor.Hp / peor.Max)) peor = sg;
                        bool libre = true; if (peor != null) foreach (var zp in _zomPos) if (DistSeg(zp, peor.A, peor.B) <= 15f) { libre = false; break; }
                        TutorialHud.Estado(TutorialGuion.Senales.TramoDespejado, libre);
                    }
                    // recorrido del capítulo 3: pelotones fuera del muro (zombi a ≤ 25 m), en el flanco sur (≤ 10 m) y en R2 (≤ 6 m)
                    bool cerca = false, enFlanco = false, enR2 = false;
                    foreach (var pl in _uw.Platoons)
                    {
                        if (pl.AliveCount() == 0) continue;
                        float px = pl.AX / 1000f, py = pl.AY / 1000f;
                        if (px > Level.FortX0 && px < Level.FortX1 && py > Level.FortY0 && py < Level.FortY1) continue;      // dentro del recinto no cuenta
                        if ((new Vector2(px, py) - new Vector2(RutaCap3.FlancoSur[0] / 1000f, RutaCap3.FlancoSur[1] / 1000f)).sqrMagnitude < 100f) enFlanco = true;
                        if ((new Vector2(px, py) - new Vector2(RutaCap3.R2[0] / 1000f, RutaCap3.R2[1] / 1000f)).sqrMagnitude < 36f) enR2 = true;
                        if (!cerca) foreach (var zp in _zomPos) if ((zp - new Vector2(px, py)).sqrMagnitude <= 625f) { cerca = true; break; }
                    }
                    TutorialHud.Recorrido(cerca, enFlanco, enR2);
                }
                if ((_nearTrain > 0 || _pileNearStation > 0) && _diagN < 6 && Time.unscaledTime - _diagT > 2f)
                {
                    _diagN++; _diagT = Time.unscaledTime; var sbd = new System.Text.StringBuilder(); int k = 0;
                    for (int i = 0; i < _zomPos.Count && k < 8; i++) { var zq = _zomPos[i]; if (zq.x > 66f && zq.x < 160f && zq.y > 60f && zq.y < 69f || (zq - StationC).sqrMagnitude < 144f) { sbd.Append($"({zq.x:0.0};{zq.y:0.0}) "); k++; } }
                    Debug.Log($"[NOCHE-DIAG] tren={_nearTrain} estacion={_pileNearStation} hora={TimeStr()} muro_permil={_ng.WallPermil} zombis_cerca={sbd}");
                    if (_diagN == 1) { var sw = new System.Text.StringBuilder(); foreach (var sg in _wallSegs) sw.Append($"[{sg.a.x:0.0};{sg.a.y:0.0}->{sg.b.x:0.0};{sg.b.y:0.0}] "); Debug.Log("[NOCHE-DIAG] muros_obras " + sw); }
                }
                TramosTick();
                _pileWall = wallPile; if (wallPile > _pilaMuroMax) _pilaMuroMax = wallPile;
                if (_wallMax <= 0f) _wallMax = _wallHp = Mathf.Max(1200000, NightSimParams.Default().WallHp);
                _wallHp = Mathf.Max(0f, _wallHp - wallPile * (NightSimParams.Default().WallDmgPerZombieS / 30f)); _ng.WallPermil = Mathf.RoundToInt(1000f * _wallHp / _wallMax);
            }
            if (_bot != null) { NaResupAntes(); _bot.Step(_uw, _tgt, _ng.Tick); NaResupDespues(); }                          // -nightbot: el bot de Nacho decide como el jugador (ordenes, recarga al 30 %)
            _res.Clear(); _uw.Step(_tgt, _res);
            _nightShotsSpent += _res.AmmoSpent;
            _contact.Clear(); _nightLost += _uw.ZombieContact(_tgt, 900, 2, _contact);
            _spawnedTotal += _spawns.Count;
            if (_rec != null && !_rec.Done)
            {
                long shotsLeft = _uw.StockShots; foreach (var plx in _uw.Platoons) if (plx.AliveCount() > 0) shotsLeft += plx.AmmoShots;
                _rec.Sample(_ng.Tick, _spawnedTotal, _zomPos.Count, _pileWall, (long)_wallHp, (long)_wallMax, _nightKills, (int)shotsLeft, _pileNearStation, _nearTrain);
                if (_rec.Done)
                {
                    try
                    {
                        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Captures")); System.IO.Directory.CreateDirectory(dir);
                        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "traza-zona" + TrazaSufijo + ".csv"), _rec.Trace.ToCsv());
                        Debug.Log("[NOCHE-TRAZA] " + NightTraceTools.CompareWithReference(_rec.Trace).Text);
                    }
                    catch (System.Exception e) { Debug.LogWarning("[TBD] traza: " + e.Message); }
                }
            }
            if (_ng.Tick % 30 == 0 && _ng.State == NightState.Jugando)   // traza de la noche entera (Sara): una fila por segundo de juego con la vida del tren y de la estacion
            {
                if (_fullTrace.Length == 0) _fullTrace.Append("tick,hora,estacion_hp,tren_hp,muro_hp,zombis,pila,atacan_tren,atacan_estacion,abatidos,bajas,trepan,entran,entran_oeste,entran_sur,entran_este,entran_norte,tren_x,tren_x0,tren_x1,tren_y0,tren_y1,atacan_tren_dentro,atacan_tren_fuera" + (char)10);
                if (_pileWall > _pilaMuroMax) _pilaMuroMax = _pileWall; _fullTrace.Append($"{_ng.Tick},{TimeStr()},{_ng.StationHp},{_ng.TrainHp},{(long)_wallHp},{_zomPos.Count},{_pileWall},{_nearTrain},{_pileNearStation},{_nightKills},{_nightLost},{_climbSec},{_entTot},{_entSide[0]},{_entSide[1]},{_entSide[2]},{_entSide[3]},{Mathf.RoundToInt(TrainXEcs())},{_ng.P.TrainRect.X0 / 1000},{_ng.P.TrainRect.X1 / 1000},{_ng.P.TrainRect.Y0 / 1000},{_ng.P.TrainRect.Y1 / 1000},{_trainIn},{_trainOut}" + (char)10); _climbSec = 0;
            }
            if (_uw.Tick % 15 == 0 && _uw.StockShots > 0)                                // reabastecimiento automatico: bajo el 30 % de su dotacion, del almacen (la T queda como recarga anticipada)
            {
                NaResupAntes();
                foreach (var plr in _uw.Platoons) if (plr.AliveCount() > 0 && plr.AmmoCap > 0 && plr.AmmoShots * 100 < plr.AmmoCap * 30 && _uw.Resupply(plr.Id) > 0) _resupplies++;
                NaResupDespues();
            }
            VehiclesTick(default(NativeSnapshot)); if (_vehHits.Count > 0) { KillZombies(_vehHits, true); _vehHits.Clear(); } _naVehHitOwner.Clear(); _naVehHitAtropello.Clear();
            if (_res.Hits.Count > 0) KillZombies(_res.Hits);
            for (int i = 0; i < _res.Shots.Count; i++) NightShotFx(_res.Shots[i]);
            NaExplosiones(_res.Shots);      // [NOCHE-ARMAS]: explosiones del AG36 y del C90 por pelotón (Sara)
            for (int i = 0; i < _res.Blasts.Count; i++) { var b = _res.Blasts[i]; VfxBlast(new Vector3(b.X / 1000f, 0f, b.Y / 1000f), b.RadiusMm / 1000f, b.Weapon); }
        }
        readonly List<(Vector2 a, Vector2 b)> _wallSegs = new List<(Vector2, Vector2)>(); float _wallSegsT = -10f; int _wallSegsTk = -1000, _gateTk = -1000;

        /// <summary>¿El zombi esta a 1,5 m o menos de un muro construido de las obras? Los segmentos salen de Works (se refrescan cada 2 s).</summary>
        bool NearWall(Vector2 p)
        {
            if (_ng.Tick - _wallSegsTk >= 60)
            {
                _wallSegsTk = _ng.Tick; _wallSegs.Clear();
                var pieces = NetSession.A != null && _zoneNode >= 0 ? NetSession.A.Works.At(_zoneNode) : null;
                if (pieces != null) foreach (var w in pieces) if ((w.IsWall || w.IsGate) && w.Built) _wallSegs.Add((new Vector2(WorkOrigin.x + w.X0 / 1000f, WorkOrigin.y + w.Y0 / 1000f), new Vector2(WorkOrigin.x + w.X1 / 1000f, WorkOrigin.y + w.Y1 / 1000f)));
            }
            for (int i = 0; i < _wallSegs.Count; i++)
            {
                var ab = _wallSegs[i].b - _wallSegs[i].a; float l2 = ab.sqrMagnitude; float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - _wallSegs[i].a, ab) / l2);
                if ((_wallSegs[i].a + ab * t - p).sqrMagnitude < 2.25f) return true;
            }
            return false;
        }
        struct ZombieSimIdOrder : IComparer<Zombie> { public int Compare(Zombie a, Zombie b) { return a.SimId.CompareTo(b.SimId); } }
        NightSim _aggroSh; readonly Dictionary<int, Vector2Int> _aggroGoal = new Dictionary<int, Vector2Int>();
        bool _zomCounted;
        readonly List<Target> _cgMen = new List<Target>(), _cgVeh = new List<Target>(), _cgWork = new List<Target>();
        /// <summary>D-N148 (Nacho): a los zombis que han pasado el muro (dentro del recinto) les pone su objetivo con ClimberTargets.Pick, la misma regla que NightSim:
        /// lo vivo más cercano a menos de 30 m (infantería, vehículos parados, obreros) y, si no hay nadie, el tren. En los dos mundos, con las mismas listas.
        /// Los de fuera siguen el campo de flujo (HasGoal = 0).</summary>
        /// <summary>D-N180 (2), copia de NightSim.TrainReachableFromInside: el tren existe y está dentro del recinto.</summary>
        /// <summary>D-N181 (Lucía; la misma regla que NightSim): fuera del recinto el zombi cuenta siempre para el edificio; dentro, solo si su objetivo es la estación (D-N180).</summary>
        bool AimsStationZona(int i) { if (i < 0 || i >= _zomPos.Count) return true; var p = _zomPos[i]; bool dentro = p.x > Level.FortX0 && p.x < Level.FortX1 && p.y > Level.FortY0 && p.y < Level.FortY1; return dentro && _aimStation.Contains((int)_zomId[i]); }      // D-N193: solo los de dentro del recinto que van a por la estación

        bool TrainReachableZona() { var r = _ng.P.TrainRect; return _ng.TrainHp > 0 && r.X1 > (int)(Level.FortX0 * 1000f) && r.X0 < (int)(Level.FortX1 * 1000f) && r.Y1 > (int)(Level.FortY0 * 1000f) && r.Y0 < (int)(Level.FortY1 * 1000f); }

        /// <summary>D-N180 (1), copia de NightSim.SurroundedTrain: un tren rodeado sin tropa a 60 m pierde al menos TrainHp / (10 min de juego) por tick. Va dentro del tick (determinista).</summary>
        void SurroundedTrainZona(int atTrain)
        {
            const long reachMm = 60000;      // D-N180: el tren rodeado sin tropa a menos de 60 m cae en 100 min de juego
            if (atTrain <= 0 || _ng.TrainHp <= 0 || _uw == null) return;
            var r = _ng.P.TrainRect; long reach2 = reachMm * reachMm;
            foreach (var p in _uw.Platoons) { if (p.Aboard != 0) continue; foreach (var m in p.Men) { if (!m.Alive) continue; long dx = System.Math.Max(0, System.Math.Max(r.X0 - m.X, m.X - r.X1)), dy = System.Math.Max(0, System.Math.Max(r.Y0 - m.Y, m.Y - r.Y1)); if (dx * dx + dy * dy <= reach2) return; } }
            long lossTicks = System.Math.Max(1L, _ng.TicksForGameSeconds(100L * 60));      // Nacho: 100 min (D-N180); con el reloj de la red, 1750 ticks
            int floor = (int)((_ng.P.TrainHp + lossTicks - 1) / lossTicks);
            _ng.DamageTrain(floor - _ng.LastDmgTrain);
        }

        readonly HashSet<int> _aimStation = new HashSet<int>();   // D-N181: los de dentro que tienen la estación de objetivo (D-N180); solo ellos cuentan para el edificio
        void ClimberGoals()
        {
            _cgMen.Clear(); _cgVeh.Clear(); _aimStation.Clear();
            foreach (var p in _uw.Platoons) { if (p.Aboard != 0) continue; foreach (var m in p.Men) if (m.Alive) _cgMen.Add(new Target { Id = m.Id, X = m.X, Y = m.Y }); }
            for (int i = 0; i < _vehs.Count; i++) { var v = _vehs[i]; if (v.Heli ? v.State != 0 : v.Speed > 0.1f) continue; _cgVeh.Add(new Target { Id = i, X = (int)(v.Pos.x * 1000f), Y = (int)(v.Pos.y * 1000f) }); }
            var tr = _ng.P.TrainRect;
            // D-N175 (Lucia, Nacho): un zombi de FUERA con tropa a 25 m o menos deja la puerta y va a por ella, con tope del 30 % de la horda; la pila y los del muro no cambian.
            // Se decide una sola vez con el mundo A (sombra de NightSim) y se aplica igual a los dos mundos por SimId.
            _aggroGoal.Clear();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sinaggro") < 0 && _worldA != null && _worldA.IsCreated)
            {
                try
                {
                    if (_aggroSh == null) { _aggroSh = NightSim.ShadowFor(_ng.P, NightSimParams.FromJson(BalanceSource.Read("nightsim_params.json")), new List<NightSim.WallSegment>(), 0); _aggroSh.OutsideAggroMm = 25000; _aggroSh.OutsideAggroCapPm = 300; }
                    var outside = new List<Target>(); int aliveAll = 0;
                    using (var qa = _worldA.EntityManager.CreateEntityQuery(typeof(Zombie)))
                    {
                        var za = qa.ToComponentDataArray<Zombie>(Allocator.Temp);
                        for (int i = 0; i < za.Length; i++)
                        {
                            var z = za[i]; aliveAll++; float x = z.Pos.x, y = z.Pos.z;
                            if (!(x > Level.FortX0 && x < Level.FortX1 && y > Level.FortY0 && y < Level.FortY1)) outside.Add(new Target { Id = (int)z.SimId, X = (int)(x * 1000f), Y = (int)(y * 1000f) });
                        }
                        za.Dispose();
                    }
                    var troops = new List<Target>(_cgMen); troops.AddRange(_cgVeh);
                    foreach (var o in _aggroSh.TickOutsideAggro(outside, troops, aliveAll)) _aggroGoal[o.ZombieId] = new Vector2Int(o.TX, o.TY);
                }
                catch (System.Exception e) { Debug.LogWarning("[NOCHE] D-N175: " + e.Message); }
            }
            foreach (var wld in new[] { _worldA, _worldB })
            {
                if (wld == null || !wld.IsCreated) continue;
                var em = wld.EntityManager;
                using (var q = em.CreateEntityQuery(typeof(Zombie)))
                {
                    var zs = q.ToComponentDataArray<Zombie>(Allocator.Temp); bool dirty = false;
                    for (int i = 0; i < zs.Length; i++)
                    {
                        var z = zs[i]; float x = z.Pos.x, y = z.Pos.z;
                        bool inside = x > Level.FortX0 && x < Level.FortX1 && y > Level.FortY0 && y < Level.FortY1;
                        if (!inside)
                        {
                            if (_aggroGoal.TryGetValue((int)z.SimId, out var ag)) { z.Goal = new float2(ag.x / 1000f, ag.y / 1000f); z.HasGoal = 1; zs[i] = z; dirty = true; }
                            else if (z.HasGoal != 0) { z.HasGoal = 0; zs[i] = z; dirty = true; }
                            continue;
                        }
                        int tx, ty; int cg = ClimberTargets.Pick((int)(x * 1000f), (int)(y * 1000f), _cgMen, _cgVeh, _cgWork, NightSim.ClimberSeekMm, tr, out tx, out ty);
                        if (cg == 4 && !TrainReachableZona()) { if (wld == _worldA) _aimStation.Add((int)z.SimId); var sr = _ng.P.StationRect; tx = System.Math.Max(sr.X0, System.Math.Min(sr.X1, (int)(x * 1000f))); ty = System.Math.Max(sr.Y0, System.Math.Min(sr.Y1, (int)(y * 1000f))); }   // D-N180 (2), como NightSim.RetargetClimber
                        ClimberTargets.Rodeo((int)(x * 1000f), (int)(y * 1000f), _ng.P.StationRect, _ng.P.AttackReachMm + 500, ref tx, ref ty);   // rodeo del edificio (Nacho, 9-nov): por la franja de detrás, 1 mm fuera
                        z.Goal = new float2(tx / 1000f, ty / 1000f); z.HasGoal = 1; zs[i] = z; dirty = true;
                    }
                    if (dirty) q.CopyFromComponentDataArray(zs);
                    zs.Dispose();
                }
            }
        }
        int _entTot, _trainIn, _trainOut; readonly int[] _entSide = new int[4]; float TrainXEcs() { try { return _worldA != null && _worldA.IsCreated ? _worldA.EntityManager.GetComponentData<TrainState>(_rootA).X : -1f; } catch (System.Exception) { return -1f; } } int _climbSec; readonly HashSet<uint> _insideIds = new HashSet<uint>(); readonly System.Text.StringBuilder _fullTrace = new System.Text.StringBuilder(); float _gateT = -10f; readonly HashSet<int> _gateWalled = new HashSet<int>(); bool _capDirDone; readonly HashSet<float> _shotAtDone = new HashSet<float>(); int _diagN; float _diagT; float _endStationF = 1f, _endTrainF = 1f; int _pileNearStation, _nearTrain, _pileWall; float _wallHp, _wallMax; readonly List<Target> _allT = new List<Target>(); readonly ContactResult _contact = new ContactResult(); readonly float _contactR2 = 1200f * 1200f; readonly int _contactPpm = 1000000 * 14 / 1000 / 30;   // NightSimParams de Nacho: 1,2 m y 14 ‰ por zombi y segundo (por tick)

        int CurrentWave()
        {
            int w = -1; for (int i = 0; i < _ng.P.Waves.Length; i++) if (_ng.Tick >= _ng.WaveStartTick(i)) w = i; return w;
        }

        string ZombisSueltos() { if (_zomPos.Count == 0 || _zomPos.Count > 8) return ""; var sb = new System.Text.StringBuilder(" en"); foreach (var q in _zomPos) sb.Append($" ({q.x:0.0};{q.y:0.0})"); return sb.ToString(); }
        string TimeStr() { if (DiaZonaRed) return NetConst.Clock(NetSession.A.Tick); return $"{_ng.Hour:00}:{_ng.Minute:00}"; }      // reloj único: de día, la hora de la red

        void SpawnZombies(List<SpawnPoint> pts)
        {
            foreach (var wld in new[] { _worldA, _worldB })
            {
                if (wld == null || !wld.IsCreated) continue;
                var em = wld.EntityManager; var root = wld == _worldA ? _rootA : _rootB;
                var clock = em.GetComponentData<SimClock>(root);
                var arch = em.CreateArchetype(typeof(Zombie));
                var ents = em.CreateEntity(arch, pts.Count, Allocator.Temp);
                for (int i = 0; i < pts.Count; i++)
                    em.SetComponentData(ents[i], Zombie.Create(clock.NextSimId++, new float2(pts[i].X / 1000f, pts[i].Y / 1000f), 0f));
                ents.Dispose(); em.SetComponentData(root, clock);
            }
        }

        readonly Dictionary<uint, Vector2> _naMuertos = new Dictionary<uint, Vector2>();
        void KillZombies(List<TargetHit> hits, bool deVehiculos = false)
        {
            _naMuertos.Clear();
            // (los sonidos 2D de impacto/caida/muerte se quitan: Nuria, zombi_caida 3D en KillZombies)
            _killSet.Clear(); for (int i = 0; i < hits.Count; i++) _killSet.Add((uint)hits[i].TargetId);
            foreach (var wld in new[] { _worldA, _worldB })
            {
                if (wld == null || !wld.IsCreated) continue;
                var em = wld.EntityManager;
                using (var q = em.CreateEntityQuery(typeof(Zombie)))
                {
                    var zs = q.ToComponentDataArray<Zombie>(Allocator.Temp); var es = q.ToEntityArray(Allocator.Temp);
                    for (int i = 0; i < zs.Length; i++)
                        if (_killSet.Contains(zs[i].SimId)) { em.DestroyEntity(es[i]); if (wld == _worldA) { _nightKills++; _cercoPend++; _naMuertos[zs[i].SimId] = new Vector2(zs[i].Pos.x, zs[i].Pos.z); BloodAt(zs[i].Pos); if (UnityEngine.Random.Range(0, 10) == 0) Sfx.PlayAt("zombi_caida", new Vector3(zs[i].Pos.x, 0.3f, zs[i].Pos.z), 0.3f, 0.25f); } }
                    zs.Dispose(); es.Dispose();
                }
            }
            for (int i = 0; i < hits.Count; i++) _uw.NoteKill(hits[i].Platoon);
            NaAtribuir(hits, _naMuertos, deVehiculos);                                   // [NOCHE-ARMAS]: a quién se le cuenta cada abatido y si estaba en la pila
        }

        void NightEnded()
        {
            if (_ng.State == NightState.Derrota) Sfx.Play("estacion_caida", Vector3.zero, 0.9f, 1f);
            if (_ng.State == NightState.Derrota) ClearNightSession(false); else if (NightRun.Session != null) NightRun.Session.Reported = true;   // D-N170: tras ganar la sesion SE QUEDA (el mundo sigue; salir y entrar no vuelve a sacar «Sagunto resiste» ni otra horda); solo la derrota la borra
            try { string d0 = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Captures")); System.IO.Directory.CreateDirectory(d0); System.IO.File.WriteAllText(System.IO.Path.Combine(d0, "traza-noche" + TrazaSufijo + ".csv"), _fullTrace.ToString()); if (_rec != null && !_rec.Done) System.IO.File.WriteAllText(System.IO.Path.Combine(d0, "traza-zona" + TrazaSufijo + ".csv"), _rec.Trace.ToCsv()); } catch (System.Exception e0) { Debug.LogWarning("[TBD] traza noche: " + e0.Message); }
            bool win = _ng.State == NightState.Victoria; var a = _uw;
            int alive = 0, total = 0; foreach (var pl in a.Platoons) { alive += pl.AliveCount(); total += pl.Men.Count; }
            _nightLost = total - alive;
            float left = a.StockShots / (float)WeaponCatalog.ShotsPerU;
            _endStationF = _ng.StationHp / (float)Mathf.Max(1, _ng.StationHpMax); _endTrainF = _ng.TrainHp / (float)Mathf.Max(1, _ng.TrainHpMax);   // el parte congela estas cifras: despues del amanecer la horda sigue y no debe vaciar la barra (Sara)
            Debug.Log($"[NOCHE] resultado={(win ? "victoria" : "derrota")} causa={(win ? "-" : _ng.Cause == DefeatCause.Estacion ? "estacion" : _ng.Cause == DefeatCause.Tren ? "tren" : _ng.Cause == DefeatCause.Ambas ? "estacion+tren" : (_wallHp <= 0 ? "muro" : "otra"))} abatidos={_nightKills} bajas={_nightLost} municion_gastada_u={_nightShotsSpent / (float)WeaponCatalog.ShotsPerU:0.0} municion_restante_u={left:0.0} combustible_gastado_u=0 pila_max={_pilaMuroMax} pila_estacion_max={_pilaMaxVista} recargas={_resupplies} desync={_desync} desync_red={(NetSession.Desync ? "True@" + NetSession.DesyncTick : "False")} hora_fin={TimeStr()}");
            NaLog();                                                                     // [NOCHE-ARMAS] (Sara, Hugo): pelotones, vehículos, almacén y dotación
            { string est = string.IsNullOrEmpty(_estacionZona) ? "Sagunto" : _estacionZona;   // partida de 30 min (Lucía): el cartel nombra la estación de la zona (Nules en la noche 2)
              _endReason = win ? (_ng.WonByClear ? $"{TimeStr()}. {est} resiste.\nLa horda ha caído." : $"06:00. {est} resiste.\nAmanece.") : _ng.DefeatHeadline(est);   // Lucía b4c2fe5: sin salto al amanecer
              if (win) NightRun.NocheGanada = true;
              _enPie = win ? null : EstacionesEnPie();
              if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-seguir-auto") >= 0) Invoke(nameof(SeguirAuto), 2f);   // QA: pulsa «Seguir» sola
              if (!win && _enPie.Count > 0) _endReason = $"<color=#982420>{est} ha caído.</color>\n{Lista(_enPie)} {(_enPie.Count > 1 ? "siguen" : "sigue")} en pie.";   // S2 de la qa50 (Lucía, 7705049): no acaba la partida
              if (est == "Nules") _endReason += "\nFin de la partida de prueba. Puedes seguir en el mundo."; }   // D-N136/D-N146
        }
        bool _endDismissed, _reportClosed; bool _seguirTrasNoche;
        bool _esperandoNoche; Vector2Int? _puestoEnviado;
        /// <summary>¿Es de día en la red (06:00–21:59) y la zona no está en el tutorial? De día la noche de la zona no avanza (parche hasta el reloj único de Nacho).</summary>
        bool DiaEnLaRed() { var w = NetSession.A; if (w == null || TutorialHud.Activo || !NightRun.Active || _zoneNode < 0 || !RelojUnicoActivo) return false; long h = w.Tick % NetConst.DayTicks; return h >= NetConst.Hours(6) && h < NetConst.Hours(22); }      // Marcos (qa52 119): con reloj propio (-zona) la noche no se congela al llegar las 06:00 de la red
        /// <summary>Las 22:00 de la red con la zona abierta de día: la noche arranca con lo que hay ahora (munición del almacén, tren en la estación y su sitio).</summary>
        void IniciaNocheZona()
        {
            _esperandoNoche = false; var w = NetSession.A; if (w == null) return;
            int reservaU = w.Stock[_zoneNode, (int)Res.Municion]; int alm = System.Math.Min(47, System.Math.Max(0, reservaU)); _uw.StockShots = alm * WeaponCatalog.ShotsPerU;
            string tren = null; bool puertas = false; foreach (var sv in w.Services) if (sv.AtStop && sv.Stops.Count > 0 && (sv.GateNode == _zoneNode || sv.Stops[sv.StopIndex].Node == _zoneNode)) { tren = sv.Train.Name; puertas = sv.GateNode == _zoneNode; break; }
            if (tren == null && _ng.P.TrainNotDefeat) { _ng.TrainHp = 0; _trenPerdidoAvisado = true; _nocheSinTren = true; }
            if (_ng.P.TrainNotDefeat && tren != null && !puertas) { var pp = _ng.P; pp.TrainRect = NightParams.Default().TrainRect; _ng.P = pp; SolidoDelTren(); }      // en el andén, dentro (Nules A)
            if (_puestoEnviado.HasValue) foreach (var id in _escolta) { if (_bot != null) _bot.Busy.Remove(id); _uw.Move(id, _puestoEnviado.Value.x, _puestoEnviado.Value.y, 32768); }   // la escolta deja el tren y va al puesto de la noche (Hugo/Nacho: (71,5; 70,5))
            Debug.Log($"[NOCHE] 22:00 en la red: empieza la noche de {_estacionZona} (almacén min(47, {reservaU}) = {alm} u, tren {(tren ?? "ninguno")}{(puertas ? " a las puertas" : "")})");
        }
        bool TrenALasPuertas() { var w = NetSession.A; if (w == null || _zoneNode < 0) return true; foreach (var sv in w.Services) if (sv.AtStop && sv.Stops.Count > 0 && (sv.GateNode == _zoneNode || sv.Stops[sv.StopIndex].Node == _zoneNode)) return sv.GateNode == _zoneNode; return true; }   // sin tren: da igual (la noche empieza sin tren)
        void SeguirAuto() { if (_ng == null) return; if (_ng.State == NightState.Victoria) { Debug.Log("[NOCHE] auto: Seguir"); NextNight(); } else if (_enPie != null && _enPie.Count > 0) { Debug.Log("[NOCHE] auto: Seguir tras caer"); SeguirTrasCaer(); } }
        List<string> _enPie;
        readonly List<int> _escolta = new List<int>(); readonly HashSet<int> _embVistos = new HashSet<int>(); bool _nocheSinTren;      // S2 qa50: sin tren en la estación no hay aviso, frente ni barra de tren
        /// <summary>Estaciones propias que siguen en pie, sin la de esta zona (Sagunto, las fundadas y la cercada; las perdidas no).</summary>
        List<string> EstacionesEnPie()
        {
            var r = new List<string>(); var w = NetSession.A; if (w == null) return r;
            for (int n = 0; n < w.Graph.Nodes.Count; n++) if (n != _zoneNode && !w.NodeLost[n] && (w.Stations.Founded[n] || n == w.Embarque.HomeNode)) r.Add(w.Graph.Nodes[n].Name);
            return r;
        }
        static string Lista(List<string> l) { return l.Count <= 1 ? (l.Count == 1 ? l[0] : "") : string.Join(", ", l.GetRange(0, l.Count - 1)) + " y " + l[l.Count - 1]; }
        /// <summary>«Seguir» tras perder la estación de la zona: la marca perdida en la red (LoseNode) y vuelve al mapa.</summary>
        void SeguirTrasCaer()
        {
            _endDismissed = true; Debug.Log($"[NOCHE] {(_estacionZona ?? "?")} ha caído; se sigue en el mapa ({Lista(_enPie)} en pie)");
            PerderNodoEnRed(_zoneNode); LeaveToMap();
        }
        void PerderNodoEnRed(int nodo) { if (nodo >= 0) NetSession.Push(NetCmdType.LoseNode, nodo, 0); }   // Nacho (268427f6): misma caída que fuera de pantalla, por la cola (A y B en el mismo tick)

        /// <summary>«Seguir» (D-N91): abre la noche siguiente con las mismas oleadas a +10 % y reinicia los contadores del parte; el mundo no se detuvo.</summary>
        void NextNight()
        {
            _reportClosed = true; _seguirTrasNoche = true; if (!(_ng != null && _ng.P.NetClock)) ClearNightSession(); LeaveToMap(); return;   // reloj único: la noche ganada vuelve a la red y la siguiente empieza a las 22:00 (BeginNextNight)   // Lucía b4c2fe5: sin salto al amanecer (rompe el tiempo real)   // Eugenio (I-Noche110): sin el cartel viejo arriba del mapa   // Lucía (3f1764c): «Seguir» lleva al mapa y la red salta a las 06:00 del día siguiente
            var np = _ng.P; np.ScalePermil = np.ScalePermil * 11 / 10;
            if (_ng.BeginNextNight(np)) { _reportClosed = false; _lastWave = -1; _nightKills = 0; _nightShotsSpent = 0; _nightLost = 0; _bannerText = $"{TimeStr()}. Noche {_ng.NightIndex + 1}.\nAguanta hasta el amanecer."; _bannerUntil = Time.unscaledTime + 4f; }
            else _reportClosed = true;
        }

        // ---------------------------------------------------------------- sangre (decal efímero)
        readonly Queue<(GameObject go, float t)> _blood = new Queue<(GameObject, float)>();
        Material _bloodMat;
        void BloodAt(float3 pos)
        {
            if (_blood.Count > 220) { var o = _blood.Dequeue(); if (o.go != null) Destroy(o.go); }
            if (_bloodMat == null) { _bloodMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); _bloodMat.SetColor("_BaseColor", new Color32(0x4c, 0x10, 0x08, 255)); }
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(g.GetComponent<Collider>());
            g.transform.position = new Vector3(pos.x, 0.13f, pos.z); g.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.value * 360f, 0f); g.transform.localScale = Vector3.one * (0.5f + UnityEngine.Random.value * 0.5f);
            g.GetComponent<MeshRenderer>().sharedMaterial = _bloodMat; g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _blood.Enqueue((g, Time.unscaledTime + 20f));
        }

        // ---------------------------------------------------------------- trazadoras
        readonly List<(LineRenderer lr, float until)> _tracers = new List<(LineRenderer, float)>(); Material _trMat;
        void NightShotFx(FireEvent s)
        {
            var pl = _uw.Find(s.Platoon); if (pl == null) return; Infantryman man = null; foreach (var m in pl.Men) if (m.Id == s.Man) { man = m; break; }
            if (man == null) return;
            if (_trMat == null) { _trMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); _trMat.SetColor("_BaseColor", new Color32(0xf0, 0xdc, 0x9c, 255)); }
            if (s.Splash) { Sfx.PlayDistAt("granada_40", new Vector3(man.X / 1000f, 1.2f, man.Y / 1000f), 1f, 0.25f); Sfx.PlayAt("explosion_granada", new Vector3(s.AimX / 1000f, 0.5f, s.AimY / 1000f), 0.7f, 0.3f); } else if (s.Kind == InfKind.Ametrallador) Sfx.PlayDistAt("ametralladora", new Vector3(man.X / 1000f, 1.2f, man.Y / 1000f), 1f, 0.2f); else if (s.Kind == InfKind.Tirador) { Sfx.PlayDistAt("tirador", new Vector3(man.X / 1000f, 1.2f, man.Y / 1000f), 1f, 0.2f); Sfx.PlayAt("cola_urbana", new Vector3(man.X / 1000f, 1.2f, man.Y / 1000f), 0.3f, 0.4f); } else Sfx.PlayDistAt("fusil_rafaga", new Vector3(man.X / 1000f, 1.2f, man.Y / 1000f), 1f, 0.12f);
            var from = new Vector3(man.X / 1000f, 1.2f, man.Y / 1000f); var to = new Vector3(s.AimX / 1000f, 0.8f, s.AimY / 1000f);
            // trazadora que viaja (no un rayo): 1 de cada 3 balas en fusileros, 1 de cada 4 en ametralladoras, todas en tiradores y granadas (Lucia §4.1)
            bool tracer = s.Splash || s.Kind == InfKind.Tirador || UnityEngine.Random.value < (s.Kind == InfKind.Ametrallador ? 0.25f : 0.34f);
            // Teo (8-nov): el disparo sale de Muzzle_n del modelo (hueso hijo de «brazos», sigue al arma en cada clip). Se usa su POSICIÓN y la
            // dirección hacia el blanco, no su +Z: el fusil del modelo apunta unos 35° a la derecha porque va cruzado entre las manos.
            var boca = _views.TryGetValue(man.Id, out var vista) && vista != null ? VxBocaSoldado(vista.Go, s.Splash) : null;
            if (boca != null)
            {
                var w = s.Splash ? WeaponId.Lanzagranadas : s.Kind == InfKind.Ametrallador ? WeaponId.MgLigera : s.Kind == InfKind.Tirador ? WeaponId.FusilPrecision : WeaponId.Fusil;
                VfxShot(w, boca.position, to, true, tracer, false);                                             // sin impacto propio: la granada y la sangre ya llegan por la simulación
                return;
            }
            if (tracer) SpawnTracer(from, to, s.Splash ? 60f : 200f, s.Splash ? 0.16f : 0.07f, s.Splash ? 3f : 5f);   // modelo sin Muzzle: lo de antes
            SpawnFlash(from + (to - from).normalized * 0.6f, s.Kind == InfKind.Ametrallador ? 0.5f : 0.38f);
        }

        // ---------------------------------------------------------------- soldados (vista)
        sealed class SoldierView
        {
            public GameObject Go; public PlayableGraph Graph; public AnimationMixerPlayable Mix; public AnimationClipPlayable[] Clips = new AnimationClipPlayable[4]; public float[] Len = new float[4];
            public int State = 0; public float T; public float[] W = { 1, 0, 0, 0 }; public bool Dead; public float DeadT;
        }
        readonly Dictionary<int, SoldierView> _views = new Dictionary<int, SoldierView>();
        Material _soldMat; Texture2D _soldPal; static readonly string[] ClipNames = { "reposo", "andar", "disparar", "morir" };

        SoldierView MakeView(Infantryman m, bool leader)
        {
            int v; string weapon;
            switch (m.Kind)                                                          // SoldadoArmado de Teo (7-nov): el arma va dentro de la malla, asi no queda tumbada: v0 G36E, v1 G36E+AG36, v2 G36E, v3 MG4E, v4 G28, v5 G36E
            {
                case InfKind.Ametrallador: v = 3; weapon = "MG4E"; break;
                case InfKind.Zapador: v = 2; weapon = "G36E"; break;
                case InfKind.Servidor: v = 2; weapon = "G36E"; break;
                case InfKind.Granadero: v = 1; weapon = "G36E_AG36"; break;
                case InfKind.Tirador: v = 4; weapon = "G28"; break;
                default: v = (m.Id & 1) == 0 ? 0 : 2; weapon = "G36E"; break;
            }
            if (leader) { v = 5; weapon = "G36E"; }                                  // jefe de pelotón: v5 + G36E
            var prefab = Resources.Load<GameObject>("Models/Infanteria/SM_SoldadoArmado_v" + v + "_" + weapon + "_LOD1"); bool armado = prefab != null; if (!armado) prefab = Resources.Load<GameObject>("Models/Infanteria/SM_Soldado_v" + v + "_LOD1"); if (prefab == null) return null;
            if (_soldMat == null) { _soldPal = Resources.Load<Texture2D>("Models/Infanteria/T_Paleta32"); _soldMat = Mat("ffffff"); _soldMat.SetTexture("_BaseMap", _soldPal); _soldMat.enableInstancing = true; }
            var go = Instantiate(prefab); go.name = "Soldado " + m.Id; go.transform.localScale = Vector3.one * RenderAssets.FigureScale;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = _soldMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            Transform brazos = FindBone(go.transform, "brazos"), pecho = FindBone(go.transform, "pecho");
            var wp = Resources.Load<GameObject>("Models/Infanteria/SM_Arma_" + weapon + "_LOD1");
            if (!armado && wp != null && brazos != null) { var w = Instantiate(wp, go.transform); w.transform.localPosition = Vector3.zero; w.transform.localRotation = Quaternion.identity; w.transform.SetParent(brazos, true); foreach (var r in w.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _soldMat; }
            if (!armado && m.Kind == InfKind.Fusilero && pecho != null) { var c = Resources.Load<GameObject>("Models/Infanteria/SM_Arma_C90_LOD1"); if (c != null) { var w = Instantiate(c, go.transform); w.transform.localPosition = Vector3.zero; w.transform.localRotation = Quaternion.identity; w.transform.SetParent(pecho, true); foreach (var r in w.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _soldMat; } }
            var sv = new SoldierView { Go = go };
            var an = go.GetComponent<Animator>(); if (an == null) an = go.AddComponent<Animator>();
            an.runtimeAnimatorController = null; an.applyRootMotion = false;
            var clips = Resources.LoadAll<AnimationClip>(armado ? "Models/Infanteria/SM_SoldadoArmado_v" + v + "_" + weapon + "_LOD1" : "Models/Infanteria/SM_Soldado_v" + v + "_LOD1");
            sv.Graph = PlayableGraph.Create("Soldado " + m.Id); sv.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(sv.Graph, "anim", an); sv.Mix = AnimationMixerPlayable.Create(sv.Graph, 4); output.SetSourcePlayable(sv.Mix);
            for (int i = 0; i < 4; i++)
            {
                AnimationClip clip = null; foreach (var c in clips) if (c.name.EndsWith("|" + ClipNames[i])) clip = c;
                if (clip == null) { sv.Len[i] = 1f; sv.Clips[i] = AnimationClipPlayable.Create(sv.Graph, new AnimationClip()); } else { sv.Len[i] = clip.length; sv.Clips[i] = AnimationClipPlayable.Create(sv.Graph, clip); }
                sv.Clips[i].SetApplyFootIK(false); sv.Clips[i].SetSpeed(0); sv.Graph.Connect(sv.Clips[i], 0, sv.Mix, i); sv.Mix.SetInputWeight(i, i == 0 ? 1f : 0f);
            }
            sv.T = (m.Id * 0.137f) % 1f; sv.Graph.Play();
            return sv;
        }

        static Transform FindBone(Transform t, string name)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c; return null;
        }

        /// <summary>Altura del suelo bajo el soldado para la vista: la cota del pelotón (EyeMm − 1,6 m). La azotea del ala oeste (4,75 m) solo encima de su planta (X 83–92, Z 71,8–80,8); la banqueta (0,4–0,7 m), siempre.</summary>
        static float CotaVista(Platoon pl, int x, int y) { float c = (pl.EyeMm - UnitsWorld.EyeInfantryMm) / 1000f; if (c <= 0f) return 0f; if (c < 1.5f) return c; return x >= 83000 && x <= 92000 && y >= 71800 && y <= 80800 ? c : 0f; }
        void UpdateNightSoldiers()
        {
            float dt = Time.unscaledDeltaTime; foreach (var pl in _uw.Platoons)
                foreach (var m in pl.Men)
                {
                    if (!_views.TryGetValue(m.Id, out var sv)) { sv = MakeView(m, pl.Men.Count > 0 && pl.Men[0] == m); _views[m.Id] = sv; if (sv == null) continue; }
                    if (sv == null) continue;
                    var go = sv.Go; var tgt = new Vector3(m.X / 1000f, CotaVista(pl, m.X, m.Y), m.Y / 1000f);   // S2 (Eugenio): los de la azotea se dibujan en el tejado
                    go.transform.position = Vector3.Lerp(go.transform.position, tgt, 1f - Mathf.Exp(-14f * dt));
                    float yaw = 90f - m.Heading * 360f / 65536f; go.transform.rotation = Quaternion.Slerp(go.transform.rotation, Quaternion.Euler(0f, yaw, 0f), 1f - Mathf.Exp(-12f * dt));
                    int st = !m.Alive ? 3 : m.Firing ? 2 : m.Moving ? 1 : 0;
                    if (st == 3 && !sv.Dead) { sv.Dead = true; sv.T = 0f; }
                    sv.State = st;
                    for (int i = 0; i < 4; i++) { float tw = i == st ? 1f : 0f; sv.W[i] = Mathf.MoveTowards(sv.W[i], tw, dt / 0.15f); sv.Mix.SetInputWeight(i, sv.W[i]); }
                    sv.T += dt;
                    for (int i = 0; i < 4; i++) { float tt = i == 3 ? Mathf.Min(sv.T, sv.Len[3] - 0.01f) : sv.T % sv.Len[i]; sv.Clips[i].SetTime(tt); }
                    sv.Graph.Evaluate(0f);
                }
        }

        // ---------------------------------------------------------------- entrada y órdenes
        void NightUpdate()
        {
            ZonaPausada = _paused;                                                     // la regla única del silencio está en NetSession.Update (Nuria, 9-nov)
            ColorCases();
            TramosUpdate();
            OrdersUpdate();
            UpdateFx();
            UpdateVfxArmas();
            GradeSweepCheck();
            ListObjsCheck();
            InputTestCheck();
            if (!_capDirDone && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-capdirecta") >= 0 && Time.realtimeSinceStartup - _nightT0 >= 30f)   // QA (Bruno): MISMO fotograma, captura de siempre vs. captura directa a resolucion completa sin Blit
            {
                _capDirDone = true; string dd = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Captures")); System.IO.Directory.CreateDirectory(dd);
                StartCoroutine(CaptureRoutine(System.IO.Path.Combine(dd, "capdirecta_1_siempre.png")));
            }
            { var cl = System.Environment.GetCommandLineArgs(); for (int li = 0; li + 1 < cl.Length; li++) if (cl[li] == "-leaveat" && float.TryParse(cl[li + 1], out var lt) && Time.realtimeSinceStartup - _nightT0 >= lt && !_leaving && !NightRun.LeftOnce && _nightInit && _ng != null && _ng.State == NightState.Jugando) { NightRun.LeftOnce = true; Debug.Log("[NOCHE] prueba: -leaveat, salgo al mapa tick " + _ng.Tick); LeaveToMap(); } }   // QA: sale al mapa a los N s reales
            if (!_nightOn) return;
            if (Keyboard.current == null && Time.realtimeSinceStartup - _fpsLogT > 20f) { _fpsLogT = Time.realtimeSinceStartup; Debug.Log($"[NOCHE-FPS] hora={TimeStr()} estado={_ng.State} zombis={_zomPos.Count} abatidos={_nightKills} tren_hp={_ng.TrainHp} estacion_hp={_ng.StationHp} (batch){ZombisSueltos()}"); }      // corridas por lotes: sin teclado no llegaba a la línea de abajo
            var kb = Keyboard.current; var mouse = Mouse.current; if (kb == null || mouse == null) return;
            if (kb.f12Key.wasPressedThisFrame) _debugHud = !_debugHud;   // F2 pasa a ser la formacion cuña (F1-F4)
            NightDemoFlags();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nightshots") >= 0 && _ng.State != NightState.Jugando)       // captura de la pantalla final
            { if (_endAt < 0f) _endAt = Time.realtimeSinceStartup; if (!_endShot && Time.realtimeSinceStartup - _endAt > 2f) { _endShot = true; SaveScreenshot(); } }
            if (Time.realtimeSinceStartup - _fpsLogT > 20f) { _fpsLogT = Time.realtimeSinceStartup; Debug.Log($"[NOCHE-FPS] hora={TimeStr()} fps={_fps:0} zombis={_total} abatidos={_nightKills} bajas={_nightLost} pila={_pileWall} muro_permil={_ng.WallPermil} desync={_desync}"); }
            { var cs = System.Environment.GetCommandLineArgs(); for (int ci = 0; ci + 1 < cs.Length; ci++) if (cs[ci] == "-shotsat") { float tt2 = Time.realtimeSinceStartup - _nightT0; foreach (var sp in cs[ci + 1].Split(',')) if (float.TryParse(sp, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var at2) && tt2 >= at2 && _shotAtDone.Add(at2)) SaveScreenshot(); } }   // QA: -shotsat 236,245,255 = capturas a esos segundos reales (Sara, la O2 del tren)
            { var ca = System.Environment.GetCommandLineArgs(); if (System.Array.IndexOf(ca, "-nightshots") >= 0) { float tt = Time.realtimeSinceStartup - _nightT0; foreach (float at in new[] { 12f, 40f, 90f, 150f }) if (tt >= at && _shotsTaken < Array.IndexOf(new[] { 12f, 40f, 90f, 150f }, at) + 1) { _shotsTaken++; SaveScreenshot(); } } }
            if (_ng.State == NightState.Derrota && !_endDismissed) { UpdateNightSoldiers(); return; }
            Vector2 mp = mouse.position.ReadValue();
            // selección: clic o arrastre de rectángulo (en pantalla) sobre los pelotones
            if (mouse.leftButton.wasPressedThisFrame && !OverNightUi(mp)) { _selDrag = true; _selStart = mp; }
            if (mouse.leftButton.wasReleasedThisFrame && _selDrag)
            {
                _selDrag = false;
                if (_c90Aim)                                                          // C90: clic en el punto de impacto (D-N137); se pierde si el punto esta fuera de alcance (10-120 m) o no quedan tiros
                {
                    _c90Aim = false;
                    if (TryGround(mp, out var gc)) { int okc = 0; foreach (var id in _sel) if (_uw.OrderC90(id, (int)(gc.x * 1000f), (int)(gc.z * 1000f))) okc++; Debug.Log($"[NOCHE] C90 sobre ({gc.x:0};{gc.z:0}): {okc} pelotones lo disparan"); _c90Msg = okc > 0 ? "C90 disparado" : "C90 fuera de alcance o sin tiros"; _c90MsgUntil = Time.unscaledTime + 2f; }
                }
                else
                if (_attackNext) { IssueAttackMove(mp); _attackNext = false; }
                else
                {
                    var vh = VehAt(mp); if (vh != null && (mp - _selStart).magnitude < 6f) { _selVeh = vh; _sel.Clear(); goto selDone; }
                    var r = Rect.MinMaxRect(Mathf.Min(_selStart.x, mp.x), Mathf.Min(_selStart.y, mp.y), Mathf.Max(_selStart.x, mp.x), Mathf.Max(_selStart.y, mp.y));
                    bool box = r.width > 6f && r.height > 6f; var found = new List<int>();
                    foreach (var pl in _uw.Platoons)                          // OJO: _worldCam pinta en una RT mas pequena, asi que se proyecta por viewport (WorldToScreenPoint devolvia pixeles de la RT y la seleccion fallaba, Eugenio)
                    {
                        if (pl.AliveCount() == 0) continue; bool hit = false;
                        foreach (var m in pl.Men)
                        {
                            if (!m.Alive) continue; var sp = ScreenOf(new Vector3(m.X / 1000f, 1f, m.Y / 1000f)); if (sp.z < 0f) continue; var q = new Vector2(sp.x, sp.y);
                            if (box ? r.Contains(q) : (q - mp).magnitude < Screen.height * 0.035f) { hit = true; break; }
                        }
                        if (hit) found.Add(pl.Id);
                    }
                    if (found.Count > 0) TutorialHud.Senal(TutorialGuion.Senales.PelotonSeleccionado);
                    if (found.Count > 0 || box) { _selVeh = null; if (!(kb.leftShiftKey.isPressed)) _sel.Clear(); foreach (var id in found) if (!_sel.Contains(id)) _sel.Add(id); }
                    selDone:;
                }
            }
            // clic derecho sin arrastre = mover (con arrastre, gira la cámara como siempre)
            if (mouse.rightButton.wasPressedThisFrame) { _rmbDown = true; _rmbStart = mp; _rmbDragging = false; _rmbW0Ok = TryGround(mp, out _rmbW0); }
            if (_rmbDown && mouse.rightButton.isPressed && _rmbW0Ok && _selVeh == null && _sel.Count > 0 && (mp - _rmbStart).magnitude >= 8f && TryGround(mp, out var gcur)) { _rmbDragging = true; _rmbW1 = gcur; }   // clic derecho y arrastrar = dibujar el frente
            if (mouse.rightButton.wasReleasedThisFrame && _rmbDown)
            {
                _rmbDown = false; bool dragged = _rmbDragging; _rmbDragging = false;
                if (dragged && _rmbW0Ok && TryGround(mp, out var g1)) IssueFormationOrder(_rmbW0, g1, _attackNext);
                else if ((mp - _rmbStart).magnitude < 6f && !OverNightUi(mp)) { if (_selVeh != null) { if (TryGround(mp, out var gv)) VehicleOrder(_selVeh, gv); } else IssueMove(mp, false); Sfx.Play("ui_orden", Vector3.zero, 0.6f, 0.05f); }
            }
            // teclas de la noche (D-N137..144; WASD mueve la camara): 1-5 pelotones, 6-8 vehiculos (doble pulsacion = centrar), G alterna el fuego de los vehiculos, T recarga, Esc pausa
            NightSelectKeys(kb);
            if (kb.cKey.wasPressedThisFrame && _sel.Count > 0 && _selVeh == null) { _c90Aim = true; _c90Msg = "C90: elige el punto en el suelo (clic); Esc cancela"; _c90MsgUntil = Time.unscaledTime + 4f; }
            if (_c90Aim && kb.escapeKey.wasPressedThisFrame) { _c90Aim = false; _c90MsgUntil = 0f; }
            if (kb.gKey.wasPressedThisFrame) { TutorialHud.Senal(TutorialGuion.Senales.FuegoVehiculos); _vehFire = !_vehFire; foreach (var vv in _vehs) vv.Fire = _vehFire; Debug.Log("[NOCHE] fuego de los vehiculos: " + (_vehFire ? "libre" : "alto el fuego")); }
            if (kb.tKey.wasPressedThisFrame) { if (_selVeh != null) StartVehicleResupply(_selVeh); else { NaResupAntes(); foreach (var id in _sel) _uw.Resupply(id); NaResupDespues(); } }
            if (kb.rKey.wasPressedThisFrame && kb.leftShiftKey.isPressed) { NaResupAntes(); foreach (var id in _sel) _uw.Resupply(id); NaResupDespues(); }
            if (kb.escapeKey.wasPressedThisFrame && !_c90Aim && _ng != null && (_ng.State == NightState.Jugando || TutorialHud.Activo)) { _paused = !_paused; if (!_paused) TutorialHud.Senal(TutorialGuion.Senales.PausaCerrada); }      // pausa (D-N137); 1.7: cerrarla con Esc también cuenta
            { float zf = Mathf.Clamp01(_total / 3000f); }
            NightAudio();
            UpdateNightSoldiers(); UpdateVehicleViews();
            while (_blood.Count > 0 && Time.unscaledTime > _blood.Peek().t) { var o = _blood.Dequeue(); if (o.go != null) Destroy(o.go); }
            // la luz sigue a la hora: de noche hasta las 05:30, luego el alba
            int minutes = _ng.GameSeconds / 60; int want = minutes < 7 * 60 + 30 ? 2 : (minutes < 8 * 60 ? 1 : 0);
            if (DiaZonaRed) { long hm = NetSession.A.Tick % NetConst.DayTicks * 60 / NetConst.HourTicks; want = hm < 6 * 60 || hm >= 21 * 60 ? 2 : (hm < 7 * 60 || hm >= 20 * 60 ? 1 : 0); }      // reloj único: de día, la luz de la hora de la red (Marcos, qa52)
            if (want != _gradeIndex && !_nightGradeLock) ApplyGrade(want);
        }
        bool _nightGradeLock;
        float _endAt = -1f; bool _endShot; int _demoStep, _botStep;
        /// <summary>Banderas de prueba para QA (Sara): -nightlose tumba la estacion a los 15 s; -vehdemo manda un VAMTAC contra el muro oeste y despega el NH90; -nightshots saca capturas.</summary>
        void NightDemoFlags()
        {
            var ca = System.Environment.GetCommandLineArgs(); float tt = Time.realtimeSinceStartup - _nightT0;
            if (false && System.Array.IndexOf(ca, "-nightbot") >= 0 && tt > 3f && _botStep == 0)
            {
                _botStep = 1; int[] ys = { 56000, 64000, 60000, 68000, 72000 }; int i = 0; int x = (int)(Level.FortX0 * 1000f) + 7000;
                foreach (var pl in _uw.Platoons) { _uw.AttackMove(pl.Id, x, ys[i++ % ys.Length]); }
                Debug.Log("[NOCHE] prueba: -nightbot, los pelotones avanzan al muro oeste (ataque-movimiento) y recargan al 30 %");
            }
            if (false && System.Array.IndexOf(ca, "-nightbot") >= 0 && tt > 12f && _botStep == 1)
            {
                _botStep = 2; foreach (var pl in _uw.Platoons) if (_pname.TryGetValue(pl.Id, out var nm) && nm.StartsWith("Ametra")) _uw.DeployAt(pl.Id, 32768);
            }
            if (System.Array.IndexOf(ca, "-nightwin") >= 0 && _ng.State == NightState.Jugando) { _ng.StationHp = _ng.StationHpMax; _ng.TrainHp = _ng.TrainHpMax; }   // prueba: estacion y tren inmortales, para ver el parte de victoria y «Seguir»
            if (System.Array.IndexOf(ca, "-nightdense") >= 0 && tt > 20f && _demoStep == 0)
            {
                _demoStep = 1; var pts = new List<SpawnPoint>(); var rr = new System.Random(5);
                for (int i = 0; i < 10000; i++) pts.Add(new SpawnPoint { Wave = 0, Entry = 0, X = 2000 + rr.Next(0, 8000), Y = 56000 + rr.Next(0, 17000) });
                SpawnZombies(pts); Debug.Log("[NOCHE] prueba: -nightdense, 10.000 zombis por O1");
            }
            if (System.Array.IndexOf(ca, "-nightlose") >= 0 && tt > 15f && _demoStep == 0 && _ng.State == NightState.Jugando) { _demoStep = 1; _ng.StationHp = 0; Debug.Log("[NOCHE] prueba: -nightlose, la estacion cae"); }
            if (System.Array.IndexOf(ca, "-vehdemo") >= 0)
            {
                if (tt > 8f && _demoStep == 0) { _demoStep = 1; foreach (var v in _vehs) { if (v.Name == "VAMTAC 1") VehicleOrder(v, new Vector3(55f, 0f, 53f)); if (v.Name == "NH90") VehicleOrder(v, new Vector3(120f, 0f, 44f)); } Debug.Log("[NOCHE] prueba: VAMTAC 1 hacia el muro oeste, NH90 despega"); }
                if (Time.frameCount % 60 == 0) foreach (var v in _vehs) if (v.Name == "VAMTAC 1" || v.Name == "NH90") Debug.Log($"[NOCHE] veh {v.Name} pos=({v.Pos.x:0.0};{v.Pos.y:0.0}) alt={v.Alt:0.0} vel={v.Speed:0.0}");
                foreach (var t in new[] { 12f, 20f, 30f }) if (tt > t && _shotsDemo < (t == 12f ? 1 : t == 20f ? 2 : 3)) { _shotsDemo++; SaveScreenshot(); }
            }
        }
        int _shotsDemo;

        /// <summary>Posicion en pantalla (pixeles reales) de un punto del mundo: por viewport, porque la camara del mundo renderiza a una RT de menor resolucion.</summary>
        Vector3 ScreenOf(Vector3 w) { var v = _worldCam.WorldToViewportPoint(w); return new Vector3(v.x * Screen.width, v.y * Screen.height, v.z); }

        bool TryGround(Vector2 mp, out Vector3 p)
        {
            var ray = _worldCam.ViewportPointToRay(new Vector3(mp.x / Screen.width, mp.y / Screen.height, 0f)); p = Vector3.zero;
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float t)) return false; p = ray.GetPoint(t); return true;
        }

        void IssueMove(Vector2 mp, bool attack)
        {
            if (!TryGround(mp, out var g)) return; int i = 0;
            foreach (var id in _sel) { int ox = (i % 3 - 1) * 2500, oy = (i / 3) * 2500; if (attack) _uw.AttackMove(id, (int)(g.x * 1000f) + ox, (int)(g.z * 1000f) + oy); else _uw.Move(id, (int)(g.x * 1000f) + ox, (int)(g.z * 1000f) + oy); i++; }
            _moveMark = g; _moveMarkUntil = Time.unscaledTime + 1.2f; TutorialHud.Senal(TutorialGuion.Senales.OrdenMover);
        }
        void IssueAttackMove(Vector2 mp) { IssueMove(mp, true); }
        Vector3 _moveMark; float _moveMarkUntil;

        bool OverNightUi(Vector2 mp) { float y = Screen.height - mp.y; return y < Screen.height * 0.075f || y > Screen.height * 0.80f; }

        // ---------------------------------------------------------------- interfaz
        GUIStyle _nStyle, _nBig; Texture2D _nPx;
        void NightGUI()
        {
            if (_ng == null) return;
            NightOverlay();
            if (_paused && _ng != null && _ng.State == NightState.Jugando) { PauseMenuGUI(); }
            if (NightKitGUI()) return;
            float W = Screen.width, H = Screen.height, k = H / 1080f;
            if (_nPx == null) { _nPx = new Texture2D(1, 1); _nPx.SetPixel(0, 0, Color.white); _nPx.Apply(); }
            if (_nStyle == null) { var f = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Lucida Console", "Courier New" }, 20); _nStyle = new GUIStyle(GUI.skin.label) { font = f, richText = true }; _nBig = new GUIStyle(_nStyle) { alignment = TextAnchor.MiddleCenter }; }
            _nStyle.fontSize = Mathf.RoundToInt(26 * k); _nBig.fontSize = Mathf.RoundToInt(40 * k);
            void Box(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, _nPx); GUI.color = o; }
            var bone = new Color32(0xd4, 0xd0, 0xcc, 255); var dark = new Color(0.07f, 0.08f, 0.04f, 0.88f);
            // barra superior: reloj al centro, recursos a la izquierda, ola a la derecha
            Box(new Rect(0, 0, W, 64 * k), dark);
            _nStyle.normal.textColor = bone;
            GUI.Label(new Rect(W * 0.5f - 90 * k, 10 * k, 200 * k, 44 * k), $"<b>{TimeStr()}</b>", _nBig);
            float u = _uw.StockShots / (float)WeaponCatalog.ShotsPerU;
            float inPl = 0f; bool empty = false; foreach (var pl0 in _uw.Platoons) { inPl += pl0.AmmoShots / (float)WeaponCatalog.ShotsPerU; if (pl0.AliveCount() > 0 && pl0.AmmoShots <= 0 && _uw.StockShots <= 0) empty = true; }
            GUI.Label(new Rect(20 * k, 16 * k, 900 * k, 40 * k), $"munición {u + inPl:0.0} u (almacén {u:0.0})   ·   abatidos {_nightKills}   ·   pila {_pileNearStation}", _nStyle);
            if (empty) { var cs0 = _nStyle.normal.textColor; _nStyle.normal.textColor = new Color32(0xe0, 0x8c, 0x30, 255); GUI.Label(new Rect(20 * k, 70 * k, 900 * k, 40 * k), "SIN MUNICIÓN: el almacén está vacío", _nStyle); _nStyle.normal.textColor = cs0; }
            GUI.Label(new Rect(W - 820 * k, 16 * k, 800 * k, 40 * k), $"estación {Mathf.CeilToInt(100f * _ng.StationHp / Mathf.Max(1, _ng.StationHpMax))} %   tren {Mathf.CeilToInt(100f * _ng.TrainHp / Mathf.Max(1, _ng.TrainHpMax))} %", _nStyle);
            // rótulo de oleada
            if (Time.unscaledTime < _bannerUntil)
            {
                var r = new Rect(W * 0.5f - 300 * k, 252 * k, 600 * k, 116 * k); Box(r, dark);
                _nStyle.alignment = TextAnchor.MiddleCenter; GUI.Label(r, _bannerText, _nStyle); _nStyle.alignment = TextAnchor.UpperLeft;
            }
            // panel de pelotones (abajo)
            float cw = 250 * k, ch = 150 * k, x0 = 20 * k, y0 = H - ch - 20 * k; int idx = 0;
            foreach (var pl in _uw.Platoons)
            {
                var r = new Rect(x0 + idx * (cw + 10 * k), y0, cw, ch); bool sel = _sel.Contains(pl.Id);
                Box(r, sel ? new Color(0.3f, 0.34f, 0.16f, 0.95f) : dark);
                string nm = PlatoonName(pl);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { _selVeh = null; if (!Event.current.shift) _sel.Clear(); if (!_sel.Contains(pl.Id)) _sel.Add(pl.Id); }
                GUI.Label(new Rect(r.x + 10 * k, r.y + 6 * k, cw, 36 * k), $"<b>{nm}</b>", _nStyle);
                float alive = pl.AliveCount() / (float)Mathf.Max(1, pl.Men.Count);
                Box(new Rect(r.x + 10 * k, r.y + 54 * k, cw - 20 * k, 16 * k), new Color(0.2f, 0.2f, 0.15f, 1f)); Box(new Rect(r.x + 10 * k, r.y + 54 * k, (cw - 20 * k) * alive, 16 * k), new Color32(0x70, 0x78, 0x4c, 255));
                float am = pl.AmmoCap > 0 ? pl.AmmoShots / (float)pl.AmmoCap : 0f;
                Box(new Rect(r.x + 10 * k, r.y + 78 * k, cw - 20 * k, 12 * k), new Color(0.2f, 0.2f, 0.15f, 1f)); Box(new Rect(r.x + 10 * k, r.y + 78 * k, (cw - 20 * k) * am, 12 * k), new Color32(0xe0, 0x8c, 0x30, 255));
                GUI.Label(new Rect(r.x + 10 * k, r.y + 96 * k, cw, 36 * k), $"{pl.AliveCount()}/{pl.Men.Count}  ·  {(pl.Moving ? "en marcha" : pl.Deploy ? "emplazado" : "alto")}", _nStyle);
                idx++;
            }
            // vehículos: tarjetas a la derecha de los pelotones (clic = elegir)
            { float vx = x0 + idx * (cw + 10 * k); int vi = 0; var cs = _nStyle.fontSize; _nStyle.fontSize = Mathf.RoundToInt(20 * k);
              foreach (var v in _vehs)
              {
                  if (v.Go == null) continue; var rv = new Rect(vx + (vi % 2) * 190 * k, y0 + (vi / 2) * 50 * k, 180 * k, 44 * k); vi++;
                  Box(rv, _selVeh == v ? new Color(0.3f, 0.34f, 0.16f, 0.95f) : dark);
                  string st = v.Heli ? (v.State == 0 ? "en tierra" : v.State == 1 ? "despegando" : v.State == 2 ? $"vuelo {v.Alt:0} m" : "aterrizando") : (v.HasTarget ? $"{Mathf.Abs(v.Speed) * 3.6f:0} km/h" : "parado");
                  GUI.Label(new Rect(rv.x + 6 * k, rv.y + 2 * k, rv.width, rv.height), $"<b>{v.Name}</b> {st}", _nStyle);
                  if (GUI.Button(rv, GUIContent.none, GUIStyle.none)) { _selVeh = v; _sel.Clear(); }
              }
              _nStyle.fontSize = cs; }
            _nStyle.fontSize = Mathf.RoundToInt(19 * k); _nStyle.wordWrap = true;
            GUI.Label(new Rect(W - 620 * k, 72 * k, 600 * k, 150 * k), "clic: elegir (arrastra: varios) · clic dcho.: mover\nA + clic: atacar moviéndose · S: mantener · D: emplazar (hacia el cursor)\nG: alto el fuego · T: reabastecer · cámara: flechas / bordes / Z X zoom / botón dcho. girar · F2: depuración · F5: guardar", _nStyle);
            if (_selDrag && Mouse.current != null)
            {
                var mp = Mouse.current.position.ReadValue(); var r = Rect.MinMaxRect(Mathf.Min(_selStart.x, mp.x), H - Mathf.Max(_selStart.y, mp.y), Mathf.Max(_selStart.x, mp.x), H - Mathf.Min(_selStart.y, mp.y));
                if (r.width > 6f) { Box(new Rect(r.x, r.y, r.width, 2), bone); Box(new Rect(r.x, r.yMax, r.width, 2), bone); Box(new Rect(r.x, r.y, 2, r.height), bone); Box(new Rect(r.xMax, r.y, 2, r.height), bone); }
            }
            bool lost = _ng.State == NightState.Derrota, won = _ng.State == NightState.Victoria && !_reportClosed;
            if ((lost && !_endDismissed) || won)
            {
                if (lost) Box(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
                var r = new Rect(W * 0.5f - 420 * k, 252 * k, 840 * k, lost ? 560 * k : 648 * k); Box(r, dark);
                _nStyle.fontSize = Mathf.RoundToInt(34 * k); _nStyle.alignment = TextAnchor.UpperCenter;
                GUI.Label(new Rect(r.x, r.y + 20 * k, r.width, 140 * k), $"<b>{_endReason}</b>", _nStyle);
                _nStyle.fontSize = Mathf.RoundToInt(28 * k); _nStyle.alignment = TextAnchor.UpperLeft;
                int alive = 0, total = 0; foreach (var pl in _uw.Platoons) { alive += pl.AliveCount(); total += pl.Men.Count; }
                var rp = _ng.Report; int integ = won ? rp.IntegrityPermil / 10 : Mathf.CeilToInt(100f * Mathf.Min(_ng.StationHp / (float)Mathf.Max(1, _ng.StationHpMax), _ng.TrainHp / (float)Mathf.Max(1, _ng.TrainHpMax)));
                var sbb = new StringBuilder();
                sbb.AppendLine($"zombis abatidos        {_nightKills}");
                sbb.AppendLine($"bajas                  {total - alive}");
                sbb.AppendLine($"munición gastada       {_nightShotsSpent / (float)WeaponCatalog.ShotsPerU:0.0} u");
                sbb.AppendLine($"munición restante      {_uw.StockShots / (float)WeaponCatalog.ShotsPerU:0.0} u");
                sbb.AppendLine($"integridad             {integ} %");
                if (won) sbb.AppendLine($"muro                   {rp.WallPermil / 10} %");
                sbb.AppendLine($"hora                   {TimeStr()}");
                GUI.Label(new Rect(r.x + 60 * k, r.y + 170 * k, r.width - 120 * k, 380 * k), sbb.ToString(), _nStyle);
                if (won)
                {
                    if (GUI.Button(new Rect(r.center.x - 160 * k, r.yMax - 100 * k, 320 * k, 64 * k), "Seguir")) NextNight();
                }
                else if (_enPie != null && _enPie.Count > 0)
                {   // S2 (Lucía, 7705049): Seguir (predeterminado), Repetir la noche (si hay guardado del atardecer) y Menú principal
                    float bw = 240 * k, gap = 20 * k, n = NetSession.HayAtardecer ? 3 : 2, bx = r.center.x - (n * bw + (n - 1) * gap) * 0.5f, by = r.yMax - 100 * k;
                    if (GUI.Button(new Rect(bx, by, bw, 64 * k), "<b>Seguir</b>") || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)) SeguirTrasCaer();
                    bx += bw + gap;
                    if (NetSession.HayAtardecer) { if (GUI.Button(new Rect(bx, by, bw, 64 * k), "Repetir la noche")) { Sfx.Play("sfx_ui_clic"); NetSession.CargarAtardecer(); NightRun.Active = true; SceneManager.LoadScene("Union"); } bx += bw + gap; }
                    if (GUI.Button(new Rect(bx, by, bw, 64 * k), "Menú principal")) { NetSession.Reset(); NightRun.Active = false; SceneManager.LoadScene("Menu"); }
                }
                else
                {
                    if (NetSession.HayAmanecer) { if (GUI.Button(new Rect(r.x + 60 * k, r.yMax - 100 * k, 320 * k, 64 * k), $"Volver al día {NetSession.AmanecerDia + 1}")) { Sfx.Play("sfx_ui_clic"); long d = NetSession.AmanecerDia; NetSession.CargarAmanecer(); NightRun.Active = true; Debug.Log($"[NOCHE] vuelve al amanecer del día {d + 1}"); SceneManager.LoadScene("Union"); } }   // Eugenio: punto de control al amanecer
                    else if (GUI.Button(new Rect(r.x + 60 * k, r.yMax - 100 * k, 320 * k, 64 * k), "Volver a empezar")) { NetSession.Reset(); NightRun.Active = true; SceneManager.LoadScene("Union"); }
                    if (GUI.Button(new Rect(r.xMax - 380 * k, r.yMax - 100 * k, 320 * k, 64 * k), "Menú principal")) { NetSession.Reset(); NightRun.Active = false; SceneManager.LoadScene("Menu"); }
                }
            }
        }

        readonly Dictionary<int, string> _pname = new Dictionary<int, string>();
        string PlatoonName(Platoon p) { return _pname.TryGetValue(p.Id, out var n) ? n : "Pelotón"; }
        static string PlatoonNameOld(Platoon p)
        {
            int amet = 0, gran = 0, tir = 0; foreach (var m in p.Men) { if (m.Kind == InfKind.Ametrallador) amet++; else if (m.Kind == InfKind.Granadero) gran++; else if (m.Kind == InfKind.Tirador) tir++; }
            return amet > 0 ? "Ametralladores" : gran > 0 ? "Granaderos" : tir > 0 ? "Tiradores" : "Fusileros";
        }
    }
}
