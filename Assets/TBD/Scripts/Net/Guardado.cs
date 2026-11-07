using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace TBD.Net
{
    // Guardado y carga deterministas con versión (GDD §14 y §17.3, regla 8). Lógica pura en C#, sin Unity: se prueba con dotnet.
    // El guardado es la serialización completa del ESTADO de la simulación de red (NetWorld). La configuración (el mapa y los CSV de balance)
    // no se guarda: se vuelve a construir con el escenario, y el guardado lleva dos huellas para saber si ha cambiado:
    //   · huella de la topología (nodos, tramos, puntos de emboscada): si no coincide, la partida no se puede cargar;
    //   · huella de los parámetros de balance: si no coincide, se carga igualmente con el balance nuevo y se avisa (Hugo puede reajustar).
    // Formato: todo en enteros de longitud variable (zigzag), sin coma flotante, sin diccionarios ni orden de recorrido que dependa del hash.
    // Dos guardados del mismo estado son idénticos byte a byte, y guardar lo cargado da el mismo archivo.

    public enum SaveError : byte
    {
        None = 0, Truncated = 1, BadMagic = 2, BadChecksum = 3, TooNew = 4, TooOld = 5, TopologyMismatch = 6, HashMismatch = 7, Corrupt = 8, MissingSection = 9,
    }

    public sealed class SaveException : Exception
    {
        public readonly SaveError Error;
        public SaveException(SaveError e, string msg) : base(msg) { Error = e; }
    }

    /// <summary>Escritor binario determinista: enteros de longitud variable con zigzag, sin coma flotante.</summary>
    public sealed class SaveWriter
    {
        readonly MemoryStream _ms = new MemoryStream();

        public int Length { get { return (int)_ms.Length; } }

        public void Byte(byte v) { _ms.WriteByte(v); }
        public void Bool(bool v) { _ms.WriteByte(v ? (byte)1 : (byte)0); }

        public void Long(long v)
        {
            ulong z = (ulong)((v << 1) ^ (v >> 63));          // zigzag: los negativos pequeños también ocupan poco
            while (z >= 0x80) { _ms.WriteByte((byte)(z | 0x80)); z >>= 7; }
            _ms.WriteByte((byte)z);
        }
        public void Int(int v) { Long(v); }

        /// <summary>64 bits fijos, en little-endian (hashes y semillas).</summary>
        public void ULong(ulong v) { for (int i = 0; i < 8; i++) { _ms.WriteByte((byte)v); v >>= 8; } }
        public void UInt(uint v) { for (int i = 0; i < 4; i++) { _ms.WriteByte((byte)v); v >>= 8; } }
        public void UShort(ushort v) { _ms.WriteByte((byte)v); _ms.WriteByte((byte)(v >> 8)); }

        public void Raw(byte[] b) { _ms.Write(b, 0, b.Length); }

        /// <summary>Texto en UTF-8: 0 = null, n + 1 = n bytes.</summary>
        public void Str(string s)
        {
            if (s == null) { Int(0); return; }
            var b = Encoding.UTF8.GetBytes(s);
            Int(b.Length + 1); _ms.Write(b, 0, b.Length);
        }

        public void Ints(int[] a) { if (a == null) { Int(-1); return; } Int(a.Length); for (int i = 0; i < a.Length; i++) Int(a[i]); }
        public void Longs(long[] a) { if (a == null) { Int(-1); return; } Int(a.Length); for (int i = 0; i < a.Length; i++) Long(a[i]); }
        public void Strs(string[] a) { if (a == null) { Int(-1); return; } Int(a.Length); for (int i = 0; i < a.Length; i++) Str(a[i]); }
        public void Ints2(int[,] a) { Int(a.GetLength(0)); Int(a.GetLength(1)); for (int i = 0; i < a.GetLength(0); i++) for (int j = 0; j < a.GetLength(1); j++) Int(a[i, j]); }

        /// <summary>Booleanos empaquetados de 8 en 8.</summary>
        public void Bools(bool[] a)
        {
            if (a == null) { Int(-1); return; }
            Int(a.Length);
            for (int i = 0; i < a.Length; i += 8) { int b = 0; for (int k = 0; k < 8 && i + k < a.Length; k++) if (a[i + k]) b |= 1 << k; _ms.WriteByte((byte)b); }
        }
        public void Bools2(bool[,] a)
        {
            Int(a.GetLength(0)); Int(a.GetLength(1));
            int n = a.Length, bit = 0, cur = 0;
            for (int i = 0; i < a.GetLength(0); i++) for (int j = 0; j < a.GetLength(1); j++) { if (a[i, j]) cur |= 1 << bit; if (++bit == 8) { _ms.WriteByte((byte)cur); bit = 0; cur = 0; } }
            if (n > 0 && bit != 0) _ms.WriteByte((byte)cur);
        }

        public byte[] ToArray() { return _ms.ToArray(); }
    }

    /// <summary>Lector con comprobación de límites: un archivo recortado o roto da SaveException, nunca un estado a medias sin avisar.</summary>
    public sealed class SaveReader
    {
        readonly byte[] _b;
        int _p;
        readonly int _end;

        public SaveReader(byte[] b, int start = 0, int length = -1) { _b = b; _p = start; _end = length < 0 ? b.Length : start + length; }

        public int Remaining { get { return _end - _p; } }
        public bool AtEnd { get { return _p >= _end; } }

        byte Next() { if (_p >= _end) throw new SaveException(SaveError.Truncated, "el guardado termina antes de lo esperado"); return _b[_p++]; }

        public byte Byte() { return Next(); }
        public bool Bool() { byte v = Next(); if (v > 1) throw new SaveException(SaveError.Corrupt, "booleano no válido"); return v == 1; }

        public long Long()
        {
            ulong z = 0; int shift = 0;
            while (true)
            {
                byte b = Next();
                z |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
                if (shift > 63) throw new SaveException(SaveError.Corrupt, "entero demasiado largo");
            }
            return (long)(z >> 1) ^ -(long)(z & 1);
        }
        public int Int() { long v = Long(); if (v < int.MinValue || v > int.MaxValue) throw new SaveException(SaveError.Corrupt, "entero fuera de rango"); return (int)v; }

        public ulong ULong() { ulong v = 0; for (int i = 0; i < 8; i++) v |= (ulong)Next() << (8 * i); return v; }
        public uint UInt() { uint v = 0; for (int i = 0; i < 4; i++) v |= (uint)Next() << (8 * i); return v; }
        public ushort UShort() { return (ushort)(Next() | (Next() << 8)); }

        public byte[] Raw(int n)
        {
            if (n < 0 || n > Remaining) throw new SaveException(SaveError.Truncated, "bloque más largo que el archivo");
            var r = new byte[n]; Array.Copy(_b, _p, r, 0, n); _p += n; return r;
        }

        public string Str()
        {
            int n = Int();
            if (n == 0) return null;
            n--;
            if (n < 0 || n > Remaining) throw new SaveException(SaveError.Corrupt, "texto más largo que el archivo");
            var s = Encoding.UTF8.GetString(_b, _p, n); _p += n; return s;
        }

        int Len(int minBytesPerItem = 1)
        {
            int n = Int();
            if (n < -1 || (long)n * minBytesPerItem > Remaining) throw new SaveException(SaveError.Corrupt, "longitud de lista no válida");
            return n;
        }

        public int[] Ints() { int n = Len(); if (n < 0) return null; var a = new int[n]; for (int i = 0; i < n; i++) a[i] = Int(); return a; }
        public long[] Longs() { int n = Len(); if (n < 0) return null; var a = new long[n]; for (int i = 0; i < n; i++) a[i] = Long(); return a; }
        public string[] Strs() { int n = Len(); if (n < 0) return null; var a = new string[n]; for (int i = 0; i < n; i++) a[i] = Str(); return a; }

        /// <summary>Lee en una matriz ya creada (las dimensiones tienen que coincidir con las del escenario).</summary>
        public void Ints2Into(int[,] a)
        {
            int n0 = Int(), n1 = Int();
            if (n0 != a.GetLength(0) || n1 != a.GetLength(1)) throw new SaveException(SaveError.TopologyMismatch, "matriz de otra forma: " + n0 + "×" + n1 + " en lugar de " + a.GetLength(0) + "×" + a.GetLength(1));
            for (int i = 0; i < n0; i++) for (int j = 0; j < n1; j++) a[i, j] = Int();
        }

        public bool[] Bools()
        {
            int n = Len(0); if (n < 0) return null;
            if ((n + 7) / 8 > Remaining) throw new SaveException(SaveError.Truncated, "booleanos truncados");
            var a = new bool[n];
            for (int i = 0; i < n; i += 8) { int b = Next(); for (int k = 0; k < 8 && i + k < n; k++) a[i + k] = (b & (1 << k)) != 0; }
            return a;
        }
        public void Bools2Into(bool[,] a)
        {
            int n0 = Int(), n1 = Int();
            if (n0 != a.GetLength(0) || n1 != a.GetLength(1)) throw new SaveException(SaveError.TopologyMismatch, "matriz de booleanos de otra forma");
            int bit = 8, cur = 0;
            for (int i = 0; i < n0; i++) for (int j = 0; j < n1; j++) { if (bit == 8) { cur = Next(); bit = 0; } a[i, j] = (cur & (1 << bit++)) != 0; }
        }

        /// <summary>Lee un entero y exige que coincida con el del escenario (nodos, tramos…).</summary>
        public void Expect(int value, string what)
        {
            int v = Int();
            if (v != value) throw new SaveException(SaveError.TopologyMismatch, what + ": el guardado tiene " + v + " y el escenario " + value);
        }
    }

    /// <summary>Una sección del archivo: la red es «NET»; la zona en detalle la añadirá quien la serialice (las desconocidas se conservan y se saltan).</summary>
    public sealed class SaveSection
    {
        public uint Id;
        public ushort Version;
        public byte[] Data;
        public static uint Tag(string s) { return (uint)(s[0] | (s[1] << 8) | (s[2] << 16) | (s[3] << 24)); }
    }

    public sealed class SaveInfo
    {
        public ushort FormatVersion;          // del contenedor
        public ushort NetVersion;             // de la sección NET tal como estaba en el archivo (antes de migrar)
        public long Tick;
        public ulong Hash;                    // el del estado guardado (se comprueba al cargar)
        public ulong TopologyHash, ParamsHash;
        public bool BalanceChanged;           // los parámetros de balance de ahora no son los de cuando se guardó
        public bool Migrated;                 // se ha aplicado alguna migración de versión
        public readonly List<SaveSection> Sections = new List<SaveSection>();
        public SaveSection Find(string tag) { uint id = SaveSection.Tag(tag); foreach (var s in Sections) if (s.Id == id) return s; return null; }
    }

    public static class NetSave
    {
        public const uint Magic = 0x53444254;           // «TBDS»
        public const ushort FormatVersion = 1;          // contenedor: cabecera, secciones y suma de comprobación
        public const ushort NetVersion = 2;             // sección NET: el estado de NetWorld. 2 = producción de las estaciones (remanentes y pérdidas al final)
        public const ushort MinNetVersion = 1;          // la más antigua que sabe leer (con las migraciones registradas)
        public static readonly uint NetTag = SaveSection.Tag("NET\0");

        /// <summary>
        /// Migraciones de la sección NET: «de la versión v a la v + 1», sobre los bytes de la sección. Al subir NetVersion se registra aquí la
        /// de la anterior; las partidas largas (GDD §14) cargan encadenándolas. Vacío mientras solo exista la versión 1.
        /// </summary>
        public static readonly Dictionary<int, Func<byte[], byte[]>> NetMigrations = new Dictionary<int, Func<byte[], byte[]>>
        {
            { 1, MigrateNet1To2 },
        };

        /// <summary>1 → 2: la producción (Produccion.cs) añade remanentes y pérdidas al final; en una partida vieja valen 0. El número de nodos sale de
        /// la primera tabla por nodo de la sección (la atracción), así que no hace falta el mapa.</summary>
        static byte[] MigrateNet1To2(byte[] v1)
        {
            int nodes = NodesOfV1(v1);
            var w = new SaveWriter(); w.Raw(v1); ProductionState.WriteEmpty(w, nodes);
            return w.ToArray();
        }

        /// <summary>Recorre la cabecera de la sección NET v1 (el orden de NetWorld.SaveState) hasta la atracción por nodo y devuelve su longitud.</summary>
        static int NodesOfV1(byte[] v1)
        {
            var r = new SaveReader(v1);
            r.Long(); r.Int(); r.Bool(); r.Bool(); r.Bool();               // tick, velocidad, pausa, frenado automático, director
            r.Long(); r.Long(); r.Long();                                   // inyectado, perdido, retirado
            r.Int(); r.Int(); r.Int(); r.Long(); r.Int();                   // zona activa
            r.Bool(); r.Int(); r.Int(); r.Int(); r.Int(); r.Long();         // emboscada en curso
            int n = r.Int(); for (int i = 0; i < n; i++) r.Int();           // emboscadas pendientes
            r.Int();                                                        // secuencia de órdenes
            n = r.Int(); for (int i = 0; i < n; i++) { r.Long(); r.Byte(); r.Int(); r.Int(); r.Int(); r.Ints(); }
            var att = r.Ints();
            if (att == null) throw new SaveException(SaveError.Corrupt, "migración 1 → 2: no se encuentra la atracción por nodo");
            return att.Length;
        }

        // ---------------------------------------------------------------- guardar
        /// <summary>Guarda el estado de la red. «extra» son secciones adicionales (la zona en detalle, de quien la serialice).</summary>
        public static byte[] Save(NetWorld w, IList<SaveSection> extra = null)
        {
            var net = new SaveWriter();
            w.SaveState(net);

            var o = new SaveWriter();
            o.UInt(Magic); o.UShort(FormatVersion); o.UShort(0);
            o.ULong(TopologyHash(w.Graph)); o.ULong(ParamsHash(w));
            o.ULong((ulong)w.Tick); o.ULong(w.Hash());
            int n = 1 + (extra == null ? 0 : extra.Count);
            o.UShort((ushort)n);
            WriteSection(o, new SaveSection { Id = NetTag, Version = NetVersion, Data = net.ToArray() });
            if (extra != null) foreach (var s in extra) WriteSection(o, s);
            var body = o.ToArray();
            var f = new SaveWriter(); f.Raw(body); f.ULong(Checksum(body, body.Length));
            return f.ToArray();
        }

        static void WriteSection(SaveWriter o, SaveSection s)
        {
            o.UInt(s.Id); o.UShort(s.Version); o.UInt((uint)s.Data.Length); o.Raw(s.Data);
        }

        // ---------------------------------------------------------------- leer
        /// <summary>Lee la cabecera y las secciones sin tocar ningún mundo (para el menú de partidas). Comprueba el formato y la suma.</summary>
        public static SaveInfo Peek(byte[] data)
        {
            if (data == null || data.Length < 4 + 2 + 2 + 8 * 4 + 2 + 8) throw new SaveException(SaveError.Truncated, "archivo demasiado corto");
            var body = data.Length - 8;
            var rc = new SaveReader(data, body, 8);
            if (rc.ULong() != Checksum(data, body)) throw new SaveException(SaveError.BadChecksum, "la suma de comprobación no coincide: archivo dañado");
            var r = new SaveReader(data, 0, body);
            if (r.UInt() != Magic) throw new SaveException(SaveError.BadMagic, "no es un guardado de TBD");
            var info = new SaveInfo();
            info.FormatVersion = r.UShort(); r.UShort();
            if (info.FormatVersion > FormatVersion) throw new SaveException(SaveError.TooNew, "guardado de un formato más nuevo (" + info.FormatVersion + "): actualiza el juego");
            if (info.FormatVersion < 1) throw new SaveException(SaveError.TooOld, "formato de guardado no válido");
            info.TopologyHash = r.ULong(); info.ParamsHash = r.ULong(); info.Tick = (long)r.ULong(); info.Hash = r.ULong();
            int n = r.UShort();
            for (int i = 0; i < n; i++)
            {
                var s = new SaveSection { Id = r.UInt(), Version = r.UShort() };
                uint len = r.UInt();
                if (len > (uint)r.Remaining) throw new SaveException(SaveError.Truncated, "sección más larga que el archivo");
                s.Data = r.Raw((int)len);
                info.Sections.Add(s);
            }
            if (!r.AtEnd) throw new SaveException(SaveError.Corrupt, "bytes de más tras las secciones");
            var net = info.Find("NET\0");
            if (net == null) throw new SaveException(SaveError.MissingSection, "falta la sección de la red");
            info.NetVersion = net.Version;
            return info;
        }

        /// <summary>
        /// Carga un guardado en un mundo recién creado con el MISMO mapa (por ejemplo NetScenario.Mvp(false)): se pisa todo el estado. Devuelve la
        /// información del archivo. Si falla, el mundo queda a medias: hay que descartarlo y crear otro.
        /// </summary>
        public static SaveInfo Load(byte[] data, NetWorld w)
        {
            var info = Peek(data);
            if (info.TopologyHash != TopologyHash(w.Graph)) throw new SaveException(SaveError.TopologyMismatch, "el mapa del guardado no es el de este escenario");
            info.BalanceChanged = info.ParamsHash != ParamsHash(w);
            var sec = info.Find("NET\0");
            int v = sec.Version;
            if (v > NetVersion) throw new SaveException(SaveError.TooNew, "la red se guardó con la versión " + v + " y este juego solo sabe hasta la " + NetVersion);
            var bytes = sec.Data;
            while (v < NetVersion)
            {
                Func<byte[], byte[]> mig;
                if (!NetMigrations.TryGetValue(v, out mig)) throw new SaveException(SaveError.TooOld, "no hay migración de la versión " + v + " de la red a la " + (v + 1));
                bytes = mig(bytes); v++; info.Migrated = true;
            }
            var r = new SaveReader(bytes);
            w.LoadState(r);
            if (!r.AtEnd) throw new SaveException(SaveError.Corrupt, "bytes de más en la sección de la red");
            if (w.Hash() != info.Hash) throw new SaveException(SaveError.HashMismatch, "tras cargar, el hash del estado no es el que se guardó (tick " + info.Tick + ")");
            return info;
        }

        /// <summary>Atajo para el escenario del MVP.</summary>
        public static NetWorld LoadMvp(byte[] data, out SaveInfo info)
        {
            var w = NetScenario.Mvp(false);
            info = Load(data, w);
            return w;
        }

        // ---------------------------------------------------------------- huellas
        public static ulong Checksum(byte[] b, int len)
        {
            ulong h = 0xCBF29CE484222325UL;                // FNV-1a de 64 bits
            for (int i = 0; i < len; i++) { h ^= b[i]; h *= 0x100000001B3UL; }
            return h;
        }

        static ulong Fold(ulong h, ulong v) { return NetWorld.Mix(h ^ NetWorld.Mix(v + 0x9E3779B97F4A7C15UL)); }
        static ulong FoldStr(ulong h, string s) { h = Fold(h, (ulong)(s == null ? -1 : s.Length)); if (s != null) foreach (char c in s) h = Fold(h, c); return h; }

        /// <summary>Huella de la topología: nodos (nombre y posición), tramos (extremos, largo, km) y puntos de emboscada. Lo que cambia con el mapa, no con la partida.</summary>
        public static ulong TopologyHash(RailGraph g)
        {
            ulong h = 0x70B0;
            h = Fold(h, (ulong)g.Nodes.Count); h = Fold(h, (ulong)g.Edges.Count);
            foreach (var n in g.Nodes) { h = FoldStr(h, n.Name); h = Fold(h, (ulong)n.XMeters); h = Fold(h, (ulong)n.YMeters); }
            foreach (var e in g.Edges)
            {
                h = Fold(h, (ulong)e.A); h = Fold(h, (ulong)e.B); h = Fold(h, (ulong)e.LengthMm); h = Fold(h, (ulong)e.KmStartMm); h = Fold(h, (ulong)e.HordeSpeedMmPerTick);
                h = Fold(h, (ulong)e.Ambush.Count);
                foreach (var a in e.Ambush) { h = Fold(h, (ulong)a.KmMm); h = Fold(h, (ulong)a.Kind); h = FoldStr(h, a.Name); }
            }
            return h;
        }

        /// <summary>
        /// Huella de los parámetros de balance (los CSV de Hugo, ya cargados en el mundo): recorre los campos públicos de cada estructura de parámetros
        /// por nombre, así que no depende del orden en que se declaren. Cambia cuando Hugo reajusta el balance. «Stage2» y los datos de la partida no entran.
        /// </summary>
        public static ulong ParamsHash(NetWorld w)
        {
            ulong h = 0xBA1A;
            h = FoldObject(h, w.Works.P); h = FoldObject(h, w.Stations.P); h = FoldObject(h, w.Taller.P, "Stage2"); h = FoldObject(h, w.Production.P);
            if (w.Traffic != null) h = FoldObject(h, w.Traffic.P);
            h = FoldObject(h, w.Director.P);
            h = Fold(h, 0xC); foreach (var v in w.Director.Curve ?? new long[0]) h = Fold(h, (ulong)v);
            h = Fold(h, 0x5); foreach (var v in w.Director.Season ?? new long[0]) h = Fold(h, (ulong)v);
            h = Fold(h, 0x4); foreach (var v in w.Director.Noise ?? new long[0]) h = Fold(h, (ulong)v);
            return h;
        }

        static ulong FoldObject(ulong h, object o, params string[] skip)
        {
            var fields = o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (var f in fields)
            {
                if (Array.IndexOf(skip, f.Name) >= 0) continue;
                h = FoldStr(h, f.Name);
                object v = f.GetValue(o);
                if (v == null) h = Fold(h, 0xFFFF);
                else if (v is string) h = FoldStr(h, (string)v);
                else if (v is string[]) { foreach (var s in (string[])v) h = FoldStr(h, s); }
                else if (v is bool) h = Fold(h, (bool)v ? 1UL : 0UL);
                else if (v is int) h = Fold(h, (ulong)(long)(int)v);
                else if (v is long) h = Fold(h, (ulong)(long)v);
                else if (v is byte) h = Fold(h, (byte)v);
                else if (v is ulong) h = Fold(h, (ulong)v);
                else if (v is int[]) { foreach (var x in (int[])v) h = Fold(h, (ulong)(long)x); }
                else if (v is bool[]) { foreach (var x in (bool[])v) h = Fold(h, x ? 1UL : 0UL); }
                else h = Fold(h, Convert.ToUInt64(v));
            }
            return h;
        }
    }
}
