"""A7 · Texturas tileables de 64x64 por código (D-N56). Bruno (Arte técnico), 6-nov-2026.

Balasto, hormigón, ladrillo, tierra y asfalto. Reglas:
  - Paleta de 32 estricta, colores mate, sin naranja ni rojo que compitan con las alertas.
  - Albedo plano: sin luz cocida ni sombras direccionales (la luz la pone Unity). Solo variación
    de material y, como mucho, la oclusión de las juntas y los huecos.
  - Tileable: todo el ruido es periódico (64 px) y las celdas se miden con distancia toroidal.
  - Deterministas: mismo código, mismo píxel.
Salida: tex_<nombre>_64.png, un mosaico 3x3 para ver las costuras y una hoja de contacto.
"""
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, Sprite  # noqa: E402
from PIL import Image  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_a7")
T = 64


def _h(x, y, seed):
    n = (x * 374761393 + y * 668265263 + seed * 2147483647) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def ruido(x, y, periodo, seed):
    """Ruido de valor periódico: la red tiene T/periodo celdas y da la vuelta."""
    n = T // periodo
    fx, fy = x / periodo, y / periodo
    x0, y0 = int(fx), int(fy)
    tx, ty = fx - x0, fy - y0
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    v = [[_h((x0 + i) % n, (y0 + j) % n, seed) for i in (0, 1)] for j in (0, 1)]
    a = v[0][0] + (v[0][1] - v[0][0]) * tx
    b = v[1][0] + (v[1][1] - v[1][0]) * tx
    return a + (b - a) * ty


def fbm(x, y, seed, periodos=(32, 16, 8, 4)):
    t, peso, s = 0.0, 1.0, 0.0
    for p in periodos:
        t += ruido(x, y, p, seed + p) * peso
        s += peso
        peso *= 0.55
    return t / s


def semillas(n, seed):
    return [(_h(i, 0, seed) * T, _h(i, 1, seed) * T) for i in range(n)]


def voronoi(x, y, pts):
    """Las dos distancias más cortas (toroidales) y el índice de la celda."""
    d = []
    for k, (px, py) in enumerate(pts):
        dx = min(abs(x - px), T - abs(x - px))
        dy = min(abs(y - py), T - abs(y - py))
        d.append((math.hypot(dx, dy), k))
    d.sort()
    return d[0][0], d[1][0], d[0][1]


def tono(rampa, v):
    return rampa[min(len(rampa) - 1, max(0, int(v * len(rampa))))]


def balasto():
    """Piedra partida de vía: cantos de 4-7 px, cada uno de un gris o pardo, juntas oscuras."""
    s = Sprite(T, T)
    pts = semillas(110, 7)
    piedra = ["grey_brown", "olive_grey", "concrete", "concrete", "olive_grey", "grey_brown", "concrete_light"]
    for y in range(T):
        for x in range(T):
            d1, d2, k = voronoi(x + .5, y + .5, pts)
            if d2 - d1 < 0.8:
                s.set(x, y, "olive_shadow")          # hueco entre piedras
            else:
                v = _h(k, 9, 7) * 0.8 + fbm(x, y, 3, (8, 4)) * 0.2
                s.set(x, y, tono(piedra, v))
    return s


def hormigon():
    """Losa de hormigón: grano fino de dos grises, manchas de humedad suaves, poros y una junta."""
    s = Sprite(T, T)
    for y in range(T):
        for x in range(T):
            v = fbm(x, y, 11, (8, 4, 2))             # grano fino, sin manchas grandes
            m = fbm(x, y, 13, (32, 16))
            g = _h(x, y, 12)
            c = "concrete_light" if v > 0.62 else "concrete"
            if m < 0.30 and v < 0.5:
                c = "olive_grey"                     # humedad, salpicada y floja
            if g > 0.975:
                c = "grey_brown"                     # poro
            s.set(x, y, c)
    for i in range(T):                               # junta de dilatación, en el borde: casa al repetir
        s.set(i, 0, "grey_brown")
        s.set(0, i, "grey_brown")
    return s


