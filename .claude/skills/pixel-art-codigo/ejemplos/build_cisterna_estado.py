"""Decal de lleno / vacío del vagón cisterna (Castellón, castellon-nivel.md §1). Bruno (Arte técnico), 7-nov.

Va sobre el panel liso de Teo (2,6 x 1,1 m, placa 605c48, a 1 cm de la cuba, en los dos costados;
bloques/vagon_cisterna.py, TBD 9c0885d). Texel 0,1 m: 26x11 px. Copia a x2 (52x22) para la vista cercana del tren.

Lectura a tamaño de juego (el mundo va a 1/3: el panel ocupa unos 18x8 px de pantalla a ortho 26):
  claro = lleno  (barra beige b4a080 en 4 tramos, ΔL* 28 sobre el 605c48)
  oscuro = vacío (los mismos 4 tramos solo con el filo, el interior en grey_brown 48403c)
Sin texto: a ese tamaño no se lee. Sin d4d0cc (selección), sin ámbar ni rojo. La placa naranja ADR real
no se pinta, porque el naranja chocaría con el ámbar reservado (SIN VERIFICAR con Lucía).
"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from pixelvm import PAL, Sprite  # noqa: E402

W, H = 26, 11


def panel(lleno):
    s = Sprite(W, H)
    s.rect(0, 0, W, H, "olive_grey")                 # el panel de Teo (se ve en los bordes del decal)
    s.rect(1, 1, W - 2, H - 2, "olive_shadow")       # marco hundido del indicador
    for k in range(4):                               # 4 tramos de 5 px con 1 px de junta
        x0, w = 2 + k * 6, 5
        if lleno:
            s.rect(x0, 3, w, 5, "beige")
            s.hline(x0, x0 + w - 1, 3, "olive_light")    # luz arriba
            s.hline(x0, x0 + w - 1, 7, "ochre_dark")
        else:
            s.rect(x0, 3, w, 5, "grey_brown")
            s.hline(x0, x0 + w - 1, 3, "olive_grey")     # solo el filo, para que se vea que hay tramos
    return s


if __name__ == "__main__":
    OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "out_cisterna")
    OUT.mkdir(parents=True, exist_ok=True)
    pl, pv = panel(True), panel(False)
    for n, s in (("lleno", pl), ("vacio", pv)):
        s.img.save(OUT / f"decal_cisterna_{n}_26x11.png")
        s.img.resize((W * 2, H * 2), Image.NEAREST).save(OUT / f"decal_cisterna_{n}_52x22.png")
    # hoja: x8 y simulación a tamaño de juego (1/3 de la resolución y vuelta a x3, sobre la cuba 605c48)
    E = 8
    hoja = Image.new("RGBA", (W * E * 2 + 24, H * E + 24 + 60), PAL["navy"])
    hoja.paste(pl.img.resize((W * E, H * E), Image.NEAREST), (8, 8))
    hoja.paste(pv.img.resize((W * E, H * E), Image.NEAREST), (W * E + 16, 8))
    for i, s in enumerate((pl, pv)):
        cuba = Image.new("RGBA", (40, 16), PAL["olive_grey"])
        cuba.alpha_composite(s.img, (7, 2))
        peq = cuba.resize((cuba.width * 2 // 3, cuba.height * 2 // 3), Image.BOX)   # aprox. a tamaño de juego
        hoja.paste(peq.resize((peq.width * 3, peq.height * 3), Image.NEAREST), (8 + i * (W * E + 8), H * E + 16))
    hoja.save(OUT / "muestra_cisterna_lleno_vacio.png")
    print("ok")
