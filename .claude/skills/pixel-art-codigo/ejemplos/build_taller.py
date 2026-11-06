"""Iconos del prototipo, 6-nov tarde (Mak). Bruno (Arte técnico).

Familia A1–A6 (luz arriba a la izquierda, contorno de 1 px, 6–10 colores, sin marco, 32 y 16 px):
  - puerta_via_rota: la puerta de vía (D-N49) reventada, hueco en el muro con escombros sobre la vía.
  - tren_nuevo (taller: fabricar): locomotora y vagón con una cruz de «nuevo» en hueso.
  - rehabilitar (taller: reparar un tren viejo): locomotora con una llave inglesa cruzada.
  - chispas: retrato-icono de Chispas Ferrer, maquinista veterano (60 años, bigote gris, gorra plana,
    gafas en la frente, mono de trabajo, cara manchada de grasa). Rasgos adultos, sin caricatura.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import Sprite, check, contact_sheet, reduce_16  # noqa: E402
import build_a5  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_taller")


def bloque_muro(s, x0, x1, y0, y1):
    """Muro de bloques de hormigón visto de frente, con juntas y luz arriba."""
    s.rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1, "concrete")
    s.hline(x0, x1, y0, "concrete_light")
    s.vline(x1, y0, y1, "slate")
    for y in range(y0 + 3, y1, 4):
        s.hline(x0, x1 - 1, y, "slate")
        off = 0 if (y // 4) % 2 else 2
        for x in range(x0 + off, x1, 4):
            s.vline(x, y - 3, y - 1, "slate")


def puerta_via_rota():
    s = Sprite()
    s.rect(9, 7, 14, 18, "olive_black")                # el hueco: oscuro, se lee como agujero
    s.hline(10, 21, 7, "olive_shadow")
    bloque_muro(s, 1, 9, 6, 24)                        # muro izquierdo
    bloque_muro(s, 22, 30, 6, 24)                      # muro derecho
    s.poly([(9, 6), (11, 9), (10, 13), (12, 17), (9, 20)], "concrete")   # borde roto izquierdo
    s.poly([(22, 8), (20, 11), (21, 15), (19, 18), (22, 21)], "concrete")
    s.set(10, 12, "slate")
    s.set(21, 14, "slate")
    # Vía que atraviesa el hueco (vista frontal en perspectiva: dos carriles hacia el fondo).
    s.rect(0, 25, 32, 2, "brown")                      # traviesa / balasto
    s.line(12, 18, 7, 27, "concrete_light")
    s.line(19, 18, 24, 27, "concrete_light")
    s.hline(10, 21, 22, "brown_dark")                  # traviesa del fondo
    # Escombros sobre la vía: cascotes de hormigón y una hoja de la puerta caída.
    s.poly([(9, 27), (13, 21), (17, 20), (23, 24), (24, 27)], "concrete")
    s.set(13, 22, "concrete_light")
    s.set(16, 21, "concrete_light")
    s.rect(14, 24, 3, 2, "slate")
    s.set(20, 23, "concrete_light")
    s.line(11, 26, 20, 19, "rust")                     # hoja de la puerta caída, chapa oxidada
    s.line(12, 26, 21, 19, "brown")
    s.set(25, 26, "slate")                             # cascote suelto
    s.set(6, 26, "concrete")
    s.outline()
    return s


def cruz(s, cx, cy):
    """Cruz de «nuevo» en hueso, 5x5 de trazo 1, como las órdenes (sin marco)."""
    s.hline(cx - 2, cx + 2, cy, "bone")
    s.vline(cx, cy - 2, cy + 2, "bone")


def tren_nuevo():
    s = Sprite()
    build_a5.tren(s, 15)
    s.outline()
    # Cruz de «nuevo» arriba a la izquierda, con su propio contorno.
    c = Sprite()
    c.rect(4, 2, 3, 9, "bone")
    c.rect(1, 5, 9, 3, "bone")
    c.vline(5, 2, 10, "grey_light")
    c.hline(1, 9, 6, "grey_light")
    c.outline()
    s.stamp(c, 0, 0)
    return s


def llave(s):
    """Llave fija en diagonal: boca abierta arriba a la izquierda, mango hacia abajo a la derecha."""
    s.ellipse(2, 2, 9, 9, "concrete_light")            # cabeza
    s.ellipse(5, 5, 9, 9, "concrete")                  # sombra abajo a la derecha
    for x, y in ((2, 2), (3, 2), (2, 3), (3, 3), (4, 3), (3, 4), (4, 4), (5, 4), (4, 5), (5, 5)):
        s.set(x, y, None)                              # boca abierta (en U, hacia el noroeste)
    for k in range(9):                                 # mango de 2 px
        s.set(8 + k, 8 + k, "concrete_light")
        s.set(9 + k, 8 + k, "concrete_light")
        s.set(8 + k, 9 + k, "concrete")


def rehabilitar():
    s = Sprite()
    build_a5.tren(s, 15)
    for x in range(1, 13):                              # vagón viejo: más apagado
        for y in range(17, 24):
            if s.get(x, y)[:3] == (0x70, 0x78, 0x4c):
                s.set(x, y, "olive_grey")
    s.set(9, 19, "rust")
    s.set(4, 21, "rust")
    s.outline()
    w = Sprite()
    llave(w)
    w.outline()
    s.stamp(w, 0, 0)
    return s


def chispas():
    """Busto de Chispas Ferrer, de frente con un leve 3/4, luz arriba a la izquierda."""
    s = Sprite()
    s.poly([(4, 31), (6, 24), (12, 21), (20, 21), (26, 24), (28, 31)], "slate")      # mono de trabajo
    s.poly([(5, 31), (7, 25), (12, 22), (14, 22), (12, 31)], "purple_grey")
    s.line(12, 22, 11, 31, "slate_dark")                 # cremallera
    s.rect(18, 25, 4, 3, "slate_dark")                   # bolsillo
    s.set(19, 26, "concrete")                            # lápiz
    s.rect(13, 19, 6, 3, "rose_brown")                   # cuello
    s.ellipse(10, 8, 21, 21, "tan")                      # cara
    s.ellipse(10, 9, 15, 19, "beige")                    # lado iluminado
    s.vline(21, 12, 18, "rose_brown")                    # sombra
    s.set(20, 19, "rose_brown")
    s.rect(9, 12, 2, 4, "tan")                           # orejas
    s.rect(21, 12, 2, 4, "rose_brown")
    s.hline(12, 14, 13, "olive_black")                   # ojos (mirada cansada)
    s.hline(17, 19, 13, "olive_black")
    s.hline(12, 14, 12, "grey_brown")                    # cejas pobladas
    s.hline(17, 19, 12, "grey_brown")
    s.vline(15, 14, 16, "rose_brown")                    # nariz
    s.set(16, 16, "rose_brown")
    s.rect(12, 17, 8, 2, "grey_light")                   # bigote gris
    s.hline(13, 18, 18, "concrete_light")
    s.set(12, 18, "concrete_light")
    s.hline(14, 17, 19, "rose_brown")                    # boca
    s.set(19, 15, "grey_brown")                          # grasa en la mejilla
    s.set(11, 17, "grey_brown")
    # Gorra plana y gafas en la frente.
    s.poly([(8, 9), (11, 4), (21, 4), (24, 8), (23, 10), (9, 10)], "grey_brown")
    s.hline(11, 20, 4, "concrete")
    s.hline(9, 23, 10, "brown_dark")                     # visera
    s.rect(11, 9, 4, 2, "slate_dark")                    # gafas de maquinista
    s.rect(17, 9, 4, 2, "slate_dark")
    s.set(12, 9, "concrete_light")
    s.set(18, 9, "concrete_light")
    s.hline(15, 16, 10, "slate_dark")
    s.outline()
    return s


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    nombres = ["puerta_via_rota", "tren_nuevo", "rehabilitar", "chispas"]
    s32 = [globals()[n]() for n in nombres]
    for s in s32:
        s.limit_colors(10)
    s16 = [reduce_16(s) for s in s32]
    for n, a, b in zip(nombres, s32, s16):
        a.save(OUT / f"vm_taller_{n}_32.png")
        b.save(OUT / f"vm_taller_{n}_16.png")
        print(n, "32:", check(a) or "OK", "| 16:", check(b) or "OK")
    contact_sheet([s32, s16, [s.silhouette() for s in s32]], OUT / "contacto_taller.png", scale=6,
                  labels=["32 px", "16 px", "silueta"])
