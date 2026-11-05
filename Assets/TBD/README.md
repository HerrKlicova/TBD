# Prototipo de zona (hito 1 del GDD) — sin arte

Escrito el 5-nov-2026 **sin poder compilarlo ni probarlo** (Unity aún no tenía licencia activa). Al abrirlo por primera vez, lo
esperable es tener que corregir algunos errores de compilación; la estructura y las reglas están pensadas para eso.

## Qué hace
- **Fuerte con muros con grosor y altura, una puerta, una vía con su tren y un edificio con tejado.** Los muros son **sólidos con geometría
  continua** (segmentos con grosor y cajas), no celdas. La colisión es **cápsula contra sólido**.
- **10.000 zombis en DOTS con movimiento continuo** (GDD §9c, v1.10): posición, velocidad y orientación como `float3`/radianes; el campo de
  flujo solo da una **dirección deseada interpolada bilinealmente**; los zombis giran suavemente y desliza a lo largo de los muros. Nada de
  ir de centro de celda a centro de celda.
- **Rejilla interna fina de 0,5 m** que solo guarda datos agregados: coste, dirección deseada y altura de pila. La **altura de pila se
  interpola** entre celdas y la altura del suelo se calcula con los sólidos, no con la rejilla.
- **Amontonamiento (GDD §5.7):** un zombi que no avanza contra un muro alto se «asienta» y suma 1 a la pila de su celda; los siguientes suben
  por la pila interpolada. Cuando la pila alcanza la altura del muro menos el escalón, la horda se derrama por encima.
- **Caídas:** los zombis del tejado caen con gravedad al salir del borde (tecla 1).
- **Construcción libre (tecla B):** clics para trazar una polilínea; cada tramo es una orden `BuildWall` con posición y orientación libres.
  El sólido se rasteriza en la rejilla **solo en la región afectada** y se fuerza el recálculo del flujo.
- **Cámara libre:** rotación (Q/E o botón derecho), inclinación (R/F), zoom continuo con la rueda y snapping a la rejilla de píxeles
  (solo visual: la simulación no sabe nada de la cámara).
- **Render en capas (dirección visual v1.4):** (1) **mundo** dibujado a 1/N de la pantalla (N = 2, 3 o 4; por defecto 3) con filtro de punto,
  ampliado con vecino más cercano, contorno de 1 px y acercamiento a la paleta de 32 colores con mezcla ajustable; (2) **efectos de luz** a
  resolución completa (trazadoras, fogonazos y chispas como partículas aditivas HDR en la capa VFX_HD); (3) **postproceso** a resolución
  completa: bloom con umbral 1 (solo brillan los efectos HDR), viñeta y gradación de día, atardecer y noche; (4) **interfaz** nítida (OnGUI).
  Pendiente: contorno por profundidad y normales (hoy por diferencia de luminosidad), luz toon, corrección de subpíxel de la cámara,
  prueba de profundidad de los efectos y medir el coste en ms de cada pasada.

## Reglas de determinismo aplicadas (GDD §17.3)
1. **Tick fijo a 30 Hz** (`SimDriver` en `PrototypeBootstrap.Update`); nada usa `Time.deltaTime` en la simulación.
2. **Todo es una orden** (`SimCommand` con número de tick y retardo de 2 ticks); la simulación solo lee la cola.
3. **Burst con `FloatMode.Deterministic`** en los jobs de simulación (requiere Burst ≥ 1.8.25; fijado en el manifest).
4. **Aleatoriedad** con `Unity.Mathematics.Random` y semillas derivadas del tick (nunca `UnityEngine.Random`).
5. **Orden:** `SimId` propio, jobs de un solo hilo, doble búfer en la pila (se lee la del tick anterior y solo se escriben
   deltas enteros) y orden de sistemas fijo y explícito.
6. **Sin física de Unity** en la jugabilidad: bloqueo con la misma rejilla del campo de flujo.
7. **Hash del estado por tick** (suma de hashes por elemento, no depende del orden) y **prueba de desincronización**: un segundo
   mundo sin dibujo recibe las mismas órdenes y su hash se compara con el del mundo visible en cada tick (F3 la activa o desactiva).
8. La serialización completa del estado (guardado) **no está hecha** todavía.
9. Velocidad y pausa **no son órdenes todavía** (la pausa P solo detiene el bucle local).

## Controles
`1` zombis en el tejado (500) · `2` abrir/cerrar la puerta · `3` arrancar/parar el tren · `4` 3.000 zombis más por el oeste · `B` muros libres ·
`WASD`/flechas mover · `Q`/`E` girar · `R`/`F` inclinar · rueda zoom · botón derecho girar e inclinar · `F3` prueba de desincronización · `P` pausa ·
`F9` captura · `F10` empieza/para la grabación de fotogramas · `5` descarga de trazadoras (solo visual) · `7`/`8`/`9` día/atardecer/noche ·
`[` `]` factor de pixelado (2-4) · `,` `.` mezcla con la paleta.

## Capturas y GIF (solo hitos importantes, petición de Eugenio)
`F9` guarda un PNG de lo que ve el jugador (con efectos, postproceso e interfaz, a mitad de tamaño) y `F10` graba fotogramas a 15 por segundo
(hasta 30 s) en `<proyecto>/Captures/`, que Git ignora. Para montar el GIF:
`python C:\Dev\mr-mak-workspace\scripts\capturas\hacer_gif.py <carpeta gif_...> <salida.gif>`.
Después se copia a la tarjeta de mr.mak y se manda a Eugenio.

## Estructura
- `Scripts/Sim/SimTypes.cs` constantes, componentes, rejillas, sólidos (muros y edificios) y geometría del nivel.
- `Scripts/Sim/SimSystems.cs` sistemas (órdenes, campo de flujo, zombis, pila, tren, hash, reloj).
- `Scripts/Sim/SimWorldSetup.cs` entidad raíz y registro de sistemas en orden fijo.
- `Scripts/View/RenderSystems.cs` dibujo con Entities Graphics (solo en el mundo visible).
- `Scripts/View/PrototypeBootstrap.cs` arranque, bucle a tick fijo, entrada, cámara, decorado y HUD.
- `Scripts/View/FxHd.cs` efectos de luz HDR a resolución completa (solo presentación).
- `Shaders/PaletteQuantize.shader` ampliación, contorno y paleta del mundo; `Shaders/FxAdditive.shader` partículas aditivas HDR.

## Pendiente de verificar al abrirlo
- Que compile (nombres exactos de API de Entities / Entities Graphics 1.5.0 escritos de memoria y de la documentación).
- Que Burst sea ≥ 1.8.25 y que `FloatMode.Deterministic` funcione.
- Rendimiento con 10.000 zombis (medir el coste del modo determinista, GDD §17.4) y el coste de la prueba de desincronización.
- Que el shader `Hidden/TBD/PaletteQuantize` se incluya al hacer una compilación (hoy se busca con `Shader.Find`; basta en el Editor).
