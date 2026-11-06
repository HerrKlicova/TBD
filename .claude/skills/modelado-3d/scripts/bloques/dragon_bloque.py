"""Bloque del VCR 8x8 Dragón (v4a). Medidas: 8 × 3 × 2,34 m (Wikipedia ES/EN, Paula). Resto de cotas estimadas
de la foto 3/4 (CC0) y SIN VERIFICAR: altura libre 0,45 m, ruedas de 1,24 m, ejes, torre 30 mm."""
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix

def perfil_casco(bm):
    # perfil lateral (y, z); delante = -Y
    P = [(-3.25, 0.45), (-4.0, 1.05), (-3.55, 1.45), (-2.55, 2.12), (3.95, 2.12), (4.0, 1.45), (4.0, 0.75), (3.55, 0.45)]
    izq = [bm.verts.new((-1.5, y, z)) for y, z in P]
    der = [bm.verts.new((1.5, y, z)) for y, z in P]
    n = len(P)
    bm.faces.new(izq[::-1]); bm.faces.new(der)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((izq[i], izq[j], der[j], der[i]))
    # flancos superiores inclinados hacia dentro: por encima de 1,45 m el casco mide 2,6 m
    for v in izq + der:
        if v.co.z > 1.5:
            v.co.x *= 2.6 / 3.0
    # belly en V suave
    for v in izq + der:
        if v.co.z < 0.5:
            v.co.x *= 0.8

def caja(bm, c, s, inclinar_frente=0.0):
    r = bmesh.ops.create_cube(bm, size=1.0)
    vs = r["verts"]
    for v in vs:
        v.co = Vector((v.co.x * s[0], v.co.y * s[1], v.co.z * s[2]))
        if inclinar_frente and v.co.y < 0 and v.co.z > 0:
            v.co.y += inclinar_frente
        v.co += Vector(c)
    return vs

def cilindro(bm, c, r, largo, eje="X", lados=16):
    res = bmesh.ops.create_cone(bm, cap_ends=True, segments=lados, radius1=r, radius2=r, depth=largo)
    rot = {"X": Matrix.Rotation(math.pi / 2, 3, "Y"), "Y": Matrix.Rotation(math.pi / 2, 3, "X"), "Z": Matrix.Identity(3)}[eje]
    for v in res["verts"]:
        v.co = rot @ v.co + Vector(c)

def construir(nombre="SM_VCR_Dragon_bloque"):
    me = bpy.data.meshes.new(nombre)
    bm = bmesh.new()
    perfil_casco(bm)
    for y in (-2.55, -1.15, 1.0, 2.4):          # ejes (estimados)
        for x in (-1.22, 1.22):
            cilindro(bm, (x, y, 0.62), 0.62, 0.42, "X", 14)
            cilindro(bm, (x * 1.18, y, 0.62), 0.28, 0.06, "X", 10)  # buje
    # torre RCWS 30 mm (estimada): base, cuerpo inclinado delante, mantelete, cañón, visor
    caja(bm, (0.35, -0.1, 2.12 + 0.14), (1.9, 1.9, 0.28))
    caja(bm, (0.35, 0.0, 2.40 + 0.28), (1.5, 1.5, 0.56), inclinar_frente=0.35)
    caja(bm, (0.35, -0.9, 2.62), (0.42, 0.35, 0.34))
    cilindro(bm, (0.35, -2.15, 2.62), 0.06, 2.4, "Y", 8)
    caja(bm, (-0.25, 0.35, 3.10), (0.32, 0.32, 0.36))
    # escotilla del jefe y periscopios del conductor
    cilindro(bm, (-0.75, -1.7, 2.18), 0.33, 0.12, "Z", 10)
    caja(bm, (-0.75, -2.55, 2.20), (0.7, 0.18, 0.14))
    # rampa trasera marcada y cajones laterales
    caja(bm, (0.0, 4.02, 1.25), (1.6, 0.06, 1.2))
    for x in (-1.36, 1.36):
        caja(bm, (x, 1.7, 1.75), (0.12, 2.2, 0.35))
    bm.to_mesh(me); bm.free()
    obj = bpy.data.objects.new(nombre, me)
    bpy.context.scene.collection.objects.link(obj)
    # bisel de 1 segmento en las aristas vivas (ayuda al contorno por normales)
    m = obj.modifiers.new("bisel", "BEVEL"); m.width = 0.05; m.segments = 1; m.limit_method = "ANGLE"
    return obj

if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    o = construir()
    p = sys.argv[sys.argv.index("--") + 1]
    ns = {"__file__": p, "__name__": "malla3d"}; exec(open(p).read(), ns)
    me = ns["_malla_eval"](o); ns["_aplicar_malla"](o, me)
    print("tris", ns["num_tris"](o), "dims", [round(d, 2) for d in o.dimensions])
