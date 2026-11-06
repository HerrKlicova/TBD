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
- Cancelar: 100 % de lo pagado si nadie ha empezado; 50 % (`cancelar_obra_empezada_permil`, ya en el CSV) si hay trabajo hecho o piezas terminadas. Lo terminado se queda.
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
- Daño y desgaste diario (1 %), terreno asegurado (rejilla de densidad de la zona).
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
- Si el almacén lo cubre, se descuenta y **la vida no baja**. Si no, no se cobra nada y se aplica el desgaste: cada pieza construida pierde `fort_desgaste_dia` (10 ‰) de la vida que le queda (mínimo 1 ‰). A 0 ‰ la pieza se destruye y se quita. Una estación sin reservas conserva ~82 % a los 20 días.
- `ObrasState.Damage(nodo, índice, ‰)`: gancho del daño de combate (aún sin llamador). Reparar y terreno asegurado, pendientes.

## Reparar un tramo y el puente (paso 4, Hugo y Mak, D-N59)
- **Escombros**: `RailEdge.Debris`. Solo los cortes del director (TR-4) los dejan: el tramo queda `Cortada` hasta que lo repara un tren de obras. Los cortes por densidad de horda siguen despejándose solos. `NetWorld.DestroyEdge(tramo, puente)` los crea por guion (el puente volado del Acto III: `bridge = true`).
- **Tren de obras**: `TrainService.WorksTrain` (marcador, entra en el hash solo si es true). Hasta que el taller rehabilite uno (paso 5), la escena lo marca a mano.
- **Orden** `NetCmdType.RepairEdge = 14` (`Arg` = tramo, `Arg2` = servicio). Resultado en `RepairResult`: el tren de obras debe estar **en posición** (parado en un extremo del tramo o detenido delante del corte), con **5 u** de materiales y **8 obreros** a bordo (`reparar_tramo_u`, `tren_obras_obreros`), y sin horda encima (sobre el tramo o densidad de asedio en un extremo). Gasta los materiales de la **carga del tren** y lo retiene (`Held`) hasta terminar.
- **Avance**: cada tick suma los obreros que quedan a bordo (máximo 8), así que 3 h con 8 obreros son 4.500 ticks y si mueren obreros va más lenta. Se para si el tren sale de posición o llega la horda, y espera. Al terminar el tramo vuelve a existir (`Ok`) y el tren se libera.
- **Puente** (el del Jiloca): 80 u (`puente_jiloca_u`, **no está en el CSV**: vive en el código con valor por defecto) y 192 obrero·h (`puente_jiloca_obreros_h`): con 8 obreros, un día entero (36.000 ticks). `reparar_tramo_h` (3) tampoco está en el CSV.
- **No incluido**: texto de aviso de reparación terminada (Lucía), la huella de la obra (1,5 para el puente) y el ruido de obra en el director, el tren de obras de verdad (taller, paso 5), la zona de emboscada como batalla.
