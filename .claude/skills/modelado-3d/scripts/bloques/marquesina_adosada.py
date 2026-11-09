"""Marquesina adosada del edificio de viajeros de Nules (Álvaro, 8-nov, nules-nivel.md §2 y §6; fotos N1_2, N1_3 y N1_5
de Paula: marquesina antigua de celosía metálica adosada a la fachada de la vía, que cubre toda la franja hasta el andén,
con pilares y valla). Teo (3), 8-nov.

21 × 16,8 m: de la fachada (X 104,5–125,5, Z 96) al borde del andén (Z 79,2); centro en (115; 0; 87,6), yaw 180.
Paula estima unos 13 m de vuelo (margen de 13 a 17 m). Celosía y colores del kit (kit_marquesina, D-N123): fundición
verde 50541c (70784c en el LOD1), chapa 94948c con ondas 6c7874 (bandas de 1 m en el LOD1), canalón 48403c (605c48 en
el LOD1). Cerchas de celosía cada 3 m apoyadas en la fachada, en una fila de pilares intermedia y en la fila de pilares
del borde del andén, con una valla baja entre los pilares del borde. Cubierta a un agua que vierte hacia la vía: 5,0 m
en la fachada y 4,3 m en el borde. Medidas SIN VERIFICAR (estimadas en las fotos).
Origen: centro de la planta, en el suelo. La fachada del edificio queda en y = +8,4 y el borde del andén en y = −8,4
(con yaw 180, −Y de Blender mira a −Z de Unity, hacia la vía).
Variantes (TBD_VARIANTE % 5): 0 la de las fotos; 1 con chapas arrancadas; 2 con un tramo hundido en el borde;
3 solo la estructura (sin chapa); 4 fortificada: tablones en la valla y sacos entre los pilares del borde.
"""
import math
from taller import Taller
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual, bandas_chapa)

