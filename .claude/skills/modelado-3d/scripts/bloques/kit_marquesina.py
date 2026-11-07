"""Kit de estaciones: tramo de marquesina de 6 m (Teo, 7-nov). Se encadena en X (5,98 m de chapa con holgura).

Sale de la marquesina de Sagunto aprobada (D-N123, notas aplicadas): fundición verde 50541c (70784c en el LOD1), chapa
94948c con ondas 6c7874 en el LOD0 y bandas de 1 m en el LOD1, canalón 48403c (605c48 en el LOD1). Altura libre de
3,8 m. Fotos: E2 (La Encina y Almazán) y E1_3 (Nules, marquesina de celosía en voladizo). Medidas SIN VERIFICAR.
Origen: centro del tramo, en la cara de arriba del andén (el montaje la sube a la altura del andén).
Variantes (TBD_VARIANTE):
  0 celosía de Sagunto: pilar en el borde de la vía y cercha hasta el edificio (andén lateral de 5 m).
  1 voladizo de un pilar (apeadero, foto de Nules): pilar al fondo y ménsula de celosía que vuela 3,5 m hacia la vía.
  2 a dos aguas sobre andén central (Castellón): pilar en el eje y cubierta que vierte a las dos vías.
  3 sin chapa: solo la estructura (tramo desnudo).
  4 tramo hundido: la cubierta cae 0,9 m por el lado de la vía y el canalón cuelga.
  5 moderna de hormigón: pilar cuadrado 94948c y losa plana con vuelo.
"""
import math
from taller import Taller
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual, bandas_chapa)

PRESUPUESTO = {0: (20, 1200), 1: (20, 600)}
LARGO, FONDO, ALTO = 6.0, 5.0, 3.8


def _chapa(t, lod, x0, y0, fondo, z0, pend, faltan=None):
    """Chapa a un agua que sube hacia +Y (pend > 0) o baja (pend < 0), con su acabado por LOD."""
    a = pend
    t.caja((x0, y0, z0), (LARGO - 0.02, fondo / math.cos(a), 0.06), "chapa", rot_x=a)
    if lod == 0:
        for k in range(12):
            if faltan and k in faltan:
                continue
            t.caja((x0 - LARGO / 2 + 0.25 + k * 0.5, y0, z0 + 0.04), (0.25, fondo / math.cos(a), 0.02), "onda", rot_x=a)
    else:
        bandas_chapa(t, x0, LARGO, y0, fondo, z0 + 0.02, a)


def _celosia(t, lod, x, y0, y1, z, alto=0.5):
    """Cercha de celosía entre y0 e y1 a la altura z (LOD0) o viga maciza (LOD1: las barras no llegan a 2 px)."""
    lc, yc = abs(y1 - y0), (y0 + y1) / 2
    if lod == 0:
        for zc in (z, z - alto):
            t.caja((x, yc, zc), (0.08, lc, 0.08), "pilar")
        n = max(2, int(lc / 0.55))
        for k in range(n):
            yy = min(y0, y1) + 0.3 + k * (lc - 0.6) / max(1, n - 1)
            t.caja((x, yy, z - alto / 2), (0.06, 0.06, alto + 0.1), "pilar", rot_x=math.radians(30 if k % 2 else -30))
    else:
        t.caja((x, yc, z - alto / 2), (0.34, lc, alto + 0.05), "pilar")


