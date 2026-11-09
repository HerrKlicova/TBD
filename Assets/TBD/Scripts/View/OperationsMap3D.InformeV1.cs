using System.Collections.Generic;
using TBD.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TBD.View
{
    /// <summary>
    /// D-N160 · Pantalla del informe de inteligencia, composición V1 «dos columnas, con remaches» (elegida por Lucía, registro 25:55; maqueta en
    /// docs/arte/pixel-codigo/informe/informe_V1.png). SOLO VISTA: lee NetWorld y no toca la simulación ni el hash. SIN COMPILAR (Bruno, desde la nube).
    /// Tab abre y cierra; Esc cierra; un clic en el panel pequeño del informe también lo abre.
    /// A la izquierda (60 %) queda una ventana sin plancha: se ve el mapa 3D vivo con la capa APP-6 (se enciende sola al abrir).
    /// A la derecha, el parte de arriba abajo: 3 amenaza · 4 fuerzas propias · 5 sin novedad · 6 valoración · 7 necesidades · 8 firma.
    /// Los textos son los de Lucía (docs/informe-textos.md, 08-11) y están TODOS en InformeTextos, abajo: cambiarlos solo ahí.
    /// Una línea que se queda vacía no se muestra (regla de Lucía). Lo que la red todavía no da se deja fuera (no se inventa):
    ///   - memoria de tráfico y de atención (sección 3): falta que la red las exponga; ver Pendiente en InformeTextos.
    ///   - tipo de vehículo (Dragón, VAMTAC): Pasajero solo dice Vehiculo; la parte opcional no sale (Lucía, 09-11).
    ///   - pelotones en casa: la guarnición de Sagunto no está en Embarque.Lista; sale «guarnición» (Lucía, añadidos 08-11).
    /// </summary>
    public sealed partial class OperationsMap3D
    {
        bool _informeOn;
        bool _informeApp6Antes;
        GUIStyle _infTit, _infNum, _infTxt, _infSec, _infGris, _infRes;
        float _infCacheT = -1f;
        readonly InformeDatos _inf = new InformeDatos();

        /// <summary>Se llama al principio de HandleInput.</summary>
        void HandleInputInforme(Keyboard kb)
        {
            if (kb == null) return;
            if (kb.tabKey.wasPressedThisFrame) AbrirInforme(!_informeOn);
            else if (_informeOn && kb.escapeKey.wasPressedThisFrame) AbrirInforme(false);
        }

        void AbrirInforme(bool on)
        {
            if (on == _informeOn) return;
            _informeOn = on;
            if (on) { _informeApp6Antes = _app6On; _app6On = true; _infCacheT = -1f; }    // el mapa del informe es la capa APP-6
            else _app6On = _informeApp6Antes;
        }

        // ================================================================ datos del parte (se rehacen cada medio segundo real)
        sealed class InformeDatos
        {
            public string Dtg, DtgInicio, Num, Mando, Resumen;
            public int Glifo;                         // 0 dispersa, 1 al acecho, 2 enjambre (−1: la red no da estado: dispersa)
            public bool ResumenFuerte;                // asedio o enjambre: la barra del resumen en hueso grueso
            public readonly List<string> Amenaza = new List<string>(), Propias = new List<string>(), SinNovedad = new List<string>(),
                Valoracion = new List<string>(), Necesidades = new List<string>();
            public string Fuentes;
        }

        static string Dtg(long tick) { return "D" + (tick / NetConst.DayTicks + 1) + " · " + NetConst.Clock(tick).Replace(":", "") + "A"; }

        /// <summary>Lado del que llega la masa alrededor de un nodo (la misma regla que la flecha de eje): media de direcciones pesada por la densidad
        /// en un anillo de 2 a 16 celdas; si no hay, la celda más cargada a menos de 32. ux, uy en celdas del campo (y crece hacia el sur).</summary>
        bool LadoHorda(RailNode nd, out double ux, out double uy)
        {
            ux = uy = 0;
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
            if (peso > 0 && sx * sx + sy * sy > 1e-6) { double m = System.Math.Sqrt(sx * sx + sy * sy); ux = sx / m; uy = sy / m; return true; }
            if (best > 0) { double m = System.Math.Sqrt(bx * bx + by * by); ux = bx / m; uy = by / m; return true; }
            return false;
        }

        /// <summary>«al norte», «al sureste»... (8 rumbos; el norte es arriba en el mapa: y del campo hacia abajo es el sur).</summary>
        static string Rumbo(double ux, double uy)
        {
            double ang = System.Math.Atan2(-uy, ux) * 180.0 / System.Math.PI;          // 0 = este, 90 = norte
            int k = (int)System.Math.Round(((ang % 360) + 360) % 360 / 45.0) % 8;
            return InformeTextos.Rumbos[k];
        }

        bool OjosEn(int node, out int pel, out int veh)
        {
            pel = veh = 0;
            foreach (var p in _a.Embarque.Lista)
                if (p.Estado == PasajeroEstado.EnEstacion && p.Node == node) { if (p.Tipo == PasajeroTipo.Peloton) pel++; else veh++; }
            return pel > 0 || veh > 0 || node == _a.Embarque.HomeNode || Nh90Encima(node);
        }

        bool Trebol(int node)
        {
            var n = _a.Graph.Nodes[node];
            if (n.State == NodeState.Perdida || n.HordeDensity < NetWorld.ThreatAt || n.HordeDensity >= NetWorld.SiegeAt) return false;
            foreach (var pl in _a.Director.Active) if (pl.Kind == HiveDirector.PlanKind.Asedio && pl.Node == node) return false;
            if (OjosEn(node, out _, out _)) return false;
            for (int i = 0; i < _a.Services.Count; i++) if (_a.TrainAtNode(_a.Services[i], node)) return false;
            return true;
        }

        void RehacerInforme()
        {
            var d = _inf; var g = _a.Graph; var T = InformeTextos.Es;
            d.Amenaza.Clear(); d.Propias.Clear(); d.SinNovedad.Clear(); d.Valoracion.Clear(); d.Necesidades.Clear();
            long now = _a.Tick;
            int dia = (int)(now / NetConst.DayTicks) + 1;
            d.Dtg = Dtg(now); d.DtgInicio = Dtg(System.Math.Max(0, now - NetConst.DayTicks));
            d.Num = T.Num(dia);
            int home = _a.Embarque.HomeNode;
            d.Mando = home >= 0 && home < g.Nodes.Count ? g.Nodes[home].Name : "—";
            int est = ColEstado(), sen = ColSenal();
            d.Glifo = est < 0 ? 0 : est;

            // ---- asedios (uno por nodo, el más cercano), del más peligroso al menos: los que ya atacan, luego por hora de llegada
            var ased = new List<HiveDirector.Plan>();
            foreach (var pl in _a.Director.Active)
            {
                if (pl.Kind != HiveDirector.PlanKind.Asedio || pl.Node < 0 || pl.Node >= g.Nodes.Count) continue;
                int k = ased.FindIndex(x => x.Node == pl.Node);
                if (k >= 0) { if (ased[k].Started || (!pl.Started && ased[k].StartTick <= pl.StartTick)) continue; ased.RemoveAt(k); }
                ased.Add(pl);
            }
            ased.Sort((a, b) => a.Started != b.Started ? (a.Started ? -1 : 1) : a.StartTick != b.StartTick ? a.StartTick.CompareTo(b.StartTick) : a.Node.CompareTo(b.Node));

            // ---- 2. resumen
            if (ased.Count > 0)
            {
                var p = ased[0]; bool ojos = OjosEn(p.Node, out _, out _);
                d.Resumen = T.ResumenAsedio(g.Nodes[p.Node].Name, p.Started ? null : NetConst.Clock(p.StartTick), Estimacion(_a.SiegeZombies(p.Node), ojos, est));
                d.ResumenFuerte = true;
            }
            else
            {
                int foco = -1, mx = 0;                                                   // la estación con más masa encima o cerca
                for (int k = 0; k < g.Nodes.Count; k++) if (g.Nodes[k].State != NodeState.Perdida && g.Nodes[k].HordeDensity > mx) { mx = g.Nodes[k].HordeDensity; foco = k; }
                string fn = foco >= 0 ? g.Nodes[foco].Name : null, dir = null, cifra = null;
                if (foco >= 0)
                {
                    if (LadoHorda(g.Nodes[foco], out var ux, out var uy)) dir = Rumbo(ux, uy);
                    cifra = Estimacion(_a.SiegeZombies(foco), OjosEn(foco, out _, out _), est);
                }
                if (est == 2 && fn != null) { d.Resumen = T.ResumenEnjambre(fn, cifra); d.ResumenFuerte = true; }
                else if (est == 1 && fn != null) { d.Resumen = T.ResumenAcecho(dir, fn, cifra, "D" + (dia + (NetConst.Clock(now).CompareTo("20:00") >= 0 ? 1 : 0))); d.ResumenFuerte = false; }
                else { d.Resumen = T.ResumenDispersa; d.ResumenFuerte = false; }
            }

            // ---- 3. situación de la amenaza (como mucho 3; la más peligrosa primero)
            foreach (var p in ased)
            {
                if (d.Amenaza.Count >= 3) break;
                var nd = g.Nodes[p.Node];
                string dir = null, rumbo = null;
                if (LadoHorda(nd, out var ux, out var uy)) { dir = Rumbo(ux, uy); rumbo = InformeTextos.SinAl(Rumbo(-ux, -uy)); }
                string cifra = Estimacion(_a.SiegeZombies(p.Node), OjosEn(p.Node, out _, out _), est);
                d.Amenaza.Add(p.Started ? InformeTextos.EnColor(T.AmenazaAtaca(nd.Name, dir, cifra), InformeTextos.ColorAtaque) : T.AmenazaMasa(nd.Name, dir, cifra, rumbo, Dtg(p.StartTick)));
            }
            var trebol = new List<int>();
            for (int k = 0; k < g.Nodes.Count; k++) if (Trebol(k)) trebol.Add(k);
            foreach (int k in trebol)
            {
                if (d.Amenaza.Count >= 3) break;
                string dir = LadoHorda(g.Nodes[k], out var ux, out var uy) ? Rumbo(ux, uy) : null;
                d.Amenaza.Add(T.AmenazaTrebol(g.Nodes[k].Name, dir));
            }

            // ---- 4. fuerzas propias: casa primero, luego las estaciones con tropa, y «sin tropa» en las amenazadas
            var conAlgo = new List<int>();
            if (home >= 0 && home < g.Nodes.Count) conAlgo.Add(home);
            foreach (var p in _a.Embarque.Lista) if (p.Estado == PasajeroEstado.EnEstacion && p.Node >= 0 && !conAlgo.Contains(p.Node)) conAlgo.Add(p.Node);
            foreach (int k in conAlgo)
            {
                OjosEn(k, out int pel, out int veh);
                d.Propias.Add(T.Propias(g.Nodes[k].Name, pel, veh, k == home, _a.Stock[k, (int)Res.Municion], _a.Stock[k, (int)Res.Viveres]));
            }
            foreach (var p in ased) if (!conAlgo.Contains(p.Node) && !_a.ZoneDefended(p.Node)) d.Propias.Add(T.SinTropa(g.Nodes[p.Node].Name));
            var aero = AerodromoVista.Estado;
            if (aero != null && aero.Aeronaves.Count > 0)
            {
                int enBase = 0, rearma = 0;
                foreach (var x in aero.Aeronaves) { if (x.Estado == AeronaveEstado.EnBase) enBase++; else if (x.Estado == AeronaveEstado.Rearmando || x.Estado == AeronaveEstado.TomandoTierra) rearma++; }
                d.Propias.Add(T.Betera(enBase, rearma));
            }

            // ---- 5. sin novedad en
            var quietas = new List<string>();
            for (int k = 0; k < g.Nodes.Count; k++)
            {
                var nd = g.Nodes[k];
                if (nd.State == NodeState.Perdida || nd.HordeDensity >= NetWorld.ThreatAt || ased.Exists(x => x.Node == k)) continue;
                quietas.Add(nd.Name);
            }
            if (quietas.Count > 0) d.SinNovedad.Add(T.SinNovedad(quietas));

            // ---- 6. valoración (una o dos frases) y 7. necesidades
            foreach (var zw in _a.UndefendedWarnings())
            {
                string nm = g.Nodes[zw.Node].Name;
                if (d.Valoracion.Count < 2) { string v = T.ValSinDefensa(nm, zw.Started ? null : Dtg(now + zw.EtaTicks), zw.Started); d.Valoracion.Add(zw.Started ? InformeTextos.EnColor(v, InformeTextos.ColorAtaque) : v); }
            }
            foreach (var p in ased)                                                      // munición mínima ante el asedio (regla de Hugo, 08-11); en ámbar si falta (Lucía, 09-11)
            {
                int min = InformeTextos.MunicionMinima(_a.SiegeZombies(p.Node));
                long mun = _a.Stock[p.Node, (int)Res.Municion];
                bool tropa = _a.ZoneDefended(p.Node);
                if (tropa && mun >= min) continue;
                string l = tropa ? T.NecMunicion(min, g.Nodes[p.Node].Name) : T.NecTropa(min, g.Nodes[p.Node].Name);
                d.Necesidades.Add(mun < min ? InformeTextos.EnColor(l, InformeTextos.ColorFalta) : l);
            }
            for (int i = 0; i < _a.Services.Count; i++)
            {
                string se = Puertas.SinEscolta(_a, i); if (se == null) continue;
                string nombre = _a.Services[i].Train.Name, sitiada = InformeTextos.SitiadaDe(se);
                if (d.Valoracion.Count < 2) d.Valoracion.Add(T.ValTrenSinEscolta(nombre, sitiada));
                d.Necesidades.Add(T.NecEscolta(nombre));
            }
            if (d.Valoracion.Count < 2 && sen >= ColAvisoMil() && est != 2) d.Valoracion.Add(T.ValSeAgita);
            if (d.Valoracion.Count == 0) d.Valoracion.Add(T.ValNada);
            foreach (int k in trebol) d.Necesidades.Add(T.NecNh90(g.Nodes[k].Name));

            // ---- 8. fuentes: las de las entradas del informe del periodo (las fuentes de textos_informe.csv: Mora, Llorens...)
            var fu = new List<string>();
            if (_texts != null)
                for (int i = _a.Intel.Count - 1; i >= 0 && fu.Count < 3; i--)
                {
                    var e = _a.Intel[i]; if (e.Tick < now - NetConst.DayTicks) break;
                    _texts.Format(e, "es", out var src);
                    if (!string.IsNullOrEmpty(src) && !fu.Contains(src)) fu.Add(src);
                }
            d.Fuentes = fu.Count > 0 ? string.Join(", ", fu) : null;
        }

        // ================================================================ dibujo
        static readonly Color32 C_NAVY = new Color32(0x18, 0x1c, 0x30, 255), C_NAVY2 = new Color32(0x20, 0x26, 0x3c, 255), C_SLATE = new Color32(0x40, 0x44, 0x60, 255),
            C_BORDE = new Color32(0x35, 0x3a, 0x55, 255), C_HUESO = new Color32(0xd4, 0xd0, 0xcc, 255), C_GRIS = new Color32(0x94, 0x94, 0x8c, 255),
            C_NEGRO = new Color32(0, 0, 0, 255);

        static void Caja(Rect r, Color col)
        {
            if (_app6Px == null) { _app6Px = new Texture2D(1, 1); _app6Px.SetPixel(0, 0, Color.white); _app6Px.Apply(); }
            var c = GUI.color; GUI.color = col; GUI.DrawTexture(r, _app6Px); GUI.color = c;
        }

        void EstilosInforme(float s)
        {
            int px = Mathf.Max(12, Mathf.RoundToInt(19 * s));
            if (_infTxt != null && _infTxt.fontSize == px) return;
            GUIStyle Nuevo(int size, FontStyle fs, Color32 col, bool wrap)
            {
                var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(10, size), fontStyle = fs, richText = true, wordWrap = wrap, clipping = TextClipping.Overflow };
                st.normal.textColor = col; st.padding = new RectOffset(0, 0, 0, 0); st.margin = new RectOffset(0, 0, 0, 0);
                return st;
            }
            _infTit = Nuevo(Mathf.RoundToInt(30 * s), FontStyle.Bold, C_HUESO, false);
            _infNum = Nuevo(Mathf.RoundToInt(18 * s), FontStyle.Bold, C_GRIS, false);
            _infSec = Nuevo(Mathf.RoundToInt(15 * s), FontStyle.Bold, C_GRIS, false);
            _infGris = Nuevo(Mathf.RoundToInt(16 * s), FontStyle.Normal, C_GRIS, false);
            _infTxt = Nuevo(px, FontStyle.Normal, C_HUESO, true);
            _infRes = Nuevo(Mathf.RoundToInt(21 * s), FontStyle.Bold, C_HUESO, true);
        }

        /// <summary>Marco con remaches (V1): negro, slate a 1 u, navy a 2 u y un remache borde/hueso en cada esquina.</summary>
        static void Marco(Rect r, int U, bool remaches)
        {
            Caja(r, C_NEGRO);
            Caja(new Rect(r.x + U, r.y + U, r.width - 2 * U, r.height - 2 * U), C_SLATE);
            if (!remaches) return;
            foreach (var p in new[] { new Vector2(r.x + 4 * U, r.y + 4 * U), new Vector2(r.xMax - 5 * U, r.y + 4 * U), new Vector2(r.x + 4 * U, r.yMax - 5 * U), new Vector2(r.xMax - 5 * U, r.yMax - 5 * U) })
            { Caja(new Rect(p.x, p.y, 2 * U, 2 * U), C_BORDE); Caja(new Rect(p.x, p.y, U, U), C_HUESO); }
        }

        /// <summary>Glifo de la Colmena de 16×16 u (Lucía, 25:55): dispersa, puntos sueltos; al acecho, anillo; enjambre, punta. Sin color: hueso.</summary>
        static void GlifoColmena(float x, float y, int k, int estado)
        {
            void P(float gx, float gy) { Caja(new Rect(x + gx * k, y + gy * k, 2 * k, 2 * k), C_HUESO); }
            if (estado == 1) { for (int a = 0; a < 8; a++) { float t = a * Mathf.PI / 4; P((int)(7 + 5.5f * Mathf.Cos(t)), (int)(7 + 5.5f * Mathf.Sin(t))); } P(7, 7); }
            else if (estado == 2)
            {
                for (int gy = 3; gy <= 13; gy++)                                      // punta (1,3)-(15,8)-(1,13) con la muesca en (5,8), en filas de 1 u
                {
                    float t = gy <= 8 ? (gy - 3) / 5f : (13 - gy) / 5f;
                    float x0 = 1 + 4 * t, x1 = 1 + 14 * t;
                    if (x1 > x0) Caja(new Rect(x + x0 * k, y + gy * k, (x1 - x0) * k, k), C_HUESO);
                }
            }
            else foreach (var q in new[] { new Vector2(1, 2), new Vector2(9, 1), new Vector2(13, 7), new Vector2(4, 9), new Vector2(10, 12), new Vector2(1, 13) }) P(q.x, q.y);
        }

        float Seccion(float x, float y, float w, int num, string titulo, List<string> lineas, int U)
        {
            if (lineas.Count == 0) return y;                                          // una sección vacía no se muestra
            GUI.Label(new Rect(x, y, w, _infSec.fontSize * 1.6f), num + ". " + titulo.ToUpper(_es), _infSec);
            y += _infSec.fontSize * 1.6f;
            float lh = _infTxt.fontSize * 1.38f, sangria = 6 * U;
            foreach (var l in lineas)
            {
                if (string.IsNullOrEmpty(l)) continue;
                var gc = new GUIContent(l);
                float h = Mathf.Max(lh, _infTxt.CalcHeight(gc, w - sangria));
                Caja(new Rect(x + 2, y + _infTxt.fontSize * 0.5f, U + 1, U + 1), C_GRIS);  // viñeta cuadrada
                GUI.Label(new Rect(x + sangria, y, w - sangria, h), gc, _infTxt);
                y += h + U;
            }
            return y + 10 * U / 3f;
        }

        /// <summary>Se llama al final de OnGUI cuando el informe está abierto (en lugar del panel de avisos, la carga y el informe pequeño).</summary>
        void DrawInformeV1()
        {
            float s = Screen.height / 1080f;
            int U = Mathf.Max(1, Mathf.RoundToInt(3 * s));
            EstilosInforme(s);
            if (Time.unscaledTime - _infCacheT > 0.5f || _infCacheT < 0) { RehacerInforme(); _infCacheT = Time.unscaledTime; }
            var d = _inf; var T = InformeTextos.Es;

            float W = Screen.width, H = Screen.height, m = 24 * s, pad = 36 * s;
            var marco = new Rect(m, m, W - 2 * m, H - 2 * m);
            float x0 = marco.x + pad, ancho = marco.width - 2 * pad;

            // ---- el layout se calcula antes de pintar: la plancha deja un hueco para el mapa
            float y = marco.y + 30 * s;
            float yCab2 = y + _infTit.fontSize * 1.45f, yRes = yCab2 + _infGris.fontSize * 1.9f;
            float resH = 58 * s, yMapa = yRes + resH + 16 * s, yFin = marco.yMax - pad;
            float mw = ancho * 0.60f;
            var mapa = new Rect(x0, yMapa, mw, yFin - yMapa);

            // fondo y marco con remaches, menos la ventana del mapa (allí se ve el 3D)
            Marco(marco, U, true);
            var dentro = new Rect(marco.x + 2 * U, marco.y + 2 * U, marco.width - 4 * U, marco.height - 4 * U);
            Caja(new Rect(dentro.x, dentro.y, dentro.width, mapa.y - 2 * U - dentro.y), C_NAVY);                          // arriba
            Caja(new Rect(dentro.x, mapa.yMax + 2 * U, dentro.width, dentro.yMax - mapa.yMax - 2 * U), C_NAVY);            // abajo
            Caja(new Rect(dentro.x, mapa.y - 2 * U, mapa.x - 2 * U - dentro.x, mapa.height + 4 * U), C_NAVY);              // izquierda
            Caja(new Rect(mapa.xMax + 2 * U, mapa.y - 2 * U, dentro.xMax - mapa.xMax - 2 * U, mapa.height + 4 * U), C_NAVY); // derecha
            // marco «plano» del mapa: negro 1 u y slate 1 u por fuera de la ventana
            Caja(new Rect(mapa.x - 2 * U, mapa.y - 2 * U, mapa.width + 4 * U, U), C_NEGRO); Caja(new Rect(mapa.x - 2 * U, mapa.yMax + U, mapa.width + 4 * U, U), C_NEGRO);
            Caja(new Rect(mapa.x - 2 * U, mapa.y - 2 * U, U, mapa.height + 4 * U), C_NEGRO); Caja(new Rect(mapa.xMax + U, mapa.y - 2 * U, U, mapa.height + 4 * U), C_NEGRO);
            Caja(new Rect(mapa.x - U, mapa.y - U, mapa.width + 2 * U, U), C_SLATE); Caja(new Rect(mapa.x - U, mapa.yMax, mapa.width + 2 * U, U), C_SLATE);
            Caja(new Rect(mapa.x - U, mapa.y - U, U, mapa.height + 2 * U), C_SLATE); Caja(new Rect(mapa.xMax, mapa.y - U, U, mapa.height + 2 * U), C_SLATE);

            // ---- 1. cabecera
            var tit = new GUIContent(T.Titulo);
            float tw = _infTit.CalcSize(tit).x;
            GUI.Label(new Rect(x0, y, tw + 4, _infTit.fontSize * 1.4f), tit, _infTit);
            GUI.Label(new Rect(x0 + tw + 24 * s, y + 9 * s, 300 * s, _infNum.fontSize * 1.4f), d.Num, _infNum);
            var dtg = new GUIContent(d.Dtg); float dw = _infTit.CalcSize(dtg).x;
            GUI.Label(new Rect(x0 + ancho - dw, y, dw + 4, _infTit.fontSize * 1.4f), dtg, _infTit);
            GUI.Label(new Rect(x0, yCab2, ancho * 0.8f, _infGris.fontSize * 1.5f), T.DePeriodo(d.Mando, d.DtgInicio, d.Dtg), _infGris);
            var cl = new GUIContent(T.Clasificacion); float cw = _infSec.CalcSize(cl).x;
            var clr = new Rect(x0 + ancho - cw - 16 * s, yCab2 - 2 * s, cw + 16 * s, 22 * s);
            Caja(new Rect(clr.x, clr.y, clr.width, 2 * s), C_GRIS); Caja(new Rect(clr.x, clr.yMax - 2 * s, clr.width, 2 * s), C_GRIS);
            Caja(new Rect(clr.x, clr.y, 2 * s, clr.height), C_GRIS); Caja(new Rect(clr.xMax - 2 * s, clr.y, 2 * s, clr.height), C_GRIS);
            GUI.Label(new Rect(clr.x + 8 * s, clr.y + 3 * s, cw + 4, clr.height), cl, _infSec);

            // ---- 2. resumen: plancha navy2, barra hueso a la izquierda (más gruesa con asedio o enjambre) y glifo de la Colmena
            Caja(new Rect(x0, yRes, ancho, resH), C_NAVY2);
            Caja(new Rect(x0, yRes, d.ResumenFuerte ? 2 * U : U, resH), C_HUESO);
            int gk = Mathf.Max(1, Mathf.RoundToInt(2 * s));
            GlifoColmena(x0 + 14 * s, yRes + (resH - 16 * gk) * 0.5f, gk, d.Glifo);
            GUI.Label(new Rect(x0 + 60 * s, yRes + 6 * s, ancho - 70 * s, resH - 8 * s), d.Resumen, _infRes);

            // ---- leyenda del mapa, dentro de la ventana, abajo (sobre una tira navy para que se lea encima del 3D)
            var ley = new GUIContent(T.LeyendaMapa);
            float lh = _infGris.fontSize * 1.6f;
            Caja(new Rect(mapa.x, mapa.yMax - lh - 4 * s, mapa.width, lh + 4 * s), new Color32(0x18, 0x1c, 0x30, 210));
            GUI.Label(new Rect(mapa.x + 8 * s, mapa.yMax - lh, mapa.width - 16 * s, lh), ley, _infGris);

            // ---- 3 a 7: el parte, a la derecha
            float cx = mapa.xMax + 40 * s, cwid = x0 + ancho - cx;
            float yy = yMapa;
            yy = Seccion(cx, yy, cwid, 3, T.SecAmenaza, d.Amenaza, U);
            yy = Seccion(cx, yy, cwid, 4, T.SecPropias, d.Propias, U);
            yy = Seccion(cx, yy, cwid, 5, T.SecSinNovedad, d.SinNovedad, U);
            yy = Seccion(cx, yy, cwid, 6, T.SecValoracion, d.Valoracion, U);
            yy = Seccion(cx, yy, cwid, 7, T.SecNecesidades, d.Necesidades, U);

            // ---- 8. firma, abajo de la columna
            float fy = yFin - 36 * s;
            Caja(new Rect(cx, fy, cwid, 2 * s), C_BORDE);
            var fir = new GUIContent(T.Firma); float fw = _infRes.CalcSize(fir).x;
            var stF = new GUIStyle(_infRes) { wordWrap = false, fontSize = Mathf.RoundToInt(20 * s) };
            GUI.Label(new Rect(cx, fy + 10 * s, fw + 8, 28 * s), fir, stF);
            GUI.Label(new Rect(cx + 160 * s, fy + 13 * s, cwid - 160 * s, 24 * s), T.FirmaPie(d.Fuentes), _infGris);

            // ---- ayuda
            GUI.Label(new Rect(marco.x + pad, marco.yMax - 22 * s, 600 * s, 20 * s), "<color=#94948C>" + T.Ayuda + "</color>", _infSec);
        }
    }

    /// <summary>
    /// TODOS los textos de la pantalla del informe (Lucía, docs/informe-textos.md, 08-11). Para cambiarlos, solo aquí.
    /// Los marcados «Lucía, añadidos 08-11» son los casos que añadió después (asedio y ataque en curso, guarnición de casa).
    /// Pendiente: memoria de tráfico («{tramo}: el tren {n} pasa a la misma hora {k} días seguidos. Lo ha aprendido.») y de atención
    /// («{zona}: la Colmena se aparta de donde miramos y crece donde no.»): la red todavía no las expone a la vista (Nacho).
    /// </summary>
    public sealed class InformeTextos
    {
        public static readonly InformeTextos Es = new InformeTextos();

        public static readonly string[] Rumbos = { "al este", "al noreste", "al norte", "al noroeste", "al oeste", "al suroeste", "al sur", "al sureste" };
        public static string SinAl(string r) { return r != null && r.StartsWith("al ") ? r.Substring(3) : r; }

        /// <summary>Munición mínima de una estación ante un asedio (regla de Hugo, 08-11): redondear hacia arriba (horda ÷ 400) + 2 u, con la horda del asedio
        /// anunciado (SiegeZombies ya lleva la dificultad de la partida). Nules 2.400: 8 u; Sagunto normal 5.250: 16 u; difícil 5.700: 17 u.
        /// Los 20 u de Nules de la D-N179 son el objetivo de diseño del día 2, no esta regla.</summary>
        public static int MunicionMinima(int horda) { return horda <= 0 ? 2 : (horda + 399) / 400 + 2; }
        /// <summary>Color de la necesidad cuando la estación tiene menos munición que el mínimo: el ámbar de aviso (Lucía, 09-11, decisiones-noche.md).</summary>
        public const string ColorFalta = "e08c30";
        /// <summary>Las líneas de ataque en curso (amenaza y valoración) van en el rojo de ataque (Lucía, 09-11). El glifo de la Colmena sigue en hueso d4d0cc.</summary>
        public const string ColorAtaque = "982420";
        public static string EnColor(string l, string hex) { return "<color=#" + hex + ">" + l + "</color>"; }

        /// <summary>Saca la estación de «Sin escolta: {estación} está sitiada» (Puertas.SinEscolta).</summary>
        public static string SitiadaDe(string aviso)
        {
            const string a = "Sin escolta: ", b = " está sitiada";
            if (aviso == null) return null;
            int i = aviso.IndexOf(a, System.StringComparison.Ordinal), j = aviso.LastIndexOf(b, System.StringComparison.Ordinal);
            return i >= 0 && j > i ? aviso.Substring(i + a.Length, j - i - a.Length) : aviso;
        }

        // 1. cabecera
        public string Titulo = "INFORME DE INTELIGENCIA";
        public string Num(int dia) { return "INTSUM " + dia.ToString("00"); }        // un INTSUM por día
        public string DePeriodo(string mando, string ini, string fin) { return "De: Sección de Inteligencia · " + mando + "   ·   Periodo: " + ini + " – " + fin; }
        public string Clasificacion = "USO INTERNO";

        // 2. resumen
        public string ResumenDispersa = "La Colmena está dispersa. Sin amenaza concentrada sobre la red.";
        public string ResumenAcecho(string dir, string est, string cifra, string dia)
        { return "La Colmena está al acecho y se junta " + (dir != null ? dir + " de " : "cerca de ") + est + (cifra != null ? " (" + cifra + ")" : "") + ". Ataque probable la noche del " + dia + "."; }
        public string ResumenEnjambre(string est, string cifra) { return "ENJAMBRE. La Colmena marcha sobre " + est + (cifra != null ? " (" + cifra + ")" : "") + ". Ataque esta noche."; }
        public string ResumenAsedio(string est, string hora, string cifra)
        {
            // con el asedio ya empezado no hay hora: «Asedio sobre {estación} en curso. {cifra}.» (Lucía, añadidos 08-11)
            return "Asedio sobre " + est + (hora != null ? " a las " + hora : " en curso") + "." + (cifra != null ? " " + cifra + "." : "");
        }

        // 3. situación de la amenaza
        public string SecAmenaza = "Situación de la amenaza";
        public string AmenazaMasa(string est, string dir, string cifra, string rumbo, string dtg)
        { return est + ": masa" + (dir != null ? " " + dir : "") + (cifra != null ? ", " + cifra : "") + (rumbo != null ? ", rumbo " + rumbo : "") + ". Llegaría hacia " + dtg + "."; }
        public string AmenazaAtaca(string est, string dir, string cifra)                 // Lucía, añadidos 08-11
        { return est + ": la Colmena ataca" + (dir != null ? " por el " + SinAl(dir) : "") + "." + (cifra != null ? " " + cifra + "." : ""); }
        public string AmenazaTrebol(string est, string dir) { return est + ": movimiento sin identificar" + (dir != null ? " " + dir + " de la estación" : " cerca de la estación") + "."; }

        // 4. fuerzas propias
        public string SecPropias = "Fuerzas propias";
        public string Propias(string est, int pel, int veh, bool casa, long mun, long viv)
        {
            var sb = new System.Text.StringBuilder(est).Append(": ");
            if (pel > 0) sb.Append(pel).Append(pel == 1 ? " pelotón" : " pelotones");
            else if (casa) sb.Append("guarnición");                                       // Lucía, añadidos 08-11: hasta que la red exponga los pelotones de casa
            // {, Dragón}{, n VAMTAC}: la red no da el tipo de vehículo (Pasajero solo dice Vehiculo), así que esa parte queda vacía y no sale (Lucía, 09-11). veh queda para cuando Nacho lo exponga.
            sb.Append(pel > 0 || casa ? ". " : "").Append("Munición ").Append(mun).Append(" u");
            if (viv > 0) sb.Append(". Víveres ").Append(viv).Append(" u");
            return sb.Append('.').ToString();
        }
        public string SinTropa(string est) { return est + ": sin tropa."; }
        public string Betera(int enBase, int rearmando)
        { return "Bétera: " + enBase + " NH90 en base" + (rearmando == 1 ? ", uno rearmando" : rearmando > 1 ? ", " + rearmando + " rearmando" : "") + "."; }

        // 5. sin novedad
        public string SecSinNovedad = "Sin novedad en";
        public string SinNovedad(List<string> l) { return string.Join(", ", l) + "."; }

        // 6. valoración
        public string SecValoracion = "Valoración";
        public string ValSinDefensa(string est, string dtg, bool ataca)
        { return ataca ? est + ", sin tropa, está bajo ataque. Cae esta noche si no llega ayuda." /* Lucía, añadidos 08-11 */ : "Si " + est + " no recibe tropa y munición antes de " + dtg + ", cae esa noche."; }
        public string ValTrenSinEscolta(string tren, string est) { return "El tren " + tren + " llegará a " + est + " sitiada sin escolta. Se perderá con su carga."; }
        public string ValSeAgita = "La Colmena se agita. Si abrimos más nodos esta semana, pasará a enjambre.";
        public string ValNada = "Sin cambios previstos en las próximas 24 horas.";

        // 7. necesidades
        public string SecNecesidades = "Necesidades";
        public string NecTropa(int u, string est) { return "≥ " + u + " u y un pelotón en " + est + "."; }
        public string NecMunicion(int u, string est) { return "≥ " + u + " u en " + est + "."; }
        public string NecNh90(string est) { return "Un NH90 sobre " + est + " para identificar el movimiento."; }
        public string NecEscolta(string tren) { return "Escolta para el tren " + tren + "."; }

        // 8. firma
        public string Firma = "Cap. Ibarra";
        public string FirmaPie(string fuentes) { return "Inteligencia" + (fuentes != null ? " · fuentes: " + fuentes : ""); }

        public string LeyendaMapa = "rectángulo: propio · cúpula: NH90 · rombo: horda (discontinuo: anunciada; rojo: ataca) · trébol: sin identificar · «?»: sin ojos";
        public string Ayuda = "Tab o Esc: cerrar el informe · el mapa se sigue moviendo con WASD y la rueda";
    }
}
