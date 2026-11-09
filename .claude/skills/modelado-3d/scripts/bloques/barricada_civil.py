"""Barricadas civiles de Castelló (Lucía, 8-nov 18:00, D-N156: la estación sale infestada en el capítulo 3; tramos de
8 m, Álvaro 18:02). Lo que levantaron los vecinos con lo que tenían a mano antes de caer. Teo (3), 8-nov.

Tramo de 8 m a lo largo de X, FUERA = −Y (de donde vino la horda): lo que cae, cae hacia dentro (+Y). Origen en el
suelo, en el centro del tramo. Alto en pie ~1,6–2 m. Medidas SIN VERIFICAR (genéricas, sin foto).
Colores apagados, sin rojo: coches 6c7874, 847c34, 605c48 y 94948c con lunas 48403c; muebles y palés 845c28, sofá 605c48,
colchones 94948c, vallas de obra 6c7874 con bases 94948c, contenedor 50541c, neumáticos 48403c, escombro 88885c.
TBD_VARIANTE = 4 · variante + estado (0..19). Variantes: 0 Coches (dos coches cruzados y una valla); 1 Muebles
(armarios, sofá, colchones y somieres); 2 Vallas (vallas de obra, un contenedor de basura y palés); 3 Furgoneta (una
furgoneta cruzada con neumáticos); 4 Mixta (puertas, palés, un coche pequeño y sillas).
Estados (los aprobados del muro): 0 Sano (en pie), 1 Danado (empujada, con huecos), 2 MuyDanado (casi todo caído
hacia dentro), 3 Brecha (desparramada por el suelo: ya no cierra el paso). En Castellón salen sobre todo el 2 y el 3.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 20
VARIANTE, ESTADO = VAR // 4, VAR % 4
NOMBRES_V = ["Coches", "Muebles", "Vallas", "Furgoneta", "Mixta"]
NOMBRES_E = ["Sano", "Danado", "MuyDanado", "Brecha"]
PIEZAS = ["coche_a", "coche_b", "coche_c", "luna", "madera", "sofa", "colchon", "valla", "base", "contenedor",
          "neumatico", "escombro"]
COLORES = {"coche_a": "6c7874", "coche_b": "847c34", "coche_c": "605c48", "luna": "48403c", "madera": "845c28",
           "sofa": "605c48", "colchon": "94948c", "valla": "6c7874", "base": "94948c", "contenedor": "50541c",
           "neumatico": "48403c", "escombro": "88885c"}
# LOD1: lunas y neumáticos suben a 605c48 (en 48403c su cara en sombra caía a L* 12, en la banda de la horda)
COLORES_LOD1 = {"luna": "605c48", "neumatico": "605c48"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (12, 2500), 1: (8, 900)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


class _B:
    """Coloca cada objeto según el estado: empujado hacia dentro (+Y), girado, tumbado o quitado."""
    def __init__(self, t, fino):
        self.t, self.fino, self.k = t, fino, 0

    def mover(self, x, y, z, s, rz, alto):
        e = ESTADO
        self.k += 1
        k = self.k
        if e == 0:
            return (x, y, z), rz, 0.0, True
        if e == 1:
            dy = 0.4 + 0.8 * _h(k, 1)
            return (x, y + dy, z), rz + (_h(k, 2) - 0.5) * 0.5, (0.35 if _h(k, 3) < 0.3 else 0.0), _h(k, 4) > 0.12
        if e == 2:
            tumb = _h(k, 3) < 0.65
            return ((x, y + 1.0 + 1.4 * _h(k, 1), (min(s[1], s[2]) / 2 if tumb else z)),
                    rz + (_h(k, 2) - 0.5) * 1.2, (1.4 if tumb else 0.2), _h(k, 4) > 0.2)
        tumb = True
        return ((x * 1.25 + (_h(k, 5) - 0.5) * 1.5, y + 1.5 + 2.6 * _h(k, 1), min(s[1], s[2]) / 2),
                rz + (_h(k, 2) - 0.5) * 2.4, 1.5, _h(k, 4) > 0.3)

    def caja(self, c, s, pieza, rz=0.0):
        (x, y, z), r, tumb, vivo = self.mover(c[0], c[1], c[2], s, rz, s[2])
        if not vivo:
            return
        if tumb > 1.0:                               # tumbado: la altura pasa a ser el ancho
            self.t.caja((x, y, min(s[1], s[2]) / 2), (s[0], s[2], s[1]), pieza, rot_z=r)
        else:
            self.t.caja((x, y, z), s, pieza, rot_z=r, rot_x=-tumb)

    def coche(self, x, y, rz, pieza, largo=4.2, ancho=1.8, alto=1.45):
        (px, py, pz), r, tumb, vivo = self.mover(x, y, 0.0, (largo, ancho, alto), rz, alto)
        if not vivo and ESTADO < 3:
            vivo = True                              # los coches no desaparecen: pesan demasiado
        if not vivo:
            return
        lado = tumb > 1.0                            # volcado de lado, hacia dentro
        rx = -math.pi / 2 if lado else 0.0
        zc = ancho / 2 if lado else 0.0
        cs, sn = math.cos(r), math.sin(r)
        def p(dx, dz):                               # punto del coche (a lo largo dx, alto dz) ya volcado
            if lado:
                return (px + cs * dx, py + sn * dx + 0.0, zc + 0.0) if dz is None else (px + cs * dx - sn * dz * 0,
                                                                                    py + sn * dx + dz * 0.0, zc)
            return (px + cs * dx, py + sn * dx, dz)
        t = self.t
        if lado:
            t.caja((px, py + 0.3, ancho / 2), (largo, alto * 0.75, ancho), pieza, rot_z=r)
            t.caja((px + cs * 0.2, py + sn * 0.2 + 0.95, ancho / 2), (largo * 0.5, 0.5, ancho * 0.85), "luna", rot_z=r)
            return
        t.caja((px, py, 0.55), (largo, ancho, 0.7), pieza, rot_z=r)
        t.caja((px - cs * 0.2, py - sn * 0.2, 1.1), (largo * 0.52, ancho * 0.9, 0.5), "luna", rot_z=r)
        t.caja((px - cs * 0.2, py - sn * 0.2, 1.38), (largo * 0.48, ancho * 0.86, 0.06), pieza, rot_z=r)
        if self.fino:
            for s in (-1, 1):
                for d in (-1, 1):
                    t.caja((px + cs * d * largo * 0.32 - sn * s * ancho * 0.45,
                            py + sn * d * largo * 0.32 + cs * s * ancho * 0.45, 0.3), (0.6, 0.2, 0.6), "neumatico",
                           rot_z=r)


def construir(nombre="SM_BarricadaCivil", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    b = _B(t, fino)
    v = VARIANTE
    if v == 0:
        b.coche(-1.9, 0.0, 0.12, "coche_a")
        b.coche(2.0, 0.2, -0.18, "coche_b")
        for x in (-3.6, 0.1, 3.7):
            b.caja((x, -0.9, 1.0), (1.2, 0.08, 2.0), "valla")
    elif v == 1:
        b.caja((-3.0, 0.0, 1.0), (1.2, 0.6, 2.0), "madera", rz=0.05)            # armarios
        b.caja((-1.7, 0.1, 0.9), (1.0, 0.6, 1.8), "madera", rz=-0.1)
        b.caja((0.0, 0.0, 0.45), (2.0, 0.9, 0.9), "sofa", rz=0.08)               # sofá
        b.caja((0.0, 0.25, 1.05), (2.0, 0.3, 0.5), "sofa", rz=0.08)
        b.caja((1.9, -0.1, 0.95), (1.5, 0.25, 1.9), "colchon", rz=-0.05)         # colchones de pie
        b.caja((3.3, 0.0, 0.95), (1.4, 0.25, 1.9), "colchon", rz=0.1)
        b.caja((0.9, 0.6, 0.5), (1.0, 0.8, 1.0), "madera", rz=0.4)               # cómoda detrás
    elif v == 2:
        for k in range(4):                                                       # vallas de obra con sus bases
            x = -3.0 + k * 2.0
            b.caja((x, -0.5, 1.0), (1.95, 0.06, 2.0), "valla", rz=(_h(k, 7) - 0.5) * 0.1)
            if fino or k % 2 == 0:
                b.caja((x, -0.5, 0.1), (1.95, 0.4, 0.2), "base")
        b.caja((-1.5, 0.6, 0.65), (1.8, 1.1, 1.3), "contenedor", rz=0.1)        # contenedor de basura
        for k in range(3):
            b.caja((1.6 + k * 0.4, 0.6, 0.6 + k * 0.0), (1.2, 0.15, 1.0), "madera", rz=1.4 + 0.1 * k)
    elif v == 3:
        b.coche(0.0, 0.2, 0.06, "coche_c", largo=5.6, ancho=2.1, alto=2.3)        # furgoneta
        for k, (x, y) in enumerate(((-3.6, -0.6), (-3.3, 0.5), (3.5, -0.5), (3.7, 0.6))):
            b.caja((x, y, 0.42 + 0.27 * (k % 2)), (0.7, 0.7, 0.25), "neumatico")
            b.caja((x, y, 0.15), (0.7, 0.7, 0.25), "neumatico")
    else:
        b.coche(2.4, 0.1, -0.1, "coche_a", largo=3.6, ancho=1.65, alto=1.4)
        for k in range(3):                                                       # puertas apoyadas
            b.caja((-3.2 + k * 0.9, -0.3, 1.0), (0.85, 0.06, 2.0), "madera", rz=(_h(k, 8) - 0.5) * 0.3)
        b.caja((-0.6, 0.2, 0.6), (1.2, 0.8, 1.2), "madera", rz=0.3)              # palés apilados
        for k in range(2):
            b.caja((0.2 + k * 0.5, -0.5, 0.45), (0.5, 0.5, 0.9), "sofa", rz=0.5 * k)  # sillas
    if ESTADO >= 2:                                  # restos sueltos por dentro
        for k in range(3 if fino else 2):
            t.caja((-3.0 + k * 2.8, 2.2 + _h(k, 9) * 1.2, 0.12), (0.8, 0.5, 0.24), "escombro", rot_z=_h(k, 10) * 3)
    return t.objeto(nombre, bisel=0)
