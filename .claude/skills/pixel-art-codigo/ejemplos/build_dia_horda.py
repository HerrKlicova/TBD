"""D-N157 · La primera horda ataca de día: 5 muestras del aspecto de día de la zona de Sagunto. Bruno (Arte técnico), 9-nov.

Encargo de Lucía: luz, cielo y cómo se ven las 5 siluetas de Teo con el shader TBD/Horda de día, con la horda atacando el muro.
Regla que no se rompe: de día la horda se lee igual que de noche, por forma y por valor, y nunca se acerca al hueso d4d0cc de la interfaz.
Se propone la banda de día de la horda (el equivalente a L* 9–13 de albedo de noche), se mide en cada muestra y se comprueba que el tren,
las vías D y el muro siguen separados con el ΔL* ≥ 8 de Sara. Sin rojo ni ámbar fuera de su uso.

Cómo: render por código (numpy), sin Unity. Cámara del juego: ortográfica, 55° de inclinación, 1 px = 0,144 m (mundo a 1/3).
Luz como el ForwardLit de TBD/Horda: Lambert del sol con sombra + ambiente de cielo y suelo (hemisferio, que es lo que da SampleSH),
sin especular. Mismo modelo para todo el decorado (URP Lit sin especular se le parece en mate).
Geometría:
  - zombis: OBJ de Teo (/mnt/project-files/entregas/teo-horda-obj/, poses base, andar1, andar2 y trepar), 5 siluetas.
    Los OBJ no traen la máscara de color de vértice: se pinta por altura (calzado < 8 %, pantalón < 50 %, camisa < 83 %, piel arriba),
    con los tonos de horda_paleta (build_horda_color.py) y la regla de pesos. APROXIMADO: la máscara real de Teo separa los brazos (camisa).
  - muro de obra: 2,5 m, hormigón (caras #6c7874, coronación #94948c). SIN VERIFICAR contra el material de la build.
  - vía D: balasto #605c48, traviesas #6c7874, carriles #b8b8b8 (T_via_D, claude/vias-zona).
  - tren: laterales #50541c, techo #b8b8b8 (Teo, kit del tren), franja ámbar en el frontal (su uso: dirección del tren).
  - suelo: apisonada #b4a080 en el recinto y seca (beige con motas #88885c) fuera (D-N67).
  - 3 soldados en la coronación (#70784c), para ver la horda contra lo propio.
Salidas (carpeta de salida, por defecto out_dia_horda): dia_<V>.png (x1), hoja_dia_horda.png (las 5 a x2 con sus medidas) y medidas.json.
Uso: python3 -I build_dia_horda.py [salida]
"""
import json
import math
import random
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_dia_horda")
OBJ = Path("/mnt/project-files/entregas/teo-horda-obj")
MURO_CARA = __import__("os").environ.get("TBD_MURO", "#6c7874")
CARGA = __import__("os").environ.get("TBD_CARGA", "#847c34")      # carga visible (Teo, mr-mak 84e1194): cajas de munición sobre el vagón v2
TREN_LADO = __import__("os").environ.get("TBD_TREN", "#70784c")    # Teo, D-N157 (mr-mak d54dd0f): de #50541c a #70784c; TBD_TREN=#50541c da el de antes    # TBD_MURO=#94948c prueba el muro claro (propuesta para vía↔muro)
PX = 0.144                       # m por píxel (ortho 26 a 1080p, mundo a 1/3)
W, H = 400, 240                  # 57,6 m × 34,6 m de pantalla
PITCH = math.radians(55)

# ---------------------------------------------------------------- color
def hexrgb(h):
    h = h.lstrip("#"); return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], float) / 255


def s2l(c):
    c = np.asarray(c, float); return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def l2s(c):
    c = np.clip(np.asarray(c, float), 0, 1); return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def lstar_lin(lin):
    y = lin[..., 0] * 0.2126 + lin[..., 1] * 0.7152 + lin[..., 2] * 0.0722
    return np.where(y > 0.008856, 116 * np.cbrt(y) - 16, 903.3 * y)


def lstar_srgb(c):
    return lstar_lin(s2l(c))


CAMISAS = [("#1c1c1e", 3), ("#202022", 3), ("#1b212f", 2), ("#262018", 2), ("#262014", 1), ("#1d2127", 2), ("#241e2a", 1), ("#23201d", 2)]
PANTALONES = [("#1a202e", 4), ("#1a1a1c", 3), ("#212121", 2), ("#262016", 2), ("#181c2a", 1), ("#242018", 1)]
PIEL = [("#21211f", 2), ("#25201d", 2), ("#231f27", 1), ("#232019", 2)]
CALZADO = "#1c200c"


