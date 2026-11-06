"""Poste de catenaria con el cable cortado (Lucía, ambientacion-sagunto.md, pieza 4). Teo, 6-nov-2026. Marcos lo
repite cada 50 m a lo largo de la vía.

MEDIDAS SIN VERIFICAR (típicas de un poste de perfil de acero de ADIF, a la espera de las fotos de Paula): mástil de
0,3 x 0,3 x 7,5 m sobre zapata de hormigón, ménsula de 3,2 m hacia la vía a 6,3 m con tirante, aisladores y el hilo
cortado colgando 2,5 m. Colores: acero galvanizado 6c7874, zapata 94948c, aisladores 1c200c, hilo 1c200c.
En el LOD1 el mástil y la ménsula se engordan a 2 px y el hilo y los aisladores se quitan (Bruno: < 2 px parpadea).
La vía queda hacia -X.
"""
import math
from taller import Taller

PIEZAS = ["acero", "zapata", "aislador", "hilo", "mensula"]
COLORES = {"acero": "6c7874", "zapata": "94948c", "aislador": "1c200c", "hilo": "1c200c", "mensula": "6c7874"}
# LOD1 (Lucía 16:51): la ménsula salía a cuadros claro y oscuro; va como barra sólida oscura de 2 px
COLORES_LOD1 = {"mensula": "303424"}
FACCION = "neutro"
PRESUPUESTO = {0: (60, 800), 1: (30, 300)}
UMBRAL_CONTORNO = {1: 9.0}


def construir(nombre="SM_PosteCatenaria", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    g = 0.3 if lod == 0 else 0.36
    t.caja((0, 0, 0.25), (0.9, 0.9, 0.5), "zapata")
    t.caja((0, 0, 0.5 + 3.75), (g, g, 7.5), "acero")
    gm = 0.14 if lod == 0 else 0.36
    t.caja((-1.6, 0, 6.3), (3.2, gm, gm), "mensula")                                # ménsula hacia la vía (-X)
    a = math.atan2(1.0, 2.8)                                                        # tirante del mástil a la punta
    fino.caja((-1.4, 0, 6.8), (0.08, math.hypot(2.8, 1.0), 0.08), "acero", rot_x=-a, rot_z=math.pi / 2)
    for x in (-0.6, -3.0):
        fino.cilindro((x, 0, 6.05), 0.07, 0.4, "Z", 6, "aislador")
    # hilo cortado: cae desde la punta y se dobla en el suelo (solo LOD0)
    fino.caja((-3.0, 0, 6.3 - 1.25 - 0.2), (0.03, 0.03, 2.5), "hilo")
    fino.caja((-3.0, -0.6, 3.6), (0.03, 1.2, 0.03), "hilo", rot_x=math.radians(-25))
    return t.objeto(nombre, bisel=0)
