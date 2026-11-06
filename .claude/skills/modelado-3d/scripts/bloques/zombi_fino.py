"""Zombi de la Colmena, variante FINA (Eugenio, 6-nov 13:07: el zombi de cerca se ve «muy gordo, muy nítido»).

Misma pose, paleta y reglas que zombi_cercano.py (aprobado por Lucía), pero con la silueta más delgada y estilizada:
torso de 0,22 m (antes 0,30), brazos de 0,08 m (antes 0,15), piernas de 0,10 m (antes 0,15) y cabeza de 0,22 m (antes
0,27). Medidas 1,75 x ~0,40 m: proporción ~4,4:1. Sin las placas del roto: menos triángulos que la variante gruesa.
Pensado para verse pixelado por el shader de Unity: las extremidades del LOD1 quedan en ~1 px a 20 px y la figura se lee
como un trazo oscuro y fino con los brazos al frente. Delante = -Y en Blender (+Z en Unity).
"""
import math
from taller import Taller
from zombi_cercano import PIEZAS, CAMISAS, COLORES, COLORES_LOD1, VALOR_PIEZAS, FACCION, LEGIBILIDAD, UMBRAL_CONTORNO

PRESUPUESTO = {0: (60, 400), 1: (60, 300)}


def construir(nombre="SM_Zombi_Fino", lod=0):
    t = Taller(PIEZAS)
    cadera = 0.96
    largo = cadera - 0.04
    g = 0.10 if lod == 0 else 0.12
    for x, ang in ((-0.065, 14), (0.065, -14)):   # piernas en V invertida (lámina de Bruno)
        r = math.radians(ang)
        t.caja((x, -largo / 2 * math.sin(r), cadera - largo / 2 * math.cos(r)), (g, g, largo), "pantalon", rot_x=-r)
        t.caja((x, -largo * math.sin(r) - 0.03, 0.03), (g, 0.2, 0.06), "calzado")
    enc = math.radians(12)                          # algo más encorvado: la silueta fina lo necesita para leerse
    t.caja((0.0, -0.05, 1.21), (0.22, 0.17, 0.54), "camisa", rot_x=enc)
    a = 0.08 if lod == 0 else 0.10
    for x, z, caida in ((-0.15, 1.4, 3), (0.15, 1.35, 10)):   # brazos al frente, desparejos
        r = math.radians(caida)
        lb, y0 = 0.5, -0.1
        t.caja((x, y0 - lb / 2 * math.cos(r), z - lb / 2 * math.sin(r)), (a, lb, a), "camisa", rot_x=r)
        hy, hz = y0 - (lb + 0.04) * math.cos(r), z - (lb + 0.04) * math.sin(r) - 0.05
        t.caja((x, hy, hz), (0.08, 0.09, 0.11), "piel", rot_x=r)
    t.caja((0.0, -0.13, 1.5), (0.08, 0.08, 0.1), "piel")              # cuello
    t.caja((0.0, -0.17, 1.64), (0.22, 0.22, 0.22), "piel")            # cabeza adelantada
    t.caja((0.0, -0.11, 1.765), (0.23, 0.12, 0.03), "pelo")
    t.caja((0.0, -0.05, 1.67), (0.23, 0.02, 0.15), "pelo")
    t.caja((0.0, -0.04, 1.71), (0.1, 0.02, 0.08), "nuca")             # luz de 1 px en la nuca (Lucía)
    return t.objeto(nombre, bisel=0)
