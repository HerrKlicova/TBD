using System;
using System.Collections.Generic;
using System.Globalization;
using TBD.Units;

namespace TBD.Net
{
    // Dato por estación (Lucía, diseno-red-estaciones.md §2.2; Álvaro, castellon-nivel.md §6): estaciones/<id>.json con las vías, los andenes,
    // el recinto, las puertas, las piezas del kit (pieza, posición, yaw), las carreteras, los sitios y los cinco campos de Lucía (tamaño, función,
    // sitio de inteligencia, paradas de asalto y estado inicial). Una estación nueva es un JSON, no una escena hecha a mano.
    // Lógica pura, sin Unity: coordenadas locales de la zona (256 × 128 m, vía a lo largo de X), en metros, tal como vienen en el JSON.
    // Solo describe el aspecto y los sitios: no entra en la simulación ni en el hash.

    public enum EstacionTamano : byte { Pequena = 0, Estacion = 1, Principal = 2, Aerodromo = 3 }   // Aerodromo: D-N173 (Bétera, tipo «aerodromo» en el JSON)
    public enum EstacionInicio : byte { Propia = 0, Abandonada = 1, Infestada = 2 }

    public struct EstacionPieza { public string Pieza; public float X, Y, Z, Yaw; public int Variante; public float Fondo; }   // Variante −1 = por hash; Fondo 0 = el del módulo
    public struct EstacionPuerta { public string Tipo; public float X, Z; }
    public struct EstacionCarretera { public string Id, Nombre; public float[] Tramo; public bool Habilitada; }   // Tramo: x0, z0, x1, z1, …
    public struct EstacionSitio { public string Id; public float X, Z; }
    public struct EstacionParada { public string Via; public float X, Z; }

    public sealed class EstacionDato
    {
        public string Id, Nombre, Funcion;
        public EstacionTamano Tamano;
        public EstacionInicio EstadoInicial;
        public readonly List<float[]> Vias = new List<float[]>();          // polilíneas: x0, z0, x1, z1, …
        public readonly List<float[]> Andenes = new List<float[]>();       // rectángulos: x0, z0, x1, z1
        public float[] Recinto;                                            // polígono cerrado: x0, z0, x1, z1, …
        public readonly List<EstacionPuerta> Puertas = new List<EstacionPuerta>();
        public readonly List<EstacionPieza> Piezas = new List<EstacionPieza>();
        public readonly List<EstacionCarretera> Carreteras = new List<EstacionCarretera>();
        public readonly List<EstacionSitio> Sitios = new List<EstacionSitio>();
        public float CuartelX = float.NaN, CuartelZ = float.NaN;
        /// <summary>
        /// Estación subterránea (Castellón v0.1, castellon-nivel.md §7; aprobada por Lucía el 8-nov): cota de las vías bajo losa y en trinchera, la losa (su
        /// cara de arriba es la calle, cota 0), las trincheras abiertas y el vestíbulo de calle con la escalera al andén. null si la estación es de superficie.
        /// </summary>
        public Subterranea Sub;
        public sealed class Subterranea { public float CotaVia, Intrados; public float[] Losa; public readonly List<float[]> Trincheras = new List<float[]>(); public float VestX, VestZ, VestYaw, VestAncho, VestFondo; }
        public string InteligenciaTipo; public float InteligenciaX = float.NaN, InteligenciaZ = float.NaN, InteligenciaYaw;
        public readonly List<EstacionParada> ParadasAsalto = new List<EstacionParada>();
        public readonly List<string> OrdenLiberacion = new List<string>();

