"""CH-47F Chinook de las FAMET con los rotores en tándem animables (coordinador, 7-nov 01:28; para DESPUÉS del MVP).
Teo, 7-nov-2026.

Fotos de Paula (manifest_vehiculos_et.json, CC BY-SA 2.0, Carlos Menendez San Juan): MV3_chinook_1.jpg (lateral,
HT.17-19 / ET-419), MV3_chinook_2.jpg (3/4 delantero) y MV3_chinook_3.jpg (de frente). Ojo: el manifest dice CH-47D (el
HT.17 de 2010), no el F. Cotas (catalogo.md de Paula): fuselaje de 15,6 m, rotores de 18,29 m, altura de 5,69 m y ejes
separados unos 11,85 m (calculado, SIN VERIFICAR). Alturas de los pilones y de los bujes medidas sobre la foto lateral
(baja, con perspectiva) y SIN VERIFICAR: cabina de 0,55 a 3,5 m, pilón delantero hasta 4,6 y trasero hasta 5,2 m.
Ancho de 2,6 m por fuera y 3,8 m con los depósitos laterales (de memoria, SIN VERIFICAR). Forma: morro redondeado con mucho cristal, pilón delantero bajo sobre la
cabina, pilón trasero alto con los dos motores colgados a los lados, rampa trasera, tren de cuatro patas (ruedas dobles
delante, simples detrás), ojos de buey en hilera y puerta de la tripulación delante a la derecha (−X).
Sentido de giro (de memoria, SIN VERIFICAR): el delantero, antihorario visto desde arriba; el trasero, horario.
Colores como el NH90 (Lucía): 50541c con los techos en b4a080 (valor partido), cristales 60546c, interior, ruedas y
metal 48403c / 605c48, palas 94948c, cubos 845c28; manchas pardas 845c28 solo en la variante 3. Nada de rojos.
Jerarquía (mira a +Z en Unity), con el morro hacia −Y de Blender como el NH90:
  SM_…_LODn            fuselaje (origen en el suelo, en el centro entre los dos ejes)
  ├─ RotorDelantero    cubo y 3 palas; origen en el buje; gira sobre la vertical
  ├─ RotorTrasero      cubo y 3 palas; origen en el buje; gira sobre la vertical (sentido contrario al delantero)
  ├─ DiscoDelantero    disco de una cara (16 lados, 94948c) para el rotor en marcha, como el DiscoRotor del NH90
  └─ DiscoTrasero      ídem para el trasero
Variantes (TBD_VARIANTE % 5): 0 estándar con la rampa cerrada; 1 rampa bajada y puerta de la tripulación abierta;
2 con las armas de puerta (MAG en la ventana delantera y en la rampa a media altura); 3 con manchas pardas del
camuflaje; 4 rampa bajada con un palé de cajas de munición dentro.
"""
import math, os
from montaje import Montaje, separar as _separar

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
ASPECTO = VAR % 5
PIEZAS = ["casco", "mancha", "optica", "interior", "pala", "cubo", "goma", "metal", "arma", "disco", "caja"]
COLORES = {"casco": "50541c", "mancha": "845c28", "optica": "60546c", "interior": "48403c", "pala": "94948c",
           "cubo": "845c28", "goma": "48403c", "metal": "605c48", "arma": "48403c", "disco": "94948c",
           "caja": "88885c"}
VALOR_PIEZAS = {"casco": ("b4a080", "50541c", "50541c")}
FACCION = "propio"
PRESUPUESTO = {0: (300, 5000), 1: (200, 3000)}
UMBRAL_CONTORNO = {0: 0.06, 1: 0.25}
HUB_D = (0.0, -5.925, 4.9)
HUB_T = (0.0, 5.925, 5.45)
R_ROTOR = 9.15
PARTES = ["fuselaje", "RotorDelantero", "RotorTrasero", "DiscoDelantero", "DiscoTrasero"]
PIV = {"RotorDelantero": HUB_D, "RotorTrasero": HUB_T, "DiscoDelantero": HUB_D, "DiscoTrasero": HUB_T}
GIRO_PRUEBA = {"RotorDelantero": ("Z", 40), "RotorTrasero": ("Z", -40)}
# sección de la cabina (x, z): rectángulo de esquinas achaflanadas, normalizado a ±1
PERFIL = [(-1.0, -0.8), (-0.8, -1.0), (0.8, -1.0), (1.0, -0.8), (1.0, 0.7), (0.75, 1.0), (-0.75, 1.0), (-1.0, 0.7)]


