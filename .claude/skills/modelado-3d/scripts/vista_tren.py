"""Aproximación de la vista del tren de Marcos: lateral (yaw 90°), pitch 32°, ortho 7 (640×360 internos, ≈0,039 m/px),
de día y de noche (luna 0,14, ambiente negro, ventanillas emisivas). La buena es la captura de Unity.
Uso: python vista_tren.py -- salida.png fbx1 fbx2 ..."""
import sys, os, math
import numpy as np
import bpy
sys.path.insert(0, "/home/claude/tbd/.claude/skills/modelado-3d/scripts")
import malla3d as M

args = sys.argv[sys.argv.index("--") + 1:]
salida, fbxs = args[0], args[1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
y0 = 0.0
piezas = []   # (obj, emisiva)
for k, f in enumerate(fbxs):
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=f)
    nuevos = [o for o in bpy.data.objects if o not in antes and o.type == "MESH"]
    bpy.context.view_layer.update()
    for o in nuevos:
        mw = o.matrix_world.copy()
        o.parent = None
        o.data.transform(mw)
        from mathutils import Matrix
        o.matrix_world = Matrix.Identity(4)
    vs = np.concatenate([np.array([v.co[:] for v in o.data.vertices]) for o in nuevos])
    largo = vs[:, 1].max() - vs[:, 1].min()
    dy = y0 - vs[:, 1].min()
    for o in nuevos:
        for v in o.data.vertices:
            v.co.y += dy
        piezas.append((o, o.name.startswith("Ventanillas")))
    y0 += largo + 0.6

V, T, B, N, E = [], [], [], [], []
off = 0
for o, emi in piezas:
    me = o.data
    me.calc_loop_triangles()
    v = np.array([x.co[:] for x in me.vertices])
    t = np.array([lt.vertices[:] for lt in me.loop_triangles])
    n = np.array([np.mean([tuple(x) for x in lt.split_normals], axis=0) for lt in me.loop_triangles])
    V.append(v); T.append(t + off); N.append(n); off += len(v)
    B.append(M.colores_por_cara(o)); E.append(np.full(len(t), emi))
V, T, B, N, E = map(np.concatenate, (V, T, B, N, E))
N /= np.linalg.norm(N, axis=1, keepdims=True) + 1e-12

ANCHO, ALTO, MPX = 640, 360, 14.0 / 360
R = M._matriz_vista(90, 32)      # desde +X (costado izquierdo del tren), mirando de lado
pal = M.cargar_paleta()
fondo = np.array([0x84, 0x5c, 0x28], float)   # llano ocre (D-N61)
p = V @ R.T
cx = (p[:, 0].min() + p[:, 0].max()) / 2
cy = (p[:, 1].min() + p[:, 1].max()) / 2
res = int(math.ceil((p[:, 0].max() - p[:, 0].min()) / MPX)) + 40
xy, z, _ = M._proyectar(V, R, res, caja=(np.array([cx, cy]), res * MPX))
zb, ib = M._rasterizar(xy, z, T, res)
luz = np.array([-0.45, 0.35, 0.82]); luz /= np.linalg.norm(luz)
# la misma luz relativa a la cámara que en las vistas de siempre (arriba a la izquierda de la vista rts)
luz = R.T @ (M._matriz_vista(*M.VISTAS["rts"]) @ luz)
lam = np.clip(N @ luz, 0, 1)
oscuro = pal[np.argmin(pal.sum(1))]


def pinta(tono, emisivo_col, fondo_col):
    img = np.zeros((res, res, 3)); img[:] = fondo_col
    m = ib >= 0
    col = B[ib[m]] * tono[ib[m], None]
    if emisivo_col is not None:
        em = E[ib[m]]
        col[em] = emisivo_col
    idx = M.indice_paleta(col, pal)
    img[m] = col * 0.35 + pal[idx] * 0.65
    zz = np.where(m, zb, -1e9); rango = float(np.ptp(z)) or 1.0
    borde = np.zeros_like(m)
    for dy, dx in ((0, 1), (1, 0), (0, -1), (-1, 0)):
        vec = np.roll(np.roll(m, dy, 0), dx, 1); vz = np.roll(np.roll(zz, dy, 0), dx, 1)
        borde |= m & (~vec | (np.abs(vz - zz) > 0.06 * rango))
    img[borde] = oscuro
    # recorte a 360 px de alto alrededor del tren
    filas = np.nonzero(m.any(1))[0]
    c = (filas.min() + filas.max()) // 2
    a = max(0, c - ALTO // 2)
    return img[a:a + ALTO]


dia = pinta(0.45 + 0.55 * lam, None, fondo)
noche = pinta(0.14 * lam, np.array([0xf0, 0xdc, 0x9c], float), fondo * 0.14)
img = np.concatenate([dia, np.full((6, res, 3), 24.0), noche], 0)
img = np.repeat(np.repeat(img, 2, 0), 2, 1)
rgba = np.concatenate([img, np.full(img.shape[:2] + (1,), 255.0)], 2)
M._png(salida, np.clip(rgba, 0, 255))
print("ok", res, img.shape)
