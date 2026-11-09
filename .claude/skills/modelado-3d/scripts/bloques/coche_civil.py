"""Kit de vehículos civiles abandonados para el atasco de la carretera vieja y el paso superior de Sagunto (encargo de
Mak, 8-nov 13:05; revisión de la Noche57 de Álvaro, puntos 6 y 9). Teo (3), 8-nov.

Cinco tipos de vehículo civil típico de España a principios de siglo, sin marcas ni logotipos: utilitario, berlina,
furgoneta, todoterreno y camión pequeño. Cada uno tiene 5 variantes de abandono. Perfiles genéricos de la clase (los
turismos siguen las fotos G2 de Paula de coche_2000). MEDIDAS SIN VERIFICAR, típicas de la clase:
utilitario 3,8 × 1,66 × 1,45 m; berlina 4,5 × 1,75 × 1,44 m; furgoneta 4,8 × 1,9 × 2,0 m; todoterreno 4,4 × 1,84 ×
1,82 m; camión pequeño 6,6 × 2,2 × 2,9 m (cabina y caja de lona).
FUERA DE LA BANDA DE LA HORDA (L* 9–13; Lucía, 8-nov): nada de 283440, 303424 ni 1c200c, y nada de 543820 ni 48403c en
caras que puedan quedar en sombra. Ruedas de goma 605c48 (separadas de la carrocería, norma de Eugenio), lunas 6c7874,
faros 88885c (LOD0), óxido 7c3c18, lona 70784c, interior quemado 605c48.
Pinturas por valor partido (techo, costado; los grises están vetados de costado): gris 94948c/605c48, beige
b4a080/605c48, blanco b8b8b8/88885c (el d4d0cc
en grande es el color de selección), ocre 847c34/605c48 y verde 88885c/70784c; el negro y el azul oscuro se funden con
la horda, y el rojo está vetado.
TBD_VARIANTE = 5 · tipo + variante (0..24). Tipos: 0 Utilitario, 1 Berlina, 2 Furgoneta, 3 Todoterreno, 4 Camion.
Variantes: 0 intacto, abandonado; 1 con la puerta del conductor abierta; 2 con una rueda pinchada, ladeado; 3 accidentado
(morro hundido y luna rota); 4 quemado (sin lunas ni ruedas, sobre las llantas).
Delante = −Y (+Z en Unity). Origen en el suelo, en el centro. Sin colisión en el MVP (sagunto-nivel.md §8).
"""
import math, os
from mathutils import Matrix
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 25
TIPO, V = VAR // 5, VAR % 5
NOMBRES_T = ["Utilitario", "Berlina", "Furgoneta", "Todoterreno", "Camion"]
PINTURAS = [("94948c", "605c48"), ("b4a080", "605c48"), ("b8b8b8", "88885c"), ("847c34", "605c48"),
            ("88885c", "70784c")]
QUEMADO = V == 4
# reparto (Lucía, 8-nov 13:09): verde y ocre juntos ≤ 20 % de los no quemados (4 de 20); el resto gris, beige y blanco
_SEQ = (0, 1, 2, 0, 1, 2, 3, 0, 1, 2, 0, 1, 4, 2, 0, 1, 2, 3, 1, 4)
_PINT = ("94948c", "605c48") if QUEMADO else PINTURAS[_SEQ[TIPO * 4 + V]]
PIEZAS = ["casco", "luna", "rueda", "faro", "puerta", "lona", "oxido", "chasis"]
COLORES = {"casco": _PINT[1], "luna": "605c48" if QUEMADO else "6c7874", "rueda": "605c48", "faro": "88885c",
           "puerta": _PINT[1], "lona": "605c48", "oxido": "7c3c18", "chasis": "6c7874"}
# lona del camión en beige o gris, nunca verde (se leía militar, Lucía 8-nov 13:09)
VALOR_PIEZAS = {"casco": _PINT, "puerta": _PINT, "lona": ("b4a080", "605c48") if V % 2 == 0 else ("94948c", "605c48")}
FACCION = "neutro"
PRESUPUESTO = {0: (60, 1200), 1: (40, 600)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}

# perfil lateral (y, z), delante en −Y; semiancho abajo; ruedas (y, r); alto del cambio de pendiente del costado
TIPOS = {
    0: dict(P=[(-1.9, 0.3), (-1.9, 0.7), (-1.3, 0.92), (-0.6, 1.42), (1.35, 1.45), (1.9, 1.1), (1.9, 0.3)],
            semi=0.83, ejes=(-1.2, 1.25), r=0.29, corte=0.98),
    1: dict(P=[(-2.25, 0.32), (-2.25, 0.72), (-1.35, 0.95), (-0.5, 1.42), (0.95, 1.42), (1.6, 1.04), (2.25, 0.98),
               (2.25, 0.32)], semi=0.875, ejes=(-1.38, 1.4), r=0.31, corte=1.0),
    2: dict(P=[(-2.4, 0.35), (-2.4, 0.85), (-1.95, 1.12), (-1.35, 1.95), (2.4, 1.98), (2.4, 0.35)],
            semi=0.95, ejes=(-1.55, 1.6), r=0.32, corte=1.15),
    3: dict(P=[(-2.2, 0.45), (-2.2, 0.98), (-1.45, 1.08), (-0.95, 1.8), (2.15, 1.82), (2.2, 0.45)],
            semi=0.92, ejes=(-1.35, 1.35), r=0.38, corte=1.1),
    4: dict(P=[(-3.3, 0.55), (-3.3, 1.3), (-3.1, 2.55), (-1.6, 2.6), (-1.6, 0.55)],
            semi=1.05, ejes=(-2.5, 1.7), r=0.45, corte=1.5),
}


