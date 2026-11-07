"""Paso a nivel con barreras animables, caseta y señales luminosas (frentes de Sagunto, D-N96). Teo, 7-nov-2026.

Fotos F3 de Paula (manifest_entorno.json): F3_paso_a_nivel_1 (Gata de Gorgos, FGV, Joanbanjo) y F3_paso_a_nivel_2
(Paterna, Camí de Campanar, Metrovalencia). Lo que se toma: semibarreras de brazo largo a rayas con el mecanismo en un
armario gris galvanizado, con el soporte acodado y el contrapeso; poste gris con dos focos redondos sobre placa negra y
viseras (F3_2 los lleva dobles, lado a lado); armarios de señalización grises a pie de vía.
Las rayas del brazo van en d4d0cc y 48403c: el rojo de la foto está vetado (982420 es el rojo de alerta del juego).
Los focos van apagados en 48403c y salen en los hijos «Luces_A» y «Luces_B» (focos izquierdo y derecho, que en la
realidad parpadean alternos), con el mismo origen que el paso, para que Marcos les dé emisión.
MEDIDAS SIN VERIFICAR (estimadas sobre las fotos): calzada de 7 m, brazo de 3,9 m a 1,0 m del suelo, armario de
0,45 × 0,5 × 1,1 m, poste de 2,8 m, focos de 0,26 m y caseta de 2,4 × 2,0 m.
Disposición: origen en el centro del cruce. La vía va a lo largo de Y (por x = 0) y la carretera a lo largo de X. Cada
semibarrera cierra su medio carril: la 1 en x = +4 (pivote en y = +3,85) y la 2 en x = −4 (pivote en y = −3,85).
Jerarquía (mira a +Z en Unity):
  SM_…_LODn                  armarios, postes, caseta y señales
  ├─ Barrera_1, Barrera_2    brazos BAJADOS (cerrado). Origen en el eje del brazo; los dos tienen el brazo hacia su −Y
  │                          local (la Barrera_2 va girada 180° sobre la vertical), así que los dos suben con el mismo
  │                          signo de giro sobre su X local (unos 85°).
  └─ Luces_A, Luces_B        focos para la emisión (transformación identidad)
Variantes (TBD_VARIANTE % 5): 0 caseta de obra con tejado de teja; 1 focos dobles en cada poste y caseta de ladrillo
de cubierta plana; 2 abandonado: la Barrera_2 partida (el trozo caído queda en el suelo) y una señal inclinada; 3 puesto
militar: sacos terreros y bloques de hormigón; 4 con poste de catenaria y farola, caseta metálica.
"""
import math, os
from mathutils import Matrix
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = ["armario", "raya_clara", "raya_oscura", "poste", "cabeza", "foco", "placa", "pared", "teja", "puerta",
          "ventana", "sacos", "hormigon", "metal"]
COLORES = {"armario": "94948c", "raya_clara": "d4d0cc", "raya_oscura": "48403c", "poste": "6c7874",
           "cabeza": "48403c", "foco": "48403c", "placa": "d4d0cc", "pared": "b4a080", "teja": "7c3c18",
           "puerta": "605c48", "ventana": "60546c", "sacos": "847c34", "hormigon": "b8b8b8", "metal": "605c48"}
if ASPECTO == 1:
    COLORES = dict(COLORES, pared="6c4038")                # ladrillo (8c5048 es de la familia roja, vetada)
if ASPECTO == 4:
    COLORES = dict(COLORES, pared="94948c", teja="6c7874")
