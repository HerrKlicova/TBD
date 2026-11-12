using System;
using System.Collections.Generic;
using TBD.Net;
using TBD.Units;

namespace TBD.Mundo
{
    public enum EstadoTren : byte { Parado, EnMarcha, EnParada }
    public enum OrdenRed : byte { TrazarVia = 1, CrearLinea = 2, AsignarTren = 3, NuevoTren = 4 }

    /// <summary>Un tren de la red construida: sigue una ruta de tramos con aceleración y frenada en enteros (mm, mm/tick, mm/tick²).
    /// Posición = (paso de la ruta, mm recorridos dentro de ese tramo en su sentido de marcha).</summary>
    public sealed class Tren
    {
        public int Id; public string Nombre;
        public int Nodo;                                   // nodo donde está parado (−1 en marcha)
        public List<Via.Paso> Ruta = new List<Via.Paso>(); public int PasoI, OffMm;
        public int VelMmT, VmaxMmT = 370, AccMmT2 = 1;      // Hugo (cifras-hito1): 40 km/h = 370 mm/tick; 1 mm/tick² ≈ 0,9 m/s²
        public EstadoTren Estado;
        public int Linea = -1, Parada, EsperaT;
        public int InicioVueltaT = -1, UltimaVueltaT = -1;      // Hugo: el texto de la línea saca el tiempo de la vuelta medida, no de un número fijo
        public readonly int[] Carga = new int[Red.Recursos]; public int Capacidad = 6;      // Hugo: 6 huecos (2 vagones de 3); 1 hueco = 1 caja de munición (105 cartuchos = 3 cargadores de 35, Mundo.CartuchosPorCaja) o 1 palé de material (100 m de vía)
        /// <summary>Distancia que queda hasta el final de la ruta (mm).</summary>
        public int Restante(Via v) { if (Ruta.Count == 0) return 0; long r = v.Tramos[Ruta[PasoI].Tramo].LongMm - OffMm; for (int i = PasoI + 1; i < Ruta.Count; i++) r += v.Tramos[Ruta[i].Tramo].LongMm; return (int)Math.Min(r, int.MaxValue); }
    }

    /// <summary>Una parada de una línea: en la estación se descarga lo indicado y luego se carga lo indicado (u por recurso; −1 = todo lo que se pueda).</summary>
    public struct ParadaLinea { public int Estacion; public int[] Cargar, Descargar; }
    public sealed class Linea { public int Id; public string Nombre; public readonly List<ParadaLinea> Paradas = new List<ParadaLinea>(); }

