"""Pino carrasco (Pinus halepensis) del pinar ralo de Bétera (D-N173; Álvaro, betera-nivel.md §5.7: 5 variantes por
semilla, sin colisión, unos 200 con instancing). Teo (3), 8-nov.

Fotos de Paula BT7_pino_carrasco_1 (cerca de Sagunto), _2 (Torres Torres: alto, de tronco desnudo y copa en parasol)
y _3 (Montgó: tronco torcido). Corteza GRIS BLANQUECINA, tronco tortuoso, copa irregular y abierta en grumos (no un
cono); en monte bajo como el de Bétera, entre 8 y 15 m (ESTIMACIÓN de Paula; llega a 25 m). Mismo método que el
naranjo aprobado: tronco con dos o tres ramas que entran en grumos de icosferas.
Colores: copa con valor partido como el naranjo, arriba 88885c, costados 70784c y abajo 50541c (nunca 303424, que es
de la horda); tronco 94948c (corteza gris blanquecina, antes 6c7874) con la base 6c7874; con 543820 la sombra caía en la banda de la horda (Lucía, 8-nov).
Origen en el pie del tronco. Variantes (TBD_VARIANTE % 5): 0 típico (11 m); 1 joven (6 m, copa más densa);
2 inclinado por el viento; 3 en parasol (14 m, tronco desnudo y copa plana y ancha arriba, el de BT7_2); 4 seco a medias (media copa sin hojas, ramas a la vista).
"""
import math, os, random
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["copa", "tronco", "base"]
COLORES = {"copa": "70784c", "tronco": "94948c", "base": "6c7874"}
VALOR_PIEZAS = {"copa": ("88885c", "70784c", "50541c")}
FACCION = "neutro"
PRESUPUESTO = {0: (150, 1400), 1: (80, 700)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _rama(t, p0, p1, r, pieza="tronco"):
    dx, dy, dz = p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]
    L = math.sqrt(dx * dx + dy * dy + dz * dz)
    t.caja(((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, (p0[2] + p1[2]) / 2), (r, r, L + r * 0.5), pieza,
           rot_z=math.atan2(dy, dx), rot_y=math.atan2(math.hypot(dx, dy), dz))


def construir(nombre="SM_Pino", lod=0):
    v = VAR
    t = Taller(PIEZAS)
    fino = lod == 0
    rnd = random.Random(7100 + v)
    H = (11.0, 6.0, 10.0, 14.0, 10.0)[v]
    k = H / 11.0
    inc = (0.10, 0.04, 0.32, 0.06, 0.12)[v]          # inclinación del tronco (rad)
    rt = max(0.36 if not fino else 0.2, 0.26 * k)
    # tronco en tres tramos que se tuercen
    pts = [(0, 0, 0)]
    az = rnd.uniform(0, 2 * math.pi)
    for j in range(3):
        z = H * ((0.22 + 0.2 * j) if v != 3 else (0.28 + 0.24 * j))
        off = math.sin(inc) * z
        pts.append((off * math.cos(az) + rnd.uniform(-0.25, 0.25), off * math.sin(az) + rnd.uniform(-0.25, 0.25), z))
    for a, b in zip(pts, pts[1:]):
        _rama(t, a, b, rt * 2, "tronco")
    t.caja((0, 0, 0.5), (rt * 2.4, rt * 2.4, 1.0), "base")
    top = pts[-1]
    # ramas principales y grumos (en pisos)
    pisos = 1
    n = (5, 6, 4, 6, 5)[v]
    for p in range(pisos):
        zc = top[2] + (0.8 if p == 0 else -3.2) * k
        base = top if p == 0 else pts[2]
        for i in range(n):
            a = i * 2 * math.pi / n + rnd.uniform(-0.3, 0.3)
            rr = rnd.uniform(1.6, 2.6) * k * (0.75 if v == 1 else 1.0) * (1.25 if v == 3 else 1.0)
            fin = (base[0] + rr * math.cos(a) + math.sin(inc) * 1.2 * math.cos(az),
                   base[1] + rr * math.sin(a) + math.sin(inc) * 1.2 * math.sin(az),
                   zc + rnd.uniform(-0.6, 0.6) * k * (0.4 if v == 3 else 1.0))
            seca = v == 4 and math.cos(a - az) > 0.2
            if fino or seca:
                _rama(t, base, fin, max(0.12 * k, 0.34 if not fino else 0.1), "tronco")
            if seca:
                continue
            g = rnd.uniform(1.1, 1.6) * k * (1.15 if v == 1 else 1.0)
            t.esfera(fin, (g * 1.25, g * 1.25, g * (0.45 if v == 3 else 0.6)), "copa", 2 if fino else 1)
        if not (v == 4):
            g = 1.5 * k
            t.esfera((base[0] + math.sin(inc) * 0.8 * math.cos(az), base[1] + math.sin(inc) * 0.8 * math.sin(az),
                      zc + 0.5 * k), (g * 1.4, g * 1.4, g * 0.7), "copa", 2 if fino else 1)
    return t.objeto(nombre, bisel=0)
