"""Torreta de vigilancia del recinto de una estación (coordinador, 7-nov 09:13: piezas que faltan para montar Castellón;
lista con Álvaro). Teo, 7-nov.

SIN FOTO: en referencias-reales no hay ninguna torre de vigilancia. Es el tipo de torre de guardia de una base avanzada:
plataforma elevada con parapeto de sacos, techo de chapa, escalera y foco hacia fuera. Los sacos y el HESCO son los del
puesto de frente (K3_control_1, mismas medidas y colores) para que case con el recinto. Medidas SIN VERIFICAR: planta
de 2,8 x 2,8 m, plataforma a 4,2 m (3,0 m sobre el HESCO de la v1), parapeto de 1,0 m y techo a 2,1 m de la plataforma.
Ejes: el frente (hacia fuera del recinto, donde mira el foco) es -Y en Blender, +Z en Unity. Origen en el suelo, en el
centro de la torre.
Jerarquía: SM_…_LODn con un hijo «Luces» (el cristal del foco, transformación identidad) para la emisión nocturna,
como el puesto de frente y las balizas.
Colores: sacos 88885c, HESCO 70784c con relleno b4a080, estructura 605c48, techo 70784c con costados 50541c (valor
partido), madera a86c50, palés b4a080, red 847c34, cabeza del foco 48403c (605c48 en el LOD1) y luz f0dc9c.
Variantes (TBD_VARIANTE % 5):
  0 torre de andamio de acero (la del ejército);  1 sobre dos alturas de HESCO (la más sólida, para las puertas);
  2 improvisada por los supervivientes: rollizos, palés y chapas desparejadas (la heredada al reclamar la estación);
  3 en reparación (estación Liberada): andamio en un costado y sacos nuevos al pie;  4 Caída: vacía (sin foco),
  inclinada, techo medio arrancado y sacos caídos al pie.
Colocación en Castellón (Álvaro, 09:14; yaw 0 = frente a +Z de Unity): esquinas NO (67; 87) yaw 315, NE (219; 87)
yaw 45, SE en el chaflán (215; 10) yaw 135, SO (67; 7) yaw 225; puertas: ramal (219; 49) yaw 90, vehículos (67; 36)
yaw 270. Se heredan de los supervivientes: al empezar, la 2 o la 4.
"""
import math
import os
from mathutils import Matrix, Vector
from montaje import Montaje, separar as _separar
from puesto_frente import _sacos, _hesco

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["sacos", "hesco", "relleno", "metal", "casco", "madera", "pale", "red", "cabeza", "luz", "chapa_b"]
COLORES = {"sacos": "88885c", "hesco": "70784c", "relleno": "b4a080", "metal": "605c48", "casco": "70784c",
           "madera": "a86c50", "pale": "b4a080", "red": "847c34", "cabeza": "48403c", "luz": "f0dc9c",
           "chapa_b": "88885c"}
COLORES_LOD1 = {"cabeza": "605c48"}
VALOR_PIEZAS = {"casco": ("70784c", "50541c", "50541c")}
FACCION = "propio"
PRESUPUESTO = {0: (150, 4000), 1: (80, 1600)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.12}
NORMALES_ARRIBA = ["sacos", "hesco", "relleno", "pale"]
NORMALES_INCLINACION = 1.0
PARTES = ["torre", "Luces"]
PIV = {"Luces": (0, 0, 0)}
R = math.radians
A = 2.8                      # planta


def _parapeto(m, z, fino, lados=("f", "i", "d", "a"), hueco_atras=True):
    """Parapeto de sacos de 1,0 m en el borde de la plataforma (a la cota z); detrás deja el hueco de la escalera."""
    s = A / 2 - 0.3
    tramos = {"f": ((-s, -s), (s, -s)), "i": ((-s, -s), (-s, s)), "d": ((s, -s), (s, s)),
              "a": ((-s, s), (0.2 if hueco_atras else s, s))}
    antes = set(m.bm.verts)
    for k in lados:
        _sacos(m, *tramos[k], fino, alto=0.9, grueso=0.5)
    for v in set(m.bm.verts) - antes:
        v.co.z += z