    /// <summary>
    /// Mundo.Red (contrato de Nacho, 11-nov): la vía construida, los trenes y las líneas automáticas. Lógica pura en enteros; Mundo.Step la llama con Step(tick)
    /// y le pasa las órdenes del jugador con Aplicar. La vista solo lee (Via, Trenes) e interpola.
    /// </summary>
    public sealed class Red
    {
        public const int Recursos = 4;                     // 0 munición (cajas de 105 cartuchos, Mundo.CartuchosPorCaja), 1 combustible, 2 material de vía (palés de 100 m), 3 víveres; Hito 1: munición y material
        public const int Municion = 0, Material = 2;
        public const int TicksPorU = 150;                  // Hugo (12-nov): 5 s por hueco cargado o descargado, en A y en B (antes 15 s)
        public const int EsperaMinT = 90;                  // al menos 3 s parado en cada parada (además de la carga)
        public readonly Via Via = new Via();
        /// <summary>Hugo (cifras-hito1): el material de vía se cobra en METROS enteros (1 palé = 100 m; curvas ×1,5; paso a nivel 200 m; Via.CosteMetros), sin redondear por tramo.
        /// La estación guarda palés enteros (lo que lleva el tren) y los metros sueltos de un palé abierto. Paga la primera estación (por orden) unida por vía con el punto
        /// de salida que tenga bastante; al borrar el tramo se le devuelve el 100 %. Lo activa MontarRedHito1; sin él, la vía es gratis.</summary>
        public bool CuestaMaterial;
        public const int MPorPale = 100;
        public readonly List<int> MetrosSueltos = new List<int>();     // por estación: metros de un palé ya abierto (0–99)
        public Red() { Via.Pagar = Cobrar; }
        public int MaterialM(int e) { return e < 0 || e >= Almacen.Count ? 0 : Almacen[e][Material] * MPorPale + (e < MetrosSueltos.Count ? MetrosSueltos[e] : 0); }
        /// <summary>Lucía (12-nov): material como «N palés y M m», sin decimales ni redondeo («14 palés y 90 m»; los metros se omiten si son 0; «90 m» si no llega a un palé).</summary>
        public static string Pales(int metros)
        {
            if (metros < 0) metros = 0; int p = metros / MPorPale, m = metros % MPorPale;
            string sp = p == 1 ? "1 palé" : $"{p} palés";
            return p == 0 ? $"{m} m" : m == 0 ? sp : $"{sp} y {m} m";
        }
        string Cobrar(int desde, int costeM, out int estacion)
        {
            estacion = -1; if (!CuestaMaterial) return null;
            int mas = 0;
            for (int i = 0; i < Via.Estaciones.Count; i++)
            {
                int n = Via.Estaciones[i].Nodo; if (n != desde && Via.Ruta(n, desde) == null) continue;
                int hay = MaterialM(i);
                if (hay >= costeM) { int queda = hay - costeM; Almacen[i][Material] = queda / MPorPale; MetrosSueltos[i] = queda % MPorPale; estacion = i; return null; }
                if (hay > mas) mas = hay;
            }
            return $"falta material: esta vía cuesta {Pales(costeM)} y faltan {Pales(costeM - mas)}";
        }
        void Devolver(int e, int metros) { if (e < 0 || e >= Almacen.Count || metros <= 0) return; int t = MaterialM(e) + metros; Almacen[e][Material] = t / MPorPale; MetrosSueltos[e] = t % MPorPale; }
        public readonly List<Tren> Trenes = new List<Tren>();
        public readonly List<Linea> Lineas = new List<Linea>();
        public readonly List<int[]> Almacen = new List<int[]>();     // por estación
        public int Tick;
        /// <summary>Último rechazo de una orden (para la vista); no entra en el hash.</summary>
        public string UltimoRechazo;

        public int AddEstacion(string nombre, int x, int y) { int e = Via.AddEstacion(nombre, x, y); Almacen.Add(new int[Recursos]); MetrosSueltos.Add(0); return e; }
        /// <summary>Índice de la estación con ese nombre («A», «B»…), o −1.</summary>
        public int EstacionPorNombre(string nombre) { for (int i = 0; i < Via.Estaciones.Count; i++) if (Via.Estaciones[i].Nombre == nombre) return i; return -1; }
        /// <summary>Unidades del recurso (Municion, Material…) en el almacén de la estación; 0 si no existe.</summary>
        public int Existencias(int estacion, int recurso) { return estacion < 0 || estacion >= Almacen.Count || recurso < 0 || recurso >= Recursos ? 0 : Almacen[estacion][recurso]; }
        /// <summary>Saca hasta «n» unidades del almacén y devuelve las que ha sacado de verdad (0 si no hay). Determinista: llamarla solo desde el paso de la simulación.</summary>
        public int Sacar(int estacion, int recurso, int n) { int hay = Existencias(estacion, recurso); int k = n <= 0 ? 0 : (n < hay ? n : hay); if (k > 0) Almacen[estacion][recurso] -= k; return k; }
        /// <summary>Mete unidades en el almacén (por ejemplo, el material que produce el frente).</summary>
        public void Meter(int estacion, int recurso, int n) { if (n > 0 && estacion >= 0 && estacion < Almacen.Count && recurso >= 0 && recurso < Recursos) Almacen[estacion][recurso] += n; }
        public int NuevoTren(string nombre, int estacion) { var t = new Tren { Id = Trenes.Count, Nombre = nombre, Nodo = Via.Estaciones[estacion].Nodo, Estado = EstadoTren.Parado }; Trenes.Add(t); return t.Id; }

        /// <summary>D-N209: trazo libre del jugador → piezas del kit. (Via.Trazar, la curva libre, queda para pruebas.)</summary>
        public Via.Resultado TrazarVia(IList<PointMm> puntos) { var r = Via.TrazarLibre(puntos); if (!r.Ok) UltimoRechazo = r.Por; return r; }

