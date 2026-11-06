"""D-N80 · Color por instancia para la horda y rampa del mapa de sangre. Bruno (Arte técnico), 7-nov (noche).

Encargo de Mak (00:18): unos 8 tonos de camisa, 6 de pantalón y 4 de piel, con su regla de mezcla, que
sigan leyéndose de noche contra el suelo, sin confundirse con nuestros soldados ni con el coyote #605c48.
Los zombis son personas (norma de variantes de Eugenio), pero la horda sigue siendo LO MÁS OSCURO de la
escena (D-N61): por eso todos los tonos viven en una banda de valor estrecha, L* 9–16, y la variedad va
en el matiz y la saturación, nunca en el valor. Así:
  - el decorado más oscuro (#303424, L* 20,9) queda siempre por encima de cualquier zombi;
  - de noche, con el suelo en L* ≈ 25 (luna 0,45, D-N69), el contraste de Sara (ΔL* ≥ 8) se mantiene;
  - ningún tono es oliva (soldado), coyote, rojo o granate (sangre y ataque) ni azul pizarra (#404460, rim).
RESERVA PARA LA HORDA: todo color final con L* < 20. En concreto, los valores en que cuantizaba el
pantalón del soldado, #1e220c y #2e3022 (65 % de #1c200c / #303424 + 35 % del albedo), son de la horda:
ningún albedo de unidad propia puede bajar de L* 28 (#48403c). Se avisa a Teo.

Salidas:
  horda_paleta_lut.png   8x3: fila 0 camisas (8), fila 1 pantalones (6), fila 2 piel (4); lo que sobra, transparente
  horda_paleta.json      hex, L*, pesos y la regla de mezcla
  sangre_rampa.png       16x1: nivel de sangre acumulada (0–15) → color; alfa 0/255
  bayer4.png             4x4: umbral del tramado en espacio de mundo (0–15 en el canal R, x16)
  muestra_horda_*.png    200 zombis a ortho 26 (16 px con la escala ×1,3), día y noche, x1 y x3
"""
import json
import math
import random
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, _lab_l  # noqa: E402

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_horda")

# (nombre, hex, peso): pesos de una calle cualquiera (más gris, negro y vaquero que color).
CAMISAS = [("negro desteñido", "#1c1c1e", 3), ("gris marengo", "#262628", 3), ("azul mahón", "#1c2230", 2),
           ("chocolate", "#28221a", 2), ("tostado", "#2a2418", 1), ("gris azulado", "#22262c", 2),
           ("ciruela apagado", "#26202c", 1), ("pardo sucio", "#2a2724", 2)]
PANTALONES = [("vaquero", "#1a202e", 4), ("negro", "#1a1a1c", 3), ("gris", "#252525", 2),
              ("marrón", "#262016", 2), ("chándal marino", "#181c2a", 1), ("pana parda", "#28241c", 1)]
PIEL = [("cerúlea", "#262624", 2), ("morena", "#2c2724", 2), ("amoratada", "#28242c", 1), ("terrosa", "#2a2720", 2)]

# Colores que la horda no debe imitar: soldado (D-N57), coyote, rim, sangre y ataque.
PROHIBIDOS = {"olive": "#70784c", "olive_dark": "#50541c", "olive_grey (coyote)": "#605c48", "slate (rim)": "#404460",
              "blood_dark": "#4c1008", "wine": "#642030", "red": "#7c181c", "red_bright": "#982420"}


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def lab(c):
    def f(v):
        v /= 255
        return v / 12.92 if v <= 0.04045 else ((v + .055) / 1.055) ** 2.4
    r, g, b = (f(v) for v in c[:3])
    X, Y, Z = (r * .4124 + g * .3576 + b * .1805) / .95047, r * .2126 + g * .7152 + b * .0722, (r * .0193 + g * .1192 + b * .9505) / 1.08883
    g3 = lambda t: t ** (1 / 3) if t > .008856 else 7.787 * t + 16 / 116
    return 116 * g3(Y) - 16, 500 * (g3(X) - g3(Y)), 200 * (g3(Y) - g3(Z))


def matiz(c):
    L, a, b = lab(c)
    return math.degrees(math.atan2(b, a)) % 360, math.hypot(a, b)


def revisar():
    """Comprueba la banda de valor y que ningún tono comparta matiz con un color prohibido (±25° con croma ≥ 4)."""
    avisos = []
    for grupo, lista in (("camisa", CAMISAS), ("pantalón", PANTALONES), ("piel", PIEL)):
        for n, h, _ in lista:
            L = _lab_l(rgb(h))
            if not 9 <= L <= 16.5:
                avisos.append(f"{grupo} {n}: L* {L:.1f} fuera de 9–16")
            hz, cz = matiz(rgb(h))
            for pn, ph in PROHIBIDOS.items():
                hp, cp = matiz(rgb(ph))
                d = min(abs(hz - hp), 360 - abs(hz - hp))
                if cz >= 4 and d < 25 and abs(L - _lab_l(rgb(ph))) < 10:   # solo confunde si además el valor es parecido
                    avisos.append(f"{grupo} {n}: matiz a {d:.0f}° de {pn} (croma {cz:.1f})")
    return avisos


