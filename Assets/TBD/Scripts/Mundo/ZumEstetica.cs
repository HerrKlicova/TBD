using System.Collections.Generic;
using UnityEngine;

namespace TBD.Mundo
{
    /// <summary>
    /// Estética del zum (Hito 1, núcleo v2) · Bruno (Arte técnico), 11-11. Lo que Marcos enchufa en MundoBootstrap para que el mundo se vea como
    /// las láminas («Estética del zum», Art/Codigo/zum/): el suelo con la paleta de 32 por uso del suelo y el fundido Bayer MAPA↔COMARCA
    /// (TBD/ZumSuelo), la calima de día sobre toda la imagen (TBD/ZumPantalla), los iconos por tamaño en pantalla (TBD/ZumIcono) y el trazado
    /// de vía (TBD/ZumTrazado). Solo VISTA: no toca la simulación. Los shaders están en Resources/Zum, así que entran en la build sin tocar
    /// «Always Included Shaders». Si un shader no carga, cada método devuelve null o cae a lo de antes (GUI.DrawTexture) y avisa una vez.
    /// Uso y propiedades: Art/Codigo/zum/LEEME_shaders.md. SIN COMPILAR: escrito en la nube, sin Unity.
    /// </summary>
    public static class ZumEstetica
    {
        // --- Niveles (altura de la cámara, m) y fundidos ----------------------------------------------------------------------
        public const float Soldado = 6f, Pueblo = 40f, Comarca = 250f, Mapa = 900f, Maximo = 4000f;
        public const float Fundido = 0.25f;                // cada cambio de nivel se funde a lo largo de ±25 % del umbral
        public const float TiempoTramado = 0.3f;           // un icono entra o sale tramándose en 0,3 s
        public const float IconoUmbralPx = 10f;            // una unidad pasa a icono cuando su modelo mide menos de 10 px (en pantalla de 720)

        public enum Nivel { Soldado, Pueblo, Comarca, Mapa }

        public static Nivel NivelDe(float altura) =>
            altura < Pueblo ? Nivel.Soldado : altura < Comarca ? Nivel.Pueblo : altura < Mapa ? Nivel.Comarca : Nivel.Mapa;

        /// <summary>La cámara solo gira en SOLDADO y PUEBLO; al subir a COMARCA vuelve al norte arriba en 0,4 s (225 °/s como mucho).</summary>
        public static bool PuedeGirar(float altura) => altura < Comarca;

        /// <summary>Fracción de píxeles que ya dibujan el plano táctico (0 = todo suelo de día, 1 = todo mapa). La usa el shader del suelo.</summary>
        public static float MezclaMapa(float altura) => Mathf.Clamp01((altura - Mapa * (1f - Fundido)) / (Mapa * 2f * Fundido));

        // --- Colores ---------------------------------------------------------------------------------------------------------
        public static readonly Color32 Contorno = new Color32(0x0f, 0x12, 0x0c, 255);
        public static readonly Color32 Hueso = new Color32(0xd4, 0xd0, 0xcc, 255);          // iconos, placas y trazado válido
        public static readonly Color32 Ambar = new Color32(0xe0, 0x8c, 0x30, 255);          // aviso: trazado inválido, munición < 25 %
        public static readonly Color32 PlacaFondo = new Color32(0x30, 0x34, 0x24, 255);     // olive_shadow
        public static readonly Color32 Horda = new Color32(0x98, 0x24, 0x20, 255);          // mancha roja tramada desde COMARCA
        public static readonly Color32 CalimaColor = new Color32(0xa2, 0x7e, 0x5a, 255);    // D-N197
        /// <summary>Un color por línea de tren (Lucía, 11-11: fuera de la paleta de 32, SOLO para interfaz y tintes de línea; nunca rojo ni ámbar).</summary>
        public static readonly Color32[] Lineas =
        {
            new Color32(0xf0, 0xdc, 0x9c, 255), new Color32(0x8c, 0xc8, 0xe8, 255), new Color32(0x7c, 0xd4, 0xa8, 255), new Color32(0xd0, 0xb4, 0xf0, 255),
        };
        public const float TrazadoValidoTramado = 0.55f, TrazadoTendiendoTramado = 0.30f, TrazadoInvalidoAncho = 6f;