        /// <summary>Borra un tramo de vía construida. Determinista, también con trenes en marcha:
        /// · un tren que está ENCIMA del tramo lo impide (se rechaza: «hay un tren en ese tramo»);
        /// · un tren cuya ruta pasa por él más adelante acaba el tramo en el que va, para en el nodo siguiente y desde ahí busca otra ruta
        ///   (si no la hay, queda parado y lo reintenta cada segundo, como sin vía).</summary>
        public bool BorrarVia(int tramo)
        {
            if (tramo < 0 || tramo >= Via.Tramos.Count || Via.Tramos[tramo].Borrado) { UltimoRechazo = "ese tramo no existe"; return false; }
            foreach (var t in Trenes) if (t.Estado == EstadoTren.EnMarcha && t.Ruta.Count > t.PasoI && t.Ruta[t.PasoI].Tramo == tramo) { UltimoRechazo = $"hay un tren en ese tramo ({t.Nombre})"; return false; }
            var tb = Via.Tramos[tramo]; Devolver(tb.Pago, tb.Coste);      // Hugo: borrar devuelve el 100 % en el Hito 1
            Via.BorrarTramo(tramo);
            foreach (var t in Trenes)
            {
                if (t.Estado != EstadoTren.EnMarcha) continue;
                for (int i = t.PasoI + 1; i < t.Ruta.Count; i++) if (t.Ruta[i].Tramo == tramo) { t.Ruta.RemoveRange(t.PasoI + 1, t.Ruta.Count - t.PasoI - 1); break; }
            }
            return true;
        }

        /// <summary>Añade una parada (o depósito) a una línea existente en la posición «pos» (fuera de rango = al final). El tren que ya la recorre la respeta:
        /// si se inserta antes de su parada actual, su índice se corre uno para seguir yendo al mismo sitio.</summary>
        public bool AnadirParada(int linea, int pos, ParadaLinea p)
        {
            if (linea < 0 || linea >= Lineas.Count || p.Estacion < 0 || p.Estacion >= Via.Estaciones.Count) { UltimoRechazo = "parada no válida"; return false; }
            if (p.Cargar == null) p.Cargar = new int[Recursos]; if (p.Descargar == null) p.Descargar = new int[Recursos];
            var l = Lineas[linea]; if (pos < 0 || pos > l.Paradas.Count) pos = l.Paradas.Count;
            l.Paradas.Insert(pos, p);
            foreach (var t in Trenes) if (t.Linea == linea && t.Parada >= pos) t.Parada++;
            return true;
        }

        public int CrearLinea(string nombre, IList<ParadaLinea> paradas)
        {
            if (paradas == null || paradas.Count < 2) { UltimoRechazo = "una línea necesita al menos dos paradas"; return -1; }
            var l = new Linea { Id = Lineas.Count, Nombre = nombre }; foreach (var p in paradas) l.Paradas.Add(p); Lineas.Add(l); return l.Id;
        }

        /// <summary>Asigna el tren a la línea: si está parado, sale hacia la primera parada (o la atiende si ya está en ella).</summary>
        public bool AsignarTren(int linea, int tren)
        {
            if (linea < 0 || linea >= Lineas.Count || tren < 0 || tren >= Trenes.Count) return false;
            var t = Trenes[tren]; t.Linea = linea; t.Parada = 0;
            if (t.Estado != EstadoTren.EnMarcha) IrAParada(t);
            return true;
        }

        void IrAParada(Tren t)
        {
            var l = Lineas[t.Linea]; int destNodo = Via.Estaciones[l.Paradas[t.Parada].Estacion].Nodo;
            if (t.Nodo == destNodo) { t.Estado = EstadoTren.EnParada; t.EsperaT = 0; return; }
            var ruta = Via.Ruta(t.Nodo, destNodo);
            if (ruta == null) { t.Estado = EstadoTren.Parado; UltimoRechazo = t.Nombre + ": no hay vía hasta " + Via.Estaciones[l.Paradas[t.Parada].Estacion].Nombre; return; }
            t.Ruta = ruta; t.PasoI = 0; t.OffMm = 0; t.Nodo = -1; t.Estado = EstadoTren.EnMarcha;
        }

