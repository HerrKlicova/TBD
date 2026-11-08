"""Catenaria bajo losa de la estación de Castelló (Lucía, 8-nov 18:00; foto RC3 de Paula: hilo de contacto bajo la losa
de hormigón, rígida o flexible SIN VERIFICAR). Teo (3), 8-nov.

Tramo de 10 m (9,98) a lo largo de X sobre el eje de UNA vía: origen en el eje de la vía, a la cota de la vía (−8 m de
la calle). Hilo o carril de contacto a 5,3 m (Álvaro, 8-nov 18:02); soportes atornillados al intradós de la losa, a
6,5 m, cada 5 m (en x −2,5 y 2,5, para que dos tramos seguidos no dupliquen soportes). Es una pieza aparte de la losa:
si Marcos oculta la losa con la cámara cenital, puede ocultar también estos soportes.
Colores: carril y soportes 94948c (aluminio), hilo y péndolas 6c7874, aisladores 6c7874; nada en la banda de la horda.
En el LOD1 nada baja de 0,34 m (el carril se lee como una barra clara sobre la vía).
Variantes (TBD_VARIANTE % 5): 0 rígida (carril de aluminio con soportes en pórtico); 1 flexible (sustentador y hilo con
péndolas, ménsulas cortas); 2 rota y colgando (un extremo baja hasta 1,5 m sobre la vía); 3 caída sobre la vía (el
carril en el suelo, los soportes vacíos); 4 arrancada (un soporte doblado y el hilo tendido hasta el andén, +Y).
"""
import math, os
from taller import Taller

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Rigida", "Flexible", "Colgando", "Caida", "Arrancada"]
PIEZAS = ["carril", "soporte", "hilo", "aislador"]
COLORES = {"carril": "94948c", "soporte": "94948c", "hilo": "6c7874", "aislador": "6c7874"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (10, 900), 1: (6, 300)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
LX, ZH, ZI = 9.98, 5.3, 6.5


def _barra(t, p0, p1, r, pieza):
    dx, dy, dz = p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]
    L = math.sqrt(dx * dx + dy * dy + dz * dz)
    t.caja(((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, (p0[2] + p1[2]) / 2), (r, r, L + r * 0.3), pieza,
           rot_z=math.atan2(dy, dx), rot_y=math.atan2(math.hypot(dx, dy), dz))


def construir(nombre="SM_CatenariaLosa", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.1 if fino else 0.34      # cables de ≥ 2 px (Lucía, nota del poste de catenaria)
    gc = 0.18 if fino else 0.34
    xs = (-2.5, 2.5)
    # soportes: placa en el intradós, dos bajantes y travesaño (pórtico pequeño) con aislador
    for i, x in enumerate(xs):
        t.caja((x, 0, ZI - 0.03), (0.3, 1.2, 0.06), "soporte")
        doblado = V == 4 and i == 1
        if doblado:
            _barra(t, (x, -0.4, ZI), (x + 0.5, -0.1, ZI - 0.6), g, "soporte")
            _barra(t, (x, 0.4, ZI), (x + 0.7, 0.9, ZI - 0.5), g, "soporte")
            continue
        top = ZI - (0.25 if V == 1 else 0.85)
        for s in (-1, 1):
            t.caja((x, s * 0.4, (ZI + top) / 2), (g, g, ZI - top), "soporte")
        t.caja((x, 0, top), (g, 0.86, g), "soporte")
        if V in (0, 2, 3, 4) and fino:
            t.caja((x, 0, top - 0.12), (0.08, 0.08, 0.2), "aislador")
    # carril o hilo
    if V == 0:
        t.caja((0, 0, ZH + 0.08), (LX, gc, 0.2 if fino else 0.34), "carril")
        if fino:
            for x in xs:
                t.caja((x, 0, (ZH + 0.16 + ZI - 0.97) / 2 + 0.04), (0.06, 0.06, ZI - 0.97 - ZH - 0.08), "soporte")
    elif V == 1:
        zs = ZI - 0.35
        t.caja((0, 0, zs), (LX, g, g), "hilo")                              # sustentador
        t.caja((0, 0, ZH), (LX, g, g), "hilo")                              # hilo de contacto
        for k in range(5 if fino else 2):
            x = -4 + k * 2 if fino else (-2.5 + 5 * k)
            t.caja((x, 0, (zs + ZH) / 2), (g * 0.6 if fino else 0.34, g * 0.6 if fino else 0.34, zs - ZH), "hilo")
    elif V == 2:                                     # colgando: de x −2,5 (sujeto) baja hasta 1,5 m en x +5
        t.caja((-3.75, 0, ZH + 0.08), (2.5, gc, 0.2 if fino else 0.34), "carril")
        _barra(t, (-2.5, 0, ZH + 0.08), (4.9, 0.3, 1.5), gc, "carril")
        _barra(t, (2.5, 0, ZI - 0.97), (2.6, 0.1, ZH - 0.6), g, "hilo")       # péndola suelta
    elif V == 3:                                     # caído sobre la vía
        t.caja((0.3, 0.25, 0.25), (LX - 0.4, gc, 0.2 if fino else 0.34), "carril", rot_z=0.04)
        _barra(t, (-4.6, 0.2, 0.3), (-4.9, 0.6, 1.4), g, "hilo")
    else:                                            # arrancada: el carril se va hacia el andén (+Y)
        t.caja((-2.5, 0, ZH + 0.08), (5.0, gc, 0.2 if fino else 0.34), "carril")
        _barra(t, (0.0, 0, ZH + 0.08), (3.2, 2.6, 2.2), gc, "carril")
        _barra(t, (3.2, 2.6, 2.2), (4.6, 4.2, 0.75), g, "hilo")              # hasta el andén (0,68 m)
    return t.objeto(nombre, bisel=0)