def pesado(rng, l):
    t = sum(p for _, p in l); r = rng.uniform(0, t)
    for h, p in l:
        r -= p
        if r <= 0: return h
    return l[-1][0]


# ---------------------------------------------------------------- geometría: listas de triángulos (v0, v1, v2) con albedo lineal y clase
class Malla:
    def __init__(self):
        self.t = []; self.alb = []; self.cls = []

    def tri(self, a, b, c, alb, cls):
        self.t.append((a, b, c)); self.alb.append(alb); self.cls.append(cls)

    def quad(self, a, b, c, d, alb, cls):
        self.tri(a, b, c, alb, cls); self.tri(a, c, d, alb, cls)

    def caja(self, x0, y0, z0, x1, y1, z1, alb, cls, alb_top=None):
        p = lambda x, y, z: np.array([x, y, z], float)
        A, B, C, D = p(x0, y0, z0), p(x1, y0, z0), p(x1, y0, z1), p(x0, y0, z1)
        E, F, G, Hh = p(x0, y1, z0), p(x1, y1, z0), p(x1, y1, z1), p(x0, y1, z1)
        self.quad(E, Hh, G, F, alb_top if alb_top is not None else alb, cls)   # arriba
        self.quad(A, E, F, B, alb, cls)    # -z
        self.quad(B, F, G, C, alb, cls)    # +x
        self.quad(C, G, Hh, D, alb, cls)   # +z
        self.quad(D, Hh, E, A, alb, cls)   # -x


def leer_obj(path):
    vs, fs = [], []
    for ln in path.read_text().splitlines():
        p = ln.split()
        if not p: continue
        if p[0] == "v": vs.append([float(x) for x in p[1:4]])
        elif p[0] == "f":
            idx = [int(t.split("/")[0]) - 1 for t in p[1:]]
            for k in range(1, len(idx) - 1): fs.append((idx[0], idx[k], idx[k + 1]))
    return np.array(vs), fs


SILUETAS = ["civil", "huerta", "uniformado", "gordo", "rapido"]


def zombi(m, objs, sil, pose, pos, yaw, rng):
    vs, fs = objs[(sil, pose)]
    cam, pan, piel = (s2l(hexrgb(pesado(rng, l))) for l in (CAMISAS, PANTALONES, PIEL))
    zap = s2l(hexrgb(CALZADO))
    c, s = math.cos(yaw), math.sin(yaw)
    R = np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    w = vs @ R.T + pos
    hmax = max(vs[:, 1].max(), 1e-3)
    for a, b, d in fs:
        hy = (vs[a, 1] + vs[b, 1] + vs[d, 1]) / 3 / hmax
        alb = zap if hy < 0.08 else pan if hy < 0.50 else cam if hy < 0.83 else piel
        m.tri(w[a], w[b], w[d], alb, "horda")


