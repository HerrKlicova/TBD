"""Fusilero de nuestra infantería, 32 px, vista cenital, mirando al norte. Bruno, 6-nov-2026.

Andar (bucle de 4), disparo (3, sin bucle; el motor lo repite) y muerte (5, el último se queda).
- Oliva con valor partido (D-N57): caras de arriba claras (casco y hombreras en beige y oliva claro),
  costados en olive_dark, contorno de 1 px.
- Rim #404460 (slate): 1 px por dentro del contorno en los lados sur y este; distingue a la tropa
  de los zombis, que no llevan rim.
- Fogonazo pequeño en la boca del fusil en el disparo (ámbar y crema: es luz).
- Sin sangre. El cadáver va girado, asimétrico y un escalón más apagado, con el fusil al lado.
"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, Sprite, check, contact_sheet  # noqa: E402
from build_zombi import exportar, girar, remapear  # noqa: E402
import build_zombi  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_fusilero")

CASCO, CASCO_L = "olive_light", "beige"
TOP, LADO = "olive_light", "olive_dark"
MOCHILA, MOCHILA_L, PIERNA, BOTA = "beige", "bone", "olive", "brown_dark"   # mochila: 2ª masa clara (Lucía)
FUSIL, FUSIL_L = "olive_black", "slate_dark"
RIM = "slate"

APAGAR = {"beige": "olive_light", "olive_light": "olive", "olive": "olive_grey", "olive_dark": "olive_shadow",
          "olive_grey": "grey_brown", "bone": "beige", "brown_dark": "olive_black", "tan": "rose_brown", "slate": "slate_dark",
          "slate_dark": "olive_shadow"}


def rim(s):
    """Rim de 1 px en slate en los píxeles de borde que dan al sur o al este (antes del contorno)."""
    src = s.img.copy().load()
    for y in range(s.h):
        for x in range(s.w):
            if not src[x, y][3]:
                continue
            sur = y + 1 >= s.h or not src[x, y + 1][3]
            este = x + 1 >= s.w or not src[x + 1, y][3]
            if sur or este:
                s.px[x, y] = PAL[RIM]


def soldado(s, cx, cy, pie_i, pie_d, fusil="puerto", retro=0):
    """fusil: 'puerto' (cruzado al pecho, andando) o 'apunta' (al frente). retro = retroceso en px."""
    cy += retro
    s.rect(cx - 4, cy + 3 - pie_i, 3, 5, PIERNA)            # piernas
    s.rect(cx + 2, cy + 3 - pie_d, 3, 5, PIERNA)
    s.rect(cx - 4, cy + 7 - pie_i, 3, 2, BOTA)
    s.rect(cx + 2, cy + 7 - pie_d, 3, 2, BOTA)
    s.rect(cx - 4, cy + 1, 9, 5, MOCHILA)                    # mochila (sur)
    s.hline(cx - 3, cx + 3, cy + 1, MOCHILA_L)
    s.ellipse(cx - 7, cy - 3, cx + 7, cy + 3, LADO)          # hombros y chaleco
    s.ellipse(cx - 6, cy - 3, cx + 6, cy + 1, TOP)
    s.set(cx - 3, cy - 1, "olive")                           # cargadores
    s.set(cx + 3, cy - 1, "olive")
    if fusil == "puerto":
        s.rect(cx - 7, cy - 3, 2, 3, LADO)                   # brazo izquierdo
        s.rect(cx + 5, cy - 3, 2, 3, LADO)
        s.line(cx - 5, cy - 6, cx + 6, cy + 1, FUSIL)        # fusil cruzado
        s.line(cx - 4, cy - 6, cx + 6, cy, FUSIL_L)
        s.rect(cx - 6, cy - 4, 2, 2, "tan")                  # mano
    else:
        s.rect(cx + 2, cy - 12, 2, 10, FUSIL)                # fusil al frente, desde el hombro derecho
        s.vline(cx + 2, cy - 11, cy - 4, FUSIL_L)
        s.rect(cx - 2, cy - 7, 4, 2, LADO)                   # brazo izquierdo sujeta el guardamanos
        s.rect(cx + 1, cy - 8, 2, 2, "tan")
        s.rect(cx + 4, cy - 3, 2, 3, LADO)                   # brazo derecho, al hombro
    s.ellipse(cx - 3, cy - 6, cx + 3, cy, CASCO)             # casco (arriba, claro)
    s.ellipse(cx - 2, cy - 5, cx + 2, cy - 1, CASCO_L)       # centro claro amplio
    s.set(cx + 2, cy - 1, LADO)


ANDAR = [(2, 0, -1), (1, 1, 0), (0, 2, 1), (1, 1, 0)]       # pie_i, pie_d, balanceo


def andar(f):
    s = Sprite()
    pi, pd, bx = ANDAR[f]
    soldado(s, 16 + bx, 18, pi, pd)
    rim(s)
    s.outline()
    return s


def disparo(f):
    s = Sprite()
    soldado(s, 16, 18, 1, 1, "apunta", retro=1 if f == 1 else 0)
    rim(s)
    s.outline()
    if f == 1:                                               # fogonazo en la boca (sin contorno)
        bx, by = 18, 18 + 1 - 13
        for k in range(-2, 3):
            s.set(bx + abs(k) // 2, by - 2 + abs(k), "amber")
        s.rect(bx, by - 2, 2, 2, "cream")
        s.set(bx - 1, by - 3, "amber")
        s.set(bx + 2, by - 3, "amber")
        s.set(bx, by - 4, "amber")
        s.set(bx + 1, by - 4, "cream")
    s.limit_colors(10)                                       # el fogonazo es acento: no se funde
    return s


def tendido(s, cx, y0, largo, abre):
    s.rect(cx - 4, y0 + largo - 10, 3, 9, PIERNA)
    s.rect(cx - 5, y0 + largo - 2, 3, 2, BOTA)
    for k in range(3):
        s.line(cx + 1 + k, y0 + largo - 10, cx + 4 + k, y0 + largo - 5, PIERNA)
    s.rect(cx + 5, y0 + largo - 3, 3, 2, BOTA)
    h = largo - 15
    s.poly([(cx - 7, y0 + 6), (cx + 6, y0 + 6), (cx + 4, y0 + 6 + h), (cx - 5, y0 + 6 + h)], LADO)
    s.poly([(cx - 6, y0 + 6), (cx + 5, y0 + 6), (cx + 3, y0 + 4 + h), (cx - 4, y0 + 4 + h)], TOP)
    s.rect(cx - 4, y0 + 8, 8, 5, MOCHILA)
    s.hline(cx - 3, cx + 3, y0 + 8, MOCHILA_L)
    x1 = cx - 9 - abre
    for k in (0, 1):
        s.line(cx - 7 + k, y0 + 7, x1 + k, y0 + 1, LADO)
    s.rect(x1, y0 - 1, 2, 2, "tan")
    s.rect(cx + 5, y0 + 9, 2, 3, LADO)
    s.ellipse(cx - 3, y0, cx + 2, y0 + 6, CASCO)
    s.ellipse(cx - 2, y0 + 1, cx, y0 + 3, CASCO_L)
    if abre:                                                  # fusil debajo: solo asoma la culata
        s.rect(cx + 4, y0 + 13, 3, 2, FUSIL)


def muerte(f):
    s = Sprite()
    if f == 0:
        soldado(s, 16, 19, 1, 1, "puerto")
    elif f == 1:
        soldado(s, 16, 17, 2, 2, "puerto")
        for x in range(32):
            for y in range(23, 32):
                s.set(x, y, None)
    elif f == 2:
        tendido(s, 16, 7, 20, 0)
        girar(s, -15)
    else:
        tendido(s, 16, 6, 21, 1)
        girar(s, -35)                                         # al otro lado que el zombi
        remapear(s, APAGAR)
    if f < 3:
        rim(s)
    s.outline()
    return s


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    build_zombi.OUT = OUT
    a = [andar(f) for f in range(4)]
    d = [disparo(f) for f in range(3)]
    m = [muerte(f) for f in range(5)]
    for n, fr in (("andar", a), ("disparo", d), ("muerte", m)):
        for k, s in enumerate(fr):
            av = [x for x in check(s) if "silueta" not in x]
            if av:
                print(n, k, av)
    exportar("vm_fusilero_andar", a, 6, True)
    exportar("vm_fusilero_disparo", d, 12, False)
    exportar("vm_fusilero_muerte", m, 8, False)
    contact_sheet([a, d, m, [s.silhouette() for s in a + d]], OUT / "contacto_fusilero.png", scale=6,
                  labels=["andar", "disparo", "muerte", "silueta"])
    # Prueba sobre suelos, junto a un zombi.
    a7 = Path("/mnt/project-files/docs/arte/pixel-codigo/a7")
    zom = OUT.parent / "z" / "vm_zombi_gris_pizarra_andar_0.png"
    fondos = [Image.new("RGBA", (32, 32), PAL["olive"])]
    fondos += [Image.open(a7 / f"tex_{n}_64.png").convert("RGBA").crop((0, 0, 32, 32))
               for n in ("tierra", "balasto", "hormigon", "asfalto") if (a7 / f"tex_{n}_64.png").exists()]
    piezas = [a[0].img, d[1].img, m[4].img] + ([Image.open(zom).convert("RGBA")] if zom.exists() else [])
    h = Image.new("RGBA", (len(piezas) * 136 + 8, len(fondos) * 136 + 8), PAL["navy"])
    for i, fo in enumerate(fondos):
        for j, p in enumerate(piezas):
            h.paste(Image.alpha_composite(fo.copy(), p).resize((128, 128), Image.NEAREST), (8 + j * 136, 8 + i * 136))
    h.save(OUT / "prueba_suelos_fusilero.png")
