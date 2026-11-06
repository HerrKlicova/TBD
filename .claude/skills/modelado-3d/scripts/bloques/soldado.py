"""Soldado de infantería del Ejército de Tierra (prototipo de la noche del 7-nov). Teo, 6-nov-2026, 23:30.

Encargo: Eugenio (6-nov 23:03: «los soldados bien modelados… con sus armas») y plan de Lucía (docs/plan-noche-7nov.md
§2): uniforme pixelado árido, casco COBAT-1, portaplacas, al menos 5 variantes y EL ARMA COMO PIEZA APARTE
(arma_infanteria.py). Referencia: docs/arte/referencias/infanteria-et.md (Paula); fotos de referencias-reales/militar/
cuando lleguen. Lo que no sale de foto va de memoria y está SIN VERIFICAR (chaleco, medidas).
Colores (Lucía): valor partido con casco y hombros claros y costados más oscuros; desde el repaso con las fotos
militares (23:29) el uniforme es boscoso liso (88885c / 70784c / 50541c) en vez de árido.
Revisión de Lucía (23:17): en 3/4 las caras del pantalón que miran a la cámara caían en el valor de la horda; el
uniforme lleva ahora los costados en 70784c (un escalón más) y el chaleco en 847c34, y las caras de abajo en 50541c.
El portaplacas va en 605c48 (coyote, aprobado) para que el chaleco se lea sobre el uniforme. Piel a86c50, botas
6c4038 (D-N80 de Bruno: ningún albedo propio bajo L* 28; el 543820 daba 26), mochila 847c34. Nada de valores de
la horda (ni en las caras de abajo).
Variantes del cuerpo (TBD_VARIANTE % 6):
  0 base con mochila de asalto;  1 casco con funda, gafas sobre el casco y rodilleras;  2 sin mochila y remangado
  (antebrazos en piel);  3 corpulento (+10 %) con mochila grande de munición (para el ametrallador);  4 braga en la
  cara y cinta oscura en el casco;  5 jefe de escuadra con radio PR4G a la espalda y antena larga.
Esqueleto propio (rig_humano.py, sin dependencias de licencia): raiz, cadera, pecho, cabeza, brazos, muslo_L/R,
pierna_L/R. Los dos brazos son UN solo hueso que gira en los hombros con el arma: a 12–24 px subir el arma se lee
igual y no hace falta IK. Clips: reposo, andar, disparar, morir.
EL ARMA: cada SM_Arma_* está modelada en el espacio del soldado en su pose de reposo (origen = pies del soldado).
En Unity se instancia como hija de la raíz del soldado con transform identidad, ANTES de reproducir animaciones, y se
cambia de padre al hueso «brazos» conservando la posición (SetParent(brazos, true)). El C90 va al hueso «pecho».
Las manos de todas las variantes están en el mismo sitio (MANOS), así que cualquier arma sirve en cualquier cuerpo.
Estatura 1,78 m. Delante = −Y en Blender (+Z en Unity). Pose de reposo: arma en alerta baja.
"""
import os
import bmesh
from mathutils import Vector, Matrix
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 6
PIEZAS = ["uniforme", "chaleco", "yelmo", "piel", "botas", "arma", "mochila", "equipo", "tubo"]
COLORES = {"uniforme": "88885c", "chaleco": "605c48", "yelmo": "88885c", "piel": "a86c50", "botas": "6c4038",
           "arma": "48403c", "mochila": "847c34", "equipo": "605c48", "tubo": "50541c"}
# claro arriba, costado, abajo (fijo: nunca 442c0c, de la horda)
# Repaso con las fotos militares de Paula (Lucía, 23:29: «el uniforme se queda en el boscoso, pero en colores lisos»;
# M1_soldado_1/_2, M2_casco_chaleco_1): boscoso en dos valores de verde, sin el arena del árido. Arriba 88885c (oliva
# claro), costados 70784c (los de la revisión de las 23:17, que no caen en la horda con las normales inclinadas) y
# abajo 50541c. El casco lleva funda boscosa: arriba 88885c y costados 50541c.
VALOR_PIEZAS = {"uniforme": ("88885c", "70784c", "50541c"), "yelmo": ("88885c", "50541c", "50541c"),
                "chaleco": ("b4a080", "847c34", "605c48")}
