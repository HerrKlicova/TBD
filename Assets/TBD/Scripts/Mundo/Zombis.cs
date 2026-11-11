using System;
using System.Collections.Generic;
using TBD.Units;

namespace TBD.Mundo
{
    public enum EstadoZombi : byte { Quieto = 0, Deambula = 1, Persigue = 2, Vuelve = 3, Ruido = 4 }

    /// <summary>Un zombi individual del mundo (nivel de detalle cercano, D-N207). Posición en mm, rumbo en 1/65536 de vuelta.</summary>
    public struct ZombiMundo
    {
        public int Id, X, Y, Rumbo;
        public int CasaX, CasaY;              // su sitio en el caserío
        public int MetaX, MetaY;              // a dónde va ahora
        public EstadoZombi Estado;
        public bool Vivo;
        public int Espera;                    // ticks que le quedan quieto antes de volver a deambular
        public int Objetivo;                  // Id del soldado al que persigue (−1 ninguno)
        public bool Andando;                  // se ha movido este tick (para la animación)
    }

    /// <summary>Reglas de los zombis del caserío (Lucía, 11-11, Hito 1). Distancias en mm y velocidades en mm/s.</summary>
    public struct ParamsZombis
    {
        public int CasaRadioMm, VeMm, PierdeMm, OyeMm, PasoDeambularMmS, PasoCorrerMmS, CuerpoMm, ParaMm, EsperaMinTicks, EsperaMaxTicks, PercibeCadaTicks, ReplanCadaTicks;
        public static ParamsZombis PorDefecto()
        {
            return new ParamsZombis
            {
                CasaRadioMm = 15000, VeMm = 25000, PierdeMm = 60000, OyeMm = 80000,
                PasoDeambularMmS = 500, PasoCorrerMmS = 1500,             // a la carrera, la misma velocidad que la horda de NightSim (ZombieSpeedMmS)
                CuerpoMm = 300, ParaMm = 700,                              // se para a 0,7 m del soldado; el contacto (UnitsWorld.ZombieContact) llega a 0,9 m
                EsperaMinTicks = 3 * IMath.TickRate, EsperaMaxTicks = 10 * IMath.TickRate,
                PercibeCadaTicks = 10, ReplanCadaTicks = 30,
            };
        }
        public ulong Hash()
        {
            return IMath.Mix((ulong)(uint)CasaRadioMm ^ ((ulong)(uint)VeMm << 32)) + IMath.Mix((ulong)(uint)PierdeMm ^ ((ulong)(uint)OyeMm << 32))
                + IMath.Mix((ulong)(uint)PasoDeambularMmS ^ ((ulong)(uint)PasoCorrerMmS << 32)) + IMath.Mix((ulong)(uint)CuerpoMm ^ ((ulong)(uint)ParaMm << 32))
                + IMath.Mix((ulong)(uint)EsperaMinTicks ^ ((ulong)(uint)EsperaMaxTicks << 32)) + IMath.Mix((ulong)(uint)PercibeCadaTicks ^ ((ulong)(uint)ReplanCadaTicks << 32));
        }
    }

    /// <summary>
    /// Los zombis individuales del mundo (núcleo v2, Hito 1; Nacho). Reglas de Lucía (11-11):
    /// quietos o deambulando a menos de 15 m de su casa; ven al soldado más cercano a 25 m con línea de visión y lo persiguen; lo pierden a 60 m y vuelven
    /// a casa; un disparo a 80 m o menos los lleva hacia el ruido (sin línea de visión: se oye a través de las casas).
    /// No atraviesan obstáculos: se mueven por la RejillaTrozos (recto si la línea está libre; si no, por camino A*), y si el paso siguiente cae en una celda
    /// bloqueada prueban a deslizarse por un eje o se quedan. Separación entre ellos por cubos de 1 m. Todo en enteros, recorriendo por Id: determinista.
    /// </summary>
    public sealed class ZombisMundo
    {
        public ParamsZombis P = ParamsZombis.PorDefecto();
        public readonly List<ZombiMundo> Lista = new List<ZombiMundo>();
        readonly List<List<PointMm>> _caminos = new List<List<PointMm>>();
        readonly List<int> _caminoI = new List<int>(), _replanTick = new List<int>(), _caminoMetaX = new List<int>(), _caminoMetaY = new List<int>(), _atasco = new List<int>(), _mejorD = new List<int>(), _apX = new List<int>(), _apY = new List<int>();
        public IRng Rng;
        int _nextId;
        public long SinCamino, Atascos;
        public string Traza(int i) { var c = _caminos[i]; int k = _caminoI[i]; return $"cam {c.Count} k {k} punto {(k < c.Count ? c[k].X + ";" + c[k].Y : "-")} atasco {_atasco[i]} replan {_replanTick[i]}"; }      // búsquedas sin camino (diagnóstico, fuera del hash)

