# Herramientas externas revisadas (6-nov-2026)

Estrellas, licencia y última actividad sacadas de la API de búsqueda de GitHub el 6-nov-2026 (**verificado**).
Se clonaron y se leyeron README, SKILL.md y licencia de 14; se buscaron llamadas de red en el código de las cuatro
principales (solo hay servidores locales de vista previa). **Ninguna se ha ejecutado**: el sistema de permisos de la
sesión bloqueó ejecutar código descargado, así que la calidad que prometen **no está verificada** por nosotros.

## Las que encajan (por código, sin gasto, MIT)
| Repo | ★ | Qué es | Para qué nos sirve |
|---|---|---|---|
| [patch-gg/pixelfx](https://github.com/patch-gg/pixelfx) | 5.409 | 244 efectos pixel art, cada uno un archivo JS; skill de Claude Code; exporta hojas con JSON estilo Aseprite (Unity lo lee) | **Fase D (VFX)**: impactos de bala, trazadoras, explosiones, polvo, humo. Recolorear con nuestra rampa en vez de generar. Node 18+, sin dependencias |
| [skulitom/Pixelbolt](https://github.com/skulitom/Pixelbolt) | 41 | Recetas JSON a PNG, atlas y GIF; CLI, servidor MCP local y skill; simulación de fluidos para fuego, humo y explosiones | Humo de locomotora, vapor y explosiones con simulación real; paleta compartida por `$ref`. Node 20+ |
| [crsolver/claude-pxl](https://github.com/crsolver/claude-pxl) | 597 | DSL `.px.js`: rejillas de texto, espejo, contorno automático, variantes por cambio de paleta, visor en vivo | Familias de iconos con variantes. Es la idea base de `pixelvm` |
| [Gamezxz/pixel-art-studio](https://github.com/Gamezxz/pixel-art-studio) | 105 | Skill en Python/Pillow: el modelo coloca cada píxel; `pixelpipe` limpia PNG generados y los fija a una paleta | Lista de revisión por pasada y limpieza de salidas de Pixelab |
| [mankidev/codixel](https://github.com/mankidev/codixel) | 125 | Guía (skill de 51 KB) de método: partir de referencia, huesos, tramado solo en fronteras | Lectura de método. Insiste en que Claude «no pinta de memoria»: rinde mejor desde una referencia |

## Útiles pero con un coste o una dependencia
| Repo | ★ | Por qué no ahora |
|---|---|---|
| [with-pebbly/aseprite-ai-artist](https://github.com/with-pebbly/aseprite-ai-artist), [willibrandon/pixel-plugin](https://github.com/willibrandon/pixel-plugin) (+ pixel-mcp), [diivi/aseprite-mcp](https://github.com/diivi/aseprite-mcp), [MalloyTheDev/aseprite-mcp](https://github.com/MalloyTheDev/aseprite-mcp) | 598 / 414 / 898 / 25 | Necesitan **Aseprite** (de pago, unos 20 $, o compilarlo desde el código fuente). Sin pagos: descartado salvo que Eugenio ya lo tenga |
| [abidoo22/Pixelorama-MCP](https://github.com/abidoo22/Pixelorama-MCP) | 40 | Pixelorama es gratis (MIT), pero el MCP dibuja en el editor abierto: lento para lotes |
| [marmikshah/atelier](https://github.com/marmikshah/atelier) | 26 | Editor sin interfaz en Rust con MCP; buena pinta, pero se instala con `curl … \| sh` y toca la configuración de Claude Code |
| [LucasJRamos/spritekiln](https://github.com/LucasJRamos/spritekiln) | 125 | Sprites de 8 direcciones desde modelos 3D en YAML. Nuestras unidades ya son 3D en Unity |
| [SbName/yoyopixel](https://github.com/SbName/yoyopixel) | 7 | Pixel art como HTML; no exporta PNG de juego directamente |

## Descartadas: gastan generaciones de otra IA
[0x0funky/agent-sprite-forge](https://github.com/0x0funky/agent-sprite-forge) (5.284 ★), [aldegad/sprite-gen](https://github.com/aldegad/sprite-gen) (3.729 ★),
[SpriteCook/skills](https://github.com/SpriteCook/skills), [ianlintner/ai-pixel-art-image-generation](https://github.com/ianlintner/ai-pixel-art-image-generation):
dependen de modelos de imagen o vídeo (GPT Image, Grok Imagine, Gemini o el servicio SpriteCook). No ahorran nada frente a Pixelab.

## Origen del método
Hugo Duprez, «How to make (good) pixel art effects with Claude» (Sprite Fusion): el código es arcilla, fíate de tus ojos y dale referencias.
pixelfx cita y adapta su código (MIT).
