"""Ambientación de Sagunto (docs/arte/ambientacion-sagunto.md, Lucía, 6-nov). Bruno (Arte técnico).

Piezas por código, paleta de 32, sin rojos ni naranjas de alerta:
  - linea_seguridad: decal tileable del andén, línea en #f0dc9c (nunca ámbar) con desgaste. 64x2 px.
  - placa_sagunto: placa esmaltada de la estación, letras #d4d0cc sobre #283440. 35x11 px (y x2).
  - reloj: reloj de fachada parado a las 17:42, la hora del último tren. 16 y 24 px.
  - via_hollin: decal tileable del eje de la vía, hollín y goteo de aceite sobre el balasto. 64x32 px.
Texturas de mundo y decals: sin contorno negro (van sobre un modelo o el suelo), alfa 0/255.
Salida: PNG por pieza y una prueba sobre los suelos de A7 (hormigón del andén y balasto).
"""
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, ROJOS, Sprite  # noqa: E402
import build_a7_texturas as a7  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_sagunto")


def _h(x, y, s):
    n = (x * 374761393 + y * 668265263 + s * 2147483647) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def ruido_periodico(x, y, p, s, periodo):
    """Ruido de valor que se repite cada `periodo` px en x (para decals tileables a lo largo)."""
    n = periodo // p
    fx, fy = x / p, y / p
    x0, y0 = math.floor(fx), math.floor(fy)
    tx, ty = fx - x0, fy - y0
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    v = lambda i, j: _h(i % n, j, s)
    a = v(x0, y0) * (1 - tx) + v(x0 + 1, y0) * tx
    b = v(x0, y0 + 1) * (1 - tx) + v(x0 + 1, y0 + 1) * tx
    return a * (1 - ty) + b * ty


# --- 1. línea de seguridad del andén ------------------------------------------------------------
def linea_seguridad():
    """Banda de 2 px en crema #f0dc9c, tileable en x. Desgaste: algún píxel en beige (pintura gastada)
    y alguno sin pintura (alfa 0, asoma la losa). Escala recomendada: 64 px = 9,6 m (0,15 m/px)."""
    s = Sprite(64, 2)
    for x in range(64):
        for y in range(2):
            h = _h(x, y, 501)
            if h < 0.06:
                continue                                  # descascarillado
            s.set(x, y, "beige" if h < 0.16 else "cream")
    return s


# --- 2. placa «SAGUNTO» -------------------------------------------------------------------------
LETRAS = {
    "S": ["011", "100", "010", "001", "110"],
    "A": ["010", "101", "111", "101", "101"],
    "G": ["011", "100", "101", "101", "011"],
    "U": ["101", "101", "101", "101", "111"],
    "N": ["1001", "1101", "1011", "1001", "1001"],
    "T": ["111", "010", "010", "010", "010"],
    "O": ["010", "101", "101", "101", "010"],
}


def placa_sagunto(texto="SAGUNTO"):
    """Placa esmaltada: fondo #283440, letras #d4d0cc de 5 px, canto de 1 px en olive_black."""
    ancho = sum(len(LETRAS[c][0]) for c in texto) + len(texto) - 1
    w, h = ancho + 8, 11
    s = Sprite(w, h)
    s.rect(0, 0, w, h, "olive_black")                     # canto de la chapa
    s.rect(1, 1, w - 2, h - 2, "slate_dark")
    x = 4
    for c in texto:
        for j, fila in enumerate(LETRAS[c]):
            for i, b in enumerate(fila):
                if b == "1":
                    s.set(x + i, 3 + j, "bone")
        x += len(LETRAS[c][0]) + 1
    return s


# --- 3. reloj de fachada ------------------------------------------------------------------------
def reloj(n=16, hora=17, minuto=42):
    """Esfera en hueso con aro #283440, marcas de 12-3-6-9 y agujas oscuras. Luz arriba a la izquierda:
    el aro de abajo a la derecha un escalón más oscuro. Parado a las 17:42 (el último tren)."""
    s = Sprite(n, n)
    c = (n - 1) / 2
    r = n / 2
    for y in range(n):
        for x in range(n):
            d = math.hypot(x - c, y - c)
            if d > r - 0.3:
                continue
            if d > r - 1.6 - (n >= 24):
                sombra = (x - c) + (y - c) > 1.5
                s.set(x, y, "olive_black" if sombra else "slate_dark")
            else:
                s.set(x, y, "bone")
    rm = r - 2.6 - (n >= 24)                              # marcas horarias
    for k in range(12 if n >= 24 else 4):
        a = k * (math.tau / (12 if n >= 24 else 4))
        s.set(round(c + math.sin(a) * rm), round(c - math.cos(a) * rm), "grey_brown")

    def aguja(ang, largo, grueso):
        for t in range(0, int(largo * 4) + 1):
            l = t / 4
            x, y = c + math.sin(ang) * l, c - math.cos(ang) * l
            s.set(round(x), round(y), "olive_black")
            if grueso:
                s.set(round(x + 0.5), round(y), "olive_black")

    aguja((hora % 12 + minuto / 60) / 12 * math.tau, rm * 0.55, n >= 24)
    aguja(minuto / 60 * math.tau, rm * 0.95, False)
    s.set(round(c), round(c), "olive_black")
    return s


