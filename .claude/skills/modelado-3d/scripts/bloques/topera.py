"""Topera de final de vía (ambientación de Sagunto, pieza 8). Teo, 6-nov-2026.

Fotos E9 de Paula (Commons): Toledo (Adif, topera de freno hidráulico Rawie: cajón de chapa con nervios, dos
cilindros horizontales y la placa de choque) y Retiro Belgrano (Buenos Aires, ancho de 1676 mm, cercano al ibérico;
la misma Rawie, amarilla y con luz roja; NO es de España). Las toperas viejas de pueblo se hacen con carriles
doblados y una traviesa de madera: eso sale de memoria, SIN FOTO (se marca así).
Colores: Rawie en amarillo f0dc9c (la de la foto 2; la azul de Toledo no cabe en la paleta: 404460 es solo de las
unidades propias), o en gris 94948c; cilindros 6c7874, placa de choque 48403c, carriles 48403c, traviesa 543820,
montón de arena 94948c. La luz roja va apagada en 48403c (283440 es de la horda).
MEDIDAS SIN VERIFICAR (típicas): ancho ibérico de 1668 mm (carriles en y = ±0,835), Rawie de 2,6 × 2,6 × 1,0 m,
placa de choque a 1,05 m (altura de topes), cilindros de 1,4 m.
La vía llega desde −X; la topera mira a −X. Origen en el eje de la vía, al pie del cajón. Sin carriles en el suelo:
la vía la pone el nivel.
Variantes: 0 Rawie amarilla (foto 2); 1 Rawie gris; 2 de carriles doblados con traviesa; 3 de carriles con montón de
arena delante; 4 Rawie empujada por un choque, girada y con un cilindro partido; 5 de carriles rota, la traviesa caída.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["cajon", "nervio", "cilindro", "placa", "carril", "traviesa", "arena", "foco"]
COLORES = {"cajon": ("f0dc9c", "94948c", "f0dc9c", "f0dc9c", "f0dc9c", "f0dc9c")[VAR % 6], "nervio": "6c7874",
           "cilindro": "6c7874", "placa": "48403c", "carril": "48403c", "traviesa": "543820", "arena": "94948c",
           "foco": "48403c"}
FACCION = "neutro"
PRESUPUESTO = {0: (20, 600), 1: (10, 300)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
YC, ZT = 0.835, 1.05


def construir(nombre="SM_Topera", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    v = VAR % 6
    if v in (0, 1, 4):
        _rawie(t, fino, lod, v == 4)
    else:
        _carriles(t, fino, lod, v)
    return t.objeto(nombre, bisel=0)


def _rawie(t, fino, lod, chocada):
    rz = 0.22 if chocada else 0.0
    dx = 0.8 if chocada else 0.0
    c, s = math.cos(rz), math.sin(rz)
    R = lambda x, y, z: (dx + x * c - y * s, x * s + y * c, z)
    t.caja(R(0.5, 0, 0.5), (1.0, 2.6, 1.0), "cajon", rot_z=rz)
    t.caja(R(0.0, 0, 0.5), (0.06, 2.6, 1.0), "cajon", rot_z=rz, inclinar=None)
    for y in (-1.0, -0.35, 0.35, 1.0):                          # nervios del cajón
        fino.caja(R(-0.04, y, 0.45), (0.05, 0.08, 0.8), "nervio", rot_z=rz)
    g = 0.12 if lod == 0 else 0.3
    for k, y in enumerate((-0.5, 0.5)):                         # cilindros y placa de choque
        largo = 0.6 if (chocada and k == 0) else 1.4
        t.cilindro(R(-largo / 2, y, ZT - 0.2), g, largo, "X", 8, "cilindro")
    if chocada:
        t.cilindro((dx - 2.2, -1.4, 0.12), g, 0.8, "X", 8, "cilindro")   # trozo de cilindro en el suelo
    t.caja(R(-1.45, 0, ZT - 0.2), (0.12, 2.0, 0.55), "placa", rot_z=rz)
    t.caja(R(0.5, 0, 1.08), (0.25, 0.25, 0.16), "foco", rot_z=rz)  # luz apagada encima


def _carriles(t, fino, lod, v):
    g = 0.14 if lod == 0 else 0.34
    rota = v == 5
    for y in (-YC, YC):                                         # carril doblado hacia arriba en cuarto de círculo
        t.caja((0.55, y, ZT), (0.5, g, g), "carril")                              # tramo de arriba
        t.caja((0.19, y, ZT / 2), (g, g, ZT + 0.2), "carril", rot_y=0.45)          # carril que sube
        t.caja((1.0, y, ZT / 2 - 0.1), (g, g, ZT + 0.4), "carril", rot_y=-0.55)    # tornapunta trasera
    if rota:
        t.caja((-1.0, 0.2, 0.15), (0.25, 2.6, 0.25), "traviesa", rot_z=0.35)
    else:
        t.caja((0.45, 0, ZT), (0.28, 2.6, 0.32), "traviesa")
        fino.caja((0.28, 0, ZT), (0.04, 2.0, 0.2), "placa")
    if v == 3:                                                  # montón de arena delante
        t.caja((-0.9, 0, 0.25), (1.4, 2.4, 0.5), "arena", inclinar=0.4)
        t.caja((-0.9, 0, 0.55), (0.8, 1.6, 0.25), "arena")
