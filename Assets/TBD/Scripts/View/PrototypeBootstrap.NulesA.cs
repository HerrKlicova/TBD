using UnityEngine;

namespace TBD.View
{
    /// <summary>Nules, opción A (Lucía, d508444): la zona usa el terreno de Sagunto; la fachada lleva el cartel «NULES» en lugar de la placa de Sagunto. Solo vista.</summary>
    public partial class PrototypeBootstrap
    {
        void PlacaTexto(string texto, Vector3 centro, Vector2 tam, Transform root)
        {
            var placa = GameObject.CreatePrimitive(PrimitiveType.Quad); placa.name = "Placa " + texto; Destroy(placa.GetComponent<Collider>());
            placa.transform.SetParent(root, false); placa.transform.position = centro; placa.transform.rotation = Quaternion.identity; placa.transform.localScale = new Vector3(tam.x, tam.y, 1f);
            placa.GetComponent<MeshRenderer>().sharedMaterial = Mat("b8b8b8");
            var go = new GameObject("Texto " + texto); go.transform.SetParent(root, false);
            go.transform.position = centro + new Vector3(0f, 0f, -0.03f); go.transform.rotation = Quaternion.identity;
            var tm = go.AddComponent<TextMesh>(); tm.text = texto; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tm.fontSize = 64; tm.characterSize = tam.y * 0.11f; tm.color = new Color32(0x30, 0x34, 0x24, 0xff);
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            Debug.Log("[NULES] opción A: placa «" + texto + "» en la fachada; sin el pueblo de Sagunto");
        }
    }
}