        public ZombisMundo(uint seed) { Rng = new IRng(seed ^ 0x2B0A1u); }

        public int Vivos { get { int n = 0; for (int i = 0; i < Lista.Count; i++) if (Lista[i].Vivo) n++; return n; } }

        public int Anadir(int x, int y, int casaX, int casaY)
        {
            var z = new ZombiMundo { Id = _nextId++, X = x, Y = y, CasaX = casaX, CasaY = casaY, MetaX = x, MetaY = y, Estado = EstadoZombi.Quieto, Vivo = true, Objetivo = -1 };
            z.Espera = P.EsperaMinTicks + Rng.Below(Math.Max(1, P.EsperaMaxTicks - P.EsperaMinTicks));
            Lista.Add(z); _caminos.Add(new List<PointMm>()); _caminoI.Add(0); _replanTick.Add(-1000); _caminoMetaX.Add(int.MinValue); _caminoMetaY.Add(int.MinValue); _atasco.Add(0); _mejorD.Add(int.MaxValue); _apX.Add(int.MinValue); _apY.Add(int.MinValue);
            return z.Id;
        }

        /// <summary>El zombi de índice «i» muere (un impacto lo abate, como en NightSim).</summary>
        public bool Abatir(int i) { if (i < 0 || i >= Lista.Count || !Lista[i].Vivo) return false; var z = Lista[i]; z.Vivo = false; z.Andando = false; Lista[i] = z; return true; }

        static long D2(int ax, int ay, int bx, int by) { long dx = ax - bx, dy = ay - by; return dx * dx + dy * dy; }

        /// <summary>Un tick. «disparos» = los de este tick (UnitsWorld.Step), para el oído.</summary>
        public void Step(UnitsWorld u, RejillaTrozos rej, IList<FireEvent> disparos, int tick)
        {
            long ve2 = (long)P.VeMm * P.VeMm, pierde2 = (long)P.PierdeMm * P.PierdeMm, oye2 = (long)P.OyeMm * P.OyeMm, para2 = (long)P.ParaMm * P.ParaMm;
            for (int i = 0; i < Lista.Count; i++)
            {
                var z = Lista[i]; if (!z.Vivo) continue; z.Andando = false;

                // 1) vista: el soldado vivo más cercano (cada PercibeCadaTicks, escalonado por Id para repartir el coste)
                if ((tick + z.Id) % P.PercibeCadaTicks == 0 || z.Estado == EstadoZombi.Persigue)
                {
                    int mejor = -1, mx = 0, my = 0; long md = long.MaxValue;
                    for (int a = 0; a < u.Platoons.Count; a++)
                    {
                        var p = u.Platoons[a]; if (p.Aboard != 0) continue;
                        for (int b = 0; b < p.Men.Count; b++) { var m = p.Men[b]; if (!m.Alive) continue; long d = D2(z.X, z.Y, m.X, m.Y); if (d < md) { md = d; mejor = m.Id; mx = m.X; my = m.Y; } }
                    }
                    if (z.Estado == EstadoZombi.Persigue)
                    {
                        if (mejor < 0 || md > pierde2) { z.Estado = EstadoZombi.Vuelve; z.Objetivo = -1; z.MetaX = z.CasaX; z.MetaY = z.CasaY; }
                        else { z.Objetivo = mejor; z.MetaX = mx; z.MetaY = my; }
                    }
                    else if (mejor >= 0 && md <= ve2 && (tick + z.Id) % P.PercibeCadaTicks == 0 && u.LineOfSight(z.X, z.Y, mx, my))
                    { z.Estado = EstadoZombi.Persigue; z.Objetivo = mejor; z.MetaX = mx; z.MetaY = my; }
                }

                // 2) oído: el disparo más cercano de este tick a OyeMm o menos
                if (z.Estado != EstadoZombi.Persigue && disparos != null && disparos.Count > 0)
                {
                    long md = long.MaxValue; int rx = 0, ry = 0;
                    for (int k = 0; k < disparos.Count; k++) { long d = D2(z.X, z.Y, disparos[k].FromX, disparos[k].FromY); if (d < md) { md = d; rx = disparos[k].FromX; ry = disparos[k].FromY; } }
                    if (md <= oye2) { z.Estado = EstadoZombi.Ruido; z.MetaX = rx; z.MetaY = ry; }
                }

                // 3) quieto y deambular alrededor de su casa
                if (z.Estado == EstadoZombi.Quieto)
                {
                    if (--z.Espera <= 0)
                    {
                        for (int intento = 0; intento < 6; intento++)
                        {
                            int r = P.CasaRadioMm, dx = Rng.Below(2 * r + 1) - r, dy = Rng.Below(2 * r + 1) - r;
                            if ((long)dx * dx + (long)dy * dy > (long)r * r) continue;
                            int nx = z.CasaX + dx, ny = z.CasaY + dy; if (!rej.Libre(nx, ny) || !rej.LineaLibre(z.CasaX, z.CasaY, nx, ny)) continue;      // solo a donde se ve desde su sitio: no rodea tapias para deambular
                            z.MetaX = nx; z.MetaY = ny; z.Estado = EstadoZombi.Deambula; break;
                        }
                        if (z.Estado == EstadoZombi.Quieto) z.Espera = P.EsperaMinTicks;
                    }
                }

                // 4) andar hacia la meta
                if (z.Estado != EstadoZombi.Quieto)
                {
                    int paso = (z.Estado == EstadoZombi.Deambula ? P.PasoDeambularMmS : P.PasoCorrerMmS) / IMath.TickRate;
                    long d2 = D2(z.X, z.Y, z.MetaX, z.MetaY);
                    long llega2 = z.Estado == EstadoZombi.Persigue ? para2 : (long)paso * paso;
                    if (d2 <= llega2)
                    {
                        if (z.Estado == EstadoZombi.Deambula || z.Estado == EstadoZombi.Vuelve || z.Estado == EstadoZombi.Ruido)
                        { z.Estado = EstadoZombi.Quieto; z.Espera = P.EsperaMinTicks + Rng.Below(Math.Max(1, P.EsperaMaxTicks - P.EsperaMinTicks)); }
                        // persiguiendo: ya está pegado al soldado (el contacto lo hace UnitsWorld.ZombieContact)
                    }
                    else
                    {
                        int ax, ay; Apuntar(i, ref z, rej, tick, out ax, out ay);
                        Andar(ref z, rej, ax, ay, paso);
                        // atasco por avance (como VehicleNav): si en 1 s no se ha acercado 0,3 m a su punto, rehace el camino desde donde está
                        int dap = IMath.Dist(ax - z.X, ay - z.Y);
                        if (dap < _mejorD[i] - 300 || _apX[i] != ax || _apY[i] != ay) { _mejorD[i] = dap; _atasco[i] = tick; _apX[i] = ax; _apY[i] = ay; }
                        else if (tick - _atasco[i] >= IMath.TickRate)
                        {
                            _caminos[i].Clear(); _replanTick[i] = -1000; _caminoMetaX[i] = int.MinValue; _mejorD[i] = int.MaxValue; _atasco[i] = tick; Atascos++;
                        }
                    }
                }
                Lista[i] = z;
            }
            Separar(rej);
        }

