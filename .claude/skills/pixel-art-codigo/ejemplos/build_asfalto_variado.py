"""Asfalto sin repetición (Eugenio, 6-nov 19:15: «las grietas del asfalto me parecen muy repetitivas»).
Bruno (Arte técnico).

El problema: las grietas iban dentro de la tesela de 6,4 m y el ojo encuentra el patrón a la segunda
repetición. La solución separa lo que se puede repetir de lo que no:
  1. suelo_asfalto_liso_64: la tesela solo lleva el árido (#94948c en motas sueltas sobre #6c7874), que
     no tiene forma y no delata la repetición. 4 variantes para que el shader elija una por celda con hash,
     con giro de 90° y espejo.
  2. atlas_grietas_asfalto: 8 grietas sueltas de 64x64 (6,4 m) en #48403c, todas distintas (longitudinal,
     transversal, en estrella, piel de cocodrilo, de borde, bacheo de asfalto nuevo más oscuro (motas olive_grey, ≈ −4 L*) con borde continuo irregular y esquinas mordidas, diagonal y en Y), que Marcos coloca como
     quads planos dispersos con giro y espejo al azar, unas 1 cada 60–100 m².
Los colores no cambian respecto a la muestra aprobada (ΔL* 11,8 sin juntas).
"""
import json
import math
import random
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_a7_texturas import T, _h, motas  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_asfalto")
G = 64
GRIETA = "grey_brown"


def liso(v):
    s = Sprite(T, T)
    s.rect(0, 0, T, T, "concrete")
    motas(s, "concrete_light", 0.055 + 0.01 * (v % 2), 900 + v, dobles=0)
    return s


def _rama(s, x, y, ang, largo, seed, prof=0, quiebro=0.9, hijos=1):
    """Fisura quebrada: el rumbo zigzaguea alrededor de `ang` sin enroscarse (vuelve hacia el rumbo base)."""
    base, a = ang, ang
    for k in range(largo):
        a += (_h(k, prof, seed) - .5) * quiebro
        a = base + (a - base) * 0.7
        x, y = x + math.cos(a), y + math.sin(a)
        s.set(int(round(x)), int(round(y)), GRIETA)
        if prof < hijos and k in (largo // 3, 2 * largo // 3) and _h(k, prof, seed + 9) > .35:
            _rama(s, x, y, a + (1 if _h(k, 1, seed) > .5 else -1) * (0.7 + _h(k, 2, seed) * .5),
                  int(largo * .4), seed + k + 1, prof + 1, quiebro, 0)


def grieta(v, color=None):
    global GRIETA
    previo = GRIETA
    GRIETA = color or previo
    try:
        return _grieta(v)
    finally:
        GRIETA = previo


def _grieta(v):
    s = Sprite(G, G)
    if v == 0:                                           # longitudinal larga
        _rama(s, 1, 30, 0.08, 62, 910)
    elif v == 1:                                         # transversal corta con ramita
        _rama(s, 30, 6, 1.45, 38, 911)
    elif v == 2:                                         # estrella (impacto o asiento)
        for k in range(5):
            _rama(s, 32, 32, k * 1.26 + .3, 10 + int(_h(k, 0, 912) * 10), 912 + k, hijos=0)
    elif v == 3:                                         # piel de cocodrilo: malla de celdas de 4–6 px en una mancha
        pts = [(14 + _h(i, 0, 913) * 36, 16 + _h(i, 1, 913) * 32) for i in range(22)]
        for y in range(G):
            for x in range(G):
                if math.hypot(x - 32, (y - 32) * 1.25) > 16 + (_h(x // 4, y // 4, 915) - .5) * 4:
                    continue
                d = sorted(math.hypot(x + .5 - px, y + .5 - py) for px, py in pts)
                if d[1] - d[0] < 0.7:
                    s.set(x, y, GRIETA)
    elif v == 4:                                         # de borde: paralela al bordillo, ondulada
        _rama(s, 1, 8, 0.0, 62, 915, quiebro=.5, hijos=0)
        _rama(s, 24, 8, 1.3, 9, 916, hijos=0)
        _rama(s, 46, 9, 1.0, 7, 917, hijos=0)
    elif v == 5:                                         # bacheo: parche de asfalto nuevo, algo más oscuro, borde continuo irregular
        def onda(t, eje):                                # ruido suave: interpola entre valores cada 6 px
            i, f = int(t // 6), (t % 6) / 6
            return ((1 - f) * _h(i, eje, 918) + f * _h(i + 1, eje, 918) - .5) * 5

        def dentro(x, y):
            if not (15 + onda(y, 0) <= x <= 48 + onda(y, 1) and 20 + onda(x, 2) <= y <= 43 + onda(x, 3)):
                return False
            return math.hypot(x - 47, y - 21) >= 6 and math.hypot(x - 16, y - 42) >= 4   # esquinas mordidas
        m = {(x, y) for y in range(G) for x in range(G) if dentro(x, y)}
        for _ in range(2):                               # sin píxeles colgando: fuera lo que tenga < 2 vecinos
            m = {(x, y) for x, y in m
                 if sum((x + dx, y + dy) in m for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))) >= 2}
        for x, y in m:
            borde = any((x + dx, y + dy) not in m for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            # relleno: sin árido claro, con un 40 % de motas olive_grey (−10 L*) → ≈ −4 L* de media a x1
            c = GRIETA if borde else ("olive_grey" if _h(x, y, 919) < .4 else "concrete")
            s.set(x, y, c)
    elif v == 6:                                         # diagonal con ramas
        _rama(s, 4, 58, -0.78, 70, 918, hijos=1)
    else:                                                # en Y
        _rama(s, 32, 62, -1.57, 24, 919, hijos=0)
        _rama(s, 32, 38, -2.2, 24, 920, hijos=0)
        _rama(s, 32, 38, -0.95, 26, 921, hijos=0)
    return s


N_LISO, N_GRIETAS = 4, 8


def atlas():
    at = Image.new("RGBA", (4 * G, 2 * G), (0, 0, 0, 0))
    rects = {}
    for v in range(N_GRIETAS):
        x, y = (v % 4) * G, (v // 4) * G
        at.alpha_composite(grieta(v).img, (x, y))
        rects[f"grieta_asfalto_{v + 1}"] = [x, y, G, G]
    return at, rects


def _gira(im, r, espejo):
    im = im.rotate(90 * r, expand=False)
    return im.transpose(Image.FLIP_LEFT_RIGHT) if espejo else im


def calle(W=384, H=192, seed=7, antes=None):
    """Una calle de 38 × 19 m: teselas lisas elegidas por hash con giro y espejo, y grietas dispersas."""
    rnd = random.Random(seed)
    im = Image.new("RGBA", (W, H))
    if antes is not None:
        for x in range(0, W, T):
            for y in range(0, H, T):
                im.paste(antes.img, (x, y))
        return im
    lis = [liso(v).img for v in range(N_LISO)]
    for x in range(0, W, T):
        for y in range(0, H, T):
            h = _h(x // T, y // T, 930)
            im.paste(_gira(lis[int(h * N_LISO)], int(_h(x, y, 931) * 4), _h(x, y, 932) > .5), (x, y))
    gr = [grieta(v).img for v in range(N_GRIETAS)]
    area = (W / 10) * (H / 10)
    for _ in range(int(area / 60)):                      # 1 grieta cada 60 m² de media
        g = _gira(gr[rnd.randrange(N_GRIETAS)], rnd.randrange(4), rnd.random() > .5)
        im.alpha_composite(g, (rnd.randrange(-24, W - 40), rnd.randrange(-24, H - 40)))
    return im


def exportar(out):
    """Piezas para Unity: 4 teselas lisas, atlas de grietas de asfalto y de tierra seca, con su JSON."""
    out = Path(out)
    for v in range(N_LISO):
        liso(v).save(out / f"suelo_asfalto_liso_{v + 1}_64.png")
    at, rects = atlas()
    at.save(out / "atlas_grietas_asfalto.png")
    (out / "atlas_grietas_asfalto.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "celda": [G, G], "texel_m": 0.1, "rects": rects,
        "colocacion": "quads planos dispersos, giro de 90° y espejo al azar, 1 cada 60–100 m²; nunca alineados a la tesela",
        "base": "suelo_asfalto_liso_1..4_64: una por celda de 6,4 m con hash, giro de 90° y espejo"}}},
        indent=1, ensure_ascii=False))
    # Grietas de tierra seca: las mismas formas en #88885c (4 variantes), para el descampado.
    at2 = Image.new("RGBA", (4 * G, G), (0, 0, 0, 0))
    r2 = {}
    for i, v in enumerate((3, 2, 7, 0)):
        at2.alpha_composite(grieta(v, "olive_light").img, (i * G, 0))
        r2[f"grieta_tierra_{i + 1}"] = [i * G, 0, G, G]
    at2.save(out / "atlas_grietas_tierra.png")
    (out / "atlas_grietas_tierra.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "celda": [G, G], "texel_m": 0.1, "rects": r2,
        "colocacion": "como las de asfalto, sobre suelo_seca: 1 cada 40–80 m², giro y espejo al azar"}}}, indent=1, ensure_ascii=False))


