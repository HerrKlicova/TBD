"""pixelvm: pixel art por código para Vía Muerta (TBD).

Herramienta propia del estudio, escrita a partir de las técnicas de pxl (crsolver/claude-pxl),
Pixel Art Studio (Gamezxz/pixel-art-studio), pixelfx (patch-gg/pixelfx) y Pixelbolt
(skulitom/Pixelbolt), todos MIT. No copia su código: reimplementa las ideas útiles
(rejillas de texto, espejo, contorno automático, rampas, bloqueo de paleta, hoja de contacto,
reducción 32 -> 16) sobre la paleta aprobada de 32 colores y las reglas de D-N46.

Solo necesita Pillow. Determinista: el mismo script da siempre los mismos píxeles.

Uso típico (un script de construcción por familia de iconos):

    from pixelvm import Sprite, PAL, contact_sheet, check
    s = Sprite(32, 32)
    s.rect(4, 10, 24, 16, PAL["olive"])
    s.outline()                 # contorno exterior de 1 px
    s.save("out/icono.png")
    print(check(s, recurso=True))  # paleta estricta, sin rojo, márgenes, legibilidad

Autoría: Bruno (Arte técnico), 6-nov-2026.
"""
from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw

AQUI = Path(__file__).resolve().parent

# --- Paleta aprobada (docs/arte/paleta_propuesta_32.hex) con nombres de trabajo ----------
_HEX = {
    "black": "000000", "maroon": "340000", "olive_black": "1c200c", "navy": "181c30",
    "blood_dark": "4c1008", "brown_dark": "442c0c", "olive_shadow": "303424", "slate_dark": "283440",
    "wine": "642030", "red": "7c181c", "brown": "543820", "grey_brown": "48403c",
    "slate": "404460", "red_bright": "982420", "rust": "7c3c18", "rose_brown": "6c4038",
    "olive_dark": "50541c", "purple_grey": "60546c", "olive_grey": "605c48", "rose": "8c5048",
    "ochre_dark": "845c28", "olive": "70784c", "concrete": "6c7874", "olive_yellow": "847c34",
    "tan": "a86c50", "olive_light": "88885c", "concrete_light": "94948c", "amber": "e08c30",
    "beige": "b4a080", "grey_light": "b8b8b8", "bone": "d4d0cc", "cream": "f0dc9c",
}


def _rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


PAL = {k: _rgb(v) + (255,) for k, v in _HEX.items()}
PALETA = [c[:3] for c in PAL.values()]
# Colores prohibidos en iconos de recurso (D-N46: sin sangre, acento sin rojo).
ROJOS = {PAL[k][:3] for k in ("maroon", "blood_dark", "wine", "red", "red_bright")}
FONDO_UI = PAL["navy"]
CONTORNO = PAL["black"]

# Rampas oscuro -> claro por material (para shade/light y selout).
RAMPAS = {
    "oliva": ["olive_black", "olive_shadow", "olive_dark", "olive", "olive_light"],
    "acero": ["slate_dark", "slate", "concrete", "concrete_light", "grey_light"],
    "laton": ["brown_dark", "ochre_dark", "amber", "cream"],
    "madera": ["brown_dark", "brown", "ochre_dark", "tan"],
    "ocre": ["brown", "ochre_dark", "tan", "beige", "cream"],
    "hueso": ["grey_brown", "olive_grey", "beige", "bone"],
    "oxido": ["brown_dark", "brown", "rust", "tan"],
    "hormigon": ["olive_shadow", "grey_brown", "concrete", "concrete_light", "bone"],
}

T = (0, 0, 0, 0)


def col(c):
    """Acepta nombre de PAL, '#hex', tupla o None (transparente)."""
    if c is None:
        return T
    if isinstance(c, str):
        if c in PAL:
            return PAL[c]
        return _rgb(c) + (255,)
    return tuple(c) if len(c) == 4 else tuple(c) + (255,)


