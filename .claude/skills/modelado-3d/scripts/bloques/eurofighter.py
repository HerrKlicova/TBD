"""Eurofighter C.16 del Ejército del Aire para el apoyo aéreo (coordinador, 7-nov 01:31; para DESPUÉS del MVP). Teo, 7-nov.

Fotos de Paula en referencias-reales/aereo/: A5_eurofighter_1.jpg (de abajo, en planta) y A5_eurofighter_2.jpg (lateral,
despegando). Cotas DE MEMORIA (SIN VERIFICAR): 15,96 m de largo, 10,95 m de envergadura con las góndolas de punta de ala,
flecha del borde de ataque de unos 53°, deriva de unos 3,3 m sobre el eje del fuselaje. Forma: ala en delta baja, canards
detrás de la cabina, una sola deriva, toma de aire bajo el morro (la «sonrisa»), dos toberas, cúpula de burbuja y el
cañón BK-27 en la raíz del ala derecha. En vuelo con el tren recogido.
Colores: gris de baja visibilidad (b8b8b8 arriba y en los costados, 6c7874 en el vientre: VALOR_PIEZAS), cúpula
60546c, toberas 6c7874, interiores 48403c, bombas guiadas y Mk-82 en 88885c (oliva, como las reales), misiles d4d0cc y
depósitos en el gris del casco. SIN escarapelas ni la cruz de San Andrés: las de verdad llevan rojo (D-N80).
Piezas finas (Lucía, 7-nov): deriva, canards y misiles de 0,25 m o más en el LOD1 (≥ 2 px a ortho 26).
Disposición: morro hacia −Y de Blender (como el NH90 y el Chinook); origen en el eje del fuselaje, en el centro del avión.
Jerarquía (mira a +Z en Unity):
  SM_…_LODn      fuselaje, alas, deriva, canards y pilones (los pilones se quedan al soltar la carga)
  ├─ Carga       todo lo que cuelga de los pilones; transformación identidad: se oculta entera al soltarla
  └─ BocaCanon   punto de salida del cañón: origen en la boca del BK-27, el disparo sale hacia −Y local (el morro)
Variantes (TBD_VARIANTE % 5): 0 precisión (2 GBU-16 y el pod Litening bajo el fuselaje); 1 bombardeo de área (6 Mk-82);
2 dos misiles de crucero Taurus; 3 aire-aire (IRIS-T, Meteor y dos depósitos); 4 casi limpia, solo dos IRIS-T de
autodefensa en los pilones de fuera.
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = ["casco", "optica", "tobera", "interior", "bomba", "misil", "deposito", "boca"]
COLORES = {"casco": "b8b8b8", "optica": "60546c", "tobera": "6c7874", "interior": "48403c", "bomba": "88885c",
           "misil": "d4d0cc", "deposito": "b8b8b8", "boca": "48403c"}
# un escalón más oscuro (Lucía, 7-nov): d4d0cc es el color de la selección. Ella pidió costados en 94948c, pero
# valor_partido lo veta para las piezas propias (gris del suelo y de la horda), así que los costados se quedan en b8b8b8
VALOR_PIEZAS = {"casco": ("b8b8b8", "b8b8b8", "6c7874")}
FACCION = "propio"
PRESUPUESTO = {0: (300, 5000), 1: (200, 3000)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.25}
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
BOCA = (-0.92, -1.75, -0.22)          # boca del BK-27 en la raíz del ala derecha (−X), SIN VERIFICAR
PARTES = ["caza", "Carga", "BocaCanon"]
PIV = {"Carga": (0.0, 0.0, 0.0), "BocaCanon": BOCA}
PERFIL = [(-1.0, -0.4), (-0.7, -1.0), (0.7, -1.0), (1.0, -0.4), (1.0, 0.4), (0.6, 1.0), (-0.6, 1.0), (-1.0, 0.4)]
Z_ALA = -0.25                          # plano medio del ala (ala baja)
PILONES = (1.9, 3.1, 4.3)              # x de los pilones del ala, por lado


def _placa(m, pts, grosor, pieza, plano="XY", fijo=0.0):
    """Placa de grosor dado a partir de un polígono convexo: plano XY (alas, canards; fijo = z) o YZ (deriva; fijo = x)."""
    def v(a, b, d):
        return (a, b, fijo + d) if plano == "XY" else (fijo + d, a, b)
    arriba = [m.bm.verts.new(v(a, b, grosor / 2)) for a, b in pts]
    abajo = [m.bm.verts.new(v(a, b, -grosor / 2)) for a, b in pts]
    n = len(pts)
    caras = [m.bm.faces.new(arriba), m.bm.faces.new(abajo[::-1])]
    for i in range(n):
        j = (i + 1) % n
        caras.append(m.bm.faces.new((arriba[i], abajo[i], abajo[j], arriba[j])))
    import bmesh
    bmesh.ops.recalc_face_normals(m.bm, faces=caras)
    m.t._marcar(caras, pieza)


def _bomba(m, x, y, z, largo, r, pieza, fino, aletas=True):
    lados = 8 if fino else 6
    m.cilindro((x, y, z), r, largo * 0.7, "Y", lados, pieza)
    # Taller.cilindro en "Y": r es el radio del extremo +Y y r2 el del −Y. Los conos se meten 2 cm en el cuerpo para
    # que sus tapas no coincidan con las del cilindro (caras dobles = aristas no manifold)
    m.cilindro((x, y - largo * 0.45 + 0.02, z), r * 0.98, largo * 0.2, "Y", lados, pieza, r2=r * 0.35)   # ojiva (−Y)
    m.cilindro((x, y + largo * 0.42 - 0.02, z), r * 0.5, largo * 0.14, "Y", lados, pieza, r2=r * 0.98)
    if aletas and fino:
        m.caja((x, y + largo * 0.42, z), (r * 2.6, largo * 0.1, 0.03), pieza)
        m.caja((x, y + largo * 0.42, z), (0.03, largo * 0.1, r * 2.6), pieza)


def _misil(m, x, y, z, largo, fino):
    r = 0.07 if fino else 0.12                                       # ≥ 0,24 m de diámetro en el LOD1 (Lucía)
    m.cilindro((x, y, z), r, largo * 0.85, "Y", 6, "misil")
    m.cilindro((x, y - largo * 0.475 + 0.02, z), r * 0.98, largo * 0.1, "Y", 6, "misil", r2=0.035)
    if fino:
        m.caja((x, y + largo * 0.38, z), (0.36, 0.2, 0.025), "misil")
        m.caja((x, y + largo * 0.38, z), (0.025, 0.2, 0.36), "misil")


def construir(nombre="SM_Eurofighter", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    a = ASPECTO
    # ---- fuselaje (A5_2): morro afilado, lomo alto detrás de la cabina y cola ancha de las dos toberas
    m.loft(PERFIL, [(-7.98, 0.1, 0.1, 0.05), (-7.4, 0.24, 0.26, 0.05), (-6.3, 0.46, 0.46, 0.05),
                    (-4.8, 0.58, 0.58, 0.08), (-3.2, 0.68, 0.62, 0.08), (-1.0, 0.8, 0.6, 0.05), (2.5, 0.92, 0.58, 0.0),
                    (5.8, 0.98, 0.55, 0.0), (7.3, 0.95, 0.5, 0.0)], "casco")
    # lomo detrás de la cúpula
    m.loft(PERFIL, [(-2.4, 0.3, 0.12, 0.55), (-1.2, 0.42, 0.25, 0.6), (3.5, 0.4, 0.2, 0.55), (6.0, 0.2, 0.08, 0.5)],
           "casco")
    # toma de aire ventral (la sonrisa) con su boca oscura
    m.loft(PERFIL, [(-3.6, 0.7, 0.3, -0.62), (-2.0, 0.78, 0.36, -0.68), (2.5, 0.8, 0.32, -0.6), (4.5, 0.55, 0.15, -0.45)],
           "casco")
    m.caja((0, -3.62, -0.62), (1.24, 0.03, 0.4), "interior")
    # cúpula de burbuja
    m.loft(PERFIL, [(-4.9, 0.12, 0.06, 0.6), (-4.4, 0.38, 0.22, 0.66), (-3.4, 0.42, 0.3, 0.72), (-2.4, 0.34, 0.2, 0.68),
                    (-2.0, 0.16, 0.06, 0.62)], "optica")
    # toberas
    for s in (-1, 1):
        m.cilindro((s * 0.46, 7.6, 0.0), 0.44, 0.7, "Y", 12 if fino else 8, "tobera", r2=0.38)
        m.cilindro((s * 0.46, 7.96, 0.0), 0.34, 0.03, "Y", 10 if fino else 6, "interior")
    # ---- ala en delta (A5_1): del borde de ataque de la raíz (y −1,3) a la punta (y 5,0) y borde de salida recto
    g = 0.2 if fino else 0.26
    for s in (-1, 1):
        _placa(m, [(s * 0.6, -1.3), (s * 5.35, 5.0), (s * 5.35, 6.0), (s * 0.6, 6.6)][::s], g, "casco", fijo=Z_ALA)
        # góndolas de punta de ala (contramedidas)
        m.cilindro((s * 5.47, 5.3, Z_ALA), 0.12 if fino else 0.14, 2.2, "Y", 6, "casco")
        # pilones (se quedan en el avión al soltar la carga)
        for k, x in enumerate(PILONES):
            y = 1.6 + 1.1 * k
            m.caja((s * x, y, Z_ALA - 0.27), (0.12 if fino else 0.2, 1.4, 0.36), "casco")
    # canards detrás de la cabina
    for s in (-1, 1):
        _placa(m, [(s * 0.55, -4.7), (s * 2.25, -3.55), (s * 2.25, -3.05), (s * 0.55, -3.25)][::s], 0.1 if fino else 0.16,
               "casco", fijo=0.15)
    # deriva
    _placa(m, [(3.4, 0.55), (7.0, 0.55), (7.25, 3.3), (6.3, 3.3)], 0.14 if fino else 0.26, "casco", plano="YZ")
    # cañón BK-27: carenado en la raíz del ala derecha
    m.cilindro((-0.88, -0.6, -0.22), 0.11, 2.2, "Y", 6, "casco")
    # ---- carga (hija «Carga», bajo los pilones del ala y del fuselaje)
    m.parte("Carga")
    zc = Z_ALA - 0.72                                               # eje de los cargamentos colgados
    if a == 0:                                                      # 2 GBU-16 y pod Litening
        for s in (-1, 1):
            _bomba(m, s * PILONES[0], 1.4, zc - 0.05, 3.7, 0.18 if fino else 0.2, "bomba", fino)
            m.caja((s * PILONES[0], -0.4, zc - 0.05), (0.04 if fino else 0.08, 0.5, 0.36), "bomba")    # aletas delanteras
        m.cilindro((0, 0.3, -1.2), 0.2, 2.0, "Y", 8 if fino else 6, "misil")
        m.cilindro((0, -0.83, -1.2), 0.19, 0.3, "Y", 8 if fino else 6, "optica", r2=0.12)
    elif a == 1:                                                    # 6 Mk-82
        for s in (-1, 1):
            for k, x in enumerate(PILONES):
                _bomba(m, s * x, 1.6 + 1.1 * k, zc, 2.2, 0.14 if fino else 0.17, "bomba", fino)
    elif a == 2:                                                    # 2 Taurus KEPD 350 (cuerpo plano y ancho)
        for s in (-1, 1):
            x = s * PILONES[0]
            m.caja((x, 1.4, zc - 0.05), (0.62, 4.2, 0.36), "bomba")
            m.caja((x, -0.95, zc - 0.05), (0.5, 0.5, 0.3), "bomba", cuña=None)
            if fino:
                m.caja((x, 3.2, zc - 0.25), (0.04, 0.5, 0.3), "bomba")
    elif a == 3:                                                    # aire-aire con depósitos
        for s in (-1, 1):
            m.cilindro((s * PILONES[1], 2.7, zc - 0.1), 0.3, 3.4, "Y", 10 if fino else 8, "deposito")
            m.cilindro((s * PILONES[1], 0.67, zc - 0.1), 0.29, 0.7, "Y", 10 if fino else 8, "deposito", r2=0.05)
            m.cilindro((s * PILONES[1], 4.73, zc - 0.1), 0.06, 0.7, "Y", 10 if fino else 8, "deposito", r2=0.29)
            _misil(m, s * PILONES[2], 3.8, zc + 0.15, 2.9, fino)                       # IRIS-T
            _misil(m, s * 0.75, 1.5, -1.05, 3.7, fino)                                 # Meteor semiempotrado
    else:                                                           # solo autodefensa
        for s in (-1, 1):
            _misil(m, s * PILONES[2], 3.8, zc + 0.15, 2.9, fino)
    # ---- punto de salida del cañón (hija «BocaCanon»)
    m.parte("BocaCanon")
    m.cilindro((BOCA[0], BOCA[1] + 0.06, BOCA[2]), 0.04 if fino else 0.06, 0.12, "Y", 6, "boca")
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    # origen en el eje del fuselaje (no en la base de la caja envolvente): así «Carga» queda con identidad
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
