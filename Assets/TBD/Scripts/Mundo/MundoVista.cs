using System.Collections.Generic;
using System.IO;
using TBD.Net;
using TBD.Units;
using UnityEngine;

namespace TBD.Mundo
{
    /// <summary>
    /// Hito 1 · enganche de la vista con la simulación única de Nacho (TBD.Mundo.Mundo, tick 30/s, mm enteros). La escena solo LEE Mundo.Unidades y
    /// Mundo.Zombis, interpola para dibujar y mete órdenes con Mundo.Encolar: no simula nada (cooperativo en lockstep).
    /// QA (Sara): -hashlog escribe Mundo.LineaHash() cada tick en Captures/mundo_hash.txt; -grabar guarda las órdenes al salir en Captures/mundo_grabacion.txt;
    /// -reproducir FICHERO las mete en sus ticks y compara cada LineaHash con el -hashlog de referencia si existe (Mundo.CompararLineas).
    /// </summary>
    public sealed class MundoVista
    {
        public readonly Mundo M; readonly float _escala = 0.001f; float _acc; public const float Dt = 1f / 30f;
        readonly StreamWriter _hash; readonly List<Orden> _reproducir; readonly string[] _refHash; int _ri, _comparados; public string Desync;
        readonly bool _grabar; readonly string _dirCap; bool _sinRef;
        /// <summary>Hay una grabación en reproducción: la vista no debe meter órdenes propias (RedVista, clic derecho).</summary>
        public static bool Reproduciendo;
        public int Peloton => M.Unidades.Platoons.Count > 0 ? M.Unidades.Platoons[0].Id : -1;

