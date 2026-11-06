"""Avisos del HUD (arriba a la izquierda) e iconos del minimapa (P1, noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:54). Avisos con el lenguaje de A2 (D-N47): emergencia con el triángulo rojo de peligro #982420,
pictograma oscuro dentro, luz arriba a la izquierda, sin texto. Van dentro de ui_alerta_slot (24x24) a 16 px y en el
cartel grande a 32 px.
  ui_aviso_oleada_entrante_{32,16}  triángulo con tres cabezas de la horda y la cuña de avance
  ui_aviso_peloton_apuros_{32,16}   casco del ET con el triángulo de peligro en la esquina
  ui_aviso_tren_atacado_{32,16}     locomotora con el triángulo de peligro en la esquina
Minimapa (base a 4 m/px del mapa de zonas de Álvaro, sagunto_zonas_1m.png, 189x170):
  ui_minimapa_base_sagunto          zonas en la subpaleta del suelo, apagadas para que manden los iconos
  ui_mm_{peloton,vehiculo,helicoptero,tren,estacion,helipuerto,horda}   iconos de 5 a 9 px
  ui_mm_entrada  (7x7, 4 f) cuña roja de entrada de oleada que late, para O1–O4
  ui_mm_alerta   (11x11, 4 f) anillo rojo que se abre donde salta un aviso
  ui_mm_camara   (8x8, slice 2) rectángulo de lo que ve la cámara
La tropa propia en hueso; la horda en sus valores (#1c200c con filo #303424); el rojo solo para ataque.
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite, reduce_16  # noqa: E402
import build_a2 as a2  # noqa: E402

ZONAS_PNG = Path("/home/claude/mr-mak-workspace/docs/niveles/sagunto_zonas_1m.png")   # repo hermano mr-mak-workspace


def _badge(s, x0, y0):
    """Triángulo de peligro pequeño (11 px) en la esquina, con su «!» en hueso."""
    capa = Sprite(s.w, s.h)
    capa.poly([(x0 + 6, y0), (x0 + 12, y0 + 11), (x0, y0 + 11)], "red_bright")
    capa.vline(x0 + 6, y0 + 4, y0 + 7, "bone")
    capa.set(x0 + 6, y0 + 9, "bone")
    capa.outline()
    s.stamp(capa, 0, 0)


def oleada_entrante():
    """Triángulo de peligro con tres zombis de frente (cabeza, hombros y brazos al frente) en silueta oscura."""
    s = Sprite()
    a2.senal_peligro(s)
    for x, y in ((9, 15), (16, 12), (21, 15)):
        s.rect(x, y, 3, 3, "olive_black")            # cabeza
        s.rect(x - 1, y + 3, 5, 6, "olive_black")    # cuerpo
        s.set(x - 2, y + 4, "olive_black"); s.set(x + 4, y + 4, "olive_black")   # brazos
        s.set(x - 2, y + 3, "olive_black"); s.set(x + 4, y + 3, "olive_black")
    s.hline(7, 25, 26, "olive_shadow")
    s.outline()
    return s


def peloton_apuros():
    """Busto de soldado (casco y hombros) con el triángulo de peligro en la esquina."""
    s = Sprite()
    s.poly([(2, 30), (4, 24), (9, 21), (19, 21), (24, 24), (26, 30)], "olive")     # hombros
    s.hline(3, 25, 30, "olive_dark")
    s.rect(10, 13, 8, 9, "tan")                                                    # cara
    s.vline(17, 13, 21, "rose_brown")
    s.rect(11, 16, 2, 1, "brown_dark"); s.rect(15, 16, 2, 1, "brown_dark")
    s.poly([(7, 14), (8, 9), (11, 6), (17, 6), (20, 9), (21, 14)], "olive")        # casco
    s.hline(7, 21, 14, "olive_grey")
    s.line(9, 10, 11, 8, "olive_light")
    s.set(14, 9, "olive_dark"); s.set(17, 11, "olive_dark"); s.set(11, 12, "olive_dark")
    s.rect(11, 24, 6, 6, "olive_grey")                                             # chaleco
    s.outline()
    _badge(s, 19, 0)
    return s


def tren_atacado():
    s = Sprite()
    s.rect(2, 14, 24, 9, "olive")
    s.hline(2, 25, 14, "olive_light")
    s.rect(18, 10, 8, 5, "olive"); s.hline(18, 25, 10, "olive_light")
    s.rect(20, 11, 4, 3, "slate_dark")
    s.hline(2, 25, 22, "olive_dark")
    for x in range(4, 17, 4):
        s.vline(x, 16, 20, "olive_dark")
    s.rect(1, 23, 27, 2, "grey_brown")
    for x in (4, 10, 18, 24):
        s.ellipse(x - 2, 24, x + 2, 28, "concrete"); s.set(x, 26, "concrete_light")
    s.hline(0, 30, 29, "concrete")
    s.outline()
    _badge(s, 0, 0)
    return s


AVISOS = {"oleada_entrante": oleada_entrante, "peloton_apuros": peloton_apuros, "tren_atacado": tren_atacado}

# --- Minimapa -------------------------------------------------------------------------------
COLOR_ZONA = {0: "olive_grey", 1: "concrete", 2: "grey_brown", 3: "grey_light", 4: "concrete", 5: "olive_dark",
              6: "grey_brown", 7: "concrete_light", 8: "olive_grey", 9: "concrete", 10: "brown", 11: "olive_dark"}


def minimapa_base(paso=4):
    z = Image.open(ZONAS_PNG)
    w, h = z.size
    W, H = w // paso, h // paso
    s = Sprite(W, H)
    src = z.load()
    for y in range(H):
        for x in range(W):
            cuenta = {}
            for j in range(paso):
                for i in range(paso):
                    v = src[x * paso + i, y * paso + j]
                    cuenta[v] = cuenta.get(v, 0) + 1
            for v in (3, 4, 9):                      # vía, calles y acequia: líneas finas que no deben perderse
                if cuenta.get(v, 0) >= 3:
                    zona = v
                    break
            else:
                zona = max(cuenta, key=cuenta.get)
            s.set(x, y, COLOR_ZONA.get(zona, "olive_grey"))
    for y in range(H):                               # borde de 1 px
        s.set(0, y, "grey_brown"); s.set(W - 1, y, "grey_brown")
    for x in range(W):
        s.set(x, 0, "grey_brown"); s.set(x, H - 1, "grey_brown")
    return s


def _mm(grid, keys):
    filas = [f for f in grid.strip().split("\n")]
    s = Sprite(len(filas[0].strip()), len(filas))
    s.grid(0, 0, grid, keys)
    return s


def mm_peloton():
    return _mm("""
