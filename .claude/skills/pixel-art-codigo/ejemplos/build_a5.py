"""A5 · Marcador de tren (mapa, 32 px) por código. Bruno (Arte técnico), 6-nov-2026 (D-N47).
Tren blindado de perfil, compacto, con el lenguaje de A4: símbolos de mapa en hueso, ataque con
flechas de estado mayor en el rojo de peligro. Estados: en marcha, detenido, bajo ataque y tren de obras."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import Sprite, check, contact_sheet, reduce_16  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_a5")


def tren(s, y=14, obras=False):
    """Locomotora blindada (derecha) y un vagón (izquierda), de 30x13."""
    # Vagón.
    s.rect(1, y + 2, 13, 7, "olive")
    s.hline(1, 13, y + 2, "olive_light")
    s.hline(1, 13, y + 8, "olive_dark")
    s.vline(13, y + 2, y + 8, "olive_dark")
    if not obras:
        for x in (3, 7, 11):
            s.hline(x, x + 1, y + 4, "olive_black")   # troneras
    s.set(5, y + 6, "olive_light")                     # pintura saltada
    # Enganche.
    s.hline(14, 15, y + 7, "slate_dark")
    # Locomotora blindada con el frente en cuña.
    s.poly([(16, y), (26, y), (30, y + 5), (30, y + 8), (16, y + 8)], "olive")
    s.hline(16, 26, y, "olive_light")
    s.line(26, y, 30, y + 5, "olive_light")
    s.hline(16, 30, y + 8, "olive_dark")
    s.rect(18, y + 2, 4, 2, "olive_black")             # visor
    s.hline(23, 28, y + 5, "olive_dark")               # plancha
    s.set(20, y + 6, "concrete")
    # Franja ámbar en el frontal (D-N42, la misma de la locomotora en la zona): dice hacia dónde va.
    s.line(27, y + 2, 30, y + 6, "amber")
    s.line(27, y + 3, 30, y + 7, "amber")
    s.vline(30, y + 6, y + 7, "amber")
    # Bastidor y ruedas.
    s.rect(1, y + 9, 30, 1, "slate_dark")
    for x in (3, 7, 11, 18, 22, 27):                   # ruedas de tren, pequeñas y juntas
        s.ellipse(x - 1, y + 10, x + 1, y + 12, "slate")
        s.set(x, y + 11, "concrete_light")
    s.hline(0, 31, y + 13, "concrete")                 # carril


def en_marcha():
    s = Sprite()
    tren(s, 13)
    # Líneas de velocidad en hueso detrás del tren (a la izquierda, arriba).
    for (y, x0, x1) in ((6, 2, 13), (9, 5, 14), (3, 8, 15)):
        s.hline(x0, x1, y, "bone")
        s.set(x0, y, "beige")
    s.outline()
    return s


def detenido():
    s = Sprite()
    tren(s, 13)
    # Símbolo de pausa en hueso sobre la locomotora.
    s.rect(19, 2, 3, 8, "bone")
    s.rect(24, 2, 3, 8, "bone")
    s.vline(19, 2, 9, "cream")
    s.vline(24, 2, 9, "cream")
    s.outline()
    return s


def bajo_ataque():
    s = Sprite()
    tren(s, 15)
    # Flechas de ataque de estado mayor en el rojo de peligro, cayendo sobre el tren desde arriba.
    flecha = """
        aa......
        aaa.....
        .aaa....
        ..aaa..a
        ...aaa.a
        ....aaaa
        .....aaa
        ...aaaaa
    """
    s.grid(3, 2, flecha, {"a": "red_bright"})
    filas = [f.strip() for f in flecha.strip("\n").split("\n")]
    for j, fila in enumerate(filas):                       # espejo para la flecha de la derecha
        for i, ch in enumerate(fila):
            if ch == "a":
                s.set(28 - i, 2 + j, "red_bright")
    s.outline()
    return s


def obras():
    s = Sprite()
    tren(s, 15, obras=True)
    # Grúa sobre el vagón: torre, pluma inclinada y gancho (acero con ámbar de señalización).
    s.rect(4, 11, 4, 6, "concrete")
    s.vline(4, 11, 16, "concrete_light")
    s.line(6, 11, 15, 3, "concrete_light")
    s.line(6, 12, 15, 4, "slate")
    s.vline(15, 4, 9, "slate_dark")                       # cable
    s.rect(14, 9, 3, 2, "amber")                          # gancho con franja de aviso
    s.set(14, 9, "cream")
    s.outline()
    return s


if __name__ == "__main__":
    nombres = ["en_marcha", "detenido", "bajo_ataque", "obras"]
    s32 = [globals()[n]() for n in nombres]
    for s in s32:
        aviso = s.limit_colors(10)   # regla de estética única: 6–10 colores; los acentos no se funden
        if aviso:
            print("AVISO limit_colors:", aviso)
    s16 = [reduce_16(s) for s in s32]
    # El marcador se voltea según el sentido de la marcha: se exportan las dos orientaciones
    # (en Unity basta con flipX del SpriteRenderer; los PNG _izq son por comodidad).
    from PIL import Image
    OUT.mkdir(parents=True, exist_ok=True)
    for n, a, b in zip(nombres, s32, s16):
        a.img.transpose(Image.FLIP_LEFT_RIGHT).save(OUT / f"vm_tren_{n}_izq_32.png")
        b.img.transpose(Image.FLIP_LEFT_RIGHT).save(OUT / f"vm_tren_{n}_izq_16.png")
    for n, a, b in zip(nombres, s32, s16):
        a.save(OUT / f"vm_tren_{n}_32.png")
        b.save(OUT / f"vm_tren_{n}_16.png")
        grave = n == "bajo_ataque"
        print(n, "32:", check(a, recurso=not grave) or "OK", "| 16:", check(b, recurso=not grave) or "OK")
    contact_sheet([s32, s16, [s.silhouette() for s in s32]], OUT / "contacto_a5.png", scale=6,
                  labels=["32 px", "16 px", "silueta"])
