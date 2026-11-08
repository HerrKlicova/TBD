using System;
using System.Collections.Generic;

namespace TBD.Net
{
    /// <summary>
    /// Recinto de una estación con el kit de defensas de Teo (8-nov, aprobado por Lucía): esquinas, remates junto a puertas y edificio, tramos de 10 m de
    /// HESCO o de 8 m de sacos y cortos de HESCO de 1 a 4 celdas (1,11 m) para lo que sobra. C# puro: qué pieza va dónde (metros de la zona, X y Z).
    /// Reglas de Teo: el frente de cada pieza mira afuera (+Z con yaw 0; oeste 270, sur 180, este 90, norte 0); las esquinas dejan 2,78 m (HESCO) o 3,9 m
    /// (sacos) antes del primer tramo; el remate cierra el muro junto a una puerta o al edificio. Estados por vida (construccion-reparar.md §1): Sano ≥ 70 %,
    /// Dañado 30–70, Muy dañado 1–30 y Brecha 0 (no bloquea). Variante por hash de la posición. Coste de Hugo: HESCO 0,6 u/m, sacos 0,2 u/m (SIN COSTEAR
    /// en Nules: lo cierra Hugo; la cifra de Lucía es 130 u).
    /// </summary>
    public enum MaterialMuro : byte { Hesco, Sacos }

    public struct PiezaMuro { public string Malla; public float X, Z, Yaw, Largo, EscalaX; public MaterialMuro Material; public bool Espejo; }

    public static class RecintoPlan
    {
        public const float HescoTramo = 10f, SacosTramo = 8f, HescoCelda = 1.11f, HescoEsquinaM = 2.78f, SacosEsquinaM = 3.9f;
        public const float HescoUporM = 0.6f, SacosUporM = 0.2f, AlaHescoM = 15f;

        /// <summary>Coste de las puertas (Hugo): de vía doble 12 u, de vía 8 u, de vehículos 10 u.</summary>
        public static float CostePuertas(string json)
        {
            var root = TBD.Units.Mini.Parse(json) as Dictionary<string, object>; float u = 0;
            foreach (var o in (List<object>)root["puertas"]) { var t = (string)((Dictionary<string, object>)o)["tipo"]; u += t == "via_doble" ? 12f : t == "via" ? 8f : t == "vehiculos" ? 10f : 0f; }
            return u;
        }
        static readonly string[] HescoVar = { "MallaLimpia", "Geotextil", "Escalonado", "RemateSacos", "Red" };
        static readonly string[] SacosVar = { "Recto", "Talud", "Irregular", "Tablon", "Red" };
        static readonly string[] Estados = { "e0_Sano", "e1_Danado", "e2_MuyDanado", "e3_Brecha" };

        /// <summary>Estado por vida (0..1): 0 Sano ≥ 0,7; 1 Dañado ≥ 0,3; 2 Muy dañado > 0; 3 Brecha.</summary>
        public static int Estado(float vida) { return vida >= 0.7f ? 0 : vida >= 0.3f ? 1 : vida > 0f ? 2 : 3; }

        public static string Nombre(string pieza, MaterialMuro m, int variante, int estado)
        {
            string v = m == MaterialMuro.Hesco ? HescoVar[variante % 5] : SacosVar[variante % 5];
            return "SM_" + pieza + "_v" + (variante % 5) + "_" + v + "_" + Estados[Math.Max(0, Math.Min(3, estado))];
        }

        /// <summary>Yaw de la pieza con el frente hacia la normal exterior (nx, nz): +Z = 0, +X = 90, −Z = 180, −X = 270.</summary>
        public static float YawAfuera(float nx, float nz) { return (float)((Math.Atan2(nx, nz) * 180.0 / Math.PI + 360.0) % 360.0); }

        public struct Hueco { public float A, B; }      // tramo del lado (en metros desde su inicio) que queda libre (puerta)

