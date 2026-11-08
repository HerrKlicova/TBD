"""Colisión de las puertas del muro para el asedio (D-N151: con la estación sitiada las puertas se cierran del todo y
pasan a ser un tramo de muro más). Teo, 9-nov.

Una FBX de colisión por modelo de puerta, con la MISMA jerarquía y los MISMOS pivotes que la puerta visible:
  SM_<Puerta>_Col          las dos jambas (cajas de 0,9 m × 1,2 m × ALTO_COL), fijas
  ├─ Hoja_O               caja de la hoja oeste, con el origen en su bisagra
  └─ Hoja_E               caja de la hoja este, con el origen en su bisagra
Marcos gira Hoja_O y Hoja_E de la colisión con los mismos ángulos que las de la puerta visible: cerrada (0°) bloquea
de jamba a jamba; abierta (±90°) deja libre el hueco. En el estado Brecha (e3) las hojas de colisión se desactivan.
Cajas de 6 caras, sin material: solo para la física o para sacar las cotas de los sólidos de la simulación.
La cara de fuera de las hojas está en y = −0,16 (la de los nervios de la blindada, la que más sobresale): la pila de
zombis (pila_zombis.py, origen al pie de la cara de fuera) delante de una puerta va en y = −0,16, 0,39 m más adentro
que delante del muro de HESCO (cara en y = −0,55). Alto 2,1 m, el mismo del muro y de la pila (MURO = 2,1).
Uso: bvenv/python colision_puertas.py <salida>
"""
import os, sys
import bpy
AQUI = os.path.dirname(os.path.abspath(__file__))
ns = {"__file__": os.path.join(AQUI, "malla3d.py"), "__name__": "malla3d"}
exec(open(ns["__file__"], encoding="utf-8").read(), ns)

ALTO_COL = 2.1
FUERA, DENTRO = -0.16, 0.10          # caras de las hojas (y de Blender; −Y = fuera)
MODELOS = {                          # nombre: semiancho del hueco hasta la bisagra
    "PuertaVehiculos": 3.99,         # D-N111 / D-N163, hueco de 8 m (Sagunto sur E4 y oeste-sur, Nules sur)
    "PuertaVia": 2.49,               # D-N59, hueco de 5 m (Sagunto oeste y este, Nules apartado oeste)
    "PuertaViaDoble": 3.69,          # Nules, hueco de 7,4 m (oeste y este)
}


def caja(nombre, x0, x1, y0, y1, z0, z1, piv=(0, 0, 0)):
    px, py, pz = piv
    vs = [(x - px, y - py, z - pz) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    fs = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    me = bpy.data.meshes.new(nombre)
    me.from_pydata(vs, [], fs)
    me.validate()
    me.update()
    for p in me.polygons:   # normales hacia fuera (el orden de arriba sale hacia dentro en la mitad)
        c = sum((me.vertices[i].co for i in p.vertices), type(me.vertices[0].co)()) / 4
        cc = type(c)(((x0 + x1) / 2 - px, (y0 + y1) / 2 - py, (z0 + z1) / 2 - pz))
        if (c - cc).dot(p.normal) < 0:
            p.flip()
    o = bpy.data.objects.new(nombre, me)
    bpy.context.scene.collection.objects.link(o)
    o.location = piv
    return o


def una(nombre, b, salida):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    j = b + 0.46                                       # centro de la jamba (JAMBA de los bloques)
    raiz = caja("SM_%s_Col" % nombre, -j - 0.45, -b - 0.01, -0.6, 0.6, 0, ALTO_COL)
    der = caja("_der", b + 0.01, j + 0.45, -0.6, 0.6, 0, ALTO_COL)
    bpy.ops.object.select_all(action="DESELECT")
    raiz.select_set(True)
    der.select_set(True)
    bpy.context.view_layer.objects.active = raiz
    bpy.ops.object.join()
    hijos = []
    for hn, s in (("Hoja_O", -1), ("Hoja_E", 1)):
        x0, x1 = sorted((s * b, s * 0.01))
        h = caja(hn, x0, x1, FUERA, DENTRO, 0, ALTO_COL, piv=(s * b, 0, 0))
        h.parent = raiz
        hijos.append(h)
    ruta = os.path.join(salida, "SM_%s_Col.fbx" % nombre)
    ns["exportar_unity"](raiz, ruta, extra=hijos)
    return ruta


if __name__ == "__main__":
    sal = sys.argv[-1]
    for n, b in MODELOS.items():
        print("ok", una(n, b, sal))
