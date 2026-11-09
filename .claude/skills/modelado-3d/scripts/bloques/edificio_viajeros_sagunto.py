"""Edificio de viajeros de Sagunt, como el real (Álvaro, 8-nov 18:07 y 18:12, sagunto-nivel.md §10.15; Lucía 18:15;
Paula, sagunto_azotea.md y fotos SG1/SG2: OSM 419847974). Sustituye al sólido edificio_viajeros. Teo (3), 8-nov.

32 × 9 m: cuerpo central de 2 plantas (14 m, alero a 9,5 m) con teja a cuatro aguas a86c50 y un frontón con óculo en
cada fachada larga (SG1: el frontón asoma por encima del tejado); dos alas de 1 planta (9 × 9 m) con AZOTEA plana
transitable a 4,75 m y balaustrada de 1 m de balaustres con jarrones en las esquinas (D-N174: ahí suben los tiradores).
Paredes crema f0dc9c (la de la foto) con pilastras, zócalo, imposta y recercados granates 642030 (8c5048 en el LOD1;
nunca el 982420), balaustrada b8b8b8 (no d4d0cc, Lucía), suelo de azotea 94948c, carpintería 543820. Sin rótulos.
Desde la primera planta del cuerpo central sale una puerta a cada azotea (SIN VERIFICAR: no hay foto del acceso). Sin
escalera exterior: se sube por dentro.
Origen: centro de la planta, en el suelo. Fachada del andén en −Y (y = −4,5) y la de la calle en +Y. En la zona va en
(99; 0; 76,3) con yaw 180, para que la fachada del andén mire al sur (−Z de Unity), pegada al andén (Z 71,8).
Alas en x −16..−7 y 7..16. Con yaw 180 la x local se invierte: el ala de x +7..+16 queda al OESTE (X 83–92 de la zona)
y la de −16..−7 al este (si el eje X de la exportación sale espejado, al revés: comprobarlo con la primera).
Variantes (TBD_VARIANTE % 5) = estados de la D-N124: 0 Abandonada (huecos tapiados, escombro en las azoteas, dos
balaustres caídos); 1 Segura (huecos abiertos; sacos en la azotea oeste, donde van los tiradores; solo a la vista);
2 EnLiberacion (tapiado a medias, escombro); 3 Caida (todo tapiado, un tramo de balaustrada caído, tejas arrancadas y
hollín); 4 Liberada (andamio en el cuerpo central y tablones nuevos). Medidas SIN VERIFICAR (Nacho va a medir).
"""
import math, os
from taller import Taller
from kit_comun import hueco

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Abandonada", "Segura", "EnLiberacion", "Caida", "Liberada"]
PIEZAS = ["fachada", "moldura", "carpinteria", "tejado", "balaustrada", "azotea", "tabla", "sacos", "escombro",
          "hollin", "andamio", "metal"]
COLORES = {"fachada": "f0dc9c", "moldura": "7c181c", "carpinteria": "6c4038", "tejado": "a86c50",
           "balaustrada": "b8b8b8", "azotea": "94948c", "tabla": "845c28", "sacos": "605c48", "escombro": "88885c",
           "hollin": "605c48", "andamio": "6c7874", "metal": "6c7874"}
