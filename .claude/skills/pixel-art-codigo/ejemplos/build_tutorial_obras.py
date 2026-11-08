"""Anillo que late del tutorial y contornos de la capa de obras (D-N154, D-N163). Bruno (Arte técnico), 8-nov 18:00.

Encargo de Lucía (tutorial-textos.md y construccion-reparar.md §1 y §9). MUESTRA de 5 estilos de cada cosa.

1) ANILLO QUE LATE (tutorial): resalta lo que hay que tocar. Hueso d4d0cc, nada de rojo. Con el estilo de la interfaz v2
   (D-N147): los marcos no son imágenes; se dibujan con rectángulos nítidos (IMGUI/USS) a cualquier escala. Por eso la muestra
   sale de la misma geometría que tendrá el C# (unidad u = alto/360, es decir, 3 px a 1080p). Ciclo de 1,2 s, 6 fotogramas.
     A. Marco que respira (lo que hay ahora en TutorialHud.Ring, como referencia).
     B. Esquinas en L que se cierran sobre el objetivo.
     C. Marco fijo y un eco que sale y se apaga (sonar).
     D. Esquinas fijas y un eco que sale (B + C).
     E. Marco fijo con una cuña que apunta desde fuera y se mueve 2 u.
   Más la flecha de borde (tutorial-refuerzos.md): cuando el objetivo queda fuera de la pantalla, una cuña de 14x16 en el borde.

2) CAPA DE OBRAS (modo reparar): sano sin marca; dañado con contorno ámbar e08c30; muy dañado con contorno ámbar que late;
   brecha con relleno rojo 982420 (permitido: es una brecha). Vista de 3/4 sobre el suelo de noche, un muro de 6 tramos.
     A. Contorno de 2 u alrededor de la huella del tramo.
     B. Contorno de 1 u y tramado al 25 % dentro (se lee como zona).
     C. Solo esquinas en L en cada tramo (menos ruido con muchos tramos).
     D. Banda en el suelo delante del tramo, por el lado de dentro (no tapa el modelo).
     E. Contorno de 2 u y una barra de vida de 1 u encima del tramo.
   Muy dañado late entre ámbar sólido y ámbar tramado al 50 % (0,6 s), nunca se apaga del todo.
   Icono de ruido (§3, D-N123): pala y ondas, en ámbar de aviso, 16 y 32 px, en 3 variantes.

Uso: python build_tutorial_obras.py <carpeta>
"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import BAYER, _h  # noqa: E402

F = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
U = 3                                   # unidad a 1080p (alto/360)
FR = 6                                  # fotogramas del ciclo
FONDO, TARJETA, BORDE = (0x18, 0x1c, 0x30), (0x20, 0x25, 0x38), (0x35, 0x3a, 0x55)
HUESO, AMBAR, ROJO = PAL["bone"], PAL["amber"], PAL["red_bright"]


def rect(d, x, y, w, h, c):
    if w > 0 and h > 0:
        d.rectangle([x, y, x + w - 1, y + h - 1], fill=c)


def marco(d, x, y, w, h, th, c):
    rect(d, x, y, w, th, c); rect(d, x, y + h - th, w, th, c)
    rect(d, x, y, th, h, c); rect(d, x + w - th, y, th, h, c)


def esquinas(d, x, y, w, h, th, largo, c):
    for (cx, cy, sx, sy) in ((x, y, 1, 1), (x + w, y, -1, 1), (x, y + h, 1, -1), (x + w, y + h, -1, -1)):
        x0 = cx if sx > 0 else cx - largo
        y0 = cy if sy > 0 else cy - th
        rect(d, x0, y0, largo, th, c)
        x1 = cx if sx > 0 else cx - th
        y1 = cy if sy > 0 else cy - largo
        rect(d, x1, y1, th, largo, c)


def mezcla(c, a, fondo=TARJETA):
    return tuple(int(fondo[i] + (c[i] - fondo[i]) * a) for i in range(3))


# ---------------------------------------------------------------- 1) anillo
def anillo(d, r, var, f):
    """r = (x, y, w, h) del objetivo; f = fotograma 0..FR-1. Misma lógica que el C#."""
    x, y, w, h = r
    t = f / FR
    pulso = 0.5 + 0.5 * math.sin(t * 2 * math.pi)
    th = U
    if var == "A":
        g = int((4 + 6 * pulso) * U / 3)
        marco(d, x - g, y - g, w + 2 * g, h + 2 * g, th, mezcla(HUESO, .55 + .45 * pulso, FONDO))
    elif var == "B":
        g = int(round((1 - pulso) * 4)) * U + U
        esquinas(d, x - g - th, y - g - th, w + 2 * (g + th), h + 2 * (g + th), th, 4 * U, HUESO)
    elif var == "C":
        marco(d, x - 2 * U, y - 2 * U, w + 4 * U, h + 4 * U, th, HUESO)
        e = int(t * 8) * U + 3 * U
        a = 1 - t
        if a > .15:
            marco(d, x - e, y - e, w + 2 * e, h + 2 * e, U if t > .4 else th, mezcla(HUESO, a * .8, FONDO))
    elif var == "D":
        esquinas(d, x - 3 * U, y - 3 * U, w + 6 * U, h + 6 * U, th, 4 * U, HUESO)
        e = int(t * 8) * U + 4 * U
        a = 1 - t
        if a > .15:
            esquinas(d, x - e, y - e, w + 2 * e, h + 2 * e, U, 3 * U, mezcla(HUESO, a * .8, FONDO))
    else:
        marco(d, x - 2 * U, y - 2 * U, w + 4 * U, h + 4 * U, th, HUESO)
        off = int(round(pulso * 2)) * U
        cx, cy = x + w // 2, y - 2 * U - off - 2 * U          # cuña encima, apuntando hacia abajo
        for k in range(5):
            rect(d, cx - (5 - k) * U, cy - (5 - k) * U, 2 * (5 - k) * U, U, HUESO)


