"""Bloque de pisos de los años 70: cuatro plantas, cubierta plana con depósitos (Lucía, ambientacion-sagunto.md,
pieza 5). Teo, 6-nov. Módulo para las manzanas de fuera del recinto.

MEDIDAS SIN VERIFICAR (típicas, a la espera de las fotos de Paula): 14 x 10 m, bajo comercial de 3,4 m con persianas
y tres plantas de 2,9 m (12,1 m al forjado), peto de 1 m en la azotea con dos depósitos y caseta de escalera.
Fachada a la calle (-Y) con balcones corridos por planta y ventanas en las otras caras. Colores: enfoscado b4a080,
balcones y peto 94948c, carpinterías y persianas 283440, bajo 605c48. Delante = -Y (+Z en Unity).
"""
import os, random
from taller import Taller

# Variantes (Eugenio, 6-nov): TBD_VARIANTE=1..5; 0 = base.
VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
_R = random.Random(2000 + VAR)
_FACH = ("b4a080", "94948c", "a86c50", "b4a080", "d4d0cc", "94948c")   # enfoscado, gris, ladrillo, ..., blanco

PIEZAS = ["fachada", "bajo", "balcon", "carpinteria", "azotea", "deposito", "quemado", "toldo"]
COLORES = {"fachada": _FACH[VAR % 6], "bajo": "605c48", "balcon": "94948c", "carpinteria": "283440", "azotea": "94948c",
           "deposito": "6c7874", "quemado": "1c200c", "toldo": "70784c"}
FACCION = "neutro"
PRESUPUESTO = {0: (150, 3000), 1: (80, 1200)}
LARGO, FONDO, BAJO, PLANTA, N = 14.0, 10.0, 3.4, 2.9, 3


def construir(nombre="SM_BloquePisos", lod=0):
    """Variantes: 1 de cinco plantas, gris; 2 de ladrillo con balcones sueltos; 3 de tres plantas más ancho con un piso
    quemado (ventanas y hollín en 1c200c); 4 blanco con toldos; 5 de cuatro plantas, gris, sin depósitos y con la caseta
    al otro lado."""
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    n = (N, N + 2, N + 1, N, N + 1, N + 1)[VAR % 6]
    largo = LARGO + (0, 0, -2.0, 4.0, 0, 1.0)[VAR % 6]
    hx, hy = largo / 2, FONDO / 2
    alto = BAJO + PLANTA * n
    t.caja((0, 0, alto / 2), (largo, FONDO, alto), "fachada")
    t.caja((0, 0, BAJO / 2), (largo + 0.06, FONDO + 0.06, BAJO), "bajo")
    t.caja((0, 0, alto + 0.03), (largo - 0.4, FONDO - 0.4, 0.06), "azotea")
    for s_ in (-1, 1):
        t.caja((0, s_ * (hy - 0.1), alto + 0.5), (largo - 0.04, 0.2, 1.0), "balcon")
        t.caja((s_ * (hx - 0.1), 0, alto + 0.5), (0.2, FONDO - 0.5, 1.0), "balcon")
    cx = 3.5 if VAR != 5 else -3.5
    t.caja((cx, 2.0, alto + 1.2), (3.0, 3.0, 2.4), "fachada")
    if VAR != 5:
        for x in ((-3.0, -1.6) if VAR != 3 else (-4.5, -3.1, -1.7)):
            t.cilindro((x, 2.5, alto + 0.6), 0.55, 1.2, "Z", 8, "deposito")
    paso = (largo - 1.0) / 4
    xs = [-hx + 0.5 + paso * (k + 0.5) for k in range(4)]
    for x in xs:
        t.caja((x, -hy - 0.05, 1.4), (min(2.4, paso - 0.6), 0.06, 2.6), "carpinteria")
    quemado = 1 if VAR == 3 else -1
    for k in range(n):
        z = BAJO + PLANTA * k
        if VAR == 2:   # balcones sueltos, uno por hueco
            for x in xs:
                t.caja((x, -hy - 0.45, z + 0.08), (2.0, 0.9, 0.16), "balcon")
                fino.caja((x, -hy - 0.88, z + 0.6), (2.0, 0.06, 1.0), "balcon")
        else:
            t.caja((0, -hy - 0.55, z + 0.08), (largo - 1.0, 1.1, 0.16), "balcon")
            fino.caja((0, -hy - 1.08, z + 0.6), (largo - 1.0, 0.06, 1.0), "balcon")
        pieza = "quemado" if k == quemado else "carpinteria"
        for x in xs:
            t.caja((x, -hy - 0.03, z + 1.25), (1.6, 0.06, 2.1), pieza)
            t.caja((x, hy + 0.03, z + 1.45), (1.2, 0.06, 1.3), pieza)
            if VAR == 4 and lod == 0 and (k + int(x)) % 2 == 0:   # toldos
                t.caja((x, -hy - 0.5, z + 2.45), (1.8, 0.9, 0.06), "toldo", rot_x=-0.35)
        for s_ in (-1, 1):
            t.caja((s_ * (hx + 0.03), 0, z + 1.45), (0.06, 1.2, 1.3), pieza)
        if k == quemado:   # hollín sobre las ventanas quemadas (cambio de valor plano: se lee en el LOD1)
            t.caja((0, -hy - 0.04, z + 2.55), (largo - 2.0, 0.04, 0.5), "quemado")
    return t.objeto(nombre, bisel=0)
