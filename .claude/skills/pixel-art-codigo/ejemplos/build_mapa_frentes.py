"""Mapa de frentes dentro de Sagunto (mundo persistente, norma de Eugenio). Bruno (Arte técnico), 7-nov 00:52.

Encargo de Mak (00:49). MUESTRA de 5 variantes (A–E) del mapa con:
  marcadores de la estación, el tren y el paso a nivel con su estado; unidades en tránsito con su «ETA mm:ss»;
  línea de ruta por la vía, por carretera habilitada o por el aire; carreteras habilitadas frente a no habilitadas;
  y el helipuerto como origen y destino del NH90.
El mapa gira como la cámara (yaw 45°, D-N93: arriba en pantalla = +X+Z) y sale del mapa de zonas de 1 m.
Datos de Sagunto: sagunto-nivel.md (paso a nivel en X 56-62 y Z 58-67; carretera vieja en Z ≈ 23; calle de la Estación
en X ≈ 59; paso superior en X = −20; helipuerto E6 en (180; 44); vía 1 en Z 64,5).
SUPUESTOS SIN VERIFICAR (los decide Marcos o Lucía):
  - qué carreteras están habilitadas: aquí, la calle de la Estación y la carretera vieja hasta el Palancia; NO habilitadas,
    la carretera del paso superior (coches abandonados encima) y la carretera vieja pasado el Palancia (camión cruzado).
  - dónde está el tren: detenido en la vía 1 al oeste, bajo el paso superior (X −100 a −36).
  - la situación de la maqueta: el tren bajo ataque, el paso a nivel amenazado y la estación tranquila.
Colores: el rojo #982420 solo para el ataque; el ámbar solo en el aviso del paso a nivel (parpadeo de 3 s, D-N109);
la H del helipuerto en grey_light (nunca ámbar, E6); nada en 283440.
"""
import math
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixelvm import PAL, Sprite  # noqa: E402
from build_d_vfx import BAYER  # noqa: E402

W, H = 640, 360
K = math.sqrt(.5)

# --- Datos de la maqueta (coordenadas del mundo, m) -------------------------------------------
SITIOS = {
    "estacion": {"pos": (112, 78), "nombre": "Estación", "vida": .92, "estado": "ok"},
    "tren": {"pos": (-68, 64.5), "nombre": "Tren", "vida": .64, "estado": "ataque"},
    "paso": {"pos": (59, 62), "nombre": "Paso a nivel", "vida": None, "estado": "aviso"},
    "heli": {"pos": (180, 44), "nombre": "Helipuerto", "vida": None, "estado": "base"},
}
DATOS = Path(__file__).resolve().parent / "datos" / "mapa_sagunto_carreteras.json"


def _datos():
    import json
    return json.loads(DATOS.read_text(encoding="utf-8"))


_D = _datos()
# (nombre, tramo, habilitada): sale del dato, no del dibujo (Mak, 00:55)
CARRETERAS = [(c["nombre"], [tuple(q) for q in c["tramo"]], c["habilitada"]) for c in _D["carreteras"]]
VIA = [tuple(q) for q in _D["via"]]
RECINTO = [tuple(q) for q in _D["recinto"]]                  # muro del fuerte (borde de la zona 1: X 70-155, Z 45-85)
RUTAS = [   # (unidad, icono, tipo, puntos, eta, avance 0..1)
    ("Pelotón 2 · VAMTAC", "vehiculo", "via", [(68, 64.5), (-36, 64.5)], "02:14", .55),
    ("Pelotón 4 · Dragón", "vehiculo", "carretera", [(68, 64.5), (59, 62), (59, 23), (-60, 23)], "03:10", .55),
    ("NH90", "helicoptero", "aire", [(180, 44), (-60, 70)], "00:48", .2),
]


def _zonas():
    import build_avisos_minimapa as am
    return Image.open(am.ZONAS_PNG).load()


class Vista:
    """Proyección del mapa girado 45°: centro (cx, cz) del mundo en el píxel (ox, oy), esc px por metro."""
    def __init__(self, cx=56, cz=54, esc=1.8, ox=328, oy=186):
        self.cx, self.cz, self.esc, self.ox, self.oy = cx, cz, esc, ox, oy

    def p(self, X, Z):
        dx, dz = X - self.cx, Z - self.cz
        return (self.ox + K * (dx - dz) * self.esc, self.oy - K * (dx + dz) * self.esc)

    def mundo(self, sx, sy):
        u, v = (sx - self.ox) / self.esc, (self.oy - sy) / self.esc
        return self.cx + K * (u + v), self.cz + K * (v - u)


