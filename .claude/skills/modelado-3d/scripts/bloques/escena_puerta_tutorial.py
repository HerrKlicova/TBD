"""Escena de comprobación (NO es una pieza del juego): la puerta de vía oeste (puerta_via, sana) con el tramo HESCO del
tutorial pegado al norte (muro_hesco_estados, e1 dañado = el 45 % del capítulo 2, tutorial-textos.md:41). Teo (3), 8-nov.

Marco de la puerta: origen en el centro del hueco, muro a lo largo de X, −Y = FUERA. La jamba va de |x| 2,5 a 3,4, así que
el módulo de 10 m, para no pisarla, va centrado en x = 3,4 + 5 = 8,4. En el nivel (yaw 270, puerta en Z 64,5) eso es
Z 72,9 y no 72 (con 72 el HESCO y la jamba se solapan 0,9 m y sus caras de arriba coinciden).
TBD_VARIANTE = variante de la puerta (0..4); TBD_HESCO_V = variante del HESCO (0..4, por defecto 0).
"""
import os
import bpy
from mathutils import Matrix

VP = int(os.environ.get("TBD_VARIANTE", "1") or 1) % 5
VH = int(os.environ.get("TBD_HESCO_V", "0") or 0) % 5
os.environ["TBD_VARIANTE"] = str(4 * VP + 0)
import puerta_via as PVIA
os.environ["TBD_VARIANTE"] = str(4 * VH + 1)
import muro_hesco_estados as MH

PIEZAS = list(dict.fromkeys(PVIA.PIEZAS + MH.PIEZAS))
COLORES = dict(PVIA.COLORES, **MH.COLORES)
FACCION = "propio"
PRESUPUESTO = {0: (60, 9000), 1: (30, 4000)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.7
X_HESCO = PVIA.JAMBA + 0.45 + MH.LARGO / 2


def construir(nombre="SM_EscenaPuertaTutorial", lod=0):
    a = PVIA.construir(nombre, lod)
    b = MH.construir("hesco_tmp", lod)
    b.data.transform(Matrix.Translation((X_HESCO, 0, 0)))
    for o in (a, b):
        for md in list(o.modifiers):
            o.modifiers.remove(md)
    bpy.ops.object.select_all(action="DESELECT")
    a.select_set(True)
    b.select_set(True)
    bpy.context.view_layer.objects.active = a
    bpy.ops.object.join()
    return a
