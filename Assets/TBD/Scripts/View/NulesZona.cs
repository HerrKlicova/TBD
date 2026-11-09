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
            { "SM_AcopioMunicion", new[] { "Acopios/SM_AcopioMunicion_v0_Lleno_LOD1", "Acopios/SM_AcopioMunicion_v1_Mitad_LOD1", "Acopios/SM_AcopioMunicion_v2_Vacio_LOD1", "Acopios/SM_AcopioMunicion_v3_Saqueado_LOD1", "Acopios/SM_AcopioMunicion_v4_Quemado_LOD1" } },
            { "SM_AcopioCombustible", new[] { "Acopios/SM_AcopioCombustible_v0_Lleno_LOD1", "Acopios/SM_AcopioCombustible_v1_Mitad_LOD1", "Acopios/SM_AcopioCombustible_v2_Vacio_LOD1", "Acopios/SM_AcopioCombustible_v3_Saqueado_LOD1", "Acopios/SM_AcopioCombustible_v4_Quemado_LOD1" } },
            { "SM_AcopioMateriales", new[] { "Acopios/SM_AcopioMateriales_v0_Lleno_LOD1", "Acopios/SM_AcopioMateriales_v1_Mitad_LOD1", "Acopios/SM_AcopioMateriales_v2_Vacio_LOD1", "Acopios/SM_AcopioMateriales_v3_Saqueado_LOD1", "Acopios/SM_AcopioMateriales_v4_Quemado_LOD1" } },
            { "SM_BajoComercial", new[] { "Nules/pueblo/BajoComercial/SM_BajoComercial_v0_Bar_LOD1", "Nules/pueblo/BajoComercial/SM_BajoComercial_v1_Ultramarinos_LOD1", "Nules/pueblo/BajoComercial/SM_BajoComercial_v2_Farmacia_LOD1", "Nules/pueblo/BajoComercial/SM_BajoComercial_v3_Saqueada_LOD1", "Nules/pueblo/BajoComercial/SM_BajoComercial_v4_Tapiada_LOD1" } },
            { "SM_CasaPatio", new[] { "Nules/pueblo/CasaPatio/SM_CasaPatio_v0_Porton_LOD1", "Nules/pueblo/CasaPatio/SM_CasaPatio_v1_Abierto_LOD1", "Nules/pueblo/CasaPatio/SM_CasaPatio_v2_Parra_LOD1", "Nules/pueblo/CasaPatio/SM_CasaPatio_v3_Naranjo_LOD1", "Nules/pueblo/CasaPatio/SM_CasaPatio_v4_Derruida_LOD1" } },
            { "SM_Esquina", new[] { "Nules/pueblo/Esquina/SM_Esquina_v0_DosPlantas_LOD1", "Nules/pueblo/Esquina/SM_Esquina_v1_TresPlantas_LOD1", "Nules/pueblo/Esquina/SM_Esquina_v2_BajoChaflan_LOD1", "Nules/pueblo/Esquina/SM_Esquina_v3_CalAmarilla_LOD1", "Nules/pueblo/Esquina/SM_Esquina_v4_Tapiada_LOD1" } },
            { "SM_TresAlturas", new[] { "Nules/pueblo/TresAlturas/SM_TresAlturas_v0_Cal_LOD1", "Nules/pueblo/TresAlturas/SM_TresAlturas_v1_Ocre_LOD1", "Nules/pueblo/TresAlturas/SM_TresAlturas_v2_Gris_LOD1", "Nules/pueblo/TresAlturas/SM_TresAlturas_v3_Ropa_LOD1", "Nules/pueblo/TresAlturas/SM_TresAlturas_v4_Ruina_LOD1" } },
            { "SM_Balsa", new[] { "Nules/viveres/Balsa/SM_Balsa_v0_Llena_LOD1", "Nules/viveres/Balsa/SM_Balsa_v1_Media_LOD1", "Nules/viveres/Balsa/SM_Balsa_v2_Vacia_LOD1", "Nules/viveres/Balsa/SM_Balsa_v3_Vallada_LOD1", "Nules/viveres/Balsa/SM_Balsa_v4_Rota_LOD1" } },
            { "SM_CasaLabor", new[] { "Nules/viveres/CasaLabor/SM_CasaLabor_v0_Encalada_LOD1", "Nules/viveres/CasaLabor/SM_CasaLabor_v1_Ocre_LOD1", "Nules/viveres/CasaLabor/SM_CasaLabor_v2_Porche_LOD1", "Nules/viveres/CasaLabor/SM_CasaLabor_v3_Abandonada_LOD1", "Nules/viveres/CasaLabor/SM_CasaLabor_v4_Cipreses_LOD1" } },
            { "SM_Naranjos", new[] { "Nules/viveres/Naranjos/SM_Naranjos_v0_Plantones_LOD1", "Nules/viveres/Naranjos/SM_Naranjos_v1_Jovenes_LOD1", "Nules/viveres/Naranjos/SM_Naranjos_v2_HileraJoven_LOD1", "Nules/viveres/Naranjos/SM_Naranjos_v3_HileraAdulta_LOD1", "Nules/viveres/Naranjos/SM_Naranjos_v4_Calva_LOD1" } },
            { "SM_Palots", new[] { "Nules/viveres/Palots/SM_Palots_v0_Pila2_LOD1", "Nules/viveres/Palots/SM_Palots_v1_Bloque_LOD1", "Nules/viveres/Palots/SM_Palots_v2_Fila_LOD1", "Nules/viveres/Palots/SM_Palots_v3_Volcados_LOD1", "Nules/viveres/Palots/SM_Palots_v4_ConFruta_LOD1" } },
            { "SM_Tapia", new[] { "Nules/viveres/Tapia/SM_Tapia_v0_Recto_LOD1", "Nules/viveres/Tapia/SM_Tapia_v1_Esquina_LOD1", "Nules/viveres/Tapia/SM_Tapia_v2_Porton_LOD1", "Nules/viveres/Tapia/SM_Tapia_v3_Roto_LOD1", "Nules/viveres/Tapia/SM_Tapia_v4_Contrafuertes_LOD1" } },
            { "aerodromo/pino", new[] { "Betera/pino/SM_Pino_v0_LOD1", "Betera/pino/SM_Pino_v1_LOD1", "Betera/pino/SM_Pino_v2_LOD1", "Betera/pino/SM_Pino_v3_LOD1", "Betera/pino/SM_Pino_v4_LOD1" } },
            { "via/topera", new[] { "Sagunto/senales/SM_topera_v0_LOD1", "Sagunto/senales/SM_topera_v1_LOD1", "Sagunto/senales/SM_topera_v2_LOD1", "Sagunto/senales/SM_topera_v3_LOD1", "Sagunto/senales/SM_topera_v4_LOD1", "Sagunto/senales/SM_topera_v5_LOD1" } },
        };

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static Material _ground, _missing;
        public const float ZonaW = 256f, ZonaD = 128f;

        public static Result Montar(Transform root, bool soloZonaMvp = false)   // Álvaro: «no» en en_zona_mvp solo es que cae en el faldón, que se ve: se monta todo
        {
            var res = new Result { Root = root };
            var csv = BalanceSource.Read("nules_instancias.csv");
            if (csv == null) { Debug.LogWarning("[NULES] falta nules_instancias.csv"); return res; }
            Suelo(root);
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


        // Terreno de Álvaro (mr-mak docs/niveles/nules_zonas_1m.png, d7d2b89): una zona por píxel a 1 m, 416 × 288, X −80..336 y Z −80..208, fila 0 arriba (Z 208);
        // ids como sagunto_zonas.json. Se importa desde el PC a Resources/Suelo/nules_zonas_1m (Read/Write, sin compresión, Point). Se compone a 5 px/m con las
        // mismas texturas de suelo que Sagunto (Resources/Suelo/suelo_*_64, a 0,1 m por texel). Si falta el mapa, un plano color tierra.
        public const float SueloX0 = -80f, SueloZ0 = -80f; public const int SueloW = 416, SueloH = 288; const int PxM = 5;
        static void Suelo(Transform root)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = "Suelo de Nules"; Kill(go.GetComponent<Collider>());
            go.transform.SetParent(root, false); go.transform.localPosition = new Vector3(SueloX0 + SueloW * 0.5f, 0f, SueloZ0 + SueloH * 0.5f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); go.transform.localScale = new Vector3(SueloW, SueloH, 1f);
            var mr = go.GetComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
            var mapa = Resources.Load<Texture2D>("Suelo/nules_zonas_1m");
            Color32[] map = null; try { if (mapa != null && mapa.width == SueloW && mapa.height == SueloH) map = mapa.GetPixels32(); } catch (System.Exception e) { Debug.LogWarning("[NULES] nules_zonas_1m sin Read/Write: " + e.Message); }
            if (map == null) { mr.sharedMaterial = _ground ??= Flat("8a7a5c"); Debug.Log("[NULES] suelo: plano (falta Suelo/nules_zonas_1m de Álvaro)"); return; }
            Color32[] Px(string n) { var t = Resources.Load<Texture2D>("Suelo/" + n); try { return t != null ? t.GetPixels32() : null; } catch { return null; } }
            var apis = Px("suelo_apisonada_64"); var labr = Px("suelo_labrada_64") ?? apis; var asf = Px("suelo_asfalto_64") ?? apis; var rod = Px("suelo_rodadas_64") ?? apis; var seca = Px("suelo_seca_64") ?? apis;
            Color32 Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
            Color32 balasto = Hex("605c48"), agua = Hex("6c7874"), aguaBorde = Hex("94948c"), plano = Hex("8a7a5c");
            int W = SueloW * PxM, H = SueloH * PxM; var px = new Color32[W * H];
            int Zona(int mx, int mz) { mx = Mathf.Clamp(mx, 0, SueloW - 1); mz = Mathf.Clamp(mz, 0, SueloH - 1); return map[mz * SueloW + mx].r; }   // GetPixels32 va de abajo arriba: mz = Z − Z0
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int mx = x / PxM, mz = y / PxM; int zona = Zona(mx, mz);
                    int u = (x * 2) & 63, v = (y * 2) & 63; Color32 c;                       // 0,2 m por píxel: cada píxel salta 2 texels de la textura de 0,1 m
                    switch (zona)
                    {
                        case 3: c = balasto; break;
                        case 2: c = rod != null ? rod[v * 64 + u] : plano; break;
                        case 4: c = asf != null ? asf[v * 64 + u] : plano; break;
                        case 0: c = seca != null ? seca[v * 64 + u] : plano; break;
                        case 5: c = labr != null ? labr[v * 64 + u] : plano; break;
                        case 9: c = (Zona(mx + 1, mz) != 9 || Zona(mx - 1, mz) != 9 || Zona(mx, mz + 1) != 9 || Zona(mx, mz - 1) != 9) && (x % PxM == 0 || y % PxM == 0) ? aguaBorde : agua; break;
                        default: c = apis != null ? apis[v * 64 + u] : plano; break;
                    }
                    c.a = 255; px[y * W + x] = c;
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Suelo de Nules compuesto" };
            tex.SetPixelData(px, 0); tex.Apply(false, true);
            var m = Flat("ffffff"); m.SetTexture("_BaseMap", tex); mr.sharedMaterial = m;
            Debug.Log("[NULES] suelo: mapa de zonas de Álvaro compuesto a " + PxM + " px/m (" + W + " × " + H + ")");
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
