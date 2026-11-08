using System.Collections.Generic;
using UnityEngine;
using TBD.Sim;
using TBD.Units;
using TBD.Net;

namespace TBD.View
{
    /// <summary>
    /// Muro por tramos y modo construccion v1 (D-N153, D-N163; docs/construccion-reparar.md de Lucia). Solo el muro OESTE (por donde entra el 96 % de la horda) y solo con la bandera -tramos
    /// (D-N165: no entra en la build hasta que Hugo y Nacho lo calibren). Cada pieza de Obras del oeste es un tramo con vida (CostE4 = precio x 10.000) y su solido propio en ECS (BuildSeg);
    /// al llegar a 0 se abre (SetSegment: brecha, ya no bloquea) y se puede reconstruir (solo sin zombis a 15 m; vuelve al 30 %). Reparar: 1 u de materiales = 5.000 de vida; los zapadores
    /// vivos gastan 1 u cada 20 s. Tecla B = modo obras (la partida sigue). Todas las ordenes de la sim van por la cola unica; la vida por tramo es solo de la vista.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        sealed class Seg { public int Id; public Vector2 A, B; public float Hp, Max; public bool Open; public string Name; public int Pile; public bool Queued; public int RebuildTick = -1; public bool IsGate; }
        readonly List<Seg> _segs = new List<Seg>(); readonly bool _tramos = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-tramos") >= 0; bool _segsInit, _obras, _tt1, _tt2; float _mats = 40f; Seg _segSel; float _segMsgUntil; string _segMsg = ""; float _repairAcc;
        static readonly Color SgAmbar = new Color32(0xe0, 0x8c, 0x30, 255), SgRojo = new Color32(0x98, 0x24, 0x20, 255);

        void TramosInit()
        {
            if (!_tramos || _segsInit) return;
            var pieces = NetSession.A != null && _zoneNode >= 0 ? NetSession.A.Works.At(_zoneNode) : null; if (pieces == null || pieces.Count == 0) return;
            _segsInit = true; int id = 16;
            foreach (var w in pieces)
            {
                if (!w.Built || !(w.IsWall || w.Kind == PieceKind.PuertaVia)) continue;
                var a = new Vector2(WorkOrigin.x + w.X0 / 1000f, WorkOrigin.y + w.Y0 / 1000f); var b = new Vector2(WorkOrigin.x + w.X1 / 1000f, WorkOrigin.y + w.Y1 / 1000f);
                if (Mathf.Abs(a.x - 70f) > 0.6f || Mathf.Abs(b.x - 70f) > 0.6f) continue;                       // solo el muro oeste
                float lifeMax = w.CostE4 * 1.1924f;                                                           // 11.924 de vida por u (Hugo, 1.200.000 en total) en vez de 10.000
                _segs.Add(new Seg { Id = id++, A = a, B = b, Hp = lifeMax, Max = lifeMax, IsGate = w.Kind == PieceKind.PuertaVia, Name = (w.Kind == PieceKind.PuertaVia ? "Puerta de la vía" : "Tramo oeste " + (_segs.Count + 1)) });
            }
            if (_segs.Count == 0) { Debug.Log("[NOCHE] tramos: no hay piezas de muro en el oeste"); return; }
            Push(CmdType.ClearWest);
            foreach (var s in _segs) Push(CmdType.BuildSeg, s.Id * 10000 + 2500, new Unity.Mathematics.float2(s.A.x, s.A.y), new Unity.Mathematics.float2(s.B.x, s.B.y));
            Debug.Log($"[NOCHE] tramos: {_segs.Count} tramos en el muro oeste (vida total {SegTotal(false):0})");
        }

        float SegTotal(bool max) { float t = 0f; foreach (var s in _segs) t += max ? s.Max : s.Hp; return t; }

