using System.Collections.Generic;
using TBD.Net;
using UnityEngine;
using UnityEngine.Rendering;

namespace TBD.View
{
    /// <summary>
    /// Zona propia de Nules (Bruno 09-11; decisión del coordinador: detrás de la F0 de Bétera). Monta nules_instancias.csv de Álvaro (263 instancias) con las
    /// piezas de Teo: cada prefab del CSV va a sus ficheros de Resources/Models (tabla de abajo, sacada del repositorio; estado e0 cuando la pieza tiene estados).
    /// Variante −1 → por hash de la posición (EstacionDato.VariantOf). Lo que no esté en Resources sale como caja gris «FALTA …» y va al log [NULES]:
    /// los coches civiles (Urbano/coches_civiles) y la cooperativa (Nules/cooperativa) están en mr-mak (entregas/teo-modelado) y hay que importarlos desde el PC.
    /// Solo vista: la sim de la zona (muros, entradas de la horda, andén) es de Marcos. El terreno, de Álvaro (de momento un plano del tamaño de la zona).
    /// </summary>
    public static class NulesZona
    {
        public sealed class Result { public Transform Root; public int Placed, Faltan; public readonly List<string> Missing = new List<string>(); }

        // prefab del CSV → ficheros (ruta bajo Resources/Models, sin extensión), uno por variante
        static readonly Dictionary<string, string[]> Ficheros = new Dictionary<string, string[]>
        {
            { "SM_AcopioViveres", new[] { "Nules/acopio_viveres/SM_AcopioViveres_v0_Lleno_LOD1", "Nules/acopio_viveres/SM_AcopioViveres_v1_Mitad_LOD1", "Nules/acopio_viveres/SM_AcopioViveres_v2_Vacio_LOD1", "Nules/acopio_viveres/SM_AcopioViveres_v3_Saqueado_LOD1", "Nules/acopio_viveres/SM_AcopioViveres_v4_Quemado_LOD1" } },
            { "SM_Banco", new[] { "Nules/urbano/SM_Banco_v0_LOD1", "Nules/urbano/SM_Banco_v1_LOD1", "Nules/urbano/SM_Banco_v2_LOD1", "Nules/urbano/SM_Banco_v3_LOD1", "Nules/urbano/SM_Banco_v4_LOD1" } },
            { "SM_CocheCivil_Berlina", new[] { "Urbano/coches_civiles/SM_CocheCivil_Berlina_v0_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Berlina_v1_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Berlina_v2_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Berlina_v3_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Berlina_v4_LOD1" } },
            { "SM_CocheCivil_Furgoneta", new[] { "Urbano/coches_civiles/SM_CocheCivil_Furgoneta_v0_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Furgoneta_v1_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Furgoneta_v2_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Furgoneta_v3_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Furgoneta_v4_LOD1" } },
            { "SM_CocheCivil_Todoterreno", new[] { "Urbano/coches_civiles/SM_CocheCivil_Todoterreno_v0_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Todoterreno_v1_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Todoterreno_v2_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Todoterreno_v3_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Todoterreno_v4_LOD1" } },
            { "SM_CocheCivil_Utilitario", new[] { "Urbano/coches_civiles/SM_CocheCivil_Utilitario_v0_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Utilitario_v1_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Utilitario_v2_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Utilitario_v3_LOD1", "Urbano/coches_civiles/SM_CocheCivil_Utilitario_v4_LOD1" } },
            { "SM_Contenedor", new[] { "Urbano/SM_Contenedor_v0_Verde_LOD1", "Urbano/SM_Contenedor_v1_Abierto_LOD1", "Urbano/SM_Contenedor_v2_Reciclaje_LOD1", "Urbano/SM_Contenedor_v3_Volcado_LOD1", "Urbano/SM_Contenedor_v4_Fila_LOD1" } },
            { "SM_Cooperativa", new[] { "Nules/cooperativa/SM_Cooperativa_v0_Almacen_LOD1", "Nules/cooperativa/SM_Cooperativa_v1_AlmacenMuelle_LOD1", "Nules/cooperativa/SM_Cooperativa_v2_AlmacenAbierto_LOD1", "Nules/cooperativa/SM_Cooperativa_v3_AlmacenHundido_LOD1", "Nules/cooperativa/SM_Cooperativa_v4_NaveVilavella_LOD1" } },
            { "SM_EdificioViajerosNules", new[] { "Nules/edificio_viajeros/SM_EdificioViajerosNules_v0_Abandonada_LOD1", "Nules/edificio_viajeros/SM_EdificioViajerosNules_v1_Segura_LOD1", "Nules/edificio_viajeros/SM_EdificioViajerosNules_v2_EnLiberacion_LOD1", "Nules/edificio_viajeros/SM_EdificioViajerosNules_v3_Caida_LOD1", "Nules/edificio_viajeros/SM_EdificioViajerosNules_v4_Liberada_LOD1" } },
            { "SM_Farola", new[] { "Nules/urbano/SM_Farola_v0_LOD1", "Nules/urbano/SM_Farola_v1_LOD1", "Nules/urbano/SM_Farola_v2_LOD1", "Nules/urbano/SM_Farola_v3_LOD1", "Nules/urbano/SM_Farola_v4_LOD1" } },
            { "SM_HescoCorto2", new[] { "Defensas/hesco/SM_HescoCorto2_v0_MallaLimpia_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto2_v1_Geotextil_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto2_v2_Escalonado_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto2_v3_RemateSacos_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto2_v4_Red_e0_Sano_LOD1" } },
            { "SM_HescoCorto3", new[] { "Defensas/hesco/SM_HescoCorto3_v0_MallaLimpia_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto3_v1_Geotextil_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto3_v2_Escalonado_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto3_v3_RemateSacos_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto3_v4_Red_e0_Sano_LOD1" } },
            { "SM_HescoCorto4", new[] { "Defensas/hesco/SM_HescoCorto4_v0_MallaLimpia_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto4_v1_Geotextil_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto4_v2_Escalonado_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto4_v3_RemateSacos_e0_Sano_LOD1", "Defensas/hesco/SM_HescoCorto4_v4_Red_e0_Sano_LOD1" } },
            { "SM_HescoEsquina", new[] { "Defensas/hesco/SM_HescoEsquina_v0_MallaLimpia_e0_Sano_LOD1", "Defensas/hesco/SM_HescoEsquina_v1_Geotextil_e0_Sano_LOD1", "Defensas/hesco/SM_HescoEsquina_v2_Escalonado_e0_Sano_LOD1", "Defensas/hesco/SM_HescoEsquina_v3_RemateSacos_e0_Sano_LOD1", "Defensas/hesco/SM_HescoEsquina_v4_Red_e0_Sano_LOD1" } },
            { "SM_HescoRemate", new[] { "Defensas/hesco/SM_HescoRemate_v0_MallaLimpia_e0_Sano_LOD1", "Defensas/hesco/SM_HescoRemate_v1_Geotextil_e0_Sano_LOD1", "Defensas/hesco/SM_HescoRemate_v2_Escalonado_e0_Sano_LOD1", "Defensas/hesco/SM_HescoRemate_v3_RemateSacos_e0_Sano_LOD1", "Defensas/hesco/SM_HescoRemate_v4_Red_e0_Sano_LOD1" } },
            { "SM_HescoRemateEspejo", new[] { "Defensas/hesco/SM_HescoRemateEspejo_v0_MallaLimpia_e0_Sano_LOD1", "Defensas/hesco/SM_HescoRemateEspejo_v1_Geotextil_e0_Sano_LOD1", "Defensas/hesco/SM_HescoRemateEspejo_v2_Escalonado_e0_Sano_LOD1", "Defensas/hesco/SM_HescoRemateEspejo_v3_RemateSacos_e0_Sano_LOD1", "Defensas/hesco/SM_HescoRemateEspejo_v4_Red_e0_Sano_LOD1" } },
            { "SM_KitAnden_F11", new[] { "Nules/kit/SM_KitAnden_F11_v0_LOD1", "Nules/kit/SM_KitAnden_F11_v1_LOD1", "Nules/kit/SM_KitAnden_F11_v2_LOD1", "Nules/kit/SM_KitAnden_F11_v3_LOD1", "Nules/kit/SM_KitAnden_F11_v4_LOD1" } },
            { "SM_KitAnden_F7", new[] { "Nules/kit/SM_KitAnden_F7_v0_LOD1", "Nules/kit/SM_KitAnden_F7_v1_LOD1", "Nules/kit/SM_KitAnden_F7_v2_LOD1", "Nules/kit/SM_KitAnden_F7_v3_LOD1", "Nules/kit/SM_KitAnden_F7_v4_LOD1" } },
            { "SM_KitMarquesina", new[] { "KitEstaciones/SM_KitMarquesina_v0_LOD1", "KitEstaciones/SM_KitMarquesina_v1_LOD1", "KitEstaciones/SM_KitMarquesina_v2_LOD1", "KitEstaciones/SM_KitMarquesina_v3_LOD1", "KitEstaciones/SM_KitMarquesina_v4_LOD1", "KitEstaciones/SM_KitMarquesina_v5_LOD1" } },
            { "SM_KitVia", new[] { "Nules/kit/SM_KitVia_v0_LOD1", "Nules/kit/SM_KitVia_v1_LOD1", "Nules/kit/SM_KitVia_v2_LOD1", "Nules/kit/SM_KitVia_v3_LOD1" } },
            { "SM_MarquesinaAdosada", new[] { "Nules/marquesina_adosada/SM_MarquesinaAdosada_v0_LOD1", "Nules/marquesina_adosada/SM_MarquesinaAdosada_v1_LOD1", "Nules/marquesina_adosada/SM_MarquesinaAdosada_v2_LOD1", "Nules/marquesina_adosada/SM_MarquesinaAdosada_v3_LOD1", "Nules/marquesina_adosada/SM_MarquesinaAdosada_v4_LOD1" } },
            { "SM_MuelleCarga", new[] { "Nules/muelle_carga/SM_MuelleCarga_v0_LOD1", "Nules/muelle_carga/SM_MuelleCarga_v1_LOD1", "Nules/muelle_carga/SM_MuelleCarga_v2_LOD1", "Nules/muelle_carga/SM_MuelleCarga_v3_LOD1", "Nules/muelle_carga/SM_MuelleCarga_v4_LOD1" } },
            { "SM_MuroHesco", new[] { "Defensas/muro/SM_MuroHesco_v0_MallaLimpia_e0_Sano_LOD1", "Defensas/muro/SM_MuroHesco_v1_Geotextil_e0_Sano_LOD1", "Defensas/muro/SM_MuroHesco_v2_Escalonado_e0_Sano_LOD1", "Defensas/muro/SM_MuroHesco_v3_RemateSacos_e0_Sano_LOD1", "Defensas/muro/SM_MuroHesco_v4_Red_e0_Sano_LOD1" } },
            { "SM_NavePoligono_Modulo", new[] { "Nules/nave/SM_NavePoligono_Modulo_e0_Intacta_LOD1" } },
            { "SM_NavePoligono_Testero", new[] { "Nules/nave/SM_NavePoligono_Testero_e0_Intacta_LOD1" } },
            { "SM_Palmera", new[] { "Nules/urbano/SM_Palmera_v0_LOD1", "Nules/urbano/SM_Palmera_v1_LOD1", "Nules/urbano/SM_Palmera_v2_LOD1", "Nules/urbano/SM_Palmera_v3_LOD1", "Nules/urbano/SM_Palmera_v4_LOD1" } },
            { "SM_ParadaBus", new[] { "Urbano/SM_ParadaBus_v0_Entera_LOD1", "Urbano/SM_ParadaBus_v1_SinCristales_LOD1", "Urbano/SM_ParadaBus_v2_Anuncio_LOD1", "Urbano/SM_ParadaBus_v3_Refugio_LOD1", "Urbano/SM_ParadaBus_v4_Caida_LOD1" } },
            { "SM_PuertaVehiculos", new[] { "Defensas/muro/SM_PuertaVehiculos_v0_TuboChapa_e0_Sano_LOD1", "Defensas/muro/SM_PuertaVehiculos_v1_ChapaOndulada_e0_Sano_LOD1", "Defensas/muro/SM_PuertaVehiculos_v2_Tablones_e0_Sano_LOD1", "Defensas/muro/SM_PuertaVehiculos_v3_MallaEspino_e0_Sano_LOD1", "Defensas/muro/SM_PuertaVehiculos_v4_Blindada_e0_Sano_LOD1" } },
            { "SM_PuertaVia", new[] { "Defensas/puerta_via/SM_PuertaVia_v0_TuboChapa_e0_Sano_LOD1", "Defensas/puerta_via/SM_PuertaVia_v1_ChapaOndulada_e0_Sano_LOD1", "Defensas/puerta_via/SM_PuertaVia_v2_Tablones_e0_Sano_LOD1", "Defensas/puerta_via/SM_PuertaVia_v3_MallaEspino_e0_Sano_LOD1", "Defensas/puerta_via/SM_PuertaVia_v4_Blindada_e0_Sano_LOD1" } },
            { "SM_PuertaViaDoble", new[] { "Nules/puerta_via_doble/SM_PuertaViaDoble_v0_TuboChapa_e0_Sano_LOD1", "Nules/puerta_via_doble/SM_PuertaViaDoble_v1_ChapaOndulada_e0_Sano_LOD1", "Nules/puerta_via_doble/SM_PuertaViaDoble_v2_Tablones_e0_Sano_LOD1", "Nules/puerta_via_doble/SM_PuertaViaDoble_v3_MallaEspino_e0_Sano_LOD1", "Nules/puerta_via_doble/SM_PuertaViaDoble_v4_Blindada_e0_Sano_LOD1" } },
            { "SM_Quiosco", new[] { "Urbano/SM_Quiosco_v0_Cerrado_LOD1", "Urbano/SM_Quiosco_v1_Abierto_LOD1", "Urbano/SM_Quiosco_v2_Loteria_LOD1", "Urbano/SM_Quiosco_v3_Saqueado_LOD1", "Urbano/SM_Quiosco_v4_Quemado_LOD1" } },
            { "SM_Semaforo", new[] { "Urbano/SM_Semaforo_v0_Simple_LOD1", "Urbano/SM_Semaforo_v1_Brazo_LOD1", "Urbano/SM_Semaforo_v2_Peatonal_LOD1", "Urbano/SM_Semaforo_v3_Doblado_LOD1", "Urbano/SM_Semaforo_v4_Caido_LOD1" } },
            { "SM_Senal", new[] { "Urbano/SM_Senal_v0_Direccion_LOD1", "Urbano/SM_Senal_v1_Redonda_LOD1", "Urbano/SM_Senal_v2_Triangular_LOD1", "Urbano/SM_Senal_v3_Doble_LOD1", "Urbano/SM_Senal_v4_Doblada_LOD1" } },
            { "SM_poste_catenaria", new[] { "Sagunto/poste_catenaria/SM_poste_catenaria_LOD1" } },
            { "aerodromo/pino", new[] { "Betera/pino/SM_Pino_v0_LOD1", "Betera/pino/SM_Pino_v1_LOD1", "Betera/pino/SM_Pino_v2_LOD1", "Betera/pino/SM_Pino_v3_LOD1", "Betera/pino/SM_Pino_v4_LOD1" } },
            { "via/topera", new[] { "Sagunto/senales/SM_topera_v0_LOD1", "Sagunto/senales/SM_topera_v1_LOD1", "Sagunto/senales/SM_topera_v2_LOD1", "Sagunto/senales/SM_topera_v3_LOD1", "Sagunto/senales/SM_topera_v4_LOD1", "Sagunto/senales/SM_topera_v5_LOD1" } },
        };

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static Material _ground, _missing;
        public const float ZonaW = 256f, ZonaD = 128f;

