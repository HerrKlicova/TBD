"""Suelo de Sagunto, capas A y B de D-N67 (docs/arte/suelo-sagunto.md, Lucía). Bruno, 6-nov-2026.

Capa A: 6 texturas de 64x64 que se repiten, una por zona. Téxel: 0,1 m (10 px/m, 64 px = 6,4 m),
igual que el píxel de la escena en el escalón 1/3 con el zoom por defecto (ortho 18 a 1080p; Marcos).
  - apisonada  (recinto)      beige #b4a080 con chinas #94948c y pisadas #88885c
  - rodadas    (glacis)       beige con dos rodadas en #88885c y gravilla
  - labrada    (huerta)       #a86c50 con surcos: lomo en #94948c al sol y hierbajo en #88885c
  - seca       (descampado)   beige con grietas finas y ralas en #88885c y polvo #94948c
  - asfalto    (calles)       el de A7 (#6c7874) con más grietas ramificadas en #48403c (junta)
  - balasto    (vía)          el de A7, aprobado (única excepción a L* ≥ 47)
Reglas (§3): nada por debajo de L* 47 salvo balasto y juntas; ΔL* ≤ 12 dentro de cada textura sin juntas;
motas sueltas, nunca islas; sin valores de la horda, rojo, ámbar ni #404460.

Capa B: atlas de decals de 128x96 (celdas de 32x32, 2 variantes): charco, aceite, rodadas, grietas,
papeles y hojarasca, con su JSON de rectángulos. Quads planos con recorte por alfa.
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite, _lab_l  # noqa: E402
import build_a7_texturas as a7  # noqa: E402
from build_a7_texturas import T, _h, fbm, motas, ruido, semillas, voronoi  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_suelo")
PROHIBIDOS = {"olive_black", "olive_shadow", "slate_dark", "brown_dark", "red_bright", "amber", "slate",
              "maroon", "blood_dark", "wine", "red", "navy", "black"}
JUNTAS = {"asfalto": {"grey_brown"}, "balasto": {"olive_shadow"}}
L_MIN = 47


# --- capa A ---------------------------------------------------------------------------------------
def apisonada():
    """Tierra apisonada con gravilla: beige liso, chinas sueltas claras y alguna pisada más oscura
    (motas sueltas: nada de bandas tramadas, que se leían como camuflaje)."""
    s = Sprite(T, T)
    s.rect(0, 0, T, T, "beige")
    motas(s, "olive_light", 0.035, 801, dobles=0.3)      # tierra pisada
    motas(s, "concrete_light", 0.06, 803)                # gravilla
    return s


def rodadas():
    """Tierra pisada del glacis: beige con dos rodadas paralelas (tileables en x) y gravilla suelta."""
    s = Sprite(T, T)
    s.rect(0, 0, T, T, "beige")
    for x in range(T):
        for y0 in (20, 38):                              # dos rodadas de 3 px (0,3 m), a 1,8 m (vía de un coche)
            for d in range(3):
                y = y0 + d + (1 if ruido(x, 0, 16, 811 + y0) > 0.6 else 0)
                if _h(x, y, 812) > 0.18:
                    s.set(x, y, "olive_light")
    motas(s, "concrete_light", 0.04, 813)
    return s


def labrada():
    """Tierra labrada de huerta en #a86c50 (el rojizo de la tierra valenciana): surcos en x cada 8 px
    (1 m) que ondulan 1 px y se cortan, con el lomo al sol en #94948c y hierbajos en #88885c a lo largo
    del surco. Marcos gira la UV para que sigan las hileras de naranjos."""
    s = Sprite(T, T)
    s.rect(0, 0, T, T, "tan")
    for y0 in range(0, T, 8):
        for x in range(T):
            y = (y0 + (1 if ruido(x, y0, 16, 821 + y0) > 0.55 else 0)) % T
            h = _h(x, y0, 822)
            if h < 0.15:
                continue                                 # surco cortado (terrón)
            s.set(x, y, "olive_light" if h > 0.9 else "concrete_light")
    motas(s, "olive_light", 0.012, 823, dobles=0)        # hierbajos sueltos entre surcos
    return s


def seca():
    """Tierra seca del descampado: base beige con grietas finas y ralas en #88885c (polígonos grandes y
    cortados) y polvo #94948c. Lucía: el oliva y la red de grietas marcada se leían como camuflaje."""
    s = Sprite(T, T)
    s.rect(0, 0, T, T, "beige")
    pts = semillas(9, 831)
    for y in range(T):
        for x in range(T):
            d1, d2, k = voronoi(x + .5, y + .5, pts)
            if d2 - d1 < 0.55 and ruido(x, y, 8, 833) > 0.42:
                s.set(x, y, "olive_light")
    motas(s, "concrete_light", 0.03, 832, dobles=0)
    return s


