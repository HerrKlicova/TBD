"""Menú principal: Sagunto de noche, botones y panel de Opciones. Bruno (Arte técnico), 7-nov 01:08.

Encargo de Mak (01:05). MUESTRA de 5 variantes (A–E); cada una en dos pantallas: el menú (con «Continuar» encima y
«Jugar» pulsado, para ver los tres estados) y el panel de Opciones abierto. Base de 640x360 (x3 a 1080p), como el HUD.
Botones: Jugar, Continuar, Cómo se juega, Opciones y Salir, en normal, encima y pulsado (y el inactivo de siempre para
«Continuar» sin partida guardada). Opciones: volumen (general, música y efectos), resolución y pantalla completa.
El texto lo pone UI Toolkit; el logo es el estarcido de siempre (ui_logo_via_muerta).
Datos de Sagunto: el castillo en el cerro al norte, la estación con su fuerte, la vía de este a oeste (sagunto-nivel.md).
Colores: noche en navy, slate y purple_grey; la horda en 1c200c solo en las siluetas de la horda; el ámbar, solo en luces
pequeñas; el rojo, en nada (no es un aviso).
"""
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import BAYER, _h  # noqa: E402

W, H = 640, 360
BOTONES = ["Jugar", "Continuar", "Cómo se juega", "Opciones", "Salir"]


def _pon(px, x, y, c, w=W, h=H):
    if 0 <= x < w and 0 <= y < h:
        px[int(x), int(y)] = PAL[c]


def _txt(im, xy, s, c="bone", t=9, centro=False, sombra=True):
    import build_maquetas as m
    m.texto(im, xy, s, t, c, centro=centro, sombra=sombra)


def _ancho(im, s, t=9):
    import build_maquetas as m
    from PIL import ImageDraw
    return int(ImageDraw.Draw(im).textlength(s, font=m._f(t)))


# --- Fondo: Sagunto de noche ------------------------------------------------------------------
def cielo(px, hz, luna=(480, 70, 16), estrellas=True, w=W):
    tonos = ["navy", "navy", "navy", "slate", "purple_grey"]   # sin slate_dark: es el color del cerro
    for y in range(hz):
        t = (y / hz) ** 2.2 * (len(tonos) - 1)
        i, fr = int(t), t - int(t)
        for x in range(w):
            px[x, y] = PAL[tonos[min(i + (1 if fr * 16 > BAYER[y % 4][x % 4] else 0), len(tonos) - 1)]]
    if estrellas:
        for k in range(110):
            x, y = int(_h(k, 0, 1101) * w), int(_h(k, 1, 1101) * hz * .7)
            px[x, y] = PAL["bone" if k % 5 else "grey_light"]
    if luna:
        lx, ly, r = luna
        for y in range(ly - r - 6, ly + r + 7):
            for x in range(lx - r - 6, lx + r + 7):
                d = math.hypot(x - lx, y - ly)
                if d <= r:
                    c = "bone"
                    for (mx, my, mr) in ((.3, .2, .22), (-.35, -.25, .14), (-.1, .45, .1), (.45, -.35, .08)):
                        if math.hypot(x - lx - mx * r, y - ly - my * r) < mr * r and (x + y) % 2 == 0:
                            c = "grey_light"
                    _pon(px, x, y, c, w)
                elif d <= r + 5 and (x + y) % 2 == 0 and d - r < 5 * (1 - (BAYER[y % 4][x % 4] / 16)):
                    _pon(px, x, y, "purple_grey", w)


