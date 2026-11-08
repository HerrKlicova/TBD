"""Piezas de ESQUINA y REMATE para los muros por estados (HESCO y sacos), a partir de sus tramos aprobados
(muro_hesco_estados y muro_sacos_estados: mismas variantes, estados, colores y daños). Teo (3), 8-nov.

Se construyen tramos cortos del módulo del muro y se juntan en L:
- ESQUINA (esquina exterior): un bloque de esquina en el origen y dos brazos, uno hacia +X (fuera = −Y) y otro hacia
  +Y (fuera = −X). El recinto queda en el cuadrante +X +Y; la esquina de fuera apunta a (−, −).
- REMATE (fin de muro): el bloque de esquina, el brazo hacia +X y una vuelta corta de un solo bloque hacia +Y (hacia
  dentro), que cierra el extremo del muro como en las defensas de campaña.
Los restos del daño que caerían lejos de un tramo corto se quitan (islas a más de 1,2 m del brazo).
"""
import math
import bpy, bmesh
from mathutils import Matrix, Vector


def _tramo(mod, largo, n=None, nombre="tmp", lod=0, dx=0.0, giro=0.0):
    viejo = (mod.LARGO, getattr(mod, "N", None), getattr(mod, "PASO", None))
    mod.LARGO = largo
    if n is not None:
        mod.N = n
        mod.PASO = largo / n
    try:
        o = mod.construir(nombre, lod=lod)
    finally:
        mod.LARGO = viejo[0]
        if n is not None:
            mod.N, mod.PASO = viejo[1], viejo[2]
    bm = bmesh.new()
    bm.from_mesh(o.data)
    # quita las islas (cajas sueltas de escombro) que caen lejos del tramo corto
    vistos = set()
    fuera = []
    for f in bm.faces:
        if f in vistos:
            continue
        isla, pila = set(), [f]
        while pila:
            g = pila.pop()
            if g in isla:
                continue
            isla.add(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in isla:
                        pila.append(h)
        vistos |= isla
        vs = {v for g in isla for v in g.verts}
        c = sum((v.co for v in vs), Vector()) / len(vs)
        if abs(c.x) > largo / 2 + 1.2:
            fuera.extend(isla)
    if fuera:
        bmesh.ops.delete(bm, geom=list(set(fuera)), context="FACES")
    M = Matrix.Rotation(math.radians(giro), 4, "Z") @ Matrix.Translation((dx, 0, 0))
    bm.transform(M)
    me = o.data
    bm.to_mesh(me)
    bm.free()
    return o


def juntar(nombre, partes):
    bm = bmesh.new()
    mats = None
    for o in partes:
        if mats is None:
            mats = list(o.data.materials)
        bm.from_mesh(o.data)
    me = bpy.data.meshes.new(nombre)
    bm.to_mesh(me)
    bm.free()
    for mt in mats:
        me.materials.append(mt)
    obj = bpy.data.objects.new(nombre, me)
    bpy.context.scene.collection.objects.link(obj)
    for o in partes:
        d = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.meshes.remove(d)
    return obj


def pieza(mod, tipo, nombre, lod, bloque, brazo, n_bloque=None, n_brazo=None, solape=0.0):
    """tipo 0 esquina, 1 remate. bloque: lado del bloque de esquina; brazo: largo de cada brazo."""
    c = bloque / 2 + brazo / 2 - solape
    partes = [_tramo(mod, bloque, n_bloque, "tmp_c", lod),
              _tramo(mod, brazo, n_brazo, "tmp_a", lod, dx=c)]
    if tipo == 0:
        # brazo hacia −X (fuera −Y) girado −90°: queda hacia +Y con fuera −X
        partes.append(_tramo(mod, brazo, n_brazo, "tmp_b", lod, dx=-c, giro=-90.0))
    else:
        cv = bloque - solape                          # vuelta de un bloque hacia dentro (+Y)
        partes.append(_tramo(mod, bloque, n_bloque, "tmp_b", lod, dx=-cv, giro=-90.0))
    return juntar(nombre, partes)


def separar(o, lod):
    from mathutils import Vector, Matrix
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    o.data.transform(Matrix.Translation(base))
    if "tbd_base" in o:
        del o["tbd_base"]
    return [o]
