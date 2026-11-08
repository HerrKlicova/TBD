"""Kit «Pueblo valenciano» para las hileras del norte de Sagunto (sagunto-nivel.md §3b y §10). Teo (relevo), 8-nov 19:10,
aprobado por Lucía (19:09) antes de modelar. Completa casa_pueblo (6-nov), que era el único tipo y se veía repetido.
DE MEMORIA: no hay fotos de Paula de calles del pueblo (pedidas). Medidas SIN VERIFICAR.

VAR = 5·pieza + variante (0..24). Origen en el suelo, en el centro de la huella; fachada a la calle en −Y y
medianeras lisas en ±X, como casa_pueblo, para que se alineen en hilera cada 6 m.
Piezas 0–3: MISMA HUELLA (6 × 10 m) Y MISMA COLISIÓN QUE casa_pueblo (caja de 6 × 10). Álvaro las cambia de una en una
sin tocar calles ni campo de tiro. La casa de patio es más baja por delante, pero su colisión sigue siendo la caja.
  0 Esquina (chaflán de 1,5 m en la esquina −X/−Y; fachada también a −X): DosPlantas, TresPlantas, BajoChaflan
    (comercio en el chaflán), CalAmarilla, Tapiada.
  1 BajoComercial (bajo de 3,6 m entero de escaparate): Bar (toldo y mesas), Ultramarinos (persiana a medias y cajas),
    Farmacia (rótulo liso, SIN cruz), Saqueada (persiana arrancada), Tapiada (tablones).
  2 TresAlturas (balcones corridos): Cal, Ocre, Gris, Ropa (ropa tendida en el balcón), Ruina (tercera planta hundida).
  3 CasaPatio (casa de una planta al fondo, patio con tapia de 2,2 m delante): Porton, Abierto (portón abierto),
    Parra (emparrado), Naranjo (un naranjo en el patio), Derruida (tapia caída).
Pieza 4: Campanario (hito, ~25 m, huella 5 × 5): Ladrillo, Piedra, Reloj, Danado (remate caído), Andamio.
  Condiciones de Lucía: lo coloca Álvaro fuera de las líneas de tiro a los accesos del muro (D-N174; si se cruza con
  alguna, Nacho mide antes). Es DECORADO: no es un puesto de tirador ni se puede usar.
Colores (nada en la banda de la horda, nada vertical en 48403c, nada rojo): cal d4d0cc / f0dc9c / b8b8b8 / 94948c,
zócalo 605c48, carpintería 605c48 (el 543820 en sombra caía en la banda), teja a86c50, alero y balcón 94948c, reja 6c7874, persiana 94948c, rótulo b8b8b8,
toldo 847c34, tablones a86c50, hueco 605c48, planta 70784c, ladrillo a86c50, piedra b8b8b8,
andamio 6c7874.

r2 (8-nov, 20:30, aprobada por Lucía 20:15) con las fotos de Paula (referencias-reales/sagunt_calles, solo como guía):
  · zócalo de 1,2 m que cambia por casa: 605c48, gris 94948c o verde azulado 6c7874 (el único de la paleta de 32;
    L* 49, lejos del d4d0cc de selección, L* 84). Cal también en b4a080 (crema ocre de las fotos).
  · balcón volado de forja (pasamanos, zócalo de pletina y barrotes) en LOD0; en LOD1 sigue siendo la losa.
  · persianas enrollables con su cajón en las ventanas de las plantas altas, cada una bajada a una altura.
  · rejas de barrotes en las ventanas de la planta baja (LOD0).
  · Campanario v1 «Silleria» (antes «Piedra»): torre de sillería maciza, un vano alto de medio punto por cara y
    terraza plana con pretil, al estilo de El Salvador. Sin chapitel.
  Balcones, rejas y persianas son FACHADA: no cambian la colisión (caja de 6 × 10) ni la línea de visión (D-N174).
  Medidas y pivotes iguales que en r1.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 25
PIEZA, V = VAR // 5, VAR % 5
NOMBRES_P = ["Esquina", "BajoComercial", "TresAlturas", "CasaPatio", "Campanario"]
NOMBRES_V = [["DosPlantas", "TresPlantas", "BajoChaflan", "CalAmarilla", "Tapiada"],
             ["Bar", "Ultramarinos", "Farmacia", "Saqueada", "Tapiada"],
             ["Cal", "Ocre", "Gris", "Ropa", "Ruina"],
             ["Porton", "Abierto", "Parra", "Naranjo", "Derruida"],
             ["Ladrillo", "Silleria", "Reloj", "Danado", "Andamio"]]
_CAL = {(0, 3): "f0dc9c", (2, 1): "f0dc9c", (2, 2): "b8b8b8", (1, 1): "f0dc9c", (3, 2): "f0dc9c",
        (0, 1): "b4a080", (1, 2): "b4a080", (3, 0): "b4a080"}
_ZOC = {0: "6c7874", 1: "94948c", 2: "605c48", 3: "6c7874", 4: "94948c"}     # zócalo por variante (verde azulado,
_ZOC_P = {0: 0, 1: 1, 2: 2, 3: 3, 4: 0}                                       # gris o pardo), desplazado por pieza
PIEZAS = ["cal", "zocalo", "carpinteria", "tejado", "alero", "balcon", "reja", "persiana", "rotulo", "toldo",
          "tablas", "hueco", "planta", "ladrillo", "piedra", "andamio", "ropa", "ropa2", "tronco", "silleria", "junta"]
COLORES = {"cal": _CAL.get((PIEZA, V), "d4d0cc"), "zocalo": _ZOC[(V + _ZOC_P[PIEZA]) % 5], "carpinteria": "605c48", "tejado": "a86c50",
           "alero": "94948c", "balcon": "94948c", "reja": "6c7874", "persiana": "94948c", "rotulo": "b8b8b8",
           "toldo": "847c34", "tablas": "a86c50", "hueco": "605c48", "planta": "70784c", "ladrillo": "a86c50",
           "piedra": "b8b8b8", "andamio": "6c7874", "ropa": "d4d0cc", "ropa2": "847c34", "tronco": "6c7874",
           "silleria": "b4a080", "junta": "94948c"}
FACCION = "neutro"
PRESUPUESTO = {0: (60, 3500), 1: (40, 1000)}
ZOC_H = 1.2
FRENTE, FONDO, PLANTA = 6.0, 10.0, 3.2


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_PuebloValenciano", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    m = 0.34
    g = lambda a: a if fino else max(a, m)
    p, v = PIEZA, V
    hx, hy = FRENTE / 2, FONDO / 2
    yf = -hy - 0.03                                   # plano de la fachada

    def hueco(x, z, w, h, y=yf, eje="Y", pieza="carpinteria"):
        if eje == "Y":
            t.caja((x, y, z), (w, 0.06, h), pieza)
        else:                                         # fachada lateral −X
            t.caja((y, x, z), (0.06, w, h), pieza)

    def balcon(x, z, w, y=-hy, eje="Y", corrido=False):
        if eje == "Y":
            t.caja((x, y - 0.3, z + 0.05), (w, 0.6, 0.12), "balcon")
        else:
            t.caja((y - 0.3, x, z + 0.05), (0.6, w, 0.12), "balcon")
        if fino:
            forja(x, z, w, y, eje)

    def forja(x, z, w, y, eje):
        """Barandilla de forja: pasamanos, pletina baja, barrotes y laterales (solo LOD0, es fachada)."""
        def c(px, py, pz, sx, sy, sz):                # px a lo largo de la fachada, py hacia fuera
            if eje == "Y":
                t.caja((x + px, y - py, pz), (sx, sy, sz), "reja")
            else:
                t.caja((y - py, x + px, pz), (sy, sx, sz), "reja")
        c(0, 0.57, z + 1.06, w + 0.02, 0.06, 0.05)                    # pasamanos
        c(0, 0.57, z + 0.2, w - 0.02, 0.05, 0.07)                     # pletina
        n = max(3, int(w / 0.25))
        for k in range(n):
            c(-w / 2 + (k + 0.5) * w / n, 0.565, z + 0.63, 0.03, 0.03, 0.82)
        for sx in (-1, 1):                                            # laterales
            c(sx * (w / 2 - 0.02), 0.3, z + 1.06, 0.045, 0.52, 0.045)
            c(sx * (w / 2 - 0.025), 0.3, z + 0.63, 0.03, 0.03, 0.82)

    def persiana(x, z, w, h, k, y=yf, eje="Y"):
        """Persiana enrollable bajada a una altura y su cajón encima del hueco (fachada, LOD0 y LOD1)."""
        f = 0.25 + 0.55 * _h(x * 7 + z + k, 5)
        hp = max(h * f, 0.36)
        for (pz, sz, sy, pc) in ((z + h / 2 + 0.14, 0.26, 0.16, "persiana"), (z + h / 2 - hp / 2, hp, 0.03, "persiana")):
            if eje == "Y":
                t.caja((x, y - sy / 2 - 0.035, pz), (w + (0.12 if sz == 0.26 else -0.02), sy, sz), pc)
            else:
                t.caja((y - sy / 2 - 0.035, x, pz), (sy, w + (0.12 if sz == 0.26 else -0.02), sz), pc)

    def reja_baja(x, z, w, h, y=yf, eje="Y"):
        """Reja de barrotes de la planta baja, algo volada (solo LOD0)."""
        if not fino:
            return
        def c(px, pz, sx, sy, sz):
            if eje == "Y":
                t.caja((x + px, y - 0.12, z + pz), (sx, sy, sz), "reja")
            else:
                t.caja((y - 0.12, x + px, z + pz), (sy, sx, sz), "reja")
        for pz in (-h / 2 - 0.02, h / 2 + 0.02):
            c(0, pz, w + 0.14, 0.05, 0.05)
        n = max(3, int(w / 0.16))
        for k in range(n):
            c(-w / 2 + (k + 0.5) * w / n, 0, 0.025, 0.025, h)

    def cuerpo(plantas, tejado=True):
        alto = plantas * PLANTA
        t.caja((0, 0, alto / 2), (FRENTE, FONDO, alto), "cal")
        t.caja((0, -hy - 0.02, ZOC_H / 2), (FRENTE - 0.02, 0.06, ZOC_H), "zocalo")
        t.caja((0, -hy - 0.15, alto - 0.1), (FRENTE - 0.02, 0.3, 0.2), "alero")
        if tejado:
            t.tejado((0, 0, alto), FRENTE - 0.8, FONDO, 2.0, "tejado", vuelo=0.4, aguas=2)
        for k in range(plantas):                       # patio trasero
            hueco(0.8, k * PLANTA + 1.6, 1.0, 1.2, y=hy + 0.03)
        return alto

    def plantas_altas(plantas, corrido=False, desde=1):
        for k in range(desde, plantas):
            z = k * PLANTA
            for x in (-1.4, 1.4):
                hueco(x, z + 1.2, 1.0, 2.2)
                persiana(x, z + 1.2, 1.0, 2.2, k)
            if corrido:
                balcon(0, z, FRENTE - 0.6)
            else:
                for x in (-1.4, 1.4):
                    balcon(x, z, 1.5)

    if p == 0:                                        # ESQUINA CON CHAFLÁN
        plantas = 3 if v == 1 else 2
        alto = plantas * PLANTA
        c = 1.5
        # cuerpo en tres trozos para dejar el chaflán en la esquina −X/−Y
        t.caja((c / 2, 0, alto / 2), (FRENTE - c, FONDO, alto), "cal")
        t.caja((-hx + c / 2 + 0.05, c / 2, alto / 2), (c + 0.1, FONDO - c, alto), "cal")
        d = c * math.sqrt(2) - 0.03   # algo menor: sus vértices no deben caer sobre las aristas de las cajas
        t.caja((-hx + c, -hy + c, alto / 2), (d, d, alto), "cal", rot_z=math.pi / 4)
        t.tejado((0, 0, alto), FRENTE - 0.8, FONDO, 2.0, "tejado", vuelo=0.4, aguas=2)
        t.caja((c / 2, -hy - 0.02, ZOC_H / 2), (FRENTE - c, 0.06, ZOC_H), "zocalo")
        t.caja((-hx - 0.02, c / 2, ZOC_H / 2), (0.06, FONDO - c, ZOC_H), "zocalo")
        t.caja((-hx + c / 2 - 0.02, -hy + c / 2 - 0.02, ZOC_H / 2), (d, 0.06, ZOC_H), "zocalo", rot_z=-math.pi / 4)
        ch = (-hx + c / 2 - 0.05, -hy + c / 2 - 0.05)  # centro de la cara del chaflán
        for k in range(plantas):
            z = k * PLANTA
            if k == 0:
                if v == 2:                            # comercio en el chaflán
                    t.caja((ch[0], ch[1], 1.5), (1.6, 0.08, 2.8), "persiana", rot_z=-math.pi / 4)
                    t.caja((ch[0] - 0.05, ch[1] - 0.05, 3.0), (1.9, 0.1, 0.4), "rotulo", rot_z=-math.pi / 4)
                else:
                    t.caja((ch[0], ch[1], 1.2), (1.0, 0.08, 2.3), "carpinteria", rot_z=-math.pi / 4)
                hueco(1.5, 1.6, 1.1, 1.4)
                hueco(2.0, 1.6, 1.1, 1.4, y=-hx - 0.03, eje="X")
                if v != 4:
                    reja_baja(1.5, 1.6, 1.1, 1.4)
                    reja_baja(2.0, 1.6, 1.1, 1.4, y=-hx - 0.03, eje="X")
                if v == 4:
                    for (x, e) in ((1.5, "Y"), (2.0, "X")):
                        for zz in (1.3, 1.8):
                            if e == "Y":
                                t.caja((x, yf - 0.08, zz), (1.3, 0.05, 0.3), "tablas")
                            else:
                                t.caja((-hx - 0.11, x, zz), (0.05, 1.3, 0.3), "tablas")
            else:
                hueco(1.5, z + 1.2, 1.0, 2.2)
                hueco(-0.5, z + 1.2, 1.0, 2.2, y=-hx - 0.03, eje="X")
                hueco(2.5, z + 1.2, 1.0, 2.2, y=-hx - 0.03, eje="X")
                if v != 4:
                    persiana(1.5, z + 1.2, 1.0, 2.2, k)
                    persiana(-0.5, z + 1.2, 1.0, 2.2, k, y=-hx - 0.03, eje="X")
                    persiana(2.5, z + 1.2, 1.0, 2.2, k + 1, y=-hx - 0.03, eje="X")
                balcon(1.5, z, 1.5)
                balcon(-0.5, z, 1.5, y=-hx, eje="X")
                balcon(2.5, z, 1.5, y=-hx, eje="X")
                t.caja((ch[0], ch[1], z + 1.2), (0.9, 0.08, 2.2), "carpinteria", rot_z=-math.pi / 4)
                if v == 4:
                    t.caja((1.5, yf - 0.08, z + 1.3), (1.1, 0.05, 0.3), "tablas")
        t.caja((c / 2, -hy - 0.17, alto - 0.1), (FRENTE - c, 0.3, 0.2), "alero")
        t.caja((-hx - 0.17, c / 2, alto - 0.1), (0.3, FONDO - c, 0.2), "alero")

    elif p == 1:                                      # BAJO COMERCIAL
        plantas = 3 if v in (1, 3) else 2
        cuerpo(plantas)
        t.caja((0, yf - 0.01, 3.35), (FRENTE - 0.2, 0.08, 0.5), "rotulo" if v != 4 else "cal")   # rótulo
        if v == 0:                                    # bar: escaparate y toldo, mesas fuera
            hueco(-0.6, 1.4, 3.8, 2.6)
            hueco(2.2, 1.2, 1.0, 2.3)
            t.caja((-0.6, -hy - 0.8, 2.8), (4.2, 1.6, 0.05), "toldo", rot_x=0.3)
            for k in range(2):
                x = -1.6 + k * 2.0
                t.cilindro((x, -hy - 1.6, 0.73), 0.4, 0.04, "Z", 8 if fino else 6, "persiana")
                t.caja((x, -hy - 1.6, 0.36), (g(0.06), g(0.06), 0.72), "reja")
        elif v == 1:                                  # ultramarinos: persiana a medias y cajas
            t.caja((-0.3, yf - 0.02, 2.4), (4.6, 0.06, 1.6), "persiana")
            hueco(-0.3, 0.8, 4.6, 1.5, pieza="hueco")
            for k in range(3):
                t.caja((-1.6 + k * 1.1, -hy - 0.5, 0.25), (0.7, 0.5, 0.5), "tablas", rot_z=_h(k) * 0.4)
        elif v == 2:                                  # farmacia: escaparate y rótulo liso (sin cruz)
            hueco(-0.6, 1.4, 3.6, 2.4)
            hueco(2.1, 1.2, 1.1, 2.3)
            t.caja((hx - 0.4, -hy - 0.45, 3.0), (0.08, 0.8, 0.8), "rotulo")                  # banderola lisa
        elif v == 3:                                  # saqueada: persiana arrancada
            hueco(-0.3, 1.4, 4.6, 2.6, pieza="hueco")
            t.caja((0.3, -hy - 1.3, 0.06), (4.0, 1.4, 0.06), "persiana", rot_z=0.25)
            for k in range(4):
                t.caja((-2 + k * 1.2, -hy - 0.6 - _h(k), 0.05), (0.4, 0.3, 0.1), "rotulo", rot_z=_h(k, 3) * 3)
        else:                                         # tapiada con tablones
            hueco(-0.3, 1.4, 4.6, 2.6, pieza="hueco")
            for k in range(5):
                t.caja((-0.3, yf - 0.08, 0.5 + k * 0.5), (4.8, 0.06, 0.3), "tablas", rot_y=(_h(k) - 0.5) * 0.08)
        plantas_altas(plantas)

    elif p == 2:                                      # TRES ALTURAS, BALCONES CORRIDOS
        ruina = v == 4
        cuerpo(2 if ruina else 3, tejado=not ruina)
        hueco(-1.4, 1.2, 1.1, 2.4)
        hueco(1.3, 1.6, 1.1, 1.4)
        reja_baja(1.3, 1.6, 1.1, 1.4)
        plantas_altas(3 if not ruina else 2, corrido=True)
        if v == 3:                                    # ropa tendida en el balcón de la 2.ª
            for k in range(5):
                t.caja((-2.0 + k * 1.0, -hy - 0.85, 4.6 + 0.35), (0.5, g(0.04), 0.6 + _h(k) * 0.3),
                       "ropa" if k % 2 else "ropa2")
        if ruina:                                     # tercera planta hundida: muros rotos y vigas
            z0 = 2 * PLANTA
            t.caja((-hx + 0.15, 0, z0 + 1.15), (0.3, FONDO, 2.4), "cal")
            t.caja((hx - 0.15, 1.0, z0 + 0.75), (0.3, 8.0, 1.6), "cal")
            t.caja((-1.5, -hy + 0.15, z0 + 0.65), (3.0, 0.3, 1.4), "cal")
            for k in range(4):
                t.caja((-2 + k * 1.3, 0.5, z0 + 0.4 + _h(k) * 0.6), (g(0.2), 8.0, g(0.2)), "tablas",
                       rot_x=(_h(k, 2) - 0.5) * 0.3)
            t.caja((0, 0, z0 + 0.1), (5.4, 9.4, 0.2), "hueco")

    elif p == 3:                                      # CASA DE PATIO (una planta al fondo, tapia delante)
        t.caja((0, 2.25, 1.8), (FRENTE, 5.5, 3.6), "cal")
        t.tejado((0, 2.25, 3.6), FRENTE - 0.8, 5.5, 1.6, "tejado", vuelo=0.4, aguas=2)
        t.caja((0, -0.53, ZOC_H / 2), (FRENTE - 0.02, 0.06, ZOC_H), "zocalo")
        hueco(-1.4, 1.2, 1.0, 2.3, y=-0.53)
        hueco(1.4, 1.6, 1.0, 1.3, y=-0.53)
        reja_baja(1.4, 1.6, 1.0, 1.3, y=-0.53)
        tapia = v != 4
        for sx in (-1, 1):                            # tapias laterales del patio
            t.caja((sx * (hx - 0.15), -2.7, 1.1), (0.3, 4.4, 2.2), "cal")
        if tapia:
            t.caja((-1.9, -hy + 0.15, 1.1), (2.2, 0.3, 2.2), "cal")
            t.caja((1.9, -hy + 0.15, 1.1), (2.2, 0.3, 2.2), "cal")
            t.caja((0, -hy + 0.15, 2.38), (1.6, 0.3, 0.4), "cal")                           # dintel del portón
            if v == 1:                                # portón abierto
                t.caja((-0.8, -hy + 0.75, 1.0), (0.06, 1.2, 2.0), "carpinteria", rot_z=0.3)
                t.caja((0.8, -hy + 0.75, 1.0), (0.06, 1.2, 2.0), "carpinteria", rot_z=-0.3)
            else:
                t.caja((0, -hy + 0.1, 1.0), (1.6, 0.1, 2.0), "carpinteria")
            t.caja((0, -hy + 0.15, 2.23), (FRENTE, 0.4, 0.1), "tejado")                     # albardilla de teja
        else:                                         # tapia caída
            t.caja((-1.9, -hy + 0.15, 0.5), (2.2, 0.3, 1.0), "cal")
            t.caja((1.5, -hy - 0.6, 0.2), (3.0, 1.6, 0.4), "cal", rot_z=0.2)
            for k in range(5):
                t.caja((-0.5 + k * 0.6, -hy - 1.2 - _h(k) * 0.5, 0.12), (0.5, 0.3, 0.24), "tejado",
                       rot_z=_h(k, 4) * 3)
        if v == 2:                                    # emparrado
            for (x, y) in ((-2.4, -1.0), (2.4, -1.0), (-2.4, -3.5), (2.4, -3.5)):
                t.caja((x, y, 1.25), (g(0.1), g(0.1), 2.5), "reja")
            t.caja((0, -2.25, 2.55), (5.2, 3.0, 0.15), "planta")
        if v == 3:                                    # naranjo en el patio
            t.caja((1.2, -2.5, 0.6), (g(0.2), g(0.2), 1.2), "tronco")
            t.esfera((1.2, -2.5, 1.9), (1.3, 1.3, 1.0), "planta", sub=1 if not fino else 2)

    elif v == 1:                                      # CAMPANARIO DE SILLERÍA (estilo El Salvador, decorado)
        L, H = 5.0, 22.0
        t.caja((0, 0, 0.5), (L + 0.4, L + 0.4, 1.0), "silleria")                         # zarpa
        t.caja((0, 0, H / 2), (L, L, H), "silleria")                                     # caña maciza
        if fino:                                                                         # hiladas marcadas
            for z in (4.0, 8.0, 12.0, 16.0):
                t.caja((0, 0, z), (L + 0.04, L + 0.04, 0.06), "junta")
        t.caja((0, 0, 17.6), (L + 0.24, L + 0.24, 0.3), "junta")                         # imposta del cuerpo
        for k in range(4):                                                               # un vano por cara
            a = k * math.pi / 2
            ux, uy = round(math.cos(a)), round(math.sin(a))
            px, py = ux * (L / 2 + 0.02), uy * (L / 2 + 0.02)
            sx, sy = (1.2, 0.06) if uy else (0.06, 1.2)
            t.caja((px, py, 19.6), (sx, sy, 3.0), "hueco")                               # vano
            t.cilindro((px, py, 21.05), 0.59, 0.07, "Y" if uy else "X", 10 if fino else 6, "hueco")   # medio punto
            t.caja((ux * (L / 2 + 0.05), uy * (L / 2 + 0.05), 18.0), (1.5 if uy else 0.12, 0.12 if uy else 1.5, 0.15),
                   "junta")                                                              # alféizar
        if fino:
            t.cilindro((0, 0, 20.2), 0.55, 1.0, "Z", 10, "alero", r2=0.3)                # campana
        t.caja((0, 0, H + 0.12), (L + 0.3, L + 0.3, 0.24), "junta")                      # cornisa
        for sx, sy, lx, ly in ((0, 1, L + 0.2, 0.36), (0, -1, L + 0.2, 0.36), (1, 0, 0.36, L - 0.53), (-1, 0, 0.36, L - 0.53)):
            t.caja((sx * (L / 2 - 0.08), sy * (L / 2 - 0.08), H + 0.75), (lx, ly, 1.0), "silleria")   # pretil
        t.caja((0, 0, H + 0.3), (L - 0.9, L - 0.9, 0.12), "tejado")                      # terraza
        t.caja((L / 2 - 0.45, L / 2 - 0.45, H + 1.6), (g(0.1), g(0.1), 1.6), "reja")     # pararrayos
    else:                                             # CAMPANARIO (hito, decorado)
        mat = "piedra" if v == 1 else "ladrillo"
        L, H = 5.0, 18.0
        roto = v == 3
        t.caja((0, 0, 0.6), (L + 0.6, L + 0.6, 1.2), "piedra")                          # zócalo de sillería
        t.caja((0, 0, H / 2), (L, L, H), mat)                                            # caña
        for z in (6.0, 12.0):                                                            # impostas
            t.caja((0, 0, z), (L + 0.2, L + 0.2, 0.3), "piedra" if mat == "ladrillo" else "alero")
        for k in range(4):                                                               # saeteras
            a = k * math.pi / 2
            ux, uy = math.cos(a), math.sin(a)
            t.caja((ux * (L / 2 + 0.03), uy * (L / 2 + 0.03), 9.0), (0.5 if uy else 0.06, 0.06 if uy else 0.5, 1.4),
                   "hueco")
        # cuerpo de campanas con arcos (huecos) en las 4 caras
        zc = H + 2.5
        alto_c = 5.0 if not roto else 3.0
        for k in range(4):                                                               # pilastras de esquina
            sx, sy = (1, 1, -1, -1)[k], (1, -1, 1, -1)[k]
            t.caja((sx * (L / 2 - 0.5), sy * (L / 2 - 0.5), H + alto_c / 2), (1.0, 1.0, alto_c), mat)
        if not roto:
            t.caja((0, 0, H + alto_c - 0.4), (L, L, 0.8), mat)                           # cornisa de los arcos
            t.caja((0, 0, H + alto_c + 0.1), (L + 0.4, L + 0.4, 0.3), "piedra")
        t.caja((0, 0, H + 0.2), (L + 0.1, L + 0.1, 0.4), "piedra")
        for k in range(4):                                                               # huecos de campana
            a = k * math.pi / 2
            ux, uy = round(math.cos(a)), round(math.sin(a))
            t.caja((ux * (L / 2 - 0.2), uy * (L / 2 - 0.2), H + 2.2), (2.6 if uy else 0.3, 0.3 if uy else 2.6, 3.2),
                   "hueco")
        if fino:
            t.cilindro((0, 0, H + 2.2), 0.7, 1.2, "Z", 10, "alero", r2=0.4)              # campana
        if not roto:                                                                     # remate: chapitel de teja
            t.tejado((0, 0, H + alto_c + 0.22), L, L, 3.5, "tejado", vuelo=0.1, aguas=4)
            t.caja((0, 0, H + alto_c + 4.1), (g(0.1), g(0.1), 1.2), "reja")              # veleta
        else:                                                                            # remate caído al pie
            t.caja((3.8, -1.5, 0.8), (3.0, 2.6, 1.6), "tejado", rot_z=0.5, rot_x=0.4)
            for k in range(6):
                t.caja((3 + _h(k) * 2.5, -3 + _h(k, 1) * 3, 0.25), (0.8, 0.5, 0.5), mat, rot_z=_h(k, 2) * 3)
        if v == 2:                                                                       # reloj en la cara −Y
            t.cilindro((0, -L / 2 - 0.05, 15.0), 1.1, 0.1, "Y", 16 if fino else 8, "rotulo")
            if fino:
                t.caja((0, -L / 2 - 0.12, 15.3), (0.08, 0.04, 0.8), "hueco")
                t.caja((0.25, -L / 2 - 0.12, 15.0), (0.6, 0.04, 0.08), "hueco")
        if v == 4:                                                                       # andamio en la cara −Y
            for x in (-2.0, 0.0, 2.0):
                t.caja((x, -L / 2 - 1.0, 8.0), (g(0.08), g(0.08), 16.0), "andamio")
            for z in range(2, 17, 3):
                t.caja((0, -L / 2 - 1.0, z), (4.4, g(0.08), g(0.08)), "andamio")
                t.caja((0, -L / 2 - 0.7, z - 0.05), (4.2, 0.6, 0.05), "tablas")
    return t.objeto(nombre, bisel=0)
