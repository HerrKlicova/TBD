"""D-N160 · Pantalla del Informe de inteligencia y lo que la Colmena enseña en el mapa. Bruno (Arte técnico), 9-nov. MUESTRA

Encargo de Lucía:
  1) Maqueta del informe con los 8 apartados aprobados (cabecera con DTG «D41 · 2030A», resumen en una línea, situación de la amenaza,
     fuerzas propias, sin novedad en, valoración, necesidades, firma) y el mapa APP-6 encajado. 5 composiciones a 1920×1080, en el estilo del
     prefijo validado (docs/arte/pixellab-prefijo-validado.md): plancha de acero navy, marco pizarra, brillos pequeños en hueso. Cada
     composición lleva además la palabra de estilo de su variante del prefijo (V1 riveted, V2 bevelled, V3 stamped brackets, V4 double frame,
     V5 cut corners), así se ve cada una montada.
  2) Avisos de la Colmena: CO-1 a CO-4 al cambiar de estado y el ámbar «La Colmena se agita» (señal ≥ 6000 ‰).
  3) En el mapa: columna de la horda en Enjambre (se ve de lejos, sin ojos) y Dispersa frente a Al acecho sin texto. Solo forma y valor.
Textos PROVISIONALES (el final lo escribe Lucía). Texto en Inter (OFL), como la interfaz v2; los marcos van en múltiplos de 3 px.
Los símbolos APP-6 salen de build_app6.py (variante D) y se pintan a ×2.
Uso: python3 -I build_informe.py [carpeta_app6] [salida]
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

APP6 = Path(sys.argv[1] if len(sys.argv) > 1 else "out_app6")
OUT = Path(sys.argv[2] if len(sys.argv) > 2 else "out_informe")

NAVY = (0x18, 0x1c, 0x30)
NAVY2 = (0x20, 0x26, 0x3c)          # plancha un poco más clara para cajas internas (entre navy y slate_dark)
SLATE_D = (0x28, 0x34, 0x40)
BORDE = (0x35, 0x3a, 0x55)
SLATE = (0x40, 0x44, 0x60)
HUESO = (0xd4, 0xd0, 0xcc)
GRIS = (0x94, 0x94, 0x8c)
GRIS_L = (0xb8, 0xb8, 0xb8)
AMBAR = (0xe0, 0x8c, 0x30)
ROJO = (0x98, 0x24, 0x20)
NEGRO = (0x00, 0x00, 0x00)
HORDA = (0x1c, 0x20, 0x0c)
MESETA, MEDIA, ALTA = (0x30, 0x34, 0x24), (0x48, 0x40, 0x3c), (0x60, 0x5c, 0x48)
U = 3                                # píxel de la interfaz a 1080p

FONT = "/usr/share/fonts/opentype/inter/Inter-%s.otf"
_f = {}


def font(px, peso="Medium"):
    k = (px, peso)
    if k not in _f: _f[k] = ImageFont.truetype(FONT % peso, px)
    return _f[k]


# ---------------------------------------------------------------- marcos (prefijo validado + palabra de estilo)
def panel(d, box, estilo, fondo=NAVY):
    x0, y0, x1, y1 = box
    if estilo == "cortadas":
        c = 4 * U
        pts = [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]
        d.polygon(pts, fill=NEGRO)
        inner = [(x + (U if x < (x0 + x1) / 2 else -U), y + (U if y < (y0 + y1) / 2 else -U)) for x, y in pts]
        d.polygon(inner, fill=SLATE)
        inner2 = [(x + (2 * U if x < (x0 + x1) / 2 else -2 * U), y + (2 * U if y < (y0 + y1) / 2 else -2 * U)) for x, y in pts]
        d.polygon(inner2, fill=fondo)
        for x, y in (pts[0], pts[2], pts[4], pts[6]):
            pass
        d.rectangle([x0 + c, y0 + U, x0 + c + 3 * U, y0 + 2 * U - 1], fill=HUESO)      # brillo pequeño arriba a la izquierda
        return
    d.rectangle(box, fill=NEGRO)
    d.rectangle([x0 + U, y0 + U, x1 - U, y1 - U], fill=SLATE)
    d.rectangle([x0 + 2 * U, y0 + 2 * U, x1 - 2 * U, y1 - 2 * U], fill=fondo)
    if estilo == "remaches":
        for x, y in ((x0 + 4 * U, y0 + 4 * U), (x1 - 5 * U, y0 + 4 * U), (x0 + 4 * U, y1 - 5 * U), (x1 - 5 * U, y1 - 5 * U)):
            d.rectangle([x, y, x + U - 1 + U, y + 2 * U - 1], fill=BORDE); d.rectangle([x, y, x + U - 1, y + U - 1], fill=HUESO)
    elif estilo == "bisel":
        d.line([x0 + 2 * U, y0 + 2 * U, x1 - 2 * U, y0 + 2 * U], fill=BORDE, width=U)
        d.line([x0 + 2 * U, y0 + 2 * U, x0 + 2 * U, y1 - 2 * U], fill=BORDE, width=U)
        d.line([x0 + 2 * U, y1 - 2 * U - U + 1, x1 - 2 * U, y1 - 2 * U - U + 1], fill=NEGRO, width=U)
        d.rectangle([x0 + 3 * U, y0 + 3 * U, x0 + 6 * U, y0 + 4 * U - 1], fill=HUESO)
    elif estilo == "escuadras":
        L = 6 * U
        for (x, y, sx, sy) in ((x0 + U, y0 + U, 1, 1), (x1 - U, y0 + U, -1, 1), (x0 + U, y1 - U, 1, -1), (x1 - U, y1 - U, -1, -1)):
            d.rectangle(sorted([x, x + sx * L]) + [], fill=None) if False else None
            d.rectangle([min(x, x + sx * L), min(y, y + sy * U), max(x, x + sx * L), max(y, y + sy * U)], fill=HUESO)
            d.rectangle([min(x, x + sx * U), min(y, y + sy * L), max(x, x + sx * U), max(y, y + sy * L)], fill=HUESO)
    elif estilo == "doble":
        d.rectangle([x0 + 3 * U, y0 + 3 * U, x1 - 3 * U, y1 - 3 * U], outline=BORDE, width=U)
        d.rectangle([x0 + 4 * U, y0 + 3 * U, x0 + 7 * U, y0 + 4 * U - 1], fill=HUESO)


def texto(d, xy, s, px=20, col=HUESO, peso="Medium"):
    d.text(xy, s, fill=col, font=font(px, peso))


def envolver(s, px, ancho, peso="Medium"):
    f = font(px, peso); out = []; lin = ""
    for w in s.split():
        t = (lin + " " + w).strip()
        if f.getlength(t) <= ancho: lin = t
        else: out.append(lin); lin = w
    if lin: out.append(lin)
    return out


def seccion(d, x, y, w, titulo, lineas, px=19, num=None, vinetas=True, col_tit=GRIS):
    t = (f"{num}. " if num else "") + titulo.upper()
    texto(d, (x, y), t, 15, col_tit, "SemiBold"); y += 24
    for l in lineas:
        partes = envolver(l, px, w - (18 if vinetas else 0))
        for i, p in enumerate(partes):
            if vinetas and i == 0: d.rectangle([x + 2, y + px // 2, x + 2 + U, y + px // 2 + U], fill=GRIS)
            texto(d, (x + (18 if vinetas else 0), y), p, px)
            y += int(px * 1.38)
        y += 3
    return y + 10


# ---------------------------------------------------------------- contenido provisional
DTG = "D41 · 2030A"
CAB = dict(titulo="INFORME DE INTELIGENCIA", num="INTSUM 07", de="Sección de Inteligencia · Sagunto", periodo="D40 2030A – D41 2030A", clas="USO INTERNO")
RESUMEN = "La Colmena está al acecho y se junta al oeste de Nules (~2.000). Ataque probable la noche del D42."
AMENAZA = ["Nules: masa al oeste, ~2.000, rumbo este. Llegaría hacia D42 2200A.",
           "Sagunto–Castellón: el tren 2 pasa a la misma hora tres días seguidos. Lo ha aprendido.",
           "Castellón: movimiento sin identificar al norte de la estación."]
PROPIAS = ["Sagunto: 5 pelotones, Dragón, 2 VAMTAC. Munición 41 u.",
           "Nules: 1 pelotón de fusileros. Munición 12 u.",
           "Bétera: 2 NH90 en base, uno rearmando."]
SIN_NOV = ["Valencia, Teruel y el tramo Valencia–Sagunto."]
VALORACION = ["Si Nules no recibe tropa y munición antes de D42 1900A, cae en la segunda noche."]
NECESIDADES = ["≥ 20 u y un pelotón en Nules.", "Un NH90 sobre Castellón para identificar el movimiento."]
FIRMA = ("Cap. Ibarra", "Inteligencia · fuentes: Mora (observación), Llorens (radio)")


# ---------------------------------------------------------------- mapa táctico con APP-6
NODOS = {"Teruel": (0.10, 0.20), "Castellón": (0.74, 0.16), "Nules": (0.66, 0.40), "Sagunto": (0.58, 0.60),
         "Bétera": (0.44, 0.74), "Valencia": (0.55, 0.88)}
VIAS = [("Teruel", "Sagunto"), ("Castellón", "Nules"), ("Nules", "Sagunto"), ("Sagunto", "Valencia")]


def ico(nombre, k=2):
    p = APP6 / f"{nombre}_24.png"
    im = Image.open(p).convert("RGBA")
    return im.resize((im.width * k, im.height * k), Image.NEAREST)


def bandas(w, h, seed=3):
    """Plano táctico: tres bandas de altura (meseta, media, alta) y el mar a la derecha. Celdas de 3 px."""
    rng = random.Random(seed)
    im = Image.new("RGB", (w, h), MESETA); px = im.load()
    cw = U
    for y in range(0, h, cw):
        for x in range(0, w, cw):
            fx, fy = x / w, y / h
            costa = 0.80 + 0.05 * math.sin(fy * 7) + 0.02 * math.sin(fy * 23)
            alt = (1 - fx) * 1.1 + 0.15 * math.sin(fx * 9 + fy * 5) + 0.1 * math.sin(fy * 13)
            col = NAVY if fx > costa else ALTA if alt > 0.85 else MEDIA if alt > 0.55 else MESETA
            for yy in range(y, min(h, y + cw)):
                for xx in range(x, min(w, x + cw)): px[xx, yy] = col
    return im


def mancha(d, cx, cy, r, estado, rng, w, h):
    """La horda en el mapa (sin texto): Dispersa = punteado claro y amplio sin núcleo; Al acecho = núcleo + orla punteada;
    Enjambre = masa maciza con contorno hueso de 1 u (se ve también sobre la meseta)."""
    if estado == "dispersa":
        for _ in range(int(r * r / 30)):
            a, rr = rng.uniform(0, 2 * math.pi), r * 1.6 * math.sqrt(rng.random())
            x, y = int(cx + rr * math.cos(a)) // U * U, int(cy + rr * math.sin(a) * 0.8) // U * U
            d.rectangle([x, y, x + U - 1, y + U - 1], fill=HORDA)
    elif estado == "acecho":
        for yy in range(int(cy - r), int(cy + r), U):
            for xx in range(int(cx - r * 1.3), int(cx + r * 1.3), U):
                q = ((xx - cx) / (r * 1.3)) ** 2 + ((yy - cy) / r) ** 2
                if q < 0.35 or (q < 1 and (xx // U + yy // U) % 2 == 0 and rng.random() < 0.8):
                    d.rectangle([xx, yy, xx + U - 1, yy + U - 1], fill=HORDA)
    else:
        pts = []
        for yy in range(int(cy - r), int(cy + r), U):
            for xx in range(int(cx - r * 1.3), int(cx + r * 1.3), U):
                if ((xx - cx) / (r * 1.3)) ** 2 + ((yy - cy) / r) ** 2 < 1 + 0.15 * math.sin(xx * 0.2):
                    pts.append((xx, yy))
        s = set(pts)
        for xx, yy in pts:
            borde = any((xx + dx, yy + dy) not in s for dx, dy in ((U, 0), (-U, 0), (0, U), (0, -U)))
            d.rectangle([xx, yy, xx + U - 1, yy + U - 1], fill=HUESO if borde else HORDA)


def columna(d, a, b, col_borde=HUESO):
    """Columna de migración del Enjambre: galones macizos de la masa hacia el nodo, con contorno hueso; se lee a cualquier zoom."""
    ax, ay = a; bx, by = b
    L = math.hypot(bx - ax, by - ay); ux, uy = (bx - ax) / L, (by - ay) / L; nx, ny = -uy, ux
    s, paso, ancho = 2 * U, 8 * U, 5 * U
    while s < L - 6 * U:
        cx, cy = ax + ux * s, ay + uy * s
        tip = (cx + ux * 4 * U, cy + uy * 4 * U)
        nt = (cx + ux * 1.5 * U, cy + uy * 1.5 * U)                    # muesca: galón en V, no rombo
        p = [(cx + nx * ancho - ux * U, cy + ny * ancho - uy * U), tip, (cx - nx * ancho - ux * U, cy - ny * ancho - uy * U), nt]
        d.polygon(p, fill=HORDA, outline=col_borde)
        s += paso


def flecha(d, a, b, col, g=U):
    ax, ay = a; bx, by = b
    L = math.hypot(bx - ax, by - ay); ux, uy = (bx - ax) / L, (by - ay) / L; nx, ny = -uy, ux
    d.line([a, b], fill=col, width=g)
    p = 7 * U
    d.line([b, (bx - ux * p + nx * p * 0.7, by - uy * p + ny * p * 0.7)], fill=col, width=g)
    d.line([b, (bx - ux * p - nx * p * 0.7, by - uy * p - ny * p * 0.7)], fill=col, width=g)


def mapa(w, h, estado="acecho", leyenda=True, ojos_nules=False, seed=5, k_ico=2, xr=(0.0, 1.0), yr=(0.0, 1.0)):
    rng = random.Random(seed)
    im = bandas(w, h).convert("RGBA"); d = ImageDraw.Draw(im)
    P = {n: (int((xr[0] + x * (xr[1] - xr[0])) * w), int((yr[0] + y * (yr[1] - yr[0])) * h)) for n, (x, y) in NODOS.items()}
    for a, b in VIAS: d.line([P[a], P[b]], fill=GRIS_L, width=U)
    # horda al oeste de Nules
    nx, ny = P["Nules"]
    hc = (nx - int(0.30 * w), ny - int(0.02 * h))
    mancha(d, hc[0], hc[1], int(0.07 * h + 10), estado, rng, w, h)
    if estado == "enjambre": columna(d, (hc[0] + int(0.07 * h), hc[1]), (nx - 6 * U, ny))
    else: flecha(d, (hc[0] + int(0.10 * h), hc[1]), (nx - 8 * U, ny), HUESO)
    # Castellón: sin identificar (en cualquier estado)
    for n, (x, y) in P.items():
        d.rectangle([x - 2 * U, y - 2 * U, x + 2 * U, y + 2 * U], fill=GRIS_L, outline=NEGRO)
    def pon(nombre, x, y, k=k_ico):
        i = ico(nombre, k); im.alpha_composite(i, (int(x), int(y))); return i.width
    f = font(15 if k_ico >= 2 else 12, "SemiBold")
    for n, (x, y) in P.items():
        d.text((x + 3 * U, y + 2 * U), n, fill=HUESO, font=f)
    hy = -26 * k_ico
    # Sagunto: 2 pelotones + Dragón; Nules: 1 pelotón; Bétera: 2 NH90; Castellón: trébol
    x, y = P["Sagunto"]; xx = x + 3 * U
    for nm in ("app6_amigo_infanteria_peloton", "app6_amigo_infanteria_peloton", "app6_amigo_ruedas_peloton"): xx += pon(nm, xx, y + hy) + 2 * U
    x, y = P["Nules"]; xx = x + 3 * U
    simb = {"dispersa": "app6_desconocido_vacio", "acecho": "app6_horda_tramado", "enjambre": "app6_horda_vacio"}[estado]
    xx += pon(simb, xx, y + hy) + U
    cifra = {"dispersa": "~2.000?", "acecho": "~2.000", "enjambre": "2.400"}[estado] if not ojos_nules else "~2.400"
    d.text((xx, y + hy + 8 * k_ico // 2), cifra, fill=HUESO, font=f); xx += int(f.getlength(cifra)) + 3 * U
    pon("app6_amigo_infanteria_peloton", xx, y + hy)
    x, y = P["Bétera"]; xx = x + 3 * U
    for _ in range(2): xx += pon("app6_aire_helicoptero", xx, y + hy) + 2 * U
    x, y = P["Castellón"]; pon("app6_desconocido_vacio", x + 3 * U, y + hy)
    if leyenda:
        ly = h - 30
        d.rectangle([0, ly - 6, w, h], fill=NAVY)
        d.text((10, ly), "rectángulo: propio · cúpula: NH90 · rombo: horda (discontinuo: anunciada) · trébol: sin identificar · «?»: sin ojos", fill=GRIS, font=font(13))
    return im


# ---------------------------------------------------------------- glifo de estado de la Colmena (sin texto)
def glifo(estado, k=U):
    """16×16 u: Dispersa = 6 puntos sueltos; Al acecho = anillo cerrado de puntos (escucha); Enjambre = puntos fundidos en una punta."""
    g = Image.new("RGBA", (16 * k, 16 * k), (0, 0, 0, 0)); d = ImageDraw.Draw(g)
    def p(x, y, c=HUESO): d.rectangle([x * k, y * k, x * k + 2 * k - 1, y * k + 2 * k - 1], fill=c)
    if estado == "dispersa":
        for x, y in ((1, 2), (9, 1), (13, 7), (4, 9), (10, 12), (1, 13)): p(x, y)
    elif estado == "acecho":
        for a in range(8):
            t = a * math.pi / 4; p(int(7 + 5.5 * math.cos(t)), int(7 + 5.5 * math.sin(t)))
        p(7, 7)
    else:
        d.polygon([(1 * k, 3 * k), (15 * k, 8 * k), (1 * k, 13 * k), (5 * k, 8 * k)], fill=HUESO)
    return g


# ---------------------------------------------------------------- las 5 composiciones
def cabecera(d, x, y, w, compacta=False):
    texto(d, (x, y), CAB["titulo"], 30, HUESO, "Bold")
    tw = font(30, "Bold").getlength(CAB["titulo"])
    texto(d, (x + tw + 24, y + 9), CAB["num"], 18, GRIS, "SemiBold")
    dt = font(30, "Bold").getlength(DTG)
    texto(d, (x + w - dt, y), DTG, 30, HUESO, "Bold")
    y += 44
    if not compacta:
        texto(d, (x, y), f"De: {CAB['de']}   ·   Periodo: {CAB['periodo']}", 16, GRIS)
        cl = font(14, "SemiBold").getlength(CAB["clas"])
        d.rectangle([x + w - cl - 16, y - 2, x + w, y + 20], outline=GRIS, width=2)
        texto(d, (x + w - cl - 8, y + 1), CAB["clas"], 14, GRIS, "SemiBold")
        y += 30
    return y


def resumen(d, x, y, w, estado="acecho"):
    d.rectangle([x, y, x + w, y + 58], fill=NAVY2)
    d.rectangle([x, y, x + U, y + 58], fill=HUESO)
    g = glifo(estado, 2)
    return g, (x + 14, y + 13), y + 58 + 16


def firma(d, x, y, w):
    d.line([x, y, x + w, y], fill=BORDE, width=2); y += 10
    texto(d, (x, y), FIRMA[0], 20, HUESO, "SemiBold"); texto(d, (x + 160, y + 3), FIRMA[1], 15, GRIS)
    return y + 32


def poner_resumen(im, d, x, y, w, estado="acecho"):
    g, pos, ny = resumen(d, x, y, w, estado)
    im.alpha_composite(g, pos)
    for i, l in enumerate(envolver(RESUMEN, 21, w - 70, "SemiBold")[:2]):
        texto(d, (x + 60, y + 6 + i * 25), l, 21, HUESO, "SemiBold")
    return ny


def fondo_pantalla():
    im = Image.new("RGBA", (1920, 1080), (0x10, 0x12, 0x20, 255))
    return im


def boton(d, box, s, estilo):
    panel(d, box, estilo, fondo=NAVY2)
    f = font(16, "SemiBold"); tw = f.getlength(s)
    texto(d, ((box[0] + box[2]) / 2 - tw / 2, (box[1] + box[3]) / 2 - 10), s, 16, HUESO, "SemiBold")


def v1(estilo="remaches"):
    """V1 · Dos columnas: el mapa grande a la izquierda (60 %) y el parte a la derecha, de arriba abajo."""
    im = fondo_pantalla(); d = ImageDraw.Draw(im)
    panel(d, (24, 24, 1896, 1056), estilo)
    y = cabecera(d, 60, 54, 1800)
    y = poner_resumen(im, d, 60, y, 1800)
    mw, mh = 1080, 1056 - 60 - y
    m = mapa(mw, mh); panel(d, (54, y - 6, 54 + mw + 12, y + mh + 6), "plano"); im.alpha_composite(m, (60, y))
    x, w = 60 + mw + 40, 1800 - mw - 40
    yy = seccion(d, x, y, w, "Situación de la amenaza", AMENAZA, num=3)
    yy = seccion(d, x, yy, w, "Fuerzas propias", PROPIAS, num=4)
    yy = seccion(d, x, yy, w, "Sin novedad en", SIN_NOV, num=5)
    yy = seccion(d, x, yy, w, "Valoración", VALORACION, num=6)
    yy = seccion(d, x, yy, w, "Necesidades", NECESIDADES, num=7)
    firma(d, x, 1056 - 60 - 36, w)
    return im


def v2(estilo="bisel"):
    """V2 · Carpeta: mapa a todo el ancho arriba; debajo, tres columnas (amenaza | propias + sin novedad | valoración + necesidades)."""
    im = fondo_pantalla(); d = ImageDraw.Draw(im)
    panel(d, (24, 24, 1896, 1056), estilo)
    y = cabecera(d, 60, 54, 1800)
    y = poner_resumen(im, d, 60, y, 1800)
    mh = 440
    m = mapa(1800, mh, leyenda=False, yr=(0.02, 0.90)); im.alpha_composite(m, (60, y)); d.rectangle([60, y, 1860, y + mh], outline=BORDE, width=2)
    texto(d, (60, y + mh + 6), "rectángulo: propio · cúpula: NH90 · rombo: horda (discontinuo: anunciada) · trébol: sin identificar · «?»: sin ojos", 13, GRIS)
    y += mh + 34
    cw = (1800 - 60) // 3
    seccion(d, 60, y, cw, "Situación de la amenaza", AMENAZA, num=3)
    yy = seccion(d, 60 + cw + 30, y, cw, "Fuerzas propias", PROPIAS, num=4)
    seccion(d, 60 + cw + 30, yy, cw, "Sin novedad en", SIN_NOV, num=5)
    yy = seccion(d, 60 + 2 * (cw + 30), y, cw, "Valoración", VALORACION, num=6)
    seccion(d, 60 + 2 * (cw + 30), yy, cw, "Necesidades", NECESIDADES, num=7)
    firma(d, 60, 1056 - 60 - 36, 1800)
    return im


def v3(estilo="escuadras"):
    """V3 · Teletipo: el parte como un mensaje impreso, por líneas numeradas, en una columna central; el mapa, recuadro a la derecha."""
    im = fondo_pantalla(); d = ImageDraw.Draw(im)
    panel(d, (200, 24, 1720, 1056), estilo)
    x, w = 250, 800
    y = cabecera(d, x, 60, 1420)
    y = poner_resumen(im, d, x, y, 1420)
    n = 1
    for tit, l in (("Situación de la amenaza", AMENAZA), ("Fuerzas propias", PROPIAS), ("Sin novedad en", SIN_NOV),
                   ("Valoración", VALORACION), ("Necesidades", NECESIDADES)):
        texto(d, (x, y), f"{n + 2:02d}", 15, GRIS, "SemiBold")
        y = seccion(d, x + 40, y, w - 40, tit, l, vinetas=False)
        n += 1
    m = mapa(560, 620, leyenda=False)
    im.alpha_composite(m, (1110, 250)); d.rectangle([1110, 250, 1670, 870], outline=GRIS, width=2)
    texto(d, (1110, 880), "rectángulo propio · rombo horda · trébol sin identificar · «?» sin ojos", 13, GRIS)
    firma(d, x, 1056 - 60 - 36, 1420)
    return im


def v4(estilo="doble"):
    """V4 · Mesa de mapas: el mapa ocupa la pantalla; el parte va en tarjetas a los lados (amenaza y valoración a la izquierda,
    propias, sin novedad y necesidades a la derecha) y la cabecera arriba en una banda."""
    im = fondo_pantalla(); d = ImageDraw.Draw(im)
    m = mapa(1920, 1080, leyenda=False, k_ico=3, xr=(0.30, 0.72), yr=(0.12, 0.92)); im.alpha_composite(m, (0, 0))
    panel(d, (24, 24, 1896, 170), estilo)
    y = cabecera(d, 60, 48, 1800, compacta=True)
    g = glifo("acecho", 2); im.alpha_composite(g, (60, y + 4))
    for i, l in enumerate(envolver(RESUMEN, 21, 1700, "SemiBold")[:1]): texto(d, (110, y + 4), l, 21, HUESO, "SemiBold")
    panel(d, (24, 196, 520, 760), estilo)
    yy = seccion(d, 54, 226, 440, "Situación de la amenaza", AMENAZA, num=3)
    seccion(d, 54, yy, 440, "Valoración", VALORACION, num=6)
    panel(d, (1400, 196, 1896, 860), estilo)
    yy = seccion(d, 1430, 226, 440, "Fuerzas propias", PROPIAS, num=4)
    yy = seccion(d, 1430, yy, 440, "Sin novedad en", SIN_NOV, num=5)
    seccion(d, 1430, yy, 440, "Necesidades", NECESIDADES, num=7)
    panel(d, (24, 960, 1896, 1056), estilo)
    firma(d, 54, 980, 1000)
    texto(d, (1100, 995), "rectángulo propio · cúpula NH90 · rombo horda · trébol sin identificar · «?» sin ojos", 14, GRIS)
    return im


def v5(estilo="cortadas"):
    """V5 · Pestañas: cabecera y resumen arriba; a la izquierda, los apartados como pestañas (uno abierto); el mapa a la derecha."""
    im = fondo_pantalla(); d = ImageDraw.Draw(im)
    panel(d, (24, 24, 1896, 1056), estilo)
    y = cabecera(d, 60, 54, 1800)
    y = poner_resumen(im, d, 60, y, 1800)
    tabs = [("3 · Amenaza", True), ("4 · Fuerzas propias", False), ("5 · Sin novedad", False), ("6 · Valoración", False), ("7 · Necesidades", False)]
    ty = y
    for t, on in tabs:
        box = (60, ty, 330, ty + 52)
        panel(d, box, "cortadas" if on else "plano", fondo=NAVY2 if on else NAVY)
        if on: d.rectangle([box[0] + 2 * U, box[1] + 2 * U, box[0] + 3 * U, box[3] - 2 * U], fill=HUESO)
        texto(d, (84, ty + 15), t, 18, HUESO if on else GRIS, "SemiBold")
        ty += 60
    panel(d, (340, y, 820, 1056 - 120), "plano", fondo=NAVY2)
    yy = seccion(d, 366, y + 20, 430, "Situación de la amenaza", AMENAZA, px=20)
    for i, l in enumerate(envolver("Valoración: " + VALORACION[0], 17, 430)): texto(d, (366, yy + 6 + i * 24), l, 17, GRIS)
    m = mapa(1000, 1056 - 120 - y); im.alpha_composite(m, (860, y)); d.rectangle([860, y, 1860, 1056 - 120], outline=BORDE, width=2)
    firma(d, 60, 1056 - 100, 1800)
    boton(d, (1600, 1056 - 96, 1860, 1056 - 54), "Cerrar  (Esc)", "cortadas")
    return im


VARIANTES = [("V1", "Dos columnas", "remaches (riveted)", v1), ("V2", "Carpeta", "bisel (bevelled edges)", v2),
             ("V3", "Teletipo", "escuadras (stamped, corner brackets)", v3), ("V4", "Mesa de mapas", "marco doble (double frame)", v4),
             ("V5", "Pestañas", "esquinas cortadas (cut corners)", v5)]


# ---------------------------------------------------------------- avisos (CO-1..CO-4 y el ámbar) y estados en el mapa
def hoja_avisos():
    im = fondo_pantalla(); d = ImageDraw.Draw(im)
    texto(d, (40, 30), "Avisos de la Colmena · CO-1 a CO-4 y el ámbar de la señal 6 (textos provisionales)", 26, HUESO, "Bold")
    # (a) panel de avisos del mapa con el ámbar
    panel(d, (40, 90, 740, 330), "plano")
    texto(d, (70, 112), "Mapa · panel de avisos (arriba a la izquierda)", 15, GRIS, "SemiBold")
    al = Image.open(APP6 / "app6_horda_tramado_24.png").convert("RGBA")
    filas = [("La Colmena se agita: si abres más nodos, irá a por ti", AMBAR, "aviso (señal ≥ 6000 ‰), ámbar porque es un aviso, no un ataque"),
             ("La horda va hacia Nules (llega hacia las 22:00) y no hay tropa", AMBAR, "el aviso que ya existe (zona sin tropa)"),
             ("Tren 2 atacado en Sagunto–Castellón km 12", ROJO, "rojo: solo ataques")]
    y = 150
    for s, c, nota in filas:
        d.rectangle([70, y + 4, 70 + 4 * U, y + 4 + 4 * U], fill=c)
        texto(d, (100, y), s, 19, c, "SemiBold"); texto(d, (100, y + 26), nota, 13, GRIS); y += 58
    # (b) entradas del informe al cambiar de estado
    panel(d, (780, 90, 1880, 610), "plano")
    texto(d, (810, 112), "Informe · entradas al cambiar de estado (con el glifo del estado, sin color)", 15, GRIS, "SemiBold")
    ent = [("CO-1", "acecho", "D38 0600A", "Mora", "Se mueve distinto. Ya no vaga: escucha.", False),
           ("CO-2", "enjambre", "D41 0600A", "Mora", "Se está juntando. Has crecido demasiado deprisa y lo ha notado.", True),
           ("CO-3", "acecho", "D44 0600A", "Mora", "Se dispersa otra vez. Por ahora.", False),
           ("CO-4", "dispersa", "D47 0600A", "Mora", "Ha perdido el interés. Vaga sin rumbo.", False)]
    y = 150
    for cid, est, dtg, src, s, causa in ent:
        if causa: d.rectangle([800, y - 8, 1860, y + 96], fill=NAVY2); d.rectangle([800, y - 8, 800 + U, y + 96], fill=HUESO)
        im.alpha_composite(glifo(est, 3), (820, y))
        texto(d, (880, y), f"{dtg} · {src}", 15, GRIS, "SemiBold"); texto(d, (1100, y), cid, 15, GRIS)
        texto(d, (880, y + 22), s, 21, HUESO, "SemiBold" if causa else "Medium")
        if causa:
            texto(d, (880, y + 54), "Causa: abriste 7 nodos en 6 días (señal 7,5). Si dejas de abrir, se calma en 3 días.", 16, GRIS)
            y += 30
        y += 92
    # (c) banda de cambio de estado arriba al centro (2,5 s), sin color
    panel(d, (560, 660, 1360, 760), "escuadras")
    im.alpha_composite(glifo("enjambre", 3), (600, 686))
    texto(d, (670, 678), "LA COLMENA · ENJAMBRE", 26, HUESO, "Bold")
    texto(d, (670, 716), "Se está juntando. Has crecido demasiado deprisa.", 17, GRIS)
    texto(d, (560, 770), "Banda arriba al centro durante 2,5 s al cambiar de estado; el motivo suena (D-N07). Hueso: lo pinta la forma, no el color.", 14, GRIS)
    # (d) los tres glifos
    texto(d, (40, 860), "Glifo del estado (16×16 u, en hueso; va en la cabecera del informe y en la banda):", 17, HUESO, "SemiBold")
    x = 40
    for est, nm in (("dispersa", "Dispersa: puntos sueltos"), ("acecho", "Al acecho: anillo cerrado (escucha)"), ("enjambre", "Enjambre: puntos fundidos en una punta")):
        im.alpha_composite(glifo(est, 4), (x, 900)); texto(d, (x + 80, 920), nm, 17, GRIS); x += 560
    return im


def hoja_mapa():
    im = fondo_pantalla(); d = ImageDraw.Draw(im)
    texto(d, (40, 26), "La Colmena en el mapa, sin ojos y sin texto · cerca (arriba) y de lejos (abajo, a ½)", 26, HUESO, "Bold")
    W, H = 600, 420
    for i, (est, nm, nota) in enumerate((("dispersa", "Dispersa", "punteado suelto y ancho, sin núcleo; trébol; «~2.000?»"),
                                         ("acecho", "Al acecho", "núcleo macizo con orla punteada; rombo discontinuo; flecha"),
                                         ("enjambre", "Enjambre", "masa maciza con contorno hueso; columna de galones; «2.400»"))):
        x = 40 + i * (W + 30)
        m = mapa(W, H, est, leyenda=False, seed=11)
        im.alpha_composite(m, (x, 80)); d.rectangle([x, 80, x + W, 80 + H], outline=BORDE, width=2)
        im.alpha_composite(glifo(est, 3), (x, 80 + H + 10))
        texto(d, (x + 60, 80 + H + 12), nm, 22, HUESO, "Bold"); texto(d, (x + 60, 80 + H + 42), nota, 15, GRIS)
        # de lejos: el mismo mapa a la mitad (iconos a ×1), como con la cámara alta
        m2 = mapa(W // 2 * 2, H // 2 * 2, est, leyenda=False, seed=11, k_ico=1).resize((W // 2, H // 2), Image.NEAREST)
        im.alpha_composite(m2, (x + W // 4, 600)); d.rectangle([x + W // 4, 600, x + W // 4 + W // 2, 600 + H // 2], outline=BORDE, width=2)
    texto(d, (40, 840), "Reglas: la masa de la horda siempre en #1c200c (lo más oscuro, D-N61). Lo que cambia entre estados es la FORMA: suelta, con núcleo o maciza con borde.", 17, GRIS)
    texto(d, (40, 868), "El Enjambre se ve de lejos por la columna de galones con borde hueso (también sobre la meseta) y porque la cifra no lleva «~».", 17, GRIS)
    texto(d, (40, 896), "Sin rojo ni ámbar: el rojo entra solo si ataca (rombo rojo y flecha roja) y el ámbar solo en el aviso «La Colmena se agita».", 17, GRIS)
    texto(d, (40, 924), "De lejos (½), Dispersa y Al acecho se separan por el núcleo: sin núcleo es ruido; con núcleo, algo se ha juntado y escucha.", 17, GRIS)
    return im


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    ims = []
    for k, nm, est, fn in VARIANTES:
        im = fn(); im.convert("RGB").save(OUT / f"informe_{k}.png"); ims.append((k, nm, est, im))
        print(k, nm)
    hoja_avisos().convert("RGB").save(OUT / "colmena_avisos.png")
    hoja_mapa().convert("RGB").save(OUT / "colmena_mapa.png")
    # hoja de contactos de las 5 (a ½)
    sw, sh = 960, 540
    hoja = Image.new("RGB", (2 * sw + 60, 3 * (sh + 50) + 60), (0x10, 0x12, 0x20)); d = ImageDraw.Draw(hoja)
    d.text((20, 14), "D-N160 · Informe de inteligencia: 5 composiciones (a ½; cada una a 1920×1080 en su PNG). Textos provisionales.", fill=HUESO, font=font(22, "Bold"))
    for i, (k, nm, est, im) in enumerate(ims):
        x, y = 20 + (i % 2) * (sw + 20), 60 + (i // 2) * (sh + 50)
        hoja.paste(im.convert("RGB").resize((sw, sh), Image.LANCZOS), (x, y))
        d.text((x, y + sh + 6), f"{k} · {nm} · marco: {est}", fill=HUESO, font=font(18, "SemiBold"))
    hoja.save(OUT / "hoja_informe.png")


if __name__ == "__main__":
    main()