PRESUPUESTO = {0: (120, 6000), 1: (60, 2500)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
LX, LY = 21.0, 16.8
ZF, ZB = 5.0, 4.3                  # alto de la cubierta en la fachada (+Y) y en el borde (−Y)
PASO = 3.0
YP = (-8.0, 0.0)                   # filas de pilares: borde del andén e intermedia


def _z(y):
    return ZB + (ZF - ZB) * (y + LY / 2) / LY


def construir(nombre="SM_MarquesinaAdosada", lod=0):
    v = var_actual() % 5
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.08 if fino else 0.34
    r = 0.12 if fino else 0.18
    a = math.atan2(ZF - ZB, LY)
    xs = [-LX / 2 + 0.25 + k * (LX - 0.5) / 7 for k in range(8)]
    hundido = v == 2
    for i, x in enumerate(xs):
        caida = hundido and i in (2, 3)
        # pilares
        for yp in YP:
            if caida and yp == YP[0]:
                continue
            hz = _z(yp) - 0.55
            t.cilindro((x, yp, hz / 2), r, hz, "Z", 8 if fino else 6, "pilar")
            if fino:
                t.cilindro((x, yp, 0.15), r + 0.08, 0.3, "Z", 8, "pilar")
                t.caja((x, yp, hz - 0.12), (0.4, 0.4, 0.24), "pilar")
        # cercha de celosía de la fachada al borde, siguiendo la pendiente
        y0, y1 = -LY / 2 + 0.1, LY / 2 - 0.15
        if caida:
            y0 = -1.0
        yc, lc = (y0 + y1) / 2, y1 - y0
        zc = _z(yc) - 0.1
        if fino:
            for dz in (0.0, -0.5):
                t.caja((x, yc, zc + dz), (0.08, lc, 0.08), "pilar", rot_x=a)
            n = int(lc / 0.6)
            for k in range(n):
                yy = y0 + 0.3 + k * (lc - 0.6) / max(1, n - 1)
                t.caja((x, yy, _z(yy) - 0.35), (0.06, 0.06, 0.6), "pilar", rot_x=math.radians(30 if k % 2 else -30))
        else:
            t.caja((x, yc, zc - 0.25), (0.34, lc, 0.55), "pilar", rot_x=a)
        if caida:                                      # el trozo del borde cuelga hacia el andén
            t.caja((x, -4.6, 2.4), (g * 1.5 if fino else 0.34, 7.6, 0.4 if fino else 0.4), "pilar", rot_x=math.radians(-28))
    # vigas longitudinales sobre las filas de pilares y en la fachada
    for yp in YP + (LY / 2 - 0.2,):
        if hundido and yp == YP[0]:
            for xa, xb in ((-LX / 2, xs[2] - 0.5), (xs[3] + 0.5, LX / 2)):
                t.caja(((xa + xb) / 2, yp, _z(yp) - 0.45), (xb - xa, 0.22 if fino else 0.34, 0.25), "pilar")
            continue
        t.caja((0, yp, _z(yp) - 0.45), (LX - 0.02, 0.22 if fino else 0.34, 0.25), "pilar")
    # cubierta
    if v != 3:
        tramos = [(-LX / 2, LX / 2)]
        if hundido:
            tramos = [(-LX / 2, xs[2] - 0.6), (xs[3] + 0.6, LX / 2)]
        faltan = {3, 4, 9, 14, 15, 16} if v == 1 else set()
        for xa, xb in tramos:
            lx = xb - xa
            xc = (xa + xb) / 2
            t.caja((xc, 0, (ZF + ZB) / 2 + 0.05), (lx, LY / math.cos(a), 0.06), "chapa", rot_x=a)
            if fino:
                n = int(lx / 0.5)
                for k in range(n):
                    xk = xa + 0.25 + k * lx / n
                    if v == 1 and int((xk + LX / 2) / 1.2) in faltan:
                        continue
                    t.caja((xk, 0, (ZF + ZB) / 2 + 0.09), (0.25, LY / math.cos(a), 0.02), "onda", rot_x=a)
            else:
                bandas_chapa(t, xc, lx, 0, LY, (ZF + ZB) / 2 + 0.07, a)
            t.caja((xc, -LY / 2 - 0.1, ZB - 0.05), (lx, 0.25, 0.25), "canalon")
        if v == 1:                                     # huecos de chapa arrancada: se ven las cerchas
            for k, (xh, yh) in enumerate(((-5.5, -3.0), (2.0, 2.5), (6.5, -5.0))):
                t.caja((xh, yh, _z(yh) + 0.1), (2.4, 3.0, 0.04), "hollin", rot_x=a)
        if hundido:                                    # chapa caída sobre el andén
            t.caja(((xs[2] + xs[3]) / 2, -4.2, 2.3), (xs[3] - xs[2] + 0.6, 7.8, 0.06), "chapa", rot_x=math.radians(-26))
    # valla baja entre los pilares del borde (barrotes en el LOD0; lámina en el LOD1)
    yv = YP[0] - 0.05
    for i in range(7):
        xa, xb = xs[i] + 0.2, xs[i + 1] - 0.2
        if hundido and i == 2:
            continue
        xc, lx = (xa + xb) / 2, xb - xa
        t.caja((xc, yv, 1.05), (lx, 0.06 if fino else 0.34, 0.08 if fino else 0.17), "pilar")
        if fino:
            for k in range(int(lx / 0.25)):
                t.caja((xa + 0.125 + k * 0.25, yv, 0.55), (0.03, 0.03, 1.0), "pilar")
        else:
            t.caja((xc, yv, 0.55), (lx, 0.04, 1.0), "pilar")
        if v == 4:                                     # tablones y sacos por fuera
            t.caja((xc, yv - 0.08, 0.6), (lx, 0.05, 1.1), "tabla", rot_z=0.004 * (i - 3))
            t.caja((xc, yv - 0.6, 0.45), (lx - 0.1, 0.8, 0.9), "hollin", rot_z=0.03)            # sacos 605c48
    return t.objeto(nombre, bisel=0)
