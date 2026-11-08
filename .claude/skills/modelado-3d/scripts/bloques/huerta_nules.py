"""Kit de huerta para Nules (y la huerta de Sagunto): lo que rodea a los naranjos en la Plana. Teo (3), 8-nov 18:50,
elegido por Teo (la lista de piezas de Álvaro para Nules estaba completa; la huerta solo tenía naranjos). La caseta de
pozo con motor viene del plan v0 de Nules (§75). DE MEMORIA (SIN VERIFICAR): casetas de riego encaladas, tapias de
huerto, balsas y acequias de hormigón de la Plana.

VAR = 5·pieza + variante (0..24). Origen en el suelo, en el centro de la pieza. Sin colisión no hace falta decirlo aquí:
lo decide el nivel (las tapias SÍ pueden bloquear; las acequias no).
Piezas:
  0 CasetaPozo (3 × 3 m): encalada con techo plano; de ladrillo con tejado a un agua; con el motor fuera bajo un
    tejadillo; abandonada (puerta caída, techo roto); con depósito elevado.
  1 Tapia (módulo de 10 m a lo largo de X, 1,6 m): encalada; de ladrillo; de bloque con malla encima; derrumbada a
    trozos; con portón de huerto.
  2 Balsa de riego (8 × 6 m, muro de 1 m): llena; a medias; vacía con lodo; con escalerilla y caseta de bomba; rota.
  3 Acequia (tramo de 10 m a lo largo de X, 0,8 m de cauce): con agua; seca; con compuerta de tablón; con pasarela;
    rota y cegada de tierra.
  4 CasetaAperos (4 × 3 m, techo de chapa a un agua): de chapa; de madera; de ladrillo; con un remolque de palots;
    abandonada.
Colores: cal b8b8b8, ladrillo a86c50, teja a86c50, zócalo y hormigón 94948c, chapa 6c7874, fondo del agua 605c48 con la lámina en
6c7874 (gris azulado: sin azul en la paleta), madera 845c28, puerta 605c48, motor 50541c, tierra y lodo b4a080, malla 94948c. Nada rojo ni en la banda.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 25
PIEZA, V = VAR // 5, VAR % 5
NOMBRES_P = ["CasetaPozo", "Tapia", "Balsa", "Acequia", "CasetaAperos"]
NOMBRES_V = [["Encalada", "Ladrillo", "Motor", "Abandonada", "Deposito"], ["Encalada", "Ladrillo", "Bloque", "Derrumbada", "Porton"],
             ["Llena", "Media", "Vacia", "Escalerilla", "Rota"], ["Agua", "Seca", "Compuerta", "Pasarela", "Cegada"],
             ["Chapa", "Madera", "Ladrillo", "Remolque", "Abandonada"]]
PIEZAS = ["cal", "ladrillo", "hormigon", "chapa", "agua", "lamina", "madera", "puerta", "motor", "tierra", "malla",
          "palot"]
COLORES = {"cal": "b8b8b8", "ladrillo": "a86c50", "hormigon": "94948c", "chapa": "6c7874", "agua": "605c48",
           "lamina": "6c7874", "madera": "845c28", "puerta": "605c48", "motor": "50541c", "tierra": "b4a080",
           "malla": "94948c", "palot": "6c7874"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 2500), 1: (10, 800)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_HuertaNules", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.08 if fino else 0.34
    p, v = PIEZA, V
    if p == 0:                                        # CASETA DE POZO
        muro = "ladrillo" if v == 1 else "cal"
        t.caja((0, 0, 1.25), (3.0, 3.0, 2.5), muro)
        t.caja((0, 0, 0.2), (3.06, 3.06, 0.4), "hormigon")                  # zócalo
        if v == 1:
            t.tejado((0, 0, 2.5), 3.4, 3.4, 0.6, "ladrillo", vuelo=0.0, aguas=1)
        elif v == 3:
            t.caja((-0.6, 0, 2.55), (1.8, 3.1, 0.1), "hormigon")             # media losa; la otra mitad caída
            t.caja((1.2, -2.2, 0.15), (1.6, 1.4, 0.1), "hormigon", rot_z=0.4)
        else:
            t.caja((0, 0, 2.55), (3.2, 3.2, 0.1), "hormigon")
        if v == 3:
            t.caja((-0.5, -1.9, 0.05), (0.9, 1.9, 0.08), "puerta", rot_z=0.3)  # puerta caída
            t.caja((-0.5, -1.51, 1.0), (0.9, 0.04, 1.9), "chapa")             # hueco oscuro (chapa)
        else:
            t.caja((-0.5, -1.51, 1.0), (0.9, 0.04, 1.9), "puerta")
        t.caja((1.51, 0.5, 1.8), (0.04, 0.6, 0.4), "chapa")                  # ventanuco
        t.cilindro((0.9, -1.8, 0.35), 0.1 if fino else 0.17, 0.6, "Y", 8, "hormigon")       # tubo de salida
        t.caja((0.9, -2.5, 0.25), (0.9, 0.7, 0.5), "hormigon")              # pila
        t.caja((0.9, -2.5, 0.48), (0.7, 0.5, 0.04), "agua")
        if v == 2:                                    # motor bajo tejadillo
            t.caja((2.2, 0, 2.0), (1.6, 2.0, 0.08), "chapa", rot_y=0.15)
            for s in (-1, 1):
                t.caja((2.9, s * 0.9, 1.0), (g, g, 2.0), "madera")
            t.caja((2.2, 0, 0.45), (0.9, 0.6, 0.7), "motor")
            t.cilindro((2.2, 0.45, 0.6), 0.2, 0.3, "Y", 10, "motor")
        if v == 4:                                    # depósito elevado
            for sx in (-1, 1):
                for sy in (-1, 1):
                    t.caja((2.6 + sx * 0.6, sy * 0.6, 2.0), (g, g, 4.0), "hormigon")
            t.cilindro((2.6, 0, 4.6), 0.9, 1.2, "Z", 12 if fino else 8, "hormigon")
    elif p == 1:                                      # TAPIA
        muro = {0: "cal", 1: "ladrillo", 2: "hormigon", 3: "cal", 4: "cal"}[v]
        h = 1.6 if v != 2 else 1.1
        tramos = [(-5.0, 5.0)]
        if v == 3:
            tramos = [(-5.0, -1.5), (1.2, 5.0)]
        if v == 4:
            tramos = [(-5.0, -1.4), (1.4, 5.0)]
        for a, b in tramos:
            t.caja(((a + b) / 2, 0, h / 2), (b - a, 0.3, h), muro)
            t.caja(((a + b) / 2, 0, h + 0.04), (b - a + 0.02, 0.38, 0.08), "hormigon")    # albardilla
        for x in (-5.0, -2.5, 0.0, 2.5, 5.0):         # pilastras
            if any(a - 0.01 <= x <= b + 0.01 for a, b in tramos):
                t.caja((x, 0, h / 2), (0.4, 0.42, h + 0.02), muro)
        if v == 2:                                    # malla encima
            for x in (-5.0, -2.5, 0.0, 2.5, 5.0):
                t.caja((x, 0, h + 0.7), (g * 0.6 if fino else 0.34, g * 0.6 if fino else 0.34, 1.3), "malla")
            if fino:
                t.caja((0, 0, h + 0.7), (10.0, 0.02, 1.1), "malla")
        if v == 3:                                    # cascotes de la parte caída
            for k in range(5):
                t.caja((-1.0 + k * 0.5, -0.6 - _h(k) * 0.8, 0.15), (0.6, 0.35, 0.3), "cal", rot_z=_h(k, 1) * 3)
        if v == 4:                                    # portón de huerto de chapa
            for s in (-1, 1):
                t.caja((s * 1.6, 0, 1.0), (0.45, 0.45, 2.0), muro)
            t.caja((0, 0, 0.9), (2.8, 0.05, 1.6), "chapa")
    elif p == 2:                                      # BALSA DE RIEGO
        LX, LY, H = 8.0, 6.0, 1.0
        rota = v == 4
        for s in (-1, 1):
            t.caja((0, s * (LY / 2 - 0.15), H / 2), (LX, 0.3, H), "hormigon")
            if not (rota and s > 0):
                t.caja((s * (LX / 2 - 0.15), 0, H / 2), (0.3, LY - 0.62, H), "hormigon")
            else:
                t.caja((LX / 2 - 0.15, -1.49, H / 2), (0.3, 2.38, H), "hormigon")
                t.caja((LX / 2 - 0.15, 2.2, H / 2), (0.3, 0.9, H), "hormigon")
                t.caja((LX / 2 + 0.6, 0.8, 0.12), (0.8, 0.6, 0.24), "hormigon", rot_z=0.5)
        nivel = {0: 0.85, 1: 0.45, 2: 0.0, 3: 0.75, 4: 0.0}[v]
        t.caja((0, 0, 0.03), (LX - 0.64, LY - 0.64, 0.06), "tierra" if v in (2, 4) else "agua")
        if nivel:
            t.caja((0, 0, nivel), (LX - 0.62, LY - 0.62, 0.04), "lamina")
        if rota:                                      # el agua derramada fuera, ya seca
            t.caja((LX / 2 + 2.0, 0.6, 0.01), (3.0, 3.5, 0.02), "tierra")
        if v == 3:
            for k in range(4):
                t.caja((-LX / 2 + 0.5, -LY / 2 + 0.15, 0.2 + k * 0.25), (0.6, 0.06 if fino else 0.34, 0.06 if fino else 0.34),
                       "malla")
            t.caja((-LX / 2 - 1.2, -1.5, 0.9), (1.6, 1.6, 1.8), "cal")
            t.caja((-LX / 2 - 1.2, -1.5, 1.84), (1.8, 1.8, 0.08), "hormigon")
    elif p == 3:                                      # ACEQUIA
        L = 10.0
        seg = [(-L / 2, L / 2)] if v != 4 else [(-L / 2, -1.0), (1.5, L / 2)]
        for a, b in seg:
            for s in (-1, 1):
                t.caja(((a + b) / 2, s * 0.475, 0.1), (b - a, 0.15 if fino else 0.34, 0.5), "hormigon")
            t.caja(((a + b) / 2, 0, -0.14), (b - a, 0.8, 0.04), "tierra" if v in (1, 4) else "agua")
            if v in (0, 2, 3):
                t.caja(((a + b) / 2, 0, 0.02), (b - a, 0.78, 0.04), "lamina")
        if v == 4:                                    # rota y cegada
            t.caja((0.25, 0, 0.1), (2.6, 1.6, 0.4), "tierra", rot_z=0.2)
            t.caja((-0.6, -0.9, 0.12), (0.8, 0.2, 0.3), "hormigon", rot_z=0.8)
        if v == 2:                                    # compuerta de tablón
            for s in (-1, 1):
                t.caja((0, s * 0.5, 0.5), (0.12, 0.12, 1.0), "hormigon")
            t.caja((0, 0, 0.6), (0.06 if fino else 0.34, 0.9, 0.8), "madera")
        if v == 3:                                    # pasarela
            t.caja((1.5, 0, 0.4), (1.2, 1.6, 0.12), "hormigon")
    else:                                             # CASETA DE APEROS
        muro = {0: "chapa", 1: "madera", 2: "ladrillo", 3: "chapa", 4: "madera"}[v]
        LX, LY, H = 4.0, 3.0, 2.3
        for s in (-1, 1):
            t.caja((s * (LX / 2 - 0.05), 0, H / 2), (0.1, LY, H), muro)
        t.caja((0, LY / 2 - 0.05, H / 2), (LX - 0.22, 0.1, H), muro)
        if v != 4:
            t.caja((0, 0, H + 0.1), (LX + 0.4, LY + 0.4, 0.08), "chapa", rot_x=0.08)
        else:
            t.caja((-1.0, 0, H + 0.1), (2.2, LY + 0.4, 0.08), "chapa", rot_x=0.08)
            t.caja((1.6, -2.0, 0.08), (1.8, 1.0, 0.06), "chapa", rot_z=0.4)
        t.caja((0, 0, 0.03), (LX + 0.1, LY + 0.1, 0.06), "hormigon")
        if fino:                                      # aperos dentro: escalera y azadas
            t.caja((-1.2, 1.2, 1.1), (0.4, 0.06, 2.0), "madera", rot_y=0.2)
            for k in range(3):
                t.caja((0.4 + k * 0.3, 1.3, 0.7), (0.04, 0.04, 1.3), "madera", rot_x=0.15)
        t.caja((1.2, 0.6, 0.4), (0.9, 0.9, 0.7), "palot")
        if v == 3:                                    # remolque de palots al lado
            t.caja((4.0, -0.5, 0.65), (1.8, 2.8, 0.15), "madera")
            for s in (-1, 1):
                t.cilindro((4.0 + s * 1.0, -0.5, 0.35), 0.35, 0.2, "X", 10 if fino else 6, "puerta")
            for k in range(2):
                t.caja((4.0, -1.2 + k * 1.35, 0.73 + 0.38), (1.2, 1.0, 0.76), "palot")
            t.caja((4.0, -2.4, 0.5), (g, 1.2, g), "chapa")
    return t.objeto(nombre, bisel=0)
