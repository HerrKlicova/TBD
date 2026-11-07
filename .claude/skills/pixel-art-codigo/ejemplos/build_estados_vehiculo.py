"""Estados de vehículo y aviso del tren (P1, noche del 7-nov). Bruno (Arte técnico).

Encargo de Mak (00:01).
  ui_estado_veh_{embarcando,desembarcando,danado,en_llamas,sin_municion,reabasteciendo}_16
        sobre el vehículo en el mundo y en la esquina de su tarjeta. En hueso con contorno negro, como los del
        pelotón (D-N89); «sin munición» es la A2 reducida (ámbar de aviso) y «reabasteciendo» el del pelotón.
        «En llamas» va sin ámbar (se queda en pantalla): beige, ocre y óxido, como el fuego de D-N87.
  (el marcador del helipuerto se movió a build_apoyo_aereo.py, con tres estados: despegue, regreso y rearme)
  ui_aviso_tren_marcha_{32,16}     información: la locomotora con líneas de velocidad (sin rojo ni ámbar)
  ui_aviso_tren_detenido_{32,16}   el de A2 (disco ámbar de alto), exportado aquí por fin con este nombre
  ui_mm_tren_marcha / _detenido / _detenido_anomalo   el tren del minimapa: en marcha con estela; detenido en el andén
        apagado y sin ámbar; detenido anómalo (vía cortada, sin combustible o fuera de estación) con punto ámbar (D-N98).
        ui_aviso_tren_detenido solo se muestra en la parada anómala.
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite, reduce_16  # noqa: E402


def _veh(s, x0=1, y0=7, c="bone"):
    """Vehículo pequeño de perfil (12x6) a la derecha."""
    s.rect(x0, y0 + 1, 12, 3, c)
    s.rect(x0 + 3, y0 - 1, 6, 2, c)
    s.set(x0 + 2, y0 + 5, c); s.set(x0 + 3, y0 + 5, c); s.set(x0 + 9, y0 + 5, c); s.set(x0 + 10, y0 + 5, c)


def estado(tipo):
    if tipo == "sin_municion":
        import build_a2 as a2
        return reduce_16(a2.sin_municion())
    if tipo == "reabasteciendo":
        import build_como_jugar as cj
        return cj.estado("reabasteciendo")
    s = Sprite(16, 16)
    if tipo in ("embarcando", "desembarcando"):
        _veh(s, 1, 10)
        s.rect(1, 11, 14, 3, "bone"); s.rect(4, 9, 7, 2, "bone")
        s.set(3, 15, "bone"); s.set(4, 15, "bone"); s.set(11, 15, "bone"); s.set(12, 15, "bone")
        if tipo == "embarcando":                    # flecha gruesa que baja hacia la escotilla
            s.rect(6, 0, 3, 4, "beige")
            s.poly([(3, 4), (11, 4), (7, 8)], "beige")
        else:                                       # flecha gruesa que sale hacia arriba
            s.rect(6, 4, 3, 4, "beige")
            s.poly([(3, 4), (11, 4), (7, 0)], "beige")
    elif tipo == "danado":                          # vehículo con bocanada de humo y grieta
        _veh(s, 2, 9)
        s.line(7, 10, 9, 12, "grey_brown")
        for (x, y, r) in ((10, 5, 2.5), (6, 3, 2), (12, 2, 1.5)):
            for yy in range(16):
                for xx in range(16):
                    if math.hypot(xx - x, yy - y) <= r:
                        s.set(xx, yy, "concrete_light")
    else:                                           # en llamas: vehículo con llama encima, sin ámbar
        _veh(s, 2, 10)
        s.grid(4, 1, """
