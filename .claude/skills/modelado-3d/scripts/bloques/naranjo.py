"""Naranjo para las hileras de detrás de las casas (Lucía, ambientacion-sagunto.md, pieza 6). Teo, 6-nov.

v2 (6-nov 17:10): Eugenio vio la v1 y dijo «parece una piedra». La v1 tenía la copa de conos apilados pegada al
suelo, el tronco escondido debajo y las naranjas solo en el LOD0 (el juego carga el LOD1, así que no salían). Ahora:
- tronco marrón 543820 bien visible: la copa arranca a 1,1 m y el tronco se abre en dos ramas que entran en la copa;
- copa redondeada hecha de grumos (icosferas que se cruzan), con la silueta ondulada de un árbol de hojas;
- naranjas en a86c50 (el naranja más cercano permitido: e08c30 es el ámbar de los avisos), de 0,28 m para que lleguen
  a ~2 px a 0,17 m/px (regla de Bruno), también en el LOD1.
v3 (17:25) con las fotos de Paula (E10_naranjo_1..3, Commons CC BY-SA) y E10_naranjo_medidas.md: horquilla a ~1 m
(Wikipedia) y copa más ancha que alta (en el modelo, unos 3,6 m de copa y 3,7 m de alto: más estrecha que la foto para que el tronco se vea a 55°). Altura de 3 a 5 m VERIFICADA (Wikipedia); el
diámetro de la copa queda SIN VERIFICAR (estimado a ojo en la foto 1; el único dato es de Venezuela, de 4 a 4,7 m).
"""
import math, os, random
from taller import Taller

# Variantes (Eugenio, 6-nov): TBD_VARIANTE=1..5; 0 = base. Escala, copa más alta o más ancha, inclinación, poda y
# cantidad de fruta.
VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)

PIEZAS = ["copa", "tronco", "fruta"]
COLORES = {"copa": "70784c", "tronco": "543820", "fruta": "a86c50"}
# Lucía (16:55): la copa no puede caer en el valor de la horda (1c200c/303424): arriba 88885c, costados 70784c
VALOR_PIEZAS = {"copa": ("88885c", "70784c", "50541c")}   # abajo 50541c: sombra de copa sin caer en 303424
FACCION = "neutro"
PRESUPUESTO = {0: (200, 1400), 1: (100, 1100)}   # unos cientos de árboles por mapa, no miles


def construir(nombre="SM_Naranjo", lod=0):
    t = Taller(PIEZAS)
    r = random.Random(3000 + VAR)
    k = (1.0, 0.85, 1.12, 0.95, 1.05, 0.9)[VAR % 6]          # escala general
    ach = (1.0, 1.12, 0.9, 1.0, 0.85, 1.1)[VAR % 6]          # >1 copa más alta, <1 más ancha
    dx, dy = ((0, 0), (0.18, 0), (0, -0.22), (-0.12, 0.1), (0.22, 0.16), (0, 0))[VAR % 6]   # inclinación
    # Fotos de Paula (E10_naranjo_1 y 3): tronco corto que se abre en ramas a ~1 m (Wikipedia, «a un metro del suelo»)
    # y copa MÁS ANCHA QUE ALTA. H ensancha la copa y V la aplasta respecto a la v2.
    H, V = 1.05, 0.88   # compromiso: la foto pide más ancho, pero a 55° una copa más ancha tapa el tronco (Eugenio lo quiere a la vista)
    horq = 1.05 * k                                           # horquilla: el tronco se abre aquí
    base = 1.5 * k                                             # la copa arranca aquí: el tronco se ve debajo
    # tronco de 0,36 m (2 px) y dos ramas en V abiertas que entran en la copa
    t.cilindro((0, 0, horq / 2 + 0.05), 0.18 * k, horq + 0.1, "Z", 6, "tronco", r2=0.15 * k)
    for sx in (-1, 1):
        t.caja((sx * 0.25 * k + dx * 0.5, dy * 0.5, horq + 0.35 * k), (0.15 * k, 0.15 * k, 0.85 * k), "tronco",
               rot_y=math.radians(28 * sx))
    # copa: un grumo central y una corona de grumos alrededor y arriba
    sub_g = 2
    sub_p = 2
    cz = base + 1.25 * k * ach * V
    cx, cy = dx * 1.5, dy * 1.5
    t.esfera((cx, cy, cz), (1.3 * k * H, 1.3 * k * H, 1.15 * k * ach * V), "copa", sub_g)
    n = (6, 5, 7, 4, 7, 6)[VAR % 6]
    if VAR == 3:        # poda reciente: copa más pequeña y abierta, se ve más tronco
        cz -= 0.1
    for i in range(n):
        a = i * 2 * math.pi / n + r.uniform(-0.25, 0.25)
        rr = 1.05 * k * H
        zz = cz + r.uniform(-0.5, 0.2) * k * ach * V   # los grumos de fuera cuelgan un poco, como en la foto
        rg = r.uniform(0.62, 0.8) * k
        t.esfera((cx + rr * math.cos(a), cy + rr * math.sin(a), zz), (rg * 1.1, rg * 1.1, rg * 0.85 * ach), "copa", sub_p)
    t.esfera((cx + 0.1, cy - 0.1, cz + 0.85 * k * ach * V), (0.85 * k * H, 0.85 * k * H, 0.55 * k * ach), "copa", sub_p)
    # naranjas: en la superficie de la copa, medio asomadas, sobre todo en los costados (lo que ve la cámara a 55°)
    nf = (20, 14, 24, 8, 22, 16)[VAR % 6]   # la foto 1 va cargada de fruta
    for i in range(nf):
        a = i * 2 * math.pi / nf + r.uniform(0, 0.35)
        el = r.uniform(-0.2, 0.75)                        # elevación sobre el ecuador de la copa
        R = 1.5 * k * H                                      # medio hundidas en los grumos, no flotando
        x = cx + R * math.cos(a) * math.cos(el)
        y = cy + R * math.sin(a) * math.cos(el)
        z = cz + 1.05 * k * ach * V * math.sin(el)
        t.caja((x, y, z), (0.28, 0.28, 0.28), "fruta", rot_z=a)
    return t.objeto(nombre, bisel=0)
