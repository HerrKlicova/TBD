using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using TBD.Sim;
using TBD.Units;
using TBD.Net;

namespace TBD.View
{
    /// <summary>
    /// La noche persistente (D-N91; Eugenio, 7-nov): salir de la zona al mapa NO destruye la noche. Todo el estado vive aqui (NightRun.Session);
    /// mientras no se ve la zona, un NightClock con DontDestroyOnLoad simula la misma noche con el NightSim de Nacho (enganchado a su NightGame y a su UnitsWorld),
    /// y al volver se recrean los zombis en ECS desde NightSim. La sesion se borra al terminar la noche, con «Volver a empezar» o con «Menu principal».
    /// Diseno de Nacho: Docs/noche-fuera-de-pantalla.md.
    /// </summary>
    public sealed class NightSession
    {
        public NightGame Ng; public UnitsWorld Uw; public NightSim Sim; public Dictionary<int, string> PName = new Dictionary<int, string>();
        public float WallHp, WallMax; public int Kills, Lost, ShotsSpent, SpawnedTotal, Resupplies, LastWave, WaveTotal;
        public NightBot Bot; public NightTraceRecorder Rec; public StringBuilder FullTrace;
        public bool Away, Reported; public NightClock Clock;
        public int Node = -1;      // zona (nodo de la red) de esta noche: cada estación tiene la suya (Iker, 9-nov: Nules reanudaba la noche de Sagunto)
    }

    /// <summary>Reloj de la noche mientras la zona no esta en pantalla: 30 Hz fijos, mismo ritmo que la zona.</summary>
    public sealed class NightClock : MonoBehaviour
    {
        float _acc; GUIStyle _st; Texture2D _px;
        public NightSession S;      // la noche que lleva este reloj (la de su zona, aunque el jugador esté en otra)
        void Update()
        {
            var s = S ?? NightRun.Session; if (s == null || !s.Away || s.Sim == null) return;
            if (SceneManager.GetActiveScene().name != "SampleScene") Sfx.Loop("sfx_batalla_mapa_1", s.Sim.Night.State == NightState.Jugando ? 0.15f : 0f, false);
            if (s.Sim.Night.State != NightState.Jugando) return;
            _acc += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            int steps = 0;
            while (_acc >= 1f / 30f && steps < 10) { _acc -= 1f / 30f; steps++; s.Sim.Step(); if (s.Sim.Night.State != NightState.Jugando) { Debug.Log($"[NOCHE] la noche termina fuera de pantalla: {s.Sim.Night.State}"); break; } }
        }
        void OnGUI()
        {
            var s = S ?? NightRun.Session; if (s == null || !s.Away || s.Sim == null) return;
            if (NightRun.Session != null && s != NightRun.Session) return;      // un solo cartel: el de la última zona
            if (SceneManager.GetActiveScene().name == "SampleScene") return;
            if (_st == null) { _st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true }; _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            var ng = s.Sim.Night; string txt;
            if (ng.State == NightState.Derrota) txt = "<b>" + ng.DefeatHeadline() + "</b> Entra para ver el parte.";
            else if (ng.State == NightState.Victoria) txt = "<b>Sagunto aguanta.</b> Entra para ver el parte.";
            else txt = $"Sagunto en combate · {ng.Hour:00}:{ng.Minute:00} · zombis {s.Sim.ZombiesAlive} · muro {Mathf.RoundToInt(100f * s.Sim.WallHp / Mathf.Max(1, s.Sim.SP.WallHp))} %";
            _st.fontSize = Mathf.Max(13, Screen.height / 55); float w = Screen.width * 0.5f, h = _st.fontSize * 2.2f;
            var r = new Rect((Screen.width - w) * 0.5f, 10f, w, h);
            var old = GUI.color; GUI.color = new Color(0.06f, 0.07f, 0.1f, 0.82f); GUI.DrawTexture(r, _px); GUI.color = old;
            _st.normal.textColor = ng.State == NightState.Jugando ? new Color(0.83f, 0.82f, 0.8f) : new Color(0.88f, 0.55f, 0.19f);
            GUI.Label(r, txt, _st);
        }
    }

