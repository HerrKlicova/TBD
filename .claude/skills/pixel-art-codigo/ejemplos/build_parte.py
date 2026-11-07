"""Pantallas del parte de la noche (NightReport), victoria y derrota. Bruno (Arte técnico), 7-nov 00:32.

Encargo de Mak: el arte de la pantalla del parte con las estrellas de la D-N104, los abatidos, las bajas, la
munición, el estado de la estación y del tren, y el botón «Seguir»; y las pantallas de victoria y de derrota.
Reglas: el parte no es modal (D-N91) y empieza por debajo de la columna de avisos (D-N106, y ≥ 84 en la base de
640x360); la derrota sí para la partida (velo tramado y marco rojo #982420, D-N78). Nada en 283440, estrellas sin
ámbar, ámbar solo como aviso pasajero.
Piezas:
  ui_parte_panel            9-slice 48x64 (slices 4, 38, 4, 4): panel con banda de cabecera de 34 px y filete beige
  ui_parte_panel_derrota    el mismo con el marco y la banda en rojo de ataque
  ui_parte_franja           9-slice 16x14 (slice 3): separador de filas (línea de 1 px abajo)
  ui_parte_estrella_{llena,vacia}_24   estrella grande; ui_parte_estrella_aparece: 4 f de «sello» al ganarla
  ui_parte_barra_fondo      9-slice 32x6 (slice 2): pista de la barra de estado (estación, tren, muro)
  ui_parte_barra_relleno    tira de 1x4 que se repite a lo ancho
  ui_parte_barra_umbral     2x8: muesca de los umbrales de la D-N104 (40 % y 75 %)
  ui_parte_cabecera_amanecer 96x16: sol que sale sobre la vía (victoria)
  ui_parte_cabecera_caida   96x16: la estación con humo (derrota)
Aprobada la variante A (Mak, 7-nov 00:34): cabecera con dibujo, estrellas grandes, cuatro cifras y tres barras de
estado (estación, tren y muro) con los umbrales de la D-N104.
Iconos de fila: los ui_resultado_*_16, ui_recurso_*_16 y ui_frente_{estacion,tren}_16 que ya existen.
"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite, reduce_16  # noqa: E402
from build_d_vfx import _h, tramado  # noqa: E402

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


# --- Piezas ----------------------------------------------------------------------------------
def panel(w=48, h=48, derrota=False, cab=18):
    s = Sprite(w, h)
    marco = "red_bright" if derrota else "grey_brown"
    s.rect(0, 0, w, h, "navy")
    for y in range(1, cab):                          # banda de cabecera: tramado claro sobre navy
        for x in range(1, w - 1):
            if BAYER[y % 4][x % 4] < 3:
                s.set(x, y, "red" if derrota else "grey_brown")
    s.hline(3, w - 4, cab, "beige" if not derrota else "red_bright")
    s.hline(3, w - 4, cab + 2, "grey_brown")
    for x in range(w):
        s.set(x, 0, marco); s.set(x, h - 1, marco)
    for y in range(h):
        s.set(0, y, marco); s.set(w - 1, y, marco)
    s.hline(1, w - 2, 1, "olive_grey" if not derrota else "red")
    s.vline(1, 1, h - 2, "olive_grey" if not derrota else "red")
    for x, y in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3)):
        s.set(x, y, "bone")
    return s


def franja(w=16, h=14):
    """Separador de filas: una línea de 1 px abajo (un fondo tramado detrás de la letra la ensuciaba)."""
    s = Sprite(w, h)
    for x in range(w):
        s.set(x, h - 1, "grey_brown")
    return s


def estrella(llena, escala=1.0, f=None):
    """Estrella de 24x24. f (0–3) es el sello al ganarla: grande, se encoge, rebota y queda."""
    s = Sprite(24, 24)
    k = {None: 1.0, 0: 1.35, 1: 0.8, 2: 1.08, 3: 1.0}[f] * escala
    pts = []
    for i in range(10):
        r = (11 if i % 2 == 0 else 4.6) * k
        a = -math.pi / 2 + i * math.pi / 5
        pts.append((11.5 + r * math.cos(a), 12.6 + r * math.sin(a)))
    pts = [(round(x), round(y)) for x, y in pts]
    if llena:
        s.poly(pts, "beige")
        cx, cy = 11.5, 12.6
        for y in range(24):                          # mitad de abajo a la derecha a la sombra
            for x in range(24):
                if s.get(x, y)[3] and (x - cx) + (y - cy) * .6 > 2.5:
                    s.set(x, y, "ochre_dark")
        s.line(11, 3, 8, 10, "bone"); s.line(12, 4, 9, 10, "cream")
        if f == 0:                                   # destello del sello
            for dx, dy in ((-9, -8), (9, -8), (-10, 5), (10, 5)):
                s.set(12 + dx, 12 + dy, "cream")
    else:
        s.poly(pts, "grey_brown")
        inner = [(round(11.5 + (x - 11.5) * .62), round(12.6 + (y - 12.6) * .62)) for x, y in pts]
        s.poly(inner, "navy")
    s.outline()
    return s


def barra_fondo():
    s = Sprite(32, 6)
    s.rect(0, 0, 32, 6, "navy")
    for x in range(32):
        s.set(x, 0, "grey_brown"); s.set(x, 5, "olive_grey")
    s.vline(0, 0, 5, "grey_brown"); s.vline(31, 0, 5, "olive_grey")
    return s


def barra_relleno():
    s = Sprite(1, 4)
    for y, c in enumerate(("bone", "beige", "beige", "ochre_dark")):
        s.set(0, y, c)
    return s


def barra_umbral():
    s = Sprite(2, 8)
    s.vline(0, 0, 7, "cream"); s.vline(1, 0, 7, "black")
    return s


def cabecera_amanecer():
    s = Sprite(96, 16)
    for y in range(16):
        for x in range(96):
            d = math.hypot(x + .5 - 48, (y + .5 - 15) * 1.2)
            if d < 9 and y < 13:
                s.set(x, y, "cream" if d < 6 else "beige")
            elif y < 13 and 9 <= d < 26 and int(math.degrees(math.atan2(15 - y, x - 48)) / 9) % 2 == 0 \
                    and tramado(x, y, max(0, .55 - (d - 9) / 34)):
                s.set(x, y, "ochre_dark")
    s.hline(0, 95, 13, "ochre_dark")
    s.hline(0, 95, 14, "olive_grey"); s.hline(0, 95, 15, "grey_brown")
    for x in range(2, 96, 6):                        # traviesas de la vía
        s.set(x, 14, "grey_brown")
    return s


def cabecera_caida():
    s = Sprite(96, 16)
    s.rect(34, 8, 28, 6, "grey_brown")               # edificio de viajeros, con el tejado hundido
    s.poly([(32, 8), (42, 3), (48, 6), (52, 4), (64, 8)], "rose_brown")
    for x in (38, 44, 52, 58):
        s.rect(x, 10, 2, 2, "navy")
    s.set(46, 7, None); s.set(47, 7, None); s.set(47, 8, None)
    for (cx, cy, r) in ((50, 3, 3), (56, 1, 2.5), (43, 2, 2)):   # humo, tramado y nunca negro
        for y in range(16):
            for x in range(96):
                if math.hypot(x - cx, y - cy) <= r and tramado(x, y, .6):
                    s.set(x, y, "olive_grey")
    s.hline(0, 95, 14, "olive_grey"); s.hline(0, 95, 15, "grey_brown")
    return s


# --- Datos de la maqueta ---------------------------------------------------------------------
DATOS = {
    True: {"titulo": "Parte de la noche", "sub": "Sagunto · Noche 1 · Amanecer, 06:00", "estrellas": 2,
           "filas": [("zombis", "Zombis abatidos", "11.842 de 13.090"), ("bajas", "Bajas propias", "5 de 26 soldados"),
                     ("municion", "Munición gastada", "34,0 u · 348 por u"),
                     ("municion", "Munición restante", "11,2 u")],
           "estado": [("estacion", "Estación", .74), ("tren", "Tren", .62), ("muro", "Muro", .38)]},
    False: {"titulo": "Sagunto ha caído", "sub": "Noche 1 · Cayó a las 03:47, en la oleada 5", "estrellas": 0,
            "filas": [("zombis", "Zombis abatidos", "6.120 de 7.400"), ("bajas", "Bajas propias", "26 de 26 · Dragón perdido"),
                      ("municion", "Munición gastada", "41,5 u · 295 por u"),
                      ("municion", "Munición restante", "3,4 u")],
            "estado": [("estacion", "Estación", 0.0), ("tren", "Tren", .31), ("muro", "Muro", .04)]},
}


def _icono(k):
    import build_recursos_hud as rh
    import build_a1 as a1
    import build_frentes_aire as fa
    return {"zombis": rh.zombis_16, "bajas": lambda: reduce_16(rh.bajas()), "municion": lambda: reduce_16(a1.municion()),
            "muro": rh.muro_16, "estacion": lambda: fa.frente("estacion"), "tren": lambda: fa.frente("tren"),
            "amanecer": rh.amanecer_16, "caida": rh.caida_16}[k]()


def _nueve(src, w, h, sl):
    """9-slice a mano (solo para las maquetas)."""
    im = src.img if isinstance(src, Sprite) else src
    W0, H0 = im.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    xs = ((0, sl, 0, sl), (sl, W0 - sl, sl, w - sl), (W0 - sl, W0, w - sl, w))
    ys = ((0, sl, 0, sl), (sl, H0 - sl, sl, h - sl), (H0 - sl, H0, h - sl, h))
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            if dx1 > dx0 and dy1 > dy0:
                out.alpha_composite(im.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.NEAREST), (dx0, dy0))
    return out


def _barra(im, x, y, w, v):
    im.alpha_composite(_nueve(barra_fondo(), w, 6, 2), (x, y))
    n = int((w - 2) * v)
    if n > 0:
        im.alpha_composite(barra_relleno().img.resize((n, 4), Image.NEAREST), (x + 1, y + 1))
    for u in (.40, .75):
        im.alpha_composite(barra_umbral().img, (x + 1 + int((w - 2) * u) - 1, y - 1))


def _ancho(t, size=9):
    import build_maquetas as m
    d = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    return d.textlength(t, font=m._f(size))


# --- Variantes -------------------------------------------------------------------------------
def pantalla(variante, victoria=True):
    import build_maquetas as m
    import build_interfaz as ui
    im = m.escena()
    m.hud(im, cartel=False)
    D = DATOS[victoria]
    W = m.W
    if not victoria:                                 # la derrota sí para la partida: velo tramado
        px = im.load()
        for y in range(m.H):
            for x in range(W):
                if BAYER[y % 4][x % 4] < 11:
                    px[x, y] = PAL["navy"]
    pw, ph = (340, 214) if variante != "B" else (380, 214)
    x0, y0 = W // 2 - pw // 2, 84 if victoria else 60
    cab = 34 if variante in ("A", "C", "E") else 18
    im.alpha_composite(panel(pw, ph, not victoria, cab).img, (x0, y0))   # igual que el 9-slice exportado
    tc = "cream" if victoria else "bone"
    if variante in ("A", "C"):                       # cabecera con dibujo, título y estrellas debajo
        im.alpha_composite((cabecera_amanecer() if victoria else cabecera_caida()).img, (W // 2 - 48, y0 + 4))
        m.texto(im, (W // 2, y0 + 20), D["titulo"], 11, tc, centro=True)
    else:
        m.texto(im, (W // 2, y0 + (4 if cab == 18 else 6)), D["titulo"], 11 if cab == 18 else 13, tc, centro=True)
        if cab != 18:
            m.texto(im, (W // 2, y0 + 22), D["sub"], 8, "beige", centro=True, sombra=False)
    y = y0 + cab + 6
    estrellas = victoria
    if variante == "B":                              # dos columnas: estrellas y estado a la izquierda
        if estrellas:
            for k in range(3):
                im.alpha_composite(estrella(k < D["estrellas"]).img, (x0 + 10 + k * 26, y))
        else:
            im.alpha_composite(_icono("caida").img, (x0 + 40, y + 4))
        yy = y + 32
        for key, nombre, v in D["estado"]:
            im.alpha_composite(_icono(key).img, (x0 + 10, yy))
            m.texto(im, (x0 + 30, yy), nombre, 8, "beige", sombra=False)
            m.texto(im, (x0 + 104 - int(_ancho(f"{int(v * 100)} %", 8)), yy), f"{int(v * 100)} %", 8, "bone", sombra=False)
            _barra(im, x0 + 30, yy + 11, 76, v)
            yy += 24
        cx = x0 + 128
        for i, (key, nombre, v) in enumerate(D["filas"]):
            yr = y + i * 26
            im.alpha_composite(_nueve(franja(), pw - 138, 24, 3), (cx, yr - 3))
            im.alpha_composite(_icono(key).img, (cx + 4, yr))
            m.texto(im, (cx + 24, yr - 1), nombre, 8, "beige", sombra=False)
            m.texto(im, (cx + 24, yr + 8), v, 8, "bone", sombra=False)
    else:
        if variante in ("A", "D", "E") and estrellas:
            for k in range(3):
                im.alpha_composite(estrella(k < D["estrellas"]).img, (W // 2 - 38 + k * 26, y - 2))
            y += 24
        elif variante == "C" and estrellas:          # estrellas a la derecha de la cabecera
            for k in range(3):
                im.alpha_composite(estrella(k < D["estrellas"], .7).img, (x0 + pw - 82 + k * 22, y0 + 6))
        filas = D["filas"]
        if variante == "D":                          # estado arriba, en tres barras anchas
            for i, (key, nombre, v) in enumerate(D["estado"]):
                bx = x0 + 12 + i * 106
                im.alpha_composite(_icono(key).img, (bx, y))
                m.texto(im, (bx + 20, y + 1), f"{nombre} {int(v * 100)} %", 8, "beige", sombra=False)
                _barra(im, bx, y + 19, 96, v)
            y += 32
        for i, (key, nombre, v) in enumerate(filas):
            im.alpha_composite(_nueve(franja(), pw - 16, 15, 3), (x0 + 8, y - 2))
            im.alpha_composite(_icono(key).img, (x0 + 12, y - 2))
            m.texto(im, (x0 + 32, y), nombre, 9, "beige", sombra=False)
            m.texto(im, (x0 + pw - 14 - int(_ancho(v)), y), v, 9, "bone", sombra=False)
            y += 16
        if variante != "D":
            y += 4
            if variante == "E":                      # estado como fila de tres cajitas con icono y cifra
                for i, (key, nombre, v) in enumerate(D["estado"]):
                    bx = x0 + 12 + i * 106
                    im.alpha_composite(_nueve(franja(), 98, 22, 3), (bx, y - 2))
                    im.alpha_composite(_icono(key).img, (bx + 3, y + 1))
                    m.texto(im, (bx + 22, y - 1), nombre, 8, "beige", sombra=False)
                    m.texto(im, (bx + 22, y + 8), f"{int(v * 100)} %", 8, "bone", sombra=False)
                    _barra(im, bx + 52, y + 9, 42, v)
            else:
                for i, (key, nombre, v) in enumerate(D["estado"]):
                    im.alpha_composite(_icono(key).img, (x0 + 12, y - 2))
                    m.texto(im, (x0 + 32, y), nombre, 9, "beige", sombra=False)
                    _barra(im, x0 + 110, y + 2, pw - 170, v)
                    t = f"{int(v * 100)} %"
                    m.texto(im, (x0 + pw - 14 - int(_ancho(t)), y), t, 9, "bone", sombra=False)
                    y += 15
    botones = ("Seguir",) if victoria else ("Volver a empezar", "Menú principal")
    for i, t in enumerate(botones):
        bw = 120
        bx = W // 2 - bw // 2 if victoria else W // 2 - bw - 6 + i * (bw + 12)
        m.pega(im, ui.boton("encima" if i == 0 else "normal", bw, 18), bx, y0 + ph - 26)
        m.texto(im, (bx + bw // 2, y0 + ph - 23), t, 9, "bone", centro=True)
    return im


VARIANTES = ["A", "B", "C", "D", "E"]
NOTAS = {"A": "Cabecera con dibujo (amanecer o estación caída), estrellas grandes, filas y barras de estado debajo",
         "B": "Dos columnas: estrellas y estado (estación, tren, muro) a la izquierda; cifras en fichas a la derecha",
         "C": "Cabecera con dibujo y estrellas pequeñas en la esquina; filas largas",
         "D": "Estado arriba en tres barras anchas, luego estrellas y cifras",
         "E": "Título grande con subtítulo; estrellas, cifras y el estado en tres cajitas al pie"}


def exportar(out):
    out = Path(out)
    todo = {}
    p, pd = panel(48, 64, False, 34), panel(48, 64, True, 34)          # cabecera de 34 px (variante A)
    p.img.save(out / "ui_parte_panel.png"); pd.img.save(out / "ui_parte_panel_derrota.png")
    for n in ("ui_parte_panel", "ui_parte_panel_derrota"):
        (out / f"{n}.json").write_text('{"slice": {"left": 4, "top": 38, "right": 4, "bottom": 4}, "cabecera_px": 34, '
                                       '"nota": "la cabecera entera cae en la franja de arriba; el centro es navy liso"}')
    fr = franja()
    fr.img.save(out / "ui_parte_franja.png")
    (out / "ui_parte_franja.json").write_text('{"slice": 3}')
    el, ev = estrella(True), estrella(False)
    el.img.save(out / "ui_parte_estrella_llena_24.png"); ev.img.save(out / "ui_parte_estrella_vacia_24.png")
    ap = [estrella(True, f=f) for f in range(4)]
    hoja = Image.new("RGBA", (96, 24), (0, 0, 0, 0))
    for f, s in enumerate(ap):
        hoja.alpha_composite(s.img, (24 * f, 0))
    hoja.save(out / "ui_parte_estrella_aparece.png")
    (out / "ui_parte_estrella_aparece.json").write_text('{"frames": 4, "frameSize": [24, 24], "duration_ms": 80, '
                                                       '"loop": false, "uso": "una estrella cada 0,4 s"}')
    bf, br, bu = barra_fondo(), barra_relleno(), barra_umbral()
    bf.img.save(out / "ui_parte_barra_fondo.png"); br.img.save(out / "ui_parte_barra_relleno.png")
    bu.img.save(out / "ui_parte_barra_umbral.png")
    (out / "ui_parte_barra_fondo.json").write_text('{"slice": 2, "relleno": "ui_parte_barra_relleno (1x4) estirado a '
                                                  '(ancho-2)*valor, en x=1, y=1", "umbrales": [0.40, 0.75]}')
    ca, cc = cabecera_amanecer(), cabecera_caida()
    ca.img.save(out / "ui_parte_cabecera_amanecer.png"); cc.img.save(out / "ui_parte_cabecera_caida.png")
    todo["piezas"] = [p, pd, fr, el, ev] + ap + [bf, bu, ca, cc]
    return todo


if __name__ == "__main__":
    OUT = Path(next((a for a in sys.argv[1:] if not a.startswith("--")), "out_parte"))
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    import build_maquetas as m
    # D-N: Mak aprobó la variante A (7-nov 00:34). La muestra de las 5 se regenera con --muestra.
    pantalla("A", True).resize((1920, 1080), Image.NEAREST).save(OUT / "maqueta_1080_parte_victoria.png")
    pantalla("A", False).resize((1920, 1080), Image.NEAREST).save(OUT / "maqueta_1080_parte_derrota.png")
    if "--muestra" in sys.argv:
        hoja = Image.new("RGBA", (5 * 640 + 4 * 12, 2 * 360 + 12 + 40), PAL["navy"])
        for i, v in enumerate(VARIANTES):
            x = i * 652
            m.texto(hoja, (x + 6, 8), f"{v} · {NOTAS[v]}"[:118], 9, "cream")
            hoja.alpha_composite(pantalla(v, True), (x, 40))
            hoja.alpha_composite(pantalla(v, False), (x, 40 + 372))
        hoja.save(OUT / "muestra_parte_5_variantes.png")
    print("ok")
