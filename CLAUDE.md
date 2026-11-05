# TBD — RTS de supervivencia zombi (Unity 6.3 LTS)

Idioma de trabajo: **español**. Título provisional: TBD.

## Dónde está la información
- **mr.mak es la fuente viva del proyecto** (GDD, idea original congelada, cambios, decisiones, arte, audio):
  tarjeta `workspace/2026-11-05_rts-zombi/` en `C:\Dev\mr-mak-workspace`. Léela antes de tocar el diseño.
- Cada cambio de diseño se anota allí en `CAMBIOS.md` (identificador `C-xxx`) y cada decisión en `DECISIONES.md` (`D-xxx`).
- Guía de preparación del entorno: `docs/guia-preparacion.md` de mr-mak-workspace.

## Reglas
- **Unity 6.3 LTS (6000.3.x)**, URP, Entities 1.5.0 y Entities Graphics 1.5.0. No actualizar a Unity 6.4 o posterior sin una decisión nueva (Entities cambia de API entre versiones).
- **Nunca** escribas claves en archivos del repositorio. Se leen de variables de entorno (`.env.example` lista los nombres).
- No mover, renombrar ni borrar recursos fuera del Editor de Unity (se rompen los `.meta`). Commitear siempre los `.meta`.
- Burst debe ser 1.8.25 o superior (requisito del diseño de simulación determinista); comprobarlo al abrir el proyecto por primera vez.
- Reglas de arquitectura del diseño (hilo de diseño, GDD v1.1): tick fijo, cada acción del jugador como una orden, simulación determinista y prueba automática de desincronización. El cooperativo (lockstep determinista) es el hito 7, después del MVP.
- No escribir código del juego hasta que el diseño marque el primer hito.

## MCP
`.mcp.json` define `unityMCP` (servidor HTTP de MCP for Unity en `http://localhost:8080/mcp`, requiere Unity abierto con el servidor iniciado en *Window → MCP for Unity*), `elevenlabs` y `pixellab`. La primera vez Claude Code pide aprobar los servidores del proyecto.
