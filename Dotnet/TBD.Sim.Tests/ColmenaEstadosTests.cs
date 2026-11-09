using System;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>[38] Los tres estados de la Colmena (D-N160, Hugo, aprobado por Lucía el 9-nov): señal diaria, subida inmediata, bajada tras 3 días, ×0,9/1/1,25 y un asedio más en Enjambre.</summary>
    public static class ColmenaEstadosTests
    {
        static int CountIntel(NetWorld w, string id) { int n = 0; foreach (var e in w.Intel) if (e.Id == id) n++; return n; }

        /// <summary>Deja con gente exactamente «held» nodos (los primeros que no son apeaderos por fundar) y vacía el resto.</summary>
        static void Hold(NetWorld w, int held)
        {
            int k = 0;
            for (int n = 0; n < w.Graph.Nodes.Count; n++)
            {
                if (w.Stations.IsSite(n) && !w.Stations.Founded[n]) continue;
                w.Stock[n, (int)Res.Personas] = k < held ? 1 : 0; k++;
            }
        }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("\n[38] Estados de la Colmena (D-N160): Dispersa, Al acecho y Enjambre");
            var w = NetScenario.Inicio(); var d = w.Director;
            check(d.P.ColOn == 1 && d.P.ColUp1Mil == 4000 && d.P.ColUp2Mil == 7000 && d.P.ColWarnMil == 6000 && d.P.ColForgetDays == 3 && d.P.ColMult0 == 900 && d.P.ColMult2 == 1250 && d.P.ColExtraSieges == 1 && d.P.H0 == 1250,
                "balance_colmena.csv: encendido, umbrales 4 y 7, aviso en 6, 3 días de olvido, ×0,9/1/1,25, +1 asedio y H0 1250");
            check(d.ColState == HiveDirector.ColDispersa && d.ColMultMil() == 900 && d.DayBudget(5, 0) == d.DayZombies(5, 0) * 900 / 1000, "empieza Dispersa: el presupuesto del día es el 90 % de la curva");
            int slots = 0; for (int n = 0; n < w.Graph.Nodes.Count; n++) if (!(w.Stations.IsSite(n) && !w.Stations.Founded[n])) slots++;
            check(slots >= 9, $"hay {slots} nodos que pueden tener gente");

            var t = NetScenario.Inicio(); var c = t.Director; long hu0 = c.HuellaMil(t);
            Hold(t, 1); for (int day = 0; day <= 7; day++) c.ColmenaDay(t, day);
            check(c.ColState == 0 && c.ColSignalMil == 0, $"una semana sin abrir nodos: señal {c.ColSignalMil} ‰, Dispersa");
            Hold(t, 5); c.ColmenaDay(t, 8);
            check(c.ColState == HiveDirector.ColAcecho && CountIntel(t, "CO-1") == 1 && !c.ColWarning, $"4 nodos en 7 días (señal {c.ColSignalMil} ‰): pasa al instante a Al acecho y sale CO-1");
            Hold(t, 7); c.ColmenaDay(t, 9);
            check(c.ColState == HiveDirector.ColAcecho && c.ColWarning, $"6 en 7 días (señal {c.ColSignalMil} ‰): sigue Al acecho y la red pide el aviso ámbar «La Colmena se agita»");
            Hold(t, 8); c.ColmenaDay(t, 10);
            check(c.ColState == HiveDirector.ColEnjambre && CountIntel(t, "CO-2") == 1 && c.ColMultMil() == 1250 && c.MaxSieges() == c.P.MaxSiegesStage1 + 1 && !c.ColWarning,
                $"7 en 7 días (señal {c.ColSignalMil} ‰): Enjambre, CO-2, presupuesto ×1,25 y un asedio simultáneo más ({c.MaxSieges()})");
            ulong hE = t.Hash(); var b = NetSave.Save(t); var l = NetScenario.Inicio(); NetSave.Load(b, l);
            check(l.Hash() == hE && l.Director.ColState == 2 && l.Director.ColSignalMil == c.ColSignalMil && l.Director.ColHeld[10 % 8] == 8, "se guarda y se carga en Enjambre, con la historia de 8 días (NetVersion 6)");
            int toAcecho = -1, toDisp = -1;
            for (int day = 11; day <= 30; day++) { int st = c.ColState; c.ColmenaDay(t, day); if (st == 2 && c.ColState == 1) toAcecho = day; if (st == 1 && c.ColState == 0) toDisp = day; }
            int co3 = CountIntel(t, "CO-3"), co4 = CountIntel(t, "CO-4");
            // ventana de 7 días: el día 14 aún cuenta 7 nodos ganados (8 − 1 del día 7); el 15 ya solo 3 (8 − 5): por debajo los días 15, 16 y 17
            check(toAcecho == 17 && co3 == 1, $"baja a Al acecho tras 3 días seguidos por debajo del umbral (el día {toAcecho}) y sale CO-3");
            check(toDisp == 20 && co4 == 1 && c.ColMultMil() == 900, $"baja de uno en uno: 3 días después (el día {toDisp}) vuelve a Dispersa y sale CO-4");
            var h = NetScenario.Inicio(); var hc = h.Director; long huBase = hc.P.ColHuellaBaseMil;
            Hold(h, 1); hc.ColmenaDay(h, 0); long hu = hc.HuellaMil(h);
            check(hc.ColSignalMil == hc.P.ColHuellaWeightMil * Math.Max(0, hu - huBase) / 1000, $"la huella entra en la señal con 0,5 × (huella − 8): huella {hu} ‰, señal {hc.ColSignalMil} ‰");
            h.Stock[0, (int)Res.Personas] = 20000; hc.ColmenaDay(h, 1); long hu2 = hc.HuellaMil(h);
            check(hu2 > 16000 && hc.ColSignalMil == hc.P.ColHuellaWeightMil * (hu2 - huBase) / 1000 && hc.ColState == HiveDirector.ColAcecho, $"con mucha gente (huella {hu2} ‰) la señal sube a {hc.ColSignalMil} ‰ sin abrir nodos");

            var off = NetScenario.Inicio(); off.Director.P.ColOn = 0; Hold(off, 9); for (int day = 0; day <= 9; day++) off.Director.ColmenaDay(off, day);
            check(off.Director.ColState == 0 && off.Director.ColMultMil() == 1000 && off.Director.MaxSieges() == off.Director.P.MaxSiegesStage1, "apagado (colmena_estados = 0) no hace nada: ×1 y los asedios de la etapa");
            var i0 = NetScenario.Inicio(); bool co0 = i0.Director.ColmenaIntro(i0), co0b = i0.Director.ColmenaIntro(i0);
            check(co0 && !co0b && CountIntel(i0, "CO-0") == 1, "CO-0: la primera vez que se abre el mapa, con la Colmena Dispersa, sale una sola vez");
            var i1 = NetScenario.Inicio(); var bi = NetSave.Save(i1); var li = NetScenario.Inicio(); NetSave.Load(bi, li);
            check(!li.Director.ColmenaIntro(li) && CountIntel(li, "CO-0") == 0 && li.Hash() == i1.Hash(), "CO-0 no sale en una partida cargada (y no cambia el hash)");
            var i2 = NetScenario.Inicio(); i2.Director.ColState = HiveDirector.ColAcecho;
            check(!i2.Director.ColmenaIntro(i2) && CountIntel(i2, "CO-0") == 0, "CO-0 no sale si la Colmena ya no está Dispersa");
            check(NetSave.NetVersion == 6, "la versión de la red sube a 6");
        }
    }
}
