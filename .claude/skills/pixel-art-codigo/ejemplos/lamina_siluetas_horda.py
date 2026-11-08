"""D-N80 · Lámina y medida de las siluetas de zombi de Teo, vistas como en el juego. Bruno (Arte técnico), 8-nov.

Comprueba que las 5 (o más) siluetas se distinguen POR LA FORMA, porque el valor no puede cambiar: toda la horda
va en L* 9–13 (D-N61, D-N80). Cámara del juego: ortográfica, inclinación 55°, ortho 26, RT del mundo a 1/3
(360 líneas para 1080p), así que 1 px son 2·26/360 = 0,144 m y un zombi de 1,75 m a ×1,3 mide unos 16 px.

Entrada: OBJ (Blender: Archivo → Exportar → Wavefront, +Y arriba, con «Colores» si se quiere la máscara).
  Un OBJ por silueta. Se toma la malla tal cual (pivote en los pies, delante hacia −Z del OBJ = +Z de Unity).
Salida (en --salida):
  lamina_horda.png   cada silueta en 8 orientaciones a 1 px = 0,144 m (×4 para verla), sobre suelo de noche, y a ×3 de zoom
  siluetas.csv       triángulos, altura en px, ancho medio y poses de cada silueta
  pares.csv          por par: IoU media (pose base, 8 orientaciones), IoU máxima (todas las orientaciones y poses) y veredicto
Criterio (Lucía, 8-nov): en negro puro, sin tono por instancia; IoU media <= 0,70 por par y ningún par > 0,80 en ninguna
orientación ni fotograma clave (andar, trepar). Si no, a 16 px se leen como la misma figura: exagerar encorvado, hombros, brazos, sombrero.
Fotogramas clave: un OBJ por pose, «<silueta>__<pose>.obj» (p. ej. civil__andar1.obj, civil__trepar.obj); la pose base es
«<silueta>.obj» o, si no está, la primera por orden alfabético.

Uso: python3 -I lamina_siluetas_horda.py civil.obj obrero.obj ... [--salida DIR] [--ortho 26] [--escala 1.3]
     python3 -I lamina_siluetas_horda.py --prueba      (5 figuras de cajas, para comprobar la herramienta)
Solo necesita Pillow.
"""
import csv
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw

HORDA = (0x1c, 0x20, 0x0c)
SUELO_NOCHE = (0x48, 0x40, 0x3c)       # L* ≈ 28, el suelo más oscuro que se lee de noche
SOMBRA = (0x30, 0x34, 0x24)
HUESO = (0xd4, 0xd0, 0xcc)
PITCH = 55.0
YAWS = [0, 45, 90, 135, 180, 225, 270, 315]


def leer_obj(path):
    vs, tris = [], []
    for ln in Path(path).read_text(encoding="utf-8", errors="replace").splitlines():
        p = ln.split()
        if not p:
            continue
        if p[0] == "v":
            vs.append(tuple(float(x) for x in p[1:4]))
        elif p[0] == "f":
            idx = [int(t.split("/")[0]) for t in p[1:]]
            idx = [i - 1 if i > 0 else len(vs) + i for i in idx]
            for k in range(1, len(idx) - 1):
                tris.append((idx[0], idx[k], idx[k + 1]))
    return vs, tris


def caja(cx, cy, cz, sx, sy, sz, inc=0.0):
    """Caja centrada en (cx, cy, cz), inclinada 'inc' grados hacia delante (−Z) sobre su base."""
    vs = []
    for dx in (-1, 1):
        for dy in (0, 1):
            for dz in (-1, 1):
                x, y, z = dx * sx / 2, dy * sy, dz * sz / 2
                a = math.radians(inc)
                y, z = y * math.cos(a) + z * math.sin(a), -y * math.sin(a) + z * math.cos(a)
                vs.append((cx + x, cy + y, cz + z))
    caras = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    return vs, [t for c in caras for t in ((c[0], c[1], c[2]), (c[0], c[2], c[3]))]


def unir(*partes):
    vs, tris = [], []
    for pv, pt in partes:
        o = len(vs); vs += pv; tris += [(a + o, b + o, c + o) for a, b, c in pt]
    return vs, tris


