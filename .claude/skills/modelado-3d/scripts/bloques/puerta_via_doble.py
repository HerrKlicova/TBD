"""Puerta de vía DOBLE de Nules (Álvaro, 8-nov, nules-nivel.md §6.4: puertas en (85; 64,5) y (188; 64,5), hueco de
Z 60,8 a 68,2). Teo (3), 8-nov.

Es la puerta de vía de Sagunto (puerta_via.py, D-N59) ensanchada: hueco de 7,4 m para las dos vías generales a 3,8 m
entre ejes (vía 1 en x = +1,9 y vía 2, la de tercer carril, en x = −1,9, con yaw 0), dos hojas de 3,69 m que abren hacia
dentro (+Y de Blender) y jambas de HESCO de 0,9 m. En la v4 (pilares y dintel a 6 m) hay dos anclajes de catenaria,
uno sobre cada vía. Los trozos de las hojas caen fuera de las dos vías (|x| > 2,9). Mismos colores, estados y
TBD_VARIANTE = 4 · variante + estado (0..19) que la puerta de vía. Medidas SIN VERIFICAR.
Disposición: origen en el suelo, en el centro del hueco (entre las dos vías); el muro va a lo largo de X; −Y es FUERA.
Jerarquía: SM_…_LODn con Hoja_O (bisagra en x = −3,69) y Hoja_E (bisagra en x = +3,69); abren +90° (O) y −90° (E).
"""
import puerta_via as P
import puerta_vehiculos as PV
import puerta_estados as PE

BISAGRA = 3.69
PV.BISAGRA = PE.BISAGRA = P.BISAGRA = BISAGRA
P.JAMBA = BISAGRA + 0.46
P.CATENARIAS = (-1.9, 1.9)
P.ESCOMBRO_X0 = 2.9
PE.ROTO_FASE = 1.3
P.PIV = {"Hoja_O": (-BISAGRA, 0.0, 0.0), "Hoja_E": (BISAGRA, 0.0, 0.0)}
VAR, VARIANTE, ESTADO, NOMBRES_V, NOMBRES_E = P.VAR, P.VARIANTE, P.ESTADO, P.NOMBRES_V, P.NOMBRES_E
PIEZAS, COLORES, FACCION, UMBRAL_CONTORNO = P.PIEZAS, P.COLORES, P.FACCION, P.UMBRAL_CONTORNO
NORMALES_ARRIBA, NORMALES_INCLINACION, ALTO = P.NORMALES_ARRIBA, P.NORMALES_INCLINACION, P.ALTO
PRESUPUESTO = {0: (50, 3000), 1: (25, 1400)}
PARTES, PIV, GIRO_PRUEBA = P.PARTES, P.PIV, P.GIRO_PRUEBA


def construir(nombre="SM_PuertaViaDoble", lod=0):
    return P.construir(nombre, lod)


def separar(o, lod):
    return P.separar(o, lod)
