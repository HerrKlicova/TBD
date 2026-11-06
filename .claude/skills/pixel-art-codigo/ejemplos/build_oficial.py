"""Oficial con radio, 32 px, vista cenital, mirando al norte. Bruno (Arte técnico), 6-nov-2026.

Misma base que el fusilero (D-N57, rim #404460, contorno) para que sea de la misma tropa, y tres
señas que se leen desde arriba:
  - mochila de radio en lugar de la mochila beige: caja con tapa beige y antena fina en diagonal;
  - franja de mando en hueso en la nuca del casco;
  - animación de radio: el auricular a la oreja (bucle de 2).
Animaciones: andar (4), radio (2, bucle), disparo (3, con el fusil del fusilero) y muerte (5).
"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, Sprite, check, contact_sheet  # noqa: E402
import build_fusilero as fu  # noqa: E402
from build_zombi import exportar, girar, remapear  # noqa: E402
import build_zombi  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_oficial")
ANTENA = "slate_dark"


def radio(s, cx, cy, antena_dx=0):
    """Mochila de radio sobre la del fusilero: caja más alta, tapa beige, antena al noreste."""
    s.rect(cx - 2, cy + 2, 5, 6, "olive_grey")               # caja estrecha: las piernas asoman a los lados
    s.rect(cx - 1, cy + 3, 3, 3, "beige")                    # tapa (cara de arriba, clara)
    s.set(cx, cy + 4, "olive_dark")                          # mando
    s.line(cx + 2, cy + 3, cx + 8 + antena_dx, cy - 8, ANTENA)   # antena fina


def mando(s, cx, cy):
    s.hline(cx - 2, cx + 2, cy - 1, "bone")                  # franja de mando en la nuca del casco


def oficial(s, cx, cy, pi, pd, fusil="puerto", retro=0, antena_dx=0, auricular=False):
    m, ml = fu.MOCHILA, fu.MOCHILA_L
    fu.MOCHILA = fu.MOCHILA_L = "olive_grey"                 # sin la mochila beige: la radio la sustituye
    fu.soldado(s, cx, cy, pi, pd, fusil, retro)
    fu.MOCHILA, fu.MOCHILA_L = m, ml
    cy += retro
    radio(s, cx, cy, antena_dx)
    # Rehace hombros y casco por encima de la caja (la caja va detrás).
    s.ellipse(cx - 7, cy - 3, cx + 7, cy + 2, fu.LADO)
    s.ellipse(cx - 6, cy - 3, cx + 6, cy + 1, fu.TOP)
    s.rect(cx - 1, cy + 3, 3, 3, "beige")                    # la tapa de la radio, por encima
    s.set(cx, cy + 4, "olive_dark")
    if fusil == "puerto":
        s.line(cx - 5, cy - 6, cx + 6, cy + 1, fu.FUSIL)
        s.line(cx - 4, cy - 6, cx + 6, cy, fu.FUSIL_L)
    else:
        s.rect(cx + 2, cy - 12, 2, 10, fu.FUSIL)
        s.vline(cx + 2, cy - 11, cy - 4, fu.FUSIL_L)
        s.rect(cx - 2, cy - 7, 4, 2, fu.LADO)
        s.rect(cx + 1, cy - 8, 2, 2, "tan")
    s.ellipse(cx - 3, cy - 6, cx + 3, cy, fu.CASCO)
    s.ellipse(cx - 2, cy - 5, cx + 2, cy - 1, fu.CASCO_L)
    mando(s, cx, cy)
    if auricular:                                            # mano y auricular junto a la oreja izquierda
        s.rect(cx - 6, cy - 3, 2, 3, fu.LADO)                # brazo doblado
        s.rect(cx - 5, cy - 5, 2, 3, "slate_dark")           # auricular
        s.set(cx - 6, cy - 4, "tan")                         # mano
        s.line(cx - 6, cy - 1, cx - 4, cy + 4, "slate_dark")  # cable a la radio


def fin(s, rim=True):
    if rim:
        fu.rim(s)
    s.outline()
    s.limit_colors(10)
    return s


def andar(f):
    s = Sprite()
    pi, pd, bx = fu.ANDAR[f]
    oficial(s, 16 + bx, 18, pi, pd, antena_dx=(-bx))         # la antena oscila al revés del balanceo
    return fin(s)


def radio_anim(f):
    s = Sprite()
    oficial(s, 16, 18, 1, 1, "puerto", antena_dx=f, auricular=True)
    return fin(s)


def disparo(f):
    s = Sprite()
    oficial(s, 16, 18, 1, 1, "apunta", retro=1 if f == 1 else 0)
    fin(s)
    if f == 1:
        bx, by = 18, 18 + 1 - 13
        s.rect(bx, by - 2, 2, 2, "cream")
        for (x, y) in ((bx - 1, by - 3), (bx + 2, by - 3), (bx, by - 4), (bx - 1, by - 1), (bx + 2, by - 1)):
            s.set(x, y, "amber")
        s.set(bx + 1, by - 4, "cream")
        s.limit_colors(10)                                   # el fogonazo es acento: no se funde
    return s


def muerte(f):
    if f < 3:
        s = Sprite()
        if f == 0:
            oficial(s, 16, 19, 1, 1)
        elif f == 1:
            oficial(s, 16, 17, 2, 2)
            for x in range(32):
                for y in range(23, 32):
                    s.set(x, y, None)
        else:
            fu.tendido(s, 16, 7, 20, 0)
            s.rect(12, 15, 8, 5, "olive_grey")
            s.rect(13, 15, 6, 2, "beige")
            girar(s, -15)
        return fin(s)
    s = Sprite()
    fu.tendido(s, 16, 6, 21, 1)
    s.rect(12, 14, 8, 5, "olive_grey")                       # radio a la espalda
    s.rect(13, 14, 6, 2, "beige")
    s.line(20, 14, 24, 9, ANTENA)                            # antena doblada
    girar(s, -35)
    remapear(s, fu.APAGAR)
    return fin(s, rim=False)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    build_zombi.OUT = OUT
    anims = {"andar": ([andar(f) for f in range(4)], 6, True),
             "radio": ([radio_anim(f) for f in range(2)], 3, True),
             "disparo": ([disparo(f) for f in range(3)], 12, False),
             "muerte": ([muerte(f) for f in range(5)], 8, False)}
    for n, (fr, fps, bucle) in anims.items():
        for k, s in enumerate(fr):
            av = [x for x in check(s) if "silueta" not in x]
            if av:
                print(n, k, av)
        exportar(f"vm_oficial_{n}", fr, fps, bucle)
    contact_sheet([v[0] for v in anims.values()], OUT / "contacto_oficial.png", scale=6, labels=list(anims))
    # Prueba sobre suelos junto al fusilero.
    a7 = Path("/mnt/project-files/docs/arte/pixel-codigo/a7")
    fusil = fu.andar(0)
    fondos = [Image.new("RGBA", (32, 32), PAL["olive"])]
    fondos += [Image.open(a7 / f"tex_{n}_64.png").convert("RGBA").crop((0, 0, 32, 32))
               for n in ("tierra", "balasto", "hormigon", "asfalto") if (a7 / f"tex_{n}_64.png").exists()]
    piezas = [anims["andar"][0][0].img, anims["radio"][0][0].img, anims["muerte"][0][4].img, fusil.img]
    h = Image.new("RGBA", (len(piezas) * 136 + 8, len(fondos) * 136 + 8), PAL["navy"])
    for i, fo in enumerate(fondos):
        for j, p in enumerate(piezas):
            h.paste(Image.alpha_composite(fo.copy(), p).resize((128, 128), Image.NEAREST), (8 + j * 136, 8 + i * 136))
    h.save(OUT / "prueba_suelos_oficial.png")