def escena(seed=157):
    rng = random.Random(seed)
    objs = {}
    for s in SILUETAS:
        for pose in ("base", "andar1", "andar2", "trepar"):
            f = OBJ / (f"{s}.obj" if pose == "base" else f"{s}__{pose}.obj")
            objs[(s, pose)] = leer_obj(f)
    m = Malla()
    L = lambda h: s2l(hexrgb(h))
    # suelo: dentro (z<0) apisonada, fuera seca; el albedo fino sale por píxel (clase suelo_in / suelo_out)
    m.quad(np.array([-40, 0, -40.]), np.array([-40, 0, 0.]), np.array([40, 0, 0.]), np.array([40, 0, -40.]), L("#b4a080"), "suelo_out")
    m.quad(np.array([-40, 0, 0.]), np.array([-40, 0, 40.]), np.array([40, 0, 40.]), np.array([40, 0, 0.]), L("#b4a080"), "suelo_in")
    # vía D a z = -9: balasto 3,2 m, traviesas cada 0,6 m, carriles
    zc = 9.0
    m.caja(-40, 0, zc - 1.6, 40, 0.18, zc + 1.6, L("#605c48"), "via")
    x = -40.0
    while x < 40:
        m.caja(x, 0.18, zc - 1.3, x + 0.24, 0.26, zc + 1.3, L("#6c7874"), "via"); x += 0.6
    for dz in (-0.72, 0.72):
        m.caja(-40, 0.26, zc + dz - 0.04, 40, 0.42, zc + dz + 0.04, L("#b8b8b8"), "carril")
    # tren: locomotora + 2 vagones, 2,9 m de ancho, 3,6 m de alto
    for x0, x1, loco in ((-26, -9, True), (-8.4, 8.6, False), (9.2, 26.2, False)):
        m.caja(x0, 0.6, zc - 1.45, x1, 3.6, zc + 1.45, L(TREN_LADO), "tren")                 # laterales
        m.caja(x0, 3.6, zc - 1.45, x1, 3.62, zc + 1.45, L("#b8b8b8"), "techo")               # techo claro
        if loco:
            m.caja(x0 - 0.02, 1.6, zc - 1.45, x0, 2.0, zc + 1.45, L("#e08c30"), "tren_franja")
    # carga visible sobre el vagón del centro (plataforma: cajas de 1,2 × 0,8 × 0,9 m en dos filas)
    for cx in np.arange(-7.6, 8.0, 1.6):
        for cz in (zc - 0.75, zc + 0.15):
            m.caja(cx, 3.62, cz, cx + 1.2, 4.5, cz + 0.6, L(CARGA), "carga")
    # muro de obra a z = 0 (cara exterior en z = 0,5), 2,5 m
    m.caja(-40, 0, -0.5, 40, 2.5, 0.0, L(MURO_CARA), "muro_cara")                    # cara exterior a z = −0,5, hacia la cámara y la horda
    m.caja(-40, 2.5, -0.5, 40, 2.52, 0.0, L("#94948c"), "muro_corona")
    # soldados en la coronación (pasarela interior a 1,6 m: van por detrás del muro, asomando)
    for sx in (-6.0, 1.5, 9.0):
        m.caja(sx - 0.25, 1.6, 0.2, sx + 0.25, 3.3, 0.6, L("#70784c"), "propio")
        m.caja(sx - 0.15, 3.3, 0.25, sx + 0.15, 3.6, 0.55, L("#50541c"), "propio")
    # horda: 170 zombis fuera, apretados contra el muro; trepadores y pila en la cara exterior
    n = 0
    for i in range(170):
        sil = SILUETAS[i % 5]
        d = rng.expovariate(1 / 3.0)                       # distancia al muro: muchos pegados, cola hasta ~15 m
        zz = -(0.6 + min(d, 16))
        xx = rng.gauss(0, 7.0) if i % 3 else rng.uniform(-24, 22)
        pose = "trepar" if zz > -1.3 and rng.random() < 0.6 else rng.choice(["base", "andar1", "andar2"])
        yy = rng.uniform(0.0, 1.1) if pose == "trepar" and abs(xx) < 4 else 0.0     # pila junto al punto de presión
        yaw = rng.gauss(0, 0.35)                       # de cara al muro (+z)
        zombi(m, objs, sil, pose, np.array([xx, yy, zz]), yaw, rng); n += 1
    T = np.array(m.t, float)                               # (N, 3, 3)
    return T, np.array(m.alb), np.array(m.cls), n


# ---------------------------------------------------------------- raster ortográfico genérico
def raster(T, right, up, fwd, x0, y0, px, w, h):
    """Devuelve depth (h, w) e id de triángulo (−1 vacío). Pantalla: u = dot(p,right), v = dot(p,up), z = dot(p,fwd) (más pequeño = más cerca)."""
    U = (T @ right - x0) / px
    V = (y0 - T @ up) / px
    Z = T @ fwd
    depth = np.full((h, w), np.inf); tid = np.full((h, w), -1, int)
    for i in range(len(T)):
        u, v, z = U[i], V[i], Z[i]
        mnx, mxx = int(max(0, math.floor(u.min()))), int(min(w - 1, math.ceil(u.max())))
        mny, mxy = int(max(0, math.floor(v.min()))), int(min(h - 1, math.ceil(v.max())))
        if mnx > mxx or mny > mxy: continue
        den = (v[1] - v[2]) * (u[0] - u[2]) + (u[2] - u[1]) * (v[0] - v[2])
        if abs(den) < 1e-9: continue
        xs, ys = np.meshgrid(np.arange(mnx, mxx + 1) + 0.5, np.arange(mny, mxy + 1) + 0.5)
        a = ((v[1] - v[2]) * (xs - u[2]) + (u[2] - u[1]) * (ys - v[2])) / den
        b = ((v[2] - v[0]) * (xs - u[2]) + (u[0] - u[2]) * (ys - v[2])) / den
        c = 1 - a - b
        ins = (a >= 0) & (b >= 0) & (c >= 0)
        if not ins.any(): continue
        zz = a * z[0] + b * z[1] + c * z[2]
        sub = depth[mny:mxy + 1, mnx:mxx + 1]; tsub = tid[mny:mxy + 1, mnx:mxx + 1]
        upd = ins & (zz < sub)
        sub[upd] = zz[upd]; tsub[upd] = i
    return depth, tid


