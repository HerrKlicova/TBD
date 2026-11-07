"""Cortes de carretera para las carreteras NO habilitadas del mundo persistente (coordinador, 7-nov 01:06). Teo, 7-nov.

El dato de qué carretera está cortada lo lleva mapa_sagunto_carreteras.json (Bruno, D-N113); esto es solo el objeto que
se pone encima del tramo. Fotos: G2_turismo_1..3 de Paula (turismos de los 2000) para los coches y H5_escombro_1..2 para
el escombro. Las barreras New Jersey, las vallas de contención y el control militar van DE MEMORIA (fotos pedidas a
Paula): SIN VERIFICAR. Medidas típicas: New Jersey de hormigón de 2,0 × 0,61 m de base, 0,81 m de alto y 0,15 m arriba
(rompe la pendiente a 0,075 y 0,33 m); valla de obra de 2,5 × 1,1 m con pies de hormigón; turismo compacto de 4,3 × 1,75 × 1,45 m.
Colores: hormigón b8b8b8, vallas b8b8b8 (galvanizado), cinta en bandas d4d0cc / 48403c (el rojo de la cinta real está vetado),
coches 6c7874 / 605c48 / quemado 48403c con óxido 7c3c18, cristales 60546c, escombro b8b8b8 / 94948c / b4a080 (ladrillo
claro, el rojizo cae en la familia vetada), sacos 847c34, hierros 48403c.
Origen en el suelo, en el centro del corte. La carretera va a lo largo de Y y el corte la cruza a lo largo de X (calzada
de unos 7 m). Variantes (TBD_VARIANTE % 5): 0 New Jersey en chicane; 1 dos coches cruzados; 2 vallas con cinta;
3 escombros con una New Jersey volcada; 4 control militar: New Jersey, sacos, alambrada y un coche.
"""
import math, os
from mathutils import Matrix, Vector
from montaje import Montaje

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = ["hormigon", "valla", "cinta_clara", "cinta_oscura", "coche", "coche2", "quemado", "oxido", "cristal",
          "goma", "escombro", "escombro2", "ladrillo", "sacos", "hierro"]
COLORES = {"hormigon": "b8b8b8", "valla": "b8b8b8", "cinta_clara": "d4d0cc", "cinta_oscura": "48403c",
           "coche": "6c7874", "coche2": "605c48", "quemado": "48403c", "oxido": "7c3c18", "cristal": "60546c",
           "goma": "48403c", "escombro": "b8b8b8", "escombro2": "94948c", "ladrillo": "b4a080", "sacos": "847c34",
           "hierro": "48403c"}
