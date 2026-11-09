"""Muro de sacos terreros: 5 variantes × 4 estados (D-N163 §1, muestra de Álvaro 236586a, aprobada por Lucía el
8-nov). Teo, 8-nov.

TRAMO DE 8 m (x −4..4: los dos módulos de 4 m de muro_sacos.py en uno, como en la muestra), 1,0 m de grueso, origen en
el suelo en el centro del tramo, el muro a lo largo de X y FUERA = −Y (el sur, por donde llega la horda: lo que cae, cae
hacia allí). Sacos de 0,85 × 0,48 × 0,3 m, dos a lo ancho, en hiladas trabadas. Forma de los sacos: K3_control_1 de
Paula; el resto DE MEMORIA (SIN VERIFICAR).
Colores (Álvaro, 8-nov): sacos 605c48 (no 88885c, que es la hierba), sacos reventados, sombra y escombro 48403c, arena
derramada b4a080, sacos claros de la v2 847c34, tablón de la v3 a86c50 y red de la v4 50541c (Lucía 10:10: 303424 bajaba de 48403c).
Siluetas (Lucía: v0–v2 se distinguen por la silueta):
  v0 recto, 4 hiladas (1,2 m);  v1 en talud, 5 hiladas, cada una 0,5 m más corta por cada punta;  v2 irregular de 0,9 a
  1,8 m, con sacos claros sueltos y dos aspilleras en la cuarta hilada (la parte por encima de 1,2 m es solo silueta: en
  el juego el tramo cuenta como 1,2 m, el umbral de brecha de Hugo);  v3 recto con un tablón encima;  v4 recto con red.
Estados: sano (≥ 70 %), dañado (30–70 %: muescas arriba, sacos reventados y caídos), muy dañado (1–30 %: hundido a media
altura) y brecha (0: escombro bajo, ≤ 0,35 m, que no bloquea).
TBD_VARIANTE = 4 · variante + estado (0..19).
"""
import math, os
from montaje import Montaje
from restos import _h

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 20
VARIANTE, ESTADO = VAR // 4, VAR % 4
NOMBRES_V = ["Recto", "Talud", "Irregular", "Tablon", "Red"]
NOMBRES_E = ["Sano", "Danado", "MuyDanado", "Brecha"]
PIEZAS = ["sacos", "claro", "roto", "arena", "tablon", "red"]
COLORES = {"sacos": "605c48", "claro": "847c34", "roto": "48403c", "arena": "b4a080", "tablon": "a86c50",
           "red": "50541c"}
