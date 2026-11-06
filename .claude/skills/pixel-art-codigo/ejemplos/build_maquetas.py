"""D-N78 · Maquetas de pantalla completa a 1080p, cartel de oleada e indicador de entrada. Bruno (Arte técnico).

Encargo de Mak (6-nov, 00:12): HUD en partida, menú de inicio y pantallas de victoria y derrota con los
textos del §8 de plan-noche-7nov.md, para que Eugenio las vea como muestra y Marcos tenga la composición
exacta. Además, dos piezas nuevas:
  - ui_cartel_oleada (9-slice, slice 6): sale arriba al centro al empezar cada oleada, con la hora, el aviso
    y la entrada. Marco en el rojo de asedio #982420 (el rojo es del ataque y del asedio: aquí toca).
  - ui_indicador_entrada (24x24, 2 f): cuña en el borde de la pantalla que apunta a una entrada activa (O1–O4)
    que está fuera de cuadro, con su placa de 2 letras. Mira a la derecha; Marcos la gira hacia la entrada.
  - ui_barra_recarga: relleno de la barra de munición mientras recarga (tramado hueso), según Hugo.
Base de 640x360 a x3 = 1920x1080 (la escala de la interfaz a 1080p). El menú va a 480x270 x4.
La letra de las maquetas es provisional (DejaVu Sans a 9–11 px sin suavizado): la de verdad es la de UI Toolkit.
"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import _h  # noqa: E402
import build_interfaz as ui  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_maquetas")
W, H, E = 640, 360, 3
FUENTE = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def _f(t):
    return ImageFont.truetype(FUENTE, t)


def texto(im, xy, s, t=9, c="bone", centro=False, sombra=True):
    d = ImageDraw.Draw(im)
    d.fontmode = "1"
    f = _f(t)
    x, y = xy
    if centro:
        x -= d.textlength(s, font=f) / 2
    if sombra:
        d.text((x + 1, y + 1), s, font=f, fill=PAL["black"])
    d.text((x, y), s, font=f, fill=PAL[c])


def pega(im, s, x, y):
    im.alpha_composite(s.img if isinstance(s, Sprite) else s, (int(x), int(y)))


# --- Piezas nuevas ---------------------------------------------------------------------------
def cartel_oleada(w=300, h=58):
    s = ui.panel(w, h)
    for y in (0, h - 1):
        s.hline(0, w - 1, y, "red_bright")
    for x in (0, w - 1):
        s.vline(x, 0, h - 1, "red_bright")
    for x0 in (3, w - 9):                               # galones de asedio en los extremos
        for k in range(4):
            for t in range(6):
                s.set(x0 + t, 9 + k * 10 + abs(t - 3) - 1, "red_bright")
    return s


def indicador_entrada(f):
    """Cuña que apunta a la derecha con su placa (32x24); f 1 es el latido (avanza 1 px y oscurece)."""
    s = Sprite(32, 24)
    c = "red_bright" if f == 0 else "red"
    for y in range(3, 21):
        a = 9 - abs(y - 11.5)
        for x in range(19 + f, int(19 + f + a)):
            s.set(x, y, c)
    for y in range(3, 12):
        s.set(int(19 + f + 9 - abs(y - 11.5)) - 1, y, "bone")
    s.rect(1, 6, 17, 12, "navy")                        # placa del nombre (O1–O4, lo escribe UI Toolkit)
    for x0, y0, w, h in ((1, 6, 17, 1), (1, 17, 17, 1), (1, 6, 1, 12), (17, 6, 1, 12)):
        s.rect(x0, y0, w, h, c)
    s.outline()
    return s


def tecla_raton(boton="dcho"):
    """Ratón de 9x12 con el botón derecho (o izquierdo) marcado: sustituye al texto «clic dcho.» (Lucía)."""
    s = Sprite(9, 12)
    s.rect(0, 0, 9, 12, "beige")
    s.hline(1, 7, 0, "bone")
    s.vline(4, 0, 4, "grey_brown")                      # separación de botones
    s.hline(0, 8, 5, "grey_brown")
    x0 = 5 if boton == "dcho" else 0
    s.rect(x0, 0, 4, 5, "bone")
    s.rect(x0, 1, 4, 3, "cream")
    for x, y in ((0, 0), (8, 0), (0, 11), (8, 11)):
        s.set(x, y, None)
    s.outline()
    return s


def estrella(llena):
    """Estrella de 16x16 de la pantalla de resultados: llena en #b4a080, vacía en #48403c (nunca ámbar)."""
    s = Sprite(16, 16)
    pts = []
    for k in range(10):
        r = 7.4 if k % 2 == 0 else 3.1
        a = -math.pi / 2 + k * math.pi / 5
        pts.append((7.5 + r * math.cos(a), 8.2 + r * math.sin(a)))
    s.poly([(round(x), round(y)) for x, y in pts], "beige" if llena else "grey_brown")
    if llena:
        s.line(7, 2, 5, 7, "bone")                      # luz arriba a la izquierda
        s.set(6, 5, "bone")
    s.outline()
    return s


