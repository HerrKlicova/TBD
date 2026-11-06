"""Locomotora diésel para la VISTA CERCANA del tren (coordinador, 6-nov 23:38). Teo, 6-nov-2026.

Es la R1 aprobada (locomotora_r1.py: Renfe 333.3 blindada por el taller de Sagunto, fotos R1_renfe333_1/_2 de Paula),
con dos cambios:
  1. D-N80 (Bruno; Lucía: también en vehículos): ningún albedo propio bajo #48403c. El bastidor, las ruedas, el metal y
     las rejillas pasan de 303424/1c200c a 48403c, los cristales de 181c30 a 60546c y las caras de abajo del casco a
     50541c (por defecto caían en 442c0c, de la horda).
  2. Las VENTANILLAS (parabrisas y mirillas de las chapas) salen en un objeto hijo, «Ventanillas», con el mismo origen
     que la locomotora, para que Marcos les dé emisión de noche.
Jerarquía del FBX: SM_…_LODn (cuerpo) └─ Ventanillas. Mira a +Z en Unity. Medidas y lo SIN VERIFICAR, en locomotora_r1.
Entregar con --claro b8b8b8 (techo D-N42).
"""
import locomotora_r1 as R
from montaje import marcar_piezas, separar_piezas

PIEZAS = R.PIEZAS
COLORES = dict(R.COLORES, bastidor="48403c", rueda="48403c", metal="48403c", rejilla="48403c", optica="60546c")
PERMITIR = ("e08c30",)          # franja ámbar del frontal (D-N42, única excepción)
ABAJO_CASCO = "50541c"
L_MIN = 28.0
VALOR_PIEZAS = {"casco": ("b8b8b8", "50541c", "50541c")}
VENTANILLAS = {"Ventanillas": ["optica"]}


def construir(nombre="SM_Locomotora", lod=0):
    return marcar_piezas(R.construir(nombre, lod))


def separar(o, lod):
    return separar_piezas(o, VENTANILLAS)
