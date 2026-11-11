using System;
using System.Collections.Generic;
using TBD.Units;

namespace TBD.Mundo
{
    /// <summary>
    /// Rejilla de paso del mundo continuo (núcleo v2, Hito 1; Nacho). El mundo es grande (2 × 2 km en el Hito 1, 8 × 8 km en el corte), así que una
    /// rejilla entera de 1 m no cabe (64 millones de celdas a 8 km). Va por TROZOS de «TrozoCeldas» × «TrozoCeldas» celdas que se montan la primera vez que
    /// alguien pregunta por ellos, con los obstáculos (rectángulos en mm) que los tocan, inflados con «InflarMm».
    /// El A* busca solo dentro de una caja alrededor de la salida y la meta (con «MargenMm» de holgura), así que su coste no depende del tamaño del mundo.
    /// Todo en enteros y con desempates por índice: el mismo camino en todas las máquinas (cooperativo).
    /// </summary>
    public sealed class RejillaTrozos
    {
        public readonly int AnchoMm, AltoMm, CeldaMm, TrozoCeldas, InflarMm;
        public readonly int CeldasX, CeldasY, TrozosX, TrozosY;
        readonly byte[][] _trozos;                  // null = sin montar; 1 = celda bloqueada
        readonly List<RectMm> _obs = new List<RectMm>();
        readonly List<int>[] _obsPorTrozo;          // índices de _obs que tocan cada trozo (con el inflado)
        public int TrozosMontados { get; private set; }
        public int Version { get; private set; }
        public int MaxExpandir = 200000;
        public int UltimosExpandidos;

        public RejillaTrozos(int anchoMm, int altoMm, int inflarMm, int celdaMm = 1000, int trozoCeldas = 64)
        {
            AnchoMm = Math.Max(1, anchoMm); AltoMm = Math.Max(1, altoMm); CeldaMm = Math.Max(100, celdaMm); TrozoCeldas = Math.Max(8, trozoCeldas); InflarMm = Math.Max(0, inflarMm);
            CeldasX = (AnchoMm + CeldaMm - 1) / CeldaMm; CeldasY = (AltoMm + CeldaMm - 1) / CeldaMm;
            TrozosX = (CeldasX + TrozoCeldas - 1) / TrozoCeldas; TrozosY = (CeldasY + TrozoCeldas - 1) / TrozoCeldas;
            _trozos = new byte[TrozosX * TrozosY][]; _obsPorTrozo = new List<int>[TrozosX * TrozosY];
        }

        /// <summary>Cambia los obstáculos (casas, estaciones, muros). Los trozos montados se tiran y se rehacen al pedirlos.</summary>
        public void PonerObstaculos(IList<RectMm> obstaculos)
        {
            _obs.Clear(); if (obstaculos != null) _obs.AddRange(obstaculos);
            for (int i = 0; i < _trozos.Length; i++) { _trozos[i] = null; _obsPorTrozo[i] = null; }
            TrozosMontados = 0; Version++;
            int lado = TrozoCeldas * CeldaMm;
            for (int k = 0; k < _obs.Count; k++)
            {
                var r = _obs[k];
                int tx0 = Math.Max(0, FloorDiv(r.X0 - InflarMm - CeldaMm, lado)), tx1 = Math.Min(TrozosX - 1, FloorDiv(r.X1 + InflarMm + CeldaMm, lado));
                int ty0 = Math.Max(0, FloorDiv(r.Y0 - InflarMm - CeldaMm, lado)), ty1 = Math.Min(TrozosY - 1, FloorDiv(r.Y1 + InflarMm + CeldaMm, lado));
                for (int ty = ty0; ty <= ty1; ty++) for (int tx = tx0; tx <= tx1; tx++) { int t = ty * TrozosX + tx; (_obsPorTrozo[t] ?? (_obsPorTrozo[t] = new List<int>())).Add(k); }
            }
        }

        public IList<RectMm> Obstaculos { get { return _obs; } }

        byte[] Trozo(int t)
        {
            var b = _trozos[t]; if (b != null) return b;
            b = new byte[TrozoCeldas * TrozoCeldas]; _trozos[t] = b; TrozosMontados++;
            var lista = _obsPorTrozo[t]; if (lista == null) return b;
            int cx0 = (t % TrozosX) * TrozoCeldas, cy0 = (t / TrozosX) * TrozoCeldas;
            foreach (int k in lista)
            {
                var r = _obs[k];      // una celda se bloquea si su centro cae dentro del rectángulo inflado (como NavGrid)
                int ax = CeilDiv(r.X0 - InflarMm - CeldaMm / 2, CeldaMm), bx = FloorDiv(r.X1 + InflarMm - CeldaMm / 2, CeldaMm);
                int ay = CeilDiv(r.Y0 - InflarMm - CeldaMm / 2, CeldaMm), by = FloorDiv(r.Y1 + InflarMm - CeldaMm / 2, CeldaMm);
                ax = Math.Max(ax, cx0); ay = Math.Max(ay, cy0); bx = Math.Min(bx, cx0 + TrozoCeldas - 1); by = Math.Min(by, cy0 + TrozoCeldas - 1);
                for (int y = ay; y <= by; y++) for (int x = ax; x <= bx; x++) b[(y - cy0) * TrozoCeldas + (x - cx0)] = 1;
            }
            return b;
        }