        /// <summary>Línea por defecto decidida por la SIMULACIÓN (no por la vista): en cuanto la estación 0 y la 1 quedan unidas por vía, se crea la línea de Hugo
        /// (4 cajas de 0 a 1, 2 palés de vuelta) y se le asigna el tren 0. Una sola vez. La activa MontarRedHito1; igual en todos los jugadores.</summary>
        public bool LineaAuto;
        public int CrearLineaPorDefecto(int a, int b, int cajas = 4, int pales = 2)
        {
            var cA = new int[Recursos]; cA[Municion] = cajas; var dA = new int[Recursos]; dA[Material] = -1;
            var cB = new int[Recursos]; cB[Material] = pales; var dB = new int[Recursos]; dB[Municion] = -1;
            return CrearLinea("Línea " + (Lineas.Count + 1), new List<ParadaLinea> { new ParadaLinea { Estacion = a, Cargar = cA, Descargar = dA }, new ParadaLinea { Estacion = b, Cargar = cB, Descargar = dB } });
        }
        void LineaAutoSiUnidas()
        {
            if (!LineaAuto || (Tick % IMath.TickRate) != 0) return;
            if (Lineas.Count > 0) { LineaAuto = false; return; }      // alguien ya hizo una línea a mano
            if (Via.Estaciones.Count < 2 || Trenes.Count == 0 || Via.Ruta(Via.Estaciones[0].Nodo, Via.Estaciones[1].Nodo) == null) return;
            int l = CrearLineaPorDefecto(0, 1); AsignarTren(l, 0); LineaAuto = false; LineaAutoCreadaTick = Tick;
        }
        public int LineaAutoCreadaTick = -1;

        public void Step(int tick)
        {
            Tick = tick;
            LineaAutoSiUnidas();
            foreach (var t in Trenes)
            {
                if (t.Estado == EstadoTren.EnMarcha) Mover(t);
                else if (t.Estado == EstadoTren.EnParada) Atender(t);
                else if (t.Linea >= 0 && (tick % IMath.TickRate) == 0) IrAParada(t);      // parado con línea (sin vía): lo reintenta cada segundo
            }
        }

        void Mover(Tren t)
        {
            int rest = t.Restante(Via);
            int frenado = (int)((long)t.VelMmT * t.VelMmT / (2 * Math.Max(1, t.AccMmT2)));
            if (rest <= frenado + t.VelMmT) t.VelMmT = Math.Max(Math.Min(t.VelMmT, 40), t.VelMmT - t.AccMmT2 * 2);      // frena (con un mínimo de 40 mm/tick para llegar)
            else t.VelMmT = Math.Min(t.VmaxMmT, t.VelMmT + t.AccMmT2);
            int avance = Math.Min(t.VelMmT, rest);
            while (avance > 0)
            {
                int lt = Via.Tramos[t.Ruta[t.PasoI].Tramo].LongMm, queda = lt - t.OffMm;
                if (avance < queda) { t.OffMm += avance; avance = 0; }
                else { avance -= queda; if (t.PasoI + 1 < t.Ruta.Count) { t.PasoI++; t.OffMm = 0; } else { t.OffMm = lt; avance = 0; } }
            }
            if (t.Restante(Via) <= 0)
            {
                var ul = t.Ruta[t.Ruta.Count - 1]; var tr = Via.Tramos[ul.Tramo]; t.Nodo = ul.Directo ? tr.B : tr.A;
                t.VelMmT = 0; t.Ruta.Clear(); t.PasoI = 0; t.OffMm = 0;
                t.Estado = t.Linea >= 0 ? EstadoTren.EnParada : EstadoTren.Parado; t.EsperaT = 0;
                if (t.Linea >= 0 && t.Nodo != Via.Estaciones[Lineas[t.Linea].Paradas[t.Parada].Estacion].Nodo) IrAParada(t);      // ruta cortada por una vía borrada: reencamina desde aquí
            }
        }

