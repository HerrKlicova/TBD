"""Silueta baja del cerro y el castillo de Sagunto, para el fondo (Álvaro, sagunto-nivel.md §3 «Fondo» y §5). Teo, 6-nov-2026.

Fotos F5 de Paula (Commons): el cerro alargado con las murallas corriendo por toda la cresta, la ciudad a sus pies y, en
lo alto, la plaza de armas con una torre. Es un telón de fondo sin simulación en el borde norte del faldón: pocas caras,
silueta clara.
MEDIDAS SIN VERIFICAR (estimadas sobre las fotos y por lo que se sabe del sitio, sin un plano): cerro de unos 900 m de
largo y 200 m de fondo, cresta a unos 120 m sobre la llanura, murallas de 10 m con torres cuadradas y la torre mayor en
la parte alta. Murallas y torres EXAGERADAS ×1,8 (18 m y 26–42 m) para que se lean en la silueta a 1 km.
Marcos puede escalarlo para el encuadre.
Colores: cerro de matorral (arriba 70784c, laderas 50541c por valor partido), roca 6c7874, murallas y torres de piedra
b4a080 con su sombra 845c28. Una sola pieza: el castillo es único y no lleva variantes.
Se construye con el cerro a lo largo de Y y al final se gira 90° para que corra a lo largo de X (paralelo a la vía).
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["cerro", "roca", "muralla", "sombra"]
COLORES = {"cerro": "70784c", "roca": "6c7874", "muralla": "b4a080", "sombra": "845c28"}
VALOR_PIEZAS = {"cerro": ("70784c", "50541c")}
FACCION = "neutro"
PRESUPUESTO = {0: (60, 1500), 1: (40, 900)}
CRESTA = [(-450, 0), (-380, 28), (-300, 62), (-220, 92), (-150, 104), (-90, 120), (-30, 112), (40, 100), (120, 108),
          (200, 90), (280, 66), (360, 32), (450, 0)]


def construir(nombre="SM_CastilloSagunto", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    P = CRESTA[:] + [(450, -2), (-450, -2)]
    t.perfil_extruido(P, lambda z: max(10.0, 105.0 * (1 - max(z, 0) / 135.0)), cortes_z=(30, 60, 90), pieza="cerro")
    # murallas por la cresta (entre el 2.º y el penúltimo punto) y torres en los quiebros
    puntos = CRESTA[1:-1]
    for (ya, za), (yb, zb) in zip(puntos, puntos[1:]):
        L = math.hypot(yb - ya, zb - za)
        ang = math.atan2(zb - za, yb - ya)
        t.caja((0, (ya + yb) / 2, (za + zb) / 2 + 9), (7.0, L + 6, 18.0), "muralla", rot_x=ang)
    for i, (y, z) in enumerate(puntos[1:-1], 1):
        mayor = (y, z) == (-90, 120)
        s = 26.0 if mayor else 16.0
        h = 42.0 if mayor else 26.0
        (t if mayor or i % 2 == 0 else fino).caja((0, y, z + h / 2 + 2), (s, s, h), "muralla")
        if mayor:   # plaza de armas: recinto alto alrededor de la torre mayor
            t.caja((0, y + 24, z + 8), (44.0, 80.0, 14.0), "muralla")
    # roca asomando en las laderas (LOD0) y la sombra de las murallas en la ladera sur
    for y, z in ((-260, 55), (-40, 80), (170, 70), (300, 40)):
        fino.caja((-max(10.0, 105.0 * (1 - z / 135.0)) + 3, y, z - 8), (8.0, 30.0, 16.0), "roca")
    o = t.objeto(nombre, bisel=0)
    from mathutils import Matrix
    o.data.transform(Matrix.Rotation(math.radians(90), 4, "Z"))
    return o
