"""Interfaz v2 (D-N147, docs/interfaz-v2.md de Lucía). Bruno (Arte técnico), 7-nov.

Genera:
  1. Las piezas en píxel (a 1x, para UI Toolkit):
     - siluetas por hombre de 10x20, de tres tipos (fusilero, ametrallador y tirador), viva en hueso d4d0cc y caída en 353a55;
     - siluetas laterales de vehículo (Dragón, VAMTAC y NH90), en hueso;
     - iconos de órdenes de 16 px: línea, cuña, columna, bloque, fuego libre, alto el fuego, aterrizar y descargar.
  2. Cinco maquetas a 1920x1080 sobre una captura de la Noche46 (qa34, CaptureRoutine ya corregida). La disposición es la
     misma en las cinco; cambia el estilo de las tarjetas, del minimapa y de las órdenes.

Disposición (§2): reloj y objetivo arriba al centro; avisos arriba a la izquierda; pausa y velocidad arriba a la derecha;
recursos abajo a la izquierda; tarjetas abajo al centro; minimapa abajo a la derecha con las órdenes encima; barra de atajos
sobre el panel. Panel inferior más barra = 156 px (14,4 % de 1080).
La disposición está inspirada en patrones de uso de Ebbtide; no copia ninguno de sus gráficos, iconos ni tipografía.
Texto en Inter (licencia OFL), de 10 a 24 px.

La captura de la Noche46 lleva el HUD viejo. Sus zonas (la franja de arriba y los 244 px de abajo) se rellenan copiando
suelo de la misma captura, unas filas más arriba o más abajo. Eso NO es la build: para la próxima ronda conviene una
captura sin HUD.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_formaciones import icono as icono_formacion  # noqa: E402

HUESO = (0xd4, 0xd0, 0xcc)
FONDO = (0x18, 0x1c, 0x30)
BORDE = (0x35, 0x3a, 0x55)
AMBAR = (0xe0, 0x8c, 0x30)
ROJO = (0x98, 0x24, 0x20)
GRIS = (0x94, 0x94, 0x8c)          # texto secundario (concrete_light)
BEIGE = (0xb4, 0xa0, 0x80)
CAIDO = BORDE
FONT = "/usr/share/fonts/opentype/inter/Inter-%s.otf"
_f = {}


def font(px, peso="Medium"):
    k = (px, peso)
    if k not in _f:
        _f[k] = ImageFont.truetype(FONT % peso, px)
    return _f[k]


# ------------------------------------------------------------------------------------------------ piezas en píxel
SIL = {
    "fusilero": [
        "...xxx....",
        "..xxxxx...",
        "...xxx....",
        "....x....x",
        "..xxxxx.x.",
        ".xxxxxxx..",
        ".x.xxxxx..",
        ".x.xxx.x..",
        "...xxx....",
        "...xxx....",
        "...xxx....",
        "..xxxxx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        ".xxx.xxx..",
        "..........",
    ],
    "ametrallador": [
        "...xxx....",
        "..xxxxx...",
        "...xxx....",
        "....x.....",
        "..xxxxx...",
        ".xxxxxxxxx",
        ".xxxxxxxxx",
        ".x.xxx.x..",
        "...xxx....",
        "...xxx....",
        "...xxx....",
        "..xxxxx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        ".xxx.xxx..",
        "..........",
    ],
    "tirador": [
        "...xxx...x",
        "..xxxxx.x.",
        "...xxx.x..",
        "....x.x...",
        "..xxxxx...",
        ".xxxxxx...",
        ".x.xxxxx..",
        ".x.xxx.x..",
        "...xxx....",
        "...xxx....",
        "...xxx....",
        "..xxxxx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        "..xx.xx...",
        ".xxx.xxx..",
        "..........",
    ],
}


def silueta(tipo, viva=True):
    s = Sprite(10, 20)
    c = HUESO + (255,) if viva else CAIDO + (255,)
    for y, fila in enumerate(SIL[tipo]):
        for x, ch in enumerate(fila):
            if ch == "x":
                s.set(x, y, c)
    return s


def vehiculo(nombre):
    """Silueta lateral en hueso, de perfil mirando a la derecha."""
    c = HUESO + (255,)
    if nombre == "dragon":                       # 8x8 con torre y cañón de 30 mm
        s = Sprite(56, 22)
        s.rect(3, 9, 48, 7, c)
        for k in range(4):
            s.hline(3 + k, 50 - k, 16 + k, c) if k < 2 else None
        for x in range(6):
            s.vline(51 + x, 9 + x, 14, c) if x < 4 else None
        s.rect(20, 5, 16, 4, c)
        s.hline(36, 52, 6, c)
        for wx in (9, 20, 31, 42):
            s.rect(wx - 3, 16, 7, 5, c)
            s.set(wx - 3, 16, (0, 0, 0, 0)); s.set(wx + 3, 16, (0, 0, 0, 0))
            s.set(wx - 3, 20, (0, 0, 0, 0)); s.set(wx + 3, 20, (0, 0, 0, 0))
        return s
    if nombre == "vamtac":                       # 4x4 con ametralladora en el techo
        s = Sprite(48, 22)
        s.rect(2, 10, 44, 6, c)
        s.rect(14, 4, 20, 6, c)
        s.rect(16, 5, 6, 3, (0, 0, 0, 0)); s.rect(24, 5, 7, 3, (0, 0, 0, 0))
        s.vline(26, 1, 3, c); s.hline(26, 36, 1, c)
        for wx in (10, 37):
            s.rect(wx - 4, 15, 9, 6, c)
            for dx, dy in ((-4, 15), (4, 15), (-4, 20), (4, 20)):
                s.set(wx + dx, dy, (0, 0, 0, 0))
        return s
    s = Sprite(64, 22)                           # NH90
    s.hline(4, 60, 2, c)
    s.vline(30, 2, 5, c)
    s.rect(14, 6, 30, 9, c)
    s.rect(44, 8, 8, 5, c)
    s.rect(10, 8, 4, 6, c)
    s.hline(52, 62, 9, c); s.hline(52, 62, 10, c)
    s.vline(62, 4, 10, c); s.vline(61, 5, 9, c)
    s.rect(16, 8, 5, 3, (0, 0, 0, 0)); s.rect(23, 8, 5, 3, (0, 0, 0, 0))
    for wx in (20, 38):
        s.vline(wx, 15, 18, c)
        s.hline(wx - 2, wx + 2, 19, c)
    return s


def icono_form(nombre, t):
    """Formación a 12 o 16 px; la cuña (F2, D-N147) es nueva y no está en build_formaciones."""
    if nombre != "cuna":
        return icono_formacion(nombre, t)
    if t == 16:
        return icono_orden("cuna")
    s = Sprite(12, 12)
    for x, y in ((5, 2), (3, 5), (7, 5), (1, 8), (9, 8)):
        s.rect(x, y, 2, 2, "bone")
    return s


def icono_orden(nombre):
    if nombre in ("linea", "columna", "bloque"):
        return icono_formacion(nombre, 16)
    s = Sprite(16, 16)
    c = "bone"
    if nombre == "cuna":                         # cuña: punta al frente (arriba)
        pts = [(7, 3), (4, 6), (10, 6), (1, 9), (13, 9), (4, 12), (10, 12)]
        for x, y in pts[:5]:
            s.rect(x, y, 2, 2, c)
        s.hline(6, 9, 0, "beige")
        return s
    if nombre in ("fuego_libre", "alto_fuego"):
        for k in range(16):
            for j in range(16):
                d = ((k - 7.5) ** 2 + (j - 7.5) ** 2) ** 0.5
                if 5.2 <= d <= 6.4:
                    s.set(k, j, c)
        s.vline(7, 0, 4, c); s.vline(7, 11, 15, c); s.hline(0, 4, 7, c); s.hline(11, 15, 7, c)
        if nombre == "fuego_libre":
            s.rect(7, 7, 2, 2, c)
        else:
            for k in range(3, 13):
                s.set(k, 15 - k, "beige"); s.set(k + 1, 15 - k, "beige")
        return s
    if nombre == "aterrizar":
        s.hline(2, 13, 14, c); s.hline(2, 13, 15, "beige")
        s.vline(7, 1, 9, c); s.vline(8, 1, 9, c)
        for k in range(4):
            s.hline(4 + k, 11 - k, 7 + k, c)
        return s
    if nombre == "descargar":
        s.rect(1, 2, 7, 12, (0, 0, 0, 0)); s.vline(1, 2, 13, c); s.hline(1, 6, 2, c); s.hline(1, 6, 13, c)
        s.hline(5, 13, 7, c); s.hline(5, 13, 8, c)
        for k in range(3):
            s.vline(12 - k, 5 + k, 10 - k, c)
        return s
    raise KeyError(nombre)


# ------------------------------------------------------------------------------------------------ datos de la maqueta
PELOTONES = [
    # tecla, nombre, tipo, vivos, munición, estado, formación
    (1, "Fusileros", "fusilero", 8, 0.80, "En posición", "linea"),
    (2, "Fusileros", "fusilero", 7, 0.12, "Sin munición", "bloque"),
    (3, "Ametralladora", "ametrallador", 6, 0.55, "Disparando", "linea"),
    (4, "Tiradores", "tirador", 8, 0.90, "Moviendo", "columna"),
    (5, "Fusileros", "fusilero", 5, 0.40, "En posición", "cuna"),
]
VEHICULOS = [
    (6, "Dragón", "dragon", 0.90, 0.70, "Fuego libre"),
    (7, "VAMTAC", "vamtac", 0.60, 0.30, "Alto el fuego"),
    (8, "NH90", "nh90", 1.00, 0.80, "En tierra"),
]
SELECCION = 1
ORDENES = [("linea", "Línea", "F1"), ("cuna", "Cuña", "F2"), ("columna", "Columna", "F3"), ("bloque", "Bloque", "F4")]
AVISOS = [("ataque", "Brecha · muro oeste", ROJO), ("sin_municion", "Pelotón 2 · sin munición", AMBAR),
          ("tren_detenido", "Tren · llega a las 23:10", BORDE)]
RECURSOS = [("municion", "47", "Munición"), ("materiales", "40", "Material"), ("combustible", "30", "Combust."),
            ("poblacion", "80", "Personas")]
ATAJOS = "Clic: seleccionar   ·   Clic derecho: mover   ·   Arrastrar con el derecho: frente   ·   F1–F4: formación   ·   WASD: cámara   ·   Esc: pausa"

W, H = 1920, 1080
BARRA_Y, PANEL_Y = 924, 940            # barra de atajos de 16 px y panel de 140 px: 156 px, el 14,4 %
CX0, CX1 = 316, 1590                   # tarjetas
MX0, MX1 = 1604, 1904                  # minimapa
CW, CH, CG = 152, 128, 8
CY = 946

ESTILOS = {
    "1_plano": dict(nombre="1 · Plano oscuro (v1.4 tal cual)", franja=True, franja_a=217, carta=(0x20, 0x25, 0x38), carta_a=255,
                    borde=True, cabecera=False, esquinas=False, sombra=False, retrato=False, mapa="rect"),
    "2_sueltas": dict(nombre="2 · Piezas sueltas con cabecera", franja=False, carta=FONDO, carta_a=217,
                      borde=True, cabecera=True, esquinas=False, sombra=False, retrato=False, mapa="rect_titulo"),
    "3_esquinas": dict(nombre="3 · Esquinas de visor", franja=True, franja_a=180, carta=FONDO, carta_a=0,
                       borde=False, cabecera=False, esquinas=True, sombra=False, retrato=False, mapa="rejilla"),
    "4_minima": dict(nombre="4 · Mínima, texto sobre el mundo", franja=False, carta=FONDO, carta_a=140,
                     borde=False, cabecera=False, esquinas=False, sombra=True, retrato=False, mapa="redondo"),
    "5_retrato": dict(nombre="5 · Compacta con retrato del jefe", franja=True, franja_a=217, carta=(0x20, 0x25, 0x38), carta_a=255,
                      borde=True, cabecera=False, esquinas=False, sombra=False, retrato=True, mapa="bisel"),
}


# ------------------------------------------------------------------------------------------------ dibujo
def base_limpia(path):
    """Quita el HUD viejo: sube el mundo limpio (filas 104-836 a 1080) hasta arriba y rellena las 192 filas que faltan
    sobre el panel copiando el suelo de justo encima, desplazado a lo largo de la vía (pendiente aprox. -0,65) para que la
    vía y el muro sigan. Queda UNA costura, en la fila 732, que no es de la build."""
    im = Image.open(path).convert("RGB").resize((W, H), Image.NEAREST)
    out = Image.new("RGB", (W, H))
    out.paste(im.crop((0, 104, W, 836)), (0, 0))
    src = out.crop((0, 540, W, 732))
    dx = 297
    fill = Image.new("RGB", (W, 192))
    fill.paste(src.crop((dx, 0, W, 192)), (0, 0))
    fill.paste(src.crop((0, 0, dx, 192)), (W - dx, 0))
    out.paste(fill, (0, 732))
    out.paste(fill.crop((0, 0, W, H - 924)), (0, 924))   # bajo el panel (casi no se ve)
    out = out.convert("RGBA")
    # SOLO PARA LA MAQUETA: degradado oscuro de la fila 690 a la 924 para que la costura no distraiga. No es parte de la propuesta.
    sc = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(sc)
    for y in range(690, 924):
        d.line((0, y, W, y), fill=FONDO + (int(170 * ((y - 690) / 234) ** 0.8),))
    out.alpha_composite(sc)
    return out


class Lienzo:
    def __init__(self, im, st):
        self.im, self.st = im, st
        self.capa = Image.new("RGBA", im.size, (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.capa)

    def caja(self, x0, y0, x1, y1, fondo=FONDO, alfa=217, borde=BORDE, ancho=1):
        if alfa:
            self.d.rectangle((x0, y0, x1 - 1, y1 - 1), fill=fondo + (alfa,))
        if borde:
            for k in range(ancho):
                self.d.rectangle((x0 + k, y0 + k, x1 - 1 - k, y1 - 1 - k), outline=borde + (255,))

    def esquinas(self, x0, y0, x1, y1, col=GRIS, l=7):
        for (x, y, dx, dy) in ((x0, y0, 1, 1), (x1 - 1, y0, -1, 1), (x0, y1 - 1, 1, -1), (x1 - 1, y1 - 1, -1, -1)):
            self.d.line((x, y, x + dx * l, y), fill=col + (255,))
            self.d.line((x, y, x, y + dy * l), fill=col + (255,))

    def texto(self, xy, t, px, col=HUESO, peso="Medium", anchor="la"):
        if self.st.get("sombra"):
            x, y = xy
            for dx, dy in ((1, 1), (-1, 1), (1, -1), (-1, -1), (0, 1), (1, 0)):
                self.d.text((x + dx, y + dy), t, font=font(px, peso), fill=(8, 10, 18, 230), anchor=anchor)
        self.d.text(xy, t, font=font(px, peso), fill=col + (255,), anchor=anchor)

    def sprite(self, spr, x, y, escala=1):
        im = spr.img if hasattr(spr, "img") else spr
        if escala != 1:
            im = im.resize((im.width * escala, im.height * escala), Image.NEAREST)
        self.capa.alpha_composite(im, (x, y))

    def barra(self, x, y, w, h, v, col):
        self.d.rectangle((x, y, x + w - 1, y + h - 1), fill=(0x0c, 0x0e, 0x18, 255))
        if v > 0:
            self.d.rectangle((x, y, x + max(1, int(w * v)) - 1, y + h - 1), fill=col + (255,))

    def fin(self):
        self.im.alpha_composite(self.capa)
        return self.im


def icono_carga(n):
    return Image.open(f"/mnt/project-files/docs/arte/pixel-codigo/vm_cargas_{n}_16.png").convert("RGBA")


def icono_alerta(n):
    return Image.open(f"/mnt/project-files/docs/arte/pixel-codigo/a2/vm_alertas_{n}_16.png").convert("RGBA")


def tarjeta_peloton(L, x, y, p, sel):
    st = L.st
    tecla, nombre, tipo, vivos, mun, estado, form = p
    L.caja(x, y, x + CW, y + CH, st["carta"], st["carta_a"], BORDE if st["borde"] else None)
    if st["esquinas"]:
        L.esquinas(x, y, x + CW, y + CH)
    ox = x + 8
    if st["retrato"]:
        ret = Image.open(f"/mnt/project-files/docs/arte/pixel-codigo/noche-prototipo/tarjetas/ui_tarjeta_"
                         f"{'ametralladora' if tipo == 'ametrallador' else 'tiradores' if tipo == 'tirador' else 'fusileros'}_48.png").convert("RGBA")
        L.sprite(ret.crop((8, 2, 40, 42)), x + 6, y + 30)
        ox = x + 44
    if st["cabecera"]:
        L.d.rectangle((x + 1, y + 1, x + CW - 2, y + 25), fill=(0x28, 0x34, 0x40, 255))
    L.d.rectangle((x + 6, y + 6, x + 23, y + 23), fill=(0x0c, 0x0e, 0x18, 255), outline=BORDE + (255,))
    L.texto((x + 15, y + 15), str(tecla), 13, peso="Bold", anchor="mm")
    L.texto((x + 30, y + 7), nombre, 14, peso="SemiBold")
    L.sprite(icono_form(form, 12), x + CW - 18, y + 9)
    if st["esquinas"]:
        L.d.line((x + 30, y + 26, x + 80, y + 26), fill=(0x70, 0x78, 0x4c, 255))
    # siluetas, una por hombre
    paso = 12 if not st["retrato"] else 13
    if st["retrato"]:
        for k in range(8):
            L.sprite(silueta(tipo, k < vivos), ox + (k % 4) * paso, y + 32 + (k // 4) * 22)
        by = y + 80
    else:
        for k in range(8):
            L.sprite(silueta(tipo, k < vivos), ox + k * 17, y + 32)
        by = y + 60
    bw = x + CW - 8 - ox
    L.texto((ox, by), "Munición", 11, GRIS)
    L.texto((x + CW - 8, by), f"{round(mun * 100)} %", 11, HUESO, "SemiBold", anchor="ra")
    L.barra(ox, by + 16, bw, 5, mun, AMBAR if mun < 0.25 else BEIGE)
    L.texto((ox, y + CH - 24), estado, 13, AMBAR if estado == "Sin munición" else HUESO, "SemiBold")
    if sel:
        L.d.rectangle((x - 1, y - 1, x + CW, y + CH), outline=HUESO + (255,), width=2)


def tarjeta_vehiculo(L, x, y, v):
    st = L.st
    tecla, nombre, clave, blin, mun, estado = v
    L.caja(x, y, x + CW, y + CH, st["carta"], st["carta_a"], BORDE if st["borde"] else None)
    if st["esquinas"]:
        L.esquinas(x, y, x + CW, y + CH)
    if st["cabecera"]:
        L.d.rectangle((x + 1, y + 1, x + CW - 2, y + 25), fill=(0x28, 0x34, 0x40, 255))
    L.d.rectangle((x + 6, y + 6, x + 23, y + 23), fill=(0x0c, 0x0e, 0x18, 255), outline=BORDE + (255,))
    L.texto((x + 15, y + 15), str(tecla), 13, peso="Bold", anchor="mm")
    L.texto((x + 30, y + 7), nombre, 14, peso="SemiBold")
    if st["esquinas"]:
        L.d.line((x + 30, y + 26, x + 80, y + 26), fill=(0x70, 0x78, 0x4c, 255))
    vs = vehiculo(clave)
    L.sprite(vs, x + (CW - vs.img.width) // 2, y + 30)
    for k, (lab, val, col) in enumerate((("Blindaje", blin, GRIS), ("Munición", mun, BEIGE))):
        yy = y + 56 + k * 22
        L.texto((x + 8, yy), lab, 11, GRIS)
        L.texto((x + CW - 8, yy), f"{round(val * 100)} %", 11, HUESO, "SemiBold", anchor="ra")
        L.barra(x + 8, yy + 15, CW - 16, 4, val, AMBAR if (lab == "Munición" and val < 0.25) else col)
    L.texto((x + 8, y + CH - 24), estado, 13, HUESO, "SemiBold")


def minimapa(L, x0, y0, x1, y1):
    st = L.st
    w, h = x1 - x0, y1 - y0
    m = Image.new("RGBA", (w, h), (0x28, 0x34, 0x40, 255))
    d = ImageDraw.Draw(m)
    if st["mapa"] == "rejilla":
        for gx in range(0, w, 25):
            d.line((gx, 0, gx, h), fill=(0x30, 0x3a, 0x4c, 255))
        for gy in range(0, h, 25):
            d.line((0, gy, w, gy), fill=(0x30, 0x3a, 0x4c, 255))
    # vía en diagonal, recinto, edificios, horda, unidades, encuadre
    d.line((0, h - 10, w, 6), fill=(0x48, 0x40, 0x3c, 255), width=3)
    rec = [(70, 22), (205, 22), (205, 104), (70, 104)]
    d.polygon(rec, outline=GRIS + (255,))
    for bx, by, bw, bh in ((150, 30, 22, 12), (180, 52, 14, 18), (90, 80, 18, 10)):
        d.rectangle((bx, by, bx + bw, by + bh), fill=(0x60, 0x5c, 0x48, 255))
    import random
    rnd = random.Random(147)                                    # horda: mancha negra irregular al oeste del muro
    for _ in range(900):
        hx, hy = rnd.gauss(40, 11), rnd.gauss(64, 13)
        if 8 < hx < 68:
            d.rectangle((hx, hy, hx + 1, hy + 1), fill=(0, 0, 0, 255))
    d.line((70, 50, 70, 78), fill=ROJO + (255,), width=3)       # brecha: rojo solo donde ataca
    for ux, uy in ((120, 60), (128, 64), (136, 58), (150, 78), (160, 84), (110, 90), (176, 40), (186, 46)):
        d.rectangle((ux, uy, ux + 2, uy + 2), fill=HUESO + (255,))
    d.rectangle((104, 38, 196, 92), outline=HUESO + (255,))    # encuadre de la cámara
    if st["mapa"] == "redondo":
        mask = Image.new("L", (w, h), 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, w - 1, h - 1), radius=h // 2, fill=255)
        m.putalpha(mask)
        L.capa.alpha_composite(m, (x0, y0))
        L.d.rounded_rectangle((x0, y0, x1 - 1, y1 - 1), radius=h // 2, outline=BORDE + (255,))
        return
    L.capa.alpha_composite(m, (x0, y0))
    if st["mapa"] == "rejilla":
        L.esquinas(x0, y0, x1, y1, HUESO, 9)
    elif st["mapa"] == "bisel":
        L.d.line((x0, y0, x1 - 1, y0), fill=(0x6c, 0x78, 0x74, 255))
        L.d.line((x0, y0, x0, y1 - 1), fill=(0x6c, 0x78, 0x74, 255))
        L.d.line((x0, y1 - 1, x1 - 1, y1 - 1), fill=(0x0c, 0x0e, 0x18, 255))
        L.d.line((x1 - 1, y0, x1 - 1, y1 - 1), fill=(0x0c, 0x0e, 0x18, 255))
    else:
        L.d.rectangle((x0, y0, x1 - 1, y1 - 1), outline=BORDE + (255,))
    if st["mapa"] == "rect_titulo":
        L.caja(x0, y0 - 20, x0 + 90, y0, FONDO, 230, BORDE)
        L.texto((x0 + 8, y0 - 17), "Sagunto · M", 11, GRIS)


def ordenes(L):
    st = L.st
    bw, bh, g = 146, 30, 8
    for i, (ic, txt, tecla) in enumerate(ORDENES):
        x = MX0 + (i % 2) * (bw + g)
        y = 870 + (i // 2) * (bh + 4)
        activa = ic == PELOTONES[SELECCION - 1][6]
        if st["mapa"] == "redondo":
            L.caja(x, y, x + bw, y + bh, FONDO, 150 if not activa else 220, HUESO if activa else None)
        else:
            L.caja(x, y, x + bw, y + bh, (0x35, 0x3a, 0x55) if activa else FONDO, 230, HUESO if activa else BORDE)
        if st["esquinas"] and not activa:
            L.esquinas(x, y, x + bw, y + bh, GRIS, 5)
        L.sprite(icono_orden(ic), x + 8, y + 7)
        L.texto((x + 32, y + 7), txt, 13, HUESO, "SemiBold")
        L.d.rectangle((x + bw - 32, y + 6, x + bw - 7, y + 23), outline=GRIS + (255,))
        L.texto((x + bw - 19, y + 15), tecla, 11, GRIS, "SemiBold", anchor="mm")
    L.texto((MX0, 850), "Órdenes · Pelotón 1", 11, GRIS)


def maqueta(base, clave):
    st = ESTILOS[clave]
    L = Lienzo(base.copy(), st)
    # franja inferior
    if st.get("franja"):
        L.d.rectangle((0, PANEL_Y, W - 1, H - 1), fill=FONDO + (st["franja_a"],))
        L.d.line((0, PANEL_Y, W - 1, PANEL_Y), fill=BORDE + (255,))
    # barra de atajos
    L.d.rectangle((CX0, BARRA_Y, CX1 - 1, PANEL_Y - 1), fill=(0x0c, 0x0e, 0x18, 170))
    L.texto(((CX0 + CX1) // 2, BARRA_Y + 8), ATAJOS, 11, GRIS, "Medium", anchor="mm")
    # reloj y objetivo
    x0, x1, y0, y1 = 810, 1110, 10, 74
    L.caja(x0, y0, x1, y1, FONDO, 217, BORDE if st["borde"] or st["cabecera"] else None)
    if st["esquinas"]:
        L.esquinas(x0, y0, x1, y1)
    L.d.ellipse((x0 + 12, y0 + 12, x0 + 52, y0 + 52), outline=BORDE + (255,), width=4)
    L.d.arc((x0 + 12, y0 + 12, x0 + 52, y0 + 52), -90, -90 + 360 * 0.35, fill=BEIGE + (255,), width=4)
    L.texto((x0 + 66, y0 + 8), "Noche · 22:34", 24, HUESO, "Bold")
    L.texto((x0 + 66, y0 + 40), "Horda: 3.120 restantes", 13, GRIS)
    # pausa y velocidad
    for i, (t, on) in enumerate((("1×", True), ("2×", False))):
        bx = 1720 + i * 44
        L.caja(bx, 16, bx + 40, 46, (0x35, 0x3a, 0x55) if on else FONDO, 217, HUESO if on else BORDE)
        L.texto((bx + 20, 31), t, 13, HUESO, "SemiBold", anchor="mm")
    L.caja(1812, 16, 1904, 46, FONDO, 217, BORDE)
    L.d.rectangle((1822, 24, 1825, 37), fill=HUESO + (255,)); L.d.rectangle((1829, 24, 1832, 37), fill=HUESO + (255,))
    L.texto((1842, 23), "Menú", 13, HUESO, "SemiBold")
    L.texto((1886, 24), "Esc", 10, GRIS, "SemiBold")
    # avisos
    for i, (ic, t, col) in enumerate(AVISOS):
        ay = 16 + i * 44
        L.caja(16, ay, 356, ay + 38, FONDO, 217, col, 1 if col == BORDE else 2)
        L.sprite(icono_alerta(ic), 28, ay + 11)
        L.texto((54, ay + 10), t, 14, HUESO, "SemiBold")
        L.texto((344, ay + 13), "6 s" if i == 0 else "3 s" if i == 1 else "1 s", 10, GRIS, anchor="ra")
    # recursos
    if not st.get("franja"):
        L.caja(12, 952, 300, 1068, FONDO, st["carta_a"], BORDE if st["borde"] else None)
    for i, (ic, n, lab) in enumerate(RECURSOS):
        rx = 24 + i * 70
        L.sprite(icono_carga(ic), rx, 990, 2)
        L.texto((rx + 38, 994), n, 15, HUESO, "Bold")
        L.texto((rx, 1030), lab, 10, GRIS)
    L.texto((24, 960), "Sagunto · almacén", 11, GRIS)
    # tarjetas
    for i, p in enumerate(PELOTONES):
        tarjeta_peloton(L, CX0 + i * (CW + CG), CY, p, p[0] == SELECCION)
    for j, v in enumerate(VEHICULOS):
        tarjeta_vehiculo(L, CX0 + (5 + j) * (CW + CG), CY, v)
    # minimapa y órdenes
    minimapa(L, MX0, CY, MX1, CY + CH)
    ordenes(L)
    # en el mundo: etiquetas cortas y flecha de borde
    for tx, ty, t in ((1290, 334, "1 Fus"), (1150, 496, "6 Dragón")):
        L.texto((tx, ty), t, 9, HUESO, "SemiBold")
    L.d.polygon(((1906, 470), (1892, 462), (1892, 478)), fill=HUESO + (255,))
    L.texto((1886, 462), "4 Tir · 140 m", 11, HUESO, "SemiBold", anchor="ra")
    L.texto((16, 156), st["nombre"], 12, GRIS, "Medium")
    return L.fin()


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_interfaz_v2")
    base_png = sys.argv[2] if len(sys.argv) > 2 else "docs/capturas/qa34/horda_2235_oeste.png"
    (OUT / "piezas").mkdir(parents=True, exist_ok=True)
    for t in SIL:
        silueta(t, True).img.save(OUT / "piezas" / f"ui_sil_{t}_viva_10x20.png")
        silueta(t, False).img.save(OUT / "piezas" / f"ui_sil_{t}_caida_10x20.png")
    for v in ("dragon", "vamtac", "nh90"):
        s = vehiculo(v)
        s.img.save(OUT / "piezas" / f"ui_veh_{v}_{s.img.width}x22.png")
    for o in ("linea", "cuna", "columna", "bloque", "fuego_libre", "alto_fuego", "aterrizar", "descargar"):
        icono_orden(o).img.save(OUT / "piezas" / f"ui_orden_{o}_16.png")
    base = base_limpia(base_png)
    base.convert("RGB").save(OUT / "base_noche46_sin_hud_viejo.png")
    for k in ESTILOS:
        maqueta(base, k).convert("RGB").save(OUT / f"interfaz_v2_{k}.png")
    # contacto de piezas x4
    pz = sorted((OUT / "piezas").glob("*.png"))
    hoja = Image.new("RGBA", (len(pz) * 70 + 10, 110), FONDO + (255,))
    x = 10
    for p in pz:
        im = Image.open(p).convert("RGBA")
        e = 4 if im.width <= 16 else 2 if im.width <= 32 else 1
        im = im.resize((im.width * e, im.height * e), Image.NEAREST)
        hoja.alpha_composite(im, (x, 10)); x += max(70, im.width + 8)
    hoja.crop((0, 0, x, 110)).save(OUT / "piezas_contacto.png")
    print("ok")
