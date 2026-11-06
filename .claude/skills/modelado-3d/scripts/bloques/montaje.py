"""Montaje: taller con PIEZAS ANIMABLES (Teo, 6-nov-2026). Cada cara nueva se marca con la parte activa en el atributo
de cara «parte»; separar() parte el objeto ya pintado en un objeto por parte, con su origen en el pivote y colgado de
su padre, listo para exportar_unity(extra=...). La parte 0 es el cuerpo (la raíz)."""
import bmesh
import bpy
from mathutils import Vector, Matrix
from taller import Taller


class Montaje:
    def __init__(self, piezas, partes):
        self.t = Taller(piezas)
        self.bm = self.t.bm
        self.partes = list(partes)
        self.capa = self.bm.faces.layers.int.new("parte")
        self.p = 0
        self.n = 0

    def parte(self, nombre):
        self._sellar()
        self.p = self.partes.index(nombre)

    def _sellar(self):
        """Las caras sin marcar (0) toman la parte activa + 1; así no importa el orden de las caras en el bmesh
        (Taller.cilindro borra y rehace caras y desplaza los índices). Al final objeto() resta 1."""
        for f in self.bm.faces:
            if f[self.capa] == 0:
                f[self.capa] = self.p + 1

    def __getattr__(self, k):            # caja, cilindro, tejado… del Taller
        return getattr(self.t, k)

    def barra(self, p0, p1, ancho, alto, pieza, giro=0.0):
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        vs = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        M = Matrix.Translation((p0 + p1) / 2) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4() @ \
            Matrix.Rotation(giro, 4, "Z") @ Matrix.Diagonal((ancho, alto, d.length, 1.0))
        bmesh.ops.transform(self.bm, matrix=M, verts=vs)
        self.t._marcar(list({f for v in vs for f in v.link_faces}), pieza)

    def loft(self, perfil, secciones, pieza):
        """Cuerpo por secciones a lo largo de Y: perfil = [(x, z)] (polígono convexo, en sentido antihorario visto
        desde −Y); secciones = [(y, sx, sz, zc)]. Tapas en los dos extremos."""
        anillos = [[self.bm.verts.new((x * sx, y, zc + z * sz)) for x, z in perfil] for y, sx, sz, zc in secciones]
        n = len(perfil)
        caras = []
        for a, b in zip(anillos, anillos[1:]):
            for i in range(n):
                caras.append(self.bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i])))
        caras.append(self.bm.faces.new(anillos[0]))
        caras.append(self.bm.faces.new(anillos[-1][::-1]))
        bmesh.ops.recalc_face_normals(self.bm, faces=caras)
        self.t._marcar(caras, pieza)

    def objeto(self, nombre):
        self._sellar()
        for f in self.bm.faces:
            f[self.capa] -= 1
        return self.t.objeto(nombre, bisel=0)


def separar(o, partes, pivotes, padres=None):
    """partes: nombres (0 = cuerpo); pivotes: {parte: (x, y, z)}; padres: {parte: padre} (por defecto el cuerpo)."""
    padres = padres or {}
    at = o.data.attributes["parte"]
    cara_parte = [at.data[i].value for i in range(len(o.data.polygons))]
    hijos = {}
    for k in range(1, len(partes)):
        sel = {i for i, v in enumerate(cara_parte) if v == k}
        if not sel:
            continue
        nom = partes[k]
        n = o.copy()
        n.data = o.data.copy()
        n.name = n.data.name = nom
        bpy.context.scene.collection.objects.link(n)
        b = bmesh.new()
        b.from_mesh(n.data)
        b.faces.ensure_lookup_table()
        bmesh.ops.delete(b, geom=[f for f in b.faces if f.index not in sel], context="FACES")
        p = Vector(pivotes[nom])
        bmesh.ops.translate(b, verts=b.verts, vec=-p)
        b.to_mesh(n.data)
        b.free()
        hijos[nom] = n
    b = bmesh.new()
    b.from_mesh(o.data)
    b.faces.ensure_lookup_table()
    bmesh.ops.delete(b, geom=[f for f in b.faces if cara_parte[f.index] != 0], context="FACES")
    b.to_mesh(o.data)
    b.free()
    for nom, n in hijos.items():
        pad = padres.get(nom)
        if pad in hijos:
            n.parent = hijos[pad]
            n.location = Vector(pivotes[nom]) - Vector(pivotes[pad])
        else:
            n.parent = o
            n.location = Vector(pivotes[nom])
    return [o] + list(hijos.values())
