"""Zombi de la Colmena, 32 px, vista cenital, mirando al norte. Bruno (Arte técnico), 6-nov-2026.

Andar en bucle de 4 fotogramas y muerte en 5 (sin bucle, el último se queda en el suelo).
Reglas: paleta de 32, contorno de 1 px, sin rim ni brillos, sin sangre ni gore (regla 11: la Colmena
se insinúa), proporciones adultas. Valor partido (D-N57): lo que mira arriba (cabeza, hombros,
brazos) claro; costados y piernas oscuros, para leerse en la trinchera y en el llano ocre.
Se distingue de la tropa por la ropa civil en gris morado y la piel pálida, no por el color de alerta.
"""
import json
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, Sprite, check, contact_sheet  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_zombi")

PIEL, PIEL_S = "grey_light", "concrete"          # piel pálida y su lado en sombra (plano)
ROPA, ROPA_S = "concrete_light", "purple_grey"   # camisa: hombros arriba claros, costados oscuros
PELO = "grey_brown"
PIERNA, PIE = "slate", "slate_dark"


def cuerpo(s, cx, cy, brazo_i, brazo_d, pie_i, pie_d, cabeza_dx=0):
    """Zombi de pie desde arriba. brazo_* = cuánto se adelanta cada brazo; pie_* = paso.
    Desde arriba se ve sobre todo pelo y hombros: el pelo oscuro separa la cabeza de la camisa clara."""
    # Piernas y pies (asoman por detrás de los hombros, al sur).
    s.rect(cx - 4, cy + 3 - pie_i, 3, 5, PIERNA)
    s.rect(cx + 2, cy + 3 - pie_d, 3, 5, PIERNA)
    s.rect(cx - 4, cy + 7 - pie_i, 3, 2, PIE)
    s.rect(cx + 2, cy + 7 - pie_d, 3, 2, PIE)
    # Torso y hombros (anchos: espalda adulta).
    s.ellipse(cx - 7, cy - 3, cx + 7, cy + 4, ROPA_S)
    s.ellipse(cx - 6, cy - 3, cx + 6, cy + 2, ROPA)
    s.set(cx + 3, cy + 1, ROPA_S)                 # roto en la camisa
    s.set(cx - 3, cy + 2, "olive_grey")           # tierra (no sangre)
    # Brazos estirados hacia delante, desparejos, 2 px de ancho.
    for x0, br in ((cx - 7, brazo_i), (cx + 6, brazo_d)):
        s.rect(x0, cy - 5 - br, 2, 5 + br, ROPA_S)
        s.vline(x0 + (1 if x0 < cx else 0), cy - 5 - br, cy - 2, ROPA)
        s.rect(x0, cy - 7 - br, 2, 2, PIEL)       # manos
    # Cabeza adelantada (asoma al norte de los hombros), caída a un lado; pelo ralo.
    hx = cx + cabeza_dx
    s.ellipse(hx - 3, cy - 7, hx + 3, cy - 1, PELO)
    s.hline(hx - 1, hx + 1, cy - 7, PIEL)          # frente
    s.set(hx + 1, cy - 4, PIEL)                    # calva
    s.set(hx - 2, cy - 3, PIEL_S)


ANDAR = [  # (brazo_i, brazo_d, pie_i, pie_d, cabeza_dx, balanceo_x)
    (3, 1, 2, 0, -1, 0),
    (2, 2, 1, 1, 0, 1),
    (1, 3, 0, 2, 1, 0),
    (2, 2, 1, 1, 0, -1),
]


def andar(f):
    s = Sprite()
    bi, bd, pi, pd, cdx, bx = ANDAR[f]
    cuerpo(s, 16 + bx, 17, bi, bd, pi, pd, cdx)
    s.outline()
    return s


