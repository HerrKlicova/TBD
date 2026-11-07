"""Panel de frentes activos e iconos aéreos (P1, noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:59), para cuando hay varios frentes a la vez. Si se publica docs/arte/referencia-ataques-aereos.md,
se repasan los iconos aéreos con él (todavía no existe: SIN VERIFICAR contra foto).
  ui_frente_fila          9-slice de 24x20 (slice 4) para cada fila del panel de arriba a la derecha
  ui_frente_{estacion,tren,base}_16   icono del incidente (pictograma oscuro sobre placa clara, como A2)
  ui_gravedad_{1,2,3}     relleno de la barra de gravedad (32x3): baja en beige, media en ámbar (aviso), alta en rojo
                          de ataque #982420; la 3 late en 2 f (ui_gravedad_3 tiene dos filas)
  ui_boton_ir_{normal,encima}         botón de 16x16 con la mira de «llevar la cámara allí»
  ui_mm_{caza,base_aerea,aerodromo}   iconos del minimapa (7 a 9 px)
  ui_mundo_{caza,base_aerea,aerodromo}_16  iconos sobre el objeto en el mundo (espacio de pantalla)
  ui_aviso_cazas_{32,16}              aviso de información «Cazas despegando» (sin rojo ni ámbar)
"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite, reduce_16  # noqa: E402


def fila():
    s = Sprite(24, 20)
    s.rect(0, 0, 24, 20, "navy")
    for x in range(24):
        s.set(x, 0, "olive_grey"); s.set(x, 19, "grey_brown")
    for y in range(20):
        s.set(0, y, "olive_grey"); s.set(23, y, "grey_brown")
    s.set(1, 1, "bone"); s.set(22, 1, "bone"); s.set(1, 18, "bone"); s.set(22, 18, "bone")
    return s


def _placa16(s):
    s.rect(0, 0, 16, 16, "beige")
    s.hline(0, 15, 0, "bone"); s.vline(0, 0, 15, "bone")
    s.hline(0, 15, 15, "ochre_dark"); s.vline(15, 0, 15, "ochre_dark")


def frente(tipo):
    s = Sprite(16, 16)
    _placa16(s)
    if tipo == "estacion":                           # edificio de viajeros con marquesina
        s.rect(3, 6, 10, 7, "olive_shadow")
        s.poly([(2, 6), (8, 2), (14, 6)], "olive_shadow")
        s.rect(5, 8, 2, 2, "beige"); s.rect(9, 8, 2, 2, "beige")
        s.rect(7, 10, 2, 3, "beige")
    elif tipo == "tren":
        s.rect(2, 6, 11, 5, "olive_shadow"); s.rect(9, 4, 4, 3, "olive_shadow")
        s.rect(10, 5, 2, 1, "beige")
        s.set(4, 12, "olive_shadow"); s.set(7, 12, "olive_shadow"); s.set(11, 12, "olive_shadow")
        s.hline(1, 14, 13, "grey_brown")
    else:                                            # base: muro de sacos con bandera
        for y, x0 in ((11, 2), (9, 3)):
            for x in range(x0, 14 - (x0 - 2), 3):
                s.rect(x, y, 3, 2, "olive_shadow")
        s.vline(8, 2, 9, "olive_shadow")
        s.rect(9, 2, 4, 3, "olive_shadow")
    return s


def gravedad(n, f=0):
    s = Sprite(32, 3)
    c, luz = {1: ("beige", "bone"), 2: ("amber", "cream"), 3: ("red_bright", "rose")}[n]
    if n == 3 and f == 1:
        c, luz = "red", "red_bright"
    largo = {1: 11, 2: 21, 3: 32}[n]
    for x in range(largo):
        s.set(x, 0, luz); s.set(x, 1, c); s.set(x, 2, c)
    return s


def boton_ir(estado):
    s = Sprite(16, 16)
    s.rect(0, 0, 16, 16, "olive_dark" if estado == "normal" else "olive")
    s.hline(0, 15, 0, "olive_light"); s.vline(0, 0, 15, "olive_light")
    s.hline(0, 15, 15, "olive_shadow"); s.vline(15, 0, 15, "olive_shadow")
    for k in range(3):                               # esquinas de mira y punto
        for (x, y, dx, dy) in ((3, 3, 1, 1), (12, 3, -1, 1), (3, 12, 1, -1), (12, 12, -1, -1)):
            s.set(x + dx * k, y, "bone"); s.set(x, y + dy * k, "bone")
    s.rect(7, 7, 2, 2, "bone")
    return s


def _grid(txt, keys):
    filas = txt.strip().split("\n")
    s = Sprite(len(filas[0]), len(filas))
    s.grid(0, 0, txt, keys)
    return s


def mm_caza():
    return _grid("""
...b...
...b...
..bbb..
.bbbbb.
b..b..b
...b...
..bbb..""", {"b": "bone"})


def mm_base_aerea():
    return _grid("""
kkkkkkkkk
kgggggggk
kbbbbbbbk
kgggggggk
kkkkkkkkk""", {"k": "black", "g": "grey_brown", "b": "bone"})


def mm_aerodromo():
    return _grid("""
