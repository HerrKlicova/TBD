"""Kit «Aquí pasó algo» de Sagunto (sagunto-nivel.md §3, fuera del muro). Teo (relevo), 8-nov 19:10, aprobado por
Lucía (19:04) antes de modelar. Completa las tolva_volcada (6-nov) y la barricada_civil (Castellón). DE MEMORIA: medidas
SIN VERIFICAR.

VAR = 5·pieza + variante (0..19). Origen en el suelo (cota del carril en las piezas de vía), en el centro; lo largo va a
lo largo de X.
Condiciones de Lucía (19:04):
  - El mercancías descarrilado va en una vía muerta o un apartadero que no use ningún tren (lo fija Álvaro).
  - La catenaria caída es solo visual: no corta la vía ni hace daño. El poste caído queda paralelo a la vía, fuera
    del gálibo (y > 2,2 m del eje).
  - El autobús cruzado SUSTITUYE al autobus_cruzado del inventario de Álvaro (11 × 2,6 m, girado 74°, tapa el tiro,
    sin sólido); no se añade.
  - 5 variantes por pieza y nada en la banda de la horda (nada vertical en 48403c: va 605c48).
Piezas:
  0 Locomotora diésel de mercancías (20 m, cabinas en los dos testeros, capó central), la cabeza del descarrilo:
    Descarrilada (de pie, girada 6°), Ladeada (15°, un bogie fuera), Volcada (sobre el costado), Quemada, Aplastada
    (cabina hundida contra la tolva de delante).
  1 Vía rota (tramo de 12 m): Levantada (los carriles doblados hacia arriba), Traviesas (sueltas y amontonadas),
    Socavon (balasto hundido), Cruzada (carril arrancado cruzado), Grava (derrame de la tolva sobre la vía).
  2 Catenaria caída (vano de 15 m junto a la vía, en +Y): Colgando (hilo caído en curva), MensulaRota, Inclinado
    (poste a 25°), Caido (poste en el suelo, paralelo a la vía), Enredado (hilo hecho un ovillo al pie del poste).
  3 Autobús cruzado (11 × 2,6 m) de la barricada de la entrada del pueblo: Cruzado, Barricada (con muebles y
    colchones), Volcado, Quemado, Empujado (con un coche contra el morro).
Colores: loco 94948c con franja 847c34 y techo 6c7874, cabina 605c48, bastidor y bogies 605c48, ruedas 6c7874,
quemado 605c48 con ceniza 94948c, carril 6c7874, traviesa a86c50, balasto 94948c, hoyo 605c48, poste 6c7874,
hilo 605c48, bus b8b8b8 con franja 847c34, lunas 605c48, muebles 845c28, colchón 94948c, coche 6c7874.
"""
import math, os
from mathutils import Matrix, Vector
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 20
PIEZA, V = VAR // 5, VAR % 5
NOMBRES_P = ["Locomotora", "ViaRota", "CatenariaCaida", "AutobusCruzado"]
NOMBRES_V = [["Descarrilada", "Ladeada", "Volcada", "Quemada", "Aplastada"],
             ["Levantada", "Traviesas", "Socavon", "Cruzada", "Grava"],
             ["Colgando", "MensulaRota", "Inclinado", "Caido", "Enredado"],
             ["Cruzado", "Barricada", "Volcado", "Quemado", "Empujado"]]
PIEZAS = ["loco", "franja", "techo", "cabina", "bastidor", "rueda", "quemado", "ceniza", "carril", "traviesa",
          "balasto", "hoyo", "poste", "hilo", "bus", "luna", "mueble", "colchon", "coche", "zapata"]
