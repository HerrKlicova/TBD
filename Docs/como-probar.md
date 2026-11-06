# Cómo probar el juego (H3 y H4), en 5–10 minutos

Marcos (Programación), noche del 5 al 6 de noviembre. Todo lo de aquí lo he comprobado yo en el Editor, y también desde una copia limpia del repositorio (clon nuevo, importado desde cero en 2 min 18 s, sin errores de compilación).

## 1. Abrir el proyecto
1. Unity Hub → **Add → Add project from disk** → `C:\Dev\TBD`. Versión: **6000.3.28f1** (la 6.3 LTS).
2. Abre la escena **`Assets/Scenes/Union.unity`** (el mapa 3D de operaciones). Pulsa **Play**.
3. No hace falta nada más. Los CSV de balance de Hugo se leen de `Assets/TBD/Data`: si cambias uno, se nota en la siguiente partida.

## 2. Teclas

**Mapa (escena Union)**

| Tecla | Qué hace |
| --- | --- |
| W A S D o flechas | mover |
| Q / E | girar |
| R / F | inclinar |
| rueda del ratón | zoom. A menos de 2 km de una estación **se entra en la zona** |
| botón derecho + ratón | girar y inclinar |
| Espacio | pausa |
| 1 / 2 / 3 | velocidad x1 / x2 / x4 |
| J | ir a la alerta más urgente |
| F8 | **emboscada de prueba** (solo para probar) |
| Intro | entrar en la emboscada activa |
| F3 | apagar la prueba de desincronización |
| F9 | captura (carpeta `Captures`) |

Una alerta crítica nueva baja la velocidad a x1 sola (D-N11): vuelve a pulsar 3.

**Zona de estación** (entras acercando la cámara a una estación)

| Tecla | Qué hace |
| --- | --- |
| 6 | pelotón a bordo: dispara desde el tren parado (sin bajas) |
| 0 | pelotón a pie (arriesgado) |
| K | reembarcar a los que estén a menos de 20 m del tren |
| 3 | mover o parar el tren |
| 2 | abrir o cerrar la puerta |
| 1 / 4 | más zombis (de prueba): al tejado / por el oeste |
| 5 | trazadoras |
| B | muros libres (clics = puntos, clic derecho corta) |
| P | pausa de la zona |
| **M** o alejar del todo | **volver al mapa** (los vivos vuelven a la mancha) |
| WASD, Q E, R F, rueda | cámara, como en el mapa |
| 7 / 8 / 9 | hora del día; `[ ]` pixelado; `, .` paleta |
| F9 / F10 | captura / grabar fotogramas para un GIF |

**Zona de emboscada (desmonte)**: las mismas, más:

| Tecla | Qué hace |
| --- | --- |
| Z | marcha atrás (la salida de emergencia: los primeros 40 m quedan libres) |
| X | adelante (lo frena el tapón de cuerpos) |
| C | parar |

## 3. Recorrido recomendado
1. **Día 1.** Pulsa Play. Pulsa **3** (x4). El reloj arranca a las 05:30. El Tren 1 sale de Valencia a las 06:00. Arriba a la derecha está el informe de la Colmena (cuando aparece algo, suena el motivo).
2. **Zona (H3).** Con la rueda, acércate a **Castellón** o a **Barracas**: empiezan «amenazadas» (la guarnición de la horda inicial). Entras en la zona. Pulsa **6** y mira cómo dispara el pelotón desde el tren. **4** añade zombis. **M** te devuelve al mapa.
3. **Caza (H4).** Hacia las 11:00 del día 1 el Tren 1 sale de Castellón y una manada lo sigue: aparece **ES-1** en el informe.
4. **Aviso y primer asedio.** Con x4 y unos 5–6 minutos reales llegas al día 2. A las 07:00 aparece la alerta **«Horda hacia Valencia en 3 h (10:00)»** (el objetivo se sortea con la semilla: puede ser otro nodo). Pulsa **J** para ir a ella. A las 10:00 el nodo pasa a **asediado** y entra **ES-2** en el informe. (Medido en el Editor: aviso a las 07:00, asedio a las 10:00 del día 2.)
5. **Emboscada y desmonte (H4).** Pulsa **F8** (emboscada de prueba) e **Intro**. Estás en el desmonte, con el tren parado, el tapón delante y la horda bajando por los taludes. Pulsa **6** (tropa a bordo), espera unos segundos (hay abatidos) y prueba **Z** (marcha atrás) y **C** (parar). **M** para salir: la red resuelve la emboscada, libera el tren y gasta la munición de su carga.
   - Si en la partida real no respondes en 30 min de juego, el tren pierde el 20 % de la carga. Una emboscada real necesita días de horario fijo: por eso F8 existe.

Una sesión limpia de punta a punta: unos 8 minutos.

## 4. Repeticiones doradas (un solo comando)
Con el Editor **cerrado** (si no, no se puede abrir el proyecto dos veces):

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath C:\Dev\TBD -executeMethod TBD.EditorTools.GoldenBatch.Run -quit -logFile -
```

Verifica **G1 a G5** (red, zona, tropas, bucle mapa-zona y desmonte), escribe `Captures\golden_editor.txt` y sale con código **0 si todo es idéntico** y 1 si algo difiere. Con el Editor abierto: menú **TBD → Repetición dorada (todas)**. En un proyecto limpio tardó 2 min 18 s (importación incluida); con el proyecto ya importado, ~40 s.

Las pruebas de lógica pura (173 comprobaciones, incluida la ecuación de masa y la prueba de reparto de asedios):

```bash
cd C:\Dev\TBD\Dotnet\TBD.Sim.Tests && dotnet run -c Release
```

Añadir `TBD_SEEDS=20` mide además 20 partidas completas contra la referencia de Hugo (tarda unos 5 minutos).

## 5. Qué es provisional
- **Arte.** El desmonte son escalones coloreados por altura, sin sombra ni relieve continuo. El tren apenas contrasta. La curva y el puente usan el desmonte como decorado. Nada de esto es el arte final.
- **Relieve del mapa.** Es de sustitución, no son datos reales (Copernicus sigue pendiente de registro; las cifras reales de elevación no se usan).
- **Balance.** Los números son los de Hugo, en `Assets/TBD/Data/*.csv`. La emboscada sin respuesta (20 %) y las utilidades de emboscada y corte son prototipos.
- **Horda inicial.** Es guarnición con correa y paisaje estático: no la recluta el director. Las amenazas del día 1 son las del escenario de prueba (dos guarniciones pegadas a Castellón y Barracas).
- **F8 y las teclas 1 y 4 de la zona** son de prueba. No valen para coop.
- **Una sola zona a la vez** y un solo encuentro de emboscada a la vez.
- **La tropa a pie** que se queda en la zona al salir se pierde (aún no es guarnición del nodo).
- **Coop** no existe todavía: lo que hay es lockstep con dos mundos en una misma máquina, y su prueba de desincronización.
