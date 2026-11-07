"""Lince (Iveco LMV) del Ejército de Tierra con piezas animables, como el VAMTAC (coordinador, 6-nov). Teo, 7-nov-2026.

Fotos de Paula (manifest_vehiculos_et.json): MV1_lince_1.jpg (lateral puro, CC BY-SA 3.0, Outisnn) y MV1_lince_2.jpg
(3/4 delantero, columna de Linces con tirador, CC BY-SA 3.0, Outisnn).
Cotas: 2,20 m de ancho y 2,05 m de alto (catalogo.md de Paula, «Cotas para Teo»). Lo demás está MEDIDO SOBRE LA FOTO
LATERAL, con la rueda como escala (≈ 0,95 m, SIN VERIFICAR): largo de unos 4,6–4,7 m, batalla de unos 3,1–3,2 m y techo a
1,99 m (cuadra con los 2,05 de Paula). El largo y la batalla están SIN VERIFICAR.
De las fotos: capó corto con tres nervios y morro con defensa de rejilla y cabrestante (MV1_2), parabrisas inclinado de
dos hojas, cabina con cuatro puertas de ventanilla rectangular, cuerpo trasero cerrado con un cofre en el costado,
pasos de rueda con aletas negras, estribo de tubo bajo las puertas, snorkel negro en el montante izquierdo, escape
vertical atrás, retrovisores grandes, baca baja atrás, tapa redonda de la escotilla abierta detrás del tirador y
ametralladora pesada en afuste de pivote (M2 en MV1_2; MG en MV1_1).
Colores (D-N80: nada propio por debajo de #48403c): verde OTAN en 50541c con el techo en b4a080 (valor partido, como el
VAMTAC), neumáticos, aletas y metal 48403c, llantas 70784c, cristales 60546c y faros f0dc9c.
Jerarquía del FBX (mira a +Z en Unity), como el VAMTAC:
  SM_…_LODn                 carrocería (origen en el suelo)
  ├─ Rueda_1L … Rueda_2R    1 = delante; L = izquierda (+X en Blender); giran sobre su X local; las Rueda_1* giran
  │                         además sobre la vertical (dirección)
  └─ Aro                    aro de la escotilla con el pivote; gira sobre la vertical
     └─ Ametralladora       se eleva sobre su X local (origen en el muñón)
"""
import math
from montaje import Montaje, separar as _separar

PIEZAS = ["casco", "goma", "llanta", "optica", "foco", "metal", "tapa"]
COLORES = {"casco": "50541c", "goma": "48403c", "llanta": "70784c", "optica": "60546c", "foco": "f0dc9c",
           "metal": "48403c", "tapa": "88885c"}   # tapa de la escotilla un paso por debajo del techo (Lucía, 7-nov)
VALOR_PIEZAS = {"casco": ("b4a080", "50541c", "50541c")}
ABAJO_CASCO = "50541c"
L_MIN = 28.0
FACCION = "propio"
PRESUPUESTO = {0: (300, 2500), 1: (150, 1200)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.25}
EJES_Y = (-1.55, 1.6)
RR, XR = 0.475, 0.92
ARO = (0.0, 0.3, 1.99)
MUNON = (0.0, 0.15, 2.55)
PARTES = ["casco", "Rueda_1L", "Rueda_1R", "Rueda_2L", "Rueda_2R", "Aro", "Ametralladora"]
PIV = {"Aro": ARO, "Ametralladora": MUNON}
for _i, _y in enumerate(EJES_Y):
    for _s, _x in (("L", XR), ("R", -XR)):
        PIV["Rueda_%d%s" % (_i + 1, _s)] = (_x, _y, RR)
PADRES = {"Ametralladora": "Aro"}
DIRECCION = ("Rueda_1L", "Rueda_1R")


def _arco(yc, r, angulos):
    return [(yc + r * math.cos(math.radians(a)), RR + r * math.sin(math.radians(a))) for a in angulos]


