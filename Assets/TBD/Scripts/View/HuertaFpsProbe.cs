using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TBD.View
{
    /// <summary>
    /// Prueba de FPS de la huerta (Teo, 7-nov). Solo se activa con TBD.exe -huertafps; sin esa bandera no hace nada.
    /// Pasa del menú al mapa y del mapa a la zona de Sagunto (como -stress10k-sagunto, sin la noche), planta los naranjos de prueba,
    /// suelta la horda en la huerta del este, espera 5 s y mide 40 s. Escribe Captures/huerta_fps_ETIQUETA.txt y cierra el juego.
    ///
    /// Banderas (todas opcionales salvo -huertafps):
    ///   -huertaradio R    naranjos de la zona 5 a menos de R m de la zona simulada (0 = solo dentro, 342 árboles; 75 = ~2.100; 150 = ~4.900; 9999 = todo el mapa, ~10.100)
    ///   -huertasin        no planta nada (medida base con los naranjos que ya hay)
    ///   -huertahorda N    zombis que se sueltan en la huerta del este (10000 por defecto; 0 = sin horda añadida)
    ///   -ortho O          zoom de la cámara (26 por defecto, el del juego; 70 = el máximo)
    ///   -huertatag T      etiqueta del archivo de resultados (por defecto r{R}_h{N}_o{O})
    ///   -huertashot       guarda una captura a los 30 s de medida
    /// </summary>
    public sealed class HuertaFpsProbe : MonoBehaviour
    {
        static readonly string[] Args = System.Environment.GetCommandLineArgs();
        static bool Flag(string n) { return System.Array.IndexOf(Args, n) >= 0; }
        static float Num(string n, float def)
        {
            for (int i = 0; i + 1 < Args.Length; i++)
                if (Args[i] == n && float.TryParse(Args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) return v;
            return def;
        }
        static string Str(string n, string def)
        {
            for (int i = 0; i + 1 < Args.Length; i++) if (Args[i] == n) return Args[i + 1];
            return def;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Flag("-huertafps")) return;
            if (FindFirstObjectByType<HuertaFpsProbe>() != null) return;
            var go = new GameObject("TBD_HuertaFpsProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<HuertaFpsProbe>();
        }

        // huerta del este de la zona simulada (mapa de zonas: X 184-255, Z 68-127)
        static readonly Vector3 Foco = new Vector3(218f, 0f, 97f);
        const float HX0 = 186f, HZ0 = 72f, HW = 66f, HH = 52f;
        const float Settle = 5f, Measure = 40f;

        int _state;                 // 0 camino a la zona, 1 esperando la zona, 2 asentando, 3 midiendo
        bool _toUnion, _entered;
        float _sceneT0, _t0, _nextSec;
        int _secFrames;
        string _scene = "";
        readonly List<float> _fps = new List<float>();
        readonly List<float> _dt = new List<float>();
        readonly List<string> _spikes = new List<string>();
        readonly StringBuilder _info = new StringBuilder();
        PrototypeBootstrap _pb;
        bool _shot;

        void Update()
        {
            var sc = SceneManager.GetActiveScene().name;
            if (sc != _scene) { _scene = sc; _sceneT0 = Time.realtimeSinceStartup; }
            float ts = Time.realtimeSinceStartup - _sceneT0;
            switch (_state)
            {
                case 0:
                    if (sc == "SampleScene") { _state = 1; break; }
                    if (sc == "Union")
                    {
                        if (_entered || ts < 1f) break;
                        var map = FindFirstObjectByType<OperationsMap3D>();
                        if (map != null) { _entered = true; map.EnterZone(0); }        // nodo 0 = Sagunto, como -stress10k-sagunto
                        break;
                    }
                    if (!_toUnion && ts > 1f) { _toUnion = true; NightRun.Active = false; SceneManager.LoadScene("Union"); }   // desde el menú, sin la noche
                    break;
                case 1:
                    if (_pb == null) _pb = FindFirstObjectByType<PrototypeBootstrap>();
                    if (_pb == null || !_pb.HuertaListo() || ts < 4f) break;           // 4 s para que el suelo por trozos cargue alrededor
                    Preparar();
                    _state = 2; _t0 = Time.realtimeSinceStartup;
                    break;
                case 2:
                    if (Time.realtimeSinceStartup - _t0 < Settle) break;
                    _state = 3; _t0 = Time.realtimeSinceStartup; _nextSec = _t0 + 1f; _secFrames = 0;
                    break;
                case 3:
                    Medir();
                    break;
            }
        }

        void Preparar()
        {
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;          // sin tope, como -stress10k
            float radio = Num("-huertaradio", 0f), ortho = Num("-ortho", 26f);
            int horda = (int)Num("-huertahorda", 10000f);
            _info.AppendLine("Prueba de FPS de la huerta (HuertaFpsProbe, Teo 7-nov)");
            _info.AppendLine("argumentos: " + string.Join(" ", Args, 1, Mathf.Max(0, Args.Length - 1)));
            int previos = _pb.HuertaNaranjosPrevios();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (Flag("-huertasin")) _info.AppendLine("naranjos de prueba: ninguno (-huertasin)");
            else
            {
                string err = _pb.HuertaPlantar(radio, 5.5f, 5f, out int arboles, out long tris, out int rend);
                if (err != null) _info.AppendLine("ERROR al plantar: " + err);
                _info.AppendLine($"naranjos de prueba: {arboles} (radio {radio:0} m, 5,5 x 5 m al tresbolillo) · triángulos {tris} · MeshRenderer {rend} · plantados en {sw.ElapsedMilliseconds} ms");
            }
            _info.AppendLine($"naranjos que ya había: {previos}");
            int antes = _pb.HuertaZombis();
            _pb.HuertaHorda(horda, HX0, HZ0, HW, HH);
            _pb.HuertaCamara(Foco, ortho);
            _info.AppendLine($"zombis: {antes} de la zona + {horda} en la huerta del este (X {HX0:0}-{HX0 + HW:0}, Z {HZ0:0}-{HZ0 + HH:0})");
            _info.AppendLine($"cámara: foco ({Foco.x:0}, {Foco.z:0}), ortho {ortho:0}");
        }

        void Medir()
        {
            float now = Time.realtimeSinceStartup, tt = now - _t0, dtm = Time.unscaledDeltaTime * 1000f;
            _secFrames++; _dt.Add(dtm);
            if (dtm > 33f && _spikes.Count < 12) _spikes.Add($"{tt:0.0}s={dtm:0}ms");
            if (now < _nextSec) return;
            _fps.Add(_secFrames); _secFrames = 0; _nextSec += 1f;
            if (!_shot && _fps.Count >= 30 && Flag("-huertashot")) { _shot = true; _pb.HuertaCaptura(); }
            if (_fps.Count < Measure) return;
            var a = _fps.ToArray(); System.Array.Sort(a); float sum = 0f; foreach (var f in a) sum += f;
            var d = _dt.ToArray(); System.Array.Sort(d);
            float med = a.Length % 2 == 1 ? a[a.Length / 2] : 0.5f * (a[a.Length / 2 - 1] + a[a.Length / 2]);
            _info.AppendLine($"zombis al final: {_pb.HuertaZombis()}");
            _info.AppendLine($"FPS mediana {med:0.0} · media {sum / a.Length:0.0} · mínimo {a[0]:0} · p5 {a[2]:0} · máximo {a[a.Length - 1]:0}  ({a.Length} s)");
            _info.AppendLine($"fotograma p99 {d[Mathf.Min(d.Length - 1, (int)(d.Length * 0.99f))]:0.00} ms · peor {d[d.Length - 1]:0.00} ms");
            _info.AppendLine("picos > 33 ms: " + (_spikes.Count == 0 ? "ninguno" : string.Join(", ", _spikes)));
            _info.AppendLine($"resolución {Screen.width}x{Screen.height} · factor de pixelado 1/{_pb.HuertaFactor()}");
            _info.AppendLine(med >= 290f ? "veredicto: PASA (mediana ≥ 290 FPS, el suelo de Sara)" : "veredicto: NO PASA (mediana < 290 FPS)");
            string tag = Str("-huertatag", $"r{Num("-huertaradio", 0f):0}_h{(int)Num("-huertahorda", 10000f)}_o{Num("-ortho", 26f):0}" + (Flag("-huertasin") ? "_base" : ""));
            Directory.CreateDirectory(PrototypeBootstrap.HuertaCapturas);
            string path = Path.Combine(PrototypeBootstrap.HuertaCapturas, "huerta_fps_" + tag + ".txt");
            File.WriteAllText(path, _info.ToString());
            Debug.Log("[HUERTA-FPS] " + path + "\n" + _info);
            Application.Quit();
            enabled = false;
        }
    }
}
