"""Vestíbulo de calle de la estación de Castelló (Álvaro, 8-nov 18:02, castellon-nivel.md §7: en (130; 0; 81), huella
X 118–142 y Z 74–88; puerta principal al norte, a la plaza, con yaw 0; boca de escaleras en el lado sur que baja al
andén lateral). Referencia: castellon_estacion.md de Paula (CS1: nave blanca con cubierta curva volada sobre la
entrada y fachada acristalada; CS2: vestíbulo con escaleras mecánicas y torniquetes). Es una versión reducida de la
real (~140 × 45 m), a la medida del nivel. Teo (3), 8-nov.

Planta de 24 × 14 m, 6 m de alto a la cornisa. r2 (18:15, con la CS1 de Paula): cubierta plana blanca con un
lucernario corrido y, delante, el ala curva volada 4 m sobre la entrada (−Y = norte con yaw 0), que arranca baja en el
borde y sube hasta la cornisa. Dentro: fila de torniquetes, máquinas de billetes y el foso de las escaleras mecánicas (dos tramos de 30° que
bajan 7,3 m hacia +X, hasta el nivel del andén) en la mitad sur.
LA CUBIERTA ES UN HIJO APARTE («Cubierta», pivote en la cornisa, (0, 0, 6)): con la cámara cenital tapa el interior, así que Marcos
puede ocultarla o fundirla, como la losa de los andenes.
Colores: fachada b8b8b8 (no d4d0cc, que es el de selección), zócalo y carpintería 6c7874, cristal 6c7874 (apagado,
no claro: se leía como luz), cubierta b8b8b8 arriba con canto 94948c, suelo 94948c, escaleras 88885c, tablones
845c28, sacos 605c48, escombro 88885c, hollín 605c48. Nada rojo ni en la banda de la horda. Medidas SIN VERIFICAR.
Origen: centro de la huella, a la cota de la calle.
Variantes (TBD_VARIANTE % 5) = estados de la D-N124, como el edificio de viajeros de Nules: 0 Abandonada (cristales
rotos a trozos, puertas abiertas, papeles); 1 Segura (cristales tapiados con tablones y sacos en la puerta); 2
EnLiberacion (barricada de bancos y máquinas dentro, escombro, la mitad tapiada); 3 Caida (sin cristales, un tramo
de cubierta hundido dentro, escombro y hollín); 4 Liberada (andamio en la fachada, tablones nuevos, limpio).
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Abandonada", "Segura", "EnLiberacion", "Caida", "Liberada"]
PIEZAS = ["fachada", "zocalo", "cristal", "cubierta", "canto", "suelo", "escalera", "metal", "tablon", "sacos",
          "escombro", "hollin", "papel", "andamio"]
COLORES = {"fachada": "b8b8b8", "zocalo": "6c7874", "cristal": "6c7874", "cubierta": "b8b8b8", "canto": "94948c",
           "suelo": "94948c", "escalera": "88885c", "metal": "6c7874", "tablon": "845c28", "sacos": "605c48",
           "escombro": "88885c", "hollin": "605c48", "papel": "b8b8b8", "andamio": "6c7874"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (60, 6000), 1: (30, 2400)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
PARTES = ["vestibulo", "Cubierta"]
AX, AY, HC = 24.0, 14.0, 6.0
PIV = {"Cubierta": (0.0, 0.0, HC)}   # pivote en la cornisa


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_VestibuloCastellon", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    v = V
    g = 0.08 if fino else 0.34
    y0, y1 = -AY / 2, AY / 2
    # suelo con el hueco del foso de las escaleras (x −9..5, y 2..5,6)
    sx0, sx1, sy0, sy1 = -AX / 2 + 0.05, AX / 2 - 0.05, -AY / 2 + 0.05, AY / 2 - 0.05
    for (xa, xb, ya_, yb_) in ((sx0, sx1, sy0, 2.0), (sx0, sx1, 5.6, sy1), (sx0, -9.0, 1.95, 5.65),
                               (5.0, sx1, 1.95, 5.65)):
        m.caja(((xa + xb) / 2, (ya_ + yb_) / 2, 0.105), (xb - xa, yb_ - ya_, 0.19), "suelo")
    # ---- fachada norte (−Y): acristalada entre pilares, con tres puertas
    for x in (-AX / 2 + 0.2, -6.0, 6.0, AX / 2 - 0.2):
        m.caja((x, y0 + 0.2, (HC - 0.05) / 2), (0.4, 0.4, HC - 0.05), "fachada")
    m.caja((0, y0 + 0.2, HC - 0.4), (AX, 0.4, 0.8), "fachada")                # dintel
    m.caja((0, y0 + 0.2, 0.3), (AX - 0.02, 0.36, 0.4), "zocalo")
    paños = [(-AX / 2 + 0.4, -6.2), (-5.8, 5.8), (6.2, AX / 2 - 0.4)]
    for i, (xa, xb) in enumerate(paños):
        xc, lx = (xa + xb) / 2, xb - xa
        if v == 3:                                   # sin cristales: solo los montantes
            for k in range(3):
                m.caja((xa + (k + 0.5) * lx / 3, y0 + 0.2, 2.3), (0.08 if fino else 0.34, 0.1, 3.8), "zocalo")
            continue
        if v in (1, 4) or (v == 2 and i != 1):       # tapiado con tablones
            nt = 6 if fino else 2
            for k in range(nt):
                m.caja((xc, y0 + 0.12, 0.6 + (k + 0.5) * 3.6 / nt), (lx - 0.05, 0.06, 3.6 / nt - (0.06 if fino else 0.04)),
                       "tablon", rot_y=(_h(k + i, 2) - 0.5) * (0.06 if v == 1 else 0.0))
            continue
        if v == 0 and i == 0:                        # un paño roto a trozos
            m.caja((xc - lx / 4, y0 + 0.2, 2.6), (lx / 2, 0.06, 3.6), "cristal")
            continue
        m.caja((xc, y0 + 0.2, 2.6), (lx, 0.06, 3.6), "cristal")
    # ---- los otros tres lados, macizos, con una banda acristalada alta
    m.caja((0, y1 - 0.2, HC / 2), (AX, 0.4, HC), "fachada")
    for s in (-1, 1):
        m.caja((s * (AX / 2 - 0.2), 0, (HC - 0.03) / 2 + 0.01), (0.4, AY - 0.7, HC - 0.04), "fachada")
        m.caja((s * (AX / 2 - 0.22), 0, 0.32), (0.4, AY - 0.9, 0.56), "zocalo")
    m.caja((0, y1 - 0.42, 4.4), (AX - 1.0, 0.06, 1.2), "cristal")
    # ---- dentro: torniquetes, máquinas y el foso de las escaleras mecánicas (mitad sur)
    yt = -1.0
    for k in range(8):
        if v in (2, 3) and k in (2, 3, 5):
            continue
        m.caja((-7.0 + k * 2.0, yt, 0.6), (0.3, 0.9, 0.8), "metal")
    for x in (-10.0, -9.0, 9.0):
        m.caja((x, -4.5, 0.95), (0.8, 0.5, 1.7), "zocalo")
    # foso: barandilla perimetral y dos tramos que bajan hacia +X hasta −7,3
    fx0, fx1, fy0, fy1 = -9.0, 5.0, 2.0, 5.6
    m.caja(((fx0 + fx1) / 2, fy0, 1.1), (fx1 - fx0, g, g), "metal")
    m.caja((fx0, (fy0 + fy1) / 2, 1.1), (g, fy1 - fy0, g), "metal")
    for k in range(4 if fino else 2):
        m.caja((fx0 + k * (fx1 - fx0) / (3 if fino else 1), fy0, 0.65), (g, g, 0.9), "metal")
    run = 12.6
    ang = math.atan2(7.3, run)
    for j, yy in enumerate((2.9, 4.6)):
        L = math.hypot(7.3, run)
        m.caja((fx0 + 0.6 + run / 2, yy, -7.3 / 2), (L, 1.2, 0.3), "escalera", rot_y=ang)
        m.caja((fx0 + 0.6 + run / 2, yy - 0.65 if j == 0 else yy + 0.65, -7.3 / 2 + 1.0), (L, g, g), "metal",
               rot_y=ang)
    m.caja((fx1 + 1.5, 3.75, -7.4), (4.0, 3.6, 0.2), "suelo")                # rellano abajo, hacia el andén (+Y)
    m.caja((fx1 + 1.5, 6.6, -7.4), (2.4, 2.4, 0.2), "suelo")
    # ---- lo que cuenta cada estado
    _estado(m, fino, v, y0)
    # ---- cubierta curva volada sobre la entrada (hijo aparte)
    if os.environ.get("TBD_SIN_CUBIERTA") == "1":    # solo para las hojas: el interior sin la cubierta
        return m.objeto(nombre)
    m.parte("Cubierta")
    # r2 con la CS1 de Paula: cubierta plana blanca con un lucernario corrido de vidrio y, delante, el ala curva volada
    # que arranca baja en la esquina y sube hasta la cornisa (el gesto de la fachada de Adif)
    hund = (v == 3)
    ZT = HC + 0.2
    if hund:                                         # un trozo de cubierta hundido dentro
        m.caja((-AX / 4 - 0.25, 0, ZT), (AX / 2 + 0.5, AY, 0.4), "cubierta")
        m.caja((AX / 4 + 3.0, 0, ZT), (AX / 2 - 6.0, AY, 0.4), "cubierta")
        m.caja((3.0, -AY / 2 + 2.5, ZT), (6.2, 5.0, 0.4), "cubierta")
        m.caja((3.0, 1.5, 1.4), (6.0, 6.0, 0.4), "cubierta", rot_x=0.35, rot_y=0.25)
    else:
        m.caja((0, 0, ZT), (AX + 0.4, AY + 0.4, 0.4), "cubierta")
    if not hund:
        m.caja((0, 1.5, ZT + 0.55), (AX - 4.0, 2.2, 0.7), "cristal")              # lucernario corrido
        m.caja((0, 1.5, ZT + 0.92), (AX - 3.8, 2.4, 0.06), "canto")
    ya = y0 - 4.0
    n = 6 if fino else 3
    pts = []
    for k in range(n + 1):                           # ala curva: de z 3,4 en el borde a la cornisa sobre la fachada
        u = k / n
        pts.append((ya + (y0 + 0.4 - ya) * u, 3.4 + (ZT + 0.2 - 3.4) * math.sin(u * math.pi / 2)))
    for (ya_, za), (yb_, zb) in zip(pts, pts[1:]):
        L = math.hypot(yb_ - ya_, zb - za)
        a = math.atan2(zb - za, yb_ - ya_)
        m.caja((-2.0, (ya_ + yb_) / 2, (za + zb) / 2), (AX - 4.0, L + 0.02, 0.25), "cubierta", rot_x=a)
    m.caja((-2.0, ya, pts[0][1]), (AX - 4.0, 0.3, 0.4), "canto")              # canto del vuelo
    for x in (-AX / 2 + 2.5, 6.5):                                           # pies del vuelo
        m.caja((x, ya + 0.6, pts[0][1] / 2), (0.3 if fino else 0.4, 0.3 if fino else 0.4, pts[0][1]), "canto")
    # lo que se ve del estado desde arriba, sobre la cubierta plana
    def sobre(x, y, lx, ly, pieza, dz=0.0):
        m.caja((x, y, ZT + 0.23 + dz), (lx, ly, 0.06), pieza)
    if v == 0:                                       # manchas de agua y suciedad
        for k in range(3):
            sobre(-8.0 + k * 7.0, -3.0 + k * 2.5, 3.0, 2.0, "canto")
    elif v == 1:                                     # sacos en el borde del vuelo: puesto de vigía
        for k in range(5):
            sobre(-6.0 + k * 1.0, y0 + 0.8, 0.95, 0.5, "sacos", dz=0.12)
            sobre(-6.0 + k * 1.0, y0 + 0.8, 0.95, 0.5, "sacos", dz=0.4)
    elif v == 2:                                     # chapas arrancadas
        sobre(5.0, 1.0, 4.0, 3.0, "canto")
        sobre(-6.0, -4.0, 3.0, 2.0, "canto")
    elif v == 3:                                     # hollín alrededor del hundimiento
        sobre(-4.0, 3.0, 9.0, 3.0, "hollin")
        sobre(8.0, -1.0, 4.0, 3.0, "hollin")
    else:                                            # chapas nuevas y lona
        sobre(-5.0, 1.0, 5.0, 3.0, "suelo")
        sobre(6.0, -2.0, 3.0, 4.0, "tablon")
    for s in (-1, 1):                                                        # pies del vuelo
        m.caja((s * (AX / 2 - 1.0), ya + 0.6, pts[0][1] / 2), (0.3 if fino else 0.4, 0.3 if fino else 0.4,
                                                                pts[0][1]), "canto")
    return m.objeto(nombre)


def _estado(m, fino, v, y0):
    z = 0.2
    if v == 0:                                       # papeles, cristales y una puerta abierta
        m.caja((-3.0, y0 - 0.6, 1.6), (1.8, 0.06, 2.6), "cristal", rot_z=1.0)
        for k in range(8 if fino else 3):
            m.caja((-9 + _h(k) * 18, -5 + _h(k, 1) * 6, z + 0.03), (0.6, 0.4, 0.04), "escombro", rot_z=_h(k, 2) * 3)
        m.caja((-6.0, y0 - 6.0, 0.4), (1.8, 0.9, 0.8), "zocalo", rot_z=0.5)     # una máquina tirada en la plaza
    elif v == 1:                                     # sacos delante de la puerta y en la plaza, fuera del vuelo
        for f in range(2):
            for k in range(6):
                m.caja((-2.5 + k * 1.0, y0 - 1.0, 0.15 + f * 0.3), (0.95, 0.5, 0.28), "sacos",
                       rot_z=(_h(k + f, 4) - 0.5) * 0.1)
        for k in range(10):
            m.caja((-5.0 + k * 1.0, y0 - 5.2, 0.15), (0.95, 0.5, 0.28), "sacos", rot_z=(_h(k, 5) - 0.5) * 0.1)
            m.caja((-5.0 + k * 1.0, y0 - 5.2, 0.45), (0.95, 0.5, 0.28), "sacos", rot_z=(_h(k, 6) - 0.5) * 0.1)
    elif v == 2:                                     # barricada de bancos y máquinas por dentro
        for k, (x, rz) in enumerate(((-4.0, 0.3), (-1.5, -0.2), (1.2, 0.5), (3.6, 0.1))):
            m.caja((x, y0 + 1.6, 0.6), (1.8, 0.7, 0.9), "zocalo", rot_z=rz, rot_y=0.2 * (k % 2))
        m.caja((0.0, y0 + 2.6, 0.4), (3.0, 1.4, 0.5), "escombro", rot_z=0.2)
        for k, (x, rz) in enumerate(((-7.0, 0.6), (-3.5, -0.3), (4.5, 0.2))):     # fuera, en la plaza
            m.caja((x, y0 - 5.5, 0.45), (2.0, 0.8, 0.9), "zocalo", rot_z=rz, rot_y=0.25)
    elif v == 3:                                     # escombro y hollín
        m.caja((-3.0, 1.0, 0.45), (5.0, 3.0, 0.5), "escombro", rot_z=0.3)
        m.caja((3.0, -3.0, 0.3), (3.0, 2.0, 0.3), "escombro", rot_z=-0.4)
        m.caja((-1.0, -3.0, z + 0.02), (8.0, 4.0, 0.03), "hollin")
        m.caja((0, y0 + 0.43, 3.0), (10.0, 0.04, 2.0), "hollin")
        m.caja((5.0, y0 - 5.5, 0.35), (3.5, 2.0, 0.6), "escombro", rot_z=0.6)      # cristales y cascotes en la plaza
    else:                                            # andamio delante de la fachada
        for x in (-11.0, -5.0, 1.0, 7.0, 11.0):
            for yy in (y0 - 0.4, y0 - 1.4):
                m.caja((x, yy, 2.4), (0.08 if fino else 0.34, 0.08 if fino else 0.34, 4.8), "andamio")
        for zz in (1.6, 3.4):
            m.caja((0, y0 - 0.9, zz), (22.2, 1.2, 0.06 if fino else 0.17), "tablon")


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
