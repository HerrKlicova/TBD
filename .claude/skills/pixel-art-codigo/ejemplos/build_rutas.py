"""Rutas, ETA y refuerzos del mundo persistente (P1, noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:57), norma nueva de Eugenio: las unidades se mandan de un sitio a otro con el ETA visible en el
mapa, y los vehículos de tierra solo van por vías y carreteras.
  ui_eta_placa            9-slice de 12x9 (slice 3): placa navy con borde hueso donde va el «mm:ss»
  ui_eta_reloj_7          relojito de 7x7 que va a la izquierda de la cifra (unidad en camino)
  ui_eta_destino_7        banderín de 7x7 para la placa del destino
  ui_digitos_3x5          atlas de 0–9 y «:» a 3x5 (4 px de paso) en hueso, para no depender de la fuente
  decal_ruta_{carretera,via,aire}_16   tesela de 16x4 que repite a lo largo de la ruta (0,1 m/texel: 1,6 m)
  decal_ruta_{carretera,via,aire}_fin  punta de flecha de 8x8 al final de la ruta (mira a +X)
  ui_mm_ruta_{carretera,via,aire}      tesela de 4x1 para la ruta en el minimapa
  ui_aviso_refuerzos_{32,16}           aviso de información (sin rojo ni ámbar): flecha con un soldado
  decal_ruta_no_valida_32              aspa ámbar sobre el destino (aviso: el vehículo no va campo a través), 4 f
  decal_ruta_no_valida_16              tesela ámbar de 16x4 para el tramo que no se puede recorrer
Colores: carretera en hueso, vía en beige con traviesas, aire en grey_light punteado; el ámbar solo en lo no válido,
que es un aviso y dura poco (≤ 1,5 s).
"""
import json
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite, reduce_16  # noqa: E402

DIG = {"0": ["###", "#.#", "#.#", "#.#", "###"], "1": [".#.", "##.", ".#.", ".#.", "###"],
       "2": ["###", "..#", "###", "#..", "###"], "3": ["###", "..#", ".##", "..#", "###"],
       "4": ["#.#", "#.#", "###", "..#", "..#"], "5": ["###", "#..", "###", "..#", "###"],
       "6": ["###", "#..", "###", "#.#", "###"], "7": ["###", "..#", ".#.", ".#.", ".#."],
       "8": ["###", "#.#", "###", "#.#", "###"], "9": ["###", "#.#", "###", "..#", "###"],
       ":": ["...", ".#.", "...", ".#.", "..."]}
ORDEN = "0123456789:"


def digitos():
    s = Sprite(4 * len(ORDEN), 5)
    for k, ch in enumerate(ORDEN):
        for j, fila in enumerate(DIG[ch]):
            for i, c in enumerate(fila):
                if c == "#":
                    s.set(k * 4 + i, j, "bone")
    return s


def placa():
    s = Sprite(12, 9)
    s.rect(0, 0, 12, 9, "navy")
    for x in range(12):
        s.set(x, 0, "bone"); s.set(x, 8, "grey_brown")
    for y in range(9):
        s.set(0, y, "bone"); s.set(11, y, "grey_brown")
    for x, y in ((0, 0), (11, 0), (0, 8), (11, 8)):
        s.set(x, y, None)
    return s


def reloj():
    s = Sprite(7, 7)
    s.grid(0, 0, """
.bbbbb.
b..b..b
b..b..b
b..bb.b
b.....b
b.....b
.bbbbb.
""", {"b": "bone", ".": "_"})
    return s


def destino():
    s = Sprite(7, 7)
    s.grid(0, 0, """
bccc...
bcccc..
bccc...
b......
b......
b......
bb.....
""", {"b": "bone", "c": "beige", ".": "_"})
    return s


def ruta(tipo):
    """Tesela de 16x4 que repite en X. Sombra de 1 px en 303424 debajo para leerse sobre suelo claro."""
    s = Sprite(16, 4)
    if tipo == "carretera":
        for x in range(0, 10):
            s.set(x, 1, "bone"); s.set(x, 2, "bone")
            s.set(x, 3, "olive_shadow")
    elif tipo == "via":
        for x in range(16):
            if x % 8 < 6:
                s.set(x, 1, "beige")
        for x in (1, 5, 9, 13):                     # traviesas
            s.set(x, 0, "beige"); s.set(x, 2, "beige")
        for x in range(16):
            if s.get(x, 2)[3] or s.get(x, 1)[3]:
                if not s.get(x, 3)[3]:
                    s.set(x, 3, "olive_shadow")
    else:                                           # aire: pares de puntos
        for x in (0, 1, 6, 7, 12, 13):
            s.set(x, 1, "grey_light"); s.set(x, 2, "grey_light")
    return s


