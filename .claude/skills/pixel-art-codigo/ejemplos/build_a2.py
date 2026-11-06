"""A2 · Alertas (32 px y 16 px) por código. Bruno (Arte técnico), 6-nov-2026 (D-N47).
Lenguaje común: pictograma con luz arriba-izquierda y el ámbar como acento de aviso.
Dos niveles (Lucía, 6-nov): aviso en ámbar (aspa en la esquina = falta o cortado) y emergencia\ncon el rojo de peligro #982420 (ataque y asedio). Sin texto ni sangre."""
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


def senal_peligro(s):
    """Triángulo de emergencia: borde rojo de peligro (#982420, token de alerta grave,
    Lucía 6-nov) y relleno claro para que la silueta oscura se lea."""
    s.poly([(16, 1), (31, 29), (1, 29)], "red_bright")
    s.line(16, 1, 1, 29, "red")
    s.poly([(16, 6), (27, 27), (5, 27)], "cream")
    s.line(16, 6, 5, 27, "bone")
    s.hline(6, 26, 27, "beige")


def ataque():
    s = Sprite()
    senal_peligro(s)
    # Mano que sale de la tierra en silueta oscura, cuatro dedos separados.
    mano = """
        ..a.a.a.a..
        ..a.a.a.a..
        ..a.a.a.a..
        ..aaaaaaa..
        a.aaaaaaa..
        aaaaaaaaa..
        .aaaaaaa...
        ..aaaaa....
        ..aaaaa....
    """
    s.grid(11, 13, mano, {"a": "olive_black"})
    s.hline(9, 23, 22, "olive_black")      # tierra removida
    s.hline(8, 24, 23, "olive_shadow")
    s.set(10, 21, "olive_black")
    s.set(22, 21, "olive_black")
    s.outline()
    return s


def via_cortada():
    s = Sprite()
    # Una sola vía horizontal vista desde arriba, cortada por el centro en su misma dirección.
    for x in (2, 7, 12, 19, 24, 29):
        if 13 <= x <= 18:
            continue
        s.rect(x, 12, 2, 13, "brown")           # traviesas verticales
        s.vline(x, 12, 24, "ochre_dark")
    for y in (15, 21):
        for (x0, x1) in ((1, 12), (19, 30)):
            s.hline(x0, x1, y, "grey_light")
            s.hline(x0, x1, y + 1, "concrete")
            s.hline(x0, x1, y + 2, "slate")
    # Extremos astillados del carril roto.
    for y in (15, 21):
        s.set(12, y + 2, "concrete")
        s.set(19, y, "concrete")
    s.outline()
    aspa(s, 19, 0, 11)
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
    for x in range(5, 22):                     # sin cartuchos de pie
        for y in range(0, 10):
            s.set(x, y, None)
    s.rect(6, 10, 16, 3, "slate_dark")
    for x in (6, 10, 14, 18):                  # huecos vacíos de los cartuchos, con el borde de latón gastado
        s.rect(x, 10, 3, 2, "olive_black")
        s.hline(x, x + 2, 12, "ochre_dark")
    apagar(s)
    # Un único cartucho tumbado sobre la tapa: la lata conserva su identidad.
    s.rect(3, 7, 9, 3, "amber")
    s.hline(3, 11, 7, "cream")
    s.hline(3, 11, 9, "ochre_dark")
    s.rect(1, 7, 2, 3, "ochre_dark")
    s.vline(9, 7, 9, "ochre_dark")
    s.outline()
    aspa(s, 16, 1, 12)
    return s


def sin_combustible():
    s = build_a1.combustible()
    apagar(s)
    aspa(s, 16, 1, 12)
    return s


def asedio():
    s = Sprite()
    # Fondo rojo de peligro (placa redondeada) para el nivel de emergencia.
    s.rect(1, 2, 30, 28, "red_bright")
    for (x, y) in ((1, 2), (30, 2), (1, 29), (30, 29)):
        s.set(x, y, None)
    s.hline(2, 29, 2, "red")
    s.vline(1, 3, 28, "red")
    # Muro de bloques de hormigón con almenas.
    s.rect(6, 7, 20, 14, "concrete")
    for y in range(7, 21, 4):
        s.hline(6, 25, y, "concrete_light")
        s.hline(6, 25, min(20, y + 3), "grey_brown")
        off = 0 if (y // 4) % 2 else 5
        for x in range(6 + off, 26, 10):
            s.vline(x, y + 1, min(20, y + 2), "grey_brown")
    s.vline(6, 7, 20, "concrete_light")
    s.vline(25, 7, 20, "grey_brown")
    for x0 in (6, 13, 20):
        s.rect(x0, 4, 5, 3, "concrete")
        s.hline(x0, x0 + 4, 4, "concrete_light")
    s.set(17, 12, "olive_shadow")              # grieta
    s.set(18, 13, "olive_shadow")
    # Cinco cabezas y hombros en silueta al pie del muro (se leen a 16 px).
    for x in (5, 12, 19, 26):
        s.ellipse(x - 2, 19, x + 1, 22, "olive_black")
        s.rect(x - 2, 23, 4, 5, "olive_black")
        s.set(x - 2, 23, None)
        s.set(x + 1, 23, None)
    s.outline()
    return s


if __name__ == "__main__":
    nombres = ["ataque", "via_cortada", "tren_detenido", "sin_municion", "sin_combustible", "asedio"]
    s32 = [globals()[n]() for n in nombres]
    s16 = [reduce_16(s) for s in s32]
    for n, a, b in zip(nombres, s32, s16):
        a.save(OUT / f"vm_alertas_{n}_32.png")
        b.save(OUT / f"vm_alertas_{n}_16.png")
        grave = n in ("ataque", "asedio")   # nivel de emergencia: lleva el rojo de peligro
        print(n, "32:", check(a, recurso=not grave) or "OK", "| 16:", check(b, recurso=not grave) or "OK")
    contact_sheet([s32, s16, [s.silhouette() for s in s32]], OUT / "contacto_a2.png", scale=6,
                  labels=["32 px", "16 px", "silueta"])
