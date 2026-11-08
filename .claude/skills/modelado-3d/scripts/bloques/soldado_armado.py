"""Soldado CON SU ARMA en la misma malla, enganchada al hueso «brazos» (Teo, 7-nov, 08:30). Arreglo de «las armas
aparecen en el suelo» (build de Marcos): las SM_Arma_* sueltas se exportaron con la conversión de ejes horneada y el
soldado no (su nodo lleva Lcl Rotation -90 en X, como todo personaje con esqueleto), así que un arma colgada del nodo
del soldado con transform identidad queda girada 90° y tumbada en el suelo. Aquí el arma es parte de la malla y va al
100 % al hueso «brazos» (el C90, al «pecho»): no hay que emparentar nada.
Mismo cuerpo, mismos colores y mismo esqueleto que soldado.py (aprobado); solo se añade el arma.
Arma por variante (propuesta por las dotaciones de decisiones-noche.md; Marcos puede pedir otra combinación):
  v0 G36E · v1 G36E con AG36 (granadero) · v2 G36E · v3 MG4E (ametrallador) · v4 G28 (tirador) · v5 G36E (jefe)
TBD_POSE=reposo o apuntando: malla FIJA (sin esqueleto) en la pose «apuntando» (§4.3 del encargo de control de disparos), para
cambiar de malla en DOTS. Sin TBD_POSE: malla con esqueleto y clips (reposo, andar, disparar, apuntar, morir).
"""
import os
import bpy
import soldado
from soldado import (PIEZAS, COLORES, VALOR_PIEZAS, FACCION, NORMALES_ARRIBA, NORMALES_INCLINACION, PRESUPUESTO,
                     UMBRAL_CONTORNO, HUESOS)

ARMA_DE = {0: 0, 1: 1, 2: 0, 3: 2, 4: 3, 5: 0}
NOMBRES = ("G36E", "G36E_AG36", "MG4E", "G28", "C90")
POSE = os.environ.get("TBD_POSE", "")
_POSE_M = None
SIN_HORNEAR = POSE in ("reposo", "apuntando")   # las mallas fijas se exportan como el soldado con esqueleto (nodo −90 en X)
# Boca del arma (Lucía, 8-nov 24:34): punto en el extremo del cañón, en la pose de reposo, hijo del hueso «brazos»;
# en las mallas fijas, ya llevado a su pose. t = distancia desde la empuñadura a lo largo de la línea de las manos y dz,
# los mismos de arma_infanteria.piezas_arma. El AG36 lleva una segunda boca (Muzzle_1). El C90 no está en estas seis.
_BOCA = {0: [(0.60, 0.02)], 1: [(0.60, 0.02), (0.56, -0.07)], 2: [(0.74, 0.02)], 3: [(0.80, 0.02)]}


def BOCAS(lod, raiz, objs):
    from mathutils import Vector, Matrix
    a = int(os.environ.get("TBD_ARMA_FORZADA", ARMA_DE[soldado.ASPECTO]))
    (xr, yr, zr), (xl, yl, zl) = soldado.MANOS
    d = Vector((xl - xr, yl - yr, zl - zr)).normalized()
    M = _POSE_M if (POSE in CLIP and _POSE_M is not None) else Matrix.Identity(4)
    base = Vector(raiz.get("tbd_base", (0.0, 0.0, 0.0)))
    padre = None if POSE in CLIP else "hueso:brazos"
    out = []
    for i, (t, dz) in enumerate(_BOCA.get(a, [])):
        p = Vector((xr + d.x * t, yr + d.y * t, zr + d.z * t + dz))
        out.append(("Muzzle_%d" % i, M @ p - base, M.to_3x3() @ d, padre))
    return out
CLIP = {"reposo": "reposo", "apuntando": "apuntar"}   # mallas fijas para cambiar de malla en DOTS


def construir(nombre="SM_SoldadoArmado", lod=0):
    a = ARMA_DE[soldado.ASPECTO]
    os.environ["TBD_ARMA"] = str(int(os.environ.get("TBD_ARMA_FORZADA", a)))
    try:
        o = soldado.construir(nombre, lod=lod)
    finally:
        os.environ.pop("TBD_ARMA", None)
    if POSE not in CLIP:
        return o
    from rig_humano import rig as _rig
    arm = _rig(o, HUESOS)
    arm.animation_data.action = bpy.data.actions[CLIP[POSE]]
    bpy.context.scene.frame_set(1)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    viejo = o.data
    o.modifiers.clear()
    o.parent = None
    o.vertex_groups.clear()
    o.data = me
    me.name = nombre
    bpy.data.meshes.remove(viejo)
    for act in list(bpy.data.actions):
        bpy.data.actions.remove(act)
    global _POSE_M   # para llevar la boca del arma de la pose de reposo a la pose fija
    pb = arm.pose.bones["brazos"]
    _POSE_M = arm.matrix_world @ pb.matrix @ arm.data.bones["brazos"].matrix_local.inverted()
    d = arm.data
    bpy.data.objects.remove(arm, do_unlink=True)
    bpy.data.armatures.remove(d)
    return o


if POSE not in CLIP:
    rig = soldado.rig
