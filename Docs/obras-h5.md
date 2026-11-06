# Obras por nodo (hito 5, carril 1)

Lógica pura en `Assets/TBD/Scripts/Net/Obras.cs`. Pruebas: `Dotnet/TBD.Sim.Tests/ObrasTests.cs` (sección [15]).
Diseño: `diseno-hito5.md` v0.3 §2b (mr.mak), D-N43 y D-N44.

## Unidades
- Longitudes y coordenadas en **mm**, locales al nodo. El andén es el (0, 0).
- Coste en **E4** (1/10.000 de u): coste de un muro = precio por 10 m × longitud en mm. Es exacto, sin redondeos intermedios.
  Al almacén se le cobran `ceil(E4 / 10.000)` u.
- Vida y nivel en **‰**.

## Modelo
- `WorkPiece`: un módulo de muro (≤ 10 m) o una pieza puntual (puerta, nido, foco, depósito, andén). Guarda `CostE4`, `LifeMil` y `Built`.
- `ObrasState` (en `NetWorld.Works`): lista de piezas por nodo. Su `Hash()` entra en el hash de **existencias** y vale 0 sin obras,
  así que los goldens G1–G5 no cambian.
- `Obras.PlanWall` / `PlanPoint` / `ObrasState.Preview`: **una sola función** para la previsualización en directo y para la orden.

## Regla de corte por falta de materiales
El muro se corta en el tramo más largo cuyo coste cabe en las existencias: `longitud máxima = existencias × 10.000 ÷ precio` (mm, hacia abajo),
recorriendo la polilínea desde el primer vértice. Menos de `muro_minimo_mm` (1 m) no se construye. Las piezas puntuales no se cortan.

## Nivel de fortificación (‰)
`Σ(CostE4 × LifeMil) ÷ (30 u × 10.000)`, tope 5.000 ‰. Solo cuentan las piezas **construidas**, **no el depósito** y con el punto medio a
**≤ 60.000 mm del andén**. Ejemplo de D-N44 (150 m HESCO, puerta, 2 nidos, 3 focos = 118 u): 3.933 ‰.

## La orden
`NetCmdType.PlaceWork = 9`. `Arg = nodo | (PieceKind << 16)`. `Payload = [x0,y0,x1,y1,…]` (muro) o `[x,y]` (pieza puntual).
`NetCommand.Payload` es nuevo; `NetGolden` lo guarda como quinto campo opcional (los .golden antiguos se siguen leyendo).
Solo se acepta con la **zona del nodo materializada** (`ActiveZoneNode == nodo`).

## Obreros, avance y cancelar (paso 2)
- `NetCmdType.AssignWorkers = 10` (`Arg` = nodo, `Arg2` = obreros) y `CancelWork = 11` (`Arg` = nodo, `Arg2` = número de obra; una orden `PlaceWork` = una obra, y `WorkPlan.Id` lo devuelve).
- Cada tick, en cada nodo, trabajan `min(obreros asignados, población del nodo)`. Se reparten por obra (la más antigua primero), como mucho **4 por pieza**; cada obrero aporta 1 obrero·tick (1 obrero·h = 1.500 ticks).
- Trabajo de una pieza (`NeedE4`, en 1/10.000 de obrero·tick): sacos 2 obrero·h por 10 m, HESCO 4 por 10 m, puerta 4, nido 6, foco 2, depósito 8, andén 10. Un muro de 10 m de sacos con 2 obreros: 1.500 ticks exactos.
- `ObrasState.IsActive(nodo)` = hay obreros asignados y alguna pieza sin terminar. Es lo que usará el director para el sondeo de obra.
- Cancelar (Hugo, D-N59): si nadie ha empezado, se devuelven **exactamente las u cobradas** (cada obra guarda `PaidU`; antes se redondeaba hacia abajo y se perdía 1 u); si ya hay trabajo hecho o piezas terminadas, `floor(50 % de lo cobrado)` por la parte sin terminar (`cancelar_obra_empezada_permil`). Lo terminado se queda.
- `ObrasState.CompleteAll` sigue ahí solo como atajo de pruebas.

