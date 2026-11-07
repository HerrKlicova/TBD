"""Regenera los atlas listos para importar en Unity: muerte del zombi, zombi LOD y oficial.
Bruno (Arte técnico), 6-nov-2026.

Los PNG no se pueden subir desde la nube (Git LFS bloqueado por el proxy), así que se generan en
local con el mismo código que los aprobados por Lucía. Es determinista: salen idénticos.

Uso (desde la raíz del repo, con Python 3 y Pillow):
    python .claude/skills/pixel-art-codigo/ejemplos/exportar_sprites.py <carpeta_salida>
Salida: atlas_zombi_muerte.png/.json (160x192), atlas_zombi_lod.png/.json (40x8),
        atlas_oficial.png/.json (160x128),
        vm_taller_{puerta_via_rota,tren_nuevo,rehabilitar,chispas}_{32,16}.png (iconos de UI),
        vm_alertas_aviso_{32,16}.png (aviso ámbar de la etiqueta de construcción),
        suelo_*_64.png, atlas_decals_suelo, suelo_asfalto_liso_1..4_64.png, atlas_grietas_{asfalto,tierra}.png/.json. Importar con filtro de punto, sin compresión.
"""
import json
import sys
import tempfile
from pathlib import Path

from PIL import Image

AQUI = Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI))
sys.path.insert(0, str(AQUI.parent / "scripts"))

import build_zombi  # noqa: E402
import build_zombi_lod  # noqa: E402
import build_oficial  # noqa: E402
import build_taller  # noqa: E402
import build_a2  # noqa: E402
import build_sagunto  # noqa: E402
import build_decals  # noqa: E402
import build_suelo_sagunto  # noqa: E402
import build_asfalto_variado  # noqa: E402
import build_a9_pelotones  # noqa: E402
import build_interfaz  # noqa: E402
import build_vfx_combate  # noqa: E402
import build_maquetas  # noqa: E402
import build_horda_color  # noqa: E402
import build_vfx_tren  # noqa: E402
import build_parallax_tren  # noqa: E402
import build_armas  # noqa: E402
import build_vfx_vehiculos  # noqa: E402
import build_recursos_hud  # noqa: E402
import build_como_jugar  # noqa: E402
import build_tarjetas  # noqa: E402
import build_marcadores  # noqa: E402
import build_avisos_minimapa  # noqa: E402
import build_rutas  # noqa: E402
import build_frentes_aire  # noqa: E402
import build_estados_vehiculo  # noqa: E402
import build_apoyo_aereo  # noqa: E402
import build_apertura  # noqa: E402
import build_parte  # noqa: E402
import build_avisos_sara  # noqa: E402
import build_tren_vista  # noqa: E402
import build_mapa_frentes  # noqa: E402
import build_vfx_nh90  # noqa: E402

PIVOTE = [0.5, 0.53]


def atlas_muerte_zombi(out):
    m = [build_zombi.muerte(f) for f in range(5)]
    filas = []
    for c in sorted(build_zombi.CAMISAS):                   # mismo orden que atlas_zombi_andar (alfabético)
        for p in sorted(build_zombi.PANTALONES):
            filas.append((f"{c}_{p}", [build_zombi.vestir(s, c, p, cadaver=k >= 3) for k, s in enumerate(m)]))
    at = Image.new("RGBA", (32 * 5, 32 * len(filas)), (0, 0, 0, 0))
    for r, (_, fr) in enumerate(filas):
        for k, s in enumerate(fr):
            at.alpha_composite(s.img, (32 * k, 32 * r))
    at.save(out / "atlas_zombi_muerte.png")
    (out / "atlas_zombi_muerte.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "frameSize": [32, 32], "frames": 5, "fps": 8, "loop": False, "pivot": PIVOTE,
        "filas": [n for n, _ in filas], "nota": "mismo orden de filas que atlas_zombi_andar; el último se queda"}}}, indent=1))


