using System.Collections.Generic;
using UnityEngine;

namespace TBD.View.UiNoche
{
    /// <summary>
    /// Kit IMGUI para pintar las piezas de Bruno (Resources/Icons/noche) a escala entera (D: x3 a 1080p).
    /// Todo se pinta en la base de 640x360: Begin() pone GUI.matrix con la escala entera más grande que cabe y centra.
    /// 9-slice propio en 9 trozos (los bordes salen de los JSON de cada pieza, copiados aquí en Slices).
    /// Tiras de fotogramas: horizontales salvo las marcadas en TirasVerticales.
    /// SIN PROBAR EN UNITY (escrito sin editor, 7-nov 01:15): si algo no compila, avisa a Bruno.
    /// </summary>
    public static class UiNocheKit
    {
        public const int BaseW = 640, BaseH = 360;
        public static int Escala { get; private set; } = 1;
        public static Vector2 Origen { get; private set; }

        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static GUIStyle _texto;
        static Matrix4x4 _matrizPrevia, _matrizTexto;

        // l, t, r, b en píxeles de la textura (de ui_interfaz.json, ui_menu.json, ui_parte_panel.json, ui_frente_fila.json...)
        static readonly Dictionary<string, RectOffset> Slices = new Dictionary<string, RectOffset>
        {
            { "ui_panel", new RectOffset(4, 4, 4, 4) },
            { "ui_recurso_slot", new RectOffset(4, 4, 4, 4) },
            { "ui_alerta_ataque", new RectOffset(4, 4, 4, 4) },
            { "ui_alerta_aviso", new RectOffset(4, 4, 4, 4) },
            { "ui_alerta_info", new RectOffset(4, 4, 4, 4) },
            { "ui_frente_fila", new RectOffset(4, 4, 4, 4) },
            { "ui_tarjeta_unidad", new RectOffset(3, 3, 3, 3) },
            { "ui_tarjeta_unidad_sel", new RectOffset(3, 3, 3, 3) },
            { "ui_boton_normal", new RectOffset(4, 4, 4, 4) },
            { "ui_boton_encima", new RectOffset(4, 4, 4, 4) },
            { "ui_boton_pulsado", new RectOffset(4, 4, 4, 4) },
            { "ui_boton_inactivo", new RectOffset(4, 4, 4, 4) },
            { "ui_cartel_oleada", new RectOffset(6, 6, 6, 6) },
            { "ui_aviso_oleada_placa", new RectOffset(4, 4, 4, 4) },
            { "ui_rotulo_inicio", new RectOffset(9, 9, 9, 9) },
            { "ui_parte_panel", new RectOffset(4, 4, 38, 4) },           // RectOffset(left, right, top, bottom)
            { "ui_parte_panel_derrota", new RectOffset(4, 4, 38, 4) },
            { "ui_parte_franja", new RectOffset(3, 3, 3, 3) },
            { "ui_aviso_oleada_urgente", new RectOffset(4, 4, 4, 4) },
            { "ui_parte_barra_fondo", new RectOffset(2, 2, 2, 2) },
            { "ui_menu_boton_normal", new RectOffset(10, 3, 3, 3) },
            { "ui_menu_boton_encima", new RectOffset(10, 3, 3, 3) },
            { "ui_menu_boton_pulsado", new RectOffset(10, 3, 3, 3) },
            { "ui_menu_boton_inactivo", new RectOffset(10, 3, 3, 3) },
            { "ui_menu_panel", new RectOffset(4, 4, 4, 4) },
            { "ui_eta_placa", new RectOffset(3, 3, 3, 3) },
            { "ui_mapa_etiqueta", new RectOffset(3, 3, 3, 3) },
        };

        static readonly HashSet<string> TirasVerticales = new HashSet<string> { "ui_gravedad_3" };

