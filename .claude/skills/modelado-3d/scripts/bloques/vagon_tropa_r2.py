"""R2 · Vagón de tropa: plataforma de bogies con parapeto de chapa y aspilleras, donde viaja el pelotón (Lucía, 6-nov).

Base: vagón plataforma de cuatro ejes de tipo UIC (Rgs).
Fotos de Paula (catálogo R3, CC BY-SA 4.0, Falk2): R3_vagon_plataforma_2.jpg, rama de Rgs de ancho ibérico en 3/4 casi
cenital, y _1.jpg, plataforma Kgs de dos ejes. De la Rgs se toman: teleros finos y altos (unos 10 por costado), larguero
oscuro bajo el tablero con el canto del piso pintado, testeros altos en los extremos y bogies de dos ejes junto a los
testeros. Ninguna de las dos da cotas: SIGUEN SIN VERIFICAR 19,9 m entre topes, tablero de 18,5 × 2,9 m a 1,25 m,
bogies con centros a ±7 m y ruedas de 0,92 m (no hay ficha consultada).
Improvisado por el taller (encargo de Lucía): parapeto de chapa de 1,3 m alrededor del tablero, sujeto a los teleros
de la plataforma (montantes cada 2,3 m), con aspilleras a la altura del tirador; acceso por los testeros; sacos en las
esquinas y cajas de munición. Colores: laterales 50541c; canto del parapeto en 70784c (entregar.py --claro 70784c: en
b8b8b8 salía a tamaño de juego como puntitos blancos y un filo claro se confunde con el contorno de selección, Lucía);
piso en 605c48 (un piso abierto es un interior y va oscuro, Lucía 15:55).
"""
import math
from taller import Taller

PIEZAS = ["casco", "bastidor", "rueda", "optica", "tablero", "chapa", "metal", "sacos", "carga"]
COLORES = {
    "casco": "88885c",     # D-N157: entregar con --costado 88885c y --claro b8b8b8 (Lucía 23:44); chapa del parapeto: laterales del tren (D-N42); el canto de arriba pasa a b8b8b8
    "bastidor": "303424",
    "rueda": "1c200c",
    "optica": "181c30",    # hueco de las aspilleras (se ve oscuro: dentro no da la luz)
    "tablero": "605c48",   # piso abierto = interior oscuro (Lucía, 15:55: en b4a080 desaparecía sobre el suelo de Sagunto c19e78)
    "chapa": "605c48",     # remiendos soldados sobre el parapeto
    "metal": "1c200c",     # teleros, topes y asideros
    "sacos": "847c34",
    "carga": "b4a080",     # cajas de munición (Lucía 9-nov 01:15: de noche 845c28 daba ΔL* 1,7 con el lateral en la build; b4a080 da ~12 de día y de noche; plan B f0dc9c)
}
L = 19.9
Y0 = L / 2
ZT = 1.25          # altura del tablero
HP = 1.3           # alto del parapeto
EP = 0.06          # espesor de la chapa


def construir(nombre="SM_R2_VagonTropa", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    # --- tablero, testeros del bastidor, topes y bogies
    t.caja((0, 0, ZT - 0.15), (2.9, 18.5, 0.3), "tablero")
    t.caja((0, 0, ZT - 0.5), (2.6, 18.5, 0.4), "bastidor")   # foto 2: larguero oscuro y alto bajo el tablero
    fino.caja((0, 0, ZT - 0.18), (2.94, 18.3, 0.22), "casco")   # canto del piso pintado (verde en la foto)
    for s in (-1, 1):
        t.caja((0, s * 9.35, ZT - 0.35), (2.9, 0.3, 0.5), "bastidor")
        for x in (-0.9, 0.9):
            t.cilindro((x, s * (Y0 - 0.2), 1.06), 0.2, 0.4, "Y", 8, "metal")
        yb = s * 7.0
        for x in (-1.0, 1.0):
            t.caja((x, yb, 0.6), (0.2, 2.8, 0.35), "bastidor")
        for e in (-1.0, 1.0):   # batalla del bogie 2,0 m (autor de la foto en Commons, R3_rgs_cotas.md; no es del fabricante)
            for x in (-0.83, 0.83):
                t.cilindro((x, yb + e, 0.46), 0.46, 0.13, "X", 12, "rueda")
    # --- parapeto de chapa: dos costados y dos testeros con hueco de acceso de 1 m en el centro
    zc = ZT + HP / 2
    for x in (-1.0, 1.0):
        t.caja((x * (1.45 - EP / 2), 0, zc), (EP, 18.5, HP), bisel=True)
    for s in (-1, 1):
        for x in (-1.0, 1.0):   # entre el hueco de acceso (0,5 m) y la cara interior del costado
            t.caja((x * (0.5 + 1.45 - EP) / 2, s * (9.25 - EP * 1.5), zc), (1.45 - EP - 0.5, EP, HP), bisel=True)
    # aspilleras (solo LOD0; en el LOD1 eran el único ruido del canto, Lucía 6-nov): ranuras oscuras a 1 m sobre el tablero, cada 1,15 m, en las dos caras
    for x in (-1.0, 1.0):
        for k in range(14):
            y = -7.5 + k * 1.15
            for cara in (1, -1):
                fino.caja((x * (1.45 - EP / 2) + cara * (EP / 2 + 0.005), y, ZT + 1.0), (0.01, 0.45, 0.12), "optica")
    # teleros (montantes de la plataforma) por fuera de la chapa
    for x in (-1.0, 1.0):
        for k in range(10):   # foto 2: unos 10 teleros por costado, finos y más altos que el borde
            fino.caja((x * 1.5, -8.8 + k * 1.955, ZT + 0.6), (0.08, 0.1, 1.5), "metal")
    # remiendos soldados (chapa de otro acero), uno por costado y desplazados
    for x, y in ((-1.0, -3.0), (1.0, 4.2), (1.0, -6.0)):
        t.caja((x * 1.47, y, ZT + 0.75), (0.03, 1.6, 0.9), "chapa")
    # --- dentro: sacos en las esquinas, cajas de munición y banco central
    for s in (-1, 1):
        for x in (-1.0, 1.0):
            t.caja((x * 1.0, s * 8.6, ZT + 0.3), (0.7, 0.9, 0.6), "sacos")
        t.caja((0.0, s * 5.0, ZT + 0.3), (0.9, 0.6, 0.6), "carga")
        fino.caja((0.4, s * 5.8, ZT + 0.2), (0.6, 0.4, 0.4), "carga")
    t.caja((0, 0, ZT + 0.25), (0.5, 8.0, 0.5), "chapa")        # banco/arcón central
    fino.caja((0, s * 9.32, ZT + 0.6), (0.6, 0.04, 0.9), "metal")  # escalerilla del testero
    return t.objeto(nombre)
