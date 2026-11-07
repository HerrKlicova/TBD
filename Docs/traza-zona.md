# Comparar la zona de Unity con NightSim, tick a tick (7-nov, 02:10)

Para Marcos. Sirve para saber en minutos si la zona cumple el contrato de `Docs/noche-zona-contrato.md` (pila, muro, abatidos, entradas). Código: `Assets/TBD/Scripts/Units/NocheTraza.cs` (sin Unity: sirve dentro de Unity y en dotnet).
Pruebas: `Dotnet/TBD.Sim.Tests/UnidadesTests.cs`, sección [29]. Referencia: `Assets/TBD/Data/traza_nightsim_o1.csv` (NightSim, normal, semilla 1, 9.000 ticks desde la primera oleada, una fila por segundo).

## Qué grabar (en la zona, una vez por tick, DESPUÉS de aplicar noche, pila, fuego y contacto de ese tick)
```csharp
// al empezar la noche (la ventana arranca en el tick de la primera oleada: NightTraceTools.FirstWaveTick(json, null, 1) = 1285)
var rec = new NightTraceRecorder("zona", startTick: 1285, windowTicks: 9000, every: 30);

// cada tick de simulación, con el Night.Tick de NightGame (el mismo calendario que NightSim):
rec.Sample(night.Tick,
    spawned:  zombisQueHanEntrado,          // acumulado desde el principio de la noche (suma de los SpawnPoint de Night.Step)
    alive:    zombisVivos,
    pile:     zombisPegadosAlMuro,          // vivos, fuera del recinto, a ≤ 1,5 m de un muro en pie (la pila del contrato, paso 3)
    wallHp:   vidaActualDelMuro,            // suma de las piezas de Obras, o la que use la zona
    wallHpMax: vidaDePartidaDelMuro,        // 1200000 en NightSim (10000 por u; sale de nightsim_params.json); si la zona usa otra escala, ponla: se compara en ppm
    kills:    abatidosPorElFuego,           // acumulado
    ammoShots: municionRestanteEnDisparos,  // almacén + pelotones; −1 si no se sabe
    atStation: atEdificio, atTrain: atTren);// los de Night.CountAttackers (0 si aún no se llama)

// al acabar la ventana (rec.Done == true):
System.IO.File.WriteAllText("docs/qa/traza-zona.csv", rec.Trace.ToCsv());
var rep = NightTraceTools.CompareWithReference(rec.Trace);   // lee Assets/TBD/Data/traza_nightsim_o1.csv por BalanceSource
UnityEngine.Debug.Log(rep.Text);                              // «FALLO», «AVISO» u «OK» por cada comprobación y un resumen
```
También sin Unity: `TBD_TRACE_COMPARE=docs/qa/traza-zona.csv dotnet run -c Release --no-build` en `Dotnet/TBD.Sim.Tests` imprime el informe y la tabla lado a lado, y sale con código 1 si hay algún FALLO.
Para regenerar la referencia: `TBD_TRACE_REFERENCE=<ruta.csv> dotnet run -c Release --no-build` (y copiarla a `Assets/TBD/Data/`).

## Qué comprueba y qué significa cada línea
| Comprobación | FALLO si… | Qué arreglar |
|---|---|---|
| Zombis que entran | difiere en cualquier fila | la zona no usa `NightGame.Step(spawns)` tal cual (las cuotas son exactas y deben ser idénticas) |
| Conservación | entraron ≠ vivos + abatidos en más de un 1 % | contar todas las bajas de zombis en `kills` (atropello, torreta de vehículo…) |
| Pila | es 0 en toda la ventana y NightSim llega a >0 (el fallo de Noche15) | contar los zombis pegados al muro (paso 3 del contrato); AVISO si la primera pila llega >25 % o >30 s antes o después, o si el pico difiere ×4 |
| Muro | no baja y el de NightSim sí | aplicar el daño de la pila a las piezas de muro y pasar la vida a `Night.WallPermil` |
| Desgaste del muro | medido / esperado por su propia pila < 0,2 (FALLO) o fuera de 0,8–1,25 (AVISO) | esperado = Σ pila × 1 punto/s ÷ 30 por tick; si es otra escala, es un AVISO y me la pasas |
| Abatidos y munición | solo AVISO | dependen de la fuerza desplegada: la zona usa 21 soldados propios y NightSim los 5 pelotones, así que no tienen por qué coincidir |
| Atacantes | AVISO si hay zombis pegados al edificio o al tren en esta ventana | con el muro en pie no llega ninguno antes de la O4: mirar por dónde entran (¿pasillo de la vía?) |

Límites: la ventana por defecto (9.000 ticks = 5 minutos reales, hasta la O3) solo gasta ~1 % del muro en NightSim; para juzgar el desgaste conviene una segunda pasada más larga (cambia `windowTicks` y regenera la referencia).
