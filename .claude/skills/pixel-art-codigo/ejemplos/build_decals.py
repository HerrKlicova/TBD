"""Decals de suelo, 32x32, 2 variantes por tipo. Bruno (Arte técnico), 6-nov-2026.

Especificación de Iris: aceite, escombros, marcas de vía (rodadas), quemado y grietas. Sin sangre.
Material de suelo (Lucía): albedo plano, sin luz cocida, colores mate, bordes irregulares con alfa
0/255 y motas sueltas en lugar de manchas de camuflaje. Sin contorno: se funden con el suelo.
Salida: decal_<tipo>_<v>_32.png y una prueba sobre las cinco texturas de A7.
"""
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, ROJOS, Sprite  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_decals")
T = 32


def _h(x, y, s):
    n = (x * 374761393 + y * 668265263 + s * 2147483647) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def ruido(x, y, p, s):
    fx, fy = x / p, y / p
    x0, y0 = math.floor(fx), math.floor(fy)
    tx, ty = fx - x0, fy - y0
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    v = [[_h(x0 + i, y0 + j, s) for i in (0, 1)] for j in (0, 1)]
    a = v[0][0] + (v[0][1] - v[0][0]) * tx
    b = v[1][0] + (v[1][1] - v[1][0]) * tx
    return a + (b - a) * ty


def mancha(seed, r=11, cx=16, cy=16, irreg=.45):
    """Campo 0–1 de una mancha irregular: distancia al centro deformada con ruido."""
    def campo(x, y):
        d = math.hypot(x + .5 - cx, (y + .5 - cy) * 1.15) / r
        n = ruido(x, y, 6, seed) * .7 + ruido(x, y, 3, seed + 1) * .3
        return 1 - d + (n - .5) * irreg * 2
    return campo


