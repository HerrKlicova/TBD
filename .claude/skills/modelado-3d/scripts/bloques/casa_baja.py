"""Casa baja valenciana encalada con tejado de teja a dos aguas (Lucía, ambientacion-sagunto.md, pieza 5). Teo, 6-nov.
Módulo para que Marcos haga manzanas de 10-15 casas.

MEDIDAS SIN VERIFICAR (típicas, a la espera de las fotos de Paula): 8 x 7 m, una planta de 3,6 m con cámara bajo
cubierta, tejado a dos aguas de 2,2 m con 0,4 m de vuelo y la cumbrera paralela a la calle. Fachada a la calle (-Y)
con puerta y dos ventanas con reja; zócalo pintado. Colores: encalado d4d0cc (bone: la cal es más clara que el
enfoscado b4a080 de la estación), zócalo 605c48, carpinterías 283440, teja a86c50. Delante = -Y (+Z en Unity).
"""
import os, random
from taller import Taller

# Variantes (Eugenio, 6-nov: al menos 5 por estructura). TBD_VARIANTE=1..5 elige una; 0 o sin definir = la base.
VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
_R = random.Random(1000 + VAR)
_CAL = ("d4d0cc", "b8b8b8", "d4d0cc", "b4a080", "d4d0cc", "b8b8b8")      # cal, cal gastada, enfoscado ocre
_ZOC = ("605c48", "283440", "6c7874", "605c48", "283440", "605c48")      # zócalo pintado (gris pardo, azul pizarra)

PIEZAS = ["cal", "zocalo", "carpinteria", "tejado", "reja", "tablas", "escombro"]
COLORES = {"cal": _CAL[VAR % 6], "zocalo": _ZOC[VAR % 6], "carpinteria": "283440", "tejado": "a86c50", "reja": "1c200c", "tablas": "605c48",
           "escombro": "94948c"}
FACCION = "neutro"
PRESUPUESTO = {0: (100, 1500), 1: (50, 600)}
LARGO, FONDO, ALTO = 8.0, 7.0, 3.6


def construir(nombre="SM_CasaBaja", lod=0):
    """Variantes: 1 más larga con tres ventanas; 2 estrecha y alta con tejadillo; 3 enfoscada en ocre con el tejado
    hundido en una esquina; 4 con las ventanas tapiadas con tablas; 5 con tapia de corral al lado y escombros."""
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    r = _R
    largo = LARGO + (0, 1.6, -1.4, 0.6, -0.4, 0.8)[VAR % 6] + (r.uniform(-0.2, 0.2) if VAR else 0)
    fondo = FONDO + (0, 0, -0.6, 0.4, 0, -0.4)[VAR % 6]
    alto = ALTO + (0, 0, 0.5, -0.2, 0, 0.2)[VAR % 6]
    hx, hy = largo / 2, fondo / 2
    t.caja((0, 0, alto / 2), (largo, fondo, alto), "cal")
    t.caja((0, 0, 0.45), (largo + 0.06, fondo + 0.06, 0.9), "zocalo")
    t.tejado((0, 0, alto), largo, fondo, 2.2 + (0, 0, 0.3, 0, -0.2, 0)[VAR % 6], "tejado", vuelo=0.4, aguas=2)
    puerta_x = (0.0, -1.2, 0.0, 1.0, -0.6, 0.8)[VAR % 6]
    ventanas = {1: (-3.2, 1.0, 3.2), 2: (1.6,), 3: (-2.4, 2.6), 4: (-2.6, 2.0), 5: (-2.4, 2.8)}.get(VAR, (-2.6, 2.6))
    for cara in (-1, 1):
        y = cara * (hy + 0.03)
        t.caja((puerta_x if cara < 0 else 1.5, y, 1.15), (1.1, 0.06, 2.3), "carpinteria")
        for x in (ventanas if cara < 0 else (-2.0,)):
            x = max(-hx + 0.8, min(hx - 0.8, x))
            t.caja((x, y, 1.7), (1.0, 0.06, 1.3), "carpinteria")
            if VAR == 4 and cara < 0:   # ventanas tapiadas con tablas (se lee también en el LOD1: 1 m de ancho)
                t.caja((x, cara * (hy + 0.08), 1.7), (1.2, 0.04, 1.4), "tablas")
            else:
                fino.caja((x, cara * (hy + 0.12), 1.7), (1.1, 0.04, 1.4), "reja")
    if VAR in (0, 2):
        fino.caja((puerta_x, -hy - 0.25, 2.6), (1.6, 0.5, 0.08), "tejado")       # tejadillo sobre la puerta
    if VAR == 3:   # tejado hundido: un hueco oscuro en el faldón de la calle y tejas caídas
        t.caja((-hx + 1.6, -hy / 2, alto + 1.0), (1.8, 1.6, 0.06), "reja", rot_x=0.5)
        t.caja((-hx + 1.0, -hy - 0.9, 0.15), (1.2, 0.8, 0.3), "tejado")
    if VAR == 5:   # tapia de corral y escombros
        t.caja((hx + 2.0, 0, 0.9), (4.0, 0.3, 1.8), "cal")
        t.caja((hx + 4.0 - 0.15, hy / 2 + 0.1, 0.9), (0.3, hy + 0.2, 1.8), "cal")
        t.caja((-hx - 0.8, -hy + 0.6, 0.2), (1.0, 1.4, 0.4), "escombro")
    return t.objeto(nombre, bisel=0)
