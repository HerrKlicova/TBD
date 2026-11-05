# Economía y balance del MVP · «Vía Muerta» (v1, noche del 5 al 6-nov-2026)

> Autor: **Hugo (Economía)**. Consultado con **Lucía** (diseño) y **Marcos** (Unity, a través de Mak). Decide **Mak**.
> Todo sale de un modelo de simulación **fuera del juego** (`sim_economia.py`, Monte Carlo de 200 partidas por tipo de jugador, 70 días de juego). Los números son **[modelo]**: hay que confirmarlos con partidas reales (casos P7 y P8).

## 1. Resumen en cinco frases
1. Con los números de abajo, el **jugador equilibrado** toma Castellón el día 1, Teruel el día 5, el puente del Jiloca el día 9 y Zaragoza hacia el día 14. Siente escasez en alguna estación **el 51 % de los días** (objetivo de Lucía: la mitad) y pierde la partida en un **~12 %** de las campañas de 70 días.
2. El **jugador con prisa** toma Zaragoza el día 7–8, pero pierde unos 17 nodos y el **33 %** de las partidas. La **tortuga**, que solo consolida la costa, muere **siempre**, hacia el día 38. Se cumple el §3b: ni esperar ni correr sin consolidar.
3. El déficit lo provoca **la Colmena** y no la falta de trenes (D-N04). Si no pasa nada, los trenes cubren de sobra (cobertura nominal ~1,6); con asedios, emboscadas y cortes, la cobertura real baja a ~1,0 en el equilibrado y a ~0,4 en el de prisa.
4. La curva de la Colmena es una **potencia**, no una exponencial: H = 1.600 · (1 + día/20)^1,4 · √(1 + 0,3·huella). Así aguanta una campaña de 40–100 h sin dispararse.
5. **Las personas son el motor**: producen (las ciudades rinden hasta x2 con más gente), guarnecen y tripulan trenes. Rechazar siempre en el checkpoint es una **derrota segura**; aceptar y aislar quedan parejos. No hay una opción dominante.

## 2. Los cuatro recursos (D-N01) y la unidad
| Carga | Unidad | Quién la produce en el MVP | Quién la gasta |
|---|---|---|---|
| **Munición** | 1 u = 1 palé = **2.000 disparos de fusil equivalentes** (D-N08) | Depósito inicial de Sagunto (150), alijos de Valencia (120) y Teruel (50), fábrica del taller (8/día) y **Zaragoza (40/día)** | Combate: ~400 zombis comunes por u sin muros, hasta x2 con muros |
| **Combustible** | u | **Castellón (60/día)**, la refinería | Trenes (16/día a pleno uso), focos y generadores |
| **Materiales** | u | **Sagunto (80/día)**, la acería del Puerto con el hierro de Ojos Negros; Teruel (20) | Fundar, fortificar, mantener, trenes, puente y fábrica |
| **Víveres** | 1 u alimenta a 10 personas un día | **Valencia (30)**, Teruel (12), Nules, Segorbe y Cariñena (6) | Población y tropa |
| *Población* | personas, no carga | Refugiados en el checkpoint | No se gasta: trabaja, guarnece y tripula |

**Producción**: nodo guarnecido (12 personas; una ciudad necesita una plantilla de 30) → 100 %. Ciudad con más gente → +50 % por cada plantilla extra, como mucho x2. Lineal a trozos para que sea entero en Unity.

