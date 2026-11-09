"""Escalera exterior a la azotea del edificio de viajeros de Sagunto (Álvaro, 8-nov, sagunto-nivel.md §10.15: testero
este, X 115,5–117,5 y Z 76–82, del patio del recinto a la azotea de 11 m). Teo (3), 8-nov.

Planta de 2 × 6 m pegada al testero. Cuatro tramos en zigzag de 2,75 m de subida cada uno (34°), en dos carriles de
1 m a lo largo del muro, con descansillos de 1 m en las dos puntas, y arriba una plataforma de 2 × 6 m a 11 m que da a la
azotea por el hueco del pretil (esquina con paso: Z 78,2–79,4). Como la escalera de la torre de control de Bétera.
Colores: metal 6c7874, peldaños y plataformas 94948c (arriba claro), obra b8b8b8 con zócalo 94948c, tubos de andamio
6c7874 con tablones 845c28, escombro 48403c (solo en el suelo). Medidas SIN VERIFICAR.
Origen: en el suelo, en el centro de la planta. El testero (el muro del edificio) queda en −X; los tramos van a lo largo
de Y (Z de Unity en el testero este). La salida a la azotea es por −X, a 11 m.
Variantes (TBD_VARIANTE % 5): 0 metálica con barandilla; 1 metálica con jaula (como la torre); 2 de obra, con muretes
macizos; 3 de andamio de tubo con tablones (provisional, montada por los ingenieros); 4 metálica dañada: le falta el
tramo de abajo y hay una escalera de mano en su lugar.
"""
import math, os
from taller import Taller

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["metal", "peldano", "obra", "zocalo", "tablon", "escombro"]
COLORES = {"metal": "6c7874", "peldano": "94948c", "obra": "b8b8b8", "zocalo": "94948c", "tablon": "845c28",
           "escombro": "48403c"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.6
PRESUPUESTO = {0: (40, 4000), 1: (20, 1600)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
AX, LY, HT, NT = 2.0, 6.0, 11.0, 4
DES = 1.0                                  # fondo de los descansillos
SUB = HT / NT


def construir(nombre="SM_EscaleraAzotea", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.08 if fino else 0.34
    paso = "tablon" if V == 3 else "peldano"
    run = LY - 2 * DES
    a = math.atan2(SUB, run)
    L = math.hypot(SUB, run)
    for k in range(NT):
        z0 = k * SUB
        xc = -0.5 if k % 2 == 0 else 0.5             # carril de dentro (pegado al muro) y de fuera
        sube = 1 if k % 2 == 0 else -1               # +Y o −Y
        zc = z0 + SUB / 2
        if V == 4 and k == 0:                        # tramo de abajo caído: escalera de mano y restos
            for s in (-1, 1):
                t.caja((xc + s * 0.3, -LY / 2 + DES + 0.5, SUB / 2), (g, g, SUB + 0.2), "metal", rot_x=0.2)
            for j in range(6 if fino else 2):
                t.caja((xc, -LY / 2 + DES + 0.5 + 0.1 * j * 0, 0.4 + j * 0.45 if fino else 0.9 + j * 1.0),
                       (0.6, g, g), "metal", rot_x=0.2)
            t.caja((0.9, -0.5, 0.1), (1.2, 2.6, 0.2), "escombro", rot_z=0.4)
            t.caja((xc + 1.1, 0.8, 0.15), (0.6, 3.6, 0.3), "metal", rot_z=0.3, rot_y=0.1)
        elif V == 2:                                 # de obra: losa inclinada sobre murete macizo
            t.caja((xc, 0, zc), (0.98, L, 0.25), paso, rot_x=sube * a)
            if k < 2:                                # muretes macizos bajo los dos primeros tramos
                t.caja((xc, 0, z0 / 2 + SUB / 4), (0.96, run - 0.2, z0 + SUB / 2), "obra")
            else:                                    # los de arriba, sobre una viga de canto
                t.caja((xc, 0, zc - 0.35), (0.6, L, 0.4), "obra", rot_x=sube * a)
        else:
            t.caja((xc, 0, zc), (0.95, L, 0.08 if fino else 0.17), paso, rot_x=sube * a)
            if fino:                                 # peldaños
                n = 14
                for j in range(n):
                    yy = -sube * run / 2 + sube * (j + 0.5) * run / n
                    t.caja((xc, yy, z0 + (j + 0.5) * SUB / n + 0.06), (0.9, 0.24, 0.04), paso)
            for s in (-1, 1):                        # zancas
                t.caja((xc + s * 0.47, 0, zc), (0.06 if fino else 0.17, L, 0.25), "metal", rot_x=sube * a)
            # barandilla exterior (en el carril de fuera) a 1 m sobre el tramo
            if xc > 0 or V == 3:
                t.caja((xc + 0.48, 0, zc + 1.0), (g, L, g), "metal", rot_x=sube * a)
        # descansillo al final del tramo
        yd = sube * (LY / 2 - DES / 2)
        zd = z0 + SUB
        if k < NT - 1:
            t.caja((0, yd, zd - 0.06), (AX, DES, 0.12 if fino else 0.17), paso)
    # plataforma de arriba (toda la planta, a 11 m) con barandilla por fuera
    t.caja((0, 0, HT - 0.06), (AX, LY, 0.12 if fino else 0.17), paso)
    t.caja((AX / 2 - 0.05, 0, HT + 0.5), (g, LY, g), "metal")
    t.caja((AX / 2 - 0.05, 0, HT + 1.0), (g, LY, g), "metal")
    for s in (-1, 1):
        t.caja((0, s * (LY / 2 - 0.05), HT + 0.5), (AX, g, 1.0 if V == 2 else g), "obra" if V == 2 else "metal")
    # pilares: de obra (muretes en las puntas) o de tubo en las esquinas
    if V == 2:
        for s in (-1, 1):
            t.caja((0.5, s * (LY / 2 - DES / 2), HT / 2 - 0.05), (1.0, DES, HT - 0.1), "obra")
            t.caja((0.5, s * (LY / 2 - DES / 2), 0.5), (1.04, DES + 0.04, 1.0), "zocalo")
    else:
        for sy in (-1, 1):
            for x in (AX / 2 - 0.1, -AX / 2 + 0.1):
                t.caja((x, sy * (LY / 2 - 0.1), HT / 2), (g * 1.5 if fino else 0.34, g * 1.5 if fino else 0.34, HT), "metal")
        if V == 3:                                   # andamio: tubos intermedios y diagonales
            for z in (2.75, 5.5, 8.25):
                t.caja((AX / 2 - 0.1, 0, z), (g, LY - 0.2, g), "metal")
            for k in range(4):
                z0 = k * SUB
                t.caja((AX / 2 - 0.1, 0, z0 + SUB / 2), (g, math.hypot(LY - 0.2, SUB), g), "metal",
                       rot_x=(1 if k % 2 else -1) * math.atan2(SUB, LY - 0.2))
    if V == 1:                                       # jaula: aros y montantes por fuera
        for z in range(2, 11, 2):
            t.caja((AX / 2 + 0.25, 0, z), (g, LY + 0.3, g), "metal")
            for s in (-1, 1):
                t.caja((AX / 4 + 0.1, s * (LY / 2 + 0.15), z), (AX / 2 + 0.3, g, g), "metal")
        for y in (-2.0, 0.0, 2.0):
            t.caja((AX / 2 + 0.25, y, HT / 2 + 0.5), (g, g, HT + 1.0), "metal")
    return t.objeto(nombre, bisel=0)