...c....
..cbc.c.
.cbbbcbc
cbbcbbbc
cbcccbbc
.cccccc.
""", {"c": "rust", "b": "ochre_dark", ".": "_"})
        s.set(7, 4, "beige"); s.set(8, 5, "beige"); s.set(7, 5, "cream")
    s.outline()
    return s


def helipuerto(f, despegue=False):
    s = Sprite(96, 96)
    t = f / 3
    d = (40 - t * 18) if not despegue else (22 + t * 18)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = 48 + sx * d * .7, 48 + sy * d * .7
            for k in range(7):                       # cuña en L que apunta al centro (o hacia fuera)
                for g in (0, 1):
                    s.set(int(cx - sx * k), int(cy + sy * g), "bone")
                    s.set(int(cx + sx * g), int(cy - sy * k), "bone")
    for y in range(96):                              # aro punteado de la zona
        for x in range(96):
            dd = math.hypot(x + .5 - 48, y + .5 - 48)
            if 44 <= dd <= 45.5 and int(math.degrees(math.atan2(y - 48, x - 48)) / 6) % 2 == f % 2:
                s.set(x, y, "beige")
    s.outline("olive_shadow")
    return s


def tren_marcha():
    s = Sprite()
    s.rect(8, 13, 20, 9, "olive"); s.hline(8, 27, 13, "olive_light")
    s.rect(21, 9, 7, 5, "olive"); s.hline(21, 27, 9, "olive_light")
    s.rect(23, 10, 4, 3, "slate_dark")
    s.hline(8, 27, 21, "olive_dark")
    for x in range(10, 20, 4):
        s.vline(x, 15, 19, "olive_dark")
    s.rect(7, 22, 22, 2, "grey_brown")
    for x in (10, 16, 22, 26):
        s.ellipse(x - 2, 23, x + 2, 27, "concrete"); s.set(x, 25, "concrete_light")
    s.hline(0, 31, 28, "concrete")
    for y, x0, x1 in ((12, 1, 6), (16, 0, 5), (20, 2, 6)):    # líneas de velocidad
        s.hline(x0, x1, y, "bone")
    s.outline()
    return s


def mm_tren(estado_):
    s = Sprite(15, 3)
    if estado_ == "marcha":
        for x in range(4, 15):
            s.set(x, 0, "black"); s.set(x, 2, "black")
        s.set(4, 1, "black"); s.set(14, 1, "black")
        for x in range(5, 14):
            s.set(x, 1, "olive_light" if (x - 5) % 4 != 3 else "black")
        s.set(0, 1, "bone"); s.set(2, 1, "bone")    # estela
    else:
        for x in range(2, 13):
            s.set(x, 0, "black"); s.set(x, 2, "black")
        s.set(2, 1, "black"); s.set(12, 1, "black")
        for x in range(3, 12):
            s.set(x, 1, "grey_brown" if (x - 3) % 4 != 3 else "black")
        if estado_ == "detenido_anomalo":           # D-N98: ámbar solo si la parada es anómala
            s.set(13, 1, "amber"); s.set(14, 1, "amber")
    return s


TIPOS = ["embarcando", "desembarcando", "danado", "en_llamas", "sin_municion", "reabasteciendo"]


def exportar(out):
    out = Path(out)
    todo = {}
    for t in TIPOS:
        s = estado(t)
        s.img.save(out / f"ui_estado_veh_{t}_16.png")
        todo[f"est_{t}"] = [s]
    # el marcador del helipuerto (despegue, regreso y rearme) está en build_apoyo_aereo.py
    m = tren_marcha()
    m.img.save(out / "ui_aviso_tren_marcha_32.png")
    reduce_16(m).img.save(out / "ui_aviso_tren_marcha_16.png")
    import build_a2 as a2
    d = a2.tren_detenido()
    d.img.save(out / "ui_aviso_tren_detenido_32.png")
    reduce_16(d).img.save(out / "ui_aviso_tren_detenido_16.png")
    todo["aviso_tren"] = [m, reduce_16(m), d, reduce_16(d)]
    for e in ("marcha", "detenido", "detenido_anomalo"):
        s = mm_tren(e)
        s.img.save(out / f"ui_mm_tren_{e}.png")
        todo[f"mm_tren_{e}"] = [s]
    return todo


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_estados_vehiculo")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 4
    filas = [[todo[f"est_{t}"][0] for t in TIPOS] + todo["mm_tren_marcha"] + todo["mm_tren_detenido"] + todo["mm_tren_detenido_anomalo"],
             todo["aviso_tren"]]
    Wt = max(sum(s.w * E // (2 if s.w == 96 else 1) + 8 for s in f) for f in filas)
    Ht = sum(max(s.h * E // (2 if s.w == 96 else 1) for s in f) + 8 for f in filas)
    hoja = Image.new("RGBA", (Wt, Ht), PAL["navy"])
    y = 0
    for f in filas:
        x = 0
        h = 0
        for s in f:
            k = E // 2 if s.w == 96 else E
            fondo = Image.new("RGBA", (s.w, s.h), PAL["olive_grey"] if s.w == 96 else PAL["slate_dark"])
            hoja.alpha_composite(Image.alpha_composite(fondo, s.img).resize((s.w * k, s.h * k), Image.NEAREST), (x, y))
            x += s.w * k + 8
            h = max(h, s.h * k)
        y += h + 8
    hoja.save(OUT / "contacto_estados_vehiculo_x4.png")
    print("ok")
