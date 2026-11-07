"""Marcadores de juego en el mundo y cursores que faltaban (P1, noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:51). Ya existían (build_interfaz, D-N78): corchetes de selección en pantalla (ui_seleccion),
marcadores de orden en pantalla de 16 px, cursores normal, mover, atacar y construir, y el indicador de entrada de
2 f. Aquí va lo que faltaba, en el suelo (decal, texel 0,1 m, vista cenital, alfa 0/255) o en pantalla:
  decal_sel_peloton_96    anillo de selección del pelotón (9,6 m), 4 f en bucle: los trazos giran despacio
  decal_sel_vehiculo_128  anillo doble del vehículo (12,8 m) con muesca de proa hacia +X (el motor lo gira)
  decal_sel_encima_96     anillo punteado de pasar el ratón por encima (sin seleccionar)
  decal_orden_mover_32    4 f, una vez: cuatro cuñas que se cierran sobre el destino (hueso)
  decal_orden_atacar_32   4 f, una vez: lo mismo con aspa, en el rojo de ataque #982420
  decal_orden_mantener_32 4 f, una vez: cuadro con escudo, en hueso
  decal_cono_emplazar_256 cono de 60° (25,6 m de radio) con el filo en hueso y el relleno tramado al 1/8
  ui_arrastre             rectángulo de arrastre de 16x16, slice 4: trazos de hueso de 3 px y relleno tramado
  ui_aviso_entrada        32x24, 4 f en bucle: el pulso de la cuña de entrada (sustituye al latido de 2 f)
  ui_aviso_entrada_o1..o4 la placa con «O1»…«O4» dibujada a mano, para no depender de la fuente
  ui_cursor_{seleccionar,mantener,emplazar,embarcar,no_valido}  16x16 con su punto caliente en el JSON
El rojo #982420 solo en atacar y en la entrada de la oleada (ataque); nada de ámbar.
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import BAYER  # noqa: E402


def _anillo(t, r0, r1, c, trazo=None, fase=0.0, sombra=True):
    """Anillo de radios r0–r1. trazo = (n, hueco): n trazos con un hueco (fracción) entre ellos."""
    s = Sprite(t, t)
    cx = cy = t / 2
    for y in range(t):
        for x in range(t):
            d = math.hypot(x + .5 - cx, y + .5 - cy)
            if not r0 <= d <= r1:
                continue
            if trazo:
                n, hueco = trazo
                a = (math.atan2(y + .5 - cy, x + .5 - cx) / math.tau + fase) % 1 * n
                if a % 1 < hueco:
                    continue
            s.set(x, y, c)
    if sombra:                                       # sombra de 1 px hacia abajo a la derecha para que se lea en claro
        src = s.img.copy().load()
        for y in range(t - 1):
            for x in range(t - 1):
                if src[x, y][3] and not src[x + 1, y + 1][3]:
                    s.set(x + 1, y + 1, "olive_shadow")
    return s


def sel_peloton(f):
    return _anillo(96, 44, 46, "bone", trazo=(12, .22), fase=f / 48)


def sel_encima(f=0):
    return _anillo(96, 45, 46, "grey_light", trazo=(36, .5), sombra=False)


def sel_vehiculo(f=0):
    s = _anillo(128, 60, 62, "bone")
    s.stamp(_anillo(128, 55.5, 56.6, "beige", trazo=(24, .4), sombra=False), 0, 0)
    for k in range(5):                               # muesca de proa (+X)
        s.set(124 - k, 64 - k // 2 - 1, "bone"); s.set(124 - k, 64 + k // 2, "bone")
    return s


def _cunas(f, c):
    s = Sprite(32, 32)
    d = 13 - f * 2.5
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = int(15.5 + sx * d + (.5 if sx > 0 else -.5)), int(15.5 + sy * d + (.5 if sy > 0 else -.5))
            for k in range(5):
                s.set(x - sx * k, y, c); s.set(x, y - sy * k, c)
                s.set(x - sx * k, y - sy, c); s.set(x - sx, y - sy * k, c)
    return s


def orden_mover(f):
    s = _cunas(f, "bone")
    s.rect(15, 15, 2, 2, "bone")
    s.outline("olive_shadow")
    return s


def orden_atacar(f):
    s = _cunas(f, "red_bright")
    for k in range(-3, 4):
        for d in (0, 1):
            s.set(15 + k + d, 15 + k, "red_bright"); s.set(15 + k + d, 16 - k, "red_bright")
    s.outline("olive_shadow")
    return s


def orden_mantener(f):
    s = Sprite(32, 32)
    m = 3 + (3 - f)                                  # el cuadro se asienta
    for k in range(m, 32 - m):
        for d in (0, 1):
            if (k // 3) % 2 == 0:
                s.set(k, m + d, "bone"); s.set(k, 31 - m - d, "bone"); s.set(m + d, k, "bone"); s.set(31 - m - d, k, "bone")
    s.poly([(12, 11), (20, 11), (20, 17), (16, 21), (12, 17)], "bone")   # escudo
    s.poly([(14, 13), (18, 13), (18, 16), (16, 18), (14, 16)], "beige")
    s.outline("olive_shadow")
    return s


def cono_emplazar(r=256, grados=60):
    """Vértice en (0, r/2) mirando a +X. Filo de 2 px en hueso, arco punteado y relleno tramado (1 de cada 8)."""
    s = Sprite(r, r)
    cy = r / 2
    med = math.radians(grados / 2)
    for y in range(r):
        for x in range(r):
            dx, dy = x + .5, y + .5 - cy
            d = math.hypot(dx, dy)
            a = abs(math.atan2(dy, dx))
            if d > r - 1 or a > med:
                continue
            borde_lado = (med - a) * d < 2
            borde_arco = d > r - 3
            if borde_lado:
                s.set(x, y, "bone")
            elif borde_arco:
                if int(math.degrees(math.atan2(dy, dx)) * 2) % 4 < 2:
                    s.set(x, y, "bone")
            elif BAYER[y % 4][x % 4] == 0 and (x // 4 + y // 4) % 2 == 0:
                s.set(x, y, "beige")
    return s


def arrastre():
    s = Sprite(16, 16)
    for k in range(16):
        if (k // 3) % 2 == 0:
            s.set(k, 0, "bone"); s.set(k, 15, "bone"); s.set(0, k, "bone"); s.set(15, k, "bone")
    for y in range(1, 15):
        for x in range(1, 15):
            if BAYER[y % 4][x % 4] == 0 and (x + y) % 8 == 0:
                s.set(x, y, "grey_light")
    return s


def aviso_entrada(f):
    """La cuña de entrada con un pulso de 4 f: avanza, se aclara y suelta una onda hacia delante."""
    s = Sprite(32, 24)
    dx = (0, 1, 2, 1)[f]
    c = ("red_bright", "red_bright", "red", "red")[f]
    for y in range(3, 21):
        a = 9 - abs(y - 11.5)
        for x in range(19 + dx, int(19 + dx + a)):
            s.set(x, y, c)
    for y in range(3, 12):
        s.set(int(19 + dx + 9 - abs(y - 11.5)) - 1, y, "bone")
    if f in (1, 2):                                  # onda
        for y in range(5, 19):
            s.set(int(29 + dx + f - abs(y - 11.5) * .6), y, "red_bright" if f == 1 else "red")
    s.rect(1, 6, 17, 12, "navy")
    for x0, y0, w, h in ((1, 6, 17, 1), (1, 17, 17, 1), (1, 6, 1, 12), (17, 6, 1, 12)):
        s.rect(x0, y0, w, h, c)
    s.outline()
    return s


DIG = {"O": ["###", "#.#", "#.#", "#.#", "###"], "1": [".#.", "##.", ".#.", ".#.", "###"],
       "2": ["###", "..#", "###", "#..", "###"], "3": ["###", "..#", ".##", "..#", "###"],
       "4": ["#.#", "#.#", "###", "..#", "..#"]}


def aviso_placa(n):
    """Placa de 15x10 con «On» en hueso, para ir dentro de la cuña (en x=2, y=7)."""
    s = Sprite(15, 10)
    for k, ch in enumerate(("O", str(n))):
        for j, fila in enumerate(DIG[ch]):
            for i, c in enumerate(fila):
                if c == "#":
                    s.set(3 + k * 5 + i, 2 + j, "bone")
    return s


def cursor(tipo):
    s = Sprite(16, 16)
    if tipo == "seleccionar":                        # flecha con un recuadro punteado
        for y in range(9):
            s.hline(1, 1 + y * 7 // 10, 1 + y, "bone")
        for k in range(6, 16):
            if k % 2 == 0:
                s.set(k, 6, "bone"); s.set(k, 15, "bone"); s.set(6, k, "bone"); s.set(15, k, "bone")
    elif tipo == "mantener":                         # escudo
        s.poly([(3, 2), (12, 2), (12, 9), (7, 14), (3, 9)], "bone")
        s.poly([(5, 4), (10, 4), (10, 8), (7, 11), (5, 8)], "beige")
    elif tipo == "emplazar":                         # cono pequeño con el vértice abajo
        s.line(7, 14, 1, 3, "bone"); s.line(8, 14, 14, 3, "bone")
        for x in range(2, 14, 2):
            s.set(x, 2, "bone")
        s.rect(6, 12, 4, 3, "bone")
    elif tipo == "embarcar":                         # flecha que entra en una caja
        s.rect(6, 7, 9, 7, "grey_light"); s.rect(7, 8, 7, 5, None)
        s.rect(7, 8, 7, 5, "olive_shadow")              # D-N92: 303424, no 283440
        s.line(0, 3, 9, 10, "bone"); s.line(1, 3, 10, 10, "bone")
        s.rect(8, 9, 3, 3, "bone")
    else:                                            # no válido: círculo con barra, en hueso (no es ataque)
        for y in range(16):
            for x in range(16):
                d = math.hypot(x - 7.5, y - 7.5)
                if 5 <= d <= 7 or (d < 6 and abs((x - 7.5) - (y - 7.5)) < 1.3):
                    s.set(x, y, "bone")
    s.outline()
    return s


CALIENTE = {"seleccionar": [1, 1], "mantener": [7, 8], "emplazar": [7, 14], "embarcar": [9, 10], "no_valido": [8, 8]}

ANIMADOS = {   # nombre: (función, fotogramas, tamaño, bucle, pivote)
    "decal_sel_peloton_96": (sel_peloton, 4, 96, True, (0.5, 0.5)),
    "decal_orden_mover_32": (orden_mover, 4, 32, False, (0.5, 0.5)),
    "decal_orden_atacar_32": (orden_atacar, 4, 32, False, (0.5, 0.5)),
    "decal_orden_mantener_32": (orden_mantener, 4, 32, False, (0.5, 0.5)),
}


def _hoja(out, nombre, frames, w, h, bucle, piv, dur=125):
    hoja = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
    for f, s in enumerate(frames):
        hoja.alpha_composite(s.img, (w * f, 0))
    hoja.save(out / f"{nombre}.png")
    meta = {"frames": [{"filename": f"{nombre} {f}", "frame": {"x": w * f, "y": 0, "w": w, "h": h}, "duration": dur}
                       for f in range(len(frames))],
            "meta": {"image": f"{nombre}.png", "viaMuerta": {"loop": bucle, "frames": len(frames), "frameSize": [w, h],
                                                             "pivot": list(piv), "texel_m": 0.1,
                                                             "origen": "pixelvm, build_marcadores.py"}}}
    (out / f"{nombre}.json").write_text(json.dumps(meta, indent=1))


def exportar(out):
    out = Path(out)
    todo = {}
    for n, (fn, k, t, bucle, piv) in ANIMADOS.items():
        fr = [fn(f) for f in range(k)]
        _hoja(out, n, fr, t, t, bucle, piv, dur=250 if bucle else 83)
        todo[n] = fr
    fijos = {"decal_sel_vehiculo_128": sel_vehiculo(), "decal_sel_encima_96": sel_encima(),
             "decal_cono_emplazar_256": cono_emplazar()}
    for n, s in fijos.items():
        s.img.save(out / f"{n}.png")
        todo[n] = [s]
    (out / "decal_cono_emplazar_256.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "pivot": [0.0, 0.5], "texel_m": 0.1, "radio_m": 25.6, "grados": 60,
        "nota": "vértice en el pelotón, mirando a +X local; si el alcance real es otro, se regenera con cono_emplazar(r)"}}},
        indent=1, ensure_ascii=False))
    a = arrastre()
    a.img.save(out / "ui_arrastre.png")
    (out / "ui_arrastre.json").write_text('{"slice": 4}')
    todo["ui_arrastre"] = [a]
    fr = [aviso_entrada(f) for f in range(4)]
    _hoja(out, "ui_aviso_entrada", fr, 32, 24, True, (0.5, 0.5), dur=150)
    todo["ui_aviso_entrada"] = fr
    for n in range(1, 5):
        s = aviso_placa(n)
        s.img.save(out / f"ui_aviso_entrada_o{n}.png")
        todo[f"placa_o{n}"] = [s]
    meta = {}
    for c in CALIENTE:
        s = cursor(c)
        s.img.save(out / f"ui_cursor_{c}.png")
        meta[f"ui_cursor_{c}"] = {"hotspot": CALIENTE[c]}
        todo[f"cursor_{c}"] = [s]
    (out / "ui_cursores_nuevos.json").write_text(json.dumps(meta, indent=1))
    return todo


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_marcadores")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 2
    filas = list(todo.items())
    anchos = [sum(s.w * E + 6 for s in fr) for _, fr in filas]
    # en filas de hasta ~1400 px
    lineas, actual, ancho = [], [], 0
    for (n, fr), w in zip(filas, anchos):
        if ancho + w > 1400 and actual:
            lineas.append(actual); actual, ancho = [], 0
        actual.append(fr); ancho += w
    lineas.append(actual)
    Ht = sum(max(fr[0].h for fr in l) * E + 8 for l in lineas)
    hoja = Image.new("RGBA", (1400, Ht), PAL["navy"])
    y = 0
    for l in lineas:
        x = 0
        for fr in l:
            for s in fr:
                fondo = Image.new("RGBA", (s.w, s.h), PAL["olive_dark"])
                hoja.paste(Image.alpha_composite(fondo, s.img).resize((s.w * E, s.h * E), Image.NEAREST), (x, y))
                x += s.w * E + 6
        y += max(fr[0].h for fr in l) * E + 8
    hoja.save(OUT / "contacto_marcadores_x2.png")
    print("ok")
