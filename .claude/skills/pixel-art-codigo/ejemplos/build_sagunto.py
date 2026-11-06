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
    locomotoras en olive_shadow y olive_black, motas sueltas en el borde y goterones de aceite
    alineados con el eje. Escala: la misma que el balasto (64 px de lado)."""
    s = Sprite(64, 32)
    for y in range(32):
        perfil = 0.75 - abs(y - 15.5) / 14                # más denso en el eje, pero a manchas
        for x in range(64):
            n = ruido_periodico(x, y, 16, 610, 64) * 0.6 + ruido_periodico(x, y, 4, 611, 64) * 0.4
            v = perfil + (n - 0.5) * 1.5
            if v > 0.55:
                s.set(x, y, "olive_black" if v > 0.85 else "olive_shadow")
            elif v > 0.3 and _h(x, y, 612) > 0.85:
                s.set(x, y, "olive_shadow")               # mota suelta de hollín
    for k in range(5):                                    # goterones de aceite en el eje
        x = int(4 + k * 12.5 + _h(k, 0, 613) * 4) % 64
        y = 14 + int(_h(k, 1, 613) * 4)
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)) if k % 2 else ((0, 0), (1, 0)):
            s.set((x + dx) % 64, y + dy, "olive_black")
        if _h(k, 2, 613) > 0.5:
            s.set((x + 2) % 64, y, "slate_dark")           # brillo mate del aceite
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
