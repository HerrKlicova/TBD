"""Cartel con el nombre de la estación de Sagunt (tablón del MVP, «si sobra tiempo»; Lucía 8-nov 18:00). Teo (3), 8-nov.

Cartel de andén de 3,2 × 0,7 m con «SAGUNT» en letras de bloque (fuente de 3 × 5 celdas, para que a tamaño de juego
se lea como un rótulo aunque no se lean las letras). Sin logotipos. Panel b8b8b8 con marco 6c7874, letras 605c48 (48403c en vertical caía en la banda), postes
6c7874, tabla 845c28. Nada rojo. El texto se lee desde −Y (la cara del andén). Medidas SIN VERIFICAR.
Origen: en el suelo, entre los dos postes (o en el punto de cuelgue, en la v1, a 4 m de alto).
Variantes (TBD_VARIANTE % 5): 0 en dos postes (2,4 m al borde de abajo); 1 colgado de la marquesina con dos varillas;
2 torcido (un poste doblado); 3 caído en el suelo, boca arriba; 4 con una tabla clavada encima (tapado a medias).
"""
import math, os
from taller import Taller

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Postes", "Colgado", "Torcido", "Caido", "Tapado"]
PIEZAS = ["panel", "marco", "letra", "poste", "tabla"]
COLORES = {"panel": "b8b8b8", "marco": "6c7874", "letra": "605c48", "poste": "6c7874", "tabla": "845c28"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.8
PRESUPUESTO = {0: (12, 900), 1: (8, 300)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
FUENTE = {"S": ["###", "#..", "###", "..#", "###"], "A": ["###", "#.#", "###", "#.#", "#.#"],
          "G": ["###", "#..", "#.#", "#.#", "###"], "U": ["#.#", "#.#", "#.#", "#.#", "###"],
          "N": ["##.", "#.#", "#.#", "#.#", "#.#"], "T": ["###", ".#.", ".#.", ".#.", ".#."]}
LX, LZ = 3.2, 0.7


def construir(nombre="SM_CartelEstacion", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.1 if fino else 0.34
    v = V
    # el panel se construye de pie, centrado en (0, 0, zc); luego cada variante lo coloca
    zc = {0: 2.75, 1: 3.0, 2: 2.75, 3: 0.1, 4: 2.75}[v]
    giro_x = {3: math.radians(90)}.get(v, 0.0)        # caído: tumbado boca arriba
    giro_y = {2: math.radians(14)}.get(v, 0.0)        # torcido
    def P(x, z):                                      # posición de un punto del panel (x a lo largo, z hacia arriba)
        xr = x * math.cos(giro_y) - z * math.sin(giro_y)
        zr = x * math.sin(giro_y) + z * math.cos(giro_y)
        if giro_x:
            return (xr, zr, zc)
        return (xr, 0.0, zc + zr)
    def caja(x, z, sx, sz, grosor, dy, pieza):
        cx, cy, cz = P(x, z)
        if giro_x:
            t.caja((cx, cy, cz + dy), (sx, sz, grosor), pieza, rot_z=0.15)
        else:
            t.caja((cx, cy + dy, cz), (sx, grosor, sz), pieza, rot_y=-giro_y)
    caja(0, 0, LX + 0.12, LZ + 0.12, 0.08, 0.0, "marco")
    caja(0, 0, LX, LZ, 0.1, 0.0, "panel")
    if not fino:                                      # LOD1: a 20 px las letras son una banda oscura
        caja(0, 0, LX - 0.8, 0.34, 0.03, -0.065 if not giro_x else 0.065, "letra")
    else:                                             # letras de bloque (3 × 5 celdas)
        texto = "SAGUNT"
        cel = 0.09 if fino else 0.11
        ancho = len(texto) * 4 * cel - cel
        x0 = -ancho / 2
        for i, ch in enumerate(texto):
            for fila, linea in enumerate(FUENTE[ch]):
                for col, c in enumerate(linea):
                    if c != "#":
                        continue
                    x = x0 + (i * 4 + col) * cel + cel / 2
                    z = (2 - fila) * cel
                    caja(x, z, cel * 1.08, cel * 1.08, 0.03 + 0.002 * col, -0.065 if not giro_x else 0.065, "letra")
    if v in (0, 2, 4):                                # dos postes
        for s in (-1, 1):
            x = s * (LX / 2 - 0.3)
            if v == 2 and s > 0:                      # poste doblado
                t.caja((x, 0.05, 0.9), (g, g, 1.8), "poste")
                t.caja((x + 0.15, 0.05, 2.4), (g, g, 1.3), "poste", rot_y=-0.35)
                continue
            t.caja((x, 0.05, (zc + LZ / 2) / 2), (g, g, zc + LZ / 2), "poste")
    if v == 1:                                        # colgado: varillas hasta la marquesina (4 m)
        for s in (-1, 1):
            t.caja((s * (LX / 2 - 0.3), 0, (zc + LZ / 2 + 4.0) / 2), (g * 0.6 if fino else 0.34, g * 0.6 if fino else 0.34,
                                                                    4.0 - zc - LZ / 2), "poste")
    if v == 3:                                        # los postes partidos, en el suelo
        for s in (-1, 1):
            t.caja((s * 1.3, 0.9, 0.05), (g, 2.0, g), "poste", rot_z=0.2 * s)
    if v == 4:                                        # tabla clavada de través
        t.caja((0.4, -0.14, zc), (2.0, 0.04, 0.3), "tabla", rot_y=0.2)
    return t.objeto(nombre, bisel=0)