        /// <summary>En la parada: primero descarga y luego carga, 1 u cada TicksPorU; cuando acaba (y lleva al menos EsperaMinT), sale hacia la siguiente parada.</summary>
        void Atender(Tren t)
        {
            t.EsperaT++;
            var l = Lineas[t.Linea]; var p = l.Paradas[t.Parada]; var alm = Almacen[p.Estacion];
            int r = SiguienteTrabajo(t, p, alm, out bool descargar);
            if (r >= 0 && (t.EsperaT % TicksPorU) == 0)      // cada hueco tarda TicksPorU (Hugo: 5 s): primero se descarga, luego se carga
            {
                if (descargar) { t.Carga[r]--; alm[r]++; Marcar(t, r, false); } else { alm[r]--; t.Carga[r]++; Marcar(t, r, true); }
                r = SiguienteTrabajo(t, p, alm, out descargar);
            }
            if (r < 0 && t.EsperaT >= EsperaMinT)
            {
                if (t.Parada == 0) { if (t.InicioVueltaT >= 0) t.UltimaVueltaT = Tick - t.InicioVueltaT; t.InicioVueltaT = Tick; }      // la vuelta se mide de salida a salida de la primera parada
                LimpiarHechos(t); t.Parada = (t.Parada + 1) % l.Paradas.Count; IrAParada(t);
            }
        }

        /// <summary>El recurso que toca mover ahora en esta parada (−1 si nada): descargar lo pedido que lleve, y luego cargar lo pedido que haya y quepa.</summary>
        int SiguienteTrabajo(Tren t, ParadaLinea p, int[] alm, out bool descargar)
        {
            descargar = true;
            for (int r = 0; r < Recursos; r++) if (Pide(p.Descargar, r) && t.Carga[r] > 0 && (p.Descargar[r] < 0 || Hecho(t, r, false) < p.Descargar[r])) return r;
            descargar = false;
            for (int r = 0; r < Recursos; r++) if (Pide(p.Cargar, r) && alm[r] > 0 && CargaTotal(t) < t.Capacidad && (p.Cargar[r] < 0 || Hecho(t, r, true) < p.Cargar[r])) return r;
            return -1;
        }

        // lo cargado/descargado en esta parada (por tren y recurso), para no pasar de lo pedido
        readonly Dictionary<int, int[]> _hechoC = new Dictionary<int, int[]>(), _hechoD = new Dictionary<int, int[]>();
        int Hecho(Tren t, int r, bool cargar) { var d = cargar ? _hechoC : _hechoD; return d.TryGetValue(t.Id, out var a) ? a[r] : 0; }
        void Marcar(Tren t, int r, bool cargar) { var d = cargar ? _hechoC : _hechoD; if (!d.TryGetValue(t.Id, out var a)) d[t.Id] = a = new int[Recursos]; a[r]++; }
        void LimpiarHechos(Tren t) { _hechoC.Remove(t.Id); _hechoD.Remove(t.Id); }
        static bool Pide(int[] a, int r) { return a != null && r < a.Length && a[r] != 0; }
        static int CargaTotal(Tren t) { int s = 0; foreach (var c in t.Carga) s += c; return s; }

        /// <summary>Texto de la línea para la interfaz (Hugo): «4 cajas por viaje · un viaje cada 7,2 min», con la vuelta medida del primer tren de la línea; «midiendo» hasta la primera vuelta.</summary>
        public string TextoLinea(int linea)
        {
            if (linea < 0 || linea >= Lineas.Count) return "";
            var p0 = Lineas[linea].Paradas[0]; int cajas = p0.Cargar != null && p0.Cargar.Length > Municion ? p0.Cargar[Municion] : 0;
            int vuelta = -1; foreach (var t in Trenes) if (t.Linea == linea && t.UltimaVueltaT > 0) { vuelta = t.UltimaVueltaT; break; }
            string c = cajas < 0 ? "lo que quepa" : cajas + (cajas == 1 ? " caja" : " cajas");
            if (vuelta < 0) return c + " por viaje · midiendo la vuelta…";
            int decimas = (vuelta * 10 + IMath.TickRate * 30) / (IMath.TickRate * 60);      // minutos con un decimal, redondeado
            return $"{c} por viaje · un viaje cada {decimas / 10},{decimas % 10} min";
        }

        /// <summary>Posición del tren (mm) y rumbo (bam16) para la vista. Parado: la del nodo.</summary>
        public PointMm Posicion(Tren t, out int rumbo)
        {
            rumbo = 0;
            if (t.Estado != EstadoTren.EnMarcha || t.Ruta.Count == 0) { var n = Via.Nodos[Math.Max(0, t.Nodo)]; return new PointMm(n.X, n.Y); }
            var ps = t.Ruta[t.PasoI]; var tr = Via.Tramos[ps.Tramo];
            var p = Via.Posicion(ps.Tramo, ps.Directo ? t.OffMm : tr.LongMm - t.OffMm, out rumbo); if (!ps.Directo) rumbo = (rumbo + IMath.Turn / 2) & 0xFFFF;
            return p;
        }

