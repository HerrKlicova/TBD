"""Muro de contención de la trinchera y boca del cajón de la estación de Castelló (Álvaro, 8-nov 18:02,
castellon-nivel.md §7: vía a −8 m; trinchera abierta en X 0–70 y 190–256 con muros de 8 m en Z 55 y Z 74; cajón bajo
losa de X 70 a 190). Teo (3), 8-nov.

- MURO: módulo de 10 m (9,98) a lo largo de X, 8 m de alto desde la vía hasta la calle, 0,6 m de grueso, con remate de
  borde (albardilla de 0,8 m) y barandilla de 1,1 m arriba. La cara vista (la de la vía) está en −Y (y = −0,3); la
  tierra en +Y. Origen: eje del muro, a la cota de la vía.
- BOCA: frente del cajón donde la trinchera entra bajo la losa: dintel de 19 m (Z 55–74) de 6,5 a 8 m con el canto de
  la losa, pretil de 1,1 m arriba y dos estribos. Mira a −Y (hacia la trinchera); el cajón queda en +Y. Origen: centro
  del dintel, a la cota de la vía. En la boca oeste (X 70) va con yaw 90 y en la este (X 190) con yaw 270, a comprobar
  con la primera que se coloque.
Colores: hormigón 94948c con la albardilla b8b8b8 arriba (arriba claro), barandilla 6c7874, grietas y manchas 605c48,
hiedra 70784c, escombro 88885c. Nada rojo ni en la banda de la horda. Medidas SIN VERIFICAR.
TBD_VARIANTE = 5 · tipo + variante (0..9). Tipos: 0 Muro, 1 Boca. Variantes: 0 hormigón liso; 1 pantalla de pilotes
(costillas verticales); 2 con malla de seguridad alta arriba; 3 agrietado, con un trozo de albardilla y de barandilla
caídos a la vía; 4 con hiedra colgando y manchas de agua.
"""
import math, os
from taller import Taller

VAR = int(os.environ.get("TBD_VARIANTE", "0") or 0) % 10
TIPO, V = VAR // 5, VAR % 5
NOMBRES_T = ["Muro", "Boca"]
NOMBRES_V = ["Liso", "Pilotes", "Malla", "Agrietado", "Hiedra"]
PIEZAS = ["hormigon", "albardilla", "baranda", "mancha", "hiedra", "escombro"]
COLORES = {"hormigon": "94948c", "albardilla": "b8b8b8", "baranda": "6c7874", "mancha": "605c48", "hiedra": "70784c",
           "escombro": "88885c"}
FACCION = "neutro"
NORMALES_ARRIBA = True
NORMALES_INCLINACION = 0.8
PRESUPUESTO = {0: (12, 1500), 1: (6, 600)}
UMBRAL_CONTORNO = {0: 9.0, 1: 9.0}
H = 8.0


def construir(nombre="SM_MuroTrinchera", lod=0):
    t = Taller(PIEZAS)
    fino = lod == 0
    g = 0.06 if fino else 0.34
    v = V
    if TIPO == 0:
        L, cara = 9.98, -0.3
        t.caja((0, 0, H / 2), (L, 0.6, H), "hormigon")
        t.caja((0, -0.05, H + 0.15), (L, 0.8, 0.3), "albardilla")
        if v == 1:                                   # pantalla de pilotes: costillas verticales en la cara vista
            for k in range(8 if fino else 4):
                x = -L / 2 + (k + 0.5) * L / (8 if fino else 4)
                t.cilindro((x, cara - 0.05, H / 2 - 0.1), 0.32 if fino else 0.5, H - 0.2, "Z", 8 if fino else 6,
                           "hormigon")
    else:
        L, cara = 19.0, -0.5
        t.caja((0, 0.25, 6.5 + 0.75), (L, 1.5, 1.5), "hormigon")          # dintel: el canto de la losa
        t.caja((0, 0.1, H + 0.15), (L + 0.6, 1.0, 0.3), "albardilla")
        for s in (-1, 1):                                                  # estribos (los muros que entran al cajón)
            t.caja((s * (L / 2 + 0.2), 0.25, 3.25), (0.8, 1.5, 6.5), "hormigon")
        if v == 1:
            for k in range(6 if fino else 3):
                x = -L / 2 + (k + 0.5) * L / (6 if fino else 3)
                t.caja((x, cara - 0.1, 7.25), (0.5, 0.2, 1.5), "hormigon")
    # barandilla (o pretil) arriba, por el lado de la vía
    yb = cara + 0.1
    if TIPO == 1:
        t.caja((0, yb + 0.1, H + 0.3 + 0.5), (L, 0.25, 1.0), "albardilla")      # pretil macizo
    else:
        tramos = [(-L / 2, L / 2)] if v != 3 else [(-L / 2, -1.2), (2.4, L / 2)]
        for x0, x1 in tramos:
            t.caja(((x0 + x1) / 2, yb, H + 0.3 + 1.1), (x1 - x0 - 0.02, g, g), "baranda")
            t.caja(((x0 + x1) / 2, yb, H + 0.3 + 0.55), (x1 - x0 - 0.02, g, g), "baranda")
            n = max(1, int((x1 - x0) / 2.0))
            for k in range(n + 1):
                t.caja((x0 + 0.05 + k * (x1 - x0 - 0.1) / n, yb, H + 0.3 + 0.55), (g, g, 1.1), "baranda")
    if v == 2:                                       # malla de seguridad alta (2 m) sobre la barandilla
        t.caja((0, yb + 0.15, H + 0.3 + 1.0), (L - 0.02, 0.03 if fino else 0.06, 2.0), "baranda")
    if v == 3:                                       # grieta, trozo caído al pie (en la vía, −Y)
        if TIPO == 0:
            t.caja((0.6, cara - 0.01, H * 0.55), (0.25, 0.02, H * 0.8), "mancha", rot_y=0.25)
        else:                                        # en la boca, la grieta va en el dintel
            t.caja((0.6, cara - 0.01, 7.25), (0.25, 0.02, 1.4), "mancha", rot_y=0.25)
        t.caja((0.5, cara - 1.0, 0.25), (2.6, 0.8, 0.5), "albardilla", rot_z=0.2, rot_y=0.15)
        t.caja((1.6, cara - 1.6, 0.2), (1.2, 0.9, 0.4), "escombro", rot_z=0.7)
        t.caja((0.6, cara - 2.0, 0.08), (3.4, g, g), "baranda", rot_z=0.1)
    if v == 4:                                       # hiedra colgando desde arriba y manchas de agua
        f = 1.0 if TIPO == 0 else 0.0
        for k, (x, hc) in enumerate(((-3.2, 3.5), (-0.4, 5.5), (2.6, 2.8))):
            hc = hc if TIPO == 0 else 1.2 + 0.25 * k     # en la boca, solo sobre el dintel
            t.caja((x, cara - 0.04, H - hc / 2), (1.8 if fino else 2.0, 0.08, hc), "hiedra")
        if TIPO == 0:
            for x in (-1.8, 1.2, 4.0):
                t.caja((x, cara - 0.02, H * 0.45), (0.7, 0.03, H * 0.6), "mancha")
    return t.objeto(nombre, bisel=0)
