"""VFX de vehículos y helicópteros en combate (noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:35). Lo que ya existe (D-N83, build_vfx_combate.py) no se repite:
  polvo_ruedas (arrancada del Dragón) y polvo_rotor (downwash del NH90, 96 px = 9,6 m). Para el Tigre se usa el
  mismo polvo_rotor a escala 0,8.
Lo nuevo:
  polvo_estela      32x32, 6 f, una vez   bocanada de polvo por rueda EN MARCHA (Lince, VAMTAC); Marcos la emite
                                         cada ~0,1 s por eje trasero según la velocidad y se queda en el mundo
  polvo_orugas      48x48, 6 f, una vez   lo mismo para el Pizarro: más grande, bajo y con terrones
  humo_danado_leve  32x48, 8 f, una vez   bocanada clara de motor tocado (una cada ~0,25 s)
  humo_danado_grave 32x48, 8 f, una vez   bocanada más oscura y gruesa de vehículo muy dañado (una cada ~0,15 s)
  fuego_vehiculo    32x32, bucle de 8     llamas sobre el vehículo ardiendo. SIN ámbar: se queda en pantalla
                                         (regla de Lucía); crema en el núcleo, beige, ocre y óxido
  explosion_vehiculo 128x128, 12 f, una vez destello, bola de fuego (ámbar transitorio), piezas y humo que sube
  decal_calcinado   64x64, 2 variantes    mancha quemada bajo el armazón destruido
Humo en grises del suelo (nada por debajo de L* 20, que es de la horda).
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import _h, blob, ruido, tramado  # noqa: E402

FPS = 12


def polvo_estela(f):
    """El vehículo va a +X: la bocanada nace a ras de suelo junto a la rueda, crece hacia atrás y se deshace."""
    s = Sprite(32, 32)
    t = f / 5
    dens = 1 - t * .85
    for k, (dx, dy, fr) in enumerate([(0, 0, 1), (-4, -2, .75), (-7, 1, .6)]):
        r = (2.5 + t * 7) * fr
        cx, cy = 20 + dx * (1 + t) - t * 4, 24 + dy - t * 4
        blob(s, cx, cy, r, "olive_light", dens)
        blob(s, cx - r * .3, cy - r * .35, r * .5, "beige", dens * .85)
    return s


def polvo_orugas(f):
    s = Sprite(48, 48)
    t = f / 5
    dens = 1 - t * .8
    for k, (dx, dy, fr) in enumerate([(0, 0, 1), (-6, -1, .85), (-12, 1, .7), (-17, -1, .55)]):
        r = (3.5 + t * 9) * fr
        cx, cy = 34 + dx * (1 + t * .8), 37 + dy - t * 5
        blob(s, cx, cy, r * 1.15, "olive_light", dens)
        blob(s, cx - r * .3, cy - r * .4, r * .5, "beige", dens * .8)
    if f < 4:                                      # terrones que despide la oruga
        for k in range(7):
            x = 36 - f * (3 + k) - _h(k, 0, 980) * 4
            y = 34 - f * 3 + f * f * .9 + (k % 3)
            s.set(int(x), int(y), "brown" if k % 2 else "grey_brown")
            if k % 3 == 0:
                s.set(int(x) + 1, int(y), "brown")
    return s


def _humo(f, claro):
    s = Sprite(32, 48)
    t = f / 7
    x, y = 16 - t * 3, 40 - t * 30
    r = (2.5 if claro else 3.5) + t * (6 if claro else 8)
    dens = 1 - t * .8
    joven, borde, viejo = (("olive_light", "olive_grey", "concrete") if claro
                           else ("grey_brown", "olive_shadow", "olive_grey"))
    for (dx, dy, fr) in ((0, 0, 1), (-r * .5, 2, .7), (r * .45, 1.5, .65)):
        if t < .45:
            blob(s, x + dx, y + dy, r * fr, joven, dens, borde=borde)
        else:
            blob(s, x + dx, y + dy, r * fr, viejo, dens * .85)
    return s


def humo_danado_leve(f):
    return _humo(f, True)


def humo_danado_grave(f):
    return _humo(f, False)


def fuego_vehiculo(f):
    """Llamas bajas y anchas (chapa ardiendo), periódicas en 8; sin ámbar."""
    s = Sprite(32, 32)
    base = 28
    for y in range(4, base + 1):
        for x in range(2, 30):
            dx = abs(x + .5 - 16) / 14
            alto = (base - y) / 22
            forma = (1 - dx * dx) - alto * 1.1
            n = ruido(x, y + f * 4, 8, 21, per=32) * .6 + ruido(x, y + f * 4, 4, 22, per=32) * .4
            v = forma + (n - .5) * .8
            if v <= .05:
                continue
            c = "cream" if v > .82 else "beige" if v > .58 else "ochre_dark" if v > .3 else "rust"
            s.set(x, y, c)
    for k in range(4):                             # pavesas
        y = int((base - 6 - _h(k, 0, 990) * 20 - f * 4) % 26) + 2
        x = int(8 + _h(k, 1, 990) * 16)
        if not s.get(x, y)[3]:
            s.set(x, y, "ochre_dark" if k % 2 else "beige")
    return s


def explosion_vehiculo(f):
    s = Sprite(128, 128)
    cx, cy = 64, 84
    if f == 0:
        blob(s, cx, cy, 14, "cream")
        for k in range(16):
            a = k * math.tau / 16
            for r in range(14, 26 + (k % 2) * 6):
                s.set(int(cx + math.cos(a) * r), int(cy + math.sin(a) * r * .8), "amber" if r < 20 else "ochre_dark")
        return s
    t = f / 11
    if f <= 5:                                     # bola de fuego que sube y se oscurece
        r = 12 + f * 4
        yy = cy - f * 3
        blob(s, cx, yy, r, "ochre_dark" if f > 3 else "amber", 1 - f * .1, borde="rust")
        blob(s, cx, yy - 2, r * .6, "amber" if f > 2 else "cream", 1 - f * .12)
    if f >= 2:                                     # humo gris que sube en columna gruesa
        tt = (f - 2) / 9
        for k, (dx, dy) in enumerate([(0, -6), (-10, -10), (10, -12), (-4, -24), (6, -34), (0, -46)]):
            blob(s, cx + dx * (1 + tt * .6), cy + dy * (.5 + tt) - tt * 10, (7 + k) * (.6 + tt * .9),
                 "grey_brown" if k % 2 else "olive_grey", (1 - tt * .75) * .95)
    for k in range(12):                            # piezas del vehículo (chapa oliva) y una rueda
        a = math.pi + _h(k, 0, 995) * math.pi
        v = 5 + _h(k, 1, 995) * 9
        x = cx + math.cos(a) * v * f
        y = cy + math.sin(a) * v * f * .9 + f * f * .55
        if 0 <= y < 126:
            c = "olive_dark" if k % 3 else "grey_brown"
            s.rect(int(x), int(y), 2, 2 if k % 2 else 1, c)
            if f < 5 and k % 4 == 0:
                s.set(int(x) - 1, int(y), "amber")
    if f < 10:
        x = cx + 4 * f
        y = cy - 14 * f * .6 + f * f * 1.1
        if y < 124:
            s.rect(int(x) - 2, int(y) - 2, 5, 5, "olive_shadow")      # rueda que sale volando
            s.set(int(x), int(y), "grey_brown")
    return s


def decal_calcinado(v):
    """Mancha quemada en planta: centro brown_dark tramado, aro de ceniza grey_brown y restos sueltos."""
    s = Sprite(64, 64)
    for y in range(64):
        for x in range(64):
            d = math.hypot((x + .5 - 32) / 28, (y + .5 - 32) / 22)
            d += (ruido(x, y, 8, 1000 + v) - .5) * .35
            if d < .55 and tramado(x, y, .9):
                s.set(x, y, "olive_shadow" if _h(x, y, 1002 + v) > .7 else "brown_dark")
            elif d < .85 and tramado(x, y, (.85 - d) * 2.2):
                s.set(x, y, "grey_brown")
    for k in range(14):
        x, y = int(10 + _h(k, v, 1003) * 44), int(12 + _h(k, v, 1004) * 40)
        s.set(x, y, "olive_grey" if k % 2 else "olive_dark")
    return s


EFECTOS = {
    #  nombre               (función, fotogramas, (w, h), bucle, pivote)
    "polvo_estela": (polvo_estela, 6, (32, 32), False, (0.62, 0.75)),
    "polvo_orugas": (polvo_orugas, 6, (48, 48), False, (0.7, 0.77)),
    "humo_danado_leve": (humo_danado_leve, 8, (32, 48), False, (0.5, 0.83)),
    "humo_danado_grave": (humo_danado_grave, 8, (32, 48), False, (0.5, 0.83)),
    "fuego_vehiculo": (fuego_vehiculo, 8, (32, 32), True, (0.5, 0.88)),
    "explosion_vehiculo": (explosion_vehiculo, 12, (128, 128), False, (0.5, 0.66)),
}


def exportar(out):
    out = Path(out)
    todo = {}
    for nombre, (fn, n, (w, h), bucle, piv) in EFECTOS.items():
        frames = [fn(f) for f in range(n)]
        hoja = Image.new("RGBA", (w * n, h), (0, 0, 0, 0))
        for f, s in enumerate(frames):
            hoja.alpha_composite(s.img, (w * f, 0))
        base = f"vm_vfx_{nombre}"
        hoja.save(out / f"{base}.png")
        meta = {"frames": [{"filename": f"{base} {f}", "frame": {"x": w * f, "y": 0, "w": w, "h": h},
                            "rotated": False, "trimmed": False, "spriteSourceSize": {"x": 0, "y": 0, "w": w, "h": h},
                            "sourceSize": {"w": w, "h": h}, "duration": round(1000 / FPS)} for f in range(n)],
                "meta": {"image": f"{base}.png", "size": {"w": w * n, "h": h}, "format": "RGBA8888",
                         "viaMuerta": {"fps": FPS, "loop": bucle, "frames": n, "frameSize": [w, h], "pivot": list(piv),
                                       "texel_m": 0.1, "blend": "alfa", "capa": "VFX_HD",
                                       "origen": "pixelvm, build_vfx_vehiculos.py"}}}
        (out / f"{base}.json").write_text(json.dumps(meta, indent=1))
        todo[nombre] = frames
    cal = [decal_calcinado(v) for v in range(2)]
    for v, s in enumerate(cal):
        s.img.save(out / f"decal_calcinado_{v + 1}_64.png")
    todo["decal_calcinado"] = cal
    return todo


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_vfx_vehiculos")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 3
    filas = list(todo.items())
    Wt = max(sum(s.w * E + 6 for s in fr) for _, fr in filas)
    Ht = sum(fr[0].h * E + 6 for _, fr in filas)
    hoja = Image.new("RGBA", (Wt, Ht), PAL["navy"])
    y = 0
    for n, fr in filas:
        x = 0
        for s in fr:
            fondo = Image.new("RGBA", (s.w, s.h), PAL["olive_dark"] if n == "decal_calcinado" else PAL["olive_shadow"])
            hoja.paste(Image.alpha_composite(fondo, s.img).resize((s.w * E, s.h * E), Image.NEAREST), (x, y))
            x += s.w * E + 6
        y += fr[0].h * E + 6
    hoja.save(OUT / "contacto_vfx_vehiculos_x3.png")
    print("ok")
