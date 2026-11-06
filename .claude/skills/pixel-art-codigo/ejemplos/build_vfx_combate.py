"""VFX de combate para el prototipo jugable (noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:04): fogonazos, impactos, explosión de granada, mucha sangre y restos de la horda,
y polvo de los rotores. El fogonazo y el polvo de impacto ya existen (build_d_vfx.py); aquí va lo nuevo.
Mismas reglas que la fase D: paleta de 32, alfa 0/255, desvanecidos con tramado, 12 fps, pivote en JSON.

  impacto_bala     32x32, 4 f, una vez   chispa y polvillo donde pega la bala (suelo o pared)
  granada          96x96, 10 f, una vez  destello, bola de fuego, metralla y humo que sube
  sangre_impacto   32x32, 5 f, una vez   salpicadura al recibir un disparo (dirección: hacia la derecha)
  sangre_muerte    48x48, 7 f, una vez   reventón al morir: chorro, trozos y gotas que caen
  polvo_rotor      96x96, 8 f, bucle     anillo de polvo que barre el suelo bajo el helicóptero
  restos_horda     32x32, 6 variantes    decals fijos: charco, regueros y trozos (se quedan en el suelo)

Sangre: blood_dark #4c1008 de base, wine #642030 y red #7c181c para lo fresco. Nunca el rojo de ataque
#982420 ni maroon (es más oscuro que la horda y se leería como un zombi caído). Los trozos van en
olive_shadow #303424, que es lo más oscuro que puede usar el decorado (la horda es #1c200c).
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import _h, blob, tramado  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_vfx_combate")
FPS = 12


# --- Impacto de bala ------------------------------------------------------------------------
def impacto_bala(f):
    s = Sprite(32, 32)
    cx, cy = 16, 20
    if f == 0:
        for k in range(-3, 4):
            s.set(cx + k, cy, "amber")
            s.set(cx, cy + k, "amber")
        s.set(cx, cy, "cream")
        for k in (1, 2):
            s.set(cx - k, cy - k, "cream")
            s.set(cx + k, cy - k, "cream")
    else:
        t = f / 3
        blob(s, cx, cy - t * 4, 2 + t * 4, "olive_grey", [1, .7, .35][f - 1])
        blob(s, cx - 1, cy - 1 - t * 4, 1 + t * 2.5, "concrete_light", [.9, .5, .2][f - 1])
        if f == 1:
            for k in range(5):                     # chinas
                a = math.pi * (1.15 + .7 * k / 4)
                s.set(int(cx + math.cos(a) * 7), int(cy + math.sin(a) * 6), "grey_brown")
            s.set(cx, cy, "amber")
    return s


# --- Granada --------------------------------------------------------------------------------
def granada(f):
    s = Sprite(96, 96)
    cx, cy = 48, 60                                # suelo bajo el centro; el humo sube
    if f == 0:                                     # destello
        blob(s, cx, cy, 9, "cream")
        for k in range(8):
            a = k * math.pi / 4
            for r in range(9, 17):
                s.set(int(cx + math.cos(a) * r), int(cy + math.sin(a) * r * .8), "cream" if r < 13 else "amber")
        return s
    if f <= 3:                                     # bola de fuego que crece y se enfría
        r = 10 + f * 5
        anillos = [("rust", 1.0), ("ochre_dark", .85), ("amber", .62), ("cream", .34)]
        for c, k in anillos:
            if f == 3 and c == "cream":
                continue
            rr = r * k
            for y in range(int(cy - rr) - 2, int(cy + rr) + 2):
                for x in range(int(cx - rr) - 2, int(cx + rr) + 2):
                    d = math.hypot(x + .5 - cx, (y + .5 - cy) * 1.15)
                    if d <= rr * (0.85 + .3 * _h(int(math.atan2(y - cy, x - cx) * 6), f, 501)):
                        s.set(x, y, c)
    # metralla y tierra lanzada (f 1–5)
    if 1 <= f <= 5:
        for k in range(18):
            a = _h(k, 0, 502) * math.tau
            v = 12 + _h(k, 1, 502) * 26
            x = cx + math.cos(a) * v * f / 3
            y = cy + math.sin(a) * v * f / 3 * .8 + 2.5 * f * f / 3     # caen
            c = "olive_shadow" if k % 3 else "grey_brown"
            s.set(int(x), int(y), c)
            if f <= 2:
                s.set(int(x) + 1, int(y), c)
    # humo (f 3–9): columna que sube y se deshace
    if f >= 3:
        t = (f - 3) / 6
        dens = 1 - t * .85
        for k, (dx, dy, fr) in enumerate([(0, 0, 1), (-9, 5, .75), (9, 5, .75), (-4, -9, .8), (5, -12, .7), (0, -20, .6)]):
            rr = (8 + t * 14) * fr
            px, py = cx + dx * (1 + t), cy + dy * (1 + t) - t * 22
            blob(s, px, py, rr, "concrete" if k < 3 else "olive_grey", dens)
            blob(s, px - rr * .3, py - rr * .3, rr * .55, "concrete_light", dens * .8)
        if f <= 5:                                 # brasas dentro del humo
            for k in range(6 - f):
                s.set(int(cx - 6 + _h(k, f, 503) * 12), int(cy - _h(k, f, 504) * 10), "amber")
    return s


# --- Sangre ---------------------------------------------------------------------------------
SANGRE = "blood_dark"


def sangre_impacto(f):
    """Salpicadura hacia la derecha (el sentido de la bala). El motor la gira según el disparo."""
    s = Sprite(32, 32)
    cx, cy = 10, 16
    for k in range(24):
        a = (_h(k, 0, 601) - .5) * 1.5
        v = 4 + _h(k, 1, 601) * 18
        d = v * (f + 1) / 4
        x = cx + math.cos(a) * d
        y = cy + math.sin(a) * d + .6 * f * f          # gravedad
        if f == 4 and k % 2:
            continue
        c = "red" if f < 2 and k % 3 == 0 else "wine" if k % 2 else SANGRE
        s.set(int(x), int(y), c)
        if f < 3 and k % 2 == 0:                       # gotas gordas al principio
            s.set(int(x) - 1, int(y), SANGRE)
            s.set(int(x), int(y) + 1, SANGRE)
    if f < 3:                                          # cono de rocío pegado al punto de impacto
        for k in range(10):
            a = (_h(k, f, 602) - .5) * 1.1
            for r in range(2, 5 + f * 2):
                s.set(int(cx + math.cos(a) * r), int(cy + math.sin(a) * r), "wine" if r < 4 else SANGRE)
        s.set(cx + 1, cy, "red")
    return s


def sangre_muerte(f):
    """Reventón al morir el zombi: chorro hacia arriba, trozos que salen y gotas que caen al suelo."""
    s = Sprite(48, 48)
    cx, cy = 24, 30
    if f <= 2:                                         # estallido en púas irregulares
        r = 6 + f * 5
        for k in range(16):
            a = k * math.tau / 16 + _h(k, 0, 612) * .3
            largo = r * (.5 + .6 * _h(k, 1, 612))
            for t in range(int(largo)):
                if f == 2 and t < largo * .5:
                    continue                           # el centro ya se ha vaciado
                c = "red" if t < 3 and f == 0 else "wine" if t > largo - 2 else SANGRE
                s.set(int(cx + math.cos(a) * t), int(cy - f * 2 + math.sin(a) * t * .85), c)
        if f < 2:
            blob(s, cx, cy - f * 2, 3 - f, "wine")
    for k in range(22):                                # gotas en abanico
        a = math.pi * (1.05 + .9 * _h(k, 0, 611))
        v = 6 + _h(k, 1, 611) * 14
        t = f / 2.2
        x = cx + math.cos(a) * v * t
        y = cy + math.sin(a) * v * t + 1.4 * f * f
        if y > cy + 12:                                # ya en el suelo: mancha plana
            y = cy + 12 - (k % 3)
        if f >= 5 and k % 3 == 0:
            continue
        s.set(int(x), int(y), "wine" if k % 4 == 0 else SANGRE)
    for k in range(5):                                 # trozos (olive_shadow: nunca el valor de la horda)
        a = math.pi * (1.2 + .6 * k / 4)
        t = min(f, 4) / 2.5
        x = cx + math.cos(a) * 12 * t
        y = cy + math.sin(a) * 10 * t + 1.6 * min(f, 4) ** 2
        y = min(y, cy + 11)
        s.rect(int(x), int(y), 2, 2, "olive_shadow")
        s.set(int(x), int(y), SANGRE)
    if f >= 4:                                         # el charco empieza a formarse
        rr = 3 + (f - 4) * 1.8
        for y in range(int(cy + 10 - rr / 2), int(cy + 12 + rr / 2)):
            for x in range(int(cx - rr * 1.6), int(cx + rr * 1.6)):
                if math.hypot((x + .5 - cx) / 1.6, (y + .5 - cy - 11) * 1.6) <= rr / 1.6 + .5 + _h(x, y, 613):
                    s.set(x, y, SANGRE)
    return s


def restos_horda(v):
    """Decal fijo que se queda en el suelo donde cae un zombi. 6 variantes (giro y espejo al azar)."""
    s = Sprite(32, 32)
    cx, cy = 16 + (_h(v, 0, 621) - .5) * 4, 16 + (_h(v, 1, 621) - .5) * 4
    r = 6 + v % 3 * 2
    for y in range(32):                                # charco irregular
        for x in range(32):
            ang = math.atan2(y - cy, x - cx)
            lim = r * (0.75 + .5 * _h(int((ang + 4) * 3), v, 622))
            d = math.hypot(x + .5 - cx, (y + .5 - cy) * 1.3)
            if d <= lim:
                s.set(x, y, "wine" if d < lim * .35 and _h(x, y, 623) < .3 else SANGRE)
    for k in range(3 + v % 3):                         # regueros en una dirección
        a = _h(k, v, 624) * math.tau
        largo = 4 + int(_h(k, v, 625) * 8)
        for t in range(largo):
            s.set(int(cx + math.cos(a) * (r + t)), int(cy + math.sin(a) * (r + t) / 1.3), SANGRE)
    for k in range(10):                                # gotas sueltas alrededor
        a = _h(k, v, 626) * math.tau
        d = r + 3 + _h(k, v, 627) * 7
        s.set(int(cx + math.cos(a) * d), int(cy + math.sin(a) * d / 1.3), SANGRE)
    for k in range(2 + v % 2):                         # trozos y harapos
        x, y = int(cx - 4 + _h(k, v, 628) * 8), int(cy - 3 + _h(k, v, 629) * 6)
        s.rect(x, y, 3, 2, "olive_shadow")
        s.set(x + 1, y, "grey_brown")
    if v in (2, 5):                                    # un zapato o un jirón de ropa, para que sean personas
        x, y = int(cx + 3), int(cy - 5)
        s.rect(x, y, 4, 2, "brown_dark")
        s.set(x, y, "brown")
    return s


# --- Polvo de rotor -------------------------------------------------------------------------
def polvo_rotor(f):
    """Anillo de polvo que barre el suelo bajo el helicóptero (vista cenital, 9,6 m de diámetro).
    Bucle de 8: las vetas giran 45° en el bucle completo para que cierre sin salto."""
    s = Sprite(96, 96)
    cx = cy = 48
    for y in range(96):
        for x in range(96):
            d = math.hypot(x + .5 - cx, y + .5 - cy)
            if d < 18 or d > 46:
                continue
            a = math.atan2(y + .5 - cy, x + .5 - cx)
            veta = .5 + .5 * math.sin(a * 8 - f * math.pi / 4 + d * .25)   # espirales que giran
            banda = 1 - abs(d - 32) / 14
            dens = banda * (.25 + .6 * veta)
            if tramado(x, y, dens):
                s.set(x, y, "beige" if veta > .7 and d < 36 else "olive_light" if veta > .35 else "concrete_light")
    for k in range(10):                                # chinas y briznas lanzadas hacia fuera
        a = k * math.tau / 10 + f * math.pi / 16
        d = 38 + (f * 3 + k * 5) % 10
        s.set(int(cx + math.cos(a) * d), int(cy + math.sin(a) * d), "grey_brown")
    return s


EFECTOS = {
    #  nombre          (función, fotogramas, tamaño, bucle, pivote)
    "impacto_bala": (impacto_bala, 4, 32, False, (0.5, 0.62)),
    "granada": (granada, 10, 96, False, (0.5, 0.62)),
    "sangre_impacto": (sangre_impacto, 5, 32, False, (0.31, 0.5)),
    "sangre_muerte": (sangre_muerte, 7, 48, False, (0.5, 0.62)),
    "polvo_rotor": (polvo_rotor, 8, 96, True, (0.5, 0.5)),
}


def exportar(out):
    """Hojas PNG con JSON estilo Aseprite (meta.viaMuerta) y el atlas de restos. Devuelve los fotogramas."""
    out = Path(out)
    todo = {}
    for nombre, (fn, n, t, bucle, piv) in EFECTOS.items():
        frames = [fn(f) for f in range(n)]
        hoja = Image.new("RGBA", (t * n, t), (0, 0, 0, 0))
        for f, s in enumerate(frames):
            hoja.alpha_composite(s.img, (t * f, 0))
        base = f"vm_vfx_{nombre}"
        hoja.save(out / f"{base}.png")
        dur = round(1000 / FPS)
        meta = {"frames": [{"filename": f"{base} {f}", "frame": {"x": t * f, "y": 0, "w": t, "h": t},
                            "rotated": False, "trimmed": False, "spriteSourceSize": {"x": 0, "y": 0, "w": t, "h": t},
                            "sourceSize": {"w": t, "h": t}, "duration": dur} for f in range(n)],
                "meta": {"image": f"{base}.png", "size": {"w": t * n, "h": t}, "format": "RGBA8888",
                         "viaMuerta": {"fps": FPS, "loop": bucle, "frames": n, "frameSize": [t, t], "pivot": list(piv),
                                       "texel_m": 0.1, "capa": "VFX_HD", "origen": "pixelvm, build_vfx_combate.py"}}}
        (out / f"{base}.json").write_text(json.dumps(meta, indent=1))
        todo[nombre] = frames
    at = Image.new("RGBA", (6 * 32, 32), (0, 0, 0, 0))
    rects = {}
    for v in range(6):
        at.alpha_composite(restos_horda(v).img, (v * 32, 0))
        rects[f"restos_horda_{v + 1}"] = [v * 32, 0, 32, 32]
    at.save(out / "atlas_restos_horda.png")
    (out / "atlas_restos_horda.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "celda": [32, 32], "texel_m": 0.1, "rects": rects,
        "colocacion": "un decal donde cae cada zombi abatido, giro y espejo al azar; máx. ~400 vivos (los más viejos se apagan con tramado)"}}},
        indent=1, ensure_ascii=False))
    return todo


def revisar(frames):
    pal = set(v[:3] for v in PAL.values())
    av = set()
    for s in frames:
        for p in s.img.getdata():
            if p[3] not in (0, 255):
                av.add("alfa parcial")
            if p[3] and p[:3] not in pal:
                av.add("fuera de paleta")
            if p[3] and p[:3] in (PAL["red_bright"][:3], PAL["maroon"][:3], PAL["olive_black"][:3]):
                av.add("color reservado")
    return sorted(av) or "OK"


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    for n, fr in todo.items():
        print(n, len(fr), revisar(fr))
    print("restos", revisar([restos_horda(v) for v in range(6)]))
    # Hoja de contacto x3 sobre asfalto, tierra y noche, y GIF x4 de cada efecto.
    fondos = [PAL["concrete"], PAL["beige"], PAL["navy"]]
    filas = list(todo.items()) + [("restos_horda", [restos_horda(v) for v in range(6)])]
    E = 3
    anchos = [sum(fr[0].w * E + 4 for _ in fr) for _, fr in filas]
    W = max(anchos) * 1 + 8
    H = sum(fr[0].h * E + 4 for _, fr in filas) * len(fondos) + 8
    hoja = Image.new("RGBA", (W, H), PAL["navy"])
    y = 4
    for fondo in fondos:
        for _, fr in filas:
            x = 4
            for s in fr:
                c = Image.alpha_composite(Image.new("RGBA", (s.w, s.h), fondo), s.img).resize((s.w * E, s.h * E), Image.NEAREST)
                hoja.paste(c, (x, y))
                x += s.w * E + 4
            y += fr[0].h * E + 4
    hoja.save(OUT / "contacto_vfx_combate.png")
    for n, fr in todo.items():
        big = [Image.alpha_composite(Image.new("RGBA", (s.w, s.h), PAL["concrete"]), s.img).resize((s.w * 4, s.h * 4), Image.NEAREST) for s in fr]
        big[0].save(OUT / f"vm_vfx_{n}_x4.gif", save_all=True, append_images=big[1:], duration=round(2000 / FPS),
                    loop=0, disposal=2)
