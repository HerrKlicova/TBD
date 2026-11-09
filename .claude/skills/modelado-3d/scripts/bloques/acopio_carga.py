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
          "quemado", "base"]
# Pase de contraste (Bruno, 9-nov 23:18, captura qa52 de la I-Noche112 a z26): flejes y tapas más claros, y una base
# oscura bajo cada palé y en las juntas (48403c, L* 28: D-N80 deja hasta ahí; ΔL* ≥ 8 con todo lo que tiene encima).
COLORES = {"caja": "70784c", "fleje": "b4a080", "bidon": "88885c", "tapa": "b8b8b8", "deposito": "b8b8b8",
           "jaula": "94948c", "gavion": "88885c", "saco": "b4a080", "tablon": "845c28", "concertina": "6c7874",
           "palet": "b4a080", "quemado": "605c48", "base": "48403c"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 4500), 1: (10, 1800)}
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
                t.caja((x, y, 0.02), (1.28, 0.88, 0.04), P("base"), rot_z=rz)
                for f in range(4 if fino else 1):
                    t.caja((x, y, 0.11 + f * 0.145), (1.2, 0.8, 0.14), P("palet"), rot_z=rz)
                    if fino and f < 3:
                        t.caja((x, y, 0.18 + f * 0.145), (1.21, 0.81, 0.012), P("base"), rot_z=rz)   # junta
                if not fino:
                    t.caja((x, y, 0.33), (1.2, 0.8, 0.58), P("palet"), rot_z=rz)
                    for f in range(3):
                        t.caja((x, y, 0.18 + f * 0.145), (1.21, 0.81, 0.02), P("base"), rot_z=rz)
            elif V == 2 and n % 6 == 1:                            # vacío: un resto que dice qué había
                _suelto(t, x, y, n, fino)
            continue
        if V == 3 and not ocupado:                                 # saqueado: lo que queda por el suelo
            if _h(n, 8) < 0.5:
                t.caja((x, y, 0.02), (1.28, 0.88, 0.04), "base", rot_z=rz + _h(n, 9))
                t.caja((x, y, 0.09), (1.2, 0.8, 0.1), "palet", rot_z=rz + _h(n, 9))
                _suelto(t, x + 0.4, y + 0.3, n, fino)
            continue
        _carga(t, x, y, rz, n, fino, q, P)
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


