"""Autobús urbano abandonado, cruzado a la entrada del pueblo de Sagunto (lista de cajas de Bruno, 9-nov 22:30: «Autobús
cruzado» en (59; 92), 11 × 2,6 × 3 m). Teo (4), 9-nov, noche de libre albedrío.

Autobús urbano de piso bajo genérico de principios de siglo, sin marcas, logotipos ni número de línea. MEDIDAS SIN
VERIFICAR, típicas de la clase: 11,0 × 2,55 × 3,0 m, batalla 5,9 m, voladizo delantero 2,7 m, tres puertas dobles en el
costado derecho (+X), ruedas de 0,5 m de radio.
Mismas reglas de color que coches_civiles (Lucía, 8-nov): nada en la banda de la horda (L* 9–13), nada rojo, d4d0cc no en
grande. Pinturas por valor partido (techo, costado): blanco b8b8b8/88885c, beige b4a080/605c48, verde 88885c/70784c, ocre
847c34/605c48. Lunas 6c7874, ruedas 605c48 (separadas), faros 88885c, óxido 7c3c18, chasis 6c7874.
Variantes (TBD_VARIANTE % 5): 0 abandonado entero (blanco); 1 con las puertas abiertas (verde); 2 con una rueda delantera
pinchada, ladeado 3° (ocre); 3 accidentado, con el morro hundido y la luna rota (beige); 4 quemado (sin lunas ni ruedas,
sobre las llantas, con el techo oxidado).
Delante = −Y (+Z en Unity). Largo a lo largo de Y de Blender: en Unity va a lo largo de Z, así que el «Autobús cruzado»
del código (caja con el largo en X y yaw 74) pide yaw 74 + 90 = 164 con este modelo. Origen en el suelo, en el centro.
"""
import math, os
from mathutils import Matrix
from taller import Taller

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Abandonado", "PuertasAbiertas", "Pinchado", "Accidentado", "Quemado"]
QUEMADO = V == 4
PINTURAS = [("b8b8b8", "88885c"), ("88885c", "70784c"), ("847c34", "605c48"), ("b4a080", "605c48"),
            ("94948c", "605c48")]
_PINT = PINTURAS[V]
PIEZAS = ["casco", "luna", "rueda", "faro", "puerta", "oxido", "chasis"]
COLORES = {"casco": _PINT[1], "luna": "605c48" if QUEMADO else "6c7874", "rueda": "605c48", "faro": "88885c",
           "puerta": _PINT[1], "oxido": "7c3c18", "chasis": "6c7874"}
VALOR_PIEZAS = {"casco": _PINT + ("605c48",), "puerta": _PINT + ("605c48",)}   # abajo en 605c48: por defecto salía 303424 (banda)
FACCION = "neutro"
PRESUPUESTO = {0: (60, 1500), 1: (40, 700)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}

L, SEMI, H, R = 11.0, 1.275, 3.0, 0.5
Y0, Y1 = -L / 2, L / 2
EJES = (Y0 + 2.7, Y0 + 2.7 + 5.9)
# perfil lateral (y, z): morro casi vertical con la luna inclinada, techo plano con la caja del aire acondicionado
P = [(Y0, 0.32), (Y0, 1.05), (Y0 + 0.12, 2.75), (Y0 + 0.45, 2.98), (Y1 - 0.25, 2.98), (Y1, 2.7), (Y1, 0.32)]
CORTE = 1.0
# puertas (y del centro, ancho): delantera, central y trasera, en el costado derecho (+X)
PUERTAS = ((Y0 + 1.15, 1.2), (Y0 + 5.0, 1.2), (Y1 - 2.6, 1.1))


