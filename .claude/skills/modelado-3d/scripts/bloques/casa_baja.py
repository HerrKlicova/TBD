"""Casa baja valenciana encalada con tejado de teja a dos aguas (Lucía, ambientacion-sagunto.md, pieza 5). Teo, 6-nov.
Módulo para que Marcos haga manzanas de 10-15 casas.

MEDIDAS SIN VERIFICAR (típicas, a la espera de las fotos de Paula): 8 x 7 m, una planta de 3,6 m con cámara bajo
cubierta, tejado a dos aguas de 2,2 m con 0,4 m de vuelo y la cumbrera paralela a la calle. Fachada a la calle (-Y)
con puerta y dos ventanas con reja; zócalo pintado. Colores: encalado d4d0cc (bone: la cal es más clara que el
enfoscado b4a080 de la estación), zócalo 605c48, carpinterías 283440, teja a86c50. Delante = -Y (+Z en Unity).
"""
from taller import Taller

PIEZAS = ["cal", "zocalo", "carpinteria", "tejado", "reja"]
COLORES = {"cal": "d4d0cc", "zocalo": "605c48", "carpinteria": "283440", "tejado": "a86c50", "reja": "1c200c"}
FACCION = "neutro"
PRESUPUESTO = {0: (100, 1500), 1: (50, 600)}
LARGO, FONDO, ALTO = 8.0, 7.0, 3.6


def construir(nombre="SM_CasaBaja", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    hy = FONDO / 2
    t.caja((0, 0, ALTO / 2), (LARGO, FONDO, ALTO), "cal")
    t.caja((0, 0, 0.45), (LARGO + 0.06, FONDO + 0.06, 0.9), "zocalo")
    # dos aguas con la cumbrera a lo largo de X: hastiales en los testeros (X) del color de la cal
    t.tejado((0, 0, ALTO), LARGO, FONDO, 2.2, "tejado", vuelo=0.4, aguas=2)
    for cara in (-1, 1):
        y = cara * (hy + 0.03)
        t.caja((0.0 if cara < 0 else 1.5, y, 1.15), (1.1, 0.06, 2.3), "carpinteria")   # puerta (calle / patio)
        for x in ((-2.6, 2.6) if cara < 0 else (-2.0,)):
            t.caja((x, y, 1.7), (1.0, 0.06, 1.3), "carpinteria")
            fino.caja((x, cara * (hy + 0.12), 1.7), (1.1, 0.04, 1.4), "reja")      # reja (solo LOD0)
    fino.caja((0.0, -hy - 0.25, 2.6), (1.6, 0.5, 0.08), "tejado")                  # tejadillo sobre la puerta
    return t.objeto(nombre, bisel=0)
