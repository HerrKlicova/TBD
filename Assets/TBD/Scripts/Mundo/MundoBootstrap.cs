using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TBD.Mundo
{
    /// <summary>
    /// Núcleo v2, Hito 1 · prototipo de cámara y zum continuo (Marcos, 11-11). Una sola escena de 2 × 2 km: del mapa (2–3 km de altura) al soldado
    /// (12 m), sin cargas. Solo VISTA y entrada: la cámara no toca ninguna simulación (el juego es cooperativo y la simulación irá en lockstep).
    /// Lo que se mueve aquí (pelotón y zombis) es un relleno local para ver la escala; la simulación de verdad la ponen Nacho e Iker.
    /// Controles: WASD/flechas mueven (velocidad según la altura), rueda = zum hacia el cursor, Q/E giran, clic izquierdo selecciona el pelotón,
    /// clic derecho lo manda a un punto. Se lanza con «TBD.exe -mundo» (o abriendo la escena Mundo).
    /// </summary>
    public sealed class MundoBootstrap : MonoBehaviour
    {
        public const float Lado = 2000f;                    // metros
        const float AlturaMin = 12f, AlturaMax = 2800f;
        const float AlturaIconos = 260f;                    // por encima, las unidades se ven como símbolos (como en el mapa)

        Camera _cam, _pantalla; RenderTexture _rt;
        Vector3 _foco = new Vector3(1000f, 0f, 1000f); float _altura = 1800f, _alturaObj = 1800f, _yaw = 0f;
        Texture2D _blanco;

        readonly List<Transform> _soldados = new List<Transform>();
        readonly List<(Transform t, Vector3 vel, bool vivo)> _zombis = new List<(Transform, Vector3, bool)>();
        Vector3 _destino; bool _seleccionado = true; float _fuego;
        readonly List<(Vector3 a, Vector3 b, float hasta)> _trazadoras = new List<(Vector3, Vector3, float)>();
        readonly List<(string nombre, Vector3 pos)> _estaciones = new List<(string, Vector3)>();
        System.Random _rng = new System.Random(1234);

        void Start()
        {
            Application.targetFrameRate = 120;
            _blanco = Texture2D.whiteTexture;
            MontarCamaras();
            MontarTerreno();
            MontarEstacion("Sagunto", new Vector3(700f, 0f, 1000f));
            MontarEstacion("Nules", new Vector3(1450f, 0f, 760f));
            MontarVia(new Vector3(700f, 0f, 1000f), new Vector3(1450f, 0f, 760f));
            MontarCaserio(new Vector3(1150f, 0f, 1350f));
            MontarPeloton(new Vector3(760f, 0f, 980f));
            var sol = new GameObject("Sol").AddComponent<Light>(); sol.type = LightType.Directional; sol.transform.rotation = Quaternion.Euler(50f, 30f, 0f); sol.intensity = 1.2f; sol.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.45f, 0.43f, 0.40f);
            Debug.Log($"[MUNDO] prototipo de zum: {Lado:0} × {Lado:0} m, altura {AlturaMin}–{AlturaMax} m, iconos por encima de {AlturaIconos} m");
        }

        // ------------------------------------------------------------------ cámara: perspectiva baja de resolución (pixel art) + pantalla
        void MontarCamaras()
        {
            var go = new GameObject("Cámara del mundo"); _cam = go.AddComponent<Camera>();
            _cam.fieldOfView = 32f; _cam.nearClipPlane = 1f; _cam.farClipPlane = 12000f; _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color32(0x2a, 0x2a, 0x32, 255);
            var gp = new GameObject("Pantalla"); _pantalla = gp.AddComponent<Camera>(); _pantalla.cullingMask = 0; _pantalla.clearFlags = CameraClearFlags.SolidColor; _pantalla.backgroundColor = Color.black; _pantalla.depth = -10;
            HacerRt();
        }

        void HacerRt()
        {
            int w = Mathf.Max(320, Screen.width / 3), h = Mathf.Max(180, Screen.height / 3);
            if (_rt != null && _rt.width == w && _rt.height == h) return;
            if (_rt != null) { _cam.targetTexture = null; _rt.Release(); }
            _rt = new RenderTexture(w, h, 24) { filterMode = FilterMode.Point, name = "Mundo baja resolución" }; _cam.targetTexture = _rt;
        }

        void ColocarCamara()
        {
            float t = Mathf.InverseLerp(Mathf.Log(AlturaMin), Mathf.Log(AlturaMax), Mathf.Log(_altura));
            float pitch = Mathf.Lerp(48f, 82f, Mathf.SmoothStep(0f, 1f, t));            // a ras de suelo, isométrica; arriba, casi cenital (el mapa)
            var rot = Quaternion.Euler(pitch, _yaw, 0f);
            float dist = _altura / Mathf.Sin(pitch * Mathf.Deg2Rad);
            _cam.transform.rotation = rot; _cam.transform.position = _foco - rot * Vector3.forward * dist;
        }

        void Update()
        {
            HacerRt();
            var kb = Keyboard.current; var ms = Mouse.current; float dt = Time.unscaledDeltaTime;
            if (kb != null)
            {
                var fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward; var right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right; Vector3 pan = Vector3.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) pan += fwd; if (kb.sKey.isPressed || kb.downArrowKey.isPressed) pan -= fwd;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) pan -= right; if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) pan += right;
                _foco += pan.normalized * _altura * 1.1f * dt;
                if (kb.qKey.isPressed) _yaw -= 60f * dt; if (kb.eKey.isPressed) _yaw += 60f * dt;
            }
            if (ms != null)
            {
                float s = ms.scroll.ReadValue().y;
                if (s != 0f)
                {
                    float notches = Mathf.Clamp(s / (Mathf.Abs(s) >= 10f ? 120f : 1f), -3f, 3f);
                    float antes = _alturaObj; _alturaObj = Mathf.Clamp(_alturaObj * Mathf.Pow(0.8f, notches), AlturaMin, AlturaMax);
                    if (Suelo(ms.position.ReadValue(), out var p)) _foco = Vector3.Lerp(_foco, p, notches > 0f ? 1f - _alturaObj / antes : 0f);   // acercar hacia el cursor
                }
                if (ms.leftButton.wasPressedThisFrame && Suelo(ms.position.ReadValue(), out var q)) _seleccionado = Vector3.Distance(q, Centro()) < Mathf.Max(15f, _altura * 0.05f);
                if (ms.rightButton.wasPressedThisFrame && _seleccionado && Suelo(ms.position.ReadValue(), out var d)) _destino = d;
            }
            _foco.x = Mathf.Clamp(_foco.x, 0f, Lado); _foco.z = Mathf.Clamp(_foco.z, 0f, Lado);
            _altura = Mathf.Lerp(_altura, _alturaObj, 1f - Mathf.Exp(-10f * dt));
            ColocarCamara();
            bool iconos = _altura > AlturaIconos;
            foreach (var sd in _soldados) sd.gameObject.SetActive(!iconos);
            for (int i = 0; i < _zombis.Count; i++) if (_zombis[i].vivo) _zombis[i].t.gameObject.SetActive(!iconos);
            MoverRelleno(Time.deltaTime);
        }

        bool Suelo(Vector2 pantalla, out Vector3 p)
        {
            var v = new Vector3(pantalla.x / Screen.width, pantalla.y / Screen.height, 0f);
            var ray = _cam.ViewportPointToRay(v); var plano = new Plane(Vector3.up, Vector3.zero);
            if (plano.Raycast(ray, out float e)) { p = ray.GetPoint(e); return true; }
            p = Vector3.zero; return false;
        }

        // ------------------------------------------------------------------ relleno local para ver la escala (NO es la simulación)
        Vector3 Centro() { Vector3 c = Vector3.zero; foreach (var s in _soldados) c += s.position; return _soldados.Count > 0 ? c / _soldados.Count : Vector3.zero; }

        void MoverRelleno(float dt)
        {
            var c = Centro(); var dir = _destino - c; dir.y = 0f; float vel = 3.2f;
            Vector3 perp = dir.sqrMagnitude > 1f ? Vector3.Cross(Vector3.up, dir.normalized) : Vector3.right;
            for (int i = 0; i < _soldados.Count; i++)
            {
                var puesto = _destino + perp * ((i - (_soldados.Count - 1) * 0.5f) * 2.2f);
                var t = _soldados[i]; var dd = puesto - t.position; dd.y = 0f;
                if (dd.magnitude > 0.2f) { t.position += dd.normalized * Mathf.Min(dd.magnitude, vel * dt); t.rotation = Quaternion.LookRotation(dd.normalized); }
            }
            for (int i = 0; i < _zombis.Count; i++)
            {
                var z = _zombis[i]; if (!z.vivo) continue;
                var aSold = c - z.t.position; aSold.y = 0f;
                Vector3 v = aSold.magnitude < 60f ? aSold.normalized * 1.6f : z.vel;
                if (aSold.magnitude >= 60f && _rng.NextDouble() < dt * 0.3) v = new Vector3((float)_rng.NextDouble() - 0.5f, 0f, (float)_rng.NextDouble() - 0.5f).normalized * 0.4f;
                if (aSold.magnitude > 2f) z.t.position += v * dt;
                if (v.sqrMagnitude > 0.01f) z.t.rotation = Quaternion.LookRotation(v.normalized);
                _zombis[i] = (z.t, v, true);
            }
            _fuego -= dt;
            if (_fuego <= 0f)
            {
                _fuego = 0.35f; int best = -1; float bd = 35f * 35f;
                for (int i = 0; i < _zombis.Count; i++) { if (!_zombis[i].vivo) continue; float d = (_zombis[i].t.position - c).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
                if (best >= 0)
                {
                    var z = _zombis[best]; var tirador = _soldados[_rng.Next(_soldados.Count)];
                    _trazadoras.Add((tirador.position + Vector3.up * 1.4f, z.t.position + Vector3.up * 1f, Time.time + 0.08f));
                    z.t.gameObject.SetActive(false); _zombis[best] = (z.t, Vector3.zero, false);
                }
            }
            _trazadoras.RemoveAll(x => x.hasta < Time.time);
        }

        // ------------------------------------------------------------------ montaje
        static Material Plano(Color c) { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0f); return m; }

        static GameObject Modelo(string carpeta, string fichero, Transform padre)
        {
            var prefab = Resources.Load<GameObject>("Models/" + carpeta + "/" + fichero); if (prefab == null) return null;
            var go = Instantiate(prefab, padre); var tex = Resources.Load<Texture2D>("Models/" + carpeta + "/T_Paleta32");
            var m = Plano(Color.white); if (tex != null) m.SetTexture("_BaseMap", tex);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = m;
            return go;
        }

        static GameObject Caja(string n, Vector3 pos, Vector3 tam, Material m, Transform padre)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = n; Destroy(go.GetComponent<Collider>()); go.transform.SetParent(padre, false);
            go.transform.position = pos; go.transform.localScale = tam; go.GetComponent<MeshRenderer>().sharedMaterial = m; return go;
        }

        void MontarTerreno()
        {
            const int res = 256; var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pix = new Color32[res * res];
            for (int y = 0; y < res; y++) for (int x = 0; x < res; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.045f + 3f, y * 0.045f + 7f), f = Mathf.PerlinNoise(x * 0.18f, y * 0.18f);
                Color32 c = n > 0.62f ? new Color32(0x5a, 0x66, 0x3a, 255) : n > 0.45f ? new Color32(0x7c, 0x74, 0x4c, 255) : new Color32(0x96, 0x88, 0x5c, 255);   // huerta, secano, tierra
                if (f > 0.72f) c = new Color32((byte)(c.r * 0.85f), (byte)(c.g * 0.85f), (byte)(c.b * 0.85f), 255);
                if (x > res * 0.86f) c = new Color32(0x2e, 0x44, 0x5c, 255);          // el mar al este
                pix[y * res + x] = c;
            }
            tex.SetPixels32(pix); tex.Apply();
            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane); suelo.name = "Terreno 2 × 2 km"; Destroy(suelo.GetComponent<Collider>());
            suelo.transform.position = new Vector3(Lado * 0.5f, 0f, Lado * 0.5f); suelo.transform.localScale = new Vector3(Lado / 10f, 1f, Lado / 10f);
            var m = Plano(Color.white); m.SetTexture("_BaseMap", tex); suelo.GetComponent<MeshRenderer>().sharedMaterial = m;
            // algo de relieve que se lea desde arriba: naranjos y caminos
            var raiz = new GameObject("Naranjos").transform; var naranjo = Resources.Load<GameObject>("Models/Sagunto/naranjo/SM_naranjo_LOD1");
            var mTronco = Plano(new Color32(0x44, 0x5c, 0x2c, 255));
            for (int i = 0; i < 900; i++)
            {
                var p = new Vector3((float)_rng.NextDouble() * Lado * 0.84f, 0f, (float)_rng.NextDouble() * Lado);
                if (Mathf.PerlinNoise(p.x / Lado * 11.5f + 3f, p.z / Lado * 11.5f + 7f) < 0.62f) continue;
                if (naranjo != null) { var go = Instantiate(naranjo, raiz); go.transform.position = p; go.transform.rotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f); }
                else Caja("Naranjo", p + Vector3.up * 1.5f, new Vector3(3f, 3f, 3f), mTronco, raiz);
            }
            var camino = Plano(new Color32(0xb4, 0xa0, 0x80, 255)); var rc = new GameObject("Caminos").transform;
            Caja("Camino", new Vector3(1000f, 0.02f, 1200f), new Vector3(1600f, 0.04f, 5f), camino, rc);
            Caja("Camino", new Vector3(1150f, 0.02f, 900f), new Vector3(5f, 0.04f, 1300f), camino, rc);
        }

        void MontarEstacion(string nombre, Vector3 pos)
        {
            var raiz = new GameObject("Estación " + nombre).transform; raiz.position = pos; _estaciones.Add((nombre, pos));
            var ed = Modelo("Sagunto/edificio_viajeros", "SM_edificio_viajeros_LOD1", raiz);
            if (ed != null) { ed.transform.localPosition = new Vector3(0f, 0f, 12f); ed.transform.localRotation = Quaternion.identity; }
            else Caja("Edificio", pos + new Vector3(0f, 4f, 12f), new Vector3(19f, 8f, 8f), Plano(new Color32(0xc8, 0xa8, 0x80, 255)), raiz);
            Caja("Andén", pos + new Vector3(0f, 0.45f, 4f), new Vector3(90f, 0.9f, 6f), Plano(new Color32(0x60, 0x5c, 0x48, 255)), raiz);
            var muro = Plano(new Color32(0x94, 0x94, 0x8c, 255));
            Caja("Muro N", pos + new Vector3(0f, 1.2f, 26f), new Vector3(100f, 2.4f, 0.6f), muro, raiz); Caja("Muro S", pos + new Vector3(0f, 1.2f, -18f), new Vector3(100f, 2.4f, 0.6f), muro, raiz);
            Caja("Muro O", pos + new Vector3(-50f, 1.2f, 4f), new Vector3(0.6f, 2.4f, 44f), muro, raiz); Caja("Muro E", pos + new Vector3(50f, 1.2f, 4f), new Vector3(0.6f, 2.4f, 44f), muro, raiz);
        }

        void MontarVia(Vector3 a, Vector3 b)
        {
            var raiz = new GameObject("Vía (trazado fijo del prototipo)").transform; var bal = Plano(new Color32(0x60, 0x5c, 0x48, 255)); var rail = Plano(new Color32(0x30, 0x30, 0x34, 255));
            Vector3 d = b - a; float len = d.magnitude; var mid = (a + b) * 0.5f; var rot = Quaternion.LookRotation(d.normalized);
            var balasto = Caja("Balasto", mid + Vector3.up * 0.05f, new Vector3(3.4f, 0.1f, len), bal, raiz); balasto.transform.rotation = rot;
            foreach (float off in new[] { -0.72f, 0.72f }) { var r = Caja("Carril", mid + rot * new Vector3(off, 0.15f, 0f), new Vector3(0.12f, 0.1f, len), rail, raiz); r.transform.rotation = rot; }
        }

        void MontarCaserio(Vector3 c)
        {
            var raiz = new GameObject("Caserío en ruinas").transform;
            for (int i = 0; i < 12; i++)
            {
                var p = c + new Vector3(((i % 4) - 1.5f) * 22f, 0f, ((i / 4) - 1f) * 24f);
                var casa = Modelo("Sagunto/casa_pueblo", "SM_casa_pueblo_LOD1", raiz) ?? Modelo("Sagunto/casa_baja", "SM_casa_baja_LOD1", raiz);
                if (casa != null) { casa.transform.position = p; casa.transform.rotation = Quaternion.Euler(0f, (i % 3) * 90f, 0f); }
                else Caja("Casa", p + Vector3.up * 3f, new Vector3(10f, 6f, 9f), Plano(new Color32(0xb8, 0x98, 0x78, 255)), raiz);
            }
            var rz = new GameObject("Zombis (relleno)").transform;
            for (int i = 0; i < 100; i++)
            {
                var p = c + new Vector3(((float)_rng.NextDouble() - 0.5f) * 110f, 0f, ((float)_rng.NextDouble() - 0.5f) * 90f);
                var z = Modelo("Zombi/camisa_283440", "SM_zombi_cercano_LOD1", rz);
                if (z == null) z = Caja("Zombi", p + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f), Plano(new Color32(0x1c, 0x18, 0x12, 255)), rz);
                z.transform.position = p; _zombis.Add((z.transform, Vector3.zero, true));
            }
        }

        void MontarPeloton(Vector3 c)
        {
            var raiz = new GameObject("Pelotón (relleno)").transform; _destino = c;
            for (int i = 0; i < 8; i++)
            {
                var s = Modelo("Infanteria", "SM_SoldadoArmado_v0_G36E_LOD1", raiz);
                if (s == null) s = Caja("Soldado", Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f), Plano(new Color32(0x50, 0x54, 0x1c, 255)), raiz);
                s.transform.position = c + new Vector3(i * 2.2f, 0f, 0f); _soldados.Add(s.transform);
            }
        }

        // ------------------------------------------------------------------ pantalla e iconos
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _rt == null) return;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _rt, ScaleMode.StretchToFill, false);
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15f * Screen.height / 1080f), alignment = TextAnchor.MiddleLeft }; st.normal.textColor = new Color32(0xf0, 0xdc, 0x9c, 255);
            foreach (var e in _estaciones)
            {
                var sp = APantalla(e.pos); if (sp.z < 0f) continue; float r = _altura > AlturaIconos ? 14f : 8f;
                GUI.color = new Color32(0xd4, 0xd0, 0xcc, 255); GUI.DrawTexture(new Rect(sp.x - r, sp.y - r, 2f * r, 2f * r), _blanco); GUI.color = Color.white;
                GUI.Label(new Rect(sp.x + r + 4f, sp.y - 12f, 220f, 24f), e.nombre, st);
            }
            if (_altura > AlturaIconos)
            {
                var c = APantalla(Centro()); if (c.z > 0f) { GUI.color = _seleccionado ? new Color32(0x80, 0xa8, 0xe0, 255) : new Color32(0x50, 0x6c, 0x9c, 255); GUI.DrawTexture(new Rect(c.x - 7f, c.y - 7f, 14f, 14f), _blanco); }
                GUI.color = new Color32(0xc0, 0x30, 0x28, 255);
                foreach (var z in _zombis) { if (!z.vivo) continue; var p = APantalla(z.t.position); if (p.z > 0f) GUI.DrawTexture(new Rect(p.x - 1.5f, p.y - 1.5f, 3f, 3f), _blanco); }
                GUI.color = Color.white;
            }
            GUI.color = new Color32(0xf0, 0xdc, 0x9c, 255);
            foreach (var t in _trazadoras) { var a = APantalla(t.a); var b = APantalla(t.b); if (a.z > 0f && b.z > 0f) Linea(a, b); }
            GUI.color = Color.white;
            int vivos = 0; foreach (var z in _zombis) if (z.vivo) vivos++;
            GUI.Label(new Rect(16f, 12f, 900f, 26f), $"Mundo (prototipo de zum) · altura {_altura:0} m · {(_altura > AlturaIconos ? "mapa" : "detalle")} · zombis {vivos}/100 · rueda: zum · WASD: mover · clic dcho: mandar el pelotón", st);
        }

        Vector3 APantalla(Vector3 w) { var v = _cam.WorldToViewportPoint(w); return new Vector3(v.x * Screen.width, (1f - v.y) * Screen.height, v.z); }

        void Linea(Vector3 a, Vector3 b)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b) / 3f), 1, 200);
            for (int i = 0; i <= n; i++) { var p = Vector3.Lerp(a, b, i / (float)n); GUI.DrawTexture(new Rect(p.x - 1f, p.y - 1f, 2f, 2f), _blanco); }
        }
    }
}
