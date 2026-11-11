"""Casa en ruinas para el caserío del Hito 1 (núcleo v2; encargo de Lucía del 11-11, docs/hito1-reparto.md). Teo (4), 11-11.

Casa de huerta valenciana de muros de carga (foto E4 de Paula, casa encalada), abandonada y en ruinas: muros con el remate
roto, sin carpinterías, tejado hundido y escombro dentro. Es lo que los zombis ocupan y el pelotón limpia; dentro queda sitio
para el depósito de material que deja el caserío limpio (lo pone el juego).
Planta de 9 × 7 m (x −4,5..4,5, y −3,5..3,5), muros de 0,45 m, una planta de 3,4 m (dos plantas en la v1, 6,4 m). Fachada a −Y
con la puerta y dos ventanas. Origen en el suelo, en el centro de la planta. MEDIDAS DE MEMORIA (SIN VERIFICAR).
Colores: enlucido b4a080 (la cal d4d0cc está reservada para la selección), mampostería a la vista a86c50, teja a86c50 con el
canto 7c3c18, vigas 845c28, hollín 48403c, escombro 94948c y 88885c, maleza 70784c y 847c34. Nada en la banda, nada rojo ni ámbar.
Variantes: 0 sin tejado (muros enteros con el remate roto); 1 dos plantas con la esquina del este caída; 2 tejado hundido hacia
dentro; 3 quemada (hollín, vigas carbonizadas); 4 casi arrasada (muros bajos y un gran montón de escombro).
TBD_VARIANTE % 5.
"""
import math, os
from taller import Taller
from restos import _h

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["SinTejado", "DosPlantas", "TejadoHundido", "Quemada", "Arrasada"]
PIEZAS = ["enlucido", "piedra", "teja", "canto", "viga", "hollin", "escombro", "escombro_c", "maleza", "maleza_c"]
COLORES = {"enlucido": "b4a080", "piedra": "a86c50", "teja": "a86c50", "canto": "7c3c18", "viga": "845c28",
           "hollin": "48403c", "escombro": "94948c", "escombro_c": "88885c", "maleza": "70784c", "maleza_c": "847c34"}
FACCION = "neutro"
NORMALES_ARRIBA = ["escombro", "escombro_c", "maleza", "maleza_c"]
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (40, 4000), 1: (20, 1600)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
LX, LY, G = 9.0, 7.0, 0.45
H = 6.4 if V == 1 else 3.4
COL = 0.5


def _alto(lado, s):
    """Altura del muro en el lado (0 sur, 1 este, 2 norte, 3 oeste) a la distancia s de su esquina de inicio."""
    r = _h((lado, round(s, 2), V), 3)
    base = H
    if V == 4:
        base = 0.8 + 1.4 * _h((lado, int(s), 1), 4)
        return base + 0.3 * r
    if V == 1 and ((lado == 0 and s > 6.0) or (lado == 1 and s < 3.0)):       # esquina SE caída
        d = (s - 6.0) if lado == 0 else (3.0 - s)
        return max(0.6, H - 2.2 * d - 1.2) + 0.3 * r
    rem = 0.5 if V != 2 else 0.25                                             # remate roto: dientes
    return base - rem * r - (0.6 if (lado + int(s / 2.5)) % 3 == 0 else 0.0)


def _huecos(lado, s):
    """None, 'puerta' o 'ventana' (también la de la planta alta en la v1)."""
    if lado == 0:
        if 3.9 < s < 5.1:
            return "puerta"
        if 1.4 < s < 2.6 or 6.4 < s < 7.6:
            return "ventana"
    if lado == 2 and 3.9 < s < 5.1:
        return "ventana"
    if lado in (1, 3) and 2.9 < s < 4.1:
        return "ventana"
    return None


def _muro(t, fino, lado):
    largo = LX if lado in (0, 2) else LY - 2 * G
    n = int(round(largo / COL))
    paso = largo / n
    for k in range(n):
        s = (k + 0.5) * paso
        h = _alto(lado, s)
        hue = _huecos(lado, s)
        tramos = [(0.0, h)]
        if hue == "puerta":
            tramos = [(2.3, h)] if h > 2.5 else []
        elif hue == "ventana":
            tramos = [(0.0, 1.0)] + ([(2.2, h)] if h > 2.4 else [])
            if V == 1 and h > 5.0:                                               # ventana de la planta alta
                tramos = [(0.0, 1.0), (2.2, 4.0), (5.2, h)]
        for z0, z1 in tramos:
            if z1 - z0 < 0.05:
                continue
            if lado == 0:
                c, sz = (-LX / 2 + s, -LY / 2 + G / 2), (paso - 0.004, G)
            elif lado == 2:
                c, sz = (LX / 2 - s, LY / 2 - G / 2), (paso - 0.004, G)
            elif lado == 1:
                c, sz = (LX / 2 - G / 2, -LY / 2 + G + s), (G, paso - 0.004)
            else:
                c, sz = (-LX / 2 + G / 2, LY / 2 - G - s), (G, paso - 0.004)
            pieza = "hollin" if (V == 3 and z1 > 1.6 and _h((lado, k, 5), 6) < 0.6) else "enlucido"
            if V != 3 and _h((lado, k, z0, 7), 8) < 0.22:                       # enlucido caído: mampostería a la vista
                pieza = "piedra"
            t.caja((c[0], c[1], (z0 + z1) / 2), (sz[0], sz[1], z1 - z0), pieza)
    if V == 1 and fino:                                                          # forjado de la planta alta, roto
        pass


