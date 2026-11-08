using System.Collections.Generic;
using System.Text;
using TBD.Units;
using UnityEngine;

namespace TBD.View
{
    /// <summary>
    /// [NOCHE-ARMAS] (Sara y Hugo, docs/qa/abatidos-por-u-build.md; aprobado por Lucía). Bruno, 8-nov.
    /// Una línea de log al final de la noche con abatidos por u, separando pelotones, vehículos y almacén. SOLO VISTA: no toca la
    /// simulación ni el hash. Lee lo que ya existe (Platoon.AmmoSpent, TargetHit.Platoon / Weapon, StockShots, Veh.Ammo) y cuenta en la vista
    /// lo que la simulación no separa: a quién se atribuye cada abatido, si estaba en la pila, y las recargas de cada pelotón y vehículo.
    ///   (1) pelotones: tipo, u disparadas, abatidos por arma, abatidos en la pila (blanco a ≤ 1,5 m de un muro de las obras al morir), recargas, u recargadas
    ///   (2) vehículos: disparos (salvas), abatidos (de ellos, atropellados), recargas, u recargadas
    ///   (3) almacén: u al empezar y al acabar, u entregadas a pelotones y a vehículos; dotación a bordo (pelotones + vehículos) al empezar y al acabar
    /// Ojo: municion_gastada_u de la línea [NOCHE] resultado sigue contando solo la infantería; aquí va todo separado.
    /// </summary>
    public partial class PrototypeBootstrap
    {
        sealed class NaPel { public int Kills, KillsPila, Resup; public long Spent0, ResupShots; public readonly Dictionary<WeaponId, int> PorArma = new Dictionary<WeaponId, int>(); }
        sealed class NaVeh { public int Disparos, Kills, KillsPila, Atropellos, Resup; public float ResupU; }

        readonly Dictionary<int, NaPel> _naPel = new Dictionary<int, NaPel>();
        readonly Dictionary<Veh, NaVeh> _naVeh = new Dictionary<Veh, NaVeh>();
        readonly List<Veh> _naVehHitOwner = new List<Veh>();          // dueño de cada entrada de _vehHits (mismo índice)
        readonly List<bool> _naVehHitAtropello = new List<bool>();
        readonly Dictionary<int, long> _naPre = new Dictionary<int, long>();
        long _naStock0; float _naDot0; bool _naOn;

        NaPel NaP(int id) { if (!_naPel.TryGetValue(id, out var p)) _naPel[id] = p = new NaPel(); return p; }
        NaVeh NaV(Veh v) { if (!_naVeh.TryGetValue(v, out var n)) _naVeh[v] = n = new NaVeh(); return n; }

        /// <summary>Dotación de un pelotón en disparos equivalentes: su munición más los C90 que lleva (cada uno vale lo que cuesta reponerlo).</summary>
        static long NaDotacion(Platoon p) => p.AmmoShots + (long)p.C90Left * System.Math.Max(0, WeaponCatalog.Table[(int)WeaponId.C90].CostShots);

        float NaVehCapU(Veh v) => VehSpec(v).ResupplyMilliU / 1000f;

        float NaDotacionTotalU()
        {
            long s = 0; foreach (var p in _uw.Platoons) s += NaDotacion(p);
            float u = s / (float)WeaponCatalog.ShotsPerU;
            foreach (var v in _vehs) u += Mathf.Clamp01(v.Ammo) * NaVehCapU(v);
            return u;
        }

        /// <summary>Al empezar la noche (después de InitVehicles).</summary>
        void NaBegin()
        {
            _naPel.Clear(); _naVeh.Clear(); _naVehHitOwner.Clear(); _naVehHitAtropello.Clear();
            if (_uw == null) { _naOn = false; return; }
            foreach (var p in _uw.Platoons) NaP(p.Id).Spent0 = p.AmmoSpent;
            _naStock0 = _uw.StockShots; _naDot0 = NaDotacionTotalU(); _naOn = true;
        }

        /// <summary>Antes y después de cualquier sitio que pueda reponer pelotones (bot, recarga automática, teclas T y Mayús+R).</summary>
        void NaResupAntes() { if (!_naOn) return; _naPre.Clear(); foreach (var p in _uw.Platoons) _naPre[p.Id] = NaDotacion(p); }
        void NaResupDespues()
        {
            if (!_naOn) return;
            foreach (var p in _uw.Platoons)
            {
                if (!_naPre.TryGetValue(p.Id, out var a)) continue;
                long d = NaDotacion(p) - a;
                if (d > 0) { var n = NaP(p.Id); n.Resup++; n.ResupShots += d; }
            }
        }