def prueba():
    """Cinco figuras de cajas, solo para probar la herramienta (no son las de Teo)."""
    return {
        "civil": unir(caja(0, 0, 0, .40, .85, .25), caja(0, .85, 0, .48, .65, .28), caja(0, 1.5, 0, .22, .25, .22),
                      caja(-.3, .9, -.2, .12, .55, .12, 40), caja(.3, .9, -.2, .12, .55, .12, 40)),
        "obrero": unir(caja(0, 0, 0, .40, .85, .25), caja(0, .85, 0, .48, .62, .28), caja(0, 1.47, 0, .22, .23, .22),
                       caja(0, 1.68, 0, .62, .05, .62), caja(.3, .9, 0, .12, .6, .12)),
        "uniformado": unir(caja(0, 0, 0, .42, .85, .26), caja(0, .85, 0, .58, .62, .38), caja(0, 1.47, 0, .30, .28, .30),
                           caja(-.34, .9, -.25, .13, .55, .13, 70), caja(.34, .9, -.25, .13, .55, .13, 70)),
        "gordo": unir(caja(0, 0, 0, .55, .8, .4), caja(0, .8, 0, .78, .62, .6), caja(0, 1.42, 0, .26, .24, .26),
                      caja(-.45, .85, 0, .16, .55, .16), caja(.45, .85, 0, .16, .55, .16)),
        "descarnado": unir(caja(0, 0, .25, .3, .8, .2, -25), caja(0, .7, -.15, .32, .6, .2, 45), caja(0, 1.05, -.6, .2, .22, .2),
                           caja(-.22, .8, .2, .08, .6, .08, -60), caja(.22, .8, .2, .08, .6, .08, -60)),
    }


def proyectar(v, yaw, escala):
    """Mundo (Unity, +Y arriba) → pantalla de la cámara: x a la derecha, y hacia abajo, en metros."""
    x, y, z = v[0] * escala, v[1] * escala, v[2] * escala
    a = math.radians(yaw)
    x, z = x * math.cos(a) - z * math.sin(a), x * math.sin(a) + z * math.cos(a)
    p = math.radians(PITCH)
    sy = y * math.cos(p) + z * math.sin(p)          # la cámara mira hacia +Z desde arriba
    return x, -sy


def mascara(vs, tris, yaw, escala, px, lado):
    """Silueta rellena en una imagen 'lado'x'lado' (1 = figura), con los pies en el centro inferior."""
    im = Image.new("1", (lado, lado), 0)
    d = ImageDraw.Draw(im)
    pts = [proyectar(v, yaw, escala) for v in vs]
    for a, b, c in tris:
        poly = [(lado / 2 + pts[i][0] / px, lado * 0.8 + pts[i][1] / px) for i in (a, b, c)]
        d.polygon(poly, fill=1)
    return im


def sombra(vs, tris, escala, px, lado):
    """Sombra del sol de noche (luna desde arriba a la izquierda, D-N69) aplastada sobre el suelo."""
    im = Image.new("1", (lado, lado), 0)
    d = ImageDraw.Draw(im)
    def s(v):
        x, y, z = v[0] * escala, v[1] * escala, v[2] * escala
        x, z = x + y * 0.6, z + y * 0.35                  # luz hacia +x, +z
        return x, -(z * math.sin(math.radians(PITCH)))
    pts = [s(v) for v in vs]
    for a, b, c in tris:
        d.polygon([(lado / 2 + pts[i][0] / px, lado * 0.8 + pts[i][1] / px) for i in (a, b, c)], fill=1)
    return im


def iou(a, b):
    pa, pb = a.load(), b.load()
    w, h = a.size
    i = u = 0
    for y in range(h):
        for x in range(w):
            ea, eb = pa[x, y], pb[x, y]
            i += ea and eb
            u += ea or eb
    return i / u if u else 1.0


def medidas(m):
    bb = m.getbbox()
    if not bb:
        return 0, 0
    w, h = m.size
    pm = m.load()
    anchos = [sum(pm[x, y] for x in range(w)) for y in range(bb[1], bb[3])]
    return bb[3] - bb[1], sum(anchos) / len(anchos)


