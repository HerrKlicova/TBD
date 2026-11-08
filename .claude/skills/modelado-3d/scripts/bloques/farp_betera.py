"""FARP de Bétera (punto de rearme y repostaje avanzado junto al claro de 25 × 25 m). Encargo de Lucía, 8-nov 21:15
(docs/decisiones-noche.md), con el informe de Paula betera_rearme_y_nules_viveres_v1.md. Aspecto: foto RA2 (cisterna
flexible con borde de contención, manguera tendida, sacos sueltos). Cajas: solo la forma de RA3, sin rotular ni calibre.
Teo (relevo). Medidas SIN VERIFICAR.

VAR = 5·pieza + variante (0..24). Origen en el suelo, en el centro de la pieza. Sin colisión (decorado del claro): lo
decide Álvaro; nada debe pisar el claro de 25 × 25.
Piezas:
  0 CisternaFlexible (vejiga de ~6 × 4 m dentro de un borde de contención): Llena, Media, Vacia (arrugada), SinBorde,
    Doble (dos vejigas en el mismo borde).
  1 Manguera (tendida en el suelo): Recta (10 m), Curva, EseLarga, Enrollada (en espiral), Boquerel (con el surtidor y
    la caja de filtro).
  2 Carrete de manguera: Patin (en su bastidor), Lleno, Vacio, Tumbado, Bomba (con la motobomba al lado).
  3 Sacos de arena: Sueltos, Hilera3, Hilera6, Pila, EnU (tramos en U, propuesta de Álvaro).
  4 CajasMunicion (metálicas, sin rótulos): Cerrada, Abierta, Pila4, Pale, Vacias (abiertas y volcadas).
Colores (paleta de 32; nada en la banda de la horda, nada vertical en 48403c, sin ámbar ni rojo): vejiga 6c7874,
borde 847c34, manguera 605c48, metal 94948c, bastidor 70784c, saco d4d0cc, saco2 b8b8b8, caja 70784c, tapa 50541c,
madera a86c50, interior 605c48.
"""
import math, os
from mathutils import Matrix, Vector
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 25
PIEZA, V = VAR // 5, VAR % 5
NOMBRES_P = ["CisternaFlexible", "Manguera", "Carrete", "Sacos", "CajasMunicion"]
NOMBRES_V = [["Llena", "Media", "Vacia", "SinBorde", "Doble"], ["Recta", "Curva", "EseLarga", "Enrollada", "Boquerel"],
             ["Patin", "Lleno", "Vacio", "Tumbado", "Bomba"], ["Sueltos", "Hilera3", "Hilera6", "Pila", "EnU"],
             ["Cerrada", "Abierta", "Pila4", "Pale", "Vacias"]]
