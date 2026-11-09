"""Ambiente del aeródromo de Bétera (Teo, 8-nov 18:45; elegido por Teo tras ver la captura de Iker betera_v1_general:
la plataforma es un plano gris vacío y P2–P3 solo son cuadrados blancos).

VAR = 5·pieza + variante (0..24). Origen en el suelo, en el centro de la pieza. Medidas DE MEMORIA (SIN VERIFICAR).
Nada de ámbar ni rojo: la manga de viento real es naranja y blanca, aquí va a franjas b8b8b8 y 605c48 (Lucía); los conos, igual.
Piezas:
  0 Manga de viento: mástil de 6 m y manga de 3,6 m. Variantes: inflada hacia +X, media, caída, rota, y con la base de
    hormigón y la manga hacia −Y.
  1 Balizas de borde (para los lados de los claros de 25 m y de la plataforma): tramo de 12 m a lo largo de X. Variantes:
    5 blancas en línea, 5 verdes de umbral, en L (6 + 6 m), con dos rotas o tumbadas, y conos portátiles.
  2 Caseta prefabricada de 6 × 2,4 × 2,6 m (a lo largo de X, la puerta a −Y). Variantes: oficina de operaciones,
    almacén de puerta doble, dos apiladas con escalera, con sacos terreros alrededor, y con antena y red de camuflaje.
  3 Torre de iluminación con generador en remolque. Variantes: desplegada, plegada, apuntando al otro lado con cable al
    suelo, con bidones al lado, y caída.
  4 Vallas de plataforma. Variantes: 4 vallas de obra en línea, en L, 3 barreras de hormigón (jersey), conos con cinta,
    y vallas caídas.
Colores: metal 94948c, base 6c7874, blanco b8b8b8, verde de umbral 70784c, caseta b4a080 con techo 94948c, lunas
6c7874, puertas 605c48, remolque 847c34, sacos 605c48, red 50541c, hormigón 94948c.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 25
PIEZA, V = VAR // 5, VAR % 5
NOMBRES_P = ["MangaViento", "Balizas", "Caseta", "TorreLuz", "Vallas"]
NOMBRES_V = [["Inflada", "Media", "Caida", "Rota", "Base"], ["Blancas", "Verdes", "EnL", "Rotas", "Conos"],
             ["Oficina", "Almacen", "Apiladas", "Sacos", "Antena"], ["Desplegada", "Plegada", "Cable", "Bidones", "Caida"],
             ["Obra", "ObraL", "Jersey", "Conos", "Caidas"]]
PIEZAS = ["metal", "base", "blanco", "verde", "caseta", "techo", "luna", "puerta", "remolque", "sacos", "red",
          "hormigon", "goma"]
COLORES = {"metal": "94948c", "base": "6c7874", "blanco": "b8b8b8", "verde": "70784c", "caseta": "b4a080",
           "techo": "94948c", "luna": "6c7874", "puerta": "605c48", "remolque": "847c34", "sacos": "605c48",
           "red": "50541c", "hormigon": "94948c", "goma": "605c48"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 2500), 1: (10, 800)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_AmbienteAerodromo", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.1 if fino else 0.34
    lados = 10 if fino else 6
    p, v = PIEZA, V
    if p == 0:                                        # MANGA DE VIENTO
        if v == 4:
            t.caja((0, 0, 0.15), (1.2, 1.2, 0.3), "hormigon")
        alto = 6.0
        if v == 3:                                    # mástil doblado
            t.caja((0, 0, 1.8), (g, g, 3.6), "metal")
            t.caja((0.6, 0, 4.3), (g, g, 1.6), "metal", rot_y=0.6)
            top = (1.05, 0, 5.0)
        else:
            t.caja((0, 0, alto / 2), (g, g, alto), "metal")
            top = (0, 0, alto)
        ang = {0: 0.0, 1: 0.45, 2: 1.25, 3: 1.4, 4: 0.1}[v]       # caída respecto a la horizontal
        rz = -math.pi / 2 if v == 4 else 0.0
        largo = 1.6 if v == 3 else 3.6
        segs = 3 if v != 3 else 1
        for i in range(segs):
            r1 = 0.45 - 0.25 * i / segs
            r2 = 0.45 - 0.25 * (i + 1) / segs
            l = largo / segs
            d = l * (i + 0.5)
            dx, dz = d * math.cos(ang), -d * math.sin(ang)
            cx, cy = top[0] + dx * math.cos(rz), top[1] + dx * math.sin(rz)
            rm = max((r1 + r2) / 2, 0.17)
            t.caja((cx, cy, top[2] + dz), (l + 0.02, 2 * rm, 2 * rm), "blanco" if i % 2 == 0 else "puerta",
                   rot_y=ang, rot_z=rz)
        if fino:
            t.caja((top[0], top[1], top[2]), (0.08, 0.95, 0.08), "metal", rot_z=rz)  # aro
    elif p == 1:                                      # BALIZAS
        pts = [(-6 + i * 3, 0) for i in range(5)]
        if v == 2:
            pts = [(-6 + i * 3, 0) for i in range(3)] + [(0, 3), (0, 6)]
        for i, (x, y) in enumerate(pts):
            if v == 4:                                # cono portátil
                t.cilindro((x, y, 0.36), 0.2, 0.7, "Z", lados, "blanco", r2=0.04 if fino else 0.17)
                t.caja((x, y, 0.02), (0.42, 0.42, 0.04), "base")
                if fino:
                    t.cilindro((x, y, 0.4), 0.13, 0.12, "Z", lados, "base", r2=0.12)
                continue
            if v == 3 and i in (1, 3):                # rota o tumbada
                t.caja((x + 0.2, y + 0.1, 0.12), (0.35, 0.24, 0.24), "base", rot_z=_h(i) * 3)
                continue
            t.cilindro((x, y, 0.15), 0.17, 0.3, "Z", lados, "base")
            t.cilindro((x, y, 0.37), 0.15 if fino else 0.175, 0.16, "Z", lados, "verde" if v == 1 else "blanco")
            if fino:
                t.caja((x, y, 0.01), (0.5, 0.5, 0.02), "hormigon")
    elif p == 2:                                      # CASETA PREFABRICADA
        def caseta(z0, puerta_doble=False, ventanas=True):
            t.caja((0, 0, z0 + 1.3), (6.0, 2.4, 2.6), "caseta")
            t.caja((0, 0, z0 + 2.64), (6.1, 2.5, 0.08), "techo")
            if puerta_doble:
                t.caja((0, -1.21, z0 + 1.1), (1.8, 0.04, 2.1), "puerta")
            else:
                t.caja((-1.8, -1.21, z0 + 1.0), (0.9, 0.04, 2.0), "puerta")
            if ventanas:
                for x in (0.2, 1.9):
                    t.caja((x, -1.21, z0 + 1.5), (1.0, 0.04, 0.8), "luna")
                t.caja((3.01, 0, z0 + 1.5), (0.04, 0.8, 0.6), "luna")
                if fino:
                    t.caja((2.4, 1.35, z0 + 2.0), (0.8, 0.3, 0.55), "metal")          # aire acondicionado
        caseta(0.0, puerta_doble=v == 1, ventanas=v != 1)
        if v == 2:
            caseta(2.69)
            for k in range(6):                        # escalera exterior en el testero +X
                t.caja((3.5, -0.6 + k * 0.25, 0.22 + k * 0.45), (0.9, 0.3, 0.06 if fino else 0.34), "metal")
            t.caja((3.5, 0.75, 2.75), (0.9, 1.2, 0.08), "metal")
        if fino or v != 2:
            t.caja((-1.8, -1.6, 0.1), (1.0, 0.7, 0.2), "hormigon")                       # escalón
        if v == 3:
            for s in (-1, 1):
                for k in range(7):
                    for f in range(3):
                        t.caja((-3.0 + k * 0.88 + (f % 2) * 0.44, s * 2.0, 0.15 + f * 0.3), (0.85, 0.48, 0.29),
                               "sacos")
        if v == 4:
            t.caja((2.2, 0.6, 2.68 + 2.0), (g * 0.6 if fino else 0.34, g * 0.6 if fino else 0.34, 4.0), "metal")
            t.caja((0, 0, 2.75), (6.6, 3.0, 0.06), "red")
            for s in (-1, 1):
                t.caja((0, s * 1.5, 1.6), (6.6, 0.05 if fino else 0.34, 2.2), "red", rot_x=s * 0.2)
    elif p == 3:                                      # TORRE DE ILUMINACIÓN
        caida = v == 4
        rz = math.pi if v == 2 else 0.0
        t.caja((0, 0, 0.75), (1.4, 2.8, 1.1), "remolque", rot_z=0.0 if not caida else 0.0)
        for s in (-1, 1):
            t.cilindro((s * 0.75, 0.3, 0.35), 0.35, 0.22, "X", lados, "goma")
        t.caja((0, -1.8, 0.45), (g, 1.0, g), "metal")                                   # lanza
        for s in (-1, 1):                                                              # estabilizadores
            t.caja((s * 1.1, 1.0, 0.2), (0.9, g, g), "metal")
        if v == 1:                                    # plegada sobre el remolque
            t.caja((0, 0.2, 1.45), (0.25, 4.0, 0.25), "metal")
            t.caja((0, 2.3, 1.55), (1.2, 0.3, 0.5), "blanco")
        elif caida:                                   # mástil tumbado en el suelo
            t.caja((-0.73, 4.14, 0.15), (0.25, 7.0, 0.25), "metal", rot_z=0.3)
            t.caja((-1.8, 7.6, 0.3), (1.4, 0.5, 0.6), "blanco", rot_z=0.3)
        else:
            t.caja((0, 0.6, 1.3 + 3.5), (0.2 if fino else 0.34, 0.2 if fino else 0.34, 7.0), "metal")
            yl = 0.6 + (-0.2 if rz == 0 else 0.2)
            t.caja((0, yl, 8.4), (1.4, 0.2, 0.6), "base")
            for i in range(4 if fino else 1):
                t.caja(((i - 1.5) * 0.33 if fino else 0, yl - (0.11 if rz == 0 else -0.11), 8.4),
                       (0.3 if fino else 1.3, 0.03 if fino else 0.34, 0.45), "blanco")
        if v == 2 and fino:                           # cable por el suelo
            for i in range(3):
                t.caja((1.2 + i * 1.5, -0.5 - i * 0.6, 0.02), (1.6, 0.04, 0.04), "goma", rot_z=-0.4)
        if v == 3:
            for i in range(3):
                t.cilindro((1.6 + (i % 2) * 0.65, -0.6 + i * 0.55, 0.44), 0.29, 0.88, "Z", lados, "puerta")
    else:                                             # VALLAS
        def valla(x, y, rz=0.0, caida=False):
            c, s = math.cos(rz), math.sin(rz)
            if caida:
                t.caja((x, y, 0.05), (2.5, 1.0, 0.06 if fino else 0.34), "metal", rot_z=rz)
                return
            t.caja((x, y, 0.6), (2.5, 0.04 if fino else 0.34, 1.0), "metal", rot_z=rz)
            for sx in (-1.1, 1.1):
                t.caja((x + sx * c, y + sx * s, 0.06), (0.5, 0.25, 0.12), "base", rot_z=rz)
        if v in (0, 1, 4):
            for i in range(4):
                if v == 1 and i >= 2:
                    valla(-5.05, 1.3 + 2.55 * (i - 2), math.pi / 2)
                else:
                    valla(-3.8 + i * 2.55, 0, 0.0, caida=v == 4 and i in (1, 2))
                    if v == 4 and i == 3:
                        valla(-3.8 + i * 2.55 + 0.3, 1.0, 0.5, caida=True)
        elif v == 2:                                  # barreras jersey
            for i in range(3):
                t.caja((-2.05 + i * 2.05, 0, 0.2), (2.0, 0.6, 0.4), "hormigon")
                t.caja((-2.05 + i * 2.05, 0, 0.6), (2.0, 0.2 if fino else 0.34, 0.42), "hormigon")
        else:                                         # conos con cinta
            for i in range(5):
                x = -4 + i * 2
                t.cilindro((x, 0, 0.36), 0.2, 0.7, "Z", lados, "blanco", r2=0.04 if fino else 0.17)
                t.caja((x, 0, 0.02), (0.42, 0.42, 0.04), "base")
            if fino:
                t.caja((0, 0, 0.62), (8.0, 0.01, 0.06), "blanco")
    return t.objeto(nombre, bisel=0)
