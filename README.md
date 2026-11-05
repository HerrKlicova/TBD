# TBD

RTS táctico de supervivencia zombi en 3D con estética pixel art, hecho con Unity 6.3 LTS (6000.3.x), URP y Entities.
Título provisional. El diseño y su historial viven en mr.mak (ver `CLAUDE.md`).

## Abrir el proyecto
1. Unity Hub → *Add → Add project from disk* → esta carpeta, con el editor **6000.3.x**.
2. La primera apertura descarga los paquetes (Entities 1.5.0, Entities Graphics 1.5.0, URP 17.3.0, MCP for Unity 10.3.0)
   y genera `Library/`, que no se versiona. Necesita conexión a internet y Git en el PATH.
3. Comprobar en *Window → Package Manager* que no hay errores y que Burst es 1.8.25 o superior.
4. *Window → MCP for Unity*: iniciar el servidor y comprobar que el estado es «Connected».

## Estado
Plantilla «3D URP» del editor sin código de juego. Pendiente de la primera apertura en Unity.
