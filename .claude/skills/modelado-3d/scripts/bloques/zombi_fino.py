"""Zombi de la Colmena, variante FINA (Eugenio, 6-nov 13:07: el zombi de cerca se ve «muy gordo, muy nítido»).

Misma pose, paleta y reglas que zombi_cercano.py (aprobado por Lucía), pero con la silueta más delgada y estilizada:
torso de 0,22 m (antes 0,30), brazos de 0,08 m (antes 0,15), piernas de 0,10 m (antes 0,15) y cabeza de 0,22 m (antes
0,27). Medidas 1,75 x ~0,40 m: proporción ~4,4:1. Sin las placas del roto: menos triángulos que la variante gruesa.
Pensado para verse pixelado por el shader de Unity: las extremidades del LOD1 quedan en ~1 px a 20 px y la figura se lee
como un trazo oscuro y fino con los brazos al frente. Delante = -Y en Blender (+Z en Unity).
"""
import math, os
from taller import Taller
from zombi_cercano import PIEZAS, CAMISAS, COLORES, COLORES_LOD1, VALOR_PIEZAS, FACCION, LEGIBILIDAD, UMBRAL_CONTORNO, \
    MASCARA_COLOR

PRESUPUESTO = {0: (60, 400), 1: (60, 300)}
PIEZAS = PIEZAS + ["objeto"]
COLORES = dict(COLORES, objeto="442c0c")   # mochila, gorra, falda: un escalón por encima del cuerpo, nunca claro
# Variantes de silueta (Eugenio, 6-nov: «son personas, tienen ropas y cosas»). TBD_VARIANTE: 0 base, 1 mochila,
# 2 gorra, 3 chaqueta abierta con faldones, 4 falda o vestido, 5 un brazo caído. Combinadas con las 3 camisas por
# instancia salen 18 personas distintas con 6 mallas: 6 lotes de instanciado, nada más.
# Complexiones (Lucía, 6-nov 17:03: a 20 px la variedad que se nota es la de silueta): 6 corpulento (el zombi grueso
# de zombi_cercano.py, aprobado antes) y 7 bajo (1,60 m). La escala de ±5 % en altura por instancia es de Unity.
# Nada de niños.
# Ampliación (Lucía, 8-nov 17:49, D-N80 en el bloque B1: ropa, objetos y silueta). Todas cambian la silueta, que es lo
# que se lee a 20 px: 8 sudadera con capucha; 9 abrigo largo hasta la rodilla (tiñe con la camisa); 10 casco y chaleco
# (soldado o guardia caído, en el escalón del objeto, sin colores propios); 11 bolsa colgando de la mano del brazo caído;
# 12 cojo, ladeado 9° y con una pierna arrastrada; 13 reptante (sin fuerza en las piernas, a 0,45 m del suelo);
# 14 anciano muy encorvado con bastón; 15 labrador de la huerta con sombrero de ala ancha.
VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
# Los objetos (falda, mochila, gorra, casco, bolsa, bastón, sombrero) tiñen con el pantalón (canal G). Antes solo la
# falda; los demás se quedaban en 442c0c (L* ≈ 20), que de noche, sobre un suelo a L* ≈ 25, se pierde en el suelo y le
# come la silueta al zombi justo donde está la variedad. Con el tinte de Bruno se quedan en la banda L* 9–13 (D-N80).
MASCARA_COLOR = dict(MASCARA_COLOR, objeto=(0, 1, 0))


def construir(nombre="SM_Zombi_Fino", lod=0):
    if VAR == 6:   # corpulento: la malla gruesa aprobada, con las mismas piezas de material
        import zombi_cercano
        return zombi_cercano.construir(nombre, lod)
    obj = _reptante(nombre, lod) if VAR == 13 else _fino(nombre, lod)
    from mathutils import Matrix
    if VAR == 7:   # bajo: 1,60 m (factor 0,914 en altura) y un poco más ancho para que se lea como otra persona
        obj.data.transform(Matrix.Diagonal((1.05, 1.0, 1.60 / 1.75, 1.0)))
    if VAR == 12:  # cojo: todo el cuerpo ladeado hacia la pierna buena
        obj.data.transform(Matrix.Rotation(math.radians(9), 4, "Y"))
    obj.data.update()
    return obj


