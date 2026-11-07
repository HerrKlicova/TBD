"""Caseta de compuertas de hormigón sobre el canal de riego de Nules, sitio N3 (Álvaro, nules-nivel.md §0b; 7-nov 08:20).
Teo, 7-nov.

Fotos N3 de Paula: N3_acequia_3 (caseta de compuertas y tajamar de hormigón sobre el Canal del Bovalar) y N3_acequia_1
(Canal de Nules: cauce ancho con muros de hormigón, barandilla, caseta al fondo). Es el tipo, no el lugar (lo dice el
nivel: esos canales están en la marjal). La caseta es un cubo de hormigón con cubierta plana y una ventana alta; se
apoya en una plataforma (tajamar) que entra en el canal, y de ella sale la pasarela de las compuertas que cruza el cauce.
En el juego es uno de los dos cuellos de botella del sur: por la pasarela se cruza el canal.
Pieza: un tramo de 12 m del canal (muros y agua) con la caseta, para que se pueda poner tal cual sobre la franja del canal
de Álvaro (Z 14-21, caseta en X 196-204). Canal de 7 m a lo largo de X, muros con la coronación a z = 0 (el suelo de la
huerta) y agua a -1,2 m. Caseta de 3,6 x 3,6 m y 4,2 m de alto. Medidas SIN VERIFICAR (estimadas sobre las fotos).
Colores: hormigón 94948c con manchas de humedad 88885c, compuertas y barandilla 605c48 (48403c en el LOD0 para la
barandilla), ventana 48403c (605c48 en el LOD1), agua 605c48 (el verde turbio de la foto), sacos b4a080.
Variantes (TBD_VARIANTE % 5):
  0 la de la foto: compuertas bajadas y ventana con reja.
  1 compuertas levantadas (el agua pasa): las tablas arriba y los husillos más altos.
  2 con escalera de bajada al canal y puerta de chapa en la caseta.
  3 abandonada: cubierta partida, barandilla caída en un tramo y óxido 7c3c18 en lo alto de las compuertas.
  4 fortificada: sacos terreros en la pasarela y en la plataforma, ventana tapiada (cuello de botella del sur).
"""
import math
import os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["hormigon", "humedad", "compuerta", "barandilla", "ventana", "agua", "saco", "oxido", "tabla"]
COLORES = {"hormigon": "94948c", "humedad": "88885c", "compuerta": "605c48", "barandilla": "48403c",
           "ventana": "48403c", "agua": "605c48", "saco": "b4a080", "oxido": "7c3c18", "tabla": "845c28"}
COLORES_LOD1 = {"barandilla": "605c48", "ventana": "605c48"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (60, 2000), 1: (40, 1000)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.12}
R = math.radians
LT, AC, ZA = 12.0, 7.0, -1.2           # largo del tramo, ancho del canal, cota del agua


