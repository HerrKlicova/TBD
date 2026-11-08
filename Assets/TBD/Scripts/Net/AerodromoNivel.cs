using System;
using System.Collections.Generic;
using System.Globalization;
using TBD.Units;

namespace TBD.Net
{
    /// <summary>
    /// Zona mínima de Bétera (D-N173; betera-nivel.md v0.2 y estaciones/betera.json de Álvaro; piezas de Teo r2). C# puro: qué pieza va dónde, para que la vista
    /// la monte y las pruebas lo comprueben sin Unity. Encuadre de reserva de 256 × 128 m («zona»); lo que cae fuera va de faldón. Sin asedios en el MVP.
    /// Coordenadas en metros de la zona: X al este, Z al norte. Las piezas de Teo tienen el frente en +Z con yaw 0 y el origen en el suelo.
    /// </summary>
    public struct Colocacion
    {
        public string Pieza, Carpeta, Malla; public int Variante; public float X, Z, Yaw; public bool Faldon;
        public string Ruta, Archivo; public float EscalaX;      // con las instancias de Álvaro: carpeta de Resources/Models y fichero sin «_LOD1» (si no, Betera/Carpeta + Malla_vN)
    }

    public sealed class AerodromoNivel
    {
        public float ZonaX0, ZonaZ0, ZonaX1, ZonaZ1;
        public float[] Pista, Plataforma;                                  // rectángulos [x0, z0, x1, z1]
        public readonly Dictionary<string, float[]> Claros = new Dictionary<string, float[]>();   // nombre → [x, z, lado]
        public readonly List<float[]> Rodaduras = new List<float[]>();     // [x0, z0, x1, z1]
        public readonly List<Colocacion> Piezas = new List<Colocacion>();
        public float CuartelX, CuartelZ;                                   // sitio de la guarnición
        public const float TramoValla = 10f;

        static float F(object o) { return Convert.ToSingle(o, CultureInfo.InvariantCulture); }
        static float[] Fs(object o) { var l = (List<object>)o; var r = new float[l.Count]; for (int i = 0; i < l.Count; i++) r[i] = F(l[i]); return r; }

        /// <summary>«aerodromo/torre_control» → carpeta torre_control, malla SM_TorreControl. «helipuerto» y «rearme_set» van igual.</summary>
        public static string Malla(string carpeta)
        {
            var partes = carpeta.Split('_'); var s = "SM_";
            foreach (var p in partes) if (p.Length > 0) s += char.ToUpperInvariant(p[0]) + p.Substring(1);
            return s;
        }

        public bool DentroZona(float x, float z) { return x >= ZonaX0 && x <= ZonaX1 && z >= ZonaZ0 && z <= ZonaZ1; }

        static readonly string[] PuertaVar = { "TuboChapa", "ChapaOndulada", "Tablones", "MallaEspino", "Blindada" };

        /// <summary>
        /// Carpeta y fichero de cada prefab del CSV de instancias de Álvaro (betera_instancias_v1.csv): las piezas de Bétera de Teo (Betera/&lt;carpeta&gt;), el
        /// reducto con el kit de defensas (Defensas/hesco, con estado por vida) y los tramos y puertas de muro-estados (Defensas/muro). Variante −1 → hash.
        /// </summary>
        public static void Resolver(string prefab, int variante, float x, float z, int estado, out string ruta, out string archivo)
        {
            int k = variante >= 0 ? variante : EstacionDato.VariantOf(x, z, 5);
            if (prefab == "helipuerto") { ruta = "Betera/helipuerto"; archivo = "SM_Helipuerto_v" + (variante >= 0 ? variante : 4); return; }
            if (prefab == "rearme_set") { ruta = "Betera/rearme_set"; archivo = "SM_RearmeSet_v" + k; return; }
            if (prefab == "SM_MuroHesco") { ruta = "Defensas/muro"; archivo = RecintoPlan.Nombre("MuroHesco", MaterialMuro.Hesco, k, estado); return; }
            if (prefab == "SM_MuroSacos") { ruta = "Defensas/muro"; archivo = RecintoPlan.Nombre("MuroSacos", MaterialMuro.Sacos, k, estado); return; }
            if (prefab == "SM_PuertaVehiculos") { ruta = "Defensas/muro"; archivo = "SM_PuertaVehiculos_v" + k + "_" + PuertaVar[k % 5] + "_e" + estado + "_" + new[] { "Sano", "Danado", "MuyDanado", "Brecha" }[Math.Max(0, Math.Min(3, estado))]; return; }
            if (prefab.StartsWith("SM_Hesco")) { ruta = "Defensas/hesco"; archivo = RecintoPlan.Nombre(prefab.Substring(3), MaterialMuro.Hesco, k, estado); return; }
            if (prefab.StartsWith("SM_Sacos")) { ruta = "Defensas/sacos"; archivo = RecintoPlan.Nombre(prefab.Substring(3), MaterialMuro.Sacos, k, estado); return; }
            // piezas de Bétera de Teo: SM_TorreControl → Betera/torre_control
            string nombre = prefab.StartsWith("SM_") ? prefab.Substring(3) : prefab, carpeta = "";
            for (int i = 0; i < nombre.Length; i++) { char c = nombre[i]; if (char.IsUpper(c) && i > 0) carpeta += "_"; carpeta += char.ToLowerInvariant(c); }
            ruta = "Betera/" + carpeta; archivo = prefab + "_v" + k;
        }

