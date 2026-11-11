"""Kit de vía modular para trazar (Hito 1 del núcleo v2; encargo de Lucía del 11-11, docs/hito1-reparto.md). Teo (4), 11-11.

La red la traza el jugador; este kit es lo que se dibuja encima. Todas las piezas empiezan en el ORIGEN (conector A), en el eje de
la vía y en el suelo, y salen hacia +X. Las curvas giran a la izquierda (+Y); la curva a la derecha es la misma pieza recorrida al
revés (girada 180° y puesta en su conector B). El desvío sí tiene izquierda y derecha. Las coordenadas de cada conector B están en
CONECTORES y en el LEEME.

Ancho ibérico de 1,668 m. Banqueta de balasto de 3,6 m abajo y 2,8 m arriba, de 0,35 m de alto. Traviesas de 2,6 m cada 0,6 m
(1,2 m en el LOD1). Carril de 0,07 × 0,17 m (0,2 m de ancho en el LOD1, para que se lea a 64 px). Cabeza del carril a 0,72 m.
Tres radios con el mismo largo de arco (15,71 m): R 60 m a 15°, R 120 m a 7,5° y R 240 m a 3,75°. Así 6, 12 o 24 piezas cierran
90°. El desvío sale con R 120 a 7,5° (se separa 1,03 m). Desvío + Recta10 + 2 Recta2 (15 m a 7,5°, +1,96 m) + curva R 120
al revés (+1,03 m) dejan dos vías paralelas a 4,01 m entre ejes.
MEDIDAS DE VÍA TÍPICAS DE ADIF, DE MEMORIA (SIN VERIFICAR).
Colores: balasto 88885c, traviesa de hormigón b8b8b8, de madera 845c28, carril 48403c, metal 6c7874, topera f0dc9c, chapa 94948c,
maleza 70784c y 847c34, tierra b4a080. Nada en la banda ni rojo ni ámbar.
TBD_VARIANTE = 5 · pieza + variante.
Piezas: 0 Recta10, 1 Recta2 (2,5 m, para ajustar), 2 CurvaR60, 3 CurvaR120, 4 CurvaR240, 5 DesvioIzq, 6 DesvioDer, 7 Topera
(10 m con la topera al final), 8 Cruce (dos vías a 90°, de 10 m cada una, cruzando en x = 5).
Variantes: 0 Hormigon (nueva); 1 Madera (traviesas viejas de madera); 2 Maleza (entre las traviesas y en la banqueta); 3 Obra (en
construcción: balasto y traviesas sin carriles, con los carriles al lado); 4 Danada (un carril levantado, traviesas sueltas).
"""
import math, os
from taller import Taller
from restos import _h

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 45
PIEZA, VARIANTE = VAR // 5, VAR % 5
NOMBRES_P = ["Recta10", "Recta2", "CurvaR60", "CurvaR120", "CurvaR240", "DesvioIzq", "DesvioDer", "Topera", "Cruce"]
NOMBRES_V = ["Hormigon", "Madera", "Maleza", "Obra", "Danada"]
PIEZAS = ["balasto", "traviesa", "madera", "carril", "metal", "topera", "chapa", "maleza", "maleza_c", "tierra"]
COLORES = {"balasto": "88885c", "traviesa": "b8b8b8", "madera": "845c28", "carril": "48403c", "metal": "6c7874",
           "topera": "f0dc9c", "chapa": "94948c", "maleza": "70784c", "maleza_c": "847c34", "tierra": "b4a080"}
FACCION = "neutro"
NORMALES_ARRIBA = ["balasto", "maleza", "maleza_c", "tierra"]
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 4000), 1: (10, 1600)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
ANCHO, H_BAL, H_TRAV, H_CARRIL = 1.668, 0.35, 0.2, 0.17
ARCO = 15.708
RADIOS = {2: 60.0, 3: 120.0, 4: 240.0}