        // --- Calima ----------------------------------------------------------------------------------------------------------
        /// <summary>Calima de día: 0 % en SOLDADO, 10 % en PUEBLO, 22 % en COMARCA y nada en MAPA, con rampas en el ±25 % de cada umbral.</summary>
        public static float CalimaDe(float altura)
        {
            float a = Rampa(altura, Pueblo, 0f, 0.10f);
            if (altura >= Comarca * (1f - Fundido)) a = Rampa(altura, Comarca, 0.10f, 0.22f);
            return a * (1f - MezclaMapa(altura));          // los píxeles que ya son mapa no llevan calima
        }

        static float Rampa(float altura, float umbral, float abajo, float arriba) =>
            Mathf.Lerp(abajo, arriba, Mathf.Clamp01((altura - umbral * (1f - Fundido)) / (umbral * 2f * Fundido)));

        // --- Iconos ----------------------------------------------------------------------------------------------------------
        /// <summary>Tamaño del icono en la pantalla de las láminas (720 de alto): 32 px en MAPA y COMARCA, 16 en PUEBLO, 0 en SOLDADO.</summary>
        public static int IconoPx720(float altura) => altura >= Comarca ? 32 : altura >= Pueblo ? 16 : 0;

        /// <summary>El mismo tamaño en píxeles de pantalla reales, en múltiplos enteros del dibujo de 16 px (para que no se emborrone).</summary>
        public static int IconoPxPantalla(float altura)
        {
            int b = IconoPx720(altura); if (b == 0) return 0;
            int px = Mathf.RoundToInt(b * Screen.height / 720f);
            return Mathf.Max(16, px / 16 * 16);
        }

        /// <summary>Alto en pantalla (en píxeles de una pantalla de 720) de un modelo de «alto» m con los pies en «pie». Vale con la RT de Marcos.</summary>
        public static float ModeloPx720(Camera cam, Vector3 pie, float alto)
        {
            var a = cam.WorldToViewportPoint(pie); var b = cam.WorldToViewportPoint(pie + Vector3.up * alto);
            if (a.z <= 0f || b.z <= 0f) return 0f;
            return Mathf.Abs(b.y - a.y) * 720f;
        }

        /// <summary>La regla de los iconos: lo que decide no es el nivel sino el tamaño en pantalla del modelo (menos de 10 px, icono).</summary>
        public static bool EsIcono(float modeloPx720) => modeloPx720 < IconoUmbralPx;

        /// <summary>Lleva «visible» (0–1) hacia 1 o 0 en 0,3 s. Se pasa a DibujarIcono como tramado.</summary>
        public static float Tramar(float visible, bool mostrar, float dt) => Mathf.MoveTowards(visible, mostrar ? 1f : 0f, dt / TiempoTramado);

        // Dibujos de 16 × 16 (los de IconosMapa más los nuevos del núcleo v2): «#» relleno, «o» hueco en color de contorno, «.» vacío.
        static readonly Dictionary<string, string[]> Dibujos = new Dictionary<string, string[]>
        {
            ["tren"] = new[] { "................", "................", "................", "................",
                               ".......###......", ".......#o#..#...", ".#########..##..", ".#o#o#o###..###.",
                               ".#########..##..", "..##.##.##..#...", "................", "................",
                               "................", "................", "................", "................" },
            ["tren_parado"] = new[] { "................", "................", "................", "................",
                                      "................", "...........###..", "...........#o#..", ".#############..",
                                      ".#o#o#o#o#####..", ".#############..", "..##.##...##.##.", "................",
                                      "................", "................", "................", "................" },
            ["soldado"] = new[] { "................", "......##........", ".....####.......", "......##........",
                                  ".....####.......", "....######......", "....#.##.##.....", "....#.##..##....",
                                  "......##...#....", ".....####.......", ".....#..#.......", ".....#..#.......",
                                  "....##..##......", "................", "................", "................" },
            ["estacion"] = new[] { "................", "................", ".....######.....", "....########....",
                                   "...##########...", "...##########...", "...##########...", "...##########...",
                                   "...##########...", "...##########...", "....########....", ".....######.....",
                                   "................", "................", "................", "................" },
            ["cuadrilla"] = new[] { "................", ".....##...#.....", "....####.##.....", ".....##.##......",
                                    "....#####.......", "...######.......", "...#.##.#.......", "...#.##.#.......",
                                    ".....##.........", "....####........", "....#..#........", "....#..#........",
                                    "...##..##.......", "................", "................", "................" },
            ["deposito"] = new[] { "................", "................", "................", "................",
                                   "................", "..############..", "..#o#o#o#o#o##..", "..############..",
                                   "...##########...", "...#o#o#o#o##...", "...##########...", "....########....",
                                   "....#o#o#o##....", "....########....", "................", "................" },
        };

