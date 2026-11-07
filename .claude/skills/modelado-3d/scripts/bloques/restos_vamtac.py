"""Restos del VAMTAC ST5 caído en combate (coordinador, 7-nov 01:54). Teo, 7-nov.

Parte de vamtac_v2 (el bloque aprobado) y lo destroza con restos.py. Sin fotos de restos: SIN VERIFICAR.
Ruedas en y −2,2 y 1,7 (x ±0,98, radio 0,52), techo a 2,166 m, aro con la ametralladora encima; morro hacia −Y.
Variantes (TBD_VARIANTE % 5): 0 calcinado sobre las llantas (sin neumáticos); 1 volcado de lado; 2 reventado por una
mina (sin la rueda delantera izquierda, que queda tirada, y el morro hundido); 3 techo hundido con la ametralladora
caída al suelo; 4 volcado boca abajo.
Hijo «Humo»: su origen es el foco del incendio (el motor, bajo el capó, o la cabina en la 3).
"""
import os
import vamtac_v2 as V
from restos import Restos, COLORES_RESTOS, COLORES_LOD1_RESTOS, separar_restos, Rx, Ry, Rz, T

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = V.PIEZAS
COLORES = dict(V.COLORES, goma="48403c", optica="48403c", metal="48403c", foco="48403c", **COLORES_RESTOS)
VALOR_PIEZAS = {"casco": ("88885c", "70784c", "88885c")}   # costados en 70784c: la sombra fuera de la banda de la horda
COLORES_LOD1 = COLORES_LOD1_RESTOS     # 7-nov: sombras fuera de L* < 20
FACCION = "neutro"                      # restos: sin el marcador de unidad propia
PRESUPUESTO = {0: (600, 6000), 1: (300, 3000)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.25}
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
MOTOR = (0.0, -2.0, 1.25)
RUEDAS = [(x, y) for y in (-2.2, 1.7) for x in (-0.98, 0.98)]


def _rueda(c, x, y):
    return abs(c.x - x) < 0.35 and abs(c.y - y) < 0.6 and c.z < 1.1


def construir(nombre="SM_RestosVamtac", lod=0):
    fino = lod == 0
    o = V.construir(nombre, lod=lod)
    r = Restos(o)
    a = ASPECTO
    fuego = MOTOR
    arma = r.caras(lambda f, p, c: c.z > V.H - 0.02 and abs(c.y + 0.1) < 0.9)
    # neumáticos calcinados en todas las variantes (K8): quedan las llantas; la rueda que arranca la mina (v2) se salva
    r.borrar(r.caras(lambda f, p, c: p == "goma" and abs(c.x) > 0.82 and not (a == 2 and _rueda(c, 0.98, -2.2))))
    if a == 2:                                         # mina bajo la rueda delantera izquierda (+X)
        rueda = r.caras(lambda f, p, c: p in ("goma", "llanta") and _rueda(c, 0.98, -2.2))
        r.mover(rueda, T(2.6, -2.4, -0.18) @ Rz(30, (0.98, -2.2, 0.52)) @ Ry(80, (0.98, -2.2, 0.52)))
        r.hundir(0.9, 0.55, centro=(0.6, -2.4), radio=2.2)
        r.mover(r.todo(), Ry(-7, (-1.1, 0, 0)) @ Rx(3, (0, 2.5, 0)))
        fuego = (0.5, -2.1, 1.0)
    if a == 3:                                         # techo hundido y la ametralladora en el suelo
        r.mover(arma, T(-1.9, 0.6, -V.H + 0.25) @ Ry(85, (0, -0.1, V.H + 0.3)))
        r.hundir(1.35, 0.5)
        fuego = (0.0, -0.2, 1.2)
    r.abollar(0.06)
    r.humo(fuego)
    r.quemar(fuego, 3.2)
    if a == 1:                                         # volcado sobre el costado derecho (−X)
        r.mover(r.todo(), Ry(-92))
    if a == 4:                                         # boca abajo
        r.mover(r.todo(), Ry(178) @ Rz(12))
    r.apoyar()
    # r.mancha_suelo((0, -0.3), 4.0, 6.6, k=a)   # 7-nov: la quita Bruno; su huella de suelo (decal) hace ese papel
    r.escombros((0, -0.3), 4.0, 8 if fino else 4, k=a)
    return r.cerrar()


def separar(o, lod):
    return separar_restos(o, lod)
