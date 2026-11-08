"""Vida en el fuerte de Sagunto: «aquí vive gente» (sagunto-nivel.md §3; Teo, 8-nov 18:55, elegido por Teo: ese apartado
no tenía ningún modelo). Sin colisión ni efecto en la simulación (§3: «nada de esto tiene colisión»). DE MEMORIA.

VAR = 5·pieza + variante (0..24). Origen en el suelo, en el centro de la pieza.
La llama y las brasas salen en el hijo «Emision» (mismo origen) para que Marcos les dé luz cálida de noche.
Piezas:
  0 BidonFuego: solo; con parrilla y cajas; dos bidones; con cajas y sillas alrededor; apagado.
  1 Tendedero (4 m a lo largo de X): una cuerda; dos cuerdas; sábanas grandes; de balcón (sale de una pared en +Y);
    vacío con pinzas.
  2 Bancales (huerto improvisado, 3 bancales de 4 × 1,2 m): con plantas; recién plantado; con regadera y cubos; con
    cañas de tomateras; abandonado y seco.
  3 Tienda: tienda militar de 4 × 3 m; lona tendida entre postes; tienda civil de iglú; colchones y mantas al aire;
    tienda caída.
  4 CocinaCampana: mesa con olla; fogón de leña; con garrafas de agua; comedor (mesas largas y bancos); recogida.
Colores: bidón 605c48, llama f0dc9c, brasa b4a080, ceniza 94948c, madera 845c28, ropa b8b8b8 / 94948c / 847c34 /
b4a080 / 6c7874 (apagados, sin ámbar ni rojo), tierra 845c28, planta 70784c, lona 847c34, tienda militar 50541c,
colchón 94948c, metal 6c7874. Nada en la banda de la horda.
"""
import math, os
from taller import Taller
from montaje import marcar_piezas, separar_piezas

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 25
PIEZA, V = VAR // 5, VAR % 5
NOMBRES_P = ["BidonFuego", "Tendedero", "Bancales", "Tienda", "CocinaCampana"]
NOMBRES_V = [["Solo", "Parrilla", "Dos", "Corro", "Apagado"], ["Cuerda", "DosCuerdas", "Sabanas", "Balcon", "Vacio"],
             ["Plantas", "Plantado", "Regadera", "Tomateras", "Seco"], ["Militar", "Lona", "Iglu", "Colchones", "Caida"],
             ["Olla", "Lena", "Garrafas", "Comedor", "Recogida"]]
PIEZAS = ["bidon", "llama", "brasa", "ceniza", "madera", "ropa1", "ropa2", "ropa3", "ropa4", "ropa5", "tierra",
          "planta", "lona", "militar", "colchon", "metal"]
COLORES = {"bidon": "605c48", "llama": "f0dc9c", "brasa": "b4a080", "ceniza": "94948c", "madera": "845c28",
           "ropa1": "b8b8b8", "ropa2": "94948c", "ropa3": "847c34", "ropa4": "b4a080", "ropa5": "6c7874",
           "tierra": "845c28", "planta": "70784c", "lona": "847c34", "militar": "50541c", "colchon": "94948c",
           "metal": "6c7874"}