def _conector_b(p):
    if p == 0:
        return (10.0, 0.0, 0.0)
    if p == 1:
        return (2.5, 0.0, 0.0)
    if p in RADIOS:
        R = RADIOS[p]
        a = ARCO / R
        return (round(R * math.sin(a), 4), round(R * (1 - math.cos(a)), 4), round(math.degrees(a), 4))
    if p in (5, 6):
        a = ARCO / 120.0
        s = 1 if p == 5 else -1
        return (round(120 * math.sin(a), 4), 0.0, 0.0, round(120 * math.sin(a), 4), round(s * 120 * (1 - math.cos(a)), 4),
                round(s * math.degrees(a), 4))
    if p == 7:
        return None
    return (10.0, 0.0, 0.0, 5.0, 5.0, 90.0)


CONECTORES = {NOMBRES_P[p]: _conector_b(p) for p in range(9)}


def _recta(L, x0=0.0, y0=0.0, h=0.0):
    n = max(2, int(math.ceil(L / 2.5)) + 1)
    return [(x0 + math.cos(h) * L * k / (n - 1), y0 + math.sin(h) * L * k / (n - 1), h) for k in range(n)]


def _curva(R, ang, signo=1, pasos=None):
    n = pasos or max(4, int(ang * R / 1.3) + 1)
    out = []
    for k in range(n + 1):
        a = ang * k / n
        out.append((R * math.sin(a), signo * R * (1 - math.cos(a)), signo * a))
    return out


def _prisma(t, camino, seccion, pieza, dz=0.0, desde=0.0, hasta=None):
    """Prisma continuo a lo largo de camino [(x, y, rumbo)], con una sección [(lateral, z)] (convexa, antihoraria vista de
    frente). desde/hasta recortan el principio y el final (en puntos del camino)."""
    pts = camino[int(desde):(None if hasta is None else int(hasta))]
    anillos = []
    for x, y, h in pts:
        nx, ny = -math.sin(h), math.cos(h)
        anillos.append([t.bm.verts.new((x + nx * l, y + ny * l, z + dz)) for l, z in seccion])
    caras = []
    n = len(seccion)
    for a, b in zip(anillos, anillos[1:]):
        for i in range(n):
            caras.append(t.bm.faces.new((a[i], b[i], b[(i + 1) % n], a[(i + 1) % n])))
    caras.append(t.bm.faces.new(anillos[0][::-1]))
    caras.append(t.bm.faces.new(anillos[-1]))
    import bmesh
    bmesh.ops.recalc_face_normals(t.bm, faces=caras)
    t._marcar(caras, pieza)


def _desplazar(camino, d):
    return [(x - math.sin(h) * d, y + math.cos(h) * d, h) for x, y, h in camino]


def _largo(camino):
    return sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(camino, camino[1:]))


def _punto(camino, s):
    """Punto a la distancia s a lo largo del camino."""
    acc = 0.0
    for a, b in zip(camino, camino[1:]):
        d = math.hypot(b[0] - a[0], b[1] - a[1])
        if acc + d >= s or b is camino[-1]:
            f = 0.0 if d == 0 else min(1.0, (s - acc) / d)
            return (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f)
        acc += d
    return camino[-1]


def _balasto(t, camino, ancho_extra=0.0, dz=0.0):
    a, b = 1.8 + ancho_extra, 1.4 + ancho_extra
    _prisma(t, camino, [(-a, 0.0), (a, 0.0), (b, H_BAL), (-b, H_BAL)], "balasto", dz=dz)


