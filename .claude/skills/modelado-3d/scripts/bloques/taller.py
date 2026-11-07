"""Taller genérico para bloques detallados (método del Dragón v5, generalizado).

Cada pieza lleva el material de su nombre (lista `piezas`) para paleta_por_piezas(). Delante = -Y, Z arriba, metros.
Uso:
    t = Taller(["casco", "goma", ...])
    fino = t.fino(lod)            # en el LOD1 las piezas finas no se construyen
    t.perfil_extruido(P, semiancho, cortes_z=(...))   # casco por perfil lateral, flancos en bandas planas
    t.caja(...); t.cilindro(...); fino.caja(...)
    t.costuras(planos)            # solo LOD0
    obj = t.objeto(nombre)
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix


class _Nada:
    """Taller que no construye nada: las piezas finas no entran en el LOD1."""
    def __getattr__(self, _):
        return lambda *a, **k: None


class Taller:
    def __init__(self, piezas):
        self.piezas = list(piezas)
        if "costura" not in self.piezas:
            self.piezas.append("costura")
        self.bm = bmesh.new()
        self.biselar = set()
        self._casco = set()

    def fino(self, lod):
        return self if lod == 0 else _Nada()

    def _marcar(self, caras, pieza):
        for f in caras:
            f.material_index = self.piezas.index(pieza)

    def perfil_extruido(self, P, semiancho, cortes_z=(), pieza="casco", desde_x=0.0):
        """Casco por perfil lateral (y, z) extruido en X con semiancho(z). Los cortes horizontales en cortes_z (los
        cambios de pendiente de semiancho) hacen que cada banda del flanco sea plana."""
        bm = self.bm
        antes = set(bm.faces)
        izq = [bm.verts.new((-1.0, y, z)) for y, z in P]
        der = [bm.verts.new((1.0, y, z)) for y, z in P]
        n = len(P)
        bm.faces.new(izq[::-1])
        bm.faces.new(der)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((izq[i], izq[j], der[j], der[i]))
        nuevas = set(bm.faces) - antes
        for zc in cortes_z:
            caras = [f for f in bm.faces if f.is_valid and f not in antes]
            geom = list({v for f in caras for v in f.verts}) + list({e for f in caras for e in f.edges}) + caras
            r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, zc), plane_no=(0, 0, 1))
            nuevas |= {g for g in r["geom"] if isinstance(g, bmesh.types.BMFace)}
        nuevas = {f for f in set(bm.faces) - antes}
        for v in {v for f in nuevas for v in f.verts}:
            v.co.x = math.copysign(semiancho(v.co.z), v.co.x) + desde_x
        self._marcar(nuevas, pieza)
        self.biselar |= nuevas
        self._casco |= nuevas
        return nuevas

    def caja(self, c, s, pieza="casco", rot_z=0.0, inclinar=None, bisel=False, cuña=None, rot_x=0.0, rot_y=0.0):
        """Caja centrada en c de tamaño s. inclinar: la arista superior delantera retrocede esa distancia.
        cuña: la cara delantera (-Y) se estrecha en X hasta ese factor (proas, arados)."""
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        R = Matrix.Rotation(rot_z, 3, "Z") @ Matrix.Rotation(rot_x, 3, "X") @ Matrix.Rotation(rot_y, 3, "Y")
        for v in r["verts"]:
            p = Vector((v.co.x * s[0], v.co.y * s[1], v.co.z * s[2]))
            if inclinar and p.z > 0 and p.y < 0:
                p.y += inclinar
            if cuña is not None and p.y < 0:
                p.x *= cuña
            v.co = R @ p + Vector(c)
        caras = list({f for v in r["verts"] for f in v.link_faces})
        self._marcar(caras, pieza)
        if bisel:
            self.biselar.update(caras)
        return caras

    def cilindro(self, c, r, largo, eje="X", lados=12, pieza="casco", r2=None):
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=lados, radius1=r,
                                    radius2=r if r2 is None else r2, depth=largo)
        rot = {"X": Matrix.Rotation(math.pi / 2, 3, "Y"), "Y": Matrix.Rotation(math.pi / 2, 3, "X"),
               "Z": Matrix.Identity(3)}[eje]
        for v in res["verts"]:
            v.co = rot @ v.co + Vector(c)
        caras = list({f for v in res["verts"] for f in v.link_faces})
        self._marcar(caras, pieza)
        return caras

    def esfera(self, c, r, pieza="casco", sub=2):
        """Icosfera centrada en c; r es un radio o (rx, ry, rz). sub=1 son 20 caras, sub=2 son 80. Las esferas que se
        cruzan no comparten aristas, así que limpiar no las funde (copas de árbol por grumos)."""
        rx, ry, rz = (r, r, r) if isinstance(r, (int, float)) else r
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=1.0)
        for v in res["verts"]:
            v.co = Vector((v.co.x * rx, v.co.y * ry, v.co.z * rz)) + Vector(c)
        caras = list({f for v in res["verts"] for f in v.link_faces})
        self._marcar(caras, pieza)
        return caras

    def costuras(self, planos, ancho=0.06):
        """Corta el casco con dos planos paralelos por junta y marca la tira de en medio como costura
        (luego costuras_por_valor). Solo en el LOD0: a 48 px salen motas."""
        bm = self.bm
        i_casco, i_cost = self.piezas.index("casco"), self.piezas.index("costura")
        for co, no in planos:
            co, no = Vector(co), Vector(no)
            for d in (-ancho / 2, ancho / 2):
                caras = [f for f in self._casco if f.is_valid and f.material_index == i_casco]
                geom = list({v for f in caras for v in f.verts}) + list({e for f in caras for e in f.edges}) + caras
                r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co + no * d, plane_no=no)
                self._casco |= {g for g in r["geom"] if isinstance(g, bmesh.types.BMFace)}
            for f in [f for f in self._casco if f.is_valid]:
                if abs((f.calc_center_median() - co).dot(no)) < ancho / 2:
                    f.material_index = i_cost

    def tejado(self, c, largo, fondo, alto, pieza="tejado", vuelo=0.0, aguas=4, grosor=0.0):
        """Tejado sobre la caja de planta largo (X) x fondo (Y) con el alero a la altura c[2]. aguas=4: cuatro aguas
        (cumbrera de largo-fondo); aguas=2: dos aguas con hastiales; aguas=1: una sola agua que sube hacia +Y.
        vuelo: lo que sobresale el alero; grosor: canto del alero (0 = sin canto)."""
        bm = self.bm
        cx, cy, z = c
        hx, hy = largo / 2 + vuelo, fondo / 2 + vuelo
        V = lambda x, y, zz: bm.verts.new((cx + x, cy + y, zz))
        b = [V(-hx, -hy, z), V(hx, -hy, z), V(hx, hy, z), V(-hx, hy, z)]
        caras = []
        if aguas == 1:
            t = [V(-hx, hy, z + alto), V(hx, hy, z + alto)]
            caras += [bm.faces.new((b[0], b[1], t[1], t[0])), bm.faces.new((b[1], b[2], t[1])),
                      bm.faces.new((b[3], b[0], t[0])), bm.faces.new((b[2], b[3], t[0], t[1]))]
        else:
            r = max(0.0, hx - hy) if aguas == 4 else hx
            t = [V(-r, 0, z + alto), V(r, 0, z + alto)]
            caras += [bm.faces.new((b[0], b[1], t[1], t[0])), bm.faces.new((b[2], b[3], t[0], t[1]))]
            if aguas == 4:
                caras += [bm.faces.new((b[1], b[2], t[1])), bm.faces.new((b[3], b[0], t[0]))]
            else:
                caras += [bm.faces.new((b[1], b[2], t[1])), bm.faces.new((b[3], b[0], t[0]))]
        caras.append(bm.faces.new(b[::-1]))   # cara de abajo (cierra la pieza)
        if grosor:
            self.caja((cx, cy, z - grosor / 2), (2 * hx, 2 * hy, grosor), pieza)
        self._marcar(caras, pieza)
        return caras

    def objeto(self, nombre, bisel=0.03):
        capa = self.bm.edges.layers.float.get("bevel_weight_edge") or self.bm.edges.layers.float.new("bevel_weight_edge")
        for f in self.biselar:
            if not f.is_valid:
                continue
            for e in f.edges:
                if len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(30):
                    e[capa] = 1.0
        me = bpy.data.meshes.new(nombre)
        self.bm.to_mesh(me)
        self.bm.free()
        obj = bpy.data.objects.new(nombre, me)
        for p in self.piezas:
            obj.data.materials.append(bpy.data.materials.get(p) or bpy.data.materials.new(p))
        bpy.context.scene.collection.objects.link(obj)
        if bisel:
            m = obj.modifiers.new("bisel", "BEVEL")
            m.width = bisel
            m.segments = 1
            m.limit_method = "WEIGHT"
        return obj