        static readonly Dictionary<(string, int, int), Texture2D> _iconos = new Dictionary<(string, int, int), Texture2D>();

        /// <summary>Textura del icono a «px» de lado (múltiplo de 16), girada «giro» cuartos de vuelta a la izquierda (el tren, con su rumbo).
        /// R = 1 relleno, R = 0 contorno; A = máscara. Contorno de 1 px alrededor, como en las láminas.</summary>
        public static Texture2D Icono(string nombre, int px = 32, int giro = 0)
        {
            px = Mathf.Max(16, px / 16 * 16); giro = ((giro % 4) + 4) % 4;
            if (_iconos.TryGetValue((nombre, px, giro), out var hecha) && hecha != null) return hecha;
            if (!Dibujos.TryGetValue(nombre, out var filas)) return null;
            int e = px / 16; var m = new byte[px, px];                       // [fila desde arriba, columna]: 0 vacío, 1 relleno, 2 hueco
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                char c = filas[y][x]; if (c == '.') continue;
                for (int a = 0; a < e; a++) for (int b = 0; b < e; b++) m[y * e + a, x * e + b] = (byte)(c == 'o' ? 2 : 1);
            }
            for (int g = 0; g < giro; g++)                                     // un cuarto de vuelta a la izquierda (como np.rot90)
            {
                var r = new byte[px, px];
                for (int y = 0; y < px; y++) for (int x = 0; x < px; x++) r[px - 1 - x, y] = m[y, x];
                m = r;
            }
            var col = new Color32[px * px];
            for (int y = 0; y < px; y++) for (int x = 0; x < px; x++)
            {
                byte v = m[y, x]; bool borde = false;
                if (v == 0)
                    for (int dy = -1; dy <= 1 && !borde; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int yy = y + dy, xx = x + dx;
                        if (yy >= 0 && yy < px && xx >= 0 && xx < px && m[yy, xx] != 0) { borde = true; break; }
                    }
                var c = v == 1 ? new Color32(255, 255, 255, 255) : (v == 2 || borde) ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
                col[(px - 1 - y) * px + x] = c;                                 // Texture2D: la fila 0 es la de abajo
            }
            var t = new Texture2D(px, px, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = $"Zum icono {nombre} {px}" };
            t.SetPixels32(col); t.Apply(false, true);
            _iconos[(nombre, px, giro)] = t;
            return t;
        }

        /// <summary>Dibuja un icono centrado en «centro» (píxeles de pantalla de OnGUI, y hacia abajo). Solo en el Repaint.</summary>
        public static void DibujarIcono(Vector2 centro, string nombre, int px, Color relleno, float visible = 1f, int giro = 0)
        {
            if (px <= 0 || visible <= 0f || Event.current == null || Event.current.type != EventType.Repaint) return;
            var tex = Icono(nombre, px, giro); if (tex == null) return;
            var r = new Rect(Mathf.Round(centro.x - px / 2f), Mathf.Round(centro.y - px / 2f), px, px);
            var m = Mat(ref _matIcono, "ZumIcono");
            if (m == null) { GUI.DrawTexture(r, tex); return; }
            m.SetColor(IdRelleno, relleno); m.SetColor(IdContorno, Contorno); m.SetFloat(IdVisible, visible);
            Graphics.DrawTexture(r, tex, m);
        }

        // --- Materiales ------------------------------------------------------------------------------------------------------
        static readonly int IdAltura = Shader.PropertyToID("_ZumAltura"), IdUsos = Shader.PropertyToID("_Usos"), IdUsoFijo = Shader.PropertyToID("_UsoFijo");
        static readonly int IdCalima = Shader.PropertyToID("_Calima"), IdCalimaColor = Shader.PropertyToID("_CalimaColor");
        static readonly int IdRelleno = Shader.PropertyToID("_Relleno"), IdContorno = Shader.PropertyToID("_Contorno"), IdVisible = Shader.PropertyToID("_Visible");
        static readonly int IdColor = Shader.PropertyToID("_Color"), IdTramado = Shader.PropertyToID("_Tramado"), IdGuion = Shader.PropertyToID("_Guion");
        static readonly int IdRelieve = Shader.PropertyToID("_Relieve"), IdNaranjos = Shader.PropertyToID("_NaranjosPintados");
        static Material _matPantalla, _matIcono;
        static readonly HashSet<string> _avisados = new HashSet<string>();

