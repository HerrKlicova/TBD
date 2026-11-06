using System;
using System.Collections.Generic;

namespace TBD.Net
{
    // Hito 5, paso 5: el taller de Sagunto fabrica y rehabilita trenes (diseno-hito5.md §5). Lógica pura en enteros, sin Unity.
    // Una cola de encargos (el taller hace uno a la vez), pagados al encargar con las existencias del taller. Al terminar, el tren nace en el nodo del taller
    // con una sola parada (sin horario no se mueve: poner horario y carga es cosa del jugador). Todo el estado entra en el hash, y vale 0 si está vacío.

    /// <summary>Qué se encarga: tren nuevo de carga, de combate o de obras (120 u + 30 de combustible, 3 días) o rehabilitar uno de los dos trenes viejos (una sola vez cada uno).</summary>
    public enum TallerKind : byte { Carga = 0, Combate = 1, Obras = 2, RehabTren3 = 3, RehabObras = 4 }

    public enum TallerResult : byte { Ok = 0, NoHayTaller = 1, TipoInvalido = 2, SinMateriales = 3, SinCombustible = 4, SinObreros = 5, MaximoTrenes = 6, YaRehabilitado = 7, EncargoInexistente = 8, ChispasNoEsta = 9, ChispasBloqueado = 10, TrenLejos = 11 }

    public struct TallerParams
    {
        public string Node;                         // nodo del taller (solo Sagunto en el MVP)
        public int TrainU, TrainFuel;               // tren nuevo: tren_coste_materiales 120, tren_coste_combustible 30
        public int Tren3U, Tren3Fuel;               // rehabilitar el Tren 3 (a medio montar): tren3_demo_coste_materiales 60, tren3_demo_coste_combustible 15
        public int ObrasU, ObrasFuel, ObrasCrew;    // rehabilitar el tren de obras: tren_obras_materiales 20, tren_obras_combustible 5, tren_obras_obreros 8 (se descuentan al salir el tren)
        public int TrainCrew;                       // tren_tripulacion 10: un tren nuevo toma esta gente de la población al encargarse (Hugo)
        public int TrainDays, RehabHours;           // taller_dias_tren 3; taller_rehabilitar_horas 36
        public int ChispasPermil;                   // Chispas Ferrer acelera un 25 % reparaciones de trenes y de vía, no la fabricación (code, taller_chispas_permil)
        public int MaxTrains, MaxTrainsStage2;      // 4 en la demo (D-N16, code, trenes_max_demo) y 6 en la etapa 2 (trenes_max_etapa2)
        public int CancelStartedMil;                // al cancelar un encargo empezado se devuelve este ‰ de lo que falta (como las obras: 500)
        public bool Stage2;                         // ¿etapa 2? (el máximo pasa de 4 a 6)

        public static TallerParams Default
        {
            get
            {
                return new TallerParams
                {
                    Node = "Sagunto", TrainU = 120, TrainFuel = 30, Tren3U = 60, Tren3Fuel = 15, ObrasU = 20, ObrasFuel = 5, ObrasCrew = 8, TrainCrew = 10,
                    TrainDays = 3, RehabHours = 36, ChispasPermil = 250, MaxTrains = 4, MaxTrainsStage2 = 6, CancelStartedMil = 500
                };
            }
        }

        /// <summary>De balance_economia.csv (e) y balance_construccion.csv (c); cualquiera puede faltar (se queda el valor por defecto).</summary>
        public static TallerParams FromCsv(CsvTable e, CsvTable c)
        {
            var d = Default;
            if (e != null)
            {
                d.TrainU = (int)e.TryValue("tren_coste_materiales", d.TrainU); d.TrainFuel = (int)e.TryValue("tren_coste_combustible", d.TrainFuel);
                d.Tren3U = (int)e.TryValue("tren3_demo_coste_materiales", d.Tren3U); d.Tren3Fuel = (int)e.TryValue("tren3_demo_coste_combustible", d.Tren3Fuel);
                d.TrainCrew = (int)e.TryValue("tren_tripulacion", d.TrainCrew); d.MaxTrainsStage2 = (int)e.TryValue("trenes_max_etapa2", d.MaxTrainsStage2); d.MaxTrains = (int)e.TryValue("trenes_max_demo", d.MaxTrains);
            }
            if (c != null)
            {
                d.TrainDays = (int)c.TryValue("taller_dias_tren", d.TrainDays); d.RehabHours = (int)c.TryValue("taller_rehabilitar_horas", d.RehabHours);
                d.ObrasU = (int)c.TryValue("tren_obras_materiales", d.ObrasU); d.ObrasFuel = (int)c.TryValue("tren_obras_combustible", d.ObrasFuel);
                d.ObrasCrew = (int)c.TryValue("tren_obras_obreros", d.ObrasCrew); d.ChispasPermil = (int)c.TryValue("taller_chispas_permil", d.ChispasPermil);
            }
            return d;
        }