def _techo(m, z, fino, pieza="casco", medio=False):
    for sx in (-1, 1):
        for sy in (-1, 1):
            if medio and sx > 0 and sy < 0:
                continue
            m.caja((sx * (A / 2 - 0.12), sy * (A / 2 - 0.12), z + 1.05), (0.1 if fino else 0.2,) * 2 + (2.1,), "metal")
    if medio:   # media chapa arrancada, colgando hacia +X
        m.caja((-A / 4, 0, z + 2.18), (A / 2 + 0.3, A + 0.4, 0.08), pieza, rot_x=R(-6))
        m.caja((A / 2 + 0.25, 0.2, z + 1.5), (0.08, A, 1.4), pieza, rot_y=R(-15))
    else:
        m.caja((0, 0, z + 2.18), (A + 0.5, A + 0.5, 0.08), pieza, rot_x=R(-6))   # cae hacia atrás (+Y)


def _foco(m, x, y, z, luces, grande=False):
    s = 1.6 if grande else 1.0
    m.caja((x, y, z), (0.5 * s, 0.3 * s, 0.4 * s), "cabeza")
    luces.append(((x, y - 0.155 * s - 0.005, z), (0.42 * s, 0.02, 0.32 * s)))


def _escalera(m, z0, z1, y, fino, pieza="metal"):
    g = 0.06 if fino else 0.2
    for x in (-0.25, 0.25):
        m.caja((x, y, (z0 + z1) / 2), (g, g, z1 - z0), pieza, rot_x=R(-8))
    if fino:
        n = int((z1 - z0) / 0.35)
        for k in range(n):
            z = z0 + (k + 0.5) * (z1 - z0) / n
            dy = -(z - (z0 + z1) / 2) * math.tan(R(8))
            m.caja((0, y + dy, z), (0.5, 0.04, 0.04), pieza)
    else:
        m.caja((0, y + 0.02, (z0 + z1) / 2), (0.5, 0.04, (z1 - z0) - 0.3), pieza, rot_x=R(-8))


