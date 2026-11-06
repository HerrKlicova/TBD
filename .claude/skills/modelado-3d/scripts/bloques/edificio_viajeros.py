"""Edificio de viajeros de la estación de Sagunto: dos plantas y tejado a cuatro aguas (Lucía, ambientacion-sagunto.md,
pieza 2). Teo, 6-nov-2026.

MEDIDAS SIN VERIFICAR (bloque adelantado con medidas típicas mientras llegan las fotos de Paula; se ajusta con ellas):
20 x 9 m de planta, dos plantas de 4 m (alero a 8 m), tejado de 2,5 m con 0,6 m de vuelo. Fachada al andén (-Y) con
cinco huecos por planta: puertas de 1,4 x 3 m abajo y balcones de 1,2 x 1,9 m arriba, zócalo de 1 m, cornisa y la
placa del nombre (las letras y el reloj los pone Bruno). Colores de Lucía: enfoscado b4a080, zócalo 605c48,
carpinterías 283440, teja a86c50, placa 283440. Delante = -Y en Blender (+Z en Unity).
"""
from taller import Taller

PIEZAS = ["fachada", "zocalo", "carpinteria", "tejado", "placa", "cornisa", "chimenea", "metal"]
COLORES = {"fachada": "b4a080", "zocalo": "605c48", "carpinteria": "283440", "tejado": "a86c50", "placa": "283440",
           "cornisa": "94948c", "chimenea": "605c48", "metal": "1c200c"}
FACCION = "neutro"
PRESUPUESTO = {0: (200, 3000), 1: (100, 1500)}
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
        t.caja((x, 1.5, ALERO + 2.2), (0.7, 0.7, 2.0), "chimenea")
    xs = (-8.0, -4.0, 0.0, 4.0, 8.0)
    for cara in (-1, 1):                                                          # andén (-Y) y trasera (+Y)
        y = cara * (hy + 0.03)
        for x in xs:
            if cara < 0:
                t.caja((x, y, 1.6), (1.4, 0.06, 3.0), "carpinteria")              # puertas al andén
            else:
                t.caja((x, y, 2.0), (1.2, 0.06, 1.8), "carpinteria")              # ventanas traseras
            t.caja((x, y, 5.5), (1.2, 0.06, 1.9), "carpinteria")                  # ventanas de arriba
            if cara < 0:                                                          # balcones (solo LOD0)
                fino.caja((x, -hy - 0.3, 4.45), (1.8, 0.6, 0.12), "cornisa")
                fino.caja((x, -hy - 0.58, 4.95), (1.8, 0.04, 0.9), "metal")
    for cara in (-1, 1):                                                          # testeros: dos huecos por planta
        xx = cara * (hx + 0.03)
        for y in (-2.0, 2.0):
            t.caja((xx, y, 2.0), (0.06, 1.2, 1.8), "carpinteria")
            t.caja((xx, y, 5.5), (0.06, 1.2, 1.9), "carpinteria")
    # imposta entre plantas (solo LOD0) y placa del nombre sobre la puerta central
    fino.caja((0, 0, PLANTA), (LARGO + 0.16, FONDO + 0.16, 0.2), "cornisa")
    t.caja((0, -hy - 0.06, 7.2), (4.0, 0.08, 0.8), "placa")
    return t.objeto(nombre, bisel=0)
