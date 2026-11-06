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


# Material de suelo (Lucía, 6-nov): dentro de cada textura, ΔL* de 12 como máximo salvo las juntas.
# El protagonismo es de la horda y la tropa, no del suelo. JUNTAS lista lo que no cuenta.
DL_MAX = 12
JUNTAS = {
    "balasto": {"olive_shadow"},
    "hormigon": {"grey_brown"},
    "ladrillo": {"olive_grey"},
    "tierra": set(),
    "asfalto": {"olive_black"},
}


def balasto():
    """Piedra partida en el valor más oscuro de la escena (#48403c, D-N42): cantos en gris pardo,
    algo de pizarra, alguna piedra clara suelta y huecos oscuros."""
    s = Sprite(T, T)
    pts = semillas(110, 7)
    for y in range(T):
        for x in range(T):
            d1, d2, k = voronoi(x + .5, y + .5, pts)
            if d2 - d1 < 0.8:
                s.set(x, y, "olive_shadow")          # hueco entre piedras (junta)
                continue
            v = _h(k, 9, 7)
            c = "grey_brown" if v < 0.80 else "slate" if v < 0.91 else "olive_grey"   # neutro: sin azules ni rojos fuertes
            s.set(x, y, c)
    return s


def hormigon():
    """Losa de hormigón: moteado fino y suave de dos grises y la junta de losa."""
    s = Sprite(T, T)
    for y in range(T):
        for x in range(T):
            v = fbm(x, y, 11, (4, 2))
            c = "concrete_light" if v > 0.68 else "concrete"   # sin poros oscuros: ΔL* ≤ 12
            s.set(x, y, c)
    for i in range(T):                               # junta de losa, en el borde: casa al repetir
        s.set(i, 0, "grey_brown")
        s.set(0, i, "grey_brown")
    return s


def ladrillo():
    """Aparejo a soga de 16x8 (llaga de 1 px). Terracota tirando a marrón, lejos del rojo de peligro."""
    s = Sprite(T, T)
    for y in range(T):
        fila = y // 8
        off = 8 if fila % 2 else 0
        for x in range(T):
            xx = (x + off) % T
            if y % 8 == 7 or xx % 16 == 15:
                s.set(x, y, "olive_grey")             # mortero (junta)
                continue
            k = (xx // 16) + fila * 4
            base = "rose" if _h(k, 3, 21) > 0.62 else "rose_brown"
            c = base
            if base == "rose" and (fbm(x, y, 22, (8, 4)) < 0.32 or _h(x, y, 23) > 0.95):
                c = "rose_brown"                      # desgaste y picadura
            s.set(x, y, c)
    return s


def tierra():
    """Tierra ocre, el color del suelo del mundo (zona y llano del mapa): moteado fino, polvo seco
    en gris oliva y alguna china."""
    s = Sprite(T, T)
    for y in range(T):
        for x in range(T):
            v = fbm(x, y, 31, (8, 4, 2))
            w = fbm(x, y, 37, (16, 8, 4))
            c = "ochre_dark"
            if v < 0.40 or (w > 0.66 and v < 0.55):
                c = "olive_grey"                     # polvo seco
            if _h(x, y, 33) > 0.985:
                c = "concrete"                       # china
            s.set(x, y, c)
    return s


def asfalto():
    """Asfalto gastado: ruido fino oscuro, árido en pizarra y una grieta fina."""
    s = Sprite(T, T)
    for y in range(T):
        for x in range(T):
            v = fbm(x, y, 41, (4, 2))
            c = "grey_brown" if v > 0.42 else "olive_shadow"
            if _h(x, y, 42) > 0.97:
                c = "slate"                          # árido
            s.set(x, y, c)
    x = 20.0                                         # grieta que envuelve al repetir (junta)
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


def info(s, nombre):
    from pixelvm import _lab_l
    cols = {}
    for y in range(T):
        for x in range(T):
            p = s.px[x, y][:3]
            cols[p] = cols.get(p, 0) + 1
    fuera = [c for c in cols if c + (255,) not in PAL.values()]
    jun = {PAL[k][:3] for k in JUNTAS[nombre]}
    ls = [_lab_l(c) for c, n in cols.items() for _ in range(n)]
    lm = [_lab_l(c) for c in cols if c not in jun]
    dl = max(lm) - min(lm)
    estado = "OK" if dl <= DL_MAX and not fuera else "AVISO"
    return f"{estado}: {len(cols)} colores, L* medio {sum(ls) / len(ls):.0f}, ΔL* sin juntas {dl:.1f}" + \
        (f", FUERA DE PALETA {fuera}" if fuera else "")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    nombres = ["balasto", "hormigon", "ladrillo", "tierra", "asfalto"]
    texs = [globals()[n]() for n in nombres]
    for n, s in zip(nombres, texs):
        s.save(OUT / f"tex_{n}_64.png")
        mosaico(s).save(OUT / f"tex_{n}_mosaico3x3.png", scale=2)
        print(n, info(s, n))
    hoja = Image.new("RGBA", (len(texs) * (T * 2 + 8) * 2 + 8, T * 2 * 2 + 16), PAL["navy"])
    for i, s in enumerate(texs):
        hoja.paste(mosaico(s, 2).img.resize((T * 4, T * 4), Image.NEAREST), (8 + i * (T * 4 + 8), 8))
    hoja.save(OUT / "contacto_a7.png")
