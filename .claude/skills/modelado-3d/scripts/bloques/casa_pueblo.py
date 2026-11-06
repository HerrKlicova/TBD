"""Casa de pueblo entre medianeras para las hileras del pueblo del norte (Álvaro, docs/niveles/sagunto-nivel.md §3b).
Teo, 6-nov-2026. Fachada estrecha de 6 m que se pega a la de al lado: los costados (±X) son medianeras lisas, sin
vuelo de tejado ni huecos, para que dos casas puestas a 6 m de distancia formen una hilera continua.

MEDIDAS SIN VERIFICAR (típicas de casa de pueblo valenciana de dos plantas; no hay foto de Paula de esta tipología,
solo las alquerías E4): 6 x 10 m, plantas de 3,2 m, tejado de teja a dos aguas con la cumbrera paralela a la calle y
0,4 m de vuelo solo a la calle y al patio. Fachada a la calle (-Y): puerta y ventana con reja abajo, balcón arriba.
Colores: cal d4d0cc (o los de cada variante), zócalo 605c48, carpintería 543820, teja a86c50, reja y baranda 1c200c.
"""
import os
from taller import Taller

# Variantes (Eugenio, 6-nov: al menos 5): 0 dos plantas, cal; 1 tres plantas, enfoscado ocre; 2 una planta con cámara
# (baja); 3 cochera y cal amarilla; 4 tapiada (tablas); 5 balcón corrido y cal gastada.
VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
_CAL = ("d4d0cc", "b4a080", "d4d0cc", "f0dc9c", "d4d0cc", "d4d0cc")
_ZOC = ("605c48", "605c48", "283440", "6c7874", "605c48", "283440")
PIEZAS = ["cal", "zocalo", "carpinteria", "tejado", "reja", "balcon", "tablas", "alero"]
COLORES = {"cal": _CAL[VAR % 6], "zocalo": _ZOC[VAR % 6], "carpinteria": "543820", "tejado": "a86c50",
           "reja": "48403c", "balcon": "94948c", "tablas": "845c28", "alero": "94948c"}
FACCION = "neutro"
PRESUPUESTO = {0: (60, 1200), 1: (40, 600)}
FRENTE, FONDO, PLANTA = 6.0, 10.0, 3.2


def construir(nombre="SM_CasaPueblo", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    plantas = (2, 3, 1, 2, 2, 2)[VAR % 6]
    alto = plantas * PLANTA + (0.9 if plantas == 1 else 0.0)        # la de una planta lleva cámara bajo cubierta
    hx, hy = FRENTE / 2, FONDO / 2
    t.caja((0, 0, alto / 2), (FRENTE, FONDO, alto), "cal")
    t.caja((0, -hy - 0.02, 0.45), (FRENTE - 0.02, 0.06, 0.9), "zocalo")      # zócalo solo en la fachada
    t.caja((0, -hy - 0.15, alto - 0.1), (FRENTE - 0.02, 0.3, 0.2), "alero")   # alero de la fachada
    # tejado: vuelo solo a la calle y al patio (largo efectivo FRENTE - 0,8 para que el vuelo lateral quede a ras)
    t.tejado((0, 0, alto), FRENTE - 0.8, FONDO, 2.0, "tejado", vuelo=0.4, aguas=2)
    yf = -hy - 0.03
    # planta baja: puerta + ventana con reja, o cochera
    if VAR == 3:
        t.caja((0.6, yf, 1.25), (2.8, 0.06, 2.5), "carpinteria")
        t.caja((-2.0, yf, 1.2), (0.9, 0.06, 2.3), "carpinteria")
    else:
        t.caja((-1.4, yf, 1.2), (1.1, 0.06, 2.4), "carpinteria")
        t.caja((1.3, yf, 1.6), (1.1, 0.06, 1.4), "carpinteria")
        fino.caja((1.3, yf - 0.06, 1.6), (1.2, 0.04, 1.5), "reja")
        if VAR == 4:
            for x in (-1.4, 1.3):
                t.caja((x, yf - 0.08, 1.4), (1.3, 0.05, 0.3), "tablas")
                t.caja((x, yf - 0.1, 1.9), (1.3, 0.05, 0.3), "tablas")
    # plantas altas: balcón con puerta (dos huecos por planta)
    for p in range(1, plantas):
        z = p * PLANTA
        huecos = (-1.4, 1.4)
        for x in huecos:
            t.caja((x, yf, z + 1.2), (1.0, 0.06, 2.2), "carpinteria")
            if VAR == 4:
                t.caja((x, yf - 0.08, z + 1.3), (1.1, 0.05, 0.3), "tablas")
        if VAR == 5:   # balcón corrido
            t.caja((0, -hy - 0.4, z + 0.05), (FRENTE - 0.6, 0.8, 0.12), "balcon")
            fino.caja((0, -hy - 0.78, z + 0.6), (FRENTE - 0.6, 0.04, 0.95), "reja")
        else:
            for x in huecos:
                t.caja((x, -hy - 0.3, z + 0.05), (1.5, 0.6, 0.12), "balcon")
                fino.caja((x, -hy - 0.58, z + 0.6), (1.5, 0.04, 0.95), "reja")
    if plantas == 1:   # ventanuco de la cámara
        t.caja((0, yf, alto - 0.55), (0.9, 0.06, 0.5), "carpinteria")
    # patio trasero: un hueco por planta
    for p in range(plantas):
        t.caja((0.8, hy + 0.03, p * PLANTA + 1.6), (1.0, 0.06, 1.2), "carpinteria")
    return t.objeto(nombre, bisel=0)
