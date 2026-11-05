# Primera apertura del proyecto en Unity

Lista para el día en que Eugenio active la licencia. Cada paso dice qué se espera ver. Si algo no coincide, parar y avisar: no seguir a ciegas.
Lo marcado **(no verificado)** no se ha podido comprobar sin el editor abierto.

## 0. Antes de abrir (Eugenio, una vez)
1. Unity Hub → iniciar sesión → activar la licencia **Unity Personal**. Hasta entonces el editor se niega a arrancar ("No valid Unity Editor license found").
2. Hub → Projects → **Add → Add project from disk** → `C:\Dev\TBD`.
3. Editor: **6000.3.28f1** (6.3 LTS), ya instalado en `C:\Program Files\Unity\Hub\Editor\6000.3.28f1`. Si el Hub propone otra versión, no actualizar.

## 1. Primera apertura (tarda: resuelve paquetes)
- Aceptar el aviso de "Enter Safe Mode" **solo si aparece** y mirar el error que lo causa (consola) antes de salir del modo seguro.
- Esperado: **0 errores rojos** en la consola. Avisos amarillos de Entities Graphics o URP son normales la primera vez.
- Si hay errores de compilación, pegarlos tal cual: la lógica pura ya está probada fuera de Unity (49 comprobaciones), así que lo más probable es una API de Entities o Entities Graphics mal nombrada (`.claude/skills/tbd-dots/SKILL.md` lista lo verificado y lo no verificado).

## 2. Comprobaciones del Package Manager
| Paquete | Esperado |
|---|---|
| Entities | 1.5.0 |
| Entities Graphics | 1.5.0 |
| Universal RP | 17.3.0 |
| Burst | **≥ 1.8.25** (si sale otra, anotar cuál: el modo determinista lo exige) |
| MCP for Unity | 10.3.0 (paquete git, `#v10.3.0`) |

## 3. Escena del prototipo
1. Abrir `Assets/Scenes/SampleScene` (el bootstrap solo actúa en esa escena).
2. Pulsar Play. Esperado: 10.000 zombis, HUD con ticks y hash.
3. Teclas: `F3` prueba de desincronización (mundo B), `1` zombis al tejado, `2` portón, `5` volea, `B` modo construir muro, `F9` captura.
4. Medir y apuntar: fps con 10.000 zombis (objetivo 60), resultado de `F3`.

## 4. MCP for Unity (para que Claude controle el editor)
1. Ventana MCP for Unity → iniciar el servidor → estado **Connected** **(no verificado: nombre exacto del menú en 10.3.0)**.
2. El repositorio ya trae `.mcp.json` con la entrada `unityMCP` (tipo `http`). Al abrir la carpeta en Claude Code, aprobar el servidor cuando lo pida.
3. Probar: pedir a Claude que lea la jerarquía de la escena.

## 5. Plugin oficial de Unity para Claude Code
Comandos tomados del hilo "Herramientas IA", **no verificados por mí**; si alguno falla, mirar la documentación de Unity en vez de improvisar:

```
/plugin marketplace add Unity-Technologies/unity-agent-plugin
/plugin install unity@unity-agent-plugin
```

Después, comprobar que aparecen sus skills y probar la CLI de Unity si el plugin la expone. Si choca con MCP for Unity, usar uno de los dos como principal y dejar el otro apagado.

## 6. Cuando todo funcione
- Commit del estado de `ProjectSettings` y de los `.meta` generados (el `.gitignore` ya excluye `Library/`, `Temp/`, `Captures/`).
- Registrar en `docs/estado-hilos.md` del workspace mr.mak lo medido (fps, F3, Burst).
