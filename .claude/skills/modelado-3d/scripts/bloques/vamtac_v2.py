"""V2 · Pick-up artillada: URO VAMTAC ST5 blindado con ametralladora pesada en el techo.

Medidas (ficha técnica de URO, «VAMTAC ST5 Extended Cab», rev1-02, en militaryvehiclesdata.com): 5,99 m de largo,
2,444 m de ancho, 2,166 m de alto, batalla de 3,90 m, altura libre 0,414 m y neumáticos 335/80 R20 (≈1,04 m).
De las fotos de Paula (V2_vamtac_1.jpg, dominio público, OR-7 Pia Dunkel, Steadfast Defender 2021; V2_vamtac_2.jpg,
CC BY-SA 2.0, koldo hormaza, generación anterior): capó largo con rejilla de lamas, parabrisas casi vertical de dos
hojas, cabina blindada de cuatro puertas con ventanillas pequeñas, caja trasera con el techo cayendo hacia atrás,
soporte de bidones atrás, defensa delantera (bullbar), snorkel en el montante izquierdo, estribo bajo las puertas,
aro en el techo con la ametralladora y escudo curvo detrás del tirador, faros en la defensa.
SIN VERIFICAR (estimado del lateral): reparto capó/cabina/caja, alto del capó (1,45 m) y del aro.
LOD1 con la regla de Bruno (6-nov, 0,17 m/px): nada de menos de 2 px (~0,35 m); el cañón engordado a 2 px; sin bullbar,
snorkel, retrovisores, estribo ni bidones sueltos (el soporte queda como un bloque).
"""
import math
from taller import Taller

PIEZAS = ["casco", "goma", "llanta", "optica", "foco", "metal", "carga"]
COLORES = {"casco": "50541c", "goma": "303424", "llanta": "70784c", "optica": "181c30", "foco": "f0dc9c",
           "metal": "1c200c", "carga": "847c34"}
L, A, H = 5.99, 2.444, 2.166
Y0 = L / 2
ZB = 0.62        # bajo de la carrocería (altura libre 0,414 + chasis)


def _semiancho(z):
    # Por debajo de 1,1 m la caja se recoge a 0,8 m: es el hueco de los pasos de rueda, para que la rueda de 1,04 m se
    # vea entera en el lateral (Lucía, 6-nov: tapada por el flanco medía 0,84 m y parecía un Humvee). Encima, flanco
    # vertical hasta 1,5 m y la cabina recogida arriba (la ficha da 2,444 con retrovisores plegados: casco 2,3)
    if z <= 1.1:
        return 0.8
    if z < 1.12:
        return 0.8 + (1.15 - 0.8) * (z - 1.1) / 0.02
    if z <= 1.5:
        return 1.15
    return 1.15 + (1.02 - 1.15) * (z - 1.5) / (H - 1.5)


COSTURAS = [((0, -0.95, 0), (0, 1, 0)), ((0, 0.25, 0), (0, 1, 0)), ((0, 1.4, 0), (0, 1, 0))]  # puertas y caja


def construir(nombre="SM_V2_Vamtac", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    # perfil lateral (delante = -Y): defensa, capó, parabrisas casi vertical, techo, caída de la caja trasera
    P = [(-Y0 + 0.25, ZB), (-Y0, 0.95), (-Y0 + 0.05, 1.35), (-Y0 + 0.45, 1.45), (-1.45, 1.5), (-1.25, H),
         (1.45, H), (Y0 - 0.25, 1.75), (Y0, 1.6), (Y0, ZB + 0.15), (Y0 - 0.3, ZB)]
    t.perfil_extruido(P, _semiancho, cortes_z=(1.1, 1.12, 1.5))
    if lod == 0:
        t.costuras(COSTURAS)
    # ruedas 335/80 R20 con la batalla de 3,90 m
    for y in (-1.95, 1.95):
        for x in (-0.98, 0.98):
            t.cilindro((x, y - 0.25, 0.52), 0.52, 0.34, "X", 14, "goma")
            t.cilindro((x * 1.18, y - 0.25, 0.52), 0.3, 0.04, "X", 10, "llanta")
    t.caja((0, -0.25, 0.55), (1.6, 4.4, 0.3), "goma")           # chasis y puentes (oscuro, entre ruedas)
    # parabrisas de dos hojas y ventanillas laterales pequeñas (cristal blindado)
    a = math.atan2(0.2, H - 1.5)
    for x in (-0.48, 0.48):
        t.caja((x, -1.37, 1.83), (0.86, 0.03, 0.55), "optica", rot_x=a)
    for s in (-1, 1):
        for y in (-0.45, 0.75):
            t.caja((s * (_semiancho(1.85) + 0.01), y, 1.85), (0.02, 0.75, 0.38), "optica")
    # rejilla del capó (lamas pintadas por valor, sin relieve en el LOD1) y faros en la defensa
    for k in range(4):
        fino.caja((0, -2.25 - k * 0.12, 1.46), (1.2, 0.05, 0.02), "metal")
    t.caja((0, -Y0 - 0.06, 0.95), (2.0, 0.14, 0.3), "metal")    # defensa baja (2 px de alto)
    fino.caja((0, -Y0 - 0.1, 1.2), (1.9, 0.06, 0.06), "metal")   # bullbar
    for x in (-0.85, 0.85):
        fino.cilindro((x, -Y0 - 0.1, 1.05), 0.03, 0.4, "Z", 6, "metal")
        t.caja((x, -Y0 - 0.02, 1.25), (0.22, 0.04, 0.14), "foco")
    # snorkel, retrovisores y estribo (solo de cerca)
    fino.cilindro((-1.1, -1.5, 1.9), 0.07, 0.9, "Z", 8, "metal")
    fino.caja((-1.1, -1.5, 2.38), (0.18, 0.18, 0.1), "metal")
    for s in (-1, 1):
        fino.caja((s * 1.3, -1.25, 1.75), (0.08, 0.12, 0.28), "metal")
        fino.caja((s * 1.18, 0.15, 0.68), (0.08, 2.4, 0.06), "metal")
    # techo: aro, ametralladora pesada y escudo curvo del tirador
    zt = H
    t.cilindro((0, -0.1, zt + 0.06), 0.55, 0.12, "Z", 12, "metal")
    t.caja((0, -0.1, zt + 0.35), (0.25, 0.3, 0.45), "metal")       # afuste
    t.caja((0, -0.45, zt + 0.55), (0.3, 0.7, 0.25), "metal")       # cajón de la ametralladora (2 px)
    r_c = 0.03 if lod == 0 else 0.15                                # cañón: 2 px en el LOD1 (Bruno)
    t.cilindro((0, -1.2, zt + 0.58), r_c, 1.1, "Y", 6, "metal")
    fino.caja((0.3, -0.25, zt + 0.5), (0.2, 0.3, 0.2), "carga")    # caja de munición
    for k in range(5):                                             # escudo curvo detrás del tirador
        ang = math.radians(-50 + k * 25)
        t.caja((0.55 * math.sin(ang), 0.45 + 0.55 * (math.cos(ang) - 1) * -0.5, zt + 0.55),
               (0.3, 0.05, 0.85), rot_z=-ang)
    # atrás: soporte de bidones (bloque) y bidones sueltos de cerca
    t.caja((0.6, Y0 + 0.2, 1.35), (0.9, 0.4, 0.6), "carga")
    fino.caja((-0.55, Y0 + 0.15, 1.3), (0.5, 0.3, 0.45), "carga")
    t.caja((0, Y0 + 0.05, ZB + 0.2), (2.0, 0.12, 0.25), "metal")   # paragolpes trasero
    return t.objeto(nombre)
