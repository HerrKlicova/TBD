# Unidades de la noche del 7-nov (pelotones, vehículos, helicóptero)

Código: `Assets/TBD/Scripts/Units/` (`IMath`, `Infanteria`, `Vehiculos`, `Helicoptero`). Pruebas: `Dotnet/TBD.Sim.Tests/UnidadesTests.cs`, sección [29].

## Qué está dentro del hash y qué no (decisión de Mak, 6-nov 23:05)
- **Pelotones** (`UnitsWorld`): DENTRO de la simulación determinista. Hash (`UnitsWorld.Hash()`), guardado (`SaveState`/`LoadState`), órdenes, xorshift propio.
- **Vehículos y helicóptero** (`Vehicle`, `Helicopter`): LOCALES, fuera del hash. Van en enteros para poder entrar en el lockstep con coma fija en el hito 7. Solo el daño a la horda
  (`Vehicle.RamDamage()`, la artillería del helicóptero) debe entrar como orden.

## Convenciones para Unity
- Suelo: X, Y en **milímetros** (int); altura Z en mm. Unity: `(X, Y) → (x, z)`, `Z → y` (÷ 1000).
- Ángulos **bam16**: 65536 = una vuelta, 0 = +X, crece hacia +Y. Los modelos de Teo miran a +Z de Unity: con `Y` = z de Unity, el rumbo `h` apunta a `(cos h, sin h)` en (x, z), así que el giro en Y
  de Unity es `90° − h` (en grados). Pitch > 0 = morro abajo; Roll > 0 = alabeo a la derecha.
- Tiempo: ticks a 30 Hz; velocidades en mm/s; mandos en milésimas (−1000..1000; freno 0..1000).

## API de pelotones
`UnitsWorld.AddPlatoon(composición, x, y, rumbo, formación)`, `Move(id, x, y, rumboFinal)`, `Stop`, `SetFormation`, `SetHoldFire`, `DamageMan` (la zona le hace daño a un soldado),
`NoteKill`. Cada tick: `Step(IList<Target> objetivos, StepResult res)`; `res.Shots` (para trazadoras y fogonazos) y `res.Hits` (daño que la zona debe aplicar a esos ids), `res.AmmoSpent`
(en disparos equivalentes). Composiciones: `Presets.Fusileros` (10), `Ametralladores` (8), `Apoyo` (8). Tipos: Jefe, Fusilero, Ametrallador, Granadero, Tirador. Formaciones: Línea, Columna, Cuña, Círculo.
Las estadísticas (`InfCatalog.Table`) son de partida; Hugo las calibra.

## Actualización (PR 2): modelo de armas de Hugo, órdenes y bucle de la noche
- `Armas.cs`: `WeaponCatalog` (12 armas) y `PlatoonCatalog` (fusileros, ametralladores, granaderos, tiradores, zapadores) con los valores de `balance_unidades.csv` (D-N72). `Load(CsvTable)` los lee; una prueba vigila
  que los valores por defecto del código coincidan con el CSV. Un impacto = un zombi abatido (`TargetHit.Damage = UnitsWorld.KillDamage`).
- Puntería = bocajarro − pendiente·metros (suelo mínimo). Explosivos con radio, letalidad por zombi y tope de bajas; vuelo de la granada (`FlightTicks`) con explosión diferida (`StepResult.Blasts`).
  El granadero solo lanza con ≥ 6 zombis en el radio, si no usa su fusil. Perforación: un zombi extra al 50 % solo con horda densa (≥ 4 zombis en 1,5 m).
- Órdenes: `Move` (sin pararse), `AttackMove` (se detiene mientras alguien dispare y reanuda), `Hold`/`Stop`, `DeployAt(id, rumbo)` (emplazar: la ametralladora emplazada exige la orden, 60 ticks parado y cono de 60°),
  `SetFormation`, `SetHoldFire`, `Resupply` (del almacén común `StockShots` a la dotación). Munición propia por pelotón en disparos equivalentes (1 u = 2.000).
