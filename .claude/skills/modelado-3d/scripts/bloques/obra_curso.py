"""Kit de «obra en curso» para el capítulo 2 del tutorial (reparar): lo que se ve sobre un tramo mientras los zapadores
lo reparan o lo reconstruyen (Lucía, 8-nov 18:34; construccion-reparar.md). Teo (3), 8-nov.

Se pone ENCIMA del tramo, con su mismo origen (en el suelo, en el centro del tramo, muro a lo largo de X, FUERA = −Y),
y se quita al acabar. Las obras van por la cara de dentro (+Y), donde trabajan los zapadores, salvo lo que se pone sobre
el propio muro. Dos tamaños: 8 m (tramo de sacos y puerta de vehículos) y 10 m (módulo de HESCO).
Nada de ámbar ni rojo: el aviso de ruido es el icono de Bruno. Medidas DE MEMORIA (SIN VERIFICAR).

VAR = 10·pieza + 2·variante + tamaño (0..49), tamaño 0 = 8 m y 1 = 10 m.
Piezas:
  0 Sacos: sacos a medio colocar encima del tramo y pila de sacos llenos detrás. Variantes: 0 pila y hilada empezada;
    1 cadena de sacos en el suelo (de mano en mano); 2 palé de sacos llenos y sacos vacíos plegados; 3 montón de arena
    con sacos a medio llenar; 4 dos pilas en los extremos.
  1 Herramientas: carretilla y palas clavadas. Variantes: 0 carretilla llena y dos palas en el montón; 1 carretilla
    volcada y pico; 2 dos carretillas y tres palas; 3 palas clavadas en fila y cubo; 4 carretilla vacía y tablón de
    rodadura.
  2 HescoAbierto: una celda de HESCO a medio llenar (malla abierta, sin tapa, con la arena dentro) y su montón de
    arena. Variantes: llena a 1/4, 1/2, 3/4, con el geotextil colgando y con dos celdas a la vez.
  3 PuertaApuntalada: tablones en diagonal contra la puerta por la cara de dentro, con sus calzos. Variantes: 2, 3 o 4
    puntales, con travesaño y con sacos en el pie.
  4 Reconstruccion: la brecha que vuelve al 30 %: escombro apartado a los lados y la primera hilada nueva (sacos en
    8 m, celdas bajas de HESCO en 10 m). Variantes: hilada a 1/3, a 2/3, entera, con estacas y cordel de replanteo, y
    con un palé de sacos al lado.
Colores: sacos 605c48 (los del muro), sacos vacíos 847c34, arena b4a080, HESCO 70784c con malla b8b8b8 y geotextil
50541c, tablones 845c28, herramientas 605c48 con mango 845c28, carretilla 6c7874, escombro 94948c, palé b4a080.
LOD1: sin mangos ni cordel; nada de menos de 0,34 m.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 50
PIEZA, VARIANTE, TAM = VAR // 10, (VAR % 10) // 2, VAR % 2
NOMBRES_P = ["Sacos", "Herramientas", "HescoAbierto", "PuertaApuntalada", "Reconstruccion"]
NOMBRES_V = [f"{p}_v{v}_{t}m" for p in NOMBRES_P for v in range(5) for t in (8, 10)]
PIEZAS = ["sacos", "vacio", "arena", "hesco", "malla", "geo", "tablon", "metal", "mango", "carretilla", "escombro",
          "palet"]
COLORES = {"sacos": "605c48", "vacio": "847c34", "arena": "b4a080", "hesco": "70784c", "malla": "b8b8b8",
           "geo": "50541c", "tablon": "845c28", "metal": "605c48", "mango": "845c28", "carretilla": "6c7874",
           "escombro": "94948c", "palet": "b4a080"}
FACCION = "propio"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 3000), 1: (10, 900)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
GR = 1.0                                              # grueso del muro de sacos (cara de dentro en y = +0,5)
FH = 1.1                                              # fondo del HESCO (cara de dentro en y = +0,55)


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


class _K:
    def __init__(self, fino):
        self.t = Taller(PIEZAS)
        self.fino = fino

    def saco(self, x, y, z, rz=0.0, p="sacos"):
        self.t.caja((x, y, z + 0.15), (0.85, 0.48, 0.29), p, rot_z=rz)

    def pila(self, x, y, filas, cols=2, rz=0.0):
        """Pila de sacos llenos trabada: cols a lo ancho, filas de alto."""
        c, s = math.cos(rz), math.sin(rz)
        for f in range(filas):
            n = cols - (f % 2)
            for i in range(n):
                dx = (i - (n - 1) / 2) * 0.88
                self.saco(x + dx * c, y + dx * s, f * 0.3, rz)

    def monton(self, x, y, r, h, p="arena"):
        self.t.cilindro((x, y, h / 2), r, h, "Z", 10 if self.fino else 6, p, r2=r * 0.25)

    def pala(self, x, y, inc=0.25, rz=0.0, clavada=True):
        if not self.fino:
            return
        z = 0.55 if clavada else 0.04
        if clavada:
            self.t.caja((x, y, z), (0.04, 0.04, 1.1), "mango", rot_x=inc, rot_z=rz)
            self.t.caja((x, y - math.sin(inc) * 0.6, 0.12), (0.22, 0.03, 0.28), "metal", rot_x=inc, rot_z=rz)
        else:
            self.t.caja((x, y, 0.03), (0.04, 1.1, 0.04), "mango", rot_z=rz)
            self.t.caja((x + 0.6 * math.sin(-rz), y + 0.6 * math.cos(rz), 0.03), (0.22, 0.28, 0.03), "metal", rot_z=rz)

    def carretilla(self, x, y, rz=0.0, llena=False, volcada=False):
        c, s = math.cos(rz), math.sin(rz)
        P = lambda dx, dy: (x + dx * c - dy * s, y + dx * s + dy * c)
        if volcada:
            self.t.caja((*P(0, 0), 0.3), (0.65, 0.95, 0.5), "carretilla", rot_z=rz, rot_y=1.3)
            self.monton(*P(0.6, 0), 0.5, 0.25)
            return
        self.t.caja((*P(0, 0), 0.5), (0.65, 0.9, 0.35), "carretilla", rot_z=rz)
        self.t.cilindro((*P(0, -0.6), 0.2), 0.2, 0.1, "X", 8, "metal")
        if self.fino:
            for sx in (-0.25, 0.25):
                self.t.caja((*P(sx, 0.75), 0.45), (0.04, 0.7, 0.04), "mango", rot_z=rz)
                self.t.caja((*P(sx, 0.2), 0.18), (0.04, 0.04, 0.36), "metal", rot_z=rz)
        if llena:
            self.t.caja((*P(0, 0), 0.7), (0.55, 0.75, 0.08), "arena", rot_z=rz)

    def tablon(self, c, largo, p="tablon", rz=0.0, rx=0.0, ry=0.0, ancho=0.22):
        self.t.caja(c, (ancho if self.fino else 0.34, largo, 0.05 if self.fino else 0.34), p, rot_z=rz, rot_x=rx, rot_y=ry)

    def celda(self, x, y, lado, alto, llenado, abierta=True, geo=False):
        """Celda de HESCO abierta por arriba: cuatro paredes de malla y la arena hasta 'llenado' (0..1)."""
        g = 0.04 if self.fino else 0.34
        for s in (-1, 1):
            self.t.caja((x + s * (lado / 2 - g / 2), y, alto / 2), (g, lado, alto), "hesco")
            self.t.caja((x, y + s * (lado / 2 - g / 2), alto / 2), (lado - 2 * g - 0.002, g, alto), "hesco")
        if self.fino:                                 # alambres de la malla arriba
            for s in (-1, 1):
                self.t.caja((x + s * lado / 2, y, alto + 0.01), (0.03, lado + 0.03, 0.03), "malla")
                self.t.caja((x, y + s * lado / 2, alto + 0.01), (lado + 0.02, 0.03, 0.03), "malla")
        if llenado > 0:
            ha = alto * llenado
            self.t.caja((x, y, ha / 2), (lado - 2 * g - 0.01, lado - 2 * g - 0.01, ha), "arena")
        if geo:                                       # el geotextil colgando por fuera de una cara
            self.t.caja((x + lado / 2 + 0.05, y, alto * 0.55), (0.04 if self.fino else 0.34, lado * 0.9, alto * 0.7),
                        "geo", rot_y=0.25)


def construir(nombre="SM_ObraCurso", lod=0):
    k = _K(lod == 0)
    t = k.t
    p, v, L = PIEZA, VARIANTE, (8.0 if TAM == 0 else 10.0)
    ya = GR / 2 + 0.6                                 # primera franja de trabajo por dentro
    yo = -ya                                          # y por fuera (la cámara del RTS ve una de las dos caras)
    if os.environ.get("TBD_CON_TRAMO", "") == "1":    # solo para las hojas: el tramo de debajo, simplificado
        if p == 3:
            t.caja((0, 0.1, 1.05), (L, 0.16, 2.1), "carretilla")
        elif p == 4:
            pass
        elif p == 2:
            for s in (-1, 1):
                t.caja((s * (L / 4 + 0.4), 0, 1.05), (L / 2 - 0.8, FH, 2.1), "hesco")
        else:
            t.caja((0, 0, 0.6), (L, GR, 1.19), "sacos" if TAM == 0 else "hesco")
    if p == 0:                                        # SACOS
        zt = 1.205                                    # sobre el tramo de sacos (4 hiladas)
        if v in (0, 3, 4):
            for i in range(3 if v != 0 else 4):       # hilada empezada encima del muro
                k.saco(-L / 2 + 0.5 + i * 0.88, 0.25, zt, 0.0)
            k.saco(-L / 2 + 0.6, -0.1, zt + 0.3, 0.15)
        if v == 0:
            k.pila(L / 4, -(ya + 0.4), 4, 3)
        elif v == 1:                                  # cadena: sacos sueltos en el suelo hacia dentro
            for i in range(6):
                k.saco(-1.5 + i * 0.9 * (L / 8), ya + 0.4 + _h(i) * 0.8, 0.0, (_h(i, 1) - 0.5) * 1.2)
            k.pila(L / 2 - 0.9, -(ya + 1.6), 3, 2)
        elif v == 2:
            t.caja((L / 4, ya + 0.6, 0.07), (1.2, 1.0, 0.14), "palet")
            for f in range(4):
                for i in range(2):
                    k.saco(L / 4 + (i - 0.5) * 0.36, ya + 0.6, 0.145 + f * 0.3, math.pi / 2)
            for i in range(3):
                t.caja((-L / 4 + i * 0.3, ya + 0.6, 0.02 + i * 0.03), (0.6, 0.9, 0.03), "vacio", rot_z=_h(i) * 0.5)
        elif v == 3:
            k.monton(-L / 4, -(ya + 1.0), 1.2, 0.9)
            for i in range(3):
                t.caja((-L / 4 + 1.4, ya + 0.3 + i * 0.55, 0.2), (0.48, 0.4, 0.4), "vacio", rot_z=_h(i, 2))
            k.pala(-L / 4 + 0.4, yo - 0.9, 0.3)
        else:
            k.pila(-L / 2 + 1.0, -(ya + 0.4), 3, 2)
            k.pila(L / 2 - 1.0, -(ya + 0.4), 3, 2)
    elif p == 1:                                      # HERRAMIENTAS
        if v == 0:
            k.monton(-L / 4, -(ya + 1.0), 1.0, 0.8)
            k.carretilla(0.4, yo - 0.8, 0.3, llena=True)
            k.pala(-L / 4 + 0.2, yo - 0.9, 0.3, 0.2)
            k.pala(-L / 4 - 0.3, yo - 1.2, -0.3, -0.4)
        elif v == 1:
            k.carretilla(-L / 6, ya + 0.9, 0.8, volcada=True)
            if k.fino:                                # pico tirado
                t.caja((L / 6, ya + 0.6, 0.03), (0.04, 0.9, 0.04), "mango", rot_z=0.7)
                t.caja((L / 6 - 0.3, ya + 0.9, 0.05), (0.5, 0.05, 0.06), "metal", rot_z=0.7)
            k.pala(L / 4, yo - 1.2, 0, 0.3, clavada=False)
        elif v == 2:
            k.carretilla(-L / 4, yo - 0.8, -0.2, llena=True)
            k.carretilla(L / 6, ya + 0.9, 0.4)
            k.monton(L / 3, -(ya + 1.5), 0.8, 0.6)
            for i in range(3):
                k.pala(L / 3 - 0.4 + i * 0.4, yo - 1.4, 0.2 + i * 0.1, i * 0.4)
        elif v == 3:
            k.monton(0, -(ya + 1.0), 1.1, 0.7)
            for i in range(4):
                k.pala(-0.9 + i * 0.6, yo - 0.5, 0.15, 0.0)
            t.cilindro((1.6, yo - 0.6, 0.15), 0.15, 0.3, "Z", 8, "metal")   # cubo
        else:
            k.carretilla(0, ya + 0.9, math.pi / 2)
            k.tablon((0, ya + 0.9, 0.03), 3.0, rz=math.pi / 2)
            k.pala(-L / 4, yo - 0.5, 0, 0.1, clavada=False)
    elif p == 2:                                      # HESCO ABIERTO (celdas de 1,1 m, sobre la línea del muro)
        lado, alto = 1.1, 2.1 if TAM == 1 else 1.4
        llen = {0: 0.25, 1: 0.5, 2: 0.75, 3: 0.4, 4: 0.6}[v]
        xs = [0.0] if v != 4 else [-0.6, 0.6]
        for i, x in enumerate(xs):
            k.celda(x, 0.0, lado, alto, llen if i == 0 else 0.15, geo=v == 3)
        k.monton(0.3, -(ya + 1.0), 1.1, 0.8)
        k.pala(0.0, yo - 0.9, 0.3, 0.2)
        if v in (1, 4):
            k.carretilla(-L / 4, ya + 0.6, 0.4, llena=True)
        if v == 2:
            k.pala(0.6, yo - 1.3, -0.3, -0.5)
    elif p == 3:                                      # PUERTA APUNTALADA (puerta de 8 m, 2,1 m; la de 10 m, por si acaso)
        n = {0: 2, 1: 3, 2: 4, 3: 3, 4: 2}[v]
        ys = 0.19                                     # cara de dentro de las hojas
        for i in range(n):
            x = (i - (n - 1) / 2) * (L / (n + 1)) * 1.4
            k.tablon((x, ys + 0.75, 0.95), 2.3, rx=-0.95)                    # puntal: de 1,9 m de alto al suelo
            t.caja((x, ys + 1.55, 0.08), (0.3, 0.25, 0.16), "tablon")         # calzo
            if v == 4:
                k.saco(x, ys + 1.9, 0.0, 0.0)
        for s in (-1, 1):                             # aspa de tablones clavada en la cara de fuera (se ve desde los dos lados)
            for sg in (-1, 1):
                t.caja((s * L / 4, -0.03 - 0.03 * (sg + 1), 1.05), (2.6, 0.05 if k.fino else 0.34, 0.22 if k.fino else 0.34),
                       "tablon", rot_y=sg * 0.62)
        if v == 3:                                    # travesaño horizontal
            k.tablon((0, ys + 0.03, 1.5), L * 0.8, rz=math.pi / 2)
        k.pala(L / 2 - 0.8, ys + 2.4, 0.2, 0.3) if v != 4 else None
    else:                                             # RECONSTRUCCION
        frac = {0: 1 / 3, 1: 2 / 3, 2: 1.0, 3: 1 / 3, 4: 2 / 3}[v]
        for s in (-1, 1):                             # escombro apartado a los dos lados de la brecha, por dentro
            k.monton(s * (L / 2 - 0.6), s * (ya + 1.2), 0.9, 0.45, "escombro")
            if k.fino:
                for j in range(3):
                    t.caja((s * (L / 2 - 1.2 - j * 0.3), s * (ya + 0.5 + _h(j, s + 3)), 0.1), (0.4, 0.3, 0.2), "escombro",
                           rot_z=_h(j, s))
        if TAM == 0:                                  # primera hilada de sacos (dos a lo ancho)
            n = int(round(8 / 0.88 * frac))
            for i in range(n):
                for yy in (-0.25, 0.25):
                    k.saco(-L / 2 + 0.47 + i * 0.88, yy, 0.0)
        else:                                         # primeras celdas de HESCO bajas (0,7 m), llenas
            n = max(1, int(round(9 * frac)))
            for i in range(n):
                k.celda(-L / 2 + 0.56 + i * 1.11, 0.0, 1.06, 0.7, 0.95)
        if v == 3 and k.fino:                         # estacas y cordel de replanteo
            for s in (-1, 1):
                for yy in (-0.6, 0.6):
                    t.caja((s * (L / 2 - 0.1), yy, 0.3), (0.05, 0.05, 0.6), "mango")
                t.caja((0, s * 0.6, 0.55), (L - 0.2, 0.015, 0.015), "malla")
        if v == 4:
            t.caja((L / 4, -(ya + 1.8), 0.07), (1.2, 1.0, 0.14), "palet")
            k.pila(L / 4, -(ya + 1.8), 3, 1, math.pi / 2)
        k.pala(-L / 6, yo - 0.6, 0.2, 0.3)
    return t.objeto(nombre, bisel=0)
