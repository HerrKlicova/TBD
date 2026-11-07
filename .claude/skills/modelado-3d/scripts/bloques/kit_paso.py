"""Kit de estaciones: paso entre andenes, pasarela por encima o paso inferior (encargo de Álvaro, 7-nov; Teo).

Cruza las vías a lo largo de Y, de un andén a otro. Origen en el centro, entre los dos andenes, a la cota del carril
(z = 0); los andenes están en y = ±luz/2 con la cara de arriba a 0,68 m (kit_anden). luz por defecto 9 m: andén
lateral y central de Castellón (y -4 y -13 en su montaje). Las escaleras bajan a lo largo de X, sobre cada andén (que
es largo en X), para no comerse su ancho de 4 m. Gálibo de la pasarela 6,0 m por encima del carril (catenaria a
~5,3 m). Colores de kit_comun: fundición verde 50541c (70784c en el LOD1) como la marquesina de Sagunto. Medidas
SIN VERIFICAR (no hay plano ni foto de una pasarela de la línea; referencia general de pasarelas de RENFE).
Variantes (TBD_VARIANTE):
  0 paso inferior: en cada andén, la boca de la escalera con su peto, barandilla y un tejadillo de chapa.
  1 pasarela metálica de celosía (la de la línea, con la fundición de Sagunto): dos torres de escalera y tablero.
  2 pasarela de hormigón: petos macizos, pilas cuadradas y escaleras de losa.
  3 pasarela metálica cubierta: la 1 con cubierta de chapa a dos aguas sobre el tablero.
  4 pasarela hundida: el tablero partido en el centro cae sobre las vías; una torre se queda en pie.
"""
import math
from taller import Taller
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual, bandas_chapa)

PRESUPUESTO = {0: (30, 2500), 1: (20, 1400)}
H_ANDEN, GALIBO, ANCHO = 0.68, 6.0, 2.2
R = math.radians


def _barra(t, p0, p1, g, pieza):
    """Barra de sección g entre dos puntos del plano XZ o YZ (una de las coordenadas X o Y igual)."""
    (x0, y0, z0), (x1, y1, z1) = p0, p1
    dx, dy, dz = x1 - x0, y1 - y0, z1 - z0
    c = ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
    if abs(dx) > abs(dy):
        L = math.hypot(dx, dz)
        t.caja(c, (L, g, g), pieza, rot_y=-math.atan2(dz, dx))
    else:
        L = math.hypot(dy, dz)
        t.caja(c, (g, L, g), pieza, rot_x=math.atan2(dz, dy))


def _escalera(t, lod, x_alto, y, z_bajo, z_alto, sentido, pieza, peto=None, ancho=1.6):
    """Tramo recto a lo largo de X: arriba en x_alto (cota z_alto) y baja hacia sentido·X hasta z_bajo, a 36°. Losa
    inclinada, peldaños como bandas en el LOD0 y zancas (o petos macizos si peto) a los lados. Devuelve la x del pie."""
    sube = z_alto - z_bajo
    run = sube / math.tan(R(36))
    x_pie = x_alto + sentido * run
    L = math.hypot(run, sube)
    a = sentido * math.atan2(sube, run)            # rot_y positiva baja el extremo +X (Blender, regla de la mano derecha)
    cx, cz = (x_alto + x_pie) / 2, (z_alto + z_bajo) / 2

    def punto(f):                                  # f = 0 arriba, 1 abajo
        return x_alto + sentido * run * f, z_alto - sube * f
    t.caja((cx, y, cz - 0.12), (L, ancho, 0.2), pieza, rot_y=a)
    if lod == 0:
        n = int(sube / 0.5)
        for k in range(n):
            x, z = punto((k + 0.5) / n)
            t.caja((x, y, z + 0.0), (0.12, ancho - 0.1, 0.04), "losa", rot_y=a)
    for s_ in (-1, 1):
        yy = y + s_ * (ancho / 2 + 0.09)
        if peto:
            t.caja((cx, yy, cz + 0.4), (L, 0.16, 1.1), peto, rot_y=a)
        else:
            t.caja((cx, yy, cz + 0.95), (L, 0.06 if lod == 0 else 0.2, 0.08 if lod == 0 else 0.2), "pilar", rot_y=a)
            if lod == 0:
                for k in range(1, 5):
                    x, z = punto(k / 5)
                    t.caja((x, yy, z + 0.48), (0.05, 0.05, 0.96), "pilar")
    return x_pie


