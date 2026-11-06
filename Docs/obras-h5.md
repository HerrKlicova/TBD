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

## Lo que queda para los pasos siguientes
- Orden «asignar obreros» y avance de la obra: hoy `ObrasState.CompleteAll` (provisional) es lo único que marca piezas como construidas.
- Cancelar (100 % / 50 %), daño y desgaste diario (1 %), terreno asegurado (rejilla de densidad de la zona).
- Sondeo de obra en el director (83 ‰/h, 5 % del presupuesto, nodo ×1,5) y huella 0,5.
- Valores que no están en `balance_construccion.csv` y viven en el código con `TryValue`: `muro_modulo_mm` (10.000), `muro_minimo_mm` (1.000),
  `perimetro_mm` (60.000), `nivel_u_por_nivel` (30). Alturas: sacos 1.200, HESCO y puerta 2.100, nido y andén 1.500 mm.
- Integración en la zona (Marcos): leer `Works.Snapshot(nodo)` al materializar y devolver con `Works.Restore(nodo, piezas)` al salir.