def _traviesas(t, camino, fino, largo_fn=None, centro_fn=None, quitar=None, dz=0.0):
    L = _largo(camino)
    paso = 0.6 if fino else 1.2
    n = int(L / paso)
    pieza = "madera" if VARIANTE == 1 else "traviesa"
    for k in range(n):
        s = paso / 2 + k * paso
        if quitar and quitar(k, s):
            continue
        x, y, h = _punto(camino, s)
        lt = largo_fn(s) if largo_fn else 2.6
        c = centro_fn(s) if centro_fn else 0.0
        rz = h
        if VARIANTE == 4 and _h((k, 1, PIEZA), 3) < 0.15:                 # traviesa suelta, girada
            rz += 0.3 * (_h((k, 2, 0), 4) - 0.5)
        if VARIANTE == 1 and _h((k, 3, PIEZA), 5) < 0.2:                  # madera vieja, algo torcida
            rz += 0.08 * (_h((k, 4, 0), 6) - 0.5)
        t.caja((x - math.sin(h) * c, y + math.cos(h) * c, H_BAL + H_TRAV / 2 + dz),
               (0.26 if fino else 0.42, lt, H_TRAV), pieza, rot_z=rz)


def _carril(t, camino, fino, lado, dz=0.0, desde=0.0, hasta=None):
    w = 0.07 if fino else 0.2
    c = _desplazar(camino, lado)
    z0 = H_BAL + H_TRAV + dz
    _prisma(t, c, [(-w / 2, z0), (w / 2, z0), (w / 2, z0 + H_CARRIL), (-w / 2, z0 + H_CARRIL)], "carril", desde=desde, hasta=hasta)


def _via(t, camino, fino, balasto=True, traviesas=True, carriles=True, dz=0.0):
    if balasto:
        _balasto(t, camino, dz=dz)
    if traviesas:
        _traviesas(t, camino, fino, dz=dz)
    if carriles:
        _carriles(t, camino, fino, dz)


def _carriles(t, camino, fino, dz=0.0):
    if VARIANTE == 3:                                                     # obra: los carriles van tirados al lado
        L = _largo(camino)
        if L > 4:
            for k, lado in enumerate((2.3, 2.7)):
                c = _desplazar(camino, lado)
                w = 0.07 if fino else 0.2
                _prisma(t, c, [(-w / 2, 0.0), (w / 2, 0.0), (w / 2, H_CARRIL), (-w / 2, H_CARRIL)], "carril",
                        desde=1, hasta=len(c) - 1)
        return
    for s in (-1, 1):
        lado = s * ANCHO / 2
        if VARIANTE == 4 and s == 1 and len(camino) > 3:                  # carril levantado en la mitad del tramo
            m = len(camino) // 2
            _carril(t, camino, fino, lado, dz, hasta=m)
            a, b = _punto(camino[m - 1:], 0.0), camino[-1]
            w = 0.07 if fino else 0.2
            x0, y0 = a[0] - math.sin(a[2]) * lado, a[1] + math.cos(a[2]) * lado
            x1, y1 = b[0] - math.sin(b[2]) * (lado + 0.6), b[1] + math.cos(b[2]) * (lado + 0.6)
            L = math.hypot(x1 - x0, y1 - y0)
            t.caja(((x0 + x1) / 2, (y0 + y1) / 2, H_BAL + H_TRAV + 0.45 + dz), (L, w, H_CARRIL), "carril",
                   rot_z=math.atan2(y1 - y0, x1 - x0), rot_y=-0.07)
        else:
            _carril(t, camino, fino, lado, dz)


def _maleza(t, camino, fino, n=None):
    if VARIANTE != 2:
        return
    L = _largo(camino)
    n = n or max(3, int(L / (1.6 if fino else 3.0)))
    for k in range(n):
        s = L * (k + 0.5) / n
        x, y, h = _punto(camino, s)
        d = (_h((k, 5, PIEZA), 7) - 0.5) * 3.4
        tam = 0.6 + 0.6 * _h((k, 6, 0), 8)
        t.caja((x - math.sin(h) * d, y + math.cos(h) * d, H_BAL + 0.12 if abs(d) < 1.4 else 0.2), (tam, tam * 0.8, 0.28),
               "maleza" if k % 2 else "maleza_c", rot_z=h + k)


