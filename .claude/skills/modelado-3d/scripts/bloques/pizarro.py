"""VCI Pizarro (ASCOD) con orugas y piezas animables (coordinador, 6-nov 23:39: «vehículos con física», PR #24). Teo.

Medidas (Wikipedia EN, «ASCOD», ficha del Pizarro): 6,83 m de largo, 3,64 m de ancho, 2,43 m de alto; 26,3 t; 3 + 7.
Disposición (misma fuente): motor delante, conductor delante a la IZQUIERDA, torre biplaza algo descentrada, cañón Mauser
MK 30/2 de 30 mm, compartimento trasero con dos escotillas en el techo; suspensión de barras de torsión.
De la foto de Paula (V3_pizarro_2.jpg, 3/4 delantero, desfile de 2008, CC BY-SA 2.0): glacis largo y poco inclinado con
blindaje añadido en losetas, frontal bajo casi vertical con faros en las esquinas y ganchos, casco ancho que vuela sobre
las orugas, siete ruedas de rodaje por lado sin rodillos de retorno (la cadena apoya arriba en las ruedas), rueda motriz
delante, torre angulosa con frente en cuña, cañón largo con bocacha, mira del tirador en caja sobre la torre, lanzafumíg-
enos en racimo a los lados de la torre, escotilla del conductor con periscopios, rejilla grande en el costado.
SIN VERIFICAR (estimado de la foto): ruedas de 0,62 m, cadena de 0,48 m, techo del casco a 1,72 m, torre de 2,3 × 2,5 m
con el techo a 2,43, su centro 0,25 m a la derecha y 0,2 m por detrás del centro, cañón de 2,3 m.

Jerarquía del FBX (mira a +Z en Unity, como el Dragón):
  SM_…_LODn                     casco (origen en el suelo, centro del vehículo)
  ├─ Rueda_1L … Rueda_7R        ruedas de rodaje (1 = la delantera; L = izquierda del vehículo = +X en Blender); giran
  │                             sobre su X local
  ├─ Motriz_L/R, Tensora_L/R    rueda motriz (delante) y tensora (detrás), también sobre su X local
  ├─ Oruga_L/R                  cadena (siempre visible)
  ├─ Zapatas_L_A/B, _R_A/B      (solo LOD0) los tacos de la cadena en dos fases, medio paso (15 cm) corridas: mostrar
  │                             A y B alternas a un ritmo proporcional a la velocidad da el efecto de cadena en marcha
  └─ Torre                      gira sobre la vertical; origen en el eje de la torre
     └─ Canon                   se eleva sobre su X local; origen en el muñón
"""
import math
from montaje import Montaje, separar as _separar

PIEZAS = ["casco", "goma", "llanta", "optica", "foco", "metal", "rejilla", "carga"]
# D-N80: nada propio por debajo de #48403c (Bruno, umbral confirmado por Lucía)
COLORES = {"casco": "50541c", "goma": "48403c", "llanta": "70784c", "optica": "60546c", "foco": "f0dc9c",
           "metal": "48403c", "rejilla": "48403c", "carga": "847c34"}
COLORES_LOD1 = {"llanta": "88885c"}        # como el Dragón: cada rueda un punto claro con la cámara alta
VALOR_PIEZAS = {"casco": ("b4a080", "50541c", "50541c")}
ABAJO_CASCO = "50541c"
L_MIN = 28.0
PRESUPUESTO = {0: (1500, 5000), 1: (400, 2500)}

L, ANCHO = 6.83, 3.64
Y0 = L / 2
ZT = 1.72                       # techo del casco
ZR = 2.43                       # techo de la torre
RR = 0.31                       # radio de rueda de rodaje
XO = 1.50                       # eje de la cadena
AO = 0.48                       # ancho de cadena
YR = [-2.34 + k * 0.78 for k in range(7)]
MOTRIZ = (-Y0 + 0.42, 0.74, 0.32)     # (y, z, r)
TENSORA = (Y0 - 0.38, 0.62, 0.29)
TORRE = (-0.25, 0.2, ZT)
CANON = (-0.25, -1.05, 2.06)

PARTES = (["casco"] + ["Rueda_%d%s" % (k + 1, s) for s in "LR" for k in range(7)] +
          ["Motriz_L", "Motriz_R", "Tensora_L", "Tensora_R", "Oruga_L", "Oruga_R", "Zapatas_L_A", "Zapatas_R_A", "Zapatas_L_B",
           "Zapatas_R_B",
           "Torre", "Canon"])


def _pivotes():
    p = {"Torre": TORRE, "Canon": CANON}
    for s, sx in (("L", 1), ("R", -1)):
        x = sx * XO
        for k, y in enumerate(YR):
            p["Rueda_%d%s" % (k + 1, s)] = (x, y, RR + 0.07)
        p["Motriz_" + s] = (x, MOTRIZ[0], MOTRIZ[1])
        p["Tensora_" + s] = (x, TENSORA[0], TENSORA[1])
        p["Oruga_" + s] = (x, 0.0, 0.0)
        p["Zapatas_%s_A" % s] = (x, 0.0, 0.0)
        p["Zapatas_%s_B" % s] = (x, 0.0, 0.0)
    return p


