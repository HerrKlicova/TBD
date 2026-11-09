using System.Collections.Generic;
using TBD.Net;
using UnityEngine;
using UnityEngine.Rendering;

namespace TBD.View
{
    /// <summary>
    /// Monta la zona mínima de Bétera a partir de AerodromoNivel (betera.json de Álvaro, v0.2) con las piezas de Teo r2 de Resources/Models/Betera. Encuadre de
    /// reserva de 256 × 128 m; lo que cae fuera (la pista, el polvorín, la mayor parte de la valla) va de faldón. Solo aspecto: no toca la simulación.
    /// Pista y plataforma en hormigón por primitivas; los claros P1–P3 y C1–C3, marcados en el suelo; la guarnición, en su sitio (sitio_cuartel).
    /// Sin asedios en el MVP. Lo que no tenga modelo sale como caja gris con su nombre (para que se vea qué falta).
    /// </summary>
    public static class AerodromoZona
    {
        public sealed class Result { public Transform Root; public int Placed, Faldon; public readonly List<string> Missing = new List<string>(); public readonly Dictionary<string, Vector3> Claros = new Dictionary<string, Vector3>();
            public readonly List<GameObject> P1Reposo = new List<GameObject>(), P1Rearme = new List<GameObject>(); public bool Rearmando; }
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static Material _ground, _concrete, _apron, _mark, _missing;

        public static Result Build(Transform root, AerodromoNivel n)
        {
            var res = new Result { Root = root };
            Quad(root, "Suelo", (n.ZonaX0 + n.ZonaX1) * 0.5f, (n.ZonaZ0 + n.ZonaZ1) * 0.5f, n.ZonaX1 - n.ZonaX0 + 160f, n.ZonaZ1 - n.ZonaZ0 + 160f, 0f, _ground ??= Flat("8a7a5c"));
            Rect(root, "Pista 09/27", n.Pista, 0.02f, _concrete ??= Flat("6c7068"));
            Rect(root, "Plataforma", n.Plataforma, 0.03f, _apron ??= Flat("7c8078"));
            foreach (var r in n.Rodaduras) { float cx = (r[0] + r[2]) * 0.5f, cz = (r[1] + r[3]) * 0.5f, len = Vector2.Distance(new Vector2(r[0], r[1]), new Vector2(r[2], r[3])); Quad(root, "Rodadura", cx, cz, 8f, len, 0.025f, _concrete); }
            var claros = new GameObject("Claros").transform; claros.SetParent(root, false);
            foreach (var kv in n.Claros)
            {
                float x = kv.Value[0], z = kv.Value[1], l = kv.Value[2];
                Marco(claros, kv.Key, x, z, l, _mark ??= Flat("b8b8b8"));      // contorno en gris claro (nada de rojo ni de d4d0cc)
                res.Claros[kv.Key] = new Vector3(x, 0f, z);
            }
            var piezas = new GameObject("Piezas").transform; piezas.SetParent(root, false);
            foreach (var c in n.Piezas)
            {
                // la valla, con el kit de defensas de Teo por vida (valla_betera: e0 sana … e4 brecha); la variante 1 de la valla vieja era «sobre muro bajo»
                var go = c.Archivo != null ? ModelEn(c.Ruta, c.Archivo + "_LOD1", piezas)
                       : c.Carpeta == "valla_base" ? ModelEn("Defensas/valla_betera", RecintoPlan.VallaBetera(VidaValla, c.Variante == 1) + "_LOD1", piezas) ?? Model(c.Carpeta, c.Malla + "_v" + c.Variante + "_LOD1", piezas)
                                                   : Model(c.Carpeta, c.Malla + "_v" + c.Variante + "_LOD1", piezas);
                if (go == null) { go = Placeholder(piezas, c.Pieza); res.Missing.Add(c.Pieza + (c.Archivo != null ? " [" + c.Ruta + "/" + c.Archivo + "]" : "") + " (" + c.X + ", " + c.Z + ")"); }
                go.transform.localPosition = new Vector3(c.X, 0f, c.Z);
                go.transform.localRotation = Quaternion.Euler(0f, c.Yaw, 0f) * go.transform.localRotation;
                if (c.EscalaX != 0f && c.EscalaX != 1f) { var sc = go.transform.localScale; go.transform.localScale = new Vector3(sc.x * c.EscalaX, sc.y, sc.z); }
                res.Placed++; if (c.Faldon) res.Faldon++;
            }
            // rearme de Teo: en P1, las dos mallas (reposo y rearme) y se enciende una u otra; en P2 y P3, la del sitio
            var rearme = new GameObject("Rearme").transform; rearme.SetParent(root, false);
            foreach (var st in RearmeBetera.Sitios)
            {
                var a = Vehiculo(rearme, st, st.Variante, res);
                if (!st.EnP1) continue;
                res.P1Reposo.Add(a);
                var b = Vehiculo(rearme, st, st.VarianteRearme, res); b.SetActive(false); res.P1Rearme.Add(b);
            }
            var g = new GameObject("Guarnición de Bétera (sitio)"); g.transform.SetParent(root, false); g.transform.localPosition = new Vector3(n.CuartelX, 0f, n.CuartelZ);
            return res;
        }

