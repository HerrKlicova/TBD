"""Acabados de camuflaje en el albedo para los vehículos propios (Dragón, VAMTAC, NH90). Teo, 7-nov-2026.

Por qué (coordinador, 7-nov 08:53: «los vehículos salen planos en la build»): el FBX de la build lleva bien el material,
la textura de la paleta y las UV, pero el albedo del casco solo tenía dos valores (techo b4a080 y costados 50541c), y
con el doble sRGB y el cuantizador al 65 % de la build (D-N129 §0, arreglo de Marcos) se quedaba en un bloque oliva
liso. D-N129: la paleta va en los albedos, así que el detalle tiene que estar en el albedo.
Cómo: se corta el casco (y la torre o el aro) con una rejilla de planos (1,0 m en el LOD0 y 1,2 m en el LOD1, para que
ninguna mancha baje de 2 px) y cada celda toma una pieza de camuflaje según un ruido 3D de su centro (mathutils.noise,
el mismo patrón en los dos LOD). Cada pieza nueva tiene su valor partido (techo, costado y abajo) en VALOR_PIEZAS, así
que se sigue leyendo el volumen. Los cortes no separan islas ni cambian el atributo «parte»: la separación de las
piezas animables sigue igual.
Acabados (TBD_VARIANTE % 5):
  0 boscoso (tres tonos, el de la OTAN): oliva con manchas pardas y verde grisáceo.
  1 liso: el verde aprobado hasta hoy, sin manchas (sale igual que antes).
  2 árido: arena con manchas pardas y oliva (despliegue en zona seca).
  3 embarrado: el verde liso con una franja de barro seco abajo (hasta ~1,3 m) y salpicaduras por todo.
  4 urbano: grises verdosos con manchas claras y oscuras.
Reglas: D-N80 (ningún albedo propio bajo L* 28: nada de 48403c ni 543820 en las manchas), ningún costado en colores de
suelo ni de zombi (COSTADOS_PROHIBIDOS: 845c28, 48403c, 6c7874, 94948c, 404460, 60546c), sin rojos.
Los colores son una propuesta para que los revise Bruno (SIN VERIFICAR en la build: depende del arreglo del sRGB).
"""
import bmesh
from mathutils import Vector, noise

NOMBRES = {0: "boscoso", 1: "liso", 2: "arido", 3: "embarrado", 4: "urbano"}
# acabado -> (casco, torre, {pieza: triplete}); triplete = (techo, costado, abajo)
TABLA = {
    0: (("88885c", "50541c", "50541c"), ("88885c", "50541c", "50541c"),
        {"camo_a": ("a86c50", "6c4038", "6c4038"), "camo_b": ("b4a080", "70784c", "50541c")}),
    1: (None, None, {}),
    2: (("b4a080", "847c34", "605c48"), ("b4a080", "847c34", "605c48"),
        {"camo_a": ("a86c50", "6c4038", "6c4038"), "camo_b": ("88885c", "50541c", "50541c")}),
    3: (None, None, {"camo_a": ("a86c50", "a86c50", "a86c50")}),   # barro seco, claro: el oscuro se perdía en la sombra
    4: (("88885c", "70784c", "605c48"), ("88885c", "70784c", "605c48"),
        {"camo_a": ("b4a080", "605c48", "605c48"), "camo_b": ("70784c", "50541c", "50541c")}),
}
ESCALA = 1.7          # tamaño típico de una mancha (m)


def valores(acabado, valor_piezas):
    """VALOR_PIEZAS del módulo con el acabado aplicado."""
    casco, torre, extra = TABLA[acabado]
    vp = dict(valor_piezas)
    if casco:
        vp["casco"] = casco
        if "torre" in vp:
            vp["torre"] = torre
    vp.update(extra)
    return vp


def colores(acabado, colores_mod):
    """COLORES del módulo con las piezas del camuflaje (el color plano es el del costado; valor_partido lo reparte)."""
    c = dict(colores_mod)
    for p, t in TABLA[acabado][2].items():
        c[p] = t[1]
    return c


def _pieza(acabado, c):
    """Pieza de camuflaje del centro c, o None (se queda en la base)."""
    p = Vector(c) / ESCALA
    n1 = noise.noise(p + Vector((3.1, 7.7, 1.3)))
    n2 = noise.noise(p * 1.3 + Vector((11.2, 2.9, 5.4)))
    if acabado == 3:   # barro: franja baja de borde irregular y salpicaduras hasta 1,6 m
        if c[2] < 1.3 + 0.4 * n1 or n2 > 0.34:
            return "camo_a"
        return None
    if acabado == 2:   # árido: manchas más escasas
        if n1 > 0.28:
            return "camo_a"
        if n2 > 0.38:
            return "camo_b"
        return None
    if n1 > 0.18:
        return "camo_a"
    if n2 > 0.24:
        return "camo_b"
    return None


def aplicar(o, lod, acabado, base=("casco", "torre")):
    """Corta las caras de las piezas `base` con la rejilla y reparte las manchas. Llamar justo después de construir."""
    extra = TABLA[acabado][2]
    if not extra:
        return 0
    nombres = [m.name.split(".")[0] if m else "" for m in o.data.materials]
    import bpy
    for p in extra:
        if p not in nombres:
            o.data.materials.append(bpy.data.materials.get(p) or bpy.data.materials.new(p))
            nombres.append(p)
    ib = {i for i, n in enumerate(nombres) if n in base}
    paso = 1.0 if lod == 0 else 1.2
    bm = bmesh.new()
    bm.from_mesh(o.data)
    for eje in range(3):
        if acabado == 3 and eje != 2 and lod:   # LOD1 del barro: basta con los cortes horizontales y pocos verticales
            paso_e = 2.0
        else:
            paso_e = paso
        caras = [f for f in bm.faces if f.material_index in ib]
        if not caras:
            break
        cs = [v.co[eje] for f in caras for v in f.verts]
        k0, k1 = int(min(cs) // paso_e) + 1, int(max(cs) // paso_e)
        no = Vector((0, 0, 0))
        no[eje] = 1.0
        for k in range(k0, k1 + 1):
            caras = [f for f in bm.faces if f.material_index in ib]
            geom = set(caras)
            for f in caras:
                geom.update(f.edges)
                geom.update(f.verts)
            co = Vector((0, 0, 0))
            co[eje] = k * paso_e + 0.013     # desplazado 1,3 cm: que no caiga justo en una arista del modelo
            bmesh.ops.bisect_plane(bm, geom=list(geom), dist=0.002, plane_co=co, plane_no=no)
    n = 0
    for f in bm.faces:
        if f.material_index in ib:
            p = _pieza(acabado, f.calc_center_median())
            if p:
                f.material_index = nombres.index(p)
                n += 1
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()
    return n
