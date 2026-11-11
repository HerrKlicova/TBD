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
        public int VelMmT, VmaxMmT = 600, AccMmT2 = 1;      // 600 mm/tick = 18 m/s (65 km/h); 1 mm/tick² ≈ 0,9 m/s²
        public EstadoTren Estado;
        public int Linea = -1, Parada, EsperaT;
        public readonly int[] Carga = new int[Red.Recursos]; public int Capacidad = 40;
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
        public const int Recursos = 4;                     // munición, combustible, materiales, víveres (provisional: lo cierra la economía de Mundo)
        public const int TicksPorU = 6;                    // carga o descarga: 1 u cada 0,2 s
        public const int EsperaMinT = 90;                  // al menos 3 s parado en cada parada
        public readonly Via Via = new Via();
        public readonly List<Tren> Trenes = new List<Tren>();
        public readonly List<Linea> Lineas = new List<Linea>();
        public readonly List<int[]> Almacen = new List<int[]>();     // por estación
        public int Tick;
        /// <summary>Último rechazo de una orden (para la vista); no entra en el hash.</summary>
        public string UltimoRechazo;

        public int AddEstacion(string nombre, int x, int y) { int e = Via.AddEstacion(nombre, x, y); Almacen.Add(new int[Recursos]); return e; }
        public int NuevoTren(string nombre, int estacion) { var t = new Tren { Id = Trenes.Count, Nombre = nombre, Nodo = Via.Estaciones[estacion].Nodo, Estado = EstadoTren.Parado }; Trenes.Add(t); return t.Id; }

        public Via.Resultado TrazarVia(IList<PointMm> puntos) { var r = Via.Trazar(puntos); if (!r.Ok) UltimoRechazo = r.Por; return r; }

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

        public void Step(int tick)
        {
            Tick = tick;
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
            }
        }

        /// <summary>En la parada: primero descarga y luego carga, 1 u cada TicksPorU; cuando acaba (y lleva al menos EsperaMinT), sale hacia la siguiente parada.</summary>
        void Atender(Tren t)
        {
            t.EsperaT++;
            var l = Lineas[t.Linea]; var p = l.Paradas[t.Parada]; int est = p.Estacion; var alm = Almacen[est];
            bool trabajo = false;
            if ((t.EsperaT % TicksPorU) == 0)
            {
                for (int r = 0; r < Recursos && !trabajo; r++) if (Pide(p.Descargar, r) && t.Carga[r] > 0 && (p.Descargar[r] < 0 || Hecho(t, r, false) < p.Descargar[r])) { t.Carga[r]--; alm[r]++; Marcar(t, r, false); trabajo = true; }
                for (int r = 0; r < Recursos && !trabajo; r++) if (Pide(p.Cargar, r) && alm[r] > 0 && CargaTotal(t) < t.Capacidad && (p.Cargar[r] < 0 || Hecho(t, r, true) < p.Cargar[r])) { alm[r]--; t.Carga[r]++; Marcar(t, r, true); trabajo = true; }
                if (!trabajo && t.EsperaT >= EsperaMinT) { LimpiarHechos(t); t.Parada = (t.Parada + 1) % l.Paradas.Count; IrAParada(t); }
            }
        }

        // lo cargado/descargado en esta parada (por tren y recurso), para no pasar de lo pedido
        readonly Dictionary<int, int[]> _hechoC = new Dictionary<int, int[]>(), _hechoD = new Dictionary<int, int[]>();
        int Hecho(Tren t, int r, bool cargar) { var d = cargar ? _hechoC : _hechoD; return d.TryGetValue(t.Id, out var a) ? a[r] : 0; }
        void Marcar(Tren t, int r, bool cargar) { var d = cargar ? _hechoC : _hechoD; if (!d.TryGetValue(t.Id, out var a)) d[t.Id] = a = new int[Recursos]; a[r]++; }
        void LimpiarHechos(Tren t) { _hechoC.Remove(t.Id); _hechoD.Remove(t.Id); }
        static bool Pide(int[] a, int r) { return a != null && r < a.Length && a[r] != 0; }
        static int CargaTotal(Tren t) { int s = 0; foreach (var c in t.Carga) s += c; return s; }

        /// <summary>Posición del tren (mm) y rumbo (bam16) para la vista. Parado: la del nodo.</summary>
        public PointMm Posicion(Tren t, out int rumbo)
        {
            rumbo = 0;
            if (t.Estado != EstadoTren.EnMarcha || t.Ruta.Count == 0) { var n = Via.Nodos[Math.Max(0, t.Nodo)]; return new PointMm(n.X, n.Y); }
            var ps = t.Ruta[t.PasoI]; var tr = Via.Tramos[ps.Tramo];
            var p = Via.Posicion(ps.Tramo, ps.Directo ? t.OffMm : tr.LongMm - t.OffMm, out rumbo); if (!ps.Directo) rumbo = (rumbo + IMath.Turn / 2) & 0xFFFF;
            return p;
        }

        public ulong Hash()
        {
            ulong h = Via.Hash() ^ ((ulong)(uint)Tick << 7);
            foreach (var t in Trenes)
            {
                h = IMath.Mix(h ^ (ulong)t.Id ^ ((ulong)(uint)t.Nodo << 8) ^ ((ulong)t.PasoI << 24) ^ ((ulong)(uint)t.OffMm << 32));
                h = IMath.Mix(h ^ (ulong)(uint)t.VelMmT ^ ((ulong)t.Estado << 20) ^ ((ulong)(uint)(t.Linea + 1) << 24) ^ ((ulong)(uint)t.Parada << 40) ^ ((ulong)(uint)t.EsperaT << 48));
                foreach (var c in t.Carga) h = IMath.Mix(h ^ (ulong)(uint)c);
                foreach (var ps in t.Ruta) h = IMath.Mix(h ^ (ulong)ps.Tramo ^ (ps.Directo ? 1UL << 40 : 0UL));
            }
            foreach (var a in Almacen) foreach (var c in a) h = IMath.Mix(h ^ (ulong)(uint)c);
            foreach (var kv in _hechoC) foreach (var c in kv.Value) h = IMath.Mix(h ^ (ulong)kv.Key ^ (ulong)(uint)c << 16);
            foreach (var kv in _hechoD) foreach (var c in kv.Value) h = IMath.Mix(h ^ (ulong)kv.Key ^ (ulong)(uint)c << 24);
            return h;
        }

        public void Save(SaveWriter w)
        {
            Via.Save(w); w.Int(Tick);
            w.Int(Almacen.Count); foreach (var a in Almacen) w.Ints(a);
            w.Int(Lineas.Count); foreach (var l in Lineas) { w.Str(l.Nombre); w.Int(l.Paradas.Count); foreach (var p in l.Paradas) { w.Int(p.Estacion); w.Ints(p.Cargar); w.Ints(p.Descargar); } }
            w.Int(Trenes.Count);
            foreach (var t in Trenes)
            {
                w.Str(t.Nombre); w.Int(t.Nodo); w.Int(t.PasoI); w.Int(t.OffMm); w.Int(t.VelMmT); w.Int(t.VmaxMmT); w.Int(t.AccMmT2); w.Byte((byte)t.Estado);
                w.Int(t.Linea); w.Int(t.Parada); w.Int(t.EsperaT); w.Ints(t.Carga); w.Int(t.Capacidad);
                w.Int(t.Ruta.Count); foreach (var ps in t.Ruta) { w.Int(ps.Tramo); w.Bool(ps.Directo); }
                w.Ints(_hechoC.TryGetValue(t.Id, out var hc) ? hc : null); w.Ints(_hechoD.TryGetValue(t.Id, out var hd) ? hd : null);
            }
        }

        public void Load(SaveReader r)
        {
            Via.Load(r); Tick = r.Int();
            Almacen.Clear(); int na = r.Int(); for (int i = 0; i < na; i++) Almacen.Add(r.Ints());
            Lineas.Clear(); int nl = r.Int();
            for (int i = 0; i < nl; i++) { var l = new Linea { Id = i, Nombre = r.Str() }; int np = r.Int(); for (int k = 0; k < np; k++) l.Paradas.Add(new ParadaLinea { Estacion = r.Int(), Cargar = r.Ints(), Descargar = r.Ints() }); Lineas.Add(l); }
            Trenes.Clear(); _hechoC.Clear(); _hechoD.Clear(); int nt = r.Int();
            for (int i = 0; i < nt; i++)
            {
                var t = new Tren { Id = i, Nombre = r.Str(), Nodo = r.Int(), PasoI = r.Int(), OffMm = r.Int(), VelMmT = r.Int(), VmaxMmT = r.Int(), AccMmT2 = r.Int(), Estado = (EstadoTren)r.Byte(), Linea = r.Int(), Parada = r.Int(), EsperaT = r.Int() };
                var c = r.Ints(); Array.Copy(c, t.Carga, Math.Min(c.Length, Recursos)); t.Capacidad = r.Int();
                int nr = r.Int(); for (int k = 0; k < nr; k++) t.Ruta.Add(new Via.Paso { Tramo = r.Int(), Directo = r.Bool() });
                var hc = r.Ints(); if (hc != null) _hechoC[i] = hc; var hd = r.Ints(); if (hd != null) _hechoD[i] = hd;
                Trenes.Add(t);
            }
        }
    }
}
