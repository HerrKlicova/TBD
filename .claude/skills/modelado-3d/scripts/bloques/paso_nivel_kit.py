"""Paso a nivel con carretera como pieza del kit de vía (Hito 1; Lucía, 11-11: la vía A–B cruza la carretera vieja a nivel en el
terreno de Álvaro, docs/hito1/terreno-hito1.md). Teo (4), 11-11. Sale de paso_nivel.py (D-N96): las mismas barreras animables,
señales, armarios y caseta, ahora con su tramo de vía y su tramo de carretera.

Conectores, como el resto del kit (via_trazado.py): VÍA, A en el origen (eje, suelo) y B en (L; 0), saliendo hacia +X.
CARRETERA, C y D a los dos lados del cruce, que está en (L/2; 0). La carretera forma el ÁNGULO de la pieza con la vía:
90°, 60°, 45° o 30°. La recta A–B del terreno corta la carretera a unos 11°, que es demasiado sesgado para un paso a nivel; el
jugador tiene que doblar la vía o la carretera hasta 30° como poco (lo decide Lucía). Calzada de 7 m (la de Álvaro) a 0,71 m
sobre el carril en el cruce (1 cm por debajo de la cabeza), bajando a 0,08 m en 6 m de rampa a cada lado.
Las barreras se apartan del cruce cuanto más sesgado es: su eje queda a 4 + 3,9 / tan(ángulo) m del centro, medido por la
carretera.
Jerarquía (mira a +Z en Unity), la de paso_nivel.py:
  SM_…_LODn                  vía, carretera, armarios, postes, caseta y señales
  ├─ Barrera_1, Barrera_2    brazos BAJADOS (cerrado), con el origen en su eje; suben girando unos 85° sobre su X local, con el
  │                          mismo signo los dos (su rotación local ya lleva el ángulo de la pieza)
  └─ Luces_A, Luces_B        focos para la emisión (transformación identidad)
Colores: los de la vía y los de paso_nivel.py, salvo la placa de la cruz, que pasa de d4d0cc (reservado a la selección) a b8b8b8;
calzada 6c7874 con la raya b8b8b8 y el tablero del cruce 605c48. MEDIDAS SIN VERIFICAR.
TBD_VARIANTE = 5 · ángulo + aspecto. Ángulos: 0 → 90°, 1 → 60°, 2 → 45°, 3 → 30°. Aspectos (los de paso_nivel.py): 0 caseta de
teja; 1 focos dobles y cubierta plana; 2 abandonado (una barrera partida, una señal inclinada y maleza en la vía); 3 puesto
militar (sacos y bloques); 4 catenaria y farola, caseta metálica.
"""
import math, os
import bmesh
from mathutils import Matrix, Vector
import via_trazado as vt
import paso_nivel as pn
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 20
IA, A = VAR // 5, VAR % 5
ANGULOS = (90, 60, 45, 30)
ANG = math.radians(ANGULOS[IA])
NOMBRES_V = ["Teja", "FocosDobles", "Abandonado", "Militar", "Catenaria"]
vt.VARIANTE = 2 if A == 2 else 0
vt.PIEZA = 0
PIEZAS = vt.PIEZAS + pn.PIEZAS + ["asfalto", "raya", "tablero"]
COLORES = {**vt.COLORES, **pn.COLORES, "placa": "b8b8b8", "asfalto": "6c7874", "raya": "b8b8b8", "tablero": "605c48"}
if A == 1:
    COLORES["pared"] = "605c48"
if A == 4:
    COLORES.update(pared="94948c", teja="6c7874")
