using System.Collections.Generic;
using TBD.Net;
using UnityEngine;
using UnityEngine.Rendering;

namespace TBD.View
{
    /// <summary>
    /// Carga visible de Teo sobre las plataformas Rgs (Models/CargaVagones; regla en CargaVisible). Uso (Marcos, en el tren de la zona): con las plataformas del
    /// tren en orden, var rep = CargaVisible.Reparto(servicio.Cargo, plataformas.Count); y para cada i, CargaVagonVista.Poner(plataformas[i], rep[i],
    /// rep[i] &lt; 0 ? 0 : servicio.Cargo[rep[i]], vidaPm). Cambia la malla solo si cambia el estado; sin recurso, quita el módulo.
    /// </summary>
    public static class CargaVagonVista
    {
        const string Hijo = "Carga";
        static Material _mat;

        public static void Poner(Transform vagon, int recurso, int u, int vidaPm = 1000, int capacidad = CargaVisible.CapacidadRefU)
        {
            if (vagon == null) return;
            string f = recurso < 0 ? null : CargaVisible.Archivo(recurso, CargaVisible.Estado(u, capacidad, vidaPm));
            var viejo = vagon.Find(Hijo);
            if (viejo != null && viejo.childCount > 0 && viejo.GetChild(0).name == f) return;
            if (viejo != null) Kill(viejo.gameObject);
            if (f == null) return;
            var prefab = Resources.Load<GameObject>("Models/CargaVagones/" + f + "_LOD1");
            if (prefab == null) { Debug.LogWarning("[CARGA] falta Models/CargaVagones/" + f + "_LOD1"); return; }
            var h = new GameObject(Hijo).transform; h.SetParent(vagon, false); h.localPosition = new Vector3(0f, 1.25f, 0f); h.localRotation = Quaternion.identity;
            var inst = Object.Instantiate(prefab, h); inst.name = f;
            Pintar(inst, "Models/CargaVagones/T_Paleta32", ref _mat);
        }

        internal static void Pintar(GameObject inst, string paleta, ref Material mat)
        {
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.SetFloat("_Smoothness", 0f); mat.enableInstancing = true;
                var tex = Resources.Load<Texture2D>(paleta); if (tex != null) mat.SetTexture("_BaseMap", tex);
            }
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.On; }
        }

        internal static void Kill(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
    }

    /// <summary>
    /// Acopios de una estación (regla en AcopioViveres). Víveres de Nules: Models/Nules/acopio_viveres, Montar(raíz de la zona, nules_instancias.csv) con las 5 pilas
    /// del grupo «viveres». Carga (Teo, nucleo-zonas a2144e5): Models/Acopios, MontarCarga(raíz, recurso, sitios) con «Municion», «Combustible» o «Materiales».
    /// Actualizar(unidades, caída, quemada) cambia la malla de todas las pilas a la vez cuando cambia el estado. Si falta el modelo, no se ve nada y sale [ACOPIO].
    /// </summary>
    public sealed class AcopioVista
    {
        readonly List<Transform> _pilas = new List<Transform>();
        int _estado = -1;
        string _recurso = "Viveres", _carpeta = "Models/Nules/acopio_viveres";
        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
        public const string CarpetaCarga = "Models/Acopios";

        public static AcopioVista Montar(Transform root, string csv, int viveres = 0)
        {
            return MontarEn(root, "Acopio de víveres", "Viveres", "Models/Nules/acopio_viveres", AcopioViveres.Pilas(csv), viveres);
        }

        /// <summary>Acopio de carga: sitios = x, y, z y yaw locales de root (el origen es el de AcopioViveres: centro de la huella de 10 × 6 m en el suelo).</summary>
        public static AcopioVista MontarCarga(Transform root, string recurso, IEnumerable<float[]> sitios, int unidades)
        {
            return MontarEn(root, "Acopio de " + recurso, recurso, CarpetaCarga, sitios, unidades);
        }

        static AcopioVista MontarEn(Transform root, string nombre, string recurso, string carpeta, IEnumerable<float[]> sitios, int unidades)
        {
            var a = new AcopioVista { _recurso = recurso, _carpeta = carpeta }; var g = new GameObject(nombre).transform; g.SetParent(root, false);
            foreach (var p in sitios)
            {
                var t = new GameObject("Pila").transform; t.SetParent(g, false); t.localPosition = new Vector3(p[0], p[1], p[2]); t.localRotation = Quaternion.Euler(0f, p[3], 0f);
                a._pilas.Add(t);
            }
            a.Actualizar(unidades);
            return a;
        }

        public void Actualizar(int unidades, bool caida = false, bool quemada = false)
        {
            int e = AcopioViveres.Estado(unidades, caida, quemada); if (e == _estado) return; _estado = e;
            string f = AcopioViveres.Archivo(_recurso, e);
            var prefab = Resources.Load<GameObject>(_carpeta + "/" + f + "_LOD1");
            _mats.TryGetValue(_carpeta, out var mat);
            foreach (var t in _pilas)
            {
                for (int i = t.childCount - 1; i >= 0; i--) CargaVagonVista.Kill(t.GetChild(i).gameObject);
                if (prefab == null) continue;
                var inst = Object.Instantiate(prefab, t); inst.name = f;
                CargaVagonVista.Pintar(inst, _carpeta + "/T_Paleta32", ref mat);
            }
            _mats[_carpeta] = mat;
            if (prefab == null) Debug.LogWarning("[ACOPIO] falta " + _carpeta + "/" + f);
        }
    }
}