def _sueltas(t, camino, fino):
    """Traviesas sueltas junto a la vía (dañada) o apiladas (obra)."""
    L = _largo(camino)
    if VARIANTE == 4:
        for k in range(2 if L > 4 else 1):
            x, y, h = _punto(camino, L * (0.3 + 0.4 * k))
            t.caja((x - math.sin(h) * 2.4, y + math.cos(h) * 2.4, 0.11), (2.6, 0.26 if fino else 0.42, 0.2), "traviesa",
                   rot_z=h + 0.4 + k)
        if fino:
            for k in range(3):
                x, y, h = _punto(camino, L * (0.2 + 0.3 * k))
                t.caja((x + math.sin(h) * 2.1, y - math.cos(h) * 2.1, 0.08), (0.5, 0.4, 0.16), "balasto", rot_z=k)
    if VARIANTE == 3 and L > 4:
        x, y, h = _punto(camino, L * 0.5)
        for j in range(3):
            t.caja((x + math.sin(h) * 2.6, y - math.cos(h) * 2.6, 0.11 + 0.21 * j), (2.6, 1.6, 0.2), "traviesa", rot_z=h + 1.5708)
        t.caja((x + math.sin(h) * 2.6, y - math.cos(h) * 2.6, 0.25 * 0 + 0.0 + 0.04), (2.8, 2.0, 0.08), "tierra", rot_z=h)


def _desvio(t, fino, signo):
    R, a = 120.0, ARCO / 120.0
    L = R * math.sin(a)
    recto = _recta(L)
    desv = _curva(R, a, signo)
    # balasto: el de la recta, ensanchado hacia el lado del desvío
    sep = R * (1 - math.cos(a))
    _prisma(t, recto, [(-1.8 if signo > 0 else -1.8 - sep, 0.0), (1.8 + sep if signo > 0 else 1.8, 0.0),
                       (1.4 + sep if signo > 0 else 1.4, H_BAL), (-1.4 if signo > 0 else -1.4 - sep, H_BAL)], "balasto")
    # traviesas largas bajo las dos vías
    def largo(s):
        d = R - math.sqrt(max(0.0, R * R - s * s))                         # separación del eje desviado en s
        return 2.6 + d
    def centro(s):
        d = R - math.sqrt(max(0.0, R * R - s * s))
        return signo * d / 2
    _traviesas(t, recto, fino, largo, centro)
    if VARIANTE != 3:
        for s in (-1, 1):                                                  # carriles de la recta
            _carril(t, recto, fino, s * ANCHO / 2)
        # carriles del desvío: el de fuera entero; el de dentro, desde el corazón (donde se separa del carril recto)
        fuera = signo * ANCHO / 2
        _carril(t, desv, fino, fuera, dz=0.001)
        k0 = int(len(desv) * 0.55)
        _carril(t, desv, fino, -fuera, dz=0.001, desde=k0)
        # agujas y motor del cambio al principio
        w = 0.07 if fino else 0.2
        t.caja((1.6, -signo * 2.2, 0.35), (0.7, 0.5, 0.5), "metal")                     # motor del cambio
        t.caja((1.6, -signo * 1.5, H_BAL + 0.05), (0.12 if fino else 0.2, 1.2, 0.1), "metal")   # varilla
        if fino:
            t.caja((2.8, -signo * 2.2, 0.55), (0.12, 0.12, 1.1), "metal")               # palanca o farol
            t.caja((2.8, -signo * 2.2, 1.12), (0.3, 0.3, 0.3), "topera")
    else:
        _carriles(t, recto, fino)
    _maleza(t, recto, fino)
    _sueltas(t, recto, fino)


