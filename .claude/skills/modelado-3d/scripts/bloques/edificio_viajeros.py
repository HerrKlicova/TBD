"""Edificio de viajeros de la estación de Sagunto: dos plantas y tejado a cuatro aguas (Lucía, ambientacion-sagunto.md,
pieza 2). Teo, 6-nov-2026.

MEDIDAS SIN VERIFICAR (bloque adelantado con medidas típicas mientras llegan las fotos de Paula; se ajusta con ellas):
20 x 9 m de planta, dos plantas de 4 m (alero a 8 m), tejado de 2,5 m con 0,6 m de vuelo. Fachada al andén (-Y) con
cinco huecos por planta: puertas de 1,4 x 3 m abajo y balcones de 1,2 x 1,9 m arriba, zócalo de 1 m, cornisa y la
placa del nombre (las letras y el reloj los pone Bruno). Colores de Lucía: enfoscado b4a080, zócalo 605c48,
carpinterías 283440, teja a86c50, placa 283440. Delante = -Y en Blender (+Z en Unity).

v2 (6-nov 17:30) con las fotos de Paula (E1_edificio_viajeros_1..3, Commons): la 2 es la estación real de Sagunt
(fachada crema, cuerpo central con frontón triangular y reloj, pilastras y recercados en rojo oscuro, ala baja con
terraza de balaustrada); la 1 y la 3 son Nules, de la misma línea (dos plantas, recercados de los huecos, zócalo).
Añadidos: cuerpo central adelantado 0,4 m con frontón, pilastras y recercados en 543820 (el rojo oscuro de la foto
está prohibido en objetos: se lleva al marrón oscuro más cercano por valor) y ala baja de 7 m con balaustrada al +X.
Las medidas siguen SIN VERIFICAR (estimadas a ojo con las fotos; no hay planos).
"""
import math, os
from taller import Taller

# Variantes (Eugenio, 6-nov: al menos 5). El edificio es único en Sagunto, así que las variantes son ESTADOS de la misma
# estación (para otras estaciones de la línea o para el avance de la partida): 0 intacto; 1 huecos de la planta baja
# tapiados con tablas; 2 chimenea caída y hueco en el tejado; 3 hollín de un incendio sobre las ventanas de arriba;
# 4 sacos terreros ante las puertas del andén; 5 balaustrada de la terraza rota y escombros.
VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)

PIEZAS = ["fachada", "zocalo", "carpinteria", "tejado", "placa", "cornisa", "chimenea", "metal", "moldura", "tabla",
          "hollin", "saco", "escombro"]
COLORES = {"fachada": "d4d0cc", "zocalo": "605c48", "carpinteria": "543820", "tejado": "a86c50", "placa": "48403c",
           "cornisa": "94948c", "chimenea": "605c48", "metal": "48403c",
           "moldura": "642030", "tabla": "845c28", "hollin": "48403c", "saco": "b4a080", "escombro": "94948c"}
# Lucía (17:25): la estación de Sagunt es blanca (cal d4d0cc, como la casa baja) con recercados, pilastras y frontón en
# granate oscuro 642030 (no es el rojo de alerta 982420: entregar con --permitir 642030); carpintería en 543820.
PERMITIR = ("642030",)
FACCION = "neutro"
# D-N80 (7-nov): nada por debajo de 48403c (283440 es de la horda; 1c200c y 303424 bajo el suelo); normales subidas
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (200, 3500), 1: (100, 1800)}
CUERPO = 7.0      # ancho del cuerpo central adelantado (foto 2)
SALIENTE = 0.4
ALA = 7.0         # largo del ala baja de la terraza
LARGO, FONDO, PLANTA = 20.0, 9.0, 4.0
ALERO = 2 * PLANTA