    public partial class PrototypeBootstrap
    {
        /// <summary>Al salir de la zona con la noche en marcha: guarda todo en la sesion, pasa los zombis y vehiculos a NightSim y arranca el reloj de fondo.</summary>
        void SaveNightSession()
        {
            if (_ng == null || _uw == null) return;
            var s = NightRun.Session ?? (NightRun.Session = new NightSession());
            s.Ng = _ng; s.Uw = _uw; s.PName = new Dictionary<int, string>(_pname); s.WallHp = _wallHp; s.WallMax = _wallMax; s.Kills = _nightKills; s.Lost = _nightLost; s.ShotsSpent = _nightShotsSpent;
            s.SpawnedTotal = _spawnedTotal; s.Resupplies = _resupplies; s.LastWave = _lastWave; s.WaveTotal = _nightWaveTotal; s.Bot = _bot; s.Rec = _rec; s.FullTrace = _fullTrace;
            NightSimParams sp; try { sp = NightSimParams.FromJson(BalanceSource.Read("nightsim_params.json")); } catch (System.Exception) { sp = NightSimParams.Default(); }
            int wall = _wallMax > 0f ? (int)(sp.WallHp * (double)_wallHp / _wallMax) : sp.WallHp;
            var sim = new NightSim(_ng, _uw, sp, wall);
            if (_worldA != null && _worldA.IsCreated)
            {
                using (var q = _worldA.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Zombie>()))
                {
                    var zs = q.ToComponentDataArray<Zombie>(Allocator.Temp);
                    for (int i = 0; i < zs.Length; i++) sim.ImportZombie((int)(zs[i].Pos.x * 1000f), (int)(zs[i].Pos.z * 1000f));
                    zs.Dispose();
                }
            }
            foreach (var v in _vehs)
            {
                if (v.Go == null) { var dead = NightVehicle.Make(v.Name, v.Heli ? NightVehKind.Nh90 : v.Name.StartsWith("Dragón") ? NightVehKind.Dragon : NightVehKind.Vamtac, 0, 0); dead.Shots = 0; dead.Fire = false; sim.Vehicles.Add(dead); continue; }   // el hueco se conserva: el id de vehiculo es el indice en _vehs (Nacho)
                var kind = v.Heli ? NightVehKind.Nh90 : v.Name.StartsWith("Dragón") ? NightVehKind.Dragon : NightVehKind.Vamtac;
                var nv = NightVehicle.Make(v.Name, kind, (int)(v.Pos.x * 1000f), (int)(v.Pos.y * 1000f));
                try { NightVehicle.ApplyParams(nv, BalanceSource.Read("nightsim_params.json")); } catch (System.Exception) { }
                nv.Shots = Mathf.Clamp((int)(v.Ammo * nv.MaxShots), 0, nv.MaxShots); nv.Fire = v.Fire; nv.Airborne = v.Heli && v.State == 2; nv.ResupplyUntil = v.ResupT;
                sim.Vehicles.Add(nv);
            }
            if (_bot != null) sim.Bot = _bot;                                  // con -nightbot, el bot sigue fuera de pantalla (Nacho): misma prueba A/B comparable
            s.Sim = sim; s.Away = true;
            if (s.Clock == null) { var go = new GameObject("NightClock"); DontDestroyOnLoad(go); s.Clock = go.AddComponent<NightClock>(); } s.Clock.S = s; s.Node = _zoneNode;
            Debug.Log($"[NOCHE] salgo al mapa con la noche en marcha: {sim.ZombiesAlive} zombis pasan a NightSim, hora {_ng.Hour:00}:{_ng.Minute:00}");
        }

        /// <summary>Al volver a la zona: recrea los zombis en ECS desde NightSim, devuelve el muro, los vehiculos y los contadores, y quita el reloj de fondo.</summary>
        void ResumeNightSession()
        {
            var s = NightRun.Session; if (s == null || !s.Away || s.Sim == null) return;
            var sim = s.Sim;
            var lst = new List<Target>(); sim.ExportZombies(lst);
            var pts = new List<SpawnPoint>(lst.Count); foreach (var t in lst) pts.Add(new SpawnPoint { X = t.X, Y = t.Y });
            if (pts.Count > 0) SpawnZombies(pts);
            _wallHp = s.WallMax > 0f ? s.WallMax * sim.WallHp / Mathf.Max(1f, sim.SP.WallHp) : s.WallHp; _wallMax = s.WallMax; _ng.WallPermil = Mathf.RoundToInt(1000f * _wallHp / Mathf.Max(1f, _wallMax));
            _nightKills = s.Kills + sim.Kills; _nightLost = s.Lost + sim.SoldierDeaths; _resupplies = s.Resupplies + sim.Resupplies; _spawnedTotal = s.SpawnedTotal + sim.Spawned;
            foreach (var nv in sim.Vehicles)
                foreach (var v in _vehs)
                    if (v.Name == nv.Name) { v.Ammo = nv.MaxShots > 0 ? nv.Shots / (float)nv.MaxShots : v.Ammo; v.Fire = nv.Fire; v.ResupT = nv.ResupplyUntil; v.Pos = new Vector2(nv.X / 1000f, nv.Y / 1000f); }
            if (s.Clock != null) Destroy(s.Clock.gameObject);
            s.Clock = null; s.Away = false; s.Sim = null;
            Debug.Log($"[NOCHE] vuelvo a la zona (tick {_ng.Tick}): {pts.Count} zombis recreados, hora {_ng.Hour:00}:{_ng.Minute:00}, muro {_ng.WallPermil}‰, estado {_ng.State}");
            bool reportado = s.Reported; if (_ng.State != NightState.Jugando && !reportado) NightEnded(); else if (_ng.State == NightState.Victoria) _reportClosed = true;   // D-N170: el parte sale una sola vez
        }

        /// <summary>La noche acaba o se reinicia: se borra la sesion y el reloj de fondo.</summary>
        public static void ClearNightSession()
        {
            var s = NightRun.Session; if (s != null && s.Clock != null) Object.Destroy(s.Clock.gameObject);
            NightRun.Session = null;
            foreach (var ps in NightRun.Aparcadas.Values) if (ps != null && ps.Clock != null) Object.Destroy(ps.Clock.gameObject);
            NightRun.Aparcadas.Clear();
        }
    }
}
