"""Acopio de víveres para el almacén citrícola de Nules (Lucía, 8-nov 18:28: «nave con muelle y cajas apiladas, que hace
de almacén de víveres»). Teo (3), 8-nov.

La nave ya existe (la cooperativa, mr-mak e774d0a, en X 74–164 / Z 2–29) y el muelle también (muelle_carga de Nules).
Esto es lo que faltaba: las cajas apiladas que dicen cuánto hay en el almacén, para el patio de carga (X 164–192) y
el muelle. Paula (betera_rearme_y_nules_viveres.md): la fruta iba del huerto al almacén y de allí al tren; Nules es la
cuna de la Clemenules. Palots de la Plana (cajones de plástico de 1,2 × 1,0 × 0,76 m, apilables en llenos de 2 y en
vacíos de 5) y cajas de campo de madera de 0,6 × 0,4 × 0,3 m. Medidas SIN VERIFICAR (genéricas).
Origen en el suelo, en el centro del acopio, que ocupa 10 × 6 m (largo a lo largo de X).
Colores: palots 6c7874, fruta f0dc9c (limón y clementina pálida: nada naranja, el ámbar es para avisos), cajas de
madera 845c28, palés b4a080, sacos de harina b8b8b8, fruta podrida y quemado 605c48. Nada rojo ni en la banda.
Variantes (TBD_VARIANTE % 5): 0 Lleno; 1 Mitad; 2 Vacio (palots vacíos apilados de 5 y cajas vacías boca abajo);
3 Saqueado (palots volcados, fruta por el suelo, cajas caídas); 4 Quemado (restos en hollín, palots derretidos).
"""
import math, os
from taller import Taller

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Lleno", "Mitad", "Vacio", "Saqueado", "Quemado"]
PIEZAS = ["palot", "fruta", "caja", "palet", "harina", "quemado"]
COLORES = {"palot": "6c7874", "fruta": "f0dc9c", "caja": "845c28", "palet": "b4a080", "harina": "b8b8b8",
           "quemado": "605c48"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 3000), 1: (10, 900)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_AcopioViveres", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    v = V
    q = v == 4
    P = (lambda p: "quemado") if q else (lambda p: p)
    # palots: rejilla de 3 × 3 pilas en la mitad oeste (x −4,6 … −0,8)
    for i in range(3):
        for j in range(3):
            n = i * 3 + j
            x, y = -4.0 + i * 1.3, -1.6 + j * 1.6
            rz = (_h(n, 1) - 0.5) * 0.12
            if v == 0:
                alto = 2
            elif v == 1:
                alto = 2 if n < 3 else (1 if n < 6 else 0)
            elif v == 2:
                alto = 5 if n % 2 == 0 else 3
            elif v == 3:
                alto = 1 if _h(n, 2) < 0.5 else 0
            else:
                alto = 1 if _h(n, 2) < 0.7 else 0
            lleno = v in (0, 1, 3)
            if lleno or v == 4:
                for f in range(alto):
                    t.caja((x, y, f * 0.77 + 0.38), (1.2, 1.0, 0.76), P("palot"), rot_z=rz)
            elif alto:
                t.caja((x, y, 0.38), (1.2, 1.0, 0.76), "palot", rot_z=rz)
            if alto and v == 2:                                       # pila de vacíos: un bloque alto con el borde
                t.caja((x, y, 0.765 + (alto - 1) * 0.2 / 2), (1.22, 1.02, (alto - 1) * 0.2), "palot", rot_z=rz)
            if alto and lleno:
                t.caja((x, y, alto * 0.77 - 0.01), (1.05, 0.85, 0.06), "fruta", rot_z=rz)
            if v == 3 and alto == 0:                                  # volcado, con la fruta por el suelo
                t.caja((x + 0.3, y, 0.5), (1.2, 0.76, 1.0), "palot", rot_z=rz + 0.4)
                t.caja((x + 1.0, y + 0.2, 0.03), (1.2, 1.0, 0.06), "fruta", rot_z=_h(n, 3) * 3)
    # cajas de campo apiladas sobre palés, en la mitad este (x 0,4 … 4,6)
    for i in range(3):
        for j in range(3):
            n = 20 + i * 3 + j
            x, y = 1.0 + i * 1.4, -1.6 + j * 1.6
            if v == 1 and n >= 23:
                continue
            if v == 3 and _h(n, 4) < 0.45:
                t.caja((x, y, 0.15), (0.6, 0.4, 0.3), "caja", rot_z=_h(n, 5) * 3)
                t.caja((x + 0.5, y + 0.4, 0.15), (0.6, 0.4, 0.3), "caja", rot_z=_h(n, 6) * 3)
                continue
            if q and _h(n, 4) < 0.5:
                continue
            harina = j == 2 and v in (0, 1)
            t.caja((x, y, 0.07), (1.2, 1.0, 0.14), P("palet"))
            capas = {0: 5, 1: 3, 2: 4, 3: 2, 4: 2}[v]
            if harina:
                capas = 4
                if fino:
                    for f in range(capas):
                        t.caja((0 + x, y, 0.145 + 0.13 + f * 0.26), (1.15, 0.95, 0.25), "harina")
                else:
                    t.caja((x, y, 0.145 + capas * 0.13), (1.15, 0.95, capas * 0.26), "harina")
                continue
            ztop = 0.145 + capas * 0.31
            if fino:
                for f in range(capas):
                    for a in range(2):
                        for b in range(2):
                            t.caja((x + (a - 0.5) * 0.6, y + (b - 0.5) * 0.49, 0.145 + 0.15 + f * 0.31),
                                   (0.58, 0.47, 0.3), P("caja"))
            else:
                t.caja((x, y, (0.145 + ztop) / 2), (1.18, 0.98, capas * 0.31), P("caja"))
            if v in (0, 1, 3):                                        # fruta en las cajas de arriba
                t.caja((x, y, ztop + 0.005), (1.0, 0.8, 0.02), "fruta")
    if q:                                                             # mancha de quemado
        t.caja((0, 0, 0.005), (9.0, 5.0, 0.01), "quemado")
    return t.objeto(nombre, bisel=0)