FACCION = "neutro"
PRESUPUESTO = {0: (100, 3000), 1: (50, 1500)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
# normales inclinadas hacia arriba (como el tren): los flancos casi verticales de la New Jersey caían a media luz
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.5                            # a medias: con 1,0 los coches perdían el volumen
NJ = [(-0.305, 0.0), (0.305, 0.0), (0.305, 0.075), (0.2, 0.33), (0.075, 0.81), (-0.075, 0.81), (-0.2, 0.33),
      (-0.305, 0.075)]


def _nj(m, c, rot_z=0.0, largo=2.0, volcada=False):
    antes = set(m.bm.verts)
    m.loft(NJ, [(-largo / 2, 1, 1, 0), (largo / 2, 1, 1, 0)], "hormigon")
    nuevos = set(m.bm.verts) - antes
    if volcada:
        R = Matrix.Rotation(math.radians(-90), 3, "Y")
        for v in nuevos:
            v.co = R @ v.co + Vector((0, 0, 0.305))
    R = Matrix.Rotation(rot_z, 3, "Z")
    for v in nuevos:
        v.co = R @ v.co + Vector(c)


def _coche(m, c, rot_z, cuerpo, fino, quemado=False):
    """Turismo compacto de 4,3 × 1,75 × 1,45 m a lo largo de su Y local (morro hacia −Y), sobre las llantas."""
    antes = set(m.bm.verts)
    p = "quemado" if quemado else cuerpo
    z0 = 0.12 if quemado else 0.25                     # el quemado se apoya en las llantas
    m.caja((0, 0, z0 + 0.35), (1.75, 4.3, 0.7), p)
    # habitáculo trapecial: perfil (x, z) con los costados recogidos
    hab = [(-0.82, 0.0), (0.82, 0.0), (0.66, 0.52), (-0.66, 0.52)]
    m.loft(hab, [(-0.9, 1, 1, z0 + 0.71), (1.25, 1, 1, z0 + 0.71)], p)
    lados = 10 if fino else 8
    for y in (-1.3, 1.35):
        for s in (-1, 1):
            if quemado:
                m.cilindro((s * 0.72, y, 0.27), 0.27, 0.12, "X", lados, "oxido")
            else:
                m.cilindro((s * 0.75, y, 0.31), 0.31, 0.2, "X", lados, "goma")
    if fino:
        for s in (-1, 1):                              # ventanillas (o huecos negros si está quemado)
            m.caja((s * 0.76, 0.17, z0 + 0.98), (0.04, 1.85, 0.34), "cristal", rot_y=s * 0.29)
        m.caja((0, -0.98, z0 + 0.96), (1.4, 0.04, 0.42), "cristal", rot_x=-0.75)
    nuevos = set(m.bm.verts) - antes
    R = Matrix.Rotation(rot_z, 3, "Z")
    for v in nuevos:
        v.co = R @ v.co + Vector(c)


def _valla(m, x0, y, fino):
    """Valla de contención de 2,5 × 1,1 m (tubo) con la cinta atada a media altura."""
    g = 0.08 if fino else 0.13                         # ≥ 2 px a tamaño de juego
    for x in (x0 - 1.2, x0 + 1.2):
        m.caja((x, y, 0.55), (g, g, 1.1), "valla")
        m.caja((x, y, 0.08), (0.25, 0.65, 0.16), "hormigon")                # pies de hormigón (valla de obra)
    for z in (0.15, 1.08):
        m.caja((x0, y, z), (2.3, g, g), "valla")
    if fino:
        for k in range(5):
            m.caja((x0 - 0.8 + 0.4 * k, y, 0.6), (0.05, 0.05, 0.86), "valla")


def _cinta(m, x0, x1, y, z, fino):
    n = max(2, int(round(abs(x1 - x0) / 0.5)))
    d = (x1 - x0) / n
    for k in range(n):
        a, b = x0 + k * d + 0.01, x0 + (k + 1) * d - 0.01
        m.caja(((a + b) / 2, y + 0.08, z), (abs(b - a), 0.03, 0.18 if fino else 0.24),
               "cinta_clara" if k % 2 == 0 else "cinta_oscura")


def _escombros(m, cx, cy, fino, n=10):
    piezas = ("escombro", "escombro2", "ladrillo")
    for k in range(n if fino else n // 2 + 1):
        a = k * 2.399
        r = 0.4 + 0.22 * k
        x, y = cx + r * math.cos(a), cy + 0.6 * r * math.sin(a)
        s = 0.9 - 0.05 * k
        m.caja((x, y, s * 0.18), (s, 0.7 * s, s * 0.36), piezas[k % 3], rot_z=a, rot_x=0.12 * (k % 3))


def construir(nombre="SM_CorteCarretera", lod=0):
    m = Montaje(PIEZAS, ["corte"])
    fino = lod == 0
    a = ASPECTO
    if a == 0:                                        # chicane de New Jersey
        for x in (-2.5, -0.45, 1.6):
            _nj(m, (x, -0.8, 0), math.pi / 2)
        _nj(m, (3.3, 1.4, 0), math.pi / 2)
        _nj(m, (-3.4, 1.4, 0), math.pi / 2 + 0.25)
    elif a == 1:                                      # dos coches cruzados
        _coche(m, (-1.6, 0.0, 0), math.pi / 2 - 0.25, "coche", fino)
        _coche(m, (2.2, 0.6, 0), math.pi / 2 + 0.4, "coche2", fino, quemado=True)
        if fino:
            _escombros(m, 0.6, -2.0, fino, n=5)
    elif a == 2:                                      # vallas con cinta
        for x in (-2.55, 0.0, 2.55):
            _valla(m, x, 0.0, fino)
        for z in (0.45, 0.85):
            _cinta(m, -3.75, 3.75, 0.0, z, fino)
        if fino:
            _valla(m, 4.2, 1.6, fino)
    elif a == 3:                                      # escombros con una New Jersey volcada
        _escombros(m, -1.0, 0.0, fino, n=12)
        _escombros(m, 2.0, 0.6, fino, n=8)
        _nj(m, (0.8, -1.4, 0), math.pi / 2 + 0.3, volcada=True)
        if fino:
            for k in range(4):                        # hierros de la armadura
                m.barra((-1.2 + 0.3 * k, 0.4, 0.4), (-1.0 + 0.35 * k, 0.9, 1.1), 0.04, 0.04, "hierro")
    else:                                             # control militar
        for x in (-2.3, 2.3):
            _nj(m, (x, 0.0, 0), math.pi / 2)
        for fila in range(3):
            for k in range(4 - fila):
                m.caja((-0.9 + 0.62 * k + 0.31 * fila, -1.2, 0.15 + 0.29 * fila), (0.58, 0.4, 0.27), "sacos")
        for k in range(10):                           # alambrada en zigzag
            x0, x1 = -4.0 + 0.8 * k, -3.6 + 0.8 * k
            m.barra((x0, 1.5, 0.1), (x1, 1.5, 0.8), 0.06 if fino else 0.1, 0.06 if fino else 0.1, "hierro")
            m.barra((x1, 1.53, 0.8), (x0 + 0.8, 1.53, 0.1), 0.06 if fino else 0.1, 0.06 if fino else 0.1, "hierro")
        _coche(m, (5.6, -0.6, 0), 0.2, "coche", fino)
    return m.objeto(nombre)
