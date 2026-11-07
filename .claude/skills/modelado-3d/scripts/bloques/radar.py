"""Radar terrestre de vigilancia para la red de estaciones (D-N124 §6.2: 30 km, contacto de todas las hordas, 20 u y
4 personas, hace ruido por el generador). Teo, 7-nov-2026.

Fotos de Paula (referencias-reales/radar/): R1_radar_raven_1.jpg, radar Raven del ET en remolque de un eje (CC BY 3.0,
Outisnn): cajón de equipos con dos tapas que se abren hacia arriba (dentro, el grupo electrógeno SIN VERIFICAR), rueda
de repuesto en el costado, lanza con rueda jockey, gatos de apoyo, mástil articulado (brazo bajo inclinado y brazo alto
casi vertical) y cabeza con carcasa curva y antena plana. R2_radar_rasura_1.jpg (radar portátil Rasura en trípode)
queda como referencia de la caja de equipos; no se modela. Del ARINE y del RAC-3D no hay fotos libres.
Medidas estimadas sobre la foto, SIN VERIFICAR: cajón de 1,5 x 1,9 x 1,0 m sobre el bastidor a 0,55 m, ruedas de
0,76 m, lanza de 1,4 m, cabeza a 5,2 m del suelo. Variantes 1 y 2 añaden un shelter de 4,4 x 2,3 x 2,3 m y un
generador aparte (instalación fija en la estación; tipo genérico, sin foto).
PIEZA ANIMABLE (FBX con jerarquía, mira a +Z en Unity, como los vehículos):
  SM_Radar_LODn   base (origen en el suelo, centro de la parcela)
  └─ Antena       carcasa y antena; origen en el eje del brazo alto; gira sobre su Y de Unity (la vertical).
                  Propuesta: una vuelta cada 4 s con el radar en marcha, parada sin combustible.
En las variantes 3 y 4 la cabeza no está arriba y no hay hijo Antena.
Colores: chapa 50541c con techos b4a080 (valor partido, como los vehículos), antena 88885c (clara, para que se lea el
giro), brazos 70784c, tapas y metal 605c48, goma 48403c, llanta 70784c, sacos b4a080, red 847c34. Nada bajo L* 28.
Variantes (TBD_VARIANTE % 5):
  0 el de la foto: el remolque Raven desplegado, con las tapas abiertas;
  1 instalación: el Raven junto a un shelter de equipos y un generador aparte, con vientos en el mástil;
  2 fortificado: como el 1 con parapeto de sacos y red de camuflaje sobre el shelter;
  3 plegado (en montaje o para mover): el mástil tumbado sobre el remolque y las tapas cerradas;
  4 destruido (estación caída): calcinado con la paleta de los restos K8, el remolque inclinado sin la rueda +X, el
    brazo alto y la cabeza en el suelo.
"""
import math
import os
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["casco", "puerta", "mastil", "antena", "metal", "goma", "llanta", "saco", "red", "hollin", "cable", "tapa",
          "red_b", "red_c", "quemado", "oxido"]
COLORES = {"casco": "50541c", "puerta": "605c48", "mastil": "70784c", "antena": "88885c", "metal": "605c48",
           "goma": "48403c", "llanta": "70784c", "saco": "b4a080", "red": "847c34", "hollin": "605c48",
           "cable": "605c48", "tapa": "b4a080", "red_b": "70784c", "red_c": "605c48", "quemado": "94948c",
           "oxido": "7c3c18"}