def fin(tipo):
    c = {"carretera": "bone", "via": "beige", "aire": "grey_light"}[tipo]
    s = Sprite(8, 8)
    for y in range(8):
        a = 4 - abs(y - 3.5)
        for x in range(int(a * 2)):
            s.set(x, y, c)
    if tipo != "aire":
        s.outline("olive_shadow")
    return s


def mm_ruta(tipo):
    s = Sprite(4, 1)
    c = {"carretera": "bone", "via": "beige", "aire": "grey_light"}[tipo]
    for x in ((0, 1) if tipo != "aire" else (0,)):
        s.set(x, 0, c)
    if tipo == "carretera":
        s.set(2, 0, c)
    return s


def refuerzos():
    """Información: flecha gruesa hacia la derecha con un soldado de pie encima, en hueso y oliva."""
    s = Sprite()
    s.poly([(2, 18), (18, 18), (18, 12), (30, 22), (18, 31), (18, 26), (2, 26)], "beige")
    s.hline(2, 17, 18, "bone"); s.line(18, 12, 29, 21, "bone")
    s.hline(2, 17, 26, "ochre_dark")
    s.rect(9, 2, 5, 4, "olive")                    # casco
    s.hline(8, 14, 5, "olive_grey")
    s.rect(10, 6, 3, 3, "tan")                     # cara
    s.rect(8, 9, 7, 7, "olive")                    # cuerpo
    s.rect(9, 10, 5, 4, "olive_grey")              # chaleco
    s.line(14, 9, 19, 4, "grey_brown")             # fusil al hombro
    s.rect(9, 16, 2, 2, "olive_dark"); s.rect(12, 16, 2, 2, "olive_dark")
    s.outline()
    return s


def no_valida(f):
    s = Sprite(32, 32)
    c = "amber" if f % 2 == 0 else "ochre_dark"
    m = 6 + (1 if f in (1, 2) else 0)
    for k in range(32 - 2 * m):
        for d in (-1, 0, 1):
            s.set(m + k + d, m + k, c); s.set(31 - m - k + d, m + k, c)
    for k in range(32 - 2 * m):
        s.set(m + k, m + k, "cream" if f % 2 == 0 else "amber")
    s.outline("olive_shadow")
    return s


def tramo_no_valido():
    s = Sprite(16, 4)
    for x in range(16):
        if x % 6 < 4:
            s.set(x, 1, "amber"); s.set(x, 2, "amber"); s.set(x, 3, "olive_shadow")
    return s


def exportar(out):
    out = Path(out)
    todo = {}
    d = digitos()
    d.img.save(out / "ui_digitos_3x5.png")
    (out / "ui_digitos_3x5.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "orden": ORDEN, "celda": [3, 5], "paso": 4, "nota": "x = indice * 4; «mm:ss» mide 19 px"}}}, ensure_ascii=False))
    todo["digitos"] = [d]
    p = placa()
    p.img.save(out / "ui_eta_placa.png")
    (out / "ui_eta_placa.json").write_text(json.dumps({"slice": 3, "uso": "placa de 9 px de alto: icono 7x7 a x=2, cifra 3x5 a y=2"}, ensure_ascii=False))
    todo["placa"] = [p]
    for n, fn in (("ui_eta_reloj_7", reloj), ("ui_eta_destino_7", destino)):
        s = fn()
        s.img.save(out / f"{n}.png")
        todo[n] = [s]
    for t in ("carretera", "via", "aire"):
        r, f, m = ruta(t), fin(t), mm_ruta(t)
        r.img.save(out / f"decal_ruta_{t}_16.png")
        f.img.save(out / f"decal_ruta_{t}_fin.png")
        m.img.save(out / f"ui_mm_ruta_{t}.png")
        todo[f"ruta_{t}"] = [r, f, m]
    (out / "decal_ruta.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "texel_m": 0.1, "wrapU": True, "teselas": "decal_ruta_*_16 repite a lo largo (U) cada 1,6 m; la punta *_fin va al final, mirando a +X",
        "minimapa": "ui_mm_ruta_* repite cada 4 px sobre la ruta"}}}, indent=1, ensure_ascii=False))
    s32 = refuerzos()
    s32.img.save(out / "ui_aviso_refuerzos_32.png")
    reduce_16(s32).img.save(out / "ui_aviso_refuerzos_16.png")
    todo["refuerzos"] = [s32, reduce_16(s32)]
    fr = [no_valida(f) for f in range(4)]
    hoja = Image.new("RGBA", (32 * 4, 32), (0, 0, 0, 0))
    for k, s in enumerate(fr):
        hoja.alpha_composite(s.img, (32 * k, 0))
    hoja.save(out / "decal_ruta_no_valida_32.png")
    (out / "decal_ruta_no_valida_32.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "frames": 4, "frameSize": [32, 32], "loop": True, "duration_ms": 125, "texel_m": 0.1,
        "nota": "en el destino pedido; se muestra 1,5 s y se apaga (aviso)"}}}, ensure_ascii=False))
    todo["no_valida"] = fr
    t = tramo_no_valido()
    t.img.save(out / "decal_ruta_no_valida_16.png")
    todo["tramo_no_valido"] = [t]
    return todo


