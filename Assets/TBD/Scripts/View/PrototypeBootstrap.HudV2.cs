using System.Collections.Generic;
using UnityEngine;
using TBD.Sim;
using TBD.Units;
using TBD.View.UiNoche;

namespace TBD.View
{
    /// <summary>
    /// Interfaz v2, estilo 1 de Bruno (D-N147, entregas/bruno-interfaz-v2/MEDIDAS_ESTILO_1.md), version basica: franja inferior con 8 tarjetas (pelotones 1-5, vehiculos 6-8)
    /// con siluetas por hombre, municion y estado, barra de atajos y panel de ordenes 2x2 (formaciones). Coordenadas a 1080p escaladas por Screen.height / 1080.
    /// Es IMGUI sobre el kit de la noche (el HUD de recursos, reloj, avisos y parte siguen siendo los de siempre); la tipografia Inter y el UI Toolkit quedan para despues.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        Texture2D _v2Px; float _v2Last; int _v2LastIdx = -1;
        static readonly Color V2Fondo = new Color(0.094f, 0.110f, 0.188f, 0.85f), V2Tarjeta = new Color32(0x20, 0x25, 0x38, 255), V2Borde = new Color32(0x35, 0x3a, 0x55, 255),
            V2Hueso = new Color32(0xd4, 0xd0, 0xcc, 255), V2Sec = new Color32(0x94, 0x94, 0x8c, 255), V2Hueco = new Color32(0x0c, 0x0e, 0x18, 255), V2Mun = new Color32(0xb4, 0xa0, 0x80, 255), V2Ambar = new Color32(0xe0, 0x8c, 0x30, 255);

