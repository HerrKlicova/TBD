using System.Collections.Generic;
using UnityEngine;
using TBD.Sim;
using TBD.Units;

namespace TBD.View
{
    /// <summary>
    /// Control de unidades de la noche (Lucia, entregas/lucia/encargo-control-disparos-noche.md §2-§3; Eugenio, 7-nov): que se vea que unidades controlas, a donde van y en que formacion.
    /// - Anillo bajo el pelotón seleccionado + un punto de 2×2 px bajo cada soldado; doble anillo con muesca de proa en el vehiculo seleccionado.
    /// - Marca de destino hasta que llegan (anillo y aspa) y linea de ruta.
    /// - Clic derecho y arrastrar = dibujar el frente (como Total War): la linea fija el ancho y hacia donde miran (a la izquierda del arrastre); silueta fantasma de cada soldado.
    ///   Varios pelotones se reparten por la linea en el orden de sus tarjetas. Formacion: F1 linea, F2 cuña, F3 columna, F4 bloque (la activa se aplica a la seleccion).
    /// Solo vista y ordenes de UnitsWorld (Move/AttackMove/SetFormation con rumbo final): los huecos los calcula la simulacion, no la vista.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        Formation _curForm = Formation.Linea; Texture2D _ovTex; Vector3 _rmbW0; bool _rmbW0Ok, _rmbDragging; Vector3 _rmbW1;
        static readonly Color OvHueso = new Color32(0xd4, 0xd0, 0xcc, 255), OvAspa = new Color32(0xb4, 0xa0, 0x80, 255);

