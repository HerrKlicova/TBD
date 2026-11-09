"""Pila de zombis contra el muro (encargo de Lucía, 8-nov 23:24). Teo, 8-nov.

En las corridas, la pila máxima tiene una mediana de 255 a 389 zombis. Con cientos de instancias amontonadas no se lee
ni rinde, así que a partir del umbral que fije Nacho las instancias sueltas se sustituyen por este tramo de pila (una
malla).

CELDA DE 0,5 m (x −0,25..0,25), como la celda de Nacho (8-nov 23:25): una instancia por celda, con la altura que diga
el número de zombis de la celda. Los cuerpos se salen 0,2 m por cada lado, así que las celdas vecinas se funden en un
solo tramo sin costura, aunque tengan alturas distintas. El núcleo y la colisión miden 0,5 m justos.
Origen en el suelo, al pie de la cara de fuera del muro. El muro corre a lo largo de X y FUERA es −Y, como en
muro_hesco_estados, así que la pila crece hacia −Y. Se apoya en el muro a la altura del estado y baja en rampa de unos
33°, por la que trepan los demás.
Estados (TBD_VARIANTE % 5) = alturas 1–5 de Nacho: 1,05 / 1,4 / 1,75 / 2,1 / 2,45 m contra el muro, para 3 / 4 / 5 / 6 / 7+
zombis por celda (con menos de 3 se ven sueltos). La altura 4 queda a ras del HESCO, y ahí empiezan a trepar. La 5 pasa
del muro: su cresta se monta 0,6 m sobre la coronación (y > 0) y hace de rampa. Lo que manda es el número de zombis por
celda, no los metros (Nacho). Las reglas para que no parpadee (vuelve a sueltos por debajo de 2 y cambia de altura como
mucho una vez por segundo) son de Marcos.
Semilla (TBD_VARIANTE // 5): 0, 1 o 2. Mismo perfil y otro reparto de cuerpos. Marcos la elige por el índice de la
celda, para que la misma malla no se repita cada 0,5 m.
REMATE (Lucía, 8-nov 24:20): TBD_REMATE=der | izq construye la pieza que cierra cada extremo de un tramo, para que no se
vea la cara lateral lisa de la cuña. Hay una por altura y semilla. «der» va a +X, pegada al borde de la última celda (su
origen x = 0 es ese borde), e «izq» es su espejo hacia −X; las dos se generan por separado para no usar escala
negativa. Es un núcleo convexo que baja en pendiente hacia fuera (40° de lado y 33° hacia −Y), y sus cuerpos se meten
0,2 m en la celda vecina. La colisión (TBD_COL=1 con TBD_REMATE) es el mismo casco convexo.
TBD_COL=1 construye la COLISIÓN: una cuña lisa con el mismo perfil (cara contra el muro, rampa y pie). Sirve para la
física y para que trepen, y no se ve.

Cuerpos: trozos de las cinco siluetas de la D-N80 (torsos, brazos, piernas, cabezas, el sombrero y el casco)
aplastados, cruzados y enredados. No hay ninguna figura entera; solo algunos brazos asoman hacia arriba en la parte
alta. Lleva la misma máscara de color de vértice que zombi_fino (R camisa, G pantalón y objetos, B piel, negro
calzado), así que vale el mismo material TBD/Horda de Bruno. El núcleo de la cuña va sin máscara (negro, #1c200c fijo): si fuera
teñido por instancia, cada celda de 0,5 m saldría de un color y la pila se vería a rayas.
Sin rojo, sin ámbar y sin gore.
"""
import math, os
from taller import Taller
from zombi_cercano import COLORES as _ZC, MASCARA_COLOR as _MC

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 15
ESTADO, SEMILLA = VAR % 5, VAR // 5
COL = os.environ.get("TBD_COL", "") == "1"
REMATE = os.environ.get("TBD_REMATE", "")            # "der" (+X) o "izq" (−X): remate del extremo de un tramo
SX = -1 if REMATE == "izq" else 1
PEND_LADO = math.radians(40)                         # caída lateral del remate
ALTOS = (1.05, 1.4, 1.75, 2.1, 2.45)
NOMBRES_E = ["Altura1", "Altura2", "Altura3", "Altura4", "Altura5"]
ANCHO = 0.5
SALE = 0.2                                            # cuánto se salen los cuerpos por cada lado
MURO = 2.1
PENDIENTE = math.radians(33)
PIEZAS = ["camisa", "piel", "pelo", "nuca", "pantalon", "calzado", "roto", "objeto", "nucleo"]
COLORES = dict(_ZC, objeto="442c0c", nucleo="1c200c")
MASCARA_COLOR = dict(_MC, objeto=(0, 1, 0), nucleo=(0, 0, 0))   # núcleo sin tinte: el mismo 1c200c en todas las celdas
VALOR_PIEZAS = {}
LEGIBILIDAD = "cuerpo_medio"
FACCION = "zombi"
PRESUPUESTO = {0: (40, 700), 1: (20, 400)}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233 + SEMILLA * 37.719) * 43758.5453
    return x - math.floor(x)