def asfalto():
    """Asfalto de A7 con más grietas: dos ramas más que dan la vuelta, en #48403c (junta)."""
    s = a7.asfalto()
    for seed, x0, y0, ang in ((841, 0, 40, 0.15), (842, 44, 0, 1.4)):
        x, y = float(x0), float(y0)
        for k in range(46):
            ang += (_h(k, 0, seed) - .5) * .8
            x, y = x + math.cos(ang), y + math.sin(ang)
            s.set(int(x) % T, int(y) % T, "grey_brown")
    return s


def balasto():
    return a7.balasto()


TEXTURAS = ["apisonada", "rodadas", "labrada", "seca", "asfalto", "balasto"]


def info(nombre, s):
    cols = {}
    for y in range(T):
        for x in range(T):
            p = s.px[x, y][:3]
            cols[p] = cols.get(p, 0) + 1
    nom = {v[:3]: k for k, v in PAL.items()}
    jun = JUNTAS.get(nombre, set())
    base = [c for c in cols if nom[c] not in jun]
    ls = [_lab_l(c) for c in base]
    dl, lmin = max(ls) - min(ls), min(ls)
    mal = [nom[c] for c in cols if nom[c] in PROHIBIDOS and nom[c] not in jun]
    ok = dl <= 12 and (lmin >= L_MIN or nombre == "balasto") and not mal
    return ok, f"{'OK' if ok else 'AVISO'}: {[nom[c] for c in cols]}, L* mín {lmin:.1f}, ΔL* {dl:.1f}" + \
        (f", PROHIBIDOS {mal}" if mal else "")


# --- capa B ---------------------------------------------------------------------------------------
C = 32


def _mancha(x, y, cx, cy, r, seed, ex=1.0):
    d = math.hypot((x + .5 - cx) / ex, y + .5 - cy) / r
    n = a7.ruido(x % T, y % T, 8, seed) * .7 + a7.ruido(x % T, y % T, 4, seed + 1) * .3
    return 1 - d + (n - .5) * .8


