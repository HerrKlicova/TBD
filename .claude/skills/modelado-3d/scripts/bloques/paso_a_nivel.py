"""Paso a nivel con barreras y caseta del guardabarrera (Álvaro, sagunto-nivel.md §5; C1: cubre las dos vías). Teo, 6-nov-2026.

Fotos F3 de Paula (Commons; Gata de Gorgos y Paterna, líneas de FGV, no de Adif: sirven por el tipo): barrera de brazo
con contrapeso sobre un pedestal-armario, semáforo de dos focos en un mástil, armarios de señalización a pie de vía.
Las barreras reales son rojas y blancas; el rojo está vetado en los objetos (Lucía), así que el brazo va a franjas de
1 m en f0dc9c y 48403c (aviso amarillo y oscuro, a la espera de lo que diga Lucía). En el LOD1 el brazo tiene 0,36 m de
grueso (2 px); el real mide unos 0,12 m.
MEDIDAS SIN VERIFICAR (típicas): calzada de 6 m que cruza las dos vías (banda de 9 m, Z 58-67), brazos de 6,4 m a 6,5 m
del eje, semáforos de 3 m y caseta de 3 × 2,5 m con tejado de teja. La vía va a lo largo de X y el camino a lo largo de Y.
Variantes: 0 barreras levantadas; 1 bajadas; 2 un brazo arrancado en el suelo; 3 un brazo partido y la caseta sin
tejado; 4 bajadas con un brazo doblado; 5 levantadas, la caseta tapiada con tablas y un semáforo caído.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["franja", "franja_osc", "poste", "armario", "foco", "cal", "tejado", "carpinteria", "tablas"]
COLORES = {"franja": "f0dc9c", "franja_osc": "48403c", "poste": "6c7874", "armario": "94948c", "foco": "283440",
           "cal": "d4d0cc", "tejado": "a86c50", "carpinteria": "543820", "tablas": "845c28"}
FACCION = "neutro"
PRESUPUESTO = {0: (60, 1500), 1: (30, 700)}
UMBRAL_CONTORNO = {1: 9.0}   # como el puente: contorno solo en la silueta, que el brazo no se lo coma el negro
YB, LB = 6.5, 6.4          # distancia de las barreras al eje de la vía y largo del brazo


def construir(nombre="SM_PasoANivel", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    v = VAR % 6
    g = 0.14 if lod == 0 else 0.5                       # grueso del brazo
    for i, s in enumerate((-1, 1)):                     # barrera sur (s=-1) y norte (s=+1)
        px, py = s * 3.7, s * YB                        # pedestal a un lado de la calzada
        t.caja((px, py, 0.55), (0.6, 0.5, 1.1), "armario")
        fino.caja((px, py - s * 0.26, 0.6), (0.4, 0.02, 0.6), "poste")                      # puerta del armario
        estado = _estado_brazo(v, i)
        _brazo(t, px, py, -s, g, estado)
        # semáforo de dos focos al otro lado de la calzada
        sx = -s * 3.9
        if v == 5 and i == 1:                           # semáforo caído
            t.caja((sx + 1.4, py, 0.15), (3.0, 0.18, 0.18), "poste")
            t.caja((sx + 2.9, py, 0.3), (0.3, 0.9, 0.4), "foco")
        else:
            t.caja((sx, py, 1.5), (0.16 if lod == 0 else 0.3, 0.16 if lod == 0 else 0.3, 3.0), "poste")
            t.caja((sx, py, 2.9), (0.4, 1.0, 0.4), "foco")
            for e in (-0.3, 0.3):
                fino.cilindro((sx - s * 0.21, py + e, 2.9), 0.13, 0.04, "X", 8, "foco")
        t.caja((sx, py + s * 1.2, 0.5), (0.5, 0.4, 1.0), "armario")                          # armario de vía
    _caseta(t, fino, -8.0, -9.0, v)
    return t.objeto(nombre, bisel=0)


def _estado_brazo(v, i):
    if v == 1:
        return "bajado"
    if v == 2:
        return "suelo" if i == 0 else "alto"
    if v == 3:
        return "partido" if i == 1 else "alto"
    if v == 4:
        return "doblado" if i == 0 else "bajado"
    return "alto"


def _brazo(t, px, py, dirx, g, estado):
    """Brazo de franjas de 1 m con contrapeso. dirx: hacia dónde cruza la calzada (+1 o -1 en X)."""
    zp = 1.0
    t.caja((px - dirx * 0.55, py, zp), (0.6, 0.4, 0.4), "poste")                              # contrapeso
    if estado == "suelo":
        _franjas(t, px + dirx * 1.5, py - 1.4, 0.1, dirx, g, 0.0, rot_z=0.5)
        return
    largo = LB / 2 if estado == "partido" else LB
    ang = {"alto": math.radians(82), "bajado": 0.0, "partido": math.radians(8), "doblado": 0.0}[estado]
    _franjas(t, px, py, zp, dirx, g, ang, largo=largo)
    if estado == "doblado":                                                                   # la punta cae al suelo
        x0 = px + dirx * LB * 0.6
        t.caja((x0 + dirx * 1.1, py, zp / 2), (2.6, g, g), "franja", rot_y=dirx * 0.38)
    if estado == "partido":
        t.caja((px + dirx * 4.6, py + 0.8, 0.1), (2.8, g, g), "franja", rot_z=0.6)


def _franjas(t, px, py, zp, dirx, g, ang, largo=LB, rot_z=0.0):
    n = int(round(largo))
    for k in range(n):
        d = (k + 0.5) * largo / n
        x = px + dirx * d * math.cos(ang) * math.cos(rot_z)
        y = py + d * math.sin(rot_z) * dirx
        z = zp + d * math.sin(ang)
        t.caja((x, y, z), (largo / n + 0.02, g * 1.5, g * 0.7), "franja" if k % 2 == 0 else "franja_osc",
               rot_y=-dirx * ang, rot_z=rot_z)


def _caseta(t, fino, x, y, v):
    """Caseta del guardabarrera encalada, 3 × 2,5 m, con tejado de teja a dos aguas y puerta al camino."""
    t.caja((x, y, 1.3), (3.0, 2.5, 2.6), "cal")
    if v != 3:
        t.tejado((x, y, 2.6), 3.0, 2.5, 1.0, "tejado", vuelo=0.25, aguas=2)
    else:                                                                                     # sin tejado: hueco oscuro
        t.caja((x, y, 2.62), (2.7, 2.2, 0.06), "foco")
    t.caja((x + 1.53, y, 1.0), (0.06, 0.9, 2.0), "tablas" if v == 5 else "carpinteria")
    t.caja((x, y - 1.28, 1.6), (0.9, 0.06, 0.8), "tablas" if v == 5 else "carpinteria")