        /// <summary>Crea el Mundo del Hito 1 con Mundo.Caserio (el único lector de los CSV de Álvaro: una sola verdad para los dos jugadores).</summary>
        public MundoVista(uint semilla)
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "Hito1");
            Mundo.Lector = f => { var p = Path.Combine(dir, f); return File.Exists(p) ? File.ReadAllText(p) : null; };
            M = Mundo.Caserio(semilla); M.Log = Debug.Log;      // Nacho: [RECARGA] y demás, fuera del hash
            var casas = M.Casas;
            var ca = System.Environment.GetCommandLineArgs(); _dirCap = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Captures"); Directory.CreateDirectory(_dirCap);
            int ih = System.Array.IndexOf(ca, "-hashlog");
            if (ih >= 0)      // Sara: ruta opcional (dos instancias en la misma carpeta no deben escribir el mismo fichero)
            {
                string ruta = ih + 1 < ca.Length && !ca[ih + 1].StartsWith("-") ? ca[ih + 1] : Path.Combine(_dirCap, "mundo_hash.txt");
                var d = Path.GetDirectoryName(Path.GetFullPath(ruta)); if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                _hash = new StreamWriter(ruta, false); Debug.Log("[MUNDO] -hashlog → " + Path.GetFullPath(ruta));
            }
            _grabar = System.Array.IndexOf(ca, "-grabar") >= 0;
            for (int i = 0; i + 1 < ca.Length; i++) if (ca[i] == "-reproducir" && File.Exists(ca[i + 1]))
            {
                _reproducir = Mundo.OrdenesDe(File.ReadAllText(ca[i + 1]), out _, out _); Reproduciendo = true;
                var refp = Path.Combine(Path.GetDirectoryName(ca[i + 1]) ?? ".", "mundo_hash_ref.txt"); if (File.Exists(refp)) _refHash = File.ReadAllLines(refp); else { _sinRef = true; Debug.Log("[MUNDO] desync=SIN_REFERENCIA: no hay " + refp); }
                if (_refHash != null) foreach (var r in _refHash) { var t = r.Split(';')[0]; if (int.TryParse(t, out var tk)) _refPorTick[tk] = r; }
                Debug.Log($"[MUNDO] reproduzco {_reproducir.Count} órdenes de {ca[i + 1]}" + (_refHash != null ? $" y comparo con {_refHash.Length} líneas de hash" : ""));
            }
            Debug.Log($"[MUNDO] simulación de Nacho: {M.Zombis.Lista.Count} zombis, {casas.Count} casas, pelotón {Peloton}");
            for (int i = 0; i < ca.Length; i++) { if (ca[i] == "-guion" && _reproducir == null) _guion = Guion(); if (ca[i] == "-turbo" && i + 1 < ca.Length) int.TryParse(ca[i + 1], out _turbo); if (ca[i] == "-salir-en" && i + 1 < ca.Length) int.TryParse(ca[i + 1], out _salirEn); }
            if (_guion != null) Debug.Log($"[MUNDO] -guion: {_guion.Count} órdenes por guion (prueba exigente de Sara)");
            LineaInicial();      // tick 0
        }

        /// <summary>Avanza la simulación a 30 ticks por segundo real (como mucho 8 por fotograma).</summary>
        public void Avanzar(float dt)
        {
            _acc += dt * System.Math.Max(1, _turbo); int n = 0;
            while (_acc >= Dt && n < 8 * System.Math.Max(1, _turbo))
            {
                if (_guion != null) while (_gi < _guion.Count && _guion[_gi].Tick <= M.Tick) M.Encolar(_guion[_gi++]);
                if (_salirEn > 0 && M.Tick >= _salirEn) { Cerrar(); _salirEn = 0; Application.Quit(); return; }
                if (_reproducir != null) while (_ri < _reproducir.Count && _reproducir[_ri].Tick <= M.Tick) M.Encolar(_reproducir[_ri++]);
                M.Step(); _acc -= Dt; n++;
                if (_hash != null || _refHash != null)
                {
                    var l = M.LineaHash(); _hash?.WriteLine(l);
                    Comparar(l);
                }
            }
            if (n >= 8 * System.Math.Max(1, _turbo)) _acc = 0f;
        }



        List<Orden> _guion; int _gi, _turbo = 1, _salirEn; bool _cerrado;
        /// <summary>Guion de la prueba exigente (bateria-hash.md, H1): vía A–B con curva y un trazado inválido, línea A→B con el tren, el pelotón en varias órdenes
        /// hasta el caserío (rodeando casas), combate, retirada a B, rearme y vuelta, segundo tren, formación y alto el fuego. Ticks y mm fijos: los dos jugadores iguales.</summary>
        List<Orden> Guion()
        {
            var g = new List<Orden>(); int pel = Peloton;
            void O(int t, TipoOrden tipo, int a = 0, int b = 0, int c = 0, int d = 0) { g.Add(new Orden { Tick = t, Jugador = 0, Tipo = tipo, A = a, B = b, C = c, D = d }); }
            // vía en dos tramos (Iker): A → P (tramo 0) y P → B (tramo 1)
            O(30, OrdenesRed.PuntoVia, 1060000, 430000); O(31, OrdenesRed.PuntoVia, 1110000, 700000); O(32, OrdenesRed.PuntoVia, 1150000, 900000); O(33, OrdenesRed.TrazarVia);
            O(40, OrdenesRed.PuntoVia, 1150000, 900000); O(41, OrdenesRed.PuntoVia, 1240000, 1300000); O(42, OrdenesRed.PuntoVia, 1300000, 1640000); O(43, OrdenesRed.TrazarVia);
            O(60, OrdenesRed.PuntoVia, 1060000, 430000); O(61, OrdenesRed.PuntoVia, 1950000, 900000); O(62, OrdenesRed.TrazarVia);          // por el mar: tiene que rechazarse
            O(90, OrdenesRed.NuevaLinea, 0, 1); O(95, OrdenesRed.AsignarTren, 0, 0);
            // borrados (106) y parada-depósito (107): con el tren encima se rechaza; por delante se acepta y el tren para en el nodo y busca otra ruta
            O(2300, (TipoOrden)106, 0); O(2410, (TipoOrden)106, 1); O(2500, (TipoOrden)107, 0, 1, -1, 0);
            O(2700, OrdenesRed.PuntoVia, 1150000, 900000); O(2701, OrdenesRed.PuntoVia, 1240000, 1300000); O(2702, OrdenesRed.PuntoVia, 1300000, 1640000); O(2703, OrdenesRed.TrazarVia);
            O(600, TipoOrden.Mover, pel, 1330000, 1560000, -1); O(1500, TipoOrden.Mover, pel, 1380000, 1520000, -1); O(2400, TipoOrden.Mover, pel, 1500000, 1470000, -1);   // la última, al otro lado del caserío: rodea casas
            O(3300, TipoOrden.AtacarMoviendo, pel, 1440000, 1480000, -1);
            O(7500, TipoOrden.Mover, pel, 1295000, 1595000, -1);              // retirada a B (rearme en el almacén de B)
            O(10500, TipoOrden.AtacarMoviendo, pel, 1440000, 1480000, -1);    // vuelta a limpiar
            O(12000, TipoOrden.Formacion, pel, (int)Formation.Linea); O(12600, TipoOrden.FuegoLibre, pel, 0); O(13200, TipoOrden.FuegoLibre, pel, 1);
            O(13500, OrdenesRed.NuevoTren, 0); O(13600, OrdenesRed.AsignarTren, 0, 1);
            O(15000, TipoOrden.Mover, pel, 1420000, 1440000, -1); O(16500, TipoOrden.Parar, pel);
            g.Sort((x, y) => x.Tick.CompareTo(y.Tick));      // ticks todos distintos: orden fijo
            return g;
        }

        readonly Dictionary<int, string> _refPorTick = new Dictionary<int, string>();
        /// <summary>Compara la línea de este tick con la de la referencia del mismo tick (por el número de tick de la línea, no por posición).</summary>
        void Comparar(string l)
        {
            if (_refHash == null || Desync != null) return;
            if (!_refPorTick.TryGetValue(M.Tick, out var r)) return;
            _comparados++; var d = Mundo.CompararLineas(r, l); if (d != null) { Desync = d; Debug.Log("[MUNDO] " + d); }
        }

        /// <summary>Sara: la línea del tick 0 (estado inicial) antes del primer Step.</summary>
        public void LineaInicial() { if (_hash == null && _refHash == null) return; var l = M.LineaHash(); _hash?.WriteLine(l); Comparar(l); }

        public void Mover(int xMm, int yMm) { if (Peloton < 0 || _reproducir != null) return; M.Encolar(new Orden { Tick = M.Tick + 1, Jugador = 0, Tipo = TipoOrden.Mover, A = Peloton, B = xMm, C = yMm, D = -1 }); }

        public float Alpha => Mathf.Clamp01(_acc / Dt);

        public void Cerrar()
        {
            if (_cerrado) return; _cerrado = true;
            _hash?.Flush(); _hash?.Dispose();
            if (_grabar) { File.WriteAllText(Path.Combine(_dirCap, "mundo_grabacion.txt"), M.GrabacionTexto(Mundo.EscenarioCaserio)); Debug.Log("[MUNDO] grabación guardada: " + Path.Combine(_dirCap, "mundo_grabacion.txt")); }
            Debug.Log($"[MUNDO] fin: tick {M.Tick}, abatidos {M.Abatidos}, bajas {M.BajasPropias}, hash {M.Hash():x16}" + (Desync != null ? " · " + Desync + $" · {_comparados} ticks comparados" : _refHash != null ? (_comparados > 0 ? $" · desync=False · {_comparados} ticks comparados" : " · desync=SIN_COMPARAR · 0 ticks comparados") : _sinRef ? " · desync=SIN_REFERENCIA" : ""));
        }
    }
}
