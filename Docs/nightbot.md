# NightBot: el jugador automático de la noche (-nightbot, petición de Sara, 7-nov 02:20)

Código: `Assets/TBD/Scripts/Units/NocheBot.cs`. Pruebas: sección [29] de `Dotnet/TBD.Sim.Tests/UnidadesTests.cs` («NightBot»). Informe: `TBD_BOT_REPORT=1 dotnet run -c Release --no-build` en `Dotnet/TBD.Sim.Tests`.

## Cómo lo activa la zona (Marcos)
```csharp
// al crear la noche, solo si la build lleva la bandera -nightbot:
NightBot bot = args.Contains("-nightbot") ? NightBot.FromParams(NightSimParams.FromJson(BalanceSource.Read("nightsim_params.json"))) : null;
//   (o new NightBot(fortX0, fortY0, fortX1, fortY1, stationX, stationY) con los mm del recinto y del edificio de la zona)

// cada tick de simulación, ANTES de Units.Step(...), con la misma lista de zombis vivos que se le pasa al fuego:
if (bot != null) bot.Step(units, zombisVivos /* IList<Target> */, night.Tick);
```
Nada más: no hay que enlazar mandos ni UI. `NightSim` lo usa con `sim.Bot = NightBot.FromParams(sim.SP)`. Con `bot.Enabled = false` (o sin crearlo) no hace nada.
`bot.Describe()` da una línea para el informe: decisiones, órdenes, cambios de lado, reabastecimientos y la amenaza suavizada por lado.

## Qué hace (decide cada 60 ticks = 2 s)
1. **Reabastece al 30 %**: a cada pelotón vivo con menos del 30 % de su dotación le pasa munición del almacén con `UnitsWorld.Resupply`, la misma orden del jugador (`ResupplyBelowPct`).
2. **Mide la amenaza** por lado del recinto: zombis vivos fuera del muro a ≤ 40 m de él (`ThreatBandMm`), asignados al lado por el que más se han pasado; media móvil entera (¾ lo anterior + ¼ lo de ahora) para que no salte de lado a lado. Guarda también el centro de la horda a lo largo de cada lado.
3. **Reparte los pelotones de línea** (todos menos zapadores) por lados en proporción a la amenaza (restos mayores). Si un pelotón ya está en un lado con cupo, se queda; los demás van a los lados con hueco, el más amenazado primero, el pelotón más cercano. Sin amenaza (< 8) no mueve a nadie.
4. **Órdenes** (`UnitsWorld.AttackMove`, ataque-movimiento: avanzan y se paran a disparar mientras haya zombis a tiro): cada pelotón a un punto del lado, centrado en la horda, separados 10 m entre sí y a la distancia del muro que pide su arma: fusileros 3 m, ametralladoras 4 m (y se **emplazan** mirando al muro con `DeployAt` al llegar), tiradores 12 m, **lanzagranadas 18 m** (su mínimo es 15 m). Solo repite una orden si el punto cambia más de 4 m.
5. **Zapadores** (reserva): `Move` junto al edificio; no van al muro.

Es determinista: solo lee el estado del mundo y la lista de zombis (en el orden que la zona se la da a `Units.Step`), todo en enteros, sin azar. Misma semilla → mismo hash y mismas órdenes (prueba).

## Resultado en NightSim (fuerza por defecto, semillas 1–3; balance v3: muro 1.200.000, difícil 1140; medido antes de la D-N127, la difícil a 1280 no está medida con el bot)
| Dificultad | Partida | Resultado | Muro al amanecer | Bajas propias | Munición que queda | Pila máxima |
|---|---|---|---|---|---|---|
| fácil | fija + bot de recarga | victoria ★3 | 81–82 % | 0 | 16–17 u | ~1.100 |
| fácil | **NightBot** | victoria ★2 | 78–84 % | 0–1 | 14–15 u | 1.100–2.100 |
| normal | fija + bot de recarga | victoria ★2 | 72–73 % | 0 | 14 u | ~1.700 |
| normal | **NightBot** | victoria ★2 | 74–76 % | 0–1 | 10,6–12,2 u | 1.500–1.900 |
| difícil | fija + bot de recarga | victoria ★1–★2 | 17–25 % | 0 | 10 u | ~4.200 |
| difícil | **NightBot** | victoria ★1 | **44–51 %** | 0–3 | 6,3–7,1 u | 3.000–3.600 |

(Tres semillas por fila. El NightBot hace entre 39 y 46 reabastecimientos por noche; en el informe de NightSim salen como «recargas 0» porque los hace él mismo, no el bot de recarga aparte.)

«fija+bot recarga» es la partida de siempre: pelotones quietos y el bot de Hugo que solo recarga. El NightBot gana siempre, gasta algo más de munición (dispara más porque se mueve hacia la horda) y cuida mucho más el muro en difícil. En fácil pierde la ★3 por la munición restante (14–15 u frente a 16–17 u). Sufre alguna baja en difícil (pelotones pegados a la pila).

## Límites
- Solo mueve **pelotones**; no conduce vehículos ni helicópteros (para eso, la orden de transporte).
- No repara el muro ni construye (los zapadores solo esperan).
- Si el jugador humano da una orden a un pelotón mientras el bot está activo, el bot la pisa en la siguiente decisión: es un modo de prueba, no un asistente.
- Para zonas con otros recintos, pasar los mm del recinto al constructor.
