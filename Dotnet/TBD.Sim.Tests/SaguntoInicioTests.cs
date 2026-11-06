using System;
using System.Collections.Generic;
using TBD.Net;
using TBD.Sim;

namespace TBD.Sim.Tests
{
    /// <summary>D-N63 (Hugo): Sagunto empieza con 120 u de muro ya construidas, nivel 4,0, en el recinto de 86 × 41 m del tren a escala real.</summary>
    static class SaguntoInicioTests
    {
        static int Id(NetWorld w, string name) { for (int i = 0; i < w.Graph.Nodes.Count; i++) if (w.Graph.Nodes[i].Name == name) return i; return -1; }

        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[27] Sagunto empieza con 120 u de muro construido (D-N63)");
            var w = NetScenario.Mvp();
            var bare = NetScenario.Mvp(false);
            int sag = Id(w, "Sagunto");
            var P = w.Works.P;
            var l = w.Works.At(sag);

            long e4 = 0, hescoMm = 0, sacosMm = 0;
            int gates = 0, doors = 0, nests = 0, spots = 0, deposits = 0, unfinished = 0;
            foreach (var p in l)
            {
                e4 += p.CostE4;
                if (!p.Built || p.WorkE4 < p.NeedE4) unfinished++;
                long len = Obras.SegLenMm(p.X0, p.Y0, p.X1, p.Y1);
                switch (p.Kind)
                {
                    case PieceKind.Hesco: hescoMm += len; break;
                    case PieceKind.Sacos: sacosMm += len; break;
                    case PieceKind.PuertaVia: gates++; break;
                    case PieceKind.Puerta: doors++; break;
                    case PieceKind.Nido: nests++; break;
                    case PieceKind.Foco: spots++; break;
                    case PieceKind.Deposito: deposits++; break;
                }
            }
            double u = e4 / 10_000.0;
            Console.WriteLine($"         {l.Count} piezas, {u:0.00} u, HESCO {hescoMm / 1000.0:0.0} m, sacos {sacosMm / 1000.0:0.0} m, nivel {w.Works.LevelMil(sag)} ‰");

            check(bare.Works.Count(sag) == 0, "Mvp(false) sigue sin obras: las pruebas de obras parten de cero");
            check(u >= 119.9 && u <= 122.0, $"el muro de partida cuesta 120 u (Hugo: vale hasta 124): {u:0.00} u");
            check(w.Works.LevelMil(sag) >= 3_990 && w.Works.LevelMil(sag) <= 4_100, $"nivel de fortificación 4,0: {w.Works.LevelMil(sag)} ‰");
            check(unfinished == 0, "todo está terminado (Built y trabajo hecho): no hay obra en curso ni sube el ruido de obra el día 1");
            check(gates == 2 && doors == 1 && nests == 2 && spots == 3 && deposits == 0, $"puertas de vía 2 (oeste y este), peatonal 1, nidos 2, focos 3, sin depósito ({gates}, {doors}, {nests}, {spots}, {deposits})");
            check(hescoMm >= 60_000 && hescoMm <= 72_000, $"HESCO en el frente oeste y las alas: {hescoMm / 1000.0:0.0} m = {hescoMm * P.HescoPer10m / 10_000.0:0.0} u");
            check(sacosMm >= 175_000 && sacosMm <= 190_000, $"el resto del perímetro en sacos: {sacosMm / 1000.0:0.0} m");
            check(Math.Abs((hescoMm + sacosMm) - (2 * (86_000 + 41_000) - 4 * P.TrackHalfMm)) <= 40, $"el perímetro está completo: {(hescoMm + sacosMm) / 1000.0:0.0} m = 2 × (86 + 41) menos los dos huecos de 3,2 m de las puertas de vía");