def flecha_borde(d, x, y, dirx, c=HUESO):
    """Cuña de 14x16 (a 1080p) en el borde, apuntando hacia fuera; con un 'paso' 2 px que late."""
    for k in range(8):
        hh = 16 - 2 * k
        xx = x + (k if dirx > 0 else -k)
        rect(d, xx, y + (16 - hh) // 2, 2, hh, c)


def muestra_anillo(out):
    fnt = ImageFont.truetype(F, 13)
    fl = ImageFont.truetype(F, 18)
    cw, ch = 300, 150
    hoja = Image.new("RGB", (cw * FR + 20 + 60, ch * 5 + 70), (0, 0, 0))
    dh = ImageDraw.Draw(hoja)
    dh.text((10, 8), "Anillo que late · 6 fotogramas (1,2 s) · 1080p real · objetivo: botón de orden de 146x30 (interfaz v2)", font=fnt, fill=PAL["cream"])
    gifs = {}
    for vi, v in enumerate("ABCDE"):
        frames = []
        for f in range(FR):
            im = Image.new("RGB", (cw, ch), FONDO)
            d = ImageDraw.Draw(im)
            bx, by = 77, 60
            rect(d, bx, by, 146, 30, TARJETA); marco(d, bx, by, 146, 30, 1, BORDE)
            d.text((bx + 32, by + 7), "Preparar tren", font=fnt, fill=HUESO)
            rect(d, bx + 8, by + 7, 16, 16, PAL["olive"])
            anillo(d, (bx, by, 146, 30), v, f)
            frames.append(im)
            hoja.paste(im, (60 + f * cw, 40 + vi * ch))
        dh.text((20, 40 + vi * ch + ch // 2 - 10), v, font=fl, fill=PAL["cream"])
        frames[0].save(out / f"anillo_{v}.gif", save_all=True, append_images=frames[1:] + frames[:0], duration=200, loop=0)
        gifs[v] = frames
    hoja.save(out / "anillo_muestra.png")
    # flecha de borde
    im = Image.new("RGB", (360, 120), FONDO); d = ImageDraw.Draw(im)
    flecha_borde(d, 340, 30, 1); d.text((220, 33), "Nules · 2,1 km", font=fnt, fill=HUESO)
    flecha_borde(d, 6, 70, -1); d.text((30, 73), "Segorbe", font=fnt, fill=HUESO)
    im.save(out / "flecha_borde_muestra.png")


# ---------------------------------------------------------------- 2) capa de obras (escena en 640x360, x3)
W, H = 640, 360
TRAMOS = [("sano", 1.0), ("danado", .55), ("muy", .2), ("brecha", 0.0), ("danado", .4), ("sano", .9)]


def suelo(px):
    for y in range(H):
        for x in range(W):
            n = _h(x // 2, y // 2, 5101)
            px[x, y] = PAL["olive_grey" if n > .82 else "grey_brown" if n > .25 else "olive_shadow"]


def tramo_modelo(px, x0, y0, largo, estado):
    """Muro de sacos en 3/4: cara de arriba beige (D-N57), costado olive_dark, contorno."""
    alto = 10 if estado != "muy" else 5
    if estado == "brecha":
        for k in range(26):                               # escombro bajo
            x = x0 + int(_h(k, 0, 5102) * largo); y = y0 + 6 + int(_h(k, 1, 5102) * 6)
            for dx in range(3):
                px[min(W - 1, x + dx), y] = PAL["olive_dark" if k % 3 else "beige"]
        return
    for x in range(x0, x0 + largo):
        for y in range(y0 + 12 - alto, y0 + 12):
            px[x, y] = PAL["olive_dark"]
        for y in range(y0 + 12 - alto - 5, y0 + 12 - alto):
            px[x, y] = PAL["beige" if (x // 5 + y) % 4 else "olive_light"]
        if estado != "sano" and _h(x, 0, 5103) > (.7 if estado == "danado" else .45):
            for y in range(y0 + 12 - alto - 5, y0 + 12 - alto - 2):
                px[x, y] = PAL["olive_dark"]
    for x in range(x0, x0 + largo):
        px[x, y0 + 12 - alto - 6] = PAL["olive_shadow"]; px[x, y0 + 12] = PAL["olive_shadow"]


def tram(px, pts, c, paso=1):
    for (x, y) in pts:
        if 0 <= x < W and 0 <= y < H and paso:
            px[x, y] = c


def capa(px, x0, y0, largo, estado, var, f):
    if estado == "sano":
        return
    t = f / FR
    late = estado == "muy"
    fuerte = (not late) or t < .5                          # muy dañado: 0,6 s sólido y 0,6 s tramado
    c = ROJO if estado == "brecha" else AMBAR
    x1, y1, ya = x0 - 2, y0 - 9, y0 + 16                   # huella del tramo con 2 px de margen
    if estado == "brecha":                                 # relleno rojo tramado y borde sólido, igual en todos los estilos
        for y in range(y1, ya):
            for x in range(x1, x0 + largo + 2):
                if BAYER[y % 4][x % 4] < 6:
                    px[x, y] = c
        for x in range(x1, x0 + largo + 2):
            px[x, y1] = c; px[x, ya - 1] = c
        for y in range(y1, ya):
            px[x1, y] = c; px[x0 + largo + 1, y] = c
        return

    def pon(x, y):
        if 0 <= x < W and 0 <= y < H and (fuerte or (x + y) % 2 == 0):
            px[x, y] = c
    if var in ("A", "E"):
        for g in range(2 if var == "A" else 1):
            for x in range(x1 - g, x0 + largo + 2 + g):
                pon(x, y1 - g); pon(x, ya - 1 + g)
            for y in range(y1 - g, ya + g):
                pon(x1 - g, y); pon(x0 + largo + 1 + g, y)
        if var == "E":
            vida = dict(TRAMOS_V).get((x0, estado), .5)
            for x in range(x1, x0 + largo + 2):
                px[x, y1 - 5] = PAL["black"]; px[x, y1 - 3] = PAL["black"]
            for x in range(x1, x0 + largo + 2):
                px[x, y1 - 4] = PAL["black"]
            for x in range(x1 + 1, x1 + 1 + int((largo + 1) * vida)):
                px[x, y1 - 4] = c
    elif var == "B":
        for x in range(x1, x0 + largo + 2):
            pon(x, y1); pon(x, ya - 1)
        for y in range(y1, ya):
            pon(x1, y); pon(x0 + largo + 1, y)
            for x in range(x1 + 1, x0 + largo + 1):
                if BAYER[y % 4][x % 4] < 4 and (fuerte or BAYER[y % 4][x % 4] < 2):
                    px[x, y] = c
    elif var == "C":
        L = 6
        for (cx, cy, sx, sy) in ((x1, y1, 1, 1), (x0 + largo + 1, y1, -1, 1), (x1, ya - 1, 1, -1), (x0 + largo + 1, ya - 1, -1, -1)):
            for k in range(L):
                for g in range(2):
                    pon(cx + sx * k, cy + sy * g); pon(cx + sx * g, cy + sy * k)
    else:                                                  # D: banda en el suelo, por dentro (abajo en pantalla)
        for y in range(ya + 1, ya + 5):
            for x in range(x0, x0 + largo):
                if y in (ya + 1, ya + 4) or (x - y) % 6 < 3:
                    pon(x, y)


TRAMOS_V = []


def escena(var, f):
    im = Image.new("RGBA", (W, H)); px = im.load()
    suelo(px)
    x = 40; y0 = 150; largo = 86
    TRAMOS_V.clear()
    for (estado, vida) in TRAMOS:
        TRAMOS_V.append(((x, estado), vida))
        x += largo + 4
    x = 40
    for (estado, vida) in TRAMOS:
        tramo_modelo(px, x, y0, largo, estado)
        x += largo + 4
    x = 40
    for (estado, vida) in TRAMOS:
        capa(px, x, y0, largo, estado, var, f)
        x += largo + 4
    for k in range(40):                                    # horda fuera (arriba), para ver el contraste
        hx = int(30 + _h(k, 0, 5104) * 580); hy = int(90 + _h(k, 1, 5104) * 40)
        for yy in range(hy - 9, hy):
            for xx in range(hx, hx + 3):
                px[xx, yy] = PAL["olive_black"]
    return im


def ruido(n, var):
    s = Sprite(n, n)
    k = n / 32
    def R(x, y, w, h, c):
        s.rect(int(x * k), int(y * k), max(1, int(w * k)), max(1, int(h * k)), PAL[c])
    # pala: mango en diagonal, empuñadura en T y hoja en punta
    for i in range(14):
        R(4 + i * .7, 3 + i, 2, 2, "beige")
    R(2, 2, 6, 2, "olive_yellow")
    for j in range(8):
        w = 8 - abs(j - 3) if j > 3 else 6
        R(11 + (6 - w) / 2 + j * .5, 17 + j, w, 1, "concrete_light" if j < 6 else "concrete")
    cols = {"A": "amber", "B": "amber", "C": "bone"}
    c = cols[var]
    for i, r in enumerate((6, 10, 14) if var != "B" else (7, 12)):    # ondas que salen de la hoja, hacia arriba a la derecha
        for a in range(-80, 11, 6):
            x = 16 + r * math.cos(math.radians(a)); y = 20 + r * math.sin(math.radians(a))
            R(x, y, 2, 2, c)
    if var == "C":
        R(25, 1, 5, 5, "amber")
    s.outline()
    return s


def muestra_obras(out):
    fnt = ImageFont.truetype(F, 16)
    hoja = Image.new("RGB", (W * 2 + 30, H * 3 + 60), (0, 0, 0))
    d = ImageDraw.Draw(hoja)
    d.text((10, 8), "Capa de obras · tramos: sano, dañado, muy dañado (late), brecha, dañado, sano · horda arriba (fuera) · base 640x360", font=fnt, fill=PAL["cream"])
    for i, v in enumerate("ABCDE"):
        frames = [escena(v, f) for f in range(FR)]
        frames[0].resize((W * 3, H * 3), Image.NEAREST).save(out / f"obras_{v}.png")
        [fr.resize((W * 2, H * 2), Image.NEAREST).convert("RGB") for fr in frames][0].save(
            out / f"obras_{v}.gif", save_all=True, append_images=[fr.resize((W * 2, H * 2), Image.NEAREST).convert("RGB") for fr in frames[1:]], duration=200, loop=0)
        cx, cy = 10 + (i % 2) * (W + 10), 36 + (i // 2) * (H + 8)
        zona = frames[2].crop((30, 75, 590, 195)).resize((W, int(120 * W / 560)), Image.NEAREST).convert("RGB")
        hoja.paste(zona, (cx, cy + 20))
        zona2 = frames[4].crop((210, 130, 410, 175)).resize((W // 2, int(45 * W / 400)), Image.NEAREST).convert("RGB")
        hoja.paste(zona2, (cx, cy + 20 + zona.height + 6))
        d.text((cx + W // 2 + 10, cy + 40 + zona.height), "abajo: muy dañado en su\nfotograma tramado (late)", font=fnt, fill=PAL["cream"]); d.text((cx + 6, cy + 4), v, font=fnt, fill=PAL["cream"])
    # icono de ruido en la última casilla
    cx, cy = 10 + W + 10, 36 + 2 * (H + 8)
    d.rectangle([cx, cy, cx + W - 1, cy + H - 1], fill=FONDO)
    d.text((cx + 10, cy + 10), "Icono de ruido (A, B, C) a 32 y 16 px, x4", font=fnt, fill=PAL["cream"])
    for j, v in enumerate("ABC"):
        for n, e in ((32, 4), (16, 4)):
            s = ruido(n, v)
            s.save(out / f"ui_obra_ruido_{v}_{n}.png")
            im = s.img.resize((n * e, n * e), Image.NEAREST)
            hoja.paste(im, (cx + 20 + j * 200 + (0 if n == 32 else 140), cy + 60 + (0 if n == 32 else 40)), im)
        d.text((cx + 20 + j * 200, cy + 200), v, font=fnt, fill=PAL["cream"])
    hoja.save(out / "obras_muestra.png")


def main():
    out = Path(sys.argv[1] if len(sys.argv) > 1 else "tutorial-obras"); out.mkdir(parents=True, exist_ok=True)
    muestra_anillo(out)
    muestra_obras(out)
    print("hecho", out)


if __name__ == "__main__":
    main()