def construir(nombre="SM_KitMarquesina", lod=0, var=None):
    v = var_actual(var)
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    r = 0.12 if lod == 0 else 0.18
    pend = math.atan2(0.6, FONDO)
    if v in (0, 3, 4):
        yp = -FONDO / 2 + 0.9
        t.cilindro((0, yp, ALTO / 2), r, ALTO, "Z", 8 if lod == 0 else 6, "pilar")
        fino.cilindro((0, yp, 0.15), r + 0.08, 0.3, "Z", 8, "pilar")
        fino.caja((0, yp, ALTO - 0.15), (0.45, 0.45, 0.3), "pilar")
        _celosia(t, lod, 0, -FONDO / 2 - 0.1, FONDO / 2 + 0.1, ALTO + 0.1)
        t.caja((0, yp, ALTO + 0.05), (LARGO - 0.02, 0.25, 0.3), "pilar")             # viga longitudinal
        caida = -0.9 if v == 4 else 0.0
        if v != 3:
            if v == 4:   # la cubierta gira hacia la vía: el canto de la vía baja 0,9 m
                _chapa(t, lod, 0, 0, FONDO, ALTO + 0.05, math.atan2(1.5, FONDO))
                t.caja((0.8, -FONDO / 2 - 0.2, ALTO - 0.9), (2.4, 0.25, 0.25), "canalon", rot_y=math.radians(25))
            else:
                _chapa(t, lod, 0, 0, FONDO, ALTO + 0.5, pend)
                t.caja((0, -FONDO / 2 - 0.1, ALTO + 0.15), (LARGO - 0.02, 0.25, 0.25), "canalon")
        if lod == 0 and v != 3:
            for sy in (-1, 1):
                t.caja((0, yp + sy * 0.45, ALTO - 0.75), (0.07, 0.07, 1.0), "pilar", rot_x=math.radians(-40 * sy))
    elif v == 1:   # voladizo de un pilar (Nules)
        yp = FONDO / 2 - 0.6
        t.cilindro((0, yp, ALTO / 2 + 0.2), r, ALTO + 0.4, "Z", 8 if lod == 0 else 6, "pilar")
        fino.cilindro((0, yp, 0.15), r + 0.08, 0.3, "Z", 8, "pilar")
        _celosia(t, lod, 0, yp + 0.3, -FONDO / 2, ALTO + 0.35, alto=0.6)
        t.caja((0, yp, ALTO + 0.45), (LARGO - 0.02, 0.25, 0.3), "pilar")
        tir = math.atan2(1.4, FONDO - 1.2)                                             # tornapuntas bajo la ménsula
        t.caja((0, yp - (FONDO - 1.2) / 2, ALTO - 0.5), (0.1 if lod == 0 else 0.34, (FONDO - 1.2) / math.cos(tir), 0.1 if lod == 0 else 0.3), "pilar", rot_x=tir)
        _chapa(t, lod, 0, 0, FONDO, ALTO + 0.75, -math.atan2(0.4, FONDO))             # vierte hacia la vía
        t.caja((0, -FONDO / 2 - 0.1, ALTO + 0.5), (LARGO - 0.02, 0.25, 0.25), "canalon")
    elif v == 2:   # dos aguas sobre isla de 4 m
        t.cilindro((0, 0, ALTO / 2 + 0.3), r, ALTO + 0.6, "Z", 8 if lod == 0 else 6, "pilar")
        fino.cilindro((0, 0, 0.15), r + 0.08, 0.3, "Z", 8, "pilar")
        f2 = 2.9
        for s in (-1, 1):
            _celosia(t, lod, 0, s * 0.2, s * f2, ALTO + 0.4 + (0.01 if s > 0 else 0), alto=0.45)
            a = -s * math.atan2(0.45, f2)
            _chapa(t, lod, 0, s * (f2 / 2 + 0.08), f2 - 0.1, ALTO + 0.75 - 0.225 + 0.05, a)
            t.caja((0, s * (f2 + 0.1), ALTO + 0.42), (LARGO - 0.02, 0.25, 0.25), "canalon")
        t.caja((0, 0, ALTO + 0.85), (LARGO - 0.02, 0.3, 0.12), "chapa")               # cumbrera
    else:          # moderna de hormigón
        g = 0.4
        yp = -FONDO / 2 + 1.2
        t.caja((0, yp, ALTO / 2), (g, g, ALTO), "losa")
        t.caja((0, 0.3, ALTO + 0.15), (LARGO - 0.02, FONDO + 0.6, 0.3), "losa")
        t.caja((0, -FONDO / 2 - 0.05, ALTO + 0.3), (LARGO - 0.02, 0.3, 0.3), "cornisa")
        if lod == 0:
            t.caja((0, 0.3, ALTO + 0.31), (LARGO - 0.3, FONDO + 0.3, 0.02), "recerco")
    return t.objeto(nombre, bisel=0)
