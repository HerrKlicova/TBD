"""Escombros: capa C del suelo (D-N67). Teo, 6-nov-2026.

Fotos H5 de Paula (escombro de obra en un descampado y ladrillo roto): cascotes de hormigón y caliza claros, trozos de
bloque, ladrillo macizo rojizo apilado y grava. Reglas de Lucía: ≤ 150 triángulos, en 94948c y 88885c; la sombra en
6c7874 (el 8c5048 se lee como sangre); ladrillo en a86c50 (la teja de la paleta, L* 51, no es de la familia roja).
Normales hacia arriba (la misma luz que el suelo). Para el glacis valen todos: ninguno pasa de 0,3 m.
Variantes: 0 montón de cascotes; 1 trozo de bloque de hormigón partido; 2 ladrillos rotos apilados; 3 losa de acera
partida en tres; 4 montón de grava con dos cascotes.
"""
import math, os, random
from taller import Taller
from suelo_piedras import canto

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["arriba", "costado", "sombra", "ladrillo"]
COLORES = {"arriba": "94948c", "costado": "88885c", "sombra": "6c7874", "ladrillo": "a86c50"}
FACCION = "neutro"
PRESUPUESTO = {0: (1, 150), 1: (1, 150)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True


def construir(nombre="SM_Escombros", lod=0):
    t = Taller(PIEZAS)
    v = VAR % 5
    R = random.Random(700 + v)
    if v == 0:
        for i in range(6):
            a, d = R.uniform(0, 6.28), R.uniform(0, 0.4)
            canto(t, (d * math.cos(a), d * math.sin(a), 0.0), R.uniform(0.08, 0.18), 0.8, 40 + i)
    elif v == 1:                                  # bloque de 0,4 × 0,2 × 0,2 m partido, con los huecos oscuros
        t.caja((0, 0, 0.1), (0.4, 0.2, 0.2), "arriba", rot_z=0.3)
        t.caja((0.03, 0.01, 0.201), (0.12, 0.1, 0.004), "sombra", rot_z=0.3)
        t.caja((0.4, 0.25, 0.08), (0.22, 0.2, 0.16), "costado", rot_z=-0.4, rot_y=0.2)
    elif v == 2:                                  # ladrillos macizos de 0,24 × 0,115 × 0,05 m
        for i in range(7):
            x, y = R.uniform(-0.3, 0.3), R.uniform(-0.25, 0.25)
            z = 0.025 + (0.05 if i in (5, 6) else 0)
            t.caja((x, y, z), (0.24, 0.115, 0.05), "ladrillo", rot_z=R.uniform(0, 3.14))
    elif v == 3:                                  # losa de 0,6 × 0,6 × 0,05 partida en tres, una levantada
        t.caja((-0.15, 0, 0.025), (0.3, 0.6, 0.05), "arriba", rot_z=0.05)
        t.caja((0.18, 0.16, 0.03), (0.3, 0.28, 0.05), "arriba", rot_z=-0.1)
        t.caja((0.2, -0.18, 0.08), (0.3, 0.3, 0.05), "costado", rot_x=0.35, rot_z=0.2)
    else:                                         # montón de grava (cuña) con dos cascotes encima
        t.caja((0, 0, 0.1), (0.9, 0.7, 0.2), "costado", inclinar=0.3)
        t.caja((0, 0.15, 0.22), (0.5, 0.35, 0.06), "arriba")
        canto(t, (0.3, -0.1, 0.18), 0.1, 0.8, 71)
        canto(t, (-0.25, 0.1, 0.2), 0.08, 0.8, 72)
    return t.objeto(nombre + "_v%d" % v, bisel=0)
