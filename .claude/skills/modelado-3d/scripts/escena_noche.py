"""Escena de conjunto de Sagunto de noche con los modelos aprobados (crónica de Elena y galería de las 08:00). Teo, 7-nov.

Coloca los FBX entregados según mapa_sagunto_carreteras.json de Bruno (X del mapa → X de Blender, Z del mapa → Y) y
renderiza con el rasterizador de malla3d: luna (0,14), luces de escena con caída (focos, balizas, faros) iluminadas por
píxel y las piezas de emisión («Emision», «Luces*») encendidas. NO es la build: el suelo, el muro de sacos y la vía son
cajas de relleno, y la posición del tren (parado al oeste del paso a nivel) y de las piezas es de composición.
Uso: python escena_noche.py -- carpeta_salida [encuadre …]
"""
import sys, os, math
import numpy as np
import bpy
from mathutils import Matrix, Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import malla3d as M

A = "/mnt/project-files/docs/arte/modelado-3d/"
args = sys.argv[sys.argv.index("--") + 1:]
SAL = args[0]
SOLO = set(args[1:])
os.makedirs(SAL, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

TRIS = []          # (V (n,3,3) en mundo, color (n,3), emisivo (n,))
# CRONICA=1 (coordinador, 7-nov 01:42): renders para la crónica de Elena, con la puerta E4 de HESCO (v1) y los focos
# del paso a nivel ENCENDIDOS en 982420 (el rojo de alerta, que solo es luz, nunca albedo) como si llegara un tren
CRONICA = os.environ.get("CRONICA") == "1"
ROJO = None


def _hex(h):
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], float)


def poner(fbx, x, y, rot=0.0, hijos=None, z=0.0):
    """Importa un FBX, gira los hijos sobre su pivote (hijos = {nombre: grados en Z}), lo gira rot° y lo lleva a (x, y)."""
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=A + fbx)
    bpy.context.view_layer.update()
    obs = [o for o in bpy.data.objects if o not in antes and o.type == "MESH"]
    G = Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(rot), 4, "Z")
    for o in obs:
        if o.name.startswith("DiscoRotor"):          # el disco de las palas en giro taparía el NH90 parado
            bpy.data.objects.remove(o)
            continue
        mw = o.matrix_world.copy()
        if hijos:
            for nom, g in hijos.items():
                if o.name.startswith(nom):
                    p = mw.translation.copy()
                    mw = Matrix.Translation(p) @ Matrix.Rotation(math.radians(g), 4, "Z") @ Matrix.Translation(-p) @ mw
        me = o.data
        me.calc_loop_triangles()
        col = M.colores_por_cara(o)
        v = np.array([(G @ mw @ vv.co)[:] for vv in me.vertices])
        t = np.array([lt.vertices[:] for lt in me.loop_triangles])
        if len(t) == 0:
            continue
        # encendidas: la capa de emisión del tren y las «Luces» del helipuerto y de las balizas; los focos del paso a nivel
        # (Luces_A/B) solo parpadean con un tren cruzando, así que van apagados
        emi = o.name.startswith("Emision") or o.name.split(".")[0] == "Luces"
        cod = 1 if emi else 0
        if CRONICA and o.name.split(".")[0] in ("Luces_A", "Luces_B"):
            cod = 2                                      # foco del paso a nivel encendido en 982420
        TRIS.append((v[t], col, np.full(len(t), cod)))
        bpy.data.objects.remove(o)


def caja(c, s, hexcol, rot=0.0):
    x, y, z = c
    sx, sy, sz = s[0] / 2, s[1] / 2, s[2] / 2
    P = np.array([[-sx, -sy, -sz], [sx, -sy, -sz], [sx, sy, -sz], [-sx, sy, -sz],
                  [-sx, -sy, sz], [sx, -sy, sz], [sx, sy, sz], [-sx, sy, sz]])
    cr, sr = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    P = P @ np.array([[cr, sr, 0], [-sr, cr, 0], [0, 0, 1]]) + np.array([x, y, z])
    F = [(0, 2, 1), (0, 3, 2), (4, 5, 6), (4, 6, 7), (0, 1, 5), (0, 5, 4), (1, 2, 6), (1, 6, 5), (2, 3, 7), (2, 7, 6),
         (3, 0, 4), (3, 4, 7)]
    TRIS.append((P[np.array(F)], np.tile(_hex(hexcol), (12, 1)), np.zeros(12, bool)))


