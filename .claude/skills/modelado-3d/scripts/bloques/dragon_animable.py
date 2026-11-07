"""VCR 8x8 Dragón con las piezas animables separadas (plan de la noche del 7-nov, Lucía §2). Teo, 6-nov-2026.

Es el Dragón v5 aprobado (dragon_v5.py), sin cambiar la geometría ni los colores: solo se parte en objetos para que
Marcos pueda animarlo. Jerarquía del FBX (todo mira a +Z en Unity, como siempre):
  SM_VCR_Dragon_LODn            casco (origen en el suelo, centro del vehículo)
  ├─ Rueda_1L … Rueda_4R        8 ruedas; origen en el eje de cada rueda; giran sobre su X local (el eje del vehículo)
  │                             (1 = la delantera; L = izquierda del vehículo, que en Blender es +X)
  └─ Torre                      origen en el eje de giro de la torre; gira sobre su Y de Unity (la vertical)
     └─ Canon                   cañón de 30 mm con su bocacha; origen en el muñón; se eleva sobre su X local
Las ruedas 1 y 2 son las directrices (en el 8x8 real giran los dos primeros ejes): Marcos les puede dar el giro de
dirección sobre la vertical. Cómo se separa: por islas de geometría (cada rueda, la torre y el cañón son piezas
sueltas), con el centro de cada isla. SIN VERIFICAR: el ángulo máximo de elevación del cañón (la ficha no está
en las referencias); proponer de −10° a +45°.
"""
import math
import bmesh
import bpy
from mathutils import Vector
import os
import dragon_v5 as D
import acabado as A

PIEZAS = D.PIEZAS
# D-N80 (Bruno; Lucía 23:29: vale también para vehículos): ningún albedo propio bajo L* 28
# y nada de valores de la horda: el metal (cañón, antenas) pasa de 1c200c a 48403c, como las armas del soldado, y las
# caras de abajo del casco y de la torre se quedan en 50541c (por defecto caían en 442c0c)
VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5   # acabado de camuflaje (acabado.py): 0 boscoso … 4 urbano
COLORES = A.colores(VAR, dict(D.COLORES, optica="60546c", goma="48403c", metal="48403c"))
COLORES_LOD1 = D.COLORES_LOD1
ABAJO_CASCO = "50541c"      # caras de abajo del casco y de las costuras (por defecto caían en 442c0c)
L_MIN = 28.0                # D-N80 también en las costuras: las del costado 50541c no tienen paso oscuro válido y se quedan
                            # lisas (antes iban a 303424); las del techo b4a080 siguen en 88885c
VALOR_PIEZAS = A.valores(VAR, {"casco": ("b4a080", "50541c", "50541c"), "torre": ("88885c", "50541c", "50541c")})
FACCION = getattr(D, "FACCION", "propio")
PRESUPUESTO = getattr(D, "PRESUPUESTO", {})
UMBRAL_CONTORNO = getattr(D, "UMBRAL_CONTORNO", {})
EJES_Y = (-2.44, -1.01, 0.52, 2.08)
TZ = D.HULL_Z
PIVOTE_TORRE = (0.25, 0.12, TZ)
PIVOTE_CANON = (0.25, -0.75, TZ + 0.52)


def construir(nombre="SM_VCR_Dragon", lod=0):
    o = D.construir(nombre, lod=lod)
    A.aplicar(o, lod, VAR, base=("casco", "torre"))
    return o


def _islas(bm):
    bm.faces.ensure_lookup_table()
    visto, islas = set(), []
    for f in bm.faces:
        if f.index in visto:
            continue
        pila, isla = [f], []
        visto.add(f.index)
        while pila:
            g = pila.pop()
            isla.append(g.index)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in visto:
                        visto.add(h.index)
                        pila.append(h)
        islas.append(isla)
    return islas


def _grupo(f_centro):
    x, y, z = f_centro
    for i, ye in enumerate(EJES_Y):                      # ruedas: cerca del eje (x de 0,9 a 1,6) y bajo 1,45 m
        if abs(y - ye) < 0.75 and 0.85 < abs(x) < 1.65 and z < 1.45:
            return "Rueda_%d%s" % (i + 1, "L" if x > 0 else "R")
    if -0.75 < x < 1.3 and -3.6 < y < 0.8 and z > TZ + 0.10:
        if y < -0.95 and -0.05 < x < 0.55:
            return "Canon"
        return "Torre"
    return None


def separar(o, lod):
    """Parte el objeto ya pintado en casco, 8 ruedas, torre y cañón. Devuelve [casco, hijos...]."""
    base0 = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    bm = bmesh.new()
    bm.from_mesh(o.data)
    grupos = {}
    for isla in _islas(bm):
        bm.faces.ensure_lookup_table()
        vs = {v for i in isla for v in bm.faces[i].verts}
        c = sum((v.co for v in vs), Vector()) / len(vs)
        g = _grupo(tuple(c + base0))   # en coordenadas del bloque
        if g:
            grupos.setdefault(g, []).extend(isla)
    bm.free()
    piv = {"Torre": PIVOTE_TORRE, "Canon": PIVOTE_CANON}
    for k in grupos:
        if k.startswith("Rueda"):
            i = int(k[6]) - 1
            xs = [1.22, -1.22] if lod == 0 else [1.32, -1.32]
            piv[k] = (xs[0] if k.endswith("L") else xs[1], EJES_Y[i], 0.68)
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))   # desplazamiento de limpiar() (origen a la base)
    hijos = {}
    quitar = set()
    for k, caras in grupos.items():
        n = o.copy()
        n.data = o.data.copy()
        n.name = n.data.name = k
        bpy.context.scene.collection.objects.link(n)
        b = bmesh.new()
        b.from_mesh(n.data)
        b.faces.ensure_lookup_table()
        sel = set(caras)
        bmesh.ops.delete(b, geom=[f for f in b.faces if f.index not in sel], context="FACES")
        p = Vector(piv[k]) - base
        bmesh.ops.translate(b, verts=b.verts, vec=-p)
        b.to_mesh(n.data)
        b.free()
        n.location = p
        hijos[k] = n
        quitar |= sel
    b = bmesh.new()
    b.from_mesh(o.data)
    b.faces.ensure_lookup_table()
    bmesh.ops.delete(b, geom=[f for f in b.faces if f.index in quitar], context="FACES")
    b.to_mesh(o.data)
    b.free()
    for k, n in hijos.items():   # el casco está en el origen: los hijos llevan su posición tal cual; el cañón, la
        if k == "Canon" and "Torre" in hijos:   # relativa a la torre
            n.parent = hijos["Torre"]
            n.location = Vector(piv[k]) - Vector(piv["Torre"])
        else:
            n.parent = o
    if "tbd_base" in o:
        del o["tbd_base"]
    return [o] + list(hijos.values())
