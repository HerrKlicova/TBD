"""Huella de suelo de los restos de vehículos calcinados: 5 variantes (7-nov). Bruno (Arte técnico).

Encargo de Mak (7-nov, 07:49). Sustituye a las 2 variantes de decal_calcinado de D-N87 (build_vfx_vehiculos.py),
que se hicieron de memoria, por 5 variantes dibujadas con las fotos K8 de Paula
(referencias-reales/entorno/K8_calcinado_1-4.jpg). Es el decal de suelo que va DEBAJO de los restos 3D de Teo
(restos_vamtac.py, restos_dragon.py, restos_nh90.py); el armazón lo pone su modelo, no este decal.

Lo que dicen las fotos (medido con pixelvm, regiones recortadas a mano):
  K8_1 turismo reciente: chapa casi blanca (L* 71-87, gris claro y hueso) con hollín en churretes (L* 22-45);
       suelo bajo el coche muy oscuro (L* 3-26) y restos sueltos delante (parachoques caído, cables).
  K8_2 turismo viejo: óxido en la chapa y en el frontal, hojas y chatarra en el suelo, interior vacío.
  K8_3 furgoneta: ceniza gris clara repartida alrededor (L* 33-52), cristales rotos, ruedas sobre la llanta.
  K8_4 blindado de cadenas: oxidado, con hierba creciendo alrededor y entre los eslabones.
  Ninguna foto muestra un camión ni aluminio fundido en el suelo: eso queda SIN VERIFICAR y no se dibuja.

Reglas que se mantienen (D-N87 y D-N61):
  base en grey_brown 48403c y olive_grey 605c48; hollín olive_shadow 303424 solo en una mancha pequeña del centro
  (el suelo real es más negro, pero lo más oscuro del suelo es la horda); nada de 442c0c ni de rojos ni de ámbar;
  ninguna variante baja de L* 20.

Variantes (64x64, texel 0,1 m, planta; el vehículo va a lo largo de X):
  1 reciente     turismo recién quemado (K8_1): hollín al centro, aro de ceniza clara, cable de neumático suelto
  2 oxidado      restos viejos (K8_2): menos hollín, escamas de óxido y hojas secas
  3 ceniza       furgoneta (K8_3): huella más larga, ceniza clara más extendida y cristales
  4 piezas       con chapa caída (K8_1 y K8_2): puerta en el suelo y parachoques
  5 cadenas      blindado de cadenas (K8_4, Pizarro): eslabones sueltos a los lados y hierba seca en el borde
"""
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite, _lab_l  # noqa: E402
from build_d_vfx import _h, ruido, tramado  # noqa: E402

T = 64          # lado en texels (0,1 m); se cambia con fijar_tam()
K = 1.0         # T / 64: escala de POSICIONES y radios; las piezas (puerta, eslabón, cable) no escalan


def fijar_tam(t):
    global T, K
    T, K = t, t / 64


def px_(v):
    return int(v * K)
PROHIBIDOS = {"brown_dark", "red", "red_bright", "maroon", "blood_dark", "wine", "amber", "black", "olive_black",
              "navy"}
NOMBRES = ["reciente", "oxidado", "ceniza", "piezas", "cadenas"]


def huella(s, v, largo=27, ancho=19, hollin=.2, ceniza=.85, ceniza_dens=2.2, base_oliva=.65):
    """Mancha quemada en planta: hollín pequeño al centro, quemado tramado y aro de ceniza clara."""
    for y in range(T):
        for x in range(T):
            d = math.hypot((x + .5 - T / 2) / (largo * K), (y + .5 - T / 2) / (ancho * K))
            d += (ruido(x, y, 8, 1100 + v) - .5) * .35
            if d < hollin / math.sqrt(K) and tramado(x, y, .8):   # el hollín crece menos que la huella (D-N87)
                s.set(x, y, "olive_shadow")
            elif d < .55 and tramado(x, y, .9):
                s.set(x, y, "olive_grey" if _h(x, y, 1102 + v) > base_oliva else "grey_brown")
            elif d < ceniza and tramado(x, y, (ceniza - d) * ceniza_dens):
                # ceniza: casi toda gris oliva, con motas de hormigón claro (K8_3)
                s.set(x, y, "concrete" if _h(x, y, 1105 + v) > .72 else "olive_grey")


def motas(s, v, n, colores, sem, zona=(8, 10, 48, 44)):
    x0, y0, w, h = [v * K for v in zona]
    for k in range(int(n * K * K)):
        x, y = int(x0 + _h(k, v, sem) * w), int(y0 + _h(k, v, sem + 1) * h)
        s.set(x, y, colores[k % len(colores)])


def cable(s, cx, cy, r=3):
    """Aro de alambre de un neumático quemado (K8_1, K8_3: la goma desaparece y queda el talón de acero)."""
    for a in range(24):
        t = a * math.tau / 24
        s.set(int(round(cx + math.cos(t) * r)), int(round(cy + math.sin(t) * r * .8)), "concrete_light")


