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
import math, os
from taller import Taller

# Variantes (Eugenio, 6-nov: al menos 5): TBD_VARIANTE 0 base; 1 poste inclinado 6° (golpe); 2 ménsula doblada hacia
# abajo; 3 ménsula doble (entre dos vías); 4 hilo caído hasta el suelo y tendido en el balasto; 5 mástil corto de 6,5 m
# sin tirante (arrancado). La vía queda hacia -X.
VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)

PIEZAS = ["acero", "zapata", "aislador", "hilo", "mensula"]
COLORES = {"acero": "6c7874", "zapata": "94948c", "aislador": "48403c", "hilo": "48403c", "mensula": "6c7874"}
# LOD1 (Lucía 16:51): la ménsula salía a cuadros claro y oscuro; va como barra sólida oscura de 2 px
COLORES_LOD1 = {"mensula": "605c48"}   # 48403c en sombra caía a L* 19 (banda de la horda)
FACCION = "neutro"
# D-N80 (7-nov): nada por debajo de 48403c (283440 es de la horda; 1c200c y 303424 bajo el suelo); normales subidas
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (60, 1600), 1: (30, 300)}
UMBRAL_CONTORNO = {1: 9.0}


def _rot(c, piv, ang):
    """Gira el punto c (x, z) alrededor de piv en el plano XZ."""
    dx, dz = c[0] - piv[0], c[1] - piv[1]
    return (piv[0] + dx * math.cos(ang) - dz * math.sin(ang), piv[1] + dx * math.sin(ang) + dz * math.cos(ang))


def construir(nombre="SM_PosteCatenaria", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    g = 0.3 if lod == 0 else 0.36
    H = 6.5 if VAR == 5 else 7.5
    zm = H - 1.2                                                                     # altura de la ménsula
    t.caja((0, 0, 0.25), (0.9, 0.9, 0.5), "zapata")
    if lod == 0:   # celosía: cuatro angulares y diagonales en dos caras
        for sx in (-1, 1):
            for sy in (-1, 1):
                t.caja((sx * 0.13, sy * 0.13, 0.5 + H / 2), (0.05, 0.05, H), "acero")
        for k in range(int(H / 0.5) - 1):
            z = 0.75 + k * 0.5
            ang = math.radians(45 if k % 2 else -45)
            for sy in (-1, 1):
                t.caja((0, sy * 0.13, z), (0.36, 0.03, 0.03), "acero", rot_y=ang)
            for sx in (-1, 1):
                t.caja((sx * 0.13, 0, z), (0.03, 0.36, 0.03), "acero", rot_x=ang)
    else:
        t.caja((0, 0, 0.5 + H / 2), (g, g, H), "acero")
    gm = 0.1 if lod == 0 else 0.36
    lados = (-1, 1) if VAR == 3 else (-1,)
    for lado in lados:
        # doblada: la ménsula gira 15° hacia abajo alrededor del mástil (en Blender, rot_y positivo sube el extremo -X)
        d = math.radians(15) if VAR == 2 else 0.0
        da = -d * lado                                                               # ángulo de giro en el plano XZ
        zl = zm if lado < 0 else zm - 0.35                                            # la segunda, algo más baja (no comparten caras)
        piv = (0.0, zl)
        def caja(cx, cz, s, pieza, ry, f=t):
            x, z = _rot((lado * -cx, cz), piv, -da if lado < 0 else da) if d else (lado * -cx, cz)
            f.caja((x, 0, z), s, pieza, rot_y=(ry * -lado) + (d if d else 0.0) * (1 if lado < 0 else -1))
        caja(1.6, zl, (3.2, gm, gm), "mensula", 0.0)                                   # tubo hacia la vía
        a = math.atan2(1.1, 3.0)
        if VAR != 5:
            caja(1.5, zl + 0.55, (math.hypot(3.0, 1.1), gm * 0.8, gm * 0.8), "mensula", a)   # tirante, baja a la punta
        caja(1.9, zl - 0.55, (2.4, 0.06, 0.06), "acero", math.radians(6), fino)       # brazo de atirantado
        for x in (0.6, 3.0):
            xx, zz = _rot((lado * -x, zl - 0.25), piv, -da if lado < 0 else da) if d else (lado * -x, zl - 0.25)
            fino.cilindro((xx, 0, zz), 0.07, 0.4, "Z", 6, "aislador")
    # hilo cortado (solo LOD0): cae desde la punta; en la v4 llega al suelo y queda tendido en el balasto
    tip = _rot((-3.0, zm), (0.0, zm), math.radians(15)) if VAR == 2 else (-3.0, zm)
    if VAR == 4:
        fino.caja((tip[0], 0, tip[1] / 2), (0.03, 0.03, tip[1]), "hilo")
        fino.caja((tip[0] - 1.8, -0.4, 0.03), (3.6, 0.03, 0.03), "hilo", rot_z=math.radians(12))
    else:
        fino.caja((tip[0], 0, tip[1] - 1.25 - 0.2), (0.03, 0.03, 2.5), "hilo")
        fino.caja((tip[0], -0.6, tip[1] - 2.7), (0.03, 1.2, 0.03), "hilo", rot_x=math.radians(-25))
    o = t.objeto(nombre, bisel=0)
    if VAR == 1:   # inclinado 6° hacia el lado contrario a la vía, girando sobre la zapata
        from mathutils import Matrix
        o.data.transform(Matrix.Rotation(math.radians(-6), 4, "Y"))
        o.data.update()
    return o
