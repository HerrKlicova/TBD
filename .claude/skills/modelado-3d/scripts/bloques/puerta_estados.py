"""Puerta de vehículos: 5 variantes × 4 estados (D-N163 §1, muestra de Álvaro 236586a, aprobada por Lucía el 8-nov).
Teo, 8-nov.

La puerta aprobada de la D-N111 (puerta_vehiculos.py: 8 m, dos hojas de 4 m, 2,1 m, jambas por fuera de los 8 m), con
los colores de la D-N163: chapa 94948c, malla b8b8b8, tablones a86c50, sacos de las jambas 605c48 (como el muro nuevo),
y el boquete, el escombro y la sombra en 48403c. Misma jerarquía y pivotes que la D-N111 (Hoja_O y Hoja_E con el origen
en su bisagra; ver puerta_vehiculos.py), así que el código de apertura no cambia.
Estados: sano; dañado (boquete en la hoja oeste y la hoja este descolgada: más baja por el centro); muy dañado (de la
hoja oeste queda la mitad de abajo y la este cuelga en diagonal de su bisagra de arriba); brecha (las hojas por el
suelo: solo quedan las bisagras en Hoja_O/Hoja_E, para que el código las siga encontrando).
Las jambas, el pórtico de la v4 y el alambre siguen en pie en todos los estados (la muestra). SIN VERIFICAR: de memoria.
TBD_VARIANTE = 4 · variante + estado (0..19).
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar
import puerta_vehiculos as PV

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 20
VARIANTE, ESTADO = VAR // 4, VAR % 4
NOMBRES_V = ["TuboChapa", "ChapaOndulada", "Tablones", "MallaEspino", "Blindada"]
NOMBRES_E = ["Sano", "Danado", "MuyDanado", "Brecha"]
PIEZAS = PV.PIEZAS + ["boquete"]
COLORES = dict(PV.COLORES, sacos="605c48", tablon="a86c50", malla="b8b8b8", boquete="48403c")
FACCION = "propio"
PRESUPUESTO = {0: (60, 3000), 1: (30, 1500)}
UMBRAL_CONTORNO = PV.UMBRAL_CONTORNO
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.5
BISAGRA, ALTO = PV.BISAGRA, PV.ALTO
PARTES = PV.PARTES
PIV = PV.PIV
GIRO_PRUEBA = PV.GIRO_PRUEBA
MATERIAL = {0: "chapa", 1: "chapa", 2: "tablon", 3: "malla", 4: "chapa"}


def _hoja_danada(m, s, a, fino):
    antes = set(m.bm.verts)
    PV._hoja(m, s, a, fino)
    vs = [w for w in m.bm.verts if w not in antes]
    if ESTADO == 1 and s > 0:            # descolgada: más baja por el lado del centro (trapecio de la muestra)
        for w in vs:
            t = (BISAGRA - abs(w.co.x)) / BISAGRA
            w.co.z = 0.04 + (w.co.z - 0.04) * (1 - 0.2 * t)
    if ESTADO == 1 and s < 0:            # boquete por fuera, a media altura
        m.caja((s * 2.2, -0.12, 1.0), (0.55, 0.04, 0.5), "boquete")
    if ESTADO == 2 and s < 0:            # queda la mitad de abajo, con el borde de arriba roto
        for w in vs:
            w.co.z = min(w.co.z, 0.8 + 0.12 * math.sin(w.co.x * 3.1))   # ≥ 0,68: por encima del nervio bajo de la blindada
    if ESTADO == 2 and s > 0:            # cuelga de la bisagra de arriba, en diagonal hasta el suelo
        for w in vs:
            w.co.z = max(w.co.z, 0.0)
        d = m.bm.verts
        piv_x, piv_z = s * BISAGRA, ALTO
        ang = math.atan2(ALTO - 0.15, BISAGRA - 0.4)
        for w in vs:                     # gira la hoja (plegada a una franja) sobre la bisagra de arriba
            x, z = w.co.x - piv_x, (w.co.z - 0.04) * 0.14 + piv_z - 0.3
            z -= piv_z
            c, sn = math.cos(ang), math.sin(ang)
            w.co.x, w.co.z = piv_x + x * c - z * sn * s, piv_z + x * sn * s + z * c
            w.co.y -= 0.15


def construir(nombre="SM_PuertaVehiculos", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    a = VARIANTE
    for s in (-1, 1):                    # jambas y remates, como la D-N111
        xc = s * 4.45
        if a in (0, 2):
            PV._jamba_sacos(m, xc, fino)
        elif a in (1, 3):
            PV._jamba_hesco(m, xc, fino)
        else:
            m.caja((xc, 0, 2.6), (0.9, 1.2, 5.2), "hormigon")
    if a == 3 and fino:
        for s in (-1, 1):
            m.cilindro((s * 4.45, 0, 2.3), 0.25, 0.9, "X", 8, "alambre")
    if a == 4:
        m.caja((0, 0, 4.9), (9.8, 0.6, 0.5), "hormigon")
        m.caja((0, -0.32, 4.9), (3.0, 0.04, 0.36), "placa")
    mat = MATERIAL[a]
    if ESTADO == 3:                      # brecha: trozos de las hojas por el suelo (en la parte fija)
        for k in range(7 if fino else 4):
            x = -3.4 + 1.1 * k + 0.3 * math.sin(k * 2.3)
            m.caja((x, -0.7 - 0.5 * math.cos(k * 1.7), 0.04 + 0.03 * (k % 2)), (1.0, 0.5 + 0.2 * (k % 3), 0.06),
                   mat if k % 3 else "marco", rot_z=k * 0.8)
        m.caja((0.4, -1.3, 0.03), (1.6, 0.9, 0.05), "boquete", rot_z=0.3)
    elif ESTADO == 2:                    # trozos caídos al pie
        for k in range(3):
            m.caja((-2.6 + 1.2 * k, -0.8, 0.04), (0.8, 0.45, 0.06), mat, rot_z=k * 1.1)
    for nombre_p, s in (("Hoja_O", -1), ("Hoja_E", 1)):
        m.parte(nombre_p)
        if ESTADO == 3:                  # solo las bisagras, arrancadas
            for z in (0.4, 1.7):
                m.caja((s * (BISAGRA - 0.08), 0, z), (0.18, 0.16, 0.3), "marco")
        else:
            _hoja_danada(m, s, a, fino)
    return m.objeto(nombre)


def separar(o, lod):
    return PV.separar(o, lod)
