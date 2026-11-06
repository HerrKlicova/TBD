"""Zombi LOD de masa (D-N58, Lucía): la horda a distancia se lee como mancha, no como ruido.
Bruno (Arte técnico), 6-nov-2026.

8x8 px (mancha de 4x5), vista cenital, mirando al norte. Cuerpo en #1c200c y cabeza de 1 px en
#88885c (olive_light, revisión de Lucía); sin contorno, sin cara clara y sin variantes. Andar de 4 fotogramas con 1 px de
balanceo lateral y paso alterno. Cadáver de masa: mancha plana en diagonal, un escalón más apagada.
Salida: atlas_zombi_lod.png (5 columnas de 8 px: andar 0–3 y cadáver), un PNG por fotograma, JSON
y prueba a x1 y x8 sobre los suelos.
También rehace atlas_zombi_muerte.png (5 fotogramas x 6 variantes, mismo orden de filas que
atlas_zombi_andar: orden alfabético de los archivos) para integrar la muerte de 32 px.
"""
import json
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, Sprite  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_lod")
ZOMBI = Path(sys.argv[2]) if len(sys.argv) > 2 else None    # carpeta con vm_zombi_*_muerte.png

# Revisión de Lucía (6-nov, 10:00): mancha compacta y redondeada de 4x5 sin brazos (a 8 px los brazos
# convierten cada zombi en una letra). Cuerpo #1c200c, cabeza de 1 px #88885c, 1 px de balanceo.
CUERPO, CABEZA = "olive_black", "olive_light"
CUERPO_M, CABEZA_M = "olive_black", "olive_shadow"          # cadáver: forma plana y cabeza apagada

BASE = [
    "........",
    "...hb...",   # cabeza de 1 px al norte
    "..bbbb..",
    "..bbbb..",
    "..bbbb..",
    "...bb...",
    "........",
    "........",
]
BALANCEO = [0, 1, 1, 0]                                     # solo 1 px

CADAVER = [
    "........",
    "........",
    ".....bb.",
    "..hbbbb.",
    ".bbbbb..",
    ".bbb....",
    "........",
    "........",
]


def pinta(filas, dx, cuerpo, cabeza):
    s = Sprite(8, 8)
    for y, fila in enumerate(filas):
        for x, ch in enumerate(fila):
            if ch in "bh" and 0 <= x + dx < 8:
                s.set(x + dx, y, cuerpo if ch == "b" else cabeza)
    return s


def andar(f):
    return pinta(BASE, BALANCEO[f], CUERPO, CABEZA)


def cadaver():
    return pinta(CADAVER, 0, CUERPO_M, CABEZA_M)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    frames = [andar(f) for f in range(4)] + [cadaver()]
    atlas = Image.new("RGBA", (8 * 5, 8), (0, 0, 0, 0))
    for k, s in enumerate(frames):
        atlas.alpha_composite(s.img, (8 * k, 0))
        s.save(OUT / (f"vm_zombi_lod_andar_{k}.png" if k < 4 else "vm_zombi_lod_cadaver.png"))
    atlas.save(OUT / "atlas_zombi_lod.png")
    (OUT / "atlas_zombi_lod.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "frameSize": [8, 8], "columnas": ["andar0", "andar1", "andar2", "andar3", "cadaver"],
        "fps_andar": 6, "loop_andar": True, "pivot": [0.5, 0.5], "orientacion": "norte (+Y de la imagen)",
        "nota": "D-N58: sin contorno ni variantes; fotograma inicial al azar por zombi"}}}, indent=1))

    # Prueba: una masa de zombis LOD a x1 (como en juego) y a x8, sobre los suelos.
    a7 = Path("/mnt/project-files/docs/arte/pixel-codigo/a7")
    fondos = [Image.new("RGBA", (64, 64), PAL["olive"])]
    fondos += [Image.open(a7 / f"tex_{n}_64.png").convert("RGBA") for n in ("tierra", "balasto", "hormigon", "asfalto")
               if (a7 / f"tex_{n}_64.png").exists()]
    import random
    rnd = random.Random(7)
    pos = [(rnd.randrange(0, 56), rnd.randrange(0, 56), rnd.randrange(4)) for _ in range(40)]
    muertos = [(rnd.randrange(0, 56), rnd.randrange(0, 56)) for _ in range(6)]
    hoja = Image.new("RGBA", (len(fondos) * (64 * 4 + 8) + 8, 64 * 4 + 16 + 64 + 8), PAL["navy"])
    for i, fo in enumerate(fondos):
        m = fo.copy()
        for x, y in muertos:
            m.alpha_composite(frames[4].img, (x, y))
        for x, y, f in pos:
            m.alpha_composite(frames[f].img, (x, y))
        hoja.paste(m.resize((256, 256), Image.NEAREST), (8 + i * 264, 8))
        hoja.paste(m, (8 + i * 264, 8 + 256 + 8))
    hoja.save(OUT / "prueba_masa_lod.png")
    grande = Image.new("RGBA", (5 * 72 + 8, 80), PAL["olive"])
    for k, s in enumerate(frames):
        grande.alpha_composite(s.img.resize((64, 64), Image.NEAREST), (8 + k * 72, 8))
    grande.save(OUT / "contacto_lod_x8.png")

    # Atlas de muerte de 32 px para integrar (mismo orden de filas que atlas_zombi_andar).
    if ZOMBI and ZOMBI.exists():
        hojas = sorted(f for f in ZOMBI.glob("vm_zombi_*_muerte.png") if "pardo" not in f.name)
        at = Image.new("RGBA", (32 * 5, 32 * len(hojas)), (0, 0, 0, 0))
        for r, f in enumerate(hojas):
            at.alpha_composite(Image.open(f).convert("RGBA"), (0, 32 * r))
        at.save(OUT / "atlas_zombi_muerte.png")
        (OUT / "atlas_zombi_muerte.json").write_text(json.dumps({"meta": {"viaMuerta": {
            "frameSize": [32, 32], "frames": 5, "fps": 8, "loop": False, "pivot": [0.5, 0.53],
            "filas": [f.name.replace("vm_zombi_", "").replace("_muerte.png", "") for f in hojas],
            "nota": "mismo orden de filas que atlas_zombi_andar; el último fotograma se queda"}}}, indent=1))
        print("atlas muerte:", [f.name for f in hojas])
