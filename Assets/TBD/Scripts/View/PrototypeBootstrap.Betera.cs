using System.Collections;
using System.Collections.Generic;
using TBD.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>F0 del núcleo (lado zona, Marcos): lo que se selecciona en una zona y sigue seleccionado en el mapa. El mapa (Iker) lo lee para el clic derecho
    /// en el destino (NH90: Aerodromo.Enviar; tren: SetSchedule). Vive fuera de la escena: sobrevive al cambio de zona a mapa. Solo vista y órdenes del jugador.</summary>
    public static class SeleccionGlobal
    {
        /// <summary>Índices de AerodromoVista.Estado.Aeronaves seleccionados en Bétera.</summary>
        public static readonly List<int> NH90 = new List<int>();
        /// <summary>Servicio (índice en NetWorld.Services) del tren seleccionado en el andén, y el nodo donde estaba; −1 si no hay.</summary>
        public static int Tren = -1, TrenNodo = -1;
        public static bool Vacia => NH90.Count == 0 && Tren < 0;
        public static void Limpiar() { NH90.Clear(); Tren = -1; TrenNodo = -1; }
        public static string Texto()
        {
            if (Tren >= 0 && NetSession.A != null && Tren < NetSession.A.Services.Count) return "Tren " + NetSession.A.Services[Tren].Train.Name + " seleccionado";
            if (NH90.Count > 0) { var a = AerodromoVista.Estado; var n = new List<string>(); foreach (int i in NH90) if (a != null && i < a.Aeronaves.Count) n.Add(a.Aeronaves[i].Nombre); return string.Join(" y ", n) + " seleccionado" + (n.Count > 1 ? "s" : ""); }
            return "";
        }
    }

    /// <summary>Entrar en Bétera: el mapa pone Activa = true antes de cargar la zona (o -zonabetera para probar). La zona monta AerodromoZona (Bruno y Teo)
    /// y los NH90 en los puestos de los hangares; con la rueda o la M se vuelve al mapa sin perder la selección.</summary>
    public static class ZonaBetera { public static bool Activa; }

    public partial class PrototypeBootstrap
    {
        AerodromoZona.Result _beteraZona; Transform _beteraRoot; bool _enBetera;
        readonly List<(GameObject go, int aero, GameObject anillo)> _nh90Zona = new List<(GameObject, int, GameObject)>();
        float _beteraNotch; bool _beteraSaliendo; int _beteraFoco = 3;   // la cámara se coloca sobre Bétera en los primeros fotogramas (la zona la recentra al arrancar)

        static readonly Vector3 BeteraOrigen = new Vector3(2000f, 0f, 0f);   // lejos de la zona de Sagunto que sigue debajo (la sim de Bétera es solo vista en la F0)

        void MontarBetera()
        {
            _beteraRoot = new GameObject("Zona de Bétera").transform; _beteraRoot.position = BeteraOrigen;
            // la noche (Night.cs, Reloj.cs) deja BalanceSource.Reader en StreamingAssets/Noche: Bétera se lee de Data (qa52 I-Noche115/118: «falta estacion_betera.json»)
            var lectorAntes = BalanceSource.Reader;
            BalanceSource.Reader = f => { var pth = System.IO.Path.Combine(BalanceBootstrap.Folder, f); return System.IO.File.Exists(pth) ? System.IO.File.ReadAllText(pth) : null; };
            try { _beteraZona = AerodromoZona.Montar(_beteraRoot); } finally { BalanceSource.Reader = lectorAntes; }
            _betera = _beteraZona;
            if (_beteraZona == null) { Debug.LogWarning("[BETERA] no se ha podido montar la zona"); return; }
            _enBetera = true;
            _focus = BeteraOrigen + AerodromoZona.Centro; _targetOrtho = 45f;
            // NH90 en los puestos de los hangares, por orden: hangar 0 puestos 0 y 1, hangar 1…
            var est = AerodromoVista.Estado; var prefab = Resources.Load<GameObject>("Models/Vehiculos/nh90/SM_NH90_LOD1"); var tex = Resources.Load<Texture2D>("Models/Vehiculos/nh90/T_Paleta32");
            Material mat = null; if (prefab != null) { mat = Mat("ffffff"); if (tex != null) mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white); }
            int puesto = 0;
            if (est != null) for (int i = 0; i < est.Aeronaves.Count; i++)
            {
                var x = est.Aeronaves[i]; if (x.Estado != AeronaveEstado.EnBase && x.Estado != AeronaveEstado.Rearmando && x.Estado != AeronaveEstado.Preparando) continue;
                Vector3 p; float yaw;
                if (_beteraZona.Hangares.Count > 0) { var h = _beteraZona.Hangares[(puesto / 2) % _beteraZona.Hangares.Count]; p = AerodromoZona.PuestoNH90(h, puesto % 2, out yaw); }
                else { p = _beteraRoot.position + AerodromoZona.Centro + new Vector3(puesto * 20f, 0f, 0f); yaw = 0f; }
                puesto++;
                GameObject go;
                if (prefab != null) { go = Instantiate(prefab, _beteraRoot); foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat; }
                else { go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(_beteraRoot, false); go.transform.localScale = new Vector3(4f, 3f, 16f); }
                go.name = "NH90 " + x.Nombre; go.transform.position = p; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                var anillo = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(anillo.GetComponent<Collider>()); anillo.name = "Selección";
                anillo.transform.SetParent(go.transform, false); anillo.transform.localPosition = new Vector3(0f, 0.08f, 0f); anillo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); anillo.transform.localScale = new Vector3(18f, 18f, 1f);
                anillo.GetComponent<MeshRenderer>().sharedMaterial = Mat("e0c050"); anillo.SetActive(SeleccionGlobal.NH90.Contains(i));
                _nh90Zona.Add((go, i, anillo)); Debug.Log($"[BETERA] {x.Nombre} en ({p.x:0.0}; {p.z:0.0}) rumbo {yaw:0}");
            }
            Debug.Log($"[BETERA] zona de juego: {_nh90Zona.Count} NH90 en los hangares; clic para seleccionar, rueda o M para volver al mapa con la selección");
        }

        /// <summary>Cada fotograma en Bétera: clic izquierdo selecciona el NH90 más cercano al cursor (a 40 px; Mayús añade), clic en vacío deselecciona;
        /// la cubierta de los hangares se apaga al acercar; rueda en el tope (dos muescas en 0,6 s) o M: al mapa.</summary>
        void UpdateBetera()
        {
            if (!_enBetera || _beteraSaliendo) return;
            if (_beteraFoco > 0 && Time.timeSinceLevelLoad > 1f * (4 - _beteraFoco)) { _focus = _nh90Zona.Count > 0 ? _nh90Zona[0].go.transform.position : BeteraOrigen + AerodromoZona.Centro; _targetOrtho = _nh90Zona.Count > 0 ? 26f : 45f; _beteraFoco--; }   // se entra mirando a los NH90 (sin la cubierta del hangar)
            var mouse = Mouse.current; var kb = Keyboard.current;
            if (_beteraZona != null) foreach (var h in _beteraZona.Hangares) if (h.Cubierta != null) h.Cubierta.SetActive(_orthoSize > AerodromoZona.OrthoSinCubierta);
            if (mouse != null && _worldCam != null && mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 mp = mouse.position.ReadValue(); int best = -1; float bd = float.MaxValue;
                for (int k = 0; k < _nh90Zona.Count; k++)
                {
                    if (!PantallaDe(_nh90Zona[k].go, out var mn, out var mx)) continue;
                    mn -= new Vector2(12f, 12f); mx += new Vector2(12f, 12f);
                    if (mp.x < mn.x || mp.x > mx.x || mp.y < mn.y || mp.y > mx.y) continue;
                    float d = Vector2.Distance(mp, (mn + mx) * 0.5f); if (d < bd) { bd = d; best = k; }
                }
                Debug.Log($"[SELECCION] clic en Bétera ({mp.x:0};{mp.y:0}): {(best >= 0 ? "NH90 " + best : "nada")}");
                bool suma = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                if (!suma) { SeleccionGlobal.NH90.Clear(); SeleccionGlobal.Tren = -1; }
                if (best >= 0 && !SeleccionGlobal.NH90.Contains(_nh90Zona[best].aero)) SeleccionGlobal.NH90.Add(_nh90Zona[best].aero);
                foreach (var n in _nh90Zona) n.anillo.SetActive(SeleccionGlobal.NH90.Contains(n.aero));
                if (best >= 0) Debug.Log("[SELECCION] " + SeleccionGlobal.Texto());
            }
            bool salir = kb != null && kb.mKey.wasPressedThisFrame;
            if (mouse != null)
            {
                float s = mouse.scroll.ReadValue().y;
                if (s < 0f && _targetOrtho >= OrthoMax - 0.5f) { if (Time.unscaledTime - _beteraNotch < 0.6f) salir = true; else _beteraNotch = Time.unscaledTime; }
            }
            if (salir) StartCoroutine(SalirDeBetera());
        }

        /// <summary>En una estación: clic izquierdo sobre el tren parado en el andén lo selecciona (con su carga) para el mapa; clic en otro sitio no lo quita
        /// (la selección de pelotones sigue como siempre). Mayús+clic sobre el tren lo deselecciona.</summary>
        void UpdateSeleccionTren()
        {
            if (_enBetera || _zoneNode < 0 || NetSession.A == null || _trainParts == null) return;
            var mouse = Mouse.current; if (mouse == null || _worldCam == null || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 mp = mouse.position.ReadValue(); bool dentro = false;
            foreach (var part in _trainParts)
            {
                if (part == null || !part.gameObject.activeInHierarchy) continue;
                foreach (var r in part.GetComponentsInChildren<MeshRenderer>())
                {
                    var b = r.bounds; Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
                    for (int c = 0; c < 8; c++) { var w = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z); var sp = ScreenOf(w); mn = Vector2.Min(mn, sp); mx = Vector2.Max(mx, sp); }
                    if (mp.x >= mn.x && mp.x <= mx.x && mp.y >= mn.y && mp.y <= mx.y) { dentro = true; break; }
                }
                if (dentro) break;
            }
            if (!dentro) return;
            int sv = TrenPreparar.TrenEn(NetSession.A, _zoneNode); if (sv < 0) return;
            var kb = Keyboard.current; bool quitar = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) && SeleccionGlobal.Tren == sv;
            SeleccionGlobal.NH90.Clear();
            if (quitar) { SeleccionGlobal.Tren = -1; SeleccionGlobal.TrenNodo = -1; } else { SeleccionGlobal.Tren = sv; SeleccionGlobal.TrenNodo = _zoneNode; }
            Debug.Log("[SELECCION] " + (quitar ? "tren deseleccionado" : SeleccionGlobal.Texto() + " en " + NetSession.A.Graph.Nodes[_zoneNode].Name));
        }

        /// <summary>Rectángulo en pantalla (píxeles, origen abajo) de los renderers de un objeto.</summary>
        bool PantallaDe(GameObject go, out Vector2 mn, out Vector2 mx)
        {
            mn = new Vector2(float.MaxValue, float.MaxValue); mx = new Vector2(float.MinValue, float.MinValue); bool hay = false;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.name == "Selección") continue; var b = r.bounds;
                for (int c = 0; c < 8; c++) { var w = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z); var sp = ScreenOf(w); if (sp.z < 0f) continue; mn = Vector2.Min(mn, sp); mx = Vector2.Max(mx, sp); hay = true; }
            }
            return hay;
        }

        IEnumerator SalirDeBetera()
        {
            _beteraSaliendo = true; ZonaBetera.Activa = false;
            Debug.Log("[BETERA] vuelvo al mapa" + (SeleccionGlobal.Vacia ? "" : " con " + SeleccionGlobal.Texto()));
            Sfx.Play("zoom_salir_mapa", Vector3.zero, 0.8f, 0.5f);
            if (_zoneNode >= 0) LeaveToMap();      // Iker (reloj único): con zona de debajo, por LeaveToMap, para que su noche vuelva a la red (SaveNightSession)
            else { yield return NetSession.CaptureTransition(); SceneManager.LoadScene("Union"); }      // Marcos (qa52, 117): sin zona, LeaveToMap no hace nada
        }

        void GuiBetera()
        {
            if (!_enBetera && SeleccionGlobal.Tren >= 0 && SeleccionGlobal.TrenNodo == _zoneNode)
            {
                var s2 = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18f * Screen.height / 1080f), alignment = TextAnchor.MiddleCenter }; s2.normal.textColor = new Color32(0xf0, 0xdc, 0x9c, 0xff);
                GUI.Label(new Rect(0f, Screen.height * 0.82f, Screen.width, 30f * Screen.height / 1080f), SeleccionGlobal.Texto() + " · aleja con la rueda y haz clic derecho en la estación de destino", s2);
            }
            if (!_enBetera) return;
            string t = SeleccionGlobal.Vacia ? "Bétera · clic en un NH90 para seleccionarlo" : SeleccionGlobal.Texto() + " · aleja con la rueda y haz clic derecho en el destino del mapa";
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18f * Screen.height / 1080f), alignment = TextAnchor.MiddleCenter }; st.normal.textColor = new Color32(0xf0, 0xdc, 0x9c, 0xff);
            GUI.Label(new Rect(0f, Screen.height * 0.82f, Screen.width, 30f * Screen.height / 1080f), t, st);
        }
    }
}
