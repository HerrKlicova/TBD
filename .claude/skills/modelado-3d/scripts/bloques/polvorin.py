"""Polvorín: iglú semienterrado con puerta blindada (D-N173; Álvaro, betera-nivel.md §5.4: 2 iglús en (107; 73) y (107;
88), yaw 90, con talud de tierra; frente B2). Teo (3), 8-nov.

Sin foto de Bétera. Tipo genérico de iglú de munición: bóveda de hormigón cubierta de tierra (un túmulo con hierba), con
el frente de hormigón a la vista, aletas inclinadas a los lados y una puerta blindada de dos hojas. Medidas SIN
VERIFICAR: 8 m de ancho, 14 m de fondo, túmulo de 4,2 m; puerta de 3 × 3 m.
Colores: tierra 88885c (hierba) o b4a080 (tierra desnuda), frente y aletas 94948c, puerta 6c7874 con nervios 605c48,
interior 48403c, cajas de munición 70784c, sacos 605c48, red 50541c, venteo 6c7874.
Disposición: origen en el suelo, en el centro de la planta; el FRENTE con la puerta en −Y de Blender (= +Z de Unity
con yaw 0). Jerarquía: SM_…_LODn con Puerta_O y Puerta_E (bisagras en x = ∓1,5 en el frente); ABRIR = girar −90° (O)
y +90° (E) sobre la vertical, hacia fuera.
Variantes (TBD_VARIANTE % 5): 0 túmulo con hierba; 1 tierra desnuda (recién hecho); 2 frente reforzado con sacos;
3 puerta abierta con cajas de munición dentro; 4 red de camuflaje y dos venteos en el túmulo.
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["tierra", "frente", "puerta", "nervio", "interior", "caja", "sacos", "red", "metal"]
COLORES = {"tierra": "b4a080" if VAR == 1 else "88885c", "frente": "94948c", "puerta": "6c7874", "nervio": "605c48",
           "interior": "48403c", "caja": "70784c", "sacos": "605c48", "red": "50541c", "metal": "6c7874"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.6
PRESUPUESTO = {0: (60, 2500), 1: (30, 1200)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
AN, FO, HT = 8.0, 14.0, 4.2
PARTES = ["iglu", "Puerta_O", "Puerta_E"]
PIV = {"Puerta_O": (-1.5, -FO / 2 - 0.15, 0.0), "Puerta_E": (1.5, -FO / 2 - 0.15, 0.0)}
GIRO_PRUEBA = {"Puerta_O": ("Z", -60), "Puerta_E": ("Z", 60)}


def construir(nombre="SM_Polvorin", lod=0):
    v = VAR
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    g = 0.08 if fino else 0.34
    n = 9 if fino else 6
    # túmulo: sección de bóveda ancha (con el talud) que se estrecha y baja hacia atrás
    perfil = [(-1.0, 0.0), (1.0, 0.0)] + [(math.cos(math.pi * k / n) * 0.92, math.sin(math.pi * k / n))
                                          for k in range(1, n)]
    secs = [(-FO / 2 + 0.3, AN / 2 + 3.0, HT, 0.0), (FO / 2 - 2.0, AN / 2 + 3.0, HT, 0.0),
            (FO / 2 + 2.5, AN / 2 + 0.5, 1.0, 0.0)]
    m.loft(perfil, secs, "tierra")
    # frente de hormigón (sobresale del túmulo) y aletas inclinadas
    m.caja((0, -FO / 2, 2.4), (AN, 0.6, 4.8), "frente")
    for s in (-1, 1):
        m.caja((s * (AN / 2 + 1.4), -FO / 2 + 0.1, 1.6), (3.0, 0.5, 3.2), "frente", cuña=None, rot_z=s * 0.5)
    m.caja((0, -FO / 2 - 0.3, 3.4), (3.6, 0.1, 0.4), "nervio")                 # dintel
    m.caja((0, -FO / 2 - 0.31, 1.5), (3.0, 0.04, 3.0), "interior")               # hueco
    if v == 3:                                       # cajas de munición en el umbral
        for k in range(3):
            m.caja((-0.8 + 0.8 * k, -FO / 2 - 0.9, 0.3 + 0.35 * (k % 2)), (0.7, 0.45, 0.32), "caja", rot_z=0.2 * k)
    if v == 2:                                       # sacos delante del frente, dejando la puerta libre
        for s in (-1, 1):
            m.caja((s * 2.8, -FO / 2 - 1.0, 0.6), (2.0, 0.9, 1.2), "sacos", rot_z=0.04 * s)
    if v == 4:                                       # red y venteos
        m.caja((0, -1.0, HT + 0.12), (AN + 2.0, FO * 0.7, 0.08), "red", rot_x=0.04)
        for y in (-2.0, 3.0):
            m.cilindro((0.0, y, HT + 0.5), 0.2 if fino else 0.25, 1.2, "Z", 8, "metal")
    for nom, s in (("Puerta_O", -1), ("Puerta_E", 1)):
        m.parte(nom)
        xc = s * 0.75
        y = -FO / 2 - 0.4
        if v == 3:                                   # abiertas hacia fuera
            m.caja((s * 1.5, y - 0.75, 1.5), (0.14, 1.48, 3.0), "puerta")
            continue
        m.caja((xc, y, 1.5), (1.48, 0.14, 3.0), "puerta")
        for z in (0.6, 1.5, 2.4):
            m.caja((xc, y - 0.1, z), (1.3, 0.06, 0.12 if fino else 0.34), "nervio")
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
