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
UMBRAL_CONTORNO = {0: 0.26, 1: 0.26}   # 0,26 = el umbral del juego (D-N129); con 0,12 la cresta de la esquina daba una banda negra
H, BASE, CORONA, L = 2.0, 6.0, 1.2, 10.0
# perfil (x, z) en sentido antihorario visto desde -Y, con puntos intermedios en los taludes para poder erosionar
PERFIL = [(-BASE / 2, 0.0), (BASE / 2, 0.0), (BASE / 4 + CORONA / 4, H / 2), (CORONA / 2, H), (-CORONA / 2, H),
          (-BASE / 4 - CORONA / 4, H / 2)]


def _h(x, y, k=0):
    s = math.sin(x * 12.9898 + y * 78.233 + k * 19.19) * 43758.5453
    return s - math.floor(s)


def _ancho(z):
    """Semiancho del caballón a la altura z."""
    return CORONA / 2 + (H - z) * (BASE / 2 - CORONA / 2) / H


def _altura(x, y, brecha=False):
    """Altura de la superficie en (x, y) (para apoyar terrones y regueros)."""
    h = max(0.0, min(H, (BASE / 2 - abs(x)) / (BASE / 2 - CORONA / 2) * H))
    if brecha and abs(y) < 2.2:
        h *= 0.25 + 0.75 * (abs(y) / 2.2) ** 2
    return h


def _brazo(m, y0, y1, fino, inglete=False):
    """inglete: el extremo y1 se recorta por el talud del otro brazo (esquina), así no asoma su tapa vertical."""
    n = int(round((y1 - y0) / (1.0 if fino else 2.5)))
    secs = [(y0 + (y1 - y0) * k / n, 1.0, 1.0, 0.0) for k in range(n + 1)]
    antes = set(m.bm.verts)
    m.loft(PERFIL, secs, "tierra")
    nuevos = [w for w in m.bm.verts if w not in antes]
    if inglete:
        for w in nuevos:
            if abs(w.co.y - y1) < 1e-4:
                w.co.y = _ancho(w.co.z) - 0.05
    return nuevos


def _matas(m, pts, fino):
    for (x, y, z, s) in pts:
        m.caja((x, y, z + s * 0.3), (s, s * 0.8, s * 0.6), "mata", rot_z=_h(x, y) * 3.0)


def construir(nombre="SM_TaludDeposito", lod=0):
    v = VAR
    m = Montaje(PIEZAS, ["talud"])
    fino = lod == 0
    if v == 3:   # esquina: brazo a lo largo de Y (de -5 a +0,6) y brazo a lo largo de X (de -0,6 a +5), solapados
        vs = _brazo(m, -L / 2, 0.0, fino, inglete=True)
        vs2 = _brazo(m, -L / 2, 0.0, fino, inglete=True)
        for w in vs2:   # girado 90°: el segundo brazo sale hacia +X
            w.co.x, w.co.y = -w.co.y, w.co.x
        vs += vs2
    else:
        vs = _brazo(m, -L / 2, L / 2, fino)
    # irregularidad suave de los taludes (no en la base ni en los extremos, para que los módulos casen)
    for w in vs:
        if 0.1 < w.co.z < H - 0.1 and abs(abs(w.co.y) - L / 2) > 0.05 and abs(w.co.y) > 3.2 * (v == 3):
            w.co.x += (_h(w.co.y, w.co.z) - 0.5) * 0.25
    if v == 2:   # regueros (Lucía 09:17: nada de manchas cuadradas): surcos de tierra removida que bajan serpenteando
        pend = math.atan2(H, BASE / 2 - CORONA / 2)          # por el talud exterior (+X) y abren un abanico al pie
        for i, yc in enumerate((-3.4, -0.8, 1.2, 3.6)):
            y = yc
            pasos = 6 if fino else 3
            for k in range(pasos):
                t = (k + 0.5) / pasos
                x = CORONA / 2 + 0.2 + t * (BASE / 2 - CORONA / 2 - 0.2)
                y += (_h(i, k, 7) - 0.5) * (0.5 if fino else 0.8)
                z = _altura(x, y) + 0.03
                w = (0.18 + 0.25 * t) if fino else max(0.36, 0.2 + 0.4 * t)
                m.caja((x, y, z), ((BASE / 2 - CORONA / 2) / pasos / math.cos(pend) + 0.1, w, 0.06), "removida",
                       rot_y=-pend, rot_z=(_h(i, k, 8) - 0.5) * 0.6)
            for j in range(3 if fino else 2):              # abanico al pie, en trozos de distinto tamaño
                m.caja((BASE / 2 + 0.3 + 0.35 * j, y + (_h(i, j, 9) - 0.5) * 0.8, 0.04),
                       (0.5 + 0.3 * _h(i, j, 10), 0.4 + 0.5 * _h(i, j, 11), 0.08), "removida", rot_z=_h(i, j, 12) * 2)
    if v == 4:   # brecha: hunde la coronación en el centro y abre un cráter con la tierra desparramada
        for w in vs:
            d = abs(w.co.y)
            if d < 2.2 and w.co.z > 0.05:
                w.co.z *= 0.25 + 0.75 * (d / 2.2) ** 2
        for k in range(9):   # terrones apoyados en la superficie, en la brecha y a los dos lados del pie
            x = (-1 if k % 2 else 1) * (0.4 + 4.0 * _h(k, 2))
            y = (_h(k, 4) - 0.5) * 4.0
            s_ = 0.5 + 0.6 * _h(k, 3)
            m.caja((x, y, _altura(x, y, brecha=True) + s_ * 0.2 - 0.05), (s_, s_ * 0.8, s_ * 0.4), "removida",
                   rot_z=k * 0.9)
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