        /// <summary>Sustituye las piezas por las instancias del CSV de Álvaro (grupo;prefab;variante;x_m;y_m;z_m;yaw_grados;escala_x;en_zona_mvp;nota).</summary>
        public void UsarInstancias(string csv, int estado = 0)
        {
            Piezas.Clear();
            var lineas = csv.Replace("\r", "").Split('\n');
            for (int i = 1; i < lineas.Length; i++)
            {
                var l = lineas[i].Trim(); if (l.Length == 0) continue;
                var c = l.Split(';'); if (c.Length < 9) continue;
                string prefab = c[1]; int v = int.Parse(c[2], CultureInfo.InvariantCulture);
                float x = float.Parse(c[3], CultureInfo.InvariantCulture), z = float.Parse(c[5], CultureInfo.InvariantCulture), yaw = float.Parse(c[6], CultureInfo.InvariantCulture), ex = float.Parse(c[7], CultureInfo.InvariantCulture);
                Resolver(prefab, v, x, z, estado, out var ruta, out var archivo);
                Piezas.Add(new Colocacion { Pieza = c[0] + "/" + prefab, Carpeta = ruta, Malla = prefab, Variante = v, X = x, Z = z, Yaw = yaw, EscalaX = ex, Faldon = c[8].Trim() != "si", Ruta = ruta, Archivo = archivo });
            }
        }

