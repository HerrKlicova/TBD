using System.Collections.Generic;
using System.IO;
using System.Text;
using TBD.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// Hito 2 «Red»: mapa de operaciones (GDD §7) dibujado como pixel art en una textura de 320 x 180 ampliada con vecino más
    /// cercano, con la red del MVP, la mancha de horda (capa S0), los trenes con horario y carga, el estado de cada nodo y las
    /// alertas priorizadas. Interfaz nítida encima (OnGUI). Solo presentación: la simulación es TBD.Net.NetWorld, a tick fijo.
    /// Prueba de desincronización: un segundo NetWorld recibe las mismas órdenes y su hash se compara en cada tick (F3).
    /// Escena: Assets/Scenes/Red.unity.
    /// </summary>
    public sealed class OperationsMap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // AfterSceneLoad solo se ejecuta al darle a Play; al cambiar de escena (mapa ↔ zona, hito 3) lo hace sceneLoaded.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Red") return;
            if (FindFirstObjectByType<OperationsMap>() != null) return;
            new GameObject("TBD_OperationsMap").AddComponent<OperationsMap>();
        }

        const int TexW = 224, TexH = 180;
        const float MapScale = 2.3f;                  // píxeles de textura por unidad de mapa
        const float MapOx = 30f, MapOy = 4f;
        const float MinX = 14f, MinY = 6f;             // esquina noroeste del mapa en unidades de mapa

        NetWorld _a, _b;                               // A se ve; B es la copia para la prueba de desincronización
        float _acc;
        bool _check = true, _desync;
        long _desyncTick;
        int _selected;
        Texture2D _tex;
        Color32[] _px;
        GUIStyle _label, _small, _panel, _title;
        Texture2D _panelTex;
        readonly StringBuilder _sb = new StringBuilder();

        // paleta (32 colores de la dirección visual, subconjunto)
        static readonly Color32 CLand = new Color32(46, 44, 38, 255), CLand2 = new Color32(54, 51, 43, 255);
        static readonly Color32 CSea = new Color32(22, 34, 52, 255), CSea2 = new Color32(26, 40, 60, 255);
        static readonly Color32 CRail = new Color32(170, 160, 140, 255), CRailCut = new Color32(200, 40, 36, 255), CRailDmg = new Color32(214, 130, 40, 255);
        static readonly Color32 CH1 = new Color32(88, 30, 34, 255), CH2 = new Color32(140, 36, 38, 255), CH3 = new Color32(196, 52, 44, 255);
        static readonly Color32 CSafe = new Color32(96, 176, 96, 255), CThreat = new Color32(224, 196, 64, 255), CSiege = new Color32(232, 110, 40, 255), CLost = new Color32(110, 20, 24, 255);
        static readonly Color32 CTrain = new Color32(250, 240, 170, 255), CTrainSel = new Color32(120, 220, 255, 255), CDark = new Color32(12, 12, 14, 255);

        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        void Start()
        {
            Application.targetFrameRate = 60;
            _a = NetScenario.Mvp();
            _b = NetScenario.Mvp();
            _tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _px = new Color32[TexW * TexH];
            if (Camera.main == null)
            {
                var cam = new GameObject("Camera").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.tag = "MainCamera";
            }
        }

        // ---------------------------------------------------------------- órdenes (también a la copia B)
        void Push(NetCmdType type, int arg, long delay)
        {
            var c = new NetCommand { Tick = _a.Tick + delay, Type = type, Arg = arg };
            _a.Push(c);
            _b.Push(c);
        }

        void Update()
        {
            HandleInput();
            _acc += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            const float step = 1f / NetConst.TickRate;
            while (_acc >= step)
            {
                _acc -= step;
                _a.ApplyCommands();
                _b.ApplyCommands();
                for (int i = 0; i < _a.Speed; i++)
                {
                    _a.Step();
                    if (_check) _b.Step();
                    if (_check && !_desync && _a.Hash() != _b.Hash()) { _desync = true; _desyncTick = _a.Tick; Debug.LogError($"[TBD] Red: desincronización en el tick {_a.Tick}"); }
                }
                if (_check && _a.Tick % 3000 == 0 && !_a.Paused) Debug.Log($"[TBD] Red tick {_a.Tick}: hash idéntico en los dos mundos ({_a.Hash():X16})");
            }
            DrawMap();
        }

        void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame) Push(_a.Paused ? NetCmdType.Resume : NetCmdType.Pause, 0, 0);
            if (kb.digit1Key.wasPressedThisFrame) Push(NetCmdType.SetSpeed, 1, 0);
            if (kb.digit2Key.wasPressedThisFrame) Push(NetCmdType.SetSpeed, 2, 0);
            if (kb.digit3Key.wasPressedThisFrame) Push(NetCmdType.SetSpeed, 4, 0);
            if (kb.tabKey.wasPressedThisFrame) _selected = (_selected + 1) % _a.Services.Count;
            if (kb.hKey.wasPressedThisFrame) Push(_a.Services[_selected].Held ? NetCmdType.TrainGo : NetCmdType.TrainHold, _selected, 2);
            if (kb.f3Key.wasPressedThisFrame) { _check = !_check; _desync = false; if (_check) Resync(); }
            if (kb.f9Key.wasPressedThisFrame) Capture();
        }

        void Resync()
        {
            // Si B dejó de avanzar mientras la prueba estaba apagada, ya no se puede comparar (para copiar A haría falta el guardado,
            // hito 6): se avisa y se deja apagada.
            if (_b.Tick != _a.Tick) { _check = false; Debug.LogWarning("[TBD] Red: la copia B se quedó atrás; la prueba solo vale desde el arranque (falta el guardado, hito 6)."); }
        }

        // ---------------------------------------------------------------- dibujo pixel art
        Vector2 MapToTex(float mx, float my) { return new Vector2(MapOx + (mx - MinX) * MapScale, TexH - 1 - (MapOy + (my - MinY) * MapScale)); }

        static float CoastX(float my)
        {
            // costa aproximada al este de Valencia, Sagunto, Castellón y Vinaròs (unidades de mapa, a ojo)
            if (my >= 84) return 53f;
            if (my >= 74) return Mathf.Lerp(56f, 53f, (my - 74f) / 10f);
            if (my >= 63) return Mathf.Lerp(61f, 56f, (my - 63f) / 11f);
            if (my >= 48) return Mathf.Lerp(72f, 61f, (my - 48f) / 15f);
            if (my >= 40) return Mathf.Lerp(76f, 72f, (my - 40f) / 8f);
            return 80f;
        }

        void Put(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < TexW && y < TexH) _px[y * TexW + x] = c; }

        void Line(Vector2 p0, Vector2 p1, Color32 c)
        {
            int x0 = Mathf.RoundToInt(p0.x), y0 = Mathf.RoundToInt(p0.y), x1 = Mathf.RoundToInt(p1.x), y1 = Mathf.RoundToInt(p1.y);
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                Put(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        void Box(Vector2 p, int r, Color32 c, Color32 outline)
        {
            int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
            for (int y = -r - 1; y <= r + 1; y++) for (int x = -r - 1; x <= r + 1; x++) Put(cx + x, cy + y, outline);
            for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++) Put(cx + x, cy + y, c);
        }

        void DrawMap()
        {
            var f = _a.Field;
            for (int ty = 0; ty < TexH; ty++)
                for (int tx = 0; tx < TexW; tx++)
                {
                    float mx = (tx - MapOx) / MapScale + MinX;
                    float my = (TexH - 1 - ty - MapOy) / MapScale + MinY;
                    bool sea = mx > CoastX(my);
                    bool alt = ((tx >> 3) + (ty >> 3) & 1) == 0;
                    Color32 c = sea ? (alt ? CSea : CSea2) : (alt ? CLand : CLand2);
                    if (!sea)
                    {
                        int d = f.At(Mathf.FloorToInt(mx), Mathf.FloorToInt(my));
                        int th = Bayer[(ty & 3) * 4 + (tx & 3)] * 50 + 60;          // tramado ordenado: la mancha se ve pixelada
                        if (d > th) c = d > 600 ? CH3 : (d > 350 ? CH2 : CH1);
                    }
                    _px[ty * TexW + tx] = c;
                }

            var g = _a.Graph;
            foreach (var e in g.Edges)
            {
                var na = g.Nodes[e.A]; var nb = g.Nodes[e.B];
                Color32 c = e.State == EdgeState.Cortada ? CRailCut : (e.State == EdgeState.Danada ? CRailDmg : CRail);
                Line(MapToTex(na.XMeters, na.YMeters), MapToTex(nb.XMeters, nb.YMeters), c);
            }
            foreach (var n in g.Nodes)
            {
                Color32 c = n.State == NodeState.Segura ? CSafe : n.State == NodeState.Amenazada ? CThreat : n.State == NodeState.Asediada ? CSiege : CLost;
                Box(MapToTex(n.XMeters, n.YMeters), 1, c, CDark);
            }
            for (int i = 0; i < _a.Services.Count; i++)
            {
                var p = TrainPos(_a.Services[i]);
                Box(p + TrainOffset(i), 1, i == _selected ? CTrainSel : CTrain, CDark);
            }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        // los trenes que coinciden en una estación no se tapan: cada uno va un poco desplazado
        static Vector2 TrainOffset(int i) { return new Vector2(-4 + 4 * (i % 3), 4); }

        Vector2 TrainPos(TrainService s)
        {
            var t = s.Train;
            var g = _a.Graph;
            var e = s.AtStop ? null : t.CurrentEdge(g);
            if (e == null)
            {
                var n = g.Nodes[s.AtStop || t.Route.Count == 0 ? s.Stops[s.StopIndex].Node : t.Route[t.Route.Count - 1]];
                return MapToTex(n.XMeters, n.YMeters);
            }
            var from = g.Nodes[t.Route[t.Leg]];
            var to = g.Nodes[t.Route[t.Leg + 1]];
            float k = e.LengthMm > 0 ? (float)t.OffsetMm / e.LengthMm : 0f;
            return Vector2.Lerp(MapToTex(from.XMeters, from.YMeters), MapToTex(to.XMeters, to.YMeters), k);
        }

        // ---------------------------------------------------------------- interfaz nítida
        void EnsureStyles()
        {
            if (_label != null) return;
            _panelTex = new Texture2D(1, 1);
            _panelTex.SetPixel(0, 0, new Color(0.04f, 0.045f, 0.06f, 0.82f));
            _panelTex.Apply();
            _panel = new GUIStyle { normal = { background = _panelTex } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Screen.height / 52), richText = true };
            _label.normal.textColor = new Color(0.92f, 0.9f, 0.84f);
            _small = new GUIStyle(_label) { fontSize = Mathf.Max(10, Screen.height / 64) };
            _title = new GUIStyle(_label) { fontSize = Mathf.Max(14, Screen.height / 40), fontStyle = FontStyle.Bold };
        }

        static string Hex(Color32 c) { return "#" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2"); }

        void OnGUI()
        {
            EnsureStyles();
            // textura del mapa ampliada a escala entera, a la izquierda
            // escala entera cuando cabe (pixel art limpio); en ventanas pequeñas del editor, escala libre para llenar el alto
            float fit = Mathf.Min(Screen.width * 0.62f / TexW, (float)Screen.height / TexH);
            float scale = fit >= 2f ? Mathf.Floor(fit) : fit;
            Rect mapRect = new Rect(0, (Screen.height - TexH * scale) / 2, TexW * scale, TexH * scale);
            GUI.DrawTexture(mapRect, _tex, ScaleMode.StretchToFill, false);

            // nombres de nodos y trenes
            var g = _a.Graph;
            foreach (var n in g.Nodes)
            {
                var p = MapToTex(n.XMeters, n.YMeters);
                float sx = mapRect.x + (p.x + 3) * scale, sy = mapRect.y + (TexH - 1 - p.y - 3) * scale;
                GUI.Label(new Rect(sx, sy, 220, 30), n.Name, _small);
            }
            for (int i = 0; i < _a.Services.Count; i++)
            {
                var p = TrainPos(_a.Services[i]) + TrainOffset(i);
                float sx = mapRect.x + (p.x - 14) * scale, sy = mapRect.y + (TexH - 1 - p.y - 6) * scale;
                GUI.Label(new Rect(sx, sy, 140, 30), "<b>" + _a.Services[i].Train.Name + "</b>", _small);
            }

            // panel derecho
            float px = mapRect.xMax + 8, pw = Screen.width - px - 8;
            GUI.Box(new Rect(px, 8, pw, Screen.height - 16), GUIContent.none, _panel);
            float y = 16;
            long day = _a.Tick / NetConst.DayTicks + 1;
            string speed = _a.Paused ? "<color=#E8C040>PAUSA</color>" : "x" + _a.Speed;
            GUI.Label(new Rect(px + 10, y, pw, 40), $"MAPA DE OPERACIONES · Día {day} · {NetConst.Clock(_a.Tick)} · {speed}", _title); y += _title.fontSize * 1.8f;
            string sync = !_check ? "<color=#999999>prueba de desincronización apagada (F3)</color>"
                        : _desync ? $"<color=#E04030>DESINCRONIZACIÓN en el tick {_desyncTick}</color>"
                        : "<color=#70C070>Desincronización: correcta (dos mundos, mismo hash)</color>";
            GUI.Label(new Rect(px + 10, y, pw, 30), sync, _small); y += _small.fontSize * 1.8f;

            GUI.Label(new Rect(px + 10, y, pw, 30), "<b>ALERTAS</b>", _label); y += _label.fontSize * 1.6f;
            int shown = 0;
            foreach (var al in _a.Alerts)
            {
                if (shown++ >= 6) break;
                string col = al.Priority == 0 ? "#F05040" : al.Priority == 1 ? "#F0A040" : al.Priority == 2 ? "#E0D070" : "#A0A0A0";
                GUI.Label(new Rect(px + 14, y, pw - 20, 30), $"<color={col}>●</color> {al.Text}", _small); y += _small.fontSize * 1.6f;
            }
            if (_a.Alerts.Count == 0) { GUI.Label(new Rect(px + 14, y, pw, 30), "<color=#808080>sin alertas</color>", _small); y += _small.fontSize * 1.6f; }
            y += 8;

            // tren seleccionado: estado, horario y carga
            var s = _a.Services[_selected];
            var t = s.Train;
            string st = s.Held ? "<color=#E8C040>retenido</color>" : s.AtStop ? "en estación" : t.State == RailTrainState.Detenido ? "<color=#F05040>detenido</color>" : "en marcha";
            GUI.Label(new Rect(px + 10, y, pw, 30), $"<b>TREN {t.Name}</b> · {st} · {s.CargoTotal()}/{NetConst.TrainCapacity} t", _label); y += _label.fontSize * 1.6f;
            string[] res = { "Munición", "Combustible", "Materiales", "Personas" };
            _sb.Clear();
            for (int r = 0; r < NetConst.ResCount; r++) _sb.Append(res[r]).Append(' ').Append(s.Cargo[r]).Append(r < NetConst.ResCount - 1 ? " · " : "");
            GUI.Label(new Rect(px + 14, y, pw - 20, 30), _sb.ToString(), _small); y += _small.fontSize * 1.8f;
            for (int k = 0; k < s.Stops.Count; k++)
            {
                var stop = s.Stops[k];
                bool here = k == s.StopIndex;
                _sb.Clear();
                _sb.Append(here ? (s.AtStop ? "▶ " : "→ ") : "   ").Append(NetConst.Clock(stop.DepartAt)).Append("  ").Append(g.Nodes[stop.Node].Name).Append("  ");
                for (int r = 0; r < NetConst.ResCount; r++)
                {
                    int q = stop.Transfer[r];
                    if (q == 0) continue;
                    _sb.Append(q > 0 ? "+" : "−").Append(Mathf.Abs(q)).Append(' ').Append(res[r].Substring(0, 3).ToLower()).Append(' ');
                }
                GUI.Label(new Rect(px + 14, y, pw - 20, 30), here ? "<b>" + _sb + "</b>" : _sb.ToString(), _small); y += _small.fontSize * 1.5f;
            }
            y += 8;
            GUI.Label(new Rect(px + 10, y, pw, 30), "<b>NODOS</b>  <color=#60B060>■</color> segura <color=#E0C440>■</color> amenazada <color=#E86E28>■</color> asediada", _small); y += _small.fontSize * 1.6f;
            foreach (var n in g.Nodes)
            {
                if (n.State == NodeState.Segura) continue;
                GUI.Label(new Rect(px + 14, y, pw - 20, 30), $"{n.Name}: {n.State.ToString().ToLower()} (densidad {n.HordeDensity / 10}%)", _small); y += _small.fontSize * 1.5f;
            }

            GUI.Label(new Rect(px + 10, Screen.height - 16 - _small.fontSize * 2.2f, pw, 30),
                "Espacio pausa · 1/2/3 x1/x2/x4 · Tab tren · H retener/soltar · F3 desincronización · F9 captura", _small);
        }

        // ---------------------------------------------------------------- captura (solo hitos)
        void Capture()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"red_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[TBD] Captura guardada en {path}");
        }
    }
}
