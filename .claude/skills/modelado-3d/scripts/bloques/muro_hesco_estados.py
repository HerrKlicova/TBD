"""Muro de HESCO (gaviones de malla rellenos de tierra): 5 variantes × 4 estados (D-N163 §1, muestra de Álvaro 236586a,
aprobada por Lucía el 8-nov). Teo, 8-nov.

MÓDULO DE 10 m (x −5..5) y 2,1 m de alto (D-N59), con 9 celdas de 1,11 m y 1,1 m de fondo. Origen en el suelo en el
centro del módulo, el muro a lo largo de X y FUERA = −Y (lo que cae, cae hacia allí). Medidas de un gavión de defensa de
memoria (SIN VERIFICAR).
Colores (Álvaro, 8-nov): HESCO 70784c (geotextil oscuro 50541c), malla b8b8b8, relleno b4a080, sacos del remate
605c48, red 50541c con tiras 605c48 (Lucía 10:10), escombro 48403c.
Variantes: v0 malla limpia; v1 geotextil a la vista (celdas en 50541c con la malla más tupida); v2 doble escalonado
(celdas de 2,1 y 1,4 m alternas); v3 remate de sacos (celdas de 1,8 m con una hilada de sacos encima); v4 red de
camuflaje colgada por la cara de fuera.
Estados: sano; dañado (dos celdas reventadas y medio vacías, con la tierra derramada fuera); muy dañado (todas hundidas a
media altura); brecha (aplastado: restos bajos, malla rota y tierra, que no bloquean).
TBD_VARIANTE = 4 · variante + estado (0..19).
"""
import math, os
from montaje import Montaje
from restos import _h

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 20
VARIANTE, ESTADO = VAR // 4, VAR % 4
NOMBRES_V = ["MallaLimpia", "Geotextil", "Escalonado", "RemateSacos", "Red"]
NOMBRES_E = ["Sano", "Danado", "MuyDanado", "Brecha"]
PIEZAS = ["hesco", "geo", "malla", "relleno", "sacos", "red", "tira", "roto"]
COLORES = {"hesco": "70784c", "geo": "50541c", "malla": "b8b8b8", "relleno": "b4a080", "sacos": "605c48",
           "red": "50541c", "tira": "605c48", "roto": "48403c"}
