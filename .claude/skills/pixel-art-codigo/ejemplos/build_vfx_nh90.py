"""VFX del NH90 sobre la losa y de la vista cercana del tren. Bruno (Arte técnico), 7-nov 01:05.

Encargo de Mak (01:00), con las fotos de Paula (referencias-reales/entorno, H7–H9). MUESTRA de 5 estilos (A–E) del
mismo juego de efectos:
  polvo_losa_despegue   96x96, 12 f, una vez  el rotor arranca, el anillo crece y, al subir, se abre y se apaga
  polvo_losa_aterrizaje 96x96, 12 f, una vez  el anillo aparece al acercarse, revienta al tocar y se asienta
  fogonazo_mag          24x24, 3 f, bucle     MAG 58 de puerta (boca a la izquierda, dispara a la derecha)
  sombra_nh90           72x72, 3 alturas      decal en olive_shadow (rotor de 16,3 m a 0,25 m/texel)
  humo_diesel           24x48, 8 f, una vez   bocanada de la locomotora en la vista lateral (13 px/m)
  chispas_rueda         32x32, 4 f, bucle     contacto rueda-carril, el tren va a +X
Lo que se ve en las fotos: la losa de la FAMET (H7_2) es hormigón claro con juntas y hierba en las juntas; sobre
hormigón el rotor no levanta tierra sino arenilla, briznas y papeles, así que el polvo es más claro y menos denso que el
del descampado (D-N83). H8_3: el disco del rotor no deja sombra nítida con sol alto, solo un velo. SIN VERIFICAR: no
hay foto de un NH90 despegando de una losa; las fases (arranque, despegue, toque) son a ojo.
Colores: polvo en beige, concrete_light y grey_light; humo en olive_grey y grey_brown (nada en la banda L* 9–13 ni en
1c200c o 442c0c); el fogonazo y las chispas enfrían de crema a ámbar y ocre (el ámbar es transitorio).
"""
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import _h, blob, tramado  # noqa: E402

ESTILOS = "ABCDE"
NOTAS = {"A": "Tramado denso (como el polvo de la D-N83): vetas que giran",
         "B": "Motas sueltas: partículas de 1-2 px, sin relleno",
         "C": "Bultos planos con borde: dos tonos y contorno",
         "D": "Vetas radiales: rayas cortas en la dirección del viento",
         "E": "Mínimo: pocos píxeles, mucho contraste"}


# --- Polvo del rotor sobre la losa ------------------------------------------------------------
def _fase(fase, f, n=12):
    s = f / (n - 1)
    if fase == "despegue":
        if s < .4:                                       # arranque en el suelo
            return 22 + s / .4 * 12, .2 + s / .4 * .6
        u = (s - .4) / .6                                # sube: el anillo se abre y se apaga
        return 34 + u * 12, .8 * (1 - u) ** 1.3
    if s < .6:                                           # se acerca
        u = s / .6
        return 46 - u * 14, u * .7
    if s < .75:                                          # toca: revienta
        u = (s - .6) / .15
        return 30 + u * 10, 1.0
    u = (s - .75) / .25                                  # se asienta
    return 40 + u * 4, 1 - u * .85


