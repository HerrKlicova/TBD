"""Hoja de la horda de noche: N zombis de zombi_fino con las variantes y el tinte por instancia de Bruno (D-N80,
horda_paleta.json), sobre suelo de noche (L* ~25, D-N69). Render propio en numpy a tamaño de juego (~20 px por zombi).
Uso: bvenv/python horda_noche.py salida.png "lista de variantes" [semilla]
"""
import sys, os, json, math, importlib, random
import numpy as np
sys.path.insert(0, "/home/user/tbd/.claude/skills/modelado-3d/scripts/bloques")
sys.path.append("/usr/local/lib/python3.13/site-packages")
import bpy
from PIL import Image, ImageDraw

sal, lista = sys.argv[1], [int(x) for x in sys.argv[2].split()]
sem = int(sys.argv[3]) if len(sys.argv) > 3 else 7
foco = len(sys.argv) > 4 and sys.argv[4] == "foco"
pal = json.load(open("/mnt/project-files/docs/arte/pixel-codigo/noche-prototipo/horda/horda_paleta.json"))
def hx(h): h = h.lstrip("#"); return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], float)
def elegir(r, l): return hx(r.choices([c["hex"] for c in l], [c["peso"] for c in l])[0])

# mallas LOD1 de cada variante: triángulos con el canal de tinte (0 camisa, 1 pantalón, 2 piel, 3 calzado)
mallas = {}
for v in sorted(set(lista)):
    os.environ["TBD_VARIANTE"] = str(v)
    import zombi_cercano, zombi_fino
    importlib.reload(zombi_fino)
    o = zombi_fino.construir("z%d" % v, lod=1)
    me = o.data
    me.calc_loop_triangles()
    vs = np.array([tuple(x.co) for x in me.vertices])
    tris, can = [], []
    nombres = [m.name.split(".")[0] for m in me.materials]
    for lt in me.loop_triangles:
        p = nombres[lt.material_index]
        m = zombi_fino.MASCARA_COLOR.get(p, (0, 0, 0))
        tris.append(tuple(lt.vertices))
        can.append(0 if m[0] else 1 if m[1] else 2 if m[2] else 3)
    mallas[v] = (vs, np.array(tris), np.array(can))

r = random.Random(sem)
W, H, PXM = 300, 170, 11.0          # ~11 px/m: un zombi de 1,75 m ronda los 19-20 px
az, el = math.radians(35), math.radians(40)
Rz = np.array([[math.cos(az), -math.sin(az), 0], [math.sin(az), math.cos(az), 0], [0, 0, 1]])
Rx = np.array([[1, 0, 0], [0, math.sin(el), math.cos(el)], [0, -math.cos(el), math.sin(el)]])
R = Rx @ Rz
luz = np.array([-0.45, 0.35, 0.82]); luz /= np.linalg.norm(luz)
img = Image.new("RGB", (W, H))
d = ImageDraw.Draw(img)
# suelo de noche: tres valores alrededor de L* 25 en manchas (tierra compactada de noche)
suelo = [hx("3c3c38"), hx("44423c"), hx("36363a")]
gi = np.zeros((H, W, 3))
rs = np.random.RandomState(sem)
ruido = rs.rand(H // 6 + 1, W // 6 + 1)
for y in range(H):
    for x in range(W):
        k = ruido[y // 6, x // 6]
        gi[y, x] = suelo[0 if k < 0.6 else 1 if k < 0.85 else 2]
if foco:   # luz del foco del muro: sube el valor, aquí se ve el matiz
    yy, xx = np.mgrid[0:H, 0:W]
    f = 1 + 1.4 * np.exp(-(((xx - W / 2) / 90) ** 2 + ((yy - H / 2) / 60) ** 2))
    gi *= f[..., None]
img = Image.fromarray(np.clip(gi, 0, 255).astype(np.uint8))
d = ImageDraw.Draw(img)
polys = []
N = 70
for i in range(N):
    v = r.choice(lista)
    vs, tris, can = mallas[v]
    s = 1 + r.uniform(-0.05, 0.05)
    yaw = math.radians(r.uniform(-35, 35))       # todos van más o menos hacia −Y (hacia el muro)
    ca, sa = math.cos(yaw), math.sin(yaw)
    Y = np.array([[ca, -sa, 0], [sa, ca, 0], [0, 0, 1]])
    pos = np.array([r.uniform(-12, 12), r.uniform(-6, 7) + (0 if r.random() < 0.6 else -3), 0])
    w = (vs * s) @ Y.T + pos
    cols = [elegir(r, pal["camisas"]), elegir(r, pal["pantalones"]), elegir(r, pal["piel"]), hx("1c200c")]
    c = w @ R.T
    for t, k in zip(tris, can):
        a, b, e = w[t[0]], w[t[1]], w[t[2]]
        n = np.cross(b - a, e - a); n /= np.linalg.norm(n) + 1e-9
        if (R @ n)[2] < 0:
            continue
        lam = max(0.0, float(n @ luz))
        base = cols[k] * (0.88 + 0.24 * lam)
        if foco:
            fx = c[t, 0].mean() * PXM + W / 2; fy = -c[t, 1].mean() * PXM + H / 2 + 20
            base = base * (1 + 1.4 * math.exp(-(((fx - W / 2) / 90) ** 2 + ((fy - H / 2) / 60) ** 2)))
        pts = [(float(c[j, 0] * PXM + W / 2), float(-c[j, 1] * PXM + H / 2 + 20)) for j in t]
        polys.append((c[t, 2].mean(), pts, tuple(int(x) for x in np.clip(base, 0, 255))))
for z, pts, col in sorted(polys, key=lambda q: q[0]):
    d.polygon(pts, fill=col)
img.save(sal.replace(".png", "_x1.png"))
img.resize((W * 3, H * 3), Image.NEAREST).save(sal)
print("ok", sal)