.kkk.
kbbbk
kbbbk
kbbbk
.kkk.""", {"k": "black", "b": "bone"})


def mm_vehiculo():
    return _mm("""
...k...
..kbk..
.kbbbk.
kbbbbbk
.kbbbk.
..kbk..
...k...""", {"k": "black", "b": "bone"})


def mm_helicoptero():
    return _mm("""
k.....k
.k...k.
..kbk..
..bbb..
..kbk..
.k...k.
k.....k""", {"k": "bone", "b": "beige"})


def mm_tren():
    return _mm("""
kkkkkkkkkkk
kbbbkbbbkbk
kkkkkkkkkkk""", {"k": "black", "b": "olive_light"})


def mm_estacion():
    return _mm("""
kkkkkkkkk
kbbbbbbbk
kbccbccbk
kbbbbbbbk
kkkkkkkkk""", {"k": "black", "b": "beige", "c": "brown"})


def mm_helipuerto():
    return _mm("""
.kkkkk.
kb.b.bk
kb.b.bk
kbbbbbk
kb.b.bk
kb.b.bk
.kkkkk.""", {"k": "black", "b": "bone", ".": "_"}) if False else _mm("""
_kkkkk_
kbcbcbk
kbcbcbk
kbbbbbk
kbcbcbk
kbcbcbk
_kkkkk_""", {"k": "black", "b": "bone", "c": "grey_brown"})


def mm_horda():
    """Mancha de densidad de la horda: se pinta por celda donde hay zombis (UI la repite)."""
    return _mm("""