def semiancho(z):
    """Flancos rectos hasta la cintura (1,35 m) y techo un poco recogido (MV1_2)."""
    return 1.0 if z <= 1.35 else 1.0 - 0.1 * (z - 1.35) / 0.64


def construir(nombre="SM_Lince", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    # ---- casco por perfil lateral (MV1_1): defensa, capó, parabrisas, techo y pasos de rueda
    perfil = [(-2.3, 0.62), (-2.3, 0.95), (-2.08, 0.97), (-2.05, 1.18), (-1.05, 1.33), (-0.68, 1.99), (2.25, 1.99),
              (2.3, 1.85), (2.3, 0.62)]
    perfil += _arco(EJES_Y[1], 0.56, (15, 50, 90, 130, 165)) + _arco(EJES_Y[0], 0.56, (15, 50, 90, 130, 165))
    m.perfil_extruido(perfil, semiancho, cortes_z=(1.35,))
    # aletas negras de los pasos de rueda
    for yc in EJES_Y:
        pts = _arco(yc, 0.6, (5, 45, 90, 135, 175))
        for s in (-1, 1):
            for (y0, z0), (y1, z1) in zip(pts, pts[1:]):
                m.barra((s * 1.03, y0, z0), (s * 1.03, y1, z1), 0.1, 0.14, "goma")
    # ---- ruedas (piezas animables)
    for i, y in enumerate(EJES_Y):
        for lado, s in (("L", 1), ("R", -1)):
            m.parte("Rueda_%d%s" % (i + 1, lado))
            m.cilindro((s * XR, y, RR), RR, 0.33, "X", 14, "goma")
            m.cilindro((s * (XR + 0.17), y, RR), 0.27, 0.04, "X", 10, "llanta")
            if fino:
                m.cilindro((s * (XR + 0.19), y, RR), 0.1, 0.04, "X", 8, "metal")     # buje
    m.parte("casco")
    # ---- cristales: parabrisas de dos hojas y ventanillas de las cuatro puertas
    n = (0.0, -0.876, 0.482)                          # normal del plano del parabrisas, para separarlo 2 cm del casco
    p0, p1 = (-1.0, 1.42), (-0.72, 1.93)
    m.barra((0, p0[0] + n[1] * 0.02, p0[1] + n[2] * 0.02), (0, p1[0] + n[1] * 0.02, p1[1] + n[2] * 0.02), 1.6, 0.03,
            "optica")
    m.barra((0, p0[0] + n[1] * 0.04, p0[1] + n[2] * 0.04), (0, p1[0] + n[1] * 0.04, p1[1] + n[2] * 0.04), 0.08, 0.02,
            "metal")                                  # parteluz
    for s in (-1, 1):
        for yc, ly in ((-0.33, 0.6), (0.6, 0.66)):
            m.caja((s * (semiancho(1.58) + 0.01), yc, 1.58), (0.03, ly, 0.34), "optica", rot_y=-s * 0.155)
        if fino:
            for y in (-0.72, 0.13, 1.02):             # juntas de las puertas
                m.caja((s * 1.005, y, 1.0), (0.02, 0.04, 0.7), "metal")
            m.caja((s * 1.02, 1.72, 1.4), (0.04, 0.85, 0.65), "casco")              # cofre del cuerpo trasero
            m.barra((s * 1.1, -0.95, 0.42), (s * 1.1, 0.95, 0.42), 0.07, 0.07, "metal")   # estribo de tubo
            for y in (-0.85, 0.0, 0.85):
                m.barra((s * 1.1, y, 0.42), (s * 0.98, y, 0.62), 0.05, 0.05, "metal")
        # retrovisores grandes (MV1_1, MV1_2)
        m.caja((s * 1.17, -0.78, 1.52), (0.07, 0.2, 0.28) if fino else (0.12, 0.25, 0.3), "metal")
        if fino:
            m.barra((s * 0.98, -0.72, 1.45), (s * 1.14, -0.78, 1.45), 0.04, 0.04, "metal")
    # ---- morro: tres nervios del capó, faros, defensa de rejilla y cabrestante (MV1_2)
    if fino:
        for x in (-0.35, 0.0, 0.35):
            m.barra((x, -1.95, 1.21), (x, -1.15, 1.33), 0.08, 0.04, "casco")
    for s in (-1, 1):
        m.cilindro((s * 0.72, -2.08, 1.06), 0.08, 0.04, "Y", 8, "foco")
        m.barra((s * 0.62, -2.45, 0.62), (s * 0.62, -2.45, 1.25), 0.07, 0.07, "metal")       # montantes de la defensa
        m.barra((s * 0.62, -2.45, 1.25), (s * 0.62, -2.1, 1.25), 0.07, 0.07, "metal")
        if fino:
            m.caja((s * 0.62, -2.45, 0.95), (0.3, 0.02, 0.55), "metal")              # rejilla
    m.barra((-0.62, -2.45, 1.25), (0.62, -2.45, 1.25), 0.07, 0.07, "metal")
    m.caja((0, -2.38, 0.76), (0.5, 0.2, 0.26), "metal")                                 # cabrestante
    # ---- snorkel en el montante izquierdo (+X) y escape vertical atrás
    m.barra((0.93, -1.2, 1.28), (0.95, -0.82, 2.0), 0.14, 0.14, "metal")
    m.cilindro((0.95, -0.9, 2.06), 0.09, 0.3, "Y", 8, "metal")
    if fino:
        m.cilindro((0.88, 2.36, 1.25), 0.05, 1.3, "Z", 6, "metal")
        m.caja((0.85, 2.3, 0.45), (0.3, 0.03, 0.35), "goma")                        # faldilla
        for s in (-1, 1):                                                               # baca baja atrás
            m.caja((s * 0.8, 1.6, 2.06), (0.05, 1.2, 0.05), "metal")
        for y in (1.05, 1.6, 2.15):
            m.caja((0, y, 2.06), (1.65, 0.05, 0.05), "metal")
    # tapa redonda de la escotilla, abierta detrás del tirador (MV1_2)
    # inclinada 45° hacia atrás (en la foto está casi vertical): de canto caía al costado oscuro y desde arriba se leía
    # como un agujero negro detrás del tirador
    antes = set(m.bm.verts)
    m.cilindro((0, ARO[1] + 0.6, 2.4), 0.4, 0.05, "Y", 12, "tapa")             # 0,8 m de diámetro
    from mathutils import Matrix, Vector
    giro, eje = Matrix.Rotation(math.radians(-45), 3, "X"), Vector((0, ARO[1] + 0.55, 2.03))
    for v in set(m.bm.verts) - antes:
        v.co = giro @ (v.co - eje) + eje
    m.caja((0, ARO[1] + 0.55, 2.03), (0.25, 0.1, 0.08), "metal")                      # bisagra
    # ---- aro y pivote (pieza animable)
    m.parte("Aro")
    ax, ay, az = ARO
    m.cilindro((ax, ay, az + 0.06), 0.45, 0.12, "Z", 12, "metal")
    m.caja((ax, ay - 0.15, az + 0.3), (0.12, 0.12, 0.45), "metal")                     # pivote
    # ---- ametralladora pesada (pieza animable)
    m.parte("Ametralladora")
    mx, my, mz = MUNON
    m.caja((mx, my - 0.2, mz), (0.22, 0.6, 0.22), "metal")                             # cajón (2 px)
    m.cilindro((mx, my - 1.05, mz + 0.03), 0.03 if fino else 0.1, 1.1, "Y", 6, "metal")
    if fino:
        m.caja((mx, my + 0.15, mz - 0.05), (0.16, 0.12, 0.12), "metal")               # empuñaduras
        m.caja((mx + 0.18, my - 0.15, mz - 0.05), (0.14, 0.3, 0.2), "metal")         # caja de munición
    return m.objeto(nombre)


def separar(o, lod):
    return _separar(o, PARTES, PIV, PADRES)
