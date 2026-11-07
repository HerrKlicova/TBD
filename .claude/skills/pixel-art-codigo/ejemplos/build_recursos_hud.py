"""Iconos de recursos del HUD y de la pantalla de resultados (P0, noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:38), con la D-N78 como guía. Mismo estilo que A1: luz fija arriba a la izquierda, contorno de
1 px, sin texto ni marco, sin rojo de ataque.
  ui_recurso_{municion,materiales,combustible}_{32,16}   los de A1, exportados por fin para Unity
  ui_recurso_pila_{32,16}     la pila de la horda contra el muro (la cifra que vigila el jugador; GDD §8)
  ui_recurso_bajas_{32,16}    bajas propias: fusil clavado con el casco encima
  ui_resultado_{zombis,amanecer,caida}_16   iconos de fila de la pantalla final (zombis abatidos, fin de la noche)
  ui_resultado_{muro,integridad}_16         D-N104: muro (saco terrero) e integridad (estación y tren)
  ui_estrellas_{0,1,2,3}      grupo de 3 estrellas de 16 (56x16) con las llenas a la izquierda
  ui_panel_derrota            ui_panel con el marco en rojo de ataque (#982420), slice 4, solo para la derrota
"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite, reduce_16  # noqa: E402
import build_a1 as a1  # noqa: E402
from build_maquetas import estrella  # noqa: E402


def pila():
    """Muro de bloques a la derecha y la horda amontonada contra él en rampa, con una mano asomando por arriba.
    D-N88: la horda va en sus valores (303424/283440) con un filo de 1 px en beige b4a080 arriba; nunca en oliva,
    que es el color de la tropa propia. La horda es siempre lo más oscuro, también en la interfaz."""
    s = Sprite()
    s.rect(21, 7, 8, 22, "concrete")                     # muro
    s.vline(21, 7, 28, "concrete_light")
    s.hline(21, 28, 7, "concrete_light")
    s.vline(28, 8, 28, "grey_brown")
    for y in (12, 17, 22, 27):                           # juntas
        s.hline(22, 27, y, "grey_brown")
    for y, x in ((9, 24), (14, 26), (19, 23), (24, 25)):
        s.vline(x, y, y + 2, "grey_brown")
    cabezas = [(4, 26), (8, 26), (12, 26), (16, 26), (6, 22), (10, 22), (14, 22), (18, 23),
               (9, 18), (13, 18), (17, 19), (13, 14), (17, 15), (17, 11)]
    for x, y in cabezas:                                 # cada cabeza, 4x4 con hombros debajo, en valores de la horda
        s.rect(x - 1, y + 2, 5, 2, "slate_dark")
        s.rect(x, y - 1, 3, 3, "olive_shadow")
        s.set(x + 2, y + 1, "slate_dark")
    s.line(18, 10, 20, 5, "olive_shadow")                # brazo que sube por el muro
    s.rect(20, 4, 2, 2, "olive_shadow")                  # mano en el borde
    s.line(13, 13, 12, 8, "olive_shadow")                # otro brazo levantado
    s.set(12, 7, "olive_shadow")
    px = s.img.load()                                    # filo de 1 px en beige arriba de cada pieza de la horda
    horda = {PAL["olive_shadow"], PAL["slate_dark"]}
    for x in range(2, 21):
        for y in range(32):
            if tuple(px[x, y][:3]) in {tuple(c[:3]) for c in horda} and px[x, y][3]:
                if y == 0 or px[x, y - 1][3] == 0:
                    s.set(x, y, "beige")
    s.hline(2, 28, 29, "olive_dark")                     # suelo
    s.outline()
    return s


def bajas():
    """Fusil clavado en la tierra con el casco del ET encima y las botas delante: la baja propia, sin sangre ni rojo."""
    s = Sprite()
    s.rect(15, 9, 3, 19, "grey_brown")                   # fusil boca abajo, largo y grueso
    s.vline(15, 9, 27, "concrete")
    s.rect(14, 9, 5, 4, "olive_grey")                    # culata, bajo el casco
    s.vline(14, 9, 12, "concrete")
    s.rect(18, 15, 3, 2, "grey_brown")                   # cargador curvo hacia fuera
    s.rect(19, 17, 3, 2, "grey_brown")
    s.set(19, 15, "concrete")
    s.rect(12, 17, 3, 2, "grey_brown")                   # pistolete
    s.poly([(10, 9), (11, 5), (14, 3), (19, 3), (22, 5), (23, 9)], "olive_dark")   # casco
    s.hline(9, 24, 9, "olive_grey")                      # ala del casco
    s.line(12, 5, 14, 4, "olive_light")                  # brillo
    s.set(13, 6, "olive_light")
    s.set(18, 5, "olive"); s.set(20, 7, "olive"); s.set(15, 7, "olive")   # moteado del camuflaje
    s.set(21, 5, "brown")
    s.poly([(6, 29), (9, 27), (24, 27), (27, 29)], "brown")              # montón de tierra
    s.hline(9, 23, 27, "ochre_dark")
    for x0 in (7, 21):                                   # botas
        s.rect(x0, 22, 3, 5, "brown_dark")
        s.rect(x0, 25, 5, 2, "brown_dark")
        s.vline(x0, 22, 25, "brown")
    s.outline()
    return s


ZOMBI16 = """
.....oooo.......
....oOOooo......
....oOOooo......
....oxoxod......
....ooooood.....
.....oood.......
....dddddd......
...dddddddd.....
"""

AMANECER16 = """
.......c........
..c....c....c...
...c.......c....
.....bbbbb......
....bccccbb.....
.c.bccccccbb.c..
...bccccccbb....
hhhhhhhhhhhhhhhh
gggggggggggggggg
"""

CAIDA16 = """
.........bbb....
........bb......
.......bb.......
.......bb.......
.......bb.......
........bb......
.........bbb....
................
"""


MURO16 = """
...ssssssss.....
..sSSSSSSSSs....
.sSSSSSSSSSSs...
.sSddSSSSddSs...
.sSSSSSSSSSSs...
..sddddddddss...
...ssssssss.....
..ssssss.sssss..
.sSSSSSs.sSSSSs.
.sddSSSs.sddSSs.
..sddddss.ddds..
"""

INTEGRIDAD16 = """
....rrrrrrr.....
...rrrrrrrrr....
..rrrrrrrrrrr...
...wwwwwwwww....
...wbbwbbwbb....
...wwwwwwwww....
..........llll..
.ooooooooolbbl..
.oxxoxxoxolllll.
.ooooooooolllll.
..kk..kk..kk.kk.
gggggggggggggggg
"""


def _grid(texto, keys, y0=3):
    s = Sprite(16, 16)
    s.grid(0, y0, texto, keys)
    s.outline()
    return s


def zombis_16():
    return _grid(ZOMBI16, {"o": "olive", "O": "olive_light", "x": "olive_black", "d": "olive_dark"}, 4)


def amanecer_16():
    return _grid(AMANECER16, {"c": "cream", "b": "beige", "h": "ochre_dark", "g": "olive_dark"}, 3)


def muro_16():
    """D-N104: un saco terrero sobre dos (muro, sacos y nidos)."""
    return _grid(MURO16, {"s": "ochre_dark", "S": "beige", "d": "olive_grey"}, 2)


def integridad_16():
    """D-N104: el edificio de viajeros con su tejado y, delante, la locomotora en la vía (lo que defiendes)."""
    s = Sprite(16, 16)
    s.grid(0, 1, INTEGRIDAD16, {"r": "rust", "w": "beige", "b": "olive_grey", "d": "grey_brown",
                                "g": "concrete", "o": "olive", "x": "olive_light", "l": "olive_light", "k": "grey_brown"})
    s.outline()
    return s


def caida_16():
    """Luna menguante: la noche no terminó (derrota)."""
    return _grid(CAIDA16, {"b": "bone"}, 4)


def estrellas(n):
    s = Sprite(56, 16)
    for k in range(3):
        s.stamp(estrella(k < n), k * 20, 0)
    return s


def exportar(out):
    out = Path(out)
    todo = {}
    for n, fn in (("municion", a1.municion), ("materiales", a1.materiales), ("combustible", a1.combustible),
                  ("pila", pila), ("bajas", bajas)):
        s32 = fn()
        s16 = reduce_16(s32)
        s32.img.save(out / f"ui_recurso_{n}_32.png")
        s16.img.save(out / f"ui_recurso_{n}_16.png")
        todo[n] = (s32, s16)
    for n, fn in (("zombis", zombis_16), ("amanecer", amanecer_16), ("caida", caida_16), ("muro", muro_16),
                  ("integridad", integridad_16)):
        s = fn()
        s.img.save(out / f"ui_resultado_{n}_16.png")
        todo[f"res_{n}"] = (s,)
    import build_interfaz as ui
    pd = ui.panel()                                      # panel de la derrota: el marco rojo se queda (Lucía, D-N78)
    for y in (0, pd.h - 1):
        pd.hline(0, pd.w - 1, y, "red_bright")
    for x in (0, pd.w - 1):
        pd.vline(x, 0, pd.h - 1, "red_bright")
    pd.img.save(out / "ui_panel_derrota.png")
    (out / "ui_panel_derrota.json").write_text('{"slice": 4, "nota": "9-slice como ui_panel; solo para la pantalla de derrota"}')
    todo["panel_derrota"] = (pd,)
    for k in range(4):
        s = estrellas(k)
        s.img.save(out / f"ui_estrellas_{k}.png")
        todo[f"estrellas_{k}"] = (s,)
    return todo


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_recursos_hud")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 5
    sprites = [s for v in todo.values() for s in v]
    Wt = sum(s.w * E + 8 for s in sprites)
    hoja = Image.new("RGBA", (Wt, 32 * E), PAL["slate_dark"])
    x = 0
    for s in sprites:
        hoja.alpha_composite(s.img.resize((s.w * E, s.h * E), Image.NEAREST), (x, 0))
        x += s.w * E + 8
    hoja.save(OUT / "contacto_recursos_hud_x5.png")
    print("ok")