.hh.
hHhh
hhhH
.hh.""", {"h": "olive_black", "H": "olive_shadow"})


def mm_entrada(f):
    s = Sprite(7, 7)
    c = "red_bright" if f in (0, 1) else "red"
    dx = (0, 1, 1, 0)[f]
    for y in range(7):
        a = 3 - abs(y - 3)
        for x in range(dx, dx + a + 1):
            s.set(x + 2, y, c)
    s.outline()
    return s


def mm_alerta(f):
    s = Sprite(11, 11)
    r = 1.5 + f * 1.2
    for y in range(11):
        for x in range(11):
            if abs(math.hypot(x - 5, y - 5) - r) < .6:
                s.set(x, y, "red_bright" if f < 3 else "red")
    return s


def mm_camara():
    s = Sprite(8, 8)
    for k in range(8):
        s.set(k, 0, "bone"); s.set(k, 7, "bone"); s.set(0, k, "bone"); s.set(7, k, "bone")
    return s


ICONOS_MM = {"peloton": mm_peloton, "vehiculo": mm_vehiculo, "helicoptero": mm_helicoptero, "tren": mm_tren,
             "estacion": mm_estacion, "helipuerto": mm_helipuerto, "horda": mm_horda, "camara": mm_camara}


def _hoja(out, nombre, frames, dur):
    w, h = frames[0].w, frames[0].h
    im = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
    for f, s in enumerate(frames):
        im.alpha_composite(s.img, (w * f, 0))
    im.save(out / f"{nombre}.png")
    (out / f"{nombre}.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "frames": len(frames), "frameSize": [w, h], "loop": True, "duration_ms": dur}}}, indent=1))


def exportar(out):
    out = Path(out)
    todo = {}
    for n, fn in AVISOS.items():
        s32 = fn()
        s16 = reduce_16(s32)
        s32.img.save(out / f"ui_aviso_{n}_32.png")
        s16.img.save(out / f"ui_aviso_{n}_16.png")
        todo[f"aviso_{n}"] = [s32, s16]
    if ZONAS_PNG.exists():
        b = minimapa_base()
        b.img.save(out / "ui_minimapa_base_sagunto.png")
        (out / "ui_minimapa_base_sagunto.json").write_text(json.dumps({"meta": {"viaMuerta": {
            "m_por_px": 4, "X": [-250, 506], "Z": [-200, 480], "fila0": "Z = 480 (norte)",
            "px": "x = (X + 250) / 4, y = (480 - Z) / 4", "origen": "sagunto_zonas_1m.png (Álvaro, mr-mak-workspace 4d02e00)"}}},
            indent=1, ensure_ascii=False))
        todo["minimapa"] = [b]
    for n, fn in ICONOS_MM.items():
        s = fn()
        s.img.save(out / f"ui_mm_{n}.png")
        todo[f"mm_{n}"] = [s]
    (out / "ui_mm_camara.json").write_text('{"slice": 2}')
    fr = [mm_entrada(f) for f in range(4)]
    _hoja(out, "ui_mm_entrada", fr, 150)
    todo["mm_entrada"] = fr
    fr = [mm_alerta(f) for f in range(4)]
    _hoja(out, "ui_mm_alerta", fr, 120)
    todo["mm_alerta"] = fr
    return todo


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_avisos_minimapa")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 3
    b = todo["minimapa"][0]
    # vista previa: minimapa con iconos colocados a mano
    prev = b.img.copy()
    pos = {"estacion": (95, 99), "tren": (82, 106), "peloton": (88, 100), "vehiculo": (100, 104),
           "helipuerto": (120, 112), "helicoptero": (110, 92)}
    for n, (x, y) in pos.items():
        prev.alpha_composite(todo[f"mm_{n}"][0].img, (x, y))
    for k in range(18):
        prev.alpha_composite(todo["mm_horda"][0].img, (40 + (k % 6) * 4, 96 + (k // 6) * 4))
    prev.alpha_composite(todo["mm_entrada"][0].img, (30, 100))
    prev.alpha_composite(todo["mm_alerta"][2].img, (84, 96))
    cam = Image.new("RGBA", (40, 24), (0, 0, 0, 0))
    for x in range(40):
        cam.putpixel((x, 0), PAL["bone"]); cam.putpixel((x, 23), PAL["bone"])
    for y in range(24):
        cam.putpixel((0, y), PAL["bone"]); cam.putpixel((39, y), PAL["bone"])
    prev.alpha_composite(cam, (78, 88))
    filas = [[s for s in todo[f"aviso_{n}"]] for n in AVISOS]
    hoja = Image.new("RGBA", (b.w * E + 20 + 3 * (32 * E + 8), max(b.h * E, 2 * (32 * E + 8)) + 8), PAL["navy"])
    hoja.alpha_composite(prev.resize((b.w * E, b.h * E), Image.NEAREST), (0, 0))
    x0 = b.w * E + 20
    for i, (s32, s16) in enumerate(filas):
        fondo = Image.new("RGBA", (32, 32), PAL["slate_dark"])
        hoja.alpha_composite(Image.alpha_composite(fondo, s32.img).resize((32 * E, 32 * E), Image.NEAREST), (x0 + i * (32 * E + 8), 0))
        f16 = Image.new("RGBA", (16, 16), PAL["slate_dark"])
        hoja.alpha_composite(Image.alpha_composite(f16, s16.img).resize((16 * E, 16 * E), Image.NEAREST), (x0 + i * (32 * E + 8), 32 * E + 8))
    hoja.save(OUT / "contacto_avisos_minimapa_x3.png")
    print("ok")
