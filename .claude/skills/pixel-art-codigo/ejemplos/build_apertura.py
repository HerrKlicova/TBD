"""Apertura de la noche (D-N91: el tren llega y la cámara se abre a la estación sin cortes). Bruno (Arte técnico).

Encargo de Mak (7-nov, 00:17). La letra la pone UI Toolkit; aquí van las placas, los iconos y la barra.
  ui_rotulo_inicio        9-slice 40x30 (slice 9): placa del rótulo «Sagunto · 22:00 · Noche 1», con doble filete
                          beige arriba y abajo y rombos en los extremos. Sin rojo: es información.
  ui_rotulo_filete_64     filete de 64x3 para separar «Sagunto» de «22:00 · Noche 1» si va en dos líneas
  ui_icono_luna_9         luna de la noche (va delante de «Noche 1»)
  ui_aviso_oleada_placa   9-slice 24x16 (slice 4) del aviso «Primera oleada en 01:30»
  ui_aviso_oleada_urgente la misma con el borde en ámbar, 2 f: SOLO los últimos 10 s (aviso pasajero)
  ui_icono_reloj_arena    reloj de arena de 9x11, 4 f (la arena cae), delante de la cuenta atrás
  ui_barra_noche_fondo    64x4: pista con una marca cada hora (8 tramos de 8 px, 22:00 → 06:00)
  ui_barra_noche_relleno  64x4: el relleno completo de la noche al alba; se recorta a lo ancho según el progreso
  ui_barra_noche_marca    3x7: la aguja de la hora actual
  ui_barra_noche_{luna,sol}_7  iconos de los extremos de la barra
Va justo debajo del panel del reloj (64x22, arriba en el centro, D-N65).
"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import tramado  # noqa: E402


def _rombo(s, cx, cy, c):
    for dx, dy in ((0, -1), (-1, 0), (0, 0), (1, 0), (0, 1)):
        s.set(cx + dx, cy + dy, c)


def rotulo(w=40, h=30):
    s = Sprite(w, h)
    s.rect(0, 0, w, h, "navy")
    for y in (2, 4, h - 5, h - 3):
        s.hline(4, w - 5, y, "beige" if y in (2, h - 3) else "grey_brown")
    for y in (3, h - 4):
        _rombo(s, 2, y, "beige"); _rombo(s, w - 3, y, "beige")
    for x in range(w):
        s.set(x, 0, "olive_grey"); s.set(x, h - 1, "grey_brown")
    for y in range(h):
        s.set(0, y, "olive_grey"); s.set(w - 1, y, "grey_brown")
    for x, y in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        s.set(x, y, None)
    return s


def filete(w=64):
    s = Sprite(w, 3)
    s.hline(4, w - 5, 1, "beige")
    _rombo(s, w // 2, 1, "cream")
    s.set(1, 1, "beige"); s.set(w - 2, 1, "beige")
    return s


def luna():
    s = Sprite(9, 9)
    s.grid(0, 0, """