FACCION = "neutro"
NORMALES_ARRIBA = ["balasto", "maleza", "maleza_c"]
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (100, 6000), 1: (50, 2600)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
L = 20.0 if IA < 2 else 30.0
CX = L / 2
SEP = 4.0 + 3.9 / math.tan(ANG) if IA else 4.0           # eje de cada barrera, medido por la carretera
MEDIA = SEP + 4.0                                          # media carretera (de C o D al cruce)
PARTES = pn.PARTES
# del marco de la carretera (carretera a lo largo de X', vía a ANG de X', cruce en el origen) al de la pieza (vía en +X)
M = Matrix.Translation((CX, 0, 0)) @ Matrix.Rotation(-ANG, 4, "Z")
PIV1 = tuple(M @ Vector((SEP, 3.85, 1.0)))
PIV2 = tuple(M @ Vector((-SEP, -3.85, 1.0)))
PIV = {"Barrera_1": PIV1, "Barrera_2": PIV2, "Luces_A": (0, 0, 0), "Luces_B": (0, 0, 0)}
DIR_C = (math.cos(ANG), -math.sin(ANG))                  # sentido de la carretera en la pieza
CONECTORES = {"A": (0.0, 0.0, 0.0), "B": (L, 0.0, 0.0),
              "C": (round(CX - MEDIA * DIR_C[0], 3), round(-MEDIA * DIR_C[1], 3), round(-ANGULOS[IA], 1)),
              "D": (round(CX + MEDIA * DIR_C[0], 3), round(MEDIA * DIR_C[1], 3), round(-ANGULOS[IA], 1))}


def _mover(m, antes):
    bmesh.ops.transform(m.bm, matrix=M, verts=[v for v in m.bm.verts if v not in antes])