        /// <summary>Hash por sistema para el registro de desincronización (-hashlog de Sara/Nacho): «red.via», «red.trenes», «red.almacen», «red.cargas». Mundo los junta con los suyos.</summary>
        public void HashPorSistema(List<KeyValuePair<string, ulong>> salida)
        {
            salida.Add(new KeyValuePair<string, ulong>("red.via", Via.Hash()));
            ulong ht = 0x51UL; foreach (var t in Trenes) { ht = IMath.Mix(ht ^ (ulong)t.Id ^ ((ulong)(uint)t.Nodo << 8) ^ ((ulong)t.PasoI << 24) ^ ((ulong)(uint)t.OffMm << 32)); ht = IMath.Mix(ht ^ (ulong)(uint)t.VelMmT ^ ((ulong)t.Estado << 20) ^ ((ulong)(uint)(t.Linea + 1) << 24) ^ ((ulong)(uint)t.Parada << 40) ^ ((ulong)(uint)t.EsperaT << 48)); ht = IMath.Mix(ht ^ (ulong)(uint)(t.InicioVueltaT + 1) ^ ((ulong)(uint)(t.UltimaVueltaT + 1) << 32)); foreach (var ps in t.Ruta) ht = IMath.Mix(ht ^ (ulong)ps.Tramo ^ (ps.Directo ? 1UL << 40 : 0UL)); }
            if (LineaAuto) ht = IMath.Mix(ht ^ 0xA070UL); if (CuestaMaterial) ht = IMath.Mix(ht ^ 0xC057UL);
            salida.Add(new KeyValuePair<string, ulong>("red.trenes", ht));
            ulong ha = 0x52UL; foreach (var a in Almacen) foreach (var c in a) ha = IMath.Mix(ha ^ (ulong)(uint)c); foreach (var m in MetrosSueltos) if (m != 0) ha = IMath.Mix(ha ^ 0x5E00UL ^ (ulong)(uint)m); salida.Add(new KeyValuePair<string, ulong>("red.almacen", ha));
            ulong hc = 0x53UL; foreach (var t in Trenes) foreach (var c in t.Carga) hc = IMath.Mix(hc ^ (ulong)(uint)c); foreach (var kv in _hechoC) foreach (var c in kv.Value) hc = IMath.Mix(hc ^ (ulong)kv.Key ^ (ulong)(uint)c << 16); foreach (var kv in _hechoD) foreach (var c in kv.Value) hc = IMath.Mix(hc ^ (ulong)kv.Key ^ (ulong)(uint)c << 24);
            salida.Add(new KeyValuePair<string, ulong>("red.cargas", hc));
        }

        public ulong Hash()
        {
            ulong h = Via.Hash() ^ ((ulong)(uint)Tick << 7); if (LineaAuto) h = IMath.Mix(h ^ 0xA070UL); if (CuestaMaterial) h = IMath.Mix(h ^ 0xC057UL);
            foreach (var t in Trenes)
            {
                h = IMath.Mix(h ^ (ulong)t.Id ^ ((ulong)(uint)t.Nodo << 8) ^ ((ulong)t.PasoI << 24) ^ ((ulong)(uint)t.OffMm << 32));
                h = IMath.Mix(h ^ (ulong)(uint)t.VelMmT ^ ((ulong)t.Estado << 20) ^ ((ulong)(uint)(t.Linea + 1) << 24) ^ ((ulong)(uint)t.Parada << 40) ^ ((ulong)(uint)t.EsperaT << 48)); h = IMath.Mix(h ^ (ulong)(uint)(t.InicioVueltaT + 1) ^ ((ulong)(uint)(t.UltimaVueltaT + 1) << 32));
                foreach (var c in t.Carga) h = IMath.Mix(h ^ (ulong)(uint)c);
                foreach (var ps in t.Ruta) h = IMath.Mix(h ^ (ulong)ps.Tramo ^ (ps.Directo ? 1UL << 40 : 0UL));
            }
            foreach (var a in Almacen) foreach (var c in a) h = IMath.Mix(h ^ (ulong)(uint)c);
            foreach (var m in MetrosSueltos) if (m != 0) h = IMath.Mix(h ^ 0x5E00UL ^ (ulong)(uint)m);
            foreach (var kv in _hechoC) foreach (var c in kv.Value) h = IMath.Mix(h ^ (ulong)kv.Key ^ (ulong)(uint)c << 16);
            foreach (var kv in _hechoD) foreach (var c in kv.Value) h = IMath.Mix(h ^ (ulong)kv.Key ^ (ulong)(uint)c << 24);
            return h;
        }