        static int FloorDiv(int a, int b) { return a >= 0 ? a / b : -((-a + b - 1) / b); }
        static int CeilDiv(int a, int b) { return -FloorDiv(-a, b); }

        public bool EnMundo(int cx, int cy) { return cx >= 0 && cy >= 0 && cx < CeldasX && cy < CeldasY; }
        public int CeldaX(int xMm) { return IMath.Clamp(FloorDiv(xMm, CeldaMm), 0, CeldasX - 1); }
        public int CeldaY(int yMm) { return IMath.Clamp(FloorDiv(yMm, CeldaMm), 0, CeldasY - 1); }
        public int CentroX(int cx) { return cx * CeldaMm + CeldaMm / 2; }
        public int CentroY(int cy) { return cy * CeldaMm + CeldaMm / 2; }

        /// <summary>¿La celda (cx, cy) está libre? Fuera del mundo, no.</summary>
        public bool LibreCelda(int cx, int cy)
        {
            if (!EnMundo(cx, cy)) return false;
            int tx = cx / TrozoCeldas, ty = cy / TrozoCeldas, t = ty * TrozosX + tx;
            if (_obsPorTrozo[t] == null) return true;                      // trozo sin obstáculos: no hace falta montarlo
            return Trozo(t)[(cy - ty * TrozoCeldas) * TrozoCeldas + (cx - tx * TrozoCeldas)] == 0;
        }
        public bool Libre(int xMm, int yMm) { if (xMm < 0 || yMm < 0) return false; return LibreCelda(xMm / CeldaMm, yMm / CeldaMm); }

        /// <summary>¿El tramo recto de a a b va todo por celdas libres? Muestra cada cuarto de celda (conservador).</summary>
        public bool LineaLibre(int ax, int ay, int bx, int by)
        {
            int d = IMath.Dist(bx - ax, by - ay), paso = Math.Max(1, CeldaMm / 4), n = d / paso + 1;
            for (int k = 0; k <= n; k++) { int x = ax + (int)((long)(bx - ax) * k / n), y = ay + (int)((long)(by - ay) * k / n); if (!Libre(x, y)) return false; }
            return true;
        }

        // ---- A* en una caja local
        int _bx0, _by0, _bw, _bh; int[] _g = new int[0], _de = new int[0], _vista = new int[0], _cerrada = new int[0]; int _busqueda;
        readonly List<long> _monticulo = new List<long>(); readonly List<int> _celdas = new List<int>();
        void Meter(int f, int h, int i) { long k = ((long)f << 42) | ((long)Math.Min(h, (1 << 20) - 1) << 22) | (uint)i; _monticulo.Add(k); int c = _monticulo.Count - 1; while (c > 0) { int p = (c - 1) / 2; if (_monticulo[p] <= _monticulo[c]) break; var t = _monticulo[p]; _monticulo[p] = _monticulo[c]; _monticulo[c] = t; c = p; } }
        long Sacar() { long top = _monticulo[0]; int ult = _monticulo.Count - 1; _monticulo[0] = _monticulo[ult]; _monticulo.RemoveAt(ult); int c = 0; while (true) { int l = 2 * c + 1, r = l + 1, m = c; if (l < _monticulo.Count && _monticulo[l] < _monticulo[m]) m = l; if (r < _monticulo.Count && _monticulo[r] < _monticulo[m]) m = r; if (m == c) break; var t = _monticulo[m]; _monticulo[m] = _monticulo[c]; _monticulo[c] = t; c = m; } return top; }
        static int Octil(int dx, int dy) { dx = Math.Abs(dx); dy = Math.Abs(dy); return 10 * Math.Max(dx, dy) + 4 * Math.Min(dx, dy); }
        bool LibreLocal(int i) { return LibreCelda(_bx0 + i % _bw, _by0 + i / _bw); }

        int LibreMasCercana(int cx, int cy, int maxR)
        {
            if (LibreCelda(cx, cy)) return (cy - _by0) * _bw + (cx - _bx0);
            for (int r = 1; r <= maxR; r++)
            {
                int mejor = -1, md = int.MaxValue;
                for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                    int x = cx + dx, y = cy + dy; if (x < _bx0 || y < _by0 || x >= _bx0 + _bw || y >= _by0 + _bh || !LibreCelda(x, y)) continue;
                    int d = dx * dx + dy * dy; if (d < md) { md = d; mejor = (y - _by0) * _bw + (x - _bx0); }
                }
                if (mejor >= 0) return mejor;
            }
            return -1;
        }

