"""Valla de la base de Bétera, tramo de 10 m (D-N173; Álvaro, betera-nivel.md §2 y §5.5: malla de 2,5 m con concertina;
retrasa a la horda, pero no la para). Teo (3), 8-nov.

Foto AE1_betera_torre_1 de Paula: postes de tubo con el remate acodado HACIA FUERA y tres hilos de espino, malla de
simple torsión. Medidas SIN VERIFICAR: 2,5 m de malla, remate de 0,5 m a 45°, postes cada 2,5 m.
Colores: postes 6c7874, malla 94948c (ni b8b8b8, que en una pieza larga se confunde con el contorno de selección), espino
y concertina 48403c, lona 50541c, hierba al pie no (la pone el suelo).
Disposición: origen en el suelo, en el centro del tramo; la valla a lo largo de X (−5..5) y FUERA = −Y (hacia allí se
acoda el remate). Se repite cada 10 m; los postes van en x = −3,75, −1,25, 1,25 y 3,75, así que dos tramos seguidos no
duplican poste. En el juego la malla es una lámina: a 0,17 m/px el rombo no se ve.
Variantes (TBD_VARIANTE % 5): 0 sana; 1 combada hacia dentro (la empujó la horda); 2 rota, con un paño caído y un hueco;
3 con lona de ocultación; 4 con la concertina suelta en el suelo por fuera.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["poste", "malla", "espino", "lona"]
COLORES = {"poste": "6c7874", "malla": "94948c", "espino": "48403c", "lona": "50541c"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.7
PRESUPUESTO = {0: (20, 900), 1: (10, 400)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
L, H = 10.0, 2.5
POSTES = (-3.75, -1.25, 1.25, 3.75)


def construir(nombre="SM_ValladeBase", lod=0):
    v = VAR
    t = Taller(PIEZAS)
    fino = lod == 0
    gp = 0.08 if fino else 0.34
    comba = lambda x: (0.5 * math.cos(math.pi * x / L)) if v == 1 else 0.0    # desplazamiento hacia dentro (+Y)
    for k, x in enumerate(POSTES):
        dy = comba(x)
        inc = math.atan2(dy, H) if v == 1 else 0.0
        t.caja((x, dy / 2, H / 2), (gp, gp, H), "poste", rot_x=-inc)
        t.caja((x, dy - 0.18, H + 0.18), (gp, gp, 0.55), "poste", rot_x=math.radians(-45))   # remate hacia fuera
    # malla: paños entre postes (y medio paño hasta cada extremo)
    bordes = [-L / 2] + list(POSTES) + [L / 2]
    for i in range(len(bordes) - 1):
        x0, x1 = bordes[i], bordes[i + 1]
        xc, lx = (x0 + x1) / 2, x1 - x0
        if v == 2 and i == 2:                        # paño caído hacia dentro: hueco
            t.caja((xc, 1.1, 0.06), (lx - 0.1, 2.2, 0.04), "malla", rot_z=0.05)
            continue
        dy = comba(xc)
        inc = math.atan2(dy, H) if v == 1 else 0.0
        hm = H - (0.6 if (v == 2 and i == 1) else 0.0)
        t.caja((xc, dy / 2, hm / 2 + 0.03), (lx - 0.02, 0.03 if fino else 0.06, hm), "malla", rot_x=-inc)
        if v == 3:
            t.caja((xc, dy / 2 + 0.05, 1.0), (lx - 0.06, 0.03 if fino else 0.06, 1.8), "lona", rot_x=-inc)
    # hilos de espino sobre los remates (en el LOD1, uno grueso)
    for j in range(3 if fino else 1):
        d = 0.15 + 0.17 * j
        t.caja((0, -d, H + d + 0.02), (L - 0.02, 0.03 if fino else 0.34, 0.03 if fino else 0.2), "espino")
    if v == 4:                                       # concertina suelta, por fuera
        for k in range(3):
            t.cilindro((-3.2 + 3.2 * k, -1.2 - 0.3 * (k % 2), 0.32), 0.32, 2.6, "X", 8 if fino else 6, "espino")
    return t.objeto(nombre, bisel=0)
