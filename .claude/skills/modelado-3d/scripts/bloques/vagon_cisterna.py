"""Vagón cisterna de bogies para el depósito de combustible de Castellón (Álvaro, castellon-nivel.md §1; Teo, 7-nov).

Foto: R4_vagon_cisterna_1.jpg de Paula (catálogo R4, Commons «Vagones con biodiesel»; tren de cisternas de bogies de
Renfe Mercancías): cuba cilíndrica gris plata con fondos abombados, bastidor oscuro corrido por debajo, plataforma con
barandilla en un testero y escalera que sube por el fondo hasta la pasarela de arriba, con su boca de carga en el centro.
Cotas SIN VERIFICAR (no hay ficha): 18 m entre topes, cuba de Ø 3,0 m y 15,4 m de cilindro con el eje a 2,85 m,
bastidor a 1,15 m, bogies con centros a ±6 m y ruedas de 0,92 m. Sin rotulación: la marca de Renfe de la foto no va.
Colores: cuba 94948c (la plata de la foto; b8b8b8 no va en piezas largas y d4d0cc es solo de fachadas fijas), bastidor y
bogies 48403c, que suben a 605c48 en el LOD1 (D-N80). Panel de calcomanía en los dos costados (placa 605c48, 2,6 x 1,1 m,
a 1 cm de la cuba): ahí va el decal de lleno o vacío de Bruno, para que el stock del depósito se lea en las cisternas
(cada cisterna ≈ 17 u, Hugo).
Ejes: largo a lo largo de Y (como el R2), delante = -Y. Origen en el centro, a la cota del carril.
Variantes (TBD_VARIANTE % 5; regla de Eugenio, lista de Álvaro):
  0 limpia;  1 oxidada (manchas de óxido 7c3c18 solo arriba y en el costado que da a la luz (-X), para que no caiga en sombra, sin cambiar
  el gris de la cuba: el 88885c es el del uniforme propio);  2 sin escalera (arrancada: queda el arranque y la barandilla de la plataforma doblada);
  3 abollada (dos abolladuras en el costado y una en el fondo);  4 quemada (cuba en hollín 605c48 con ceniza 94948c
  arriba y óxido, sin escalera ni barandillas; el panel de la calcomanía se queda para que siga contando en el stock).
"""
import math
import os
from mathutils import Vector
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["cuba", "bastidor", "bogie", "rueda", "escalera", "pasarela", "placa", "oxido", "ceniza", "topes"]
COLORES = {"cuba": "94948c", "bastidor": "48403c", "bogie": "48403c", "rueda": "48403c", "escalera": "605c48",
           "pasarela": "6c7874", "placa": "605c48", "oxido": "7c3c18", "ceniza": "94948c", "topes": "48403c"}
if VAR == 4:
    COLORES["cuba"] = "605c48"
COLORES_LOD1 = {"bastidor": "605c48", "bogie": "605c48", "rueda": "605c48", "topes": "605c48"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (150, 3500), 1: (100, 1600)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.12}
R = math.radians
ZE, RC, LC = 2.85, 1.5, 15.4          # eje de la cuba, radio, largo del cilindro
ZB = 1.15                             # bastidor


def _nuevos(t, antes):
    return [v for v in t.bm.verts if v not in antes]


def _abollar(verts, centro, radio, hondo, eje=Vector((0, 0, ZE))):
    """Empuja hacia el eje de la cuba los vértices a menos de radio del centro (una abolladura)."""
    c = Vector(centro)
    for v in verts:
        d = (v.co - c).length
        if d < radio:
            r = Vector((v.co.x, 0, v.co.z - eje.z))
            if r.length > 1e-6:
                v.co -= r.normalized() * hondo * (1 - d / radio) ** 1.5


def _parche(t, ang, y, largo, ancho, pieza):
    """Mancha pegada a la cuba en el ángulo ang (0 = arriba, positivo hacia +X), a 1 cm de la superficie."""
    a = R(ang)
    x, z = (RC + 0.01) * math.sin(a), ZE + (RC + 0.01) * math.cos(a)
    t.caja((x, y, z), (ancho, largo, 0.03), pieza, rot_y=a)


