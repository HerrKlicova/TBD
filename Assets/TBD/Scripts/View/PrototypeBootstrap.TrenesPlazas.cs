using System.Collections.Generic;
using TBD.Net;
using TBD.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace TBD.View
{
    /// <summary>
    /// Lucía (11-11 22:40, prioridad alta): la zona dibuja TODOS los trenes que hay en la estación, cada uno en su plaza (Nules 2, Sagunto 2), y los que no
    /// caben esperan fuera, a las puertas (D-N152). Mismo criterio que el rótulo del mapa (OperationsMap3D.TrenesEnEstacion): parado en el andén de este nodo,
    /// o con GateNode == este nodo. El primero del andén es el tren de la simulación de la zona (el de siempre); los demás son solo vista.
    /// Plaza 1: vía general junto al andén (la de siempre). Plaza 2: vía 2, al oeste (X −14…50, Z = vía − 5). A las puertas: fuera de la puerta este, en la general.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        sealed class TrenExtra { public int Servicio; public Transform[] Partes; public string Nombre; public Vector3 Frente; }
        readonly List<TrenExtra> _trenesExtra = new List<TrenExtra>();
        readonly List<(string nombre, Vector3 frente, bool puertas)> _trenesRotulos = new List<(string, Vector3, bool)>();
        float _trenesT;

        static int PlazasDe(string estacion) { return estacion == "Nules" || estacion == "Sagunto" ? 2 : 1; }

        /// <summary>Los servicios parados en esta estación: primero los del andén (por índice), luego los de las puertas.</summary>
        void TrenesDeLaEstacion(List<int> anden, List<int> puertas)
        {
            anden.Clear(); puertas.Clear(); var w = NetSession.A; if (w == null || _zoneNode < 0) return;
            for (int k = 0; k < w.Services.Count; k++)
            {
                var sv = w.Services[k]; if (sv.Destroyed || !sv.AtStop || sv.Stops.Count == 0) continue;
                if (sv.GateNode == _zoneNode) { puertas.Add(k); continue; }
                if (sv.Stops[sv.StopIndex].Node == _zoneNode) anden.Add(k);
            }
        }

        readonly List<int> _tAnden = new List<int>(), _tPuertas = new List<int>();

        /// <summary>Cada 0,25 s (solo vista): coloca un tren por servicio parado en la estación, además del tren de la zona.</summary>
        void UpdateTrenesPlazas()
        {
            if (_trainParts == null || _zoneNode < 0 || _enBetera) return;
            if (Time.unscaledTime - _trenesT < 0.25f) return; _trenesT = Time.unscaledTime;
            TrenesDeLaEstacion(_tAnden, _tPuertas); var w = NetSession.A; if (w == null) return;
            string est = w.Graph.Nodes[_zoneNode].Name; int plazas = PlazasDe(est);
            // el primero del andén es el tren de la zona (sus partes ya las coloca UpdateTrainVisual); los demás del andén ocupan plazas 2…; los que sobran, a las puertas
            var colocar = new List<(int sv, Vector3 frente)>();
            float ty = Level.TrackY;
            for (int i = 1; i < _tAnden.Count; i++)
            {
                if (i < plazas) colocar.Add((_tAnden[i], new Vector3(50f, 0f, ty - 5f)));                  // plaza 2: vía 2, el frente en la topera (X 50)
                else colocar.Add((_tAnden[i], new Vector3(224f + 70f * (i - plazas), 0f, ty)));          // sin plaza: a las puertas (fuera de la puerta este)
            }
            for (int j = 0; j < _tPuertas.Count; j++) colocar.Add((_tPuertas[j], new Vector3(224f + 70f * (j + System.Math.Max(0, _tAnden.Count - plazas)), 0f, ty)));
            // si en la zona no hay tren de la red (HasTrain falso) pero sí uno a las puertas, el de las puertas se dibuja igualmente con estos
            while (_trenesExtra.Count < colocar.Count) _trenesExtra.Add(new TrenExtra { Partes = CrearPartesTren("Tren extra " + _trenesExtra.Count) });
            for (int k = 0; k < _trenesExtra.Count; k++)
            {
                var te = _trenesExtra[k]; bool usar = k < colocar.Count;
                foreach (var p in te.Partes) if (p != null) p.gameObject.SetActive(usar);
                if (!usar) continue;
                te.Servicio = colocar[k].sv; te.Nombre = w.Services[te.Servicio].Train.Name; te.Frente = colocar[k].frente;
                float f = te.Frente.x, z = te.Frente.z;
                te.Partes[0].position = new Vector3(f - 12.015f, 0f, z); te.Partes[1].position = new Vector3(f - 24.03f - 9.95f, 0f, z); te.Partes[2].position = new Vector3(f - 24.03f - 19.9f - 9.95f, 0f, z);
            }
            // rótulos: el tren de la zona y los extra, con su nombre (como en el mapa)
            _trenesRotulos.Clear();
            if (_tAnden.Count > 0 && _trainParts[0] != null && _trainParts[0].gameObject.activeInHierarchy) _trenesRotulos.Add((w.Services[_tAnden[0]].Train.Name, _trainParts[0].position, false));
            for (int k = 0; k < colocar.Count; k++) _trenesRotulos.Add((_trenesExtra[k].Nombre, _trenesExtra[k].Partes[0].position, _tPuertas.Contains(colocar[k].sv) || colocar[k].frente.x > 200f));
            if (_trenesLog != _tAnden.Count * 10 + _tPuertas.Count)
            {
                _trenesLog = _tAnden.Count * 10 + _tPuertas.Count;
                var nombres = new List<string>(); foreach (var r in _trenesRotulos) nombres.Add(r.nombre + (r.puertas ? " (a las puertas)" : ""));
                Debug.Log($"[TRENES] {est}: {_tAnden.Count} en el andén y {_tPuertas.Count} a las puertas ({plazas} plazas): {string.Join(", ", nombres)}");
            }
        }
        int _trenesLog = -1;

        /// <summary>Las tres partes de un tren (locomotora R1 y dos vagones R2 de Teo, de cara a +X), como las del tren de la zona.</summary>
        Transform[] CrearPartesTren(string nombre)
        {
            var partes = new Transform[3];
            string[] modelos = { "Models/R1/SM_R1_Locomotora_LOD1", "Models/R2/SM_R2_VagonTropa_LOD1", "Models/R2/SM_R2_VagonTropa_LOD1" };
            string[] texturas = { "Models/R1/T_Paleta32", "Models/R2/T_Paleta32", "Models/R2/T_Paleta32" };
            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject(nombre + (i == 0 ? " · locomotora" : " · vagón " + i)); root.transform.SetParent(transform, false);
                var prefab = Resources.Load<GameObject>(modelos[i]); var tex = Resources.Load<Texture2D>(texturas[i]);
                if (prefab != null && tex != null)
                {
                    var inst = Instantiate(prefab, root.transform); inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    var mat = Mat("ffffff"); mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white);
                    foreach (var r in inst.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.On; }
                }
                else { var box = Box("Caja", Vector3.zero, new Vector3(i == 0 ? 24f : 19.9f, 2.5f, 3f), Mat("50541c"), root.transform); box.transform.localPosition = new Vector3(0f, 1.25f, 0f); }
                partes[i] = root.transform;
            }
            return partes;
        }

        /// <summary>Rótulo con el nombre de cada tren sobre su locomotora (IMGUI), como en el mapa.</summary>
        void GuiTrenesPlazas()
        {
            if (_trenesRotulos.Count == 0 || _worldCam == null || Event.current.type != EventType.Repaint) return;
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(11, Mathf.RoundToInt(14f * Screen.height / 1080f)), alignment = TextAnchor.MiddleCenter }; st.normal.textColor = new Color32(0xf0, 0xdc, 0x9c, 0xff);
            foreach (var r in _trenesRotulos)
            {
                var sp = ScreenOf(r.frente + Vector3.up * 4f); if (sp.z < 0f) continue;
                GUI.Label(new Rect(sp.x - 110f, Screen.height - sp.y - 12f, 220f, 24f), r.nombre + (r.puertas ? " · a las puertas" : ""), st);
            }
        }
    }
}
