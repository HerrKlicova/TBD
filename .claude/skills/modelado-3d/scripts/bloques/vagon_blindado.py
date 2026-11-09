# Entregar con: --costado 88885c --claro b8b8b8 (D-N157, como el R2). Sin esos dos, el lateral sale en 50541c (S3, 9-nov).
"""R3 · Vagón blindado: el tren dispara (D-N189, Lucía 9-nov 22:25; GDD §15c). Teo (4), 9-nov, noche de libre albedrío.

Parte del R2 · Vagón de tropa (vagon_tropa_r2.py): la misma plataforma de cuatro ejes tipo UIC (Rgs) con las mismas cotas
SIN VERIFICAR (19,9 m entre topes, tablero de 18,5 × 2,9 m a 1,25 m, bogies a ±7 m, ruedas de 0,92 m). Así engancha igual que
el R2 y ocupa una plaza de carga. El taller le sube el parapeto a una casamata cerrada de chapa de 2,1 m, techada en los dos
tercios de los extremos, con aspilleras a dos alturas y una torreta giratoria en el centro con una ametralladora pesada
tras un escudo (tipo MG de 12,7 mm, de memoria: SIN VERIFICAR).
Jerarquía del FBX (todo mira a +Z en Unity, como siempre):
  SM_R3_VagonBlindado_LODn   casco (origen en el suelo, en el centro del vagón)
  └─ Torre                   anillo, escudo y ametralladora; origen en el eje de giro (0; 3,45 m; 0 en Unity); gira sobre
                             su Y de Unity. Sin elevación: el tiro va por el plano (los zombis están en el suelo).
     └─ Muzzle_0             vacío en la boca de la ametralladora; el disparo sale por su +Z local (regla de bocas).
Colores (D-N80, nada bajo L* 28; nada rojo ni ámbar): laterales 88885c y techo b8b8b8 (los del R2), bastidor, ruedas,
aspilleras y arma en 48403c (el R2 viejo usa 303424 y 1c200c, que caen en la banda de la horda), remiendos 605c48,
escudo 70784c, sacos 847c34, traviesas 845c28, red 70784c y bidones 6c7874.
Variantes (TBD_VARIANTE % 5):
  0 Chapa: casamata de chapa soldada lisa, con remiendos.
  1 Traviesas: chapa forrada por fuera con traviesas de madera atadas (taller pobre, sin acero).
  2 Faldones: con faldones de chapa que tapan los bogies y sacos en el techo alrededor de la torreta.
  3 Castigado: chapas abolladas y una arrancada, escudo doblado (se ve que ya ha aguantado una emboscada).
  4 Camuflado: red por encima de los techos y bidones atados en los testeros.
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 5
NOMBRES_V = ["Chapa", "Traviesas", "Faldones", "Castigado", "Camuflado"]
PIEZAS = ["casco", "bastidor", "rueda", "optica", "tablero", "chapa", "metal", "sacos", "escudo", "arma", "traviesa",
          "red", "bidon"]
COLORES = {"casco": "88885c", "bastidor": "48403c", "rueda": "48403c", "optica": "48403c", "tablero": "605c48",
           "chapa": "605c48", "metal": "48403c", "sacos": "847c34", "escudo": "70784c", "arma": "48403c",
           "traviesa": "845c28", "red": "70784c", "bidon": "6c7874"}
ABAJO_CASCO = "605c48"           # caras de abajo del casco (por defecto caían en 442c0c)
FACCION = "propio"
PRESUPUESTO = {0: (60, 3500), 1: (40, 1400)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
L, ZT, HC, EP = 19.9, 1.25, 2.1, 0.08
Y0 = L / 2
ZTECHO = ZT + HC
PARTES = ["casco", "Torre"]
PIV = {"Torre": (0.0, 0.0, ZTECHO)}


def BOCAS(lod, raiz, objs):
    """Boca de la ametralladora: centro de la cara más adelantada (−Y) del cañón, disparo hacia −Y, hija de la Torre."""
    from mathutils import Vector
    h = next((x for x in objs if x.name.split(".")[0] == "Torre"), None)
    if h is None:
        return []
    vs = [h.matrix_world @ v.co for v in h.data.vertices]
    ymin = min(v.y for v in vs)
    cara = [v for v in vs if v.y < ymin + 0.02]
    c = sum(cara, Vector()) / len(cara)
    return [("Muzzle_0", c, (0.0, -1.0, 0.0), "Torre")]


def construir(nombre="SM_R3_VagonBlindado", lod=0):
    v = VAR
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    F = (lambda *a, **k: m.caja(*a, **k)) if fino else (lambda *a, **k: None)
    # --- plataforma del R2: tablero, larguero, testeros, topes y bogies
    m.caja((0, 0, ZT - 0.15), (2.9, 18.5, 0.3), "tablero")
    m.caja((0, 0, ZT - 0.5), (2.6, 18.5, 0.4), "bastidor")
    for s in (-1, 1):
        m.caja((0, s * 9.35, ZT - 0.35), (2.9, 0.3, 0.5), "bastidor")
        for x in (-0.9, 0.9):
            m.cilindro((x, s * (Y0 - 0.2), 1.06), 0.2, 0.4, "Y", 8, "metal")
        yb = s * 7.0
        for x in (-1.0, 1.0):
            m.caja((x, yb, 0.6), (0.2, 2.8, 0.35), "bastidor")
        for e in (-1.0, 1.0):
            for x in (-0.83, 0.83):
                m.cilindro((x, yb + e, 0.46), 0.46, 0.13, "X", 12 if fino else 8, "rueda")
    # --- casamata: costados y testeros de chapa de 2,1 m (puerta en cada testero)
    zc = ZT + HC / 2
    for x in (-1.0, 1.0):
        m.caja((x * (1.45 - EP / 2), 0, zc), (EP, 18.5, HC), "casco")
    for s in (-1, 1):
        for x in (-1.0, 1.0):
            m.caja((x * (0.45 + 1.45 - EP) / 2, s * (9.25 - EP / 2), zc), (1.45 - EP - 0.45, EP, HC), "casco")
        m.caja((0, s * (9.25 - EP / 2), ZT + HC - 0.25), (0.9, EP, 0.5), "casco")          # dintel de la puerta
        F((0, s * (9.25 - EP / 2 + 0.01 * s), ZT + 0.85), (0.86, 0.02, 1.7), "optica")      # puerta (hueco oscuro)
    # techos de los dos tercios de los extremos; en el centro, el pozo de la torreta (4,4 m abierto)
    for s in (-1, 1):
        m.caja((0, s * (2.2 + 7.05 / 2), ZTECHO + 0.04), (2.9, 7.05, 0.08), "casco")
    for x in (-1.0, 1.0):                                                                    # brocal del pozo
        m.caja((x * 1.3, 0, ZTECHO + 0.15), (0.3, 4.4, 0.3), "casco")
    # aspilleras a dos alturas (de pie y de rodillas), solo LOD0 en las dos caras; en el LOD1 una banda por costado
    for x in (-1.0, 1.0):
        if fino:
            for k in range(12):
                y = -8.0 + k * 1.45
                if abs(y) < 0.8:
                    continue
                for z in (ZT + 1.55, ZT + 0.75):
                    for cara in (1, -1):
                        m.caja((x * (1.45 - EP / 2) + cara * (EP / 2 + 0.005), y, z), (0.01, 0.4, 0.12), "optica")
        else:
            m.caja((x * (1.45 + 0.005), 0, ZT + 1.55), (0.01, 16.0, 0.14), "optica")
    # --- variantes del forro
    if v == 0:
        for x, y in ((-1.0, -3.4), (1.0, 4.6), (1.0, -6.2)):
            m.caja((x * 1.5, y, ZT + 1.1), (0.03, 1.6, 1.0), "chapa")
    if v == 1:                                                                               # traviesas atadas
        for x in (-1.0, 1.0):
            for k in range(9 if fino else 4):
                y = -8.4 + k * (16.8 / (8 if fino else 3))
                if abs(y) < 0.9:
                    continue
                m.caja((x * 1.55, y, ZT + 0.95), (0.14, 2.6 if fino else 3.6, 0.24), "traviesa")
                F((x * 1.55, y, ZT + 0.45), (0.14, 2.6, 0.24), "traviesa")
            for k in range(4):
                F((x * 1.6, -6.0 + k * 4.0, ZT + 0.8), (0.04, 0.08, 1.3), "metal")         # flejes
    if v == 2:                                                                               # faldones y sacos
        for x in (-1.0, 1.0):
            for s in (-1, 1):
                m.caja((x * 1.5, s * 7.0, 0.75), (0.06, 3.4, 0.8), "chapa")
        for k in range(6):
            a = k * math.pi / 3 + 0.5
            m.caja((math.cos(a) * 1.0, math.sin(a) * 1.6 + (0 if abs(math.sin(a)) < 0.5 else 0), ZTECHO + 0.45),
                   (0.6, 0.4, 0.3), "sacos", rot_z=a)
    if v == 3:                                                                               # castigado
        for x, y, rz in ((-1.0, 2.8, 0.0), (1.0, -5.2, 0.0)):
            m.caja((x * 1.5, y, ZT + 1.3), (0.04, 1.3, 1.1), "chapa", rot_x=0.08)          # abolladuras
        m.caja((1.62, -2.0, 0.55), (0.05, 1.5, 1.0), "casco", rot_y=0.5)                    # chapa colgando
        F((1.46, -2.0, ZT + 1.1), (0.02, 1.4, 0.9), "optica")                                # hueco
    if v == 4:                                                                               # red y bidones
        for s in (-1, 1):
            m.caja((0, s * 5.8, ZTECHO + 0.2), (3.1, 6.2, 0.06), "red", rot_x=0.02 * s)
            for x in (-0.95, 0.95):
                m.cilindro((x, s * (Y0 - 0.35), ZT + 0.45), 0.29, 0.9, "Z", 10 if fino else 6, "bidon")
    # sacos en las esquinas del techo (todas)
    for s in (-1, 1):
        for x in (-1.0, 1.0):
            m.caja((x * 1.05, s * 8.7, ZTECHO + 0.25), (0.7, 0.9, 0.4), "sacos")
    # --- Torre: anillo, escudo y ametralladora (el cañón hacia −Y)
    m.parte("Torre")
    m.cilindro((0, 0, ZTECHO + 0.2), 0.95, 0.25, "Z", 12 if fino else 8, "escudo")
    m.caja((0, 0, ZTECHO + 0.55), (0.35, 0.35, 0.5), "arma")                                 # pedestal
    m.caja((0, -0.25, ZTECHO + 0.85), (0.22, 1.0, 0.22), "arma")                            # cajón del arma
    m.cilindro((0, -1.25, ZTECHO + 0.88), 0.045 if fino else 0.08, 1.2, "Y", 8 if fino else 6, "arma")   # cañón
    F((0.22, -0.2, ZTECHO + 0.8), (0.2, 0.3, 0.22), "escudo")                                # caja de cinta
    curva = 0.25 if v == 3 else 0.0
    for k, a in enumerate((-0.45, 0.0, 0.45)):                                               # escudo de tres planchas
        m.caja((math.sin(a) * 0.75, -0.55 - (1 - math.cos(a)) * 0.0 + abs(math.sin(a)) * 0.25, ZTECHO + 0.95),
               (0.7, 0.05, 0.9 if k != 2 else 0.9 - curva), "escudo", rot_z=a, rot_x=curva if k == 2 else 0.0)
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    return objs
