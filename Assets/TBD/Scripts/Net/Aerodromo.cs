using System;
using System.Collections.Generic;
using TBD.Units;

namespace TBD.Net
{
    /// <summary>
    /// Aeródromo (D-N173, Lucía; Bétera elegida con la ficha de Paula y el nivel v0 de Álvaro, estaciones/betera.json). C# puro.
    /// Es una localización propia del mapa global, sin estación de tren: solo se llega por aire. Allí viven los NH90 con sus pelotones de embarque y uno de
    /// guarnición, con munición y combustible propios (50 u y 70 u, sin logística en el MVP).
    /// Tiempos (Lucía y Hugo): preparar y despegar, 20 s reales fijos; vuelo en línea recta a 260 km/h (dato público del NH90, SIN VERIFICAR) con el reloj del
    /// juego; tomar tierra en Bétera, 20 s; rearme, 30 s. Cada salida gasta unas 3 u de combustible y cada rearme 1,8 u de munición (Hugo, betera-existencias.md).
    /// Al bajar del 30 % de munición en la zona, el NH90 vuelve solo a rearmar (D-N173, enmienda la D-N167): lo pide la zona con Volver.
    /// PROTOTIPO: este estado vive en la vista (sesión), no en NetWorld: no entra en el hash ni en el guardado. Para pasarlo a la red, Nacho.
    /// </summary>
    public enum AeronaveEstado : byte { EnBase, Preparando, Ida, EnZona, Volviendo, TomandoTierra, Rearmando }

    public sealed class Aeronave
    {
        public int Id; public string Nombre;
        public AeronaveEstado Estado;
        public int Destino = -1;               // nodo de la red al que va o donde está
        public float Quedan;                   // segundos reales que le quedan a la fase actual
        public int Peloton = -1;               // pelotón de embarque a bordo (índice en AerodromoEstado.Pelotones), −1 sin pelotón
        public int MunicionPm = 1000;          // ‰ de munición a bordo
        public int CargaMilliU;                // D-N190: munición de carga (para el almacén del destino), cargada en Bétera al salir
    }

    public sealed class AerodromoDestino { public string Nombre; public double Km; public int RumboLlegada; }

    public sealed class AerodromoEstado
    {
        public const double NH90Kmh = 260.0;                   // SIN VERIFICAR (Paula); Hugo lo usa así
        public const float PrepararS = 20f, TomarTierraS = 20f, RearmeS = 30f;
        public const int CombustiblePorSalidaMilliU = 3000, MunicionPorRearmeMilliU = 1800;
        public const int CargaPorVueloMilliU = 3000;           // D-N190 (Lucía; cifras de Hugo, curva de Nacho): cada vuelo lleva 3 u desde Bétera
        public const int VolverPm = 300;                       // vuelve solo al bajar del 30 %

        public string Id = "betera", Nombre = "Base «Jaime I» de Bétera";
        public double Lat, Lon, MapX, MapY;
        public int MunicionMilliU = 50_000, CombustibleMilliU = 70_000;
        /// <summary>
        /// Víveres y gente de la base (Lucía, 8-nov: Bétera empieza con 20 u y los consume como las demás). Tasas de sim_economia.py de Hugo: 0,1 u por persona
        /// y día; sin víveres, la gente baja un 5 % al día. 24 personas es SUPUESTO de Hugo (3 pelotones de 8). Lo llama la red al amanecer (ConsumirDia).
        /// </summary>
        public int ViveresMilliU = 20_000, Personas = 24;
        public const int ViveresPorPersonaDiaMilliU = 100, HambrePm = 50;
        public bool Hambre { get; private set; }
        /// <summary>Algún NH90 rearmando: la zona muestra el camión Repostando y el remolque Descargando en P1 (AerodromoZona.Actualizar).</summary>
        public bool AlgunoRearmando { get { foreach (var x in Aeronaves) if (x.Estado == AeronaveEstado.Rearmando) return true; return false; } }
        /// <summary>Lucía, 8-nov: en el MVP Bétera NO gasta sus víveres (lista «no_consumen_en_mvp» de existencias_red.json). Con false, ConsumirDia no hace nada y nunca avisa de hambre.</summary>
        public bool Consume = false;
        public readonly List<Aeronave> Aeronaves = new List<Aeronave>();
        public readonly List<string> Pelotones = new List<string>();        // de embarque (nombres); van con un NH90 o se quedan
        public readonly List<bool> PelotonEnBase = new List<bool>();
        public string Guarnicion = "Guarnición de Bétera";
        public readonly Dictionary<string, AerodromoDestino> Destinos = new Dictionary<string, AerodromoDestino>();

