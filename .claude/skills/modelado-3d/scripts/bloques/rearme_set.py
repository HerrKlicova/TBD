"""Set de cajas de munición y bidones para el punto de rearme del helipuerto (coordinador, 7-nov 00:57). Teo, 7-nov.

Piezas sueltas para repartir alrededor del helipuerto (el helipuerto ya trae un rearme fijo). Fotos de Paula: H8_1
(depósitos flexibles rodantes negros con la tapa de la válvula y la manguera), H8_2 y H8_3 (FARP con mangueras por
el suelo). Las cajas de munición, las de cohetes, los bidones de 200 l y los jerricanes van DE MEMORIA (Paula no
encontró un FARP con cajas): SIN VERIFICAR. Medidas típicas: caja de munición de 0,75 × 0,4 × 0,34 m, caja de cohetes
de 1,9 × 0,5 × 0,4 m, bidón de 0,6 × 0,88 m, jerricán de 0,35 × 0,17 × 0,47 m, palé de 1,2 × 1,0 m.
Colores: cajas de munición 50541c con la tapa en 88885c (valor partido), cajas de cohetes 847c34, bidones 605c48 con
la tapa en 94948c, jerricanes 50541c, depósitos 48403c, palés b4a080 (madera clara; el 6c4038 se leía rojizo). Nada de rojo ni ámbar.
Origen en el centro del palé, en el suelo. Variantes (TBD_VARIANTE % 5): 0 palé con 2 × 3 cajas de munición y cuatro
bidones; 1 palé con cajas de cohetes y dos bidones tumbados; 2 depósito rodante con manguera y bomba (H8_1);
3 palé de bidones con jerricanes; 4 revuelto: una caja abierta, cajas sueltas y un bidón volcado.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = ["caja", "cohete", "bidon", "tapa", "jerrican", "deposito", "palet", "metal", "interior"]
COLORES = {"caja": "50541c", "cohete": "847c34", "bidon": "605c48", "tapa": "94948c", "jerrican": "50541c",
           "deposito": "48403c", "palet": "b4a080", "metal": "6c7874", "interior": "48403c"}
VALOR_PIEZAS = {"caja": ("88885c", "50541c", "50541c")}
FACCION = "neutro"
PRESUPUESTO = {0: (20, 800), 1: (10, 400)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _palet(t, x, y):
    t.caja((x, y, 0.07), (1.2, 1.0, 0.14), "palet")


def _caja(t, x, y, z, rot=0.0, fino=True):
    t.caja((x, y, z + 0.175), (0.75, 0.4, 0.34), "caja", rot_z=rot)
    if fino:                                          # asas en los testeros
        for s in (-1, 1):
            dx, dy = s * 0.39 * math.cos(rot), s * 0.39 * math.sin(rot)
            t.caja((x + dx, y + dy, z + 0.25), (0.03, 0.14, 0.04), "metal", rot_z=rot)


def _bidon(t, x, y, z=0.0, tumbado=False, eje="X", fino=True):
    lados = 10 if fino else 8
    if tumbado:
        t.cilindro((x, y, z + 0.3), 0.3, 0.88, eje, lados, "bidon")
    else:
        t.cilindro((x, y, z + 0.43), 0.3, 0.86, "Z", lados, "bidon")
        t.cilindro((x, y, z + 0.88), 0.28, 0.03, "Z", lados, "tapa")      # 5 mm por encima: si toca, caras coplanarias


def construir(nombre="SM_RearmeSet", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    a = ASPECTO
    if a == 0:
        _palet(t, 0, 0)
        for fila in range(3):
            for k in (-1, 1):
                _caja(t, 0, k * 0.22, 0.145 + 0.355 * fila, fino=fino)
        for i, (x, y) in enumerate(((1.3, -0.35), (1.3, 0.32), (1.95, -0.35), (1.95, 0.32))):
            _bidon(t, x, y, fino=fino)
    elif a == 1:
        _palet(t, 0, 0)
        zb = 0.16                                     # 2 cm entre piezas: con menos, limpiar() fundía vértices
        t.caja((0, 0, zb + 0.2), (1.9, 0.5, 0.4), "cohete")
        t.caja((0, 0, zb + 0.62), (1.9, 0.5, 0.4), "cohete")
        t.caja((0, 0.53, zb + 0.2), (1.9, 0.5, 0.4), "cohete")
        if fino:
            for y in (0.0, 0.53):
                for x in (-0.6, 0.6):
                    t.caja((x, y, zb + 0.41), (0.06, 0.52, 0.015), "metal")        # flejes
        _bidon(t, -0.2, -1.0, tumbado=True, fino=fino)
        _bidon(t, 0.75, -1.0, tumbado=True, fino=fino)
    elif a == 2:
        t.cilindro((0, 0, 0.62), 0.62, 1.5, "X", 12 if fino else 8, "deposito")      # depósito rodante (H8_1)
        for s in (-1, 1):
            t.cilindro((s * 0.78, 0, 0.62), 0.22, 0.06, "X", 8, "metal")              # tapa de la válvula
        t.caja((1.6, 0.6, 0.4), (0.8, 0.6, 0.8), "metal")                            # bomba
        if fino:                                                                       # manguera de la válvula
            t.caja((0.88, 0.0, 0.35), (0.1, 0.1, 0.42), "interior")
            t.caja((1.02, 0.22, 0.05), (0.1, 0.5, 0.1), "interior", rot_z=-0.55)
        for (x0, y0), (x1, y1) in (((1.6, 0.95), (1.5, 2.0)), ((1.5, 2.0), (0.6, 2.8))):
            t.caja(((x0 + x1) / 2, (y0 + y1) / 2, 0.05), (0.1, math.hypot(x1 - x0, y1 - y0), 0.1), "interior",
                   rot_z=-math.atan2(x1 - x0, y1 - y0))                             # manguera por el suelo
    elif a == 3:
        _palet(t, 0, 0)
        for x in (-0.31, 0.31):
            for y in (-0.22, 0.22):
                _bidon(t, x, y, z=0.14, fino=fino)
        for k in range(4):
            t.caja((1.0 + 0.2 * k, -0.6, 0.235), (0.17, 0.35, 0.47), "jerrican")
        if fino:
            t.caja((1.1, 0.3, 0.085), (0.35, 0.47, 0.17), "jerrican")              # uno tumbado
    else:
        t.caja((0, 0, 0.175), (0.75, 0.4, 0.34), "caja")                             # caja abierta
        t.caja((0, 0, 0.33), (0.68, 0.33, 0.02), "interior")                         # interior (cintas)
        t.caja((0, 0.33, 0.42), (0.75, 0.04, 0.4), "caja", rot_x=0.35)                # tapa levantada
        for x, y, rot in ((1.0, -0.4, 0.5), (1.3, 0.5, 1.4), (-0.9, 0.6, 2.2)):
            _caja(t, x, y, 0.0, rot, fino=fino)
        _caja(t, 1.1, 0.5, 0.345, 1.4, fino=fino)
        _bidon(t, -0.9, -0.7, tumbado=True, eje="Y", fino=fino)
    return t.objeto(nombre, bisel=0)
