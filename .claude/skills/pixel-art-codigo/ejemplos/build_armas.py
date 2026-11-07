"""Iconos del HUD y VFX de disparo e impacto de las armas del ET (noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:32). Referencias: lote militar de Paula (referencias-reales/militar/, M3 G36E, M4 MG4E,
M7 G28, y después M5 G36 con AG36 y M6 C90, que ya están verificados contra foto).
Iconos: 48x16, mirando a la derecha, en hueso con sombra gris y contorno negro para leerse sobre el panel.
Ningún color de icono es ámbar ni rojo (se quedan en pantalla). Los VFX pueden usar ámbar: duran ≤ 0,25 s
(regla de Lucía), salvo el humo, que va en grises del suelo.

  icono_g36e, icono_ag36, icono_mg4e, icono_g28, icono_c90, icono_30mm   48x16
  fogonazo_g36e    24x24, 2 f, ráfaga     estrella corta de 5,56 con bocacha cerrada
  fogonazo_mg4e    24x24, 2 f, ráfaga     lengua más larga que el fusil (cañón corto de ametralladora ligera)
  fogonazo_g28     32x32, 3 f, una vez    7,62: más grande, tres chorros del apagallamas y anillo de humo
  impacto_g28      32x32, 5 f, una vez    impacto pesado contra suelo o pared: polvo y lascas (en la horda, sangre_impacto)
  disparo_ag36     24x24, 4 f, una vez    «plof» del lanzagranadas: destello pequeño y bocanada gris
  proyectil_40mm   16x16, bucle de 2      granada en vuelo (sin trazador): cuerpo gris y estela tenue
  rebufo_c90       64x32, 5 f, una vez    rebufo hacia atrás (izquierda), destello delante y polvo levantado
  cohete_c90       32x16, bucle de 2      cohete en vuelo con llama del motor
  humo_cohete      16x16, 6 f, una vez    bocanada de la estela; Marcos la emite cada ~0,05 s detrás del cohete
  explosion_c90    96x96, 9 f, una vez    carga hueca: destello duro, chorro hacia delante y humo
El G36E y la MG4E usan impacto_bala; el AG36 usa granada; el 30 mm ya tiene fogonazo, trazadora e impactos.
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

FPS = 12
CUERPO, SOMBRA, LUZ, NEGRO = "bone", "grey_light", "cream", "black"


# --- Iconos ---------------------------------------------------------------------------------
def _fin(s):
    """Sombra en la mitad de abajo de cada pieza, brillo en el canto de arriba y contorno negro."""
    w, h = s.w, s.h
    px = s.img.load()
    llenos = {(x, y) for x in range(w) for y in range(h) if px[x, y][3]}
    for x, y in llenos:
        if (x, y + 1) not in llenos or (x, y + 2) not in llenos and (x, y - 1) in llenos:
            s.set(x, y, SOMBRA)
        if (x, y - 1) not in llenos and (x, y + 1) in llenos:
            s.set(x, y, LUZ)
    s.outline(NEGRO)
    return s


def _g36(s, dx=0):
    s.rect(14 + dx, 6, 18, 3, CUERPO)            # cajón
    s.rect(16 + dx, 3, 13, 2, CUERPO)            # asa de transporte con el visor
    s.rect(17 + dx, 2, 4, 1, CUERPO)             # visor
    s.vline(16 + dx, 4, 5, CUERPO); s.vline(28 + dx, 4, 5, CUERPO)
    s.rect(32 + dx, 6, 5, 2, CUERPO)             # guardamanos
    s.rect(37 + dx, 7, 7, 1, CUERPO)             # cañón
    s.rect(44 + dx, 6, 2, 3, CUERPO)             # bocacha
    s.poly([(20 + dx, 9), (24 + dx, 9), (26 + dx, 13), (22 + dx, 13)], CUERPO)   # cargador curvo
    s.poly([(15 + dx, 9), (18 + dx, 9), (17 + dx, 13), (14 + dx, 13)], CUERPO)   # pistolete
    s.rect(3 + dx, 6, 11, 2, CUERPO)             # culata plegable (esqueleto)
    s.rect(3 + dx, 8, 2, 4, CUERPO)
    s.rect(3 + dx, 11, 7, 1, CUERPO)
    s.line(9 + dx, 11, 13 + dx, 8, CUERPO)


def icono_g36e():
    s = Sprite(48, 16)
    _g36(s)
    return _fin(s)


def icono_ag36():
    """G36E con el AG36 debajo (M5_g36_ag36_1 y _2 de Paula): tubo largo bajo el guardamanos que llega casi a la
    boca, y la empuñadura y el gatillo propios del lanzagranadas delante del cargador."""
    s = Sprite(48, 16)
    _g36(s, dx=-2)
    s.rect(27, 9, 16, 3, CUERPO)                 # tubo del AG36
    s.rect(42, 9, 2, 3, CUERPO)                  # boca del tubo
    s.poly([(27, 12), (30, 12), (29, 15), (26, 15)], CUERPO)   # empuñadura del AG36
    return _fin(s)


def icono_mg4e():
    """MG4E: cajón largo con tapa, asa, caja de cinta debajo, bípode abierto y cañón largo."""
    s = Sprite(48, 16)
    s.rect(12, 5, 20, 3, CUERPO)                 # cajón y tapa
    s.rect(14, 3, 10, 2, CUERPO)                 # tapa con mira
    s.rect(26, 2, 6, 1, CUERPO)                  # asa de transporte
    s.vline(28, 3, 4, CUERPO)
    s.rect(32, 6, 13, 2, CUERPO)                 # cañón
    s.rect(45, 5, 2, 4, CUERPO)                  # apagallamas
    s.line(39, 8, 36, 13, CUERPO); s.line(40, 8, 43, 13, CUERPO)   # bípode abierto (la seña de la MG4E, D-N86)
    s.rect(21, 8, 2, 2, CUERPO)                  # boca de alimentación
    s.rect(17, 10, 10, 4, CUERPO)                # caja de cinta de 200, separada del cajón
    s.poly([(12, 8), (14, 8), (13, 13), (11, 13)], CUERPO)   # pistolete
    s.rect(2, 5, 10, 3, CUERPO)                  # culata
    s.rect(2, 8, 3, 3, CUERPO)
    return _fin(s)


def icono_g28():
    """G28 (M7_g28_3): cañón largo, visor grande y culata regulable de color arena. Sin bípode (D-N86)."""
    s = Sprite(48, 16)
    s.rect(12, 6, 16, 3, CUERPO)                 # cajón
    s.rect(28, 6, 8, 2, CUERPO)                  # guardamanos largo
    s.rect(36, 7, 9, 1, CUERPO)                  # cañón
    s.rect(45, 6, 2, 3, CUERPO)                  # apagallamas
    s.rect(13, 2, 15, 2, CUERPO)                 # visor
    s.rect(11, 1, 3, 4, CUERPO); s.rect(27, 1, 3, 4, CUERPO)   # campanas del visor
    s.rect(18, 4, 2, 2, CUERPO); s.rect(23, 4, 2, 2, CUERPO)   # anillas
    s.rect(18, 9, 4, 4, CUERPO)                  # cargador recto de 7,62
    s.poly([(13, 9), (16, 9), (15, 13), (12, 13)], CUERPO)   # pistolete
    s.rect(1, 5, 11, 3, CUERPO)                  # culata con carrillera
    s.rect(1, 8, 3, 4, CUERPO)
    s.rect(4, 4, 6, 1, CUERPO)
    s = _fin(s)
    for x in range(2, 11):                      # culata arena como en la foto (tan, nunca ámbar)
        for y in range(4, 12):
            if s.get(x, y) in (PAL[CUERPO], PAL[LUZ]):
                s.set(x, y, "beige")
    return s


def icono_c90():
    """C90 (M6_c90_1 y _2 de Paula): tubo recto sin abocinar, aros finos en los extremos, bloque de mira alto
    hacia delante con su ocular y empuñadura plegable debajo."""
    s = Sprite(48, 16)
    s.rect(3, 7, 41, 4, CUERPO)                  # tubo recto
    s.rect(2, 6, 2, 6, CUERPO)                   # aro trasero
    s.rect(43, 6, 2, 6, CUERPO)                  # aro delantero
    s.rect(28, 2, 4, 5, CUERPO)                  # bloque de mira
    s.rect(26, 2, 2, 2, CUERPO)                  # ocular hacia atrás
    s.poly([(20, 11), (23, 11), (22, 15), (19, 15)], CUERPO)   # empuñadura
    s = _fin(s)
    for x in (16, 33):                           # bandas oscuras del tubo
        s.vline(x, 7, 10, SOMBRA)
    return s


def icono_30mm():
    """Cartucho de 30 mm del cañón del Dragón, tumbado: vaina con culote y proyectil con banda."""
    s = Sprite(48, 16)
    s.rect(6, 4, 22, 8, CUERPO)                  # vaina
    s.rect(4, 3, 3, 10, CUERPO)                  # culote
    s.poly([(28, 4), (36, 5), (42, 7), (44, 8), (42, 9), (36, 11), (28, 12)], CUERPO)   # proyectil
    s = _fin(s)
    s.vline(30, 5, 11, "tan")                    # banda de forzamiento
    s.vline(31, 5, 11, "tan")
    return s


ICONOS = {"g36e": icono_g36e, "ag36": icono_ag36, "mg4e": icono_mg4e, "g28": icono_g28, "c90": icono_c90,
          "30mm": icono_30mm}


# --- VFX ------------------------------------------------------------------------------------
def _lengua(s, ox, oy, largo, ancho, f2=False):
    for x in range(largo):
        t = x / largo
        semi = ancho * math.sin(math.pi * min(1, t * 1.6)) * (1 - t * .5)
        for y in range(int(oy - semi), int(oy + semi) + 1):
            v = (1 - t) * (1 - abs(y + .5 - oy) / max(semi, .5) * .7)
            c = "cream" if v > .55 else "amber" if v > .28 else "ochre_dark"
            if f2:
                c = {"cream": "amber", "amber": "ochre_dark", "ochre_dark": None}[c]
            if c:
                s.set(ox + x, y, c)


def fogonazo_g36e(f):
    s = Sprite(24, 24)
    ox, oy = 3, 12
    _lengua(s, ox, oy, 9 if f == 0 else 6, 2.5)
    for k in range(1, 4 if f == 0 else 2):       # pétalos de la bocacha
        s.set(ox + 1, oy - k - 1, "amber"); s.set(ox + 1, oy + k + 1, "amber")
    s.set(ox, oy, "cream")
    return s


def fogonazo_mg4e(f):
    s = Sprite(24, 24)
    ox, oy = 2, 12
    _lengua(s, ox, oy, 15 if f == 0 else 10, 3 if f == 0 else 2)
    for d in (-1, 1):
        for k in range(1, 5 if f == 0 else 3):
            s.set(ox + 2 + k // 2, oy + d * (k + 1), "amber" if k < 3 else "ochre_dark")
    return s


def fogonazo_g28(f):
    s = Sprite(32, 32)
    ox, oy = 3, 16
    if f < 2:
        _lengua(s, ox, oy, 18 if f == 0 else 12, 3.5 if f == 0 else 2.5, f2=f == 1)
        for a in (-.9, .9):                       # chorros laterales del apagallamas
            for r in range(2, 9 if f == 0 else 6):
                s.set(int(ox + 1 + math.cos(a) * r), int(oy + math.sin(a) * r), "cream" if r < 4 else "amber")
    else:
        blob(s, ox + 8, oy - 1, 6, "concrete", .5)
        blob(s, ox + 3, oy, 3, "olive_grey", .5)
    return s


def impacto_g28(f):
    s = Sprite(32, 32)
    cx, cy = 16, 22
    if f == 0:
        blob(s, cx, cy, 2.5, "cream")
        for k in range(6):
            a = math.pi * (1.1 + .8 * _h(k, 0, 950))
            for r in range(2, 6):
                s.set(int(cx + math.cos(a) * r), int(cy + math.sin(a) * r), "amber")
    t = f / 4
    for k in range(9):                            # lascas y piedrecitas que saltan
        a = math.pi * (1.1 + .8 * _h(k, 1, 951))
        v = 3 + _h(k, 2, 951) * 8
        x = cx + math.cos(a) * v * t * 2
        y = cy + math.sin(a) * v * t * 2 + t * t * 14
        if f and y < 32:
            s.set(int(x), int(y), "grey_brown" if k % 2 else "olive_grey")
    if f:
        blob(s, cx, cy - t * 6, 3 + t * 6, "beige", (1 - t) * .8)
        blob(s, cx + 1, cy - t * 6 + 1, 2 + t * 4, "olive_light", (1 - t) * .7)
    return s


def disparo_ag36(f):
    s = Sprite(24, 24)
    ox, oy = 3, 12
    if f == 0:
        blob(s, ox + 3, oy, 3, "cream")
        blob(s, ox + 3, oy, 4.5, "amber", .5)
    else:
        t = f / 3
        blob(s, ox + 4 + t * 6, oy - t * 3, 3 + t * 4, "concrete", 1 - t * .7, borde="olive_grey")
    return s


def proyectil_40mm(f):
    s = Sprite(16, 16)
    s.rect(9, 7, 4, 3, "grey_brown")             # granada de 40 mm (sin trazador)
    s.set(13, 8, "olive_grey")
    s.hline(9, 12, 7, "concrete")
    for x in range(2, 9):                        # estela tenue
        if (x + f) % 2 and tramado(x, 8, .6 - (8 - x) * .07):
            s.set(x, 8, "concrete_light" if x > 5 else "concrete")
    return s


def rebufo_c90(f):
    """El tirador está en x≈40 mirando a la derecha; el rebufo sale hacia la izquierda en cono."""
    s = Sprite(64, 32)
    ox, oy = 40, 16
    if f == 0:
        blob(s, ox + 4, oy, 3, "cream")           # destello en la boca
        for x in range(ox - 20, ox):
            t = (ox - x) / 20
            semi = 2 + t * 6
            for y in range(int(oy - semi), int(oy + semi) + 1):
                v = (1 - t) * (1 - abs(y + .5 - oy) / semi * .6)
                s.set(x, y, "cream" if v > .6 else "amber" if v > .3 else "ochre_dark")
    else:
        t = f / 4
        for k, (dx, dy) in enumerate([(-8, 0), (-16, -2), (-24, 1), (-32, -1)]):
            r = (3 + k * 1.5) * (1 + t * .8)
            blob(s, ox + dx * (1 + t * .5), oy + dy - t * 3, r, "concrete" if k < 2 else "olive_light",
                 (1 - t * .75) * (1 - k * .12), borde="olive_grey" if f == 1 else None)
        for k in range(10):                        # polvo levantado del suelo
            x = ox - 6 - _h(k, 0, 960) * 34 * (1 + t)
            y = 27 - t * 6 * _h(k, 1, 960)
            if tramado(int(x), int(y), 1 - t):
                s.set(int(x), int(y), "beige")
    return s


def cohete_c90(f):
    s = Sprite(32, 16)
    s.rect(14, 7, 12, 2, "grey_brown")           # cuerpo
    s.rect(26, 7, 2, 2, "olive_grey"); s.set(28, 7, "olive_grey")
    s.set(14, 6, "grey_brown"); s.set(14, 9, "grey_brown")   # aletas
    for x in range(6 if f == 0 else 8, 14):     # llama del motor
        t = (14 - x) / 8
        s.set(x, 7, "cream" if t < .4 else "amber"); s.set(x, 8, "amber" if t < .6 else "ochre_dark")
    return s


def humo_cohete(f):
    s = Sprite(16, 16)
    t = f / 5
    blob(s, 8, 8 - t * 2, 2 + t * 4, "concrete_light" if f < 2 else "concrete", 1 - t * .8)
    return s


def explosion_c90(f):
    """Carga hueca: destello duro, chorro hacia delante (derecha), bola de fuego corta y humo gris."""
    s = Sprite(96, 96)
    cx, cy = 40, 60
    if f == 0:
        blob(s, cx, cy, 10, "cream")
        for k in range(12):
            a = k * math.tau / 12
            for r in range(10, 20):
                s.set(int(cx + math.cos(a) * r), int(cy + math.sin(a) * r), "amber" if r < 16 else "ochre_dark")
        return s
    if f <= 3:
        largo = 50 - f * 8
        for x in range(largo):                     # chorro de la carga
            t = x / largo
            for y in range(int(cy - 2 * (1 - t)), int(cy + 2 * (1 - t)) + 1):
                s.set(cx + x, y, "cream" if t < .5 else "amber")
        r = 9 + f * 3
        blob(s, cx, cy, r, "amber", 1 - f * .15, borde="ochre_dark")
        blob(s, cx, cy, r * .55, "cream" if f == 1 else "amber")
    t = max(0, (f - 2) / 6)
    if f >= 2:
        for k, (dx, dy) in enumerate([(0, 0), (-8, -4), (8, -6), (0, -12), (-4, -20)]):
            blob(s, cx + dx * (1 + t), cy + dy - t * 22, (6 + k) * (1 + t * .9),
                 "grey_brown" if k % 2 else "olive_grey", (1 - t * .8) * .9)
    for k in range(14):                            # metralla y cascotes
        a = _h(k, 0, 970) * math.pi + math.pi
        v = 6 + _h(k, 1, 970) * 18
        x = cx + math.cos(a) * v * f * .8
        y = cy + math.sin(a) * v * f * .6 + f * f * .9
        if 0 < y < 96 and f > 0:
            s.set(int(x), int(y), "amber" if f < 3 else "grey_brown")
    return s


EFECTOS = {
    #  nombre             (función, fotogramas, (w, h), bucle, pivote)
    "fogonazo_g36e": (fogonazo_g36e, 2, (24, 24), False, (0.125, 0.5)),
    "fogonazo_mg4e": (fogonazo_mg4e, 2, (24, 24), False, (0.08, 0.5)),
    "fogonazo_g28": (fogonazo_g28, 3, (32, 32), False, (0.1, 0.5)),
    "impacto_g28": (impacto_g28, 5, (32, 32), False, (0.5, 0.69)),
    "disparo_ag36": (disparo_ag36, 4, (24, 24), False, (0.125, 0.5)),
    "proyectil_40mm": (proyectil_40mm, 2, (16, 16), True, (0.7, 0.5)),
    "rebufo_c90": (rebufo_c90, 5, (64, 32), False, (0.625, 0.5)),
    "cohete_c90": (cohete_c90, 2, (32, 16), True, (0.8, 0.5)),
    "humo_cohete": (humo_cohete, 6, (16, 16), False, (0.5, 0.5)),
    "explosion_c90": (explosion_c90, 9, (96, 96), False, (0.42, 0.62)),
}


def exportar(out):
    out = Path(out)
    todo = {}
    for n, fn in ICONOS.items():
        s = fn()
        s.img.save(out / f"ui_arma_{n}_48x16.png")
        todo[f"icono_{n}"] = [s]
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
                                       "origen": "pixelvm, build_armas.py"}}}
        (out / f"{base}.json").write_text(json.dumps(meta, indent=1))
        todo[nombre] = frames
    return todo


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_armas")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 4
    filas = list(todo.items())
    iconos = [fr[0] for n, fr in filas if n.startswith("icono_")]
    vfx = [(n, fr) for n, fr in filas if not n.startswith("icono_")]
    Wt = max(max(sum(s.w * E + 6 for s in fr) for _, fr in vfx), 3 * (48 * E + 6))
    Ht = 2 * (16 * E + 6) + sum(fr[0].h * E + 6 for _, fr in vfx)
    hoja = Image.new("RGBA", (Wt, Ht), PAL["navy"])
    for i, s in enumerate(iconos):               # iconos sobre el gris del panel
        fondo = Image.new("RGBA", (48, 16), PAL["slate_dark"])
        hoja.paste(Image.alpha_composite(fondo, s.img).resize((48 * E, 16 * E), Image.NEAREST),
                   ((i % 3) * (48 * E + 6), (i // 3) * (16 * E + 6)))
    y = 2 * (16 * E + 6)
    for n, fr in vfx:
        x = 0
        for s in fr:
            fondo = Image.new("RGBA", (s.w, s.h), PAL["olive_shadow"])
            hoja.paste(Image.alpha_composite(fondo, s.img).resize((s.w * E, s.h * E), Image.NEAREST), (x, y))
            x += s.w * E + 6
        y += fr[0].h * E + 6
    hoja.save(OUT / "contacto_armas_x4.png")
    print("ok")
