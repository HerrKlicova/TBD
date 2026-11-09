using System.Collections.Generic;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Dibujos del mapa de operaciones (D-N185, interfaz v3; Bruno 09-11). Siluetas a píxel generadas por código, sin PNG (desde la nube no se suben binarios):
    /// tren en marcha (con la punta del sentido delante) y parado, helicóptero NH90, soldado, estación (disco) y la flecha de avance de la horda en 982420
    /// (cuerpo y punta por separado, para estirar el cuerpo). Hueso d4d0cc con contorno 0f120c de 1 px a cualquier tamaño (16 o 32 px), filtro Point.
    /// Nada de APP-6: son dibujos que se entienden solos. La mancha tramada y el anillo de ataque NO van aquí (MapaTactico.Mancha y _anillo).
    /// Uso: IconosMapa.Textura(IconosMapa.Icono.TrenMarcha, 32), IconosMapa.Figura(...), o IconosMapa.Hoja(32) + IconosMapa.Uv(icono) para un atlas.
    /// </summary>
    public static class IconosMapa
    {
        public enum Icono { TrenMarcha, TrenParado, Helicoptero, Soldado, Estacion, FlechaCuerpo, FlechaPunta }

        public static readonly Color32 Hueso = new Color32(0xd4, 0xd0, 0xcc, 255), Contorno = new Color32(0x0f, 0x12, 0x0c, 255), Rojo = new Color32(0x98, 0x24, 0x20, 255);

        // 16 × 16, la fila 0 arriba. '#' relleno, 'o' detalle oscuro (ventanas, cabina), '.' vacío. El contorno se calcula, no se dibuja.
        static readonly string[][] Dibujos =
        {
            new[] { // TrenMarcha: locomotora con ventanas y la punta del sentido delante (mira a la derecha; girar el icono con el rumbo)
                "................", "................", "................", "................",
                ".......###......", ".......#o#..#...", ".#########..##..", ".#o#o#o###..###.",
                ".#########..##..", "..##.##.##..#...", "................", "................",
                "................", "................", "................", "................" },
            new[] { // TrenParado: sin punta y sin las rayas «II» de antes
                "................", "................", "................", "................",
                "................", "...........###..", "...........#o#..", ".#############..",
                ".#o#o#o#o#####..", ".#############..", "..##.##...##.##.", "................",
                "................", "................", "................", "................" },
            new[] { // Helicoptero: NH90 de perfil, rotor arriba, cola a la derecha
                "................", "................", "..###########...", ".......#........",
                "...######.......", "..#oo######...#.", "..#o#######..##.", "...###########..",
                "....#####.......", "....#...#.......", "...#######......", "................",
                "................", "................", "................", "................" },
            new[] { // Soldado: de pie con el fusil
                "................", "......##........", ".....####.......", "......##........",
                ".....####.......", "....######......", "....#.##.##.....", "....#.##..##....",
                "......##...#....", ".....####.......", ".....#..#.......", ".....#..#.......",
                "....##..##......", "................", "................", "................" },
            new[] { // Estacion: disco
                "................", "................", ".....######.....", "....########....",
                "...##########...", "...##########...", "...##########...", "...##########...",
                "...##########...", "...##########...", "....########....", ".....######.....",
                "................", "................", "................", "................" },
            new[] { // FlechaCuerpo: banda de 4 px de alto a todo el ancho (se estira en X)
                "................", "................", "................", "................",
                "................", "................", "################", "################",
                "################", "################", "................", "................",
                "................", "................", "................", "................" },
            new[] { // FlechaPunta: el cuerpo entra por la izquierda a la misma altura (filas 6-9)
                "................", "................", "#...............", "###.............",
                "#####...........", "#######.........", "#########.......", "##########......",
                "##########......", "#########.......", "#######.........", "#####...........",
                "###.............", "#...............", "................", "................" },
        };

        static readonly Dictionary<long, Texture2D> _tex = new Dictionary<long, Texture2D>();
        static readonly Dictionary<long, Sprite> _spr = new Dictionary<long, Sprite>();
        static readonly Dictionary<int, Texture2D> _hojas = new Dictionary<int, Texture2D>();

        public static int Cuantos => Dibujos.Length;

        /// <summary>El icono a px × px (múltiplo de 16; 16 o 32 son los que pide el mapa), con contorno de 1 px a ese tamaño.</summary>
        public static Texture2D Textura(Icono icono, int px)
        {
            px = Mathf.Max(16, px / 16 * 16);
            long k = ((long)icono << 16) | (uint)px;
            if (_tex.TryGetValue(k, out var t) && t != null) return t;
            t = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Icono " + icono + " " + px };
            t.SetPixels32(Pixeles(icono, px)); t.Apply(false, false);
            _tex[k] = t; return t;
        }

        public static Sprite Figura(Icono icono, int px)
        {
            px = Mathf.Max(16, px / 16 * 16);
            long k = ((long)icono << 16) | (uint)px;
            if (_spr.TryGetValue(k, out var s) && s != null) return s;
            var t = Textura(icono, px);
            s = UnityEngine.Sprite.Create(t, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px, 0, SpriteMeshType.FullRect); s.name = t.name;
            _spr[k] = s; return s;
        }

        /// <summary>Atlas en una fila: los iconos en el orden del enum, cada uno de px × px. Para la leyenda de UI Toolkit.</summary>
        public static Texture2D Hoja(int px)
        {
            px = Mathf.Max(16, px / 16 * 16);
            if (_hojas.TryGetValue(px, out var h) && h != null) return h;
            int n = Dibujos.Length;
            h = new Texture2D(px * n, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Hoja de iconos del mapa " + px };
            var todo = new Color32[px * n * px];
            for (int i = 0; i < n; i++)
            {
                var p = Pixeles((Icono)i, px);
                for (int y = 0; y < px; y++) System.Array.Copy(p, y * px, todo, y * px * n + i * px, px);
            }
            h.SetPixels32(todo); h.Apply(false, false);
            _hojas[px] = h; return h;
        }

        /// <summary>Rectángulo UV del icono dentro de Hoja(px) (el mismo a cualquier tamaño).</summary>
        public static Rect Uv(Icono icono) { int n = Dibujos.Length; return new Rect((float)icono / n, 0f, 1f / n, 1f); }

        static Color32[] Pixeles(Icono icono, int px)
        {
            var rows = Dibujos[(int)icono]; int esc = px / 16;
            var relleno = icono == Icono.FlechaCuerpo || icono == Icono.FlechaPunta ? Rojo : Hueso;
            var m = new byte[px * px];                                     // 0 vacío, 1 relleno, 2 detalle oscuro; fila 0 arriba
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    char c = rows[y][x]; if (c == '.') continue;
                    byte v = c == 'o' ? (byte)2 : (byte)1;
                    for (int dy = 0; dy < esc; dy++) for (int dx = 0; dx < esc; dx++) m[(y * esc + dy) * px + x * esc + dx] = v;
                }
            var o = new Color32[px * px]; var vacio = new Color32(0, 0, 0, 0);
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    byte v = m[y * px + x]; Color32 c;
                    if (v == 1) c = relleno; else if (v == 2) c = Contorno;
                    else
                    {
                        bool borde = false;
                        for (int dy = -1; dy <= 1 && !borde; dy++) for (int dx = -1; dx <= 1; dx++)
                        { int yy = y + dy, xx = x + dx; if (yy >= 0 && yy < px && xx >= 0 && xx < px && m[yy * px + xx] != 0) { borde = true; break; } }
                        c = borde ? Contorno : vacio;
                    }
                    o[(px - 1 - y) * px + x] = c;                            // Texture2D va de abajo arriba
                }
            return o;
        }
    }
}
