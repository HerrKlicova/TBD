using System.Collections.Generic;
using UnityEngine;
using TBD.Units;

namespace TBD.View
{
    /// <summary>
    /// VFX de armas por código (Bruno, 7-nov; queja de Eugenio con la Noche55: «las balas no salen de sus cañones, no tienen fogonazos, no hay feedback»).
    /// Solo vista: no toca la simulación ni el hash. Todo sale de dos funciones:
    ///   VfxShot(arma, boca, blanco, alBlanco): fogonazo en la boca, cono de llama, destello de luz, humo, casquillo, trazadora que sale de la boca e impacto al llegar.
    ///   VfxBlast(punto, radio, arma): miniexplosión (30 mm, granada, C90, cohetes) con destello, bola de fuego, chispas, cascotes, anillo de polvo del radio y humo.
    /// Capas: lo que brilla (fogonazo, cono, chispas, fuego, trazadora) va en VFX_HD (LayerFx) con el material aditivo TBD/FxAdditive y brillo HDR para el bloom;
    /// lo que es materia (humo, polvo, cascotes, casquillos, sangre, anillo) va en la capa del mundo, para que se pixele con él.
    /// Paleta de 32, sin colores nuevos: f0dc9c crema, e08c30 ámbar (efectos de 0,25 s o menos), 7c3c18 óxido, b4a080 beige, 88885c oliva claro, 94948c hormigón claro,
    /// 605c48 oliva gris, 48403c gris pardo, 847c34 oliva amarillo (latón) y 4c1008 sangre oscura. Sin d4d0cc (selección) ni 982420 (ataque).
    /// </summary>
    public partial class PrototypeBootstrap
    {
        static readonly Color32 VxCrema = new Color32(0xf0, 0xdc, 0x9c, 255), VxAmbar = new Color32(0xe0, 0x8c, 0x30, 255), VxOxido = new Color32(0x7c, 0x3c, 0x18, 255),
            VxBeige = new Color32(0xb4, 0xa0, 0x80, 255), VxOlivaClaro = new Color32(0x88, 0x88, 0x5c, 255), VxHormigon = new Color32(0x94, 0x94, 0x8c, 255),
            VxOlivaGris = new Color32(0x60, 0x5c, 0x48, 255), VxGrisPardo = new Color32(0x48, 0x40, 0x3c, 255), VxLaton = new Color32(0x84, 0x7c, 0x34, 255),
            VxSangre = new Color32(0x4c, 0x10, 0x08, 255);

        /// <summary>Cómo se ve cada arma. Tamaños en metros, tiempos en segundos, luz en intensidad de URP.</summary>
        struct VxSpec
        {
            public float Flash, Cone, FlashT, Light, LightRange, LightT, Smoke, Shell, TracerW, TracerTail, TracerSpeed;
            public int SmokeEvery, Sparks, Dust; public bool Blast;
        }

