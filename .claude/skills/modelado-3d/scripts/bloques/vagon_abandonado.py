"""Vagones abandonados de Castelló (Lucía, 8-nov 18:00, D-N156: la estación infestada del capítulo 3; 18–20 m, Álvaro
18:02). Salen de los vagones aprobados (vagon_cercano.py: coche de viajeros R14, plataforma Rgs y cisterna), en civil y
dejados ahí cuando cayó la ciudad. Teo (3), 8-nov.

Ejes como el resto de vagones: largo a lo largo de Y, origen en el centro y a la cota del carril. Sin ventanillas
aparte: aquí no se encienden (lunas rotas en 48403c; el 60546c no va en cristales).
Colores, sin rojo ni la banda de la horda: coche 605c48 (costado civil) con techo b8b8b8, puertas y chapas 605c48, bastidor 48403c
(605c48 en el LOD1), vagón cerrado 847c34, contenedor quemado 605c48 con ceniza 94948c, cisterna 94948c, cajas y
maletas 845c28, escombro 88885c.
Variantes (TBD_VARIANTE % 5): 0 Saqueado (coche de viajeros con las lunas rotas y maletas tiradas al lado, +X); 1
Volcado (el mismo coche tumbado de lado hacia +X); 2 Cerrado (vagón de mercancías con las puertas correderas abiertas y
la carga caída); 3 Descarrilado (cisterna con un bogie fuera de la vía, girada 6° y ladeada 10°); 4 Quemado (plataforma
con un contenedor de 40' calcinado y la puerta abierta).
"""
import math, os
from mathutils import Matrix
from taller import Taller
import vagon_cercano as VC

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Saqueado", "Volcado", "Cerrado", "Descarrilado", "Quemado"]
PIEZAS = VC.PIEZAS + ["maleta", "escombro", "ceniza"]
COLORES = dict(VC.COLORES, casco="605c48", ventana="48403c", optica="48403c", sacos="847c34",
               contenedor="605c48" if V == 4 else "6c7874", contenedor2="847c34", maleta="845c28", escombro="88885c",
               ceniza="94948c", carga="845c28")
COLORES_LOD1 = {"bastidor": "605c48", "rueda": "605c48", "metal": "605c48", "fuelle": "605c48",
                "ventana": "6c7874", "puerta": "6c7874"}   # 48403c en sombra caía en la banda de la horda
VALOR_PIEZAS = {"casco": ("b8b8b8", "605c48", "605c48")}   # costado civil (la regla del valor partido)
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (40, 6000), 1: (20, 2000)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def _cerrado(t, fino):
    L = 18.0
    VC._chasis(t, fino, L, 6.2, piso=True)
    zb, h, a = 1.25, 2.7, 2.9
    t.caja((0, 0, zb + h / 2), (a, L - 1.0, h), "contenedor2")
    t.caja((0, 0, zb + h + 0.1), (a - 0.2, L - 1.2, 0.2), "contenedor2")
    for s in (-1, 1):                                # huecos de las puertas abiertas y las hojas corridas
        t.caja((s * (a / 2 + 0.005), 0, zb + 1.2), (0.03, 3.2, 2.3), "bastidor")
        t.caja((s * (a / 2 + 0.06), 3.4 if s > 0 else -3.4, zb + 1.2), (0.06, 3.2, 2.4), "puerta")
    for k in range(5 if fino else 3):                # carga caída por la puerta de +X
        t.caja((a / 2 + 0.8 + _h(k) * 1.6, -1.4 + k * 0.7, 0.3 + 0.2 * (k % 2)), (1.0, 0.8, 0.6), "carga",
               rot_z=_h(k, 1) * 2)


def construir(nombre="SM_VagonAbandonado", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    v = V
    if v in (0, 1):
        VC._coche(t, fino, False)
        if v == 0:
            for k in range(6 if fino else 3):        # maletas y bultos tirados por el lado de +X
                t.caja((2.1 + _h(k, 2) * 1.6, -7 + k * 2.6, 0.18), (0.7, 0.45, 0.36), "maleta", rot_z=_h(k, 3) * 3)
            t.caja((1.6, 4.0, 1.2), (0.06, 0.9, 1.95), "puerta", rot_z=0.9)        # una puerta arrancada
    elif v == 2:
        _cerrado(t, fino)
    elif v == 3:
        VC._cisterna(t, fino)
    else:
        VC._portacontenedores(t, fino, False)
        t.caja((0, 6.12, 1.27 + 1.3), (2.2, 0.06, 2.4), "bastidor")               # puerta abierta: hueco oscuro
        t.caja((1.6, 6.5, 1.27 + 1.3), (0.06, 1.2, 2.4), "contenedor", rot_z=0.6)
        t.caja((0, -1.5, 1.27 + 2.63), (2.0, 5.0, 0.05), "ceniza")                 # ceniza en el techo hundido
        for k in range(3):
            t.caja((-2.2 + k * 0.4, -4 + k * 3.5, 0.15), (0.9, 0.6, 0.3), "escombro", rot_z=_h(k, 4) * 3)
    o = t.objeto(nombre, bisel=0)
    if v == 1:                                       # volcado de lado hacia +X: gira 90° sobre el eje largo
        o.data.transform(Matrix.Rotation(-math.pi / 2, 4, "Y"))
        zs = min(vv.co.z for vv in o.data.vertices)
        o.data.transform(Matrix.Translation((1.5, 0, -zs)))
    if v == 3:                                       # descarrilada: girada, ladeada y con el bogie de −Y fuera
        o.data.transform(Matrix.Translation((0.9, 0, 0)) @ Matrix.Rotation(math.radians(6), 4, "Z") @
                         Matrix.Rotation(math.radians(10), 4, "Y"))
        zs = min(vv.co.z for vv in o.data.vertices)
        o.data.transform(Matrix.Translation((0, 0, -zs)))
    o.data.update()
    return o