def suelo(x0, y0, x1, y1, hexcol, z=0.0):
    P = np.array([[x0, y0, z], [x1, y0, z], [x1, y1, z], [x0, y1, z]])
    TRIS.append((P[np.array([(0, 1, 2), (0, 2, 3)])], np.tile(_hex(hexcol), (2, 1)), np.zeros(2, bool)))


# ------------------------------------------------------------------ suelo, carreteras, vía y recinto
suelo(-60, -10, 240, 130, "845c28", -0.02)                       # llano ocre
suelo(-60, 19.5, 240, 26.5, "6c7874", 0.0)                       # carretera vieja (habilitada), z = 23
suelo(55.5, 26.5, 62.5, 130, "6c7874", 0.0)                      # calle de la Estación, x = 59
suelo(-23.5, 26.5, -16.5, 130, "60546c", 0.0)                    # paso superior (cortado), más oscuro
suelo(-60, 61.5, 240, 67.5, "48403c", 0.01)                      # balasto
for dy in (-0.72, 0.72):                                         # carriles
    caja((90, 64.5 + dy, 0.12), (300, 0.1, 0.18), "94948c")
suelo(68, 43, 157, 87, "b4a080", 0.005)                          # explanada del recinto
suelo(94, 49, 140, 60, "6c7874", 0.012)                          # parque de vehículos
suelo(80, 66.5, 145, 71.5, "94948c", 0.3)                        # andén
# muro de sacos (1,2 m, 1,0 m de grueso) con la puerta E4 en X 95–103 y huecos de vía en X 68 y 157
def muro(x0, y0, x1, y1, hexcol="88885c", alto=1.2):
    caja(((x0 + x1) / 2, (y0 + y1) / 2, alto / 2), (abs(x1 - x0) or 1.0, abs(y1 - y0) or 1.0, alto), hexcol)
muro(68, 43, 94.1, 43); muro(103.9, 43, 157, 43)                 # sur, con la puerta
muro(68, 87, 157, 87)                                            # norte
muro(157, 43, 157, 62.4); muro(157, 66.6, 157, 87)               # este, con la puerta de vía cerrada
caja((157, 64.5, 1.05), (0.4, 4.2, 2.1), "605c48")
for (y0, y1) in ((43, 62.4), (66.6, 87)):                        # frente oeste en HESCO (2,1 m)
    caja((68, (y0 + y1) / 2, 1.05), (1.2, y1 - y0, 2.1), "70784c")
# ------------------------------------------------------------------ modelos
poner("sagunto/edificio_viajeros/SM_edificio_viajeros_LOD0.fbx", 112, 79)
poner("sagunto/marquesina/SM_marquesina_LOD0.fbx", 112, 69.6, z=0.3)
# tren de noche parado al oeste del paso a nivel (locomotora, tropa y contenedor), con la vía a lo largo de X
poner("tren_cercano/locomotora/SM_Locomotora_Cercana_LOD0.fbx", 40.0, 64.5, 90)
poner("tren_cercano/vagon_v2/SM_Vagon_Cercano_v2_LOD0.fbx", 17.4, 64.5, 90)
poner("tren_cercano/vagon_v3/SM_Vagon_Cercano_v3_LOD0.fbx", -3.2, 64.5, 90)
poner("paso_nivel/SM_PasoNivel_v1_LOD0.fbx", 59, 64.5, 90)
poner("helipuerto/SM_Helipuerto_v0_LOD0.fbx", 180, 44)
poner("nh90/SM_NH90_LOD0.fbx", 179, 44, 25, hijos={"RotorPrincipal": 20, "DiscoRotor": 20})
for fbx, x, y, r in (("rearme_set/SM_RearmeSet_v0_LOD0.fbx", 192, 34, 0), ("rearme_set/SM_RearmeSet_v1_LOD0.fbx", 192, 38, 10),
                     ("rearme_set/SM_RearmeSet_v2_LOD0.fbx", 168, 32, -20), ("rearme_set/SM_RearmeSet_v3_LOD0.fbx", 195, 52, 90)):
    poner(fbx, x, y, r)