def main(argv):
    salida, ortho, escala, objs, usar_prueba = Path("out_siluetas_horda"), 26.0, 1.3, [], False
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--salida": salida = Path(argv[i + 1]); i += 2; continue
        if a == "--ortho": ortho = float(argv[i + 1]); i += 2; continue
        if a == "--escala": escala = float(argv[i + 1]); i += 2; continue
        if a == "--prueba": usar_prueba = True; i += 1; continue
        objs.append(Path(a)); i += 1
    poses = {}                                       # silueta → {pose: malla}; «civil__trepar.obj» es la pose «trepar» de «civil»
    if usar_prueba:
        poses = {n: {"base": m} for n, m in prueba().items()}
    else:
        for p in objs:
            n, _, pose = p.stem.partition("__")
            poses.setdefault(n, {})[pose or "base"] = leer_obj(p)
    figuras = {n: (ps["base"] if "base" in ps else ps[sorted(ps)[0]]) for n, ps in poses.items()}
    if len(figuras) < 2:
        print(__doc__); return 1
    salida.mkdir(parents=True, exist_ok=True)
    px = 2 * ortho / 360.0
    L = 40
    nombres = list(figuras)
    masc = {n: [mascara(*figuras[n], y, escala, px, L) for y in YAWS] for n in nombres}

    # Lámina: fila por figura, 8 orientaciones a ortho 26 (×4) y una a ×3 de zoom (ortho 26/3) con sombra.
    K, Z = 4, 3
    celda = L * K
    out = Image.new("RGB", (celda * (len(YAWS) + 1) + 140, celda * len(nombres) + 30), (0, 0, 0))
    d = ImageDraw.Draw(out)
    for fi, n in enumerate(nombres):
        y0 = 30 + fi * celda
        d.text((6, y0 + celda // 2), n[:18], fill=HUESO)
        for k, yaw in enumerate(YAWS):
            base = Image.new("RGB", (L, L), SUELO_NOCHE)
            base.paste(SOMBRA, (0, 0), sombra(*figuras[n], escala, px, L))
            base.paste(HORDA, (0, 0), masc[n][k])
            out.paste(base.resize((celda, celda), Image.NEAREST), (140 + k * celda, y0))
        LZ = L * K // 2                                  # lienzo mayor, ×2 en la lámina
        grande = Image.new("RGB", (LZ, LZ), SUELO_NOCHE)
        grande.paste(SOMBRA, (0, 0), sombra(*figuras[n], escala, px / Z, LZ))
        grande.paste(HORDA, (0, 0), mascara(*figuras[n], 30, escala, px / Z, LZ))
        out.paste(grande.resize((celda, celda), Image.NEAREST), (140 + len(YAWS) * celda, y0))
    for k, yaw in enumerate(YAWS):
        d.text((140 + k * celda + 4, 8), f"{yaw}°", fill=HUESO)
    d.text((140 + len(YAWS) * celda + 4, 8), "x3 zoom", fill=HUESO)
    out.save(salida / "lamina_horda.png")

    # Medida de forma (criterio de Lucía, 8-nov): siluetas en negro puro, sin el tono por instancia.
    #  1) IoU media <= 0,70 por par, en la pose base y en las 8 orientaciones de cámara;
    #  2) ningún par > 0,80 en ninguna orientación ni en ningún fotograma clave (cada pose de A contra cada pose de B).
    mp = {n: {po: [mascara(*m, y, escala, px, L) for y in YAWS] for po, m in ps.items()} for n, ps in poses.items()}
    filas, pares = [], []
    for ai, a in enumerate(nombres):
        alto, ancho = medidas(masc[a][0])
        filas.append({"silueta": a, "triangulos": len(figuras[a][1]), "alto_px": alto, "ancho_medio_px": round(ancho, 1),
                      "poses": " ".join(sorted(poses[a]))})
        for b in nombres[ai + 1:]:
            media = sum(iou(masc[a][k], masc[b][k]) for k in range(len(YAWS))) / len(YAWS)
            pico, donde = 0.0, ""
            for pa, ma in mp[a].items():
                for pb, mb in mp[b].items():
                    for k, yaw in enumerate(YAWS):
                        v = iou(ma[k], mb[k])
                        if v > pico:
                            pico, donde = v, f"{pa}/{pb} a {yaw}°"
            ok = media <= 0.70 and pico <= 0.80
            pares.append({"par": f"{a} / {b}", "iou_media": round(media, 2), "iou_max": round(pico, 2), "peor_caso": donde,
                          "apto": "APTO" if ok else ("MEDIA > 0,70" if media > 0.70 else "PICO > 0,80")})
    with open(salida / "siluetas.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(filas[0])); w.writeheader(); w.writerows(filas)
    with open(salida / "pares.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(pares[0])); w.writeheader(); w.writerows(pares)
    print(f"{'silueta':14} {'tris':>5} {'alto':>5} {'ancho':>6}  poses")
    for r in filas:
        print(f"{r['silueta']:14} {r['triangulos']:5} {r['alto_px']:5} {r['ancho_medio_px']:6}  {r['poses']}")
    print(f"\n{'par':28} {'media':>5} {'máx':>5}  peor caso               veredicto")
    for r in pares:
        print(f"{r['par']:28} {r['iou_media']:5} {r['iou_max']:5}  {r['peor_caso']:22}  {r['apto']}")
    n_ok = sum(r["apto"] == "APTO" for r in pares)
    print(f"\n{n_ok}/{len(pares)} pares aptos (media <= 0,70 y máx <= 0,80) · {salida / 'lamina_horda.png'} · {salida / 'pares.csv'}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