        /// <summary>Lee el bloque «nodo» de estaciones/betera.json (lat, lon, mapa_xy, destinos con km y rumbo, plantel).</summary>
        public static AerodromoEstado FromJson(string json)
        {
            var root = Mini.Parse(json) as Dictionary<string, object> ?? throw new FormatException("aeródromo: el JSON no es un objeto");
            var a = new AerodromoEstado();
            if (root.TryGetValue("id", out var id) && id is string sid) a.Id = sid;
            if (root.TryGetValue("nombre", out var nm) && nm is string snm) a.Nombre = snm;
            var nodo = root.TryGetValue("nodo", out var n) ? n as Dictionary<string, object> : null;
            if (nodo == null) throw new FormatException("aeródromo: falta «nodo»");
            a.Lat = Num(nodo["lat"]); a.Lon = Num(nodo["lon"]);
            var xy = nodo["mapa_xy"] as List<object>; a.MapX = Num(xy[0]); a.MapY = Num(xy[1]);
            if (nodo.TryGetValue("destinos", out var ds) && ds is Dictionary<string, object> dd)
                foreach (var kv in dd)
                {
                    var o = kv.Value as Dictionary<string, object>; if (o == null) continue;
                    int rumbo = o.TryGetValue("rumbo", out var r) ? (int)Math.Round(Num(r)) : 0;
                    a.Destinos[kv.Key] = new AerodromoDestino { Nombre = kv.Key, Km = Num(o["km"]), RumboLlegada = (rumbo + 180) % 360 };
                }
            int nh = 2, emb = 2;
            if (nodo.TryGetValue("plantel", out var pl) && pl is Dictionary<string, object> pd)
            {
                if (pd.TryGetValue("NH90", out var x)) nh = (int)Num(x);
                if (pd.TryGetValue("pelotones_embarque", out var y)) emb = (int)Num(y);
            }
            for (int i = 0; i < nh; i++) a.Aeronaves.Add(new Aeronave { Id = i, Nombre = "NH90 " + (i + 1) });
            for (int i = 0; i < emb; i++) { a.Pelotones.Add("Fusileros " + (i + 1) + " (Bétera)"); a.PelotonEnBase.Add(true); }
            return a;
        }