        public static EstacionDato FromJson(string json)
        {
            var root = Mini.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("estación: el JSON no es un objeto");
            var d = new EstacionDato
            {
                Id = Req<string>(root, "id"),
                Nombre = Opt<string>(root, "nombre") ?? Req<string>(root, "id"),
                Funcion = Opt<string>(root, "funcion"),
                Tamano = Opt<string>(root, "tipo") == "aerodromo" ? EstacionTamano.Aerodromo : ParseTamano(Opt<string>(root, "tamano")),
                EstadoInicial = ParseInicio(Opt<string>(root, "estado_inicial")),
            };
            foreach (var v in List(root, "vias")) d.Vias.Add(Points(v, "vias"));
            if (root.TryGetValue("subterranea", out var subO) && subO is Dictionary<string, object> sd)
            {
                var sub = new Subterranea { CotaVia = Num(sd["cota_via_m"]), Intrados = sd.ContainsKey("intrados_m") ? Num(sd["intrados_m"]) : 6.5f, Losa = Floats(sd["losa"], "losa") };
                foreach (var t in List(sd, "trinchera")) sub.Trincheras.Add(Floats(t, "trinchera"));
                if (sd.TryGetValue("vestibulo", out var vo) && vo is Dictionary<string, object> vd)
                {
                    var vp = Floats(vd["pos"], "vestibulo"); sub.VestX = vp[0]; sub.VestZ = vp.Length > 2 ? vp[2] : vp[1];
                    sub.VestYaw = vd.ContainsKey("yaw") ? Num(vd["yaw"]) : 0f;
                    if (vd.TryGetValue("huella", out var hu)) { var h = Floats(hu, "huella"); sub.VestAncho = h[0]; sub.VestFondo = h[1]; }
                }
                d.Sub = sub;
            }
            foreach (var a in List(root, "andenes")) { var r = Floats(a, "andenes"); if (r.Length != 4) throw new FormatException("andén: hacen falta 4 números"); d.Andenes.Add(r); }
            if (root.ContainsKey("recinto")) d.Recinto = Points(root["recinto"], "recinto");
            foreach (var o in List(root, "puertas")) { var p = Obj(o, "puertas"); var xz = XZ(p["pos"], "puertas"); d.Puertas.Add(new EstacionPuerta { Tipo = Req<string>(p, "tipo"), X = xz[0], Z = xz[1] }); }
            foreach (var o in List(root, "piezas"))
            {
                var p = Obj(o, "piezas"); var pos = Floats(p["pos"], "piezas");
                if (pos.Length != 3) throw new FormatException("pieza: «pos» tiene que ser [x, y, z]");
                d.Piezas.Add(new EstacionPieza { Pieza = Req<string>(p, "pieza"), X = pos[0], Y = pos[1], Z = pos[2], Yaw = p.ContainsKey("yaw") ? Num(p["yaw"]) : 0f, Variante = p.ContainsKey("variante") ? (int)Num(p["variante"]) : -1, Fondo = p.ContainsKey("fondo") ? Num(p["fondo"]) : 0f });
            }
            foreach (var o in List(root, "carreteras"))
            {
                var c = Obj(o, "carreteras");
                d.Carreteras.Add(new EstacionCarretera { Id = Req<string>(c, "id"), Nombre = Opt<string>(c, "nombre"), Tramo = Points(c["tramo"], "carreteras"), Habilitada = c.ContainsKey("habilitada") && c["habilitada"] is bool b && b });
            }
            if (root.TryGetValue("sitios", out var so) && so is Dictionary<string, object> sitios)
                foreach (var kv in sitios) { var xz = XZ(kv.Value, "sitios"); d.Sitios.Add(new EstacionSitio { Id = kv.Key, X = xz[0], Z = xz[1] }); }
            if (root.TryGetValue("sitio_cuartel", out var cu) && cu != null) { var xz = XZ(cu, "sitio_cuartel"); d.CuartelX = xz[0]; d.CuartelZ = xz[1]; }
            if (root.TryGetValue("sitio_inteligencia", out var io) && io is Dictionary<string, object> intel)
            { d.InteligenciaTipo = Opt<string>(intel, "tipo"); var xz = XZ(intel["pos"], "sitio_inteligencia"); d.InteligenciaX = xz[0]; d.InteligenciaZ = xz[1]; d.InteligenciaYaw = intel.ContainsKey("yaw") ? Num(intel["yaw"]) : 0f; }
            foreach (var o in List(root, "paradas_asalto")) { var p = Obj(o, "paradas_asalto"); var xz = XZ(p["pos"], "paradas_asalto"); d.ParadasAsalto.Add(new EstacionParada { Via = Opt<string>(p, "via"), X = xz[0], Z = xz[1] }); }
            foreach (var o in List(root, "orden_liberacion")) d.OrdenLiberacion.Add(o as string ?? throw new FormatException("orden_liberacion: hace falta texto"));
            foreach (var s in d.OrdenLiberacion) if (d.FindSitio(s) < 0) throw new FormatException("orden_liberacion: el sitio «" + s + "» no está en «sitios»");
            return d;
        }