def polvo(estilo, fase, f):
    s = Sprite(96, 96)
    cx = cy = 48
    R, D = _fase(fase, f)
    ancho = 12
    if estilo == "A":
        for y in range(96):
            for x in range(96):
                d = math.hypot(x + .5 - cx, y + .5 - cy)
                if abs(d - R) > ancho:
                    continue
                a = math.atan2(y + .5 - cy, x + .5 - cx)
                veta = .5 + .5 * math.sin(a * 8 - f * math.pi / 4 + d * .25)
                banda = 1 - abs(d - R) / ancho
                if tramado(x, y, D * banda * (.3 + .6 * veta)):
                    s.set(x, y, "grey_light" if veta > .8 and d < R else "beige" if veta > .45 else "concrete_light")
    elif estilo == "B":
        for k in range(int(380 * D)):
            a = _h(k, 0, 901) * math.tau + f * .05
            d = R + (_h(k, 1, 902) - .5) * 2 * ancho + f * .6 * _h(k, 2, 903)
            x, y = cx + math.cos(a) * d, cy + math.sin(a) * d
            c = "grey_light" if k % 4 == 0 else "beige"
            s.set(int(x), int(y), c)
            if k % 5 == 0:
                s.set(int(x) + 1, int(y), c)
    elif estilo == "C":
        n = 10
        for k in range(n):
            a = k * math.tau / n + f * math.pi / 24 + _h(k, 0, 904) * .3
            r = (3 + _h(k, 1, 905) * 3) * (.4 + D)
            if D < .08:
                continue
            x, y = cx + math.cos(a) * R, cy + math.sin(a) * R
            blob(s, x, y, r, "beige", 1, borde="concrete_light")
            blob(s, x - r * .3, y - r * .3, r * .45, "grey_light", 1)
    elif estilo == "D":
        for k in range(int(70 * D) + 1):
            a = k * math.tau / 70 + _h(k, 0, 906) * .08 + f * .03
            d0 = R - ancho * .6 + _h(k, 1, 907) * ancho
            largo = 3 + int(_h(k, 2, 908) * 5 * (.5 + D))
            for t in range(largo):
                c = "grey_light" if t == largo - 1 else "beige" if t > largo // 2 else "concrete_light"
                s.set(int(cx + math.cos(a) * (d0 + t)), int(cy + math.sin(a) * (d0 + t)), c)
    else:                                                # E: anillo de trazos y cuatro motas
        if D > .05:
            n = int(48 * min(1, D * 1.4))
            for k in range(n):
                a = k * math.tau / 48 + f * math.pi / 24
                if k % 3 == 2:
                    continue
                px_, py_ = int(cx + math.cos(a) * R), int(cy + math.sin(a) * R)
                s.set(px_, py_, "grey_light"); s.set(px_ + 1, py_, "beige")
            for k in range(4):
                a = k * math.pi / 2 + f * .2
                s.set(int(cx + math.cos(a) * (R + 6)), int(cy + math.sin(a) * (R + 6)), "grey_light")
    # briznas y papeles lanzados hacia fuera (en todas, menos en la E): solo cuando el viento aprieta
    if estilo != "E" and D > .4:
        for k in range(6):
            a = k * math.tau / 6 + f * .4 + _h(k, f, 909)
            d = R + ancho + (f * 2 + k * 3) % 8
            x, y = int(cx + math.cos(a) * d), int(cy + math.sin(a) * d)
            if k % 2:
                s.set(x, y, "olive_light"); s.set(x + 1, y, "olive_light")       # brizna de hierba de las juntas
            else:
                s.rect(x, y, 2, 2, "bone")                                        # papel
    return s


# --- Fogonazo de la MAG -----------------------------------------------------------------------
def mag(estilo, f):
    s = Sprite(24, 24)
    ox, oy = 3, 12
    if estilo == "A":
        largo = (12, 9, 5)[f]
        for x in range(largo):
            t = x / largo
            semi = (2.5, 2, 1.2)[f] * (1 - t)
            for y in range(int(oy - semi), int(oy + semi) + 1):
                if tramado(x, y, 1 - t * .6):
                    s.set(ox + x, y, "cream" if t < .35 else "amber" if t < .7 else "ochre_dark")
        for k in range(1, (5, 3, 1)[f]):
            s.set(ox + 2, oy - k - 1, "amber"); s.set(ox + 2, oy + k + 1, "amber")
    elif estilo == "B":                                  # estrella de 4 puntas y motas
        r = (6, 4, 2)[f]
        for k in range(-r, r + 1):
            c = "cream" if abs(k) < 2 else "amber"
            s.set(ox + 3 + k, oy, c)
            if abs(k) <= r // 2:
                s.set(ox + 3, oy + k, c)
        for k in range(3 - f):
            s.set(ox + 10 + k * 3, oy + (k % 2) * 2 - 1, "amber")
    elif estilo == "C":                                  # lengua plana con borde
        if f < 2:
            blob(s, ox + 5, oy, (4, 3)[f], "cream", 1, borde="amber")
            s.rect(ox + 9, oy - 1, (6, 3)[f], 3, "amber"); s.rect(ox + 10, oy, (5, 2)[f], 1, "cream")
        else:
            blob(s, ox + 6, oy, 2, "ochre_dark", 1)
    elif estilo == "D":                                  # rayos finos
        for a in (-0.5, -0.2, 0, 0.2, 0.5):
            largo = int((14, 9, 4)[f] * (1 - abs(a)))
            for t in range(largo):
                s.set(int(ox + 1 + math.cos(a) * t), int(oy + math.sin(a) * t), "cream" if t < 3 else "amber")
    else:                                                # E: cruz de 3 y punta
        if f < 2:
            s.set(ox + 1, oy, "cream"); s.set(ox + 2, oy, "cream"); s.set(ox + 3, oy, "cream")
            s.set(ox + 2, oy - 1, "amber"); s.set(ox + 2, oy + 1, "amber")
            if f == 0:
                s.set(ox + 4, oy, "amber"); s.set(ox + 5, oy, "amber")
    return s


