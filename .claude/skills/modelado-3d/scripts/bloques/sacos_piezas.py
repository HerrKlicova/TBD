"""Muro de sacos terreros: esquina y remate por estados (kit de defensas, Mak 8-nov 13:46; recinto de Nules).
Teo (3), 8-nov.

Salen del tramo aprobado muro_sacos_estados (D-N163 §1): mismas 5 variantes, 4 estados, colores y daños; 1,0 m de
grueso y 4 hiladas (1,2 m). Ver defensa_piezas.py para la forma:
- ESQUINA exterior: bloque de esquina de 1 × 1 m en el origen y dos brazos de 3,4 m, hacia +X (fuera −Y) y hacia +Y
  (fuera −X). Los tramos de 8 m empiezan a 3,9 m del origen en cada dirección.
- REMATE: bloque de esquina, brazo de 3,4 m hacia +X y un bloque de vuelta hacia +Y (hacia dentro).
TBD_VARIANTE = 20 · tipo + 4 · variante + estado (0..39). Tipos: 0 Esquina, 1 Remate.
Variantes: Recto, Talud, Irregular, Tablon, Red. Estados: Sano, Danado, MuyDanado, Brecha.
"""
import os
import muro_sacos_estados as M
import defensa_piezas as D

_V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 40
TIPO = _V // 20
NOMBRES_T = ["Esquina", "Remate"]
VARIANTE, ESTADO, NOMBRES_V, NOMBRES_E = M.VARIANTE, M.ESTADO, M.NOMBRES_V, M.NOMBRES_E
PIEZAS, COLORES, FACCION, UMBRAL_CONTORNO = M.PIEZAS, M.COLORES, M.FACCION, M.UMBRAL_CONTORNO
NORMALES_ARRIBA, NORMALES_INCLINACION = M.NORMALES_ARRIBA, M.NORMALES_INCLINACION
PRESUPUESTO = {0: (10, 4000), 1: (6, 1500)}


def construir(nombre="SM_SacosPieza", lod=0):
    return D.pieza(M, TIPO, nombre, lod, bloque=1.0, brazo=3.4)


separar = D.separar