def ladrillo():
    """Aparejo a soga de 16x8 (llaga de 1 px). Terracota apagada y algún ocre, sin naranja."""
    s = Sprite(T, T)
    piezas = ["rose_brown", "rose", "rose", "rose_brown", "rose", "grey_brown"]
    for y in range(T):
        fila = y // 8
        off = 8 if fila % 2 else 0
        for x in range(T):
            xx = (x + off) % T
            if y % 8 == 7 or xx % 16 == 15:
                s.set(x, y, "olive_grey")             # mortero
                continue
            k = (xx // 16) + fila * 4
            base = tono(piezas, _h(k, 3, 21))
            g = fbm(x, y, 22, (8, 4))
            c = base
            if g < 0.30:                               # desgaste, sin brillos (albedo plano)
                c = {"rose": "rose_brown", "rose_brown": "grey_brown", "grey_brown": "olive_shadow"}.get(base, base)
            elif _h(x, y, 23) > 0.96:
                c = "grey_brown"                       # picadura
            s.set(x, y, c)
    return s


def tierra():
    """Tierra batida: pardos con manchas de ocre seco, algo de hierba muerta y chinas."""
    s = Sprite(T, T)
    for y in range(T):
        for x in range(T):
            v = fbm(x, y, 31, (8, 4, 2))
            w = fbm(x, y, 37, (16, 8, 4))
            c = tono(["brown_dark", "brown", "brown", "brown", "rose_brown"], v)
            if w > 0.70 and v > 0.45:
                c = "olive_grey"                     # polvo seco
            elif w < 0.27 and v < 0.55:
                c = "olive_dark"                     # hierba muerta
            g = _h(x, y, 33)
            if g > 0.985:
                c = "concrete"                       # china
            elif g > 0.97:
                c = "grey_brown"
            s.set(x, y, c)
    return s


def asfalto():
    """Asfalto gastado: negro azulado con árido claro salpicado, parches y una grieta fina."""
    s = Sprite(T, T)
    for y in range(T):
        for x in range(T):
            v = fbm(x, y, 41, (4, 2))
            m = fbm(x, y, 44, (32, 16))
            c = "grey_brown" if v > 0.40 else "olive_shadow"
            if m > 0.68 and v > 0.6:
                c = "olive_grey"                     # rodada gastada
            g = _h(x, y, 42)
            if g > 0.975:
                c = "slate"
            elif g > 0.965:
                c = "concrete"
            s.set(x, y, c)
    # Grieta fina que cruza el borde (envuelve al repetir).
    x = 20.0
    for y in range(T):
        x += (ruido(0, y, 8, 43) - 0.5) * 2.2
        s.set(int(x) % T, y, "olive_black")
    return s


def mosaico(s, n=3):
    im = Image.new("RGBA", (T * n, T * n))
    for j in range(n):
        for i in range(n):
            im.paste(s.img, (i * T, j * T))
    t = Sprite(T * n, T * n)
    t.img = im
    t.px = im.load()
    return t


def info(s):
    from pixelvm import _lab_l
    cols = {}
    for y in range(T):
        for x in range(T):
            p = s.px[x, y][:3]
            cols[p] = cols.get(p, 0) + 1
    fuera = [c for c in cols if c + (255,) not in PAL.values()]
    ls = [_lab_l(c) for c, n in cols.items() for _ in range(n)]
    med = sum(ls) / len(ls)
    return f"{len(cols)} colores, L* medio {med:.0f}, rango {min(ls):.0f}-{max(ls):.0f}" + \
        (f", FUERA DE PALETA {fuera}" if fuera else "")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    nombres = ["balasto", "hormigon", "ladrillo", "tierra", "asfalto"]
    texs = [globals()[n]() for n in nombres]
    for n, s in zip(nombres, texs):
        s.save(OUT / f"tex_{n}_64.png")
        mosaico(s).save(OUT / f"tex_{n}_mosaico3x3.png", scale=2)
        print(n, info(s))
    hoja = Image.new("RGBA", (len(texs) * (T * 2 + 8) * 2 + 8, T * 2 * 2 + 16), PAL["navy"])
    for i, s in enumerate(texs):
        hoja.paste(mosaico(s, 2).img.resize((T * 4, T * 4), Image.NEAREST), (8 + i * (T * 4 + 8), 8))
    hoja.save(OUT / "contacto_a7.png")
