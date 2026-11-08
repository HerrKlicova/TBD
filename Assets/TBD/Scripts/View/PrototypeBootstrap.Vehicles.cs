using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TBD.Units;
using TBD.Sim;
using TBD.Net;

namespace TBD.View
{
    /// <summary>
    /// Vehículos de la noche (D-N71 P1): VCR 8×8 Dragón, VAMTAC y helicóptero NH90 de Teo, con física arcade LOCAL (fuera del hash, decisión de Mak): aceleración, inercia, radio de giro,
    /// ruedas que giran y viran, torre que apunta sola y dispara (30 mm / ametralladora), atropello; y el NH90 con despegue, vuelo a 30 m con inclinación e inercia, rotores, sombra y MAG de puerta.
    /// Su daño a la horda se aplica como abatidos (igual que los soldados). Se eligen con clic o con su tarjeta; clic derecho = mover.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        sealed class Veh
        {
            public string Name; public bool Heli; public GameObject Go; public Vector2 Pos; public float Heading;          // rumbo = giro en Y de Unity (grados), +Z = 0
            public float Speed, Alt, Tilt, Rpm, SpinWheel, Steer, TurretYaw, GunElev; public Vector2 Target; public bool HasTarget; public Vector2 Home;
            public Transform[] WheelsFront, WheelsAll; public Quaternion[] WheelInit; public Transform Turret, Gun, RotorMain, RotorTail, Disc; public Quaternion TurretInit, GunInit, RotorMainInit, RotorTailInit;
            public float FireAt; public int FireTick; public float Ammo = 1f; public GameObject Shadow; public int State;                  // helicóptero: 0 en tierra, 1 despegando, 2 en vuelo, 3 aterrizando
            public float StateT; public float SpinMainDeg, SpinTailDeg; public bool Fire = true; public int ResupT = -1; public float ResupU;
        }
        readonly List<Veh> _vehs = new List<Veh>(); Veh _selVeh; readonly List<TargetHit> _vehHits = new List<TargetHit>(); readonly List<Vector2> _zomPos = new List<Vector2>(); readonly List<uint> _zomId = new List<uint>();
        readonly Dictionary<string, Material> _vehMats = new Dictionary<string, Material>();

        Veh MakeVeh(string name, string folder, string file, Vector2 pos, float headingDeg, bool heli, string[] wheels, string[] front, string turret, string gun)
        {
            var prefab = Resources.Load<GameObject>("Models/Vehiculos/" + folder + "/" + file); if (prefab == null) { Debug.LogWarning("[TBD] Falta el modelo " + folder + "/" + file); return null; }
            if (!_vehMats.TryGetValue(folder, out var mat) || mat == null) { var pal = Resources.Load<Texture2D>("Models/Vehiculos/" + folder + "/T_Paleta32"); mat = Mat("ffffff"); if (pal != null) mat.SetTexture("_BaseMap", pal); _vehMats[folder] = mat; }
            var go = Instantiate(prefab); go.name = name; go.transform.localScale = Vector3.one * RenderAssets.FigureScale;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            var v = new Veh { Name = name, Heli = heli, Go = go, Pos = pos, Heading = headingDeg, Home = pos };
            var all = new List<Transform>(); foreach (var n in wheels) { var t = Find(go.transform, n); if (t != null) all.Add(t); }
            v.WheelsAll = all.ToArray(); v.WheelInit = new Quaternion[v.WheelsAll.Length]; for (int i = 0; i < v.WheelsAll.Length; i++) v.WheelInit[i] = v.WheelsAll[i].localRotation;
            var fr = new List<Transform>(); foreach (var n in front) { var t = Find(go.transform, n); if (t != null) fr.Add(t); } v.WheelsFront = fr.ToArray();
            if (turret != null) { v.Turret = Find(go.transform, turret); if (v.Turret != null) v.TurretInit = v.Turret.localRotation; }
            if (gun != null) { v.Gun = Find(go.transform, gun); if (v.Gun != null) v.GunInit = v.Gun.localRotation; }
            if (heli)
            {
                v.RotorMain = Find(go.transform, "RotorPrincipal"); v.RotorTail = Find(go.transform, "RotorCola"); v.Disc = Find(go.transform, "DiscoRotor");
                if (v.RotorMain != null) v.RotorMainInit = v.RotorMain.localRotation; if (v.RotorTail != null) v.RotorTailInit = v.RotorTail.localRotation; if (v.Disc != null) v.Disc.gameObject.SetActive(false);
                var sh = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(sh.GetComponent<Collider>()); sh.name = "Sombra " + name; sh.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var sm = new Material(Shader.Find("Universal Render Pipeline/Unlit")); sm.SetColor("_BaseColor", new Color(0.05f, 0.06f, 0.03f, 1f)); sh.GetComponent<MeshRenderer>().sharedMaterial = sm; sh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; v.Shadow = sh;
            }
            go.transform.position = new Vector3(pos.x, 0f, pos.y); go.transform.rotation = Quaternion.Euler(0f, headingDeg, 0f);
            _vehs.Add(v); return v;
        }