        /// <summary>Camino de (sx, sy) a (tx, ty) en mm por celdas libres, buscando dentro de la caja de las dos puntas con «margenMm» de holgura.
        /// Rellena «camino» con los puntos de paso alisados (el último es el destino, o la celda libre más cercana). false si no hay camino en la caja.</summary>
        public bool BuscarCamino(int sx, int sy, int tx, int ty, List<PointMm> camino, int margenMm = 64000)
        {
            camino.Clear(); UltimosExpandidos = 0;
            int m = Math.Max(1, margenMm / CeldaMm);
            int scx = CeldaX(sx), scy = CeldaY(sy), tcx0 = CeldaX(tx), tcy0 = CeldaY(ty);
            _bx0 = Math.Max(0, Math.Min(scx, tcx0) - m); _by0 = Math.Max(0, Math.Min(scy, tcy0) - m);
            int bx1 = Math.Min(CeldasX - 1, Math.Max(scx, tcx0) + m), by1 = Math.Min(CeldasY - 1, Math.Max(scy, tcy0) + m);
            _bw = bx1 - _bx0 + 1; _bh = by1 - _by0 + 1; int n = _bw * _bh;
            if (_g.Length < n) { _g = new int[n]; _de = new int[n]; _vista = new int[n]; _cerrada = new int[n]; _busqueda = 0; }
            _busqueda++; if (_busqueda == int.MaxValue) { Array.Clear(_vista, 0, _vista.Length); Array.Clear(_cerrada, 0, _cerrada.Length); _busqueda = 1; }
            // al cambiar el tamaño de la caja, los índices viejos no valen: se limpian los sellos de la parte usada
            if (_busqueda == 1) { Array.Clear(_vista, 0, n); Array.Clear(_cerrada, 0, n); }
            int s = LibreMasCercana(scx, scy, 8), t = LibreMasCercana(tcx0, tcy0, 30);
            if (s < 0 || t < 0) return false;
            bool metaExacta = t == (tcy0 - _by0) * _bw + (tcx0 - _bx0) && Libre(tx, ty);
            if (s == t) { camino.Add(metaExacta ? new PointMm(tx, ty) : new PointMm(CentroX(_bx0 + t % _bw), CentroY(_by0 + t / _bw))); return true; }
            _monticulo.Clear();
            int tcx = t % _bw, tcy = t / _bw;
            _vista[s] = _busqueda; _g[s] = 0; _de[s] = -1; int h0 = Octil(s % _bw - tcx, s / _bw - tcy); Meter(h0, h0, s);
            bool hallado = false;
            while (_monticulo.Count > 0)
            {
                int i = (int)(Sacar() & 0x3FFFFF);
                if (_cerrada[i] == _busqueda) continue;
                _cerrada[i] = _busqueda;
                if (i == t) { hallado = true; break; }
                if (++UltimosExpandidos > MaxExpandir) break;
                int cx = i % _bw, cy = i / _bw;
                for (int k = 0; k < 8; k++)
                {
                    int dx = k == 0 ? 1 : k == 1 ? -1 : k == 2 ? 0 : k == 3 ? 0 : k == 4 ? 1 : k == 5 ? 1 : k == 6 ? -1 : -1;
                    int dy = k == 0 ? 0 : k == 1 ? 0 : k == 2 ? 1 : k == 3 ? -1 : k == 4 ? 1 : k == 5 ? -1 : k == 6 ? 1 : -1;
                    int x = cx + dx, y = cy + dy; if (x < 0 || y < 0 || x >= _bw || y >= _bh) continue;
                    int j = y * _bw + x; if (!LibreLocal(j) || _cerrada[j] == _busqueda) continue;
                    if (dx != 0 && dy != 0 && (!LibreLocal(cy * _bw + x) || !LibreLocal(y * _bw + cx))) continue;      // sin cortar esquinas
                    int g = _g[i] + (dx != 0 && dy != 0 ? 14 : 10);
                    if (_vista[j] == _busqueda && g >= _g[j]) continue;
                    _vista[j] = _busqueda; _g[j] = g; _de[j] = i;
                    int h = Octil(x - tcx, y - tcy); Meter(g + h, h, j);
                }
            }
            if (!hallado) return false;
            _celdas.Clear(); for (int c = t; c >= 0; c = _de[c]) _celdas.Add(c); _celdas.Reverse();
            int a = 0, ax = sx, ay = sy; if (!Libre(sx, sy)) { ax = CentroX(_bx0 + s % _bw); ay = CentroY(_by0 + s / _bw); camino.Add(new PointMm(ax, ay)); }
            while (a < _celdas.Count - 1)
            {
                int b = _celdas.Count - 1;
                while (b > a + 1 && !LineaLibre(ax, ay, CentroX(_bx0 + _celdas[b] % _bw), CentroY(_by0 + _celdas[b] / _bw))) b--;
                a = b; ax = CentroX(_bx0 + _celdas[a] % _bw); ay = CentroY(_by0 + _celdas[a] / _bw);
                camino.Add(new PointMm(ax, ay));
            }
            if (metaExacta)
            {
                int px = camino.Count > 1 ? camino[camino.Count - 2].X : sx, py = camino.Count > 1 ? camino[camino.Count - 2].Y : sy;
                if (camino.Count > 0 && LineaLibre(px, py, tx, ty)) camino[camino.Count - 1] = new PointMm(tx, ty); else camino.Add(new PointMm(tx, ty));
            }
            return true;
        }
    }
}
