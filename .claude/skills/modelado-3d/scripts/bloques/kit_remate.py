"""Kit de estaciones: remates del edificio de viajeros (Teo, 7-nov). Se ponen en los extremos o en el centro de una
fila de módulos de kit_cuerpo. Fachada al andén en -Y; origen en el centro de su planta, en el suelo.

Fotos E1: Sagunt (cuerpo central con frontón y reloj, ala con balaustrada) y Nules (hastial escalonado con óculo).
Medidas SIN VERIFICAR.
Variantes (TBD_VARIANTE):
  0 cuerpo central con frontón (8 m de fachada, adelantado 0,6 m, dos plantas y media; banda oscura para la placa y el
    reloj de Bruno) en granate 642030.
  1 hastial escalonado de Nules: muro de testero de 0,5 m que remata en escalones por encima del tejado, con óculo.
  2 torreta: 4 x 4 m hasta 12 m, tejado a cuatro aguas, esquinas granate y huecos por planta.
  3 ala baja con terraza: módulo de una planta con antepecho de balaustrada (macizo en el LOD1).
  4 pórtico de entrada: tres arcos en 8 m de fachada y 3 m de fondo, con cubierta plana.
  5 testero liso con pilastras granate y chimenea (cierre sencillo de una fila).
"""
import math
from taller import Taller
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual, hueco)

PRESUPUESTO = {0: (30, 1500), 1: (20, 800)}


def _prisma(t, y0, fondo, z, semi, alto, cara_frente="moldura", resto="tejado"):
    """Prisma triangular (frontón) con el frente en y0 y la cumbrera a lo largo de Y."""
    bm = t.bm
    P = [[bm.verts.new((-semi, y, z)), bm.verts.new((semi, y, z)), bm.verts.new((0, y, z + alto))] for y in (y0, y0 + fondo)]
    t._marcar([bm.faces.new((P[0][0], P[0][2], P[0][1]))], cara_frente)
    cs = [bm.faces.new((P[1][0], P[1][1], P[1][2])), bm.faces.new((P[0][0], P[0][1], P[1][1], P[1][0])),
          bm.faces.new((P[0][1], P[0][2], P[1][2], P[1][1])), bm.faces.new((P[0][2], P[0][0], P[1][0], P[1][2]))]
    t._marcar(cs, resto)


