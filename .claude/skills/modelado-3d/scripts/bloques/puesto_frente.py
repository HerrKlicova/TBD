"""Puesto de frente del mundo persistente: la base pequeña que ocupa un pelotón en cada sitio del mapa (coordinador,
7-nov 01:45). Teo, 7-nov.

Foto de Paula: K3_control_1.jpg (referencias-reales/entorno/, puesto de control con parapeto de sacos terreros apilados
a soga, de unos 1,2 m). Forma de la tienda confirmada con K6_tienda_2 (modulares de pared baja y cumbrera) y la torre
de luz con K7_foco_2 (Magnum en remolque). La lona de las fotos es beige, pero queda verde por legibilidad (Lucía,
7-nov): color y cotas SIN VERIFICAR para un despliegue concreto. Medidas
típicas: parapeto de sacos de 1,2 m de alto y 0,9 m de grueso, sacos de 0,85 × 0,3 m; tienda modular de pelotón de
6 × 4,5 m con 1,75 m de pared y 2,9 m de cumbrera; mástil de 7 m; torre de luz en remolque de 7,5 m.
Colores: sacos 88885c (como el muro de Marcos), HESCO 70784c con el relleno b4a080, tienda 50541c con el techo en 70784c
(valor partido), mástil y remolque 6c7874, cabezas de los focos 48403c, red de camuflaje 50541c, cajas 605c48, palés
b4a080 y los focos en f0dc9c (hijo «Luces», como las balizas: nada de rojo ni ámbar).
Jerarquía: SM_…_LODn con un hijo «Luces» (los cristales de los focos, transformación identidad) para la emisión
nocturna. Origen en el suelo, en el centro del puesto. Ocupa unos 15 × 15 m.
Variantes (TBD_VARIANTE % 5): 0 anillo octogonal de sacos con tienda y mástil con foco; 1 parapeto en U con tienda
grande y torre de luz en remolque; 2 recinto cuadrado de HESCO con tienda y mástil de antena con foco; 3 sacos con la
tienda bajo red de camuflaje y foco en trípode; 4 dos tiendas pequeñas y un nido de ametralladora de sacos con techo.
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = ["sacos", "hesco", "relleno", "tienda", "puerta", "metal", "cabeza", "red", "caja", "pale", "banderin", "luz"]
COLORES = {"sacos": "88885c", "hesco": "70784c", "relleno": "b4a080", "tienda": "50541c", "puerta": "605c48",
           "metal": "6c7874", "cabeza": "48403c", "red": "50541c", "caja": "605c48", "pale": "b4a080",
           "banderin": "70784c", "luz": "f0dc9c"}
VALOR_PIEZAS = {"tienda": ("847c34", "70784c")}   # paredes en 70784c con las normales a 1,0: la sombra fuera de la banda de la horda (Lucía, 7-nov)
FACCION = "propio"
PRESUPUESTO = {0: (300, 6000), 1: (150, 2500)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PARTES = ["puesto", "Luces"]
PIV = {"Luces": (0, 0, 0)}
CASA = [(-1.0, -1.0), (1.0, -1.0), (1.0, 0.2), (0.0, 1.0), (-1.0, 0.2)]   # sección de la tienda (x, z)


def _sacos(m, p0, p1, fino, alto=1.2, grueso=0.9):
    """Parapeto de sacos de p0 a p1 (en planta). LOD0: hiladas de sacos a soga, trabadas; LOD1: un bloque con la
    hilada de arriba (para que se lea la textura de sacos a tamaño de juego)."""
    (x0, y0), (x1, y1) = p0, p1
    L = math.hypot(x1 - x0, y1 - y0)
    rz = math.atan2(y1 - y0, x1 - x0)
    ux, uy = (x1 - x0) / L, (y1 - y0) / L
    if not fino:
        m.caja(((x0 + x1) / 2, (y0 + y1) / 2, (alto - 0.3) / 2), (L, grueso, alto - 0.3), "sacos", rot_z=rz)
        n = max(1, round(L / 1.2))
        for k in range(n):
            t = (k + 0.5) * L / n
            m.caja((x0 + ux * t, y0 + uy * t, alto - 0.14), (L / n - 0.06, grueso - 0.1, 0.26), "sacos", rot_z=rz)
        return
    filas = round(alto / 0.3)
    for f in range(filas):
        h = 0.3
        z = f * h + h / 2
        off = 0.425 if f % 2 else 0.0
        n = max(1, int((L - off) / 0.85))
        lb = (L - off) / n
        for k in range(n):
            t = off + (k + 0.5) * lb
            m.caja((x0 + ux * t, y0 + uy * t, z), (lb - 0.04, grueso - (0.06 if f % 2 else 0.0), h - 0.03), "sacos",
                   rot_z=rz)


def _hesco(m, p0, p1, fino):
    (x0, y0), (x1, y1) = p0, p1
    L = math.hypot(x1 - x0, y1 - y0)
    rz = math.atan2(y1 - y0, x1 - x0)
    ux, uy = (x1 - x0) / L, (y1 - y0) / L
    n = max(1, round(L / 1.1))
    for k in range(n):
        t = (k + 0.5) * L / n
        c = (x0 + ux * t, y0 + uy * t)
        m.caja((c[0], c[1], 0.7), (L / n - 0.04, 1.06, 1.4), "hesco", rot_z=rz)
        m.caja((c[0], c[1], 1.415), (L / n - 0.14, 0.96, 0.03), "relleno", rot_z=rz)


def _tienda(m, largo, ancho, fino):
    """Tienda de cumbrera a lo largo de Y, centrada en el origen, con la puerta oscura en el frente (−Y)."""
    m.loft(CASA, [(-largo / 2, ancho / 2, 1.45, 1.45), (largo / 2, ancho / 2, 1.45, 1.45)], "tienda")
    m.caja((0, -largo / 2 - 0.02, 0.85), (1.1, 0.04, 1.6), "puerta")
    if fino:                                            # vientos y piquetas de los lados largos
        for s in (-1, 1):
            for y in (-largo / 3, largo / 3):
                m.barra((s * ancho / 2, y, 1.7), (s * (ancho / 2 + 0.9), y, 0.02), 0.03, 0.03, "metal")


def _mover(m, vs, dx, dy):
    for v in vs:
        v.co.x += dx
        v.co.y += dy


def _poner(m, fn, dx, dy, *a, **k):
    antes = set(m.bm.verts)
    fn(m, *a, **k)
    _mover(m, set(m.bm.verts) - antes, dx, dy)


def _mastil(m, x, y, alto, fino, luces, antena=False):
    r = 0.06 if fino else 0.12                         # ≥ 0,24 m en el LOD1 (Lucía: 2 px a ortho 26)
    m.cilindro((x, y, alto / 2), r, alto, "Z", 6, "metal")
    m.caja((x, y, 0.1), (0.6, 0.6, 0.2), "metal")      # base lastrada
    if fino:
        for a in (0, 2.1, 4.2):
            m.barra((x, y, alto * 0.6), (x + 2.2 * math.cos(a), y + 2.2 * math.sin(a), 0.02), 0.025, 0.025, "metal")
    m.caja((x + 0.2, y - 0.5, alto - 0.6), (0.03, 0.9, 0.5), "banderin")         # banderín del pelotón
    if antena:
        m.cilindro((x, y, alto + 1.0), 0.03 if fino else 0.08, 2.0, "Z", 4, "metal")
    # foco orientado hacia −Y (la entrada) y un poco hacia abajo
    m.caja((x, y - 0.25, alto - 1.1), (0.5, 0.3, 0.4), "cabeza")
    luces.append(((x, y - 0.41, alto - 1.1), (0.42, 0.02, 0.32)))


def _cajas(m, x, y, fino):
    m.caja((x, y, 0.07), (1.2, 1.0, 0.12), "pale")
    for dx in (-0.3, 0.3):
        for dz in ((0, 0.32) if fino else (0,)):
            m.caja((x + dx, y, 0.29 + dz), (0.56, 0.9, 0.3), "caja")


def construir(nombre="SM_PuestoFrente", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    a = ASPECTO
    luces = []
    if a == 0:                                         # anillo octogonal de sacos con la entrada a −Y
        R = 6.8
        pts = [(R * math.cos(math.radians(-90 + 22.5 + 45 * k)), R * math.sin(math.radians(-90 + 22.5 + 45 * k)))
               for k in range(8)]
        for k in range(8):
            p0, p1 = pts[k], pts[(k + 1) % 8]
            if k == 7:                                 # tramo de la entrada (sur): dos medios tramos
                continue
            ux, uy = p1[0] - p0[0], p1[1] - p0[1]
            L = math.hypot(ux, uy)
            e = 0.4 / L                                # alargado 0,4 m por cada punta: las esquinas se solapan
            _sacos(m, (p0[0] - ux * e, p0[1] - uy * e), (p1[0] + ux * e, p1[1] + uy * e), fino)
        _sacos(m, (pts[7][0] - 0.4, pts[7][1] + 0.15), (-1.3, pts[7][1]), fino)           # entrada de 2,6 m en el tramo sur
        _sacos(m, (1.3, pts[0][1]), (pts[0][0] + 0.4, pts[0][1] + 0.15), fino)
        _poner(m, _tienda, -1.2, 1.6, 6.0, 4.5, fino)
        _mastil(m, 3.6, -2.2, 7.0, fino, luces)
        _cajas(m, 3.6, 2.6, fino)
    elif a == 1:                                       # parapeto en U abierto a −Y, torre de luz en remolque
        _sacos(m, (-7, -5.5), (-7, 6.5), fino)
        _sacos(m, (-6.5, 7), (6.5, 7), fino)
        _sacos(m, (7, 6.5), (7, -5.5), fino)
        _sacos(m, (-7, -6.2), (-3.5, -6.2), fino)
        _poner(m, _tienda, -2.0, 2.0, 8.0, 5.0, fino)
        # torre de luz en remolque: chasis, mástil y barra de 4 focos
        m.caja((4.2, -2.5, 0.75), (1.4, 2.4, 0.9), "metal")
        for s in (-1, 1):
            m.cilindro((4.2 + s * 0.75, -2.0, 0.35), 0.33, 0.22, "X", 8, "cabeza")
        m.barra((4.2, -3.7, 0.5), (4.2, -4.6, 0.4), 0.12, 0.12, "metal")
        m.cilindro((4.2, -2.9, 4.2), 0.08 if fino else 0.13, 6.8, "Z", 6, "metal")
        m.caja((4.2, -2.9, 7.4), (2.0, 0.2, 0.2), "metal")
        for dx in (-0.75, -0.25, 0.25, 0.75):
            m.caja((4.2 + dx, -3.05, 7.4), (0.42, 0.24, 0.36), "cabeza")
            luces.append(((4.2 + dx, -3.18, 7.4), (0.34, 0.02, 0.28)))
        _cajas(m, 4.5, 4.5, fino)
        _cajas(m, 2.8, 5.0, fino)
    elif a == 2:                                       # recinto cuadrado de HESCO con la entrada a −Y
        H = 6.6
        _hesco(m, (-H, -H), (-H, H), fino)
        _hesco(m, (-H + 0.6, H), (H - 0.6, H), fino)
        _hesco(m, (H, H), (H, -H), fino)
        _hesco(m, (-H + 0.6, -H), (-1.8, -H), fino)
        _hesco(m, (1.8, -H), (H - 0.6, -H), fino)
        _sacos(m, (-1.6, -H - 2.2), (1.6, -H - 2.2), fino)   # sacos delante de la entrada, en chicane
        _poner(m, _tienda, 1.6, 1.5, 6.0, 4.5, fino)
        _mastil(m, -3.8, -3.2, 7.0, fino, luces, antena=True)
        _cajas(m, -3.8, 3.5, fino)
    elif a == 3:                                       # sacos y tienda bajo red de camuflaje, foco en trípode
        _sacos(m, (-6.5, -6.5), (-6.5, 6.5), fino)
        _sacos(m, (-6.0, 6.5), (6.5, 6.5), fino)
        _sacos(m, (6.5, 6.0), (6.5, -6.5), fino)
        _sacos(m, (6.0, -6.5), (1.5, -6.5), fino)
        _poner(m, _tienda, -1.5, 1.2, 6.0, 4.5, fino)
        # red de camuflaje en dos aguas por encima de la tienda, sobre cuatro postes
        for x in (-4.6, 1.6):
            for y in (-2.6, 5.0):
                m.cilindro((x, y, 1.6), 0.05 if fino else 0.1, 3.2, "Z", 4, "metal")
        for s in (-1, 1):
            m.barra((-1.5, -2.6, 3.45), (-1.5 + s * 3.5, -2.6, 2.6), 0.04, 0.04, "metal") if fino else None
        m.loft([(-1.0, -0.05), (1.0, -0.05), (0.0, 1.0)], [(-2.8, 3.4, 0.5, 2.95), (5.2, 3.4, 0.5, 2.95)], "red")
        # foco en trípode
        x, y = 3.4, -3.4
        for k in range(3):
            g = k * 2 * math.pi / 3
            m.barra((x + 0.6 * math.cos(g), y + 0.6 * math.sin(g), 0.0), (x, y, 2.2), 0.05 if fino else 0.12,
                    0.05 if fino else 0.12, "metal")
        m.caja((x, y - 0.1, 2.4), (0.5, 0.35, 0.4), "cabeza")
        luces.append(((x, y - 0.29, 2.4), (0.42, 0.02, 0.32)))
        _cajas(m, 4.0, 3.5, fino)
    else:                                              # dos tiendas pequeñas y un nido de ametralladora con techo
        _poner(m, _tienda, -3.4, 2.5, 4.5, 3.4, fino)
        _poner(m, _tienda, 1.8, 2.5, 4.5, 3.4, fino)
        # nido: sacos en herradura de 3,4 m con techo de chapa y tierra
        for p0, p1 in (((-1.7, -5.6), (1.7, -5.6)), ((-2.1, -5.2), (-2.1, -2.2)), ((2.1, -5.2), (2.1, -2.2))):
            _sacos(m, p0, p1, fino, alto=1.5)
        for x in (-1.6, 1.6):
            m.cilindro((x, -2.3, 1.0), 0.06 if fino else 0.12, 2.0, "Z", 4, "metal")
        m.caja((0, -3.9, 2.08), (4.6, 4.0, 0.12), "metal")
        m.caja((0, -3.9, 2.3), (4.2, 3.6, 0.3), "sacos")
        m.barra((0, -5.4, 1.35), (0, -6.6, 1.35), 0.08 if fino else 0.14, 0.08 if fino else 0.14, "cabeza")   # MG
        _sacos(m, (-7.0, -1.0), (-7.0, 6.5), fino)
        _sacos(m, (7.0, -1.0), (7.0, 6.5), fino)
        _sacos(m, (-6.5, 7.0), (6.5, 7.0), fino)
        _mastil(m, 5.2, -1.6, 6.0, fino, luces)
        _cajas(m, -5.0, -2.5, fino)
    # ---- cristales de los focos (hijo «Luces» para la emisión)
    m.parte("Luces")
    for c, s in luces:
        m.caja(c, s, "luz")
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    # origen en el centro del puesto, en el suelo (no en el centro de la caja envolvente)
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
