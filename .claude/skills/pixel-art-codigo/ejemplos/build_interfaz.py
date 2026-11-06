"""Interfaz del prototipo jugable (noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:04): menú de inicio (fondo y botones) y HUD (selección de pelotón, órdenes, recursos y
alertas arriba a la izquierda). Los iconos ya existen (A1 recursos, A2 alertas, A3 órdenes, A6 y A9 unidades);
aquí van los marcos, botones, barras, marcadores y el fondo del menú. Todo en piezas 9-slice para UI Toolkit:
el JSON de cada pieza trae el borde (`slice`) en píxeles. Escala entera (x3 a 1080p, x4 a 1440p), filtro de punto.

Lenguaje (§4 de analisis-referencias-6nov.md): marcos de 1 px, fondo navy #181c30 (el fondo de UI de A1–A6),
remaches en hueso, oliva para lo propio, ámbar solo para avisos y rojo #982420 solo para el ataque.
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import _h  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_interfaz")
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


# --- Piezas 9-slice -------------------------------------------------------------------------
def panel(w=24, h=24):
    """Panel del HUD: fondo navy, marco de 1 px en grey_brown con luz arriba a la izquierda en olive_grey,
    y un remache en hueso en cada esquina. Slice 4."""
    s = Sprite(w, h)
    s.rect(0, 0, w, h, "navy")
    s.rect(0, 0, w, 1, "grey_brown")
    s.rect(0, h - 1, w, 1, "grey_brown")
    s.rect(0, 0, 1, h, "grey_brown")
    s.rect(w - 1, 0, 1, h, "grey_brown")
    s.hline(1, w - 2, 1, "olive_grey")                 # luz interior arriba y a la izquierda
    s.vline(1, 1, h - 2, "olive_grey")
    s.hline(1, w - 2, h - 2, "slate_dark")             # sombra interior abajo y a la derecha
    s.vline(w - 2, 2, h - 2, "slate_dark")
    for x, y in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3)):
        s.set(x, y, "bone")
    return s


def boton(estado, w=48, h=16):
    """Botón del menú y del HUD en 4 estados. Slice 4. El texto lo pone UI Toolkit (hueso; gris si inactivo)."""
    s = Sprite(w, h)
    fondo, luz, sombra, marco = {
        "normal": ("olive_dark", "olive", "olive_shadow", "grey_brown"),
        "encima": ("olive", "olive_light", "olive_dark", "bone"),
        "pulsado": ("olive_shadow", "olive_dark", "olive_dark", "bone"),
        "inactivo": ("grey_brown", "olive_grey", "olive_shadow", "olive_shadow"),
    }[estado]
    s.rect(0, 0, w, h, fondo)
    s.rect(0, 0, w, 1, marco)
    s.rect(0, h - 1, w, 1, marco)
    s.rect(0, 0, 1, h, marco)
    s.rect(w - 1, 0, 1, h, marco)
    if estado == "pulsado":                            # hundido: la luz pasa abajo
        s.hline(1, w - 2, h - 2, luz)
        s.hline(1, w - 2, 1, sombra)
    else:
        s.hline(1, w - 2, 1, luz)
        s.vline(1, 1, h - 2, luz)
        s.hline(1, w - 2, h - 2, sombra)
        s.vline(w - 2, 2, h - 2, sombra)
    for x, y in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        s.set(x, y, None)                              # esquinas redondeadas de 1 px
    return s


def tarjeta_unidad(sel=False):
    """Tarjeta de pelotón de la barra de selección (40x48): hueco del icono de 32, barra de efectivos y
    barra de munición. Seleccionada: marco en hueso. Slice 3 (no se estira: se usa a su tamaño)."""
    s = panel(40, 48)
    if sel:
        for y in (0, 47):
            s.hline(0, 39, y, "bone")
        for x in (0, 39):
            s.vline(x, 0, 47, "bone")
    s.rect(4, 4, 32, 32, "slate_dark")                 # hueco del icono (A6/A9 encima)
    s.rect(4, 38, 32, 3, "olive_shadow")               # efectivos (el relleno es barra_relleno)
    s.rect(4, 42, 32, 2, "olive_shadow")               # munición
    return s


def barra(tipo, w=32, h=3):
    """Relleno de barra: efectivos (oliva claro), munición (latón), combustible (ocre), baja (ámbar de aviso)."""
    c, luz = {"efectivos": ("olive_light", "beige"), "municion": ("ochre_dark", "amber"),
              "combustible": ("olive_yellow", "beige"), "baja": ("amber", "cream")}[tipo]
    s = Sprite(w, h)
    s.rect(0, 0, w, h, c)
    s.hline(0, w - 1, 0, luz)
    return s


def recurso_slot():
    """Hueco de recurso de la barra de arriba a la izquierda: icono de 16 (A1) + número. 56x20, slice 4."""
    s = panel(56, 20)
    s.rect(2, 2, 16, 16, "slate_dark")
    return s


def alerta_slot(nivel):
    """Marco de alerta de 24x24 bajo los recursos (icono A2 de 16 dentro). aviso: ámbar; ataque: rojo."""
    s = panel(24, 24)
    c = {"aviso": "amber", "ataque": "red_bright", "info": "bone"}[nivel]
    for y in (0, 23):
        s.hline(0, 23, y, c)
    for x in (0, 23):
        s.vline(x, 0, 23, c)
    return s


# --- Marcadores en el mundo ----------------------------------------------------------------
def seleccion(c="bone", t=16):
    """Corchetes de esquina alrededor del pelotón seleccionado (se estiran por 9-slice, slice 5)."""
    s = Sprite(t, t)
    for (x, y, dx, dy) in ((0, 0, 1, 1), (t - 1, 0, -1, 1), (0, t - 1, 1, -1), (t - 1, t - 1, -1, -1)):
        for k in range(4):
            s.set(x + dx * k, y, c)
            s.set(x, y + dy * k, c)
    return s


def orden_mover(f):
    """Marcador de destino al dar la orden de mover: cuatro esquinas que se cierran sobre el punto (4 f, una vez)."""
    s = Sprite(16, 16)
    d = 7 - f * 1.5
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = int(round(7.5 + sx * d + (.5 if sx > 0 else -.5))), int(round(7.5 + sy * d + (.5 if sy > 0 else -.5)))
            for k in range(3):
                s.set(x - sx * k, y, "bone")
                s.set(x, y - sy * k, "bone")
    s.rect(7, 7, 2, 2, "bone")
    return s


def orden_atacar(f):
    """Igual que mover, en el rojo de ataque y con aspa en el centro."""
    s = orden_mover(f)
    s.replace("bone", "red_bright")
    for k in range(-2, 3):
        s.set(8 + k, 8 + k, "red_bright")
        s.set(8 + k, 8 - k, "red_bright")
    return s


def cursor(tipo):
    """Cursores de 16x16 con contorno negro: flecha (normal), mover (cruz de flechas), atacar (mira roja),
    construir (llave en hueso). Punto caliente en el JSON."""
    s = Sprite(16, 16)
    if tipo == "normal":
        for y in range(11):
            s.hline(1, 1 + y * 7 // 10, 1 + y, "bone")
        s.rect(4, 11, 2, 4, "bone")
        s.line(2, 2, 2, 10, "grey_light")
    elif tipo == "mover":
        s.grid(1, 1, """
            ......#......
            .....###.....
            ....#####....
            ......#......
            ..#...#...#..
            .##...#...##.
            #############
            .##...#...##.
            ..#...#...#..
            ......#......
            ....#####....
            .....###.....
            ......#......
        """, {"#": "bone"})
    elif tipo == "atacar":
        for y in range(16):
            for x in range(16):
                if 5.2 <= math.hypot(x - 7.5, y - 7.5) <= 6.4:
                    s.set(x, y, "red_bright")
        s.rect(7, 0, 2, 5, "red_bright")
        s.rect(7, 11, 2, 5, "red_bright")
        s.rect(0, 7, 5, 2, "red_bright")
        s.rect(11, 7, 5, 2, "red_bright")
    else:                                              # construir: martillo
        s.line(3, 13, 10, 6, "beige")
        s.line(4, 13, 11, 6, "beige")
        s.poly([(8, 2), (12, 0), (15, 4), (13, 6), (11, 5), (10, 7), (7, 5)], "bone")
        s.line(8, 2, 12, 0, "grey_light")
    s.outline()
    return s


CALIENTE = {"normal": [1, 1], "mover": [8, 8], "atacar": [8, 8], "construir": [3, 12]}


# --- Iconos de orden que faltaban (A3: hueso sin marco; el marco lo pone el botón) -----------------
def orden_mover_icono():
    """Mover: flecha de A3 hacia la derecha que llega a un destino marcado en el suelo (anillo de cuñas)."""
    import build_a3 as a3
    s = Sprite()
    s.ellipse(17, 20, 30, 28, "beige")                 # destino en el suelo, visto en 3/4
    s.ellipse(19, 22, 28, 26, None)
    for x in (22, 23, 24, 25):
        s.set(x, 20, None)
        s.set(x, 28, None)
    a3.flecha_der(s, 2, 8, 26, 5)
    s.outline()
    return s


def orden_atacar_icono():
    """Atacar: mira en hueso con el centro en el rojo de ataque (el único rojo de la interfaz)."""
    s = Sprite()
    for y in range(32):
        for x in range(32):
            d = math.hypot(x - 15.5, y - 15.5)
            if 8.5 <= d <= 11.5:
                s.set(x, y, "cream" if (x < 16 and y < 16 and d < 10) else "bone" if d < 11 else "beige")
    for a, b in ((2, 9), (22, 29)):
        s.rect(14, a, 4, b - a + 1, "bone")
        s.rect(a, 14, b - a + 1, 4, "bone")
    s.vline(14, 2, 9, "cream")
    s.hline(2, 9, 14, "cream")
    s.rect(14, 14, 4, 4, "red_bright")
    s.set(14, 14, "red")
    s.outline()
    return s


def orden_emplazar():
    """Emplazar (solo ametralladores): ametralladora sobre el bípode y una flecha que la clava al suelo."""
    s = Sprite()
    s.hline(1, 30, 27, "grey_brown")                   # suelo
    s.hline(1, 30, 28, "olive_shadow")
    s.rect(6, 15, 22, 3, "bone")                       # arma en hueso, de perfil
    s.hline(6, 27, 15, "cream")
    s.rect(4, 15, 4, 5, "bone")                        # culata
    s.rect(10, 13, 7, 2, "beige")                      # cajón y asa
    s.line(22, 18, 19, 26, "bone")                     # bípode abierto
    s.line(23, 18, 26, 26, "bone")
    s.rect(14, 2, 4, 6, "bone")                        # flecha hacia abajo
    s.poly([(10, 8), (21, 8), (15, 12)], "bone")
    s.vline(14, 2, 7, "cream")
    s.outline()
    return s


def _vehiculo(s, x0):
    """Caja de vagón o blindado de 15x14 en oliva con la puerta oscura en el centro y dos ruedas."""
    s.rect(x0, 6, 15, 14, "olive")
    s.hline(x0, x0 + 14, 6, "olive_light")
    s.vline(x0, 7, 19, "olive_light")
    s.hline(x0, x0 + 14, 19, "olive_dark")
    s.vline(x0 + 14, 7, 19, "olive_dark")
    s.rect(x0 + 4, 9, 7, 10, "olive_shadow")             # puerta abierta
    for x in (x0 + 3, x0 + 11):
        s.ellipse(x - 2, 19, x + 2, 23, "slate_dark")
        s.set(x, 21, "concrete")


def orden_embarcar():
    """Embarcar: flecha gruesa que ENTRA por la puerta del vehículo (vehículo a la derecha)."""
    import build_a3 as a3
    s = Sprite()
    _vehiculo(s, 16)
    a3.flecha_der(s, 1, 12, 23, 5)
    s.outline()
    return s


def orden_desembarcar():
    """Desembarcar: el vehículo a la izquierda y la flecha gruesa que SALE de su puerta hacia fuera."""
    import build_a3 as a3
    s = Sprite()
    _vehiculo(s, 1)
    a3.flecha_der(s, 8, 12, 24, 5)
    s.outline()
    return s


# --- Señalización horizontal (Álvaro y Lucía, plan §4: «Escala») ---------------------------
def linea_central():
    """Línea central discontinua de carretera: 3 m de trazo y 3 m de hueco, 0,15 m de ancho → 64x2 en
    b8b8b8 (grey_light), con 2 téxeles comidos para que no parezca una línea de interfaz. Se repite en x."""
    s = Sprite(64, 2)
    for x in range(30):
        for y in (0, 1):
            if _h(x, y, 951) > .06:
                s.set(x, y, "grey_light")
    return s


def arcen():
    """Línea de arcén continua en 94948c (concrete_light): 64x1, con alguna falta de pintura."""
    s = Sprite(64, 1)
    for x in range(64):
        if _h(x, 0, 952) > .1:
            s.set(x, 0, "concrete_light")
    return s


def helipuerto():
    """H del helipuerto dentro de un círculo, en b8b8b8 (nunca ámbar). 128x128 = 12,8 m."""
    s = Sprite(128, 128)
    for y in range(128):
        for x in range(128):
            d = math.hypot(x - 63.5, y - 63.5)
            if 56 <= d <= 60 and _h(x, y, 953) > .05:
                s.set(x, y, "grey_light")
    for y in range(36, 92):
        for x in list(range(42, 52)) + list(range(76, 86)):
            if _h(x, y, 954) > .05:
                s.set(x, y, "grey_light")
    for y in range(59, 69):
        for x in range(52, 76):
            if _h(x, y, 955) > .05:
                s.set(x, y, "grey_light")
    return s


# --- Fondo del menú de inicio ---------------------------------------------------------------
LETRAS = {  # 5x7, trazo de 1 (se pinta a 2x2 con relieve)
    "V": ["10001", "10001", "10001", "10001", "01010", "01010", "00100"],
    "I": ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
    "A": ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    "M": ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
    "U": ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
    "E": ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
    "R": ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
    "T": ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
    " ": ["000"] * 7,
}


def logo(texto="VÍA MUERTA", e=3):
    """Rótulo de estarcido militar: letras de 5x7 a escala e, en hueso, con sombra de 1 px en grey_brown
    y los cortes del estarcido (una fila vacía a media altura en las verticales). Tilde en la Í."""
    t = texto.replace("Í", "I")
    ancho = sum(len(LETRAS[c][0]) + 1 for c in t) * e + 4
    s = Sprite(ancho, 7 * e + 4 + e * 2)
    x0 = 1
    for i, c in enumerate(t):
        for j, fila in enumerate(LETRAS[c]):
            for k, b in enumerate(fila):
                if b == "1":
                    for a in range(e):
                        for bb in range(e):
                            yy = 2 * e + j * e + bb
                            if j == 3 and bb == e // 2 and c in "VIMUERT":   # puente del estarcido
                                continue
                            s.set(x0 + k * e + a + 1, yy + 1, "grey_brown")
                            s.set(x0 + k * e + a, yy, "bone")
        if texto[i] == "Í":
            for a in range(e):
                for bb in range(e - 1):
                    s.set(x0 + 2 * e + a + (e - 1 - bb), bb, "bone")
        x0 += (len(LETRAS[c][0]) + 1) * e
    return s


def menu_fondo(W=480, H=270):
    """Fondo del menú (480x270, se escala x4 a 1920x1080 o x3 con bandas). Atardecer sobre el corredor:
    cielo en bandas tramadas, sierra y castillo de Sagunto al fondo, un tren con los faros encendidos sobre
    el terraplén y la horda que llega por el campo en primer plano. Hueco arriba a la izquierda para el
    logo y abajo a la izquierda para los botones (columna de 160 px)."""
    im = Image.new("RGBA", (W, H))
    px = im.load()
    cielo = ["navy", "slate_dark", "purple_grey", "rose_brown", "tan", "ochre_dark", "amber", "cream"]
    hz = 168                                           # horizonte
    for y in range(hz):
        t = (y / hz) ** 1.4 * (len(cielo) - 1)
        i, fr = int(t), t - int(t)
        for x in range(W):
            c = cielo[min(i + (1 if fr * 16 > BAYER[y % 4][x % 4] else 0), len(cielo) - 1)]
            px[x, y] = PAL[c]
    # sol bajo detrás de la sierra, a la derecha
    sx, sy = 372, 158
    for y in range(sy - 16, hz):
        for x in range(sx - 16, sx + 17):
            if math.hypot(x - sx, y - sy) <= 15:
                px[x, y] = PAL["cream"]
    # sierra lejana (dos planos) y el cerro del castillo con su muralla almenada
    for x in range(W):
        h1 = 150 + 8 * math.sin(x / 47) + 5 * math.sin(x / 13 + 1) + 3 * _h(x // 6, 0, 901)
        for y in range(int(h1), hz + 1):
            px[x, y] = PAL["purple_grey"]
        cer = 30 * math.exp(-((x - 300) / 70) ** 2)
        h2 = 160 - cer + 2 * math.sin(x / 9)
        for y in range(int(h2), H):
            px[x, y] = PAL["grey_brown"]
        if 248 <= x <= 356:                            # muralla en la cresta
            top = int(h2) - 6 - (2 if (x // 4) % 2 == 0 else 0)
            for y in range(top, int(h2) + 1):
                px[x, y] = PAL["grey_brown"]
            if x % 26 == 0:                            # torreones
                for y in range(top - 5, top):
                    for dx in range(-2, 3):
                        px[x + dx, y] = PAL["grey_brown"]
    # luz rasante del sol sobre la cresta (borde superior más claro)
    for x in range(W):
        for y in range(120, H):
            if px[x, y][:3] == PAL["grey_brown"][:3] and px[x, y - 1][:3] != PAL["grey_brown"][:3]:
                px[x, y] = PAL["rose_brown"]
                break
    # llanura y terraplén de la vía
    for y in range(176, H):
        for x in range(W):
            t = (y - 176) / (H - 176)
            c = "olive_dark" if t * 16 > BAYER[y % 4][x % 4] * .6 + 2 else "olive_grey"
            px[x, y] = PAL[c]
    for y in range(184, 196):                          # terraplén
        for x in range(W):
            px[x, y] = PAL["olive_grey" if y < 186 else "grey_brown"]
    for x in range(W):                                 # carriles
        px[x, 184] = PAL["concrete_light"]
        px[x, 186] = PAL["concrete"]
    # tren a contraluz: locomotora a la derecha, 4 vagones, ventanillas encendidas y faro
    x = 150
    for k, largo in enumerate((52, 52, 52, 52, 64)):
        top = 160 if k < 4 else 156
        for yy in range(top, 184):
            for xx in range(x, x + largo):
                px[xx, yy] = PAL["olive_shadow"]
        for xx in range(x, x + largo):
            px[xx, top] = PAL["grey_brown"]            # luz del cielo en el techo
        if k < 4:
            for wx in range(x + 5, x + largo - 6, 9):
                if _h(wx, k, 902) > .3:
                    for yy in range(166, 170):
                        for xx in range(wx, wx + 5):
                            px[xx, yy] = PAL["cream" if _h(xx // 5, k, 904) > .5 else "ochre_dark"]
        else:
            for yy in range(150, 156):                 # cabina
                for xx in range(x + 34, x + 56):
                    px[xx, yy] = PAL["olive_shadow"]
            for yy in range(158, 163):
                for xx in range(x + 46, x + 54):
                    px[xx, yy] = PAL["cream"]
        for wx in range(x + 6, x + largo - 4, 12):     # ruedas
            px[wx, 183] = PAL["slate_dark"]
            px[wx + 1, 183] = PAL["slate_dark"]
        x += largo + 3
    fx, fy = x - 2, 172                                # haz del faro hacia la derecha
    for xx in range(fx, W):
        semi = (xx - fx) * .12
        for yy in range(int(fy - semi), int(fy + semi) + 1):
            if 0 <= yy < H and ((xx + yy) % 2 == 0 or xx - fx < 12):
                px[xx, yy] = PAL["cream" if xx - fx < 40 else "beige"]
    # horda en primer plano: siluetas verticales #1c200c que vienen por el campo hacia el tren
    for k in range(70):
        hx = int(_h(k, 1, 903) * 230) - 10
        hy = 206 + int(_h(k, 2, 903) * 60)
        alto = 8 + int((hy - 206) / 60 * 14)
        ancho = max(2, alto // 3)
        for yy in range(hy - alto, hy):
            for xx in range(hx, hx + ancho):
                if 0 <= xx < W and yy < H:
                    px[xx, yy] = PAL["olive_black"]
        if alto >= 12:                                 # brazos al frente (D-N61), hacia la derecha
            for xx in range(hx + ancho, hx + ancho + alto // 4):
                if xx < W:
                    px[xx, hy - alto + alto // 4] = PAL["olive_black"]
    return im


# --- Exportación ----------------------------------------------------------------------------
def piezas():
    p = {"ui_panel": (panel(), 4), "ui_recurso_slot": (recurso_slot(), 4),
         "ui_tarjeta_unidad": (tarjeta_unidad(False), 3), "ui_tarjeta_unidad_sel": (tarjeta_unidad(True), 3),
         "ui_seleccion": (seleccion(), 5), "ui_seleccion_encima": (seleccion("beige"), 5)}
    for e in ("normal", "encima", "pulsado", "inactivo"):
        p[f"ui_boton_{e}"] = (boton(e), 4)
    for n in ("aviso", "ataque", "info"):
        p[f"ui_alerta_{n}"] = (alerta_slot(n), 4)
    for b in ("efectivos", "municion", "combustible", "baja"):
        p[f"ui_barra_{b}"] = (barra(b), 1)
    import build_maquetas as bm                          # D-N78
    p["ui_cartel_oleada"] = (bm.cartel_oleada(), 6)
    p["ui_barra_recarga"] = (bm.barra_recarga(), 1)
    for c in CALIENTE:
        p[f"ui_cursor_{c}"] = (cursor(c), 0)
    return p


def ordenes():
    """Iconos de orden de 32 y 16 del panel: los nuevos y los de A3 que encajan (mantener = aguantar)."""
    import build_a3 as a3
    from pixelvm import reduce_16
    o = {"mover": orden_mover_icono(), "atacar": orden_atacar_icono(), "mantener": a3.aguantar(),
         "emplazar": orden_emplazar(), "embarcar": orden_embarcar(), "desembarcar": orden_desembarcar()}
    return {n: (s, reduce_16(s)) for n, s in o.items()}


def exportar(out):
    out = Path(out)
    meta = {}
    for n, (s, sl) in piezas().items():
        s.save(out / f"{n}.png")
        meta[n] = {"size": [s.w, s.h], "slice": [sl] * 4}
        if n.startswith("ui_cursor_"):
            meta[n]["hotspot"] = CALIENTE[n[10:]]
    meta["ui_indicador_entrada"] = {"size": [64, 24], "frames": 2, "frameSize": [32, 24], "fps": 4, "loop": True,
                                    "nota": "mira a la derecha; se gira hacia la entrada activa fuera de cuadro; placa de 16x12 en 1,6 para «O1»–«O4»"}
    for nombre, fn in (("mover", orden_mover), ("atacar", orden_atacar)):
        hoja = Image.new("RGBA", (64, 16), (0, 0, 0, 0))
        for f in range(4):
            hoja.alpha_composite(fn(f).img, (16 * f, 0))
        hoja.save(out / f"ui_orden_{nombre}.png")
        meta[f"ui_orden_{nombre}"] = {"size": [64, 16], "frames": 4, "frameSize": [16, 16], "fps": 12, "loop": False}
    teclas = {"mover": "clic derecho", "atacar": "A", "mantener": "S", "emplazar": "D", "embarcar": "E", "desembarcar": "E"}
    for n, (a, b) in ordenes().items():
        a.save(out / f"vm_ordenes_{n}_32.png")
        b.save(out / f"vm_ordenes_{n}_16.png")
        meta[f"vm_ordenes_{n}"] = {"size": [32, 32], "tecla": teclas[n]}
    for n, fn in (("decal_linea_central_64x2", linea_central), ("decal_arcen_64x1", arcen), ("decal_helipuerto_128", helipuerto)):
        fn().save(out / f"{n}.png")
        meta[n] = {"texel_m": 0.1, "nota": "quad plano sobre el asfalto; la central y el arcén se repiten en x"}
    lg = logo()
    lg.save(out / "ui_logo_via_muerta.png")
    meta["ui_logo_via_muerta"] = {"size": [lg.w, lg.h], "nota": "título provisional (el del GDD); se escala x2–x4"}
    menu_fondo().save(out / "ui_menu_fondo.png")
    meta["ui_menu_fondo"] = {"size": [480, 270], "nota": "x4 a 1920x1080; logo arriba a la izquierda, botones en la columna izquierda"}
    (out / "ui_interfaz.json").write_text(json.dumps({"meta": {"viaMuerta": meta}}, indent=1, ensure_ascii=False))


def maqueta(out):
    """Maquetas x3 para revisar: menú con logo y botones, y HUD sobre una captura neutra de suelo."""
    out = Path(out)
    p = piezas()
    m = menu_fondo()
    lg = logo()
    m.alpha_composite(lg.img, (20, 20))
    for i, e in enumerate(("encima", "normal", "normal")):
        b = boton(e, 96, 16).img
        m.alpha_composite(b, (20, 120 + i * 22))
    m.resize((m.width * 3, m.height * 3), Image.NEAREST).save(out / "maqueta_menu_x3.png")
    # HUD
    import build_a1 as a1
    import build_a6 as a6
    import build_a9_pelotones as a9
    hud = Image.new("RGBA", (480, 270), PAL["beige"])
    for i, fn in enumerate((a1.municion, a1.combustible, a1.materiales, a1.poblacion)):
        x = 4 + i * 60
        hud.alpha_composite(recurso_slot().img, (x, 4))
        ic = fn().img.resize((16, 16), Image.NEAREST)
        hud.alpha_composite(ic, (x + 2, 2 + 2))
    hud.alpha_composite(alerta_slot("aviso").img, (4, 28))
    hud.alpha_composite(alerta_slot("ataque").img, (32, 28))
    s32, _ = a9.iconos()
    unidades = [a6.fusileros(), s32["ametralladores"], s32["granaderos"], s32["tiradores"], s32["zapadores"]]
    for i, u in enumerate(unidades):
        x = 120 + i * 44
        hud.alpha_composite(tarjeta_unidad(i == 1).img, (x, 218))
        hud.alpha_composite(u.img, (x + 4, 222))
        hud.alpha_composite(barra("efectivos", 32 - i * 5).img, (x + 4, 256))
        hud.alpha_composite(barra("municion" if i != 3 else "baja", 24 + i).img.crop((0, 0, 24 + i, 2)), (x + 4, 260))
    hud.alpha_composite(panel(120, 56).img, (356, 212))
    for i, (n, (a, b)) in enumerate(ordenes().items()):
        x, y = 362 + (i % 3) * 36, 220 + (i // 3) * 24
        hud.alpha_composite(boton("encima" if i == 1 else "normal", 32, 22).img, (x, y))
        hud.alpha_composite(b.img, (x + 8, y + 3))
    hud.alpha_composite(seleccion().img, (200, 120))
    hud.alpha_composite(orden_mover(2).img, (300, 150))
    hud.alpha_composite(orden_atacar(2).img, (330, 110))
    hud.alpha_composite(cursor("atacar").img, (336, 116))
    hud.resize((1440, 810), Image.NEAREST).save(out / "maqueta_hud_x3.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    exportar(OUT)
    maqueta(OUT)
    print("ok")
