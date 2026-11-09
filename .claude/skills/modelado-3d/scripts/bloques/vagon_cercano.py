# Entregar con: --costado 88885c --claro b8b8b8 (D-N157: lateral 88885c, techo b8b8b8). Sin esos dos, el lateral sale en 50541c (S3, 9-nov).
"""Vagones para la VISTA CERCANA del tren (coordinador, 6-nov 23:38: al menos 5 variantes de pasaje y de mercancías, con
las ventanillas aparte para iluminarlas). Teo, 6-nov-2026. TBD_VARIANTE % 6:
  0 coche de viajeros            caja de bogies con fila de ventanillas, puertas en los extremos y fuelles de paso
  1 coche de viajeros blindado   el mismo, con chapas soldadas sobre una ventanilla de cada dos (con mirilla) y sacos
                                 en el techo, como la locomotora R1
  2 vagón de tropa               el R2 aprobado (vagon_tropa_r2.py), con los colores de D-N80
  3 plataforma con contenedor    plataforma de bogies con un contenedor de 40 pies (12,19 × 2,44 × 2,59 m, norma ISO)
  4 cisterna                     cisterna de bogies con cúpula, pasarela y escalerillas
  5 plataforma con dos de 20     dos contenedores de 20 pies (6,06 m) de distinto color
Fuentes: coche de viajeros, fotos R14 de Paula (serie 10000 en 3/4 y serie 9000 de lateral): techo abovedado, unas
11 ventanillas por costado, puerta con ventanilla en cada extremo y fuelle de paso negro. Cotas SIN VERIFICAR: caja de
23,5 × 2,9 m, techo a 4,0 m y ventanillas de 1,05 × 0,8 m.
Plataformas: R3_vagon_plataforma_2 (Rgs, CC BY-SA 4.0, Falk2) y las cotas de R3_rgs_cotas.md (19,9 m, batalla de bogie
2,0 m). Contenedores: R4_vagon_portacontenedores_2 (Lgnss, CC BY-SA, Falk2): bastidor de vigas sin piso, contenedor
con chapa ondulada vertical y puertas en un testero. Cisterna: R4_vagon_cisterna_1 («Vagones con biodiésel», Commons):
cuerpo cilíndrico largo con fondos abombados, cúpula arriba en el centro, escalerilla y pasarela; 14,5 m entre topes
SIN VERIFICAR.
Colores (D-N80: nada propio por debajo de #48403c): laterales 50541c y techo b8b8b8 (D-N42, como la locomotora),
bastidor, ruedas y metal 48403c, cristales 60546c, puertas y chapas 605c48, contenedores 6c7874 / 847c34 / 605c48 (sin
el azul real: 404460 es el rim de las unidades propias), cisterna 94948c (acero).
Las ventanillas (pieza «ventana») salen en el hijo «Ventanillas», con el mismo origen que el vagón, para que Marcos
les dé emisión de noche. Jerarquía: SM_…_LODn └─ Ventanillas (solo en 0, 1 y la locomotora). Mira a +Z en Unity.
"""
import math
import os
from taller import Taller
from montaje import marcar_piezas, separar_piezas, capa_emision, anadir_cajas
import vagon_tropa_r2 as R2

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 6
PIEZAS = ["casco", "bastidor", "rueda", "ventana", "puerta", "fuelle", "metal", "sacos", "chapa", "tablero",
          "contenedor", "contenedor2", "cisterna", "carga", "optica", "foco", "luz"]
COLORES = {"casco": "88885c", "bastidor": "48403c", "rueda": "48403c", "ventana": "60546c", "puerta": "605c48",
           "fuelle": "48403c", "metal": "48403c", "sacos": "847c34", "chapa": "605c48", "tablero": "605c48",
           "contenedor": "6c7874", "contenedor2": "847c34", "cisterna": "94948c", "carga": "b4a080",
           "optica": "60546c", "foco": "48403c", "luz": "f0dc9c"}
if ASPECTO == 5:
    COLORES = dict(COLORES, contenedor="605c48")
ABAJO_CASCO = "50541c"
L_MIN = 28.0
# techo b8b8b8 en los coches (D-N42, como la locomotora); canto del parapeto en 70784c en el de tropa (Lucía, R2)
VALOR_PIEZAS = {"casco": ("b8b8b8", "88885c", "50541c")}  # D-N157 (Lucía, 8-nov 24:33): el lateral sube a 88885c (Bruno midió 7,9 con 70784c y 14,5 con 88885c; Lucía 23:43); el techo
# de v2 (antes 70784c) pasa a b8b8b8 como los demás, para no fundirse con el lateral (Lucía 23:44)
VENTANILLAS = {"Ventanillas": ["ventana"], "Emision": ["luz"]}
# De noche (coordinador, 7-nov 00:57: el tren se veía demasiado oscuro en la vista cercana), en el v2 y el v3: faroles
# (pieza «foco», apagados en 48403c) y una capa «Emision» 1,5 cm por fuera en f0dc9c que Marcos enciende de noche. En
# el de tropa se encienden también las aspilleras (la luz de los faroles de dentro). Posiciones SIN VERIFICAR.
L2, L3 = 19.9, 19.9


