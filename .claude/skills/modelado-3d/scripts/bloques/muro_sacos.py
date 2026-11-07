"""Estados de daño del muro de sacos del recinto (coordinador, 7-nov 02:03). Teo, 7-nov.

El muro actual (main, PrototypeBootstrap.WallVisual con Obras.HeightMm(Sacos)): una caja por pieza de la red, de
1,2 m de alto y 1,0 m de grueso (Level.WallHalf × 0,5 de semigrosor), color 88885c, a lo largo de su segmento A→B y con
el origen en el suelo, en el centro del segmento. Aquí va un TRAMO MODULAR de 4,0 m (x −2..2) con ese mismo alto,
grueso y origen, para repetirlo a lo largo del segmento (n = round(L / 4) tramos escalados en X a L / n) y cambiar un
tramo por otro según la vida del muro. Fuera = −Y (el sur, por donde llega la horda): los derrumbes caen hacia allí.
Sacos de 0,85 × 0,48 × 0,3 m en 4 hiladas trabadas, dos a lo ancho (LOD0); en el LOD1 los sacos que quedan de cada
hilada se funden en bloques. Todo DE MEMORIA (K3_control_1 de Paula para la forma de los sacos): SIN VERIFICAR.
Colores: sacos 88885c, sacos nuevos de la reparación y arena derramada b4a080, andamio 6c7874 con tablones 605c48,
ingenieros 50541c con el casco 70784c, el chaleco 6c7874 y la cara a86c50.
Variantes (TBD_VARIANTE % 5) = estados: 0 sano; 1 dañado (una muesca en la hilada de arriba y sacos caídos fuera); 2 muy dañado
(media hilada de arriba fuera, mordiscos y arena derramada); 3 brecha (un hueco de 1,8 m casi hasta el suelo con los
sacos y la arena desparramados); 4 en reparación (la brecha a medio rellenar con sacos nuevos, andamio de tubo encima,
un palé de sacos y la cuadrilla de tres ingenieros).
"""
import math, os
from montaje import Montaje
from restos import _h

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = ["sacos", "nuevo", "arena", "tubo", "tablon", "uniforme", "casco", "chaleco", "cara", "pale"]
COLORES = {"sacos": "88885c", "nuevo": "b4a080", "arena": "b4a080", "tubo": "6c7874", "tablon": "605c48",
           "uniforme": "50541c", "casco": "70784c", "chaleco": "6c7874", "cara": "a86c50", "pale": "605c48"}
FACCION = "propio"
PRESUPUESTO = {0: (100, 5000), 1: (40, 1500)}     # el tramo sano del LOD1 son 4 cajas
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
LARGO, ALTO, GRUESO = 4.0, 1.2, 1.0
FILAS, H, LB = 4, 0.3, 0.85


def _sacos_fila(f):
    """Posiciones x de los sacos de la hilada f (trabados: las impares desplazadas medio saco)."""
    off = LB / 2 if f % 2 else 0.0
    xs = []
    x = -LARGO / 2 + off
    if off:
        xs.append((-LARGO / 2, -LARGO / 2 + off))               # medio saco en la punta
    while x + LB <= LARGO / 2 + 1e-6:
        xs.append((x, x + LB))
        x += LB
    if x < LARGO / 2 - 0.05:
        xs.append((x, LARGO / 2))
    return xs


def _queda(a, f, x0, x1, lado):
    """¿Sigue en su sitio el saco de la hilada f entre x0 y x1, en el lado (−1 fuera, +1 dentro)?"""
    xc = (x0 + x1) / 2
    r = _h((xc, f, lado), 3)
    if a == 0:
        return True
    if a == 1:
        if f == FILAS - 1 and -1.1 < xc < 0.5:               # muesca de ~1,6 m en la hilada de arriba (Lucía: ≥ 2 px)
            return False
        if f == FILAS - 2 and -0.6 < xc < 0.1 and lado < 0:  # y medio saco de la de debajo, hacia fuera
            return False
        return not (f == FILAS - 1 and r < 0.22)
    if a == 2:
        if f == FILAS - 1:
            return r > 0.65 and lado > 0
        if f == FILAS - 2:
            return r > 0.3
        return not (abs(xc - 0.6) < 0.7 and f == 1 and lado < 0)
    hueco = 0.9 + 0.25 * f + 0.2 * (r - 0.5)                     # brecha en V, más ancha arriba
    if a in (3, 4):
        return abs(xc) > hueco or f == 0 and abs(xc) > 0.5
    return True


def _figura(m, x, y, rz, fino, agachado=False, saco=False):
    """Ingeniero de bloques (1,75 m): piernas, cuerpo con chaleco, cabeza con casco; agachado o cargando un saco."""
    c, s = math.cos(rz), math.sin(rz)

    def P(dx, dy, z):
        return (x + dx * c - dy * s, y + dx * s + dy * c, z)
    hz = 0.75 if agachado else 1.0
    for dx in (-0.1, 0.1):
        m.caja(P(dx, 0, hz / 2 - 0.05 if not agachado else 0.25), (0.14, 0.16, hz - 0.1 if not agachado else 0.5),
               "uniforme", rot_z=rz)
    m.caja(P(0, 0.05 if agachado else 0, hz + 0.28), (0.42, 0.26, 0.56), "chaleco", rot_z=rz, rot_x=0.4 if agachado else 0)
    m.caja(P(0, -0.02 if not agachado else 0.2, hz + 0.68), (0.2, 0.2, 0.22), "cara", rot_z=rz)
    m.caja(P(0, -0.02 if not agachado else 0.2, hz + 0.83), (0.28, 0.28, 0.12), "casco", rot_z=rz)
    for dx in (-0.27, 0.27):                                  # brazos
        m.caja(P(dx, -0.12, hz + 0.25), (0.11, 0.14 if fino else 0.2, 0.5), "uniforme", rot_z=rz, rot_x=0.6)
    if saco:
        m.caja(P(0, -0.32, hz + 0.25), (0.6, 0.3, 0.26), "nuevo", rot_z=rz)


