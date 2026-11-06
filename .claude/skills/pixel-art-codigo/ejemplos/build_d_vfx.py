"""Fase D · VFX por código, 64x64 por fotograma. Bruno (Arte técnico), 6-nov-2026.

Especificación de Iris: capa VFX_HD (nítida), paleta de 32, alfa 0/255, solo el efecto y fondo
transparente.
  - fogonazo        5 fotogramas, sin bucle   (boca del arma a la izquierda, dispara a la derecha)
  - humo_impacto    5 fotogramas, sin bucle   (polvo que levanta un impacto en el suelo)
  - trazadora       bucle de 6                (horizontal, cabeza a la derecha; el motor la mueve)
  - haz_foco        bucle de 8                (cono del foco de noche, de arriba abajo, con motas)
  - fuego           bucle de 8                (fuego en el suelo, sin leños)
Sin rojos (el rojo es de la interfaz): el fuego enfría de crema a ámbar, ocre y óxido.
Los desvanecidos van con tramado Bayer, nunca con alfa parcial.
Salida por efecto: hoja PNG, JSON estilo Aseprite (meta.viaMuerta con fps, bucle y pivote), GIF x4.
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, ROJOS, Sprite  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_d")
W = H = 64
FPS = 12
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def _h(x, y, s):
    n = (x * 374761393 + y * 668265263 + s * 2147483647) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def ruido(x, y, p, s, per=64):
    """Ruido de valor periódico (periodo per) para que los bucles cierren sin salto."""
    n = per // p
    fx, fy = x / p, y / p
    x0, y0 = math.floor(fx), math.floor(fy)
    tx, ty = fx - x0, fy - y0
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    v = [[_h((x0 + i) % n, (y0 + j) % n, s) for i in (0, 1)] for j in (0, 1)]
    a = v[0][0] + (v[0][1] - v[0][0]) * tx
    b = v[1][0] + (v[1][1] - v[1][0]) * tx
    return a + (b - a) * ty


def tramado(x, y, d):
    """True si el píxel se pinta con densidad d (0–1). Bayer mezclado con ruido por píxel, para que
    el desvanecido se deshilache sin dibujar la rejilla de ajedrez (como el humo de A8)."""
    return d * 16 > BAYER[y % 4][x % 4] * .5 + _h(x, y, 77) * 8


def blob(s, cx, cy, r, color, dens=1.0, borde=None):
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            d = math.hypot(x + .5 - cx, y + .5 - cy)
            if d <= r and tramado(x, y, dens):
                s.set(x, y, borde if (borde and d > r - 1.2) else color)


# --- Fogonazo -------------------------------------------------------------------------------
def fogonazo(f):
    s = Sprite(W, H)
    oy, ox = 32, 2
    if f == 0:                                     # chispa inicial: estrella pequeña
        for k in range(-3, 4):
            s.set(ox + 3 + k, oy, "amber")
            s.set(ox + 3, oy + k, "amber")
        blob(s, ox + 3, oy + .5, 2.2, "cream")
    elif f in (1, 2):                              # llamarada: lengua a la derecha y púas
        largo = 30 if f == 1 else 22
        for x in range(largo):
            t = x / largo
            semi = (6 if f == 1 else 4) * math.sin(math.pi * min(1, t * 1.6)) * (1 - t * .5)
            for y in range(int(oy - semi), int(oy + semi) + 1):
                d = abs(y + .5 - oy) / max(semi, .5)
                v = (1 - t) * (1 - d * .8)
                c = "cream" if v > .62 else "amber" if v > .35 else "ochre_dark"
                if f == 2:
                    c = {"cream": "amber", "amber": "ochre_dark", "ochre_dark": "rust"}[c]
                s.set(ox + x, y, c)
        if f == 1:                                 # púas laterales en 45º
            for k in range(1, 8):
                s.set(ox + 4 + k, oy - k, "amber")
                s.set(ox + 4 + k, oy + k, "amber")
            for k in range(1, 5):
                s.set(ox + 2, oy - 3 - k, "ochre_dark")
                s.set(ox + 2, oy + 3 + k, "ochre_dark")
    else:                                          # humo de pólvora que se deshace
        r = 5 + (f - 3) * 3
        dens = .75 if f == 3 else .35
        blob(s, ox + 12 + (f - 3) * 4, oy - (f - 3) * 3, r, "concrete", dens)
        blob(s, ox + 7, oy - 1, r * .6, "slate", dens * .8)
        if f == 3:
            blob(s, ox + 4, oy + .5, 1.6, "ochre_dark")
    return s


# --- Humo de impacto (polvo) ----------------------------------------------------------------
def humo_impacto(f):
    s = Sprite(W, H)
    cx, cy = 32, 46
    t = f / 4
    dens = [1, .95, .7, .45, .2][f]
    r = 4 + t * 12
    for k, (dx, dy, fr) in enumerate([(0, 0, 1), (-5, 2, .8), (5, 2, .8), (-2, -4, .85), (3, -5, .8)]):
        rr = r * fr
        cxx, cyy = cx + dx * (.3 + t * .5), cy + dy * (.3 + t * .5) - t * 6
        blob(s, cxx, cyy, rr, "olive_grey", dens)
        blob(s, cxx - rr * .25, cyy - rr * .25, rr * .6, "beige" if f < 2 else "concrete_light", dens)
    if f < 2:                                      # chinas que saltan
        for k in range(6):
            a = math.pi * (1.1 + .8 * k / 5)
            d = 8 + f * 10
            s.set(int(cx + math.cos(a) * d), int(cy + math.sin(a) * d * .8), "grey_brown")
    return s


# --- Trazadora ------------------------------------------------------------------------------
def trazadora(f):
    s = Sprite(W, H)
    y = 32
    head = 60
    for x in range(8, head):
        t = (head - x) / (head - 8)                # 0 en la cabeza, 1 en la cola
        n = ruido(x + f * 64 / 6 * 2, f * 4, 8, 5)  # parpadeo que corre por la estela
        if t < .12:
            c = "cream"
        elif t < .45:
            c = "amber"
        else:
            c = "ochre_dark"
        if t > .3 and n < .25 + t * .5 and not tramado(x, f, 1 - t):
            continue                               # la cola se rompe y chisporrotea
        s.set(x, y, c)
        if t < .25:
            s.set(x, y + 1, "amber" if t > .06 else "cream")
    s.rect(head, y, 2, 2, "bone")                  # punta al blanco
    for k in range(3):                             # chispas sueltas tras la cabeza
        x = int(head - 10 - _h(k, f, 7) * 30)
        s.set(x, y + (-2 if _h(k, f, 8) < .5 else 3), "amber")
    return s


# --- Haz del foco ---------------------------------------------------------------------------
def haz_foco(f):
    s = Sprite(W, H)
    top, ancho_arriba, ancho_abajo = 0, 6, 54
    for y in range(H):
        t = y / (H - 1)
        semi = (ancho_arriba + (ancho_abajo - ancho_arriba) * t) / 2
        for x in range(int(32 - semi), int(32 + semi) + 1):
            d = abs(x + .5 - 32) / semi
            breath = .06 * math.sin(2 * math.pi * (f / 8 + y / 64))
            dens = (1 - t) * .55 * (1 - d * d) + breath + .05
            if d > .92 and tramado(x, y, .5):
                s.set(x, y, "concrete_light")      # canto del cono
            elif tramado(x, y, dens):
                s.set(x, y, "bone" if dens > .42 else "cream" if dens > .3 else "concrete_light")
    for k in range(7):                             # motas de polvo que cruzan el haz
        y = int((_h(k, 1, 3) * 64 + f * 8 * (1 + k % 2)) % 64)
        semi = (ancho_arriba + (ancho_abajo - ancho_arriba) * y / 63) / 2
        x = int(32 + (_h(k, 2, 3) * 2 - 1) * semi * .8)
        s.set(x, y, "cream")
    return s


# --- Fuego ----------------------------------------------------------------------------------
def fuego(f):
    s = Sprite(W, H)
    base = 56
    for y in range(8, base + 1):
        for x in range(12, 52):
            dx = abs(x + .5 - 32) / 20
            alto = (base - y) / 40                  # 0 en la base, 1 arriba
            forma = (1 - dx * dx) - alto * 1.05
            n = ruido(x, y + f * 8, 8, 11) * .6 + ruido(x, y + f * 8, 4, 12) * .4
            v = forma + (n - .5) * .7
            if v <= .05:
                continue
            c = "cream" if v > .62 else "amber" if v > .38 else "ochre_dark" if v > .2 else "rust"
            s.set(x, y, c)
    for k in range(6):                             # pavesas que suben (periódicas en 8)
        y = int((base - 10 - _h(k, 0, 9) * 40 - f * 8) % 48) + 4
        x = int(18 + _h(k, 1, 9) * 28 + math.sin((f + k) * math.pi / 4) * 2)
        if not s.get(x, y)[3]:
            s.set(x, y, "amber" if k % 2 else "ochre_dark")
    for k in range(5):                             # brasas en la base
        s.set(int(20 + _h(k, 2, 9) * 24), base + 1, "rust" if (k + f) % 3 else "amber")
    return s


EFECTOS = {
    "fogonazo": (fogonazo, 5, False, (0.03, 0.5)),
    "humo_impacto": (humo_impacto, 5, False, (0.5, 0.75)),
    "trazadora": (trazadora, 6, True, (0.95, 0.5)),
    "haz_foco": (haz_foco, 8, True, (0.5, 0.0)),
    "fuego": (fuego, 8, True, (0.5, 0.9)),
}


def revisar(frames):
    avisos = set()
    cols = set()
    pal = set(v[:3] for v in PAL.values())
    for s in frames:
        for y in range(H):
            for x in range(W):
                p = s.px[x, y]
                if p[3] not in (0, 255):
                    avisos.add("alfa parcial")
                if p[3]:
                    cols.add(p[:3])
                    if p[:3] not in pal:
                        avisos.add("fuera de paleta")
                    if p[:3] in ROJOS:
                        avisos.add("rojo")
    return f"{len(cols)} colores", sorted(avisos)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    filas = []
    for nombre, (fn, n, bucle, pivote) in EFECTOS.items():
        frames = [fn(f) for f in range(n)]
        hoja = Image.new("RGBA", (W * n, H), (0, 0, 0, 0))
        for f, s in enumerate(frames):
            hoja.alpha_composite(s.img, (W * f, 0))
        base = f"vm_vfx_{nombre}"
        hoja.save(OUT / f"{base}.png")
        dur = round(1000 / FPS)
        meta = {"frames": [{"filename": f"{base} {f}", "frame": {"x": W * f, "y": 0, "w": W, "h": H},
                            "rotated": False, "trimmed": False,
                            "spriteSourceSize": {"x": 0, "y": 0, "w": W, "h": H},
                            "sourceSize": {"w": W, "h": H}, "duration": dur} for f in range(n)],
                "meta": {"image": f"{base}.png", "size": {"w": W * n, "h": H}, "format": "RGBA8888",
                         "viaMuerta": {"fps": FPS, "loop": bucle, "frames": n, "frameSize": [W, H],
                                       "pivot": list(pivote), "capa": "VFX_HD",
                                       "origen": "pixelvm, build_d_vfx.py"}}}
        (OUT / f"{base}.json").write_text(json.dumps(meta, indent=1))
        noche = PAL["navy"]
        big = [Image.alpha_composite(Image.new("RGBA", (W, H), noche), s.img).resize((W * 4, H * 4), Image.NEAREST)
               for s in frames]
        big[0].save(OUT / f"{base}_x4.gif", save_all=True, append_images=big[1:], duration=dur * 2,
                    loop=0 if bucle else 1, disposal=2)
        print(nombre, n, "fotogramas", "bucle" if bucle else "una vez", *revisar(frames))
        filas.append(frames)
    # Hoja de contacto: cada efecto en una fila, x3, sobre gris oliva (día) y azul noche.
    n_max = max(len(r) for r in filas)
    esc = 3
    hoja = Image.new("RGBA", (n_max * (W * esc + 4) * 2 + 12, len(filas) * (H * esc + 4) + 4), PAL["navy"])
    for i, frames in enumerate(filas):
        for j, s in enumerate(frames):
            for k, fondo in enumerate((PAL["olive_grey"], PAL["navy"])):
                c = Image.alpha_composite(Image.new("RGBA", (W, H), fondo), s.img).resize((W * esc, H * esc), Image.NEAREST)
                hoja.paste(c, (4 + k * (n_max * (W * esc + 4) + 4) + j * (W * esc + 4), 4 + i * (H * esc + 4)))
    hoja.save(OUT / "contacto_fase_d_vfx.png")