# --- Sombra del NH90 --------------------------------------------------------------------------
def _fuselaje(s, cx, cy, c, dens=1.0):
    for y in range(72):
        for x in range(72):
            u, v = (x + .5 - cx), (y + .5 - cy)
            cuerpo = (u / 22) ** 2 + (v / 6.5) ** 2 <= 1 and u > -12 or (-12 <= u <= 0 and abs(v) <= 6 - (-u) * .3)
            cola = -31 <= u <= -10 and abs(v) <= 1.5
            deriva = -33 <= u <= -29 and abs(v) <= 4 and u <= -29 + (4 - abs(v))
            if (cuerpo or cola or deriva) and tramado(x, y, dens):
                s.set(x, y, c)


def sombra(estilo, altura):
    s = Sprite(72, 72)
    cx = cy = 36
    k = {"bajo": 0, "medio": 1, "alto": 2}[altura]
    dens = (.95, .7, .45)[k]
    R = 32
    if estilo in ("A", "C"):
        disco = (.35, .22, .12)[k] if estilo == "A" else (.25, .2, .15)[k]
        for y in range(72):
            for x in range(72):
                d = math.hypot(x + .5 - cx, y + .5 - cy)
                if d <= R and tramado(x, y, disco * (1 if estilo == "A" else (1 if d > R - 3 else .4))):
                    s.set(x, y, "olive_shadow")
        _fuselaje(s, cx, cy, "olive_shadow", dens)
    elif estilo == "B":                                  # fuselaje y motas donde pasan las palas
        _fuselaje(s, cx, cy, "olive_shadow", dens)
        for j in range(90):
            a = j * math.tau / 90
            d = R * (.4 + .6 * _h(j, k, 910))
            if tramado(j, k, (.6, .4, .25)[k]):
                s.set(int(cx + math.cos(a) * d), int(cy + math.sin(a) * d), "olive_shadow")
    elif estilo == "D":                                  # anillo del disco y fuselaje
        for j in range(200):
            a = j * math.tau / 200
            if j % (2 + k) == 0:
                s.set(int(cx + math.cos(a) * R), int(cy + math.sin(a) * R), "olive_shadow")
        _fuselaje(s, cx, cy, "olive_shadow", dens)
    else:                                                # E: fuselaje y cuatro palas
        _fuselaje(s, cx, cy, "olive_shadow", dens)
        for j in range(4):
            a = j * math.pi / 2 + math.pi / 4
            for t in range(4, R):
                if tramado(t, j, dens):
                    s.set(int(cx + math.cos(a) * t), int(cy + math.sin(a) * t), "olive_shadow")
    return s