        /// <summary>Carga un shader de Resources/Zum (o por nombre, «TBD/…»). Null si no está (y avisa una vez).</summary>
        public static Shader Cargar(string nombre)
        {
            var sh = Resources.Load<Shader>("Zum/" + nombre);
            if (sh == null) sh = Shader.Find("TBD/" + nombre);
            if (sh == null || !sh.isSupported)
            {
                if (_avisados.Add(nombre)) Debug.LogWarning($"[ZUM] no carga el shader TBD/{nombre} (¿error de compilación? mira la consola): se queda lo de antes");
                return null;
            }
            return sh;
        }

        static Material Mat(ref Material m, string shader)
        {
            if (m != null) return m;
            var sh = Cargar(shader); if (sh == null) return null;
            m = new Material(sh) { name = "Zum " + shader, hideFlags = HideFlags.DontSave };
            return m;
        }

        /// <summary>Textura de usos del suelo para el shader: R8 LINEAL, sin filtro, un texel por metro. «px» es el PNG de Álvaro tal como lo deja
        /// LoadImage (fila 0 al sur, el id en R). Hay que pasarla así: si se pasa el PNG tal cual, Unity lo lee en sRGB y los ids salen mal.</summary>
        public static Texture2D TexturaUsos(Color32[] px, int w, int h)
        {
            var b = new byte[w * h];
            for (int i = 0; i < b.Length; i++) b[i] = px[i].r;
            var t = new Texture2D(w, h, TextureFormat.R8, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Zum usos del suelo" };
            t.SetPixelData(b, 0); t.Apply(false, true);
            return t;
        }

        /// <summary>Material del terreno (las mallas de 128 m). «naranjos3D»: true si ya hay naranjos de verdad (no se pintan copas en el suelo).
        /// Null si el shader no carga: entonces se queda el URP/Lit de antes.</summary>
        public static Material MaterialSuelo(Texture2D usos, bool naranjos3D = true, float relieve = 0.5f)
        {
            var sh = Cargar("ZumSuelo"); if (sh == null) return null;
            var m = new Material(sh) { name = "Zum suelo" };
            m.SetTexture(IdUsos, usos); m.SetFloat(IdUsoFijo, -1f); m.SetFloat(IdNaranjos, naranjos3D ? 0f : 1f); m.SetFloat(IdRelieve, relieve);
            return m;
        }

        /// <summary>Material del plano del mar (mismo shader, uso fijo 0: navy en el mapa, slate de día).</summary>
        public static Material MaterialMar(Texture2D usos)
        {
            var m = MaterialSuelo(usos); if (m == null) return null;
            m.name = "Zum mar"; m.SetFloat(IdUsoFijo, 0f);
            return m;
        }

        /// <summary>Material de una cinta de trazado. Válido: (Hueso, 0.55, 0) y el eje (Hueso, 1, 6); inválido: (Ambar, 1, 0) sobre una cinta
        /// 2 px más ancha en Contorno; tendiéndose: (Hueso, 0.3, 0). uv.x de la malla = metros a lo largo del eje.</summary>
        public static Material MaterialTrazado(Color color, float tramado, float guionM = 0f)
        {
            var sh = Cargar("ZumTrazado"); if (sh == null) return null;
            var m = new Material(sh) { name = "Zum trazado" };
            m.SetColor(IdColor, color); m.SetFloat(IdTramado, tramado); m.SetFloat(IdGuion, guionM);
            return m;
        }

        // --- Cada frame ------------------------------------------------------------------------------------------------------
        /// <summary>Llamar en Update con la altura de la cámara: la lee el shader del suelo para el fundido MAPA↔COMARCA.</summary>
        public static void Actualizar(float altura) => Shader.SetGlobalFloat(IdAltura, altura);

        /// <summary>Sustituye a GUI.DrawTexture(rect, rt) en OnGUI: la misma imagen con la calima del nivel. Solo en el Repaint.</summary>
        public static void DibujarPantalla(Rect r, Texture rt, float altura)
        {
            if (rt == null || Event.current == null || Event.current.type != EventType.Repaint) return;
            var m = Mat(ref _matPantalla, "ZumPantalla");
            if (m == null) { GUI.DrawTexture(r, rt, ScaleMode.StretchToFill, false); return; }
            m.SetColor(IdCalimaColor, CalimaColor); m.SetFloat(IdCalima, CalimaDe(altura));
            Graphics.DrawTexture(r, rt, m);
        }
    }
}