        public static Result Montar(Transform root, bool soloZonaMvp = true)
        {
            var res = new Result { Root = root };
            var csv = BalanceSource.Read("nules_instancias.csv");
            if (csv == null) { Debug.LogWarning("[NULES] falta nules_instancias.csv"); return res; }
            var suelo = GameObject.CreatePrimitive(PrimitiveType.Quad); suelo.name = "Suelo de Nules (plano; terreno de Álvaro pendiente)"; Kill(suelo.GetComponent<Collider>());
            suelo.transform.SetParent(root, false); suelo.transform.localPosition = new Vector3(ZonaW * 0.5f, 0f, ZonaD * 0.5f); suelo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            suelo.transform.localScale = new Vector3(ZonaW + 160f, ZonaD + 160f, 1f); suelo.GetComponent<MeshRenderer>().sharedMaterial = _ground ??= Flat("8a7a5c");
            var lineas = csv.Replace("\r", "").Split('\n');
            for (int i = 1; i < lineas.Length; i++)
            {
                var l = lineas[i].Trim(); if (l.Length == 0) continue;
                var c = l.Split(';'); if (c.Length < 9) continue;
                if (soloZonaMvp && c[8].Trim() != "si") continue;
                string prefab = c[1];
                int v = int.Parse(c[2], System.Globalization.CultureInfo.InvariantCulture);
                float x = F(c[3]), y = F(c[4]), z = F(c[5]), yaw = F(c[6]), ex = F(c[7]);
                GameObject go = null;
                if (Ficheros.TryGetValue(prefab, out var fs) && fs.Length > 0)
                {
                    int k = v >= 0 ? Mathf.Clamp(v, 0, fs.Length - 1) : EstacionDato.VariantOf(x, z, fs.Length);
                    go = Modelo(fs[k], root);
                }
                if (go == null) { go = Caja(root, prefab); res.Faltan++; res.Missing.Add(prefab + " (" + x + ", " + z + ")"); }
                go.transform.localPosition = new Vector3(x, y, z);
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
                if (ex != 0f && ex != 1f) { var sc = go.transform.localScale; go.transform.localScale = new Vector3(sc.x * ex, sc.y, sc.z); }
                res.Placed++;
            }
            Debug.Log("[NULES] zona montada: " + res.Placed + " piezas, " + res.Faltan + " sin modelo");
            var cuenta = new Dictionary<string, int>(); foreach (var m in res.Missing) { var p = m.Substring(0, m.IndexOf(' ')); cuenta[p] = cuenta.TryGetValue(p, out var n) ? n + 1 : 1; }
            foreach (var kv in cuenta) Debug.Log("[NULES] sin modelo: " + kv.Key + " ×" + kv.Value);
            return res;
        }

        static float F(string s) { return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture); }

        static GameObject Modelo(string ruta, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Models/" + ruta); if (prefab == null) return null;
            string carpeta = ruta.Substring(0, ruta.LastIndexOf('/'));
            var inst = Object.Instantiate(prefab, parent); inst.name = ruta.Substring(carpeta.Length + 1);
            if (!Mats.TryGetValue(carpeta, out var mat) || mat == null)
            {
                var tex = Resources.Load<Texture2D>("Models/" + carpeta + "/T_Paleta32");
                mat = Flat("ffffff"); if (tex != null) mat.SetTexture("_BaseMap", tex); mat.enableInstancing = true; Mats[carpeta] = mat;
            }
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.On; }
            return inst;
        }

        static GameObject Caja(Transform parent, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "FALTA " + name; Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localScale = new Vector3(3f, 2f, 3f);
            go.GetComponent<MeshRenderer>().sharedMaterial = _missing ??= Flat("9a9a9a");
            return go;
        }

        static void Kill(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
        static Material Flat(string hex) { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); ColorUtility.TryParseHtmlString("#" + hex, out var c); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0f); return m; }
    }
}