FACCION = "propio"
PRESUPUESTO = {0: (20, 6000), 1: (10, 2000)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
LARGO, ALTO, FONDO, N = 10.0, 2.1, 1.1, 9
PASO = LARGO / N


def _alto_sano(k):
    if VARIANTE == 2:
        return ALTO if k % 2 == 0 else 1.4
    if VARIANTE == 3:
        return 1.8
    return ALTO


def _alto(k):
    """Altura de la celda k en el estado actual."""
    h = _alto_sano(k)
    r = _h((k, ESTADO, VARIANTE), 3)
    if ESTADO == 1:
        return h * (0.45 + 0.1 * r) if k in (4, 5) else h
    if ESTADO == 2:
        return 0.7 + 0.35 * r
    if ESTADO == 3:
        return 0.12 + 0.12 * r if k in (0, 1, 7, 8) else 0.0
    return h


def _celda(m, k, h, fino):
    xc = -LARGO / 2 + PASO * (k + 0.5)
    cuerpo = "geo" if VARIANTE == 1 else "hesco"
    rota = ESTADO >= 1 and h < _alto_sano(k) - 0.05
    ancho = FONDO + (0.12 if rota else 0.0)               # la celda reventada se abomba
    m.caja((xc, -0.06 if rota else 0, h / 2), (PASO - 0.04, ancho, h), cuerpo)
    m.caja((xc, 0, h + 0.015), (PASO - 0.14, FONDO - 0.1, 0.03), "relleno")
    if not fino or h < 0.3:
        if not fino and h > 0.6:                         # LOD1: una banda de malla a media altura (2 px)
            m.caja((xc, -FONDO / 2 - 0.02, h * 0.55), (PASO - 0.1, 0.03, 0.18), "malla")
        return
    # malla: bandas horizontales en las caras de fuera y de dentro, y el canto vertical entre celdas
    paso = 0.3 if VARIANTE == 1 else 0.45
    z = paso
    while z < h - 0.1:
        for s in (-1, 1):
            m.caja((xc, s * (ancho / 2 + 0.01) - (0.06 if rota else 0), z), (PASO - 0.08, 0.02, 0.04), "malla")
        z += paso
    m.caja((xc - PASO / 2 + 0.02, -ancho / 2 - 0.01, h / 2), (0.04, 0.03, h), "malla")
    if rota:                                              # malla rota asomando por arriba
        for j in range(3):
            m.barra((xc - 0.4 + 0.4 * j, -FONDO / 2, h), (xc - 0.3 + 0.35 * j, -FONDO / 2 - 0.35, h + 0.3 + 0.2 * j),
                    0.04, 0.04, "malla")


def construir(nombre="SM_MuroHesco", lod=0):
    m = Montaje(PIEZAS, ["muro"])
    fino = lod == 0
    altos = [_alto(k) for k in range(N)]
    for k in range(N):
        if altos[k] > 0:
            _celda(m, k, altos[k], fino)
    # remate de sacos de la v3 (dos a lo ancho) sobre las celdas que siguen enteras
    if VARIANTE == 3:
        for k in range(N):
            if altos[k] < _alto_sano(k) - 0.05:
                continue
            xc = -LARGO / 2 + PASO * (k + 0.5)
            if fino:
                for s in (-1, 1):
                    m.caja((xc, s * 0.27, 1.8 + 0.15), (PASO - 0.06, 0.5, 0.28), "sacos", rot_z=(_h((k, s, 0), 4) - .5) * .08)
            else:
                m.caja((xc, 0, 1.8 + 0.15), (PASO - 0.06, 1.04, 0.28), "sacos")
    # red de la v4: cuelga por la cara de fuera desde lo alto de cada celda entera, con tiras de color
    if VARIANTE == 4 and ESTADO < 3:
        for k in range(N):
            h = altos[k]
            if ESTADO == 2 and _h((k, 9, 0), 5) < 0.5:
                continue
            xc = -LARGO / 2 + PASO * (k + 0.5)
            cuelga = min(h, 0.9 + 0.6 * _h((k, 1, 0), 6))
            m.caja((xc, 0, h + 0.04 + 0.005 * (k % 2)), (PASO - 0.03, FONDO + 0.08, 0.04), "red")
            m.caja((xc, -FONDO / 2 - 0.05 - 0.005 * (k % 2), h - cuelga / 2), (PASO - 0.03, 0.04, cuelga), "red")
            for j in range(2 if fino else 1):
                m.caja((xc - 0.25 + 0.5 * j, -FONDO / 2 - 0.08, h - cuelga * 0.45), (0.14, 0.03, cuelga * 0.8), "tira")
    # tierra derramada fuera (y algo dentro), restos de malla y de celdas en la brecha
    montones = {1: [(0.0, -1.0, 1.4, 0.6, 0.3)],                       # tierra derramada, sin tapar el muro
                2: [(-2.0, -0.95, 1.6, 0.6, 0.3), (1.8, -1.0, 1.4, 0.55, 0.26), (0.0, 0.85, 1.4, 0.4, 0.18)],
                3: [(-2.2, -0.5, 1.8, 0.8, 0.18), (1.9, -0.3, 1.5, 0.7, 0.16), (0.2, 0.7, 1.2, 0.5, 0.12)]}
    for x, y, sx, sy, sz in montones.get(ESTADO, []):
        m.caja((x, y, sz / 2), (sx, sy, sz), "relleno", rot_z=0.12)
        mn = 0.0 if fino else 0.36                                  # LOD1: ≥ 2 px (Lucía 10:10)
        m.caja((x + 0.1, y, sz + 0.06), (max(sx * 0.6, mn), max(sy * 0.55, mn), 0.12), "relleno", rot_z=0.3)
    if ESTADO == 3:
        w = 0.05 if fino else 0.1
        for j in range(10 if fino else 5):               # malla aplastada: varillas y paneles caídos
            x = -4.2 + 0.9 * j + (_h((j, 2, 0), 7) - 0.5) * 0.4
            m.barra((x, -0.9, 0.06), (x + 0.6 + 0.5 * _h((j, 3, 0), 8), 0.6, 0.18 + 0.15 * _h((j, 4, 0), 9)), w, w, "malla")
        for j in range(7):                               # paneles de gavión tumbados (lo que más se ve de la brecha)
            x = -4.0 + 1.35 * j
            m.caja((x, -0.4 + 0.5 * _h((j, 5, 0), 10), 0.05), (1.0, 0.9, 0.06), "hesco" if VARIANTE != 1 else "geo",
                   rot_z=(_h((j, 6, 0), 11) - 0.5) * 0.8)
        if VARIANTE == 3:
            for j in range(6 if fino else 3):
                m.caja((-3.5 + 1.4 * j, -1.5 + 0.4 * (j % 2), 0.13), (0.8, 0.46, 0.26), "sacos", rot_z=j * 0.7)
        if VARIANTE == 4:
            for j in range(3):
                m.caja((-3 + 3 * j, -1.6, 0.03), (1.4, 0.8, 0.05), "red", rot_z=j * 0.9)
    elif ESTADO >= 1:                                   # escombro y sacos del remate caídos
        n = 3 if ESTADO == 1 else 6
        for j in range(n):
            x = -2.5 + 5.0 * _h((j, 7, 0), 12)
            p = "sacos" if VARIANTE == 3 else "roto"
            m.caja((x, -1.6 - 0.6 * _h((j, 8, 0), 13), 0.13), (0.7, 0.45, 0.26), p, rot_z=j * 1.1)
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector, Matrix
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    o.data.transform(Matrix.Translation(base))
    if "tbd_base" in o:
        del o["tbd_base"]
    return [o]
