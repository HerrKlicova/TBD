"""Fondo con parallax para la vista del tren (Lucía, para que se note la velocidad). Bruno (Arte técnico).

La cámara de Marcos es ortho 7 a 1080p con escalón de 1/6, o sea 320x180 píxeles de juego. Cada tira mide 640 px
de ancho (dos pantallas) y repite en X sin costura. Marcos las mueve más despacio que el primer plano:
  cielo     640x120  opaco, degradado tramado. De noche lleva estrellas y el resplandor de Sagunto en el horizonte
  sierra    640x56   sierra de Sagunto (Calderona) lejana, plana, con perspectiva aérea; fondo transparente
  colinas   640x44   lomas cercanas con bancales, algarrobos y naranjos, y de noche alguna casa encendida
Cada tira tiene versión de día y de noche. Sin #404460 (rim propio), sin rojos ni ámbar.
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import _h, tramado  # noqa: E402

W = 640

COLORES = {
    "dia": {"cielo": ["concrete_light", "grey_light", "bone"], "estrella": None,
            "sierra": "concrete", "sierra_luz": "concrete_light", "sierra_sombra": "olive_grey",
            "colina": "olive", "colina_luz": "olive_light", "colina_sombra": "olive_dark",
            "bancal": "ochre_dark", "arbol": "olive_dark", "arbol_luz": "olive", "casa": "bone", "ventana": None},
    "noche": {"cielo": ["navy", "slate_dark", "grey_brown"], "estrella": "bone",
              "sierra": "navy", "sierra_luz": "grey_brown", "sierra_sombra": "navy",
              # D-N110 (Lucía): nada en 1c200c ni 442c0c junto a la vía, que se leería como horda
              "colina": "olive_shadow", "colina_luz": "grey_brown", "colina_sombra": "grey_brown",
              "bancal": "olive_dark", "arbol": "olive_dark", "arbol_luz": "olive_grey", "casa": "grey_brown",
              "ventana": "cream"},
}


def perfil(semilla, armonicos):
    """Altura 0–1 por columna, periódica en W: suma de senos cuyo periodo divide a W."""
    fases = [_h(k, 0, semilla) * math.tau for k in range(len(armonicos))]
    alt = []
    for x in range(W):
        v = sum(a * math.sin(math.tau * n * x / W + f) for (n, a), f in zip(armonicos, fases))
        alt.append(v)
    lo, hi = min(alt), max(alt)
    return [(v - lo) / (hi - lo) for v in alt]


def cielo(hora):
    c = COLORES[hora]
    s = Sprite(W, 120)
    for y in range(120):
        t = y / 119
        for x in range(W):
            if t < .55:
                col = c["cielo"][1] if tramado(x, y, max(0, (t - .25) / .3)) else c["cielo"][0]
            else:
                col = c["cielo"][2] if tramado(x, y, (t - .55) / .45 * (.7 if hora == "noche" else 1)) else c["cielo"][1]
            s.set(x, y, col)
    if c["estrella"]:
        for k in range(70):
            x, y = int(_h(k, 1, 901) * W), int(_h(k, 2, 901) ** 1.6 * 70)
            s.set(x, y, c["estrella"] if k % 5 else "grey_light")
    return s


def sierra(hora):
    """Sierra lejana: picos suaves, ladera de sol (izquierda) un escalón más clara, sombra tramada a la derecha."""
    c = COLORES[hora]
    s = Sprite(W, 56)
    p = perfil(910, [(1, 1.0), (2, .6), (3, .45), (5, .3), (9, .18), (17, .08)])
    for x in range(W):
        top = int(56 - 10 - p[x] * 42)
        pend = p[(x + 1) % W] - p[x - 1]
        for y in range(top, 56):
            col = c["sierra"]
            prof = y - top
            if prof < 3 and pend > .002:
                col = c["sierra_luz"]
            elif pend < 0 and tramado(x, y, min(.6, -pend * 90) * max(0, 1 - prof / 30)):
                col = c["sierra_sombra"]                # ladera de sombra: más densa cuanto más pendiente
            s.set(x, y, col)
    return s


def colinas(hora):
    """Lomas cercanas: bancales horizontales, árboles redondos sueltos y alguna casa (con ventana de noche)."""
    c = COLORES[hora]
    s = Sprite(W, 44)
    p = perfil(920, [(2, 1.0), (3, .5), (7, .3), (13, .15)])
    tops = [int(44 - 8 - p[x] * 24) for x in range(W)]
    for x in range(W):
        for y in range(tops[x], 44):
            col = c["colina"]
            if y - tops[x] < 2:
                col = c["colina_luz"]
            elif (y + int(_h(x // 23, 0, 921) * 4)) % 7 == 0:
                col = c["bancal"]                       # muro de bancal
            elif tramado(x, y, max(0, (y - 30) / 14) * .6):
                col = c["colina_sombra"]
            s.set(x, y, col)
    for k in range(60):                                  # algarrobos y naranjos
        x = int(_h(k, 0, 930) * W)
        y = tops[x] + 2 + int(_h(k, 1, 930) * 10)
        r = 2 + int(_h(k, 2, 930) * 2)
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r + 1:
                    col = c["arbol_luz"] if dy < 0 and dx < 0 else c["arbol"]
                    s.set((x + dx) % W, y + dy, col)
    for k in range(5):                                   # casas de campo
        x = int(_h(k, 3, 940) * W)
        y = tops[x] + 4
        for dy in range(4):
            for dx in range(6):
                s.set((x + dx) % W, y + dy, c["casa"])
        if c["ventana"] and k % 2 == 0:
            s.set((x + 2) % W, y + 2, c["ventana"])
    return s


CAPAS = {"cielo": (cielo, 0.0), "sierra": (sierra, 0.08), "colinas": (colinas, 0.25)}   # factor de parallax sugerido


def exportar(out):
    out = Path(out)
    todo = {}
    for hora in ("dia", "noche"):
        for nombre, (fn, fac) in CAPAS.items():
            s = fn(hora)
            base = f"bg_tren_{nombre}_{hora}"
            s.img.save(out / f"{base}.png")
            meta = {"image": f"{base}.png", "size": [s.w, s.h], "wrapX": True, "parallax": fac,
                    "pixelesJuego": [320, 180], "origen": "pixelvm, build_parallax_tren.py",
                    "nota": "parallax = fracción de la velocidad del primer plano; sugerido, Marcos decide"}
            (out / f"{base}.json").write_text(json.dumps(meta, indent=1))
            todo[(hora, nombre)] = s
    return todo


def composicion(todo, hora, desfase=0):
    """Pantalla de 320x180 con las tres capas, como la vería la cámara (para revisar)."""
    im = Image.new("RGBA", (320, 180), PAL["olive_dark" if hora == "dia" else "olive_shadow"])
    for nombre, y in (("cielo", 0), ("sierra", 64), ("colinas", 86)):
        s = todo[(hora, nombre)]
        fac = CAPAS[nombre][1]
        x0 = int(desfase * fac) % W
        tira = Image.new("RGBA", (W * 2, s.h))
        tira.paste(s.img, (0, 0))
        tira.paste(s.img, (W, 0))
        im.alpha_composite(tira.crop((x0, 0, x0 + 320, s.h)), (0, y))
    return im


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_parallax_tren")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 3
    hoja = Image.new("RGBA", (320 * E * 2 + 8, 180 * E * 2 + 8), PAL["black"])
    for i, hora in enumerate(("dia", "noche")):
        for j, d in enumerate((0, 2400)):
            hoja.paste(composicion(todo, hora, d).resize((320 * E, 180 * E), Image.NEAREST),
                       (j * (320 * E + 8), i * (180 * E + 8)))
    hoja.save(OUT / "contacto_parallax_tren_x3.png")
    print("ok")
