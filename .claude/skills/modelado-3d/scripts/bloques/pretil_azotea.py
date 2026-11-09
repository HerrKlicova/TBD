"""Pretil de sacos de la azotea del edificio de viajeros de Sagunto (Álvaro, 8-nov, sagunto-nivel.md §10.15, aprobado
por Lucía: sustituye a las atalayas). Teo (3), 8-nov.

Azotea de 19 × 8,8 m (X 96,5–115,5, Z 75–83,8) a 11 m. Pretil de sacos de 1,2 m (4 hiladas) y 1 m de grueso sobre la
cornisa, por los cuatro lados: sale del muro de sacos aprobado (muro_sacos_estados: mismas 5 variantes, 4 estados,
colores y daños), viene hecho al empezar (sin estado de obra). Lo que cae hacia fuera se quita (no se queda en el aire);
los sacos caídos y la arena quedan dentro, sobre la azotea.
Piezas (origen a la altura de la azotea; fuera = −Y en el tramo, −X −Y en las esquinas):
- TRAMO de 5,1 m, centrado, a lo largo de X.
- ESQUINA: bloque de 1 × 1 m en el origen y brazos de 3,4 m hacia +X y +Y: cubre 4,4 m por cada lado. El lado corto
  (8,8 m) son dos esquinas y el largo (19 m), dos esquinas y dos tramos de 5,1 m.
- ESQUINA CON PASO: igual, con el brazo +Y de 2,2 m: deja 1,2 m libres en el lado corto para la llegada de la escalera
  exterior del testero este (con la esquina del sureste: hueco en Z 78,2–79,4).
TBD_VARIANTE = 20 · tipo + 4 · variante + estado (0..59). Tipos: 0 Tramo, 1 Esquina, 2 EsquinaPaso.
Variantes: Recto, Talud, Irregular, Tablon, Red. Estados: Sano, Danado, MuyDanado, Brecha.
"""
import os
import muro_sacos_estados as M
import defensa_piezas as D

_V = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 60
TIPO = _V // 20
NOMBRES_T = ["Tramo", "Esquina", "EsquinaPaso"]
VARIANTE, ESTADO, NOMBRES_V, NOMBRES_E = M.VARIANTE, M.ESTADO, M.NOMBRES_V, M.NOMBRES_E
PIEZAS, COLORES, FACCION, UMBRAL_CONTORNO = M.PIEZAS, M.COLORES, M.FACCION, M.UMBRAL_CONTORNO
NORMALES_ARRIBA, NORMALES_INCLINACION = M.NORMALES_ARRIBA, M.NORMALES_INCLINACION
PRESUPUESTO = {0: (10, 4000), 1: (6, 1500)}


def construir(nombre="SM_PretilAzotea", lod=0):
    if TIPO == 0:
        o = D.juntar(nombre, [D._tramo(M, 5.1, None, "tmp_t", lod)])
        return D.quitar_fuera(o, limite=-0.6, en_x=False)
    o = D.pieza(M, 0, nombre, lod, bloque=1.0, brazo=3.4, brazo_b=3.4 if TIPO == 1 else 2.2)
    return D.quitar_fuera(o)


separar = D.separar