def _torre(t, lod, y, z_top, metal=True):
    """Rellano alto sobre el andén con cuatro pilares y la escalera que baja hacia +X."""
    g = 0.22 if lod == 0 else 0.34
    pieza = "pilar" if metal else "cornisa"
    for x in (-ANCHO / 2 + 0.15, ANCHO / 2 - 0.15):
        for yy in (y - ANCHO / 2 + 0.15, y + ANCHO / 2 - 0.15):
            t.caja((x, yy, (H_ANDEN + z_top) / 2), (g, g, z_top - H_ANDEN), pieza)
    t.caja((0, y, z_top - 0.15), (ANCHO, ANCHO, 0.3), "chapa" if metal else "losa")
    xb = _escalera(t, lod, ANCHO / 2 - 0.05, y, H_ANDEN, z_top, 1, "chapa" if metal else "losa",
                   peto=None if metal else "cornisa")
    t.caja((xb + 0.25, y, H_ANDEN + 0.06), (0.9, 1.7, 0.12), "losa" if metal else "cornisa")     # meseta de arranque


def _tablero_metal(t, lod, y0, y1, z, cubierta=False):
    """Tablero de la pasarela entre y0 e y1 (a lo largo de Y) con las dos celosías de los lados."""
    L, yc = y1 - y0, (y0 + y1) / 2
    t.caja((0, yc, z - 0.15), (ANCHO - 0.004, L + 0.1, 0.3), "chapa")       # entra 5 cm en los rellanos
    h = 1.3
    for s in (-1, 1):
        x = s * (ANCHO / 2 - 0.05)
        if lod == 0:
            t.caja((x, yc, z + h), (0.12, L, 0.12), "pilar")                      # cordón superior
            t.caja((x, yc, z + 0.05), (0.12, L, 0.16), "pilar")                   # cordón inferior
            n = max(2, int(L / 1.5))
            for k in range(n):
                ya, yb = y0 + k * L / n, y0 + (k + 1) * L / n
                _barra(t, (x, ya, z + 0.1), (x, yb, z + h), 0.06, "pilar")
                _barra(t, (x, yb, z + 0.1), (x, ya, z + h), 0.06, "pilar")
        else:   # LOD1: la celosía se lee como un peto calado; cordón de 2 px y panel de chapa oscura
            t.caja((x, yc, z + h), (0.2, L, 0.34), "pilar")
            t.caja((x, yc, z + 0.6), (0.06, L - 0.1, 0.9), "metal")
    if cubierta:
        zc = z + 2.6
        for s in (-1, 1):
            for yy in (y0 + 0.3, yc, y1 - 0.3):
                t.caja((s * (ANCHO / 2 - 0.05), yy, z + 1.3), (0.12 if lod == 0 else 0.2, 0.12 if lod == 0 else 0.2, 2.6),
                       "pilar")
        a = R(18)
        for s in (-1, 1):
            t.caja((s * 0.65, yc, zc + 0.2), (1.5, L + 0.6, 0.06), "chapa", rot_y=s * a)
        if lod == 1:
            for s in (-1, 1):
                t.caja((s * 0.65, yc, zc + 0.24), (0.5, L + 0.5, 0.02), "onda", rot_y=s * a)
        t.caja((0, yc, zc + 0.45), (0.2, L + 0.6, 0.12 if lod == 0 else 0.2), "canalon")   # cumbrera