# --- Dibujo básico ----------------------------------------------------------------------------
def _pon(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((int(x), int(y)), PAL[c] if isinstance(c, str) else c)


def linea(im, a, b, c, w=1, patron=None):
    (x0, y0), (x1, y1) = a, b
    n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
    for i in range(n):
        if patron and not patron(i):
            continue
        t = i / max(1, n - 1)
        x, y = round(x0 + (x1 - x0) * t), round(y0 + (y1 - y0) * t)
        for dy in range(w):
            for dx in range(w):
                _pon(im, x + dx - w // 2, y + dy - w // 2, c)


def polilinea(im, pts, c, w=1, patron=None):
    k = 0
    for a, b in zip(pts, pts[1:]):
        n = int(max(abs(b[0] - a[0]), abs(b[1] - a[1]))) + 1
        linea(im, a, b, c, w, (lambda i, k=k: patron(i + k)) if patron else None)
        k += n


def caja(im, x, y, w, h, fondo="navy", borde="bone", sombra="grey_brown"):
    for yy in range(h):
        for xx in range(w):
            c = fondo
            if yy == 0 or xx == 0:
                c = borde
            elif yy == h - 1 or xx == w - 1:
                c = sombra
            if (xx in (0, w - 1)) and (yy in (0, h - 1)):
                continue
            if c:
                _pon(im, x + xx, y + yy, c)


def txt(im, xy, s, c="bone", t=9, centro=False, sombra=False):
    import build_maquetas as m
    m.texto(im, xy, s, t, c, centro=centro, sombra=sombra)


def letras_px(im, x, y, s, c="bone"):
    """Texto en las letras de 3x5 (ui_letras_3x5 + ui_digitos_3x5), 4 px de paso."""
    import build_apoyo_aereo as aa
    import build_rutas as r
    for k, ch in enumerate(s):
        g = aa.LET.get(ch) or r.DIG.get(ch)
        if not g:
            continue
        for j, fila in enumerate(g):
            for i, cc in enumerate(fila):
                if cc == "#":
                    _pon(im, x + k * 4 + i, y + j, c)


def eta(texto, borde="bone"):
    """Placa «ETA mm:ss» de 9 px: ui_eta_placa (slice 3) + ui_eta_reloj_7 en x 2 + letras y dígitos de 3x5 en y 2."""
    import build_rutas as r
    s = "ETA " + texto
    w = 2 + 7 + 2 + 4 * len(s) - 1 + 3
    im = Image.new("RGBA", (w, 9), (0, 0, 0, 0))
    caja(im, 0, 0, w, 9, "navy", borde, "grey_brown")
    im.alpha_composite(r.reloj().img, (2, 1))
    letras_px(im, 11, 2, s)
    return im


# --- Iconos nuevos ----------------------------------------------------------------------------
def paso_nivel_16():
    """Icono del paso a nivel: cruz de San Andrés sobre su poste y barrera rota (pictograma oscuro sobre placa)."""
    import build_frentes_aire as fa
    s = Sprite(16, 16)
    fa._placa16(s)
    for k in range(9):
        s.set(3 + k, 2 + k * 6 // 8, "olive_shadow"); s.set(4 + k, 2 + k * 6 // 8, "olive_shadow")
        s.set(12 - k, 2 + k * 6 // 8, "olive_shadow"); s.set(11 - k, 2 + k * 6 // 8, "olive_shadow")
    s.vline(7, 5, 13, "olive_shadow"); s.vline(8, 5, 13, "olive_shadow")
    s.hline(2, 6, 12, "olive_shadow"); s.hline(9, 13, 11, "olive_shadow")      # barrera partida
    s.hline(2, 13, 13, "grey_brown")
    return s


def helipuerto_16(nh90_dentro=False):
    """Helipuerto: H en grey_light dentro de un círculo (E6). Con el NH90 dentro, el rotor en hueso encima."""
    s = Sprite(16, 16)
    s.ellipse(0, 0, 15, 15, "navy")
    for a in range(48):
        t = a / 48 * 2 * math.pi
        s.set(int(round(7.5 + 6.8 * math.cos(t))), int(round(7.5 + 6.8 * math.sin(t))), "grey_light")
    s.vline(5, 4, 11, "grey_light"); s.vline(10, 4, 11, "grey_light"); s.hline(5, 10, 7, "grey_light")
    if nh90_dentro:
        s.line(3, 3, 12, 12, "bone"); s.line(12, 3, 3, 12, "bone")
    return s


def heli_9():
    """NH90 de perfil en 11x9 (en el mapa grande; en el minimapa sigue ui_mm_helicoptero)."""
    s = Sprite(11, 9)
    s.hline(0, 10, 1, "bone"); s.vline(5, 1, 3, "bone")
    s.ellipse(3, 3, 9, 7, "bone"); s.hline(0, 3, 4, "bone"); s.vline(0, 3, 5, "bone")
    s.set(7, 4, "navy"); s.set(8, 4, "navy")
    s.hline(4, 9, 8, "beige")
    return s


def icono_unidad(tipo):
    import build_avisos_minimapa as am
    if tipo == "helicoptero":
        return heli_9()
    return {"vehiculo": am.mm_vehiculo, "helicoptero": am.mm_helicoptero, "peloton": am.mm_peloton}[tipo]()


def marcador(tipo):
    import build_frentes_aire as fa
    if tipo == "paso":
        return paso_nivel_16()
    if tipo == "heli":
        return helipuerto_16()
    return fa.frente(tipo)


COL_ESTADO = {"ok": "beige", "aviso": "amber", "ataque": "red_bright", "base": "grey_light"}


# --- Fondos -----------------------------------------------------------------------------------
FONDOS = {
    # zona: color por estilo. 0 descampado, 1 recinto, 2 glacis, 3 vía, 4 calle, 5 huerta, 6 pisado, 7 pueblo,
    # 8 cauce, 9 acequia, 10 solar, 11 cerro
    "tactico": {0: "olive_shadow", 1: "grey_brown", 2: "olive_shadow", 3: "olive_shadow", 4: "olive_grey", 5: "olive_dark",
                6: "grey_brown", 7: "olive_grey", 8: "grey_brown", 9: "olive_grey", 10: "grey_brown", 11: "olive_shadow"},
    "papel": {0: "beige", 1: "bone", 2: "beige", 3: "beige", 4: "bone", 5: "olive_light", 6: "beige", 7: "concrete_light",
              8: "olive_light", 9: "concrete", 10: "beige", 11: "olive_light"},
    "velado": {0: "olive_grey", 1: "concrete", 2: "grey_brown", 3: "olive_grey", 4: "olive_grey", 5: "olive_dark",
               6: "grey_brown", 7: "concrete_light", 8: "olive_grey", 9: "concrete", 10: "brown", 11: "olive_dark"},
}


def fondo(v, estilo, x0=0, y0=0, x1=W, y1=H, velo=0):
    im = Image.new("RGBA", (W, H), PAL["navy"])
    if estilo in FONDOS:
        z = _zonas()
        tabla = FONDOS[estilo]
        for sy in range(y0, y1):
            for sx in range(x0, x1):
                X, Z = v.mundo(sx + .5, sy + .5)
                px, py = int(X + 250), int(480 - Z)
                if 0 <= px < 756 and 0 <= py < 680:
                    c = tabla.get(z[px, py], "olive_shadow")
                    if velo and velo * 16 > BAYER[sy % 4][sx % 4]:
                        c = "navy"
                    im.putpixel((sx, sy), PAL[c])
    return im


def carreteras(im, v, estilo):
    hab = {"tactico": ("concrete_light", 3), "papel": ("navy", 3), "velado": ("bone", 3), "lineas": ("bone", 2)}[estilo]
    nohab = {"tactico": "grey_brown", "papel": "rose_brown", "velado": "grey_brown", "lineas": "grey_brown"}[estilo]
    for _, tramo, ok in CARRETERAS:
        pts = [v.p(*q) for q in tramo]
        if ok:
            polilinea(im, pts, hab[0], hab[1])
        else:                                             # no habilitada: trazo a rayas y aspas cada 24 px
            polilinea(im, pts, nohab, 3, lambda i: i % 6 < 3)
            (xa, ya), (xb, yb) = pts
            n = int(math.hypot(xb - xa, yb - ya))
            for d in range(12, n, 24):
                t = d / n
                cx, cy = xa + (xb - xa) * t, ya + (yb - ya) * t
                linea(im, (cx - 3, cy - 3), (cx + 3, cy + 3), nohab, 1)
                linea(im, (cx + 3, cy - 3), (cx - 3, cy + 3), nohab, 1)


def via_px(im, a, b, c):
    """Vía en diagonal de 45°: dos carriles a ±1 px en perpendicular y traviesa cada 3 pasos (tesela ui_mapa_via)."""
    (x0, y0), (x1, y1) = a, b
    n = int(round(max(abs(x1 - x0), abs(y1 - y0))))
    sx, sy = (1 if x1 >= x0 else -1), (1 if y1 >= y0 else -1)
    px, py = sy, -sx                                       # perpendicular en la rejilla diagonal
    for i in range(n + 1):
        x, y = round(x0) + sx * i, round(y0) + sy * i
        _pon(im, x + px, y + py, c); _pon(im, x - px, y - py, c)
        if i % 3 == 0:
            _pon(im, x, y, c)


def via(im, v, estilo):
    a, b = [v.p(*q) for q in VIA]
    c = {"tactico": "beige", "papel": "grey_brown", "velado": "beige", "lineas": "beige"}[estilo]
    via_px(im, a, b, c)


def recinto(im, v, c="grey_light"):
    pts = [v.p(*q) for q in RECINTO + RECINTO[:1]]
    polilinea(im, pts, c, 1)


# --- Rutas, unidades y marcadores -------------------------------------------------------------
RUTA_COL = {"via": "cream", "carretera": "bone", "aire": "grey_light"}


def _sobre(pts, t):
    largos = [math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(pts, pts[1:])]
    d = t * sum(largos)
    for (a, b), l in zip(zip(pts, pts[1:]), largos):
        if d <= l:
            return a[0] + (b[0] - a[0]) * d / l, a[1] + (b[1] - a[1]) * d / l, (b[0] - a[0]) / l, (b[1] - a[1]) / l
        d -= l
    a, b = pts[-2], pts[-1]
    return b[0], b[1], (b[0] - a[0]) / largos[-1], (b[1] - a[1]) / largos[-1]


def flecha(im, x, y, ux, uy, c, r=4):
    for k in range(r + 1):
        for s in (-1, 1):
            _pon(im, round(x - ux * k + s * uy * k * .8), round(y - uy * k - s * ux * k * .8), c)
    _pon(im, round(x), round(y), c)


def ruta(im, v, r, f=0, estilo="tactico", hecha="grey_brown"):
    nombre, icono, tipo, pts_m, t_eta, av = r
    pts = [v.p(*q) for q in pts_m]
    c = RUTA_COL[tipo] if estilo != "papel" else "navy"
    # tramo ya recorrido apagado; lo que falta con el patrón de su tipo; las marchas avanzan con f
    x, y, ux, uy = _sobre(pts, av)
    corte = pts[:1]
    for a, b in zip(pts, pts[1:]):
        corte.append(b)
        if (x - a[0]) * (x - b[0]) <= 0.5 and (y - a[1]) * (y - b[1]) <= 0.5:
            break
    corte[-1] = (x, y)
    polilinea(im, corte, hecha, 1)
    resto = [(x, y)] + [p for p in pts if p not in corte][0:]
    resto = [(x, y)] + pts[len(corte) - 1:]
    if tipo == "aire":
        patron = lambda i: (i + 2 * f) % 4 == 0
    elif tipo == "via":
        patron = lambda i: (i + 3 * f) % 6 < 4
    else:
        patron = lambda i: (i + 3 * f) % 6 != 5
    polilinea(im, resto, c, 2 if tipo != "aire" else 1, patron)
    xb, yb, uxb, uyb = _sobre(pts, 1.0)
    flecha(im, xb, yb, uxb, uyb, c)
    return x, y


def unidad(im, x, y, icono, etiqueta, borde="bone", lado=1):
    ic = icono_unidad(icono)
    caja(im, int(x - 6), int(y - 6), 13, 13, "navy", borde, "grey_brown")
    im.alpha_composite(ic.img, (int(x - ic.w // 2), int(y - ic.h // 2)))
    e = eta(etiqueta)
    ex = int(x + 9) if lado > 0 else int(x - 9 - e.width)
    im.alpha_composite(e, (ex, int(y - 4)))


def sitio(im, v, clave, f=0, modo="placa"):
    d = SITIOS[clave]
    x, y = v.p(*d["pos"])
    x, y = int(x), int(y)
    col = COL_ESTADO[d["estado"]]
    if d["estado"] == "ataque" and f == 1:
        col = "red"
    if d["estado"] == "aviso" and f == 1:
        col = "ochre_dark"
    mk = marcador(clave)
    if modo == "pin":                                     # alfiler con mástil
        for yy in range(y - 14, y):
            _pon(im, x, yy, col)
        caja(im, x - 10, y - 34, 20, 20, "navy", col, col)
        im.alpha_composite(mk.img, (x - 8, y - 32))
        _pon(im, x - 1, y, col); _pon(im, x + 1, y, col)
        ty = y - 46 if y > 60 else y + 4
    elif modo == "anillo":                                # círculo con el icono dentro
        for a in range(64):
            t = a / 64 * 2 * math.pi
            for r in (11, 12):
                _pon(im, round(x + r * math.cos(t)), round(y + r * math.sin(t)), col)
        im.alpha_composite(mk.img, (x - 8, y - 8))
        ty = y - 26
    else:                                                 # placa con borde de estado
        caja(im, x - 10, y - 10, 20, 20, "navy", col, col)
        im.alpha_composite(mk.img, (x - 8, y - 8))
        ty = y + 12 if (clave != "paso" and y < H - 40) else y - 25
    # nombre y estado
    rot = d["nombre"] + (f" {int(d['vida'] * 100)} %" if d["vida"] is not None else "")
    if clave == "heli":
        rot = "Helipuerto · NH90 en misión"
    import build_maquetas as m
    from PIL import ImageDraw
    w = int(ImageDraw.Draw(im).textlength(rot, font=m._f(9))) + 8
    caja(im, x - w // 2, ty, w, 13, "navy", "grey_brown", "grey_brown")
    txt(im, (x, ty + 1), rot, "bone" if d["estado"] != "ataque" else "cream", centro=True)
    if d["vida"] is not None:                             # barra de vida bajo el nombre
        bw = w - 6
        for xx in range(bw):
            c = col if xx < bw * d["vida"] else "grey_brown"
            _pon(im, x - w // 2 + 3 + xx, ty + 11, c)


def hud_mapa(im, titulo="Mapa de frentes", tecla="M"):
    import build_interfaz as ui
    import build_maquetas as m
    caja(im, W // 2 - 32, 4, 64, 22, "navy", "bone", "grey_brown")   # sin el 283440 de ui_panel (D-N113)
    m.texto(im, (W // 2, 8), "01:12", 11, centro=True)
    import build_maquetas as m2
    from PIL import ImageDraw
    t = f"{titulo} · {tecla}: volver"
    w = int(ImageDraw.Draw(im).textlength(t, font=m2._f(9))) + 14
    caja(im, 8, 6, w, 15, "navy", "bone", "grey_brown")
    txt(im, (14, 8), t, "cream")


def leyenda(im, x, y, estilo="tactico", fondo_="navy"):
    caja(im, x, y, 168, 58, fondo_, "grey_brown", "grey_brown")
    tc = "bone" if fondo_ == "navy" else "navy"
    hab = {"tactico": "concrete_light", "papel": "navy", "velado": "bone", "lineas": "bone"}[estilo]
    nohab = {"tactico": "grey_brown", "papel": "rose_brown", "velado": "grey_brown", "lineas": "grey_brown"}[estilo]
    linea(im, (x + 6, y + 9), (x + 22, y + 9), hab, 3)
    txt(im, (x + 28, y + 3), "Carretera habilitada", tc)
    linea(im, (x + 6, y + 21), (x + 22, y + 21), nohab, 3, lambda i: i % 6 < 3)
    txt(im, (x + 28, y + 15), "No habilitada", tc)
    for d in range(17):
        _pon(im, x + 6 + d, y + 32, "beige"); _pon(im, x + 6 + d, y + 35, "beige")
        if d % 4 == 0:
            for k in range(32, 36):
                _pon(im, x + 6 + d, y + k, "beige")
    txt(im, (x + 28, y + 27), "Vía", tc)
    linea(im, (x + 6, y + 46), (x + 22, y + 46), "grey_light", 1, lambda i: i % 4 == 0)
    txt(im, (x + 28, y + 40), "Aire (NH90)", tc)


# --- Variantes --------------------------------------------------------------------------------
def pantalla(var, f=0):
    import build_maquetas as m
    v = Vista()
    if var == "A":                                        # plano táctico oscuro, placas
        im = fondo(v, "tactico")
        carreteras(im, v, "tactico"); via(im, v, "tactico"); recinto(im, v)
        pos = [ruta(im, v, r, f, "tactico") for r in RUTAS]
        for k in ("paso", "estacion", "tren", "heli"):
            sitio(im, v, k, f, "placa")
        for (x, y), r in zip(pos, RUTAS):
            unidad(im, x, y, r[1], r[4])
        hud_mapa(im); leyenda(im, W - 176, H - 66)
    elif var == "B":                                      # mapa de papel
        im = fondo(v, "papel")
        carreteras(im, v, "papel"); via(im, v, "papel"); recinto(im, v, "grey_brown")
        pos = [ruta(im, v, r, f, "papel", "beige") for r in RUTAS]
        for k in ("paso", "estacion", "tren", "heli"):
            sitio(im, v, k, f, "placa")
        for (x, y), r in zip(pos, RUTAS):
            unidad(im, x, y, r[1], r[4])
        hud_mapa(im); leyenda(im, W - 176, H - 66, "papel", "bone")
    elif var == "C":                                      # sobre el mapa de colores velado, alfileres
        im = fondo(v, "velado", velo=.5)
        carreteras(im, v, "velado"); via(im, v, "velado"); recinto(im, v)
        pos = [ruta(im, v, r, f, "velado") for r in RUTAS]
        for k in ("paso", "estacion", "tren", "heli"):
            sitio(im, v, k, f, "pin")
        for (x, y), r in zip(pos, RUTAS):
            unidad(im, x, y, r[1], r[4])
        hud_mapa(im); leyenda(im, W - 176, H - 66, "velado")
    elif var == "D":                                      # solo líneas sobre navy, anillos
        im = fondo(v, "lineas")
        for sy in range(0, H, 16):                        # retícula tenue cada 16 px
            for sx in range(0, W, 2):
                _pon(im, sx, sy, "slate")
        carreteras(im, v, "lineas"); via(im, v, "lineas"); recinto(im, v, "concrete")
        pos = [ruta(im, v, r, f, "lineas") for r in RUTAS]
        for k in ("paso", "estacion", "tren", "heli"):
            sitio(im, v, k, f, "anillo")
        for (x, y), r in zip(pos, RUTAS):
            unidad(im, x, y, r[1], r[4])
        hud_mapa(im); leyenda(im, W - 176, H - 66, "lineas")
    else:                                                 # E: mapa a la izquierda + lista de frentes y tránsitos
        v = Vista(esc=1.25, ox=220, oy=196)
        im = fondo(v, "tactico", 0, 30, 440, H)
        carreteras(im, v, "tactico"); via(im, v, "tactico"); recinto(im, v)
        pos = [ruta(im, v, r, f, "tactico") for r in RUTAS]
        for k in ("paso", "estacion", "tren", "heli"):
            d = SITIOS[k]
            x, y = map(int, v.p(*d["pos"]))
            col = COL_ESTADO[d["estado"]] if not (f == 1 and d["estado"] in ("ataque", "aviso")) else \
                {"ataque": "red", "aviso": "ochre_dark"}[d["estado"]]
            caja(im, x - 10, y - 10, 20, 20, "navy", col, col)
            im.alpha_composite(marcador(k).img, (x - 8, y - 8))
        for (x, y), r in zip(pos, RUTAS):
            ic = icono_unidad(r[1])
            caja(im, int(x - 6), int(y - 6), 13, 13, "navy", "bone", "grey_brown")
            im.alpha_composite(ic.img, (int(x - ic.w // 2), int(y - ic.h // 2)))
        # columna derecha
        caja(im, 444, 30, 190, H - 36, "navy", "grey_brown", "grey_brown")
        txt(im, (452, 34), "Frentes", "cream")
        y = 50
        for k in ("tren", "paso", "estacion"):
            d = SITIOS[k]
            col = COL_ESTADO[d["estado"]] if not (f == 1 and d["estado"] in ("ataque", "aviso")) else \
                {"ataque": "red", "aviso": "ochre_dark"}[d["estado"]]
            caja(im, 450, y, 178, 22, "navy", col, col)
            im.alpha_composite(marcador(k).img, (453, y + 3))
            txt(im, (474, y + 2), d["nombre"], "bone")
            if d["vida"] is not None:
                txt(im, (600, y + 2), f"{int(d['vida'] * 100)} %", "bone")
                for xx in range(140):
                    _pon(im, 474 + xx, y + 16, col if xx < 140 * d["vida"] else "grey_brown")
                    _pon(im, 474 + xx, y + 17, col if xx < 140 * d["vida"] else "grey_brown")
            else:
                txt(im, (474, y + 11), "Horda cerca", "beige", 8)
            y += 26
        txt(im, (452, y + 4), "En camino", "cream")
        y += 20
        for r in RUTAS:
            caja(im, 450, y, 178, 22, "navy", "grey_brown", "grey_brown")
            ic = icono_unidad(r[1])
            im.alpha_composite(ic.img, (458 - ic.w // 2 + 3, y + 11 - ic.h // 2))
            txt(im, (474, y + 2), r[0], "bone")
            e = eta(r[4])
            im.alpha_composite(e, (626 - e.width, y + 10 - 1))
            dest = "Tren"
            txt(im, (474, y + 11), {"via": "por la vía", "carretera": "por carretera", "aire": "por el aire"}[r[2]] +
                f" → {dest}", "beige", 8)
            y += 26
        caja(im, 450, y + 4, 178, 22, "navy", "grey_light", "grey_light")
        im.alpha_composite(helipuerto_16().img, (453, y + 7))
        txt(im, (474, y + 6), "Helipuerto · NH90 en misión", "bone")
        hud_mapa(im)
        leyenda(im, W - 176, H - 66)
    return im


VARIANTES = "ABCDE"
NOTAS = {"A": "Plano táctico oscuro: zonas apagadas, placas de 20 px con borde de estado y nombre con barra de vida debajo",
         "B": "Mapa de papel: fondo claro, trazos oscuros; las mismas placas y etiquetas",
         "C": "Mapa de colores velado en navy; alfileres con mástil sobre cada frente",
         "D": "Pantalla de mando: solo líneas sobre navy con retícula; anillos de estado alrededor de cada frente",
         "E": "Mapa a la izquierda y columna de frentes y tránsitos a la derecha (ETA en la lista y en el mapa)"}


def muestra(out):
    import build_maquetas as m
    hoja = Image.new("RGBA", (2 * 640 + 12, 5 * 384), PAL["navy"])
    for i, v in enumerate(VARIANTES):
        y = i * 384
        m.texto(hoja, (6, y + 4), f"{v} · {NOTAS[v]}", 9, "cream")
        a = pantalla(v, 0)
        hoja.alpha_composite(a, (0, y + 22))
        hoja.alpha_composite(pantalla(v, 1), (652, y + 22))
        a.resize((1920, 1080), Image.NEAREST).save(out / f"muestra_mapa_{v}_1080.png")
    hoja.save(out / "muestra_mapa_frentes_5_variantes.png")


# --- Piezas finales (variante A = D-N113) -----------------------------------
ESC = 1.8                                                  # px de la base 640x360 por metro (x3 a 1080p)
PASO = 6                                                   # periodo de las teselas diagonales


def _banda(dibuja, d):
    """Dibuja un trazo diagonal largo y recorta una banda de PASO px de ancho: tesela que repite en diagonal."""
    im = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    if d == "x":                                           # +X del mundo: arriba a la derecha
        dibuja(im, (0, 60), (60, 0))
    else:                                                  # +Z del mundo: arriba a la izquierda
        dibuja(im, (60, 60), (0, 0))
    c0 = 24
    caja_ = im.crop((c0, 0, c0 + PASO, 64))
    bb = caja_.getbbox()
    centro = 36 if d == "x" else 24                        # y del eje del trazo en la columna c0
    return caja_.crop((0, bb[1], PASO, bb[3])), centro - bb[1]


def placa_estado(estado, f=0):
    col = COL_ESTADO[estado]
    if f == 1:
        col = {"ataque": "red", "aviso": "ochre_dark"}.get(estado, col)
    im = Image.new("RGBA", (20, 20), (0, 0, 0, 0))
    caja(im, 0, 0, 20, 20, "navy", col, col)
    return im


def etiqueta_9():
    im = Image.new("RGBA", (8, 13), (0, 0, 0, 0))
    caja(im, 0, 0, 8, 13, "navy", "grey_brown", "grey_brown")
    return im


def chip_unidad():
    im = Image.new("RGBA", (13, 13), (0, 0, 0, 0))
    caja(im, 0, 0, 13, 13, "navy", "bone", "grey_brown")
    return im


def aspa_7(c="grey_brown"):
    im = Image.new("RGBA", (7, 7), (0, 0, 0, 0))
    linea(im, (0, 0), (6, 6), c); linea(im, (6, 0), (0, 6), c)
    return im


def fin_flecha(c, ang):
    im = Image.new("RGBA", (11, 11), (0, 0, 0, 0))
    t = math.radians(ang)
    flecha(im, 5, 5, math.cos(t), -math.sin(t), c)
    return im


def fondo_completo():
    """Mapa de Sagunto entero girado 45° a ESC px/m, sin carreteras con estado (las pinta el juego desde el dato)."""
    esquinas = [(-250, -200), (506, -200), (506, 480), (-250, 480)]
    v0 = Vista(0, 0, ESC, 0, 0)
    xs, ys = zip(*[v0.p(*q) for q in esquinas])
    ox, oy = -min(xs), -min(ys)
    Wf, Hf = int(math.ceil(max(xs) - min(xs))) + 1, int(math.ceil(max(ys) - min(ys))) + 1
    v = Vista(0, 0, ESC, ox, oy)
    z = _zonas()
    tabla = FONDOS["tactico"]
    im = Image.new("RGBA", (Wf, Hf), (0, 0, 0, 0))
    px = im.load()
    for sy in range(Hf):
        for sx in range(Wf):
            X, Z = v.mundo(sx + .5, sy + .5)
            a, b = int(X + 250), int(480 - Z)
            if 0 <= a < 756 and 0 <= b < 680:
                px[sx, sy] = PAL[tabla.get(z[a, b], "olive_shadow")]
    via(im, v, "tactico"); recinto(im, v)
    return im, ox, oy


def exportar(out):
    import json
    out = Path(out)
    fo, ox, oy = fondo_completo()
    fo.save(out / "ui_mapa_fondo_sagunto.png")
    for e in ("ok", "base"):
        placa_estado(e).save(out / f"ui_mapa_placa_{e}.png")
    for e in ("aviso", "ataque"):
        h = Image.new("RGBA", (40, 20), (0, 0, 0, 0))
        for f in range(2):
            h.alpha_composite(placa_estado(e, f), (20 * f, 0))
        h.save(out / f"ui_mapa_placa_{e}.png")
    etiqueta_9().save(out / "ui_mapa_etiqueta.png")
    chip_unidad().save(out / "ui_mapa_unidad.png")
    paso_nivel_16().img.save(out / "ui_frente_paso_nivel_16.png")
    helipuerto_16().img.save(out / "ui_mapa_helipuerto_16.png")
    heli_9().img.save(out / "ui_mapa_nh90_11x9.png")
    for n, c in (("fondo", "grey_brown"), ("ok", "beige"), ("aviso", "amber"), ("ataque", "red_bright")):
        t = Image.new("RGBA", (1, 1), PAL[c]); t.save(out / f"ui_mapa_vida_{n}.png")
    aspa_7().save(out / "ui_mapa_aspa_7.png")
    teselas = {}
    estilos = {
        "carretera_habilitada": lambda im, a, b: linea(im, a, b, "concrete_light", 3),
        "carretera_no_habilitada": lambda im, a, b: linea(im, a, b, "grey_brown", 3, lambda i: i % 6 < 3),
        "via": lambda im, a, b: via_px(im, a, b, "beige"),
        "ruta_via": lambda im, a, b: linea(im, a, b, RUTA_COL["via"], 2, lambda i: i % 6 < 4),
        "ruta_carretera": lambda im, a, b: linea(im, a, b, RUTA_COL["carretera"], 2, lambda i: i % 6 != 5),
        "ruta_hecha": lambda im, a, b: linea(im, a, b, "grey_brown", 1),
    }
    for nombre, fn in estilos.items():
        for d in ("x", "z"):
            t, ay = _banda(fn, d)
            t.save(out / f"ui_mapa_{nombre}_{d}.png")
            teselas[f"ui_mapa_{nombre}_{d}"] = {"tamano": list(t.size), "ancla": [0, ay],
                                                 "paso": [PASO, -PASO] if d == "x" else [-PASO, -PASO]}
    Image.new("RGBA", (1, 1), PAL[RUTA_COL["aire"]]).save(out / "ui_mapa_ruta_aire_punto.png")
    for nombre, c in (("via", RUTA_COL["via"]), ("carretera", RUTA_COL["carretera"]), ("aire", RUTA_COL["aire"])):
        h = Image.new("RGBA", (88, 11), (0, 0, 0, 0))
        for k in range(8):
            h.alpha_composite(fin_flecha(c, 45 * k), (11 * k, 0))
        h.save(out / f"ui_mapa_ruta_fin_{nombre}.png")
    kE = round(K * ESC, 5)
    (out / "ui_mapa_frentes.json").write_text(json.dumps({
        "variante": "A, plano táctico oscuro = D-N113 (Mak 00:55, Lucía 01:00)",
        "base": [640, 360], "escala_1080": 3, "px_por_metro": ESC,
        "fondo": {"png": "ui_mapa_fondo_sagunto", "tamano": list(fo.size),
                  "mundo_a_px": f"x = {ox:.2f} + {kE} * (X - Z);  y = {oy:.2f} - {kE} * (X + Z)",
                  "nota": "girado 45° como la cámara (D-N93): arriba = +X+Z. Lleva la vía y el muro; las carreteras con estado no"},
        "carreteras": {"dato": "mapa_sagunto_carreteras.json",
                       "habilitada": "ui_mapa_carretera_habilitada_{x|z}", "no_habilitada": "ui_mapa_carretera_no_habilitada_{x|z} + ui_mapa_aspa_7 cada 24 px",
                       "uso": "cada tramo va a lo largo de X (tesela _x) o de Z (_z): la columna 0 y la fila «ancla» de la tesela caen sobre el eje del tramo; se estampa cada «paso» (en _z, de derecha a izquierda)"},
        "teselas": teselas,
        "sitios": {"placa": "ui_mapa_placa_{ok|aviso|ataque|base} (20x20, slice 3; aviso y ataque, 2 f de 500 ms; el aviso parpadea solo 3 s, D-N109)",
                   "iconos": {"estacion": "ui_frente_estacion_16", "tren": "ui_frente_tren_16", "paso_nivel": "ui_frente_paso_nivel_16",
                              "helipuerto": "ui_mapa_helipuerto_16 (con ui_mapa_placa_base)"},
                   "etiqueta": "ui_mapa_etiqueta (slice 3, 13 px de alto) 12 px por debajo de la placa (por encima si queda a menos de 40 px del borde de abajo o es el paso a nivel); nombre y % en la letra de UI Toolkit",
                   "vida": "1 px bajo el texto, en la fila 11 de la etiqueta: ui_mapa_vida_fondo + ui_mapa_vida_{ok|aviso|ataque} estirado a (ancho − 6) × vida"},
        "unidades": {"chip": "ui_mapa_unidad (13x13, slice 3)", "iconos": {"vehiculo": "ui_mm_vehiculo", "peloton": "ui_mm_peloton", "helicoptero": "ui_mapa_nh90_11x9"},
                     "eta": "a 9 px a la derecha del chip: ui_eta_placa + ui_eta_reloj_7 (x 2, y 1) + «ETA mm:ss» con ui_letras_3x5 y ui_digitos_3x5 (x 11, y 2, paso 4)"},
        "rutas": {"falta": "ui_mapa_ruta_{via|carretera}_{x|z}; aire: ui_mapa_ruta_aire_punto cada 4 px en línea recta",
                  "hecha": "ui_mapa_ruta_hecha_{x|z} (1 px grey_brown)", "fin": "ui_mapa_ruta_fin_{via|carretera|aire}: 8 direcciones de 11x11, 45° cada una empezando por la derecha, en sentido antihorario",
                  "marcha": "las teselas avanzan 3 px por fotograma (aire, 2) para que la ruta se mueva hacia el destino"},
        "colores": "rojo #982420 solo en el ataque; ámbar solo en el aviso; H del helipuerto en grey_light (E6 y foto H7 de Paula); nada en 283440",
    }, ensure_ascii=False, indent=1), encoding="utf-8")
    (out / "mapa_sagunto_carreteras.json").write_text(DATOS.read_text(encoding="utf-8"), encoding="utf-8")


def maqueta(out):
    out = Path(out)
    for f in range(2):
        pantalla("A", f).resize((1920, 1080), Image.NEAREST).save(out / f"maqueta_1080_mapa_frentes{'_f1' if f else ''}.png")


if __name__ == "__main__":
    OUT = Path(next((a for a in sys.argv[1:] if not a.startswith("--")), "out_mapa_frentes"))
    OUT.mkdir(parents=True, exist_ok=True)
    if "--muestra" in sys.argv:
        muestra(OUT)
    else:
        exportar(OUT)
        maqueta(OUT)
    print("ok")
