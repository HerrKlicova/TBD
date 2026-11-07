"""Restos destruidos de vehículos (coordinador, 7-nov 01:54). Teo, 7-nov.

Toma la malla del bloque aprobado (VAMTAC v2, Dragón v5, NH90) y la convierte en chatarra: quita piezas (gomas, rotor,
torre), las cae o las tuerce, hunde y abolla la chapa con un ruido determinista (mismo resultado en cada exportación),
pinta parches quemados y de óxido alrededor del foco del incendio y añade escombros. Todo SIN VERIFICAR: sin fotos de
restos (de memoria: vehículos calcinados por IED y helicópteros derribados).
Colores (D-N80: nada por debajo de 48403c; la sombra fuera de la banda de la horda con las normales subidas):
quemado 94948c (la chapa calcinada se queda gris ceniza), hollín 605c48 (centro del foco y mancha del suelo), óxido
845c28 (solo en caras pequeñas: en las grandes parecía una losa naranja), ceniza b8b8b8 (el rombo del Humo).
Jerarquía: SM_…_LODn (los restos) y un hijo «Humo» (un rombo pequeño de ceniza; su ORIGEN es el foco del incendio,
para colgar el VFX). Origen de los restos en el suelo, en el punto donde estaba el origen del vehículo.
"""
import math
import bmesh
from mathutils import Vector, Matrix

NUEVAS = ["quemado", "hollin", "oxido", "ceniza"]
COLORES_RESTOS = {"quemado": "94948c", "hollin": "605c48", "oxido": "845c28", "ceniza": "b8b8b8"}


def _h(v, k=0):
    """Ruido determinista en [0, 1) a partir de una posición (vértices coincidentes se mueven igual)."""
    x, y, z = round(v[0], 3), round(v[1], 3), round(v[2], 3)
    s = math.sin(x * 12.9898 + y * 78.233 + z * 37.719 + k * 19.19) * 43758.5453
    return s - math.floor(s)


