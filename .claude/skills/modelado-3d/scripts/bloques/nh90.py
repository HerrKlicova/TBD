"""NH90 TTH de las FAMET (prototipo de la noche del 7-nov, plan de Lucía §2). Teo, 6-nov-2026, 23:55.

Fotos de Paula: A2_nh90_1.jpg (de lado, ET-822) y A2_nh90_2.jpg (3/4 desde arriba, ET-805). De ellas: morro romo con
radar abajo y parabrisas grande, cabina de sección casi cuadrada con las esquinas achaflanadas, joroba de motores
con las dos tomas y escapes, rotor de 4 palas con cubo grande, puertas correderas a los dos lados (abiertas, con el
tirador de puerta), patines laterales (sponsons) con el tren principal, rueda de morro doble, cono de cola que se
estrecha y sube, deriva inclinada con el rotor de cola de 4 palas a la IZQUIERDA y el estabilizador a la derecha.
Medidas (Paula: Wikipedia en inglés): fuselaje de 16,13 m y rotor de 16,3 m de diámetro. Lo demás, estimado sobre las
fotos y SIN VERIFICAR: cabina de 3,0 m de ancho y 2,4 de alto, rotor de cola de 3,2 m, altura total de 5,2 m.
Armas de puerta: dos FN MAG 58 (7,62), una en cada puerta, en afuste de pivote (montaje SIN VERIFICAR).
Colores (Lucía): 50541c con los techos en b4a080 (valor partido del casco) y manchas 845c28 del camuflaje en los
costados (el pardo de la foto); cristales 60546c y ruedas 48403c (D-N80 de Bruno: ningún albedo propio bajo
L* 28, así que nada de 181c30 ni 303424); palas 94948c (las puntas rojas de la foto no: nada de rojos); interior de la
cabina 48403c.
PIEZAS ANIMABLES (FBX con jerarquía, mira a +Z en Unity):
  SM_NH90_LODn       fuselaje (origen en el suelo, bajo el mástil)
  ├─ RotorPrincipal  cubo y 4 palas; origen en el cubo; gira sobre su Y de Unity (la vertical). Sentido real: antihorario
  │                  visto desde arriba (SIN VERIFICAR para el NH90).
  ├─ RotorCola       cubo y 4 palas; origen en el cubo; gira sobre su X local (el eje lateral).
  └─ DiscoRotor      disco de una cara (16 lados, 94948c) para el rotor en marcha: se enciende por encima de cierta
                     velocidad y entonces se ocultan las palas (Lucía, 23:29). Mira hacia arriba: una sola cara.
Las MAG van fijas en el fuselaje (a 12–24 px no se ve si apuntan).
"""
import math, os
from montaje import Montaje, separar as _separar
import acabado as A

VAR = 1   # pintura lisa, como el real (Eugenio 7-nov 09:20 rechazó el camuflaje por caras); TBD_VARIANTE es ahora el
          # grado de uso de la textura horneada (textura_real.py): 0 parque, 1 campaña, 2 árido, 3 embarrado, 4 castigado
TEXTURA_REAL = True
PIEZAS = ["casco", "mancha", "optica", "interior", "pala", "cubo", "goma", "metal", "arma", "disco"]
COLORES = {"casco": "50541c", "mancha": "845c28", "optica": "48403c", "interior": "48403c", "pala": "94948c",
           "cubo": "845c28", "goma": "48403c", "metal": "605c48", "arma": "48403c", "disco": "94948c"}
VALOR_PIEZAS = A.valores(VAR, {"casco": ("70784c", "50541c", "50541c")})   # caras de abajo fijas: nunca 442c0c (horda)
COLORES = A.colores(VAR, COLORES)
if A.TABLA[VAR][0]:   # las manchas pintadas de la foto toman el pardo del acabado
    COLORES["mancha"] = A.TABLA[VAR][2]["camo_a"][1]
