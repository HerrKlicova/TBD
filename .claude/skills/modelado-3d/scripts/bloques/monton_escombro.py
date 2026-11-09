"""Montones de escombro para los solares del pueblo de Sagunto (lista de cajas de Bruno, 9-nov 22:30: 8 «Escombro» en los
solares 5.º y 9.º de la hilera delantera, Z 100–104; cajas de 2,2 × 1,8 m y 0,8–1,4 m de alto). Teo (4), 9-nov.

No es la capa de suelo (suelo_escombros, ≤ 0,3 m): son montones que tapan, de casa derribada. Fotos H5 de Paula (escombro
de obra en un descampado). MEDIDAS SIN VERIFICAR. Planta de unos 2,2 × 1,8 m, origen en el suelo, en el centro.
Colores de suelo_escombros (Lucía): cascote 94948c arriba y 88885c de costado, sombra 6c7874, ladrillo a86c50 (no es de la
familia roja); madera 845c28, hierro 605c48 y saca de obra b8b8b8 (pequeña: el d4d0cc es de selección). Nada en la banda.
Variantes (TBD_VARIANTE % 5): 0 cascotes de hormigón (1,0 m); 1 ladrillo y cascote (0,8 m); 2 losa partida con ferralla
(1,2 m); 3 tablones y una puerta rota encima (1,1 m); 4 dos sacas de obra llenas junto al montón (1,4 m).
"""
import math, os
from taller import Taller

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Cascotes", "Ladrillo", "Ferralla", "Tablones", "Sacas"]
PIEZAS = ["arriba", "costado", "sombra", "ladrillo", "madera", "hierro", "saca"]
COLORES = {"arriba": "94948c", "costado": "88885c", "sombra": "6c7874", "ladrillo": "a86c50", "madera": "845c28",
           "hierro": "605c48", "saca": "b8b8b8"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.7
PRESUPUESTO = {0: (30, 700), 1: (15, 300)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
ALTO = [1.0, 0.8, 1.2, 1.1, 1.4][V]


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_MontonEscombro", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    a = ALTO if V != 4 else 0.9
    # el montón: un cono bajo de 8 lados, achatado (cascote), con la falda en sombra
    t.cilindro((0, 0, a * 0.5), 1.05, a, "Z", 8 if fino else 6, "costado", r2=0.25)
    t.cilindro((0, 0, 0.04), 1.15, 0.08, "Z", 8 if fino else 6, "sombra")
    piedra = "ladrillo" if V == 1 else "arriba"
    n = 14 if fino else 5
    for k in range(n):
        ang, rr = _h(k, 1) * 6.283, 0.25 + _h(k, 2) * 0.7
        z = a * (1 - rr / 1.05) + 0.05
        s = 0.18 + _h(k, 3) * 0.3
        t.caja((math.cos(ang) * rr, math.sin(ang) * rr * 0.85, z), (s, s * 0.8, s * 0.6),
               piedra if k % 3 else "arriba", rot_z=_h(k, 4) * 3, rot_x=(_h(k, 5) - 0.5) * 0.6)
    if V == 1 and fino:
        for k in range(4):                                                  # ladrillos sueltos al pie
            t.caja((0.9 + 0.12 * k, -0.6 + 0.3 * k, 0.04), (0.24, 0.12, 0.07), "ladrillo", rot_z=k)
    if V == 2:                                                              # losa partida y ferralla
        t.caja((0.15, -0.1, a * 0.75), (1.3, 0.9, 0.15), "arriba", rot_x=0.35, rot_z=0.4)
        for k in range(4 if fino else 2):
            t.caja((0.2 + 0.18 * k, 0.1, a + 0.15), (0.03, 0.03, 0.8), "hierro", rot_x=0.3 + 0.15 * k, rot_y=0.2 * k)
    if V == 3:                                                              # tablones y una puerta rota
        for k in range(3):
            t.caja((-0.2 + 0.25 * k, 0.1 * k, a * 0.7 + 0.08 * k), (0.15, 1.9, 0.04), "madera",
                   rot_z=0.5 + 0.4 * k, rot_x=0.25)
        t.caja((0.3, -0.3, a * 0.55), (0.8, 0.04, 1.6), "madera", rot_x=1.0, rot_z=0.3)
    if V == 4:                                                              # dos sacas de obra (1 m³)
        for k, (x, y) in enumerate(((0.55, 0.85), (-0.45, 0.95))):
            t.caja((x, y, 0.45), (0.9, 0.9, 0.9), "saca", rot_z=0.2 * k)
            t.caja((x, y, 0.92), (0.8, 0.8, 0.06), "arriba", rot_z=0.2 * k)
    return t.objeto(nombre, bisel=0)
