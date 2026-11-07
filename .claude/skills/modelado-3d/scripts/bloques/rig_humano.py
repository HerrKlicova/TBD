"""Esqueleto y animaciones del soldado (Teo, 6-nov-2026, 23:20). Rígido: cada vértice va al 100 % a un hueso, que sale
del atributo de cara «hueso» que pone soldado.Cuerpo. Animaciones (24 fps), una acción por clip, para que el FBX las
exporte como tomas separadas que Unity importa como clips: reposo (24 f, bucle), andar (24 f, bucle, un paso completo
a ~1,4 m/s: avanza 0,7 m por ciclo si se quiere root motion, aquí sin desplazamiento), disparar (12 f, bucle: encara y
dos retrocesos), apuntar (12 f, pose fija) y morir (36 f, sin bucle: flojea de rodillas y cae de espaldas; revisión de Lucía 23:17: sin
arrodillarse, para que las piernas no se separen de la pelvis en ningún fotograma).
Ángulos SIN VERIFICAR contra captura de movimiento; ajustados a ojo en el render a tamaño de juego."""
import math
import bpy

HUESOS = {   # nombre: (cabeza, cola, padre)
    "raiz": ((0, 0, 0), (0, 0, 0.3), None),
    "cadera": ((0, 0, 0.93), (0, 0, 1.02), "raiz"),
    "pecho": ((0, 0, 1.02), (0, 0, 1.45), "cadera"),
    "cabeza": ((0, 0, 1.47), (0, 0, 1.75), "pecho"),
    "brazos": ((0, 0, 1.40), (0, -0.3, 1.40), "pecho"),
    "muslo_L": ((0.1, 0, 0.93), (0.1, 0, 0.5), "cadera"),
    "pierna_L": ((0.1, 0, 0.5), (0.1, 0, 0.08), "muslo_L"),
    "muslo_R": ((-0.1, 0, 0.93), (-0.1, 0, 0.5), "cadera"),
    "pierna_R": ((-0.1, 0, 0.5), (-0.1, 0, 0.08), "muslo_R"),
}
R = math.radians
# pose «apuntando» (grados XYZ en el espacio de cada hueso, como las demás claves)
POSE_APUNTAR = {"brazos": (24, 0, 0), "pecho": (-6, 0, 0), "cabeza": (8, 0, 0), "muslo_L": (-12, 0, 0),
                "muslo_R": (8, 0, 0), "pierna_R": (10, 0, 0)}
POSE_APUNTAR_LOC = (0, -0.12, 0.0)   # el hueso mira a -Y: Y local negativa = hacia atrás, culata al hombro


def _clave(arm, f, hueso, rot=None, loc=None):
    pb = arm.pose.bones[hueso]
    pb.rotation_mode = "XYZ"
    if rot is not None:
        pb.rotation_euler = tuple(R(a) for a in rot)
        pb.keyframe_insert("rotation_euler", frame=f)
    if loc is not None:
        pb.location = loc
        pb.keyframe_insert("location", frame=f)


def _accion(arm, nombre, claves, fin, bucle=True):
    """claves: {frame: {hueso: (rot_xyz_grados, loc o None)}}. Todos los huesos animados se clavan en todos los frames
    clave, y el último frame repite el primero si es un bucle."""
    act = bpy.data.actions.new(nombre)
    act.use_fake_user = True
    arm.animation_data.action = act
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
    huesos = [pb.name for pb in arm.pose.bones]   # todos los huesos en todas las tomas: nada se arrastra de otra
    frames = sorted(claves)
    if bucle:
        claves = dict(claves)
        claves[fin] = claves[frames[0]]
        frames.append(fin)
    estado = {}
    for f in frames:
        for h in huesos:
            r, l = claves[f].get(h, estado.get(h, ((0, 0, 0), (0, 0, 0))))
            estado[h] = (r, l if l is not None else (0, 0, 0))
            _clave(arm, f, h, r, estado[h][1])
    act.frame_range = (frames[0], fin)
    arm.animation_data.action = None   # sin pistas NLA: el FBX exporta cada acción como una toma suelta
    return act