## Condiciones de Marcos (obreros)
- **Máximo de obreros por pieza**: `obra_obreros_max_pieza` (por defecto 4). Cada pieza guarda su `MaxWorkers` al registrarse, así que una obra grande puede llevar más (el puente del Jiloca, 8). El hash lo incluye.
- **Retén**: nunca trabajan obreros por debajo de `obras_guarnicion_minima` (6, de `balance_construccion.csv`, D-N59): `EffectiveWorkers = min(asignados, población − 6)`. NO es la `guarnicion_minima` de 12 de la producción (con 12, una estación recién fundada, con 12 personas, no podría construir nada).
- **Pausa**: `NetWorld.WorkPaused(nodo)` = nodo asediado o perdido, o con densidad de horda ≥ 500 (`SiegeAt`). Una obra pausada no avanza.
- **Obra activa** (`NetWorld.WorkActive`) = trabajo pendiente y obreros efectivos > 0 y sin pausa. Es lo que usa el director.

## Ruido de obra en el director (paso 3, D-N44)
- **Sondeo**: cada hora, cada nodo con obra activa tiene un 83 ‰ de recibir un sondeo (sorteo por semilla, tick y nodo; sin estado). Trae el 5 % del presupuesto del día de la Colmena y se concentra en el nodo con `Gather`. **Va descontado, no es masa extra** (Hugo, aprobado por Mak): se paga con el 400 ‰ del presupuesto que no va a asedios, primero con la deriva de esa hora y después con lo que el canal de emboscada lleva ahorrado. Si no alcanza, el sondeo es más pequeño. El total de 20 días no cambia y la masa del campo no cambia. Si la celda del nodo es la de la zona en detalle (congelada), la horda se concentra en una celda vecina.
- **Avisos** (textos de Lucía en `textos_informe.csv`): `OC-1` (primera obra activa de la partida, una sola vez), `OC-3` (llega un sondeo; el sondeo no tiene plazo, así que `OC-2` «en {minutos} min» no se usa todavía) y `OC-4` (la horda para la obra; una vez por parada).
- **Peso**: `HiveDirector.SiegeTargetWeight` = peso × 1,5 con obra activa. Solo se usa para sortear el objetivo del asedio, no para la deriva.
- **Huella**: +0,5 por nodo con obra activa (partir una obra en varias órdenes no la multiplica). El puente y las obras grandes (1,5) llegan con el paso 4.
- Valores en `balance_construccion.csv` (ya estaban): `obra_sondeo_permil_hora`, `obra_sondeo_permil_presupuesto_dia`, `obra_peso_asedio_permil`, `obra_huella_muros_permil`.

## Lo que queda para los pasos siguientes
- Daño y desgaste diario (1 %).
- `OC-2` (sondeo en camino, con visión) necesita un plazo de aviso antes de que llegue la masa: es un cambio de modelo con Hugo.
- Valores que no están en `balance_construccion.csv` y viven en el código con `TryValue`: `muro_modulo_mm` (10.000), `muro_minimo_mm` (1.000),
  `perimetro_mm` (60.000), `obra_obreros_max_pieza` (4) y las horas de obra de cada pieza (`obra_sacos_h_por_10m` 2, `obra_hesco_h_por_10m` 4, `obra_puerta_h` 4, `obra_nido_h` 6, `obra_foco_h` 2, `obra_deposito_h` 8, `obra_anden_h` 10; salen de `diseno-hito5.md` §2). Desde D-N59 estas claves **ya están en `balance_construccion.csv`** (copia de Hugo en `Assets/TBD/Data`), y los u por nivel salen de `fort_materiales_por_nivel` de `balance_economia.csv` (la clave `nivel_u_por_nivel` ya no existe). Alturas: sacos 1.200, HESCO y puerta 2.100, nido y andén 1.500 mm.
- Integración en la zona (Marcos): leer `Works.Snapshot(nodo)` al materializar y devolver con `Works.Restore(nodo, piezas)` al salir.

