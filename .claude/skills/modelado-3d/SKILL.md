---
name: modelado-3d
description: Pasar un modelo 3D (Hunyuan, TRELLIS, base CC0 o bloque hecho a mano) a pieza de juego de TBD en Blender. Úsala para limpiar, diezmar a 1.500–4.000 triángulos, verificar la malla (normales, manifold, escala, UV, origen), dar albedo plano con la paleta de 32, revisar con una vista pixelada y exportar a Unity, y para preparar las 4 vistas de Hunyuan3D-2mv.
---

# TBD · Modelado 3D con Claude en Blender

Escrita el 6-nov-2026 (hilo «Investigar herramientas para modelar en 3D»). Lo marcado **(verificado)** se comprobó en código,
documentación o en el Blender de Eugenio; lo marcado **(no verificado)** falta probarlo.

## Qué hay instalado y dónde
- **Blender 5.2.1 LTS** en el PC de Eugenio, con el **MCP oficial de Blender Lab** (extensión `mcp`, 26 herramientas:
  `execute_blender_code`, resúmenes de escena, capturas, render y búsqueda en la API y el manual) **(verificado)**.
  El modo de consola (`*_for_cli`) **no funciona todavía**: falta la variable `BLENDER_PATH` en el PC.
- **Addon «Image to 3D» 1.0.3 (3DRedbox)** en `D:\3D ENGINES\`, con Hunyuan3D 2.1, **Hunyuan3D-2mv instalado**
  (`hy_mv_ready() = True`) y TRELLIS.2. Pixal3D y Hi3D no están instalados **(verificado)**.
- **`scripts/malla3d.py`** (esta skill): todo el repaso de la malla en un solo archivo, sin dependencias aparte de Blender
  y numpy (que trae Blender). No usa la red ni gasta generaciones.

## Flujo de una pieza (D-N54)
1. **Generar** en el addon (panel N › Image to 3D). Para piezas clave, **con 4 vistas** (ver abajo).
2. **Procesar** con `procesar()`: importa, limpia, escala a la altura real, remalla por vóxeles, diezmado con control de
   silueta, albedo plano en paleta, valor partido (D-N57), facción, auditoría, legibilidad, vistas pixeladas y FBX. Deja `<nombre>_informe.json` y PNG de revisión.
   Por defecto **remalla por vóxeles** (1/160 de la dimensión mayor) antes de diezmar: en la prueba con un GLB real de
   Hunyuan dejó la malla estanca (0 aristas no manifold, 0 de borde) y veredicto OK, frente a 127 aristas de borde y
   110 vértices duplicados sin vóxeles. Con `voxel=False` se conservan las UV originales.
3. **Mirar las vistas** (`_rts.png` contra `_ORIGINAL_rts.png`). Si el veredicto es ERROR, no se exporta a Unity.
4. **Detalle a mano** donde haga falta (ver «Añadir detalle»), y otra vez `auditar()`.
5. **Unity**: Marcos importa el FBX con la textura `T_Paleta32.png` en filtro de punto y lo mira bajo el shader
   (regla 13: captura de día y de noche).

### Desde el MCP oficial (lo normal)
```python
import bpy
ruta = r"C:/Dev/TBD/.claude/skills/modelado-3d/scripts/malla3d.py"
ns = {"__file__": ruta, "__name__": "malla3d"}          # __file__ hace falta para encontrar la paleta
exec(open(ruta, encoding="utf-8").read(), ns)
inf = ns["procesar"](r"D:/3D ENGINES/output/loco.glb", r"C:/Dev/TBD/Assets/Art/_staging/loco",
                     nombre="SM_Loco_S592", objetivo=3000, altura_m=4.2, faccion="propio")
