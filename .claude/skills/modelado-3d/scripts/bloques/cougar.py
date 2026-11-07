"""AS532 Cougar de las FAMET con piezas animables, como el NH90 (coordinador, 6-nov). Teo, 7-nov-2026.

Fotos de Paula (manifest_vehiculos_et.json): MV2_cougar_1.jpg (lateral izquierdo en vuelo, ET-670 de la UME, CC BY-SA
4.0, Bene Riobó) y MV2_cougar_2.jpg (costado derecho en plataforma, camuflaje del ET, dominio público, US Army NG).
Cotas (catalogo.md de Paula, «Cotas para Teo»): 15,53 m de largo, 4,92 m de alto y rotor de 15,6 m. Lo demás está MEDIDO
SOBRE LA FOTO LATERAL con el largo como escala y está SIN VERIFICAR: cabina de 2,0 m de alto, mástil a 2,6 m por
delante del centro, cubierta de motores a 3,3 m, rotor de cola de unos 3,0 m y semiancho del fuselaje de 1,05 m.
De las fotos: morro corto y romo con radomo negro, cabina de pilotos acristalada, cabina de carga cuadrada con puerta
corredera de dos ventanillas, cubierta de motores con las dos tomas de rejilla delante y los escapes redondos a los
lados, caja de transmisión con el rotor de 4 palas, flotadores laterales (sponsons) con el tren principal de ruedas
gemelas, rueda de morro doble, cono de cola que sube hasta la deriva y estabilizador a un lado (izquierdo en
MV2_cougar_1). Rotor de cola de 5 palas: el número sale de la foto (borrosa); el lado (derecho, −X) es SIN VERIFICAR.
Colores: camuflaje del ET (MV2_cougar_2) en 50541c con manchas 845c28 y techo b4a080 (valor partido, como el NH90);
radomo e interior 48403c, cristales 60546c, palas 94948c (sin el naranja de la UME de MV2_1: el juego es del ET).
D-N80: nada propio por debajo de #48403c.
Jerarquía del FBX (mira a +Z en Unity), como el NH90:
  SM_…_LODn          fuselaje (origen en el suelo)
  ├─ RotorPrincipal  gira sobre su vertical
  ├─ RotorCola       gira sobre su X local
  └─ DiscoRotor      disco de una cara (abanico de 16 triángulos) para el rotor en marcha
"""
import math
from montaje import Montaje, separar as _separar

PIEZAS = ["casco", "mancha", "optica", "interior", "pala", "cubo", "goma", "metal", "disco"]
COLORES = {"casco": "50541c", "mancha": "845c28", "optica": "60546c", "interior": "48403c", "pala": "94948c",
           "cubo": "845c28", "goma": "48403c", "metal": "605c48", "disco": "94948c"}
VALOR_PIEZAS = {"casco": ("b4a080", "50541c", "50541c")}
ABAJO_CASCO = "50541c"
L_MIN = 28.0
FACCION = "propio"
PRESUPUESTO = {0: (300, 4000), 1: (200, 2500)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.25}
HUB = (0.0, -2.6, 4.3)
COLA = (-0.25, 7.3, 4.05)
PARTES = ["fuselaje", "RotorPrincipal", "RotorCola", "DiscoRotor"]
PIV = {"RotorPrincipal": HUB, "RotorCola": COLA, "DiscoRotor": HUB}
PERFIL = [(-1.0, -0.7), (-0.75, -1.0), (0.75, -1.0), (1.0, -0.7), (1.0, 0.6), (0.7, 1.0), (-0.7, 1.0), (-1.0, 0.6)]
OCT = [(-1, -0.6), (-0.6, -1), (0.6, -1), (1, -0.6), (1, 0.6), (0.6, 1), (-0.6, 1), (-1, 0.6)]