def barra_recarga(w=32, h=3):
    s = Sprite(w, h)
    for x in range(w):
        for y in range(h):
            if (x + y) % 3 != 2:
                s.set(x, y, "bone" if (x + y) % 3 == 0 else "grey_light")
    return s


# --- Escena de partida de fondo --------------------------------------------------------------
def escena():
    """Trozo de Sagunto a la escala del juego (0,1 m/px a 1/3): asfalto variado, apisonada, el muro de sacos,
    la tropa propia y la horda que llega por el oeste. Solo para dar contexto a la maqueta."""
    import build_asfalto_variado as av
    import build_suelo_sagunto as su
    import build_vfx_combate as fx
    import build_lamina_siluetas as bl
    im = Image.new("RGBA", (W, H))
    ap = su.apisonada().img
    for x in range(0, W, 64):
        for y in range(0, H, 64):
            im.paste(ap, (x, y))
    calle = av.calle(W=W, H=96, seed=5)
    im.alpha_composite(calle, (0, 150))
    lc = ui.linea_central().img
    for x in range(0, W, 64):
        im.alpha_composite(lc, (x, 197))
    for x in range(0, W):
        if _h(x, 0, 952) > .1:
            im.putpixel((x, 152), PAL["concrete_light"])
            im.putpixel((x, 243), PAL["concrete_light"])
    # muro de sacos (vista cenital, en caqui) en x≈330, de norte a sur
    for y in range(40, 330):
        for x in range(326, 338):
            c = "olive_yellow" if (y // 4 + (x > 331)) % 2 else "beige"
            if x in (326, 337):
                c = "olive_grey"
            im.putpixel((x, y), PAL[c])
    # restos y sangre en el campo de tiro, delante del muro
    rs = [fx.restos_horda(v).img for v in range(6)]
    for k in range(16):
        im.alpha_composite(rs[k % 6].rotate(90 * (k % 4)), (int(160 + _h(k, 0, 961) * 150), int(20 + _h(k, 1, 961) * 300)))
    # horda: siluetas de 12 px con sombra, más densa al oeste
    figs = []
    for k in range(150):
        x = int((_h(k, 2, 962) ** 1.6) * 300)
        y = int(10 + _h(k, 3, 962) * 335)
        figs.append((x, y, 12 if k % 5 else 14, k % 2))
    pinta = []
    for x, y, h, paso in figs:
        m = bl.silueta(h, paso)
        for (dx, dy), c in m.items():
            if 0 <= x + dx < W and 0 <= y + dy - h < H:
                pinta.append((x + dx, y + dy - h, c))
    for x, y, c in pinta:
        im.putpixel((x, y), tuple(c[:3]) + (255,) if isinstance(c, tuple) else PAL[c])
    # tropa propia tras el muro: soldados de A6 (icono) a 1/3 no existen; marcadores de pelotón en olive con rim
    for (cx, cy, n) in ((350, 90, 6), (352, 175, 4), (348, 262, 8)):
        for k in range(n):
            x, y = cx + (k % 2) * 6, cy + (k // 2) * 7
            for yy in range(y - 10, y):
                for xx in range(x, x + 4):
                    im.putpixel((xx, yy), PAL["olive"])
            im.putpixel((x + 1, y - 10), PAL["beige"])     # cara de arriba clara (D-N57)
            for yy in range(y - 9, y):
                im.putpixel((x + 4, yy), PAL["slate"])     # rim propio al este
    # fogonazos y una granada estallando entre la horda
    gr = fx.granada(2).img
    im.alpha_composite(gr, (200, 150))
    for (x, y) in ((356, 168), (356, 176), (352, 82)):
        for k in range(-2, 3):
            im.putpixel((x - 4 + k, y), PAL["cream"])
        im.putpixel((x - 4, y - 1), PAL["amber"])
    return im


# --- Pantallas --------------------------------------------------------------------------------
def hud(im, cartel=True):
    import build_a1 as a1
    import build_a6 as a6
    import build_a9_pelotones as a9
    from pixelvm import reduce_16
    # barra de arriba: reloj y los tres recursos del almacén (munición 45, materiales 40, combustible 30)
    pega(im, ui.panel(64, 22), W // 2 - 32, 4)            # reloj arriba en el centro (D-N65)
    texto(im, (W // 2, 8), "22:47", 11, centro=True)
    for i, (fn, v) in enumerate(((a1.municion, "45"), (a1.materiales, "40"), (a1.combustible, "30"))):
        x = 4 + i * 60
        pega(im, ui.recurso_slot(), x, 4)
        pega(im, reduce_16(fn()), x + 2, 6)
        texto(im, (x + 22, 8), v, 10)
    # alertas bajo los recursos
    import build_a2 as a2
    pega(im, ui.panel(160, 24), 28, 28)
    pega(im, ui.panel(196, 24), 28, 54)
    pega(im, ui.alerta_slot("ataque"), 4, 28)
    pega(im, reduce_16(a2.ataque()), 8, 32)
    texto(im, (32, 34), "Oleada 2 · O1 · la vía", 9, "bone")
    pega(im, ui.alerta_slot("aviso"), 4, 54)
    pega(im, reduce_16(a2.sin_municion()), 8, 58)
    texto(im, (32, 60), "Ametralladores: poca munición", 9, "bone")
    # cartel de oleada
    if cartel:
        c = cartel_oleada()
        cx = W // 2 - c.w // 2
        pega(im, c, cx, 84)
        texto(im, (W // 2, 90), "23:30 · OLEADA 2", 11, "bone", centro=True)
        texto(im, (W // 2, 107), "La Colmena se acerca por el oeste", 9, "beige", centro=True)
        texto(im, (W // 2, 121), "Entrada O1 · la vía", 9, "beige", centro=True)
    # indicadores de entrada en el borde (O1 al oeste fuera de cuadro, O2 al norte)
    i0 = indicador_entrada(0)
    pega(im, i0.img.transpose(Image.FLIP_LEFT_RIGHT), 2, 168)
    texto(im, (19, 173), "O1", 8, "bone", sombra=False)
    i1 = indicador_entrada(1).img.rotate(90, expand=True)
    pega(im, i1, 520, 2)
    texto(im, (522, 20), "O2", 8, "bone", sombra=False)   # (O2 aún no ha entrado: en la maqueta solo enseña la pieza)
    # panel del pelotón seleccionado (abajo a la izquierda)
    pega(im, ui.panel(168, 64), 4, 292)
    s32, _ = a9.iconos()
    pega(im, ui.tarjeta_unidad(True), 10, 300)
    pega(im, s32["ametralladores"], 14, 304)
    pega(im, ui.barra("efectivos"), 14, 338)
    pega(im, barra_recarga(), 14, 342)
    texto(im, (56, 300), "Ametralladores", 9)
    texto(im, (56, 314), "Efectivos 4/4", 9, "beige")
    texto(im, (56, 326), "Munición: recargando", 8, "beige")
    texto(im, (56, 338), "10 s junto al tren", 9, "olive_light")
    # barra de selección: 5 pelotones, Dragón y NH90
    unidades = [a6.fusileros(), s32["ametralladores"], s32["granaderos"], s32["tiradores"], s32["zapadores"],
                a6.blindado_8x8(), s32["nh90"]]
    ef = [32, 32, 26, 32, 22, 32, 32]
    mu = [20, 0, 28, 30, 25, 12, 32]
    x0 = 176
    for i, u in enumerate(unidades):
        x = x0 + i * 42
        pega(im, ui.tarjeta_unidad(i == 1), x, 306)
        pega(im, u, x + 4, 310)
        pega(im, ui.barra("efectivos", ef[i]), x + 4, 344)
        if i == 1:
            pega(im, barra_recarga(), x + 4, 348)
        else:
            b = ui.barra("baja" if mu[i] < 14 else "municion", mu[i], 2) if mu[i] else None
            if b:
                pega(im, b, x + 4, 348)
        texto(im, (x + 31, 309), str(i + 1), 7, "bone")
    # órdenes con su tecla (abajo a la derecha)
    pega(im, ui.panel(130, 76), 506, 280)
    o = ui.ordenes()
    botones = [("mover", None), ("atacar", "A"), ("mantener", "S"), ("emplazar", "D"), ("embarcar", "E")]
    for i, (n, tecla) in enumerate(botones):
        x, y = 512 + (i % 3) * 40, 286 + (i // 3) * 34
        pega(im, ui.boton("encima" if i == 0 else "normal", 36, 31), x, y)
        pega(im, o[n][1], x + 10, y + 3)
        if tecla:
            texto(im, (x + 18, y + 19), tecla, 8, "beige", centro=True, sombra=False)
        else:
            pega(im, tecla_raton(), x + 14, y + 18)
    # selección en el mundo y orden de mover
    pega(im, ui.seleccion(), 344, 164)
    pega(im, ui.orden_mover(2), 300, 200)
    pega(im, ui.cursor("atacar"), 150, 236)
    return im


RESULTADOS = {   # cifras de ejemplo con los formatos de D-N77 (Hugo, clave «resultados» del JSON de la noche)
    True: [("Zombis abatidos", "11.842 de 13.090"), ("Bajas propias", "5 de 26 soldados"),
           ("Munición gastada", "34,0 u · 348 zombis por u"), ("Munición restante", "11,2 u"),
           ("Combustible gastado", "17 de 30 u"), ("Fin de la noche", "Amanecer, 06:00")],
    False: [("Zombis abatidos", "6.120 de 7.400"), ("Bajas propias", "26 de 26 soldados · Dragón perdido"),
            ("Munición gastada", "41,5 u · 295 zombis por u"), ("Munición restante", "3,4 u"),
            ("Combustible gastado", "24 de 30 u"), ("Fin de la noche", "Cayó a las 03:47, en la oleada 5")],
}


def pantalla_final(victoria, estrellas=2):
    im = escena()
    hud(im, cartel=False)
    px = im.load()
    for y in range(H):                                  # velo con tramado (nunca alfa parcial)
        for x in range(W):
            if BAYER[y % 4][x % 4] < 11:
                px[x, y] = PAL["navy"]
    pw, ph = 340, 230
    p = ui.panel(pw, ph)
    if not victoria:                                    # el marco rojo de la derrota se queda (Lucía)
        for y in (0, ph - 1):
            p.hline(0, pw - 1, y, "red_bright")
        for x in (0, pw - 1):
            p.vline(x, 0, ph - 1, "red_bright")
    x0, y0 = W // 2 - pw // 2, 64
    pega(im, p, x0, y0)
    texto(im, (W // 2, y0 + 10), "Sagunto resiste" if victoria else "Sagunto ha caído", 13, "cream" if victoria else "bone", centro=True)
    if victoria:                                        # de 1 a 3 estrellas; con derrota no hay
        for k in range(3):
            pega(im, estrella(k < estrellas), W // 2 - 30 + k * 20, y0 + 32)
    import build_a1 as a1
    import build_recursos_hud as rh                     # aquí dentro: rh importa estrella de este módulo
    from pixelvm import reduce_16
    iconos = [rh.zombis_16(), reduce_16(rh.bajas()), reduce_16(a1.municion()), reduce_16(a1.municion()),
              reduce_16(a1.combustible()), rh.amanecer_16() if victoria else rh.caida_16()]
    ty = y0 + (56 if victoria else 40)
    for i, (k, v) in enumerate(RESULTADOS[victoria]):
        y = ty + i * 18
        if i % 2 == 0:
            for xx in range(x0 + 8, x0 + pw - 8):
                for yy in range(y - 2, y + 13):
                    px[xx, yy] = PAL["slate_dark"]
        pega(im, iconos[i], x0 + 12, y - 2)              # icono de la fila (build_recursos_hud)
        texto(im, (x0 + 32, y), k, 9, "beige", sombra=False)
        d = ImageDraw.Draw(im)
        d.fontmode = "1"
        ancho = d.textlength(v, font=_f(9))
        texto(im, (x0 + pw - 14 - ancho, y), v, 9, "bone", sombra=False)
    for i, t in enumerate(("Repetir la noche", "Menú principal")):
        x = W // 2 - 150 + i * 160
        pega(im, ui.boton("encima" if i == 0 else "normal", 140, 20), x, y0 + ph - 30)
        texto(im, (x + 70, y0 + ph - 26), t, 9, "bone", centro=True)
    return im


def menu():
    m = ui.menu_fondo()
    lg = ui.logo()
    pega(m, lg, 20, 20)
    texto(m, (22, 52), "La primera noche en Sagunto", 9, "cream")
    for i, (t, e) in enumerate((("Jugar", "encima"), ("Opciones", "normal"), ("Salir", "normal"))):
        pega(m, ui.boton(e, 96, 18), 20, 112 + i * 24)
        texto(m, (68, 115 + i * 24), t, 9, "bone", centro=True)
    return m


def entrada():
    """Rótulo de entrada de 3 s sobre la escena (§8)."""
    im = escena()
    hud(im, cartel=False)
    p = ui.panel(360, 30)
    pega(im, p, W // 2 - 180, 150)
    texto(im, (W // 2, 158), "22:00. El tren se detiene en Sagunto. Aguanta hasta el amanecer", 9, "bone", centro=True)
    return im


def exportar(out):
    out = Path(out)
    cartel_oleada().save(out / "ui_cartel_oleada.png")
    hoja = Image.new("RGBA", (64, 24), (0, 0, 0, 0))
    for f in range(2):
        hoja.alpha_composite(indicador_entrada(f).img, (32 * f, 0))
    hoja.save(out / "ui_indicador_entrada.png")
    barra_recarga().save(out / "ui_barra_recarga.png")
    estrella(True).save(out / "ui_estrella_llena.png")
    estrella(False).save(out / "ui_estrella_vacia.png")
    tecla_raton("dcho").save(out / "ui_tecla_raton_dcho.png")
    tecla_raton("izq").save(out / "ui_tecla_raton_izq.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    exportar(OUT)
    a = hud(escena())
    a.resize((W * E, H * E), Image.NEAREST).save(OUT / "maqueta_1080_hud.png")
    entrada().resize((W * E, H * E), Image.NEAREST).save(OUT / "maqueta_1080_entrada.png")
    pantalla_final(True).resize((W * E, H * E), Image.NEAREST).save(OUT / "maqueta_1080_victoria.png")
    pantalla_final(False).resize((W * E, H * E), Image.NEAREST).save(OUT / "maqueta_1080_derrota.png")
    menu().resize((1920, 1080), Image.NEAREST).save(OUT / "maqueta_1080_menu.png")
    print("ok")
