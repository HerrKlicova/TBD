"""Piezas para el prototipo del 6-nov (petición de Marcos). Bruno (Arte técnico).

Mundo, 32 px, vista cenital, albedo plano: puerta, nido de ametralladora y foco.
  Valor partido (D-N57, Lucía): caras de arriba claras (beige y hueso) y costados oscuros
  (olive_dark) con contorno negro, para que no desaparezcan sobre la trinchera (oliva con el mismo L*).
  Sin luz cocida más allá de esa partición.
Interfaz, 16 px: obreros y reloj de arena de obra en marcha (familia A1–A6: luz arriba a la izquierda,
  contorno de 1 px, hueso para lo que es interfaz).
"""
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import Sprite, check, contact_sheet  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_proto")

ARRIBA, ARRIBA2, COSTADO = "beige", "bone", "olive_dark"   # D-N57


def puerta():
    """Puerta de la empalizada vista desde arriba: dos postes de hormigón y dos hojas de chapa
    con travesaños, un poco abiertas en el centro. Ocupa el ancho de un tramo de muro."""
    s = Sprite()
    for x0 in (0, 27):                                   # postes
        s.rect(x0, 9, 5, 14, "concrete_light")
        s.rect(x0, 20, 5, 3, "concrete")                 # costado del poste (abajo)
        s.set(x0 + 2, 12, "bone")
    for x0, x1 in ((5, 14), (17, 26)):                   # hojas
        s.rect(x0, 12, x1 - x0 + 1, 7, ARRIBA)
        s.rect(x0, 17, x1 - x0 + 1, 2, COSTADO)          # canto de la chapa
        for x in range(x0 + 2, x1, 3):
            s.vline(x, 12, 16, "olive")                  # nervios
        s.hline(x0, x1, 12, ARRIBA2)                     # travesaño superior
    s.rect(14, 14, 3, 2, "slate_dark")                   # cerrojo
    s.set(15, 14, "concrete_light")
    s.outline()
    return s


def nido():
    """Nido de ametralladora desde arriba: anillo de sacos con tronera al norte, tirador (casco)
    y el arma asomando por la tronera."""
    s = Sprite()
    cx, cy = 15.5, 17.5
    for y in range(32):
        for x in range(32):
            d = math.hypot(x + .5 - cx - .5, y + .5 - cy - .5)
            ang = math.degrees(math.atan2(y + .5 - cy, x + .5 - cx)) % 360
            if 8.5 <= d <= 13.5 and not (250 < ang < 290):
                a = (ang + (0 if d < 11 else 15)) % 30   # sacos de 30º, hiladas desfasadas
                c = ARRIBA2 if d < 12.6 else "olive_yellow"
                if a < 3 or abs(d - 11) < .5:
                    c = "olive_grey"                     # juntas entre sacos
                if d > 12.8:
                    c = COSTADO                          # cara exterior del anillo
                s.set(x, y, c)
    s.ellipse(12, 15, 19, 22, ARRIBA)                    # casco del tirador, visto desde arriba
    s.hline(14, 17, 15, "bone")
    s.ellipse(11, 20, 20, 24, COSTADO, only=None)        # hombros
    s.ellipse(12, 15, 19, 22, ARRIBA)
    s.set(14, 16, "bone")
    s.rect(15, 5, 2, 11, "slate_dark")                   # arma hacia el norte
    s.vline(15, 6, 14, "slate")
    s.rect(14, 4, 4, 2, "slate_dark")                    # bocacha
    s.set(18, 12, "amber")                               # cinta de latón
    s.set(18, 13, "ochre_dark")
    s.outline()
    return s


def foco():
    """Foco de noche desde arriba: trípode, horquilla y el tambor con la lente al norte."""
    s = Sprite()
    for (x1, y1) in ((4, 28), (27, 28), (16, 3)):        # patas del trípode
        s.line(16, 17, x1, y1, "slate_dark")
    s.rect(9, 13, 14, 3, COSTADO)                        # horquilla
    s.rect(11, 8, 10, 14, ARRIBA)                        # tambor
    s.vline(11, 8, 21, ARRIBA2)
    s.vline(20, 8, 21, COSTADO)
    s.hline(12, 19, 21, COSTADO)
    for y in (11, 14, 17):
        s.hline(12, 19, y, "olive")                      # aletas de ventilación
    s.rect(10, 5, 12, 3, "concrete_light")               # aro de la lente
    s.hline(11, 20, 5, "bone")
    s.hline(12, 19, 6, "cream")                          # lente
    s.set(8, 14, "slate_dark")                           # mando
    s.outline()
    return s


OBREROS16 = """
....yyy...
...yyyyy..
...ottto..
....ttt...
..cccccc..m
..cccccc.m.
..cxcccc.m.
..cccccc.m.
...gg.gg.m.
...gg.gg.m.
...bb.bb.mmm
"""


def obreros_16():
    """Obrero con casco de obra y pala (interfaz, 16 px)."""
    s = Sprite(16, 16)
    s.grid(2, 2, OBREROS16, {"y": "olive_yellow", "o": "olive_grey", "t": "tan", "c": "olive",
                             "x": "olive_light", "g": "grey_brown", "b": "brown_dark", "m": "concrete_light"})
    s.set(5, 2, "beige")
    s.outline()
    return s


RELOJ16 = """
hhhhhhhhh
.h.....h.
.hsssssh.
..hsssh..
...hsh...
....s....
...h.h...
..h.s.h..
.h.sss.h.
.hsssssh.
hhhhhhhhh
"""


def reloj_16():
    """Reloj de arena de obra en marcha: marco en hueso, arena en beige cayendo."""
    s = Sprite(16, 16)
    s.grid(3, 2, RELOJ16, {"h": "bone", "s": "beige"})
    s.set(4, 3, "grey_light")
    s.outline()
    return s


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    mundo = {"puerta": puerta(), "nido": nido(), "foco": foco()}
    ui = {"obreros": obreros_16(), "reloj_obra": reloj_16()}
    for n, s in mundo.items():
        s.save(OUT / f"vm_mundo_{n}_32.png")
        print(n, check(s, recurso=True) or "OK")
    for n, s in ui.items():
        s.save(OUT / f"vm_ui_{n}_16.png")
        print(n, check(s, recurso=True) or "OK")
    contact_sheet([list(mundo.values()), list(ui.values())], OUT / "contacto_prototipo.png", scale=6,
                  labels=["mundo 32", "interfaz 16"])
