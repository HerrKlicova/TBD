"""Textura de detalle del suelo de NOCHE (D-N129, regla 6). Bruno (Arte técnico), 7-nov.

Encargo de Lucía: el suelo de noche deja de pasar por SnapSub (17 colores) y usa la paleta de 32 completa, con 3 o más
valores de su familia y el color dominante en el 30 % como mucho. En la Noche41 (marcos/noche-prototipo 347ea06) el
dominante ocupa el 54,5 %, porque las texturas de día son beige b4a080 en un 91 % y el paso a 17 colores lo devuelve todo a beige.

Una sola textura de 64x64 (6,4 m, téxel 0,1 m, anclada al mundo como las de día) para TODAS las zonas de noche, porque D-N129
dice que de noche ninguna zona se distingue (sin parches ni aristas rectas). Se usa tal cual, sin SnapSub ni mezcla con la media.
Manchas orgánicas de fbm periódico (tileable) en 3 valores de la familia de la tierra, más grano suelto de 1 px. Sin tramado:
ninguna rejilla ni patrón ordenado. Sin valores de la horda, sin d4d0cc, ámbar ni rojos, sin 442c0c.

Primera prueba (manchas grandes de fbm a 32 px y 3 valores): se leía como camuflaje y la repetición se veía. Descartada.
Ahora es grano fino: fbm de periodo 8-4 (o 16-4) mezclado con ruido de 1 px, y umbrales por cuantiles. Así las cuotas se cumplen exactas,
y cada mancha toca solo a sus vecinas de valor (rampa continua, de 5 a 7 L* por paso).

5 variantes (la regla de las 5 muestras):
  A  beige 30 / concrete_light 30 / olive_light 30 / olive 10, grano 8-4
  B  lo mismo con algo más de estructura (16-8-4)
  C  beige 28 / concrete_light 30 / olive_light 28 / tan 14: más cálida
  D  beige 20 / concrete_light 30 / olive_light 30 / olive 20: más fría y algo más oscura
  E  beige 30 / concrete_light 25 / olive_light 25 / olive_yellow 10 / olive 10, grano 16-8-4
"""
import sys
from collections import Counter
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite, _lab_l  # noqa: E402
from build_a7_texturas import T, _h, fbm, motas  # noqa: E402

PROHIBIDOS = {"olive_black", "olive_shadow", "slate_dark", "brown_dark", "red_bright", "amber", "slate", "maroon",
              "blood_dark", "wine", "red", "navy", "black", "bone"}
MAX_DOM = 0.30
MIN_VALORES = 3          # valores que pasan del 5 %

VARIANTES = {
    # nombre: ({color: cuota}, periodos del fbm, peso del ruido de 1 px)
    "A": ({"beige": .30, "concrete_light": .30, "olive_light": .30, "olive": .10}, (8, 4), 0.30),
    "B": ({"beige": .30, "concrete_light": .30, "olive_light": .30, "olive": .10}, (16, 8, 4), 0.30),
    "C": ({"beige": .28, "concrete_light": .30, "olive_light": .28, "tan": .14}, (8, 4), 0.30),
    "D": ({"beige": .20, "concrete_light": .30, "olive_light": .30, "olive": .20}, (8, 4), 0.30),
    "E": ({"beige": .30, "concrete_light": .25, "olive_light": .25, "olive_yellow": .10, "olive": .10}, (16, 8, 4), 0.30),
}


def textura(nombre):
    cuotas, per, w = VARIANTES[nombre]
    cols = sorted(cuotas, key=lambda k: -_lab_l(PAL[k]))          # de claro a oscuro: rampa continua
    seed = 9100 + ord(nombre)
    v = [[(1 - w) * fbm(x, y, seed, per) + w * _h(x, y, seed + 7) for x in range(T)] for y in range(T)]
    orden = sorted((v[y][x], x, y) for y in range(T) for x in range(T))
    s = Sprite(T, T)
    i = 0
    for c in reversed(cols):                                     # lo bajo del ruido es lo oscuro
        n = round(cuotas[c] * T * T) if c != cols[0] else len(orden) - i
        for _, x, y in orden[i:i + n]:
            s.set(x, y, c)
        i += n
    return s


def medir(s):
    nombres = {PAL[k][:3]: k for k in PAL}
    c = Counter(nombres.get(s.img.getpixel((x, y))[:3], "?") for y in range(T) for x in range(T))
    n = T * T
    return {k: round(v / n, 3) for k, v in c.most_common()}


def comprobar(nombre, s):
    d = medir(s)
    prob = [k for k in d if k in PROHIBIDOS or k == "?"]
    dom = max(d.values())
    val = sum(1 for f in d.values() if f > 0.05)
    ok = not prob and dom <= MAX_DOM and val >= MIN_VALORES
    return ok, d, dom, val, prob


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_suelo_noche")
    OUT.mkdir(parents=True, exist_ok=True)
    E, G = 3, 8
    hoja = Image.new("RGBA", (5 * (T * 3 * E // 2 + G) + G, T * 3 * E // 2 + 2 * G + 20), PAL["navy"])
    malos = 0
    for i, n in enumerate(VARIANTES):
        s = textura(n)
        ok, d, dom, val, prob = comprobar(n, s)
        malos += not ok
        print(n, "OK" if ok else "FALLA", f"dominante {dom:.1%}", f"valores>5% {val}", d,
              {k: round(_lab_l(PAL[k]), 1) for k in d if k in PAL}, prob or "")
        s.img.save(OUT / f"suelo_noche_{n}_64.png")
        mos = Image.new("RGBA", (T * 3, T * 3))
        for a in range(3):
            for b in range(3):
                mos.paste(s.img, (a * T, b * T))
        mos.save(OUT / f"suelo_noche_{n}_mosaico3x3.png")
        hoja.paste(mos.resize((T * 3 * E // 2, T * 3 * E // 2), Image.NEAREST), (G + i * (T * 3 * E // 2 + G), G))
    hoja.save(OUT / "muestra_suelo_noche_5.png")
    print("ok" if not malos else f"{malos} variantes fallan")