        public static AerodromoNivel FromJson(string json, bool soloMvp = true)
        {
            var root = Mini.Parse(json) as Dictionary<string, object> ?? throw new FormatException("aeródromo: el JSON no es un objeto");
            var n = new AerodromoNivel();
            var z = Fs(root["zona"]); n.ZonaX0 = z[0]; n.ZonaZ0 = z[1]; n.ZonaX1 = z[2]; n.ZonaZ1 = z[3];
            n.Pista = Fs(((Dictionary<string, object>)root["pista"])["rect"]);
            n.Plataforma = Fs(root["plataforma"]);
            float lado = root.TryGetValue("claros_lado_m", out var cl) ? F(cl) : 25f;
            foreach (var kv in (Dictionary<string, object>)root["claros"]) { var p = Fs(kv.Value); n.Claros[kv.Key] = new[] { p[0], p[1], kv.Key == "P1" ? 30f : lado }; }   // P1 es la losa de 30 m (Lucía)
            if (root.TryGetValue("rodaduras", out var ro)) foreach (var r in (List<object>)ro) { var l = (List<object>)r; var a = Fs(l[0]); var b = Fs(l[1]); n.Rodaduras.Add(new[] { a[0], a[1], b[0], b[1] }); }
            var cu = Fs(root["sitio_cuartel"]); n.CuartelX = cu[0]; n.CuartelZ = cu[1];

            foreach (var o in (List<object>)root["piezas"])
            {
                var p = (Dictionary<string, object>)o;
                string nota = p.TryGetValue("nota", out var nt) ? nt as string ?? "" : "";
                if (soloMvp && nota.Contains("post-MVP")) continue;                 // las torretas del faldón sur, después del MVP
                string pieza = (string)p["pieza"]; var pos = Fs(p["pos"]);
                string carpeta = pieza.StartsWith("aerodromo/") ? pieza.Substring(10) : pieza;
                int v = p.TryGetValue("variante", out var vv) ? (int)F(vv) : -1;
                if (v < 0) v = carpeta == "helipuerto" ? 4 : EstacionDato.VariantOf(pos[0], pos[2], 5);   // Teo: el helipuerto v4 es la toma P1
                n.Piezas.Add(new Colocacion { Pieza = pieza, Carpeta = carpeta, Malla = Malla(carpeta), Variante = v, X = pos[0], Z = pos[2], Yaw = p.TryGetValue("yaw", out var y) ? F(y) : 0f, Faldon = !n.DentroZona(pos[0], pos[2]) });
            }
            // la valla de la base: tramos de 10 m a lo largo de la polilínea, con el tramo a lo largo de su X local (Teo)
            if (root.TryGetValue("valla", out var va))
            {
                var pts = (List<object>)va;
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    var a = Fs(pts[i]); var b = Fs(pts[i + 1]);
                    float dx = b[0] - a[0], dz = b[1] - a[1], len = (float)Math.Sqrt(dx * dx + dz * dz); int k = (int)Math.Round(len / TramoValla);
                    float yaw = (float)(Math.Atan2(-dz, dx) * 180.0 / Math.PI);       // X local del tramo a lo largo del lado
                    for (int t = 0; t < k; t++)
                    {
                        float f = (t + 0.5f) / k, x = a[0] + dx * f, zz = a[1] + dz * f;
                        n.Piezas.Add(new Colocacion { Pieza = "aerodromo/valla_base", Carpeta = "valla_base", Malla = "SM_VallaBase", Variante = EstacionDato.VariantOf(x, zz, 5), X = x, Z = zz, Yaw = yaw, Faldon = !n.DentroZona(x, zz) });
                    }
                }
            }
            return n;
        }
    }

    /// <summary>
    /// Despegue y toma del NH90 en Bétera (D-N173): preparar y despegar en 20 s (12 s de rotor en el suelo y 8 s de subida a 30 m de altura de crucero, la de
    /// HeliStats), y salida en línea recta con el rumbo del destino; la toma, al revés: llega con el rumbo de llegada, baja en 8 s y para el rotor en 12 s.
    /// C# puro: posición en metros de la zona y altura, según el tiempo real desde que empieza la maniobra.
    /// </summary>
    public static class VueloLocal
    {
        public const float RotorS = 12f, SubidaS = 8f, AlturaM = 30f, VelocidadMs = 72f;      // 260 km/h ≈ 72 m/s (SIN VERIFICAR, como AerodromoEstado)

        /// <summary>Despegue desde (x0, z0) hacia el rumbo (grados, 0 = norte, 90 = este): t desde que se da la orden.</summary>
        public static void Despegue(float x0, float z0, float rumbo, float t, out float x, out float z, out float y)
        {
            double r = rumbo * Math.PI / 180.0; float ex = (float)Math.Sin(r), ez = (float)Math.Cos(r);
            if (t <= RotorS) { x = x0; z = z0; y = 0f; return; }
            if (t <= RotorS + SubidaS) { x = x0; z = z0; y = AlturaM * (t - RotorS) / SubidaS; return; }
            float d = (t - RotorS - SubidaS) * VelocidadMs; x = x0 + ex * d; z = z0 + ez * d; y = AlturaM;
        }

        /// <summary>Toma en (x1, z1) llegando con «rumboLlegada»; t = segundos que faltan para estar parado (de 20 a 0 la maniobra final; más de 20, en vuelo).</summary>
        public static void Toma(float x1, float z1, float rumboLlegada, float faltan, out float x, out float z, out float y)
        {
            double r = rumboLlegada * Math.PI / 180.0; float ex = (float)Math.Sin(r), ez = (float)Math.Cos(r);
            if (faltan <= RotorS) { x = x1; z = z1; y = 0f; return; }
            if (faltan <= RotorS + SubidaS) { x = x1; z = z1; y = AlturaM * (faltan - RotorS) / SubidaS; return; }
            float d = (faltan - RotorS - SubidaS) * VelocidadMs; x = x1 - ex * d; z = z1 - ez * d; y = AlturaM;      // viene de atrás del rumbo de llegada
        }
    }
}
