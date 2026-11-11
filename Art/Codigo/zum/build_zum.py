"""build_zum.py: láminas de la estética del zum continuo (Hito 1 del núcleo v2). Bruno (Arte técnico), 11-11-2026.

Encargo de Lucía (docs/hito1-reparto.md): qué se ve en cada nivel del zum (mapa, comarca, pueblo y soldado), cómo se
funde de uno a otro, los iconos de unidades y trenes en el mapa, el trazado de vía con vista previa (válido o inválido)
y la calima de día adaptada. Son MAQUETAS de referencia para el prototipo de Marcos: el mundo (2 × 2 km de costa, A, B y
el caserío) es inventado aquí hasta que llegue el terreno de Álvaro. Determinista: el mismo script da los mismos píxeles.

Uso: python build_zum.py <carpeta_salida>
Salida: zum_0_regla.png (paleta, niveles y fundidos), zum_1_mapa.png, zum_2_comarca.png, zum_3_pueblo.png,
zum_4_soldado.png, zum_5_trazado.png y zum_paleta.json.
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

# --- Paleta aprobada de 32 (D-N46) ------------------------------------------------------------------------------------
HEX = {
    "black": "000000", "maroon": "340000", "olive_black": "1c200c", "navy": "181c30",
    "blood_dark": "4c1008", "brown_dark": "442c0c", "olive_shadow": "303424", "slate_dark": "283440",
    "wine": "642030", "red": "7c181c", "brown": "543820", "grey_brown": "48403c",
    "slate": "404460", "red_bright": "982420", "rust": "7c3c18", "rose_brown": "6c4038",
    "olive_dark": "50541c", "purple_grey": "60546c", "olive_grey": "605c48", "rose": "8c5048",
    "ochre_dark": "845c28", "olive": "70784c", "concrete": "6c7874", "olive_yellow": "847c34",
    "tan": "a86c50", "olive_light": "88885c", "concrete_light": "94948c", "amber": "e08c30",
    "beige": "b4a080", "grey_light": "b8b8b8", "bone": "d4d0cc", "cream": "f0dc9c",
}


def rgb(h):
    h = HEX.get(h, h).lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32)


CONTORNO = rgb("0f120c")
CALIMA = rgb("a27e5a")          # D-N197: el suelo de día tal como sale en pantalla (mediana de la captura de la I-Noche151)
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], dtype=np.float32) / 16.0 + 1 / 32

# Colores de las líneas de tren (uno por línea; nunca rojo, que es peligro, ni ámbar, que es aviso)
# Lucía (11-11): los de antes (tan, oliva claro, gris) se perdían en el suelo, la huerta y la vía. Son colores de interfaz, fuera de la
# paleta de 32 (que no tiene celeste, verde agua ni lila): L* 78–88, ΔL* ≥ 10 sobre el suelo de día más claro (b4a080, L* 67) y ≥ 39 sobre el
# plano del mapa (605c48, L* 39); con contorno 0f120c de 1 px. Tono distinto de todo el terreno.
LINEAS = ["f0dc9c", "8cc8e8", "7cd4a8", "d0b4f0"]

# --- Niveles del zum (cámara ortográfica: medio alto visible en metros) -------------------------------------------------
NIVELES = [
    # nombre, medio alto mínimo y máximo (m), m por píxel de la lámina
    ("soldado", 6, 40),
    ("pueblo", 40, 250),
    ("comarca", 250, 900),
    ("mapa", 900, 4000),
]
FUNDIDO = 0.25   # cada cambio de nivel se funde a lo largo de ±25 % del umbral, con tramado Bayer (alfa binaria)

# --- El mundo de la maqueta (2 × 2 km, x al este, y al sur, en metros) -------------------------------------------------
W = 2000.0
EST_A = (1460.0, 1610.0)        # estación A (sur, junto a la costa)
EST_B = (1120.0, 520.0)         # estación B (puesto avanzado, norte)
CASERIO = (760.0, 690.0)
PELOTON = (815.0, 702.0)        # pelotón entrando en el caserío por el este
VIA_HECHA = [EST_A, (1500, 1400), (1450, 1180), (1330, 980), (1250, 820)]          # tendida
VIA_PREVIA = [(1250, 820), (1185, 690), (1120, 520)]                                 # vista previa válida hasta B
VIA_MALA = [(1250, 820), (1150, 800), (1040, 735)]                                   # vista previa inválida (radio corto y casas)


def ruido(x, y, s, semilla):
    """Ruido de valor suave determinista (x, y arrays en m, s = tamaño de celda)."""
    gx, gy = x / s, y / s
    x0, y0 = np.floor(gx), np.floor(gy)
    fx, fy = gx - x0, gy - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)

    def h(ix, iy):
        v = (ix.astype(np.int64) * 374761393 + iy.astype(np.int64) * 668265263 + semilla * 1274126177) & 0xffffffff
        v = ((v ^ (v >> 13)) * 1274126177) & 0xffffffff
        return ((v ^ (v >> 16)) & 0xffff) / 65535.0

    a, b, c, d = h(x0, y0), h(x0 + 1, y0), h(x0, y0 + 1), h(x0 + 1, y0 + 1)
    return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy


def campos(x, y):
    """Altura (m), costa, cauce, carretera y huerta del mundo inventado."""
    costa_x = 1760 + 60 * np.sin(y / 260.0) + 30 * ruido(x, y, 90, 3)
    alt = 4 + 140 * np.clip((900 - x) / 900, 0, 1) ** 1.6 + 25 * ruido(x, y, 300, 1) + 8 * ruido(x, y, 70, 2)
    cauce_y = 1290 + 90 * np.sin(x / 310.0) + 25 * ruido(x, y, 160, 4)
    d_cauce = np.abs(y - cauce_y)
    alt = alt - 6 * np.clip(1 - d_cauce / 60, 0, 1)
    carretera_x = 1290 + 45 * np.sin(y / 420.0)
    d_carr = np.abs(x - carretera_x)
    return alt, costa_x, d_cauce, d_carr


def casas_caserio():
    """Huellas del caserío (cx, cy, ancho, fondo, giro, estado 0..2) en m."""
    r = []
    rnd = np.random.RandomState(7)
    for i in range(9):
        a = i * 0.7 + rnd.uniform(-0.2, 0.2)
        d = 18 + 9 * i ** 0.8
        r.append((CASERIO[0] + d * math.cos(a), CASERIO[1] + d * math.sin(a), rnd.uniform(9, 15), rnd.uniform(7, 11),
                  rnd.uniform(-0.3, 0.3), i % 3))
    return r


CASAS = casas_caserio()


def zombis(n=100):
    rnd = np.random.RandomState(11)
    a = rnd.uniform(0, 2 * math.pi, n)
    d = np.abs(rnd.normal(0, 38, n)) + 4
    return np.stack([CASERIO[0] + d * np.cos(a), CASERIO[1] + d * np.sin(a)], 1)


ZOMBIS = zombis()

# --- utilidades de dibujo ---------------------------------------------------------------------------------------------


class Lienzo:
    """Lámina a resolución de píxel (w × h) que cubre la ventana del mundo [x0, x0 + w·mpp] × [y0, y0 + h·mpp]."""

    def __init__(self, w, h, cx, cy, mpp):
        self.w, self.h, self.mpp = w, h, mpp
        self.x0, self.y0 = cx - w * mpp / 2, cy - h * mpp / 2
        self.px = np.zeros((h, w, 3), np.float32)
        jj, ii = np.mgrid[0:h, 0:w]
        self.X = self.x0 + (ii + 0.5) * mpp
        self.Y = self.y0 + (jj + 0.5) * mpp
        self.I, self.J = ii, jj

    def a_px(self, x, y):
        return (x - self.x0) / self.mpp, (y - self.y0) / self.mpp

    def pon(self, mask, c, alfa=1.0, tramado=False):
        c = rgb(c) if isinstance(c, str) else c
        if tramado:
            mask = mask & (BAYER[self.J % 4, self.I % 4] < alfa)
            self.px[mask] = c
        elif alfa >= 1:
            self.px[mask] = c
        else:
            self.px[mask] = self.px[mask] * (1 - alfa) + c * alfa

    def linea(self, pts, c, ancho_m, alfa=1.0, guiones_m=None, tramado=False):
        """Polilínea en metros con grosor en metros (mínimo 1 px)."""
        ancho = max(ancho_m / self.mpp, 1.0)
        mask = np.zeros((self.h, self.w), bool)
        dist_acum = 0.0
        for (ax, ay), (bx, by) in zip(pts[:-1], pts[1:]):
            dx, dy = bx - ax, by - ay
            L = math.hypot(dx, dy)
            t = np.clip(((self.X - ax) * dx + (self.Y - ay) * dy) / (L * L), 0, 1)
            d = np.hypot(self.X - (ax + t * dx), self.Y - (ay + t * dy)) / self.mpp
            m = d <= ancho / 2
            if guiones_m:
                s = dist_acum + t * L
                m &= (np.floor(s / guiones_m) % 2) == 0
            mask |= m
            dist_acum += L
        self.pon(mask, c, alfa, tramado)
        return mask

    def disco(self, x, y, r_px, c, anillo=0):
        px, py = self.a_px(x, y)
        d = np.hypot(self.I + 0.5 - px, self.J + 0.5 - py)
        if anillo:
            self.pon((d <= r_px) & (d > r_px - anillo), c)
        else:
            self.pon(d <= r_px, c)

    def rect_m(self, cx, cy, w, h, giro, c, alfa=1.0):
        ca, sa = math.cos(giro), math.sin(giro)
        u = (self.X - cx) * ca + (self.Y - cy) * sa
        v = -(self.X - cx) * sa + (self.Y - cy) * ca
        m = (np.abs(u) <= w / 2) & (np.abs(v) <= h / 2)
        self.pon(m, c, alfa)
        return m

    def imagen(self, escala):
        im = Image.fromarray(np.clip(self.px, 0, 255).astype(np.uint8), "RGB")
        return im.resize((self.w * escala, self.h * escala), Image.NEAREST)


# --- iconos (los mismos dibujos que IconosMapa.cs, más los nuevos del núcleo v2) ----------------------------------------
ICONOS = {
    "tren": ["................", "................", "................", "................",
             ".......###......", ".......#o#..#...", ".#########..##..", ".#o#o#o###..###.",
             ".#########..##..", "..##.##.##..#...", "................", "................",
             "................", "................", "................", "................"],
    "tren_parado": ["................", "................", "................", "................",
                    "................", "...........###..", "...........#o#..", ".#############..",
                    ".#o#o#o#o#####..", ".#############..", "..##.##...##.##.", "................",
                    "................", "................", "................", "................"],
    "soldado": ["................", "......##........", ".....####.......", "......##........",
                ".....####.......", "....######......", "....#.##.##.....", "....#.##..##....",
                "......##...#....", ".....####.......", ".....#..#.......", ".....#..#.......",
                "....##..##......", "................", "................", "................"],
    "estacion": ["................", "................", ".....######.....", "....########....",
                 "...##########...", "...##########...", "...##########...", "...##########...",
                 "...##########...", "...##########...", "....########....", ".....######.....",
                 "................", "................", "................", "................"],
    # nuevos (núcleo v2): cuadrilla de vía (pico al hombro) y depósito de material (pila de carriles)
    "cuadrilla": ["................", ".....##...#.....", "....####.##.....", ".....##.##......",
                  "....#####.......", "...######.......", "...#.##.#.......", "...#.##.#.......",
                  ".....##.........", "....####........", "....#..#........", "....#..#........",
                  "...##..##.......", "................", "................", "................"],
    "deposito": ["................", "................", "................", "................",
                 "................", "..############..", "..#o#o#o#o#o##..", "..############..",
                 "...##########...", "...#o#o#o#o##...", "...##########...", "....########....",
                 "....#o#o#o##....", "....########....", "................", "................"],
}


def icono(nombre, px=16, relleno="bone", girar=0):
    rows = ICONOS[nombre]
    e = px // 16
    m = np.zeros((px, px), np.uint8)
    for y in range(16):
        for x in range(16):
            c = rows[y][x]
            if c != ".":
                m[y * e:(y + 1) * e, x * e:(x + 1) * e] = 2 if c == "o" else 1
    if girar:
        m = np.rot90(m, girar)
    img = np.zeros((px, px, 4), np.uint8)
    img[m == 1, :3] = rgb(relleno)
    img[m == 1, 3] = 255
    img[m == 2, :3] = CONTORNO
    img[m == 2, 3] = 255
    lleno = m > 0
    borde = np.zeros_like(lleno)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            borde |= np.roll(np.roll(lleno, dy, 0), dx, 1)
    borde &= ~lleno
    img[borde, :3] = CONTORNO
    img[borde, 3] = 255
    return Image.fromarray(img, "RGBA")


def pega(im, ic, x, y):
    im.alpha_composite(ic, (int(x - ic.width / 2), int(y - ic.height / 2))) if im.mode == "RGBA" else im.paste(ic, (int(x - ic.width / 2), int(y - ic.height / 2)), ic)


def placa(d, x, y, texto, fuente, fondo="olive_shadow", tinta="bone", borde=None):
    """Rótulo del mapa: placa oscura con texto hueso (el texto real lo pone la interfaz v3; aquí, para leer la maqueta)."""
    b = d.textbbox((0, 0), texto, font=fuente)
    w, h = b[2] - b[0] + 8, b[3] - b[1] + 6
    d.rectangle([x, y, x + w, y + h], fill=tuple(int(v) for v in rgb(fondo)), outline=tuple(int(v) for v in rgb(borde)) if borde else None)
    d.text((x + 4, y + 2 - b[1]), texto, font=fuente, fill=tuple(int(v) for v in rgb(tinta)))


def fuente(t):
    for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf"):
        if Path(p).exists():
            return ImageFont.truetype(p, t)
    return ImageFont.load_default()


F_T, F_N, F_P = fuente(22), fuente(14), fuente(12)

# --- terreno por nivel -------------------------------------------------------------------------------------------------


def terreno_mapa(L):
    """Mapa: el plano táctico oscuro de siempre (MapaTactico, D-N113), con la red construible encima."""
    alt, costa_x, d_cauce, d_carr = campos(L.X, L.Y)
    bandas = np.where(alt < 40, 0, np.where(alt < 90, 1, 2))
    cols = [rgb("olive_shadow"), rgb("grey_brown"), rgb("olive_grey")]
    for i, c in enumerate(cols):
        L.pon(bandas == i, c)
    # sombreado de ladera: la que mira al noroeste sube un escalón (tramado al 50 %)
    gx = np.gradient(alt, axis=1)
    gy = np.gradient(alt, axis=0)
    luz = (-gx - gy) > 0.9 * L.mpp / 4
    L.pon(luz & (bandas == 0), "grey_brown", 0.5, True)
    L.pon(luz & (bandas == 1), "olive_grey", 0.5, True)
    # curvas cada 20 m y maestras cada 100 m
    for paso, c in ((20, "olive_dark"), (100, "concrete")):
        q = np.floor(alt / paso)
        borde = (q != np.roll(q, 1, 0)) | (q != np.roll(q, 1, 1))
        L.pon(borde & (alt > 10), c)
    # huerta en el llano: lindes de parcela
    hu = (alt < 30) & (L.X < costa_x - 40)
    u = (L.X * 0.96 + L.Y * 0.28)
    v = (-L.X * 0.28 + L.Y * 0.96)
    lindes = ((np.floor(u / 60) != np.floor((u - L.mpp) / 60)) | (np.floor(v / 110) != np.floor((v - L.mpp) / 110)))
    L.pon(hu & lindes, "olive_dark", 0.35, True)
    L.pon(d_carr < max(4, L.mpp), "concrete")
    L.pon(d_cauce < max(10, L.mpp), "slate_dark")
    L.pon(d_cauce < max(3, L.mpp * 0.6), "concrete")
    L.pon(L.X >= costa_x, "navy")
    borde_costa = (L.X >= costa_x) & (L.X - L.mpp < costa_x)
    L.pon(borde_costa, "concrete_light")
    for (cx, cy, w, h, g, e) in CASAS:
        L.rect_m(cx, cy, w + 2, h + 2, g, "olive_grey")


def terreno_dia(L, detalle):
    """Comarca, pueblo y soldado: el suelo de día de la zona (texturas de suelo de Sagunto), con más detalle cuanto más cerca."""
    alt, costa_x, d_cauce, d_carr = campos(L.X, L.Y)
    mota = ruido(L.X, L.Y, max(L.mpp * 2, 0.2), 9)
    seca = alt >= 30
    L.pon(np.ones_like(seca), "tan")                                   # apisonada
    L.pon(seca & (mota > 0.55), "beige")                               # seca en los altos
    L.pon(~seca & (mota > 0.62), "ochre_dark", 1.0)                    # motas
    hu = (alt < 30) & (L.X < costa_x - 40)
    u = (L.X * 0.96 + L.Y * 0.28)
    v = (-L.X * 0.28 + L.Y * 0.96)
    parcela = (np.floor(u / 60) * 7 + np.floor(v / 110) * 13) % 5
    lindes = ((np.floor(u / 60) != np.floor((u - L.mpp) / 60)) | (np.floor(v / 110) != np.floor((v - L.mpp) / 110)))
    naranjal = hu & (parcela <= 1)
    labrada = hu & (parcela == 2)
    L.pon(labrada, "brown")
    surco = (np.floor(u / 2.4) % 2) == 0
    L.pon(labrada & surco, "ochre_dark")
    L.pon(naranjal, "ochre_dark")
    # naranjos en marco de 5 m (copa de 2 m): se dibujan como manchas oliva con sombra al sureste
    cu, cv = (u % 5) - 2.5, (v % 5) - 2.5
    copa = naranjal & (np.hypot(cu, cv) < 1.3)
    sombra = naranjal & (np.hypot(cu - 0.6, cv - 0.6) < 1.3) & ~copa
    L.pon(sombra, "olive_shadow")
    L.pon(copa, "olive")
    L.pon(copa & (np.hypot(cu + 0.4, cv + 0.4) < 0.6), "olive_light")
    L.pon(hu & lindes, "olive_grey")
    # carretera vieja y cauce
    L.pon(d_carr < 4.5, "concrete")
    L.pon((d_carr < 4.5) & (mota > 0.7), "concrete_light")
    L.pon((d_carr >= 4.5) & (d_carr < 5.5), "grey_brown")
    L.pon(d_cauce < 12, "olive_grey")
    L.pon(d_cauce < 4, "concrete")
    L.pon((d_cauce >= 3.4) & (d_cauce < 4.0), "concrete_light")
    L.pon(L.X >= costa_x - 25, "beige")                                 # playa
    L.pon(L.X >= costa_x, "slate")
    L.pon((L.X >= costa_x) & (mota > 0.75), "concrete")
    if detalle >= 3:     # soldado: grietas y rodadas de las texturas de cerca
        grieta = (np.abs(ruido(L.X, L.Y, 3.0, 21) - 0.5) < 0.005) & (alt >= 0)
        L.pon(grieta & ~hu, "brown")


def casas_dia(L, im_d, detalle):
    """Casas del caserío: tejado (terracota o hundido), sombra al sureste (sol del noroeste)."""
    for (cx, cy, w, h, g, e) in CASAS:
        L.rect_m(cx + 1.5, cy + 1.5, w, h, g, "grey_brown")               # sombra
        L.rect_m(cx, cy, w, h, g, "concrete_light" if e == 2 else "rose")
        if e == 0:
            L.rect_m(cx - w * 0.25, cy, w * 0.5, h, g, "tan")               # agua del tejado al sol
        elif e == 1:   # tejado hundido: se ve el interior oscuro
            L.rect_m(cx + w * 0.15, cy, w * 0.45, h * 0.6, g, "brown_dark")
        else:          # sin tejado: muros y escombro
            L.rect_m(cx, cy, w - 1.2, h - 1.2, g, "grey_brown")
            if detalle >= 2:
                L.rect_m(cx + 1, cy - 0.5, w * 0.3, h * 0.25, g + 0.4, "concrete")


def via_dia(L, pts, construida=True):
    L.linea(pts, "olive_grey", 4.2)                                      # balasto
    if L.mpp <= 0.6:
        # traviesas cada 0,6 m
        for (ax, ay), (bx, by) in zip(pts[:-1], pts[1:]):
            dx, dy = bx - ax, by - ay
            Lg = math.hypot(dx, dy)
            t = np.clip(((L.X - ax) * dx + (L.Y - ay) * dy) / (Lg * Lg), 0, 1)
            s = t * Lg
            d = np.hypot(L.X - (ax + t * dx), L.Y - (ay + t * dy))
            L.pon((d < 1.3) & ((s % 0.6) < 0.22), "concrete")
    for off in (-0.72, 0.72):
        L.linea(desplaza(pts, off), "grey_light", 0.12)


def desplaza(pts, off):
    r = []
    for i, (x, y) in enumerate(pts):
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, len(pts) - 1)]
        dx, dy = b[0] - a[0], b[1] - a[1]
        n = math.hypot(dx, dy) or 1
        r.append((x - dy / n * off, y + dx / n * off))
    return r


def invalido(L, pts):
    """Trazado inválido (Lucía, 11-11: el ámbar fino no se leía sobre la huerta): banda ámbar llena de 6 m con borde 0f120c de 1 px y eje oscuro."""
    ancho = max(6.0, 3 * L.mpp)
    L.linea(pts, CONTORNO, ancho + 2 * L.mpp)
    L.linea(pts, "amber", ancho)
    L.linea(pts, CONTORNO, L.mpp, guiones_m=6)


def aspa(dr, a, b, r=8):
    """Aspa ámbar con borde 0f120c en el punto que invalida."""
    for t in range(-r, r + 1):
        for w in (-2, -1, 0, 1, 2):
            c = rgb("amber") if abs(w) <= 1 else CONTORNO
            dr.point([(a + t, b + t + w), (a + t, b - t + w)], fill=tuple(int(v) for v in c))


def calima(L, alfa):
    """D-N197 adaptada al zum: ya no es una franja en el borde de la zona (el mundo es continuo), sino perspectiva aérea
    que sube con la altura de la cámara: 0 % a pie de soldado, 10 % en el pueblo, 22 % en la comarca. En el mapa no hay
    calima: el plano táctico es un dibujo, no una vista."""
    if alfa > 0:
        L.px = L.px * (1 - alfa) + CALIMA * alfa


# --- láminas ----------------------------------------------------------------------------------------------------------
ESC = 2
PW, PH = 640, 360                     # píxeles de la lámina (×2 en la imagen)
PANEL = 420


def marco(im_juego, titulo, lineas, nivel_activo):
    """Lámina completa: la vista a la izquierda, la ficha a la derecha y la barra de niveles abajo."""
    W_, H_ = im_juego.width + PANEL, im_juego.height + 70
    out = Image.new("RGB", (W_, H_), tuple(int(v) for v in rgb("navy")))
    out.paste(im_juego, (0, 0))
    d = ImageDraw.Draw(out)
    x = im_juego.width + 18
    d.text((x, 16), titulo, font=F_T, fill=tuple(int(v) for v in rgb("bone")))
    y = 54
    for ln in lineas:
        color = "cream" if ln.startswith("•") is False and ln.endswith(":") else "bone"
        for trozo in envuelve(ln, F_N, PANEL - 36, d):
            d.text((x, y), trozo, font=F_N, fill=tuple(int(v) for v in rgb(color)))
            y += 19
        y += 4
    # barra de niveles
    yb = im_juego.height + 18
    n = len(NIVELES)
    ancho = (W_ - 40) // n
    for i, (nom, a, b) in enumerate(reversed(NIVELES)):
        xb = 20 + i * ancho
        act = nom == nivel_activo
        d.rectangle([xb, yb, xb + ancho - 8, yb + 34], fill=tuple(int(v) for v in rgb("olive" if act else "olive_shadow")),
                    outline=tuple(int(v) for v in rgb("bone" if act else "grey_brown")))
        d.text((xb + 10, yb + 8), f"{nom.upper()}   medio alto {a}–{b} m", font=F_N,
               fill=tuple(int(v) for v in rgb("bone" if act else "concrete_light")))
    return out


def envuelve(t, f, ancho, d):
    pal, r, cur = t.split(" "), [], ""
    for p in pal:
        prueba = (cur + " " + p).strip()
        if d.textlength(prueba, font=f) > ancho and cur:
            r.append(cur)
            cur = p
        else:
            cur = prueba
    if cur:
        r.append(cur)
    return r


def lamina_mapa(out):
    L = Lienzo(PW, PH, 1080, 1000, 1.25 * 2000 / PW)
    terreno_mapa(L)
    # horda del caserío: mancha roja tramada (MapaTactico.Mancha, D-N185)
    d = np.hypot(L.X - CASERIO[0], L.Y - CASERIO[1])
    dens = np.clip(1 - d / 95, 0, 1)
    L.pon(dens > 0.05, "red_bright", 0.85 * dens.max(), True) if False else None
    m = (dens > 0.05) & (BAYER[L.J % 4, L.I % 4] < np.clip((dens - 0.05) * 1.6, 0, 0.85))
    L.pon(m, "red_bright")
    # vía tendida (hueso, 2 px) y vista previa no se ve en el mapa salvo trazando
    L.linea(VIA_HECHA + VIA_PREVIA[1:], "bone", 2 * L.mpp)
    # ruta de la línea 1 (A↔B): guiones en el color de la línea, por fuera de la vía
    L.linea(desplaza(VIA_HECHA + VIA_PREVIA[1:], 3 * L.mpp), LINEAS[0], 1 * L.mpp, guiones_m=8 * L.mpp)
    im = L.imagen(ESC).convert("RGBA")
    dr = ImageDraw.Draw(im)
    def P(x, y):
        a, b = L.a_px(x, y)
        return a * ESC, b * ESC
    for (x, y), nom in ((EST_A, "A · 120 cajas"), (EST_B, "B · 18 cajas")):
        px, py = P(x, y)
        pega(im, icono("estacion", 32), px, py)
        placa(dr, px + 20, py - 12, nom, F_P)
    tx, ty = P(1450, 1180)
    pega(im, icono("tren", 32, relleno=LINEAS[0], girar=1), tx, ty)
    placa(dr, tx + 20, ty - 10, "L1 · 4 cajas por viaje · un viaje cada 6,7 min", F_P, tinta="cream")
    px, py = P(*PELOTON)
    pega(im, icono("soldado", 32), px, py)
    placa(dr, px + 18, py + 6, "1.º pelotón · 52 %", F_P)
    cx, cy = P(*VIA_HECHA[-1])
    pega(im, icono("cuadrilla", 32), cx + 22, cy + 8)
    zx, zy = P(*CASERIO)
    pega(im, icono("deposito", 32), zx - 30, zy - 24)
    im = im.convert("RGB")
    lam = marco(im, "1 · MAPA", [
        "Qué se ve:",
        "• El plano táctico oscuro de siempre (D-N113): bandas de altura, curvas cada 20 m y maestras cada 100, mar navy, costa en 94948c, lindes de la huerta.",
        "• La red: vía tendida en hueso de 2 px; cada línea, en guiones de su color por fuera de la vía (L1 crema). Nunca rojo ni ámbar en una línea.",
        "• Iconos de 32 px de tamaño fijo en pantalla: estación (disco), tren de la línea teñido de su color y girado con el rumbo, pelotón, cuadrilla y depósito.",
        "• Placas: almacén en cajas, la línea como «4 cajas por viaje · un viaje cada 6,7 min» y la munición del pelotón. Nunca «por hora»: el reloj va a ×30 y el movimiento a 1:1 (D-N208). Las raciones, por día de juego.",
        "• Horda: mancha roja 982420 tramada, como ahora.",
        "Qué NO se ve: modelos 3D, suelo de día, soldados sueltos, zombis sueltos, calima.",
    ], "mapa")
    lam.save(out / "zum_1_mapa.png")


def lamina_comarca(out):
    L = Lienzo(PW, PH, 1040, 690, 1.0)
    terreno_dia(L, 1)
    casas_dia(L, None, 1)
    via_dia(L, VIA_HECHA)
    calima(L, 0.22)
    # horda: a esta escala aún mancha (tramado rojo más fino, 1 de cada 2 píxeles como máximo)
    d = np.hypot(L.X - CASERIO[0], L.Y - CASERIO[1])
    dens = np.clip(1 - d / 60, 0, 1)
    L.pon((dens > 0.05) & (BAYER[L.J % 4, L.I % 4] < np.clip((dens - 0.05) * 1.0, 0, 0.4)), "red_bright")
    # vista previa de la vía hasta B (válida) y una alternativa inválida
    L.linea(VIA_PREVIA, "bone", 4.2, alfa=0.55, tramado=True)
    L.linea(VIA_PREVIA, "bone", 1.0, guiones_m=6)
    invalido(L, VIA_MALA)
    im = L.imagen(ESC).convert("RGBA")
    dr = ImageDraw.Draw(im)
    def P(x, y):
        a, b = L.a_px(x, y)
        return a * ESC, b * ESC
    bx, by = P(*EST_B)
    pega(im, icono("estacion", 32), bx, by)
    placa(dr, bx + 22, by - 12, "B · 18 cajas", F_P)
    # aspa ámbar en el punto que invalida (radio < 150 m y casas)
    ax, ay = P(1040, 735)
    aspa(dr, ax, ay)
    placa(dr, ax - 120, ay + 14, "Curva demasiado cerrada", F_P, tinta="amber", borde="amber")
    mx, my = P(1185, 690)
    placa(dr, mx + 14, my - 4, "330 m · 4 palés", F_P, tinta="bone")
    px, py = P(*PELOTON)
    pega(im, icono("soldado", 32), px + 20, py - 20)
    cx, cy = P(*VIA_HECHA[-1])
    pega(im, icono("cuadrilla", 32), cx + 20, cy + 4)
    im = im.convert("RGB")
    lam = marco(im, "2 · COMARCA", [
        "Qué se ve:",
        "• El suelo de día ya cocido por trozos (huerta, naranjos, carretera, cauce, playa) y las casas como tejados con sombra. Sin modelos 3D todavía: huellas planas.",
        "• La vía tendida con su balasto y sus raíles; las traviesas aún no.",
        "• Calima de día al 22 % (D-N197 adaptada): perspectiva aérea, no franja.",
        "• Unidades y trenes siguen como iconos de 32 px (un pelotón mide aquí menos de 10 px: no se leería).",
        "• Horda: la mancha roja, más fina.",
        "Trazado (este es el nivel donde se traza):",
        "• Válido: banda hueso tramada al 55 % del ancho del balasto y eje en guiones; placa con metros y material.",
        "• Inválido: banda ámbar e08c30 de 6 m con borde 0f120c, el aspa ámbar con borde donde falla y la causa en la placa. Ámbar = aviso; el rojo se queda para el peligro.",
    ], "comarca")
    lam.save(out / "zum_2_comarca.png")


def figura(L, x, y, alto_m, cuerpo, cabeza="tan", arma=False, sel=False):
    """Soldado o zombi visto desde arriba en 3/4: elipse de cuerpo, cabeza y sombra."""
    px, py = L.a_px(x, y)
    r = max(alto_m * 0.16 / L.mpp, 1.0)
    d = np.hypot(L.I + 0.5 - px - r * 0.6, (L.J + 0.5 - py - r * 0.5) * 1.6)
    L.pon(d < r * 1.1, "olive_shadow", 0.6)
    d = np.hypot(L.I + 0.5 - px, (L.J + 0.5 - py) * 1.3)
    L.pon(d < r, cuerpo)
    d = np.hypot(L.I + 0.5 - px, L.J + 0.5 - py + r * 0.9)
    L.pon(d < r * 0.55, cabeza)
    if arma:
        L.linea([(x + alto_m * 0.05, y - alto_m * 0.05), (x + alto_m * 0.45, y - alto_m * 0.25)], "black", alto_m * 0.05)
    if sel:
        d = np.hypot(L.I + 0.5 - px, (L.J + 0.5 - py) * 1.6)
        L.pon((d < r * 2.2) & (d > r * 2.2 - 1.2), "bone")


SOLDADOS = [(PELOTON[0] - 8 + 2.2 * i, PELOTON[1] - 2 + (i % 3) * 1.8) for i in range(8)]


def lamina_pueblo(out):
    L = Lienzo(PW, PH, CASERIO[0] + 22, CASERIO[1] + 4, 0.14)
    terreno_dia(L, 2)
    casas_dia(L, None, 2)
    calima(L, 0.10)
    for (x, y) in ZOMBIS:
        figura(L, x, y, 1.8, "olive_black", cabeza="grey_brown")
    for i, (x, y) in enumerate(SOLDADOS):
        figura(L, x, y, 1.8, "olive_dark", arma=True, sel=True)
    im = L.imagen(ESC).convert("RGBA")
    dr = ImageDraw.Draw(im)
    def P(x, y):
        a, b = L.a_px(x, y)
        return a * ESC, b * ESC
    sx = np.mean([s[0] for s in SOLDADOS])
    sy = min(s[1] for s in SOLDADOS)
    px, py = P(sx, sy)
    pega(im, icono("soldado", 16), px, py - 36)
    # barra de munición del pelotón sobre el icono (hueso sobre olive_shadow; ámbar por debajo del 25 %)
    dr.rectangle([px - 16, py - 24, px + 16, py - 20], fill=tuple(int(v) for v in rgb("olive_shadow")))
    dr.rectangle([px - 16, py - 24, px - 16 + int(32 * 0.52), py - 20], fill=tuple(int(v) for v in rgb("bone")))
    zx, zy = P(*CASERIO)
    pega(im, icono("deposito", 16), zx, zy - 30)
    im = im.convert("RGB")
    lam = marco(im, "3 · PUEBLO", [
        "Qué se ve:",
        "• Los modelos 3D de Teo (casas en ruinas, vía, estación) y el suelo con las texturas de cerca. Es la vista de zona de ahora.",
        "• Cada soldado y cada zombi con cuerpo. Los zombis, en la banda de la horda (L* 9–13, 1c200c) sobre el suelo de día: ΔL* ≥ 8.",
        "• El pelotón con su anillo de selección y, encima, un icono de 16 px con la barra de munición (ámbar por debajo del 25 %). El icono se apaga del todo al pasar a SOLDADO.",
        "• El depósito de material con su icono de 16 px hasta que se recoge.",
        "• Calima al 10 %.",
        "Qué NO se ve: la mancha roja (aquí los zombis ya son individuos), las placas de almacén (pasan a la interfaz).",
    ], "pueblo")
    lam.save(out / "zum_3_pueblo.png")


def lamina_soldado(out):
    cx, cy = SOLDADOS[3][0] - 4, SOLDADOS[3][1] - 2
    L = Lienzo(PW, PH, cx, cy, 0.05)
    terreno_dia(L, 3)
    for (x, y) in ZOMBIS:
        if abs(x - cx) < 20 and abs(y - cy) < 12:
            figura(L, x, y, 1.8, "olive_black", cabeza="grey_brown")
    extra = [(cx - 13, cy - 3), (cx - 14.5, cy + 1.5), (cx - 12, cy + 4), (cx - 15.5, cy - 6.5), (cx - 11, cy - 7.5)]
    for (x, y) in extra:
        figura(L, x, y, 1.8, "olive_black", cabeza="grey_brown")
    for (x, y) in SOLDADOS:
        figura(L, x, y, 1.8, "olive_dark", arma=True, sel=True)
    im = L.imagen(ESC).convert("RGBA")
    dr = ImageDraw.Draw(im)
    for i, (x, y) in enumerate(SOLDADOS):
        a, b = L.a_px(x, y)
        a, b = a * ESC, b * ESC
        if not (0 <= a < im.width and 0 <= b < im.height):
            continue
        frac = [0.9, 0.7, 0.45, 0.2, 0.8, 0.6, 0.33, 0.15][i]
        col = "amber" if frac < 0.25 else "bone"
        dr.rectangle([a - 18, b - 46, a + 18, b - 42], fill=tuple(int(v) for v in rgb("olive_shadow")))
        dr.rectangle([a - 18, b - 46, a - 18 + int(36 * frac), b - 42], fill=tuple(int(v) for v in rgb(col)))
    im = im.convert("RGB")
    lam = marco(im, "4 · SOLDADO", [
        "Qué se ve:",
        "• Todo el detalle: modelos, armas, fogonazos y VFX, decals del suelo, y la cuenta de cada soldado.",
        "• Sobre cada soldado, su barra de munición (60 cartuchos; ámbar por debajo del 25 %). Sin iconos: aquí sobran.",
        "• Sin calima (0 %).",
        "• Es el zum de la noche de ahora (medio alto 6–35). De noche no se puede pasar de PUEBLO hacia arriba sin que la horda lejana pase a mancha.",
    ], "soldado")
    lam.save(out / "zum_4_soldado.png")


def lamina_trazado(out):
    """Estados del trazado de vía a escala de comarca: ratón libre, enganche, válido, inválido y tendido."""
    cajas = []
    titulos = ["1 · Libre", "2 · Engancha a B", "3 · Válido", "4 · Inválido", "5 · Tendiéndose"]
    for k in range(5):
        L = Lienzo(240, 200, 1160, 675, 1.6)
        terreno_dia(L, 1)
        casas_dia(L, None, 1)
        via_dia(L, VIA_HECHA)
        calima(L, 0.22)
        if k == 0:
            L.linea([(1250, 820), (1215, 740)], "bone", 1.0, guiones_m=6)
            L.disco(1215, 740, 3, "bone", anillo=1)
        if k == 1:
            L.linea([(1250, 820), (1185, 690), (1132, 548)], "bone", 1.0, guiones_m=6)
            L.disco(*EST_B, 9, "bone", anillo=2)
        if k == 2:
            L.linea(VIA_PREVIA, "bone", 4.2, alfa=0.55, tramado=True)
            L.linea(VIA_PREVIA, "bone", 1.0, guiones_m=6)
        if k == 3:
            invalido(L, VIA_MALA)
        if k == 4:
            via_dia(L, [(1250, 820), (1215, 750)])
            L.linea([(1215, 750), (1185, 690), (1120, 520)], "bone", 4.2, alfa=0.3, tramado=True)
        im = L.imagen(2).convert("RGBA")
        dr = ImageDraw.Draw(im)
        if k == 3:
            a, b = L.a_px(1040, 735)
            a, b = a * 2, b * 2
            aspa(dr, a, b)
            placa(dr, a - 60, b + 14, "Pendiente excesiva", F_P, tinta="amber", borde="amber")
        if k == 0:
            a, b = L.a_px(1215, 740)
            placa(dr, a * 2 + 12, b * 2 - 10, "90 m · 1 palé", F_P)
        if k == 1:
            a, b = L.a_px(*EST_B)
            placa(dr, a * 2 + 22, b * 2 + 4, "1250 m · 13 palés", F_P)
        if k == 2:
            a, b = L.a_px(1185, 690)
            placa(dr, a * 2 + 12, b * 2 - 10, "1250 m · 13 palés", F_P)
        if k == 4:
            a, b = L.a_px(1215, 750)
            pega(im, icono("cuadrilla", 32), a * 2 + 18, b * 2)
            placa(dr, a * 2 - 190, b * 2 + 18, "Tendiendo · 480 de 1250 m", F_P)
        cajas.append(im.convert("RGB"))
    W_ = sum(c.width for c in cajas) + 20 * 6
    out_im = Image.new("RGB", (W_, cajas[0].height + 200), tuple(int(v) for v in rgb("navy")))
    d = ImageDraw.Draw(out_im)
    d.text((20, 14), "5 · TRAZADO DE VÍA CON VISTA PREVIA (a escala de comarca)", font=F_T, fill=tuple(int(v) for v in rgb("bone")))
    x = 20
    notas = [
        "Desde la punta de la vía sale el eje en guiones hueso hasta el ratón; un anillo de 3 px marca el punto.",
        "Cerca de una estación (o de otra vía) el eje se engancha: anillo hueso de 2 px alrededor del destino.",
        "Tramo válido: banda hueso tramada al 55 % con el ancho del balasto. Placa: metros y palés de material.",
        "Inválido: banda ámbar llena de 6 m con borde 0f120c, aspa ámbar con borde en el fallo (radio, pendiente, casa, agua sin puente) y la causa en la placa con borde ámbar.",
        "Al confirmar: el tramo queda tramado al 30 % y la cuadrilla lo va convirtiendo en vía de verdad (con escolta).",
    ]
    for c, t, n in zip(cajas, titulos, notas):
        out_im.paste(c, (x, 56))
        d.text((x, 56 + c.height + 10), t, font=F_N, fill=tuple(int(v) for v in rgb("cream")))
        yy = 56 + c.height + 32
        for trozo in envuelve(n, F_P, c.width, d):
            d.text((x, yy), trozo, font=F_P, fill=tuple(int(v) for v in rgb("bone")))
            yy += 16
        x += c.width + 20
    out_im.save(out / "zum_5_trazado.png")


def lamina_regla(out):
    """Paleta del zum, umbrales y fundidos (tramado Bayer, alfa binaria)."""
    im = Image.new("RGB", (1700, 1150), tuple(int(v) for v in rgb("navy")))
    d = ImageDraw.Draw(im)
    d.text((20, 14), "0 · REGLA DEL ZUM: niveles, fundidos y paleta", font=F_T, fill=tuple(int(v) for v in rgb("bone")))
    # tira de fundido mapa → comarca → pueblo → soldado: 9 cuadros de la misma vista con la cámara bajando
    altos = [1600, 1050, 900, 760, 420, 250, 200, 60, 20]
    x = 20
    for k, a in enumerate(altos):
        cx, cy = 1000 + (CASERIO[0] - 1000) * min(1, (1600 - a) / 1400), 700
        mpp = a * 2 / 160
        A = Lienzo(160, 160, cx, cy, mpp)
        terreno_mapa(A)
        B = Lienzo(160, 160, cx, cy, mpp)
        terreno_dia(B, 1 if a > 250 else 2)
        casas_dia(B, None, 1)
        calima(B, 0.22 if a > 250 else 0.10 if a > 40 else 0.0)
        # mezcla: por encima de 900·(1+F) todo mapa; por debajo de 900·(1−F) todo día; en medio, tramado
        t = np.clip((a - 900 * (1 - FUNDIDO)) / (900 * 2 * FUNDIDO), 0, 1)
        mask = BAYER[A.J % 4, A.I % 4] < t
        C = Lienzo(160, 160, cx, cy, mpp)
        C.px = np.where(mask[..., None], A.px, B.px)
        cim = C.imagen(1)
        im.paste(cim.resize((160, 160), Image.NEAREST), (x, 60))
        d.text((x, 228), f"medio alto {a} m", font=F_P, fill=tuple(int(v) for v in rgb("bone")))
        d.text((x, 244), f"mapa {int(t * 100)} %", font=F_P, fill=tuple(int(v) for v in rgb("concrete_light")))
        x += 180
    reglas = [
        "Umbrales (medio alto de la cámara ortográfica): SOLDADO 6–40 m · PUEBLO 40–250 · COMARCA 250–900 · MAPA 900–4000.",
        "Fundidos: cada cambio se funde a lo largo de ±25 % del umbral con tramado Bayer 4×4 (alfa binaria, nada semitransparente). Mapa↔comarca es el único cambio de dibujo entero (plano táctico ↔ suelo de día); el resto son capas que entran o salen.",
        "Lo que decide un icono no es el nivel sino el tamaño en pantalla: una unidad pasa a icono cuando su modelo mide menos de 10 px de alto, y el icono sale tramándose en 0,3 s. Los iconos son de 32 px en MAPA y COMARCA, de 16 px en PUEBLO, y no hay en SOLDADO.",
        "Zombis: individuos con cuerpo hasta PUEBLO; desde COMARCA, mancha roja tramada (982420). El cambio es por densidad visible, sin pasar por negro.",
        "Textos de la red (D-N208, Hugo y Lucía): nunca «por hora». Línea: «4 cajas por viaje · un viaje cada 6,7 min». Almacén: en cajas. Raciones: por día de juego.",
        "Calima de día (D-N197 adaptada): a27e5a al 0 % en SOLDADO, 10 % en PUEBLO y 22 % en COMARCA; en MAPA no hay. De noche, la bruma oscura deja de ser franja y pasa a ser la oscuridad fuera del alcance de focos y unidades.",
        "Rotación: solo en SOLDADO y PUEBLO (Q/E); al subir a COMARCA la cámara vuelve al norte arriba en 0,4 s, para que el mapa y el trazado no salgan girados.",
    ]
    y = 280
    for r in reglas:
        for trozo in envuelve(r, F_N, 1650, d):
            d.text((20, y), trozo, font=F_N, fill=tuple(int(v) for v in rgb("bone")))
            y += 20
        y += 6
    # paleta del zum por uso
    usos = [
        ("Mapa: terreno", ["olive_shadow", "grey_brown", "olive_grey", "olive_dark", "concrete", "navy", "concrete_light"]),
        ("Día: suelo", ["tan", "beige", "ochre_dark", "brown", "olive", "olive_light", "olive_shadow", "concrete", "concrete_light", "slate"]),
        ("Día: casas", ["rose", "tan", "brown_dark", "grey_brown", "concrete_light"]),
        ("Vía", ["olive_grey", "concrete", "grey_light"]),
        ("Iconos y placas", ["bone", "olive_shadow", "0f120c"]),
        ("Líneas (fuera de la paleta de 32)", LINEAS),
        ("Trazado", ["bone", "amber"]),
        ("Horda", ["red_bright", "olive_black", "grey_brown"]),
        ("Calima", ["a27e5a"]),
    ]
    y += 10
    pal = {}
    for nombre, cols in usos:
        d.text((20, y + 6), nombre, font=F_N, fill=tuple(int(v) for v in rgb("cream")))
        x = 260
        for c in cols:
            v = tuple(int(t) for t in rgb(c))
            d.rectangle([x, y, x + 54, y + 30], fill=v, outline=tuple(int(t) for t in rgb("black")))
            hx = "%02x%02x%02x" % v
            d.text((x, y + 33), hx, font=F_P, fill=tuple(int(t) for t in rgb("concrete_light")))
            x += 70
        pal[nombre] = ["%02x%02x%02x" % tuple(int(t) for t in rgb(c)) for c in cols]
        y += 56
    # prueba de los colores de línea sobre los dos fondos (Lucía, 11-11): suelo de día y plano del mapa
    for k, (fondo, cx, cy, mpp) in enumerate((("dia", 1040, 690, 1.0), ("mapa", 1080, 1000, 3.9))):
        Lc = Lienzo(300, 150, cx, cy, mpp)
        (terreno_dia if fondo == "dia" else terreno_mapa)(*((Lc, 1) if fondo == "dia" else (Lc,)))
        if fondo == "dia":
            calima(Lc, 0.22)
        for i, c in enumerate(LINEAS):
            y0 = Lc.y0 + (25 + i * 32) * mpp
            Lc.linea([(Lc.x0 + 20 * mpp, y0), (Lc.x0 + 200 * mpp, y0)], CONTORNO, 3 * mpp, guiones_m=8 * mpp)
            Lc.linea([(Lc.x0 + 20 * mpp, y0), (Lc.x0 + 200 * mpp, y0)], c, 1.5 * mpp, guiones_m=8 * mpp)
        ci = Lc.imagen(2).convert("RGBA")
        for i, c in enumerate(LINEAS):
            pega(ci, icono("tren", 32, relleno=c), 470, 50 + i * 64)
        xx = 1020 + k * 0
        im.paste(ci.convert("RGB"), (1060, 520 + k * 310) if False else (1080, 540 + k * 300))
        d.text((1080, 522 + k * 300), "Líneas sobre el suelo de día (calima 22 %)" if fondo == "dia" else "Líneas sobre el plano del mapa", font=F_P, fill=tuple(int(v) for v in rgb("cream")))
    im.save(out / "zum_0_regla.png")
    (out / "zum_paleta.json").write_text(json.dumps({
        "niveles": [{"nombre": n, "medio_alto_m": [a, b]} for n, a, b in NIVELES],
        "fundido_fraccion": FUNDIDO,
        "calima_dia": {"color": "a27e5a", "soldado": 0.0, "pueblo": 0.10, "comarca": 0.22, "mapa": None},
        "icono_px": {"mapa": 32, "comarca": 32, "pueblo": 16, "soldado": 0},
        "icono_umbral_px_modelo": 10,
        "lineas": ["%02x%02x%02x" % tuple(int(t) for t in rgb(c)) for c in LINEAS],
        "trazado": {"valido": "d4d0cc", "invalido": "e08c30", "banda_tramado": 0.55, "tendiendo_tramado": 0.30},
        "paleta_por_uso": pal,
    }, indent=1, ensure_ascii=False), encoding="utf-8")


if __name__ == "__main__":
    out = Path(sys.argv[1] if len(sys.argv) > 1 else "salida_zum")
    out.mkdir(parents=True, exist_ok=True)
    lamina_regla(out)
    lamina_mapa(out)
    lamina_comarca(out)
    lamina_pueblo(out)
    lamina_soldado(out)
    lamina_trazado(out)
    print("hecho:", sorted(p.name for p in out.iterdir()))
