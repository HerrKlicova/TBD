"""Montaje de prueba 2 con el kit: estación principal de Castellón (Teo, 7-nov). Cotas de Álvaro
(docs/niveles/castellon-nivel.md §1): vías generales en Z 57,5 y 66,5; andén central Z 60-64 y andén lateral Z 69-73,
los dos de X 70 a 190 (120 m); edificio de viajeros de X 98 a 162 y Z 75 a 88 (64 x 13 m): dos alas iguales y un cuerpo
central más alto con frontón y reloj.
SIN FOTO de la estación real (la de hoy es moderna): es una «principal de la línea» con el kit de Sagunto, combinada
de otra manera (tres plantas, torretas, pórtico, andén central a dos aguas). Coordenadas locales: origen en el centro
del edificio (X 130) y en la línea de su fachada (Z 75); la vía queda hacia -Y.
Reparto del edificio (64 m): ala de 4 módulos (16 m) + torreta (4) + cuerpo central de 24 m (2 módulos de tres plantas,
Variantes (TBD_VARIANTE) = estados de la D-N124: 0 Segura; 1 Asediada (andén roto y marquesinas sin chapa o hundidas);
2 Caída (horda dentro: más andén roto, marquesinas hundidas, escombro en las vías); 3 Liberada (en reconstrucción:
andamios en el cuerpo central y en una torreta, marquesinas a medio cubrir). El resto repite 0.
"""
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual)
from kit_montaje import juntar, fila

PRESUPUESTO = {0: (500, 150000), 1: (300, 80000)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.12}
FONDO = 13.0


def construir(nombre="SM_EstacionCastellon", lod=0):
    v = var_actual()
    v = v if v in (0, 1, 2, 3) else 0
    h = 0.68
    yb = FONDO / 2
    kw = {"fondo": FONDO}
    L = []
    # edificio
    L += fila("kit_cuerpo", [0, 0, 1, 0], -30.0, 4.0, yb, kw=kw)                 # ala oeste, x -30..-18
    L.append(("kit_remate", 2, {"fondo": FONDO, "alto": 11.5}, (-14.0, yb, 0.0), 0.0))
    L += fila("kit_cuerpo", [6, 6], -10.0, 4.0, yb, kw=kw)
    L.append(("kit_remate", 0, {"fondo": FONDO, "alto": 11.5}, (0.0, yb, 0.0), 0.0))
    L += fila("kit_cuerpo", [6, 6], 6.0, 4.0, yb, kw=kw)
    L.append(("kit_remate", 2, {"fondo": FONDO, "alto": 11.5}, (14.0, yb, 0.0), 0.0))
    L += fila("kit_cuerpo", [0, 1, 0, 0], 18.0, 4.0, yb, kw=kw)                   # ala este, x 18..30
    L.append(("kit_remate", 5, {"fondo": FONDO, "alto": 8.0}, (-32.25, yb, 0.0), 0.0))
    L.append(("kit_remate", 5, {"fondo": FONDO, "alto": 8.0}, (32.25, yb, 0.0), 180.0))
    # andén lateral (y -6..-2) y central (y -15..-11), 12 tramos de 10 m de x -55 a 55
    lat = {1: [0, 2, 4, 0, 0, 4, 2, 0, 4, 0, 2, 3], 2: [2, 2, 4, 0, 2, 4, 2, 0, 2, 0, 2, 3],
           3: [0, 2, 4, 0, 0, 4, 0, 0, 4, 0, 0, 3]}.get(v, [0, 0, 4, 0, 0, 4, 0, 0, 4, 0, 0, 3])
    L += fila("kit_anden", lat, -55.0, 10.0, -4.0)
    L += fila("kit_anden", [5] * 12, -55.0, 10.0, -13.0)
    # marquesinas: 5 grupos de 3 tramos en el lateral; a dos aguas en 10 tramos del central
    for xc in (-48.0, -24.0, 0.0, 24.0, 48.0):
        vs = {1: [0, 3, 0] if xc < 0 else [0, 4, 0], 2: [4, 3, 4], 3: [0, 3, 0] if xc in (-24.0, 24.0) else [0, 0, 0]
              }.get(v, [0, 0, 0])
        L += fila("kit_marquesina", vs, xc - 6.0, 6.0, -3.5, z=h)
    L += fila("kit_marquesina", [2, 3, 2, 2, 3, 3, 2, 2, 3, 2] if v == 2 else [2] * 10, -27.0, 6.0, -13.0, z=h)
    # piezas sueltas
    for x in (-40.0, -12.0, 12.0, 40.0):
        L.append(("kit_sueltas", 0, None, (x, -2.6, h), 0.0))
        L.append(("kit_sueltas", 0, None, (x, -13.0, h), 0.0))
    for x in (-50.0, -30.0, -10.0, 10.0, 30.0, 50.0):
        L.append(("kit_sueltas", 1, None, (x, -2.4, h), 0.0))
    for x in (-36.0, 36.0):
        L.append(("kit_sueltas", 1, None, (x, -13.0, h), 0.0))
    L.append(("kit_sueltas", 2, None, (-20.0, -2.4, h), 0.0))
    L.append(("kit_sueltas", 2, None, (20.0, -14.6, h), 180.0))
    for x in (-16.0, 16.0):
        L.append(("kit_sueltas", 3, None, (x, -2.4, h), 0.0))
    L.append(("kit_sueltas", 4, None, (6.0, -3.0, h), 0.0))
    L.append(("kit_sueltas", 5, None, (60.0, -9.0, 0.0), 0.0))
    if v == 2:     # Caída: escombro en las dos vías y en los andenes
        for (x, y, g) in ((-30.0, -8.5, 0.0), (5.0, -8.0, 60.0), (35.0, -9.0, 20.0), (-12.0, -17.5, 80.0), (22.0, -17.0, 0.0)):
            L.append(("kit_sueltas", 6, None, (x, y, 0.0), g))
        for (x, y) in ((-44.0, -4.0), (14.0, -3.6), (-6.0, -13.0)):
            L.append(("kit_sueltas", 6, None, (x, y, h), 30.0))
    elif v == 3:   # Liberada: andamios en el cuerpo central y en la torreta oeste
        for x in (-8.0, 8.0, -14.0):
            L.append(("kit_sueltas", 7, None, (x, -0.75, 0.0), 0.0))                   # caseta del enclavamiento, en la cabeza
    return juntar(nombre, lod, L)
