using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TBD.View
{
    /// <summary>
    /// Recinto de Sagunto con el kit de defensas de Teo (Lucía, prioridad de arte del 9-nov): las 33 piezas de HESCO y sacos y las 3 puertas modeladas
    /// sustituyen a las cajas de WallVisual y a los sprites de puerta. Solo es vista: la simulación sigue con los muros de Works (BuildWall), en los mismos
    /// ejes y a la misma altura. Datos: Resources/Niveles/sagunto_recinto.json (mr-mak entregas/teo-modelado/sagunto-recinto, 942f7ef, revisado por Álvaro).
    /// -sinrecinto vuelve a las cajas.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        static readonly bool RecintoOn = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sinrecinto") < 0;
        bool _recintoModelos;                                     // true cuando el recinto de Teo está puesto: SyncWorks no dibuja cajas ni sprites de puerta
        readonly Dictionary<string, Material> _recintoMats = new Dictionary<string, Material>();
        readonly List<Transform> _recintoPuertas = new List<Transform>();

        void BuildRecintoSagunto(Transform parent)
        {
            if (!RecintoOn) return;
            var ta = Resources.Load<TextAsset>("Niveles/sagunto_recinto");
            if (ta == null) { Debug.LogWarning("[RECINTO] falta Resources/Niveles/sagunto_recinto.json: se quedan las cajas"); return; }
            var root = TBD.Units.Mini.Parse(ta.text) as Dictionary<string, object>;
            if (root == null) { Debug.LogWarning("[RECINTO] sagunto_recinto.json no se puede leer"); return; }
            var go = new GameObject("Recinto de Sagunto (Teo)"); go.transform.SetParent(parent, false);
            int piezas = 0, faltan = 0, puertas = 0;
            if (root.TryGetValue("piezas", out var po) && po is List<object> pl)
                foreach (var o in pl)
                {
                    if (!(o is Dictionary<string, object> p)) continue;
                    string ruta = Str(p, "ruta");
                    var inst = RecintoModelo(ruta + "_LOD1", go.transform);
                    if (inst == null) { faltan++; Debug.LogWarning("[RECINTO] falta " + ruta); continue; }
                    inst.transform.position = new Vector3(Num(p, "x"), 0f, Num(p, "z"));
                    inst.transform.rotation = Quaternion.Euler(0f, Num(p, "yaw"), 0f) * inst.transform.rotation;
                    float ex = Num(p, "escala_x");
                    if (ex != 0f && ex != 1f) { var sc = inst.transform.localScale; inst.transform.localScale = new Vector3(sc.x * ex, sc.y, sc.z); }
                    piezas++;
                }
            if (root.TryGetValue("puertas", out var qo) && qo is List<object> ql)
                foreach (var o in ql)
                {
                    if (!(o is Dictionary<string, object> q)) continue;
                    string malla = Str(q, "malla");
                    var inst = RecintoModelo(malla + "_e0_Sano_LOD1", go.transform);
                    if (inst == null) { faltan++; Debug.LogWarning("[RECINTO] falta la puerta " + malla); continue; }
                    var pos = q.TryGetValue("pos", out var pv) && pv is List<object> pp && pp.Count == 2 ? new Vector3(ToF(pp[0]), 0f, ToF(pp[1])) : Vector3.zero;
                    inst.transform.position = pos;
                    inst.transform.rotation = Quaternion.Euler(0f, Num(q, "yaw"), 0f) * inst.transform.rotation;
                    inst.name = "Puerta " + Str(q, "id");
                    _recintoPuertas.Add(inst.transform);
                    SetHojas(inst.transform, 0f);                      // cerrada (Teo: O +90 / E −90 abierta, hacia dentro)
                    puertas++;
                }
            if (root.TryGetValue("postes_catenaria_fuera", out var _)) { }
            _recintoModelos = piezas > 0 && faltan == 0;
            if (!_recintoModelos) { Destroy(go); _recintoPuertas.Clear(); }
            Debug.Log($"[RECINTO] Sagunto: {piezas} piezas y {puertas} puertas de Teo{(faltan > 0 ? $"; faltan {faltan}: se quedan las cajas" : "")}");
        }

        /// <summary>Abre (1) o cierra (0) las hojas de una puerta del recinto: Hoja_O gira +90°·t y Hoja_E −90°·t sobre su Y local (hacia dentro).</summary>
        static void SetHojas(Transform puerta, float t)
        {
            var o = FindDeep(puerta, "Hoja_O"); var e = FindDeep(puerta, "Hoja_E");
            if (o != null) o.localRotation = Quaternion.Euler(0f, 90f * t, 0f) * HojaBase(o);
            if (e != null) e.localRotation = Quaternion.Euler(0f, -90f * t, 0f) * HojaBase(e);
        }

        static readonly Dictionary<Transform, Quaternion> _hojaBase = new Dictionary<Transform, Quaternion>();
        static Quaternion HojaBase(Transform h) { if (!_hojaBase.TryGetValue(h, out var q)) { q = h.localRotation; _hojaBase[h] = q; } return q; }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        GameObject RecintoModelo(string ruta, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Models/" + ruta);
            if (prefab == null) return null;
            string folder = ruta.Contains("/") ? ruta.Substring(0, ruta.LastIndexOf('/')) : "";
            var inst = Instantiate(prefab, parent); inst.name = ruta.Substring(ruta.LastIndexOf('/') + 1);
            if (!_recintoMats.TryGetValue(folder, out var mat) || mat == null)
            {
                var tex = Resources.Load<Texture2D>("Models/" + folder + "/T_Paleta32");
                mat = Mat("ffffff"); if (tex != null) mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white); mat.enableInstancing = true; _recintoMats[folder] = mat;
            }
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.On; }
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Destroy(c);
            return inst;
        }

        static string Str(Dictionary<string, object> d, string k) { return d.TryGetValue(k, out var v) && v != null ? v.ToString() : ""; }
        static float Num(Dictionary<string, object> d, string k) { return d.TryGetValue(k, out var v) ? ToF(v) : 0f; }
        static float ToF(object v)
        {
            switch (v) { case double x: return (float)x; case float f: return f; case long l: return l; case int i: return i; }
            return v != null && float.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0f;
        }
    }
}
