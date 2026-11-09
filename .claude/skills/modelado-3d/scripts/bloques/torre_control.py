"""Torre de control del helipuerto de Bétera (D-N173; Álvaro, betera-nivel.md §5.1: (226; 0; 44), yaw 270, 4 × 4 m).
Teo (3), 8-nov.

Fotos AE1 de Paula (AE1_betera_torre_1 y _2, dominio público): fuste blanco cuadrado, un cuerpo blanco bajo el fanal,
FANAL acristalado de 8 lados que se abre hacia arriba (tronco de pirámide invertido), alero oscuro, barandilla con
antenas en la azotea y ESCALERA EXTERIOR en zigzag dentro de una jaula metálica por un costado. Medidas SIN VERIFICAR,
estimadas en la foto: unos 12 m hasta el alero (Álvaro), fuste de 3,2 m, fanal de 5,4 m arriba.
Colores (Lucía, 8-nov 13:03): fachada b8b8b8 (el d4d0cc en grande es el color de selección); cristal 605c48 y
carpintería y alero 6c7874, porque con 48403c y 543820 las caras en sombra caían en la banda de la horda (L* 9–13; el
alero rojizo de la foto está vetado), escalera y antenas 6c7874, azotea b4a080 (arriba claro, D-N57), sacos 605c48, tablones a86c50.
Origen en el suelo, en el centro del fuste; la escalera en +X, el fanal mira a todos lados. «Fuera» no aplica.
Variantes (TBD_VARIANTE % 5):
  0 la de la foto.
  1 abandonada: cristales rotos y parte del fanal tapiado con tablones; antenas dobladas.
  2 fortificada: sacos terreros en la base y en la azotea, con un puesto de vigía.
  3 con caseta anexa al pie y radar en la azotea (sitio de inteligencia, betera-nivel.md §1).
  4 más alta (14 m) con un segundo cuerpo de oficinas a media altura.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["fachada", "cristal", "carpinteria", "metal", "azotea", "sacos", "tablon", "sombra"]
COLORES = {"fachada": "b8b8b8", "cristal": "605c48", "carpinteria": "6c7874", "metal": "6c7874", "azotea": "b4a080",
           "sacos": "605c48", "tablon": "a86c50", "sombra": "605c48"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.5
PRESUPUESTO = {0: (100, 3500), 1: (50, 1600)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
R = math.radians


def construir(nombre="SM_TorreControl", lod=0):
    v = VAR
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.08 if fino else 0.34                       # grosor mínimo (LOD1 ≥ 2 px)
    H = 9.6 if v != 4 else 11.6                      # arranque del fanal
    F = 3.2                                          # fuste
    # fuste y cuerpo bajo el fanal (más ancho, hacia −X, como en la foto 2)
    t.caja((0, 0, (H - 1.6) / 2), (F, F, H - 1.6), "fachada")
    t.caja((-0.3, 0, H - 0.8), (4.2, 4.0, 1.62), "fachada")
    if v == 4:                                       # segundo cuerpo de oficinas a media altura
        t.caja((-0.6, 0, 5.2), (4.4, 4.2, 2.4), "fachada")
        for y in (-2.11, 2.11):
            t.caja((-0.6, y, 5.3), (3.4, 0.04, 0.8 if fino else 0.9), "cristal")
    # fanal: tronco de octógono invertido; carpintería en las aristas
    hf = 2.6
    t.cilindro((0, 0, H + hf / 2), 2.25, hf, "Z", 8, "cristal", r2=2.75)
    t.cilindro((0, 0, H + 0.12), 2.3, 0.24, "Z", 8, "carpinteria")
    if fino:
        for k in range(8):
            a = (k + 0.5) * 2 * math.pi / 8
            r0, r1 = 2.25 / math.cos(math.pi / 8), 2.75 / math.cos(math.pi / 8)
            t.caja(((r0 + r1) / 2 * math.cos(a), (r0 + r1) / 2 * math.sin(a), H + hf / 2), (0.1, 0.1, hf + 0.05),
                   "carpinteria", rot_z=a, rot_y=0)
        t.cilindro((0, 0, H + 1.2), 2.52, 0.08, "Z", 8, "carpinteria", r2=2.55)
    # alero y azotea
    t.cilindro((0, 0, H + hf + 0.18), 2.95, 0.36, "Z", 8, "carpinteria")
    t.cilindro((0, 0, H + hf + 0.38), 2.7, 0.06, "Z", 8, "azotea")
    zt = H + hf + 0.41
    # barandilla y antenas (en el LOD1, solo las gruesas)
    if fino:
        for k in range(8):
            a = k * 2 * math.pi / 8
            t.caja((2.6 * math.cos(a), 2.6 * math.sin(a), zt + 0.5), (0.05, 0.05, 1.0), "metal")
        t.cilindro((0, 0, zt + 1.0), 2.62, 0.05, "Z", 8, "metal", r2=2.62)
    n_ant = 6 if fino else 2
    for k in range(n_ant):
        a = k * 2 * math.pi / n_ant + 0.3
        h = (1.4, 2.4, 1.0, 1.8, 3.2, 1.2)[k % 6] * (0.6 if v == 1 else 1.0)
        rr = 1.6 if fino else 1.2
        t.caja((rr * math.cos(a), rr * math.sin(a), zt + h / 2), (g * 0.8, g * 0.8, h), "metal",
               rot_y=R(25) if (v == 1 and k % 2) else 0)
    # escalera exterior en zigzag dentro de su jaula, por +X (la de la foto)
    xs = F / 2 + 0.6
    tramo = 2.4
    z = 0.0
    k = 0
    while z + tramo <= H - 1.0:
        y0, y1 = (-1.0, 1.0) if k % 2 == 0 else (1.0, -1.0)
        t.caja((xs, y1, z + tramo), (1.1, 0.9, g), "metal")                  # rellano
        if fino:
            t.caja((xs, 0, z + tramo / 2), (1.0, 2.6, 0.08), "metal", rot_x=math.atan2(tramo, 2.0) * (1 if k % 2 == 0 else -1))
            t.caja((xs + 0.55, y1, z + tramo + 0.55), (0.05, 0.9, 1.1), "metal")   # jaula
        else:
            t.caja((xs, 0, z + tramo / 2), (0.9, 2.4, 0.34), "metal", rot_x=math.atan2(tramo, 2.0) * (1 if k % 2 == 0 else -1))
        z += tramo
        k += 1
    for y in (-1.15, 1.15):                          # montantes de la jaula
        t.caja((xs + 0.55, y, (z + 1.0) / 2), (g, g, z + 1.0), "metal")
    # puerta al pie (−X) y una ventanita en el cuerpo (la de la foto 2)
    t.caja((-F / 2 - 0.02, 0, 1.1), (0.04, 1.0 if fino else 1.0, 2.1), "carpinteria")
    t.caja((-2.42, -0.8, H - 0.6), (0.04, 0.5, 0.5 if fino else 0.4), "cristal")
    # variantes
    if v == 1:                                       # tablones sobre tres caras del fanal y cristal caído
        for k in (0, 3, 5):
            a = (k + 0.5) * 2 * math.pi / 8 + math.pi / 8
            r = 2.62
            t.caja((r * math.cos(a), r * math.sin(a), H + 1.2), (0.12, 1.6, 1.6), "tablon", rot_z=a)
        for k in range(4 if fino else 2):
            t.caja((-2.2 + 1.3 * k, 2.6 + 0.4 * (k % 2), 0.04), (0.6, 0.4, 0.06), "cristal", rot_z=k)
    if v == 2:                                       # sacos al pie y vigía en la azotea
        for s in (-1, 1):
            t.caja((-2.3, s * 1.0, 0.5), (0.8, 1.55, 1.0), "sacos", rot_z=0.05)
            t.caja((0.1, s * 2.3, 0.55), (2.5, 0.8, 1.1), "sacos", rot_z=-0.04)
        for k in range(4):
            a = k * math.pi / 2
            t.caja((1.6 * math.cos(a), 1.6 * math.sin(a), zt + 0.45 + 0.05 * (k % 2)),
                   (0.6 if k % 2 == 0 else 2.45, 2.45 if k % 2 == 0 else 0.6, 0.9), "sacos", rot_z=0.03 * k)
    if v == 3:                                       # caseta anexa (−X) y radar en la azotea
        t.caja((-4.4, 0.6, 1.5), (4.0, 5.0, 3.0), "fachada")
        t.caja((-4.4, 0.6, 3.08), (4.3, 5.3, 0.16), "azotea")
        t.caja((-6.42, 0.6, 1.6), (0.04, 2.4, 0.9 if fino else 0.9), "cristal")
        t.cilindro((0.4, 0, zt + 0.6), 0.25, 1.2, "Z", 8, "metal")
        t.caja((0.4, 0, zt + 1.35), (0.5, 3.0, 0.5), "metal", rot_z=R(30))
    if v == 4:
        pass
    return t.objeto(nombre, bisel=0)