..bbbb...
.bbb.....
bbb......
bbb......
bbb......
bbbb.....
.bbbb..b.
..bbbbbb.
....bb...""", {"b": "bone", ".": "_"})
    s.outline()
    return s


def placa_oleada(urgente=False, f=0):
    s = Sprite(24, 16)
    s.rect(0, 0, 24, 16, "navy")
    borde = ("amber" if f == 0 else "ochre_dark") if urgente else "beige"
    sombra = "ochre_dark" if urgente else "grey_brown"
    for x in range(24):
        s.set(x, 0, borde); s.set(x, 15, sombra)
    for y in range(16):
        s.set(0, y, borde); s.set(23, y, sombra)
    s.set(1, 1, "cream" if urgente and f == 0 else "bone")
    for x, y in ((0, 0), (23, 0), (0, 15), (23, 15)):
        s.set(x, y, None)
    return s


def reloj_arena(f):
    """La arena baja de arriba abajo en 4 fotogramas (bucle de 1 s mientras corre la cuenta atrás)."""
    s = Sprite(9, 11)
    s.hline(0, 8, 0, "beige"); s.hline(0, 8, 10, "beige")
    for y in range(1, 10):
        a = abs(y - 5)
        s.set(4 - a, y, "bone") if a else None
        s.set(4 + a, y, "bone") if a else None
    s.set(4, 5, "bone")
    arriba = 3 - f                                   # filas de arena que quedan arriba
    for k in range(arriba):
        y = 4 - k
        for x in range(4 - (5 - y) + 1, 4 + (5 - y)):
            s.set(x, y, "beige")
    for k in range(f + 1):                           # montón de abajo
        y = 9 - k
        for x in range(4 - (y - 5) + 1, 4 + (y - 5)):
            s.set(x, y, "beige")
    if f % 2 == 0:
        s.set(4, 6, "beige")                         # hilo que cae
    s.outline()
    return s


def barra_fondo():
    s = Sprite(64, 4)
    s.rect(0, 0, 64, 4, "navy")
    s.hline(0, 63, 3, "grey_brown")
    for k in range(9):
        x = min(63, k * 8)
        s.vline(x, 0, 3, "olive_grey")
    s.vline(0, 0, 3, "beige"); s.vline(63, 0, 3, "beige")
    return s


RAMPA = ["olive_grey", "purple_grey", "rose_brown", "ochre_dark", "beige", "cream"]


def barra_relleno():
    """De la noche al alba: oliva gris → gris violeta → óxido rosado → ocre → beige → crema, tramado entre tonos."""
    s = Sprite(64, 4)
    for x in range(64):
        t = x / 63 * (len(RAMPA) - 1)
        i = int(t)
        frac = t - i
        for y in range(3):
            c = RAMPA[min(i + 1, len(RAMPA) - 1)] if tramado(x, y, frac) else RAMPA[i]
            s.set(x, y, c)
        s.set(x, 0, RAMPA[min(i + 1, len(RAMPA) - 1)] if x < 57 else "bone")   # filo de luz arriba
        s.set(x, 3, "grey_brown")
    return s


def barra_marca():
    s = Sprite(3, 7)
    s.vline(1, 0, 6, "bone")
    s.hline(0, 2, 0, "bone"); s.hline(0, 2, 6, "bone")
    return s


def sol():
    s = Sprite(7, 7)
    s.grid(0, 0, """
...c...
.c...c.
..ccc..
c.ccc.c
..ccc..
.c...c.
...c...""", {"c": "cream", ".": "_"})
    return s


def luna7():
    s = Sprite(7, 7)
    s.grid(0, 0, """
