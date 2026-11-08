# Aeródromo de Bétera (D-N173): piezas nuevas · Teo, 8-nov

> **Revisión 2 (8-nov, tarde), con las fotos de Bétera de Paula (`referencias-reales/betera/`). Aprobada por Lucía.** Hay una pieza nueva, `torreta_vigia/`, y cambian `valla_base/`, `polvorin/` y `pino/`. Las demás siguen igual. Con todo esto, ninguna hoja pasa del 0,9 % de píxeles en la banda de la horda (Lucía midió como mucho un 0,4 %), y el d4d0cc está al 0 %. El hangar pasa a 40 × 25 m (decisión de Álvaro con la estimación de Paula), porque en 30 m no caben dos NH90.

Para Iker. Siguen el §5 de `docs/niveles/betera-nivel.md` (Álvaro). **Aprobadas por Lucía (8-nov, 13:03)** con tres cambios, ya hechos:
- La torre y la garita tenían sombras en la banda de la horda (L* 9–13). Ahora la carpintería va en 6c7874 y el cristal en 605c48.
- La fachada de la torre pasa de d4d0cc a b8b8b8.
- El pino queda SIN VERIFICAR hasta que haya foto.

Además, el tronco del pino y el interior del hangar se han sacado de la banda. Medido sobre el LOD1 a tamaño de juego, ninguna pieza pasa del 1,7 % de píxeles en la banda. Las hojas están en /mnt/project-files/docs/arte/modelado-3d/betera/.

Cada carpeta tiene 5 variantes (`SM_<Pieza>_v0..v4_LOD0/LOD1.fbx`) y su `T_Paleta32.png`, que es la misma en todas: material URP/Lit con `_BaseMap` = paleta, como el kit. La variante la elige la semilla de cada instancia. Fuera es −Y de Blender, que en Unity es +Z con yaw 0. El origen está en el suelo.

| Carpeta | Pieza | Origen y orientación | Hijos animables |
|---|---|---|---|
| torre_control | Torre de control de 4 × 4 m, unos 12,7 m hasta la azotea (14,7 m en la v4). Sale de las fotos AE1. Variantes: foto, abandonada, fortificada, con caseta y radar, y alta | Centro del fuste. La escalera va en +X, la caseta de la v3 en −X | — |
| hangar | Hangar de 40 × 25 m (r2), con bóveda baja de 12 m sobre muros de 8,5 m y portón de 32 × 8 m. Variantes: cerrado, entreabierto, a dos aguas, castigado y con red | Centro de la planta. El portón está en −Y (+Z en Unity): el nivel lo pone con yaw 0 | `Porton_O` y `Porton_E`: abrir = trasladar 16 m en X local hacia fuera (O a −X, E a +X) |
| tanque_combustible | Tanque horizontal de 12 × 3 m sobre cunas, con el eje a 2,1 m. Variantes: gris, verde, blanco, oxidado y con sacos | Centro, con el eje a lo largo de X | — |
| polvorin | Búnker cubierto de tierra (BT4) de 11 × 22 m, con muro frontal de 4 m y túmulo de 4,6 m. La huella es de unos 14,4 m de ancho. El frente queda 11,3 m por delante del origen y el talud trasero 13,5 m por detrás. Variantes: hierba, tierra, sacos, abierto con cajas, y red con venteos | Centro de la planta. La puerta está en −Y | `Puerta_O` y `Puerta_E` (bisagras en x = ∓1,5): abrir = girar −90° y +90° hacia fuera |
| valla_base | Tramo de valla de 10 m con malla de 2,5 m y el remate hacia fuera. Variantes: sana, sobre un muro bajo enfoscado de 1,2 m (BT5_2), rota, con lona y con concertina suelta | Centro del tramo, a lo largo de X. Se repite cada 10 m: los postes van en x = −3,33, 0 y 3,33, así que no se duplican | — |
| garita | Garita de 2,4 × 2,4 m con barrera de 6 m. Variantes: de obra, de chapa, con sacos, con marquesina y abandonada | Centro de la caseta. Ventana y barrera en −Y | `Barrera` (pivote en el poste): abrir = girar −80° sobre Y local |
| puerta_valla | Puerta principal de la foto AE1: hueco de 7 m, hojas de 2,2 m, pórtico de 5 m y placa del lema sin letras. Variantes: foto, sin pórtico, con chicane de sacos, con espino y forzada | Centro del hueco, a lo largo de X | `Hoja_O` y `Hoja_E` (bisagras en ∓3,5): abrir = girar +90° y −90° hacia dentro |
| pino | Pino carrasco (BT7), de 6 a 14 m, con corteza gris blanquecina y sin colisión. Variantes: típico, joven, inclinado, en parasol con el tronco desnudo y medio seco | Pie del tronco | — |
| torreta_vigia | **Nueva.** Torreta de vigilancia elevada de la valla (BT6 y BT5_3): fuste de 2,6 m con aspilleras, antepecho de 4,6 m, cabina, tejado volado de 4,4 m y foco; 6,7 m de alto. Variantes: verde de la foto, descolorida ocre, con sacos, abandonada, y con red y concertina. En el MVP van en (4; 121) y (252; 121), con yaw 0 (Álvaro, betera.json) | Centro del fuste. La escalera va en +X | — |

- El helipuerto v4 (helipuerto/) es la toma P1. Las demás piezas del nivel son del kit (§5).
- Colores: nada rojo ni ámbar. SIN VERIFICAR: todas las medidas son estimadas, menos la forma de la torre, que sale de la foto. El pino y las variantes de la revisión 2 salen de las fotos de Paula.
- Auditoría y presupuesto OK en las 80 mallas.
- Pendiente de medir: los FPS con unos 200 pinos en instancing (betera-nivel.md §5).

Código: TBD `claude/modelado-3d-skill`, 2cd964c, c1f26dc , 0a5f6e4 y 7ae0a89 (revisión 2, más `torreta_vigia.py`) (`bloques/torre_control.py`, `hangar.py`, `tanque_combustible.py`, `polvorin.py`, `valla_base.py`, `garita.py`, `puerta_valla.py` y `pino.py`).
