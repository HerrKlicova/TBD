"""Poste del foco del recinto (lista de cajas de Bruno, 9-nov 22:30: «Poste del foco» en cada foco del recinto, caja de
0,25 × 1,6 m colocada en pos + (0,3; −0,8; 0) bajo el foco). Teo (4), 9-nov.

Solo el poste y su base: el foco y su luz los pone el juego. Montaje de obra improvisado. MEDIDAS SIN VERIFICAR.
Origen en el suelo, en el eje del poste; el soporte del foco queda arriba, a 1,6 m, con el brazo hacia −X 0,3 m (donde el
código pone el foco). Colores: tubo 94948c, hormigón 88885c, madera 845c28, sacos 605c48, bidón 70784c, cable 6c7874.
Nada en la banda, nada rojo ni ámbar.
Variantes (TBD_VARIANTE % 5): 0 tubo de andamio en un bloque de hormigón; 1 poste de madera calzado con sacos; 2 trípode
de obra; 3 tubo atado a un bidón lleno de arena; 4 tubo con tres vientos de cable a piquetas.
"""
import math, os
from taller import Taller

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Hormigon", "Madera", "Tripode", "Bidon", "Vientos"]
PIEZAS = ["tubo", "hormigon", "madera", "sacos", "bidon", "cable"]
COLORES = {"tubo": "94948c", "hormigon": "88885c", "madera": "845c28", "sacos": "605c48", "bidon": "70784c",
           "cable": "6c7874"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.5
PRESUPUESTO = {0: (12, 400), 1: (8, 160)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
H = 1.6


def construir(nombre="SM_PosteFoco", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    lados = 8 if fino else 6
    if V == 1:
        t.caja((0, 0, H / 2 + 0.05), (0.16, 0.16, H + 0.1), "madera")
    elif V == 2:
        for k in range(3):                                                  # trípode
            a = k * 2.094
            t.caja((math.cos(a) * 0.3, math.sin(a) * 0.3, 0.55), (0.05, 0.05, 1.2), "tubo",
                   rot_z=a, rot_y=-0.45 if True else 0)
        t.cilindro((0, 0, 1.15), 0.035, 0.9, "Z", lados, "tubo")
    else:
        t.cilindro((0, 0, H / 2), 0.045, H, "Z", lados, "tubo")
    # brazo y soporte del foco, arriba
    t.caja((-0.15, 0, H), (0.32, 0.06, 0.06), "tubo")
    t.caja((-0.3, 0, H - 0.06), (0.1, 0.1, 0.06), "tubo")
    if V == 0:
        t.caja((0, 0, 0.15), (0.5, 0.5, 0.3), "hormigon")
    if V == 1:
        for k in range(4 if fino else 2):
            a = k * 1.571 + 0.3
            t.caja((math.cos(a) * 0.32, math.sin(a) * 0.32, 0.12), (0.5, 0.3, 0.22), "sacos", rot_z=a + 1.571)
        if fino:
            t.caja((0, 0, 0.33), (0.45, 0.28, 0.2), "sacos", rot_z=0.8)
    if V == 3:
        t.cilindro((0.0, 0.0, 0.45), 0.29, 0.9, "Z", 10 if fino else 6, "bidon")
        if fino:
            for z in (0.3, 0.6):
                t.cilindro((0, 0, z), 0.3, 0.03, "Z", 10, "bidon")
            t.caja((0, 0, 0.75), (0.62, 0.04, 0.03), "cable")
    if V == 4:
        t.caja((0, 0, 0.05), (0.3, 0.3, 0.1), "hormigon")
        for k in range(3):
            a = k * 2.094 + 0.5
            d = 0.9
            L = math.hypot(d, H - 0.2)
            ang = math.atan2(d, H - 0.2)
            t.caja((math.cos(a) * d / 2, math.sin(a) * d / 2, (H - 0.2) / 2), (0.015 if fino else 0.04, 0.015 if fino else 0.04, L),
                   "cable", rot_z=a, rot_y=-ang)
            t.caja((math.cos(a) * d, math.sin(a) * d, 0.08), (0.04, 0.04, 0.16), "tubo")
    return t.objeto(nombre, bisel=0)
