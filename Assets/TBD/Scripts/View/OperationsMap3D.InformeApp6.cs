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
    ///   - Desconocido (trébol neutro, trazo continuo): densidad entre ThreatAt y SiegeAt en una estación sin tropa, sin tren y sin asedio
    ///     anunciado. Aprobado por Lucía (9-nov): es el «algo se mueve» que prepara la Colmena. Se puede apagar con _app6Desconocido.
    ///   - Aire: cúpula del NH90 en Bétera mientras está en tierra, sobre el mapa mientras vuela y encima del nodo en zona (Lucía, 9-nov).
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

            bool ojos = pel > 0 || veh > 0 || Nh90Encima(node);                 // Lucía 9-nov: con ojos allí, centenas; sin ojos, millares con «?»
            bool presente = ataca || n.HordeDensity >= NetWorld.SiegeAt;
            if (presente || asedio)
            {
                // Paula 9-nov: asedio anunciado = rombo discontinuo («previsto», SIN VERIFICAR en la norma); continuo cuando la horda llega.
                _app6Tmp.Add(ataca ? "app6/app6_horda_vacio_ataque" : presente ? "app6/app6_horda_vacio" : "app6/app6_horda_tramado");
                etiqueta = Estimacion(_a.SiegeZombies(node), ojos);
            }
            else if (_app6Desconocido && n.HordeDensity >= NetWorld.ThreatAt && pel == 0 && veh == 0 && !tren)
                _app6Tmp.Add("app6/app6_desconocido_vacio");

            for (int i = 0; i < pel; i++) _app6Tmp.Add("app6/app6_amigo_infanteria_peloton");
            for (int i = 0; i < veh; i++) _app6Tmp.Add("app6/app6_amigo_ruedas_peloton");
            return _app6Tmp;
        }

        static readonly System.Globalization.CultureInfo _es = System.Globalization.CultureInfo.GetCultureInfo("es-ES");

        /// <summary>Zombis estimados (Lucía, 9-nov): con ojos, a la centena («~2.400»); sin ojos, al millar y con interrogación («~2.000?», «<1.000?»).</summary>
        static string Estimacion(int z, bool ojos)
        {
            if (z <= 0) return null;
            if (ojos) return "~" + System.Math.Max(100, (z + 50) / 100 * 100).ToString("#,0", _es);
            return z < 500 ? "<1.000?" : "~" + ((z + 500) / 1000 * 1000).ToString("#,0", _es) + "?";
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

        /// <summary>Leyenda de una línea bajo el informe de inteligencia (solo con la capa encendida).</summary>
        void DrawApp6Leyenda(float x, float y, float w)
        {
            if (!_app6On) return;
            GUI.Label(new Rect(x, y, w, _small.fontSize * 2f),
                "<color=#808080>Mapa (I): rectángulo propio · cúpula NH90 · rombo horda (discontinuo: anunciada; rojo: ataca) · trébol sin identificar · «?» sin ojos allí</color>", _small);
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
