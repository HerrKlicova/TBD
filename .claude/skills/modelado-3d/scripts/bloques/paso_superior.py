"""Paso superior de carretera sobre la vía (Álvaro, docs/niveles/sagunto-nivel.md §5, encargo 1). Teo, 6-nov-2026.

Tablero de hormigón de una sola luz de 26 m sobre dos estribos, con pretiles. La vía va a lo largo de X y la carretera
cruza a lo largo de Y. MEDIDAS SIN VERIFICAR (típicas, a falta de las fotos de Paula; referencia provisional: el paso
superior del fondo de E3_poste_catenaria_2): gálibo libre de 7,0 m sobre el carril (vía electrificada), tablero de
1,3 m de canto con cuatro vigas, calzada de 9 m entre pretiles de 0,3 x 1,1 m, estribos de 3 m de fondo con aletas.
Los terraplenes de acceso los pone el terreno (Marcos). Colores: hormigón b8b8b8, cara inferior y vigas 6c7874,
calzada 48403c, línea de la calzada d4d0cc (solo LOD0), imposta 94948c.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["hormigon", "viga", "calzada", "linea", "imposta", "estribo"]
# Hormigón claro b8b8b8 como en la foto (la regla de no usar filos b8b8b8 es para vehículos largos, no para obra civil)
COLORES = {"hormigon": "b8b8b8", "viga": "6c7874", "calzada": "48403c", "linea": "d4d0cc", "imposta": "94948c",
           "estribo": "b8b8b8"}
FACCION = "neutro"
PRESUPUESTO = {0: (60, 1500), 1: (30, 600)}
LUZ, ANCHO, GALIBO, CANTO = 26.0, 10.2, 7.0, 1.3


def construir(nombre="SM_PasoSuperior", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    hy, hx = LUZ / 2, ANCHO / 2
    z0 = GALIBO                                   # cara inferior del tablero
    zt = z0 + CANTO                               # rasante
    # losa del tablero (toda la luz y un poco sobre los estribos)
    t.caja((0, 0, zt - 0.15), (ANCHO, LUZ + 2.0, 0.3), "hormigon")
    t.caja((0, 0, zt + 0.01), (ANCHO - 0.7, LUZ + 1.96, 0.02), "calzada")
    # vigas bajo la losa (en el LOD1, un cajón macizo: las cuatro vigas no se separan a 2 px)
    if lod == 0:
        for x in (-3.6, -1.2, 1.2, 3.6):
            t.caja((x, 0, z0 + (CANTO - 0.3) / 2), (0.6, LUZ + 1.9, CANTO - 0.3), "viga")
    else:
        t.caja((0, 0, z0 + (CANTO - 0.3) / 2), (ANCHO - 1.0, LUZ + 1.9, CANTO - 0.3), "viga")
    # impostas (cantos laterales) y pretiles
    for sx in (-1, 1):
        t.caja((sx * (hx - 0.12), 0, zt - 0.35), (0.26, LUZ + 2.02, 0.7), "imposta")
        t.caja((sx * (hx - 0.2), 0, zt + 0.55), (0.3, LUZ + 1.98, 1.1), "hormigon")   # más corto que la losa: sin aristas coincidentes
    # línea discontinua del eje (solo LOD0)
    for k in range(9):
        fino.caja((0, -12.0 + k * 3.0, zt + 0.03), (0.15, 1.5, 0.02), "linea")
    # estribos con aletas a 45° (los terraplenes van detrás)
    for sy in (-1, 1):
        ye = sy * (hy + 1.5)
        t.caja((0, ye, z0 / 2 - 0.05), (ANCHO + 0.6, 3.0, z0 - 0.1), "estribo")
        for sx in (-1, 1):
            t.caja((sx * (hx + 1.6), sy * (hy + 2.4), z0 / 2 - 0.2), (0.6, 3.6, z0 - 0.4), "estribo",
                   rot_z=math.radians(45 * sx * sy))
        t.caja((0, sy * (hy + 0.05), z0 - 0.35), (ANCHO + 0.2, 0.5, 0.5), "imposta")    # durmiente de apoyo
    return t.objeto(nombre, bisel=0)