class Sprite:
    def __init__(self, w=32, h=32):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), T)
        self.px = self.img.load()

    # --- primitivas (todas a píxel entero, sin suavizado) --------------------------------
    def inside(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h

    def get(self, x, y):
        return self.px[x, y] if self.inside(x, y) else T

    def set(self, x, y, c, only=None):
        if not self.inside(x, y):
            return
        if only == "opaque" and self.px[x, y][3] == 0:
            return
        if only == "empty" and self.px[x, y][3] != 0:
            return
        if isinstance(only, (tuple, str)) and only not in ("opaque", "empty"):
            if self.px[x, y] != col(only):
                return
        self.px[x, y] = col(c)

    def rect(self, x, y, w, h, c, only=None):
        for j in range(y, y + h):
            for i in range(x, x + w):
                self.set(i, j, c, only)

    def hline(self, x0, x1, y, c, only=None):
        for i in range(min(x0, x1), max(x0, x1) + 1):
            self.set(i, y, c, only)

    def vline(self, x, y0, y1, c, only=None):
        for j in range(min(y0, y1), max(y0, y1) + 1):
            self.set(x, j, c, only)

    def line(self, x0, y0, x1, y1, c, only=None):
        """Bresenham: líneas pixel-perfect."""
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.set(x0, y0, c, only)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def ellipse(self, x0, y0, x1, y1, c, only=None):
        """Elipse rellena inscrita en la caja (x0,y0)-(x1,y1), inclusiva."""
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        rx, ry = (x1 - x0 + 1) / 2, (y1 - y0 + 1) / 2
        for j in range(y0, y1 + 1):
            for i in range(x0, x1 + 1):
                if ((i + .5 - cx - .5) / rx) ** 2 + ((j + .5 - cy - .5) / ry) ** 2 <= 1.0:
                    self.set(i, j, c, only)

    def poly(self, pts, c, only=None):
        m = Image.new("L", (self.w, self.h), 0)
        ImageDraw.Draw(m).polygon(pts, fill=255, outline=255)
        mp = m.load()
        for j in range(self.h):
            for i in range(self.w):
                if mp[i, j]:
                    self.set(i, j, c, only)

    def grid(self, x, y, text, keys, only=None):
        """Rejilla de texto: un carácter por píxel; '.' no pinta, '_' borra.
        keys: {'a': 'olive', ...}. Las líneas en blanco iniciales se ignoran."""
        filas = [f for f in text.strip("\n").split("\n")]
        ind = min(len(f) - len(f.lstrip()) for f in filas if f.strip())
        for j, f in enumerate(filas):
            for i, ch in enumerate(f[ind:]):
                if ch == ".":
                    continue
                self.set(x + i, y + j, None if ch == "_" else keys[ch], only)

    # --- operaciones de pixel art --------------------------------------------------------
    def mirror_x(self, from_left=True):
        """Copia la mitad izquierda sobre la derecha (simetría)."""
        for j in range(self.h):
            for i in range(self.w // 2):
                a, b = i, self.w - 1 - i
                if from_left:
                    self.px[b, j] = self.px[a, j]
                else:
                    self.px[a, j] = self.px[b, j]

    def outline(self, c=CONTORNO, diagonal=False):
        """Contorno exterior de 1 px alrededor de todo lo opaco."""
        src = self.img.copy().load()
        vec = [(1, 0), (-1, 0), (0, 1), (0, -1)]
        if diagonal:
            vec += [(1, 1), (1, -1), (-1, 1), (-1, -1)]
        for j in range(self.h):
            for i in range(self.w):
                if src[i, j][3]:
                    continue
                for dx, dy in vec:
                    x, y = i + dx, j + dy
                    if 0 <= x < self.w and 0 <= y < self.h and src[x, y][3]:
                        self.px[i, j] = col(c)
                        break

    def selout(self, rampa, side="br"):
        """Contorno interior selectivo: oscurece un paso los píxeles del borde
        en el lado de sombra (br = abajo-derecha), como hace el pixel art a mano."""
        nombres = RAMPAS[rampa]
        tonos = [col(n) for n in nombres]
        src = self.img.copy().load()
        dirs = {"br": [(1, 0), (0, 1)], "bl": [(-1, 0), (0, 1)], "b": [(0, 1)]}[side]
        for j in range(self.h):
            for i in range(self.w):
                p = src[i, j]
                if not p[3] or p not in tonos:
                    continue
                for dx, dy in dirs:
                    x, y = i + dx, j + dy
                    if not (0 <= x < self.w and 0 <= y < self.h) or src[x, y][3] == 0 or src[x, y] == CONTORNO:
                        k = tonos.index(p)
                        self.px[i, j] = tonos[max(0, k - 1)]
                        break

    def replace(self, old, new):
        o, n = col(old), col(new)
        for j in range(self.h):
            for i in range(self.w):
                if self.px[i, j] == o:
                    self.px[i, j] = n

    def lock_palette(self, paleta=None):
        """Lleva cada píxel opaco al color más cercano de la paleta; alfa binaria."""
        paleta = paleta or PALETA
        for j in range(self.h):
            for i in range(self.w):
                r, g, b, a = self.px[i, j]
                if a < 128:
                    self.px[i, j] = T
                    continue
                m = min(paleta, key=lambda p: (p[0] - r) ** 2 + (p[1] - g) ** 2 + (p[2] - b) ** 2)
                self.px[i, j] = m + (255,)

    def stamp(self, other, x, y):
        for j in range(other.h):
            for i in range(other.w):
                p = other.px[i, j]
                if p[3]:
                    self.set(x + i, y + j, p)

    def copy(self):
        s = Sprite(self.w, self.h)
        s.img = self.img.copy()
        s.px = s.img.load()
        return s

    # --- exportación ---------------------------------------------------------------------
    def save(self, path, scale=1):
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        im = self.img if scale == 1 else self.img.resize((self.w * scale, self.h * scale), Image.NEAREST)
        im.save(path)
        return path

    def silhouette(self):
        s = self.copy()
        for j in range(s.h):
            for i in range(s.w):
                if s.px[i, j][3]:
                    s.px[i, j] = PAL["bone"]
        return s


def reduce_16(s32: Sprite, outline=True) -> Sprite:
    """Reduce un icono de 32 a 16 px (D-N46): quita el contorno, toma el color
    más frecuente de cada bloque 2x2 (con prioridad a lo opaco) y vuelve a contornear."""
    base = s32.copy()
    base.replace(CONTORNO, None)
    s = Sprite(16, 16)
    for j in range(16):
        for i in range(16):
            blk = [base.px[2 * i + a, 2 * j + b] for a in (0, 1) for b in (0, 1)]
            opacos = [p for p in blk if p[3]]
            if len(opacos) >= 2:
                s.px[i, j] = max(set(opacos), key=lambda p: (opacos.count(p), -sum(p[:3])))
    if outline:
        s.outline()
    return s


# --- control de calidad ------------------------------------------------------------------
def _lum(c):
    r, g, b = (v / 255 for v in c[:3])
    f = lambda v: v / 12.92 if v <= 0.03928 else ((v + 0.055) / 1.055) ** 2.4
    return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b)


def check(s: Sprite, recurso=False, margen=0):
    """Devuelve una lista de avisos (vacía = pasa). recurso=True prohíbe los rojos."""
    avisos, usados, opacos = [], {}, 0
    xs, ys = [], []
    for j in range(s.h):
        for i in range(s.w):
            p = s.px[i, j]
            if p[3] == 0:
                continue
            if p[3] != 255:
                avisos.append(f"alfa parcial en ({i},{j})")
            opacos += 1
            xs.append(i)
            ys.append(j)
            usados[p[:3]] = usados.get(p[:3], 0) + 1
            if p[:3] not in PALETA:
                avisos.append(f"color fuera de paleta {p[:3]} en ({i},{j})")
            if recurso and p[:3] in ROJOS:
                avisos.append(f"rojo prohibido {p[:3]} en ({i},{j})")
    if not opacos:
        return ["vacío"]
    if min(xs) < margen or min(ys) < margen or max(xs) > s.w - 1 - margen or max(ys) > s.h - 1 - margen:
        avisos.append("toca el borde del lienzo")
    # Legibilidad sobre el fondo de la interfaz: el tono más claro debe contrastar.
    claro = max(usados, key=_lum)
    ratio = (_lum(claro) + 0.05) / (_lum(FONDO_UI) + 0.05)
    if ratio < 4.5:
        avisos.append(f"contraste bajo sobre #181c30 ({ratio:.1f}:1)")
    cobertura = opacos / (s.w * s.h)
    if cobertura < 0.25:
        avisos.append(f"silueta pequeña ({cobertura:.0%} del lienzo)")
    if len(usados) > 12:
        avisos.append(f"demasiados colores ({len(usados)})")
    return sorted(set(avisos))


def contact_sheet(filas, path, scale=4, gap=4, bg=FONDO_UI, labels=None):
    """filas: lista de listas de Sprite. Hoja a xscale con filtro de punto sobre #181c30,
    más una columna a x1 para juzgar el tamaño real."""
    cw = max(sum(s.w * scale + gap for s in fila) + gap + sum(s.w + 2 for s in fila) + gap * 2 for fila in filas)
    rh = [max(s.h for s in fila) * scale + gap for fila in filas]
    lab = 70 if labels else 0
    im = Image.new("RGBA", (cw + lab, sum(rh) + gap), bg)
    d = ImageDraw.Draw(im)
    y = gap
    for n, fila in enumerate(filas):
        x = gap + lab
        if labels:
            d.text((4, y + 4), labels[n], fill=(180, 180, 180, 255))
        for s in fila:
            im.alpha_composite(s.img.resize((s.w * scale, s.h * scale), Image.NEAREST), (x, y))
            x += s.w * scale + gap
        x += gap
        for s in fila:  # tamaño real
            im.alpha_composite(s.img, (x, y))
            x += s.w + 2
        y += rh[n]
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    im.save(path)
    return path
