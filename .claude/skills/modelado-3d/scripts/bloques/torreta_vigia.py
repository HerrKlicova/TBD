"""Torreta de vigilancia elevada de la valla de Bétera (D-N173; fotos de Paula BT6_garita_betera_1 y
BT5_valla_garita_betera_3, Bétera 2006, dominio público). Teo (3), 8-nov.

En Bétera la «garita» del perímetro es una TORRETA elevada, no una caseta de suelo (Paula): fuste cuadrado de hormigón
con aspilleras, antepecho macizo más ancho que el fuste, cabina con ventanas estrechas, tejado plano muy volado, foco en
una esquina y escalera metálica exterior hasta el antepecho. La luz roja de la foto está vetada (nada rojo ni ámbar).
La garita de suelo con barrera (garita.py) sigue para el control de acceso de la puerta principal.
Medidas ESTIMADAS en la foto (Paula: 6-8 m en total, cabina de unos 3 × 3 m): fuste de 2,6 m y 3,6 m de alto,
antepecho de 4,6 × 4,6 m y 1,7 m, cabina de 2,6 m hasta 6,4 m, tejado de 4,4 m; total 6,7 m.
Colores: verde oliva desgastado por valor partido, arriba 88885c, costados 70784c y abajo 50541c (la sombra del vuelo
y del antepecho, como en la foto); aspilleras y ventanas 605c48; escalera y foco 6c7874 con la lente f0dc9c; losa
94948c; sacos 605c48, red 50541c, concertina 6c7874. Nada en la banda de la horda (L* 9–13).
Origen en el suelo, en el centro del fuste; la escalera por +X; el frente de vigilancia hacia −Y (= +Z de Unity con
yaw 0), aunque mira a los cuatro lados.
Variantes (TBD_VARIANTE % 5): 0 la de la foto BT6, verde; 1 descolorida a ocre (la de BT5_3); 2 reforzada con sacos en
el antepecho y al pie; 3 abandonada: sin el tramo bajo de la escalera, foco descolgado y una esquina del tejado rota;
4 con red de camuflaje sobre el tejado y concertina al pie.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["muro", "hueco", "metal", "lente", "losa", "sacos", "red", "alambre"]
COLORES = {"muro": "70784c", "hueco": "605c48", "metal": "6c7874", "lente": "f0dc9c", "losa": "94948c",
           "sacos": "605c48", "red": "50541c", "alambre": "6c7874"}
VALOR_PIEZAS = {"muro": ("b4a080", "847c34", "605c48") if VAR == 1 else ("88885c", "70784c", "50541c")}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.5
PRESUPUESTO = {0: (60, 1500), 1: (30, 700)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
F, HF = 2.6, 3.6                 # fuste
AP, HA = 4.6, 1.7                # antepecho
HC, AT = 6.4, 4.4                # alto de la cabina y lado del tejado


def construir(nombre="SM_TorretaVigia", lod=0):
    v = VAR
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.08 if fino else 0.34
    t.caja((0, 0, 0.05), (4.0, 4.0, 0.12), "losa")
    t.caja((0, 0, HF / 2 + 0.05), (F, F, HF + 0.1), "muro")                      # fuste
    t.caja((0, 0, HF + HA / 2), (AP, AP, HA), "muro")                            # antepecho
    t.caja((0, 0, (HF + HA + HC) / 2 - 0.2), (F, F, HC - HF - HA + 0.4), "muro")  # cabina
    # tejado volado (en la variante 3 le falta la esquina +X+Y)
    if v == 3:
        t.caja((-0.55, 0, HC + 0.1), (AT - 1.1, AT, 0.2), "muro")
        t.caja((AT / 2 - 0.65, -0.55, HC + 0.1), (1.3, AT - 1.1, 0.2), "muro")
    else:
        t.caja((0, 0, HC + 0.1), (AT, AT, 0.2), "muro")
    # aspilleras del fuste y ventanas estrechas de la cabina (dos por cara en −Y, una en las otras)
    zc = HF + HA + 0.45
    for (nx, ny) in ((0, -1), (-1, 0), (1, 0), (0, 1)):
        xs = (-0.45, 0.45) if (nx, ny) == (0, -1) else (0.0,)
        for o in xs:
            px, py = nx * (F / 2 + 0.01) + (o if nx == 0 else 0), ny * (F / 2 + 0.01) + (o if ny == 0 else 0)
            sx, sy = (0.22, 0.04) if nx == 0 else (0.04, 0.22)
            if not fino:
                sx, sy = (0.36, 0.04) if nx == 0 else (0.04, 0.36)
            t.caja((px, py, zc), (sx, sy, 0.7), "hueco")
            if fino:
                t.caja((px, py, 1.6), (sx, sy, 0.4), "hueco")
    # escalera exterior por +X: un tramo del suelo al antepecho, con zancas
    xs = F / 2 + 0.55
    tramo0 = 1.2 if v == 3 else 0.0                  # abandonada: falta el tramo bajo
    z0, z1 = tramo0, HF
    y0, y1 = 1.6 - 3.2 * tramo0 / HF, -1.2
    L = math.hypot(z1 - z0, y1 - y0)
    ang = math.atan2(z1 - z0, y0 - y1)
    cy, cz = (y0 + y1) / 2, (z0 + z1) / 2
    for dx in ((-0.4, 0.4) if fino else (0.0,)):
        t.caja((xs + dx, cy, cz), (g, L, g * 1.5 if fino else 0.34), "metal", rot_x=-ang)
    if fino:
        for k in range(1, 9):
            f = k / 9
            t.caja((xs, y0 + (y1 - y0) * f, z0 + (z1 - z0) * f), (0.8, 0.22, 0.04), "metal")
    else:
        t.caja((xs, cy, cz), (0.8, L, 0.1), "metal", rot_x=-ang)
    t.caja((xs, -1.2, HF - 0.05), (1.0, 0.8, g), "metal")                        # rellano bajo el antepecho
    # foco en la esquina −X−Y del tejado (en la 3, descolgado)
    fx, fy = -AT / 2 + 0.3, -AT / 2 + 0.3
    fz = HC - 0.35 if v != 3 else HC - 1.2
    t.caja((fx, fy, HC - 0.1), (g, g, 0.3), "metal")
    t.caja((fx, fy, fz), (0.4, 0.3, 0.35), "metal", rot_x=0.4 if v != 3 else 1.2)
    if fino:
        t.caja((fx, fy - 0.16, fz - 0.02), (0.3, 0.03, 0.25), "lente", rot_x=0.4 if v != 3 else 1.2)
    if v == 2:                                       # sacos sobre el antepecho y al pie
        for (nx, ny) in ((0, -1), (-1, 0), (1, 0), (0, 1)):
            if nx == 0:
                t.caja((0, ny * (AP / 2 - 0.3), HF + HA + 0.22), (AP - 0.1, 0.55, 0.45), "sacos", rot_z=0.02)
            else:
                t.caja((nx * (AP / 2 - 0.3), 0, HF + HA + 0.25), (0.55, AP - 1.3, 0.45), "sacos", rot_z=-0.02)
        for s in (-1, 1):
            t.caja((s * 0.6, -F / 2 - 0.5, 0.5), (1.1, 0.8, 0.9), "sacos", rot_z=0.06 * s)
    if v == 4:                                       # red sobre el tejado y concertina al pie
        t.caja((0, 0, HC + 0.28), (AT + 0.6, AT + 0.6, 0.08), "red", rot_x=0.03)
        for s in (-1, 1):
            t.caja((s * (AT / 2 + 0.2), 0, HC - 0.4), (0.06 if fino else 0.34, AT + 0.4, 0.9), "red")
        for k in range(3):
            t.cilindro((-2.4 + 2.4 * k, -2.6, 0.32), 0.32, 2.3, "X", 8 if fino else 6, "alambre")
    return t.objeto(nombre, bisel=0)
