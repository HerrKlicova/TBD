"""Armas de infantería del Ejército de Tierra, como piezas aparte del soldado (plan de Lucía, 7-nov §2). Teo, 6-nov-2026.

Modeladas en el espacio del soldado en su pose de reposo (origen = pies del soldado, delante = −Y): en Unity se
instancian como hijas de la raíz del soldado con transform identidad y se pasan al hueso «brazos» conservando la
posición (el C90, al hueso «pecho»). Ver soldado.py.
Variantes (TBD_VARIANTE % 5), cada una con su silueta a 12–24 px (Lucía):
  0 HK G36E: culata, asa de transporte con visor (la joroba), cargador curvo; 1 m.
  1 G36E con AG36: lo mismo con el tubo grueso del lanzagranadas bajo el guardamanos.
  2 HK MG4E: más larga (1,03 m), cajón alto, caja de munición colgando y bípode desplegado hacia abajo.
  3 HK G28 (tirador selecto): la más larga (1,08 m aprox.), mira telescópica alta, cargador recto, sin asa.
  4 Instalaza C90: tubo de 0,84 m a la espalda, en diagonal (hueso «pecho»).
Medidas de catálogo de memoria (SIN VERIFICAR hasta las fotos de Paula de referencias-reales/militar/).
Color único 48403c (pavonado); el AG36 y la caja de munición en 605c48 y el C90 en 50541c, para que se separen.
"""
import os
from mathutils import Vector
from soldado import Cuerpo, MANOS

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
TIPO = VAR % 5
NOMBRES = ("G36E", "G36E_AG36", "MG4E", "G28", "C90")
PIEZAS = ["arma", "equipo", "tubo"]
COLORES = {"arma": "48403c", "equipo": "605c48", "tubo": "50541c"}
FACCION = "propio"
PRESUPUESTO = {0: (10, 200), 1: (10, 120)}
UMBRAL_CONTORNO = {0: 0.2, 1: 9.0}


def piezas_arma(c, tipo, fino):
    """Construye el arma en el Cuerpo c (en el hueso activo)."""
    (xr, yr, zr), (xl, yl, zl) = MANOS
    d = Vector((xl - xr, yl - yr, zl - zr)).normalized()

    def p(t, dz=0.0, dx=0.0):
        return (xr + d.x * t + dx, yr + d.y * t, zr + d.z * t + dz)
    g = 1.0 if fino else 1.4          # LOD1: piezas finas algo más gruesas para que no desaparezcan
    if tipo == 4:   # C90 a la espalda, en diagonal
        c.barra((0.16, 0.25, 0.92), (-0.12, 0.27, 1.62), 0.10, 0.10, "tubo")
        c.barra((0.13, 0.25, 0.99), (0.09, 0.25, 1.07), 0.13, 0.13, "tubo")        # aro trasero
        return
    if tipo == 2:   # MG4E
        c.barra(p(-0.30), p(0.04), 0.06, 0.12, "arma")
        c.barra(p(0.0, 0.03), p(0.40, 0.03), 0.08, 0.13, "arma")
        c.barra(p(0.40, 0.02), p(0.74, 0.02), 0.035 * g, 0.035 * g, "arma")
        c.barra(p(0.10, 0.13), p(0.30, 0.13), 0.03, 0.04, "arma")                   # tapa y alza
        c.caja(p(0.17, -0.12), (0.10, 0.13, 0.12), "equipo")                        # caja de munición
        c.barra(p(0.62, 0.0, 0.03), p(0.70, -0.20, 0.06), 0.025 * g, 0.025 * g, "arma")   # bípode
        c.barra(p(0.62, 0.0, -0.03), p(0.70, -0.20, -0.06), 0.025 * g, 0.025 * g, "arma")
    elif tipo == 3:  # G28
        c.barra(p(-0.32), p(0.04), 0.05, 0.13, "arma")
        c.barra(p(0.0, 0.02), p(0.44, 0.02), 0.06, 0.08, "arma")
        c.barra(p(0.44, 0.02), p(0.80, 0.02), 0.03 * g, 0.03 * g, "arma")
        c.barra(p(0.00, 0.11), p(0.30, 0.11), 0.055, 0.055, "arma")                 # mira
        c.barra(p(0.12, 0.05), p(0.16, 0.05), 0.03, 0.08, "arma")                   # anillas
        c.barra(p(0.16, -0.02), p(0.17, -0.13), 0.04, 0.05, "arma")                 # cargador recto
    else:            # G36E (con o sin AG36)
        c.barra(p(-0.28), p(0.04), 0.045, 0.11, "arma")
        c.barra(p(0.0, 0.02), p(0.42, 0.02), 0.06, 0.09, "arma")
        c.barra(p(0.42, 0.02), p(0.60, 0.02), 0.03 * g, 0.03 * g, "arma")
        c.barra(p(0.02, 0.11), p(0.24, 0.11), 0.035, 0.06, "arma")                  # asa-visor
        c.barra(p(0.17, -0.03), p(0.22, -0.17), 0.04, 0.06, "arma")                 # cargador curvo
        if tipo == 1:
            c.barra(p(0.28, -0.07), p(0.56, -0.07), 0.065, 0.065, "equipo")         # AG36


def construir(nombre="SM_Arma", lod=0):
    c = Cuerpo(PIEZAS)
    c.hueso("brazos" if TIPO != 4 else "pecho")
    piezas_arma(c, TIPO, lod == 0)
    return c.objeto(nombre + "_" + NOMBRES[TIPO])