def construir(nombre="SM_EdificioViajeros", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    hx, hy = LARGO / 2, FONDO / 2
    t.caja((0, 0, ALERO / 2), (LARGO, FONDO, ALERO), "fachada")
    t.caja((0, 0, 0.5), (LARGO + 0.1, FONDO + 0.1, 1.0), "zocalo")
    t.caja((0, 0, ALERO + 0.15), (LARGO + 0.5, FONDO + 0.5, 0.3), "cornisa")      # cornisa bajo el alero
    t.tejado((0, 0, ALERO + 0.3), LARGO, FONDO, 2.5, "tejado", vuelo=0.6)
    for x in (-5.5, 5.5):                                                         # chimeneas
        if VAR == 2 and x > 0:   # chimenea caída sobre el faldón
            t.caja((x - 0.6, 2.2, ALERO + 1.5), (0.7, 2.0, 0.7), "chimenea", rot_x=math.radians(28))
            # hueco en el tejado: placa oscura de 3 x 2 m pegada al faldón trasero (no es el valor de la horda)
            t.caja((x + 1.0, 2.6, ALERO + 1.6), (3.0, 2.0, 0.06), "hollin", rot_x=math.radians(-26))
            continue
        t.caja((x, 1.5, ALERO + 2.2), (0.7, 0.7, 2.0), "chimenea")
    hc = CUERPO / 2
    # cuerpo central adelantado con su cornisa y un frontón triangular (foto 2: hastial con el reloj)
    t.caja((0, -hy - SALIENTE / 2 + 0.1, (ALERO - 0.05) / 2), (CUERPO, SALIENTE + 0.2, ALERO - 0.05), "fachada")
    t.caja((0, -hy - SALIENTE / 2 + 0.1, 0.5), (CUERPO + 0.1, SALIENTE + 0.3, 1.02), "zocalo")
    t.caja((0, -hy - 0.2, ALERO + 0.16), (CUERPO + 0.6, 0.9, 0.34), "cornisa")
    _fronton(t, -hy - SALIENTE - 0.05, 4.9, ALERO + 0.33, hc + 0.3, 2.3)   # fondo hasta meterse bajo el faldón (sin testero suelto detrás)
    # pilastras en las esquinas y en los cantos del cuerpo central (rojo oscuro de la foto -> 543820)
    for x, y in ((-hx + 0.25, -hy - 0.06), (hx - 0.25, -hy - 0.06), (-hc + 0.25, -hy - SALIENTE - 0.06),
                 (hc - 0.25, -hy - SALIENTE - 0.06)):
        t.caja((x, y, (ALERO + 1.0) / 2), (0.5, 0.12, ALERO - 1.1), "moldura")
    # ala baja con terraza y balaustrada (foto 2), al +X
    ax = hx + ALA / 2 - 0.1
    t.caja((ax, -0.6, 2.25), (ALA + 0.2, FONDO - 1.6, 4.5), "fachada")
    t.caja((ax, -0.6, 0.5), (ALA + 0.3, FONDO - 1.5, 1.02), "zocalo")
    t.caja((ax, -0.6, 4.6), (ALA + 0.4, FONDO - 1.3, 0.2), "cornisa")
    bal_y0, bal_y1, bal_x1 = -0.6 - (FONDO - 1.3) / 2 + 0.1, -0.6 + (FONDO - 1.3) / 2 - 0.1, hx + ALA + 0.05
    rotos = set(range(4, 10)) if VAR == 5 else set()                               # v5: balaustres caídos
    if lod == 0:   # balaustres y pasamanos
        for k in [k for k in range(14) if k not in rotos]:
            t.caja((hx + 0.3 + k * (ALA - 0.3) / 13, bal_y0, 5.1), (0.14, 0.14, 0.8), "fachada")
        for k in range(1, 12):
            t.caja((bal_x1, bal_y0 + k * (bal_y1 - bal_y0) / 12, 5.1), (0.14, 0.14, 0.8), "fachada")
        if VAR == 5:
            for x0, x1 in ((hx, hx + 1.9), (hx + 4.9, bal_x1)):
                t.caja(((x0 + x1) / 2 + 0.05, bal_y0, 5.55), (x1 - x0, 0.22, 0.1), "cornisa")
        else:
            t.caja(((hx + bal_x1) / 2 + 0.05, bal_y0, 5.55), (ALA + 0.05, 0.22, 0.1), "cornisa")
        t.caja((bal_x1, (bal_y0 + bal_y1) / 2, 5.55), (0.22, bal_y1 - bal_y0 + 0.2, 0.1), "cornisa")
    elif VAR == 5:  # LOD1: antepecho macizo con el tramo roto
        for x0, x1 in ((hx, hx + 1.9), (hx + 4.9, bal_x1)):
            t.caja(((x0 + x1) / 2 + 0.05, bal_y0, 5.15), (x1 - x0, 0.2, 0.9), "fachada")
        t.caja((bal_x1, (bal_y0 + bal_y1) / 2, 5.15), (0.2, bal_y1 - bal_y0 + 0.18, 0.9), "fachada")
    else:          # LOD1: antepecho macizo (los balaustres no llegan a 2 px)
        t.caja(((hx + bal_x1) / 2 + 0.05, bal_y0, 5.15), (ALA + 0.05, 0.2, 0.9), "fachada")
        t.caja((bal_x1, (bal_y0 + bal_y1) / 2, 5.15), (0.2, bal_y1 - bal_y0 + 0.18, 0.9), "fachada")
    for x in (hx + 1.6, hx + 3.5, hx + 5.4):                      # huecos del ala al andén
        t.caja((x, bal_y0 - 0.03, 2.0), (1.2, 0.06, 2.6), "carpinteria")
        fino.caja((x, bal_y0 - 0.01, 2.05), (1.5, 0.04, 2.95), "moldura")
    xs = (-8.0, -4.0, 0.0, 4.0, 8.0)
    for cara in (-1, 1):                                                          # andén (-Y) y trasera (+Y)
        for x in xs:
            y = cara * (hy + 0.03) - (SALIENTE if (cara < 0 and abs(x) < hc) else 0.0)
            if cara < 0:
                t.caja((x, y, 1.6), (1.4, 0.06, 3.0), "carpinteria")              # puertas al andén
            else:
                t.caja((x, y, 2.0), (1.2, 0.06, 1.8), "carpinteria")              # ventanas traseras
            t.caja((x, y, 5.5), (1.2, 0.06, 1.9), "carpinteria")                  # ventanas de arriba
            fino.caja((x, y - cara * 0.02, 5.55), (1.5, 0.04, 2.25), "moldura")    # recercados (fotos 1 y 3)
            if cara < 0:                                                          # balcones (solo LOD0)
                fino.caja((x, y + 0.03 - 0.3, 4.45), (1.8, 0.6, 0.12), "cornisa")
                fino.caja((x, y + 0.03 - 0.58, 4.95), (1.8, 0.04, 0.9), "metal")
    for cara in (-1,):                                                            # testero libre (el +X lo tapa el ala)
        xx = cara * (hx + 0.03)
        for y in (-2.0, 2.0):
            t.caja((xx, y, 2.0), (0.06, 1.2, 1.8), "carpinteria")
            t.caja((xx, y, 5.5), (0.06, 1.2, 1.9), "carpinteria")
    # imposta entre plantas (solo LOD0) y placa del nombre sobre la puerta central
    fino.caja((0, 0, PLANTA), (LARGO + 0.16, FONDO + 0.16, 0.2), "cornisa")
    fino.caja((0, -hy - SALIENTE / 2 + 0.1, PLANTA), (CUERPO + 0.16, SALIENTE + 0.36, 0.22), "cornisa")
    # banda oscura para los decals de Bruno (Lucía 16:51), vista desde el andén: a la izquierda la placa
    # placa_sagunto_x2.png (72x22, 3,6 x 1,1 m, centro x=-0,8) y a la derecha el reloj reloj_fachada_24.png
    # (1,1 x 1,1 m, centro x=+1,95); la banda va de x=-2,6 a 2,6 con el centro a 7,15 m de altura
    t.caja((0, -hy - SALIENTE - 0.06, 7.15), (5.2, 0.08, 1.1), "placa")   # en el cuerpo central (v2)
    _estado(t, fino, hx, hy, hc, bal_y0)
    return t.objeto(nombre, bisel=0)


def _estado(t, fino, hx, hy, hc, bal_y0):
    """Detalles de las variantes de estado (v1..v5) que no tocan la forma base."""
    xs = (-8.0, -4.0, 0.0, 4.0, 8.0)
    yf = lambda x: -hy - (SALIENTE if abs(x) < hc else 0.0)
    if VAR == 1:   # tablas clavadas sobre las puertas del andén (en aspa: se lee como «cerrado»)
        for x in xs:
            for ang in (35, -35):
                t.caja((x, yf(x) - 0.1 - (0.02 if ang > 0 else 0.0), 1.6), (0.3, 0.05, 3.3), "tabla", rot_y=math.radians(ang))
    if VAR == 3:   # hollín: manchas oscuras que suben desde las ventanas de arriba hacia el alero
        for x in (-4.0, 0.0, 8.0):
            t.caja((x, yf(x) - 0.08, 7.0), (1.6, 0.04, 1.8), "hollin")
    if VAR == 4:   # sacos terreros ante las puertas: un murete de 0,9 m en forma de U
        for x in (-4.0, 0.0, 4.0):
            y0 = yf(x) - 1.3
            for k in range(3):
                t.caja((x, y0, 0.2 + k * 0.3), (2.2 - k * 0.15, 0.55, 0.28), "saco")
            for sx in (-1, 1):
                t.caja((x + sx * 1.05, y0 + 0.6, 0.35), (0.5, 0.8, 0.6), "saco")
    if VAR == 5:   # escombros de la balaustrada al pie del ala
        for k, (dx, ang) in enumerate(((2.3, 20), (3.0, -35), (3.8, 60), (4.4, -10))):
            t.caja((hx + dx, bal_y0 - 1.0 - 0.3 * (k % 2), 0.2), (0.9, 0.35, 0.3), "escombro", rot_z=math.radians(ang))


def _fronton(t, y_frente, fondo, z, semiancho, alto):
    """Prisma triangular con la cara al andén en y_frente y la cumbrera a lo largo de Y (hastial de la foto 2)."""
    bm = t.bm
    caras = []
    ys = (y_frente, y_frente + fondo)
    P = [[bm.verts.new((-semiancho, y, z)), bm.verts.new((semiancho, y, z)), bm.verts.new((0, y, z + alto))] for y in ys]
    frente = bm.faces.new((P[0][0], P[0][2], P[0][1]))      # cara del frontón al andén: marco granate (Lucía)
    t._marcar([frente], "moldura")
    caras.append(bm.faces.new((P[1][0], P[1][1], P[1][2])))
    caras.append(bm.faces.new((P[0][0], P[0][1], P[1][1], P[1][0])))
    caras.append(bm.faces.new((P[0][1], P[0][2], P[1][2], P[1][1])))
    caras.append(bm.faces.new((P[0][2], P[0][0], P[1][0], P[1][2])))
    t._marcar(caras, "tejado")
    # tímpano en enfoscado, un poco por delante, con el canto en la cornisa
    T = [bm.verts.new(v) for v in ((-semiancho + 0.35, y_frente - 0.02, z + 0.12), (semiancho - 0.35, y_frente - 0.02, z + 0.12),
                                   (0, y_frente - 0.02, z + alto - 0.3))]
    T2 = [bm.verts.new((v.co.x, v.co.y - 0.06, v.co.z)) for v in T]
    cs = [bm.faces.new((T[0], T[1], T[2])), bm.faces.new((T2[0], T2[2], T2[1]))]
    for i in range(3):
        j = (i + 1) % 3
        cs.append(bm.faces.new((T[i], T2[i], T2[j], T[j])))
    t._marcar(cs, "fachada")