def construir(nombre="SM_KitRemate", lod=0, var=None, fondo=9.0, alto=8.0):
    v = var_actual(var)
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    hy = fondo / 2
    if v == 0:   # cuerpo central con frontón
        A, S, za = 7.98, 0.6, alto + 1.5
        y0 = -hy - S
        t.caja((0, -S / 2, za / 2), (A, fondo + S, za), "fachada")
        t.caja((0, -S / 2, 0.45), (A + 0.04, fondo + S + 0.1, 0.9), "zocalo")
        t.caja((0, y0 - 0.15, za + 0.17), (A + 0.6, 0.6, 0.34), "cornisa")
        t.tejado((0, -S / 2, za + 0.3), A, fondo + S, 2.4, "tejado", aguas=4)
        _prisma(t, y0 - 0.1, 3.0, za + 0.34, A / 2 + 0.3, 2.4)
        t.caja((0, y0 - 0.14, za + 1.15), (A - 1.6, 0.06, 1.1), "fachada")             # tímpano
        for x in (-A / 2 + 0.25, A / 2 - 0.25):
            t.caja((x, y0 - 0.06, za / 2 + 0.45), (0.5, 0.12, za - 0.9), "moldura")      # pilastras
        for x in (-2.0, 0.0, 2.0):
            hueco(t, fino, lod, x, y0, 1.55, 1.4, 2.9)
            hueco(t, fino, lod, x, y0, 5.6, 1.2, 1.9)
        t.caja((0, y0 - 0.08, za - 1.0), (5.2, 0.08, 1.1), "placa")                    # placa y reloj (decals de Bruno)
        if lod == 0:
            t.caja((0, y0 - 0.06, 4.0), (A + 0.1, 0.12, 0.2), "cornisa")
    elif v == 1:  # hastial escalonado (testero de Nules)
        e = 0.5
        t.caja((0, 0, alto / 2), (e, fondo + 0.6, alto), "fachada")
        t.caja((0, 0, 0.45), (e + 0.04, fondo + 0.7, 0.9), "recerco")
        cum = alto + min(2.6, 0.24 * fondo)
        n = 4
        for k in range(n):                                                           # escalones
            w = (fondo + 0.6) * (1 - k / n)
            z0 = alto + (cum + 0.8 - alto) * k / n
            dz = (cum + 0.8 - alto) / n
            t.caja((0, 0, z0 + dz / 2), (e, w, dz + 0.02), "fachada")
            t.caja((0, 0, z0 + dz + 0.06), (e + 0.12, w, 0.14), "recerco")
        t.cilindro((-e / 2 - 0.02, 0, alto + 0.9), 0.4 if lod else 0.35, 0.06, "X", 10, "recerco")   # óculo
        for y in (-hy / 2, hy / 2):                                                 # huecos de medio punto al testero (-X)
            for z in (2.0, 5.6):
                if z < alto:
                    t.caja((-e / 2 - 0.03, y, z), (0.06, 1.1, 1.8), "carpinteria")
                    t.caja((-e / 2 - 0.01, y, z), (0.04, 1.6, 2.3), "recerco")
                    t.caja((-e / 2 - 0.015, y, z + 1.27), (0.05, 1.3, 0.34), "recerco")
    elif v == 2:  # torreta
        A, H = 3.98, max(12.0, alto + 4.0)   # siempre por encima del cuerpo al que se arrima
        t.caja((0, 0, H / 2), (A, A, H), "fachada")
        t.caja((0, 0, 0.45), (A + 0.04, A + 0.04, 0.9), "zocalo")
        t.caja((0, 0, H + 0.15), (A + 0.5, A + 0.5, 0.3), "cornisa")
        t.tejado((0, 0, H + 0.3), A, A, 2.2, "tejado", aguas=4, vuelo=0.2)
        for sx in (-1, 1):
            for sy in (-1, 1):
                t.caja((sx * (A / 2 - 0.2), sy * (A / 2 + 0.02), H / 2), (0.4, 0.06, H - 0.9), "moldura")
        for z in [1.55] + [5.6 + 4.0 * k for k in range(int((H - 4.0) / 4.0))]:
            hueco(t, fino, lod, 0, -A / 2, z, 1.2, 1.9 if z > 2 else 2.9)
            hueco(t, fino, lod, 0, A / 2, z, 1.2, 1.9, cara=1)
        t.caja((0, -A / 2 - 0.08, H - 1.0), (1.4, 0.08, 1.4), "placa")                 # sitio del reloj
    elif v == 3:  # ala baja con terraza
        A, za = 3.98, 4.5
        f = fondo - 1.6
        t.caja((0, 0, za / 2), (A, f, za), "fachada")
        t.caja((0, 0, 0.45), (A + 0.02, f + 0.1, 0.9), "zocalo")
        t.caja((0, 0, za + 0.1), (A + 0.02, f + 0.3, 0.2), "cornisa")
        yb = -f / 2 + 0.1
        if lod == 0:
            for k in range(8):
                t.caja((-A / 2 + 0.25 + k * (A - 0.5) / 7, yb, za + 0.6), (0.14, 0.14, 0.8), "fachada")
            t.caja((0, yb, za + 1.05), (A, 0.22, 0.1), "cornisa")
        else:
            t.caja((0, yb, za + 0.65), (A, 0.2, 0.9), "fachada")
        for x in (-0.95, 0.95):
            hueco(t, fino, lod, x, -f / 2, 1.9, 1.1, 2.6)
    elif v == 4:  # pórtico de tres arcos
        A, F, H = 7.98, 3.0, 4.2
        yc = -hy - F / 2
        t.caja((0, yc, H + 0.2), (A, F, 0.4), "fachada")
        t.caja((0, yc - F / 2 - 0.05, H + 0.45), (A + 0.2, 0.3, 0.3), "cornisa")
        for x in (-A / 2 + 0.3, -A / 6, A / 6, A / 2 - 0.3):
            t.caja((x, yc - F / 2 + 0.3, H / 2), (0.6, 0.6, H), "fachada")             # pilares encalados
            t.caja((x, yc - F / 2 + 0.3, 0.45), (0.7, 0.7, 0.9), "zocalo")
        for k in range(3):                                                           # arcos (enjutas)
            xc = -A / 3 + k * A / 3
            t.caja((xc, yc - F / 2 + 0.3, H - 0.35), (A / 3 - 0.6, 0.5, 0.7), "fachada")
            t.caja((xc, yc - F / 2 + 0.3, H - 0.85), (A / 3 - 1.4, 0.5, 0.34), "fachada")
    else:         # testero liso con pilastras y chimenea
        e = 0.5
        t.caja((0, 0, alto / 2), (e, fondo + 0.6, alto), "fachada")
        t.caja((0, 0, 0.45), (e + 0.04, fondo + 0.7, 0.9), "zocalo")
        for y in (-hy, hy):
            t.caja((-e / 2 - 0.04, y, alto / 2 + 0.45), (0.12, 0.5, alto - 0.9), "moldura")
        _prisma_x = min(2.6, 0.24 * fondo)
        bm = t.bm
        za = alto - 0.05                                                           # se mete 5 cm en el muro (sin caras compartidas)
        e2 = e / 2 - 0.01
        P = [bm.verts.new((-e2, -hy - 0.3, za)), bm.verts.new((-e2, hy + 0.3, za)), bm.verts.new((-e2, 0, alto + _prisma_x)),
             bm.verts.new((e2, -hy - 0.3, za)), bm.verts.new((e2, hy + 0.3, za)), bm.verts.new((e2, 0, alto + _prisma_x))]
        cs = [bm.faces.new((P[0], P[2], P[1])), bm.faces.new((P[3], P[4], P[5])), bm.faces.new((P[0], P[1], P[4], P[3])),
              bm.faces.new((P[1], P[2], P[5], P[4])), bm.faces.new((P[2], P[0], P[3], P[5]))]
        t._marcar(cs, "fachada")
        t.caja((0, 1.5, alto + _prisma_x + 0.4), (0.7, 0.7, 2.2), "zocalo")             # chimenea
        for y, z in ((-2.0, 2.0), (2.0, 2.0), (-2.0, 5.6), (2.0, 5.6)):
            if z < alto:
                t.caja((-e / 2 - 0.03, y, z), (0.06, 1.2, 1.8), "carpinteria")
                t.caja((-e / 2 - 0.01, y, z), (0.04, 1.7, 2.3), "moldura")
    return t.objeto(nombre, bisel=0)