        /// <summary>
        /// Planifica el recinto de un polígono cerrado (en orden, cualquier sentido). «material(i)» dice el material del lado i (de vértice i a i+1); «edificio(i)»,
        /// si ese lado es un edificio (no lleva muro). «huecos»: puertas por lado. Vida de todas las piezas: «vida» (0..1).
        /// </summary>
        public static List<PiezaMuro> Plan(float[][] poly, Func<int, MaterialMuro> material, Func<int, bool> edificio, Dictionary<int, List<Hueco>> huecos, float vida = 1f)
        {
            var res = new List<PiezaMuro>(); int n = poly.Length; int estado = Estado(vida);
            // sentido del polígono, para saber dónde queda fuera
            double area = 0; for (int i = 0; i < n; i++) { var a = poly[i]; var b = poly[(i + 1) % n]; area += a[0] * b[1] - b[0] * a[1]; }
            bool ccw = area > 0;
            for (int i = 0; i < n; i++)
            {
                if (edificio(i)) continue;
                var a = poly[i]; var b = poly[(i + 1) % n];
                float dx = b[0] - a[0], dz = b[1] - a[1], len = (float)Math.Sqrt(dx * dx + dz * dz); if (len < 0.5f) continue;
                float ux = dx / len, uz = dz / len;
                float nx = ccw ? uz : -uz, nz = ccw ? -ux : ux;                  // normal hacia fuera
                float yaw = YawAfuera(nx, nz); var m = material(i);
                float res0 = m == MaterialMuro.Hesco ? HescoEsquinaM : SacosEsquinaM;
                // extremos: esquina si el vecino es muro (convexa), remate si es edificio; los dos reservan su brazo
                bool prevEdif = edificio((i - 1 + n) % n), nextEdif = edificio((i + 1) % n);
                // vértices alineados (cambio de material a mitad de un lado, p. ej. las alas de HESCO de Hugo): ni esquina ni hueco de esquina
                bool iniRecto = Alineado(poly[(i - 1 + n) % n], a, b), finRecto = Alineado(a, b, poly[(i + 2) % n]);
                // trozos libres del lado, quitando las puertas
                var cortes = new List<Hueco>(); if (huecos != null && huecos.TryGetValue(i, out var hs)) cortes.AddRange(hs);
                cortes.Sort((p, q) => p.A.CompareTo(q.A));
                float desde = 0f; var trozos = new List<float[]>();
                foreach (var c in cortes) { if (c.A > desde) trozos.Add(new[] { desde, c.A, 1f }); desde = Math.Max(desde, c.B); }
                if (desde < len) trozos.Add(new[] { desde, len, 0f });
                // esquina en el vértice de inicio (si el lado anterior es muro): una por vértice
                if (!prevEdif && !iniRecto)
                {
                    // la esquina de Teo tiene el recinto en (+X, −Z) con yaw 0; se gira según hacia dónde quedan los dos muros desde el vértice
                    var pv = poly[(i - 1 + n) % n]; float wx = pv[0] - a[0], wz = pv[1] - a[1];
                    int sx = Math.Sign(ux + Math.Sign(wx)), sz = Math.Sign(uz + Math.Sign(wz));
                    float ye = sx > 0 && sz < 0 ? 0f : sx < 0 && sz < 0 ? 90f : sx < 0 && sz > 0 ? 180f : 270f;
                    res.Add(new PiezaMuro { Malla = Nombre(m == MaterialMuro.Hesco ? "HescoEsquina" : "SacosEsquina", m, EstacionDato.VariantOf(a[0], a[1], 5), estado), X = a[0], Z = a[1], Yaw = ye, Material = m, Largo = 0 });
                }
                for (int t = 0; t < trozos.Count; t++)
                {
                    float s0 = trozos[t][0], s1 = trozos[t][1];
                    bool iniEsquina = s0 == 0f, finEsquina = s1 >= len - 1e-3f;
                    // remates junto a puertas y junto al edificio
                    if ((!iniEsquina || prevEdif) && !(iniEsquina && iniRecto)) res.Add(Remate(m, a, ux, uz, s0, yaw, estado, +1));
                    if ((!finEsquina || nextEdif) && !(finEsquina && finRecto)) res.Add(Remate(m, a, ux, uz, s1, yaw, estado, -1));
                    float l0 = s0 + (iniEsquina && iniRecto ? 0f : res0), l1 = s1 - (finEsquina && finRecto ? 0f : res0); if (l1 - l0 < 0.3f) continue;
                    Rellenar(res, m, a, ux, uz, l0, l1, yaw, estado);
                }
            }
            return res;
        }

        static bool Alineado(float[] p, float[] q, float[] r)
        {
            double c = (q[0] - p[0]) * (r[1] - q[1]) - (q[1] - p[1]) * (r[0] - q[0]);
            double d = (q[0] - p[0]) * (r[0] - q[0]) + (q[1] - p[1]) * (r[1] - q[1]);
            return Math.Abs(c) < 1e-3 && d > 0;
        }

