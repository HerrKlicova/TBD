"""Muelle de carga modular del apartado de Nules (Álvaro, 8-nov, nules-nivel.md §6.2 y nules.json «muelle»: X 90–170,
Z 41,5–47,3, 1,2 m de alto, rampa de 12 m al este, X 170–182; ficción, idea de Paula). Teo (3), 8-nov.

Módulos de 10 m a lo largo de la vía (9,98 con holgura) y 5,8 m de fondo (los 47,3 − 41,5 del JSON), muro de 1,2 m con
el canto de hormigón hacia la vía y la losa arriba; colores del kit (kit_comun, como el andén aprobado): losa 94948c,
costados 88885c, canto 94948c, banda de seguridad f0dc9c, palés y cajas 845c28, sacos 605c48, topes 48403c (605c48 en
el LOD1). Medidas SIN VERIFICAR.
Origen: centro de la planta, en el suelo. Vía del apartado hacia −Y (= +Z de Unity con yaw 0); el canto queda en
y = −2,9 (a 1,7 m del eje del apartado, Z 49).
Variantes (TBD_VARIANTE % 5): 0 módulo liso; 1 módulo con palés, cajas y sacos (carga del viaje a Nules, D-N172);
2 módulo roto (se ha caído un trozo del canto); 3 rampa de testero de 12 m (baja de 1,2 m a 0 hacia +X; origen en su
centro); 4 módulo de cabeza oeste: testero cerrado en −X con dos topes de goma y escalera.
"""
import math
from taller import Taller
from kit_comun import (PIEZAS, COLORES, COLORES_LOD1, PERMITIR, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION,
                       var_actual)

PRESUPUESTO = {0: (30, 1200), 1: (15, 500)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
LARGO, FONDO, H = 9.98, 5.8, 1.2


def construir(nombre="SM_MuelleCarga", lod=0):
    v = var_actual() % 5
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.06 if fino else 0.34
    yb = -FONDO / 2
    if v == 3:                                       # rampa de 12 m
        L = 12.0
        a = math.atan2(H, L)
        t.caja((0, 0, H / 2 - 0.06), (math.hypot(L, H), FONDO, 0.12), "losa", rot_y=-a)
        t.caja((-L / 4 + 0.15, 0, H / 4 + 0.15), (L / 2 + 0.3, FONDO - 0.1, H / 2 + 0.3), "costado")
        t.caja((L / 4 - 1.5, 0, H / 8 + 0.05), (L / 2 - 3.0, FONDO - 0.1, H / 4 + 0.1), "costado")
        for s in (-1, 1):                            # bordillos laterales
            t.caja((0, s * (FONDO / 2 - 0.1), H / 2 + 0.05), (math.hypot(L, H), 0.2 if fino else 0.34, 0.15), "cornisa",
                   rot_y=-a)
        return t.objeto(nombre, bisel=0)
    if v == 2:                                       # canto roto: falta un trozo de 3 × 1,2 m
        t.caja((0, 0.6, H / 2), (LARGO, FONDO - 1.2, H), "costado")
        t.caja((0, 0.6, H - 0.05), (LARGO - 0.004, FONDO - 1.18, 0.12), "losa")
        for x0, x1 in ((-LARGO / 2, -1.2), (1.8, LARGO / 2)):
            t.caja(((x0 + x1) / 2, yb + 0.65, H / 2 - 0.01), (x1 - x0, 1.3, H - 0.02), "costado")
            t.caja(((x0 + x1) / 2, yb + 0.65, H - 0.04), (x1 - x0 + 0.01, 1.32, 0.12), "losa")
            t.caja(((x0 + x1) / 2, yb + 0.15, H + 0.02), (x1 - x0, 0.3 if fino else 0.34, 0.1), "cornisa")
        t.caja((0.3, yb + 0.7, H * 0.35), (3.0, 1.0, H * 0.7), "costado")
        for k, (dx, dy, gr) in enumerate(((-0.6, -0.6, 20), (0.7, -0.9, -35), (1.5, -0.5, 50))):
            t.caja((dx, yb + dy, 0.18), (0.8, 0.5, 0.36), "escombro", rot_z=math.radians(gr))
    else:
        t.caja((0, 0, H / 2), (LARGO, FONDO, H), "costado")
        t.caja((0, 0, H - 0.05), (LARGO - 0.004, FONDO + 0.02, 0.12), "losa")
        t.caja((0, yb + 0.15, H + 0.02), (LARGO - 0.004, 0.3 if fino else 0.34, 0.1), "cornisa")   # canto de hormigón
        t.caja((0, yb + 0.75, H + 0.025), (LARGO - 0.1, 0.34, 0.03), "borde")                     # banda de seguridad
    if v == 1:                                       # carga: palés con cajas y sacos
        for k, (x, y, alto, pieza) in enumerate(((-3.0, 0.8, 0.9, "madera"), (-1.6, 1.2, 0.6, "hollin"),
                                                 (1.2, 0.4, 1.1, "madera"), (3.2, 1.4, 0.6, "hollin"))):
            t.caja((x, y, H + 0.07), (1.2, 1.0, 0.14), "tabla", rot_z=0.05 * k)
            t.caja((x, y, H + 0.14 + alto / 2), (1.1, 0.9, alto), pieza, rot_z=0.05 * k)
    if v == 4:                                       # cabeza oeste: testero, topes y escalera
        t.caja((-LARGO / 2 + 0.18, 0, H + 0.3), (0.3, FONDO - 0.04, 0.6), "cornisa")
        for y in (-1.5, 1.5):
            t.cilindro((-LARGO / 2 - 0.15, y, 0.7), 0.25 if fino else 0.3, 0.4, "X", 8 if fino else 6, "metal")
        n = 5
        for k in range(n):                           # escalera en el costado de detrás (+Y), peldaños solapados
            hk = H * (k + 1) / (n + 1)
            t.caja((2.0, FONDO / 2 - 0.05 + 0.3 * (n - k) - 0.15, hk / 2 + 0.01), (1.4 - 0.03 * k, 0.34, hk), "cornisa")
    return t.objeto(nombre, bisel=0)