def construir(nombre="SM_Autobus", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    semi = lambda z: SEMI if z <= 2.7 else SEMI - (z - 2.7) * 0.4
    t.perfil_extruido(P, semi, cortes_z=(2.7,), pieza="casco")
    if not QUEMADO:
        t.caja((0, 0.8, 3.12), (1.7, 2.6, 0.28), "casco")                   # aire acondicionado del techo
    else:
        t.caja((0, 0.8, 3.0), (1.7, 2.6, 0.06), "oxido")
    # lunas: banda lateral continua y luna delantera grande
    zl, hl = 2.05, 1.05
    for s in (-1, 1):
        y0, y1 = Y0 + 0.5, Y1 - 0.5
        t.caja((s * (SEMI + 0.005), (y0 + y1) / 2, zl), (0.02, y1 - y0, hl), "luna")
    t.caja((0, Y0 - 0.005, 1.95), (2.3, 0.02, 1.5), "luna")
    if fino:
        t.caja((0, Y1 + 0.005, 2.1), (1.9, 0.02, 0.8), "luna")
        for k in range(5):                                                  # montantes entre lunas
            y = Y0 + 2.0 + k * 1.75
            for s in (-1, 1):
                t.caja((s * (SEMI + 0.012), y, zl), (0.02, 0.1, hl), "puerta")
    # puertas del costado derecho: cerradas (hoja en la pared) o abiertas (hojas giradas hacia fuera)
    for yc, w in PUERTAS:
        if V == 1:
            for s in (-1, 1):
                a = math.radians(70)
                t.caja((SEMI + math.sin(a) * w / 4, yc + s * (w / 2 - math.cos(a) * w / 4), 1.3), (0.06, w / 2, 2.2),
                       "puerta", rot_z=-s * a)
            t.caja((SEMI - 0.04, yc, 1.3), (0.04, w, 2.2), "chasis")       # hueco oscuro
        elif fino or not QUEMADO:
            t.caja((SEMI + 0.01, yc, 1.3), (0.03, w, 2.2), "puerta")
            if fino and not QUEMADO:
                t.caja((SEMI + 0.03, yc, 1.75), (0.02, w * 0.9, 1.1), "luna")
    # ruedas separadas de la carrocería
    for yw in EJES:
        for s in (-1, 1):
            if QUEMADO:
                if fino:
                    t.cilindro((s * (SEMI - 0.15), yw, R * 0.6), R * 0.6, 0.14, "X", 8, "oxido")
                continue
            rr = R * 0.65 if (V == 2 and yw == EJES[0] and s < 0) else R
            t.cilindro((s * (SEMI - 0.15), yw, rr), rr, 0.3, "X", 12 if fino else 8, "rueda")
    if fino and not QUEMADO:
        for s in (-1, 1):
            t.caja((s * (SEMI - 0.3), Y0 - 0.01, 0.75), (0.35, 0.03, 0.18), "faro")
        t.caja((0, Y0 - 0.02, 0.45), (2.4, 0.06, 0.22), "chasis")         # parachoques
    if QUEMADO:                                                             # manchas de óxido por el techo
        mn = 0.15 if fino else 0.4
        for k, (fy, fx, a1, a2, rz) in enumerate(((0.12, -0.3, 1.2, 0.7, 0.3), (0.35, 0.3, 0.9, 1.4, -0.6),
                                                  (0.62, -0.2, 1.5, 0.6, 0.9), (0.86, 0.25, 0.6, 1.0, 0.2))):
            t.caja((fx * SEMI, Y0 + L * fy, 2.99 + 0.004 * k), (max(a1, mn), max(a2, mn), 0.03), "oxido", rot_z=rz)
        for s in (-1, 1):
            t.caja((s * (SEMI + 0.02), -1.5, 1.4), (0.03, 2.2, max(0.5, mn)), "oxido", rot_z=0.0)
    o = t.objeto(nombre, bisel=0)
    me = o.data
    if V == 3:                                                              # morro hundido
        for vtx in me.vertices:
            if vtx.co.y < Y0 + 0.9:
                k = (Y0 + 0.9 - vtx.co.y) / 0.9
                vtx.co.y += 0.45 * k
                vtx.co.z -= 0.1 * k * max(0.0, vtx.co.z - 0.5)
    if V == 2:
        me.transform(Matrix.Rotation(math.radians(3), 4, "Y"))
    if QUEMADO:
        me.transform(Matrix.Translation((0, 0, -R * 0.4)))
    return o
