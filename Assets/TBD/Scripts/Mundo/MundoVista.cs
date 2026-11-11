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
        readonly StreamWriter _hash; readonly List<Orden> _reproducir; readonly string[] _refHash; int _ri; public string Desync;
        readonly bool _grabar; readonly string _dirCap; bool _sinRef;
        public int Peloton => M.Unidades.Platoons.Count > 0 ? M.Unidades.Platoons[0].Id : -1;

        public MundoVista(uint semilla, IList<Casa> casas, IList<(int x, int y)> zombis, int pelX, int pelY, int pelRumbo)
        {
            M = new Mundo(semilla, 2048000, 2048000); M.PonerCasas(casas);
            foreach (var z in zombis) M.Zombis.Anadir(z.x, z.y, z.x, z.y);
            M.Unidades.AddPlatoon(PlatoonKind.Fusileros, pelX, pelY, pelRumbo);
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
                _reproducir = Mundo.OrdenesDe(File.ReadAllText(ca[i + 1]), out _, out _);
                var refp = Path.Combine(Path.GetDirectoryName(ca[i + 1]) ?? ".", "mundo_hash_ref.txt"); if (File.Exists(refp)) _refHash = File.ReadAllLines(refp); else { _sinRef = true; Debug.Log("[MUNDO] desync=SIN_REFERENCIA: no hay " + refp); }
                Debug.Log($"[MUNDO] reproduzco {_reproducir.Count} órdenes de {ca[i + 1]}" + (_refHash != null ? $" y comparo con {_refHash.Length} líneas de hash" : ""));
            }
            Debug.Log($"[MUNDO] simulación de Nacho: {M.Zombis.Lista.Count} zombis, {casas.Count} casas, pelotón {Peloton}");
        }

        /// <summary>Avanza la simulación a 30 ticks por segundo real (como mucho 8 por fotograma).</summary>
        public void Avanzar(float dt)
        {
            _acc += dt; int n = 0;
            while (_acc >= Dt && n < 8)
            {
                if (_reproducir != null) while (_ri < _reproducir.Count && _reproducir[_ri].Tick <= M.Tick) M.Encolar(_reproducir[_ri++]);
                M.Step(); _acc -= Dt; n++;
                if (_hash != null || _refHash != null)
                {
                    var l = M.LineaHash(); _hash?.WriteLine(l);
                    if (_refHash != null && Desync == null && M.Tick - 1 < _refHash.Length) { var d = Mundo.CompararLineas(_refHash[M.Tick - 1], l); if (d != null) { Desync = d; Debug.Log("[MUNDO] " + d); } }
                }
            }
            if (n == 8) _acc = 0f;
        }

        public void Mover(int xMm, int yMm) { if (Peloton < 0 || _reproducir != null) return; M.Encolar(new Orden { Tick = M.Tick + 1, Jugador = 0, Tipo = TipoOrden.Mover, A = Peloton, B = xMm, C = yMm, D = -1 }); }

        public float Alpha => Mathf.Clamp01(_acc / Dt);

        public void Cerrar()
        {
            _hash?.Flush(); _hash?.Dispose();
            if (_grabar) { File.WriteAllText(Path.Combine(_dirCap, "mundo_grabacion.txt"), M.GrabacionTexto(Mundo.EscenarioCaserio)); Debug.Log("[MUNDO] grabación guardada: " + Path.Combine(_dirCap, "mundo_grabacion.txt")); }
            Debug.Log($"[MUNDO] fin: tick {M.Tick}, abatidos {M.Abatidos}, bajas {M.BajasPropias}, hash {M.Hash():x16}" + (Desync != null ? " · " + Desync : _refHash != null ? " · desync=False" : _sinRef ? " · desync=SIN_REFERENCIA" : ""));
        }
    }
}