# Lucía 09:08: todo en la familia oliva, como en la foto (el techo beige hacía leer una caja y no un radar); el beige
# solo en el interior de las tapas abiertas («tapa»)
VALOR_PIEZAS = {"casco": ("70784c", "50541c", "50541c")}
COLORES_LOD1 = {"goma": "605c48"}
FACCION = "propio"
PRESUPUESTO = {0: (150, 3500), 1: (80, 1600)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.12}
PARTES = ["base", "Antena"]
R = math.radians
XR, YR = 1.2, 0.0                       # centro del remolque (la lanza mira a -Y)
ZC = 0.55                               # bastidor
CAJ = (1.5, 1.9, 1.0)                   # cajón
P_BRAZO = (XR, YR + 0.75, ZC + CAJ[2] + 0.05)   # articulación del brazo bajo (arriba, al fondo del cajón)
CODO = (XR, YR + 0.25, 3.9)
H = 5.2
PIV = {"Antena": (XR, YR + 0.15, H)}
GIRO_PRUEBA = {"Antena": ("Z", 50)}


def _cabeza(m, x, y, z, fino, rot_x=0.0):
    """Carcasa curva y antena plana (mira a -Y) con la base en z."""
    m.cilindro((x, y, z + 0.12), 0.16 if fino else 0.2, 0.24, "Z", 8, "metal")
    m.cilindro((x, y + 0.12, z + 0.52), 0.34, 0.62, "X", 10 if fino else 8, "casco")      # carcasa curva
    m.caja((x, y - 0.2, z + 0.55), (1.1, 0.14, 0.62), "antena", rot_x=R(-8) + rot_x)      # antena plana
    m.caja((x, y + 0.2, z + 0.32), (0.6, 0.5, 0.3), "casco")                               # caja de la electrónica


def _remolque(m, fino, v):
    x, y = XR, YR
    sx, sy, sz = CAJ
    zc = ZC + sz / 2
    # bastidor, cajón y tapas
    m.caja((x, y, ZC - 0.06), (sx - 0.1, sy + 0.2, 0.12), "metal")
    m.caja((x, y, zc), (sx, sy, sz), "casco")
    if v in (0, 1, 2):   # tapas abiertas hacia los lados, como en la foto
        for s in (-1, 1):
            m.caja((x + s * (sx / 2 + 0.25), y - 0.2, ZC + sz + 0.2), (0.75, 1.1, 0.05), "tapa", rot_y=s * R(-35))
    else:
        m.caja((x, y - 0.2, ZC + sz + 0.03), (sx - 0.1, 1.1, 0.06), "puerta")
    if fino:
        m.caja((x - sx / 2 - 0.02, y + 0.3, zc), (0.04, 0.6, 0.5), "puerta")            # panel del costado
    # ruedas (la +X reventada en la destruida) y rueda de repuesto en el costado
    for s in (-1, 1):
        if v == 4 and s == 1:      # destruido: la rueda +X arrancada
            continue
        if v == 4:                 # calcinado: sin neumático, queda la llanta
            m.cilindro((x + s * (sx / 2 + 0.18), y + 0.15, 0.38), 0.27, 0.2, "X", 10 if fino else 8, "llanta")
        else:
            m.cilindro((x + s * (sx / 2 + 0.18), y + 0.15, 0.38), 0.38, 0.24, "X", 12 if fino else 8, "goma")
            m.cilindro((x + s * (sx / 2 + 0.31), y + 0.15, 0.38), 0.21, 0.03, "X", 8, "llanta")
        m.caja((x + s * (sx / 2 + 0.18), y + 0.15, 0.85), (0.3, 0.9, 0.06), "casco")    # guardabarros
    if v != 4:
        m.cilindro((x + sx / 2 + 0.14, y - 0.55, zc + 0.05), 0.36, 0.2, "X", 10 if fino else 8, "goma")
        m.cilindro((x + sx / 2 + 0.25, y - 0.55, zc + 0.05), 0.2, 0.03, "X", 8, "llanta")
    # lanza con rueda jockey y gatos de apoyo
    g = 0.1 if fino else 0.2
    m.barra((x - 0.5, y - sy / 2, ZC), (x, y - sy / 2 - 1.4, ZC - 0.02), g, g, "metal")
    m.barra((x + 0.5, y - sy / 2, ZC), (x, y - sy / 2 - 1.4, ZC - 0.02), g, g, "metal")
    m.cilindro((x, y - sy / 2 - 1.1, 0.27), 0.1 if fino else 0.18, 0.5, "Z", 8, "metal")
    for (gx, gy) in ((-1, 1), (1, 1)):
        m.cilindro((x + gx * (sx / 2 - 0.1), y + gy * (sy / 2 + 0.15), ZC / 2), 0.06 if fino else 0.17, ZC, "Z", 8,
                   "metal")


