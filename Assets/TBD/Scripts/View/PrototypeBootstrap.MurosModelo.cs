using System.Collections.Generic;
using TBD.Net;
using TBD.Sim;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Eugenio (I-Noche110): «se hicieron los sacos, los muros… y aquí siguen unos rectángulos planos». Los tramos de muro de las obras (sacos y HESCO) se
    /// dibujan con los modelos del kit de defensas de Teo (Models/Defensas/muro: SM_MuroSacos 8 m y SM_MuroHesco 10 m, 5 variantes por posición, como
    /// RecintoPlan), mirando hacia fuera del recinto. Solo vista: el sólido de la simulación sigue siendo el mismo BuildWall. Sin modelo, la caja de siempre.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        static readonly string[] _sacosVar = { "Recto", "Talud", "Irregular", "Tablon", "Red" }, _hescoVar = { "MallaLimpia", "Geotextil", "Escalonado", "RemateSacos", "Red" };
        static Material _matDefensas;
        static readonly Dictionary<string, GameObject> _defCache = new Dictionary<string, GameObject>();

        static GameObject PrefabDefensa(string file)
        {
            if (_defCache.TryGetValue(file, out var p)) return p;
            p = Resources.Load<GameObject>("Models/Defensas/muro/" + file); _defCache[file] = p; return p;
        }

        /// <summary>Tramo a-b con los modelos de muro; null si no hay modelo (el que llama pone la caja).</summary>
        GameObject MuroConModelo(Vector3 a, Vector3 b, PieceKind kind, int estado = 0)
        {
            bool hesco = kind == PieceKind.Hesco; string pieza = hesco ? "MuroHesco" : "MuroSacos"; float paso = hesco ? RecintoPlan.HescoTramo : RecintoPlan.SacosTramo;
            var d = new Vector3(b.x - a.x, 0f, b.z - a.z); float len = d.magnitude; if (len < 0.3f) return null;
            var u = d / len;
            // fuera = lejos del centro del recinto (de la zona en la que estamos; el terreno de Sagunto también en Nules A)
            var centro = new Vector2((Level.FortX0 + Level.FortX1) * 0.5f, (Level.FortY0 + Level.FortY1) * 0.5f);
            var n = new Vector2(u.z, -u.x); var mid = new Vector2((a.x + b.x) * 0.5f, (a.z + b.z) * 0.5f); if (Vector2.Dot(n, mid - centro) < 0f) n = -n;
            float yaw = RecintoPlan.YawAfuera(n.x, n.y);
            if (PrefabDefensa(Nombre(pieza, hesco, 0, estado)) == null) return null;
            if (_matDefensas == null) { var tex = Resources.Load<Texture2D>("Models/Defensas/muro/T_Paleta32"); _matDefensas = new Material(Shader.Find("Universal Render Pipeline/Lit")); if (tex != null) _matDefensas.SetTexture("_BaseMap", tex); _matDefensas.enableInstancing = true; }
            var root = new GameObject(hesco ? "Muro HESCO" : "Muro de sacos");
            int piezas = Mathf.Max(1, Mathf.RoundToInt(len / paso)); float l = len / piezas;      // tramos iguales, escalados en X para cubrir el lado entero
            for (int i = 0; i < piezas; i++)
            {
                var c = new Vector3(a.x, 0f, a.z) + u * (l * (i + 0.5f));
                int v = EstacionDato.VariantOf(c.x, c.z, 5);
                var pf = PrefabDefensa(Nombre(pieza, hesco, v, estado)) ?? PrefabDefensa(Nombre(pieza, hesco, 0, estado)); if (pf == null) continue;
                var go = Instantiate(pf, root.transform); go.transform.position = c; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f); go.transform.localScale = new Vector3(l / paso, 1f, 1f);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = _matDefensas; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
                foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
            }
            return root;
        }

        static string Nombre(string pieza, bool hesco, int v, int estado)
        {
            string[] est = { "e0_Sano", "e1_Danado", "e2_MuyDanado", "e3_Brecha" };
            return "SM_" + pieza + "_v" + (v % 5) + "_" + (hesco ? _hescoVar[v % 5] : _sacosVar[v % 5]) + "_" + est[Mathf.Clamp(estado, 0, 3)] + "_LOD1";
        }
    }
}
