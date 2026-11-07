"""Retratos de tarjeta de unidad, 48x48, con 6 variantes por tipo (P1, noche del 6-nov). Bruno (Arte técnico).

Encargo de Mak (23:47): las cinco escuadras (fusileros, granaderos, ametralladora, tiradores y C90) y los ocho
vehículos y helicópteros (Lince, VAMTAC, Pizarro, Dragón, Cougar, Tigre, NH90 y Chinook). Tienen que leerse a 48 px.
Referencias: lote militar de Paula (uniforme boscoso, casco y chaleco M2, G36E, MG4E, G28, NH90) y, desde el 7-nov,
MV1 a MV7 (Lince, Cougar, Chinook, VAMTAC, Pizarro y Tigre) y V1 (Dragón, con cotas de V1_dragon_dimensiones.md).
Lince, VAMTAC y Chinook van en verde liso; Pizarro y Dragón con camuflaje; Tigre y Cougar con las manchas claras de
las FAMET. Cougar y Tigre llevan ruedas, no patines. Los blindados de tierra miran a la derecha y los helicópteros
a la izquierda.
Reglas: tropa propia con albedo ≥ L* 28 (nada de los valores de la horda), sin ámbar ni rojo, luz arriba a la
izquierda, contorno negro. El fondo es transparente: lo pone la tarjeta (ui_tarjeta_unidad).
Escuadras: busto con el arma de su tipo cruzada; las variantes cambian la piel, la barba, las gafas, el braga y el
casco o el chambergo. Vehículos: de perfil; las variantes cambian las manchas del camuflaje (si lo llevan), el número
táctico, el barro y los accesorios.
"""
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import _h  # noqa: E402

N_VAR = 6
PIELES = [("tan", "rose_brown"), ("rose", "rose_brown"), ("tan", "ochre_dark"), ("rose_brown", "brown"),
          ("beige", "tan"), ("ochre_dark", "brown")]


