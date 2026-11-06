"""A2 · Alertas (32 px y 16 px) por código. Bruno (Arte técnico), 6-nov-2026 (D-N47).
Lenguaje común: pictograma con luz arriba-izquierda y el ámbar como acento de aviso.
Sin rojo (D-N46): las «cruces» de vacío son aspas ámbar. Sin texto ni sangre."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import CONTORNO, Sprite, check, contact_sheet, reduce_16  # noqa: E402
import build_a1  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_a2")


def aspa(s, x0, y0, n):
    """Aspa ámbar de 3 px de grosor con contorno, encima de lo que haya."""
    capa = Sprite(s.w, s.h)
    for d in range(n):
        for k in (-1, 0, 1):
            capa.set(x0 + d + k, y0 + d, "amber")
            capa.set(x0 + n - 1 - d + k, y0 + d, "amber")
    for d in range(n):
        capa.set(x0 + d, y0 + d, "cream")      # filo iluminado
    capa.outline()
    s.stamp(capa, 0, 0)


def apagar(s):
    """Versión «vacía»: baja un paso los tonos de luz para que el aspa mande."""
    for a, b in (("olive_light", "olive"), ("concrete_light", "concrete"), ("grey_light", "concrete_light"),
                 ("cream", "tan"), ("amber", "ochre_dark")):
        s.replace(a, b)


def ataque():
    s = Sprite()
    # Señal triangular de aviso en ámbar con una mano que sale del suelo, en silueta.
    s.poly([(16, 2), (30, 28), (2, 28)], "amber")
    s.line(16, 2, 2, 28, "cream")
    s.hline(3, 29, 28, "ochre_dark")
    s.line(17, 3, 30, 27, "ochre_dark")
    s.poly([(16, 6), (27, 26), (5, 26)], "ochre_dark")
    s.poly([(16, 8), (25, 25), (7, 25)], "brown_dark")
    # Mano: dedos abiertos, piel gris verdosa apagada (no sangre).
    mano = """
        ....a.a....
        ..a.a.a.a..
        ..a.a.a.a..
        ..a.a.a.a..
        ..aaaaaaa..
        a.aaaaaaa..
        aaaaaaaaa..
        .aaaaaaaa..
        ..aaaaaa...
        ..aaaaaa...
        ..aaaaaa...
    """
    s.grid(11, 11, mano, {"a": "olive_light"})
    for x in (13, 15, 17, 19):
        s.set(x, 12, "concrete_light")             # luz en las yemas
    s.vline(19, 15, 21, "olive")                   # sombra a la derecha
    s.vline(18, 19, 21, "olive")
    s.set(14, 17, "olive")                         # nudillo
    s.hline(8, 24, 22, "olive_shadow")             # tierra removida
    s.hline(10, 22, 23, "olive_shadow")
    s.set(9, 21, "olive_shadow")
    s.set(23, 21, "olive_shadow")
    s.outline()
    return s


def via_cortada():
    s = Sprite()
    # Tramo de vía en perspectiva (se aleja hacia arriba) roto por el centro.
    for y, (x0, x1) in zip(range(6, 30, 4), ((11, 20), (10, 21), (8, 23), (7, 24), (5, 26), (4, 27))):
        if 13 <= y <= 17:
            continue
        s.hline(x0, x1, y, "brown")            # traviesa
        s.hline(x0, x1, y + 1, "brown_dark")
        s.set(x0, y, "ochre_dark")
    for lado in (0, 1):
        pts = [(12, 4), (8, 30)] if lado == 0 else [(19, 4), (23, 30)]
        (xa, ya), (xb, yb) = pts
        for y in range(ya, yb + 1):
            if 12 <= y <= 18:
                continue
            x = round(xa + (xb - xa) * (y - ya) / (yb - ya))
            s.set(x, y, "concrete_light")
            s.set(x + (1 if lado else -1), y, "slate")
    # Extremos doblados del carril roto y el aspa del corte.
    s.line(11, 11, 9, 9, "concrete")
    s.line(21, 19, 24, 20, "concrete")
    aspa(s, 18, 1, 12)
    s.outline()
    return s


def tren_detenido():
    s = Sprite()
    # Locomotora diésel de perfil, apagada, con una barra de alto ámbar encima.
    s.rect(3, 13, 24, 9, "olive")              # caja
    s.hline(3, 26, 13, "olive_light")
    s.rect(19, 9, 8, 5, "olive")               # cabina
    s.hline(19, 26, 9, "olive_light")
    s.rect(21, 10, 4, 3, "slate_dark")         # ventanilla
    s.set(21, 10, "slate")
    s.rect(6, 10, 2, 3, "olive_shadow")        # chimenea apagada
    s.hline(3, 26, 21, "olive_dark")
    for x in range(5, 18, 4):
        s.vline(x, 15, 19, "olive_dark")       # rejillas
    s.rect(2, 22, 27, 2, "slate_dark")         # bastidor
    for x in (5, 11, 19, 25):
        s.ellipse(x - 2, 23, x + 2, 27, "slate")
        s.set(x, 25, "concrete_light")
    s.hline(1, 30, 28, "concrete")             # carril
    s.set(14, 15, "concrete")                  # pintura saltada
    # Señal de alto: disco ámbar con barra horizontal (sin texto ni rojo).
    s.ellipse(9, 1, 17, 9, "amber")
    s.ellipse(10, 2, 13, 5, "cream", only="amber")
    s.rect(10, 5, 7, 2, "brown_dark")
    s.outline()
    return s


def sin_municion():
    s = build_a1.municion()
    s.replace(CONTORNO, None)
    for x in range(5, 22):                     # sin cartuchos: borra lo que asoma
        for y in range(0, 10):
            s.set(x, y, None)
    s.rect(6, 11, 16, 2, "slate_dark")
    apagar(s)
    s.outline()
    aspa(s, 15, 2, 12)
    return s


def sin_combustible():
    s = build_a1.combustible()
    apagar(s)
    aspa(s, 16, 1, 12)
    return s


def asedio():
    s = Sprite()
    # Muro de bloques de hormigón con la horda (siluetas oscuras) apretada contra él.
    s.rect(6, 6, 20, 16, "concrete")
    for y in range(6, 22, 4):
        s.hline(6, 25, y, "concrete_light")
        s.hline(6, 25, y + 3, "grey_brown")
        off = 0 if (y // 4) % 2 else 5
        for x in range(6 + off, 26, 10):
            s.vline(x, y + 1, y + 2, "grey_brown")
    s.vline(6, 6, 21, "concrete_light")
    s.vline(25, 6, 21, "grey_brown")
    s.rect(9, 3, 4, 3, "concrete")             # almenas de sacos
    s.rect(19, 3, 4, 3, "concrete")
    s.hline(9, 12, 3, "concrete_light")
    s.hline(19, 22, 3, "concrete_light")
    s.set(17, 11, "olive_shadow")              # grieta
    s.set(18, 12, "olive_shadow")
    s.set(18, 13, "olive_shadow")
    # Horda: figuras con los brazos en alto contra el muro, luz de borde arriba a la izquierda.
    for k, (x, y) in enumerate(((1, 19), (6, 21), (11, 20), (16, 22), (21, 20), (26, 21),
                                (3, 25), (9, 26), (14, 25), (19, 26), (24, 25))):
        s.rect(x, y + 3, 4, 6, "olive_shadow")             # cuerpo
        s.ellipse(x, y, x + 3, y + 3, "olive_shadow")      # cabeza
        s.set(x, y + 1, "olive_grey")                      # luz de borde
        s.vline(x, y + 3, y + 5, "olive_grey")
        if k < 6:                                          # fila de delante: brazos al muro
            s.vline(x - 1, y - 3, y + 3, "olive_shadow")
            s.vline(x + 4, y - 2, y + 3, "olive_shadow")
            s.set(x - 1, y - 3, "olive_grey")
    s.rect(0, 29, 32, 1, None)
    s.outline()
    # Acento ámbar: el foco del muro encendido.
    s.rect(15, 4, 2, 2, "amber")
    s.set(15, 4, "cream")
    return s


if __name__ == "__main__":
    nombres = ["ataque", "via_cortada", "tren_detenido", "sin_municion", "sin_combustible", "asedio"]
    s32 = [globals()[n]() for n in nombres]
    s16 = [reduce_16(s) for s in s32]
    for n, a, b in zip(nombres, s32, s16):
        a.save(OUT / f"vm_alertas_{n}_32.png")
        b.save(OUT / f"vm_alertas_{n}_16.png")
        print(n, "32:", check(a, recurso=True) or "OK", "| 16:", check(b, recurso=True) or "OK")
    contact_sheet([s32, s16, [s.silhouette() for s in s32]], OUT / "contacto_a2.png", scale=6,
                  labels=["32 px", "16 px", "silueta"])