        /// <summary>Punto al que apuntar: la meta si la línea está libre; si no, el siguiente punto del camino A* (se rehace si la meta se ha movido 3 m o cada ReplanCadaTicks).</summary>
        void Apuntar(int i, ref ZombiMundo z, RejillaTrozos rej, int tick, out int ax, out int ay)
        {
            ax = z.MetaX; ay = z.MetaY;
            if (rej.LineaLibre(z.X, z.Y, z.MetaX, z.MetaY)) { _caminos[i].Clear(); return; }
            var cam = _caminos[i];
            bool metaMovida = D2(_caminoMetaX[i], _caminoMetaY[i], z.MetaX, z.MetaY) > 3000L * 3000;
            bool rehacer = (cam.Count == 0 && (metaMovida || tick - _replanTick[i] >= 4 * P.ReplanCadaTicks)) || (cam.Count > 0 && metaMovida) || (z.Estado == EstadoZombi.Persigue && tick - _replanTick[i] >= P.ReplanCadaTicks);
            if (rehacer)
            {
                _replanTick[i] = tick; _caminoMetaX[i] = z.MetaX; _caminoMetaY[i] = z.MetaY; _caminoI[i] = 0;
                if (!rej.BuscarCamino(z.X, z.Y, z.MetaX, z.MetaY, cam) && !rej.BuscarCamino(z.X, z.Y, z.MetaX, z.MetaY, cam, 200000))      // primero en una caja de 64 m; si no, de 200 m (rodear un cercado)
                {
                    cam.Clear(); SinCamino++;
                    if (z.Estado != EstadoZombi.Persigue) { z.Estado = EstadoZombi.Quieto; z.Espera = P.EsperaMinTicks; }      // no hay paso: se queda donde está (sin buscar cada tick)
                    return;
                }
            }
            if (cam.Count == 0) return;      // sin camino hace poco: recto y deslizándose, sin volver a buscar hasta que toque
            int k = _caminoI[i];
            while (k < cam.Count - 1 && D2(z.X, z.Y, cam[k].X, cam[k].Y) <= 600L * 600) k++;     // punto alcanzado: al siguiente
            _caminoI[i] = k;
            if (k < cam.Count) { ax = cam[k].X; ay = cam[k].Y; }
        }

