"""Talud de tierra del depósito de combustible de Castellón (cubeto de contención), en módulos de 10 m (Álvaro, 7-nov
09:14: lado norte, Z 33 de X 144 a 216, y lado este, X 216 de Z 6 a 33; unos 10 módulos). Teo, 7-nov.

SIN FOTO y medidas SIN VERIFICAR: caballón de tierra de 2,0 m de alto, 6,0 m de base y 1,2 m de coronación (taludes a
unos 34°), como el cubeto de un depósito de combustible. Módulo de 10 m a lo largo de Y de Blender (= Z de Unity con
yaw 0); los extremos son verticales para que dos módulos seguidos casen sin costura. Origen en el suelo, en el centro.
Colores: tierra con la coronación en 847c34 y los taludes en a86c50 (no en 845c28, que es el llano: se fundiría con el
suelo), matas 50541c, sacos 88885c (los del recinto), y la tierra removida de la brecha 6c4038. Normales hacia arriba
(como las matas y el suelo), para que los taludes en sombra no caigan en la banda de la horda.
Variantes (TBD_VARIANTE % 5):
  0 liso con matas;  1 coronado con una hilada de sacos (el depósito defendido);  2 erosionado: cárcavas de lluvia en
  el talud exterior;  3 módulo de esquina (los dos brazos de 5 m a 90°, para la esquina noreste en (216; 33));
  4 reventado (estación caída o ataque al depósito): brecha con un cráter y la tierra desparramada.
"""
import math
import os
from mathutils import Vector
from montaje import Montaje
from puesto_frente import _sacos

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["tierra", "mata", "sacos", "removida"]
COLORES = {"tierra": "a86c50", "mata": "50541c", "sacos": "88885c", "removida": "6c4038"}
VALOR_PIEZAS = {"tierra": ("847c34", "a86c50", "a86c50")}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (60, 2500), 1: (30, 1200)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.12}
H, BASE, CORONA, L = 2.0, 6.0, 1.2, 10.0
# perfil (x, z) en sentido antihorario visto desde -Y, con puntos intermedios en los taludes para poder erosionar
PERFIL = [(-BASE / 2, 0.0), (BASE / 2, 0.0), (BASE / 4 + CORONA / 4, H / 2), (CORONA / 2, H), (-CORONA / 2, H),
          (-BASE / 4 - CORONA / 4, H / 2)]


def _h(x, y, k=0):
    s = math.sin(x * 12.9898 + y * 78.233 + k * 19.19) * 43758.5453
    return s - math.floor(s)


def _brazo(m, y0, y1, fino):
    n = int(round((y1 - y0) / (1.0 if fino else 2.5)))
    secs = [(y0 + (y1 - y0) * k / n, 1.0, 1.0, 0.0) for k in range(n + 1)]
    antes = set(m.bm.verts)
    m.loft(PERFIL, secs, "tierra")
    return [w for w in m.bm.verts if w not in antes]


def _matas(m, pts, fino):
    for (x, y, z, s) in pts:
        m.caja((x, y, z + s * 0.3), (s, s * 0.8, s * 0.6), "mata", rot_z=_h(x, y) * 3.0)


def construir(nombre="SM_TaludDeposito", lod=0):
    v = VAR
    m = Montaje(PIEZAS, ["talud"])
    fino = lod == 0
    if v == 3:   # esquina: brazo a lo largo de Y (de -5 a +0,6) y brazo a lo largo de X (de -0,6 a +5), solapados
        vs = _brazo(m, -L / 2, CORONA / 2 + 0.6, fino)
        vs2 = _brazo(m, -L / 2, CORONA / 2 + 0.6, fino)
        for w in vs2:   # girado 90°: el segundo brazo sale hacia +X
            w.co.x, w.co.y = -w.co.y, w.co.x
        vs += vs2
    else:
        vs = _brazo(m, -L / 2, L / 2, fino)
    # irregularidad suave de los taludes (no en la base ni en los extremos, para que los módulos casen)
    for w in vs:
        if 0.1 < w.co.z < H - 0.1 and abs(abs(w.co.y) - L / 2) > 0.05:
            w.co.x += (_h(w.co.y, w.co.z) - 0.5) * 0.25
    if v == 2:   # cárcavas: surcos en el talud exterior (+X), del medio hacia abajo
        for yc in (-3.0, 0.5, 3.4):
            for w in vs:
                if w.co.x > 0.3 and w.co.z < H - 0.05 and abs(w.co.y - yc) < 0.9:
                    w.co.x -= 0.45 * (1 - abs(w.co.y - yc) / 0.9)
            m.caja((BASE / 2 + 0.5, yc, 0.08), (1.0, 1.2, 0.16), "removida", rot_z=_h(yc, 1) * 1.5)
    if v == 4:   # brecha: hunde la coronación en el centro y abre un cráter con la tierra desparramada
        for w in vs:
            d = abs(w.co.y)
            if d < 2.2 and w.co.z > 0.05:
                w.co.z *= 0.25 + 0.75 * (d / 2.2) ** 2
        for k in range(7):
            a = k * 0.9
            r = 3.6 + _h(k, 2) * 1.5
            m.caja((r * math.cos(a) * 0.9, r * math.sin(a) * 0.6, 0.12), (1.0 + _h(k, 3), 0.8, 0.24), "removida",
                   rot_z=a)
        m.caja((0, 0, 0.03), (2.6, 3.0, 0.06), "removida")
    if v == 1:   # una hilada doble de sacos en la coronación
        antes = set(m.bm.verts)
        _sacos(m, (0.0, -L / 2 + 0.3), (0.0, L / 2 - 0.3), fino, alto=0.6, grueso=0.8)
        for w in set(m.bm.verts) - antes:
            w.co.z += H - 0.02
    # matas en los taludes
    pts = []
    n = 14 if fino else 6
    for k in range(n):
        y = -L / 2 + 0.6 + (L - 1.2) * _h(k, 5)
        lado = 1 if k % 2 else -1
        t = 0.2 + 0.6 * _h(k, 6)                               # posición a lo largo del talud
        x = lado * (BASE / 2 - t * (BASE - CORONA) / 2)
        z = t * H - 0.1
        if v == 3 and y > CORONA / 2:
            continue
        if v == 4 and abs(y) < 2.4:
            continue
        pts.append((x, y, z, 0.5 if fino else 0.7))
    _matas(m, pts, fino)
    return m.objeto(nombre)