            // el frente oeste y la puerta de vía en la vía (y = 0)
            int westHesco = 0, westSacos = 0;
            foreach (var p in l) if (p.IsWall && p.X0 == -43_000 && p.X1 == -43_000) { if (p.Kind == PieceKind.Hesco) westHesco++; else westSacos++; }
            check(westHesco > 0 && westSacos == 0, $"el frente oeste (el que da a la horda) es todo HESCO ({westHesco} módulos, 0 de sacos)");
            int west = 0, east = 0, eastLocked = 0;
            foreach (var p in l)
                if (p.IsGate && Math.Abs(p.Y0) == P.TrackHalfMm && p.Y1 == -p.Y0)
                {
                    if (p.X0 == -43_000 && p.X1 == -43_000) west++;
                    if (p.X0 == 43_000 && p.X1 == 43_000) { east++; if (p.GateMode == 2) eastLocked++; }
                }
            check(west == 1, "la puerta de vía del oeste está en el muro oeste, justo sobre la vía (y de -1,6 a +1,6 m), en automático");
            check(east == 1 && eastLocked == 1, "el muro este cruza la vía y la regla de PlanWall le pone su puerta de vía; queda bloqueada cerrada (la vía acaba dentro del fuerte)");
            bool trackFree = true;
            foreach (var p in l) if (p.IsWall && Math.Min(Math.Abs((long)p.Y0), Math.Abs((long)p.Y1)) < P.TrackHalfMm && Math.Max(Math.Abs((long)p.Y0), Math.Abs((long)p.Y1)) < P.TrackHalfMm) trackFree = false;
            check(trackFree, "ningún muro tapa la vía");

            // todo cuenta para el nivel y el mantenimiento: dentro de 60 m del andén
            int counted = 0; foreach (var p in l) if (Obras.CountsForLevel(P, p)) counted++;
            check(counted == l.Count, $"las {l.Count} piezas están a ≤ 60 m del andén y cuentan (andén centrado en el recinto, D-N63)");

            // coherencia con el recinto de la zona (Level.cs): si Marcos vuelve a cambiar la escala, esto avisa
            check(Math.Abs((Level.FortX1 - Level.FortX0) * 500f - 43_000f) < 1f && Math.Abs((Level.FortY1 - Level.FortY0) * 500f - 20_500f) < 1f, $"las medidas coinciden con Level.cs (fuerte {Level.FortX1 - Level.FortX0} × {Level.FortY1 - Level.FortY0} m)");
            check(Math.Abs((Level.FortX0 + Level.FortX1) * 0.5f - 113f) < 0.01f && Level.TrackY == 64.5f && Math.Abs((Level.FortY0 + Level.FortY1) * 0.5f - Level.TrackY) < 0.01f, "el andén (0, 0) de las obras es el centro del recinto (113; 64,5) y la vía pasa por él");

            // el stock del día 1 no se toca y el estado es determinista
            bool sameStock = true;
            for (int n = 0; n < w.Graph.Nodes.Count; n++) for (int r = 0; r < NetConst.ResCount; r++) if (w.Stock[n, r] != bare.Stock[n, r]) sameStock = false;
            check(sameStock, "el stock del día 1 no se toca (el muro de partida no se cobra)");
            check(NetScenario.Mvp().Hash() == w.Hash() && w.Hash() != bare.Hash(), "dos mundos con muro dan el mismo hash, y es distinto del de uno sin muro");

            // mantenimiento: ceil(1 % de 120 u) = 2 u al día, con reservas la vida no baja
            int maint = w.Works.MaintenanceU(sag);
            check(maint == 2, $"el mantenimiento diario es ceil(1 % de {u:0.0} u) = {maint} u");
            long mat0 = w.Stock[sag, (int)Res.Materiales];
            long mat0Bare = bare.Stock[sag, (int)Res.Materiales];
            for (long t = 0; t < NetConst.DayTicks; t++) { w.Step(); bare.Step(); }
            long dMat = (w.Stock[sag, (int)Res.Materiales] - mat0) - (bare.Stock[sag, (int)Res.Materiales] - mat0Bare);
            int minLife = 1000; foreach (var p in w.Works.At(sag)) if (p.IsWall || p.IsGate) minLife = Math.Min(minLife, p.LifeMil);
            check(minLife == 1000 || maint == 0 || dMat == -maint, $"tras un día el almacén de Sagunto ha pagado {-dMat} u de mantenimiento y la vida no baja ({minLife} ‰)");
        }
    }
}
