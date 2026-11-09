using TBD.Net;
using TBD.Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TBD.View
{
    /// <summary>
    /// Interfaz v3 (D-N185, aprobada por Eugenio el 9-nov). Primer paso: en el mapa de operaciones, la franja superior (dónde y cuándo,
    /// el objetivo del día 2 con sus casillas, y la velocidad), la barra de ayuda sin depuración y la leyenda del mapa (L).
    /// Va en UI Toolkit con el tema común (Resources/UI3/tema.uss). Hoy SOLO se enciende con <c>-ui3</c>: sin la bandera el juego
    /// no cambia nada. Es solo vista: lee NetSession.A, no toca la simulación ni el hash. Todo con pickingMode Ignore (no come clics).
    /// Textos: los del objetivo del día 2 v2 (docs/informe-textos.md), sin cambiar.
    /// </summary>
    public sealed class Ui3 : MonoBehaviour
    {
        /// <summary>¿Interfaz v3 encendida? (bandera -ui3). El resto del código la mira para no pintar la pieza vieja que sustituye.</summary>
        public static readonly bool Activa = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-ui3") >= 0;

        static Ui3 _yo;
        UIDocument _doc;
        VisualElement _raiz, _franja, _ayuda, _leyenda, _objBloque;
        Label _donde, _reloj, _objTitulo;
        Label[] _vel;
        VisualElement _chkTropa, _chkMun;
        Label _txtTropa, _txtMun;
        bool _leyendaVisible = true;
        OperationsMap3D _mapa;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Activa || _yo != null) return;
            var go = new GameObject("TBD_Ui3");
            DontDestroyOnLoad(go);
            _yo = go.AddComponent<Ui3>();
        }

        void Awake()
        {
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.sortingOrder = 10;
            ps.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            ps.clearColor = false;

            gameObject.SetActive(false);          // el UIDocument necesita el PanelSettings antes de activarse
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = ps;
            gameObject.SetActive(true);

            var tema = Resources.Load<StyleSheet>("UI3/tema");
            if (tema == null) Debug.LogWarning("[UI3] no encuentro Resources/UI3/tema.uss");
            var root = _doc.rootVisualElement;
            if (tema != null) root.styleSheets.Add(tema);
            root.pickingMode = PickingMode.Ignore;
            Construir(root);
            Debug.Log("[UI3] interfaz v3 encendida (-ui3)");
        }

        // ---------------------------------------------------------------- construcción
        static Label L(string txt, params string[] clases)
        {
            var l = new Label(txt); l.pickingMode = PickingMode.Ignore;
            foreach (var c in clases) l.AddToClassList(c);
            return l;
        }

        static VisualElement E(params string[] clases)
        {
            var e = new VisualElement(); e.pickingMode = PickingMode.Ignore;
            foreach (var c in clases) e.AddToClassList(c);
            return e;
        }

        void Construir(VisualElement root)
        {
            _raiz = E("raiz"); root.Add(_raiz);

            // franja superior
            _franja = E("franja"); _raiz.Add(_franja);
            var izq = E("blk"); _franja.Add(izq);
            var col = E("col"); izq.Add(col);
            _donde = L("Red · día 1", "lbl"); col.Add(_donde);
            _reloj = L("00:00", "reloj"); col.Add(_reloj);
            var seg = E("seg"); izq.Add(seg);
            _vel = new[] { L("||", "seg-op"), L("×1", "seg-op"), L("×2", "seg-op"), L("×4", "seg-op") };
            foreach (var v in _vel) seg.Add(v);

            _objBloque = E("blk", "blk-centro"); _franja.Add(_objBloque);
            _objBloque.Add(L("OBJETIVO · DÍA 2", "lbl", "lbl-amb"));
            _objTitulo = L("Defiende Nules", "titulo"); _objBloque.Add(_objTitulo);
            _chkTropa = Casilla("Un pelotón en Nules", out _txtTropa); _objBloque.Add(_chkTropa);
            _chkMun = Casilla("Munición en Nules: 0/20 u", out _txtMun); _objBloque.Add(_chkMun);

            var dcha = E("blk", "blk-dcha"); _franja.Add(dcha);
            dcha.Add(L("Tab", "tecla")); dcha.Add(L("INFORME", "lbl"));
            dcha.Add(L("Esc", "tecla")); dcha.Add(L("MENÚ", "lbl"));

            // barra de ayuda: solo lo que sirve para jugar (la depuración sale con -depuracion en la vieja)
            _ayuda = E("ayuda"); _raiz.Add(_ayuda);
            Atajo("WASD", "mover el mapa"); Atajo("Q E", "girar"); Atajo("Rueda", "acercar (a menos de 2 km entras en la estación)");
            Atajo("J", "ir a la alerta"); Atajo("Tab", "informe"); Atajo("1 2 3", "velocidad"); Atajo("Espacio", "pausa"); Atajo("L", "leyenda");

            // leyenda del mapa
            _leyenda = E("leyenda"); _raiz.Add(_leyenda);
            var cab = E("ley-cab"); _leyenda.Add(cab);
            cab.Add(L("LEYENDA", "lbl")); cab.Add(L("L", "tecla"));
            FilaLey("sim-disco", "Tu estación");
            FilaLey("sim-aro-amb", "Cercada");
            FilaLey("sim-aro-rojo", "Bajo ataque (parpadea)");
            FilaLey("sim-otra", "Otra estación (no es tuya)");
            FilaIcono(IconosMapa.Icono.TrenMarcha, "Tren en marcha (la punta, hacia dónde)");
            FilaIcono(IconosMapa.Icono.TrenParado, "Tren parado");
            FilaIcono(IconosMapa.Icono.Helicoptero, "Helicóptero NH90");
            FilaLey("sim-horda", "Horda");
            FilaIcono(IconosMapa.Icono.FlechaPunta, "Horda en marcha y hacia dónde");
        }

        VisualElement Casilla(string txt, out Label etiqueta)
        {
            var c = E("chk"); c.Add(E("chk-caja"));
            etiqueta = L(txt, "chk-txt"); c.Add(etiqueta);
            return c;
        }

        void Atajo(string tecla, string que)
        {
            var it = E("ayuda-it"); it.Add(L(tecla, "tecla")); it.Add(L(que, "ayuda-txt")); _ayuda.Add(it);
        }

        void FilaLey(string sim, string txt)
        {
            var f = E("ley-fila"); var s = E("ley-sim"); s.Add(E(sim)); f.Add(s); f.Add(L(txt, "ley-txt")); _leyenda.Add(f);
        }

        /// <summary>Fila de la leyenda con una silueta de Bruno (IconosMapa, por código, 32 px en filtro Point).</summary>
        void FilaIcono(IconosMapa.Icono icono, string txt)
        {
            var f = E("ley-fila"); var s = E("ley-sim");
            var img = E(); img.style.width = 28; img.style.height = 28;
            var tex = IconosMapa.Textura(icono, 32);
            if (tex != null) img.style.backgroundImage = new StyleBackground(tex);
            s.Add(img); f.Add(s); f.Add(L(txt, "ley-txt")); _leyenda.Add(f);
        }

        // ---------------------------------------------------------------- cada fotograma
        static void Ver(VisualElement e, bool si) { var d = si ? DisplayStyle.Flex : DisplayStyle.None; if (e.style.display != d) e.style.display = d; }

        void Update()
        {
            var w = NetSession.A;
            bool enMapa = SceneManager.GetActiveScene().name == "Union" && !IntroVista.Mostrando && w != null;
            if (enMapa && (_mapa == null || !_mapa)) _mapa = FindFirstObjectByType<OperationsMap3D>();
            enMapa &= _mapa != null && _mapa.isActiveAndEnabled;
            Ver(_raiz, enMapa);
            if (!enMapa) return;

            var kb = Keyboard.current;
            if (kb != null && kb.lKey.wasPressedThisFrame) _leyendaVisible = !_leyendaVisible;
            Ver(_leyenda, _leyendaVisible && !TrenPanel.PanelAbierto);      // con «Preparar tren» abierto la leyenda se aparta

            _donde.text = "RED · DÍA " + (w.Tick / NetConst.DayTicks + 1);
            _reloj.text = NetConst.Clock(w.Tick);
            int on = w.Paused ? 0 : w.Speed >= 4 ? 3 : w.Speed == 2 ? 2 : 1;
            for (int i = 0; i < _vel.Length; i++) _vel[i].EnableInClassList("seg-op--on", i == on);

            // objetivo del día 2 v2 (mismos datos que el recuadro viejo de TutorialHud)
            int nul = TrenPreparar.Nodo(w, "Nules");
            bool dia2 = nul >= 0 && w.Tick >= ObjetivoDefensa.Desde && w.Tick < ObjetivoDefensa.Hasta && !w.NodeLost[nul];
            Ver(_objBloque, dia2);
            if (dia2)
            {
                int mun = w.Stock[nul, (int)Res.Municion]; bool tropa = w.ObjTroops;
                _chkTropa.EnableInClassList("chk--ok", tropa);
                _chkMun.EnableInClassList("chk--ok", mun >= ObjetivoDefensa.MunicionMin);
                _txtMun.text = $"Munición en Nules: {System.Math.Min(mun, 999)}/{ObjetivoDefensa.MunicionMin} u";
                _objTitulo.text = tropa && mun >= ObjetivoDefensa.MunicionMin ? "Nules está lista para la noche." : "Defiende Nules";
            }
        }
    }
}
