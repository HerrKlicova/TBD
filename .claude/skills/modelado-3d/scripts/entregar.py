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
SUELOS = (("trinchera", (0x48, 0x40, 0x3c)), ("llano", (0x84, 0x5c, 0x28)))


def generar(modulo, salida, lod, claro, costado, permitir, px):
    import bpy
    import numpy as np
    bpy.ops.wm.read_factory_settings(use_empty=True)
    p = os.path.join(AQUI, "malla3d.py")
    ns = {"__file__": p, "__name__": "malla3d"}
    exec(open(p, encoding="utf-8").read(), ns)
    sys.path.insert(0, os.path.join(AQUI, "bloques"))
    db = importlib.import_module(modulo)
    nombre = "SM_%s_LOD%d" % (modulo, lod)
    o = db.construir(nombre, lod=lod)
    out = os.path.join(salida, "lod%d" % lod)
    os.makedirs(out, exist_ok=True)
    inf = {"limpiar": ns["limpiar"](o)}
    colores = {**db.COLORES, **(getattr(db, "COLORES_LOD1", {}) if lod else {})}
    r = ns["paleta_por_piezas"](o, colores, ruta_textura=os.path.join(out, "T_Paleta32.png"), permitir=permitir)
    m = r["mascaras"]["casco"]
    mc = r["mascaras"].get("costura", np.zeros_like(m))
    inf["valor_partido"] = ns["valor_partido"](o, claro=claro, costado=costado, alisar=0, suavizar=0, mascara=m | mc)
    for pieza, (cl, co) in getattr(db, "VALOR_PIEZAS", {}).items():   # p. ej. la torre del LOD1 en olive_light
        mp = r["mascaras"].get(pieza)
        if mp is not None and mp.any():
            inf["valor_" + pieza] = ns["valor_partido"](o, claro=cl, costado=co, alisar=0, suavizar=0, mascara=mp)
    if mc.any():
        inf["costuras"] = ns["costuras_por_valor"](o, mc)
    inf["faccion"] = ns["marcar_faccion"](o, "propio")
    a = ns["auditar"](o, presupuesto=(500, 4000) if lod else (1500, 4000))
    inf["veredicto"] = a["veredicto"]
    inf["avisos"] = [(x["clave"], x["valor"]) for x in a["puntos"] if x["nivel"] != "OK"]
    inf["legibilidad"] = ns["medir_legibilidad"](o)["suelos"]
    inf["medidas_m"] = [round(d, 2) for d in o.dimensions]
    u = 0.25 if lod else 0.06
    for suelo, hx in SUELOS:
        ns["vista_pixelada"](o, os.path.join(out, "%s_rts.png" % suelo), vista="rts", fondo=hx, umbral_contorno=u)
        ns["vista_pixelada"](o, os.path.join(out, "%s_juego.png" % suelo), vista="rts", res=px, ampliar=384 // px,
                             fondo=hx, umbral_contorno=u)
    for v in ("izquierda", "frente"):
        ns["vista_pixelada"](o, os.path.join(out, "%s.png" % v), vista=v, fondo=SUELOS[0][1], umbral_contorno=u)
    ns["exportar_unity"](o, os.path.join(out, nombre + ".fbx"))
    with open(os.path.join(out, "informe.json"), "w", encoding="utf-8") as f:
        json.dump(inf, f, ensure_ascii=False, indent=1, default=str)
    return inf


def captura(salida, fotos, titulo, px):
    from PIL import Image, ImageDraw, ImageFont
    S = 384
    try:
        F = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 13)
    except OSError:
        F = ImageFont.load_default()

    def foto(spec):
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
    ap.add_argument("--fotos", nargs="+", required=True, help="ruta:x0,y0,x1,y1 (la primera 3/4, la última lateral)")
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
    print(captura(a.salida, a.fotos, a.titulo, a.px))


if __name__ == "__main__":
    main()
