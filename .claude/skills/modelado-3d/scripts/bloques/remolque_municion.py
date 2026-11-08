"""Remolque de munición para la zona de rearme de Bétera, junto a P1–P3 (Lucía, 8-nov 18:21), al lado del camión
cisterna: el NH90 reposta y rearma a la vez. Teo (3), 8-nov.

Remolque militar de un eje de 3,6 × 1,9 m (caja de 2,8 m con laterales abatibles de 0,4 m) y lanza con la argolla y la
pata de apoyo. DE MEMORIA (Paula no tiene foto de un remolque de munición del ET): medidas SIN VERIFICAR. La carga
repite las cajas del rearme_set (caja de munición de 0,75 × 0,4 × 0,34 m, caja de cohetes de 1,9 × 0,5 × 0,4 m) para
que casen con el palé del helipuerto.
Ejes: largo a lo largo de Y con la lanza hacia −Y, origen en el suelo bajo el eje.
Colores: caja del remolque 50541c con el piso en 88885c (valor partido), cajas de munición 50541c con la tapa en
88885c, cajas de cohetes 847c34, lona 847c34, ruedas 48403c (605c48 en el LOD1), metal 605c48. Nada rojo ni ámbar.
Variantes (TBD_VARIANTE % 5): 0 Cargado (dos hiladas de cajas de munición); 1 Cohetes (cajas de cohetes en dos
hiladas); 2 Descargando (lateral de +X abatido y cajas en el suelo, una abierta, camino del helicóptero); 3 Tapado (lona
atada encima de la carga); 4 Vacío (desenganchado, apoyado en la pata, con el lateral trasero abierto).
"""
import math, os
from taller import Taller

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Cargado", "Cohetes", "Descargando", "Tapado", "Vacio"]
PIEZAS = ["remolque", "caja", "cohete", "lona", "goma", "metal", "interior"]
COLORES = {"remolque": "50541c", "caja": "50541c", "cohete": "847c34", "lona": "847c34", "goma": "48403c",
           "metal": "605c48", "interior": "605c48"}
COLORES_LOD1 = {"goma": "605c48"}
VALOR_PIEZAS = {"remolque": ("88885c", "50541c", "50541c"), "caja": ("88885c", "50541c", "50541c")}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (40, 2000), 1: (20, 600)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
LC, AC, HL = 2.8, 1.9, 0.4
ZP = 0.75                                             # piso de la caja


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def _caja(t, x, y, z, rot=0.0, fino=True, abierta=False):
    t.caja((x, y, z + 0.17), (0.75, 0.4, 0.34 if not abierta else 0.3), "caja" if not abierta else "interior", rot_z=rot)
    if abierta:
        t.caja((x - 0.2 * math.sin(rot), y + 0.3 * math.cos(rot), z + 0.4), (0.75, 0.04, 0.4), "caja", rot_z=rot)
    elif fino:
        for s in (-1, 1):
            t.caja((x + s * 0.39 * math.cos(rot), y + s * 0.39 * math.sin(rot), z + 0.25), (0.03, 0.14, 0.04), "metal",
                   rot_z=rot)


