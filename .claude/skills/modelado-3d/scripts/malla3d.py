"""malla3d.py · Limpieza, diezmado, verificación, paleta y vista pixelada de mallas en Blender.

Proyecto TBD (Vía Muerta). Escrito el 6-nov-2026 para la skill `modelado-3d`.
Probado con Blender 5.2 (módulo bpy 5.2.3 en Linux y Blender 5.2.1 en Windows).

Tres formas de usarlo:
  1. Desde el MCP oficial de Blender (execute_blender_code):
         ruta = r"C:/Dev/TBD/.claude/skills/modelado-3d/scripts/malla3d.py"
         ns = {"__file__": ruta}; exec(open(ruta, encoding="utf-8").read(), ns)
         result = ns["auditar"](bpy.data.objects["Loco"], presupuesto=(1500, 4000))
  2. Por consola, con el Blender instalado:
         blender -b -P malla3d.py -- entrada.glb --objetivo 3000 --salida carpeta/ --nombre SM_Loco
  3. Con el módulo bpy de PyPI (nube):  python malla3d.py entrada.glb --objetivo 3000 --salida carpeta/

Todo devuelve diccionarios JSON. Nada se conecta a internet ni gasta generaciones.
"""

import json
import math
import os
import struct
import sys
import zlib

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.interpolate import poly_3d_calc

AQUI = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()

# ---------------------------------------------------------------- paleta

TOKENS_INTERFAZ = ("982420", "e08c30")  # rojo de peligro y ámbar de alerta: nunca como color de un objeto (regla 4)
# Lucía (6-nov): en un objeto no queda ningún rojo. Se prohíbe toda la familia roja y el ámbar, y los rojos y naranjas
# de origen se reasignan POR VALOR a oliva, ocre o gris (no al rojo más cercano de la paleta).
PROHIBIDOS_OBJETO = TOKENS_INTERFAZ + ("340000", "4c1008", "642030", "7c181c", "8c5048")
# Lucía (6-nov 09:31): ningún costado tiene color de suelo ni de zombi. Costados por defecto según el tipo de pieza.
COSTADO_POR_TIPO = {"militar": "50541c",  # oliva de los laterales del tren (D-N42): todo lo propio en la misma familia
                    "civil": "605c48"}    # gris pardo: carretillas, cajas, mobiliario
COSTADOS_PROHIBIDOS = ("48403c", "845c28",            # suelos (trinchera y llano)
                       "6c7874", "94948c", "404460",  # grises de las texturas A7 (hormigón, balasto)
                       "60546c")                      # costado de los zombis
SUSTITUTOS_ROJO = ("303424", "48403c", "50541c", "605c48", "6c7874", "70784c", "845c28", "847c34", "88885c", "b8b8b8")


PALETA_POR_DEFECTO = os.path.join(AQUI, "..", "referencias", "paleta_32.hex")


def cargar_paleta(ruta=None):
    """Lista de 32 colores RGB 0-255 desde un .hex (uno por línea, sin #)."""
    ruta = ruta or PALETA_POR_DEFECTO
    cols = []
    with open(ruta, encoding="utf-8") as f:
        for ln in f:
            ln = ln.strip().lstrip("#")
            if len(ln) == 6:
                cols.append(tuple(int(ln[i:i + 2], 16) for i in (0, 2, 4)))
    if not cols:
        raise ValueError("paleta vacía: %s" % ruta)
    return np.array(cols, dtype=np.float64)


def _srgb_a_lab(rgb):
    """rgb (N,3) 0-255 sRGB -> Lab D65."""
    c = rgb / 255.0
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = c @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[:, 1] - 16, 500 * (f[:, 0] - f[:, 1]), 200 * (f[:, 1] - f[:, 2])], 1)


def indice_paleta(rgb, paleta):
    """Índice del color de paleta más cercano (distancia en Lab) para cada fila de rgb."""
    a = _srgb_a_lab(np.asarray(rgb, dtype=np.float64).reshape(-1, 3))
    b = _srgb_a_lab(paleta)
    d = ((a[:, None, :] - b[None, :, :]) ** 2).sum(2)
    return d.argmin(1)


# ---------------------------------------------------------------- utilidades

def _malla_eval(obj):
    """Copia de la malla con modificadores aplicados, en espacio de mundo."""
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    me.transform(obj.matrix_world)
    return me


def _tris(me):
    me.calc_loop_triangles()
    n = len(me.loop_triangles)
    v = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", v)
    t = np.empty(n * 3, dtype=np.int64)
    me.loop_triangles.foreach_get("vertices", t)
    return v.reshape(-1, 3), t.reshape(-1, 3)


def num_tris(obj):
    me = _malla_eval(obj)
    me.calc_loop_triangles()
    n = len(me.loop_triangles)
    bpy.data.meshes.remove(me)
    return n


def _aplicar_malla(obj, me_nueva):
    """Sustituye la malla del objeto por me_nueva (en espacio local actual) y quita modificadores."""
    vieja = obj.data
    obj.modifiers.clear()
    obj.data = me_nueva
    if vieja.users == 0:
        bpy.data.meshes.remove(vieja)


def _activar(obj):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


# ---------------------------------------------------------------- auditoría

def auditar(obj, presupuesto=(1500, 4000), altura_m=None, tolerancia_altura=0.15, uv_res=256):
    """Informe de la malla con OK / AVISO / ERROR por punto.

    presupuesto: (mín, máx) de triángulos. altura_m: altura real esperada (p. ej. 4.3 para una locomotora).
    """
    if obj.type != "MESH":
        return {"objeto": obj.name, "error": "no es una malla"}
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.transform(obj.matrix_world)
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    bm.normal_update()

    rep = {"objeto": obj.name, "puntos": []}

    def punto(nivel, clave, valor, nota=""):
        rep["puntos"].append({"nivel": nivel, "clave": clave, "valor": valor, "nota": nota})

    tris = sum(len(f.verts) - 2 for f in bm.faces)
    rep["triangulos"] = tris
    rep["vertices"] = len(bm.verts)
    lo, hi = presupuesto
    punto("OK" if lo <= tris <= hi else ("AVISO" if tris < lo else "ERROR"), "triangulos", tris,
          "presupuesto %d-%d" % (lo, hi))

    no_manifold = [e for e in bm.edges if not e.is_manifold and not e.is_boundary and not e.is_wire]
    borde = [e for e in bm.edges if e.is_boundary]
    sueltas = [e for e in bm.edges if e.is_wire]
    v_sueltos = [v for v in bm.verts if not v.link_faces]
    punto("OK" if not no_manifold else "ERROR", "aristas_no_manifold", len(no_manifold),
          "aristas con 3 o más caras")
    punto("OK" if not borde else "AVISO", "aristas_de_borde", len(borde),
          "agujeros; aceptables en piezas que no se ven por debajo")
    punto("OK" if not (sueltas or v_sueltos) else "ERROR", "geometria_suelta", len(sueltas) + len(v_sueltos))

    degeneradas = [f for f in bm.faces if f.calc_area() < 1e-8]
    punto("OK" if not degeneradas else "AVISO", "caras_degeneradas", len(degeneradas))
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    punto("OK" if not ngons else "AVISO", "ngons", len(ngons), "Unity triangula; mejor triangular en Blender")

    # vértices duplicados
    from mathutils.kdtree import KDTree
    kd = KDTree(len(bm.verts))
    for v in bm.verts:
        kd.insert(v.co, v.index)
    kd.balance()
    dup = 0
    for v in bm.verts:
        for (_, i, _) in kd.find_range(v.co, 1e-5):
            if i > v.index:
                dup += 1
    punto("OK" if dup == 0 else "AVISO", "vertices_duplicados", dup)

    # islas y orientación (volumen con signo de cada isla cerrada)
    islas = _islas(bm)
    tam = sorted((len(i) for i in islas), reverse=True)
    rep["islas"] = tam[:10]
    diminutas = sum(1 for t in tam[1:] if t < max(4, 0.01 * tam[0])) if tam else 0
    punto("OK" if diminutas == 0 else "AVISO", "islas_diminutas", diminutas,
          "trozos flotantes típicos de Hunyuan; quitar con limpiar()")
    invertidas = 0
    for isla in islas:
        cerrada = all(e.is_manifold for f in isla for e in f.edges)
        if cerrada and _volumen(isla) < 0:
            invertidas += 1
    punto("OK" if invertidas == 0 else "ERROR", "islas_con_normales_hacia_dentro", invertidas)

    # transformaciones y escala
    loc, rot, esc = obj.matrix_world.decompose()
    esc_ok = all(abs(s - 1) < 1e-4 for s in esc)
    rot_ok = rot.angle < 1e-4
    punto("OK" if esc_ok else "ERROR", "escala_aplicada", [round(s, 4) for s in esc])
    punto("OK" if rot_ok else "AVISO", "rotacion_aplicada", round(math.degrees(rot.angle), 3))
    xs = [v.co for v in bm.verts]
    mn = Vector((min(c.x for c in xs), min(c.y for c in xs), min(c.z for c in xs))) if xs else Vector()
    mx = Vector((max(c.x for c in xs), max(c.y for c in xs), max(c.z for c in xs))) if xs else Vector()
    dims = mx - mn
    rep["dimensiones_m"] = [round(d, 3) for d in dims]
    if altura_m:
        err = abs(dims.z - altura_m) / altura_m
        punto("OK" if err <= tolerancia_altura else "ERROR", "altura_real", round(dims.z, 3),
              "esperada %.2f m (±%d %%)" % (altura_m, tolerancia_altura * 100))
    base_ok = abs(mn.z - loc.z) < 0.01 * max(dims.z, 1e-6) and \
        abs((mn.x + mx.x) / 2 - loc.x) < 0.02 * max(dims.x, 1e-6) and \
        abs((mn.y + mx.y) / 2 - loc.y) < 0.02 * max(dims.y, 1e-6)
    punto("OK" if base_ok else "AVISO", "origen_en_la_base", base_ok, "origen en el centro de la base")

    # UV
    uv = bm.loops.layers.uv.active
    if uv is None:
        punto("ERROR", "uv", "sin capa UV")
    else:
        fuera = 0
        cero = 0
        img = np.zeros((uv_res, uv_res), dtype=np.uint16)
        for f in bm.faces:
            p = [l[uv].uv.copy() for l in f.loops]
            if any(c.x < -1e-4 or c.x > 1.0001 or c.y < -1e-4 or c.y > 1.0001 for c in p):
                fuera += 1
            area = 0.0
            for k in range(1, len(p) - 1):
                area += abs((p[k] - p[0]).cross(p[k + 1] - p[0])) / 2
                _raster_tri_add(img, p[0], p[k], p[k + 1], uv_res)
            if area < 1e-10:
                cero += 1
        usado = img > 0
        solape = (img > 1).sum() / max(usado.sum(), 1)
        rep["uv_ocupacion"] = round(float(usado.mean()), 3)
        punto("OK" if fuera == 0 else "AVISO", "uv_fuera_de_0_1", fuera)
        atlas = uv.name == "UV_paleta"
        punto("OK" if cero == 0 or atlas else "AVISO", "uv_caras_sin_area", cero,
              "normal en el atlas de paleta (cara entera en un color)")
        punto("OK" if solape < 0.02 or atlas else "AVISO", "uv_solape", round(float(solape), 3),
              "fracción de UV pisada dos veces; normal en el atlas de paleta")

    mats = [s.material for s in obj.material_slots]
    punto("OK" if 1 <= len(mats) <= 2 and all(mats) else "AVISO", "materiales", len(mats),
          "1 material por pieza (una llamada de dibujo)")
    bm.free()

    niveles = [p["nivel"] for p in rep["puntos"]]
    rep["veredicto"] = "ERROR" if "ERROR" in niveles else ("AVISO" if "AVISO" in niveles else "OK")
    return rep