def normales(T):
    n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    l = np.linalg.norm(n, axis=1, keepdims=True); n = n / np.maximum(l, 1e-9)
    return n


# ---------------------------------------------------------------- variantes de luz y cielo (lineal; exposición calibrada en la A)
def dirsol(elev, az):
    e, a = math.radians(elev), math.radians(az)
    return np.array([math.cos(e) * math.sin(a), math.sin(e), math.cos(e) * math.cos(a)])     # hacia el sol; az 0 = +z (norte, de donde viene la horda)


VARIANTES = {
    "A": dict(nombre="Mediodía limpio", desc="sol alto 62°, blanco cálido; cielo azul pálido",
              sol=dirsol(62, 200), sol_col=hexrgb("#fff4e0"), sol_i=2.4, cielo=hexrgb("#a8bcd0"), suelo=hexrgb("#a89878"), amb_i=0.55, bruma=0.0, bruma_col=hexrgb("#c8c4b8")),
    "B": dict(nombre="Mañana rasante", desc="sol 24° del este, cálido; sombras largas hacia el oeste",
              sol=dirsol(24, 95), sol_col=hexrgb("#ffe2b8"), sol_i=2.4, cielo=hexrgb("#9cb0c8"), suelo=hexrgb("#a08c70"), amb_i=1.0, bruma=0.0, bruma_col=hexrgb("#c8c4b8")),
    "C": dict(nombre="Nublado", desc="sol velado 50°, luz plana; cielo gris",
              sol=dirsol(50, 160), sol_col=hexrgb("#f0f0ec"), sol_i=0.7, cielo=hexrgb("#b4b8bc"), suelo=hexrgb("#9c9484"), amb_i=1.25, bruma=0.0, bruma_col=hexrgb("#c8c4b8")),
    "D": dict(nombre="Tarde dorada", desc="sol 18° del oeste, dorado suave (sin ámbar de aviso)",
              sol=dirsol(18, 265), sol_col=hexrgb("#ffd8a0"), sol_i=2.3, cielo=hexrgb("#a4acc0"), suelo=hexrgb("#a88c68"), amb_i=1.0, bruma=0.0, bruma_col=hexrgb("#c8c4b8")),
    "E": dict(nombre="Calima sin niebla", desc="sol 45° velado, cielo beige; el polvo va en la luz, no en niebla",
              sol=dirsol(45, 180), sol_col=hexrgb("#fff0d8"), sol_i=1.3, cielo=hexrgb("#c8bca4"), suelo=hexrgb("#a89878"), amb_i=1.0, bruma=0.0, bruma_col=hexrgb("#c4b89c")),
    "N": dict(nombre="Noche (referencia)", desc="luna 0,45 (D-N69), suelo al sol de luna en L* 25",
              sol=dirsol(40, 220), sol_col=hexrgb("#b8c4e0"), sol_i=1.0, cielo=hexrgb("#283440"), suelo=hexrgb("#303424"), amb_i=0.35, bruma=0.0, bruma_col=hexrgb("#c8c4b8"), suelo_L=25),
}
EXPO_Y = ((64 + 16) / 116) ** 3    # Y lineal del suelo apisonado al sol tras la exposición (L* 64; su albedo #b4a080 es L* 66)


def ruido_suelo(wx, wz, dentro):
    """Albedo fino del suelo (D-N67): motas sueltas #88885c y chinas #94948c sobre beige, determinista por celda de 0,1 m."""
    cx, cz = np.floor(wx / 0.1).astype(np.int64), np.floor(wz / 0.1).astype(np.int64)
    hsh = np.abs((cx * 73856093) ^ (cz * 19349663) ^ np.where(dentro, 0, 83492791))
    r = (hsh % 1000) / 1000.0
    base = np.broadcast_to(s2l(hexrgb("#b4a080")), wx.shape + (3,)).copy()
    mot = r < np.where(dentro, 0.05, 0.10)
    chi = (r > 0.96)
    base[mot] = s2l(hexrgb("#88885c")); base[chi] = s2l(hexrgb("#94948c"))
    return base


