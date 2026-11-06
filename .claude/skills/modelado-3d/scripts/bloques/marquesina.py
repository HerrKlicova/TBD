"""Marquesina de andén de 30 m: pilares de fundición y cubierta de chapa a un agua (Lucía, ambientacion-sagunto.md,
pieza 3). Teo, 6-nov-2026. Pieza repetible.

MEDIDAS SIN VERIFICAR (típicas, a la espera de las fotos de Paula): 30 x 5 m, pilares cada 6 m en una fila a 1 m del
borde de atrás, 3,8 m de altura libre, cubierta de chapa que sube 0,6 m hacia el edificio (+Y), con ménsulas.
Colores de Lucía: chapa 94948c; pilares y vigas 283440. En el LOD1 los pilares se engordan a 2 px (Bruno).
Delante (lado de la vía) = -Y en Blender (+Z en Unity).

v2 (6-nov 17:35) con las fotos de Paula (E2_marquesina_1..3: La Encina y Almazán, marquesinas de andén de la misma
época): los pilares de fundición van en la fila del lado de la vía y la cubierta se apoya en cerchas de celosía que
cruzan hasta el edificio, con un tornapuntas curvo en cada pilar. Pilares y cerchas en verde oscuro (la fundición
verde de la foto, en el 50541c de la paleta). En el LOD1 la celosía es una viga maciza (las barras no llegan a 2 px).
Medidas SIN VERIFICAR (a ojo con las fotos).
"""
import math, os
from taller import Taller

# Variantes (Eugenio, 6-nov: al menos 5): TBD_VARIANTE 0 base; 1 un tramo sin chapa (se ven las cerchas); 2 un tramo
# hundido; 3 un pilar caído y su tramo colgando; 4 corta de 24 m (4 tramos); 5 chapa vieja, con tiras sueltas y
# canalón roto. La marquesina va por tramos de 6 m separados 2 cm (sin aristas compartidas para limpiar).
VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)

PIEZAS = ["chapa", "pilar", "viga", "onda", "canalon"]
COLORES = {"chapa": "94948c", "pilar": "50541c", "viga": "50541c", "onda": "6c7874", "canalon": "283440"}
FACCION = "neutro"
PRESUPUESTO = {0: (100, 2500), 1: (60, 1500)}
LARGO, FONDO, ALTO = 30.0, 5.0, 3.8
YP = -FONDO / 2 + 0.9      # fila de pilares en el borde de la vía (fotos E2)


def construir(nombre="SM_Marquesina", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    r = 0.12 if lod == 0 else 0.18
    ntr = 4 if VAR == 4 else 5
    largo = 6.0 * ntr
    xs = [-largo / 2 + 3.0 + 6.0 * k for k in range(ntr)]
    for i, x in enumerate(xs):
        caido = VAR == 3 and i == 2
        if caido:   # pilar caído en el andén, hacia la vía
            t.cilindro((x, YP - ALTO / 2 + 0.2, r), r, ALTO, "Y", 8 if lod == 0 else 6, "pilar")
        else:
            t.cilindro((x, YP, ALTO / 2), r, ALTO, "Z", 8 if lod == 0 else 6, "pilar")
            fino.cilindro((x, YP, 0.15), r + 0.08, 0.3, "Z", 8, "pilar")              # basa
            fino.caja((x, YP, ALTO - 0.15), (0.45, 0.45, 0.3), "pilar")               # capitel
        dz = -0.9 if caido else 0.0
        # cercha de celosía que cruza de la vía al edificio (+Y), horizontal bajo la chapa
        yc, lc = 0.0, FONDO + 0.2
        if lod == 0:
            for zc in (ALTO + 0.1, ALTO - 0.4):                                      # cordones superior e inferior
                t.caja((x, yc, zc + dz / 2), (0.08, lc, 0.08), "viga", rot_x=math.atan2(dz, FONDO))
            for k in range(10):                                                    # diagonales en zigzag
                yy = -FONDO / 2 + 0.35 + k * (FONDO - 0.7) / 9
                zz = ALTO - 0.15 + dz * (0.5 - (yy + FONDO / 2) / FONDO)
                t.caja((x, yy, zz), (0.06, 0.06, 0.6), "viga", rot_x=math.radians(30 if k % 2 else -30))
            if not caido:
                for sy in (-1, 1):                                                 # tornapuntas del pilar a la cercha
                    t.caja((x, YP + sy * 0.45, ALTO - 0.75), (0.07, 0.07, 1.0), "viga", rot_x=math.radians(-40 * sy))
        else:
            t.caja((x, yc, ALTO - 0.15 + dz / 2), (0.3, lc, 0.55), "viga", rot_x=math.atan2(dz, FONDO))
    t.caja((0, YP, ALTO + 0.05), (largo, 0.25, 0.3), "viga")                      # viga longitudinal
    # cubierta por tramos de 6 m (con 2 cm de holgura entre tramos)
    a = math.atan2(0.6, FONDO)
    r_ = __import__("random").Random(77 + VAR)
    for i in range(ntr):
        x0 = -largo / 2 + 6.0 * i + 3.0
        if VAR == 1 and i == 1:
            continue                                                               # tramo sin chapa
        caida = 0.0
        if VAR == 2 and i == 3:
            caida = -0.5                                                           # tramo hundido
        if VAR == 3 and i == 2:
            caida = -0.9                                                           # tramo que cuelga del pilar caído
        t.tejado((x0, 0, ALTO + 0.2 + caida), 5.98, FONDO, 0.6, "chapa", aguas=1)
        # chapa ondulada (Lucía 16:51): tiras de 0,25 m en 6c7874 cada 0,5 m sobre la chapa, con su pendiente
        for k in range(12):
            if VAR == 5 and r_.random() < 0.3:
                continue                                                           # chapa vieja: tiras sueltas
            x = x0 - 3.0 + 0.25 + k * 0.5
            t.caja((x, 0, ALTO + 0.5 + 0.02 + caida), (0.25, FONDO / math.cos(a), 0.02), "onda", rot_x=a)
    # canalón más oscuro en el borde de la vía (roto en la v5)
    if VAR == 5:
        t.caja((-largo / 4 - 0.5, -FONDO / 2 - 0.1, ALTO + 0.15), (largo / 2 - 1.0, 0.25, 0.25), "canalon")
        t.caja((largo / 4 + 1.0, -FONDO / 2 - 0.1, ALTO + 0.15), (largo / 2 - 2.0, 0.25, 0.25), "canalon")
        t.caja((0.6, -FONDO / 2 - 0.3, ALTO - 0.6), (1.6, 0.25, 0.25), "canalon", rot_y=math.radians(35))   # trozo colgando
    else:
        t.caja((0, -FONDO / 2 - 0.1, ALTO + 0.15), (largo, 0.25, 0.25), "canalon")
    return t.objeto(nombre, bisel=0)
