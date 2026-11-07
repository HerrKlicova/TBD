"""Plataforma del helipuerto de Sagunto con balizas y punto de rearme (frentes de Sagunto, D-N96 y D-N97). Teo, 7-nov.

Para el NH90 y el Tigre, que despegan, vuelven, rearman y repostan aquí (D-N97).
Cotas (catalogo.md de Paula, «Cotas para el helipuerto»): NH90 con los rotores girando de 19,56 × 16,30 m (Airbus);
FATO de al menos 1,5 × 19,56 = 29,3 m (OACI); losa (TLOF) de unos 16,2 m, SIN VERIFICAR; la «H» y el círculo
también SIN VERIFICAR. Fotos H7–H9 de Paula (helipuerto, FARP y baliza): pendientes; la forma de las balizas, las
cajas y los bidones va de memoria hasta que lleguen y está SIN VERIFICAR.
Lo que se modela: losa cuadrada de 18 m (cubre la TLOF de 16,2 con margen), marcas de la zona de toma (círculo y «H»
en d4d0cc sobre hormigón 6c7874, oscuro para que la H se lea), borde de la FATO de 30 m marcado con balizas bajas, manga de viento y un punto de
rearme y repostaje con cajas de munición, bidones y depósito flexible.
Los focos de las balizas van apagados en 60546c y salen en el hijo «Luces» (transformación identidad) para que Marcos
les dé emisión de noche. Nada de rojo ni ámbar (la manga va en d4d0cc con bandas 48403c).
Origen en el centro de la H, en el suelo; la H se lee mirando a −Y (+Z en Unity).
Variantes (TBD_VARIANTE % 5): 0 losa de hormigón con H y círculo, 12 balizas y rearme pequeño; 1 de campaña: chapas
perforadas (PSP) en vez de losa, balizas solo en las esquinas y depósito flexible con bomba; 2 dañada: esquina rota,
cráter y cajas por el suelo; 3 fortificada: el rearme protegido con muros de gaviones y sacos; 4 de noche: 20 balizas,
torre de focos y tienda.
"""
import math, os
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = ["losa", "marca", "baliza", "foco", "caja", "bidon", "deposito", "palet", "manga", "mastil", "sacos",
          "gavion", "lona", "chapa", "crater"]
COLORES = {"losa": "6c7874", "marca": "d4d0cc", "baliza": "605c48", "foco": "60546c", "caja": "50541c",
           "bidon": "605c48", "deposito": "605c48", "palet": "6c4038", "manga": "d4d0cc", "mastil": "6c7874",
           "sacos": "847c34", "gavion": "847c34", "lona": "70784c", "chapa": "6c7874", "crater": "48403c"}