def v_reciente():
    s = Sprite(T, T)
    huella(s, 1)
    motas(s, 1, 10, ["concrete_light", "grey_light"], 1110)      # cristal y chapa clara sueltos
    cable(s, px_(53), px_(46))
    return s


def v_oxidado():
    s = Sprite(T, T)
    huella(s, 2, hollin=.12, base_oliva=.45, ceniza_dens=1.6)
    motas(s, 2, 16, ["rust", "tan", "rust"], 1120)                 # escamas de óxido (K8_2)
    motas(s, 2, 9, ["ochre_dark", "olive_dark"], 1123, (4, 4, 56, 56))   # hojas secas
    return s


def v_ceniza():
    s = Sprite(T, T)
    huella(s, 3, largo=31, ancho=18, ceniza=1.0, ceniza_dens=2.6)
    motas(s, 3, 18, ["grey_light", "concrete_light", "grey_light"], 1130, (4, 12, 56, 40))   # cristales (K8_3); sin d4d0cc, que es el color de la selección (Lucía)
    return s


def v_piezas():
    s = Sprite(T, T)
    huella(s, 4)
    # puerta caída (chapa quemada clara con hollín) al lado izquierdo, un poco girada
    for j in range(9):
        for i in range(14):
            x, y = px_(6) + i + j // 4, px_(44) + j
            c = "concrete_light" if _h(i, j, 1140) > .35 else "concrete"
            if i in (0, 13) or j in (0, 8):
                c = "grey_brown"
            elif _h(i // 3, j // 2, 1141) > .7:
                c = "olive_grey"                                  # churrete de hollín
            s.set(x, y, c)
    # parachoques caído delante (K8_1)
    for i in range(16):
        s.set(px_(46) + i // 2, px_(12) + i, "concrete")
        s.set(px_(47) + i // 2, px_(12) + i, "grey_brown")
    motas(s, 4, 8, ["concrete_light", "rust"], 1145)
    return s


def v_cadenas():
    s = Sprite(T, T)
    huella(s, 5, largo=30, ancho=22, hollin=.16, base_oliva=.5)
    # tramos de cadena caídos a los dos lados (K8_4): 2 tramos por lado, eslabones de 3x2 con 1 px de junta,
    # y algún eslabón suelto; no en fila completa, que parecería un marco
    for lado, y0 in ((0, px_(9)), (1, px_(51))):
        for tramo in range(2):
            x0 = px_(8 + tramo * 26 + _h(tramo, lado, 1150) * 8)
            n = 3 + int(_h(tramo, lado, 1151) * 3)
            for k in range(n):
                x = x0 + k * 4
                y = y0 + int((k * (_h(tramo, lado, 1152) - .5)) * .8)
                s.rect(x, y, 3, 2, "rust" if (k + tramo) % 3 else "grey_brown")
                s.set(x + 1, y, "tan")
        x, y = px_(20 + _h(lado, 9, 1153) * 24), y0 + (4 if lado == 0 else -4)
        s.rect(x, y, 3, 2, "rust")
    # matas de hierba seca en el borde (sin oliva 70784c, que es el color de nuestros soldados)
    for k in range(6):
        a = _h(k, 5, 1155) * math.tau
        cx = px_(32 + math.cos(a) * 28)
        cy = px_(32 + math.sin(a) * 23)
        for j in range(5):
            x, y = cx + int((_h(k, j, 1156) - .5) * 5), cy + int((_h(k, j, 1157) - .5) * 3)
            s.set(x, y, "olive_dark")
            s.set(x, y - 1, "olive_yellow" if j % 2 else "olive_dark")
    return s


def v_dispersa():
    """Para los vehículos de ruedas y el NH90 (no llevan cadenas): huella con otra semilla, dos aros de
    alambre y trozos de chapa quemada desperdigados (K8_1). Sustituye a «cadenas» en los tamaños grandes."""
    s = Sprite(T, T)
    huella(s, 6, largo=28, ancho=21, hollin=.15, base_oliva=.55)
    cable(s, px_(12), px_(50))
    cable(s, px_(50), px_(13))
    for k in range(int(6 * K)):
        x, y = px_(6 + _h(k, 1, 1160) * 52), px_(6 + _h(k, 2, 1160) * 52)
        w, h = 3 + int(_h(k, 3, 1160) * 4), 2 + int(_h(k, 4, 1160) * 2)
        s.rect(x, y, w, h, "concrete_light" if k % 2 else "concrete")
        s.hline(x, x + w - 1, y + h, "grey_brown")
    motas(s, 6, 8, ["concrete_light", "grey_light"], 1165)
    return s


VARIANTES = [v_reciente, v_oxidado, v_ceniza, v_piezas, v_cadenas]
# Tamaños por vehículo (Teo, 7-nov 08:05: planta de los LOD1 con escombros): 64 genérico y Pizarro,
# 85 VAMTAC (hasta 7,2 x 8,5 m), 110 Dragón (hasta 10,7 x 9,2 m), 180 NH90 (hasta 13,5 x 17,4 m).
TAMANOS = {64: "generico", 85: "vamtac", 110: "dragon", 180: "nh90"}


def comprobar(s):
    nombres = {v[:3]: k for k, v in PAL.items()}
    usados = {}
    for y in range(s.h):
        for x in range(s.w):
            p = s.px[x, y]
            if p[3]:
                usados[nombres.get(p[:3], "FUERA")] = usados.get(nombres.get(p[:3], "FUERA"), 0) + 1
    malos = [k for k in usados if k in PROHIBIDOS or k == "FUERA"]
    lmin = min(_lab_l(PAL[k][:3]) for k in usados if k != "FUERA")
    hollin = usados.get("olive_shadow", 0) / sum(usados.values())
    return usados, malos, lmin, hollin


def suelo(nombre):
    """Suelo de prueba: tierra ocre (suelo del mundo) y asfalto A7, simplificados."""
    s = Sprite(T, T)
    for y in range(T):
        for x in range(T):
            if nombre == "tierra":
                c = "olive_grey" if ruido(x, y, 8, 31) < .4 else "ochre_dark"
            else:
                c = "concrete_light" if _h(x, y, 42) > .94 else "concrete"
            s.set(x, y, c)
    return s


def noche(im):
    """Vista de noche aproximada: multiplica por el tono de luna (sin verificar frente a Unity)."""
    luna = (0.42, 0.46, 0.58)
    px = im.load()
    out = im.copy()
    po = out.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            po[x, y] = (int(r * luna[0]), int(g * luna[1]), int(b * luna[2]), a)
    return out


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_restos_calcinados")
    OUT.mkdir(parents=True, exist_ok=True)
    sprites = [fn() for fn in VARIANTES]
    for i, (n, s) in enumerate(zip(NOMBRES, sprites)):
        s.img.save(OUT / f"decal_calcinado_{i + 1}_{n}_64.png")
        usados, malos, lmin, hollin = comprobar(s)
        print(f"{i + 1} {n}: {len(usados)} colores, L* mín {lmin:.0f}, hollín {hollin:.1%}",
              "OK" if not malos and lmin >= 20 else f"MAL {malos}")
    E = 4
    filas = [("tierra de día", suelo("tierra"), False), ("asfalto de día", suelo("asfalto"), False),
             ("tierra de noche", suelo("tierra"), True), ("asfalto de noche", suelo("asfalto"), True)]
    G = 8
    hoja = Image.new("RGBA", (5 * (T * E + G) + G, len(filas) * (T * E + G) + G), PAL["navy"])
    for j, (_, fondo, de_noche) in enumerate(filas):
        for i, s in enumerate(sprites):
            im = Image.alpha_composite(fondo.img.copy(), s.img)
            if de_noche:
                im = noche(im)
            hoja.paste(im.resize((T * E, T * E), Image.NEAREST), (G + i * (T * E + G), G + j * (T * E + G)))
    hoja.save(OUT / "muestra_calcinado_5_variantes_x4.png")

    # tamaños por vehículo: misma densidad de texel (0,1 m); «cadenas» pasa a «dispersa» (no hay cadenas)
    filas_t = []
    for t, veh in TAMANOS.items():
        if t == 64:
            continue
        fijar_tam(t)
        fns = [v_reciente, v_oxidado, v_ceniza, v_piezas, v_dispersa]
        noms = NOMBRES[:4] + ["dispersa"]
        sp = [fn() for fn in fns]
        for i, (n, s) in enumerate(zip(noms, sp)):
            s.img.save(OUT / f"decal_calcinado_{veh}_{i + 1}_{n}_{t}.png")
            usados, malos, lmin, hollin = comprobar(s)
            print(f"{veh} {t} {i + 1} {n}: {len(usados)} colores, L* mín {lmin:.0f}, hollín {hollin:.1%}",
                  "OK" if not malos and lmin >= 20 else f"MAL {malos}")
        filas_t.append((t, [Image.alpha_composite(suelo("asfalto").img, s.img) for s in sp]))
    E = 2
    Wt = max(5 * (t * E + G) + G for t, _ in filas_t)
    Ht = sum(t * E + G for t, _ in filas_t) + G
    hoja = Image.new("RGBA", (Wt, Ht), PAL["navy"])
    y = G
    for t, ims in filas_t:
        for i, im in enumerate(ims):
            hoja.paste(im.resize((t * E, t * E), Image.NEAREST), (G + i * (t * E + G), y))
        y += t * E + G
    hoja.save(OUT / "muestra_calcinado_tamanos_x2.png")
    print("ok")
