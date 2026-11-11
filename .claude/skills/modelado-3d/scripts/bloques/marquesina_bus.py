"""Marquesina de la estación de autobuses de Castellón (foto CS9 de Paula: cubierta ondulada sobre pilares en V y viga de
borde en celosía). Kit del cerco de Castellón, después del MVP (aprobado por Lucía el 11-nov). Teo (4), 11-nov.

MÓDULO DE 12 m (x −6..6) y 10 m de fondo (y −5..5): tres bóvedas de 4 m con el eje a lo ancho (Y), bajas en los pilares
(4,5 m) y con 0,7 m de flecha en el centro. Pilares en x −6, −2 y 2 en dos filas (y ±3); el de x = 6 lo pone el módulo
siguiente (se encadenan a lo largo de X). Origen en el suelo, en el centro del módulo. MEDIDAS A OJO DE LA FOTO (SIN VERIFICAR).
Colores: cubierta 94948c, viga de borde 6c7874, pilares b8b8b8 con bandas 94948c (el fuste trenzado de la foto), losa
88885c, tablones 845c28, sacos 605c48, lona 605c48, hollín 48403c. Nada en la banda, nada rojo ni ámbar.
Variantes: v0 limpia; v1 chapas arrancadas (huecos en la cubierta y chapas en el suelo); v2 barricada de tablones y sacos
entre los pilares de delante; v3 lonas colgadas del borde; v4 quemada (media cubierta en hollín).
Estados: sano; dañado (la bóveda de un extremo cuelga de un lado); muy dañado (dos bóvedas en el suelo y pilares caídos);
brecha (todo en el suelo: muñones de pilar y chapas, no tapa nada).
TBD_VARIANTE = 4 · variante + estado (0..19).
"""
import math, os
from montaje import Montaje
from restos import _h

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 20
VARIANTE, ESTADO = VAR // 4, VAR % 4
NOMBRES_V = ["Limpia", "Chapas", "Barricada", "Lonas", "Quemada"]
NOMBRES_E = ["Sano", "Danado", "MuyDanado", "Brecha"]
PIEZAS = ["cubierta", "viga", "pilar", "banda", "losa", "tablon", "sacos", "lona", "hollin"]
COLORES = {"cubierta": "94948c", "viga": "6c7874", "pilar": "b8b8b8", "banda": "94948c", "losa": "88885c",
           "tablon": "845c28", "sacos": "605c48", "lona": "605c48", "hollin": "48403c"}
