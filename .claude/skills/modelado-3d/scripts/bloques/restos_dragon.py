"""Restos del VCR 8x8 Dragón caído en combate (coordinador, 7-nov 01:54). Teo, 7-nov.

Parte de dragon_v5 (el bloque aprobado) y lo destroza con restos.py. Sin fotos de restos: SIN VERIFICAR.
Casco de 8 × 3 m hasta 2,34 m, ruedas en y −2,44 / −1,01 / 0,52 / 2,08 (x ±1,22, radio ~0,68), torre RCWS sobre
(0,25, 0,12) con el cañón hacia −Y; morro hacia −Y.
Variantes (TBD_VARIANTE % 5): 0 calcinado sobre las llantas; 1 torre arrancada y caída al lado; 2 volcado de lado;
3 hundido sobre el costado izquierdo (ruedas reventadas) con el cañón doblado; 4 reventado por una mina bajo el morro
(sin las dos ruedas delanteras de la derecha y con el morro hundido).
Hijo «Humo»: su origen es el foco del incendio (el compartimento del motor, delante a la derecha, o el hueco de la torre).
"""
import os
import dragon_v5 as D
from restos import Restos, COLORES_RESTOS, COLORES_LOD1_RESTOS, separar_restos, Rx, Ry, Rz, T

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = D.PIEZAS
COLORES = dict(D.COLORES, goma="48403c", optica="48403c", metal="48403c", foco="48403c", visor="94948c",
               **COLORES_RESTOS)
VALOR_PIEZAS = {"casco": ("88885c", "70784c", "88885c"), "torre": ("88885c", "70784c", "88885c")}
COLORES_LOD1 = COLORES_LOD1_RESTOS     # 7-nov: sombras fuera de L* < 20
FACCION = "neutro"
PRESUPUESTO = {0: (900, 8000), 1: (400, 4000)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.25}
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
TZ = D.HULL_Z
MOTOR = (-0.7, -2.6, 1.7)               # motor delante a la derecha (−X), junto al conductor (SIN VERIFICAR)
TORRE = (0.25, 0.12, TZ)


def construir(nombre="SM_RestosDragon", lod=0):
    fino = lod == 0
    o = D.construir(nombre, lod=lod)
    r = Restos(o)
    a = ASPECTO
    fuego = MOTOR
    torre = r.caras(lambda f, p, c: c.z > TZ + 0.03 and abs(c.x - 0.25) < 1.0 and -3.6 < c.y < 1.15)
    canon = r.caras(lambda f, p, c: p == "metal" and c.z > TZ + 0.3 and c.y < -0.8 and abs(c.x - 0.25) < 0.3)
    r.borrar(r.caras(lambda f, p, c: p == "goma" and abs(c.x) > 1.0))   # neumáticos calcinados en todas (K8)
    if a == 1:                                         # torre arrancada: cae al suelo a la izquierda (+X), volcada
        r.mover(r.islas(torre), T(3.2, 0.8, -TZ + 0.05) @ Rz(70, TORRE) @ Ry(-110, (1.1, 0.12, TZ)))
        r.caja((0.25, 0.12, TZ + 0.04), (1.3, 1.4, 0.04), "hollin")          # hueco de la torre (placa oscura)
        fuego = (0.25, 0.12, TZ + 0.1)
    if a == 2:
        r.mover(r.todo(), Ry(-95))                     # volcado sobre el costado derecho
    if a == 3:                                         # ruedas izquierdas reventadas y el cañón doblado hacia abajo
        r.borrar(r.caras(lambda f, p, c: p == "goma" and c.x > 1.0))
        r.mover(canon, Rx(-14, (0.25, -1.0, TZ + 0.52)))
        r.mover(r.todo(), Ry(9))
        fuego = (0.25, -1.0, TZ + 0.2)
    if a == 4:                                         # mina bajo el morro derecho
        r.borrar(r.caras(lambda f, p, c: p in ("goma", "llanta") and c.x < -1.0 and c.y < -0.3))
        r.hundir(1.6, 0.6, centro=(-1.0, -3.5), radio=2.5)
        r.mover(r.todo(), Rx(-5, (0, 3.5, 0)) @ Ry(-4))
        fuego = (-0.8, -3.0, 1.5)
    r.abollar(0.07)
    r.humo(fuego)
    r.quemar(fuego, 4.2)
    r.apoyar()
    r.mancha_suelo((0, -0.3), 5.0, 8.6, k=a)
    r.escombros((0, -0.3), 5.0, 10 if fino else 5, k=a + 10)
    return r.cerrar()


def separar(o, lod):
    return separar_restos(o, lod)