PIEZAS = PIEZAS + [p for p in A.TABLA[VAR][2] if p not in PIEZAS]
FACCION = "propio"
PRESUPUESTO = {0: (300, 4000), 1: (200, 2500)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.25}
PARTES = ["fuselaje", "RotorPrincipal", "RotorCola", "DiscoRotor"]
PIV = {"RotorPrincipal": (0.0, -1.0, 4.6), "RotorCola": (0.42, 7.75, 4.55), "DiscoRotor": (0.0, -1.0, 4.6)}
# perfil de la cabina (x, z): casi cuadrado con las esquinas achaflanadas, normalizado a semiancho 1,5 y semialto 1,2
PERFIL = [(-1.5, -0.85), (-1.15, -1.2), (1.15, -1.2), (1.5, -0.85), (1.5, 0.75), (1.1, 1.2), (-1.1, 1.2), (-1.5, 0.75)]


def construir(nombre="SM_NH90", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    # ---- fuselaje: morro, cabina y cono de cola (y, escala x, escala z, centro z)
    m.loft([(x / 1.5, z / 1.2) for x, z in PERFIL], [
        (-8.05, 0.30, 0.28, 1.30), (-7.70, 0.75, 0.55, 1.40), (-6.70, 1.45, 1.05, 1.75),
        (1.30, 1.50, 1.15, 1.85), (2.80, 0.85, 0.70, 2.50), (7.30, 0.32, 0.36, 3.15), (8.05, 0.26, 0.34, 3.25)],
        "casco")
    # joroba de motores y caja de transmisión
    m.loft([(x / 1.5, z / 1.2) for x, z in PERFIL], [
        (-4.4, 0.45, 0.10, 3.05), (-3.6, 0.95, 0.42, 3.25), (1.6, 0.95, 0.42, 3.25), (2.9, 0.45, 0.15, 3.1)], "casco")
    m.cilindro((0.0, -1.0, 4.0), 0.32, 1.0, "Z", 8, "metal")                      # mástil
    for s in (-1, 1):
        m.caja((s * 0.95, -3.2, 3.35), (0.08, 0.7, 0.45), "optica")                # tomas de aire
        m.caja((s * 0.98, 1.3, 3.4), (0.35, 0.8, 0.3), "interior", rot_z=s * 0.3)  # escapes
    # ---- parabrisas y ventanillas
    m.caja((0, -7.25, 2.3), (2.0, 0.06, 0.95), "optica", rot_x=-0.85)
    for s in (-1, 1):
        m.caja((s * 1.48, -6.3, 2.25), (0.06, 1.3, 0.7), "optica")
        m.caja((s * 1.53, 0.0, 2.25), (0.05, 0.55, 0.45), "optica")
    m.caja((0, -7.85, 0.95), (0.6, 0.5, 0.4), "casco")                             # radar del morro
    # ---- puertas correderas abiertas (las dos) con su MAG 58 en pivote
    for s in (-1, 1):
        m.caja((s * 1.505, -3.2, 1.75), (0.04, 1.9, 1.65), "interior")
        m.caja((s * 1.56, -1.15, 1.8), (0.08, 1.9, 1.6), "casco")                   # hoja corrida hacia atrás
        m.barra((s * 1.35, -3.0, 1.85), (s * 2.35, -3.75, 1.75), 0.09 if fino else 0.14, 0.09 if fino else 0.14,
                "arma")
        m.caja((s * 1.55, -3.15, 1.85), (0.22, 0.35, 0.2), "arma")                  # cajón de la MAG
        if fino:
            m.barra((s * 1.45, -3.0, 0.95), (s * 1.6, -3.1, 1.75), 0.06, 0.06, "metal")   # afuste
    # ---- manchas del camuflaje en los costados (pardo de la foto)
    for s in (-1, 1):
        for y, z, w, h in ((-5.6, 1.4, 1.3, 0.9), (-0.2, 1.1, 1.6, 0.6), (0.9, 2.2, 1.0, 0.5)):
            m.caja((s * 1.515, y, z), (0.02, w, h), "mancha")
    m.caja((0, -1.2, 3.68), (1.2, 2.0, 0.02), "mancha")                             # techo de la joroba
    # ---- patines laterales con el tren principal y rueda de morro
    for s in (-1, 1):
        m.loft([(x / 1.5, z / 1.2) for x, z in PERFIL], [
            (-1.6, 0.12, 0.15, 0.95), (-1.2, 0.30, 0.32, 0.95), (1.6, 0.30, 0.32, 0.95), (2.1, 0.12, 0.15, 1.0)],
            "casco")
        m.t.bm.verts.ensure_lookup_table()
        for v in list(m.t.bm.verts)[-32:]:
            v.co.x += s * 1.6
        m.cilindro((s * 1.75, 0.6, 0.36), 0.36, 0.28, "X", 10, "goma")
        m.barra((s * 1.65, 0.6, 0.36), (s * 1.6, 0.6, 0.75), 0.12, 0.12, "metal")
    for s in (-1, 1):
        m.cilindro((s * 0.16, -5.6, 0.28), 0.28, 0.16, "X", 10, "goma")
    m.barra((0, -5.6, 0.28), (0, -5.6, 0.7), 0.12, 0.12, "metal")
    # ---- deriva inclinada y estabilizador a la derecha (−X)
    m.barra((0, 7.25, 3.1), (0, 7.95, 5.35), 0.28, 1.25, "casco")
    m.caja((-1.15, 7.55, 3.55), (2.0, 0.75, 0.12), "casco", rot_y=0.08)
    # ---- rotor principal (pieza animable)
    m.parte("RotorPrincipal")
    hx, hy, hz = PIV["RotorPrincipal"]
    m.cilindro((hx, hy, hz), 0.6, 0.4, "Z", 10, "cubo")
    m.cilindro((hx, hy, hz + 0.32), 0.25, 0.25, "Z", 8, "cubo")
    R, cuerda, grosor = 8.15, (0.6 if fino else 0.8), (0.07 if fino else 0.12)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        m.barra((hx + 0.5 * math.cos(a), hy + 0.5 * math.sin(a), hz),
                (hx + R * math.cos(a), hy + R * math.sin(a), hz - 0.15), grosor, cuerda, "pala")
    # ---- rotor de cola, a la izquierda de la deriva (+X)
    m.parte("RotorCola")
    tx, ty, tz = PIV["RotorCola"]
    m.cilindro((tx, ty, tz), 0.22, 0.3, "X", 8, "cubo")
    for k in range(4):
        a = math.radians(20 + 90 * k)
        m.barra((tx + 0.08, ty + 0.2 * math.cos(a), tz + 0.2 * math.sin(a)),
                (tx + 0.08, ty + 1.6 * math.cos(a), tz + 1.6 * math.sin(a)), 0.25, 0.06 if fino else 0.1, "pala")
    # ---- disco del rotor en marcha (Lucía, 23:29): una sola cara de 16 lados a la altura del buje, en 94948c. Marcos
    # lo muestra por encima de cierta velocidad (tramado al 25 % en el shader) y oculta entonces las palas
    m.parte("DiscoRotor")
    # En abanico (16 triángulos con vértice central): limpiar() borra las islas de menos de 4 caras y una cara suelta
    # desaparecía del FBX. A la vista es la misma cara plana.
    c0 = m.bm.verts.new((hx, hy, hz - 0.08))
    vs = [m.bm.verts.new((hx + R * math.cos(2 * math.pi * k / 16), hy + R * math.sin(2 * math.pi * k / 16), hz - 0.08))
          for k in range(16)]
    m.t._marcar([m.bm.faces.new((c0, vs[k], vs[(k + 1) % 16])) for k in range(16)], "disco")
    o = m.objeto(nombre)
    A.aplicar(o, lod, VAR, base=("casco",))
    return o


def separar(o, lod):
    return _separar(o, PARTES, PIV)
