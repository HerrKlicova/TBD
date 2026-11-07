"""Balizas y señales de las carreteras HABILITADAS del mundo persistente (coordinador, 7-nov 01:06). Teo, 7-nov.

Marcan en la zona por dónde pueden ir los vehículos de tierra (mapa_sagunto_carreteras.json de Bruno, D-N113); en el
mapa la carretera habilitada va en 94948c y la cortada en 48403c, y aquí se sigue la misma clave: placas y paneles en
94948c / d4d0cc, nada oscuro como señal. TODO DE MEMORIA (fotos pedidas a Paula): SIN VERIFICAR. Medidas típicas:
hito de borde de 1,0 m y 0,1 m de radio, señal de 0,9 × 0,6 m a 2,2 m, panel direccional de 1,6 × 0,4 m a 1,0 m,
estaca de 1,2 m, trípode de 1,5 m con un foco de 0,25 m.
Colores: blanco d4d0cc, gris de la clave 94948c, bandas oscuras 48403c, madera b4a080, metal 6c7874 y los catadióptricos
y el foco en f0dc9c (crema, como las balizas del helipuerto: nada de rojo ni ámbar).
Jerarquía: SM_…_LODn (lo apagado) con un hijo «Luces» (catadióptricos y foco, transformación identidad) para que Marcos
les dé emisión de noche. Origen en el suelo, al pie de la baliza (o en el centro del grupo).
Variantes (TBD_VARIANTE % 5): 0 tres hitos de borde con banda reflectante; 1 señal de ruta con flecha en poste;
2 panel direccional de chevrones sobre dos patas; 3 estaca militar con placa y flecha pintada; 4 baliza luminosa en
trípode con caja de baterías.
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = ["blanco", "clave", "oscuro", "madera", "metal", "luz"]
COLORES = {"blanco": "d4d0cc", "clave": "94948c", "oscuro": "48403c", "madera": "b4a080", "metal": "6c7874",
           "luz": "f0dc9c"}
FACCION = "neutro"
PRESUPUESTO = {0: (20, 800), 1: (10, 400)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
# casi todo son placas verticales: con la luz casi cenital caían a media luz (como el tren y los cortes)
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PARTES = ["baliza", "Luces"]
PIV = {"Luces": (0, 0, 0)}


def _flecha(m, c, s, pieza, eje="X", grosor=0.02):
    """Flecha que apunta a +Y (cuerpo y punta en dos cajas giradas), pintada sobre una placa de normal ±X o +Z."""
    x, y, z = c
    if eje == "X":                                    # placa vertical que mira a +X
        m.caja((x, y - 0.12 * s, z), (grosor, 0.36 * s, 0.1 * s), pieza)
        for a in (0.7, -0.7):
            m.caja((x, y + 0.1 * s, z + a * 0.08 * s), (grosor, 0.24 * s, 0.07 * s), pieza, rot_x=-a)
    else:                                             # en el suelo o sobre una placa horizontal
        m.caja((x, y - 0.12 * s, z), (0.1 * s, 0.36 * s, grosor), pieza)
        for a in (0.7, -0.7):
            m.caja((x + a * 0.08 * s, y + 0.1 * s, z), (0.07 * s, 0.24 * s, grosor), pieza, rot_z=a)


def construir(nombre="SM_BalizaRuta", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    a = ASPECTO
    luces = []                                        # (centro, tamaño, rot_z) de los catadióptricos
    if a == 0:                                        # hitos de borde
        for k, y in enumerate((-2.0, 0.0, 2.0)):
            m.cilindro((0, y, 0.5), 0.1 if fino else 0.13, 1.0, "Z", 8, "blanco")
            m.cilindro((0, y, 0.52), 0.104 if fino else 0.134, 0.12, "Z", 8, "oscuro")
            luces.append(((0.11 if fino else 0.14, y, 0.8), (0.03, 0.12, 0.1), 0.0))
    elif a == 1:                                      # señal de ruta con flecha
        m.caja((0, 0, 1.15), (0.08, 0.08, 2.3) if fino else (0.12, 0.12, 2.3), "metal")
        m.caja((0.08, 0, 2.2), (0.04, 0.9, 0.6), "clave")
        m.caja((0.107, 0, 2.2), (0.01, 0.8, 0.5), "blanco")
        _flecha(m, (0.115, 0, 2.2), 1.3, "oscuro", grosor=0.012)
        luces.append(((0.115, 0.0, 2.53), (0.02, 0.9, 0.05), 0.0))
    elif a == 2:                                      # panel de chevrones
        for y in (-0.65, 0.65):
            m.caja((0, y, 0.55), (0.06, 0.06, 1.1), "metal")
        m.caja((0.05, 0, 1.0), (0.04, 1.6, 0.42), "blanco")                  # panel claro: nada oscuro como señal
        n = 4 if fino else 3
        for k in range(n):
            y = -0.55 + 1.1 * k / (n - 1)
            for s in (1, -1):                         # chevrón oscuro que apunta a +Y, con el catadióptrico encima
                m.caja((0.08, y, 1.0 + s * 0.09), (0.02, 0.1, 0.22), "oscuro", rot_x=s * 0.6)
            luces.append(((0.1, y + 0.1, 1.0), (0.02, 0.06, 0.06), 0.0))
    elif a == 3:                                      # estaca militar con placa
        m.caja((0, 0, 0.6), (0.08, 0.08, 1.2), "madera")
        m.caja((0.07, 0, 1.0), (0.03, 0.5, 0.36), "blanco")
        _flecha(m, (0.09, 0, 1.0), 1.0, "oscuro", grosor=0.012)
        if fino:
            _flecha(m, (0.6, 0.6, 0.01), 1.6, "blanco", eje="Z")              # flecha pintada en el asfalto
        luces.append(((0.09, 0.0, 1.21), (0.02, 0.5, 0.05), 0.0))
    else:                                             # baliza luminosa en trípode
        for k in range(3):
            ang = k * 2 * math.pi / 3
            m.barra((0.55 * math.cos(ang), 0.55 * math.sin(ang), 0.0), (0, 0, 1.35), 0.05 if fino else 0.08,
                    0.05 if fino else 0.08, "metal")
        m.cilindro((0, 0, 1.4), 0.15, 0.1, "Z", 10, "oscuro")
        m.caja((0.7, -0.5, 0.18), (0.45, 0.3, 0.36), "clave")                  # caja de baterías
        m.caja((0.7, -0.5, 0.375), (0.47, 0.32, 0.03), "oscuro")
        if fino:
            m.barra((0.6, -0.4, 0.38), (0.06, -0.04, 1.3), 0.025, 0.025, "oscuro")   # cable
    # ---- catadióptricos y foco (hijo «Luces» para la emisión)
    m.parte("Luces")
    for c, s, rz in luces:
        m.caja(c, s, "luz", rot_z=rz)
    if a == 4:
        m.cilindro((0, 0, 1.6), 0.13 if fino else 0.16, 0.28, "Z", 10, "luz")
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    # origen al pie de la baliza (no en el centro de la caja envolvente): así «Luces» queda con identidad
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
