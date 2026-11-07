"""Textura horneada «de verdad» para los vehículos (Teo, 7-nov 09:25). Eugenio rechazó las manchas planas por cara
(«parece que tiene parches… tomad de referencia imágenes reales»).

En vez de un color de paleta por cara, cada vehículo lleva una textura propia a ~32 px/m (LOD0) y ~16 px/m (LOD1) con lo
que se ve en las fotos de vehículos del ET en uso: la pintura lisa (el Dragón es verde OTAN liso, V1_dragon_34), las
juntas de los paneles y los remaches, los bordes con la pintura gastada, el polvo en los techos y en la parte baja, los
regueros de lluvia que bajan desde los herrajes y el barro de los pasos de rueda. Todo se cuantiza a la paleta de 32
sin tramado (D-N129: la paleta va en el albedo) y sin colores de la banda de la horda.

Una sola textura por vehículo para los dos LOD (el código de Marcos crea un material por carpeta): el LOD0 ocupa la
región (0, 0)-(2/3, 1) y el LOD1 la (2/3, 0)-(1, 1/2) de una textura de 768 x 512.

    hornear(obj, mascaras, ruta_png, lod, uso, paleta)

uso (TBD_VARIANTE % 5): 0 de parque, 1 de campaña, 2 polvo árido, 3 embarrado, 4 castigado.
"""
import math
import os
import bmesh
import bpy
import numpy as np

ANCHO, ALTO = 768, 512
REGION = {0: (0.0, 0.0, 512, 512), 1: (512.0, 0.0, 256, 256)}       # x0, y0 (px desde abajo), ancho, alto
NOMBRES_USO = {0: "parque", 1: "campana", 2: "arido", 3: "embarrado", 4: "castigado"}
#           polvo arriba, suciedad abajo (fuerza, altura m), barro, desgaste de bordes, regueros, óxido, color del polvo
USOS = {0: dict(polvo=0.25, sucio=0.45, h_sucio=0.9, barro=0.0, gasto=0.25, regueros=0.25, oxido=0.0, c_polvo="b4a080",
                c_sucio="847c34"),
        1: dict(polvo=0.45, sucio=0.75, h_sucio=1.45, barro=0.25, gasto=0.45, regueros=0.45, oxido=0.0, c_polvo="b4a080",
                c_sucio="847c34"),
        2: dict(polvo=0.8, sucio=0.85, h_sucio=1.9, barro=0.0, gasto=0.55, regueros=0.2, oxido=0.0, c_polvo="b4a080",
                c_sucio="a86c50"),
        3: dict(polvo=0.25, sucio=0.95, h_sucio=1.6, barro=0.9, gasto=0.35, regueros=0.4, oxido=0.0, c_polvo="847c34",
                c_sucio="6c4038"),
        4: dict(polvo=0.5, sucio=0.7, h_sucio=1.4, barro=0.3, gasto=0.95, regueros=0.8, oxido=0.55, c_polvo="88885c",
                c_sucio="605c48")}
CHAPA = ("casco", "torre", "costura", "camo_a", "camo_b", "mancha", "carga")
SIN_TOCAR = ("optica", "foco", "luz", "disco", "visor")
HORDA = ("000000", "340000", "1c200c", "181c30", "4c1008", "442c0c", "303424", "283440", "543820", "642030", "7c181c",
         "8c5048", "982420", "e08c30", "d4d0cc")


def _hex(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float64)


def _lab(rgb):
    c = rgb / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = c @ M.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


# ------------------------------------------------------------------------------------------- ruido de valor 3D
def _hash(ix, iy, iz, s):
    h = (ix * 374761393 + iy * 668265263 + iz * 2147483647 + s * 1442695041) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return (h & 0xFFFF) / 65535.0


def ruido(p, f, s=0):
    """Ruido de valor 3D en [0, 1], suave (interpolación con smoothstep). p: (n, 3) en metros; f: ciclos por metro."""
    q = p * f
    i = np.floor(q).astype(np.int64)
    t = q - i
    t = t * t * (3 - 2 * t)
    r = 0.0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (t[:, 0] if dx else 1 - t[:, 0]) * (t[:, 1] if dy else 1 - t[:, 1]) * (t[:, 2] if dz else 1 - t[:, 2])
                r = r + w * _hash(i[:, 0] + dx, i[:, 1] + dy, i[:, 2] + dz, s)
    return r


