"""Unifica imágenes de Pixelab (u otras) con la estética de la familia hecha por código.

Eugenio, 6-nov: «todo el arte y todos los iconos tienen que tener una misma estética». Esto pasa
cualquier PNG por las mismas reglas que pixelvm aplica a lo dibujado por código:
  1. Fondo fuera (si no trae transparencia, toma el color de las esquinas) y alfa binaria.
  2. Tamaño real: si la imagen es un múltiplo entero del tamaño pedido, reduce por bloques
     (color más frecuente de cada bloque); si no, vecino más cercano.
  3. Contorno: quita el contorno oscuro propio (píxeles del borde muy oscuros) y pone el nuestro,
     1 px exterior en el negro de la paleta.
  4. Paleta de 32 estricta. Con --sin-rojo (recursos, órdenes, unidades), los rojos van al no rojo
     más cercano (óxido, marrón), que de paso quita la sangre.
  5. Quita motas de difusión y limita a 11 colores, como en lo dibujado por código.
  6. Control de calidad (check) y hoja antes/después a x6 sobre #181c30.

Uso:
  python unificar.py <png|carpeta> <salida> [--tam 32] [--sin-rojo] [--max-colores 11] [--tol 24]
                    [--umbral-contorno 40]
"""
import argparse
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import CONTORNO, PALETA, ROJOS, Sprite, check, contact_sheet  # noqa: E402

SIN_ROJO = [c for c in PALETA if c not in ROJOS]


def quitar_fondo(im, tol):
    px = im.load()
    w, h = im.size
    if min(px[x, y][3] for x in range(w) for y in range(h)) < 255:
        return im
    esquinas = [px[0, 0], px[w - 1, 0], px[0, h - 1], px[w - 1, h - 1]]
    fondo = max(set(esquinas), key=esquinas.count)[:3]
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if abs(r - fondo[0]) + abs(g - fondo[1]) + abs(b - fondo[2]) <= tol * 3:
                px[x, y] = (0, 0, 0, 0)
    return im


def reducir(im, tam):
    w, h = im.size
    if (w, h) == (tam, tam):
        return im
    if w % tam == 0 and h % tam == 0 and w // tam == h // tam:
        f = w // tam
        src = im.load()
        out = Image.new("RGBA", (tam, tam), (0, 0, 0, 0))
        dst = out.load()
        for j in range(tam):
            for i in range(tam):
                blk = [src[i * f + a, j * f + b] for a in range(f) for b in range(f)]
                op = [p for p in blk if p[3] >= 128]
                if len(op) * 2 >= len(blk):
                    dst[i, j] = max(set(op), key=op.count)
        return out
    return im.resize((tam, tam), Image.NEAREST)


def unificar(im, tam=32, sin_rojo=False, tol=24, umbral=40, max_colores=11):
    im = quitar_fondo(im.convert("RGBA"), tol)
    im = reducir(im, tam)
    s = Sprite(tam, tam)
    s.img = im
    s.px = im.load()
    # Alfa binaria.
    for y in range(tam):
        for x in range(tam):
            if s.px[x, y][3] < 128:
                s.px[x, y] = (0, 0, 0, 0)
    # Quitar el contorno propio: píxeles opacos de borde muy oscuros.
    borde = []
    for y in range(tam):
        for x in range(tam):
            r, g, b, a = s.px[x, y]
            if not a or (r + g + b) / 3 > umbral:
                continue
            if any(s.get(x + dx, y + dy)[3] == 0 for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                borde.append((x, y))
    for (x, y) in borde:
        s.px[x, y] = (0, 0, 0, 0)
    # Paleta estricta (sin rojos si se pide).
    s.lock_palette(SIN_ROJO if sin_rojo else PALETA)
    s.replace(CONTORNO, "olive_black")   # el negro queda reservado al contorno
    limpiar_motas(s)
    limitar_colores(s, max_colores)
    s.outline()
    return s


def limpiar_motas(s):
    """Quita el ruido de difusión: un píxel distinto de sus 4 vecinos opacos, que a su vez coinciden
    en mayoría, toma el color de esa mayoría. Acerca el tramado de Pixelab a nuestras masas limpias."""
    src = s.img.copy().load()
    for y in range(s.h):
        for x in range(s.w):
            p = src[x, y]
            if not p[3]:
                continue
            vec = [src[x + dx, y + dy] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
                   if 0 <= x + dx < s.w and 0 <= y + dy < s.h]
            if len(vec) < 4 or any(v[3] == 0 for v in vec) or p in vec:
                continue
            m = max(set(vec), key=vec.count)
            if vec.count(m) >= 3:
                s.px[x, y] = m


def limitar_colores(s, n):
    """Deja como mucho n colores: los menos usados pasan al color usado más cercano."""
    cuenta = {}
    for y in range(s.h):
        for x in range(s.w):
            p = s.px[x, y]
            if p[3]:
                cuenta[p] = cuenta.get(p, 0) + 1
    if len(cuenta) <= n:
        return
    quedan = sorted(cuenta, key=cuenta.get, reverse=True)[:n]
    mapa = {c: min(quedan, key=lambda q: sum((a - b) ** 2 for a, b in zip(c[:3], q[:3]))) for c in cuenta}
    for y in range(s.h):
        for x in range(s.w):
            p = s.px[x, y]
            if p[3]:
                s.px[x, y] = mapa[p]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("entrada")
    ap.add_argument("salida")
    ap.add_argument("--tam", type=int, default=32)
    ap.add_argument("--sin-rojo", action="store_true")
    ap.add_argument("--tol", type=int, default=24)
    ap.add_argument("--umbral-contorno", type=int, default=40)
    ap.add_argument("--max-colores", type=int, default=11, help="sin contar el contorno")
    a = ap.parse_args()
    ent = Path(a.entrada)
    archivos = sorted(ent.glob("*.png")) if ent.is_dir() else [ent]
    out = Path(a.salida)
    out.mkdir(parents=True, exist_ok=True)
    antes, despues = [], []
    for f in archivos:
        im = Image.open(f)
        s = unificar(im, a.tam, a.sin_rojo, a.tol, a.umbral_contorno, a.max_colores)
        s.save(out / f.name)
        print(f.name, check(s, recurso=a.sin_rojo) or "OK")
        o = Sprite(a.tam, a.tam)
        o.img = reducir(quitar_fondo(im.convert("RGBA"), a.tol), a.tam)
        o.px = o.img.load()
        antes.append(o)
        despues.append(s)
    for k in range(0, len(antes), 8):
        contact_sheet([antes[k:k + 8], despues[k:k + 8]], out / f"antes_despues_{k // 8}.png", scale=6,
                      labels=["antes", "después"])


if __name__ == "__main__":
    main()
