# Qué tiene que llamar la zona de Unity de lo que hace NightSim (7-nov, 02:00)

Para Marcos. Fuente: `Assets/TBD/Scripts/Units/NocheSim.cs` (`NightSim.Step`) y `Noche.cs` (`NightGame`). `NightSim` es la zona **abstracta** de las pruebas: lo que hace en un tick es lo que la zona real debe hacer con zombis reales.
Todo en mm, ticks a 30 Hz, enteros. La zona ya hace casi todo; lo que falla (Sara, Noche15) es la **conexión muro ↔ pila ↔ daño**.

## Orden de un tick (igual que `NightSim.Step`)
1. `Night.WallPermil = vida del muro en ‰` (en el juego, `Obras.WallPermil`; lo copia al parte al amanecer). Si el muro no baja, el parte siempre dirá 100 %.
2. `Night.Step(spawns)` → rellena `spawns` con los zombis que entran este tick (cuotas exactas por oleada y entrada). La zona crea un zombi por cada `SpawnPoint`.
3. **Pila y desgaste del muro** (lo que NO está conectado en la zona real):
   - *Pila* = zombis **vivos, fuera del recinto, a ≤ 1,5 m del muro** (`PileRadiusMm`), contando mientras el muro esté en pie. NightSim los cuenta uno por uno cada tick (`pile`, pico `PeakPile`).
   - *Desgaste*: cada zombi de la pila quita `WallDmgPerZombieS` (= 1) punto de muro **por segundo** → `resto += pila × 1; daño = resto / 30; resto %= 30; WallHp -= daño`. Resto exacto, sin flotantes.
   - `WallHp` de partida **1.200.000** (10.000 por u de muro de la partida; Hugo, v3 aprobada por Mak, `nightsim_params.json`; NightSim lo lee de ese JSON). Con vida 0 el muro **deja de bloquear** (los zombis pasan y van al edificio). En el juego real, la vida es la de las piezas de `Obras`, y el daño debe repartirse entre las piezas que tocan los zombis; la suma normalizada es lo que va a `Night.WallPermil`.
   - Si la zona calcula la pila (`CellPile`, la que ya usa `Vehicle.PileAhead`), el dato está: falta **sumar, por tick, los zombis de las celdas pegadas a un muro y aplicarlos como daño** a ese muro. Sara midió pila = 0 con 6.845 vivos: o no se rellena `CellPile` para los muros de Obras, o no se lee.
4. **Atacantes del edificio y del tren**: `Night.CountAttackers(zombisVivos, out aEdificio, out aTren)` y luego `Night.Attack(aEdificio, aTren)`. `zombisVivos` es una lista de `Target {Id, X, Y}` (mm). Un zombi a ≤ 1,5 m del rectángulo del edificio (98–114 × 75–83 m) o del tren (74–154 × 62,9–66,1 m) cuenta; 20 de vida por zombi y segundo (edificio 60.000, tren 120.000). Si una llega a 0, el siguiente `Night.Step` es **derrota**.
5. **Fuego**: `Units.Step(zombisVivos, res)`. Para cada `res.Hits[i]`: matar al zombi con `TargetId`, `Units.NoteKill(h.Platoon)`. `res.AmmoSpent` es lo gastado. (Los disparos de vehículos y helicóptero son locales: `StepWeapons`, devuelven sus propios `TargetHit`.)
6. **Contacto con los soldados**: `Units.ZombieContact(zombisVivos, 900, 2, contacto)`; `contacto.Hurt` trae, **por pelotón**, contactos y vida quitada de este tick (para el estado «puesto bajo ataque» del mapa).
7. **Reabastecimiento**: la dotación de cada pelotón es pequeña (un pelotón lleva ~1,5 u). `Units.Resupply(pelotón)` (o el bot: a cada pelotón con < 30 % se le repone del almacén).

## Lo que importa para ganar (medido en NightSim, semilla 3, fuerza por defecto, sin vehículos)
`TBD_NIGHT_REPORT=1 dotnet run -c Release --no-build` en `Dotnet/TBD.Sim.Tests` lo repite.