        public int MaxTrainsNow { get { return Stage2 ? MaxTrainsStage2 : MaxTrains; } }
    }

    public struct TallerOrder
    {
        public TallerKind Kind;
        public int PaidU, PaidFuel, PaidCrew;       // lo cobrado al encargar (para devolver exactamente); PaidCrew = tripulación de un tren nuevo, que sale de la población
        public long NeedTicks, DoneMilli;           // trabajo en milésimas de tick: 1.000 por tick, 1.250 en una rehabilitación con Chispas en el taller
        public bool Waiting;                        // acabado, pero esperando a que haya dotación libre para salir (tren de obras)
        public bool Started { get { return DoneMilli > 0; } }
        public long DoneTicks { get { return DoneMilli / 1000; } }
    }

    public sealed class TallerState
    {
        public TallerParams P;
        public readonly int NodeIndex;
        public readonly List<TallerOrder> Queue = new List<TallerOrder>();
        /// <summary>
        /// Dónde está Chispas Ferrer (Lucía, D-N45): -2 no está, -1 en el taller (rehabilitaciones un 25 % más rápidas), ≥ 0 en ese tren (índice de servicio):
        /// si es un tren de obras, las reparaciones de vía y puente que haga van un 25 % más rápidas. En la demo no sale del taller hasta que rueda el tren de obras.
        /// </summary>
        public int ChispasAt = -1;
        public bool ObrasOut;               // el tren de obras ya ha salido del taller: desde ahora Chispas puede subir a un tren
        public bool Tren3Done, ObrasDone;   // rehabilitados (solo una vez cada uno)
        public int Built;                   // trenes que ha sacado el taller (para nombrarlos)

        public TallerState(RailGraph g, TallerParams p)
        {
            P = p; NodeIndex = -1;
            for (int i = 0; i < g.Nodes.Count; i++) if (g.Nodes[i].Name == p.Node) { NodeIndex = i; break; }
        }

        /// <summary>Velocidad de un trabajo del taller en milésimas por tick: 1.000, o 1.000 + ChispasPermil en una rehabilitación con Chispas en el taller.</summary>
        public int RateMilli(TallerKind k)
        {
            bool rehab = k == TallerKind.RehabTren3 || k == TallerKind.RehabObras;
            return rehab && ChispasAt == -1 ? 1000 + P.ChispasPermil : 1000;
        }

        /// <summary>Tiempo de una reparación con Chispas a bordo: ceil(t × 1000 / (1000 + ‰)). Con 250 ‰ es ×0,8.</summary>
        public long Faster(long ticks)
        {
            if (ticks <= 0) return ticks;
            long den = 1000 + P.ChispasPermil;
            return (ticks * 1000 + den - 1) / den;
        }

        public ulong Hash()
        {
            ulong h = 0;
            for (int i = 0; i < Queue.Count; i++)
            {
                var o = Queue[i];
                h += NetWorld.Mix(0x7A11E4UL ^ ((ulong)(uint)i << 48) ^ ((ulong)o.Kind << 40) ^ ((ulong)(uint)o.PaidU << 8) ^ (ulong)o.DoneMilli * 0x9E3779B1UL ^ ((ulong)(uint)o.NeedTicks << 20));
                h += NetWorld.Mix(0x7A11E5UL ^ ((ulong)(uint)i << 48) ^ ((ulong)(uint)o.PaidFuel << 16) ^ (ulong)(uint)o.PaidCrew ^ (o.Waiting ? 0x8000000000UL : 0UL));
            }
            if (ChispasAt != -1) h += NetWorld.Mix(0x7A11E6UL ^ ((ulong)(uint)(ChispasAt + 2) << 32));
            if (ObrasOut) h += NetWorld.Mix(0x7A11EAUL);
            if (Tren3Done) h += NetWorld.Mix(0x7A11E7UL);
            if (ObrasDone) h += NetWorld.Mix(0x7A11E8UL);
            if (Built > 0) h += NetWorld.Mix(0x7A11E9UL ^ ((ulong)(uint)Built << 32));
            return h;
        }
    }
}
