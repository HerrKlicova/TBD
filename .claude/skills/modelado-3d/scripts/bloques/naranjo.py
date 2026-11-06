"""Naranjo para las hileras de detrás de las casas (Lucía, ambientacion-sagunto.md, pieza 6). Teo, 6-nov. Un solo
modelo instanciado.

MEDIDAS SIN VERIFICAR (típicas de un naranjo de huerta adulto, a la espera de las fotos de Paula): 3,6 m de alto,
copa redondeada de 3,4 m de diámetro que arranca a 0,7 m (el naranjo de cultivo tiene la copa baja y densa), tronco
de 0,15 m de radio. Colores: copa con valor partido, arriba olive 70784c y costados olive_dark 50541c; tronco 605c48;
naranjas en olive_yellow 847c34 solo en el LOD0 (el naranja de la paleta, e08c30, es el ámbar de los avisos y está
prohibido en objetos).
"""
import math
from taller import Taller

PIEZAS = ["copa", "tronco", "fruta"]
COLORES = {"copa": "50541c", "tronco": "605c48", "fruta": "847c34"}
VALOR_PIEZAS = {"copa": ("70784c", "50541c")}
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
    t.cilindro((0, 0, 1.05), 1.3, 0.7, "Z", lados, "copa", r2=1.66)
    t.cilindro((0, 0, 1.86), 1.7, 1.0, "Z", lados, "copa", r2=1.45)
    t.cilindro((0, 0, 2.88), 1.4, 1.1, "Z", lados, "copa", r2=0.5)
    for k in range(9):                                                             # naranjas (solo LOD0)
        a = k * 2 * math.pi / 9
        r, z = (1.62, 1.75) if k % 2 else (1.5, 2.3)
        fino.caja((r * math.cos(a), r * math.sin(a), z), (0.14, 0.14, 0.14), "fruta")
    return t.objeto(nombre, bisel=0)
