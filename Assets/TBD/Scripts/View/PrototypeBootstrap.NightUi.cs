using System.Collections.Generic;
using UnityEngine;
using TBD.Units;
using TBD.View.UiNoche;

namespace TBD.View
{
    /// <summary>
    /// Interfaz de la noche con el kit de Bruno (UiNoche: IMGUI a base 640×360, escala entera): HUD, rótulo de inicio y parte de la noche. Los datos salen del estado de la noche (NightGame, UnitsWorld, vehículos).
    /// Los clics se resuelven con botones transparentes en coordenadas de pantalla colocados sobre las piezas (el kit solo pinta).
    /// </summary>
    public partial class PrototypeBootstrap
    {
        sealed class NightUiDatos : IUiNocheDatos
        {
            public PrototypeBootstrap G;
            public readonly List<Aviso> AvisosL = new List<Aviso>(); public readonly List<Frente> FrentesL = new List<Frente>(); public readonly List<Tarjeta> TarjetasL = new List<Tarjeta>();
            public string Reloj { get { return G.TimeStr(); } }
            public float ProgresoNoche { get { return G._ng.TotalTicks > 0 ? Mathf.Clamp01(G._ng.Tick / (float)G._ng.TotalTicks) : 0f; } }
            public int Municion { get { return Mathf.RoundToInt(G._uw.StockShots / (float)WeaponCatalog.ShotsPerU); } }
            public int Materiales { get { return 40; } }
            public int Combustible { get { return 30; } }
            public int Pila { get { return G._pileNearStation; } }
            public int Bajas { get { return G._nightLost; } }
            public float PrimeraOleadaEn { get { return -1f; } }
            public IReadOnlyList<Aviso> Avisos { get { return AvisosL; } }
            public IReadOnlyList<Frente> Frentes { get { return FrentesL; } }
            public IReadOnlyList<Tarjeta> Tarjetas { get { return TarjetasL; } }
            public Parte Parte { get; set; }
            public bool HayPartidaGuardada { get { return NetSession.HasSave; } }
        }

        NightUiDatos _uiD; float _uiNext; readonly Dictionary<string, float> _avisoDesde = new Dictionary<string, float>();

        float AvisoT(string key) { if (!_avisoDesde.TryGetValue(key, out var t)) { t = Time.unscaledTime; _avisoDesde[key] = t; } return t; }

        void UiRefresh()
        {
            if (_uiD == null) _uiD = new NightUiDatos { G = this };
            if (Time.unscaledTime < _uiNext) return; _uiNext = Time.unscaledTime + 0.25f; var d = _uiD; d.AvisosL.Clear(); d.FrentesL.Clear(); d.TarjetasL.Clear();
            bool emptyStock = _uw.StockShots <= 0, anyZero = false; int idx = 0;
            string[] tipos = { "fusileros", "ametralladora", "granaderos", "tiradores", "fusileros" };
            foreach (var pl in _uw.Platoons)
            {
                float alive = pl.AliveCount() / (float)Mathf.Max(1, pl.Men.Count), am = pl.AmmoCap > 0 ? pl.AmmoShots / (float)pl.AmmoCap : 0f; bool zero = pl.AliveCount() > 0 && pl.AmmoShots <= 0; if (zero) anyZero = true;
                string nm = PlatoonName(pl); string ty = nm.StartsWith("Ametra") ? "ametralladora" : nm.StartsWith("Grana") ? "granaderos" : nm.StartsWith("Tirad") ? "tiradores" : "fusileros";
                d.TarjetasL.Add(new Tarjeta { tipo = ty, id = pl.Id, efectivos = alive, municion = am, seleccionada = _sel.Contains(pl.Id) && _selVeh == null, sinMunicion = zero, sinMunicionDesde = zero ? AvisoT("sm" + pl.Id) : 0f }); idx++;
            }
            if (anyZero && emptyStock) d.AvisosL.Add(new Aviso { nivel = Nivel.Aviso, icono = "ui_estado_sin_municion_16", texto = "Sin munición: almacén vacío", desde = AvisoT("sinmun") });
            else if (!anyZero) _avisoDesde.Remove("sinmun");
            if (_pileNearStation > 0) d.AvisosL.Add(new Aviso { nivel = Nivel.Ataque, icono = "ui_aviso_estacion_atacada_16", texto = "Estación atacada", desde = AvisoT("est") }); else _avisoDesde.Remove("est");
            if (_nearTrain > 0) d.AvisosL.Add(new Aviso { nivel = Nivel.Ataque, icono = "ui_aviso_tren_atacado_16", texto = "Tren atacado", desde = AvisoT("tren") }); else _avisoDesde.Remove("tren");
            int wv = CurrentWave(); if (wv >= 0 && Time.unscaledTime - _waveBannerT0 < 8f) d.AvisosL.Add(new Aviso { nivel = Nivel.Info, icono = "ui_aviso_oleada_entrante_16", texto = $"Oleada {wv + 1} entrante", desde = _waveBannerT0 });
            Gravedad G(int n) { return n > 150 ? Gravedad.Alta : n > 30 ? Gravedad.Media : Gravedad.Baja; }
            if (_pileNearStation > 0) d.FrentesL.Add(new Frente { icono = "ui_frente_estacion_16", nombre = "Estación", gravedad = G(_pileNearStation) });
            if (_nearTrain > 0) d.FrentesL.Add(new Frente { icono = "ui_frente_tren_16", nombre = "Tren", gravedad = G(_nearTrain) });
            d.FrentesL.Sort((a, b) => ((int)b.gravedad).CompareTo((int)a.gravedad));
            int alv = 0, tot = 0; foreach (var pl in _uw.Platoons) { alv += pl.AliveCount(); tot += pl.Men.Count; }
            bool won = _ng.State == NightState.Victoria;
            d.Parte = new Parte
            {
                victoria = won, titulo = _ng.State == NightState.Derrota ? _ng.DefeatHeadline().TrimEnd((char)46) : null, estrellas = _endStars, zombis = _nightKills, bajas = tot - alv, municionGastada = Mathf.RoundToInt(_nightShotsSpent / (float)WeaponCatalog.ShotsPerU), municionRestante = d.Municion,
                estacion = _ng.State != NightState.Jugando ? _endStationF : _ng.StationHp / (float)Mathf.Max(1, _ng.StationHpMax), tren = _ng.State != NightState.Jugando ? _endTrainF : _ng.TrainHp / (float)Mathf.Max(1, _ng.TrainHpMax), muro = won ? _ng.Report.WallPermil / 1000f : _ng.WallPermil / 1000f
            };
        }
        float _waveBannerT0 = -100f;

