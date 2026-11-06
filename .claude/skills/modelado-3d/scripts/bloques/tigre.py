"""Helicóptero de ataque Tigre HAD (FAMET) con piezas animables (coordinador, 6-nov 23:39). Teo, 6-nov-2026.

Medidas (aerospaceweb.org, «Eurocopter Tiger»): 14,08 m de largo sin rotores (UHT; 15,0 el HAP), 3,81 m hasta la cabeza
del rotor, rotor principal de 13,00 m, envergadura de los planos de 4,52 m; cañón de 30 mm en torreta bajo el morro.
De las fotos de Paula (A3_tigre_1.jpg, 3/4 en despegue, y A3_tigre_2.jpg, 3/4 en cubierta; las dos de dominio público,
US Navy, USS Kearsarge 2016): fuselaje estrecho con dos cabinas en tándem (la de atrás más alta), morro afilado oscuro
con la torreta del cañón debajo, joroba de los motores con tomas a los lados, cuatro palas, planos cortos con pilones
(lanzacohetes fuera y misiles dentro), tren fijo de rueda de cola con las principales en brazos, cono de cola con
estabilizador horizontal con placas en los extremos, deriva con el rotor de cola de tres palas a la derecha; camuflaje
verde con manchas pardas.
SIN VERIFICAR (estimado de las fotos): secciones del fuselaje, posición del mástil (0,8 m por delante del centro), rotor
de cola de 2,7 m, tren y pilones.
Jerarquía del FBX (mira a +Z en Unity, como el NH90):
  SM_…_LODn          fuselaje
  ├─ RotorPrincipal  gira sobre su Z local (la vertical)
  ├─ RotorCola       gira sobre su X local
  ├─ DiscoRotor      disco del rotor en marcha (94948c), como el del NH90: tramado al 25 % por encima de cierta velocidad
  └─ Torreta         torreta del cañón bajo el morro; gira sobre la vertical
     └─ Canon        se eleva sobre su X local
"""
import math
from montaje import Montaje, separar as _separar

PIEZAS = ["casco", "mancha", "optica", "interior", "pala", "cubo", "goma", "metal", "arma", "disco"]
COLORES = {"casco": "50541c", "mancha": "845c28", "optica": "60546c", "interior": "48403c", "pala": "94948c",
           "cubo": "845c28", "goma": "48403c", "metal": "605c48", "arma": "48403c", "disco": "94948c"}
VALOR_PIEZAS = {"casco": ("b4a080", "50541c", "50541c")}
ABAJO_CASCO = "50541c"
L_MIN = 28.0
FACCION = "propio"
PRESUPUESTO = {0: (300, 4000), 1: (200, 2500)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.25}
HUB = (0.0, -0.8, 3.65)
COLA = (-0.22, 6.75, 3.1)
TORRETA = (0.0, -5.3, 0.82)
CANON = (0.0, -5.42, 0.72)
PARTES = ["fuselaje", "RotorPrincipal", "RotorCola", "DiscoRotor", "Torreta", "Canon"]
PIV = {"RotorPrincipal": HUB, "RotorCola": COLA, "DiscoRotor": HUB, "Torreta": TORRETA, "Canon": CANON}
PADRES = {"Canon": "Torreta"}
OCT = [(-1, -0.6), (-0.6, -1), (0.6, -1), (1, -0.6), (1, 0.6), (0.6, 1), (-0.6, 1), (-1, 0.6)]


