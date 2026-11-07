"""Restos del NH90 derribado (coordinador, 7-nov 01:54). Teo, 7-nov.

Parte de nh90 (el bloque aprobado) y lo destroza con restos.py. Sin fotos de restos: SIN VERIFICAR (de memoria:
helicópteros derribados que caen con poca velocidad, con el tren aplastado y las palas dobladas o partidas).
Rotor principal en (0, −1, 4,6) con 4 palas de 8,15 m, rotor de cola en (0,42, 7,75, 4,55), tren de morro en y −5,6 y
principal en y 0,6 (x ±1,75); morro hacia −Y. El disco del rotor en marcha no va en los restos.
Variantes (TBD_VARIANTE % 5): 0 caída dura sobre la panza (tren aplastado, palas dobladas hacia abajo); 1 tumbado
sobre el costado derecho con dos palas partidas; 2 cono de cola quebrado, doblado hacia un lado y caído; 3 morro hundido y cabeceado
hacia delante, con una pala partida; 4 calcinado: techo hundido, sin palas en el cubo y dos tiradas en el suelo.
Hijo «Humo»: su origen es el foco del incendio (la joroba de los motores, o la cabina en la 3 y la 4).
"""
import math, os
from mathutils import Vector
import nh90 as N
from restos import Restos, COLORES_RESTOS, separar_restos, Rx, Ry, Rz, T

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = N.PIEZAS
COLORES = dict(N.COLORES, optica="48403c", interior="48403c", goma="48403c", **COLORES_RESTOS)
VALOR_PIEZAS = {"casco": ("88885c", "70784c", "50541c")}
FACCION = "neutro"
PRESUPUESTO = {0: (600, 6000), 1: (300, 3000)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.25}
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
HUB = Vector((0.0, -1.0, 4.6))
MOTORES = (0.0, -0.6, 3.5)


def _palas(r):
    """Palas del rotor principal: cada pala es una isla (una barra); el cubo, cerca del eje, no cuenta. Ordenadas
    por el ángulo en planta."""
    libres = {f for f in r.bm.faces if r.parte0.get(f) == 1}
    palas = []
    while libres:
        isla = set(r.islas([next(iter(libres))]))
        libres -= isla
        c = sum((f.calc_center_median() for f in isla), Vector()) / len(isla)
        d = Vector((c.x - HUB.x, c.y - HUB.y))
        if d.length > 1.5:
            palas.append((math.atan2(d.y, d.x), list(isla)))
    palas.sort(key=lambda t: t[0])
    return {k: cs for k, (_, cs) in enumerate(palas)}


def _doblar(r, caras, k):
    """Pala doblada hacia abajo: cae más cuanto más lejos del cubo."""
    for v in {v for f in caras for v in f.verts}:
        d = Vector((v.co.x - HUB.x, v.co.y - HUB.y)).length
        if d > 1.0:
            v.co.z -= k * (d - 1.0) ** 1.4


def _partir(r, caras, f=0.35):
    """Pala partida: se queda en un trozo de f de su largo (escalada hacia el cubo; sigue cerrada)."""
    for v in {v for g in caras for v in g.verts}:
        d = Vector((v.co.x - HUB.x, v.co.y - HUB.y))
        v.co.x, v.co.y = HUB.x + d.x * f, HUB.y + d.y * f


def construir(nombre="SM_RestosNH90", lod=0):
    fino = lod == 0
    o = N.construir(nombre, lod=lod)
    r = Restos(o)
    a = ASPECTO
    fuego = MOTORES
    r.borrar(r.caras(lambda f, p, c: p == "disco"))
    palas = _palas(r)
    tren = r.islas(r.caras(lambda f, p, c: p == "goma"))
    if a != 1:
        r.borrar(tren)                                 # tren aplastado: queda sobre la panza
    if a == 0:
        for k, cs in palas.items():
            _doblar(r, cs, 0.09 + 0.03 * k)
    if a == 1:
        for k in (0, 2):
            _partir(r, palas[k], 0.3)
        _doblar(r, palas[1] + palas[3], 0.05)
        r.mover(r.todo(), Ry(-82))
    if a == 2:                                         # cola quebrada por detrás de la cabina, doblada y caída
        # el fuselaje es un solo loft de morro a cola: se dobla por los vértices de detrás de y 4,6 (sigue cerrado)
        M = Rz(32, (0, 4.6, 1.5)) @ Rx(-9, (0, 4.6, 1.5))
        for v in r.bm.verts:
            if v.co.y > 4.6:
                v.co = M @ v.co
        _partir(r, palas[1], 0.45)
        for k in (0, 2, 3):
            _doblar(r, palas[k], 0.08)
        fuego = (0.0, 3.6, 1.8)
    if a == 3:                                         # morro hundido y cabeceado hacia delante
        r.hundir(0.6, 0.6, centro=(0, -6.5), radio=2.5)
        for v in r.bm.verts:
            if v.co.y < -5.0:
                v.co.y = -5.0 + (v.co.y + 5.0) * 0.7
        _partir(r, palas[3], 0.4)
        for k in (0, 1, 2):
            _doblar(r, palas[k], 0.1)
        r.mover(r.todo(), Rx(-9, (0, -6, 0)))
        fuego = (0.0, -4.0, 1.8)
    if a == 4:                                         # calcinado
        for cs in palas.values():
            r.borrar(cs)
        r.hundir(2.1, 0.65)
        for i, (x, y, g) in enumerate(((4.2, -3.0, 0.4), (-3.8, 4.0, 2.2))):
            r.caja((x, y, 0.06), (0.55, 6.5, 0.1), "pala", rz=g)            # palas caídas
        fuego = (0.0, -2.5, 1.6)
    r.abollar(0.06)
    r.humo(fuego)
    r.quemar(fuego, 7.0 if a == 4 else 5.0)
    r.apoyar()
    r.mancha_suelo((0, 0.5), 6.0, 12.0, k=a)
    r.escombros((0, 0.5), 7.0, 12 if fino else 6, k=a + 20)
    return r.cerrar()


def separar(o, lod):
    return separar_restos(o, lod)