## Fundar una estación (paso 3 del orden de trabajo de Lucía)
`Net/Estaciones.cs`, pruebas en `EstacionesTests.cs` (sección [18]).
- **Modelo del flag** (Mak, 6-nov): no se añaden nodos en caliente. Los emplazamientos existen desde el principio con `Founded = false` (en el hash, solo cuando es true). La lista es `StationParams.Sites`; hoy, solo **Nules** (Jérica salió de la demo).
- **Orden** `NetCmdType.FoundStation = 12` (`Arg` = nodo). Requisitos: el nodo es un emplazamiento sin fundar; un tren **parado en el nodo** (en una parada del horario, o detenido a menos de `StopRadiusMm` = 5 m del nodo en su tramo) con **30 u de materiales y 12 personas** a bordo (`fundar_materiales` y `guarnicion_minima` de `balance_economia.csv`); sin horda encima (densidad < 500). Efecto: los 30 u se gastan, las 12 personas pasan a la población del nodo y el nodo queda fundado. Resultado en `FoundResult` (la interfaz explica por qué no).
- **Nivel**: `NetWorld.StationKindOf(nodo)`: sin fundar, apeadero, o **estación** (depósito construido y 40 personas, `estacion_personas`, valor por defecto porque no está en el CSV de Hugo).
- **Detener en el emplazamiento** (Mak, 6-nov): `NetCmdType.HaltAtSite = 13` (`Arg` = nodo emplazamiento, `-1` cancela; `Arg2` = servicio). El tren para **justo en el nodo** en cuanto llega (la marcha sobrante de ese tick se pierde), queda retenido y `TrainGo` lo suelta. Solo emplazamientos. La parada pendiente (`TrainService.HaltAt`) entra en el hash del tren mientras está.
- **Un emplazamiento sin fundar no cuenta** para el peso del director (ni como objetivo ni en la deriva) ni para la huella (Mak, 6-nov). Con los presupuestos de los goldens la parte de Nules en la deriva se redondea a 0, así que **G1–G5 siguen idénticos**: no hubo que regenerar ninguno.
- **No incluido, a propósito**: el retén de 6 personas para asegurar apeaderos existentes; refugiados del checkpoint.

## Mantenimiento y desgaste diario (paso 4, #8, Hugo)
- Una vez al día (cambio de día), cada nodo con obras construidas paga el **mantenimiento** de su almacén: `MaintenanceU = ceil(fort_desgaste_dia ‰ × Σ(CostE4 × vida) ÷ 10.000)` u de materiales (la misma Σ que el nivel de fortificación, Hugo: piezas defensivas construidas a ≤ 60 m del andén, sin el depósito).
- Si el almacén lo cubre, se descuenta y **la vida no baja**. Si no, no se cobra nada y lo que está dentro del perímetro pierde `fort_desgaste_dia` (10 ‰) de la vida que le queda (mínimo 1 ‰). A 0 ‰ la pieza se destruye y se quita. Una estación sin reservas conserva ~82 % a los 20 días.
- **El depósito no se desgasta.** Los **muros a más de 60 m** del andén se desgastan **siempre**, sin mantenimiento posible, a `desgaste_fuera_perimetro_permil_dia` (10 ‰, de `balance_construccion.csv`): ~82 % a los 20 días, así que no hay anillos exteriores eternos.
- `ObrasState.Damage(nodo, índice, ‰)`: gancho del daño de combate (aún sin llamador). Reparar y terreno asegurado, pendientes.

## Reparar un tramo y el puente (paso 4, Hugo y Mak, D-N59)
- **Escombros**: `RailEdge.Debris`. Solo los cortes del director (TR-4) los dejan: el tramo queda `Cortada` hasta que lo repara un tren de obras. Los cortes por densidad de horda siguen despejándose solos. `NetWorld.DestroyEdge(tramo, puente)` los crea por guion (el puente volado del Acto III: `bridge = true`).
- **Tren de obras**: `TrainService.WorksTrain` (marcador, entra en el hash solo si es true). Hasta que el taller rehabilite uno (paso 5), la escena lo marca a mano.
- **Orden** `NetCmdType.RepairEdge = 14` (`Arg` = tramo, `Arg2` = servicio). Resultado en `RepairResult`: el tren de obras debe estar **en posición** (parado en un extremo del tramo o detenido delante del corte), con **5 u** de materiales y **8 obreros** a bordo (`reparar_tramo_u`, `tren_obras_obreros`), y sin horda encima (sobre el tramo o densidad de asedio en un extremo). Gasta los materiales de la **carga del tren** y lo retiene (`Held`) hasta terminar.
- **Avance**: cada tick suma los obreros que quedan a bordo (máximo 8), así que 3 h con 8 obreros son 4.500 ticks y si mueren obreros va más lenta. Se para si el tren sale de posición o llega la horda, y espera. Al terminar el tramo vuelve a existir (`Ok`) y el tren se libera.
- **Puente** (el del Jiloca): `puente_jiloca_materiales` (80 u, `balance_economia.csv`) y `puente_jiloca_obreros_h` (192, `balance_construccion.csv`): con los 8 obreros del tren de obras y el horario de obra, 2 días (72.000 ticks). `reparar_tramo_h` (3) ya está en el CSV.
- **No incluido**: texto de aviso de reparación terminada (Lucía), la huella de la obra (1,5 para el puente) y el ruido de obra en el director, el tren de obras de verdad (taller, paso 5), la zona de emboscada como batalla.