        static VxSpec VxSpecOf(WeaponId w)
        {
            switch (w)
            {
                case WeaponId.Canon30:
                case WeaponId.Canon30Heli:   // 30 mm: fogonazo grande, luz fuerte, humo cada disparo, vaina gorda, trazadora gruesa y miniexplosión de 2,5 m
                    return new VxSpec { Flash = 1.6f, Cone = 2.4f, FlashT = 0.07f, Light = 14f, LightRange = 12f, LightT = 0.08f, Smoke = 1.1f, SmokeEvery = 1, Shell = 0.14f, TracerW = 0.20f, TracerTail = 9f, TracerSpeed = 150f, Blast = true };
                case WeaponId.MgPesada:      // 12,70 del VAMTAC
                case WeaponId.MgEmplazada:
                    return new VxSpec { Flash = 0.9f, Cone = 1.3f, FlashT = 0.05f, Light = 7f, LightRange = 7f, LightT = 0.05f, Smoke = 0.6f, SmokeEvery = 2, Shell = 0.09f, TracerW = 0.12f, TracerTail = 6f, TracerSpeed = 200f, Sparks = 4, Dust = 2 };
                case WeaponId.MgPuerta:      // MAG de puerta del NH90
                case WeaponId.MgLigera:
                    return new VxSpec { Flash = 0.7f, Cone = 1.0f, FlashT = 0.05f, Light = 5f, LightRange = 6f, LightT = 0.05f, Smoke = 0.45f, SmokeEvery = 3, Shell = 0.07f, TracerW = 0.10f, TracerTail = 5f, TracerSpeed = 200f, Sparks = 3, Dust = 2 };
                case WeaponId.Lanzagranadas:
                case WeaponId.C90:
                case WeaponId.Cohetes70:
                case WeaponId.CargaZapador:
                    return new VxSpec { Flash = 0.8f, Cone = 1.2f, FlashT = 0.06f, Light = 6f, LightRange = 6f, LightT = 0.06f, Smoke = 0.9f, SmokeEvery = 1, TracerW = 0.16f, TracerTail = 3f, TracerSpeed = 60f, Blast = true };
                default:                     // fusiles
                    return new VxSpec { Flash = 0.45f, Cone = 0.6f, FlashT = 0.04f, Light = 0f, Smoke = 0.3f, SmokeEvery = 4, Shell = 0.05f, TracerW = 0.08f, TracerTail = 5f, TracerSpeed = 200f, Sparks = 2, Dust = 1 };
            }
        }

        ParticleSystem _vxGlow, _vxCone, _vxSmoke, _vxDust, _vxDebris, _vxShell, _vxRing, _vxBlood, _vxMark;
        readonly List<Light> _vxLightLive = new List<Light>(); readonly List<float> _vxLightUntil = new List<float>(); readonly List<float> _vxLightI0 = new List<float>(); readonly Stack<Light> _vxLightFree = new Stack<Light>();
        struct VxPending { public float At; public Vector3 P; public WeaponId W; public bool Zombie; }
        readonly List<VxPending> _vxPending = new List<VxPending>();
        readonly Dictionary<GameObject, Transform> _vxBoca = new Dictionary<GameObject, Transform>();
        int _vxShotCount; bool _vxReady; bool _vxDebugBoca;

