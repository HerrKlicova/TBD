"""Montaje de prueba 1 con el kit: apeadero de Nules (Teo, 7-nov). Cotas de Álvaro (docs/niveles/nules-nivel.md):
andén lateral de 60 m (X 100-160) y edificio de viajeros abandonado de 18 x 8 m (X 121-139, Z 74-82). Fotos E1_1 y
E1_3 de Paula y lote N1 (Nules-La Vilavella: dos plantas, recercados grises de arco rebajado, persianas a86c50,
hastial escalonado con óculo y marquesina de celosía adosada; §0b de nules-nivel.md).
Coordenadas locales: origen en el centro del edificio (X 130 de Álvaro) y en la línea de su fachada (Z 74); la vía
queda hacia -Y. Andén de y -5 a -1 (Z 69-73). Montaje de prueba: en el juego, cada módulo es una instancia.
Variantes (TBD_VARIANTE) = estados de la D-N124 (nombres de Lucía, para que cuadren el JSON y los iconos de Bruno):
0 abandonada (estado_inicial: tapiada, como dice el nivel); 1 Segura (edificio entero, huecos abiertos, más bancos);
2 En liberación (marquesina a medias y un tramo de andén roto); 3 Caída (horda dentro: puertas tapiadas y rotas,
marquesina hundida, escombro en el andén y en la vía); 4 Liberada (en reconstrucción: andamio, recinto abierto, sin
guarnición). El resto repite 0.
"""
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual)
from kit_montaje import juntar, fila

PRESUPUESTO = {0: (500, 60000), 1: (300, 30000)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.12}


def construir(nombre="SM_EstacionNules", lod=0):
    v = var_actual()
    h = 0.68
    v = v if v in (0, 1, 2, 3, 4) else 0
    edif = {0: [4, 4, 1, 4], 1: [1, 1, 1, 1], 2: [4, 4, 1, 4], 3: [4, 4, 4, 4], 4: [1, 1, 1, 1]}[v]
    anden = {2: [0, 0, 2, 0, 4, 3], 3: [0, 2, 0, 2, 4, 3], 4: [0, 0, 2, 0, 4, 3]}.get(v, [0, 0, 0, 4, 0, 3])
    marq = {2: [0, 3, 0], 3: [4, 3, 4], 4: [0, 3, 0]}.get(v, [0, 0, 0])
    L = []
    L += fila("kit_anden", anden, -25.0, 10.0, -3.0)
    # marquesina de celosía de Sagunto adosada por el lado de la vía, X 115-135 de Álvaro (§0b): 3 tramos
    L += fila("kit_marquesina", marq, -11.0, 6.0, -3.5, z=h)
    L += fila("kit_cuerpo", edif, -6.0, 4.0, 4.0, kw={"fondo": 8.0})
    L.append(("kit_remate", 1, {"fondo": 8.0, "alto": 8.0}, (-8.25, 4.0, 0.0), 0.0))
    L.append(("kit_remate", 1, {"fondo": 8.0, "alto": 8.0}, (8.25, 4.0, 0.0), 180.0))
    sueltas = [(2, 12.0, -2.2), (0, -9.0, -1.8), (0, -15.0, -1.8), (1, -20.0, -1.5), (1, 0.0, -1.5), (1, 20.0, -1.5),
               (3, -12.0, -1.6)]
    if v == 1:
        sueltas += [(0, 9.0, -1.8), (4, 16.0, -2.5)]
    elif v == 3:   # Caída: sin bancos ni carro; escombro en el andén y en la vía
        sueltas = [(1, -20.0, -1.5), (1, 20.0, -1.5), (2, 12.0, -2.2), (6, -4.0, -2.0), (6, 16.0, -3.0)]
        L.append(("kit_sueltas", 6, None, (2.0, -7.5, 0.0), 0.0))
        L.append(("kit_sueltas", 6, None, (-14.0, -8.0, 0.0), 70.0))
    elif v == 4:   # Liberada: andamio en la fachada y escombro recogido al final del andén
        sueltas = [(1, -20.0, -1.5), (1, 0.0, -1.5), (1, 20.0, -1.5), (2, 12.0, -2.2), (6, 24.0, -2.5)]
        L.append(("kit_sueltas", 7, None, (2.0, -0.75, 0.0), 0.0))
    for s, x, y in sueltas:
        L.append(("kit_sueltas", s, None, (x, y, h), 0.0))
    L.append(("kit_sueltas", 5, None, (27.0, 3.0, 0.0), 0.0))                     # caseta de enclavamiento
    return juntar(nombre, lod, L)