## Puerta de vía (paso 4, diseno-hito5.md §2a, Lucía y Hugo)
- **La red conoce la vía**: es la recta y = 0 del nodo, con un gálibo de 3,2 m (`TrackHalfMm` = 1.600, `via_hueco_mm` / 2; no está en el CSV, vive en el código). `PlanWall` recorta la polilínea contra la franja |y| < 1.600:
  - un tramo que **cruza de lado a lado** deja una **puerta de vía** (`PieceKind.PuertaVia = 7`) en el cruce, con un hueco de 3,2 ÷ sen α medido sobre el muro. Entra en el coste en directo, en el orden del trazado y en la regla de corte por materiales (si no cabe la puerta, el muro se corta antes de la vía);
  - cruzar con **menos de 45°** rechaza el trazado: «El muro cruza la vía demasiado en diagonal»;
  - lo que queda **dentro de la franja sin cruzarla** (a lo largo de la vía, o un muro que acaba en ella) se recorta en el borde del gálibo y no lleva puerta; si todo cae sobre la vía, «el muro cae sobre la vía»;
  - las piezas puntuales (puerta, nido, foco, depósito) no pueden estar en la franja («la vía queda libre»); el andén fortificado sí, va junto a la vía.
- **Coste**: `pieza_puerta_via_u` (8 u) y `obra_puerta_via_h` (6 obrero·h), del CSV de Hugo. Defensiva: cuenta para el nivel y para el mantenimiento. Altura 2,1 m. No se coloca a mano.
- **Estado, en el hash** (solo para las puertas de vía): `GateMode` (0 automática, 1 bloqueada abierta, 2 bloqueada cerrada), `GateProg` (0 cerrada … 180 abierta; 180 ticks de zona = 6 s a 30 Hz, claves `puerta_via_ticks` y `puerta_via_cerca_mm` en el código) y `Broken`.
- **Para la zona** (Marcos): `ObrasState.StepGates(nodo, posicionesXdeLosTrenesEnMm)` en cada tick de zona (se abre sola con un tren propio a ≤ 150 m y se cierra cuando se aleja; bloqueada, ignora los trenes) y `ObrasState.GatePassable(P, pieza)` (abierta del todo o rota). Con la puerta cerrada, el tren se para antes de llegar. Un tren parado en el hueco cuenta como cierre (D-N27) en la zona.
- **Orden** `NetCmdType.SetGate = 15`: `Arg` = nodo | (modo << 16), `Payload` = [x, y] de la puerta (la más cercana, a ≤ 3,2 m). Solo con la zona del nodo materializada.
- **Rota** (daño o desgaste a 0 ‰): la pieza **no desaparece**: queda `Broken` (hueco con escombros, la horda entra, la vía sigue usable), no cuenta para el nivel ni el mantenimiento y no se mueve. Se **reconstruye encima**: una puerta nueva a ≤ 3,2 m la sustituye.
- **Zona**: `SplitAroundTrack` de la vista ya no hace falta; basta enviar la polilínea entera con `PlaceWork` y la red añade las puertas. La vista debe dibujar `PieceKind.PuertaVia` (sprite de puerta de Bruno) y no tratar `Puerta` y `PuertaVia` igual en el sólido que bloquea a la horda: la de vía, abierta, deja pasar al tren.
- Las pruebas de coste, nivel y obra de las secciones [15] a [19] trazan muros por el andén (y = 0), así que desactivan la vía en su mundo (`TrackHalfMm = 0`); la franja se prueba en la sección [21].

