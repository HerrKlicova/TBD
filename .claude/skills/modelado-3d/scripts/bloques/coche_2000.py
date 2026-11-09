"""Turismos españoles de los años 2000 para el atasco (Álvaro, sagunto-nivel.md §2 y C5). Teo, 6-nov-2026.

Fotos G2 de Paula (Commons): SEAT León II (compacto de 5 puertas), Toledo II y Córdoba II (berlinas). No se copia ninguna
marca: son perfiles genéricos de compacto y de berlina de esa época, sin logotipos.
MEDIDAS SIN VERIFICAR (típicas de la clase, sin ficha consultada): compacto de 4,3 × 1,77 × 1,46 m; berlina de
4,4 × 1,75 × 1,44 m y berlina corta de 4,3 m; ruedas de 0,62 m de diámetro.
Colores (el rojo de la foto del León está vetado): carrocería por valor partido (techo, costado): gris azulado
azul 94948c/283440 (los grises y 60546c están vetados en costados), beige b4a080/605c48 y blanco b8b8b8 (9-nov; antes d4d0cc, reservado)/b8b8b8 (Lucía: el negro se fundía con la horda). Lunas 48403c, ruedas de goma 283440 (separadas de la
carrocería, norma de Eugenio), faros 88885c en el LOD0. Delante = −Y (+Z en Unity).
Variantes: 0 compacto; 1 berlina; 2 berlina corta; 3 compacto con la puerta del conductor abierta y una rueda pinchada;
4 berlina quemada (sin lunas ni ruedas, caída sobre las llantas); 5 berlina corta con las puertas y el maletero abiertos.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
_V = VAR % 6
_TIPO = (0, 1, 2, 0, 1, 2)[_V]            # 0 compacto, 1 berlina, 2 berlina corta
_PINT = (("94948c", "283440"), ("b4a080", "605c48"), ("b8b8b8", "88885c"))[_TIPO]   # blanco en b8b8b8/88885c (9-nov, Lucía: d4d0cc es la selección), antes (Lucía, 18:14: 181c30 se funde con la horda)
QUEMADO = _V == 4
PIEZAS = ["casco", "luna", "rueda", "faro", "puerta"]
COLORES = {"casco": "48403c" if QUEMADO else _PINT[1], "luna": "283440" if QUEMADO else "48403c", "rueda": "283440",
           "faro": "88885c", "puerta": "48403c" if QUEMADO else _PINT[1]}
VALOR_PIEZAS = {"casco": ("605c48", "543820"), "puerta": ("605c48", "543820")} if QUEMADO else {"casco": _PINT, "puerta": _PINT}   # quemado: ceniza y óxido
FACCION = "neutro"
PRESUPUESTO = {0: (60, 900), 1: (40, 500)}

_PERFIL = {
    0: [(-2.15, 0.32), (-2.15, 0.72), (-1.25, 0.95), (-0.45, 1.44), (1.35, 1.42), (2.1, 1.0), (2.15, 0.32)],
    1: [(-2.2, 0.32), (-2.2, 0.72), (-1.3, 0.95), (-0.5, 1.42), (0.9, 1.42), (1.55, 1.04), (2.2, 0.98), (2.2, 0.32)],
    2: [(-2.15, 0.32), (-2.15, 0.72), (-1.25, 0.95), (-0.45, 1.42), (0.85, 1.42), (1.5, 1.04), (2.15, 0.98),
        (2.15, 0.32)],
}


def construir(nombre="SM_Coche2000", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    P = _PERFIL[_TIPO]
    ancho = 0.885 if _TIPO == 0 else 0.875
    semi = lambda z: ancho if z <= 1.0 else ancho - (z - 1.0) * 0.5
    t.perfil_extruido(P, semi, cortes_z=(1.0,), pieza="casco")
    if True:   # lunas (en la quemada quedan como huecos oscuros 283440)
        # lunas: banda oscura por los costados y lunas delantera y trasera (en el LOD1 también: 0,35 m = 2 px)
        ys = [p[0] for p in P if p[1] > 1.3]
        y0, y1 = min(ys) - 0.15, max(ys) + 0.1
        for s in (-1, 1):
            t.caja((s * (semi(1.2) + 0.005), (y0 + y1) / 2, 1.2), (0.02, y1 - y0, 0.3), "luna")
        fino.caja((0, y0 - 0.3, 1.2), (1.4, 0.02, 0.36), "luna", rot_x=-0.9)
    # ruedas de goma separadas de la carrocería (C5: una pinchada en la v3; la v4 sobre las llantas)
    for yw in (-1.32, 1.35):
        for s in (-1, 1):
            if QUEMADO:
                fino.cilindro((s * 0.78, yw, 0.2), 0.2, 0.12, "X", 8, "rueda")
                continue
            r = 0.2 if (_V == 3 and yw < 0 and s < 0) else 0.31
            t.cilindro((s * 0.78, yw, r), r, 0.21, "X", 10 if lod == 0 else 8, "rueda")
    for s in (-1, 1):                                                                      # faros (LOD0)
        fino.caja((s * 0.6, P[0][0] - 0.01, 0.75), (0.35, 0.03, 0.15), "faro")
    # puertas abiertas (C5: «una caja fina a 60°»)
    if _V in (3, 5):
        _puerta(t, -1, -0.55)
    if _V == 5:
        _puerta(t, 1, -0.55)
        t.caja((0, P[-1][0] - 0.25, 1.35), (1.4, 0.06, 0.9), "puerta", rot_x=-1.1)          # maletero abierto
    o = t.objeto(nombre, bisel=0)
    from mathutils import Matrix
    if _V == 3:
        o.data.transform(Matrix.Rotation(math.radians(2.5), 4, "Y"))                         # cae hacia la pinchada
    if QUEMADO:
        o.data.transform(Matrix.Translation((0, 0, -0.11)))
    return o


def _puerta(t, s, yb):
    """Puerta abierta 60° con la bisagra delante, en el lado s (−1 izquierda)."""
    a = math.radians(60)
    lp = 1.05
    x0 = s * 0.89
    t.caja((x0 + s * math.sin(a) * lp / 2, yb + math.cos(a) * lp / 2, 0.75), (0.06, lp, 0.8), "puerta",
           rot_z=-s * a)
