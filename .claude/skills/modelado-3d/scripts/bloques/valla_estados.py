"""Valla de la base de Bétera por estados de vida: tramo de 10 m (kit de defensas, Mak 8-nov 13:46; 5000 HP por tramo,
betera-nivel.md §5.5: retrasa a la horda, no la para). Teo (3), 8-nov.

Sale de la valla aprobada (valla_base.py, revisión 2 con las fotos BT5 de Paula): malla de simple torsión de 2,5 m,
postes cada 3,33 m en x = −3,33, 0 y 3,33 con el remate acodado hacia fuera (−Y) y tres hilos de espino. Dos aspectos:
malla sola (BT5_1) y malla sobre muro bajo enfoscado de 1,2 m (BT5_2). Mismos colores: postes 6c7874, malla 94948c,
espino 48403c, muro 94948c con valor b4a080/88885c; tierra removida b4a080 y cascotes del muro 88885c.
Estados por la vida que le queda: los 4 aprobados (construccion-reparar.md §1; Álvaro, 8-nov 13:51), con «sano»
partido en dos solo a la vista, para que la lógica y el coste de Hugo no cambien:
  0 sana (≥ 85 %); 1 sana pero golpeada (70–85 %: un paño abombado hacia dentro y un hilo de espino colgando);
  2 dañada (30–70 %: todo el tramo empujado hacia dentro, dos postes inclinados y el espino suelto); 3 muy dañada
  (1–30 %: un paño caído y otro arrancado a medias, postes tumbados; en el muro bajo, un trozo de muro caído);
  4 brecha (0 HP: la malla en el suelo pisoteada, ya no bloquea; del muro bajo quedan restos de 0,4 m).
Disposición: origen en el suelo, en el centro del tramo, a lo largo de X; FUERA = −Y (la horda empuja desde fuera, así
que todo cae hacia dentro, +Y).
TBD_VARIANTE = 5 · aspecto + estado (0..9). Aspectos: 0 Malla, 1 MuroBajo.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 10
ASPECTO, ESTADO = VAR // 5, VAR % 5
NOMBRES_A = ["Malla", "MuroBajo"]
NOMBRES_E = ["Sana", "SanaGolpeada", "Danada", "MuyDanada", "Brecha"]
PIEZAS = ["poste", "malla", "espino", "muro", "tierra", "cascote"]
COLORES = {"poste": "6c7874", "malla": "94948c", "espino": "48403c", "muro": "94948c", "tierra": "b4a080",
           "cascote": "88885c"}
VALOR_PIEZAS = {"muro": ("b4a080", "88885c")}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.7
PRESUPUESTO = {0: (10, 1500), 1: (6, 600)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
L, H = 10.0, 2.5
POSTES = (-10 / 3, 0.0, 10 / 3)
BORDES = (-5.0,) + POSTES + (5.0,)


def construir(nombre="SM_VallaEstados", lod=0):
    e = ESTADO
    t = Taller(PIEZAS)
    fino = lod == 0
    gp = 0.08 if fino else 0.34
    gm = 0.03 if fino else 0.06
    zm = 1.2 if ASPECTO == 1 else 0.0
    # muro bajo (aspecto 1)
    if ASPECTO == 1:
        if e <= 2:
            t.caja((0, 0, zm / 2), (L, 0.3, zm), "muro")
            t.caja((0, 0, zm + 0.04), (L, 0.38, 0.08), "muro")
        elif e == 3:                                 # un trozo del muro caído hacia dentro
            for x0, x1 in ((-5.0, -0.6), (2.2, 5.0)):
                t.caja(((x0 + x1) / 2, 0, zm / 2), (x1 - x0, 0.3, zm), "muro")
                t.caja(((x0 + x1) / 2, 0, zm + 0.04), (x1 - x0, 0.38, 0.08), "muro")
            t.caja((0.8, 0.0, 0.2), (2.7, 0.28, 0.4), "muro")
            for k, (x, y) in enumerate(((0.2, 0.7), (1.3, 0.9), (0.9, 1.5))):
                t.caja((x, y, 0.15), (0.6, 0.4, 0.3), "cascote", rot_z=0.7 * k)
        else:                                        # restos de 0,4 m
            t.caja((0, 0, 0.2), (L, 0.3, 0.4), "muro")
            for k in range(4 if fino else 2):
                t.caja((-3.5 + 2.4 * k, 0.8 + 0.3 * (k % 2), 0.15), (0.7, 0.45, 0.3), "cascote", rot_z=0.6 * k)
    # postes: (x, inclinación hacia dentro en rad, en pie)
    incl = {0: (0, 0, 0), 1: (0, 0.08, 0), 2: (0.25, 0.35, 0.2), 3: (0.4, 1.35, 0.1), 4: (1.45, 1.5, 1.4)}[e]
    for x, a in zip(POSTES, incl):
        hp = H - zm
        if a > 1.2:                                  # tumbado en el suelo
            t.caja((x, hp / 2, max(zm if e < 4 else 0.0, 0.0) + 0.05), (gp, hp, gp), "poste")
            continue
        cy = math.sin(a) * hp / 2
        t.caja((x, cy, zm + math.cos(a) * hp / 2), (gp, gp, hp), "poste", rot_x=-a)
        tz = zm + math.cos(a) * hp
        ty = math.sin(a) * hp
        t.caja((x, ty - 0.18, tz + 0.18), (gp, gp, 0.55), "poste", rot_x=math.radians(-45) - a)
    # malla por paños
    for i in range(len(BORDES) - 1):
        x0, x1 = BORDES[i], BORDES[i + 1]
        xc, lx = (x0 + x1) / 2, x1 - x0 - 0.02
        hm = H - zm
        a = (incl[min(i, 2)] + incl[max(i - 1, 0)]) / 2 if e in (2, 3) else 0.0
        if e == 1 and i == 2:                        # paño abombado hacia dentro
            a = 0.18
        if e == 4 or (e == 3 and i == 1):            # paño en el suelo, hacia dentro
            t.caja((xc, hm / 2 + 0.15, 0.04 + (zm if e == 3 else 0.0) * 0), (lx, hm, gm), "malla", rot_z=0.04 * (i - 1))
            continue
        if e == 3 and i == 2:                        # arrancado a medias: cuelga de un lado
            t.caja((xc, 0.6, zm + hm * 0.3), (lx, gm, hm * 0.6), "malla", rot_x=-0.9)
            continue
        if a > 1.0:
            t.caja((xc, hm / 2, zm + 0.06), (lx, hm, gm), "malla")
            continue
        t.caja((xc, math.sin(a) * hm / 2, zm + math.cos(a) * hm / 2 + 0.03), (lx, gm, hm), "malla", rot_x=-a)
    # espino: tres hilos sobre los remates; suelto o en el suelo según el estado
    if e == 0:
        for j in range(3 if fino else 1):
            d = 0.15 + 0.17 * j
            t.caja((0, -d, H + d + 0.02), (L - 0.02, 0.03 if fino else 0.34, 0.03 if fino else 0.2), "espino")
    elif e in (1, 2):
        sh = 0.0 if e == 1 else 0.6
        for j in range(2 if fino else 1):
            d = 0.15 + 0.17 * j
            t.caja((-2.5, -d + sh, H + d + 0.02 - sh * 0.5), (5.0, 0.03 if fino else 0.34, 0.03 if fino else 0.2),
                   "espino")
        w = 0.04 if fino else 0.2
        t.caja((2.4, 0.2 + sh, H - 0.6), (4.6, w, w), "espino", rot_y=0.25)       # hilo colgando
    else:
        w = 0.04 if fino else 0.2
        for j in range(2):
            t.caja((-1.0 + 2.0 * j, 1.6 + 0.4 * j, 0.06), (5.5, w, w), "espino", rot_z=0.15 - 0.3 * j)
    if e >= 3:                                       # tierra removida al pie, por fuera
        t.caja((-0.5, -1.0, 0.06), (4.0, 1.2, 0.12), "tierra", rot_z=0.1)
    return t.objeto(nombre, bisel=0)