        static Transform Find(Transform root, string name) { foreach (var c in root.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c; return null; }

        void InitVehicles()
        {
            string[] w8 = { "Rueda_1L", "Rueda_1R", "Rueda_2L", "Rueda_2R", "Rueda_3L", "Rueda_3R", "Rueda_4L", "Rueda_4R" };
            MakeVeh("Dragón", "dragon", "SM_VCR_Dragon_Animable_LOD1", new Vector2(99f, 53f), 180f, false, w8, new[] { "Rueda_1L", "Rueda_1R", "Rueda_2L", "Rueda_2R" }, "Torre", "Canon");
            string[] w4 = { "Rueda_1L", "Rueda_1R", "Rueda_2L", "Rueda_2R" };
            MakeVeh("VAMTAC 1", "vamtac", "SM_Vamtac_Animable_LOD1", new Vector2(88f, 53f), 180f, false, w4, new[] { "Rueda_1L", "Rueda_1R" }, "Aro", "Ametralladora");
            MakeVeh("VAMTAC 2", "vamtac", "SM_Vamtac_Animable_LOD1", new Vector2(80f, 53f), 180f, false, w4, new[] { "Rueda_1L", "Rueda_1R" }, "Aro", "Ametralladora");
            MakeVeh("NH90", "nh90", "SM_NH90_LOD1", new Vector2(180f, 44f), 270f, true, new string[0], new string[0], null, null);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vehfuego") >= 0) foreach (var vf in _vehs) vf.Fire = true;   // prueba de techo (Sara): fuego libre desde el principio
        }

        // ---------------------------------------------------------------- un tick de vehículos (30 Hz)
        void VehiclesTick(NativeSnapshot z)
        {
            float dt = 1f / 30f;
            foreach (var v in _vehs)
            {
                if (v.Go == null) continue;
                if (v.Heli)                                                                  // el NH90 sale solo cuando hay horda y vuelve a su plataforma cuando no queda ninguna (Eugenio: «no despega ni dispara»); la orden del jugador manda
                {
                    if (v.Fire && v.State == 0 && !v.HasTarget && _zomPos.Count > 30 && v.Ammo > 0.05f) { v.State = 1; v.StateT = 0f; v.Target = new Vector2(100f, 64f); v.HasTarget = true; Debug.Log("[NOCHE] NH90 despega: hay horda"); }
                    else if (v.State == 2 && !v.HasTarget && _zomPos.Count == 0) { v.Target = v.Home; v.HasTarget = true; v.State = 3; }
                    HeliStep(v, dt);
                }
                else GroundStep(v, dt);
                if (_bot != null && !_noVehResup && (!v.Heli || v.State == 0) && v.Ammo < 0.3f && _ng != null && _ng.Tick % 30 == 0) StartVehicleResupply(v); VehicleResupply(v); VehicleFire(v, z, dt);
            }
        }

        /// <summary>Recarga de un vehiculo del almacen (Hugo, D-N74/D-N101): Dragon 0,6 u y VAMTAC 1,2 u en 20 s junto al tren; NH90 1,3 u en 30 s en el helipuerto. Local (fuera del hash).</summary>
        void StartVehicleResupply(Veh v)
        {
            if (_uw == null || _ng == null || v.ResupT >= 0 || v.Ammo > 0.95f) return;
            bool near = v.Heli ? (v.State == 0 && (v.Pos - v.Home).magnitude < 12f) : (Mathf.Abs(v.Pos.y - Level.TrackY) < 12f && v.Pos.x > 70f && v.Pos.x < 156f);
            if (!near || v.Speed > 0.5f) { Debug.Log($"[NOCHE] recarga de {v.Name}: debe estar parado junto al {(v.Heli ? "helipuerto" : "tren")}"); return; }
            var spec = VehSpec(v); float u = spec.ResupplyMilliU / 1000f * (1f - v.Ammo); long cost = (long)(u * WeaponCatalog.ShotsPerU);   // coste y tiempo de NightVehicle (nightsim_params.json de Hugo, D-N128)
            if (_uw.StockShots < cost) { Debug.Log($"[NOCHE] recarga de {v.Name}: falta municion en el almacen"); return; }
            _uw.StockShots -= (int)cost; v.ResupU = u; v.ResupT = _ng.Tick + spec.ResupplyTicks; Sfx.Play("sfx_ui_clic", 0.6f);
            Debug.Log($"[NOCHE] recarga de {v.Name}: {u:0.0} u del almacen, lista a las {TimeStr()} + {(v.Heli ? 30 : 20)} s");
        }

        void VehicleResupply(Veh v)
        {
            if (v.ResupT < 0 || _ng == null) return;
            if (v.Speed > 1f && !v.Heli) { v.ResupT = -1; return; }        // si se mueve, se pierde la recarga en curso
            if (_ng.Tick >= v.ResupT) { v.Ammo = 1f; v.ResupT = -1; Debug.Log($"[NOCHE] {v.Name} recargado"); }
        }

        readonly bool _noVehResup = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sinrecarga") >= 0;   // QA (Sara): el bot no recarga los vehiculos
        readonly Dictionary<string, NightVehicle> _vehSpec = new Dictionary<string, NightVehicle>();
        /// <summary>Cifras del vehiculo de NightVehicle con los NightVehicles de nightsim_params.json aplicados (una sola fuente para la zona y para fuera de pantalla).</summary>
        NightVehicle VehSpec(Veh v)
        {
            string k = v.Heli ? "nh90" : v.Name.StartsWith("Drag") ? "dragon" : "vamtac";
            if (!_vehSpec.TryGetValue(k, out var s))
            {
                s = NightVehicle.Make(v.Name, v.Heli ? NightVehKind.Nh90 : k == "dragon" ? NightVehKind.Dragon : NightVehKind.Vamtac, 0, 0);
                try { NightVehicle.ApplyParams(s, BalanceSource.Read("nightsim_params.json")); } catch (System.Exception) { }
                _vehSpec[k] = s;
            }
            return s;
        }
        struct NativeSnapshot { public int N; }

        void GroundStep(Veh v, float dt)
        {
            float maxSpeed = 9f, accel = 4f, wheelbase = 4.5f, maxSteer = 35f;
            float desired = 0f, steerCmd = 0f;
            if (v.HasTarget)
            {
                var d = v.Target - v.Pos; float dist = d.magnitude; float want = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; float diff = Mathf.DeltaAngle(v.Heading, want);
                steerCmd = Mathf.Clamp(diff / 30f, -1f, 1f);
                desired = dist < 1.5f ? 0f : Mathf.Min(maxSpeed, Mathf.Sqrt(2f * 3f * dist)) * (Mathf.Abs(diff) > 100f ? 0.35f : 1f);
                if (dist < 1.5f) v.HasTarget = false;
            }
            v.Speed = Mathf.MoveTowards(v.Speed, desired, (desired > v.Speed ? accel : 6f) * dt);
            v.Steer = Mathf.MoveTowards(v.Steer, steerCmd * maxSteer, 120f * dt);
            v.Heading += v.Speed / wheelbase * Mathf.Tan(v.Steer * Mathf.Deg2Rad) * Mathf.Rad2Deg * dt;
            float hr = v.Heading * Mathf.Deg2Rad; var np = v.Pos + new Vector2(Mathf.Sin(hr), Mathf.Cos(hr)) * v.Speed * dt;
            if (WallBlocks(v.Pos, np)) { if (v.Speed > 1f) Debug.Log($"[NOCHE] choque {v.Name} contra el muro en ({v.Pos.x:0.0};{v.Pos.y:0.0})"); v.Speed = 0f; v.HasTarget = false; np = v.Pos; }
            v.Pos = np;
            v.Pos.x = Mathf.Clamp(v.Pos.x, 5f, 505f); v.Pos.y = Mathf.Clamp(v.Pos.y, 5f, 475f);
            v.SpinWheel += v.Speed / 0.55f * Mathf.Rad2Deg * dt;
        }

        /// <summary>¿El tramo a-b cruza un muro del recinto? Oeste (puerta cerrada), norte y este: macizos; sur: macizo salvo la puerta de vehiculos E4 (X 95-103).</summary>
        static bool WallBlocks(Vector2 a, Vector2 b)
        {
            float x0 = Level.FortX0, x1 = Level.FortX1, y0 = Level.FortY0, y1 = Level.FortY1;
            bool Cross(float wallC, float v0, float v1) { return (v0 - wallC) * (v1 - wallC) < 0f; }
            if (Cross(x0, a.x, b.x) && b.y > y0 - 1f && b.y < y1 + 1f) return true;                  // oeste
            if (Cross(x1, a.x, b.x) && b.y > y0 - 1f && b.y < y1 + 1f) return true;                  // este
            if (Cross(y1, a.y, b.y) && b.x > x0 - 1f && b.x < x1 + 1f) return true;                  // norte
            if (Cross(y0, a.y, b.y) && b.x > x0 - 1f && b.x < x1 + 1f && !(b.x >= 95f && b.x <= 103f)) return true;   // sur (E4 abierta)
            return false;
        }

        void HeliStep(Veh v, float dt)
        {
            const float cruiseAlt = 30f, cruiseSpeed = 22f;
            v.StateT += dt;
            float rpmTarget = v.State == 0 ? 0f : 1f; v.Rpm = Mathf.MoveTowards(v.Rpm, rpmTarget, dt / 3f);
            if (v.State == 1) { v.Alt = Mathf.MoveTowards(v.Alt, cruiseAlt, 4.5f * dt); if (v.Alt >= cruiseAlt - 0.1f) v.State = 2; }
            if (v.State == 2 || v.State == 3)
            {
                float desired = 0f;
                if (v.HasTarget)
                {
                    var d = v.Target - v.Pos; float dist = d.magnitude; float want = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                    v.Heading += Mathf.Clamp(Mathf.DeltaAngle(v.Heading, want), -60f * dt, 60f * dt);
                    desired = Mathf.Min(cruiseSpeed, Mathf.Sqrt(2f * 4f * Mathf.Max(0f, dist - 2f)));
                    if (dist < 3f) { v.HasTarget = false; if (v.State == 3 || (v.Target - v.Home).magnitude < 4f) v.State = 3; }
                }
                float prev = v.Speed; v.Speed = Mathf.MoveTowards(v.Speed, desired, 7f * dt);
                v.Tilt = Mathf.Lerp(v.Tilt, Mathf.Clamp((v.Speed - prev) / dt * 2.2f + v.Speed * 0.4f, -8f, 18f), 4f * dt);
                float hr = v.Heading * Mathf.Deg2Rad; v.Pos += new Vector2(Mathf.Sin(hr), Mathf.Cos(hr)) * v.Speed * dt;
                if (v.State == 3 && v.Speed < 1f) { v.Alt = Mathf.MoveTowards(v.Alt, 0f, 3.5f * dt); if (v.Alt <= 0.01f) { v.State = 0; v.Speed = 0f; v.Tilt = 0f; } }
            }
            else v.Tilt = Mathf.Lerp(v.Tilt, 0f, 6f * dt);
        }

        // ---------------------------------------------------------------- fuego automático del vehículo
        void VehicleFire(Veh v, NativeSnapshot z, float dt)
        {
            // los vehiculos disparan por defecto, tambien con -nightbot (decision de Mak/Eugenio, 7-nov); G = alto el fuego, A = fuego libre
            if (!v.Fire) return;                                                              // de noche solo disparan con orden del jugador (A = fuego, G = alto el fuego; Hugo/Mak)
            if (_zomPos.Count == 0) return; if (v.Heli && v.State != 2) return;
            if (v.Ammo <= 0f) return;                                                        // sin municion no dispara (reserva por vehiculo)
            float range = v.Heli ? 70f : (v.Name.StartsWith("Dragón") ? 55f : 40f); int best = -1; float bd = range * range;
            for (int i = 0; i < _zomPos.Count; i++) { float d = (_zomPos[i] - v.Pos).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            if (best < 0) return;
            v.TurretYaw = Mathf.MoveTowardsAngle(v.TurretYaw, Mathf.Atan2(_zomPos[best].x - v.Pos.x, _zomPos[best].y - v.Pos.y) * Mathf.Rad2Deg - (v.Heli ? 0f : v.Heading), 120f * dt);
            if (_ng != null && _ng.Tick < v.FireTick) return;                              // cadencia por ticks de la simulacion (no por tiempo de pantalla)
            bool dragon = v.Name.StartsWith("Dragón"); var vs = VehSpec(v); v.FireTick = (_ng != null ? _ng.Tick : 0) + vs.PeriodTicks; v.Ammo -= 1f / Mathf.Max(1, vs.MaxShots);   // Dragon 1 salva/s (120 salvas), VAMTAC y NH90 ~3 disparos/s (300-400)
            int kills = dragon ? 3 : 1; var tp = _zomPos[best];
            for (int i = 0; i < _zomPos.Count && kills > 0; i++) if ((_zomPos[i] - tp).sqrMagnitude < (dragon ? 9f : 1.2f)) { _vehHits.Add(new TargetHit { TargetId = (int)_zomId[i], Damage = 100, Platoon = -1 }); kills--; }
            if (_trMat == null) { _trMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); _trMat.SetColor("_BaseColor", new Color32(0xf0, 0xdc, 0x9c, 255)); }
            VfxVehicleShot(v, new Vector3(tp.x, 0.8f, tp.y));                          // trazadora, fogonazo, humo y casquillos desde la boca del arma (Bruno)
            Sfx.PlayDistAt(dragon ? "canon_30" : "ametralladora", new Vector3(v.Pos.x, v.Alt + 2f, v.Pos.y), 1f, dragon ? 0.5f : 0.25f); if (dragon) { Sfx.PlayAt("cola_urbana", new Vector3(v.Pos.x, v.Alt + 2f, v.Pos.y), 0.4f, 0.5f); }
        }

        // ---------------------------------------------------------------- atropello
        void RunOver(Veh v)
        {
            if (v.Heli || Mathf.Abs(v.Speed) < 2f) return; float hr = v.Heading * Mathf.Deg2Rad; var front = v.Pos + new Vector2(Mathf.Sin(hr), Mathf.Cos(hr)) * 3f; int n = 0;
            for (int i = 0; i < _zomPos.Count && n < 40; i++) if ((_zomPos[i] - front).sqrMagnitude < 6f) { _vehHits.Add(new TargetHit { TargetId = (int)_zomId[i], Damage = 100, Platoon = -1 }); n++; }
        }

        // ---------------------------------------------------------------- vista de los vehículos (cada fotograma)
        void UpdateVehicleViews()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var v in _vehs)
            {
                if (v.Go == null) continue;
                var t = v.Go.transform; t.position = Vector3.Lerp(t.position, new Vector3(v.Pos.x, v.Alt, v.Pos.y), 1f - Mathf.Exp(-20f * dt));
                if (!v.Heli)
                {
                    t.rotation = Quaternion.Slerp(t.rotation, Quaternion.Euler(0f, v.Heading, 0f), 1f - Mathf.Exp(-16f * dt));
                    for (int i = 0; i < v.WheelsAll.Length; i++)
                    {
                        bool fr = Array.IndexOf(v.WheelsFront, v.WheelsAll[i]) >= 0; float steer = fr ? v.Steer : 0f;
                        v.WheelsAll[i].localRotation = v.WheelInit[i] * Quaternion.Euler(v.SpinWheel, steer, 0f);
                    }
                    if (v.Turret != null) v.Turret.localRotation = v.TurretInit * Quaternion.Euler(0f, v.TurretYaw, 0f);
                    if (v.Gun != null) v.Gun.localRotation = v.GunInit * Quaternion.Euler(-Mathf.Clamp(v.GunElev, -10f, 45f), 0f, 0f);
                    RunOverCheck(v);
                }
                else
                {
                    t.rotation = Quaternion.Slerp(t.rotation, Quaternion.Euler(v.Tilt, v.Heading, 0f), 1f - Mathf.Exp(-6f * dt));
                    v.SpinMainDeg += v.Rpm * 900f * dt; v.SpinTailDeg += v.Rpm * 1800f * dt;
                    if (v.RotorMain != null) v.RotorMain.localRotation = v.RotorMainInit * Quaternion.Euler(0f, v.SpinMainDeg, 0f);
                    if (v.RotorTail != null) v.RotorTail.localRotation = v.RotorTailInit * Quaternion.Euler(v.SpinTailDeg, 0f, 0f);
                    if (v.Shadow != null) { float s = 6f + v.Alt * 0.12f; v.Shadow.transform.position = new Vector3(v.Pos.x, 0.14f, v.Pos.y); v.Shadow.transform.localScale = new Vector3(s, s * 1.5f, 1f); v.Shadow.transform.rotation = Quaternion.Euler(90f, v.Heading, 0f); v.Shadow.SetActive(v.Alt > 0.2f || v.Rpm > 0.1f); }
                    // (el rotor suena en Fx.cs, NightAudio: un AudioSource 3D por helicoptero; el bucle MP3 viejo en 2D se ha quitado)
                }
            }
        }

        void RunOverCheck(Veh v) { if (Time.frameCount % 2 == 0) RunOver(v); }

        // ---------------------------------------------------------------- selección y órdenes de vehículos (se llama desde la entrada de noche)
        Veh VehAt(Vector2 mp)
        {
            Veh best = null; float bd = Screen.height * 0.06f;
            foreach (var v in _vehs) { if (v.Go == null) continue; var sp = ScreenOf(new Vector3(v.Pos.x, v.Alt + 1.5f, v.Pos.y)); if (sp.z < 0) continue; float d = (new Vector2(sp.x, sp.y) - mp).magnitude; if (d < bd) { bd = d; best = v; } }
            return best;
        }

        void VehicleOrder(Veh v, Vector3 ground)
        {
            var tgt = new Vector2(ground.x, ground.z);
            if (v.Heli)
            {
                if ((tgt - v.Home).magnitude < 12f && v.State == 2) { v.Target = v.Home; v.HasTarget = true; v.State = 3; return; }
                if (v.State == 0) { v.State = 1; v.StateT = 0f; }
                v.Target = tgt; v.HasTarget = true; if (v.State == 3) v.State = 2;
            }
            else { v.Target = tgt; v.HasTarget = true; }
            Sfx.Play("ui_orden", Vector3.zero, 0.6f, 0.05f);
        }
    }
}
