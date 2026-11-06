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
N = 4                      # bocanadas vivas a la vez: una nueva cada 2 fotogramas
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
# Del escape (carbón, denso) al humo frío (claro y ralo). Revisión de Lucía 6-nov: base carbón, no oliva.
TONOS = ["slate_dark", "slate", "concrete", "concrete_light"]
LUZ = {"slate_dark": "slate", "slate": "concrete", "concrete": "concrete_light",
       "concrete_light": "grey_light"}
ESCAPE = (46, 58)          # chimenea abajo a la derecha; el tren avanza a la derecha


SOMBRA = {"slate_dark": "black", "slate": "slate_dark", "concrete": "slate", "concrete_light": "concrete"}


def _ruido(i, j, f):
    """Ruido determinista por píxel y fotograma (hash entero), para deshilachar sin rejilla."""
    h = (i * 374761393 + j * 668265263 + f * 2147483647) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return (h & 0xFFFF) / 0xFFFF


def _blob(s, cx, cy, r, tono, densidad, f=0):
    for j in range(int(cy - r) - 1, int(cy + r) + 2):
        for i in range(int(cx - r) - 1, int(cx + r) + 2):
            d = math.hypot(i + .5 - cx, (j + .5 - cy) * 1.1)
            if d > r:
                continue
            borde = 1 - d / r
            # Umbral mitad Bayer, mitad ruido: el desvanecido se deshilacha en vez de formar un damero.
            umbral = 0.45 * BAYER[j % 4][i % 4] / 16 + 0.55 * _ruido(i, j, f)
            if umbral >= densidad * min(1, borde * 5 + 0.4):
                continue
            # Volumen: luz arriba a la izquierda, sombra abajo a la derecha, tono medio en el centro.
            ox, oy = (i + .5 - cx) / r, (j + .5 - cy) / r
            if ox + oy < -0.55:
                c = LUZ[tono]
            elif ox + oy > 0.75:
                c = SOMBRA[tono]
            else:
                c = tono
            s.set(i, j, c)


def bocanada(s, age, semilla, f=0):
    """Nace pequeña y densa en el escape, sube, se abre, se queda atrás (izquierda)
    por el avance del tren y al final se deshace en lóbulos sueltos."""
    x0, y0 = ESCAPE
    rise = 44 * (1 - (1 - age) ** 1.7)
    cx = x0 - 30 * age ** 1.25
    cy = y0 - rise
    r = 2 + 9 * age ** 0.7
    tono = TONOS[min(len(TONOS) - 1, int(age * len(TONOS)))]
    densidad = 1.0 if age < 0.5 else max(0.0, 1 - (age - 0.5) / 0.5) ** 0.8
    # Lóbulos: juntos al nacer, se separan al envejecer (la bocanada se rompe).
    nl = 1 if age < 0.12 else 4
    sep = 0.25 + 0.9 * age
    for n in range(nl):
        ang = semilla * 1.9 + n * (2 * math.pi / nl) + age * 1.5
        _blob(s, cx + math.cos(ang) * r * sep * 0.6, cy + math.sin(ang) * r * sep * 0.45,
              r * (0.75 - 0.15 * age), tono, densidad, f)


def fotograma(f):
    s = Sprite(W, H)
    edades = sorted((((f / F) + k / N) % 1.0, k) for k in range(N))
    for age, k in reversed(edades):   # las viejas detrás, la recién salida delante
        bocanada(s, age, k, f)
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