poner("puerta_vehiculos/SM_PuertaVehiculos_v%d_LOD0.fbx" % (1 if CRONICA else 4), 99, 43, 0,
      hijos={"Hoja_O": -75, "Hoja_E": 75})
poner("dragon/v5/SM_VCR_Dragon_v5_LOD0.fbx", 99, 50.5, 0)                       # saliendo por la puerta
poner("dragon/v5/SM_VCR_Dragon_v5_LOD0.fbx", 110, 54, 90)
poner("v2_vamtac/SM_vamtac_v2_LOD0.fbx", 118, 54, 90)
poner("v2_vamtac/SM_vamtac_v2_LOD0.fbx", 124, 54, 90)
poner("v2_vamtac/SM_vamtac_v2_LOD0.fbx", 99, 34, 15)                             # fuera, por la calle
# cortes (carreteras no habilitadas) y balizas (habilitadas)
poner("corte_carretera/SM_CorteCarretera_v0_LOD0.fbx", -20, 30, 0)              # paso superior
poner("corte_carretera/SM_CorteCarretera_v1_LOD0.fbx", 222, 23, 90)             # carretera vieja, pasado el Palancia
poner("corte_carretera/SM_CorteCarretera_v4_LOD0.fbx", 214, 23, 90)
for k, x in enumerate(range(0, 210, 30)):
    poner("baliza_ruta/SM_BalizaRuta_v%d_LOD0.fbx" % (0 if k % 2 else 2), x + 6, 18.6, 180)
for y in (34, 90, 115):
    poner("baliza_ruta/SM_BalizaRuta_v1_LOD0.fbx", 63.5, y, 0)
poner("baliza_ruta/SM_BalizaRuta_v4_LOD0.fbx", 106, 38, 0)
poner("baliza_ruta/SM_BalizaRuta_v4_LOD0.fbx", 209, 19.0, 0)                   # luz del control de la carretera vieja
for k, (x, y) in enumerate(((211, 20.5), (212.2, 19.6), (210.5, 27.5))):
    poner("infanteria/SM_Soldado_v%d_LOD0.fbx" % (k + 2), x, y, 70 + 40 * k)
poner("baliza_ruta/SM_BalizaRuta_v3_LOD0.fbx", 92, 37, 0)
# soldados: en la puerta, en el andén y en el helipuerto
gr = [(96, 45.5), (97.2, 45.0), (101, 45.4), (102.3, 46.0), (95, 47), (103.5, 47.4),
      (118, 69.5), (119.2, 70.1), (121, 69.4), (122.3, 70.3), (124, 69.8), (126, 69.2),
      (172, 37), (173, 36.2), (186, 36), (187.4, 37)]
for k, (x, y) in enumerate(gr):
    poner("infanteria/SM_Soldado_v%d_LOD0.fbx" % (k % 6), x, y, (k * 47) % 360)
if CRONICA:                                        # puesto de frente v2 (HESCO) ocupado por un pelotón, al oeste
    poner("puesto_frente/SM_PuestoFrente_v2_LOD0.fbx", 28, 42, 0)
    pel = [(24.5, 44.5), (25.6, 45.2), (23.8, 46.4), (27.0, 38.4), (31.5, 39.0), (32.6, 38.2), (26.8, 47.6),
           (28.6, 44.0), (27.2, 34.7), (29.0, 34.6)]          # los dos últimos, de guardia en la entrada
    for k, (x, y) in enumerate(pel):
        poner("infanteria/SM_Soldado_v%d_LOD0.fbx" % (k % 6), x, y, 180 if k >= 8 else (k * 53) % 360)
    poner("v2_vamtac/SM_vamtac_v2_LOD0.fbx", 36.5, 31.0, 20)
# pueblo y huerta (contexto)
for x, y in ((30, 92), (44, 95), (140, 100), (165, 96)):
    poner("sagunto/casa_baja/SM_casa_baja_LOD0.fbx", x, y, (x * 7) % 30)
for x in range(-10, 230, 9):
    for y in (6, 13):
        poner("sagunto/naranjo/SM_naranjo_LOD0.fbx", x + (y % 2) * 4, y, (x * 13) % 360)

