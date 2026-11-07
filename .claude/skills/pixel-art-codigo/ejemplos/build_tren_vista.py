"""Vista cercana del tren: modo de cámara, vida por vagón y aviso de tren atacado. Bruno (Arte técnico), 7-nov 00:42.

Encargo de Mak. MUESTRA de 5 variantes (A–E) de la pantalla entera. Cámara de Marcos: lateral (yaw 90°), ortho 7,
320x180 píxeles de juego y unos 13 px/m, siguiendo al tren hacia +X; en pantalla cabe más o menos un vagón, así que la
vida de cada vagón va en un esquema del tren entero. La interfaz va encima a 640x360 (x3 a 1080p), como el HUD.
Composición decidida por Mak (07-11 00:44): 3 piezas, unos 64 m: locomotora R1, tropa (vagon_v2, R2) y plataforma con contenedor (v3).
Colores: vida en hueso → beige → ocre (sin ámbar fijo); el ataque, en rojo #982420; nada en 283440.
"""
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import _h  # noqa: E402

TREN = [("loco", "Locomotora", 1.0), ("tropa", "Vagón 1", .85), ("contenedor", "Vagón 2", .38)]
ATACADO = 2                                          # índice del vagón atacado en la maqueta
VISIBLE = 1                                          # la cámara enseña el vagón 1 y el morro de la locomotora


# --- Escena de juego (320x180) ---------------------------------------------------------------
def escena():
    import build_parallax_tren as bp
    todo = {("noche", n): fn("noche") for n, (fn, _) in bp.CAPAS.items()}
    im = bp.composicion(todo, "noche", 900)
    s = Sprite(320, 180)
    s.img = im
    s.px = im.load()
    s.rect(0, 151, 320, 29, "olive_shadow")          # suelo (de noche)
    s.rect(0, 150, 320, 5, "grey_brown")             # balasto
    for x in range(0, 320, 5):
        s.rect(x, 152, 3, 2, "olive_grey")           # traviesas
    s.hline(0, 319, 151, "concrete"); s.hline(0, 319, 150, "concrete_light")   # carril
    # vagón de tropa (R2): plataforma de 20 m, de x = -30 a 230
    s.rect(-30, 132, 260, 10, "olive_dark")
    s.hline(-30, 229, 132, "olive"); s.hline(-30, 229, 141, "olive_shadow")
    for x in range(-20, 230, 26):
        s.vline(x, 124, 132, "olive_dark")           # teleros
    s.hline(-30, 229, 124, "olive_dark")
    for cx in (10, 28, 172, 190):                    # bogies
        s.rect(cx - 9, 141, 18, 4, "grey_brown")
        s.ellipse(cx - 4, 142, cx + 4, 150, "olive_shadow"); s.set(cx, 146, "concrete")
    for k, x in enumerate((40, 62, 85, 120, 148)):   # soldados en la plataforma
        s.rect(x, 121, 4, 3, "olive"); s.rect(x - 1, 124, 6, 8, "olive")
        s.set(x + 1, 122, "tan"); s.hline(x + 4, x + 9, 126, "grey_brown")
    s.rect(230, 136, 10, 3, "grey_brown")            # enganche
    # morro de la locomotora diésel, a la derecha
    s.rect(240, 104, 90, 38, "olive_dark")
    s.hline(240, 319, 104, "olive")
    s.rect(244, 108, 18, 14, "olive_grey"); s.rect(246, 110, 14, 10, "concrete")   # ventanilla de cabina
    s.rect(270, 110, 50, 4, "olive_shadow")
    for cx in (262, 300):
        s.rect(cx - 12, 141, 24, 4, "grey_brown")
        s.ellipse(cx - 4, 142, cx + 4, 150, "olive_shadow")
    # horda que corre junto a la vía, a la izquierda (siluetas oscuras, los valores de la horda)
    for k in range(26):
        x = int(_h(k, 0, 51) * 120) - 10
        y = 150 + int(_h(k, 1, 51) * 22)
        s.rect(x, y - 7, 2, 2, "olive_black"); s.rect(x - 1, y - 5, 4, 5, "olive_black")
        s.set(x + 3, y - 5, "olive_black")
    return s.img


