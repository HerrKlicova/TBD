using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Marca que un modelo es una unidad propia (propiedad de usuario tbd_rim = 1 del FBX de Teo): la presentación le pone el rim azul (#404460).
    /// La pone el AssetPostprocessor ModelUserProps al importar; también guarda la facción para quien la quiera leer.
    /// </summary>
    public class RimMarker : MonoBehaviour
    {
        public string Faccion = "";
        public int Rim = 1;
    }
}
