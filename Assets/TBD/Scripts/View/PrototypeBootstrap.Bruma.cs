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
        GameObject _brumaRoot;     // Bruno 11-11 (Eugenio, I-Noche151: «bandas negras» de día en Nules): la franja solo se ve de noche, que es cuando nace la horda tapada

        void BuildBrumaBorde()
        {
            if (!BrumaOn) return;
            // Lucía (a073ff8): degradado suave en los 28 m (0 % dentro → 80 % en el borde), sin negro: bruma del suelo de noche oscurecido (L* ≈ 9, croma ≤ 5),
            // y el borde de dentro irregular (±4 m de ruido a lo largo de la franja). u = 0 es el borde exterior; v recorre la franja.
            const int n = 64, m = 128; const float maxA = 0.8f, ruidoM = 4f;
            var tex = new Texture2D(n, m, TextureFormat.RGBA32, false) { wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var col = new Color(0.098f, 0.098f, 0.086f);      // sRGB ≈ L* 9, croma ≈ 2
            for (int j = 0; j < m; j++)
            {
                float v = j / (float)m; float ruido = (Mathf.PerlinNoise(v * 9f, 3.7f) - 0.5f) * 2f * 0.6f + (Mathf.PerlinNoise(v * 23f, 8.1f) - 0.5f) * 2f * 0.4f;
                float dentro = 1f - (Mathf.Clamp(ruido, -1f, 1f) + 1f) * ruidoM / BrumaAnchoM;      // borde de dentro entre 20 y 28 m (24 ± 4), nunca más allá del quad: sin línea
                for (int i = 0; i < n; i++) { float t = i / (float)(n - 1); float x = Mathf.Clamp01(t / dentro); float a = maxA * (1f - x * x * (3f - 2f * x)); tex.SetPixel(i, j, new Color(col.r, col.g, col.b, a)); }
            }
            tex.Apply();
            var sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh) { mainTexture = tex, renderQueue = 2990 };
            var root = new GameObject("Bruma del borde (D-N182)").transform; root.SetParent(transform, false);
            _brumaRoot = root.gameObject; _brumaRoot.SetActive(_nightOn);
            float W = SimConst.WorldW, H = SimConst.WorldH, extra = 60f;
            // oeste, este, sur y norte: el lado opaco del degradado queda siempre en el borde de la zona (y un poco más allá, para tapar el horizonte)
            Franja(root, mat, new Vector3(BrumaAnchoM * 0.5f - extra * 0.5f, BrumaAltoM, H * 0.5f), new Vector2(BrumaAnchoM + extra, H + 2f * extra), 0f, extra / (BrumaAnchoM + extra));
            Franja(root, mat, new Vector3(W - BrumaAnchoM * 0.5f + extra * 0.5f, BrumaAltoM, H * 0.5f), new Vector2(BrumaAnchoM + extra, H + 2f * extra), 180f, extra / (BrumaAnchoM + extra));
            Franja(root, mat, new Vector3(W * 0.5f, BrumaAltoM, BrumaAnchoM * 0.5f - extra * 0.5f), new Vector2(BrumaAnchoM + extra, W + 2f * extra), 90f, extra / (BrumaAnchoM + extra));
            Franja(root, mat, new Vector3(W * 0.5f, BrumaAltoM, H - BrumaAnchoM * 0.5f + extra * 0.5f), new Vector2(BrumaAnchoM + extra, W + 2f * extra), 270f, extra / (BrumaAnchoM + extra));
            Debug.Log($"[BRUMA] D-N182: franja de {BrumaAnchoM} m en los bordes de la zona y zoom de noche hasta {OrthoNoche}");
        }

        /// <summary>Cada fotograma: la franja se enciende con la noche y se apaga de día (de día era un quad oscuro al 80 % a 4 m que se leía como bandas negras
        /// a los lados de la zona, cortando el paso elevado y el terraplén por la mitad).</summary>
        void UpdateBrumaBorde() { if (_brumaRoot != null && _brumaRoot.activeSelf != _nightOn) _brumaRoot.SetActive(_nightOn); }

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
