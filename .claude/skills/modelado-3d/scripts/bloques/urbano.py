"""Mobiliario de la plaza de la estación de Nules (Álvaro, 8-nov, nules-nivel.md §6.6: plaza con jardín, X 10–100,
Z 92–128; urbano/palmera, urbano/farola y urbano/banco, sin colisión). Teo (3), 8-nov.

Tipos genéricos de plaza valenciana, sin foto (medidas SIN VERIFICAR, típicas):
- Palmera datilera (Phoenix): tronco de escamas de 9 m, ligeramente curvado, con la corona de hojas arqueadas.
- Farola de plaza: fernandina de fundición de 4 m con farol, o de brazo de 8 m.
- Banco de plaza de 1,8 m.
Colores: tronco de palmera b4a080 con la base 845c28, hojas por valor partido 88885c/70784c/50541c, hojas secas
847c34; farola 6c7874 con el farol 94948c y el cristal 6c7874 (apagado: el f0dc9c se leía como luz encendida, Lucía 8-nov 13:45); banco de listones 845c28 con patas
6c7874, de hormigón 94948c. Nada rojo ni en la banda de la horda. En el LOD1 nada baja de 0,34 m.
Origen en el suelo, en el pie (palmera y farola) o en el centro (banco, de cara a −Y).
TBD_VARIANTE = 5 · tipo + variante (0..14). Tipos: 0 Palmera, 1 Farola, 2 Banco.
  Palmera: 0 datilera de 9 m; 1 alta y curvada de 12 m; 2 joven de 4 m; 3 seca, con las hojas colgando; 4 tronchada a
  5 m, con la corona en el suelo.
  Farola: 0 fernandina de 4 m; 1 de brazo de 8 m; 2 fernandina de dos faroles; 3 doblada por un golpe; 4 caída.
  Banco: 0 de listones con respaldo; 1 de hormigón sin respaldo; 2 de listones sin respaldo; 3 roto, con los listones
  caídos; 4 volcado.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 15
TIPO, V = VAR // 5, VAR % 5
NOMBRES_T = ["Palmera", "Farola", "Banco"]
PIEZAS = ["tronco", "base", "hoja", "seca", "metal", "farol", "cristal", "madera", "hormigon"]
COLORES = {"tronco": "b4a080", "base": "845c28", "hoja": "70784c", "seca": "847c34", "metal": "6c7874",
           "farol": "94948c", "cristal": "6c7874", "madera": "845c28", "hormigon": "94948c"}
VALOR_PIEZAS = {"hoja": ("88885c", "70784c", "50541c")}
FACCION = "neutro"
PRESUPUESTO = {0: (12, 900), 1: (8, 450)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _barra(t, p0, p1, r, pieza):
    dx, dy, dz = p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]
    L = math.sqrt(dx * dx + dy * dy + dz * dz)
    t.caja(((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, (p0[2] + p1[2]) / 2), (r, r, L + r * 0.3), pieza,
           rot_z=math.atan2(dy, dx), rot_y=math.atan2(math.hypot(dx, dy), dz))


def construir(nombre="SM_Urbano", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    (_palmera, _farola, _banco)[TIPO](t, fino)
    return t.objeto(nombre, bisel=0)


def _corona(t, fino, c, n, largo, caida, pieza="hoja"):
    """Hojas arqueadas: dos tramos por hoja (el de fuera cae más)."""
    gw = 0.5 if fino else 0.6
    for i in range(n):
        a = i * 2 * math.pi / n + (0.3 if i % 2 else 0.0)
        e = (0.5 if i % 2 else 0.25) + caida           # inclinación hacia abajo (rad) del primer tramo
        p1 = (c[0] + math.cos(a) * largo * 0.55 * math.cos(e), c[1] + math.sin(a) * largo * 0.55 * math.cos(e),
              c[2] + largo * 0.55 * math.sin(-e + 0.6))
        e2 = e + 0.6
        p2 = (p1[0] + math.cos(a) * largo * 0.5 * math.cos(e2), p1[1] + math.sin(a) * largo * 0.5 * math.cos(e2),
              p1[2] - largo * 0.5 * math.sin(e2))
        for q0, q1, w in ((c, p1, gw), (p1, p2, gw * 0.8)):
            dx, dy, dz = q1[0] - q0[0], q1[1] - q0[1], q1[2] - q0[2]
            L = math.sqrt(dx * dx + dy * dy + dz * dz)
            t.caja(((q0[0] + q1[0]) / 2, (q0[1] + q1[1]) / 2, (q0[2] + q1[2]) / 2), (L + 0.1, w, 0.06 if fino else 0.12),
                   pieza, rot_z=a, rot_y=math.atan2(-dz, math.hypot(dx, dy)))


def _palmera(t, fino):
    v = V
    H = (9.0, 12.0, 4.0, 9.0, 5.0)[v]
    curva = (0.4, 1.4, 0.1, 0.5, 0.3)[v]
    r = (0.35, 0.32, 0.4, 0.33, 0.35)[v] if fino else 0.4
    n = 4 if fino else 2
    pts = [(curva * (k / n) ** 2, 0.0, H * k / n) for k in range(n + 1)]
    for a, b in zip(pts, pts[1:]):
        _barra(t, a, b, r * 2, "tronco")
    t.caja((0, 0, 0.4), (r * 2.6, r * 2.6, 0.8), "base")
    top = pts[-1]
    if v == 4:                                       # tronchada: tocón astillado y la corona en el suelo
        t.caja((top[0], 0, H + 0.2), (r * 1.6, r * 1.6, 0.5), "base", rot_y=0.3)
        _barra(t, (top[0] + 0.6, 0.3, 0.4), (top[0] + 5.5, 1.4, 0.4), r * 1.8, "tronco")
        _corona(t, fino, (top[0] + 6.2, 1.6, 0.9), 8 if fino else 6, 3.0, 0.9, "seca")
        return
    nh = (14, 14, 10, 12, 14)[v] if fino else 8
    largo = (3.6, 3.4, 2.4, 3.4, 3.6)[v]
    _corona(t, fino, (top[0], top[1], top[2] + 0.2), nh, largo, 0.9 if v == 3 else 0.0, "seca" if v == 3 else "hoja")
    t.esfera((top[0], top[1], top[2] + 0.1), (r * 1.6, r * 1.6, r * 1.4), "base", 1)
    if v != 3 and fino:                              # faldón de hojas secas bajo la corona
        _corona(t, fino, (top[0], top[1], top[2] - 0.1), 6, largo * 0.6, 1.1, "seca")


def _farola(t, fino):
    v = V
    g = 0.14 if fino else 0.34
    if v == 4:                                       # caída en el suelo, con la base arrancada
        t.caja((0, 0, 0.25), (0.6, 0.6, 0.5), "metal", rot_z=0.2)
        _barra(t, (0.3, 0.1, 0.12), (4.0, 0.9, 0.12), g, "metal")
        t.caja((4.3, 1.0, 0.25), (0.5, 0.5, 0.5), "farol", rot_z=0.3)
        return
    if v == 1:                                       # de brazo, 8 m
        t.caja((0, 0, 4.0), (g * 1.2, g * 1.2, 8.0), "metal")
        _barra(t, (0, 0, 7.8), (0, -1.6, 8.2), g, "metal")
        t.caja((0, -1.8, 8.05), (0.4, 0.8, 0.2), "farol")
        t.caja((0, -1.8, 7.93), (0.34, 0.6, 0.06), "cristal")
        t.caja((0, 0, 0.3), (0.4, 0.4, 0.6), "metal")
        return
    H = 4.0
    t.caja((0, 0, 0.45), (0.5, 0.5, 0.9), "metal")                        # basa de fundición
    dobla = v == 3
    if dobla:
        _barra(t, (0, 0, 0.8), (0, 0, 2.2), g, "metal")
        tip = (1.6, -0.3, 3.2)
        _barra(t, (0, 0, 2.2), tip, g, "metal")
    else:
        t.caja((0, 0, H / 2), (g, g, H), "metal")
        tip = (0, 0, H)
    faroles = [(-0.6, 0), (0.6, 0)] if v == 2 else [(0, 0)]
    if v == 2:
        t.caja((0, 0, H - 0.1), (1.4, g, g), "metal")
    for dx, dy in faroles:
        c = (tip[0] + dx, tip[1] + dy, tip[2] + 0.35)
        t.caja(c, (0.45, 0.45, 0.6), "farol", rot_y=0.5 if dobla else 0)
        if fino:
            t.caja((c[0], c[1] - 0.23, c[2]), (0.32, 0.02, 0.4), "cristal", rot_y=0.5 if dobla else 0)
        t.caja((c[0], c[1], c[2] + 0.4), (0.55, 0.55, 0.15), "metal", rot_y=0.5 if dobla else 0)


def _banco(t, fino):
    v = V
    g = 0.08 if fino else 0.34
    if v == 1:                                       # de hormigón
        t.caja((0, 0, 0.23), (1.8, 0.5, 0.06), "hormigon")
        for x in (-0.7, 0.7):
            t.caja((x, 0, 0.1), (0.25, 0.45, 0.2), "hormigon")
        return
    rot = {"rot_x": math.radians(90)} if v == 4 else {}
    zb = 0.0
    if v == 4:                                       # volcado hacia atrás: respaldo en el suelo
        t.caja((0, 0.3, 0.25), (1.8, 0.45, 0.06), "madera", rot_x=math.radians(80))
        t.caja((0, 0.55, 0.06), (1.8, 0.45, 0.06), "madera")
        for x in (-0.75, 0.75):
            t.caja((x, 0.35, 0.25), (g, 0.5, 0.45), "metal", rot_x=math.radians(80))
        return
    for x in (-0.75, 0.75):                          # patas
        t.caja((x, 0, 0.22), (g, 0.45, 0.44), "metal")
        if v in (0, 3):
            t.caja((x, 0.22, 0.65), (g, g, 0.5), "metal", rot_x=math.radians(-12))
    n = 3 if fino else 1
    caidos = (1,) if v == 3 else ()
    for k in range(n):
        if k in caidos:
            t.caja((0.2, -0.5, 0.03), (1.8, 0.12, 0.04), "madera", rot_z=0.3)
            continue
        w = 0.12 if fino else 0.45
        t.caja((0, -0.15 + k * 0.15 if fino else 0, 0.45), (1.8, w, 0.04 if fino else 0.08), "madera")
    if v in (0, 3):                                  # respaldo
        for k in range(2 if fino else 1):
            t.caja((0, 0.26, 0.62 + k * 0.18 if fino else 0.7), (1.8, 0.04 if fino else 0.08, 0.12 if fino else 0.34),
                   "madera", rot_x=math.radians(-12))
