"""Estación pequeña colocable del Hito 1 (núcleo v2; encargo de Lucía del 11-11, docs/hito1-reparto.md). Teo (4), 11-11.

Apeadero con almacén que el jugador pone sobre su vía: es una pieza más del kit de vía (via_trazado.py). Trae su propio tramo
recto de 30 m, con el origen en el CONECTOR A (eje de la vía, en el suelo) y el conector B en (30; 0), así que se encaja en la
vía trazada como tres rectas de 10 m. Andén lateral de 24 × 4 m a +Y (borde a 1,9 m del eje, 0,68 m de alto), edificio de
8 × 5 m detrás, marquesina pequeña y pilas de cajas de munición en el andén (el almacén de la estación). Foto E1 de Paula (edificio
de viajeros) para el tipo, simplificado. MEDIDAS DE MEMORIA (SIN VERIFICAR).
Colores: los de la vía; andén 94948c con el borde f0dc9c, enfoscado b4a080, zócalo 605c48, huecos 605c48, teja a86c50,
marquesina 6c7874 sobre postes 6c7874, cajas 70784c con la tapa 847c34, sacos 605c48, tablas 845c28, tanque 6c7874, hollín
48403c, escombro 88885c. Nada en la banda, nada rojo ni ámbar; la cal d4d0cc está reservada para la selección.
Variantes: 0 Apeadero; 1 Fortificada (sacos en el andén y ventanas tapiadas); 2 Almacen (nave de chapa en lugar de la
marquesina y más cajas); 3 Abandonada (tejado roto, escombro y maleza, sin cajas); 4 Deposito (tanque de gasoil en su
bancada y bidones).
TBD_VARIANTE % 5.
"""
import math, os
import via_trazado as vt
from restos import _h

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
vt.VARIANTE = 2 if V == 3 else 0                     # la vía de la abandonada, con maleza; el resto, nueva
vt.PIEZA = 0
NOMBRES_V = ["Apeadero", "Fortificada", "Almacen", "Abandonada", "Deposito"]
PIEZAS = vt.PIEZAS + ["anden", "borde", "enfoscado", "zocalo", "hueco", "teja", "poste", "caja", "tapa", "sacos", "tabla",
                      "hollin"]
COLORES = {**vt.COLORES, "anden": "94948c", "borde": "f0dc9c", "enfoscado": "b4a080", "zocalo": "605c48", "hueco": "605c48",
           "teja": "a86c50", "poste": "6c7874", "caja": "70784c", "tapa": "847c34", "sacos": "605c48", "tabla": "845c28",
           "hollin": "48403c"}
FACCION = "neutro"
NORMALES_ARRIBA = ["balasto", "maleza", "maleza_c", "tierra"]
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (40, 7000), 1: (20, 2800)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
LARGO = 30.0
Y0, Y1, HA = 1.9, 5.9, 0.68                          # andén
EX0, EX1, EY0, EY1, EH = 11.0, 19.0, 6.4, 11.4, 3.4  # edificio


def _cajas(t, fino, x, y, filas, cols, alto=2):
    for i in range(cols):
        for j in range(filas):
            for k in range(alto):
                cx, cy, cz = x + i * 1.25, y + j * 1.05, HA + 0.27 + k * 0.55
                t.caja((cx, cy, cz), (1.2, 1.0, 0.52), "caja")
                if k == alto - 1:
                    t.caja((cx, cy, cz + 0.28), (1.1 if fino else 1.16, 0.9 if fino else 0.96, 0.04), "tapa")


def _edificio(t, fino):
    cx, cy = (EX0 + EX1) / 2, (EY0 + EY1) / 2
    w, d = EX1 - EX0, EY1 - EY0
    t.caja((cx, cy, EH / 2), (w, d, EH), "enfoscado")
    t.caja((cx, cy, 0.4), (w + 0.06, d + 0.06, 0.8), "zocalo")
    # fachada al andén (−Y): puerta y dos ventanas
    tapiar = V == 1
    for x, ancho, z, alto in ((cx, 1.2, 1.15, 2.3), (cx - 2.6, 1.0, 1.7, 1.2), (cx + 2.6, 1.0, 1.7, 1.2)):
        t.caja((x, EY0 - 0.03, z), (ancho, 0.06, alto), "tabla" if (tapiar and alto < 2) else "hueco")
    if fino:
        t.caja((cx, EY0 - 0.06, EH - 0.35), (3.0, 0.08, 0.5), "anden")                  # cartel con el nombre
        t.caja((cx, EY0 - 0.1, EH - 0.35), (2.6, 0.04, 0.3), "hueco")
    # tejado a dos aguas con la cumbrera a lo largo de X
    if V == 3:                                                                         # roto: solo un faldón y medio
        t.tejado((cx - 1.6, cy, EH), w * 0.55, d + 0.6, 1.4, pieza="teja", vuelo=0.3, aguas=2)
        t.caja((cx + 2.2, cy, EH - 0.12), (0.18, d - 0.4, 0.22), "tabla")
        t.caja((cx + 3.2, cy, EH - 0.12), (0.18, d - 0.4, 0.22), "hollin")
    else:
        t.tejado((cx, cy, EH), w + 0.6, d + 0.6, 1.4, pieza="teja", vuelo=0.3, aguas=2)


