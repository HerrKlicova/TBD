"""Poste de catenaria con el cable cortado (Lucía, ambientacion-sagunto.md, pieza 4). Teo, 6-nov-2026. Marcos lo
repite cada 50 m a lo largo de la vía.

MEDIDAS SIN VERIFICAR (típicas de un poste de perfil de acero de ADIF, a la espera de las fotos de Paula): mástil de
0,3 x 0,3 x 7,5 m sobre zapata de hormigón, ménsula de 3,2 m hacia la vía a 6,3 m con tirante, aisladores y el hilo
cortado colgando 2,5 m. Colores: acero galvanizado 6c7874, zapata 94948c, aisladores 1c200c, hilo 1c200c.
En el LOD1 el mástil y la ménsula se engordan a 2 px y el hilo y los aisladores se quitan (Bruno: < 2 px parpadea).
La vía queda hacia -X.

v2 (6-nov 17:35) con las fotos de Paula (E3_poste_catenaria_1..3, postes de ADIF): el mástil es de CELOSÍA
(cuatro angulares con diagonales en zigzag) sobre un dado de hormigón, y la ménsula es un tubo horizontal con un
tirante en diagonal que forman un triángulo, más un brazo de atirantado debajo. En el LOD1 la celosía es un mástil
macizo (las diagonales no llegan a 2 px). Medidas SIN VERIFICAR (a ojo con las fotos).
"""
import math
from taller import Taller

PIEZAS = ["acero", "zapata", "aislador", "hilo", "mensula"]
COLORES = {"acero": "6c7874", "zapata": "94948c", "aislador": "1c200c", "hilo": "1c200c", "mensula": "6c7874"}
# LOD1 (Lucía 16:51): la ménsula salía a cuadros claro y oscuro; va como barra sólida oscura de 2 px
COLORES_LOD1 = {"mensula": "303424"}
FACCION = "neutro"
PRESUPUESTO = {0: (60, 1600), 1: (30, 300)}
UMBRAL_CONTORNO = {1: 9.0}


def construir(nombre="SM_PosteCatenaria", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    g = 0.3 if lod == 0 else 0.36
    t.caja((0, 0, 0.25), (0.9, 0.9, 0.5), "zapata")
    if lod == 0:   # celosía: cuatro angulares y diagonales en dos caras
        for sx in (-1, 1):
            for sy in (-1, 1):
                t.caja((sx * 0.13, sy * 0.13, 0.5 + 3.75), (0.05, 0.05, 7.5), "acero")
        for k in range(14):
            z = 0.75 + k * 0.5
            ang = math.radians(45 if k % 2 else -45)
            for sy in (-1, 1):
                t.caja((0, sy * 0.13, z), (0.36, 0.03, 0.03), "acero", rot_y=ang)
            for sx in (-1, 1):
                t.caja((sx * 0.13, 0, z), (0.03, 0.36, 0.03), "acero", rot_x=ang)
    else:
        t.caja((0, 0, 0.5 + 3.75), (g, g, 7.5), "acero")
    gm = 0.1 if lod == 0 else 0.36
    t.caja((-1.6, 0, 6.3), (3.2, gm, gm), "mensula")                                # tubo horizontal hacia la vía (-X)
    a = math.atan2(1.1, 3.0)                                                        # tirante: del mástil a la punta
    t.caja((-1.5, 0, 6.85), (math.hypot(3.0, 1.1), gm * 0.8, gm * 0.8), "mensula", rot_y=-a)   # baja hacia la punta
    fino.caja((-1.9, 0, 5.75), (2.4, 0.06, 0.06), "acero", rot_y=math.radians(-6))  # brazo de atirantado
    for x in (-0.6, -3.0):
        fino.cilindro((x, 0, 6.05), 0.07, 0.4, "Z", 6, "aislador")
    # hilo cortado: cae desde la punta y se dobla en el suelo (solo LOD0)
    fino.caja((-3.0, 0, 6.3 - 1.25 - 0.2), (0.03, 0.03, 2.5), "hilo")
    fino.caja((-3.0, -0.6, 3.6), (0.03, 1.2, 0.03), "hilo", rot_x=math.radians(-25))
    return t.objeto(nombre, bisel=0)