def aceite(v):
    s = Sprite(T, T)
    m = mancha(10 + v, r=10 if v == 0 else 12, cx=15 if v == 0 else 17, cy=16)
    for y in range(T):
        for x in range(T):
            c = m(x, y)
            if c <= 0:
                if c > -.12 and _h(x, y, 3 + v) > .8:
                    s.set(x, y, "grey_brown")        # salpicadura suelta
                continue
            # Sin el #1c200c de la horda (Lucía, 6-nov): un punto en el valor más oscuro parece un zombi suelto.
            s.set(x, y, "olive_shadow" if c > .3 else "slate_dark" if c > .12 else "grey_brown")
    for k in range(4 if v == 0 else 6):              # brillo apagado del aceite
        x, y = int(10 + _h(k, v, 4) * 12), int(10 + _h(k, v, 5) * 12)
        if s.get(x, y)[:3] == PAL["olive_shadow"][:3]:
            s.set(x, y, "concrete")                # brillo mate (sin slate: es el rim de las tropas)
    if v == 1:                                       # reguero
        for k in range(9):
            s.set(26 + k // 3, 18 + k, "slate_dark")
    return s


def escombros(v):
    s = Sprite(T, T)
    tonos = ["concrete", "concrete_light", "grey_brown", "brown", "olive_grey"]   # sin rosado: nada que parezca sangre
    n = 9 if v == 0 else 13
    for k in range(n):
        cx, cy = 4 + _h(k, v, 21) * 24, 4 + _h(k, v, 22) * 24
        w, h = 1 + int(_h(k, v, 23) * 4), 1 + int(_h(k, v, 24) * 3)
        c = tonos[int(_h(k, v, 25) * len(tonos))]
        for j in range(h):
            for i in range(w):
                if (i, j) in ((0, 0), (w - 1, h - 1)) and w > 2 and h > 1:
                    continue                         # esquinas rotas
                s.set(int(cx) + i, int(cy) + j, c)
        s.hline(int(cx), int(cx) + w - 1, int(cy) + h, "olive_shadow")   # contacto con el suelo
    for k in range(18):                              # polvo y gravilla
        x, y = int(_h(k, v, 26) * 30) + 1, int(_h(k, v, 27) * 30) + 1
        if not s.get(x, y)[3]:
            s.set(x, y, "grey_brown" if k % 2 else "olive_grey")
    if v == 1:                                       # trozo de varilla
        s.line(5, 26, 12, 22, "brown_dark")          # varilla oxidada, en pardo (el óxido parecía sangre)
    return s


def marcas_via(v):
    """Rodadas de vehículo: v0 de oruga (recta, con tacos), v1 de neumático en curva."""
    s = Sprite(T, T)
    if v == 0:
        for x0 in (6, 21):                           # solo tacos sueltos, sin carriles
            for y in range(0, T, 4):
                ancho = 4 + (1 if _h(x0, y, 31) > .5 else 0)
                dx = int(_h(x0, y, 34) * 2)
                for i in range(ancho):
                    if _h(x0 + i, y, 32) > .12:
                        s.set(x0 + dx + i, y, "olive_shadow")
                if _h(x0, y, 35) > .6:
                    s.set(x0 + 2, y + 2, "grey_brown")
    else:
        for off in (0, 9):
            for y in range(T):
                xc = 8 + off + 6 * math.sin(y / T * math.pi)
                for i in range(3):
                    x = int(xc) + i
                    if (y + i) % 3 != 0 and _h(x, y, 33) > .1:
                        s.set(x, y, "olive_shadow" if i != 1 else "grey_brown")
    return s


def quemado(v):
    s = Sprite(T, T)
    m = mancha(40 + v, r=11 if v == 0 else 13, irreg=.6)
    for y in range(T):
        for x in range(T):
            c = m(x, y)
            if c <= 0:
                if c > -.25 and _h(x, y, 41 + v) > .78:
                    s.set(x, y, "grey_brown")        # hollín suelto
                continue
            s.set(x, y, "olive_shadow" if c > .45 else "brown_dark" if c > .2 else "grey_brown")   # sin #1c200c
            if c > .45 and _h(x, y, 43) > .93:
                s.set(x, y, "grey_brown")            # ceniza
    if v == 1:                                       # rayos de la deflagración
        for k in range(6):
            a = k * math.pi / 3 + .4
            for d in range(10, 15):
                if _h(k, d, 44) > .3:
                    s.set(int(16 + math.cos(a) * d), int(16 + math.sin(a) * d), "olive_shadow")
    return s


def grietas(v):
    s = Sprite(T, T)

    def grieta(x, y, ang, largo, seed, prof=0):
        for k in range(largo):
            ang += (_h(k, prof, seed) - .5) * .7
            x += math.cos(ang)
            y += math.sin(ang)
            xi, yi = int(x), int(y)
            s.set(xi, yi, "olive_shadow")            # sin #1c200c (regla del negro de la horda)
            if prof == 0 and k % 2 == 0:
                s.set(xi + 1, yi, "grey_brown")      # labio de la grieta, 2 px de ancho
            if prof < 1 and k in (largo // 3, 2 * largo // 3):
                grieta(x, y, ang + (1 if k % 2 else -1) * .9, largo // 2, seed + k, prof + 1)

    if v == 0:
        grieta(2, 14, 0.2, 28, 51)
    else:
        for k, a in enumerate((0.3, 2.3, 4.2)):      # estrella de impacto
            grieta(16, 16, a, 14, 60 + k)
        s.set(16, 16, "olive_shadow")
        s.set(15, 16, "olive_shadow")
    return s


TIPOS = ["aceite", "escombros", "marcas_via", "quemado", "grietas"]


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    pal = set(c[:3] for c in PAL.values())
    todos = []
    for t in TIPOS:
        for v in (0, 1):
            s = globals()[t](v)
            cols = {s.px[x, y][:3] for y in range(T) for x in range(T) if s.px[x, y][3]}
            mal = [c for c in cols if c not in pal or c in ROJOS]
            s.save(OUT / f"decal_{t}_{v + 1}_32.png")
            print(t, v + 1, f"{len(cols)} colores", "AVISO " + str(mal) if mal else "OK")
            todos.append(s)
    # Prueba sobre las texturas de A7, si están: cada fila una textura, x4.
    a7 = Path(__file__).resolve().parent.parent.parent.parent.parent / "docs/arte/pixel-codigo/a7"
    a7 = Path(sys.argv[2]) if len(sys.argv) > 2 else a7
    texs = sorted(a7.glob("tex_*_64.png")) if a7.exists() else []
    esc = 4
    hoja = Image.new("RGBA", (len(todos) * (T * esc + 4) + 4, max(1, len(texs)) * (T * esc + 4) + 4), PAL["navy"])
    for i, tex in enumerate(texs or [None]):
        fondo = Image.open(tex).convert("RGBA").crop((0, 0, T, T)) if tex else Image.new("RGBA", (T, T), PAL["olive_grey"])
        for j, s in enumerate(todos):
            c = Image.alpha_composite(fondo.copy(), s.img).resize((T * esc, T * esc), Image.NEAREST)
            hoja.paste(c, (4 + j * (T * esc + 4), 4 + i * (T * esc + 4)))
    hoja.save(OUT / "contacto_decals.png")
