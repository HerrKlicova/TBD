"""Banqueta de tiro y nido del muro oeste de Sagunto (sagunto-nivel.md §10.11, decisión de Lucía 8-nov 13:08: la franja
de 2 m pegada a la cara interior del HESCO, X 70,5–72,5 y Z 44,5–84,5, y los nidos de la puerta de vía a 1,5 m de la
cara, en X 72,0 y Z 57,5 y 71,5). Kit de defensas (Mak, 8-nov 13:46). Teo (3), 8-nov.

BANQUETA: módulo de 5 m a lo largo del muro (4,98 con holgura), 2 m de fondo y 0,4 m de alto: el escalón que explica por
qué se dispara por encima de los 2,1 m del HESCO (boca a unos 2,0 m sobre el suelo). Coste 0 en la simulación.
NIDO: plataforma de 3 × 2,4 m y 0,6 m de alto con parapeto de sacos de 0,6 m por los lados y por detrás (el frente es
el HESCO) y dos peldaños por detrás.
Colores: palés y tablones 845c28 (con a86c50 una tarima grande se leía salmón), sacos 605c48, cajas de munición 70784c, chapa 94948c, red 50541c, sacos reventados
y escombro 48403c (solo en lo caído). Nada rojo ni ámbar. Medidas SIN VERIFICAR.
Origen: centro de la planta, en el suelo. El lado −Y va contra la cara interior del muro (en el muro oeste, con yaw 270,
−Y mira al oeste); la banqueta ocupa y −1..1 y el nido y −1,2..1,2 (con el origen en X 72,0, el nido deja 0,3 m al muro).
Módulos de 5 m y de 2,5 m (Álvaro, 8-nov 13:51, sagunto-nivel.md §10.14: 4 de 5 m en Z 45–55 y 74–84, 2 de 2,5 m en
Z 59,25–61,75 y 67,25–69,75, y los nidos en Z 56–59 y 70–73 con el lado de 2,4 m perpendicular al muro).
TBD_VARIANTE = 5 · tipo + variante (0..14). Tipos: 0 Banqueta (5 m), 1 Nido, 2 BanquetaCorta (2,5 m).
  Banqueta: 0 palés con una hilada de sacos encima; 1 escalón de sacos; 2 tarima de tablones sobre cajas;
  3 gastada (palés rotos y sacos caídos); 4 palés con cajas de munición y sacos de reserva.
  Nido: 0 palés y sacos; 1 tablones sobre cajas, con parapeto de sacos; 2 con techo de chapa sobre postes;
  3 con red de camuflaje; 4 dañado (parapeto mordido y sacos caídos).
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 15
TIPO, V = VAR // 5, VAR % 5
NOMBRES_T = ["Banqueta", "Nido", "BanquetaCorta"]
PIEZAS = ["pale", "sacos", "caja", "chapa", "red", "roto", "poste"]
COLORES = {"pale": "845c28", "sacos": "605c48", "caja": "70784c", "chapa": "94948c", "red": "50541c", "roto": "48403c",
           "poste": "6c7874"}
FACCION = "propio"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 3000), 1: (10, 1200)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_BanquetaTiro", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    if TIPO == 1:
        _nido(t, fino)
    else:
        _banqueta(t, fino, 4.98 if TIPO == 0 else 2.48)
    return t.objeto(nombre, bisel=0)


def _pales(t, fino, x0, x1, y0, y1, z, alto, rotos=()):
    """Palés de 1,2 × 0,8 m en hilera; en el LOD0 con las tablas de arriba marcadas."""
    nx = max(1, round((x1 - x0) / 1.2))
    ny = max(1, round((y1 - y0) / 1.0))
    lx, ly = (x1 - x0) / nx, (y1 - y0) / ny
    for i in range(nx):
        for j in range(ny):
            k = i * ny + j
            xc, yc = x0 + lx * (i + 0.5), y0 + ly * (j + 0.5)
            if k in rotos:
                t.caja((xc, yc, z + alto * 0.3), (lx - 0.1, ly - 0.1, alto * 0.6), "pale", rot_z=0.15, rot_y=0.1)
                continue
            t.caja((xc, yc, z + alto / 2), (lx - 0.04, ly - 0.04, alto), "pale", rot_z=(_h(k, 1) - 0.5) * 0.04)
            if fino:
                for q in range(3):
                    t.caja((xc, yc - ly / 3 + q * ly / 3, z + alto + 0.005), (lx - 0.1, 0.05, 0.01), "roto")


def _sacos_fila(t, fino, x0, x1, y, z, alto=0.3, pieza="sacos", huecos=()):
    n = max(1, int((x1 - x0) / 0.85))
    l = (x1 - x0) / n
    if not fino:
        t.caja(((x0 + x1) / 2, y, z + alto / 2), (x1 - x0 - 0.04, 0.48, alto - 0.02), pieza)
        return
    for i in range(n):
        if i in huecos:
            continue
        t.caja((x0 + l * (i + 0.5), y, z + alto / 2), (l - 0.04, 0.46, alto - 0.03), pieza,
               rot_z=(_h(i, 3) - 0.5) * 0.08)


def _banqueta(t, fino, L=4.98):
    v = V
    F, H = 2.0, 0.4
    k = L / 4.98
    if v == 1:                                       # escalón de sacos: dos hiladas, cuatro a lo ancho
        for f in range(2):
            for j in range(4):
                _sacos_fila(t, fino, -L / 2 + (0.2 if f else 0), L / 2 - (0.2 if f else 0), -F / 2 + 0.25 + j * 0.5,
                            f * 0.2, 0.2)
        return
    if v == 2:                                       # tarima de tablones sobre cajas
        for x in ((-1.8, 0.0, 1.8) if k > 0.7 else (-0.8, 0.8)):
            for y in (-0.55, 0.55):
                t.caja((x, y, 0.15), (0.7, 0.5, 0.3), "caja")
        n = 8 if fino else 2
        for k in range(n):
            t.caja((0, -F / 2 + (k + 0.5) * F / n, 0.34), (L, F / n - (0.03 if fino else 0.02), 0.08), "pale")
        return
    rotos = (3, 6) if v == 3 else ()
    _pales(t, fino, -L / 2, L / 2, -F / 2, F / 2, 0.0, 0.25 if v != 3 else 0.22, rotos)
    # hilada de sacos encima, contra el muro (−Y), donde se apoyan los tiradores
    _sacos_fila(t, fino, -L / 2 + 0.05, L / 2 - 0.05, -F / 2 + 0.3, 0.25, 0.15 if fino else 0.17,
                huecos=(1, 4) if v == 3 and k > 0.7 else ((1,) if v == 3 else ()))
    if v == 3:                                       # sacos caídos y uno reventado
        for j, (x, y) in enumerate(((-1.4, 1.4), (0.6, 1.5), (1.9, 1.3))):
            t.caja((x * k, y, 0.13), (0.8, 0.46, 0.26), "roto" if j == 1 else "sacos", rot_z=j * 0.9)
    if v == 4:                                       # cajas de munición y sacos de reserva
        for j, x in enumerate((-1.6, -0.9, 1.2)):
            t.caja((x * k, 0.55, 0.25 + 0.14), (0.6, 0.35, 0.28), "caja", rot_z=0.1 * j)
        t.caja((2.0 * k, 0.6, 0.25 + 0.15), (0.85, 0.5, 0.3), "sacos", rot_z=0.2)


def _nido(t, fino):
    v = V
    A, F, H = 3.0, 2.4, 0.6
    if v == 1:                                       # tablones sobre cajas
        for x in (-1.0, 1.0):
            for y in (-0.7, 0.7):
                t.caja((x, y, 0.27), (0.8, 0.6, 0.54), "caja")
        n = 8 if fino else 2
        for k in range(n):
            t.caja((0, -F / 2 + (k + 0.5) * F / n, H - 0.04), (A, F / n - (0.03 if fino else 0.02), 0.08), "pale")
    else:
        _pales(t, fino, -A / 2, A / 2, -F / 2, F / 2, 0.0, 0.3, (4,) if v == 4 else ())
        _pales(t, fino, -A / 2 + 0.1, A / 2 - 0.1, -F / 2 + 0.1, F / 2 - 0.1, 0.3, 0.3)
    # parapeto de sacos (dos hiladas) por los lados y por detrás; el frente (−Y) es el HESCO
    huecos = (1, 2) if v == 4 else ()
    for f in range(2):
        z = H + f * 0.3
        _sacos_fila(t, fino, -A / 2 + 0.3, A / 2 - 0.3, F / 2 - 0.25, z, huecos=huecos if f else ())
        for s in (-1, 1):
            if v == 4 and s > 0 and f == 1:
                continue
            for j in range(2 if fino else 1):
                yc = -F / 2 + 0.5 + j * 0.9 if fino else -0.05
                t.caja((s * (A / 2 - 0.25), yc, z + 0.15), (0.46, 0.85 if fino else 1.9, 0.27), "sacos",
                       rot_z=(_h(j + f, 5) - 0.5) * 0.06)
    # peldaños por detrás
    for k, (yy, hh) in enumerate(((F / 2 + 0.35, 0.2), (F / 2 + 0.2, 0.4))):
        if k == 1:
            continue
        t.caja((A / 2 - 0.7, yy, hh / 2), (0.9, 0.5, hh), "caja")
    if v == 2:                                       # techo de chapa sobre cuatro postes
        g = 0.08 if fino else 0.34
        for sx in (-1, 1):
            for sy in (-1, 1):
                t.caja((sx * (A / 2 - 0.1), sy * (F / 2 - 0.1), H + 1.0), (g, g, 2.0), "poste")
        t.caja((0, 0, H + 2.05), (A + 0.4, F + 0.4, 0.06), "chapa", rot_x=0.06)
    if v == 3:                                       # red por encima y colgando por detrás
        t.caja((0, 0, H + 1.9), (A + 0.6, F + 0.6, 0.04), "red")
        for sx in (-1, 1):
            t.caja((sx * (A / 2 + 0.3), 0, H + 1.0), (0.04, F + 0.6, 1.8), "red")
    if v == 4:                                       # sacos caídos detrás y uno reventado
        for k, (x, y) in enumerate(((-0.6, F / 2 + 0.9), (0.7, F / 2 + 1.1), (A / 2 + 0.6, 0.4))):
            t.caja((x, y, 0.13), (0.8, 0.46, 0.26), "roto" if k == 2 else "sacos", rot_z=k * 1.1)
