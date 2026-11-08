"""D-N160 · Símbolos APP-6 en pixel art para el mapa del «Informe de inteligencia». Bruno (Arte técnico), 8-nov.

Encargo de Lucía (vía coordinación) y referencia de Paula (informe_inteligencia_referencias.md, §b). Formas de APP-6:
  amigo        rectángulo 3:2 en tierra; cúpula (medio óvalo abierto por abajo) en el aire
  hostil       rombo
  desconocido  trébol de cuatro hojas
Colores de NUESTRA paleta (no los de la OTAN):
  amigo        un azul de la paleta (se elige entre 5 variantes, abajo)
  hostil       hueso d4d0cc; rojo 982420 SOLO mientras ataca
  desconocido  neutro concrete_light 94948c, nunca amarillo (no se puede confundir con el ámbar e08c30 de los avisos)
  horda        rombo VACÍO, sin icono de infantería (no es una unidad militar)
Iconos dentro del marco: infantería (X), mecanizado (X + óvalo), acorazado (óvalo), ruedas (dos círculos debajo), helicóptero (rotor en
pajarita, en la cúpula). Tamaño encima: ● pelotón, ●●● sección, I compañía. El dibujo exacto está SIN VERIFICAR frente a los SVG de Commons
(Paula los baja); cuando estén, se compara y se retoca.
Medidas: altura de celda 16 px (mapa a x1) y 24 px (informe ampliado). Contorno de 1 px; el fondo del marco va relleno para recortarse sobre
las tres bandas del plano táctico (303424, 48403c, 605c48) y el mar navy.

Salida: app6_<afiliación>_<icono>_<tam>.png (variante de azul recomendada) y muestra_app6.png con las 5 variantes de azul sobre el mapa (x4),
más la medida de contraste del contorno con cada banda.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, _lab_l  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_app6")


def c(n):
    return PAL[n][:3] + (255,)


HUESO, ROJO, NEUTRO = c("bone"), c("red_bright"), c("concrete_light")
FONDOS = {"meseta 303424": c("olive_shadow"), "media 48403c": c("grey_brown"), "alta 605c48": c("olive_grey"), "mar 181c30": c("navy")}

# Variantes del azul amigo: (relleno, contorno e icono). La paleta no tiene azul vivo: slate es el único azul medio
# y está reservado al rim de NUESTRAS unidades, así que en el mapa tiene sentido (es lo mismo: lo propio).
AZULES = {
    "A": ("slate", "bone", "relleno slate, contorno e icono hueso"),
    "B": ("slate_dark", "slate", "relleno slate_dark, contorno slate"),
    "C": ("navy", "slate", "relleno navy, contorno slate"),
    "D": ("slate", "grey_light", "relleno slate, contorno grey_light"),
    "E": ("slate_dark", "grey_light", "relleno slate_dark, contorno grey_light"),
}
RECOMENDADA = "D"


# Trébol por filas (de, a): cuatro lóbulos redondos con una muesca de 2 px en cada diagonal, que es lo que lo separa del círculo.
TREBOL = {
    15: [(6, 8), (5, 9), (4, 10), (4, 10), (2, 12), (1, 13), (0, 14), (0, 14), (0, 14), (1, 13), (2, 12), (4, 10), (4, 10), (5, 9), (6, 8)],
    23: [(9, 13), (7, 15), (6, 16), (6, 16), (6, 16), (6, 16), (2, 20), (1, 21), (1, 21), (0, 22), (0, 22), (0, 22), (0, 22), (0, 22),
         (1, 21), (1, 21), (2, 20), (6, 16), (6, 16), (6, 16), (6, 16), (7, 15), (9, 13)],
}


def borde(mask):
    """Píxeles de la máscara con algún vecino (4) fuera: el contorno de 1 px."""
    w, h = mask.size
    m = mask.load()
    out = Image.new("1", (w, h), 0)
    o = out.load()
    for y in range(h):
        for x in range(w):
            if m[x, y] and any(not (0 <= x + dx < w and 0 <= y + dy < h) or not m[x + dx, y + dy] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                o[x, y] = 1
    return out


def marco(forma, H):
    """Máscara del marco, en una celda de ancho W y alto H; devuelve (máscara, caja interior para el icono)."""
    W = H * 3 // 2 if forma in ("amigo", "aire") else (H if H % 2 else H - 1)   # rombo y trébol: lado impar, con centro en un píxel
    m = Image.new("1", (W, H), 0)
    d = ImageDraw.Draw(m)
    if forma == "amigo":
        y0 = H // 5                                         # deja sitio arriba para el tamaño (●, ●●●, I)
        d.rectangle([0, y0, W - 1, H - 1], fill=1)
        caja = (1, y0 + 1, W - 2, H - 2)
    elif forma == "aire":
        y0 = H // 5
        d.ellipse([0, y0, W - 1, y0 + 2 * (H - y0) - 1], fill=1)
        d.rectangle([0, H, W, 2 * H], fill=0)
        caja = (W // 4, y0 + (H - y0) // 3, W - 1 - W // 4, H - 2)
    elif forma == "hostil":
        r = (H - 1) // 2                                     # rombo de lado impar: punta de 1 px arriba, abajo y a los lados
        cx, cy = W // 2, r
        d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=1)
        caja = (cx - r // 2, cy - r // 2, cx + r // 2, cy + r // 2)
    else:                                                    # trébol dibujado a mano (Lucía: a 16 px el geométrico se leía como octógono)
        S = H if H % 2 else H - 1
        mp = m.load()
        for y, (x0, x1) in enumerate(TREBOL[S]):
            for x in range(x0, x1 + 1):
                mp[x, y] = 1
        c0 = S // 2
        k = S // 5
        caja = (c0 - k, c0 - k, c0 + k, c0 + k)
    return m, caja


def icono(d, nombre, caja, col):
    x0, y0, x1, y1 = caja
    # Cotejado con los SVG de Commons de Paula (referencias-reales/informe-inteligencia/app6, 8-nov):
    #   coraza = «estadio» (rectángulo de extremos redondos, ~64 % de ancho y 50 % de alto del marco), no una elipse;
    #   motorizado = X + trazo vertical; ruedas = dos círculos pequeños abajo; ala rotatoria = pajarita con mástil hacia abajo.
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    if nombre in ("infanteria", "mecanizado", "motorizado", "ruedas"):
        d.line([x0, y0, x1, y1], fill=col)
        d.line([x0, y1, x1, y0], fill=col)
    if nombre == "motorizado":
        d.line([cx, y0, cx, y1], fill=col)
    if nombre in ("acorazado", "mecanizado"):
        ex0, ey0, ex1, ey1 = x0 + round(w * 0.18), y0 + round(h * 0.25), x1 - round(w * 0.18), y1 - round(h * 0.25)
        d.rounded_rectangle([ex0, ey0, ex1, ey1], radius=(ey1 - ey0) // 2, outline=col)
    if nombre == "ruedas":                                   # blindado de ruedas (Dragón): X + dos ruedas bajo el cruce
        r = 1 if h < 14 else 2
        for k in (-1, 1):
            rx = cx + k * (r + 2)
            d.ellipse([rx - r, y1 - 2 * r - 1, rx + r, y1 - 1], outline=col)
    if nombre == "helicoptero":                              # rotor en pajarita + mástil (Commons: ala rotatoria)
        hw, hh = w // 2, max(1, h // 3)
        cyr = y0 + hh
        d.polygon([(cx - hw, cyr - hh), (cx, cyr), (cx - hw, cyr + hh)], fill=col)
        d.polygon([(cx + hw, cyr - hh), (cx, cyr), (cx + hw, cyr + hh)], fill=col)
        d.line([cx, cyr, cx, y1], fill=col)


def tamano(d, W, H, eche, col):
    """Indicador de tamaño encima del marco, con la equivalencia española de APP-6: 1 = ● escuadra, 2 = ●● pelotón, 3 = ●●● sección, 'I' = compañía."""
    if not eche:
        return
    y = max(0, H // 5 - 3)
    if eche == "I":
        d.line([W // 2, y, W // 2, y + 1], fill=col)
    else:
        n = int(eche)
        for k in range(n):
            x = W // 2 + (k - (n - 1) / 2) * 3
            d.rectangle([int(x), y, int(x) + 1, y + 1], fill=col)


def simbolo(afil, ico, H, azul=RECOMENDADA, ataque=False, eche="", tramado=False):
    forma = {"amigo": "amigo", "aire": "aire", "hostil": "hostil", "horda": "hostil", "desconocido": "desconocido"}[afil]
    m, caja = marco(forma, H)
    W = m.size[0]
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    if afil in ("amigo", "aire"):
        rel, lin = c(AZULES[azul][0]), c(AZULES[azul][1])
    elif afil in ("hostil", "horda"):
        rel, lin = c("olive_shadow"), (ROJO if ataque else HUESO)
    else:
        rel, lin = c("olive_shadow"), NEUTRO
    if afil == "horda":
        rel = None                                           # rombo vacío: se ve el mapa por dentro
    if rel is not None:
        im.paste(rel, (0, 0), m)
    b = borde(m)
    if tramado:                                              # horda tramada (Lucía, por si se confunde con el hostil): contorno a píxeles alternos
        bp = b.load()
        for y in range(b.size[1]):
            for x in range(b.size[0]):
                if bp[x, y] and x % 2: bp[x, y] = 0
    im.paste(lin, (0, 0), b)
    if ataque:                                               # el rojo tiene poco valor sobre la banda alta: contorno de 2 px mientras ataca
        dentro = Image.new("1", m.size, 0)
        dentro.paste(m, (0, 0)); dentro.paste(0, (0, 0), b)
        im.paste(lin, (0, 0), borde(dentro))
    if afil != "horda" and ico:
        icono(d, ico, caja, lin)
    tamano(d, W, H, eche, lin)
    return im


CATALOGO = [  # (afiliación, icono, tamaño de unidad)
    ("amigo", "infanteria", "2"), ("amigo", "infanteria", "3"), ("amigo", "infanteria", "I"), ("amigo", "mecanizado", "2"),
    ("amigo", "acorazado", "2"), ("amigo", "motorizado", "2"), ("amigo", "ruedas", "2"), ("aire", "helicoptero", ""), ("hostil", "infanteria", ""), ("horda", "", ""),
    ("desconocido", "", ""),
]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for H in (16, 24):
        for afil, ico, eche in CATALOGO:
            if ico == "ruedas" and H < 20:                   # a 16 px las ruedas son 3 px y se comen la X: solo existe a 24
                continue
            nombre = f"app6_{afil}_{ico or 'vacio'}{'_' + eche if eche else ''}_{H}.png".replace("_I_", "_compania_").replace("_1_", "_escuadra_").replace("_2_", "_peloton_").replace("_3_", "_seccion_")
            simbolo(afil, ico, H, eche=eche).save(OUT / nombre)
        simbolo("horda", "", H, ataque=True).save(OUT / f"app6_horda_vacio_ataque_{H}.png")
        simbolo("horda", "", H, tramado=True).save(OUT / f"app6_horda_tramado_{H}.png")
        simbolo("hostil", "infanteria", H, ataque=True).save(OUT / f"app6_hostil_infanteria_ataque_{H}.png")

    # Muestra: cada variante de azul con todo el catálogo, sobre las cuatro bandas del mapa (x4).
    K, H = 4, 16
    f = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 12)
    fila_w = sum(simbolo(a, i, H, eche=e).size[0] + 4 for a, i, e in CATALOGO) + 2 * (H + 4) + 8
    out = Image.new("RGB", (220 + fila_w * K, 30 + len(AZULES) * len(FONDOS) * (H + 4) * K + len(AZULES) * 16), (0, 0, 0))
    d = ImageDraw.Draw(out)
    d.text((220, 6), "amigo (inf ●●, inf ●●●, inf I, mec, acor, motor, ruedas, helo) · hostil · horda · desconocido · horda ataque · hostil ataque", fill=HUESO[:3], font=f)
    y = 30
    for v, (rel, lin, desc) in AZULES.items():
        d.text((6, y), f"{v}{' (recomendada)' if v == RECOMENDADA else ''}", fill=HUESO[:3], font=f)
        d.text((6, y + 16), desc[:30], fill=NEUTRO[:3], font=f)
        for fn, fc in FONDOS.items():
            tira = Image.new("RGBA", (fila_w, H + 4), fc)
            x = 4
            for a, i, e in CATALOGO:
                s = simbolo(a, i, H, azul=v, eche=e); tira.alpha_composite(s, (x, 2)); x += s.size[0] + 4
            for a, i in (("horda", ""), ("hostil", "infanteria")):
                s = simbolo(a, i, H, ataque=True); tira.alpha_composite(s, (x, 2)); x += s.size[0] + 4
            out.paste(tira.convert("RGB").resize((fila_w * K, (H + 4) * K), Image.NEAREST), (220, y))
            y += (H + 4) * K
        y += 16
    out.save(OUT / "muestra_app6.png")

    print(f"{'var':3} {'contorno':12} " + " ".join(f"{k[:12]:>12}" for k in FONDOS) + "   (ΔL* contorno amigo − banda)")
    for v, (rel, lin, _) in AZULES.items():
        ll = _lab_l(c(lin)); lr = _lab_l(c(rel))
        print(f"{v:3} {lin:12} " + " ".join(f"{ll - _lab_l(fc):12.1f}" for fc in FONDOS.values()) + f"   relleno L* {lr:.1f}")
    print(f"hostil hueso: " + " ".join(f"{_lab_l(HUESO) - _lab_l(fc):.1f}" for fc in FONDOS.values()) +
          f" · ataque rojo: " + " ".join(f"{_lab_l(ROJO) - _lab_l(fc):.1f}" for fc in FONDOS.values()) +
          f" · desconocido: " + " ".join(f"{_lab_l(NEUTRO) - _lab_l(fc):.1f}" for fc in FONDOS.values()))
    print(f"→ {OUT}")


if __name__ == "__main__":
    main()