PIV = _pivotes()
PADRES = {"Canon": "Torre"}


def _camino():
    """Recorrido de la cadena en (y, z): abajo bajo las ruedas, sube a la motriz y a la tensora, arriba apoyada en las
    ruedas. Polígono cerrado en sentido de avance."""
    zb = 0.04
    pts = [(YR[0] - 0.2, zb), (YR[-1] + 0.2, zb)]
    ty, tz, tr = TENSORA
    R = tr + 0.04
    pts += [(ty + R * math.cos(math.radians(a)), tz + R * math.sin(math.radians(a))) for a in (-60, -30, 0, 30, 60)]
    zt = 2 * RR + 0.07 + 0.04
    pts += [(YR[-1], zt), (YR[0], zt)]
    my, mz, mr = MOTRIZ
    R = mr + 0.04
    pts += [(my - R * math.cos(math.radians(a)), mz + R * math.sin(math.radians(a))) for a in (60, 30, 0, -30, -60)]
    return pts


def _oruga(m, x):
    pts = _camino()
    n = len(pts)
    for i in range(n):
        y0, z0 = pts[i]
        y1, z1 = pts[(i + 1) % n]
        m.barra((x, y0, z0), (x, y1, z1), AO, 0.08, "goma")


def _zapatas(m, x, fase):
    pts = _camino()
    n = len(pts)
    paso = 0.30                                   # zapatas solo en los tramos rectos de abajo y de arriba
    for (ya, za), (yb, zb) in ((pts[0], pts[1]), (pts[n - 7], pts[n - 6])):
        d = math.hypot(yb - ya, zb - za)
        k = 0
        while (k + 0.5) * paso + fase <= d:
            t = ((k + 0.5) * paso + fase) / d
            y, z = ya + (yb - ya) * t, za + (zb - za) * t
            dz = -0.055 if za < 0.3 else 0.055
            m.caja((x, y, z + dz), (AO + 0.02, 0.07, 0.04), "goma")
            k += 1