def atlas_lod(out):
    fr = [build_zombi_lod.andar(f) for f in range(4)] + [build_zombi_lod.cadaver()]
    at = Image.new("RGBA", (8 * 5, 8), (0, 0, 0, 0))
    for k, s in enumerate(fr):
        at.alpha_composite(s.img, (8 * k, 0))
    at.save(out / "atlas_zombi_lod.png")
    (out / "atlas_zombi_lod.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "frameSize": [8, 8], "columnas": ["andar0", "andar1", "andar2", "andar3", "cadaver"], "fps_andar": 6,
        "loop_andar": True, "pivot": [0.5, 0.5], "nota": "D-N58: sin contorno ni variantes"}}}, indent=1))


def atlas_oficial(out):
    o = build_oficial
    filas = [("andar", [o.andar(f) for f in range(4)], 6, True),
             ("radio", [o.radio_anim(f) for f in range(2)], 3, True),
             ("disparo", [o.disparo(f) for f in range(3)], 12, False),
             ("muerte", [o.muerte(f) for f in range(5)], 8, False)]
    at = Image.new("RGBA", (32 * 5, 32 * len(filas)), (0, 0, 0, 0))
    for r, (_, fr, _, _) in enumerate(filas):
        for k, s in enumerate(fr):
            at.alpha_composite(s.img, (32 * k, 32 * r))
    at.save(out / "atlas_oficial.png")
    (out / "atlas_oficial.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "frameSize": [32, 32], "pivot": PIVOTE, "orientacion": "norte (+Y de la imagen)",
        "filas": [{"anim": n, "fila": r, "frames": len(fr), "fps": fps, "loop": b}
                  for r, (n, fr, fps, b) in enumerate(filas)], "nota": "fila 0 arriba"}}}, indent=1))


def iconos_taller(out):
    """Iconos de UI del taller y de la puerta rota, a 32 y 16 px (sin atlas: uno por archivo)."""
    for n in ("puerta_via_rota", "tren_nuevo", "rehabilitar", "chispas"):
        s = getattr(build_taller, n)()
        s.limit_colors(10)
        s.save(out / f"vm_taller_{n}_32.png")
        build_taller.icono_16(n, s).save(out / f"vm_taller_{n}_16.png")



def aviso_ambar(out):
    """Icono de aviso en ámbar para la etiqueta de construcción (D-N62), a 32 y 16 px."""
    s = build_a2.aviso()
    s.limit_colors(10)
    s.save(out / "vm_alertas_aviso_32.png")
    build_a2.aviso_16().save(out / "vm_alertas_aviso_16.png")



def ambientacion_sagunto(out):
    """Línea de seguridad, placa, reloj y hollín de la vía (ambientacion-sagunto.md), y los decals de suelo."""
    b = build_sagunto
    b.linea_seguridad().save(out / "decal_linea_seguridad_64x2.png")
    b.placa_sagunto().save(out / "placa_sagunto.png")
    b.reloj(16).save(out / "reloj_fachada_16.png")
    b.reloj(24).save(out / "reloj_fachada_24.png")
    b.via_hollin().save(out / "decal_via_hollin_64x32.png")
    b.pintada_texto().save(out / "decal_pintada_aqui_hay_vivos.png")      # sagunto-nivel.md v1.1, §3
    b.pintada_flecha().save(out / "decal_pintada_flecha.png")
    b.hollin_puerta(0).save(out / "decal_hollin_puerta_1_64.png")
    b.hollin_puerta(1).save(out / "decal_hollin_puerta_2_64.png")
    for t in ("aceite", "escombros", "quemado"):
        for v in (0, 1):
            getattr(build_decals, t)(v).save(out / f"decal_{t}_{v + 1}_32.png")



def suelo_sagunto(out):
    """D-N67, capas A y B: 6 texturas de suelo de 64x64 (téxel 0,1 m) y el atlas de decals de suelo."""
    b = build_suelo_sagunto
    for n in b.TEXTURAS:
        getattr(b, n)().save(out / f"suelo_{n}_64.png")
    at, rects = b.atlas()
    at.save(out / "atlas_decals_suelo.png")
    (out / "atlas_decals_suelo.json").write_text(json.dumps({"meta": {"viaMuerta": {
        "celda": [b.C, b.C], "texel_m": 0.1, "rects": rects}}}, indent=1))