        // ---------------------------------------------------------------- texturas en píxel, hechas por código (punto, sin mips)
        static Texture2D VxTex(int n, System.Func<int, int, float> alpha)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(alpha(x, y))));
            t.SetPixels32(px); t.Apply(false, true); return t;
        }
        static float VxHash(int x, int y) { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xffff) / 65535f; }

        ParticleSystem VxSystem(string name, int layer, Material mat, int max, float gravity, ParticleSystemRenderMode mode, AnimationCurve size, float alphaEnd)
        {
            var go = new GameObject("VFX " + name); go.layer = layer; go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity; main.startSpeed = 0f; main.startLifetime = 1f; main.startSize = 1f; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(alphaEnd, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            if (size != null) { var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, size); }
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.renderMode = mode; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.None; r.minParticleSize = 0f; r.maxParticleSize = 10f;
            if (mode == ParticleSystemRenderMode.Stretch) { r.velocityScale = 0.05f; r.lengthScale = 1f; }
            ps.Play(); return ps;
        }

        void VxEnsure()
        {
            if (_vxReady) return; _vxReady = true;
            _vxDebugBoca = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vfxboca") >= 0;
            // brillo (aditivo HDR, capa VFX_HD): el shader dibuja un disco suave, así que no lleva textura
            var glow = new Material(_fxMat); glow.SetFloat("_Intensity", 6f);
            var cone = new Material(_fxMat); cone.SetFloat("_Intensity", 4f);
            // materia (alfa, capa del mundo): texturas en píxel
            var tPuff = VxTex(16, (x, y) => { float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(8f, 8f)) / 8f; return d < 0.62f + 0.3f * VxHash(x, y) - 0.15f ? 1f : 0f; });
            var tDot = VxTex(2, (x, y) => 1f);
            var tRing = VxTex(32, (x, y) => { float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)); return d > 13.5f && d < 16f && VxHash(x, y) > 0.25f ? 1f : 0f; });
            Material M(Texture2D t) { var m = new Material(Shader.Find("Sprites/Default")); m.mainTexture = t; return m; }
            int world = 0;
            _vxGlow = VxSystem("brillo", LayerFx, glow, 1500, 0f, ParticleSystemRenderMode.Billboard, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.35f), 0f);
            _vxCone = VxSystem("cono", LayerFx, cone, 400, 0f, ParticleSystemRenderMode.Stretch, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.5f), 0f);
            _vxSmoke = VxSystem("humo", world, M(tPuff), 800, -0.05f, ParticleSystemRenderMode.Billboard, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.7f), 0f);
            _vxDust = VxSystem("polvo", world, M(tPuff), 800, 0f, ParticleSystemRenderMode.Billboard, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f), 0f);
            _vxDebris = VxSystem("cascotes", world, M(tDot), 1000, 2.5f, ParticleSystemRenderMode.Billboard, null, 1f);
            _vxShell = VxSystem("casquillos", world, M(tDot), 600, 1.6f, ParticleSystemRenderMode.Billboard, null, 1f);
            _vxRing = VxSystem("anillo", world, M(tRing), 64, 0f, ParticleSystemRenderMode.HorizontalBillboard, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f), 0f);
            _vxBlood = VxSystem("sangre", world, M(tDot), 600, 1.4f, ParticleSystemRenderMode.Billboard, null, 1f);
            if (_vxDebugBoca) _vxMark = VxSystem("marca boca", world, M(tDot), 200, 0f, ParticleSystemRenderMode.Billboard, null, 1f);   // sin gravedad: la marca se queda quieta en la boca
        }

        /// <summary>La materia (polvo, humo, anillo) es sin luz: de noche va un paso más oscura para no brillar sobre el suelo (L* aprox. 26 en pantalla).</summary>
        bool VxNoche => _gradeIndex == 2;
        Color32 VxPolvo(int i) => VxNoche ? (i % 2 == 0 ? VxOlivaGris : VxGrisPardo) : (i % 2 == 0 ? VxBeige : VxOlivaClaro);
        Color32 VxHumo(byte a) { var c = VxNoche ? VxOlivaGris : VxHormigon; return new Color32(c.r, c.g, c.b, a); }

        static void VxEmit(ParticleSystem ps, Vector3 p, Vector3 vel, float size, float life, Color32 c)
        {
            var e = new ParticleSystem.EmitParams { position = p, velocity = vel, startSize = size, startLifetime = life, startColor = c, rotation = Random.Range(0f, 360f), applyShapeToPosition = false };
            ps.Emit(e, 1);
        }

        void VxLight(Vector3 p, float intensity, float range, float t)
        {
            if (intensity <= 0f) return;
            Light l;
            if (_vxLightFree.Count > 0) l = _vxLightFree.Pop();
            else if (_vxLightLive.Count < 8) { var go = new GameObject("Destello"); go.transform.SetParent(transform, false); l = go.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.None; l.color = VxCrema; }
            else { l = _vxLightLive[0]; _vxLightLive.RemoveAt(0); _vxLightUntil.RemoveAt(0); _vxLightI0.RemoveAt(0); }   // como mucho 8 luces a la vez: la más vieja se reutiliza
            l.transform.position = p; l.range = range; l.intensity = intensity; l.enabled = true;
            _vxLightLive.Add(l); _vxLightUntil.Add(Time.time + t); _vxLightI0.Add(intensity);
        }

        // ---------------------------------------------------------------- disparo
        /// <summary>Un disparo: fogonazo, luz, humo y casquillo en la boca; trazadora desde la boca; impacto (o miniexplosión) cuando la trazadora llega.
        /// impacto = false para los explosivos de la simulación (granada, C90): su explosión llega aparte por StepResult.Blasts y no debe salir dos veces.</summary>
        void VfxShot(WeaponId w, Vector3 boca, Vector3 blanco, bool zombie, bool tracer = true, bool impacto = true)
        {
            VxEnsure(); var s = VxSpecOf(w); _vxShotCount++;
            var d = blanco - boca; float dist = Mathf.Max(0.1f, d.magnitude); var dir = d / dist;
            VxEmit(_vxGlow, boca, Vector3.zero, s.Flash, s.FlashT, VxCrema);                                          // estrella en la boca
            VxEmit(_vxCone, boca + dir * (s.Cone * 0.35f), dir * (s.Cone * 8f), s.Cone * 0.45f, s.FlashT, VxAmbar);   // lengua de fuego hacia delante
            if (s.Flash >= 0.9f) VxEmit(_vxGlow, boca + dir * 0.4f, dir * 2f, s.Flash * 0.6f, s.FlashT * 1.4f, VxAmbar);
            VxLight(boca + Vector3.up * 0.4f, s.Light, s.LightRange, s.LightT);
            if (s.SmokeEvery > 0 && _vxShotCount % s.SmokeEvery == 0)
                VxEmit(_vxSmoke, boca + dir * 0.3f, dir * 1.2f + Vector3.up * 0.7f + Random.insideUnitSphere * 0.3f, s.Smoke, 1.1f + s.Smoke * 0.5f, VxHumo(150));
            if (s.Shell > 0f)                                                                                           // casquillo: sale por la derecha del arma y cae
            {
                var right = Vector3.Cross(Vector3.up, dir).normalized;
                VxEmit(_vxShell, boca - dir * 0.7f, right * Random.Range(1.8f, 2.8f) + Vector3.up * Random.Range(1.5f, 2.5f), s.Shell, 0.7f, VxLaton);
            }
            if (_vxDebugBoca) VxEmit(_vxMark, boca, Vector3.zero, 0.25f, 2f, VxCrema);                            // -vfxboca: marca la boca 2 s para ajustar los desplazamientos
            if (tracer)
            {
                SpawnTracer(boca, blanco, s.TracerSpeed, s.TracerW, s.TracerTail);
                if (_tracerLive.Count > 0) _tracerLive[_tracerLive.Count - 1].Lr.gameObject.layer = LayerFx;            // la trazadora, a resolución completa y con bloom
            }
            if (impacto) _vxPending.Add(new VxPending { At = Time.time + dist / Mathf.Max(1f, s.TracerSpeed), P = blanco, W = w, Zombie = zombie });
        }

        /// <summary>Impacto de bala: polvo y chispas en el suelo; si da a un zombi, un poco de sangre oscura. El 30 mm y los explosivos van a VfxBlast.</summary>
        void VfxImpact(WeaponId w, Vector3 p, bool zombie)
        {
            VxEnsure(); var s = VxSpecOf(w);
            if (s.Blast) { VfxBlast(new Vector3(p.x, 0f, p.z), WeaponCatalog.Get(w).RadiusMm / 1000f, w); return; }
            var g = new Vector3(p.x, 0.15f, p.z);
            for (int i = 0; i < s.Dust; i++)
                VxEmit(_vxDust, g + Random.insideUnitSphere * 0.15f, new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.6f, 1.2f), Random.Range(-0.5f, 0.5f)), Random.Range(0.3f, 0.55f), 0.5f, VxPolvo(i));
            for (int i = 0; i < s.Sparks; i++)
            {
                var v = Random.insideUnitSphere * 4f; v.y = Mathf.Abs(v.y) + 1.5f;
                VxEmit(_vxGlow, g, v, 0.09f, 0.12f, VxCrema);
            }
            if (zombie) for (int i = 0; i < 3; i++) VxEmit(_vxBlood, new Vector3(p.x, 0.9f, p.z), Random.insideUnitSphere * 1.5f + Vector3.up, 0.14f, 0.35f, VxSangre);
        }

        /// <summary>Miniexplosión. radio en metros (30 mm: 2,5; granada: 4; C90: 5; cohete: 7). El anillo de polvo marca el radio del daño.</summary>
        void VfxBlast(Vector3 p, float radio, WeaponId w)
        {
            VxEnsure(); float k = Mathf.Clamp(radio / 2.5f, 0.6f, 3f); var g = new Vector3(p.x, 0.2f, p.z);
            VxLight(g + Vector3.up * 1.2f, 10f * k, radio * 5f, 0.1f);
            VxEmit(_vxGlow, g + Vector3.up * 0.4f, Vector3.zero, 1.8f * k, 0.07f, VxCrema);                          // destello
            for (int i = 0; i < 5; i++)                                                                                 // bola de fuego, menos de 0,25 s (el ámbar es corto)
                VxEmit(_vxGlow, g + Vector3.up * 0.5f + Random.insideUnitSphere * 0.6f * k, Vector3.up * Random.Range(0.8f, 2f), Random.Range(1.0f, 1.6f) * k, Random.Range(0.16f, 0.24f), i < 2 ? VxCrema : VxAmbar);
            for (int i = 0; i < 12; i++)                                                                                // chispas
            {
                var v = Random.insideUnitSphere * Random.Range(6f, 10f) * Mathf.Sqrt(k); v.y = Mathf.Abs(v.y) * 0.8f + 2f;
                VxEmit(_vxGlow, g, v, 0.1f, Random.Range(0.18f, 0.3f), i % 3 == 0 ? VxAmbar : VxCrema);
            }
            for (int i = 0; i < 10; i++)                                                                                // cascotes
            {
                var v = Random.insideUnitSphere * Random.Range(3f, 6f) * Mathf.Sqrt(k); v.y = Mathf.Abs(v.y) + 3f;
                VxEmit(_vxDebris, g, v, Random.Range(0.08f, 0.16f), Random.Range(0.6f, 0.9f), i % 2 == 0 ? VxGrisPardo : VxOlivaGris);
            }
            VxEmit(_vxRing, new Vector3(p.x, 0.06f, p.z), Vector3.zero, radio * 2f, 0.45f, new Color32(VxPolvo(0).r, VxPolvo(0).g, VxPolvo(0).b, 170));   // anillo del radio
            for (int i = 0; i < 4; i++)                                                                                 // humo que queda
                VxEmit(_vxSmoke, g + Vector3.up * 0.6f + Random.insideUnitSphere * 0.5f * k, Vector3.up * Random.Range(0.6f, 1.1f) + Random.insideUnitSphere * 0.4f, Random.Range(1.0f, 1.5f) * k, Random.Range(1.4f, 2.0f),
                       i % 2 == 0 ? new Color32(VxGrisPardo.r, VxGrisPardo.g, VxGrisPardo.b, 190) : new Color32(VxOlivaGris.r, VxOlivaGris.g, VxOlivaGris.b, 170));
            for (int i = 0; i < 4; i++)                                                                                 // polvo bajo
                VxEmit(_vxDust, g + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * radio * 0.6f, new Vector3(Random.Range(-1f, 1f), 0.4f, Random.Range(-1f, 1f)), Random.Range(0.8f, 1.2f) * k, 0.8f, VxPolvo(i));
            if (w == WeaponId.Canon30 || w == WeaponId.Canon30Heli) Sfx.PlayDistAt("impacto_30mm", g, 1f, 0.35f);
        }

        // ---------------------------------------------------------------- vehículos: punto de boca
        /// <summary>
        /// Boca del arma de un vehículo. 1) Si el modelo trae un vacío llamado «Boca» (Teo puede añadirlo), se usa. 2) Si no, sale del pivote del arma (Gun, o Turret)
        /// más el largo del cañón en la dirección del blanco. Largos y alturas SIN VERIFICAR: ajustarlos con -vfxboca, que deja una marca crema 2 s en cada boca.
        /// </summary>
        Vector3 VxMuzzle(Veh v, Vector3 blanco)
        {
            if (!_vxBoca.TryGetValue(v.Go, out var boca)) { boca = Find(v.Go.transform, "Boca"); _vxBoca[v.Go] = boca; }
            if (boca != null) return boca.position;
            var flat = new Vector3(blanco.x - v.Pos.x, 0f, blanco.z - v.Pos.y); if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward; flat.Normalize();
            if (v.Heli)                                                                                                 // MAG de puerta: en el costado que mira al blanco
            {
                var fwd = Quaternion.Euler(0f, v.Heading, 0f) * Vector3.forward; var right = Vector3.Cross(Vector3.up, fwd);
                float side = Vector3.Dot(right, flat) >= 0f ? 1f : -1f;
                return new Vector3(v.Pos.x, v.Alt + 1.3f, v.Pos.y) + right * (1.4f * side) + fwd * 0.6f + flat * 0.6f;
            }
            bool dragon = v.Name.StartsWith("Drag");
            var pivot = v.Gun != null ? v.Gun.position : v.Turret != null ? v.Turret.position + Vector3.up * 0.3f : new Vector3(v.Pos.x, dragon ? 2.6f : 2.4f, v.Pos.y);
            return pivot + flat * (dragon ? 2.8f : 1.1f);
        }

        /// <summary>Disparo de un vehículo (llamar desde VehicleFire en lugar de SpawnTracer y SpawnFlash).</summary>
        void VfxVehicleShot(Veh v, Vector3 blanco)
        {
            bool dragon = v.Name.StartsWith("Drag");
            var w = dragon ? WeaponId.Canon30 : v.Heli ? WeaponId.MgPuerta : WeaponId.MgPesada;
            var boca = VxMuzzle(v, blanco);
            VfxShot(w, boca, blanco, true);
            if (!dragon) for (int i = 1; i < 3; i++) VxPendingBurst(w, boca, blanco, i * 0.09f);                      // la ametralladora suelta una ráfaga de 3 por disparo de la vista
        }

        readonly List<(float at, WeaponId w, Vector3 a, Vector3 b)> _vxBurst = new List<(float, WeaponId, Vector3, Vector3)>();
        void VxPendingBurst(WeaponId w, Vector3 a, Vector3 b, float delay) { _vxBurst.Add((Time.time + delay, w, a, b + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f)))); }

        // ---------------------------------------------------------------- cada fotograma (llamar junto a UpdateFx)
        void UpdateVfxArmas()
        {
            if (!_vxReady) return; float now = Time.time;
            for (int i = _vxBurst.Count - 1; i >= 0; i--) if (now >= _vxBurst[i].at) { var b = _vxBurst[i]; _vxBurst.RemoveAt(i); VfxShot(b.w, b.a, b.b, true, i % 2 == 0); }
            for (int i = _vxPending.Count - 1; i >= 0; i--) if (now >= _vxPending[i].At) { var p = _vxPending[i]; _vxPending.RemoveAt(i); VfxImpact(p.W, p.P, p.Zombie); }
            for (int i = _vxLightLive.Count - 1; i >= 0; i--)
            {
                float left = _vxLightUntil[i] - now; var l = _vxLightLive[i];
                if (left <= 0f) { l.enabled = false; _vxLightFree.Push(l); _vxLightLive.RemoveAt(i); _vxLightUntil.RemoveAt(i); _vxLightI0.RemoveAt(i); }
                else l.intensity = _vxLightI0[i] * Mathf.Clamp01(left / 0.05f);
            }
        }
    }
}