COLORES = {"loco": "94948c", "franja": "847c34", "techo": "6c7874", "cabina": "605c48", "bastidor": "605c48",
           "rueda": "6c7874", "quemado": "605c48", "ceniza": "94948c", "carril": "6c7874", "traviesa": "a86c50",
           "balasto": "94948c", "hoyo": "605c48", "poste": "6c7874", "hilo": "605c48", "bus": "b8b8b8",
           "luna": "605c48", "mueble": "845c28", "colchon": "94948c", "coche": "6c7874", "zapata": "94948c"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0
PRESUPUESTO = {0: (20, 3000), 1: (10, 1000)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}


def _h(k, s=0):
    x = math.sin(k * 12.9898 + s * 78.233) * 43758.5453
    return x - math.floor(x)


def construir(nombre="SM_AquiPasoAlgo", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    m = 0.34
    g = lambda a: a if fino else max(a, m)
    lados = 10 if fino else 6
    p, v = PIEZA, V
    bm = t.bm

    def marca():
        bm.verts.ensure_lookup_table()
        return len(bm.verts)

    def girar(n0, M, piv=(0, 0, 0)):
        """Gira (Matrix 3×3) los vértices creados desde n0 alrededor de piv."""
        bm.verts.ensure_lookup_table()
        P = Vector(piv)
        for vv in bm.verts[n0:]:
            vv.co = M @ (vv.co - P) + P

    def mover(n0, d):
        bm.verts.ensure_lookup_table()
        for vv in bm.verts[n0:]:
            vv.co += Vector(d)

    if p == 0:                                        # LOCOMOTORA DIÉSEL DE MERCANCÍAS
        q = v == 3
        cuerpo = "quemado" if q else "loco"
        n0 = marca()
        t.caja((0, 0, 0.75), (19.0, 2.6, 0.5), "bastidor")                                   # bastidor
        for bx in (-6.5, 6.5):                                                                # bogies de 3 ejes
            t.caja((bx, 0, 0.5), (3.6, 2.4, 0.6), "bastidor")
            for k in (-1.4, 0, 1.4):
                for sy in (-1.0, 1.0):
                    t.cilindro((bx + k, sy * (1.0 if fino else 1.05), 0.55), 0.55, g(0.12), "Y", lados, "rueda")
        t.caja((0, 0, 2.3), (13.0, 2.4, 2.6), cuerpo)                                         # capó central
        t.caja((0, 0, 3.65), (13.2, 2.5, 0.12), "ceniza" if q else "techo")
        if not q:
            t.caja((0, -1.21, 1.55), (19.0, g(0.04), 0.35), "franja")
            t.caja((0, 1.21, 1.55), (19.0, g(0.04), 0.35), "franja")
        for sx in (-1, 1):                                                                    # cabinas
            aplast = v == 4 and sx == 1
            h = 2.2 if aplast else 3.2
            t.caja((sx * 8.0, 0, 1.0 + h / 2), (3.0, 2.8, h), cuerpo, rot_y=0.25 if aplast else 0.0)
            t.caja((sx * 8.0, 0, 1.0 + h + 0.06), (3.1, 2.9, 0.12), "ceniza" if q else "techo",
                   rot_y=0.25 if aplast else 0.0)
            if not aplast:
                t.caja((sx * 9.52, 0, 3.2), (g(0.04), 2.2, 0.8), "cabina")                    # parabrisas
                for sy in (-1.41, 1.41):
                    t.caja((sx * 8.3, sy, 3.2), (1.2, g(0.04), 0.7), "cabina")
        if fino and not q:
            for k in range(6):                                                                # rejillas del capó
                t.caja((-5 + k * 2.0, -1.21, 2.6), (1.2, 0.03, 0.9), "techo")
        if q:
            for k in range(5):
                t.caja((-6 + k * 3.0, (_h(k) - 0.5) * 1.2, 3.75), (1.5, 1.0, 0.1), "ceniza", rot_z=_h(k, 1))
        # pose (el vuelco va en la malla; el origen, al suelo bajo el centro)
        if v == 0:
            girar(n0, Matrix.Rotation(math.radians(6), 3, "Z"))
        elif v == 1:
            girar(n0, Matrix.Rotation(math.radians(15), 3, "X"), (0, 1.3, 0))
            girar(n0, Matrix.Rotation(math.radians(-4), 3, "Z"))
        elif v == 2:
            girar(n0, Matrix.Rotation(math.radians(88), 3, "X"), (0, 1.3, 0))
            girar(n0, Matrix.Rotation(math.radians(-8), 3, "Z"))
            mover(n0, (0, 0, 0.05))
        elif v == 4:
            girar(n0, Matrix.Rotation(math.radians(9), 3, "Z"))
        if v in (1, 2, 4):                                                                    # balasto removido
            t.caja((3.0, -2.6, 0.15), (7.0, 2.0, 0.3), "balasto", rot_z=0.15)
        if v == 4:                                                                            # chatarra delante
            for k in range(4):
                t.caja((10.8 + _h(k) * 1.5, -0.8 + k * 0.5, 0.25), (g(0.9), g(0.5), 0.5), "bastidor",
                       rot_z=_h(k, 3) * 3)

    elif p == 1:                                      # VÍA ROTA (12 m)
        def traviesa(x, y=0.0, rz=0.0, z=0.09):
            t.caja((x, y, z), (0.26 if fino else m, 2.6, 0.18), "traviesa", rot_z=rz)
        t.caja((0, 0, -0.05), (12.0, 3.4, 0.1), "balasto")
        if v == 0:                                    # carriles doblados hacia arriba en el centro
            for k in range(20):
                x = -5.7 + k * 0.6
                if abs(x) > 1.5:
                    traviesa(x)
            for sy in (-0.72, 0.72):
                for x0, x1 in ((-6, -1.5), (1.5, 6)):
                    t.caja(((x0 + x1) / 2, sy, 0.25), (abs(x1 - x0), g(0.07), g(0.15)), "carril")
                for s in (-1, 1):                     # tramos que se levantan
                    t.caja((s * 0.9, sy + s * 0.1, 0.9), (1.6, g(0.07), g(0.15)), "carril", rot_y=s * 0.9)
            t.caja((0, 0, 0.05), (3.0, 3.0, 0.1), "hoyo")
        elif v == 1:                                  # traviesas sueltas y amontonadas
            for sy in (-0.72, 0.72):
                t.caja((-2.5, sy, 0.25), (7.0, g(0.07), g(0.15)), "carril")
            for k in range(11):
                traviesa(-5.7 + k * 0.6)
            for k in range(8):
                traviesa(2.4 + _h(k) * 3, 0.5 + _h(k, 1) * 1.5, _h(k, 2) * 3, 0.09 + (k % 3) * 0.18)
        elif v == 2:                                  # socavón
            t.caja((0.5, 0, 0.0), (4.5, 3.4, 0.12), "hoyo")
            for k in range(20):
                x = -5.7 + k * 0.6
                if not (-1.6 < x < 2.6):
                    traviesa(x)
            for sy in (-0.72, 0.72):
                t.caja((0, sy, 0.1), (12.0, g(0.07), g(0.15)), "carril", rot_y=0.0)
            for k in range(3):                         # traviesas colgando sobre el hueco
                traviesa(-1.0 + k * 1.2, 0, 0.0, 0.0 - k * 0.05)
        elif v == 3:                                  # carril arrancado y cruzado
            for k in range(20):
                traviesa(-5.7 + k * 0.6)
            t.caja((0, -0.72, 0.25), (12.0, g(0.07), g(0.15)), "carril")
            t.caja((1.0, 0.6, 0.3), (9.0, g(0.07), g(0.15)), "carril", rot_z=0.35)
        else:                                         # derrame de grava de la tolva
            for k in range(20):
                traviesa(-5.7 + k * 0.6)
            for sy in (-0.72, 0.72):
                t.caja((0, sy, 0.25), (12.0, g(0.07), g(0.15)), "carril")
            for k, (x, r, hh) in enumerate(((-1.5, 2.2, 0.9), (1.8, 1.6, 0.6), (3.8, 1.0, 0.4))):
                t.cilindro((x, -0.6 + _h(k) * 0.6, hh / 2), r, hh, "Z", 8 if fino else 6, "balasto", r2=r * 0.3)

    elif p == 2:                                      # CATENARIA CAÍDA (junto a la vía, en +Y)
        Y = 3.2                                       # eje del poste (fuera del gálibo)
        def poste(x, alto=7.5):
            t.caja((x, Y, 0.3), (0.9, 0.9, 0.6), "zapata")
            t.caja((x, Y, alto / 2), (g(0.3), g(0.3), alto), "poste")
            if fino:
                for k in range(6):                    # celosía: travesaños
                    t.caja((x, Y - 0.16, 0.8 + k * 1.1), (0.3, 0.02, 0.05), "poste")
        def hilo_suelo(x0, x1, y, pieza="hilo"):
            t.caja(((x0 + x1) / 2, y, 0.03), (abs(x1 - x0), g(0.05), g(0.05)), pieza)
        if v in (0, 1, 4):
            n0 = marca()
            poste(0)
            if v == 0:                                # ménsula en su sitio, hilo caído en curva hasta el suelo
                t.caja((0, Y - 1.6, 6.8), (g(0.12), 3.2, g(0.12)), "poste")
                for k in range(5):
                    z = 6.6 - k * 1.45
                    t.caja((-0.3 - k * 1.2, Y - 3.1 + k * 0.15, max(0.05, z)), (1.95, g(0.04), g(0.04)), "hilo",
                           rot_y=0.85)
                hilo_suelo(-6.0, 7.5, Y - 2.4)
            elif v == 1:                              # ménsula rota colgando
                t.caja((0, Y - 0.5, 6.8), (g(0.12), 1.0, g(0.12)), "poste")
                t.caja((0, Y - 1.0, 5.5), (g(0.12), g(0.12), 2.6), "poste", rot_x=0.3)
                hilo_suelo(-7.5, 7.5, Y - 1.4)
                t.caja((4.0, Y - 1.0, 0.04), (3.0, g(0.05), g(0.05)), "hilo", rot_z=0.5)
            else:                                     # hilo enredado al pie
                t.caja((0, Y - 1.6, 6.8), (g(0.12), 3.2, g(0.12)), "poste")
                for k in range(7 if fino else 4):
                    a = k * 0.9
                    t.caja((0.8 + math.cos(a) * 0.9, Y - 1.2 + math.sin(a) * 0.9, 0.08 + k * 0.04),
                           (1.8, g(0.04), g(0.04)), "hilo", rot_z=a + 1.2)
                t.caja((0.2, Y - 1.0, 3.5), (g(0.04), g(0.04), 6.6), "hilo", rot_x=0.2)
        elif v == 2:                                  # poste inclinado a 25°, hacia fuera de la vía
            n0 = marca()
            poste(0)
            t.caja((0, Y - 1.6, 6.8), (g(0.12), 3.2, g(0.12)), "poste")
            girar(n0, Matrix.Rotation(math.radians(-25), 3, "X"), (0, Y, 0))
            hilo_suelo(-7.5, 7.5, Y - 0.3)
            t.caja((0, Y - 0.2, 0.25), (1.6, 1.4, 0.5), "balasto")                       # zapata descalzada
        else:                                         # poste caído en el suelo, paralelo a la vía
            t.caja((-3.5, Y, 0.3), (0.9, 0.9, 0.6), "zapata")
            t.caja((0.2, Y + 0.3, 0.2), (7.5, g(0.3), g(0.3)), "poste", rot_z=0.04)
            if fino:
                for k in range(6):
                    t.caja((-2.4 + k * 1.1, Y + 0.3, 0.37), (0.05, 0.3, 0.02), "poste")
            t.caja((4.3, Y + 1.2, 0.08), (g(0.12), 3.0, g(0.12)), "poste", rot_z=0.2)       # ménsula
            hilo_suelo(-7.5, 7.5, Y + 1.8)

    else:                                             # AUTOBÚS CRUZADO (11 × 2,6 m)
        q = v == 3
        caja_c = "quemado" if q else "bus"
        def autobus():
            t.caja((0, 0, 1.75), (11.0, 2.55, 2.6), caja_c)                                  # caja
            t.caja((0, 0, 3.1), (10.8, 2.4, 0.12), "ceniza" if q else "techo")
            if not q:
                for sy in (-1.28, 1.28):
                    t.caja((0, sy, 1.0), (11.0, g(0.04), 0.3), "franja")
            for sy in (-1.28, 1.28):                                                        # lunas laterales
                t.caja((0.4, sy, 2.25), (8.8, g(0.04), 0.9), "luna")
            t.caja((5.51, 0, 2.2), (g(0.04), 2.3, 1.2), "luna")                              # parabrisas
            t.caja((-5.51, 0, 2.5), (g(0.04), 2.0, 0.6), "luna")
            for x in (-3.4, 3.6):
                for sy in (-1.1, 1.1):
                    t.cilindro((x, sy, 0.5), 0.5, g(0.3), "Y", lados, "luna" if q else "rueda")
        if v == 2:                                    # volcado sobre el costado
            n0 = marca()
            autobus()
            girar(n0, Matrix.Rotation(math.radians(90), 3, "X"), (0, 1.28, 0))
            mover(n0, (0, 0, 0.0))
            t.caja((-2.0, -2.8, 0.1), (3.0, 1.5, 0.2), "luna")                               # cristales rotos
        else:
            autobus()
        if v == 1:                                    # barricada: muebles y colchones a los lados
            for k in range(5):
                x = -4.5 + k * 2.2
                t.caja((x, -1.9 - _h(k) * 0.5, 0.45), (1.4, 0.7, 0.9), "mueble", rot_z=_h(k, 1) - 0.5)
            for k in range(3):
                t.caja((-3 + k * 3, 1.9, 0.6), (1.9, 0.25, 1.2), "colchon", rot_x=-0.4, rot_z=_h(k, 2) - 0.5)
        if v == 4:                                    # empujado: un coche contra el morro
            t.caja((7.4, -0.9, 0.7), (4.0, 1.7, 1.0), "coche", rot_z=0.7)
            t.caja((7.3, -0.9, 1.4), (2.2, 1.5, 0.5), "coche", rot_z=0.7)
            t.caja((7.3, -0.9, 1.4), (2.0, 1.55, 0.35), "luna", rot_z=0.7)
    return t.objeto(nombre, bisel=0)
