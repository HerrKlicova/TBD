"""A1 · Cargas (32 px y 16 px) dibujadas por código. Bruno (Arte técnico), 6-nov-2026.
Acentos de Lucía: munición latón y ámbar, combustible oliva, materiales gris acero,
víveres ocre y beige, población hueso. Sin rojo, sin texto, sin marco, contorno de 1 px."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import Sprite, check, contact_sheet, reduce_16  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_a1")


def municion():
    s = Sprite()
    # Cartuchos de latón asomando de la caja (detrás de la tapa frontal).
    for x in (7, 11, 15, 19, 23):
        s.rect(x, 7, 3, 9, "amber")          # vaina
        s.vline(x, 7, 15, "cream")           # brillo a la izquierda
        s.vline(x + 2, 7, 15, "ochre_dark")  # sombra a la derecha
        s.rect(x, 4, 3, 3, "ochre_dark")     # punta (plomo/cobre apagado)
        s.set(x + 1, 3, "ochre_dark")
        s.set(x, 4, "tan")
        s.hline(x, x + 2, 9, "ochre_dark")   # ranura de la vaina
    # Caja de munición militar (lata) en verde oliva.
    s.rect(4, 14, 24, 2, "olive_light")      # borde superior iluminado
    s.rect(4, 16, 24, 13, "olive")           # cara frontal
    s.rect(4, 26, 24, 3, "olive_dark")       # base en sombra
    s.vline(27, 16, 28, "olive_dark")        # canto derecho en sombra
    s.vline(4, 16, 25, "olive_light")        # canto izquierdo iluminado
    # Asa central y cierres laterales.
    s.rect(13, 18, 6, 2, "olive_shadow")
    s.hline(13, 18, 17, "olive_black")
    s.rect(6, 19, 2, 4, "olive_shadow")
    s.rect(24, 19, 2, 4, "olive_shadow")
    # Franja de marcado (latón) sin texto.
    s.hline(9, 22, 23, "ochre_dark")
    s.outline()
    return s


def combustible():
    s = Sprite()
    # Cuerpo del bidón (jerrycan) con esquinas redondeadas.
    s.rect(8, 9, 17, 20, "olive")
    for (x, y) in ((8, 9), (24, 9), (8, 28), (24, 28)):
        s.set(x, y, None)
    # Asa triple arriba a la izquierda y boca con tapón a la derecha.
    s.rect(9, 5, 9, 4, "olive_dark")
    s.rect(10, 6, 2, 2, None)
    s.rect(13, 6, 2, 2, None)
    s.rect(16, 6, 1, 2, None)
    s.hline(9, 17, 5, "olive")
    s.rect(19, 5, 4, 4, "olive_dark")         # boca
    s.rect(19, 4, 4, 1, "olive_shadow")       # tapón
    s.set(22, 6, "olive")
    # Marco en relieve y la X estampada típica del bidón.
    s.rect(10, 11, 13, 16, "olive_dark")
    s.rect(11, 12, 11, 14, "olive")
    s.line(11, 12, 21, 25, "olive_dark")
    s.line(21, 12, 11, 25, "olive_dark")
    s.line(12, 12, 21, 24, "olive_light")
    s.line(20, 12, 11, 24, "olive_light")
    # Luz desde arriba a la izquierda.
    s.vline(9, 10, 27, "olive_light")
    s.hline(9, 23, 10, "olive_light")
    s.vline(24, 10, 27, "olive_dark")
    s.hline(9, 23, 28, "olive_dark")
    s.outline()
    return s


def materiales():
    s = Sprite()
    # Tablones (abajo), escalonados, en madera apagada.
    for k, (x0, x1, y) in enumerate(((3, 27, 23), (5, 28, 19))):
        s.rect(x0, y, x1 - x0 + 1, 4, "ochre_dark")
        s.hline(x0, x1, y, "tan")
        s.hline(x0, x1, y + 3, "brown")
        s.rect(x0, y, 2, 4, "tan")            # testa del tablón
        s.set(x0 + 1, y + 1, "ochre_dark")
        s.set(x0 + 1, y + 2, "brown")
        s.vline(x0 + 9 + k * 4, y + 1, y + 2, "brown")   # junta
    # Viga de acero en I encima, vista de frente con el perfil en la testa izquierda.
    s.rect(9, 10, 20, 2, "concrete_light")    # ala superior
    s.rect(9, 12, 20, 4, "concrete")          # alma
    s.rect(9, 16, 20, 2, "slate")             # ala inferior
    s.hline(9, 28, 10, "grey_light")
    s.hline(9, 28, 13, "slate")
    # Perfil en I en la testa.
    s.grid(3, 9, """
        AAAAAAA
        BBBBBBB
        ..CBC..
        ..CBC..
        ..CBC..
        ..CBC..
        BBBBBBB
        CCCCCCC
    """, {"A": "grey_light", "B": "concrete_light", "C": "slate"})
    # Óxido leve en el ala inferior (causa: humedad), sin ruido gratuito.
    s.set(14, 17, "rust")
    s.set(22, 17, "rust")
    s.outline()
    return s


def viveres():
    s = Sprite()
    # Caja de raciones de cartón en 3/4.
    s.poly([(2, 12), (6, 8), (21, 8), (17, 12)], "cream")   # tapa
    s.rect(2, 12, 16, 16, "beige")                           # frente
    s.poly([(17, 12), (21, 8), (21, 24), (17, 27)], "tan")  # lateral
    s.hline(2, 17, 12, "cream")
    s.hline(2, 17, 27, "tan")
    # Cinta de embalar ocre por el centro.
    s.line(9, 12, 13, 8, "ochre_dark")
    s.line(10, 12, 14, 8, "ochre_dark")
    s.rect(9, 12, 2, 16, "ochre_dark")
    s.hline(3, 8, 20, "tan")      # pliegue del cartón
    s.hline(12, 16, 20, "tan")
    # Cantimplora de aluminio delante, a la derecha, con cuello, tapón y cadenita.
    s.ellipse(17, 15, 29, 29, "concrete")
    s.ellipse(18, 16, 27, 27, "concrete_light", only="opaque")
    s.ellipse(19, 17, 22, 21, "grey_light", only="opaque")
    s.rect(21, 12, 5, 4, "concrete")               # cuello
    s.rect(20, 10, 7, 3, "slate")                  # tapón
    s.hline(20, 26, 10, "concrete")
    s.vline(26, 12, 15, "slate")
    # Funda de lona ocre en la mitad inferior (acento de víveres).
    s.rect(17, 23, 13, 7, "ochre_dark", only="opaque")
    s.hline(17, 29, 23, "tan", only="opaque")
    s.vline(23, 24, 29, "brown", only="opaque")
    s.outline()
    return s


def poblacion():
    s = Sprite()
    # Adulto (izquierda) con mochila; niño (derecha) de la mano. Sin rostros.
    # Ropa beige, pantalón oliva grisáceo, cabeza y manos hueso.
    s.rect(3, 11, 4, 9, "olive_grey")              # mochila
    s.vline(3, 12, 18, "grey_brown")
    s.hline(4, 6, 11, "olive_light")
    s.ellipse(8, 2, 13, 7, "bone")                 # cabeza
    s.hline(9, 12, 2, "cream")
    s.rect(10, 8, 2, 1, "beige")                   # cuello
    s.poly([(6, 9), (15, 9), (15, 19), (6, 19)], "beige")   # chaqueta
    s.vline(6, 9, 19, "tan")
    s.vline(14, 10, 19, "tan")                     # sombra lateral
    s.hline(6, 15, 19, "ochre_dark")               # cinturón
    s.rect(5, 10, 2, 9, "tan")                     # brazo izquierdo (colgando)
    s.rect(5, 19, 2, 2, "bone")                    # mano
    s.rect(7, 20, 3, 9, "olive_grey")              # piernas
    s.rect(11, 20, 3, 9, "olive_grey")
    s.vline(9, 20, 28, "grey_brown")
    s.vline(13, 20, 28, "grey_brown")
    s.rect(6, 29, 4, 1, "grey_brown")              # botas
    s.rect(11, 29, 4, 1, "grey_brown")
    # Brazo derecho del adulto hacia la mano del niño.
    s.line(15, 10, 18, 16, "beige")
    s.line(16, 10, 19, 16, "tan")
    s.rect(18, 17, 2, 2, "bone")
    # Niño.
    s.ellipse(20, 10, 25, 15, "bone")
    s.hline(21, 24, 10, "cream")
    s.poly([(19, 16), (25, 16), (25, 23), (19, 23)], "beige")
    s.vline(25, 16, 23, "tan")
    s.rect(26, 17, 1, 5, "tan")                    # brazo
    s.rect(26, 22, 1, 1, "bone")
    s.rect(20, 24, 2, 5, "olive_grey")
    s.rect(23, 24, 2, 5, "olive_grey")
    s.rect(19, 29, 3, 1, "grey_brown")
    s.rect(23, 29, 3, 1, "grey_brown")
    s.outline()
    return s


POB16 = """
....aa..........
...aaaa.........
...aaaa.........
..mbbbbc........
.mmbbbbc..aa....
.mmbbbbcdaaaa...
.mmbbbbc..dd....
..bbbbbc.bbbc...
..beeeec.bbbc...
...gg.gg.bbbc...
...gg.gg..g.g...
...gg.gg..g.g...
...gg.gg..g.g...
..hhh.hhh.h.h...
"""


def poblacion_16():
    s = Sprite(16, 16)
    s.grid(0, 1, POB16, {"a": "bone", "b": "beige", "c": "tan", "d": "bone", "e": "ochre_dark",
                         "g": "olive_grey", "h": "grey_brown", "m": "olive_grey"})
    s.outline()
    return s


if __name__ == "__main__":
    nombres = ["municion", "combustible", "materiales", "viveres", "poblacion"]
    s32 = [globals()[n]() for n in nombres]
    s16 = [reduce_16(s) for s in s32]
    s16[4] = poblacion_16()  # redibujado a mano: la reducción no se lee
    for n, a, b in zip(nombres, s32, s16):
        a.save(OUT / f"vm_cargas_{n}_32.png")
        b.save(OUT / f"vm_cargas_{n}_16.png")
        print(n, "32:", check(a, recurso=True) or "OK", "| 16:", check(b, recurso=True) or "OK")
    contact_sheet([s32, s16, [s.silhouette() for s in s32]], OUT / "contacto_a1.png", scale=6,
                  labels=["32 px", "16 px", "silueta"])