        static GameObject Vehiculo(Transform parent, RearmeBetera.Sitio st, int v, Result res)
        {
            string f = RearmeBetera.Archivo(st.EsCamion, v);
            var go = ModelEn("Betera/rearme", f + "_LOD1", parent);
            if (go == null) { go = Placeholder(parent, f); res.Missing.Add(f + " (" + st.X + ", " + st.Z + ")"); }
            go.transform.localPosition = new Vector3(st.X, 0f, st.Z); go.transform.localRotation = Quaternion.Euler(0f, st.Yaw, 0f) * go.transform.localRotation;
            return go;
        }

        /// <summary>Cada fotograma (o al cambiar): con un NH90 rearmando, Repostando y Descargando en P1; en reposo, Aparcado y Cargado (mejora de Lucía).</summary>
        public static void Actualizar(Result res, AerodromoEstado a) { SetRearme(res, a != null && a.AlgunoRearmando); }

        public static void SetRearme(Result res, bool rearmando)
        {
            if (res == null || res.Rearmando == rearmando) return;
            res.Rearmando = rearmando;
            foreach (var g in res.P1Reposo) if (g != null) g.SetActive(!rearmando);
            foreach (var g in res.P1Rearme) if (g != null) g.SetActive(rearmando);
            // sonidos del FARP (Nuria, aprobados por Lucía): variante por semilla del sitio; todo se calla al pausar (AudioListener.pause)
            if (res.P1Rearme.Count < 2 || res.P1Rearme[0] == null) return;
            var cam = res.P1Rearme[0].transform.position; var rem = res.P1Rearme[1] != null ? res.P1Rearme[1].transform.position : cam;
            int v = 1 + (int)(((uint)(cam.x * 73856093f) ^ (uint)(cam.z * 19349663f)) % 5u);
            if (rearmando) { Sfx.LoopAt("farp_bomba", "sfx_farp_bomba_" + v, cam, 1f, 50f); Sfx.PlayAt("farp_manguera", cam, 1f); Sfx.PlayAt("farp_caja_municion", rem, 1f); }
            else Sfx.StopLoop("farp_bomba");
        }

        public static int VidaValla = 5000;      // HP de cada tramo de valla (5.000 = sana); sin asedios en el MVP, siempre sana

        static GameObject Model(string carpeta, string file, Transform parent) { return ModelEn("Betera/" + carpeta, file, parent); }

        static GameObject ModelEn(string folder, string file, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Models/" + folder + "/" + file);
            if (prefab == null) return null;
            var inst = Object.Instantiate(prefab, parent); inst.name = file;
            if (!Mats.TryGetValue(folder, out var mat) || mat == null)
            {
                var tex = Resources.Load<Texture2D>("Models/" + folder + "/T_Paleta32");
                mat = Flat("ffffff"); if (tex != null) mat.SetTexture("_BaseMap", tex); mat.enableInstancing = true; Mats[folder] = mat;
            }
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.On; }
            return inst;
        }

        static void Rect(Transform p, string name, float[] r, float y, Material m) { Quad(p, name, (r[0] + r[2]) * 0.5f, (r[1] + r[3]) * 0.5f, Mathf.Abs(r[2] - r[0]), Mathf.Abs(r[3] - r[1]), y, m); }

        static void Quad(Transform p, string name, float cx, float cz, float w, float d, float y, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = name; Kill(q.GetComponent<Collider>());
            q.transform.SetParent(p, false); q.transform.localPosition = new Vector3(cx, y, cz); q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); q.transform.localScale = new Vector3(w, d, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        static void Marco(Transform p, string name, float x, float z, float l, Material m)
        {
            var g = new GameObject("Claro " + name).transform; g.SetParent(p, false); g.localPosition = new Vector3(x, 0f, z);
            float h = l * 0.5f, w = 0.4f;
            Quad(g, "lado", 0f, h, l, w, 0.04f, m); Quad(g, "lado", 0f, -h, l, w, 0.04f, m); Quad(g, "lado", h, 0f, w, l, 0.04f, m); Quad(g, "lado", -h, 0f, w, l, 0.04f, m);
        }

        static GameObject Placeholder(Transform parent, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "FALTA " + name; Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localScale = new Vector3(4f, 3f, 4f);
            go.GetComponent<MeshRenderer>().sharedMaterial = _missing ??= Flat("9a9a9a");
            return go;
        }

        static void Kill(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
        static Material Flat(string hex) { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); ColorUtility.TryParseHtmlString("#" + hex, out var c); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0f); return m; }
    }
}