def ejemplo_eta(texto="03:40", icono="reloj"):
    """Placa compuesta como la montaría UI Toolkit (solo para la revisión)."""
    ancho = 2 + 7 + 2 + len(texto) * 4 - 1 + 3
    s = Sprite(ancho, 9)
    p = placa()
    s.rect(0, 0, ancho, 9, "navy")
    for x in range(ancho):
        s.set(x, 0, "bone"); s.set(x, 8, "grey_brown")
    for y in range(9):
        s.set(0, y, "bone"); s.set(ancho - 1, y, "grey_brown")
    for x, y in ((0, 0), (ancho - 1, 0), (0, 8), (ancho - 1, 8)):
        s.set(x, y, None)
    s.stamp(reloj() if icono == "reloj" else destino(), 2, 1)
    for k, ch in enumerate(texto):
        for j, fila in enumerate(DIG[ch]):
            for i, c in enumerate(fila):
                if c == "#":
                    s.set(11 + k * 4 + i, 2 + j, "bone")
    return s


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_rutas")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    # escena de revisión: ruta por carretera, por vía y por aire sobre suelo, con las placas
    E = 3
    W, H = 220, 120
    esc = Image.new("RGBA", (W, H), PAL["olive_grey"])
    for y in range(28, 40):
        for x in range(W):
            esc.putpixel((x, y), PAL["concrete"])
    for y in range(66, 72):
        for x in range(W):
            esc.putpixel((x, y), PAL["grey_brown"])
    for nombre, y in (("carretera", 32), ("via", 67), ("aire", 98)):
        r, f, _ = todo[f"ruta_{nombre}"]
        for x in range(20, 180, 16):
            esc.alpha_composite(r.img, (x, y))
        esc.alpha_composite(f.img, (180, y - 2))
        esc.alpha_composite(ejemplo_eta("03:40" if nombre != "aire" else "01:15").img, (12, y - 13))
        esc.alpha_composite(ejemplo_eta("03:40" if nombre != "aire" else "01:15", "destino").img, (180, y - 13))
    esc.alpha_composite(todo["no_valida"][0].img, (180, 84))
    for x in range(120, 176, 16):
        esc.alpha_composite(todo["tramo_no_valido"][0].img, (x, 112))
    hoja = Image.new("RGBA", (W * E + 20 + 32 * E, H * E), PAL["navy"])
    hoja.alpha_composite(esc.resize((W * E, H * E), Image.NEAREST), (0, 0))
    f = Image.new("RGBA", (32, 32), PAL["slate_dark"])
    hoja.alpha_composite(Image.alpha_composite(f, todo["refuerzos"][0].img).resize((32 * E, 32 * E), Image.NEAREST), (W * E + 10, 0))
    f = Image.new("RGBA", (16, 16), PAL["slate_dark"])
    hoja.alpha_composite(Image.alpha_composite(f, todo["refuerzos"][1].img).resize((16 * E, 16 * E), Image.NEAREST), (W * E + 10, 32 * E + 10))
    hoja.save(OUT / "contacto_rutas_x3.png")
    print("ok")
