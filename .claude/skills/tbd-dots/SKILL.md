---
name: tbd-dots
description: Reglas de DOTS para el proyecto TBD (Unity 6.3 LTS, Entities 1.5.0, Burst 1.8.25 o superior) y de la simulación determinista. Úsala al escribir o cambiar sistemas, jobs, componentes, buffers o la presentación de las hordas, y antes de dar por buena una simulación.
---

# TBD · DOTS y simulación determinista

Escrita el 5-nov-2026 a partir de la documentación oficial y de lo aprendido al probar la lógica fuera de Unity. Lo marcado **(verificado)**
se comprobó en la documentación de Entities, Entities Graphics, URP o Burst; lo marcado **(no verificado)** es de memoria. Si algo choca
con lo que ves en el Editor, manda el Editor: corrige esta skill.

## Versiones fijadas
- Unity **6.3 LTS (6000.3.x)**, Entities **1.5.0**, Entities Graphics **1.5.0**, URP 17.3.0, Burst **≥ 1.8.25** (fijado en el manifest).
- **No subir a Unity 6.4 o posterior** sin una decisión nueva: desde la 6.4 Entities es un paquete del editor y la 6.5 elimina
  `Entities.ForEach`, `Job.WithCode` y los Aspects; la 6.6 deja obsoletos los componentes gestionados y `PlaybackPolicy` (guía de actualización de Unity).
- **Prohibido en este proyecto:** `Entities.ForEach`, `Job.WithCode`, Aspects, componentes gestionados, `UnityEngine.Random`, `Time.deltaTime` en la simulación,
  física de Unity en la jugabilidad.

## Arquitectura (una idea: la lógica pura se prueba fuera de Unity)
- `Assets/TBD/Scripts/Sim/SimTypes.cs` y `SimCore.cs` son **lógica pura** (sin ECS) y se compilan también con dotnet en `Dotnet/TBD.Sim.Tests`
  con shims mínimos. **Cualquier cambio en esos dos archivos exige ejecutar** `dotnet run -c Release --project Dotnet/TBD.Sim.Tests` antes del commit.
- `SimSystems.cs` solo trae datos de los buffers y llama a esa lógica (`ZombieLogic.Step`, `FlowJob`, `PileApplyJob`, `HashMath`). Si añades lógica, ponla
  en `SimCore.cs` y escribe su prueba; los sistemas deben quedarse finos.
- Los sistemas de simulación llevan `[DisableAutoCreation]` y se añaden a mano, en orden fijo, a `SimRootGroup` (`EnableSystemSorting = false`),
  que se actualiza con `group.Update()` a tick fijo. Así el orden es idéntico en el mundo visible y en el mundo B de la prueba de desincronización.
- La presentación (`RenderAttachSystem`, `ZombieTransformSystem`) vive solo en el mundo por defecto y **nunca** escribe estado de simulación.

## Reglas de determinismo (GDD §17.3)
1. Tick fijo (30 Hz). Nada de `Time.deltaTime` ni de la cámara en la simulación; el snapping de cámara es solo visual.
2. Todo lo que hace el jugador es un `SimCommand` con número de tick (retardo de entrada de 2 ticks) por la única cola. La orden lleva sus datos (`P0`, `P1`, `Arg`).
3. Jobs de simulación con `[BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]` **(verificado: existen en Burst 1.8; solo 64 bits,
   solo código Burst; el patrón de bits de los NaN puede variar)**. Economía y capa S0 con enteros o punto fijo.
4. Aleatoriedad con `Unity.Mathematics.Random` y semilla derivada del tick y del sistema. Semilla distinta de 0 (`| 1u`).
5. **Doble búfer:** cada agente lee el estado del tick anterior y solo escribe **deltas enteros** (sumas conmutativas). Resultado independiente del orden de proceso
   (está probado: mismo hash procesando en orden inverso o aleatorio). Si dos agentes dependen del resultado del otro dentro del mismo tick, el diseño es incorrecto.
6. IDs estables propios (`SimId`). Los hashes de estado son sumas de hashes por elemento (no dependen del orden de recorrido).
7. Hash del estado por tick y prueba de desincronización con un segundo mundo y las mismas órdenes (`F3` en el prototipo).
8. Guardado = serialización completa del estado (pendiente).

