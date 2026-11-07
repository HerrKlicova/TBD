"""Almacén de naranjas (magatzem), nave de 11 × 21 m (encargada a dos aguas; azotea por la colisión) (Álvaro, sagunto-nivel.md §5). Teo, 6-nov-2026.

Fotos F4 de Paula (Commons, Alcàsser, l'Horta Sud; no es del Camp de Morvedre, pero es el mismo tipo): fachada encalada
en el hastial con frontón ondulado que tapa el tejado y lleva un óculo, portón metálico grande, puertas pequeñas,
ventana enrejada y zócalo gris. El frontón ondulado se hace a escalones (centro alto, dos hombros y dos remates):
desde arriba, a 64 px, una curva y un escalón se ven igual.
MEDIDAS: planta de 11 × 21 m (encargo de Álvaro). Azotea a 4,0 m con pretil de 0,7 m (cuadra con la colisión del
tejado de pruebas, Level.RoofH = 4: no hay que tocar la simulación; Álvaro, 17:58). Frontón hasta 6,8 m, portón de 4 × 4,2 m, óculo de 1,2 m:
SIN VERIFICAR (típicas, estimadas sobre la foto). Colores: cal d4d0cc, zócalo 94948c, portones de chapa 6c7874,
teja a86c50, rejas y óculo 48403c (nada de valores de la horda). Fachada a la calle = −Y (+Z en Unity).
Se construye con la nave a lo largo de X y el hastial en −X, y al final se gira 90° para que la fachada mire a −Y.
Variantes: 0 la de la foto; 1 con muelle de carga y marquesina en el costado; 2 con un trozo de tejado hundido;
3 con el portón abierto (interior oscuro) y cajas de fruta apiladas; 4 enfoscada en ocre b4a080 con frontón sin óculo;
5 tapiada con sacos y tablas.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["cal", "zocalo", "porton", "tejado", "oscuro", "madera", "saco"]
_CAL = ("d4d0cc", "d4d0cc", "d4d0cc", "d4d0cc", "b4a080", "d4d0cc")
COLORES = {"cal": _CAL[VAR % 6], "zocalo": "94948c", "porton": "6c7874", "tejado": "a86c50", "oscuro": "48403c",
           "madera": "845c28", "saco": "b4a080"}
FACCION = "neutro"
PRESUPUESTO = {0: (80, 1500), 1: (40, 700)}
L, W, ALERO, CUMBRE = 21.0, 11.0, 3.8, 2.0   # azotea a 4,0 m = colisión del tejado de pruebas (Level.RoofH = 4)


def construir(nombre="SM_AlmacenNaranjas", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    v = VAR % 6
    hx, hy = L / 2, W / 2
    t.caja((0, 0, ALERO / 2), (L, W, ALERO), "cal")
    t.caja((0, 0, 0.5), (L + 0.08, W + 0.08, 1.0), "zocalo")
    # azotea de rasilla con la cara de arriba a 4,0 m (Álvaro, 17:58: la colisión es una caja plana de 4 m; así los
    # zombis pisan justo la cubierta) y pretil bajo de cal en los costados y la trasera
    if v == 2:   # azotea hundida: hueco oscuro de 4,6 m
        for x0, x1 in ((-hx - 0.05, 0.0), (4.6, hx + 0.05)):
            t.caja(((x0 + x1) / 2, 0, ALERO + 0.1), (x1 - x0, W + 0.1, 0.2), "tejado")
        t.caja((2.3, 0, ALERO + 0.06), (4.7, W + 0.04, 0.12), "oscuro")
        t.caja((2.3, -hy / 2, ALERO - 0.6), (3.6, 0.2, 1.6), "tejado", rot_x=0.9)
    else:
        t.caja((0, 0, ALERO + 0.1), (L + 0.1, W + 0.1, 0.2), "tejado")
    for s_ in (-1, 1):
        t.caja((0.2, s_ * (hy - 0.12), ALERO + 0.45), (L - 0.3, 0.3, 0.7), "zocalo")   # pretil gris: nada de filo claro
    t.caja((hx - 0.12, 0, ALERO + 0.45), (0.3, W - 0.6, 0.7), "zocalo")
    # frontón escalonado en el hastial (−X), de 0,4 m de grueso
    xf = -hx - 0.2
    for (ya, yb, z) in ((-1.6, 1.6, ALERO + CUMBRE + 1.0), (1.5, 4.0, ALERO + 1.7), (3.9, hy + 0.1, ALERO + 0.85)):
        for s in ((1,) if ya < 0 else (-1, 1)):
            y0, y1 = (ya, yb) if s > 0 else (-yb, -ya)
            t.caja((xf, (y0 + y1) / 2, (ALERO - 0.2 + z) / 2), (0.4, y1 - y0, z - ALERO + 0.2), "cal")
    if v != 4:
        t.cilindro((xf - 0.21, 0, ALERO + 1.5), 0.6, 0.06, "X", 12, "oscuro")                   # óculo
    for y in ((-hy + 0.1, hy - 0.1, -1.55, 1.55) if lod == 0 else ()):                          # remates (LOD0)
        fino.caja((xf, y, ALERO + (1.0 if abs(y) > 2 else 3.15)), (0.3, 0.3, 0.4), "cal")
    # fachada: portón grande, dos puertas y una ventana con reja
    xd = -hx - 0.03
    abierto = v == 3
    t.caja((xd, 0, 2.1), (0.06, 4.0, 4.2), "oscuro" if abierto else "porton")
    for y in (-3.9, 3.9):
        t.caja((xd, y, 1.2), (0.06, 1.2, 2.4), "madera" if v == 5 else "porton")
    t.caja((xd, 2.4 if v else -2.4, 3.4), (0.06, 1.0, 1.2), "oscuro")                          # ventana
    fino.caja((xd - 0.08, 2.4 if v else -2.4, 3.4), (0.04, 1.1, 1.3), "oscuro")                # reja (LOD0)
    # costados: ventanas altas y una puerta lateral
    for s in (-1, 1):
        for x in (-6.0, 0.0, 6.0):
            t.caja((x, s * (hy + 0.03), 4.3), (1.4, 0.06, 0.8), "oscuro")
    t.caja((5.0, hy + 0.03, 1.6), (3.0, 0.06, 3.2), "porton")
    if v == 1:   # muelle de carga con marquesina en el costado norte (+Y)
        t.caja((2.0, hy + 1.2, 0.6), (10.0, 2.4, 1.2), "zocalo")
        t.caja((2.0, hy + 1.4, 4.4), (11.0, 3.0, 0.15), "porton", rot_x=-0.12)
        for x in (-3.0, 2.0, 7.0):
            fino.caja((x, hy + 2.7, 2.8), (0.15, 0.15, 3.2), "porton")
    if v == 3:   # cajas de fruta apiladas delante
        for i, (dx, dy, n) in enumerate(((-1.2, -3.0, 3), (-2.0, -1.8, 2), (-1.0, 3.2, 2), (-2.4, 4.4, 1))):
            for k in range(n):
                t.caja((-hx + dx, dy + 0.05 * k, 0.25 + 0.5 * k), (0.9, 1.1, 0.48), "madera")
    if v == 5:   # portón tapiado con sacos y tablas
        for k in range(3):
            t.caja((-hx - 0.5, 0, 0.3 + 0.55 * k), (0.8, 4.4 - 0.4 * k, 0.5), "saco")
        t.caja((xd - 0.05, 0, 2.8), (0.05, 4.2, 0.35), "madera", rot_x=0.4)
    o = t.objeto(nombre, bisel=0)
    from mathutils import Matrix
    o.data.transform(Matrix.Rotation(math.radians(90), 4, "Z"))     # hastial de −X a −Y
    return o
