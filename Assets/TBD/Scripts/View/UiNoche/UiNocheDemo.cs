using UnityEngine;
using UnityEngine.InputSystem;   // el proyecto usa solo el Input System (activeInputHandler 1)

namespace TBD.View.UiNoche
{
    /// <summary>
    /// Prueba de la interfaz de noche con las piezas de Bruno (Resources/Icons/noche) y datos falsos.
    /// Solo se activa con la bandera -uidemo (acordado con Marcos, 7-nov 01:11). Teclas:
    ///   1 HUD de noche · 2 menú (O abre y cierra Opciones) · 3 parte (V alterna victoria y derrota, D-N108)
    ///   4 avisos (apertura D-N106 y avisos de Sara D-N109) · H oculta la ayuda.
    /// Las posiciones son las de las maquetas en la base 640x360 (build_maquetas.hud, build_menu, build_parte).
    /// Para conectarlo al juego: pasa tu IUiNocheDatos a UiNocheVistas y llama a sus métodos desde tu OnGUI.
    /// SIN PROBAR EN UNITY (escrito sin editor).
    /// </summary>
    public sealed class UiNocheDemo : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-uidemo") < 0) return;
            if (FindFirstObjectByType<UiNocheDemo>() != null) return;
            var go = new GameObject("UiNoche (prueba)");
            DontDestroyOnLoad(go);
            go.AddComponent<UiNocheDemo>();
        }

        int _pantalla = 1;
        bool _opciones, _ayuda = true;
        DatosFalsos _datos;

        void Awake() { _datos = new DatosFalsos(Time.unscaledTime); }

        void Update()
        {
            var kb = Keyboard.current; if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) _pantalla = 1;
            if (kb.digit2Key.wasPressedThisFrame) _pantalla = 2;
            if (kb.digit3Key.wasPressedThisFrame) _pantalla = 3;
            if (kb.digit4Key.wasPressedThisFrame) { _pantalla = 4; _datos = new DatosFalsos(Time.unscaledTime); }
            if (kb.oKey.wasPressedThisFrame) _opciones = !_opciones;
            if (kb.hKey.wasPressedThisFrame) _ayuda = !_ayuda;
            if (kb.vKey.wasPressedThisFrame) { var p = _datos.Parte; p.victoria = !p.victoria; _datos.Parte = p; }
        }

        void OnGUI()
        {
            GUI.depth = -1000;   // encima del IMGUI del juego
            UiNocheKit.Begin();
            float t = Time.unscaledTime;
            // primero la pantalla de fondo, luego la ayuda y encima lo que se superpone (parte y apertura), para que la ayuda no tape el parte
            if (_pantalla == 2) UiNocheVistas.Menu(_datos, t, _opciones);
            else UiNocheVistas.Hud(_datos, t);
            if (_ayuda)
                UiNocheKit.Texto(4, 276, "Prueba de interfaz · 1 HUD · 2 menú (O: opciones) · 3 parte (V) · 4 avisos · H", UiNocheKit.OlivaGris, 8);
            if (_pantalla == 3) UiNocheVistas.ParteNoche(_datos.Parte, t);
            if (_pantalla == 4) UiNocheVistas.Apertura(_datos, t);
            UiNocheKit.End();
        }
    }

    /// <summary>Las pantallas, separadas de la prueba para que Marcos las llame con sus datos.</summary>
    public static class UiNocheVistas
    {
        public static bool SinTarjetas;   // interfaz v2: sin las tarjetas del kit antiguo
        public static float[] OpcVal = { .8f, .55f, .9f };
        static void Recurso(int i, string icono, int valor)
        {
            float x = i < 3 ? 4 + i * 60 : 360 + (i - 3) * 60;   // munición, materiales y combustible a la izquierda del reloj (maqueta); pila y bajas a su derecha
            UiNocheKit.Slice("ui_recurso_slot", new Rect(x, 4, 56, 20));
            UiNocheKit.Pieza(icono, x + 2, 6);
            UiNocheKit.Texto(x + 22, 8, valor.ToString(), UiNocheKit.Hueso, 10);
        }

        public static void Hud(IUiNocheDatos d, float t)
        {
            // reloj (D-N65); la barra de la noche (D-N106) se quita del todo: D-N157, no hay oleadas (D-N136)
            UiNocheKit.Slice("ui_panel", new Rect(288, 4, 64, 22));
            UiNocheKit.Texto(320, 8, d.Reloj, UiNocheKit.Hueso, 11, TextAnchor.UpperCenter);
            // recursos
            Recurso(0, "ui_recurso_municion_16", d.Municion);
            Recurso(1, "ui_recurso_materiales_16", d.Materiales);
            Recurso(2, "ui_recurso_combustible_16", d.Combustible);
            Recurso(3, "ui_recurso_pila_16", d.Pila);
            Recurso(4, "ui_recurso_bajas_16", d.Bajas);
            // columna de avisos (izquierda)
            for (int i = 0; i < d.Avisos.Count && i < 6; i++)
            {
                var a = d.Avisos[i];
                float y = 28 + i * 26;
                string slot = a.nivel == Nivel.Ataque ? "ui_alerta_ataque" : a.nivel == Nivel.Aviso ? "ui_alerta_aviso" : "ui_alerta_info";
                bool parpadeo = a.nivel == Nivel.Aviso && t - a.desde < 3f && Mathf.FloorToInt((t - a.desde) * 2f) % 2 == 1;   // D-N109
                if (!parpadeo) UiNocheKit.Slice(slot, new Rect(4, y, 24, 24));
                UiNocheKit.Pieza(a.icono, 8, y + 4);
                float w = 172;
                UiNocheKit.Slice("ui_panel", new Rect(30, y, w, 24));
                UiNocheKit.Texto(38, y + 6, a.texto, UiNocheKit.Hueso, 9);
                if (a.teclaT) UiNocheKit.Pieza("ui_tecla_t_9", 108, y + 7);
                if (a.teclaT) UiNocheKit.Texto(120, y + 6, "para recargar", UiNocheKit.Hueso, 9);
            }
            // frentes activos (arriba a la derecha)
            for (int i = 0; i < d.Frentes.Count && i < 4; i++)
            {
                var f = d.Frentes[i];
                float x = 640 - 4 - 150, y = 28 + i * 22;
                UiNocheKit.Slice("ui_frente_fila", new Rect(x, y, 150, 20));
                UiNocheKit.Pieza(f.icono, x + 2, y + 2);
                UiNocheKit.Texto(x + 22, y + 5, f.nombre, UiNocheKit.Hueso, 9);
                string g = "ui_gravedad_" + (int)f.gravedad;
                if (f.gravedad == Gravedad.Alta) UiNocheKit.Fotograma(g, x + 96, y + 9, 2, UiNocheKit.FotogramaPorTiempo(t, 400, 2));
                else UiNocheKit.Pieza(g, x + 96, y + 9);
                UiNocheKit.Pieza("ui_boton_ir_normal", x + 130, y + 2);
            }
            // tarjetas de los pelotones (abajo): con la interfaz v2 las pinta PrototypeBootstrap.HudV2
            for (int i = 0; !SinTarjetas && i < d.Tarjetas.Count && i < 5; i++)
            {
                var c = d.Tarjetas[i];
                float x = 176 + i * 58, y = 292;
                UiNocheKit.Slice(c.seleccionada ? "ui_tarjeta_unidad_sel" : "ui_tarjeta_unidad", new Rect(x, y, 56, 64));
                UiNocheKit.Fotograma("ui_tarjeta_" + c.tipo + "_48", x + 4, y + 4, 6, c.id % 6);
                UiNocheKit.Estirada("ui_barra_efectivos", new Rect(x + 4, y + 54, 48 * c.efectivos, 3));
                if (c.municion > 0) UiNocheKit.Estirada(c.municion < .3f ? "ui_barra_baja" : "ui_barra_municion", new Rect(x + 4, y + 58, 48 * c.municion, 2));
                UiNocheKit.Texto(x + 47, y + 3, (i + 1).ToString(), UiNocheKit.Hueso, 7);
                if (c.sinMunicion)    // capa de 40x48 (D-N109): parpadea 3 s y luego se queda fija en el fotograma 0
                {
                    int fr = t - c.sinMunicionDesde < 3f ? UiNocheKit.FotogramaPorTiempo(t - c.sinMunicionDesde, 500, 2) : 0;
                    UiNocheKit.Fotograma("ui_tarjeta_sin_municion", x + 8, y + 8, 2, fr);
                }
            }
            // el panel de iconos de ordenes se retira (D-N144; la interfaz v2 de Lucia lo sustituira)
        }

        public static void Menu(IUiNocheDatos d, float t, bool opciones)
        {
            UiNocheKit.Pieza("ui_menu_fondo_noche", 0, 0);
            UiNocheKit.Pieza("ui_logo_via_muerta", 24, 24);
            string[] botones = { "Jugar", "Continuar", "Cómo se juega", "Opciones", "Salir" };
            var raton = UiNocheKit.Raton();
            bool pulsado = Mouse.current != null && Mouse.current.leftButton.isPressed;
            for (int i = 0; i < botones.Length; i++)
            {
                var r = new Rect(24, 120 + i * 26, 132, 20);
                string e = i == 1 && !d.HayPartidaGuardada ? "inactivo" : r.Contains(raton) && !opciones ? (pulsado ? "pulsado" : "encima") : "normal";
                UiNocheKit.Slice("ui_menu_boton_" + e, r);
                Color c = e == "inactivo" ? UiNocheKit.OlivaGris : e == "encima" ? UiNocheKit.Crema : e == "pulsado" ? UiNocheKit.Beige : UiNocheKit.Hueso;
                UiNocheKit.Texto(r.x + 14, r.y + 4, botones[i], c, 9);
            }
            UiNocheKit.Texto(632, 348, "v0.1 · prototipo", UiNocheKit.Hex(0x48403c), 8, TextAnchor.UpperRight);
            if (!opciones) return;
            UiNocheKit.Velo();
            float x = 170, y = 92;
            UiNocheKit.Slice("ui_menu_panel", new Rect(x, y, 300, 176));
            UiNocheKit.Texto(x + 12, y + 8, "Opciones", UiNocheKit.Crema, 11);
            UiNocheKit.Estirada("ui_menu_separador", new Rect(x + 10, y + 24, 280, 1));
            string[] vol = { "Volumen general", "Música", "Efectos" };
            float[] val = OpcVal;   // valores reales de las opciones (los pone MainMenu); por defecto los de la maqueta
            for (int i = 0; i < 3; i++)
            {
                float yy = y + 34 + i * 20;
                UiNocheKit.Texto(x + 14, yy, vol[i], UiNocheKit.Hueso, 9);
                UiNocheKit.Estirada("ui_deslizador_pista", new Rect(x + 130, yy + 6, 120, 2));
                UiNocheKit.Estirada("ui_deslizador_relleno", new Rect(x + 130, yy + 6, 120 * val[i], 1));
                UiNocheKit.Pieza(i == 1 ? "ui_deslizador_mando_encima" : "ui_deslizador_mando_normal", x + 130 + 120 * val[i] - 2, yy + 3);
                UiNocheKit.Texto(x + 258, yy, Mathf.RoundToInt(val[i] * 100).ToString(), UiNocheKit.Beige, 9);
            }
            UiNocheKit.Texto(x + 14, y + 98, "Resolución", UiNocheKit.Hueso, 9);
            UiNocheKit.Pieza("ui_flecha_izq", x + 130, y + 101);
            UiNocheKit.Pieza("ui_flecha_dcha", x + 246, y + 101);
            UiNocheKit.Texto(x + 190, y + 98, Screen.width + " × " + Screen.height, UiNocheKit.Crema, 9, TextAnchor.UpperCenter);
            UiNocheKit.Texto(x + 14, y + 118, "Pantalla completa", UiNocheKit.Hueso, 9);
            UiNocheKit.Pieza(Screen.fullScreen ? "ui_casilla_si" : "ui_casilla_no", x + 130, y + 120);
            UiNocheKit.Slice("ui_menu_boton_encima", new Rect(x + 102, y + 148, 90, 18));
            UiNocheKit.Texto(x + 147, y + 151, "Aplicar", UiNocheKit.Crema, 9, TextAnchor.UpperCenter);
            UiNocheKit.Slice("ui_menu_boton_normal", new Rect(x + 200, y + 148, 90, 18));
            UiNocheKit.Texto(x + 245, y + 151, "Volver", UiNocheKit.Hueso, 9, TextAnchor.UpperCenter);
        }

        static void Barra(float x, float y, float w, float v, float u1, float u2)
        {
            UiNocheKit.Slice("ui_parte_barra_fondo", new Rect(x, y, w, 6));
            UiNocheKit.Estirada("ui_parte_barra_relleno", new Rect(x + 1, y + 1, (w - 2) * Mathf.Clamp01(v), 4));
            UiNocheKit.Pieza("ui_parte_barra_umbral", x + w * u1 - 1, y - 1);
            UiNocheKit.Pieza("ui_parte_barra_umbral", x + w * u2 - 1, y - 1);
        }

        /// <summary>Parte de la noche (D-N108). Victoria: y = 84, sin velo; derrota: y = 60, con velo y la partida en pausa.</summary>
        public static void ParteNoche(Parte p, float t)
        {
            if (!p.victoria) UiNocheKit.Velo();
            float pw = 340, ph = 214, x0 = 320 - pw / 2, y0 = p.victoria ? 84 : 60;
            UiNocheKit.Slice(p.victoria ? "ui_parte_panel" : "ui_parte_panel_derrota", new Rect(x0, y0, pw, ph));
            UiNocheKit.Pieza(p.victoria ? "ui_parte_cabecera_amanecer" : "ui_parte_cabecera_caida", 320 - 48, y0 + 4);
            UiNocheKit.Texto(320, y0 + 20, p.victoria ? "Sagunto resiste" : (string.IsNullOrEmpty(p.titulo) ? "Sagunto ha caído" : p.titulo), p.victoria ? UiNocheKit.Crema : UiNocheKit.Hueso, 11, TextAnchor.UpperCenter);
            float y = y0 + 40;
            (string icono, string nombre, string valor)[] filas =
            {
                ("ui_resultado_zombis_16", "Zombis abatidos", p.zombis.ToString("N0")),
                ("ui_recurso_bajas_16", "Bajas propias", p.bajas.ToString()),
                ("ui_recurso_municion_16", "Munición gastada", p.municionGastada + " u"),
                ("ui_recurso_municion_16", "Munición restante", p.municionRestante + " u"),
            };
            foreach (var f in filas)
            {
                UiNocheKit.Slice("ui_parte_franja", new Rect(x0 + 8, y - 2, pw - 16, 15));
                UiNocheKit.Pieza(f.icono, x0 + 12, y - 2);
                UiNocheKit.Texto(x0 + 32, y, f.nombre, UiNocheKit.Beige, 9);
                UiNocheKit.Texto(x0 + pw - 14, y, f.valor, UiNocheKit.Hueso, 9, TextAnchor.UpperRight);
                y += 16;
            }
            y += 4;
            (string icono, string nombre, float v, float u1, float u2)[] estado =
            {
                ("ui_frente_estacion_16", "Estación", p.estacion, .40f, .75f),
                ("ui_frente_tren_16", "Tren", p.tren, .40f, .75f),
                ("ui_resultado_muro_16", "Muro", p.muro, .25f, .50f),      // D-N108
            };
            foreach (var e in estado)
            {
                UiNocheKit.Pieza(e.icono, x0 + 12, y - 2);
                UiNocheKit.Texto(x0 + 32, y, e.nombre, UiNocheKit.Beige, 9);
                Barra(x0 + 110, y + 2, pw - 170, e.v, e.u1, e.u2);
                UiNocheKit.Texto(x0 + pw - 14, y, Mathf.RoundToInt(e.v * 100) + " %", UiNocheKit.Hueso, 9, TextAnchor.UpperRight);
                y += 15;
            }
            string[] botones = p.victoria ? new[] { "Seguir" } : new[] { "Volver a empezar", "Menú principal" };
            float bw = 120, total = botones.Length * bw + (botones.Length - 1) * 8, bx = 320 - total / 2, by = y0 + ph - 26;
            for (int i = 0; i < botones.Length; i++)
            {
                var rb = new Rect(bx + i * (bw + 8), by, bw, 18); UiNocheKit.Slice(i == 0 ? "ui_boton_principal" : "ui_boton_normal", rb); if (i == 0) UiNocheKit.Pieza("ui_boton_cuna", rb.x + 4, rb.y + rb.height / 2 - 3);   // boton principal opcion C (Bruno/Lucia): contraste 5,2:1
                UiNocheKit.Texto(bx + i * (bw + 8) + bw / 2, by + 3, botones[i], UiNocheKit.Hueso, 9, TextAnchor.UpperCenter);
            }
        }

        /// <summary>Apertura (D-N106): rótulo, «Primera oleada en mm:ss» y, en los últimos 10 s, la placa urgente.</summary>
        public static void Apertura(IUiNocheDatos d, float t)
        {
            float x = 210, y = 64;
            UiNocheKit.Slice("ui_rotulo_inicio", new Rect(x, y, 220, 64));
            UiNocheKit.Pieza("ui_icono_luna_9", 320 - 4, y + 8);
            UiNocheKit.Texto(320, y + 20, "Sagunto · 22:00 · Noche 1", UiNocheKit.Crema, 11, TextAnchor.UpperCenter);
            UiNocheKit.Pieza("ui_rotulo_filete_64", 320 - 32, y + 36);
            UiNocheKit.Texto(320, y + 42, "Aguanta hasta el amanecer", UiNocheKit.Beige, 9, TextAnchor.UpperCenter);
            float quedan = Mathf.Max(0, d.PrimeraOleadaEn - (t % 45f));
            bool urgente = quedan <= 10f;
            var r = new Rect(250, 140, 140, 16);
            if (urgente) UiNocheKit.Slice("ui_aviso_oleada_urgente", r, 2, UiNocheKit.FotogramaPorTiempo(t, 250, 2));   // sustituye a la placa los últimos 10 s
            else UiNocheKit.Slice("ui_aviso_oleada_placa", r);
            UiNocheKit.Fotograma("ui_icono_reloj_arena", r.x + 4, r.y + 3, 4, UiNocheKit.FotogramaPorTiempo(t, 250, 4));
            int s = Mathf.CeilToInt(quedan);
            UiNocheKit.Texto(r.x + 18, r.y + 3, "Primera oleada en " + (s / 60).ToString("00") + ":" + (s % 60).ToString("00"), urgente ? UiNocheKit.Crema : UiNocheKit.Hueso, 9);
        }
    }
}
