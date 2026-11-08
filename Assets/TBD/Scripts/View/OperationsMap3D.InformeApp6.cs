using System.Collections.Generic;
using TBD.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TBD.View
{
    /// <summary>
    /// D-N160 · Capa APP-6 del mapa del informe de inteligencia (Bruno, 8-nov; contenido revisado por Lucía y Paula el 9-nov). SOLO VISTA: lee el
    /// estado de NetWorld y no toca el hash. Tecla I: enciende y apaga la capa (empieza encendida). Iconos en Resources/Icons/app6 (build_app6.py,
    /// variante D). Si faltan los PNG, DrawIcon no pinta nada y no hay error.
    /// Reglas (D-N160, adaptaciones de Lucía, 08-11 24:40):
    ///   - Amigo (rectángulo slate): un símbolo por pelotón bajado en la estación (●● pelotón, D-N160 08-11 25:20) y uno por vehículo
    ///     (blindado de ruedas, solo existe a 24 px). En casa (Embarque.HomeNode), si no hay ninguno bajado, un pelotón de guarnición.
    ///   - Hostil: la horda va como rombo VACÍO, sin icono (no es fuerza militar). Asedio anunciado: rombo discontinuo (idea de Paula, 9-nov;
    ///     «previsto» SIN VERIFICAR en la norma). Horda presente (densidad ≥ NetWorld.SiegeAt): continuo en hueso. Rojo, con contorno de 2 px,
    ///     solo mientras ataca (D-N160; Paula señala que en APP-6 el color es la afiliación, lo decide Lucía). Al lado, los zombis estimados:
    ///     con ojos allí (tropa propia o un NH90 en zona) a la centena, «~2.400»; sin ojos, al millar con interrogación, «~2.000?» (Lucía, 9-nov).
    ///   - Flecha de eje de avance hacia la estación cuando hay asedio, desde el lado de la mancha: hueso mientras avanza, rojo mientras ataca.
    ///     El color rojo solo en el ataque es LICENCIA del juego frente a APP-6 (Lucía, registro 24:50), igual que el rombo discontinuo.
    ///   - Desconocido (trébol neutro, trazo continuo): densidad entre ThreatAt y SiegeAt en una estación sin tropa, sin tren y sin asedio
    ///     anunciado. Aprobado por Lucía (9-nov): es el «algo se mueve» que prepara la Colmena. Se puede apagar con _app6Desconocido.
    ///   - Aire: cúpula del NH90 en Bétera mientras está en tierra, sobre el mapa mientras vuela y encima del nodo en zona (Lucía, 9-nov).
    ///   - Estados de la Colmena (Lucía 25:15; Hugo, colmena-estados.md §4 y §6): la cifra es la mejor de dos reglas, ojos o estado.
    ///     Dispersa y sin ojos: trébol desconocido con «~2.000?». Al acecho: «~2.000». Enjambre: «2.400» y galones de columna de migración detrás de la flecha.
    ///     Aviso ámbar «La Colmena se agita…» con la señal en col_aviso (6) antes del Enjambre (7). Necesita que la red rellene ColmenaEstadoFuente y
    ///     ColmenaSenalFuente; sin eso, la capa se comporta como antes. Textos CO-0 a CO-4 en textos_informe.csv (la red los mete en el informe).
    /// Las emboscadas preparadas (PendingAmbushes) no se dibujan: el jugador no las conoce hasta que el tren cae en ellas (TR-3).
    /// </summary>
    public sealed partial class OperationsMap3D
    {
        bool _app6On = true;

        // ---- Estados de la Colmena (D-N160, aprobado por Lucía el 9-nov 25:15; Hugo, docs/economia/colmena-estados.md §4 y §6)
        /// <summary>Estado de la Colmena entera: 0 Dispersa, 1 Al acecho, 2 Enjambre; −1 si la red todavía no lo calcula.
        /// Lo conecta la red cuando exista (Nacho, colmena-estados.md §5): <c>OperationsMap3D.ColmenaEstadoFuente = () => (int)w.Colmena.Estado;</c>.
        /// Mientras sea null, la vista solo aplica la regla de los ojos y no sale el aviso.</summary>
        public static System.Func<int> ColmenaEstadoFuente;
        /// <summary>Señal diaria de la Colmena (abiertos_7d + 0,5 × (huella − 8)), en las mismas unidades que los umbrales; null si no existe.</summary>
        public static System.Func<int> ColmenaSenalFuente;
        /// <summary>Umbrales (balance_colmena: col_aviso y el del Enjambre). Si la red los lee del CSV, que los copie aquí.</summary>
        public static int ColAviso = 6, ColEnjambre = 7;
        static int ColEstado() { try { return ColmenaEstadoFuente != null ? ColmenaEstadoFuente() : -1; } catch { return -1; } }
        static int ColSenal() { try { return ColmenaSenalFuente != null ? ColmenaSenalFuente() : -1; } catch { return -1; } }

        /// <summary>Aviso ámbar antes del Enjambre (Lucía 25:15): con la señal en col_aviso, un punto antes del Enjambre. Ámbar, no rojo: el rojo es de ataques.
        /// Va en el panel de avisos del mapa, debajo de los de «zona sin tropa». Devuelve la nueva y.</summary>
        float DrawColmenaAviso(float pw, float y, ref int shown)
        {
            int sen = ColSenal(), est = ColEstado();
            if (sen < ColAviso || est == 2 || shown >= 4) return y;
            shown++;
            int zp = IconPx(16);
            DrawIcon(new Rect(20, y - 2, zp, zp), "alertas/vm_alertas_asedio_16");
            GUI.Label(new Rect(20 + zp + 6, y, pw - 20 - zp, 30), "<color=#E08C30>La Colmena se agita: si abres más nodos, irá a por ti</color>", _small);
            return y + Mathf.Max(_small.fontSize * 1.5f, zp + 4);
        }
        bool _app6Desconocido = true;
        readonly List<string> _app6Tmp = new List<string>();
        int _app6Eje;                          // App6Nodo: 0 sin flecha, 1 la horda avanza (hueso), 2 ataca (rojo)
        static Texture2D _app6Px;

        void HandleInputApp6(Keyboard kb)
        {
            if (kb != null && kb.iKey.wasPressedThisFrame) _app6On = !_app6On;
        }

        /// <summary>Símbolos de un nodo, de izquierda a derecha: primero lo hostil o desconocido y luego lo amigo.</summary>
        List<string> App6Nodo(int node, out string etiqueta)
        {
            _app6Tmp.Clear(); etiqueta = null; _app6Eje = 0;
            var n = _a.Graph.Nodes[node];

            bool asedio = false, ataca = false;
            foreach (var pl in _a.Director.Active)
                if (pl.Kind == HiveDirector.PlanKind.Asedio && pl.Node == node) { asedio = true; if (pl.Started) ataca = true; }

            int pel = 0, veh = 0;
            foreach (var p in _a.Embarque.Lista)
                if (p.Estado == PasajeroEstado.EnEstacion && p.Node == node) { if (p.Tipo == PasajeroTipo.Peloton) pel++; else veh++; }
            if (node == _a.Embarque.HomeNode && pel == 0 && veh == 0) pel = 1;          // guarnición de casa (ZoneDefended la da por defendida)

            bool tren = false;
            for (int i = 0; i < _a.Services.Count; i++) if (_a.TrainAtNode(_a.Services[i], node)) { tren = true; break; }

            bool ojos = pel > 0 || veh > 0 || Nh90Encima(node);                 // Lucía 9-nov: con ojos allí, centenas; sin ojos, millares con «?»
            bool presente = ataca || n.HordeDensity >= NetWorld.SiegeAt;
            if (presente || asedio)
            {
                // Paula 9-nov: asedio anunciado = rombo discontinuo («previsto», SIN VERIFICAR en la norma); continuo cuando la horda llega.
                int est = ColEstado();
                // Dispersa y sin ojos: no se sabe qué es (trébol desconocido, Hugo §4); si ataca, ya está identificada.
                bool trebol = est == 0 && !ojos && !ataca;
                _app6Tmp.Add(trebol ? "app6/app6_desconocido_vacio" : ataca ? "app6/app6_horda_vacio_ataque" : presente ? "app6/app6_horda_vacio" : "app6/app6_horda_tramado");
                etiqueta = Estimacion(_a.SiegeZombies(node), ojos, est);
                if (asedio) _app6Eje = ataca ? 2 : 1;
            }
            else if (_app6Desconocido && n.HordeDensity >= NetWorld.ThreatAt && pel == 0 && veh == 0 && !tren)
                _app6Tmp.Add("app6/app6_desconocido_vacio");

            for (int i = 0; i < pel; i++) _app6Tmp.Add("app6/app6_amigo_infanteria_peloton");
            for (int i = 0; i < veh; i++) _app6Tmp.Add("app6/app6_amigo_ruedas_peloton");
            return _app6Tmp;
        }

        static readonly System.Globalization.CultureInfo _es = System.Globalization.CultureInfo.GetCultureInfo("es-ES");

        /// <summary>
        /// Zombis estimados: la MEJOR de dos reglas (Lucía, 9-nov 25:15). Ojos (tropa propia o NH90 en zona): a la centena, «~2.400».
        /// Estado de la Colmena (Hugo §4): Enjambre se ve de lejos, «2.400» (centena, sin «~»); Al acecho, «~2.000» (millar);
        /// Dispersa, o estado desconocido, «~2.000?» (millar con interrogación; «<1.000?» por debajo de 500).
        /// </summary>
        static string Estimacion(int z, bool ojos, int estado = -1)
        {
            if (z <= 0) return null;
            string cen = System.Math.Max(100, (z + 50) / 100 * 100).ToString("#,0", _es);
            string mil = ((z + 500) / 1000 * 1000).ToString("#,0", _es);
            if (estado == 2) return cen;
            if (ojos) return "~" + cen;
            if (estado == 1) return z < 500 ? "<1.000" : "~" + mil;
            return z < 500 ? "<1.000?" : "~" + mil + "?";
        }

        /// <summary>¿Hay un NH90 de Bétera sobre ese nodo (en zona)? Cuenta como ojos para la estimación.</summary>
        static bool Nh90Encima(int node)
        {
            var b = AerodromoVista.Estado; if (b == null) return false;
            foreach (var x in b.Aeronaves) if (x.Estado == AeronaveEstado.EnZona && x.Destino == node) return true;
            return false;
        }

        /// <summary>Helicópteros (Lucía, 9-nov): cúpula amiga en Bétera mientras están en tierra; sobre el mapa mientras vuelan
        /// (entre la base y el destino, según lo que queda de vuelo) y encima del nodo cuando están en zona.</summary>
        void DrawApp6Aire(int esc)
        {
            var b = AerodromoVista.Estado; if (b == null) return;
            var t = App6Tex("app6/app6_aire_helicoptero_24"); if (t == null) return;
            float w = t.width * esc, hh = t.height * esc;
            var lift = 0.8f * Mathf.Clamp(_dist / 230f, 0.0015f, 2f);
            var bsp = _cam.WorldToScreenPoint(MapToWorld(b.MapX, b.MapY, lift));
            int enBase = 0;
            foreach (var x in b.Aeronaves)
            {
                Vector3 sp;
                if (x.Estado == AeronaveEstado.Ida || x.Estado == AeronaveEstado.Volviendo || x.Estado == AeronaveEstado.EnZona)
                {
                    if (x.Destino < 0 || x.Destino >= _a.Graph.Nodes.Count) continue;
                    var nd = _a.Graph.Nodes[x.Destino];
                    float f = 1f;                                                     // 0 = en la base, 1 = en el destino
                    if (x.Estado != AeronaveEstado.EnZona)
                    {
                        float vuelo = AerodromoEstado.VueloS(System.Math.Max(0, b.Km(nd.Name)));
                        float q = vuelo > 0 ? Mathf.Clamp01(x.Quedan / vuelo) : 0.5f;
                        f = x.Estado == AeronaveEstado.Ida ? 1f - q : q;
                    }
                    sp = _cam.WorldToScreenPoint(MapToWorld(b.MapX + (nd.XMeters - b.MapX) * f, b.MapY + (nd.YMeters - b.MapY) * f, lift));
                    if (sp.z <= 0) continue;
                    GUI.DrawTexture(new Rect(sp.x - w * 0.5f, Screen.height - sp.y - hh * (x.Estado == AeronaveEstado.EnZona ? 2.6f : 1f), w, hh), t, ScaleMode.StretchToFill, true);
                }
                else
                {
                    if (bsp.z <= 0) continue;
                    int st = IconPx(32);                                              // en fila, debajo del rótulo de la base
                    GUI.DrawTexture(new Rect(bsp.x + st * 0.6f + 4 + enBase * (w + 2 * esc), Screen.height - bsp.y + 2 * esc, w, hh), t, ScaleMode.StretchToFill, true);
                    enBase++;
                }
            }
        }

        /// <summary>Se llama en OnGUI, después de los iconos de estación: los símbolos van en fila encima del nombre de la estación.</summary>
        void DrawApp6()
        {
            if (!_app6On) return;
            var g = _a.Graph;
            int esc = Mathf.Max(1, Mathf.RoundToInt(Screen.height / 540f));
            int h = 24 * esc;
            for (int k = 0; k < g.Nodes.Count; k++)
            {
                var nd = g.Nodes[k];
                var sp = _cam.WorldToScreenPoint(MapToWorld(nd.XMeters, nd.YMeters, 0.8f * Mathf.Clamp(_dist / 230f, 0.0015f, 2f)));
                if (sp.z <= 0) continue;
                var l = App6Nodo(k, out var etiqueta);
                if (l.Count == 0) continue;
                if (_app6Eje > 0) DrawEje(nd, sp, _app6Eje == 2, esc);
                int st = IconPx(32);
                float x = sp.x + st * 0.5f + 4, y = Screen.height - sp.y - st * 1.1f - h * 0.25f;
                for (int i = 0; i < l.Count; i++)
                {
                    var t = App6Tex(l[i] + "_24");
                    if (t == null) continue;
                    float w = t.width * esc;                                  // anchos distintos: rectángulo 37, rombo 23, cúpula 29
                    GUI.DrawTexture(new Rect(x, y, w, t.height * esc), t, ScaleMode.StretchToFill, true);
                    x += w + 2 * esc;
                    if (i == 0 && etiqueta != null)                           // los zombis estimados, pegados al rombo de la horda
                    {
                        var gc = new GUIContent("<color=#D4D0CC>" + etiqueta + "</color>");
                        float lw = _small.CalcSize(gc).x;
                        GUI.Label(new Rect(x, y + h * 0.25f, lw + 4, _small.fontSize * 2f), gc, _small);
                        x += lw + 6 * esc;
                    }
                }
            }
            DrawApp6Aire(esc);
        }

        /// <summary>
        /// Flecha de eje de avance (Lucía, 9-nov, a propuesta de Paula): hacia la estación desde el lado por el que llega la horda; hueso mientras avanza,
        /// rojo 982420 mientras ataca. El lado sale de la mancha (HordeField) alrededor del nodo: media de las direcciones pesada por la densidad,
        /// en un anillo de 2 a 16 celdas. Si no hay masa cerca (asedio de zona abstracto, sin mancha), se usa la celda más cargada a menos de 32;
        /// si tampoco hay, no se dibuja flecha. OJO: es el lado en el mapa, no la puerta concreta de la zona (la red no sabe por qué puerta entra).
        /// </summary>
        void DrawEje(RailNode nd, Vector3 sp, bool ataca, int esc)
        {
            var f = _a.Field;
            int cx = nd.XMeters, cy = nd.YMeters;
            double sx = 0, sy = 0, peso = 0; int best = 0, bx = 0, by = 0;
            for (int dy = -32; dy <= 32; dy++)
                for (int dx = -32; dx <= 32; dx++)
                {
                    int r2 = dx * dx + dy * dy; if (r2 < 4 || r2 > 32 * 32) continue;
                    int d = f.At(cx + dx, cy + dy); if (d <= 0) continue;
                    if (r2 <= 16 * 16) { double r = System.Math.Sqrt(r2); sx += d * dx / r; sy += d * dy / r; peso += d; }
                    if (d > best) { best = d; bx = dx; by = dy; }
                }
            double ux, uy;
            if (peso > 0 && sx * sx + sy * sy > 1e-6) { double m = System.Math.Sqrt(sx * sx + sy * sy); ux = sx / m; uy = sy / m; }
            else if (best > 0) { double m = System.Math.Sqrt(bx * bx + by * by); ux = bx / m; uy = by / m; }
            else return;

            var lift = 0.8f * Mathf.Clamp(_dist / 230f, 0.0015f, 2f);
            var lejos = _cam.WorldToScreenPoint(MapToWorld(cx + ux * 6, cy + uy * 6, lift));
            if (lejos.z <= 0) return;
            Vector2 a = new Vector2(lejos.x, Screen.height - lejos.y), b = new Vector2(sp.x, Screen.height - sp.y);
            Vector2 v = b - a; float len = v.magnitude; if (len < 8f) return;
            Vector2 dir = v / len;
            float largo = Mathf.Clamp(len, 40f * esc, 90f * esc);              // longitud en pantalla acotada: que se lea a cualquier zoom
            b -= dir * IconPx(32) * 0.7f;                                     // la punta se queda fuera del icono de la estación
            a = b - dir * largo;
            var col = ataca ? new Color32(0x98, 0x24, 0x20, 255) : new Color32(0xd4, 0xd0, 0xcc, 255);
            float g = 2f * esc, punta = 7f * esc;
            Linea(a, b, g, col);
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            Linea(b, b - dir * punta + nrm * punta * 0.7f, g, col);
            Linea(b, b - dir * punta - nrm * punta * 0.7f, g, col);
            if (ColEstado() == 2)                                             // Enjambre: columna de migración, galones detrás de la flecha (Hugo §4)
                for (int k = 1; k <= 3; k++)
                {
                    var c0 = a - dir * (k * 12f * esc);
                    Linea(c0, c0 - dir * punta * 0.8f + nrm * punta * 0.6f, g, col);
                    Linea(c0, c0 - dir * punta * 0.8f - nrm * punta * 0.6f, g, col);
                }
        }

        static void Linea(Vector2 a, Vector2 b, float grosor, Color col)
        {
            if (_app6Px == null) { _app6Px = new Texture2D(1, 1); _app6Px.SetPixel(0, 0, Color.white); _app6Px.Apply(); }
            var m = GUI.matrix; var c = GUI.color;
            Vector2 d = b - a; float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.color = col;
            GUI.DrawTexture(new Rect(a.x, a.y - grosor * 0.5f, d.magnitude, grosor), _app6Px);
            GUI.matrix = m; GUI.color = c;
        }

        /// <summary>Leyenda de una línea bajo el informe de inteligencia (solo con la capa encendida).</summary>
        void DrawApp6Leyenda(float x, float y, float w)
        {
            if (!_app6On) return;
            GUI.Label(new Rect(x, y, w, _small.fontSize * 2f),
                "<color=#808080>Mapa (I): rectángulo propio · cúpula NH90 · rombo horda (discontinuo: anunciada; rojo: ataca; flecha: por dónde llega; galones: Enjambre) · trébol sin identificar · «?» sin ojos allí</color>", _small);
        }

        static Texture2D App6Tex(string path)
        {
            if (!_iconCache.TryGetValue(path, out var t))
            {
                t = Resources.Load<Texture2D>("Icons/" + path);
                if (t != null) t.filterMode = FilterMode.Point;
                _iconCache[path] = t;
            }
            return t;
        }
    }
}