def perfil():
    """(alto contra el muro, largo de la rampa)."""
    h = ALTOS[ESTADO]
    return h, h / math.tan(PENDIENTE)


def _casco(t, pts, pieza):
    import bmesh
    vs = [t.bm.verts.new(p) for p in pts]
    res = bmesh.ops.convex_hull(t.bm, input=vs)
    for v in vs:                                     # vértices interiores que el casco no usa
        if v.is_valid and not v.link_faces:
            t.bm.verts.remove(v)
    caras = [g for g in res["geom"] if isinstance(g, bmesh.types.BMFace)]
    t._marcar(caras, pieza)
    return caras


def _pts_remate(h, L, enc=0.0):
    R_ = h / math.tan(PEND_LADO)
    pts = [(0, 0, 0), (0, 0, h - enc), (0, -L + enc, 0), (SX * (R_ - enc), 0, 0)]
    if h > MURO:                                     # cresta sobre la coronación
        pts += [(0, 0.6 - enc, h - 0.1 - enc), (0, 0.6 - enc, MURO), (SX * (R_ * (1 - MURO / h) - enc), 0.6 - enc, MURO)]
    return pts, R_


def construir(nombre="SM_PilaZombi", lod=0):
    t = Taller(PIEZAS)
    h, L = perfil()
    if REMATE:
        _casco(t, _pts_remate(h, L, 0.0 if COL else 0.05)[0], "nucleo")
        if COL:
            return t.objeto(nombre, bisel=0)
    if COL:                                          # cuña lisa: cara al muro (y = 0), rampa y pie
        t.caja((0, -L / 2, h / 2), (ANCHO, L, h), "nucleo", inclinar=L - 0.02)
        if h > MURO:                                 # cresta sobre la coronación del muro
            t.caja((0, 0.3, MURO + (h - MURO) / 2), (ANCHO, 0.62, h - MURO), "nucleo", inclinar=0.3)
        return t.objeto(nombre, bisel=0)
    fino = lod == 0
    m = 0.34                                         # detalle mínimo en LOD1 (2 px)
    # núcleo: la cuña, algo hundida y con los extremos en X recogidos para que dos tramos seguidos se solapen sin costura
    if not REMATE:
        t.caja((0, -L / 2 + 0.05, h / 2 - 0.04), (ANCHO + 0.02, L * 0.92, h - 0.08), "nucleo", inclinar=L * 0.9)
        if h > MURO:
            t.caja((0, 0.28, MURO + (h - MURO) / 2 - 0.03), (ANCHO + 0.02, 0.56, h - MURO), "nucleo", inclinar=0.28)
    R_ = h / math.tan(PEND_LADO)

    def z_sup(y, x=0.0):                             # altura de la rampa en y (−L..0) y de la cresta (y > 0)
        lado = max(0.0, x * SX) / R_ if REMATE else 0.0   # en el remate baja también hacia fuera
        if y > 0:
            return max(0.0, max(MURO, h - y * 0.6) - lado * h)
        return max(0.0, h * (1 + y / L - lado))

    ymax = 0.55 if h > MURO else -0.1
    area = (ANCHO + 2 * SALE) * (math.hypot(L, h) + 0.6) if not REMATE else (R_ / 2 + SALE) * (math.hypot(L, h) + 0.6)
    n = int(area * (6.0 if fino else 3.5)) + 3
    tipos = ("torso", "brazo", "pierna", "cabeza", "brazo", "pierna", "torso", "pie", "objeto")
    capa = t.bm.faces.layers.int.get("cuerpo") or t.bm.faces.layers.int.new("cuerpo")   # semilla por cuerpo (alfa)
    marca = [len(t.bm.faces), 0]

    def marcar(k):                                   # las caras nuevas desde la última marca son del cuerpo k
        t.bm.faces.ensure_lookup_table()
        for i in range(marca[0], len(t.bm.faces)):
            t.bm.faces[i][capa] = k
        marca[0] = len(t.bm.faces)

    for k in range(n):
        marcar(k)                                    # cierra el cuerpo anterior (k = 0: nada)
        tipo = tipos[int(_h(k, 1) * len(tipos))]
        if not fino and tipo in ("pie",):
            continue
        if REMATE:                                   # de −0,2 (dentro de la última celda) a R hacia fuera, más denso cerca
            x = SX * (-SALE + (_h(k, 2) ** 1.6) * (R_ + SALE))
        else:
            x = (_h(k, 2) - 0.5) * (ANCHO + 2 * SALE)
        y = ymax - _h(k, 3) * (L + 0.5 + ymax)              # unos 0,4 m más allá del pie de la rampa: cuerpos sueltos
        z = z_sup(y, x)
        if REMATE and z <= 0.0 and _h(k, 12) < 0.6:   # fuera del montón: pocos cuerpos sueltos
            continue
        rz = _h(k, 4) * math.pi * 2
        tilt = PENDIENTE * (0.6 + 0.8 * _h(k, 5)) if y > -L else _h(k, 5) * 0.3
        hund = 0.05 + 0.06 * _h(k, 6)                # medio enterrados en el montón
        if tipo == "torso":
            pieza = "camisa" if _h(k, 7) < 0.7 else "objeto"     # 30 %: chaleco o abrigo
            s = (0.42 + 0.12 * _h(k, 8), 0.24, 0.18 + 0.08 * _h(k, 9))
            if _h(k, 10) < 0.25:                     # el del gordo: más ancho
                s = (0.5, 0.36, 0.3)
            t.caja((x, y, z + s[2] / 2 - hund), s, pieza, rot_z=rz, rot_x=tilt)
        elif tipo in ("brazo", "pierna"):
            largo = (0.5 if tipo == "brazo" else 0.75) + 0.15 * _h(k, 8)
            g = (0.08 if tipo == "brazo" else 0.11) if fino else 0.12
            pieza = "camisa" if tipo == "brazo" else "pantalon"
            arriba = tipo == "brazo" and z > h * 0.55 and _h(k, 9) < 0.45 and ESTADO >= 2
            if arriba:                               # brazo que asoma hacia arriba en la parte alta (la horda trepa)
                ang = math.radians(25 + 35 * _h(k, 10))
                t.caja((x, y + math.sin(ang) * largo / 2, z + math.cos(ang) * largo / 2 - hund), (g, g, largo),
                       pieza, rot_x=-ang, rot_z=(_h(k, 11) - 0.5) * 0.8)
                t.caja((x, y + math.sin(ang) * (largo + 0.04), z + math.cos(ang) * (largo + 0.04) - hund),
                       (0.08, 0.09, 0.11) if fino else (0.12, 0.12, 0.12), "piel", rot_x=-ang)
            else:                                    # tendido, cruzado sobre el montón
                t.caja((x, y, z + g / 2 - hund * 0.5), (g, largo, g), pieza, rot_z=rz, rot_x=tilt * 0.8)
        elif tipo == "cabeza":
            s = 0.21 if fino else 0.24
            t.caja((x, y, z + s / 2 - hund), (s, s, s), "piel", rot_z=rz, rot_x=_h(k, 8))
            if fino and _h(k, 9) < 0.5:
                t.caja((x, y, z + s - hund + 0.01), (s + 0.01, s * 0.55, 0.03), "pelo", rot_z=rz)
        elif tipo == "pie":
            t.caja((x, y, z + 0.03), (0.1, 0.2, 0.07), "calzado", rot_z=rz)
        else:                                        # objetos sueltos: el ala del sombrero, el casco, una mochila
            q = _h(k, 8)
            if q < 0.35:
                t.cilindro((x, y, z + 0.02), 0.36 if fino else 0.4, 0.03, "Z", 8, "objeto")
            elif q < 0.65:
                t.esfera((x, y, z + 0.05), (0.24, 0.24, 0.11), "objeto", 1)
            else:
                t.caja((x, y, z + 0.12 - hund), (0.3, 0.18, 0.36), "objeto", rot_z=rz, rot_x=1.2)
    marcar(n)
    return t.objeto(nombre, bisel=0)


_construir = construir


def construir(nombre="SM_PilaZombi", lod=0):
    """El origen va al pie de la cara de fuera del muro (x = 0, y = 0, z = 0), no al centro de la base: entregar.py
    llevaba el origen al centro de la caja (con la rampa, unos 1,3–1,9 m hacia fuera) y la pila quedaba metida en el
    muro (Teo, 9-nov, al encajarla delante de las puertas de la D-N151)."""
    o = _construir(nombre, lod)
    o["tbd_base_fija"] = [0.0, 0.0, 0.0]
    return o