def construir(nombre="SM_MuroSacos", lod=0):
    m = Montaje(PIEZAS, ["muro"])
    fino = lod == 0
    a = ASPECTO
    caidos = []
    for f in range(FILAS):
        z = f * H + H / 2
        for x0, x1 in _sacos_fila(f):
            for lado in (-1, 1):
                ok = _queda(a, f, x0, x1, lado)
                if a == 4 and not ok and f <= 2 and abs((x0 + x1) / 2) < 1.6:
                    pieza = "nuevo"                           # brecha a medio rellenar: las tres hiladas de abajo
                elif ok:
                    pieza = "sacos"
                else:
                    if f > 0:
                        caidos.append(((x0 + x1) / 2, lado, f))
                    continue
                if fino:
                    m.caja(((x0 + x1) / 2, lado * GRUESO / 4, z), (x1 - x0 - 0.04, GRUESO / 2 - 0.03, H - 0.03), pieza,
                           rot_z=(_h((x0, f, lado), 9) - 0.5) * 0.06)
        if not fino:                                          # LOD1: tramos seguidos de sacos fundidos por hilada
            for pieza in ("sacos", "nuevo"):
                tramo = None
                for x0, x1 in _sacos_fila(f) + [(9, 9)]:
                    oks = []
                    for lado in (-1, 1):
                        ok = _queda(a, f, x0, x1, lado)
                        p = "sacos" if ok else ("nuevo" if a == 4 and f <= 2 and abs((x0 + x1) / 2) < 1.6 else None)
                        oks.append(p == pieza)
                    if x0 < 9 and all(oks):
                        tramo = (tramo[0] if tramo else x0, x1)
                        continue
                    if tramo:
                        m.caja(((tramo[0] + tramo[1]) / 2, 0, z), (tramo[1] - tramo[0] - 0.04, GRUESO - 0.04, H - 0.03),
                               pieza)
                        tramo = None
                    if x0 < 9 and any(oks):                   # solo un lado: medio grueso
                        lado = -1 if oks[0] else 1
                        m.caja(((x0 + x1) / 2, lado * GRUESO / 4, z), (x1 - x0 - 0.04, GRUESO / 2 - 0.03, H - 0.03),
                               pieza)
    # ---- sacos caídos (hacia fuera, −Y, casi todos) y arena derramada
    for i, (x, lado, f) in enumerate(caidos[: (14 if fino else 6)]):
        r = _h((x, f, i), 5)
        y = -GRUESO / 2 - 0.35 - 0.9 * r if r < 0.8 or a >= 3 else GRUESO / 2 + 0.4
        m.caja((x + (r - 0.5) * 0.6, y, 0.14 + 0.28 * (i % 2) * (a >= 3)), (0.82, 0.46, 0.26), "sacos",
               rot_z=(r - 0.5) * 2.4)
    if a >= 2:                                                # montones de arena (cuñas bajas)
        montones = [(0.6, -1.0, 1.6, 0.8, 0.25)] if a == 2 else [(-0.2, -1.2, 2.6, 1.3, 0.35), (0.3, 0.9, 1.8, 0.8, 0.22)]
        for x, y, sx, sy, sz in montones:
            m.caja((x, y, sz / 2), (sx, sy, sz), "arena", rot_z=0.2)
            m.caja((x, y, sz + 0.06), (sx * 0.6, sy * 0.55, 0.12), "arena", rot_z=0.35)
    # ---- reparación: andamio sobre la brecha, palé de sacos y la cuadrilla
    if a == 4:
        t = 0.08 if fino else 0.2                             # tubos de 2 px o más en el LOD1 (Lucía)
        for x in (-1.3, 1.3):
            for y in (-0.85, 0.85):
                m.caja((x, y, 1.1), (t, t, 2.2), "tubo")
        for y in (-0.85, 0.85):
            m.caja((0, y, 2.15), (2.7, t, t), "tubo")
            if fino:
                m.barra((-1.3, y, 0.2), (1.3, y, 2.1), t, t, "tubo")
        for x in (-1.3, 1.3):
            m.caja((x, 0, 2.15), (t, 1.8, t), "tubo")
        for y in (-0.55, 0.55):                               # dos tablones (una plataforma entera tapaba la brecha)
            m.caja((0, y, 2.26), (2.6, 0.3, 0.06), "tablon")
        m.caja((2.9, 1.6, 0.07), (1.2, 1.0, 0.12), "pale")    # palé con sacos nuevos, dentro
        for k in range(4 if fino else 2):
            m.caja((2.9 + (k % 2 - 0.5) * 0.45, 1.6, 0.27 + 0.27 * (k // 2)), (0.42, 0.85, 0.25), "nuevo")
        _figura(m, -0.5, 1.4, 0.3, fino, agachado=True)                     # colocando sacos
        _figura(m, 2.0, 1.9, 2.2, fino, saco=True)                          # trayendo un saco del palé
        _figura(m, -2.0, 1.5, 1.2, fino, saco=True)                         # pasando otro saco
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector, Matrix
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    o.data.transform(Matrix.Translation(base))               # origen en el suelo, en el centro del tramo (como el muro)
    if "tbd_base" in o:
        del o["tbd_base"]
    return [o]
