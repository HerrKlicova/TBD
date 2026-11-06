"""Marquesina de andén de 30 m: pilares de fundición y cubierta de chapa a un agua (Lucía, ambientacion-sagunto.md,
pieza 3). Teo, 6-nov-2026. Pieza repetible.

MEDIDAS SIN VERIFICAR (típicas, a la espera de las fotos de Paula): 30 x 5 m, pilares cada 6 m en una fila a 1 m del
borde de atrás, 3,8 m de altura libre, cubierta de chapa que sube 0,6 m hacia el edificio (+Y), con ménsulas.
Colores de Lucía: chapa 94948c; pilares y vigas 283440. En el LOD1 los pilares se engordan a 2 px (Bruno).
Delante (lado de la vía) = -Y en Blender (+Z en Unity).
"""
from taller import Taller

PIEZAS = ["chapa", "pilar", "viga"]
COLORES = {"chapa": "94948c", "pilar": "283440", "viga": "283440"}
FACCION = "neutro"
PRESUPUESTO = {0: (100, 2000), 1: (60, 800)}
LARGO, FONDO, ALTO = 30.0, 5.0, 3.8
YP = FONDO / 2 - 1.0


def construir(nombre="SM_Marquesina", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    r = 0.12 if lod == 0 else 0.18
    xs = [-15.0 + 3.0 + 6.0 * k for k in range(5)]
    for x in xs:
        t.cilindro((x, YP, ALTO / 2), r, ALTO, "Z", 8 if lod == 0 else 6, "pilar")
        fino.cilindro((x, YP, 0.15), r + 0.08, 0.3, "Z", 8, "pilar")              # basa
        fino.caja((x, YP, ALTO - 0.15), (0.45, 0.45, 0.3), "pilar")               # capitel
        # ménsula hacia la vía: viga inclinada que sostiene el vuelo
        t.caja((x, YP - 1.9, ALTO - 0.05), (0.12 if lod == 0 else 0.3, 4.0, 0.2), "viga")
    t.caja((0, YP, ALTO + 0.05), (LARGO, 0.25, 0.3), "viga")                      # viga longitudinal
    t.tejado((0, 0, ALTO + 0.2), LARGO, FONDO, 0.6, "chapa", aguas=1)
    fino.caja((0, -FONDO / 2 + 0.05, ALTO + 0.05), (LARGO, 0.06, 0.35), "chapa")  # faldón de la chapa (lado vía)
    return t.objeto(nombre, bisel=0)