def elige(idx):
    """Regla de mezcla (determinista, la misma en todos los clientes del coop): hash del Entity.Index.
    camisa y pantalón por peso; piel uniforme; si camisa y pantalón quedan a menos de 4 de ΔE (un mono
    entero del mismo tono), el pantalón pasa al siguiente de la lista."""
    def h(k):
        n = (idx * 374761393 + k * 668265263) & 0xFFFFFFFF
        n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
        return ((n ^ (n >> 16)) & 0xFFFF) / 65536

    def por_peso(lista, u):
        tot = sum(p for *_, p in lista)
        acc = 0
        for i, (*_, p) in enumerate(lista):
            acc += p / tot
            if u < acc:
                return i
        return len(lista) - 1
    c = por_peso(CAMISAS, h(1))
    p = por_peso(PANTALONES, h(2))
    lc, lp = lab(rgb(CAMISAS[c][1])), lab(rgb(PANTALONES[p][1]))
    if math.dist(lc, lp) < 4:
        p = (p + 1) % len(PANTALONES)
    s = int(h(3) * len(PIEL))
    return c, p, s


# --- Rampa del mapa de sangre ------------------------------------------------------------------
def rampa_sangre():
    """16 niveles de sangre acumulada en un trozo. 0–3 nada; 4–7 gotas sueltas en #4c1008 (las pone el
    tramado: el nivel se compara con el umbral Bayer); 8–11 charco #4c1008; 12–13 charco con vetas #642030;
    14–15 fresco, con #7c181c. Nunca #982420. El trozo se seca bajando un nivel cada 1,25 s (20 s de 16 a 0)."""
    cols = [None] * 4 + ["blood_dark"] * 8 + ["wine"] * 2 + ["red"] * 2
    im = Image.new("RGBA", (16, 1), (0, 0, 0, 0))
    for i, c in enumerate(cols):
        if c:
            im.putpixel((i, 0), PAL[c])
    return im


def bayer4():
    B = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
    im = Image.new("L", (4, 4))
    for y in range(4):
        for x in range(4):
            im.putpixel((x, y), B[y][x] * 16)
    return im


def lut():
    im = Image.new("RGBA", (8, 3), (0, 0, 0, 0))
    for fila, lista in enumerate((CAMISAS, PANTALONES, PIEL)):
        for i, (_, h, _) in enumerate(lista):
            im.putpixel((i, fila), rgb(h) + (255,))
    return im


# --- Muestra -----------------------------------------------------------------------------------
def figura(h, paso, cam, pan, piel):
    """Silueta de build_lamina_siluetas (16 px, brazos al frente) pintada por partes: cabeza y manos en piel,
    torso y brazos en camisa, piernas en pantalón. Devuelve {(x, y): rgb}."""
    import build_lamina_siluetas as bl
    m = bl.silueta(h, paso)
    cab = 2 if h < 16 else 3
    cadera = round(h * 0.55)
    out = {}
    maxx = max(x for x, _ in m)
    for (x, y) in m:
        if y < cab:
            c = piel
        elif y >= cadera:
            c = pan
        elif x >= maxx - 0:                            # la mano, al final del brazo
            c = piel
        else:
            c = cam
        out[(x, y)] = c
    return out


def luz(c, hora):
    """Día: luz cálida suave. Noche: el suelo de la apisonada cae a L* ≈ 25 (D-N69); la horda, a lo que dé."""
    if hora == "dia":
        t, k = (240, 220, 156), .15
        return tuple(int(a * (1 - k) + a * b / 255 * k) for a, b in zip(c, t))
    f = .42
    return tuple(int(a * f * w) for a, w in zip(c, (0.95, 1.0, 1.15)))


SUBPAL_HORDA = ["black", "olive_black", "brown_dark", "olive_shadow"]   # sin navy ni slate_dark: sacaban zombis azules


def pase_paleta(c, horda=False):
    """Simula el paso de paleta del juego: 65 % del color de paleta más cercano + 35 % del color de entrada.
    La horda se cuantiza a su subpaleta (como el suelo a la suya, D-N69 y adenda del asfalto)."""
    if horda:
        pal = [PAL[n][:3] for n in SUBPAL_HORDA]
    else:
        pal = [PAL[n][:3] for n in ("brown_dark", "olive_shadow", "brown", "grey_brown", "olive_dark", "olive_grey",
                                    "ochre_dark", "olive", "concrete", "olive_yellow", "tan", "olive_light",
                                    "concrete_light", "beige", "grey_light", "bone", "cream")]
    p = min(pal, key=lambda q: sum((a - b) ** 2 for a, b in zip(q, c)))
    return tuple(int(.65 * a + .35 * b) for a, b in zip(p, c))