def _faroles(o):
    if ASPECTO == 2:
        y0, z = L2 / 2, R2.ZT + R2.HP
        cajas = [((x, y, z + 0.14), (0.18, 0.18, 0.26)) for x in (-1.42, 1.42) for y in (-6.0, 0.0, 6.0)]
        cajas += [((x, s * (y0 - 0.38), 0.98), (0.2, 0.08, 0.2)) for x in (-1.15, 1.15) for s in (-1, 1)]
        fuentes = ("foco", "optica")
    else:
        y0 = L3 / 2
        cajas = [((x, s * (y0 - 0.43), 0.92), (0.2, 0.08, 0.2)) for x in (-1.15, 1.15) for s in (-1, 1)]
        # balizas en los costados del contenedor (2,44 de ancho, a 2 cm de la chapa): en la vista lateral de Marcos los
        # faroles de las puertas quedaban tapados por el propio contenedor
        cajas += [((x, y, 3.5), (0.08, 0.2, 0.22)) for x in (-1.28, 1.28) for y in (-5.0, 0.0, 5.0)]
        fuentes = ("foco",)
    anadir_cajas(o, cajas, "foco")
    capa_emision(o, fuentes)
# Normales inclinadas hacia arriba, como el soldado: con la luz casi cenital del juego los costados verticales (casi
# todo lo que se ve de un contenedor o de una caja de 23 m) caían a media luz, a valores de la horda. Vale para el
# R2 de tropa también desde el 7-nov 00:16 (Lucía: en la vista lateral del tren sus costados caían a valores de la horda).
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (300, 4000), 1: (150, 2000)}


def _chasis(t, fino, L, bogie, zt=1.25, ancho=2.9, piso=True):
    """Bastidor, topes y dos bogies de dos ejes (batalla 2,0 m, ruedas de 0,92 m)."""
    y0 = L / 2
    if piso:
        t.caja((0, 0, zt - 0.15), (ancho, L - 1.4, 0.3), "tablero")
    t.caja((0, 0, zt - 0.5), (2.4, L - 1.4, 0.4), "bastidor")
    for s in (-1, 1):
        t.caja((0, s * (y0 - 0.6), zt - 0.35), (ancho, 0.3, 0.5), "bastidor")
        for x in (-0.9, 0.9):
            t.cilindro((x, s * (y0 - 0.2), 1.06), 0.2, 0.4, "Y", 8, "metal")      # topes
        yb = s * bogie
        for x in (-1.0, 1.0):
            t.caja((x, yb, 0.6), (0.2, 2.8, 0.35), "bastidor")
        for e in (-1.0, 1.0):
            for x in (-0.83, 0.83):
                t.cilindro((x, yb + e, 0.46), 0.46, 0.13, "X", 12, "rueda")


def _coche(t, fino, blindado):
    L, y0, zb, zr = 23.5, 11.75, 1.15, 4.0
    _chasis(t, fino, L, 8.3, zt=zb, piso=False)

    def semiancho(z):                     # techo abovedado (R14: coche de la serie 10000 en 3/4)
        if z <= 3.5:
            return 1.45
        if z <= 3.82:
            return 1.45 - 0.2 * (z - 3.5) / 0.32
        return 1.25 - 0.45 * (z - 3.82) / (zr - 3.82)
    t.perfil_extruido([(-y0 + 0.4, zb), (-y0 + 0.4, zr), (y0 - 0.4, zr), (y0 - 0.4, zb)], semiancho,
                      cortes_z=(3.5, 3.82))
    xs = 1.45
    # ventanillas: 11 por costado entre las plataformas de las puertas (R14_coche_convencional_1/_2)
    ys = [-8.0 + k * 1.6 for k in range(11)]
    for s in (-1, 1):
        for k, y in enumerate(ys):
            tapada = blindado and k % 2 == 0
            if tapada:
                t.caja((s * (xs + 0.03), y, 2.55), (0.06, 1.2, 0.95), "chapa")
                t.caja((s * (xs + 0.065), y, 2.7), (0.02, 0.6, 0.08), "ventana")      # mirilla (se enciende)
            else:
                t.caja((s * (xs + 0.005), y, 2.55), (0.03, 1.05, 0.8), "ventana")
                if fino:
                    t.caja((s * (xs + 0.02), y, 2.55), (0.02, 0.05, 0.8), "metal")    # parteluz
        # puertas en los extremos con su ventanilla
        for e in (-1, 1):
            y = e * (y0 - 1.45)
            t.caja((s * (xs + 0.02), y, 2.15), (0.04, 0.9, 1.95), "puerta")
            t.caja((s * (xs + 0.045), y, 2.65), (0.02, 0.45, 0.55), "ventana")
            if fino:
                t.caja((s * (xs + 0.03), y, zb - 0.15), (0.12, 0.7, 0.06), "metal")   # estribo
    # fuelles de paso en los testeros
    for e in (-1, 1):
        t.caja((0, e * (y0 - 0.2), 2.5), (1.2, 0.45, 2.2), "fuelle")
    if fino:
        for e in (-1, 1):                                                            # ventiladores del techo
            for y in (-5.0, 0.0, 5.0):
                t.cilindro((0, y + e * 0.0, zr + 0.06), 0.28, 0.12, "Z", 10, "metal")
    if blindado:
        for e in (-1, 1):                                                            # nidos de sacos en el techo
            for x in (-0.55, 0.55):
                t.caja((x, e * 8.5, zr + 0.15), (0.4, 1.6, 0.3), "sacos")
            t.caja((0, e * 9.2, zr + 0.15), (1.2, 0.35, 0.3), "sacos")
        for y in (-3.0, 4.0):                                                        # remiendos soldados
            t.caja((1.49, y, 1.8), (0.03, 1.4, 0.6), "chapa")


