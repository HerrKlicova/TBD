"""R1 · Locomotora diésel blindada: Renfe 333.3 blindada de forma improvisada por el taller de Sagunto (Lucía, 6-nov).

Medidas (Wikipedia EN, «Renfe Class 333», subserie 333.3): 22,33 m de largo, 3,16 m de ancho y 4,307 m de alto; Co'Co'.
De las fotos de Paula (R1_renfe333_1.jpg lateral, CC BY-SA 3.0, Galandil; R1_renfe333_2.jpg 3/4 delantero, CC BY-SA 2.0,
Nelso Silva): dos cabinas con parabrisas inclinado, caja de anchura completa con el canto del techo achaflanado, gran
panel de rejillas horizontales en el centro del costado, rejillas y ventiladores en el techo, depósito entre bogies,
topes y enganche, faros bajos en el frontal y juntas verticales de la caja.
SIN VERIFICAR (estimado de las fotos): centros de bogie a ±7,25 m, ejes a 1,9 m dentro del bogie, rueda de 1,07 m, alto
de la caja 4,05 m (el resto hasta 4,307 son los equipos del techo), bastidor a 0,95-1,25 m.
Blindaje improvisado (encargo de Lucía): chapas soldadas con mirilla sobre las ventanas laterales, rejilla en el
parabrisas, sacos terreros y arado frontal para apartar cuerpos (§5.7). Colores D-N42: techo b8b8b8, laterales 50541c y
franja ámbar e08c30 en el frontal (el único ámbar permitido en un modelo). Sin logotipos ni rótulos de Renfe.
Los «sacos en los pasillos»: la 333 no tiene pasillos exteriores, así que van en el techo, en nidos junto a las cabinas.
"""
import math
from taller import Taller

PIEZAS = ["casco", "bastidor", "rueda", "optica", "foco", "franja", "chapa", "rejilla", "metal", "sacos", "arado"]
COLORES = {
    "casco": "50541c",     # laterales del tren (D-N42); el techo pasa a b8b8b8 con valor_partido(claro="b8b8b8")
    "bastidor": "303424",  # bastidor, bogies y depósito: el gris más oscuro sin caer al negro del contorno
    "rueda": "1c200c",
    "optica": "181c30",    # cristales y mirillas
    "foco": "f0dc9c",
    "franja": "e08c30",    # franja ámbar del frontal: dice hacia dónde va el tren (única excepción al ámbar)
    "chapa": "605c48",     # chapas soldadas encima: otro acero, 4,6 de L* sobre el lateral
    "rejilla": "303424",   # panel de rejillas del costado (lateral 1: la mitad central de la caja)
    "metal": "1c200c",     # barrotes de la rejilla del parabrisas, topes, tubos de escape
    "sacos": "847c34",
    "arado": "6c7874",     # acero del arado, más claro para que la cuña se lea delante
}
L = 22.33
Y0 = L / 2
Z_CAJA = 4.05


def _semiancho(z):
    if z <= 3.6:
        return 1.58
    return 1.58 + (1.25 - 1.58) * (z - 3.6) / (Z_CAJA - 3.6)


COSTURAS = [((0, y, 0), (0, 1, 0)) for y in (-8.2, -5.6, 5.6, 8.2)]  # juntas de cabina y caja (lateral 1)


