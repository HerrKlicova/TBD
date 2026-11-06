"""Regenera los atlas listos para importar en Unity: muerte del zombi, zombi LOD y oficial.
Bruno (Arte técnico), 6-nov-2026.

Los PNG no se pueden subir desde la nube (Git LFS bloqueado por el proxy), así que se generan en
local con el mismo código que los aprobados por Lucía. Es determinista: salen idénticos.

Uso (desde la raíz del repo, con Python 3 y Pillow):
    python .claude/skills/pixel-art-codigo/ejemplos/exportar_sprites.py <carpeta_salida>
Salida: atlas_zombi_muerte.png/.json (160x192), atlas_zombi_lod.png/.json (40x8),
        atlas_oficial.png/.json (160x128),
        vm_taller_{puerta_via_rota,tren_nuevo,rehabilitar,chispas}_{32,16}.png (iconos de UI),
        vm_alertas_aviso_{32,16}.png (aviso ámbar de la etiqueta de construcción). Importar con filtro de punto, sin compresión.
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
    for t in ("aceite", "escombros", "quemado"):
        for v in (0, 1):
            getattr(build_decals, t)(v).save(out / f"decal_{t}_{v + 1}_32.png")


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
    print("atlas en", out.resolve())