..bbb..
.bb....
bb.....
bb.....
bb.....
.bb....
..bbb..""", {"b": "bone", ".": "_"})
    return s


def exportar(out):
    out = Path(out)
    todo = {}
    r = rotulo()
    r.img.save(out / "ui_rotulo_inicio.png")
    (out / "ui_rotulo_inicio.json").write_text('{"slice": 9, "texto": "Sagunto · 22:00 · Noche 1", '
                                              '"uso": "centro de la pantalla mientras la cámara se abre a la estación; '
                                              'entra y sale en 0,3 s, dura unos 3 s"}', encoding="utf-8")
    fi, lu = filete(), luna()
    fi.img.save(out / "ui_rotulo_filete_64.png")
    lu.img.save(out / "ui_icono_luna_9.png")
    todo["rotulo"] = [r, fi, lu]
    p = placa_oleada()
    p.img.save(out / "ui_aviso_oleada_placa.png")
    (out / "ui_aviso_oleada_placa.json").write_text('{"slice": 4}')
    ur = [placa_oleada(True, f) for f in range(2)]
    hoja = Image.new("RGBA", (48, 16), (0, 0, 0, 0))
    for f, s in enumerate(ur):
        hoja.alpha_composite(s.img, (24 * f, 0))
    hoja.save(out / "ui_aviso_oleada_urgente.png")
    (out / "ui_aviso_oleada_urgente.json").write_text('{"slice": 4, "frames": 2, "duration_ms": 250, "loop": true, '
                                                     '"uso": "solo los últimos 10 s de la cuenta atrás"}', encoding="utf-8")
    ra = [reloj_arena(f) for f in range(4)]
    hoja = Image.new("RGBA", (36, 11), (0, 0, 0, 0))
    for f, s in enumerate(ra):
        hoja.alpha_composite(s.img, (9 * f, 0))
    hoja.save(out / "ui_icono_reloj_arena.png")
    (out / "ui_icono_reloj_arena.json").write_text('{"frames": 4, "frameSize": [9, 11], "duration_ms": 250, "loop": true}')
    todo["oleada"] = [p] + ur + ra
    bf, br, bm, so, l7 = barra_fondo(), barra_relleno(), barra_marca(), sol(), luna7()
    bf.img.save(out / "ui_barra_noche_fondo.png")
    br.img.save(out / "ui_barra_noche_relleno.png")
    bm.img.save(out / "ui_barra_noche_marca.png")
    so.img.save(out / "ui_barra_noche_sol_7.png")
    l7.img.save(out / "ui_barra_noche_luna_7.png")
    (out / "ui_barra_noche.json").write_text(
        '{"ancho_px": 64, "horas": [22, 6], "px_por_hora": 8, "relleno": "recortar a lo ancho: progreso * 64", '
        '"marca": "centrada en x = progreso * 63", "posicion": "justo debajo del panel del reloj (64x22), con luna a '
        'la izquierda y sol a la derecha"}', encoding="utf-8")
    todo["barra"] = [bf, br, bm, so, l7]
    return todo


def maqueta(prog=0.0, cuenta="01:30", urgente=False):
    """Apertura sobre la escena del HUD (640x360): reloj con la barra, rótulo al centro y aviso de primera oleada."""
    import build_maquetas as m
    import build_interfaz as ui
    im = m.escena()
    W = m.W
    m.pega(im, ui.panel(64, 22), W // 2 - 32, 4)
    m.texto(im, (W // 2, 8), "22:00", 11, centro=True)
    bx, by = W // 2 - 32, 28
    m.pega(im, barra_fondo(), bx, by)
    rel = barra_relleno().img.crop((0, 0, max(1, int(prog * 64)), 4))
    im.alpha_composite(rel, (bx, by))
    m.pega(im, barra_marca(), bx + int(prog * 63) - 1, by - 2)
    m.pega(im, luna7(), bx - 9, by - 2); m.pega(im, sol(), bx + 66, by - 2)
    # rótulo de inicio
    rw, rh = 220, 52
    base = rotulo()
    placa = Image.new("RGBA", (rw, rh), (0, 0, 0, 0))
    sl = 9                                           # 9-slice a mano para la maqueta
    src = base.img
    for (sx0, sx1, dx0, dx1) in ((0, sl, 0, sl), (sl, 40 - sl, sl, rw - sl), (40 - sl, 40, rw - sl, rw)):
        for (sy0, sy1, dy0, dy1) in ((0, sl, 0, sl), (sl, 30 - sl, sl, rh - sl), (30 - sl, 30, rh - sl, rh)):
            trozo = src.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.NEAREST)
            placa.alpha_composite(trozo, (dx0, dy0))
    x0, y0 = W // 2 - rw // 2, 120
    im.alpha_composite(placa, (x0, y0))
    m.texto(im, (W // 2, y0 + 10), "Sagunto", 13, "cream", centro=True)
    m.pega(im, filete(120), W // 2 - 60, y0 + 28)
    m.pega(im, luna(), W // 2 - 50, y0 + 33)
    m.texto(im, (W // 2 + 6, y0 + 33), "22:00 · Noche 1", 9, "beige", centro=True)
    # aviso de primera oleada, a la derecha del reloj
    aw, ah = 150, 18
    pl = placa_oleada(urgente).img
    av = Image.new("RGBA", (aw, ah), (0, 0, 0, 0))
    s4 = 4
    for (sx0, sx1, dx0, dx1) in ((0, s4, 0, s4), (s4, 24 - s4, s4, aw - s4), (24 - s4, 24, aw - s4, aw)):
        for (sy0, sy1, dy0, dy1) in ((0, s4, 0, s4), (s4, 16 - s4, s4, ah - s4), (16 - s4, 16, ah - s4, ah)):
            av.alpha_composite(pl.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.NEAREST), (dx0, dy0))
    ax, ay = W // 2 - aw // 2, 40
    im.alpha_composite(av, (ax, ay))
    m.pega(im, reloj_arena(1), ax + 5, ay + 4)
    m.texto(im, (ax + 18, ay + 3), f"Primera oleada en {cuenta}", 9, "cream" if urgente else "bone", sombra=False)
    return im


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_apertura")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 6
    filas = list(todo.values())
    Wt = max(sum(s.w * E + 8 for s in f) for f in filas)
    Ht = sum(max(s.h for s in f) * E + 8 for f in filas)
    hoja = Image.new("RGBA", (Wt, Ht), PAL["olive_grey"])
    y = 0
    for f in filas:
        x = 0
        for s in f:
            hoja.alpha_composite(s.img.resize((s.w * E, s.h * E), Image.NEAREST), (x, y))
            x += s.w * E + 8
        y += max(s.h for s in f) * E + 8
    hoja.save(OUT / "contacto_apertura_x6.png")
    maqueta(0.0).resize((1920, 1080), Image.NEAREST).save(OUT / "maqueta_1080_apertura.png")
    maqueta(0.12, "00:08", urgente=True).resize((1920, 1080), Image.NEAREST).save(OUT / "maqueta_1080_apertura_urgente.png")
    print("ok")
