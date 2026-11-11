using System;
using System.Collections.Generic;
using System.Text;
using TBD.Units;

namespace TBD.Mundo
{
    /// <summary>Tipos de orden. 1–99: unidades (Nacho); 100 o más: la red (Iker, Red.Aplicar).</summary>
    public enum TipoOrden : ushort { Mover = 1, AtacarMoviendo = 2, Parar = 3, Formacion = 4, FuegoLibre = 5, RedPrimera = 100 }

    /// <summary>Una acción de un jugador, aplicada al empezar el tick «Tick». A, B, C y D según el tipo (Mover: pelotón, x, y, rumbo final o −1).</summary>
    public struct Orden
    {
        public int Tick, Jugador; public TipoOrden Tipo; public int A, B, C, D;
        public override string ToString() { return $"{Tick};{Jugador};{(ushort)Tipo};{A};{B};{C};{D}"; }
        public static Orden Parse(string s) { var p = s.Split(';'); return new Orden { Tick = int.Parse(p[0]), Jugador = int.Parse(p[1]), Tipo = (TipoOrden)ushort.Parse(p[2]), A = int.Parse(p[3]), B = int.Parse(p[4]), C = int.Parse(p[5]), D = int.Parse(p[6]) }; }
    }

    /// <summary>Un caso de la casa del caserío: rectángulo en mm y altura (para la línea de visión).</summary>
    public struct Casa { public RectMm R; public int AlturaMm; }

    /// <summary>
    /// LA simulación del núcleo v2 (Hito 1; contrato de Nacho aceptado por Iker, 11-11). Un solo mundo para todos: el que se mira y el que no (D-N207: el nivel de
    /// detalle lo decide la actividad, nunca la cámara; la cámara no existe aquí). C# puro, sin Unity: entra en las pruebas de dotnet.
    /// Cooperativo (D-N206): tick fijo de 30/s, todo en enteros (mm), cada acción de un jugador es una Orden en cola, y Hash por tick (y por sistema) para la prueba de
    /// desincronización. Grabación = semilla + órdenes; reproducirla da los mismos hashes.
    /// La red de Iker entra por los métodos parciales AplicarRed, StepRed y HashRedPorSistema (archivo Mundo/Red/, partial class Mundo), sin tocar este archivo.
    /// Unity (la escena única de Marcos) solo lee Unidades y Zombis, interpola para dibujar y mete órdenes con Encolar.
    /// </summary>
    public sealed partial class Mundo
    {
        public readonly int AnchoMm, AltoMm; public readonly uint Semilla;
        public int Tick { get; private set; }
        public readonly UnitsWorld Unidades;
        public readonly ZombisMundo Zombis;
        public readonly RejillaTrozos Rejilla;
        public readonly List<Casa> Casas = new List<Casa>();
        public int AlcanceFuegoMm = 60000;                  // como la zona y NightSim (D-N186): blancos a 60 m de un pelotón
        public int ContactoMm = 900, ContactoDanioTick = 2;  // NightParams de la noche (noche_contacto_mm, noche_contacto_danio_tick)
        public int Abatidos, BajasPropias;
        readonly List<Orden> _cola = new List<Orden>();      // ordenadas por (Tick, Jugador, llegada)
        readonly List<Orden> _aplicadas = new List<Orden>(); // la grabación
        readonly List<Target> _blancos = new List<Target>();
        readonly StepResult _res = new StepResult(); readonly ContactResult _contacto = new ContactResult();

        public Mundo(uint semilla, int anchoMm, int altoMm)
        {
            Semilla = semilla; AnchoMm = anchoMm; AltoMm = altoMm;
            Unidades = new UnitsWorld(semilla ^ 0x5A17u);
            Zombis = new ZombisMundo(semilla);
            Rejilla = new RejillaTrozos(anchoMm, altoMm, UnitsWorld.ManRadiusMm + 50);
        }

        /// <summary>Casas y demás obstáculos fijos: bloquean el paso (zombis y soldados) y la vista.</summary>
        public void PonerCasas(IList<Casa> casas)
        {
            Casas.Clear(); Casas.AddRange(casas);
            var rects = new List<RectMm>(); foreach (var c in casas) rects.Add(c.R);
            Rejilla.PonerObstaculos(rects);
            Unidades.Obstacles.Clear(); Unidades.Obstacles.AddRange(rects);
            Unidades.LosBlockers.Clear(); foreach (var c in casas) Unidades.LosBlockers.Add(new UnitsWorld.LosBlocker { R = c.R, HeightMm = c.AlturaMm });
        }

