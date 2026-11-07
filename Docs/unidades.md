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