def _camo(s, x0, y0, x1, y1, semilla, base="olive_dark", colores=("olive", "olive_grey", "brown"), dens=.35, solo=None):
    """Manchas de camuflaje boscoso sobre lo que ya es del color base (o de solo)."""
    obj = PAL[solo or base]
    for y in range(y0, y1):
        for x in range(x0, x1):
            if s.get(x, y) != obj:
                continue
            h = _h(x // 2, y // 2, semilla)
            if h < dens:
                s.set(x, y, colores[int(_h(x // 3, y // 3, semilla + 1) * len(colores)) % len(colores)])


# --- Escuadras ------------------------------------------------------------------------------
def _busto(v, sombrero=False):
    s = Sprite(48, 48)
    piel, piel_s = PIELES[v % len(PIELES)]
    s.poly([(4, 47), (7, 37), (15, 32), (33, 32), (41, 37), (44, 47)], "olive_dark")   # hombros
    _camo(s, 0, 30, 48, 48, 1100 + v)
    s.poly([(13, 35), (35, 35), (36, 47), (12, 47)], "olive_grey")                     # chaleco
    for x in (14, 20, 27):                                                             # portacargadores
        s.rect(x, 40, 5, 5, "olive_dark")
        s.hline(x, x + 4, 40, "olive")
    s.hline(13, 35, 35, "olive")
    s.rect(20, 27, 8, 6, piel_s)                                                       # cuello
    s.ellipse(16, 12, 31, 30, piel)                                                    # cara
    s.vline(30, 18, 28, piel_s)
    s.hline(19, 28, 29, piel_s)
    s.rect(19, 21, 3, 1, "brown_dark"); s.rect(25, 21, 3, 1, "brown_dark")             # ojos
    s.set(23, 23, piel_s); s.set(23, 24, piel_s)                                       # nariz
    s.hline(21, 26, 26, piel_s)                                                        # boca
    if v % 3 == 1:                                                                     # barba
        for y in range(24, 30):
            for x in range(17, 31):
                if s.get(x, y) in (PAL[piel], PAL[piel_s]) and (y > 25 or x < 19 or x > 28):
                    s.set(x, y, "brown" if v != 4 else "grey_brown")
        s.hline(21, 26, 26, "rose_brown")
    if v == 5:                                                                         # bigote
        s.hline(20, 27, 25, "brown")
    if v == 2:                                                                         # braga hasta la nariz
        for y in range(24, 33):
            for x in range(15, 33):
                if s.get(x, y) in (PAL[piel], PAL[piel_s]):
                    s.set(x, y, "olive_grey")
        s.hline(16, 31, 24, "olive")
    if sombrero:                                                                       # chambergo de tirador
        s.ellipse(10, 11, 37, 17, "olive_dark")
        s.ellipse(15, 5, 32, 15, "olive_dark")
        _camo(s, 10, 4, 38, 18, 1150 + v)
        s.hline(11, 36, 15, "olive")
    else:                                                                              # casco con funda
        s.ellipse(14, 6, 33, 21, "olive_dark")
        s.rect(14, 16, 20, 3, "olive_dark")
        _camo(s, 13, 5, 35, 19, 1160 + v, dens=.2)
        s.hline(14, 33, 18, "olive_grey")
        s.vline(16, 19, 25, "olive_grey"); s.vline(31, 19, 25, "olive_grey")           # barboquejo
        if v in (0, 3):                                                                # gafas balísticas en el casco
            s.rect(17, 10, 14, 3, "grey_brown")
            s.rect(18, 10, 5, 2, "concrete_light"); s.rect(25, 10, 5, 2, "concrete_light")
        if v == 4:                                                                     # soporte de visión nocturna
            s.rect(21, 7, 6, 3, "grey_brown"); s.set(22, 7, "concrete")
    if v == 3:                                                                         # gafas de sol
        s.rect(18, 20, 5, 2, "grey_brown"); s.rect(24, 20, 5, 2, "grey_brown")
        s.hline(18, 28, 20, "slate_dark")
    return s


def _arma_cruzada(w, largo=34, y0=44):
    """Arma en diagonal sobre el chaleco, en su propia capa w: 3 px de grueso, canto de arriba en concrete."""
    pts = []
    for k in range(largo):
        x = 6 + k
        y = int(y0 - k * .36)
        pts.append((x, y))
        w.set(x, y - 1, "concrete")
        w.set(x, y, "grey_brown")
        w.set(x, y + 1, "grey_brown")
    return pts


def _con_arma(s, w):
    """Contorno slate_dark alrededor del arma para que se despegue del camuflaje, y encima del busto."""
    w.outline("slate_dark")
    s.stamp(w, 0, 0)


def fusileros(v):
    s, w = _busto(v), Sprite(48, 48)
    p = _arma_cruzada(w)
    x, y = p[14]
    w.rect(x - 1, y + 2, 3, 5, "grey_brown"); w.set(x - 1, y + 2, "concrete")          # cargador
    x, y = p[18]
    w.rect(x - 2, y - 5, 9, 3, "grey_brown"); w.hline(x - 2, x + 6, y - 5, "concrete")  # asa con visor
    _con_arma(s, w)
    s.outline()
    return s


def granaderos(v):
    s, w = _busto(v), Sprite(48, 48)
    p = _arma_cruzada(w)
    x, y = p[22]
    w.rect(x - 5, y + 2, 12, 4, "grey_brown"); w.hline(x - 5, x + 6, y + 2, "concrete")  # tubo del AG36, gordo
    _con_arma(s, w)
    for k in range(3):                                                                  # granadas de 40 mm en el chaleco
        s.rect(15 + k * 6, 37, 4, 3, "olive_light"); s.hline(15 + k * 6, 18 + k * 6, 37, "beige")
    s.outline()
    return s


def ametralladora(v):
    s, w = _busto(v), Sprite(48, 48)
    for k in range(10):                                                                 # cinta en bandolera
        s.set(10 + k * 3, 32 + k, "tan"); s.set(11 + k * 3, 32 + k, "ochre_dark"); s.set(10 + k * 3, 33 + k, "ochre_dark")
    p = _arma_cruzada(w, largo=40, y0=46)
    x, y = p[12]
    w.rect(x - 3, y + 2, 8, 6, "grey_brown"); w.hline(x - 3, x + 4, y + 2, "concrete")  # caja de cinta
    x, y = p[34]
    w.line(x, y + 2, x - 3, y + 7, "grey_brown"); w.line(x + 1, y + 2, x + 4, y + 7, "grey_brown")   # bípode
    _con_arma(s, w)
    s.outline()
    return s


def tiradores(v):
    s, w = _busto(v, sombrero=v % 2 == 0), Sprite(48, 48)
    p = _arma_cruzada(w, largo=41, y0=46)
    x, y = p[16]
    w.rect(x - 3, y - 5, 12, 3, "grey_brown"); w.hline(x - 3, x + 8, y - 5, "concrete")  # visor largo
    w.rect(x - 5, y - 6, 3, 5, "grey_brown"); w.rect(x + 8, y - 6, 3, 5, "grey_brown")
    for k in range(0, 8):                                                               # culata arena
        x, y = p[k]
        for d in (-1, 0, 1):
            w.set(x, y + d, "beige")
    _con_arma(s, w)
    s.outline()
    return s


def c90(v):
    s = _busto(v)
    for t in range(44):                                                                 # tubo al hombro, detrás de la cabeza
        x = 2 + t
        y = int(34 - t * .5)
        for d in range(-2, 2):
            if s.get(x, y + d)[3] == 0 or s.get(x, y + d) in (PAL["olive_dark"], PAL["olive"], PAL["olive_grey"], PAL["brown"]):
                if not (14 <= x <= 33 and 5 <= y + d <= 32):                            # la cabeza tapa el tubo
                    s.set(x, y + d, "olive" if d == -2 else "olive_dark")
    s.rect(1, 31, 3, 6, "olive_dark"); s.rect(43, 10, 3, 6, "olive_dark")               # bocas
    s.rect(36, 13, 4, 3, "grey_brown")                                                  # mira
    w = Sprite(48, 48)
    _arma_cruzada(w, largo=24, y0=46)                                                   # fusil corto colgado
    _con_arma(s, w)
    s.outline()
    return s


ESCUADRAS = {"fusileros": fusileros, "granaderos": granaderos, "ametralladora": ametralladora,
             "tiradores": tiradores, "c90": c90}


# --- Vehículos ------------------------------------------------------------------------------
def _rueda(s, cx, cy, r=4):
    s.ellipse(cx - r, cy - r, cx + r, cy + r, "grey_brown")
    s.ellipse(cx - r + 1, cy - r + 1, cx + r - 1, cy + r - 1, "slate_dark" if False else "grey_brown")
    s.ellipse(cx - 1, cy - 1, cx + 1, cy + 1, "concrete")
    s.set(cx - r + 1, cy - r + 2, "concrete")


def _numero(s, x, y, v):
    """Número táctico de 3x5 en hueso (dos cifras)."""
    DIG = {0: "###,#.#,#.#,#.#,###", 1: ".#.,##.,.#.,.#.,###", 2: "###,..#,###,#..,###", 3: "###,..#,.##,..#,###",
           4: "#.#,#.#,###,..#,..#", 5: "###,#..,###,..#,###", 6: "###,#..,###,#.#,###", 7: "###,..#,.#.,.#.,.#.",
           8: "###,#.#,###,#.#,###", 9: "###,#.#,###,..#,###"}
    n = 10 + int(_h(v, 0, 1200) * 89)
    for k, d in enumerate((n // 10, n % 10)):
        for j, fila in enumerate(DIG[d].split(",")):
            for i, ch in enumerate(fila):
                if ch == "#":
                    s.set(x + k * 4 + i, y + j, "bone")


def _barro(s, v, y0, y1):
    if v % 3 != 2:
        return
    for y in range(y0, y1):
        for x in range(48):
            if s.get(x, y)[3] and s.get(x, y) not in (PAL["grey_brown"], PAL["concrete"]) and _h(x, y, 1210) < (y - y0) / (y1 - y0) * .7:
                s.set(x, y, "ochre_dark" if _h(x, y, 1211) < .5 else "brown")


def _camo4(s, semilla, arena=False):
    """Camuflaje de manchas grandes (celdas de 4 px) para que a 48 px no se vuelva ruido. Con arena=True lleva
    también las manchas claras del esquema de las FAMET (Tigre y Cougar en MV2_2 y MV7)."""
    obj = PAL["olive_dark"]
    for y in range(48):
        for x in range(48):
            if s.get(x, y) != obj:
                continue
            h = _h((x + int(_h(y // 3, 0, semilla) * 3)) // 4, y // 3, semilla)
            if h < .22:
                s.set(x, y, "olive")
            elif h > .86:
                s.set(x, y, "brown")
            elif arena and .5 < h < .6:
                s.set(x, y, "olive_grey")


def _espejo(s):
    """Dibujo hecho con el morro a la izquierda → morro a la derecha, como el Pizarro y el Dragón."""
    s.img = s.img.transpose(Image.FLIP_LEFT_RIGHT)
    s.px = s.img.load()


def _vehiculo_fin(s, v, y0, y1, xn, yn, camo=True, arena=False):
    """camo=False: verde liso (Lince, VAMTAC y Chinook en las fotos MV1, MV3 y MV4 de Paula)."""
    if camo:
        _camo4(s, 1220 + v, arena)
    _barro(s, v, y0, y1)
    _numero(s, xn, yn, v)
    s.outline()
    return s


def lince(v):
    """Contra MV1_lince_1/2: caja alta de techo largo, capó corto e inclinado delante, pasos de rueda oscuros,
    ruedas grandes y mucha altura libre; verde liso, sin camuflaje. Se dibuja con el morro a la izquierda y se
    gira: en la tarjeta mira a la derecha, como el Pizarro y el Dragón."""
    s = Sprite(48, 48)
    s.poly([(3, 32), (3, 25), (11, 23), (15, 15), (44, 15), (45, 17), (45, 32)], "olive_dark")
    s.hline(15, 44, 15, "olive")                                                       # techo con luz
    s.poly([(12, 23), (15, 17), (20, 17), (20, 23)], "concrete")                       # parabrisas
    s.hline(15, 19, 17, "concrete_light")
    s.rect(23, 18, 7, 5, "concrete"); s.rect(33, 18, 6, 5, "concrete")                 # ventanillas blindadas
    s.vline(31, 16, 31, "grey_brown")                     # junta de puertas
    s.vline(21, 16, 31, "grey_brown")
    s.vline(14, 14, 24, "grey_brown")                                                  # snorkel en el montante
    s.hline(3, 45, 25, "olive")
    for cx in (12, 36):                                                                # pasos de rueda oscuros
        s.ellipse(cx - 7, 27, cx + 7, 41, "grey_brown")
    if v % 2 == 0:
        s.rect(26, 11, 7, 4, "olive_dark"); s.rect(33, 12, 7, 2, "grey_brown")         # ametralladora en el techo
    if v in (1, 4):
        s.vline(42, 6, 15, "grey_brown")                                               # antena
    _rueda(s, 12, 35, 6); _rueda(s, 36, 35, 6)
    _espejo(s)
    return _vehiculo_fin(s, v, 26, 42, 5, 25, camo=False)


def vamtac(v):
    """Contra MV4_vamtac_lateral_1/2: capó largo y bajo, cabina y caja abierta detrás (o la versión blindada con
    puesto de tirador); verde liso. Se dibuja con el morro a la izquierda y se
    gira: en la tarjeta mira a la derecha, como el Pizarro y el Dragón."""
    s = Sprite(48, 48)
    blindado = v % 2 == 0
    s.poly([(2, 33), (2, 25), (14, 23), (18, 17), (31, 17), (31, 24), (46, 24), (46, 33)], "olive_dark")
    s.poly([(15, 23), (18, 18), (22, 18), (22, 23)], "concrete")                       # parabrisas
    s.rect(24, 19, 5, 4, "concrete")
    s.hline(2, 13, 24, "olive")                                                         # capó con luz
    s.rect(3, 26, 2, 2, "beige")                                  # faro
    if blindado:
        s.rect(31, 17, 15, 8, "olive_dark"); s.hline(31, 45, 17, "olive")              # caja blindada
        s.rect(34, 19, 4, 3, "concrete")
        s.rect(22, 12, 8, 5, "olive_dark"); s.rect(14, 12, 8, 2, "grey_brown")         # escudo y ametralladora
    else:
        s.hline(31, 46, 23, "olive_light")                                              # borde de la caja abierta
        s.vline(46, 20, 24, "olive_dark"); s.vline(31, 17, 24, "olive_dark")
        if v % 3 == 1:
            s.vline(37, 13, 23, "grey_brown"); s.rect(33, 11, 10, 2, "grey_brown")     # lanzador en la caja
    for cx in (10, 38):
        s.ellipse(cx - 6, 28, cx + 6, 40, "grey_brown")
    _rueda(s, 10, 35, 5); _rueda(s, 38, 35, 5)
    _espejo(s)
    return _vehiculo_fin(s, v, 27, 40, 19, 26, camo=False)


def pizarro(v):
    s = Sprite(48, 48)
    s.poly([(2, 36), (2, 27), (8, 24), (38, 24), (46, 29), (46, 36)], "olive_dark")    # casco
    s.poly([(14, 24), (16, 17), (32, 17), (34, 24)], "olive_dark")                      # torre
    s.rect(32, 19, 14, 2, "grey_brown"); s.hline(32, 45, 19, "concrete")               # cañón de 30 mm
    s.rect(2, 33, 44, 8, "grey_brown")                                                  # oruga
    for x in range(7, 43, 6):                                                           # seis ruedas de rodadura (MV5)
        s.ellipse(x - 2, 35, x + 2, 39, "olive_grey")
    s.rect(3, 31, 42, 3, "olive_dark"); s.hline(3, 44, 33, "grey_brown")   # faldón
    if v % 2 == 0:
        s.rect(4, 26, 8, 3, "olive"); s.hline(4, 11, 26, "olive_light")                # cajones laterales
    if v in (1, 3, 5):
        s.vline(16, 9, 17, "grey_brown")
    return _vehiculo_fin(s, v, 28, 34, 22, 26)


def dragon(v):
    s = Sprite(48, 48)
    s.poly([(2, 34), (2, 25), (10, 23), (40, 23), (46, 28), (46, 34)], "olive_dark")
    s.poly([(17, 23), (19, 17), (31, 17), (33, 23)], "olive_dark")                      # torre del 30 mm
    s.rect(31, 18, 15, 2, "grey_brown"); s.hline(31, 45, 18, "concrete")
    s.hline(3, 45, 25, "olive")
    if v % 2 == 1:
        s.rect(21, 14, 5, 3, "grey_brown")                                              # optrónica
    for cx in (8, 18, 30, 40):                                                          # 8x8: cuatro ruedas por lado
        _rueda(s, cx, 36, 4)
    return _vehiculo_fin(s, v, 26, 40, 6, 27)


def _rotor(s, x0, x1, y, cubo):
    s.hline(x0, x1, y, "grey_brown"); s.hline(x0, x1, y - 1, "concrete")
    s.rect(cubo - 1, y, 3, 3, "grey_brown")


def cougar(v):
    s = Sprite(48, 48)
    s.poly([(4, 30), (6, 24), (12, 20), (30, 20), (34, 24), (34, 31), (10, 33)], "olive_dark")   # cabina
    s.poly([(34, 24), (46, 23), (46, 26), (34, 29)], "olive_dark")                               # cola
    s.rect(44, 18, 2, 8, "olive_dark")
    s.rect(7, 23, 6, 4, "concrete"); s.hline(7, 12, 23, "concrete_light")
    s.rect(16, 23, 4, 4, "concrete"); s.rect(22, 23, 4, 4, "concrete")
    _rotor(s, 2, 44, 16, 21)
    s.rect(26, 30, 7, 3, "olive_dark")                                                  # barquilla del tren
    s.vline(11, 33, 34, "grey_brown"); _rueda(s, 11, 36, 2)                             # rueda de morro (MV2: ruedas, no patines)
    s.vline(29, 33, 34, "grey_brown"); _rueda(s, 29, 36, 2)
    return _vehiculo_fin(s, v, 0, 0, 27, 25, arena=True)


def tigre(v):
    s = Sprite(48, 48)
    s.poly([(4, 30), (8, 22), (14, 20), (28, 21), (32, 25), (32, 30), (8, 32)], "olive_dark")
    s.poly([(32, 25), (46, 24), (46, 27), (32, 29)], "olive_dark")
    s.rect(44, 19, 2, 8, "olive_dark")
    s.poly([(9, 22), (12, 18), (16, 18), (16, 22)], "concrete")                         # cabina en tándem
    s.poly([(17, 21), (19, 17), (23, 17), (23, 21)], "concrete")
    s.rect(18, 14, 4, 3, "grey_brown")                                                  # visor en el mástil
    _rotor(s, 4, 40, 13, 20)
    s.rect(14, 30, 12, 3, "olive_grey"); s.rect(12, 33, 6, 2, "grey_brown"); s.rect(22, 33, 6, 2, "grey_brown")   # alas y lanzadores
    s.rect(5, 32, 4, 2, "grey_brown"); s.hline(2, 4, 33, "grey_brown")                 # cañón bajo el morro (MV7)
    s.vline(13, 33, 35, "grey_brown"); _rueda(s, 13, 37, 2)                             # tren fijo
    s.vline(27, 33, 35, "grey_brown"); _rueda(s, 27, 37, 2)
    s.vline(43, 27, 29, "grey_brown"); s.rect(42, 30, 3, 2, "grey_brown")               # rueda de cola
    return _vehiculo_fin(s, v, 0, 0, 34, 25, arena=True)


def nh90(v):
    s = Sprite(48, 48)
    s.poly([(3, 31), (5, 24), (11, 20), (33, 20), (36, 25), (36, 32), (8, 34)], "olive_dark")
    s.poly([(36, 24), (46, 22), (46, 26), (36, 30)], "olive_dark")
    s.rect(43, 17, 3, 9, "olive_dark")
    s.rect(6, 23, 6, 4, "concrete"); s.hline(6, 11, 23, "concrete_light")
    s.rect(16, 23, 9, 7, "grey_brown")                                                  # puerta lateral
    s.rect(17, 24, 7, 5, "olive_shadow" if False else "olive_grey")
    s.rect(20, 14, 10, 6, "olive_dark")                                                 # motores (la joroba)
    _rotor(s, 2, 44, 13, 25)
    _rueda(s, 12, 35, 2); _rueda(s, 30, 35, 2)
    return _vehiculo_fin(s, v, 0, 0, 27, 24)


def chinook(v):
    s = Sprite(48, 48)
    s.poly([(2, 31), (4, 24), (9, 21), (42, 21), (46, 24), (46, 32), (6, 33)], "olive_dark")
    s.poly([(38, 21), (40, 13), (46, 13), (46, 21)], "olive_dark")                      # pilón trasero
    s.rect(5, 23, 5, 4, "concrete"); s.hline(5, 9, 23, "concrete_light")
    for x in range(14, 36, 5):                                                          # ventanillas
        s.rect(x, 24, 2, 2, "concrete")
    _rotor(s, 0, 22, 17, 9); _rotor(s, 28, 47, 10, 43)                                  # rotores en tándem
    s.rect(8, 18, 3, 3, "grey_brown")
    _rueda(s, 10, 35, 2); _rueda(s, 36, 35, 2); _rueda(s, 41, 35, 2)
    return _vehiculo_fin(s, v, 0, 0, 22, 27, camo=False)                                # verde oliva liso (MV3)


VEHICULOS = {"lince": lince, "vamtac": vamtac, "pizarro": pizarro, "dragon": dragon, "cougar": cougar,
             "tigre": tigre, "nh90": nh90, "chinook": chinook}


def exportar(out):
    out = Path(out)
    todo = {}
    for n, fn in list(ESCUADRAS.items()) + list(VEHICULOS.items()):
        vs = [fn(v) for v in range(N_VAR)]
        hoja = Image.new("RGBA", (48 * N_VAR, 48), (0, 0, 0, 0))
        for v, s in enumerate(vs):
            hoja.alpha_composite(s.img, (48 * v, 0))
        hoja.save(out / f"ui_tarjeta_{n}_48.png")
        todo[n] = vs
    return todo


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_tarjetas")
    OUT.mkdir(parents=True, exist_ok=True)
    todo = exportar(OUT)
    E = 2
    filas = list(todo.items())
    hoja = Image.new("RGBA", (N_VAR * (48 * E + 6), len(filas) * (48 * E + 6)), PAL["navy"])
    for j, (n, vs) in enumerate(filas):
        for i, s in enumerate(vs):
            fondo = Image.new("RGBA", (48, 48), PAL["slate_dark"])
            hoja.paste(Image.alpha_composite(fondo, s.img).resize((48 * E, 48 * E), Image.NEAREST),
                       (i * (48 * E + 6), j * (48 * E + 6)))
    hoja.save(OUT / "contacto_tarjetas_x2.png")
    print("ok")
