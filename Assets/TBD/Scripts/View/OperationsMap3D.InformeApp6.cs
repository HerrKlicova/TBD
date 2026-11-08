using System.Collections.Generic;
using TBD.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TBD.View
{
    /// <summary>
    /// D-N160 · Capa APP-6 del mapa del informe de inteligencia (Bruno, 8-nov; contenido a revisar por Paula y Lucía). SOLO VISTA: lee el
    /// estado de NetWorld y no toca el hash. Tecla I: enciende y apaga la capa (empieza encendida). Iconos en Resources/Icons/app6 (build_app6.py,
    /// variante D). Si faltan los PNG, DrawIcon no pinta nada y no hay error.
    /// Reglas (D-N160, adaptaciones de Lucía, 08-11 24:40):
    ///   - Amigo (rectángulo slate): un símbolo por pelotón bajado en la estación (●● pelotón, D-N160 08-11 25:20) y uno por vehículo
    ///     (blindado de ruedas, solo existe a 24 px). En casa (Embarque.HomeNode), si no hay ninguno bajado, un pelotón de guarnición.
    ///   - Hostil: la horda va como rombo VACÍO, sin icono (no es fuerza militar). En hueso si hay asedio anunciado o la densidad llega a
    ///     NetWorld.SiegeAt; en rojo, con contorno de 2 px, solo mientras ataca (plan de asedio empezado). Al lado, los zombis estimados.
    ///   - Desconocido (trébol neutro): densidad entre ThreatAt y SiegeAt en una estación sin tropa, sin tren y sin asedio anunciado:
    ///     hay algo pero nadie lo ha identificado. PROPUESTA de Bruno, sin decidir: si Lucía no la quiere, se quita con _app6Desconocido = false.
    /// Las emboscadas preparadas (PendingAmbushes) no se dibujan: el jugador no las conoce hasta que el tren cae en ellas (TR-3).
    /// </summary>
    public sealed partial class OperationsMap3D
    {
        bool _app6On = true;
        bool _app6Desconocido = true;
        readonly List<string> _app6Tmp = new List<string>();

        void HandleInputApp6(Keyboard kb)
        {
            if (kb != null && kb.iKey.wasPressedThisFrame) _app6On = !_app6On;
        }

        /// <summary>Símbolos de un nodo, de izquierda a derecha: primero lo hostil o desconocido y luego lo amigo.</summary>
        List<string> App6Nodo(int node, out string etiqueta)
        {
            _app6Tmp.Clear(); etiqueta = null;
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

            if (ataca || asedio || n.HordeDensity >= NetWorld.SiegeAt)
            {
                _app6Tmp.Add(ataca ? "app6/app6_horda_vacio_ataque" : "app6/app6_horda_vacio");
                int z = _a.SiegeZombies(node);
                if (z > 0) etiqueta = "~" + (z >= 1000 ? (z / 100 * 100).ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("es-ES")) : z.ToString());
            }
            else if (_app6Desconocido && n.HordeDensity >= NetWorld.ThreatAt && pel == 0 && veh == 0 && !tren)
                _app6Tmp.Add("app6/app6_desconocido_vacio");

            for (int i = 0; i < pel; i++) _app6Tmp.Add("app6/app6_amigo_infanteria_peloton");
            for (int i = 0; i < veh; i++) _app6Tmp.Add("app6/app6_amigo_ruedas_peloton");
            return _app6Tmp;
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
                int st = IconPx(32);
                float x = sp.x + st * 0.5f + 4, y = Screen.height - sp.y - st * 1.1f - h * 0.25f;
                foreach (var s in l)
                {
                    var t = App6Tex(s + "_24");
                    if (t == null) continue;
                    float w = t.width * esc;                                  // anchos distintos: rectángulo 37, rombo 23, cúpula 29
                    GUI.DrawTexture(new Rect(x, y, w, t.height * esc), t, ScaleMode.StretchToFill, true);
                    x += w + 2 * esc;
                }
                if (etiqueta != null) GUI.Label(new Rect(x + 2, y + h * 0.25f, 120, _small.fontSize * 2f), "<color=#D4D0CC>" + etiqueta + "</color>", _small);
            }
        }

        /// <summary>Leyenda de una línea bajo el informe de inteligencia (solo con la capa encendida).</summary>
        void DrawApp6Leyenda(float x, float y, float w)
        {
            if (!_app6On) return;
            GUI.Label(new Rect(x, y, w, _small.fontSize * 2f),
                "<color=#808080>Mapa (I): rectángulo propio · rombo horda (rojo: ataca) · trébol sin identificar</color>", _small);
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
