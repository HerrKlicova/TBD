"""Ambulancia española de soporte vital básico, furgón grande (Álvaro, sagunto-nivel.md §5). Teo, 6-nov-2026.

Fotos G3 de Paula (Commons): furgones de techo alto con el damero de Emergencias Sanitarias (Castilla y León); sin
confirmar que sean SVB, pero valen para la carrocería y el damero. Sin marca ni escudos.
Reglas de Lucía: NADA de cruz roja ni media luna; la estrella de la vida (azul oscuro 283440, solo en el LOD0) o nada.
Damero de dos filas a los costados en f0dc9c y 70784c (cuadros de 0,35 m = 2 px), carrocería amarilla clara f0dc9c,
luces del techo apagadas en 283440 (ni un píxel rojo).
MEDIDAS SIN VERIFICAR (típicas de un furgón grande de techo alto): 5,9 × 2,0 × 2,7 m, ruedas de 0,7 m.
Colores: lunas 48403c, ruedas de goma 283440 separadas de la carrocería, paragolpes 6c7874. Delante = −Y (+Z en Unity).
Variantes: 0 intacta; 1 con las puertas traseras abiertas (interior oscuro); 2 empotrada contra algo: morro hundido,
inclinada y con la puerta del conductor abierta.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0)
PIEZAS = ["casco", "damero", "luna", "rueda", "faro", "paragolpes", "interior", "estrella"]
COLORES = {"casco": "f0dc9c", "damero": "70784c", "luna": "48403c", "rueda": "283440", "faro": "283440",
           "paragolpes": "6c7874", "interior": "48403c", "estrella": "283440"}
VALOR_PIEZAS = {"casco": ("f0dc9c", "b4a080")}
FACCION = "neutro"
PRESUPUESTO = {0: (80, 1500), 1: (50, 800)}
P = [(-2.95, 0.42), (-2.95, 0.95), (-2.45, 1.18), (-1.6, 2.68), (2.95, 2.68), (2.95, 0.42)]
HX = 1.0


def construir(nombre="SM_Ambulancia", lod=0):
    t = Taller(PIEZAS)
    fino = t.fino(lod)
    v = VAR % 3
    t.perfil_extruido(P, lambda z: HX if z < 2.2 else HX - 0.06, cortes_z=(2.2,), pieza="casco")
    # damero: dos filas de cuadros de 0,35 m en los costados (y una en la trasera)
    c = 0.35
    for s in (-1, 1):
        n = int((2.95 + 2.4) / c)
        for k in range(n):
            y = -2.4 + (k + 0.5) * c
            for fila in (0, 1):
                if (k + fila) % 2 == 0:
                    t.caja((s * (HX + 0.006), y, 0.98 + fila * c), (0.02, c - 0.03, c - 0.03), "damero")   # 3 cm de holgura: cuadros en esquina = aristas no manifold
    for k in range(5):
        t.caja((-0.7 + k * c, 2.956, 0.98 + (k % 2) * c), (c - 0.03, 0.02, c - 0.03), "damero")
    # lunas: parabrisas, ventanillas de cabina y una alta en el habitáculo
    t.caja((0, -2.04, 1.965), (1.8, 0.02, 1.2), "luna", rot_x=-0.515)   # sobre la pendiente del morro
    for s in (-1, 1):
        t.caja((s * (HX + 0.01), -1.85, 1.75), (0.02, 0.8, 0.6), "luna")
        fino.caja((s * (HX + 0.01), 0.6, 2.05), (0.02, 1.2, 0.4), "luna")
    # luces del techo (apagadas), paragolpes, faros y estrella de la vida (LOD0)
    t.caja((0, -1.2, 2.78), (1.5, 0.35, 0.2), "faro")
    fino.caja((0, 2.7, 2.74), (1.2, 0.2, 0.12), "faro")
    t.caja((0, -2.97, 0.55), (2.04, 0.12, 0.3), "paragolpes")
    t.caja((0, 2.97, 0.5), (2.04, 0.1, 0.22), "paragolpes")
    for s in (-1, 1):
        fino.caja((s * 0.72, -2.96, 0.95), (0.3, 0.03, 0.18), "faro")
        _estrella(fino, (s * (HX + 0.02), 1.8, 1.95), s)
    # ruedas de goma separadas
    for yw in (-1.75, 1.85):
        for s in (-1, 1):
            t.cilindro((s * 0.83, yw, 0.35), 0.35, 0.24, "X", 10 if lod == 0 else 8, "rueda")
    if v == 1:   # puertas traseras abiertas: interior oscuro y dos hojas a 90°
        t.caja((0, 2.96, 1.5), (1.7, 0.03, 1.9), "interior")
        for s in (-1, 1):
            t.caja((s * (HX + 0.42), 3.0, 1.5), (0.85, 0.06, 1.9), "casco", rot_z=s * 0.3)
    if v == 2:   # morro hundido y puerta del conductor abierta
        t.caja((0, -2.9, 0.75), (2.06, 0.3, 0.7), "interior", rot_x=0.3)
        a = math.radians(60)
        t.caja((-(HX + math.sin(a) * 0.45), -2.0 + math.cos(a) * 0.45, 1.3), (0.06, 0.9, 1.4), "casco", rot_z=a)
    o = t.objeto(nombre, bisel=0)
    if v == 2:
        from mathutils import Matrix
        o.data.transform(Matrix.Rotation(math.radians(3), 4, "X"))
    return o


def _estrella(t, c, s):
    """Estrella de la vida: tres barras de 0,5 m cruzadas a 60° (azul oscuro, solo LOD0)."""
    x, y, z = c
    for k in range(3):
        t.caja((x, y, z), (0.02, 0.5, 0.14), "estrella", rot_x=k * math.pi / 3)
