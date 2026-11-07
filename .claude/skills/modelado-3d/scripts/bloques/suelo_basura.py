"""Basura de cuneta: capa C del suelo (D-N67). Teo, 6-nov-2026.

Fotos H6 de Paula (basura de cuneta): bolsas, papeles y plásticos claros sueltos, envases y algún objeto grande.
Reglas de Lucía: ≤ 150 triángulos; b8b8b8 solo en motas pequeñas (nunca bordes largos, se confunden con la selección
d4d0cc) y 94948c; nada de valores de la horda, rojo ni ámbar. Sin colisión ni sombra.
Colores: plástico 94948c, motas b8b8b8, goma del neumático 605c48 (polvorienta; en 48403c se leía negra, como la horda), madera vieja b4a080 con sombra 88885c, colchón
b4a080 con manchas 88885c.
Variantes: 0 bolsa de basura reventada con papeles; 1 neumático tirado; 2 palé roto; 3 caja de naranjas de madera
volcada; 4 colchón tirado (1,9 × 0,9 m).
"""
import math, os, random
from taller import Taller
from suelo_piedras import canto

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["plastico", "mota", "goma", "madera", "sombra"]
COLORES = {"plastico": "94948c", "mota": "b8b8b8", "goma": "605c48", "madera": "b4a080", "sombra": "88885c"}
FACCION = "neutro"
PRESUPUESTO = {0: (1, 150), 1: (1, 150)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True   # sin esto, el neumático y las caras de sombra caían casi a negro, como la horda


def construir(nombre="SM_Basura", lod=0):
    t = Taller(PIEZAS)
    v = VAR % 5
    R = random.Random(300 + v)
    if v == 0:                                   # bolsa reventada y papeles sueltos (motas pequeñas)
        f = canto(t, (0, 0, 0.0), 0.3, 0.8, 11)
        t._marcar(f, "plastico")
        for i in range(5):
            a = R.uniform(0, 6.28)
            d = R.uniform(0.35, 0.7)
            t.caja((d * math.cos(a), d * math.sin(a), 0.01), (0.16, 0.12, 0.02), "mota", rot_z=a)
        t.caja((0.05, -0.1, 0.42), (0.1, 0.1, 0.08), "mota")              # nudo
    elif v == 1:                                 # neumático tumbado: anillo de 10 tramos
        n = 10
        for k in range(n):
            a = k * 2 * math.pi / n
            t.caja((0.3 * math.cos(a), 0.3 * math.sin(a), 0.1), (0.17, 0.2, 0.2), "goma", rot_z=a)
        t.caja((0, 0, 0.005), (0.3, 0.3, 0.01), "plastico")                # agua sucia dentro
    elif v == 2:                                 # palé de 1,2 × 0,8 m roto: tablas y tacos, dos tablas sueltas
        for x in (-0.5, 0.0, 0.5):
            t.caja((x, 0, 0.05), (0.1, 0.8, 0.1), "sombra")
        for k, y in enumerate((-0.33, -0.11, 0.11, 0.33)):
            if k == 2:
                continue
            t.caja((0, y, 0.12), (1.2, 0.14, 0.03), "madera")
        t.caja((0.75, 0.55, 0.015), (1.0, 0.14, 0.03), "madera", rot_z=0.5)
        t.caja((-0.4, -0.6, 0.015), (0.6, 0.14, 0.03), "madera", rot_z=-0.3)
    elif v == 3:                                 # caja de naranjas de madera volcada de lado
        for z in (0.04, 0.16, 0.28):
            t.caja((0, 0.0, z), (0.5, 0.02, 0.08), "madera")               # fondo de listones (de pie)
        t.caja((-0.25, -0.15, 0.17), (0.04, 0.3, 0.34), "sombra")
        t.caja((0.25, -0.15, 0.17), (0.04, 0.3, 0.34), "sombra")
        for y in (-0.08, -0.22):
            t.caja((0, y, 0.02), (0.5, 0.08, 0.02), "madera")
        t.caja((0.5, -0.3, 0.01), (0.5, 0.08, 0.02), "madera", rot_z=0.7)  # listón suelto
    else:                                        # colchón tirado, un poco doblado, con manchas
        t.caja((-0.45, 0, 0.1), (1.0, 0.9, 0.2), "madera")
        t.caja((0.45, 0, 0.16), (0.9, 0.9, 0.2), "madera", rot_y=-0.15)
        t.caja((-0.5, 0.15, 0.205), (0.4, 0.3, 0.02), "sombra")
        t.caja((0.4, -0.2, 0.28), (0.3, 0.25, 0.02), "sombra", rot_y=-0.15)
    return t.objeto(nombre + "_v%d" % v, bisel=0)