def _islas(bm):
    vistos = set()
    islas = []
    for f in bm.faces:
        if f.index in vistos:
            continue
        pila = [f]
        vistos.add(f.index)
        isla = []
        while pila:
            g = pila.pop()
            isla.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in vistos:
                        vistos.add(h.index)
                        pila.append(h)
        islas.append(isla)
    return islas


def _volumen(caras):
    vol = 0.0
    for f in caras:
        vs = [v.co for v in f.verts]
        for k in range(1, len(vs) - 1):
            vol += vs[0].dot(vs[k].cross(vs[k + 1])) / 6.0
    return vol


def _raster_tri_add(img, a, b, c, res):
    xs = [a.x * res, b.x * res, c.x * res]
    ys = [a.y * res, b.y * res, c.y * res]
    x0, x1 = max(int(math.floor(min(xs))), 0), min(int(math.ceil(max(xs))), res - 1)
    y0, y1 = max(int(math.floor(min(ys))), 0), min(int(math.ceil(max(ys))), res - 1)
    if x1 < x0 or y1 < y0:
        return
    gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
    if abs(d) < 1e-12:
        return
    l1 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / d
    l2 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / d
    m = (l1 >= 0) & (l2 >= 0) & (1 - l1 - l2 >= 0)
    img[y0:y1 + 1, x0:x1 + 1] += m.astype(img.dtype)


# ---------------------------------------------------------------- limpieza

def limpiar(obj, fusion_m=0.0005, isla_min=0.01, triangular_ngons=True, origen_base=True, rellenar_agujeros=False):
    """Aplica transformaciones, fusiona duplicados, quita sueltos, islas diminutas y caras degeneradas,
    reorienta normales hacia fuera y lleva el origen al centro de la base. Devuelve qué hizo."""
    hecho = {}
    me = _malla_eval(obj)
    obj.matrix_world = Matrix.Identity(4)
    _aplicar_malla(obj, me)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    n0 = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=fusion_m)
    hecho["vertices_fusionados"] = n0 - len(bm.verts)
    bmesh.ops.dissolve_degenerate(bm, dist=fusion_m * 0.1, edges=bm.edges)
    sueltos = [v for v in bm.verts if not v.link_faces]
    aristas = [e for e in bm.edges if not e.link_faces]
    bmesh.ops.delete(bm, geom=aristas, context="EDGES")
    bmesh.ops.delete(bm, geom=[v for v in sueltos if v.is_valid], context="VERTS")
    hecho["sueltos_quitados"] = len(sueltos) + len(aristas)
    bm.faces.ensure_lookup_table()
    bm.faces.index_update()
    islas = _islas(bm)
    if islas:
        mayor = max(len(i) for i in islas)
        quitar = [f for i in islas if len(i) < max(4, isla_min * mayor) for f in i]
        hecho["islas_quitadas"] = sum(1 for i in islas if len(i) < max(4, isla_min * mayor))
        if quitar:
            bmesh.ops.delete(bm, geom=quitar, context="FACES")
            bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    if rellenar_agujeros:
        r = bmesh.ops.holes_fill(bm, edges=bm.edges, sides=64)
        hecho["agujeros_rellenos"] = len(r["faces"])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # recalc_face_normals hace cada isla coherente; si una isla cerrada queda con volumen negativo, se invierte
    bm.faces.index_update()
    for isla in _islas(bm):
        if all(e.is_manifold for f in isla for e in f.edges) and _volumen(isla) < 0:
            bmesh.ops.reverse_faces(bm, faces=isla)
    if triangular_ngons:
        ng = [f for f in bm.faces if len(f.verts) > 4]
        if ng:
            bmesh.ops.triangulate(bm, faces=ng)
        hecho["ngons_triangulados"] = len(ng)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    if origen_base:
        llevar_origen_a_base(obj)
    hecho["triangulos"] = num_tris(obj)
    return hecho


def llevar_origen_a_base(obj, poner_en_cero=True):
    me = obj.data
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mn, mx = co.min(0), co.max(0)
    base = np.array([(mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2]])
    me.transform(Matrix.Translation(Vector(-base)))
    me.update()
    # lo que se ha movido la malla: los pivotes de las piezas animables (separar) se dan en las coordenadas del bloque
    # y hay que restárselo (sin esto, las ruedas del Dragón giraban 11 cm descentradas)
    obj["tbd_base"] = [float(x) for x in base]
    obj.location = (0, 0, 0) if poner_en_cero else obj.location + Vector(base)


def escalar_a_altura(obj, altura_m):
    """Escala uniforme para que la pieza mida altura_m (Hunyuan sale en un cubo de ~1-2 unidades)."""
    me = obj.data
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    z = co.reshape(-1, 3)[:, 2]
    h = float(z.max() - z.min())
    if h <= 0:
        return {"error": "altura cero"}
    k = altura_m / h
    me.transform(Matrix.Scale(k, 4))
    me.update()
    return {"factor": round(k, 5)}


def escalar_a_largo(obj, largo_m):
    """Escala uniforme para que la dimensión horizontal mayor mida largo_m (vehículos: la longitud es la medida fiable;
    la altura suele no aclarar si incluye la torre)."""
    me = obj.data
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    l = float((co.max(0) - co.min(0))[:2].max())
    if l <= 0:
        return {"error": "largo cero"}
    k = largo_m / l
    me.transform(Matrix.Scale(k, 4))
    me.update()
    return {"factor": round(k, 5)}


def alisar_superficie(obj, iteraciones=10, lambda_factor=1.0):
    """Quita las ondulaciones de Hunyuan en superficies que deberían ser planas (techos, chapas) con un alisado
    laplaciano que conserva el volumen. Se aplica a la malla densa, antes de remallar y diezmar: si no, la luz de
    Unity marca cada bulto como una mancha de otro tono."""
    m = obj.modifiers.new("_laplaciano", "LAPLACIANSMOOTH")
    m.iterations = int(iteraciones)
    m.lambda_factor = lambda_factor
    m.lambda_border = 0.0
    m.use_volume_preserve = True
    m.use_normalized = True
    me = _malla_eval(obj)
    me.transform(obj.matrix_world.inverted())
    _aplicar_malla(obj, me)
    return {"iteraciones": int(iteraciones), "lambda": lambda_factor}


