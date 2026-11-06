---
name: pixel-art-codigo
description: Dibujar pixel art del proyecto TBD por código (iconos de interfaz, marcadores del mapa, estados, variantes de color y VFX sencillos como humo o polvo) con la paleta aprobada de 32 colores y las reglas de D-N46, sin gastar generaciones de Pixelab. Úsala antes de pedir a Pixelab un icono, un marcador o una variante, y para limpiar o recolorear lo que salga de Pixelab.
---

# TBD · Pixel art por código

Escrita el 6-nov-2026 por Bruno (Arte técnico) tras revisar 25 repos, skills y MCP públicos (lista y veredicto en
`referencias/externos.md`). La herramienta es `scripts/pixelvm.py`: propia, solo necesita Pillow y es determinista.
Toma las ideas de pxl, Pixel Art Studio, pixelfx y Pixelbolt (todos MIT) sin copiar su código.

**El modelo es el artista y el script es su mano.** Ningún resultado sale en una sola pasada: dibujar, mirar la hoja de
contacto, criticar con la lista de `referencias/criterios.md` y corregir. Mínimo dos pasadas antes de enseñarlo.

## Cuándo código y cuándo Pixelab
| Encargo | Herramienta |
|---|---|
| Iconos de recurso, alerta y orden (A1–A3), marcadores del mapa (A4–A5), sellos y piezas (fase B) | **Código** |
| Variantes de estado o de color de algo ya dibujado (seguro, amenazado, asedio, perdido) | **Código** (`replace`, sin generar) |
| VFX de cuerpo sencillos: humo, polvo, vapor, chispas en bucle | **Código** (ver `ejemplos/build_a8_humo.py`) |
| Reducir de 32 a 16 px y repasar el contorno (D-N46) | **Código** (`reduce_16`, y a mano si no se lee) |
| Retratos (fase C), ilustraciones de evento (E), logotipo, texturas ricas (G) | **Pixelab**: el código da formas limpias pero planas |
| Unidades con mucho detalle (A6) | Primero una prueba por código; si no llega, Pixelab |

## Reglas fijas del proyecto (D-N46)
- **Paleta estricta** de 32 (`docs/arte/paleta_propuesta_32.hex`); en `pixelvm.PAL` con nombres de trabajo. Nada fuera de paleta.
- Paleta apagada y desaturada: óxido, verde oliva, gris hormigón. **Sin sangre, sin calaveras, sin texto, sin marco.**
- **Acento sin rojo**. En recursos, `check(s, recurso=True)` rechaza los cinco rojos de la paleta.
- Acentos por carga (Lucía): munición latón y ámbar, combustible oliva, materiales gris acero, víveres ocre y beige, población hueso.
- Siluetas de alto contraste, contorno exterior de 1 px (`outline()`, negro de la paleta), luz desde arriba a la izquierda.
- Tamaños: 32×32 de origen; 16×16 reduciendo el de 32 y repasando. Comprobar siempre a ×1 sobre `#181c30` (la hoja lo incluye).
- Alfa binaria (nada semitransparente): los desvanecidos se hacen con tramado Bayer.

## Flujo
1. Copia el ejemplo más parecido de `ejemplos/` a `Art/Codigo/<bloque>/build_<bloque>.py` (o a una carpeta de trabajo).
2. Una función por icono. Dibuja de lo grande a lo pequeño: silueta, masas de color, luz y sombra, detalles con causa.
3. `python build_<bloque>.py <carpeta_salida>`: escribe `vm_<bloque>_<nombre>_32.png`, `_16.png` y `contacto_*.png`.
4. **Mira** `contacto_*.png` (×6, tamaño real y silueta) y critica con `referencias/criterios.md`.
5. Corrige y repite. Lee los avisos de `check()`: paleta, rojos, bordes, contraste y número de colores.
6. Entrega los PNG a ×1 y la hoja de contacto. El script es la fuente: nunca retoques el PNG a mano.

## API de `pixelvm`
- `Sprite(w, h)`: `set`, `rect`, `hline`, `vline`, `line` (Bresenham), `ellipse`, `poly`, `grid(x, y, texto, claves)`
  (un carácter por píxel; `.` no pinta, `_` borra). Todas aceptan `only="opaque" | "empty" | color` para enmascarar.
- Pixel art: `outline()`, `selout(rampa)` (contorno interior en sombra), `mirror_x()`, `replace(viejo, nuevo)`,
  `lock_palette()` (para importar o limpiar PNG de Pixelab), `stamp(otro, x, y)`, `silhouette()`.
- `RAMPAS`: oliva, acero, latón, madera, ocre, hueso, óxido y hormigón (de oscuro a claro).
- `reduce_16(s32)`, `check(s, recurso=False)`, `contact_sheet(filas, ruta, scale, labels)`.

## Hábitos que dan mejor resultado
- Escribe la mitad de lo simétrico y usa `mirror_x()`; deja que `outline()` dibuje el contorno.
- Las familias se hacen con bucles y las variantes con `replace`: una variante no cuesta nada.
- **Nada de ruido de relleno**: cada mancha necesita una causa (óxido bajo un remache, una junta, un pliegue).
- 2–4 tonos por material. Más de 12 colores en un icono de 32 es una señal de alarma.
- Si la reducción a 16 px no se lee, redibújala con `grid` (ver `poblacion_16` en `ejemplos/build_a1.py`).

## Limpiar lo que sale de Pixelab
`docs/arte/herramientas/pixel_post.py` (Iris) quita el fondo y reduce a la paleta. Para más control:
`s = Sprite(32, 32); s.img = Image.open(png).convert("RGBA"); s.px = s.img.load(); s.lock_palette()`, y después
`replace` para quitar rojos o unificar acentos y `check()` para validar.
