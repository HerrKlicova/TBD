"""Piedras del suelo de Sagunto (capa C, D-N67). MUESTRA para Eugenio. Teo, 6-nov-2026.

SIN FOTO todavía (Paula buscará escombro de obra y piedra de solar). Reglas de Lucía (§3, §4): ≤ 150 triángulos, en
94948c y 88885c, con la cara de sombra en 6c7874 (Lucía, §8: el 8c5048 se leía rojizo, como sangre); sin valor partido; nada de valores de la horda.
Cada piedra es una icosfera de 20 caras con los vértices movidos (semilla fija) y aplastada; las caras que miran
arriba van en 94948c, los costados en 88885c y las de abajo, junto al suelo, en 6c7874.
Variantes (TBD_VARIANTE): 0 piedra suelta de 0,5 m; 1 grupo de tres cantos de 0,15 a 0,35 m; 2 cantos de arenisca
ocre (foto H1_2; b4a080 arriba y 88885c en sombra), SOLO para la huerta, donde se leen sobre la tierra a86c50 (Lucía).
La gris es la principal: el cerro de Sagunto es caliza gris. 3 losa plana de caliza medio enterrada (1 m, 0,15 m de
alto); 4 piedra grande partida en dos mitades separadas (0,7 m).
"""
import math, os, random
import bmesh
from mathutils import Vector
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["arriba", "costado", "sombra"]
COLORES = ({"arriba": "b4a080", "costado": "88885c", "sombra": "88885c"} if VAR % 5 == 2   # arenisca ocre, solo huerta
           else {"arriba": "94948c", "costado": "88885c", "sombra": "6c7874"})
FACCION = "neutro"
PRESUPUESTO = {0: (1, 150), 1: (1, 150)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True   # normales horneadas hacia arriba (Lucía, §8): la misma luz que el suelo


def canto(t, c, r, aplastado, semilla):
    R = random.Random(semilla)
    res = bmesh.ops.create_icosphere(t.bm, subdivisions=1, radius=1.0)
    rx, ry = r * R.uniform(0.85, 1.2), r * R.uniform(0.7, 1.0)
    rot = R.uniform(0, math.pi)
    for v in res["verts"]:
        k = R.uniform(0.8, 1.15)
        x, y, z = v.co.x * rx * k, v.co.y * ry * k, v.co.z * r * aplastado * k
        z = max(z, -0.15 * r)                                    # base plana, medio enterrada
        v.co = Vector((x * math.cos(rot) - y * math.sin(rot), x * math.sin(rot) + y * math.cos(rot), z + c[2])) + Vector((c[0], c[1], 0))
    caras = list({f for v in res["verts"] for f in v.link_faces})
    for f in caras:
        f.normal_update()
        n = f.normal
        f.material_index = PIEZAS.index("arriba" if n.z > 0.55 else ("costado" if n.z > -0.1 and (n.x + n.y) > -0.6 else "sombra"))
    return caras


def piedra(t, variante, cx=0.0, cy=0.0, semilla=1, escala=1.0):
    if variante == 0:
        canto(t, (cx, cy, 0.0), 0.25 * escala, 0.6, semilla)
    elif variante == 2:
        for i, (dx, dy, r) in enumerate(((0.0, 0.0, 0.3), (0.45, -0.1, 0.16))):
            canto(t, (cx + dx * escala, cy + dy * escala, 0.0), r * escala, 0.55, semilla * 17 + i)
    elif variante == 3:
        res = canto(t, (cx, cy, 0.0), 0.5 * escala, 0.3, semilla)
    elif variante == 4:
        canto(t, (cx - 0.22 * escala, cy, 0.0), 0.3 * escala, 0.8, semilla)
        canto(t, (cx + 0.24 * escala, cy + 0.05 * escala, 0.0), 0.26 * escala, 0.7, semilla + 1)
    else:
        for i, (dx, dy, r) in enumerate(((0.0, 0.0, 0.17), (0.3, 0.12, 0.11), (-0.12, 0.28, 0.08))):
            canto(t, (cx + dx * escala, cy + dy * escala, 0.0), r * escala, 0.65, semilla * 13 + i)


def construir(nombre="SM_Piedra", lod=0):
    t = Taller(PIEZAS)
    piedra(t, VAR % 5, semilla=5)
    return t.objeto(nombre + "_v%d" % (VAR % 5), bisel=0)