result = {"veredicto": inf["auditoria"]["veredicto"], "intentos": inf["diezmar_intentos"]}
```
- Trabajar en una **escena temporal** (`bpy.data.scenes.new`, `context.window.scene = tmp`) y borrarla al acabar, para no
  tocar lo que Eugenio tenga abierto.
- Un GLB de ~100.000 triángulos tarda 6–30 s; el MCP corta la espera a los 60 s, así que **una pieza por llamada**.

### Funciones sueltas
| Función | Para qué |
|---|---|
| `auditar(obj, presupuesto=(1500,4000), altura_m=None)` | Informe OK/AVISO/ERROR: triángulos, aristas no manifold y de borde, geometría suelta, caras degeneradas, n-gons, vértices duplicados, islas diminutas, islas con normales hacia dentro (volumen con signo), escala y rotación aplicadas, altura real, origen en la base, UV (fuera de 0–1, sin área, solape) y materiales |
| `limpiar(obj)` | Aplica transformaciones, fusiona duplicados, quita sueltos, caras degeneradas e islas <1 % (las motas de Hunyuan), normales hacia fuera, triangula n-gons y lleva el origen al centro de la base |
| `escalar_a_altura(obj, m)` | Hunyuan sale en un cubo de ~1 unidad: escala a la medida real. **Siempre después de `limpiar`** (las motas falsean la altura) |
| `escalar_a_largo(obj, m)` | Vehículos: escala por la longitud (la altura de las fichas no suele aclarar si incluye la torre) |
| `alisar_superficie(obj, 20)` | Laplaciano que conserva volumen, sobre la malla densa: quita las ondulaciones de Hunyuan que la luz marca como manchas. `procesar(alisar=20)` |
| `remallar_voxel(obj)` | Cierra agujeros y une piezas (malla estanca). Pierde las UV; el color se recupera de la original |
| `diezmar(obj, objetivo=3000)` | Disolución planar de 2° + colapso con búsqueda binaria hasta ±4 % + separa aristas no manifold. Mide la **silueta en 4 vistas** con 1 px de tolerancia a 256 px; pasa con ≥ 0,985 (la IoU estricta queda como dato) |
| `paleta_desde_original(obj, original)` | Albedo plano: 7 muestras por cara de la textura original, la paleta más repetida, tope de 10 colores, mayoría entre vecinas, UV a un atlas 8×4 (`T_Paleta32.png`, 32×16 px). **Sin ningún rojo ni el ámbar**: los rojos y naranjas de origen pasan por valor a oliva, ocre o gris (Lucía) |
| `valor_partido(obj, claro=None, costado=None)` | **D-N57**: caras de arriba al beige de la paleta (b4a080 cálido o 94948c neutro), costados un paso más oscuros (o un color fijo, p. ej. `costado="50541c"` oliva oscuro para tropa), bajos más oscuros aún. Horneado en el albedo, no depende de la luz de Unity |
| `marcar_faccion(obj, "propio")` | Propiedades `tbd_faccion` y `tbd_rim` (1 solo en unidades propias) que viajan en el FBX para el marcador de rim del prefab |
| `medir_legibilidad(obj)` | ΔL* de D-N57 contra la trinchera (#48403c) y el llano ocre (#845c28, confirmado por Lucía): 20 % más claro del interior y contorno, sin luz y con la paleta al 65 % |
| `vista_pixelada(obj, png, vista="rts")` | Imita el shader: 128 px ×3, luz arriba a la izquierda, paleta al 65 %, contorno de 1 px. Es una **aproximación**: manda la captura de Unity |
| `paleta_uniforme(obj, "50541c")` | Un solo color de la paleta en el atlas: para bloques hechos en Blender, antes de `valor_partido` |
| `paleta_por_piezas(obj, colores)` | Color de paleta por material de origen (piezas); devuelve las máscaras para `valor_partido(mascara=…)` |
| `vistas_para_hunyuan(obj, carpeta)` | front/left/back/right en RGBA desde un bloque hecho en Blender |
| `exportar_unity(obj, ruta)` | FBX: 1 u = 1 m, −Z adelante, Y arriba, transformaciones horneadas, normales por cara |

## Legibilidad D-N57 y reglas de Iris (`docs/arte/modelado-3d/encargo-modelos-hunyuan.md`)
1. **Valor partido horneado en el albedo**: `procesar()` llama a `valor_partido()` por defecto (`partido=False` lo quita).
2. **Beige de la paleta y contorno por script**: el claro sale siempre de la paleta (b4a080 o 94948c). El contorno de 1 px
   lo pone el shader de Unity o `vista_pixelada()`, **nunca se pinta en la textura**.
3. **Rim frío #404460 solo en unidades propias**: `procesar(..., faccion="propio")` para tropa y vehículos propios;
   `"civil"`, `"zombi"` o `"neutro"` para el resto. En Unity, un AssetPostprocessor lee `tbd_rim` y pone el marcador en el
   prefab (encargo para Marcos; **no verificado** que Unity reciba la propiedad del FBX).
4. **Medida**: `medir_legibilidad()` da una primera lectura en Blender y entra en el veredicto. La que manda es
   `cohesion_sheet.py --tipo modelo` sobre la captura de Unity (de día a mediodía; de noche, dentro del charco).
- Prueba (carretilla de Hunyuan, 6-nov): 4 colores, beige arriba; ΔL* 39 contra la trinchera y 42 contra el llano
  (el 20 % claro solo llega a 24,5 contra el llano; pasa por el contorno, como permite la fórmula de D-N57).
- **Decisiones de Lucía (6-nov)**: claro por defecto **b4a080** (94948c es la camisa de los zombis); llano = **#845c28**
  (ocre de A7); **ningún rojo en objetos**: se prohíbe la familia roja (340000, 4c1008, 642030, 7c181c, 8c5048, 982420)
  y el ámbar, y los rojos y naranjas de origen se reasignan por valor a oliva, ocre o gris.
- **Costados (Lucía, 09:31)**: vehículos y material militar en **50541c** (oliva de los laterales del tren, D-N42);
  objetos civiles en **605c48**. Prohibidos en costados: 48403c y 845c28 (suelos), 6c7874, 94948c y 404460 (grises de A7)
  y 60546c (costado de los zombis). `procesar(tipo="militar"|"civil")`; por defecto, militar si `faccion="propio"`.

## Hunyuan con 4 vistas (pregunta de Eugenio)
- **Sí se puede (verificado)**: el modelo **Hunyuan3D-2mv** acepta un diccionario `{"front", "left", "back", "right"}`
  (código de Tencent: `MVImageProcessorV2`, «view order: front, front clockwise 90, back, front clockwise 270»). Hunyuan 2.1
  no tiene modelo multivista publicado.
- **En el addon**: elegir el motor Hunyuan, poner la imagen principal (frente) y añadir **Back, Left y Right** con
  «Add View». La forma sale de las 4 vistas; la textura la pinta Hunyuan desde la imagen principal (código del addon).
- **Requisitos de las vistas**: el mismo objeto, la misma escala y la misma altura de cámara, de frente, sin perspectiva
  fuerte, fondo liso o transparente. La forma más fiable de conseguirlas: un **bloque en Blender** (cajas y cilindros con
  las proporciones reales) y `vistas_para_hunyuan()`, o una hoja de vistas de Pixelab.
- **(no verificado)** qué lado es «left»: según Tencent es el frente girado 90° en sentido horario visto desde arriba.
  Probar antes de un lote con una pieza asimétrica; si sale espejada, intercambiar left y right.

## PASO OBLIGATORIO en todos los modelos: detalle y textura desde las fotos reales (Eugenio, 6-nov)
Ningún modelo se entrega como bloque liso ni con un solo color. Para cada pieza:
1. **Fotos de Paula** (`C:\Dev\mr-mak-workspace\referencias-reales\<categoria>\` y su `manifest.json`): mirar todas
   antes de modelar y apuntar en la cabecera del script **qué detalle sale de qué foto** (y lo que es de un modelo
   parecido, como el casco del Piranha V para el Dragón).
2. **Detalle en la geometría**, lo que se ve en las fotos y aguanta el pixelado: escotillas, periscopios, visores,
   rejillas, focos, retrovisores, ganchos, cajas, redes. Piezas finas de menos de ~6 cm de grosor (antenas, cables)
   salen como trazos sueltos a tamaño de juego: engordarlas o quitarlas, y decirlo.
3. **Color por pieza** desde las fotos, llevado a la paleta: cada pieza con su material (`casco`, `goma`, `llanta`,
   `optica`, `foco`, `metal`, `carga`…) → `paleta_por_piezas(obj, COLORES)` → `valor_partido(obj, costado=…,
   alisar=0, mascara=mascaras["casco"])`. Las ruedas, de goma oscura (303424) y separadas del casco; sin rojos ni ámbar.
   Así se cumplen las reglas de Iris: albedo horneado, beige de la paleta, contorno por script y marcador de rim.
4. **Entrega con captura junto a la foto real**: foto, versión anterior y nueva en 3/4, a tamaño de juego y en lateral
   (ejemplo: `capturas/dragon_v3_v5_foto.png`).
Ejemplo completo: `scripts/bloques/dragon_v5.py` (2.432 tris, auditoría OK, 9 colores).
**Regla de píxel de Bruno (6-nov) para el LOD1**: (1) medir la escala de juego: ~0,17 m/px (el Dragón, 8 m, ocupa
~48 px; un VAMTAC de 6 m, ~36 px: `entregar.py --px`); (2) detalle mínimo de 2 px en su lado más corto (~0,35 m): con
menos de 1 px parpadea al girar y entre 1 y 2 px el contorno lo vuelve una línea negra; (3) al menos 2 px de superficie
entre dos detalles; (4) nada que salga de la silueta con menos de 3×3 px; (5) lo que no llega va fuera o como cambio de
valor plano, sin geometría ni contorno (escotillas, juntas, lamas). Los cañones se engordan a 2 px en el LOD1.
(6) torres y superestructuras en un solo bloque, con su propio claro para separarse del techo (Dragón: `VALOR_PIEZAS =
{"torre": ("88885c", "50541c")}` en el módulo del bloque, que `entregar.py` aplica tras el casco); visores como mancha
clara de 2×2 (bone d4d0cc). (7) Las ruedas deben asomar bajo el costado: si el flanco las tapa en la vista RTS, en el
LOD1 se sacan unos 10 cm (desviación consciente de la medida). Las líneas oscuras que quedan dentro de la torre no son
contorno interior sino sus caras en sombra: con umbral 0,25, 0,6 o solo silueta la vista sale igual. El contorno
definitivo lo pone el shader de Unity (Marcos), no el render de comprobación.

**Atajo (nube, sin el PC de Eugenio)**: `scripts/bloques/taller.py` (caja, cilindro, perfil extruido con bandas planas,
costuras, LOD con `fino`) para escribir el bloque, y `scripts/entregar.py <modulo> <salida> --fotos 34.jpg:caja
lateral.jpg:caja --claro … --python_bpy <python con bpy>`, que genera el LOD0 y el LOD1, sus informes y FBX, y la captura
junto a la foto real. Ejemplos: `locomotora_r1.py` (R1, con `--claro b8b8b8 --permitir e08c30`) y `vagon_tropa_r2.py`.
5. **Dos lecturas (Lucía, 6-nov, como D-N61 de la horda)**: **LOD0** de cerca con todo el detalle y **LOD1** a tamaño
   de juego solo con las piezas de 0,4 m o más (unos 3 px): `construir(lod=1)`, que deja fuera las piezas finas.
   El contorno de 1 px va solo en la silueta y en los grandes cambios de volumen (`vista_pixelada(...,
   umbral_contorno=0.15)`; en Unity es cosa del shader, **no verificado** allí). El detalle interior se dibuja con valor,
   nunca con líneas negras. Dragón: LOD0 2.432 tris y LOD1 1.844, los dos OK.
6. **Chapa desde las fotos**: juntas de los paneles medidas en las fotos (`COSTURAS` en el script del bloque), cortadas
   en el casco como tiras de 6 cm (`Taller.costuras`) y oscurecidas sobre su panel entre 5 y 12 de L* con
   `costuras_por_valor(obj, mascara)` en la misma familia de tono (±25° de matiz; Lucía: el oliva no puede derivar al óxido). Dragón: b4a080→88885c y 50541c→303424 (13,4 de L*, la paleta no tiene un oliva más cerca; se admite con 2 de holgura). **Solo en el LOD0**: probado en el LOD1 a
   48 px con tiras de 6 y 20 cm, y salen motas sueltas. Por la misma razón, en el LOD1 tampoco van las placas planas del
   techo (menos de 10 cm de alto). Los remaches no se modelan porque miden menos de 1 px.
   Contorno interior del LOD1 a 0,25 (Lucía: que solo marque la base de la torre).
7. **Bloques**: `valor_partido(..., alisar=0, suavizar=0)`. La mayoría entre vecinas es para el ruido de Hunyuan; en un
   bloque cortado se come los trozos pequeños de techo y flanco. Los flancos se cortan en bandas horizontales para que
   sean planos: un n-gono alabeado sale con triángulos de distinto tono.

## Vehículos con pocas fotos: bloque con medidas reales (estándar de Lucía, 6-nov)
Cuando solo hay una o dos fotos, Hunyuan con una imagen inventa la parte que no ve y deja la chapa ondulada (manchas
de luz en el techo, v3 del Dragón). El método estándar es este:
1. **Ficha de medidas**: largo, ancho y alto de una fuente citada (Paula); el resto de cotas, estimadas de la foto y
   marcadas **SIN VERIFICAR** en la cabecera del script.
2. **Bloque en Blender por código** (`scripts/bloques/<pieza>_bloque.py`, función `construir()`): casco por perfil
   lateral extruido, **chapas planas**, torre angulosa con cajas, ruedas en cilindros de 14 lados, bisel de 0,05 m.
   Ejemplo: `dragon_bloque.py`, 2.404 triángulos y 8 × 3 × 3,26 m.
3. `limpiar(obj)` **antes de colorear** (el bisel deja n-gons y vértices duplicados) → `paleta_uniforme(obj, "50541c")`
   → `vistas_para_hunyuan(obj, carpeta)` → `valor_partido(obj, costado="50541c", alisar=0)`.
   **`alisar=0` en los bloques**: el alisado de normales deforma una malla de pocas caras y el techo sale oscuro.
4. Dos salidas que se comparan: **(a) el bloque tal cual** (ya está en presupuesto y con chapa limpia) y **(b) las 4
   vistas por Hunyuan3D-2mv** + `procesar()`. Se queda la que mejor lea en la vista de juego; Lucía decide.
- **Prueba del Dragón (6-nov, verificada)**: con 4 vistas lisas de un bloque, 2mv (FAST, 40.000 caras, 58 s) devuelve el
  mismo bloque peor: ruedas fundidas en bultos, chapa ondulada y 0,25 m más alto. `procesar(..., color_unico="50541c")`
  lo deja en 2.988 tris y OK, pero el **bloque tal cual lee mejor**. 2mv solo aporta si las vistas traen detalle
  (fotos o vistas pintadas), no con un bloque liso.
- **Orientación (verificada en esa prueba)**: la malla de 2mv sale girada 180° respecto al bloque (el cañón mira a +Y).
  Girarla antes de exportar. **(no verificado)** si left y right van espejados: el bloque era casi simétrico.

## Añadir detalle sin romper el presupuesto
- El pixelado de 1/3 se come todo lo que mida menos de ~3 px en pantalla: **el detalle va en la silueta y en bloques de
  color**, no en relieve fino. Un faro, una escalerilla o un parachoques sí; tornillos y remaches, no.
- Piezas añadidas: primitivas con pocos lados (cilindros de 8–12), unidas al objeto y con `limpiar()` después.
- Bisel de 1 segmento solo en aristas largas de la silueta (ayuda al contorno por normales del shader).
- Ruedas, ejes y torretas como objetos aparte si se van a mover, con el origen en su eje.
- Si la pieza va justa de triángulos: quitar caras que nunca se ven (bajos, interiores) antes de diezmar.

## Comprobaciones antes de dar una pieza por buena
1. `auditar()` sin ERROR (los AVISO se explican en el informe).
2. Silueta ≥ 0,985 (con 1 px de tolerancia) en las 4 vistas. Si no llega ni con 4.000 triángulos, la pieza es demasiado fina para el presupuesto:
   simplificar a mano (barras más gruesas, menos piezas) en vez de subir el límite.
3. Altura real dentro del ±15 % (tabla en `referencias/criterios.md`).
4. Vista pixelada: silueta legible y colores de la familia (Lucía decide).
5. Captura en Unity de día y de noche (regla 13).

## Referencias
- `referencias/criterios.md`: presupuestos, alturas reales y umbrales.
- `referencias/externos.md`: herramientas externas evaluadas (qué se instaló, qué no y por qué).
- `referencias/paleta_32.hex`: copia de `docs/arte/paleta_propuesta_32.hex`.

**Convención de ejes (medida por Marcos en Unity, 6-nov)**: en Blender se modela con delante = -Y y Z arriba. Con
`exportar_unity()` (FBX, axis_forward="-Z", axis_up="Y", bake_space_transform) el modelo sale en Unity mirando a **+Z**
(transform.forward) y con Y arriba. Vale para todos los bloques entregados (Dragón, R1, R2, VAMTAC y zombi). El "-Z" es
el parámetro del exportador, no la dirección del modelo en Unity.
