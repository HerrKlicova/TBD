"""Escena de comprobación del fondo (NO es una pieza): fuerte de Sagunto (caja de 86 × 41 m con el tren de 64 m dentro),
el castillo reducido de D-N68 a ~290 m al norte y la sierra baja detrás, en tramos repetidos. Teo, 6-nov-2026.

El render del método mira desde −Y, así que aquí el «norte» es +Y (lo que en el juego queda lejos de la cámara) y el
castillo y la sierra van con su frente hacia el fuerte. NO es la cámara del juego (ángulo y zoom aproximados): sirve
para ver proporciones y si «castillo» se lee sin quitarle el primer plano al fuerte. La captura buena la saca Marcos.
"""
import math, os
import bpy
from taller import Taller
import castillo_sagunto as C
import sierra_fondo as S

VAR = 0
PIEZAS = ["fuerte", "tren"] + C.PIEZAS + S.PIEZAS
COLORES = {"fuerte": "d4d0cc", "tren": "6c7874", **C.COLORES, **S.COLORES}
VALOR_PIEZAS = {**C.VALOR_PIEZAS, **S.VALOR_PIEZAS}
FACCION = "neutro"
PRESUPUESTO = {0: (500, 9000), 1: (500, 9000)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def construir(nombre="SM_EscenaFondo", lod=0):
    t = Taller(PIEZAS)
    for x, y, sx, sy in ((0, -20.5, 86, 0.8), (0, 20.5, 86, 0.8), (-43, 0, 0.8, 41), (43, 0, 0.8, 41)):
        t.caja((x, y, 2.0), (sx, sy, 4.0), "fuerte")
    t.caja((0, 0, 2.0), (64, 3.0, 4.0), "tren")
    base = t.objeto("fuerte", bisel=0)
    otros = []
    c = C.construir("castillo", lod=lod)
    c.location = (17.0, 290.0, 0.0)
    otros.append(c)
    for i, x in enumerate((-450.0, -150.0, 150.0, 450.0)):
        S.VAR = i
        s = S.construir("sierra%d" % i, lod=lod)
        s.location = (x, 440.0, 0.0)
        otros.append(s)
    for o in [base] + otros:
        o.select_set(True)
    with bpy.context.temp_override(active_object=base, selected_editable_objects=[base] + otros, object=base):
        bpy.ops.object.join()
    base.name = nombre
    return base
