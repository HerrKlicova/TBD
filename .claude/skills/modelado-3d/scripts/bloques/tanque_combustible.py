"""Tanque de combustible horizontal del aeródromo de Bétera (D-N173; Álvaro, betera-nivel.md §5.3: unos 12 × 3 m sobre
cunas; 3 tanques en Z 35, 41 y 47 con yaw 90, dentro de un cubeto con talud). Teo (3), 8-nov.

Sin foto de Bétera. Tipo genérico de tanque aéreo de combustible: cilindro de acero con fondos abombados sobre cunas de
hormigón, pasarela con barandilla arriba, escalera en un fondo, boca de hombre, venteo y tubería de salida con válvula
abajo. Medidas SIN VERIFICAR: 12 m de largo, 3 m de diámetro y eje a 2,1 m.
Las existencias del aeródromo se leen en los tanques (D-N173): el nivel lo pone el juego, no el modelo.
Colores: cuerpo 94948c (gris), 70784c (verde) o b8b8b8 (9-nov; antes d4d0cc, reservado) (blanco) según la variante, con la generatriz de arriba más
clara b4a080 en la pasarela; cunas 94948c, tubería y escalera 6c7874, válvula 543820, óxido 7c3c18, sacos 605c48.
Origen en el suelo, en el centro; eje del tanque a lo largo de X. «Fuera» no aplica.
Variantes (TBD_VARIANTE % 5): 0 gris; 1 verde militar; 2 blanco; 3 viejo con regueros de óxido; 4 protegido con un
murete de sacos alrededor.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["cuerpo", "cuna", "metal", "pasarela", "valvula", "oxido", "sacos"]
CUERPO = {0: "94948c", 1: "70784c", 2: "b8b8b8", 3: "94948c", 4: "70784c"}[VAR]   # v2 blanco en b8b8b8 (9-nov, Lucía: d4d0cc reservado)
COLORES = {"cuerpo": CUERPO, "cuna": "94948c" if VAR != 0 else "88885c", "metal": "6c7874", "pasarela": "b4a080",
           "valvula": "543820", "oxido": "7c3c18", "sacos": "605c48"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.6
PRESUPUESTO = {0: (60, 2500), 1: (30, 1200)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
L, RT, ZE = 12.0, 1.5, 2.1


def construir(nombre="SM_TanqueCombustible", lod=0):
    v = VAR
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.06 if fino else 0.34
    lados = 16 if fino else 10
    t.cilindro((0, 0, ZE), RT, L - 0.8, "X", lados, "cuerpo")
    t.cilindro((L / 2 - 0.25, 0, ZE), RT * 0.97, 0.5, "X", lados, "cuerpo", r2=RT * 0.75)     # fondos abombados
    t.cilindro((-L / 2 + 0.25, 0, ZE), RT * 0.75, 0.5, "X", lados, "cuerpo", r2=RT * 0.97)
    for x in (-4.0, 0.0, 4.0):                           # cunas
        t.caja((x, 0, 0.45), (0.7, 2.8, 0.9), "cuna")
        t.caja((x, 0, 1.0), (0.5, 2.2, 0.5), "cuna")
    # pasarela con barandilla sobre la generatriz de arriba
    t.caja((0, 0, ZE + RT + 0.05), (L - 2.0, 0.9, 0.1), "pasarela")
    if fino:
        for s in (-1, 1):
            t.caja((0, s * 0.45, ZE + RT + 1.0), (L - 2.0, 0.05, 0.05), "metal")
            for k in range(6):
                t.caja((-L / 2 + 1.2 + k * 1.9, s * 0.45, ZE + RT + 0.55), (0.05, 0.05, 0.9), "metal")
        t.cilindro((2.5, 0.2, ZE + RT + 0.35), 0.35, 0.5, "Z", 10, "metal")          # boca de hombre
        t.cilindro((-3.5, -0.2, ZE + RT + 0.6), 0.08, 1.0, "Z", 6, "metal")         # venteo
    # escalera en el fondo −X
    t.caja((-L / 2 - 0.55, 0.9, (ZE + RT + 0.1) / 2), (0.1 if fino else 0.34, 0.7, ZE + RT + 0.1), "metal",
           rot_y=math.radians(-12))
    # tubería de salida y válvula, abajo por −Y
    t.caja((3.0, -1.0, 0.45), (0.25 if fino else 0.34, 1.2, 0.25 if fino else 0.34), "metal")
    t.caja((3.0, -1.9, 0.35), (4.0 if v != 4 else 2.0, 0.25 if fino else 0.34, 0.25 if fino else 0.34), "metal")
    t.caja((3.0, -1.55, 0.45), (0.5, 0.4, 0.5), "valvula")
    if v == 3:                                           # regueros de óxido por los costados
        for k in range(5 if fino else 3):
            x = -4.5 + 2.2 * k
            for s in (-1, 1):
                t.caja((x, s * (RT - 0.12), ZE + 0.2), (0.4, 0.3, 1.8), "oxido", rot_x=s * 0.3)
    if v == 4:                                           # murete de sacos alrededor (cubeto propio)
        for s in (-1, 1):
            t.caja((0, s * 2.6, 0.5), (L + 1.4, 0.8, 1.0), "sacos", rot_z=0.01 * s)
            t.caja((s * (L / 2 + 0.95), 0, 0.5), (0.8, 4.2, 1.0), "sacos", rot_z=0.02 * s)
    return t.objeto(nombre, bisel=0)