COLORES_LOD1 = {"moldura": "8c5048", "carpinteria": "6c4038"}   # escalón de LOD1 aprobado (Sagunt, D-N123)
PERMITIR = ("7c181c", "8c5048")   # 7c181c (Lucía 18:15): el 642030 a la sombra caía en la banda en el LOD0
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (100, 9000), 1: (50, 4000)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
LA, FY, XC = 9.0, 9.0, 7.0              # ala, fondo, medio largo del cuerpo central
HA, HC = 4.75, 9.5                       # suelo de la azotea y alero del central
Y0, Y1 = -FY / 2, FY / 2


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_EdificioViajerosSagunto", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    v = V
    tapiar = {0: set(range(30)) - {3, 7}, 1: set(), 2: {0, 2, 4, 9, 11, 13, 16}, 3: set(range(30)), 4: {3}}[v]
    n = [0]

    def hue(x, y, z, an, al, cara):
        k = n[0]
        n[0] += 1
        hueco(t, fino, lod, x, y, z, an, al, cara=cara, marco="moldura",
              pieza="tabla" if k in tapiar else "carpinteria")

    # ---------------------------------------------------------------- volúmenes
    t.caja((0, 0, HC / 2), (2 * XC, FY, HC), "fachada")                              # cuerpo central
    for s in (-1, 1):                                                                 # alas (se meten 0,1 en el central)
        xa = s * (XC + LA / 2 - 0.05)
        t.caja((xa, 0, HA / 2 - 0.01), (LA + 0.1, FY - 0.02, HA - 0.02), "fachada")
        t.caja((xa + s * 0.0, 0, HA + 0.015), (LA - 0.5, FY - 0.5, 0.03), "azotea")   # suelo de la azotea
        t.caja((xa, 0, HA - 0.25), (LA + 0.18, FY + 0.16, 0.3), "moldura")             # cornisa granate
        t.caja((xa, 0, 0.35), (LA + 0.14, FY + 0.12, 0.7), "moldura")                  # zócalo
    t.caja((0, 0, 0.35), (2 * XC + 0.12, FY + 0.12, 0.7), "moldura")
    t.caja((0, 0, HA - 0.1), (2 * XC + 0.1, FY + 0.1, 0.25), "moldura")                # imposta
    t.caja((0, 0, HC - 0.2), (2 * XC + 0.2, FY + 0.2, 0.4), "moldura")                 # cornisa
    # pilastras granates en las esquinas del central y de las alas
    for x in (-XC - 0.05, XC + 0.05, -(XC + LA) + 0.25, (XC + LA) - 0.25):
        alto = HC if abs(x) < XC + 1 else HA
        for y in (Y0 - 0.04, Y1 + 0.04):
            t.caja((x, y, alto / 2), (0.55, 0.12, alto), "moldura")
    # ---------------------------------------------------------------- huecos
    for cara, y in ((-1, Y0), (1, Y1)):
        for x in (-4.4, 0.0, 4.4):                                                    # planta baja del central
            hue(x, y, 1.75, 1.6 if x == 0 else 1.2, 2.6, cara)
        for x in (-4.8, -1.6, 1.6, 4.8):                                              # primera planta
            hue(x, y, 6.9, 1.0, 1.9, cara)
        for s in (-1, 1):                                                             # alas: puerta y ventanas
            for dx in (-2.6, 0.0, 2.6):
                hue(s * (XC + LA / 2) + dx, y, 1.8 if dx else 1.65, 1.1 if dx else 1.5, 2.2 if dx else 2.5, cara)
    for s in (-1, 1):                                                                 # testeros de las alas
        for dy in (-2.0, 2.0):
            t.caja((s * (XC + LA + 0.03), dy, 1.8), (0.06, 1.1, 2.2), "tabla" if v in (0, 3) else "carpinteria")
            t.caja((s * (XC + LA + 0.01), dy, 1.8), (0.04, 1.6, 2.7), "moldura")
        # puerta de la primera planta del central a cada azotea
        t.caja((s * (XC + 0.03), 0.0, HA + 1.15), (0.06, 1.2, 2.2), "tabla" if v == 3 else "carpinteria")
        t.caja((s * (XC + 0.01), 0.0, HA + 1.2), (0.04, 1.7, 2.6), "moldura")
    # ---------------------------------------------------------------- tejado a cuatro aguas y frontones
    if v == 3:                                       # tejas arrancadas: un faldón abierto con los cabios
        t.tejado((0, 0, HC), 2 * XC, FY, 1.3, "tejado", vuelo=0.4)
        t.caja((3.0, 2.2, HC + 0.75), (3.0, 2.4, 0.04), "hollin", rot_x=math.atan2(1.3, 4.9))
        for k in range(4):
            t.caja((1.8 + k * 0.8, 2.2, HC + 0.8), (0.1, 2.6, 0.1), "carpinteria", rot_x=math.atan2(1.3, 4.9))
    else:
        t.tejado((0, 0, HC), 2 * XC, FY, 1.3, "tejado", vuelo=0.4)
    for s in (-1, 1):                                                                 # frontones con óculo
        yf = s * (FY / 2 + 0.05)
        hp = 1.9
        P = [(yf - 0.15, HC), (yf - 0.15, HC + hp), (yf + 0.15, HC + hp), (yf + 0.15, HC)] if s > 0 else \
            [(yf - 0.15, HC), (yf - 0.15, HC + hp), (yf + 0.15, HC + hp), (yf + 0.15, HC)]
        t.perfil_extruido(P, lambda z, hp=hp: max(0.08, 5.4 * (1 - (z - HC) / hp)), pieza="moldura")
        P2 = [(yf + s * 0.05 - 0.12, HC + 0.1), (yf + s * 0.05 - 0.12, HC + hp - 0.35),
              (yf + s * 0.05 + 0.12, HC + hp - 0.35), (yf + s * 0.05 + 0.12, HC + 0.1)]
        t.perfil_extruido(P2, lambda z, hp=hp: max(0.08, 4.7 * (1 - (z - HC - 0.1) / (hp - 0.45))), pieza="fachada")
        t.cilindro((0, yf + s * 0.2, HC + 0.75), 0.35, 0.1, "Y", 12 if fino else 8, "moldura")   # óculo
    # ---------------------------------------------------------------- balaustradas de las azoteas
    for s in (-1, 1):
        x0, x1 = (XC, XC + LA) if s > 0 else (-XC - LA, -XC)
        lados = [((x0, Y0 + 0.2), (x1 - 0.2 if s > 0 else x1, Y0 + 0.2)),
                 ((x0, Y1 - 0.2), (x1 - 0.2 if s > 0 else x1, Y1 - 0.2))]
        xe = x1 - 0.2 if s > 0 else x0 + 0.2
        lados.append(((xe, Y0 + 0.2), (xe, Y1 - 0.2)))
        if s < 0:
            lados[0] = ((x0 + 0.2, Y0 + 0.2), (x1, Y0 + 0.2))
            lados[1] = ((x0 + 0.2, Y1 - 0.2), (x1, Y1 - 0.2))
        for i, ((ax, ay), (bx, by)) in enumerate(lados):
            caido = (v == 3 and s > 0 and i == 2)
            L = math.hypot(bx - ax, by - ay)
            cx, cy = (ax + bx) / 2, (ay + by) / 2
            rz = math.atan2(by - ay, bx - ax)
            if caido:                                # tramo caído: trozos al pie del testero, fuera
                for k in range(3):
                    t.caja((xe + 1.4 + _h(k) * 1.2, -3 + k * 2.6, 0.25), (0.4, 2.0, 0.5), "balaustrada",
                           rot_z=_h(k, 1) * 1.5)
                continue
            t.caja((cx, cy, HA + 0.1), (L + 0.3, 0.4, 0.2), "balaustrada", rot_z=rz)          # pedestal
            t.caja((cx, cy, HA + 1.0), (L + 0.3, 0.4, 0.14), "balaustrada", rot_z=rz)         # pasamanos
            if fino:
                nb = int(L / 0.3)
                for k in range(nb):
                    if v == 0 and s < 0 and i == 0 and k in (6, 7):
                        continue
                    u = (k + 0.5) / nb - 0.5
                    t.caja((cx + math.cos(rz) * u * L, cy + math.sin(rz) * u * L, HA + 0.55),
                           (0.14, 0.14, 0.72), "balaustrada")
            else:                                    # LOD1: paño calado en dos bandas (se lee como balaustrada)
                t.caja((cx, cy, HA + 0.55), (L, 0.2, 0.72), "balaustrada", rot_z=rz)
        for (px, py) in ((xe, Y0 + 0.2), (xe, Y1 - 0.2)):                               # jarrones en las esquinas
            t.caja((px, py, HA + 0.55), (0.45, 0.45, 1.1), "balaustrada")
            t.cilindro((px, py, HA + 1.35), 0.2, 0.5, "Z", 8 if fino else 6, "balaustrada", r2=0.1)
    _estado(t, fino, v)
    return t.objeto(nombre, bisel=0)


