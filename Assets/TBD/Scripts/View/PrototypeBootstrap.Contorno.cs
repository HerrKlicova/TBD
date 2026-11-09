using UnityEngine;
using UnityEngine.Rendering;

namespace TBD.View
{
    /// <summary>
    /// Contorno oscuro de 1 px alrededor del fantasma de construcción y del anillo de selección (Lucía, 9-nov; Sara lo mide con qa47). Bruno. SIN COMPILAR.
    /// Color: slate oscuro 283440 de la paleta (L* 21). ΔL* 63 contra la cal de día d4d0cc y 46 contra la arena b4a080; 10 contra la horda 1c200c.
    /// - Fantasma de obra en marcha (malla en el mundo): contorno en la RT del mundo, exacto a 1 px del arte (shader TBD/FantasmaContorno, ver su cabecera).
    /// - Fantasma del modo de construcción y anillos (IMGUI, a resolución de pantalla): una pasada oscura debajo, más ancha en 1 px del arte por cada lado
    ///   (1 px del arte = Screen.height / altura de la RT del mundo; 3 px de pantalla en el escalón 1/3). OJO: el IMGUI no va pegado a la rejilla del pixelado,
    ///   así que ahí el contorno mide 1 px del arte pero no cae justo en sus píxeles.
    /// -sincontorno lo apaga todo (para comparar en captura).
    /// </summary>
    public partial class PrototypeBootstrap
    {
        static readonly Color Contorno = new Color32(0x28, 0x34, 0x40, 255);
        static readonly bool ContornoOn = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sincontorno") < 0;
        Material _mFantasmaMascara, _mFantasmaContorno;

        /// <summary>Ancho de 1 px del arte en píxeles de pantalla.</summary>
        float PxArte() { return _lowRt != null && _lowRt.height > 0 ? Mathf.Max(1f, (float)Screen.height / _lowRt.height) : Mathf.Max(1f, Screen.height / 360f); }

        /// <summary>Pone al MeshRenderer del fantasma la máscara y el contorno detrás de su material (más materiales que submallas: se repite la malla).</summary>
        void ContornoFantasma(MeshRenderer r)
        {
            if (!ContornoOn || r == null) return;
            if (_mFantasmaMascara == null)
            {
                var sh = Shader.Find("TBD/FantasmaContorno");
                if (sh == null) return;                                                       // sin el shader, el fantasma queda como antes
                _mFantasmaMascara = new Material(sh) { name = "Fantasma máscara", renderQueue = 2008 };
                _mFantasmaMascara.SetFloat("_Inflar", 0f);
                _mFantasmaMascara.SetFloat("_StencilComp", (float)CompareFunction.Always);
                _mFantasmaMascara.SetFloat("_StencilOp", (float)StencilOp.Replace);
                _mFantasmaMascara.SetFloat("_ColorMask", 0f);
                _mFantasmaContorno = new Material(sh) { name = "Fantasma contorno", renderQueue = 2009 };
                _mFantasmaContorno.SetColor("_BaseColor", HexColor("283440"));
                _mFantasmaContorno.SetFloat("_Inflar", 1f);
                _mFantasmaContorno.SetFloat("_StencilComp", (float)CompareFunction.NotEqual);
                _mFantasmaContorno.SetFloat("_StencilOp", (float)StencilOp.Keep);
                _mFantasmaContorno.SetFloat("_ColorMask", 15f);
            }
            var m = r.sharedMaterial;
            if (m != null && m.renderQueue < 2010) m.renderQueue = 2010;                     // el tramado, siempre después del contorno
            r.sharedMaterials = new[] { m, _mFantasmaMascara, _mFantasmaContorno };
        }

        /// <summary>Línea con contorno: la pasada oscura (1 px del arte más ancha por cada lado y por los extremos) y luego la del color.</summary>
        void OvLineC(Vector2 a, Vector2 b, float w, Color c, float al)
        {
            if (ContornoOn)
            {
                float k = PxArte(); var d = b - a; float len = d.magnitude;
                var e = len > 0.5f ? d / len * k : Vector2.zero;
                OvLine(a - e, b + e, w + 2f * k, Contorno, al);
            }
            OvLine(a, b, w, c, al);
        }

        /// <summary>Anillo con contorno: primero todo el anillo oscuro y después el de color, para que el oscuro no tape tramos ya pintados.</summary>
        void OvRingC(Vector3 c, float r, float w, Color col, float al)
        {
            if (ContornoOn) OvRing(c, r, w + 2f * PxArte(), Contorno, al);
            OvRing(c, r, w, col, al);
        }

        /// <summary>Punto con contorno (los soldados del pelotón elegido).</summary>
        void OvDotC(Vector2 p, float s, Color c, float a)
        {
            if (ContornoOn) OvDot(p, s + 2f * PxArte(), Contorno, a);
            OvDot(p, s, c, a);
        }

        /// <summary>Contorno oscuro por fuera de un recuadro (fantasma de pieza puntual en el modo de construcción).</summary>
        void FillRectContorno(Rect r)
        {
            if (!ContornoOn) return;
            float k = PxArte();
            FillRect(new Rect(r.x - k, r.y - k, r.width + 2 * k, k), Contorno); FillRect(new Rect(r.x - k, r.yMax, r.width + 2 * k, k), Contorno);
            FillRect(new Rect(r.x - k, r.y, k, r.height), Contorno); FillRect(new Rect(r.xMax, r.y, k, r.height), Contorno);
        }
    }
}