kkkkkkkkk
kgbgggggk
kggbggggk
kgggbgggk
kggggbggk
kgggggbgk
kkkkkkkkk""", {"k": "black", "g": "grey_brown", "b": "bone"})


def mundo(tipo):
    """Icono de 16x16 con placa oscura y contorno, para flotar sobre el objeto en el mundo."""
    s = Sprite(16, 16)
    s.rect(1, 1, 14, 14, "navy")
    for k in range(1, 15):
        s.set(k, 1, "bone"); s.set(k, 14, "bone"); s.set(1, k, "bone"); s.set(14, k, "bone")
    m = {"caza": mm_caza, "base_aerea": mm_base_aerea, "aerodromo": mm_aerodromo}[tipo]()
    s.stamp(m, 8 - m.w // 2, 8 - m.h // 2)
    s.outline()
    return s


def aviso_cazas():
    """Caza de perfil que sube en diagonal desde una pista, con la estela; información, sin rojo ni ámbar."""
    s = Sprite()
    s.hline(1, 30, 28, "grey_brown"); s.hline(1, 30, 29, "concrete")             # pista
    for x in range(3, 29, 6):
        s.hline(x, x + 2, 28, "bone")
    for k in range(12):                                                         # estela
        if k % 3 != 2:
            s.set(3 + k, 24 - k // 2, "concrete_light")
    s.poly([(12, 18), (26, 8), (30, 7), (28, 10), (14, 21)], "concrete_light")   # fuselaje
    s.line(13, 18, 27, 8, "bone")
    s.poly([(18, 15), (22, 9), (24, 10), (21, 17)], "concrete")                  # ala
    s.poly([(12, 18), (12, 13), (15, 16)], "concrete")                           # deriva
    s.set(27, 8, "slate_dark"); s.set(26, 9, "slate_dark")                       # cabina
    s.outline()
    return s


def exportar(out):
    out = Path(out)
    todo = {}
    f = fila()
    f.img.save(out / "ui_frente_fila.png")
    (out / "ui_frente_fila.json").write_text('{"slice": 4, "uso": "icono 16 a x=2, nombre, barra de 32x3 y botón 16 a la derecha"}')
    todo["fila"] = [f]
    for t in ("estacion", "tren", "base"):
        s = frente(t)
        s.img.save(out / f"ui_frente_{t}_16.png")
        todo[f"frente_{t}"] = [s]
    for n in (1, 2, 3):
        if n == 3:
            hoja = Image.new("RGBA", (32, 6), (0, 0, 0, 0))
            hoja.alpha_composite(gravedad(3, 0).img, (0, 0)); hoja.alpha_composite(gravedad(3, 1).img, (0, 3))
            hoja.save(out / "ui_gravedad_3.png")
            todo["grav_3"] = [gravedad(3, 0), gravedad(3, 1)]
        else:
            s = gravedad(n)
            s.img.save(out / f"ui_gravedad_{n}.png")
            todo[f"grav_{n}"] = [s]
    for e in ("normal", "encima"):
        s = boton_ir(e)
        s.img.save(out / f"ui_boton_ir_{e}.png")
        todo[f"ir_{e}"] = [s]
    for n, fn in (("caza", mm_caza), ("base_aerea", mm_base_aerea), ("aerodromo", mm_aerodromo)):
        s = fn()
        s.img.save(out / f"ui_mm_{n}.png")
        m = mundo(n)
        m.img.save(out / f"ui_mundo_{n}_16.png")
        todo[f"aire_{n}"] = [s, m]
    a = aviso_cazas()
    a.img.save(out / "ui_aviso_cazas_32.png")
    reduce_16(a).img.save(out / "ui_aviso_cazas_16.png")
    todo["aviso_cazas"] = [a, reduce_16(a)]
    return todo


def maqueta_panel(todo):
    """Panel de frentes como lo montaría UI Toolkit: tres filas de 150x20 (sin texto real: barra gris de nombre)."""
    W = 150
    im = Image.new("RGBA", (W + 8, 3 * 22 + 8), PAL["olive_grey"])
    for i, (t, g) in enumerate((("tren", 3), ("estacion", 2), ("base", 1))):
        y = 4 + i * 22
        base = Image.new("RGBA", (W, 20), PAL["navy"])
        im.alpha_composite(base, (4, y))
        for x in range(W):
            im.putpixel((4 + x, y), PAL["olive_grey"]); im.putpixel((4 + x, y + 19), PAL["grey_brown"])
        im.alpha_composite(todo[f"frente_{t}"][0].img, (6, y + 2))
        for x in range(26, 70):                      # hueco del nombre
            im.putpixel((4 + x, y + 6), PAL["bone"]) if x % 5 < 4 else None
        im.alpha_composite(todo[f"grav_{g}"][0].img, (4 + 26, y + 12))
        im.alpha_composite(todo["ir_encima" if i == 0 else "ir_normal"][0].img, (4 + W - 18, y + 2))
    return im


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_frentes_aire")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 4
    p = maqueta_panel(todo)
    sueltos = [s for k, v in todo.items() if k.startswith(("aire_", "aviso_")) for s in v]
    Wt = max(p.width * E, sum(s.w * E + 8 for s in sueltos))
    hoja = Image.new("RGBA", (Wt, p.height * E + 8 + 32 * E), PAL["navy"])
    hoja.alpha_composite(p.resize((p.width * E, p.height * E), Image.NEAREST), (0, 0))
    x = 0
    for s in sueltos:
        f = Image.new("RGBA", (s.w, s.h), PAL["slate_dark"])
        hoja.alpha_composite(Image.alpha_composite(f, s.img).resize((s.w * E, s.h * E), Image.NEAREST), (x, p.height * E + 8))
        x += s.w * E + 8
    hoja.save(OUT / "contacto_frentes_aire_x4.png")
    print("ok")