        public void Save(SaveWriter w)
        {
            Via.Save(w); w.Int(Tick); w.Int(LineaAuto ? 1 : 0); w.Int(CuestaMaterial ? 1 : 0);
            w.Int(Almacen.Count); foreach (var a in Almacen) w.Ints(a); w.Ints(MetrosSueltos.ToArray());
            w.Int(Lineas.Count); foreach (var l in Lineas) { w.Str(l.Nombre); w.Int(l.Paradas.Count); foreach (var p in l.Paradas) { w.Int(p.Estacion); w.Ints(p.Cargar); w.Ints(p.Descargar); } }
            w.Int(Trenes.Count);
            foreach (var t in Trenes)
            {
                w.Str(t.Nombre); w.Int(t.Nodo); w.Int(t.PasoI); w.Int(t.OffMm); w.Int(t.VelMmT); w.Int(t.VmaxMmT); w.Int(t.AccMmT2); w.Byte((byte)t.Estado);
                w.Int(t.Linea); w.Int(t.Parada); w.Int(t.EsperaT); w.Int(t.InicioVueltaT); w.Int(t.UltimaVueltaT); w.Ints(t.Carga); w.Int(t.Capacidad);
                w.Int(t.Ruta.Count); foreach (var ps in t.Ruta) { w.Int(ps.Tramo); w.Bool(ps.Directo); }
                w.Ints(_hechoC.TryGetValue(t.Id, out var hc) ? hc : null); w.Ints(_hechoD.TryGetValue(t.Id, out var hd) ? hd : null);
            }
        }

        public void Load(SaveReader r)
        {
            Via.Load(r); Tick = r.Int(); LineaAuto = r.Int() != 0; CuestaMaterial = r.Int() != 0;
            Almacen.Clear(); int na = r.Int(); for (int i = 0; i < na; i++) Almacen.Add(r.Ints()); MetrosSueltos.Clear(); MetrosSueltos.AddRange(r.Ints() ?? new int[0]); while (MetrosSueltos.Count < Almacen.Count) MetrosSueltos.Add(0);
            Lineas.Clear(); int nl = r.Int();
            for (int i = 0; i < nl; i++) { var l = new Linea { Id = i, Nombre = r.Str() }; int np = r.Int(); for (int k = 0; k < np; k++) l.Paradas.Add(new ParadaLinea { Estacion = r.Int(), Cargar = r.Ints(), Descargar = r.Ints() }); Lineas.Add(l); }
            Trenes.Clear(); _hechoC.Clear(); _hechoD.Clear(); int nt = r.Int();
            for (int i = 0; i < nt; i++)
            {
                var t = new Tren { Id = i, Nombre = r.Str(), Nodo = r.Int(), PasoI = r.Int(), OffMm = r.Int(), VelMmT = r.Int(), VmaxMmT = r.Int(), AccMmT2 = r.Int(), Estado = (EstadoTren)r.Byte(), Linea = r.Int(), Parada = r.Int(), EsperaT = r.Int(), InicioVueltaT = r.Int(), UltimaVueltaT = r.Int() };
                var c = r.Ints(); Array.Copy(c, t.Carga, Math.Min(c.Length, Recursos)); t.Capacidad = r.Int();
                int nr = r.Int(); for (int k = 0; k < nr; k++) t.Ruta.Add(new Via.Paso { Tramo = r.Int(), Directo = r.Bool() });
                var hc = r.Ints(); if (hc != null) _hechoC[i] = hc; var hd = r.Ints(); if (hd != null) _hechoD[i] = hd;
                Trenes.Add(t);
            }
        }
    }
}
