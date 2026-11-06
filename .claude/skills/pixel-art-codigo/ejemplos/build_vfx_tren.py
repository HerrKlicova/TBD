"""VFX de la vista cercana del tren en marcha (noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (00:26). Cámara según Marcos: ortográfica 3/4, yaw ≈ 90° (de lado a la vía), pitch 30–35°,
ortho 7 a 1080p (≈ 13 px/m), sigue al tren, que va a +X a 20 m/s. Vía, traviesas y postes son geometría real,
así que no hay bucle de UV. Locomotora diésel: no hay pantógrafo; en su lugar va el humo del escape.
  chispas_rueda        32x32, bucle de 4   chispas en el contacto de la rueda con el carril (frenada y curvas)
  humo_diesel          24x48, 8 f, una vez UNA bocanada suelta; Marcos las emite en el mundo y se quedan atrás
  destello_faro        32x32, bucle de 2   destello ADITIVO en el mundo sobre el faro (la luz es un spot real)
  luz_ventanilla       16x8, bucle de 2    resplandor de una ventanilla, ADITIVO; el f 1 es el parpadeo de un fluorescente
  polvo_hojas          48x48, 8 f, una vez remolino de polvo y hojas que levanta el tren al pasar (espacio de mundo)
Decals junto a la vía (repiten en X, 64x16 a 0,1 m/texel = 6,4 m): decal_via_balasto_1..3 y decal_via_hierba_1..3.
Paleta: solo la subpaleta del suelo en los decals; nada de #404460 ni azules.
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

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_vfx_tren")
FPS = 12


def chispas_rueda(f):
    """Chispas que salen hacia atrás y abajo desde el contacto (centro, 16,20). El tren va a la derecha."""
    s = Sprite(32, 32)
    cx, cy = 16, 20
    s.rect(cx - 1, cy - 1, 2, 2, "cream")
    for k in range(9):
        a = math.pi * (0.85 + .35 * _h(k, f, 801))     # hacia atrás (izquierda) y algo abajo
        largo = 3 + int(_h(k, f, 802) * 9)
        for t in range(largo):
            x = cx + math.cos(a) * t
            y = cy - math.sin(a) * t * .4 + t * t * .04
            c = "cream" if t < 2 else "amber" if t < largo * .7 else "ochre_dark"
            if t >= largo - 1 and (k + f) % 2:
                continue
            s.set(int(x), int(y), c)
    return s


def humo_diesel(f):
    """UNA bocanada suelta (D-N84, Lucía): nace en el escape (12, 36), crece, se aplana y se deshilacha.
    No es una columna hecha: Marcos la emite en espacio del mundo cada ~0,15 s y, como el tren va a +X a 20 m/s,
    las bocanadas se quedan atrás y se tienden en horizontal solas. Joven en olive_grey con borde grey_brown."""
    s = Sprite(24, 48)
    t = f / 7
    x = 12 - t * 2
    y = 36 - t * 10
    r = 2.5 + t * 7
    dens = 1 - t * .8
    for (dx, dy, fr) in ((0, 0, 1), (-r * .6, 1, .7), (r * .5, 1.5, .6)):    # tres lóbulos que se separan
        if t < .4:
            blob(s, x + dx, y + dy * t * 3, r * fr, "olive_grey", dens, borde="grey_brown")
        else:
            blob(s, x + dx, y + dy * t * 3, r * fr, "grey_brown", dens * .85)
            blob(s, x + dx + 1, y + dy * t * 3 - 1, r * fr * .5, "olive_grey", dens * .6)
    return s


def destello_faro(f):
    """Destello aditivo sobre el faro, centrado: núcleo crema, halo en tres escalones con tramado y una raya
    horizontal corta. El f 1 baja un poco el halo para que respire."""
    s = Sprite(32, 32)
    cx, cy = 16, 16
    k = .85 if f else 1
    for y in range(32):
        for x in range(32):
            d = math.hypot(x + .5 - cx, (y + .5 - cy) * 1.3)
            v = max(0, 1 - d / (14 * k))
            raya = abs(y + .5 - cy) < 1 and abs(x + .5 - cx) < 15 * k
            if d < 2.5:
                c = "cream"
            elif v > .6 or (raya and abs(x + .5 - cx) < 8):
                c = "beige"
            elif v > .35 or raya:
                c = "ochre_dark" if tramado(x, y, .8) else None
            elif v > .1:
                c = "brown_dark" if tramado(x, y, .35) else None
            else:
                c = None
            if c:
                s.set(x, y, c)
    return s


def luz_ventanilla(f):
    """Resplandor de ventanilla (aditivo): núcleo crema y halo ocre de 1 px; f 1 baja medio escalón."""
    s = Sprite(16, 8)
    s.rect(2, 1, 12, 6, "ochre_dark" if f else "beige")
    s.rect(3, 2, 10, 4, "beige" if f else "cream")
    for x, y in ((2, 1), (13, 1), (2, 6), (13, 6)):
        s.set(x, y, None)
    for x in range(1, 15):                             # halo de 1 px con tramado
        for y in (0, 7):
            if tramado(x, y, .4):
                s.set(x, y, "brown_dark")
    return s


def polvo_hojas(f):
    """El paso del tren levanta polvo y hojas secas que giran y caen detrás (el tren va a la derecha)."""
    s = Sprite(48, 48)
    t = f / 7
    dens = [.9, .85, .75, .6, .45, .3, .18, .08][f]
    for k, (dx, dy, fr) in enumerate([(0, 0, 1), (-7, -3, .8), (6, -4, .7), (-12, 2, .6)]):
        r = (5 + t * 9) * fr
        blob(s, 30 + dx * (1 + t) - t * 10, 36 + dy - t * 8, r, "olive_light", dens)
        blob(s, 28 + dx * (1 + t) - t * 10, 34 + dy - t * 8, r * .5, "beige", dens * .8)
    for k in range(7):                                 # hojas: 2 px que giran (alternan horizontal y vertical)
        a = _h(k, 0, 821) * math.tau + f * .9
        x = 26 + math.cos(a) * (6 + t * 14) - t * 12 + k
        y = 30 - math.sin(a) * 6 - 10 * t + t * t * 24 + (k % 3)
        c = "ochre_dark" if k % 2 else "olive_yellow"
        if (f + k) % 2:
            s.set(int(x), int(y), c)
            s.set(int(x) + 1, int(y), c)
        else:
            s.set(int(x), int(y), c)
            s.set(int(x), int(y) + 1, c)
    return s


def _set_rep(s, x, y, c):
    s.set(x % s.w, y, c)                                 # repite en X: lo que sale por un lado entra por el otro


def decal_via_balasto(v):
    """Borde del balasto que se derrama sobre la tierra: piedras densas arriba (lado de la vía) que se aclaran
    hacia abajo. 64x16, repite en X. Piedra de 1–2 px en olive_grey/grey_brown con brillo concrete."""
    s = Sprite(64, 16)
    for y in range(16):
        for x in range(64):
            dens = max(0, 1 - y / 13) ** 1.3
            h = _h(x, y, 840 + v)
            if h < dens * .55:
                c = "olive_grey" if h < dens * .3 else "grey_brown"
                _set_rep(s, x, y, c)
                if _h(x, y, 850 + v) > .8 and y > 0:
                    _set_rep(s, x - 1, y - 1, "concrete")
            elif h < dens * .7:
                _set_rep(s, x, y, "brown")
    return s


def decal_via_hierba(v):
    """Matas de hierba seca junto a la vía: tallos de 2–5 px desde la base (y=15), olive y olive_dark,
    puntas olive_yellow. 64x16, repite en X; el fondo es transparente."""
    s = Sprite(64, 16)
    n = 10 + v * 3
    for m in range(n):
        x0 = int(_h(m, v, 860) * 64)
        alto = 6 + int(_h(m, v, 861) * 8)
        for k in range(5 + int(_h(m, v, 862) * 5)):
            h = alto - int(_h(m, k, 863 + v) * 4)
            inc = (_h(m, k, 864 + v) - .5) * 1.2
            for t in range(h):
                c = "olive_dark" if t < h * .4 else "olive" if t < h - 1 else "olive_yellow"
                _set_rep(s, int(x0 + k - 2 + inc * t), 15 - t, c)
    return s


EFECTOS = {
    #  nombre             (función, fotogramas, (w, h), bucle, pivote, aditivo)
    "chispas_rueda": (chispas_rueda, 4, (32, 32), True, (0.5, 0.62), False),
    "humo_diesel": (humo_diesel, 8, (24, 48), False, (0.5, 0.75), False),
    "destello_faro": (destello_faro, 2, (32, 32), True, (0.5, 0.5), True),
    "luz_ventanilla": (luz_ventanilla, 2, (16, 8), True, (0.5, 0.5), True),
    "polvo_hojas": (polvo_hojas, 8, (48, 48), False, (0.6, 0.75), False),
}


def exportar(out):
    out = Path(out)
    todo = {}
    for nombre, (fn, n, (w, h), bucle, piv, adit) in EFECTOS.items():
        frames = [fn(f) for f in range(n)]
        hoja = Image.new("RGBA", (w * n, h), (0, 0, 0, 0))
        for f, s in enumerate(frames):
            hoja.alpha_composite(s.img, (w * f, 0))
        base = f"vm_vfx_{nombre}"
        hoja.save(out / f"{base}.png")
        dur = 400 if nombre in ("luz_ventanilla", "destello_faro") else round(1000 / FPS)
        meta = {"frames": [{"filename": f"{base} {f}", "frame": {"x": w * f, "y": 0, "w": w, "h": h},
                            "rotated": False, "trimmed": False, "spriteSourceSize": {"x": 0, "y": 0, "w": w, "h": h},
                            "sourceSize": {"w": w, "h": h}, "duration": dur} for f in range(n)],
                "meta": {"image": f"{base}.png", "size": {"w": w * n, "h": h}, "format": "RGBA8888",
                         "viaMuerta": {"fps": FPS, "loop": bucle, "frames": n, "frameSize": [w, h], "pivot": list(piv),
                                       "texel_m": 0.1, "blend": "aditivo" if adit else "alfa", "capa": "VFX_HD",
                                       "origen": "pixelvm, build_vfx_tren.py"}}}
        (out / f"{base}.json").write_text(json.dumps(meta, indent=1))
        todo[nombre] = frames
    for nombre, fn in (("balasto", decal_via_balasto), ("hierba", decal_via_hierba)):
        vs = [fn(v) for v in range(3)]
        for v, s in enumerate(vs):
            s.img.save(out / f"decal_via_{nombre}_{v + 1}_64x16.png")
        todo[f"decal_{nombre}"] = vs
    return todo


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 4
    filas = list(todo.items())
    Wt = max(sum(s.w * E + 6 for s in fr) for _, fr in filas)
    Ht = sum(fr[0].h * E + 6 for _, fr in filas)
    hoja = Image.new("RGBA", (Wt, Ht), PAL["navy"])
    y = 0
    for n, fr in filas:
        x = 0
        adit = EFECTOS[n][5] if n in EFECTOS else False
        for s in fr:
            fondo = Image.new("RGBA", (s.w, s.h), PAL["black"] if adit else PAL["olive_shadow"])
            c = Image.alpha_composite(fondo, s.img).resize((s.w * E, s.h * E), Image.NEAREST)
            hoja.paste(c, (x, y))
            x += s.w * E + 6
        y += fr[0].h * E + 6
    hoja.save(OUT / "contacto_vfx_tren_x4.png")
    print("ok")