- `Noche.cs`: `NightGame` (reloj 22:00–06:00 a ×28 = 30.857 ticks, 6 oleadas del CSV con la palanca de escala, frente denso y cola dispersa, entradas O1–O4 de Álvaro) y `NightParams.FromCsv`.
  **Fin de partida: siempre gana la derrota** (`Step(stationAlive, trainAlive, spawns)`): si en el mismo tick cae el edificio de viajeros o la locomotora y dan las 06:00, es derrota.
  La simulación no crea zombis: `Step` devuelve los puntos de aparición del tick y la zona los materializa.

## Noche sin ventana (`NocheSim.cs`) y mundo continuo (D-N91)
- `NightSim.FromJson(json, dificultad, semilla)` crea la noche desde `noche_sagunto.json` (Mini JSON propio, sin dependencias): oleadas, reparto exacto por entrada (O4 abierta en la 5 y la 6) y recursos.
  `AddDefaultForce()`, órdenes en `sim.Units` (Move, AttackMove, Hold, DeployAt, SetFormation, SetHoldFire), `sim.Resupply(id)` (cuenta recargas), `Run(n)` / `Step()`, `Snapshot()` (hora, zombis, munición restante, pila contra el muro, bajas, recargas),
  `Waves[i]` (estadísticas por oleada) y `Stars()`. **La «zona» es una abstracción mínima y provisional** (`NightSimParams`: velocidad del zombi, desgaste del muro por la pila, contacto con soldados): sirve para medir munición y equilibrio relativo sin ventana, no sustituye a la zona de Unity.
- Mundo continuo: al amanecer `NightGame` pasa a `Victoria` = noche cerrada con su `Report`; el reloj sigue y no entra nadie. `BeginNextNight(params)` abre la siguiente. Solo la derrota acaba la partida.

## Daño de los zombis y vida del edificio y del tren (7-nov, 00:20)

**Soldados** — `UnitsWorld.ZombieContact(IList<Target> zombies, int radiusMm, int dmgPerTick, ContactResult res)`. Proximidad pura y sin azar, la misma regla que
la zona (`balance_combate`: `cuerpo_a_cuerpo_mm` 900, `zombi_danio_tick` 2): cada zombi a ≤ radio de un soldado le quita `dmgPerTick` de vida ese tick; un zombi
toca a todos los soldados de su radio. Un soldado tiene 100 de vida: con 4000 de vida (D-N107) un zombi lo abate en 2000 ticks (67 s) y tres en 667. Se llama UNA vez por tick, después de
`Step` (disparo) y de mover a los zombis. Devuelve los caídos; `res.Fallen` dice (pelotón, soldado, X, Y) para la animación. `Platoon.Losses` cuenta las bajas
y entra en el hash y el guardado. No depende del orden de la lista (probado). Coste medido: 0,16 ms por tick con 10.000 zombis y 40 soldados (dotnet, Release).
Parámetros por defecto en `NightParams.ContactRadiusMm/ContactDmgPerTick` (CSV: `noche_contacto_mm`, `noche_contacto_danio_tick`).
Ojo: esta regla es mucho más letal que la ‰ de Hugo en NightSim (antes 14 ‰ por zombi·s; ahora 60 de vida por zombi·s). NightSim ya usa la del juego.

**Edificio y tren** — `NightGame.StationHp` (60.000) y `TrainHp` (40.000), con `P.StationDmgPerZombieS`/`TrainDmgPerZombieS` = 20 por zombi y segundo (VALORES DE
DISEÑO SIN MEDIR; el del tren es provisional). Cada tick: `Attack(zombisPegadosAlEdificio, zombisPegadosAlTren)` y después `Step(spawns)` (sin booleanos: usa su vida).
En cuanto una vida llega a 0, el siguiente `Step` es derrota (`EndedByStation`/`EndedByTrain`; gana a las 06:00). `RepairStation/RepairTrain(hp)` suben sin pasar del máximo.
Vida, desgaste fraccionario y guardado entran en el hash. CSV: `noche_estacion_vida`, `noche_tren_vida`, `noche_estacion_danio_zombi_s`, `noche_tren_danio_zombi_s`.
Quién cuenta «pegados»: la zona (los zombis a menos de X m de la fachada o del tren; la cifra de X es de Marcos y Lucía). La simulación solo suma.

## Vehículos de tierra, NH90 y pasajeros (7-nov, 04:00; datos de Hugo)

