using System.Collections.Generic;
using TBD.Net;
using TBD.Units;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// Partida de 30 min (Lucía, plan-partida-30min.md; red de Nacho, NetWorld.LastAmbush): cuando la red dispara la emboscada en el nodo de esta zona con el jugador
    /// dentro (OnScreen), la zona crea los zombis (unos 300) en su entrada de más peso, que está fuera de la vista (D-N182). Una vez por emboscada (Tick).
    /// Fuera de pantalla la resuelve la red. Con InApartado, la zona repite el aviso de la D-N177.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        long _emboscadaVista = -1;

        void EmboscadaEnZona(List<SpawnPoint> spawns)
        {
            var w = NetSession.A; if (w == null || _ng == null) return;
            var e = w.LastAmbush;
            if (e.Tick < 0 || e.Tick == _emboscadaVista || !e.OnScreen || e.Node != _zoneNode) return;
            _emboscadaVista = e.Tick;
            var en = _ng.P.Entries; if (en == null || en.Length == 0) { Debug.LogWarning("[EMBOSCADA] la zona no tiene entradas: no se crean zombis"); return; }
            int best = 0; for (int i = 1; i < en.Length; i++) if (en[i].Weight > en[best].Weight) best = i;
            var r = en[best]; int n = Mathf.Max(1, e.Zombies);
            for (int i = 0; i < n; i++)
            {
                uint h = (uint)(i * 2654435761u) ^ (uint)e.Tick;                  // reparto fijo por índice y tick (sin azar de Unity: los dos mundos ven lo mismo)
                int x = r.X0 + (int)(h % (uint)Mathf.Max(1, r.X1 - r.X0)), y = r.Y0 + (int)((h / 7919u) % (uint)Mathf.Max(1, r.Y1 - r.Y0));
                spawns.Add(new SpawnPoint { Wave = 0, Entry = best, X = x, Y = y, Front = true });
            }
            string tren = e.Service >= 0 && e.Service < w.Services.Count ? w.Services[e.Service].Train.Name : "tren";
            Debug.Log($"[EMBOSCADA] zona {w.Graph.Nodes[e.Node].Name}: {n} zombis por {r.Name} contra el {tren}{(e.InApartado ? " (en el apartado)" : "")}");
            HintMsg($"Emboscada en {w.Graph.Nodes[e.Node].Name}: unos {n} zombis atacan el {tren}." + (e.InApartado ? " " + Puertas.TextoApartado : ""), true);   // texto de EM-1 (Lucía, a8b49fd)
        }
    }
}
