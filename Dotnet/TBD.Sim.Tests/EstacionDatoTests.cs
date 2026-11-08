using System;
using System.IO;
using TBD.Net;

namespace TBD.Sim.Tests
{
    /// <summary>Dato por estación (EstacionDato.cs): estaciones/castellon.json de Álvaro, campos de Lucía y variante por hash.</summary>
    static class EstacionDatoTests
    {
        public static void Run(Action<bool, string> check)
        {
            Console.WriteLine("[18c] Dato por estación (estacion_castellon.json)");
            var d = EstacionDato.FromJson(File.ReadAllText(Path.Combine(BalanceTests.DataDirPublic(), "estacion_castellon.json")));
            check(d.Id == "castellon" && d.Nombre == "Castellón" && d.Tamano == EstacionTamano.Principal && d.EstadoInicial == EstacionInicio.Infestada && d.Funcion == "combustible",
                $"cabecera y campos de Lucía: {d}");
            check(d.Piezas.Count == 150 && d.Vias.Count == 8 && d.Andenes.Count == 2 && d.Puertas.Count == 6 && d.Recinto != null && d.Recinto.Length == 10,
                $"150 piezas (v0.4 con el mobiliario urbano, estación subterránea), 8 vías, 2 andenes, 6 puertas y un recinto de 5 vértices → {d.Piezas.Count}, {d.Vias.Count}, {d.Andenes.Count}, {d.Puertas.Count}, {(d.Recinto == null ? 0 : d.Recinto.Length / 2)}");
            int cuerpos = 0; foreach (var p in d.Piezas) if (p.Pieza == "kit_cuerpo" || p.Pieza == "kit_remate") cuerpos++;
            check(cuerpos == 0, "v0.4: sin edificio de viajeros de superficie (kit_cuerpo/kit_remate): la estación va bajo losa con el vestíbulo de Teo");
            int c2 = d.FindSitio("C2_deposito");
            check(d.Sitios.Count == 4 && c2 >= 0 && d.Sitios[c2].X == 178f && d.Sitios[c2].Z == 22f && d.OrdenLiberacion.Count == 4 && d.OrdenLiberacion[0] == "C3_ronda" && d.OrdenLiberacion[3] == "C1_estacion",
                "sitios C1–C4 (el depósito en (178; 22)) y orden de liberación C3, C4, C2, C1");
            check(d.CuartelX == 78f && d.CuartelZ == 14f && d.InteligenciaTipo == "radar" && d.InteligenciaX == 206f && d.InteligenciaZ == 82f && d.ParadasAsalto.Count == 3 && d.ParadasAsalto[0].X == -170f,
                "cuartel (78; 14), radar (206; 82) y 3 paradas del tren de asalto (la oeste en x = −170)");
            bool det = true, range = true; int[] seen = new int[6];
            foreach (var p in d.Piezas) { int v = EstacionDato.VariantOf(p.X, p.Z, 6); det &= v == EstacionDato.VariantOf(p.X, p.Z, 6); range &= v >= 0 && v < 6; seen[v]++; }
            int distinct = 0; foreach (var s in seen) if (s > 0) distinct++;
            check(det && range && distinct >= 5, $"la variante por hash de la posición es determinista, cae en 0–5 y reparte las piezas ({distinct} variantes distintas)");
            bool bad = false; try { EstacionDato.FromJson("{\"id\":\"x\",\"sitios\":{},\"orden_liberacion\":[\"C9\"]}"); } catch (FormatException) { bad = true; }
            check(bad, "un orden de liberación con un sitio que no existe es un error de datos");
        }
    }
}
