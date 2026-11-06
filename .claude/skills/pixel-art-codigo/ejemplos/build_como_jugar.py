"""Cartel «Cómo se juega» del menú e iconos de estado de los pelotones (P0, noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:44). Las teclas son las de plan-noche-7nov §8: mover (clic derecho), Atacar (A), Mantener (S),
Emplazar (D, solo ametralladores) y Embarcar o desembarcar (E). La selección es clic y arrastre (§2, fila 3) y el
zoom es la rueda (D-N70). El desplazamiento de la cámara NO está en el plan: va marcado como «por confirmar».
  ui_tecla_{a,s,d,e}             tecla de 14x14 con la letra dibujada a mano (sin fuente)
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


CONTROLES = [   # (icono, texto). icono: ("tecla", letra) | ("raton", "izq"/"dcho") | ("rueda",)
    (("raton", "izq"), "Clic y arrastre: seleccionar pelotones"),
    (("raton", "dcho"), "Clic derecho: mover"),
    (("tecla", "a"), "Atacar moviéndose"),
    (("tecla", "s"), "Mantener la posición"),
    (("tecla", "d"), "Emplazar (ametralladores)"),
    (("tecla", "e"), "Embarcar o desembarcar"),
    (("rueda",), "Rueda: acercar y alejar"),
]
ESTADOS_TEXTO = [("emplazado", "Emplazado"), ("embarcado", "Embarcado"), ("sin_municion", "Sin munición"),
                 ("reabasteciendo", "Reabasteciendo"), ("alto_el_fuego", "Alto el fuego")]


def cartel():
    """Cartel sobre el fondo del menú (480x270): objetivo arriba, controles a la izquierda y estados a la derecha."""
    import build_interfaz as ui
    import build_maquetas as m
    fondo = ui.menu_fondo().copy()
    W, H = fondo.size
    pw, ph = 440, 236
    x0, y0 = (W - pw) // 2, (H - ph) // 2
    m.pega(fondo, ui.panel(pw, ph), x0, y0)
    m.texto(fondo, (W // 2, y0 + 8), "Cómo se juega", 13, "cream", centro=True)
    m.texto(fondo, (W // 2, y0 + 28), "Aguanta en Sagunto de las 22:00 al amanecer. Si la horda entra, cae.", 9,
            "beige", centro=True, sombra=False)
    for i, (ic, t) in enumerate(CONTROLES):
        y = y0 + 52 + i * 22
        if ic[0] == "tecla":
            m.pega(fondo, tecla(ic[1]), x0 + 16, y)
        elif ic[0] == "raton":
            m.pega(fondo, m.tecla_raton(ic[1]), x0 + 18, y + 1)
        else:
            s = m.tecla_raton("dcho")
            s.rect(4, 1, 1, 3, "brown_dark")                 # rueda marcada
            m.pega(fondo, s, x0 + 18, y + 1)
        m.texto(fondo, (x0 + 38, y + 2), t, 9, "bone", sombra=False)
    m.texto(fondo, (x0 + 290, y0 + 52), "Estados del pelotón", 9, "cream", sombra=False)
    for i, (n, t) in enumerate(ESTADOS_TEXTO):
        y = y0 + 72 + i * 24
        m.pega(fondo, estado(n), x0 + 290, y)
        m.texto(fondo, (x0 + 312, y + 3), t, 9, "bone", sombra=False)
    m.pega(fondo, ui.boton("encima", 96, 18), W // 2 - 48, y0 + ph - 26)
    m.texto(fondo, (W // 2, y0 + ph - 23), "Volver", 9, "bone", centro=True)
    return fondo


def exportar(out):
    out = Path(out)
    todo = {}
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