def asfalto_variado(out):
    """Asfalto sin repetición: suelo_asfalto_liso_1..4_64 (base por hash) y atlas de grietas de asfalto y de tierra."""
    build_asfalto_variado.exportar(out)


def prototipo_noche(out):
    """Noche del 6-nov (plan-noche-7nov.md): iconos de pelotón y NH90 (A9), interfaz (paneles, botones,
    tarjetas, barras, alertas, cursores, órdenes, marcadores, logo y fondo del menú), señalización de la
    carretera y del helipuerto, y VFX de combate (impacto, granada de 40 mm, sangre, restos y polvo de rotor).
    Los iconos de fusileros y del Dragón (blindado 8x8) son los de A6."""
    s32, s16 = build_a9_pelotones.iconos()
    for n in build_a9_pelotones.NOMBRES:
        s32[n].save(out / f"vm_unidades_{n}_32.png")
        s16[n].save(out / f"vm_unidades_{n}_16.png")
    build_interfaz.exportar(out)
    build_vfx_combate.exportar(out)
    build_maquetas.exportar(out)                         # D-N78: cartel, indicador, recarga, estrellas y ratón
    build_horda_color.exportar(out)                      # D-N80: LUT de color de la horda y rampa de sangre
    build_vfx_tren.exportar(out)                         # vista cercana del tren: chispas, faro, ventanillas, polvo
    build_parallax_tren.exportar(out)                    # fondo del tren: cielo, sierra y colinas, día y noche
    build_armas.exportar(out)                            # iconos del HUD y VFX de disparo e impacto por arma
    build_vfx_vehiculos.exportar(out)                    # polvo en marcha, orugas, humo, fuego, explosión, calcinado
    build_recursos_hud.exportar(out)                     # P0: recursos (con pila y bajas), iconos de resultados, estrellas
    build_como_jugar.exportar(out)                       # teclas A S D E y estados del pelotón
    build_tarjetas.exportar(out)                         # P1: retratos de tarjeta, 6 variantes por tipo
    build_marcadores.exportar(out)                       # P1: selección, órdenes, cono, arrastre, aviso de entrada, cursores
    build_avisos_minimapa.exportar(out)                  # P1: avisos del HUD y minimapa (base de Sagunto e iconos)
    build_rutas.exportar(out)                            # mundo persistente: ETA, rutas, refuerzos y ruta no válida
    build_frentes_aire.exportar(out)                     # frentes activos, iconos aéreos y aviso de cazas
    build_estados_vehiculo.exportar(out)                 # estados de vehículo y avisos del tren
    build_apoyo_aereo.exportar(out)                      # sombra y altura del helicóptero, helipuerto en 3 estados, humo, fuego, cráter
    build_apertura.exportar(out)                         # apertura (D-N91): rótulo de inicio, primera oleada, barra de la noche
    build_parte.exportar(out)                            # parte de la noche, victoria y derrota (variante A)
    build_avisos_sara.exportar(out)                      # sin munición (T), muro o estación atacados, panel de vehículos
    build_tren_vista.exportar(out)                       # vista cercana del tren (D-N110): esquema, cajas, franja y avisos
    build_mapa_frentes.exportar(out)                     # mapa de frentes de Sagunto (variante A): fondo, placas, teselas y dato de carreteras
    build_vfx_nh90.exportar(out)                         # polvo del rotor en la losa, MAG de puerta y sombra del NH90 (estilo A, sombra D)


if __name__ == "__main__":
    out = Path(sys.argv[1] if len(sys.argv) > 1 else "sprites_exportados")
    out.mkdir(parents=True, exist_ok=True)
    build_zombi.OUT = Path(tempfile.mkdtemp())
    atlas_muerte_zombi(out)
    atlas_lod(out)
    atlas_oficial(out)
    iconos_taller(out)
    aviso_ambar(out)
    ambientacion_sagunto(out)
    suelo_sagunto(out)
    asfalto_variado(out)
    prototipo_noche(out)
    print("atlas en", out.resolve())
