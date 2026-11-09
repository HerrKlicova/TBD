"""Entrega de un bloque detallado en un solo paso (método del Dragón, R1 y R2).

    python entregar.py <modulo_bloque> <salida> --fotos 34.jpg:x0,y0,x1,y1 lateral.jpg:x0,y0,x1,y1 [--claro b8b8b8]
           [--costado 50541c] [--permitir e08c30] [--px 64]

<modulo_bloque> es un script de scripts/bloques/ con construir(nombre, lod) y COLORES. Para cada LOD (0 y 1):
limpiar → paleta_por_piezas → valor_partido(mascara=casco|costura, alisar=0, suavizar=0) → costuras_por_valor →
marcar_faccion("propio") → auditar → medir_legibilidad → vistas → FBX. Después compone la captura obligatoria junto
a la foto real (3/4, tamaño de juego en los dos suelos y lateral) con Pillow, que debe estar en el Python del sistema.
Necesita bpy (módulo de Blender para Python); se ejecuta en la nube, sin tocar el PC de Eugenio.
"""
import argparse, importlib, json, os, subprocess, sys

AQUI = os.path.dirname(os.path.abspath(__file__))
# Fondo de banco de la trinchera a L* ~44 (D-N61, corrección de Lucía de las 10:18: el fondo real va a L* 42-46; el
# 48403c de L* 28 es solo la franja de balasto bajo la vía, que medir_legibilidad sigue midiendo como «trinchera»)
SUELOS = (("trinchera", (0x6e, 0x6a, 0x5a)), ("llano", (0x84, 0x5c, 0x28)))
CLAVE_SUELO = {"trinchera": "fondo_trinchera", "llano": "llano_ocre"}



def mascara_color(o, mascaras, mapa):
    """Atributo de color «Col» por esquina (BYTE_COLOR; el FBX lo exporta y Unity lo lee como color de vértice): cada
    cara toma el (r, g, b) de su pieza en `mapa`; las piezas que no están, negro. Alfa = 1, salvo si la malla trae el
    atributo de cara «cuerpo» (pila de zombis): entonces cada cuerpo lleva su semilla en el alfa."""
    import numpy as np
    n = len(o.data.polygons)
    col = np.zeros((n, 4), dtype=np.float32)
    col[:, 3] = 1.0
    cuenta = {}
    for pieza, rgb in mapa.items():
        m = mascaras.get(pieza)
        if m is not None and m.any():
            col[np.asarray(m, dtype=bool), :3] = rgb
            cuenta[pieza] = int(np.asarray(m).sum())
    cu = o.data.attributes.get("cuerpo")          # semilla por cuerpo en el alfa (Bruno, TBD/Horda 47ac1ed): 255 = sin
    if cu is not None and cu.domain == "FACE":     # cambio; 254, 253… desplazan el tono de ese cuerpo dentro de la LUT
        ids = np.zeros(n, dtype=np.int32)
        cu.data.foreach_get("value", ids)
        sel = ids > 0
        col[sel, 3] = (254 - (ids[sel] - 1) % 200) / 255.0
    at = o.data.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    tot = np.repeat(col, [p.loop_total for p in o.data.polygons], axis=0)
    at.data.foreach_set("color", tot.ravel())
    o.data.color_attributes.active_color = at
    o.data.color_attributes.render_color_index = 0
    return {"atributo": "Col", "caras_por_pieza": cuenta}