FACCION = "neutro"
PRESUPUESTO = {0: (40, 5000), 1: (20, 1800)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
LARGO, FONDO, H, FLECHA, BAHIA = 12.0, 10.0, 4.5, 0.7, 4.0
XS_PILAR = (-6.0, -2.0, 2.0)
YS_PILAR = (-3.0, 3.0)


def _modos():
    """Estado de cada bóveda (de x −6 a 6): ok, cuelga (un extremo en el suelo), suelo, falta."""
    return {0: ["ok", "ok", "ok"], 1: ["ok", "ok", "cuelga"], 2: ["cuelga", "suelo", "suelo"],
            3: ["suelo", "suelo", "suelo"]}[ESTADO]


def _pilar_en_pie(x):
    if ESTADO == 0:
        return True
    if ESTADO == 1:
        return True
    if ESTADO == 2:
        return x < -5
    return False


def _pieza_cubierta(i, j):
    """Pieza del segmento j de la bóveda i (hollín en media cubierta en la v4)."""
    if VARIANTE == 4 and (i == 0 or (i == 1 and j < 3)):
        return "hollin"
    return "cubierta"


def _boveda(m, i, modo, fino):
    x0 = -LARGO / 2 + BAHIA * i
    n = 6 if fino else 3
    huecos = set()
    if VARIANTE == 1:                                    # chapas arrancadas
        huecos = {j for j in range(n) if _h((i, j, 1), 3) < 0.3}
    for j in range(n):
        if j in huecos and modo == "ok":
            continue
        t0, t1 = j / n, (j + 1) / n
        xa, xb = x0 + BAHIA * t0, x0 + BAHIA * t1
        za, zb = H + FLECHA * math.sin(math.pi * t0), H + FLECHA * math.sin(math.pi * t1)
        if modo == "ok":
            ang = math.atan2(zb - za, xb - xa)
            L = math.hypot(xb - xa, zb - za) - 0.01             # sin vértices compartidos con la vecina
            m.caja(((xa + xb) / 2, 0, (za + zb) / 2), (L, FONDO, 0.12), _pieza_cubierta(i, j), rot_y=-ang)
    if modo == "cuelga":                                  # un extremo sigue en el pilar, el otro toca el suelo
        caida = math.atan2(H - 0.6, BAHIA)
        lado = 1                                          # queda colgada del pilar de su izquierda (el de la derecha cayó)
        xc = x0 + BAHIA / 2
        m.caja((xc, 0, (H + 0.6) / 2), (BAHIA / math.cos(caida), FONDO - 0.6, 0.12), _pieza_cubierta(i, 0),
               rot_y=lado * caida)
    elif modo == "suelo":                                 # chapas en el suelo, algo montadas
        for k in range(3 if fino else 2):
            r = _h((i, k, 2), 5)
            m.caja((x0 + 0.8 + 1.2 * k, -3 + 3 * k + (r - .5), 0.18 + 0.12 * k),
                   (BAHIA * 0.8, FONDO * 0.32, 0.12), _pieza_cubierta(i, k), rot_z=(r - .5) * .5, rot_y=(r - .5) * .25)


def _viga(m, i, modo, fino):
    if modo != "ok":
        return
    x0 = -LARGO / 2 + BAHIA * i
    for s in (-1, 1):
        y = s * (FONDO / 2 - 0.1)
        m.caja((x0 + BAHIA / 2, y, H + FLECHA + 0.2), (BAHIA - 0.01, 0.12, 0.12), "viga")      # cordón de arriba
        m.caja((x0 + BAHIA / 2, y, H + 0.05), (BAHIA - 0.01, 0.12, 0.12), "viga")               # cordón de abajo
        if fino:                                                                          # diagonales de la celosía
            for k in range(4):
                xa = x0 + k * 1.0
                m.barra((xa, y, H + 0.05), (xa + 1.0, y, H + FLECHA + 0.2), 0.06, 0.06, "viga")


def _pilar(m, x, y, fino):
    lados = 8 if fino else 6
    if _pilar_en_pie(x):
        if fino:                                         # fuste trenzado: bandas alternas
            z = 0.0
            k = 0
            while z < 3.2:
                m.cilindro((x, y, z + 0.2), 0.2, 0.38, "Z", lados, "pilar" if k % 2 == 0 else "banda")
                z += 0.4
                k += 1
        else:
            m.cilindro((x, y, 1.6), 0.2, 3.2, "Z", lados, "pilar")
        for s in (-1, 1):                                # brazos de la V a lo largo de X
            m.barra((x, y, 3.1), (x + s * 0.9, y, H + 0.02), 0.22, 0.22, "pilar")
    elif ESTADO == 3 or x > -5:
        alto = 0.6 + 0.6 * _h((x, y, 4), 7)              # muñón
        m.cilindro((x, y, alto / 2), 0.2, alto, "Z", lados, "pilar")
        if ESTADO == 2 or fino:                          # fuste caído en el suelo
            a = _h((x, y, 5), 8) * 6.28
            m.barra((x + math.cos(a) * 0.3, y + math.sin(a) * 0.3, 0.2),
                    (x + math.cos(a) * 3.0, y + math.sin(a) * 3.0, 0.2), 0.36, 0.36, "pilar")


def construir(nombre="SM_MarquesinaBus", lod=0):
    m = Montaje(PIEZAS, ["marquesina"])
    fino = lod == 0
    m.caja((0, 0, 0.075), (LARGO, 7.0, 0.15), "losa")                     # andén de las dársenas
    if fino:
        m.caja((0, 3.55, 0.1), (LARGO, 0.2, 0.2), "losa")                # bordillo
    modos = _modos()
    for i, modo in enumerate(modos):
        _boveda(m, i, modo, fino)
        _viga(m, i, modo, fino)
    for x in XS_PILAR:
        for y in YS_PILAR:
            _pilar(m, x, y, fino)
    if VARIANTE == 1:                                    # chapas arrancadas por el suelo
        for k in range(3 if fino else 2):
            r = _h((k, 9, 1), 9)
            m.caja((-4.5 + 4.0 * k, -4.0 + 1.2 * r, 0.06), (1.8, 1.0, 0.06), "cubierta", rot_z=r * 2.0)
    if VARIANTE == 2:                                    # barricada entre los pilares de delante (y = 3)
        for k, x in enumerate((-6.0, -2.0)):
            if ESTADO < 3:
                for j in range(3 if fino else 2):
                    m.caja((x + 2.0, 3.0, 0.35 + 0.35 * j), (3.6, 0.08, 0.28), "tablon",
                           rot_x=(_h((k, j, 3), 10) - .5) * .1, rot_z=(_h((k, j, 4), 11) - .5) * .06)
            for j in range(4 if fino else 2):
                m.caja((x + 0.9 + 0.75 * j, 3.45, 0.14), (0.7, 0.45, 0.28), "sacos", rot_z=(_h((k, j, 5), 12) - .5) * .3)
    if VARIANTE == 3 and ESTADO < 3:                     # lonas colgadas del borde de delante
        for k in range(3):
            if modos[k] != "ok":
                continue
            x = -LARGO / 2 + BAHIA * (k + 0.5)
            cuelga = 1.6 + 0.8 * _h((k, 6, 0), 13)
            m.caja((x, FONDO / 2 - 0.2, H - cuelga / 2), (BAHIA * 0.7, 0.05, cuelga), "lona",
                   rot_x=(_h((k, 7, 0), 14) - .5) * .2)
        if fino:                                         # tienda bajo la cubierta
            m.tejado((-3.5, -1.0, 0.15), 2.2, 1.8, 1.2, pieza="lona", aguas=2)
    if VARIANTE == 4:                                    # restos quemados en el andén
        for k in range(3 if fino else 2):
            m.caja((-5.0 + 1.6 * k, -1.5 + 0.8 * k, 0.25), (1.2, 0.6, 0.2), "hollin", rot_z=k * 0.9)
    if ESTADO >= 2:                                      # escombro de la cubierta
        for k in range(4 if fino else 2):
            r = _h((k, 8, ESTADO), 15)
            m.caja((-5 + 10 * r, 4.4 - 9 * _h((k, 9, ESTADO), 16), 0.08), (0.9, 0.7, 0.16), "hollin" if k % 2 else "viga",
                   rot_z=r * 3)
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector, Matrix
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    o.data.transform(Matrix.Translation(base))
    if "tbd_base" in o:
        del o["tbd_base"]
    return [o]
