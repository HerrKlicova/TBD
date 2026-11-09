"""Kit de estaciones: módulo de cuerpo de edificio de viajeros, 4 m de fachada (Teo, 7-nov). Se encadena en X (3,98 m).

Fachada al andén en -Y. Fondo por defecto 9 m (Sagunto); el montaje lo cambia (Nules 8 m, Castellón 13 m). Tejado a
dos aguas con la cumbrera a lo largo de X, así una fila de módulos da un tejado corrido y los extremos se cierran con
su hastial (o con un remate de kit_remate). Planta de 4 m (alero a 8 m en dos plantas, 4,5 m en una, 11,5 m en tres).
Colores y huecos de las fotos E1: Sagunt (blanca, recercados granate 642030, carpintería 543820) y Nules (blanca,
recercados, zócalo e imposta en gris 6c7874, huecos de medio punto). Medidas SIN VERIFICAR.
Variantes (TBD_VARIANTE):
  0 dos plantas Sagunto: puerta al andén y balcón arriba, recercado granate.
  1 dos plantas Nules: puerta y ventana de medio punto, recercado e imposta grises.
  2 una planta Nules: puerta de medio punto (cuerpo bajo o ala).
  3 una planta Sagunto: dos ventanas (ala de servicios).
  4 dos plantas abandonado (Nules tapiado): tablas en aspa en la puerta y ventana arriba sin carpintería.
  5 una planta, taquilla: fachada ocre b4a080 con puerta ancha y ventanilla.
  6 tres plantas Sagunto (cuerpo central alto de Castellón).
"""
import math
from taller import Taller
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual, hueco)

PRESUPUESTO = {0: (30, 900), 1: (20, 500)}
ANCHO, PLANTA = 3.98, 4.0
PLANTAS = {0: 2, 1: 2, 2: 1, 3: 1, 4: 2, 5: 1, 6: 3}


def alero(v):
    return {1: 4.5, 2: 8.0, 3: 11.5}[PLANTAS[v]]


def construir(nombre="SM_KitCuerpo", lod=0, var=None, fondo=9.0, tejado=True):
    v = var_actual(var)
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    nules = v in (1, 2, 4)
    marco = "recerco" if nules else "moldura"
    zoc = "recerco" if nules else "zocalo"
    fach = "fachada"
    za = alero(v)
    hy = fondo / 2
    t.caja((0, 0, za / 2), (ANCHO, fondo, za), fach)
    t.caja((0, 0, 0.45), (ANCHO - 0.004, fondo + 0.1, 0.9), zoc)
    t.caja((0, -hy - 0.15, za + 0.15), (ANCHO + 0.01, 0.5, 0.3), "cornisa")           # cornisa al andén
    t.caja((0, hy + 0.15, za + 0.15), (ANCHO + 0.01, 0.5, 0.3), "cornisa")            # y a la trasera
    if tejado:
        cs = t.tejado((0, 0, za + 0.3), ANCHO, fondo + 0.6, min(2.6, 0.24 * fondo), "tejado", aguas=2)
        t._marcar([f for f in cs if len(f.verts) == 3], fach)        # hastiales en el color del muro, no de la teja
    for p in range(1, PLANTAS[v]):                                                    # imposta entre plantas
        if nules or lod == 0:
            t.caja((0, -hy - 0.04, p * PLANTA), (ANCHO + 0.01, 0.12, 0.34 if lod else 0.2), "recerco" if nules else "cornisa")
    yf = -hy
    if v == 5:   # taquilla: fachada ocre, puerta ancha y ventanilla
        t.caja((0, yf - 0.02, za / 2 + 0.45), (ANCHO - 0.1, 0.04, za - 0.9), "costado")
        hueco(t, fino, lod, -0.7, yf - 0.04, 1.5, 1.8, 2.8, marco="moldura")
        hueco(t, fino, lod, 1.2, yf - 0.04, 1.6, 0.9, 0.9, marco="moldura")
        t.caja((1.2, yf - 0.25, 1.05), (1.2, 0.4, 0.08), "cornisa")                    # repisa de la ventanilla
    else:
        x = 0.0
        if v == 3:
            for xx in (-0.95, 0.95):
                hueco(t, fino, lod, xx, yf, 2.0, 1.1, 1.7, marco=marco)
        else:
            hueco(t, fino, lod, x, yf, 1.55, 1.4, 2.9, marco=marco, arco=nules,
                  pieza="madera" if v == 4 else "carpinteria")
        for p in range(1, PLANTAS[v]):
            z = p * PLANTA + 1.6
            hueco(t, fino, lod, x, yf, z, 1.2, 1.9, marco=marco, arco=nules,
                  pieza="tejado" if v == 1 else "carpinteria")                          # persiana a86c50 (foto N1)
            if v in (0, 6) and lod == 0:                                              # balcón (solo LOD0)
                t.caja((x, yf - 0.3, z - 1.05), (1.8, 0.6, 0.12), "cornisa")
                t.caja((x, yf - 0.58, z - 0.55), (1.8, 0.04, 0.9), "metal")
            elif v in (0, 6):                                                         # LOD1: repisa de 2 px
                t.caja((x, yf - 0.25, z - 1.05), (1.8, 0.5, 0.34), "cornisa")
    # trasera: una ventana por planta
    for p in range(PLANTAS[v]):
        hueco(t, fino, lod, 0.0, hy, p * PLANTA + 2.0, 1.2, 1.8, cara=1, marco=marco, arco=nules)
    if v == 4:   # abandonado: tablas en aspa sobre la puerta y hollín en la ventana de arriba
        for ang in (35, -35):
            t.caja((0, yf - 0.12 - (0.02 if ang > 0 else 0), 1.55), (0.3, 0.05, 3.2), "tabla", rot_y=math.radians(ang))
        t.caja((0, yf - 0.06, PLANTA + 2.9), (1.3, 0.04, 1.2), "hollin")
    return t.objeto(nombre, bisel=0)
