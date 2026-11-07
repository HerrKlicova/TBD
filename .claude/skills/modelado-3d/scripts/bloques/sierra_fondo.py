"""Sierra baja del fondo, pieza repetible a lo ancho, detrás del castillo (adenda de D-N68, Lucía 6-nov 18:38). Teo, 6-nov-2026.

Referencia: la sierra que se ve detrás de los naranjos en las fotos E10 de Paula y a contraluz en F5 (2): lomas suaves y
continuas, sin picos, con algún resalte. Tramo de 300 m de ancho y 30 a 40 m de alto (Lucía), 120 m de fondo, con sección de lomo (laderas a unos 30°).
REPETIBLE: los dos extremos acaban a la misma altura (24 m) y en el mismo punto de la cresta, así que Marcos puede poner
tramos uno detrás de otro a lo ancho, cada 300 m en X, mezclando las 5 variantes para que no se note la repetición.
Colores: arriba 94948c y costados 88885c (valor partido), con menos contraste que el castillo; sin contorno por dentro.
Nada de oliva, ni 50541c, ni valores de la horda.
MEDIDAS SIN VERIFICAR (no hay plano de la sierra: es un telón). Origen en el centro del tramo, al pie.
Se construye a lo largo de Y y se gira 90° para que corra a lo largo de X.
La cresta va en zigzag en planta (±22 m), para que las laderas tengan pliegues con luz y sombra y no se lea como una
franja lisa. Variantes: 0 a 4, cinco perfiles distintos con la misma altura y posición en los extremos.
"""
import math, os, random
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["sierra"]
COLORES = {"sierra": "94948c"}
VALOR_PIEZAS = {"sierra": ("94948c", "88885c")}
FACCION = "neutro"
PRESUPUESTO = {0: (20, 400), 1: (20, 300)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
ANCHO, H_EXT, FONDO = 300.0, 24.0, 120.0


def perfil(v):
    R = random.Random(1000 + v)
    n = 9
    pts = [(-ANCHO / 2, H_EXT)]
    for i in range(1, n):
        y = -ANCHO / 2 + ANCHO * i / n + R.uniform(-8, 8)
        base = 30 + 8 * math.sin(math.pi * i / n + v)              # una loma por tramo, desplazada según la variante
        pts.append((y, max(26.0, min(40.0, base + R.uniform(-4, 5)))))
    pts.append((ANCHO / 2, H_EXT))
    return pts


def construir(nombre="SM_SierraFondo", lod=0):
    """Sección de lomo (frente y trasera en pendiente hasta la cresta): todas las caras son cuadriláteros convexos."""
    import bmesh
    t = Taller(PIEZAS)
    v = VAR % 5
    bm = t.bm
    F, G, Rr = [], [], []
    R = random.Random(2000 + v)
    pts = perfil(v)
    for i, (y, z) in enumerate(pts):
        F.append(bm.verts.new((FONDO / 2, y, 0.0)))
        G.append(bm.verts.new((-FONDO / 2, y, 0.0)))
        xr = 0.0 if i in (0, len(pts) - 1) else R.uniform(-22.0, 22.0)   # cresta en zigzag: pliegues con luz y sombra
        Rr.append(bm.verts.new((xr, y, z)))
    caras = []
    for i in range(len(F) - 1):
        caras.append(bm.faces.new((F[i], F[i + 1], Rr[i + 1], Rr[i])))
        caras.append(bm.faces.new((G[i + 1], G[i], Rr[i], Rr[i + 1])))
        caras.append(bm.faces.new((F[i + 1], F[i], G[i], G[i + 1])))
    caras.append(bm.faces.new((F[0], Rr[0], G[0])))
    caras.append(bm.faces.new((F[-1], G[-1], Rr[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=caras)
    t._marcar(caras, "sierra")
    o = t.objeto(nombre + "_v%d" % v, bisel=0)
    from mathutils import Matrix
    o.data.transform(Matrix.Rotation(math.radians(90), 4, "Z"))
    return o