def crear_bocas(db, lod, raiz, objs=(), arm=None, sin_hornear=False):
    """Puntos vacíos de boca de arma (Lucía, 8-nov 24:34; mecánica de Marcos «el disparo sale de la boca del arma»).
    db.BOCAS(lod, raiz, objs) devuelve [(nombre, pos, dir, padre)] en coordenadas de mundo de Blender. padre: None (la
    raíz), el nombre de un hijo (Canon, Ametralladora) o "hueso:<nombre>". El disparo sale por el +Z LOCAL del vacío en
    Unity: con bake_space_transform (vehículos y mallas fijas) eso es su −Y local de Blender; sin hornear (soldado con
    esqueleto), su +Z local de Blender. No toca mallas ni pivotes."""
    import bpy
    from mathutils import Matrix, Vector
    fn = getattr(db, "BOCAS", None)
    if fn is None:
        return []
    horneado = arm is None and not sin_hornear
    hijos = {x.name.split(".")[0]: x for x in objs}
    out = []
    for nombre, pos, dirc, padre in fn(lod, raiz, objs):
        d = Vector(dirc).normalized()
        arriba = Vector((0, 0, 1)) if abs(d.z) < 0.95 else Vector((0, 1, 0))
        x = arriba.cross(d).normalized() if not horneado else (-d).cross(arriba).normalized()
        if horneado:          # −Y local = disparo, +Z local ≈ arriba
            y = -d
            z = x.cross(y).normalized()
        else:                 # +Z local = disparo, +Y local ≈ arriba
            z = d
            y = z.cross(x).normalized()
        M = Matrix((x, y, z)).transposed().to_4x4()
        M.translation = Vector(pos)
        e = bpy.data.objects.new(nombre, None)
        e.empty_display_type = "SINGLE_ARROW"
        e.empty_display_size = 0.3
        bpy.context.scene.collection.objects.link(e)
        if padre and padre.startswith("hueso:"):
            # un vacío emparentado a un hueso sale mal en el FBX (comprobado: 1,9 m desplazado); va como HUESO hoja sin
            # pesos, hijo del hueso del arma. En Unity es un Transform «Muzzle_n» bajo ese hueso, y sigue los clips.
            bpy.data.objects.remove(e, do_unlink=True)
            bpy.context.view_layer.objects.active = arm
            bpy.ops.object.mode_set(mode="EDIT")
            eb = arm.data.edit_bones.new(nombre)
            Mi = arm.matrix_world.inverted()
            h = Mi @ Vector(pos)
            eb.head = h
            eb.tail = h + (Mi.to_3x3() @ y).normalized() * 0.08      # el hueso apunta hacia arriba (su Y)
            eb.align_roll(Mi.to_3x3() @ d)                           # y su Z local, hacia donde sale el disparo
            eb.parent = arm.data.edit_bones[padre.split(":", 1)[1]]
            eb.use_deform = False
            bpy.ops.object.mode_set(mode="OBJECT")
            continue
        else:
            e.parent = hijos.get(padre, raiz) if padre else raiz
        bpy.context.view_layer.update()
        e.matrix_world = M
        bpy.context.view_layer.update()
        out.append(e)
    return out