# --- Piezas de interfaz ----------------------------------------------------------------------
def _color_vida(v):
    return "bone" if v >= .5 else "beige" if v >= .25 else "ochre_dark"


def silueta(tipo, w=24, h=8):
    """Silueta pequeña del coche para el esquema (24x8)."""
    s = Sprite(w, h)
    if tipo == "loco":
        s.rect(0, 1, w - 1, h - 3, "grey_brown"); s.rect(1, 0, 6, 2, "grey_brown")
    elif tipo == "tropa":
        s.rect(0, h - 5, w, 3, "grey_brown")
        for x in range(1, w, 4):
            s.vline(x, h - 7, h - 5, "grey_brown")
    elif tipo == "contenedor":
        s.rect(0, h - 4, w, 2, "grey_brown"); s.rect(3, 0, w - 6, h - 4, "grey_brown")
    else:
        s.rect(0, h - 4, w, 2, "grey_brown"); s.ellipse(2, 0, w - 3, h - 3, "grey_brown")
    for x in (3, w - 4):
        s.set(x, h - 1, "grey_brown"); s.set(x - 1, h - 1, "grey_brown")
    return s


def esquema(estilo, f=0, ancho=28):
    """Tren entero en miniatura: un hueco por coche con su vida. estilo: barra, relleno, pips, cifra."""
    import build_maquetas as m
    n = len(TREN)
    W = n * (ancho + 3) + 2
    H = 20 if estilo != "relleno" else 14
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for i, (tipo, nombre, v) in enumerate(reversed(TREN)):          # la locomotora a la derecha (va hacia +X)
        idx = n - 1 - i
        x = 2 + i * (ancho + 3)
        caja = Sprite(ancho, H)
        caja.rect(0, 0, ancho, H, "navy")
        borde = "red_bright" if (idx == ATACADO and f == 0) else ("red" if idx == ATACADO else "grey_brown")
        for xx in range(ancho):
            caja.set(xx, 0, borde); caja.set(xx, H - 1, borde)
        for yy in range(H):
            caja.set(0, yy, borde); caja.set(ancho - 1, yy, borde)
        if idx == VISIBLE:                           # el coche que enseña la cámara: esquinas en hueso
            for (cx, cy) in ((1, 1), (ancho - 2, 1), (1, H - 2), (ancho - 2, H - 2)):
                caja.set(cx, cy, "bone")
        sil = silueta(tipo, ancho - 6, 8)
        if estilo == "relleno":                      # la silueta se llena de abajo arriba según la vida
            corte = int(8 * (1 - v))
            for yy in range(8):
                for xx in range(sil.w):
                    if sil.get(xx, yy)[3]:
                        sil.set(xx, yy, _color_vida(v) if yy >= corte else "grey_brown")
            caja.stamp(sil, 3, 3)
        else:
            caja.stamp(sil, 3, 2)
            if estilo == "barra":
                caja.hline(3, ancho - 4, 14, "grey_brown"); caja.hline(3, ancho - 4, 15, "grey_brown")
                for xx in range(3, 3 + int((ancho - 6) * v)):
                    caja.set(xx, 14, _color_vida(v)); caja.set(xx, 15, _color_vida(v))
            elif estilo == "pips":
                for k in range(5):
                    c = _color_vida(v) if v * 5 > k + .25 else "grey_brown"
                    caja.rect(3 + k * 5, 13, 4, 3, c)
        im.alpha_composite(caja.img, (x, 0))
        if estilo == "cifra":
            m.texto(im, (x + 4, 10), f"{int(v * 100)}", 8, _color_vida(v), sombra=False)
    return im