def construir(nombre="SM_Tigre", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    # ---- fuselaje estrecho (y, semiancho, semialto, centro z) y cono de cola
    m.loft(OCT, [(-7.04, 0.12, 0.14, 1.22), (-6.6, 0.36, 0.36, 1.3), (-5.0, 0.5, 0.55, 1.5), (-3.0, 0.6, 0.66, 1.6),
                 (-1.2, 0.64, 0.68, 1.65), (0.8, 0.55, 0.6, 1.72), (2.0, 0.3, 0.32, 1.98), (6.3, 0.17, 0.2, 2.25),
                 (7.04, 0.14, 0.17, 2.3)], "casco")
    # cabinas en tándem (la de atrás más alta) y joroba de los motores
    m.loft(OCT, [(-5.85, 0.06, 0.05, 1.98), (-5.4, 0.3, 0.3, 2.0), (-4.4, 0.33, 0.33, 2.1), (-4.15, 0.15, 0.15, 2.15)],
           "optica")
    m.loft(OCT, [(-4.15, 0.06, 0.05, 2.25), (-3.8, 0.33, 0.36, 2.33), (-2.8, 0.35, 0.38, 2.38), (-2.5, 0.2, 0.22, 2.42)],
           "optica")
    m.loft(OCT, [(-2.4, 0.3, 0.1, 2.32), (-1.9, 0.78, 0.48, 2.32), (0.6, 0.78, 0.48, 2.32), (1.5, 0.3, 0.18, 2.22)],
           "casco")
    for s in (-1, 1):
        m.caja((s * 0.75, -1.75, 2.4), (0.06, 0.45, 0.35), "interior")               # tomas de aire
        m.caja((s * 0.6, 1.1, 2.55), (0.3, 0.6, 0.25), "interior", rot_z=s * 0.4)    # escapes hacia arriba y afuera
    m.cilindro((0.0, HUB[1], 3.15), 0.22, 0.75, "Z", 8, "metal")                      # mástil
    m.cilindro((0.0, -2.2, 2.95), 0.2, 0.25, "Z", 8, "optica")                        # visor del techo (HAD)
    m.caja((0.0, -6.75, 1.22), (0.26, 0.5, 0.26), "interior")                         # morro oscuro (foto)
    # ---- manchas pardas del camuflaje
    for s in (-1, 1):
        for y, z, w, h, x in ((-3.6, 1.35, 1.2, 0.5, 0.56), (-0.6, 1.55, 1.4, 0.6, 0.6), (3.0, 2.1, 1.6, 0.25, 0.27)):
            m.caja((s * (x + 0.01), y, z), (0.02, w, h), "mancha")
    m.caja((0, -0.6, 2.81), (0.9, 1.6, 0.02), "mancha")
    # ---- planos cortos con pilones: lanzacohetes fuera y misiles dentro
    for s in (-1, 1):
        m.caja((s * 1.4, -1.5, 1.55), (2.0, 0.8, 0.1), "casco", rot_y=s * 0.06)
        m.caja((s * 2.0, -1.5, 1.38), (0.08, 0.5, 0.25), "metal")
        m.cilindro((s * 2.0, -1.55, 1.12), 0.22, 1.6, "Y", 8, "arma")                 # lanzacohetes de 68 mm
        m.caja((s * 1.2, -1.5, 1.38), (0.08, 0.5, 0.25), "metal")
        for dx in (-0.1, 0.1):
            for dz in (0.0, -0.2):
                m.cilindro((s * 1.2 + dx, -1.5, 1.15 + dz), 0.08, 1.5 if fino else 1.6, "Y", 6, "arma")
    # ---- tren fijo: principales en brazos y rueda de cola
    for s in (-1, 1):
        m.barra((s * 0.5, -2.0, 1.0), (s * 1.0, -2.1, 0.32), 0.1, 0.1, "metal")
        m.cilindro((s * 1.05, -2.1, 0.3), 0.3, 0.18, "X", 10, "goma")
    m.barra((0, 5.5, 2.05), (0, 5.85, 0.3), 0.08, 0.08, "metal")
    m.cilindro((0, 5.85, 0.18), 0.17, 0.12, "X", 8, "goma")
    # ---- cola: estabilizador con placas y deriva
    m.caja((0, 5.6, 2.22), (2.0, 0.5, 0.08), "casco")
    for s in (-1, 1):
        m.caja((s * 1.0, 5.6, 2.3), (0.06, 0.55, 0.6), "casco")
    m.barra((0, 6.25, 2.15), (0, 6.95, 3.6), 0.14, 0.8, "casco")
    # ---- torreta del cañón de 30 mm bajo el morro
    m.parte("Torreta")
    tx, ty, tz = TORRETA
    m.cilindro((tx, ty, tz), 0.24, 0.24, "Z", 8, "arma")
    m.caja((tx, ty - 0.08, tz - 0.12), (0.3, 0.35, 0.18), "arma")
    m.parte("Canon")
    cx, cy, cz = CANON
    m.cilindro((cx, cy - 0.9, cz), 0.05 if fino else 0.09, 1.8, "Y", 6, "arma")
    # ---- rotor principal: 4 palas
    m.parte("RotorPrincipal")
    hx, hy, hz = HUB
    m.cilindro((hx, hy, hz), 0.42, 0.3, "Z", 10, "cubo")
    R, cuerda, grosor = 6.5, (0.5 if fino else 0.7), (0.06 if fino else 0.1)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        m.barra((hx + 0.4 * math.cos(a), hy + 0.4 * math.sin(a), hz),
                (hx + R * math.cos(a), hy + R * math.sin(a), hz - 0.12), grosor, cuerda, "pala")
    # ---- rotor de cola: 3 palas a la derecha de la deriva (−X)
    m.parte("RotorCola")
    tx, ty, tz = COLA
    m.cilindro((tx, ty, tz), 0.16, 0.25, "X", 8, "cubo")
    for k in range(3):
        a = math.radians(30 + 120 * k)
        m.barra((tx - 0.05, ty + 0.15 * math.cos(a), tz + 0.15 * math.sin(a)),
                (tx - 0.05, ty + 1.35 * math.cos(a), tz + 1.35 * math.sin(a)), 0.22, 0.05 if fino else 0.09, "pala")
    # ---- disco del rotor en marcha (como el NH90): abanico de 16 triángulos para que la limpieza no lo borre
    m.parte("DiscoRotor")
    c0 = m.bm.verts.new((hx, hy, hz - 0.08))
    vs = [m.bm.verts.new((hx + R * math.cos(2 * math.pi * k / 16), hy + R * math.sin(2 * math.pi * k / 16), hz - 0.08))
          for k in range(16)]
    m.t._marcar([m.bm.faces.new((c0, vs[k], vs[(k + 1) % 16])) for k in range(16)], "disco")
    return m.objeto(nombre)


def separar(o, lod):
    return _separar(o, PARTES, PIV, PADRES)