FACCION = "neutro"
PRESUPUESTO = {0: (100, 4000), 1: (50, 2000)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
PARTES = ["helipuerto", "Luces"]
PIV = {"Luces": (0.0, 0.0, 0.0)}
LOSA, FATO = 18.0, 30.0
REARME = (13.5, 4.0)          # punto de rearme, a un lado de la FATO


def _balizas(m, n, lado):
    """Posiciones de n balizas repartidas por el perímetro de un cuadrado de lado «lado»."""
    h = lado / 2
    per = 4 * lado
    pts = []
    for k in range(n):
        d = per * k / n
        e, r = int(d // lado), d % lado - h
        pts.append([(r, -h), (h, r), (-r, h), (-h, -r)][e])
    return pts


def _caja_municion(m, x, y, z, rot=0.0):
    m.caja((x, y, z + 0.175), (0.75, 0.4, 0.34), "caja", rot_z=rot)


def _bidon(m, x, y, tumbado=False):
    if tumbado:
        m.cilindro((x, y, 0.3), 0.3, 0.88, "X", 8, "bidon")
    else:
        m.cilindro((x, y, 0.44), 0.3, 0.88, "Z", 8, "bidon")


def construir(nombre="SM_Helipuerto", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    a = ASPECTO
    h = LOSA / 2
    # ---- losa (o chapas perforadas en la de campaña)
    if a == 1:
        n = 6
        for i in range(n):                                   # tiras de PSP con juntas de 4 cm
            y = -h + (i + 0.5) * LOSA / n
            m.caja((0, y, 0.03), (LOSA, LOSA / n - 0.04, 0.06), "chapa")
    elif a == 2:                                             # esquina rota: la losa en tres trozos
        m.caja((0, 2.0, 0.08), (LOSA, LOSA - 4.0, 0.16), "losa")
        m.caja((-2.5, -h + 2.0, 0.08), (LOSA - 5.0, 3.96, 0.16), "losa")
        m.caja((7.2, -7.4, 0.04), (2.6, 2.2, 0.08), "losa", rot_z=0.3)        # trozo hundido
        m.cilindro((5.0, 3.5, 0.17), 1.6, 0.02, "Z", 12, "crater")              # cráter
    else:
        m.caja((0, 0, 0.08), (LOSA, LOSA, 0.16), "losa")
    zt = 0.06 if a == 1 else 0.16
    # ---- marcas: círculo y H
    r, ancho = 6.0, 0.5
    seg = 24 if fino else 16
    for k in range(seg):
        a0, a1 = 2 * math.pi * k / seg, 2 * math.pi * (k + 0.85) / seg
        p0 = (r * math.cos(a0), r * math.sin(a0), zt + 0.01)
        p1 = (r * math.cos(a1), r * math.sin(a1), zt + 0.01)
        m.barra(p0, p1, ancho, 0.02, "marca")
    for x in (-1.1, 1.1):
        m.caja((x, 0, zt + 0.01), (0.6, 4.0, 0.02), "marca")
    m.caja((0, 0, zt + 0.01), (1.6, 0.6, 0.02), "marca")
    # ---- balizas del borde de la FATO
    nb = {0: 12, 1: 4, 2: 12, 3: 12, 4: 20}[a]
    pts = _balizas(m, nb, FATO) if a != 1 else [(-15, -15), (15, -15), (15, 15), (-15, 15)]
    if a == 2:
        pts = pts[:-3]                                       # balizas arrancadas
    for x, y in pts:
        m.cilindro((x, y, 0.15), 0.12 if fino else 0.2, 0.3, "Z", 8, "baliza")
    # ---- manga de viento
    mx, my = -h - 4.0, h + 4.0
    m.cilindro((mx, my, 3.0), 0.06 if fino else 0.15, 6.0, "Z", 6, "mastil")
    for k in range(4):                                       # cono en bandas claras y oscuras
        y0, y1 = 0.3 + k * 0.6, 0.3 + (k + 1) * 0.6 - 0.03
        r0, r1 = 0.4 - k * 0.07, 0.4 - (k + 1) * 0.07
        m.cilindro((mx + 0.0, my - (y0 + y1) / 2, 5.6), r0, y1 - y0, "Y", 8, "manga" if k % 2 == 0 else "crater",
                   r2=r1)
    # ---- punto de rearme y repostaje
    rx, ry = REARME
    m.caja((rx, ry, 0.07), (1.2, 1.0, 0.14), "palet")
    for fila in range(3):
        for k in range(2):
            _caja_municion(m, rx - 0.2, ry - 0.22 + 0.45 * k, 0.14 + 0.36 * fila)
    m.caja((rx + 1.6, ry, 0.07), (1.2, 1.0, 0.14), "palet")
    for k in range(2):
        _caja_municion(m, rx + 1.6, ry - 0.22 + 0.45 * k, 0.14)
    for i, (dx, dy) in enumerate(((0.0, 2.0), (0.65, 2.0), (1.3, 2.0), (0.32, 2.6), (0.97, 2.6))):
        _bidon(m, rx + dx, ry + dy)
    if fino:
        _bidon(m, rx + 2.6, ry + 2.2, tumbado=True)
    if a in (1, 4):                                          # depósito flexible con bomba
        m.caja((rx + 1.0, ry - 4.5, 0.35), (4.0, 2.6, 0.7), "deposito")
        m.caja((rx + 1.0, ry - 4.5, 0.75), (3.4, 2.0, 0.12), "deposito")
        m.caja((rx - 1.6, ry - 4.5, 0.4), (0.8, 0.6, 0.8), "chapa")             # bomba
        if fino:
            m.barra((rx - 1.6, ry - 4.2, 0.2), (rx - 3.0, ry - 1.5, 0.08), 0.1, 0.1, "crater")   # manguera
    if a == 2:                                               # cajas por el suelo
        for x, y, rot in ((8.5, 2.0, 0.4), (9.4, -1.0, 1.1), (rx - 2.0, ry + 1.0, 0.7)):
            _caja_municion(m, x, y, 0.0, rot)
    if a == 3:                                               # gaviones y sacos alrededor del rearme
        for k in range(4):
            m.caja((rx - 1.5 + 1.05 * k, ry + 3.6, 0.5), (1.0, 1.0, 1.0), "gavion")
            m.caja((rx - 1.5 + 1.05 * k, ry - 1.6, 0.5), (1.0, 1.0, 1.0), "gavion")
        for k in range(5):
            m.caja((rx + 3.3, ry - 1.4 + 1.0 * k, 0.2), (0.45, 0.95, 0.38), "sacos")
            if k < 4:
                m.caja((rx + 3.3, ry - 0.9 + 1.0 * k, 0.6), (0.45, 0.95, 0.38), "sacos")
    if a == 4:                                               # torre de focos y tienda
        tx, ty = h + 3.0, -h - 3.0
        m.caja((tx, ty, 0.4), (1.6, 1.0, 0.8), "chapa")                             # remolque del grupo
        m.cilindro((tx, ty, 3.5), 0.08 if fino else 0.17, 6.0, "Z", 6, "mastil")
        m.caja((tx, ty, 6.6), (1.4, 0.2, 0.5), "baliza")                            # barra de focos
        m.caja((rx + 1.0, ry + 7.0, 1.1), (4.0, 3.0, 2.2), "lona")
        m.tejado((rx + 1.0, ry + 7.0, 2.2), 4.0, 3.0, 0.9, "lona", vuelo=0.1, aguas=2)
    # ---- focos de las balizas (pieza para la emisión)
    m.parte("Luces")
    for x, y in pts:
        m.cilindro((x, y, 0.36), 0.09 if fino else 0.17, 0.12, "Z", 8, "foco")
    if a == 4:
        tx, ty = h + 3.0, -h - 3.0
        for dx in (-0.45, 0.0, 0.45):
            m.caja((tx + dx, ty - 0.12, 6.6), (0.35, 0.04, 0.35), "foco")
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector, Matrix
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    o.data.transform(Matrix.Translation(base))          # origen en el centro de la H (no en el de la caja envolvente)
    for n in objs[1:]:
        n.location = n.location + base
    return objs
