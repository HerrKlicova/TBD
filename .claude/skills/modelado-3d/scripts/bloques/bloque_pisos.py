"""Bloque de pisos de los años 70: cuatro plantas, cubierta plana con depósitos (Lucía, ambientacion-sagunto.md,
pieza 5). Teo, 6-nov. Módulo para las manzanas de fuera del recinto.

MEDIDAS SIN VERIFICAR (típicas, a la espera de las fotos de Paula): 14 x 10 m, bajo comercial de 3,4 m con persianas
y tres plantas de 2,9 m (12,1 m al forjado), peto de 1 m en la azotea con dos depósitos y caseta de escalera.
Fachada a la calle (-Y) con balcones corridos por planta y ventanas en las otras caras. Colores: enfoscado b4a080,
balcones y peto 94948c, carpinterías y persianas 283440, bajo 605c48. Delante = -Y (+Z en Unity).
"""
from taller import Taller

PIEZAS = ["fachada", "bajo", "balcon", "carpinteria", "azotea", "deposito"]
COLORES = {"fachada": "b4a080", "bajo": "605c48", "balcon": "94948c", "carpinteria": "283440", "azotea": "94948c",
           "deposito": "6c7874"}
FACCION = "neutro"
PRESUPUESTO = {0: (150, 3000), 1: (80, 1200)}
LARGO, FONDO, BAJO, PLANTA, N = 14.0, 10.0, 3.4, 2.9, 3


def construir(nombre="SM_BloquePisos", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    hx, hy = LARGO / 2, FONDO / 2
    alto = BAJO + PLANTA * N
    t.caja((0, 0, alto / 2), (LARGO, FONDO, alto), "fachada")
    t.caja((0, 0, BAJO / 2), (LARGO + 0.06, FONDO + 0.06, BAJO), "bajo")
    t.caja((0, 0, alto + 0.03), (LARGO - 0.4, FONDO - 0.4, 0.06), "azotea")       # suelo de la azotea
    for s in (-1, 1):                                                              # peto
        t.caja((0, s * (hy - 0.1), alto + 0.5), (LARGO - 0.04, 0.2, 1.0), "balcon")   # sin aristas comunes con la fachada
        t.caja((s * (hx - 0.1), 0, alto + 0.5), (0.2, FONDO - 0.5, 1.0), "balcon")   # sin esquinas coincidentes
    t.caja((3.5, 2.0, alto + 1.2), (3.0, 3.0, 2.4), "fachada")                     # caseta de escalera
    for x in (-3.0, -1.6):                                                         # depósitos de agua
        t.cilindro((x, 2.5, alto + 0.6), 0.55, 1.2, "Z", 8, "deposito")
    xs = (-5.0, -1.7, 1.7, 5.0)
    for x in xs:                                                                   # persianas del bajo (calle)
        t.caja((x, -hy - 0.05, 1.4), (2.4, 0.06, 2.6), "carpinteria")
    for k in range(N):
        z = BAJO + PLANTA * k
        t.caja((0, -hy - 0.55, z + 0.08), (LARGO - 1.0, 1.1, 0.16), "balcon")     # balcón corrido
        fino.caja((0, -hy - 1.08, z + 0.6), (LARGO - 1.0, 0.06, 1.0), "balcon")   # antepecho (solo LOD0)
        for x in xs:
            t.caja((x, -hy - 0.03, z + 1.25), (1.6, 0.06, 2.1), "carpinteria")    # balconeras
            t.caja((x, hy + 0.03, z + 1.45), (1.2, 0.06, 1.3), "carpinteria")     # ventanas traseras
        for s in (-1, 1):
            t.caja((s * (hx + 0.03), 0, z + 1.45), (0.06, 1.2, 1.3), "carpinteria")
    return t.objeto(nombre, bisel=0)