def _fino(nombre, lod):
    t = Taller(PIEZAS)
    cadera = 0.96
    largo = cadera - 0.04
    g = 0.10 if lod == 0 else 0.12
    piernas = [(-0.065, 14, largo), (0.065, -14, largo)]   # piernas en V invertida (lámina de Bruno)
    if VAR == 12:   # la pierna mala arrastrada hacia atrás
        piernas = [(-0.065, 6, largo), (0.07, -32, cadera / math.cos(math.radians(32)) - 0.07)]
    for x, ang, lp in piernas:
        r = math.radians(ang)
        t.caja((x, -lp / 2 * math.sin(r), cadera - lp / 2 * math.cos(r)), (g, g, lp), "pantalon", rot_x=-r)
        t.caja((x, -lp * math.sin(r) - 0.03, 0.03), (g, 0.2, 0.06), "calzado")
    # encorvado: el anciano mucho más; la cabeza y los brazos bajan y se adelantan con el torso
    enc = math.radians(42 if VAR == 14 else 12)   # algo más encorvado: la silueta fina lo necesita para leerse
    dy, dz = (-0.3, -0.26) if VAR == 14 else (0.0, 0.0)
    t.caja((0.0, -0.05 + dy / 2, 1.21 + dz / 3), (0.22, 0.17, 0.54), "camisa", rot_x=enc)
    a = 0.08 if lod == 0 else 0.10
    brazos = ((-0.15, 1.4, 3), (0.15, 1.35, 10))
    if VAR in (5, 11):   # un brazo caído a lo largo del cuerpo y el otro al frente
        brazos = ((-0.15, 1.4, 3), (0.15, 1.35, 80))
    if VAR == 14:        # el anciano: un brazo al frente y el otro en el bastón
        brazos = ((-0.15, 1.4, 8), (0.15, 1.35, 62))
    for x, z, caida in brazos:   # brazos al frente, desparejos
        r = math.radians(caida)
        lb, y0 = 0.5, -0.1 + dy
        z += dz
        t.caja((x, y0 - lb / 2 * math.cos(r), z - lb / 2 * math.sin(r)), (a, lb, a), "camisa", rot_x=r)
        hy, hz = y0 - (lb + 0.04) * math.cos(r), z - (lb + 0.04) * math.sin(r) - 0.05
        t.caja((x, hy, hz), (0.08, 0.09, 0.11), "piel", rot_x=r)
        if VAR == 11 and caida > 45:   # bolsa de la compra colgando de la mano
            t.caja((x + 0.03, hy, hz - 0.22), (0.07, 0.3, 0.32), "objeto")
        if VAR == 14 and caida > 45:   # bastón hasta el suelo, algo adelantado
            t.caja((x + 0.02, hy - 0.06, hz / 2), (0.035 if lod == 0 else 0.06, 0.035 if lod == 0 else 0.06, hz),
                   "objeto", rot_x=0.12)
    t.caja((0.0, -0.13 + dy, 1.5 + dz), (0.08, 0.08, 0.1), "piel")              # cuello
    t.caja((0.0, -0.17 + dy, 1.64 + dz), (0.22, 0.22, 0.22), "piel")            # cabeza adelantada
    if VAR != 8:
        t.caja((0.0, -0.11 + dy, 1.765 + dz), (0.23, 0.12, 0.03), "pelo")
        t.caja((0.0, -0.05 + dy, 1.67 + dz), (0.23, 0.02, 0.15), "pelo")
        t.caja((0.0, -0.04 + dy, 1.71 + dz), (0.1, 0.02, 0.08), "nuca")         # luz de 1 px en la nuca (Lucía)
    if VAR == 1:   # mochila a la espalda
        t.caja((0.0, 0.1, 1.22), (0.26, 0.16, 0.38), "objeto", rot_x=enc)
    if VAR == 2:   # gorra con visera adelantada
        t.caja((0.0, -0.17, 1.765), (0.24, 0.24, 0.05), "objeto")
        t.caja((0.0, -0.32, 1.75), (0.18, 0.12, 0.02), "objeto")
    if VAR == 3:   # chaqueta abierta: faldones abiertos hacia los lados (Lucía: a 20 px tienen que ensanchar la cadera)
        for x, ry in ((-0.19, 38), (0.19, -38)):
            t.caja((x, -0.06, 0.92), (0.14, 0.05, 0.4), "camisa", rot_x=enc, rot_y=math.radians(ry))
    if VAR == 4:   # falda o vestido hasta la rodilla (tronco de cono)
        t.cilindro((0.0, 0.0, 0.72), 0.19, 0.5, "Z", 6, "objeto", r2=0.13)
    if VAR == 8:   # capucha puesta, que tapa el pelo y abulta la nuca (con la camisa: es la sudadera)
        t.caja((0.0, -0.13, 1.66), (0.27, 0.27, 0.27), "camisa")
        t.caja((0.0, 0.01, 1.52), (0.24, 0.1, 0.16), "camisa", rot_x=0.4)
        t.caja((0.0, -0.04, 1.71), (0.1, 0.02, 0.08), "nuca")
    if VAR == 9:   # abrigo largo hasta la rodilla, abierto abajo: ensancha la silueta de la cadera para abajo
        t.cilindro((0.0, -0.02, 0.8), 0.2, 0.62, "Z", 6, "camisa", r2=0.15)
        t.caja((0.0, -0.05, 1.21), (0.27, 0.21, 0.5), "camisa", rot_x=enc)
    if VAR == 10:  # casco redondo y chaleco abultado
        t.esfera((0.0, -0.16, 1.73), (0.16, 0.16, 0.1), "objeto", 1)
        t.caja((0.0, -0.06, 1.22), (0.28, 0.24, 0.36), "objeto", rot_x=enc)
    if VAR == 15:  # sombrero de ala ancha y copa baja
        t.cilindro((0.0, -0.17, 1.775), 0.24, 0.025, "Z", 8, "objeto")
        t.cilindro((0.0, -0.17, 1.81), 0.12, 0.08, "Z", 8, "objeto", r2=0.11)
    return t.objeto(nombre, bisel=0)


