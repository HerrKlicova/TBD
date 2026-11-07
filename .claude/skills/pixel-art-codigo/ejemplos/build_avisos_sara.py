"""Avisos que pidió la prueba de humo de Sara (7-nov 00:36). Bruno (Arte técnico). MUESTRA de 5 variantes.

Encargo de Mak:
  1. «Sin munición · T para recargar» sobre la tarjeta del pelotón y en el HUD, con icono y parpadeo (ámbar de aviso:
     es pasajero, se apaga al recargar).
  2. Aviso de muro o estación bajo ataque (rojo de ataque #982420, lenguaje de A2).
  3. Iconos del panel de vehículos: munición, combustible del NH90, embarcar y desembarcar.
Cada grupo sale en 5 variantes (A–E) en una hoja; la que se apruebe se exporta con sus nombres finales.
"""
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite, reduce_16  # noqa: E402

VARIANTES = "ABCDE"


# --- Piezas comunes --------------------------------------------------------------------------
def tecla_t(peq=True):
    """Tecla T: 9x9 (pequeña, para la tarjeta) o la de 14x14 de «Cómo se juega»."""
    if not peq:
        import build_como_jugar as cj
        cj.LETRAS.setdefault("t", ["###.", ".#..", ".#..", ".#..", ".#.."])
        return cj.tecla("t")
    s = Sprite(9, 9)
    s.rect(0, 0, 9, 8, "grey_brown")
    s.rect(1, 0, 7, 7, "beige")
    s.hline(1, 7, 0, "bone")
    s.hline(2, 6, 2, "brown_dark"); s.vline(4, 2, 5, "brown_dark")
    for x, y in ((0, 0), (8, 0), (0, 7), (8, 7)):
        s.set(x, y, None)
    s.outline()
    return s


def sin_municion_16():
    import build_a2 as a2
    return reduce_16(a2.sin_municion())


def tarjeta_base():
    """Tarjeta de pelotón (ametralladores) con munición a cero."""
    import build_interfaz as ui
    import build_a9_pelotones as a9
    s = ui.tarjeta_unidad(False)
    ico = a9.ametralladores()
    s.stamp(ico, 4, 4)
    s.stamp(ui.barra("efectivos", 32, 3), 4, 38)
    return s


