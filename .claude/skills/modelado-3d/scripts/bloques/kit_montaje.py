"""Montaje de estaciones con el kit (Teo, 7-nov). Junta módulos del kit en una sola malla: todos comparten PIEZAS
(kit_comun), así que los índices de material coinciden y la paleta no se rehace.

    juntar(nombre, lod, [(modulo, var, kwargs, (x, y, z), giro_z_en_grados), ...])

Es la versión de prueba (captura y visto bueno). En el juego, Marcos instancia cada módulo por separado desde el JSON
de la estación (castellon §3 de Álvaro); este montaje solo enseña cómo quedan juntos.
"""
import importlib, math
import bpy, bmesh
from mathutils import Matrix


def juntar(nombre, lod, lista):
    bm = bmesh.new()
    mats = None
    tmp = []
    for k, (mod, var, kw, pos, giro) in enumerate(lista):
        m = importlib.import_module(mod)
        o = m.construir("tmp_%s_%d" % (mod, k), lod=lod, var=var, **(kw or {}))
        M = Matrix.Translation(pos) @ Matrix.Rotation(math.radians(giro or 0.0), 4, "Z")
        o.data.transform(M)
        if mats is None:
            mats = [mt for mt in o.data.materials]
        else:
            assert [mt.name for mt in o.data.materials] == [mt.name for mt in mats], "PIEZAS distintas en " + mod
        bm.from_mesh(o.data)
        tmp.append(o)
    me = bpy.data.meshes.new(nombre)
    bm.to_mesh(me)
    bm.free()
    for mt in mats:
        me.materials.append(mt)
    obj = bpy.data.objects.new(nombre, me)
    bpy.context.scene.collection.objects.link(obj)
    for o in tmp:
        d = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.meshes.remove(d)
    return obj


def fila(mod, vars_, x0, paso, y, z=0.0, kw=None, giro=0.0):
    """Módulos en fila a lo largo de X empezando por el centro x0."""
    return [(mod, v, kw, (x0 + i * paso, y, z), giro) for i, v in enumerate(vars_)]