## 3. Trenes: horas-tren, no toneladas
Hallazgo clave: con 60 km/h y las distancias reales del MVP, **el tonelaje no limita nunca** (un tren llevaría todo el día de producción en un viaje). Lo que limita es **el tiempo del tren**, que es a la vez camión y cuartel (§10):
- Cada tren tiene **16 h útiles al día**. Un viaje cuesta ida y vuelta a 60 km/h + **4 h de carga y descarga** (Sagunto–Teruel: 12.500 ticks, ~1,9 viajes al día).
- **Ruta diaria** de cada rama (relevos, correo y vigilancia): ida y vuelta al último nodo + 1 h por estación.
- **Misiones**: abrir un nodo (8 h; un apeadero, 2,7 h), obra del puente (12 h/día) y **defender un asedio (6 h)**.
- **Interdicción de la Colmena**: entre el 4 % y el **35 %** de las horas-tren, más alta cuanto más interior y más presión.
- **Repostaje**: un tren reposta en cualquier depósito de la red (Castellón incluido). Sin esto la red se ahogaba en un círculo vicioso.
- Trenes: empiezas con 2. El **Tren 3 está a medio montar** (60 materiales y 15 de combustible). Cada tren nuevo cuesta 120 materiales, 30 de combustible, 10 tripulantes y 3 días. Máximo 6 en la etapa 2 (§4). En la demo hay 3–4.

## 4. La Colmena: presupuesto diario
`H(día) = 1.600 · tabla[día]/1000 · √(1 + 0,3·huella) · estación · rampa · ruido`
- **tabla** = (1 + día/20)^1,4 precalculada (`csv/colmena_curva.csv`, días 0–400), para no calcular potencias en punto fijo en el lockstep.
- **huella** = personas/100 + 0,5 por tren + 0,4 por nodo + 1,5 por obra activa.
- **Rampa** (D-N08): día 0 = 15 % (sondeos), día 1 = 50 %, día 2 = 80 % (primer asedio anunciado).
- **Asedios a la vez**: 1 en la etapa 1, 2 en la etapa 2 y 3 en la etapa 3. El 45 % de H se concentra en los asedios (x1,8) y cada 7 días hay un **gran ataque** (x2).
- Valencia atrae el **doble** (es un nido, idea de Lucía) y el interior, 1,4 veces.

| Día | 0 | 10 | 20 | 37 | 100 | 150 | 300 |
|---|---|---|---|---|---|---|---|
| Factor de tiempo | x1 | x1,8 | x2,6 | x4,3 | x12 | x20 | x49 |

Una exponencial (la primera prueba: +4 %/día) daría x400 el día 150 y rompería la campaña larga.

**Memoria de tráfico (H4, validada con Lucía)**: +200 por paso de tren en su franja, 6 franjas de 4 h, **50 % de derrame a las franjas vecinas**, decaimiento de 0,85/día y umbrales 400/600/850. En 10 días: rutinario, 4 emboscadas y 1 corte; variable, 0; horario automático fijo, 12 emboscadas. Con +150 (la propuesta inicial), el corte de 850 **no saltaba nunca**: era la asíntota exacta. Atención: 6 h de juego acumuladas en una zona (con 2 h, cualquier batalla la disparaba).

## 5. Combate abstracto S0 y pila (D-N16)
- **Eficacia** = 400 zombis por u × (1 + 0,2 × nivel de fortificación). Un nivel = 30 materiales; máximo nivel 5 (x2).
- **Zombi no abatido**: destruye 0,01 de materiales de fortificación y mata 0,004 personas (x3 sin muros).
- **Pila por pulso de 1 h**: sube (vivos + 10 % de los abatidos junto al muro) × 40 mm / metros de muro; baja 150 mm/h sin presión. Brecha al llegar a la altura del muro (sacos 1.200 mm, andén 1.500, puerta 2.000, HESCO 2.100). Mientras dura la brecha, entra el 25 % de los zombis en contacto cada hora.
  - Ejemplo: 60 m de sacos, 900 vivos y 2.100 abatidos en una hora → 740 mm, aguanta. Con otros 600 vivos y 1.000 abatidos la hora siguiente → 1.206 mm, **brecha**. Se rompe cuando falta en torno a la mitad de la munición.

## 6. Checkpoint (§9b, diseñado con Lucía)
| Opción | Qué pasa | Resultado en el modelo (70 días) |
|---|---|---|
| Aceptar | Entran ya y trabajan. Riesgo de brote de 1,2 % por persona (400 zombis dentro y −8 % de población) | Derrota 17 % |
| Aislar | 1 día de cuarentena, comen el doble y no trabajan. Cupo de 12 al día por ciudad (4 en apeaderos) | Derrota 10 % |
| Rechazar | Ahorra víveres, pero cada rechazado vuelve como 25 zombis junto a esa estación y baja la reputación (llegan menos) | Derrota **100 %** si se hace siempre |
| Mixto (el sensato) | Rechaza con hambre, acepta con prisa por guarnecer y si no aísla | Derrota 12 % |

