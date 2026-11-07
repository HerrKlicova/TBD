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
  4 destruido (estación caída): el brazo alto y la cabeza en el suelo, hollín y una rueda reventada.
"""
import math
import os
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["casco", "puerta", "mastil", "antena", "metal", "goma", "llanta", "saco", "red", "hollin", "cable"]
COLORES = {"casco": "50541c", "puerta": "605c48", "mastil": "70784c", "antena": "88885c", "metal": "605c48",
           "goma": "48403c", "llanta": "70784c", "saco": "b4a080", "red": "847c34", "hollin": "605c48",
           "cable": "605c48"}
VALOR_PIEZAS = {"casco": ("b4a080", "50541c", "50541c")}
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
            m.caja((x + s * (sx / 2 + 0.25), y - 0.2, ZC + sz + 0.2), (0.75, 1.1, 0.05), "puerta", rot_y=s * R(-35))
    else:
        m.caja((x, y - 0.2, ZC + sz + 0.03), (sx - 0.1, 1.1, 0.06), "puerta")
    if fino:
        m.caja((x - sx / 2 - 0.02, y + 0.3, zc), (0.04, 0.6, 0.5), "puerta")            # panel del costado
    # ruedas (la +X reventada en la destruida) y rueda de repuesto en el costado
    for s in (-1, 1):
        r = 0.38 if not (v == 4 and s == 1) else 0.3
        m.cilindro((x + s * (sx / 2 + 0.18), y + 0.15, r), r, 0.24, "X", 12 if fino else 8, "goma")
        m.cilindro((x + s * (sx / 2 + 0.31), y + 0.15, r), r * 0.55, 0.03, "X", 8, "llanta")
        m.caja((x + s * (sx / 2 + 0.18), y + 0.15, 0.85), (0.3, 0.9, 0.06), "casco")    # guardabarros
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
    if v == 4:
        m.caja((x + 0.2, y - 0.4, ZC + sz + 0.075), (1.0, 0.9, 0.03), "hollin")      # sobre la tapa cerrada
        m.caja((x - sx / 2 - 0.035, y - 0.15, zc + 0.12), (0.03, 1.2, 0.66), "hollin")  # 1,5 cm fuera del panel


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
        m.caja((xs, ys, 3.05), (5.4, 3.3, 0.06), "red", rot_x=R(4))
        g = 0.05 if fino else 0.2
        for x in (xs - 2.6, xs + 2.6):
            for y in (ys - 1.55, ys + 1.55):
                m.caja((x, y, 1.5), (g * 1.6, g * 1.6, 3.0), "mastil")             # postes de la red


def construir(nombre="SM_Radar", lod=0):
    v = VAR
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    b = 0.2 if fino else 0.34      # grueso de los brazos
    _remolque(m, fino, v)
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
    else:                # destruido: el brazo bajo en pie, el alto y la cabeza caídos a +X
        m.barra(P_BRAZO, CODO, b * 1.3, b, "mastil")
        m.barra((XR + 0.3, YR + 0.2, 0.2), (XR + 2.4, YR + 0.8, 0.25), b, b * 0.8, "mastil")
        m.cilindro((XR + 2.9, YR + 1.0, 0.36), 0.34, 0.62, "Y", 8, "casco")
        m.caja((XR + 3.1, YR + 0.3, 0.35), (1.1, 0.14, 0.62), "antena", rot_z=R(25), rot_x=R(75))
    return m.objeto(nombre)


def separar(o, lod):
    return _separar(o, PARTES, PIV)