        /// <summary>Existencias de partida del bloque «aerodromos» de existencias_red.json de Hugo (munición y combustible en u; no se reponen en el MVP).</summary>
        public bool AplicarExistencias(string json)
        {
            if (json == null) return false;
            var root = Mini.Parse(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("aerodromos", out var a) || !(a is List<object> l)) return false;
            foreach (var o in l)
            {
                var d = o as Dictionary<string, object>; if (d == null || !(d.TryGetValue("id", out var id) && (string)id == Id)) continue;
                var r = d["reserva"] as Dictionary<string, object>;
                MunicionMilliU = (int)Math.Round(Num(r["municion_u"]) * 1000); CombustibleMilliU = (int)Math.Round(Num(r["combustible_u"]) * 1000);
                if (r.TryGetValue("viveres_u", out var v)) ViveresMilliU = (int)Math.Round(Num(v) * 1000);
                if (d.TryGetValue("personas", out var pe) && Num(pe) > 0) Personas = (int)Math.Round(Num(pe));
                Consume = true;
                if (root.TryGetValue("viveres", out var vo) && vo is Dictionary<string, object> vd && vd.TryGetValue("no_consumen_en_mvp", out var nc) && nc is List<object> ncl)
                    foreach (var x in ncl) if (x as string == Id) Consume = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Un día, con la misma regla que NetWorld.ConsumeFood de Nacho (D-N176): come la gente (0,1 u por persona); si no llega, se come lo que haya y la gente
        /// baja el 5 % por la fracción que falta (redondeo hacia abajo). Devuelve lo comido en milésimas.
        /// </summary>
        public int ConsumirDia()
        {
            if (!Consume || Personas <= 0) { Hambre = false; return 0; }
            int pide = Personas * ViveresPorPersonaDiaMilliU, come = Math.Min(pide, Math.Max(0, ViveresMilliU));
            ViveresMilliU -= come; Hambre = come < pide;
            if (Hambre) Personas -= (int)((long)Personas * HambrePm * (pide - come) / (1000L * pide));
            return come;
        }

        static double Num(object o) { return o is double d ? d : o is long l ? l : o is int i ? i : Convert.ToDouble(o, System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>Segundos reales de vuelo para esa distancia, con el reloj del juego (NetConst.HourTicks a 30 Hz).</summary>
        public static float VueloS(double km)
        {
            double horasJuego = km / NH90Kmh;
            double relojX = 3600.0 / NetConst.HourTicks * NetConst.TickRate;      // segundos de juego por segundo real (×103 con el reloj de Nacho)
            return (float)(horasJuego * 3600.0 / relojX);
        }

        public double Km(string destino) { return Destinos.TryGetValue(destino, out var d) ? d.Km : -1; }
        /// <summary>Segundos reales desde «Enviar» hasta que entra en la zona del destino (preparar + vuelo).</summary>
        public float EtaIdaS(string destino) { double km = Km(destino); return km < 0 ? -1 : PrepararS + VueloS(km); }

        /// <summary>Por qué no puede salir (null si puede).</summary>
        public string PorQueNo(int aero, string destino)
        {
            if (aero < 0 || aero >= Aeronaves.Count) return "No hay aparato";
            var a = Aeronaves[aero];
            if (a.Estado != AeronaveEstado.EnBase) return "No está en la base";
            if (Km(destino) < 0) return "Destino sin ruta aérea";
            if (CombustibleMilliU < CombustiblePorSalidaMilliU) return "No queda combustible";
            return null;
        }

        /// <summary>Manda el NH90 al destino. Si conPeloton y queda alguno en la base, sube uno. Gasta el combustible de la salida.</summary>
        public string Enviar(int aero, string destino, int nodoDestino, bool conPeloton)
        {
            var r = PorQueNo(aero, destino); if (r != null) return r;
            var a = Aeronaves[aero];
            a.Peloton = -1;
            if (conPeloton) for (int i = 0; i < Pelotones.Count; i++) if (PelotonEnBase[i]) { PelotonEnBase[i] = false; a.Peloton = i; break; }
            CombustibleMilliU -= CombustiblePorSalidaMilliU;
            a.CargaMilliU = Math.Min(CargaPorVueloMilliU, Math.Max(0, MunicionMilliU)); MunicionMilliU -= a.CargaMilliU;      // D-N190: se carga en Bétera, de su reserva
            a.Estado = AeronaveEstado.Preparando; a.Quedan = PrepararS; a.Destino = nodoDestino; DestinoNombre[aero] = destino;
            return null;
        }
        readonly Dictionary<int, string> DestinoNombre = new Dictionary<int, string>();

        /// <summary>La zona: el NH90 ha bajado del 30 % (o el jugador lo manda a rearmar). Vuelve solo a Bétera.</summary>
        public bool Volver(int aero)
        {
            if (aero < 0 || aero >= Aeronaves.Count) return false;
            var a = Aeronaves[aero]; if (a.Estado != AeronaveEstado.EnZona) return false;
            DestinoNombre.TryGetValue(aero, out var dn);
            a.Estado = AeronaveEstado.Volviendo; a.Quedan = VueloS(Math.Max(0, Km(dn ?? "")));
            return true;
        }

        /// <summary>El pelotón que llevaba ha bajado en la zona: deja de ir a bordo (sigue siendo de Bétera, pero ya no está en la base).</summary>
        public void PelotonBajado(int aero) { if (aero >= 0 && aero < Aeronaves.Count) Aeronaves[aero].Peloton = -1; }

        /// <summary>Segundos reales hasta el siguiente hito del aparato (llegada a la zona o vuelta a estar listo en la base). −1 si no hay.</summary>
        public float Eta(int aero)
        {
            var a = Aeronaves[aero];
            switch (a.Estado)
            {
                case AeronaveEstado.Preparando: { DestinoNombre.TryGetValue(aero, out var dn); return a.Quedan + VueloS(Math.Max(0, Km(dn ?? ""))); }
                case AeronaveEstado.Ida: return a.Quedan;
                case AeronaveEstado.Volviendo: return a.Quedan + TomarTierraS + RearmeS;
                case AeronaveEstado.TomandoTierra: return a.Quedan + RearmeS;
                case AeronaveEstado.Rearmando: return a.Quedan;
                default: return -1;
            }
        }

        public struct Suceso { public int Aero; public AeronaveEstado Nuevo; public int Destino; }

        /// <summary>Avanza dt segundos reales. Devuelve los cambios de fase (la vista avisa; la zona hace entrar el NH90 al pasar a EnZona).</summary>
        public List<Suceso> Avanzar(float dt)
        {
            var ev = new List<Suceso>();
            foreach (var a in Aeronaves)
            {
                if (a.Estado == AeronaveEstado.EnBase || a.Estado == AeronaveEstado.EnZona) continue;
                a.Quedan -= Math.Max(0f, dt);
                while (a.Quedan <= 0f && a.Estado != AeronaveEstado.EnBase && a.Estado != AeronaveEstado.EnZona)
                {
                    float sobra = -a.Quedan;
                    switch (a.Estado)
                    {
                        case AeronaveEstado.Preparando: { DestinoNombre.TryGetValue(a.Id, out var dn); a.Estado = AeronaveEstado.Ida; a.Quedan = VueloS(Math.Max(0, Km(dn ?? ""))) - sobra; break; }
                        case AeronaveEstado.Ida: a.Estado = AeronaveEstado.EnZona; a.Quedan = 0; break;
                        case AeronaveEstado.Volviendo: a.Estado = AeronaveEstado.TomandoTierra; a.Quedan = TomarTierraS - sobra; break;
                        case AeronaveEstado.TomandoTierra:
                            a.Estado = AeronaveEstado.Rearmando; a.Quedan = RearmeS - sobra;
                            if (a.Peloton >= 0) { PelotonEnBase[a.Peloton] = true; a.Peloton = -1; }      // si volvió con el pelotón a bordo, se queda en la base
                            break;
                        case AeronaveEstado.Rearmando:
                            int falta = (1000 - a.MunicionPm) * MunicionPorRearmeMilliU / 1000, pone = Math.Min(falta, MunicionMilliU);
                            MunicionMilliU -= pone; a.MunicionPm += falta > 0 ? (1000 - a.MunicionPm) * pone / falta : 0;
                            a.Estado = AeronaveEstado.EnBase; a.Quedan = 0; a.Destino = -1; break;
                    }
                    ev.Add(new Suceso { Aero = a.Id, Nuevo = a.Estado, Destino = a.Destino });
                }
            }
            return ev;
        }

        public static string Mmss(float s) { if (s < 0) return "—"; int t = (int)Math.Ceiling(s); return $"{t / 60}:{t % 60:00}"; }
    }

    /// <summary>
    /// Plantel de cada estación al entrar en su zona (D-N173: «en todas las estaciones salen las mismas unidades, helicóptero incluido»). Datos para la zona
    /// (Marcos), que crea las unidades. Sagunto, sin NH90 (el aire vive en Bétera). PROTOTIPO: cifras de Lucía y Hugo; Nules solo con el tutorial (D-N172).
    /// </summary>
    public static class Plantel
    {
        public sealed class Lista { public string[] Pelotones = new string[0]; public string[] Vehiculos = new string[0]; public int NH90; }

        /// <summary>Un pelotón de la zona: tipo, nombre visible y sitio de partida (mm) con su rumbo.</summary>
        public struct Puesto { public PlatoonKind Tipo; public string Nombre; public int X, Y, Rumbo; public Formation Formacion; public int CotaMm, Hombres; }   // Hombres: 0 = el pelotón entero; «fusileros:4» en De() saca 4 (D-N192, Lucía)   // CotaMm: suelo bajo el pelotón (azotea, banqueta)
        /// <summary>Un vehículo de la zona: tipo (dragon, vamtac), nombre visible y sitio (m) con su rumbo.</summary>
        public struct Coche { public string Tipo, Nombre; public float X, Z, Yaw; }

        // los cinco sitios de partida de Sagunto (los de siempre, hacia la puerta oeste), en el orden del plantel
        static readonly int[,] SitioPel = { { 88000, 54000 }, { 94000, 64000 }, { 98000, 57000 }, { 98000, 72000 }, { 88000, 76000 } };
        static readonly float[] SitioVehX = { 99f, 88f, 80f, 72f };
        const int Oeste = 32768, Norte = 16384, Sur = 49152;

        /// <summary>
        /// D-N183 y reglas de Lucía (decisiones-noche 09-11 07:40): cada estación arranca con SU guarnición, colocada en sus puertas y muros. Puestos de Álvaro,
        /// docs/niveles/guarnicion_puestos_v1.json (9-nov 07:55): x, y en mm de la zona; rumbo bam16 (0 este, 16384 norte); vehículos en m con yaw de Unity (0 norte).
        /// El orden de cada lista es el del plantel de De(). Lo que pase de la tabla (el pelotón del capítulo 3 del tutorial en Nules) usa los sitios viejos.
        /// </summary>
        struct PuestoDato { public string Tipo; public int X, Y, Rumbo, CotaMm; public Formation F; public PuestoDato(string t, int x, int y, int r, Formation f, int cotaMm = 0) { Tipo = t; X = x; Y = y; Rumbo = r; F = f; CotaMm = cotaMm; } }
        static readonly Dictionary<string, PuestoDato[]> PuestosPel = new Dictionary<string, PuestoDato[]>
        {
            { "Sagunto", new[] {
                new PuestoDato("fusileros", 71500, 58000, Oeste, Formation.Linea, 400),        // banqueta oeste, al sur de la puerta de vía
                new PuestoDato("ametralladores", 71500, 71000, Oeste, Formation.Linea, 400),   // banqueta oeste, al norte de la puerta de vía
                new PuestoDato("granaderos", 99000, 69000, Oeste, Formation.Cuna),        // reserva en el andén, delante del edificio de viajeros
                new PuestoDato("tiradores", 84500, 74300, Oeste, Formation.Linea, 4750),  // v1.1 (Lucía 07:41): azotea del ala oeste, a 4,75 m
                new PuestoDato("zapadores", 71500, 50000, Oeste, Formation.Linea, 400) } },    // ala sur
            { "Nules", new[] {
                new PuestoDato("fusileros", 71500, 58500, Oeste, Formation.Linea, 400),        // v1.2 (Nules opción A): banqueta oeste
                new PuestoDato("ametralladores", 84500, 76300, Oeste, Formation.Linea, 4750),  // v1.2: azotea del ala oeste
                new PuestoDato("fusileros", 124000, 75500, 0, Formation.Linea),          // D-N194 (Lucía d27276b; Álvaro 7597be0, Plantel v1.4): los 4 fusileros en la fachada este del edificio, mirando al este (la D-N192 los completa a 8 con la escolta)
                new PuestoDato("fusileros", 71500, 72000, Oeste, Formation.Linea, 400) } }, // v1.2 tutorial_extra: el pelotón del capítulo 3
            { "Castellón", new[] {
                new PuestoDato("fusileros", 72500, 60000, Oeste, Formation.Linea),        // borde oeste de la losa sobre la boca (mitad sur)
                new PuestoDato("ametralladores", 72500, 69000, Oeste, Formation.Linea),   // v1.1: ametralladores en la mitad norte de la boca
                new PuestoDato("fusileros", 114500, 81000, Oeste, Formation.Cuna) } },    // reserva junto al vestíbulo
            { "Bétera", new[] {
                new PuestoDato("fusileros", 120000, 84000, Norte, Formation.Cuna),        // embarque del NH90 de P1
                new PuestoDato("fusileros", 31000, 85000, Norte, Formation.Cuna),         // embarque del NH90 de P2
                new PuestoDato("fusileros", 36000, 20000, Sur, Formation.Linea) } },      // guarnición en el vial X 36
        };
        static readonly Dictionary<string, Coche[]> PuestosVeh = new Dictionary<string, Coche[]>
        {
            { "Sagunto", new[] { new Coche { Tipo = "dragon", X = 99f, Z = 51f, Yaw = 180f }, new Coche { Tipo = "vamtac", X = 91.5f, Z = 51f, Yaw = 180f }, new Coche { Tipo = "vamtac", X = 106.5f, Z = 51f, Yaw = 180f } } },   // dentro de la puerta sur
            { "Castellón", new[] { new Coche { Tipo = "vamtac", X = 70f, Z = 29f, Yaw = 270f } } },     // dentro de la puerta de vehículos (64; 29)
        };
        static string Clave(string estacion) { return estacion == "betera" ? "Bétera" : estacion ?? ""; }

        /// <summary>
        /// D-N173: los pelotones que salen en la zona de esa estación, con su sitio (el de los cinco de Sagunto, por orden). Sin plantel, la lista va vacía:
        /// la zona no se inventa unidades (el fallo que vio Eugenio: en todas salían las mismas).
        /// </summary>
        public static List<Puesto> Pelotones(string estacion, bool tutorial = false)
        {
            var r = new List<Puesto>(); var l = De(estacion, tutorial); var cuenta = new Dictionary<PlatoonKind, int>();
            for (int i = 0; i < l.Pelotones.Length; i++)
            {
                int hom = 0; { int dp = l.Pelotones[i].IndexOf(':'); if (dp > 0) int.TryParse(l.Pelotones[i].Substring(dp + 1), out hom); }
                var k = Tipo(l.Pelotones[i]); cuenta[k] = cuenta.TryGetValue(k, out var c) ? c + 1 : 1;
                int si = i % SitioPel.GetLength(0);
                string nombre = Nombre(k); if (Array.FindAll(l.Pelotones, x => Tipo(x) == k).Length > 1) nombre += " " + cuenta[k];
                if (PuestosPel.TryGetValue(Clave(estacion), out var tabla) && i < tabla.Length) { var t = tabla[i]; r.Add(new Puesto { Tipo = k, Nombre = nombre, X = t.X, Y = t.Y, Rumbo = t.Rumbo, Formacion = t.F, CotaMm = t.CotaMm, Hombres = hom }); continue; }
                r.Add(new Puesto { Tipo = k, Nombre = nombre, X = SitioPel[si, 0], Y = SitioPel[si, 1], Rumbo = Oeste, Formacion = i < 2 ? Formation.Linea : Formation.Cuna, Hombres = hom });
            }
            return r;
        }

        /// <summary>D-N173: los vehículos terrestres de la zona de esa estación (el NH90 sale de Bétera, no de aquí).</summary>
        public static List<Coche> Vehiculos(string estacion, bool tutorial = false)
        {
            var r = new List<Coche>(); var l = De(estacion, tutorial); int nv = 0;
            for (int i = 0; i < l.Vehiculos.Length; i++)
            {
                string t = l.Vehiculos[i];
                string nombre = t == "dragon" ? "Dragón" : "VAMTAC " + (++nv);
                if (PuestosVeh.TryGetValue(Clave(estacion), out var tabla) && i < tabla.Length) { var c = tabla[i]; r.Add(new Coche { Tipo = t, Nombre = nombre, X = c.X, Z = c.Z, Yaw = c.Yaw }); continue; }
                r.Add(new Coche { Tipo = t, Nombre = nombre, X = SitioVehX[i % SitioVehX.Length], Z = 53f, Yaw = 180f });
            }
            return r;
        }

        public static PlatoonKind Tipo(string s)
        {
            int dp = s.IndexOf(':'); if (dp > 0) s = s.Substring(0, dp);
            switch (s) { case "ametralladores": return PlatoonKind.Ametralladores; case "granaderos": return PlatoonKind.Granaderos; case "tiradores": return PlatoonKind.Tiradores; case "zapadores": return PlatoonKind.Zapadores; default: return PlatoonKind.Fusileros; }
        }

        public static string Nombre(PlatoonKind k) { return k == PlatoonKind.Ametralladores ? "Ametralladores" : k == PlatoonKind.Granaderos ? "Granaderos" : k == PlatoonKind.Tiradores ? "Tiradores" : k == PlatoonKind.Zapadores ? "Zapadores" : "Fusileros"; }

        public static Lista De(string estacion, bool tutorial = false)
        {
            switch (estacion)
            {
                case "Sagunto": return new Lista { Pelotones = new[] { "fusileros", "ametralladores", "granaderos", "tiradores", "zapadores" }, Vehiculos = new[] { "dragon", "vamtac", "vamtac" }, NH90 = 0 };
                case "Nules": return tutorial ? new Lista { Pelotones = new[] { "fusileros", "ametralladores", "fusileros:4", "fusileros" } } : new Lista { Pelotones = new[] { "fusileros", "ametralladores", "fusileros:4" } };   // D-N192 (Lucía, 10-nov): guarnición de 16 (+4 fusileros en la banqueta de la vía); Nacho mide la cifra definitiva   // Hugo (nules.json, 9-nov): la guarnición de 12 (fusileros + ametralladores); con el tutorial, más el pelotón del capítulo 3
                case "betera": case "Bétera": return new Lista { Pelotones = new[] { "fusileros", "fusileros", "fusileros" }, NH90 = 2 };   // 2 de embarque y 1 de guarnición
                case "Castellón": return new Lista { Pelotones = new[] { "fusileros", "ametralladores", "fusileros" }, Vehiculos = new[] { "vamtac" } };   // D-N183: propia al empezar; 2 fusileros y 1 ametralladores (Lucía 07:41) y 1 VAMTAC
                default: return new Lista();      // el resto: sin plantel propio
            }
        }
    }
}

namespace TBD.Net
{
    /// <summary>
    /// F1 paso 2 del plan del núcleo (Nacho): el NH90 dentro de NetWorld. Todo en enteros y en ticks de red, con órdenes de la cola (AirSend, AirReturn,
    /// AirDrop, AirPickup, AirAmmo), en el hash y en el guardado. Sustituye a AerodromoEstado.Avanzar (segundos reales con float), que no es determinista.
    /// Tiempos (Lucía, 10-nov): preparar 600 ticks, tomar tierra 600, rearme 900; a ×4 duran la cuarta parte, como todo. Vuelo a 260 km/h de juego
    /// (≈ 248 m por tick). Coste (Hugo): 3 u de combustible por salida, 1,8 u de munición por rearme, 3 u de carga por vuelo (D-N190) que se dejan en el
    /// almacén del destino al llegar. Vuelta automática al bajar del 30 % de munición: la decide la red.
    /// El hash solo cuenta lo que se aparta del estado inicial (aparatos en la base, existencias de partida): sin usar el aire, el hash de la red no cambia.
    /// </summary>
    public enum AirPhase : byte { EnBase, Preparando, Ida, EnZona, Volviendo, TomandoTierra, Rearmando }

    public sealed class AirCraft
    {
        public int Id;
        public AirPhase Phase;
        public int DestNode = -1;                  // nodo de destino, o −1 si va a un punto libre
        public int DestXMm, DestYMm;               // destino en mm del mapa de operaciones (también para un nodo)
        public long PhaseStartTick, PhaseEndTick;  // fase actual; EnBase y EnZona no acaban solas (PhaseEndTick = −1)
        public int FlightTicks;                    // ticks de vuelo de la salida actual (ida y vuelta iguales)
        public int Platoon = -1;                   // pelotón de embarque a bordo (índice en AirWing.PlatoonAt), −1 sin pelotón
        public int AmmoPm = 1000;                  // ‰ de munición a bordo
        public int CargoMilliU;                    // carga de munición para el almacén del destino (D-N190)
    }

    public sealed class AirWing
    {
        public const int PrepTicks = 600, LandTicks = 600, RearmTicks = 900;
        public const long SpeedMmPerHour = 260_000_000;                 // 260 km/h (SIN VERIFICAR, Paula)
        public const int MmPerCell = 3_500_000;                         // una casilla del mapa de operaciones ≈ 3,5 km (Bétera–Sagunto 18,1 km en 5,4 casillas)
        public const int FuelPerSortieMilliU = 3000, AmmoPerRearmMilliU = 1800, CargoPerSortieMilliU = 3000, ReturnPm = 300;
        public const int AtBase = -1, Aboard = -2;                      // PlatoonAt: en la base, a bordo; ≥ 0 = en ese nodo; ≤ −3 = en un punto libre (−3 − aparato)

        public int BaseXMm, BaseYMm;
        public int AmmoMilliU, FuelMilliU;
        readonly int _ammo0, _fuel0;
        public readonly List<AirCraft> Craft = new List<AirCraft>();
        public readonly int[] PlatoonAt;                                // dónde está cada pelotón de embarque
        /// <summary>Distancias publicadas (estacion_betera.json, «destinos»): nodo → mm. Si un nodo no está, se usa la distancia en casillas.</summary>
        public readonly SortedDictionary<int, int> RouteMm = new SortedDictionary<int, int>();
        /// <summary>Último aviso de la red para la vista (no entra en el hash): por qué no se cumplió una orden.</summary>
        public string LastRefusal;

        public AirWing(int baseXMm, int baseYMm, int aircraft, int platoons, int ammoMilliU, int fuelMilliU)
        {
            BaseXMm = baseXMm; BaseYMm = baseYMm; AmmoMilliU = _ammo0 = ammoMilliU; FuelMilliU = _fuel0 = fuelMilliU;
            for (int i = 0; i < aircraft; i++) Craft.Add(new AirCraft { Id = i, PhaseEndTick = -1 });
            PlatoonAt = new int[platoons]; for (int i = 0; i < platoons; i++) PlatoonAt[i] = AtBase;
        }

        /// <summary>Bétera del prototipo (estacion_betera.json: mapa_xy 48;75, 2 NH90, 2 pelotones de embarque, 50 u de munición y 70 de combustible; Sagunto 18,1 km, Nules 37,4 km).</summary>
        public static AirWing Betera(RailGraph g)
        {
            var a = new AirWing(48 * MmPerCell, 75 * MmPerCell, 2, 2, 50_000, 70_000);
            foreach (var n in g.Nodes) { if (n.Name == "Sagunto") a.RouteMm[n.Id] = 18_100_000; else if (n.Name == "Nules") a.RouteMm[n.Id] = 37_400_000; }
            return a;
        }

        public int DistanceMm(RailGraph g, int node, int xMm, int yMm)
        {
            if (node >= 0 && RouteMm.TryGetValue(node, out var d)) return d;
            long dx = xMm - BaseXMm, dy = yMm - BaseYMm; return (int)IMath.Isqrt(dx * dx + dy * dy);
        }
        public static int TicksFor(int mm) { return (int)(((long)mm * NetConst.HourTicks + SpeedMmPerHour - 1) / SpeedMmPerHour); }

        /// <summary>Por qué no puede salir (null si puede). Lo usa la vista para el botón y la red al aplicar la orden.</summary>
        public string WhyNot(RailGraph g, int craft, int node)
        {
            if (craft < 0 || craft >= Craft.Count) return "No hay aparato";
            if (Craft[craft].Phase != AirPhase.EnBase) return "No está en la base";
            if (node >= g.Nodes.Count) return "Destino no válido";
            if (FuelMilliU < FuelPerSortieMilliU) return "No queda combustible";
            return null;
        }

        public void Send(NetWorld w, int craft, int node, int xMm, int yMm, bool withPlatoon)
        {
            var why = WhyNot(w.Graph, craft, node); if (why != null) { LastRefusal = why; return; }
            var a = Craft[craft];
            if (node >= 0) { xMm = w.Graph.Nodes[node].XMeters * MmPerCell; yMm = w.Graph.Nodes[node].YMeters * MmPerCell; }
            a.DestNode = node; a.DestXMm = xMm; a.DestYMm = yMm; a.FlightTicks = Math.Max(1, TicksFor(DistanceMm(w.Graph, node, xMm, yMm)));
            a.Platoon = -1;
            if (withPlatoon) for (int p = 0; p < PlatoonAt.Length; p++) if (PlatoonAt[p] == AtBase) { PlatoonAt[p] = Aboard; a.Platoon = p; break; }
            FuelMilliU -= FuelPerSortieMilliU;
            a.CargoMilliU = Math.Min(CargoPerSortieMilliU, Math.Max(0, AmmoMilliU)); AmmoMilliU -= a.CargoMilliU;
            Enter(a, AirPhase.Preparando, w.Tick, PrepTicks);
        }

        public void Return(NetWorld w, int craft)
        {
            if (craft < 0 || craft >= Craft.Count) return;
            var a = Craft[craft];
            if (a.Phase == AirPhase.EnZona) Enter(a, AirPhase.Volviendo, w.Tick, a.FlightTicks);
            else if (a.Phase == AirPhase.Ida) { long hecho = w.Tick - a.PhaseStartTick; Enter(a, AirPhase.Volviendo, w.Tick, (int)Math.Max(1, hecho)); }      // da la vuelta en el aire: tarda lo que llevaba
            else if (a.Phase == AirPhase.Preparando) { Enter(a, AirPhase.EnBase, w.Tick, -1); FuelMilliU += FuelPerSortieMilliU; AmmoMilliU += a.CargoMilliU; a.CargoMilliU = 0; if (a.Platoon >= 0) { PlatoonAt[a.Platoon] = AtBase; a.Platoon = -1; } }
        }

        /// <summary>Desembarca el pelotón en el destino. La zona comprueba antes que no hay zombis a menos de 30 m (si los hay, no emite la orden).</summary>
        public void Drop(int craft)
        {
            if (craft < 0 || craft >= Craft.Count) return;
            var a = Craft[craft]; if (a.Phase != AirPhase.EnZona || a.Platoon < 0) { LastRefusal = "No hay pelotón a bordo en la zona"; return; }
            PlatoonAt[a.Platoon] = a.DestNode >= 0 ? a.DestNode : -3 - a.Id; a.Platoon = -1;
        }

        public void Pickup(int craft, int platoon)
        {
            if (craft < 0 || craft >= Craft.Count || platoon < 0 || platoon >= PlatoonAt.Length) return;
            var a = Craft[craft]; int aqui = a.DestNode >= 0 ? a.DestNode : -3 - a.Id;
            if (a.Phase != AirPhase.EnZona || a.Platoon >= 0 || PlatoonAt[platoon] != aqui) { LastRefusal = "El pelotón no está donde el aparato"; return; }
            PlatoonAt[platoon] = Aboard; a.Platoon = platoon;
        }

        /// <summary>La zona informa de la munición que le queda al aparato (‰). Solo baja: la munición se repone en Bétera.</summary>
        public void SetAmmo(int craft, int pm) { if (craft >= 0 && craft < Craft.Count) Craft[craft].AmmoPm = Math.Max(0, Math.Min(Craft[craft].AmmoPm, pm)); }

        static void Enter(AirCraft a, AirPhase p, long tick, int ticks) { a.Phase = p; a.PhaseStartTick = tick; a.PhaseEndTick = ticks < 0 ? -1 : tick + ticks; }

        /// <summary>Un tick de red (después de las órdenes y de las noches).</summary>
        public void Step(NetWorld w)
        {
            foreach (var a in Craft)
            {
                if (a.Phase == AirPhase.EnZona && a.AmmoPm < ReturnPm) Enter(a, AirPhase.Volviendo, w.Tick, a.FlightTicks);      // D-N173: vuelve solo a rearmar
                while (a.PhaseEndTick >= 0 && w.Tick >= a.PhaseEndTick)
                {
                    long t = a.PhaseEndTick;
                    switch (a.Phase)
                    {
                        case AirPhase.Preparando: Enter(a, AirPhase.Ida, t, a.FlightTicks); break;
                        case AirPhase.Ida:
                            Enter(a, AirPhase.EnZona, t, -1);
                            if (a.DestNode >= 0 && a.CargoMilliU > 0)                                                          // D-N190: la carga al almacén del destino
                            { w.AirDeliver(a.DestNode, a.CargoMilliU / 1000); a.CargoMilliU %= 1000; }                        // lo que no llega a 1 u vuelve a Bétera
                            break;                                                                                            // punto libre: estacionario; la carga vuelve con él
                        case AirPhase.Volviendo: Enter(a, AirPhase.TomandoTierra, t, LandTicks); break;
                        case AirPhase.TomandoTierra:
                            Enter(a, AirPhase.Rearmando, t, RearmTicks);
                            if (a.Platoon >= 0) { PlatoonAt[a.Platoon] = AtBase; a.Platoon = -1; }
                            AmmoMilliU += a.CargoMilliU; a.CargoMilliU = 0;                                                   // la carga que no se dejó vuelve al almacén de Bétera
                            break;
                        case AirPhase.Rearmando:
                            int falta = (1000 - a.AmmoPm) * AmmoPerRearmMilliU / 1000, pone = Math.Min(falta, AmmoMilliU);
                            AmmoMilliU -= pone; a.AmmoPm += falta > 0 ? (1000 - a.AmmoPm) * pone / falta : 0;
                            Enter(a, AirPhase.EnBase, t, -1); a.DestNode = -1; break;
                    }
                }
            }
        }

        /// <summary>Posición en el mapa (mm), para la vista: interpolada en Ida y Volviendo, en el destino en EnZona y en la base el resto.</summary>
        public void Position(AirCraft a, long tick, out int xMm, out int yMm)
        {
            long num = 0, den = 1;
            if (a.Phase == AirPhase.Ida || a.Phase == AirPhase.Volviendo) { den = Math.Max(1, a.PhaseEndTick - a.PhaseStartTick); num = Math.Min(den, Math.Max(0, tick - a.PhaseStartTick)); if (a.Phase == AirPhase.Volviendo) num = den - num; }
            else if (a.Phase == AirPhase.EnZona) num = den;
            xMm = (int)(BaseXMm + (a.DestXMm - BaseXMm) * num / den); yMm = (int)(BaseYMm + (a.DestYMm - BaseYMm) * num / den);
        }

        /// <summary>Ticks hasta el siguiente hito (llegada a la zona, o listo en la base). −1 si no hay.</summary>
        public long Eta(AirCraft a, long tick)
        {
            long q = a.PhaseEndTick < 0 ? 0 : Math.Max(0, a.PhaseEndTick - tick);
            switch (a.Phase)
            {
                case AirPhase.Preparando: return q + a.FlightTicks;
                case AirPhase.Ida: return q;
                case AirPhase.Volviendo: return q + LandTicks + RearmTicks;
                case AirPhase.TomandoTierra: return q + RearmTicks;
                case AirPhase.Rearmando: return q;
                default: return -1;
            }
        }

        public ulong Hash()
        {
            ulong h = 0;
            if (AmmoMilliU != _ammo0) h += NetWorld.Mix(0xA1A0UL ^ ((ulong)(uint)AmmoMilliU << 16));
            if (FuelMilliU != _fuel0) h += NetWorld.Mix(0xA1F0UL ^ ((ulong)(uint)FuelMilliU << 16));
            foreach (var a in Craft)
            {
                if (a.Phase == AirPhase.EnBase && a.AmmoPm == 1000 && a.Platoon < 0 && a.CargoMilliU == 0 && a.PhaseStartTick == 0) continue;
                h += NetWorld.Mix(((ulong)(uint)a.Id << 56) ^ ((ulong)a.Phase << 48) ^ ((ulong)(uint)(a.DestNode + 1) << 32) ^ (ulong)(uint)a.AmmoPm);
                h += NetWorld.Mix(((ulong)(uint)a.Id << 60) ^ (ulong)a.PhaseEndTick ^ ((ulong)(uint)a.FlightTicks << 40) ^ 0xA15EUL);
                h += NetWorld.Mix(((ulong)(uint)a.Id << 58) ^ ((ulong)(uint)a.DestXMm << 24) ^ (ulong)(uint)a.DestYMm ^ ((ulong)(uint)(a.Platoon + 1) << 52) ^ ((ulong)(uint)a.CargoMilliU << 12));
            }
            for (int p = 0; p < PlatoonAt.Length; p++) if (PlatoonAt[p] != AtBase) h += NetWorld.Mix(0xA1B0UL ^ ((ulong)(uint)p << 40) ^ (ulong)(uint)(PlatoonAt[p] + 1000));
            return h;
        }

        public void SaveState(SaveWriter w)
        {
            w.Int(AmmoMilliU); w.Int(FuelMilliU); w.Int(Craft.Count);
            foreach (var a in Craft) { w.Byte((byte)a.Phase); w.Int(a.DestNode); w.Int(a.DestXMm); w.Int(a.DestYMm); w.Long(a.PhaseStartTick); w.Long(a.PhaseEndTick); w.Int(a.FlightTicks); w.Int(a.Platoon); w.Int(a.AmmoPm); w.Int(a.CargoMilliU); }
            w.Ints(PlatoonAt);
        }

        public void LoadState(SaveReader r)
        {
            AmmoMilliU = r.Int(); FuelMilliU = r.Int(); int n = r.Int();
            if (n != Craft.Count) throw new SaveException(SaveError.TopologyMismatch, "aparatos de Bétera");
            foreach (var a in Craft) { a.Phase = (AirPhase)r.Byte(); a.DestNode = r.Int(); a.DestXMm = r.Int(); a.DestYMm = r.Int(); a.PhaseStartTick = r.Long(); a.PhaseEndTick = r.Long(); a.FlightTicks = r.Int(); a.Platoon = r.Int(); a.AmmoPm = r.Int(); a.CargoMilliU = r.Int(); }
            var pa = r.Ints(); if (pa == null || pa.Length != PlatoonAt.Length) throw new SaveException(SaveError.TopologyMismatch, "pelotones de Bétera");
            Array.Copy(pa, PlatoonAt, pa.Length);
        }
    }
}
