"""Camión cisterna de repostaje para la zona de rearme de Bétera, junto a P1–P3 (Lucía, 8-nov 18:21: «que el jugador vea
que el NH90 está repostando mientras rearma»). Teo (3), 8-nov.

Referencia: foto BT3_cisterna_aviacion_1 de Paula (camión cisterna militar FBW de repostaje de aviones, verde oliva):
cabina avanzada de frente plano con parabrisas grande, cisterna cilíndrica detrás con pasarela y barandilla arriba,
armario de la bomba entre la cabina y la cisterna, cajones bajo la cisterna y armario trasero con el carrete de la
manguera. La placa naranja de mercancías peligrosas NO va (nada ámbar): queda una placa lisa b8b8b8.
Medidas SIN VERIFICAR (estimadas de la foto): 8,0 × 2,45 m, cabina de 3,0 m de alto, cisterna de 2,0 m de diámetro y
unos 5,6 m de largo, dos ejes con ruedas de 1,0 m.
Ejes: largo a lo largo de Y con el frente hacia −Y (como el VAMTAC), origen en el suelo en el centro.
Colores: casco 50541c con el techo en 88885c (valor partido, sin nada por debajo de L* 28 en grande), lunas 6c7874,
neumáticos 48403c (605c48 en el LOD1), manguera 605c48, sacos 847c34, hollín 605c48. Nada rojo ni ámbar.
Variantes (TBD_VARIANTE % 5): 0 Aparcado (en espera); 1 Repostando (la manguera desplegada 7 m hacia +X, hasta el
helicóptero, con la boquilla y un extintor de carro); 2 Protegido (pretil de sacos terreros por el lado de +X y la
trasera); 3 Averiado (la cabina basculada hacia delante, una rueda fuera y calzos); 4 Quemado (todo en hollín, la
cisterna reventada por arriba y las lunas fuera).
"""
import math, os
from taller import Taller

V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Aparcado", "Repostando", "Protegido", "Averiado", "Quemado"]
PIEZAS = ["casco", "cisterna", "luna", "goma", "llanta", "metal", "manguera", "placa", "sacos", "hollin"]
COLORES = {"casco": "50541c", "cisterna": "50541c", "luna": "6c7874", "goma": "48403c", "llanta": "70784c",
           "metal": "605c48", "manguera": "605c48", "placa": "b8b8b8", "sacos": "847c34", "hollin": "605c48"}