        /// <summary>Mete una orden. Se aplica al empezar su tick; si llega tarde (tick pasado), se aplica en el siguiente (en el cooperativo no pasa: lockstep).</summary>
        public void Encolar(Orden o)
        {
            if (o.Tick < Tick) o.Tick = Tick;
            int i = _cola.Count; while (i > 0 && (_cola[i - 1].Tick > o.Tick || (_cola[i - 1].Tick == o.Tick && _cola[i - 1].Jugador > o.Jugador))) i--;
            _cola.Insert(i, o);
        }
        public IReadOnlyList<Orden> Grabacion { get { return _aplicadas; } }

        void Aplicar(Orden o)
        {
            switch (o.Tipo)
            {
                case TipoOrden.Mover: Unidades.Move(o.A, o.B, o.C, o.D); break;
                case TipoOrden.AtacarMoviendo: Unidades.AttackMove(o.A, o.B, o.C, o.D); break;
                case TipoOrden.Parar: Unidades.Stop(o.A); break;
                case TipoOrden.Formacion: Unidades.SetFormation(o.A, (Formation)o.B); break;
                case TipoOrden.FuegoLibre: Unidades.SetHoldFire(o.A, o.B == 0); break;
                default: if ((ushort)o.Tipo >= (ushort)TipoOrden.RedPrimera) AplicarRed(o); break;
            }
        }

        partial void AplicarRed(Orden o);
        partial void StepRed();

        bool CercaDePeloton(int x, int y)
        {
            long r2 = (long)AlcanceFuegoMm * AlcanceFuegoMm;
            for (int i = 0; i < Unidades.Platoons.Count; i++) { var p = Unidades.Platoons[i]; if (p.Aboard != 0) continue; long dx = x - p.AX, dy = y - p.AY; if (dx * dx + dy * dy <= r2) return true; }
            return false;
        }

        /// <summary>Un tick de 1/30 s: órdenes, red, fuego, zombis y contacto, en este orden fijo.</summary>
        public void Step()
        {
            while (_cola.Count > 0 && _cola[0].Tick <= Tick) { var o = _cola[0]; _cola.RemoveAt(0); Aplicar(o); _aplicadas.Add(o); }
            StepRed();
            // fuego: blancos a 60 m de algún pelotón; un impacto abate (la regla de NightSim)
            _blancos.Clear();
            var zs = Zombis.Lista;
            for (int i = 0; i < zs.Count; i++) if (zs[i].Vivo && CercaDePeloton(zs[i].X, zs[i].Y)) _blancos.Add(new Target { Id = i, X = zs[i].X, Y = zs[i].Y });
            Unidades.Step(_blancos, _res);
            foreach (var h in _res.Hits) if (Zombis.Abatir(h.TargetId)) { Abatidos++; Unidades.NoteKill(h.Platoon); }
            // zombis: vista, oído, deambular y andar
            Zombis.Step(Unidades, Rejilla, _res.Shots, Tick);
            // contacto con los soldados
            _blancos.Clear();
            for (int i = 0; i < zs.Count; i++) if (zs[i].Vivo && CercaDePeloton(zs[i].X, zs[i].Y)) _blancos.Add(new Target { Id = i, X = zs[i].X, Y = zs[i].Y });
            BajasPropias += Unidades.ZombieContact(_blancos, ContactoMm, ContactoDanioTick, _contacto);
            Tick++;
        }

        public StepResult UltimoFuego { get { return _res; } }

        // ---- hash por sistema (Sara, plan-hito1 §3: «desync=True@tick · difiere: <sistemas>»)
        /// <summary>Hash de cada sistema, con nombre y en orden fijo: «reloj», «unidades», «zombis» y lo que añada la red (Iker: «red.via», «red.trenes», «red.almacen», «red.cargas»).</summary>
        public List<KeyValuePair<string, ulong>> HashPorSistema()
        {
            var l = new List<KeyValuePair<string, ulong>>
            {
                new KeyValuePair<string, ulong>("reloj", IMath.Mix(0x4D0D0UL ^ (ulong)(uint)Tick ^ ((ulong)(uint)Abatidos << 32) ^ ((ulong)(uint)BajasPropias << 52))),
                new KeyValuePair<string, ulong>("unidades", Unidades.Hash()),
                new KeyValuePair<string, ulong>("zombis", Zombis.Hash()),
            };
            HashRedPorSistema(l);
            return l;
        }
        partial void HashRedPorSistema(List<KeyValuePair<string, ulong>> l);
        public ulong Hash() { ulong h = 0; var s = HashPorSistema(); for (int i = 0; i < s.Count; i++) h += IMath.Mix(s[i].Value ^ (ulong)(i + 1)); return h; }