        public int FindSitio(string id) { for (int i = 0; i < Sitios.Count; i++) if (Sitios[i].Id == id) return i; return -1; }

        /// <summary>
        /// Variante determinista de una pieza por su posición (regla de Eugenio: 5 variantes o más por estructura, elegidas por instancia con
        /// semilla). Es la misma que usa Sagunto (PrototypeBootstrap.PlaceVariant): hash de (x·10, z·10) módulo el número de variantes, así una
        /// pieza en el mismo sitio sale igual en las dos estaciones. La función de hash es la de Unity.Mathematics (math.hash de un int2), copiada
        /// aquí en enteros para que también valga fuera de Unity.
        /// </summary>
        public static int VariantOf(float x, float z, int count)
        {
            if (count <= 1) return 0;
            uint h = Int2Hash((int)(x * 10f), (int)(z * 10f));
            return (int)(h % (uint)count);
        }

        // math.hash(int2 v) = csum(asuint(v) * uint2(0x83B58237u, 0x833E3E29u)) + 0xA9D919BFu (Unity.Mathematics 1.x)
        static uint Int2Hash(int a, int b) { unchecked { return (uint)a * 0x83B58237u + (uint)b * 0x833E3E29u + 0xA9D919BFu; } }

        // ---- lectura
        static EstacionTamano ParseTamano(string s)
        {
            switch (s) { case null: case "principal": return EstacionTamano.Principal; case "estacion": return EstacionTamano.Estacion; case "pequena": return EstacionTamano.Pequena; }
            throw new FormatException("tamano: «" + s + "» (vale principal, estacion o pequena)");
        }
        static EstacionInicio ParseInicio(string s)
        {
            switch (s) { case null: case "propia": return EstacionInicio.Propia; case "abandonada": return EstacionInicio.Abandonada; case "infestada": return EstacionInicio.Infestada; }
            throw new FormatException("estado_inicial: «" + s + "» (vale propia, abandonada o infestada)");
        }
        static T Req<T>(Dictionary<string, object> o, string k) where T : class { if (!o.TryGetValue(k, out var v) || !(v is T t)) throw new FormatException("estación: falta «" + k + "»"); return t; }
        static T Opt<T>(Dictionary<string, object> o, string k) where T : class { return o.TryGetValue(k, out var v) ? v as T : null; }
        static List<object> List(Dictionary<string, object> o, string k) { return o.TryGetValue(k, out var v) && v is List<object> l ? l : new List<object>(); }
        static Dictionary<string, object> Obj(object o, string what) { return o as Dictionary<string, object> ?? throw new FormatException(what + ": se esperaba un objeto"); }
        static float Num(object o)
        {
            if (o is long l) return l;
            if (o is double d) return (float)d;
            throw new FormatException("estación: se esperaba un número");
        }
        static float[] Floats(object o, string what)
        {
            var l = o as List<object> ?? throw new FormatException(what + ": se esperaba una lista de números");
            var r = new float[l.Count]; for (int i = 0; i < r.Length; i++) r[i] = Num(l[i]); return r;
        }
        /// <summary>Un punto del plano: [x, z] o [x, y, z] (Álvaro usa los dos) → { x, z }.</summary>
        static float[] XZ(object o, string what)
        {
            var p = Floats(o, what);
            if (p.Length == 2) return p;
            if (p.Length == 3) return new[] { p[0], p[2] };
            throw new FormatException(what + ": un punto es [x, z] o [x, y, z]");
        }

        /// <summary>[[x, z], [x, z], …] → x0, z0, x1, z1, …</summary>
        static float[] Points(object o, string what)
        {
            var l = o as List<object> ?? throw new FormatException(what + ": se esperaba una lista de puntos");
            var r = new float[l.Count * 2];
            for (int i = 0; i < l.Count; i++) { var p = Floats(l[i], what); if (p.Length != 2) throw new FormatException(what + ": cada punto es [x, z]"); r[i * 2] = p[0]; r[i * 2 + 1] = p[1]; }
            return r;
        }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} ({1}, {2}): {3} piezas, {4} vías, {5} sitios", Nombre, Tamano, EstadoInicial, Piezas.Count, Vias.Count, Sitios.Count);
    }
}
