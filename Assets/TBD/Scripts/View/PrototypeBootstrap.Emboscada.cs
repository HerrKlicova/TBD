using System.Collections.Generic;
using TBD.Net;
using TBD.Sim;
using TBD.Units;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Partida de 30 min, emboscada de Lucía (e5f2f1c) con la red de Nacho (3544f914): el segundo tren que llega a Nules el día 2 queda a las puertas y la red rellena
    /// NetWorld.LastAmbush. Con el jugador en la zona (OnScreen), la zona crea los 300 en la franja oeste (AmbushSpawn*, X 0–20 m, Z 30–70 m), repartidos en
    /// AmbushArrivalS (30 s), y van a por el tren (X 20–84 m sobre la vía). Cuando se acaba la horda llama a w.AmbushEnd(trenPerdido); si no, la red suelta el tren
    /// a los 10 min (HoldUntil). Fuera de pantalla lo resuelve la red. Una vez por emboscada (Tick).
    /// </summary>
    public partial class PrototypeBootstrap
    {
        long _emboscadaVista = -1; bool _trenPerdidoAvisado; float _trenSolidoX = float.NaN; static readonly int _rectTrenSagunto = NightParams.Default().TrainRect.X1;
        /// <summary>El sólido ECS del tren (muro mientras está parado) va donde lo pone la noche: en Nules A, fuera de la puerta este (TrainRect de nules.json), no en el andén
        /// de Sagunto. Sin tren (perdido), el sólido sale del mapa. Los dos mundos igual (A y B).</summary>
        void SolidoDelTren()
        {
            if (_ng == null) return; if (_ng.P.TrainRect.X1 == _rectTrenSagunto) return;   // Sagunto: el sólido de siempre (no coincide con el rectángulo por defecto y no se toca)
            float x = _ng.TrainHp > 0 ? _ng.P.TrainRect.X1 / 1000f - Level.TrainFront : 900f;
            if (x == _trenSolidoX) return;
            foreach (var wld in new[] { _worldA, _worldB })
            {
                if (wld == null || !wld.IsCreated) continue; var em = wld.EntityManager; var root = wld == _worldA ? _rootA : _rootB;
                var t = em.GetComponentData<TrainState>(root); if (t.Speed > 0f) return;      // en marcha no se toca
                if (Mathf.Abs(t.X - x) < 0.5f) continue; t.X = x; em.SetComponentData(root, t); var zi = em.GetComponentData<ZoneInfo>(root); zi.TrainWallDirty = true; em.SetComponentData(root, zi);
            }
            if (!float.IsNaN(_trenSolidoX) || Mathf.Abs(x - 136f) > 1f) Debug.Log($"[NOCHE] sólido del tren en X {x - Level.TrainRear:0.0}–{x + Level.TrainFront:0.0}");
            _trenSolidoX = x;
        }
        int _embPendientes, _embTicks, _embBase = -1, _embIdx; bool _embEnCurso;

        void EmboscadaEnZona(List<SpawnPoint> spawns)
        {
            if (TrenPanel.Dia2Volver && _nightInit && Time.timeSinceLevelLoad > 4f) { TrenPanel.Dia2Volver = false; Debug.Log("[DIA2] sesión de zona creada; vuelvo al mapa"); LeaveToMap(); return; }   // -dia2-auto
            var w = NetSession.A; if (w == null || _ng == null) return;
            SolidoDelTren();
            if (_ng.P.TrainNotDefeat && _ng.TrainHp <= 0 && !_trenPerdidoAvisado)      // Lucía (da2d050): en Nules perder el tren no es derrota; aviso y se sigue
            {
                _trenPerdidoAvisado = true; string tn = "tren"; int si = -1; for (int k = 0; k < w.Services.Count; k++) { var sv = w.Services[k]; if (sv.AtStop && sv.Stops.Count > 0 && sv.Stops[sv.StopIndex].Node == _zoneNode) { tn = sv.Train.Name; si = k; break; } }
                if (si >= 0) NetSession.Push(NetCmdType.TrainLost, si, 0);   // Nacho (49e7ddf1): la red quita el tren, su carga y su tropa
                HintMsg($"Se ha perdido el {tn} con su carga.", false); Debug.Log($"[NOCHE] se ha perdido el {tn} con su carga (la noche sigue)");
            }
            var e = w.LastAmbush;
            if (e.Tick >= 0 && e.Tick != _emboscadaVista && e.OnScreen && e.Node == _zoneNode)
            {
                _emboscadaVista = e.Tick; _embEnCurso = true; _embPendientes = Mathf.Max(1, e.Zombies); _embTicks = NetWorld.AmbushArrivalS * IMath.TickRate; _embIdx = 0;
                _embBase = _zomPos.Count;
                string tren = e.Service >= 0 && e.Service < w.Services.Count ? w.Services[e.Service].Train.Name : "tren";
                Debug.Log($"[EMBOSCADA] zona {w.Graph.Nodes[e.Node].Name}: {_embPendientes} zombis en {NetWorld.AmbushArrivalS} s por la franja oeste contra el {tren}");
                HintMsg($"Emboscada en {w.Graph.Nodes[e.Node].Name}: unos {_embPendientes} zombis atacan el {tren} fuera de la puerta este.", true);   // texto de EM-1 (Lucía, a8b49fd)
            }
            if (!_embEnCurso) return;
            if (_embPendientes > 0)
            {
                int n = _embTicks > 1 ? Mathf.CeilToInt(_embPendientes / (float)_embTicks) : _embPendientes; _embTicks = Mathf.Max(1, _embTicks - 1);
                for (int i = 0; i < n && _embPendientes > 0; i++, _embPendientes--, _embIdx++)
                {
                    uint h = (uint)(_embIdx * 2654435761u) ^ (uint)_emboscadaVista;           // reparto fijo (sin azar de Unity: los dos mundos ven lo mismo)
                    int x = NetWorld.AmbushSpawnX0Mm + (int)(h % (uint)(NetWorld.AmbushSpawnX1Mm - NetWorld.AmbushSpawnX0Mm));
                    int y = NetWorld.AmbushSpawnZ0Mm + (int)((h / 7919u) % (uint)(NetWorld.AmbushSpawnZ1Mm - NetWorld.AmbushSpawnZ0Mm));
                    spawns.Add(new SpawnPoint { Wave = 0, Entry = 0, X = x, Y = y, Front = true });
                }
                return;
            }
            // acabada la llegada: termina cuando la horda vuelve a lo que había antes (o el tren cae)
            bool perdido = _ng.TrainHp <= 0;
            if (perdido || (_zomCounted && _zomPos.Count <= _embBase + 5))
            {
                _embEnCurso = false; w.AmbushEnd(perdido);
                Debug.Log($"[EMBOSCADA] fin en la zona: {(perdido ? "tren perdido" : "rechazada")} (zombis {_zomPos.Count})");
                HintMsg(perdido ? "El tren ha caído en la emboscada." : "Emboscada rechazada: el tren entra.", perdido);
            }
        }
    }
}