def remallar_voxel(obj, voxel_m=None, divisiones=160):
    """Remallado por vóxeles: cierra agujeros y une piezas. Pierde las UV y la textura (el color se recupera
    con paleta_desde_original). voxel_m por defecto = dimensión mayor / divisiones."""
    if voxel_m is None:
        # medida desde los vértices: obj.dimensions no se actualiza hasta el siguiente depsgraph tras transformar
        co = np.empty(len(obj.data.vertices) * 3)
        obj.data.vertices.foreach_get("co", co)
        co = co.reshape(-1, 3)
        voxel_m = float((co.max(0) - co.min(0)).max()) / divisiones
    m = obj.modifiers.new("remesh_voxel", "REMESH")
    m.mode = "VOXEL"
    m.voxel_size = voxel_m
    m.adaptivity = 0.0
    me = _malla_eval(obj)
    me.transform(obj.matrix_world.inverted())
    _aplicar_malla(obj, me)
    return {"voxel_m": round(voxel_m, 5), "triangulos": num_tris(obj)}


# ---------------------------------------------------------------- diezmado

def diezmar(obj, objetivo=3000, angulo_plano=2.0, tolerancia=0.04, simetria=None, medir_silueta=True):
    """Reduce a ~objetivo triángulos conservando la silueta.

    1) disolución planar suave (quita caras coplanarias sin cambiar la forma), 2) colapso por proporción con
    búsqueda binaria hasta caer en objetivo ± tolerancia, 3) triangula. Mide la silueta antes y después.
    simetria: None, 'X' o 'Y' (simetría del modificador Decimate)."""
    antes = _siluetas(obj) if medir_silueta else None
    t0 = num_tris(obj)
    if t0 > 20 * objetivo:
        # mallas muy densas (p. ej. tras los vóxeles): primero un colapso rápido a ~20× el objetivo, porque la
        # disolución planar de bmesh es lenta con cientos de miles de caras
        m = obj.modifiers.new("prediezmar", "DECIMATE")
        m.decimate_type = "COLLAPSE"
        m.ratio = 20 * objetivo / t0
        me = _malla_eval(obj)
        me.transform(obj.matrix_world.inverted())
        _aplicar_malla(obj, me)
    if num_tris(obj) > objetivo and angulo_plano > 0:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(angulo_plano), use_dissolve_boundaries=False,
                                 verts=bm.verts, edges=bm.edges)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
    t1 = num_tris(obj)
    if t1 > objetivo * (1 + tolerancia):
        lo, hi = 0.0, 1.0
        mejor = None
        for _ in range(14):
            r = (lo + hi) / 2
            m = obj.modifiers.new("diezmar", "DECIMATE")
            m.decimate_type = "COLLAPSE"
            m.ratio = r
            m.use_collapse_triangulate = True
            if simetria:
                m.use_symmetry = True
                m.symmetry_axis = simetria
            n = num_tris(obj)
            obj.modifiers.remove(m)
            if mejor is None or abs(n - objetivo) < abs(mejor[1] - objetivo):
                mejor = (r, n)
            if abs(n - objetivo) <= objetivo * tolerancia:
                break
            if n > objetivo:
                hi = r
            else:
                lo = r
        m = obj.modifiers.new("diezmar", "DECIMATE")
        m.decimate_type = "COLLAPSE"
        m.ratio = mejor[0]
        m.use_collapse_triangulate = True
        if simetria:
            m.use_symmetry = True
            m.symmetry_axis = simetria
        me = _malla_eval(obj)
        me.transform(obj.matrix_world.inverted())
        _aplicar_malla(obj, me)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    res = {"triangulos_antes": t0, "tras_planar": t1}
    res["no_manifold_separadas"] = reparar_no_manifold(obj)
    res["triangulos"] = num_tris(obj)
    if medir_silueta:
        despues = _siluetas(obj)
        # IoU estricta (informativa) y parecido con 1 px de tolerancia (decide): en piezas finas, como barras, un
        # píxel de desplazamiento hunde la IoU sin que se note nada en pantalla
        res["silueta_iou"] = {k: round(_iou(antes[k], despues[k]), 4) for k in antes}
        res["silueta_iou_min"] = min(res["silueta_iou"].values())
        res["silueta_1px"] = {k: round(_parecido_1px(antes[k], despues[k]), 4) for k in antes}
        res["silueta_1px_min"] = min(res["silueta_1px"].values())
        res["silueta_ok"] = res["silueta_1px_min"] >= UMBRAL_SILUETA
    return res


