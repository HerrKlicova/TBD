"""Garita del control de acceso de Bétera (D-N173; Álvaro, betera-nivel.md §2: garita en (252; 66), yaw 270, junto a la
puerta principal de (250; 59)). Teo (3), 8-nov.

Sin foto de la garita de Bétera (la AE1 muestra la puerta, no la garita). Tipo genérico de garita de control militar:
caseta de obra de 2,4 × 2,4 m y 2,6 m, cubierta plana con vuelo, ventanas corridas, puerta, barrera de brazo y una
pequeña marquesina. Medidas SIN VERIFICAR.
Colores (Lucía, 8-nov 13:03): fachada b8b8b8 con zócalo 94948c, cubierta b4a080 (arriba claro), carpintería 6c7874 y
cristal 605c48 (con 543820 y 48403c la sombra caía en la banda de la horda), barrera
f0dc9c con franjas 605c48 (el rojo está vetado, como en el paso a nivel), sacos 605c48, chapa 94948c, focos f0dc9c.
Disposición: origen en el suelo, en el centro de la caseta; la ventana principal y la barrera hacia −Y (= +Z de Unity
con yaw 0). Jerarquía: SM_…_LODn con el hijo Barrera (pivote en el poste, x = +1,6). ABRIR = girar −80° sobre Y local.
Variantes (TBD_VARIANTE % 5): 0 de obra, encalada; 1 prefabricada de chapa; 2 reforzada con sacos hasta la ventana;
3 con marquesina y foco; 4 abandonada: cristales rotos y barrera caída.
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["fachada", "zocalo", "cubierta", "carpinteria", "cristal", "barrera", "franja", "sacos", "foco", "poste"]
COLORES = {"fachada": "94948c" if VAR == 1 else "b8b8b8", "zocalo": "94948c" if VAR != 1 else "6c7874",
           "cubierta": "b4a080", "carpinteria": "6c7874", "cristal": "605c48", "barrera": "f0dc9c",
           "franja": "605c48", "sacos": "605c48", "foco": "f0dc9c", "poste": "6c7874"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.5
PRESUPUESTO = {0: (40, 1500), 1: (20, 700)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
A, HG = 2.4, 2.6
PARTES = ["garita", "Barrera"]
PIV = {"Barrera": (A / 2 + 0.4, -A / 2 - 1.0, 1.0)}
GIRO_PRUEBA = {"Barrera": ("Y", -80)}


def construir(nombre="SM_Garita", lod=0):
    v = VAR
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    m.caja((0, 0, HG / 2), (A, A, HG), "fachada")
    m.caja((0, 0, 0.3), (A + 0.04, A + 0.04, 0.6), "zocalo")
    m.caja((0, 0, HG + 0.1), (A + 0.6, A + 0.6, 0.2), "cubierta")
    if v == 1 and fino:                              # chapa: juntas verticales
        for k in range(5):
            m.caja((-A / 2 + 0.4 * (k + 1), -A / 2 - 0.02, HG / 2), (0.04, 0.03, HG - 0.2), "zocalo")
    # ventanas corridas en −Y y −X, puerta en +X
    rotas = v == 4
    for (cx, cy, sx, sy) in ((0, -A / 2 - 0.02, A - 0.4, 0.04), (-A / 2 - 0.02, 0, 0.04, A - 0.4)):
        m.caja((cx, cy, 1.6), (sx, sy, 0.9), "cristal" if not (rotas and cx == 0) else "carpinteria")
        if fino:
            m.caja((cx, cy * 1.01, 1.6), (sx * 0.05 if sx > 0.1 else sx, sy * 0.05 if sy > 0.1 else sy, 0.92),
                   "carpinteria")
    m.caja((A / 2 + 0.02, 0.4, 1.05), (0.04, 0.9, 2.1), "carpinteria")
    if v == 2:                                       # sacos hasta la ventana
        m.caja((0, -A / 2 - 0.45, 0.6), (A + 0.9, 0.8, 1.2), "sacos", rot_z=0.03)
        m.caja((-A / 2 - 0.45, 0, 0.6), (0.8, A, 1.2), "sacos", rot_z=-0.03)
    if v == 3:                                       # marquesina hacia −Y y foco
        m.caja((0, -A / 2 - 1.2, HG - 0.05), (A + 0.6, 2.2, 0.12), "cubierta")
        for s in (-1, 1):
            m.caja((s * (A / 2 + 0.2), -A / 2 - 2.2, (HG - 0.1) / 2), (0.12 if fino else 0.34, 0.12 if fino else 0.34, HG - 0.1), "poste")
        m.caja((0, -A / 2 - 0.1, HG + 0.4), (0.4, 0.4, 0.3), "foco")
    # poste de la barrera
    m.caja((A / 2 + 0.4, -A / 2 - 1.0, 0.5), (0.3, 0.3, 1.0), "poste")
    m.parte("Barrera")
    L = 6.0
    if rotas:                                        # caída al suelo
        m.caja((A / 2 + 0.4 - L / 2, -A / 2 - 1.3, 0.08), (L, 0.14 if fino else 0.34, 0.14 if fino else 0.34), "barrera", rot_z=0.15)
    else:
        m.caja((A / 2 + 0.4 - L / 2, -A / 2 - 1.0, 1.0), (L, 0.14 if fino else 0.34, 0.14 if fino else 0.34), "barrera")
        for k in range(3):
            m.caja((A / 2 + 0.4 - 1.0 - 2.0 * k, -A / 2 - 1.0, 1.0), (0.5, 0.16 if fino else 0.36, 0.16 if fino else 0.36), "franja")
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
