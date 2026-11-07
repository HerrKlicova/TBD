"""Locomotora diésel para la VISTA CERCANA del tren (coordinador, 6-nov 23:38). Teo, 6-nov-2026.

Es la R1 aprobada (locomotora_r1.py: Renfe 333.3 blindada por el taller de Sagunto, fotos R1_renfe333_1/_2 de Paula),
con dos cambios:
  1. D-N80 (Bruno; Lucía: también en vehículos): ningún albedo propio bajo #48403c. El bastidor, las ruedas, el metal y
     las rejillas pasan de 303424/1c200c a 48403c, los cristales de 181c30 a 60546c y las caras de abajo del casco a
     50541c (por defecto caían en 442c0c, de la horda).
  2. Las VENTANILLAS (parabrisas y mirillas de las chapas) salen en un objeto hijo, «Ventanillas», con el mismo origen
     que la locomotora, para que Marcos les dé emisión de noche.
  3. De noche (coordinador, 7-nov 00:57: el tren se veía demasiado oscuro en la vista cercana): hijo «Emision», una capa
     1,5 cm por fuera de los cristales y los faros en f0dc9c (encendida), que Marcos activa de noche con emisión, y hijo
     «Escape» con los dos tubos de escape del diésel, con el origen en lo alto del tubo delantero y +Z hacia arriba,
     para colgar el humo.
Jerarquía del FBX: SM_…_LODn (cuerpo) ├─ Ventanillas ├─ Emision └─ Escape. Mira a +Z en Unity. Medidas y lo SIN VERIFICAR, en locomotora_r1.
Entregar con --claro b8b8b8 (techo D-N42).
"""
import bpy
import locomotora_r1 as R
import bmesh
from mathutils import Vector
from montaje import marcar_piezas, separar_piezas, capa_emision

PIEZAS = R.PIEZAS + ["luz"]
COLORES = dict(R.COLORES, bastidor="48403c", rueda="48403c", metal="48403c", rejilla="48403c", optica="60546c", luz="f0dc9c")
PERMITIR = ("e08c30",)          # franja ámbar del frontal (D-N42, única excepción)
ABAJO_CASCO = "50541c"
L_MIN = 28.0
VALOR_PIEZAS = {"casco": ("b8b8b8", "50541c", "50541c")}
VENTANILLAS = {"Ventanillas": ["optica"], "Emision": ["luz"]}
ESCAPE = (0.4, -4.2, R.Z_CAJA + 0.4)           # boca del tubo delantero (el trasero, 0,6 m detrás)


def construir(nombre="SM_Locomotora", lod=0):
    o = R.construir(nombre, lod)
    _luces_extra(o)
    capa_emision(o, ("optica", "foco"))
    return marcar_piezas(o)


def _luces_extra(o):
    """Luces de posición en las cuatro esquinas de los frontales y un farol junto a cada puerta de cabina (SIN
    VERIFICAR en la 333 blindada: es para que de noche se lea el contorno). En la pieza «foco»."""
    nombres = [m.name.split(".")[0] for m in o.data.materials]
    k = nombres.index("foco")
    b = bmesh.new()
    b.from_mesh(o.data)
    cajas = [((x, s * (R.Y0 + 0.03), 2.4), (0.22, 0.05, 0.2)) for x in (-1.05, 1.05) for s in (-1, 1)]
    cajas += [((x * 1.63, s * 8.9, 3.25), (0.05, 0.28, 0.2)) for x in (-1, 1) for s in (-1, 1)]
    for c, t in cajas:
        r = bmesh.ops.create_cube(b, size=1.0)
        for v in r["verts"]:
            v.co = Vector((v.co.x * t[0] + c[0], v.co.y * t[1] + c[1], v.co.z * t[2] + c[2]))
        for f in {f for v in r["verts"] for f in v.link_faces}:
            f.material_index = k
    b.to_mesh(o.data)
    b.free()


def _escape(o):
    """Saca los tubos de escape (caras en x≈0,4, y entre −4,5 y −3,3, sobre el techo) a un hijo con el origen en ESCAPE."""
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    b = bmesh.new()
    b.from_mesh(o.data)
    def es_tubo(f):
        c = f.calc_center_median() + base
        return abs(c.x - 0.4) < 0.2 and -4.5 < c.y < -3.3 and c.z > R.Z_CAJA + 0.02
    sel = {f.index for f in b.faces if es_tubo(f)}
    b.free()
    n = o.copy()
    n.data = o.data.copy()
    n.name = n.data.name = "Escape"
    bpy.context.scene.collection.objects.link(n)
    p = Vector(ESCAPE) - base
    for ob, quedar in ((n, True), (o, False)):
        b = bmesh.new()
        b.from_mesh(ob.data)
        b.faces.ensure_lookup_table()
        bmesh.ops.delete(b, geom=[f for f in b.faces if (f.index in sel) != quedar], context="FACES")
        if quedar:
            bmesh.ops.translate(b, verts=b.verts, vec=-p)
        b.to_mesh(ob.data)
        b.free()
    n.parent = o
    n.matrix_parent_inverse.identity()
    n.location = p
    for k in ("tbd_piezas", "tbd_base"):
        if k in n:
            del n[k]
    return n


def separar(o, lod):
    esc = _escape(o)
    objs = separar_piezas(o, VENTANILLAS)
    if "tbd_base" in o:
        del o["tbd_base"]
    return objs + [esc]
