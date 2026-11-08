"""Vía con traviesas para la vista de zona: textura que se repite sobre el quad del balasto. Bruno (Arte técnico), 8-nov.

Hoy (PrototypeBootstrap) la vía son ~150 cajas de traviesa por tramo en #442c0c, que es un color RESERVADO a la horda
(1c200c / 442c0c, L* < 21): de noche una traviesa se lee como un zombi tumbado. Esta muestra propone sustituirlas por UNA
textura que se repite (wrap Repeat, Point, sin mipmaps) sobre el quad del balasto, y 5 maneras de dibujarla.

Escala: 1 téxel = 0,15 m, que es lo que mide 1 px del mundo a ortho 26 (2·26/360 = 0,144 m). Ancho 24 px = 3,6 m.
Ancho de vía ibérico 1,668 m (SIN VERIFICAR para esta línea; lo confirma Paula) → carriles a 11 px.
  A  hormigón real      traviesa de hormigón cada 0,6 m (4 px), balasto grey_brown
  B  hormigón ralo      traviesa de hormigón cada 1,2 m (8 px): menos ruido a 16 px por zombi
  C  madera             traviesa en brown #543820 (L* 26, fuera de la reserva) cada 0,6 m
  D  balasto claro      balasto olive_grey con piedra concrete_light; el tren (#303424) y la horda se recortan mejor
  E  banda tramada      sin traviesas sueltas: banda bajo los carriles al 50 % (lo más tranquilo de lejos)
Salida (en la carpeta dada): T_via_<v>.png (24 x periodo), muestra_vias_zona.png (día y noche aproximada, x4)
y medidas en pantalla: L* del balasto, ΔL* con la horda (#1c200c) y con el tren (#303424). Criterio de Sara: ΔL* >= 8.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, _lab_l  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_vias_zona")
W = 24
RIEL_IZQ, RIEL_DER = 6, 17           # 11 px = 1,65 m
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def c(n):
    return PAL[n][:3]


def _h(x, y, s):
    n = (x * 374761393 + y * 668265263 + s * 2147483647) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65536


VARIANTES = {
    "A": dict(nombre="hormigón real", periodo=4, grosor=2, trav="concrete", bal="grey_brown", piedra="olive_grey", dens=0.25, largo=17),
    "B": dict(nombre="hormigón ralo", periodo=8, grosor=2, trav="concrete", bal="grey_brown", piedra="olive_grey", dens=0.25, largo=17),
    "C": dict(nombre="madera", periodo=4, grosor=2, trav="brown", bal="grey_brown", piedra="olive_grey", dens=0.25, largo=17),
    "D": dict(nombre="balasto claro", periodo=4, grosor=2, trav="concrete", bal="olive_grey", piedra="concrete_light", dens=0.20, largo=17),
    "E": dict(nombre="banda tramada", periodo=8, grosor=0, trav="concrete", bal="grey_brown", piedra="olive_grey", dens=0.25, largo=17),
}


def textura(v):
    p = VARIANTES[v]
    H = p["periodo"] * 2                           # dos periodos: la piedra no se repite tan a la vista
    im = Image.new("RGB", (W, H))
    px = im.load()
    x0 = (W - p["largo"]) // 2
    for y in range(H):
        for x in range(W):
            col = c(p["bal"])
            if _h(x, y, 7) < p["dens"]:
                col = c(p["piedra"])
            borde = x in (0, W - 1)
            if borde and BAYER[y % 4][x % 4] < 8:   # el talud del balasto se deshace en tramado, sin alfa
                col = c("olive_shadow") if v != "D" else c("grey_brown")
            if p["grosor"] and x0 <= x < x0 + p["largo"] and (y % p["periodo"]) < p["grosor"]:
                col = c(p["trav"])
            if v == "E" and x0 <= x < x0 + p["largo"] and BAYER[y % 4][x % 4] < 8:
                col = c(p["trav"])
            if x in (RIEL_IZQ, RIEL_DER):
                col = c("concrete_light")           # cabeza del carril: lo único claro, se lee de noche
            if x in (RIEL_IZQ + 1, RIEL_DER + 1):
                col = c("olive_shadow")             # sombra del patín, en el valor más oscuro del decorado (no el de la horda)
            px[x, y] = col
    return im


def noche(rgb):
    """Aproximación de la luna (0,45) con cielo navy; solo para la muestra, no es el render."""
    n = c("navy")
    return tuple(int(min(255, rgb[i] * 0.45 + n[i] * 0.35)) for i in range(3))


def muestra(texs):
    L = 120                                         # 18 m de vía
    filas = []
    for v, t in texs.items():
        tira = Image.new("RGB", (L, W))
        tr = t.rotate(90, expand=True)              # vía en horizontal
        for x in range(0, L, tr.width):
            tira.paste(tr, (x, 0))
        d = ImageDraw.Draw(tira)
        d.rectangle([8, 3, 55, W - 4], fill=c("olive_shadow"))     # vagón (#303424 en el juego; aquí su valor)
        d.rectangle([8, 3, 55, 3], fill=c("grey_brown"))
        for k, (zx, zy) in enumerate([(70, 6), (74, 15), (79, 10), (86, 18), (92, 5), (101, 13)]):
            d.rectangle([zx, zy, zx + 1, zy + 2], fill=c("olive_black"))   # zombis tumbados/agachados a 2x3 px
        dia = tira
        noc = Image.new("RGB", tira.size)
        noc.putdata([noche(p) for p in tira.get_flattened_data()])
        filas.append((v, dia, noc))
    K = 4
    out = Image.new("RGB", (L * 2 * K + 40 + 180, len(filas) * (W * K + 24) + 30), (0, 0, 0))
    d = ImageDraw.Draw(out)
    f = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 12)
    d.text((180, 8), "día", fill=c("bone"), font=f); d.text((180 + L * K + 40, 8), "noche (aproximada)", fill=c("bone"), font=f)
    for i, (v, dia, noc) in enumerate(filas):
        y = 30 + i * (W * K + 24)
        d.text((6, y + W * K // 2 - 6), f"{v} · {VARIANTES[v]['nombre']}", fill=c("bone"), font=f)
        out.paste(dia.resize((L * K, W * K), Image.NEAREST), (180, y))
        out.paste(noc.resize((L * K, W * K), Image.NEAREST), (180 + L * K + 40, y))
    return out


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    texs = {v: textura(v) for v in VARIANTES}
    for v, t in texs.items():
        t.save(OUT / f"T_via_{v}.png")
    muestra(texs).save(OUT / "muestra_vias_zona.png")
    lh, lt = _lab_l(c("olive_black")), _lab_l(c("olive_shadow"))
    print(f"{'var':3} {'nombre':16} {'L* balasto':>10} {'ΔL* horda':>9} {'ΔL* tren':>8} {'L* traviesa':>11}")
    for v, p in VARIANTES.items():
        lb = _lab_l(c(p["bal"]))
        print(f"{v:3} {p['nombre']:16} {lb:10.1f} {lb - lh:9.1f} {lb - lt:8.1f} {_lab_l(c(p['trav'])):11.1f}")
    print(f"→ {OUT}")


if __name__ == "__main__":
    main()