V = np.concatenate([t[0] for t in TRIS])                    # (n,3,3)
C = np.concatenate([t[1] for t in TRIS])
E = np.concatenate([t[2] for t in TRIS])
N = np.cross(V[:, 1] - V[:, 0], V[:, 2] - V[:, 0])
N /= np.linalg.norm(N, axis=1, keepdims=True) + 1e-12
print("triangulos", len(V))

# ------------------------------------------------------------------ luces de escena (posición, color, radio, intensidad)
CREMA, FRIO = _hex("f0dc9c"), _hex("b8b8b8")
LUCES = [((68 + 2.5, 64.5 + 15, 6), CREMA, 30, 1.0), ((68 + 2.5, 64.5 - 15, 6), CREMA, 30, 1.0),
         ((112, 85, 6), CREMA, 28, 0.9),                                    # focos del recinto
         ((99, 41.5, 4.5), CREMA, 16, 1.1),                                 # foco de la puerta E4
         ((100, 69.5, 4.2), CREMA, 12, 0.7), ((112, 69.5, 4.2), CREMA, 12, 0.7), ((124, 69.5, 4.2), CREMA, 12, 0.7),
         ((112, 74.5, 3.0), CREMA, 10, 0.8),                                # puerta de la estación
         ((52.5, 64.5, 2.6), CREMA, 22, 1.1),                               # faros de la locomotora (hacia el paso)
         ((59, 64.5, 3.0), CREMA, 9, 0.3 if CRONICA else 0.5),              # focos del paso a nivel
         ((180, 44, 6), FRIO, 22, 0.7), ((171, 35, 1.2), CREMA, 8, 0.6), ((189, 35, 1.2), CREMA, 8, 0.6),
         ((171, 53, 1.2), CREMA, 8, 0.6), ((189, 53, 1.2), CREMA, 8, 0.6),
         ((106, 38, 1.8), CREMA, 7, 0.8),                                   # baliza en trípode
         ((99, 36.5, 1.2), CREMA, 12, 0.8),
         ((209, 19.0, 1.8), CREMA, 12, 1.0), ((213, 23, 5.0), FRIO, 16, 0.6)]   # control de la carretera vieja
if CRONICA:                                                                 # foco del mástil del puesto y luz de la tienda
    LUCES.append(((24.2, 38.4, 5.9), CREMA, 15, 1.1))
    LUCES.append(((29.6, 41.2, 2.2), CREMA, 6, 0.6))
if CRONICA:                                                                 # faroles del vagón de tropa y del contenedor
    for x, z, r, it in ((11.4, 3.6, 9, 0.55), (23.4, 3.6, 9, 0.55), (-8.2, 4.2, 8, 0.45), (1.8, 4.2, 8, 0.45),
                        (30.0, 4.5, 10, 0.4)):
        for y in (64.5 - 2.2, 64.5 + 2.2):
            LUCES.append(((x, y, z), CREMA, r, it))
if CRONICA:                                                                 # resplandor rojo de los focos encendidos
    for y in (64.5 - 4.5, 64.5 + 4.5):
        LUCES.append(((59, y, 2.4), _hex("982420"), 7, 1.2))
for x in range(0, 210, 30):
    LUCES.append(((x + 6, 18.6, 1.0), CREMA, 3.5, 0.5))                     # catadióptricos de las balizas
LP = np.array([l[0] for l in LUCES], float)
LC = np.array([l[1] for l in LUCES], float) / 255.0
LR = np.array([l[2] for l in LUCES], float)
LI = np.array([l[3] for l in LUCES], float)

PAL = M.cargar_paleta()
OSCURO = PAL[np.argmin(PAL.sum(1))]
LUNA = np.array([-0.45, 0.35, 0.82]); LUNA /= np.linalg.norm(LUNA)

ENCUADRES = {
    # nombre: (centro x, y, z), azimut, elevación, ancho en metros, ancho px, alto px
    "general": ((100, 58, 0), -20, 38, 185, 1600, 900),
    "tren": ((40, 62, 1.5), -35, 24, 62, 1600, 900),
    "helipuerto": ((182, 42, 1), 20, 40, 46, 1600, 900),
    "muro": ((101, 46, 1), -15, 32, 40, 1600, 900),
    "estacion": ((112, 70, 2), 15, 30, 56, 1600, 900),
    "cortes": ((215, 24, 0.5), -30, 34, 30, 1600, 900),
    # crónica de Elena (CRONICA=1)
    "cronica_tren": ((20, 63.5, 1.8), -30, 22, 60, 1600, 900),
    "cronica_puerta": ((99, 44, 1.2), -12, 30, 30, 1600, 900),
    "cronica_paso_nivel": ((57, 63, 1.2), -40, 26, 28, 1600, 900),
    "cronica_helipuerto": ((181, 43, 1), 20, 40, 44, 1600, 900),
    "cronica_puesto": ((28, 40.5, 1), -25, 34, 30, 1600, 900),
}


