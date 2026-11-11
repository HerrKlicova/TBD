"""Muro de la rampa del túnel de Castellón (fotos CS5 y CS8 de Paula: trinchera de hormigón con una banda de canaletas a
media altura, albardilla y valla de malla arriba). Kit del cerco de Castellón, después del MVP (aprobado por Lucía el
11-nov). Teo (4), 11-nov.

MÓDULO DE 10 m (x −5..5). El paramento mira a −Y (la vía) en y = 0 y el terreno queda detrás (+Y), a la altura de la
coronación (se monta al revés y se gira 180° al final). Origen en el pie del muro, en el centro del módulo. Alto 5 m en la trinchera (CS8) y 1,2 m al salir (CS5).
MEDIDAS A OJO DE LAS FOTOS (SIN VERIFICAR).
Colores: hormigón 88885c, juntas y metal 6c7874, albardilla y canaletas 94948c, malla b8b8b8, tierra b4a080, hiedra
70784c y 847c34, desconchón y escombro 48403c. Nada en la banda, nada rojo ni ámbar.
Variantes: v0 alto (trinchera de CS8); v1 bajo con valla (salida de CS5); v2 alto con hiedra colgando; v3 alto con
pasillo de servicio y barandilla; v4 escalonado (baja de 5 a 2,6 m a lo largo del módulo, el tramo de transición).
Estados: sano; dañado (desconchones y un tramo de valla caído); muy dañado (coronación rota en 3 m, escombro al pie y
tierra derramada); brecha (4 m hundidos hasta 0,8 m con una rampa de tierra que baja a la vía).
TBD_VARIANTE = 4 · variante + estado (0..19).
"""
import math, os
from montaje import Montaje
from restos import _h

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 20
VARIANTE, ESTADO = VAR // 4, VAR % 4
NOMBRES_V = ["Alto", "BajoValla", "Hiedra", "Pasillo", "Escalonado"]
NOMBRES_E = ["Sano", "Danado", "MuyDanado", "Brecha"]
PIEZAS = ["hormigon", "junta", "albardilla", "malla", "tierra", "hiedra", "hiedra_c", "roto"]
COLORES = {"hormigon": "88885c", "junta": "6c7874", "albardilla": "94948c", "malla": "b8b8b8", "tierra": "b4a080",
           "hiedra": "70784c", "hiedra_c": "847c34", "roto": "48403c"}