def construir(nombre="SM_RemolqueMunicion", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    v = V
    g = 0.08 if fino else 0.34
    # inclinación: desenganchado (v4) apoya en la pata con la lanza a 0,45 m; enganchado, horizontal
    a = math.radians(6) if v == 4 else 0.0
    def C(x, y, z):
        return (x, y * math.cos(a) - (z - 0.5) * math.sin(a), 0.5 + y * math.sin(a) + (z - 0.5) * math.cos(a))
    def caja(c, s, pieza, rz=0.0):
        t.caja(C(*c), s, pieza, rot_z=rz, rot_x=a)
    # eje y ruedas
    for s in (-1, 1):
        t.cilindro((s * (AC / 2 + 0.2), 0, 0.45), 0.45, 0.3, "X", 14 if fino else 8, "goma")
    t.caja((0, 0, 0.45), (AC + 0.1, 0.16, 0.16), "metal")
    # caja
    caja((0, 0, ZP - 0.06), (AC, LC, 0.12), "remolque")                        # piso
    caja((0, 0, ZP - 0.25), (0.9, LC, 0.26), "metal")                          # largueros
    lados = {"x+": v != 2, "x-": True, "y+": v != 4, "y-": True}
    if lados["x-"]:
        caja((-AC / 2 + 0.03, 0, ZP + HL / 2 - 0.03), (0.06, LC, HL + 0.06), "remolque")
    if lados["x+"]:
        caja((AC / 2 - 0.03, 0, ZP + HL / 2 - 0.03), (0.06, LC, HL + 0.06), "remolque")
    else:                                                                       # abatido, colgando
        caja((AC / 2 + 0.045, 0, ZP - HL / 2 - 0.01), (0.06, LC, HL), "remolque")
    caja((0, -LC / 2 + 0.03, ZP + HL / 2 - 0.03), (AC - 0.13, 0.06, HL + 0.06), "remolque")
    if lados["y+"]:
        caja((0, LC / 2 - 0.03, ZP + HL / 2 - 0.03), (AC - 0.13, 0.06, HL + 0.06), "remolque")
    else:
        caja((0, LC / 2 + 0.045, ZP - HL / 2 - 0.01), (AC - 0.13, 0.06, HL), "remolque")
    for s in (-1, 1):                                                           # guardabarros
        caja((s * (AC / 2 + 0.2), 0, 1.0), (0.4, 1.1, 0.05), "remolque")
    # lanza, argolla y pata
    yl = -LC / 2 - 0.5
    caja((0, yl, ZP - 0.2), (g, 1.0, g), "metal")
    for s in ((-1, 1) if fino else ()):
        caja((s * 0.3, -LC / 2 - 0.25, ZP - 0.2), (g, 0.6, g), "metal", rz=s * 0.45)
    caja((0, yl - 0.55, ZP - 0.2), (0.16 if fino else 0.34, 0.16, 0.06), "metal")
    zp = 0.5 + (yl + 0.1) * math.sin(a) + (ZP - 0.2 - 0.5)                     # bajo de la lanza donde va la pata
    t.caja((0, yl + 0.1, zp / 2), (g, g, max(zp, 0.1)), "metal")
    # carga
    z0 = ZP + 0.01
    if v == 0 or v == 3:
        for f in range(2):
            for i in range(2):
                for j in range(6):
                    _caja(t, (i - 0.5) * 0.82, -1.1 + j * 0.44, z0 + f * 0.355, fino=fino and f == 1)
        if v == 3:                                                              # lona encima
            t.caja((0, 0, z0 + 0.72), (AC - 0.1, LC - 0.1, 0.1), "lona")
            for s in (-1, 1):
                t.caja((s * (AC / 2 - 0.04), 0, z0 + 0.45), (0.06, LC - 0.2, 0.5), "lona")
            if fino:
                for k in range(3):
                    t.caja((0, -0.9 + k * 0.9, z0 + 0.78), (AC + 0.02, 0.05, 0.03), "metal")
    elif v == 1:
        for f in range(2):
            for i in range(3):
                t.caja((-0.55 + i * 0.55, 0, z0 + 0.2 + f * 0.42), (0.5, 1.9, 0.4), "cohete")
    elif v == 2:
        for i in range(2):
            for j in range(4):
                _caja(t, (i - 0.5) * 0.82 - 0.1, -1.1 + j * 0.44, z0, fino=fino)
        for k in range(4):                                                      # en el suelo, camino de +X
            _caja(t, AC / 2 + 0.9 + _h(k) * 0.9, -0.9 + k * 0.6, 0.0, rot=(_h(k, 1) - 0.5) * 0.8, fino=fino,
                  abierta=(k == 2 and fino))
        _caja(t, AC / 2 + 1.2, -0.6, 0.36, rot=0.2, fino=fino)
    return t.objeto(nombre, bisel=0)