def construir(nombre="SM_Cougar", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    # ---- fuselaje: morro, cabina de pilotos, cabina de carga y cono de cola (y, semiancho, semialto, centro z)
    m.loft(PERFIL, [(-7.76, 0.25, 0.25, 0.95), (-7.4, 0.65, 0.55, 1.1), (-6.6, 0.95, 0.88, 1.35),
                    (-5.5, 1.05, 0.98, 1.3), (1.1, 1.05, 0.98, 1.3), (2.0, 0.6, 0.6, 1.75), (5.7, 0.3, 0.45, 2.45),
                    (7.0, 0.22, 0.35, 2.6), (7.77, 0.18, 0.3, 2.7)], "casco")
    # cubierta de motores y caja de transmisión
    m.loft(PERFIL, [(-6.4, 0.3, 0.1, 2.3), (-5.6, 0.85, 0.55, 2.75), (0.3, 0.85, 0.55, 2.75), (1.6, 0.4, 0.25, 2.55)],
           "casco")
    m.loft(PERFIL, [(-3.8, 0.3, 0.1, 3.25), (-3.3, 0.7, 0.35, 3.35), (-1.6, 0.7, 0.35, 3.35), (-0.8, 0.3, 0.1, 3.3)],
           "casco")
    m.cilindro((0.0, HUB[1], 3.95), 0.25, 0.7, "Z", 8, "metal")                       # mástil
    for s in (-1, 1):
        m.caja((s * 0.78, -5.1, 3.0), (0.25, 0.8, 0.45), "interior")                  # tomas de rejilla
        m.cilindro((s * 0.88, -2.9, 2.95), 0.3, 0.35, "X", 8, "interior")             # escapes redondos
    m.cilindro((0, -7.62, 0.95), 0.32, 0.3, "Y", 8, "interior", r2=0.18)              # radomo negro
    # ---- cristales: parabrisas, laterales de los pilotos y ventanillas de la cabina de carga
    m.caja((0, -7.0, 1.95), (1.6, 0.06, 0.8), "optica", rot_x=-0.7)
    for s in (-1, 1):
        m.caja((s * 1.0, -6.25, 1.75), (0.06, 1.1, 0.75), "optica")
        m.caja((s * 1.065, -2.65, 1.2), (0.04, 1.45, 1.75), "casco")                  # puerta corredera
        for y in (-3.0, -2.3):
            m.caja((s * 1.09, y, 1.6), (0.03, 0.42, 0.45), "optica")
        for y, h in ((-4.4, 0.45), (-0.9, 0.35), (0.25, 0.35)):
            m.caja((s * 1.06, y, 1.65), (0.03, 0.45, h), "optica")
        if fino:
            m.caja((s * 1.1, -2.65, 2.15), (0.06, 2.2, 0.05), "metal")              # carril de la puerta
    # ---- manchas del camuflaje del ET (MV2_cougar_2)
    for s in (-1, 1):
        for x, y, z, w, h in ((1.04, -6.0, 1.3, 1.0, 1.0), (1.075, -3.8, 1.0, 1.0, 1.2), (1.075, -0.3, 1.5, 1.4, 0.9),
                              (0.5, 3.5, 2.2, 1.6, 0.35)):
            m.caja((s * x, y, z), (0.02, w, h), "mancha")
        m.caja((s * 0.86, -4.2, 2.85), (0.02, 1.4, 0.4), "mancha")
    m.caja((0, -1.5, 3.31), (1.2, 1.8, 0.02), "mancha")
    # ---- flotadores laterales con el tren principal de ruedas gemelas y rueda de morro doble
    for s in (-1, 1):
        m.loft(OCT, [(-2.0, 0.12, 0.12, 1.0), (-1.5, 0.45, 0.38, 1.0), (0.6, 0.45, 0.38, 1.0), (1.2, 0.15, 0.15, 1.05)],
               "casco")
        m.t.bm.verts.ensure_lookup_table()
        for v in list(m.t.bm.verts)[-32:]:
            v.co.x += s * 1.45
        for dx in (-0.2, 0.2):
            m.cilindro((s * 1.5 + dx, -0.35, 0.32), 0.32, 0.22, "X", 10, "goma")
        m.barra((s * 1.5, -0.35, 0.32), (s * 1.45, -0.35, 0.75), 0.12, 0.12, "metal")
    for x in (-0.15, 0.15):
        m.cilindro((x, -5.8, 0.25), 0.25, 0.14, "X", 10, "goma")
    m.barra((0, -5.8, 0.25), (0, -5.75, 0.5), 0.1, 0.1, "metal")
    # ---- deriva y estabilizador a la izquierda (+X, MV2_cougar_1)
    m.barra((0, 6.5, 2.55), (0, 7.45, 4.25), 0.25, 1.1, "casco")
    m.caja((0.9, 6.55, 2.25), (1.6, 0.6, 0.1), "casco", rot_y=-0.06)
    # ---- rotor principal: 4 palas
    m.parte("RotorPrincipal")
    hx, hy, hz = HUB
    m.cilindro((hx, hy, hz), 0.55, 0.35, "Z", 10, "cubo")
    m.cilindro((hx, hy, hz + 0.3), 0.45, 0.08, "Z", 10, "cubo")                       # sombrerete (MV2_2)
    R, cuerda, grosor = 7.8, (0.6 if fino else 0.8), (0.07 if fino else 0.12)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        m.barra((hx + 0.5 * math.cos(a), hy + 0.5 * math.sin(a), hz),
                (hx + R * math.cos(a), hy + R * math.sin(a), hz - 0.15), grosor, cuerda, "pala")
    # ---- rotor de cola: 5 palas a la derecha de la deriva (−X)
    m.parte("RotorCola")
    tx, ty, tz = COLA
    m.cilindro((tx, ty, tz), 0.2, 0.3, "X", 8, "cubo")
    for k in range(5):
        a = math.radians(18 + 72 * k)
        m.barra((tx - 0.05, ty + 0.18 * math.cos(a), tz + 0.18 * math.sin(a)),
                (tx - 0.05, ty + 1.5 * math.cos(a), tz + 1.5 * math.sin(a)), 0.24, 0.06 if fino else 0.1, "pala")
    # ---- disco del rotor en marcha, en abanico para que limpiar() no lo borre (como el NH90)
    m.parte("DiscoRotor")
    c0 = m.bm.verts.new((hx, hy, hz - 0.08))
    vs = [m.bm.verts.new((hx + R * math.cos(2 * math.pi * k / 16), hy + R * math.sin(2 * math.pi * k / 16), hz - 0.08))
          for k in range(16)]
    m.t._marcar([m.bm.faces.new((c0, vs[k], vs[(k + 1) % 16])) for k in range(16)], "disco")
    return m.objeto(nombre)


def separar(o, lod):
    return _separar(o, PARTES, PIV)