# --- 4. hollín y aceite en el eje de la vía -----------------------------------------------------
def via_hollin():
    """Decal tileable en x (64x32) para el centro de la vía, entre carriles: hollín difuso de las
    locomotoras en #303424 y #48403c (nunca el #1c200c de la horda ni tonos rojizos; Lucía, v1.1), motas sueltas en el borde y goterones de aceite
    alineados con el eje. Escala: la misma que el balasto (64 px de lado)."""
    s = Sprite(64, 32)
    for y in range(32):
        perfil = 0.75 - abs(y - 15.5) / 14                # más denso en el eje, pero a manchas
        for x in range(64):
            n = ruido_periodico(x, y, 16, 610, 64) * 0.6 + ruido_periodico(x, y, 4, 611, 64) * 0.4
            v = perfil + (n - 0.5) * 1.5
            if v > 0.55:
                s.set(x, y, "olive_shadow" if v > 0.85 else "grey_brown")
            elif v > 0.3 and _h(x, y, 612) > 0.85:
                s.set(x, y, "grey_brown")                 # mota suelta de hollín
    for k in range(5):                                    # goterones de aceite en el eje
        x = int(4 + k * 12.5 + _h(k, 0, 613) * 4) % 64
        y = 14 + int(_h(k, 1, 613) * 4)
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)) if k % 2 else ((0, 0), (1, 0)):
            s.set((x + dx) % 64, y + dy, "olive_shadow")
        if _h(k, 2, 613) > 0.5:
            s.set((x + 2) % 64, y, "slate_dark")           # brillo mate del aceite
    return s


# --- 5. pintadas (sagunto-nivel.md v1.1, §3) ---------------------------------------------------
GRAFITI = {
    "A": ["0110", "1001", "1111", "1001", "1001"], "Q": ["0110", "1001", "1001", "1010", "0101"],
    "U": ["1001", "1001", "1001", "1001", "0110"], "I": ["1", "1", "1", "1", "1"],
    "H": ["1001", "1001", "1111", "1001", "1001"], "Y": ["10001", "01010", "00100", "00100", "00100"],
    "V": ["10001", "10001", "01010", "01010", "00100"], "O": ["0110", "1001", "1001", "1001", "0110"],
    "S": ["0111", "1000", "0110", "0001", "1110"], " ": ["00", "00", "00", "00", "00"],
}


def _espray(s, bloques, seed):
    """Trazo de espray: cada punto lógico es un bloque de 2x2 (x, y en px reales); trazo lleno y un
    pulverizado escaso alrededor. Solo #d4d0cc (espray blanco), nunca rojo."""
    trazo = set()
    for (x, y) in bloques:
        for dx in (0, 1):
            for dy in (0, 1):
                trazo.add((x + dx, y + dy))
    for (x, y) in trazo:
        s.set(x, y, "bone")
    for (x, y) in trazo:                                  # pulverizado: motas sueltas a 2 px
        ox = x + (2 if _h(x, y, seed) > 0.5 else -2)
        oy = y + int(_h(y, x, seed + 1) * 5) - 2
        if (ox, oy) not in trazo and _h(ox, oy, seed + 2) > 0.93 and s.inside(ox, oy):
            s.set(ox, oy, "bone")


