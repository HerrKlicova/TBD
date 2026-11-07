"""Iconos de formación y silueta fantasma del destino (7-nov). Bruno (Arte técnico).

Encargo de Lucía, docs/encargo-control-disparos-noche.md §3.2 (Eugenio: «en línea o dibujando un cuadrado los
pones como en pelotón», al estilo Total War).

Iconos (planta, el frente mira ARRIBA; mismo lenguaje que A3 órdenes: hueso sin marco, el marco lo pone el botón):
  formacion_linea    F1  dos filas largas
  formacion_bloque   F2  cuadrado lleno
  formacion_columna  F3  fila de a dos hacia el frente
  formacion_dispersa F4  línea abierta en zigzag
  Tamaños: 32 y 16 (botones del HUD) y 12 (esquina de la tarjeta del pelotón de 48). A 32 y 16 llevan una raya de
  frente en beige arriba; a 12 no cabe y se quita.

Silueta fantasma (se pinta a resolución de pantalla, sobre el mundo, mientras se arrastra el frente):
  fantasma_punto       2x2   d4d0cc al 50 % (alfa 128), uno por soldado
  fantasma_flecha      7x5   d4d0cc al 50 %, mira a +X (Marcos la gira hacia donde mira el pelotón)
  fantasma_no_valido   3x3   aspa en hueso b4a080 al 100 % (dentro de un muro o fuera del recinto)
  La flecha va una por pelotón, delante del centro de la línea.
"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, Sprite  # noqa: E402

FORMAS = {
    # (columnas x, filas y) en una rejilla abstracta; y=0 es la fila del frente
    "linea": [(x, y) for y in range(2) for x in range(6)],
    "bloque": [(x, y) for y in range(4) for x in range(4)],
    "columna": [(x, y) for y in range(5) for x in range(2)],
    "dispersa": [(0, 1), (1, 0), (2, 1), (3, 0), (4, 1), (5, 0)],
}
# paso de la rejilla y tamaño del punto por tamaño de icono
PASO = {32: {"linea": (5, 5), "bloque": (6, 6), "columna": (6, 5), "dispersa": (5, 6)},
        16: {"linea": (3, 3), "bloque": (3, 3), "columna": (3, 3), "dispersa": (3, 3)},
        12: {"linea": (2, 3), "bloque": (3, 3), "columna": (3, 2), "dispersa": (2, 3)}}
PUNTO = {32: 3, 16: 2, 12: 1}


def icono(nombre, t):
    s = Sprite(t, t)
    pts = FORMAS[nombre]
    if t == 16 and nombre in ("linea", "dispersa"):
        pts = [(x, y) for x, y in pts if x < 5]          # a 16 px solo caben 5 por fila con 1 px de aire
    if t == 16 and nombre == "columna":
        pts = [(x, y) for x, y in pts if y < 4]
    px, py = PASO[t][nombre]
    p = PUNTO[t]
    nx = max(x for x, _ in pts) + 1
    ny = max(y for _, y in pts) + 1
    w = int(round((nx - 1) * px)) + p
    h = int(round((ny - 1) * py)) + p
    raya = 3 if t >= 16 else 0
    x0 = (t - w) // 2
    y0 = (t - h + raya) // 2
    for x, y in pts:
        xx, yy = x0 + int(round(x * px)), y0 + int(round(y * py))
        s.rect(xx, yy, p, p, "bone")
        if p == 3:
            s.set(xx, yy, "cream")                     # luz arriba a la izquierda, como A1-A3
            s.set(xx + 2, yy + 2, "beige")
    if raya:                                           # frente: raya beige encima de la primera fila
        s.hline(x0, x0 + w - 1, y0 - raya, "beige")
    if t == 32:
        s.outline()
    return s


def fantasma_punto():
    s = Sprite(2, 2)
    s.rect(0, 0, 2, 2, PAL["bone"][:3] + (128,))
    return s


def fantasma_flecha():
    s = Sprite(7, 5)
    c = PAL["bone"][:3] + (128,)
    s.hline(0, 4, 2, c)
    for k in range(3):
        s.set(4 + k, 2, c)
        s.set(4 + k - 2, 2 - (2 - k), c) if k < 2 else None
    s.set(3, 0, c); s.set(4, 1, c); s.set(4, 3, c); s.set(3, 4, c)
    return s


def fantasma_no_valido():
    s = Sprite(3, 3)
    for k in range(3):
        s.set(k, k, "beige")
        s.set(2 - k, k, "beige")
    return s


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_formaciones")
    OUT.mkdir(parents=True, exist_ok=True)
    noms = list(FORMAS)
    ics = {t: [icono(n, t) for n in noms] for t in (32, 16, 12)}
    for t, lst in ics.items():
        for n, s in zip(noms, lst):
            s.img.save(OUT / f"ui_formacion_{n}_{t}.png")
    fp, ff, fn = fantasma_punto(), fantasma_flecha(), fantasma_no_valido()
    fp.img.save(OUT / "ui_fantasma_punto_2.png")
    ff.img.save(OUT / "ui_fantasma_flecha_7x5.png")
    fn.img.save(OUT / "ui_fantasma_no_valido_3.png")

    # hoja: iconos sobre la placa de botón (navy) x4, y la tarjeta de 48 con el icono de 12 en la esquina
    E = 4
    G = 8
    hoja = Image.new("RGBA", (4 * (48 * E + G) + G, (32 + 16 + 48) * E + 5 * G + 40 * E), PAL["navy"])
    y = G
    for t in (32, 16):
        for i, s in enumerate(ics[t]):
            placa = Image.new("RGBA", (t, t), PAL["slate_dark"])
            hoja.paste(Image.alpha_composite(placa, s.img).resize((t * E, t * E), Image.NEAREST),
                       (G + i * (32 * E + G), y))
        y += t * E + G
    tarjeta = Path("/mnt/project-files/docs/arte/pixel-codigo/noche-prototipo/tarjetas/ui_tarjeta_fusileros_48.png")
    for i, s in enumerate(ics[12]):
        base = Image.new("RGBA", (48, 48), PAL["slate_dark"])
        if tarjeta.exists():
            base.alpha_composite(Image.open(tarjeta).convert("RGBA"))
        base.alpha_composite(Image.new("RGBA", (14, 14), PAL["navy"]), (33, 33))
        base.alpha_composite(s.img, (34, 34))
        hoja.paste(base.resize((48 * E, 48 * E), Image.NEAREST), (G + i * (48 * E + G), y))
    y += 48 * E + G
    # silueta fantasma sobre suelo de noche aproximado (3b3c35, el valor real que sale al quitar la doble
    # codificación de las capturas de qa20): un pelotón en línea con su flecha y un punto no válido junto a un muro
    W, H = 4 * (48 * E + G) // E, 40
    suelo = Image.new("RGBA", (W, H), (0x3b, 0x3c, 0x35, 255))
    muro = Image.new("RGBA", (6, 22), PAL["olive_grey"])
    suelo.alpha_composite(muro, (W - 40, 6))
    for k in range(10):
        x = 30 + k * 4 + (k // 5) * 0
        yy = 18 + (k % 2) * 4
        if k == 9:
            suelo.alpha_composite(fn.img, (W - 40, yy - 1))
        else:
            suelo.alpha_composite(fp.img, (x, yy))
    suelo.alpha_composite(ff.img.rotate(90, expand=True), (46, 8))
    hoja.paste(suelo.resize((W * E, H * E), Image.NEAREST), (0, y))
    hoja.save(OUT / "muestra_formaciones_fantasma_x4.png")
    print("ok")
