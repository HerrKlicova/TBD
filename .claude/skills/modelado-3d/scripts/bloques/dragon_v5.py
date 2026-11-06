"""VCR 8x8 Dragón v5: bloque detallado a partir de las fotos de Paula (referencias-reales/vehiculos/).

Fuentes de cada medida y detalle (referencias-reales/V1_dragon_dimensiones.md):
- 8 × 3 m y 2,34 m de casco: Wikipedia ES y EN. El lateral del Piranha V (V1_piranhaV_lateral.jpg, CC BY-SA 4.0)
  da la misma altura de casco a escala con los 8 m: 2,36 m (medido en píxeles), así que 2,34 es la del casco.
- Medido en el lateral del Piranha V (mismo casco, SIN VERIFICAR en el Dragón): ruedas de ~1,36 m, ejes a 1,56 /
  2,99 / 4,52 / 6,08 m del morro, altura libre 0,45 m, flanco vertical hasta 1,17 m y debajo casco en V.
- Del 3/4 (V1_dragon_34.jpg, CC0) y el frontal (V1_dragon_frontal.jpg, CC0): torre RCWS de 30 mm angulosa con visores
  a los lados y cañón largo, mástil del visor del jefe a la izquierda delante, escotilla abierta junto a la torre,
  retrovisores grandes en las esquinas delanteras, grupos de faros en las esquinas del frontal, ganchos de remolque,
  redes de carga en el glacis, asas y escotillas en el techo, dos antenas.
- De la trasera (V1_piranhaV_trasera.jpg, CC BY 3.0, casco del Piranha V): rampa con puerta y cajones en las esquinas.
Delante = -Y. Cada pieza lleva su material (casco, goma, llanta, optica, foco, metal, carga) para paleta_por_piezas().
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix

PIEZAS = ["casco", "goma", "llanta", "optica", "foco", "metal", "carga", "costura"]
COLORES = {  # muestreados de las fotos y llevados a la paleta de 32 (sin rojos ni ámbar)
    "casco": "50541c",   # verde OTAN del Dragón -> oliva oscuro (costados D-N57; el techo pasa a b4a080)
    "goma": "303424",    # neumático negro -> el gris más oscuro sin caer al negro del contorno
    "llanta": "70784c",  # llanta verde grisácea más clara que el casco (se ve en el 3/4 y en el lateral)
    "optica": "181c30",  # cristales de visores, periscopios y retrovisores
    "foco": "f0dc9c",    # faros encendidos del frontal (crema claro)
    "metal": "1c200c",   # mástiles, antenas, ganchos, asas y cañón
    "carga": "847c34",   # redes y bolsas de carga del glacis (oliva amarillento)
    "costura": "50541c", # provisional: costuras_por_valor() las oscurece 5-12 de L* sobre su panel
}

# Costuras de la chapa medidas en el lateral del Piranha V (mismo casco; SIN VERIFICAR en el Dragón): juntas verticales
# de los paneles del flanco a 1,7 / 3,1 / 4,7 / 6,1 m del morro, y la junta longitudinal del techo y el glacis (frontal
# del Dragón). Los remaches no se modelan: miden menos de 1 px a cualquier zoom del juego.
COSTURAS = [((0, -2.3, 0), (0, 1, 0)), ((0, -0.9, 0), (0, 1, 0)), ((0, 0.7, 0), (0, 1, 0)), ((0, 2.1, 0), (0, 1, 0)),
            ((0, 0, 0), (1, 0, 0))]

HULL_Z = 2.34


def _ancho(z):
    """Semiancho del casco por altura: V abajo, flanco vertical de 1,17 a 2,2 m y canto del techo recogido."""
    tabla = [(0.45, 0.95), (0.75, 1.25), (1.17, 1.5), (2.2, 1.5), (HULL_Z, 1.38)]
    if z <= tabla[0][0]:
        return tabla[0][1]
    for (z0, a0), (z1, a1) in zip(tabla, tabla[1:]):
        if z <= z1:
            return a0 + (a1 - a0) * (z - z0) / (z1 - z0)
    return tabla[-1][1]


class Taller:
    def __init__(self):
        self.bm = bmesh.new()
        self.biselar = set()   # caras cuyas aristas vivas llevan bisel (solo casco y cuerpo de la torre)

    def _marcar(self, caras, pieza):
        for f in caras:
            f.material_index = PIEZAS.index(pieza)

    def casco(self):
        # perfil lateral (y, z), medido en el lateral del Piranha V a escala de 8 m
        P = [(-3.2, 0.45), (-3.75, 0.75), (-3.95, 1.17), (-4.0, 1.6), (-2.8, HULL_Z), (3.85, HULL_Z), (4.0, 2.2),
             (4.0, 1.17), (3.75, 0.75), (3.2, 0.45)]
        bm = self.bm
        izq = [bm.verts.new((-1.0, y, z)) for y, z in P]
        der = [bm.verts.new((1.0, y, z)) for y, z in P]
        n = len(P)
        caras = [bm.faces.new(izq[::-1]), bm.faces.new(der)]
        for i in range(n):
            j = (i + 1) % n
            caras.append(bm.faces.new((izq[i], izq[j], der[j], der[i])))
        # cortes horizontales en los cambios de anchura y después la anchura por altura: así cada banda del flanco es
        # plana (sin esto el flanco era un n-gono alabeado y la luz lo partía en triángulos de distinto tono)
        for zc in (0.75, 1.17, 2.2):
            geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, zc), plane_no=(0, 0, 1))
        for v in bm.verts:
            v.co.x = math.copysign(_ancho(v.co.z), v.co.x)
        caras = list(bm.faces)
        self._marcar(caras, "casco")
        self.biselar.update(caras)
        self.caras_casco = caras

    def costuras(self, planos, ancho=0.06):
        """Corta el casco con dos planos paralelos por junta y marca la tira de en medio como costura."""
        bm = self.bm
        for co, no in planos:
            co, no = Vector(co), Vector(no)
            for d in (-ancho / 2, ancho / 2):
                caras = [f for f in bm.faces if f.is_valid and f.material_index == PIEZAS.index("casco")
                         and f in self._casco_set()]
                geom = list({v for f in caras for v in f.verts}) + list({e for f in caras for e in f.edges}) + caras
                r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co + no * d, plane_no=no)
                self._casco_nuevo(r["geom"])
            for f in self._casco_set():
                if abs((f.calc_center_median() - co).dot(no)) < ancho / 2:
                    f.material_index = PIEZAS.index("costura")

    def _casco_set(self):
        return {f for f in self._casco if f.is_valid}

    def _casco_nuevo(self, geom):
        self._casco |= {g for g in geom if isinstance(g, bmesh.types.BMFace)}

    def caja(self, c, s, pieza="casco", rot_z=0.0, inclinar=None, bisel=False):
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        R = Matrix.Rotation(rot_z, 3, "Z")
        for v in r["verts"]:
            p = Vector((v.co.x * s[0], v.co.y * s[1], v.co.z * s[2]))
            if inclinar and p.z > 0 and p.y < 0:   # frente superior hacia atrás (planos de la torre)
                p.y += inclinar
            v.co = R @ p + Vector(c)
        caras = list({f for v in r["verts"] for f in v.link_faces})
        self._marcar(caras, pieza)
        if bisel:
            self.biselar.update(caras)

    def cilindro(self, c, r, largo, eje="X", lados=12, pieza="casco", r2=None):
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=lados, radius1=r,
                                    radius2=r if r2 is None else r2, depth=largo)
        rot = {"X": Matrix.Rotation(math.pi / 2, 3, "Y"), "Y": Matrix.Rotation(math.pi / 2, 3, "X"),
               "Z": Matrix.Identity(3)}[eje]
        for v in res["verts"]:
            v.co = rot @ v.co + Vector(c)
        self._marcar(list({f for v in res["verts"] for f in v.link_faces}), pieza)

    def neumatico(self, c, r=0.68, ancho=0.5, tacos=12):
        """Neumático con tacos en la silueta (radio alterno), llanta y buje."""
        bm = self.bm
        n = tacos * 2
        x0, y0, z0 = c
        lado = 1 if x0 > 0 else -1
        anillos = []
        for dx in (-ancho / 2, ancho / 2):
            anillo = []
            for k in range(n):
                a = 2 * math.pi * k / n
                rr = r if k % 2 == 0 else r - 0.05
                anillo.append(bm.verts.new((x0 + dx, y0 + rr * math.cos(a), z0 + rr * math.sin(a))))
            anillos.append(anillo)
        caras = []
        for k in range(n):
            j = (k + 1) % n
            caras.append(bm.faces.new((anillos[0][k], anillos[0][j], anillos[1][j], anillos[1][k])))
        caras.append(bm.faces.new(anillos[0][::-1]))
        caras.append(bm.faces.new(anillos[1]))
        self._marcar(caras, "goma")
        # llanta (disco algo hundido respecto al flanco) y buje saliente
        self.cilindro((x0 + lado * (ancho / 2 + 0.01), y0, z0), 0.38, 0.04, "X", 10, "llanta")
        self.cilindro((x0 + lado * (ancho / 2 + 0.06), y0, z0), 0.17, 0.1, "X", 8, "casco")


class _Nada:
    """Taller que no construye nada: las piezas finas no entran en el LOD1."""
    def __getattr__(self, _):
        return lambda *a, **k: None


def construir(nombre="SM_VCR_Dragon_v5", lod=0):
    """lod=0: de cerca, con todo el detalle. lod=1: tamaño de juego (regla de Lucía del 6-nov): solo piezas de
    0,4 m o más (unos 3 px): torre, escotillas, rampa, cajones, ruedas y faros. Fuera retrovisores, ganchos,
    periscopios, redes, mástil, lanzafumígenos, asas y lamas, y también las placas planas del techo (escotillas de
    tropa y del conductor, rejilla): miden menos de 10 cm de alto y sus cantos salen a 48 px como motas sueltas."""
    t = Taller()
    fino = t if lod == 0 else _Nada()
    t.casco()
    t._casco = set(t.caras_casco)
    # Costuras solo en el LOD0. Probado el 6-nov: en el LOD1 a 48 px, tanto con tiras de 6 cm como de 20 cm, la junta
    # sale como motas sueltas (ruido), así que allí la chapa se queda en el valor partido limpio
    if lod == 0:
        t.costuras(COSTURAS, ancho=0.06)
    # --- ruedas: ejes medidos en el lateral (desde el morro 1,56 / 2,99 / 4,52 / 6,08 m)
    for y in (-2.44, -1.01, 0.52, 2.08):
        for x in (-1.22, 1.22):
            t.neumatico((x, y, 0.68))
    # --- torre RCWS 30 mm (3/4 y frontal): base, cuerpo con frente inclinado, mantelete, cañón con bocacha
    tz = HULL_Z
    t.caja((0.25, 0.1, tz + 0.12), (1.8, 1.9, 0.24), bisel=True)
    t.caja((0.25, 0.15, tz + 0.52), (1.5, 1.6, 0.56), inclinar=0.3, bisel=True)
    t.caja((0.25, -0.75, tz + 0.5), (0.5, 0.4, 0.38))
    t.cilindro((0.25, -2.1, tz + 0.52), 0.065, 2.5, "Y", 8, "metal")
    t.cilindro((0.25, -3.3, tz + 0.52), 0.1, 0.22, "Y", 8, "metal")
    # visores a los dos lados de la torre con su cristal (frontal: cajas con óptica en las esquinas)
    for x in (-0.62, 1.12):
        t.caja((x, -0.35, tz + 0.62), (0.32, 0.42, 0.34))
        t.caja((x, -0.57, tz + 0.64), (0.22, 0.03, 0.18), "optica")
    # visor panorámico del jefe encima de la torre (cilindro con cabeza)
    fino.cilindro((0.85, 0.45, tz + 0.95), 0.12, 0.3, "Z", 8)
    fino.caja((0.85, 0.45, tz + 1.16), (0.28, 0.28, 0.16))
    fino.caja((0.85, 0.3, tz + 1.16), (0.2, 0.02, 0.1), "optica")
    # lanzafumígenos: dos racimos a los lados de la torre
    for x in (-0.55, 1.05):
        for k in range(3):
            fino.cilindro((x + (k - 1) * 0.11, 0.55, tz + 0.9), 0.045, 0.2, "Z", 6, "metal")
    # --- techo: escotilla abierta junto a la torre (3/4: marco grande vertical), escotilla del conductor,
    # periscopios, dos escotillas de tropa y asas
    t.caja((-0.85, -0.85, tz + 0.04), (0.8, 0.8, 0.08))
    t.caja((-0.85, -1.22, tz + 0.45), (0.8, 0.06, 0.8))
    fino.cilindro((-0.75, -2.35, tz + 0.05), 0.32, 0.1, "Z", 10)
    for k in range(3):
        fino.caja((-0.75 + (k - 1) * 0.2, -2.78, tz + 0.08), (0.14, 0.1, 0.14))
        fino.caja((-0.75 + (k - 1) * 0.2, -2.835, tz + 0.09), (0.1, 0.01, 0.06), "optica")
    for x in (-0.6, 0.6):
        fino.caja((x, 2.6, tz + 0.04), (0.9, 1.2, 0.08))
        fino.caja((x, 3.25, tz + 0.12), (0.5, 0.05, 0.08), "metal")
    for y in (1.1, 1.6):
        fino.caja((1.0, y, tz + 0.08), (0.45, 0.04, 0.06), "metal")
    # rejilla del motor (delante a la derecha): placa con lamas
    fino.caja((0.85, -1.9, tz + 0.03), (1.0, 1.0, 0.06))
    for k in range(3):
        fino.caja((0.85, -2.25 + k * 0.3, tz + 0.08), (0.9, 0.12, 0.04), "metal")
    # mástil del visor del jefe (3/4: poste delante a la izquierda con caja negra arriba)
    fino.cilindro((-1.15, -1.7, tz + 0.6), 0.07, 1.2, "Z", 6, "metal")
    fino.caja((-1.15, -1.7, tz + 1.35), (0.26, 0.26, 0.36), "metal")
    fino.caja((-1.15, -1.84, tz + 1.38), (0.18, 0.02, 0.14), "optica")
    # antenas en las esquinas traseras: solo la base. La varilla (1,8 m y 5 cm) sale a tamaño de juego como trazos
    # sueltos que no se unen; se quitó a propósito (lectura antes que fidelidad)
    for x in (-1.2, 1.2):
        fino.cilindro((x, 3.6, tz + 0.12), 0.09, 0.24, "Z", 6, "metal")
    # --- frontal: retrovisores en mástil, grupos de faros, ganchos y redes de carga
    for s in (-1, 1):
        fino.cilindro((s * 1.45, -3.35, 2.45), 0.06, 0.9, "Z", 6, "metal")
        fino.caja((s * 1.62, -3.35, 2.75), (0.12, 0.2, 0.42), "metal")
        fino.caja((s * 1.62, -3.46, 2.75), (0.08, 0.02, 0.34), "optica")
        t.caja((s * 1.1, -3.98, 1.68), (0.5, 0.08, 0.16))
        for k in range(2):
            t.caja((s * (0.98 + k * 0.22), -4.03, 1.68), (0.1, 0.02, 0.1), "foco")
        fino.cilindro((s * 0.75, -3.9, 0.95), 0.07, 0.18, "Y", 6, "metal")
        fino.caja((s * 0.75, -3.95, 0.82), (0.16, 0.08, 0.1), "metal")
    fino.caja((0.0, -3.62, 1.15), (1.4, 0.18, 0.45), "carga", inclinar=None)
    fino.caja((-0.95, -3.25, 2.0), (0.7, 0.3, 0.22), "carga")
    # --- costados: cajones de herramientas sobre el flanco, entre ruedas 3 y 4
    for s in (-1, 1):
        t.caja((s * 1.58, 1.3, 1.65), (0.16, 1.6, 0.5))      # salen 16 cm: la tapa de arriba toma el claro y se lee
        t.caja((s * 1.58, -1.75, 1.75), (0.16, 0.9, 0.35))
    # --- trasera: rampa con puerta, cajones en las esquinas y pilotos
    t.caja((0.0, 4.03, 1.3), (1.5, 0.06, 1.25))
    t.caja((0.35, 4.07, 1.4), (0.6, 0.03, 0.9))
    for s in (-1, 1):
        t.caja((s * 1.2, 4.12, 1.55), (0.55, 0.25, 0.9))
        t.caja((s * 1.2, 4.255, 1.15), (0.2, 0.02, 0.08), "optica")
    # bisel de 1 segmento solo en las aristas vivas del casco y de la torre (ayuda al contorno por normales)
    capa = t.bm.edges.layers.float.get("bevel_weight_edge") or t.bm.edges.layers.float.new("bevel_weight_edge")
    for f in t.biselar:
        for e in f.edges:
            if len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(30):
                e[capa] = 1.0
    me = bpy.data.meshes.new(nombre)
    t.bm.to_mesh(me)
    t.bm.free()
    obj = bpy.data.objects.new(nombre, me)
    for p in PIEZAS:
        obj.data.materials.append(bpy.data.materials.get(p) or bpy.data.materials.new(p))
    bpy.context.scene.collection.objects.link(obj)
    m = obj.modifiers.new("bisel", "BEVEL")
    m.width = 0.03
    m.segments = 1
    m.limit_method = "WEIGHT"
    return obj
