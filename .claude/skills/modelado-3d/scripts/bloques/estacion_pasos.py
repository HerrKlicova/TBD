"""Montaje de prueba del paso entre andenes (Lucía, 7-nov 08:40): las 5 variantes de kit_paso sobre las vías de
Castellón (cotas de Álvaro, castellon-nivel.md §1): andén lateral Z 69-73 (y -6..-2), vía general Z 66,5 (y -8,5),
andén central Z 60-64 (y -15..-11) y vía Z 57,5 (y -17,5); y = Z - 75. Los pasos van con el origen en la vía de
y -8,5, de x -48 a 48 cada 24 m (v0 a v4 de oeste a este). Solo para la captura.
"""
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual)
from kit_montaje import juntar, fila

PRESUPUESTO = {0: (500, 60000), 1: (300, 30000)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.12}


def construir(nombre="SM_EstacionPasos", lod=0):
    h = 0.68
    L = []
    L += fila("kit_anden", [0] * 12, -55.0, 10.0, -4.0)
    L += fila("kit_anden", [5] * 12, -55.0, 10.0, -13.0)
    L += fila("kit_via", [0] * 12, -55.0, 10.0, -8.5)
    L += fila("kit_via", [0] * 12, -55.0, 10.0, -17.5)
    L += fila("kit_paso", [0, 1, 2, 3, 4], -48.0, 24.0, -8.5)
    for x in (-36.0, -12.0, 12.0, 36.0):
        L.append(("kit_sueltas", 1, None, (x, -2.4, h), 0.0))
    return juntar(nombre, lod, L)
