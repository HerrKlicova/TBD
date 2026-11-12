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

    /// <summary>Un obstáculo fijo del nivel (edificio, tapia, arbolado): rectángulo en mm, altura, y si tapa el paso y la vista (obstaculos_hito1.csv de Álvaro).</summary>
    public struct Casa { public RectMm R; public int AlturaMm; public bool TapaPaso, TapaVision; public string Id; }

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
        /// <summary>Combate del Hito 1 (Hugo y Lucía, 11-11; medido con 40 semillas en el caserío): puntería ×6 sobre la tabla de la noche (k ≈ 5,4 disparos por zombi)
        /// y 35 cartuchos por soldado (100 × k ÷ 16, redondeado a 5). Va en Mundo, no en balance_unidades.csv, para no mover la noche vieja.</summary>
        public const int PunteriaHito1Pm = 6000, CartuchosPorSoldado = 35;
        /// <summary>Cartuchos por caja de munición del almacén (Hugo, 23:23: 3 cargadores de 35 = 105) y cajas con las que empieza B (Hugo: 1).</summary>
        public const int CartuchosPorCaja = 105, CajasInicialesB = 1;
        public int AlcanceFuegoMm = 60000;                  // como la zona y NightSim (D-N186): blancos a 60 m de un pelotón
        public int ContactoMm = 900, ContactoDanioTick = 2;  // NightParams de la noche (noche_contacto_mm, noche_contacto_danio_tick)
        public int Abatidos, BajasPropias;
        /// <summary>Retirada sola sin munición (Hugo y Lucía, 11-11: «sin la línea el pelotón se queda seco a mitad del caserío pero sin morir entero»): un pelotón que no puede
        /// disparar ni una vez vuelve a pie a «PuntoRetirada» (B) en lugar de quedarse a que lo maten. Al reabastecerse allí, el jugador lo vuelve a mandar.</summary>
        public bool RetiradaSinMunicion = true; public string PuntoRetirada = "B";
        /// <summary>Un pelotón a menos de esto de B se recarga solo del almacén (Unidades.StockShots; con la red de Iker será Red.Almacen de B), una vez por segundo.</summary>
        public int RecargaRadioMm = 40000;      // la explanada de B (70 × 120 m): la salida del pelotón está a 32 m del centro
        readonly HashSet<int> _retirandose = new HashSet<int>();
        /// <summary>Medidas para Hugo (fuera del hash): distancia de cada abatido a su pelotón (mm) y tick de cada baja propia.</summary>
        public readonly List<int> DistAbatidoMm = new List<int>(), TickBaja = new List<int>();
        readonly List<Orden> _cola = new List<Orden>();      // ordenadas por (Tick, Jugador, llegada)
        readonly List<Orden> _aplicadas = new List<Orden>(); // la grabación
        readonly List<Target> _blancos = new List<Target>();
        readonly StepResult _res = new StepResult(); readonly ContactResult _contacto = new ContactResult();

        public Mundo(uint semilla, int anchoMm, int altoMm)
        {
            Semilla = semilla; AnchoMm = anchoMm; AltoMm = altoMm;
            Unidades = new UnitsWorld(semilla ^ 0x5A17u) { HitScalePm = PunteriaHito1Pm };
            Zombis = new ZombisMundo(semilla);
            Rejilla = new RejillaTrozos(anchoMm, altoMm, UnitsWorld.ManRadiusMm + 50);
        }

        /// <summary>Casas y demás obstáculos fijos: bloquean el paso (zombis y soldados) y la vista.</summary>
        public void PonerCasas(IList<Casa> casas)
        {
            Casas.Clear(); Casas.AddRange(casas);
            var rects = new List<RectMm>(); foreach (var c in casas) if (c.TapaPaso) rects.Add(c.R);
            Rejilla.PonerObstaculos(rects);
            Unidades.Obstacles.Clear(); Unidades.Obstacles.AddRange(rects);
            Unidades.LosBlockers.Clear(); foreach (var c in casas) if (c.TapaVision) Unidades.LosBlockers.Add(new UnitsWorld.LosBlocker { R = c.R, HeightMm = c.AlturaMm });
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
            long t0 = Prof ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            Unidades.Step(_blancos, _res);
            if (Prof) { long t1 = System.Diagnostics.Stopwatch.GetTimestamp(); ProfUnidades += t1 - t0; t0 = t1; }
            foreach (var h in _res.Hits)
            {
                if (h.TargetId < 0 || h.TargetId >= zs.Count || !zs[h.TargetId].Vivo) continue;
                var pl = Unidades.Find(h.Platoon); if (pl != null) DistAbatidoMm.Add(IMath.Dist(zs[h.TargetId].X - pl.AX, zs[h.TargetId].Y - pl.AY));
                Zombis.Abatir(h.TargetId); Abatidos++; Unidades.NoteKill(h.Platoon);
            }
            // zombis: vista, oído, deambular y andar
            Zombis.Step(Unidades, Rejilla, _res.Shots, Tick);
            if (Prof) { long t1 = System.Diagnostics.Stopwatch.GetTimestamp(); ProfZombis += t1 - t0; t0 = t1; }
            // contacto con los soldados
            _blancos.Clear();
            for (int i = 0; i < zs.Count; i++) if (zs[i].Vivo && CercaDePeloton(zs[i].X, zs[i].Y)) _blancos.Add(new Target { Id = i, X = zs[i].X, Y = zs[i].Y });
            int caidos = Unidades.ZombieContact(_blancos, ContactoMm, ContactoDanioTick, _contacto); BajasPropias += caidos; for (int k = 0; k < caidos; k++) TickBaja.Add(Tick);
            if (RetiradaSinMunicion) Retiradas();
            Tick++;
        }

        int CosteMinimo(Platoon p)
        {
            int min = int.MaxValue;
            foreach (var m in p.Men) { if (!m.Alive) continue; if (m.W0 >= 0) min = Math.Min(min, WeaponCatalog.Table[m.W0].CostShots); if (m.W1 >= 0) min = Math.Min(min, WeaponCatalog.Table[m.W1].CostShots); }
            return min == int.MaxValue ? 1 : Math.Max(1, min);
        }

        void Retiradas()
        {
            if (!Puntos.TryGetValue(PuntoRetirada, out var b)) return;
            foreach (var p in Unidades.Platoons)
            {
                if (p.Aboard != 0 || p.AliveCount() == 0) continue;
                long dbx = p.AX - b.X, dby = p.AY - b.Y;
                bool enB = dbx * dbx + dby * dby <= (long)RecargaRadioMm * RecargaRadioMm;
                if (!enB) _avisadoSinCajas.Remove(p.Id);
                else if (p.AmmoShots < p.AmmoCap && Tick % IMath.TickRate == 0) RecargarEnB(p);      // en B se recarga de las cajas del almacén de la red      // en B se recarga de las cajas del almacén de la red
                bool seco = p.AmmoShots < CosteMinimo(p);
                if (seco && !_retirandose.Contains(p.Id)) { _retirandose.Add(p.Id); Unidades.Move(p.Id, b.X, b.Y); }
                // Si el jugador lo manda a otra parte estando seco (Mundo8 de Marcos: AtacarMoviendo sin munición), obedece; pero cuando llega y se para fuera de B,
                // vuelve solo a B en lugar de quedarse quieto entre los zombis sin poder disparar.
                else if (seco && !p.Moving && !enB) Unidades.Move(p.Id, b.X, b.Y);
                else if (!seco && _retirandose.Contains(p.Id)) _retirandose.Remove(p.Id);
            }
        }
        /// <summary>La unión de los dos géneros (Iker, de8dabb5): el pelotón en B se recarga de las cajas de munición del almacén de B de la RED (Red.Sacar), que llena el tren
        /// de la línea. Se abre una caja entera de CartuchosPorCaja; lo que sobra queda suelto en B para la siguiente recarga. Sin cajas en B, no hay recarga.</summary>
        void RecargarEnB(Platoon p)
        {
            int b = Red.EstacionPorNombre(PuntoRetirada); if (b < 0) return;
            int falta = p.AmmoCap - p.AmmoShots;
            int abiertas = 0;
            while (_sueltosB < falta && Red.Sacar(b, Red.Municion, 1) == 1) { _sueltosB += CartuchosPorCaja; CajasAbiertas++; abiertas++; }
            int dar = Math.Min(falta, _sueltosB);
            if (dar <= 0)
            {
                // Una vez por llegada a B (se rearma al salir del radio o al recargar), no cada segundo.
                if (_avisadoSinCajas.Add(p.Id)) Evento("[RECARGA] sin cajas en B (pelotón " + p.Id + ", tick " + Tick + ", munición " + p.AmmoShots + "/" + p.AmmoCap + ")");
                return;
            }
            _sueltosB -= dar; p.AmmoShots += dar; _avisadoSinCajas.Remove(p.Id);
            Evento("[RECARGA] " + abiertas + " cajas → " + dar + " cartuchos (pelotón " + p.Id + ", tick " + Tick + ", munición " + p.AmmoShots + "/" + p.AmmoCap + ", sueltos en B " + _sueltosB + ", cajas en B " + Red.Existencias(b, Red.Municion) + ")");
        }
        readonly HashSet<int> _avisadoSinCajas = new HashSet<int>();
        /// <summary>Registro de sucesos de la simulación (deterministas: mismo texto en las dos instancias). No entra en el hash; se deriva del estado.
        /// Unity lo engancha a su log con <c>mundo.Log = Debug.Log</c>; las pruebas leen <see cref="Eventos"/>.</summary>
        public Action<string> Log;
        public readonly List<string> Eventos = new List<string>();
        public int EventosMax = 2000;
        void Evento(string linea) { if (Eventos.Count < EventosMax) Eventos.Add(linea); Log?.Invoke(linea); }
        int _sueltosB; public int CajasAbiertas;
        public int SueltosEnB { get { return _sueltosB; } }

        ulong HashRetiradas() { ulong h = (RetiradaSinMunicion ? 0x5E7UL : 0) + IMath.Mix(0x5E71UL ^ (ulong)(uint)_sueltosB ^ ((ulong)(uint)CajasAbiertas << 32)); foreach (int id in _retirandose) h += IMath.Mix(0x5E70UL ^ (ulong)(uint)id); return h; }
        public bool SeRetira(int peloton) { return _retirandose.Contains(peloton); }

        /// <summary>Medida de coste (fuera del hash): ticks de Stopwatch en el fuego de UnitsWorld y en los zombis.</summary>
        public static bool Prof; public static long ProfUnidades, ProfZombis;
        public StepResult UltimoFuego { get { return _res; } }

        // ---- hash por sistema (Sara, plan-hito1 §3: «desync=True@tick · difiere: <sistemas>»)
        /// <summary>Hash de cada sistema, con nombre y en orden fijo: «reloj», «unidades», «zombis» y lo que añada la red (Iker: «red.via», «red.trenes», «red.almacen», «red.cargas»).</summary>
        public List<KeyValuePair<string, ulong>> HashPorSistema()
        {
            var l = new List<KeyValuePair<string, ulong>>
            {
                new KeyValuePair<string, ulong>("reloj", IMath.Mix(0x4D0D0UL ^ (ulong)(uint)Tick ^ ((ulong)(uint)Abatidos << 32) ^ ((ulong)(uint)BajasPropias << 52)) + HashRetiradas()),
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

        // ---- escenario del Hito 1: el caserío (datos de Álvaro, Assets/StreamingAssets/Hito1/, alvaro/hito1-terreno f864a1e6)
        public const string EscenarioCaserio = "caserio";
        public const string ArchivoPosiciones = "posiciones_hito1.csv", ArchivoObstaculos = "obstaculos_hito1.csv";
        /// <summary>Lector de los CSV del Hito 1 (nombre → texto). Unity lo apunta a StreamingAssets/Hito1; las pruebas, a la carpeta del repositorio.</summary>
        public static Func<string, string> Lector;
        /// <summary>Puntos con nombre de posiciones_hito1.csv (A, B, caserio, deposito_material, peloton_inicio, puente_carretera), en mm (X este, Y = Z de Unity, norte).</summary>
        public readonly Dictionary<string, PointMm> Puntos = new Dictionary<string, PointMm>();
        public static int CaserioX = 1090000, CaserioY = 1530000;

        /// <summary>Si (x, y) no está libre, lo lleva al centro de la celda libre más cercana (anillos de hasta 10 m, orden fijo).</summary>
        public bool LibreCercano(ref int x, ref int y)
        {
            if (Rejilla.Libre(x, y)) return false;
            int cx = Rejilla.CeldaX(x), cy = Rejilla.CeldaY(y);
            for (int r = 1; r <= 10; r++)
            {
                int mejor = -1, mx = 0, my = 0;
                for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue; if (!Rejilla.LibreCelda(cx + dx, cy + dy)) continue;
                    int px = Rejilla.CentroX(cx + dx), py = Rejilla.CentroY(cy + dy), d = IMath.Dist(px - x, py - y); if (mejor < 0 || d < mejor) { mejor = d; mx = px; my = py; }
                }
                if (mejor >= 0) { x = mx; y = my; Recolocados++; return true; }
            }
            return false;
        }
        public int Recolocados;

        static int Mm(string s) { return (int)Math.Round(double.Parse(s, System.Globalization.CultureInfo.InvariantCulture) * 1000.0); }
        static string[] Campos(string linea) { var l = new List<string>(); var sb = new StringBuilder(); bool q = false; foreach (char c in linea) { if (c == '"') q = !q; else if (c == ';' && !q) { l.Add(sb.ToString()); sb.Clear(); } else sb.Append(c); } l.Add(sb.ToString()); return l.ToArray(); }

        /// <summary>Obstáculos de obstaculos_hito1.csv (mismo esquema que sagunto_obstaculos_v1.csv): «caja» = rectángulo; «seg» = tramo engordado con medio_grosor.</summary>
        public static List<Casa> ObstaculosDeCsv(string csv)
        {
            var l = new List<Casa>(); if (string.IsNullOrEmpty(csv)) return l;
            var lineas = csv.Split('\n');
            for (int k = 1; k < lineas.Length; k++)
            {
                var t = lineas[k].Trim(); if (t.Length == 0 || t.StartsWith("#")) continue; var f = Campos(t); if (f.Length < 11) continue;
                int x0 = int.Parse(f[3]), y0 = int.Parse(f[4]), x1 = int.Parse(f[5]), y1 = int.Parse(f[6]), g = int.Parse(f[7]), h = int.Parse(f[8]);
                var r = new RectMm(Math.Min(x0, x1) - g, Math.Min(y0, y1) - g, Math.Max(x0, x1) + g, Math.Max(y0, y1) + g);
                l.Add(new Casa { Id = f[0], R = r, AlturaMm = h, TapaVision = f[9].Trim() == "si", TapaPaso = f[10].Trim() == "si" });
            }
            return l;
        }

        /// <summary>El Hito 1: terreno de 2048 × 2048 m con los obstáculos de Álvaro, sus 100 zombis (cada uno con su sitio en el caserío donde empieza) y un pelotón
        /// de fusileros en la salida de B (peloton_inicio). Lee los CSV con «Lector»; si falta alguno, lanza una excepción (sin datos no hay caserío).</summary>
        public static Mundo Caserio(uint semilla)
        {
            string pos = Lector != null ? Lector(ArchivoPosiciones) : null, obs = Lector != null ? Lector(ArchivoObstaculos) : null;
            if (pos == null || obs == null) throw new InvalidOperationException("Mundo.Caserio: faltan " + ArchivoPosiciones + " u " + ArchivoObstaculos + " (Mundo.Lector)");
            var m = new Mundo(semilla, 2048000, 2048000);
            m.PonerCasas(ObstaculosDeCsv(obs));
            int yawPeloton = 0; bool hayPeloton = false;
            var lineas = pos.Split('\n');
            for (int k = 1; k < lineas.Length; k++)
            {
                var t = lineas[k].Trim(); if (t.Length == 0) continue; var f = Campos(t); if (f.Length < 6) continue;
                int x = Mm(f[2]), y = Mm(f[4]);
                if (f[1] == "zombi") { m.LibreCercano(ref x, ref y); m.Zombis.Anadir(x, y, x, y); }      // un zombi puesto encima de una tapia o una casa sale a la celda libre más cercana
                else { m.Puntos[f[0]] = new PointMm(x, y); if (f[1] == "peloton") { hayPeloton = true; yawPeloton = (int)Math.Round(double.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture)); } }
            }
            if (m.Puntos.TryGetValue("caserio", out var cs)) { CaserioX = cs.X; CaserioY = cs.Y; }
            if (hayPeloton)
            {
                var pi = m.Puntos["peloton_inicio"]; int rumbo = (int)((long)(90 - yawPeloton) * 65536 / 360) & 0xFFFF;     // Unity: yaw 0 = +Z (norte), 90 = +X; aquí rumbo 0 = +X
                var pl = m.Unidades.AddPlatoon(PlatoonKind.Fusileros, pi.X, pi.Y, rumbo); pl.AmmoCap = pl.AmmoShots = CartuchosPorSoldado * pl.Men.Count;
            }
            m.MontarRedHito1();      // Iker (MundoRed.cs): estaciones A y B desde Puntos, carreteras (carreteras_hito1.csv), «Tren 1» y 20 cajas en A
            { int eb = m.Red.EstacionPorNombre("B"); if (eb >= 0) m.Red.Meter(eb, Red.Municion, CajasInicialesB); }
            return m;
        }
    }
}