## Horario de obra (Hugo, D-N59)
- **Todas las obras trabajan de 06:00 a 18:00** (`obra_hora_inicio` 6 y `obra_hora_fin` 18; 12 h al día, el `horas_obra_dia` del modelo) y de noche se paran: obras de muros y piezas (`EffectiveWorkers`) y reparaciones de tramo y puente. Una obra a medias a las 18:00 se queda congelada y sigue a las 06:00 sin perder progreso.
- Una obra parada de noche **no cuenta como activa** (`WorkActive`): no hay sondeo de obra ni peso ×1,5 ni huella de 0,5 mientras no se trabaja. *Esto lo deduje yo: Hugo no lo dijo; si la Colmena debe oír la obra aunque pare de noche, es una línea en `EffectiveWorkers`.*

### Obra en curso y obra trabajando
La noche para el trabajo pero no la obra. `WorkInProgress` (encargada y sin acabar, con obreros) cuenta para el peso ×1,5 del asedio y la huella, de día y de noche, para que los objetivos del asedio sean estables. `WorkActive` (en curso y dentro del horario) es lo que hace sondeo y avanza.

### Terreno asegurado
`PlaceWork` y `PreviewWork` exigen que no haya zombis en pie a menos de `SecureMm` (30 m) de ningún punto de la obra. La rejilla de densidad la ofrece la zona con `NetWorld.ZoneDensity` (`DensityView`: celdas en mm locales al nodo); sin zona enchufada todo vale. No entra en el hash. Pendiente del lado de la zona: construir la vista y enchufarla (Marcos).
### Horario de un tren (SetSchedule)
`NetWorld.SetSchedule(tren, datos)` sustituye las paradas de un tren parado: seis enteros por parada `[nodo, hora de salida, munición, combustible, materiales, personas]`. Tiene que incluir el nodo donde está el tren, de 1 a 12 paradas, sin dos seguidas en el mismo nodo. Entra en el hash solo si el jugador lo ha puesto. Es lo que permite mover un tren recién salido del taller y cargar los materiales del tren de obras.

### El taller (paso 5)
`Taller.cs`: cola de encargos del taller de Sagunto, un encargo a la vez y las 24 h (tren nuevo 3 días, rehabilitar 36 h). `OrderTrain(tipo)` cobra con las existencias del taller y no cobra nada si falla (materiales, combustible, libres por encima del retén, máximo de trenes, rehabilitación ya hecha). Un tren de servicio (nuevo o el Tren 3) toma 10 de tripulación al encargarse; el de obras no: sus 8 obreros son su tripulación y se descuentan al salir (si no hay, el tren espera en el taller, TL-2). `CancelTrainOrder(i)`: 100 % de lo pagado sin empezar, floor(50 %) empezado (TL-3). Chispas (`ChispasAt`: -2 no está, -1 taller, ≥ 0 tren) acelera un 25 % las rehabilitaciones en el taller y las reparaciones de vía y puente a bordo del tren de obras; no sale del taller hasta que ha rodado el tren de obras. El tren nace en Sagunto con una sola parada y TL-1; el horario lo pone `SetSchedule`.

## Sagunto empieza con 120 u de muro construido (D-N63)
`NetScenario.Mvp()` llama a `NetScenario.SagunteStart(w)`: coloca con `Obras.PlanWall`/`PlanPoint` y da por terminadas 36 piezas, sin cobrar (el stock del día 1 no se toca): 120,24 u, nivel 4.008 ‰.
- Recinto de 86 × 41 m, en mm locales al nodo: X ±43.000, Y ±20.500, vía en y = 0, andén (0, 0) = (113; 64,5) en la zona (`Level`).
- HESCO: frente oeste (4 módulos y la puerta de vía en la vía) y 14,5 m en cada ala, 66,8 m en total. Sacos: el resto, 180,8 m. Puerta peatonal en el este (X +43.000, Y -8.000), nidos en (-39.500, ±7.000) y focos en (-40.500, ±15.000) y (0, 18.500).
- El muro este cruza la vía, así que `PlanWall` le pone su puerta de vía: hay **dos**; la del este queda **bloqueada cerrada** (`GateMode` 2).
- `Mvp(false)` deja Sagunto sin obras: las pruebas de obras, taller, horario, estaciones, director y red parten de ahí. Pruebas: `SaguntoInicioTests.cs` ([27]); si Marcos cambia otra vez el recinto en `Level.cs`, las comprobaciones de medidas fallan.
- Mantenimiento: 2 u al día de los materiales de Sagunto.