class Restos:
    def __init__(self, o):
        self.o = o
        for p in NUEVAS:
            import bpy
            o.data.materials.append(bpy.data.materials.get(p) or bpy.data.materials.new(p))
        self.mats = [m.name.split(".")[0] for m in o.data.materials]
        self.bm = bmesh.new()
        self.bm.from_mesh(o.data)
        self.bm.faces.ensure_lookup_table()
        self.capa = self.bm.faces.layers.int.get("parte") or self.bm.faces.layers.int.new("parte")
        self.parte0 = {}                                 # parte original de cada cara (rotores del NH90)
        for f in self.bm.faces:
            self.parte0[f] = f[self.capa]
            f[self.capa] = 0

    def mat(self, nombre):
        return self.mats.index(nombre)

    def caras(self, pred):
        """pred(cara, pieza, centro) -> bool"""
        return [f for f in self.bm.faces if f.is_valid and pred(f, self.mats[f.material_index], f.calc_center_median())]

    def islas(self, caras):
        """Amplía la selección a las islas enteras (para no partir una pieza cerrada al moverla)."""
        sel = set(caras)
        pila = list(sel)
        while pila:
            f = pila.pop()
            for v in f.verts:
                for g in v.link_faces:
                    if g not in sel:
                        sel.add(g)
                        pila.append(g)
        return list(sel)

    def borrar(self, caras):
        bmesh.ops.delete(self.bm, geom=list(caras), context="FACES")

    def mover(self, caras, M):
        vs = {v for f in caras for v in f.verts}
        for v in vs:
            v.co = M @ v.co

    def todo(self):
        return list(self.bm.faces)

    def abollar(self, amp, caras=None, k=0):
        vs = {v for f in (caras or self.bm.faces) for v in f.verts}
        for v in vs:
            c = tuple(v.co)
            v.co += Vector(((_h(c, k) - 0.5) * amp, (_h(c, k + 1) - 0.5) * amp, (_h(c, k + 2) - 0.5) * amp))

    def hundir(self, z0, factor, caras=None, centro=None, radio=None):
        """Aplasta hacia z0 lo que queda por encima (techo hundido); con centro y radio, solo cerca de ese punto."""
        vs = {v for f in (caras or self.bm.faces) for v in f.verts}
        for v in vs:
            if v.co.z <= z0:
                continue
            w = 1.0
            if centro is not None:
                d = (Vector((v.co.x, v.co.y)) - Vector(centro[:2])).length
                w = max(0.0, 1.0 - d / radio)
            v.co.z = z0 + (v.co.z - z0) * (1.0 - (1.0 - factor) * w)

    def quemar(self, fuego, radio, piezas_no=("optica",)):
        """Parches: cerca del foco, hollín y quemado; más lejos, óxido y quemado a manchas; los cristales, hollín."""
        fx = Vector(fuego)
        for f in self.bm.faces:
            p = self.mats[f.material_index]
            if p in NUEVAS:
                continue
            c = f.calc_center_median()
            d = (c - fx).length / radio
            r = _h(c, 7)
            if p in piezas_no:
                f.material_index = self.mat("hollin")
            elif d < 0.35 and r < 0.5:
                f.material_index = self.mat("hollin")
            elif d < 0.8:
                f.material_index = self.mat("quemado") if r < 0.85 or f.calc_area() > 0.4 else self.mat("oxido")
            elif d < 1.3:
                if r < 0.45:
                    f.material_index = self.mat("quemado")
                elif r < 0.53 and f.calc_area() < 0.4:
                    f.material_index = self.mat("oxido")

    def caja(self, c, s, pieza, rz=0.0, rx=0.0, parte=0):
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        M = Matrix.Translation(c) @ Matrix.Rotation(rz, 4, "Z") @ Matrix.Rotation(rx, 4, "X") @ \
            Matrix.Diagonal((s[0], s[1], s[2], 1.0))
        bmesh.ops.transform(self.bm, matrix=M, verts=r["verts"])
        caras = list({f for v in r["verts"] for f in v.link_faces})
        for f in caras:
            f.material_index = self.mat(pieza)
            f[self.capa] = parte
        return caras

    def escombros(self, centro, radio, n, k=0, pieza="quemado"):
        cx, cy = centro
        for i in range(n):
            a = _h((i, k, 1)) * 2 * math.pi
            d = radio * (0.4 + 0.6 * _h((i, k, 2)))
            sx = 0.3 + 0.5 * _h((i, k, 3))
            self.caja((cx + d * math.cos(a), cy + d * math.sin(a), 0.06), (sx, sx * 0.7, 0.12),
                      pieza if _h((i, k, 4)) < 0.75 else "oxido", rz=a)

    def mancha_suelo(self, centro, sx, sy, k=0):
        """Mancha quemada en el suelo bajo los restos: tres placas bajas giradas y escalonadas en altura (sin caras
        coplanarias), en hollín 605c48 (la ceniza clara parecía una losa)."""
        for i in range(3):
            g = (_h((i, k, 5)) - 0.5) * 0.9
            f = 0.55 + 0.25 * _h((i, k, 6))
            dx, dy = (_h((i, k, 8)) - 0.5) * sx * 0.3, (_h((i, k, 9)) - 0.5) * sy * 0.3
            self.caja((centro[0] + dx, centro[1] + dy, 0.01 + 0.012 * i), (sx * f, sy * f, 0.02), "hollin", rz=g)

    def humo(self, fuego, lado=0.3):
        """Rombo del hijo «Humo» centrado en el foco (su origen será el foco)."""
        fx = Vector(fuego)
        r = bmesh.ops.create_icosphere(self.bm, subdivisions=1, radius=lado)
        for v in r["verts"]:
            v.co = v.co + fx
        for f in {f for v in r["verts"] for f in v.link_faces}:
            f.material_index = self.mat("ceniza")
            f[self.capa] = 1

    def apoyar(self):
        """Lo deja apoyado en el suelo (z mínima = 0) después de volcarlo o de quitarle las gomas."""
        z = min(v.co.z for v in self.bm.verts)
        for v in self.bm.verts:
            v.co.z -= z

    def cerrar(self):
        hs = [v.co for f in self.bm.faces if f[self.capa] == 1 for v in f.verts]
        if hs:
            c = sum(hs, Vector()) / len(hs)
            self.o["tbd_fuego"] = (c.x, c.y, c.z)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(self.o.data)
        self.bm.free()
        self.o.data.update()
        return self.o


def separar_restos(o, lod=0):
    from montaje import separar as _separar
    fuego = tuple(o.get("tbd_fuego", (0.0, 0.0, 1.0)))
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, ["restos", "Humo"], {"Humo": tuple(fuego)})
    o.data.transform(Matrix.Translation(base))      # origen donde estaba el del vehículo (no en la caja envolvente)
    for n in objs[1:]:
        n.location = n.location + base
    return objs


def Rx(g, p=(0, 0, 0)):
    p = Vector(p)
    return Matrix.Translation(p) @ Matrix.Rotation(math.radians(g), 4, "X") @ Matrix.Translation(-p)


def Ry(g, p=(0, 0, 0)):
    p = Vector(p)
    return Matrix.Translation(p) @ Matrix.Rotation(math.radians(g), 4, "Y") @ Matrix.Translation(-p)


def Rz(g, p=(0, 0, 0)):
    p = Vector(p)
    return Matrix.Translation(p) @ Matrix.Rotation(math.radians(g), 4, "Z") @ Matrix.Translation(-p)


def T(x, y, z):
    return Matrix.Translation((x, y, z))