def pintada_texto(texto="AQUÍ HAY VIVOS"):
    """«AQUÍ HAY VIVOS» a espray: letras de 10 px con 2 px de trazo que bailan 1 px, la tilde de la Í
    y algún chorreón corto. A 0,1 m/px: unos 13 m de largo y 1 m de alto, legible desde el aire."""
    t = texto.replace("Í", "I")
    ancho = sum(len(GRAFITI[c][0]) for c in t) + len(t) - 1
    w, h = 2 * ancho + 6, 20
    s = Sprite(w, h)
    bl, x = [], 3
    for i, c in enumerate(t):
        jy = 5 + (1 if _h(i, 7, 701) > 0.5 else 0)        # baile vertical de 1 px
        for j, fila in enumerate(GRAFITI[c]):
            for k, b in enumerate(fila):
                if b == "1":
                    bl.append((x + 2 * k, jy + 2 * j))
        if texto[i] == "Í":
            bl.append((x + 1, jy - 4))                     # tilde
        if c not in " I" and _h(i, 8, 702) > 0.6:          # chorreón de 1 px bajo la letra
            for d in range(2):
                s.set(x + 1, jy + 10 + d, "bone")
        x += 2 * (len(GRAFITI[c][0]) + 1)
    _espray(s, bl, 703)
    return s


def pintada_flecha():
    """Flecha a espray hacia la derecha (se gira en el decal hacia la estación). 0,1 m/px: 4 m."""
    s = Sprite(40, 18)
    pts = [(x, 4) for x in range(1, 14)]                  # astil, con una ligera caída
    pts += [(14, 4), (15, 4)] + [(15 - k, 4 - k) for k in range(1, 4)] + [(15 - k, 4 + k) for k in range(1, 4)]
    _espray(s, [(2 * x + 4, 2 * y + 1) for x, y in pts], 711)
    return s


# --- 6. hollín de la puerta oeste (huellas de la horda) -----------------------------------------
def hollin_puerta(v=0):
    """Mancha de 64x64 en el balasto junto a la puerta oeste, donde siempre rompe la horda: núcleo
    en #303424, halo en #48403c con motas sueltas y arrastres alargados hacia la puerta (este).
    Se lee como suciedad, nunca como sangre: sin rojizos ni el #1c200c de la horda."""
    s = Sprite(64, 64)
    cx, cy = 30 + 4 * v, 32 - 3 * v
    for y in range(64):
        for x in range(64):
            d = math.hypot((x - cx) / (24 + 4 * v), (y - cy) / 16)
            n = ruido_periodico(x, y, 8, 720 + v, 64) * 0.6 + ruido_periodico(x, y, 4, 722 + v, 64) * 0.4
            arrastre = 0.25 * max(0.0, math.sin((y + 3 * v) * 0.9)) * (x > cx)   # rayas de arrastre al este
            c = 1 - d + (n - 0.5) * 0.9 + arrastre
            if c > 0.45:
                s.set(x, y, "olive_shadow")
            elif c > 0.12:
                s.set(x, y, "grey_brown")
            elif c > -0.05 and _h(x, y, 724 + v) > 0.88:
                s.set(x, y, "grey_brown")
    return s


