"""Traviesas sueltas (tablón del MVP, «si sobra tiempo»; Lucía 8-nov 18:00): acopios y restos de vía para la playa
de vías, junto a la vía cortada y en las obras. Teo (3), 8-nov.

Traviesa monobloque de hormigón de 2,6 × 0,3 × 0,22 m (94948c) y de madera de 2,6 × 0,24 × 0,15 m (845c28); carril de
0,07 × 0,15 m (6c7874). Nada rojo ni en la banda de la horda (en el LOD1 nada baja de 0,34 m: se juntan las hiladas).
Origen en el suelo, en el centro del montón. Medidas SIN VERIFICAR (genéricas).
Variantes (TBD_VARIANTE % 5): 0 acopio ordenado de hormigón (4 hiladas de 6 con calzos); 1 acopio de madera; 2
desparramadas por el suelo; 3 acopio con dos carriles encima; 4 rotas y quemadas (madera en hollín 605c48 y trozos).
"""
import math, os
from taller import Taller

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Hormigon", "Madera", "Desparramadas", "ConCarriles", "Quemadas"]
PIEZAS = ["hormigon", "madera", "carril", "calzo", "hollin"]
COLORES = {"hormigon": "94948c", "madera": "845c28", "carril": "6c7874", "calzo": "845c28", "hollin": "605c48"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (10, 1500), 1: (6, 400)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_TraviesasSueltas", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    v = V
    madera = v in (1, 4)
    pieza = "hollin" if v == 4 else ("madera" if madera else "hormigon")
    ly, lz = (0.24, 0.15) if madera else (0.3, 0.22)
    if v == 2:                                       # desparramadas
        for k in range(9):
            m = k % 3 == 0
            t.caja((-2.5 + _h(k) * 5, -2 + _h(k, 1) * 4, (0.15 if m else 0.22) / 2), (2.6, 0.24 if m else 0.3,
                   0.15 if m else 0.22), "madera" if m else "hormigon", rot_z=_h(k, 2) * 3)
        return t.objeto(nombre, bisel=0)
    hiladas = 3 if v == 4 else 4
    n = 6
    z = 0.0
    for f in range(hiladas):
        if fino:
            for k in range(n):
                if v == 4 and f == hiladas - 1 and k in (1, 4):
                    continue
                y = (k - (n - 1) / 2) * (ly + 0.08)
                t.caja((0, y, z + lz / 2), (2.6, ly, lz), pieza, rot_z=(_h(k + f * 7, 3) - 0.5) * 0.04)
        else:                                        # LOD1: la hilada entera como un bloque con las juntas marcadas
            t.caja((0, 0, z + lz / 2), (2.6, n * (ly + 0.08) - 0.08, lz), pieza)
        z += lz
        if f < hiladas - 1:                          # calzos de madera entre hiladas
            for x in (-0.8, 0.8):
                t.caja((x, 0, z + 0.03), (0.1, n * (ly + 0.08) + 0.1, 0.06), "calzo")
            z += 0.06
    if v == 3:                                       # dos carriles encima, a lo largo
        for y in (-0.35, 0.35):
            t.caja((0.3, y, z + 0.075), (6.0, 0.07 if fino else 0.34, 0.15), "carril", rot_z=0.03)
    if v == 4:                                       # trozos quemados al lado
        for k in range(3):
            t.caja((1.8 + _h(k, 4), -1.2 + k * 1.0, 0.08), (1.0, 0.24, 0.15), "hollin", rot_z=_h(k, 5) * 2)
    return t.objeto(nombre, bisel=0)
