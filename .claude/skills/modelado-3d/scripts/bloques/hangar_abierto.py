"""Hangar de Bétera en el que se entra (núcleo de Eugenio, 9-nov 21:59: «tú te metes al aeródromo, ahí están los
helicópteros, los seleccionas…»). Teo (4), 9-nov, noche de libre albedrío.

El hangar de hangar.py (8-nov) es un bloque macizo con el hueco del portón pintado: desde la cámara del RTS no se puede ver
dentro. Este es el mismo hangar (40 × 25 m, muros de 8,5 m, clave a 12 m, hueco de 32 × 8 m, mismas medidas SIN VERIFICAR y
mismos colores) pero HUECO: muros de 0,3 m, testero con el hueco abierto, suelo de hormigón con dos puestos para NH90
(rotor de unos 16,3 m) y el interior amueblado. Mismo sitio en betera_instancias.csv (SM_Hangar, X 90 y 160, Z 40).
Jerarquía: SM_…_LODn (muros, suelo e interior) con tres hijos:
  Cubierta  bóveda, nervios y red. Origen en (0, 0, 8,5 m). La vista la apaga (o la funde) al entrar en el hangar o al
            acercar la cámara, para ver los helicópteros desde arriba. Encendida, el hangar se ve igual que el cerrado.
  Porton_O / Porton_E  las hojas, ABIERTAS: corridas 16 m hacia fuera por delante del testero. CERRAR = trasladar cada
            hoja 16 m hacia el centro en X local (al revés que en hangar.py, que se entrega cerrado).
Puestos de los NH90 (centro del círculo, coordenadas de Unity con yaw 0): (−10; 0; +1) y (+10; 0; +1), morro a +Z
(hacia el portón). Quedan libres en todas las variantes: el mobiliario va pegado al fondo y a los costados.
Colores: cubierta 94948c, nervios 88885c, muros 70784c, portón 6c7874 con bandas 605c48, suelo 94948c con marcas b8b8b8
(finas: el d4d0cc es la selección), metal 6c7874, madera 845c28, cajas 70784c, lona b4a080, sacos 605c48. Nada rojo ni ámbar.
Disposición: origen en el suelo, en el centro; el portón en −Y de Blender (= +Z de Unity con yaw 0).
Variantes (TBD_VARIANTE % 5), la nave de hangar.py con el interior según el uso:
  0 Taller: bóveda; bancos de trabajo, estanterías, carros de herramientas y plataforma de mantenimiento.
  1 Almacen: bóveda; palés de cajas de munición, bidones y un contenedor al fondo.
  2 Refugio: nave a dos aguas; literas, mesas y sacos (los pilotos y la tropa duermen aquí).
  3 Castigado: bóveda con chapas arrancadas; chapas caídas en el suelo y estanterías volcadas, con los puestos libres.
  4 Fortificado: bóveda con red de camuflaje; sacos a los lados del portón por dentro y por fuera, y cajas al fondo.
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Taller", "Almacen", "Refugio", "Castigado", "Fortificado"]
PIEZAS = ["cubierta", "nervio", "muro", "porton", "banda", "interior", "puerta", "sacos", "red", "zocalo", "suelo",
          "marca", "metal", "madera", "caja", "lona"]
COLORES = {"cubierta": "94948c", "nervio": "88885c", "muro": "70784c", "porton": "6c7874", "banda": "605c48",
           "interior": "605c48", "puerta": "605c48", "sacos": "605c48", "red": "88885c", "zocalo": "94948c",
           "suelo": "94948c", "marca": "b8b8b8", "metal": "6c7874", "madera": "845c28", "caja": "70784c",
           "lona": "b4a080"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.5
PRESUPUESTO = {0: (80, 6000), 1: (40, 2500)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
LUZ, FONDO, HM, HC = 40.0, 25.0, 8.5, 12.0
HUECO, HP, E = 32.0, 8.0, 0.3
PARTES = ["nave", "Cubierta", "Porton_O", "Porton_E"]
PIV = {"Cubierta": (0.0, 0.0, HM), "Porton_O": (-HUECO / 4 - 16.0, -FONDO / 2 - 0.25, 0.0),
       "Porton_E": (HUECO / 4 + 16.0, -FONDO / 2 - 0.25, 0.0)}
GIRO_PRUEBA = {}
PUESTOS = ((-10.0, -1.0), (10.0, -1.0))          # en Blender (Y = −Z de Unity)


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def _boveda(x):
    u = max(-0.98, min(0.98, 2 * x / LUZ))
    r = math.sqrt(1 - u * u)
    return HM + (HC - HM) * r + 0.18, -(HC - HM) * u / r * (2 / LUZ)


def _perfil(n):
    if VAR == 2:
        return [(-LUZ / 2, 0.0), (LUZ / 2, 0.0), (LUZ / 2, HM), (0.0, HC - 0.5), (-LUZ / 2, HM)]
    p = []
    for k in range(n + 1):
        a = k / n
        p.append((LUZ / 2 * math.cos(math.pi * a), HM + (HC - HM) * math.sin(math.pi * a)))
    return [(-LUZ / 2, 0.0), (LUZ / 2, 0.0)] + p


def _banco(m, x, y, rz, fino):
    m.caja((x, y, 0.9), (2.0, 0.8, 0.08), "madera", rot_z=rz)
    m.caja((x, y, 0.45), (1.9, 0.7, 0.9) if not fino else (1.9, 0.05, 0.9), "metal", rot_z=rz)
    if fino:
        m.caja((x - 0.5, y, 1.05), (0.4, 0.3, 0.25), "caja", rot_z=rz)


def _estanteria(m, x, y, rz, fino, caida=False):
    if caida:
        m.caja((x, y, 0.25), (2.4, 2.0, 0.5), "metal", rot_z=rz)
        for k in range(3 if fino else 1):
            m.caja((x + 0.6 * k - 0.6, y + 1.3, 0.15), (0.5, 0.4, 0.3), "caja", rot_z=rz + k)
        return
    m.caja((x, y, 1.2), (2.4, 0.6, 2.4) if not fino else (2.4, 0.6, 0.06), "metal", rot_z=rz)
    if fino:
        for z in (0.3, 1.1, 1.9):
            m.caja((x, y, z), (2.4, 0.6, 0.05), "metal", rot_z=rz)
            m.caja((x, y, z + 0.2), (2.0, 0.45, 0.35), "caja", rot_z=rz)


def _cajas(m, x, y, capas, fino, rz=0.0):
    m.caja((x, y, 0.07), (1.2, 1.0, 0.14), "madera", rot_z=rz)
    m.caja((x, y, 0.14 + capas * 0.2), (1.1, 0.9, capas * 0.4), "caja", rot_z=rz)
    if fino:
        for f in range(capas):
            m.caja((x, y, 0.14 + 0.4 * f + 0.4), (1.12, 0.92, 0.03), "banda", rot_z=rz)


def _bidones(m, x, y, fino):
    for i in range(2):
        for j in range(2):
            m.cilindro((x + (i - 0.5) * 0.62, y + (j - 0.5) * 0.62, 0.45), 0.29, 0.9, "Z", 8 if fino else 6, "caja")


def _litera(m, x, y, rz, fino):
    for z in (0.45, 1.45):
        m.caja((x, y, z), (0.9, 2.0, 0.12), "metal", rot_z=rz)
        m.caja((x, y, z + 0.1), (0.85, 1.9, 0.1), "lona", rot_z=rz)
    if fino:
        for sx in (-1, 1):
            for sy in (-1, 1):
                m.caja((x + sx * 0.43, y + sy * 0.97, 0.85), (0.05, 0.05, 1.7), "metal", rot_z=rz)


def _sacos(m, x, y, largo, alto, rz, fino):
    m.caja((x, y, alto / 2), (largo, 0.7, alto), "sacos", rot_z=rz)
    if fino:
        for k in range(int(alto / 0.3)):
            m.caja((x, y, 0.3 * (k + 1)), (largo + 0.02, 0.72, 0.03), "banda", rot_z=rz)


def _cubierta(m, perfil, v, fino, yf, yb):
    """Hijo Cubierta: lámina de la bóveda (o dos aguas), nervios, chapas arrancadas (v3) o red (v4)."""
    techo = [(x * 1.012, max(z, HM - 0.3) + 0.12) for x, z in perfil if z >= HM - 0.01]
    techo = [(LUZ / 2 * 1.012, HM - 0.3)] + techo[1:-1] + [(-LUZ / 2 * 1.012, HM - 0.3)] if v != 2 else \
        [(LUZ / 2 * 1.02, HM - 0.25), (LUZ / 2 * 1.02, HM + 0.12), (0.0, HC - 0.38), (-LUZ / 2 * 1.02, HM + 0.12),
         (-LUZ / 2 * 1.02, HM - 0.25)]
    m.loft(techo, [(yf - 0.3, 1.0, 1.0, 0.0), (yb + 0.3, 1.0, 1.0, 0.0)], "cubierta")
    for k in range(7 if fino else 4):
        y = yf + 2 + k * (FONDO - 4) / (6 if fino else 3)
        if v == 3 and k in (2, 3):
            continue
        sec = [(x * 1.02, z + 0.06) for x, z in techo]
        m.loft(sec, [(y - (0.15 if fino else 0.3), 1.0, 1.0, 0.0), (y + (0.15 if fino else 0.3), 1.0, 1.0, 0.0)],
               "nervio")
    if v == 3:
        for x, y in ((-8.0, -1.0), (6.0, 2.5), (10.5, -4.0)):
            z, pend = _boveda(x)
            m.caja((x, y, z + 0.3), (4.0, 3.0, 0.12), "interior", rot_y=-math.atan(pend))
    if v == 4:
        for k in range(5):
            x = -16 + 8 * k
            z, pend = _boveda(x)
            m.caja((x, 0, z + 0.45), (7.0, FONDO * 0.8, 0.08), "red", rot_y=-math.atan(pend))


def construir(nombre="SM_HangarAbierto", lod=0):
    v = VAR
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    n = 10 if fino else 6
    perfil = _perfil(n)
    yb, yf = FONDO / 2, -FONDO / 2
    # --- nave hueca: zócalo, suelo, muros laterales, fondo y testero con el hueco
    m.caja((0, 0, 0.03), (LUZ, FONDO, 0.06), "suelo")
    for s in (-1, 1):
        m.caja((s * (LUZ / 2 - E / 2), 0, HM / 2), (E, FONDO, HM), "muro")
        m.caja((s * (LUZ / 2 + 0.05), 0, 0.3), (0.12, FONDO + 0.1, 0.6), "zocalo")
    m.loft(perfil, [(yb - E, 1.0, 1.0, 0.0), (yb, 1.0, 1.0, 0.0)], "muro")                 # fondo
    for s in (-1, 1):                                                                        # testero a los lados
        w = LUZ / 2 - HUECO / 2
        m.caja((s * (HUECO / 2 + w / 2), yf + E / 2, HM / 2), (w, E, HM), "muro")
    alto = [(x, z) for x, z in perfil if z >= HP + 0.01]
    m.loft([(-LUZ / 2, HP), (LUZ / 2, HP)] + alto[2:] if v != 2 else
           [(-LUZ / 2, HP), (LUZ / 2, HP), (LUZ / 2, HM), (0.0, HC - 0.5), (-LUZ / 2, HM)],
           [(yf, 1.0, 1.0, 0.0), (yf + E, 1.0, 1.0, 0.0)], "muro")                           # dintel y frontón
    m.caja((0, yf - 0.12, HP + 0.25), (HUECO + 1.0, 0.24, 0.5), "banda")
    m.caja((0, yf - 0.4, 0.04), (2 * HUECO + 4, 0.2 if fino else 0.34, 0.08), "banda")      # carril
    m.caja((-LUZ / 2 - 0.06, 6.0, 1.1), (0.08, 1.1, 2.2), "puerta")
    # --- puestos de los NH90: círculo de 8 lados y línea de eje, en marca fina
    for px, py in PUESTOS:
        r = 8.2
        for k in range(8 if fino else 4):
            a = k * math.pi / (4 if fino else 2)
            lado = 2 * r * math.sin(math.pi / (8 if fino else 4))
            m.caja((px + math.cos(a + math.pi / 8) * r * 0.97, py + math.sin(a + math.pi / 8) * r * 0.97, 0.065),
                   (0.15 if fino else 0.34, lado, 0.01), "marca", rot_z=a + math.pi / 8)
        m.caja((px, py - 4.0, 0.065), (0.15 if fino else 0.34, 8.0, 0.01), "marca")
    # --- interior pegado al fondo (y > 9) y a los costados (|x| > 18,5): los puestos quedan libres
    yi = yb - E - 0.6
    if v == 0:                                                                               # taller
        for k, x in enumerate((-15.0, -11.0, 11.0, 15.0)):
            _banco(m, x, yi, 0.0, fino)
        for x in (-5.0, -2.2, 2.2, 5.0):
            _estanteria(m, x, yi - 0.1, 0.0, fino)
        for s in (-1, 1):
            m.caja((s * 18.8, -2.0, 0.5), (0.7, 1.0, 1.0), "metal")                          # carros
            m.caja((s * 18.8, 3.0, 1.6), (1.2, 2.4, 0.1), "metal")                           # plataforma
            for sy in (-1, 1):
                m.caja((s * 18.8, 3.0 + sy * 1.1, 0.8), (1.1, 0.06, 1.6), "metal")
            m.caja((s * 18.8, 1.2, 0.8), (0.9, 1.4, 0.06), "metal", rot_x=0.85)              # escalera
    elif v == 1:                                                                             # almacén
        for i in range(8):
            _cajas(m, -14.0 + i * 1.5, yi, 2 + (i % 2), fino)
            _cajas(m, -14.0 + i * 1.5, yi - 1.3, 1 + (i % 3 == 0), fino)
        _bidones(m, 6.0, yi, fino)
        _bidones(m, 7.5, yi, fino)
        m.caja((14.0, yi - 0.6, 1.3), (6.1, 2.45, 2.6), "caja")                              # contenedor de 20'
        if fino:
            for k in range(8):
                m.caja((11.2 + k * 0.8, yi - 1.84, 1.3), (0.1, 0.03, 2.4), "banda")
        for s in (-1, 1):
            _bidones(m, s * 18.8, 2.0, fino)
    elif v == 2:                                                                             # refugio
        for k in range(6):
            _litera(m, -15.0 + k * 1.3, yi - 0.7, 0.0, fino)
            _litera(m, 8.5 + k * 1.3, yi - 0.7, 0.0, fino)
        for x in (-3.0, 3.0):
            m.caja((x, yi - 1.2, 0.75), (2.2, 0.9, 0.06), "madera")
            m.caja((x, yi - 1.2, 0.37), (2.0, 0.08, 0.74) if fino else (2.0, 0.8, 0.74), "metal")
        for s in (-1, 1):
            for k in range(3):
                m.caja((s * 18.9, -6.0 + 2.2 * k, 0.15), (0.8, 1.9, 0.3), "lona")
    elif v == 3:                                                                             # castigado
        for k, (x, y, rz) in enumerate(((-13.0, yi, 0.2), (-4.0, yi - 0.4, -0.3), (12.0, yi, 0.5))):
            _estanteria(m, x, y, rz, fino, caida=True)
        for k, (x, y, rz) in enumerate(((-18.5, -4.0, 0.3), (18.0, 4.0, -0.6), (0.5, 10.0, 1.2), (6.0, 9.6, 0.4))):
            m.caja((x, y, 0.1 + 0.05 * k), (4.0, 1.2, 0.06), "cubierta", rot_z=rz, rot_x=0.08)   # chapas caídas
        _cajas(m, 16.0, yi, 1, fino, 0.6)
    else:                                                                                    # fortificado
        for s in (-1, 1):
            _sacos(m, s * (HUECO / 2 - 1.5), yf + 2.0, 3.0, 1.2, 0.0, fino)
            _sacos(m, s * (HUECO / 2 + 1.2), yf - 1.2, 1.6, 1.2, 0.05 * s, fino)
        for i in range(6):
            _cajas(m, -4.5 + i * 1.6, yi, 2, fino)
        for s in (-1, 1):
            _sacos(m, s * 18.9, 0.0, 6.0, 1.0, math.pi / 2, fino)
    # --- cubierta (hijo): lámina, nervios, chapas arrancadas o red
    m.parte("Cubierta")
    if os.environ.get("TBD_SIN_CUBIERTA"):          # solo para revisar el interior desde arriba (no se entrega así)
        m.caja((0, 0, HM + 0.5), (0.1, 0.1, 0.1), "nervio")
    else:
        _cubierta(m, perfil, v, fino, yf, yb)
    # --- hojas del portón, abiertas (16 m hacia fuera)
    for nom, s in (("Porton_O", -1), ("Porton_E", 1)):
        m.parte(nom)
        xc = s * (HUECO / 4 + 16.0)
        L = HUECO / 2 - 0.02
        m.caja((xc, yf - 0.25, HP / 2 + 0.05), (L, 0.18, HP), "porton")
        nb = 5 if fino else 3
        for k in range(nb):
            z = 0.8 + k * (HP - 1.2) / (nb - 1)
            m.caja((xc, yf - 0.36, z), (L - 0.2, 0.06, 0.14 if fino else 0.34), "banda")
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