def _shelter(m, fino, v):
    xs, ys = -2.4, 0.6
    for x in (xs - 1.6, xs + 1.6):
        m.caja((x, ys, 0.15), (0.3, 2.5, 0.3), "metal")
    m.caja((xs, ys, 0.3 + 1.15), (4.4, 2.3, 2.3), "casco")
    m.caja((xs + 0.9, ys - 1.17, 1.3), (0.9, 0.06, 1.9), "puerta")
    m.caja((xs - 2.24, ys, 1.6), (0.12, 1.0, 0.8), "puerta")                       # climatizador
    xg, yg = -1.6, -2.3                                                            # generador aparte, con escape
    m.caja((xg, yg, 0.55), (1.8, 1.0, 1.1), "casco")
    m.cilindro((xg + 0.6, yg + 0.25, 1.35), 0.09 if fino else 0.17, 0.5, "Z", 8, "metal")
    if fino:
        m.barra((xg + 0.9, yg + 0.3, 0.05), (XR - 0.75, YR - 0.4, 0.05), 0.06, 0.06, "cable")
        for k in range(3):                                                         # vientos del mástil
            a = R(30 + 120 * k)
            m.barra(CODO, (XR + 3.0 * math.cos(a), YR + 0.25 + 3.0 * math.sin(a), 0.0), 0.025, 0.025, "cable")
    if v == 2:
        for (x, y, dx, dy) in ((xg, yg - 0.75, 2.4, 0.5), (xg - 1.15, yg, 0.5, 1.4), (XR, YR - 2.3, 2.6, 0.5),
                               (XR + 1.6, YR, 0.5, 3.0), (XR, YR + 1.6, 2.6, 0.5)):
            m.caja((x, y, 0.32), (dx, dy, 0.64), "saco")
            m.caja((x, y, 0.84), (dx - 0.1, dy - 0.05, 0.4), "saco")
        _red(m, xs, ys, 3.05, 5.6, 3.6, fino)
        g = 0.05 if fino else 0.2
        for x in (xs - 2.6, xs + 2.6):
            for y in (ys - 1.55, ys + 1.55):
                m.caja((x, y, 1.5), (g * 1.6, g * 1.6, 3.0), "mastil")             # postes de la red


def _h(x, y, k=0):
    s = math.sin(x * 12.9898 + y * 78.233 + k * 19.19) * 43758.5453
    return s - math.floor(s)


def _red(m, xc, yc, z, lx, ly, fino):
    """Red de camuflaje (Lucía 09:08, regla 6 de la D-N129): teselas moteadas en tres valores, con huecos que dejan
    ver el shelter y el borde deshilachado (teselas que faltan o cuelgan en el perímetro)."""
    p = 0.6 if fino else 0.8
    nx, ny = int(lx / p), int(ly / p)
    for i in range(nx):
        for j in range(ny):
            x = xc - lx / 2 + (i + 0.5) * p
            y = yc - ly / 2 + (j + 0.5) * p
            borde = i in (0, nx - 1) or j in (0, ny - 1)
            h = _h(i, j)
            if h < ((0.4 if fino else 0.25) if borde else 0.14):   # huecos, más en el borde
                continue
            pieza = ("red", "red_b", "red_c")[int(_h(i, j, 1) * 3)]
            zz = z + 0.06 * (y - yc) / ly + 0.012 * ((i + j) % 3)    # 1,2 cm entre teselas vecinas: sin caras gemelas
            if borde and h > 0.75:                     # cuelga hacia fuera
                rx = R(35) * (1 if j == ny - 1 else -1 if j == 0 else 0)
                ry = R(35) * (-1 if i == nx - 1 else 1 if i == 0 else 0)
                m.caja((x, y, zz - 0.15), (p * 1.05, p * 1.05, 0.04), pieza, rot_x=rx, rot_y=ry)
            else:
                m.caja((x, y, zz), (p * 1.05, p * 1.05, 0.04), pieza, rot_z=R(10) * (_h(i, j, 2) - 0.5))


