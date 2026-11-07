"""Trozo de prueba del suelo (capa C) para que Eugenio lo vea a tamaño de juego. NO es una pieza del juego. Teo, 6-nov-2026.

Un cuadro de 11 × 11 m de suelo de día (c19e78, el de Lucía en §3) con las 3 matas y las 2 piedras de la muestra
repartidas con semilla fija, a densidad «descampado» (alta) en la mitad oeste y «recinto» (baja, solo al pie de un
muro) en la mitad este. Tres siluetas de zombi (cajas de 0,4 × 0,3 × 1,7 m en el valor del cuerpo medio, 303424)
solo para dar la escala y ver si la horda se sigue leyendo encima. A 64 px el cuadro sale a unos 0,17 m/px.
"""
import math, os, random
from taller import Taller
import suelo_matas as M
import suelo_piedras as P

VAR = 0
PIEZAS = ["suelo", "muro", "zombi"] + M.PIEZAS + P.PIEZAS
COLORES = {"suelo": "b4a080", "muro": "d4d0cc", "zombi": "303424", **M.COLORES, **P.COLORES}
FACCION = "neutro"
PRESUPUESTO = {0: (200, 9000), 1: (200, 9000)}
LADO = 11.0
NORMALES_ARRIBA = tuple(M.PIEZAS) + tuple(P.PIEZAS)
# el suelo va en b4a080 (L* 66), el de la paleta más cercano a c19e78 (L* 67,4)


def construir(nombre="SM_MuestraSuelo", lod=0):
    t = Taller(PIEZAS)
    h = LADO / 2
    t.caja((0, 0, -0.05), (LADO, LADO, 0.1), "suelo")
    t.caja((h - 0.4, 0, 1.0), (0.8, LADO, 2.0), "muro")                       # muro del recinto al este
    R = random.Random(42)
    for i in range(35):                                                     # descampado (oeste): alta
        x, y = R.uniform(-h + 0.3, -0.5), R.uniform(-h + 0.3, h - 0.3)
        tipo = R.choices(M.TIPOS, (5, 3, 1))[0]
        M.mata(t, tipo, x, y, semilla=i, lod=lod, escala=R.uniform(0.8, 1.2))
    for i in range(8):
        x, y = R.uniform(-h + 0.5, -0.5), R.uniform(-h + 0.5, h - 0.5)
        P.piedra(t, i % 2, x, y, semilla=100 + i, escala=R.uniform(0.8, 1.2))
    for i in range(12):                                                     # recinto (este): rala, al pie del muro
        x, y = R.uniform(h - 1.6, h - 0.85), R.uniform(-h + 0.3, h - 0.3)
        M.mata(t, R.choice(M.TIPOS[:2]), x, y, semilla=200 + i, lod=lod, escala=R.uniform(0.8, 1.1))
    for i in range(3):
        x, y = R.uniform(0.5, h - 2.2), R.uniform(-h + 1, h - 1)
        P.piedra(t, 1, x, y, semilla=300 + i, escala=0.8)
    for x, y in ((-2.5, -1.0), (-1.6, 2.4), (2.0, 0.5)):                   # zombis de escala
        t.caja((x, y, 0.85), (0.4, 0.3, 1.7), "zombi")
    return t.objeto(nombre, bisel=0)