def construir(nombre="SM_Pizarro", lod=0):
    fino = lod == 0
    m = Montaje(PIEZAS, PARTES)
    f = m.fino(lod)
    # ---- casco: parte baja entre las cadenas, y la alta, que vuela sobre ellas, con el glacis largo
    m.caja((0, 0.05, 0.72), (2.3, L - 0.3, 0.56), "casco")
    P = [(-Y0, 0.98), (-Y0 + 0.05, 1.12), (-Y0 + 2.45, ZT), (Y0, ZT), (Y0, 0.98)]
    m.perfil_extruido(P, lambda z: ANCHO / 2 - 0.12)
    m.caja((0, -Y0 + 0.25, 0.75), (2.3, 0.5, 0.5), "casco", inclinar=0.12)          # frontal bajo
    # faros y ganchos en el frontal
    for x in (-1.35, 1.35):
        m.cilindro((x, -Y0 - 0.06, 1.25), 0.11, 0.14, "Y", 8, "foco")
        f.caja((x, -Y0 - 0.02, 1.25), (0.3, 0.06, 0.3), "metal")
        m.caja((x * 0.62, -Y0 - 0.05, 0.62), (0.16, 0.18, 0.16), "metal")
    f.caja((0, -Y0 + 0.02, 0.9), (1.3, 0.04, 0.2), "metal")                         # placa de matrícula/defensa
    # losetas del blindaje añadido en el glacis (solo LOD0): tiras finas en relieve
    if fino:
        g = math.atan2(ZT - 1.12, 2.4)
        for k in range(1, 6):
            y = -Y0 + 0.05 + k * 0.4
            z = 1.12 + (y + Y0 - 0.05) * math.tan(g)
            m.caja((0, y, z + 0.015), (ANCHO - 0.3, 0.03, 0.03), "casco", rot_x=g)
        for x in (-1.2, -0.6, 0.0, 0.6, 1.2):
            yc = -Y0 + 1.25
            m.caja((x, yc, 1.12 + 1.2 * math.tan(g) + 0.015), (0.03, 2.4, 0.03), "casco", rot_x=g)
    # escotilla y periscopios del conductor (delante a la izquierda = +X)
    m.caja((0.95, -1.15, ZT + 0.06), (0.75, 0.6, 0.12), "casco")
    for k in range(3):
        m.caja((0.75 + k * 0.2, -1.47, ZT + 0.1), (0.14, 0.06, 0.1), "optica")
    # rejilla grande en el costado izquierdo y salida de humos a la derecha
    m.caja((ANCHO / 2 - 0.1, -0.2, 1.38), (0.04, 1.6, 0.5), "rejilla")
    m.caja((-(ANCHO / 2 - 0.1), -1.4, 1.38), (0.04, 0.9, 0.35), "rejilla")
    # techo trasero: dos escotillas, cajones laterales y puerta trasera
    for x in (-0.55, 0.55):
        m.caja((x, 2.4, ZT + 0.04), (0.8, 1.0, 0.08), "casco")
    for x in (-1.45, 1.45):
        m.caja((x, 2.2, ZT + 0.15), (0.45, 1.8, 0.3), "carga")
    m.caja((0, Y0 + 0.03, 1.2), (1.0, 0.06, 1.1), "casco")
    f.caja((0.35, Y0 + 0.07, 1.25), (0.08, 0.04, 0.3), "metal")
    f.caja((0, Y0 + 0.08, 1.55), (0.3, 0.04, 0.2), "optica")                         # visor de la puerta
    # guardabarros sobre las cadenas delante y detrás
    for x in (-XO, XO):
        m.caja((x, -Y0 + 0.2, 1.02), (AO + 0.1, 0.4, 0.05), "casco")
    # ---- orugas, ruedas, motriz y tensora
    for s, sx in (("L", 1), ("R", -1)):
        x = sx * XO
        for k, y in enumerate(YR):
            m.parte("Rueda_%d%s" % (k + 1, s))
            m.cilindro((x, y, RR + 0.07), RR, 0.42, "X", 14 if fino else 8, "llanta")
            m.cilindro((x + sx * 0.215, y, RR + 0.07), 0.1, 0.04, "X", 8, "metal")
        m.parte("Motriz_" + s)
        my, mz, mr = MOTRIZ
        m.cilindro((x, my, mz), mr, 0.38, "X", 14 if fino else 8, "metal")
        if fino:                                           # dientes de la motriz
            for a in range(0, 360, 36):
                r = math.radians(a)
                m.caja((x, my + (mr + 0.03) * math.cos(r), mz + (mr + 0.03) * math.sin(r)), (0.3, 0.07, 0.07), "metal",
                       rot_x=r)
        m.parte("Tensora_" + s)
        ty, tz, tr = TENSORA
        m.cilindro((x, ty, tz), tr, 0.4, "X", 14 if fino else 8, "llanta")
        m.parte("Oruga_" + s)
        _oruga(m, x)
        if fino:
            m.parte("Zapatas_%s_A" % s)
            _zapatas(m, x, 0.0)
            m.parte("Zapatas_%s_B" % s)
            _zapatas(m, x, 0.15)
    # ---- torre
    m.parte("Torre")
    tx, ty, tz = TORRE
    # sin anillo a la vista ni escotillas redondas: a tamaño de juego sus arcos dibujaban una cara sonriente (Lucía, 23:46)
    m.caja((tx, ty, tz + 0.05), (1.5, 1.5, 0.1), "metal")                              # base de la torre (oculta)
    # frente en cuña: un solo bloque con el frente muy tendido y estrechado (antes, una cuña aparte cuyo canto oscuro
    # hacía de «boca» con las escotillas de «ojos»; Lucía, 23:46)
    m.caja((tx, ty - 0.15, tz + 0.36), (2.3, 2.7, 0.62), "casco", inclinar=0.75, cuña=0.7)
    m.caja((tx, ty + 1.45, tz + 0.38), (1.8, 0.6, 0.5), "casco")                      # cesta trasera
    m.caja((tx - 0.65, ty - 0.55, ZR + 0.12), (0.5, 0.6, 0.3), "casco")               # mira del tirador
    m.caja((tx - 0.65, ty - 0.86, ZR + 0.12), (0.4, 0.04, 0.2), "optica")
    m.caja((tx + 0.6, ty + 0.25, ZR + 0.06), (0.62, 0.7, 0.12), "casco")             # escotilla del jefe
    if fino:
        m.caja((tx - 0.35, ty + 0.95, ZR + 0.04), (0.4, 0.4, 0.08), "casco")         # escotilla del tirador
    for k in range(4):                                                                # lanzafumígenos
        for sx in (1, -1):
            m.cilindro((tx + sx * 1.18, ty + 0.9 + k * 0.17, tz + 0.55), 0.06, 0.28, "X", 6, "metal")
    if fino:
        m.barra((tx + 0.85, ty + 1.4, ZR), (tx + 0.85, ty + 1.45, ZR + 1.4), 0.025, 0.025, "metal")   # antena
    # ---- cañón (hijo de la torre)
    m.parte("Canon")
    cx, cy, cz = CANON
    m.caja((cx, cy - 0.1, cz), (0.5, 0.3, 0.38), "casco")                             # mantelete
    gr = 0.06 if fino else 0.09
    m.cilindro((cx, cy - 1.3, cz), gr, 2.3, "Y", 8, "metal")
    m.cilindro((cx, cy - 2.42, cz), gr * 1.6, 0.22, "Y", 8, "metal")                  # bocacha
    return m.objeto(nombre)


def separar(o, lod):
    return _separar(o, PARTES, PIV, PADRES)