def _rotor(m, hub, fase, fino):
    hx, hy, hz = hub
    m.cilindro((hx, hy, hz), 0.55, 0.45, "Z", 10, "cubo")
    m.cilindro((hx, hy, hz + 0.35), 0.25, 0.3, "Z", 8, "cubo")
    cuerda, grosor = (0.8 if fino else 1.0), (0.08 if fino else 0.14)
    for k in range(3):
        a = math.radians(fase + 120 * k)
        m.barra((hx + 0.5 * math.cos(a), hy + 0.5 * math.sin(a), hz),
                (hx + R_ROTOR * math.cos(a), hy + R_ROTOR * math.sin(a), hz - 0.25), grosor, cuerda, "pala")


def _disco(m, hub):
    hx, hy, hz = hub
    c0 = m.bm.verts.new((hx, hy, hz - 0.1))
    vs = [m.bm.verts.new((hx + R_ROTOR * math.cos(2 * math.pi * k / 16), hy + R_ROTOR * math.sin(2 * math.pi * k / 16),
                          hz - 0.1)) for k in range(16)]
    m.t._marcar([m.bm.faces.new((c0, vs[k], vs[(k + 1) % 16])) for k in range(16)], "disco")


def construir(nombre="SM_Chinook", lod=0):
    m = Montaje(PIEZAS, PARTES)
    fino = lod == 0
    a = ASPECTO
    rampa_abajo = a in (1, 4)
    # ---- fuselaje (MV3_1, MV3_2): sección alta de costados planos, 2,6 m de ancho y de 0,55 a 3,5 m de alto
    cola = [(5.6, 1.3, 1.47, 2.02), (6.9, 1.25, 1.3, 2.2), (7.8, 1.15, 0.95, 2.55)]
    if rampa_abajo:                                   # sin la parte baja de la cola: la rampa va aparte, bajada
        cola = [(5.6, 1.3, 1.47, 2.02), (6.9, 1.25, 0.85, 2.65), (7.8, 1.15, 0.6, 2.9)]
    m.loft(PERFIL, [(-7.8, 0.55, 0.55, 1.45), (-7.5, 0.95, 0.95, 1.75), (-6.8, 1.25, 1.35, 1.95),
                    (-6.0, 1.3, 1.47, 2.02)] + cola, "casco")
    if rampa_abajo:                                   # rampa bajada hasta el suelo e interior oscuro de la bodega
        m.caja((0, 6.75, 1.65), (2.3, 0.05, 1.6), "interior")
        m.barra((0, 7.2, 0.8), (0, 9.2, 0.06), 2.3, 0.12, "casco")
    # pilones altos (MV3_1, MV3_2): el delantero sobre la cabina hasta 4,6 m y el trasero hasta 5,2 m
    m.loft(PERFIL, [(-7.0, 0.45, 0.1, 3.5), (-6.85, 0.7, 0.55, 3.95), (-6.5, 0.8, 0.65, 3.95), (-4.9, 0.8, 0.65, 3.95),
                    (-4.4, 0.55, 0.3, 3.75), (-3.9, 0.35, 0.1, 3.55)], "casco")
    m.loft(PERFIL, [(2.4, 0.45, 0.1, 3.55), (3.4, 0.85, 0.75, 4.2), (6.6, 0.85, 0.95, 4.25), (7.7, 0.4, 0.95, 4.25)],
           "casco")
    m.cilindro((HUB_D[0], HUB_D[1], 4.68), 0.3, 0.4, "Z", 8, "metal")            # mástiles
    m.cilindro((HUB_T[0], HUB_T[1], 5.3), 0.3, 0.35, "Z", 8, "metal")
    # motores colgados a los lados del pilón trasero
    for s in (-1, 1):
        m.cilindro((s * 1.4, 4.9, 3.75), 0.45, 2.6, "Y", 10 if fino else 8, "casco")
        m.cilindro((s * 1.4, 6.25, 3.75), 0.32, 0.12, "Y", 8, "interior")        # tobera
        m.barra((s * 0.8, 4.5, 3.95), (s * 1.15, 4.5, 3.8), 0.4, 0.3, "casco")    # soporte
    # depósitos laterales (sponsons)
    for s in (-1, 1):
        antes = set(m.bm.verts)
        m.loft(PERFIL, [(-4.4, 0.12, 0.2, 1.0), (-3.8, 0.36, 0.42, 1.0), (4.6, 0.36, 0.42, 1.0),
                        (5.3, 0.12, 0.2, 1.05)], "casco")
        for v in set(m.bm.verts) - antes:
            v.co.x += s * 1.53
    # ---- cristales: parabrisas, ventanas del morro y ventanillas de la cabina
    m.caja((0, -7.3, 2.75), (2.0, 0.06, 0.75), "optica", rot_x=-0.6)
    for s in (-1, 1):
        m.caja((s * 1.25, -6.6, 2.7), (0.05, 1.2, 0.7), "optica")
        m.caja((s * 1.0, -7.35, 1.75), (0.5, 0.05, 0.5), "optica", rot_z=s * 0.6)  # ventanas bajas del morro
        n = 5 if fino else 3
        for k in range(n):                              # ojos de buey en hilera (MV3_2)
            m.cilindro((s * 1.315, -3.2 + k * 7.6 / max(n - 1, 1), 2.2), 0.24, 0.04, "X", 10 if fino else 6, "optica")
    # puerta de la tripulación delante a la derecha (−X)
    if a == 1:
        m.caja((-1.315, -5.0, 1.75), (0.04, 0.9, 1.6), "interior")
        m.caja((-1.45, -5.0, 0.55), (0.3, 0.9, 0.05), "metal")                    # escalón
    else:
        m.caja((-1.315, -5.0, 1.75), (0.03, 0.9, 1.6), "metal")
    # ---- tren: ruedas dobles delante, simples detrás
    for s in (-1, 1):
        for dx in ((0.85, 1.25) if fino else (1.05,)):
            m.cilindro((s * dx, -3.9, 0.42), 0.42, 0.28 if fino else 0.5, "X", 10, "goma")
        m.barra((s * 1.05, -3.9, 0.42), (s * 1.05, -3.9, 0.95), 0.14, 0.14, "metal")
        m.cilindro((s * 1.1, 3.4, 0.42), 0.42, 0.3, "X", 10, "goma")
        m.barra((s * 1.1, 3.4, 0.42), (s * 1.0, 3.4, 0.95), 0.14, 0.14, "metal")
    # ---- extras por variante
    if a == 2:                                        # MAG en la ventana delantera derecha y en la rampa
        m.barra((-1.3, -4.0, 2.2), (-2.3, -4.7, 2.1), 0.1 if fino else 0.15, 0.1 if fino else 0.15, "arma")
        m.caja((-1.4, -4.0, 2.2), (0.22, 0.35, 0.2), "arma")
        m.barra((0, 7.4, 2.0), (0, 8.6, 1.9), 0.1 if fino else 0.15, 0.1 if fino else 0.15, "arma")
    if a == 3:                                        # manchas pardas en los costados y en el pilón trasero
        for s in (-1, 1):
            for y, z, w, h in ((-5.8, 1.6, 1.2, 0.8), (-1.5, 1.3, 1.8, 0.6), (2.0, 2.9, 1.4, 0.6), (5.2, 1.8, 1.0, 0.9)):
                m.caja((s * 1.315, y, z), (0.02, w, h), "mancha")
            m.caja((s * 0.865, 5.0, 4.4), (0.02, 1.6, 0.5), "mancha")
    if a == 4:                                        # palé de munición en la rampa
        for dx in (-0.4, 0.4):
            for dz in (0.0, 0.36):
                m.caja((dx, 7.9, 0.6 + dz), (0.72, 0.38, 0.33), "caja")
    # ---- rotores (piezas animables)
    m.parte("RotorDelantero")
    _rotor(m, HUB_D, 30, fino)
    m.parte("RotorTrasero")
    _rotor(m, HUB_T, 90, fino)
    m.parte("DiscoDelantero")
    _disco(m, HUB_D)
    m.parte("DiscoTrasero")
    _disco(m, HUB_T)
    return m.objeto(nombre)


def separar(o, lod):
    return _separar(o, PARTES, PIV)
