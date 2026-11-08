"""Fondo de la intro «Día 41» (D-N158, variante C). Bruno (Arte técnico), 8-nov.

Encargo de Mak (10:32): Eugenio quiere el fondo ya dentro de las tarjetas. IntroVista (Iker, c6df090) lo carga de
Resources/Intro/fondo_intro con ScaleAndCrop y pone el texto en hueso d4d0cc, centrado, en la caja de y 30–70 % y
ancho 70 %. La letra va a ser más gorda y de máquina de escribir, así que la composición deja una banda tranquila
(y 22–76 %, x 8–92 %) en navy liso o casi liso: todo lo que tiene detalle va arriba (luna, estrellas) o abajo (horizonte
a y >= 272 de 360, tren, vía, ciudad). Contraste del hueso sobre navy: unos 11:1.

Base de 640x360 en la paleta de 32 (x3 = 1920x1080, vecino más próximo). MUESTRA de 5 variantes (A–E):
  A. «La ciudad apagada»: el perfil de Valencia sin una luz, tres columnas de humo, y el tren con el faro por la vía.
  B. «Catenaria»: postes de catenaria hacia el punto de fuga y la luna; nada más se mueve.
  C. «Sagunto desde fuera»: el cerro del castillo, el fuerte con sus focos y el tren llegando.
  D. «El faro»: la vía de frente al punto de fuga y el faro del tren que se acerca, muy pequeño.
  E. «Andén vacío»: la marquesina de la estación a contraluz, con un solo farol encendido.
Uso:
  python build_intro.py <carpeta> --muestra      # las 5, con el texto de la tarjeta 2 en la letra de prueba
  python build_intro.py <carpeta> [--var A]      # exporta fondo_intro.png (1920x1080) con la variante elegida
Colores: noche en navy, slate y purple_grey (la excepción del navy en el cielo es la del menú, D-N115); siluetas en
slate_dark y olive_shadow; 1c200c y 442c0c en nada (son de la horda); el ámbar en nada; luces en cream y ochre_dark.
"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL  # noqa: E402
from build_d_vfx import BAYER, _h  # noqa: E402
import build_menu as bm  # noqa: E402

W, H = 640, 360
HZ = 276                       # horizonte: por debajo de la caja del texto (y 252 a 1080/3)
BANDA = (int(H * .22), int(H * .76))
TARJETAS = [
    "Día 41 desde el brote.",
    "Ya nadie dice el año. Las ciudades cayeron en dos semanas; las carreteras, en tres.",
    "Lo único que todavía se mueve es el tren.",
    "Desde Sagunto, el Mando Ferroviario de Emergencia reúne lo que queda: tropa, vagones y vía.",
    "Reparar la red es recuperar el país. Tramo a tramo.",
]


def _pon(px, x, y, c):
    if 0 <= x < W and 0 <= y < H:
        px[int(x), int(y)] = PAL[c]


def cielo_calmo(px, hz, luna=None):
    """Cielo de noche con la banda del texto en navy: el degradado empieza por debajo de la banda."""
    tonos = ["navy", "navy", "slate", "purple_grey"]
    y0 = BANDA[1] - 10
    for y in range(hz):
        t = 0.0 if y < y0 else ((y - y0) / max(1, hz - y0)) ** 1.6 * (len(tonos) - 1)
        i, fr = int(t), t - int(t)
        for x in range(W):
            px[x, y] = PAL[tonos[min(i + (1 if fr * 16 > BAYER[y % 4][x % 4] else 0), len(tonos) - 1)]]
    for k in range(90):                                   # estrellas solo arriba, fuera de la banda
        x, y = int(_h(k, 0, 4101) * W), int(_h(k, 1, 4101) * (BANDA[0] - 6))
        px[x, y] = PAL["bone" if k % 6 else "grey_light"]
    for k in range(18):                                   # y unas pocas, muy tenues, en los laterales de la banda
        x = int(_h(k, 2, 4102) * W * .08) if k % 2 else W - 1 - int(_h(k, 2, 4102) * W * .08)
        y = int(BANDA[0] + _h(k, 3, 4102) * (BANDA[1] - BANDA[0]))
        px[x, y] = PAL["slate"]
    if luna:
        lx, ly, r = luna
        for y in range(ly - r - 6, ly + r + 7):
            for x in range(lx - r - 6, lx + r + 7):
                d = math.hypot(x - lx, y - ly)
                if d <= r:
                    c = "bone"
                    for (mx, my, mr) in ((.3, .2, .22), (-.35, -.25, .14), (-.1, .45, .1)):
                        if math.hypot(x - lx - mx * r, y - ly - my * r) < mr * r and (x + y) % 2 == 0:
                            c = "grey_light"
                    _pon(px, x, y, c)
                elif d <= r + 5 and (x + y) % 2 == 0 and d - r < 5 * (1 - BAYER[y % 4][x % 4] / 16):
                    _pon(px, x, y, "slate")


def suelo(px, hz):
    for y in range(hz, H):
        t = (y - hz) / max(1, H - hz)
        for x in range(W):
            px[x, y] = PAL["olive_shadow" if t * 16 > BAYER[y % 4][x % 4] * .5 + 1 else "slate_dark"]


def via_frente(px, fx, hz, ancho=170, traviesas=34):
    """Vía hacia el punto de fuga (fx, hz): traviesas en grey_brown y carriles en concrete_light."""
    for y in range(hz, H):
        t = (y - hz) / (H - hz)
        if int(t * traviesas) != int((y - 1 - hz) / (H - hz) * traviesas):
            for x in range(int(fx - 3 - t * ancho * 1.12), int(fx + 3 + t * ancho * 1.12)):
                _pon(px, x, y, "grey_brown")
        for s in (-1, 1):
            _pon(px, fx + s * (1 + t * ancho), y, "concrete_light")
            if t > .4:
                _pon(px, fx + s * (1 + t * ancho) + s, y, "concrete")


def humo(px, x0, y0, alto, semilla, ancho=6):
    """Columna de humo que sube y se tuerce con el viento; tramada, nunca negra. Se apaga antes de entrar en la banda."""
    for k in range(alto):
        y = y0 - k
        techo = 214                                       # por encima de aquí estaría la caja del texto: el humo se tumba
        if y < techo:
            break
        sube = min(k, y0 - techo)
        cx = x0 + sube * .35 + 4 * math.sin(k / 9 + semilla) + max(0, k - (y0 - techo) * .6) * 2.2
        w = ancho + k * .18
        for x in range(int(cx - w), int(cx + w) + 1):
            dentro = abs(x - cx) / w
            dens = (1 - dentro) * (1 - k / max(alto, y0 - techo + 1)) * 16
            if dens > BAYER[y % 4][x % 4] + 2 and _h(x, y, int(semilla * 10)) > .15:
                _pon(px, x, y, "purple_grey" if dens > 9 else "slate")


def ciudad(px, x0, x1, hz, semilla=4103):
    """Perfil de la ciudad sin una luz: bloques de pisos, una torre, grúas y antenas."""
    x = x0
    while x < x1:
        w = 6 + int(_h(x, 0, semilla) * 16)
        alto = 8 + int(_h(x, 1, semilla) ** 2 * 30)
        for xx in range(x, min(x1, x + w)):
            for y in range(hz - alto, hz):
                _pon(px, xx, y, "slate_dark")
        if _h(x, 2, semilla) > .8:                           # antena
            for y in range(hz - alto - 6, hz - alto):
                _pon(px, x + w // 2, y, "slate_dark")
        if _h(x, 3, semilla) > .9:                           # bloque roto: mordisco en la esquina
            for y in range(hz - alto, hz - alto + 4):
                for xx in range(x + w - 3, x + w):
                    _pon(px, xx, y, "slate")
        x += w + (1 if _h(x, 4, semilla) > .6 else 0)
    gx = x0 + (x1 - x0) // 3                                 # grúa parada
    for y in range(hz - 54, hz):
        _pon(px, gx, y, "slate_dark")
    for xx in range(gx - 10, gx + 26):
        _pon(px, xx, hz - 54, "slate_dark")
    for y in range(hz - 54, hz - 44):
        _pon(px, gx + 22, y, "slate_dark")


def poste_catenaria(px, x, ybase, alto, brazo, lado):
    g = max(1, alto // 40)                                   # grosor según la distancia
    for y in range(ybase - alto, ybase):
        for d in range(g):
            _pon(px, x + d, y, "grey_brown" if y > HZ else "slate_dark")
        if g > 1:
            _pon(px, x + g, y, "olive_grey" if y > HZ else "slate")   # filo de luna
    for k in range(brazo):
        for d in range(g):
            _pon(px, x + lado * k, ybase - alto + 2 + d, "slate_dark")
    _pon(px, x + lado * brazo, ybase - alto + 3, "concrete")


def fondo(var):
    im = Image.new("RGBA", (W, H))
    px = im.load()
    if var == "A":                                           # la ciudad apagada
        cielo_calmo(px, HZ, (566, 44, 13))
        bm.sierra(px, HZ, castillo_x=560, escala=.55)       # la sierra detrás; la ciudad, delante
        ciudad(px, 10, 330, HZ + 4)
        for (x, y, a, s) in ((70, HZ - 30, 120, 1.0), (170, HZ - 24, 150, 2.3), (250, HZ - 20, 95, 3.1)):
            humo(px, x, y, a, s)
        suelo(px, HZ + 4)
        bm.tren(px, 300, HZ + 40, faro=True, n=2)
        for x in range(W):                                   # segunda vía y balasto
            _pon(px, x, HZ + 41, "grey_brown")
            _pon(px, x, HZ + 58, "concrete")
    elif var == "B":                                         # catenaria
        cielo_calmo(px, HZ, (100, 46, 15))
        bm.sierra(px, HZ, castillo_x=470, escala=.6)
        suelo(px, HZ + 4)
        fx = 330
        via_frente(px, fx, HZ + 4, 190)
        for k in range(9):
            t = (k + 1) / 10
            y = int(HZ + 4 + t * t * (H - HZ - 4) * 1.4)
            if y > H + 40:
                break
            alto = int(6 + t * t * 105)
            xb = int(fx - 6 - t * t * 300)
            poste_catenaria(px, xb, min(y, H), alto, int(fx - xb + 2), 1)
        for x in range(fx - 300, fx):                        # el hilo de contacto, cayendo hacia el punto de fuga
            pass
    elif var == "C":                                         # Sagunto desde fuera
        cielo_calmo(px, HZ, (90, 42, 12))
        bm.sierra(px, HZ, castillo_x=430, escala=.75)
        suelo(px, HZ + 6)
        bm.fuerte(px, 360, HZ + 2, 170)
        bm.foco(px, 368, HZ - 27, math.radians(195), 120)
        bm.tren(px, 140, HZ + 34, faro=True, n=2)
    elif var == "D":                                         # el faro que se acerca
        cielo_calmo(px, HZ, None)
        bm.sierra(px, HZ, castillo_x=150, escala=.6)
        suelo(px, HZ + 4)
        fx = 320
        via_frente(px, fx, HZ + 4, 200)
        for dy in range(-3, 1):                              # el tren, un punto con su faro
            for dx in range(-3, 4):
                _pon(px, fx + dx, HZ + 2 + dy, "slate_dark")
        _pon(px, fx, HZ + 1, "cream"); _pon(px, fx - 1, HZ + 1, "cream")
        for r in range(2, 9):                                # halo tramado del faro
            for a in range(0, 360, 12):
                x, y = fx + r * math.cos(math.radians(a)), HZ + 1 + r * .6 * math.sin(math.radians(a))
                if (int(x) + int(y)) % 2 == 0 and r < 9 - BAYER[int(y) % 4][int(x) % 4] / 3:
                    _pon(px, x, y, "beige" if r < 4 else "concrete")
    else:                                                    # E: andén vacío
        cielo_calmo(px, HZ, (540, 50, 14))
        bm.sierra(px, HZ, castillo_x=200, escala=.7)
        suelo(px, HZ + 4)
        ytecho = HZ - 20
        for x in range(60, 600):                             # marquesina a contraluz
            for y in range(ytecho - 4, ytecho + 6):
                _pon(px, x, y, "slate_dark" if y > ytecho - 4 else "slate")
            if (x - 60) % 60 == 0:
                for y in range(ytecho + 5, HZ + 30):
                    _pon(px, x, y, "slate_dark"); _pon(px, x + 1, y, "slate_dark")
        for x in range(40, 620):                             # andén
            for y in range(HZ + 30, HZ + 36):
                _pon(px, x, y, "grey_brown" if y > HZ + 30 else "olive_grey")
        lx = 420                                             # el único farol encendido
        _pon(px, lx, ytecho + 6, "cream"); _pon(px, lx + 1, ytecho + 6, "cream")
        for d in range(2, 30):
            semi = 2 + d * .9
            for k in range(-int(semi), int(semi) + 1):
                x, y = lx + k, ytecho + 6 + d
                if (x + y) % 2 == 0 and abs(k) < semi * (1 - BAYER[y % 4][x % 4] / 32):
                    _pon(px, x, y, "ochre_dark" if d < 14 else "olive_grey")
        for x in range(W):
            _pon(px, x, HZ + 50, "concrete")
            _pon(px, x, HZ + 66, "concrete")
    return im


def x3(im, k=3):
    return im.resize((im.width * k, im.height * k), Image.NEAREST)


def muestra_texto(im, s):
    """Simula la letra futura (máquina de escribir, gruesa) en la caja de IntroVista, a 1080p, para ver el hueco."""
    d = ImageDraw.Draw(im)
    f = ImageFont.truetype("/usr/share/fonts/truetype/freefont/FreeMonoBold.ttf", 54)
    wcaja = min(im.width * .7, im.height * 1.4)
    palabras, lineas, l = s.split(), [], ""
    for p in palabras:
        if d.textlength((l + " " + p).strip(), font=f) > wcaja:
            lineas.append(l); l = p
        else:
            l = (l + " " + p).strip()
    lineas.append(l)
    alto = 64 * len(lineas); y = im.height * .5 - alto / 2
    for ln in lineas:
        d.text(((im.width - d.textlength(ln, font=f)) / 2, y), ln, font=f, fill=PAL["bone"])
        y += 64


def main():
    out = Path(sys.argv[1] if len(sys.argv) > 1 else "intro"); out.mkdir(parents=True, exist_ok=True)
    if "--muestra" in sys.argv:
        hoja = Image.new("RGBA", (1920 * 2 // 2, 1080 * 3 // 2 + 60), PAL["black"])
        for i, v in enumerate("ABCDE"):
            im = x3(fondo(v)); im.save(out / f"intro_fondo_{v}.png")
            con = im.copy(); muestra_texto(con, TARJETAS[1]); con.save(out / f"intro_fondo_{v}_texto.png")
        cols = [Image.open(out / f"intro_fondo_{v}_texto.png").resize((640, 360), Image.LANCZOS) for v in "ABCDE"]
        hoja = Image.new("RGBA", (640 * 2 + 30, 360 * 3 + 40), PAL["black"])
        d = ImageDraw.Draw(hoja); f = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 14)
        for i, c in enumerate(cols):
            x, y = 10 + (i % 2) * 650, 10 + (i // 2) * 370
            hoja.alpha_composite(c, (x, y)); d.text((x + 6, y + 4), "ABCDE"[i], font=f, fill=PAL["cream"])
        hoja.save(out / "intro_fondo_hoja.png")
    var = sys.argv[sys.argv.index("--var") + 1] if "--var" in sys.argv else "A"
    x3(fondo(var)).convert("RGB").save(out / "fondo_intro.png")
    print("fondo_intro.png =", var)


if __name__ == "__main__":
    main()
