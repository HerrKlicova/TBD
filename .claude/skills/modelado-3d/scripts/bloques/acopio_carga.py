"""Acopios de carga de las estaciones: munición, combustible y materiales (núcleo de Eugenio, 9-nov 21:59: «seleccionas el
tren dentro de la estación, eliges lo que cargar…»; persistencia: lo que hay en la estación se ve). Teo (4), 9-nov.

Completa el acopio de víveres de Nules (acopio_viveres, aprobado por Lucía el 8-nov) con los otros tres recursos que
carga el tren (TrenPreparar.Recurso: Municion, Combustible, Materiales). Misma huella, mismo origen y misma regla de
estados que los víveres (AcopioViveres.Estado: Lleno desde 2/3 de la capacidad de referencia, Mitad desde 1/3; Saqueado si
la estación cae y Quemado si arde), para que el código sirva para los cuatro cambiando solo el nombre del fichero.
Ocupa 10 × 6 m, con el largo a lo largo de X. Origen en el suelo, en el centro. Medidas SIN VERIFICAR (genéricas):
palé europeo 1,2 × 0,8 m; caja de munición de 0,8 × 0,4 × 0,3 m; bidón de 200 l (Ø 0,58 × 0,9 m); GRG/IBC de 1,2 × 1,0 ×
1,15 m; fardo de gaviones plegados de 1,2 × 1,0 × 0,6 m; saco terrero vacío en fardos; rollo de concertina de Ø 0,9 m.
Colores (nada rojo, nada ámbar, nada en la banda de la horda): cajas de munición 70784c con flejes 605c48, bidones 88885c
con tapa 6c7874, depósito del GRG b8b8b8 (cara pequeña) en jaula 94948c, gaviones 88885c, sacos b4a080, tablones 845c28,
concertina 6c7874, palés b4a080, quemado 605c48.
TBD_VARIANTE = 5 · tipo + estado (0..14). Tipos: 0 Municion, 1 Combustible, 2 Materiales. Estados: 0 Lleno, 1 Mitad,
2 Vacio, 3 Saqueado, 4 Quemado.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 15
TIPO, V = VAR // 5, VAR % 5
NOMBRES_T = ["Municion", "Combustible", "Materiales"]
NOMBRES_V = ["Lleno", "Mitad", "Vacio", "Saqueado", "Quemado"]
PIEZAS = ["caja", "fleje", "bidon", "tapa", "deposito", "jaula", "gavion", "saco", "tablon", "concertina", "palet",
          "quemado"]
COLORES = {"caja": "70784c", "fleje": "605c48", "bidon": "88885c", "tapa": "6c7874", "deposito": "b8b8b8",
           "jaula": "94948c", "gavion": "88885c", "saco": "b4a080", "tablon": "845c28", "concertina": "6c7874",
           "palet": "b4a080", "quemado": "605c48"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 3500), 1: (10, 1000)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
# huecos de palé: rejilla de 6 × 3 (x de −3,9 a 3,9; y de −1,8 a 1,8)
HUECOS = [(-3.9 + i * 1.56, -1.8 + j * 1.8) for i in range(6) for j in range(3)]


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def _cuantos(n):
    """Palés ocupados según el estado (de 18)."""
    return {0: 18, 1: 9, 2: 0, 3: 7, 4: 12}[V] if n is None else n


def construir(nombre="SM_AcopioCarga", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    q = V == 4
    P = (lambda p: "quemado") if q else (lambda p: p)
    lleno = _cuantos(None)
    for n, (x, y) in enumerate(HUECOS):
        rz = (_h(n, 1) - 0.5) * 0.1
        ocupado = n < lleno if V != 3 else _h(n, 7) < 0.4
        if V == 2 or (not ocupado and V != 3):
            if n % 3 == 0:                                         # palés vacíos apilados
                for f in range(4 if fino else 1):
                    t.caja((x, y, 0.07 + f * 0.145), (1.2, 0.8, 0.14), P("palet"), rot_z=rz)
                if not fino:
                    t.caja((x, y, 0.29), (1.2, 0.8, 0.58), P("palet"), rot_z=rz)
            elif V == 2 and n % 6 == 1:                            # vacío: un resto que dice qué había
                _suelto(t, x, y, n, fino)
            continue
        if V == 3 and not ocupado:                                 # saqueado: lo que queda por el suelo
            if _h(n, 8) < 0.5:
                t.caja((x, y, 0.07), (1.2, 0.8, 0.14), "palet", rot_z=rz + _h(n, 9))
                _suelto(t, x + 0.4, y + 0.3, n, fino)
            continue
        t.caja((x, y, 0.07), (1.2, 0.8, 0.14), P("palet"), rot_z=rz)
        if TIPO == 0:
            capas = 3 if (V in (0, 3) or n % 2) else 2
            if q:
                capas = 1 + (n % 2)
            if fino:
                for f in range(capas):
                    for a in range(2):
                        t.caja((x + (a - 0.5) * 0.6, y, 0.14 + 0.15 + f * 0.31), (0.58, 0.78, 0.3), P("caja"), rot_z=rz)
                t.caja((x, y, 0.14 + capas * 0.155), (1.22, 0.06, capas * 0.31 + 0.02), P("fleje"), rot_z=rz)
            else:
                t.caja((x, y, 0.14 + capas * 0.155), (1.18, 0.78, capas * 0.31), P("caja"), rot_z=rz)
        elif TIPO == 1:
            if n % 3 == 1 and not q:                               # GRG (IBC): depósito en jaula
                t.caja((x, y, 0.14 + 0.55), (1.0, 0.78, 1.0), "deposito", rot_z=rz)
                if fino:
                    for z in (0.3, 0.7, 1.1):
                        t.caja((x, y, 0.14 + z), (1.04, 0.82, 0.04), "jaula", rot_z=rz)
                t.caja((x, y, 0.14 + 1.1), (1.04, 0.82, 0.06), "jaula", rot_z=rz)
            else:
                for a in range(2):
                    for b in range(1 if not fino else 1):
                        cx = x + (a - 0.5) * 0.6
                        if q and a == 1 and n % 2:
                            t.cilindro((cx, y + 0.3, 0.3), 0.29, 0.9, "X", 8 if fino else 6, "quemado")   # volcado
                            continue
                        t.cilindro((cx, y, 0.14 + 0.45), 0.29, 0.9, "Z", 10 if fino else 6, P("bidon"))
                        t.cilindro((cx, y, 0.14 + 0.91), 0.27, 0.02, "Z", 10 if fino else 6, P("tapa"))
                        if fino and not q:
                            for z in (0.3, 0.6):
                                t.cilindro((cx, y, 0.14 + z), 0.3, 0.03, "Z", 10, "tapa")
        else:
            k = n % 3
            if k == 0:                                             # fardo de gaviones plegados
                h = 0.6 if not q else 0.3
                t.caja((x, y, 0.14 + h / 2), (1.2, 0.8, h), P("gavion"), rot_z=rz)
                if fino and not q:
                    for f in range(5):
                        t.caja((x, y, 0.14 + 0.06 + f * 0.11), (1.22, 0.82, 0.02), "concertina", rot_z=rz)
            elif k == 1:                                           # fardos de sacos terreros y tablones
                h = 0.5 if V != 1 else 0.3
                t.caja((x - 0.25, y, 0.14 + h / 2), (0.65, 0.78, h), P("saco"), rot_z=rz)
                for f in range(4 if fino else 1):
                    hh = 0.08 if fino else 0.32
                    t.caja((x + 0.33, y, 0.14 + hh / 2 + f * 0.08), (0.5, 0.78, hh), P("tablon"), rot_z=rz)
            else:                                                  # rollos de concertina
                for a in range(2 if not q else 1):
                    t.cilindro((x, y + (a - 0.5) * 0.42, 0.14 + 0.45), 0.45, 0.38, "Y", 10 if fino else 6,
                               P("concertina"))
    if q:
        t.caja((0, 0, 0.005), (9.5, 5.5, 0.01), "quemado")
    return t.objeto(nombre, bisel=0)


def _suelto(t, x, y, n, fino):
    """Una pieza del recurso tirada por el suelo (saqueado)."""
    rz = _h(n, 10) * 3
    if TIPO == 0:
        t.caja((x, y, 0.15), (0.8, 0.4, 0.3), "caja", rot_z=rz)
        if fino:
            t.caja((x + 0.5, y - 0.4, 0.2), (0.8, 0.3, 0.4), "caja", rot_z=rz + 0.6, rot_x=1.2)
    elif TIPO == 1:
        t.cilindro((x, y, 0.29), 0.29, 0.9, "X", 8 if fino else 6, "bidon")
    else:
        t.caja((x, y, 0.05), (2.0, 0.2, 0.05), "tablon", rot_z=rz)
        t.caja((x + 0.2, y + 0.3, 0.12), (0.6, 0.4, 0.24), "saco", rot_z=rz + 0.5)
