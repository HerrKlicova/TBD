"""Adapta una hoja de pixelfx (patch-gg/pixelfx, MIT) a Vía Muerta.

pixelfx exporta hojas a 30 fps, en lienzos grandes y con su propia paleta (rojos de fuego,
brillo semitransparente). Esto la deja lista para la capa pixelada del juego:
  1. Toma 1 de cada N fotogramas (--fps 12 por defecto) y corta la cola casi vacía.
  2. Recorta todos los fotogramas a la misma caja (la unión de lo opaco) y ajusta el pivote.
  3. Alfa binaria con tramado Bayer (sin semitransparencias) y paleta de 32 estricta.
  4. Sin rojos (D-N46): los rojos pasan a óxido o ámbar según su luminosidad.
  5. Escala opcional (--escala 0.5) con vecino más cercano, para igualar el tamaño de píxel del mundo.
Salida: <nombre>.png (hoja), <nombre>.json (estilo Aseprite, con fps y pivote) y contacto.png.

Uso:
  python fx_adaptar.py <hoja.png> <hoja.json> <carpeta_salida> [--fps 12] [--escala 1] [--max N] [--destello]
                       [--nombre vm_vfx_x]

Duraciones aprobadas (Lucía, 6-nov): impacto de bala 6 fotogramas, explosión pequeña 12–14,
explosión de tierra 16–18, a 12 fps.
"""
import argparse
import json
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, PALETA, ROJOS, Sprite, contact_sheet  # noqa: E402

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
SIN_ROJO = [c for c in PALETA if c not in ROJOS]


def cercano(c, cache):
    if c not in cache:
        r, g, b = c
        cache[c] = min(SIN_ROJO, key=lambda p: (p[0] - r) ** 2 + (p[1] - g) ** 2 + (p[2] - b) ** 2)
    return cache[c]


def adaptar_fotograma(im, cache):
    im = im.convert("RGBA")
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            if a / 255 * 16 <= BAYER[y % 4][x % 4]:
                px[x, y] = (0, 0, 0, 0)
                continue
            # Los rojos saturados del fuego van a ámbar (claros) u óxido (oscuros), nunca al rojo.
            if r > 1.6 * g and r > 1.6 * b:
                c = PAL["amber"][:3] if r > 170 else PAL["rust"][:3]
            else:
                c = cercano((r, g, b), cache)
            px[x, y] = c + (255,)
    return im


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("png")
    ap.add_argument("json")
    ap.add_argument("salida")
    ap.add_argument("--fps", type=int, default=12)
    ap.add_argument("--escala", type=float, default=1.0)
    ap.add_argument("--nombre", default=None)
    ap.add_argument("--max", type=int, default=0, help="fotogramas máximos (0 = todos); el resto lo pone el emisor de humo")
    ap.add_argument("--destello", action="store_true",
                    help="saca el primer fotograma (el destello) a su propio PNG para la capa VFX_HD")
    a = ap.parse_args()

    meta = json.loads(Path(a.json).read_text())
    hoja = Image.open(a.png).convert("RGBA")
    info = meta["meta"]["pixelfx"]
    fps0, (fw, fh), (pvx, pvy) = info["fps"], info["frameSize"], info["pivot"]
    paso = max(1, round(fps0 / a.fps))
    marcos = [hoja.crop((f["frame"]["x"], f["frame"]["y"], f["frame"]["x"] + fw, f["frame"]["y"] + fh))
              for f in meta["frames"]][::paso]
    if a.escala != 1:
        nw, nh = max(1, round(fw * a.escala)), max(1, round(fh * a.escala))
        marcos = [m.resize((nw, nh), Image.NEAREST) for m in marcos]
        fw, fh, pvx, pvy = nw, nh, round(pvx * a.escala), round(pvy * a.escala)
    destello = None
    if a.destello:
        destello, marcos = marcos[0], marcos[1:]
    if a.max:
        marcos = marcos[:a.max]
    cache = {}
    marcos = [adaptar_fotograma(m, cache) for m in marcos]
    # Corta la cola: fotogramas finales con menos del 3 % de los píxeles del fotograma más lleno
    # (restos como la marca de quemado, que en el juego pone un decal).
    llenos = [m.width * m.height - m.getchannel("A").histogram()[0] for m in marcos]
    while len(marcos) > 1 and llenos[-1] < 0.03 * max(llenos):
        marcos.pop()
        llenos.pop()
    cajas = [m.getbbox() for m in marcos if m.getbbox()]
    x0, y0 = min(b[0] for b in cajas), min(b[1] for b in cajas)
    x1, y1 = max(b[2] for b in cajas), max(b[3] for b in cajas)
    marcos = [m.crop((x0, y0, x1, y1)) for m in marcos]
    w, h = x1 - x0, y1 - y0
    pivote = [pvx - x0, pvy - y0]

    nombre = a.nombre or "vm_vfx_" + Path(a.png).stem.replace("-", "_")
    out = Path(a.salida)
    out.mkdir(parents=True, exist_ok=True)
    cols = min(len(marcos), 8)
    filas = (len(marcos) + cols - 1) // cols
    sheet = Image.new("RGBA", (w * cols, h * filas), (0, 0, 0, 0))
    frames = []
    dur = round(1000 / a.fps)
    for i, m in enumerate(marcos):
        x, y = (i % cols) * w, (i // cols) * h
        sheet.alpha_composite(m, (x, y))
        frames.append({"filename": f"{nombre} {i}", "frame": {"x": x, "y": y, "w": w, "h": h}, "rotated": False,
                       "trimmed": False, "spriteSourceSize": {"x": 0, "y": 0, "w": w, "h": h},
                       "sourceSize": {"w": w, "h": h}, "duration": dur})
    sheet.save(out / f"{nombre}.png")
    if destello is not None:
        # El destello va a resolución completa en Unity (VFX_HD, aditivo): se guarda sin bloquear la paleta
        # ni binarizar la alfa, recortado a la misma caja que la hoja para que comparta pivote.
        destello.crop((x0, y0, x1, y1)).save(out / f"{nombre}_destello_vfxhd.png")
    (out / f"{nombre}.json").write_text(json.dumps({"frames": frames, "meta": {
        "app": "pixelvm fx_adaptar (desde pixelfx, MIT)", "image": f"{nombre}.png", "format": "RGBA8888",
        "size": {"w": sheet.width, "h": sheet.height}, "scale": "1",
        "frameTags": [{"name": nombre, "from": 0, "to": len(frames) - 1, "direction": "forward"}],
        "viaMuerta": {"fps": a.fps, "loop": info["loop"], "frames": len(frames), "frameSize": [w, h],
                      "pivot": pivote, "origen": info["id"]}}}, indent=1))
    sprites = []
    for m in marcos:
        s = Sprite(w, h)
        s.img = m
        s.px = m.load()
        sprites.append(s)
    contact_sheet([sprites[i:i + 8] for i in range(0, len(sprites), 8)], out / f"contacto_{nombre}.png", scale=2)
    print(f"{nombre}: {len(frames)} fotogramas de {w}x{h} a {a.fps} fps, pivote {pivote}")


if __name__ == "__main__":
    main()
