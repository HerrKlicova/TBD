# Herramientas externas evaluadas (6-nov-2026)

Leídas antes de usar. Nada de pago, nada con credenciales, ni Tripo ni Higgsfield.

## Ya instalado en el PC de Eugenio (verificado con el MCP el 6-nov)
| Herramienta | Qué aporta | Estado |
|---|---|---|
| **Blender MCP oficial** (Blender Lab, GPL-3.0, v1.0.x, requiere Blender 5.1+) | 26 herramientas: ejecutar Python, resúmenes de escena y de .blend, capturas, render, búsqueda en la API y el manual de Blender | Activo en Blender 5.2.1. El modo consola falla: falta `BLENDER_PATH` |
| **Image to 3D 1.0.3** (3DRedbox) | Hunyuan3D 2.1, **Hunyuan3D-2mv (4 vistas)** y TRELLIS.2 en local desde Blender | Hunyuan, 2mv y TRELLIS listos; Pixal3D y Hi3D sin instalar |
| Extensiones Tripo3d_Blender_Bridge y higgsfield_blender | — | Instaladas en Blender, **no se usan** (regla del proyecto) |

## Copiado al repo
| Herramienta | Licencia | Qué se tomó |
|---|---|---|
| arjun988/blender-skills (1.000★, 94 skills) | MIT | 8 skills de lectura en `referencias/arjun988-blender-skills/`. Son texto, sin código ni llamadas de red (revisado). Genéricas: el valor real de esta skill está en `malla3d.py` |

## Evaluado y no instalado
| Herramienta | Por qué no |
|---|---|
| MushroomFleet/BlenderRetopology-Skill (Apache-2.0) | Retopología manual en quads para animación; las piezas de TBD son rígidas y los personajes vienen de bases CC0 ya limpias |
| ahujasid/blender-mcp, ahora «MCP for Blender» (MIT) | Duplica al oficial y trae integraciones de generación (Rodin, Tripo, Hunyuan en la nube, Sketchfab) que no queremos. Se puede probar más adelante solo por `export_scene` |
| StraySpark MeshPrep | Addon comercial de reparación, UV, LOD y exportación. No se ha comprobado el precio: descartado por la regla de no pagar |
| Instant Meshes / QuadriFlow | Remallado a quads. QuadriFlow viene dentro de Blender (`object.quadriflow_remesh`); no hace falta para piezas rígidas trianguladas |
| 3D Print Toolbox (extensión oficial) | Sus comprobaciones (manifold, caras degeneradas, espesor) ya las hace `auditar()` sin instalar nada |

## Hunyuan3D multivista (verificado en el código de Tencent/Hunyuan3D-2 y del addon)
- Modelo `tencent/Hunyuan3D-2mv` (variantes normal, fast y turbo), licencia tencent-hunyuan-community (ver el aviso de la UE
  en la memoria del proyecto; Eugenio decidió seguir con Hunyuan el 5-nov).
- Entrada: diccionario con `front`, `left`, `back`, `right` (de 1 a 4 vistas). Orden interno: frente, frente +90° en sentido
  horario, detrás, frente +270°.
- El addon permite añadir Back, Left y Right junto a la imagen principal; da la forma con las vistas y pinta con Hunyuan.
