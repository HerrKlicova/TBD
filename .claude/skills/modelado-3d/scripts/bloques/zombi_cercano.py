"""Zombi de la Colmena para la vista cercana (encargo de Lucía, análisis del 6-nov, punto 8). Teo, 6-nov-2026.

Referencias: el sprite aprobado de Bruno (pixel-codigo/zombi, variante «piedra/oliva») para colores y pose, y el
análisis de Lucía (docs/arte/analisis-referencias-6nov.md, secciones 1 y 3):
- proporción alargada, unos 3:1 de alto a ancho: piernas largas y torso estrecho;
- brazos adelantados en la pose base, desparejos (el derecho más bajo, como en el sprite), porque es lo que dice
  «zombi» y no «soldado» por encima de 12-16 px;
- cabeza algo grande (0,30 m) y adelantada: va encorvado;
- pocas caras y sin bisel: pensado para instanciado masivo en DOTS. Sin Hunyuan.
Medidas: 1,75 m de alto, 0,58 m de hombro a hombro con los brazos. Delante = -Y, Z arriba, metros.
Sin sangre ni gore (regla 11): el roto de la camisa y la tierra van en grises y oliva.
"""
import math
from taller import Taller

PIEZAS = ["camisa", "piel", "pelo", "pantalon", "calzado", "roto"]
COLORES = {
    "camisa": "94948c",    # piedra (concrete_light), la camisa de los zombis según Lucía
    "piel": "b8b8b8",      # piel pálida (grey_light), como el sprite
    "pelo": "303424",      # olive_shadow: separa la cabeza de cualquier camisa
    "pantalon": "605c48",  # olive_grey (variante «oliva»)
    "calzado": "303424",
    "roto": "60546c",      # roto y tierra de la camisa en purple_grey (sin rojos)
}
# valor partido por pieza (D-N57): lo que mira arriba claro, costados un escalón abajo, como el sprite de Bruno
VALOR_PIEZAS = {"camisa": ("94948c", "60546c"), "piel": ("b8b8b8", "6c7874")}
FACCION = "zombi"          # sin rim (regla 3 de Iris: el rim solo en unidades propias)
PRESUPUESTO = {0: (100, 600), 1: (60, 300)}
UMBRAL_CONTORNO = {1: 9.0}   # LOD1: contorno solo en la silueta (Bruno): a 20 px las líneas interiores lo ensucian   # instanciado masivo: el mínimo de 1500 de los vehículos no aplica


def construir(nombre="SM_Zombi_Cercano", lod=0):
    """Medidas ajustadas a la lámina de siluetas de Bruno (16 y 20 px; a 20 px, 1 px = 8,75 cm): cabeza de 3 px
    adelantada 1 px, torso de 3 px, cadera al 55 % de la altura, brazos a la altura del hombro de 2 px de grueso y
    6 px de largo con la mano caída 1 px, y piernas abiertas en el paso."""
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    cadera = 0.96                                   # 55 % de 1,75 m
    # piernas abiertas en el paso, en V invertida desde la cadera (lámina de Bruno): la izquierda adelante
    largo = cadera - 0.04
    for x, ang in ((-0.1, 14), (0.1, -14)):
        r = math.radians(ang)
        g = 0.15 if lod == 0 else 0.19   # LOD1: 2 px enteros a 20 px; con 0,15 el contorno se come la pierna
        t.caja((x, -largo / 2 * math.sin(r), cadera - largo / 2 * math.cos(r)), (g, g, largo), "pantalon", rot_x=-r)
        t.caja((x, -largo * math.sin(r) - 0.04, 0.04), (g, 0.26, 0.08), "calzado")
    # torso de 3 px de fondo, estrecho e inclinado hacia delante (encorvado)
    enc = math.radians(10)
    t.caja((0.0, -0.05, 1.2), (0.3, 0.26, 0.56), "camisa", rot_x=enc)
    # brazos al frente a la altura del hombro (6 px = 0,52 m, 2 px = 0,17 m), desparejos: el derecho algo más caído
    for x, z, caida in ((-0.225, 1.4, 3), (0.225, 1.36, 9)):
        r = math.radians(caida)
        largo = 0.52
        y0 = -0.1
        t.caja((x, y0 - largo / 2 * math.cos(r), z - largo / 2 * math.sin(r)), (0.15, largo, 0.15), "camisa", rot_x=r)
        # mano caída 1 px al final del brazo
        hy, hz = y0 - (largo + 0.05) * math.cos(r), z - (largo + 0.05) * math.sin(r) - 0.06
        t.caja((x, hy, hz), (0.12, 0.12, 0.14), "piel", rot_x=r)
    # cabeza de 3 px (0,27 m) adelantada 1 px sobre un cuello corto
    t.caja((0.0, -0.13, 1.5), (0.12, 0.12, 0.1), "piel")
    t.caja((0.0, -0.17, 1.615), (0.27, 0.27, 0.27), "piel")
    # pelo ralo: casquete encima y detrás (desde arriba se ve sobre todo pelo, como en el sprite)
    t.caja((0.0, -0.09, 1.76), (0.28, 0.14, 0.03), "pelo")   # mitad de atrás: la coronilla clara asoma (calva)
    t.caja((0.0, -0.03, 1.65), (0.28, 0.02, 0.18), "pelo")
    # LOD0: roto de la camisa en la espalda y mancha de tierra delante (placas finas, fuera del LOD1)
    fino.caja((0.08, 0.11, 1.2), (0.14, 0.02, 0.16), "roto", rot_x=enc)
    fino.caja((-0.08, -0.2, 1.1), (0.12, 0.02, 0.1), "roto", rot_x=enc)
    return t.objeto(nombre, bisel=0)