def reparar_no_manifold(obj):
    """El colapso puede dejar aristas con 3 o más caras. Se separan (pasan a borde) y se borran las caras
    duplicadas que compartan todos sus vértices. Devuelve cuántas aristas se trataron."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    vistas = {}
    dup = []
    for f in bm.faces:
        k = tuple(sorted(v.index for v in f.verts))
        if k in vistas:
            dup.append(f)
        else:
            vistas[k] = f
    if dup:
        bmesh.ops.delete(bm, geom=dup, context="FACES")
    malas = [e for e in bm.edges if len(e.link_faces) > 2]
    if malas:
        bmesh.ops.split_edges(bm, edges=malas)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return len(malas) + len(dup)


# ---------------------------------------------------------------- siluetas y vista pixelada (rasterizador propio)

VISTAS = {
    # nombre: (azimut°, elevación°). Azimut 0 = mirando desde -Y hacia +Y (vista «Front» de Blender).
    "frente": (0, 0), "izquierda": (-90, 0), "detras": (180, 0), "derecha": (90, 0),
    "arriba": (0, 90), "rts": (35, 40),
}


def _matriz_vista(az, el):
    """Matriz 3x3 que lleva el mundo a cámara (x derecha, y arriba, z hacia la cámara)."""
    a, e = math.radians(az), math.radians(el)
    # dirección desde el objeto hacia la cámara
    d = np.array([math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)])
    arriba = np.array([0.0, 0.0, 1.0]) if abs(e) < math.radians(89) else np.array([0.0, 1.0, 0.0])
    x = np.cross(arriba, d)
    x /= np.linalg.norm(x)
    y = np.cross(d, x)
    return np.stack([x, y, d])


def _proyectar(v, R, res, caja=None):
    p = v @ R.T
    if caja is None:
        mn, mx = p[:, :2].min(0), p[:, :2].max(0)
        c = (mn + mx) / 2
        s = (mx - mn).max() * 1.08 + 1e-9
        caja = (c, s)
    c, s = caja
    xy = (p[:, :2] - c) / s * res + res / 2
    xy[:, 1] = res - xy[:, 1]
    return xy, p[:, 2], caja


def _rasterizar(xy, z, tris, res, color=None):
    """Z-buffer. Devuelve (profundidad, índice de triángulo) con -1 en el fondo."""
    zb = np.full((res, res), -np.inf)
    ib = np.full((res, res), -1, dtype=np.int64)
    # vía rápida para mallas densas: los triángulos de ~1 px se marcan de golpe (centro y vértices) con numpy
    txy = xy[tris]                                   # (n, 3, 2)
    ext = txy.max(1) - txy.min(1)
    peque = (ext[:, 0] <= 1.5) & (ext[:, 1] <= 1.5)
    if peque.any():
        ks = np.nonzero(peque)[0]
        pts = np.concatenate([txy[ks].mean(1)[:, None, :], txy[ks]], 1).reshape(-1, 2)
        zs = np.repeat(z[tris[ks]].mean(1), 4)
        kk = np.repeat(ks, 4)
        px = pts.astype(np.int64)
        dentro = (px[:, 0] >= 0) & (px[:, 0] < res) & (px[:, 1] >= 0) & (px[:, 1] < res)
        px, zs, kk = px[dentro], zs[dentro], kk[dentro]
        orden = np.argsort(zs)                       # el más cercano se escribe el último y gana
        px, zs, kk = px[orden], zs[orden], kk[orden]
        zb[px[:, 1], px[:, 0]] = zs
        ib[px[:, 1], px[:, 0]] = kk
    for k in np.nonzero(~peque)[0]:
        i, j, l = tris[k]
        x = xy[[i, j, l], 0]
        y = xy[[i, j, l], 1]
        x0, x1 = max(int(x.min()), 0), min(int(math.ceil(x.max())), res - 1)
        y0, y1 = max(int(y.min()), 0), min(int(math.ceil(y.max())), res - 1)
        if x1 < x0 or y1 < y0:
            continue
        d = (y[1] - y[2]) * (x[0] - x[2]) + (x[2] - x[1]) * (y[0] - y[2])
        if abs(d) < 1e-12:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        l1 = ((y[1] - y[2]) * (gx - x[2]) + (x[2] - x[1]) * (gy - y[2])) / d
        l2 = ((y[2] - y[0]) * (gx - x[2]) + (x[0] - x[2]) * (gy - y[2])) / d
        l3 = 1 - l1 - l2
        m = (l1 >= -1e-6) & (l2 >= -1e-6) & (l3 >= -1e-6)
        if not m.any():
            # triángulo más pequeño que un píxel: marca el píxel de su centro
            cx, cy = int(x.mean()), int(y.mean())
            if 0 <= cx < res and 0 <= cy < res:
                zz = z[[i, j, l]].mean()
                if zz > zb[cy, cx]:
                    zb[cy, cx] = zz
                    ib[cy, cx] = k
            continue
        zz = l1 * z[i] + l2 * z[j] + l3 * z[l]
        sub_z = zb[y0:y1 + 1, x0:x1 + 1]
        gana = m & (zz > sub_z)
        sub_z[gana] = zz[gana]
        ib[y0:y1 + 1, x0:x1 + 1][gana] = k
    return zb, ib


def _siluetas(obj, res=256):
    me = _malla_eval(obj)
    v, t = _tris(me)
    bpy.data.meshes.remove(me)
    out = {}
    for nombre in ("frente", "izquierda", "arriba", "rts"):
        R = _matriz_vista(*VISTAS[nombre])
        xy, z, _ = _proyectar(v, R, res, caja=_caja_fija(obj, R))
        _, ib = _rasterizar(xy, z, t, res)
        out[nombre] = ib >= 0
    return out


_CAJAS = {}


def _caja_fija(obj, R):
    """La misma caja de proyección antes y después de diezmar (se fija con la primera medición)."""
    clave = (obj.name, R.tobytes())
    if clave not in _CAJAS:
        me = _malla_eval(obj)
        v, _ = _tris(me)
        bpy.data.meshes.remove(me)
        p = v @ R.T
        mn, mx = p[:, :2].min(0), p[:, :2].max(0)
        _CAJAS[clave] = ((mn + mx) / 2, (mx - mn).max() * 1.08 + 1e-9)
    return _CAJAS[clave]


UMBRAL_SILUETA = 0.985


def _dilatar(m):
    d = m.copy()
    d[1:] |= m[:-1]
    d[:-1] |= m[1:]
    d[:, 1:] |= m[:, :-1]
    d[:, :-1] |= m[:, 1:]
    return d


def _parecido_1px(a, b):
    """1 - (píxeles de una silueta a más de 1 px de la otra) / unión."""
    u = (a | b).sum()
    if not u:
        return 1.0
    fallo = (a & ~_dilatar(b)) | (b & ~_dilatar(a))
    return float(1 - fallo.sum() / u)


def _iou(a, b):
    u = (a | b).sum()
    return float((a & b).sum() / u) if u else 1.0


def _png(ruta, rgba):
    """Escribe un PNG RGBA 8 bits sin dependencias."""
    h, w, _ = rgba.shape
    raw = b"".join(b"\x00" + rgba[y].astype(np.uint8).tobytes() for y in range(h))

    def bloque(tipo, datos):
        c = struct.pack(">I", len(datos)) + tipo + datos
        return c + struct.pack(">I", zlib.crc32(tipo + datos) & 0xFFFFFFFF)
    with open(ruta, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + bloque(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) +
                bloque(b"IDAT", zlib.compress(raw, 9)) + bloque(b"IEND", b""))


def colores_por_cara(obj):
    """Color base (RGB 0-255) de cada triángulo: del atlas de paleta o de la textura si la hay, si no gris."""
    me = _malla_eval(obj)
    me.calc_loop_triangles()
    n = len(me.loop_triangles)
    col = np.full((n, 3), 150.0)
    img, uvl = _imagen_y_uv(obj, me)
    if img is not None and uvl is not None:
        px, w, h = img
        for k, lt in enumerate(me.loop_triangles):
            u = sum(uvl[li].uv.x for li in lt.loops) / 3
            v = sum(uvl[li].uv.y for li in lt.loops) / 3
            col[k] = _muestra(px, w, h, u, v)
    bpy.data.meshes.remove(me)
    return col


def _imagen_y_uv(obj, me):
    uvl = me.uv_layers.active.data if me.uv_layers.active else None
    for s in obj.material_slots:
        m = s.material
        if m and m.use_nodes:
            for nd in m.node_tree.nodes:
                if nd.type == "TEX_IMAGE" and nd.image and nd.image.size[0] > 0:
                    im = nd.image
                    w, h = im.size
                    px = np.empty(w * h * 4, dtype=np.float32)
                    im.pixels.foreach_get(px)
                    px = px.reshape(h, w, 4)
                    px = px * 255.0  # imágenes de 8 bits: Blender da los valores tal cual (sRGB), sin linealizar
                    return (px, w, h), uvl
    return None, uvl


def _muestra(px, w, h, u, v):
    x = min(max(int((u % 1.0) * w), 0), w - 1)
    y = min(max(int((v % 1.0) * h), 0), h - 1)
    return px[y, x, :3]


def _render(obj, vista="rts", res=128, paleta=None, intensidad=0.65, fondo=(0x18, 0x1c, 0x30), contorno=True,
            luz_on=True, umbral_contorno=0.06):
    """Render propio: (imagen RGB, máscara del modelo, máscara del contorno)."""
    pal = cargar_paleta() if paleta is None else paleta
    me = _malla_eval(obj)
    v, t = _tris(me)
    nc = None   # normales horneadas (p. ej. hacia arriba en las matas): el render usa las mismas que verá Unity
    if obj.get("tbd_normales_arriba"):
        nc = np.array([np.mean([tuple(x) for x in lt.split_normals], axis=0) for lt in me.loop_triangles])
    bpy.data.meshes.remove(me)
    R = _matriz_vista(*VISTAS[vista])
    xy, z, _ = _proyectar(v, R, res)
    zb, ib = _rasterizar(xy, z, t, res)
    base = colores_por_cara(obj)
    n = np.cross(v[t[:, 1]] - v[t[:, 0]], v[t[:, 2]] - v[t[:, 0]])
    n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-12
    if nc is not None:
        n = nc / (np.linalg.norm(nc, axis=1, keepdims=True) + 1e-12)
    luz = np.array([-0.45, 0.35, 0.82])  # arriba a la izquierda, ligeramente desde delante
    luz /= np.linalg.norm(luz)
    lam = np.clip(n @ luz, 0, 1)
    tono = 0.45 + 0.55 * lam if luz_on else np.ones(len(t))
    img = np.zeros((res, res, 3))
    img[:] = fondo
    m = ib >= 0
    img[m] = base[ib[m]] * tono[ib[m], None]
    if intensidad > 0 and m.any():
        idx = indice_paleta(img[m], pal)
        img[m] = img[m] * (1 - intensidad) + pal[idx] * intensidad
    borde = np.zeros_like(m)
    if contorno:
        oscuro = pal[np.argmin(pal.sum(1))]
        zz = np.where(m, zb, -1e9)
        rango = float(np.ptp(z)) or 1.0
        for dy, dx in ((0, 1), (1, 0), (0, -1), (-1, 0)):
            vec = np.roll(np.roll(m, dy, 0), dx, 1)
            vz = np.roll(np.roll(zz, dy, 0), dx, 1)
            borde |= m & (~vec | (np.abs(vz - zz) > umbral_contorno * rango))
        img[borde] = oscuro
    return img, m, borde


def vista_pixelada(obj, ruta_png, vista="rts", res=128, ampliar=3, paleta=None, intensidad=None,
                   fondo=(0x18, 0x1c, 0x30), contorno=True, umbral_contorno=0.06):
    """umbral_contorno: salto de profundidad (fracción del fondo de la pieza) que dibuja línea interior. Con 0,15 solo
    quedan la silueta y los grandes cambios de volumen (regla de Lucía del 6-nov: el detalle interior va con valor).
    Imitación del shader del juego: render a baja resolución, luz desde arriba a la izquierda, acercamiento a la
    paleta al 65 %, contorno de 1 px por profundidad y vecino más cercano. Sirve para revisar sin abrir Unity;
    la última palabra la tiene la captura de Unity (regla 13). El contorno lo pone siempre el script (o el shader),
    nunca se pinta en la textura (regla 2 de encargo-modelos-hunyuan.md)."""
    if intensidad is None:   # Lucía 7-nov 09:08 (D-N129: el mundo va sin cuantizar): hojas sin acercar a la paleta;
        intensidad = float(os.environ.get("TBD_CUANTIZAR", "0") or 0)   # TBD_CUANTIZAR=0.65 da el render antiguo
    img, _, _ = _render(obj, vista, res, paleta, intensidad, fondo, contorno, umbral_contorno=umbral_contorno)
    rgba = np.concatenate([img, np.full((res, res, 1), 255.0)], 2)
    rgba = np.repeat(np.repeat(rgba, ampliar, 0), ampliar, 1)
    _png(ruta_png, np.clip(rgba, 0, 255))
    return {"png": ruta_png, "vista": vista, "res": res, "ampliar": ampliar}


# ---------------------------------------------------------------- legibilidad D-N57

SUELOS_POR_DEFECTO = {"trinchera": "48403c", "llano_ocre": "845c28",  # confirmado por Lucía el 6-nov (ocre de A7)
                      # D-N61 (corrección de las 10:18): el fondo real de la trinchera va a L* 42-46; 48403c queda solo
                      # en la franja de balasto bajo la vía. 6e6a5a (L* ~44) es un fondo de banco, no un color de paleta
                      "fondo_trinchera": "6e6a5a",
                      "sagunto": "c19e78"}   # suelo nuevo de la zona de Sagunto (Lucía, 6-nov 15:55)


def _hex_rgb(h):
    h = h.lstrip("#")
    return np.array([[int(h[i:i + 2], 16) for i in (0, 2, 4)]], dtype=np.float64)


def medir_legibilidad(obj, suelos=None, vista="rts", res=128):
    """D-N57: ΔL* = máx(|L* del 20 % más claro del interior − L* suelo|, |L* contorno − L* suelo|) ≥ 25, y el contorno
    por sí solo ≥ 25 (Iris). Se mide sobre el albedo horneado sin luz (lo que no depende del foco) con la paleta al 65 %.
    La medida que manda es la de cohesion_sheet.py --tipo modelo sobre la captura de Unity."""
    suelos = suelos or SUELOS_POR_DEFECTO
    img, m, borde = _render(obj, vista, res, luz_on=False)
    interior = m & ~borde
    if not interior.any():
        return {"error": "modelo sin interior visible"}
    L = _srgb_a_lab(img[interior])[:, 0]
    top = np.sort(L)[-max(1, len(L) // 5):].mean()
    Lc = float(_srgb_a_lab(img[borde][:1])[0, 0]) if borde.any() else None
    out = {"L_top20": round(float(top), 1), "L_contorno": None if Lc is None else round(Lc, 1), "suelos": {}}
    for nombre, hx in suelos.items():
        Ls = float(_srgb_a_lab(_hex_rgb(hx))[0, 0])
        d_top = abs(top - Ls)
        d_c = abs(Lc - Ls) if Lc is not None else 0.0
        out["suelos"][nombre] = {"L_suelo": round(Ls, 1), "dL_top20": round(d_top, 1), "dL_contorno": round(d_c, 1),
                                 "dL": round(max(d_top, d_c), 1), "ok": bool(max(d_top, d_c) >= 25 and d_c >= 25),
                                 "ok_top20": bool(d_top >= 25)}
    out["ok"] = bool(all(v["ok"] for v in out["suelos"].values()))
    return out


def _indices_atlas(obj):
    """Índice de paleta de cada cara, leído de la capa UV_paleta."""
    me = obj.data
    capa = me.uv_layers.get("UV_paleta")
    if capa is None:
        raise RuntimeError("primero paleta_desde_original()")
    idx = np.zeros(len(me.polygons), dtype=np.int64)
    for p in me.polygons:
        u, v = capa.data[p.loop_start].uv
        col = min(int(u * 8), 7)
        fila = min(int((1 - v) * 4), 3)
        idx[p.index] = fila * 8 + col
    return idx


def _poner_indices(obj, idx):
    me = obj.data
    capa = me.uv_layers["UV_paleta"]
    for p in me.polygons:
        i = int(idx[p.index])
        cu, cv = ((i % 8) + 0.5) / 8, 1 - ((i // 8) + 0.5) / 4
        for li in range(p.loop_start, p.loop_start + p.loop_total):
            capa.data[li].uv = (cu, cv)
    me.update()


def valor_partido(obj, claro="b4a080", costado=None, umbral_arriba=0.7, oscurecer=12.0, paleta=None, suavizar=3,
                  alisar=25,
                  excluir=PROHIBIDOS_OBJETO, mascara=None, permitir_costado=(), abajo=None):
    """D-N57 + regla 1 de Iris: valor partido horneado en el albedo. Caras que miran arriba (normal z > umbral) al
    color claro de la paleta (b4a080 por defecto, decisión de Lucía: 94948c es la camisa de los zombis; con claro=None se elige por el tono dominante); costados
    oscurecidos (al color de paleta más parecido con L* al menos `oscurecer` por debajo, o a `costado` si se da, p. ej.
    50541c oliva oscuro para tropa); caras de abajo, un paso más oscuras. Requiere paleta_desde_original().
    mascara: array booleano por cara; solo esas caras se repintan (p. ej. el casco y la torre, no la goma de las
    ruedas ni las ópticas de paleta_por_piezas).
    abajo: color fijo para las caras que miran abajo (p. ej. la copa del naranjo, que no puede caer en el valor de la
    horda: Lucía, 6-nov 16:55)."""
    pal = cargar_paleta() if paleta is None else paleta
    hexes = ["%02x%02x%02x" % tuple(int(x) for x in c) for c in pal]
    lab = _srgb_a_lab(pal)
    permit = np.array([h not in (excluir or ()) for h in hexes])
    idx = _indices_atlas(obj)
    me = obj.data
    # normales de una copia alisada (misma topología): las ondulaciones de Hunyuan no cuentan, solo la forma grande
    nz = None
    if alisar:
        m = obj.modifiers.new("_alisar", "SMOOTH")
        m.factor = 1.0
        m.iterations = int(alisar)
        dg = bpy.context.evaluated_depsgraph_get()
        me_a = obj.evaluated_get(dg).to_mesh()
        if len(me_a.polygons) == len(me.polygons):
            nz = np.array([(obj.matrix_world.to_3x3() @ p.normal).normalized().z for p in me_a.polygons])
        obj.evaluated_get(dg).to_mesh_clear()
        obj.modifiers.remove(m)
    if nz is None:
        nz = np.array([(obj.matrix_world.to_3x3() @ p.normal).normalized().z for p in me.polygons])
    areas = np.array([p.area for p in me.polygons])
    if claro is None:
        uso = np.bincount(idx, weights=areas, minlength=len(pal))
        dom = lab[uso.argmax()]
        claro = "94948c" if math.hypot(dom[1], dom[2]) < 8 else "b4a080"
    i_claro = hexes.index(claro.lower().lstrip("#"))

    def mas_oscuro(i, paso, suelo_L=12.0):
        # un paso más oscuro, sin caer al negro (el negro es del contorno) y sin pasarse de 25 de L*
        cand = np.where(permit & (lab[:, 0] <= lab[i, 0] - paso) & (lab[:, 0] >= max(suelo_L, lab[i, 0] - paso - 25)))[0]
        if not len(cand):
            cand = np.where(permit & (lab[:, 0] <= lab[i, 0] - paso) & (lab[:, 0] >= suelo_L))[0]
        if not len(cand):
            cand = np.where(permit & (lab[:, 0] >= suelo_L))[0]
            return int(cand[np.argmin(lab[cand, 0])])
        d = ((lab[cand, 1:] - lab[i, 1:]) ** 2).sum(1) + 0.3 * (lab[cand, 0] - (lab[i, 0] - paso)) ** 2
        return int(cand[d.argmin()])
    # permitir_costado: solo para el propio zombi, que es el dueño de 60546c y 6c7874 (sus costados en el sprite)
    prohib = tuple(h for h in COSTADOS_PROHIBIDOS if h not in permitir_costado)
    if costado and costado.lower().lstrip("#") in prohib:
        raise ValueError("costado %s prohibido (suelo o zombi): usa 50541c (militar) o 605c48 (civil)" % costado)
    permit = permit & np.array([h not in prohib for h in hexes])
    # clase de cada cara: 1 arriba, 0 costado, -1 abajo; se suaviza por mayoría (ponderada por área) entre vecinas
    # para que el ruido de Hunyuan no deje motas de costado en el techo ni de beige en los flancos
    clase = np.where(nz > umbral_arriba, 1, np.where(nz < -umbral_arriba, -1, 0))
    if suavizar:
        arista_caras = {}
        for p in me.polygons:
            for ek in p.edge_keys:
                arista_caras.setdefault(ek, []).append(p.index)
        vecinas = [[] for _ in me.polygons]
        for caras in arista_caras.values():
            for a in caras:
                vecinas[a].extend(c for c in caras if c != a)
        for _ in range(suavizar):
            nueva = clase.copy()
            for k, vs in enumerate(vecinas):
                if not vs:
                    continue
                peso = {}
                for c in vs + [k]:
                    peso[clase[c]] = peso.get(clase[c], 0.0) + areas[c]
                nueva[k] = max(peso, key=peso.get)
            clase = nueva
    nuevo = idx.copy()
    i_cost = hexes.index(costado.lower().lstrip("#")) if costado else None
    for k in range(len(idx)):
        if mascara is not None and not mascara[k]:
            continue
        if clase[k] == 1:
            nuevo[k] = i_claro
        elif clase[k] == -1:
            base = i_cost if i_cost is not None else idx[k]
            nuevo[k] = hexes.index(abajo.lower().lstrip("#")) if abajo else mas_oscuro(base, oscurecer + 8)
        else:
            nuevo[k] = i_cost if i_cost is not None else mas_oscuro(idx[k], oscurecer)
    _poner_indices(obj, nuevo)
    usados = sorted(set(int(i) for i in nuevo))
    return {"claro": claro, "costado": costado or "oscurecido por cara",
            "caras_arriba": int(((clase == 1) & (True if mascara is None else np.asarray(mascara))).sum()),
            "caras_costado": int(((clase == 0) & (True if mascara is None else np.asarray(mascara))).sum()),
            "colores_usados": len(usados), "hex": [hexes[i] for i in usados]}


def costuras_por_valor(obj, mascara, paso=(5.0, 12.0), paleta=None, excluir=PROHIBIDOS_OBJETO, tono_max=25.0,
                       holgura=2.0, suelo_L=0.0):
    """Chapa sin ruido (regla de Lucía del 6-nov): las caras de `mascara` (tiras de costura entre paneles, sacadas de
    las fotos) toman el color de sus caras vecinas que no son costura, oscurecido entre paso[0] y paso[1] de L* con el
    tono más parecido de la paleta. Así la diferencia interna no pasa de 12, como en los suelos. Llamar después de
    valor_partido()."""
    pal = cargar_paleta() if paleta is None else paleta
    hexes = ["%02x%02x%02x" % tuple(int(x) for x in c) for c in pal]
    lab = _srgb_a_lab(pal)
    permit = np.array([h not in (excluir or ()) and h not in COSTADOS_PROHIBIDOS for h in hexes])
    idx = _indices_atlas(obj)
    me = obj.data
    mascara = np.asarray(mascara, dtype=bool)
    arista_caras = {}
    for pol in me.polygons:
        for ek in pol.edge_keys:
            arista_caras.setdefault(ek, []).append(pol.index)
    # el color de referencia se propaga desde los paneles hacia dentro de la tira (varias pasadas)
    ref = np.where(mascara, -1, idx)
    for _ in range(6):
        cambio = False
        for pol in me.polygons:
            k = pol.index
            if not mascara[k] or ref[k] >= 0:
                continue
            cuenta = {}
            for ek in pol.edge_keys:
                for c in arista_caras[ek]:
                    if c != k and ref[c] >= 0 and not mascara[c]:
                        cuenta[ref[c]] = cuenta.get(ref[c], 0) + 1
            if not cuenta:
                for ek in pol.edge_keys:
                    for c in arista_caras[ek]:
                        if c != k and ref[c] >= 0:
                            cuenta[ref[c]] = cuenta.get(ref[c], 0) + 1
            if cuenta:
                ref[k] = max(cuenta, key=cuenta.get)
                cambio = True
        if not cambio:
            break
    cache, nuevo, cambiadas = {}, idx.copy(), 0
    for k in np.where(mascara)[0]:
        i = int(ref[k]) if ref[k] >= 0 else int(idx[k])
        if i not in cache:
            # misma familia de tono (±tono_max grados de matiz): que el oliva no derive al óxido (Lucía, 6-nov).
            # Si la paleta no tiene ese paso dentro de paso[1], se admite hasta paso[1] + holgura
            matiz = np.degrees(np.arctan2(lab[:, 2], lab[:, 1]))
            dm = np.abs((matiz - matiz[i] + 180) % 360 - 180)
            familia = permit & ((dm <= tono_max) | (np.hypot(lab[:, 1], lab[:, 2]) < 8))
            cache[i] = i  # sin vecino válido en la paleta: la costura no se marca (mejor que meter ruido)
            for tope in (paso[1], paso[1] + holgura):
                # suelo_L (D-N80, Bruno): ningún albedo propio bajo L* 28; si no hay paso por encima, no hay costura
                cand = np.where(familia & (lab[:, 0] <= lab[i, 0] - paso[0]) & (lab[:, 0] >= max(lab[i, 0] - tope, suelo_L)))[0]
                if len(cand):
                    d = ((lab[cand, 1:] - lab[i, 1:]) ** 2).sum(1)
                    cache[i] = int(cand[d.argmin()])
                    break
        nuevo[k] = cache[i]
        cambiadas += int(cache[i] != i)
    _poner_indices(obj, nuevo)
    return {"caras_costura": int(mascara.sum()), "marcadas": cambiadas,
            "pares": {hexes[a]: hexes[b] for a, b in cache.items()},
            "dL": {hexes[a]: round(float(lab[a, 0] - lab[b, 0]), 1) for a, b in cache.items()}}


FACCIONES = ("propio", "civil", "zombi", "neutro")


def marcar_faccion(obj, faccion):
    """Regla 3 de Iris: el rim frío (#404460) solo en unidades propias. Se guarda como propiedad del objeto
    (tbd_faccion, tbd_rim) y viaja en el FBX como propiedad de usuario; en Unity un AssetPostprocessor
    (OnPostprocessGameObjectWithUserProperties) pone el marcador en el prefab (lo hace Marcos; no verificado)."""
    if faccion not in FACCIONES:
        raise ValueError("faccion debe ser una de %s" % (FACCIONES,))
    obj["tbd_faccion"] = faccion
    obj["tbd_rim"] = 1 if faccion == "propio" else 0
    return {"tbd_faccion": faccion, "tbd_rim": obj["tbd_rim"]}


def vistas_para_hunyuan(obj, carpeta, res=512):
    """Cuatro vistas ortográficas en RGBA (fondo transparente) con los nombres que pide Hunyuan3D-2mv
    (front, left, back, right). Útil para dar forma a partir de un bloque modelado en Blender.
    OJO (no verificado): el preprocesador de Hunyuan define left = «front clockwise 90»; comprobar el sentido con una
    pieza asimétrica antes de un lote y, si sale espejada, intercambiar left y right."""
    os.makedirs(carpeta, exist_ok=True)
    me = _malla_eval(obj)
    v, t = _tris(me)
    bpy.data.meshes.remove(me)
    base = colores_por_cara(obj)
    n = np.cross(v[t[:, 1]] - v[t[:, 0]], v[t[:, 2]] - v[t[:, 0]])
    n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-12
    # misma escala en las cuatro vistas: caja común
    p_all = np.concatenate([v @ _matriz_vista(*VISTAS[k]).T for k in ("frente", "izquierda", "detras", "derecha")])
    s = (p_all[:, :2].max(0) - p_all[:, :2].min(0)).max() * 1.15
    rutas = {}
    for nombre, clave in (("front", "frente"), ("left", "izquierda"), ("back", "detras"), ("right", "derecha")):
        R = _matriz_vista(*VISTAS[clave])
        p = v @ R.T
        c = (p[:, :2].min(0) + p[:, :2].max(0)) / 2
        xy, z, _ = _proyectar(v, R, res, caja=(c, s))
        _, ib = _rasterizar(xy, z, t, res)
        d = R[2]
        lam = np.clip(n @ (d * 0.6 + np.array([0, 0, 0.8])) / 1.0, 0, 1)
        img = np.zeros((res, res, 4))
        m = ib >= 0
        img[m, :3] = base[ib[m]] * (0.5 + 0.5 * lam[ib[m], None])
        img[m, 3] = 255
        ruta = os.path.join(carpeta, nombre + ".png")
        _png(ruta, img)
        rutas[nombre] = ruta
    return rutas


# ---------------------------------------------------------------- paleta en la malla (albedo plano)

def paleta_desde_original(obj, original=None, paleta=None, max_colores=10, suavizar=2, celda_px=4,
                          ruta_textura=None, excluir=PROHIBIDOS_OBJETO):
    """Albedo plano con la paleta de 32 (pedido de Lucía, D-N54): cada cara toma el color de la textura original
    (de `original` si se remalló, si no del propio objeto), se acerca a la paleta, se limita a max_colores y se
    suaviza por mayoría entre caras vecinas. Las UV pasan a un atlas de 8×4 celdas (una por color de la paleta).
    Escribe la textura (celda_px por celda, filtro de punto en Unity)."""
    pal = cargar_paleta() if paleta is None else paleta
    fuente = original or obj
    me_f = _malla_eval(fuente)
    me_f.calc_loop_triangles()
    img, uvl = _imagen_y_uv(fuente, me_f)
    vf, tf = _tris(me_f)
    bvh = BVHTree.FromPolygons([Vector(p) for p in vf], [tuple(int(i) for i in x) for x in tf])
    me = obj.data
    me.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.transform(obj.matrix_world)
    bm.faces.ensure_lookup_table()
    # 7 muestras por cara (centro y puntos hacia cada vértice y arista); gana el color de paleta más repetido
    pesos_m = [(1 / 3, 1 / 3, 1 / 3), (.6, .2, .2), (.2, .6, .2), (.2, .2, .6), (.45, .45, .1), (.1, .45, .45),
               (.45, .1, .45)]
    idx = np.full(len(bm.faces), int(indice_paleta(np.array([[150, 150, 150]]), pal)[0]))
    if img is not None and uvl is not None:
        muestras, dueno = [], []
        for f in bm.faces:
            vs = [v.co for v in f.verts]
            sub = [(vs[0], vs[k], vs[k + 1]) for k in range(1, len(vs) - 1)]
            for a, b, c in sub[:2]:
                for wa, wb, wc in pesos_m:
                    loc, _, k, _ = bvh.find_nearest(a * wa + b * wb + c * wc)
                    if k is None:
                        continue
                    lt = me_f.loop_triangles[k]
                    pw = poly_3d_calc([Vector(vf[i]) for i in tf[k]], loc)
                    u = sum(w * uvl[li].uv.x for w, li in zip(pw, lt.loops))
                    v = sum(w * uvl[li].uv.y for w, li in zip(pw, lt.loops))
                    muestras.append(_muestra(img[0], img[1], img[2], u, v))
                    dueno.append(f.index)
        hx = ["%02x%02x%02x" % tuple(int(x) for x in c) for c in pal]
        permitidos = np.array([i for i, h in enumerate(hx) if h not in (excluir or ())])
        sust = np.array([hx.index(h) for h in SUSTITUTOS_ROJO if h in hx])
        muestras = np.array(muestras)
        mi = permitidos[indice_paleta(muestras, pal[permitidos])]
        # rojos y naranjas de origen (croma alto, tono entre -30° y 75°) -> sustituto de L* más parecida
        lab_m = _srgb_a_lab(muestras)
        croma = np.hypot(lab_m[:, 1], lab_m[:, 2])
        tono = np.degrees(np.arctan2(lab_m[:, 2], lab_m[:, 1]))
        rojo = (croma > 18) & (tono > -30) & (tono < 75)
        if rojo.any() and len(sust):
            L_sust = _srgb_a_lab(pal[sust])[:, 0]
            mi[rojo] = sust[np.abs(lab_m[rojo, 0][:, None] - L_sust[None, :]).argmin(1)]
        dueno = np.array(dueno)
        cuenta = np.zeros((len(bm.faces), len(pal)))
        np.add.at(cuenta, (dueno, mi), 1)
        con = cuenta.sum(1) > 0
        idx[con] = cuenta[con].argmax(1)
    bpy.data.meshes.remove(me_f)

    # limitar a los max_colores más usados (por área)
    areas = np.array([f.calc_area() for f in bm.faces])
    uso = np.bincount(idx, weights=areas, minlength=len(pal))
    elegidos = np.argsort(-uso)[:max_colores]
    elegidos = elegidos[uso[elegidos] > 0]
    sub = pal[elegidos]
    idx = elegidos[indice_paleta(pal[idx], sub)]
    # suavizado por mayoría (quita motas de una cara)
    for _ in range(suavizar):
        nuevo = idx.copy()
        for f in bm.faces:
            vec = [g.index for e in f.edges for g in e.link_faces if g.index != f.index]
            if not vec:
                continue
            cuenta = np.bincount(idx[vec], minlength=len(pal))
            if cuenta.max() >= 2 and cuenta[idx[f.index]] == 0:
                nuevo[f.index] = cuenta.argmax()
        idx = nuevo
    # UV al atlas
    uv = bm.loops.layers.uv.get("UV_paleta") or bm.loops.layers.uv.new("UV_paleta")
    cols_atlas, filas_atlas = 8, int(math.ceil(len(pal) / 8))
    for f in bm.faces:
        i = int(idx[f.index])
        cu = ((i % cols_atlas) + 0.5) / cols_atlas
        cv = 1 - ((i // cols_atlas) + 0.5) / filas_atlas
        for l in f.loops:
            l[uv].uv = (cu, cv)
    bm.transform(obj.matrix_world.inverted())
    bm.to_mesh(me)
    bm.free()
    # dejar sólo la capa del atlas como activa y de render
    for capa in list(me.uv_layers):
        if capa.name != "UV_paleta":
            me.uv_layers.remove(capa)
    me.uv_layers.active = me.uv_layers["UV_paleta"]
    me.update()
    # textura y material
    w, h = cols_atlas * celda_px, filas_atlas * celda_px
    tex = np.zeros((h, w, 4))
    for i, c in enumerate(pal):
        fy, fx = i // cols_atlas, i % cols_atlas
        tex[fy * celda_px:(fy + 1) * celda_px, fx * celda_px:(fx + 1) * celda_px, :3] = c
        tex[fy * celda_px:(fy + 1) * celda_px, fx * celda_px:(fx + 1) * celda_px, 3] = 255
    ruta_textura = ruta_textura or os.path.join(bpy.app.tempdir or ".", "T_paleta32.png")
    _png(ruta_textura, tex)
    _material_paleta(obj, ruta_textura)
    usados = sorted(set(int(i) for i in idx))
    return {"colores_usados": len(usados), "hex": ["%02x%02x%02x" % tuple(int(x) for x in pal[i]) for i in usados],
            "textura": ruta_textura}


def paleta_uniforme(obj, color="50541c", paleta=None, ruta_textura=None, celda_px=4):
    """Atlas de paleta con todas las caras de un color (para bloques hechos en Blender, sin textura de origen).
    Después se aplica valor_partido() igual que a una pieza de Hunyuan."""
    pal = cargar_paleta() if paleta is None else paleta
    hexes = ["%02x%02x%02x" % tuple(int(x) for x in c) for c in pal]
    i = hexes.index(color.lower().lstrip("#"))
    me = obj.data
    capa = me.uv_layers.get("UV_paleta") or me.uv_layers.new(name="UV_paleta")
    for otra in [c for c in me.uv_layers if c.name != "UV_paleta"]:
        me.uv_layers.remove(otra)
    me.uv_layers.active = me.uv_layers["UV_paleta"]
    _poner_indices(obj, np.full(len(me.polygons), i))
    filas = int(math.ceil(len(pal) / 8))
    tex = np.zeros((filas * celda_px, 8 * celda_px, 4))
    for k, c in enumerate(pal):
        fy, fx = k // 8, k % 8
        tex[fy * celda_px:(fy + 1) * celda_px, fx * celda_px:(fx + 1) * celda_px, :3] = c
        tex[fy * celda_px:(fy + 1) * celda_px, fx * celda_px:(fx + 1) * celda_px, 3] = 255
    ruta_textura = ruta_textura or os.path.join(bpy.app.tempdir or ".", "T_paleta32.png")
    _png(ruta_textura, tex)
    _material_paleta(obj, ruta_textura)
    return {"color": color, "textura": ruta_textura}


def paleta_por_piezas(obj, colores, por_defecto="50541c", paleta=None, ruta_textura=None, celda_px=4,
                      excluir=PROHIBIDOS_OBJETO, permitir=()):
    """Albedo plano por pieza: cada cara toma el color de paleta asignado al nombre de su material de origen
    (p. ej. {"casco": "50541c", "goma": "303424", "llanta": "70784c", "optica": "181c30", "foco": "f0dc9c"}).
    Los colores salen de las fotos de referencia llevados a la paleta de 32 y sin rojos ni ámbar (excepción con
    permitir=, p. ej. la franja ámbar e08c30 del frontal de la locomotora, D-N42).
    Deja un solo material con el atlas (como paleta_uniforme) y devuelve la máscara de cada pieza para
    valor_partido(mascara=...)."""
    pal = cargar_paleta() if paleta is None else paleta
    hexes = ["%02x%02x%02x" % tuple(int(x) for x in c) for c in pal]
    for h in list(colores.values()) + [por_defecto]:
        h = h.lower().lstrip("#")
        if h not in hexes:
            raise ValueError("%s no está en la paleta de 32" % h)
        if h in (excluir or ()) and h not in permitir:
            raise ValueError("%s prohibido en objetos (familia roja o ámbar); excepción explícita con permitir=" % h)
    me = obj.data
    nombres = [m.name.split(".")[0] if m else "" for m in me.materials]
    mi = np.zeros(len(me.polygons), dtype=np.int64)
    me.polygons.foreach_get("material_index", mi)
    pieza = np.array([nombres[i] if i < len(nombres) else "" for i in mi])
    idx = np.array([hexes.index(colores.get(n, por_defecto).lower().lstrip("#")) for n in pieza])
    ruta = paleta_uniforme(obj, por_defecto, paleta=pal, ruta_textura=ruta_textura, celda_px=celda_px)["textura"]
    _poner_indices(obj, idx)
    obj.data.polygons.foreach_set("material_index", np.zeros(len(me.polygons), dtype=np.int32))
    mascaras = {n: pieza == n for n in set(pieza)}
    return {"textura": ruta, "piezas": {n: int(m.sum()) for n, m in mascaras.items()}, "mascaras": mascaras}


def _material_paleta(obj, ruta):
    mat = bpy.data.materials.get("MAT_Paleta32") or bpy.data.materials.new("MAT_Paleta32")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 1.0
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(ruta, check_existing=True)
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    obj.data.materials.clear()
    obj.data.materials.append(mat)


# ---------------------------------------------------------------- exportar

def exportar_unity(obj, ruta, formato="FBX", armature=None, extra=()):
    """FBX para Unity: escala 1 = 1 m, Y arriba, transformaciones horneadas, sombreado plano. Convención de ejes
    MEDIDA por Marcos en Unity (6-nov): un modelo que en Blender mira a -Y sale en Unity mirando a +Z, es decir, a
    transform.forward, que es lo que espera Unity. axis_forward="-Z" es el parámetro del exportador, no hacia dónde
    mira el modelo en Unity. Todos los bloques (Dragón, R1, R2, VAMTAC, zombi) se modelan con delante = -Y en Blender
    y usan este exportador, así que en Unity todos miran a +Z. Sombreado plano
    (normales por cara: el contorno del shader las usa). GLB si Unity tiene glTFast. Comprobar en Unity la
    escala contra un cubo de 1 m la primera vez (no verificado en este proyecto)."""
    _activar(obj)
    os.makedirs(os.path.dirname(os.path.abspath(ruta)), exist_ok=True)
    if armature is not None and formato.upper() == "FBX":   # personaje: malla + esqueleto + una toma por acción
        armature.select_set(True)
        bpy.ops.export_scene.fbx(filepath=ruta, use_selection=True, object_types={"MESH", "ARMATURE"},
                                 apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                                 bake_space_transform=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
                                 add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
                                 bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                                 bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                                 path_mode="COPY", embed_textures=False, use_custom_props=True)
        return {"ruta": ruta, "bytes": os.path.getsize(ruta)}
    for x in extra:   # hijos animables (ruedas, torre, rotores): van en el mismo FBX con su jerarquía
        x.select_set(True)
    if formato.upper() == "FBX":
        bpy.ops.export_scene.fbx(filepath=ruta, use_selection=True, object_types={"MESH"},
                                 apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                                 bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type="FACE",
                                 add_leaf_bones=False, path_mode="COPY", embed_textures=False, use_custom_props=True)
    else:
        bpy.ops.export_scene.gltf(filepath=ruta, export_format="GLB", use_selection=True, export_apply=True,
                                  export_extras=True)
    return {"ruta": ruta, "bytes": os.path.getsize(ruta)}


# ---------------------------------------------------------------- flujo completo

def importar(ruta):
    antes = set(bpy.data.objects)
    ext = os.path.splitext(ruta)[1].lower()
    if ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=ruta)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=ruta)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=ruta)
    else:
        raise ValueError("formato no soportado: " + ext)
    nuevos = [o for o in bpy.data.objects if o not in antes and o.type == "MESH"]
    if not nuevos:
        raise RuntimeError("no se importó ninguna malla")
    if len(nuevos) > 1:
        _activar(nuevos[0])
        for o in nuevos:
            o.select_set(True)
        with bpy.context.temp_override(active_object=nuevos[0], selected_editable_objects=nuevos):
            bpy.ops.object.join()
    obj = nuevos[0]
    # glTF importa con padres vacíos y rotaciones: se hornea todo al mundo
    me = _malla_eval(obj)
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)
    _aplicar_malla(obj, me)
    return obj


def procesar(ruta, salida, nombre=None, objetivo=3000, altura_m=None, voxel=True, max_colores=10,
             presupuesto=(1500, 4000), formato="FBX", faccion="neutro", partido=True, claro="b4a080", costado=None,
             suelos=None, tipo=None, largo_m=None, alisar=0, color_unico=None):
    """color_unico: hex de la paleta para salidas sin textura (p. ej. 4 vistas de un bloque): sustituye a
    paleta_desde_original.
    Hunyuan (GLB denso) -> pieza de juego: importar, escalar, limpiar, (remallar), diezmar, paleta, auditar,
    vistas y exportar. Devuelve el informe completo y lo guarda en <salida>/<nombre>_informe.json."""
    os.makedirs(salida, exist_ok=True)
    obj = importar(ruta)
    nombre = nombre or ("SM_" + os.path.splitext(os.path.basename(ruta))[0])
    obj.name = nombre
    inf = {"entrada": ruta, "nombre": nombre, "triangulos_entrada": num_tris(obj)}
    original = obj.copy()
    original.data = obj.data.copy()
    original.name = nombre + "_ORIGINAL"
    bpy.context.scene.collection.objects.link(original)
    original.hide_render = True
    original.hide_set(True)
    inf["limpiar"] = limpiar(obj)  # antes de escalar: las motas flotantes falsean la altura
    if altura_m or largo_m:
        co = np.empty(len(obj.data.vertices) * 3)
        obj.data.vertices.foreach_get("co", co)
        ext = np.ptp(co.reshape(-1, 3), axis=0)
        k = (largo_m / max(ext[:2].max(), 1e-9)) if largo_m else (altura_m / max(ext[2], 1e-9))
        inf["escala"] = escalar_a_largo(obj, largo_m) if largo_m else escalar_a_altura(obj, altura_m)
        original.data.transform(Matrix.Scale(k, 4))
        llevar_origen_a_base(obj)
    llevar_origen_a_base(original)
    if alisar:
        inf["alisar"] = alisar_superficie(obj, iteraciones=alisar)
    if voxel:
        inf["remallar"] = remallar_voxel(obj)
    # si la silueta cae por debajo del 95 %, se reintenta con más triángulos sin pasar del máximo del presupuesto
    copia = obj.data.copy()
    intentos = []
    techo = int(presupuesto[1] * 0.96)  # el diezmado cae en ±4 %: así no se pasa del máximo
    for obj_tris in sorted({min(objetivo, techo), int(min(objetivo * 1.3, techo)), techo}):
        if intentos:
            vieja = obj.data
            obj.data = copia.copy()
            bpy.data.meshes.remove(vieja)
        _CAJAS.clear()
        d = diezmar(obj, objetivo=obj_tris)
        intentos.append(d)
        if d["silueta_ok"]:
            break
    bpy.data.meshes.remove(copia)
    inf["diezmar"] = intentos[-1]
    inf["diezmar_intentos"] = [(d["triangulos"], d["silueta_1px_min"], d["silueta_iou_min"]) for d in intentos]
    if color_unico:
        inf["paleta"] = paleta_uniforme(obj, color_unico, ruta_textura=os.path.join(salida, "T_Paleta32.png"))
    else:
        inf["paleta"] = paleta_desde_original(obj, original=original, max_colores=max_colores,
                                              ruta_textura=os.path.join(salida, "T_Paleta32.png"))
    if partido:
        # tipo: "militar" (vehículos y material propio) o "civil"; por defecto, militar si la facción es propia
        tipo = tipo or ("militar" if faccion == "propio" else "civil")
        inf["valor_partido"] = valor_partido(obj, claro=claro, costado=costado or COSTADO_POR_TIPO[tipo])
        inf["valor_partido"]["tipo"] = tipo
    inf["faccion"] = marcar_faccion(obj, faccion)
    inf["auditoria"] = auditar(obj, presupuesto=presupuesto, altura_m=altura_m)
    leg = medir_legibilidad(obj, suelos)
    inf["legibilidad"] = leg
    inf["auditoria"]["puntos"].append({"nivel": "OK" if leg.get("ok") else "ERROR", "clave": "legibilidad_D-N57",
                                       "valor": {k: v["dL"] for k, v in leg.get("suelos", {}).items()},
                                       "nota": "ΔL* ≥ 25 contra cada suelo (albedo sin luz)"})
    if not leg.get("ok"):
        inf["auditoria"]["veredicto"] = "ERROR"
    inf["vistas"] = [vista_pixelada(obj, os.path.join(salida, "%s_%s.png" % (nombre, v)), vista=v)["png"]
                     for v in ("rts", "frente", "izquierda")]
    inf["vista_original"] = vista_pixelada(original, os.path.join(salida, "%s_ORIGINAL_rts.png" % nombre),
                                           vista="rts", intensidad=0.65)["png"]
    inf["exportado"] = exportar_unity(obj, os.path.join(salida, nombre + (".fbx" if formato == "FBX" else ".glb")),
                                      formato)
    with open(os.path.join(salida, nombre + "_informe.json"), "w", encoding="utf-8") as f:
        json.dump(inf, f, ensure_ascii=False, indent=1, default=str)
    return inf


def _main(argv):
    import argparse
    ap = argparse.ArgumentParser(description="Hunyuan GLB -> pieza de juego (limpiar, diezmar, paleta, auditar)")
    ap.add_argument("entrada")
    ap.add_argument("--salida", required=True)
    ap.add_argument("--nombre")
    ap.add_argument("--objetivo", type=int, default=3000)
    ap.add_argument("--altura", type=float, help="altura real en metros")
    ap.add_argument("--largo", type=float, help="longitud real en metros (escala por la longitud; la altura se audita)")
    ap.add_argument("--sin-voxel", dest="voxel", action="store_false",
                    help="no remallar por vóxeles (conserva las UV originales; deja más aristas de borde)")
    ap.add_argument("--colores", type=int, default=10)
    ap.add_argument("--formato", default="FBX", choices=["FBX", "GLB"])
    ap.add_argument("--faccion", default="neutro", choices=list(FACCIONES))
    ap.add_argument("--sin-partido", dest="partido", action="store_false", help="no aplicar el valor partido D-N57")
    ap.add_argument("--claro", default="b4a080", help="color de las caras de arriba (por defecto b4a080, Lucía)")
    ap.add_argument("--costado", help="color fijo de los costados (por defecto según --tipo)")
    ap.add_argument("--tipo", choices=list(COSTADO_POR_TIPO), help="militar (costado 50541c) o civil (605c48)")
    a = ap.parse_args(argv)
    inf = procesar(a.entrada, a.salida, a.nombre, a.objetivo, a.altura, a.voxel, a.colores, formato=a.formato,
                   faccion=a.faccion, partido=a.partido, claro=a.claro, costado=a.costado, tipo=a.tipo,
                   largo_m=a.largo)
    print(json.dumps({k: inf[k] for k in ("nombre", "triangulos_entrada", "diezmar", "paleta")}, ensure_ascii=False,
                     default=str))
    print("VEREDICTO", inf["auditoria"]["veredicto"])


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    if args:
        _main(args)