def construir(nombre="SM_VagonCisterna", lod=0):
    v = VAR
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    g = 0.06 if lod == 0 else 0.2           # barras finas: 2 px en el LOD1
    # --- bastidor, testeros, topes
    t.caja((0, 0, ZB), (2.6, 16.8, 0.35), "bastidor")
    for s in (-1, 1):
        t.caja((0, s * 8.55, ZB - 0.05), (2.9, 0.3, 0.5), "bastidor")
        for x in (-0.9, 0.9):
            t.cilindro((x, s * 8.85, 1.06), 0.2, 0.3, "Y", 8, "topes")
            t.cilindro((x, s * 9.06, 1.06), 0.27, 0.12, "Y", 8, "topes")       # platillo
        # bogie: bastidor de bogie y dos ejes (batalla 2 m)
        yb = s * 6.0
        for x in (-1.0, 1.0):
            t.caja((x, yb, 0.62), (0.2, 2.8, 0.38), "bogie")
        for e in (-1.0, 1.0):
            for x in (-0.83, 0.83):
                t.cilindro((x, yb + e, 0.46), 0.46, 0.13, "X", 12 if lod == 0 else 8, "rueda")
        t.caja((0, yb, 0.85), (2.2, 0.5, 0.25), "bogie")                          # traviesa del bogie
    # --- cunas que apoyan la cuba en el bastidor
    for y in (-5.0, 0.0, 5.0):
        t.caja((0, y, ZB + 0.33), (2.2, 0.4, 0.35), "bastidor")
    # --- cuba: tramos de cilindro (para poder abollarla) y fondos abombados
    lados = 20 if lod == 0 else 12
    antes = set(t.bm.verts)
    n = 8 if v != 3 else (16 if lod == 0 else 12)                # la abollada necesita más anillos para que se vea el hundido
    paso = LC / n
    for k in range(n):
        y = -LC / 2 + paso / 2 + k * paso
        t.cilindro((0, y, ZE), RC, paso + 0.02, "Y", lados, "cuba")               # se solapan 2 cm: sin caras gemelas
    for s in (-1, 1):
        t.cilindro((0, s * (LC / 2 + 0.3), ZE), RC, 0.6, "Y", lados, "cuba", r2=RC * 0.62)
        t.cilindro((0, s * (LC / 2 + 0.69), ZE), RC * 0.6, 0.2, "Y", lados, "cuba", r2=RC * 0.3)   # 1 cm dentro: sin aro gemelo
    cuba = _nuevos(t, antes)
    if v == 3:
        _abollar(cuba, (1.4, -2.6, ZE + 0.4), 2.6, 1.0)           # en el costado +X, el que ve la cámara de juego
        _abollar(cuba, (1.3, 3.9, ZE - 0.2), 2.0, 0.7)
        _abollar(cuba, (0.4, -(LC / 2 + 0.6), ZE + 0.5), 0.9, 0.25)
    # --- boca de carga y pasarela de arriba
    t.cilindro((0, 0, ZE + RC + 0.12), 0.4, 0.3, "Z", 10 if lod == 0 else 8, "cuba")
    t.caja((0, 0, ZE + RC + 0.3), (0.95, 0.95, 0.08), "pasarela")
    t.caja((0, 0, ZE + RC + 0.02), (0.8, 4.0, 0.06), "pasarela")
    if v != 4:
        for x in (-0.45, 0.45):
            t.caja((x, 0, ZE + RC + 0.95), (g, 4.0, g), "escalera")              # barandillas de la pasarela
            for y in (-1.9, 0.0, 1.9):
                t.caja((x, y, ZE + RC + 0.5), (g, g, 0.95), "escalera")
    # --- plataforma del testero +Y con barandilla y escalera por el fondo
    yp = 8.25
    t.caja((0, yp, ZB + 0.25), (2.6, 0.9, 0.08), "pasarela")
    if v not in (4,):
        doblada = v == 2
        for x in (-1.25, 1.25):
            if doblada and x > 0:
                t.caja((x, yp, ZB + 0.75), (g, g, 1.0), "escalera", rot_x=R(35))    # poste doblado
            else:
                t.caja((x, yp, ZB + 0.8), (g, g, 1.0), "escalera")
        if doblada:
            t.caja((-0.35, yp + 0.4, ZB + 1.1), (1.8, g, g), "escalera", rot_y=R(14))
        else:
            t.caja((0, yp + 0.4, ZB + 1.3), (2.5, g, g), "escalera")
    if v in (0, 1, 3):   # escalera: dos largueros y peldaños (LOD1: una banda de 2 px con los largueros)
        ye = LC / 2 + 0.75
        z0, z1 = ZB + 0.3, ZE + RC + 0.05
        for x in (-0.25, 0.25):
            t.caja((x, ye, (z0 + z1) / 2), (g, g, z1 - z0), "escalera", rot_x=R(-14))
        if lod == 0:
            for k in range(8):
                z = z0 + (k + 0.5) * (z1 - z0) / 8
                dy = -(z - (z0 + z1) / 2) * math.tan(R(14))
                t.caja((0, ye + dy, z), (0.5, 0.04, 0.04), "escalera")
        else:
            t.caja((0, ye + 0.02, (z0 + z1) / 2), (0.5, 0.04, (z1 - z0) - 0.4), "escalera", rot_x=R(-14))
    elif v == 2:         # arranque de la escalera arrancada
        for x in (-0.25, 0.25):
            t.caja((x, LC / 2 + 0.85, ZB + 0.6), (g, g, 0.6), "escalera")
    # --- panel de la calcomanía de lleno o vacío (los dos costados)
    for s in (-1, 1):
        t.caja((s * (RC + 0.02), 0.0, ZE + 0.15), (0.04, 2.6, 1.1), "placa")
    # --- desgaste
    if v == 1:
        for (a, y, l, w) in ((-15, -5.5, 2.2, 0.9), (5, -1.5, 1.4, 0.7), (-35, 2.8, 1.8, 0.8), (0, 5.5, 2.4, 1.0),
                             (-45, -3.6, 1.0, 0.6)):
            _parche(t, a, y, l, w, "oxido")
    elif v == 4:
        for (a, y, l, w) in ((0, -4.0, 4.0, 1.4), (-20, 3.0, 3.0, 1.0), (25, 6.0, 1.6, 0.8)):
            _parche(t, a, y, l, w, "ceniza")
        for (a, y, l, w) in ((-10, -1.0, 1.6, 0.7), (-40, 5.0, 1.2, 0.6), (5, -6.5, 1.0, 0.6)):
            _parche(t, a, y, l, w, "oxido")
    return t.objeto(nombre, bisel=0)
