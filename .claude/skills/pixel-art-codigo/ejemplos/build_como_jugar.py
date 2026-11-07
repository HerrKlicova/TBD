"""Cartel «Cómo se juega» del menú e iconos de estado de los pelotones (P0, noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:44). Las teclas son las de plan-noche-7nov §8: mover (clic derecho), Atacar (A), Mantener (S),
Emplazar (D, solo ametralladores) y Embarcar o desembarcar (E). La selección es clic y arrastre (§2, fila 3) y el
zoom es la rueda (D-N70). La cámara (build Noche05 de Marcos, vía Lucía): flechas, bordes de la pantalla, Z y X,
rueda y arrastre con el botón central. Z acerca y X aleja, como la rueda; clic derecho y arrastre gira; R y F
inclinan (Marcos, 7-nov 00:29).
  ui_tecla_{z,x,r,f}, ui_tecla_raton_giro (17x12), ui_tecla_flechas (26x15), ui_icono_borde_pantalla (16x12), ui_tecla_raton_central (17x12)
  ui_tecla_{a,s,d,e}             tecla de 14x14 con la letra dibujada a mano (sin fuente)
  ui_tecla_raton_rueda           ratón de 9x12 con la rueda marcada
  Reabasteciendo y alto el fuego existen en la lógica de Nacho (PR #24: Resupply y SetHoldFire).
  ui_estado_{emplazado,embarcado,sin_municion,reabasteciendo,alto_el_fuego}_16
                                 estado del pelotón sobre su tarjeta y sobre el pelotón en el mundo
  maqueta_como_jugar_x3.png      el cartel sobre el fondo del menú (texto con fuente provisional)
Estados en hueso con contorno negro; «sin munición» es el icono de la alerta A2 (ámbar de aviso) reducido a 16.
"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402

LETRAS = {
    "a": [".##.", "#..#", "####", "#..#", "#..#"],
    "s": [".###", "#...", ".##.", "...#", "###."],
    "d": ["###.", "#..#", "#..#", "#..#", "###."],
    "e": ["####", "#...", "###.", "#...", "####"],
    "z": ["####", "...#", "..#.", ".#..", "####"],
    "x": ["#..#", "#..#", ".##.", "#..#", "#..#"],
    "r": ["###.", "#..#", "###.", "#.#.", "#..#"],
    "f": ["####", "#...", "###.", "#...", "#..."],
}


def tecla(letra):
    s = Sprite(14, 14)
    s.rect(0, 0, 14, 13, "grey_brown")             # base de la tecla
    s.rect(1, 0, 12, 11, "beige")                  # cara
    s.hline(1, 12, 0, "bone")
    s.vline(1, 0, 10, "bone")
    for y, fila in enumerate(LETRAS[letra]):
        for x, ch in enumerate(fila):
            if ch == "#":
                s.set(5 + x, 3 + y, "brown_dark")
    for x, y in ((0, 0), (13, 0), (0, 12), (13, 12)):
        s.set(x, y, None)
    s.outline()
    return s


EMPLAZADO = """
.......bb.......
......bbbb......
...bbbbbbbbbbb..
......bbbb......
......b..b......
.....b....b.....
....b......b....
...b........b...
..bbb......bbb..
"""

EMBARCADO = """
....bbbbbbb.....
...bggggggbb....
..bbbbbbbbbbbb..
..bggbbbbbbggb..
..bbbbbbbbbbbb..
...ww..ww..ww...
...ww..ww..ww...
"""

SIN_MUNICION = """
.aa..aa..aa.....
.aa..aa..aa.....
.aa..aa..aa.....
.oo..oo..oo.....
.oo..oo..oo.....
.oo..oo..oo.....
...........a....
..........aaa...
.........aa.aa..
.........a...a..
"""

REABASTECIENDO = """
......b.........
.....bbb........
....bbbbb.......
......b.........
.gggggggggggg...
.gllllllllllg...
.gggggggggggg...
.gg.......ggg...
.ggggggggggg....
"""

ALTO_EL_FUEGO = """
..bb..bb..bb....
..bb..bb..bb....
..bb..bb..bb.bb.
..bb..bb..bb.bb.
..bbbbbbbbbbbb..
..bbbbbbbbbbbb..
...bbbbbbbbbb...
....bbbbbbbb....
....bbbbbbbb....
"""

ESTADOS = {
    "emplazado": (EMPLAZADO, {"b": "bone"}, 3),
    "embarcado": (EMBARCADO, {"b": "bone", "g": "grey_light", "w": "grey_brown"}, 4),
    "sin_municion": (SIN_MUNICION, {"a": "amber", "o": "ochre_dark"}, 3),
    "reabasteciendo": (REABASTECIENDO, {"b": "bone", "g": "grey_light", "l": "concrete_light"}, 3),
    "alto_el_fuego": (ALTO_EL_FUEGO, {"b": "bone"}, 3),
}


def estado(nombre):
    if nombre == "sin_municion":                     # el mismo icono que la alerta A2, para que se lea igual
        import build_a2 as a2
        from pixelvm import reduce_16
        return reduce_16(a2.sin_municion())
    texto, keys, y0 = ESTADOS[nombre]
    s = Sprite(16, 16)
    s.grid(0, y0, texto, keys)
    s.outline()
    return s


def rueda_raton():
    """Ratón de 9x12 como tecla_raton (D-N78), pero con los dos botones apagados y la rueda marcada (D-N89)."""
    s = Sprite(9, 12)
    s.rect(0, 0, 9, 12, "beige")
    s.hline(1, 7, 0, "bone")
    s.hline(0, 8, 5, "grey_brown")
    s.vline(4, 0, 4, "grey_brown")
    s.rect(3, 1, 3, 4, "cream")                      # rueda resaltada
    s.vline(4, 1, 4, "ochre_dark")
    for x, y in ((0, 0), (8, 0), (0, 11), (8, 11)):
        s.set(x, y, None)
    s.outline()
    return s


def _tecla_mini(s, x0, y0, dirc):
    """Tecla de flecha de 8x7 con el triángulo dibujado (dirc: arriba, abajo, izq, dcha)."""
    s.rect(x0, y0, 8, 7, "grey_brown")
    s.rect(x0 + 1, y0, 6, 6, "beige")
    s.hline(x0 + 1, x0 + 6, y0, "bone")
    tri = {"arriba": [(4, 2), (3, 3), (4, 3), (5, 3)], "abajo": [(3, 2), (4, 2), (5, 2), (4, 3)],
           "izq": [(3, 3), (4, 2), (4, 3), (4, 4)], "dcha": [(4, 3), (3, 2), (3, 3), (3, 4)]}[dirc]
    for x, y in tri:
        s.set(x0 + x, y0 + y, "brown_dark")


def flechas():
    """Las cuatro flechas en T invertida (26x15)."""
    s = Sprite(26, 15)
    _tecla_mini(s, 9, 0, "arriba")
    for k, d in enumerate(("izq", "abajo", "dcha")):
        _tecla_mini(s, 1 + k * 8, 8, d)
    s.outline()
    return s


def borde_pantalla():
    """Pantalla de 16x12 con una flecha que empuja contra el borde derecho: el ratón en el borde mueve la cámara."""
    s = Sprite(16, 12)
    s.rect(0, 0, 16, 12, "bone")
    s.rect(1, 1, 14, 10, "grey_brown")
    s.hline(6, 11, 5, "bone"); s.hline(6, 11, 6, "bone")
    s.set(10, 3, "bone"); s.set(11, 4, "bone"); s.set(12, 5, "bone"); s.set(12, 6, "bone")
    s.set(11, 7, "bone"); s.set(10, 8, "bone")
    s.set(10, 4, "bone"); s.set(10, 7, "bone")
    s.outline()
    return s


def raton_central():
    """Ratón con la rueda (botón central) pulsada y cuatro flechas de arrastre a la derecha (17x12)."""
    s = Sprite(17, 12)
    r = rueda_raton()
    s.stamp(r, 0, 0)
    cx, cy = 13, 6
    for (dx, dy) in ((0, -1), (0, 1), (-1, 0), (1, 0)):
        for k in (1, 2, 3):
            s.set(cx + dx * k, cy + dy * k, "bone")
    for (x, y) in ((12, 3), (14, 3), (12, 9), (14, 9), (10, 5), (10, 7), (16, 5), (16, 7)):
        s.set(x, y, "bone")
    s.set(cx, cy, "bone")
    return s


def raton_giro():
    """Ratón con el botón derecho marcado y una flecha curva de giro a la derecha (17x12)."""
    import build_maquetas as m
    s = Sprite(17, 12)
    s.stamp(m.tecla_raton("dcho"), 0, 0)
    for x, y in ((11, 4), (12, 3), (13, 3), (14, 3), (15, 4), (16, 5), (16, 6), (15, 7)):
        s.set(x, y, "bone")
    for x, y in ((14, 7), (15, 8), (16, 8)):
        s.set(x, y, "bone")
    return s


CONTROLES = [   # (icono, texto). icono: ("tecla", letra) | ("raton", "izq"/"dcho") | ("rueda",)
    (("raton", "izq"), "Clic y arrastre: seleccionar pelotones"),
    (("raton", "dcho"), "Clic derecho: mover"),
    (("tecla", "a"), "Atacar moviéndose"),
    (("tecla", "s"), "Mantener la posición"),
    (("tecla", "d"), "Emplazar (ametralladores)"),
    (("tecla", "e"), "Embarcar o desembarcar"),
    (("tecla_t",), "Recargar (sin munición)"),
    (("rueda",), "Rueda: acercar y alejar"),
    (("flechas",), "Flechas o borde de pantalla: mover"),
    (("central",), "Botón central y arrastre: mover"),
    (("zx",), "Z y X: acercar y alejar (como la rueda)"),
    (("raton_giro",), "Clic derecho y arrastre: girar la cámara"),
    (("rf",), "R y F: inclinar la cámara"),
]
ESTADOS_TEXTO = [("emplazado", "Emplazado"), ("embarcado", "Embarcado"), ("sin_municion", "Sin munición"),
                 ("reabasteciendo", "Reabasteciendo"), ("alto_el_fuego", "Alto el fuego")]


def cartel():
    """Cartel sobre el fondo del menú (480x270): objetivo arriba, controles a la izquierda y estados a la derecha."""
    import build_interfaz as ui
    import build_maquetas as m
    fondo = ui.menu_fondo().copy()
    W, H = fondo.size
    pw, ph = 440, 258
    x0, y0 = (W - pw) // 2, (H - ph) // 2
    m.pega(fondo, ui.panel(pw, ph), x0, y0)
    m.texto(fondo, (W // 2, y0 + 8), "Cómo se juega", 13, "cream", centro=True)
    m.texto(fondo, (W // 2, y0 + 28), "Aguanta en Sagunto de las 22:00 al amanecer. Si cae la estación o el tren, se acaba.", 9,
            "beige", centro=True, sombra=False)
    for i, (ic, t) in enumerate(CONTROLES):
        y = y0 + 46 + i * 16
        tx = x0 + 38
        if ic[0] == "tecla":
            m.pega(fondo, tecla(ic[1]), x0 + 16, y)
        elif ic[0] == "raton":
            m.pega(fondo, m.tecla_raton(ic[1]), x0 + 18, y + 1)
        elif ic[0] == "flechas":
            m.pega(fondo, flechas(), x0 + 10, y - 1)
            m.pega(fondo, borde_pantalla(), x0 + 40, y + 1)
            tx = x0 + 62
        elif ic[0] == "central":
            m.pega(fondo, raton_central(), x0 + 18, y + 1)
            tx = x0 + 42
        elif ic[0] == "tecla_t":
            import build_avisos_sara as sara
            m.pega(fondo, sara.tecla_t(False), x0 + 16, y)
        elif ic[0] in ("zx", "rf"):
            a, b = ic[0]
            m.pega(fondo, tecla(a), x0 + 8, y)
            m.pega(fondo, tecla(b), x0 + 24, y)
            tx = x0 + 44
        elif ic[0] == "raton_giro":
            m.pega(fondo, raton_giro(), x0 + 18, y + 1)
            tx = x0 + 42
        else:
            s = rueda_raton()
            m.pega(fondo, s, x0 + 18, y + 1)
        m.texto(fondo, (tx, y + 2), t, 9, "bone", sombra=False)
    m.texto(fondo, (x0 + 290, y0 + 52), "Estados del pelotón", 9, "cream", sombra=False)
    for i, (n, t) in enumerate(ESTADOS_TEXTO):
        y = y0 + 72 + i * 24
        m.pega(fondo, estado(n), x0 + 290, y)
        m.texto(fondo, (x0 + 312, y + 3), t, 9, "bone", sombra=False)
    bx = x0 + 290 + 48
    m.pega(fondo, ui.boton("encima", 96, 18), bx - 48, y0 + ph - 26)
    m.texto(fondo, (bx, y0 + ph - 23), "Volver", 9, "bone", centro=True)
    return fondo


def exportar(out):
    out = Path(out)
    todo = {}
    rueda_raton().img.save(out / "ui_tecla_raton_rueda.png")
    flechas().img.save(out / "ui_tecla_flechas.png")
    borde_pantalla().img.save(out / "ui_icono_borde_pantalla.png")
    raton_central().img.save(out / "ui_tecla_raton_central.png")
    raton_giro().img.save(out / "ui_tecla_raton_giro.png")
    for l in LETRAS:
        s = tecla(l)
        s.img.save(out / f"ui_tecla_{l}.png")
        todo[f"tecla_{l}"] = s
    for n in ESTADOS:
        s = estado(n)
        s.img.save(out / f"ui_estado_{n}_16.png")
        todo[f"estado_{n}"] = s
    return todo


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_como_jugar")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    c = cartel()
    c.resize((c.width * 3, c.height * 3), Image.NEAREST).save(OUT / "maqueta_como_jugar_x3.png")
    E = 6
    sp = list(todo.values())
    hoja = Image.new("RGBA", (sum(s.w * E + 8 for s in sp), 16 * E), PAL["slate_dark"])
    x = 0
    for s in sp:
        hoja.alpha_composite(s.img.resize((s.w * E, s.h * E), Image.NEAREST), (x, 0))
        x += s.w * E + 8
    hoja.save(OUT / "contacto_estados_teclas_x6.png")
    print("ok")
