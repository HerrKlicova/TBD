"""A8 · Humo de escape de locomotora diésel, 64x64, bucle de 8 fotogramas, por código.
Bruno (Arte técnico), 6-nov-2026. Técnica de bocanadas (pixelfx/Pixelbolt): cada bocanada
depende solo de su edad, y las edades son periódicas, así que el bucle cierra sin salto.
Paleta: grises de hormigón y oliva, sin negro puro dentro del humo; desvanecido con tramado
Bayer (alfa binaria, como pide el filtro de punto)."""
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, Sprite, check, contact_sheet  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_a8")
F, W, H = 8, 64, 64
N = 11                      # bocanadas vivas a la vez
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
# Del escape (denso, oscuro) al humo frío (claro y ralo).
TONOS = ["olive_shadow", "grey_brown", "olive_grey", "concrete", "concrete_light"]
LUZ = {"olive_shadow": "grey_brown", "grey_brown": "olive_grey", "olive_grey": "concrete",
       "concrete": "concrete_light", "concrete_light": "grey_light"}


def _blob(s, cx, cy, r, tono, densidad, solo_vacio=False):
    for j in range(int(cy - r) - 1, int(cy + r) + 2):
        for i in range(int(cx - r) - 1, int(cx + r) + 2):
            d = math.hypot(i + .5 - cx, (j + .5 - cy) * 1.1)
            if d > r:
                continue
            borde = 1 - d / r
            if BAYER[j % 4][i % 4] / 16 >= densidad * min(1, borde * 4 + 0.5):
                continue
            # Luz desde arriba a la izquierda: media luna clara (círculo desplazado), no un corte recto.
            dl = math.hypot(i + .5 - (cx - r * 0.35), (j + .5 - (cy - r * 0.4)) * 1.1)
            s.set(i, j, LUZ[tono] if dl < r * 0.62 else tono, "empty" if solo_vacio else None)


def bocanada(s, age, semilla):
    # Sube, se abre y deriva a la derecha (viento por la marcha del tren).
    x0, y0 = 18, 60
    rise = 46 * (1 - (1 - age) ** 1.5)
    cx = x0 + 22 * age ** 1.4
    cy = y0 - rise
    r = 2.5 + 7.5 * age ** 0.75
    tono = TONOS[min(len(TONOS) - 1, int(age ** 0.8 * len(TONOS)))]
    densidad = 1.0 if age < 0.6 else max(0.0, 1 - (age - 0.6) / 0.4)
    # Cada bocanada son 3 lóbulos que giran despacio: forma de coliflor, no de bola.
    for n in range(3):
        ang = semilla * 1.7 + n * 2.1 + age * 2.5
        _blob(s, cx + math.cos(ang) * r * 0.45, cy + math.sin(ang) * r * 0.35, r * 0.72, tono, densidad)


def fotograma(f):
    s = Sprite(W, H)
    edades = sorted((((f / F) + k / N) % 1.0, k) for k in range(N))
    for age, k in reversed(edades):   # las viejas detrás, la recién salida delante
        bocanada(s, age, k)
    return s


if __name__ == "__main__":
    frames = [fotograma(f) for f in range(F)]
    hoja = Image.new("RGBA", (W * F, H), (0, 0, 0, 0))
    for f, s in enumerate(frames):
        hoja.alpha_composite(s.img, (W * f, 0))
        avisos = [a for a in check(s) if "silueta" not in a and "contraste" not in a]
        print("f", f, avisos or "OK")
    OUT.mkdir(parents=True, exist_ok=True)
    hoja.save(OUT / "vm_vfx_humo_escape_8f.png")
    big = [s.img.resize((W * 4, H * 4), Image.NEAREST) for s in frames]
    fondo = [Image.alpha_composite(Image.new("RGBA", b.size, PAL["olive"]), b) for b in big]
    fondo[0].save(OUT / "vm_vfx_humo_escape_x4.gif", save_all=True, append_images=fondo[1:],
                  duration=100, loop=0, disposal=2)
    contact_sheet([frames], OUT / "contacto_a8.png", scale=3)
