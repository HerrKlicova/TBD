"""VAMTAC ST5 artillado con piezas animables (coordinador, 6-nov 23:39: vehículos con física, PR #24). Teo, 6-nov-2026.

Es la V2 aprobada (vamtac_v2.py; medidas de la ficha de URO y fotos V2_vamtac_1/_2 de Paula), sin cambiar la geometría,
solo partida en objetos como el Dragón y con los colores de D-N80 (Bruno: nada propio por debajo de #48403c; neumáticos,
chasis y metal de 303424/1c200c a 48403c, cristales de 181c30 a 60546c, bajos del casco en 50541c).
Jerarquía del FBX (mira a +Z en Unity):
  SM_…_LODn                 carrocería (origen en el suelo, centro del vehículo)
  ├─ Rueda_1L … Rueda_2R    4 ruedas (1 = delante; L = izquierda del vehículo = +X en Blender); giran sobre su X local.
  │                         Las delanteras (Rueda_1*) además giran sobre la vertical para la dirección
  └─ Aro                    aro del techo con el afuste y el escudo; gira sobre la vertical
     └─ Ametralladora       cajón y cañón; se eleva sobre su X local (origen en el muñón del afuste)
SIN VERIFICAR: los ángulos (propuesta: dirección ±35°, ametralladora de −10° a +60°).
"""
import bmesh
import bpy
from mathutils import Vector
import os
import vamtac_v2 as V
import acabado as A
from dragon_animable import _islas

PIEZAS = V.PIEZAS
VAR = 1   # pintura lisa, como el real (Eugenio 7-nov 09:20 rechazó el camuflaje por caras); TBD_VARIANTE es ahora el
          # grado de uso de la textura horneada (textura_real.py): 0 parque, 1 campaña, 2 árido, 3 embarrado, 4 castigado
TEXTURA_REAL = True
COLORES = A.colores(VAR, dict(V.COLORES, goma="48403c", optica="48403c", metal="48403c"))  # cristal 48403c, oscuro de la familia 303424: el 60546c violeta salía de noche como una raya azul (Lucía 09:40)
VALOR_PIEZAS = A.valores(VAR, {"casco": ("70784c", "50541c", "50541c")})
ABAJO_CASCO = "50541c"
L_MIN = 28.0
EJES_Y = (-2.2, 1.7)
DIRECCION = ("Rueda_1L", "Rueda_1R")
ZT = V.H
PIVOTES = {"Aro": (0.0, -0.1, ZT), "Ametralladora": (0.0, -0.1, ZT + 0.5)}
for i, y in enumerate(EJES_Y):
    for s, x in (("L", 0.98), ("R", -0.98)):
        PIVOTES["Rueda_%d%s" % (i + 1, s)] = (x, y, 0.52)


def construir(nombre="SM_Vamtac", lod=0):
    o = V.construir(nombre, lod=lod)
    A.aplicar(o, lod, VAR, base=("casco",))
    return o


def _grupo(c):
    x, y, z = c
    for i, ye in enumerate(EJES_Y):
        if abs(y - ye) < 0.6 and 0.7 < abs(x) < 1.3 and z < 1.1:
            return "Rueda_%d%s" % (i + 1, "L" if x > 0 else "R")
    if abs(x) < 0.8 and -1.9 < y < 0.9 and z > ZT + 0.02:
        if abs(x) < 0.2 and y < -0.2 and ZT + 0.45 < z < ZT + 0.7:   # la chapa alta del escudo (ZT+0,84) va al aro
            return "Ametralladora"
        return "Aro"
    return None


def separar(o, lod):
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))     # desplazamiento de limpiar() al llevar el origen a la base
    bm = bmesh.new()
    bm.from_mesh(o.data)
    grupos = {}
    for isla in _islas(bm):
        bm.faces.ensure_lookup_table()
        vs = {v for i in isla for v in bm.faces[i].verts}
        c = sum((v.co for v in vs), Vector()) / len(vs) + base
        g = _grupo(tuple(c))
        if g:
            grupos.setdefault(g, []).extend(isla)
    bm.free()
    hijos, quitar = {}, set()
    for k, caras in grupos.items():
        n = o.copy()
        n.data = o.data.copy()
        n.name = n.data.name = k
        bpy.context.scene.collection.objects.link(n)
        b = bmesh.new()
        b.from_mesh(n.data)
        b.faces.ensure_lookup_table()
        sel = set(caras)
        bmesh.ops.delete(b, geom=[f for f in b.faces if f.index not in sel], context="FACES")
        p = Vector(PIVOTES[k]) - base
        bmesh.ops.translate(b, verts=b.verts, vec=-p)
        b.to_mesh(n.data)
        b.free()
        n.location = p
        hijos[k] = n
        quitar |= sel
    b = bmesh.new()
    b.from_mesh(o.data)
    b.faces.ensure_lookup_table()
    bmesh.ops.delete(b, geom=[f for f in b.faces if f.index in quitar], context="FACES")
    b.to_mesh(o.data)
    b.free()
    for k, n in hijos.items():
        if k == "Ametralladora" and "Aro" in hijos:
            n.parent = hijos["Aro"]
            n.location = Vector(PIVOTES[k]) - Vector(PIVOTES["Aro"])
        else:
            n.parent = o
    if "tbd_base" in o:
        del o["tbd_base"]
    return [o] + list(hijos.values())


def BOCAS(lod, raiz, objs):
    """Boca del arma (Lucía, 8-nov 24:34): centro de la cara más adelantada (−Y) del hijo «Ametralladora», disparo hacia −Y
    (delante, +Z en Unity), hijo de esa pieza para que siga a la torre y a la elevación. Ametralladora del aro."""
    from mathutils import Vector
    h = next((x for x in objs if x.name.split(".")[0] == "Ametralladora"), None)
    if h is None:
        return []
    vs = [h.matrix_world @ v.co for v in h.data.vertices]
    ymin = min(v.y for v in vs)
    cara = [v for v in vs if v.y < ymin + 0.02]
    c = sum(cara, Vector()) / len(cara)
    return [("Muzzle_0", c, (0.0, -1.0, 0.0), "Ametralladora")]