def _contenedor(t, fino, c, largo, pieza):
    x, y, z0 = c
    a, h = 2.44, 2.59
    t.caja((x, y, z0 + h / 2), (a, largo, h), pieza)
    if fino:                                    # chapa ondulada: nervios verticales en los costados, cada 0,6 m
        n = int(largo / 0.6)
        for k in range(1, n):
            yy = y - largo / 2 + k * largo / n
            for s in (-1, 1):
                t.caja((x + s * (a / 2 + 0.015), yy, z0 + h / 2), (0.03, 0.08, h - 0.2), pieza)
        for k in range(4):                      # barras de cierre de las puertas (testero trasero)
            t.caja((x - 0.9 + k * 0.6, y + largo / 2 + 0.03, z0 + h / 2), (0.05, 0.05, h - 0.3), "metal")
    t.caja((x, y, z0 + h + 0.02), (a - 0.1, largo - 0.1, 0.04), pieza)              # techo


def _portacontenedores(t, fino, dos):
    L = 19.9
    _chasis(t, fino, L, 7.0, piso=False)
    for x in (-1.2, 1.2):                       # largueros laterales del bastidor de vigas (sin piso)
        t.caja((x, 0, 1.12), (0.25, L - 1.4, 0.3), "bastidor")
    for k in range(5):
        t.caja((0, -7.2 + k * 3.6, 1.12), (2.4, 0.25, 0.25), "bastidor")
    if dos:
        _contenedor(t, fino, (0, -3.15, 1.27), 6.06, "contenedor")
        _contenedor(t, fino, (0, 3.15, 1.27), 6.06, "contenedor2")
    else:
        _contenedor(t, fino, (0, 0, 1.27), 12.19, "contenedor")


def _cisterna(t, fino):
    L = 14.5
    _chasis(t, fino, L, 4.6, piso=False)
    zc, r, lc = 2.75, 1.4, 11.4
    t.cilindro((0, 0, zc), r, lc, "Y", 16, "cisterna")
    for s in (-1, 1):                           # fondos abombados
        t.cilindro((0, s * (lc / 2 + 0.25), zc), r, 0.5, "Y", 16, "cisterna", r2=r * 0.55) if s > 0 else \
            t.cilindro((0, s * (lc / 2 + 0.25), zc), r * 0.55, 0.5, "Y", 16, "cisterna", r2=r)
    t.cilindro((0, 0, zc + r + 0.1), 0.38, 0.35, "Z", 10, "cisterna")              # cúpula
    for y in (-3.5, 3.5):                       # cunas de apoyo
        t.caja((0, y, 1.45), (2.2, 0.4, 0.5), "bastidor")
    if fino:
        t.caja((0, 0, zc + r + 0.02), (0.6, 3.0, 0.04), "metal")                   # pasarela
        for s in (-1, 1):
            t.caja((s * 1.3, 0, 2.2), (0.04, 0.5, 2.0), "metal")                    # escalerillas
            t.caja((s * (r - 0.1), 0, zc + 0.3), (0.04, 0.5, 1.4), "metal")
            t.caja((s * 0.35, 0, zc + r + 0.5), (0.04, 3.0, 0.04), "metal")        # barandillas


def construir(nombre="SM_Vagon", lod=0):
    if ASPECTO == 2:
        o = R2.construir(nombre, lod)
        _faroles(o)
        return marcar_piezas(o)
    t = Taller(PIEZAS)
    fino = lod == 0
    if ASPECTO in (0, 1):
        _coche(t, fino, ASPECTO == 1)
    elif ASPECTO in (3, 5):
        _portacontenedores(t, fino, ASPECTO == 5)
    else:
        _cisterna(t, fino)
    o = t.objeto(nombre, bisel=0)
    if ASPECTO == 3:
        _faroles(o)
    return marcar_piezas(o)


def separar(o, lod):
    return separar_piezas(o, VENTANILLAS)
