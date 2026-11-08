"""Hangar de mantenimiento de helicópteros del aeródromo de Bétera (D-N173; Álvaro, betera-nivel.md §5.2: H1 y H2 de
30 × 24 m, con el portón a +Z). Teo (3), 8-nov.

Sin foto de Bétera (aerodromos.md: «hueco, sin foto libre de los hangares»). Tipo genérico de hangar militar de chapa:
nave de bóveda rebajada sobre muros de 6 m, portón corredero de dos hojas en el testero. Medidas SIN VERIFICAR (de
memoria): 30 m de luz (X), 24 m de fondo (Y), muros de 6 m y clave a 10 m; hueco del portón de 24 × 7 m.
Colores: cubierta 94948c con nervios 88885c (arriba claro), muros 70784c (militar), portón 6c7874 con bandas 605c48,
interior 605c48 (con 48403c el portón abierto caía en la banda de la horda), puerta peatonal 543820, sacos 605c48, red 50541c. Nada rojo ni ámbar.
Disposición: origen en el suelo, en el centro de la planta; el portón en −Y de Blender (= +Z de Unity con yaw 0).
Jerarquía: SM_…_LODn (la nave) con Porton_O y Porton_E (origen en el centro de cada hoja cerrada). ABRIR = trasladar
cada hoja 12 m hacia fuera en X local (O hacia −X, E hacia +X); corren por delante del testero.
Variantes (TBD_VARIANTE % 5):
  0 bóveda de chapa, portón cerrado.
  1 bóveda, portón entreabierto (se ve el interior oscuro).
  2 nave a dos aguas (otro modelo de hangar), portón cerrado.
  3 bóveda castigada: chapas arrancadas en la cubierta y una hoja descolgada.
  4 bóveda con red de camuflaje por encima y sacos a los lados del portón.
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
PIEZAS = ["cubierta", "nervio", "muro", "porton", "banda", "interior", "puerta", "sacos", "red", "zocalo"]
COLORES = {"cubierta": "94948c", "nervio": "88885c", "muro": "70784c", "porton": "6c7874", "banda": "605c48",
           "interior": "605c48", "puerta": "543820", "sacos": "605c48", "red": "50541c", "zocalo": "94948c"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.5
PRESUPUESTO = {0: (80, 3000), 1: (40, 1400)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
LUZ, FONDO, HM, HC = 30.0, 24.0, 6.0, 10.0
HUECO, HP = 24.0, 7.0
PARTES = ["nave", "Porton_O", "Porton_E"]
PIV = {"Porton_O": (-HUECO / 4, -FONDO / 2 - 0.25, 0.0), "Porton_E": (HUECO / 4, -FONDO / 2 - 0.25, 0.0)}
GIRO_PRUEBA = {}


def _perfil(n):
    """Sección de la nave en XZ (convexa, antihoraria vista desde −Y): muros de HM y bóveda rebajada hasta HC."""
    p = [(-LUZ / 2, 0.0), (LUZ / 2, 0.0)]
    if VAR == 2:                                     # dos aguas
        return [(-LUZ / 2, 0.0), (LUZ / 2, 0.0), (LUZ / 2, HM), (0.0, HC - 0.5), (-LUZ / 2, HM)]
    for k in range(n + 1):
        a = k / n                                    # de +X a −X por arriba
        x = LUZ / 2 * math.cos(math.pi * a)
        z = HM + (HC - HM) * math.sin(math.pi * a)
        p.append((x, z))
    return [(-LUZ / 2, 0.0), (LUZ / 2, 0.0)] + p[2:]


def construir(nombre="SM_Hangar", lod=0):
    v = VAR
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    g = 0.1 if fino else 0.34
    n = 10 if fino else 6
    perfil = _perfil(n)
    # cuerpo: muros (todo el perfil en «muro») y una cubierta un poco mayor por encima de HM
    m.loft(perfil, [(-FONDO / 2, 1.0, 1.0, 0.0), (FONDO / 2, 1.0, 1.0, 0.0)], "muro")
    techo = [(x * 1.012, max(z, HM - 0.3) + 0.12) for x, z in perfil if z >= HM - 0.01]
    techo = [(LUZ / 2 * 1.012, HM - 0.3)] + techo[1:-1] + [(-LUZ / 2 * 1.012, HM - 0.3)] if v != 2 else \
        [(LUZ / 2 * 1.02, HM - 0.25), (LUZ / 2 * 1.02, HM + 0.12), (0.0, HC - 0.38), (-LUZ / 2 * 1.02, HM + 0.12),
         (-LUZ / 2 * 1.02, HM - 0.25)]
    # la cubierta es una lámina: la hago como polígono convexo cerrado por abajo a HM−0,3
    m.loft(techo, [(-FONDO / 2 - 0.3, 1.0, 1.0, 0.0), (FONDO / 2 + 0.3, 1.0, 1.0, 0.0)], "cubierta")
    # nervios transversales de la cubierta (bandas más claras cada 4 m)
    for k in range(7 if fino else 4):
        y = -FONDO / 2 + 2 + k * (FONDO - 4) / (6 if fino else 3)
        if v == 3 and k in (2, 3):
            continue
        sec = [(x * 1.02, z + 0.06) for x, z in techo]
        m.loft(sec, [(y - (0.15 if fino else 0.3), 1.0, 1.0, 0.0), (y + (0.15 if fino else 0.3), 1.0, 1.0, 0.0)],
               "nervio")
    if v == 3:                                       # chapas arrancadas: huecos oscuros en la cubierta
        for x, y in ((-6.0, -1.0), (4.5, 2.5), (8.0, -4.0)):
            z = HM + (HC - HM) * math.cos(math.pi * x / LUZ) + 0.25 if v != 2 else HC
            m.caja((x, y, z), (3.0, 2.6, 0.12), "interior", rot_y=-math.atan2((HC - HM) * math.pi / LUZ *
                   math.sin(math.pi * x / LUZ), 1.0) * 1.0)
    # zócalo de hormigón y puerta peatonal en el costado −X
    m.caja((0, 0, 0.3), (LUZ + 0.1, FONDO + 0.1, 0.6), "zocalo")
    m.caja((-LUZ / 2 - 0.06, 6.0, 1.1), (0.08, 1.1, 2.2), "puerta")
    if fino:
        for y in (-6.0, 0.0):
            m.caja((-LUZ / 2 - 0.06, y, 4.6), (0.08, 3.0, 0.8), "interior")       # ventanas altas
    # hueco del portón (interior oscuro en el testero) y dintel
    m.caja((0, -FONDO / 2 - 0.05, HP / 2), (HUECO, 0.1, HP), "interior")
    m.caja((0, -FONDO / 2 - 0.12, HP + 0.25), (HUECO + 1.0, 0.24, 0.5), "banda")
    m.caja((0, -FONDO / 2 - 0.4, 0.04), (HUECO + 12.0, 0.2 if fino else 0.34, 0.08), "banda")   # carril
    if v == 4:                                       # sacos a los lados del portón y red sobre la cubierta
        for s in (-1, 1):
            m.caja((s * (HUECO / 2 + 1.2), -FONDO / 2 - 1.2, 0.6), (1.6, 1.4, 1.2), "sacos", rot_z=0.05 * s)
        for k in range(5):
            x = -12 + 6 * k
            z = HM + (HC - HM) * math.cos(math.pi * x / LUZ) + 0.45
            m.caja((x, 0, z), (5.2, FONDO * 0.8, 0.08), "red", rot_y=math.atan2((HC - HM) * math.pi / LUZ *
                   math.sin(math.pi * x / LUZ), 1.0))
    # hojas del portón
    for nom, s in (("Porton_O", -1), ("Porton_E", 1)):
        m.parte(nom)
        xc = s * HUECO / 4
        if v == 1:
            xc += s * 4.0                            # entreabierto
        caida = v == 3 and s > 0
        L = HUECO / 2 - 0.02
        rot = 0.08 if caida else 0.0
        m.caja((xc, -FONDO / 2 - 0.25, HP / 2 + 0.05 - (0.4 if caida else 0)), (L, 0.18, HP), "porton", rot_y=rot)
        nb = 5 if fino else 3
        for k in range(nb):
            z = 0.8 + k * (HP - 1.2) / (nb - 1)
            m.caja((xc, -FONDO / 2 - 0.36, z - (0.4 if caida else 0)),
                   (L - 0.2, 0.06, 0.14 if fino else 0.34), "banda", rot_y=rot)
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
