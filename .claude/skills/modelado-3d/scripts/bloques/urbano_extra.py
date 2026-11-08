"""Mobiliario urbano para la plaza de Castellón y la calle de la estación de Nules (Teo, 8-nov 18:52, elegido por Teo
para dar ambiente; completa urbano/palmera, farola y banco de Nules). DE MEMORIA (SIN VERIFICAR).

VAR = 5·pieza + variante (0..24). Origen en el suelo, en el centro de la pieza; el frente a −Y.
Colisión (Lucía, 18:46): contenedores, señales y semáforos SIN colisión; el quiosco y la marquesina de bus son SÓLIDOS
y tapan la visión.
Nada rojo ni ámbar: las señales llevan la cara en blanco b8b8b8 con el borde 6c7874 (sin el rojo real: solo se lee
la forma) y los semáforos van apagados. Sin azul (404460 es el rim de lo propio).
Piezas:
  0 Contenedor de basura (1,4 × 1,0 × 1,3 m): verde con tapa cerrada, gris con tapa abierta, de reciclaje (crema),
    volcado con bolsas, y tres en fila.
  1 Marquesina de bus (4 × 1,5 × 2,5 m): entera, sin cristales, con el panel de anuncios, con bolsas y mantas
    (refugio), y caída.
  2 Quiosco (2,4 × 2,4 × 2,6 m): cerrado con persiana, abierto con toldo, de lotería (más bajo), saqueado (persiana
    arrancada), y quemado.
  3 Señal: rectangular de dirección, redonda, triangular, doble en un poste, y doblada.
  4 Semáforo: simple, con brazo sobre la calzada, peatonal doble, doblado, y caído.
Colores: metal 94948c, poste 6c7874, verde 70784c, gris 6c7874, crema f0dc9c, cristal 6c7874, cara b8b8b8, toldo
847c34, persiana 94948c, madera 845c28, bolsas 605c48, quemado 605c48, cuerpo del semáforo 605c48.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 25
PIEZA, V = VAR // 5, VAR % 5
NOMBRES_P = ["Contenedor", "ParadaBus", "Quiosco", "Senal", "Semaforo"]
NOMBRES_V = [["Verde", "Abierto", "Reciclaje", "Volcado", "Fila"], ["Entera", "SinCristales", "Anuncio", "Refugio", "Caida"],
             ["Cerrado", "Abierto", "Loteria", "Saqueado", "Quemado"], ["Direccion", "Redonda", "Triangular", "Doble", "Doblada"],
             ["Simple", "Brazo", "Peatonal", "Doblado", "Caido"]]
PIEZAS = ["metal", "poste", "verde", "gris", "crema", "cristal", "cara", "toldo", "persiana", "madera", "bolsas",
          "quemado", "cuerpo"]
COLORES = {"metal": "94948c", "poste": "6c7874", "verde": "70784c", "gris": "6c7874", "crema": "f0dc9c",
           "cristal": "6c7874", "cara": "b8b8b8", "toldo": "847c34", "persiana": "94948c", "madera": "845c28",
           "bolsas": "605c48", "quemado": "605c48", "cuerpo": "605c48"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 1500), 1: (10, 500)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_UrbanoExtra", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.09 if fino else 0.34
    p, v = PIEZA, V
    lados = 10 if fino else 6
    if p == 0:                                        # CONTENEDOR
        def cont(x, y, pieza, abierto=False, volcado=False, rz=0.0):
            if volcado:
                t.caja((x, y, 0.5), (1.4, 1.3, 1.0), pieza, rot_z=rz)
                t.caja((x, y - 0.9, 0.3), (1.4, 0.06, 1.0), pieza, rot_z=rz, rot_x=0.3)
                for k in range(4):
                    t.caja((x + (k - 1.5) * 0.45, y - 1.6 - _h(k) * 0.6, 0.18), (0.4, 0.4, 0.36), "bolsas",
                           rot_z=_h(k, 1) * 3)
                return
            t.caja((x, y, 0.62), (1.4, 1.0, 1.1), pieza, rot_z=rz)
            for sx in (-0.55, 0.55):
                t.cilindro((x + sx, y, 0.06), 0.06 if fino else 0.17, 0.12, "Z", 6, "poste")
            if abierto:
                t.caja((x, y + 0.6, 1.5), (1.45, 0.06, 0.7), pieza, rot_x=-0.3, rot_z=rz)
            else:
                t.caja((x, y, 1.22), (1.45, 1.05, 0.1), pieza, rot_x=-0.04, rot_z=rz)
        if v == 4:
            for i, pz in enumerate(("verde", "gris", "crema")):
                cont(-1.6 + i * 1.6, 0, pz)
        else:
            cont(0, 0, {0: "verde", 1: "gris", 2: "crema", 3: "verde"}[v], abierto=v == 1, volcado=v == 3)
    elif p == 1:                                      # MARQUESINA DE BUS
        caida = v == 4
        rx = 1.2 if caida else 0.0
        if caida:                                     # todo el conjunto tumbado hacia −Y
            t.caja((0, -1.2, 0.08), (4.2, 2.5, 0.1), "metal")
            t.caja((0, -0.2, 0.05), (4.0, 1.4, 0.06), "cristal")
            t.caja((0, 0.4, 0.25), (3.0, 0.4, 0.45), "madera")
            return t.objeto(nombre, bisel=0)
        for x in (-1.9, 1.9):
            t.caja((x, 0.6, 1.25), (g, g, 2.5), "metal")
        t.caja((0, 0, 2.55), (4.2, 1.6, 0.1), "metal")
        if v != 1:
            t.caja((0, 0.66, 1.35), (3.7, 0.03 if fino else 0.34, 1.9), "cristal")         # trasera
            t.caja((-2.0, 0.1, 1.35), (0.03 if fino else 0.34, 1.0, 1.9), "cristal")
        if v == 2:
            t.caja((2.0, 0.1, 1.2), (0.25, 1.2, 1.8), "cara")                              # panel de anuncios
        t.caja((0, 0.4, 0.45), (2.6, 0.4, 0.06), "madera")                                 # banco
        for x in (-1.1, 1.1):
            t.caja((x, 0.4, 0.22), (g, 0.3, 0.44), "metal")
        if v == 3:                                    # refugio: bolsas y mantas
            for k in range(4):
                t.caja((-1.2 + k * 0.7, 0.1 - _h(k) * 0.3, 0.2), (0.5, 0.45, 0.4), "bolsas", rot_z=_h(k, 2))
            t.caja((0.4, -0.3, 0.03), (1.6, 1.0, 0.05), "toldo", rot_z=0.2)
    elif p == 2:                                      # QUIOSCO
        h = 2.2 if v == 2 else 2.6
        q = v == 4
        muro = "quemado" if q else "verde"
        t.caja((0, 0, h / 2), (2.4, 2.4, h), muro)
        t.caja((0, 0, h + 0.08), (2.8, 2.8, 0.16), "quemado" if q else "metal")
        if v == 0:
            t.caja((0, -1.21, 1.3), (2.0, 0.04, 1.5), "persiana")
        elif v in (1, 2):
            t.caja((0, -1.21, 1.4), (2.0, 0.04, 1.1), "cara")                             # mostrador y revistas
            t.caja((0, -1.7, h - 0.2), (2.6, 1.0, 0.05), "toldo", rot_x=0.35)
        elif v == 3:
            t.caja((0, -1.21, 1.3), (2.0, 0.04, 1.5), "cristal")
            t.caja((0.5, -2.0, 0.05), (2.0, 1.2, 0.06), "persiana", rot_z=0.5)
            for k in range(3):
                t.caja((-0.9 + k * 0.5, -1.8 - _h(k), 0.03), (0.4, 0.3, 0.05), "cara", rot_z=_h(k, 4) * 3)
        else:
            t.caja((0, -1.21, 1.3), (2.0, 0.04, 1.5), "quemado")
    elif p == 3:                                      # SEÑAL
        def poste(x, alto=2.6, ry=0.0):
            t.caja((x, 0, alto / 2), (g * 0.8 if fino else 0.34, g * 0.8 if fino else 0.34, alto), "poste", rot_y=ry)
        def placa(x, z, forma, rx=0.0):
            dy = 0.0 if fino else -0.17           # en el LOD1 el poste mide 0,34: la placa, delante
            k = 1.0 if fino else 1.25
            if forma == "rect":
                t.caja((x, -0.06 + dy, z), (1.2 * k, 0.04, 0.5 * k), "cara", rot_x=rx)
                if fino:
                    t.caja((x, -0.04, z), (1.26, 0.03, 0.56), "poste", rot_x=rx)
            elif forma == "redonda":
                t.cilindro((x, -0.06 + dy, z), 0.35 * k, 0.04, "Y", 14 if fino else 8, "cara")
                if fino:
                    t.cilindro((x, -0.04, z), 0.38, 0.03, "Y", 14, "poste")
            else:                                     # triangular (aprox. con un prisma de 3 lados)
                t.cilindro((x, -0.06 + dy, z), 0.45 * k, 0.04, "Y", 3, "cara")
                if fino:
                    t.cilindro((x, -0.04, z), 0.5, 0.03, "Y", 3, "poste")
        if v == 4:                                    # doblada
            t.caja((0, 0, 0.6), (g * 0.8 if fino else 0.34, g * 0.8 if fino else 0.34, 1.2), "poste")
            t.caja((0, -0.5, 1.4), (g * 0.8 if fino else 0.34, 1.2, g * 0.8 if fino else 0.34), "poste", rot_x=-0.5)
            t.caja((0, -1.1, 1.0), (1.2, 0.5, 0.04), "cara", rot_x=-0.9)
        else:
            poste(0)
            forma = {0: "rect", 1: "redonda", 2: "tri", 3: "rect"}[v]
            placa(0, 2.35, forma)
            if v == 3:
                placa(0, 1.75, "redonda")
    else:                                             # SEMÁFORO
        def semaforo(x, z, rx=0.0):
            t.caja((x, -0.15, z), (0.34, 0.25, 0.9), "cuerpo", rot_x=rx)
            if fino:
                for k in range(3):
                    t.cilindro((x, -0.28, z + 0.28 - k * 0.28), 0.09, 0.03, "Y", 8, "cristal")
        if v == 4:                                    # caído
            t.caja((0, -1.5, 0.08), (0.14 if fino else 0.34, 3.2, 0.14 if fino else 0.34), "poste")
            t.caja((0.1, -3.2, 0.2), (0.34, 0.9, 0.3), "cuerpo")
            t.caja((0, 0, 0.1), (0.4, 0.4, 0.2), "poste")
        elif v == 3:                                  # doblado
            t.caja((0, 0, 0.8), (0.14 if fino else 0.34, 0.14 if fino else 0.34, 1.6), "poste")
            t.caja((0.4, 0, 2.2), (0.14 if fino else 0.34, 0.14 if fino else 0.34, 1.4), "poste", rot_y=0.6)
            t.caja((0.85, -0.15, 2.7), (0.34, 0.25, 0.9), "cuerpo", rot_y=0.6)
        else:
            t.caja((0, 0, 1.6), (0.14 if fino else 0.34, 0.14 if fino else 0.34, 3.2), "poste")
            semaforo(0, 2.6)
            if v == 1:                                # brazo sobre la calzada (−X)
                t.caja((-2.0, 0, 4.4), (4.0, 0.12 if fino else 0.34, 0.12 if fino else 0.34), "poste")
                t.caja((0, 0, 3.8), (0.13 if fino else 0.33, 0.13 if fino else 0.33, 1.3), "poste")
                semaforo(-3.6, 4.0)
            if v == 2:                                # peatonal doble (más bajo, dos caras)
                t.caja((0, 0.15, 1.6), (0.34, 0.25, 0.6), "cuerpo")
    return t.objeto(nombre, bisel=0)
