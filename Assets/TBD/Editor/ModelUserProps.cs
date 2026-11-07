using UnityEditor;
using UnityEngine;

namespace TBD.EditorTools
{
    /// <summary>
    /// Los FBX de Teo traen las propiedades de usuario tbd_faccion y tbd_rim (1 = propia). Unity las entrega en
    /// OnPostprocessGameObjectWithUserProperties (con «Import Custom Properties» activado en el importador de modelos).
    /// Si tbd_rim vale 1 se añade un RimMarker al objeto; el resultado queda en Library para poder comprobar que Unity las recibe.
    /// </summary>
    public class ModelUserProps : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/TBD/Resources/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            if (assetPath.Contains("/Models/Suelo/")) mi.importNormals = ModelImporterNormals.Import;
            if (assetPath.Contains("/Models/Vehiculos/")) mi.importNormals = ModelImporterNormals.Import;   // vehiculos y helicopteros de Teo
            if (assetPath.Contains("/Models/Infanteria/")) { mi.importNormals = ModelImporterNormals.Import; mi.animationType = ModelImporterAnimationType.Generic; mi.importAnimation = true; }   // soldados de Teo: Generic (no Humanoid), 4 clips
            if (assetPath.Contains("/Models/Suelo/")) mi.isReadable = true;   // la capa C se combina por trozo en una sola malla: hace falta leer las mallas en la build   // matas y piedras de Teo: normales horneadas hacia arriba (D-N67)
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
        }

        void OnPostprocessGameObjectWithUserProperties(GameObject g, string[] names, object[] values)
        {
            if (!assetPath.StartsWith("Assets/TBD/Resources/Models/")) return;
            string faccion = ""; int rim = 0; var log = new System.Text.StringBuilder();
            for (int i = 0; i < names.Length; i++)
            {
                log.Append(names[i]).Append('=').Append(values[i]).Append(';');
                if (names[i] == "tbd_faccion") faccion = values[i] != null ? values[i].ToString() : "";
                if (names[i] == "tbd_rim") { int.TryParse(values[i] != null ? values[i].ToString() : "0", out rim); }
            }
            System.IO.Directory.CreateDirectory("Library/TBD");
            System.IO.File.AppendAllText("Library/TBD/userprops.log", assetPath + " | " + g.name + " | " + log + "\n");
            if (rim == 1)
            {
                var m = g.GetComponent<TBD.View.RimMarker>() ?? g.AddComponent<TBD.View.RimMarker>();
                m.Rim = 1; m.Faccion = faccion;
            }
        }
    }
}
