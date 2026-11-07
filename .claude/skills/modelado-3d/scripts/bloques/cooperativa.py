"""Cooperativa citrícola de Nules, sitio N2 (Álvaro, nules-nivel.md §0b, 7-nov 08:20). Teo, 7-nov.

De Nules no hay foto libre (catalogo_nules.md de Paula). Como dice el nivel, la cooperativa se hace con el almacén de
naranjas del kit de Sagunto (almacen_naranjas.py, aprobado): la variante de frontón ondulado con óculo, encalado,
zócalo gris y portón metálico, que es la del almacén de Alcàsser (N2_2 y N2_3, las mismas fotos de la F4). La quinta
variante es la nave moderna de la Cooperativa Sant Josep de La Vilavella (N2_1): nave de bloque beige con portones
verdes, cuerpo de oficinas de dos plantas y la marquesina verde de la gasolinera.
Variantes (TBD_VARIANTE % 5):
  0 almacén de la foto N2_2 (= almacén v0);  1 con muelle de carga y marquesina en el costado, el de la vía (= almacén v1);
  2 portón abierto con cajas de fruta, en plena campaña (= almacén v3);  3 con un trozo de tejado hundido (= almacén v2);
  4 nave moderna de La Vilavella (N2_1): 24 x 16 m y 8,5 m de alto, oficinas de 10 x 8 m y marquesina de 12 x 7 m.
Las medidas de la v4 son SIN VERIFICAR (estimadas sobre la foto). Fachada a la calle = -Y (+Z en Unity).
Colores de la v4: bloque b4a080 (el beige de la foto), portones, pilares y faja de la marquesina 50541c (el verde de la
foto, que es el de la fundición del kit), chapa 94948c, vidrio 48403c (605c48 en el LOD1, D-N80). Sin rotulación.
"""
import math
import os
import almacen_naranjas as alm
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
ALMACEN = {0: 0, 1: 1, 2: 3, 3: 2}
PIEZAS = alm.PIEZAS + ["bloque", "verde", "chapa", "vidrio"]
COLORES = dict(alm.COLORES, cal="d4d0cc", bloque="b4a080", verde="50541c", chapa="94948c", vidrio="48403c")
COLORES_LOD1 = {"vidrio": "605c48", "verde": "70784c"} if VAR == 4 else {}
FACCION = "neutro"
PRESUPUESTO = {0: (80, 2500), 1: (40, 1200)}
R = math.radians


def _nave_vilavella(nombre, lod):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    # nave principal: 24 x 16 m, 8,5 m de alto, con pretil que tapa la cubierta de chapa
    NX, NY, H = 24.0, 16.0, 8.5
    xn, yn = 4.0, 2.0
    t.caja((xn, yn, H / 2), (NX, NY, H), "bloque")
    t.caja((xn, yn, H + 0.05), (NX - 0.6, NY - 0.6, 0.1), "chapa")                  # cubierta (detrás del pretil)
    for k in range(-2, 3):                                                         # bandas de la chapa
        t.caja((xn + k * 4.6, yn, H + 0.12), (1.0 if lod else 0.3, NY - 0.7, 0.04), "zocalo")
    t.caja((xn, yn - NY / 2 - 0.03, H - 0.6), (NX + 0.02, 0.06, 0.34), "zocalo")   # junta de los paneles, arriba
    # dos portones verdes a la calle y uno en el costado
    for x in (xn + 2.5, xn + 8.5):
        t.caja((x, yn - NY / 2 - 0.04, 2.6), (4.4, 0.08, 5.2), "verde")
        if lod == 0:
            for z in (1.3, 2.6, 3.9):
                t.caja((x, yn - NY / 2 - 0.09, z), (4.3, 0.03, 0.06), "chapa")      # lamas del portón
    t.caja((xn + NX / 2 + 0.04, yn + 3.0, 2.6), (0.08, 4.4, 5.2), "verde")
    t.caja((xn, yn - NY / 2 - 0.03, 0.25), (NX + 0.02, 0.06, 0.5), "zocalo")
    # oficinas: dos plantas de 10 x 8 m delante a la izquierda, con ventanas en vertical
    OX, OY, OH = 10.0, 8.0, 7.0
    xo, yo = xn - NX / 2 + OX / 2 - 0.5, yn - NY / 2 - OY / 2 + 0.5
    t.caja((xo, yo, OH / 2), (OX, OY, OH), "bloque")
    t.caja((xo, yo, OH + 0.3), (OX + 0.2, OY + 0.2, 0.6), "zocalo")                # remate de la azotea
    for k in range(5):
        x = xo - 3.2 + k * 1.6
        for z in (1.8, 4.9):
            t.caja((x, yo - OY / 2 - 0.03, z), (0.8, 0.06, 1.6), "vidrio")
    t.caja((xo + 2.6, yo - OY / 2 - 0.04, 1.4), (1.6, 0.08, 2.6), "verde")         # puerta de las oficinas
    t.caja((xo - 1.0, yo - OY / 2 - 0.05, 3.3), (3.0, 0.08, 0.6), "verde")         # rótulo verde (sin letras)
    # marquesina de la gasolinera: losa de 12 x 7 m con faja verde de 0,9 m, en tres pilares, y dos surtidores
    MX, MY, MZ = 12.0, 7.0, 5.0
    xm, ym = xo - 2.0, yo - OY / 2 - 6.0
    t.caja((xm, ym, MZ), (MX, MY, 0.3), "chapa")
    for s in (-1, 1):
        t.caja((xm, ym + s * (MY / 2), MZ + 0.15), (MX + 0.2, 0.2, 0.9), "verde")
        t.caja((xm + s * (MX / 2), ym, MZ + 0.15), (0.2, MY, 0.9), "verde")
    for x in (xm - 4.0, xm, xm + 4.0):
        t.caja((x, ym, MZ / 2), (0.7, 0.7, MZ), "verde")
    for x in (xm - 2.0, xm + 2.0):
        t.caja((x, ym, 0.8), (0.6, 1.0, 1.6), "zocalo")                            # surtidor
        fino.caja((x, ym - 0.52, 1.1), (0.4, 0.04, 0.3), "vidrio")
    t.caja((xm, ym, 0.1), (5.6, 2.0, 0.2), "zocalo")                               # isleta
    return t.objeto(nombre, bisel=0)


def construir(nombre="SM_Cooperativa", lod=0):
    if VAR == 4:
        return _nave_vilavella(nombre, lod)
    alm.VAR = ALMACEN[VAR]
    return alm.construir(nombre, lod=lod)