        static bool Andar(ref ZombiMundo z, RejillaTrozos rej, int ax, int ay, int paso)
        {
            int dx = ax - z.X, dy = ay - z.Y; int d = IMath.Dist(dx, dy); if (d <= 0 || paso <= 0) return true;
            if (paso > d) paso = d;
            int sx = (int)((long)dx * paso / d), sy = (int)((long)dy * paso / d);
            int nx = z.X + sx, ny = z.Y + sy;
            if (rej.Libre(nx, ny) || !rej.Libre(z.X, z.Y)) { z.X = nx; z.Y = ny; }      // si ya está metido en un obstáculo (empujado), puede salir
            else if (sx != 0 && rej.Libre(z.X + Math.Sign(sx) * paso, z.Y)) z.X += Math.Sign(sx) * paso;      // deslizarse por el muro
            else if (sy != 0 && rej.Libre(z.X, z.Y + Math.Sign(sy) * paso)) z.Y += Math.Sign(sy) * paso;
            else return false;
            z.Rumbo = IMath.Atan2(dy, dx) & 0xFFFF; z.Andando = true; return true;
        }

        // separación: cubos de 1 m; dos zombis a menos de 2 cuerpos se apartan la mitad del solape cada uno (si el sitio está libre)
        readonly Dictionary<long, List<int>> _cubos = new Dictionary<long, List<int>>(); readonly List<long> _clavesUsadas = new List<long>();
        void Separar(RejillaTrozos rej)
        {
            foreach (var k in _clavesUsadas) _cubos[k].Clear(); _clavesUsadas.Clear();
            const int cubo = 1000;
            for (int i = 0; i < Lista.Count; i++)
            {
                var z = Lista[i]; if (!z.Vivo) continue; long key = ((long)(z.X / cubo) << 32) | (uint)(z.Y / cubo);
                if (!_cubos.TryGetValue(key, out var l)) { l = new List<int>(); _cubos[key] = l; }
                if (l.Count == 0) _clavesUsadas.Add(key); l.Add(i);
            }
            int min = 2 * P.CuerpoMm; long min2 = (long)min * min;
            for (int i = 0; i < Lista.Count; i++)
            {
                var a = Lista[i]; if (!a.Vivo) continue; int cx = a.X / cubo, cy = a.Y / cubo; int px = 0, py = 0;
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                {
                    if (!_cubos.TryGetValue(((long)(cx + ox) << 32) | (uint)(cy + oy), out var l)) continue;
                    foreach (int j in l)
                    {
                        if (j == i) continue; var b = Lista[j]; long d2 = D2(a.X, a.Y, b.X, b.Y); if (d2 >= min2) continue;
                        int d = (int)IMath.Isqrt(d2), solape = (min - d) / 2;
                        if (d == 0) { int s = i < j ? 1 : -1; px += s * solape; continue; }      // en el mismo punto: se apartan por X, según el orden
                        px += (int)((long)(a.X - b.X) * solape / d); py += (int)((long)(a.Y - b.Y) * solape / d);
                    }
                }
                if (px == 0 && py == 0) continue;
                int lim = P.CuerpoMm; px = IMath.Clamp(px, -lim, lim); py = IMath.Clamp(py, -lim, lim);
                if (rej.Libre(a.X + px, a.Y + py)) { a.X += px; a.Y += py; Lista[i] = a; }
            }
        }

        public ulong Hash()
        {
            ulong h = IMath.Mix(0x2B0A2UL ^ Rng.S) + P.Hash() + IMath.Mix((ulong)(uint)_nextId);
            for (int i = 0; i < Lista.Count; i++)
            {
                var z = Lista[i];
                h += IMath.Mix((ulong)(uint)z.Id + ((ulong)(uint)z.X << 20) + ((ulong)(uint)z.Y << 42)) ^ IMath.Mix((ulong)(uint)z.MetaX + ((ulong)(uint)z.MetaY << 32))
                   ^ IMath.Mix((ulong)z.Estado + ((z.Vivo ? 1UL : 0UL) << 8) + ((ulong)(uint)z.Espera << 9) + ((ulong)(uint)(z.Objetivo + 1) << 32) + ((ulong)(uint)z.Rumbo << 48) + (ulong)(i + 1) * 0x9E3779B97F4A7C15UL);
            }
            return h;
        }
    }
}