def triangulo_rojo():
    import build_avisos_minimapa as am
    s = Sprite(13, 12)
    am._badge(s, 0, 0)
    return s


def flecha_ataque():
    """Flecha roja que apunta hacia abajo, sobre el vagón atacado en el mundo (9x10)."""
    s = Sprite(9, 10)
    s.rect(3, 0, 3, 5, "red_bright")
    s.poly([(0, 5), (8, 5), (4, 9)], "red_bright")
    s.outline()
    return s


# --- Pantallas de la muestra -----------------------------------------------------------------
def pantalla(v, f=0):
    import build_maquetas as m
    import build_interfaz as ui
    import build_apertura as ap
    base = escena().resize((640, 360), Image.NEAREST)
    im = base
    W, H = 640, 360
    # reloj y barra de la noche (siempre)
    m.pega(im, ui.panel(64, 22), W // 2 - 32, 4)
    m.texto(im, (W // 2, 8), "01:12", 11, centro=True)
    m.pega(im, ap.barra_fondo(), W // 2 - 32, 28)
    im.alpha_composite(ap.barra_relleno().img.crop((0, 0, 26, 4)), (W // 2 - 32, 28))
    if v == "A":                                     # cine: franjas arriba y abajo, esquema en la franja de abajo
        for y in list(range(0, 0)) + list(range(H - 34, H)):
            for x in range(W):
                im.putpixel((x, y), PAL["navy"])
        for x in range(W):
            im.putpixel((x, H - 34), PAL["grey_brown"])
        e = esquema("barra", f)
        im.alpha_composite(e, (W // 2 - e.width // 2, H - 27))
        m.texto(im, (10, H - 26), "Vista del tren", 9, "cream", sombra=False)
        m.texto(im, (10, H - 14), "V: volver al mapa", 9, "beige", sombra=False)
        x_at = W // 2 - e.width // 2 + 2 + (len(TREN) - 1 - ATACADO) * 31 + 8
        m.pega(im, triangulo_rojo(), x_at, H - 40)
        m.pega(im, flecha_ataque(), 40, 210)
    elif v == "B":                                   # visor: esquinas de cámara y barras de vida sobre el vagón
        for (x, y, dx, dy) in ((8, 8, 1, 1), (W - 9, 8, -1, 1), (8, H - 9, 1, -1), (W - 9, H - 9, -1, -1)):
            for k in range(14):
                for g in (0, 1):
                    im.putpixel((x + dx * k, y + dy * g), PAL["bone"]); im.putpixel((x + dx * g, y + dy * k), PAL["bone"])
        m.texto(im, (24, 12), "Vista del tren · V", 9, "bone")
        e = esquema("pips", f)
        im.alpha_composite(e, (W - e.width - 24, 14))
        bx, by = 60, 228                             # barra sobre el vagón 1 en el mundo
        bar = ui.barra("efectivos", 120, 3)
        im.alpha_composite(Image.new("RGBA", (124, 7), PAL["navy"]), (bx - 2, by - 2))
        im.alpha_composite(bar.img.crop((0, 0, int(120 * .85), 3)), (bx, by))
        if f == 0:                                   # el ataque llega por la izquierda: borde rojo tramado
            for y in range(40, H - 40):
                for x in range(0, 6):
                    if (x + y) % 2 == 0 or x < 2:
                        im.putpixel((x, y), PAL["red_bright"])
        m.pega(im, triangulo_rojo(), 10, H // 2 - 6)
    elif v == "C":                                   # marco de panel alrededor y aviso en la columna de avisos
        p = ui.panel(W, H)
        marco = p.img.copy()
        dentro = Image.new("RGBA", (W - 8, H - 8), (0, 0, 0, 0))
        marco.paste(dentro, (4, 4))
        im.alpha_composite(marco, (0, 0))
        tab = ui.panel(120, 18)
        m.pega(im, tab, 12, 0)
        m.texto(im, (20, 3), "Vista del tren", 9, "cream", sombra=False)
        e = esquema("pips", f)
        im.alpha_composite(e, (W // 2 - e.width // 2, 40))
        slot = ui.alerta_slot("ataque")
        import build_avisos_minimapa as am
        from pixelvm import reduce_16
        m.pega(im, slot, 12, 60)
        m.pega(im, reduce_16(am.tren_atacado()), 16, 64)
        m.pega(im, ui.panel(150, 24), 38, 60)
        m.texto(im, (46, 66), "Tren atacado · vagón 2", 9, "bone", sombra=False)
    elif v == "D":                                   # esquema grande abajo con siluetas que se llenan
        e = esquema("relleno", f, 40)
        pl = ui.panel(e.width + 16, 34)
        m.pega(im, pl, W // 2 - pl.w // 2, H - 40)
        im.alpha_composite(e, (W // 2 - e.width // 2, H - 31))
        m.texto(im, (W // 2 - pl.w // 2 + 8, H - 52), "Vista del tren · V: mapa", 9, "bone")
        x_at = W // 2 - e.width // 2 + 2 + (len(TREN) - 1 - ATACADO) * 43 + 14
        if f == 0:
            m.pega(im, triangulo_rojo(), x_at, H - 47)
    else:                                            # E: mínimo: placas con cifra sobre cada vagón y corchetes rojos
        m.texto(im, (10, 10), "Vista del tren · V", 9, "bone")
        e = esquema("cifra", f, 24)
        im.alpha_composite(e, (W - e.width - 10, 10))
        # el vagón atacado (2) queda fuera de cuadro a la izquierda: corchete rojo en el borde y su placa
        c = PAL["red_bright"] if f == 0 else PAL["red"]
        for y in range(226, 304):
            for g in (2, 3):
                im.putpixel((g, y), c)
        for k in range(10):
            for g in (0, 1):
                im.putpixel((2 + k, 226 + g), c); im.putpixel((2 + k, 302 + g), c)
        m.pega(im, ui.panel(72, 14), 10, 206)
        m.texto(im, (15, 207), "Vagón 2 · 38 %", 9, "cream", sombra=False)
        m.pega(im, ui.panel(72, 14), 196, 206)
        m.texto(im, (201, 207), "Vagón 1 · 85 %", 9, "bone", sombra=False)
    return im


VARIANTES = "ABCDE"
NOTAS = {"A": "Cine: franjas navy arriba y abajo; esquema con barras en la franja; flecha roja sobre el atacado",
         "B": "Visor: esquinas de cámara; vida en pips arriba a la derecha y barra sobre el vagón; borde rojo por donde atacan",
         "C": "Marco de panel alrededor; pips arriba; aviso «Tren atacado · vagón 2» en la columna de avisos",
         "D": "Esquema grande abajo: las siluetas se llenan según la vida; triángulo rojo sobre el atacado",
         "E": "Mínimo: cifras en el esquema; placa de vida sobre el vagón; corchete rojo en el borde por donde atacan, con su placa"}


def muestra(out):
    import build_maquetas as m
    hoja = Image.new("RGBA", (2 * 640 + 12, 5 * 384), PAL["navy"])
    for i, v in enumerate(VARIANTES):
        y = i * 384
        m.texto(hoja, (6, y + 4), f"{v} · {NOTAS[v]}", 9, "cream")
        hoja.alpha_composite(pantalla(v, 0), (0, y + 22))
        hoja.alpha_composite(pantalla(v, 1), (652, y + 22))
        pantalla(v, 0).resize((1920, 1080), Image.NEAREST).save(out / f"muestra_tren_{v}_1080.png")
    hoja.save(out / "muestra_tren_vista_5_variantes.png")


if __name__ == "__main__":
    OUT = Path(next((a for a in sys.argv[1:] if not a.startswith("--")), "out_tren_vista"))
    OUT.mkdir(parents=True, exist_ok=True)
    muestra(OUT)
    print("ok")