        /// <summary>Línea del registro de hashes (-hashlog): «tick;total;reloj=…;unidades=…;…» en hexadecimal.</summary>
        public string LineaHash()
        {
            var s = HashPorSistema(); var sb = new StringBuilder(); sb.Append(Tick).Append(';').Append(Hash().ToString("x16"));
            for (int i = 0; i < s.Count; i++) sb.Append(';').Append(s[i].Key).Append('=').Append(s[i].Value.ToString("x16"));
            return sb.ToString();
        }

        /// <summary>Compara dos líneas de LineaHash del mismo tick: null si coinciden; si no, «desync=True@tick · difiere: Zombis, Unidades».</summary>
        public static string CompararLineas(string a, string b)
        {
            if (a == b) return null;
            var pa = a.Split(';'); var pb = b.Split(';'); var dif = new List<string>();
            for (int i = 2; i < Math.Min(pa.Length, pb.Length); i++) if (pa[i] != pb[i]) dif.Add(pa[i].Split('=')[0]);
            if (pa[0] != pb[0]) dif.Insert(0, "tick distinto");
            return $"desync=True@{pa[0]} · difiere: {(dif.Count > 0 ? string.Join(", ", dif) : "total")}";
        }

        /// <summary>Grabación para -grabar/-reproducir: primera línea «semilla;escenario», luego una orden por línea.</summary>
        public string GrabacionTexto(string escenario)
        {
            var sb = new StringBuilder(); sb.Append(Semilla).Append(';').Append(escenario).Append('\n');
            foreach (var o in _aplicadas) sb.Append(o.ToString()).Append('\n');
            return sb.ToString();
        }
        public static List<Orden> OrdenesDe(string grabacion, out uint semilla, out string escenario)
        {
            var lineas = grabacion.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries); var cab = lineas[0].Split(';');
            semilla = uint.Parse(cab[0]); escenario = cab.Length > 1 ? cab[1] : "";
            var l = new List<Orden>(); for (int i = 1; i < lineas.Length; i++) l.Add(Orden.Parse(lineas[i].Trim())); return l;
        }

        // ---- escenario del Hito 1: el caserío
        public const string EscenarioCaserio = "caserio";
        public const int CaserioX = 1400000, CaserioY = 1000000;      // centro del caserío (provisional hasta el CSV de posiciones de Álvaro)

        /// <summary>Terreno de 2 × 2 km con un caserío de 8 casas y «zombis» zombis repartidos por ellas, y un pelotón de fusileros a 150 m al oeste mirando al este.
        /// Posiciones provisionales hasta que Álvaro suba el CSV del terreno.</summary>
        public static Mundo Caserio(uint semilla, int zombis = 100)
        {
            var m = new Mundo(semilla, 2000000, 2000000);
            var casas = new List<Casa>();
            int[] cx = { -30000, -8000, 14000, 34000, -26000, -2000, 20000, 40000 }, cy = { -18000, -22000, -17000, -20000, 16000, 20000, 15000, 19000 };
            for (int k = 0; k < cx.Length; k++)
            {
                int w = 10000 + (k % 3) * 2000, h = 8000 + (k % 2) * 2000, x = CaserioX + cx[k], y = CaserioY + cy[k];
                casas.Add(new Casa { R = new RectMm(x - w / 2, y - h / 2, x + w / 2, y + h / 2), AlturaMm = 6000 + (k % 2) * 3000 });
            }
            m.PonerCasas(casas);
            var rng = new IRng(semilla ^ 0xCA5E1u);
            for (int i = 0; i < zombis; i++)
            {
                var c = casas[i % casas.Count].R; int hx = (c.X0 + c.X1) / 2, hy = (c.Y0 + c.Y1) / 2;
                int puertaY = hy < CaserioY ? c.Y1 + 2500 : c.Y0 - 2500;                       // la «puerta» da a la calle del medio
                int x = hx, y = puertaY;
                for (int intento = 0; intento < 20; intento++) { int px = hx + rng.Below(14001) - 7000, py = puertaY + rng.Below(6001) - 3000; if (m.Rejilla.Libre(px, py)) { x = px; y = py; break; } }
                m.Zombis.Anadir(x, y, hx, puertaY);
            }
            m.Unidades.AddPlatoon(PlatoonKind.Fusileros, CaserioX - 150000, CaserioY, 0);   // rumbo 0 = +X (este)
            return m;
        }
    }
}