| Dificultad | Recargas permitidas en toda la noche | Resultado |
|---|---|---|
| normal (850) | sin límite (el bot hizo 38) | **victoria** 05:59, 2 estrellas, muro 72 % |
| normal | 8 | derrota 04:30 |
| normal | 5 | derrota 04:11 |
| normal | 3 | derrota 03:49 |
| fácil (725) | sin límite (36) / 3 | victoria 3 estrellas, muro 82 % / derrota 04:15 |
| difícil (1140, 18.240 zombis) | sin límite (42) / 3 | victoria 2 estrellas, muro 25 % / derrota 03:11 |

(Datos de la v3 de Hugo: muro 1.200.000 y difícil a 1140. La D-N127 sube la difícil a 1280: esa fila no está remedida.)

Lectura: **la noche se gana reabasteciendo unas 36–42 veces**, no con 3. Un jugador de prueba que recarga 3 veces pierde también en NightSim (a las 03:49 en normal; en la zona real a las 04:45 porque el muro no se gasta, ver arriba). Así que hay **dos** cosas distintas:
- Si el jugador de Noche15 solo hace 3 recargas, perder es **lo esperado**; para la prueba debe seguir la regla del bot (reponer al bajar del 30 %, mirando cada ~150 ticks).
- Con el muro desconectado, la derrota llega por el tren, no por el muro: es un fallo de conexión, no de calibración. Hasta conectarlo, la calibración (tren 120.000, O1/O2, muro 1.200.000) **no se puede juzgar** en la zona real.
- Si diseño cree que 38 recargas a mano son demasiadas, la palanca no es el muro: es la dotación por pelotón o un reabastecimiento automático dentro del recinto. Decisión de Hugo y Mak; yo puedo medir cualquier variante en NightSim.

## Hipótesis sin verificar (no la he visto en la zona)
NightSim cierra el recinto (70–156 × 44–85 m). Si en la zona real la vía entra y sale por los extremos del recinto (y=64,5, x=70 y x=156) y esos huecos no tienen muro mientras no hay tren parado, los zombis pueden llegar al tren **sin tocar el muro**: llegan por el pasillo de la vía. Comprobar con una captura de la posición de los zombis que atacan el tren cuando el muro está al 100 %.

## Por qué la pila sale 0 en la zona real (Noche18, 7-nov 02:40)

Causa más probable (leída en el código de la zona, **no medida en Unity**): la puerta de vía (`PieceKind.PuertaVia`, `IsGate`) se dibuja como sprite y **no lleva sólido**
(`SyncWorks`: «la puerta de vía va abierta al tren»), y el planificador de obras deja siempre «la vía libre» (hueco de 3,2 m, `via_hueco_mm`). Los muros son un anillo
con un hueco justo en la y del andén, que es por donde apunta toda la horda (`StationY` = `TrackY` = 64,5 m). El flujo de la zona rodea el muro hasta el hueco y entra en fila:
nadie se amontona contra un muro (pila 0), el muro no se desgasta (1000 ‰) y, sin pelotones que cubran el pasillo, la horda llega al tren (derrota a las 05:56).
NightSim con el recinto cerrado nunca tuvo ese hueco: la horda se amontona (pila 140 en la oleada 1 con 4.500 ticks) y desgasta el muro.

`NightSimParams.GateGapMm` (por defecto 0 = cerrado, lo medido hasta ahora; `nightsim_params.json` puede ponerlo) modela el hueco: la horda rodea hasta `FortX0 − 1 m` y cruza solo por
|y − StationY| < GateGapMm/2. Con 3200: pila 0 en las oleadas 1–4 y 5–8 en las dos últimas, muro 99 % al amanecer (misma firma que la zona). Informe: `TBD_WAVE_REPORT=1`.

Arreglo para la zona: la pila y el desgaste no existen mientras haya hueco. Hay que dar sólido al hueco cuando la puerta está cerrada (en la noche el tren está parado, así que cerrada
mientras dure la noche): `Push(CmdType.BuildWall, 2100, (X0,Y0), (X1,Y1))` de la pieza `IsGate` construida, con la misma altura que el HESCO. Con el anillo cerrado, la pila vuelve a contar
(150–250 en las oleadas 1 y 2 según NightSim). Segunda diferencia: los sacos miden 1,2 m y un zombi sube 0,6 m (`StepUp`) sobre cuerpos de 0,35 m, así que a la tercera capa de la pila
pasa por encima; NightSim no deja pasar hasta que el muro cae. HESCO (2,1 m) necesita 5 capas.