FACCION = "neutro"
PRESUPUESTO = {0: (20, 4000), 1: (10, 1400)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = ["tierra", "hiedra", "hiedra_c"]
NORMALES_INCLINACION = 1.0
LARGO, GRUESO, N = 10.0, 0.5, 10


def _alto_sano(k):
    if VARIANTE == 1:
        return 1.2
    if VARIANTE == 4:
        return 5.0 - 0.6 * (k // 2)
    return 5.0


def _alto(k):
    h = _alto_sano(k)
    if ESTADO == 2 and k in (6, 7, 8):
        return h - (0.9 + 0.4 * _h((k, 1, VARIANTE), 3))
    if ESTADO == 3 and k in (3, 4, 5, 6):
        return 0.8 if k in (4, 5) else max(0.8, h * 0.45)
    return h


def construir(nombre="SM_MuroRampa", lod=0):
    m = Montaje(PIEZAS, ["muro"])
    fino = lod == 0
    paso = LARGO / N
    altos = [_alto(k) for k in range(N)]
    for k in range(N):
        x = -LARGO / 2 + paso * (k + 0.5)
        h, hs = altos[k], _alto_sano(k)
        roto = h < hs - 0.05
        m.caja((x, -GRUESO / 2, h / 2), (paso - 0.004, GRUESO, h), "hormigon")
        m.caja((x, -GRUESO - 1.02, hs - 0.06 if not roto else h - 0.1), (paso - 0.004, 2.0, 0.1), "tierra")       # terreno de detrás
        if not roto:
            m.caja((x, -GRUESO / 2 + 0.05, h + 0.06), (paso - 0.004, GRUESO + 0.2, 0.12), "albardilla")
        else:                                            # borde roto, con el desconchón a la vista
            m.caja((x, 0.01, h - 0.15), (paso * 0.9, 0.02, 0.3), "roto")
        if hs >= 3 and h > 2.2:                          # banda de canaletas a media altura (CS8)
            m.caja((x, 0.12, 1.85), (paso - 0.004, 0.25, 0.32 if fino else 0.4), "albardilla")
            if fino:
                m.caja((x, 0.26, 1.78), (paso - 0.004, 0.03, 0.06), "junta")
        if fino and k % 3 == 0 and h > 1.0:              # juntas verticales del encofrado
            m.caja((x - paso / 2, 0.012, h / 2), (0.04, 0.025, h - 0.1), "junta")
    # valla de malla en la coronación
    for k in range(N):
        if ESTADO >= 1 and k in (6, 7):                  # tramo de valla caído
            if ESTADO == 1:
                x = -LARGO / 2 + paso * (k + 0.5)
                m.caja((x, -1.4, altos[k] + 0.08), (paso - 0.004, 1.6, 0.04), "malla", rot_x=0.15)
            continue
        if ESTADO == 3 and k in (3, 4, 5, 6):
            continue
        x0 = -LARGO / 2 + paso * k
        h = altos[k]
        alto_v = 1.8 if VARIANTE != 1 else 1.5
        if k % 2 == 0:
            m.caja((x0, -0.15, h + alto_v / 2), (0.06, 0.06, alto_v), "junta")
        hilos = (0.4, 1.0, alto_v - 0.05) if fino else (alto_v / 2,)
        for z in hilos:
            m.caja((x0 + paso / 2, -0.15, h + z), (paso - 0.004, 0.03 if fino else 0.06, 0.03 if fino else 0.5), "malla")
    if VARIANTE == 1:                                    # murete con bloques de remate (CS5)
        for k in range(N if fino else N // 2):
            pk = paso if fino else paso * 2
            m.caja((-LARGO / 2 + pk * (k + 0.5), 0.35, 0.25), (pk - 0.06, 0.3, 0.5), "hormigon")
    if VARIANTE == 2 and ESTADO < 3:                     # hiedra colgando desde arriba
        for k in range(7 if fino else 4):
            x = -4.4 + (8.8 / (6 if fino else 3)) * k
            kk = min(N - 1, int((x + 5) / paso))
            h = altos[kk]
            cuelga = min(h - 0.4, 1.2 + 2.2 * _h((k, 2, 0), 4))
            m.caja((x, 0.04 + 0.01 * (k % 2), h - cuelga / 2), (0.9 + 0.5 * _h((k, 3, 0), 5), 0.08, cuelga),
                   "hiedra" if k % 2 == 0 else "hiedra_c")
        for k in range(3 if fino else 2):                # matas encima
            m.caja((-3.5 + 3.5 * k, -0.9, altos[3 * k] + 0.25), (1.6, 1.2, 0.5), "hiedra_c", rot_z=k)
    if VARIANTE == 3:                                    # pasillo de servicio con barandilla
        m.caja((0, 0.45, 0.2), (LARGO, 0.9, 0.4), "albardilla")
        for k in range(6 if fino else 3):
            x = -5 + (10 / (5 if fino else 2)) * k
            m.caja((min(4.95, x), 0.85, 0.4 + 0.5), (0.05, 0.05, 1.0), "junta")
        m.caja((0, 0.85, 1.4), (LARGO, 0.06 if fino else 0.12, 0.06 if fino else 0.12), "junta")
        if fino:
            m.caja((-3.2, 0.2, 0.4 + 0.6), (0.4, 0.3, 1.2), "malla")               # armario de señales
    if ESTADO == 1:                                      # desconchones
        for k in range(3 if fino else 2):
            r = _h((k, 4, VARIANTE), 6)
            x = -4 + 8 * r
            kk = min(N - 1, int((x + 5) / paso))
            m.caja((x, 0.012, min(altos[kk] - 0.4, 0.8 + 2.5 * _h((k, 5, 0), 7))), (0.7, 0.025, 0.5), "roto")
    if ESTADO >= 2:                                      # escombro al pie y tierra derramada
        n = 5 if fino else 3
        for k in range(n):
            r = _h((k, 6, ESTADO), 8)
            m.caja((0.8 + 3.6 * r, 0.6 + 0.9 * _h((k, 7, 0), 9), 0.2), (0.7, 0.6, 0.4),
                   "hormigon" if k % 2 else "roto", rot_z=r * 3, rot_x=(r - .5) * .4)
    if ESTADO == 3:                                      # rampa de tierra de la brecha: del terreno de arriba a la vía
        top = _alto_sano(4)
        L = math.hypot(top, 4.5)
        ang = math.atan2(top, 4.5)
        m.caja((-0.5, 0.6, top / 2 - 0.1), (4.2, L, 0.5), "tierra", rot_x=-ang)
        for k in range(4 if fino else 2):
            m.caja((-2.0 + 1.2 * k, 2.6 + 0.4 * (k % 2), 0.2), (0.9, 0.7, 0.4), "roto", rot_z=k * 0.8)
    o = m.objeto(nombre)
    from mathutils import Matrix                         # se monta mirando a +Y y se gira: el paramento queda hacia −Y,
    o.data.transform(Matrix.Rotation(math.pi, 4, "Z"))   # que es la cara que ve la cámara del juego y la de la revisión
    o.data.update()
    return o


def separar(o, lod):
    from mathutils import Vector, Matrix
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    o.data.transform(Matrix.Translation(base))
    if "tbd_base" in o:
        del o["tbd_base"]
    return [o]