        public static Texture2D Tex(string nombre)
        {
            if (_tex.TryGetValue(nombre, out var t)) return t;
            t = Resources.Load<Texture2D>(nombre.Contains("/") ? "Icons/" + nombre : "Icons/noche/" + nombre);   // «ordenes/vm_...» lee de otra carpeta de Icons
            if (t != null) { t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; }
            else Debug.LogWarning("[UiNoche] falta la pieza " + nombre);
            _tex[nombre] = t;
            return t;
        }

        /// <summary>Empieza a pintar en la base de 640x360. Llamar dentro de OnGUI y cerrar con End().</summary>
        public static void Begin()
        {
            Escala = Mathf.Max(1, Mathf.Min(Screen.width / BaseW, Screen.height / BaseH));
            Origen = new Vector2((Screen.width - BaseW * Escala) * .5f, (Screen.height - BaseH * Escala) * .5f);
            _matrizPrevia = GUI.matrix;
            var o = new Vector3(Mathf.Round(Origen.x), Mathf.Round(Origen.y), 0);
            _matrizTexto = _matrizPrevia * Matrix4x4.TRS(o, Quaternion.identity, Vector3.one);
            GUI.matrix = _matrizPrevia * Matrix4x4.TRS(o, Quaternion.identity, new Vector3(Escala, Escala, 1));
        }

        public static void End() { GUI.matrix = _matrizPrevia; }

        /// <summary>Ratón en coordenadas de la base (para encima y pulsado).</summary>
        public static Vector2 Raton()
        {
            var m = Event.current != null ? Event.current.mousePosition : Vector2.zero;   // ya viene transformado por GUI.matrix
            return m;
        }

        public static void Pieza(string nombre, float x, float y)
        {
            var t = Tex(nombre); if (t == null) return;
            GUI.DrawTexture(new Rect(x, y, t.width, t.height), t);
        }

        public static void Estirada(string nombre, Rect r)
        {
            var t = Tex(nombre); if (t == null || r.width <= 0 || r.height <= 0) return;
            GUI.DrawTexture(r, t, ScaleMode.StretchToFill);
        }

        /// <summary>Recorte horizontal de una pieza (p. ej. el relleno de la barra de la noche a progreso × ancho).</summary>
        public static void Recorte(string nombre, float x, float y, float fraccion)
        {
            var t = Tex(nombre); if (t == null) return;
            fraccion = Mathf.Clamp01(fraccion);
            int w = Mathf.RoundToInt(t.width * fraccion); if (w <= 0) return;
            GUI.DrawTextureWithTexCoords(new Rect(x, y, w, t.height), t, new Rect(0, 0, (float)w / t.width, 1));
        }

        public static void Fotograma(string nombre, float x, float y, int fotogramas, int f)
        {
            var t = Tex(nombre); if (t == null) return;
            f = ((f % fotogramas) + fotogramas) % fotogramas;
            if (TirasVerticales.Contains(nombre))
            {
                float h = (float)t.height / fotogramas;
                GUI.DrawTextureWithTexCoords(new Rect(x, y, t.width, h), t, new Rect(0, 1f - (f + 1f) / fotogramas, 1, 1f / fotogramas));
            }
            else
            {
                float w = (float)t.width / fotogramas;
                GUI.DrawTextureWithTexCoords(new Rect(x, y, w, t.height), t, new Rect((float)f / fotogramas, 0, 1f / fotogramas, 1));
            }
        }

        public static int FotogramaPorTiempo(float t, int ms, int fotogramas, bool bucle = true)
        {
            int f = Mathf.FloorToInt(t * 1000f / ms);
            return bucle ? f % fotogramas : Mathf.Min(f, fotogramas - 1);
        }

