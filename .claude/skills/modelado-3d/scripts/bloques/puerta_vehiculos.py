"""Puerta de vehículos E4 del muro sur de Sagunto, de hoja doble (coordinador, 7-nov 01:14). Teo, 7-nov.

Cotas del código de Nacho (PR #24, rama nacho/unidades-noche): `PieceKind.PuertaVeh`, `VehGateWidthMm` = 8000 (8 m a lo
largo del muro, centrada en su X0..X1), `Obras.HeightMm(PuertaVeh)` = 2100 mm, en el muro sur de SACOS (1,2 m de alto y
1,0 m de grueso en la vista: `WallHalf × 0,5` de semigrosor) en X −18.000..−10.000 locales. Se abre sola con un
vehículo propio a ≤ 20 m (180 ticks; D-N72 y la adenda de Nacho en decisiones-noche.md). Los sacos del muro de Marcos
van en 88885c (`_mWall`) y los HESCO en 50541c con el relleno en b4a080 (muestra de piezas): aquí se usan los mismos.
Lo que NO está en el código y va estimado (SIN VERIFICAR): hojas de 3,98 m (4 m menos las holguras de 1 cm), jambas de
0,9 m de ancho por fuera de los 8 m (así las hojas miden los 4 m pedidos y la jamba tapa el final del muro de sacos) y
2,4 m de alto, y el diseño de cada hoja (de memoria: puertas de obra y de recinto militar, sin foto).
Disposición: origen en el suelo, en el centro de la puerta. El muro va a lo largo de X; −Y es FUERA (el sur).
Jerarquía (mira a +Z en Unity):
  SM_…_LODn          jambas, travesaño y remates (lo fijo)
  ├─ Hoja_O          hoja oeste (−X). Origen en su bisagra (x = −3,99), la hoja hacia +X local. Abre hacia fuera (−Y)
  │                  girando −90° sobre la vertical.
  └─ Hoja_E          hoja este (+X). Origen en su bisagra (x = +3,99), la hoja hacia −X local. Abre hacia fuera
                     girando +90° sobre la vertical.
Variantes (TBD_VARIANTE % 5): 0 marco de tubo con chapa abajo y barrotes, jambas de sacos; 1 chapa ondulada, jambas
HESCO; 2 improvisada de tablones con flejes, jambas de sacos; 3 malla con remate de alambre de espino, jambas HESCO;
4 blindada de chapa gruesa, pilares de hormigón con pórtico.
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = ["sacos", "hesco", "relleno", "hormigon", "marco", "chapa", "tablon", "fleje", "malla", "alambre", "placa"]
COLORES = {"sacos": "88885c", "hesco": "50541c", "relleno": "b4a080", "hormigon": "b8b8b8", "marco": "6c7874",
           "chapa": "94948c", "tablon": "b4a080", "fleje": "48403c", "malla": "70784c", "alambre": "48403c",
           "placa": "d4d0cc"}
FACCION = "propio"
PRESUPUESTO = {0: (150, 3000), 1: (75, 1500)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
NORMALES_ARRIBA = True                   # hojas y jambas casi verticales: con la luz cenital caían a media luz
NORMALES_INCLINACION = 0.5
BISAGRA = 3.99
ALTO = 2.1
PARTES = ["puerta", "Hoja_O", "Hoja_E"]
PIV = {"Hoja_O": (-BISAGRA, 0.0, 0.0), "Hoja_E": (BISAGRA, 0.0, 0.0)}
GIRO_PRUEBA = {"Hoja_O": ("Z", -60), "Hoja_E": ("Z", 60)}      # en la captura animable_giro, entreabiertas hacia fuera


def _jamba_sacos(m, xc, fino):
    filas = 9 if fino else 5
    h = 2.4 / filas
    for k in range(filas):
        z = h * k + h / 2
        if k % 2 == 0:
            for y in (-0.3, 0.3):
                m.caja((xc, y, z), (0.88, 0.56, h - 0.02), "sacos")
        else:
            for dx in (-0.23, 0.23):
                m.caja((xc + dx, 0, z), (0.42, 1.16, h - 0.02), "sacos")


def _jamba_hesco(m, xc, fino):
    m.caja((xc, 0, 1.05), (0.9, 1.2, 2.1), "hesco")
    m.caja((xc, 0, 2.115), (0.8, 1.1, 0.03), "relleno")
    if fino:                                           # malla del gavión en las caras de fuera y de dentro
        for s in (-1, 1):
            for dx in (-0.3, 0.0, 0.3):
                m.caja((xc + dx, s * 0.61, 1.05), (0.03, 0.02, 2.06), "fleje")


def _hoja(m, s, a, fino):
    """Hoja desde la bisagra (x = s·BISAGRA) hasta el centro (x = s·0,01). s = −1 oeste, +1 este."""
    x0, x1 = s * BISAGRA, s * 0.01
    xc, L = (x0 + x1) / 2, abs(x1 - x0)
    z0 = 0.06                                          # holgura con el suelo
    zc = z0 + ALTO / 2
    g = 0.12 if fino else 0.16
    if a == 4:                                         # blindada
        m.caja((xc, 0, zc), (L, 0.2, ALTO), "chapa")
        for z in (0.5, 1.15, 1.8):
            m.caja((xc, -0.13, z0 + z), (L - 0.2, 0.06, 0.14), "marco")      # nervios por fuera
        m.caja((x0 - s * 0.12, 0, zc), (0.16, 0.24, ALTO), "marco")          # bisagra
        return
    # marco de la hoja (largueros y travesaños)
    for x in (x0 - s * g / 2, x1 + s * g / 2):
        m.caja((x, 0, zc), (g, 0.12, ALTO), "marco")
    for z in (z0 + g / 2, z0 + ALTO - g / 2):
        m.caja((xc, 0, z), (L - 2 * g - 0.02, 0.12, g), "marco")
    dentro = L - 2 * g - 0.04
    hz = ALTO - 2 * g - 0.04
    if a == 0:                                         # chapa abajo y barrotes arriba, con riostra
        m.caja((xc, 0, z0 + g + 0.5), (dentro, 0.05, 0.98), "chapa")
        n = 8 if fino else 5
        for k in range(n):
            x = x0 - s * (g + (k + 0.5) * dentro / n + 0.02)
            m.caja((x, 0, z0 + g + 1.0 + (hz - 1.0) / 2 + 0.01), (0.06 if fino else 0.1, 0.06, hz - 1.0), "marco")
        if not fino:                                   # a tamaño de juego los barrotes eran rayas: malla en su lugar
            m.caja((xc, 0, z0 + g + 1.0 + (hz - 1.0) / 2 + 0.01), (dentro, 0.03, hz - 1.0), "malla")
        m.barra((x0 - s * g, -0.07, z0 + g + 1.0), (x1 + s * g, -0.07, z0 + ALTO - g), 0.1, 0.06, "marco")
    elif a == 1:                                       # chapa ondulada
        m.caja((xc, 0, zc), (dentro, 0.05, hz), "chapa")
        if fino:
            n = int(dentro / 0.25)
            for k in range(n):
                m.caja((x0 - s * (g + 0.12 + k * dentro / n), -0.04, zc), (0.06, 0.03, hz - 0.02), "marco")
        m.caja((xc, -0.08, zc), (dentro, 0.05, 0.12), "marco")               # travesaño de en medio
    elif a == 2:                                       # tablones con flejes
        n = 5
        h = hz / n
        for k in range(n):
            m.caja((xc, 0, z0 + g + 0.02 + h * k + h / 2), (dentro, 0.07, h - 0.04), "tablon")
        for sx in (-1, 1):
            m.barra((xc - L * 0.35, -0.06, z0 + g + 0.1 if sx > 0 else z0 + ALTO - g - 0.1),
                    (xc + L * 0.35, -0.06, z0 + ALTO - g - 0.1 if sx > 0 else z0 + g + 0.1), 0.1, 0.03, "fleje")
    else:                                              # malla con alambre de espino arriba
        m.caja((xc, 0, zc), (dentro, 0.03, hz), "malla")
        m.caja((xc, -0.06, zc), (dentro, 0.05, 0.1), "marco")
        n = 6 if fino else 4
        for k in range(n):                             # alambre en zigzag sobre el travesaño
            xa = x0 - s * (k * L / n)
            xb = x0 - s * ((k + 0.5) * L / n)
            xc2 = x0 - s * ((k + 1) * L / n)
            zt = z0 + ALTO
            w = 0.04 if fino else 0.08
            m.barra((xa, 0, zt + 0.02), (xb, 0, zt + 0.35), w, w, "alambre")
            m.barra((xb, 0.03, zt + 0.35), (xc2, 0.03, zt + 0.02), w, w, "alambre")


def construir(nombre="SM_PuertaVehiculos", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    a = ASPECTO
    # ---- jambas por fuera de los 8 m (x 4,0..4,9): tapan el final del muro de sacos
    for s in (-1, 1):
        xc = s * 4.45
        if a in (0, 2):
            _jamba_sacos(m, xc, fino)
        elif a in (1, 3):
            _jamba_hesco(m, xc, fino)
        else:
            m.caja((xc, 0, 2.6), (0.9, 1.2, 5.2), "hormigon")
    if a == 3 and fino:                                # alambre de espino sobre las jambas
        for s in (-1, 1):
            m.cilindro((s * 4.45, 0, 2.3), 0.25, 0.9, "X", 8, "alambre")
    if a == 4:                                         # pórtico con placa
        m.caja((0, 0, 4.9), (9.8, 0.6, 0.5), "hormigon")
        m.caja((0, -0.32, 4.9), (3.0, 0.04, 0.36), "placa")
    # ---- hojas (piezas animables)
    m.parte("Hoja_O")
    _hoja(m, -1, a, fino)
    m.parte("Hoja_E")
    _hoja(m, 1, a, fino)
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    # origen en el centro de la puerta, en el suelo (no en el centro de la caja envolvente)
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
