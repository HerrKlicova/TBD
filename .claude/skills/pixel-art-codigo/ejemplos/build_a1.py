"""A1 · Cargas (32 px y 16 px) dibujadas por código. Bruno (Arte técnico), 6-nov-2026.
Acentos de Lucía: munición latón y ámbar, combustible oliva, materiales gris acero,
víveres ocre y beige, población hueso (en la ropa).
Revisión de Lucía 6-nov: luz fija arriba-izquierda, desgaste, munición en acero, víveres saco y latas. Sin rojo, sin texto, sin marco, contorno de 1 px."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import Sprite, check, contact_sheet, reduce_16  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_a1")


def municion():
    """Lata de munición de acero oscuro en 3/4, abierta, con cartuchos de latón.
    El acero va apagado para que mande el latón (Lucía, 6-nov)."""
    s = Sprite()
    # Cartuchos (detrás del borde frontal).
    for x in (6, 10, 14, 18):
        s.rect(x, 6, 3, 9, "amber")
        s.vline(x, 6, 14, "cream")
        s.vline(x + 2, 6, 14, "ochre_dark")
        s.rect(x, 3, 3, 3, "ochre_dark")      # punta
        s.set(x + 1, 2, "ochre_dark")
        s.set(x, 3, "tan")
        s.hline(x, x + 2, 8, "ochre_dark")    # ranura
    s.set(19, 6, "ochre_dark")                 # una vaina con un golpe
    # Cara superior (borde de la abertura) y laterales.
    s.poly([(3, 13), (6, 10), (25, 10), (22, 13)], "slate")
    s.rect(6, 11, 16, 2, "slate_dark")         # interior en sombra, detrás de los cartuchos
    for x in (6, 10, 14, 18):
        s.rect(x, 10, 3, 3, "amber")
        s.vline(x, 10, 12, "cream")
        s.vline(x + 2, 10, 12, "ochre_dark")
    s.hline(4, 22, 13, "concrete")             # filo frontal iluminado
    s.rect(3, 14, 20, 15, "slate")             # cara frontal
    s.vline(3, 14, 28, "concrete")             # canto izquierdo iluminado
    s.poly([(23, 13), (26, 10), (26, 25), (23, 28)], "slate_dark")   # lateral en sombra
    s.vline(23, 13, 28, "slate_dark")
    s.hline(3, 22, 28, "slate_dark")           # base en sombra
    # Asa, cierre y refuerzos.
    s.rect(9, 16, 7, 2, "slate_dark")
    s.hline(9, 15, 15, "concrete")
    s.rect(4, 18, 2, 5, "slate_dark")
    s.vline(4, 18, 22, "concrete")
    s.hline(4, 22, 24, "slate_dark")           # nervio inferior
    s.hline(4, 22, 25, "concrete")
    # Desgaste: pintura saltada en esquinas y un golpe con óxido.
    s.set(21, 14, "concrete_light")
    s.set(22, 15, "concrete_light")
    s.set(4, 27, "concrete_light")
    s.set(17, 20, "slate_dark")
    s.set(18, 21, "rust")
    s.set(18, 22, "rust")
    s.outline()
    return s


def combustible():
    s = Sprite()
    # Lateral en sombra (3/4) y cuerpo.
    s.poly([(23, 9), (26, 7), (26, 26), (23, 29)], "olive_shadow")
    s.rect(7, 9, 17, 20, "olive")
    for (x, y) in ((7, 9), (7, 28)):
        s.set(x, y, None)
    # Asa triple y boca con tapón.
    s.rect(8, 5, 9, 4, "olive_dark")
    s.rect(9, 6, 2, 2, None)
    s.rect(12, 6, 2, 2, None)
    s.rect(15, 6, 1, 2, None)
    s.hline(8, 16, 5, "olive_light")
    s.rect(18, 5, 4, 4, "olive_dark")
    s.rect(18, 4, 4, 1, "olive_shadow")
    s.set(18, 5, "olive_light")
    # Marco en relieve y X estampada, con luz arriba a la izquierda.
    s.rect(9, 11, 13, 16, "olive_dark")
    s.rect(10, 12, 11, 14, "olive")
    s.line(10, 12, 20, 25, "olive_dark")
    s.line(20, 12, 10, 25, "olive_dark")
    s.line(11, 12, 20, 24, "olive_light")
    s.line(19, 12, 10, 24, "olive_light")
    s.vline(8, 10, 27, "olive_light")
    s.hline(8, 22, 10, "olive_light")
    s.vline(23, 10, 28, "olive_dark")
    s.hline(8, 22, 28, "olive_dark")
    # Desgaste: pintura saltada (se ve el acero) y una abolladura.
    s.set(8, 27, "concrete")
    s.set(9, 28, "concrete")
    s.set(22, 10, "concrete")
    s.set(25, 12, "olive_dark")
    s.set(13, 19, "olive_dark")
    s.set(14, 19, "olive_light")
    s.outline()
    return s


def materiales():
    s = Sprite()
    # Tablones (abajo), escalonados.
    for k, (x0, x1, y) in enumerate(((3, 27, 23), (5, 28, 19))):
        s.rect(x0, y, x1 - x0 + 1, 4, "ochre_dark")
        s.hline(x0, x1, y, "tan")
        s.hline(x0, x1, y + 3, "brown")
        s.rect(x0, y, 2, 4, "tan")            # testa
        s.set(x0 + 1, y + 1, "ochre_dark")
        s.set(x0 + 1, y + 2, "brown")
        s.vline(x0 + 9 + k * 4, y + 1, y + 2, "brown")   # junta
        s.hline(x0 + 13, x0 + 17, y + 2, "brown")       # veta
    s.set(24, 24, "slate")                     # clavo
    # Viga en I: ala superior iluminada, alma en sombra bajo el ala, ala inferior oscura.
    s.rect(9, 10, 20, 2, "concrete_light")
    s.rect(9, 12, 20, 4, "concrete")
    s.hline(9, 28, 12, "slate")                # sombra del ala sobre el alma
    s.rect(9, 16, 20, 2, "slate")
    s.hline(9, 28, 10, "grey_light")
    s.hline(9, 28, 17, "slate_dark")
    s.grid(3, 9, """
        AAAAAAA
        BBBBBBB
        ..CBD..
        ..CBD..
        ..CBD..
        ..CBD..
        BBBBBBB
        DDDDDDD
    """, {"A": "grey_light", "B": "concrete_light", "C": "slate", "D": "slate_dark"})
    # Desgaste: chorreón de óxido bajo un remache y una muesca en el ala.
    s.set(16, 13, "slate_dark")
    s.set(16, 14, "rust")
    s.set(16, 15, "rust")
    s.set(17, 16, "rust")
    s.set(24, 10, "concrete")
    s.outline()
    return s


def viveres():
    """Saco de harina atado y dos latas apiladas (Lucía, 6-nov)."""
    s = Sprite()
    # Saco: cuerpo ancho y abombado, cuello corto atado con las puntas abiertas.
    s.ellipse(2, 9, 20, 30, "beige")
    s.rect(3, 22, 17, 7, "beige")
    s.rect(8, 6, 6, 4, "beige")                                  # cuello
    s.poly([(5, 2), (10, 5), (12, 5), (17, 2), (15, 6), (7, 6)], "beige")  # puntas
    s.hline(6, 9, 3, "bone")
    s.hline(7, 15, 6, "ochre_dark")                              # cuerda
    s.set(16, 7, "ochre_dark")
    s.set(16, 8, "ochre_dark")
    # Luz arriba a la izquierda y sombra a la derecha y abajo.
    s.ellipse(4, 11, 12, 20, "bone", only="beige")
    s.vline(19, 15, 27, "tan")
    s.vline(18, 21, 28, "tan")
    s.hline(4, 18, 28, "tan")
    s.hline(6, 17, 29, "tan", only="opaque")
    s.set(9, 8, "tan")                                           # frunce del cuello
    s.set(12, 8, "tan")
    s.line(5, 17, 8, 15, "tan")                                  # pliegue
    # Franja ocre impresa (sin texto) y un remiendo.
    s.rect(3, 20, 15, 2, "ochre_dark", only="opaque")
    s.rect(13, 24, 3, 2, "tan")
    s.set(13, 24, "beige")
    # Latas apiladas delante a la derecha: cilindros de acero con etiqueta ocre.
    for y0 in (21, 12):
        x0, w, h = 19, 9, 9
        s.rect(x0, y0, w, h, "concrete")
        s.vline(x0 + 1, y0 + 1, y0 + h - 2, "concrete_light")     # brillo del cilindro
        s.vline(x0 + w - 2, y0 + 1, y0 + h - 2, "slate")
        s.vline(x0 + w - 1, y0 + 1, y0 + h - 2, "slate")
        s.rect(x0, y0 + 3, w, 3, "ochre_dark")                    # etiqueta
        s.vline(x0 + w - 2, y0 + 3, y0 + 5, "brown")
        s.vline(x0 + w - 1, y0 + 3, y0 + 5, "brown")
        s.vline(x0 + 1, y0 + 3, y0 + 5, "tan")
        s.hline(x0 + 1, x0 + w - 2, y0, "grey_light")             # tapa (elipse)
        s.hline(x0 + 2, x0 + w - 3, y0 + 1, "concrete_light")
        for (x, y) in ((x0, y0), (x0 + w - 1, y0), (x0, y0 + h - 1), (x0 + w - 1, y0 + h - 1)):
            s.set(x, y, None)                                    # esquinas redondeadas
        s.hline(x0 + 1, x0 + w - 2, y0 + h - 1, "slate")
    s.set(25, 17, "slate")                                      # abolladura
    s.set(21, 27, "grey_light")                                 # arañazo
    s.outline()
    return s


def poblacion():
    """Adulto con mochila y niño de la mano. Pelo oscuro y piel de la paleta; el hueso
    queda en la ropa (Lucía, 6-nov). Sin rasgos faciales."""
    s = Sprite()
    s.rect(3, 11, 4, 9, "olive_grey")              # mochila
    s.vline(3, 12, 18, "grey_brown")
    s.hline(4, 6, 11, "olive_light")
    # Cabeza del adulto: pelo oscuro, cara en tono piel.
    s.ellipse(8, 2, 13, 7, "tan")
    s.rect(8, 2, 6, 2, "brown_dark")
    s.rect(8, 4, 1, 3, "brown_dark")
    s.set(13, 4, "brown_dark")
    s.vline(13, 5, 6, "rose_brown")                # sombra de la cara
    s.rect(10, 8, 2, 1, "rose_brown")              # cuello
    # Chaqueta hueso con luz y sombra.
    s.rect(6, 9, 9, 11, "bone")
    s.vline(6, 9, 19, "cream")
    s.vline(14, 10, 19, "beige")
    s.vline(13, 12, 18, "beige")
    s.hline(6, 14, 19, "ochre_dark")               # cinturón
    s.vline(10, 10, 18, "beige")                   # cremallera
    s.rect(5, 10, 2, 9, "beige")                   # brazo izquierdo
    s.rect(5, 19, 2, 2, "tan")                     # mano
    s.set(7, 15, "concrete")                       # roce en la manga
    s.rect(7, 20, 3, 9, "olive_grey")              # pantalón
    s.rect(11, 20, 3, 9, "olive_grey")
    s.vline(9, 20, 28, "grey_brown")
    s.vline(13, 20, 28, "grey_brown")
    s.rect(6, 29, 4, 1, "brown_dark")              # botas
    s.rect(11, 29, 4, 1, "brown_dark")
    s.set(8, 25, "olive_light")                    # rodillera gastada
    # Brazo derecho hacia la mano del niño.
    s.line(15, 10, 18, 16, "bone")
    s.line(16, 10, 19, 16, "beige")
    s.rect(18, 17, 2, 2, "tan")
    # Niño.
    s.ellipse(20, 10, 25, 15, "tan")
    s.rect(20, 10, 6, 2, "brown_dark")
    s.rect(20, 12, 1, 2, "brown_dark")
    s.vline(25, 12, 14, "rose_brown")
    s.rect(19, 16, 7, 8, "bone")
    s.vline(19, 16, 23, "cream")
    s.vline(25, 16, 23, "beige")
    s.rect(26, 17, 1, 5, "beige")
    s.set(26, 22, "tan")
    s.rect(20, 24, 2, 5, "olive_grey")
    s.rect(23, 24, 2, 5, "olive_grey")
    s.rect(19, 29, 3, 1, "brown_dark")
    s.rect(23, 29, 3, 1, "brown_dark")
    s.outline()
    return s


POB16 = """
...hhhh.........
...hssh.........
...hssr.........
..mbbbbc...hhh..
.mmbbbbc..hssr..
.mmbbbbcbt.ss...
.mmbbbbc..bbbc..
..beeeec..bbbc..
...gg.gg..bbbc..
...gg.gg..g.g...
...gg.gg..g.g...
...gg.gg..g.g...
..ddd.ddd.d.d...
"""


def poblacion_16():
    s = Sprite(16, 16)
    s.grid(0, 2, POB16, {"h": "brown_dark", "s": "tan", "r": "rose_brown", "b": "bone", "c": "beige",
                         "e": "ochre_dark", "g": "olive_grey", "d": "brown_dark", "m": "olive_grey",
                         "t": "tan"})
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
