"""A4 · Estado de estación (marcador del mapa, 32 px) por código. Bruno (Arte técnico), 6-nov-2026.
Simbología de mapa ferroviario y de estado mayor antiguo: círculo de estación con la marca del andén,
banderines y flechas; sin calaveras (D-N48). Tokens de estado (Lucía/Iris):
segura #70784c, amenazada #847c34, asediada #982420, perdida #340000 con borde #7c181c.
Además se exporta una base en grises para que Unity la tiña si lo prefiere."""
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import Sprite, check, contact_sheet, reduce_16  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_a4")
C = 15.5   # centro del círculo


def anillo(s, r0, r1, color, cx=C, cy=C):
    for j in range(32):
        for i in range(32):
            d = math.hypot(i + .5 - cx - .5, j + .5 - cy - .5)
            if r0 <= d < r1:
                s.set(i, j, color)


def base(token, borde=None, dentro="bone", marca="slate_dark"):
    s = Sprite()
    anillo(s, 0, 10.5, dentro)
    anillo(s, 7.2, 10.5, token)                       # anillo del estado
    anillo(s, 10.5, 11.6, borde or token)
    # Luz arriba a la izquierda: un arco fino de un tono más claro en el anillo (volumen de ficha).
    luz = {"olive": "olive_light", "olive_yellow": "olive_light", "red_bright": "rose", "concrete": "concrete_light",
           "maroon": "wine"}[token]
    for j in range(32):
        for i in range(32):
            d = math.hypot(i + .5 - C - .5, j + .5 - C - .5)
            if 9.3 <= d < 10.5 and (i - C) + (j - C) < -6:
                s.set(i, j, luz)
    # Marca del andén: vía que cruza el círculo y andén a un lado (símbolo de estación).
    s.hline(9, 22, 16, marca)
    s.hline(9, 22, 17, marca)
    s.rect(11, 12, 10, 2, marca)
    return s


def segura(t="olive"):
    s = base(t)
    s.outline()
    return s


def amenazada(t="olive_yellow"):
    s = base(t)
    # Banderín de aviso clavado en el borde superior derecho.
    s.vline(24, 1, 10, "brown_dark")
    s.poly([(25, 1), (31, 3), (25, 6)], t)
    s.line(25, 1, 30, 3, "cream")
    s.outline()
    return s


def asediada(t="red_bright"):
    s = base(t)
    # Cuatro flechas de ataque de mapa de estado mayor, con la punta clavada en el anillo.
    flecha = [f for f in """
aa.......
aaa......
.aaa.....
..aaa..a.
...aaa.a.
....aaaa.
.....aaa.
...aaaaa.
""".strip("\n").split("\n")]
    for (sx, sy) in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
        for j, fila in enumerate(flecha):
            for i, ch in enumerate(fila):
                if ch == "a":
                    s.set(i if sx > 0 else 31 - i, j if sy > 0 else 31 - j, t)
    s.outline()
    return s


def perdida():
    s = base("maroon", borde="red", dentro="grey_brown", marca="concrete_light")
    # Tachada a la manera de los mapas antiguos: barra diagonal sobre la estación.
    for k in range(-1, 2):
        s.line(8 + k, 23, 23 + k, 8, "red")
    s.line(9, 23, 23, 9, "rose")
    s.outline()
    return s


def gris(f):
    """Base en grises para teñir en Unity: mismo dibujo, el token en gris medio."""
    return f({"segura": "concrete", "amenazada": "concrete", "asediada": "concrete"}[f.__name__])


if __name__ == "__main__":
    nombres = ["segura", "amenazada", "asediada", "perdida"]
    s32 = [globals()[n]() for n in nombres]
    grises = [gris(globals()[n]) for n in nombres[:3]]
    s16 = [reduce_16(s) for s in s32]
    for n, a, b in zip(nombres, s32, s16):
        a.save(OUT / f"vm_estacion_{n}_32.png")
        b.save(OUT / f"vm_estacion_{n}_16.png")
        print(n, "32:", check(a) or "OK", "| 16:", check(b) or "OK")
    for n, g in zip(nombres, grises):
        g.save(OUT / f"vm_estacion_{n}_gris_32.png")
    contact_sheet([s32, s16, grises], OUT / "contacto_a4.png", scale=6, labels=["32 px", "16 px", "gris"])