def render(T, ALB, CLS, var, cam_buf):
    depth, tid, right, up, fwd, x0, y0 = cam_buf
    N = normales(T)
    # cámara mira hacia el muro: si la normal apunta lejos de la cámara, se gira (caras dobles)
    flip = (N @ fwd) > 0
    N[flip] *= -1
    # sombra: raster desde el sol (0,08 m por texel), sobre la zona visible
    L = var["sol"]
    sr = np.cross(np.array([0, 1.0, 0]), -L); sr = sr / np.linalg.norm(sr) if np.linalg.norm(sr) > 1e-6 else np.array([1.0, 0, 0])
    su = np.cross(-L, sr)
    sp = 0.08
    pts = T.reshape(-1, 3)
    vis = (np.abs(pts[:, 0]) < 34) & (pts[:, 2] > -24) & (pts[:, 2] < 24)
    pu, pv = pts[vis] @ sr, pts[vis] @ su
    sx0, sy0 = pu.min() - 1, pv.max() + 1
    sw, sh = int((pu.max() - pu.min() + 2) / sp) + 1, int((pv.max() - pv.min() + 2) / sp) + 1
    sdepth, _ = raster(T, sr, su, -L, sx0, sy0, sp, sw, sh)
    # posición de mundo de cada píxel de cámara
    hh, ww = depth.shape
    ys, xs = np.mgrid[0:hh, 0:ww]
    U = x0 + (xs + 0.5) * PX; V = y0 - (ys + 0.5) * PX
    P = U[..., None] * right + V[..., None] * up + depth[..., None] * fwd
    ok = tid >= 0
    img = np.zeros((hh, ww, 3))
    t = tid[ok]
    alb = ALB[t].copy()
    cls = CLS[t]
    suelo = (cls == "suelo_in") | (cls == "suelo_out")
    if suelo.any():
        alb[suelo] = ruido_suelo(P[ok][suelo, 0], P[ok][suelo, 2], cls[suelo] == "suelo_in")
    n = N[t]
    ndl = np.clip(n @ L, 0, 1)
    # sombra
    q = P[ok] + n * 0.04
    qu = ((q @ sr) - sx0) / sp; qv = (sy0 - (q @ su)) / sp; qz = q @ (-L)
    dentro = (qu >= 0) & (qu < sw) & (qv >= 0) & (qv < sh)
    iu, iv = np.clip(qu.astype(int), 0, sw - 1), np.clip(qv.astype(int), 0, sh - 1)
    lit = (qz <= sdepth[iv, iu] + 0.06) | ~dentro                        # fuera del mapa de sombra: al sol
    amb = var["amb_i"] * (s2l(var["cielo"]) * (0.5 + 0.5 * n[:, 1:2]) + s2l(var["suelo"]) * (0.5 - 0.5 * n[:, 1:2]))
    luz = s2l(var["sol_col"]) * var["sol_i"] * (ndl * lit)[:, None] + amb
    c = alb * luz
    # exposición automática por variante (como la haría el juego por hora): el suelo apisonado al sol en L* ≈ 64
    sol_suelo = suelo & lit & (ndl > 0.05)
    ref = c[sol_suelo] if sol_suelo.any() else c[suelo]
    yref = np.median(ref @ np.array([0.2126, 0.7152, 0.0722]))
    ly = var.get("suelo_L", 64)
    c = c * ((((ly + 16) / 116) ** 3) / max(yref, 1e-6))
    if var["bruma"] > 0: c = c * (1 - var["bruma"]) + s2l(var["bruma_col"]) * var["bruma"] * 0.95
    img[ok] = c
    return img, tid, cls, ok


