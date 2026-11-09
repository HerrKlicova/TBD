"""Carga visible en los vagones (Lucía, 8-nov 18:28: objetivo «Lleva munición a Nules» y víveres de la D-N176). Teo (3),
8-nov.

Un módulo de carga por recurso que se pone ENCIMA del tablero de la plataforma Rgs (18,5 × 2,9 m a 1,25 m del carril,
vagon_cercano / vagon_tropa_r2). El módulo ocupa 18,2 × 2,7 m y nunca pasa de 1,6 m de alto (gálibo). Origen: el centro
del tablero, a la cota de su cara de arriba (en Unity, hijo del vagón en (0; 1,25; 0)). Largo a lo largo de Y.
La tropa no lleva módulo: es el vagón R2 que ya existe (vagon_cercano v2).

VAR = 5·recurso + estado.
  recurso: 0 Municion (cajas militares de 0,75 × 0,4 × 0,34 m en palés, como el rearme_set), 1 Combustible (bidones de
  200 l en palés de 1,2 × 1,2 m), 2 Materiales (tablones, HESCO plegado y sacos), 3 Viveres (cajas de fruta y sacos de
  harina).
  estado: 0 Lleno, 1 Mitad, 2 Vacio (palés sueltos y cinchas), 3 Danado (faltan palés, otros movidos, alguno quemado y
  bultos caídos en el tablero), 4 Destrozado (la mitad quemada y volcada, bultos caídos a la vía por los dos lados).
Medidas SIN VERIFICAR (genéricas). Colores: palés b4a080, cajas de munición 50541c con la tapa en 88885c, bidones
605c48 con la tapa en 94948c, tablones 845c28, HESCO plegado 6c7874 con el geotextil b4a080, sacos de material 94948c,
cajas de fruta 845c28 con la fruta en f0dc9c (limón: nada naranja, el ámbar es para avisos), sacos de harina b8b8b8,
cinchas 847c34, quemado 605c48. Nada rojo ni en la banda de la horda.
TBD_CON_PLATAFORMA=1 dibuja el tablero debajo (solo para las hojas; el FBX va sin él).
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 20
REC, EST = VAR // 5, VAR % 5
RECURSOS = ["Municion", "Combustible", "Materiales", "Viveres"]
ESTADOS = ["Lleno", "Mitad", "Vacio", "Danado", "Destrozado"]
NOMBRES_V = [f"{r}{e}" for r in RECURSOS for e in ESTADOS]
CON_PLATAFORMA = os.environ.get("TBD_CON_PLATAFORMA", "") == "1"
PIEZAS = ["palet", "caja", "metal", "bidon", "tapa", "tablon", "hesco", "geotextil", "saco", "fruta", "harina",
          "cincha", "quemado", "tablero"]
COLORES = {"palet": "b4a080", "caja": "50541c", "metal": "605c48", "bidon": "605c48", "tapa": "94948c",
           "tablon": "845c28", "hesco": "6c7874", "geotextil": "b4a080", "saco": "94948c", "fruta": "f0dc9c",
           "harina": "b8b8b8", "cincha": "847c34", "quemado": "605c48", "tablero": "605c48"}
VALOR_PIEZAS = {"caja": ("88885c", "50541c", "50541c")}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (60, 9000), 1: (30, 2500)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
LARGO, ANCHO = 18.2, 2.7
ZV = -1.25                                            # el suelo de la vía, respecto al tablero


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


class _C:
    """Coloca piezas de un palé con un giro en Z alrededor del centro del palé, una inclinación y el quemado."""
    def __init__(self, t, fino, x, y, z, rz=0.0, rx=0.0, quemado=False):
        self.t, self.fino, self.x, self.y, self.z, self.rz, self.rx, self.q = t, fino, x, y, z, rz, rx, quemado

    def _p(self, dx, dy, dz):
        c, s = math.cos(self.rz), math.sin(self.rz)
        dy2 = dy * math.cos(self.rx) - dz * math.sin(self.rx)
        dz2 = dy * math.sin(self.rx) + dz * math.cos(self.rx)
        return (self.x + dx * c - dy2 * s, self.y + dx * s + dy2 * c, self.z + dz2)

    def pieza(self, p):
        return "quemado" if self.q and p not in ("metal",) else p

    def caja(self, d, s, p):
        self.t.caja(self._p(*d), s, self.pieza(p), rot_z=self.rz, rot_x=self.rx)

    def cil(self, d, r, h, p, lados):
        if self.rx:                                   # inclinado: el cilindro pasa a caja (nunca se ve de cerca)
            self.caja(d, (2 * r, 2 * r, h), p)
        else:
            self.t.cilindro(self._p(*d), r, h, "Z", lados, self.pieza(p))


# ------------------------------------------------------------------ una carga por palé (altura total devuelta)
def _palet(c, ancho_y=1.0):
    c.caja((0, 0, 0.07), (1.2, ancho_y, 0.14), "palet")
    return 0.145


def _municion(c, capas):
    z = _palet(c)
    if not c.fino:
        if capas:
            c.caja((0, 0, z + capas * 0.345 / 2), (1.18, 0.78, capas * 0.345), "caja")
        return
    for f in range(capas):
        for i in range(3):
            c.caja(((i - 1) * 0.405, 0, z + 0.17 + f * 0.345), (0.4, 0.75, 0.34), "caja")
    if capas:
        for i in (-0.3, 0.3):                         # cinchas
            c.caja((i, 0, z + capas * 0.345 / 2), (0.05, 0.8, capas * 0.345 + 0.02), "cincha")


def _combustible(c, n):
    z = _palet(c, 1.2)
    pos = [(-0.3, -0.3), (0.3, -0.3), (-0.3, 0.3), (0.3, 0.3)][:n]
    if not c.fino:
        if n:
            c.caja((0, -0.3 if n <= 2 else 0, z + 0.44), (1.16, 0.58 if n <= 2 else 1.16, 0.88), "bidon")
            c.caja((0, -0.3 if n <= 2 else 0, z + 0.885), (1.0, 0.45 if n <= 2 else 1.0, 0.02), "tapa")
        return
    for dx, dy in pos:
        c.cil((dx, dy, z + 0.44), 0.285, 0.88, "bidon", 10)
        c.cil((dx, dy, z + 0.885), 0.24, 0.02, "tapa", 8)
    if n == 4:
        c.caja((0, 0, z + 0.6), (1.22, 1.22, 0.05), "cincha")


def _viveres(c, capas, sacos=False):
    z = _palet(c)
    if sacos:                                         # palé de sacos de harina (5 por capa)
        if not c.fino:
            if capas:
                c.caja((0, 0, z + capas * 0.13), (1.15, 0.95, capas * 0.26), "harina")
            return
        for f in range(capas):
            for i in range(2):
                c.caja(((i - 0.5) * 0.58 if f % 2 == 0 else 0, 0 if f % 2 == 0 else (i - 0.5) * 0.48,
                        z + 0.13 + f * 0.26), (0.56, 0.95, 0.25) if f % 2 == 0 else (1.15, 0.46, 0.25), "harina")
        return
    if not c.fino:
        if capas:
            c.caja((0, 0, z + capas * 0.31 / 2), (1.18, 0.98, capas * 0.31), "tablon")
            c.caja((0, 0, z + capas * 0.31 + 0.005), (1.0, 0.8, 0.02), "fruta")
        return
    for f in range(capas):                            # cajas de fruta abiertas de 0,6 × 0,5 × 0,3 m (2 × 2 por capa)
        arriba = f == capas - 1
        for i in range(2):
            for j in range(2):
                d = ((i - 0.5) * 0.6, (j - 0.5) * 0.49, z + 0.15 + f * 0.31)
                c.caja(d, (0.58, 0.47, 0.3), "tablon")
                if arriba:
                    c.caja((d[0], d[1], d[2] + 0.145), (0.5, 0.39, 0.02), "fruta")


def _materiales(c, tipo, capas):
    if tipo == 0:                                     # haz de tablones de 4 m, a lo largo de Y (sin palé)
        for f in range(capas):
            if c.fino:
                for i in range(5):
                    c.caja(((i - 2) * 0.23, 0, 0.11 + 0.08 + f * 0.17), (0.22, 4.0, 0.16), "tablon")
            else:
                c.caja((0, 0, 0.11 + 0.08 + f * 0.17), (1.14, 4.0, 0.16), "tablon")
        for y in (-1.6, 0, 1.6):                      # rastreles
            c.caja((0, y, 0.055), (1.2, 0.1, 0.1), "tablon")
        return
    z = _palet(c)
    if tipo == 1:                                     # HESCO plegado: paquetes planos de malla con geotextil
        if capas:
            c.caja((0, 0, z + capas * 0.11), (1.1, 0.95, capas * 0.22), "hesco")
            c.caja((0, 0, z + capas * 0.22 + 0.01), (1.0, 0.85, 0.02), "geotextil")
            if c.fino:
                for f in range(1, capas):
                    c.caja((0, 0, z + f * 0.22), (1.12, 0.97, 0.03), "geotextil")
        return
    for f in range(capas):                            # sacos de material
        if c.fino:
            for i in range(2):
                c.caja(((i - 0.5) * 0.58, 0, z + 0.11 + f * 0.22), (0.56, 0.9, 0.21), "saco")
        else:
            c.caja((0, 0, z + capas * 0.11), (1.16, 0.9, capas * 0.22), "saco")
            break


# ------------------------------------------------------------------ reparto en el tablero
def _huecos():
    """Huecos de palé: 2 columnas (x ±0,66) × 14 filas cada 1,3 m."""
    return [(x, -LARGO / 2 + 0.65 + k * 1.3, k) for k in range(14) for x in (-0.66, 0.66)]


def construir(nombre="SM_CargaVagon", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    r, e = REC, EST
    if CON_PLATAFORMA:
        t.caja((0, 0, -0.16), (2.9, 18.5, 0.3), "tablero")
    huecos = _huecos()
    if r == 2:                                        # materiales: tablones (4 haces), HESCO (6 palés) y sacos (8 palés)
        huecos = ([(x, -LARGO / 2 + 2.05 + k * 4.1, ("tab", k)) for k in range(2) for x in (-0.66, 0.66)] +
                  [(x, -0.2 + k * 1.3, ("hes", k)) for k in range(3) for x in (-0.66, 0.66)] +
                  [(x, 4.0 + k * 1.3, ("sac", k)) for k in range(4) for x in (-0.66, 0.66)])
    caidos = []
    for n, (x, y, k) in enumerate(huecos):
        fila = k if isinstance(k, int) else k[1]
        # qué queda en cada estado
        if e == 1 and n % 2 == 1 and _h(n, 9) > 0.15:  # mitad: falta uno de cada dos palés (casi siempre)
            continue
        if e == 2 and n not in (1, 6, 11, 20):
            continue
        rz = rx = 0.0
        q = False
        if e == 3:
            if _h(n, 1) < 0.3:
                if _h(n, 2) < 0.4:
                    caidos.append((x, y, n, False))
                continue
            rz = (_h(n, 3) - 0.5) * 0.5
            q = _h(n, 4) < 0.2
        if e == 4:
            if _h(n, 1) < 0.5:
                caidos.append((x, y, n, True))
                continue
            rz = (_h(n, 3) - 0.5) * 0.9
            rx = (_h(n, 5) - 0.5) * 0.25
            q = _h(n, 4) < 0.6
        c = _C(t, fino, x, y, 0.005, rz, rx, q)
        vacio = e == 2 and n != 6                    # en el vacío queda un resto en un palé
        resto = e == 2 and n == 6
        if r == 0:
            _municion(c, 0 if vacio else 1 if resto else (3 if e != 1 or fila < 7 else 2))
        elif r == 1:
            _combustible(c, 0 if vacio else 1 if resto else 4 if e != 1 else (4 if fila < 7 else 2))
        elif r == 3:
            _viveres(c, 0 if vacio else 1 if resto else (4 if e != 1 else 3), sacos=fila >= 10)
        else:
            tipo = {"tab": 0, "hes": 1, "sac": 2}[k[0]]
            if vacio and tipo == 0:
                _materiales(c, 0, 1)
            else:
                _materiales(c, tipo, 0 if vacio else 1 if resto else (4 if e != 1 else 2))
    if e == 2 and fino:                               # cinchas sueltas por el tablero
        for k in range(4):
            t.caja(((_h(k, 6) - 0.5) * 2.0, -7 + k * 4.3, 0.01), (0.05, 2.2, 0.01), "cincha", rot_z=_h(k, 7) * 2)
    # bultos caídos: en el tablero (dañado) o en la vía a los dos lados (destrozado)
    for i, (x, y, n, a_via) in enumerate(caidos[:8 if fino else 5]):
        s = 1 if x > 0 else -1
        if a_via:
            cx, cz = s * (2.3 + _h(n, 8) * 1.4), ZV
        else:
            cx, cz = x + s * 0.15, 0.0
        rz = _h(n, 10) * 3
        q = a_via and _h(n, 11) < 0.6
        c = _C(t, fino, cx, y + (_h(n, 12) - 0.5), cz + 0.005, rz, 0.0, q)
        if r == 0:
            c.caja((0, 0, 0.17), (0.75, 0.4, 0.34), "caja")
            c.caja((0.5, 0.45, 0.17), (0.75, 0.4, 0.34), "caja")
        elif r == 1:
            c.caja((0, 0, 0.29), (0.88, 0.58, 0.58), "bidon")        # bidón tumbado (caja, para no gastar)
            if fino:
                c.cil((0.7, 0.4, 0.44), 0.285, 0.88, "bidon", 10)
        elif r == 2:
            c.caja((0, 0, 0.11), (0.6, 0.9, 0.21), "saco")
            c.caja((0.55, 0.6, 0.1), (0.5, 1.8, 0.2), "tablon")
        else:
            c.caja((0, 0, 0.15), (0.58, 0.47, 0.3), "tablon")
            c.caja((0.55, 0.3, 0.125), (0.56, 0.95, 0.25), "harina")
    return t.objeto(nombre, bisel=0)
