using TBD.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// Hito 3 «Unión»: la red (S0) vive aquí y sigue corriendo al cambiar entre el mapa y una zona (GDD §6: nunca se congela nada).
    /// Dos mundos (A, el que se ve, y B, la copia de la prueba de desincronización) reciben las mismas órdenes. Un único objeto
    /// que sobrevive a los cambios de escena avanza el reloj a 30 Hz con la velocidad x1/x2/x4.
    /// También guarda el último fotograma antes de cambiar de escena, para el fundido (sin pantalla de carga).
    /// </summary>
    public sealed class NetSession : MonoBehaviour
    {
        static NetSession _instance;
        public static NetWorld A { get; private set; }
        public static NetWorld B { get; private set; }
        public static bool Check = true;
        public static bool Desync;
        public static long DesyncTick;

        // paso entre escenas
        public static Texture2D TransitionFrame;       // último fotograma de la escena anterior (para el fundido)
        public static bool PendingAmbush;              // se entra en la zona de la emboscada (el encuentro ya está activo en la red)
        public static int PendingZoneNode = -1;        // nodo al que se entra (la zona espera a que la orden Materialize se aplique)
        public static int ReturnFromNode = -1;         // nodo del que se sale (el mapa coloca la cámara ahí, a 2,5 km)

        float _acc;

        /// <summary>Descarta la sesion de red (nueva partida o repetir la noche).</summary>
        public static void Reset() { PrototypeBootstrap.ClearNightSession(); if (_instance != null) Destroy(_instance.gameObject); _instance = null; A = null; B = null; Desync = false; _atardecerDia = -1; }

        public static string SavePath { get { return System.IO.Path.Combine(Application.persistentDataPath, "partida.tbd"); } }
        public static bool HasSave { get { return System.IO.File.Exists(SavePath); } }
        /// <summary>Multiplicador de pasos de red por fotograma para las corridas automáticas (A y B pasan los mismos ticks). 1 en el juego.</summary>
        public static int Turbo = 1;
        /// <summary>Lucía (3f1764c): al acabar una noche de zona la red salta a las 06:00 del día siguiente (los trenes avanzan con ella). Tick destino, −1 sin salto.</summary>
        public static long SaltarHasta = -1;
        /// <summary>Las 06:00 siguientes al tick de la red (hoy si aún no han dado, si no mañana).</summary>
        public static long ProximoAmanecer(long tick) { long d = tick / NetConst.DayTicks, am = d * NetConst.DayTicks + NetConst.Hours(6); return tick < am ? am : am + NetConst.DayTicks; }
        public static void SaltarAlAmanecer() { if (A == null) return; SaltarHasta = ProximoAmanecer(A.Tick); if (A.Paused) Push(NetCmdType.Resume, 0, 0); Debug.Log($"[TBD] la red salta al amanecer: {NetConst.Clock(A.Tick)} → día {SaltarHasta / NetConst.DayTicks + 1}, 06:00"); }
        // Guardado automático del atardecer (Lucía, 7705049: «Repetir la noche»): la red a las 19:00 de cada día, la hora del aviso de la horda de zona. Solo la red.
        public static string SaveAtardecer { get { return System.IO.Path.Combine(Application.persistentDataPath, "atardecer.tbd"); } }
        public static bool HayAtardecer { get { return _atardecerDia >= 0 && System.IO.File.Exists(SaveAtardecer); } }
        static long _atardecerDia = -1;
        void GuardarAtardecer()
        {
            if (A == null) return; long dia = A.Tick / NetConst.DayTicks;
            if (dia == _atardecerDia || A.Tick % NetConst.DayTicks < NetConst.Hours(19) || A.Tick % NetConst.DayTicks >= NetConst.Hours(22)) return;
            try { System.IO.File.WriteAllBytes(SaveAtardecer, NetSave.Save(A)); _atardecerDia = dia; Debug.Log($"[TBD] guardado del atardecer (día {dia + 1}, {NetConst.Clock(A.Tick)})"); } catch (System.Exception e) { Debug.LogWarning("[TBD] guardado del atardecer: " + e.Message); }
        }
        /// <summary>«Repetir la noche»: la red del último atardecer en dos mundos nuevos.</summary>
        public static void CargarAtardecer()
        {
            var data = System.IO.File.ReadAllBytes(SaveAtardecer); long dia = _atardecerDia;
            Reset(); var a = NetScenario.Inicio(false); NetSave.Load(data, a); var b = NetScenario.Inicio(false); NetSave.Load(data, b);
            Ensure(); A = a; B = b; _atardecerDia = dia;
        }

        /// <summary>Guarda la red (NetSave) en partida.tbd. Devuelve false si no hay sesion.</summary>
        public static bool SaveGame()
        {
            if (A == null) return false;
            System.IO.File.WriteAllBytes(SavePath, NetSave.Save(A)); return true;
        }

        /// <summary>Carga partida.tbd en dos mundos nuevos (A y B) y los pone en la sesion. Lanza SaveException si el archivo no vale.</summary>
        public static void LoadGame()
        {
            var data = System.IO.File.ReadAllBytes(SavePath);
            var a = NetScenario.Inicio(false); NetSave.Load(data, a);
            var b = NetScenario.Inicio(false); NetSave.Load(data, b);
            Ensure(); A = a; B = b;
        }

        public static void Ensure()
        {
            if (_instance != null) return;
            A = NetScenario.Inicio();
            B = NetScenario.Inicio();
            var go = new GameObject("TBD_NetSession");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NetSession>();
        }

        /// <summary>
        /// Solo para probar (F8 en el mapa): monta una emboscada ya materializada sobre el primer tramo con punto de emboscada, con el tren 1
        /// parado en él y 300 ‰ de horda. Se aplica igual a los dos mundos (no es una orden de red: no vale para coop).
        /// </summary>
        public static bool DebugForceAmbush()
        {
            Ensure();
            if (A.Encounter.Active) return false;
            foreach (var w in new[] { A, B })
            {
                int ei = w.Graph.Edges.FindIndex(e => e.Ambush.Count > 0);
                var edge = w.Graph.Edges[ei];
                int cell = (w.Graph.Nodes[edge.A].YMeters + w.Graph.Nodes[edge.B].YMeters) / 2 * HordeField.W + (w.Graph.Nodes[edge.A].XMeters + w.Graph.Nodes[edge.B].XMeters) / 2;
                var sv = w.Services[0];
                for (int r = 0; r < NetConst.ResCount; r++) if (sv.Cargo[r] < 100) sv.Cargo[r] = 100;       // solo prueba: con munición el tren puede disparar
                sv.Ambushed = true; sv.AtStop = false; sv.Train.SetRoute(w.Graph.ShortestPath(edge.A, edge.B)); sv.Train.Stop();
                w.Encounter = new NetWorld.AmbushEncounter { Active = true, Service = 0, Edge = ei, Cell = cell, Taken = 300, ExpireTick = w.Tick + NetWorld.AmbushWaitTicks };
            }
            return true;
        }

        public static void PushPayload(NetCmdType type, int arg, int[] payload)
        {
            Ensure();
            var c = new NetCommand { Tick = A.Tick, Type = type, Arg = arg, Payload = payload };
            A.Push(c);
            B.Push(c);
        }

        public static void Push(NetCmdType type, int arg, long delay, int arg2 = 0)
        {
            Ensure();
            var c = new NetCommand { Tick = A.Tick + delay, Type = type, Arg = arg, Arg2 = arg2 };
            A.Push(c);
            B.Push(c);
        }

        // QA (Sara, reloj x103): el aviso «Horda en 3 h» debe durar 105 s +-2 entre que se anuncia y que empieza el asedio; se escribe en el log con realtimeSinceStartup
        readonly System.Collections.Generic.Dictionary<long, float> _avisoT = new System.Collections.Generic.Dictionary<long, float>(); readonly System.Collections.Generic.HashSet<long> _asedioIniciado = new System.Collections.Generic.HashSet<long>();
        void AvisoLog()
        {
            if (A == null || A.Director == null) return;
            foreach (var pl in A.Director.Active)
            {
                if (pl.Kind != HiveDirector.PlanKind.Asedio) continue; long key = pl.StartTick * 1000L + pl.Node;
                if (!_avisoT.ContainsKey(key)) { _avisoT[key] = Time.realtimeSinceStartup; Debug.Log($"[AVISO] Horda en 3 h anunciada: nodo {pl.Node}, tick {A.Tick}, empieza en el tick {pl.StartTick}, t = {Time.realtimeSinceStartup:0.0} s"); }
                if (pl.Started && _asedioIniciado.Add(key)) Debug.Log($"[AVISO] asedio EMPIEZA: nodo {pl.Node}, tick {A.Tick}, t = {Time.realtimeSinceStartup:0.0} s, avisado hace {Time.realtimeSinceStartup - _avisoT[key]:0.0} s reales");
            }
        }

        void Update()
        {
            AudioListener.pause = (PrototypeBootstrap.ZonaPausada && SceneManager.GetActiveScene().name == "SampleScene") || (A != null && A.Paused && SceneManager.GetActiveScene().name == "Union");   // al pausar, silencio (Nuria, revisión del 9-nov): pausa de la zona o del mapa
            AvisoLog(); GuardarAtardecer();
            var kb5 = UnityEngine.InputSystem.Keyboard.current;
            if (kb5 != null && kb5.f5Key.wasPressedThisFrame) { bool ok = SaveGame(); Debug.Log(ok ? "[TBD] Partida guardada en " + SavePath : "[TBD] No hay partida que guardar"); }   // F5: guardado rapido
            if (SaltarHasta > A.Tick)
            {
                int n = (int)System.Math.Min(SaltarHasta - A.Tick, 2500L);
                for (int i = 0; i < n; i++) { A.Step(); if (Check) B.Step(); if (A.Paused) break; }
                if (Check && !Desync && A.Hash() != B.Hash()) { Desync = true; DesyncTick = A.Tick; Debug.LogError($"[TBD] Red: desincronización en el tick {A.Tick}"); }
                if (A.Tick >= SaltarHasta) { SaltarHasta = -1; _acc = 0f; Debug.Log($"[TBD] amanece: día {A.Tick / NetConst.DayTicks + 1}, {NetConst.Clock(A.Tick)}"); }
                return;
            }
            _acc += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            const float step = 1f / NetConst.TickRate;
            while (_acc >= step)
            {
                _acc -= step;
                int n = A.Paused ? 1 : A.Speed * Mathf.Max(1, Turbo);   // en pausa se sigue llamando a Step para aplicar las órdenes; Turbo solo para corridas (-dia2-auto)
                for (int i = 0; i < n; i++)
                {
                    A.Step();
                    if (Check) B.Step();
                    if (Check && !Desync && A.Hash() != B.Hash()) { Desync = true; DesyncTick = A.Tick; Debug.LogError($"[TBD] Red: desincronización en el tick {A.Tick}"); }
                }
            }
        }

        /// <summary>Captura el fotograma actual (al final del fotograma) para fundirlo sobre la escena siguiente.</summary>
        public static System.Collections.IEnumerator CaptureTransition()
        {
            yield return new WaitForEndOfFrame();
            if (TransitionFrame != null) Destroy(TransitionFrame);
            TransitionFrame = ScreenCapture.CaptureScreenshotAsTexture();
        }

        /// <summary>Dibuja el último fotograma de la escena anterior desvaneciéndose (llamar desde OnGUI). Devuelve false al acabar.</summary>
        public static bool DrawFade(float since, float duration = 0.6f)
        {
            if (TransitionFrame == null) return false;
            float a = 1f - Mathf.Clamp01(since / duration);
            if (a <= 0f) { Destroy(TransitionFrame); TransitionFrame = null; return false; }
            var prev = GUI.color;
            GUI.color = new Color(1, 1, 1, a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), TransitionFrame, ScaleMode.StretchToFill, false);
            GUI.color = prev;
            return true;
        }
    }
}