def render(nombre, centro, az, el, ancho_m, W, H):
    R = M._matriz_vista(az, el)
    res = W
    c = (np.array(centro) @ R.T)[:2]
    flat = V.reshape(-1, 3)
    xy, z, _ = M._proyectar(flat, R, res, caja=(c, ancho_m))
    tris = np.arange(len(flat)).reshape(-1, 3)
    # recorte: solo triángulos que caen cerca del cuadro (el rasterizador es cuadrado)
    txy = xy[tris]
    y0 = res / 2 - H / 2
    dentro = (txy[:, :, 0].max(1) > -50) & (txy[:, :, 0].min(1) < res + 50) & \
             (txy[:, :, 1].max(1) > y0 - 50) & (txy[:, :, 1].min(1) < y0 + H + 50)
    ks = np.nonzero(dentro)[0]
    zb, ib = M._rasterizar(xy, z, tris[ks], res)
    ib = np.where(ib >= 0, ks[np.maximum(ib, 0)], -1)
    a = int(y0)
    zb, ib = zb[a:a + H], ib[a:a + H]
    m = ib >= 0
    # posición en el mundo de cada píxel (cámara → mundo)
    s = ancho_m / res
    yy, xx = np.nonzero(m)
    pc = np.stack([(xx + 0.5 - res / 2) * s + c[0], (res / 2 - (yy + a + 0.5)) * s + c[1], zb[yy, xx]], 1)
    pw = pc @ R
    k = ib[yy, xx]
    n = N[k]
    n = np.where((n @ R[2])[:, None] < 0, -n, n)               # caras de dos lados: la normal hacia la cámara
    base = C[k] / 255.0
    luz = (0.14 * np.clip(n @ LUNA, 0, 1) + 0.08)[:, None] * np.ones(3)
    for p, lc, r, it in zip(LP, LC, LR, LI):
        d = p - pw
        dist = np.linalg.norm(d, axis=1) + 1e-6
        caida = np.clip(1 - dist / r, 0, 1) ** 2
        lam = 0.35 + 0.65 * np.clip((n * d).sum(1) / dist, 0, 1)
        luz += (it * caida * lam)[:, None] * lc
    col = base * luz * 255.0 * 1.25
    em = E[k]
    col[em == 1] = CREMA                                     # emisión en crema f0dc9c (Lucía)
    col[em == 2] = _hex("982420") * 1.35                     # focos del paso a nivel encendidos (luz, no albedo)
    col = np.clip(col, 0, 255)
    img = np.zeros((H, W, 3))
    img[:] = (16, 12, 8)
    idx = M.indice_paleta(col, PAL)
    img[yy, xx] = col * 0.7 + PAL[idx] * 0.3          # poca cuantización: con más, la caída de las luces salía en anillos
    # contorno por salto de profundidad o de objeto
    zz = np.where(m, zb, -1e9)
    rango = 0.02 * ancho_m
    borde = np.zeros_like(m)
    for dy, dx in ((0, 1), (1, 0), (0, -1), (-1, 0)):
        vec = np.roll(np.roll(m, dy, 0), dx, 1); vz = np.roll(np.roll(zz, dy, 0), dx, 1)
        borde |= m & (~vec | (vz - zz > rango))
    img[borde] = img[borde] * 0.25 + OSCURO * 0.75
    rgba = np.concatenate([img, np.full((H, W, 1), 255.0)], 2)
    ruta = os.path.join(SAL, "sagunto_noche_%s.png" % nombre)
    M._png(ruta, np.clip(rgba, 0, 255))
    print("ok", ruta)


for nom, e in ENCUADRES.items():
    if not SOLO or nom in SOLO:
        render(nom, *e)