        static PiezaMuro Remate(MaterialMuro m, float[] a, float ux, float uz, float s, float yaw, int estado, int hacia)
        {
            float x = a[0] + ux * s, z = a[1] + uz * s;
            // el brazo del remate va a lo largo del muro alejándose del hueco: si con este yaw apunta al revés, se espeja en X
            double r = yaw * Math.PI / 180.0; float bx = (float)Math.Cos(r), bz = (float)-Math.Sin(r);
            bool espejo = (bx * ux + bz * uz) * hacia < 0;
            return new PiezaMuro { Malla = Nombre(m == MaterialMuro.Hesco ? "HescoRemate" : "SacosRemate", m, EstacionDato.VariantOf(x, z, 5), estado), X = x, Z = z, Yaw = yaw, Material = m, Espejo = espejo };
        }

        static void Rellenar(List<PiezaMuro> res, MaterialMuro m, float[] a, float ux, float uz, float l0, float l1, float yaw, int estado)
        {
            float paso = m == MaterialMuro.Hesco ? HescoTramo : SacosTramo, s = l0;
            while (l1 - s >= paso - 1e-3f) { Pon(res, m, a, ux, uz, s, paso, yaw, estado, m == MaterialMuro.Hesco ? "MuroHesco" : "MuroSacos", 1f); s += paso; }
            float resto = l1 - s; if (resto < 0.3f) return;
            if (m == MaterialMuro.Hesco)
                while (resto >= HescoCelda * 0.5f) { int k = Math.Max(1, Math.Min(4, (int)Math.Round(resto / HescoCelda))); float l = k * HescoCelda; Pon(res, m, a, ux, uz, s, Math.Min(l, resto + 0.3f), yaw, estado, "HescoCorto" + k, 1f); s += l; resto = l1 - s; }
            else Pon(res, m, a, ux, uz, s, resto, yaw, estado, "MuroSacos", resto / SacosTramo);      // el tramo de 8 m, escalado en X
        }

        static void Pon(List<PiezaMuro> res, MaterialMuro m, float[] a, float ux, float uz, float s, float l, float yaw, int estado, string pieza, float escala)
        {
            float c = s + l * 0.5f, x = a[0] + ux * c, z = a[1] + uz * c;
            res.Add(new PiezaMuro { Malla = Nombre(pieza, m, EstacionDato.VariantOf(x, z, 5), estado), X = x, Z = z, Yaw = yaw, Largo = l, EscalaX = escala, Material = m });
        }

        /// <summary>
        /// Coste lineal del recinto como lo cuenta Hugo: metros de muro por material (lados menos huecos de puerta y menos el edificio), a 0,6 u/m de HESCO y
        /// 0,2 u/m de sacos. Las esquinas y los remates ya van dentro de esos metros.
        /// </summary>
        public static float CosteLineal(float[][] poly, Func<int, MaterialMuro> material, Func<int, bool> edificio, Dictionary<int, List<Hueco>> huecos)
        {
            float u = 0; int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                if (edificio(i)) continue;
                var a = poly[i]; var b = poly[(i + 1) % n]; float len = (float)Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
                if (huecos != null && huecos.TryGetValue(i, out var hs)) foreach (var h in hs) len -= Math.Max(0f, Math.Min(h.B, len) - Math.Max(h.A, 0f));
                u += len * (material(i) == MaterialMuro.Hesco ? HescoUporM : SacosUporM);
            }
            return u;
        }

        /// <summary>Coste de las piezas de tramo (sin esquinas ni remates): solo para comprobar la colocación.</summary>
        public static float Coste(List<PiezaMuro> plan)
        {
            float u = 0; foreach (var p in plan) u += p.Largo * (p.Material == MaterialMuro.Hesco ? HescoUporM : SacosUporM);
            return u;
        }