# --- Humo del diésel (vista lateral) ----------------------------------------------------------
def humo(estilo, f):
    s = Sprite(24, 48)
    t = f / 7
    x, y = 12 - t * 3, 38 - t * 12
    r = 2.5 + t * 7
    dens = 1 - t * .8
    if estilo == "A":
        for (dx, dy, fr) in ((0, 0, 1), (-r * .6, 1, .7), (r * .5, 1.5, .6)):
            if t < .4:
                blob(s, x + dx, y + dy * t * 3, r * fr, "olive_grey", dens, borde="grey_brown")
            else:
                blob(s, x + dx, y + dy * t * 3, r * fr, "grey_brown", dens * .85)
                blob(s, x + dx + 1, y + dy * t * 3 - 1, r * fr * .5, "olive_grey", dens * .6)
    elif estilo == "B":
        for k in range(int(40 * dens) + 4):
            a = _h(k, 0, 911) * math.tau
            d = r * math.sqrt(_h(k, 1, 912))
            s.set(int(x + math.cos(a) * d * 1.2), int(y + math.sin(a) * d * .8), "olive_grey" if k % 3 else "grey_brown")
    elif estilo == "C":
        if dens > .25:
            blob(s, x, y, r * .9, "grey_brown", 1, borde="olive_grey" if t < .5 else None)
            blob(s, x - r * .3, y - r * .3, r * .45, "olive_grey", 1)
        else:
            blob(s, x, y, r * .8, "grey_brown", .5)
    elif estilo == "D":                                  # bocanada estirada hacia atrás (el tren va a +X)
        for yy in range(int(y - r * .5), int(y + r * .5) + 1):
            largo = int(r * 2 * (1 - abs(yy - y) / max(1, r * .5)))
            for xx in range(largo):
                if tramado(int(x - xx + r * .6), yy, max(.35, dens) * (1 - .6 * xx / max(1, largo))):
                    s.set(int(x - xx + r * .6), yy, "olive_grey" if xx < largo * .4 else "grey_brown")
    else:                                                # E: contorno de tres círculos
        for (dx, dy, fr) in ((0, 0, 1), (-r * .6, 1, .6)):
            if dens < .3 and dx:
                continue
            rr = r * fr * .8
            for j in range(int(rr * 6) + 4):
                a = j * math.tau / (int(rr * 6) + 4)
                s.set(int(x + dx + math.cos(a) * rr), int(y + dy + math.sin(a) * rr), "olive_grey")
    return s