COLORES_LOD1 = {"goma": "605c48"}
VALOR_PIEZAS = {"casco": ("88885c", "50541c", "50541c"), "cisterna": ("88885c", "50541c", "50541c")}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (60, 4000), 1: (30, 1200)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
L, A = 8.0, 2.45
YF = -L / 2


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_CamionCisterna", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    v = V
    q = v == 4
    C = (lambda p: "hollin") if q else (lambda p: p)
    g = 0.06 if fino else 0.34
    # chasis y ruedas
    t.caja((0, 0.2, 0.75), (1.0, L - 0.6, 0.3), C("metal"))
    ejes = (YF + 1.3, 2.4)
    for i, y in enumerate(ejes):
        for s in (-1, 1):
            if v == 3 and i == 0 and s > 0:
                continue                              # la rueda delantera derecha, fuera
            t.cilindro((s * 0.95, y, 0.5), 0.5, 0.4, "X", 14 if fino else 8, C("goma"))
            if fino:
                t.cilindro((s * 1.16, y, 0.5), 0.28, 0.04, "X", 10, C("llanta"))
    if v == 3:
        t.cilindro((2.0, YF + 0.6, 0.2), 0.5, 0.4, "Z", 14 if fino else 8, "goma")       # la rueda, tumbada al lado
        t.caja((0.95, YF + 1.3, 0.4), (0.3, 0.3, 0.8), "metal")                          # gato
        for y in (2.4 - 0.6, 2.4 + 0.6):
            t.caja((1.0, y, 0.12), (0.3, 0.25, 0.24), "sacos")                           # calzos
    # cabina avanzada (en la v3 basculada hacia delante sobre el eje delantero)
    zc0, hc, fc = 0.95, 2.05, 1.9
    yc = YF + fc / 2
    if v == 3:
        a = 0.75
        piv_y, piv_z = YF, zc0
        def P(y, z):
            dy, dz = y - piv_y, z - piv_z
            return piv_y + dy * math.cos(a) - dz * math.sin(a), piv_z + dy * math.sin(a) + dz * math.cos(a)
        ry = -a
    else:
        P = lambda y, z: (y, z)
        ry = 0.0
    def cab(c, s, pieza):
        y, z = P(c[1], c[2])
        t.caja((c[0], y, z), s, pieza, rot_x=ry)
    cab((0, yc, zc0 + hc / 2), (A, fc, hc), C("casco"))
    cab((0, YF + 0.95, zc0 + hc + 0.05), (A - 0.1, fc - 0.2, 0.12), C("casco"))
    if not q:
        cab((0, YF - 0.01, zc0 + 1.45), (A - 0.4, 0.04, 0.9), "luna")                   # parabrisas
        for s in (-1, 1):
            cab((s * (A / 2 + 0.01), YF + 0.7, zc0 + 1.4), (0.04, 0.8, 0.75), "luna")   # ventanilla de la puerta
        cab((0.35, YF - 0.03, zc0 + 0.35), (0.6, 0.04, 0.35), "placa")                   # placa (sin el naranja)
    else:
        cab((0, YF - 0.01, zc0 + 1.45), (A - 0.4, 0.04, 0.9), "metal")
    cab((0, YF - 0.15, 0.75), (A, 0.3, 0.35), C("metal"))                                # parachoques
    if fino and not q:
        for s in (-1, 1):
            cab((s * (A / 2 + 0.15), YF + 0.25, zc0 + 1.5), (0.06, 0.12, 0.35), "metal")  # retrovisores
    # armario de la bomba
    ya = YF + fc + 0.45
    t.caja((0, ya, 1.0 + 1.0), (A - 0.1, 0.8, 2.0), C("casco"))
    # cisterna
    y0, y1 = ya + 0.45, L / 2 - 0.55
    lc = y1 - y0
    ym = (y0 + y1) / 2
    t.cilindro((0, ym, 2.1), 1.0, lc, "Y", 18 if fino else 10, C("cisterna"))
    t.caja((0, ym, 1.0), (A, lc, 0.3), C("casco"))                                       # bastidor bajo la cisterna
    for s in (-1, 1):                                                                    # cajones laterales
        t.caja((s * (A / 2 - 0.15), ym - 0.6, 1.0 - 0.25), (0.3, lc - 2.0, 0.5), C("casco"))
    if fino:
        for s in (-1, 1):                                                                # barandilla arriba
            t.caja((s * 0.45, ym, 3.38), (g, lc - 0.4, g), C("metal"))
            for k in range(4):
                t.caja((s * 0.45, y0 + 0.4 + k * (lc - 0.8) / 3, 3.2), (g, g, 0.36), C("metal"))
        t.caja((0, ym, 3.1), (0.6, lc - 0.4, 0.06), C("metal"))                          # pasarela
        t.cilindro((0, ym - 1.0, 3.12), 0.3, 0.12, "Z", 10, C("metal"))                  # boca de carga
    else:
        t.caja((0, ym, 3.15), (1.0, lc - 0.4, 0.34), C("metal"))
    if q:                                                                                # reventada por arriba
        t.caja((0, ym + 0.5, 3.12), (1.2, 1.6, 0.1), "metal", rot_z=0.3, rot_x=0.4)
    # armario trasero con el carrete
    yt = L / 2 - 0.3
    t.caja((0, yt, 1.0 + 0.95), (A - 0.1, 0.6, 1.9), C("casco"))
    if fino:
        t.cilindro((0.6, yt + 0.32, 1.6), 0.45, 0.06, "Y", 12, C("manguera"))           # carrete visto por detrás
    if v == 1:                                                                           # manguera desplegada a +X
        pts = [(0.9, yt + 0.4), (2.0, yt + 0.6), (4.0, yt), (6.0, 2.2), (7.6, 1.2)]
        for (xa, ya_), (xb, yb) in zip(pts, pts[1:]):
            dx, dy = xb - xa, yb - ya_
            t.caja(((xa + xb) / 2, (ya_ + yb) / 2, 0.06), (math.hypot(dx, dy) + 0.1, 0.1 if fino else 0.34,
                   0.1 if fino else 0.12), "manguera", rot_z=math.atan2(dy, dx))
        t.caja((0.9, yt + 0.4, 0.8), (0.1 if fino else 0.34, 0.1 if fino else 0.34, 1.5), "manguera")
        t.caja((7.8, 1.1, 0.12), (0.4, 0.2, 0.2), "metal", rot_z=-0.5)                   # boquilla
        t.caja((3.0, -1.8, 0.45), (0.5, 0.5, 0.9), "metal")                              # extintor de carro
        if fino:
            t.cilindro((3.0, -1.8, 0.95), 0.18, 0.5, "Z", 8, "placa")
    if v == 2:                                                                           # pretil de sacos
        for k in range(8):
            for f in range(2):
                t.caja((A / 2 + 1.0, -3.2 + k * 0.9 + f * 0.45, 0.13 + f * 0.26), (0.5, 0.72, 0.26), "sacos",
                       rot_z=(_h(k, f) - 0.5) * 0.2)
        for k in range(3):
            for f in range(2):
                t.caja((-0.9 + k * 0.9 + f * 0.45, L / 2 + 1.0, 0.13 + f * 0.26), (0.72, 0.5, 0.26), "sacos",
                       rot_z=(_h(k, f + 3) - 0.5) * 0.2)
    if q:
        for k in range(4):                                                               # chapas y restos
            t.caja((-1.8 - _h(k) * 1.2, -2 + k * 1.5, 0.05), (0.7, 0.5, 0.06), "hollin", rot_z=_h(k, 1) * 3)
    return t.objeto(nombre, bisel=0)
