# La noche sigue fuera de pantalla

Nacho (Simulación), 7-nov-2026. Petición de Eugenio (08:25): el mundo es persistente; si el jugador sale al mapa o visita otra estación, el combate de Sagunto **no se para ni se reinicia**. Esto anula la deuda «la partida salta el mapa».

## Problema actual (rama `marcos/noche-prototipo`, f6956b0)

Todo el estado de la noche vive en `PrototypeBootstrap` (escena de la zona): `_ng` (NightGame), `_uw` (UnitsWorld), `_wallHp`, los vehículos (`_vehs`) y los zombis (entidades ECS de los mundos A y B). Al salir de la zona (`SceneManager.LoadScene("Union")`) la escena se destruye y todo se pierde. Al volver, `_nightInit` vale `false` y `InitNight()` crea una noche nueva desde las 22:00.

## Solución: una sola noche y dos maneras de simular el combate

1. **La noche sale de la escena.** Un contenedor estático (`NightRun.Session`, de Marcos) guarda el estado de la noche: `NightGame`, `UnitsWorld`, vida del muro, vehículos y zombis. Se crea una sola vez («Jugar») y solo se borra al terminar (victoria o derrota) o con «Volver a empezar» o «Menú principal».
2. **En pantalla (zona):** igual que ahora, pero `_ng` y `_uw` se leen de la sesión en vez de crearse. `InitNight()` crea la noche solo si no existe la sesión; si existe, **reanuda**.
3. **Fuera de pantalla (mapa u otra estación):** un `MonoBehaviour` con `DontDestroyOnLoad` (o el `Update` del mapa) llama a `NightSim.Step()` a 30 Hz con paso fijo. NightSim ya simula sin ventana la misma noche: los zombis van al edificio, se amontonan contra el muro y lo desgastan, los pelotones disparan (`UnitsWorld.Step`), hay contacto y llegan las oleadas. **Nuevo (PR #24):** `new NightSim(night, units, sp, wallHp)` se engancha al `NightGame` y al `UnitsWorld` que ya existen, sin copiarlos ni reiniciarlos.
4. **Al salir de la zona:** se exportan a la sesión los zombis vivos de ECS (mundo A) con `sim.ImportZombie(x, y)` en mm, y la vida del muro (`(int)_wallHp`). Los pelotones no hay que pasarlos, porque ya están en `UnitsWorld`. Los vehículos de tierra se pasan con su posición y su reserva a `sim.Vehicles`; el NH90 solo si está en vuelo.
5. **Al volver a la zona:** con `sim.ExportZombies(lista)` se crean los zombis en ECS (en A y en B a la vez, como ahora) en su posición; `_wallHp = sim.WallHp`; los vehículos, con su posición y su reserva. Las piezas de la puerta (`_gateWalled`) se vuelven a cerrar con muro porque la escena es nueva.
6. **Si la noche termina fuera de pantalla** (`sim.Night.State` pasa a Victoria o Derrota), aviso en el mapa («Sagunto ha caído» o «Sagunto aguanta»), con el parte de la noche. Cuando el jugador entre, verá el resultado.

## Reglas fuera de pantalla

- Los pelotones conservan la última orden (mantener, emplazado, ataque-movimiento) y disparan solos.
- Recarga automática de los pelotones al 30 %, la misma regla que la zona aplica cada 15 ticks (`AutoPlatoonResupply`, activa por defecto en el modo enganchado).
- Los vehículos siguen con el fuego en el estado en que estaban (fuego libre desde el principio, decisión de Mak). Fuera de pantalla **no recargan solos**, porque nadie pulsa T (`AutoVehicleResupply = false`). Si Mak prefiere que recarguen, es una línea.
- El bot (`-nightbot`) sigue funcionando igual, en pantalla y fuera.

## Lo que se pierde (y es aceptable para el MVP)

- Fuera de pantalla el combate es el **abstracto** de NightSim. No es el detallado de ECS: la pila contra el muro es un rectángulo, no hay flujo por las calles ni cuerpos. Mirar o no mirar puede cambiar un poco el resultado. Es la misma diferencia que ya medimos entre la zona y NightSim (contrato en `Docs/noche-zona-contrato.md`).
- NightSim no conoce los muros que el jugador haya construido aparte del recinto. Se toma la vida del muro como un solo número.

## Determinismo y hash

El hash de `NightGame` y `UnitsWorld` no cambia: es el mismo objeto antes y después. NightSim enganchado es entero y determinista (prueba: dos salidas iguales dan el mismo hash). La zona sigue fuera del hash, como hasta ahora.

## Varios frentes (después del MVP)

`NightRun.Session` pasa a ser `Dictionary<nodo, NightSim>`: cada estación atacada tiene su NightSim; solo la que está en pantalla usa la zona. Encaja con el mundo persistente de la D-N91 y la D-N96.

## Pruebas (sección [29])

- Al salir de la zona la noche sigue donde estaba: mismo reloj, zombis, muro y almacén.
- Fuera de pantalla el reloj avanza, llegan oleadas y los pelotones disparan.
- Al volver salen los zombis vivos con su posición.
- La noche puede acabar fuera de pantalla.
- Es determinista.
- Engancharse sin zombis al principio da la misma noche que NightSim normal.

## Reparto

- **Nacho (hecho, PR #24):** `NightSim(night, units, sp, wallHp)`, `ImportZombie`, `ExportZombies`, `ZombiesAlive`, `AutoPlatoonResupply`, vehículos en NightSim (`NightVehicle`, `AddDefaultVehicles`), pruebas.
- **Marcos:** `NightRun.Session` estática, el reloj persistente (`DontDestroyOnLoad`, paso fijo de 30 Hz), reanudar en `InitNight`, exportar e importar ECS y vehículos al cambiar de escena, aviso en el mapa.