PIEZAS = ["vejiga", "borde", "manguera", "metal", "bastidor", "saco", "saco2", "caja", "tapa", "madera", "interior"]
COLORES = {"vejiga": "6c7874", "borde": "847c34", "manguera": "605c48", "metal": "94948c", "bastidor": "70784c",
           "saco": "d4d0cc", "saco2": "b8b8b8", "caja": "70784c", "tapa": "50541c", "madera": "a86c50",
           "interior": "605c48"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 2500), 1: (10, 900)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_FarpBetera", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    m = 0.34
    g = lambda a: a if fino else max(a, m)
    lados = 12 if fino else 6
    p, v = PIEZA, V

    def tramo(x0, y0, x1, y1, z=0.06, pieza="manguera", d=0.12):
        L = math.hypot(x1 - x0, y1 - y0)
        t.caja(((x0 + x1) / 2, (y0 + y1) / 2, z), (L + g(d) * 0.5, g(d), g(d)), pieza,
               rot_z=math.atan2(y1 - y0, x1 - x0))

    def polilinea(P, **k):
        for a, b in zip(P, P[1:]):
            tramo(a[0], a[1], b[0], b[1], **k)

    def saco(x, y, z=0.0, rz=0.0, pieza="saco"):
        t.caja((x, y, z + 0.13), (0.6, 0.35, 0.26), pieza, rot_z=rz)

    def caja(x, y, z=0.0, rz=0.0, abierta=False, volcada=False):
        if volcada:
            t.caja((x, y, z + 0.2), (0.5, 0.4, 0.3), "caja", rot_z=rz, rot_x=1.5)
            return
        t.caja((x, y, z + 0.15), (0.5, 0.3, 0.3), "caja", rot_z=rz)
        if abierta:
            t.caja((x, y, z + 0.29), (0.44, 0.24, 0.03), "interior", rot_z=rz)
            t.caja((x - math.sin(rz) * 0.25, y + math.cos(rz) * 0.25, z + 0.38), (0.5, 0.04, 0.2), "tapa", rot_z=rz)
        else:
            t.caja((x, y, z + 0.31), (0.52, 0.32, 0.03), "tapa", rot_z=rz)
            if fino:
                t.caja((x, y, z + 0.34), (0.16, 0.04, 0.03), "metal", rot_z=rz)                  # asa

    if p == 0:                                        # CISTERNA FLEXIBLE
        def vejiga(cx, ancho, alto, vacia=False):
            if vacia:
                for k in range(5):
                    t.caja((cx - 2 + k * 1.0, (_h(k) - 0.5) * 0.4, 0.08), (1.1, ancho - 0.3 * _h(k, 1), 0.1 + 0.06 * (k % 2)),
                           "vejiga", rot_z=(_h(k, 2) - 0.5) * 0.2)
            else:
                t.caja((cx, 0, alto / 2), (5.6, ancho, alto), "vejiga")
                t.caja((cx, 0, alto + 0.04), (5.0, ancho - 0.6, 0.08), "vejiga")               # lomo redondeado
            if fino:
                t.cilindro((cx + 2.4, 0, (alto if not vacia else 0.15) + 0.08), 0.12, 0.16, "Z", 8, "metal")
        if v != 3:
            W = 9.0 if v == 4 else 7.2
            for sy in (-2.6, 2.6):                    # borde de contención (bermas)
                t.caja((0, sy, 0.25), (W, g(0.25), 0.5), "borde")
            for sx in (-W / 2, W / 2):
                t.caja((sx, 0, 0.25), (g(0.25), 5.45, 0.5), "borde")
            t.caja((0, 0, 0.02), (W, 5.2, 0.04), "borde")                                      # lona de fondo
        if v == 4:
            vejiga(-1.9, 2.0, 0.9)
            vejiga(2.6, 2.0, 0.6)
        else:
            vejiga(0, 4.0, {0: 1.2, 1: 0.7, 2: 0.2, 3: 1.1}[v], vacia=v == 2)
        polilinea([(3.0, 0), (4.5, 0.4), (6.0, 1.6), (7.5, 1.8)])
        for k in range(3 if fino else 2):                                                      # sacos sobre la berma
            saco(-3.0 + k * 3.0, -2.95, 0.0, 0.1 * k)

    elif p == 1:                                      # MANGUERA
        if v == 0:
            polilinea([(-5, 0), (5, 0)])
            for x in (-5.1, 0.0, 5.1):                 # racores
                t.caja((x, 0, 0.07), (g(0.2), g(0.18), g(0.16)), "metal")
        elif v == 1:
            P = [(4 * math.cos(a), 4 * math.sin(a) - 2) for a in [i * math.pi / 8 for i in range(9)]]
            polilinea(P)
        elif v == 2:
            P = [(-6 + i * 0.75, 1.6 * math.sin(i * 0.5)) for i in range(17)]
            polilinea(P)
        elif v == 3:                                  # en espiral (como en RA2_2)
            P = []
            for i in range(40 if fino else 24):
                a = i * (0.45 if fino else 0.75)
                r = 0.4 + a * 0.18
                P.append((r * math.cos(a), r * math.sin(a)))
            polilinea(P)
        else:                                         # con boquerel y caja de filtro
            polilinea([(-5, 0.5), (-2, 0.2), (0.5, 0.6), (2.5, 0.0)])
            t.caja((-5.5, 0.5, 0.35), (0.9, 0.7, 0.7), "bastidor")                           # filtro/separador
            t.caja((-5.5, 0.5, 0.75), (0.95, 0.75, 0.08), "metal")
            t.caja((2.75, 0.0, 0.1), (0.5, g(0.12), 0.18), "metal")                           # boquerel
            for k in range(2):
                saco(-1.0 + k * 2.0, 0.85, 0.0, 0.3, "saco2")                                 # sacos que la sujetan

    elif p == 2:                                      # CARRETE
        def carrete(cx, cy, lleno=True, tumbado=False):
            if tumbado:
                t.cilindro((cx, cy, 0.12), 0.65, 0.12, "Z", lados, "metal")
                t.cilindro((cx, cy, 0.82), 0.65, 0.12, "Z", lados, "metal")
                t.cilindro((cx, cy, 0.47), 0.42 if lleno else 0.15, 0.6, "Z", lados, "manguera" if lleno else "metal")
                return
            t.caja((cx, cy, 0.08), (1.4, 1.1, 0.16), "bastidor")                              # patín
            for sx in (-0.55, 0.55):
                t.caja((cx + sx, cy, 0.55), (g(0.08), 0.12, 0.9), "bastidor")
            for sx in (-0.4, 0.4):
                t.cilindro((cx + sx, cy, 0.85), 0.62, g(0.05), "X", lados, "metal")
            t.cilindro((cx, cy, 0.85), 0.45 if lleno else 0.14, 0.75, "X", lados, "manguera" if lleno else "metal")
            if fino:
                t.caja((cx + 0.7, cy + 0.3, 0.85), (0.05, 0.4, 0.05), "metal")                 # manivela
        if v == 3:
            carrete(0, 0, tumbado=True)
            polilinea([(0.6, 0.2), (2.0, 0.8), (3.5, 0.6)])
        else:
            carrete(0, 0, lleno=v != 2)
            if v in (1, 4):
                polilinea([(0, -0.5), (0.2, -1.6), (1.4, -2.6), (3.0, -2.8)])
            if v == 0:
                for k in range(2):
                    saco(-1.0 + k * 0.7, -0.9, 0.0, 0.2)
            if v == 4:                                # motobomba
                t.caja((1.8, 0.4, 0.35), (1.0, 0.7, 0.6), "bastidor")
                t.cilindro((1.8, 0.4, 0.75), 0.25, 0.2, "Z", 8 if fino else 6, "metal")
                t.caja((1.8, 0.4, 0.06), (1.2, 0.85, 0.12), "metal")

    elif p == 3:                                      # SACOS DE ARENA (blancos, como en RA1/RA2)
        if v == 0:
            for k in range(6):
                saco(-2 + _h(k) * 4, -1 + _h(k, 1) * 2, 0.0, _h(k, 2) * 3, ("saco", "saco2")[k % 2])
        elif v in (1, 2):
            n = 3 if v == 1 else 6
            for k in range(n):
                saco(-(n - 1) * 0.33 + k * 0.66, 0, 0.0, (_h(k) - 0.5) * 0.15, ("saco", "saco2")[k % 2])
        elif v == 3:
            for f in range(3):
                for k in range(3 - f):
                    saco(-0.66 + k * 0.66 + f * 0.33, 0, f * 0.25, (_h(k + f) - 0.5) * 0.1, ("saco", "saco2")[(k + f) % 2])
        else:
            for k in range(5):                         # U abierta hacia +Y: fondo de 3,3 m y brazos de 2 m
                saco(-1.32 + k * 0.66, 0, 0.0, 0.0, ("saco", "saco2")[k % 2])
            for sx in (-1.75, 1.75):
                for k in range(3):
                    saco(sx, 0.5 + k * 0.66, 0.0, math.pi / 2, ("saco", "saco2")[(k + 1) % 2])

    else:                                             # CAJAS DE MUNICIÓN (sin rótulos ni calibre)
        if v == 0:
            caja(0, 0)
        elif v == 1:
            caja(0, 0, abierta=True)
            caja(0.7, 0.1, rz=0.2)
        elif v == 2:
            for k in range(4):
                caja((k % 2) * 0.55 - 0.27, 0, (k // 2) * 0.33, 0.0)
        elif v == 3:
            t.caja((0, 0, 0.07), (1.2, 0.8, 0.14), "madera")
            for f in range(2):
                for k in range(4):
                    caja(-0.3 + (k % 2) * 0.6, -0.17 + (k // 2) * 0.34, 0.14 + f * 0.33)
        else:
            caja(-0.5, 0, abierta=True, rz=0.3)
            caja(0.3, 0.3, volcada=True, rz=1.0)
            caja(0.6, -0.4, abierta=True, rz=-0.5)
            t.caja((1.0, 0.5, 0.02), (0.5, 0.3, 0.03), "tapa", rot_z=0.8)
    return t.objeto(nombre, bisel=0)
