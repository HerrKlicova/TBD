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
        public const float Lado = 2048f;                    // metros (terreno de Álvaro, alvaro/hito1-terreno)
        const float AlturaMin = 6f, AlturaMax = 4000f;      // Bruno (zum v1, aprobado por Lucía): SOLDADO 6–40, PUEBLO 40–250, COMARCA 250–900, MAPA 900–4000
        const float AlturaIconos = 250f;                    // por encima, las unidades se ven como símbolos (como en el mapa)

        Camera _cam, _pantalla; RenderTexture _rt;
        Vector3 _foco = new Vector3(1180f, 0f, 1040f); float _altura = 1800f, _alturaObj = 1800f, _yaw = 0f;
        Texture2D _blanco; bool _camGuion; float _camT0; bool _fpsLog; int _fpsN; float _fpsT, _fps;

        readonly List<Transform> _soldados = new List<Transform>();
        readonly List<(Transform t, Vector3 vel, bool vivo)> _zombis = new List<(Transform, Vector3, bool)>();
        Vector3 _destino; bool _seleccionado = true; float _fuego;
        readonly List<(Vector3 a, Vector3 b, float hasta)> _trazadoras = new List<(Vector3, Vector3, float)>();
        readonly List<(string nombre, Vector3 pos)> _estaciones = new List<(string, Vector3)>();
        System.Random _rng = new System.Random(1234);

        void Start()
        {
            Application.targetFrameRate = 120; if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fpslog") >= 0) { _fpsLog = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            { var ca = System.Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < ca.Length; i++) if (ca[i] == "-altura" && float.TryParse(ca[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hh)) { _altura = _alturaObj = hh; } }
            _blanco = Texture2D.whiteTexture; _camGuion = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-camguion") >= 0; _camT0 = Time.unscaledTime; if (_camGuion) Debug.Log("[MUNDO] -camguion: recorrido de cámara de 90 s en bucle");
            MontarCamaras();
            var ter = MontarTerrenoAlvaro();
            if (ter == null) { MontarTerreno(); MontarEstacion("Sagunto", new Vector3(700f, 0f, 1000f)); MontarEstacion("Nules", new Vector3(1450f, 0f, 760f)); MontarCaserio(new Vector3(1150f, 0f, 1350f)); MontarPeloton(new Vector3(760f, 0f, 980f)); }
            else MontarDesdeCsv(ter);
            var rv = gameObject.AddComponent<RedVista>(); if (_sim != null) rv.UsarMundo(_sim.M);      // Iker: la red del Hito 1 lee Mundo.Red del Mundo común (antes de su Start)
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
                if (kb.zKey.isPressed) _alturaObj = Mathf.Max(AlturaMin, _alturaObj * (1f - 1.5f * dt)); if (kb.xKey.isPressed) _alturaObj = Mathf.Min(AlturaMax, _alturaObj * (1f + 1.5f * dt));
                if (kb.fKey.wasPressedThisFrame) _foco = Centro();      // F: ir al pelotón
            }
            if (ms != null)
            {
                float s = ms.scroll.ReadValue().y;
                if (s != 0f)
                {
                    float notches = Mathf.Clamp(s / (Mathf.Abs(s) >= 10f ? 120f : 1f), -3f, 3f);
                    float antes = _alturaObj; _alturaObj = Mathf.Clamp(_alturaObj * Mathf.Pow(0.8f, notches), AlturaMin, AlturaMax);
                }
                if (!TrazandoVia && ms.leftButton.wasPressedThisFrame && Suelo(ms.position.ReadValue(), out var q)) _seleccionado = Vector3.Distance(q, Centro()) < Mathf.Max(15f, _altura * 0.05f);
                if (!TrazandoVia && ms.rightButton.wasPressedThisFrame && _seleccionado && Suelo(ms.position.ReadValue(), out var d)) { _destino = d; _sim?.Mover((int)(d.x * 1000f), (int)(d.z * 1000f)); }
            }
            if (_camGuion)      // Sara (H3): recorrido de cámara por guion — mapa, baja a soldado sobre el pelotón en el combate y vuelve al mapa; solo vista
            {
                float tc = Time.unscaledTime - _camT0, ciclo = tc % 90f;
                if (ciclo < 15f) { _alturaObj = 2500f; _foco = Vector3.Lerp(_foco, new Vector3(1300f, 0f, 1200f), dt); }
                else if (ciclo < 45f) { _alturaObj = 8f; _foco = Vector3.Lerp(_foco, Centro(), 3f * dt); _yaw += 20f * dt; }
                else if (ciclo < 70f) { _alturaObj = 120f; _foco = Vector3.Lerp(_foco, new Vector3(1440f, 0f, 1480f), dt); }
                else { _alturaObj = 3800f; }
            }
            _foco.x = Mathf.Clamp(_foco.x, 0f, Lado); _foco.z = Mathf.Clamp(_foco.z, 0f, Lado); _foco = EnSuelo(_foco);
            _altura = Mathf.Lerp(_altura, _alturaObj, 1f - Mathf.Exp(-10f * dt));
            ZumEstetica.Actualizar(_altura);
            if (!ZumEstetica.PuedeGirar(_altura)) _yaw = Mathf.MoveTowardsAngle(_yaw, 0f, 225f * dt);      // Bruno: por encima de 250 m (COMARCA) el norte vuelve arriba
            ColocarCamara();
            bool iconos = _altura > AlturaIconos;
            foreach (var sd in _soldados) sd.gameObject.SetActive(!iconos);
            for (int i = 0; i < _zombis.Count; i++) if (_zombis[i].vivo) _zombis[i].t.gameObject.SetActive(!iconos);
            if (_sim != null) DibujarSim(Time.unscaledDeltaTime); else MoverRelleno(Time.deltaTime);
            _fpsN++; _fpsT += Time.unscaledDeltaTime; if (_fpsT >= 2f) { _fps = _fpsN / _fpsT; if (_fpsLog) Debug.Log($"[MUNDO-FPS] altura {_altura:0} m · {_fps:0} fps"); _fpsN = 0; _fpsT = 0f; }
        }

        bool Suelo(Vector2 pantalla, out Vector3 p)
        {
            var v = new Vector3(pantalla.x / Screen.width, pantalla.y / Screen.height, 0f);
            var ray = _cam.ViewportPointToRay(v); var plano = new Plane(Vector3.up, new Vector3(0f, _foco.y, 0f));
            if (_alt != null) { for (float d = 0f; d < 15000f; d += Mathf.Max(0.5f, d * 0.004f)) { var q = ray.GetPoint(d); if (q.y <= Alt(q.x, q.z)) { p = q; return true; } } }
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
                t.position = EnSuelo(t.position);
            }
            for (int i = 0; i < _zombis.Count; i++)
            {
                var z = _zombis[i]; if (!z.vivo) continue;
                var aSold = c - z.t.position; aSold.y = 0f;
                Vector3 v = aSold.magnitude < 60f ? aSold.normalized * 1.6f : z.vel;
                if (aSold.magnitude >= 60f && _rng.NextDouble() < dt * 0.3) v = new Vector3((float)_rng.NextDouble() - 0.5f, 0f, (float)_rng.NextDouble() - 0.5f).normalized * 0.4f;
                if (aSold.magnitude > 2f) z.t.position = EnSuelo(z.t.position + v * dt);
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


        // ------------------------------------------------------------------ simulación de Nacho: solo se lee (lockstep)
        MundoVista _sim;
        void DibujarSim(float dt)
        {
            _sim.Avanzar(dt); var M = _sim.M; float k = 1f - Mathf.Exp(-25f * dt);
            if (M.Unidades.Platoons.Count > 0)
            {
                var men = M.Unidades.Platoons[0].Men;
                for (int i = 0; i < _soldados.Count && i < men.Count; i++)
                {
                    var m = men[i]; var t = _soldados[i]; if (!m.Alive) { t.gameObject.SetActive(false); continue; }
                    var objetivo = EnSuelo(new Vector3(m.X / 1000f, 0f, m.Y / 1000f)); t.position = Vector3.Lerp(t.position, objetivo, k);
                    float rumbo = m.Heading / 65536f * 360f; t.rotation = Quaternion.Euler(0f, 90f - rumbo, 0f);
                }
            }
            var zs = M.Zombis.Lista;
            for (int i = 0; i < _zombis.Count && i < zs.Count; i++)
            {
                var z = zs[i]; var zt = _zombis[i];
                if (!z.Vivo) { if (zt.vivo) { zt.t.gameObject.SetActive(false); _zombis[i] = (zt.t, Vector3.zero, false); } continue; }
                var objetivo = EnSuelo(new Vector3(z.X / 1000f, 0f, z.Y / 1000f)); zt.t.position = Vector3.Lerp(zt.t.position, objetivo, k);
                zt.t.rotation = Quaternion.Euler(0f, 90f - z.Rumbo / 65536f * 360f, 0f);
            }
            var fuego = M.UltimoFuego;
            if (fuego != null && M.Unidades.Platoons.Count > 0) foreach (var sh in fuego.Shots)
            {
                var men = M.Unidades.Platoons[0].Men; Vector3 a = Centro();
                foreach (var mm in men) if (mm.Id == sh.Man) { a = EnSuelo(new Vector3(mm.X / 1000f, 0f, mm.Y / 1000f)); break; }
                var b = EnSuelo(new Vector3(sh.AimX / 1000f, 0f, sh.AimY / 1000f)) + Vector3.up * 1f;
                _trazadoras.Add((a + Vector3.up * 1.4f, b, Time.time + 0.08f));
            }
            _trazadoras.RemoveAll(x => x.hasta < Time.time);
            // Nacho: palés del caserío en el suelo (cayó su portador). Solo se dibuja: el estado es de la simulación.
            if (M.MaterialEnSuelo)
            {
                if (_montonPales == null) _montonPales = Caja("Palés en el suelo", Vector3.zero, new Vector3(2.4f, 1.2f, 2.4f), Plano(new Color32(0x8c, 0x6c, 0x40, 255)), transform).transform;
                _montonPales.gameObject.SetActive(true); _montonPales.position = EnSuelo(new Vector3(M.SueloX / 1000f, 0f, M.SueloY / 1000f)) + Vector3.up * 0.6f;
            }
            else if (_montonPales != null) _montonPales.gameObject.SetActive(false);
        }
        Transform _montonPales;
        /// <summary>Aviso de los palés (Nacho, [MATERIAL]): lo lleva el pelotón o están en el suelo.</summary>
        public string AvisoMaterial { get { var M = _sim != null ? _sim.M : null; if (M == null) return null; if (M.Portador >= 0) return "El pelotón lleva 4 palés a B: va más lento y no dispara"; if (M.MaterialEnSuelo) return "Palés en el suelo: que otro pelotón pase a recogerlos"; return null; } }
        void OnApplicationQuit() { _sim?.Cerrar(); }

        // ------------------------------------------------------------------ terreno y posiciones de Álvaro (StreamingAssets/Hito1)
        float[,] _alt;      // alturas en metros, [z, x], 1 m por muestra (Álvaro)
        float Alt(float x, float z)
        {
            if (_alt == null) return 0f; x = Mathf.Clamp(x, 0f, 2047.999f); z = Mathf.Clamp(z, 0f, 2047.999f); int ix = (int)x, iz = (int)z; float fx = x - ix, fz = z - iz;
            return Mathf.Lerp(Mathf.Lerp(_alt[iz, ix], _alt[iz, ix + 1], fx), Mathf.Lerp(_alt[iz + 1, ix], _alt[iz + 1, ix + 1], fx), fz);
        }
        Vector3 EnSuelo(Vector3 p) { if (_alt != null) p.y = Alt(p.x, p.z); return p; }
        // Iker (red del Hito 1): lo que necesita RedVista de esta escena, sin tocar lo demás
        public Camera CamaraMundo { get { return _cam; } }
        public float AlturaSuelo(float x, float z) { return Alt(x, z); }
        public bool PuntoSuelo(Vector2 pantalla, out Vector3 p) { return Suelo(pantalla, out p); }
        public float AlturaCamara { get { return _altura; } }
        public void Enfocar(float x, float z) { _foco = new Vector3(x, 0f, z); }
        /// <summary>RedVista se queda con el clic izquierdo mientras se traza una vía.</summary>
        public static bool TrazandoVia;
        /// <summary>El Mundo común (el de MundoVista): RedVista usa su Red en lugar de una propia.</summary>
        public Mundo MundoComun { get { return _sim != null ? _sim.M : null; } }
        public float AlphaSim { get { return _sim != null ? _sim.Alpha : 0f; } }

        Transform MontarTerrenoAlvaro()
        {
            string dir = System.IO.Path.Combine(Application.streamingAssetsPath, "Hito1"), raw = System.IO.Path.Combine(dir, "alturas_2049_u16le.raw");
            if (!System.IO.File.Exists(raw)) { Debug.LogWarning("[MUNDO] falta " + raw + ": terreno de relleno"); return null; }
            const int n = 2049; var bytes = System.IO.File.ReadAllBytes(raw); var h = new float[n, n];
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) { int i = (z * n + x) * 2; h[z, x] = (bytes[i] | (bytes[i + 1] << 8)) / 65535f; }   // fila 0 = Z 0 (sur); TerrainData es [z, x]
            _alt = new float[n, n]; for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) _alt[z, x] = -10f + h[z, x] * 128f;
            var usosPng = System.IO.Path.Combine(dir, "usos_2049.png"); Texture2D usos = null;
            if (System.IO.File.Exists(usosPng)) { var u = new Texture2D(2, 2); if (u.LoadImage(System.IO.File.ReadAllBytes(usosPng))) usos = u; }
            Color32[] paleta = { new Color32(0x2e, 0x44, 0x5c, 255), new Color32(0xc8, 0xb4, 0x8c, 255), new Color32(0x5c, 0x64, 0x48, 255), new Color32(0x6c, 0x74, 0x40, 255), new Color32(0x96, 0x88, 0x5c, 255),
                                 new Color32(0x6c, 0x60, 0x44, 255), new Color32(0xa8, 0x98, 0x78, 255), new Color32(0x44, 0x44, 0x48, 255), new Color32(0x8c, 0x80, 0x64, 255), new Color32(0x7c, 0x6c, 0x54, 255), new Color32(0x4a, 0x5c, 0x2c, 255) };
            var col = new Texture2D(2048, 2048, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[2048 * 2048];
            Color32[] up = usos != null ? usos.GetPixels32() : null; int uw = usos != null ? usos.width : 0, uh = usos != null ? usos.height : 0;
            for (int z = 0; z < 2048; z++) for (int x = 0; x < 2048; x++)
            {
                int id = 3; if (up != null) id = up[Mathf.Min(z, uh - 1) * uw + Mathf.Min(x, uw - 1)].r;      // LoadImage deja la fila 0 abajo (sur): coincide con Z
                var c = paleta[Mathf.Clamp(id, 0, paleta.Length - 1)];
                uint hh = (uint)(x * 73856093) ^ (uint)(z * 19349663); float k = 0.92f + ((hh >> 7) & 15u) / 15f * 0.12f;   // grano de 1 m
                px[z * 2048 + x] = new Color32((byte)Mathf.Min(255f, c.r * k), (byte)Mathf.Min(255f, c.g * k), (byte)Mathf.Min(255f, c.b * k), 255);
            }
            col.SetPixels32(px); col.Apply();
            var mat = Plano(Color.white); mat.SetTexture("_BaseMap", col);
            Material zum = up != null ? ZumEstetica.MaterialSuelo(ZumEstetica.TexturaUsos(up, uw, uh), naranjos3D: true) : null;      // Bruno (zum-shaders)
            const int trozo = 128, paso = 2; var raizT = new GameObject("Terreno Hito 1 (Álvaro, mallas)").transform;
            for (int cz = 0; cz < 2048; cz += trozo) for (int cx = 0; cx < 2048; cx += trozo)
            {
                int m = trozo / paso + 1; var v = new Vector3[m * m]; var uv = new Vector2[m * m]; var tri = new int[(m - 1) * (m - 1) * 6]; int t = 0;
                for (int a = 0; a < m; a++) for (int b = 0; b < m; b++) { int x = cx + b * paso, z = cz + a * paso; v[a * m + b] = new Vector3(x, _alt[z, x], z); uv[a * m + b] = new Vector2(x / 2048f, z / 2048f); }
                for (int a = 0; a < m - 1; a++) for (int b = 0; b < m - 1; b++) { int i0 = a * m + b; tri[t++] = i0; tri[t++] = i0 + m; tri[t++] = i0 + 1; tri[t++] = i0 + 1; tri[t++] = i0 + m; tri[t++] = i0 + m + 1; }
                var mesh = new Mesh { name = $"trozo {cx},{cz}" }; mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var go = new GameObject(mesh.name); go.transform.SetParent(raizT, false); go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = zum != null ? zum : mat; mr.shadowCastingMode = ShadowCastingMode.On;
            }
            var mar = GameObject.CreatePrimitive(PrimitiveType.Plane); mar.name = "Mar"; Destroy(mar.GetComponent<Collider>()); mar.transform.position = new Vector3(1024f, 0f, 1024f); mar.transform.localScale = new Vector3(600f, 1f, 600f);
            mar.GetComponent<MeshRenderer>().sharedMaterial = Plano(new Color32(0x2e, 0x44, 0x5c, 255));
            var mm = zum != null ? ZumEstetica.MaterialMar(zum.GetTexture("_Usos") as Texture2D) : null; if (mm != null) mar.GetComponent<MeshRenderer>().sharedMaterial = mm;
            Debug.Log("[MUNDO] terreno de Álvaro: 2049² muestras, 2048 × 128 × 2048 m en Y = −10" + (usos != null ? ", con usos del suelo" : ", sin usos"));
            return raizT;
        }

        /// <summary>Monta la VISTA a partir del estado del Mundo de Nacho (Mundo.Caserio lee los CSV; aquí no se lee ninguno): estaciones en Mundo.Puntos,
        /// edificios, tapias y naranjales en Mundo.Casas, zombis en Mundo.Zombis y el pelotón en Mundo.Unidades. Con -relleno, el relleno local de antes.</summary>
        void MontarDesdeCsv(Transform ter)
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-relleno") >= 0) { MontarCaserio(new Vector3(1440f, 0f, 1480f)); MontarPeloton(new Vector3(1290f, 0f, 1600f)); return; }
            try { _sim = new MundoVista(20261111u); }
            catch (System.Exception e) { Debug.LogWarning("[MUNDO] no se pudo crear el Mundo de Nacho (" + e.Message + "): relleno local"); MontarCaserio(new Vector3(1440f, 0f, 1480f)); MontarPeloton(new Vector3(1290f, 0f, 1600f)); return; }
            var M = _sim.M; Vector3 Pm(int x, int y) { return EnSuelo(new Vector3(x / 1000f, 0f, y / 1000f)); }
            if (M.Puntos.TryGetValue("A", out var pa)) MontarEstacionCsv("Estación A", Pm(pa.X, pa.Y), 41f, 86f);      // medidas del recinto: Álvaro, terreno-hito1.md (solo vista)
            if (M.Puntos.TryGetValue("B", out var pbb)) MontarEstacionCsv("Estación B", Pm(pbb.X, pbb.Y), 30f, 60f);
            int ned = 0, ntap = 0, nnar = 0; var rc = new GameObject("Caserío y naranjales (Mundo.Casas)").transform; var naranjo = Resources.Load<GameObject>("Models/Sagunto/naranjo/SM_naranjo_LOD1");
            var mEd = Plano(new Color32(0xb8, 0x98, 0x78, 255)); var mTapia = Plano(new Color32(0x9c, 0x8c, 0x70, 255)); var mNar = Plano(new Color32(0x44, 0x5c, 0x2c, 255));
            foreach (var c in M.Casas)
            {
                float x0 = c.R.X0 / 1000f, z0 = c.R.Y0 / 1000f, x1 = c.R.X1 / 1000f, z1 = c.R.Y1 / 1000f, alto = c.AlturaMm / 1000f; var centro = EnSuelo(new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f));
                string id = c.Id ?? "";
                if (!c.TapaPaso)       // naranjales: tapan la vista, no el paso
                {
                    for (float x = x0 + 3f; x < x1; x += 7f) for (float z = z0 + 3f; z < z1; z += 7f)
                    { var p = EnSuelo(new Vector3(x, 0f, z)); if (naranjo != null) { var g = Instantiate(naranjo, rc); g.transform.position = p; } else Caja("Naranjo", p + Vector3.up * 1.6f, new Vector3(3f, 3.2f, 3f), mNar, rc); nnar++; }
                }
                else if (id.StartsWith("tapia") || Mathf.Min(x1 - x0, z1 - z0) < 1.2f) { Caja("Tapia " + id, centro + Vector3.up * alto * 0.5f, new Vector3(x1 - x0, alto, z1 - z0), mTapia, rc); ntap++; }
                else { Caja("Edificio " + id, centro + Vector3.up * alto * 0.5f, new Vector3(x1 - x0, alto, z1 - z0), mEd, rc); ned++; }
            }
            var rz = new GameObject("Zombis (Mundo.Zombis)").transform;
            foreach (var z in M.Zombis.Lista)
            {
                var go = Modelo("Zombi/camisa_283440", "SM_zombi_cercano_LOD1", rz) ?? Caja("Zombi", Vector3.zero, new Vector3(0.6f, 1.8f, 0.6f), Plano(new Color32(0x1c, 0x18, 0x12, 255)), rz);
                go.transform.position = Pm(z.X, z.Y); _zombis.Add((go.transform, Vector3.zero, true));
            }
            var pel = M.Unidades.Platoons.Count > 0 ? Pm(M.Unidades.Platoons[0].AX, M.Unidades.Platoons[0].AY) : new Vector3(1290f, 0f, 1600f);
            int nm = M.Unidades.Platoons.Count > 0 ? M.Unidades.Platoons[0].Men.Count : 8; MontarPeloton(pel);
            while (_soldados.Count > nm) { Destroy(_soldados[_soldados.Count - 1].gameObject); _soldados.RemoveAt(_soldados.Count - 1); }
            for (int i = 0; i < _soldados.Count; i++) { var m = M.Unidades.Platoons[0].Men[i]; _soldados[i].position = Pm(m.X, m.Y); }
            _foco = EnSuelo(new Vector3(1180f, 0f, 1040f));
            Debug.Log($"[MUNDO] vista desde el Mundo: {M.Zombis.Lista.Count} zombis, {ned} edificios, {ntap} tapias, {nnar} naranjos, pelotón en ({pel.x:0}; {pel.z:0})");
        }

        void MontarEstacionCsv(string nombre, Vector3 pos, float ancho, float fondo)
        {
            var raiz = new GameObject(nombre).transform; raiz.position = pos; _estaciones.Add((nombre, pos));
            var ed = Modelo("Sagunto/edificio_viajeros", "SM_edificio_viajeros_LOD1", raiz);
            if (ed != null) { ed.transform.position = pos + new Vector3(ancho * 0.25f, 0f, 0f); ed.transform.rotation = Quaternion.Euler(0f, 90f, 0f); }
            var muro = Plano(new Color32(0x94, 0x94, 0x8c, 255)); float hx = ancho * 0.5f, hz = fondo * 0.5f;
            Caja("Muro N", pos + new Vector3(0f, 1.2f, hz), new Vector3(ancho, 2.4f, 0.6f), muro, raiz); Caja("Muro S", pos + new Vector3(0f, 1.2f, -hz), new Vector3(ancho, 2.4f, 0.6f), muro, raiz);
            Caja("Muro O", pos + new Vector3(-hx, 1.2f, 0f), new Vector3(0.6f, 2.4f, fondo), muro, raiz); Caja("Muro E", pos + new Vector3(hx, 1.2f, 0f), new Vector3(0.6f, 2.4f, fondo), muro, raiz);
            Caja("Andén", pos + new Vector3(-ancho * 0.15f, 0.45f, 0f), new Vector3(6f, 0.9f, fondo * 0.8f), Plano(new Color32(0x60, 0x5c, 0x48, 255)), raiz);
        }

        // ------------------------------------------------------------------ pantalla e iconos
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _rt == null) return;
            ZumEstetica.DibujarPantalla(new Rect(0, 0, Screen.width, Screen.height), _rt, _altura);
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15f * Screen.height / 1080f), alignment = TextAnchor.MiddleLeft }; st.normal.textColor = new Color32(0xf0, 0xdc, 0x9c, 255);
            foreach (var e in _estaciones)
            {
                var sp = APantalla(e.pos); if (sp.z < 0f) continue; float r = _altura > AlturaIconos ? 14f : 8f;
                ZumEstetica.DibujarIcono(new Vector2(sp.x, sp.y), "estacion", ZumEstetica.IconoPxPantalla(_altura), ZumEstetica.Hueso);      // Bruno: icono de estación
                GUI.Label(new Rect(sp.x + r + 4f, sp.y - 12f, 220f, 24f), e.nombre, st);
            }
            if (_altura > AlturaIconos)
            {
                var c = APantalla(Centro()); if (c.z > 0f) { GUI.color = _seleccionado ? new Color32(0x80, 0xa8, 0xe0, 255) : new Color32(0x50, 0x6c, 0x9c, 255); GUI.color = Color.white; ZumEstetica.DibujarIcono(new Vector2(c.x, c.y), "soldado", ZumEstetica.IconoPxPantalla(_altura), _seleccionado ? ZumEstetica.Hueso : new Color32(0x94, 0x94, 0x8c, 255)); }
                GUI.color = new Color32(0xc0, 0x30, 0x28, 255);
                foreach (var z in _zombis) { if (!z.vivo) continue; var p = APantalla(z.t.position); if (p.z > 0f) GUI.DrawTexture(new Rect(p.x - 1.5f, p.y - 1.5f, 3f, 3f), _blanco); }
                GUI.color = Color.white;
            }
            GUI.color = new Color32(0xf0, 0xdc, 0x9c, 255);
            foreach (var t in _trazadoras) { var a = APantalla(t.a); var b = APantalla(t.b); if (a.z > 0f && b.z > 0f) Linea(a, b); }
            GUI.color = Color.white;
            if (_sim != null && _sim.M.Puntos.TryGetValue("caserio", out var pcas))      // Lucía: la placa del caserío dice cuántos zombis quedan (solo lectura del Mundo)
            {
                int quedan = 0; foreach (var z in _sim.M.Zombis.Lista) if (z.Vivo) quedan++;
                var sc = APantalla(EnSuelo(new Vector3(pcas.X / 1000f, 0f, pcas.Y / 1000f)) + Vector3.up * 12f);
                if (sc.z > 0f) GUI.Label(new Rect(sc.x - 90f, sc.y - 14f, 260f, 28f), quedan > 0 ? $"<b>Caserío · quedan {quedan}</b>" : "<b>Caserío · limpio</b>", st);
            }
            var aviso = AvisoMaterial; if (aviso != null) GUI.Label(new Rect(16f, 40f, 900f, 26f), "<color=#e0b050>" + aviso + "</color>", st);
            int vivos = 0; foreach (var z in _zombis) if (z.vivo) vivos++;
            GUI.Label(new Rect(16f, 12f, 900f, 26f), $"Mundo (prototipo de zum) · {_fps:0} fps · altura {_altura:0} m · {(_altura > AlturaIconos ? "mapa" : "detalle")} · zombis {vivos}/100 · rueda: zum · WASD: mover · clic dcho: mandar el pelotón", st);
        }

        Vector3 APantalla(Vector3 w) { var v = _cam.WorldToViewportPoint(w); return new Vector3(v.x * Screen.width, (1f - v.y) * Screen.height, v.z); }

        void Linea(Vector3 a, Vector3 b)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b) / 3f), 1, 200);
            for (int i = 0; i <= n; i++) { var p = Vector3.Lerp(a, b, i / (float)n); GUI.DrawTexture(new Rect(p.x - 1f, p.y - 1f, 2f, 2f), _blanco); }
        }
    }
}