## API de Entities que se usa (comprobada en la documentación 1.5)
- `ComponentSystemGroup.AddSystemToUpdateList(SystemHandle)` y `(ComponentSystemBase)`, `EnableSystemSorting`, `SortSystems()` **(verificado)**. `Update()` se hereda de `SystemBase` **(verificado que es heredado; accesibilidad no mostrada)**.
- `World(string name, WorldFlags flags)`, `CreateSystemManaged<T>()`, `CreateSystem<T>()` (devuelve el manejador), `GetExistingSystemManaged<T>()`, `Dispose()`, `IsCreated` **(verificado los nombres; los tipos de retorno no se muestran)**.
- `DynamicBuffer<T>`: `AsNativeArray()`, `Reinterpret<U>()`, `Resize(int, NativeArrayOptions)`, `ResizeUninitialized(int)`, `RemoveAt`, `Add` **(verificado)**.
- `RenderMeshUtility.AddComponents(Entity, EntityManager, in RenderMeshDescription, RenderMeshArray, MaterialMeshInfo = default)` **(verificado)**.
  `RenderMeshDescription(ShadowCastingMode, bool receiveShadows = false, ...)` y `MaterialMeshInfo.FromRenderMeshArrayIndices(int, int, ushort = 0)` **(verificado)**.
  `RenderMeshArray(Material[], Mesh[], MaterialMeshIndex[] = null)` **(verificado)**.
- `PostTransformMatrix`, `LocalTransform`, `TransformSystemGroup` están en `Unity.Transforms` **(verificado)**.
- **No hay `URPMaterialProperty*` documentados** en Entities Graphics 1.5: define tu propio componente con `[MaterialProperty("_BaseColor")]` y un `float4 Value` **(verificado el atributo: `MaterialPropertyAttribute(string, short overrideSizeGPU = -1)`)**; así está hecho (`ZombieBaseColor`).
- `IJob` está en **`Unity.Jobs`** (hace falta `using Unity.Jobs;` también para `.Run()`), `[ReadOnly]` en `Unity.Collections`, `IJobEntity` en `Unity.Entities`.
- En un `ISystem` con Burst, un `IJobEntity` se lanza con `new Job { ... }.Run()` o `.ScheduleParallel()` sin pasar el estado **(no verificado en la documentación; así lo muestran los ejemplos de Unity)**.

## Trampas que ya han mordido (o habrían mordido)
- **Un `DynamicBuffer` queda invalidado por cualquier cambio estructural** de su entidad (añadir un buffer, crear entidades). Añade todos los buffers primero y obtén las referencias después; vuelve a pedir el buffer tras crear entidades.
- **Oscilación por doble búfer:** si una decisión depende de la altura de toda la pila, todos los zombis apilados cambian de estado a la vez y la pila oscila entre llena y vacía cada tick. Decide con una propiedad **propia** del agente (aquí, su altura dentro de la pila).
- **No decidas una caída por la altura de una masa de agentes** (la pila): provoca parpadeo entre caer y andar. Se decide por el desnivel estático del terreno.
- La rejilla solo guarda datos agregados; los agentes y los muros son continuos (GDD §9c). Consulta el suelo con los sólidos (índice espacial de cubetas), no con la rejilla.
- Los sólidos se consultan con el índice espacial (`SolidIndex`); al añadir o quitar un sólido hay que reconstruirlo (`SolidIndex.Rebuild`).
- `Allocator.Temp` solo en el hilo principal. **No lo pases nunca a un job, aunque lo lances con `.Run()` en el mismo `OnUpdate`**: falla en ejecución («Temp memory containers cannot be used when scheduling a job»; visto en Unity 6000.3, commit `87f3a5f`). Usa `TempJob` y libéralo después del job.

## Skills de terceros en este repositorio
- `save-systems`, `procedural-gen` y `performance-optimization` vienen de awesome-gamedev-agent-skills (Apache-2.0, sin su cargador automático). Cada una lleva arriba sus «Notas para TBD (DOTS)». Si chocan con esta skill, manda esta. Lo principal: ni `System.Random` ni pooling de MonoBehaviour en la simulación.
- Plugin oficial de Unity (`unity@unity-agent-plugin` 0.1.9-beta, instalado en este proyecto): no trae nada de DOTS. Sus skills de URP, Shader Graph y UI sí sirven.

## Antes de dar por buena una tarea de simulación
1. `dotnet run -c Release --project Dotnet/TBD.Sim.Tests` en verde (hoy 49 comprobaciones).
2. En Unity: sin errores de compilación, prueba de desincronización correcta (`F3`), 10.000 zombis a 60 fps, Burst ≥ 1.8.25 en el Package Manager.
3. Registrar el cambio de diseño en mr.mak (`CAMBIOS.md` con `C-xxx`, y `DECISIONES.md` si hay decisión).
