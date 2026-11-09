"""Puerta principal de la valla de Bétera (D-N173; Álvaro, betera-nivel.md §2: puerta en (250; 59), yaw 270; el control
de acceso de la foto AE1). Teo (3), 8-nov.

Foto AE1_betera_torre_1 de Paula: pórtico de tubo verde con el cartel de letras sueltas encima («TODO POR LA PATRIA»;
en el modelo, una placa sin letras) y una puerta de dos hojas de barrotes con chapa abajo, entre muretes encalados.
Medidas SIN VERIFICAR: hueco de 7 m (carretera de dos carriles), hojas de 3,5 × 2,2 m, pórtico de 5 m.
Colores: pórtico y hojas 50541c (el verde de la foto), chapa 70784c, placa b8b8b8 (9-nov; antes d4d0cc, reservado) (letras 48403c en el LOD0), muretes
b8b8b8 (9-nov; antes d4d0cc, reservado) con remate 94948c, sacos 605c48, espino 48403c.
Disposición: origen en el suelo, en el centro del hueco; la valla a lo largo de X y FUERA = −Y (= +Z de Unity con yaw 0).
Jerarquía: SM_…_LODn con Hoja_O (bisagra en x = −3,5) y Hoja_E (x = +3,5); ABRIR = girar +90° (O) y −90° (E) sobre la
vertical, hacia dentro.
Variantes (TBD_VARIANTE % 5): 0 la de la foto, con el pórtico y su placa; 1 sin pórtico; 2 con dos muretes de sacos
por fuera en chicane; 3 con espino sobre las hojas; 4 forzada: una hoja arrancada en el suelo y el pórtico torcido.
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["portico", "hoja", "chapa", "placa", "letra", "murete", "remate", "sacos", "espino"]
COLORES = {"portico": "50541c", "hoja": "50541c", "chapa": "70784c", "placa": "b8b8b8", "letra": "48403c",
           "murete": "b8b8b8", "remate": "94948c", "sacos": "605c48", "espino": "48403c"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.5
PRESUPUESTO = {0: (40, 2000), 1: (20, 900)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
HUECO, HH = 7.0, 2.2
B = HUECO / 2
PARTES = ["puerta", "Hoja_O", "Hoja_E"]
PIV = {"Hoja_O": (-B, 0.0, 0.0), "Hoja_E": (B, 0.0, 0.0)}
GIRO_PRUEBA = {"Hoja_O": ("Z", 60), "Hoja_E": ("Z", -60)}


def _hoja(m, s, fino):
    x0, x1 = s * B, s * 0.02
    xc, L = (x0 + x1) / 2, abs(x1 - x0)
    g = 0.08 if fino else 0.2
    m.caja((xc, 0, 0.55), (L, 0.05, 0.9), "chapa")
    for z in (0.1, 1.0, HH):
        m.caja((xc, 0, z), (L, g, g), "hoja")
    for x in (x0 - s * g / 2, x1 + s * g / 2):
        m.caja((x, 0, HH / 2 + 0.05), (g, g, HH), "hoja")
    if fino:
        for k in range(1, 9):
            m.caja((x0 + (x1 - x0) * k / 9, 0, 1.0 + (HH - 1.0) / 2), (0.04, 0.04, HH - 1.0), "hoja")
    else:
        m.caja((xc, 0, 1.0 + (HH - 1.0) / 2), (L - 0.4, 0.03, HH - 1.0), "hoja")
    if VAR == 3:
        m.cilindro((xc, 0, HH + 0.28), 0.25, L - 0.1, "X", 8 if fino else 6, "espino")


def construir(nombre="SM_PuertaValla", lod=0):
    v = VAR
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    g = 0.12 if fino else 0.34
    for s in (-1, 1):                                # muretes encalados a los lados
        m.caja((s * (B + 1.6), 0, 1.2), (3.0, 0.4, 2.4), "murete")
        m.caja((s * (B + 1.6), 0, 2.45), (3.1, 0.5, 0.1), "remate")
    if v in (0, 2, 3, 4):                            # pórtico con la placa del lema
        tor = 0.12 if v == 4 else 0.0
        for s in (-1, 1):
            m.caja((s * (B + 0.15), 0, 2.5), (g * 1.5, g * 1.5, 5.0), "portico", rot_y=tor * (s > 0))
        m.caja((0, 0, 4.95), (HUECO + 0.5, g * 1.5, g * 1.5), "portico", rot_y=tor * 0.3)
        m.caja((0, -0.05, 5.45), (5.2, 0.06, 0.6), "placa", rot_y=tor * 0.3)
        if fino:
            for k in range(14):                      # letras sueltas (sin texto: bloques)
                if k in (4, 8, 11):
                    continue
                m.caja((-2.4 + k * 0.37, -0.1, 5.45), (0.24, 0.04, 0.36), "letra")
    if v == 2:                                       # chicane de sacos por fuera
        m.caja((-1.5, -3.0, 0.6), (4.0, 0.9, 1.2), "sacos", rot_z=0.03)
        m.caja((1.8, -6.0, 0.6), (4.0, 0.9, 1.2), "sacos", rot_z=-0.03)
    if v == 4:                                       # hoja arrancada en el suelo (en la parte fija)
        m.caja((-1.6, -2.0, 0.06), (3.4, 2.1, 0.08), "hoja", rot_z=0.4)
        m.caja((-1.6, -2.0, 0.11), (3.0, 0.8, 0.03), "chapa", rot_z=0.4)
    for nom, s in (("Hoja_O", -1), ("Hoja_E", 1)):
        m.parte(nom)
        if v == 4 and s < 0:                         # solo queda la bisagra
            m.caja((-B + 0.1, 0, 1.0), (0.18, 0.16, 0.3), "hoja")
            continue
        _hoja(m, s, fino)
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
