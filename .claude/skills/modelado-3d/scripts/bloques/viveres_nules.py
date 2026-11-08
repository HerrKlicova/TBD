"""Víveres de Nules: la huerta de naranjos que produce los víveres (D-N176, D-N178). Encargo de Lucía, 8-nov 21:15
(docs/decisiones-noche.md), con el informe de Paula betera_rearme_y_nules_viveres_v1.md (NV1–NV5). Ninguna foto es de
Nules: son guía, no calco. Teo (relevo). Medidas SIN VERIFICAR.

VAR = 5·pieza + variante (0..24). Origen en el suelo, en el centro; lo largo, a lo largo de X; la cara a la calle, en −Y.
D-N178: las TAPIAS cortan la línea de visión y llevan COLISIÓN DE MURO; los naranjos NO cortan la vista (sin
colisión de visión). La casa de labor es sólida, como las casas. La balsa y los palots, lo que decida Álvaro.
Nada de naranja chillón: el ámbar es de los avisos. La fruta es pequeña y apagada (a86c50, como el naranjo aprobado).
Piezas:
  0 Naranjos en hilera (NV1 adultos, NV3 jóvenes; marco de 5 m): Plantones (con tutor), Jovenes (2–3 años),
    HileraJoven (4 en 15 m), HileraAdulta (3 en 10 m, con fruta), Calva (hilera adulta con un hueco y un tocón).
  1 Tapia de huerto (NV2: mampostería encalada, 2,2 m, albardilla de teja; tramo de 10 m, como el módulo de Álvaro): Recto, Esquina, Porton,
    Roto, Contrafuertes.
  2 Casa de labor de dos plantas con cipreses (NV2_casa_huerto; 10 × 8 m, el máximo de Álvaro): Encalada, Ocre, Porche (emparrado
    delante), Abandonada (huecos tapiados y tejado roto), Cipreses (fila de 4 cipreses en vez de 2).
  3 Balsa de riego (NV5: lámina impermeable y agua; Ø 14 m): Llena, Media, Vacia, Vallada, Rota (talud caído).
  4 Palots genéricos (cajones de 1,2 × 1,0 × 0,75 m; sin copiar los de manzana de NV4): Pila2, Bloque (3 × 2 × 3),
    Fila, Volcados, ConFruta.
Colores: copa 70784c (arriba 88885c, abajo 50541c), tronco 605c48, fruta a86c50, tutor b4a080, cal d4d0cc, piedra
94948c, teja a86c50, carpintería 605c48, ciprés 50541c (arriba 70784c), agua 6c7874, lámina 605c48, valla b8b8b8,
palot b8b8b8 / 94948c, tierra 845c28 (solo arriba).
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 25
PIEZA, V = VAR // 5, VAR % 5
NOMBRES_P = ["Naranjos", "Tapia", "CasaLabor", "Balsa", "Palots"]
NOMBRES_V = [["Plantones", "Jovenes", "HileraJoven", "HileraAdulta", "Calva"],
             ["Recto", "Esquina", "Porton", "Roto", "Contrafuertes"],
             ["Encalada", "Ocre", "Porche", "Abandonada", "Cipreses"],
             ["Llena", "Media", "Vacia", "Vallada", "Rota"],
             ["Pila2", "Bloque", "Fila", "Volcados", "ConFruta"]]
PIEZAS = ["copa", "tronco", "fruta", "tutor", "cal", "piedra", "teja", "carpinteria", "cipres", "agua", "lamina",
          "valla", "palot", "palot2", "tierra", "hueco"]
COLORES = {"copa": "70784c", "tronco": "605c48", "fruta": "a86c50", "tutor": "b4a080",
           "cal": "f0dc9c" if (PIEZA, V) == (2, 1) else "d4d0cc", "piedra": "94948c", "teja": "a86c50",
           "carpinteria": "605c48", "cipres": "50541c", "agua": "6c7874", "lamina": "605c48", "valla": "b8b8b8",
           "palot": "b8b8b8", "palot2": "94948c", "tierra": "845c28", "hueco": "605c48"}
VALOR_PIEZAS = {"copa": ("88885c", "70784c", "50541c"), "cipres": ("70784c", "50541c", "50541c")}
FACCION = "neutro"
PRESUPUESTO = {0: (20, 2500), 1: (10, 1000)}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_ViveresNules", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    m = 0.34
    g = lambda a: a if fino else max(a, m)
    p, v = PIEZA, V
    sub = 2 if fino else 1

    def naranjo(x, y, esc=1.0, fruta=True, k=0):
        h0 = 1.0 * esc
        t.caja((x, y, h0 / 2), (g(0.18 * esc), g(0.18 * esc), h0), "tronco")
        cr = 1.8 * esc
        for i in range(4 if fino else 2):           # copa de grumos
            a = i * 1.7 + k
            t.esfera((x + math.cos(a) * cr * 0.35, y + math.sin(a) * cr * 0.35, h0 + cr * 0.75 + (i % 2) * 0.25 * esc),
                     (cr * 0.75, cr * 0.75, cr * 0.65), "copa", sub=sub)
        if fruta and esc > 0.7:
            for i in range(6 if fino else 4):
                a = i * 1.05 + k
                t.caja((x + math.cos(a) * cr * 0.95, y + math.sin(a) * cr * 0.95, h0 + cr * (0.5 + 0.4 * _h(i, k))),
                       (g(0.2), g(0.2), g(0.2)), "fruta")

    def joven(x, y, alto, tutor=True):
        t.caja((x, y, alto * 0.3), (g(0.05), g(0.05), alto * 0.6), "tronco")
        t.esfera((x, y, alto * 0.75), (alto * 0.3, alto * 0.3, alto * 0.3), "copa", sub=1)
        if tutor:
            t.caja((x + 0.08, y, alto * 0.45), (g(0.03), g(0.03), alto * 0.9), "tutor")

    def cipres(x, y, alto=8.0):
        t.caja((x, y, 0.4), (g(0.2), g(0.2), 0.8), "tronco")
        t.cilindro((x, y, 0.6 + alto / 2), 0.9, alto, "Z", 8 if fino else 6, "cipres", r2=0.15)

    if p == 0:                                        # NARANJOS EN HILERA
        t.caja((0, 0, 0.02), (16.0 if v in (2, 3, 4) else 6.0, 2.4, 0.04), "tierra")         # caballón
        if v == 0:
            for k in range(3):
                joven(-1.8 + k * 1.8, 0, 0.8)
        elif v == 1:
            for k in range(2):
                joven(-1.5 + k * 3.0, 0, 1.6, tutor=k == 0)
        elif v == 2:
            for k in range(4):
                joven(-7.5 + k * 5.0, 0, 1.2 + 0.3 * (k % 2))
        elif v == 3:
            for k in range(3):
                naranjo(-5.0 + k * 5.0, 0, 1.0 - 0.08 * (k % 2), k=k)
        else:
            naranjo(-5.0, 0, 1.0, k=1)
            t.cilindro((0, 0, 0.2), 0.16 if fino else m, 0.4, "Z", 6, "tronco")              # tocón
            naranjo(5.0, 0, 0.92, fruta=False, k=2)

    elif p == 1:                                      # TAPIA DE HUERTO (colisión de muro)
        H, E = 2.2, 0.4
        def tramo(x0, x1, y=0.0, alto=H, eje="X"):
            L = abs(x1 - x0)
            c = (x0 + x1) / 2
            if eje == "X":
                t.caja((c, y, alto / 2), (L, E, alto), "cal")
                t.caja((c, y, alto + 0.06), (L + 0.05, E + 0.14, 0.12), "teja")
                if fino:
                    t.caja((c, y - E / 2 - 0.01, 0.25), (L, 0.04, 0.5), "piedra")            # zócalo sin cal
            else:
                t.caja((y, c, alto / 2), (E, L, alto), "cal")
                t.caja((y, c, alto + 0.06), (E + 0.14, L + 0.05, 0.12), "teja")
        if v == 0:
            tramo(-5, 5)
        elif v == 1:                                  # esquina: brazos de 3 m hacia −X y +Y
            tramo(-5, 0.2)
            tramo(0.15, 5.0, y=0.0, eje="Y")
        elif v == 2:                                  # portón de 2,4 m con pilares
            tramo(-5, -1.4)
            tramo(1.4, 5)
            for sx in (-1.3, 1.3):
                t.caja((sx, 0, 1.3), (0.5, 0.55, 2.6), "cal")
                t.caja((sx, 0, 2.66), (0.6, 0.65, 0.12), "teja")
            t.caja((0, 0.05, 1.0), (2.1, 0.08, 2.0), "carpinteria")
        elif v == 3:                                  # tramo roto: hueco y cascotes
            tramo(-5, -0.8)
            tramo(1.2, 5, alto=1.4)
            t.caja((0.2, 0, 0.3), (1.9, E - 0.02, 0.6), "cal")
            for k in range(6):
                t.caja((-0.6 + _h(k) * 2, -0.6 - _h(k, 1) * 1.2, 0.12), (0.4, 0.3, 0.24),
                       ("piedra", "cal", "teja")[k % 3], rot_z=_h(k, 2) * 3)
        else:                                         # con contrafuertes (lado de la calle)
            tramo(-5, 5)
            for x in (-3.0, 0.0, 3.0):
                t.caja((x, -0.45, 0.8), (0.5, 0.5, 1.6), "cal", inclinar=0.3)

    elif p == 2:                                      # CASA DE LABOR (12 × 8, dos plantas) CON CIPRESES
        W, D, PL = 10.0, 8.0, 3.4
        alto = 2 * PL
        roto = v == 3
        t.caja((0, 0, alto / 2), (W, D, alto), "cal")
        t.caja((0, -D / 2 - 0.02, 0.45), (W - 0.02, 0.06, 0.9), "piedra")
        if roto:
            t.tejado((-2.0, 0, alto), W / 2 - 1.0, D, 2.4, "teja", vuelo=0.3, aguas=2)
            for k in range(4):
                t.caja((2.5 + k * 0.6, 0.2 * k - 0.5, alto + 0.1), (g(0.2), D - 0.6, g(0.2)), "carpinteria",
                       rot_x=0.2 * (k % 2))
        else:
            t.tejado((0, 0, alto), W - 0.4, D, 2.6, "teja", vuelo=0.3, aguas=2)
        yf = -D / 2 - 0.03
        for x in (-3.4, -1.2, 1.2, 3.4):              # huecos de la planta alta, balcón central
            t.caja((x, yf, PL + 1.4), (0.9, 0.06, 1.5), "carpinteria" if not roto else "hueco")
        if fino:
            t.caja((0, -D / 2 - 0.3, PL + 0.05), (1.8, 0.6, 0.12), "piedra")
        t.caja((0, yf, 1.4), (1.8, 0.06, 2.8), "carpinteria")                                  # portalón
        for x in (-3.4, 3.4):
            t.caja((x, yf, 1.5), (0.9, 0.06, 1.2), "carpinteria" if not roto else "hueco")
        if v == 2:                                    # porche con emparrado
            for x in (-4.2, -1.5, 1.5, 4.2):
                t.caja((x, -D / 2 - 2.6, 1.3), (g(0.15), g(0.15), 2.6), "piedra")
            t.caja((0, -D / 2 - 1.5, 2.65), (9.4, 3.0, 0.15), "copa")
        n = 4 if v == 4 else 2
        for k in range(n):                            # cipreses junto a la casa
            cipres(W / 2 + 1.5, -D / 2 + 1.0 + k * (6.0 / max(1, n - 1)), 8.0 - 0.6 * (k % 2))

    elif p == 3:                                      # BALSA DE RIEGO (Ø 14)
        R = 7.0
        rota = v == 4
        lados = 16 if fino else 10
        t.cilindro((0, 0, 0.6), R + 1.2, 1.2, "Z", lados, "tierra", r2=R + 0.6)               # talud de tierra
        t.cilindro((0, 0, 1.23), R + 0.6, 0.04, "Z", lados, "lamina")                        # corona con lámina
        # el talud es macizo: el nivel se lee por el ancho de lámina oscura que queda a la vista alrededor del agua
        t.cilindro((0, 0, 1.26), R, 0.04, "Z", lados, "lamina")                               # vaso (lámina)
        ragua = {0: R - 0.3, 1: R - 1.6, 2: None, 3: R - 0.4, 4: R - 2.6}[v]
        if ragua is None:
            t.cilindro((1.5, -1.0, 1.29), 2.0, 0.03, "Z", 10, "agua")                         # charco en el fondo
        else:
            t.cilindro((0, 0, 1.3), ragua, 0.04, "Z", lados, "agua")
        if v == 3:                                    # vallada
            for k in range(12 if fino else 8):
                a = k * 2 * math.pi / (12 if fino else 8)
                t.caja(((R + 1.4) * math.cos(a), (R + 1.4) * math.sin(a), 0.75), (g(0.08), g(0.08), 1.5), "valla")
            if fino:
                t.cilindro((0, 0, 1.4), R + 1.45, 0.04, "Z", lados, "valla")
        if rota:                                      # talud caído hacia +X
            t.caja((R + 1.0, 0, 0.3), (3.0, 3.5, 0.6), "tierra", rot_z=0.2)
            t.caja((R + 2.4, 0.4, 0.04), (3.0, 4.0, 0.05), "agua", rot_z=0.3)
        if fino:
            t.cilindro((-R - 0.8, 0, 1.3), 0.15, 0.3, "Z", 8, "piedra")                       # boca de riego

    else:                                             # PALOTS GENÉRICOS
        def palot(x, y, z=0.0, rz=0.0, pieza="palot", fruta=False, rx=0.0):
            t.caja((x, y, z + 0.375), (1.2, 1.0, 0.75), pieza, rot_z=rz, rot_x=rx)
            if fino and not rx:
                for sx in (-0.3, 0.3):              # rejillas de ventilación
                    t.caja((x + sx, y - 0.51, z + 0.45), (0.4, 0.03, 0.4), "palot2" if pieza == "palot" else "palot",
                           rot_z=rz)
            if fruta:
                t.caja((x, y, z + 0.76), (1.05, 0.85, 0.04), "fruta", rot_z=rz)
        if v == 0:
            palot(0, 0); palot(0, 0, 0.76, pieza="palot2")
        elif v == 1:
            for i in range(3):
                for j in range(2):
                    for f in range(3):
                        palot(-1.25 + i * 1.25, -0.52 + j * 1.04, f * 0.76, pieza=("palot", "palot2")[(i + j + f) % 2])
        elif v == 2:
            for i in range(5):
                palot(-2.5 + i * 1.25, 0, pieza=("palot", "palot2")[i % 2])
        elif v == 3:
            palot(-0.8, 0, rz=0.3)
            palot(0.8, 0.2, rz=-0.4, rx=1.57)
            palot(0.2, 1.3, rz=1.2, pieza="palot2")
        else:
            for i in range(3):
                palot(-1.25 + i * 1.25, 0, fruta=True, pieza=("palot", "palot2")[i % 2])
            palot(0, 0, 0.76, fruta=True)
    return t.objeto(nombre, bisel=0)
