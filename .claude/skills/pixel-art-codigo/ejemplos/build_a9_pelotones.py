"""A9 · Iconos de los tipos de pelotón (32 y 16 px) para el prototipo. Bruno (Arte técnico), 6-nov-2026 (noche).

Eugenio (23:03): «la infantería va por pelotones como fusileros, ametralladores, granaderos… que haya variedad».
Misma familia que A6 (fusileros, nido de ametralladora, blindado 8x8): el soldado de 9x17 de A6, luz arriba
a la izquierda, contorno de 1 px y 6–10 colores. Cada tipo se reconoce por UN objeto grande a la derecha
(el arma o la herramienta), porque a 16 px el soldado solo ya no se distingue.
  ametralladores · granaderos · tiradores · zapadores · lanzallamas · sanitarios · helicoptero
La cruz del sanitario va en hueso sobre oliva: nada rojo (el rojo es del ataque).
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import Sprite, check, contact_sheet, reduce_16  # noqa: E402
from build_a6 import soldado  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_a9")

SOMBRA = (("olive_grey", "grey_brown"), ("olive", "olive_dark"), ("tan", "rose_brown"),
          ("olive_light", "olive"), ("bone", "beige"), ("slate", "slate_dark"))


def _pareja(s, armado=False):
    """Un soldado de retaguardia en sombra (izquierda) y el de delante, más grande en la composición."""
    soldado(s, 0, 4, fusil=True)
    for a, b in SOMBRA:
        s.replace(a, b)
    soldado(s, 6, 12, fusil=armado)


def ametralladores():
    s = Sprite()
    _pareja(s)
    # Ametralladora ligera con bípode, terciada por delante del soldado; cinta de latón colgando.
    s.line(9, 20, 30, 15, "slate_dark")
    s.line(9, 19, 30, 14, "slate")
    s.rect(12, 18, 6, 3, "slate_dark")                  # cajón de mecanismos
    s.hline(12, 17, 18, "concrete")
    s.rect(28, 13, 3, 3, "slate_dark")                  # bocacha
    s.line(25, 16, 23, 22, "slate_dark")                # bípode
    s.line(26, 16, 28, 22, "slate_dark")
    s.rect(16, 24, 8, 5, "olive_yellow")                # caja de munición
    s.hline(16, 23, 24, "beige")
    s.vline(23, 25, 28, "olive_grey")
    for j in range(5):                                  # cinta
        s.set(14 + (j // 2), 21 + j, "amber" if j % 2 == 0 else "ochre_dark")
    s.outline()
    return s


def granaderos():
    s = Sprite()
    _pareja(s, armado=True)
    # Granada de mano grande arriba a la derecha (cuerpo oliva, palanca y anilla en hueso) y su arco.
    s.ellipse(20, 6, 29, 17, "olive")
    s.ellipse(21, 7, 25, 11, "olive_light")
    s.set(22, 8, "bone")
    for y in (10, 13):                                  # estrías del cuerpo
        s.hline(21, 28, y, "olive_dark")
    s.vline(28, 9, 15, "olive_dark")
    s.rect(23, 3, 4, 3, "concrete")                     # espoleta
    s.hline(23, 26, 3, "concrete_light")
    s.line(27, 4, 30, 8, "bone")                        # palanca
    s.ellipse(19, 1, 22, 4, "bone")                     # anilla
    s.ellipse(20, 2, 21, 3, None)
    for k, (x, y) in enumerate(((13, 12), (15, 9), (17, 7))):   # arco del lanzamiento, punteado
        s.set(x, y, "beige")
    s.outline()
    return s


def tiradores():
    s = Sprite()
    _pareja(s)
    # Fusil de precisión largo, casi horizontal, con mira telescópica grande y bípode.
    s.line(8, 21, 31, 17, "slate_dark")
    s.line(8, 20, 31, 16, "slate")
    s.rect(6, 20, 5, 3, "brown")                        # culata
    s.hline(6, 10, 20, "ochre_dark")
    s.rect(13, 15, 8, 3, "slate_dark")                  # mira
    s.hline(13, 20, 15, "concrete")
    s.rect(20, 14, 2, 4, "slate_dark")                  # objetivo
    s.set(21, 15, "concrete_light")                     # destello de la lente
    s.vline(15, 18, 19, "slate_dark")                   # montura
    s.vline(18, 18, 19, "slate_dark")
    s.rect(13, 13, 9, 2, "slate_dark")                  # mira más alta: es lo que lo distingue a 16 px
    s.rect(21, 12, 3, 5, "slate_dark")
    s.set(22, 13, "bone")
    s.outline()
    return s


def zapadores():
    s = Sprite()
    _pareja(s, armado=True)
    # Pala de campaña en diagonal y carga de demolición con el cable.
    s.line(18, 4, 27, 19, "brown")                      # mango
    s.line(19, 4, 28, 19, "ochre_dark")
    s.rect(16, 2, 5, 2, "brown")                        # empuñadura en T
    s.poly([(26, 18), (31, 21), (29, 27), (24, 24)], "concrete")   # hoja
    s.line(26, 18, 31, 21, "concrete_light")
    s.line(29, 27, 24, 24, "grey_brown")
    s.rect(18, 23, 7, 5, "olive_yellow")                # carga (bloque caqui)
    s.hline(18, 24, 23, "beige")
    s.vline(24, 24, 27, "olive_grey")
    s.hline(18, 24, 25, "olive_dark")                   # cinta
    s.line(17, 24, 13, 28, "slate_dark")                # cable
    s.outline()
    return s


def lanzallamas():
    s = Sprite()
    soldado(s, 0, 4, fusil=True)
    for a, b in SOMBRA:
        s.replace(a, b)
    # Botellas a la espalda del de delante (antes de dibujarlo, para que queden detrás).
    s.rect(4, 13, 3, 8, "olive_dark")
    s.rect(6, 12, 3, 9, "olive")
    s.vline(6, 13, 19, "olive_light")
    s.set(7, 11, "concrete")
    soldado(s, 7, 12, fusil=False)
    # Lanza y llama (los colores del fuego de la fase D: crema, ámbar y ocre).
    s.line(12, 21, 20, 18, "slate_dark")
    s.line(12, 20, 20, 17, "slate")
    s.rect(20, 16, 2, 3, "slate_dark")
    for x in range(22, 32):
        t = (x - 22) / 9
        semi = 1 + t * 4
        for y in range(int(17 - semi), int(17 + semi) + 1):
            d = abs(y + .5 - 17.5) / semi
            v = (1 - t * .6) * (1 - d)
            s.set(x, y - 1, "cream" if v > .55 else "amber" if v > .25 else "ochre_dark")
    s.outline()
    return s


def sanitarios():
    s = Sprite()
    _pareja(s, armado=True)
    # Bolsa sanitaria grande con cruz en hueso (nunca roja) y asa.
    s.rect(18, 9, 13, 11, "olive_grey")
    s.hline(18, 30, 9, "olive_light")
    s.vline(18, 10, 19, "olive_light")
    s.vline(30, 10, 19, "grey_brown")
    s.hline(18, 30, 19, "grey_brown")
    s.rect(22, 11, 5, 7, "bone")                        # cruz
    s.rect(20, 13, 9, 3, "bone")
    s.rect(22, 6, 5, 1, "olive_dark")                   # asa
    s.vline(21, 6, 8, "olive_dark")
    s.vline(27, 6, 8, "olive_dark")
    s.set(6 + 3, 12 + 8, "bone")                        # brazalete del soldado
    s.outline()
    return s


def nh90():
    """NH90 de perfil hacia la derecha (plan §4: 50541c con el techo en b4a080): morro redondo, fuselaje alto
    con la puerta corredera, joroba de los motores, botalón grueso que sube hacia la deriva y rotor de 2 px."""
    s = Sprite()
    s.rect(1, 4, 30, 2, "concrete_light")               # pala del rotor principal
    s.hline(5, 26, 6, "concrete")
    s.rect(14, 6, 3, 2, "slate_dark")                   # mástil
    s.poly([(9, 8), (22, 8), (25, 9), (25, 11), (11, 11)], "beige")   # joroba de motores, techo claro
    s.hline(9, 22, 8, "bone")
    s.poly([(8, 11), (25, 11), (29, 14), (30, 18), (28, 21), (9, 21), (7, 17)], "olive_dark")   # fuselaje
    s.hline(9, 25, 11, "olive")
    s.line(25, 11, 29, 14, "olive")
    s.hline(9, 28, 21, "olive_shadow")
    s.poly([(24, 12), (27, 12), (29, 15), (24, 16)], "concrete_light")   # parabrisas
    s.line(27, 12, 29, 15, "bone")
    s.rect(13, 13, 7, 6, "olive_shadow")                # puerta corredera abierta
    s.vline(13, 13, 18, "olive_grey")
    s.set(19, 15, "slate_dark")                         # ametrallador de puerta
    s.hline(20, 22, 15, "slate_dark")
    s.poly([(0, 9), (8, 13), (8, 17), (2, 13)], "olive_dark")   # botalón que sube
    s.line(0, 9, 8, 13, "olive")
    s.rect(0, 5, 3, 6, "olive_dark")                    # deriva
    s.vline(3, 5, 10, "concrete")                       # rotor de cola
    for x in (12, 25):                                  # tren de ruedas (el NH90 no lleva patines)
        s.rect(x, 22, 3, 3, "slate_dark")
        s.set(x + 1, 23, "concrete")
    s.vline(13, 21, 22, "slate_dark")
    s.set(10, 18, "olive")                              # desgaste
    s.outline()
    return s


# Los 4 pelotones nuevos del plan de la noche (fusileros y el Dragón son de A6) y el NH90.
# lanzallamas() y sanitarios() quedan hechos para cuando entren en el juego.
NOMBRES = ["ametralladores", "granaderos", "tiradores", "zapadores", "nh90"]


def iconos():
    s32 = {n: globals()[n]() for n in NOMBRES}
    for s in s32.values():
        s.limit_colors(10)
    s16 = {n: reduce_16(s) for n, s in s32.items()}
    return s32, s16


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    s32, s16 = iconos()
    for n in NOMBRES:
        s32[n].save(OUT / f"vm_unidades_{n}_32.png")
        s16[n].save(OUT / f"vm_unidades_{n}_16.png")
        print(n, "32:", check(s32[n], recurso=True) or "OK", "| 16:", check(s16[n], recurso=True) or "OK")
    contact_sheet([list(s32.values()), list(s16.values()), [s.silhouette() for s in s32.values()]],
                  OUT / "contacto_a9.png", scale=6, labels=["32 px", "16 px", "silueta"])