if __name__ == "__main__":
    import build_suelo_sagunto as bs
    OUT.mkdir(parents=True, exist_ok=True)
    exportar(OUT)
    at = atlas()[0]
    at.resize((at.width * 3, at.height * 3), Image.NEAREST).save(OUT / "atlas_grietas_asfalto_x3.png")
    sec = Image.new("RGBA", (384, 192))
    st = bs.seca().img
    for x in range(0, 384, T):
        for y in range(0, 192, T):
            sec.paste(st, (x, y))
    rnd = random.Random(11)
    g2 = [grieta(v, "olive_light").img for v in (3, 2, 7, 0)]
    for _ in range(14):
        sec.alpha_composite(_gira(g2[rnd.randrange(4)], rnd.randrange(4), rnd.random() > .5),
                            (rnd.randrange(-24, 344), rnd.randrange(-24, 152)))
    sec.resize((768, 384), Image.NEAREST).save(OUT / "tierra_seca_con_grietas_x2.png")
    a = calle(antes=bs.asfalto())
    d = calle()
    W, H = a.size
    hoja = Image.new("RGBA", (W * 2 + 24, H * 2 * 2 + 24), PAL["navy"])
    hoja.paste(a.resize((W * 2, H * 2), Image.NEAREST), (8, 8))
    hoja.paste(d.resize((W * 2, H * 2), Image.NEAREST), (8, 16 + H * 2))
    hoja.save(OUT / "asfalto_antes_despues_x2.png")
    x1 = Image.new("RGBA", (W, H * 2 + 8), PAL["navy"])
    x1.paste(a, (0, 0))
    x1.paste(d, (0, H + 8))
    x1.save(OUT / "asfalto_antes_despues_x1.png")
    print("ok")