def _reptante(nombre, lod):
    """Se arrastra con los brazos: torso casi tumbado, cabeza levantada y piernas a rastras (alto ~0,45 m)."""
    t = Taller(PIEZAS)
    g = 0.10 if lod == 0 else 0.12
    a = 0.08 if lod == 0 else 0.10
    t.caja((0.0, 0.0, 0.26), (0.24, 0.56, 0.2), "camisa", rot_x=math.radians(-14))
    t.caja((0.0, -0.33, 0.36), (0.08, 0.1, 0.08), "piel")
    t.caja((0.0, -0.42, 0.4), (0.22, 0.22, 0.22), "piel")
    t.caja((0.0, -0.4, 0.52), (0.23, 0.18, 0.03), "pelo")
    t.caja((0.0, -0.3, 0.45), (0.1, 0.02, 0.08), "nuca")
    for x, alc in ((-0.17, 0.62), (0.17, 0.48)):        # brazos tendidos hacia delante, apoyados en el suelo
        t.caja((x, -0.2 - alc / 2, 0.2), (a, alc, a), "camisa", rot_x=math.radians(18))
        t.caja((x, -0.22 - alc, 0.06), (0.09, 0.11, 0.08), "piel")
    for x, rz in ((-0.07, 0.12), (0.08, -0.25)):        # piernas a rastras
        t.caja((x + 0.0, 0.7, 0.1), (g, 0.86, g), "pantalon", rot_z=rz)
        t.caja((x - math.sin(rz) * 0.45, 1.14, 0.06), (g, 0.08, 0.2), "calzado", rot_z=rz)
    return t.objeto(nombre, bisel=0)
