using System.Collections.Generic;
using TBD.Net;
using TBD.Sim;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Puerta de vehículos E4 visible (pulido de Lucía hasta el 19-nov, punto 2 de Teo; D-N198): el modelo de Teo Defensas/muro/SM_PuertaVehiculos
    /// (5 variantes por posición y 4 estados por la vida de la pieza) en lugar del sprite plano. Las dos hojas (hijos Hoja_O y Hoja_E, con el origen
    /// en su bisagra) giran 90° hacia DENTRO del fuerte (Lucía, 9-nov 26:05): abiertas de día (D-N198: la partida empieza con la E4 abierta y los
    /// vehículos salen por ella) y cerradas durante el asedio (D-N151). El sentido del giro se elige al montar la puerta: el que deja la punta de
    /// la hoja más cerca del centro del fuerte. Solo vista: no toca la sim (el sólido de la puerta lo pone SyncWorks).
    /// La vista sigue a la sim: la puerta se dibuja cerrada cuando la pieza tiene su muro en la zona (_gateWalled, que pone NightTick) y
    /// abierta cuando no. Cuando la D-N198 quite el muro de día (Nacho/Iker), la puerta se abre sola si esa pieza sale de _gateWalled.
    /// SIN COMPILAR NI VER EN CAPTURA (Teo, 11-nov).
    /// </summary>
    public partial class PrototypeBootstrap
    {
        static readonly string[] PuertaVehVar = { "TuboChapa", "ChapaOndulada", "Tablones", "MallaEspino", "Blindada" };
        class PuertaVehVista
        {
            public GameObject Go; public Transform HojaO, HojaE; public Quaternion RotO, RotE; public float SignoO = 1f, SignoE = -1f;
            public int Estado = -1; public Vector3 A, B; public float Apertura = -1f;    // 0 cerrada, 1 abierta
        }
        readonly Dictionary<int, PuertaVehVista> _puertasVeh = new Dictionary<int, PuertaVehVista>();
        Material _puertaVehMat;
        const float PuertaVehSegundos = 2.5f;                                         // lo que tarda en abrir o cerrar (vista)

        bool PuertaVehCerrada(int pieza) { return _gateWalled.Contains(pieza); }

        GameObject ModeloPuertaVeh(Vector3 a, Vector3 b, int estado)
        {
            Vector3 c = (a + b) * 0.5f; int k = EstacionDato.VariantOf(c.x, c.z, 5);
            string file = "SM_PuertaVehiculos_v" + k + "_" + PuertaVehVar[k] + "_e" + estado + "_" + PuertaViaEst[estado] + "_LOD1";
            var prefab = Resources.Load<GameObject>("Models/Defensas/muro/" + file);
            if (prefab == null) { Debug.LogWarning("[PUERTA-VEH] falta " + file); return null; }
            if (_puertaVehMat == null)
            {
                var tex = Resources.Load<Texture2D>("Models/Defensas/muro/T_Paleta32");
                _puertaVehMat = Mat("ffffff"); if (tex != null) _puertaVehMat.SetTexture("_BaseMap", tex); _puertaVehMat.SetColor("_BaseColor", Color.white); _puertaVehMat.enableInstancing = true;
            }
            var go = Instantiate(prefab, transform); go.name = "Puerta de vehículos " + file;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = _puertaVehMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            // el modelo tiene el ancho de la puerta (8 m entre hojas) a lo largo de su X y mira a +Z: X a lo largo del hueco, sin escalar
            Vector3 d = b - a; d.y = 0f;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - 90f;
            go.transform.SetPositionAndRotation(new Vector3(c.x, 0f, c.z), Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        static Transform Hijo(Transform t, string nombre)
        {
            if (t.name == nombre || t.name.StartsWith(nombre + ".")) return t;
            for (int i = 0; i < t.childCount; i++) { var f = Hijo(t.GetChild(i), nombre); if (f != null) return f; }
            return null;
        }

        /// <summary>Signo del giro (sobre la Y local de la hoja) que lleva la hoja hacia dentro del fuerte: se prueba +90° y se mira dónde queda.</summary>
        static float SignoHaciaDentro(Transform hoja, Quaternion cerrada, Vector2 centro)
        {
            var rs = hoja.GetComponentsInChildren<MeshRenderer>(); if (rs.Length == 0) return 1f;
            float Dist()
            {
                Bounds bb = rs[0].bounds; for (int i = 1; i < rs.Length; i++) bb.Encapsulate(rs[i].bounds);
                return Vector2.Distance(new Vector2(bb.center.x, bb.center.z), centro);
            }
            hoja.localRotation = cerrada * Quaternion.Euler(0f, 90f, 0f); float dMas = Dist();
            hoja.localRotation = cerrada * Quaternion.Euler(0f, -90f, 0f); float dMenos = Dist();
            hoja.localRotation = cerrada;
            return dMas <= dMenos ? 1f : -1f;
        }

        /// <summary>Cada fotograma (solo vista): crea, cambia de estado y abre o cierra las puertas de vehículos construidas de la zona.</summary>
        void UpdatePuertasVeh()
        {
            var gp = NetSession.A != null && _zoneNode >= 0 ? NetSession.A.Works.At(_zoneNode) : null;
            if (gp == null) return;
            var centro = new Vector2((Level.FortX0 + Level.FortX1) * 0.5f, (Level.FortY0 + Level.FortY1) * 0.5f);
            for (int i = 0; i < gp.Count; i++)
            {
                var w = gp[i]; bool cerrada = PuertaVehCerrada(i);
                if (w.Kind != PieceKind.PuertaVeh || !w.Built) { if (_puertasVeh.TryGetValue(i, out var vx)) { if (vx.Go != null) Destroy(vx.Go); _puertasVeh.Remove(i); } continue; }
                if (!_puertasVeh.TryGetValue(i, out var v)) { v = new PuertaVehVista { A = new Vector3(WorkOrigin.x + w.X0 / 1000f, 0f, WorkOrigin.y + w.Y0 / 1000f), B = new Vector3(WorkOrigin.x + w.X1 / 1000f, 0f, WorkOrigin.y + w.Y1 / 1000f) }; _puertasVeh[i] = v; }
                int est = EstadoPuerta(w.LifeMil);
                if (est != v.Estado || v.Go == null)
                {
                    if (v.Go != null) Destroy(v.Go);
                    v.Go = ModeloPuertaVeh(v.A, v.B, est); v.Estado = est;
                    if (v.Go == null) continue;
                    if (i < _workVisuals.Count && _workVisuals[i] != null) _workVisuals[i].SetActive(false);      // fuera el sprite plano
                    v.HojaO = Hijo(v.Go.transform, "Hoja_O"); v.HojaE = Hijo(v.Go.transform, "Hoja_E");
                    if (v.HojaO != null) { v.RotO = v.HojaO.localRotation; v.SignoO = SignoHaciaDentro(v.HojaO, v.RotO, centro); }
                    if (v.HojaE != null) { v.RotE = v.HojaE.localRotation; v.SignoE = SignoHaciaDentro(v.HojaE, v.RotE, centro); }
                    if (v.Apertura < 0f) v.Apertura = cerrada ? 0f : 1f;                                     // al entrar en la zona, ya en su sitio
                    Debug.Log($"[PUERTA-VEH] pieza {i} en ({(v.A.x + v.B.x) * 0.5f:0.0};{(v.A.z + v.B.z) * 0.5f:0.0}) estado {est} hojas {(v.HojaO != null ? "O" : "-")}{(v.HojaE != null ? "E" : "-")} {(cerrada ? "cerrada" : "abierta")}");
                }
                if (v.Go == null) continue;
                float meta = cerrada ? 0f : 1f;
                v.Apertura = Mathf.MoveTowards(v.Apertura, meta, Time.deltaTime / PuertaVehSegundos);
                float ang = 90f * Mathf.SmoothStep(0f, 1f, v.Apertura);
                if (v.HojaO != null) v.HojaO.localRotation = v.RotO * Quaternion.Euler(0f, v.SignoO * ang, 0f);
                if (v.HojaE != null) v.HojaE.localRotation = v.RotE * Quaternion.Euler(0f, v.SignoE * ang, 0f);
            }
        }
    }
}
