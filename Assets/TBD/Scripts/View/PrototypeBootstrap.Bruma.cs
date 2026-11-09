using UnityEngine;
using TBD.Sim;

namespace TBD.View
{
    /// <summary>
    /// D-N182 en la zona (Lucía, 9-nov; opción A de Marcos): ninguna horda nace a la vista. La zona mide 256 × 128 m y las entradas (O1–O4) están en sus bordes,
    /// a unos 62 m del muro, así que la horda sigue naciendo donde nacía y a la misma hora (la llegada al muro no cambia), pero:
    ///  1) una franja de oscuridad de BrumaAnchoM en cada borde de la zona tapa el sitio donde nacen: se las ve salir andando de lo oscuro;
    ///  2) de noche la cámara no se aleja más de OrthoNoche, para que no se pueda mirar por encima de la franja.
    /// Solo vista: no toca la simulación ni el hash. -sinbruma la quita (QA).
    /// </summary>
    public partial class PrototypeBootstrap
    {
        public const float BrumaAnchoM = 28f, BrumaAltoM = 4f, OrthoNoche = 35f;
        static readonly bool BrumaOn = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sinbruma") < 0;
        float OrthoMax => _nightOn && BrumaOn ? OrthoNoche : 70f;

        void BuildBrumaBorde()
        {
            if (!BrumaOn) return;
            const int n = 64;
            var tex = new Texture2D(n, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int i = 0; i < n; i++) { float t = i / (float)(n - 1); float a = Mathf.SmoothStep(1f, 0f, t); tex.SetPixel(i, 0, new Color(0.02f, 0.025f, 0.04f, a)); }   // opaco en el borde, transparente hacia dentro
            tex.Apply();
            var sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh) { mainTexture = tex, renderQueue = 2990 };
            var root = new GameObject("Bruma del borde (D-N182)").transform; root.SetParent(transform, false);
            float W = SimConst.WorldW, H = SimConst.WorldH, extra = 60f;
            // oeste, este, sur y norte: el lado opaco del degradado queda siempre en el borde de la zona (y un poco más allá, para tapar el horizonte)
            Franja(root, mat, new Vector3(BrumaAnchoM * 0.5f - extra * 0.5f, BrumaAltoM, H * 0.5f), new Vector2(BrumaAnchoM + extra, H + 2f * extra), 0f, extra / (BrumaAnchoM + extra));
            Franja(root, mat, new Vector3(W - BrumaAnchoM * 0.5f + extra * 0.5f, BrumaAltoM, H * 0.5f), new Vector2(BrumaAnchoM + extra, H + 2f * extra), 180f, extra / (BrumaAnchoM + extra));
            Franja(root, mat, new Vector3(W * 0.5f, BrumaAltoM, BrumaAnchoM * 0.5f - extra * 0.5f), new Vector2(BrumaAnchoM + extra, W + 2f * extra), 90f, extra / (BrumaAnchoM + extra));
            Franja(root, mat, new Vector3(W * 0.5f, BrumaAltoM, H - BrumaAnchoM * 0.5f + extra * 0.5f), new Vector2(BrumaAnchoM + extra, W + 2f * extra), 270f, extra / (BrumaAnchoM + extra));
            Debug.Log($"[BRUMA] D-N182: franja de {BrumaAnchoM} m en los bordes de la zona y zoom de noche hasta {OrthoNoche}");
        }

        /// <summary>Quad horizontal a «alto» m. El degradado va a lo largo de su eje local X: u = 0 (opaco) en el borde exterior. «opacoHasta» = parte del quad que queda opaca del todo (lo que sobresale de la zona).</summary>
        static void Franja(Transform root, Material baseMat, Vector3 centro, Vector2 tam, float yaw, float opacoHasta)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Franja de bruma"; Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(root, false); q.transform.position = centro; q.transform.rotation = Quaternion.Euler(90f, yaw, 0f); q.transform.localScale = new Vector3(tam.x, tam.y, 1f);
            var m = new Material(baseMat); m.mainTextureScale = new Vector2(1f / Mathf.Max(0.01f, 1f - opacoHasta), 1f); m.mainTextureOffset = new Vector2(-opacoHasta / Mathf.Max(0.01f, 1f - opacoHasta), 0f);
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
    }
}
