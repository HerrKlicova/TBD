"""A6 · Iconos de unidad (32 px y 16 px) por código. Bruno (Arte técnico), 6-nov-2026.
Familia A1–A5: luz arriba a la izquierda, contorno de 1 px, oliva militar y 1–2 px de desgaste.
Sin calaveras ni aire de dibujo animado (los de Pixelab se descartaron). Forma de la artillería tomada
de las variantes v2 y v5 de Pixelab: tubo grueso inclinado arriba a la derecha con boca marcada y escudo."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import Sprite, check, contact_sheet, reduce_16  # noqa: E402
import build_a5  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_a6")


def soldado(s, x, y, fusil=True):
    """Soldado actual de 9x17 de perfil hacia la derecha: casco moderno redondeado (sin ala),
    chaleco con cargadores, fusil de asalto negro; detalle en hueso (luz del casco y parche)."""
    s.ellipse(x + 2, y, x + 7, y + 4, "olive")       # casco moderno, cúpula
    s.hline(x + 3, x + 5, y, "bone")                  # luz del casco
    s.set(x + 2, y + 2, "olive_light")
    s.hline(x + 2, x + 7, y + 4, "olive_dark")        # borde del casco
    s.rect(x + 4, y + 5, 3, 2, "tan")                 # cara
    s.set(x + 6, y + 5, "rose_brown")
    s.rect(x + 2, y + 7, 5, 5, "olive_grey")          # chaleco
    s.vline(x + 2, y + 7, y + 11, "olive_light")
    s.vline(x + 6, y + 8, y + 11, "grey_brown")
    s.rect(x + 3, y + 9, 3, 2, "olive_dark")          # cargadores
    s.set(x + 3, y + 7, "bone")                       # parche de unidad
    s.rect(x, y + 7, 2, 4, "olive_dark")              # mochila
    s.rect(x + 2, y + 12, 2, 4, "grey_brown")         # piernas
    s.rect(x + 5, y + 12, 2, 4, "grey_brown")
    s.hline(x + 1, x + 3, y + 16, "brown_dark")       # botas
    s.hline(x + 5, x + 7, y + 16, "brown_dark")
    if fusil:
        # Fusil de asalto en posición de marcha, cañón hacia abajo y adelante.
        s.line(x + 4, y + 8, x + 10, y + 12, "slate_dark")
        s.line(x + 5, y + 8, x + 10, y + 11, "slate")
        s.set(x + 6, y + 11, "slate_dark")            # cargador


def fusileros():
    s = Sprite()
    # Escuadra de tres, escalonados en profundidad (los de atrás un tono más oscuros).
    soldado(s, 0, 5)
    soldado(s, 20, 6)
    for (a, b) in (("olive_grey", "grey_brown"), ("olive", "olive_dark"), ("tan", "rose_brown"),
                   ("olive_light", "olive"), ("bone", "beige"), ("slate", "slate_dark")):
        s.replace(a, b)                                # retaguardia en sombra
    soldado(s, 10, 13)                                 # el de delante, en primer plano
    s.outline()
    return s


def ametralladora():
    s = Sprite()
    # Tirador tumbado detrás de una ametralladora sobre bípode, cinta de cartuchos a un lado.
    # Cuerpo tendido (de izquierda a derecha: botas, piernas, torso, cabeza).
    s.rect(1, 21, 4, 2, "brown_dark")                  # botas
    s.rect(4, 20, 7, 3, "grey_brown")                  # piernas
    s.hline(4, 10, 20, "olive_grey")
    s.rect(10, 18, 8, 5, "olive_grey")                 # torso con chaleco
    s.hline(10, 17, 18, "olive_light")
    s.rect(12, 20, 3, 2, "olive_dark")                 # cargadores
    s.set(11, 19, "bone")                              # parche
    s.rect(9, 16, 5, 2, "olive_dark")                  # mochila sobre la espalda
    s.ellipse(17, 14, 22, 18, "olive")                 # casco
    s.hline(18, 20, 14, "bone")
    s.hline(17, 22, 18, "olive_dark")
    s.rect(20, 19, 2, 2, "tan")                        # cara pegada a la culata
    s.rect(17, 21, 3, 2, "olive_grey")                 # brazo al guardamanos
    # Ametralladora: culata, cajón, cañón largo y bípode.
    s.rect(18, 19, 3, 2, "slate_dark")                 # culata
    s.rect(21, 18, 5, 3, "slate_dark")                 # cajón de mecanismos
    s.hline(21, 25, 18, "slate")
    s.hline(26, 31, 19, "slate")                       # cañón
    s.hline(26, 31, 20, "slate_dark")
    s.line(27, 21, 25, 24, "slate")                    # bípode
    s.line(28, 21, 30, 24, "slate")
    s.line(23, 21, 21, 24, "amber")                    # cinta de cartuchos colgando
    s.set(22, 22, "ochre_dark")
    s.hline(0, 31, 25, "olive_shadow")                 # suelo
    s.set(6, 21, "olive_light")                        # rodillera gastada
    s.outline()
    return s


def blindado_8x8():
    s = Sprite()
    # Blindado de ruedas 8x8 de perfil hacia la derecha: casco en cuña, torreta pequeña, cuatro ruedas.
    s.poly([(1, 13), (24, 13), (30, 18), (29, 22), (2, 22)], "olive")
    s.hline(1, 24, 13, "olive_light")
    s.line(24, 13, 30, 18, "olive_light")
    s.hline(2, 29, 22, "olive_dark")
    s.vline(1, 14, 21, "olive_light")
    s.hline(3, 27, 17, "olive_dark")                    # arista lateral
    s.rect(18, 15, 2, 1, "olive_black")                 # visor del conductor
    s.rect(5, 15, 6, 2, "olive_dark")                   # escotilla de tropa
    s.hline(5, 10, 15, "olive")
    # Torreta y cañón.
    s.rect(9, 9, 9, 4, "olive")
    s.hline(9, 17, 9, "olive_light")
    s.vline(17, 10, 12, "olive_dark")
    s.rect(18, 10, 11, 2, "slate")
    s.hline(18, 28, 10, "concrete_light")
    s.rect(11, 7, 3, 2, "olive_dark")                   # periscopio
    # Ruedas.
    for x in (6, 12, 19, 25):
        s.ellipse(x - 3, 20, x + 2, 25, "slate_dark")
        s.ellipse(x - 2, 21, x + 1, 24, "slate")
        s.set(x - 1, 22, "concrete_light")
    s.set(22, 19, "concrete")                           # pintura saltada
    s.set(4, 20, "rust")
    s.outline()
    return s


def tren_blindado():
    s = Sprite()
    build_a5.tren(s, 15)
    # Torreta con cañón sobre el vagón: lo distingue del marcador de mapa.
    s.rect(3, 11, 8, 6, "olive")
    s.hline(3, 10, 11, "olive_light")
    s.vline(10, 12, 16, "olive_dark")
    s.rect(10, 12, 9, 2, "slate")
    s.hline(10, 18, 12, "concrete_light")
    s.rect(5, 9, 3, 2, "olive_dark")                    # cúpula
    s.outline()
    return s


def artilleria_ferroviaria():
    s = Sprite()
    # Plataforma ferroviaria con obús de tubo grueso inclinado arriba a la derecha (como las v2 y v5).
    s.rect(1, 21, 30, 3, "olive_dark")                  # plataforma
    s.hline(1, 30, 21, "olive")
    s.rect(2, 24, 28, 1, "slate_dark")
    for x in (4, 8, 23, 27):                            # bogies
        s.ellipse(x - 1, 25, x + 1, 27, "slate")
        s.set(x, 26, "concrete_light")
    s.hline(0, 31, 28, "concrete")                      # carril
    # Afuste y escudo.
    s.poly([(7, 21), (11, 13), (19, 13), (22, 21)], "olive")
    s.line(7, 21, 11, 13, "olive_light")
    s.hline(11, 19, 13, "olive_light")
    s.line(19, 13, 22, 21, "olive_dark")
    s.rect(12, 16, 6, 2, "olive_dark")                  # mecanismo de elevación
    # Tubo grueso con boca marcada.
    for k in range(4):
        s.line(13, 13 - k + 2, 26, 3 - k + 2, "slate" if k else "slate_dark")
    s.line(13, 12, 26, 2, "concrete_light")
    s.rect(25, 1, 4, 5, "slate")                        # boca (freno de boca)
    s.hline(25, 28, 1, "concrete_light")
    s.vline(28, 2, 5, "slate_dark")
    s.rect(12, 11, 4, 4, "slate_dark")                  # recámara
    s.set(15, 18, "concrete")                           # desgaste
    s.set(9, 22, "rust")
    s.outline()
    return s


if __name__ == "__main__":
    nombres = ["fusileros", "ametralladora", "blindado_8x8", "tren_blindado", "artilleria_ferroviaria"]
    s32 = [globals()[n]() for n in nombres]
    s16 = [reduce_16(s) for s in s32]
    for n, a, b in zip(nombres, s32, s16):
        a.save(OUT / f"vm_unidades_{n}_32.png")
        b.save(OUT / f"vm_unidades_{n}_16.png")
        print(n, "32:", check(a, recurso=True) or "OK", "| 16:", check(b, recurso=True) or "OK")
    contact_sheet([s32, s16, [s.silhouette() for s in s32]], OUT / "contacto_a6.png", scale=6,
                  labels=["32 px", "16 px", "silueta"])
