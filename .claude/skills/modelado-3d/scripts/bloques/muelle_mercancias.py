"""Muelle de mercancías con almacén ferroviario (ambientación de Sagunto, pieza 7: «aquí se carga el tren»). Teo, 6-nov-2026.

Fotos E7 de Paula (Commons): Sardón de Duero (almacén de ladrillo con alero sobre muelle de piedra), Santa Cruz de la
Zarza (almacén de mampostería con ménsulas bajo el alero, muelle con escalones) y Benavente (almacén largo con alero
corrido sobre el muelle, abandonado). Son de la meseta, no de Sagunto: sirven por el tipo, que es el mismo en toda la red.
Lo que se toma de las fotos: muelle de piedra a la altura del piso del vagón, nave de muros de mampostería con
esquinas y recercados de sillar, ventanas altas de medio punto, portones de madera correderos al lado de la vía,
alero largo sobre el muelle sostenido por ménsulas de hierro y tejado de teja a dos aguas.
MEDIDAS SIN VERIFICAR (típicas, estimadas sobre las fotos): nave de 18 × 8 m, alero a 4,6 m y cumbrera a 6,4 m;
muelle a 1,1 m del suelo que sobresale 3,5 m hacia la vía, con rampa de 5 m en +X; portones de 2,6 × 3 m.
Colores: mampostería d4d0cc, sillares f0dc9c, muelle 88885c con el borde en 94948c (Lucía: nada claro en bordes
largos, que se confunde con la selección), teja a86c50, portones 543820,
huecos y ménsulas 48403c, cajas 845c28 y sacos b4a080 (sin rojos ni valores de la horda).
Orientación: la vía va a lo largo de X y pasa por −Y (+Z en Unity); el borde del muelle queda en y = −7,5.
Colisión sugerida: el muelle es una caja de 1,1 m (la tropa sube por la rampa) y la nave una caja hasta el alero.
Variantes: 0 la de la foto, cerrada; 1 portones abiertos con cajas y sacos en el muelle; 2 un trozo de tejado
hundido; 3 alero caído sobre el muelle y portones tapiados con tablas; 4 enfoscada en ocre con carretilla y palés;
5 quemada, sin tejado, interior 48403c y las vigas a la vista en 605c48.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["muro", "sillar", "muelle", "borde", "tejado", "porton", "oscuro", "madera", "saco", "viga"]
_MURO = ("d4d0cc", "d4d0cc", "d4d0cc", "d4d0cc", "b4a080", "d4d0cc")
COLORES = {"muro": _MURO[VAR % 6], "sillar": "f0dc9c", "muelle": "88885c", "borde": "94948c", "tejado": "a86c50",
           "porton": "543820", "oscuro": "48403c", "madera": "845c28", "saco": "b4a080", "viga": "605c48"}
FACCION = "neutro"
PRESUPUESTO = {0: (80, 1800), 1: (40, 800)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}   # contorno solo en la silueta: los huecos oscuros no se llenan de negro
L, W, ALERO, CUMBRE = 18.0, 8.0, 4.6, 1.8
HM, VM = 1.1, 3.5            # altura del muelle y lo que sobresale hacia la vía (−Y)


def construir(nombre="SM_MuelleMercancias", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    v = VAR % 6
    hx, hy = L / 2, W / 2
    yb = -hy - VM                                         # borde del muelle, del lado de la vía
    # muelle de piedra: plataforma con el borde (albardilla) claro y rampa en +X
    y0, y1 = yb, hy + 0.6
    t.caja((0, (y0 + y1) / 2, HM / 2), (L + 2.0, y1 - y0, HM), "muelle")
    t.caja((0, yb + 0.28, HM + 0.02), (L + 2.1, 0.6, 0.1), "borde")
    t.caja((hx + 1.0 + 2.4, (y0 + y1) / 2, HM / 2), (y1 - y0, 5.0, HM), "muelle", rot_z=math.pi / 2, inclinar=4.85)
    # nave sobre el muelle
    z0 = HM
    hm = ALERO - z0
    t.caja((0, 0, z0 + hm / 2), (L, W, hm), "muro")
    for sx in (-1, 1):                                    # esquinas de sillar
        for sy in (-1, 1):
            t.caja((sx * (hx - 0.3), sy * (hy - 0.3), z0 + hm / 2), (0.7, 0.7, hm + 0.02), "sillar")
    t.caja((0, 0, ALERO - 0.15), (L + 0.1, W + 0.1, 0.3), "sillar")      # cornisa
    _tejado(t, v, hx, hy)
    # portones del lado de la vía (tres) y uno en la trasera
    xs = (-6.0, 0.0, 6.0)
    for i, x in enumerate(xs):
        abierto = v == 1 or (v == 5 and i != 1)
        _porton(t, fino, x, -hy, z0, abierto, tapiado=(v == 3 and i != 1), lod=lod)
    _porton(t, fino, 0.0, hy, z0, False, lod=lod, dy=1)
    # ventanas altas de medio punto entre portones (lados largos) y en los hastiales
    for x in (-3.0, 3.0, -7.8 + 0.0, 7.8):
        if abs(x) > 7.5:
            continue
        for sy in (-1, 1):
            _ventana(t, fino, x, sy * hy, z0 + 2.4, sy)
    for sx in (-1, 1):
        t.caja((sx * (hx + 0.03), 0, z0 + 2.6), (0.06, 1.0, 1.3), "oscuro")
        fino.caja((sx * (hx + 0.05), 0, z0 + 3.3), (0.06, 1.3, 0.25), "sillar")
    _alero(t, fino, v, hx, hy, lod)
    _carga(t, fino, v, hx, hy, yb)
    return t.objeto(nombre, bisel=0)


def _tejado(t, v, hx, hy):
    if v == 5:                                            # quemada: sin teja, interior oscuro y vigas a la vista
        t.caja((0, 0, ALERO + 0.02), (2 * hx - 0.6, 2 * hy - 0.6, 0.06), "oscuro")
        for x in range(-8, 9, 4):
            t.caja((x, 0, ALERO + 0.5), (0.3, 2 * hy, 0.3), "viga")
        t.caja((0, 0, ALERO + CUMBRE - 0.2), (2 * hx, 0.35, 0.35), "viga")
        return
    t.tejado((0, 0, ALERO), 2 * hx, 2 * hy, CUMBRE, "tejado", vuelo=0.4, aguas=2)
    if v == 2:                                            # trozo hundido en el faldón trasero: parche oscuro
        a = math.atan2(CUMBRE, hy + 0.4)
        t.caja((3.5, hy / 2 + 0.2, ALERO + CUMBRE / 2 + 0.05), (5.0, hy / math.cos(a) * 0.8, 0.1), "oscuro", rot_x=a)


def _porton(t, fino, x, y, z0, abierto, tapiado=False, lod=0, dy=-1):
    """Portón corredero de madera de 2,6 × 3 m con recercado de sillar. dy: hacia dónde mira la fachada."""
    yy = y + dy * 0.04
    t.caja((x, yy, z0 + 1.55), (3.1, 0.08, 3.4), "sillar")               # recercado
    if abierto:
        t.caja((x, yy + dy * 0.02, z0 + 1.5), (2.6, 0.08, 3.0), "oscuro")
        t.caja((x + 2.8, yy + dy * 0.12, z0 + 1.5), (2.6, 0.12, 3.0), "porton")   # hoja corrida a un lado
    elif tapiado:
        t.caja((x, yy + dy * 0.02, z0 + 1.5), (2.6, 0.08, 3.0), "porton")
        for k, z in enumerate((0.6, 1.5, 2.4)):
            t.caja((x, yy + dy * 0.1, z0 + z), (3.0, 0.08, 0.35), "madera", rot_y=(0.12 if k % 2 else -0.1))
    else:
        t.caja((x, yy + dy * 0.02, z0 + 1.5), (2.6, 0.08, 3.0), "porton")
        fino.caja((x, yy + dy * 0.07, z0 + 1.5), (0.06, 0.04, 3.0), "oscuro")     # junta de las dos hojas
    fino.caja((x, yy + dy * 0.08, z0 + 3.12), (3.2, 0.06, 0.1), "oscuro")        # carril


def _ventana(t, fino, x, y, z, sy):
    """Ventana alta de medio punto: hueco oscuro con arco de sillar (el arco a escalones)."""
    t.caja((x, y + sy * 0.03, z), (1.0, 0.06, 1.3), "oscuro")
    t.caja((x, y + sy * 0.05, z + 0.75), (1.3, 0.06, 0.2), "sillar")
    fino.caja((x, y + sy * 0.05, z - 0.72), (1.2, 0.08, 0.12), "sillar")         # vierteaguas


def _alero(t, fino, v, hx, hy, lod):
    """Alero largo sobre el muelle con ménsulas de hierro (Santa Cruz de la Zarza)."""
    vuelo = 2.6
    yc = -hy - vuelo / 2
    if v == 3:                                            # caído: tumbado sobre el muelle, inclinado
        t.caja((1.0, -hy - 2.0, HM + 0.7), (L - 3.0, vuelo, 0.14), "tejado", rot_x=0.45)
        t.caja((-hx + 0.8, -hy - 1.2, HM + 0.8), (1.5, vuelo, 0.14), "tejado", rot_x=-0.15)
        for x in (-6.0, 0.0, 6.0):
            t.caja((x + 0.5, -hy - 2.4, HM + 0.15), (0.2, 1.6, 0.2), "oscuro", rot_z=0.4)
        return
    za = ALERO + 0.1
    t.caja((0, yc, za), (L + 0.8, vuelo, 0.14), "tejado", rot_x=0.12)
    g = 0.12 if lod == 0 else 0.3
    for x in range(-8, 9, 4):                             # ménsulas: tornapunta diagonal y tirante
        t.caja((x, -hy - 0.75, za - 0.9), (g, 1.9, g), "oscuro", rot_x=-0.75)
        fino.caja((x, -hy - 1.2, za - 0.12), (g, 2.3, g), "oscuro")


def _carga(t, fino, v, hx, hy, yb):
    zt = HM
    if v == 1:                                            # cajas de fruta y sacos en el muelle
        for i, (x, y, n) in enumerate(((-4.6, -5.6, 3), (-3.4, -6.3, 2), (2.5, -5.4, 2), (3.6, -6.4, 1))):
            for k in range(n):
                t.caja((x, y, zt + 0.25 + k * 0.5), (0.9, 0.6, 0.46), "madera", rot_z=0.1 * i)
        for x in (7.6, 8.2, 7.9):
            t.caja((x, -5.8 + (x - 7.9), zt + 0.2), (0.8, 0.5, 0.36), "saco")
    if v == 4:                                            # carretilla de dos ruedas y palés
        t.caja((-6.0, -6.0, zt + 0.08), (1.2, 1.0, 0.14), "madera")
        t.caja((-6.0, -6.0, zt + 0.24), (1.2, 1.0, 0.14), "madera")
        t.caja((-4.4, -5.9, zt + 0.08), (1.2, 1.0, 0.14), "madera")
        t.caja((3.0, -6.2, zt + 0.55), (0.1, 0.5, 1.1), "oscuro", rot_x=0.35)
        t.cilindro((3.0, -6.6, zt + 0.15), 0.15, 0.6, "X", 8, "oscuro")
        t.caja((4.0, -5.6, zt + 0.25), (0.9, 0.6, 0.46), "saco")
