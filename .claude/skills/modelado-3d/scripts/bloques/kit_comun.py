"""Kit modular de estaciones (encargo de Mak, 7-nov, a partir de la estación de Sagunto a1a77ef). Teo, 7-nov-2026.

Todas las piezas del kit comparten la misma lista de PIEZAS (mismos materiales y mismos índices), para que un montaje
pueda juntar módulos en una sola malla sin rehacer la paleta. Los colores salen de la estación de Sagunto aprobada
(D-N123) y de las fotos E1 de Paula: Sagunt, blanca con recercados granate 642030 (PERMITIR); Nules, blanca con
recercados, zócalo e imposta en gris 6c7874 y hastial escalonado.
Ejes: delante (la vía) = -Y en Blender (+Z en Unity), Z arriba, metros. Origen de cada módulo: centro de su planta, en
el suelo. Los módulos de edificio miden 4 m de fachada (3,98 con holgura) y se encadenan en X.
Medidas SIN VERIFICAR (a ojo con las fotos; no hay planos).
"""
import math, os

PIEZAS = ["fachada", "zocalo", "carpinteria", "tejado", "cornisa", "moldura", "recerco", "placa", "chapa", "onda",
          "pilar", "canalon", "losa", "borde", "costado", "tabla", "metal", "escombro", "madera", "hollin"]
COLORES = {"fachada": "d4d0cc", "zocalo": "605c48", "carpinteria": "543820", "tejado": "a86c50", "cornisa": "94948c",
           "moldura": "642030", "recerco": "6c7874", "placa": "48403c", "chapa": "94948c", "onda": "6c7874",
           "pilar": "50541c", "canalon": "48403c", "losa": "94948c", "borde": "f0dc9c", "costado": "88885c",
           "tabla": "845c28", "metal": "48403c", "escombro": "94948c", "madera": "845c28", "hollin": "605c48"}
# D-N80 en el LOD1 (lo mismo que se aprobó en la marquesina de Sagunto): lo oscuro sube un escalón para que su sombra no
# caiga bajo L* 20
COLORES_LOD1 = {"pilar": "70784c", "canalon": "605c48", "metal": "605c48", "placa": "605c48",
                "carpinteria": "6c4038", "moldura": "8c5048"}   # 543820 en una fachada a la sombra cae a L* 18,5 en puertas enteras
PERMITIR = ("642030", "8c5048")   # granate de Sagunt (Lucía) y su escalón de LOD1 (Lucía 7-nov 08:30: sombra L* ≥ 14)
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 1.0


def var_actual(var=None):
    return int(os.environ.get("TBD_VARIANTE", "0") or 0) if var is None else var


def hueco(t, fino, lod, x, y, z, ancho, alto, cara=-1, marco="moldura", arco=False, pieza="carpinteria"):
    """Hueco (puerta o ventana) en una fachada que mira a cara*Y: carpintería oscura y recercado. En el LOD1 el
    recercado es una placa 0,25 m mayor por lado (1,5 px) para que se lea; arco=True añade la dovela de los huecos
    de medio punto de Nules (una banda más alta arriba)."""
    t.caja((x, y + cara * 0.03, z), (ancho, 0.06, alto), pieza)
    m = 0.25 if lod else 0.18
    t.caja((x, y + cara * 0.01, z), (ancho + 2 * m, 0.04, alto + 2 * m), marco)
    if arco:
        t.caja((x, y + cara * 0.015, z + alto / 2 + m + 0.12), (ancho + 2 * m - 0.3, 0.05, 0.34), marco)


def bandas_chapa(t, x0, largo, y0, fondo, z0, pendiente, ancho=1.0, paso=2.0, faltan=None):
    """Bandas de chapa del LOD1 a lo largo de X (nota 1 de Lucía de la D-N123: nada de damero de 1 px)."""
    n = max(1, int(round(fondo / paso)))
    for k in range(n):
        if faltan and k in faltan:
            continue
        y = y0 - fondo / 2 + paso / 2 + k * paso
        t.caja((x0, y, z0 + (y - y0) * math.tan(pendiente) + 0.02), (largo - 0.1, ancho / math.cos(pendiente), 0.02),
               "onda", rot_x=pendiente)