def _rayas(s, x0, y0, w, h, f):
    for y in range(y0, y0 + h):
        for x in range(x0, x0 + w):
            s.set(x, y, "amber" if ((x + y + f * 2) // 2) % 2 == 0 else "black")


def tarjeta(v, f):
    s = tarjeta_base()
    on = f == 0
    if v == "A":                                     # banda ámbar sobre el pie del retrato con la tecla
        s.rect(4, 27, 32, 9, "amber" if on else "ochre_dark")
        s.hline(4, 35, 27, "cream" if on else "amber")
        s.stamp(_mini_caja(), 6, 28)
        s.stamp(tecla_t(), 26, 27)
    elif v == "B":                                   # triángulo de aviso en la esquina y tecla abajo
        if on:
            c = Sprite(40, 48)
            c.poly([(28, 1), (38, 1), (38, 11)], "amber")
            c.vline(35, 3, 5, "black"); c.set(35, 7, "black")
            s.stamp(c, 0, 0)
        s.stamp(tecla_t(), 27, 34)
    elif v == "C":                                   # marco ámbar que late y el icono sobre un velo
        c = "amber" if on else "ochre_dark"
        for y in (0, 47):
            s.hline(0, 39, y, c)
        for x in (0, 39):
            s.vline(x, 0, 47, c)
        for y in range(4, 36):
            for x in range(4, 36):
                if (x + y) % 2 == 0:
                    s.set(x, y, "navy")
        s.stamp(sin_municion_16(), 12, 9)
        s.stamp(tecla_t(), 15, 26)
    elif v == "D":                                   # franja de peligro arriba (avanza) y tecla
        _rayas(s, 4, 4, 32, 4, f)
        s.stamp(tecla_t(), 4, 9)
    else:                                            # E: barra de munición que parpadea y tecla en la esquina
        s.rect(4, 42, 32, 2, "amber" if on else "olive_shadow")
        s.stamp(sin_municion_16(), 22, 21)
        s.stamp(tecla_t(), 30, 38)
    return s


def _mini_caja():
    """Caja de munición vacía de 8x7, para la banda de la variante A."""
    s = Sprite(8, 7)
    s.rect(0, 2, 8, 5, "black"); s.rect(1, 3, 6, 3, "grey_brown")
    s.hline(1, 6, 2, "black"); s.rect(2, 0, 4, 2, "black")
    return s


def hud_fila(v, f):
    """Fila del HUD (240x24): marco de alerta, icono y texto «Sin munición · T para recargar»."""
    import build_interfaz as ui
    import build_maquetas as m
    im = Image.new("RGBA", (240, 26), (0, 0, 0, 0))
    on = f == 0
    slot = ui.alerta_slot("aviso")
    if v in ("C",) and not on:
        slot.replace(PAL["amber"], PAL["ochre_dark"])
    im.alpha_composite(slot.img, (0, 1))
    im.alpha_composite(sin_municion_16().img, (4, 5))
    p = ui.panel(210, 24)
    if v == "D":
        _rayas(p, 2, 2, 4, 20, f)
    im.alpha_composite(p.img, (26, 1))
    tx = 34 if v != "D" else 40
    if v == "A":
        m.texto(im, (tx, 7), "Sin munición ·", 9, "bone", sombra=False)
        im.alpha_composite(tecla_t().img, (tx + 82, 8))
        m.texto(im, (tx + 94, 7), "para recargar", 9, "bone", sombra=False)
    elif v == "B":
        im.alpha_composite(tecla_t(False).img, (tx, 5))
        m.texto(im, (tx + 18, 7), "Sin munición: recarga", 9, "bone", sombra=False)
    elif v == "E":
        m.texto(im, (tx, 7), "Sin munición", 9, "cream" if on else "bone", sombra=False)
        im.alpha_composite(tecla_t(False).img, (200, 5))
    else:
        m.texto(im, (tx, 7), "Sin munición · T para recargar", 9, "cream" if (on and v == "C") else "bone", sombra=False)
    return im


# --- Muro o estación bajo ataque ------------------------------------------------------------
def muro32():
    s = Sprite()
    for fila, (y, x0, n) in enumerate(((22, 3, 4), (16, 6, 3), (10, 9, 2))):
        for k in range(n):
            x = x0 + k * 7
            s.rect(x, y, 7, 6, "beige")
            s.hline(x + 1, x + 5, y, "bone")
            s.hline(x, x + 6, y + 5, "ochre_dark")
            s.vline(x + 6, y + 1, y + 5, "ochre_dark")
            s.set(x + 3, y + 2, "ochre_dark")
    s.hline(1, 30, 28, "grey_brown")
    s.outline()
    return s


def estacion32():
    s = Sprite()
    s.rect(4, 13, 24, 14, "beige")
    s.hline(4, 27, 13, "bone")
    s.poly([(2, 13), (16, 5), (29, 13)], "rust")
    s.line(2, 13, 16, 5, "tan")
    for x in (7, 13, 21):
        s.rect(x, 16, 3, 4, "grey_brown")
    s.rect(15, 20, 4, 7, "brown")
    s.hline(1, 30, 27, "ochre_dark"); s.hline(0, 31, 28, "grey_brown")
    s.outline()
    return s


def _estallido(s, cx, cy, r=6):
    c = Sprite(s.w, s.h)
    pts = []
    for k in range(16):
        rr = r if k % 2 == 0 else r * .45
        a = k * math.pi / 8
        pts.append((round(cx + rr * math.cos(a)), round(cy + rr * math.sin(a))))
    c.poly(pts, "red_bright")
    c.rect(cx - 1, cy - 1, 3, 3, "cream")
    c.outline()
    s.stamp(c, 0, 0)


def ataque(tipo, v):
    import build_avisos_minimapa as am
    s = muro32() if tipo == "muro" else estacion32()
    if v == "A":
        am._badge(s, 19, 0)
    elif v == "B":
        _estallido(s, 23, 8)
    elif v == "C":                                   # grietas en el dibujo y la cinta roja de ataque debajo
        for (x, y) in ((8, 14), (20, 18), (14, 22)):
            for k in range(5):
                s.set(x + (k % 2), y + k, "grey_brown")
                s.set(x + 1 + (k % 2), y + k, "black" if k % 2 else "grey_brown")
        s.rect(0, 29, 32, 3, "red_bright"); s.hline(0, 31, 29, "red")
    elif v == "D":                                   # el dibujo dentro de un marco rojo de ataque
        f = Sprite(32, 32)
        f.stamp(s, 0, 0)
        for k in (0, 1):
            f.hline(0, 31, k, "red_bright"); f.hline(0, 31, 31 - k, "red_bright")
            f.vline(k, 0, 31, "red_bright"); f.vline(31 - k, 0, 31, "red_bright")
        s = f
    else:                                            # E: flechas rojas que entran por los dos lados
        c = Sprite(32, 32)
        for (x0, d) in ((0, 1), (31, -1)):
            yc = 18
            for k in range(4):                       # astil
                c.set(x0 + d * k, yc, "red_bright"); c.set(x0 + d * k, yc + 1, "red_bright")
            for k in range(4):                       # punta
                c.vline(x0 + d * (4 + k), yc - 3 + k, yc + 4 - k, "red_bright")
        c.outline()
        s.stamp(c, 0, 0)
    return s


# --- Panel de vehículos ---------------------------------------------------------------------
def picto(nombre, c, sombra):
    s = Sprite(12, 12)
    if nombre == "municion":                         # tres proyectiles de 30 mm
        for x in (1, 5, 9):
            s.rect(x, 3, 2, 8, c); s.set(x, 2, c); s.set(x + 1, 2, c)
            s.hline(x, x + 1, 1, sombra if sombra else c)
            s.hline(x, x + 1, 8, sombra if sombra else c)
    elif nombre == "combustible":                    # bidón con el rotor encima (es el del NH90)
        s.hline(0, 11, 0, c)
        s.vline(6, 0, 2, c)
        s.rect(2, 3, 8, 9, c)
        if sombra:
            s.line(3, 5, 8, 10, sombra); s.line(8, 5, 3, 10, sombra)
        s.rect(7, 2, 2, 1, c)
    elif nombre in ("embarcar", "desembarcar"):      # puerta y flecha hacia dentro o hacia fuera
        s.rect(7, 1, 5, 11, c)
        if sombra:
            s.rect(8, 2, 3, 9, sombra)
        dentro = nombre == "embarcar"
        y = 6
        s.hline(0, 6, y, c); s.hline(0, 6, y - 1, c)
        tip = 7 if dentro else 0
        d = -1 if dentro else 1
        for k in range(1, 4):
            s.set(tip + d * k, y - 1 - k, c); s.set(tip + d * k, y + k, c)
    return s


ICONOS_VEH = ["municion", "combustible", "embarcar", "desembarcar"]


def icono_veh(nombre, v):
    s = Sprite(16, 16)
    if v == "A":                                     # botón oliva como ui_boton_ir, dibujo en hueso
        s.rect(0, 0, 16, 16, "olive_dark")
        s.hline(0, 15, 0, "olive_light"); s.vline(0, 0, 15, "olive_light")
        s.hline(0, 15, 15, "olive_shadow"); s.vline(15, 0, 15, "olive_shadow")
        s.stamp(picto(nombre, "bone", None), 2, 2)
    elif v == "B":                                   # placa beige con dibujo oscuro (como los frentes)
        s.rect(0, 0, 16, 16, "beige")
        s.hline(0, 15, 0, "bone"); s.vline(0, 0, 15, "bone")
        s.hline(0, 15, 15, "ochre_dark"); s.vline(15, 0, 15, "ochre_dark")
        s.stamp(picto(nombre, "grey_brown", None), 2, 2)
    elif v == "C":                                   # sin placa, contorno negro y medidor debajo
        p = picto(nombre, "bone", "beige")
        s.stamp(p, 2, 0)
        if nombre in ("municion", "combustible"):
            s.hline(2, 13, 14, "grey_brown")
            s.hline(2, 9, 14, "ochre_dark" if nombre == "municion" else "olive_yellow")
        s.outline()
    elif v == "D":                                   # chapa redonda navy con aro hueso
        for y in range(16):
            for x in range(16):
                d = math.hypot(x + .5 - 8, y + .5 - 8)
                if d <= 8:
                    s.set(x, y, "bone" if d > 6.9 else "navy")
        s.stamp(picto(nombre, "beige", None), 2, 2)
    else:                                            # E: panel navy con el color de su recurso
        s.rect(0, 0, 16, 16, "navy")
        s.hline(0, 15, 0, "grey_brown"); s.hline(0, 15, 15, "grey_brown")
        s.vline(0, 0, 15, "grey_brown"); s.vline(15, 0, 15, "grey_brown")
        c = {"municion": "ochre_dark", "combustible": "olive_yellow"}.get(nombre, "bone")
        s.stamp(picto(nombre, c, "brown" if nombre != "embarcar" and nombre != "desembarcar" else "grey_brown"), 2, 2)
    return s


# --- Hoja de muestra ------------------------------------------------------------------------
def muestra(out):
    import build_maquetas as m
    E = 4
    filas = []
    for v in VARIANTES:
        fila = [tarjeta(v, 0), tarjeta(v, 1)]
        filas.append((v, fila, [hud_fila(v, 0), hud_fila(v, 1)], [ataque("muro", v), ataque("estacion", v)],
                      [icono_veh(n, v) for n in ICONOS_VEH]))
    W = 1880
    H = 40 + len(filas) * (48 * E + 30)
    hoja = Image.new("RGBA", (W, H), PAL["olive_grey"])
    m.texto(hoja, (8, 8), "Sin munición en la tarjeta (2 f) · fila del HUD (2 f) · muro y estación bajo ataque · "
                          "panel de vehículos (munición, combustible NH90, embarcar, desembarcar)", 13, "cream")
    y = 40
    for v, tarj, hud, atq, veh in filas:
        m.texto(hoja, (8, y + 4), v, 20, "cream")
        x = 40
        for s in tarj:
            hoja.alpha_composite(s.img.resize((s.w * E, s.h * E), Image.NEAREST), (x, y)); x += s.w * E + 10
        x += 10
        for im in hud:
            hoja.alpha_composite(im.resize((im.width * 2, im.height * 2), Image.NEAREST), (x, y + (0 if im is hud[0] else 60)))
        x += 240 * 2 + 20
        for s in atq:
            hoja.alpha_composite(s.img.resize((s.w * E, s.h * E), Image.NEAREST), (x, y)); x += s.w * E + 10
        x += 10
        for s in veh:
            hoja.alpha_composite(s.img.resize((s.w * E, s.h * E), Image.NEAREST), (x, y + 30)); x += s.w * E + 10
        # a tamaño real (x1) debajo, para ver si se lee
        y += 48 * E + 30
    hoja.save(out / "muestra_avisos_sara_5_variantes.png")


if __name__ == "__main__":
    OUT = Path(next((a for a in sys.argv[1:] if not a.startswith("--")), "out_avisos_sara"))
    OUT.mkdir(parents=True, exist_ok=True)
    muestra(OUT)
    print("ok")