def tendido(s, cx, y0, largo, abre):
    """Cuerpo boca abajo, cabeza al norte. largo = de la coronilla a los pies; abre = brazos abiertos."""
    s.rect(cx - 4, y0 + largo - 10, 3, 9, PIERNA)
    s.rect(cx + 1, y0 + largo - 10, 3, 9, PIERNA)
    s.rect(cx - 4, y0 + largo - 2, 3, 2, PIE)
    s.rect(cx + 1, y0 + largo - 2, 3, 2, PIE)
    h = largo - 15                                    # espalda: hombros anchos, cadera estrecha
    s.poly([(cx - 7, y0 + 6), (cx + 6, y0 + 6), (cx + 4, y0 + 6 + h), (cx - 5, y0 + 6 + h)], ROPA_S)
    s.poly([(cx - 6, y0 + 6), (cx + 5, y0 + 6), (cx + 3, y0 + 4 + h), (cx - 4, y0 + 4 + h)], ROPA)
    s.set(cx + 2, y0 + 9, ROPA_S)
    s.set(cx - 2, y0 + 10, "olive_grey")
    for sg in (-1, 1):                                # brazos de 2 px hacia delante, abiertos
        x0 = cx - 7 if sg < 0 else cx + 5
        x1 = x0 + sg * (2 + abre)
        for k in (0, 1):
            s.line(x0 + k, y0 + 7, x1 + k, y0 + 1, ROPA_S)
        s.rect(x1, y0 - 1, 2, 2, PIEL)
    s.ellipse(cx - 3, y0, cx + 2, y0 + 6, PELO)        # cabeza
    s.set(cx, y0 + 2, PIEL)
    s.set(cx - 1, y0 + 4, PIEL_S)


def muerte(f):
    s = Sprite()
    if f == 0:                        # recibe el golpe: se encoge, brazos caen
        cuerpo(s, 16, 18, 0, 0, 1, 1, 0)
    elif f == 1:                      # de rodillas: se acorta y se inclina adelante
        cuerpo(s, 16, 16, 0, -1, 2, 2, 0)
        for x in range(32):           # quita los pies (rodillas)
            for y in range(22, 32):
                s.set(x, y, None)
    elif f == 2:                      # cayendo de bruces
        tendido(s, 16, 8, 20, 0)
    elif f == 3:                      # en el suelo
        tendido(s, 16, 4, 26, 1)
    else:                             # quieto: se queda así
        tendido(s, 16, 4, 26, 2)
    s.outline()
    return s


def exportar(nombre, frames, fps, bucle):
    W = H = 32
    hoja = Image.new("RGBA", (W * len(frames), H), (0, 0, 0, 0))
    for f, s in enumerate(frames):
        hoja.alpha_composite(s.img, (W * f, 0))
        s.save(OUT / f"{nombre}_{f}.png")
    hoja.save(OUT / f"{nombre}.png")
    dur = round(1000 / fps)
    meta = {"frames": [{"filename": f"{nombre} {f}", "frame": {"x": W * f, "y": 0, "w": W, "h": H},
                        "rotated": False, "trimmed": False, "spriteSourceSize": {"x": 0, "y": 0, "w": W, "h": H},
                        "sourceSize": {"w": W, "h": H}, "duration": dur} for f in range(len(frames))],
            "meta": {"image": f"{nombre}.png", "size": {"w": W * len(frames), "h": H}, "format": "RGBA8888",
                     "viaMuerta": {"fps": fps, "loop": bucle, "frames": len(frames), "frameSize": [W, H],
                                   "pivot": [0.5, 0.53], "orientacion": "norte (+Y de la imagen)",
                                   "origen": "pixelvm, build_zombi.py"}}}
    (OUT / f"{nombre}.json").write_text(json.dumps(meta, indent=1))
    fondo = PAL["olive"]
    big = [Image.alpha_composite(Image.new("RGBA", (W, H), fondo), s.img).resize((W * 6, H * 6), Image.NEAREST)
           for s in frames]
    big[0].save(OUT / f"{nombre}_x6.gif", save_all=True, append_images=big[1:], duration=dur,
                loop=0 if bucle else 1, disposal=2)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    a = [andar(f) for f in range(4)]
    m = [muerte(f) for f in range(5)]
    for k, s in enumerate(a + m):
        print("andar" if k < 4 else "muerte", k if k < 4 else k - 4,
              [x for x in check(s, recurso=True) if "silueta" not in x] or "OK")
    exportar("vm_zombi_andar", a, 6, True)
    exportar("vm_zombi_muerte", m, 8, False)
    contact_sheet([a, m, [s.silhouette() for s in a + m]], OUT / "contacto_zombi.png", scale=6,
                  labels=["andar", "muerte", "silueta"])