        /// <summary>Un tick de tramos: reparto del dano de la pila, brechas, reparacion y reconstruccion. Se llama desde NightTick con las posiciones de los zombis.</summary>
        void TramosTick()
        {
            if (!_tramos) return; if (!_segsInit) { TramosInit(); return; }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-tramostest") >= 0 && _segs.Count > 1)
            {
                if (_ng.Tick >= 1800 && !_tt1) { _tt1 = true; _segs[1].Open = true; Push(CmdType.SetSegment, _segs[1].Id * 10 + 1); Debug.Log("[TEST] tramos: abro " + _segs[1].Name + " en el tick 1800"); }
                if (_ng.Tick >= 2400 && !_tt2) { _tt2 = true; _segs[1].Open = false; _segs[1].Hp = _segs[1].Max * 0.3f; Push(CmdType.SetSegment, _segs[1].Id * 10 + 0); Debug.Log("[TEST] tramos: cierro " + _segs[1].Name + " en el tick 2400"); }
            }
            foreach (var s in _segs) s.Pile = 0;
            for (int i = 0; i < _zomPos.Count; i++)
            {
                var p = _zomPos[i]; Seg best = null; float bd = 2.25f;
                foreach (var s in _segs) { if (s.Open) continue; float d = DistSeg(p, s.A, s.B); if (d * d < bd) { bd = d * d; best = s; } }
                if (best != null) best.Pile++;
            }
            float dmg = NightSimParams.Default().WallDmgPerZombieS / 30f;
            foreach (var s in _segs)
            {
                if (s.Open) { if (s.RebuildTick >= 0 && _ng.Tick >= s.RebuildTick) { s.Open = false; s.Hp = s.Max * 0.3f; s.RebuildTick = -1; Push(CmdType.SetSegment, s.Id * 10 + 0); if (s.IsGate) Push(CmdType.ToggleGate); Debug.Log("[NOCHE] " + s.Name + " reconstruido al 30 %"); } continue; }
                if (s.Pile > 0) s.Hp = Mathf.Max(0f, s.Hp - s.Pile * dmg);                                          // 1 punto por zombi de la pila y por segundo, sin factor (NightSim, Nacho)
                if (s.Hp <= 0f && !s.Open) { s.Open = true; Push(CmdType.SetSegment, s.Id * 10 + 1); if (s.IsGate) Push(CmdType.ToggleGate); Debug.Log("[NOCHE] BRECHA en " + s.Name); }
            }
            // reparacion: los zapadores vivos reparan los tramos en cola o el seleccionado (si no hay zombis pegados)
            int sappers = 0; foreach (var pl in _uw.Platoons) if (pl.Kind == PlatoonKind.Zapadores) sappers += pl.AliveCount();
            foreach (var s in _segs)
            {
                if (!s.Queued || s.Open || s.Hp >= s.Max || s.Pile > 0 || sappers == 0 || _mats <= 0f) { if (s.Queued && (s.Hp >= s.Max || s.Open)) s.Queued = false; continue; }
                float rate = sappers * (1f / 20f) * 5000f / 30f;                                                  // cada zapador gasta 1 u cada 20 s y esa u repara 5.000
                float add = Mathf.Min(rate, s.Max - s.Hp); s.Hp += add; _mats = Mathf.Max(0f, _mats - add / 5000f);
            }
            // el muro total del HUD: vida de los tramos del oeste (el resto del recinto sigue como un solo bloque)
            if (_wallMax > 0f) { float sumH = SegTotal(false), sumM = SegTotal(true); if (sumM > 0f) _ng.WallPermil = Mathf.RoundToInt(1000f * sumH / sumM); }
        }

