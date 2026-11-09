# Criterios de pieza 3D para TBD (6-nov-2026)

## Presupuestos (D-N54 y GDD §11)
| Pieza | Triángulos | Fuente |
|---|---|---|
| Vehículos y material ferroviario (locomotora, vagón, blindado 8×8) | 1.500–4.000 | Hunyuan + `procesar()` |
| Infantería y civiles | lo que traiga la base CC0 (Kenney «Animated Characters Retro», Quaternius) | se retoca, no se diezma |
| Edificios clave | 1.500–4.000 por módulo (propuesta, sin decidir) | Hunyuan o bloque |
| Zombis (horda DOTS) | lo mínimo; fuera de esta skill | — |

## Alturas reales orientativas (para `altura_m`; **sin verificar con planos**, contrastar antes de un lote)
| Pieza | Altura aprox. |
|---|---|
| Locomotora diésel de vía ancha | 4,0–4,3 m |
| Vagón de mercancías cerrado | 3,8–4,1 m |
| Plataforma con carga | 1,2 m + carga |
| Blindado 8×8 (casco, sin torre) | 2,3–2,6 m |
| Soldado de pie | 1,75–1,80 m |
| Muro HESCO de un módulo | 1,0–2,2 m según modelo |

## Umbrales que aplica `malla3d.py`
- Triángulos dentro del presupuesto (ERROR si se pasa; AVISO si se queda corto).
- 0 aristas no manifold, 0 geometría suelta, 0 islas con normales hacia dentro, escala (1,1,1): si no, ERROR.
- Altura real dentro del ±15 % si se pasa `altura_m`.
- Silueta tras diezmar ≥ 0,985 en frente, izquierda, arriba y vista RTS (1 px de tolerancia a 256 px).
- Colores por pieza: 10 como máximo (regla 9 de la estética, versión 3D). Ningún rojo (340000, 4c1008, 642030, 7c181c, 8c5048, 982420) ni el ámbar #e08c30 (regla 4 y Lucía, 6-nov).
- Un material por pieza (`MAT_Paleta32`, textura `T_Paleta32.png` de 32×16 px, filtro de punto).
- Nombres: `SM_<Pieza>` para la malla y `MAT_`/`T_` para material y textura.