def muestra(hora, variada, W=320, H=180, n=200, seed=26):
    import build_suelo_sagunto as su
    import build_asfalto_variado as av
    import build_lamina_siluetas as bl
    base = Image.new("RGB", (W, H))
    ap = su.apisonada().img.convert("RGB")
    for x in range(0, W, 64):
        for y in range(0, H, 64):
            base.paste(ap, (x, y))
    base.paste(av.calle(W=W, H=60, seed=3).convert("RGB"), (0, 100))
    px = base.load()
    suelo_cache = {}
    for y in range(H):
        for x in range(W):
            c = px[x, y]
            if c not in suelo_cache:
                suelo_cache[c] = pase_paleta(luz(c, hora))
            px[x, y] = suelo_cache[c]
    rnd = random.Random(seed)
    figs = sorted(((rnd.randrange(2, W - 10), rnd.randrange(18, H - 2), i) for i in range(n)), key=lambda t: t[1])
    H16 = 16                                           # 1,75 m × 1,3 / (2·26 / 360) ≈ 16 px a 1/3
    sombra = set()
    for x0, pies, i in figs:
        sombra |= {(x0 + sx, pies + sy) for sx, sy in bl.sombra(bl.silueta(H16, i % 2), H16, 1.5 if hora == "dia" else 1.0)}
    for X, Y in sombra:
        if 0 <= X < W and 0 <= Y < H:
            c = px[X, Y]
            px[X, Y] = tuple(int(a * .68) for a in c)
    lums = []
    for x0, pies, i in figs:
        if variada:
            c, p, s = elige(i)
            cols = rgb(CAMISAS[c][1]), rgb(PANTALONES[p][1]), rgb(PIEL[s][1])
        else:
            cols = (PAL["olive_black"][:3],) * 3
        f = figura(H16, i % 2, *cols)
        for (x, y), col in f.items():
            X, Y = x0 + x, pies - (H16 - 1) + y
            if 0 <= X < W and 0 <= Y < H:
                v = pase_paleta(luz(col, hora), horda=True)
                px[X, Y] = v
                lums.append(_lab_l(v))
    suelo_l = sorted(_lab_l(c) for c in suelo_cache.values())
    return base, (max(lums), suelo_l[len(suelo_l) // 10])


def exportar(out):
    """Piezas para Unity: LUT de la horda, rampa de sangre y Bayer 4x4 (la regla va en horda_paleta.json)."""
    out = Path(out)
    lut().save(out / "horda_paleta_lut.png")
    rampa_sangre().save(out / "sangre_rampa.png")
    bayer4().save(out / "bayer4.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    av_ = revisar()
    print("revisión:", av_ or "OK")
    exportar(OUT)
    rampa_sangre().resize((256, 24), Image.NEAREST).save(OUT / "sangre_rampa_x16.png")
    info = {}
    hoja = Image.new("RGB", (640 + 12, 360 + 12), PAL["navy"][:3])
    for j, hora in enumerate(("dia", "noche")):
        for i, var in enumerate((False, True)):
            im, (lmax, lsuelo) = muestra(hora, var)
            nombre = f"muestra_horda_{hora}_{'variada' if var else 'un_valor'}"
            im.save(OUT / f"{nombre}_x1.png")
            im.resize((im.width * 3, im.height * 3), Image.NEAREST).save(OUT / f"{nombre}_x3.png")
            hoja.paste(im, (4 + i * 324, 4 + j * 184))
            info[nombre] = {"L_max_horda": round(lmax, 1), "L_p10_suelo": round(lsuelo, 1), "dL": round(lsuelo - lmax, 1)}
            print(nombre, info[nombre])
    hoja.resize((hoja.width * 3, hoja.height * 3), Image.NEAREST).save(OUT / "muestra_horda_comparativa_x3.png")
    meta = {"nota": __doc__.split("Salidas:")[0].strip(),
            "camisas": [{"nombre": n, "hex": h, "L": round(_lab_l(rgb(h)), 1), "peso": p} for n, h, p in CAMISAS],
            "pantalones": [{"nombre": n, "hex": h, "L": round(_lab_l(rgb(h)), 1), "peso": p} for n, h, p in PANTALONES],
            "piel": [{"nombre": n, "hex": h, "L": round(_lab_l(rgb(h)), 1), "peso": p} for n, h, p in PIEL],
            "regla": "hash(Entity.Index): camisa = por peso con h(1); pantalón = por peso con h(2), y si ΔE(camisa, pantalón) < 4 pasa al siguiente; piel = uniforme con h(3). Ver elige() en build_horda_color.py",
            "subpaleta_horda": {n: "#%02x%02x%02x" % PAL[n][:3] for n in SUBPAL_HORDA},
            "reserva_horda": "todo color final con L* < 20, incluidos #1e220c y #2e3022; ninguna unidad propia por debajo de L* 28 (#48403c)",
            "medidas_muestra": info,
            "sangre": {"rampa": "sangre_rampa.png (16 niveles)", "bayer": "bayer4.png en espacio de mundo, celda = téxel de 0,1 m",
                       "regla": "nivel = floor(cantidad*16); se pinta si nivel > umbral Bayer y el color es rampa[nivel]; cada impacto suma 3 niveles en un radio de 0,3 m, cada muerte 8 en 0,6 m; se seca 1 nivel cada 1,25 s (20 s)"}}
    (OUT / "horda_paleta.json").write_text(json.dumps(meta, indent=1, ensure_ascii=False))
