"""HESCO: esquina y remate por estados (kit de defensas, Mak 8-nov 13:46; recinto de Nules, 130 u de HESCO al este).
Teo (3), 8-nov.

Salen del tramo aprobado muro_hesco_estados (D-N163 §1): mismas 5 variantes, 4 estados, colores y daños. Celdas de
1,11 m de lado y 2,1 m de alto. Ver defensa_piezas.py para la forma:
- ESQUINA exterior: celda de esquina en el origen y dos brazos de 2 celdas (2,22 m), hacia +X (fuera −Y) y hacia +Y
  (fuera −X). Los tramos de 10 m empiezan a 2,78 m del origen en cada dirección.
- REMATE: celda de esquina, brazo de 2 celdas hacia +X y una celda de vuelta hacia +Y (hacia dentro), que cierra el
  final del muro (junto a una puerta o al empalme con los sacos).
- TRAMO CORTO de 1 a 4 celdas (1,11 a 4,44 m), para los restos que no son múltiplo de 10 m (Álvaro, 8-nov 13:51):
  centro en el origen, a lo largo de X y fuera −Y, como el tramo de 10 m.
TBD_VARIANTE = 20 · tipo + 4 · variante + estado (0..119). Tipos: 0 Esquina, 1 Remate, 2 Corto1, 3 Corto2, 4 Corto3,
5 Corto4.
Variantes: MallaLimpia, Geotextil, Escalonado, RemateSacos, Red. Estados: Sano, Danado, MuyDanado, Brecha.
"""
import os
import muro_hesco_estados as M
import defensa_piezas as D

_V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 120
TIPO = _V // 20
NOMBRES_T = ["Esquina", "Remate", "Corto1", "Corto2", "Corto3", "Corto4"]
VARIANTE, ESTADO, NOMBRES_V, NOMBRES_E = M.VARIANTE, M.ESTADO, M.NOMBRES_V, M.NOMBRES_E
PIEZAS, COLORES, FACCION, UMBRAL_CONTORNO = M.PIEZAS, M.COLORES, M.FACCION, M.UMBRAL_CONTORNO
NORMALES_ARRIBA, NORMALES_INCLINACION = M.NORMALES_ARRIBA, M.NORMALES_INCLINACION
PRESUPUESTO = {0: (10, 4000), 1: (6, 1500)}
P = 10.0 / 9

_alto0 = M._alto


def _alto(k):
    # en los brazos cortos el daño del tramo (celdas 4 y 5) no cae: aquí revienta la celda de fuera (k = 1)
    if M.ESTADO == 1 and k == 1:
        return M._alto_sano(k) * 0.5
    return _alto0(k)


M._alto = _alto


def construir(nombre="SM_HescoPieza", lod=0):
    if TIPO >= 2:
        n = TIPO - 1
        return D.juntar(nombre, [D._tramo(M, n * P, n, "tmp_t", lod)])
    return D.pieza(M, TIPO, nombre, lod, bloque=P, brazo=2 * P, n_bloque=1, n_brazo=2)


separar = D.separar
