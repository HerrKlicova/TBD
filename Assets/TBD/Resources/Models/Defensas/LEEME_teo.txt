# Kit de defensas · Teo, 8-nov

Para Iker (Nules y Bétera) y Marcos (Sagunto). Lo encargó Mak el 8-nov a las 13:46. Las medidas son de Álvaro: nules-nivel.md §3 y sagunto-nivel.md §10.11 y §10.14. **Lo aprobó Lucía el 8-nov a las 13:54**, sin cambios. Medido sobre el LOD1 a tamaño de juego, ninguna pieza pasa del 0,3 % de píxeles en la banda de la horda. Las hojas están en /mnt/project-files/docs/arte/modelado-3d/defensas/.

Todas las carpetas comparten la misma `T_Paleta32.png`: material URP/Lit con `_BaseMap` = paleta. Fuera es −Y de Blender, que en Unity es +Z con yaw 0 (muros: oeste 270, sur 180, este 90 y norte 0). El origen está en el suelo. Los estados son los aprobados (construccion-reparar.md §1): Sano ≥70 %, Danado 30–70, MuyDanado 1–30 y Brecha 0, que no bloquea. Todas las medidas están SIN VERIFICAR.

| Carpeta | Piezas | Origen y forma |
|---|---|---|
| hesco | `SM_HescoEsquina`, `SM_HescoRemate` y `SM_HescoCorto1`…`Corto4` × 5 variantes × 4 estados (`_v<a>_<Variante>_e<n>_<Estado>`). Variantes y daños del tramo de 10 m aprobado (entregas/teo-modelado/muro-estados) | **Esquina** exterior: celda de esquina en el origen y brazos de 2 celdas hacia +X (fuera −Y) y hacia +Y (fuera −X). El recinto queda en +X +Y y los tramos empiezan a 2,78 m del origen. **Remate**: celda de esquina, brazo de 2 celdas hacia +X y una celda de vuelta hacia +Y (dentro); cierra el muro junto a las puertas y al edificio. **Corto**: de 1 a 4 celdas de 1,11 m, centrado, como el tramo de 10 m. Celdas de 2,1 m de alto. En Nules hay 4 esquinas, 10 remates y cortos para los restos (§3) |
| sacos | `SM_SacosEsquina` y `SM_SacosRemate` × 5 variantes × 4 estados | Como el HESCO, con un bloque de esquina de 1 × 1 m y brazos de 3,4 m: los tramos de 8 m empiezan a 3,9 m del origen. Sagunto pasa de HESCO a sacos en X 84,5 (Z 44 y Z 85) |
| banqueta | `SM_Banqueta` (5 m), `SM_BanquetaCorta` (2,5 m) y `SM_Nido` (3 × 2,4 m), en v0–v4 | Centro de la planta. El lado −Y va contra la cara interior del muro: en el oeste de Sagunto, yaw 270. Banqueta de 2 m de fondo y 0,4 m de alto: 4 de 5 m en Z 45–55 y 74–84, y 2 de 2,5 m en Z 59,25–61,75 y 67,25–69,75. Nido de 0,6 m de alto con parapeto de sacos, en X 72,0 y Z 57,5 y 71,5. Coste 0 en la simulación (§10.11) |
| valla_betera | `SM_VallaBetera_Malla` y `_MuroBajo`, e0–e4 | Tramo de 10 m centrado, a lo largo de X y fuera −Y (los postes caen en x −3,33, 0 y 3,33). **Por vida**: e0 Sana ≥85 %, e1 SanaGolpeada 70–85 (solo a la vista), e2 Danada 30–70, e3 MuyDanada 1–30 y e4 Brecha a 0 HP. Con 5000 HP: ≥4250, 3500–4250, 1500–3500, 1–1500 y 0. Todo cae hacia dentro (+Y) |

Auditoría y presupuesto OK en las 370 mallas.

Código: TBD `claude/modelado-3d-skill` b192749 (`bloques/defensa_piezas.py`, `hesco_piezas.py`, `sacos_piezas.py`, `banqueta_tiro.py` y `valla_estados.py`; en `muro_sacos_estados.py`, ajustes que no cambian el tramo de 8 m).
