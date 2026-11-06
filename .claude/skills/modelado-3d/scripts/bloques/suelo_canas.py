"""Cañas (Arundo donax) del cauce del Palancia y de las acequias: capa C del suelo (D-N67). Teo, 6-nov-2026.

Fotos H2 de Paula (cañar del Palancia en Soneja y del Gorgos): matas muy densas de tallos rectos de 2 a 3 m, la base
verde y los tallos secos pajizos, con penachos claros arriba y hojas que cuelgan. Reglas de Lucía: ≤ 150 triángulos,
de 2 a 3 m; tallos en 847c34 y penachos en b4a080; NORMALES HACIA ARRIBA (un tallo vertical caería a L* 21, el valor
del zombi). Solo en los parches al pie de las orillas del cauce y en las acequias del faldón, lejos de la horda.
Revisión de Lucía (19:32): a tamaño de juego un tallo suelto medía 1 px y la mata se leía como un garabato oscuro.
Ahora los tallos van AGRUPADOS en 3 o 4 haces gruesos (tronco de pirámide de unos 0,55 m de base y 0,33 m arriba, más
de 2 px a 0,17 m/px), con la parte baja en 847c34 y el tramo alto en 88885c (seco), penachos octaedro b4a080. Sin hojas
colgantes: a tamaño de juego eran otra vez líneas de 1 px. Los haces se abren hacia fuera para que se lean separados.
Cada haz son 18 triángulos. Revisión 19:36: haces afilados arriba y 1–2 tallos sueltos más altos (silueta dentada).
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


def haz(t, puntos, anchos, giro, piezas):
    """Haz de tallos: tronco de pirámide cuadrado por tramos (un anillo en cada punto), cerrado."""
    bm = t.bm
    anillos = [[bm.verts.new((c[0] + w / 2 * math.cos(giro + j * math.pi / 2),
                              c[1] + w / 2 * math.sin(giro + j * math.pi / 2), c[2])) for j in range(4)]
               for c, w in zip(puntos, anchos)]
    t._marcar([bm.faces.new(tuple(reversed(anillos[0])))], piezas[0])
    for k in range(len(anillos) - 1):
        a, b = anillos[k], anillos[k + 1]
        t._marcar([bm.faces.new((a[j], a[(j + 1) % 4], b[(j + 1) % 4], b[j])) for j in range(4)], piezas[k])
    t._marcar([bm.faces.new(tuple(anillos[-1]))], piezas[-1])


def construir(nombre="SM_Canas", lod=0):
    t = Taller(PIEZAS)
    v = VAR % 5
    R = random.Random(500 + v)
    n, H, rad = {0: (4, 3.0, 0.62), 1: (4, 2.5, 0.58), 2: (3, 2.0, 0.6), 3: (4, 2.6, 0.55), 4: (4, 2.2, 0.6)}[v]
    npen = 0
    a0 = R.uniform(0, 2 * math.pi)
    for i in range(n):
        a = a0 + 2 * math.pi * i / n + R.uniform(-0.3, 0.3)
        r = rad * R.uniform(0.6, 1.0) if i else 0.0
        x, y = r * math.cos(a), r * math.sin(a)
        h = H * (1.0 if i == 0 else R.uniform(0.72, 0.95))
        dx, dy = 0.5 * x + R.uniform(-0.12, 0.12), 0.5 * y + R.uniform(-0.12, 0.12)   # se abren hacia fuera
        if v == 3:                                        # tumbada por la riada: todo hacia +X
            dx += h * 0.55
            h *= 0.75
        roto = v == 4 and i % 2 == 1                      # partido a media altura
        if roto:
            h *= 0.55
        f1 = 0.62 if not roto else 0.8
        base, mid, top = (x, y, -0.02), (x + dx * f1, y + dy * f1, h * f1), (x + dx, y + dy, h)
        w = (R.uniform(0.5, 0.6), R.uniform(0.42, 0.48), R.uniform(0.08, 0.12))   # casi en punta bajo el penacho (Lucía 19:36)
        haz(t, (base, mid, top), w, a + R.uniform(0, 1), ("tallo", "tallo" if roto else "seco"))
        if not roto and i < 2:                            # tallo suelto más alto: silueta de arriba dentada
            b = (x + dx * 0.5, y + dy * 0.5, h * 0.5)
            hoja(t, b, (x + dx * 1.25 + 0.15 * math.cos(a), y + dy * 1.25 + 0.15 * math.sin(a), h * 1.2), 0.34,
                 "seco", giro=a)
        if not roto and npen < 5:
            _octaedro(t, (top[0], top[1], top[2] + 0.12), 0.17, "penacho")
            npen += 1
    if v == 4:                                            # penacho caído en el suelo
        _octaedro(t, (0.75, -0.45, 0.08), 0.15, "penacho")
    return t.objeto(nombre + "_v%d" % v, bisel=0)