def _topera(t, fino):
    via = _recta(10.0)
    _balasto(t, via)
    _traviesas(t, via, fino, quitar=lambda k, s: s > 9.6)
    if VARIANTE != 3:
        for s in (-1, 1):
            _carril(t, via, fino, s * ANCHO / 2)
    else:
        _carriles(t, via, fino)
    z = H_BAL + H_TRAV
    if VARIANTE in (0, 2):                                                  # topera de freno: cajón, cilindros y placa
        col = "topera" if VARIANTE == 0 else "chapa"
        t.caja((9.2, 0, z + 0.55), (1.6, 2.4, 1.1), col)
        for s in (-1, 1):
            t.cilindro((8.2, s * 0.8, z + 1.05), 0.12, 0.9, "X", 8 if fino else 6, "metal")
        t.caja((7.7, 0, z + 1.05), (0.15, 2.6, 0.5), "chapa" if VARIANTE == 0 else "metal")
    elif VARIANTE == 1:                                                     # de carriles doblados con traviesa de madera
        for s in (-1, 1):
            t.caja((9.3, s * ANCHO / 2, z + 0.5), (0.12 if fino else 0.2, 0.12 if fino else 0.2, 1.0), "carril", rot_y=0.5)
        t.caja((9.0, 0, z + 0.85), (0.3, 2.8, 0.3), "madera")
    elif VARIANTE == 3:                                                     # obra: montón de tierra al final
        t.caja((9.3, 0, 0.6), (2.0, 3.4, 1.2), "tierra", rot_x=0.0)
        t.caja((9.1, 0, 1.25), (1.2, 2.4, 0.3), "tierra")
    else:                                                                   # dañada: la topera empujada y girada
        t.caja((9.4, 0.4, z + 0.5), (1.6, 2.4, 1.0), "topera", rot_z=0.35)
        t.caja((8.2, -0.6, z + 0.25), (0.15, 2.4, 0.5), "chapa", rot_z=0.5, rot_x=0.4)
    _maleza(t, via, fino)
    _sueltas(t, via, fino)


def _cruce(t, fino):
    vx = _recta(10.0)
    vy = _recta(10.0, 5.0, -5.0, math.pi / 2)
    _balasto(t, vx)
    _balasto(t, vy, dz=-0.002)
    # traviesas de las dos vías, salvo en el cuadro del cruce (allí, un tablero de madera)
    _traviesas(t, vx, fino, quitar=lambda k, s: abs(s - 5.0) < 1.5)
    _traviesas(t, vy, fino, quitar=lambda k, s: abs(s - 5.0) < 1.5, dz=-0.004)
    t.caja((5.0, 0.0, H_BAL + H_TRAV / 2 - 0.002), (2.9, 2.9, H_TRAV), "madera" if VARIANTE != 0 else "traviesa")
    if VARIANTE == 3:
        _carriles(t, vx, fino)
        return
    for s in (-1, 1):
        _carril(t, vx, fino, s * ANCHO / 2)
        _carril(t, vy, fino, s * ANCHO / 2, dz=-0.003)
    _maleza(t, vx, fino)
    _maleza(t, vy, fino)
    if VARIANTE == 4:
        _sueltas(t, vx, fino)


def construir(nombre="SM_ViaTrazado", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    if PIEZA in (0, 1):
        c = _recta(10.0 if PIEZA == 0 else 2.5)
        _via(t, c, fino)
        _maleza(t, c, fino)
        _sueltas(t, c, fino)
    elif PIEZA in RADIOS:
        R = RADIOS[PIEZA]
        c = _curva(R, ARCO / R)
        _via(t, c, fino)
        _maleza(t, c, fino)
        _sueltas(t, c, fino)
    elif PIEZA in (5, 6):
        _desvio(t, fino, 1 if PIEZA == 5 else -1)
    elif PIEZA == 7:
        _topera(t, fino)
    else:
        _cruce(t, fino)
    return t.objeto(nombre, bisel=0)


def separar(o, lod):
    """Deshace el origen al centro de la base de limpiar(): el origen tiene que quedar en el conector A."""
    from mathutils import Vector, Matrix
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    o.data.transform(Matrix.Translation(base))
    if "tbd_base" in o:
        del o["tbd_base"]
    return [o]
