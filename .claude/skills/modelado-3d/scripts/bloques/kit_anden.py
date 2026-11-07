"""Kit de estaciones: tramo de andén de 10 m (Teo, 7-nov). Se encadena en X (9,98 m con holgura).

Andén lateral de 4 m de fondo (Nules y el lateral de Castellón: Z 69-73) o central de 4 m con borde a los dos lados
(Castellón: Z 60-64). Borde hacia la vía en -Y. Altura de andén de 0,68 m (alto) o 0,38 m (bajo): SIN VERIFICAR.
Losa 94948c arriba (como el borde del muelle de Sagunto), costados 88885c, banda de seguridad f0dc9c (la de la topera
Rawie: L* 85, no es ámbar) a 0,6 m del borde.
Variantes (TBD_VARIANTE): 0 lateral alto con banda; 1 lateral bajo sin banda (apeadero viejo); 2 lateral alto roto
(se ha caído un trozo del borde y hay cascotes en la vía); 3 lateral alto que acaba en rampa (+X, cabeza del andén);
4 lateral alto con bordillo de piedra que vuela 0,15 m y juntas de losa; 5 central (isla) con banda a los dos lados.
"""
import math
from taller import Taller
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual)

PRESUPUESTO = {0: (12, 900), 1: (12, 500)}
LARGO, FONDO = 9.98, 4.0


def construir(nombre="SM_KitAnden", lod=0, var=None):
    v = var_actual(var)
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    h = 0.38 if v == 1 else 0.68
    lx = LARGO
    if v == 3:   # rampa en la cabeza (+X): el andén baja a 0 en los últimos 4 m
        lx = LARGO - 4.0
        x0 = -LARGO / 2 + lx / 2
        t.caja((x0, 0, h / 2), (lx, FONDO, h), "costado")
        t.caja((x0, 0, h - 0.04), (lx - 0.004, FONDO + 0.02, 0.1), "losa")
        a = math.atan2(h, 4.0)
        L = math.hypot(4.0, h)
        t.caja((LARGO / 2 - 2.0, 0, h / 2 - 0.05), (L, FONDO - 0.02, 0.12), "losa", rot_y=a)
        t.caja((LARGO / 2 - 2.6, 0, h / 4), (2.8, FONDO - 0.1, h / 2), "costado")
    elif v == 2:  # borde roto: falta un trozo de 3 x 1,2 m en el canto de la vía
        t.caja((0, 0.6, h / 2), (lx, FONDO - 1.2, h), "costado")
        t.caja((0, 0.6, h - 0.04), (lx - 0.004, FONDO - 1.18, 0.1), "losa")
        for x0, x1 in ((-lx / 2, -1.0), (2.0, lx / 2)):
            t.caja(((x0 + x1) / 2, -FONDO / 2 + 0.65, h / 2 - 0.01), (x1 - x0, 1.3, h - 0.02), "costado")
            t.caja(((x0 + x1) / 2, -FONDO / 2 + 0.65, h - 0.03), (x1 - x0 + 0.01, 1.32, 0.1), "losa")
        t.caja((0.5, -FONDO / 2 + 0.7, h * 0.35), (3.0, 1.0, h * 0.7), "costado")      # muro hundido del hueco
        for k, (dx, dy, g) in enumerate(((-0.4, -2.5, 20), (0.8, -2.8, -35), (1.6, -2.4, 50), (0.2, -3.1, 5))):
            t.caja((dx, dy, 0.15), (0.7, 0.45, 0.3), "escombro", rot_z=math.radians(g))
    else:
        f = FONDO
        t.caja((0, 0, h / 2), (lx, f, h), "costado")
        vuelo = 0.15 if v == 4 else 0.0
        t.caja((0, 0, h - 0.04), (lx - 0.004, f + 0.02, 0.1), "losa")
        if v == 4:   # bordillo de piedra que vuela sobre la vía
            t.caja((0, -f / 2 - vuelo / 2 + 0.2, h + 0.02), (lx - 0.004, 0.4 + vuelo, 0.14), "cornisa")
            for k in range(1, 10):               # juntas de losa (solo LOD0: no llegan a 2 px)
                fino.caja((-lx / 2 + k * lx / 10, 0.2, h + 0.012), (0.04, f - 0.6, 0.01), "recerco")
    if v in (0, 2, 3, 4, 5):                     # banda de seguridad (2 px en el LOD1: 0,34 m)
        ancho = 0.34
        lb = lx if v != 3 else lx
        xb = 0.0 if v != 3 else -LARGO / 2 + lx / 2
        lados = (-1, 1) if v == 5 else (-1,)
        for s in lados:
            if v == 2:
                for x0, x1 in ((-lx / 2, -1.0), (2.0, lx / 2)):
                    t.caja(((x0 + x1) / 2, -FONDO / 2 + 0.6, h + 0.015), (x1 - x0, ancho, 0.03), "borde")
            else:
                t.caja((xb, s * (FONDO / 2 - 0.6), h + 0.015 + (0.02 if v == 4 else 0)), (lb - 0.1, ancho, 0.03), "borde")
    if lod == 0 and v in (0, 5):                 # tapas de arqueta (solo LOD0)
        for x in (-3.0, 3.0):
            t.caja((x, 0.9 if v == 0 else 0.0, h + 0.012), (0.6, 0.6, 0.02), "recerco")
    return t.objeto(nombre, bisel=0)