        void NaVehDisparo(Veh v) { if (_naOn) NaV(v).Disparos++; }
        void NaVehRecarga(Veh v, float u) { if (_naOn) { var n = NaV(v); n.Resup++; n.ResupU += u; } }
        void NaVehHit(Veh v, bool atropello) { _naVehHitOwner.Add(v); _naVehHitAtropello.Add(atropello); }

        /// <summary>Reparto de los abatidos de verdad (los que KillZombies ha quitado del mundo A) entre quien les dio primero.</summary>
        void NaAtribuir(List<TargetHit> hits, Dictionary<uint, Vector2> muertos, bool deVehiculos)
        {
            if (!_naOn || muertos.Count == 0) return;
            for (int i = 0; i < hits.Count; i++)
            {
                uint id = (uint)hits[i].TargetId;
                if (!muertos.TryGetValue(id, out var pos)) continue;
                muertos.Remove(id);                                             // un abatido se cuenta una sola vez
                bool pila = NearWall(pos);
                if (deVehiculos && i < _naVehHitOwner.Count && _naVehHitOwner[i] != null)
                {
                    var n = NaV(_naVehHitOwner[i]); n.Kills++; if (pila) n.KillsPila++; if (_naVehHitAtropello[i]) n.Atropellos++;
                }
                else if (!deVehiculos && hits[i].Platoon >= 0)
                {
                    var n = NaP(hits[i].Platoon); n.Kills++; if (pila) n.KillsPila++;
                    n.PorArma.TryGetValue(hits[i].Weapon, out var k); n.PorArma[hits[i].Weapon] = k + 1;
                }
            }
        }

        /// <summary>Al acabar la noche, junto a la línea [NOCHE] resultado.</summary>
        void NaLog()
        {
            if (!_naOn || _uw == null) return;
            float spu = WeaponCatalog.ShotsPerU;
            var sb = new StringBuilder("[NOCHE-ARMAS] pelotones:");
            long pelResup = 0; int pelKills = 0; float pelU = 0f;
            foreach (var p in _uw.Platoons)
            {
                var n = NaP(p.Id); float u = (p.AmmoSpent - n.Spent0) / spu; pelU += u; pelKills += n.Kills; pelResup += n.ResupShots;
                sb.Append($" [{p.Id} {p.Kind} u={u:0.00} abatidos={n.Kills} pila={n.KillsPila} abatidos_u={(u > 0f ? n.Kills / u : 0f):0}");
                foreach (var kv in n.PorArma) sb.Append($" {kv.Key}={kv.Value}");
                sb.Append($" recargas={n.Resup} recargado_u={n.ResupShots / spu:0.00}]");
            }
            sb.Append(" | vehiculos:");
            float vehResupU = 0f; int vehKills = 0;
            foreach (var v in _vehs)
            {
                var n = NaV(v); vehResupU += n.ResupU; vehKills += n.Kills;
                sb.Append($" [{v.Name} disparos={n.Disparos} abatidos={n.Kills} pila={n.KillsPila} atropellos={n.Atropellos} recargas={n.Resup} recargado_u={n.ResupU:0.00} a_bordo_u={Mathf.Clamp01(v.Ammo) * NaVehCapU(v):0.00}]");
            }
            float dot1 = NaDotacionTotalU();
            float st0 = _naStock0 / spu, st1 = _uw.StockShots / spu;
            sb.Append($" | almacen: inicio_u={st0:0.00} fin_u={st1:0.00} entregado_pelotones_u={pelResup / spu:0.00} entregado_vehiculos_u={vehResupU:0.00}");
            sb.Append($" | dotacion: inicio_u={_naDot0:0.00} fin_u={dot1:0.00}");
            float consumo = (st0 - st1) + (_naDot0 - dot1);
            sb.Append($" | consumo_real_u={consumo:0.00} abatidos_pelotones={pelKills} abatidos_vehiculos={vehKills} abatidos_u_total={(consumo > 0f ? (pelKills + vehKills) / consumo : 0f):0} abatidos_u_infanteria={(pelU > 0f ? pelKills / pelU : 0f):0}");
            Debug.Log(sb.ToString());
        }
    }
}