def construir(nombre="SM_Torreta", lod=0):
    v = VAR
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    g = 0.1 if fino else 0.24
    luces = []
    antes = set(m.bm.verts)
    if v == 1:   # dos alturas de HESCO en anillo de 3,3 m, plataforma encima
        zp = 2.85
        for k, z in enumerate((0.0, 1.42)):
            h0 = set(m.bm.verts)
            s = 1.65 - 0.05 * k
            for p0, p1 in (((-s, -s), (s, -s)), ((s, -s), (s, s)), ((s, s), (-s, s)), ((-s, s), (-s, -s))):
                _hesco(m, p0, p1, fino)
            for w in set(m.bm.verts) - h0:
                w.co.z += z
        m.caja((0, 0, zp), (A + 0.4, A + 0.4, 0.12), "metal")
        _escalera(m, 0.0, zp, A / 2 + 0.75, fino)
    elif v == 2:   # rollizos, palés y chapas desparejadas
        zp = 3.6
        for sx in (-1, 1):
            for sy in (-1, 1):
                m.cilindro((sx * (A / 2 - 0.15), sy * (A / 2 - 0.15), zp / 2), 0.12 if fino else 0.17, zp, "Z", 6,
                           "madera")
        for z in (1.2, 2.4):   # travesaños
            m.caja((0, -(A / 2 - 0.15), z), (A, g, g), "madera", rot_y=R(8))
            m.caja((-(A / 2 - 0.15), 0, z), (g, A, g), "madera", rot_x=R(-6))
            m.caja((A / 2 - 0.15, 0, z + 0.1), (g, A, g), "madera")
        for dx in (-0.65, 0.65):   # suelo de palés
            m.caja((dx, 0, zp), (1.25, A, 0.14), "pale")
        _escalera(m, 0.0, zp, A / 2 + 0.75, fino, pieza="madera")
    else:          # andamio de acero (0, 3, 4)
        zp = 4.2
        for sx in (-1, 1):
            for sy in (-1, 1):
                m.caja((sx * (A / 2 - 0.1), sy * (A / 2 - 0.1), zp / 2), (g, g, zp), "metal")
        niveles = (1.4, 2.8) if fino else (2.1,)
        for z in niveles:
            for sy in (-1, 1):
                m.caja((0, sy * (A / 2 - 0.1), z), (A, g * 0.8, g * 0.8), "metal")
            for sx in (-1, 1):
                m.caja((sx * (A / 2 - 0.1), 0, z), (g * 0.8, A, g * 0.8), "metal")
        if fino:   # cruces de San Andrés en el frente y los costados
            for (x0, y0, x1, y1) in ((-1, -1, 1, -1), (-1, -1, -1, 1), (1, -1, 1, 1)):
                m.barra((x0 * 1.3, y0 * 1.3, 0.1), (x1 * 1.3, y1 * 1.3, zp - 0.1), 0.05, 0.05, "metal")
        m.caja((0, 0, zp), (A + 0.3, A + 0.3, 0.12), "metal")
        _escalera(m, 0.0, zp, A / 2 + 0.75, fino)
    # ---- parapeto, techo y foco
    zs = zp + 0.06
    if v == 2:   # chapas desparejadas en el frente y un lado; sacos en el otro
        m.caja((0, -(A / 2 - 0.05), zs + 0.5), (A, 0.06, 1.0), "chapa_b", rot_x=R(4))
        m.caja((-(A / 2 - 0.05), 0, zs + 0.5), (0.06, A, 1.0), "casco", rot_y=R(-3))
        _parapeto(m, zs, fino, lados=("d",))
        m.caja((0.2, 0.1, zs + 2.0), (A + 0.4, A + 0.6, 0.06), "chapa_b", rot_x=R(-9), rot_y=R(4))
        m.caja((-0.7, -0.4, zs + 2.06), (1.3, 1.6, 0.06), "casco", rot_x=R(-9), rot_y=R(4))
        for sx in (-1, 1):
            for sy in (-1, 1):
                m.cilindro((sx * (A / 2 - 0.15), sy * (A / 2 - 0.15), zs + 1.0), 0.1 if fino else 0.17, 2.0, "Z", 6,
                           "madera")
        _foco(m, 0.9, -(A / 2 + 0.1), zs + 1.3, luces)
    else:
        _parapeto(m, zs, fino)
        _techo(m, zs, fino, medio=(v == 4))
        if v != 4:   # la caída está vacía: sin foco
            _foco(m, 0.8, -(A / 2 - 0.05), zs + 1.85, luces)
        if v == 3:   # en reparación (Liberada): andamio de dos niveles en el costado +X, con tablones y palés
            xa = A / 2 + 0.75
            for dx in (-0.55, 0.55):
                for y in (-1.2, 1.2):
                    m.caja((xa + dx, y, 2.6), (g * 0.7, g * 0.7, 5.2), "metal")
            for z in (2.0, 4.0):
                m.caja((xa, 0, z), (1.25, 2.7, 0.08), "pale")
                if fino:
                    m.caja((xa + 0.55, 0, z + 0.5), (0.05, 2.5, 0.05), "metal")      # barandilla
            if fino:
                m.barra((xa + 0.55, -1.2, 0.1), (xa + 0.55, 1.2, 3.9), 0.05, 0.05, "metal")
            m.caja((-0.9, -2.4, 0.07), (1.2, 1.0, 0.12), "pale")                       # sacos nuevos esperando
            m.caja((-0.9, -2.4, 0.35), (1.0, 0.8, 0.45), "sacos")
    if v == 4:   # inclinada 7° hacia el frente; sacos caídos al pie
        piv = Vector((0, -A / 2, 0))
        M = Matrix.Translation(piv) @ Matrix.Rotation(R(-7), 4, "X") @ Matrix.Translation(-piv)
        for w in set(m.bm.verts) - antes:
            w.co = M @ w.co
        luces = [((M @ Vector(c)).to_tuple(), s) for c, s in luces]
        for (x, y, rz) in ((-1.4, -2.3, 20), (0.3, -2.7, -10), (1.6, -2.0, 40), (-0.6, -3.2, 70)):
            m.caja((x, y, 0.15), (0.85, 0.5, 0.28), "sacos", rot_z=R(rz))
    m.parte("Luces")
    for c, s in luces:
        m.caja(c, s, "luz", rot_x=R(-7) if v == 4 else 0.0)
    return m.objeto(nombre)


def separar(o, lod):
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    o.data.transform(Matrix.Translation(base))      # origen en el centro de la torre, en el suelo
    for n in objs[1:]:
        n.location = n.location + base
    return objs
