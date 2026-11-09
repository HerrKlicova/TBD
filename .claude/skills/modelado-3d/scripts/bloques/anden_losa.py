"""Andén cubierto bajo losa de la estación de Castelló (Lucía, 8-nov 18:00, D-N156: Castellón sale infestada en el
capítulo 3). Foto RC3 de Paula (Estació de Castelló de la Plana, vies, Joanbanjo, CC BY-SA 3.0): estación bajo una losa
de hormigón con vigas de canto, pilares redondos claros sobre los andenes, andén de baldosa clara con la banda amarilla
y la de botones en el borde, y fluorescentes en las vigas. Teo (3), 8-nov.

Módulo de 10 m a lo largo de X (9,98 con holgura), como el kit de andenes. El borde de la vía está en −Y.
- CENTRAL (isla): andén de 4 m con borde a los dos lados; la losa cubre también las dos vías (ejes a ±4,5 m): 14 m.
- LATERAL: andén de 4 m con el borde en −Y y el muro de fondo alicatado en +Y (y 2,5–3); la losa va de y −2 a +3,
  empalmando con la central (Álvaro, 8-nov 18:02: central en Z 55–69 y lateral en Z 69–74, muro en Z 73,5–74).
Pilar redondo de Ø 0,9 m por módulo, viga de canto transversal sobre el pilar y longitudinal a lo largo del andén;
intradós a 6,5 m, losa de 1,5 m: la cara de arriba queda a +8 m sobre la vía, que es la cota 0 de la calle (vía a −8). Andén de 0,68 m. Medidas SIN VERIFICAR (sin planos; a ojo con la foto).
LA LOSA ES UN HIJO APARTE («Losa», con las vigas y los fluorescentes), con el pivote en el intradós (z 6,5): con la
cámara cenital Marcos la oculta o la funde, porque si no tapa todo el andén.
Que se lea como perdida sin rojo: escombro, hollín 605c48, papeles, maletas; nada en la banda de la horda.
TBD_VARIANTE = 5 · tipo + variante (0..9). Tipos: 0 Central, 1 Lateral. Variantes: 0 abandonada (papeles, un banco
volcado, una maleta); 1 falso techo caído (placas en el andén y un fluorescente colgando); 2 losa rota (hueco en la
losa con la ferralla, montón de escombro sobre el andén y la vía); 3 campamento abandonado (colchones, cartones,
mantas, una tienda hundida); 4 quemada (hollín en el pilar y el muro, restos calcinados).
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 10
TIPO, V = VAR // 5, VAR % 5
NOMBRES_T = ["Central", "Lateral"]
NOMBRES_V = ["Abandonada", "TechoCaido", "LosaRota", "Campamento", "Quemada"]
PIEZAS = ["anden", "costado", "banda", "botones", "pilar", "losa", "viga", "tubo", "muro", "zocalo", "escombro",
          "hollin", "papel", "trapo", "ferralla"]
COLORES = {"anden": "b8b8b8", "costado": "88885c", "banda": "f0dc9c", "botones": "6c7874", "pilar": "b4a080",
           "losa": "94948c", "viga": "88885c", "tubo": "94948c", "muro": "b8b8b8", "zocalo": "6c7874",
           "escombro": "94948c", "hollin": "605c48", "papel": "d4d0cc", "trapo": "6c7874", "ferralla": "6c7874"}
VALOR_PIEZAS = {"escombro": ("94948c", "88885c")}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (40, 3000), 1: (20, 1200)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
PARTES = ["anden", "Losa"]
ZI, EL, HA = 6.5, 1.5, 0.68           # intradós, canto de la losa, alto del andén
PIV = {"Losa": (0.0, 0.0, ZI)}
LX = 9.98


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_AndenLosa", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    v = V
    lat = TIPO == 1
    y0, y1 = -2.0, 2.0                               # andén
    ly0, ly1 = (-2.0, 3.0) if lat else (-7.0, 7.0)   # losa
    yp = 0.8 if lat else 0.0                         # pilar
    # ---- andén: losa de baldosa clara, costados, banda amarilla y de botones en el borde (o en los dos bordes)
    m.caja((0, 0, HA / 2), (LX, y1 - y0, HA), "costado")
    m.caja((0, 0, HA + 0.01), (LX - 0.01, y1 - y0 - 0.02, 0.02), "anden")
    bordes = (-1,) if lat else (-1, 1)
    for s in bordes:
        yb = s * 2.0
        m.caja((0, yb - s * 0.55, HA + 0.025), (LX - 0.02, 0.3 if fino else 0.34, 0.02), "banda")
        m.caja((0, yb - s * 0.95, HA + 0.025), (LX - 0.02, 0.4 if fino else 0.4, 0.02), "botones")
    # ---- pilar redondo (hollín en la quemada)
    hp = ZI - 0.7 - HA
    m.cilindro((0, yp, HA + hp / 2), 0.45, hp, "Z", 12 if fino else 8, "pilar")
    if v == 4:
        m.cilindro((0, yp, HA + hp * 0.35), 0.47, hp * 0.6, "Z", 12 if fino else 8, "hollin")
    if lat:                                          # muro de fondo alicatado con zócalo
        m.caja((0, 2.75, ZI / 2 + 0.1), (LX, 0.48, ZI + 0.2), "muro")      # se mete 0,2 m en la losa
        m.caja((0, 2.49, HA + 0.5), (LX, 0.04, 1.0), "zocalo")
        if v == 4:
            m.caja((-1.5, 2.48, HA + 2.0), (4.0, 0.03, 3.4), "hollin")
    _suelo(m, fino, v, lat)
    # ---- losa (hijo aparte), vigas y fluorescentes
    if os.environ.get("TBD_SIN_LOSA") == "1":        # solo para las hojas: el andén sin la losa encima
        return m.objeto(nombre)
    m.parte("Losa")
    yc, ly = (ly0 + ly1) / 2, ly1 - ly0
    if v == 2:                                       # hueco en la losa: cuatro trozos alrededor y ferralla
        hx0, hx1, hy0, hy1 = (-3.2, 2.2, -1.4, 2.0) if lat else (-3.2, 2.2, -6.0, -1.6)
        for (xa, xb, ya, yb) in ((-LX / 2, hx0, ly0, ly1), (hx1, LX / 2, ly0, ly1), (hx0 - 0.05, hx1 + 0.05, ly0, hy0),
                                 (hx0 - 0.05, hx1 + 0.05, hy1, ly1)):
            m.caja(((xa + xb) / 2, (ya + yb) / 2, ZI + EL / 2), (xb - xa, yb - ya, EL), "losa")
        if fino:
            for k in range(6):
                x = hx0 + 0.4 + k * 0.9
                m.caja((x, hy1 - 0.4, ZI + 0.1), (0.04, 0.9, 0.04), "ferralla", rot_x=-0.6)
                m.caja((x + 0.2, hy0 + 0.3, ZI + 0.15), (0.04, 0.7, 0.04), "ferralla", rot_x=0.5)
        else:
            m.caja((hx0 + 2.5, hy1 - 0.3, ZI), (4.4, 0.6, 0.34), "ferralla", rot_x=-0.5)
    else:
        m.caja((0, yc, ZI + EL / 2), (LX, ly, EL), "losa")
    m.caja((0, yc, ZI - 0.35), (0.7, ly - 0.02, 0.7), "viga")              # viga de canto sobre el pilar
    m.caja((0, yp, ZI - 0.25), (LX - 0.02, 0.9, 0.5), "viga")              # viga longitudinal sobre el andén
    for s in bordes:                                                        # fluorescentes a lo largo del borde
        yt = s * 1.4
        if v == 1:                                   # uno colgando de un lado
            m.caja((-2.6, yt, ZI - 1.1), (2.4, 0.2 if fino else 0.34, 0.12 if fino else 0.34), "tubo", rot_y=0.5)
            m.caja((2.6, yt, ZI - 0.1), (2.4, 0.2 if fino else 0.34, 0.12 if fino else 0.34), "tubo")
        else:
            for x in (-2.6, 2.6):
                m.caja((x, yt, ZI - 0.1), (2.4, 0.2 if fino else 0.34, 0.12 if fino else 0.34), "tubo")
    return m.objeto(nombre)


def _suelo(m, fino, v, lat):
    """Lo que hay en el andén: lo que dice que se perdió."""
    z = HA + 0.02
    if v == 0:
        m.caja((-3.0, 0.9, z + 0.25), (1.6, 0.4, 0.08), "trapo", rot_x=1.3)        # banco volcado
        m.caja((-3.0, 0.7, z + 0.1), (1.6, 0.08, 0.2), "zocalo")
        m.caja((2.8, -0.4, z + 0.18), (0.7, 0.45, 0.36), "hollin", rot_z=0.4)      # maleta
        if fino:
            for k in range(7):
                m.caja((-4 + _h(k) * 8, -1.6 + _h(k, 1) * 3.2, z + 0.005), (0.3, 0.22, 0.01), "papel",
                       rot_z=_h(k, 2) * 3)
    elif v == 1:                                     # placas del falso techo en el suelo
        for k in range(6 if fino else 3):
            m.caja((-3.8 + k * 1.4 * (1 if fino else 2), -0.8 + _h(k, 3) * 1.6, z + 0.03), (1.2, 0.6, 0.04),
                   "escombro", rot_z=_h(k, 4) * 2, rot_y=0.05)
        m.caja((1.5, 1.0, z + 0.2), (0.9, 0.7, 0.4), "escombro", rot_z=0.3)
    elif v == 2:                                     # montón de escombro bajo el hueco, sobre el andén y la vía
        m.caja((-0.5, -1.2, z + 0.45), (4.2, 2.6, 0.9), "escombro", rot_z=0.15)
        m.caja((-1.2, -3.6, 0.5), (3.6, 2.4, 1.0), "escombro", rot_z=-0.2)
        m.caja((0.6, -4.6, 0.3), (2.0, 1.6, 0.6), "escombro", rot_z=0.5)
        for k in range(4 if fino else 2):
            m.caja((-2.4 + k * 1.3, -2.4 + _h(k, 5), z + 0.95), (0.9, 0.7, 0.4), "escombro", rot_z=_h(k, 6) * 2,
                   rot_y=0.3)
    elif v == 3:                                     # campamento abandonado
        for k, (x, y) in enumerate(((-3.6, 0.6), (-1.6, 0.9), (2.0, 0.7))):
            m.caja((x, y, z + 0.08), (1.9, 0.9, 0.16), "trapo", rot_z=0.1 * k)      # colchones
        m.caja((3.8, 0.4, z + 0.5), (1.6, 1.4, 1.0), "zocalo", rot_z=0.2, rot_y=0.25)  # tienda hundida
        m.caja((-0.2, -0.6, z + 0.15), (0.6, 0.4, 0.3), "costado", rot_z=0.6)        # cartones y maleta
        m.caja((0.6, -0.9, z + 0.18), (0.7, 0.45, 0.36), "hollin", rot_z=1.0)
        if fino:
            for k in range(5):
                m.caja((-4 + _h(k, 7) * 8, -1.5 + _h(k, 8) * 1.2, z + 0.005), (0.3, 0.22, 0.01), "papel",
                       rot_z=_h(k, 9) * 3)
    else:                                            # quemada: restos calcinados
        for k in range(5 if fino else 3):
            m.caja((-3.5 + k * 1.7 * (1 if fino else 1.5), -0.5 + _h(k, 10) * 1.4, z + 0.12), (1.0, 0.6, 0.24),
                   "hollin", rot_z=_h(k, 11) * 2)
        m.caja((0.4, 1.0, z + 0.03), (3.0, 1.4, 0.04), "hollin", rot_z=0.2)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
