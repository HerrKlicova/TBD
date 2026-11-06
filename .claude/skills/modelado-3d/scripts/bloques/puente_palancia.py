"""Puente ferroviario sobre el Palancia, v3 fiel a la foto (Álvaro, sagunto-nivel.md §5; Lucía, 17:44). Teo, 6-nov-2026.

Las fotos F2 de Paula (Commons, «Riu Palància i pont del tren, Sagunt», CC0) enseñan un viaducto de VIGAS DE HORMIGÓN
sobre pilas bajas y muchos vanos sobre un cauce seco y ancho, con catenaria. Para el sitio de Álvaro (estribo oeste en X = 262, fuera de
la zona simulada) Álvaro, 17:44: con los vanos que pida la foto) se modela un viaducto de 75 m: 5 vanos de 15 m, 4 pilas de tabique y 2 estribos con aletas.
MEDIDAS SIN VERIFICAR (estimadas a ojo sobre la foto: la foto no tiene escala): carril a 4,5 m sobre el lecho, tablero
de 10 m para vía doble, vigas de 1,2 m de canto, pilas de 1,2 m de grueso. La vía va a lo largo de X; el carril en z = 0.
Colores (Lucía): tablero e impostas 94948c, vigas y pilas a la sombra 6c7874, balasto 48403c. El cauce y las cañas son
del terreno (Marcos, C6). Los postes de catenaria van aparte (poste_catenaria).
"""
import os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["hormigon", "viga", "pila", "balasto", "imposta"]
COLORES = {"hormigon": "94948c", "viga": "6c7874", "pila": "6c7874", "balasto": "48403c", "imposta": "94948c"}
FACCION = "neutro"
PRESUPUESTO = {0: (60, 2000), 1: (30, 800)}
LUZ, ANCHO, VANOS, ALTURA, CANTO = 75.0, 10.0, 5, 4.5, 1.2


def construir(nombre="SM_PuentePalancia", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    hx, hy = LUZ / 2, ANCHO / 2
    lecho = -ALTURA
    zl = -0.75                                        # cara superior de la losa (bajo el balasto)
    z0 = zl - 0.35 - CANTO                            # cara inferior de las vigas
    # losa, balasto e impostas
    t.caja((0, 0, zl - 0.175), (LUZ + 0.6, ANCHO, 0.35), "hormigon")
    t.caja((0, 0, zl + 0.2), (LUZ + 0.5, ANCHO - 1.6, 0.42), "balasto")
    for sy in (-1, 1):
        t.caja((0, sy * (hy - 0.3), zl + 0.45), (LUZ + 0.56, 0.4, 0.9), "imposta")          # murete-pretil
    # vigas: LOD0 cuatro vigas; LOD1 un cajón (las vigas no se separan a 2 px)
    zv = z0 + CANTO / 2 + 0.02
    if lod == 0:
        for y in (-3.3, -1.1, 1.1, 3.3):
            t.caja((0, y, zv), (LUZ + 0.4, 0.7, CANTO + 0.04), "viga")
        for k in range(VANOS + 1):                                                         # riostras
            fino.caja((-hx + k * LUZ / VANOS, 0, zv), (0.3, 6.4, CANTO * 0.8), "viga")
    else:
        t.caja((0, 0, zv), (LUZ + 0.4, ANCHO - 1.4, CANTO + 0.04), "viga")
    # pilas de tabique con cabezal
    for k in range(1, VANOS):
        x = -hx + k * LUZ / VANOS
        alto = z0 - lecho
        t.caja((x, 0, lecho + alto / 2 - 0.1), (1.2, ANCHO - 2.6, alto - 0.2), "pila")
        t.caja((x, 0, z0 - 0.3), (1.6, ANCHO - 1.0, 0.62), "hormigon")                       # cabezal
        fino.caja((x, 0, lecho + 0.3), (2.0, ANCHO - 2.0, 0.6), "pila")                       # zapata asomando
    # estribos con aletas (el terraplén lo pone el terreno)
    for sx in (-1, 1):
        xe = sx * (hx + 1.6)
        t.caja((xe, 0, (lecho + zl) / 2 - 0.1), (3.0, ANCHO + 0.4, zl - lecho - 0.2), "hormigon")
        for sy in (-1, 1):
            t.caja((xe + sx * 2.9, sy * (hy + 0.05), (lecho + zl) / 2 - 0.3), (3.0, 0.6, zl - lecho - 0.6), "hormigon")
        fino.caja((xe - sx * 1.52, 0, z0 - 0.5), (0.04, ANCHO - 1.2, 0.25), "viga")           # apoyo (LOD0)
    return t.objeto(nombre, bisel=0)
