"""Señal luminosa de vía junto a la puerta del recinto (ambientación de Sagunto, pieza 8). Teo, 6-nov-2026.

Fotos E8 de Paula (Commons): Padrón (señal alta en mástil con su armario a pie de vía), Casetas (Adif, entre vías
electrificadas) y una señal de Renfe de 1999 vista de frente (cabeza de tres focos con visera, disco auxiliar debajo
y placa identificadora «4N-II» en blanco con letras negras). Lo que se toma: mástil de perfil galvanizado, cabeza negra
alargada de tres focos con viseras, disco redondo debajo, placa blanca y armario gris al pie.
Los focos van apagados en 48403c (Lucía: 283440 es de la horda; nada de rojo ni ámbar). La cabeza negra va en 48403c, que no es de la horda.
MEDIDAS SIN VERIFICAR (típicas, estimadas sobre la foto de frente): mástil de 4,6 m, cabeza de 0,55 × 1,45 m, disco
de 0,7 m, placa de 0,7 × 0,3 m a 2,4 m, armario de 0,7 × 0,45 × 1,3 m. El mástil del LOD1 tiene 0,34 m (2 px).
Origen al pie del mástil; la señal mira a −Y (+Z en Unity), hacia el tren que llega. La vía pasaría a 2,2 m en +X.
Variantes: 0 la de Padrón (cabeza de tres focos y armario); 1 la de la foto de frente, con disco auxiliar y placa;
2 inclinada 14° con el armario abierto; 3 caída en el balasto; 4 sin cabeza, arrancada, con los cables colgando;
5 doble cabeza (de entrada) con escalerilla.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["mastil", "cabeza", "foco", "placa", "letra", "armario"]
COLORES = {"mastil": "6c7874", "cabeza": "48403c", "foco": "48403c", "placa": "d4d0cc", "letra": "48403c",
           "armario": "94948c"}
FACCION = "neutro"
PRESUPUESTO = {0: (20, 700), 1: (10, 300)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
H = 4.6


def construir(nombre="SM_SenalVia", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    v = VAR % 6
    g = 0.16 if lod == 0 else 0.34
    rx = {2: math.radians(14), 3: math.radians(88)}.get(v, 0.0)   # inclinación hacia +Y (cae de espaldas a la vía)
    def P(x, y, z):                                                  # gira un punto del mástil alrededor del pie
        return (x, y * math.cos(rx) + z * math.sin(rx), -y * math.sin(rx) + z * math.cos(rx) + (0.12 if v == 3 else 0))
    # el armario no se inclina
    t.caja((0.9, 0.3, 0.65), (0.7, 0.45, 1.3), "armario")
    if v == 2:
        t.caja((0.9 - 0.2, 0.3 - 0.45, 0.65), (0.04, 0.5, 1.2), "armario", rot_z=0.9)   # puerta abierta
        fino.caja((0.9, 0.3 - 0.2, 0.65), (0.6, 0.04, 1.1), "cabeza")                    # interior oscuro
    else:
        fino.caja((0.9, 0.3 - 0.235, 0.65), (0.5, 0.02, 1.0), "mastil")                  # puerta
    alto = H * 0.62 if v == 4 else H
    t.caja(P(0, 0, alto / 2), (g, g, alto), "mastil", rot_x=-rx)
    if v == 4:                                                       # arrancada: cables colgando
        fino.caja(P(0.05, -0.1, alto - 0.3), (0.03, 0.03, 0.6), "letra", rot_x=0.3)
        fino.caja(P(-0.05, -0.08, alto - 0.25), (0.03, 0.03, 0.5), "letra", rot_x=-0.4)
        t.caja((1.6, -0.6, 0.18), (0.55, 1.45, 0.36), "cabeza", rot_z=0.7)              # cabeza en el suelo
        return t.objeto(nombre, bisel=0)
    cabezas = (H - 0.8, H - 2.5) if v == 5 else (H - 0.8,)
    for zc in cabezas:
        _cabeza(t, fino, P, zc, rx)
    if v == 1:                                                       # disco auxiliar y placa
        t.cilindro(P(0, -0.15, H - 2.0), 0.35, 0.1, "Y", 12 if lod == 0 else 8, "cabeza")
        fino.cilindro(P(0, -0.21, H - 2.0), 0.16, 0.03, "Y", 8, "foco")
    if v in (0, 1, 2, 3):
        t.caja(P(0, -0.12, 2.4), (0.7, 0.05, 0.3), "placa", rot_x=-rx)
        for k, x in enumerate((-0.2, 0.0, 0.2)):
            fino.caja(P(x, -0.15, 2.4), (0.09, 0.02, 0.2), "letra", rot_x=-rx)
    if v == 5:                                                       # escalerilla
        for z in range(1, 4):
            fino.caja((0, 0.2, z * 0.8), (0.4, 0.04, 0.04), "mastil")
        t.caja((0.2, 0.2, 1.6), (0.04, 0.04, 3.2), "mastil")
        t.caja((-0.2, 0.2, 1.6), (0.04, 0.04, 3.2), "mastil")
    return t.objeto(nombre, bisel=0)


def _cabeza(t, fino, P, zc, rx):
    t.caja(P(0, -0.2, zc), (0.55, 0.22, 1.45), "cabeza", rot_x=-rx)
    for k in (-1, 0, 1):
        z = zc + k * 0.42
        fino.cilindro(P(0, -0.32, z), 0.13, 0.03, "Y", 10, "foco")
        fino.caja(P(0, -0.42, z + 0.15), (0.32, 0.2, 0.03), "cabeza", rot_x=-rx)      # visera
