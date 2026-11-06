"""Cañas (Arundo donax) del cauce del Palancia y de las acequias: capa C del suelo (D-N67). Teo, 6-nov-2026.

Fotos H2 de Paula (cañar del Palancia en Soneja y del Gorgos): matas muy densas de tallos rectos de 2 a 3 m, la base
verde y los tallos secos pajizos, con penachos claros arriba y hojas que cuelgan. Reglas de Lucía: ≤ 150 triángulos,
de 2 a 3 m; tallos en 847c34 y penachos en b4a080; NORMALES HACIA ARRIBA (un tallo vertical caería a L* 21, el valor
del zombi). Solo en los parches al pie de las orillas del cauce y en las acequias del faldón, lejos de la horda.
Cada tallo es un tetraedro alto (4 triángulos) y cada penacho un octaedro (8).
Variantes: 0 densa de 3 m; 1 media de 2,5 m; 2 rala de 2 m; 3 tumbada por una riada (tallos inclinados hacia +X);
4 seca y rota (más penachos, tallos partidos a media altura).
"""
import math, os, random
from taller import Taller
from suelo_matas import hoja, _octaedro

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["tallo", "seco", "penacho"]
COLORES = {"tallo": "847c34", "seco": "88885c", "penacho": "b4a080"}
FACCION = "neutro"
PRESUPUESTO = {0: (1, 150), 1: (1, 120)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True


def construir(nombre="SM_Canas", lod=0):
    t = Taller(PIEZAS)
    v = VAR % 5
    R = random.Random(500 + v)
    n, H, rad = {0: (24, 3.0, 0.7), 1: (20, 2.5, 0.65), 2: (13, 2.0, 0.8), 3: (20, 2.6, 0.7), 4: (18, 2.2, 0.7)}[v]
    if lod:
        n = int(n * 0.65)
    npen = 0
    for i in range(n):
        a = R.uniform(0, 2 * math.pi)
        r = rad * math.sqrt(R.uniform(0, 1))
        x, y = r * math.cos(a), r * math.sin(a)
        h = H * R.uniform(0.7, 1.0)
        dx, dy = R.uniform(-0.25, 0.25), R.uniform(-0.25, 0.25)
        if v == 3:
            dx += h * 0.45
            h *= 0.8
        if v == 4 and i % 3 == 0:
            h *= 0.5
        top = (x + dx, y + dy, h)
        hoja(t, (x, y, -0.01), top, 0.07, "seco" if (i % 3 == 0 or v == 4 and i % 2) else "tallo", giro=a)
        if (i % 4 == 0 or v == 4 and i % 3 == 0) and npen < 6 and h > H * 0.6:
            _octaedro(t, (top[0], top[1], top[2] + 0.1), 0.12, "penacho")
            npen += 1
    return t.objeto(nombre + "_v%d" % v, bisel=0)
