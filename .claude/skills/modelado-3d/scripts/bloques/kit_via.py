"""Kit de estaciones: tramo de vía de 10 m (Teo, 7-nov). Pieza auxiliar para los montajes de prueba (la vía de juego
es la de Marcos; esta solo sirve para ver las piezas sobre las vías en las capturas). Ancho ibérico 1,668 m.
Origen: centro del tramo, en la cota del carril (z = 0). Vía a lo largo de X. Medidas SIN VERIFICAR.
Variantes: 0 vía sana; 1 vía con un carril levantado y traviesas sueltas (estado Caída). El resto repite 0.
"""
import math
from taller import Taller
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual)

PRESUPUESTO = {0: (10, 1200), 1: (10, 400)}
LARGO, ANCHO_VIA = 9.98, 1.668


def construir(nombre="SM_KitVia", lod=0, var=None):
    v = var_actual(var) % 2
    t = Taller(PIEZAS)
    t.caja((0, 0, -0.35), (LARGO, 3.2, 0.3), "zocalo")                              # banqueta de balasto
    t.caja((0, 0, -0.17), (LARGO, 2.6, 0.06), "escombro")                           # caja de balasto (piedra gris)
    paso = 0.6 if lod == 0 else 1.2
    n = int(LARGO / paso)
    for k in range(n):
        x = -LARGO / 2 + paso / 2 + k * paso
        r = math.radians(12 if (v == 1 and k % 4 == 1) else 0)
        t.caja((x, 0, -0.1), (0.24 if lod == 0 else 0.4, 2.5, 0.12), "cornisa", rot_z=r)   # traviesa de hormigón
    for s in (-1, 1):
        y = s * ANCHO_VIA / 2
        if v == 1 and s == 1:   # carril levantado: sube por un extremo
            t.caja((0.5, y + 0.3, 0.35), (LARGO - 1.0, 0.07 if lod == 0 else 0.2, 0.15), "metal",
                   rot_y=math.radians(-6), rot_z=math.radians(3))
        else:
            t.caja((0, y, -0.01), (LARGO - 0.004, 0.07 if lod == 0 else 0.2, 0.15), "metal")
    return t.objeto(nombre, bisel=0)