Todo **local**: no entra en el hash ni en el guardado ni en el lockstep (decisión de Mak). Solo entra en la simulación el daño a la horda (los `TargetHit` que devuelven las armas) y quién va a bordo
de qué (`Platoon.Aboard`, sí en hash y guardado). Física en enteros para poder llevarla a coma fija en el H7.

**Datos**: `VehicleCatalog.Load(csv)` y `HeliCatalog.Load(csv)` leen `vehiculo_<vamtac|pizarro|dragon>_*` y `heli_<nh90|tigre>_*` de `balance_unidades.csv`. Los valores por defecto del código son los del CSV
(prueba). El Lince no está en el CSV (provisional). Lo que no está en el CSV (ruedas, frenos, rotor, geometría, giro de la torreta) es mío y de jugabilidad.

**Vehicle** (`Dragon`, `Vamtac`, `Pizarro`, `Lince`): `Controls(acelerador, volante, freno)` y `Step()` cada tick. La zona rellena antes de `Step`: `PileAhead` (zombis apilados delante; a `BlockPile` se para en seco)
y `InHorde` (horda suelta ≥ 0,5/m²: tope `HordeSpeedMmS`). Después: `TakeContact(zombisPegados, danioPorTick)` (armadura en ‰, resto exacto), `RunOver(zombis, tick, killed)` (a ≥ velocidad mínima abate a cada zombi
que pisa con `RunOverPm` ‰, tirada por hash de vehículo, zombi y tick: sin estado; la zona aplica los ids de `killed`), `StepWeapons(unitsWorld, zombis, res)` (torreta automática: apunta al blanco más cercano de la
primera arma que tenga uno, gira a su ritmo, dispara alineada; las demás armas disparan a su blanco si está a ±6,6° de la torreta; sigue disparando sin motor, no destruido).
**Combustible**: `FuelE4` (E4 de u), se gasta cada tick en E4/s ÷ 30 con resto exacto (marcha, ralentí; apagado, 0); a cero el motor se apaga y se queda inmóvil. `Refuel(e4)` devuelve lo que ha entrado de verdad
(la economía descuenta solo eso), `SetEngine`, `LowFuel` (< 20 %), `FuelPm`, `FuelUsedE4`.
**Pasajeros**: `CanBoard/Board(world, platoon)` (casi parado, pelotón a ≤ 15 m, plazas), `CarryPassengers(world)` cada tick tras `Step`, `Unload(world, platoon)` detrás. A bordo el pelotón ni se mueve ni dispara ni lo
tocan los zombis (`ZombieContact` lo salta).

**Helicopter** (`Nh90`, `Tigre`, `Cougar`, `Chinook`): `Controls(colectivo, cíclico adelante, cíclico lateral, pedales)`, `StartEngine/StopEngine`, `Step()`. Combustible igual que arriba (vuelo 1,62 u/min, posado con rotores 0,54);
a cero en el aire entra en **autorrotación**: sin mandos, rotor girando, baja a `MaxDescentMmS` (4 m/s en el NH90) y aterriza sin daño. `Refuel` solo posado. **Vulnerable** solo por debajo de `ContactAltMm` (3 m) o posado:
`TakeContact` no hace nada por encima. `StepWeapons`: dos ametralladoras de puerta (izquierda = rumbo + 90°, derecha = rumbo − 90°), cada una solo a su semiplano, con el alcance recortado por la altura (3D);
el Tigre lleva el cañón fijo al morro (±8°). **Transporte**: `Board` solo posado (EnTierra), `Unload` solo posado; en vuelo los pasajeros van con el aparato.

**Armas montadas**: `UnitsWorld.MountFire` (usa `MountRng`, que no entra en el hash; la explosión cae ya, sin impactos pendientes). En los eventos, `Platoon` es el dueño: vehículo = −1 − Id, helicóptero = −1001 − Id.

**Para la vista**: `FireEvent.FromX/FromY` (dónde está el tirador) y `ArcPeakMm` (vértice de la parábola de las granadas, g·T²/8: 545 mm con 20 ticks de vuelo; 0 en tiro directo).
**Francotiradores**: `Target.Special` marca a un zombi especial; `FusilPrecision` y `FusilAntimaterial` (`Weapon.PrefersSpecials`) disparan antes a un especial a tiro que a cualquier otro. El resto, al más cercano.
