"""Kit de estaciones: piezas sueltas de andén (Teo, 7-nov). Origen en el suelo (la cara de arriba del andén).
Fachada o frente hacia -Y (la vía). En el LOD1 nada fino baja de 0,34 m (2 px). Medidas típicas, SIN VERIFICAR.
Variantes (TBD_VARIANTE): 0 banco de listones con patas de fundición; 1 farola de andén de 4,5 m con brazo; 2 cartel de
estación de 3,6 x 1,1 m sobre dos postes (banda oscura para el nombre, decal de Bruno); 3 papelera con su poste; 4 carro
de equipajes de dos ruedas; 5 caseta de enclavamiento de 3 x 3 m con tejado a dos aguas; 6 montón de escombro (2,6 x 1,8 m, estado Caída, en el andén
o en la vía); 7 andamio de fachada de 4 x 1,2 m y 8 m de alto (estado Liberada: la estación en reconstrucción, D-N124).
"""
import math
from taller import Taller
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual, hueco)

PRESUPUESTO = {0: (12, 500), 1: (12, 300)}


def construir(nombre="SM_KitSuelta", lod=0, var=None):
    v = var_actual(var)
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    g = 0.06 if lod == 0 else 0.34
    if v == 0:   # banco
        for x in (-0.8, 0.8):
            t.caja((x, 0, 0.22), (0.08 if lod == 0 else 0.3, 0.45, 0.44), "pilar")
        if lod == 0:
            for k in range(3):
                t.caja((0, -0.15 + k * 0.15, 0.46), (1.9, 0.1, 0.04), "madera")
            for k in range(2):
                t.caja((0, 0.24, 0.62 + k * 0.14), (1.9, 0.04, 0.1), "madera")
        else:
            t.caja((0, 0, 0.47), (1.9, 0.45, 0.08), "madera")
            t.caja((0, 0.22, 0.72), (1.9, 0.08, 0.34), "madera")
    elif v == 1:  # farola
        t.cilindro((0, 0, 2.25), 0.07 if lod == 0 else 0.17, 4.5, "Z", 8, "pilar")
        fino.cilindro((0, 0, 0.3), 0.14, 0.6, "Z", 8, "pilar")
        t.caja((0, -0.45, 4.45), (g, 0.9, g), "pilar")
        t.caja((0, -0.9, 4.25), (0.4, 0.4, 0.4), "cornisa")                            # farol (apagado)
    elif v == 2:  # cartel
        for x in (-1.6, 1.6):
            t.caja((x, 0, 1.25), (g if lod == 0 else 0.2, g if lod == 0 else 0.2, 2.5), "pilar")
        t.caja((0, 0, 2.6), (3.6, 0.1, 1.1), "fachada")
        t.caja((0, -0.06, 2.6), (3.3, 0.04, 0.8), "placa")
    elif v == 3:  # papelera
        t.caja((0, 0, 0.6), (g, g, 1.2), "pilar")
        t.cilindro((0, -0.22, 0.75), 0.2, 0.55, "Z", 8, "recerco")
    elif v == 4:  # carro de equipajes
        t.caja((0, 0, 0.55), (1.6, 0.8, 0.08), "madera")
        for x in (-0.78, 0.78):
            t.caja((x, 0, 0.75), (0.06 if lod == 0 else 0.2, 0.8, 0.4), "madera")
        t.cilindro((0, -0.4, 0.3), 0.3, 0.1, "Y", 10, "metal")
        t.cilindro((0, 0.4, 0.3), 0.3, 0.1, "Y", 10, "metal")
        t.caja((-0.8, 0, 0.3), (0.08 if lod == 0 else 0.2, 0.5, 0.5), "pilar")          # pata
        t.caja((1.15, 0, 0.7), (0.8, g, g), "pilar", rot_y=math.radians(-20))            # lanza
    elif v == 6:  # montón de escombro: piedra y algún tablón, sin nada fino
        for (x, y, z, a, b, c, r) in ((0, 0, 0.3, 1.6, 1.2, 0.6, 10), (-0.7, 0.3, 0.25, 0.9, 0.8, 0.5, -25),
                                      (0.8, -0.2, 0.2, 0.9, 0.9, 0.4, 35), (0.1, 0.1, 0.7, 0.8, 0.6, 0.4, -15),
                                      (-0.2, -0.6, 0.15, 0.6, 0.5, 0.3, 50)):
            t.caja((x, y, z), (a, b, c), "escombro", rot_z=math.radians(r))
        t.caja((0.2, 0.05, 0.75), (2.4, 0.22, 0.12 if lod == 0 else 0.34), "tabla", rot_z=math.radians(25),
               rot_y=math.radians(12))
        fino.caja((-0.9, -0.4, 0.3), (0.4, 0.3, 0.2), "fachada")                       # un trozo de muro encalado
    elif v == 7:  # andamio de fachada (tubos y plataformas de tablas)
        tb = 0.05 if lod == 0 else 0.2
        for x in (-1.9, 0.0, 1.9):
            for y in (-0.55, 0.55):
                t.caja((x, y, 4.0), (tb, tb, 8.0), "metal")
        for z in (2.0, 4.0, 6.0, 7.9):
            t.caja((0, 0, z), (3.98, 1.2, 0.08 if lod == 0 else 0.2), "tabla")
            t.caja((0, -0.6, z + 0.9), (3.98, tb, tb), "metal")                       # barandilla
        if lod == 0:
            for x0 in (-0.95, 0.95):
                for z0 in (1.0, 3.0, 5.0, 7.0):
                    t.caja((x0, -0.62, z0), (0.04, 0.04, 2.6), "metal", rot_y=math.radians(43))
        else:
            t.caja((0, -0.62, 3.0), (3.6, 0.04, 0.34), "metal", rot_y=math.radians(0))   # rodapié: 2 px
    elif v == 5:  # caseta de enclavamiento
        A, F, H = 2.98, 2.98, 2.8
        t.caja((0, 0, H / 2), (A, F, H), "fachada")
        t.caja((0, 0, 0.35), (A + 0.04, F + 0.04, 0.7), "zocalo")
        cs = t.tejado((0, 0, H), A, F, 1.0, "tejado", aguas=2, vuelo=0.25)
        t._marcar([f for f in cs if len(f.verts) == 3], "fachada")
        hueco(t, fino, lod, -0.6, -F / 2, 1.2, 0.9, 2.2, marco="recerco")
        hueco(t, fino, lod, 0.75, -F / 2, 1.6, 0.9, 0.9, marco="recerco")
        hueco(t, fino, lod, 0.0, F / 2, 1.6, 1.4, 0.9, cara=1, marco="recerco")
    return t.objeto(nombre, bisel=0)
