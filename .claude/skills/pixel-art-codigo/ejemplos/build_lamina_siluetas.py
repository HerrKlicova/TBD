"""Lámina de siluetas del zombi de pie (billboard vertical) a 6, 8, 10, 12, 16 y 20 px, con sombra
proyectada, en 3 suelos (tierra, olivo, asfalto) y 3 horas (día, atardecer, noche). Bruno, 6-nov-2026.

Encargo de Lucía (analisis-referencias-6nov.md, §1 y prioridad 5): figura vertical aislada, de 3:1 o
más, con brazos al frente, sombra como segunda silueta y un solo valor oscuro (#1c200c). Sirve de
criterio para la mancha LOD de Marcos y para fijar el umbral en el que deja de leerse el brazo.
Paleta estricta: todo, sombras y gradación incluidas, se ajusta al color más cercano de la paleta de 32.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_siluetas")
A7 = Path("/mnt/project-files/docs/arte/pixel-codigo/a7")
ALTURAS = [6, 8, 10, 12, 16, 20]
CUERPO = PAL["olive_black"][:3]            # #1c200c, un solo valor para la horda
CABEZA_LUZ = PAL["olive_shadow"][:3]       # 1 px de luz en la nuca desde 16 px (sigue siendo oscuro)


# Para graduar suelos y sombras no se usan los rojos ni los rosas: son solo de alerta (UI) y la
# mezcla con la luz cálida los sacaba en el borde del charco del foco.
SIN_ROJOS = [PAL[k][:3] for k in PAL if k not in ("maroon", "blood_dark", "wine", "red", "red_bright", "rose",
                                                  "rose_brown", "purple_grey", "tan", "rust")]


def cercano(c):
    return min(SIN_ROJOS, key=lambda p: sum((a - b) ** 2 for a, b in zip(p, c[:3])))


# --- silueta -------------------------------------------------------------------------------------
def silueta(h, paso=0):
    """Máscara {(x, y): color} con los pies en y = h-1 y la figura mirando a la derecha (3/4).
    paso 0/1 alterna la pierna adelantada. Por debajo de 10 px no hay brazo: es el palito (LOD)."""
    m = {}

    def px(x, y, c=CUERPO):
        m[(x, y)] = c

    if h <= 8:                                     # palito: 2 px de ancho, cabeza de 1 px
        for y in range(1, h):
            px(0, y)
            px(1, y)
        px(1 if h == 8 else 0, 0)
        if h == 8:                                 # hombro hacia delante: insinúa el brazo
            px(2, 2)
        return m
    cab = 2 if h < 16 else 3                       # cabeza algo grande (Lucía)
    tw = 2 if h < 16 else 3                        # torso estrecho
    hombro = cab
    cadera = round(h * 0.55)
    x0 = 1
    for y in range(cab):                           # cabeza, adelantada 1 px (encorvado)
        for x in range(cab):
            px(x0 + 1 + x, y)
    for y in range(hombro, cadera):                # torso
        for x in range(tw):
            px(x0 + x, y)
    largo = 3 if h < 12 else (4 if h < 16 else 6)  # brazos al frente, a la altura del hombro
    grueso = 1 if h < 16 else 2
    for g in range(grueso):
        for x in range(largo):
            px(x0 + tw + x, hombro + 1 + g + (1 if x == largo - 1 and h >= 12 else 0))  # mano caída
    # piernas: separadas 1 px desde 10 px, una adelantada según el paso
    pie = h - 1
    adel, atras = (x0 + tw, x0 - 1) if paso == 0 else (x0 + tw - 1, x0)
    for y in range(cadera, h):
        t = (y - cadera) / max(1, pie - cadera)
        px(round(x0 + (adel - x0) * t), y)
        px(round(x0 + tw - 1 + (atras - (x0 + tw - 1)) * t), y)
        if h >= 16:
            px(round(x0 + (adel - x0) * t) + 1, y)
    if h >= 16:
        px(x0 + 1, 0, CABEZA_LUZ)                  # 1 px de luz arriba a la izquierda
    return m


def sombra(m, h, largo, dx=0.85, dy=0.53):
    """La sombra es un valor intermedio entre el suelo y la figura (nunca tan oscura como ella): si
    no, figura y sombra se funden en una «L» y la masa vuelve a leerse como garras.
    Proyecta la silueta en el suelo: cada píxel a altura z sobre los pies se desplaza z*largo en
    la dirección contraria a la luz. Devuelve un conjunto de píxeles relativos a los pies."""
    s = set()
    for (x, y) in m:
        z = (h - 1) - y
        for k in range(4):                         # submuestreo para no dejar huecos
            zz = z + k / 4
            s.add((round(x + zz * largo * dx), round(zz * largo * dy)))
    return s


# --- suelos y horas -----------------------------------------------------------------------------
def suelo(nombre, w, h):
    if nombre == "olivo":
        return Image.new("RGB", (w, h), PAL["olive"][:3])
    t = Image.open(A7 / f"tex_{nombre}_64.png").convert("RGB")
    im = Image.new("RGB", (w, h))
    for x in range(0, w, 64):
        for y in range(0, h, 64):
            im.paste(t, (x, y))
    return im


HORAS = {
    # tinte multiplicativo, mezcla, largo de sombra (× altura) y opacidad de la sombra
    "dia": dict(tinte=(240, 220, 156), k=0.15, largo=1.5, op=0.32),
    "atardecer": dict(tinte=(224, 140, 48), k=0.45, largo=2.5, op=0.45),
    "noche": dict(tinte=(24, 28, 48), k=0.85, largo=1.0, op=0.45),
}


def gradua(c, hora):
    p = HORAS[hora]
    mult = tuple(a * b / 255 for a, b in zip(c, p["tinte"]))
    return cercano(tuple(a * (1 - p["k"]) + b * p["k"] for a, b in zip(c, mult)))


def panel(nombre, hora, paso=0, w=132, h=40):
    """Una tira con las 6 alturas sobre un suelo y una hora. Pies en y = 27."""
    base = suelo(nombre, w, h)
    px = base.load()
    p = HORAS[hora]
    foco = (w // 2 + 8, 22)                         # de noche, charco de foco en el centro
    for y in range(h):
        for x in range(w):
            c = px[x, y]
            if hora == "noche":
                d = ((x - foco[0]) / 46) ** 2 + ((y - foco[1]) / 16) ** 2
                if d < 1:                          # dentro del charco: luz cálida #f0dc9c, borde en 2 escalones
                    k = 0.55 if d < 0.6 else 0.35
                    c = tuple(a * (1 - k) + b * k for a, b in zip(c, (240, 220, 156)))
                    px[x, y] = cercano(c)
                    continue
            px[x, y] = gradua(c, hora)
    pies = 27
    xs = [4, 16, 30, 46, 66, 92]
    for hh, x0 in zip(ALTURAS, xs):
        m = silueta(hh, paso)
        largo = p["largo"]
        dxy = (0.85, 0.53)
        if hora == "noche":                         # la sombra la hace el foco: sale hacia fuera de él
            vx = x0 - foco[0]
            dxy = (-0.85, 0.53) if vx < 0 else (0.85, 0.53)
        for (sx, sy) in sombra(m, hh, largo, *dxy):
            X, Y = x0 + sx, pies + sy
            if 0 <= X < w and 0 <= Y < h:
                c = px[X, Y]
                px[X, Y] = cercano(tuple(a * (1 - p["op"]) for a in c))
        for (x, y), c in m.items():
            X, Y = x0 + x, pies - (hh - 1) + y
            if 0 <= X < w and 0 <= Y < h:
                px[X, Y] = c
    return base


def masa(nombre, hora, n=60, w=132, h=60, seed=3):
    """Prueba de masa a una sola altura (8 px, LOD) con aire entre figuras (≈40 % de suelo)."""
    import random
    rnd = random.Random(seed)
    base = suelo(nombre, w, h)
    px = base.load()
    for y in range(h):
        for x in range(w):
            px[x, y] = gradua(px[x, y], hora) if hora != "noche" else px[x, y]
    p = HORAS[hora]
    pos = []
    while len(pos) < n:
        x, y = rnd.randrange(2, w - 6), rnd.randrange(10, h - 2)
        if all(abs(x - a) > 3 or abs(y - b) > 2 for a, b in pos):
            pos.append((x, y))
    pos.sort(key=lambda t: t[1])
    for x0, pies in pos:
        hh = rnd.choice((8, 8, 6))
        m = silueta(hh, rnd.randrange(2))
        for (sx, sy) in sombra(m, hh, p["largo"]):
            X, Y = x0 + sx, pies + sy
            if 0 <= X < w and 0 <= Y < h:
                px[X, Y] = cercano(tuple(a * (1 - p["op"]) for a in px[X, Y]))
        for (x, y), c in m.items():
            X, Y = x0 + x, pies - (hh - 1) + y
            if 0 <= X < w and 0 <= Y < h:
                px[X, Y] = c
    return base


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    suelos, horas, E, G = ["tierra", "olivo", "asfalto"], list(HORAS), 3, 6
    pw, ph = 132, 40
    hoja = Image.new("RGB", (len(horas) * (pw * E + G) + G + 70, len(suelos) * (ph * E + G) + G + 22 + 34 + G),
                     PAL["navy"][:3])
    d = ImageDraw.Draw(hoja)
    for j, hora in enumerate(horas):
        d.text((70 + G + j * (pw * E + G), 6), hora, fill=PAL["bone"][:3])
    for i, s in enumerate(suelos):
        d.text((G, 22 + G + i * (ph * E + G) + ph * E // 2), s, fill=PAL["bone"][:3])
        for j, hora in enumerate(horas):
            im = panel(s, hora)
            im.save(OUT / f"panel_{s}_{hora}_x1.png")
            hoja.paste(im.resize((pw * E, ph * E), Image.NEAREST), (70 + G + j * (pw * E + G), 22 + G + i * (ph * E + G)))
    # fila final: los paneles de día a x1, como se verán en pantalla
    y1 = 22 + G + len(suelos) * (ph * E + G)
    d.text((G, y1 + 10), "x1 (dia)", fill=PAL["bone"][:3])
    for i, s in enumerate(suelos):
        hoja.paste(panel(s, "dia"), (70 + G + i * (pw + G), y1))
    hoja.save(OUT / "lamina_siluetas_zombi.png")
    # siluetas sueltas a x8 (las dos fases del paso) para Marcos y Teo
    gr = Image.new("RGB", (len(ALTURAS) * 8 * 12 + 8, 2 * 8 * 22 + 8), PAL["olive"][:3])
    for k, hh in enumerate(ALTURAS):
        for f in (0, 1):
            fig = Image.new("RGBA", (12, 22), (0, 0, 0, 0))
            for (x, y), c in silueta(hh, f).items():
                fig.putpixel((x, 21 - (hh - 1) + y), c + (255,))
            gr.paste(fig.resize((96, 176), Image.NEAREST), (8 + k * 96, 4 + f * 176), fig.resize((96, 176), Image.NEAREST))
    gr.save(OUT / "siluetas_x8.png")
    # prueba de masa LOD (6–8 px) con aire, en los tres suelos de día y al atardecer, a x3
    mh = Image.new("RGB", (3 * (132 * 3 + G) + G, 2 * (60 * 3 + G) + G), PAL["navy"][:3])
    for i, s in enumerate(suelos):
        for j, hora in enumerate(("dia", "atardecer")):
            mh.paste(masa(s, hora).resize((396, 180), Image.NEAREST), (G + i * (396 + G), G + j * (180 + G)))
    mh.save(OUT / "prueba_masa_lod.png")
    print("ok", OUT)