        /// <summary>Nules (nules-nivel.md §3; Lucía: HESCO al este y sacos en el resto; el edificio de viajeros hace de muro norte entre X 104,5 y 125,5).</summary>
        public static List<PiezaMuro> Nules(string json, float vida = 1f)
        {
            var root = TBD.Units.Mini.Parse(json) as Dictionary<string, object>;
            var rl = (List<object>)root["recinto"]; var poly = new float[rl.Count][];
            for (int i = 0; i < rl.Count; i++) { var p = (List<object>)rl[i]; poly[i] = new[] { Convert.ToSingle(p[0], System.Globalization.CultureInfo.InvariantCulture), Convert.ToSingle(p[1], System.Globalization.CultureInfo.InvariantCulture) }; }
            float maxX = float.MinValue; foreach (var p in poly) maxX = Math.Max(maxX, p[0]);
            // Hugo (nules-recinto.md §2): HESCO en el frente este y un ala de 15 m en cada esquina norte y sur del este; se parte el lado con un vértice alineado
            var lista = new List<float[]>();
            for (int i = 0; i < poly.Length; i++)
            {
                var a0 = poly[i]; var b0 = poly[(i + 1) % poly.Length]; lista.Add(a0);
                if (Math.Abs(a0[1] - b0[1]) < 1e-3f && (Math.Abs(a0[0] - maxX) < 1e-3f || Math.Abs(b0[0] - maxX) < 1e-3f))
                { float x = maxX - AlaHescoM; if (x > Math.Min(a0[0], b0[0]) && x < Math.Max(a0[0], b0[0])) lista.Add(new[] { x, a0[1] }); }
            }
            poly = lista.ToArray();
            Func<int, bool> edif = i => { var a = poly[i]; var b = poly[(i + 1) % poly.Length]; return (a[1] > 96f + 1e-3f || b[1] > 96f + 1e-3f); };   // los tres lados que rodean el edificio
            Func<int, MaterialMuro> mat = i => { var a = poly[i]; var b = poly[(i + 1) % poly.Length]; return Math.Min(a[0], b[0]) >= maxX - AlaHescoM - 1e-3f ? MaterialMuro.Hesco : MaterialMuro.Sacos; };
            // puertas: cada una corta el lado donde cae
            var huecos = new Dictionary<int, List<Hueco>>();
            foreach (var o in (List<object>)root["puertas"])
            {
                var d = (Dictionary<string, object>)o; var pos = (List<object>)d["pos"]; float px = Convert.ToSingle(pos[0], System.Globalization.CultureInfo.InvariantCulture), pz = Convert.ToSingle(pos[1], System.Globalization.CultureInfo.InvariantCulture);
                float[] hz = d.TryGetValue("hueco_z", out var h1) ? Fs(h1) : null, hx = d.TryGetValue("hueco_x", out var h2) ? Fs(h2) : null;
                for (int i = 0; i < poly.Length; i++)
                {
                    var a = poly[i]; var b = poly[(i + 1) % poly.Length];
                    float dx = b[0] - a[0], dz = b[1] - a[1], len = (float)Math.Sqrt(dx * dx + dz * dz); if (len < 0.5f) continue;
                    float s = ((px - a[0]) * dx + (pz - a[1]) * dz) / len; float ex = a[0] + dx * s / len - px, ez = a[1] + dz * s / len - pz;
                    if (s < 0 || s > len || ex * ex + ez * ez > 1f) continue;
                    float h0, h1b;
                    if (hz != null && Math.Abs(dx) < 1e-3f) { h0 = (hz[0] - a[1]) * Math.Sign(dz); h1b = (hz[1] - a[1]) * Math.Sign(dz); }
                    else if (hx != null && Math.Abs(dz) < 1e-3f) { h0 = (hx[0] - a[0]) * Math.Sign(dx); h1b = (hx[1] - a[0]) * Math.Sign(dx); }
                    else { h0 = s - 4f; h1b = s + 4f; }
                    if (!huecos.TryGetValue(i, out var l)) huecos[i] = l = new List<Hueco>();
                    l.Add(new Hueco { A = Math.Min(h0, h1b), B = Math.Max(h0, h1b) });
                }
            }
            UltimoCosteLineal = CosteLineal(poly, mat, edif, huecos);
            return Plan(poly, mat, edif, huecos, vida);
        }
        /// <summary>Coste lineal (Hugo) del último recinto planificado con Nules().</summary>
        public static float UltimoCosteLineal { get; private set; }

        static float[] Fs(object o) { var l = (List<object>)o; return new[] { Convert.ToSingle(l[0], System.Globalization.CultureInfo.InvariantCulture), Convert.ToSingle(l[1], System.Globalization.CultureInfo.InvariantCulture) }; }

        /// <summary>
        /// Valla de Bétera por vida (Teo, valla_betera: 5.000 HP por tramo de 10 m): e0 Sana ≥ 85 %, e1 SanaGolpeada 70–85 (solo a la vista), e2 Dañada 30–70,
        /// e3 MuyDañada 1–30 y e4 Brecha a 0. «muroBajo»: la variante sobre muro bajo enfoscado.
        /// </summary>
        public static string VallaBetera(int hp, bool muroBajo, int hpMax = 5000)
        {
            float f = hpMax <= 0 ? 0 : (float)hp / hpMax;
            string e = hp <= 0 ? "e4_Brecha" : f < 0.3f ? "e3_MuyDanada" : f < 0.7f ? "e2_Danada" : f < 0.85f ? "e1_SanaGolpeada" : "e0_Sana";
            return "SM_VallaBetera_" + (muroBajo ? "MuroBajo" : "Malla") + "_" + e;
        }
    }
}
