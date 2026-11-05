# Prototipo de zona (hito 1 del GDD) — sin arte

Escrito el 5-nov-2026 **sin poder compilarlo ni probarlo** (Unity aún no tenía licencia activa). Al abrirlo por primera vez, lo
esperable es tener que corregir algunos errores de compilación; la estructura y las reglas están pensadas para eso.

## Qué hace
- **Fuerte con muro y puerta** (celdas de 1 m), **vía con su tren** (locomotora y 3 vagones) y un **edificio con tejado**.
- **10.000 zombis en DOTS** que van hacia el tren con un **campo de flujo** (relajación en barridos, determinista).
- **Amontonamiento (GDD §5.7):** un zombi que no puede subir el desnivel se «asienta» y suma 1 a la pila de su celda; los siguientes
  caminan sobre la pila. Cuando la pila alcanza la altura del muro, la horda se derrama por encima.
- **Caídas:** los zombis del tejado caen con gravedad simple al salir del borde (se pueden lanzar con la tecla 1).
- **Pixelado:** la cámara dibuja a 480×270 (filtro de punto) y se reduce a la **paleta fija de 32 colores** con un shader al ampliar.
  Pendiente: contornos por profundidad y normales, luz toon, snapping de cámara (GDD §11).

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
`1` zombis en el tejado (500) · `2` abrir/cerrar la puerta · `3` arrancar/parar el tren · `4` 3.000 zombis más por el oeste ·
`WASD`/flechas mover la cámara · rueda del ratón zoom · `F3` prueba de desincronización · `P` pausa.

## Estructura
- `Scripts/Sim/SimTypes.cs` constantes, componentes, rejillas y geometría del nivel.
- `Scripts/Sim/SimSystems.cs` sistemas (órdenes, campo de flujo, zombis, pila, tren, hash, reloj).
- `Scripts/Sim/SimWorldSetup.cs` entidad raíz y registro de sistemas en orden fijo.
- `Scripts/View/RenderSystems.cs` dibujo con Entities Graphics (solo en el mundo visible).
- `Scripts/View/PrototypeBootstrap.cs` arranque, bucle a tick fijo, entrada, cámara, decorado y HUD.
- `Shaders/PaletteQuantize.shader` reducción a la paleta de 32 colores.

## Pendiente de verificar al abrirlo
- Que compile (nombres exactos de API de Entities / Entities Graphics 1.5.0 escritos de memoria y de la documentación).
- Que Burst sea ≥ 1.8.25 y que `FloatMode.Deterministic` funcione.
- Rendimiento con 10.000 zombis (medir el coste del modo determinista, GDD §17.4) y el coste de la prueba de desincronización.
- Que el shader `Hidden/TBD/PaletteQuantize` se incluya al hacer una compilación (hoy se busca con `Shader.Find`; basta en el Editor).
