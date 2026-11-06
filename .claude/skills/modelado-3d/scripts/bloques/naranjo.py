"""Naranjo para las hileras de detrás de las casas (Lucía, ambientacion-sagunto.md, pieza 6). Teo, 6-nov. Un solo
modelo instanciado.

MEDIDAS SIN VERIFICAR (típicas de un naranjo de huerta adulto, a la espera de las fotos de Paula): 3,6 m de alto,
copa redondeada de 3,4 m de diámetro que arranca a 0,7 m (el naranjo de cultivo tiene la copa baja y densa), tronco
de 0,15 m de radio. Colores: copa con valor partido, arriba olive_light 88885c y costados olive 70784c (valor medio, Lucía); tronco 605c48;
naranjas en olive_yellow 847c34 solo en el LOD0 (el naranja de la paleta, e08c30, es el ámbar de los avisos y está
prohibido en objetos).
"""
import math, os, random
from taller import Taller

# Variantes (Eugenio, 6-nov): TBD_VARIANTE=1..5; 0 = base. Escala, copa más o menos achatada, inclinación, poda y
# cantidad de fruta.
VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
_R = random.Random(3000 + VAR)

PIEZAS = ["copa", "tronco", "fruta"]
COLORES = {"copa": "70784c", "tronco": "605c48", "fruta": "847c34"}
# Lucía (16:55): la copa no puede caer en el valor de la horda (1c200c/303424). En albedo, costados 70784c y arriba
# 88885c: con la luz del juego los costados quedan cerca de 50541c, un valor medio
VALOR_PIEZAS = {"copa": ("88885c", "70784c")}
FACCION = "neutro"
PRESUPUESTO = {0: (60, 600), 1: (30, 200)}


def construir(nombre="SM_Naranjo", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    r = _R
    k = (1.0, 0.85, 1.12, 0.95, 1.05, 0.9)[VAR % 6]                  # escala general
    ach = (1.0, 1.1, 0.9, 1.0, 0.85, 1.15)[VAR % 6]                  # >1 más alto, <1 más achatado
    dx, dy = ((0, 0), (0.15, 0), (0, -0.2), (-0.1, 0.1), (0.2, 0.15), (0, 0))[VAR % 6]   # copa desplazada (inclinado)
    t.cilindro((0, 0, 0.45 * k), 0.15 * k, 0.9 * k, "Z", 6, "tronco")
    lados = 10 if lod == 0 else 8
    capas = [(1.0, 1.2, 0.6, 1.68), (1.58, 1.7, 0.6, 1.62), (2.3, 1.6, 0.9, 1.1), (3.0, 1.08, 0.55, 0.35)]
    if VAR == 3:   # poda reciente: sin casquete, copa plana arriba
        capas = capas[:3]
    for z, r1, h, r2 in capas:
        zz = 0.7 * k + (z - 0.7) * k * ach
        t.cilindro((dx * (z - 0.7), dy * (z - 0.7), zz), r1 * k, h * k * ach, "Z", lados, "copa", r2=r2 * k)
    nf = (9, 6, 12, 4, 10, 8)[VAR % 6]
    for i in range(nf):                                                # naranjas (solo LOD0)
        a = i * 2 * math.pi / nf + r.uniform(0, 0.4)
        rr, z = (1.68, 1.6) if i % 2 else (1.45, 2.3)
        fino.caja((rr * k * math.cos(a) + dx, rr * k * math.sin(a) + dy, (0.7 + (z - 0.7) * ach) * k), (0.14, 0.14, 0.14), "fruta")
    return t.objeto(nombre, bisel=0)
