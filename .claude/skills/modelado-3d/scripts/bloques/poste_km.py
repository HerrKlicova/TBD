"""Poste kilométrico de la vía (ambientación de Sagunto, pieza 8). Teo, 6-nov-2026.

SIN FOTO de Paula (no está en las E: se pide). De memoria y SIN VERIFICAR: poste de hormigón blanco de sección
cuadrada, de unos 0,25 m y 1,1 m de alto, con la cabeza achaflanada y el número del kilómetro pintado en negro en dos
caras; los hectométricos son más bajos (0,6 m). Colores: hormigón d4d0cc, número 48403c, base de tierra 94948c.
Origen al pie; las caras con número miran a ±X (a lo largo de la vía). El LOD1 tiene 0,34 m de sección (2 px).
Variantes: 0 kilométrico; 1 inclinado; 2 partido por la mitad, con el trozo en el suelo; 3 hectométrico;
4 kilométrico con una placa de chapa atornillada; 5 tumbado.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["hormigon", "numero", "base"]
COLORES = {"hormigon": "d4d0cc", "numero": "48403c", "base": "94948c"}
FACCION = "neutro"
PRESUPUESTO = {0: (6, 200), 1: (3, 100)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def construir(nombre="SM_PosteKm", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    v = VAR % 6
    a = 0.25 if lod == 0 else 0.34
    h = 0.6 if v == 3 else 1.1
    t.caja((0, 0, 0.04), (0.6, 0.6, 0.08), "base")
    if v == 5:
        t.caja((0.55, 0.1, a / 2 + 0.02), (h, a, a), "hormigon", rot_z=0.3)
        return t.objeto(nombre, bisel=0)
    ry = {1: 0.2}.get(v, 0.0)
    hh = h * 0.5 if v == 2 else h
    zc = hh / 2
    t.caja((math.sin(ry) * zc, 0, math.cos(ry) * zc + 0.02), (a, a, hh), "hormigon", rot_y=ry)
    if v != 2:
        t.caja((math.sin(ry) * (hh + 0.05), 0, math.cos(ry) * (hh + 0.05) + 0.02), (a * 0.7, a * 0.7, 0.1), "hormigon", rot_y=ry)
        for sx in (-1, 1):
            zn = hh * 0.72
            fino.caja((math.sin(ry) * zn + sx * (a / 2 + 0.01) * math.cos(ry), 0, math.cos(ry) * zn + 0.02),
                      (0.02, a * 0.6, 0.22), "numero", rot_y=ry)
    else:
        t.caja((0.5, 0.25, a / 2 + 0.02), (h * 0.5, a, a), "hormigon", rot_z=0.8)
    if v == 4:
        t.caja((-(a / 2 + 0.02), 0, hh * 0.55), (0.03, a + 0.08, 0.3), "numero")
    return t.objeto(nombre, bisel=0)