def fbm(p, f, s=0, oct=3):
    a, tot, r = 1.0, 0.0, 0.0
    for k in range(oct):
        r = r + a * ruido(p, f * 2 ** k, s + 17 * k)
        tot += a
        a *= 0.5
    return r / tot


# ------------------------------------------------------------------------------------------- rasterizado en UV
def _uv_nuevas(obj, lod):
    """Desenvuelve en una capa nueva «UV_textura» (proyección inteligente) y la deja como la única."""
    me = obj.data
    capa = me.uv_layers.new(name="UV_textura")
    me.uv_layers.active = capa
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.006, area_weight=1.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=0.006)          # que ocupe toda la región
    bpy.ops.object.mode_set(mode="OBJECT")
    return capa


def hornear(obj, mascaras, ruta_png, lod, uso, paleta, piezas_por_cara=None):
    P = USOS[uso]
    me = obj.data
    pal = np.asarray(paleta, dtype=np.float64)
    hexes = ["%02x%02x%02x" % tuple(int(x) for x in c) for c in pal]
    lab_pal = _lab(pal)
    # tonos naturales de chapa, polvo y tierra: sin violetas (60546c es el zombi), sin el llano 845c28 y sin 6c7874
    natur = ["48403c", "50541c", "605c48", "70784c", "847c34", "88885c", "94948c", "a86c50", "b4a080"]
    if uso == 3:
        natur.append("6c4038")
    if uso == 4:
        natur.append("7c3c18")
    permit = np.array([h in natur for h in hexes])
    # color base de cada cara: el del atlas de paleta (después del valor partido)
    uvp = me.uv_layers["UV_paleta"]
    base = np.zeros((len(me.polygons), 3))
    for p in me.polygons:
        u, v = uvp.data[p.loop_start].uv
        base[p.index] = pal[min(int((1 - v) * 4), 3) * 8 + min(int(u * 8), 7)]
    pieza = np.array([""] * len(me.polygons), dtype=object)
    for n, m in mascaras.items():
        pieza[np.asarray(m, dtype=bool)] = n
    _uv_nuevas(obj, lod)
    uvl = me.uv_layers["UV_textura"]
    x0, y0, W, H = REGION[lod]
    # aristas vivas (para el desgaste de bordes): ángulo diedro > 35° o borde abierto
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    vivas = {}
    for f in bm.faces:
        segs = []
        for e in f.edges:
            if len(e.link_faces) != 2 or e.calc_face_angle(0) > math.radians(35):
                segs.append((np.array(e.verts[0].co), np.array(e.verts[1].co)))
        vivas[f.index] = segs
    bm.free()
    # buffers de texel
    pos = np.zeros((H, W, 3))
    nor = np.zeros((H, W, 3))
    col = np.zeros((H, W, 3))
    pz = np.full((H, W), -1, dtype=np.int64)          # índice de cara
    dborde = np.full((H, W), 9.0)
    co = np.array([v.co for v in me.vertices])
    for p in me.polygons:
        lis = list(range(p.loop_start, p.loop_start + p.loop_total))
        vs = [me.loops[i].vertex_index for i in lis]
        uvs = np.array([uvl.data[i].uv for i in lis]) * np.array([W, H])
        P3 = co[vs]
        n = np.array(p.normal)
        for k in range(1, len(lis) - 1):
            a, b, c = 0, k, k + 1
            ux, uy = uvs[[a, b, c], 0], uvs[[a, b, c], 1]
            bx0, bx1 = max(int(ux.min()) - 1, 0), min(int(math.ceil(ux.max())) + 1, W - 1)
            by0, by1 = max(int(uy.min()) - 1, 0), min(int(math.ceil(uy.max())) + 1, H - 1)
            d = (uy[1] - uy[2]) * (ux[0] - ux[2]) + (ux[2] - ux[1]) * (uy[0] - uy[2])
            if abs(d) < 1e-12 or bx1 < bx0 or by1 < by0:
                continue
            gx, gy = np.meshgrid(np.arange(bx0, bx1 + 1) + 0.5, np.arange(by0, by1 + 1) + 0.5)
            l1 = ((uy[1] - uy[2]) * (gx - ux[2]) + (ux[2] - ux[1]) * (gy - uy[2])) / d
            l2 = ((uy[2] - uy[0]) * (gx - ux[2]) + (ux[0] - ux[2]) * (gy - uy[2])) / d
            l3 = 1 - l1 - l2
            dentro = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
            if not dentro.any():
                continue
            yy, xx = np.nonzero(dentro)
            L = np.stack([l1[dentro], l2[dentro], l3[dentro]], 1).clip(0, 1)
            L /= L.sum(1, keepdims=True)
            q = L @ P3[[a, b, c]]
            ty, tx = yy + by0, xx + bx0
            pos[ty, tx] = q
            nor[ty, tx] = n
            col[ty, tx] = base[p.index]
            pz[ty, tx] = p.index
            segs = vivas.get(p.index, [])
            if segs:
                dm = np.full(len(q), 9.0)
                for s0, s1 in segs:
                    e = s1 - s0
                    t = np.clip(((q - s0) @ e) / max(e @ e, 1e-12), 0, 1)
                    dm = np.minimum(dm, np.linalg.norm(q - (s0 + t[:, None] * e), axis=1))
                dborde[ty, tx] = np.minimum(dborde[ty, tx], dm)
    m = pz >= 0
    q = pos[m]
    nz = nor[m][:, 2]
    c = col[m].copy()
    pi = pieza[pz[m]]
    chapa = np.isin(pi, CHAPA)
    tocar = ~np.isin(pi, SIN_TOCAR)
    goma = pi == "goma"
    lado = np.abs(nz) < 0.7
    arriba = nz >= 0.7
    # 1) variación grande de tono de la pintura (sin manchas: ±5 %)
    c *= (0.95 + 0.10 * fbm(q, 0.6, 1))[:, None]
    # pintura descolorida por el sol en zonas del techo (la foto: el verde de arriba es más claro y desigual)
    sol = arriba & chapa & (fbm(q, 1.6, 2) > 0.62)
    c[sol] = c[sol] * 0.75 + _hex("88885c") * 0.25
    # 2) juntas de paneles y remaches en la chapa: rejilla en el plano de cada cara
    # chapas grandes e irregulares (no una cuadrícula): el paso a lo largo varía chapa a chapa con un hash
    paso_y = 1.55
    celda = np.floor((q[:, 1] + 0.31) / paso_y).astype(np.int64)
    off = (_hash(celda, celda * 0 + 3, celda * 0 + 5, 11) - 0.5) * 0.5        # desplazamiento de cada junta
    yy = (q[:, 1] + 0.31) / paso_y - celda
    gy = np.minimum(np.abs(yy - (0.0 + off * 0)) , np.abs(1 - yy)) * paso_y
    gy = np.abs(((q[:, 1] + 0.31 + off) / paso_y) % 1 - 0.5) * paso_y
    gx = np.abs(np.abs(q[:, 0]) - 0.62)                                      # dos juntas a lo largo del techo
    gz = np.abs(q[:, 2] - 1.75)                                              # una junta horizontal en los costados
    ancho = 0.035 if lod == 0 else 0.07
    junta_v = (paso_y / 2 - gy) < ancho                                      # juntas verticales (cortan Y)
    junta_h = np.where(arriba, gx < ancho, gz < ancho * 0.8)
    junta = chapa & (junta_v | junta_h)
    c[junta] *= 0.72
    if lod == 0:   # remaches: puntos claros cada 0,2 m a 6 cm de cada junta
        r_y = (paso_y / 2 - gy > 0.05) & (paso_y / 2 - gy < 0.09) & ((np.abs(((q[:, 2] + 0.1) / 0.22) % 1 - 0.5)) < 0.14)
        r_x = arriba & (gx > 0.05) & (gx < 0.09) & ((np.abs(((q[:, 1] + 0.1) / 0.22) % 1 - 0.5)) < 0.14)
        c[chapa & ((lado & r_y) | r_x)] = c[chapa & ((lado & r_y) | r_x)] * 0.5 + _hex("88885c") * 0.5
    # 3) bordes con la pintura gastada (desgaste en las aristas vivas, roto por ruido)
    db = dborde[m]
    gasto = (db < (0.06 if lod == 0 else 0.09)) & (ruido(q, 6.0, 3) < 0.25 + 0.7 * P["gasto"]) & tocar & ~goma
    c[gasto] = c[gasto] * 0.4 + _hex("88885c") * 0.6 if P["oxido"] == 0 else c[gasto] * 0.3 + _hex("94948c") * 0.7
    # 4) regueros: bajan por los costados desde el borde de arriba
    if P["regueros"] > 0:
        franja = ruido(np.stack([q[:, 0] * 9 + q[:, 1] * 9, q[:, 2] * 0.3, q[:, 0] * 0 + 1.3], 1), 1.0, 5)
        largo = fbm(q * np.array([3.0, 3.0, 0.4]), 1.0, 6)
        reg = chapa & lado & (franja > 1 - 0.18 * P["regueros"]) & (largo > 0.45)
        c[reg] *= 0.8
    # 5) polvo en las caras de arriba (zonas blandas de ruido; más en los rincones = menos en el centro de los paneles)
    pv = fbm(q, 2.6, 7, oct=3)      # grano fino: polvo, no manchas (Lucía/Eugenio: nada de parches)
    amt = np.clip((pv - (1 - P["polvo"]) * 0.7) / 0.25, 0, 1) * (0.3 + 0.35 * P["polvo"])
    amt = np.where(arriba, amt, amt * 0.3) * tocar
    c = c * (1 - amt[:, None]) + _hex(P["c_polvo"]) * amt[:, None]
    # 6) suciedad de la parte baja, con el borde irregular
    hs = P["h_sucio"] * (0.75 + 0.5 * fbm(q, 0.9, 8))
    sucio = np.clip((hs - q[:, 2]) / (0.6 * hs), 0, 1) ** 0.8 * P["sucio"] * tocar
    c = c * (1 - sucio[:, None] * 0.8) + _hex(P["c_sucio"]) * (sucio[:, None] * 0.8)
    # 7) barro: salpicaduras en la parte baja (y en los pasos de rueda)
    if P["barro"] > 0:
        sal = (ruido(q, 4.5, 9) > 1 - 0.35 * P["barro"]) & (q[:, 2] < 1.6) & tocar
        c[sal] = c[sal] * 0.25 + _hex("6c4038") * 0.75
        c[goma & (q[:, 2] < 0.9)] = c[goma & (q[:, 2] < 0.9)] * 0.6 + _hex("605c48") * 0.4
    # 8) óxido en los bordes gastados y en algún reguero (castigado)
    if P["oxido"] > 0:
        ox = gasto & (ruido(q, 3.0, 10) < P["oxido"])
        c[ox] = _hex("7c3c18")
    # cuantizado a la paleta (Lab, sin tramado y sin colores de la horda); lo que no se toca conserva su color
    cand = np.nonzero(permit)[0]
    lab_c = _lab(np.clip(c, 0, 255))
    d = ((lab_c[:, None, :] - lab_pal[None, cand, :]) ** 2).sum(-1)
    qc = pal[cand[d.argmin(1)]]
    qc[~tocar] = col[m][~tocar]
    out = np.zeros((H, W, 3))
    out[m] = qc
    # margen: 3 px de relleno hacia fuera de cada isla (filtro de punto, pero por si acaso con mips)
    lleno = m.copy()
    for _ in range(3):
        for dy, dx in ((0, 1), (1, 0), (0, -1), (-1, 0)):
            src = np.roll(np.roll(lleno, dy, 0), dx, 1) & ~lleno
            out[src] = np.roll(np.roll(out, dy, 0), dx, 1)[src]
            lleno |= src
    # escribe la región en la textura compartida (los dos LOD van en el mismo archivo)
    from PIL import Image
    if os.path.exists(ruta_png):
        tex = np.asarray(Image.open(ruta_png).convert("RGB")).copy()
    else:
        tex = np.zeros((ALTO, ANCHO, 3), dtype=np.uint8)
        tex[:] = (0x50, 0x54, 0x1c)
    fila0 = ALTO - int(y0) - H
    tex[fila0:fila0 + H, int(x0):int(x0) + W] = np.clip(out[::-1], 0, 255).astype(np.uint8)
    Image.fromarray(tex).save(ruta_png)
    # UV a la región de la textura grande y fuera la capa de la paleta
    for li in range(len(uvl.data)):
        u, v = uvl.data[li].uv
        uvl.data[li].uv = ((x0 + u * W) / ANCHO, (y0 + v * H) / ALTO)
    me.uv_layers.remove(me.uv_layers["UV_paleta"])
    me.uv_layers.active = me.uv_layers["UV_textura"]
    obj["tbd_textura"] = ruta_png
    return {"textura": ruta_png, "texeles": int(m.sum()), "region": REGION[lod], "uso": NOMBRES_USO[uso],
            "px_por_m": round(float(math.sqrt(m.sum() / max(sum(p.area for p in me.polygons), 1e-6))), 1)}