def construir(nombre="SM_R1_Locomotora", lod=0):
    """lod=0 de cerca; lod=1 tamaño de juego: solo piezas de 0,4 m o más y sin placas planas del techo."""
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    a = math.radians(24)   # inclinación del parabrisas medida en el 3/4 delantero
    P = [(-Y0, 1.25), (-Y0, 2.6), (-Y0 + 0.45, 3.6), (-Y0 + 0.95, Z_CAJA), (Y0 - 0.95, Z_CAJA), (Y0 - 0.45, 3.6),
         (Y0, 2.6), (Y0, 1.25)]
    t.perfil_extruido(P, _semiancho, cortes_z=(3.6,))
    if lod == 0:
        t.costuras(COSTURAS)
    # --- bastidor, topes, depósito y bogies Co'Co'
    t.caja((0, 0, 1.1), (2.9, L - 0.2, 0.3), "bastidor")
    t.caja((0, 0, 0.72), (2.5, 7.0, 0.5), "bastidor")                     # depósito entre bogies
    for s in (-1, 1):
        for x in (-0.9, 0.9):
            t.cilindro((x, s * (Y0 + 0.2), 1.06), 0.2, 0.4, "Y", 8, "metal")   # topes
        yb = s * 7.25
        for x in (-1.08, 1.08):
            t.caja((x, yb, 0.72), (0.22, 4.6, 0.45), "bastidor")          # largueros del bogie
        for e in (-1.9, 0.0, 1.9):
            for x in (-0.83, 0.83):
                t.cilindro((x, yb + e, 0.535), 0.535, 0.14, "X", 12, "rueda")
            for x in (-1.12, 1.12):
                fino.cilindro((x, yb + e, 0.95), 0.12, 0.3, "Z", 6, "metal")    # muelles
    # --- frontales (los dos): franja ámbar, faros, parabrisas con rejilla
    for s in (-1, 1):
        yf = s * (Y0 + 0.02)
        t.caja((0, yf, 2.15), (3.0, 0.04, 0.24), "franja")
        for x in (-1.05, 1.05):
            t.caja((x, s * (Y0 + 0.03), 1.72), (0.26, 0.04, 0.18), "foco")
        # parabrisas sobre la cara inclinada
        yc, zc = s * (Y0 - 0.25), 3.1
        t.caja((0, yc + s * 0.03, zc), (2.6, 0.03, 1.0), "optica", rot_x=-s * a)
        for k in range(3):
            t.caja(((k - 1) * 0.8, yc + s * 0.07, zc), (0.12, 0.05, 1.06), "metal", rot_x=-s * a)
        t.caja((0, yc + s * 0.07, zc), (2.6, 0.05, 0.12), "metal", rot_x=-s * a)
        # chapas soldadas con mirilla sobre las ventanas de cabina y las puertas
        for x in (-1.0, 1.0):
            for dy, alto in ((1.6, 0.8), (2.5, 1.6)):
                y = s * (Y0 - dy)
                zz = 2.95 if alto < 1 else 2.45
                t.caja((x * 1.6, y, zz), (0.06, 0.8, alto), "chapa")
                t.caja((x * 1.635, y, 3.05), (0.02, 0.55, 0.07), "optica")
        # nido de sacos en el techo junto a la cabina
        for x in (-1.0, 1.0):
            t.caja((x, s * (Y0 - 3.0), Z_CAJA + 0.15), (0.35, 1.6, 0.3), "sacos")
        t.caja((0, s * (Y0 - 3.75), Z_CAJA + 0.15), (1.6, 0.35, 0.3), "sacos")
        for k in range(3):
            fino.caja(((k - 1) * 0.45, s * (Y0 - 2.3), Z_CAJA + 0.12), (0.4, 0.3, 0.24), "sacos")
    # --- arado frontal (solo delante): cuña de acero hasta 1,1 m por delante de los topes
    t.caja((0, -Y0 - 0.75, 0.55), (3.0, 1.1, 0.85), "arado", cuña=0.12, inclinar=0.35)
    t.caja((0, -Y0 - 0.2, 0.95), (1.6, 0.3, 0.3), "metal")
    # --- costado: panel de rejillas en la mitad central (lateral 1) y nervios en el LOD0
    for x in (-1.0, 1.0):
        # Solo en el LOD0 (Lucía, 6-nov): en el LOD1 el 303424 era un agujero negro en el flanco, y la paleta no tiene un
        # oliva entre 5 y 12 de L* por debajo del 50541c, así que en el LOD1 el flanco queda liso en 50541c
        fino.caja((x * 1.6, 0.0, 2.5), (0.04, 11.0, 1.8), "rejilla")
        for k in range(6):
            fino.caja((x * 1.625, 0.0, 1.75 + k * 0.3), (0.03, 10.9, 0.08), "casco")
    # --- techo: carenado de radiadores con rejilla, escapes y ventiladores
    t.caja((0, 3.0, Z_CAJA + 0.12), (2.2, 5.0, 0.24), bisel=True)
    fino.caja((0, 3.0, Z_CAJA + 0.25), (1.8, 4.4, 0.04), "rejilla")
    for y in (-2.6, -1.2):
        fino.cilindro((0, y, Z_CAJA + 0.05), 0.55, 0.1, "Z", 12, "rejilla")
    for y in (-4.2, -3.6):
        t.cilindro((0.4, y, Z_CAJA + 0.2), 0.12, 0.4, "Z", 8, "metal")
    return t.objeto(nombre)