def _carretera(m, fino):
    """Calzada con rampas, por el sentido DIR_C, de C a D. La sección es una caja de 7 m de ancho del suelo a z(s)."""
    occ = 1.8 / math.sin(ANG) + 0.6                       # media banqueta, medida por la carretera, con margen
    def z(s):
        d = abs(s)
        return 0.71 if d <= occ else max(0.08, 0.71 - (d - occ) * (0.63 / 6.0))
    n = 24 if fino else 12
    ss = [-MEDIA + 2 * MEDIA * k / n for k in range(n + 1)] + [-occ, occ]
    ss = sorted(set(round(s, 4) for s in ss))
    nx, ny = -DIR_C[1], DIR_C[0]
    anillos = []
    for s in ss:
        x, y = CX + DIR_C[0] * s, DIR_C[1] * s
        h = z(s)
        anillos.append([m.bm.verts.new((x + nx * l, y + ny * l, zz)) for l, zz in ((-3.5, 0.0), (3.5, 0.0), (3.5, h), (-3.5, h))])
    caras = []
    for a, b in zip(anillos, anillos[1:]):
        for i in range(4):
            caras.append(m.bm.faces.new((a[i], b[i], b[(i + 1) % 4], a[(i + 1) % 4])))
    caras.append(m.bm.faces.new(anillos[0][::-1]))
    caras.append(m.bm.faces.new(anillos[-1]))
    bmesh.ops.recalc_face_normals(m.bm, faces=caras)
    m.t._marcar(caras, "asfalto")
    # tablero del cruce entre los carriles y raya central (no cruza la vía)
    m.caja((CX, 0, 0.705), (7.0 / math.sin(ANG) - 0.4, vt.ANCHO - 0.12, 0.02), "tablero")      # entre los carriles, a lo largo de la vía
    if fino:
        for k in range(int(MEDIA // 3)):
            for sg in (-1, 1):
                s = sg * (occ + 1.5 + 3.0 * k)
                if abs(s) > MEDIA - 1.0:
                    continue
                x, y = CX + DIR_C[0] * s, DIR_C[1] * s
                m.caja((x, y, z(s) + 0.01), (1.5, 0.15, 0.02), "raya", rot_z=-ANG)


def construir(nombre="SM_PasoNivelKit", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    # ---- lo fijo del paso, en el marco de la carretera (como en paso_nivel.py, con las barreras en ±SEP)
    antes = set(m.bm.verts)
    for (x, y, z), s in (((SEP, 3.85, 1.0), 1), ((-SEP, -3.85, 1.0), -1)):
        m.caja((x, y + s * 0.25, 0.55), (0.45, 0.5, 1.1), "armario")
        m.caja((x, y + s * 0.25, 1.12), (0.5, 0.55, 0.06), "armario")
        m.barra((x, y + s * 0.35, 1.05), (x, y, z), 0.14, 0.14, "armario")
    focos = []
    focos += pn._senal(m, (SEP + 0.7, 4.4, 0), 1, A == 1, fino)
    focos += pn._senal(m, (-SEP - 0.7, -4.4, 0), -1, A == 1, fino, inclinada=math.radians(12) if A == 2 else 0.0)
    cx, cy = SEP + 2.6, -5.8
    if A == 4:
        m.caja((cx, cy, 1.15), (2.0, 1.6, 2.3), "pared")
        m.caja((cx, cy, 2.36), (2.3, 1.9, 0.12), "teja")
    else:
        m.caja((cx, cy, 1.15), (2.4, 2.0, 2.3), "pared")
        if A == 1:
            m.caja((cx, cy, 2.36), (2.6, 2.2, 0.14), "hormigon")
        else:
            m.tejado((cx, cy, 2.3), 2.4, 2.0, 0.7, "teja", vuelo=0.2, aguas=2)
    if A == 2:
        m.caja((cx - 0.6, cy + 1.02, 1.0), (0.85, 0.04, 2.0), "ventana")
        m.caja((cx - 1.15, cy + 1.45, 1.0), (0.04, 0.85, 2.0), "puerta")
        for k in range(4):                                                      # trozo de brazo caído
            m.barra((-SEP + 0.3 + 0.15 * k, -1.2 + 0.45 * k, 0.06), (-SEP + 0.44 + 0.15 * k, -0.78 + 0.45 * k, 0.06),
                    0.24 if fino else 0.32, 0.2 if fino else 0.3, "raya_clara" if k % 2 else "raya_oscura")
    else:
        m.caja((cx - 0.6, cy + 1.02, 1.0), (0.85, 0.05, 2.0), "puerta")
    m.caja((cx + 0.5, cy + 1.02, 1.45), (0.7, 0.05, 0.5 if A == 3 else 0.6), "sacos" if A == 3 else "ventana")
    if A == 3:
        for fila in range(3):
            for k in range(5 - fila):
                m.caja((cx - 1.9 + 0.8 * k + 0.4 * fila, cy + 2.0, 0.17 + 0.3 * fila), (0.75, 0.45, 0.27), "sacos")
        for x in (SEP + 2.0, SEP + 3.6):
            for y in (3.9, -3.9):
                m.caja((x, y, 0.4), (1.5, 0.5, 0.8), "hormigon")
    if A == 4:
        m.cilindro((-SEP - 2.5, 5.0, 2.5), 0.07, 5.0, "Z", 6, "poste")
        m.caja((-SEP - 2.5, 4.6, 5.0), (0.2, 0.8, 0.12), "poste")
    _mover(m, antes)
    # ---- vía y carretera, ya en el marco de la pieza
    via = vt._recta(L)
    vt._via(m, via, fino)
    vt._maleza(m, via, fino)
    if A == 4:                                                                  # poste de catenaria junto a la vía
        m.caja((2.0, -2.9, 3.5), (0.3, 0.3, 7.0), "poste")
        m.caja((2.0, -1.7, 6.6), (0.15, 2.4, 0.15), "poste")
    _carretera(m, fino)
    # ---- barreras y focos (piezas aparte), en el marco de la carretera y movidas
    m.parte("Barrera_1")
    antes = set(m.bm.verts)
    pn._brazo(m, (SEP, 3.85, 1.0), 1, pn.LARGO, fino)
    _mover(m, antes)
    m.parte("Barrera_2")
    antes = set(m.bm.verts)
    pn._brazo(m, (-SEP, -3.85, 1.0), -1, 1.5 if A == 2 else pn.LARGO, fino)
    _mover(m, antes)
    for lado in ("A", "B"):
        m.parte("Luces_" + lado)
        antes = set(m.bm.verts)
        for l, (x, y, z) in focos:
            if l == lado:
                m.cilindro((x, y, z), 0.13 if fino else 0.17, 0.06, "X", 10, "foco")
        _mover(m, antes)
    return m.objeto(nombre)


def separar(o, lod):
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    o.data.transform(Matrix.Translation(base))                                  # origen en el conector A
    for n in objs[1:]:
        n.location = n.location + base
    for n in objs[1:]:                                                          # la rotación del ángulo va en el objeto
        if n.name in ("Barrera_1", "Barrera_2"):
            extra = math.pi if n.name == "Barrera_2" else 0.0
            n.data.transform(Matrix.Rotation(ANG + extra, 4, "Z"))
            n.rotation_euler.z = -ANG + extra
    return objs