def animaciones(arm, tipo=0):
    arm.animation_data_create()
    # muslos: rot X positiva = pierna hacia atrás o adelante (se fija en el render); rodilla: pierna rot X
    p = 28
    _accion(arm, "reposo", {1: {"pecho": ((0, 0, 0), None)}, 12: {"pecho": ((-2, 0, 0), None)}}, 24)
    _accion(arm, "andar", {
        1: {"muslo_L": ((-p, 0, 0), None), "muslo_R": ((p, 0, 0), None), "pierna_L": ((0, 0, 0), None),
            "pierna_R": ((25, 0, 0), None), "cadera": ((0, 0, 4), (0, 0, 0)), "pecho": ((0, 0, -6), None)},
        7: {"muslo_L": ((0, 0, 0), None), "muslo_R": ((0, 0, 0), None), "pierna_L": ((5, 0, 0), None),
            "pierna_R": ((45, 0, 0), None), "cadera": ((0, 0, 0), (0, 0, 0.03)), "pecho": ((0, 0, 0), None)},
        13: {"muslo_L": ((p, 0, 0), None), "muslo_R": ((-p, 0, 0), None), "pierna_L": ((25, 0, 0), None),
             "pierna_R": ((0, 0, 0), None), "cadera": ((0, 0, -4), (0, 0, 0)), "pecho": ((0, 0, 6), None)},
        19: {"muslo_L": ((0, 0, 0), None), "muslo_R": ((0, 0, 0), None), "pierna_L": ((45, 0, 0), None),
             "pierna_R": ((5, 0, 0), None), "cadera": ((0, 0, 0), (0, 0, 0.03)), "pecho": ((0, 0, 0), None)},
    }, 25)
    a = 22 if tipo != 3 else 14   # encarar: el arma pasa de alerta baja a la horizontal
    _accion(arm, "disparar", {
        1: {"brazos": ((a, 0, 0), None), "pecho": ((0, 0, 0), None), "muslo_L": ((-12, 0, 0), None),
            "muslo_R": ((8, 0, 0), None), "pierna_R": ((10, 0, 0), None)},
        4: {"brazos": ((a + 5, 0, 0), None), "pecho": ((-4, 0, 0), None)},
        7: {"brazos": ((a, 0, 0), None), "pecho": ((0, 0, 0), None)},
        10: {"brazos": ((a + 5, 0, 0), None), "pecho": ((-4, 0, 0), None)},
    }, 13)
    # apuntando (Lucía, encargo de control de disparos §4.3, 7-nov): arma al hombro y a la horizontal, orientada al
    # frente; pose fija (bucle de 12 f sin movimiento). El paso de reposo a apuntando (0,25 s) y el retroceso (2 px,
    # 1 fotograma) los hace Marcos por código. Misma pose que el primer fotograma de «disparar», más el pecho
    # adelantado y la cabeza agachada sobre el arma.
    _accion(arm, "apuntar", {
        1: {"brazos": (POSE_APUNTAR["brazos"], POSE_APUNTAR_LOC), "pecho": (POSE_APUNTAR["pecho"], None),
            "cabeza": (POSE_APUNTAR["cabeza"], None), "muslo_L": (POSE_APUNTAR["muslo_L"], None),
            "muslo_R": (POSE_APUNTAR["muslo_R"], None), "pierna_R": (POSE_APUNTAR["pierna_R"], None)},
    }, 12)
    _accion(arm, "morir", {
        1: {"raiz": ((0, 0, 0), (0, 0, 0)), "muslo_L": ((0, 0, 0), None), "muslo_R": ((0, 0, 0), None),
            "pierna_L": ((0, 0, 0), None), "pierna_R": ((0, 0, 0), None), "brazos": ((0, 0, 0), None),
            "pecho": ((0, 0, 0), None), "cabeza": ((0, 0, 0), None)},
        8: {"raiz": ((-15, 0, 0), (0, 0.05, -0.12)), "muslo_L": ((-25, 0, 0), None), "muslo_R": ((-15, 0, 0), None),
            "pierna_L": ((40, 0, 0), None), "pierna_R": ((30, 0, 0), None), "pecho": ((8, 0, 0), None),
            "brazos": ((-30, 0, 0), None)},
        22: {"raiz": ((-88, 0, 0), (0, 0.2, 0.1)), "muslo_L": ((-20, 0, 0), None), "muslo_R": ((-5, 0, 0), None),
             "pierna_L": ((30, 0, 0), None), "pierna_R": ((10, 0, 0), None), "pecho": ((-5, 0, 0), None),
             "brazos": ((-70, 0, 10), None), "cabeza": ((-20, 0, 15), None)},
        36: {"raiz": ((-90, 0, 0), (0, 0.2, 0.1)), "muslo_L": ((-15, 0, 0), None), "muslo_R": ((0, 0, 0), None),
             "pierna_L": ((20, 0, 0), None), "pierna_R": ((5, 0, 0), None), "pecho": ((-8, 0, 0), None),
             "brazos": ((-80, 0, 15), None), "cabeza": ((-25, 0, 20), None)},
    }, 36, bucle=False)


def rig(o, nombres_huesos, tipo=0):
    """Crea el esqueleto, asigna los vértices por el atributo «hueso» y crea las acciones. Devuelve el armature."""
    ad = bpy.data.armatures.new(o.name + "_Rig")
    arm = bpy.data.objects.new(o.name + "_Rig", ad)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    for x in bpy.context.view_layer.objects:
        x.select_set(False)
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for n, (h, t, pad) in HUESOS.items():
        eb = ad.edit_bones.new(n)
        eb.head, eb.tail = h, t
        eb.roll = 0.0
        if pad:
            eb.parent = ad.edit_bones[pad]
    bpy.ops.object.mode_set(mode="OBJECT")
    me = o.data
    at = me.attributes.get("hueso")
    if at is None:
        raise RuntimeError("la malla no trae el atributo de cara «hueso»")
    vgs = {n: o.vertex_groups.new(name=n) for n in nombres_huesos}
    por_v = {}
    for pol in me.polygons:
        n = nombres_huesos[at.data[pol.index].value]
        for vi in pol.vertices:
            por_v.setdefault(vi, n)
    for vi, n in por_v.items():
        vgs[n].add([vi], 1.0, "REPLACE")
    o.parent = arm
    m = o.modifiers.new("Armature", "ARMATURE")
    m.object = arm
    animaciones(arm, tipo)
    return arm