def generar(modulo, salida, lod, claro, costado, permitir, px):
    import bpy
    import numpy as np
    bpy.ops.wm.read_factory_settings(use_empty=True)
    p = os.path.join(AQUI, "malla3d.py")
    ns = {"__file__": p, "__name__": "malla3d"}
    exec(open(p, encoding="utf-8").read(), ns)
    sys.path.insert(0, os.path.join(AQUI, "bloques"))
    db = importlib.import_module(modulo)
    var = os.environ.get("TBD_VARIANTE")   # variantes (Eugenio 6-nov: al menos 5 por estructura); el bloque la lee
    nombre = "SM_%s%s_LOD%d" % (modulo, "_v%s" % var if var else "", lod)
    o = db.construir(nombre, lod=lod)
    out = os.path.join(salida, "lod%d" % lod)
    os.makedirs(out, exist_ok=True)
    fija = o.get("tbd_base_fija")   # pivote impuesto por el bloque (mallas fijas en pose: el mismo que en reposo)
    inf = {"limpiar": ns["limpiar"](o, origen_base=fija is None)}
    if fija is not None:
        from mathutils import Matrix, Vector
        o.data.transform(Matrix.Translation(-Vector(fija)))
        o.data.update()
        o["tbd_base"] = [float(x) for x in fija]
    colores = {**db.COLORES, **(getattr(db, "COLORES_LOD1", {}) if lod else {})}
    permitir = tuple(permitir) + tuple(getattr(db, "PERMITIR", ()))   # excepciones de color del bloque (p. ej. 642030)
    r = ns["paleta_por_piezas"](o, colores, ruta_textura=os.path.join(out, "T_Paleta32.png"), permitir=permitir)
    m = r["mascaras"].get("casco")
    mc = r["mascaras"].get("costura")
    if m is not None:   # los bloques sin casco (p. ej. el zombi) solo usan VALOR_PIEZAS
        mc = np.zeros_like(m) if mc is None else mc
        inf["valor_partido"] = ns["valor_partido"](o, claro=claro, costado=costado, alisar=0, suavizar=0,
                                                   mascara=m | mc, abajo=getattr(db, "ABAJO_CASCO", None))
    for pieza, vp in getattr(db, "VALOR_PIEZAS", {}).items():   # p. ej. la torre del LOD1 en olive_light
        cl, co, ab = (tuple(vp) + (None,))[:3]   # tercer valor opcional: color de las caras de abajo
        mp = r["mascaras"].get(pieza)
        if mp is not None and mp.any():
            pc = (co,) if getattr(db, "FACCION", "propio") == "zombi" else ()   # el zombi es dueño de sus grises
            inf["valor_" + pieza] = ns["valor_partido"](o, claro=cl, costado=co, alisar=0, suavizar=0, mascara=mp,
                                                        permitir_costado=pc, abajo=ab)
    mv = getattr(db, "MASCARA_COLOR", None)   # D-N80 (Bruno): máscara de color por vértice para teñir por instancia
    if mv:
        inf["mascara_color"] = mascara_color(o, r["mascaras"], mv(lod) if callable(mv) else mv)
    if mc is not None and mc.any():
        inf["costuras"] = ns["costuras_por_valor"](o, mc, suelo_L=getattr(db, "L_MIN", 0.0))
    inf["faccion"] = ns["marcar_faccion"](o, getattr(db, "FACCION", "propio"))
    if getattr(db, "TEXTURA_REAL", False) and os.environ.get("TBD_TEXTURA", "1") != "0":
        # textura horneada de verdad (Eugenio 7-nov 09:20: nada de parches): una textura por vehículo para los dos LOD
        sys.path.insert(0, AQUI)
        import textura_real
        ruta_t = os.path.join(salida, "T_Albedo.png")
        if lod == 0 and os.path.exists(ruta_t):
            os.remove(ruta_t)
        inf["textura"] = textura_real.hornear(o, r["mascaras"], ruta_t, lod,
                                              int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5, ns["cargar_paleta"]())
        ns["_material_paleta"](o, ruta_t)
    a = ns["auditar"](o, presupuesto=getattr(db, "PRESUPUESTO", {}).get(lod, (500, 4000) if lod else (1500, 4000)))
    inf["veredicto"] = a["veredicto"]
    inf["avisos"] = [(x["clave"], x["valor"]) for x in a["puntos"] if x["nivel"] != "OK"]
    inf["legibilidad"] = ns["medir_legibilidad"](o)["suelos"]
    inf["criterio_legibilidad"] = getattr(db, "LEGIBILIDAD", "top20_o_contorno")
    inf["medidas_m"] = [round(d, 2) for d in o.dimensions]
    if getattr(db, "NORMALES_ARRIBA", False):   # matas y piedras del suelo (Lucía, suelo-sagunto.md §8): luz del suelo
        me = o.data   # True: toda la pieza; una lista de piezas: solo esas caras (p. ej. el trozo de prueba del suelo)
        na = db.NORMALES_ARRIBA
        idx = None if na is True else {db.PIEZAS.index(p) for p in na}
        ns_ = []
        k = getattr(db, "NORMALES_INCLINACION", None)   # None: del todo hacia arriba; k: n + k·Z (conserva el volumen)
        for pol in me.polygons:
            if idx is None or pol.material_index in idx:
                if k is None:
                    n = (0.0, 0.0, 1.0)
                else:
                    v = pol.normal.copy()
                    v.z += k
                    n = tuple(v.normalized())
            else:
                n = tuple(pol.normal)
            ns_ += [n] * pol.loop_total
        me.normals_split_custom_set(ns_)
        o["tbd_normales_arriba"] = 1
        inf["normales_arriba"] = True
    u = getattr(db, "UMBRAL_CONTORNO", {}).get(lod, 0.25 if lod else 0.06)
    ov = o
    md = r["mascaras"].get("disco")
    if md is not None and md.any():
        # el disco del rotor en marcha (Lucía, 23:29) va en el FBX, pero en las vistas tapaba el aparato entero: se
        # dibuja la copia sin él, con las palas (el rotor parado)
        import bmesh
        ov = o.copy()
        ov.data = o.data.copy()
        bpy.context.scene.collection.objects.link(ov)
        b_ = bmesh.new()
        b_.from_mesh(ov.data)
        b_.faces.ensure_lookup_table()
        bmesh.ops.delete(b_, geom=[f for f in b_.faces if md[f.index]], context="FACES")
        b_.to_mesh(ov.data)
        b_.free()
        o.hide_render = True
        o.hide_set(True)
    for suelo, hx in SUELOS:
        ns["vista_pixelada"](ov, os.path.join(out, "%s_rts.png" % suelo), vista="rts", fondo=hx, umbral_contorno=u)
        ns["vista_pixelada"](ov, os.path.join(out, "%s_juego.png" % suelo), vista="rts", res=px, ampliar=384 // px,
                             fondo=hx, umbral_contorno=u)
    for v in ("izquierda", "frente"):
        ns["vista_pixelada"](ov, os.path.join(out, "%s.png" % v), vista=v, fondo=SUELOS[0][1], umbral_contorno=u)
    if "textura" in inf:   # de cerca, a la escala de la captura de Eugenio (~40 px/m): para juzgar la textura
        ns["vista_pixelada"](ov, os.path.join(out, "cerca_rts.png"), vista="rts", res=320, ampliar=2,
                             fondo=SUELOS[0][1], umbral_contorno=u)
    if ov is not o:
        bpy.data.objects.remove(ov, do_unlink=True)
        o.hide_render = False
        o.hide_set(False)
    if hasattr(db, "rig"):   # personajes con esqueleto (soldado): armature + clips como tomas del FBX
        arm = db.rig(o, lod)
        inf["rig"] = {"huesos": [b.name for b in arm.data.bones],
                      "clips": [a.name for a in bpy.data.actions]}
        bocas = crear_bocas(db, lod, o, arm=arm)
        inf["bocas"] = {e.name: [round(c, 3) for c in e.matrix_world.translation] for e in bocas}
        ns["exportar_unity"](o, os.path.join(out, nombre + ".fbx"), armature=arm, extra=bocas)
        if lod == 1:   # fotogramas de cada clip a tamaño de juego, para la hoja de animaciones
            for act in bpy.data.actions:
                arm.animation_data.action = act
                f0, f1 = (int(x) for x in act.frame_range)
                for k, f in enumerate(range(f0, f1 + 1, max(1, (f1 - f0) // 5))):
                    bpy.context.scene.frame_set(f)
                    ns["vista_pixelada"](o, os.path.join(out, "pose_%s_%02d.png" % (act.name, k)), vista="rts",
                                         fondo=SUELOS[0][1], umbral_contorno=u)
                    ns["vista_pixelada"](o, os.path.join(out, "lat_%s_%02d.png" % (act.name, k)), vista="izquierda",
                                         fondo=SUELOS[0][1], umbral_contorno=u)
            arm.animation_data.action = None
    elif hasattr(db, "separar"):   # vehículos con piezas animables: casco + hijos (ruedas, torre, rotores…)
        objs = db.separar(o, lod)
        bpy.context.view_layer.update()
        inf["piezas_animables"] = {x.name: [round(c, 3) for c in x.matrix_world.translation] for x in objs[1:]}
        inf["triangulos_por_objeto"] = {x.name: sum(len(p.vertices) - 2 for p in x.data.polygons) for x in objs}
        bocas = crear_bocas(db, lod, o, objs=objs[1:])
        inf["bocas"] = {e.name: [round(c, 3) for c in e.matrix_world.translation] for e in bocas}
        ns["exportar_unity"](o, os.path.join(out, nombre + ".fbx"), extra=list(objs[1:]) + bocas)
        # comprobación: la torre (o el rotor) girada 50° sobre la vertical y el cañón elevado 20°, con las mismas
        # transformaciones locales que usará Unity; luego se hornea en una sola malla para el render
        import math
        from mathutils import Matrix
        for x in objs[1:]:
            if x.name.startswith(("Torre", "RotorPrincipal", "Aro")):
                x.rotation_euler.z = math.radians(50)
            if x.name.startswith(("Canon", "Ametralladora")):
                x.rotation_euler.x = math.radians(-20)     # en Blender, elevar el cañón (que mira a −Y) es X negativo
            if x.name in getattr(db, "DIRECCION", ()):   # ruedas directrices: giradas 25° sobre la vertical
                x.rotation_euler.z = math.radians(25)
            if x.name.startswith("Barrera"):             # barreras de paso a nivel: subidas 85° (brazo hacia su −Y)
                x.rotation_euler.x = math.radians(-85)
            if x.name.startswith("RotorCola"):
                x.rotation_euler.x = math.radians(30)
            g = getattr(db, "GIRO_PRUEBA", {}).get(x.name)   # giro de prueba propio del módulo: {hijo: (eje, grados)}
            if g:
                setattr(x.rotation_euler, g[0].lower(), math.radians(g[1]))
        bpy.context.view_layer.update()
        copias = []
        for x in objs:
            c = x.copy()
            c.data = x.data.copy()
            bpy.context.scene.collection.objects.link(c)
            mw = x.matrix_world.copy()
            c.parent = None
            c.matrix_world = Matrix.Identity(4)
            c.data.transform(mw)
            copias.append(c)
        for x in objs[1:]:
            x.rotation_euler = (0, 0, 0)
        bpy.ops.object.select_all(action="DESELECT")
        for c in copias:
            c.select_set(True)
        bpy.context.view_layer.objects.active = copias[0]
        bpy.ops.object.join()
        ns["vista_pixelada"](copias[0], os.path.join(out, "animable_giro.png"), vista="rts", fondo=SUELOS[0][1],
                             umbral_contorno=u)
    else:
        sin_hornear = bool(getattr(db, "SIN_HORNEAR", False))
        bocas = crear_bocas(db, lod, o, sin_hornear=sin_hornear)
        if bocas:
            inf["bocas"] = {e.name: [round(c, 3) for c in e.matrix_world.translation] for e in bocas}
        ns["exportar_unity"](o, os.path.join(out, nombre + ".fbx"), extra=bocas, hornear=not sin_hornear)
    with open(os.path.join(out, "informe.json"), "w", encoding="utf-8") as f:
        json.dump(inf, f, ensure_ascii=False, indent=1, default=str)
    return inf


def _L(rgb):
    """L* de colores sRGB (N,3) 0-255."""
    import numpy as np
    c = np.asarray(rgb, float) / 255.0
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    y = c @ np.array([0.2126, 0.7152, 0.0722])
    return np.where(y > 0.008856, 116 * np.cbrt(y) - 16, 903.3 * y)


def cuerpo_medio(salida):
    """Criterio de los zombis (Lucía, 6-nov): L* medio de la figura a tamaño de juego contra el suelo, ΔL* >= 25.
    Se añade al informe.json de cada LOD cuyo bloque declara LEGIBILIDAD = "cuerpo_medio"."""
    import numpy as np
    from PIL import Image
    for lod in (0, 1):
        ri = os.path.join(salida, "lod%d" % lod, "informe.json")
        inf = json.load(open(ri, encoding="utf-8"))
        if inf.get("criterio_legibilidad") != "cuerpo_medio":
            continue
        for suelo, hx in SUELOS:
            px_ = np.asarray(Image.open(os.path.join(salida, "lod%d" % lod, "%s_juego.png" % suelo)).convert("RGB"))
            px_ = px_.reshape(-1, 3).astype(int)
            fig = px_[np.abs(px_ - np.array(hx)).sum(1) > 6]
            d = abs(float(_L(fig).mean()) - float(_L([hx])[0]))
            clave = CLAVE_SUELO[suelo]
            inf["legibilidad"].setdefault(clave, {}).update(dL_cuerpo_medio=round(d, 1), ok_cuerpo_medio=d >= 25)
        json.dump(inf, open(ri, "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
        print("lod%d cuerpo medio:" % lod, {k: v.get("dL_cuerpo_medio") for k, v in inf["legibilidad"].items()})


def captura(salida, fotos, titulo, px):
    from PIL import Image, ImageDraw, ImageFont
    S = 384
    try:
        F = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 13)
    except OSError:
        F = ImageFont.load_default()

    def foto(spec):
        if spec == "pendiente":
            c = Image.new("RGB", (S, S), (72, 64, 60))
            ImageDraw.Draw(c).text((S // 2 - 50, S // 2), "foto pendiente", fill=(235, 235, 235), font=F)
            return c
        ruta, caja = spec.rsplit(":", 1)
        im = Image.open(ruta).convert("RGB").crop(tuple(int(v) for v in caja.split(",")))
        k = S / max(im.size)
        im = im.resize((int(im.width * k), int(im.height * k)), Image.LANCZOS)
        c = Image.new("RGB", (S, S), (72, 64, 60))
        c.paste(im, ((S - im.width) // 2, (S - im.height) // 2))
        return c

    def juego(im):
        return im.resize((px, px), Image.BOX).resize((S, S), Image.NEAREST)

    f34, flat = foto(fotos[0]), foto(fotos[-1])
    L = lambda p: Image.open(os.path.join(salida, p)).convert("RGB").resize((S, S), Image.NEAREST)
    filas = [("3/4 de cerca", [f34, L("lod0/trinchera_rts.png"), L("lod1/trinchera_rts.png")]),
             ("Tamaño de juego (%d px), trinchera" % px, [juego(f34), L("lod0/trinchera_juego.png"),
                                                          L("lod1/trinchera_juego.png")]),
             ("Tamaño de juego, llano", [juego(f34), L("lod0/llano_juego.png"), L("lod1/llano_juego.png")]),
             ("Lateral", [flat, L("lod0/izquierda.png"), L("lod1/izquierda.png")])]
    cab = ["Foto real", "LOD0: de cerca", "LOD1: tamaño de juego"]
    top = 26
    c = Image.new("RGB", (S * 3, top + (S + top) * 4), (24, 24, 28))
    d = ImageDraw.Draw(c)
    for i, t in enumerate(cab):
        d.text((i * S + 8, 6), t if i else "%s · %s" % (t, titulo), fill=(235, 235, 235), font=F)
    for r, (t, ims) in enumerate(filas):
        y = top + r * (S + top)
        d.text((8, y + 6), t, fill=(200, 200, 200), font=F)
        for i, im in enumerate(ims):
            c.paste(im, (i * S, y + top))
    ruta = os.path.join(salida, "captura_foto.png")
    c.save(ruta)
    return ruta


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("modulo")
    ap.add_argument("salida")
    ap.add_argument("--fotos", nargs="+", default=["pendiente"],
                    help="ruta:x0,y0,x1,y1 (la primera 3/4, la última lateral). Sin fotos, la columna dice «foto pendiente»: "
                         "solo para adelantar el bloque con medidas; la entrega final lleva la foto (norma 3D)")
    ap.add_argument("--titulo", default="")
    ap.add_argument("--claro", default="b4a080")
    ap.add_argument("--costado", default="50541c")
    ap.add_argument("--permitir", nargs="*", default=[])
    ap.add_argument("--px", type=int, default=64)
    ap.add_argument("--python_bpy", default=sys.executable, help="Python con bpy")
    ap.add_argument("--solo_lod", type=int)
    a = ap.parse_args()
    if a.solo_lod is not None:
        print(json.dumps(generar(a.modulo, a.salida, a.solo_lod, a.claro, a.costado, tuple(a.permitir), a.px),
                         ensure_ascii=False, default=str))
        return
    for lod in (0, 1):   # un proceso por LOD: bpy no se reinicia limpio dentro del mismo intérprete
        cmd = [a.python_bpy, __file__, a.modulo, a.salida, "--fotos", *a.fotos, "--claro", a.claro,
               "--costado", a.costado, "--px", str(a.px), "--solo_lod", str(lod)]
        if a.permitir:
            cmd += ["--permitir", *a.permitir]
        res = subprocess.run(cmd, capture_output=True, text=True)
        print(res.stdout.strip().splitlines()[-1] if res.stdout.strip() else res.stderr[-2000:])
    cuerpo_medio(a.salida)
    print(captura(a.salida, a.fotos, a.titulo, a.px))


if __name__ == "__main__":
    main()