def _estado(t, fino, v):
    z = HA + 0.03
    xo = XC + LA / 2                                  # centro del ala de +x (la oeste con yaw 180)
    if v in (0, 2, 3):                                # escombro en las azoteas y al pie
        for k in range(4 if fino else 2):
            s = -1 if k % 2 else 1
            t.caja((s * (xo - 1.5 + _h(k, 2) * 3), -2 + _h(k, 3) * 4, z + 0.15), (0.9, 0.6, 0.3), "escombro",
                   rot_z=_h(k, 4) * 3)
        t.caja((-3.0, Y0 - 1.2, 0.3), (2.4, 1.2, 0.6), "escombro", rot_z=0.3)
    if v == 1:                                        # sacos sobre la balaustrada oeste (+x local), donde tiran
        xe = XC + LA - 0.2
        for k in range(7):
            t.caja((xe, -3.3 + k * 1.1, HA + 1.07 + 0.14), (0.5, 1.0, 0.27), "sacos", rot_z=(_h(k, 5) - 0.5) * 0.1)
        for k in range(6):                            # y una hilada en la esquina del andén
            t.caja((XC + 1.2 + k * 1.1, Y0 + 0.2, HA + 1.07 + 0.14), (1.0, 0.5, 0.27), "sacos")
    if v == 3:                                        # hollín en la fachada del andén
        t.caja((2.0, Y0 - 0.08, 6.0), (6.0, 0.03, 3.0), "hollin")
        t.caja((-11.0, Y0 - 0.08, 3.0), (4.0, 0.03, 2.0), "hollin")
    if v == 4:                                        # andamio delante del central, en la fachada del andén
        g = 0.08 if fino else 0.34
        for x in (-6.0, -2.0, 2.0, 6.0):
            for yy in (Y0 - 0.5, Y0 - 1.5):
                t.caja((x, yy, 4.6), (g, g, 9.2), "andamio")
        for zz in (2.4, 4.8, 7.2):
            t.caja((0, Y0 - 1.0, zz), (12.4, 1.2, 0.06 if fino else 0.17), "tabla")
