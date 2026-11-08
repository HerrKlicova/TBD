"""Puerta de vía del muro oeste de Sagunto: 5 variantes × 4 estados (Álvaro, 8-nov 10:18, sagunto-nivel.md §6.3 y §7 C1).
Teo, 8-nov.

Hueco de 5 m (Z 62–67 del nivel) por donde entra solo la vía 1, en el muro de HESCO (2,1 m, D-N59; la altura del sólido
está SIN VERIFICAR, Álvaro: que la confirme Marcos). Dos hojas batientes de 2,5 m que abren HACIA DENTRO (+Y de
Blender; el tren no las pisa). Jambas de HESCO de 0,9 m por fuera de los 5 m (en la v4, pilares de hormigón con dintel a
6 m: abajo a 6,0 m, por encima del tren y de la catenaria de ~5,3 m; Lucía, 8-nov 10:22; SIN VERIFICAR hasta la foto de Paula). Las hojas, los estados y la paleta son los de la puerta de vehículos de la D-N163 (puerta_estados.py): chapa
94948c, malla b8b8b8, tablones a86c50, boquete y escombro 48403c; nada rojo ni ámbar.
Disposición: origen en el suelo, en el centro del hueco (sobre el eje de la vía 1); el muro va a lo largo de X; −Y de
Blender es FUERA (= +Z de Unity con yaw 0). Jerarquía: SM_…_LODn (jambas y dintel) con Hoja_O (bisagra en x = −2,49) y
Hoja_E (bisagra en x = +2,49); abren hacia dentro girando +90° (O) y −90° (E) sobre la vertical.
TBD_VARIANTE = 4 · variante + estado (0..19).
"""
import os
import puerta_vehiculos as PV
import puerta_estados as PE
from montaje import Montaje

BISAGRA = 2.49
PV.BISAGRA = BISAGRA                       # _hoja y _hoja_danada leen la bisagra de sus módulos
PE.BISAGRA = BISAGRA
VAR = PE.VAR
VARIANTE, ESTADO = PE.VARIANTE, PE.ESTADO
NOMBRES_V, NOMBRES_E = PE.NOMBRES_V, PE.NOMBRES_E
PIEZAS = PE.PIEZAS
COLORES = PE.COLORES
FACCION = "propio"
PRESUPUESTO = {0: (40, 2500), 1: (20, 1200)}
UMBRAL_CONTORNO = PE.UMBRAL_CONTORNO
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.5
ALTO = PV.ALTO
PARTES = ["puerta", "Hoja_O", "Hoja_E"]
PIV = {"Hoja_O": (-BISAGRA, 0.0, 0.0), "Hoja_E": (BISAGRA, 0.0, 0.0)}
GIRO_PRUEBA = {"Hoja_O": ("Z", 60), "Hoja_E": ("Z", -60)}      # entreabiertas hacia dentro
JAMBA = BISAGRA + 0.46


def construir(nombre="SM_PuertaVia", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    a = VARIANTE
    for s in (-1, 1):
        if a == 4:
            m.caja((s * JAMBA, 0, 3.25), (0.9, 1.2, 6.5), "hormigon")
        else:
            PV._jamba_hesco(m, s * JAMBA, fino)
    if a == 4:                              # dintel con la cara de abajo a 6,0 m: deja pasar el tren y la catenaria
        m.caja((0, 0, 6.25), (2 * JAMBA + 0.9, 0.6, 0.5), "hormigon")    # (Lucía, 8-nov 10:22; gálibo SIN VERIFICAR)
    if a == 3 and fino:
        for s in (-1, 1):
            m.cilindro((s * JAMBA, 0, 2.3), 0.25, 0.9, "X", 8, "alambre")
    mat = PE.MATERIAL[a]
    if ESTADO == 3:                         # trozos de las hojas, hacia dentro (+Y) y fuera de la vía (|x| > 0,9)
        for k in range(5 if fino else 3):
            s = -1 if k % 2 else 1
            m.caja((s * (1.2 + 0.4 * k / 2), 0.6 + 0.3 * (k % 3), 0.04 + 0.03 * (k % 2)), (0.9, 0.45, 0.06),
                   mat if k % 3 else "marco", rot_z=k * 0.8)
        m.caja((-1.6, 1.1, 0.03), (0.9, 0.7, 0.05), "boquete", rot_z=0.3)
    elif ESTADO == 2:
        for k in range(2):
            m.caja((-1.8 + 0.5 * k, 0.7, 0.04), (0.7, 0.4, 0.06), mat, rot_z=k * 1.1)
    for nombre_p, s in (("Hoja_O", -1), ("Hoja_E", 1)):
        m.parte(nombre_p)
        if ESTADO == 3:
            for z in (0.4, 1.7):
                m.caja((s * (BISAGRA - 0.08), 0, z), (0.18, 0.16, 0.3), "marco")
        else:
            antes = set(m.bm.verts)
            PE._hoja_danada(m, s, a, fino)
            if ESTADO == 2 and s > 0:       # la hoja que cuelga cae hacia dentro, no hacia la horda
                import bmesh
                nuevos = set(m.bm.verts) - antes
                for w in nuevos:
                    w.co.y = -w.co.y
                caras = [f for f in m.bm.faces if all(v in nuevos for v in f.verts)]
                bmesh.ops.reverse_faces(m.bm, faces=caras)         # el espejo invierte las caras
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector, Matrix
    from montaje import separar as _separar
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)                 # pivotes en las bisagras de 2,49 (no las de 3,99 de la D-N111)
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
