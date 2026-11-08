"""Kit «Sagunto vivo»: la vida dentro del fuerte y las huellas fuera (sagunto-nivel.md §3 y §10.8). Teo (relevo),
8-nov 19:00, aprobado por Lucía (18:56) antes de modelar. DE MEMORIA salvo el armario de Adif y la cruz (fotos P2/P3
de Paula, referencias-reales/entorno/). Medidas SIN VERIFICAR.

VAR = 5·pieza + variante (0..14): 0 Bidones, 1 Solar, 2 ArmarioAdif. El bidón con fuego, la ropa tendida y el huerto
de la propuesta los cubre vida_fuerte.py (hilo anterior de Teo, TBD 03bb160), que va junto a este en la misma entrega.
El código interno conserva los números 3–5 de la propuesta (las ramas 0–2 no se usan). Origen en el suelo, en el centro de la pieza; el frente a −Y.
Condiciones de Lucía (18:56):
  - NINGUNA pieza lleva colisión (tampoco el armario). La colocación la hace Álvaro, lejos de la banqueta, de la
    azotea del ala oeste y de los recorridos de la tropa.
  - La luz del bidón la regula Bruno (-capdirecta): cálida y pequeña, sin iluminar la horda al otro lado del muro.
    El modelo solo lleva la llama (f0dc9c) y un hueco «Luz» en (0; 0; 1,1) como referencia para el punto de luz.
  - Borde del STOP en 605c48 (48403c en vertical cae en la banda de la horda). Nada vertical en 48403c.
  - Los bidones de combustible son decorado: no cuentan como existencias.
Piezas:
  0 BidonFuego (bidón de 0,58 × 0,88 m): Solo, Parrilla (rejilla y olla), Corro (cajas y palé para sentarse),
    Apagado (sin llama, ceniza), Doble.
  1 RopaTendida (cuerda entre dos palos, 4–6 m): Cuerda4, Cuerda6, DosCuerdas, Tendedero (de aspas, plegable),
    Caida (un palo tumbado y la ropa por el suelo).
  2 Huerto (bancales de tablones de 1,2 × 4 m): Lechugas, Tomateras (con cañas), Plantado (recién), Cuatro
    (cuatro bancales y regadera), Seco (abandonado).
  3 Bidones de combustible: Pale4, Fila6, Tumbados (pirámide), Lona, Volcados.
  4 Solar de casa demolida (6 × 10 m, la huella de casa_pueblo): Escombro (montón), Medianera (trozo de pared y
    baldosas), Vigas, Despejado (casi limpio, con marcas), Tejas.
  5 Armario de Adif (paso a nivel, §10.8): Completo (armario, cruz con STOP y baliza), Pintado (con pintadas),
    Abierto (puerta abierta y cables), CruzDoblada, Volcado.
Retoques de Lucía (19:01): el solar en tierra b4a080/605c48 con restos de solería (una losa gris se leía como un
claro del NH90) y los bidones solo en gris 94948c/6c7874 (el verde es el código de la munición).
Colores: óxido 7c3c18, bidón 605c48, verde 70784c, llama f0dc9c, ceniza 48403c (solo en el suelo), metal 94948c,
madera a86c50, tierra 845c28 (solo arriba), planta 70784c/50541c, seco 847c34, caña b4a080, ropa d4d0cc/b8b8b8/
847c34/6c7874, palo 6c7874, pared d4d0cc, escombro 94948c, teja 7c3c18, armario b8b8b8, borde 605c48.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 15
PIEZA, V = VAR // 5 + 3, VAR % 5   # 3 Bidones, 4 Solar, 5 ArmarioAdif (0–2: vida_fuerte.py)
NOMBRES_P = ["BidonFuego", "RopaTendida", "Huerto", "Bidones", "Solar", "ArmarioAdif"]
NOMBRES_V = [["Solo", "Parrilla", "Corro", "Apagado", "Doble"],
             ["Cuerda4", "Cuerda6", "DosCuerdas", "Tendedero", "Caida"],
             ["Lechugas", "Tomateras", "Plantado", "Cuatro", "Seco"],
             ["Pale4", "Fila6", "Tumbados", "Lona", "Volcados"],
             ["Escombro", "Medianera", "Vigas", "Despejado", "Tejas"],
             ["Completo", "Pintado", "Abierto", "CruzDoblada", "Volcado"]]
PIEZAS = ["oxido", "bidon", "verde", "llama", "ceniza", "metal", "madera", "tierra", "planta", "planta2", "seco",
          "cana", "ropa1", "ropa2", "ropa3", "ropa4", "palo", "pared", "escombro", "teja", "armario", "borde", "lona", "solar", "gris2"]
COLORES = {"oxido": "7c3c18", "bidon": "605c48", "verde": "70784c", "llama": "f0dc9c", "ceniza": "48403c",
           "metal": "94948c", "madera": "a86c50", "tierra": "845c28", "planta": "70784c", "planta2": "50541c",
           "seco": "847c34", "cana": "b4a080", "ropa1": "d4d0cc", "ropa2": "b8b8b8", "ropa3": "847c34",
           "ropa4": "6c7874", "palo": "6c7874", "pared": "d4d0cc", "escombro": "94948c", "teja": "7c3c18",
           "armario": "b8b8b8", "borde": "605c48", "lona": "50541c",
           "solar": "b4a080", "gris2": "6c7874"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 2500), 1: (10, 900)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
ROPAS = ("ropa1", "ropa2", "ropa3", "ropa4")


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_SaguntoVivo", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    m = 0.34                                          # detalle mínimo del LOD1 (2 px)
    g = lambda a: a if fino else max(a, m)
    lados = 10 if fino else 6
    p, v = PIEZA, V

    def bidon(x, y, pieza="oxido", tumbado=False, rz=0.0, z0=0.0):
        if tumbado:
            t.cilindro((x, y, z0 + 0.29), 0.29, 0.88, "X", lados, pieza)
            return
        t.cilindro((x, y, z0 + 0.44), 0.29, 0.88, "Z", lados, pieza)
        if fino:
            for zz in (0.3, 0.6):                     # aros
                t.cilindro((x, y, z0 + zz), 0.30, 0.03, "Z", lados, "metal")

    def pale(x, y, rz=0.0, z=0.07):
        t.caja((x, y, z), (1.2, 0.8, 0.14), "madera", rot_z=rz)

    def caja(x, y, s=0.5, pieza="madera", rz=0.0, z=0.0):
        t.caja((x, y, z + s / 2), (s, s * 0.8, s), pieza, rot_z=rz)

    if p == 0:                                        # BIDÓN CON FUEGO
        def fuego(x, y, alto=0.6):
            bidon(x, y, "oxido")
            if fino:
                for k in range(4):                    # agujeros de tiro: manchas de óxido oscuro
                    a = k * math.pi / 2
                    t.caja((x + math.cos(a) * 0.29, y + math.sin(a) * 0.29, 0.25), (0.1, 0.1, 0.08), "bidon",
                           rot_z=a)
            t.cilindro((x, y, 0.9), 0.24, 0.04, "Z", lados, "ceniza")
            t.cilindro((x, y, 0.92 + alto / 2), 0.2, alto, "Z", 5 if fino else 4, "llama", r2=0.04)
            if fino:
                t.cilindro((x + 0.1, y - 0.05, 0.92 + alto * 0.35), 0.1, alto * 0.7, "Z", 4, "llama", r2=0.02)
        if v == 3:                                    # apagado
            bidon(0, 0, "oxido")
            t.cilindro((0, 0, 0.9), 0.25, 0.05, "Z", lados, "ceniza")
            t.cilindro((0, 0, 0.02), 0.8, 0.04, "Z", lados, "ceniza")
            for k in range(3):
                t.caja((0.5 + _h(k) * 0.4, -0.3 + k * 0.3, 0.05), (0.5, g(0.08), g(0.08)), "madera", rot_z=_h(k, 1) * 3)
        elif v == 4:
            fuego(-0.45, 0, 0.55)
            fuego(0.45, 0.1, 0.45)
        else:
            fuego(0, 0)
            t.cilindro((0, 0, 0.02), 0.7, 0.04, "Z", lados, "ceniza")      # suelo quemado alrededor
            if v == 1:                                # parrilla y olla
                t.caja((0, 0, 0.96), (0.7, 0.7, g(0.03)), "metal")
                t.cilindro((0.12, 0.05, 1.08), 0.15, 0.22, "Z", lados, "metal")
            if v == 2:                                # corro: cajas y un palé para sentarse
                for k, a in enumerate((0.3, 2.1, 3.9)):
                    caja(math.cos(a) * 1.4, math.sin(a) * 1.4, 0.45, "madera", a)
                pale(0.2, -1.5, 0.1)
                t.caja((0.2, -1.5, 0.25), (1.0, 0.6, g(0.06)), "lona", rot_z=0.1)   # manta
            for k in range(3 if fino else 2):         # leña
                t.caja((-0.7, 0.6 + k * 0.12, 0.06 + k * 0.1), (0.7, g(0.1), g(0.1)), "madera", rot_z=0.2 * k)
        t.caja((0, 0, 1.1), (0.01, 0.01, 0.01), "llama")   # marca del punto de luz (se funde en la llama)

    elif p == 1:                                      # ROPA TENDIDA
        def tendal(x0, x1, y, alto=1.9, k0=0, caido=False):
            L = x1 - x0
            for x in (x0, x1):
                t.caja((x, y, alto / 2), (g(0.07), g(0.07), alto), "palo")
            t.caja(((x0 + x1) / 2, y, alto - 0.05), (L, g(0.02), g(0.02)), "palo")
            n = int(L / 0.75)
            for i in range(n):
                w = 0.4 + _h(i, k0) * 0.35
                hh = 0.45 + _h(i, k0 + 3) * 0.45
                x = x0 + 0.45 + i * (L - 0.9) / max(1, n - 1)
                t.caja((x, y, alto - 0.08 - hh / 2), (w, g(0.04), hh), ROPAS[(i + k0) % 4],
                       rot_z=(_h(i, k0 + 7) - 0.5) * 0.25)
        if v == 0:
            tendal(-2, 2, 0)
        elif v == 1:
            tendal(-3, 3, 0, k0=2)
        elif v == 2:
            tendal(-2.5, 2.5, -0.6, k0=1)
            tendal(-2.5, 2.5, 0.6, k0=3)
        elif v == 3:                                  # tendedero de aspas
            t.caja((0, 0, 0.5), (g(0.05), g(0.05), 1.0), "palo", rot_x=0.35)
            t.caja((0, 0, 0.5), (g(0.05), g(0.05), 1.0), "palo", rot_x=-0.35)
            for yy in (-0.4, -0.15, 0.15, 0.4):
                t.caja((0, yy, 0.95), (1.6, g(0.02), g(0.02)), "palo")
            for i in range(5):
                x = -0.6 + i * 0.3
                hh = 0.3 + _h(i, 5) * 0.3
                t.caja((x, (-0.4, 0.15, 0.4, -0.15, 0.15)[i], 0.93 - hh / 2), (0.28, g(0.03), hh), ROPAS[i % 4])
        else:                                         # caída: un palo tumbado, la ropa por el suelo
            t.caja((-2, 0, 0.95), (g(0.07), g(0.07), 1.9), "palo")
            t.caja((1.0, 0, 0.05), (1.9, g(0.07), g(0.07)), "palo", rot_z=0.3)
            t.caja((0, 0, 1.0), (4.0, g(0.02), g(0.02)), "palo", rot_y=0.45)
            for i in range(6):
                t.caja((-1.2 + i * 0.6, -0.3 + _h(i) * 0.6, 0.03), (0.5, 0.4, 0.05), ROPAS[i % 4], rot_z=_h(i, 2) * 3)

    elif p == 2:                                      # HUERTO IMPROVISADO
        def bancal(x, y, tipo, n=0):
            t.caja((x, y, 0.15), (1.2, 4.0, 0.3), "tierra")
            for sx in (-0.62, 0.62):
                t.caja((x + sx, y, 0.17), (g(0.05), 4.1, 0.34), "madera")
            for sy in (-2.03, 2.03):
                t.caja((x, y + sy, 0.17), (1.3, g(0.05), 0.34), "madera")
            filas = 8 if tipo != "plantado" else 5
            for i in range(filas):
                for j in (-0.3, 0.3):
                    yy = y - 1.7 + i * 3.4 / (filas - 1)
                    if tipo == "plantado" and _h(i, n) < 0.3:
                        continue
                    if tipo == "lechuga":
                        t.caja((x + j, yy, 0.38), (g(0.3), g(0.3), 0.16), "planta" if (i + n) % 2 else "planta2")
                    elif tipo == "tomate":
                        t.caja((x + j, yy, 0.85), (g(0.04), g(0.04), 1.1), "cana")
                        t.caja((x + j, yy, 0.7), (g(0.28), g(0.28), 0.7), "planta")
                    elif tipo == "plantado":
                        t.caja((x + j, yy, 0.34), (g(0.14), g(0.14), 0.1), "planta")
                    else:                                    # seco
                        if _h(i, n + 2) < 0.6:
                            t.caja((x + j, yy, 0.36), (g(0.25), g(0.25), 0.14), "seco", rot_z=_h(i, 4))
        if v == 0:
            bancal(-0.8, 0, "lechuga"); bancal(0.8, 0, "lechuga", 1)
        elif v == 1:
            bancal(-0.8, 0, "tomate"); bancal(0.8, 0, "lechuga", 2)
        elif v == 2:
            bancal(-0.8, 0, "plantado"); bancal(0.8, 0, "plantado", 3)
            t.caja((2.1, -1.0, 0.25), (0.6, 0.4, 0.5), "madera")                 # caja de plantel
        elif v == 3:
            for i, tp in enumerate(("lechuga", "tomate", "lechuga", "plantado")):
                bancal(-2.4 + i * 1.6, 0, tp, i)
            t.cilindro((3.6, -2.2, 0.15), 0.15, 0.3, "Z", lados, "verde")        # regadera
            bidon(3.6, 1.6, "verde")                                              # agua
        else:
            bancal(-0.8, 0, "seco"); bancal(0.8, 0, "seco", 5)
            t.caja((0.8, 2.6, 0.05), (1.3, g(0.05), 0.1), "madera", rot_z=0.6)  # tablón suelto

    elif p == 3:                                      # BIDONES DE COMBUSTIBLE (decorado)
        if v == 0:
            pale(0, 0)
            for i in range(4):
                bidon(-0.3 + (i % 2) * 0.6, -0.2 + (i // 2) * 0.4 if fino else -0.2 + (i // 2) * 0.45,
                      "gris2" if i != 2 else "metal", z0=0.14)
        elif v == 1:
            for i in range(6):
                bidon(-1.6 + i * 0.64, (_h(i) - 0.5) * 0.1, "gris2" if i % 3 else "metal")
        elif v == 2:                                  # pirámide de tumbados con calzos
            for i in range(3):
                bidon(0, -0.62 + i * 0.62, "gris2")
            for i in range(2):
                bidon(0, -0.31 + i * 0.62, "gris2", tumbado=True, z0=0.5)
            for sy in (-0.95, 0.95):
                t.caja((0, sy, 0.1), (1.0, g(0.1), 0.2), "madera")
        elif v == 3:                                  # tapados con lona
            pale(0, 0)
            pale(1.3, 0)
            t.caja((0.65, 0, 0.6), (2.6, 1.0, 0.95), "lona")
            t.caja((0.65, -0.55, 0.3), (2.7, g(0.04), 0.5), "lona", rot_x=0.2)
            for x in (-0.6, 1.9):
                t.caja((x, -0.55, 0.15), (0.3, 0.3, 0.3), "escombro")             # piedras que sujetan
        else:                                         # volcados con mancha
            t.cilindro((0.6, -0.4, 0.015), 1.1, 0.03, "Z", lados, "ceniza")
            bidon(-0.6, 0.3, "gris2")
            bidon(0.2, -0.2, "gris2", tumbado=True)
            bidon(1.1, 0.4, "metal", tumbado=True)

    elif p == 4:                                      # SOLAR DE CASA DEMOLIDA (6 × 10)
        t.caja((0, 0, 0.03), (6.0, 10.0, 0.06), "solar")                          # tierra del solar (Lucía 19:01)
        for i in range(9 if fino else 5):                                         # manchas de tierra oscura
            t.caja((-2.2 + _h(i, 9) * 4.4, -4.2 + _h(i, 10) * 8.4, 0.065), (0.8 + _h(i, 11), 0.6 + _h(i, 12), 0.01),
                   "borde", rot_z=_h(i, 13) * 3)
        for i in range(6 if fino else 3):                                         # restos de solería
            t.caja((-1.8 + _h(i, 14) * 3.6, -3.5 + _h(i, 15) * 7.0, 0.075), (0.9, 0.9, 0.03), "ropa2",
                   rot_z=_h(i, 16) * 0.4)
        def monton(x, y, r, alto, k):
            t.cilindro((x, y, alto / 2), r, alto, "Z", 7 if fino else 5, "escombro", r2=r * 0.35)
            n = 7 if fino else 3
            for i in range(n):
                a = _h(i, k) * 6.28
                rr = r * (0.3 + _h(i, k + 1) * 0.6)
                t.caja((x + math.cos(a) * rr, y + math.sin(a) * rr, alto * 0.4 + _h(i, k + 2) * alto * 0.4),
                       (g(0.5), g(0.25), g(0.15)), ("pared", "teja", "madera")[i % 3], rot_z=a,
                       rot_x=_h(i, k + 3) - 0.5)
        if v in (0, 1, 2, 4):
            for sx in (-2.97, 2.97):                  # arranques de las medianeras de las vecinas
                alto = 0.8 if v != 1 else 0.5
                t.caja((sx, 0, alto / 2), (g(0.2), 10.0, alto), "pared")
        if v == 0:
            monton(0.3, 0.8, 2.2, 1.6, 1)
            monton(-1.6, -3.0, 1.2, 0.8, 2)
        elif v == 1:                                  # trozo de medianera en pie y baldosas
            t.caja((-2.9, 2.0, 2.2), (g(0.25), 4.0, 4.4), "pared")
            t.caja((-2.9, 2.0, 4.0), (g(0.27), 1.2, 0.8), "teja")                  # ladrillo visto arriba
            for i in range(4):
                for j in range(3):
                    if _h(i * 3 + j) < 0.6:
                        t.caja((-1.5 + i * 1.0, -3.0 + j * 1.0, 0.08), (0.95, 0.95, 0.04), "ropa2")
            monton(1.4, 1.5, 1.4, 1.0, 3)
        elif v == 2:                                  # vigas de madera cruzadas sobre el escombro
            monton(0, 0, 2.0, 1.2, 4)
            for i in range(4):
                t.caja((-0.8 + i * 0.6, 0.3 * i - 0.5, 0.9 + i * 0.1), (g(0.2), 5.0, g(0.2)), "madera",
                       rot_z=(i - 1.5) * 0.35, rot_x=0.15)
        elif v == 3:                                  # despejado por el Mando: casi limpio
            t.caja((0, -4.0, 0.25), (3.0, 1.2, 0.5), "escombro")                   # lo que quedó apartado
            for i in range(5):
                t.caja((-2.0 + i * 1.0, 2.5 + _h(i) * 2, 0.08), (g(0.4), g(0.3), 0.1), "pared", rot_z=_h(i, 1) * 3)
            for i in range(2):                        # rodadas
                t.caja((-1.0 + i * 2.0, 0, 0.065), (0.4, 9.5, 0.01), "tierra")
        else:                                         # tejas: el tejado hundido entero
            t.caja((0, 0.5, 0.6), (5.4, 6.0, 0.15), "teja", rot_x=0.18)
            t.caja((0, -3.6, 0.3), (5.0, 2.0, 0.15), "teja", rot_x=-0.25)
            monton(-1.6, 3.6, 1.0, 0.9, 5)

    else:                                             # ARMARIO DE ADIF (fotos P2/P3)
        def armario(x, y, rz=0.0, abierto=False, pintadas=False):
            t.caja((x, y, 0.15), (1.4, 0.8, 0.3), "escombro", rot_z=rz)          # zócalo de hormigón
            t.caja((x, y, 1.2), (1.2, 0.6, 1.8), "armario", rot_z=rz)
            t.caja((x, y, 2.13), (1.3, 0.7, 0.06), "armario", rot_z=rz)
            t.caja((x + 0.65, y, 1.4), (0.12, 0.45, 0.6), "armario", rot_z=rz)  # caja lateral (foto P3)
            if fino and not abierto:
                t.caja((x, y - 0.305, 1.2), (g(0.03), 0.04, 1.6), "borde", rot_z=rz)   # junta de las puertas
            if abierto:
                t.caja((x - 0.6, y - 0.6, 1.2), (0.03, 0.6, 1.6), "armario", rot_z=rz + 0.0)
                t.caja((x, y - 0.29, 1.2), (1.0, 0.02, 1.4), "borde", rot_z=rz)    # interior en sombra
                for i in range(3):
                    t.caja((x - 0.2 + i * 0.2, y - 0.5, 0.2), (g(0.04), 0.5, g(0.04)), "palo", rot_x=0.9)
            if pintadas:
                for i in range(4):
                    t.caja((x - 0.4 + i * 0.25, y - 0.31, 0.9 + _h(i) * 0.7),
                           (0.22, 0.04, g(0.06)), ("borde", "palo", "ropa3", "borde")[i], rot_y=_h(i, 3) - 0.5)
        def cruz(x, y, doblada=False):
            alto = 3.2
            if doblada:
                t.caja((x, y, 0.7), (g(0.09), g(0.09), 1.4), "palo")
                t.caja((x, y - 0.7, 1.6), (g(0.09), g(0.09), 1.8), "palo", rot_x=-0.9)
                cx, cy, cz, rx = x, y - 1.35, 2.15, -0.9
            else:
                t.caja((x, y, alto / 2), (g(0.09), g(0.09), alto), "palo")
                cx, cy, cz, rx = x, y - 0.06, alto - 0.35, 0.0
            for s in (0.7, -0.7):                     # aspa: blanco con borde (sin el rojo real)
                if fino:
                    t.caja((cx, cy, cz), (1.2, 0.03, 0.2), "borde", rot_y=s, rot_x=rx)
                    t.caja((cx, cy - 0.02, cz), (1.1, 0.03, 0.13), "armario", rot_y=s, rot_x=rx)
                else:                                 # LOD1: aspa blanca de 2 px, sin borde (el borde hacía mancha)
                    t.caja((cx, cy - 0.17, cz), (1.3, 0.1, 0.34), "armario", rot_y=s, rot_x=rx)
            if not doblada:                           # STOP octogonal debajo
                dy = 0.0 if fino else -0.17           # en el LOD1 el poste mide 0,34: la placa, delante
                t.cilindro((x, y - 0.08 + dy, 1.9), 0.33 if fino else 0.42, 0.03, "Y", 8, "borde")
                t.cilindro((x, y - 0.1 + dy, 1.9), 0.28 if fino else 0.3, 0.03, "Y", 8, "armario")
        def baliza(x, y):
            for i in range(4):
                t.caja((x, y, 0.15 + i * 0.3), (g(0.12) + 0.02 * (i % 2), g(0.12) + 0.02 * (i % 2), 0.3), "armario" if i % 2 else "borde")
        if v == 4:                                    # volcado: el armario por el suelo y la cruz en pie
            t.caja((-0.8, 0, 0.15), (1.4, 0.8, 0.3), "escombro")
            t.caja((-0.6, -1.1, 0.3), (1.2, 1.8, 0.6), "armario", rot_z=0.2)
            for i in range(3):
                t.caja((-0.8 + i * 0.15, -0.2, 0.35), (g(0.04), g(0.04), 0.4), "palo", rot_x=0.4)
            cruz(1.0, 0)
        else:
            armario(-0.8, 0, abierto=v == 2, pintadas=v == 1)
            cruz(1.0, 0, doblada=v == 3)
            baliza(1.0, -1.4)
    return t.objeto(nombre, bisel=0)