        void OvEnsure() { if (_ovTex == null) { _ovTex = new Texture2D(1, 1, TextureFormat.RGBA32, false); _ovTex.SetPixel(0, 0, Color.white); _ovTex.Apply(); } }
        void OvDot(Vector2 p, float s, Color c, float a) { OvEnsure(); var old = GUI.color; GUI.color = new Color(c.r, c.g, c.b, a); GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), _ovTex); GUI.color = old; }
        void OvLine(Vector2 a, Vector2 b, float w, Color c, float al)
        {
            OvEnsure(); var d = b - a; float len = d.magnitude; if (len < 0.5f) return;
            var m = GUI.matrix; var old = GUI.color; GUI.color = new Color(c.r, c.g, c.b, al);
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a); GUI.DrawTexture(new Rect(a.x, a.y - w * 0.5f, len, w), _ovTex); GUI.matrix = m; GUI.color = old;
        }
        void OvRing(Vector3 c, float r, float w, Color col, float al)
        {
            Vector2 prev = Vector2.zero; const int N = 32;
            for (int i = 0; i <= N; i++) { float a = i * Mathf.PI * 2f / N; var q = ToGui(new Vector3(c.x + Mathf.Cos(a) * r, 0.06f, c.z + Mathf.Sin(a) * r)); if (i > 0) OvLine(prev, q, w, col, al); prev = q; }
        }

        string _hintMsg = ""; float _hintUntil; bool _hintAmber;
        void HintMsg(string s, bool amber) { _hintMsg = s; _hintAmber = amber; _hintUntil = Time.unscaledTime + 4f; }
        bool _c90Aim; string _c90Msg = ""; float _c90MsgUntil;
        bool _vehFire = true; float _lastDigitT = -1f; int _lastDigit = -1;
        /// <summary>1-5 elige el pelotón (Mayus suma), 6-8 los vehiculos; pulsar dos veces seguidas centra la camara en ellos (D-N139).</summary>
        void NightSelectKeys(UnityEngine.InputSystem.Keyboard kb)
        {
            var keys = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key };
            for (int d = 0; d < keys.Length; d++)
            {
                if (!keys[d].wasPressedThisFrame) continue;
                bool dbl = _lastDigit == d && Time.unscaledTime - _lastDigitT < 0.4f; _lastDigit = d; _lastDigitT = Time.unscaledTime; Vector3? at = null;
                if (d < 5)
                {
                    if (d >= _uw.Platoons.Count) continue; var pl = _uw.Platoons[d]; _selVeh = null; if (!kb.leftShiftKey.isPressed) _sel.Clear(); if (!_sel.Contains(pl.Id)) _sel.Add(pl.Id);
                    if (dbl) { float cx = 0f, cy = 0f; int n = 0; foreach (var m in pl.Men) if (m.Alive) { cx += m.X; cy += m.Y; n++; } if (n > 0) at = new Vector3(cx / (1000f * n), 0f, cy / (1000f * n)); }
                }
                else
                {
                    int k = d - 5, vi = 0; Veh pick = null; foreach (var v in _vehs) { if (v.Go == null || v.Name == "VAMTAC 2") continue; if (vi++ == k) { pick = v; break; } }
                    if (pick == null) continue; _selVeh = pick; _sel.Clear(); if (dbl) at = new Vector3(pick.Pos.x, 0f, pick.Pos.y);
                }
                if (at.HasValue) _focus = at.Value;
                Sfx.Play("ui_clic", Vector3.zero, 0.5f, 0.05f);
            }
        }

        /// <summary>Menu de pausa (Esc): Continuar, Salir al mapa y Menu principal.</summary>
        void PauseMenuGUI()
        {
            OvEnsure(); float W = Screen.width, H = Screen.height, k = H / 1080f;
            var old = GUI.color; GUI.color = new Color(0.094f, 0.11f, 0.188f, 0.72f); GUI.DrawTexture(new Rect(0, 0, W, H), _ovTex); GUI.color = old;
            var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(44 * k) }; st.normal.textColor = OvHueso;
            GUI.Label(new Rect(0, H * 0.28f, W, 70 * k), "Pausa", st);
            var bs = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(26 * k) };
            float bw = 360 * k, bh = 58 * k, x = (W - bw) * 0.5f, y = H * 0.40f;
            if (GUI.Button(new Rect(x, y, bw, bh), "Continuar", bs)) _paused = false;
            if (GUI.Button(new Rect(x, y + bh + 14 * k, bw, bh), "Salir al mapa", bs)) { _paused = false; LeaveToMap(); }
            if (GUI.Button(new Rect(x, y + 2 * (bh + 14 * k), bw, bh), "Menú principal", bs)) { _paused = false; NetSession.Reset(); NightRun.Active = false; UnityEngine.SceneManagement.SceneManager.LoadScene("Menu"); }
        }

        /// <summary>Teclas de formacion (F1-F4) y arrastre del frente. Se llama en NightUpdate.</summary>
        void OrdersUpdate()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current; if (kb == null) return;
            Formation? nf = null; if (kb.f1Key.wasPressedThisFrame) nf = Formation.Linea; if (kb.f2Key.wasPressedThisFrame) nf = Formation.Cuna; if (kb.f3Key.wasPressedThisFrame) nf = Formation.Columna; if (kb.f4Key.wasPressedThisFrame) nf = Formation.Bloque;   // Lucía (7-nov): el bloque sustituye al círculo
            if (nf.HasValue) { _curForm = nf.Value; foreach (var id in _sel) _uw.SetFormation(id, _curForm); Debug.Log("[NOCHE] formacion " + _curForm); }
        }

        /// <summary>Orden de frente: de g0 a g1 (suelo, metros). El pelotón mira a la izquierda del arrastre.</summary>
        void IssueFormationOrder(Vector3 g0, Vector3 g1, bool attack)
        {
            var d = new Vector2(g1.x - g0.x, g1.z - g0.z); float L = d.magnitude;
            if (_selVeh != null || _sel.Count == 0) return;
            var f = new Vector2(-d.y, d.x) / Mathf.Max(0.01f, L); int heading = IMath.Atan2((int)(f.y * 1000f), (int)(f.x * 1000f));
            var ids = new List<int>(); foreach (var pl in _uw.Platoons) if (_sel.Contains(pl.Id)) ids.Add(pl.Id);
            int n = ids.Count; if (n == 0) return;
            for (int i = 0; i < n; i++)
            {
                var p = _uw.Find(ids[i]); if (p == null) continue; float t = (i + 0.5f) / n; var c = g0 + (g1 - g0) * t; int men = Mathf.Max(1, p.AliveCount());
                p.SpacingMm = Mathf.Clamp((int)(L / n * 1000f / men), 1200, 4000); _uw.SetFormation(ids[i], _curForm);
                if (attack) _uw.AttackMove(ids[i], (int)(c.x * 1000f), (int)(c.z * 1000f), heading); else _uw.Move(ids[i], (int)(c.x * 1000f), (int)(c.z * 1000f), heading);
            }
            _moveMark = (g0 + g1) * 0.5f; _moveMarkUntil = Time.unscaledTime + 1.2f;
            Sfx.Play("ui_orden", Vector3.zero, 0.7f, 0.05f);
        }

        /// <summary>Capa de pista visual (IMGUI): anillos, puntos por soldado, destinos, ruta y silueta fantasma del frente.</summary>
        void NightOverlay()
        {
            if (_uw == null || _worldCam == null) return;
            foreach (var pl in _uw.Platoons)
            {
                if (pl.AliveCount() == 0) continue; bool sel = _selVeh == null && _sel.Contains(pl.Id);
                float cx = 0f, cy = 0f; int n = 0; foreach (var m in pl.Men) if (m.Alive) { cx += m.X; cy += m.Y; n++; }
                cx /= 1000f * n; cy /= 1000f * n; var cen = new Vector3(cx, 0f, cy);
                if (sel)
                {
                    float r = 3.5f; foreach (var m in pl.Men) if (m.Alive) r = Mathf.Max(r, Vector2.Distance(new Vector2(m.X / 1000f, m.Y / 1000f), new Vector2(cx, cy)) + 1.6f);
                    OvRing(cen, r, 2f, OvHueso, 0.85f);
                    foreach (var m in pl.Men) if (m.Alive) OvDot(ToGui(new Vector3(m.X / 1000f, 0.05f, m.Y / 1000f)), 3f, OvHueso, 0.9f);
                }
                if (pl.Moving)                                                       // destino y ruta hasta que lleguen
                {
                    var dst = new Vector3(pl.TX / 1000f, 0.06f, pl.TY / 1000f); var a = ToGui(cen); var b = ToGui(dst);
                    int segs = Mathf.Max(2, (int)(Vector2.Distance(a, b) / 14f)); for (int i = 0; i < segs; i += 2) OvLine(Vector2.Lerp(a, b, i / (float)segs), Vector2.Lerp(a, b, (i + 1) / (float)segs), 1.5f, OvHueso, sel ? 0.6f : 0.25f);
                    if (sel) { OvRing(dst, 1.3f, 2f, OvHueso, 0.9f); var q = ToGui(dst); OvLine(q + new Vector2(-6, -6), q + new Vector2(6, 6), 2f, OvHueso, 0.9f); OvLine(q + new Vector2(-6, 6), q + new Vector2(6, -6), 2f, OvHueso, 0.9f); }
                }
            }
            if (_selVeh != null)                                                     // vehiculo: doble anillo con muesca de proa
            {
                var v = _selVeh; var c = new Vector3(v.Pos.x, 0f, v.Pos.y);
                OvRing(c, 3.6f, 2f, OvHueso, 0.9f); OvRing(c, 3.0f, 2f, OvHueso, 0.6f);
                float hd = v.Heading * Mathf.Deg2Rad; var fw = new Vector3(Mathf.Sin(hd), 0f, Mathf.Cos(hd)); OvLine(ToGui(c + fw * 3.0f), ToGui(c + fw * 4.4f), 3f, OvHueso, 0.95f);
                if (v.HasTarget) { var dst = new Vector3(v.Target.x, 0.06f, v.Target.y); OvRing(dst, 1.3f, 2f, OvHueso, 0.9f); }
            }
            if (Time.unscaledTime < _moveMarkUntil) { float k = (_moveMarkUntil - Time.unscaledTime) / 1.2f; OvRing(_moveMark, 0.8f + (1f - k) * 1.6f, 2f, OvHueso, k); }
            // arrastre del frente: linea + silueta fantasma
            if (_rmbDragging && _rmbW0Ok && _sel.Count > 0 && _selVeh == null)
            {
                var g0 = _rmbW0; var g1 = _rmbW1; var d = new Vector2(g1.x - g0.x, g1.z - g0.z); float L = d.magnitude; if (L > 0.5f)
                {
                    OvLine(ToGui(g0), ToGui(g1), 2f, OvHueso, 0.8f);
                    var f = new Vector2(-d.y, d.x) / L; int heading = IMath.Atan2((int)(f.y * 1000f), (int)(f.x * 1000f));
                    var ids = new List<int>(); foreach (var pl in _uw.Platoons) if (_sel.Contains(pl.Id)) ids.Add(pl.Id); int np = ids.Count;
                    for (int i = 0; i < np; i++)
                    {
                        var p = _uw.Find(ids[i]); if (p == null) continue; var c = g0 + (g1 - g0) * ((i + 0.5f) / np); int men = Mathf.Max(1, p.AliveCount());
                        int sp = Mathf.Clamp((int)(L / np * 1000f / men), 1200, 4000); int k = 0;
                        foreach (var m in p.Men) if (m.Alive)
                        {
                            UnitsWorld.Slot(_curForm, k, men, sp, heading, out int dx, out int dy); k++;
                            var w = new Vector3(c.x + dx / 1000f, 0.05f, c.z + dy / 1000f); bool bad = w.x < Level.FortX0 + 1f || w.x > Level.FortX1 - 1f || w.z < Level.FortY0 + 1f || w.z > Level.FortY1 - 1f;
                            var q = ToGui(w); if (bad) { OvLine(q + new Vector2(-3, -3), q + new Vector2(3, 3), 2f, OvAspa, 0.9f); OvLine(q + new Vector2(-3, 3), q + new Vector2(3, -3), 2f, OvAspa, 0.9f); } else OvDot(q, 3f, OvHueso, 0.55f);
                        }
                        var ctr = ToGui(c); var fw = ToGui(c + new Vector3(f.x, 0f, f.y) * 2.2f); OvLine(ctr, fw, 2f, OvHueso, 0.7f);
                    }
                }
            }
            // recuadro de seleccion al arrastrar con el boton izquierdo (hueso, relleno tenue), por encima del mundo
            if (_selDrag && UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
            {
                var mpq = UnityEngine.InputSystem.Mouse.current.position.ReadValue(); var ds = mpq - _selStart;
                if (ds.magnitude > 6f)
                {
                    float x0 = Mathf.Min(_selStart.x, mpq.x), x1 = Mathf.Max(_selStart.x, mpq.x), y0 = Screen.height - Mathf.Max(_selStart.y, mpq.y), y1 = Screen.height - Mathf.Min(_selStart.y, mpq.y);
                    OvEnsure(); var oldc = GUI.color; GUI.color = new Color(OvHueso.r, OvHueso.g, OvHueso.b, 0.12f); GUI.DrawTexture(Rect.MinMaxRect(x0, y0, x1, y1), _ovTex); GUI.color = oldc;
                    OvLine(new Vector2(x0, y0), new Vector2(x1, y0), 2f, OvHueso, 0.95f); OvLine(new Vector2(x0, y1), new Vector2(x1, y1), 2f, OvHueso, 0.95f);
                    OvLine(new Vector2(x0, y0), new Vector2(x0, y1), 2f, OvHueso, 0.95f); OvLine(new Vector2(x1, y0), new Vector2(x1, y1), 2f, OvHueso, 0.95f);
                }
            }
            if (Time.unscaledTime < _c90MsgUntil) { var cst = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 45), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; cst.normal.textColor = OvHueso; GUI.Label(new Rect(0, Screen.height * 0.12f, Screen.width, 40f), _c90Msg, cst); }
            if (Time.unscaledTime < _hintUntil) { var hs = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(13, Screen.height / 50), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft }; hs.normal.textColor = _hintAmber ? SgAmbar : OvHueso; var hr = new Rect(16f, 120f * Screen.height / 1080f, 420f * Screen.height / 1080f, 34f); OvEnsure(); var oc = GUI.color; GUI.color = new Color(0.094f, 0.11f, 0.188f, 0.85f); GUI.DrawTexture(hr, _ovTex); GUI.color = oc; GUI.Label(new Rect(hr.x + 10f, hr.y, hr.width, hr.height), _hintMsg, hs); }
            TramosGUI();
            // formacion activa
            { var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(11, Screen.height / 70), wordWrap = false, clipping = TextClipping.Overflow }; st.normal.textColor = OvHueso; if (_sel.Count > 0 && _selVeh == null) GUI.Label(new Rect(12f, Screen.height * 0.80f - st.fontSize * 1.6f, Screen.width * 0.6f, st.fontSize * 1.6f), "Formación " + _curForm + "  (F1 línea · F2 cuña · F3 columna · F4 bloque) · clic derecho y arrastrar = frente", st); }
        }
    }
}
