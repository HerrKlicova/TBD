"""Matas del suelo de Sagunto (capa C, D-N67; docs/arte/suelo-sagunto.md). MUESTRA para Eugenio. Teo, 6-nov-2026.

Fotos de Paula H1 (hierba seca de bancales, Marina Alta) y H3 (lentisco); el cardo sigue SIN FOTO (de memoria).
H1 enseña la hierba seca pajiza, casi del color del suelo: por eso la mitad de sus hojas van en b4a080. Reglas de Lucía (§3, §4): ≤ 60 triángulos, ≤ 0,4 m de alto, hierba en 88885c
y 847c34, puntas en b4a080, 70784c solo de acento; SIN valor partido ni contorno propio (ΔL* 12–16 del suelo);
nada de valores de la horda, rojo ni ámbar. Sin colisión ni sombra (las pone Marcos al instanciar).
Cada hoja es un tetraedro cerrado (4 triángulos), así la malla es estanca con muy pocos triángulos.
Las hojas van muy abiertas (de 20° a 45° sobre el suelo): con la luz del juego, una hoja casi vertical se oscurece
hasta L* 21, el mismo valor que el cuerpo del zombi (medido en la primera prueba). Abiertas, quedan en su color.
Variantes (TBD_VARIANTE): 0 hierba seca; 1 hierba verde; 2 cardo seco.
El LOD1 lleva menos hojas (a 0,17 m/px una hoja suelta no llega a 1 px; lo que se lee es la mancha).
"""
import math, os, random
from mathutils import Vector
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["hierba", "hierba_v", "punta", "acento"]
COLORES = {"hierba": "88885c", "hierba_v": "847c34", "punta": "b4a080", "acento": "70784c"}
FACCION = "neutro"
PRESUPUESTO = {0: (1, 60), 1: (1, 40)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True   # normales horneadas hacia arriba (Lucía, §8): la misma luz que el suelo
TIPOS = ("hierba_seca", "hierba_verde", "cardo")


def hoja(t, base, punta, ancho, pieza, giro=0.0):
    """Tetraedro: base triangular en el suelo (un poco enterrada) y la punta en `punta`."""
    bm = t.bm
    bx, by, bz = base
    vs = [bm.verts.new((bx + ancho / 2 * math.cos(giro + k * 2.094), by + ancho / 2 * math.sin(giro + k * 2.094), bz))
          for k in range(3)]
    p = bm.verts.new(punta)
    caras = [bm.faces.new((vs[2], vs[1], vs[0]))] + [bm.faces.new((vs[k], vs[(k + 1) % 3], p)) for k in range(3)]
    t._marcar(caras, pieza)


def mata(t, tipo, cx=0.0, cy=0.0, semilla=1, lod=0, escala=1.0):
    """Construye una mata en (cx, cy). Devuelve el nº de hojas."""
    R = random.Random(semilla * 7919 + TIPOS.index(tipo))
    e = escala
    if tipo in ("hierba_seca", "hierba_verde"):
        n = (14 if tipo == "hierba_seca" else 15) if lod == 0 else 9
        for i in range(n):
            a = i * 2.39996 + R.uniform(-0.3, 0.3)              # ángulo áureo: la mata sale redonda
            r0 = R.uniform(0.0, 0.07) * e
            h = R.uniform(0.06, 0.2 if tipo == "hierba_seca" else 0.24) * e
            caida = R.uniform(0.26, 0.4 if tipo == "hierba_seca" else 0.32) * e   # la seca se abre más
            b = (cx + r0 * math.cos(a), cy + r0 * math.sin(a), -0.01)
            p = (b[0] + caida * math.cos(a), b[1] + caida * math.sin(a), h)
            if tipo == "hierba_seca":
                pieza = "punta" if i % 2 == 1 else "hierba"   # H1 (Paula): la hierba seca es pajiza; mitad b4a080
            else:
                pieza = "acento" if i % 5 == 4 else ("punta" if i % 7 == 6 else "hierba_v")
            hoja(t, b, p, 0.08 * e, pieza, giro=a)
        return n
    # cardo: roseta de hojas pinchudas tumbadas, dos o tres tallos y cabezuelas secas
    nh = 6 if lod == 0 else 4
    for i in range(nh):
        a = i * 2 * math.pi / nh + R.uniform(-0.2, 0.2)
        b = (cx, cy, -0.01)
        L = R.uniform(0.22, 0.3) * e
        hoja(t, b, (cx + L * math.cos(a), cy + L * math.sin(a), 0.07 * e), 0.12 * e, "hierba", giro=a)
    nt = 3 if lod == 0 else 2
    for i in range(nt):
        a = i * 2.1 + R.uniform(-0.3, 0.3)
        d = 0.06 * e
        h = (0.4 - 0.06 * i) * e - 0.05 * e
        top = (cx + d * math.cos(a), cy + d * math.sin(a), h)
        hoja(t, (cx + 0.02 * math.cos(a), cy + 0.02 * math.sin(a), -0.01), top, 0.035 * e, "punta", giro=a)   # tallo seco, claro: vertical y oscuro caería en L* 21
        # cabezuela: octaedro pequeño (8 triángulos) en la punta del tallo
        _octaedro(t, (top[0], top[1], top[2] + 0.025 * e), 0.035 * e, "punta")
    return nh + nt


def _octaedro(t, c, r, pieza):
    bm = t.bm
    x, y, z = c
    V = [bm.verts.new(p) for p in ((x + r, y, z), (x, y + r, z), (x - r, y, z), (x, y - r, z), (x, y, z + r), (x, y, z - r))]
    caras = []
    for k in range(4):
        a, b = V[k], V[(k + 1) % 4]
        caras.append(bm.faces.new((a, b, V[4])))
        caras.append(bm.faces.new((b, a, V[5])))
    t._marcar(caras, pieza)


def construir(nombre="SM_Mata", lod=0):
    t = Taller(PIEZAS)
    mata(t, TIPOS[VAR % 3], semilla=3, lod=lod)
    return t.objeto(nombre + "_" + TIPOS[VAR % 3], bisel=0)