def construir(nombre="SM_CasetaCompuertas", lod=0):
    v = VAR
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    g = 0.06 if lod == 0 else 0.3
    # --- tramo de canal: dos muros (coronación a z 0) y la lámina de agua
    for s in (-1, 1):
        t.caja((0, s * (AC / 2 + 0.25), -0.9), (LT, 0.5, 1.8), "hormigon")
        t.caja((0, s * (AC / 2 + 0.01), ZA + 0.35), (LT - 0.01, 0.04, 0.5), "humedad")   # cerco de humedad
    t.caja((0, 0, ZA - 0.05), (LT - 0.004, AC, 0.1), "agua")
    # --- plataforma (tajamar) en la orilla +Y que entra 2 m en el cauce, y caseta encima
    xp, yp = -2.5, AC / 2 - 0.5
    t.caja((xp, yp, -0.6), (4.6, 5.0, 1.2), "hormigon")
    t.caja((xp - 2.3 - 0.6, yp - 1.6, -0.6), (1.2, 1.4, 1.2), "hormigon", cuña=0.3, rot_z=R(90))  # proa del tajamar
    CX, CY, CH = 3.6, 3.6, 4.2
    xc, yc = xp, yp + 0.4
    roto = v == 3
    t.caja((xc, yc, CH / 2), (CX, CY, CH), "hormigon")
    if roto:   # cubierta partida: media losa caída hacia dentro
        t.caja((xc - CX / 4, yc, CH + 0.12), (CX / 2 + 0.2, CY + 0.2, 0.24), "hormigon")
        t.caja((xc + CX / 4, yc, CH - 0.5), (CX / 2, CY, 0.24), "hormigon", rot_y=R(25))
    else:
        t.caja((xc, yc, CH + 0.12), (CX + 0.3, CY + 0.3, 0.24), "hormigon")        # losa con vuelo
    t.caja((xc, yc - CY / 2 - 0.01, 0.6), (CX - 0.01, 0.04, 1.2), "humedad")       # zócalo de humedad
    # ventana alta hacia el canal (-Y) y otra hacia aguas abajo (+X)
    for (x, y, sx, sy) in ((xc, yc - CY / 2 - 0.03, 0.9, 0.06), (xc + CX / 2 + 0.03, yc, 0.06, 0.9)):
        if v == 4:
            t.caja((x, y, 2.9), (sx + 0.04, sy + 0.04, 1.0), "saco")              # tapiada con sacos
        else:
            t.caja((x, y, 2.9), (sx, sy, 1.0), "ventana")
            if lod == 0 and v != 3:
                for k in (-1, 0, 1):
                    t.caja((x + (k * 0.25 if sx > 0.5 else 0), y + (k * 0.25 if sy > 0.5 else 0), 2.9),
                           (0.03 if sx > 0.5 else sx + 0.02, 0.03 if sy > 0.5 else sy + 0.02, 1.0), "barandilla")
    if v == 2:   # puerta de chapa en el costado -X y escalera que baja al agua
        t.caja((xc - CX / 2 - 0.03, yc, 1.1), (0.06, 1.0, 2.2), "compuerta")
        for k in range(5):
            t.caja((xp + 2.6 + k * 0.35, AC / 2 - 0.45, -0.15 - k * 0.22), (0.38, 0.9, 0.24), "hormigon")   # se solapan: sin aristas compartidas
    if roto:
        t.caja((xc - 0.6, yc - CY / 2 - 0.02, 2.0), (0.06, 0.04, 2.6), "ventana", rot_y=R(20))   # grieta
    # --- pasarela de compuertas: dos pilas en el cauce, tablero a z 0,3 y barandilla
    xg = 2.5
    pilas = (-AC / 6, AC / 6)
    for y in pilas:
        t.caja((xg, y, ZA / 2 + 0.1), (1.0, 0.5, -ZA + 0.5), "hormigon", cuña=None)
    an = 1.4 if lod == 0 else 2.0       # LOD1: tablero más ancho, para que se lea como una banda clara
    t.caja((xg, 0, 0.15), (an, AC + 0.98, 0.3), "hormigon")                          # tablero (apoya en los muros)
    for s in (-1, 1):
        x = xg + s * (an / 2 - 0.04)
        if roto and s == 1:   # barandilla caída: un tramo colgando hacia el agua
            t.caja((x + 0.3, -1.0, -0.1), (g, 3.0, g), "barandilla", rot_x=R(10), rot_y=R(-60))
            t.caja((x, 2.2, 1.3), (g, 2.6, g), "barandilla")
        else:
            t.caja((x, 0, 1.3), (g, AC + 0.8, g), "barandilla")
        for y in ((-AC / 2, -AC / 6, AC / 6, AC / 2) if lod == 0 else (-AC / 2, AC / 2)):   # LOD1: solo los extremos
            if not (roto and s == 1 and y < 0):
                t.caja((x, y, 0.8), (g, g, 1.0), "barandilla")
    # compuertas entre muro y pila y entre pilas: tablas de chapa; husillo con volante por encima del tablero
    vanos = ((-AC / 2, pilas[0]), (pilas[0], pilas[1]), (pilas[1], AC / 2))
    sube = 1.0 if v == 1 else 0.0
    for (y0, y1) in vanos:
        yc_ = (y0 + y1) / 2
        ancho = (y1 - y0) - 0.5 + (0.25 if y0 == -AC / 2 or y1 == AC / 2 else 0)
        if lod == 0 or sube:
            t.caja((xg - 0.35, yc_, ZA + 0.5 + sube), (0.12, ancho, 1.2), "compuerta")
        else:   # LOD1 con las compuertas bajadas: solo asoma el canto (el resto, bajo el agua y a la sombra)
            t.caja((xg - 0.35, yc_, ZA + 0.15), (0.2, ancho, 0.3), "compuerta")
        if lod == 0:   # husillo y volante (en el LOD1 el contorno los juntaba en una mancha negra)
            t.caja((xg - 0.35, yc_, 0.9 + sube / 2), (g, g, 1.2 + sube), "barandilla")
            t.cilindro((xg - 0.35, yc_, 1.55 + sube), 0.25, g, "Z", 8, "compuerta")
        else:          # LOD1: un bloque del mecanismo de 2 px sobre el tablero
            t.caja((xg - 0.35, yc_, 0.5 + sube / 2), (0.34, 0.34, 0.4 + sube), "compuerta")
        if v == 3:
            t.caja((xg - 0.35, yc_, ZA + 1.12), (0.14, ancho * 0.7, 0.03), "oxido")   # óxido arriba de la tabla
    # --- fortificada: sacos terreros
    if v == 4:
        # parapetos de sacos de 1,2 m de ancho y dos hiladas a la entrada de la pasarela y en la plataforma
        for (x, y, rz) in ((xg, -AC / 2 - 0.9, 0), (xg, AC / 2 - 1.4, 0), (xp + 2.0, AC / 2 - 1.6, 90),
                           (xp - 1.6, AC / 2 - 2.8, 0)):
            dx, dy = (1.3, 0.5) if rz == 0 else (0.5, 1.3)
            t.caja((x, y, 0.5), (dx, dy, 0.36), "saco")
            t.caja((x, y, 0.84), (dx - 0.15, dy - 0.05, 0.32), "saco")
    # manchas de humedad en la caseta (LOD0)
    fino.caja((xc + 0.9, yc - CY / 2 - 0.02, 3.9), (0.9, 0.04, 0.5), "humedad")
    return t.objeto(nombre, bisel=0)
