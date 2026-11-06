"""Puente ferroviario sobre el Palancia (Álvaro, docs/niveles/sagunto-nivel.md §5, encargo 2). Teo, 6-nov-2026.

Tramo metálico de celosía Warren de tablero inferior (el tren pasa por dentro), de una luz de 28 m, para vía doble,
sobre dos estribos de sillería. La vía va a lo largo de X. MEDIDAS SIN VERIFICAR (típicas de un tramo metálico de
línea convencional, a falta de las fotos de Paula): vigas de celosía de 6,5 m de alto (gálibo de la catenaria), 10 m
entre ejes de vigas, 7 recuadros de 4 m, arriostramiento superior en cada nudo, tablero con balasto. Colores: acero
b8b8b8 (gris claro de obra pública: en sombra, el 6c7874 y el 94948c se perdían contra el tablero), tablero y balasto 48403c, estribos de sillería b4a080 con remate 94948c.
En el LOD1 los cordones son de 0,5 m y las diagonales de 0,36 m (2 px).
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["acero", "tablero", "balasto", "sillar", "remate"]
COLORES = {"acero": "b8b8b8", "tablero": "605c48", "balasto": "48403c", "sillar": "b4a080", "remate": "94948c"}
FACCION = "neutro"
PRESUPUESTO = {0: (100, 2500), 1: (60, 1200)}
UMBRAL_CONTORNO = {1: 0.2}   # LOD1: la viga de delante queda en la cara en sombra (-Y); el contorno por salto de profundidad la separa del tablero
LUZ, ANCHO, ALTO, N = 28.0, 10.0, 6.5, 7


def construir(nombre="SM_PuentePalancia", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    hx, hy = LUZ / 2, ANCHO / 2
    zt = 0.0                                          # cota del carril = 0 (el río queda debajo, en el terreno)
    c = 0.4 if lod == 0 else 0.7                      # canto de los cordones
    d = 0.25 if lod == 0 else 0.5                     # diagonales (LOD1: 3 px, para que no se las coma el contorno)
    dy = 0.4 if lod == 0 else 0.9                     # ancho de ala: desde 55° se ve sobre todo la cara de arriba
    paso = LUZ / N
    # tablero y balasto
    t.caja((0, 0, zt - 0.9), (LUZ + 0.4, ANCHO - 0.6, 0.7), "tablero")
    t.caja((0, 0, zt - 0.4), (LUZ + 0.3, ANCHO - 1.4, 0.3), "balasto")
    for sy in (-1, 1):
        y = sy * hy
        t.caja((0, y, zt - 0.7), (LUZ + 0.4, c, c * 1.6), "acero")                         # cordón inferior
        t.caja((0, y, zt + ALTO), (LUZ - paso + d, max(c, dy), c), "acero")                         # cordón superior
        zb_, zt_ = zt - 0.5, zt + ALTO
        for k in range(N):                                                                  # diagonales Warren
            xb0, xt, xb1 = -hx + k * paso, -hx + (k + 0.5) * paso, -hx + (k + 1) * paso
            for (xa, za, xb, zb) in ((xb0, zb_, xt, zt_), (xt, zt_, xb1, zb_)):
                L = math.hypot(xb - xa, zb - za)
                ang = math.atan2(zb - za, xb - xa)
                t.caja(((xa + xb) / 2, y, (za + zb) / 2), (L, dy, d), "acero", rot_y=-ang)
        for k in range(N):                                                                  # montantes (solo LOD0)
            fino.caja((-hx + (k + 0.5) * paso, y, zt + ALTO / 2 - 0.2), (0.15, 0.15, ALTO - 0.6), "acero")
    # arriostramiento superior: travesaños en los nudos altos y cruces de San Andrés (solo LOD0)
    for k in range(0, N, 2):
        t.caja((-hx + (k + 0.5) * paso, 0, zt + ALTO + 0.05), (d, ANCHO - 0.2, d), "acero")
    if lod == 0:
        for k in range(0, N - 2, 2):
            x0, x1 = -hx + (k + 0.5) * paso, -hx + (k + 2.5) * paso
            L = math.hypot(x1 - x0, ANCHO)
            for s in (-1, 1):
                t.caja(((x0 + x1) / 2, 0, zt + ALTO + 0.1 + 0.05 * s), (L, 0.1, 0.1), "acero",
                       rot_z=s * math.atan2(ANCHO, x1 - x0))
    # estribos de sillería con remate (el agua y las orillas, en el terreno)
    for sx in (-1, 1):
        xe = sx * (hx + 2.0)
        t.caja((xe, 0, zt - 4.0), (4.0, ANCHO + 2.0, 6.0), "sillar")
        t.caja((xe, 0, zt - 0.85), (4.2, ANCHO + 2.2, 0.3), "remate")
        for sy in (-1, 1):                                                                  # aletas
            t.caja((xe + sx * 2.5, sy * (hy + 1.6), zt - 3.0), (5.0, 0.8, 4.0), "sillar")
        # hiladas de la sillería (solo LOD0): juntas en el mismo tono un escalón más oscuro
        for k in range(4):
            fino.caja((xe - sx * 2.02, 0, zt - 6.0 + 1.2 * (k + 1)), (0.02, ANCHO + 1.9, 0.08), "remate")
    return t.objeto(nombre, bisel=0)
