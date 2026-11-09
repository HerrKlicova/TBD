using System.Collections.Generic;
using TBD.Net;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// D-N182 (Lucía, 9-nov) · Las hordas nacen fuera del alcance de la cámara y llegan andando por el rumbo de la Colmena. SOLO VISTA: no toca el
    /// sim, el campo ni el hash. Cada asedio anunciado (también la horda de la noche de la zona, aviso 19:00 → 22:00) se dibuja como una masa que sale,
    /// al saltar el aviso, a velocidad de marcha × (ataque − aviso) del nodo, como mucho 10 km, por el lado de la horda (el mismo rumbo que da el
    /// informe, LadoHorda), y avanza en línea recta hasta llegar al nodo justo a la hora del ataque. Al empezar el ataque desaparece.
    /// </summary>
    public sealed partial class OperationsMap3D
    {
        const double MasaVelMs = 1.5;          // velocidad de marcha: la de los zombis en la noche (NocheSim ZombieSpeedMmS = 1500)
        const double MasaKmMax = 10.0;          // Lucía: como mucho unos 10 km
        sealed class Masa { public Transform T; public double Ux, Uy, Km; public long Aviso, Ataque; public int Nodo; public bool Visto; }
        readonly Dictionary<long, Masa> _masas = new Dictionary<long, Masa>();
        readonly List<long> _masasFuera = new List<long>();
        Material _masaMat;

        /// <summary>Posición de la masa en unidades de mapa en el tick dado (para la vista y para comprobarlo en las pruebas).</summary>
        static void MasaPos(RailNode nd, double ux, double uy, double km, long aviso, long ataque, long tick, out double mx, out double my)
        {
            double r = ataque > aviso ? System.Math.Max(0.0, System.Math.Min(1.0, (ataque - tick) / (double)(ataque - aviso))) : 0.0;
            double u = km / MapGeo.KmPerUnit * r;
            mx = nd.XMeters + ux * u; my = nd.YMeters + uy * u;
        }

        void UpdateMasasEnMarcha()
        {
            var dir = _a.Director; long warn = NetConst.Hours(dir.P.SiegeWarnH);
            foreach (var m in _masas.Values) m.Visto = false;
            foreach (var p in dir.Active)
            {
                if (p.Kind != HiveDirector.PlanKind.Asedio || p.Started || _a.Tick >= p.StartTick || p.Node < 0 || p.Node >= _a.Graph.Nodes.Count) continue;
                long aviso = p.StartTick - warn; if (_a.Tick < aviso) continue;
                long key = ((long)p.Node << 40) ^ p.StartTick;
                var nd = _a.Graph.Nodes[p.Node];
                if (!_masas.TryGetValue(key, out var ms))
                {
                    if (!LadoHorda(nd, out var ux, out var uy)) { ux = 0; uy = -1; }      // sin horda alrededor: viene del norte (no debería pasar con un asedio anunciado)
                    double horas = (p.StartTick - aviso) / (double)NetConst.HourTicks;
                    double km = System.Math.Min(MasaKmMax, MasaVelMs * 3.6 * horas);
                    if (_masaMat == null) { _masaMat = new Material(_lineMat); _masaMat.SetColor("_BaseColor", new Color32(0x1c, 0x20, 0x0c, 0xff)); }
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = "Masa en marcha " + nd.Name; Destroy(go.GetComponent<Collider>());
                    go.GetComponent<Renderer>().sharedMaterial = _masaMat;
                    int n = HiveDirector.IsZoneSiege(p) ? p.Moved * NetWorld.ZombiesPerPermil : _a.SiegeZombies(p.Node);
                    float s = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1, n) / 2000f), 0.8f, 2.5f) * 1.4f;      // km de diámetro, según la cifra
                    go.transform.localScale = new Vector3(s, 0.05f, s);
                    ms = new Masa { T = go.transform, Ux = ux, Uy = uy, Km = km, Aviso = aviso, Ataque = p.StartTick, Nodo = p.Node };
                    _masas[key] = ms;
                    Debug.Log($"[MAPA] D-N182 masa en marcha hacia {nd.Name}: {km:0.0} km, rumbo {Rumbo(-ux, -uy)}, {n} zombis, llega a las {NetConst.Clock(p.StartTick)}");
                }
                ms.Visto = true;
                MasaPos(nd, ms.Ux, ms.Uy, ms.Km, ms.Aviso, ms.Ataque, _a.Tick, out double mx, out double my);
                ms.T.position = MapToWorld(mx, my, 0.25f);
            }
            _masasFuera.Clear();
            foreach (var kv in _masas) if (!kv.Value.Visto) _masasFuera.Add(kv.Key);
            foreach (var k in _masasFuera) { if (_masas[k].T != null) Destroy(_masas[k].T.gameObject); _masas.Remove(k); }
        }
    }
}