FACCION = "propio"
# Normales inclinadas hacia arriba (n + 1,0·Z, normalizada; se exportan en el FBX: Normals = Import). Con la luz del
# juego, casi cenital, las caras verticales caían a la mitad de luz y el pantalón y el torso cuantizaban en
# 1e220c/303424, los valores de la horda (Lucía, 23:17). Así los costados conservan su color de paleta y el volumen
# sigue leyéndose por el valor partido horneado (arriba claro, costados un escalón más oscuros).
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (150, 900), 1: (100, 500)}
UMBRAL_CONTORNO = {0: 0.2, 1: 9.0}
HUESOS = ["raiz", "cadera", "pecho", "cabeza", "brazos", "muslo_L", "pierna_L", "muslo_R", "pierna_R"]
HOMBRO = 1.40
MANOS = [(0.07, -0.28, 1.12), (-0.07, -0.47, 1.06)]      # derecha (empuñadura) e izquierda (guardamanos)


class Cuerpo:
    """Taller con hueso activo: cada cara nueva queda asignada al hueso en el atributo de cara «hueso»."""

    def __init__(self, piezas):
        self.t = Taller(piezas)
        self.bm = self.t.bm
        self.capa = self.bm.faces.layers.int.new("hueso")
        self.h = 0
        self.n = 0

    def hueso(self, nombre):
        self._sellar()
        self.h = HUESOS.index(nombre)

    def _sellar(self):
        """Las caras sin marcar (0) toman la parte activa + 1; así no importa el orden de las caras en el bmesh
        (Taller.cilindro borra y rehace caras y desplaza los índices). Al final objeto() resta 1."""
        for f in self.bm.faces:
            if f[self.capa] == 0:
                f[self.capa] = self.h + 1

    def barra(self, p0, p1, ancho, alto, pieza):
        """Caja orientada de p0 a p1 (sección ancho × alto)."""
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        vs = r["verts"]
        M = Matrix.Translation((p0 + p1) / 2) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4() @ \
            Matrix.Diagonal((ancho, alto, d.length, 1.0))
        bmesh.ops.transform(self.bm, matrix=M, verts=vs)
        self.t._marcar(list({f for v in vs for f in v.link_faces}), pieza)

    def caja(self, *a, **k):
        self.t.caja(*a, **k)

    def cilindro(self, *a, **k):
        self.t.cilindro(*a, **k)

    def objeto(self, nombre):
        self._sellar()
        for f in self.bm.faces:
            f[self.capa] -= 1
        return self.t.objeto(nombre, bisel=0)