        static float DistSeg(Vector2 p, Vector2 a, Vector2 b) { var ab = b - a; float l2 = ab.sqrMagnitude; float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2); return (a + ab * t - p).magnitude; }

        void TramosUpdate()
        {
            if (!_tramos) return; var kb = UnityEngine.InputSystem.Keyboard.current; var ms = UnityEngine.InputSystem.Mouse.current; if (kb == null || ms == null) return;
            if (kb.bKey.wasPressedThisFrame) { _obras = !_obras; _segSel = null; }
            if (!_obras) return;
            if (ms.leftButton.wasPressedThisFrame && TryGround(ms.position.ReadValue(), out var g) && !OverNightUi(ms.position.ReadValue()))
            {
                Seg best = null; float bd = 4f; foreach (var s in _segs) { float d = DistSeg(new Vector2(g.x, g.z), s.A, s.B); if (d < bd) { bd = d; best = s; } }
                _segSel = best;
            }
        }

        string SegBlockReason(Seg s, bool rebuild)
        {
            int sappers = 0; foreach (var pl in _uw.Platoons) if (pl.Kind == PlatoonKind.Zapadores) sappers += pl.AliveCount();
            if (!rebuild && s.Open) return "Tramo caído: hay que reconstruirlo";
            if (sappers == 0) return "Sin zapadores";
            if (_mats <= 0f) return "Sin materiales";
            float near = rebuild ? 15f : 3f; foreach (var p in _zomPos) if (DistSeg(p, s.A, s.B) <= near) return rebuild ? "Hay zombis a menos de 15 m del tramo" : "Hay zombis pegados al tramo";
            return null;
        }

        /// <summary>Capa de obras (contorno por estado) y panel pequeno abajo a la derecha. IMGUI.</summary>
        void TramosGUI()
        {
            if (!_tramos || !_segsInit || _worldCam == null) return;
            foreach (var s in _segs)
            {
                float f = s.Open ? 0f : s.Hp / s.Max; var a = ToGui(new Vector3(s.A.x, 0.3f, s.A.y)); var b = ToGui(new Vector3(s.B.x, 0.3f, s.B.y));
                if (!_obras && !s.Open && f >= 0.7f) continue;                                                        // sano: sin marca; solo se ve en modo obras
                Color c = s.Open ? SgRojo : f >= 0.7f ? OvHueso : SgAmbar; float al = s.Open ? 0.75f : (f < 0.3f ? 0.55f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 0.9f);
                OvLine(a, b, s.Open ? 5f : 3f, c, al); if (s == _segSel) OvLine(a + Vector2.up * 5f, b + Vector2.up * 5f, 2f, OvHueso, 1f);
            }
            if (!_obras) return;
            float k = Screen.height / 1080f; OvEnsure(); var r = new Rect(Screen.width - 330f * k, Screen.height - 410f * k, 316f * k, 210f * k);
            var old = GUI.color; GUI.color = new Color(0.094f, 0.11f, 0.188f, 0.9f); GUI.DrawTexture(r, _ovTex); GUI.color = old;
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(11, Mathf.RoundToInt(13 * k)), wordWrap = true }; st.normal.textColor = OvHueso;
            int sapp = 0, sappTot = 0; foreach (var pl in _uw.Platoons) if (pl.Kind == PlatoonKind.Zapadores) { sapp += pl.AliveCount(); sappTot += pl.Men.Count; }
            float x = r.x + 8f * k, y = r.y + 6f * k, lh = 20f * k;
            GUI.Label(new Rect(x, y, r.width, lh), "Obras · reparar muro (B sale)", st); y += lh;
            GUI.Label(new Rect(x, y, r.width, lh), $"Materiales {_mats:0} u · Zapadores {sapp}/{sappTot} · Muro {_ng.WallPermil / 10} %", st); y += lh;
            if (_segSel == null) GUI.Label(new Rect(x, y, r.width - 16f * k, lh * 2f), "Clic en un tramo del muro oeste.", st);
            else
            {
                var s = _segSel; bool rebuild = s.Open; string why = SegBlockReason(s, rebuild); float need = rebuild ? s.Max / 11924f : Mathf.Max(0.01f, (s.Max - s.Hp) / 5000f);
                GUI.Label(new Rect(x, y, r.width - 16f * k, lh), $"{s.Name}: {(s.Open ? "BRECHA" : Mathf.RoundToInt(100f * s.Hp / s.Max) + " %")}", st); y += lh;
                GUI.Label(new Rect(x, y, r.width - 16f * k, lh), (rebuild ? "Reconstruir" : "Reparar") + $" · {need:0.0} u" + (rebuild ? " · 10 s (provisional)" : ""), st); y += lh;
                var bs = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Max(11, Mathf.RoundToInt(14 * k)) }; GUI.enabled = why == null || (why != null && false);
                if (GUI.Button(new Rect(x, y, 150f * k, 30f * k), rebuild ? "Reconstruir" : "Reparar", bs) && why == null)
                {
                    if (rebuild) { _mats -= s.Max / 11924f; s.RebuildTick = _ng.Tick + 300; } else s.Queued = true;
                }
                GUI.enabled = true; y += 34f * k;
                if (why != null) GUI.Label(new Rect(x, y, r.width - 16f * k, lh * 2f), why, st);
                else if (s.Queued) GUI.Label(new Rect(x, y, r.width - 16f * k, lh), "En cola: los zapadores reparan.", st);
                else if (s.RebuildTick >= 0) GUI.Label(new Rect(x, y, r.width - 16f * k, lh), "Reconstruyendo…", st);
            }
        }
    }
}
