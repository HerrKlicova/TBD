"""Edificio de viajeros de Nules-La Vilavella, v0.1 (Álvaro, 8-nov, nules-nivel.md §6.1: 21 × 17 m y 2 plantas, centro en
(115; 104,5), yaw 180, fachada a la vía). Teo (3), 8-nov.

Montaje del kit de estaciones (kit_cuerpo, kit_remate y kit_sueltas, colores de kit_comun ya aprobados): fachada al
andén de 5 módulos Nules de dos plantas (5 huecos por planta, recercados e imposta grises, persianas) y piñón
escalonado con óculo en los dos testeros (kit_remate v1): 21 m. Detrás, un cuerpo de una planta de 8 m de fondo con la
fachada hacia el pueblo, hasta los 17 m de OSM (que pueden incluir anexos: SIN VERIFICAR). Fotos N1 y E1 de Paula.
Origen: centro de la huella de 21 × 17 m, en el suelo. Fachada a la vía en −Y (cuerpo principal de y −8,5 a 0,5);
cuerpo trasero de y 0,5 a 8,5. Con yaw 180 la fachada mira a −Z de Unity, hacia la vía.
Variantes (TBD_VARIANTE) = estados de la D-N124, como el montaje de prueba: 0 abandonada (tapiada); 1 Segura (entera,
huecos abiertos); 2 En liberación (tapiada a medias); 3 Caída (todo tapiado y escombro delante); 4 Liberada (en
reconstrucción, con andamio). En el juego el edificio hace de tramo del muro norte del recinto (nules.json).
"""
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual)
from kit_montaje import juntar, fila

PRESUPUESTO = {0: (120, 12000), 1: (60, 6000)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
F1, F2 = 9.0, 8.3                   # fondo del cuerpo principal y del trasero (se meten 0,3 m: sin caras coincidentes)
Y1 = -8.5 + F1 / 2                  # centro del cuerpo principal (fachada en y = −8,5)
Y2 = 8.5 - F2 / 2                   # centro del cuerpo trasero


def construir(nombre="SM_EdificioViajerosNules", lod=0):
    v = var_actual() % 5
    frente = {0: [4, 4, 1, 4, 4], 1: [1, 1, 1, 1, 1], 2: [1, 4, 1, 4, 1], 3: [4, 4, 4, 4, 4], 4: [1, 1, 1, 1, 1]}[v]
    detras = {0: [2, 2, 2, 2, 2], 1: [2, 2, 5, 2, 2], 2: [2, 2, 2, 2, 2], 3: [2, 2, 2, 2, 2], 4: [2, 2, 5, 2, 2]}[v]
    L = []
    L += fila("kit_cuerpo", frente, -8.0, 4.0, Y1, kw={"fondo": F1})
    L.append(("kit_remate", 1, {"fondo": F1, "alto": 8.0}, (-10.25, Y1, 0.0), 0.0))
    L.append(("kit_remate", 1, {"fondo": F1, "alto": 8.0}, (10.25, Y1, 0.0), 180.0))
    # cuerpo trasero de una planta, girado para que su fachada mire al pueblo (+Y)
    L += fila("kit_cuerpo", detras, 8.0, -4.0, Y2, kw={"fondo": F2}, giro=180.0)
    if v == 3:                                       # Caída: escombro delante de la fachada
        L.append(("kit_sueltas", 6, None, (-4.0, -10.0, 0.0), 20.0))
        L.append(("kit_sueltas", 6, None, (5.0, -9.8, 0.0), -40.0))
    if v == 4:                                       # Liberada: andamio en la fachada
        L.append(("kit_sueltas", 7, None, (-2.1, -9.25, 0.0), 0.0))
        L.append(("kit_sueltas", 7, None, (2.1, -9.25, 0.0), 0.0))
    if v == 1:                                       # Segura: bancos junto a la puerta
        for x in (-6.0, 6.0):
            L.append(("kit_sueltas", 0, None, (x, -9.3, 0.0), 0.0))
    return juntar(nombre, lod, L)
