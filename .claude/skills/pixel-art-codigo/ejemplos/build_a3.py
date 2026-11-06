"""A3 · Órdenes (32 px y 16 px) por código. Bruno (Arte técnico), 6-nov-2026 (D-N50).
Lenguaje: las flechas y símbolos de orden van en hueso (el color «de mando», neutro);
el marco lo pone el botón, así que aquí no hay marco. Misma luz, contorno y desgaste que A1."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import Sprite, check, contact_sheet, reduce_16  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_a3")


def flecha_der(s, x0, y0, largo, grueso=4):
    """Flecha hueso hacia la derecha: cuerpo de 'grueso' px y punta triangular."""
    pm = grueso + 3
    s.rect(x0, y0, largo - pm, grueso, "bone")
    s.hline(x0, x0 + largo - pm - 1, y0, "cream")
    s.hline(x0, x0 + largo - pm - 1, y0 + grueso - 1, "beige")
    cy = y0 + grueso // 2
    s.poly([(x0 + largo - pm, cy - pm + 1), (x0 + largo - 1, cy), (x0 + largo - pm, cy + pm - 1 - (grueso % 2 == 0))],
           "bone")
    s.line(x0 + largo - pm, cy - pm + 1, x0 + largo - 1, cy, "cream")


def via_lateral(s, y):
    """Carril visto de lado con traviesas, a lo ancho del icono."""
    s.hline(1, 30, y, "concrete_light")
    s.hline(1, 30, y + 1, "slate")
    for x in range(2, 30, 5):
        s.rect(x, y + 2, 3, 2, "brown")
        s.hline(x, x + 2, y + 2, "ochre_dark")


def vagon(s, x0, y0, w=18, puerta=True, izq=False):
    """Vagón de tropa oliva de perfil con la puerta abierta."""
    s.rect(x0, y0, w, 10, "olive")
    s.hline(x0, x0 + w - 1, y0, "olive_light")
    s.hline(x0, x0 + w - 1, y0 + 9, "olive_dark")
    s.vline(x0, y0, y0 + 9, "olive_light")
    s.vline(x0 + w - 1, y0, y0 + 9, "olive_dark")
    for x in range(x0 + 3, x0 + w - 2, 4):
        s.vline(x, y0 + 2, y0 + 7, "olive_dark")        # tablas
    if puerta:
        px = x0 + 2 if izq else x0 + w - 7
        s.rect(px, y0 + 2, 5, 8, "olive_black")          # hueco de la puerta
        s.vline(px - 1 if not izq else px + 5, y0 + 2, y0 + 9, "olive_light")
    s.rect(x0 + 1, y0 + 10, w - 2, 1, "slate_dark")
    for x in (x0 + 3, x0 + w - 4):
        s.ellipse(x - 2, y0 + 10, x + 2, y0 + 14, "slate")
        s.set(x, y0 + 12, "concrete_light")
    s.set(x0 + 5, y0 + 4, "olive_light")                  # pintura saltada


def soldado(s, x, y):
    """Soldado de 6x12: casco oliva, cara tostada, uniforme oliva grisáceo."""
    s.rect(x + 1, y, 4, 2, "olive")
    s.hline(x, x + 5, y + 2, "olive_dark")               # ala del casco
    s.rect(x + 1, y + 3, 4, 2, "tan")
    s.rect(x, y + 5, 6, 4, "olive_grey")
    s.vline(x, y + 5, y + 8, "olive_light")
    s.rect(x + 1, y + 9, 2, 3, "grey_brown")
    s.rect(x + 3, y + 9, 2, 3, "grey_brown")


def aguantar():
    """Puño cerrado de frente con los nudillos marcados (pose de la v6 de Pixelab)."""
    s = Sprite()
    s.grid(6, 3, """
        ..aaaa.aaaa.aaaa.aaa..
        .abbbbabbbbabbbbabbba.
        .abbbbabbbbabbbbabbbb.
        .accccacccc.cccc.cccd.
        .abbbbbbbbbbbbbbbbbbd.
        .bbbbbbbbbbbbbbbbbbbd.
        tttttttttttbbbbbbbbbd.
        tttttttttttttbbbbbbbd.
        .ttttttttttttbbbbbbbd.
        ..ccccccccctbbbbbbbbd.
        ...bbbbbbbbbbbbbbbbd..
        ...bbbbbbbbbbbbbbbbd..
        ....bbbbbbbbbbbbbbd...
        .....bbbbbbbbbbbbd....
        ......ddddddddddd.....
        ......wwwwwwwwwww.....
        ......wwwwwwwwwww.....
        ......wwwwwwwwwww.....
        ......wwwwwwwwwww.....
        ......wwwwwwwwwww.....
        ......vvvvvvvvvvv.....
    """, {"a": "cream", "b": "bone", "c": "beige", "d": "beige", "t": "bone", "w": "olive_grey",
          "v": "grey_brown"})
    # Pulgar cruzado por delante, con su sombra, y luz en los nudillos.
    s.hline(6, 17, 12, "beige")
    s.hline(7, 16, 9, "cream")
    s.vline(7, 9, 10, "cream")
    s.set(10, 5, "cream")
    s.set(15, 5, "cream")
    s.set(20, 5, "cream")
    s.hline(12, 22, 22, "olive_light")          # puño de la manga iluminado
    s.set(14, 21, "olive_light")                 # roce
    s.outline()
    return s


def seguir():
    s = Sprite()
    via_lateral(s, 20)
    flecha_der(s, 3, 8, 26, 5)
    s.outline()
    return s


def retirarse():
    s = Sprite()
    via_lateral(s, 24)
    # Flecha en U: arriba va hacia delante, gira por la derecha y vuelve abajo hacia atrás.
    s.grid(2, 3, """
        ......aaaaaaaaaaaaaaa.....
        ......bbbbbbbbbbbbbbbbb...
        ......bbbbbbbbbbbbbbbbbb..
        ......ccccccccccccccbbbbb.
        ....................bbbbc.
        .....................bbbc.
        .....................bbbc.
        .......b............bbbbc.
        ......bb.......aaaaabbbbc.
        .....bbbaaaaaaaabbbbbbbc..
        ....bbbbbbbbbbbbbbbbbbc...
        ...bbbbbbbbbbbbbbbbbcc....
        ....bbbbbbbbbbbbbbccc.....
        .....bbbcccccccccc........
        ......bb..................
        .......b..................
    """, {"a": "cream", "b": "bone", "c": "beige"})
    s.outline()
    return s


def flecha_vertical(s, x0, y0, largo, abajo=True):
    """Flecha hueso vertical de 3 px de cuerpo; abajo=True apunta hacia abajo."""
    if abajo:
        s.rect(x0, y0, 3, largo - 4, "bone")
        s.vline(x0, y0, y0 + largo - 5, "cream")
        s.poly([(x0 - 2, y0 + largo - 4), (x0 + 4, y0 + largo - 4), (x0 + 1, y0 + largo - 1)], "bone")
    else:
        s.rect(x0, y0 + 4, 3, largo - 4, "bone")
        s.vline(x0, y0 + 4, y0 + largo - 1, "cream")
        s.poly([(x0 - 2, y0 + 3), (x0 + 4, y0 + 3), (x0 + 1, y0)], "bone")
        s.line(x0 + 1, y0, x0 - 2, y0 + 3, "cream")


def desplegar():
    """Vagón arriba con la puerta abierta; la flecha baja de la puerta al soldado ya en tierra."""
    s = Sprite()
    vagon(s, 1, 1, 21)
    flecha_vertical(s, 16, 17, 12, abajo=True)       # de la puerta al suelo
    soldado(s, 23, 18)                               # ya en tierra
    s.hline(0, 31, 30, "slate")
    s.outline()
    return s


def embarcar():
    """El mismo vagón; la flecha sube desde el soldado hasta la puerta."""
    s = Sprite()
    vagon(s, 1, 1, 21)
    flecha_vertical(s, 16, 17, 12, abajo=False)      # del suelo a la puerta
    soldado(s, 23, 18)                               # a punto de subir
    s.hline(0, 31, 30, "slate")
    s.outline()
    return s


def escoltar():
    s = Sprite()
    # Locomotora pequeña de perfil a la derecha y escudo de escolta a la izquierda, delante.
    s.rect(12, 12, 18, 8, "olive")
    s.hline(12, 29, 12, "olive_light")
    s.rect(23, 8, 7, 5, "olive")
    s.hline(23, 29, 8, "olive_light")
    s.rect(25, 9, 3, 3, "slate_dark")
    s.rect(15, 9, 2, 3, "olive_shadow")
    s.hline(12, 29, 19, "olive_dark")
    s.rect(11, 20, 20, 2, "slate_dark")
    for x in (15, 21, 27):
        s.ellipse(x - 2, 21, x + 2, 25, "slate")
        s.set(x, 23, "concrete_light")
    s.set(19, 14, "concrete")
    # Escudo: hueso con banda oliva (heráldica simple), luz arriba a la izquierda.
    s.poly([(2, 6), (14, 6), (14, 18), (8, 26), (2, 18)], "bone")
    s.line(2, 18, 8, 26, "beige")
    s.vline(14, 7, 18, "beige")
    s.line(14, 18, 9, 25, "beige")
    s.hline(2, 14, 6, "cream")
    s.vline(2, 6, 17, "cream")
    s.poly([(3, 11), (8, 15), (13, 11), (13, 14), (8, 18), (3, 14)], "olive")   # galón
    s.line(3, 11, 8, 15, "olive_light")
    s.set(11, 8, "beige")                            # muesca
    s.outline()
    return s


if __name__ == "__main__":
    nombres = ["aguantar", "seguir", "retirarse", "desplegar", "embarcar", "escoltar"]
    s32 = [globals()[n]() for n in nombres]
    for s in s32:
        aviso = s.limit_colors(10)   # regla de estética única: 6–10 colores; los acentos no se funden
        if aviso:
            print("AVISO limit_colors:", aviso)
    s16 = [reduce_16(s) for s in s32]
    for n, a, b in zip(nombres, s32, s16):
        a.save(OUT / f"vm_ordenes_{n}_32.png")
        b.save(OUT / f"vm_ordenes_{n}_16.png")
        print(n, "32:", check(a, recurso=True) or "OK", "| 16:", check(b, recurso=True) or "OK")
    contact_sheet([s32, s16, [s.silhouette() for s in s32]], OUT / "contacto_a3.png", scale=6,
                  labels=["32 px", "16 px", "silueta"])