def construir(nombre="SM_CocheCivil", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    d = TIPOS[TIPO]
    P = d["P"]
    s0, corte = d["semi"], d["corte"]
    if TIPO == 4:
        semi = lambda z: s0
        t.perfil_extruido(P, semi, pieza="casco")                         # cabina
        t.caja((0, 0.75, 1.55), (2.2, 4.6, 2.0) if V != 4 else (2.2, 4.6, 0.12), "lona" if V != 4 else "chasis")
        t.caja((0, -0.1, 0.45), (1.0, 6.4, 0.3), "chasis")                  # bastidor
        t.caja((0, 0.75, 0.6), (2.24, 4.64, 0.1), "casco")                  # plataforma
        if V == 4:                                                          # arcos de la lona, desnudos
            for k in range(4 if fino else 2):
                y = -1.2 + k * (3.9 / (3 if fino else 1))
                w = 0.08 if fino else 0.34
                for sx in (-1, 1):
                    t.caja((sx * 1.05, y, 1.6), (w, w, 2.0), "oxido")
                t.caja((0, y, 2.6), (2.2, w, w), "oxido")
    else:
        semi = lambda z: s0 if z <= corte else s0 - (z - corte) * 0.45
        t.perfil_extruido(P, semi, cortes_z=(corte,), pieza="casco")
    # lunas: banda por los costados y luna delantera (en el LOD1 también, ≥ 2 px)
    altas = [p for p in P if p[1] > corte + 0.2]
    if altas and not (TIPO == 4 and V == 4):
        y0 = min(p[0] for p in altas) - 0.1
        y1 = max(p[0] for p in altas) - 0.1 if TIPO != 2 else y0 + 0.9
        zl = corte + (max(p[1] for p in altas) - corte) * 0.5
        for s in (-1, 1):
            t.caja((s * (semi(zl) + 0.005), (y0 + y1) / 2, zl), (0.02, y1 - y0, 0.36), "luna")
        if fino:
            t.caja((0, y0 - 0.25, zl), (1.3, 0.02, 0.4), "luna", rot_x=-0.8)
    # ruedas (separadas de la carrocería)
    r = d["r"]
    for yw in d["ejes"]:
        for s in (-1, 1):
            if QUEMADO:
                if fino:
                    t.cilindro((s * (s0 - 0.1), yw, r * 0.65), r * 0.65, 0.12, "X", 8, "oxido")
                continue
            rr = r * 0.65 if (V == 2 and yw == d["ejes"][0] and s < 0) else r
            t.cilindro((s * (s0 - 0.1), yw, rr), rr, 0.24, "X", 10 if fino else 8, "rueda")
    if TIPO == 3 and not QUEMADO:                                         # rueda de repuesto en el portón
        t.cilindro((0, P[-1][0] + 0.12, 1.0), r * 0.9, 0.24, "Y", 10 if fino else 8, "rueda")
    if fino and not QUEMADO:
        for s in (-1, 1):
            t.caja((s * (s0 - 0.25), P[0][0] - 0.01, P[1][1] - 0.05), (0.3, 0.03, 0.14), "faro")
    if V == 1:                                                            # puerta del conductor abierta 60°
        a = math.radians(60)
        yb = (P[2][0] + 0.2) if TIPO != 4 else -3.0
        lp = 1.0
        zc = (P[0][1] + corte) / 2 + 0.25
        t.caja((-s0 - math.sin(a) * lp / 2, yb + math.cos(a) * lp / 2, zc), (0.07, lp, 0.85), "puerta", rot_z=a)
    if QUEMADO:                                                           # óxido en el techo y huecos de lunas
        # manchas irregulares de óxido por el techo y el capó (no un cuadrado: parecía una selección, Lucía 13:09)
        top = max(p[1] for p in P)
        yt0, yt1 = P[3][0] if TIPO != 4 else P[2][0], P[-2][0] if TIPO != 4 else P[3][0]
        mn = 0.12 if fino else 0.36
        for k, (fy, fx, a1, a2, rz) in enumerate(((0.2, -0.3, 0.7, 0.45, 0.4), (0.45, 0.25, 0.5, 0.8, -0.7),
                                                  (0.7, -0.1, 0.9, 0.4, 1.1), (0.85, 0.35, 0.35, 0.5, 0.2))):
            t.caja((fx * s0, yt0 + (yt1 - yt0) * fy, top + 0.012 + 0.004 * k), (max(a1, mn), max(a2, mn), 0.03), "oxido",
                   rot_z=rz)
        if TIPO != 4:                                    # capó
            yc, zc = (P[1][0] + P[2][0]) / 2, (P[1][1] + P[2][1]) / 2
            ang = math.atan2(P[2][1] - P[1][1], P[2][0] - P[1][0])
            for k, (fx, a1, a2, rz) in enumerate(((-0.35, 0.5, 0.35, 0.5), (0.3, 0.4, 0.55, -0.4))):
                t.caja((fx * s0, yc + 0.1 * k, zc + 0.03), (max(a1, mn), max(a2, mn), 0.03), "oxido",
                       rot_x=ang, rot_z=rz)
    o = t.objeto(nombre, bisel=0)
    me = o.data
    if V == 3:                                                            # morro hundido
        yf = P[0][0]
        for vtx in me.vertices:
            if vtx.co.y < yf + 0.7:
                k = (yf + 0.7 - vtx.co.y) / 0.7
                vtx.co.y += 0.35 * k
                vtx.co.z -= 0.12 * k * max(0.0, vtx.co.z - 0.4)
    if V == 2:
        me.transform(Matrix.Rotation(math.radians(3), 4, "Y"))
    if QUEMADO:
        me.transform(Matrix.Translation((0, 0, -r * 0.35)))
    return o
