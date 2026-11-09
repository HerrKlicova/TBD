"""Lee el color de cada vértice (UV contra T_Paleta32) de varios FBX y lo guarda por objeto, en orden."""
import bpy, sys, json, numpy as np
args = json.load(open(sys.argv[-1])); out = {}
for key, (fbx, pal) in args["fbx"].items():
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=fbx)
    im = bpy.data.images.load(pal); W, H = im.size; px = np.array(im.pixels[:]).reshape(H, W, 4)
    objs = {}
    for o in sorted([o for o in bpy.context.scene.objects if o.type == 'MESH'], key=lambda o: o.name):
        me = o.data; uv = me.uv_layers.active
        cols = []
        for l in me.loops:
            u, v = uv.data[l.index].uv; c = px[min(H-1, int(v*H)), min(W-1, int(u*W)), :3]
            cols.append('%02x%02x%02x' % tuple(int(round(x*255)) for x in c))
        nm = o.name.split('.')[0]; nm = 'RAIZ' if nm.startswith('SM_') else nm
        objs[nm] = cols
    out[key] = objs
json.dump(out, open(args["salida"], "w"))