def _escombro(t, fino, x, y, r, alto, n=None):
    n = n or (6 if fino else 3)
    for k in range(n):
        a = k * 2.4 + _h((x, y, k), 9) * 2
        d = r * 0.6 * _h((k, x, 10), 11)
        t.caja((x + math.cos(a) * d, y + math.sin(a) * d, alto * 0.35), (r * 0.9, r * 0.7, alto * 0.7),
               "escombro" if k % 2 else "escombro_c", rot_z=a, rot_x=(_h((k, 12, 0), 13) - .5) * .3)
    t.caja((x, y, alto * 0.75), (r * 0.8, r * 0.6, alto * 0.5), "escombro_c", rot_z=0.5)


def construir(nombre="SM_CasaRuina", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    for lado in range(4):
        _muro(t, fino, lado)
    # tejado: lo que queda de él
    if V == 2:                                                                   # faldón hundido hacia dentro
        t.caja((0.3, -0.8, 1.9), (LX - 1.6, 4.2, 0.18), "teja", rot_x=0.55, rot_y=0.08)
        t.caja((0.3, 1.0, H + 0.2), (LX - 0.4, 2.2, 0.18), "teja", rot_x=-0.45)
        if fino:
            for k in range(3):
                t.caja((-3 + 3 * k, 0.4, 2.6), (0.18, 5.6, 0.22), "viga", rot_x=0.35 + 0.1 * k)
    elif V in (0, 3):                                                            # vigas sueltas y algo de teja en una esquina
        for k in range(4 if fino else 2):
            x = -3.4 + (6.8 / (3 if fino else 1)) * k
            if _h((k, 14, V), 15) < 0.35:
                continue
            t.caja((x, 0, H - 0.25), (0.2, LY - 0.5, 0.24), "hollin" if V == 3 else "viga")
        if V == 0:
            t.caja((-3.2, 2.2, H + 0.25), (2.4, 2.4, 0.16), "teja", rot_x=-0.4)
            t.caja((-3.2, 1.05, H - 0.15), (2.4, 0.1, 0.12), "canto", rot_x=-0.4)
        else:
            for k in range(3 if fino else 2):
                t.caja((-2.0 + 2.0 * k, -0.5 + 0.8 * k, 0.25), (2.8, 0.2, 0.24), "hollin", rot_z=0.7 * k, rot_y=0.25)
    elif V == 1:                                                                 # forjado partido y tejado colgando
        t.caja((-1.6, 0, 3.3), (LX - 3.6, LY - 1.0, 0.25), "escombro_c")
        t.caja((2.6, -0.4, 1.9), (2.6, LY - 1.4, 0.25), "escombro_c", rot_y=-0.55)
        t.caja((-1.8, 0.8, H + 0.3), (LX - 4.2, 3.6, 0.18), "teja", rot_x=-0.38)
        t.caja((-1.8, -0.9, H - 0.4), (LX - 4.2, 0.12, 0.14), "canto", rot_x=-0.38)
    # escombro dentro y fuera
    if V == 4:
        _escombro(t, fino, 0.4, 0.2, 3.2, 1.6, 9 if fino else 4)
        _escombro(t, fino, 3.2, -4.4, 1.6, 0.6)
        for k in range(3 if fino else 2):
            t.caja((-2 + 2.2 * k, -2.2 + 1.5 * k, 1.3), (2.2, 0.2, 0.2), "viga", rot_z=1.0 * k, rot_y=0.3)
    else:
        _escombro(t, fino, -1.5, 0.6, 2.0, 0.9)
        if V == 1:
            _escombro(t, fino, 4.6, -3.8, 2.4, 1.2)
        if V == 3:
            _escombro(t, fino, 1.8, -0.6, 1.6, 0.7)
    _escombro(t, fino, 0.0, -4.4, 1.0, 0.35, 3 if fino else 2)                   # lo que salió por la puerta
    # maleza contra los muros
    for k in range(5 if fino else 3):
        a = k * 1.3
        x, y = math.cos(a) * 5.2, math.sin(a) * 4.2
        tam = 0.8 + 0.6 * _h((k, 16, V), 17)
        t.caja((x, y, 0.22), (tam, tam * 0.8, 0.44), "maleza" if k % 2 else "maleza_c", rot_z=a)
    return t.objeto(nombre, bisel=0)


def separar(o, lod):
    """El origen queda en el centro de la planta (no en el de la caja, que mueve el escombro de fuera)."""
    from mathutils import Vector, Matrix
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    o.data.transform(Matrix.Translation(base))
    if "tbd_base" in o:
        del o["tbd_base"]
    return [o]