EMISION = {"Emision": ["llama", "brasa"]}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 2000), 1: (10, 900)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
ROPA = ["ropa1", "ropa2", "ropa3", "ropa4", "ropa5"]


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_VidaFuerte", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.06 if fino else 0.34
    lados = 12 if fino else 8
    p, v = PIEZA, V

    def bidon(x, y, fuego=True):
        t.cilindro((x, y, 0.44), 0.29, 0.88, "Z", lados, "bidon")
        if fuego:
            t.cilindro((x, y, 0.9), 0.24, 0.06, "Z", lados, "brasa")
            t.cilindro((x, y, 1.08), 0.17, 0.32, "Z", 6 if fino else 5, "llama", r2=0.03 if fino else 0.17)
            if fino:                                  # lenguas sueltas: que no parezca la punta de un lápiz
                for k in range(3):
                    a = k * 2.1
                    t.cilindro((x + 0.1 * math.cos(a), y + 0.1 * math.sin(a), 1.02), 0.08, 0.2, "Z", 5, "llama", r2=0.02)
        else:
            t.cilindro((x, y, 0.885), 0.25, 0.02, "Z", lados, "ceniza")

    def caja(x, y, z=0.0, rz=0.0, s=(0.5, 0.4, 0.4)):
        t.caja((x, y, z + s[2] / 2), s, "madera", rot_z=rz)

    def silla(x, y, rz):
        t.caja((x, y, 0.45), (0.45, 0.45, 0.05), "metal", rot_z=rz)
        c, s = math.cos(rz), math.sin(rz)
        t.caja((x - 0.2 * s, y + 0.2 * c, 0.7), (0.45, 0.04 if fino else 0.34, 0.5), "metal", rot_z=rz)
        if fino:
            for dx in (-0.18, 0.18):
                for dy in (-0.18, 0.18):
                    t.caja((x + dx * c - dy * s, y + dx * s + dy * c, 0.22), (0.03, 0.03, 0.44), "metal")

    if p == 0:                                        # BIDÓN CON FUEGO
        if v == 2:
            bidon(-0.5, 0)
            bidon(0.5, 0.2)
        else:
            bidon(0, 0, fuego=v != 4)
        if v == 1:                                    # parrilla encima y cajas
            t.caja((0, 0, 0.95), (0.7, 0.7, 0.03 if fino else 0.34), "metal")
            caja(0.9, 0.3)
            caja(0.9, -0.3, 0.4, 0.2)
        if v == 3:
            for k in range(4):
                a = k * math.pi / 2 + 0.4
                if k % 2:
                    silla(1.2 * math.cos(a), 1.2 * math.sin(a), a + math.pi / 2)
                else:
                    caja(1.2 * math.cos(a), 1.2 * math.sin(a), rz=a)
        if v == 4:
            t.caja((0.5, 0.35, 0.01), (0.9, 0.7, 0.02), "ceniza", rot_z=0.3)
            for k in range(3):
                t.caja((0.7 + k * 0.2, -0.5, 0.05), (0.6, 0.08, 0.08), "madera", rot_z=k * 0.7)
    elif p == 1:                                      # TENDEDERO
        cuerdas = [0.0] if v != 1 else [-0.4, 0.4]
        balcon = v == 3
        for y in cuerdas:
            if balcon:                                # cuerda que sale de la pared (+Y) a un palo
                t.caja((0, 0.6, 2.4), (4.0, g * 0.5 if fino else 0.34, g * 0.5 if fino else 0.34), "metal")
                for x in (-2.0, 2.0):
                    t.caja((x, 0.3, 2.4), (g, 0.7, g), "metal")
            else:
                for x in (-2.0, 2.0):
                    t.caja((x, y, 0.9), (g, g, 1.8), "madera")
                if fino:
                    t.caja((0, y, 1.75), (4.0, 0.02, 0.02), "metal")
            z = 2.4 if balcon else 1.75
            yy = 0.6 if balcon else y
            if v == 4:
                if fino:
                    for k in range(6):
                        t.caja((-1.5 + k * 0.6, yy, z - 0.04), (0.03, 0.03, 0.06), "ropa5")
                continue
            if v == 2:                                # sábanas grandes
                for k in range(2):
                    t.caja((-1.0 + k * 2.0, yy, z - 0.65), (1.6, 0.03 if fino else 0.34, 1.3), ROPA[k * 3 % 5])
                continue
            k0 = int(y * 10) + 3
            x = -1.7
            k = 0
            while x < 1.6:
                w = 0.35 + _h(k + k0) * 0.35
                hgt = 0.4 + _h(k + k0, 1) * 0.4
                t.caja((x + w / 2, yy, z - hgt / 2), (w, 0.03 if fino else 0.34, hgt), ROPA[(k + k0) % 5])
                x += w + 0.12
                k += 1
    elif p == 2:                                      # BANCALES
        for i in range(3):
            y = (i - 1) * 1.8
            for s in (-1, 1):                         # tablones del borde
                t.caja((0, y + s * 0.6, 0.12), (4.0, 0.05 if fino else 0.34, 0.24), "madera")
                t.caja((s * 2.0, y, 0.12), (0.05 if fino else 0.34, 1.15, 0.24), "madera")
            seco = v == 4
            t.caja((0, y, 0.1), (3.9, 1.1, 0.2), "ceniza" if seco else "tierra")
            if v in (0, 2, 3):
                n = 6 if fino else 3
                for k in range(n):
                    for f in (-0.25, 0.25):
                        x = -1.6 + k * 3.2 / (n - 1)
                        t.caja((x, y + f, 0.33), (0.35, 0.3, 0.26), "planta", rot_z=_h(k + i * 7, 2))
            if v == 1 and fino:                       # recién plantado: plantones
                for k in range(8):
                    for f in (-0.25, 0.25):
                        t.caja((-1.7 + k * 0.48, y + f, 0.24), (0.08, 0.08, 0.08), "planta")
            if v == 3:                                # cañas en A de las tomateras
                for k in range(5):
                    x = -1.6 + k * 0.8
                    for s in (-1, 1):
                        t.caja((x, y + s * 0.15, 0.9), (g * 0.5 if fino else 0.34, g * 0.5 if fino else 0.34, 1.4),
                               "madera", rot_x=-s * 0.2)
            if v == 4 and fino:                       # matas secas
                for k in range(3):
                    t.caja((-1.2 + k * 1.1, y, 0.25), (0.3, 0.3, 0.1), "brasa", rot_z=k)
        if v == 2:
            t.cilindro((2.6, -0.4, 0.15), 0.15, 0.3, "Z", 8, "metal")
            t.cilindro((2.6, 0.2, 0.15), 0.15, 0.3, "Z", 8, "metal")
            t.caja((2.7, 0.9, 0.18), (0.4, 0.2, 0.3), "ropa5")                       # regadera
    elif p == 3:                                      # TIENDA
        if v == 0:
            t.tejado((0, 0, 0.0), 4.0, 3.0, 2.0, "militar", vuelo=0.0, aguas=2)
            t.caja((0, -2.0, 0.8), (1.0, 0.04, 1.6), "lona")                         # puerta recogida
        elif v == 1:                                  # lona entre postes
            for x in (-1.8, 1.8):
                for y in (-1.3, 1.3):
                    t.caja((x, y, 1.0 if y < 0 else 0.8), (g, g, 2.0 if y < 0 else 1.6), "madera")
            t.caja((0, 0, 1.9), (4.0, 2.8, 0.04 if fino else 0.34), "lona", rot_x=0.07)
            caja(0.5, 0.3)
            t.caja((-0.6, 0.2, 0.15), (1.9, 0.8, 0.3), "colchon")
        elif v == 2:                                  # iglú civil
            t.esfera((0, 0, 0.0), (1.4, 1.4, 1.1), "ropa4", sub=2 if fino else 1)
            t.caja((0, -1.15, 0.45), (0.7, 0.1, 0.8), "ropa5")
        elif v == 3:                                  # colchones y mantas al aire
            for k in range(3):
                t.caja((-1.2 + k * 1.2, (k % 2) * 0.3, 0.12 + k * 0.0), (1.0, 1.9, 0.22), "colchon", rot_z=_h(k) * 0.4)
                t.caja((-1.2 + k * 1.2, (k % 2) * 0.3, 0.25), (0.9, 1.0, 0.04), ROPA[k + 1], rot_z=_h(k) * 0.4)
        else:                                         # tienda caída
            t.caja((0, 0, 0.12), (4.0, 3.2, 0.24), "militar", rot_z=0.15)
            t.caja((1.0, 1.0, 0.4), (g, 2.0, g), "madera", rot_x=0.3)
    else:                                             # COCINA DE CAMPAÑA
        def mesa(x, y, l=2.0, rz=0.0):
            t.caja((x, y, 0.75), (l, 0.8, 0.05), "madera", rot_z=rz)
            c, s = math.cos(rz), math.sin(rz)
            for dx in (-l / 2 + 0.1, l / 2 - 0.1):
                for dy in (-0.3, 0.3):
                    t.caja((x + dx * c - dy * s, y + dx * s + dy * c, 0.37), (g, g, 0.74), "madera", rot_z=rz)
        if v == 3:                                    # comedor
            for i in range(2):
                y = (i - 0.5) * 2.4
                mesa(0, y, 3.0)
                for s in (-1, 1):
                    t.caja((0, y + s * 0.65, 0.42), (3.0, 0.3, 0.05), "madera")
                    for x in (-1.3, 1.3):
                        t.caja((x, y + s * 0.65, 0.2), (g, 0.25, 0.4), "madera")
        else:
            mesa(0, 0)
            if v in (0, 2):
                t.cilindro((0.4, 0, 0.95), 0.25, 0.35, "Z", lados, "metal")             # olla
                t.cilindro((-1.6, 0.0, 0.3), 0.3, 0.3, "Z", lados, "metal")            # fogón
                t.cilindro((-1.6, 0.0, 0.47), 0.22, 0.04, "Z", lados, "brasa")
                t.cilindro((-1.6, 0.6, 0.3), 0.15, 0.6, "Z", 8, "ceniza")              # bombona (gris)
            if v == 1:                                # fogón de leña
                for k in range(3):
                    t.caja((-1.6, 0, 0.15), (0.25, 0.25, 0.3), "ceniza", rot_z=k * 1.05)
                t.cilindro((-1.6, 0, 0.12), 0.35, 0.06, "Z", lados, "brasa")
                t.cilindro((-1.6, 0, 0.35), 0.15, 0.35, "Z", 6 if fino else 5, "llama", r2=0.04 if fino else 0.17)
                for k in range(4):
                    t.caja((-2.4, -0.4 + k * 0.2, 0.06 + (k % 2) * 0.1), (0.7, 0.1, 0.1), "madera")
            if v == 2:
                for k in range(4):
                    t.caja((1.6, -0.6 + k * 0.4, 0.22), (0.3, 0.3, 0.44), "ropa1")
            if v == 4:
                caja(0.4, 0, 0.775)
                caja(-1.4, 0.2, rz=0.3)
    o = t.objeto(nombre, bisel=0)
    return marcar_piezas(o)


def separar(o, lod):
    return separar_piezas(o, EMISION)
