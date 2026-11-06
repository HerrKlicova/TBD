"""Vagón tolva de mercancías, volcado (Álvaro, sagunto-nivel.md §2 y §5; corrección C2). Teo, 6-nov-2026.

El tren de mercancías descarrilado en la vía 2, al oeste: 3 o 4 tolvas volcadas. Tolva de cuatro ejes para áridos o
cereal: caja de costados rectos con dos tolvas en V debajo, plataformas en los testeros, topes y dos bogies.
Fotos G1 de Paula (Commons: tolvas TT1 de Renfe Mercancías, verdes, de costado y en 3/4). MEDIDAS SIN VERIFICAR (Paula no
ha encontrado la longitud entre topes en ninguna ficha) (típicas): 14 m entre topes, caja de 12,2 × 3,0 m con el
borde a 4,1 m sobre el carril, bogies con centros a ±4,8 m, batalla de 1,8 m y ruedas de 0,92 m.
Colores (Lucía, 17:49: 845c28 es ocre de suelo, vetado en costados): caja de óxido 7c3c18 con costillas 543820, bastidor y bogies 48403c, ruedas 283440 (nada de valores de la
horda), carga de grava 94948c. El vagón va a lo largo de Y (delante = −Y, +Z en Unity).
Variantes (C2: un vagón volcado cae sobre su costado, girado sobre su eje largo y con los bogies a la vista):
0 de pie (descarrilado, sin girar); 1 sobre el costado (90°); 2 recostado a 75° contra la carga; 3 sobre el costado con la
grava derramada; 4 de pie, inclinado 12° y sin un bogie (suelto al lado); 5 boca abajo (180°).
El giro va en la malla: el origen queda en el suelo, bajo el centro del vagón.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["casco", "costilla", "bastidor", "rueda", "carga"]
COLORES = {"casco": "50541c", "costilla": "543820", "bastidor": "48403c", "rueda": "283440", "carga": "94948c"}
VALOR_PIEZAS = {"casco": ("70784c", "50541c")}   # verde de las TT1 de Renfe de las fotos G1 (antes óxido 7c3c18)
FACCION = "neutro"
PRESUPUESTO = {0: (100, 2000), 1: (60, 900)}
HX, LY, ZB = 1.5, 6.1, 4.1           # semiancho, semilargo de la caja y borde superior


def construir(nombre="SM_TolvaVolcada", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    v = VAR % 6
    # caja con las dos tolvas en V (perfil lateral y-z extruido en X)
    P = [(-LY, ZB), (LY, ZB), (LY, 2.3), (2.6, 1.3), (1.6, 1.3), (0.4, 2.0), (-0.4, 2.0), (-1.6, 1.3), (-2.6, 1.3),
         (-LY, 2.3)]
    t.perfil_extruido(P, lambda z: HX if z >= 3.0 else HX - (3.0 - z) * 0.25, cortes_z=(3.0,), pieza="casco")   # G1: costados que se estrechan hacia abajo
    # borde superior y costillas verticales en los costados
    for sx in (-1, 1):
        t.caja((sx * (HX + 0.06), 0, ZB - 0.12), (0.14, 2 * LY + 0.1, 0.24), "costilla")
        for y in (-4.5, -1.5, 1.5, 4.5):
            (t if abs(y) > 2 else fino).caja((sx * (HX + 0.05), y, 3.25), (0.1, 0.35, 1.6), "costilla")
    # bocas de descarga bajo las tolvas
    for s in (-1, 1):
        t.caja((0, s * 2.1, 1.15), (1.8, 1.2, 0.32), "bastidor")
    # plataformas de testero, topes y bogies
    bogies_quitados = (1,) if v == 4 else ()
    for i, s in enumerate((-1, 1)):
        t.caja((0, s * (LY + 0.3), 1.2), (2.8, 0.9, 0.3), "bastidor")
        t.caja((0, s * 4.8, 1.55), (2.6, 3.0, 0.5), "bastidor")                 # traviesa de pivote bajo la caja
        for x in (-0.9, 0.9):
            t.cilindro((x, s * (LY + 0.9), 1.05), 0.2, 0.5, "Y", 8, "bastidor")
        fino.caja((0, s * (LY + 0.62), 2.2), (2.4, 0.06, 1.6), "costilla")      # escalerilla / pared del testero (LOD0)
        if i in bogies_quitados:
            continue
        _bogie(t, 0, s * 4.8)
    if v == 4:                                                                   # bogie suelto, tirado al lado
        _bogie(t, 3.4, 4.6, girado=True)
    # carga: grava rasa (de pie) o asomando por la boca (volcado)
    if v in (0, 4):
        t.caja((0, 0, ZB - 0.05), (2 * HX - 0.2, 2 * LY - 0.3, 0.2), "carga")
        fino.caja((0, 0, ZB + 0.15), (2 * HX - 0.6, 2 * LY - 1.6, 0.3), "carga")
    else:
        t.caja((0, 0, ZB - 0.15), (2 * HX - 0.2, 2 * LY - 0.3, 0.2), "carga")
    if v == 3:   # grava derramada por la boca: en coordenadas antes del giro de 90° (x -> -z, z -> x)
        for h, d, ly in ((0.95, 1.1, 2 * LY - 1.0), (0.6, 2.1, 2 * LY - 2.4), (0.28, 3.2, 2 * LY - 4.4)):   # 3 escalones
            t.caja((1.63 - h / 2 + 0.01 * d, 0.3 * (d - 1), 3.9 + (d + 0.2) / 2), (h, ly, d + 0.2), "carga")
    o = t.objeto(nombre, bisel=0)
    from mathutils import Matrix
    ang = {1: 90, 2: 75, 3: 90, 5: 180}.get(v, 0)
    if v == 4:
        o.data.transform(Matrix.Rotation(math.radians(12), 4, "Y"))
    if ang:
        o.data.transform(Matrix.Translation((0, 0, -ZB / 2)))
        o.data.transform(Matrix.Rotation(math.radians(ang), 4, "Y"))
        zmin = min(vx.co.z for vx in o.data.vertices)
        o.data.transform(Matrix.Translation((0, 0, -zmin)))
    return o


def _bogie(t, x0, y0, girado=False):
    """Bogie de dos ejes: bastidor lateral y cuatro ruedas de 0,92 m (girado: tirado de lado en el suelo)."""
    if girado:
        t.caja((x0, y0, 0.35), (1.0, 3.0, 0.5), "bastidor")
        for e in (-0.9, 0.9):
            t.cilindro((x0, y0 + e, 0.25), 0.46, 0.5, "Z", 12, "rueda")
        return
    for x in (-1.0, 1.0):
        t.caja((x0 + x, y0, 0.62), (0.22, 2.9, 0.38), "bastidor")
    for e in (-0.9, 0.9):
        for x in (-0.82, 0.82):
            t.cilindro((x0 + x, y0 + e, 0.46), 0.46, 0.13, "X", 12, "rueda")