def construir(nombre="SM_Soldado", lod=0):
    c = Cuerpo(PIEZAS)
    fino = lod == 0
    a = ASPECTO
    e = 1.1 if a == 3 else 1.0
    # ---------------- piernas
    for lado, s in (("L", 1), ("R", -1)):
        x = s * 0.1 * e
        c.hueso("pierna_" + lado)
        c.caja((x, -0.03, 0.06), (0.13, 0.27, 0.12), "botas")
        c.caja((x, 0.0, 0.31), (0.12 * e, 0.14, 0.38), "uniforme")
        if a == 1:
            c.caja((x, -0.085, 0.50), (0.12, 0.04, 0.10), "equipo")            # rodillera
        c.hueso("muslo_" + lado)
        c.caja((x, 0.0, 0.74), (0.14 * e, 0.17, 0.48), "uniforme")   # sube hasta dentro de la cadera: sin hueco al doblar
        if fino:
            c.caja((x + s * 0.08 * e, 0.0, 0.74), (0.03, 0.10, 0.14), "equipo")   # bolsillo de pernera
    # ---------------- cadera y torso
    c.hueso("cadera")
    c.caja((0, 0, 0.95), (0.32 * e, 0.19, 0.16), "uniforme")
    c.caja((0, 0, 1.02), (0.34 * e, 0.21, 0.05), "equipo")                     # cinturón
    if fino:
        c.caja((0.12 * e, 0.11, 0.98), (0.07, 0.04, 0.09), "equipo")           # cantimplora
    c.hueso("pecho")
    c.caja((0, 0, 1.23), (0.34 * e, 0.20, 0.40), "uniforme")
    c.caja((0, 0, 1.24), (0.38 * e, 0.26, 0.31), "chaleco")                    # portaplacas
    for k in (-1, 0, 1):                                                       # cargadores
        c.caja((k * 0.095 * e, -0.15, 1.15), (0.08, 0.05, 0.13), "equipo")
    if fino:
        c.caja((0.13 * e, -0.14, 1.33), (0.06, 0.04, 0.07), "equipo")          # radio personal
    c.caja((0, 0, 1.425), (0.40 * e, 0.22, 0.05), "uniforme")                  # hombros (cara de arriba clara)
    if a == 5:                                                                 # jefe: radio PR4G y antena
        c.caja((0.03, 0.19, 1.22), (0.22, 0.12, 0.28), "mochila")
        c.barra((0.11, 0.21, 1.34), (0.14, 0.27, 2.05), 0.03 if fino else 0.045, 0.03 if fino else 0.045, "arma")
    elif a == 3:
        c.caja((0, 0.2, 1.2), (0.32, 0.15, 0.36), "mochila")
        c.caja((0, 0.2, 1.4), (0.24, 0.12, 0.06), "mochila")
    elif a != 2:
        c.caja((0, 0.18, 1.24), (0.27, 0.11, 0.32), "mochila")
    # ---------------- cabeza y casco COBAT (cúpula baja, la silueta típica)
    c.hueso("cabeza")
    c.caja((0, -0.01, 1.50), (0.10, 0.10, 0.06), "piel")
    c.caja((0, -0.01, 1.60), (0.16, 0.19, 0.19), "piel")
    if a == 4 and fino:
        c.caja((0, -0.103, 1.56), (0.15, 0.02, 0.07), "uniforme")              # braga
    r = 0.145 * (1.05 if a == 1 else 1.0)
    c.cilindro((0, -0.005, 1.66), r, 0.10, "Z", 8, "yelmo", r2=0.115)
    c.cilindro((0, -0.005, 1.725), 0.115, 0.04, "Z", 8, "yelmo", r2=0.065)
    if a == 1 and fino:
        c.caja((0, -0.13, 1.69), (0.13, 0.03, 0.03), "arma")                   # gafas sobre el casco
    elif fino:
        c.caja((0, -0.15, 1.70), (0.05, 0.03, 0.05), "arma")                   # soporte de visión nocturna (M2_1)
    if a == 4:
        c.cilindro((0, -0.005, 1.645), r + 0.006, 0.025, "Z", 8, "arma")       # cinta del casco
    # ---------------- brazos (un hueso; el arma va aparte)
    c.hueso("brazos")
    hombros = [(0.21 * e, 0.0, HOMBRO), (-0.21 * e, 0.0, HOMBRO)]
    codos = [(0.24 * e, 0.0, 1.14), (-0.23 * e, -0.14, 1.15)]
    for h, co, m in zip(hombros, codos, MANOS):
        c.barra(h, co, 0.10 * e, 0.10, "uniforme")
        c.barra(co, m, 0.08 * e, 0.085, "piel" if a == 2 else "uniforme")
        c.caja(m, (0.075, 0.08, 0.075), "piel")
    arma = os.environ.get("TBD_ARMA")              # SOLO para las capturas: el arma de juego va aparte
    if arma is not None:
        from arma_infanteria import piezas_arma
        t = int(arma)
        if t == 4:
            c.hueso("pecho")
            piezas_arma(c, 4, fino)
            c.hueso("brazos")
            t = 0
        piezas_arma(c, t, fino)
    return c.objeto(nombre)


def rig(o, lod=0):
    from rig_humano import rig as _rig
    return _rig(o, HUESOS)