def _carga(t, x, y, rz, n, fino, q, P):
    """Un palé ocupado, con el mismo dibujo en los dos LOD (en el juego se ve el LOD1): base oscura un poco mayor que el
    palé (lo separa del suelo), núcleo oscuro debajo de la carga (se ve en las juntas) y la carga en bultos separados por
    huecos de 0,1 m, con flejes y tapas claros encima."""
    B = (lambda p: "quemado") if q else (lambda p: p)
    t.caja((x, y, 0.07), (1.28, 0.88, 0.14), "base" if not q else "quemado", rot_z=rz)
    c, s_ = math.cos(rz), math.sin(rz)
    R = lambda dx, dy: (x + dx * c - dy * s_, y + dx * s_ + dy * c)
    z0 = 0.14
    if TIPO == 0:                                                  # munición: 2 columnas de cajas
        capas = 3 if (V in (0, 3) or n % 2) else 2
        if q:
            capas = 1 + (n % 2)
        h = capas * 0.31
        t.caja((x, y, z0 + h / 2 - 0.02), (1.0, 0.6, h - 0.04), "base" if not q else "quemado", rot_z=rz)
        for a in (-1, 1):
            cx, cy = R(a * 0.3, 0)
            if fino:
                for f in range(capas):
                    t.caja((cx, cy, z0 + 0.15 + f * 0.31), (0.5, 0.78, 0.27), P("caja"), rot_z=rz)
            else:
                t.caja((cx, cy, z0 + h / 2), (0.5, 0.78, h), P("caja"), rot_z=rz)
            if not q:
                t.caja((cx, cy, z0 + h + 0.01), (0.5, 0.08, 0.02), "fleje", rot_z=rz)          # fleje claro arriba
                if fino:
                    t.caja((cx, cy, z0 + h / 2), (0.52, 0.06, h), "fleje", rot_z=rz)           # y por los lados
    elif TIPO == 1:
        if n % 3 == 1 and not q:                                   # GRG (IBC): depósito claro en marco gris
            t.caja((x, y, z0 + 0.55), (0.96, 0.74, 1.0), "deposito", rot_z=rz)
            for dx, dy, sx, sy in ((0, 0.39, 1.04, 0.06), (0, -0.39, 1.04, 0.06), (0.5, 0, 0.06, 0.82),
                                   (-0.5, 0, 0.06, 0.82)):
                cx, cy = R(dx, dy)
                t.caja((cx, cy, z0 + 1.08), (sx, sy, 0.06), "jaula", rot_z=rz)
            if fino:
                for z in (0.3, 0.7):
                    t.caja((x, y, z0 + z), (1.0, 0.78, 0.04), "jaula", rot_z=rz)
            cx, cy = R(0, 0)
            t.cilindro((cx, cy, z0 + 1.1), 0.12, 0.06, "Z", 8 if fino else 6, "base")              # boca oscura
        else:                                                      # dos bidones con 0,1 m de hueco y tapa clara
            for a in (-1, 1):
                cx, cy = R(a * 0.32, 0)
                if q and a == 1 and n % 2:
                    t.cilindro((cx, cy + 0.3, 0.3), 0.27, 0.9, "X", 8 if fino else 6, "quemado")
                    continue
                t.cilindro((cx, cy, z0 + 0.45), 0.27, 0.9, "Z", 10 if fino else 6, B("bidon"))
                t.cilindro((cx, cy, z0 + 0.91), 0.22, 0.02, "Z", 10 if fino else 6, B("tapa"))
                if not q:
                    t.cilindro((cx + 0.1 * c, cy + 0.1 * s_, z0 + 0.93), 0.05, 0.02, "Z", 6, "base")  # tapón
                if fino and not q:
                    for z in (0.3, 0.6):
                        t.cilindro((cx, cy, z0 + z), 0.28, 0.03, "Z", 10, "tapa")
    else:
        k = n % 3
        if k == 0:                                                 # fardo de gaviones con dos flejes claros
            h = 0.6 if not q else 0.3
            t.caja((x, y, z0 + h / 2), (1.16, 0.76, h), P("gavion"), rot_z=rz)
            if not q:
                for a in (-1, 1):
                    cx, cy = R(a * 0.3, 0)
                    t.caja((cx, cy, z0 + h + 0.01), (0.08, 0.78, 0.02), "fleje", rot_z=rz)
                if fino:
                    for f in range(4):
                        t.caja((x, y, z0 + 0.1 + f * 0.13), (1.18, 0.78, 0.02), "concertina", rot_z=rz)
        elif k == 1:                                               # sacos y tablones, con hueco oscuro entre medias
            h = 0.5 if V != 1 else 0.3
            t.caja((x, y, z0 + 0.2), (1.0, 0.6, 0.36), "base" if not q else "quemado", rot_z=rz)
            cx, cy = R(-0.3, 0)
            t.caja((cx, cy, z0 + h / 2), (0.55, 0.76, h), P("saco"), rot_z=rz)
            cx, cy = R(0.33, 0)
            if fino:
                for f in range(4):
                    t.caja((cx, cy, z0 + 0.04 + f * 0.09), (0.46, 0.76, 0.08), P("tablon"), rot_z=rz)
            else:
                t.caja((cx, cy, z0 + 0.18), (0.46, 0.76, 0.36), P("tablon"), rot_z=rz)
        else:                                                      # rollos de concertina
            for a in ((-1, 1) if not q else (0,)):
                cx, cy = R(0, a * 0.21)
                t.cilindro((cx, cy, z0 + 0.4), 0.4, 0.34, "Y", 10 if fino else 6, P("concertina"))