        static Rect ScreenRect(float bx, float by, float bw, float bh) { var o = UiNocheKit.Origen; int e = UiNocheKit.Escala; return new Rect(o.x + bx * e, o.y + by * e, bw * e, bh * e); }

        /// <summary>HUD, rótulo y parte con el kit de Bruno. Devuelve true si lo ha pintado (si faltan las piezas, se usa el HUD antiguo).</summary>
        bool NightKitGUI()
        {
            if (UiNocheKit.Tex("ui_panel") == null) return false;
            UiRefresh(); float t = Time.unscaledTime; var d = _uiD;
            UiNocheKit.Begin();
            UiNocheVistas.Hud(d, t);
            int vi = 0;
            foreach (var v in _vehs)                                                        // tarjetas de vehículos (el kit solo trae las de los pelotones): Dragón, VAMTAC 1 y NH90
            {
                if (v.Go == null || v.Name == "VAMTAC 2") continue; float x = 4 + vi * 58, y = 292; vi++;
                UiNocheKit.Slice(_selVeh == v ? "ui_tarjeta_unidad_sel" : "ui_tarjeta_unidad", new Rect(x, y, 56, 64));
                string ty = v.Heli ? "nh90" : v.Name.StartsWith("Drag") ? "dragon" : "vamtac"; UiNocheKit.Fotograma("ui_tarjeta_" + ty + "_48", x + 4, y + 4, 6, 1);
                UiNocheKit.Estirada("ui_barra_efectivos", new Rect(x + 4, y + 54, 48, 3));
                UiNocheKit.Texto(x + 28, y + 3, v.Heli ? (v.State == 0 ? "tierra" : $"{v.Alt:0} m") : (v.HasTarget ? $"{Mathf.Abs(v.Speed) * 3.6f:0}k" : "alto"), UiNocheKit.Crema, 7, TextAnchor.UpperCenter);
            }
            if (Time.unscaledTime < _bannerUntil)                                           // rótulo de inicio / oleada
            {
                var parts = _bannerText.Split('\n'); UiNocheKit.Slice("ui_rotulo_inicio", new Rect(210, 64, 220, 64));
                UiNocheKit.Texto(320, 76, parts[0], UiNocheKit.Crema, 11, TextAnchor.UpperCenter);
                if (parts.Length > 1) UiNocheKit.Texto(320, 100, parts[1], UiNocheKit.Beige, 9, TextAnchor.UpperCenter);
            }
            bool lost = _ng.State == NightState.Derrota, won = _ng.State == NightState.Victoria && !_reportClosed;
            if ((lost && !_endDismissed) || won) UiNocheVistas.ParteNoche(d.Parte, t);
            UiNocheKit.End();
            // clics (coordenadas de pantalla): tarjetas de pelotones y de vehículos, botones del parte
            int ci = 0; foreach (var pl in _uw.Platoons) { if (ci >= 5) break; var r = ScreenRect(176 + ci * 58, 292, 56, 64); if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { _selVeh = null; if (!Event.current.shift) _sel.Clear(); if (!_sel.Contains(pl.Id)) _sel.Add(pl.Id); Sfx.Play("sfx_ui_clic", 0.5f); } ci++; }
            vi = 0; foreach (var v in _vehs) { if (v.Go == null || v.Name == "VAMTAC 2") continue; var r = ScreenRect(4 + vi * 58, 292, 56, 64); vi++; if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { _selVeh = v; _sel.Clear(); Sfx.Play("sfx_ui_clic", 0.5f); } }
            if (won) { if (GUI.Button(ScreenRect(260, 272, 120, 18), GUIContent.none, GUIStyle.none)) NextNight(); }
            else if (lost && !_endDismissed)
            {
                if (GUI.Button(ScreenRect(196, 248, 120, 18), GUIContent.none, GUIStyle.none)) { NetSession.Reset(); NightRun.Active = true; UnityEngine.SceneManagement.SceneManager.LoadScene("Union"); }
                if (GUI.Button(ScreenRect(324, 248, 120, 18), GUIContent.none, GUIStyle.none)) { NetSession.Reset(); NightRun.Active = false; UnityEngine.SceneManagement.SceneManager.LoadScene("Menu"); }
            }
            return true;
        }
    }
}
