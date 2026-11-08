"""Nave modular del polígono de Nules (Álvaro, 8-nov, nules-nivel.md §6.5: naves de 60 × 28 m en X 4–64 y de 58 × 28 m
en X 192–250, Z 2–29, yaw 0; persianas de muelle hacia la calle del polígono). Teo (3), 8-nov.

Nave industrial típica de polígono valenciano (sin foto; tipo genérico, medidas SIN VERIFICAR): muros de bloque de
hormigón de 7 m, pórticos cada 10 m y cubierta de chapa clara a dos aguas con la cumbrera a lo largo de la nave, a
8,5 m. Se monta con MÓDULOS de 10 m (9,98 con holgura) y dos TESTEROS. Cada módulo lleva en el frente (−Y, la calle)
una persiana de muelle de 5 × 5 m con su visera y una puerta de paso; detrás (+Y), muro ciego con ventanas altas.
Colores: muro 94948c con zócalo 6c7874, cubierta b4a080 con nervios 88885c (arriba claro), persiana 6c7874 con lamas
605c48, visera 6c7874, interior y hollín 605c48, óxido 7c3c18, palés y cajas 845c28, cristal 6c7874. Nada rojo ni ámbar,
nada en la banda de la horda.
Origen: módulo, en el centro de su planta (10 × 28 m), en el suelo; testero, en el centro del muro (0,3 × 28 m), con
la cara de fuera en −X (con giro 180 cierra el otro extremo). Frente a la calle en −Y (= +Z de Unity con yaw 0).
TBD_VARIANTE = 5 · tipo + estado (0..9). Tipos: 0 módulo, 1 testero. Estados (los de Álvaro): 0 intacta; 1 con la
persiana abierta; 2 con la cubierta hundida; 3 quemada; 4 saqueada.
Montaje: nave de 60 m = 6 módulos en x = −25, −15, …, 25 y testeros en x = ∓30,15 (el de +X con giro 180).
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 10
TIPO, ESTADO = VAR // 5, VAR % 5
NOMBRES_T = ["Modulo", "Testero"]
NOMBRES_E = ["Intacta", "PersianaAbierta", "CubiertaHundida", "Quemada", "Saqueada"]
PIEZAS = ["muro", "zocalo", "cubierta", "nervio", "persiana", "lama", "visera", "interior", "oxido", "carga", "cristal"]
COLORES = {"muro": "94948c", "zocalo": "6c7874", "cubierta": "b4a080", "nervio": "88885c", "persiana": "6c7874",
           "lama": "605c48", "visera": "6c7874", "interior": "605c48", "oxido": "7c3c18", "carga": "845c28",
           "cristal": "6c7874"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.6
PRESUPUESTO = {0: (20, 2000), 1: (10, 900)}         # el testero es casi un muro: pocos triángulos
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
L, F, HM, HC, E = 9.98, 28.0, 7.0, 8.5, 0.3


def _zc(y):
    return HM + (HC - HM) * (1 - abs(y) / (F / 2))


def construir(nombre="SM_NavePoligono", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    if TIPO == 1:
        _testero(t, fino)
    else:
        _modulo(t, fino)
    return t.objeto(nombre, bisel=0)


def _modulo(t, fino):
    e = ESTADO
    g = 0.08 if fino else 0.34
    a = math.atan2(HC - HM, F / 2)
    # muros largos (frente y fondo) y zócalo
    for s in (-1, 1):
        y = s * (F / 2 - E / 2)
        t.caja((0, y, HM / 2), (L, E, HM), "muro")
        t.caja((0, s * (F / 2 + 0.02), 0.5), (L, 0.06, 1.0), "zocalo")
    # cubierta a dos aguas (en la 3 no hay: quedan los pórticos; en la 2, hundida por la mitad de detrás)
    if e != 3:
        for s in (-1, 1):
            if e == 2 and s > 0:
                continue
            yc = s * F / 4
            t.caja((0, yc, (HM + HC) / 2 + 0.1), (L, F / 2 / math.cos(a) + 0.3, 0.12), "cubierta", rot_x=-s * a)
            for k in range(5 if fino else 2):
                x = -L / 2 + (k + 0.5) * L / (5 if fino else 2)
                t.caja((x, yc, (HM + HC) / 2 + 0.18), (0.15 if fino else 0.34, F / 2 / math.cos(a) + 0.3, 0.04), "nervio",
                       rot_x=-s * a)
        if e == 2:                                   # faldón de detrás caído dentro de la nave
            t.caja((0, F / 4, 3.4), (L - 0.6, F / 2 - 1.0, 0.12), "cubierta", rot_x=math.radians(18))
            t.caja((0, F / 4, HM - 0.02), (L - 0.4, F / 2 - 0.6, 0.06), "interior")       # se ve el interior oscuro
    # pórtico en el borde del módulo (+X): pilares y dintel quebrado (en la 3, a la vista y oxidado)
    xp = L / 2 - 0.2
    pz = "oxido" if e == 3 else "zocalo"
    for s in (-1, 1):
        t.caja((xp, s * (F / 2 - 0.5), HM / 2), (0.35, 0.35, HM), pz)
        yc = s * F / 4
        t.caja((xp, yc, _zc(yc) - 0.3), (0.3, F / 2 / math.cos(a) - 0.2, 0.4 if fino else 0.4), pz, rot_x=-s * a)
    if e == 3:                                       # quemada: sin cubierta, interior y hollín sobre los huecos
        t.caja((0, 0, 0.06), (L - 0.4, F - 1.0, 0.1), "interior")
        t.caja((0, -F / 2 - 0.03, 5.6), (6.5, 0.04, 2.6), "interior")
    # frente (−Y): persiana de muelle de 5 × 5 m con visera, y puerta de paso
    yf = -F / 2 - 0.04
    if e in (1, 3):                                  # abierta (o quemada sin persiana)
        t.caja((-1.0, yf, 2.5), (5.0, 0.04, 5.0), "interior")
        if e == 1:
            t.cilindro((-1.0, yf - 0.25, 5.3), 0.35, 5.2, "X", 8 if fino else 6, "persiana")      # rollo arriba
    elif e == 4:                                     # forzada: la persiana a medias y doblada
        t.caja((-1.0, yf, 1.0), (5.0, 0.04, 2.0), "interior")
        t.caja((-1.0, yf - 0.06, 3.5), (5.0, 0.06, 3.0), "persiana", rot_z=0.03, rot_x=0.08)
    else:
        t.caja((-1.0, yf, 2.5), (5.0, 0.06, 5.0), "persiana")
        if fino:
            for k in range(9):
                t.caja((-1.0, yf - 0.04, 0.5 + k * 0.5), (5.0, 0.03, 0.06), "lama")
        else:
            t.caja((-1.0, yf - 0.04, 2.5), (5.0, 0.03, 0.34), "lama")
    t.caja((-1.0, yf - 0.7, 5.6), (5.8, 1.4, 0.12), "visera")
    t.caja((3.3, yf, 1.1), (1.0, 0.05, 2.2), "interior" if e in (3, 4) else "persiana")      # puerta de paso
    # fondo (+Y): ventanas altas
    for x in ((-2.5, 2.5) if fino else (0.0,)):
        t.caja((x, F / 2 + 0.03, 5.6), (3.0 if fino else 5.0, 0.04, 0.8), "interior" if e == 3 else "cristal")
    if e == 4:                                       # saqueada: palés y cajas sacados a la calle
        for k, (x, y, h) in enumerate(((-3.2, -2.0, 0.8), (-0.6, -2.8, 0.5), (1.8, -1.6, 1.0), (0.4, -4.0, 0.3))):
            t.caja((x, yf + y, h / 2), (1.2, 1.0, h), "carga", rot_z=0.4 * k)


def _testero(t, fino):
    e = ESTADO
    x0 = 0.0
    # muro de testero con su piñón, como un perfil extruido (en la 2, el piñón roto: le falta la mitad de +Y)
    if e == 2:
        P = [(-F / 2, 0.0), (-F / 2, HM), (-4.0, HM + 0.9), (F / 2, HM - 0.8), (F / 2, 0.0)]
    else:
        P = [(-F / 2, 0.0), (-F / 2, HM), (0.0, HC), (F / 2, HM), (F / 2, 0.0)]
    t.perfil_extruido(P, lambda z: E / 2, pieza="muro", desde_x=x0)
    t.caja((x0 - E / 2 - 0.02, 0, 0.5), (0.06, F, 1.0), "zocalo")
    # puerta de paso y ventana alta en la cara de fuera
    xf = x0 - E / 2 - 0.03
    t.caja((xf, -8.0, 1.1), (0.04, 1.0, 2.2), "interior" if e in (1, 3, 4) else "persiana")
    t.caja((xf, 3.0, 5.4), (0.04, 6.0 if fino else 6.0, 0.8), "interior" if e == 3 else "cristal")
    if e == 3:                                       # hollín sobre la ventana
        t.caja((xf - 0.01, 3.0, 6.5), (0.04, 5.0, 1.0), "interior")
    if e == 4:
        for k, (y, h) in enumerate(((-6.5, 0.8), (-9.5, 0.5))):
            t.caja((xf - 1.5, y, h / 2), (1.2, 1.0, h), "carga", rot_z=0.5 * k)