def medir(img, tid, cls_all, ok, CLS):
    Ls = lstar_lin(img)
    cl = np.full(tid.shape, "", object); cl[ok] = CLS[tid[ok]]
    res = {}
    for k in ("horda", "suelo_in", "suelo_out", "via", "carril", "tren", "techo", "carga", "muro_cara", "muro_corona", "propio"):
        v = Ls[cl == k]
        if v.size: res[k] = dict(p5=float(np.percentile(v, 5)), med=float(np.median(v)), p95=float(np.percentile(v, 95)), n=int(v.size))
    return res


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    T, ALB, CLS, n = escena()
    right = np.array([1.0, 0, 0])
    fwd = np.array([0, -math.sin(PITCH), math.cos(PITCH)])
    up = np.cross(fwd, right) * -1
    if up[1] < 0: up = -up
    # encuadre: el muro (z = 0) algo por encima del centro
    centro = np.array([0, 0, 3.0])
    x0 = centro @ right - W * PX / 2; y0 = centro @ up + H * PX / 2
    depth, tid = raster(T, right, up, fwd, x0, y0, PX, W, H)
    cam = (depth, tid, right, up, fwd, x0, y0)
    medidas, imgs = {}, {}
    for k, var in VARIANTES.items():
        img, tid_, cls_, ok = render(T, ALB, CLS, var, cam)
        srgb = (l2s(img) * 255 + 0.5).astype(np.uint8)
        srgb[~ok] = (np.array([0x30, 0x34, 0x24]))
        Image.fromarray(srgb).save(OUT / f"dia_{k}.png")
        imgs[k] = Image.fromarray(srgb)
        medidas[k] = medir(img, tid_, None, ok, CLS)
        print(k, {kk: round(v["med"], 1) for kk, v in medidas[k].items()})
    (OUT / "medidas.json").write_text(json.dumps(medidas, indent=1, ensure_ascii=False))
    hoja(imgs, medidas)


def hoja(imgs, medidas):
    K = 2
    f = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 13)
    fb = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 15)
    cw, chh = W * K, H * K
    tw = 360
    out = Image.new("RGB", (cw + tw + 30, 40 + len(imgs) * (chh + 20)), (0x18, 0x1c, 0x30))
    d = ImageDraw.Draw(out)
    d.text((10, 10), "D-N157 · Sagunto de día con la horda en el muro (render por código, cámara del juego, 1 px = 0,144 m; x2)", fill=(0xd4, 0xd0, 0xcc), font=fb)
    y = 40
    for k, im in imgs.items():
        var, m = VARIANTES[k], medidas[k]
        out.paste(im.resize((cw, chh), Image.NEAREST), (10, y))
        sky = Image.new("RGB", (tw - 10, 18), tuple(int(c * 255) for c in var["cielo"]))
        x = cw + 20
        d.text((x, y), f"{k} · {var['nombre']}", fill=(0xd4, 0xd0, 0xcc), font=fb)
        d.text((x, y + 20), var["desc"], fill=(0xb8, 0xb8, 0xb8), font=f)
        out.paste(sky, (x, y + 40)); d.text((x + 4, y + 41), "cielo (ambiente)", fill=(0x18, 0x1c, 0x30), font=f)
        hz = m["horda"]
        filas = [("horda (p5–p95)", f"{hz['p5']:.1f}–{hz['p95']:.1f}  med {hz['med']:.1f}")]
        for kk, nm in (("suelo_out", "suelo fuera"), ("muro_cara", "muro, cara"), ("muro_corona", "muro, corona"), ("via", "vía D"), ("tren", "tren, lateral"), ("techo", "tren, techo"), ("propio", "soldados")):
            if kk in m: filas.append((nm, f"med {m[kk]['med']:.1f}"))
        yy = y + 66
        for a_, b_ in filas:
            d.text((x, yy), a_, fill=(0xb8, 0xb8, 0xb8), font=f); d.text((x + 140, yy), b_, fill=(0xd4, 0xd0, 0xcc), font=f); yy += 17
        # ΔL*: el p95 de la horda contra la mediana de cada superficie; entre superficies, medianas (regla de Sara, ΔL* ≥ 8)
        md = lambda k_: m[k_]["med"]
        chk = [("horda→suelo", md("suelo_out") - hz["p95"]), ("horda→muro", md("muro_cara") - hz["p95"]),
               ("horda→vía", md("via") - hz["p95"]), ("horda→tren", md("tren") - hz["p95"]), ("horda→soldado", md("propio") - hz["p95"]),
               ("tren↔vía", abs(md("tren") - md("via"))), ("vía↔muro", abs(md("via") - md("muro_cara"))), ("muro↔suelo", abs(md("muro_cara") - md("suelo_in")))]
        yy += 4
        for a, v in chk:
            okv = v >= 8
            d.text((x, yy), f"ΔL* {a}", fill=(0xb8, 0xb8, 0xb8), font=f)
            d.text((x + 170, yy), f"{v:5.1f} {'✓' if okv else '✗ < 8'}", fill=(0xd4, 0xd0, 0xcc) if okv else (0xe0, 0x8c, 0x30), font=f); yy += 17
        y += chh + 20
    out.save(OUT / "hoja_dia_horda.png")


if __name__ == "__main__":
    main()
