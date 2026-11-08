using System;
using System.Collections.Generic;

namespace TBD.Units
{
    /// <summary>
    /// La parte de ZONA del embarque (D-N166): subir o bajar tarda 10 s por pelotón y 20 s por vehículo, y mientras tanto no disparan.
    /// De noche solo se sube o se baja dentro del recinto y sin zombis a 3 m, salvo la tropa que baja junto al tren a las puertas de una estación sitiada.
    /// Al terminar un trabajo, quien llama manda la orden de red (NetWorld.CmdLoad / CmdUnload). Ticks de zona (IMath.TickRate por segundo), enteros y sin azar.
    /// El pelotón sube con UnitsWorld.Embark(id, carrierId, plazas): a bordo no dispara ni es blanco (Platoon.Aboard ≠ 0), como en el helicóptero.
    /// Los vehículos solo cuentan su tiempo aquí: su «a bordo» lo lleva la red (Embarque.cs) y la vista los quita o los pone.
    /// </summary>
    public sealed class EmbarqueZona
    {
        public const int RetenMm = 3000;           // D-N166 = la regla de reparar: sin zombis a 3 m
        public sealed class Trabajo
        {
            public bool Vehiculo, Bajar;
            public int Id, CarrierId, X, Y, Heading;   // X, Y, Heading: dónde se baja
            public int Ticks, Need;
            public bool PrevHoldFire;
        }
        public readonly List<Trabajo> Cola = new List<Trabajo>();

        public static int TicksPara(bool vehiculo) { return (vehiculo ? Net.EmbarqueState.SubirVehiculoS : Net.EmbarqueState.SubirPelotonS) * IMath.TickRate; }

        /// <summary>Regla de noche (D-N166). null si se puede.</summary>
        public static string NocheBlockReason(bool esNoche, bool dentroRecinto, bool zombisCerca, bool bajadaSitiada)
        {
            if (!esNoche || bajadaSitiada) return null;
            if (!dentroRecinto) return "De noche solo se sube dentro del recinto";
            if (zombisCerca) return "Hay zombis a menos de 3 m";
            return null;
        }

        /// <summary>¿Hay algún zombi a RetenMm o menos de (x, y)?</summary>
        public static bool ZombisCerca(IList<Target> zombis, int x, int y)
        {
            if (zombis == null) return false; long r2 = (long)RetenMm * RetenMm;
            for (int i = 0; i < zombis.Count; i++) { long dx = zombis[i].X - x, dy = zombis[i].Y - y; if (dx * dx + dy * dy <= r2) return true; }
            return false;
        }

        bool Ocupado(bool veh, int id) { foreach (var t in Cola) if (t.Vehiculo == veh && t.Id == id) return true; return false; }

        /// <summary>Empieza a subir un pelotón al transporte «carrierId» (TrenPreparar.ATren). Para en seco y deja de disparar. false si no existe, no tiene vivos, ya va a bordo o ya está en la cola.</summary>
        public bool SubirPeloton(UnitsWorld w, int platoon, int carrierId)
        {
            var p = w.Platoons.Find(q => q.Id == platoon);
            if (p == null || p.Aboard != 0 || p.AliveCount() == 0 || carrierId == 0 || Ocupado(false, platoon)) return false;
            Cola.Add(new Trabajo { Id = platoon, CarrierId = carrierId, Need = TicksPara(false), PrevHoldFire = p.HoldFire });
            p.HoldFire = true; p.Moving = false; p.AttackMove = false;
            return true;
        }

        /// <summary>Empieza a bajar un pelotón que va a bordo, en (x, y).</summary>
        public bool BajarPeloton(UnitsWorld w, int platoon, int x, int y, int heading = 0)
        {
            var p = w.Platoons.Find(q => q.Id == platoon);
            if (p == null || p.Aboard == 0 || Ocupado(false, platoon)) return false;
            Cola.Add(new Trabajo { Id = platoon, CarrierId = p.Aboard, Bajar = true, X = x, Y = y, Heading = heading, Need = TicksPara(false), PrevHoldFire = false });
            return true;
        }

        /// <summary>Un vehículo (índice de la zona) empieza a subir o a bajar: solo cuenta los 20 s.</summary>
        public bool Vehiculo(int index, int carrierId, bool bajar)
        {
            if (index < 0 || Ocupado(true, index)) return false;
            Cola.Add(new Trabajo { Vehiculo = true, Id = index, CarrierId = carrierId, Bajar = bajar, Need = TicksPara(true) });
            return true;
        }

        /// <summary>Cancela el trabajo (el jugador da otra orden, o se rompe la regla de noche): el pelotón vuelve a disparar como antes.</summary>
        public bool Cancelar(UnitsWorld w, bool vehiculo, int id)
        {
            for (int i = 0; i < Cola.Count; i++)
            {
                var t = Cola[i]; if (t.Vehiculo != vehiculo || t.Id != id) continue;
                if (!vehiculo && !t.Bajar) { var p = w.Platoons.Find(q => q.Id == id); if (p != null) p.HoldFire = t.PrevHoldFire; }
                Cola.RemoveAt(i); return true;
            }
            return false;
        }

        /// <summary>Un tick de zona. Devuelve los trabajos que acaban en este tick (en el orden de la cola): el pelotón ya está a bordo (o abajo) y hay que mandar la orden de red.</summary>
        public List<Trabajo> Step(UnitsWorld w)
        {
            var done = new List<Trabajo>();
            for (int i = 0; i < Cola.Count; i++)
            {
                var t = Cola[i];
                if (!t.Vehiculo)
                {
                    var p = w.Platoons.Find(q => q.Id == t.Id);
                    if (p == null || p.AliveCount() == 0) { Cola.RemoveAt(i--); continue; }     // cayó mientras subía: no sube nadie
                    if (!t.Bajar) { p.HoldFire = true; p.Moving = false; }
                }
                if (++t.Ticks < t.Need) continue;
                if (!t.Vehiculo)
                {
                    var p = w.Platoons.Find(q => q.Id == t.Id);
                    if (t.Bajar) w.Disembark(t.Id, t.X, t.Y, t.Heading);
                    else { p.HoldFire = t.PrevHoldFire; w.Embark(t.Id, t.CarrierId, int.MaxValue / 2); }
                }
                done.Add(t); Cola.RemoveAt(i--);
            }
            return done;
        }
    }
}