def _calcinar(m, caras):
    """Destruido (Lucía 09:08): chapa calcinada con la paleta de los restos K8 oxidados (quemado 94948c, hollín
    605c48, óxido 7c3c18 arriba); nada por debajo de 48403c."""
    ip = {n: PIEZAS.index(n) for n in PIEZAS}
    for f in caras:
        if not f.is_valid or f.material_index not in (ip["casco"], ip["puerta"], ip["mastil"], ip["metal"], ip["antena"],
                                                              ip["llanta"]):
            continue
        c = f.calc_center_median()
        h = _h(round(c.x, 2), round(c.y + c.z, 2), 3)
        if f.normal.z > 0.7 and h < 0.45:
            f.material_index = ip["oxido"]
        elif h < 0.35:
            f.material_index = ip["hollin"]
        else:
            f.material_index = ip["quemado"]


def construir(nombre="SM_Radar", lod=0):
    v = VAR
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    b = 0.2 if fino else 0.34      # grueso de los brazos
    antes = set(m.bm.verts)
    _remolque(m, fino, v)
    if v == 4:   # el brazo bajo, que se queda en pie sobre el remolque
        m.barra(P_BRAZO, CODO, b * 1.3, b, "mastil")
        # inclinado 16° hacia el lado de la rueda arrancada, apoyado en la rueda -X
        from mathutils import Matrix, Vector
        piv = Vector((XR - 0.95, 0, 0.0))
        M = Matrix.Translation(piv) @ Matrix.Rotation(R(16), 4, "Y") @ Matrix.Translation(-piv)
        nuevos = [w for w in m.bm.verts if w not in antes]
        for w in nuevos:
            w.co = M @ w.co
        _calcinar(m, {f for w in nuevos for f in w.link_faces})
    if v in (1, 2):
        _shelter(m, fino, v)
    if v in (0, 1, 2):   # mástil desplegado y cabeza arriba (pieza animable)
        m.barra(P_BRAZO, CODO, b * 1.3, b, "mastil")
        m.barra(CODO, (XR, YR + 0.15, H), b, b * 0.8, "mastil")
        m.cilindro(CODO, 0.16 if fino else 0.2, 0.36, "X", 8, "metal")              # codo
        m.parte("Antena")
        _cabeza(m, XR, YR + 0.15, H, fino)
        m.parte("base")
    elif v == 3:         # plegado: el brazo tumbado hacia la lanza y la cabeza sobre el cajón
        z = ZC + CAJ[2] + 0.25
        m.barra(P_BRAZO, (XR, YR - 1.4, z), b * 1.3, b, "mastil")
        m.caja((XR, YR - 1.75, z), (0.5, 0.5, 0.3), "metal")                         # soporte de transporte
        _cabeza(m, XR, YR - 0.3, z - 0.05, fino, rot_x=R(8))
    else:                # destruido: el brazo bajo (ya puesto) en pie, el alto y la cabeza caídos a +X
        antes = set(m.bm.verts)
        m.barra((XR + 0.3, YR + 0.2, 0.2), (XR + 2.4, YR + 0.8, 0.25), b, b * 0.8, "mastil")
        m.cilindro((XR + 2.9, YR + 1.0, 0.36), 0.34, 0.62, "Y", 8, "casco")
        m.caja((XR + 3.1, YR + 0.3, 0.35), (1.1, 0.14, 0.62), "antena", rot_z=R(25), rot_x=R(75))
        _calcinar(m, {f for w in m.bm.verts if w not in antes for f in w.link_faces})
    return m.objeto(nombre)


def separar(o, lod):
    return _separar(o, PARTES, PIV)