def construir(nombre="SM_EstacionH1", lod=0):
    t = vt.Taller(PIEZAS)
    fino = lod == 0
    via = vt._recta(LARGO)
    vt._via(t, via, fino)
    vt._maleza(t, via, fino)
    # andén
    t.caja(((LARGO) / 2, (Y0 + Y1) / 2, HA / 2), (24.0, Y1 - Y0, HA), "anden")
    t.caja((LARGO / 2, Y0 + 0.3, HA + 0.005), (24.0, 0.25 if fino else 0.4, 0.02), "borde")
    # explanada detrás del andén, hasta el edificio
    t.caja((LARGO / 2, (Y1 + 0.01 + EY1 + 1.0) / 2, 0.05), (24.0, EY1 + 1.0 - Y1 - 0.01, 0.1), "zocalo" if V == 3 else "anden")
    _edificio(t, fino)
    if V in (0, 1, 4):                                                                    # marquesina de 8 × 3 m
        for x in (4.6, 8.6, 12.6):
            if fino or x != 8.6:
                t.caja((x, Y1 - 0.5, HA + 1.5), (0.16 if fino else 0.3, 0.16 if fino else 0.3, 3.0), "poste")
        t.caja((8.6, Y1 - 1.6, HA + 3.05), (9.0, 3.4, 0.14), "metal", rot_x=-0.08)     # 6c7874: en 94948c no se separa del andén
    if V == 2:                                                                            # nave de chapa del almacén
        t.caja((5.6, 8.6, 1.9), (7.0, 5.0, 3.8), "chapa")
        t.tejado((5.6, 8.6, 3.8), 7.4, 5.4, 0.7, pieza="poste", vuelo=0.2, aguas=2)
        t.caja((5.6, 6.08, 1.5), (3.2, 0.06, 3.0), "hueco")
    # almacén de la estación: cajas de munición en el andén
    if V == 0:
        _cajas(t, fino, 19.6, Y0 + 1.2, 2, 3)
    elif V == 1:
        _cajas(t, fino, 20.6, Y0 + 1.6, 2, 2)
        for k in range(14 if fino else 7):                                               # sacos en el borde de atrás y a los lados
            x = 3.6 + (22.8 / (13 if fino else 6)) * k
            t.caja((x, Y1 - 0.35, HA + 0.15), (1.5 if fino else 3.2, 0.55, 0.3), "sacos", rot_z=(_h((k, 1, 0), 2) - .5) * .1)
            if k % 2 == 0:
                t.caja((x + 0.4, Y1 - 0.35, HA + 0.44), (1.4 if fino else 3.0, 0.5, 0.28), "sacos")
    elif V == 2:
        _cajas(t, fino, 15.0, Y0 + 1.0, 2, 5)
        _cajas(t, fino, 21.6, Y0 + 1.0, 2, 3, 3)
    elif V == 4:                                                                          # tanque de gasoil y bidones
        t.caja((22.5, 9.0, 0.5), (5.0, 2.2, 1.0), "zocalo")
        t.cilindro((22.5, 9.0, 2.05), 1.05, 4.6, "X", 12 if fino else 8, "poste")
        if fino:
            t.caja((22.5, 9.0, 3.15), (0.6, 0.6, 0.2), "chapa")
        for k in range(6 if fino else 3):
            t.cilindro((19.0 + 0.7 * k, Y0 + 2.6, HA + 0.45), 0.3, 0.9, "Z", 10 if fino else 6, "caja")
        _cajas(t, fino, 19.4, Y0 + 0.9, 1, 2)
    else:                                                                                 # abandonada: escombro y maleza
        for k in range(6 if fino else 3):
            r = _h((k, 3, 0), 4)
            t.caja((6 + 18 * r, Y0 + 0.8 + 2.4 * _h((k, 5, 0), 6), HA + 0.15), (1.0, 0.8, 0.3),
                   "hollin" if k % 2 else "balasto", rot_z=r * 3)
        for k in range(5 if fino else 3):
            t.caja((5 + 5 * k, 6.6 + 3.4 * _h((k, 7, 0), 8), 0.3), (1.2, 1.0, 0.5), "maleza" if k % 2 else "maleza_c", rot_z=k)
    return t.objeto(nombre, bisel=0)


def separar(o, lod):
    """El origen queda en el conector A (eje de la vía, x = 0), como el resto del kit de vía."""
    from mathutils import Vector, Matrix
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    o.data.transform(Matrix.Translation(base))
    if "tbd_base" in o:
        del o["tbd_base"]
    return [o]
