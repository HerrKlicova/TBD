using System;
using System.Collections.Generic;
using System.IO;
using TBD.Net;
using TBD.Tutorial;
using TBD.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// Vista del motor del tutorial (TutorialMotor, C# puro). Un único objeto que sobrevive a los cambios de escena (mapa ↔ zona), como NetSession.
    /// Dibuja la tarjeta arriba a la izquierda, debajo de los avisos («Capítulo N · Paso x de y»), y el anillo que late en hueso d4d0cc sobre lo que hay que tocar.
    /// Nada de rojo. IMGUI, como el resto de la interfaz del prototipo (la v2 de PixelLab llegará después, D-N162).
    ///
    /// Enganches (lo que llama el resto del juego; todo estático y seguro aunque el tutorial no esté en marcha):
    ///   IntroVista.Antes(seguir)      «Nueva partida»: intro C y, al entrar en Sagunto, el capítulo 0.
    ///   TutorialHud.Senal("…")        avisa de algo que ha hecho el jugador (nombres en TutorialGuion.Senales).
    ///   TutorialHud.Objetivo(k, r)    en OnGUI, quien dibuja un elemento resaltable da su rectángulo (claves en TutorialGuion.Resaltes).
    ///   TutorialHud.PidePausa         la zona se pausa mientras sale la tarjeta del capítulo 0.
    ///   TutorialHud.HordaEnEspera     la zona deja la horda agrupada mientras dura un capítulo con combate (D-N154).
    ///   TutorialHud.Activo/Saltar()   el menú de pausa enseña «Saltar tutorial» si Activo.
    ///   TutorialHud.AnclaY            la zona dice dónde acaban sus avisos (px); la tarjeta va debajo.
    ///   TutorialHud.ComprobarC90(..)  la zona, una vez por segundo, con los pelotones, los zombis y «dentro del recinto»: tarjeta del C90.
    /// Lo que hace solo: «mapa_abierto» al cargar el mapa, «tren_en_nules» al llegar un tren a Nules, «vuelta_zona» al volver a la zona,
    /// y los anillos de los 3 nodos del mapa.
    /// Pruebas: -tutorial (sale aunque ya se haya visto), -sintutorial, -tutorial-auto (cada tarjeta se da por hecha a los 2,5 s y se captura en
    /// Captures/tutorial/: es la corrida automática, sin jugar).
    /// </summary>
    public sealed class TutorialHud : MonoBehaviour
    {
        sealed class Prefs : IMemoria
        {
            public bool Leer(string c) { return PlayerPrefs.GetInt("tbd." + c, 0) != 0; }
            public void Escribir(string c) { PlayerPrefs.SetInt("tbd." + c, 1); PlayerPrefs.Save(); }
        }

        static TutorialHud _i;
        static bool _pendiente;
        TutorialMotor _m;
        int _logVisto;
        readonly Dictionary<string, (Rect r, int frame)> _obj = new Dictionary<string, (Rect, int)>();
        GUIStyle _head, _txt, _btn, _line; Texture2D _panel, _white, _btnTex, _btnHot;
        float _flash; int _repVistas; string _pasoVisto; bool _pausaMapa; float _c90T;
        Maquina _maq, _maqS; Suelta _sueltaVista; readonly List<string> _snd = new List<string>();   // texto letra a letra con tecleo (Eugenio, 8-nov)

        static readonly Color Bone = new Color32(0xd4, 0xd0, 0xcc, 255), Ink = new Color32(0x1c, 0x20, 0x0c, 255);

        public static float AnclaY = -1f;       // < 0: la tarjeta se coloca sola (mapa: debajo de sus paneles; zona: 22 % de la altura)
        public static bool Auto { get { return Arg("-tutorial-auto"); } }
        public static TutorialMotor Motor { get { return _i != null ? _i._m : null; } }
        public static bool Activo { get { return _i != null && _i._m != null && _i._m.Estado == EstadoTutorial.Activo; } }
        public static bool PidePausa { get { return Activo && _i._m.PidePausa; } }
        public static bool HordaEnEspera { get { return Activo && _i._m.PideHordaEnEspera; } }

        static bool Arg(string a) { return Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0; }

        /// <summary>Lo llama «Nueva partida» (a través de IntroVista.Antes): el tutorial arranca al cargar la zona de Sagunto.</summary>
        public static void NuevaPartida() { Ensure(); _pendiente = true; _pila150 = false; _contactoT = -1f; _zona.Reiniciar(); _zonaEstado.Clear(); AerodromoVista.Reiniciar(); if (_i._m != null && _i._m.Estado == EstadoTutorial.Activo) _i._m.Saltar(); _i._m = null; }

        /// <summary>D-N172: ¿hay destacamento en Nules? Solo con el tutorial en marcha (fácil y normal, la primera vez). La zona lo mira al crear sus unidades.</summary>
        public static bool DestacamentoNules { get { return Activo; } }

        /// <summary>
        /// D-N172: la zona (Marcos) ha creado el pelotón de fusileros del destacamento de Nules (sin Lince desde la revisión: vehIndex = −1). Aquí se aparta
        /// de Sagunto (Aboard = EnEstacion(Nules)) y se apunta en la red, en los dos mundos.
        /// </summary>
        public static void RegistrarDestacamento(UnitsWorld uw, int platoonId, int vehIndex = -1)
        {
            var w = NetSession.A; if (w == null) return;
            int nul = TrenPreparar.Nodo(w, Destacamento.Estacion);
            var p = uw != null ? uw.Platoons.Find(q => q.Id == platoonId) : null; if (p != null) p.Aboard = TrenPreparar.EnEstacion(nul);
            if (vehIndex >= 0 && vehIndex < NightRun.Vehs.Count && NightRun.Vehs[vehIndex] != null) NightRun.Vehs[vehIndex].Embarcado = true;
            Destacamento.Registrar(w, platoonId, vehIndex); if (NetSession.B != null) Destacamento.Registrar(NetSession.B, platoonId, vehIndex);
            Debug.Log($"[TUTORIAL] destacamento de Nules: pelotón {platoonId}, vehículo {vehIndex}");
        }

        // objetivo de después de la primera noche (D-N172 ajustada): «Viaje de carga a Nules», sin tarjetas de tutorial. Lo avanza TrenPanel.
        public static readonly ObjetivoViaje Viaje = new ObjetivoViaje();
        static float _viajeCumplidoHasta = -1f;
        static void VigilarViaje()
        {
            var w = NetSession.A;
            // objetivo de campaña: al amanecer después de la primera noche, también si se saltó el tutorial; en difícil, no (Lucía)
            if (Viaje.Paso == PasoViaje.Inactivo && NightRun.Active && ObjetivoViaje.HayObjetivo(NightRun.Difficulty) && w != null && w.Tick >= NetConst.DayTicks + NetConst.Hours(7))
            { Viaje.Empezar(); Debug.Log("[TUTORIAL] objetivo: " + ObjetivoViaje.Titulo); }
            if (Viaje.Paso == PasoViaje.Cumplido && _viajeCumplidoHasta < 0) { _viajeCumplidoHasta = Time.unscaledTime + 6f; Debug.Log("[TUTORIAL] objetivo cumplido: " + ObjetivoViaje.Titulo); }
        }
        static bool ObjetivoVisible { get { return Viaje.Paso != PasoViaje.Inactivo && (Viaje.Paso != PasoViaje.Cumplido || Time.unscaledTime < _viajeCumplidoHasta); } }
        TutorialMotor _principal;                 // el tutorial de la primera partida (capítulos 0 a 3)
        bool _cap4Pendiente;

        /// <summary>
        /// El jugador abre el aeródromo (AerodromoVista). La primera vez salta el capítulo 4 (D-N173), en fácil y normal. Si el tutorial principal está en
        /// marcha, espera a que acabe.
        /// </summary>
        public static void AbrirAerodromo()
        {
            Ensure(); Senal(TutorialGuion.Senales.AerodromoAbierto);
            var prefs = new Prefs();
            if (_i._cap4Pendiente || !TutorialMotor.HayTutorial(NightRun.Difficulty) || prefs.Leer(TutorialGuion.ClaveCap4) || Arg("-sintutorial")) return;
            if (prefs.Leer(TutorialMotor.ClaveVisto + TutorialMotor.ClaveSaltado) && !Arg("-tutorial")) return;     // tutorial saltado: tampoco sale el capítulo 4 (Lucía)
            if (_i._m != null && _i._m.Estado == EstadoTutorial.Activo && _i._m == _i._principal) { _i._cap4Pendiente = true; return; }
            _i.ArrancarCap4();
        }
        void ArrancarCap4()
        {
            var m4 = TutorialGuion.NuevoCap4(new Prefs()); m4.EstadoActual = EstadoJuego; m4.Relleno = t => t.Replace("{eta}", AerodromoVista.EtaTexto());
            if (!m4.Arrancar(NightRun.Difficulty, forzar: Arg("-tutorial"))) return;
            _cap4Pendiente = false; _m = m4; _logVisto = 0; _pasoVisto = null;
            Debug.Log("[TUTORIAL] capítulo 4 (aeródromo): arranca");
        }



        public static void Senal(string s)
        {
            if (s == TutorialGuion.Senales.Pila150) _pila150 = true;
            if (s == TutorialGuion.Senales.ContactoMuro && _contactoT < 0) _contactoT = Time.unscaledTime;
            if (_i != null && _i._m != null) _i._m.Senal(s);
        }
        static float _contactoT = -1f;
        public const float Cap3TrasContacto = 60f;      // Lucía: 60 s reales después del primer contacto con el muro
        static bool PorTiempo { get { return _contactoT >= 0 && Time.unscaledTime - _contactoT >= Cap3TrasContacto; } }
        static bool _pila150;      // la pila ya pasó de 150 alguna vez en esta partida (abre el capítulo 3 aunque pasara antes de acabar el 2)
        public static void Saltar() { if (_i != null && _i._m != null) _i._m.Saltar(); }
        public static void Objetivo(string clave, Rect pantalla) { if (_i != null) _i._obj[clave] = (pantalla, Time.frameCount); }

        /// <summary>
        /// La zona lo llama con sus pelotones, sus zombis (en mm, como UnitsWorld) y su prueba de «dentro del recinto». Cuenta como mucho una vez por segundo.
        /// Regla de Lucía (8-nov): 40 zombis o más dentro del recinto a menos de 30 m de un pelotón de fusileros (C90Aviso).
        /// </summary>
        public static void ComprobarC90(UnitsWorld uw, IList<Target> zombis, Func<int, int, bool> dentroRecinto = null)   // null: sin la prueba de recinto (cuenta todos); Marcos la pasa
        {
            if (!Activo || Time.unscaledTime - _i._c90T < 1f) return;
            _i._c90T = Time.unscaledTime;
            if (C90Aviso.Buscar(uw, zombis, dentroRecinto) >= 0) Senal(TutorialGuion.Senales.C90ATiro);
        }

        /// <summary>
        /// La zona lo llama cada segundo de noche (PrototypeBootstrap.NightTick): zombis vivos, vehículos fuera del tren y la vida por mil de cada tramo del muro.
        /// Envía horda_avanza, ataque_rechazado, vehiculos_fuera y tramo_sobre_70 (SenalesZona) y los guarda como estado para las tarjetas que ya estén fuera.
        /// </summary>
        public static void Zona(int zombisVivos, int vehiculosFuera, IList<int> tramosPm)
        {
            if (!Activo || Time.unscaledTime - _zonaT < 1f) return;
            _zonaT = Time.unscaledTime;
            _zonaEstado.Clear();
            foreach (var s in _zona.Mirar(HordaEnEspera, zombisVivos, vehiculosFuera, tramosPm)) { _zonaEstado.Add(s); Senal(s); }
        }
        static float _zonaT = -1f;
        static readonly SenalesZona _zona = new SenalesZona();
        static readonly HashSet<string> _zonaEstado = new HashSet<string>();

        static void Ensure()
        {
            if (_i != null) return;
            var go = new GameObject("TBD_Tutorial"); DontDestroyOnLoad(go);
            _i = go.AddComponent<TutorialHud>();
            SceneManager.sceneLoaded += _i.OnScene;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // pruebas: -tutorial-auto o -tutorial sin pasar por el menú (con -menu-auto el menú arranca solo, pero sin intro); se arranca al entrar en la zona
            if (GoldenRunner.Active) return;
            if (Arg("-tutorial-auto") || Arg("-tutorial")) { Ensure(); _pendiente = true; }
            if (Arg("-tutorial-auto") && Arg("-intro") && SceneManager.GetActiveScene().name == "Menu") IntroVista.Antes(null);   // corrida automática: la intro se ve aunque «Nueva partida» aún no la llame
        }

        void OnDestroy() { SceneManager.sceneLoaded -= OnScene; if (_i == this) _i = null; }

        void OnScene(Scene s, LoadSceneMode mode)
        {
            AnclaY = -1f; _obj.Clear();
            if (s.name == "SampleScene")
            {
                if (_pendiente && NightRun.Active && !Arg("-sintutorial"))
                {
                    _pendiente = false;
                    _m = TutorialGuion.Nuevo(new Prefs()); _logVisto = 0; _m.EstadoActual = EstadoJuego; _m.Relleno = t => t.Replace("{eta}", TrenPanel.EtaTexto()); _principal = _m;
                    bool ok = _m.Arrancar(NightRun.Difficulty, forzar: Arg("-tutorial") || Arg("-tutorial-auto"));
                    Debug.Log(ok ? $"[TUTORIAL] arranca (dificultad {NightRun.Difficulty})" : $"[TUTORIAL] no arranca (dificultad {NightRun.Difficulty})");
                    if (!ok) _m = null;
                    else if (NetSession.A != null)
                    {   // D-N172: con el tutorial, Nules tiene su tren en el andén (el destacamento lo registra la zona con RegistrarDestacamento)
                        int a = Destacamento.SembrarTren(NetSession.A); if (NetSession.B != null) Destacamento.SembrarTren(NetSession.B);
                        Debug.Log($"[TUTORIAL] tren de Nules en el andén (servicio {a})");
                    }
                }
                else if (Activo && _m.Hecho(TutorialGuion.Senales.MapaAbierto)) Senal(TutorialGuion.Senales.VueltaZona);
            }
            else if (s.name == "Union") Senal(TutorialGuion.Senales.MapaAbierto);
        }

        /// <summary>Señales que son estados y se pueden mirar en cualquier momento (revisión de Nacho): si ya se cumplen al salir la tarjeta, cuenta.</summary>
        public static Func<string, bool> EstadoExtra;      // lo amplía el panel del tren (TrenPanel)
        static bool EstadoJuego(string s)
        {
            if (s == TutorialGuion.Senales.MapaAbierto) return SceneManager.GetActiveScene().name == "Union";
            if (s == TutorialGuion.Senales.Pila150) return _pila150;
            if (s == TutorialGuion.Senales.Cap3Dispara) return _pila150 || PorTiempo;
            if (s == TutorialGuion.Senales.Cap3PorTiempo) return !_pila150 && PorTiempo;
            if (_zonaEstado.Contains(s)) return true;
            return EstadoExtra != null && EstadoExtra(s);
        }

        void Update()
        {
            if (_cap4Pendiente && (_m == null || _m.Estado != EstadoTutorial.Activo)) ArrancarCap4();
            if (_m == null || IntroVista.Mostrando) return;     // mientras sale la intro, el tutorial espera
            _m.Avanzar(Time.unscaledDeltaTime);
            // tren en Nules: lo mira en la red (servicio parado en Nules) una vez enviado
            if (Activo && _m.Hecho(TutorialGuion.Senales.TrenEnviado) && !_m.Hecho(TutorialGuion.Senales.TrenEnNules) && NetSession.A != null)
            {
                var w = NetSession.A; int nules = w.Graph.Nodes.FindIndex(n => n.Name == "Nules");
                foreach (var sv in w.Services) if (sv.AtStop && sv.Stops.Count > 0 && sv.Stops[sv.StopIndex].Node == nules) { Senal(TutorialGuion.Senales.TrenEnNules); break; }
            }
            // en el mapa la pausa es la de la red (en la zona la aplica la zona con PidePausa)
            if (SceneManager.GetActiveScene().name == "Union" && NetSession.A != null)
            {
                bool want = PidePausa;
                if (want && !_pausaMapa && !NetSession.A.Paused) { NetSession.Push(NetCmdType.Pause, 0, 0); _pausaMapa = true; }
                if (!want && _pausaMapa) { NetSession.Push(NetCmdType.Resume, 0, 0); _pausaMapa = false; }
            }
            var p = _m.Actual;
            if (p != null && _m.Visible)
            {
                if (_pasoVisto != p.Id) { _pasoVisto = p.Id; _repVistas = _m.Repeticiones; _flash = 0.6f; _maq = new Maquina(_m.TextoSinRelleno, (uint)p.Id.GetHashCode()); }
                if (_m.Repeticiones != _repVistas) { _repVistas = _m.Repeticiones; _flash = 1.2f; _maq = new Maquina(_m.TextoSinRelleno, (uint)(p.Id.GetHashCode() + _repVistas)); }   // al repetirse se vuelve a escribir
                if (_maq != null && _maq.Texto != _m.TextoSinRelleno) _maq = new Maquina(_m.TextoSinRelleno, 7);   // el texto cambió (la línea extra del 1.3); el ETA no cuenta
                _maq?.Avanzar(Time.unscaledDeltaTime, _snd);
                if (Auto && _m.EnPaso >= 2.5f && p.Cierre == Cierre.Senal)
                {
                    CapturaAuto("paso_" + p.Id.Replace('.', '_'));
                    if (_m.EnPaso >= 3f) foreach (var s in p.Senales) { Debug.Log($"[TUTORIAL] auto: {p.Id} → {s}"); _m.Senal(s); if (_m.Actual != p) break; }
                }
                else if (Auto && p.Cierre == Cierre.Segundos && _m.EnPaso >= Mathf.Min(2.5f, p.Segundos - 0.3f)) CapturaAuto("paso_" + p.Id.Replace('.', '_'));
            }
            else if (Auto && p != null && !_m.Visible && _m.EnPaso == 0 && p.Espera != null) { _esperaT += Time.unscaledDeltaTime; if (_esperaT > 2f) { _esperaT = 0; Debug.Log($"[TUTORIAL] auto: {p.Id} espera {p.Espera}"); _m.Senal(p.Espera); } }
            if (_m.SueltaActual != _sueltaVista) { _sueltaVista = _m.SueltaActual; _maqS = _sueltaVista != null ? new Maquina(_sueltaVista.Texto, 90) : null; }
            _maqS?.Avanzar(Time.unscaledDeltaTime, _snd);
            IntroVista.Sonar(_snd);
            if (_m.SueltaActual != null && Auto) CapturaAuto("suelta_" + _m.SueltaActual.Id);
            _flash = Mathf.Max(0, _flash - Time.unscaledDeltaTime);
            while (_logVisto < _m.Registro.Count) Debug.Log("[TUTORIAL] " + _m.Registro[_logVisto++]);
        }
        float _esperaT;

        // ---------------------------------------------------------------- capturas de la corrida automática
        static readonly HashSet<string> _capturadas = new HashSet<string>();
        public static void CapturaAuto(string nombre)
        {
            if (!Auto || !_capturadas.Add(nombre)) return;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "tutorial"));
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, nombre + ".png"));
            Debug.Log("[TUTORIAL] captura " + nombre);
        }

        // ---------------------------------------------------------------- dibujo
        static Texture2D Tex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

        void Styles()
        {
            if (_txt != null) return;
            _panel = Tex(new Color(0.07f, 0.08f, 0.05f, 0.92f)); _white = Tex(Color.white);
            _btnTex = Tex(new Color(0.16f, 0.18f, 0.10f, 1f)); _btnHot = Tex(Bone);
            int u = Mathf.Max(10, Screen.height / 60);
            _head = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 1.2f), fontStyle = FontStyle.Bold }; _head.normal.textColor = new Color(Bone.r, Bone.g, Bone.b, 0.7f);
            _txt = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 1.7f), fontStyle = FontStyle.Bold, wordWrap = true, richText = true }; _txt.normal.textColor = Bone;   // letra gruesa (Eugenio)
            _line = new GUIStyle(_txt) { wordWrap = false };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u * 1.2f), fontStyle = FontStyle.Bold };
            _btn.normal.background = _btnTex; _btn.hover.background = _btnHot; _btn.active.background = _btnHot; _btn.focused.background = _btnTex;
            _btn.normal.textColor = Bone; _btn.hover.textColor = Ink; _btn.active.textColor = Ink; _btn.focused.textColor = Bone;
        }

        float Ancla()
        {
            if (AnclaY >= 0) return AnclaY;
            if (SceneManager.GetActiveScene().name == "Union" && NetSession.A != null)
            {   // debajo del panel de carga de los trenes de OperationsMap3D (misma cuenta que DrawCargo)
                float fs = Mathf.Max(10, Screen.height / 64); int px = 16 * Mathf.Max(1, Mathf.RoundToInt(Screen.height / 540f));
                return Screen.height * 0.12f + 6 * fs + (px + 6) * (NetSession.A.Services.Count + 0.8f) + 10;
            }
            return Screen.height * 0.22f;
        }

        void MapTargets()
        {
            if (SceneManager.GetActiveScene().name != "Union" || NetSession.A == null) return;
            var op = FindFirstObjectByType<OperationsMap3D>(); var cam = Camera.main;
            if (op == null || cam == null) return;
            int px = 32 * Mathf.Max(1, Mathf.RoundToInt(Screen.height / 540f));
            foreach (var n in NetSession.A.Graph.Nodes)
            {
                string k = n.Name == "Sagunto" ? TutorialGuion.Resaltes.NodoSagunto : n.Name == "Nules" ? TutorialGuion.Resaltes.NodoNules : n.Name == "Castellón" ? TutorialGuion.Resaltes.NodoCastellon : null;
                if (k == null) continue;
                var sp = cam.WorldToScreenPoint(op.MapToWorld(n.XMeters, n.YMeters, 0f));
                if (sp.z <= 0) continue;
                Objetivo(k, new Rect(sp.x - px * 0.6f, Screen.height - sp.y - px * 1.2f, px * 1.2f, px * 1.3f));
            }
        }

        // ---- Anillo que late, estilo D (Lucía, 8-nov): esquinas fijas en hueso y un eco de esquinas que sale y se apaga.
        // Sustituye a Ring(Rect r, float t) en TutorialHud.cs (línea ~291). Usa Bone y _white, que ya existen.
        // Unidad u = alto/360 (3 px a 1080p), redondeada para que salga nítido. Ciclo de 1,2 s.
        // Esquinas en vez de marco entero: un marco hueso entero es como se ve una tarjeta SELECCIONADA (D-N147);
        // así «toca aquí» no se confunde con «ya está seleccionado».
        void Ring(Rect r, float t)
        {
            float u = Mathf.Max(2f, Mathf.Round(Screen.height / 360f));
            float fase = Mathf.Repeat(t, 1.2f) / 1.2f;
            var prev = GUI.color;
            GUI.color = Bone;
            Esquinas(r, 3f * u, u, 4f * u);                                       // fijas: 3u por fuera, trazo de 1u, brazos de 4u
            float a = 1f - fase;
            if (a > 0.15f)                                                         // eco: sale de 4u a 11u en saltos de 1u y se apaga
            {
                GUI.color = new Color(Bone.r, Bone.g, Bone.b, 0.8f * a);
                Esquinas(r, (4f + Mathf.Floor(fase * 8f)) * u, u, 3f * u);
            }
            GUI.color = prev;
        }

        void Esquinas(Rect r, float g, float th, float largo)
        {
            float x0 = Mathf.Round(r.x - g), y0 = Mathf.Round(r.y - g), x1 = Mathf.Round(r.xMax + g), y1 = Mathf.Round(r.yMax + g);
            Barra(x0, y0, largo, th); Barra(x0, y0, th, largo);                                  // arriba a la izquierda
            Barra(x1 - largo, y0, largo, th); Barra(x1 - th, y0, th, largo);                     // arriba a la derecha
            Barra(x0, y1 - th, largo, th); Barra(x0, y1 - largo, th, largo);                     // abajo a la izquierda
            Barra(x1 - largo, y1 - th, largo, th); Barra(x1 - th, y1 - largo, th, largo);        // abajo a la derecha
        }

        void Barra(float x, float y, float w, float h) { GUI.DrawTexture(new Rect(x, y, w, h), _white); }

        // Opcional (capítulo 4, tutorial-refuerzos.md): flecha de borde cuando el objetivo queda fuera de la pantalla.
        // Cuña de 14x16 a 1080p (escala con u/3), en hueso, pegada al borde y apuntando hacia fuera; avanza 1u con el mismo ciclo.
        void FlechaBorde(Vector2 pantallaObjetivo, float t, string rotulo = null)
        {
            float u = Mathf.Max(2f, Mathf.Round(Screen.height / 360f)), k = u / 3f, m = 8f * u;
            var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f); var d = pantallaObjetivo - c; if (d.sqrMagnitude < 1f) return;
            float s = Mathf.Min((c.x - m) / Mathf.Max(1e-3f, Mathf.Abs(d.x)), (c.y - m) / Mathf.Max(1e-3f, Mathf.Abs(d.y)));
            var p = c + d * s;
            float paso = Mathf.Repeat(t, 1.2f) < 0.6f ? 0f : u, dir = Mathf.Sign(Mathf.Abs(d.x) > Mathf.Abs(d.y) ? d.x : d.y);
            var prev = GUI.color; GUI.color = Bone;
            bool horiz = Mathf.Abs(d.x) * (c.y - m) >= Mathf.Abs(d.y) * (c.x - m);
            for (int i = 0; i < 8; i++)                                            // 8 columnas de 2 px: 16 de alto a 2 de alto
            {
                float hh = (16 - 2 * i) * k, off = (i * 2f * k + paso) * dir;
                if (horiz) Barra(Mathf.Round(p.x + off), Mathf.Round(p.y - hh * 0.5f), Mathf.Round(2f * k), Mathf.Round(hh));
                else Barra(Mathf.Round(p.x - hh * 0.5f), Mathf.Round(p.y + off), Mathf.Round(hh), Mathf.Round(2f * k));
            }
            GUI.color = prev;
            if (rotulo != null) GUI.Label(new Rect(p.x + (horiz ? -dir * 150f * k - (dir < 0 ? -20f * k : 0f) : -70f * k), p.y + (horiz ? -8f * k : -dir * 30f * k), 140f * k, 20f * k), rotulo);
        }

        void Frame(Rect r, float a)
        {
            float th = Mathf.Max(1f, Screen.height / 540f);
            var prev = GUI.color; GUI.color = new Color(Bone.r, Bone.g, Bone.b, a);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, th), _white); GUI.DrawTexture(new Rect(r.x, r.yMax - th, r.width, th), _white);
            GUI.DrawTexture(new Rect(r.x, r.y, th, r.height), _white); GUI.DrawTexture(new Rect(r.xMax - th, r.y, th, r.height), _white);
            GUI.color = prev;
        }

        /// <summary>Lo escrito hasta ahora y el resto transparente: el bloque ocupa ya su sitio final y las líneas no saltan al escribirse.</summary>
        static string Escrito(Maquina mq, string entero)
        {
            if (mq == null || mq.Completa || entero == null) return entero;
            int v = Math.Min(mq.Visibles, entero.Length);                // el hueco del ETA ya va relleno: se cuentan letras sobre el texto final
            return entero.Substring(0, v) + "<color=#00000000>" + entero.Substring(v) + "</color>";
        }

        float _radarHasta = -1f;
        void Avisos()
        {
            // aviso de radar que abre el capítulo 3 (D-N172): 12 s arriba en el centro, en hueso (nada de rojo)
            float u = _txt.fontSize;     // (el aviso de radar del capítulo 3 salió con la D-N172 revisada: ahora lo abre la pila)
            if (Time.unscaledTime < _radarHasta)
            {
                var r = new Rect(Screen.width * 0.5f - u * 15, u * 4.2f, u * 30, u * 2.6f);
                GUI.DrawTexture(r, _panel); Frame(r, 0.8f); Objetivo(TutorialGuion.Resaltes.AvisoRadar, r);
                GUI.Label(new Rect(r.x + u * 0.8f, r.y + u * 0.45f, r.width - u * 1.6f, r.height), "RADAR · Horda grande hacia Sagunto. Llegará esta noche.", _line);
            }
            VigilarViaje();
            if (ObjetivoVisible)
            {
                var wn = NetSession.A; int nul = wn != null ? TrenPreparar.Nodo(wn, "Nules") : -1;
                string txt = "Objetivo · " + ObjetivoViaje.Titulo + ": " + Viaje.Texto(wn != null && nul >= 0 && wn.Besieged(nul)).Replace("{eta}", TrenPanel.EtaTexto());
                var r = new Rect(8, Ancla(), Mathf.Min(640f, Screen.width * 0.42f), u * 2.4f);
                GUI.DrawTexture(r, _panel); Frame(r, 0.5f);
                GUI.Label(new Rect(r.x + u * 0.7f, r.y + u * 0.4f, r.width, r.height), txt, _line);
            }
        }

        void OnGUI()
        {
            if (_m == null || IntroVista.Mostrando) return;
            GUI.depth = -100;                 // por encima del resto de la interfaz
            Styles(); MapTargets(); Avisos();
            float t = Time.unscaledTime;
            if (_m.Visible)
            {
                foreach (var k in _m.Resalta) if (_obj.TryGetValue(k, out var o) && Time.frameCount - o.frame <= 2) Ring(o.r, t);
                var p = _m.Actual;
                float w = Mathf.Min(640f, Screen.width * 0.42f), pad = _txt.fontSize * 0.8f, x = 8f, y = Ancla();
                if (p.Linea)
                {
                    float lh = _line.fontSize * 2f;
                    GUI.DrawTexture(new Rect(x, y, w, lh), _panel); GUI.Label(new Rect(x + pad, y + lh * 0.18f, w - 2 * pad, lh), Escrito(_maq, _m.Texto), _line);
                    y += lh + 6;
                }
                else
                {
                    float th = _txt.CalcHeight(new GUIContent(_m.Texto), w - 2 * pad);
                    float bh = p.Botones.Length > 0 ? _btn.fontSize * 2.4f + pad : 0f;
                    float h = pad + _head.fontSize * 1.6f + th + bh + pad;
                    var card = new Rect(x, y, w, h);
                    GUI.DrawTexture(card, _panel);
                    Frame(card, 0.35f + Mathf.Clamp01(_flash) * 0.65f);       // al salir o al repetirse, el borde se enciende un momento
                    GUI.Label(new Rect(x + pad, y + pad * 0.6f, w - 2 * pad, _head.fontSize * 1.8f), _m.Cabecera, _head);
                    GUI.Label(new Rect(x + pad, y + pad * 0.6f + _head.fontSize * 1.6f, w - 2 * pad, th + 4), Escrito(_maq, _m.Texto), _txt);
                    var ev = Event.current;                                                                     // clic en la tarjeta con el texto a medias: se completa
                    if (ev.type == EventType.MouseDown && ev.button == 0 && card.Contains(ev.mousePosition) && _maq != null && !_maq.Completa) { _maq.Completar(_snd); ev.Use(); }
                    float bx = x + pad;
                    foreach (var b in p.Botones)
                    {
                        float bw = _btn.CalcSize(new GUIContent(b)).x + _btn.fontSize * 2f;
                        if (GUI.Button(new Rect(bx, y + h - pad - _btn.fontSize * 2.4f, bw, _btn.fontSize * 2.4f), b, _btn)) { Sfx.Play("sfx_ui_clic"); _m.Senal("boton:" + b); }
                        bx += bw + pad;
                    }
                    y += h + 6;
                }
                if (_m.SueltaActual != null) Suelta(x, y, w);
            }
            else if (_m.SueltaActual != null) Suelta(8f, Ancla(), Mathf.Min(560f, Screen.width * 0.4f));
        }

        void Suelta(float x, float y, float w)
        {
            float pad = _txt.fontSize * 0.8f, th = _txt.CalcHeight(new GUIContent(_m.SueltaActual.Texto), w - 2 * pad);
            var r = new Rect(x, y, w, th + 2 * pad);
            GUI.DrawTexture(r, _panel); Frame(r, 0.6f);
            GUI.Label(new Rect(x + pad, y + pad, w - 2 * pad, th + 4), Escrito(_maqS, _m.SueltaActual.Texto), _txt);
        }
    }
}