        void V2Rect(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, _v2Px); GUI.color = o; }
        void V2Box(Rect r, Color fill, Color border, float bw) { V2Rect(r, border); V2Rect(new Rect(r.x + bw, r.y + bw, r.width - 2 * bw, r.height - 2 * bw), fill); }
        void V2Tex(string name, float x, float y, float w, float h) { var t = UiNocheKit.Tex(name); if (t == null) return; t.filterMode = FilterMode.Point; GUI.DrawTexture(new Rect(x, y, w, h), t); }
        void V2Txt(string s, float x, float y, float w, int size, FontStyle fs, Color c, TextAnchor a, float k)
        {
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(8, Mathf.RoundToInt(size * k)), fontStyle = fs, alignment = a, wordWrap = false, clipping = TextClipping.Overflow, richText = false };
            st.normal.textColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.gamma : c; GUI.Label(new Rect(x, y, w, size * 1.6f * k), s, st);
        }

        /// <summary>Dibuja la franja inferior v2. Devuelve true si la ha pintado.</summary>
        bool NightHudV2()
        {
            if (_ng == null || _uw == null) return false;
            if (_v2Px == null) { _v2Px = new Texture2D(1, 1, TextureFormat.RGBA32, false); _v2Px.SetPixel(0, 0, Color.white); _v2Px.Apply(); }
            float k = Screen.height / 1080f, ox = (Screen.width - 1920f * k) * 0.5f;
            Rect R(float x, float y, float w, float h) { return new Rect(ox + x * k, y * k, w * k, h * k); }
            // franja inferior
            V2Rect(R(-ox / k, 940, Screen.width / k, 140), V2Fondo); V2Rect(R(-ox / k, 940, Screen.width / k, 1), V2Borde);
            // barra de atajos
            V2Rect(R(316, 924, 1274, 16), new Color(V2Hueco.r, V2Hueco.g, V2Hueco.b, 0.67f));
            V2Txt("1-5 pelotones · 6-8 vehículos · clic derecho mueve, arrastrar dibuja el frente · F1-F4 formación · G fuego vehículos · T recargar · Esc pausa", ox + 316 * k, 924 * k, 1274 * k, 11, FontStyle.Normal, V2Sec, TextAnchor.UpperCenter, k);
            var ev = Event.current; int idxClick = -1;
            // tarjetas
            var cards = new List<(string name, string key, Platoon pl, Veh v)>();
            int pi = 0; foreach (var p in _uw.Platoons) { if (pi >= 5) break; cards.Add((_pname.TryGetValue(p.Id, out var nm) ? nm : "Pelotón", (pi + 1).ToString(), p, null)); pi++; }
            foreach (var v in _vehs) { if (v.Go == null || v.Name == "VAMTAC 2") continue; if (cards.Count >= 8) break; cards.Add((v.Name, (cards.Count + 1).ToString(), null, v)); }
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i]; float x = 316 + i * 160, y = 946; bool sel = c.pl != null ? (_selVeh == null && _sel.Contains(c.pl.Id)) : (_selVeh == c.v);
                if (sel) V2Box(R(x - 2, y - 2, 156, 132), new Color(0, 0, 0, 0), V2Hueso, 2f);
                V2Box(R(x, y, 152, 128), V2Tarjeta, V2Borde, 1f);
                V2Box(R(x + 6, y + 6, 18, 18), V2Hueco, V2Borde, 1f); V2Txt(c.key, ox + (x + 6) * k, (y + 6) * k, 18 * k, 13, FontStyle.Bold, V2Hueso, TextAnchor.MiddleCenter, k);
                V2Txt(c.name, ox + (x + 30) * k, (y + 7) * k, 120 * k, 14, FontStyle.Bold, V2Hueso, TextAnchor.UpperLeft, k);
                if (c.pl != null)
                {
                    var p = c.pl; string fi = p.Form == Formation.Linea ? "ui_orden_linea_16" : p.Form == Formation.Columna ? "ui_orden_columna_16" : p.Form == Formation.Cuna ? "ui_orden_cuna_16" : "ui_orden_bloque_16";
                    V2Tex(fi, ox + (x + 134) * k, (y + 9) * k, 12 * k, 12 * k);
                    int n = 0; foreach (var m in p.Men) { if (n >= 8) break; string tp = m.Kind == InfKind.Ametrallador ? "ametrallador" : m.Kind == InfKind.Tirador ? "tirador" : "fusilero"; V2Tex("ui_sil_" + tp + (m.Alive ? "_viva_10x20" : "_caida_10x20"), ox + (x + 8 + n * 17) * k, (y + 32) * k, 10 * k, 20 * k); n++; }
                    float am = p.AmmoCap > 0 ? p.AmmoShots / (float)p.AmmoCap : 0f; int alive = p.AliveCount();
                    V2Txt("Munición", ox + (x + 8) * k, (y + 60) * k, 80 * k, 11, FontStyle.Normal, V2Sec, TextAnchor.UpperLeft, k); V2Txt(Mathf.RoundToInt(am * 100f) + " %", ox + (x + 8) * k, (y + 60) * k, 136 * k, 11, FontStyle.Normal, V2Hueso, TextAnchor.UpperRight, k);
                    V2Rect(R(x + 8, y + 76, 136, 5), V2Hueco); V2Rect(R(x + 8, y + 76, 136 * Mathf.Clamp01(am), 5), am < 0.25f ? V2Ambar : V2Mun);
                    if (p.Kind == PlatoonKind.Fusileros) V2Txt("C90 x" + p.C90Left + "  (C)", ox + (x + 8) * k, (y + 88) * k, 136 * k, 11, FontStyle.Normal, p.C90Left > 0 ? V2Hueso : V2Sec, TextAnchor.UpperLeft, k);
                    string st = alive == 0 ? "Sin efectivos" : am <= 0.01f ? "Sin munición" : p.Moving ? "En marcha" : p.Deploy ? "Emplazado" : "En posición"; Color sc = (am <= 0.01f && alive > 0) ? V2Ambar : V2Hueso;
                    V2Txt(st, ox + (x + 8) * k, (y + 104) * k, 136 * k, 13, FontStyle.Bold, sc, TextAnchor.UpperLeft, k);
                }
                else
                {
                    var v = c.v; string vt = v.Heli ? "ui_veh_nh90_64x22" : v.Name.StartsWith("Drag") ? "ui_veh_dragon_56x22" : "ui_veh_vamtac_48x22"; float vw = v.Heli ? 64 : v.Name.StartsWith("Drag") ? 56 : 48;
                    V2Tex(vt, ox + (x + (152 - vw * 1.5f) * 0.5f) * k, (y + 30) * k, vw * 1.5f * k, 33 * k);
                    V2Txt("Munición", ox + (x + 8) * k, (y + 78) * k, 80 * k, 11, FontStyle.Normal, V2Sec, TextAnchor.UpperLeft, k); V2Txt(Mathf.RoundToInt(v.Ammo * 100f) + " %", ox + (x + 8) * k, (y + 78) * k, 136 * k, 11, FontStyle.Normal, V2Hueso, TextAnchor.UpperRight, k);
                    V2Rect(R(x + 8, y + 93, 136, 4), V2Hueco); V2Rect(R(x + 8, y + 93, 136 * Mathf.Clamp01(v.Ammo), 4), v.Ammo < 0.25f ? V2Ambar : V2Mun);
                    string st = v.Ammo <= 0.01f ? "Sin munición" : v.ResupT >= 0 ? "Recargando" : v.Heli ? (v.State == 0 ? "En tierra" : "En vuelo · " + Mathf.RoundToInt(v.Alt) + " m") : (v.HasTarget ? "En marcha" : "Parado");
                    V2Txt(st, ox + (x + 8) * k, (y + 104) * k, 136 * k, 13, FontStyle.Bold, v.Ammo <= 0.01f ? V2Ambar : V2Hueso, TextAnchor.UpperLeft, k);
                }
                if (GUI.Button(R(x, y, 152, 128), GUIContent.none, GUIStyle.none)) idxClick = i;
            }
            if (idxClick >= 0)
            {
                var c = cards[idxClick]; bool dbl = _v2LastIdx == idxClick && Time.unscaledTime - _v2Last < 0.4f; _v2LastIdx = idxClick; _v2Last = Time.unscaledTime;
                if (c.pl != null) { _selVeh = null; if (!(ev != null && ev.shift)) _sel.Clear(); if (!_sel.Contains(c.pl.Id)) _sel.Add(c.pl.Id); if (dbl) { float cx = 0f, cy = 0f; int n = 0; foreach (var m in c.pl.Men) if (m.Alive) { cx += m.X; cy += m.Y; n++; } if (n > 0) _focus = new Vector3(cx / (1000f * n), 0f, cy / (1000f * n)); } }
                else { _selVeh = c.v; _sel.Clear(); if (dbl) _focus = new Vector3(c.v.Pos.x, 0f, c.v.Pos.y); }
                Sfx.Play("ui_clic", Vector3.zero, 0.5f, 0.05f);
            }
            // ordenes: formaciones 2x2 (F1-F4) a la derecha de las tarjetas
            string[] fn = { "Línea", "Cuña", "Columna", "Bloque" }; string[] ficon = { "ui_orden_linea_16", "ui_orden_cuna_16", "ui_orden_columna_16", "ui_orden_bloque_16" }; Formation[] ff = { Formation.Linea, Formation.Cuna, Formation.Columna, Formation.Bloque };
            V2Txt("Formación · " + (_selVeh != null ? _selVeh.Name : _sel.Count > 0 ? _sel.Count + " pelotón(es)" : "sin selección"), ox + 1604 * k, 850 * k + (940 - 850) * k * 0f, 300 * k, 11, FontStyle.Normal, V2Sec, TextAnchor.UpperLeft, k);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? 1604 : 1758, y = i < 2 ? 870 : 904 - 0; bool act = _curForm == ff[i];
                V2Box(R(x, y, 146, 30), act ? V2Borde : V2Tarjeta, act ? V2Hueso : V2Borde, 1f); V2Tex(ficon[i], ox + (x + 8) * k, (y + 7) * k, 16 * k, 16 * k);
                V2Txt(fn[i], ox + (x + 32) * k, (y + 7) * k, 90 * k, 13, FontStyle.Bold, V2Hueso, TextAnchor.UpperLeft, k); V2Txt("F" + (i + 1), ox + (x + 118) * k, (y + 8) * k, 25 * k, 11, FontStyle.Normal, V2Sec, TextAnchor.UpperCenter, k);
                if (GUI.Button(R(x, y, 146, 30), GUIContent.none, GUIStyle.none)) { _curForm = ff[i]; foreach (var id in _sel) _uw.SetFormation(id, _curForm); }
            }
            // estado del fuego de los vehiculos
            V2Txt("Fuego de vehículos: " + (_vehFire ? "libre" : "alto el fuego") + "  (G)", ox + 1604 * k, 1000 * k, 300 * k, 12, FontStyle.Bold, _vehFire ? V2Hueso : V2Ambar, TextAnchor.UpperLeft, k);
            return true;
        }
    }
}
