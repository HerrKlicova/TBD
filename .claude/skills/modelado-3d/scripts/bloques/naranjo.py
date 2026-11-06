"""Naranjo para las hileras de detrás de las casas (Lucía, ambientacion-sagunto.md, pieza 6). Teo, 6-nov. Un solo
modelo instanciado.

MEDIDAS SIN VERIFICAR (típicas de un naranjo de huerta adulto, a la espera de las fotos de Paula): 3,6 m de alto,
copa redondeada de 3,4 m de diámetro que arranca a 0,7 m (el naranjo de cultivo tiene la copa baja y densa), tronco
de 0,15 m de radio. Colores: copa con valor partido, arriba olive_light 88885c y costados olive 70784c (valor medio, Lucía); tronco 605c48;
naranjas en olive_yellow 847c34 solo en el LOD0 (el naranja de la paleta, e08c30, es el ámbar de los avisos y está
prohibido en objetos).
"""
import math
from taller import Taller

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
    t.cilindro((0, 0, 0.45), 0.15, 0.9, "Z", 6, "tronco")
    lados = 10 if lod == 0 else 8
    # copa en tres troncos de cono apilados: base ancha, cintura y casquete (bola baja y achatada)
    # (solapados 5 cm y con radios distintos en las juntas: si las tapas coinciden, limpiar() las funde en aristas
    # no manifold)
    # copa achatada (naranjo de cultivo: bola baja y ancha). Más superficie mirando arriba = más copa en el claro, para
    # que de lejos no se lea como una mancha oscura de horda (Lucía 16:55)
    t.cilindro((0, 0, 1.0), 1.2, 0.6, "Z", lados, "copa", r2=1.68)
    t.cilindro((0, 0, 1.58), 1.7, 0.6, "Z", lados, "copa", r2=1.62)
    t.cilindro((0, 0, 2.3), 1.6, 0.9, "Z", lados, "copa", r2=1.1)
    t.cilindro((0, 0, 3.0), 1.08, 0.55, "Z", lados, "copa", r2=0.35)
    for k in range(9):                                                             # naranjas (solo LOD0)
        a = k * 2 * math.pi / 9
        r, z = (1.68, 1.6) if k % 2 else (1.45, 2.3)
        fino.caja((r * math.cos(a), r * math.sin(a), z), (0.14, 0.14, 0.14), "fruta")
    return t.objeto(nombre, bisel=0)