        /// <summary>
        /// 9-slice propio con GUI.DrawTextureWithTexCoords (9 trozos), en vez de GUIStyle.border:
        /// así sirve también para un fotograma de una tira (ui_aviso_oleada_urgente, 2 f) y los bordes salen exactos en píxeles.
        /// </summary>
        public static void Slice(string nombre, Rect r, int fotogramas = 1, int f = 0)
        {
            var t = Tex(nombre); if (t == null || r.width <= 0 || r.height <= 0) return;
            RectOffset b;
            if (!Slices.TryGetValue(nombre, out b)) b = new RectOffset(4, 4, 4, 4);
            fotogramas = Mathf.Max(1, fotogramas);
            f = ((f % fotogramas) + fotogramas) % fotogramas;
            float W = t.width, H = t.height, fw = W / fotogramas, u0 = f * fw;
            // si el rectángulo es más pequeño que los bordes, se reparten a partes iguales
            float l = Mathf.Min(b.left, r.width * .5f), rr = Mathf.Min(b.right, r.width * .5f);
            float tp = Mathf.Min(b.top, r.height * .5f), bt = Mathf.Min(b.bottom, r.height * .5f);
            float[] dx = { r.x, r.x + l, r.xMax - rr, r.xMax };
            float[] sx = { 0, l, fw - rr, fw };
            float[] dy = { r.y, r.y + tp, r.yMax - bt, r.yMax };
            float[] sy = { 0, tp, H - bt, H };   // píxeles desde arriba de la textura
            for (int j = 0; j < 3; j++)
                for (int i = 0; i < 3; i++)
                {
                    float w = dx[i + 1] - dx[i], h = dy[j + 1] - dy[j];
                    float sw = sx[i + 1] - sx[i], sh = sy[j + 1] - sy[j];
                    if (w <= 0 || h <= 0 || sw <= 0 || sh <= 0) continue;
                    var uv = new Rect((u0 + sx[i]) / W, 1f - sy[j + 1] / H, sw / W, sh / H);   // UV de Unity: v = 0 abajo
                    GUI.DrawTextureWithTexCoords(new Rect(dx[i], dy[j], w, h), t, uv);
                }
        }

        public static readonly Color Hueso = Hex(0xd4d0cc), Crema = Hex(0xf0dc9c), Beige = Hex(0xb4a080), OlivaGris = Hex(0x605c48), Ambar = Hex(0xe08c30);

        public static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        /// <summary>
        /// Texto en la base. tam = alto en píxeles de la base (9 u 11 en las maquetas). Letra por defecto de Unity.
        /// Se pinta a tamaño de pantalla (tam × Escala) quitando la escala de GUI.matrix, para que no salga borroso al escalar.
        /// </summary>
        public static void Texto(float x, float y, string s, Color c, int tam = 9, TextAnchor ancla = TextAnchor.UpperLeft, float ancho = 0)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (_texto == null) _texto = new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), wordWrap = false, clipping = TextClipping.Overflow };
            int e = Escala;
            _texto.fontSize = tam * e;
            _texto.alignment = ancla;
            _texto.normal.textColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.gamma : c;   // IMGUI en Linear pasa el color a lineal sin recodificar: sale oscuro (Bruno, qa13-15)
            float w = ancho > 0 ? ancho : 400;
            float xx = ancla == TextAnchor.UpperCenter || ancla == TextAnchor.MiddleCenter ? x - w * .5f : ancla == TextAnchor.UpperRight ? x - w : x;
            var m = GUI.matrix;
            GUI.matrix = _matrizTexto;
            GUI.Label(new Rect(xx * e, (y - 1) * e, w * e, (tam + 4) * e), s, _texto);
            GUI.matrix = m;
        }

        /// <summary>Velo tramado en navy (ui_velo_navy_4x4 en mosaico), detrás del panel de Opciones y de la derrota.</summary>
        public static void Velo()
        {
            var t = Tex("ui_velo_navy_4x4"); if (t == null) return;
            t.wrapMode = TextureWrapMode.Repeat;
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, BaseW, BaseH), t, new Rect(0, 0, BaseW / 4f, BaseH / 4f));
        }
    }
}
