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
import math
from taller import Taller

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
    xs = [-15.0 + 3.0 + 6.0 * k for k in range(5)]
    for x in xs:
        t.cilindro((x, YP, ALTO / 2), r, ALTO, "Z", 8 if lod == 0 else 6, "pilar")
        fino.cilindro((x, YP, 0.15), r + 0.08, 0.3, "Z", 8, "pilar")              # basa
        fino.caja((x, YP, ALTO - 0.15), (0.45, 0.45, 0.3), "pilar")               # capitel
        # cercha de celosía que cruza de la vía al edificio (+Y), horizontal bajo la chapa
        yc, lc = 0.0, FONDO + 0.2
        if lod == 0:
            for zc in (ALTO + 0.1, ALTO - 0.4):                                      # cordones superior e inferior
                t.caja((x, yc, zc), (0.08, lc, 0.08), "viga")
            for k in range(10):                                                    # diagonales en zigzag
                yy = -FONDO / 2 + 0.35 + k * (FONDO - 0.7) / 9
                t.caja((x, yy, ALTO - 0.15), (0.06, 0.06, 0.6), "viga", rot_x=math.radians(30 if k % 2 else -30))
            for sy in (-1, 1):                                                     # tornapuntas del pilar a la cercha
                t.caja((x, YP + sy * 0.45, ALTO - 0.75), (0.07, 0.07, 1.0), "viga", rot_x=math.radians(-40 * sy))
        else:
            t.caja((x, yc, ALTO - 0.15), (0.3, lc, 0.55), "viga")                  # LOD1: viga maciza de 2 px
    t.caja((0, YP, ALTO + 0.05), (LARGO, 0.25, 0.3), "viga")                      # viga longitudinal
    t.tejado((0, 0, ALTO + 0.2), LARGO, FONDO, 0.6, "chapa", aguas=1)
    # chapa ondulada (Lucía 16:51): franjas de ~1 px en dos valores, perpendiculares a la vía. A 30 m en 128 px,
    # 1 px = 0,23 m: tiras de 0,25 m en 6c7874 cada 0,5 m, pegadas 2 cm sobre la chapa con su misma pendiente
    a = math.atan2(0.6, FONDO)
    for k in range(60):
        x = -LARGO / 2 + 0.25 + k * 0.5
        t.caja((x, 0, ALTO + 0.5 + 0.02), (0.25, FONDO / math.cos(a), 0.02), "onda", rot_x=a)
    # canalón más oscuro en el borde de la vía
    t.caja((0, -FONDO / 2 - 0.1, ALTO + 0.15), (LARGO, 0.25, 0.25), "canalon")
    return t.objeto(nombre, bisel=0)