# --- Chispas de la rueda ----------------------------------------------------------------------
def chispas(estilo, f):
    s = Sprite(32, 32)
    cx, cy = 16, 20
    if estilo == "A":
        s.rect(cx - 1, cy - 1, 2, 2, "cream")
        for k in range(9):
            a = math.pi * (0.85 + .35 * _h(k, f, 801))
            largo = 3 + int(_h(k, f, 802) * 9)
            for t in range(largo):
                if t >= largo - 1 and (k + f) % 2:
                    continue
                s.set(int(cx + math.cos(a) * t), int(cy - math.sin(a) * t * .4 + t * t * .04),
                      "cream" if t < 2 else "amber" if t < largo * .7 else "ochre_dark")
    elif estilo == "B":
        s.set(cx, cy, "cream")
        for k in range(14):
            d = 2 + _h(k, f, 913) * 13
            a = math.pi * (0.85 + .3 * _h(k, f, 914))
            s.set(int(cx + math.cos(a) * d), int(cy - math.sin(a) * d * .3 + d * d * .03),
                  "cream" if d < 5 else "amber" if d < 10 else "ochre_dark")
    elif estilo == "C":
        blob(s, cx, cy, 2.5 if f % 2 == 0 else 2, "cream", 1, borde="amber")
        for k in range(4):
            a = math.pi * (0.9 + .25 * _h(k, f, 915))
            for t in range(3, 8):
                s.set(int(cx + math.cos(a) * t), int(cy - math.sin(a) * t * .4 + t * .2), "amber")
    elif estilo == "D":
        s.rect(cx - 1, cy - 1, 2, 2, "cream")
        for k in range(5):
            largo = 8 + int(_h(k, f, 916) * 8)
            y0 = cy + int((_h(k, f, 917) - .3) * 3)
            for t in range(largo):
                s.set(cx - t, y0 + t // 6, "cream" if t < 3 else "amber" if t < largo * .7 else "ochre_dark")
    else:
        s.set(cx, cy, "cream"); s.set(cx - 2, cy, "amber"); s.set(cx - 4 - f % 2, cy + 1, "ochre_dark")
    return s


# --- Hoja de muestra --------------------------------------------------------------------------
def _losa(w, h):
    im = Image.new("RGBA", (w, h), PAL["concrete"])
    px = im.load()
    for y in range(h):
        for x in range(w):
            if x % 32 == 0 or y % 32 == 0:
                px[x, y] = PAL["grey_brown"]
            elif (x * 7 + y * 13) % 97 == 0:
                px[x, y] = PAL["olive_grey"]
    return im


def _noche(w, h):
    im = Image.new("RGBA", (w, h), PAL["navy"])
    px = im.load()
    for y in range(int(h * .88), h):
        for x in range(w):
            px[x, y] = PAL["olive_shadow"]
    for x in range(w):
        px[x, int(h * .88)] = PAL["concrete"]
    return im


def muestra(out):
    import build_maquetas as m
    E = 2
    filas = []
    for est in ESTILOS:
        celdas = []
        for f in (0, 3, 6, 9):
            celdas.append(("losa", polvo(est, "despegue", f).img))
        for f in (5, 8):
            celdas.append(("losa", polvo(est, "aterrizaje", f).img))
        for alt in ("bajo", "medio", "alto"):
            celdas.append(("losa", sombra(est, alt).img))
        for f in range(3):
            celdas.append(("noche", mag(est, f).img))
        for f in (0, 2, 4, 7):
            celdas.append(("noche", humo(est, f).img))
        for f in (0, 1):
            celdas.append(("noche", chispas(est, f).img))
        filas.append(celdas)
    ancho = max(sum(c[1].width * (E if c[1].width > 48 else 4) + 8 for c in fila) for fila in filas) + 12
    alto_fila = 96 * E + 28
    hoja = Image.new("RGBA", (ancho, alto_fila * 5 + 24), PAL["navy"])
    m.texto(hoja, (6, 4), "Polvo despegue (f 0, 3, 6, 9) · aterrizaje (f 5, 8) · sombra baja, media, alta · "
            "MAG (3 f) · humo diésel (f 0, 2, 4, 7) · chispas (2 f)", 9, "bone")
    for i, (est, fila) in enumerate(zip(ESTILOS, filas)):
        y0 = 24 + i * alto_fila
        m.texto(hoja, (6, y0 + 2), f"{est} · {NOTAS[est]}", 9, "cream")
        x = 6
        for tipo, img in fila:
            w, h = img.width, img.height
            fondo = _losa(w, h) if tipo == "losa" else _noche(w, h)
            fondo.alpha_composite(img)
            e = E if w > 48 else 4
            g = fondo.resize((w * e, h * e), Image.NEAREST)
            hoja.alpha_composite(g, (x, y0 + 20 + (96 * E - h * e) // 2))
            x += w * e + 8
    hoja.save(out / "muestra_vfx_nh90_tren_5_estilos.png")
    return hoja


# --- Piezas finales: estilo A con la sombra de la D (aprobado por Mak, 07-11 01:03) -------------
FPS = 12
EFECTOS = {
    # nombre                    (función, fotogramas, (w, h), bucle, pivote, texel_m, ms)
    "polvo_losa_despegue": (lambda f: polvo("A", "despegue", f), 12, (96, 96), False, (.5, .5), .1, 125),
    "polvo_losa_aterrizaje": (lambda f: polvo("A", "aterrizaje", f), 12, (96, 96), False, (.5, .5), .1, 125),
    "fogonazo_mag_puerta": (lambda f: mag("A", f), 3, (24, 24), True, (.12, .5), .1, 50),
}


def _hoja(out, base, frames, w, h, bucle, piv, texel, ms, extra=None):
    import json
    n = len(frames)
    hoja = Image.new("RGBA", (w * n, h), (0, 0, 0, 0))
    for f, sp in enumerate(frames):
        hoja.alpha_composite(sp.img, (w * f, 0))
    hoja.save(out / f"{base}.png")
    vm = {"fps": round(1000 / ms), "loop": bucle, "frames": n, "frameSize": [w, h], "pivot": list(piv),
          "texel_m": texel, "blend": "alfa", "capa": "VFX_HD", "origen": "pixelvm, build_vfx_nh90.py"}
    vm.update(extra or {})
    meta = {"frames": [{"filename": f"{base} {f}", "frame": {"x": w * f, "y": 0, "w": w, "h": h}, "rotated": False,
                        "trimmed": False, "spriteSourceSize": {"x": 0, "y": 0, "w": w, "h": h},
                        "sourceSize": {"w": w, "h": h}, "duration": ms} for f in range(n)],
            "meta": {"image": f"{base}.png", "size": {"w": w * n, "h": h}, "format": "RGBA8888", "viaMuerta": vm}}
    (out / f"{base}.json").write_text(json.dumps(meta, ensure_ascii=False, indent=1), encoding="utf-8")


def exportar(out):
    out = Path(out)
    todo = {}
    notas = {"polvo_losa_despegue": {"uso": "del arranque del rotor (f 0) a 10 m de altura (f 11); 1,5 s en total, centrado bajo el rotor"},
             "polvo_losa_aterrizaje": {"uso": "de 10 m de altura (f 0) al toque (f 7) y el asentamiento (f 11); en la losa del helipuerto. Fuera de la losa sigue vm_vfx_polvo_rotor (D-N83)"},
             "fogonazo_mag_puerta": {"uso": "MAG 58 de puerta del NH90; sustituye a vm_vfx_fogonazo_mag (2 f) en el NH90; 50 ms por fotograma en ráfaga"}}
    for nombre, (fn, n, (w, h), bucle, piv, texel, ms) in EFECTOS.items():
        frames = [fn(f) for f in range(n)]
        _hoja(out, f"vm_vfx_{nombre}", frames, w, h, bucle, piv, texel, ms, notas[nombre])
        todo[nombre] = frames
    for alt in ("bajo", "medio", "alto"):
        sp = sombra("D", alt)
        sp.img.save(out / f"decal_sombra_nh90_disco_{alt}.png")
        todo[f"sombra_{alt}"] = [sp]
    import json
    (out / "decal_sombra_nh90_disco.json").write_text(json.dumps({
        "texel_m": 0.25, "tamano": [72, 72], "pivote": [0.5, 0.5], "morro": "+X de la textura (a la derecha)",
        "alturas": {"bajo": "< 3 m", "medio": "3–15 m", "alto": "> 15 m"},
        "uso": "decal en el suelo bajo el NH90, girado con el yaw del aparato; sustituye a decal_sombra_heli_nh90_* (anillo del disco del rotor y fuselaje)",
        "color": "olive_shadow (D-N100)"}, ensure_ascii=False, indent=1), encoding="utf-8")
    return todo


if __name__ == "__main__":
    OUT = Path(next((a for a in sys.argv[1:] if not a.startswith("--")), "out_vfx_nh90"))
    OUT.mkdir(parents=True, exist_ok=True)
    if "--muestra" in sys.argv:
        muestra(OUT)
    else:
        todo = exportar(OUT)
        import build_maquetas as m
        E = 3
        filas = list(todo.items())
        Wt = max(sum(sp.w * E + 6 for sp in fr) for _, fr in filas) + 220
        Ht = sum(fr[0].h * E + 6 for _, fr in filas)
        hoja = Image.new("RGBA", (Wt, Ht), PAL["navy"])
        y = 0
        for n, fr in filas:
            m.texto(hoja, (4, y + 4), n, 9, "cream")
            x = 220
            for sp in fr:
                fondo = _losa(sp.w, sp.h) if sp.w >= 48 else _noche(sp.w, sp.h)
                fondo.alpha_composite(sp.img)
                hoja.alpha_composite(fondo.resize((sp.w * E, sp.h * E), Image.NEAREST), (x, y))
                x += sp.w * E + 6
            y += fr[0].h * E + 6
        hoja.save(OUT / "contacto_vfx_nh90_x3.png")
    print("ok")