## 7. El guion de la demo (validado con Lucía)
Política equilibrada, mediana de 200 partidas: Castellón el día 1–2, **Teruel el día 5**, Tren 3 el día 8–9 (el día 6 si se encarga el 3–4), **puente el día 9** y ≥ 9 nodos el día 10. Ninguna derrota en los 10 días. El final «Zaragoza o día 10» funciona: el medio termina mirando Zaragoza y el atrevido la toma. Cambios aprobados por Lucía:
- **Apeaderos** (Puçol, Almenara, Segorbe, Barracas, Caminreal y Calamocha): se aseguran con un retén de 6 y sin materiales.
- La estación que se funda es **Nules**.
- **Tren 3 a medio montar**.
- **Sagunto a 80** de materiales.

## 8. Sensibilidad: dónde se rompe (`resultados.txt`)
| Parámetro | Valor bajo | **Elegido** | Valor alto |
|---|---|---|---|
| α (curva) | 1,2: prisa solo pierde el 15 % | **1,4** | 1,6: el equilibrado pierde el 33 % |
| H0 | 1.300: nadie pierde | **1.600** | 1.900: el equilibrado pierde el 43 % |
| k_huella | 0,15: la prisa gana | **0,3** | 0,6: el equilibrado pierde el 79 % |
| **Zombis por u** | **300: el equilibrado pierde el 74 %** | **400** | 500: nadie pierde |
| Interdicción máx. | 0,25: la prisa gana | **0,35** | 0,45: el equilibrado pierde el 23 % |

**El punto más frágil es la eficacia de la munición.** Si en la zona real un soldado mata menos de lo previsto, la economía se cae. Por eso el caso **P7** es lo primero que hay que medir en el hito 3. Los ajustes de dificultad deberían tocar H0 y k_huella, que son los parámetros más suaves.

## 9. Lo que dice el modelo y aún no está en el GDD
- Hacia el **día 60**, el equilibrado gasta 70–85 u de munición al día y Zaragoza más la fábrica dan ~90. **La red del MVP se queda pequeña** justo cuando debería abrirse la etapa 3 (Calatayud, Vinaròs). Encaja con el acceso anticipado.
- **Castellón es el talón de Aquiles**: es la única fuente de combustible. La Colmena debería aprender a ir a por ella (memoria de "dónde te duele").
- Derrota por **colapso**: 5 días seguidos sin combustible para mover trenes (§3b, PROTOTIPO).

## 10. Archivos
- `sim_economia.py`: el modelo (`--csv DIR`, `--barrido`, `--calibrar`, `--checkpoint`).
- `sim_memoria_trafico.py`: validación de la memoria de tráfico.
- `generar_csv.py` → `csv/balance_economia.csv`, `balance_nodos.csv`, `balance_colmena.csv`, `colmena_curva.csv` y `balance_pila.csv`. Formato de Marcos: «;», todo entero, milésimas con columna de escala y redondeo hacia abajo.
- `casos_prueba.py` → `csv/casos_prueba.csv`: casos exactos P1–P6 en aritmética entera y rangos P7–P8.
- `series/`: series diarias de una partida (semilla 7) por tipo de jugador. `resultados.txt`: el último Monte Carlo.

## 11. Limitaciones honestas del modelo
- Los tres jugadores son **reglas**, no personas. Un humano abandona apeaderos, cambia el horario o retrasa una obra. Los porcentajes de derrota son orientativos.
- El combate S0 es una fórmula. Hasta que el prototipo mida P7, el 400 es una estimación.
- La memoria de la Colmena (H4) se valida aparte; el modelo económico solo la representa como interdicción.
- Las distancias son aproximadas a la red real (Sagunto–Teruel 130 km, Sagunto–Zaragoza 310 km).
