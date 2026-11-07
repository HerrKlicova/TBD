"""Castillo de Sagunto para el fondo, REDUCIDO (D-N68 de Lucía, 6-nov 18:36). Teo, 6-nov-2026.

Fotos F5 de Paula (Commons): el cerro visto desde la llanura (dos lomas con un collado, murallas por la cresta, torres
cuadradas) y a contraluz (plaza de armas con la torre, lienzos almenados que bajan por la ladera).
D-N68: a escala real (900 m) se leía como un muro oliva plano. Como la cámara es ortográfica, alejarlo no lo encoge:
solo lo sube en pantalla. Es un telón de fondo, así que se hace una EXCEPCIÓN a la escala real: solo el tramo central
del real (plaza de armas y ciudadela), unos 400 m de ancho, con el cerro a unos 80 m.
Arriba, silueta almenada con 4 torres y lienzos de muralla en piedra b4a080, con las caras en sombra (las laterales y la
trasera) en 94948c. Laderas de matorral: arriba 88885c, costados 847c34 (valor partido), y manchas de roca 94948c.
Nada de oliva plano ni 50541c. Fondo sin contorno negro.
MEDIDAS: 400 × 150 m de planta, cresta de 80 m (D-N68). Murallas de 14 m con merlones de 4 m, torres de 22 a 34 m:
EXAGERADAS para que la silueta se lea a 1 km; SIN VERIFICAR contra un plano.
Posición (Lucía): hacia (130; 330) o algo más al norte, siempre que entre con la cámara alejada. La pone Marcos.
Se construye con el cerro a lo largo de Y y al final se gira −90° para que corra a lo largo de X. El FRENTE (la cara
con las rocas y las caras claras de las torres, la que tiene que ver la cámara) queda en −Y de Blender, como en todos
los modelos: en Unity es su +Z. Con la cámara mirando al norte, Marcos lo gira 180° para que el frente mire al sur, al
fuerte, igual que el castillo anterior (misma convención).
Detrás va la sierra baja repetible (sierra_fondo.py).
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["cerro", "roca", "muralla", "sombra"]
COLORES = {"cerro": "88885c", "roca": "94948c", "muralla": "b4a080", "sombra": "94948c"}
VALOR_PIEZAS = {"cerro": ("88885c", "847c34")}
FACCION = "neutro"
PRESUPUESTO = {0: (80, 2500), 1: (60, 1600)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}   # telón de fondo: sin contorno negro por dentro
# dos lomas con collado (foto 1): la alta, a la izquierda, con la plaza de armas y la torre mayor
CRESTA = [(-200, 0), (-150, 34), (-130, 62), (-95, 78), (-55, 80), (-20, 70), (15, 60), (55, 66), (100, 72),
          (140, 58), (200, 0)]


def semiancho(z):
    return max(8.0, 75.0 * (1 - max(z, 0) / 92.0))


def construir(nombre="SM_CastilloSagunto", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    P = CRESTA[:] + [(200, -2), (-200, -2)]
    t.perfil_extruido(P, semiancho, cortes_z=(25, 50, 70), pieza="cerro")
    puntos = CRESTA[2:-1]
    hm = 14.0
    for (ya, za), (yb, zb) in zip(puntos, puntos[1:]):            # lienzos de muralla siguiendo la cresta
        L = math.hypot(yb - ya, zb - za)
        ang = math.atan2(zb - za, yb - ya)
        yc, zc = (ya + yb) / 2, (za + zb) / 2
        t.caja((0, yc, zc + hm / 2 - 2), (8.0, L + 4, hm), "muralla", rot_x=ang)
        t.caja((-4.1, yc, zc + hm / 2 - 2), (0.4, L + 4, hm), "sombra", rot_x=ang)        # trasera en sombra
        n = max(1, int(L // 8))                                                           # merlones cada 8 m
        for k in range(n):
            f = (k + 0.5) / n
            y, z = ya + (yb - ya) * f, za + (zb - za) * f
            t.caja((2.0, y, z + hm - 2 + 2.0), (4.0, 4.0, 4.0), "muralla")
    torres = [(-75, 34.0, 20.0), (-20, 24.0, 14.0), (55, 26.0, 14.0), (120, 22.0, 13.0)]  # (y, alto, lado)
    for y, h, s in torres:
        z = _z_cresta(y)
        _torre(t, fino, y, z - 2, h, s)
    # plaza de armas: recinto alto alrededor de la torre mayor
    t.caja((0, -75, _z_cresta(-75) + 6), (40.0, 70.0, 12.0), "muralla")
    t.caja((-20.1, -75, _z_cresta(-75) + 6), (0.4, 70.0, 12.0), "sombra")
    # manchas de roca en la ladera que ve la cámara (+X)
    for y, z, w in ((-120, 45, 22), (-100, 58, 16), (-40, 45, 28), (30, 40, 20), (90, 50, 18), (125, 40, 14)):
        x = semiancho(z) + 1.5
        t.caja((x - 1.0 if lod else x, y, z), (3.0, w, w * 0.6), "roca", rot_y=-0.55)
    for v in t.bm.verts:                       # los extremos del cerro se estrechan en planta: que no acabe en tabla
        ay = abs(v.co.y)
        if ay > 110:
            v.co.x *= max(0.3, 1 - (ay - 110) / 90 * 0.7)
    o = t.objeto(nombre, bisel=0)
    from mathutils import Matrix
    o.data.transform(Matrix.Rotation(math.radians(-90), 4, "Z"))   # +X de construcción -> −Y (el frente)
    return o


def _z_cresta(y):
    for (ya, za), (yb, zb) in zip(CRESTA, CRESTA[1:]):
        if ya <= y <= yb:
            return za + (zb - za) * (y - ya) / (yb - ya)
    return 0.0


def _torre(t, fino, y, z, h, s):
    """Torre cuadrada almenada: cara a la cámara (+X) en piedra clara, laterales y trasera en sombra."""
    t.caja((0, y, z + h / 2), (s, s, h), "muralla")
    t.caja((0, y + s / 2 + 0.15, z + h / 2), (s - 0.1, 0.4, h - 0.1), "sombra")          # lateral en sombra
    t.caja((-s / 2 - 0.15, y, z + h / 2), (0.4, s - 0.3, h - 0.1), "sombra")             # trasera
    m = s / 4
    for k in (-1.5, 0.5):                                                    # merlones de la cara a la cámara
        t.caja((s / 2 - m / 2, y + k * m + m / 2, z + h + m / 2), (m, m, m), "muralla")
    fino.caja((s / 2 + 0.1, y, z + h * 0.6), (0.3, 1.2, 2.4), "sombra")    # saetera