def tileable_x(im, veces=3):
    w, h = im.size
    out = Image.new("RGBA", (w * veces, h))
    for i in range(veces):
        out.paste(im, (i * w, 0))
    return out


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    piezas = {
        "decal_linea_seguridad_64x2": linea_seguridad(),
        "placa_sagunto": placa_sagunto(),
        "reloj_fachada_16": reloj(16),
        "reloj_fachada_24": reloj(24),
        "decal_via_hollin_64x32": via_hollin(),
        "decal_pintada_aqui_hay_vivos": pintada_texto(),
        "decal_pintada_flecha": pintada_flecha(),
        "decal_hollin_puerta_1_64": hollin_puerta(0),
        "decal_hollin_puerta_2_64": hollin_puerta(1),
    }
    pal = {c[:3] for c in PAL.values()}
    for n, s in piezas.items():
        cols = {s.px[x, y][:3] for y in range(s.h) for x in range(s.w) if s.px[x, y][3]}
        mal = [c for c in cols if c not in pal or c in ROJOS]
        s.save(OUT / f"{n}.png")
        print(n, f"{s.w}x{s.h}", f"{len(cols)} colores", "AVISO " + str(mal) if mal else "OK")
    piezas["placa_sagunto"].img.resize((piezas["placa_sagunto"].w * 2, 22), Image.NEAREST).save(OUT / "placa_sagunto_x2.png")

    # Prueba: andén (hormigón) con la línea a 2 px del borde, vía (balasto) con el hollín, y la
    # fachada en enfoscado #b4a080 con zócalo #605c48, la placa y el reloj. A x1 y a x4.
    E = 4
    W, H = 192, 96
    esc = Image.new("RGBA", (W, H), PAL["navy"])
    hor = a7.hormigon().img
    bal = a7.balasto().img
    for x in range(0, W, 64):
        esc.paste(bal, (x, 56))                           # vía abajo
        esc.paste(hor.crop((0, 0, 64, 20)), (x, 36))       # andén
    esc.alpha_composite(tileable_x(piezas["decal_via_hollin_64x32"].img), (0, 60))
    esc.alpha_composite(tileable_x(piezas["decal_linea_seguridad_64x2"].img), (0, 52))
    fach = Image.new("RGBA", (W, 36), PAL["beige"])        # fachada (enfoscado #b4a080)
    fach.paste(Image.new("RGBA", (W, 6), PAL["olive_grey"]), (0, 30))   # zócalo #605c48
    for x in range(14, W, 40):                            # ventanas en #283440
        fach.paste(Image.new("RGBA", (8, 14), PAL["slate_dark"]), (x, 10))
    esc.paste(fach, (0, 0))
    p = piezas["placa_sagunto"].img
    esc.alpha_composite(p, ((W - p.width) // 2, 2))
    esc.alpha_composite(piezas["reloj_fachada_16"].img, ((W - 16) // 2, 14))
    esc.save(OUT / "prueba_sagunto_x1.png")
    esc.resize((W * E, H * E), Image.NEAREST).save(OUT / "prueba_sagunto_x4.png")

    hoja = Image.new("RGBA", (8 + 35 * 6 + 8 + 24 * 6 + 8 + 16 * 6 + 8, 8 + 24 * 6 + 8 + 32 * 4 + 8 + 2 * 6 + 8), PAL["navy"])
    hoja.alpha_composite(p.resize((p.width * 6, p.height * 6), Image.NEAREST), (8, 8))
    r24 = piezas["reloj_fachada_24"].img
    hoja.alpha_composite(r24.resize((144, 144), Image.NEAREST), (8 + 35 * 6 + 8, 8))
    hoja.alpha_composite(piezas["reloj_fachada_16"].img.resize((96, 96), Image.NEAREST), (8 + 35 * 6 + 8 + 144 + 8, 8))
    vh = Image.new("RGBA", (64 * 2, 32), (0, 0, 0, 0))
    vh.paste(bal, (0, 0)); vh.paste(bal, (64, 0))
    vh.alpha_composite(tileable_x(piezas["decal_via_hollin_64x32"].img, 2))
    hoja.alpha_composite(vh.resize((128 * 4, 128), Image.NEAREST), (8, 8 + 144 + 8))
    ls = Image.new("RGBA", (128, 2), PAL["concrete"])
    ls.alpha_composite(tileable_x(piezas["decal_linea_seguridad_64x2"].img, 2))
    hoja.alpha_composite(ls.resize((128 * 4, 12), Image.NEAREST), (8, 8 + 144 + 8 + 128 + 8))
    hoja.save(OUT / "contacto_sagunto.png")

    # Prueba de los decals de §3 (v1.1): pintadas sobre el muro (hormigón A7) y sobre la fachada,
    # hollín de la puerta oeste sobre balasto y tierra. x1 y x3.
    txt, fle = piezas["decal_pintada_aqui_hay_vivos"].img, piezas["decal_pintada_flecha"].img
    W2 = txt.width + fle.width + 24
    muro = Image.new("RGBA", (W2, 64), PAL["navy"])
    for x in range(0, W2, 64):
        muro.paste(hor, (x, 0))
    muro.paste(Image.new("RGBA", (W2, 32), PAL["beige"]), (0, 32))
    for y0 in (4, 36):
        muro.alpha_composite(txt, (4, y0))
        muro.alpha_composite(fle, (txt.width + 12, y0 + 2))
    suelo = Image.new("RGBA", (64 * 4, 64), PAL["navy"])
    tie = a7.tierra().img
    for i, (fo, dv) in enumerate(((bal, 0), (bal, 1), (tie, 0), (tie, 1))):
        b = fo.copy()
        b.alpha_composite(piezas[f"decal_hollin_puerta_{dv + 1}_64"].img)
        suelo.paste(b, (64 * i, 0))
    p2 = Image.new("RGBA", (max(W2, 256) * 3 + 16, (64 + 64) * 3 + 24), PAL["navy"])
    p2.paste(muro.resize((W2 * 3, 192), Image.NEAREST), (8, 8))
    p2.paste(suelo.resize((768, 192), Image.NEAREST), (8, 16 + 192))
    p2.save(OUT / "prueba_decals_v11_x3.png")
    muro.save(OUT / "prueba_pintadas_x1.png")
    suelo.save(OUT / "prueba_hollin_puerta_x1.png")
