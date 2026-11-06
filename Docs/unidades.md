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