FACCION = "propio"
PRESUPUESTO = {0: (20, 6000), 1: (10, 2000)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
LARGO, GRUESO, H, LB = 8.0, 1.0, 0.3, 0.85


def _filas():
    """Hiladas de la variante: lista de (x0, x1) por hilada, de abajo arriba."""
    if VARIANTE == 1:
        return [(-LARGO / 2 + 0.5 * f, LARGO / 2 - 0.5 * f) for f in range(5)]
    if VARIANTE == 2:
        return [(-LARGO / 2, LARGO / 2)] * 6
    return [(-LARGO / 2, LARGO / 2)] * 4


def _alto_v2(x):
    """v2: número de hiladas en x (3 a 6, en escalones irregulares de unos 1,3 m)."""
    perfil = [(-4.0, 4), (-3.0, 5), (-1.7, 6), (-0.9, 4), (0.3, 3), (1.1, 5), (2.4, 6), (3.3, 4)]
    n = 4
    for x0, k in perfil:
        if x >= x0:
            n = k
    return n


def _celdas():
    """Sacos del tramo sano: (fila, x0, x1, lado, pieza)."""
    cs = []
    for f, (a, b) in enumerate(_filas()):
        off = LB / 2 if f % 2 else 0.0
        x = a + off
        tramos = [(a, a + off)] if off else []
        while x + LB <= b + 1e-6:
            tramos.append((x, x + LB))
            x += LB
        if x < b - 0.05:
            tramos.append((x, b))
        for x0, x1 in tramos:
            xc = (x0 + x1) / 2
            if VARIANTE == 2:
                if f >= _alto_v2(xc):
                    continue
                if f == 3 and (abs(xc + 2.5) < 0.3 or abs(xc - 1.75) < 0.3):
                    continue                                         # aspilleras en la cuarta hilada
            for lado in (-1, 1):
                pieza = "claro" if VARIANTE == 2 and _h((xc, f, lado), 21) < 0.22 else "sacos"
                cs.append((f, x0, x1, lado, pieza))
    return cs


def _danar(cs):
    """Aplica el estado: devuelve (sacos que quedan, sacos caídos [(x, fila)], sacos reventados en su sitio)."""
    if ESTADO == 0:
        return cs, [], []
    nf = max(c[0] for c in cs) + 1
    quedan, caidos, rotos = [], [], []
    for c in cs:
        f, x0, x1, lado, p = c
        xc = (x0 + x1) / 2
        r = _h((xc, f, lado), 3)
        if ESTADO == 1:      # dañado: dos muescas en lo alto y algún saco reventado
            arriba = f >= nf - 2
            muesca = (-2.7 < xc < -1.2 and f >= nf - 2) or (1.0 < xc < 2.2 and f >= nf - 1) or (
                1.0 < xc < 1.6 and f == nf - 2 and lado < 0)
            if muesca or (arriba and r < 0.1):
                caidos.append((xc, f))
                continue
            if r > 0.93 and f < nf - 1:
                rotos.append(c)
                continue
        elif ESTADO == 2:    # muy dañado: hundido a media altura
            mitad = max(1, nf // 2)
            if f >= mitad or (f == mitad - 1 and r < 0.45):
                if r < 0.6:
                    caidos.append((xc, f))
                continue
            if r > 0.85:
                rotos.append(c)
                continue
        else:                # brecha: solo restos de la hilada de abajo
            if f > 0 or r < 0.55:
                if r < 0.5:
                    caidos.append((xc, f))
                continue
            rotos.append(c)
            continue
        quedan.append(c)
    return quedan, caidos, rotos


def construir(nombre="SM_MuroSacos", lod=0):
    m = Montaje(PIEZAS, ["muro"])
    fino = lod == 0
    quedan, caidos, rotos = _danar(_celdas())
    if fino:
        for f, x0, x1, lado, p in quedan:
            m.caja(((x0 + x1) / 2, lado * GRUESO / 4, f * H + H / 2), (x1 - x0 - 0.04, GRUESO / 2 - 0.03, H - 0.03), p,
                   rot_z=(_h((x0, f, lado), 9) - 0.5) * 0.06)
    else:                    # LOD1: los sacos seguidos de cada hilada (los dos lados) se funden en bloques
        filas = {}
        for c in quedan:
            filas.setdefault(c[0], []).append(c)
        for f, cs in filas.items():
            por_x = {}
            for _, x0, x1, lado, p in cs:
                por_x.setdefault((round(x0, 3), round(x1, 3)), {})[lado] = p
            tramo = None
            for (x0, x1) in sorted(por_x) + [(99, 99)]:
                d = por_x.get((x0, x1), {})
                entero = len(d) == 2
                if entero and tramo and tramo[1] >= x0 - 1e-3:
                    tramo = (tramo[0], x1)
                    continue
                if tramo:
                    m.caja(((tramo[0] + tramo[1]) / 2, 0, f * H + H / 2), (tramo[1] - tramo[0] - 0.04, GRUESO - 0.04,
                                                                          H - 0.03), "sacos")
                    tramo = None
                if x0 > 90:
                    break
                if entero:
                    tramo = (x0, x1)
                elif d:
                    lado = next(iter(d))
                    m.caja(((x0 + x1) / 2, lado * GRUESO / 4, f * H + H / 2), (x1 - x0 - 0.04, GRUESO / 2 - 0.03,
                                                                               H - 0.03), "sacos")
        if VARIANTE == 2:    # los sacos claros, encima de su bloque para que se lean a tamaño de juego
            for f, x0, x1, lado, p in quedan:
                if p == "claro":
                    m.caja(((x0 + x1) / 2, lado * GRUESO / 4 + lado * 0.02, f * H + H / 2), (x1 - x0 - 0.02,
                                                                                         GRUESO / 2, H - 0.01), "claro")
    # sacos reventados en su sitio: más bajos y hundidos, en 48403c
    for f, x0, x1, lado, p in rotos:
        if fino or ESTADO == 3:
            m.caja(((x0 + x1) / 2, lado * GRUESO / 4, f * H + H * 0.3), (x1 - x0 - 0.02, GRUESO / 2 + 0.06, H * 0.6),
                   "roto", rot_z=(_h((x0, f, 0), 8) - 0.5) * 0.3)
    # sacos caídos: hacia fuera (−Y) casi todos; en la brecha, desparramados a los dos lados y bajos
    n_max = (22 if fino else 9) if ESTADO >= 2 else (10 if fino else 5)
    n_max = max(2, round(n_max * min(1.0, LARGO / 8.0)))       # piezas cortas (esquina y remate): menos sacos caídos
    for i, (x, f) in enumerate(caidos[:n_max]):
        r = _h((x, f, i), 5)
        fuera = r < 0.75 or ESTADO == 3 and r < 0.5
        y = -(GRUESO / 2 + 0.3 + 1.2 * _h((x, i, 0), 6)) if fuera else GRUESO / 2 + 0.3 + 0.8 * _h((x, i, 0), 7)
        z = 0.13 + (0.26 if (i % 3 == 0 and ESTADO < 3) else 0.0)
        p = "roto" if r > 0.6 else ("claro" if VARIANTE == 2 and r < 0.15 else "sacos")
        m.caja((x + (r - 0.5) * 0.7, y, z), (0.8, 0.46, 0.26), p, rot_z=(r - 0.5) * 2.6)
    # arena derramada (cuñas bajas)
    montones = {1: [(-1.9, -0.85, 0.8, 0.4, 0.12)],                 # poca arena: la muestra la pinta como regueros
                2: [(-1.5, -0.95, 1.2, 0.5, 0.18), (1.6, -0.9, 0.9, 0.45, 0.14)],
                3: [(-2.0, -0.8, 1.3, 0.55, 0.14), (1.3, -0.6, 1.1, 0.5, 0.16), (0.1, 0.75, 0.8, 0.4, 0.1)]}
    for x, y, sx, sy, sz in montones.get(ESTADO, []):
        m.caja((x, y, sz / 2), (sx, sy, sz), "arena", rot_z=0.15)
        mn = 0.0 if fino else 0.36                                  # LOD1: ≥ 2 px (Lucía 10:10)
        m.caja((x + 0.1, y, sz + 0.05), (max(sx * 0.6, mn), max(sy * 0.55, mn), 0.1), "arena", rot_z=0.3)
    # tablón de la v3 (a86c50, 0,25 × 0,06 m, de punta a punta y algo volado hacia fuera)
    if VARIANTE == 3:
        zt = 4 * H + 0.03
        k = LARGO / 8.0                                          # a escala en las piezas cortas (en el tramo, k = 1)
        if ESTADO == 0:
            m.caja((0, -0.1, zt), (8.2 * k, 0.3, 0.06), "tablon")
        elif ESTADO == 1:    # se ha caído por un extremo en la muesca
            m.caja((-2.0 * k, -0.1, zt), (4.2 * k, 0.3, 0.06), "tablon")
            m.barra((0.2 * k, -0.15, zt), (4.1 * k, -0.2, 3 * H + 0.03), 0.3, 0.06, "tablon")
        elif ESTADO == 2:    # sobre los escombros, cruzado
            m.barra((-3.0 * k, -1.4, 0.35), (2.0 * k, -0.2, 0.65), 0.3, 0.06, "tablon")
        else:                # partido en dos en el suelo
            m.caja((-1.8 * k, -1.3, 0.05), (3.4 * k, 0.3, 0.06), "tablon", rot_z=0.25)
            m.caja((2.2 * k, -0.9, 0.05), (2.6 * k, 0.3, 0.06), "tablon", rot_z=-0.4)
    # red de camuflaje de la v4 (303424): por encima y colgando por la cara de fuera, con el borde irregular
    if VARIANTE == 4:
        alto = {0: 4, 1: 4, 2: 2, 3: 0}[ESTADO] * H
        if ESTADO < 3:
            n = 8                                                # también en LOD1: si no, las muescas no se ven
            for k in range(n):
                x0 = -LARGO / 2 + k * LARGO / n
                xc = x0 + LARGO / n / 2
                if ESTADO == 1 and (-2.7 < xc < -1.2 or 1.0 < xc < 2.2):
                    continue                                         # la red se ha ido con la muesca
                if ESTADO == 2 and _h((k, 5, 0), 34) < 0.55:
                    continue                                         # hundido: solo quedan jirones
                cuelga = 0.45 + 0.4 * _h((k, 1, 0), 30)
                m.caja((xc, 0, alto + 0.02), (LARGO / n - 0.02, GRUESO + 0.06, 0.04), "red")
                m.caja((xc, -GRUESO / 2 - 0.03, alto - cuelga / 2), (LARGO / n - 0.02, 0.04, cuelga), "red")
        if ESTADO >= 1:      # jirones de red en el suelo
            for k in range(2 if ESTADO == 1 else 4):
                m.caja((-3 + 2.0 * k + _h((k, 2, 0), 31), -1.2 - 0.5 * _h((k, 3, 0), 32), 0.03),
                       (1.1, 0.7, 0.05), "red", rot_z=_h((k, 4, 0), 33) * 2)
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector, Matrix
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    o.data.transform(Matrix.Translation(base))               # origen en el suelo, en el centro del tramo
    if "tbd_base" in o:
        del o["tbd_base"]
    return [o]
