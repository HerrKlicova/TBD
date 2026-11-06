# Guardado y carga deterministas (GDD §14 y §17.3, regla 8)

Código: `Assets/TBD/Scripts/Net/Guardado.cs` (`NetSave`, `SaveWriter`, `SaveReader`) y un `SaveState`/`LoadState` en cada clase con estado.
Pruebas: `Dotnet/TBD.Sim.Tests/GuardadoTests.cs`, sección [28].

## Qué se guarda y qué no

El guardado es la serialización completa del **estado** de `NetWorld`. La configuración (mapa, CSV de balance) no se guarda: se reconstruye con el
escenario (`NetScenario.Mvp()`), y el archivo lleva dos huellas para saber si ha cambiado:

| Huella | Si no coincide |
|---|---|
| Topología (nodos, tramos, puntos de emboscada) | Error duro (`SaveError.TopologyMismatch`): la partida no se puede cargar. |
| Parámetros de balance | Se carga con el balance nuevo y `SaveInfo.BalanceChanged = true` (Hugo puede reajustar entre versiones). |

Tras cargar se recalcula `NetWorld.Hash()` y se compara con el de la cabecera (`SaveError.HashMismatch`).

## Formato

`magic "TBDS"` · `FormatVersion` (contenedor) · topología · parámetros · tick · hash · secciones (`id`, `version`, `bytes`) · suma FNV-1a de 64 bits.
Todo en enteros de longitud variable con zigzag; sin coma flotante, sin diccionarios, sin orden de recorrido que dependa de hashes.
Dos guardados del mismo estado son idénticos byte a byte, y guardar lo cargado da el mismo archivo.

Secciones: `NET` (la simulación de red). La zona (Unity/DOTS) la serializa Marcos en una sección extra `ZON` (`NetSave.Save(w, extra)`).
Hasta que exista, **guardar con `ActiveZoneNode >= 0` no incluye el estado de la zona** y no debe ofrecerse en la interfaz.

Si el archivo está truncado, lleva basura o tiene un campo fuera de rango, `Load` lanza `SaveException` con un `SaveError`. La suma de comprobación y la
cabecera se validan antes de tocar el mundo (`Peek`), pero un fallo a mitad de `LoadState` deja el mundo a medias: **hay que descartarlo y crear otro**
(`LoadMvp` ya lo hace).

## Reglas de versión (OBLIGATORIO al tocar el estado)

1. Si cambias lo que escribe algún `SaveState` (campos nuevos, orden, tipos): sube `NetSave.NetVersion` y registra en `NetSave.NetMigrations[v]`
   la migración «de v a v+1» sobre los bytes de la sección.
2. Si ya no quieres leer guardados viejos, sube `MinNetVersion`; un guardado más viejo da `SaveError.TooOld`.
3. Un guardado de una versión más nueva que el juego da `SaveError.TooNew` («actualiza el juego»).
4. Campo nuevo en una clase con estado: añádelo a su `SaveState`/`LoadState`. La prueba de reflexión (compara TODOS los campos, también privados)
   falla si olvidas uno; si un campo es de caché y se reconstruye, añádelo al conjunto de excluidos de la prueba con una nota.

## Medidas (Release, este PC de la nube)

Partida del MVP con 20 días acumulados: **23,9 KB**, guardar en **0,16 ms**, cargar en **0,15 ms** (objetivos de la prueba: < 200 KB, < 100 ms y < 200 ms).
Comprobado además: 2 días más (72.000 ticks) desde un guardado dan el mismo hash que el original y que dos cargas independientes;
304 recortes distintos de un guardado dan siempre error; un cambio de balance carga con aviso.

## Cuándo guardar (GDD §14, para Marcos)

Inicio de cada día de juego y antes de un ataque grande anunciado, con rotación de ranuras y guardado manual: es política de la interfaz,
la API solo es `NetSave.Save(world)` / `NetSave.Load(bytes, world)` (o `LoadMvp`).