def charco(v):
    """Charco: agua en #6c7874 con borde de 1 px en #94948c (reflejo del cielo, sin azul)."""
    s = Sprite(C, C)
    for y in range(C):
        for x in range(C):
            if _mancha(x, y, 16, 16, 9 + 2 * v, 850 + v, ex=1.5) > 0:
                s.set(x, y, "concrete")
    borde = [(x, y) for y in range(C) for x in range(C) if s.get(x, y)[3] and
             any(not s.get(x + dx, y + dy)[3] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    for x, y in borde:
        s.set(x, y, "concrete_light")
    return s


def aceite(v):
    """Aceite sin valores de la horda: núcleo en #48403c, halo en #605c48 y brillo mate en #6c7874."""
    s = Sprite(C, C)
    for y in range(C):
        for x in range(C):
            c = _mancha(x, y, 15 + v, 16, 7 + 2 * v, 860 + v)
            if c > .35:
                s.set(x, y, "grey_brown")
            elif c > 0:
                s.set(x, y, "olive_grey")
            elif c > -.15 and _h(x, y, 862 + v) > .85:
                s.set(x, y, "olive_grey")
    for k in range(3 + v):
        x, y = int(12 + _h(k, v, 863) * 7), int(12 + _h(k, v, 864) * 7)
        if s.get(x, y)[:3] == PAL["grey_brown"][:3]:
            s.set(x, y, "concrete")
    return s


def rodada(v):
    """Rodada suelta de neumático (tramo curvo), en #88885c con el dibujo de la banda."""
    s = Sprite(C, C)
    for k in range(40):
        t = k / 39
        x = 2 + t * 28
        y = 16 + math.sin(t * math.pi * (1 + v)) * (5 + 2 * v)
        for d in (-1, 0, 1):
            if (k + d) % 3 != 0 or d == 0:
                s.set(int(x), int(y) + d, "olive_light")
    return s


def grieta(v):
    """Grieta de firme en #605c48 (no en el valor de la horda): una línea quebrada con dos ramas."""
    s = Sprite(C, C)

    def rama(x, y, ang, largo, seed, prof=0):
        for k in range(largo):
            ang += (_h(k, prof, seed) - .5) * .8
            x, y = x + math.cos(ang), y + math.sin(ang)
            s.set(int(x), int(y), "olive_grey")
            if prof < 1 and k == largo // 2:
                rama(x, y, ang + (0.9 if v else -0.9), largo // 2, seed + 3, 1)
    rama(2, 12 + 6 * v, 0.25 - 0.4 * v, 30, 870 + v)
    return s


def papeles(v):
    """Papeles y basura menuda: motas de 1–2 px en #b8b8b8 y #94948c, nunca bordes largos."""
    s = Sprite(C, C)
    for k in range(7 + 3 * v):
        x, y = 3 + int(_h(k, v, 880) * 26), 3 + int(_h(k, v, 881) * 26)
        c = "grey_light" if k % 2 else "concrete_light"
        s.set(x, y, c)
        if _h(k, v, 882) > .5:
            s.set(x + 1, y, c)
    return s


def hojarasca(v):
    """Hojarasca bajo los naranjos: hojas sueltas en #847c34, #88885c y alguna #845c28 (pardo, no ámbar)."""
    s = Sprite(C, C)
    tonos = ["olive_yellow", "olive_light", "olive_yellow", "ochre_dark"]
    for k in range(40 + 15 * v):
        a, r = _h(k, v, 890) * math.tau, math.sqrt(_h(k, v, 891)) * (11 + 2 * v)
        x, y = int(16 + math.cos(a) * r), int(16 + math.sin(a) * r * .8)
        s.set(x, y, tonos[int(_h(k, v, 892) * 4)])
    return s


DECALS = ["charco", "aceite", "rodada", "grieta", "papeles", "hojarasca"]


def atlas():
    at = Image.new("RGBA", (4 * C, 3 * C), (0, 0, 0, 0))
    rects = {}
    k = 0
    for n in DECALS:
        for v in (0, 1):
            x, y = (k % 4) * C, (k // 4) * C
            at.alpha_composite(globals()[n](v).img, (x, y))
            rects[f"{n}_{v + 1}"] = [x, y, C, C]
            k += 1
    return at, rects


# --- prueba (regla 13): zombis y su sombra sobre cada textura, de día y de noche ---------------------
def prueba(texs, at):
    import build_lamina_siluetas as bl
    E, W = 3, 96
    filas = []
    for hora in ("dia", "noche"):
        fila = []
        for n in TEXTURAS:
            base = Image.new("RGBA", (W, W))
            for x in range(0, W, T):
                for y in range(0, W, T):
                    base.paste(texs[n].img, (x, y))
            dec = at.crop((0 if n != "labrada" else 5 * C % 128, 0, C, C)) if n != "labrada" else None
            if n in ("asfalto", "apisonada"):
                base.alpha_composite(at.crop((0, 0, C, C)), (60, 58))          # charco
                base.alpha_composite(at.crop((C, 0, 2 * C, C)), (6, 60))       # aceite
            if n == "labrada":
                base.alpha_composite(at.crop((2 * C, 2 * C, 3 * C, 3 * C)), (50, 50))   # hojarasca
            if n == "seca":
                base.alpha_composite(at.crop((0, 2 * C, C, 3 * C)), (40, 50))  # papeles
            rgb = base.convert("RGB")
            px = rgb.load()
            if hora == "noche":
                for y in range(W):
                    for x in range(W):
                        d = ((x - 48) / 40) ** 2 + ((y - 50) / 30) ** 2
                        if d < 1:
                            k = 0.55 if d < 0.6 else 0.35
                            px[x, y] = bl.cercano(tuple(a * (1 - k) + b * k for a, b in zip(px[x, y], (240, 220, 156))))
                        else:
                            px[x, y] = bl.gradua(px[x, y], "noche")
            figs = []
            for i, (fx, fy, hh) in enumerate(((14, 40, 12), (34, 36, 16), (58, 44, 12), (76, 38, 8),
                                              (20, 78, 8), (44, 74, 12), (70, 82, 16), (86, 70, 8))):
                dxy = (0.85, 0.53)
                if hora == "noche":
                    vx, vy = fx - 48, fy - 50
                    nn = max(1e-6, math.hypot(vx, vy))
                    dxy = (vx / nn, vy / nn)
                figs.append((fx, fy, hh, bl.silueta(hh, i % 2), dxy))
            bl.pinta(rgb, figs, hora)
            fila.append(rgb)
        filas.append(fila)
    hoja = Image.new("RGB", (len(TEXTURAS) * (W * E + 6) + 6, 2 * (W * E + 6) + 6), PAL["navy"][:3])
    for j, fila in enumerate(filas):
        for i, im in enumerate(fila):
            hoja.paste(im.resize((W * E, W * E), Image.NEAREST), (6 + i * (W * E + 6), 6 + j * (W * E + 6)))
    return hoja, filas


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    texs = {n: globals()[n]() for n in TEXTURAS}
    for n, s in texs.items():
        s.save(OUT / f"suelo_{n}_64.png")
        print(n, info(n, s)[1])
    mos = Image.new("RGBA", (len(TEXTURAS) * (T * 2 + 4) + 4, T * 2 + 8), PAL["navy"])
    for i, n in enumerate(TEXTURAS):
        for a in (0, 1):
            for b in (0, 1):
                mos.paste(texs[n].img, (4 + i * (T * 2 + 4) + a * T, 4 + b * T))
    mos.resize((mos.width * 2, mos.height * 2), Image.NEAREST).save(OUT / "contacto_suelo_2x2.png")
    at, rects = atlas()
    at.save(OUT / "atlas_decals_suelo.png")
    at.resize((at.width * 4, at.height * 4), Image.NEAREST).save(OUT / "atlas_decals_suelo_x4.png")
    (OUT / "atlas_decals_suelo.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "celda": [C, C], "texel_m": 0.1, "rects": rects,
        "nota": "D-N67 capa B: quads planos, recorte por alfa, giro aleatorio; sin valores de la horda, rojo ni ámbar"}}},
        indent=1, ensure_ascii=False))
    hoja, filas = prueba(texs, at)
    hoja.save(OUT / "prueba_suelo_dia_noche_x3.png")
    x1 = Image.new("RGB", (len(TEXTURAS) * 100, 200), PAL["navy"][:3])
    for j, fila in enumerate(filas):
        for i, im in enumerate(fila):
            x1.paste(im, (i * 100, j * 100))
    x1.save(OUT / "prueba_suelo_x1.png")
