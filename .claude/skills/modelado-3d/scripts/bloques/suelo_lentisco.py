"""Lentisco (Pistacia lentiscus), mata densa del secano: OBJETO de la capa C del suelo (D-N67). Teo, 6-nov-2026.

Fotos H3 de Paula (lentisco de Mallorca; de romero no hay): mata redonda y apretada, de copa lisa por fuera, verde
oscuro con brotes más claros. Reglas de Lucía: ≤ 150 triángulos, ≤ 1,2 m de alto; solo en el descampado del este y en
el faldón, nunca en el glacis ni en los caminos de la horda. Normales hacia arriba (la misma luz que el suelo).
Colores: copa 70784c con grumos en 847c34 y brotes 88885c (nada de 50541c ni valores de la horda).
Forma: 5 o 6 grumos (icosferas de 20 caras deformadas) que se cruzan, aplastados abajo.
Variantes: 0 mata redonda de 1,1 m; 1 alargada de 1,6 × 0,9 m; 2 baja y ancha (0,7 m); 3 dos matas juntas;
4 mata ramoneada con un hueco (dos grumos menos).
"""
import math, os, random
import bmesh
from mathutils import Vector
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["copa", "grumo", "brote"]
COLORES = {"copa": "70784c", "grumo": "847c34", "brote": "88885c"}
FACCION = "neutro"
PRESUPUESTO = {0: (1, 150), 1: (1, 150)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True


def grumo(t, c, r, semilla, pieza):
    R = random.Random(semilla)
    res = bmesh.ops.create_icosphere(t.bm, subdivisions=1, radius=1.0)
    for v in res["verts"]:
        k = R.uniform(0.85, 1.12)
        z = v.co.z * r[2] * k
        v.co = Vector((v.co.x * r[0] * k + c[0], v.co.y * r[1] * k + c[1], max(z, -0.2 * r[2]) + c[2]))
    caras = list({f for v in res["verts"] for f in v.link_faces})
    t._marcar(caras, pieza)


def construir(nombre="SM_Lentisco", lod=0):
    t = Taller(PIEZAS)
    v = VAR % 5
    R = random.Random(77 + v)
    sx, sy, h = {0: (1.0, 1.0, 1.1), 1: (1.6, 0.9, 1.05), 2: (1.3, 1.2, 0.7), 3: (1.0, 0.8, 1.0), 4: (1.0, 1.0, 1.0)}[v]
    centros = [(0, 0, h * 0.45, 0.5, 0.5, h * 0.48, "copa")]
    for i in range(5):
        a = i * 2 * math.pi / 5 + R.uniform(-0.3, 0.3)
        centros.append((math.cos(a) * sx * 0.28, math.sin(a) * sy * 0.28, h * R.uniform(0.3, 0.5),
                        sx * 0.3, sy * 0.3, h * 0.3, ("grumo", "copa", "brote", "copa", "grumo")[i]))
    if v == 4:
        centros = centros[:4]
    if v == 3:   # segunda mata pequeña al lado
        centros = centros[:4] + [(0.9, 0.3, 0.32, 0.38, 0.35, 0.35, "copa"), (1.05, 0.05, 0.25, 0.25, 0.25, 0.25, "brote")]
    for k, (x, y, z, rx, ry, rz, p) in enumerate(centros[:7]):
        grumo(t, (x * sx if k == 0 else x, y * sy if k == 0 else y, z), (rx * (sx if k == 0 else 1), ry * (sy if k == 0 else 1), rz), 900 + 10 * v + k, p)
    return t.objeto(nombre + "_v%d" % v, bisel=0)
