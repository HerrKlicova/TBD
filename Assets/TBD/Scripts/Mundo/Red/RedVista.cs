using System.Collections.Generic;
using TBD.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TBD.Mundo
{
    /// <summary>
    /// Vista de la red del Hito 1 (Iker) sobre la escena del mundo de Marcos. Solo dibuja lo que dice la red y mete órdenes:
    /// - T: modo trazar. Arrastrar con el botón izquierdo dibuja la vía; la cadena de piezas del kit se ajusta en vivo (D-N209). Vista previa con TBD/ZumTrazado
    ///   (Bruno): banda hueso si vale; ámbar con aspa y causa si no. Al soltar se manda la orden. Esc cancela.
    /// - Con A y B unidas, la línea por defecto (4 cajas a B y 2 palés de vuelta, Hugo) se crea sola y el tren la recorre.
    /// - Vía: piezas FBX de Teo (Resources/Models/Red) pieza a pieza; si faltan, una cinta. Por encima de 900 m (MAPA), símbolo hueso con grosor (lámina de Bruno).
    /// Con el Mundo común (UsarMundo, lo llama la vista de Marcos), la red es Mundo.Red y todo va por Mundo.Encolar; sin él, RedVista lleva su propia Red.
    /// </summary>
    public sealed class RedVista : MonoBehaviour
    {
        MundoBootstrap _mb; Mundo _m; Red _propia;
        Red R { get { return _m != null ? _m.Red : _propia; } }
        int _tren = 0, _linea = -1, _tick; float _acc; bool _lineaPedida;
        readonly List<PointMm> _trazo = new List<PointMm>(); bool _modo, _arrastrando; string _causa; bool _valida; Vector3 _finPrevia;
        GameObject _previa; int _tramosDibujados; string _rechazoVisto, _costePrevia;
        readonly List<GameObject> _detalle = new List<GameObject>(), _simbolo = new List<GameObject>(); float _anchoSimbolo = -1f;
        readonly List<Transform> _vagones = new List<Transform>(); Vector3[] _posAnt, _posAct; Quaternion[] _rotAnt, _rotAct;
        Material _mBalasto, _mCarril, _mTren, _mPaleta;
        const float AlturaCarril = 0.72f;      // Teo: cabeza del carril a 0,72 m
        static readonly string[] VarianteVia = { "v0_Hormigon", "v1_Madera", "v2_Maleza" };      // las de vía en servicio (v3 Obra y v4 Dañada son estados)
        static readonly string[] VariantePaso = { "v0_Teja", "v1_FocosDobles", "v2_Abandonado", "v3_Militar", "v4_Catenaria" };

        /// <summary>La vista de Marcos le pasa el Mundo común: desde ahí, RedVista no simula ni crea nada por su cuenta.</summary>
        public void UsarMundo(Mundo m)
        {
            _m = m; _propia = null;
            m.MontarRedHito1();      // idempotente: si el escenario ya la montó, no hace nada (I-Mundo6: sin esto no había estaciones y el trazo se rechazaba en silencio)
            Debug.Log($"[RED] usando el Mundo común (Mundo.Red): {m.Red.Via.Estaciones.Count} estaciones, {m.Red.Trenes.Count} tren, {m.Red.Via.Carreteras.Count} trozos de carretera");
        }

        void Start()
        {
            _mb = GetComponent<MundoBootstrap>();
            _mBalasto = Lit(new Color32(0x60, 0x5c, 0x48, 255)); _mCarril = Lit(new Color32(0x38, 0x38, 0x3c, 255)); _mTren = Lit(new Color32(0x4a, 0x55, 0x3a, 255));
            var pal = Resources.Load<Texture2D>("Models/Red/T_Paleta32"); _mPaleta = Lit(Color.white); if (pal != null) { _mPaleta.SetTexture("_BaseMap", pal); _mPaleta.mainTexture = pal; }
            if (_m == null)
            {
                _propia = new Red();
                int a = _propia.AddEstacion("A", 1_060_000, 430_000); _propia.AddEstacion("B", 1_300_000, 1_640_000);      // posiciones_hito1.csv de Álvaro
                _propia.Almacen[a][Red.Municion] = 20;
                try { var csv = System.IO.Path.Combine(Application.streamingAssetsPath, "Hito1", "carreteras_hito1.csv"); if (System.IO.File.Exists(csv)) Debug.Log($"[RED] carreteras de Álvaro: {_propia.Via.CargarCarreterasCsv(System.IO.File.ReadAllText(csv))} trozos (sin el puente)"); }
                catch (System.Exception e) { Debug.LogWarning("[RED] carreteras_hito1.csv: " + e.Message); }
                _propia.NuevoTren("Tren 1", a);
            }
            MontarTren();
            { var ca = System.Environment.GetCommandLineArgs(); int fi = System.Array.IndexOf(ca, "-redfoco"); if (fi >= 0 && fi + 1 < ca.Length) { var f = ca[fi + 1].Split(','); if (f.Length == 2 && float.TryParse(f[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fx) && float.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fz)) _mb.Enfocar(fx, fz); } }      // QA: -redfoco x,z
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-redauto") >= 0) { _trazo.Clear(); var pa = Pos("A"); var pb = Pos("B");
                var args = System.Environment.GetCommandLineArgs(); int ic = System.Array.IndexOf(args, "-redcurva");
                if (ic >= 0)
                {   // prueba visual de las curvas de Teo: arco A→B desviado N m por el centro (Bézier cuadrática, 12 puntos); -redcurva [N], 250 por defecto, negativo al otro lado
                    long desv = 250_000; if (ic + 1 < args.Length && int.TryParse(args[ic + 1], out int dm)) desv = dm * 1000L;
                    long mx = (pa.X + pb.X) / 2, my = (pa.Y + pb.Y) / 2, dx = pb.X - pa.X, dy = pb.Y - pa.Y; long l = System.Math.Max(1, IMath.Dist((int)dx, (int)dy));
                    long cx = mx - dy * desv / l, cy = my + dx * desv / l;
                    for (int k = 0; k <= 12; k++) { long t = k, u = 12 - k; _trazo.Add(new PointMm((int)((u * u * pa.X + 2 * u * t * cx + t * t * pb.X) / 144), (int)((u * u * pa.Y + 2 * u * t * cy + t * t * pb.Y) / 144))); }
                    Debug.Log($"[RED] -redcurva: arco por ({cx / 1000}; {cy / 1000}) m");
                }
                else { _trazo.Add(pa); _trazo.Add(pb); }
                MandarTrazo(); _trazo.Clear(); Debug.Log("[RED] -redauto: vía A–B pedida"); }
            Debug.Log("[RED] vista de la red lista: T para trazar vía (arrastrar con el botón izquierdo), Esc cancela");
        }

        PointMm Pos(string est) { foreach (var e in R.Via.Estaciones) if (e.Nombre == est) { var n = R.Via.Nodos[e.Nodo]; return new PointMm(n.X, n.Y); } return new PointMm(0, 0); }
        static Material Lit(Color c) { var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")); m.color = c; return m; }

        void Update()
        {
            if (R == null) return;
            Entrada();
            if (_m == null)      // red propia: 30 ticks/s (1:1); -redx8 para probar
            {
                float x = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-redx8") >= 0 ? 8f : 1f;
                _acc += Time.deltaTime * x; int pasos = 0;
                while (_acc >= 1f / 30f && pasos < 40) { _acc -= 1f / 30f; pasos++; GuardarAnterior(); _propia.Step(_tick++); LeerTren(); }
            }
            else if (_m.Tick != _tick) { GuardarAnterior(); _tick = _m.Tick; LeerTren(); _acc = 0f; }
            else _acc += Time.deltaTime;
            if (R.UltimoRechazo != null && R.UltimoRechazo != _rechazoVisto)
            {
                _rechazoVisto = R.UltimoRechazo;
                if (R.UltimoRechazo.Contains(": no hay vía hasta")) Debug.Log("[RED] estado del tren: " + R.UltimoRechazo);      // Sara: es el estado del tren, no un rechazo de una orden (sale en la línea de abajo)
                else { _causa = R.UltimoRechazo; Debug.Log("[RED] la red rechaza: " + R.UltimoRechazo); }
            }
            if (R.Via.Tramos.Count > _tramosDibujados) { var tn = R.Via.Tramos[R.Via.Tramos.Count - 1]; Debug.Log($"[RED] vía trazada: tramo {tn.Id}, {tn.Piezas?.Length ?? 0} piezas, {tn.LongMm / 1000} m, {R.Via.PasosNivel.Count} pasos a nivel");
                if (tn.Piezas != null) { long qx = R.Via.Nodos[tn.A].X, qy = R.Via.Nodos[tn.A].Y; int qr = tn.RumboIni, nc = 0; string donde = ""; foreach (var pz in tn.Piezas) { if (Via.RadioMm(pz) > 0) { nc++; if (donde == "" ) donde = $" · primera curva ({pz}) en ({qx / 1000}; {qy / 1000}) m"; } Via.Avanzar(pz, ref qx, ref qy, ref qr); } Debug.Log($"[RED] curvas: {nc} de {tn.Piezas.Length}{donde}"); } }
            LineaSiUnidas();
            DibujarTramos(); Simbolo();
            InterpolarTren(_acc * 30f);
            Capturas();
        }

        // ---------------------------------------------------------------- órdenes (por Mundo.Encolar si hay Mundo común)
        void Mandar(TipoOrden t, int a = 0, int b = 0, int c = 0, int d = 0)
        {
            if (_m != null) { _m.Encolar(new Orden { Tick = _m.Tick + 1, Jugador = 1, Tipo = t, A = a, B = b, C = c, D = d }); return; }
            switch (t)      // misma lógica que Mundo.AplicarRed, sobre la red propia
            {
                case OrdenesRed.NuevaLinea:
                    {
                        var cA = new int[Red.Recursos]; cA[Red.Municion] = 4; var dA = new int[Red.Recursos]; dA[Red.Material] = -1;
                        var cB = new int[Red.Recursos]; cB[Red.Material] = 2; var dB = new int[Red.Recursos]; dB[Red.Municion] = -1;
                        _propia.CrearLinea("Línea " + (_propia.Lineas.Count + 1), new List<ParadaLinea> { new ParadaLinea { Estacion = a, Cargar = cA, Descargar = dA }, new ParadaLinea { Estacion = b, Cargar = cB, Descargar = dB } });
                        break;
                    }
                case OrdenesRed.AsignarTren: _propia.AsignarTren(a, b); break;
            }
        }

        void MandarTrazo()
        {
            if (_trazo.Count < 2) return;
            if (_m != null) { foreach (var p in _trazo) Mandar(OrdenesRed.PuntoVia, p.X, p.Y); Mandar(OrdenesRed.TrazarVia); Debug.Log($"[RED] trazo de {_trazo.Count} puntos mandado al Mundo"); return; }
            var r = _propia.TrazarVia(_trazo);
            Debug.Log(r.Ok ? $"[RED] vía trazada: tramo {r.Tramo}, {_propia.Via.Tramos[r.Tramo].Piezas.Length} piezas, {_propia.Via.Tramos[r.Tramo].LongMm / 1000} m, {_propia.Via.PasosNivel.Count} pasos a nivel" : "[RED] vía rechazada: " + r.Por);
            if (!r.Ok) _causa = r.Por;
        }

        // ---------------------------------------------------------------- trazado (D-N209: trazo libre, piezas en vivo)
        void Entrada()
        {
            var kb = Keyboard.current; var ms = Mouse.current; if (kb == null || ms == null) return;
            if (kb.tKey.wasPressedThisFrame) { _modo = !_modo; _trazo.Clear(); BorrarPrevia(); _causa = null; Debug.Log("[RED] modo trazar " + (_modo ? "activado" : "desactivado")); }
            if (kb.escapeKey.wasPressedThisFrame && _modo) { _modo = false; _arrastrando = false; _trazo.Clear(); BorrarPrevia(); }
            if (kb.lKey.wasPressedThisFrame && R.Via.Estaciones.Count >= 2) { Mandar(OrdenesRed.NuevaLinea, 0, 1, 4, 2); Mandar(OrdenesRed.AsignarTren, R.Lineas.Count, _tren); Debug.Log("[RED] L: línea A–B pedida desde la interfaz"); }
            if (kb.deleteKey.wasPressedThisFrame && _mb.PuntoSuelo(ms.position.ReadValue(), out var pb))      // Supr: borra el tramo de vía bajo el cursor (orden 106)
            {
                var q = Mm(pb); int mejor = -1; long md = 25_000L * 25_000L;
                foreach (var t in R.Via.Tramos) { if (t.Borrado) continue; foreach (var pt in t.Puntos) { long dx = pt.X - q.X, dy = pt.Y - q.Y, d2 = dx * dx + dy * dy; if (d2 < md) { md = d2; mejor = t.Id; } } }
                if (mejor >= 0) { Mandar(OrdenesRed.BorrarVia, mejor); Debug.Log($"[RED] Supr: borrar tramo {mejor} pedido"); } else Debug.Log("[RED] Supr: no hay vía bajo el cursor");
            }
            MundoBootstrap.TrazandoVia = _modo;
            if (!_modo) return;
            var pos = ms.position.ReadValue();
            if (ms.leftButton.wasPressedThisFrame || ms.leftButton.wasReleasedThisFrame) Debug.Log($"[RED] ratón {(ms.leftButton.isPressed ? "abajo" : "arriba")} en ({pos.x:0}; {pos.y:0}) · trazo {_trazo.Count} puntos");      // QA partida a mano
            if (ms.leftButton.wasPressedThisFrame && _mb.PuntoSuelo(pos, out var p0)) { _arrastrando = true; _trazo.Clear(); _trazo.Add(Mm(p0)); }
            if (_arrastrando && ms.leftButton.isPressed && _mb.PuntoSuelo(pos, out var p))
            {
                var q = Mm(p); var u = _trazo[_trazo.Count - 1];
                if (IMath.Dist(q.X - u.X, q.Y - u.Y) >= 5_000) { _trazo.Add(q); Previa(); }
            }
            if (_arrastrando && ms.leftButton.wasReleasedThisFrame)
            {
                _arrastrando = false;
                if (_trazo.Count >= 2 && _valida) { MandarTrazo(); BorrarPrevia(); _causa = null; }
                else if (_trazo.Count >= 2) Debug.Log("[RED] vía no válida, no se manda: " + _causa);
                _trazo.Clear();
            }
        }

        static PointMm Mm(Vector3 p) { return new PointMm(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.z * 1000f)); }
        Vector3 AMundo(PointMm p, float sobre = 0f) { float x = p.X / 1000f, z = p.Y / 1000f; return new Vector3(x, _mb.AlturaSuelo(x, z) + sobre, z); }
        /// <summary>Giro para un FBX de Teo (su +X de Blender llega a Unity como −X): el del rumbo más media vuelta.</summary>
        static Quaternion GiroTeo(int rumbo) { return Giro((rumbo + IMath.Turn / 2) & 0xFFFF); }
        static Quaternion Giro(int rumbo) { return Quaternion.Euler(0f, -rumbo * 360f / IMath.Turn, 0f); }      // rumbo 0 = +X; en Unity el yaw positivo gira +X hacia −Z

        /// <summary>Vista previa: la cadena de piezas sobre una copia de la vía (la simulación no se toca hasta soltar).</summary>
        void Previa()
        {
            var copia = new Red(); var w = new TBD.Net.SaveWriter(); R.Save(w); copia.Load(new TBD.Net.SaveReader(w.ToArray()));
            var r = copia.Via.TrazarLibre(_trazo);
            _valida = r.Ok; _causa = r.Ok ? null : r.Por;
            _costePrevia = null;      // Sara: con el margen justo, la vista previa enseña el coste y lo que le queda a la estación que paga
            if (r.Ok && copia.Via.Tramos[r.Tramo].Coste > 0) { var tn = copia.Via.Tramos[r.Tramo]; int pasos = 0; foreach (var pn in copia.Via.PasosNivel) if (pn.Tramo == tn.Id) pasos++;
                long curvasMm = 0; foreach (var pz in tn.Piezas) if (Via.RadioMm(pz) > 0) curvasMm += Via.LargoMm(pz) / 2;      // lo que añaden las curvas (×1,5)
                int extraCurvas = (int)(curvasMm / 1000), base_ = tn.Coste - extraCurvas - 200 * pasos;
                // Sara (S2): desglose corto para que cuadre a simple vista
                _costePrevia = $"{base_} m de vía + {extraCurvas} m por curvas{(pasos > 0 ? $" + {200 * pasos} m por {pasos} paso{(pasos > 1 ? "s" : "")} a nivel" : "")} · cuesta {Red.Pales(tn.Coste)} · a {copia.Via.Estaciones[tn.Pago].Nombre} le quedan {Red.Pales(copia.MaterialM(tn.Pago))}"; }
            var pts = r.Ok ? copia.Via.Tramos[r.Tramo].Puntos : _trazo.ToArray();
            BorrarPrevia(); _previa = new GameObject("Vía · vista previa");
            if (_valida)
            {
                Cinta(_previa.transform, pts, 0f, 3.6f, 0.9f, Trazado(ZumEstetica.Hueso, 0.55f, 0f), "Banda", true);
                Cinta(_previa.transform, pts, 0f, 0.5f, 1.0f, Trazado(ZumEstetica.Hueso, 1f, 6f), "Eje", true);
            }
            else
            {
                Cinta(_previa.transform, pts, 0f, 6.6f, 0.9f, Trazado(ZumEstetica.Contorno, 1f, 0f), "Contorno", true);
                Cinta(_previa.transform, pts, 0f, 6f, 1.0f, Trazado(ZumEstetica.Ambar, 1f, 0f), "Banda", true);
            }
            _finPrevia = AMundo(pts[pts.Length - 1], 1.5f);
            if (_causa != null && _causa.StartsWith("la vía tiene que empezar") && pts.Length > 0) _finPrevia = AMundo(pts[0], 1.5f);      // Sara/Iker: la ✕ va donde está el problema (el arranque), no al final del trazo
        }

        Material Trazado(Color c, float tramado, float guion)
        {
            var m = ZumEstetica.MaterialTrazado(c, tramado, guion);
            if (m == null) { m = new Material(Shader.Find("Sprites/Default")); m.color = c; }      // sin el shader de Bruno, color plano
            return m;
        }

        void BorrarPrevia() { if (_previa != null) Destroy(_previa); _previa = null; }

        void OnGUI()
        {
            GUI.depth = -10;      // encima de la pantalla del zum (MundoBootstrap dibuja la RT a pantalla completa)
            if (R == null) return;
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(13, Screen.height / 60), richText = true };
            float y = Screen.height - 70f;
            if (_linea >= 0 && _linea < R.Lineas.Count) GUI.Label(new Rect(16f, y - 26f, Screen.width - 32f, 28f), "<color=#d4d0cc>Línea A–B · " + R.TextoLinea(_linea) + $" · A {R.Almacen[0][Red.Municion]} cajas · B {R.Almacen[1][Red.Municion]} cajas</color>", st);
            foreach (var tr in R.Trenes) if (tr.Estado == EstadoTren.Parado && tr.Linea >= 0 && tr.Linea < R.Lineas.Count && tr.Parada < R.Lineas[tr.Linea].Paradas.Count)
            { GUI.Label(new Rect(16f, y - 52f, 1100f, 28f), $"<color=#e0b050>{tr.Nombre} parado: no hay vía hasta {R.Via.Estaciones[R.Lineas[tr.Linea].Paradas[tr.Parada].Estacion].Nombre}</color>", st); }
            GUI.Label(new Rect(16f, y, Screen.width - 32f, 28f), _modo ? "<color=#d4d0cc>Trazar vía: arrastra desde una estación o una vía con el botón izquierdo · Esc cancela" + (R.CuestaMaterial && R.Almacen.Count > 0 ? $" · material en A: {Red.Pales(R.MaterialM(0))} (1 palé = 100 m; las curvas cuentan ×1,5; paso a nivel, 200 m)" : "") + "</color>" : "<color=#8c8a86>T: trazar vía</color>", st);
            if (_modo && _causa == null && _costePrevia != null && _mb.CamaraMundo != null)
            {
                var sp = _mb.CamaraMundo.WorldToViewportPoint(_finPrevia);
                if (sp.z > 0f) GUI.Label(new Rect(16f, y - 78f, Screen.width - 32f, 28f), "<color=#d4d0cc>Vista previa: " + _costePrevia + "</color>", st);      // Sara: en la franja de abajo, nunca encima de estaciones o placas      // Sara: que no se corte por la derecha
            }
            if (_modo && _causa != null && _mb.CamaraMundo != null)
            {
                var sp = _mb.CamaraMundo.WorldToViewportPoint(_finPrevia);
                if (sp.z > 0f) { float sx = sp.x * Screen.width, sy = (1f - sp.y) * Screen.height; GUI.Label(new Rect(sx - 10f, sy - 14f, 30f, 28f), "<color=#e08c30><b>✕</b></color>", st); GUI.Label(new Rect(16f, y - 78f, Screen.width - 32f, 28f), "<color=#e08c30>" + _causa + "</color>", st); }
            }
        }

        // ---------------------------------------------------------------- línea por defecto en cuanto A y B están unidas
        void LineaSiUnidas()
        {
            if (_linea < 0 && R.Lineas.Count > 0) { _linea = 0; Debug.Log("[RED] línea A–B en marcha"); }
            if (_m != null) { if (!_lineaPedida && R.LineaAutoCreadaTick >= 0) { _lineaPedida = true; Debug.Log($"[RED] A y B unidas: la simulación crea la línea por defecto en el tick {R.LineaAutoCreadaTick} (4 cajas a B, 2 palés de vuelta) y el tren sale"); } return; }      // Mundo común: lo decide la simulación (Red.LineaAuto)
            if (_lineaPedida || R.Via.Estaciones.Count < 2 || MundoVista.Reproduciendo) return;      // Marcos (H2): al reproducir, las órdenes ya vienen en la grabación; si no, se meten otra vez en un tick que depende de los fotogramas
            if (R.Via.Ruta(R.Via.Estaciones[0].Nodo, R.Via.Estaciones[1].Nodo) == null) return;
            _lineaPedida = true;
            Mandar(OrdenesRed.NuevaLinea, 0, 1); Mandar(OrdenesRed.AsignarTren, R.Lineas.Count, _tren);
            Debug.Log("[RED] A y B unidas: línea por defecto (4 cajas a B, 2 palés de vuelta) y el tren sale");
        }

        // ---------------------------------------------------------------- vía: FBX de Teo pieza a pieza (o cinta) y, en MAPA, símbolo con grosor
        void DibujarTramos()
        {
            for (int i = 0; i < _detalle.Count && i < R.Via.Tramos.Count; i++)      // _detalle[i] es el tramo i: los borrados (orden 106) desaparecen
                if (_detalle[i] != null && R.Via.Tramos[i].Borrado) { Destroy(_detalle[i]); _detalle[i] = null; _anchoSimbolo = -1f; Debug.Log($"[RED] vía borrada: tramo {i}"); }
            while (_tramosDibujados < R.Via.Tramos.Count)
            {
                var t = R.Via.Tramos[_tramosDibujados++];
                var det = new GameObject($"Vía · tramo {t.Id} ({t.Piezas?.Length ?? 0} piezas)");
                if (!MontarPiezas(det.transform, t)) { Cinta(det.transform, t.Puntos, 0f, 3.4f, 0.25f, _mBalasto, "Balasto", false); Cinta(det.transform, t.Puntos, -0.75f, 0.12f, AlturaCarril, _mCarril, "Carril izquierdo", false); Cinta(det.transform, t.Puntos, 0.75f, 0.12f, AlturaCarril, _mCarril, "Carril derecho", false); }
                foreach (var pn in R.Via.PasosNivel) if (pn.Tramo == t.Id) MontarPasoNivel(det.transform, t, pn);
                _detalle.Add(det); _anchoSimbolo = -1f;
            }
        }

        /// <summary>Coloca el FBX de cada pieza desde el conector A del tramo. Teo: origen en el conector A, sale hacia +X; su curva gira a −Z en Unity (= nuestra Der).
        /// Nuestra Izq es la misma curva recorrida al revés: se pone en el final de la pieza, girada 180°. Variante por instancia, con semilla determinista.</summary>
        bool MontarPiezas(Transform padre, Via.Tramo t)
        {
            if (t.Piezas == null || t.Piezas.Length == 0) return false;
            if (Resources.Load<GameObject>("Models/Red/SM_Via_Recta10_v0_Hormigon_LOD1") == null) return false;
            long x = R.Via.Nodos[t.A].X, y = R.Via.Nodos[t.A].Y; int ru = t.RumboIni;
            for (int i = 0; i < t.Piezas.Length; i++)
            {
                var pz = t.Piezas[i]; long x0 = x, y0 = y; int r0 = ru; Via.Avanzar(pz, ref x, ref y, ref ru);
                string tipo = pz == Via.Pieza.Recta10 ? "Recta10" : pz == Via.Pieza.Recta2_5 ? "Recta2" : "CurvaR" + (Via.RadioMm(pz) / 1000);
                string var = VarianteVia[(int)(IMath.Mix((ulong)(t.Id * 1000 + i) ^ 0xBEEFUL) % (ulong)VarianteVia.Length)];
                var pre = Resources.Load<GameObject>($"Models/Red/SM_Via_{tipo}_{var}_LOD1"); if (pre == null) continue;
                // Comprobado en la captura del cruce (Mundo-I2): el FBX importado mira a −X de Unity (Blender→Unity gira 180° en vertical), así que se suma media vuelta;
                // y la curva de Teo, vista así, gira a la IZQUIERDA respecto a su avance (nuestra Izq). Nuestra Der = la misma curva desde el final, girada otros 180°.
                bool alReves = Via.GiroBam(pz) < 0;
                var go = Instantiate(pre, padre);
                if (!alReves) { go.transform.position = AMundo(new PointMm((int)x0, (int)y0)); go.transform.rotation = GiroTeo(r0); }
                else { go.transform.position = AMundo(new PointMm((int)x, (int)y)); go.transform.rotation = GiroTeo((ru + IMath.Turn / 2) & 0xFFFF); }
                Pintar(go);
            }
            return true;
        }

        /// <summary>Paso a nivel de Teo (SM_PasoNivel_&lt;ángulo&gt;): su vía va de A (0;0) a B (20 o 30; 0) con el cruce en el centro; se coloca centrado en el punto de cruce.</summary>
        void MontarPasoNivel(Transform padre, Via.Tramo t, Via.PasoNivel pn)
        {
            string var = VariantePaso[(int)(IMath.Mix((ulong)(pn.Tramo * 77 + pn.OffMm) ^ 0xCAFEUL) % (ulong)VariantePaso.Length)];
            int largo = pn.AnguloGrados >= 60 ? 20_000 : 30_000;
            var a = R.Via.Posicion(t.Id, Mathf.Max(0, pn.OffMm - largo / 2), out int rumbo);
            // Teo (hito1-red LEEME §1b): en XZ, t = vía de A a B, r = carretera con t·r > 0; t.x·r.z − t.z·r.x < 0 → pieza normal, > 0 → la simétrica «E».
            string lado = LadoPaso(pn, rumbo);
            var pre = Resources.Load<GameObject>($"Models/Red/SM_PasoNivel_{pn.AnguloGrados}{lado}_{var}_LOD1"); if (pre == null) { Debug.Log($"[RED] falta SM_PasoNivel_{pn.AnguloGrados}{lado}_{var} en Resources/Models/Red"); return; }
            Debug.Log($"[RED] paso a nivel: SM_PasoNivel_{pn.AnguloGrados}{lado}_{var} en ({pn.P.X / 1000}; {pn.P.Y / 1000}) m");
            var go = Instantiate(pre, padre); go.transform.position = AMundo(a); go.transform.rotation = GiroTeo(rumbo); Pintar(go);
        }

        /// <summary>«E» si la carretera queda del lado que pide la pieza simétrica de Teo; "" si va la normal. Sim X→x, Y→z de Unity (AMundo).</summary>
        string LadoPaso(Via.PasoNivel pn, int rumbo)
        {
            if (pn.Carretera < 0 || pn.Carretera >= R.Via.Carreteras.Count) return "";
            var c = R.Via.Carreteras[pn.Carretera]; if (c.Length < 2) return "";
            int k = 1; long best = long.MaxValue;      // tramo de carretera más cercano al cruce
            for (int i = 1; i < c.Length; i++) { long mx = (c[i - 1].X + (long)c[i].X) / 2 - pn.P.X, my = (c[i - 1].Y + (long)c[i].Y) / 2 - pn.P.Y; long d = mx * mx + my * my; if (d < best) { best = d; k = i; } }
            double tx = IMath.Cos(rumbo), tz = IMath.Sin(rumbo), rx = c[k].X - c[k - 1].X, rz = c[k].Y - c[k - 1].Y;
            if (tx * rx + tz * rz < 0) { rx = -rx; rz = -rz; }
            return tx * rz - tz * rx > 0 ? "E" : "";
        }

        void Pintar(GameObject go) { foreach (var r in go.GetComponentsInChildren<Renderer>()) { var ms = r.sharedMaterials; for (int k = 0; k < ms.Length; k++) ms[k] = _mPaleta; r.sharedMaterials = ms; } }

        /// <summary>MAPA (Bruno, 900 m o más): la vía como cinta hueso con grosor proporcional a la altura de la cámara; el detalle se apaga.</summary>
        void Simbolo()
        {
            float alt = _mb.AlturaCamara; bool mapa = alt >= ZumEstetica.Mapa;
            foreach (var d in _detalle) if (d != null) d.SetActive(!mapa);
            float ancho = Mathf.Clamp(alt * 0.004f, 3f, 16f);
            if (mapa && (_anchoSimbolo < 0f || Mathf.Abs(ancho - _anchoSimbolo) > _anchoSimbolo * 0.25f))
            {
                foreach (var s in _simbolo) if (s != null) Destroy(s); _simbolo.Clear(); _anchoSimbolo = ancho;
                foreach (var t in R.Via.Tramos) { if (t.Borrado) continue; var g = new GameObject($"Vía · símbolo {t.Id}"); Cinta(g.transform, t.Puntos, 0f, ancho * 1.25f, 2f, Trazado(ZumEstetica.Contorno, 1f, 0f), "Contorno", true); var mh = Trazado(ZumEstetica.Hueso, 1f, 0f); mh.renderQueue = 2461; Cinta(g.transform, t.Puntos, 0f, ancho, 2.2f, mh, "Hueso", true); _simbolo.Add(g); }      // Marcos: ZWrite Off y misma cola, el contorno tapaba el hueso (Sara, H3: línea negra); el hueso va después (cola 2461)
            }
            foreach (var s in _simbolo) if (s != null) s.SetActive(mapa);
        }

        /// <summary>Cinta sobre el terreno a lo largo de una polilínea (uv.x = metros, como pide TBD/ZumTrazado).</summary>
        void Cinta(Transform padre, PointMm[] pts, float desplaz, float ancho, float alto, Material m, string nombre, bool uvMetros)
        {
            var go = new GameObject(nombre); go.transform.SetParent(padre, false);
            var verts = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>(); float s = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                var p = AMundo(pts[i]); var a = AMundo(pts[Mathf.Max(0, i - 1)]); var b = AMundo(pts[Mathf.Min(pts.Length - 1, i + 1)]);
                if (i > 0) s += Vector3.Distance(AMundo(pts[i - 1]), p);
                var dir = (b - a); dir.y = 0f; dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward; var izq = new Vector3(-dir.z, 0f, dir.x);
                var c = p + izq * desplaz;
                verts.Add(c + izq * (ancho * 0.5f) + Vector3.up * alto); verts.Add(c - izq * (ancho * 0.5f) + Vector3.up * alto);
                uv.Add(new Vector2(uvMetros ? s : 0f, 0f)); uv.Add(new Vector2(uvMetros ? s : 0f, 1f));
                if (i > 0) { int k = verts.Count - 4; tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(verts); mesh.SetUVs(0, uv); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        // ---------------------------------------------------------------- tren: locomotora y dos vagones (2 × 3 huecos, Hugo), interpolado entre ticks
        void MontarTren()
        {
            var raiz = new GameObject("Tren 1").transform;
            for (int i = 0; i < 3; i++)
            {
                var v = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(v.GetComponent<Collider>());
                v.name = i == 0 ? "Locomotora" : "Vagón " + i; v.transform.SetParent(raiz, false); v.transform.localScale = new Vector3(3f, i == 0 ? 3.8f : 2.6f, i == 0 ? 16f : 13f);
                v.GetComponent<MeshRenderer>().sharedMaterial = _mTren; _vagones.Add(v.transform);
            }
            _posAnt = new Vector3[3]; _posAct = new Vector3[3]; _rotAnt = new Quaternion[3]; _rotAct = new Quaternion[3];
            LeerTren(); GuardarAnterior();
        }

        void GuardarAnterior() { for (int i = 0; i < 3; i++) { _posAnt[i] = _posAct[i]; _rotAnt[i] = _rotAct[i]; } }

        void LeerTren()
        {
            if (R.Trenes.Count <= _tren) return;
            var t = R.Trenes[_tren];
            for (int i = 0; i < 3; i++)
            {
                int atras = i == 0 ? 0 : 8_500 + (i - 1) * 14_000 + 6_500;      // centros de cada coche detrás de la cabeza
                var p = PosEnRuta(t, atras, out int rumbo);
                var dir = new Vector3(IMath.Cos(rumbo) / (float)IMath.One, 0f, IMath.Sin(rumbo) / (float)IMath.One);
                _posAct[i] = AMundo(p, AlturaCarril + (i == 0 ? 1.9f : 1.3f)); _rotAct[i] = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Vector3.forward);
            }
        }

        /// <summary>Punto de la ruta del tren «atras» mm por detrás de la cabeza (dentro del tramo actual; parado, junto al nodo).</summary>
        PointMm PosEnRuta(Tren t, int atras, out int rumbo)
        {
            rumbo = 0; var v = R.Via;
            if (t.Estado != EstadoTren.EnMarcha || t.Ruta.Count == 0)
            {
                foreach (var tr in v.Tramos) if (tr.A == t.Nodo || tr.B == t.Nodo) { bool alA = tr.A == t.Nodo; var q = v.Posicion(tr.Id, alA ? Mathf.Min(tr.LongMm, atras) : Mathf.Max(0, tr.LongMm - atras), out rumbo); if (!alA) rumbo = (rumbo + IMath.Turn / 2) & 0xFFFF; return q; }
                var n = v.Nodos[Mathf.Max(0, t.Nodo)]; return new PointMm(n.X, n.Y - atras);
            }
            var ps = t.Ruta[t.PasoI]; var tram = v.Tramos[ps.Tramo]; int off = Mathf.Max(0, t.OffMm - atras);
            var p = v.Posicion(ps.Tramo, ps.Directo ? off : tram.LongMm - off, out rumbo); if (!ps.Directo) rumbo = (rumbo + IMath.Turn / 2) & 0xFFFF;
            return p;
        }

        void InterpolarTren(float f)
        {
            f = Mathf.Clamp01(f);
            for (int i = 0; i < 3; i++) { _vagones[i].position = Vector3.Lerp(_posAnt[i], _posAct[i], f); _vagones[i].rotation = Quaternion.Slerp(_rotAnt[i], _rotAct[i], f); }
        }

        // QA: -redcapturas 20,60,120 = capturas a esos segundos reales (Captures/red_<s>s.png)
        readonly HashSet<float> _capHechas = new HashSet<float>();
        void Capturas()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-redcapturas"); if (i < 0 || i + 1 >= a.Length) return;
            foreach (var t in a[i + 1].Split(','))
                if (float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) && Time.realtimeSinceStartup >= s && _capHechas.Add(s))
                {
                    var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Captures")); System.IO.Directory.CreateDirectory(dir);
                    var f = System.IO.Path.Combine(dir, $"red_{s:0}s.png"); ScreenCapture.CaptureScreenshot(f);
                    var tr = R.Trenes.Count > _tren ? R.Trenes[_tren] : null;
                    Debug.Log($"[RED] captura {f}: tren {(tr != null ? tr.Estado.ToString() : "-")}, A {R.Almacen[0][Red.Municion]} cajas, B {R.Almacen[1][Red.Municion]} cajas, {(_linea >= 0 ? R.TextoLinea(_linea) : "sin línea")}");
                }
        }
    }
}
