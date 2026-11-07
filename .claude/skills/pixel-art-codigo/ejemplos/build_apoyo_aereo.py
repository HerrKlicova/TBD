"""Helicópteros a la vista y efectos del apoyo aéreo (P1, noche del 7-nov). Bruno (Arte técnico).

Encargo de Mak (00:03), sacado de docs/arte/referencia-ataques-aereos.md (§3.C, §3.D y §3.E):
  decal_sombra_heli_{nh90,tigre}_{bajo,medio,alto}   sombra vertical del aparato (texel 0.2 m, morro a +X); cuanto
        más alto, más clara y con el disco del rotor más tenue. olive_shadow tramado (como el hollín de D-N87).
  ui_altura_linea_3x8   trozo de la línea discontinua de altura (se repite en Y) del aparato a su sombra
  ui_altura_suelo       la marca del pie de la línea en el suelo
  ui_letras_3x5         letras para la etiqueta corta del aparato («NH-1»), como ui_digitos_3x5 (paso 4)
  decal_helipuerto_{despegue,regreso,rearme}_96   tres estados que se distinguen sin color:
        despegue  cuñas que se abren y flechas hacia fuera (bucle)
        regreso   cuñas que se cierran y flechas hacia dentro (bucle; sustituye a «aterrizaje»)
        rearme    cuñas quietas y el aro como barra de progreso (8 f, NO bucle: fotograma = progreso x 8)
  vm_vfx_humo_bombardeo_{denso,medio,disipando}   E1: humo de 8–12 s, tramado y nunca negro (no tapa la horda)
  vm_vfx_fuego_suelo / vm_vfx_rescoldo_suelo       E3: fuego en el suelo como negación de zona, sin ámbar
  decal_suelo_quemado_30                           lo que queda en la celda (3.0 m = 6 celdas de 0.5 m)
  decal_crater_{1,2}_64                            E2: cráter persistente con escombros
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import _h, ruido, tramado  # noqa: E402

FPS = 12


# --- Sombra y altura --------------------------------------------------------------------------
# Planta (vista desde arriba), texel 0.2 m, buje del rotor en el centro. Medidas públicas aproximadas:
# NH90 19.6 m de largo con rotores, rotor de 16.3 m; Tigre 15.8 m, rotor de 13 m.
HELIS = {
    "nh90": {"lado": 96, "rotor": 40.5, "cuerpo": [(-14, -9), (16, -9), (26, -7), (31, -3), (31, 3), (26, 7),
                                                    (16, 9), (-14, 9), (-18, 4), (-18, -4)],
             "cola": (-46, -18, 2), "estab": (-42, 7), "patines": None},
    "tigre": {"lado": 80, "rotor": 32.5, "cuerpo": [(-10, -3), (14, -3), (24, -2), (27, 0), (24, 2), (14, 3),
                                                     (-10, 3)],
              "cola": (-38, -10, 1), "estab": (-34, 6), "alas": (-4, 4, 11)},
}
ALTURAS = {"bajo": (.9, .32), "medio": (.62, .2), "alto": (.4, .1)}


def sombra(tipo, altura):
    d = HELIS[tipo]
    n = d["lado"]
    c = n // 2
    s = Sprite(n, n)
    dc, dr = ALTURAS[altura]
    m = Sprite(n, n)                                 # máscara del fuselaje
    m.poly([(c + x, c + y) for x, y in d["cuerpo"]], "bone")
    x0, x1, w = d["cola"]
    m.rect(c + x0, c - w, x1 - x0, 2 * w + 1, "bone")
    xe, he = d["estab"]
    m.rect(c + xe, c - he, 3, 2 * he + 1, "bone")
    if "alas" in d:
        xa, wa, la = d["alas"]
        m.rect(c + xa, c - la, wa, 2 * la + 1, "bone")
    for y in range(n):
        for x in range(n):
            r = math.hypot(x + .5 - c, y + .5 - c)
            if m.get(x, y)[3]:
                if tramado(x, y, dc):
                    s.set(x, y, "olive_shadow")
            elif r <= d["rotor"]:
                # disco del rotor girando: más denso hacia la punta de las palas
                k = dr * (.55 + .45 * min(1, r / d["rotor"])) * (1.25 if r > d["rotor"] - 2 else 1)
                if tramado(x, y, k):
                    s.set(x, y, "olive_shadow")
    return s


def linea_altura():
    s = Sprite(3, 8)
    for y in range(1, 5):
        s.set(0, y, "black"); s.set(1, y, "bone"); s.set(2, y, "black")
    s.set(1, 0, "black"); s.set(1, 5, "black")
    return s


def marca_suelo():
    """Pie de la línea: anillo aplastado (el suelo se ve a 30–35°) con punto central."""
    s = Sprite(13, 7)
    for y in range(7):
        for x in range(13):
            e = math.hypot((x + .5 - 6.5) / 5, (y + .5 - 3.5) / 2.4)
            if .62 <= e <= 1.0:
                s.set(x, y, "bone")
    s.set(6, 3, "bone")
    s.outline()
    return s


LET = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"], "B": ["##.", "#.#", "##.", "#.#", "##."],
    "C": [".##", "#..", "#..", "#..", ".##"], "D": ["##.", "#.#", "#.#", "#.#", "##."],
    "E": ["###", "#..", "##.", "#..", "###"], "F": ["###", "#..", "##.", "#..", "#.."],
    "G": [".##", "#..", "#.#", "#.#", ".##"], "H": ["#.#", "#.#", "###", "#.#", "#.#"],
    "I": ["###", ".#.", ".#.", ".#.", "###"], "J": ["..#", "..#", "..#", "#.#", ".#."],
    "K": ["#.#", "#.#", "##.", "#.#", "#.#"], "L": ["#..", "#..", "#..", "#..", "###"],
    "M": ["#.#", "###", "###", "#.#", "#.#"], "N": ["##.", "#.#", "#.#", "#.#", "#.#"],
    "O": [".#.", "#.#", "#.#", "#.#", ".#."], "P": ["##.", "#.#", "##.", "#..", "#.."],
    "Q": [".#.", "#.#", "#.#", "##.", ".##"], "R": ["##.", "#.#", "##.", "#.#", "#.#"],
    "S": [".##", "#..", ".#.", "..#", "##."], "T": ["###", ".#.", ".#.", ".#.", ".#."],
    "U": ["#.#", "#.#", "#.#", "#.#", "###"], "V": ["#.#", "#.#", "#.#", "#.#", ".#."],
    "W": ["#.#", "#.#", "###", "###", "#.#"], "X": ["#.#", "#.#", ".#.", "#.#", "#.#"],
    "Y": ["#.#", "#.#", ".#.", ".#.", ".#."], "Z": ["###", "..#", ".#.", "#..", "###"],
    "-": ["...", "...", "###", "...", "..."],
}
ORDEN_LETRAS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ-"


def letras():
    s = Sprite(4 * len(ORDEN_LETRAS), 5)
    for k, ch in enumerate(ORDEN_LETRAS):
        for j, fila in enumerate(LET[ch]):
            for i, ch2 in enumerate(fila):
                if ch2 == "#":
                    s.set(k * 4 + i, j, "bone")
    return s


def etiqueta(texto):
    """Muestra de la etiqueta montada (placa de ruta + letras y dígitos), solo para la hoja de contactos."""
    import build_rutas as r
    w = 4 * len(texto) + 3
    s = Sprite(w, 9)
    s.rect(0, 0, w, 9, "navy")
    s.hline(1, w - 2, 0, "bone"); s.vline(0, 1, 7, "bone")
    s.hline(1, w - 2, 8, "grey_brown"); s.vline(w - 1, 1, 7, "grey_brown")
    for k, ch in enumerate(texto):
        g = LET.get(ch) or r.DIG[ch]
        for j, fila in enumerate(g):
            for i, c in enumerate(fila):
                if c == "#":
                    s.set(2 + k * 4 + i, 2 + j, "bone")
    return s


# --- Helipuerto en tres estados -------------------------------------------------------------
def _cunas(s, d):
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = 48 + sx * d * .7, 48 + sy * d * .7
            for k in range(7):
                for g in (0, 1):
                    s.set(int(cx - sx * k), int(cy + sy * g), "bone")
                    s.set(int(cx + sx * g), int(cy - sy * k), "bone")


def _flecha(s, ang, r, fuera):
    """Galón de 2 px de grueso en el ángulo ang (rad) y radio r; la punta mira hacia fuera o hacia dentro."""
    ux, uy = math.cos(ang), math.sin(ang)
    px, py = -uy, ux
    t = 1 if fuera else -1
    tip = (48 + ux * (r + 2.5 * t), 48 + uy * (r + 2.5 * t))
    for lado in (-1, 1):
        for g in (0, 1):
            bx = 48 + ux * (r - 2.5 * t - g * t) + px * 4 * lado
            by = 48 + uy * (r - 2.5 * t - g * t) + py * 4 * lado
            s.line(int(round(bx)), int(round(by)), int(round(tip[0] - ux * g * t)), int(round(tip[1] - uy * g * t)),
                   "bone")


def _aro(s, f, progreso=None):
    for y in range(96):
        for x in range(96):
            dd = math.hypot(x + .5 - 48, y + .5 - 48)
            if not 44 <= dd <= 45.5:
                continue
            a = (math.degrees(math.atan2(x + .5 - 48, -(y + .5 - 48))) + 360) % 360   # 0 arriba, horario
            if progreso is None:
                if int(a / 6) % 2 == f % 2:
                    s.set(x, y, "beige")
            else:
                s.set(x, y, "bone" if a < progreso * 360 else ("beige" if int(a / 6) % 2 == 0 else None))


def helipuerto(modo, f):
    s = Sprite(96, 96)
    if modo == "rearme":
        _cunas(s, 22)
        _aro(s, f, progreso=(f + 1) / 8)
        for k in range(8):                           # muescas de los octavos por dentro del aro
            a = math.radians(k * 45)
            x, y = 48 + math.sin(a) * 41.5, 48 - math.cos(a) * 41.5
            s.rect(int(x) - 0, int(y) - 0, 2, 2, "bone" if k <= f else "beige")
    else:
        t = f / 3
        fuera = modo == "despegue"
        _cunas(s, (22 + t * 18) if fuera else (40 - t * 18))
        r = (30 + t * 8) if fuera else (38 - t * 8)
        for k in range(4):
            _flecha(s, math.radians(k * 90), r, fuera)        # en los ejes, entre las cuñas
        _aro(s, f)
    s.outline("olive_shadow")
    return s


# --- E1: humo del bombardeo ------------------------------------------------------------------
HUMO = {"denso": .58, "medio": .38, "disipando": .2}


def humo_bombardeo(etapa, f):
    """Nube baja y ancha (128x96) que se revuelve en bucle de 8 f; Marcos encadena denso → medio → disipando
    (unos 4 + 4 + 3 s). Nunca más del 58 % de píxeles: la horda se sigue leyendo por debajo (ΔL* de Sara)."""
    s = Sprite(128, 96)
    dens = HUMO[etapa]
    hinch = {"denso": 1.0, "medio": 1.08, "disipando": 1.16}[etapa]
    centros = ((64, 60, 34), (40, 66, 22), (88, 64, 24), (58, 38, 22), (78, 44, 18))
    for y in range(96):
        for x in range(128):
            v = 0
            for cx, cy, r in centros:
                v = max(v, 1 - math.hypot(x + .5 - cx, (y + .5 - cy) * 1.25) / (r * hinch))
            if v <= 0:
                continue
            n = ruido(x + f * 4, y, 32, 31, per=32) * .55 + ruido(x + f * 4, y + 3, 8, 32, per=32) * .45
            q = v * 1.6 + (n - .5) * .8
            if q <= .15:
                continue
            borde = q < .45
            if not tramado(x + f * 4, y, dens * (.55 if borde else 1)):
                continue
            c = "concrete" if borde else "olive_grey"
            s.set(x, y, c)
    return s


# --- E3: fuego en el suelo -------------------------------------------------------------------
def fuego_suelo(f):
    """Celda de 30x30 (3.0 m = 6 celdas de 0.5 m de la simulación, decisión de Mak) que se repite en X;
    llamas bajas, sin ámbar, periódicas en 8."""
    s = Sprite(30, 30)
    base = 28
    for y in range(6, base + 1):
        for x in range(30):
            lengua = .55 + .55 * _h(int(x // 5) % 6, f, 551)
            alto = (base - y) / (22 * lengua)
            n = ruido(x, y + f * 3.75, 10, 41, per=30) * .6 + ruido(x, y + f * 3.75, 5, 42, per=30) * .4
            v = (1 - alto * 1.15) + (n - .5) * .9 - .1
            if v <= .05:
                continue
            nucleo = .78 if f % 2 == 0 else .92
            c = "cream" if v > nucleo else "beige" if v > .6 else "ochre_dark" if v > .32 else "rust"
            s.set(x, y, c)
    for k in range(3):                               # pavesas
        y = int((base - 8 - _h(k, 0, 552) * 18 - f * 4) % 22) + 2
        x = int(4 + _h(k, 1, 552) * 22)
        if not s.get(x, y)[3]:
            s.set(x, y, "beige" if k % 2 else "ochre_dark")
    return s


def rescoldo_suelo(f):
    """Lo que queda los últimos segundos: brasas que parpadean sobre la celda (30x16, 4 f)."""
    s = Sprite(30, 16)
    for y in range(16):
        for x in range(30):
            n = ruido(x, y * 2, 10, 61, per=30)
            p = _h(x, y, 600 + f)
            if n > .62 and p > .55:
                s.set(x, y, "ochre_dark" if n > .74 and p > .8 else "rust")
            if n > .78 and p > .93:
                s.set(x, y, "beige")
    return s


def suelo_quemado():
    s = Sprite(30, 30)
    for y in range(30):
        for x in range(30):
            e = math.hypot(x + .5 - 15, y + .5 - 15) / 15
            n = ruido(x, y, 10, 71, per=30)
            v = 1 - e + (n - .5) * .7
            if v <= .1:
                continue
            if v > .55 and tramado(x, y, .7):
                s.set(x, y, "olive_shadow")
            elif tramado(x, y, .5 + v * .4):
                s.set(x, y, "grey_brown" if v > .3 else "olive_grey")
    return s


# --- E2: cráter ------------------------------------------------------------------------------
def crater(v):
    s = Sprite(64, 64)
    r = 20 if v == 0 else 16
    sx = 1.0 if v == 0 else 1.15
    for y in range(64):
        for x in range(64):
            dx, dy = (x + .5 - 32) / sx, (y + .5 - 32)
            d = math.hypot(dx, dy) + (ruido(x, y, 8, 80 + v, per=64) - .5) * 5
            if d < r * .55:                          # fondo del hoyo
                s.set(x, y, "olive_shadow" if d < r * .3 and tramado(x, y, .8) else "grey_brown")
            elif d < r * .8:                         # pared: la mitad de arriba a la izquierda a la sombra
                s.set(x, y, "grey_brown" if dx + dy < 0 else "olive_grey")
            elif d < r:                              # labio de tierra levantada, con luz arriba a la izquierda
                s.set(x, y, "beige" if dx + dy < -r * .4 else "ochre_dark" if dx + dy < r * .5 else "brown")
            elif d < r * 1.6 and tramado(x, y, .45 * (1.6 - d / r) / .6):   # tierra proyectada
                s.set(x, y, "ochre_dark" if _h(x, y, 90 + v) > .5 else "brown")
    for k in range(10):                              # escombros
        a = _h(k, 0, 91 + v) * math.tau
        rr = r * (1.1 + _h(k, 1, 91 + v) * .8)
        x, y = int(32 + math.cos(a) * rr * sx), int(32 + math.sin(a) * rr)
        s.rect(x, y, 2, 2 if k % 3 else 1, "concrete_light" if k % 2 else "concrete")
        s.set(x + 1, y + 1, "grey_brown")
    return s


# --- Exportar --------------------------------------------------------------------------------
def _hoja(out, base, frames, bucle, piv, blend="alfa", fps=FPS, extra=None):
    w, h = frames[0].w, frames[0].h
    n = len(frames)
    hoja = Image.new("RGBA", (w * n, h), (0, 0, 0, 0))
    for k, s in enumerate(frames):
        hoja.alpha_composite(s.img, (w * k, 0))
    hoja.save(out / f"{base}.png")
    vm = {"fps": fps, "loop": bucle, "frames": n, "frameSize": [w, h], "pivot": list(piv), "texel_m": 0.1,
          "blend": blend, "capa": "VFX_HD", "origen": "pixelvm, build_apoyo_aereo.py"}
    vm.update(extra or {})
    meta = {"frames": [{"filename": f"{base} {k}", "frame": {"x": w * k, "y": 0, "w": w, "h": h}, "rotated": False,
                        "trimmed": False, "spriteSourceSize": {"x": 0, "y": 0, "w": w, "h": h},
                        "sourceSize": {"w": w, "h": h}, "duration": round(1000 / fps)} for k in range(n)],
            "meta": {"image": f"{base}.png", "size": {"w": w * n, "h": h}, "format": "RGBA8888", "viaMuerta": vm}}
    (out / f"{base}.json").write_text(json.dumps(meta, indent=1, ensure_ascii=False))


def exportar(out):
    out = Path(out)
    todo = {}
    for t in HELIS:
        fila = []
        for a in ALTURAS:
            s = sombra(t, a)
            s.img.save(out / f"decal_sombra_heli_{t}_{a}.png")
            fila.append(s)
        (out / f"decal_sombra_heli_{t}.json").write_text(json.dumps({
            "texel_m": 0.2, "pivot": [0.5, 0.5], "morro": "+X", "alturas_m": {"bajo": "0-5", "medio": "5-20",
                                                                             "alto": ">20"},
            "nota": "decal en el suelo justo debajo del buje (sombra vertical, no la del sol); gira con el rumbo"},
            ensure_ascii=False))
        todo[f"sombra_{t}"] = fila
    li, ms, le = linea_altura(), marca_suelo(), letras()
    li.img.save(out / "ui_altura_linea_3x8.png")
    ms.img.save(out / "ui_altura_suelo.png")
    le.img.save(out / "ui_letras_3x5.png")
    (out / "ui_altura_linea_3x8.json").write_text(json.dumps({
        "wrapY": True, "uso": "capa de interfaz nítida, del buje del aparato a ui_altura_suelo (pivote 0.5, 0.5)"},
        ensure_ascii=False))
    (out / "ui_letras_3x5.json").write_text(json.dumps({"orden": ORDEN_LETRAS, "paso": 4, "alto": 5,
                                                        "uso": "con ui_digitos_3x5 y ui_eta_placa: «NH-1», «TG-1»"},
                                                       ensure_ascii=False))
    todo["altura"] = [li, ms, etiqueta("NH-1"), etiqueta("TG-1")]
    todo["letras"] = [le]
    for modo in ("despegue", "regreso", "rearme"):
        n = 8 if modo == "rearme" else 4
        fr = [helipuerto(modo, f) for f in range(n)]
        extra = {"nota": "centrado sobre decal_helipuerto_128"}
        if modo == "rearme":
            extra["progreso"] = "no es bucle: fotograma = min(7, floor(progreso * 8))"
        _hoja(out, f"decal_helipuerto_{modo}_96", fr, modo != "rearme", (.5, .5), fps=1000 // 150 if modo != "rearme"
              else 1, extra=extra)
        todo[f"heli_{modo}"] = fr
    for e in HUMO:
        fr = [humo_bombardeo(e, f) for f in range(8)]
        _hoja(out, f"vm_vfx_humo_bombardeo_{e}", fr, True, (.5, .8), fps=8,
              extra={"cadena": "denso 4 s → medio 4 s → disipando 3 s", "nota": "tramado, nunca negro (E1)"})
        todo[f"humo_{e}"] = fr
    fr = [fuego_suelo(f) for f in range(8)]
    _hoja(out, "vm_vfx_fuego_suelo", fr, True, (.5, .94), extra={"wrapX": True, "celda_m": 3.0, "celdas_sim": 6,
                                                                 "nota": "negación de zona (E3), unos 15 s; sin ámbar"})
    todo["fuego_suelo"] = fr
    fr = [rescoldo_suelo(f) for f in range(4)]
    _hoja(out, "vm_vfx_rescoldo_suelo", fr, True, (.5, .5), fps=6, extra={"celda_m": 3.0, "celdas_sim": 6})
    todo["rescoldo"] = fr
    q = suelo_quemado()
    q.img.save(out / "decal_suelo_quemado_30.png")
    cr = [crater(v) for v in range(2)]
    for v, s in enumerate(cr):
        s.img.save(out / f"decal_crater_{v + 1}_64.png")
    todo["suelo"] = [q] + cr
    return todo


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_apoyo_aereo")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 3
    filas = list(todo.items())
    fondos = {"sombra": "olive_dark", "suelo": "olive_dark", "fuego": "olive_grey", "rescoldo": "olive_dark",
              "humo": "olive_dark", "heli": "olive_grey"}
    Wt = max(sum(s.w * E + 6 for s in fr) for _, fr in filas)
    Ht = sum(max(s.h for s in fr) * E + 6 for _, fr in filas)
    hoja = Image.new("RGBA", (Wt, Ht), PAL["navy"])
    y = 0
    for n, fr in filas:
        x = 0
        col = next((v for k, v in fondos.items() if n.startswith(k)), "slate_dark")
        for s in fr:
            fondo = Image.new("RGBA", (s.w, s.h), PAL[col])
            hoja.alpha_composite(Image.alpha_composite(fondo, s.img).resize((s.w * E, s.h * E), Image.NEAREST), (x, y))
            x += s.w * E + 6
        y += max(s.h for s in fr) * E + 6
    hoja.save(OUT / "contacto_apoyo_aereo_x3.png")
    print("ok")
