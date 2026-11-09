using System.Collections.Generic;
using TBD.Net;
using TBD.Sim;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Puerta de vía visible (S2 de Eugenio con la I-Noche107: en el muro oeste de Sagunto se veía un hueco con soldados encima aunque la sim la tiene cerrada).
    /// Modelo de Teo Defensas/puerta_via/SM_PuertaVia: 5 variantes por posición (semilla determinista) y 4 estados por la vida de la pieza.
    /// Cerrada de noche y de día mientras no pasa el tren; abierta (oculta) cuando el tren está en el hueco. Solo vista: no toca la sim.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        static readonly string[] PuertaViaVar = { "TuboChapa", "ChapaOndulada", "Tablones", "MallaEspino", "Blindada" };
        static readonly string[] PuertaViaEst = { "Sano", "Danado", "MuyDanado", "Brecha" };
        class PuertaViaVista { public GameObject Go; public int Pieza, Estado = -1; public Vector3 A, B; }
        readonly Dictionary<int, PuertaViaVista> _puertasVia = new Dictionary<int, PuertaViaVista>();
        Material _puertaViaMat;

        static int EstadoPuerta(int lifeMil) { return lifeMil >= 750 ? 0 : lifeMil >= 400 ? 1 : lifeMil > 0 ? 2 : 3; }

        GameObject ModeloPuertaVia(Vector3 a, Vector3 b, int estado)
        {
            Vector3 c = (a + b) * 0.5f; int k = EstacionDato.VariantOf(c.x, c.z, 5);
            string file = "SM_PuertaVia_v" + k + "_" + PuertaViaVar[k] + "_e" + estado + "_" + PuertaViaEst[estado] + "_LOD1";
            var prefab = Resources.Load<GameObject>("Models/Defensas/puerta_via/" + file);
            if (prefab == null) { Debug.LogWarning("[PUERTA-VIA] falta " + file); return null; }
            if (_puertaViaMat == null)
            {
                var tex = Resources.Load<Texture2D>("Models/Defensas/puerta_via/T_Paleta32");
                _puertaViaMat = Mat("ffffff"); if (tex != null) _puertaViaMat.SetTexture("_BaseMap", tex); _puertaViaMat.SetColor("_BaseColor", Color.white); _puertaViaMat.enableInstancing = true;
            }
            var go = Instantiate(prefab, transform); go.name = "Puerta de vía " + file;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = _puertaViaMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            // orientación y ancho: el eje horizontal más largo del modelo va a lo largo del hueco y se ajusta a su largo
            go.transform.position = c; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            Bounds bb = new Bounds(c, Vector3.zero); bool any = false;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) { if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds); }
            Vector3 d = b - a; d.y = 0f; float largo = d.magnitude; if (largo < 0.1f) largo = 3.2f;
            bool anchoX = bb.size.x >= bb.size.z; float anchoModelo = Mathf.Max(0.1f, anchoX ? bb.size.x : bb.size.z);
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - (anchoX ? 90f : 0f);
            float s = largo / anchoModelo; if (Mathf.Abs(s - 1f) < 0.15f) s = 1f;
            go.transform.localScale = anchoX ? new Vector3(s, 1f, 1f) : new Vector3(1f, 1f, s);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = new Vector3(c.x, 0f, c.z);
            Bounds b2 = new Bounds(c, Vector3.zero); bool any2 = false;                                          // el centro del modelo, sobre el centro del hueco
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) { if (!any2) { b2 = r.bounds; any2 = true; } else b2.Encapsulate(r.bounds); }
            if (any2) go.transform.position += new Vector3(c.x - b2.center.x, 0f, c.z - b2.center.z);
            return go;
        }

        /// <summary>Cada fotograma (solo vista): crea, cambia de estado y abre o cierra las puertas de vía construidas de la zona.</summary>
        void UpdatePuertasVia()
        {
            var gp = NetSession.A != null && _zoneNode >= 0 ? NetSession.A.Works.At(_zoneNode) : null;
            if (gp == null) return;
            float tx = TrainXEcs();
            for (int i = 0; i < gp.Count; i++)
            {
                var w = gp[i]; if (w.Kind != PieceKind.PuertaVia || !w.Built) { if (_puertasVia.TryGetValue(i, out var vx)) { if (vx.Go != null) Destroy(vx.Go); _puertasVia.Remove(i); } continue; }
                if (!_puertasVia.TryGetValue(i, out var v)) { v = new PuertaViaVista { Pieza = i, A = new Vector3(WorkOrigin.x + w.X0 / 1000f, 0f, WorkOrigin.y + w.Y0 / 1000f), B = new Vector3(WorkOrigin.x + w.X1 / 1000f, 0f, WorkOrigin.y + w.Y1 / 1000f) }; _puertasVia[i] = v; }
                int est = EstadoPuerta(w.LifeMil);
                if (est != v.Estado || v.Go == null)
                {
                    if (v.Go != null) Destroy(v.Go);
                    v.Go = ModeloPuertaVia(v.A, v.B, est); v.Estado = est;
                    if (v.Go != null) { if (i < _workVisuals.Count && _workVisuals[i] != null) _workVisuals[i].SetActive(false); Debug.Log($"[PUERTA-VIA] pieza {i} en ({(v.A.x + v.B.x) * 0.5f:0.0};{(v.A.z + v.B.z) * 0.5f:0.0}) estado {est}"); }
                }
                if (v.Go == null) continue;
                float gx = (v.A.x + v.B.x) * 0.5f;
                bool trenEnHueco = tx > 0f && gx >= tx - Level.TrainRear - 6f && gx <= tx + Level.TrainFront + 6f;
                bool abierta = !_nightOn && trenEnHueco;                                  // de noche siempre cerrada (la sim tiene el muro); de día, abierta al paso del tren
                if (v.Go.activeSelf == abierta) v.Go.SetActive(!abierta);
            }
        }
    }
}