def _boca(t, lod, y, sentido):
    """Boca de paso inferior sobre el andén en y: hueco de 1,8 x 6 m con peto, la escalera bajando hacia sentido·X
    (se ve oscura), barandilla y tejadillo de chapa."""
    lx, ly = 6.0, 1.8
    z = H_ANDEN
    t.caja((0, y, z + 0.012), (lx, ly, 0.02), "placa")                               # fondo oscuro del hueco
    if lod == 0:
        for k in range(8):                                                           # peldaños que se hunden
            t.caja((sentido * (-lx / 2 + 0.6 + k * 0.6), y, z + 0.03), (0.08, ly - 0.1, 0.02), "losa")
    for s in (-1, 1):
        t.caja((0, y + s * (ly / 2 + 0.1), z + 0.5), (lx + 0.4, 0.2, 1.0), "cornisa")   # petos largos
    t.caja((-sentido * (lx / 2 + 0.1), y, z + 0.5), (0.18, ly + 0.37, 0.98), "cornisa")   # peto del fondo
    # barandilla sobre el peto
    for s in (-1, 1):
        t.caja((0, y + s * (ly / 2 + 0.1), z + 1.15), (lx + 0.4, 0.06 if lod == 0 else 0.2, 0.06 if lod == 0 else 0.2),
               "pilar")
    # tejadillo: dos pilares y chapa a un agua
    for x in (-lx / 2 + 0.3, lx / 2 - 0.3):
        t.caja((x, y + ly / 2 + 0.1, z + 1.6), (0.12 if lod == 0 else 0.2, 0.12 if lod == 0 else 0.2, 2.2), "pilar")
    t.caja((0, y, z + 2.75), (lx + 0.6, ly + 0.8, 0.06), "chapa", rot_x=R(-8))
    if lod == 1:
        bandas_chapa(t, 0, lx + 0.6, y, ly + 0.8, z + 2.77, R(-8))
    t.caja((0, y - (ly + 0.8) / 2, z + 2.6), (lx + 0.6, 0.12, 0.2 if lod == 0 else 0.34), "canalon")
    # cartel de «paso inferior» en el frente (banda oscura para el decal de Bruno)
    t.caja((-sentido * (lx / 2 + 0.1), y, z + 1.4), (0.08, 1.4, 0.5), "placa")


def construir(nombre="SM_KitPaso", lod=0, var=None, luz=9.0):
    v = var_actual(var) % 5
    t = Taller(PIEZAS)
    ya, yb = -luz / 2, luz / 2
    if v == 0:
        _boca(t, lod, ya, 1)
        _boca(t, lod, yb, -1)
    elif v in (1, 3):
        for y in (ya, yb):
            _torre(t, lod, y, GALIBO + 0.3)
        _tablero_metal(t, lod, ya + ANCHO / 2, yb - ANCHO / 2, GALIBO + 0.3, cubierta=(v == 3))
    elif v == 2:
        for y in (ya, yb):
            _torre(t, lod, y, GALIBO + 0.4, metal=False)
        L = yb - ya - ANCHO
        t.caja((0, 0, GALIBO), (ANCHO - 0.004, L + 0.1, 0.8), "losa")                           # tablero de canto 0,8
        for s in (-1, 1):
            t.caja((s * (ANCHO / 2 - 0.08), 0, GALIBO + 0.95), (0.16, L, 1.1), "cornisa")   # petos macizos
            t.caja((s * (ANCHO / 2 - 0.08), 0, GALIBO + 1.52), (0.24, L, 0.06 if lod == 0 else 0.2), "costado")
    else:   # hundida: torre de -Y en pie con medio tablero; la otra mitad cae sobre las vías
        _torre(t, lod, ya, GALIBO + 0.3)
        _tablero_metal(t, lod, ya + ANCHO / 2, -0.6, GALIBO + 0.3)
        # tramo caído: girado sobre su arranque, con un extremo en el suelo entre las vías
        L2 = yb - ANCHO / 2 - 0.2
        a = math.asin(min(0.95, (GALIBO + 0.3 - 0.4) / L2))
        cz = (GALIBO + 0.3 + 0.4) / 2 - 0.3
        cy = 0.2 + L2 * math.cos(a) / 2 - 0.6
        t.caja((0, cy, cz), (ANCHO, L2, 0.3), "chapa", rot_x=a)
        for s in (-1, 1):
            t.caja((s * (ANCHO / 2 - 0.05), cy, cz + 0.6), (0.12 if lod == 0 else 0.2, L2, 0.12 if lod == 0 else 0.34),
                   "pilar", rot_x=a)
        # torre de +Y: muñones de los pilares y la escalera caída en el andén
        g = 0.22 if lod == 0 else 0.34
        for x in (-ANCHO / 2 + 0.15, ANCHO / 2 - 0.15):
            for k, yy in enumerate((yb - ANCHO / 2 + 0.15, yb + ANCHO / 2 - 0.15)):
                hh = 1.6 + 0.9 * k
                t.caja((x, yy, H_ANDEN + hh / 2), (g, g, hh), "pilar")
        t.caja((ANCHO / 2 + 2.4, yb, H_ANDEN + 0.4), (4.8, 1.6, 0.2), "chapa", rot_y=R(-8))
        for (x, y, r) in ((-1.5, 1.8, 20), (1.8, 2.6, -30)):
            t.caja((x, y, 0.25), (1.3, 0.9, 0.5), "escombro", rot_z=R(r))
    return t.objeto(nombre, bisel=0)