def sierra(px, hz, base=0, w=W, castillo_x=300, escala=1.0):
    for x in range(w):
        h1 = hz - 22 + 8 * math.sin(x / 53) + 5 * math.sin(x / 17 + 1) + 2 * _h(x // 5, 0, 1102)
        for y in range(int(h1), hz + 1):
            px[x, y] = PAL["slate"]
        cer = 46 * escala * math.exp(-((x - castillo_x) / (90 * escala)) ** 2)
        h2 = hz - 4 - cer + 1.5 * math.sin(x / 7)
        for y in range(int(h2), hz + 8):
            px[x, y] = PAL["slate_dark"]
        if abs(x - castillo_x) < 70 * escala:          # muralla almenada en la cresta
            top = int(h2) - 6 - (2 if (x // 4) % 2 == 0 else 0)
            for y in range(top, int(h2) + 1):
                px[x, y] = PAL["slate_dark"]
            if (x - castillo_x) % 28 == 0:
                for y in range(top - 6, top):
                    for dx in range(-2, 3):
                        _pon(px, x + dx, y, "slate_dark", w)
    for x in range(w):                                   # filo de luna en la cresta y la muralla
        for y in range(hz - 80, hz + 8):
            if px[x, y][:3] == PAL["slate_dark"][:3] and px[x, y - 1][:3] != PAL["slate_dark"][:3]:
                px[x, y] = PAL["purple_grey"]
                break


def llanura(px, y0, w=W, h=H):
    for y in range(y0, h):
        t = (y - y0) / max(1, h - y0)
        for x in range(w):
            c = "olive_shadow" if t * 16 > BAYER[y % 4][x % 4] * .5 + 1 else "grey_brown"
            px[x, y] = PAL[c]


def fuerte(px, x0, y0, w_=150):
    """Fuerte de la estación: muro de sacos con focos y el edificio de viajeros asomando, a contraluz."""
    for x in range(x0, x0 + w_):                         # edificio de viajeros detrás del muro
        if x0 + 30 <= x < x0 + 104:
            for y in range(y0 - 22, y0):
                px[x, y] = PAL["grey_brown"]
            if (x - x0) % 12 in (4, 5, 6) and x < x0 + 100:
                for y in range(y0 - 17, y0 - 11):
                    px[x, y] = PAL["ochre_dark" if (x // 12) % 3 else "cream"]
        for y in range(y0, y0 + 12):                     # muro
            px[x, y] = PAL["olive_grey" if y == y0 else "grey_brown"]
        if (x - x0) % 6 == 0:
            px[x, y0 + 4] = PAL["olive_shadow"]
    for fx in (x0 + 8, x0 + w_ - 10):                    # torres de foco
        for y in range(y0 - 28, y0):
            px[fx, y] = PAL["grey_brown"]
        for dx in range(-2, 3):
            px[fx + dx, y0 - 29] = PAL["cream"]


def foco(px, x0, y0, ang, largo=150, w=W, h=H):
    for d in range(4, largo):
        semi = d * .09
        cx, cy = x0 + math.cos(ang) * d, y0 + math.sin(ang) * d
        for k in range(-int(semi), int(semi) + 1):
            x, y = int(cx - math.sin(ang) * k), int(cy + math.cos(ang) * k)
            if 0 <= x < w and 0 <= y < h and (x + y) % 2 == 0 and d < largo * (1 - abs(k) / (semi + 1) * .3):
                px[x, y] = PAL["grey_light" if d < largo * .5 else "concrete_light"]


def tren(px, x0, y, faro=True, w=W, n=2):
    """Tren de la noche (D-N110): locomotora a la derecha y dos vagones, a contraluz, ventanillas encendidas."""
    x = x0
    for k, largo in enumerate([46] * n + [56]):
        top = y - 22 if k < n else y - 26
        for yy in range(top, y):
            for xx in range(x, x + largo):
                _pon(px, xx, yy, "olive_shadow", w)
        for xx in range(x, x + largo):
            _pon(px, xx, top, "grey_brown", w)
        if k < n:
            for wx in range(x + 5, x + largo - 6, 9):
                for yy in range(top + 5, top + 9):
                    for xx in range(wx, wx + 5):
                        _pon(px, xx, yy, "ochre_dark" if _h(wx, k, 1103) > .5 else "olive_grey", w)
        else:
            for yy in range(top - 6, top):
                for xx in range(x + 30, x + 50):
                    _pon(px, xx, yy, "olive_shadow", w)
            for yy in range(top + 4, top + 8):
                for xx in range(x + 40, x + 50):
                    _pon(px, xx, yy, "cream", w)
        x += largo + 3
    if faro:
        fx, fy = x - 2, y - 10
        for xx in range(fx, min(w, fx + 160)):
            semi = (xx - fx) * .1
            for yy in range(int(fy - semi), int(fy + semi) + 1):
                if (xx + yy) % 2 == 0 or xx - fx < 8:
                    _pon(px, xx, yy, "cream" if xx - fx < 30 else "beige", w)
    for xx in range(0, w):                               # carril
        _pon(px, xx, y, "concrete", w)


def horda(px, x0, x1, y0, y1, n=60, semilla=1104, w=W, h=H):
    for k in range(n):
        hx = int(x0 + _h(k, 1, semilla) * (x1 - x0))
        hy = int(y0 + _h(k, 2, semilla) * (y1 - y0))
        alto = 7 + int((hy - y0) / max(1, y1 - y0) * 15)
        ancho = max(2, alto // 3)
        for yy in range(hy - alto, hy):
            for xx in range(hx, hx + ancho):
                _pon(px, xx, yy, "olive_black", w, h)
        if alto >= 12:                                   # brazos al frente (D-N61), hacia el fuerte
            for xx in range(hx + ancho, hx + ancho + alto // 4):
                _pon(px, xx, hy - alto + alto // 4, "olive_black", w, h)


def fondo(var):
    im = Image.new("RGBA", (W, H))
    px = im.load()
    if var == "A":                                       # panorama: castillo, fuerte, tren y horda
        hz = 200
        cielo(px, hz, (500, 64, 18))
        sierra(px, hz, castillo_x=430)
        llanura(px, hz + 6)
        fuerte(px, 360, hz + 2, 170)
        foco(px, 368, hz - 27, math.radians(160), 170)
        tren(px, 300, hz + 34)
        horda(px, 0, 260, hz + 44, H, 80)
    elif var == "B":                                     # desde dentro del fuerte: sacos en primer plano
        hz = 228
        cielo(px, hz, (150, 70, 14))
        sierra(px, hz, castillo_x=180, escala=1.2)
        llanura(px, hz + 6)
        horda(px, 330, 640, hz + 8, hz + 40, 70, 1105)
        foco(px, 520, H - 60, math.radians(200), 260)
        for x in range(W):                               # muro de sacos en primer plano, con almenas
            top = H - 60 - (6 if (x // 40) % 3 == 1 else 0)
            for y in range(top, H):
                fila = (y - top) // 7
                c = "olive_grey" if (y - top) % 7 == 0 or (x + fila * 9) % 18 == 0 else "grey_brown"
                px[x, y] = PAL[c]
        for x in range(500, 540):
            for y in range(H - 72, H - 60):
                px[x, y] = PAL["grey_brown"]
    elif var == "C":                                     # el mapa de frentes (D-N113) velado
        import build_mapa_frentes as mf
        v = mf.Vista(esc=1.6, ox=420, oy=180)
        base = mf.fondo(v, "tactico")
        mf.carreteras(base, v, "tactico"); mf.via(base, v, "tactico"); mf.recinto(base, v)
        bp = base.load()
        for y in range(H):
            for x in range(W):
                if (.55 - x / W * .5) * 16 > BAYER[y % 4][x % 4]:
                    bp[x, y] = PAL["navy"]
        im = base
    elif var == "D":                                     # cartel: luna grande y el tren cruzando delante
        hz = 250
        cielo(px, hz, (420, 150, 70), estrellas=True)
        sierra(px, hz, castillo_x=150, escala=.9)
        llanura(px, hz + 6)
        tren(px, 210, hz + 20, faro=True)
        horda(px, 0, 640, hz + 30, H, 90, 1106)
    else:                                                # E: mínimo: la vía hacia el punto de fuga y la estación
        hz = 190
        cielo(px, hz, None)
        for y in range(hz, H):
            for x in range(W):
                px[x, y] = PAL["navy" if (y - hz) * 2 < BAYER[y % 4][x % 4] + 2 else "olive_shadow"]
        fx = 420
        for y in range(hz, H):                           # carriles hacia el punto de fuga
            t = (y - hz) / (H - hz)
            for s in (-1, 1):
                _pon(px, fx + s * (2 + t * 150), y, "concrete_light")
            if int(t * 40) != int((y - 1 - hz) / (H - hz) * 40):
                for x in range(int(fx - 3 - t * 165), int(fx + 3 + t * 165)):
                    _pon(px, x, y, "grey_brown")
        for dx in range(-14, 15):                        # la estación al fondo, con una luz
            for dy in range(-8, 0):
                _pon(px, fx + dx, hz + dy, "slate_dark")
        _pon(px, fx - 6, hz - 4, "cream"); _pon(px, fx - 5, hz - 4, "cream")
        _pon(px, fx + 4, hz - 4, "ochre_dark")
    return im


# --- Botones ----------------------------------------------------------------------------------
def boton(var, estado, w, h):
    """Placa del botón en cada estilo (9-slice). estado: normal, encima, pulsado, inactivo."""
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = im.load()
    if var in ("A", "D"):                                # placa navy con borde; encima: borde hueso y cuña
        fondo_, borde, luz = {"normal": ("navy", "grey_brown", "slate_dark"), "encima": ("slate_dark", "bone", "slate"),
                              "pulsado": ("navy", "bone", "navy"), "inactivo": ("navy", "olive_shadow", "navy")}[estado]
        for y in range(h):
            for x in range(w):
                c = fondo_
                if x in (0, w - 1) or y in (0, h - 1):
                    c = borde
                elif y == 1 and estado != "pulsado":
                    c = luz
                elif y == h - 2 and estado == "pulsado":
                    c = "slate_dark"
                if (x in (0, w - 1)) and (y in (0, h - 1)):
                    continue
                px[x, y] = PAL[c]
        if estado == "encima":
            for k in range(4):
                for j in range(k, 7 - k):
                    px[3 + k, h // 2 - 3 + j] = PAL["bone"]
    elif var == "B":                                     # chapa de estarcido: placa oliva con remaches
        im.paste(_boton_viejo(estado, w, h), (0, 0))
        px = im.load()
        for (x, y) in ((3, 3), (w - 4, 3), (3, h - 4), (w - 4, h - 4)):
            px[x, y] = PAL["concrete_light" if estado == "encima" else "olive_light"]
    elif var == "C":                                     # fila de lista: solo fondo al estar encima y marca
        if estado in ("encima", "pulsado"):
            for y in range(h):
                for x in range(w):
                    if (estado == "pulsado") or (x + y) % 2 == 0 or x < 3:
                        px[x, y] = PAL["slate_dark" if estado == "encima" else "navy"]
            for y in range(h):
                px[0, y] = PAL["bone"]; px[1, y] = PAL["bone"]
    else:                                                # E: texto con subrayado; encima, subrayado largo
        largo = {"normal": 0, "encima": w, "pulsado": w, "inactivo": 0}[estado]
        for x in range(largo):
            px[x, h - 1] = PAL["bone" if estado == "encima" else "grey_light"]
            if estado == "pulsado":
                px[x, h - 2] = PAL["grey_light"]
    return im


def _boton_viejo(estado, w, h):
    import build_interfaz as ui
    return ui.boton(estado, w, h).img


def color_texto(var, estado):
    if estado == "inactivo":
        return "olive_grey"
    if var == "E":
        return {"normal": "beige", "encima": "cream", "pulsado": "bone"}[estado]
    return {"normal": "bone", "encima": "cream", "pulsado": "beige"}[estado]


def logo(im, x, y, e=3, centro=False):
    import build_interfaz as ui
    lg = ui.logo(e=e)
    if centro:
        x = x - lg.w // 2
    im.alpha_composite(lg.img, (int(x), int(y)))
    return lg.w, lg.h


# --- Panel de Opciones ------------------------------------------------------------------------
def deslizador(im, x, y, w, v, encima=False, var="A"):
    px = im.load()
    for xx in range(w):
        px[x + xx, y] = PAL["grey_brown"]
        px[x + xx, y + 1] = PAL["olive_shadow"]
    for xx in range(int(w * v)):
        px[x + xx, y] = PAL["beige" if var != "E" else "bone"]
    kx = x + int(w * v)
    for yy in range(-3, 5):
        for xx in range(-2, 3):
            c = "bone" if encima else "grey_light"
            if abs(xx) == 2 or yy in (-3, 4):
                c = "grey_brown" if not encima else "cream"
            px[kx + xx, y + yy] = PAL[c]


def casilla(im, x, y, on=True):
    px = im.load()
    for yy in range(9):
        for xx in range(9):
            c = "navy"
            if xx in (0, 8) or yy in (0, 8):
                c = "bone"
            px[x + xx, y + yy] = PAL[c]
    if on:
        for k in range(5):
            px[x + 2 + k, y + 2 + k] = PAL["cream"]; px[x + 6 - k, y + 2 + k] = PAL["cream"]


def flecha(im, x, y, d, c="bone"):
    px = im.load()
    for k in range(4):
        for j in range(-k, k + 1):
            px[x + (3 - k if d > 0 else k), y + j] = PAL[c]


def panel_opciones(im, var, x, y, w=300, h=176):
    import build_mapa_frentes as mf
    marco = {"A": ("navy", "bone"), "B": ("olive_dark", "olive_light"), "C": ("navy", "grey_brown"),
             "D": ("navy", "bone"), "E": ("navy", "grey_brown")}[var]
    if var == "B":
        im.alpha_composite(_boton_viejo("normal", w, h), (x, y))
    else:
        mf.caja(im, x, y, w, h, marco[0], marco[1], "grey_brown")
    _txt(im, (x + 12, y + 8), "Opciones", "cream", 11)
    px = im.load()
    for xx in range(x + 10, x + w - 10):
        px[xx, y + 24] = PAL["grey_brown"]
    filas = [("Volumen general", .8), ("Música", .55), ("Efectos", .9)]
    for i, (n, v) in enumerate(filas):
        yy = y + 34 + i * 20
        _txt(im, (x + 14, yy), n, "bone", 9, sombra=False)
        deslizador(im, x + 130, yy + 6, 120, v, encima=(i == 1), var=var)
        _txt(im, (x + 258, yy), f"{int(v * 100)}", "beige", 9, sombra=False)
    yy = y + 98
    _txt(im, (x + 14, yy), "Resolución", "bone", 9, sombra=False)
    flecha(im, x + 130, yy + 6, -1); flecha(im, x + 246, yy + 6, 1)
    _txt(im, (x + 190, yy), "1920 × 1080", "cream", 9, centro=True, sombra=False)
    yy = y + 118
    _txt(im, (x + 14, yy), "Pantalla completa", "bone", 9, sombra=False)
    casilla(im, x + 130, yy + 2, True)
    for i, (t, e) in enumerate((("Aplicar", "encima"), ("Volver", "normal"))):
        bw, bh = 90, 18
        bx, by = x + w - 2 * (bw + 8) + i * (bw + 8), y + h - bh - 10
        im.alpha_composite(boton(var if var != "C" else "A", e, bw, bh), (bx, by))
        _txt(im, (bx + bw // 2, by + 3), t, color_texto(var, e), 9, centro=True, sombra=False)


# --- Pantallas --------------------------------------------------------------------------------
def columna(im, var, x, y, w, h, paso, estados):
    for i, (t, e) in enumerate(zip(BOTONES, estados)):
        by = y + i * paso
        im.alpha_composite(boton(var, e, w, h), (x, by))
        tx = x + (14 if var in ("A", "D") else 10)
        if var in ("B",):
            _txt(im, (x + w // 2, by + (h - 11) // 2), t, color_texto(var, e), 9, centro=True)
        else:
            _txt(im, (tx, by + (h - 11) // 2), t, color_texto(var, e), 9)


ESTADOS = ["pulsado", "encima", "normal", "normal", "normal"]


def pantalla(var, opciones=False):
    im = fondo(var)
    if var == "A":
        logo(im, 24, 24, 3)
        columna(im, "A", 24, 120, 132, 20, 26, ESTADOS)
    elif var == "B":
        logo(im, W // 2, 22, 4, centro=True)
        columna(im, "B", W // 2 - 70, 96, 140, 20, 24, ESTADOS)
    elif var == "C":
        logo(im, 24, 28, 3)
        px = im.load()
        for i in range(5):
            for xx in range(24, 170):
                px[xx, 140 + i * 22 + 20] = PAL["grey_brown"]
        columna(im, "C", 24, 140, 146, 20, 22, ESTADOS)
    elif var == "D":
        logo(im, W // 2, 20, 4, centro=True)
        bw = 108
        x0 = (W - (5 * bw + 4 * 6)) // 2
        for i, (t, e) in enumerate(zip(BOTONES, ESTADOS)):
            bx = x0 + i * (bw + 6)
            im.alpha_composite(boton("D", e, bw, 20), (bx, H - 34))
            _txt(im, (bx + bw // 2 + 4, H - 30), t, color_texto("D", e), 9, centro=True)
    else:
        logo(im, 40, 60, 3)
        for i, (t, e) in enumerate(zip(BOTONES, ESTADOS)):
            by = 150 + i * 22
            w_ = _ancho(im, t, 11) + 2
            im.alpha_composite(boton("E", e, w_, 16), (40, by))
            _txt(im, (40, by + 1), t, color_texto("E", e), 11, sombra=False)
    _txt(im, (W - 8 - _ancho(im, "v0.1 · prototipo", 8), H - 12), "v0.1 · prototipo", "grey_brown", 8, sombra=False) \
        if var != "D" else None
    if opciones:
        px = im.load()
        for y in range(H):                               # velo tramado en navy detrás del panel
            for x in range(W):
                if BAYER[y % 4][x % 4] < 8:
                    px[x, y] = PAL["navy"]
        panel_opciones(im, var, W // 2 - 150, H // 2 - 88)
    return im


def tira_estados(var):
    im = Image.new("RGBA", (4 * 112 + 12, 28), PAL["navy"])
    for i, e in enumerate(("normal", "encima", "pulsado", "inactivo")):
        x = 4 + i * 112
        im.alpha_composite(boton(var, e, 104, 20), (x, 4))
        _txt(im, (x + 52, 7), e.capitalize(), color_texto(var, e), 9, centro=True, sombra=False)
    return im


VARIANTES = "ABCDE"
NOTAS = {"A": "Panorama: castillo en el cerro, fuerte con focos, tren y horda; logo y columna de placas a la izquierda",
         "B": "Desde dentro del fuerte: sacos en primer plano, foco sobre la horda; chapas de estarcido centradas",
         "C": "El mapa de frentes de fondo (D-N113), velado; lista con marca a la izquierda",
         "D": "Cartel: luna grande y el tren cruzando; botones en fila abajo",
         "E": "Mínimo: la vía hacia la estación; botones de solo texto con subrayado"}


def muestra(out):
    import build_maquetas as m
    alto = 360 + 28 + 30
    hoja = Image.new("RGBA", (2 * 640 + 12, 5 * alto), PAL["navy"])
    for i, v in enumerate(VARIANTES):
        y = i * alto
        m.texto(hoja, (6, y + 4), f"{v} · {NOTAS[v]}", 9, "cream")
        a = pantalla(v)
        hoja.alpha_composite(a, (0, y + 22))
        hoja.alpha_composite(pantalla(v, True), (652, y + 22))
        hoja.alpha_composite(tira_estados(v), (0, y + 22 + 362))
        a.resize((1920, 1080), Image.NEAREST).save(out / f"muestra_menu_{v}_1080.png")
    hoja.save(out / "muestra_menu_5_variantes.png")


# --- Piezas finales (variante A = D-N115, Mak y Lucía 07-11 01:09) --------------------------------
def exportar(out):
    import json
    out = Path(out)
    fondo("A").save(out / "ui_menu_fondo_noche.png")
    for e in ("normal", "encima", "pulsado", "inactivo"):
        boton("A", e, 32, 20).save(out / f"ui_menu_boton_{e}.png")
    import build_mapa_frentes as mf
    pan = Image.new("RGBA", (12, 12), (0, 0, 0, 0)); mf.caja(pan, 0, 0, 12, 12, "navy", "bone", "grey_brown")
    pan.save(out / "ui_menu_panel.png")
    sep = Image.new("RGBA", (1, 1), PAL["grey_brown"]); sep.save(out / "ui_menu_separador.png")
    pista = Image.new("RGBA", (2, 2), (0, 0, 0, 0)); pp = pista.load()
    pp[0, 0] = pp[1, 0] = PAL["grey_brown"]; pp[0, 1] = pp[1, 1] = PAL["olive_shadow"]
    pista.save(out / "ui_deslizador_pista.png")
    Image.new("RGBA", (1, 1), PAL["beige"]).save(out / "ui_deslizador_relleno.png")
    for enc in (False, True):
        im = Image.new("RGBA", (5, 8), (0, 0, 0, 0))
        tmp = Image.new("RGBA", (20, 20), (0, 0, 0, 0))
        deslizador(tmp, 2, 5, 0, 0, encima=enc)
        im = tmp.crop((0, 2, 5, 10))
        im.save(out / f"ui_deslizador_mando_{'encima' if enc else 'normal'}.png")
    for on in (False, True):
        im = Image.new("RGBA", (9, 9), (0, 0, 0, 0)); casilla(im, 0, 0, on)
        im.save(out / f"ui_casilla_{'si' if on else 'no'}.png")
    for d, n in ((-1, "izq"), (1, "dcha")):
        im = Image.new("RGBA", (4, 7), (0, 0, 0, 0)); flecha(im, 0, 3, d)
        im.save(out / f"ui_flecha_{n}.png")
    velo = Image.new("RGBA", (4, 4), (0, 0, 0, 0)); vp = velo.load()
    for y in range(4):
        for x in range(4):
            if BAYER[y][x] < 8:
                vp[x, y] = PAL["navy"]
    velo.save(out / "ui_velo_navy_4x4.png")
    (out / "ui_menu.json").write_text(json.dumps({
        "variante": "A = D-N115; sustituye a ui_menu_fondo (el atardecer)",
        "base": [640, 360], "escala_1080": 3,
        "fondo": {"png": "ui_menu_fondo_noche", "tamano": [640, 360]},
        "logo": {"png": "ui_logo_via_muerta", "x": 24, "y": 24},
        "botones": {"png": "ui_menu_boton_{normal|encima|pulsado|inactivo}", "slice": {"left": 10, "top": 3, "right": 3, "bottom": 3},
                    "tamano": [132, 20], "x": 24, "y0": 120, "paso": 26,
                    "orden": BOTONES, "texto": {"x": 14, "color": {"normal": "#d4d0cc", "encima": "#f0dc9c", "pulsado": "#b4a080", "inactivo": "#605c48"}},
                    "continuar": "inactivo si no hay partida guardada"},
        "opciones": {"velo": "ui_velo_navy_4x4 en mosaico sobre toda la pantalla", "panel": "ui_menu_panel (slice 4), 300x176 centrado",
                     "titulo": "«Opciones» a (12, 8), 11 px, cream; separador ui_menu_separador en y 24",
                     "filas": {"volumen": ["Volumen general", "Música", "Efectos"], "y0": 34, "paso": 20,
                               "deslizador": "ui_deslizador_pista (estirada a 120 px) + ui_deslizador_relleno (2 px de alto en la fila 0, ancho × valor) + ui_deslizador_mando_{normal|encima} centrado en el valor; x 130",
                               "resolucion": "y 98: ui_flecha_izq en x 130, ui_flecha_dcha en x 246, el valor centrado en x 190",
                               "pantalla_completa": "y 118: ui_casilla_{si|no} en x 130"},
                     "botones": "Aplicar y Volver con ui_menu_boton_*, 90x18, abajo a la derecha"},
        "colores": "sin rojo ni ámbar; horda en 1c200c solo en sus siluetas",
    }, ensure_ascii=False, indent=1), encoding="utf-8")


def maqueta(out):
    out = Path(out)
    pantalla("A").resize((1920, 1080), Image.NEAREST).save(out / "maqueta_1080_menu.png")
    pantalla("A", True).resize((1920, 1080), Image.NEAREST).save(out / "maqueta_1080_menu_opciones.png")


if __name__ == "__main__":
    OUT = Path(next((a for a in sys.argv[1:] if not a.startswith("--")), "out_menu"))
    OUT.mkdir(parents=True, exist_ok=True)
    if "--muestra" in sys.argv:
        muestra(OUT)
    else:
        exportar(OUT)
        maqueta(OUT)
    print("ok")