FACCION = "neutro"
PRESUPUESTO = {0: (100, 3000), 1: (50, 1500)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
PIV1, PIV2 = (4.0, 3.85, 1.0), (-4.0, -3.85, 1.0)
PARTES = ["paso", "Barrera_1", "Barrera_2", "Luces_A", "Luces_B"]
PIV = {"Barrera_1": PIV1, "Barrera_2": PIV2, "Luces_A": (0, 0, 0), "Luces_B": (0, 0, 0)}
LARGO = 3.9


def _brazo(m, piv, s, largo, fino, roto=False):
    """Brazo desde el pivote hacia s·(−Y): rayas alternas, contrapeso detrás y soporte acodado (F3_1)."""
    x, y, z = piv
    paso = 0.45 if fino else 0.65
    n = max(1, int(round(largo / paso)))
    d = largo / n
    for k in range(n):
        y0 = y - s * (0.1 + k * d + 0.01)              # 2 cm entre rayas: si se tocan, limpiar() funde los
        y1 = y - s * (0.1 + (k + 1) * d - 0.01)        # vértices y quedan aristas no manifold
        m.barra((x, y0, z), (x, y1, z), 0.12 if fino else 0.22, 0.1 if fino else 0.2,
                "raya_clara" if k % 2 == 0 else "raya_oscura")
    m.caja((x, y + s * 0.35, z), (0.16, 0.5, 0.2), "raya_oscura")                          # contrapeso
    m.cilindro((x, y, z), 0.11, 0.3, "X", 8, "armario")                                     # eje


def _senal(m, c, cara, doble, fino, inclinada=0.0):
    """Poste con placa negra y focos que miran a cara·X (al tráfico que llega). Devuelve los focos para las Luces."""
    x, y, _ = c
    h = 2.8
    top = (x + cara * math.sin(inclinada) * h, y, h * math.cos(inclinada))
    m.barra((x, y, 0.0), top, 0.1 if fino else 0.3, 0.1 if fino else 0.3, "poste")
    focos = []
    alturas = (2.55, 2.0) if doble else (2.55,)
    for zz in alturas:
        zz *= math.cos(inclinada)
        xx = x + cara * math.sin(inclinada) * zz
        m.caja((xx + cara * 0.08, y, zz), (0.06, 1.0, 0.42), "cabeza")                       # placa negra
        for lado, dy in (("A", -0.28), ("B", 0.28)):
            focos.append((lado, (xx + cara * 0.14, y + dy, zz)))
            if fino:
                m.caja((xx + cara * 0.2, y + dy, zz + 0.16), (0.14, 0.3, 0.03), "cabeza")    # visera
    # cruz de San Andrés bajo los focos
    zc = 1.55 * math.cos(inclinada)
    xc = x + cara * (math.sin(inclinada) * zc + 0.1)
    for a in (0.6, -0.6):
        m.caja((xc, y, zc), (0.04, 1.2, 0.18), "placa", rot_x=a)
    return focos


def construir(nombre="SM_PasoNivel", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    a = ASPECTO
    # ---- mecanismos de las barreras: armario gris y soporte acodado
    for (x, y, z), s in ((PIV1, 1), (PIV2, -1)):
        m.caja((x, y + s * 0.25, 0.55), (0.45, 0.5, 1.1), "armario")
        m.caja((x, y + s * 0.25, 1.12), (0.5, 0.55, 0.06), "armario")
        m.barra((x, y + s * 0.35, 1.05), (x, y, z), 0.14, 0.14, "armario")
    # ---- señales con focos (una por sentido, a la derecha del tráfico que llega)
    focos = []
    focos += _senal(m, (4.7, 4.4, 0), 1, a == 1, fino)
    focos += _senal(m, (-4.7, -4.4, 0), -1, a == 1, fino, inclinada=math.radians(12) if a == 2 else 0.0)
    # armarios de señalización a pie de vía (F3_2)
    for x, y in ((2.2, 5.2), (-2.2, -5.2)):
        m.caja((x, y, 0.65), (0.6, 0.45, 1.3), "armario")
        if fino:
            m.caja((x, y - 0.24 * (1 if y > 0 else -1), 0.8), (0.4, 0.03, 0.5), "metal")      # puerta
    # ---- caseta
    cx, cy = 6.6, -5.6
    if a == 4:                                                                              # caseta metálica
        m.caja((cx, cy, 1.15), (2.0, 1.6, 2.3), "pared")
        m.caja((cx, cy, 2.36), (2.3, 1.9, 0.12), "teja")
    else:
        m.caja((cx, cy, 1.15), (2.4, 2.0, 2.3), "pared")
        if a == 1:                                                                          # cubierta plana
            m.caja((cx, cy, 2.36), (2.6, 2.2, 0.14), "hormigon")
        else:
            m.tejado((cx, cy, 2.3), 2.4, 2.0, 0.7, "teja", vuelo=0.2, aguas=2)
    puerta_abierta = a == 2
    if puerta_abierta:
        m.caja((cx - 0.6, cy + 1.02, 1.0), (0.85, 0.04, 2.0), "ventana")                  # hueco oscuro
        m.caja((cx - 1.15, cy + 1.45, 1.0), (0.04, 0.85, 2.0), "puerta")                  # hoja abierta
    else:
        m.caja((cx - 0.6, cy + 1.02, 1.0), (0.85, 0.05, 2.0), "puerta")
    if a == 3:
        m.caja((cx + 0.5, cy + 1.02, 1.45), (0.7, 0.05, 0.5), "sacos")                    # ventana tapiada
    else:
        m.caja((cx + 0.5, cy + 1.02, 1.45), (0.7, 0.05, 0.6), "ventana")
    # ---- extras por variante
    if a == 2:                                                                              # trozo de brazo caído
        for k in range(4):
            m.barra((-3.7 + 0.15 * k, -1.2 + 0.45 * k, 0.06), (-3.56 + 0.15 * k, -0.78 + 0.45 * k, 0.06),
                    0.12 if fino else 0.22, 0.1 if fino else 0.2, "raya_clara" if k % 2 else "raya_oscura")
    if a == 3:                                                                              # puesto militar
        for fila in range(3):
            for k in range(5 - fila):
                m.caja((cx - 1.9 + 0.8 * k + 0.4 * fila, cy + 2.0, 0.17 + 0.3 * fila), (0.75, 0.45, 0.27), "sacos")
        for x in (6.0, 7.6):
            for y in (3.9, -3.9):
                m.caja((x, y, 0.4), (1.5, 0.5, 0.8), "hormigon")
    if a == 4:                                                                              # catenaria y farola
        m.caja((-2.6, 5.5, 3.5), (0.3, 0.3, 7.0), "poste")
        m.caja((-1.4, 5.5, 6.6), (2.4, 0.15, 0.15), "poste")
        m.cilindro((-6.5, 5.0, 2.5), 0.07, 5.0, "Z", 6, "poste")
        m.caja((-6.5, 4.6, 5.0), (0.2, 0.8, 0.12), "poste")
    # ---- barreras (piezas animables)
    m.parte("Barrera_1")
    _brazo(m, PIV1, 1, LARGO, fino)
    m.parte("Barrera_2")
    _brazo(m, PIV2, -1, 1.5 if a == 2 else LARGO, fino)
    # ---- focos (piezas para la emisión)
    for lado in ("A", "B"):
        m.parte("Luces_" + lado)
        for l, (x, y, z) in focos:
            if l == lado:
                m.cilindro((x, y, z), 0.13 if fino else 0.17, 0.06, "X", 10, "foco")
    return m.objeto(nombre)


def separar(o, lod):
    from mathutils import Vector
    base = Vector(o.get("tbd_base", (0.0, 0.0, 0.0)))
    objs = _separar(o, PARTES, PIV)
    # origen en el centro del cruce (no en el centro de la caja envolvente): así las Luces quedan con identidad
    o.data.transform(Matrix.Translation(base))
    for n in objs[1:]:
        n.location = n.location + base
    # la Barrera_2 se gira 180° sobre la vertical: así su brazo también va hacia su −Y local y las dos suben con el
    # mismo signo de giro sobre X
    for n in objs[1:]:
        if n.name == "Barrera_2":
            n.data.transform(Matrix.Rotation(math.pi, 4, "Z"))
            n.rotation_euler.z = math.pi
    return objs
